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

    private static bool _useCurrentConditions = true;

    public static bool Visible { get; set; } = true;

    public static void Draw(Vehicle vehicle, Viewport viewport)
    {
        if (!Visible)
            return;

        OrbitSummary orbit = OrbitSummaryCalculator.Compute(vehicle);
        SuicideBurnInfo suicideBurn = SuicideBurnCalculator.Compute(vehicle, orbit);

        float ambientPressure = 0f;
        float surfaceGravity = 0f;
        if (_useCurrentConditions)
        {
            ambientPressure = (float)vehicle.PhysicsEnvironment.AtmosphericPressure;
            surfaceGravity = EnvironmentHelpers.ComputeSurfaceGravity(vehicle.Parent);
        }

        VehicleBurnAnalysis burn = SequenceAnalyzer.Analyze(vehicle.Parts, vehicle.TotalMass, ambientPressure, surfaceGravity);

        float defaultHeight = 600f + burn.Sequences.Count * PanelKit.RowHeight;
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

            if (PanelKit.DrawCollapsibleSection(origin, y, "ORBIT"u8, out float nextY))
            {
                y = nextY;
                y = PanelKit.DrawRow(dl, origin, contentWidth, y, "Apoapsis", orbit.ApoapsisAltitude.HasValue ? FormatAltitude(orbit.ApoapsisAltitude.Value) : "N/A (unbound)");
                y = PanelKit.DrawRow(dl, origin, contentWidth, y, "Periapsis", FormatAltitude(orbit.PeriapsisAltitude));
                y = PanelKit.DrawRow(dl, origin, contentWidth, y, "Period", orbit.Period.HasValue ? PanelKit.FormatDuration(orbit.Period.Value) : "N/A (unbound)");
                y = PanelKit.DrawRow(dl, origin, contentWidth, y, "Time to AP", orbit.TimeToApoapsis.HasValue ? PanelKit.FormatDuration(orbit.TimeToApoapsis.Value) : "N/A (unbound)");
                y = PanelKit.DrawRow(dl, origin, contentWidth, y, "Time to PE", PanelKit.FormatDuration(orbit.TimeToPeriapsis));
                y = PanelKit.DrawRow(dl, origin, contentWidth, y, "Inclination", $"{orbit.Inclination * RadToDeg:F2}\u00b0");
                y = PanelKit.DrawRow(dl, origin, contentWidth, y, "Eccentricity", $"{orbit.Eccentricity:F3}");
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
                y = PanelKit.DrawTwoWayToggle(dl, origin, contentWidth, y, "Vacuum", "Current", _useCurrentConditions,
                    "##KerHudToggleVac", "##KerHudToggleCurrent", out bool clickedCurrent, out bool clickedVac);
                if (clickedCurrent) _useCurrentConditions = true;
                if (clickedVac) _useCurrentConditions = false;

                y += 4f;
                foreach (SequenceBurnInfo stage in burn.Sequences)
                {
                    string label = $"Stage {stage.SequenceNumber}";
                    string value = $"{stage.DeltaV:F0} m/s  Isp {stage.Isp:F0}s  TWR {stage.Twr:F2}";
                    y = PanelKit.DrawRow(dl, origin, contentWidth, y, label, value);
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
}