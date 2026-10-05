using System;
using Brutal.ImGuiApi;
using Brutal.Numerics;
using KSA;
using KittenEngineerRedux.Analysis;
using KittenEngineerRedux.UI;

namespace KittenEngineerRedux.Flight;

internal static class FlightHud
{
    private const float DefaultWidth = 340f;
    private const float SidePadding = 16f;
    private const float LowFuelFraction = 0.2f;
    private const double RadToDeg = 180.0 / System.Math.PI;

    private sealed class FrameData
    {
        public Vehicle Vehicle = default!;
        public OrbitSummary Orbit = default!;
        public SuicideBurnInfo SuicideBurn = default!;
        public VehicleBurnAnalysis Burn = default!;
        public ActualEnginePerformanceInfo Engine = default!;
        public bool UsedNativeResults;
        public float MaxAnalysisAltitude;
        public float MaximumThrust;
        public float ActualTwr;
        public float MaximumTwr;
        public float Mass;
        public float AccelerationG;
        public int ActiveSequence;
    }

    private static readonly FrameData Frame = new();

    private static readonly HudSection[] Sections =
    {
        new("live", "LIVE", DrawLiveSection),
        new("fuel", "FUEL", DrawFuelSection),
        new("orbit", "ORBIT", DrawOrbitSection),
        new("suicide", "SUICIDE BURN", DrawSuicideSection),
        new("stages", "STAGES", DrawStagesSection),
        new("elements", "ORBIT ELEMENTS", DrawElementsSection),
        new("planner", "ORBIT PLANNER", DrawPlannerSection),
        new("surface", "SURFACE", DrawSurfaceSection),
        new("history", "HISTORY", DrawHistorySection),
    };

    private static readonly string[] HistoryLabels = { "Altitude", "Speed", "TWR", "Thrust", "Accel", "Mass" };
    private static readonly string[] HistoryTitles = { "Altitude", "Surface speed", "Thrust-to-weight ratio", "Thrust", "Acceleration", "Vessel mass" };
    private static readonly string[] HistoryUnits = { "m", "m/s", "", "kN", "g", "kg" };
    private static readonly TelemetryMetric[] HistoryMetrics =
    {
        TelemetryMetric.Altitude, TelemetryMetric.SurfaceSpeed, TelemetryMetric.Twr,
        TelemetryMetric.Thrust, TelemetryMetric.Acceleration, TelemetryMetric.Mass,
    };

    private static bool _useNativeStaging = true;
    private static bool _useAtmosphere = true;
    private static bool _analysisAltitudeInitialized;
    private static float _analysisAltitude;
    private static double _targetOrbitAltitude = 200_000.0;
    private static TelemetryMetric _historyMetric = TelemetryMetric.Altitude;
    private static string _exportStatus = string.Empty;

    public static bool Visible { get; set; } = true;

    internal static bool UseNativeStaging
    {
        get => _useNativeStaging;
        set => _useNativeStaging = value;
    }

    internal static bool UseAtmosphere
    {
        get => _useAtmosphere;
        set => _useAtmosphere = value;
    }

    internal static double TargetOrbitAltitude
    {
        get => _targetOrbitAltitude;
        set => _targetOrbitAltitude = value;
    }

    internal static string HistoryMetricName
    {
        get => _historyMetric.ToString();
        set
        {
            if (Enum.TryParse(value, out TelemetryMetric metric))
                _historyMetric = metric;
        }
    }

