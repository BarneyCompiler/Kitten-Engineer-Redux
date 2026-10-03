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
    double? TimeToAscendingNode,
    double? TimeToDescendingNode,
    double Inclination,
    double Eccentricity,
    double3 EccentricityVector,
    double3 OrbitalNormal,
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

        UniverseTime now = Universe.GetElapsedTime();
        double? timeToApoapsis = isBound ? (vehicle.NextApoapsisTime - now).Seconds() : null;
        double timeToPeriapsis = (vehicle.NextPeriapsisTime - now).Seconds();

        double? timeToAscendingNode = null;
        double? timeToDescendingNode = null;
        if (isBound && Math.Abs(Math.Sin(orbit.Inclination)) > 1e-8)
        {
            double argumentOfPeriapsis = orbit.ArgumentOfPeriapsis;
            timeToAscendingNode = orbit.GetRemainingTimeTo(new TrueAnomaly(WrapRadians(-argumentOfPeriapsis))).Seconds();
            timeToDescendingNode = orbit.GetRemainingTimeTo(new TrueAnomaly(WrapRadians(Math.PI - argumentOfPeriapsis))).Seconds();
        }

        double3 positionCci = orbit.StateVectors.PositionCci;
        double3 velocityCci = orbit.StateVectors.VelocityCci;
        double3 radial = positionCci.NormalizeOrZero();
        double3 angularMomentum = double3.Cross(positionCci, velocityCci);
        double3 eccentricityVector = orbit.Mu > 0.0
            ? double3.Cross(velocityCci, angularMomentum) / orbit.Mu - radial
            : double3.Zero;
        double3 orbitalNormal = angularMomentum.NormalizeOrZero();
        double3 angularVelocityCci = orbit.Parent.GetAngularVelocityCci();
        double3 surfaceVelocity = velocityCci - double3.Cross(angularVelocityCci, positionCci);
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
            timeToAscendingNode,
            timeToDescendingNode,
            orbit.Inclination,
            orbit.Eccentricity,
            eccentricityVector,
            orbitalNormal,
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

    private static double WrapRadians(double angle)
    {
        angle %= Math.PI * 2.0;
        return angle < 0.0 ? angle + Math.PI * 2.0 : angle;
    }
}