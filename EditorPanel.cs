using Brutal.ImGuiApi;
using Brutal.Numerics;
using KSA;
using KittenEngineerRedux.Analysis;
using KittenEngineerRedux.UI;

namespace KittenEngineerRedux.Editor;

internal static class EditorPanel
{
    private const float DefaultWidth = 340f;
    private const float SidePadding = 16f;

    private static readonly HudSection ConditionsSection = new("editorconditions", "ANALYSIS CONDITIONS", DrawConditionsSection);

    private static bool _useNativeStaging = true;
    private static bool _useAtmosphere = true;
    private static float _analysisAltitude;
    private static IParentBody? _home;
    private static float _maxAnalysisAltitude;
    private static bool _usedNativeResults;

    public static bool Visible { get; set; } = true;

    public static void Draw(VehicleEditor editor, GameViewport viewport)
    {
        if (!Visible)
            return;

        PartTree? parts = editor.EditingSpace.Parts;
        if (parts == null)
            return;

        VehicleMassSummary mass = MassAnalyzer.Analyze(parts);
        _home = Universe.CurrentSystem?.HomeBody;
        _maxAnalysisAltitude = EnvironmentHelpers.GetAtmosphereHeight(_home);
        _analysisAltitude = Math.Clamp(_analysisAltitude, 0f, _maxAnalysisAltitude);
        float ambientPressure = _useAtmosphere
            ? EnvironmentHelpers.GetAtmosphericPressureAtAltitude(_home, _analysisAltitude)
            : 0f;
        float surfaceGravity = EnvironmentHelpers.ComputeSurfaceGravity(_home, _analysisAltitude);

        VehicleBurnAnalysis burn;
        if (_useNativeStaging)
        {
            burn = SequenceAnalyzer.AnalyzeNative(parts, surfaceGravity, recomputeIfDirty: true, out bool usedNativeResults);
            _usedNativeResults = usedNativeResults;
        }
        else
        {
            burn = SequenceAnalyzer.Analyze(parts, mass.WetMass, ambientPressure, surfaceGravity);
            _usedNativeResults = false;
        }

        float estimatedHeight = 380f + burn.Sequences.Count * 22f;
        float maxHeight = Math.Max(300f, viewport.Size.Y - 120f);
        float defaultHeight = Math.Min(Math.Max(300f, estimatedHeight), maxHeight);
        float2 defaultPos = viewport.Position + new float2(viewport.Size.X - DefaultWidth - SidePadding, 60f);
        bool open = PanelKit.BeginWindow("Kitten Engineer Redux###KerEditorPanel"u8, defaultPos, new float2(DefaultWidth, defaultHeight));
        if (open)
        {
            float contentWidth = ImGui.GetContentRegionAvail().X;
            ImDrawListPtr drawList = ImGui.GetWindowDrawList();
            float2 origin = ImGui.GetCursorScreenPos();
            PanelKit.DrawAccentStrip(drawList, origin, ImGui.GetWindowSize().Y);

            float y = origin.Y;

            y = PanelKit.DrawStatTiles(drawList, origin, contentWidth, y,
                ["dV (m/s)", "WET MASS", "PARTS"],
                [$"{burn.TotalDeltaV:F0}", PanelKit.FormatMass(mass.WetMass), mass.PartCount.ToString()]);

            y = PanelKit.DrawSectionHeader(drawList, origin, contentWidth, y, "VEHICLE MASS");
            y = PanelKit.DrawRow(drawList, origin, contentWidth, y, "Dry mass", PanelKit.FormatMass(mass.DryMass));
            y = PanelKit.DrawRow(drawList, origin, contentWidth, y, "Propellant", PanelKit.FormatMass(mass.PropellantMass));
            y += 6f;

            y = SectionHost.DrawDocked(ConditionsSection, drawList, origin, contentWidth, y);

            y = PanelKit.DrawSectionHeader(drawList, origin, contentWidth, y, "STAGE PERFORMANCE");
            y = StageListView.Draw(drawList, origin, contentWidth, y, burn, _useNativeStaging);

            PanelKit.EndContent(origin, contentWidth, y);
        }
        PanelKit.EndWindow();

        SectionHost.DrawFloating(ConditionsSection, viewport, 0);
    }

    private static float DrawConditionsSection(ImDrawListPtr drawList, float2 origin, float width, float y)
    {
        y = PanelKit.DrawTwoWayToggle(drawList, origin, width, y,
            "Altitude estimate", "KSA simulation", _useNativeStaging,
            "##KerEditorCustom", "##KerEditorNative", out bool clickedNative, out bool clickedCustom);
        if (clickedNative)
            _useNativeStaging = true;
        if (clickedCustom)
            _useNativeStaging = false;

        if (_useNativeStaging)
        {
            y = PanelKit.DrawRow(drawList, origin, width, y, "Performance source",
                _usedNativeResults ? "KSA stage simulation" : "KSA data unavailable; estimate");
            return y;
        }

        y = PanelKit.DrawTwoWayToggle(drawList, origin, width, y,
            "Vacuum", "Atmosphere", _useAtmosphere,
            "##KerEditorToggleVac", "##KerEditorToggleAtmo",
            out bool clickedAtmosphere, out bool clickedVacuum);
        if (clickedAtmosphere)
            _useAtmosphere = true;
        if (clickedVacuum)
            _useAtmosphere = false;

        if (_useAtmosphere && _maxAnalysisAltitude > 0f)
        {
            y = PanelKit.DrawSliderRow(drawList, origin, width, y,
                "Analysis altitude", "##KerEditorAnalysisAltitude"u8, ref _analysisAltitude,
                0f, _maxAnalysisAltitude, "%.0f m", out _);
            y = PanelKit.DrawRow(drawList, origin, width, y, "Pressure",
                PanelKit.FormatPressure(EnvironmentHelpers.GetAtmosphericPressureAtAltitude(_home, _analysisAltitude)));
        }
        else if (_useAtmosphere)
        {
            y = PanelKit.DrawRow(drawList, origin, width, y, "Atmosphere", "Not available");
        }
        return y;
    }
}