using Brutal.ImGuiApi;
using Brutal.Numerics;
using KSA;
using KittenEngineerRedux.Analysis;
using KittenEngineerRedux.UI;

namespace KittenEngineerRedux.Editor;

internal static class EditorPanel
{
    private const float DefaultWidth = 300f;
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

        float defaultHeight = Math.Min(1200f, 420f + burn.Sequences.Count * PanelKit.RowHeight * 9f);
        float2 defaultPos = viewport.Position + new float2(viewport.Size.X - DefaultWidth - SidePadding, 60f);
        bool open = PanelKit.BeginWindow("Kitten Engineer Redux###KerEditorPanel"u8, defaultPos, new float2(DefaultWidth, defaultHeight));
        if (open)
        {
            float contentWidth = ImGui.GetContentRegionAvail().X;
            ImDrawListPtr drawList = ImGui.GetWindowDrawList();
            float2 origin = ImGui.GetCursorScreenPos();
            PanelKit.DrawAccentStrip(drawList, origin, ImGui.GetWindowSize().Y);

            float y = origin.Y;
            y = PanelKit.DrawSectionHeader(drawList, origin, contentWidth, y, "VEHICLE MASS");
            y = PanelKit.DrawRow(drawList, origin, contentWidth, y, "Dry mass", PanelKit.FormatMass(mass.DryMass));
            y = PanelKit.DrawRow(drawList, origin, contentWidth, y, "Wet mass", PanelKit.FormatMass(mass.WetMass));
            y = PanelKit.DrawRow(drawList, origin, contentWidth, y, "Propellant", PanelKit.FormatMass(mass.PropellantMass));
            y = PanelKit.DrawRow(drawList, origin, contentWidth, y, "Total parts", mass.PartCount.ToString());

            y += 6f;
            y = PanelKit.DrawTwoWayToggle(drawList, origin, contentWidth, y,
                "Altitude estimate", "KSA simulation", _useNativeStaging,
                "##KerEditorCustom", "##KerEditorNative", out bool clickedNative, out bool clickedCustom);
            bool conditionsChanged = false;
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

            y += 4f;
            y = PanelKit.DrawSectionHeader(drawList, origin, contentWidth, y, "STAGE PERFORMANCE");
            foreach (SequenceBurnInfo stage in burn.Sequences)
            {
                y = PanelKit.DrawSectionHeader(drawList, origin, contentWidth, y, $"STAGE {stage.SequenceNumber}");
                y = PanelKit.DrawRow(drawList, origin, contentWidth, y, "Delta-v / cumulative",
                    $"{stage.DeltaV:F0} / {stage.CumulativeDeltaV:F0} m/s");
                y = PanelKit.DrawRow(drawList, origin, contentWidth, y, "Burn / Isp",
                    $"{stage.BurnTime:F0} s / {stage.Isp:F0} s");
                y = PanelKit.DrawRow(drawList, origin, contentWidth, y, "Thrust / engines",
                    $"{PanelKit.FormatThrust(stage.Thrust)} / {stage.EngineCount}");
                y = PanelKit.DrawRow(drawList, origin, contentWidth, y, "TWR initial / max",
                    $"{stage.InitialTwr:F2} / {stage.MaxTwr:F2}");
                y = PanelKit.DrawRow(drawList, origin, contentWidth, y, "Wet / dry mass",
                    $"{PanelKit.FormatMass(stage.WetMass)} / {PanelKit.FormatMass(stage.DryMass)}");
                y = PanelKit.DrawRow(drawList, origin, contentWidth, y, "Craft parts remaining", stage.PartCount.ToString());
                if (stage.EngineCount > 0 && _useNativeStaging)
                    y = PanelKit.DrawRow(drawList, origin, contentWidth, y, "Feed diagnostics",
                        stage.FeedSummary, stage.HasFeedWarning);
            }
            y = PanelKit.DrawTotalRow(drawList, origin, contentWidth, y, "Total dV", $"{burn.TotalDeltaV:F0} m/s");
            ImGui.Dummy(new float2(contentWidth, y - origin.Y));
        }
        PanelKit.EndWindow();
    }
}
