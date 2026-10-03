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
        if (body is not Astronomical astro)
            return 0f;

        var atmosphere = astro.GetAtmosphereReference();
        return atmosphere == null ? 0f : Math.Max(0f, (float)(double)atmosphere.Physical.Height);
    }

    public static float GetAtmosphericPressureAtAltitude(IParentBody? body, double altitudeMeters)
    {
        if (body is not Astronomical astro)
            return 0f;

        var atmosphere = astro.GetAtmosphereReference();
        return atmosphere == null
            ? 0f
            : (float)atmosphere.Physical.GetAtmosphericPressureAtAltitude(Math.Max(0.0, altitudeMeters));
    }

}