    public static void Draw(Vehicle vehicle, GameViewport viewport)
    {
        SettingsStore.Tick();

        if (!Visible)
            return;

        FrameData f = Frame;
        f.Vehicle = vehicle;
        f.Orbit = OrbitSummaryCalculator.Compute(vehicle);
        f.SuicideBurn = SuicideBurnCalculator.Compute(vehicle, f.Orbit);

        f.MaxAnalysisAltitude = EnvironmentHelpers.GetAtmosphereHeight(vehicle.Parent);
        if (!_analysisAltitudeInitialized)
        {
            _analysisAltitude = Math.Clamp(Math.Max(0f, (float)vehicle.GetBarometricAltitude()), 0f, f.MaxAnalysisAltitude);
            _analysisAltitudeInitialized = true;
        }
        _analysisAltitude = Math.Clamp(_analysisAltitude, 0f, f.MaxAnalysisAltitude);
        float ambientPressure = _useAtmosphere
            ? EnvironmentHelpers.GetAtmosphericPressureAtAltitude(vehicle.Parent, _analysisAltitude)
            : 0f;
        float surfaceGravity = EnvironmentHelpers.ComputeSurfaceGravity(vehicle.Parent, _analysisAltitude);

        bool usedNativeResults;
        if (_useNativeStaging)
            f.Burn = SequenceAnalyzer.AnalyzeNative(vehicle.Parts, surfaceGravity, recomputeIfDirty: false, out usedNativeResults);
        else
        {
            f.Burn = SequenceAnalyzer.Analyze(vehicle.Parts, vehicle.TotalMass, ambientPressure, surfaceGravity);
            usedNativeResults = false;
        }
        f.UsedNativeResults = usedNativeResults;

        float livePressure = (float)vehicle.PhysicsEnvironment.AtmosphericPressure;
        f.Engine = ActiveEngineThrust.ComputeActual(vehicle.Parts);
        f.MaximumThrust = vehicle.ComputeActiveThrust(livePressure);
        float currentGravity = EnvironmentHelpers.ComputeSurfaceGravity(
            vehicle.Parent, Math.Max(0f, vehicle.GetBarometricAltitude()));
        f.Mass = vehicle.TotalMass;
        f.ActualTwr = f.Mass > 0f && currentGravity > 0f
            ? f.Engine.TotalThrust / (f.Mass * currentGravity)
            : 0f;
        f.MaximumTwr = f.Mass > 0f && currentGravity > 0f
            ? f.MaximumThrust / (f.Mass * currentGravity)
            : 0f;
        f.AccelerationG = (float)(vehicle.AccelerationBody.Length() / KSA.Constants.STANDARD_GRAVITY);
        TelemetryHistory.Record(vehicle, f.Engine, f.ActualTwr);

        f.ActiveSequence = vehicle.Parts.SequenceList.ActiveSequence;
        if (f.ActiveSequence <= 0)
            f.ActiveSequence = vehicle.Parts.SequenceList.GetNextSequenceNumber();

        ComputeFuelFractions(f.Burn, f.ActiveSequence, out float totalFuelFraction, out float stageFuelFraction);
        HudWidgetContext widgetContext = new(
            f.Orbit, f.Burn.TotalDeltaV, f.ActualTwr, f.MaximumTwr, f.Mass,
            f.Engine.TotalThrust, f.AccelerationG, totalFuelFraction, stageFuelFraction);
        HudAlerts.Evaluate(widgetContext);

        float defaultHeight = Math.Max(300f, Math.Min(640f, viewport.Size.Y - 120f));
        float2 defaultPos = viewport.Position + new float2(SidePadding, 60f);

        bool open = PanelKit.BeginWindow("Kitten Engineer Redux - Flight###KerFlightHud"u8, defaultPos, new float2(DefaultWidth, defaultHeight));
        if (open)
        {
            float contentWidth = ImGui.GetContentRegionAvail().X;
            ImDrawListPtr dl = ImGui.GetWindowDrawList();
            float2 origin = ImGui.GetCursorScreenPos();

            PanelKit.DrawAccentStrip(dl, origin, ImGui.GetWindowSize().Y);

            float y = origin.Y;

            float buildRowY = y;
            float settingsWidth = 78f;
            y = PanelKit.DrawRow(dl, origin, contentWidth - settingsWidth - 8f, y, "Build", BuildMarker.Stamp);
            ImGui.SetCursorScreenPos(new float2(origin.X + contentWidth - settingsWidth, buildRowY));
            if (ImGui.Button("Settings"u8, (float2?)new float2(settingsWidth, PanelKit.RowHeight)))
                ImGui.OpenPopup("KER HUD Settings");
            if (ImGui.BeginPopup("KER HUD Settings"))
            {
                PanelKit.DrawAppearanceSettings();
                HudWidgets.DrawCustomizer();
                HudAlerts.DrawConfigurator();
                if (ImGui.Button("Open data folder"u8, (float2?)null))
                    SettingsStore.OpenFolder(SettingsStore.DataDirectory);
                ImGui.EndPopup();
            }
            y += 4f;

            y = HudAlerts.DrawBanner(dl, origin, contentWidth, y);

            HudWidgets.Build(widgetContext, out string[] widgetLabels, out string[] widgetValues, out bool[] widgetWarnings);
            y = PanelKit.DrawStatTiles(dl, origin, contentWidth, y, widgetLabels, widgetValues, widgetWarnings);

            for (int i = 0; i < Sections.Length; i++)
                y = SectionHost.DrawDocked(Sections[i], dl, origin, contentWidth, y);

            PanelKit.EndContent(origin, contentWidth, y);
        }
        PanelKit.EndWindow();

        for (int i = 0; i < Sections.Length; i++)
            SectionHost.DrawFloating(Sections[i], viewport, i);
    }

