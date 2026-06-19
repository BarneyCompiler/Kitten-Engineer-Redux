using System;
using KSA;

namespace KittenEngineerRedux.Analysis;

internal static class MassHelpers
{
    public static float SumInertMass(ModuleList modules)
    {
        float mass = 0f;
        Span<InertMass> inerts = modules.Get<InertMass>();
        for (int i = 0; i < inerts.Length; i++)
            mass += inerts[i].MassPropertiesAsmb.Props.Mass;
        return mass;
    }

    public static float ComputeTankMaxMass(Tank tank)
    {
        float maxMass = 0f;
        foreach (Mole mole in tank.Moles)
            maxMass += ComputeMoleMaxMass(mole);
        return maxMass;
    }

    private static float ComputeMoleMaxMass(Mole mole) => mole.SubstancePhase switch
    {
        Liquid liquid => liquid.ComputeMass(mole.ContainerVolume),
        Solid solid => solid.ComputeMass(mole.ContainerVolume),
        _ => 0f,
    };
}