using KSA;

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
        IParentBody parent = vehicle.Parent;
        double radius = vehicle.Orbit.StateVectors.PositionCci.Length();
        double localGravity = radius > 0.0
            ? Constants.GRAVITATIONAL_CONSTANT * parent.Mass / (radius * radius)
            : 0.0;

        float ambientPressure = (float)vehicle.PhysicsEnvironment.AtmosphericPressure;
        ActiveEngineThrustInfo engines = ActiveEngineThrust.Compute(vehicle.Parts, ambientPressure);
        float mass = vehicle.TotalMass;

        double descentRate = -orbit.VerticalVelocity;
        bool isDescending = descentRate > 0.0;

        double thrustAccel = mass > 0f ? engines.TotalThrust / mass : 0.0;
        double netDeceleration = thrustAccel - localGravity;

        if (engines.TotalThrust <= 0f || netDeceleration <= 0.0)
            return new SuicideBurnInfo(false, isDescending, false, 0f, null, 0f);

        double surfaceSpeed = vehicle.GetSurfaceSpeed();
        double burnAltitude = surfaceSpeed * surfaceSpeed / (2.0 * netDeceleration);
        double burnDuration = surfaceSpeed / netDeceleration;

        double radarAltitude = vehicle.GetRadarAltitude();
        bool burnNow = radarAltitude <= burnAltitude;

        float? timeToBurn = null;
        if (!burnNow && isDescending)
            timeToBurn = (float)((radarAltitude - burnAltitude) / descentRate);

        return new SuicideBurnInfo(true, isDescending, burnNow, (float)burnAltitude, timeToBurn, (float)burnDuration);
    }
}