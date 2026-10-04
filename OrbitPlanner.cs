using System;
using KSA;

namespace KittenEngineerRedux.Analysis;

internal readonly record struct OrbitTransferEstimate(
    bool IsValid,
    double CurrentAltitude,
    double TargetAltitude,
    double DepartureDeltaV,
    double ArrivalDeltaV,
    double TotalDeltaV,
    double TransferTime,
    string Status);

internal static class OrbitPlanner
{
    public static OrbitTransferEstimate EstimateCircularTransfer(Vehicle vehicle, double targetAltitude)
    {
        Orbit orbit = vehicle.Orbit;
        double currentRadius = orbit.StateVectors.PositionCci.Length();
        double currentAltitude = currentRadius - orbit.Parent.MeanRadius;
        double targetRadius = orbit.Parent.MeanRadius + targetAltitude;
        double mu = KSA.Constants.GRAVITATIONAL_CONSTANT * orbit.Parent.Mass;

        if (orbit.Eccentricity >= 1.0)
            return Invalid(currentAltitude, targetAltitude, "Requires a bound starting orbit");
        if (!double.IsFinite(targetAltitude) || targetAltitude < 0.0 || targetRadius <= 0.0)
            return Invalid(currentAltitude, targetAltitude, "Target altitude must be non-negative");
        if (currentRadius <= 0.0 || mu <= 0.0)
            return Invalid(currentAltitude, targetAltitude, "Orbit data unavailable");

        double currentCircularSpeed = Math.Sqrt(mu / currentRadius);
        double targetCircularSpeed = Math.Sqrt(mu / targetRadius);
        if (Math.Abs(targetRadius - currentRadius) < 0.01)
        {
            return new OrbitTransferEstimate(
                true, currentAltitude, targetAltitude, 0.0, 0.0, 0.0, 0.0,
                "Already at target radius; assumes circular, coplanar orbit");
        }

        double transferSemiMajorAxis = (currentRadius + targetRadius) * 0.5;
        double transferSpeedAtDeparture = Math.Sqrt(mu * (2.0 / currentRadius - 1.0 / transferSemiMajorAxis));
        double transferSpeedAtArrival = Math.Sqrt(mu * (2.0 / targetRadius - 1.0 / transferSemiMajorAxis));
        double departureDeltaV = Math.Abs(transferSpeedAtDeparture - currentCircularSpeed);
        double arrivalDeltaV = Math.Abs(targetCircularSpeed - transferSpeedAtArrival);
        double transferTime = Math.PI * Math.Sqrt(Math.Pow(transferSemiMajorAxis, 3.0) / mu);

        return new OrbitTransferEstimate(
            true,
            currentAltitude,
            targetAltitude,
            departureDeltaV,
            arrivalDeltaV,
            departureDeltaV + arrivalDeltaV,
            transferTime,
            "Coplanar Hohmann estimate; assumes a circular starting orbit");
    }

    private static OrbitTransferEstimate Invalid(double currentAltitude, double targetAltitude, string status)
    {
        return new OrbitTransferEstimate(false, currentAltitude, targetAltitude, 0.0, 0.0, 0.0, 0.0, status);
    }
}
