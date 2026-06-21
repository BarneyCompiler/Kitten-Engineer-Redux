using System;
using Brutal.Numerics;
using KSA;

namespace KittenEngineerRedux.Analysis;

internal readonly record struct OrbitSummary(
    double? ApoapsisAltitude,
    double PeriapsisAltitude,
    double? Period,
    double? TimeToApoapsis,
    double TimeToPeriapsis,
    double Inclination,
    double Eccentricity,
    double SemiMajorAxis,
    double LongitudeOfAscendingNode,
    double ArgumentOfPeriapsis,
    double OrbitalSpeed,
    double VerticalVelocity,
    double HorizontalVelocity,
    double Latitude,
    double Longitude,
    double TerrainAltitude,
    double SeaLevelAltitude);

internal static class OrbitSummaryCalculator
{
    public static OrbitSummary Compute(Vehicle vehicle)
    {
        Orbit orbit = vehicle.Orbit;
        bool isBound = orbit.Eccentricity < 1.0;
        double bodyRadius = orbit.Parent.MeanRadius;

        double? apoapsisAltitude = isBound ? orbit.Apoapsis - bodyRadius : (double?)null;
        double periapsisAltitude = orbit.Periapsis - bodyRadius;

        double? period = null;
        if (isBound && orbit.SemiMajorAxis > 0.0)
            period = 2.0 * Math.PI * Math.Sqrt(Math.Pow(orbit.SemiMajorAxis, 3.0) / orbit.Mu);

        SimTime now = Universe.GetElapsedSimTime();
        double? timeToApoapsis = isBound ? (vehicle.NextApoapsisTime - now).Seconds() : null;
        double timeToPeriapsis = (vehicle.NextPeriapsisTime - now).Seconds();

        double3 positionCci = orbit.StateVectors.PositionCci;
        double3 velocityCci = orbit.StateVectors.VelocityCci;
        double3 angularVelocityCci = orbit.Parent.GetAngularVelocityCci();
        double3 surfaceVelocity = velocityCci - double3.Cross(angularVelocityCci, positionCci);
        double3 radial = positionCci.NormalizeOrZero();
        double vertical = double3.Dot(surfaceVelocity, radial);
        double3 horizontalVector = surfaceVelocity - vertical * radial;
        double horizontal = horizontalVector.Length();

        double3 positionCcf = positionCci.Transform(vehicle.Parent.GetCci2Ccf());
        double3 lla = vehicle.Parent.GetLlaFromCcf(positionCcf);

        return new OrbitSummary(
            apoapsisAltitude,
            periapsisAltitude,
            period,
            timeToApoapsis,
            timeToPeriapsis,
            orbit.Inclination,
            orbit.Eccentricity,
            orbit.SemiMajorAxis,
            orbit.LongitudeOfAscendingNode,
            orbit.ArgumentOfPeriapsis,
            vehicle.OrbitalSpeed,
            vertical,
            horizontal,
            lla.X,
            lla.Y,
            vehicle.GetRadarAltitude(),
            vehicle.GetBarometricAltitude());
    }
}