    private static void ComputeFuelFractions(VehicleBurnAnalysis burn, int activeSequence, out float total, out float activeStage)
    {
        float remaining = 0f;
        float capacity = 0f;
        activeStage = float.NaN;
        foreach (SequenceBurnInfo stage in burn.Sequences)
        {
            if (stage.FuelCapacity <= 0f)
                continue;
            remaining += stage.FuelRemaining;
            capacity += stage.FuelCapacity;
            if (stage.SequenceNumber == activeSequence)
                activeStage = stage.FuelRemaining / stage.FuelCapacity;
        }
        total = capacity > 0f ? remaining / capacity : float.NaN;
    }

    private static float DrawLiveSection(ImDrawListPtr dl, float2 origin, float width, float y)
    {
        FrameData f = Frame;
        y = PanelKit.DrawRow(dl, origin, width, y, "Thrust (act / max)",
            $"{PanelKit.FormatThrust(f.Engine.TotalThrust)} / {PanelKit.FormatThrust(f.MaximumThrust)}");
        y = PanelKit.DrawRow(dl, origin, width, y, "TWR (act / max)",
            $"{f.ActualTwr:F2} / {f.MaximumTwr:F2}",
            HudAlerts.IsTriggered(AlertMetric.ActualTwr) || HudAlerts.IsTriggered(AlertMetric.MaximumTwr));
        y = PanelKit.DrawRow(dl, origin, width, y, "Vessel mass", PanelKit.FormatMass(f.Mass));
        y = PanelKit.DrawRow(dl, origin, width, y, "Acceleration", $"{f.AccelerationG:F2} g",
            HudAlerts.IsTriggered(AlertMetric.Acceleration));
        y = PanelKit.DrawRow(dl, origin, width, y, "Speed (vert / horiz)",
            $"{f.Orbit.VerticalVelocity:F1} / {f.Orbit.HorizontalVelocity:F1} m/s",
            HudAlerts.IsTriggered(AlertMetric.VerticalSpeed));
        y = PanelKit.DrawRow(dl, origin, width, y, "Mass flow",
            $"{f.Engine.TotalMassFlowRate:F2} kg/s");

        SequenceBurnInfo? activeStage = null;
        foreach (SequenceBurnInfo stage in f.Burn.Sequences)
        {
            if (stage.SequenceNumber == f.ActiveSequence && stage.EngineCount > 0)
            {
                activeStage = stage;
                break;
            }
        }
        string activeStageEndurance = activeStage.HasValue && f.Engine.TotalMassFlowRate > 0f
            ? PanelKit.FormatDuration(activeStage.Value.FuelMass / f.Engine.TotalMassFlowRate)
            : "N/A (no active burn)";
        y = PanelKit.DrawRow(dl, origin, width, y, "Stage fuel time", activeStageEndurance);
        y = PanelKit.DrawRow(dl, origin, width, y, "Fuel time (max thrust)",
            PanelKit.FormatDuration(f.Burn.TotalBurnTime));
        return y;
    }

