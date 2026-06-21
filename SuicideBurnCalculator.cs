using KSA;
using System;

namespace KittenEngineerRedux.Analysis;

internal readonly record struct SuicideBurnInfo(
    bool HasSufficientThrust,
    bool IsDescending,
    bool BurnNow,
    float BurnAltitude,
    float? TimeToBurn,
    float BurnDuration);

internal static class SuicideBurnCalculator
{
    public static SuicideBurnInfo Compute(Vehicle vehicle, OrbitSummary orbit)
    {
        double radarAltitude = vehicle.GetRadarAltitude();
        double verticalVelocity = orbit.VerticalVelocity;
        double surfaceSpeed = vehicle.GetSurfaceSpeed();
        
        bool isDescending = verticalVelocity < 0.0;
        double descentRate = -verticalVelocity;

        float ambientPressure = (float)vehicle.PhysicsEnvironment.AtmosphericPressure;
        ActiveEngineThrustInfo engines = ActiveEngineThrust.Compute(vehicle.Parts, ambientPressure);
        double massInitial = vehicle.TotalMass;

        if (engines.TotalThrust <= 0f || engines.TotalMassFlowRate <= 0f || massInitial <= 0.0)
            return new SuicideBurnInfo(false, isDescending, false, 0f, null, 0f);

        IParentBody parent = vehicle.Parent;
        double currentRadius = vehicle.Orbit.StateVectors.PositionCci.Length();
        double surfaceRadius = parent.GetNearSurfaceRadius();
        double gravityCurrent = currentRadius > 0.0 ? parent.Mu / (currentRadius * currentRadius) : 0.0;
        double gravitySurface = surfaceRadius > 0.0 ? parent.Mu / (surfaceRadius * surfaceRadius) : gravityCurrent;
        double localGravity = (gravityCurrent + gravitySurface) * 0.5;
        double massFlowRate = engines.TotalMassFlowRate;
        double initialThrustAccel = engines.TotalThrust / massInitial;

        if ((initialThrustAccel - localGravity) <= 0.0)
            return new SuicideBurnInfo(false, isDescending, false, 0f, null, 0f);

        double thrustToMassRatio = engines.TotalThrust / massInitial;
        double burnDuration = surfaceSpeed / (thrustToMassRatio + (0.5 * massFlowRate * surfaceSpeed / massInitial) - localGravity);

        if (burnDuration < 0.0 || double.IsNaN(burnDuration))
            return new SuicideBurnInfo(false, isDescending, false, 0f, null, 0f);

        double pitchAngle = surfaceSpeed > 0.0 ? System.Math.Asin(descentRate / surfaceSpeed) : System.Math.PI * 0.5;
        
        double massFinal = massInitial - (massFlowRate * burnDuration);
        if (massFinal < 0.1) massFinal = 0.1; 

        double averageThrustAccel = engines.TotalThrust / ((massInitial + massFinal) * 0.5);
        double netDecelDynamic = averageThrustAccel - (localGravity * System.Math.Sin(pitchAngle));

        if (netDecelDynamic <= 0.0)
            return new SuicideBurnInfo(false, isDescending, false, 0f, null, 0f);

        double burnDistance = (surfaceSpeed * surfaceSpeed) / (2.0 * netDecelDynamic);
        double burnAltitude = burnDistance * System.Math.Sin(pitchAngle);
        bool burnNow = radarAltitude <= burnAltitude;

        float? timeToBurn = null;
        if (!burnNow && isDescending)
        {
            timeToBurn = (float)((radarAltitude - burnAltitude) / descentRate);
        }

        return new SuicideBurnInfo(
            HasSufficientThrust: true,
            IsDescending: isDescending,
            BurnNow: burnNow,
            BurnAltitude: (float)burnAltitude,
            TimeToBurn: timeToBurn,
            BurnDuration: (float)burnDuration
        );
    }
}