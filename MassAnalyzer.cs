using KSA;

namespace KittenEngineerRedux.Analysis;

internal readonly record struct VehicleMassSummary(float DryMass, float PropellantMass, float WetMass, int PartCount);

internal static class MassAnalyzer
{
    public static VehicleMassSummary Analyze(PartTree parts)
    {
        float dry = parts.ComputeInertMassPropertiesAsmb().Props.Mass;
        float prop = parts.ComputePropellantMassPropertiesAsmb().Props.Mass;
        int partCount = parts.Parts.Length - 1;

        return new VehicleMassSummary(dry, prop, dry + prop, partCount);
    }
}