    private static float DrawFuelSection(ImDrawListPtr dl, float2 origin, float width, float y)
    {
        FrameData f = Frame;
        float remaining = 0f;
        float capacity = 0f;
        foreach (SequenceBurnInfo stage in f.Burn.Sequences)
        {
            if (stage.FuelCapacity <= 0f)
                continue;
            remaining += stage.FuelRemaining;
            capacity += stage.FuelCapacity;
        }

        if (capacity <= 0f)
            return PanelKit.DrawRow(dl, origin, width, y, "Fuel", "No tanks found");

        float total = remaining / capacity;
        y = PanelKit.DrawGauge(dl, origin, width, y, "Total fuel",
            total,
            $"{PanelKit.FormatMass(remaining)} / {PanelKit.FormatMass(capacity)}  {total * 100f:F0}%",
            total < LowFuelFraction || HudAlerts.IsTriggered(AlertMetric.TotalFuel));

        foreach (SequenceBurnInfo stage in f.Burn.Sequences)
        {
            if (stage.FuelCapacity <= 0f)
                continue;

            float fraction = stage.FuelRemaining / stage.FuelCapacity;
            bool active = stage.SequenceNumber == f.ActiveSequence;
            bool warn = fraction < LowFuelFraction || (active && HudAlerts.IsTriggered(AlertMetric.StageFuel));
            string label = active ? $"Stage {stage.SequenceNumber} (active)" : $"Stage {stage.SequenceNumber}";
            y = PanelKit.DrawGauge(dl, origin, width, y, label, fraction,
                $"{PanelKit.FormatMass(stage.FuelRemaining)}  {fraction * 100f:F0}%", warn);
        }
        return y;
    }

    private static float DrawOrbitSection(ImDrawListPtr dl, float2 origin, float width, float y)
    {
        OrbitSummary orbit = Frame.Orbit;
        y = PanelKit.DrawRow(dl, origin, width, y, "Time to AP", orbit.TimeToApoapsis.HasValue ? PanelKit.FormatDuration(orbit.TimeToApoapsis.Value) : "N/A (unbound)",
            HudAlerts.IsTriggered(AlertMetric.TimeToApoapsis));
        y = PanelKit.DrawRow(dl, origin, width, y, "Time to PE", PanelKit.FormatDuration(orbit.TimeToPeriapsis),
            HudAlerts.IsTriggered(AlertMetric.TimeToPeriapsis));
        y = PanelKit.DrawRow(dl, origin, width, y, "Period", orbit.Period.HasValue ? PanelKit.FormatDuration(orbit.Period.Value) : "N/A (unbound)");
        y = PanelKit.DrawRow(dl, origin, width, y, "Orbital speed", $"{orbit.OrbitalSpeed:F1} m/s");
        y = PanelKit.DrawRow(dl, origin, width, y, "Inclination", $"{orbit.Inclination * RadToDeg:F2}\u00b0");
        y = PanelKit.DrawRow(dl, origin, width, y, "Eccentricity", $"{orbit.Eccentricity:F3}");
        return y;
    }

    private static float DrawSuicideSection(ImDrawListPtr dl, float2 origin, float width, float y)
    {
        SuicideBurnInfo info = Frame.SuicideBurn;
        if (!info.HasSufficientThrust)
            return PanelKit.DrawRow(dl, origin, width, y, "Status", "INSUFFICIENT THRUST", warning: true);

        if (!info.IsDescending)
            return PanelKit.DrawRow(dl, origin, width, y, "Status", "NOT DESCENDING");

        y = PanelKit.DrawRow(dl, origin, width, y, "Burn altitude", FormatAltitude(info.BurnAltitude));
        y = PanelKit.DrawRow(dl, origin, width, y, "Burn duration", $"{info.BurnDuration:F1} s");

        if (info.BurnNow)
            y = PanelKit.DrawRow(dl, origin, width, y, "Status", "BURN NOW", warning: true);
        else
            y = PanelKit.DrawRow(dl, origin, width, y, "Time to burn", info.TimeToBurn.HasValue ? PanelKit.FormatDuration(info.TimeToBurn.Value) : "N/A");

        return y;
    }

