using KSA;

namespace KittenEngineerRedux.Analysis;

internal static class EnvironmentHelpers
{
    public static float ComputeSurfaceGravity(IParentBody? body, double altitudeMeters = 0.0)
    {
        if (body == null)
            return 0f;
        double r = body.MeanRadius + Math.Max(0.0, altitudeMeters);
        if (r <= 0.0)
            return 0f;
        return (float)(Constants.GRAVITATIONAL_CONSTANT * body.Mass / (r * r));
    }

    public static float GetAtmosphereHeight(IParentBody? body)
    {
        var atmosphere = body?.GetPhysicalAtmosphere();
        return atmosphere == null ? 0f : Math.Max(0f, (float)atmosphere.Height);
    }

    public static float GetAtmosphericPressureAtAltitude(IParentBody? body, double altitudeMeters)
    {
        var atmosphere = body?.GetPhysicalAtmosphere();
        return atmosphere == null
            ? 0f
            : (float)atmosphere.ComputePressureAtAltitude(Math.Max(0.0, altitudeMeters));
    }

}