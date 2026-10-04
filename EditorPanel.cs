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

    private static bool _useNativeStaging = true;
    private static bool _useAtmosphere = true;
    private static float _analysisAltitude;

    public static bool Visible { get; set; } = true;

    public static void Draw(VehicleEditor editor, GameViewport viewport)
    {
        if (!Visible)
            return;

        PartTree? parts = editor.EditingSpace.Parts;
        if (parts == null)
            return;

        VehicleMassSummary mass = MassAnalyzer.Analyze(parts);
        IParentBody? home = Universe.CurrentSystem?.HomeBody;
        float maxAnalysisAltitude = EnvironmentHelpers.GetAtmosphereHeight(home);
        _analysisAltitude = Math.Clamp(_analysisAltitude, 0f, maxAnalysisAltitude);
        float ambientPressure = _useAtmosphere
            ? EnvironmentHelpers.GetAtmosphericPressureAtAltitude(home, _analysisAltitude)
            : 0f;
        float surfaceGravity = EnvironmentHelpers.ComputeSurfaceGravity(home, _analysisAltitude);

        VehicleBurnAnalysis burn;
        bool usedNativeResults;
        if (_useNativeStaging)
        {
            burn = SequenceAnalyzer.AnalyzeNative(parts, surfaceGravity, recomputeIfDirty: true, out usedNativeResults);
        }
        else
        {
            burn = SequenceAnalyzer.Analyze(parts, mass.WetMass, ambientPressure, surfaceGravity);
            usedNativeResults = false;
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

            bool conditionsChanged = false;
            if (PanelKit.DrawCollapsibleSection(origin, y, "ANALYSIS CONDITIONS"u8, out float nextY))
            {
                y = nextY;
                y = PanelKit.DrawTwoWayToggle(drawList, origin, contentWidth, y,
                    "Altitude estimate", "KSA simulation", _useNativeStaging,
                    "##KerEditorCustom", "##KerEditorNative", out bool clickedNative, out bool clickedCustom);
                if (clickedNative)
                {
                    _useNativeStaging = true;
                    conditionsChanged = true;
                }
                if (clickedCustom)
                {
                    _useNativeStaging = false;
                    conditionsChanged = true;
                }

                if (_useNativeStaging)
                {
                    y = PanelKit.DrawRow(drawList, origin, contentWidth, y, "Performance source",
                        usedNativeResults ? "KSA stage simulation" : "KSA data unavailable; estimate");
                }
                else
                {
                    y = PanelKit.DrawTwoWayToggle(drawList, origin, contentWidth, y,
                        "Vacuum", "Atmosphere", _useAtmosphere,
                        "##KerEditorToggleVac", "##KerEditorToggleAtmo",
                        out bool clickedAtmosphere, out bool clickedVacuum);
                    if (clickedAtmosphere)
                    {
                        _useAtmosphere = true;
                        conditionsChanged = true;
                    }
                    if (clickedVacuum)
                    {
                        _useAtmosphere = false;
                        conditionsChanged = true;
                    }
                    if (_useAtmosphere && maxAnalysisAltitude > 0f)
                    {
                        y = PanelKit.DrawSliderRow(drawList, origin, contentWidth, y,
                            "Analysis altitude", "##KerEditorAnalysisAltitude"u8, ref _analysisAltitude,
                            0f, maxAnalysisAltitude, "%.0f m", out bool altitudeChanged);
                        conditionsChanged |= altitudeChanged;
                        y = PanelKit.DrawRow(drawList, origin, contentWidth, y, "Pressure",
                            PanelKit.FormatPressure(EnvironmentHelpers.GetAtmosphericPressureAtAltitude(home, _analysisAltitude)));
                    }
                    else if (_useAtmosphere)
                    {
                        y = PanelKit.DrawRow(drawList, origin, contentWidth, y, "Atmosphere", "Not available");
                    }
                }
                y += 4f;
            }
            else
            {
                y = nextY;
            }

            if (conditionsChanged)
            {
                if (_useNativeStaging)
                {
                    burn = SequenceAnalyzer.AnalyzeNative(parts, surfaceGravity, recomputeIfDirty: true, out usedNativeResults);
                }
                else
                {
                    ambientPressure = _useAtmosphere
                        ? EnvironmentHelpers.GetAtmosphericPressureAtAltitude(home, _analysisAltitude)
                        : 0f;
                    surfaceGravity = EnvironmentHelpers.ComputeSurfaceGravity(home, _analysisAltitude);
                    burn = SequenceAnalyzer.Analyze(parts, mass.WetMass, ambientPressure, surfaceGravity);
                    usedNativeResults = false;
                }
            }

            y = PanelKit.DrawSectionHeader(drawList, origin, contentWidth, y, "STAGE PERFORMANCE");
            y = StageListView.Draw(drawList, origin, contentWidth, y, burn, _useNativeStaging);

            PanelKit.EndContent(origin, contentWidth, y);
        }
        PanelKit.EndWindow();
    }
}