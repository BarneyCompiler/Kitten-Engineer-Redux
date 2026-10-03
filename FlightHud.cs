using System;
using Brutal.ImGuiApi;
using Brutal.Numerics;
using KSA;
using KittenEngineerRedux.Analysis;
using KittenEngineerRedux.UI;

namespace KittenEngineerRedux.Flight;

internal static class FlightHud
{
    private const float DefaultWidth = 320f;
    private const float SidePadding = 16f;
    private const double RadToDeg = 180.0 / System.Math.PI;

    private static bool _useAtmosphere = true;
    private static bool _analysisAltitudeInitialized;
    private static float _analysisAltitude;

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

        VehicleBurnAnalysis burn = SequenceAnalyzer.Analyze(vehicle.Parts, vehicle.TotalMass, ambientPressure, surfaceGravity);
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

        float defaultHeight = Math.Min(1200f, 920f + burn.Sequences.Count * PanelKit.RowHeight * 6f);
        float2 defaultPos = viewport.Position + new float2(SidePadding, 60f);

        bool open = PanelKit.BeginWindow("Kitten Engineer Redux - Flight###KerFlightHud"u8, defaultPos, new float2(DefaultWidth, defaultHeight));
        if (open)
        {
            float contentWidth = ImGui.GetContentRegionAvail().X;
            ImDrawListPtr dl = ImGui.GetWindowDrawList();
            float2 origin = ImGui.GetCursorScreenPos();

            PanelKit.DrawAccentStrip(dl, origin, ImGui.GetWindowSize().Y);

            float y = origin.Y;
            y = PanelKit.DrawRow(dl, origin, contentWidth, y, "Build", BuildMarker.Stamp);
            y += 4f;

            if (PanelKit.DrawCollapsibleSection(origin, y, "LIVE"u8, out float nextY))
            {
                y = nextY;
                y = PanelKit.DrawRow(dl, origin, contentWidth, y, "Thrust actual / max",
                    $"{PanelKit.FormatThrust(actualEnginePerformance.TotalThrust)} / {PanelKit.FormatThrust(maximumAvailableThrust)}");
                y = PanelKit.DrawRow(dl, origin, contentWidth, y, "TWR actual / max",
                    $"{actualTwr:F2} / {maximumTwr:F2}");
                y = PanelKit.DrawRow(dl, origin, contentWidth, y, "Vessel mass", PanelKit.FormatMass(currentMass));
                y = PanelKit.DrawRow(dl, origin, contentWidth, y, "Acceleration",
                    $"{vehicle.AccelerationBody.Length() / KSA.Constants.STANDARD_GRAVITY:F2} g");
                y = PanelKit.DrawRow(dl, origin, contentWidth, y, "Current mass flow",
                    $"{actualEnginePerformance.TotalMassFlowRate:F2} kg/s");

                SequenceBurnInfo? activeStage = null;
                int activeSequenceNumber = vehicle.Parts.SequenceList.ActiveSequence;
                if (activeSequenceNumber <= 0)
                    activeSequenceNumber = vehicle.Parts.SequenceList.GetNextSequenceNumber();
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
                y = PanelKit.DrawRow(dl, origin, contentWidth, y, "Active-stage fuel time", activeStageEndurance);
                y = PanelKit.DrawRow(dl, origin, contentWidth, y, "Fuel exhaustion (max thrust)",
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
                y = PanelKit.DrawRow(dl, origin, contentWidth, y, "Apoapsis", orbit.ApoapsisAltitude.HasValue ? FormatAltitude(orbit.ApoapsisAltitude.Value) : "N/A (unbound)");
                y = PanelKit.DrawRow(dl, origin, contentWidth, y, "Periapsis", FormatAltitude(orbit.PeriapsisAltitude));
                y = PanelKit.DrawRow(dl, origin, contentWidth, y, "Period", orbit.Period.HasValue ? PanelKit.FormatDuration(orbit.Period.Value) : "N/A (unbound)");
                y = PanelKit.DrawRow(dl, origin, contentWidth, y, "Time to AP", orbit.TimeToApoapsis.HasValue ? PanelKit.FormatDuration(orbit.TimeToApoapsis.Value) : "N/A (unbound)");
                y = PanelKit.DrawRow(dl, origin, contentWidth, y, "Time to PE", PanelKit.FormatDuration(orbit.TimeToPeriapsis));
                y = PanelKit.DrawRow(dl, origin, contentWidth, y, "Time to AN", orbit.TimeToAscendingNode.HasValue ? PanelKit.FormatDuration(orbit.TimeToAscendingNode.Value) : "N/A");
                y = PanelKit.DrawRow(dl, origin, contentWidth, y, "Time to DN", orbit.TimeToDescendingNode.HasValue ? PanelKit.FormatDuration(orbit.TimeToDescendingNode.Value) : "N/A");
                y = PanelKit.DrawRow(dl, origin, contentWidth, y, "Inclination", $"{orbit.Inclination * RadToDeg:F2}\u00b0");
                y = PanelKit.DrawRow(dl, origin, contentWidth, y, "Eccentricity", $"{orbit.Eccentricity:F3}");
                y = PanelKit.DrawRow(dl, origin, contentWidth, y, "Eccentricity vector", FormatVector(orbit.EccentricityVector));
                y = PanelKit.DrawRow(dl, origin, contentWidth, y, "Orbital normal", FormatVector(orbit.OrbitalNormal));
                y = PanelKit.DrawRow(dl, origin, contentWidth, y, "Semi-Major Axis", FormatAltitude(orbit.SemiMajorAxis));
                y = PanelKit.DrawRow(dl, origin, contentWidth, y, "LAN", $"{orbit.LongitudeOfAscendingNode * RadToDeg:F2}\u00b0");
                y = PanelKit.DrawRow(dl, origin, contentWidth, y, "Argument of PE", $"{orbit.ArgumentOfPeriapsis * RadToDeg:F2}\u00b0");
                y = PanelKit.DrawRow(dl, origin, contentWidth, y, "Orbital Speed", $"{orbit.OrbitalSpeed:F1} m/s");
                y += 4f;
            }
            else
            {
                y = nextY;
            }

            if (PanelKit.DrawCollapsibleSection(origin, y, "VELOCITY"u8, out nextY))
            {
                y = nextY;
                y = PanelKit.DrawRow(dl, origin, contentWidth, y, "Vertical", $"{orbit.VerticalVelocity:F1} m/s");
                y = PanelKit.DrawRow(dl, origin, contentWidth, y, "Horizontal", $"{orbit.HorizontalVelocity:F1} m/s");
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
                y = PanelKit.DrawRow(dl, origin, contentWidth, y, "Terrain Alt", FormatAltitude(orbit.TerrainAltitude));
                y = PanelKit.DrawRow(dl, origin, contentWidth, y, "Sea Level Alt", FormatAltitude(orbit.SeaLevelAltitude));
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

            if (PanelKit.DrawCollapsibleSection(origin, y, "STAGE DELTA-V"u8, out nextY))
            {
                y = nextY;
                y = PanelKit.DrawTwoWayToggle(dl, origin, contentWidth, y, "Vacuum", "Atmosphere", _useAtmosphere,
                    "##KerHudToggleVac", "##KerHudToggleAtmo", out bool clickedAtmosphere, out bool clickedVacuum);
                bool conditionsChanged = false;
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

                if (conditionsChanged)
                {
                    ambientPressure = _useAtmosphere
                        ? EnvironmentHelpers.GetAtmosphericPressureAtAltitude(vehicle.Parent, _analysisAltitude)
                        : 0f;
                    surfaceGravity = EnvironmentHelpers.ComputeSurfaceGravity(vehicle.Parent, _analysisAltitude);
                    burn = SequenceAnalyzer.Analyze(vehicle.Parts, vehicle.TotalMass, ambientPressure, surfaceGravity);
                }

                y += 4f;
                foreach (SequenceBurnInfo stage in burn.Sequences)
                {
                    y = PanelKit.DrawSectionHeader(dl, origin, contentWidth, y, $"STAGE {stage.SequenceNumber}");
                    y = PanelKit.DrawRow(dl, origin, contentWidth, y, "Delta-v / cumulative",
                        $"{stage.DeltaV:F0} / {stage.CumulativeDeltaV:F0} m/s");
                    y = PanelKit.DrawRow(dl, origin, contentWidth, y, "Burn / Isp",
                        $"{stage.BurnTime:F0} s / {stage.Isp:F0} s");
                    y = PanelKit.DrawRow(dl, origin, contentWidth, y, "TWR initial / max",
                        $"{stage.InitialTwr:F2} / {stage.MaxTwr:F2}");
                    y = PanelKit.DrawRow(dl, origin, contentWidth, y, "Wet / dry mass",
                        $"{PanelKit.FormatMass(stage.WetMass)} / {PanelKit.FormatMass(stage.DryMass)}");
                    y = PanelKit.DrawRow(dl, origin, contentWidth, y, "Parts remaining", stage.PartCount.ToString());
                }
                y = PanelKit.DrawTotalRow(dl, origin, contentWidth, y, "Total dV", $"{burn.TotalDeltaV:F0} m/s");
                y += 4f;
            }
            else
            {
                y = nextY;
            }

            ImGui.Dummy(new float2(contentWidth, y - origin.Y));
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

        y = PanelKit.DrawRow(dl, origin, width, y, "Burn Altitude", FormatAltitude(info.BurnAltitude));
        y = PanelKit.DrawRow(dl, origin, width, y, "Burn Duration", $"{info.BurnDuration:F1} s");

        if (info.BurnNow)
            y = PanelKit.DrawRow(dl, origin, width, y, "Status", "BURN NOW", warning: true);
        else
            y = PanelKit.DrawRow(dl, origin, width, y, "Time to Burn", info.TimeToBurn.HasValue ? PanelKit.FormatDuration(info.TimeToBurn.Value) : "N/A");

        return y;
    }

    private static string FormatAltitude(double meters)
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