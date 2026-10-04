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
    private const double RadToDeg = 180.0 / System.Math.PI;

    private static readonly string[] HistoryLabels = { "Altitude", "Speed", "TWR", "Thrust", "Accel", "Mass" };
    private static readonly string[] HistoryTitles = { "Altitude", "Surface speed", "Thrust-to-weight ratio", "Thrust", "Acceleration", "Vessel mass" };
    private static readonly string[] HistoryUnits = { "m", "m/s", "", "N", "", "kg" };
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

    public static bool Visible { get; set; } = true;

    public static void Draw(Vehicle vehicle, GameViewport viewport)
    {
        if (!Visible)
            return;

        OrbitSummary orbit = OrbitSummaryCalculator.Compute(vehicle);
        SuicideBurnInfo suicideBurn = SuicideBurnCalculator.Compute(vehicle, orbit);

        float maxAnalysisAltitude = EnvironmentHelpers.GetAtmosphereHeight(vehicle.Parent);
        if (!_analysisAltitudeInitialized)
        {
            _analysisAltitude = Math.Clamp(Math.Max(0f, (float)vehicle.GetBarometricAltitude()), 0f, maxAnalysisAltitude);
            _analysisAltitudeInitialized = true;
        }
        _analysisAltitude = Math.Clamp(_analysisAltitude, 0f, maxAnalysisAltitude);
        float ambientPressure = _useAtmosphere
            ? EnvironmentHelpers.GetAtmosphericPressureAtAltitude(vehicle.Parent, _analysisAltitude)
            : 0f;
        float surfaceGravity = EnvironmentHelpers.ComputeSurfaceGravity(vehicle.Parent, _analysisAltitude);

        VehicleBurnAnalysis burn;
        bool usedNativeResults;
        if (_useNativeStaging)
            burn = SequenceAnalyzer.AnalyzeNative(vehicle.Parts, surfaceGravity, recomputeIfDirty: false, out usedNativeResults);
        else
        {
            burn = SequenceAnalyzer.Analyze(vehicle.Parts, vehicle.TotalMass, ambientPressure, surfaceGravity);
            usedNativeResults = false;
        }
        float livePressure = (float)vehicle.PhysicsEnvironment.AtmosphericPressure;
        ActualEnginePerformanceInfo actualEnginePerformance = ActiveEngineThrust.ComputeActual(vehicle.Parts);
        float maximumAvailableThrust = vehicle.ComputeActiveThrust(livePressure);
        float currentGravity = EnvironmentHelpers.ComputeSurfaceGravity(
            vehicle.Parent, Math.Max(0f, vehicle.GetBarometricAltitude()));
        float currentMass = vehicle.TotalMass;
        float actualTwr = currentMass > 0f && currentGravity > 0f
            ? actualEnginePerformance.TotalThrust / (currentMass * currentGravity)
            : 0f;
        float maximumTwr = currentMass > 0f && currentGravity > 0f
            ? maximumAvailableThrust / (currentMass * currentGravity)
            : 0f;
        float accelerationG = (float)(vehicle.AccelerationBody.Length() / KSA.Constants.STANDARD_GRAVITY);
        TelemetryHistory.Record(vehicle, actualEnginePerformance, actualTwr);

        int activeSequenceNumber = vehicle.Parts.SequenceList.ActiveSequence;
        if (activeSequenceNumber <= 0)
            activeSequenceNumber = vehicle.Parts.SequenceList.GetNextSequenceNumber();

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
                ImGui.EndPopup();
            }
            y += 4f;

            HudWidgetContext widgetContext = new(
                orbit, burn.TotalDeltaV, actualTwr, maximumTwr, currentMass,
                actualEnginePerformance.TotalThrust, accelerationG);
            HudWidgets.Build(widgetContext, out string[] widgetLabels, out string[] widgetValues);
            y = PanelKit.DrawStatTiles(dl, origin, contentWidth, y, widgetLabels, widgetValues);

            if (PanelKit.DrawCollapsibleSection(origin, y, "LIVE"u8, out float nextY))
            {
                y = nextY;
                y = PanelKit.DrawRow(dl, origin, contentWidth, y, "Thrust (act / max)",
                    $"{PanelKit.FormatThrust(actualEnginePerformance.TotalThrust)} / {PanelKit.FormatThrust(maximumAvailableThrust)}");
                y = PanelKit.DrawRow(dl, origin, contentWidth, y, "TWR (act / max)",
                    $"{actualTwr:F2} / {maximumTwr:F2}");
                y = PanelKit.DrawRow(dl, origin, contentWidth, y, "Vessel mass", PanelKit.FormatMass(currentMass));
                y = PanelKit.DrawRow(dl, origin, contentWidth, y, "Acceleration", $"{accelerationG:F2} g");
                y = PanelKit.DrawRow(dl, origin, contentWidth, y, "Speed (vert / horiz)",
                    $"{orbit.VerticalVelocity:F1} / {orbit.HorizontalVelocity:F1} m/s");
                y = PanelKit.DrawRow(dl, origin, contentWidth, y, "Mass flow",
                    $"{actualEnginePerformance.TotalMassFlowRate:F2} kg/s");

                SequenceBurnInfo? activeStage = null;
                foreach (SequenceBurnInfo stage in burn.Sequences)
                {
                    if (stage.SequenceNumber == activeSequenceNumber && stage.EngineCount > 0)
                    {
                        activeStage = stage;
                        break;
                    }
                }
                string activeStageEndurance = activeStage.HasValue && actualEnginePerformance.TotalMassFlowRate > 0f
                    ? PanelKit.FormatDuration(activeStage.Value.FuelMass / actualEnginePerformance.TotalMassFlowRate)
                    : "N/A (no active burn)";
                y = PanelKit.DrawRow(dl, origin, contentWidth, y, "Stage fuel time", activeStageEndurance);
                y = PanelKit.DrawRow(dl, origin, contentWidth, y, "Fuel time (max thrust)",
                    PanelKit.FormatDuration(burn.TotalBurnTime));
                y += 4f;
            }
            else
            {
                y = nextY;
            }

            if (PanelKit.DrawCollapsibleSection(origin, y, "ORBIT"u8, out nextY))
            {
                y = nextY;
                y = PanelKit.DrawRow(dl, origin, contentWidth, y, "Time to AP", orbit.TimeToApoapsis.HasValue ? PanelKit.FormatDuration(orbit.TimeToApoapsis.Value) : "N/A (unbound)");
                y = PanelKit.DrawRow(dl, origin, contentWidth, y, "Time to PE", PanelKit.FormatDuration(orbit.TimeToPeriapsis));
                y = PanelKit.DrawRow(dl, origin, contentWidth, y, "Period", orbit.Period.HasValue ? PanelKit.FormatDuration(orbit.Period.Value) : "N/A (unbound)");
                y = PanelKit.DrawRow(dl, origin, contentWidth, y, "Orbital speed", $"{orbit.OrbitalSpeed:F1} m/s");
                y = PanelKit.DrawRow(dl, origin, contentWidth, y, "Inclination", $"{orbit.Inclination * RadToDeg:F2}\u00b0");
                y = PanelKit.DrawRow(dl, origin, contentWidth, y, "Eccentricity", $"{orbit.Eccentricity:F3}");
                y += 4f;
            }
            else
            {
                y = nextY;
            }

            if (PanelKit.DrawCollapsibleSection(origin, y, "SUICIDE BURN"u8, out nextY))
            {
                y = nextY;
                y = DrawSuicideBurnSection(dl, origin, contentWidth, y, suicideBurn);
                y += 4f;
            }
            else
            {
                y = nextY;
            }

            if (PanelKit.DrawCollapsibleSection(origin, y, "STAGES"u8, out nextY))
            {
                y = nextY;

                if (PanelKit.DrawCollapsibleSection(origin, y, "ANALYSIS SETTINGS"u8, out float settingsY))
                {
                    y = settingsY;
                    bool conditionsChanged = false;
                    y = PanelKit.DrawTwoWayToggle(dl, origin, contentWidth, y,
                        "Altitude estimate", "KSA simulation", _useNativeStaging,
                        "##KerHudCustom", "##KerHudNative", out bool clickedNative, out bool clickedCustom);
                    if (clickedNative) { _useNativeStaging = true; conditionsChanged = true; }
                    if (clickedCustom) { _useNativeStaging = false; conditionsChanged = true; }

                    if (_useNativeStaging)
                    {
                        y = PanelKit.DrawRow(dl, origin, contentWidth, y, "Performance source",
                            usedNativeResults ? "KSA stage simulation" : "Native cache pending; estimate");
                    }
                    else
                    {
                        y = PanelKit.DrawTwoWayToggle(dl, origin, contentWidth, y,
                            "Vacuum", "Atmosphere", _useAtmosphere,
                            "##KerHudToggleVac", "##KerHudToggleAtmo",
                            out bool clickedAtmosphere, out bool clickedVacuum);
                        if (clickedAtmosphere) { _useAtmosphere = true; conditionsChanged = true; }
                        if (clickedVacuum) { _useAtmosphere = false; conditionsChanged = true; }
                        if (_useAtmosphere && maxAnalysisAltitude > 0f)
                        {
                            y = PanelKit.DrawSliderRow(dl, origin, contentWidth, y,
                                "Analysis altitude", "##KerHudAnalysisAltitude"u8, ref _analysisAltitude,
                                0f, maxAnalysisAltitude, "%.0f m", out bool altitudeChanged);
                            conditionsChanged |= altitudeChanged;
                            y = PanelKit.DrawRow(dl, origin, contentWidth, y, "Analysis pressure",
                                PanelKit.FormatPressure(EnvironmentHelpers.GetAtmosphericPressureAtAltitude(vehicle.Parent, _analysisAltitude)));
                        }
                        else if (_useAtmosphere)
                        {
                            y = PanelKit.DrawRow(dl, origin, contentWidth, y, "Atmosphere", "Not available");
                        }
                    }

                    if (conditionsChanged)
                    {
                        if (_useNativeStaging)
                            burn = SequenceAnalyzer.AnalyzeNative(vehicle.Parts, surfaceGravity, recomputeIfDirty: false, out usedNativeResults);
                        else
                        {
                            ambientPressure = _useAtmosphere
                                ? EnvironmentHelpers.GetAtmosphericPressureAtAltitude(vehicle.Parent, _analysisAltitude)
                                : 0f;
                            surfaceGravity = EnvironmentHelpers.ComputeSurfaceGravity(vehicle.Parent, _analysisAltitude);
                            burn = SequenceAnalyzer.Analyze(vehicle.Parts, vehicle.TotalMass, ambientPressure, surfaceGravity);
                            usedNativeResults = false;
                        }
                    }
                    y += 4f;
                }
                else
                {
                    y = settingsY;
                }

                y = StageListView.Draw(dl, origin, contentWidth, y, burn, _useNativeStaging, activeSequenceNumber);
                y += 4f;
            }
            else
            {
                y = nextY;
            }

            if (PanelKit.DrawCollapsibleSection(origin, y, "ORBIT ELEMENTS"u8, out nextY))
            {
                y = nextY;
                y = PanelKit.DrawRow(dl, origin, contentWidth, y, "Semi-major axis", FormatAltitude(orbit.SemiMajorAxis));
                y = PanelKit.DrawRow(dl, origin, contentWidth, y, "LAN", $"{orbit.LongitudeOfAscendingNode * RadToDeg:F2}\u00b0");
                y = PanelKit.DrawRow(dl, origin, contentWidth, y, "Argument of PE", $"{orbit.ArgumentOfPeriapsis * RadToDeg:F2}\u00b0");
                y = PanelKit.DrawRow(dl, origin, contentWidth, y, "Time to AN", orbit.TimeToAscendingNode.HasValue ? PanelKit.FormatDuration(orbit.TimeToAscendingNode.Value) : "N/A");
                y = PanelKit.DrawRow(dl, origin, contentWidth, y, "Time to DN", orbit.TimeToDescendingNode.HasValue ? PanelKit.FormatDuration(orbit.TimeToDescendingNode.Value) : "N/A");
                y = PanelKit.DrawRow(dl, origin, contentWidth, y, "Eccentricity vector", FormatVector(orbit.EccentricityVector));
                y = PanelKit.DrawRow(dl, origin, contentWidth, y, "Orbital normal", FormatVector(orbit.OrbitalNormal));
                y += 4f;
            }
            else
            {
                y = nextY;
            }

            if (PanelKit.DrawCollapsibleSection(origin, y, "ORBIT PLANNER"u8, out nextY))
            {
                y = nextY;
                y = PanelKit.DrawInputDoubleRow(dl, origin, contentWidth, y, "Target circular altitude",
                    "##KerTargetOrbitAltitude"u8, ref _targetOrbitAltitude, 1000.0, 10000.0);
                OrbitTransferEstimate estimate = OrbitPlanner.EstimateCircularTransfer(vehicle, _targetOrbitAltitude);
                if (estimate.IsValid)
                {
                    y = PanelKit.DrawRow(dl, origin, contentWidth, y, "Departure / arrival dV",
                        $"{estimate.DepartureDeltaV:F0} / {estimate.ArrivalDeltaV:F0} m/s");
                    y = PanelKit.DrawRow(dl, origin, contentWidth, y, "Total transfer dV",
                        $"{estimate.TotalDeltaV:F0} m/s");
                    y = PanelKit.DrawRow(dl, origin, contentWidth, y, "Transfer time",
                        PanelKit.FormatDuration(estimate.TransferTime));
                }
                y = PanelKit.DrawRow(dl, origin, contentWidth, y, "Model", estimate.Status,
                    warning: !estimate.IsValid);
                y += 4f;
            }
            else
            {
                y = nextY;
            }

            if (PanelKit.DrawCollapsibleSection(origin, y, "SURFACE"u8, out nextY))
            {
                y = nextY;
                y = PanelKit.DrawRow(dl, origin, contentWidth, y, "Latitude", $"{orbit.Latitude:F4}\u00b0");
                y = PanelKit.DrawRow(dl, origin, contentWidth, y, "Longitude", $"{orbit.Longitude:F4}\u00b0");
                y = PanelKit.DrawRow(dl, origin, contentWidth, y, "Terrain altitude", FormatAltitude(orbit.TerrainAltitude));
                y = PanelKit.DrawRow(dl, origin, contentWidth, y, "Sea level altitude", FormatAltitude(orbit.SeaLevelAltitude));
                y += 4f;
            }
            else
            {
                y = nextY;
            }

            if (PanelKit.DrawCollapsibleSection(origin, y, "HISTORY"u8, out nextY))
            {
                y = nextY;
                int selectedMetric = Array.IndexOf(HistoryMetrics, _historyMetric);
                y = PanelKit.DrawChips(dl, origin, contentWidth, y, HistoryLabels, selectedMetric, "##KerHistChip", out int clickedMetric);
                if (clickedMetric >= 0)
                {
                    _historyMetric = HistoryMetrics[clickedMetric];
                    selectedMetric = clickedMetric;
                }
                selectedMetric = Math.Max(0, selectedMetric);

                string graphTitle = HistoryUnits[selectedMetric].Length > 0
                    ? $"{HistoryTitles[selectedMetric]} ({HistoryUnits[selectedMetric]})"
                    : HistoryTitles[selectedMetric];
                ReadOnlySpan<float> history = TelemetryHistory.GetSamples(_historyMetric);
                y = PanelKit.DrawLineGraph(dl, origin, contentWidth, y, graphTitle, history);

                ImGui.SetCursorScreenPos(new float2(origin.X, y));
                if (ImGui.Button("Clear history"u8, (float2?)null))
                    TelemetryHistory.Clear();
                y += Math.Max(PanelKit.RowHeight, ImGui.GetFrameHeight()) + 4f;
            }
            else
            {
                y = nextY;
            }

            PanelKit.EndContent(origin, contentWidth, y);
        }
        PanelKit.EndWindow();
    }

    private static float DrawSuicideBurnSection(ImDrawListPtr dl, float2 origin, float width, float y, SuicideBurnInfo info)
    {
        if (!info.HasSufficientThrust)
        {
            return PanelKit.DrawRow(dl, origin, width, y, "Status", "INSUFFICIENT THRUST", warning: true);
        }

        if (!info.IsDescending)
        {
            return PanelKit.DrawRow(dl, origin, width, y, "Status", "NOT DESCENDING");
        }

        y = PanelKit.DrawRow(dl, origin, width, y, "Burn altitude", FormatAltitude(info.BurnAltitude));
        y = PanelKit.DrawRow(dl, origin, width, y, "Burn duration", $"{info.BurnDuration:F1} s");

        if (info.BurnNow)
            y = PanelKit.DrawRow(dl, origin, width, y, "Status", "BURN NOW", warning: true);
        else
            y = PanelKit.DrawRow(dl, origin, width, y, "Time to burn", info.TimeToBurn.HasValue ? PanelKit.FormatDuration(info.TimeToBurn.Value) : "N/A");

        return y;
    }

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