    private static float DrawStagesSection(ImDrawListPtr dl, float2 origin, float width, float y)
    {
        FrameData f = Frame;
        if (PanelKit.DrawCollapsibleSection(origin, y, "ANALYSIS SETTINGS"u8, out float settingsY))
        {
            y = settingsY;
            y = PanelKit.DrawTwoWayToggle(dl, origin, width, y,
                "Altitude estimate", "KSA simulation", _useNativeStaging,
                "##KerHudCustom", "##KerHudNative", out bool clickedNative, out bool clickedCustom);
            if (clickedNative)
                _useNativeStaging = true;
            if (clickedCustom)
                _useNativeStaging = false;

            if (_useNativeStaging)
            {
                y = PanelKit.DrawRow(dl, origin, width, y, "Performance source",
                    f.UsedNativeResults ? "KSA stage simulation" : "Native cache pending; estimate");
            }
            else
            {
                y = PanelKit.DrawTwoWayToggle(dl, origin, width, y,
                    "Vacuum", "Atmosphere", _useAtmosphere,
                    "##KerHudToggleVac", "##KerHudToggleAtmo",
                    out bool clickedAtmosphere, out bool clickedVacuum);
                if (clickedAtmosphere)
                    _useAtmosphere = true;
                if (clickedVacuum)
                    _useAtmosphere = false;
                if (_useAtmosphere && f.MaxAnalysisAltitude > 0f)
                {
                    y = PanelKit.DrawSliderRow(dl, origin, width, y,
                        "Analysis altitude", "##KerHudAnalysisAltitude"u8, ref _analysisAltitude,
                        0f, f.MaxAnalysisAltitude, "%.0f m", out _);
                    y = PanelKit.DrawRow(dl, origin, width, y, "Analysis pressure",
                        PanelKit.FormatPressure(EnvironmentHelpers.GetAtmosphericPressureAtAltitude(f.Vehicle.Parent, _analysisAltitude)));
                }
                else if (_useAtmosphere)
                {
                    y = PanelKit.DrawRow(dl, origin, width, y, "Atmosphere", "Not available");
                }
            }
            y += 4f;
        }
        else
        {
            y = settingsY;
        }

        y = StageListView.Draw(dl, origin, width, y, f.Burn, _useNativeStaging, f.ActiveSequence);
        return y;
    }

    private static float DrawElementsSection(ImDrawListPtr dl, float2 origin, float width, float y)
    {
        OrbitSummary orbit = Frame.Orbit;
        y = PanelKit.DrawRow(dl, origin, width, y, "Semi-major axis", FormatAltitude(orbit.SemiMajorAxis));
        y = PanelKit.DrawRow(dl, origin, width, y, "LAN", $"{orbit.LongitudeOfAscendingNode * RadToDeg:F2}\u00b0");
        y = PanelKit.DrawRow(dl, origin, width, y, "Argument of PE", $"{orbit.ArgumentOfPeriapsis * RadToDeg:F2}\u00b0");
        y = PanelKit.DrawRow(dl, origin, width, y, "Time to AN", orbit.TimeToAscendingNode.HasValue ? PanelKit.FormatDuration(orbit.TimeToAscendingNode.Value) : "N/A");
        y = PanelKit.DrawRow(dl, origin, width, y, "Time to DN", orbit.TimeToDescendingNode.HasValue ? PanelKit.FormatDuration(orbit.TimeToDescendingNode.Value) : "N/A");
        y = PanelKit.DrawRow(dl, origin, width, y, "Eccentricity vector", FormatVector(orbit.EccentricityVector));
        y = PanelKit.DrawRow(dl, origin, width, y, "Orbital normal", FormatVector(orbit.OrbitalNormal));
        return y;
    }

    private static float DrawPlannerSection(ImDrawListPtr dl, float2 origin, float width, float y)
    {
        y = PanelKit.DrawInputDoubleRow(dl, origin, width, y, "Target circular altitude",
            "##KerTargetOrbitAltitude"u8, ref _targetOrbitAltitude, 1000.0, 10000.0);
        OrbitTransferEstimate estimate = OrbitPlanner.EstimateCircularTransfer(Frame.Vehicle, _targetOrbitAltitude);
        if (estimate.IsValid)
        {
            y = PanelKit.DrawRow(dl, origin, width, y, "Departure / arrival dV",
                $"{estimate.DepartureDeltaV:F0} / {estimate.ArrivalDeltaV:F0} m/s");
            y = PanelKit.DrawRow(dl, origin, width, y, "Total transfer dV",
                $"{estimate.TotalDeltaV:F0} m/s");
            y = PanelKit.DrawRow(dl, origin, width, y, "Transfer time",
                PanelKit.FormatDuration(estimate.TransferTime));
        }
        y = PanelKit.DrawRow(dl, origin, width, y, "Model", estimate.Status,
            warning: !estimate.IsValid);
        return y;
    }

