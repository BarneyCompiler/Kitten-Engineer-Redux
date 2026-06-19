using KSA;

namespace KittenEngineerRedux.Analysis;

internal readonly record struct VehicleMassSummary(float DryMass, float PropellantMass, float WetMass);

internal static class MassAnalyzer
{
    public static VehicleMassSummary Analyze(PartTree parts)
    {
        float dry = parts.ComputeInertMassPropertiesAsmb().Props.Mass;
        float prop = parts.ComputePropellantMassPropertiesAsmb().Props.Mass;
        return new VehicleMassSummary(dry, prop, dry + prop);
    }
}