    private static float DrawSurfaceSection(ImDrawListPtr dl, float2 origin, float width, float y)
    {
        OrbitSummary orbit = Frame.Orbit;
        y = PanelKit.DrawRow(dl, origin, width, y, "Latitude", $"{orbit.Latitude:F4}\u00b0");
        y = PanelKit.DrawRow(dl, origin, width, y, "Longitude", $"{orbit.Longitude:F4}\u00b0");
        y = PanelKit.DrawRow(dl, origin, width, y, "Terrain altitude", FormatAltitude(orbit.TerrainAltitude));
        y = PanelKit.DrawRow(dl, origin, width, y, "Sea level altitude", FormatAltitude(orbit.SeaLevelAltitude),
            HudAlerts.IsTriggered(AlertMetric.Altitude));
        return y;
    }

    private static float DrawHistorySection(ImDrawListPtr dl, float2 origin, float width, float y)
    {
        int selectedMetric = Array.IndexOf(HistoryMetrics, _historyMetric);
        y = PanelKit.DrawChips(dl, origin, width, y, HistoryLabels, selectedMetric, "##KerHistChip", out int clickedMetric);
        if (clickedMetric >= 0)
        {
            _historyMetric = HistoryMetrics[clickedMetric];
            selectedMetric = clickedMetric;
        }
        selectedMetric = Math.Max(0, selectedMetric);

        string unit = HistoryUnits[selectedMetric];
        string graphTitle = unit.Length > 0
            ? $"{HistoryTitles[selectedMetric]} ({unit})"
            : HistoryTitles[selectedMetric];

        float? threshold = null;
        bool thresholdAbove = false;
        AlertMetric? alertMetric = AlertForHistory(_historyMetric);
        if (alertMetric.HasValue && HudAlerts.TryGetThreshold(alertMetric.Value, out float thresholdValue, out thresholdAbove))
            threshold = thresholdValue;

        ReadOnlySpan<float> history = TelemetryHistory.GetSamples(_historyMetric);
        y = GraphView.Draw(dl, origin, width, y, graphTitle, unit, history,
            (float)TelemetryHistory.SampleInterval, threshold, thresholdAbove, true);

        float buttonHeight = PanelKit.RowHeight;
        float x = origin.X;
        float clearWidth = ImGui.CalcTextSize("Clear").X + 18f;
        if (PanelKit.DrawMiniButton(dl, "Clear", "##KerHistClear", new float2(x, y), clearWidth, buttonHeight))
            TelemetryHistory.Clear();
        x += clearWidth + 6f;

        float exportWidth = ImGui.CalcTextSize("Export CSV").X + 18f;
        if (PanelKit.DrawMiniButton(dl, "Export CSV", "##KerHistExport", new float2(x, y), exportWidth, buttonHeight, true))
            _exportStatus = TelemetryHistory.ExportCsv();
        x += exportWidth + 6f;

        float folderWidth = ImGui.CalcTextSize("Open folder").X + 18f;
        if (PanelKit.DrawMiniButton(dl, "Open folder", "##KerHistFolder", new float2(x, y), folderWidth, buttonHeight))
            SettingsStore.OpenFolder(SettingsStore.ExportDirectory);
        y += buttonHeight + 6f;

        if (_exportStatus.Length > 0)
            y = PanelKit.DrawRow(dl, origin, width, y, "Export", _exportStatus);
        return y;
    }

    private static AlertMetric? AlertForHistory(TelemetryMetric metric) => metric switch
    {
        TelemetryMetric.Altitude => AlertMetric.Altitude,
        TelemetryMetric.Twr => AlertMetric.ActualTwr,
        TelemetryMetric.Acceleration => AlertMetric.Acceleration,
        _ => null,
    };

    internal static string FormatAltitude(double meters)
    {
        double abs = System.Math.Abs(meters);
        if (abs >= 1000.0)
            return $"{meters / 1000.0:F2} km";
        return $"{meters:F0} m";
    }

    private static string FormatVector(double3 vector)
    {
        return $"({vector.X:F2}, {vector.Y:F2}, {vector.Z:F2})";
    }
}