using System;
using System.Collections.Generic;
using System.Text;
using Brutal.Numerics;
using KSA;

namespace KittenEngineerRedux.Analysis;

internal record struct SequenceBurnInfo
{
    public int SequenceNumber;
    public bool IsActivated;
    public float DeltaV;
    public float CumulativeDeltaV;
    public float BurnTime;
    public float Thrust;
    public float ExhaustVelocity;
    public float Isp;
    public float StartMass;
    public float EndMass;
    public float FuelMass;
    public float MaxFuelMass;
    public float FuelFraction;
    public float MassFlowRate;
    public float InitialTwr;
    public float MaxTwr;
    public float WetMass;
    public float DryMass;
    public int PartCount;
    public float JettisonedMass;
    public int EngineCount;
    public bool HasFeedWarning;
    public string FeedSummary;
}

internal record struct VehicleBurnAnalysis
{
    public List<SequenceBurnInfo> Sequences;
    public float TotalDeltaV;
    public float TotalBurnTime;
}

internal static class SequenceAnalyzer
{
    private const float MinMassFlowRate = 1e-6f;
    private const float MinDryMass = 1f;

    private static readonly List<SequenceBurnInfo> _pooledSequences = new();
    private static readonly HashSet<uint> _pooledJettisonedPartIds = new();
    private static readonly HashSet<ulong> _pooledFuelClaimedTankIds = new();
    private static readonly List<EngineController> _pooledEngines = new();

    public static void ResetPools()
    {
        _pooledSequences.Clear();
        _pooledJettisonedPartIds.Clear();
        _pooledFuelClaimedTankIds.Clear();
        _pooledEngines.Clear();
    }

    public static VehicleBurnAnalysis Analyze(PartTree parts, float totalMass, float ambientPressure, float surfaceGravity)
    {
        _pooledSequences.Clear();
        _pooledJettisonedPartIds.Clear();
        _pooledFuelClaimedTankIds.Clear();

        var result = new VehicleBurnAnalysis
        {
            Sequences = _pooledSequences,
            TotalDeltaV = 0f,
            TotalBurnTime = 0f
        };

        ReadOnlySpan<Sequence> sequences = parts.SequenceList.Sequences;
        ReadOnlySpan<MoleState> moleStates = parts.Moles.States;
        float currentMass = totalMass;
        float currentDryMass = parts.ComputeInertMassPropertiesAsmb().Props.Mass;

        for (int si = 0; si < sequences.Length; si++)
        {
            Sequence sequence = sequences[si];
            if (sequence.Parts.IsEmpty)
                continue;

            (float jettisonedMass, float jettisonedDryMass) = ComputeJettisonedMass(
                sequence, moleStates, _pooledJettisonedPartIds, _pooledFuelClaimedTankIds);
            currentMass -= jettisonedMass;
            currentDryMass = Math.Max(0f, currentDryMass - jettisonedDryMass);

            CollectEngines(sequence, _pooledJettisonedPartIds, sequence.Activated);
            if (_pooledEngines.Count == 0)
                continue;

            float totalThrust = 0f;
            float totalFlowRate = 0f;
            if (ambientPressure > 0f)
            {
                foreach (EngineController engine in _pooledEngines)
                {
                    var data = RocketControllerData.ComputeFromCores(engine.Cores.AsSpan(), ambientPressure, 1f);
                    totalThrust += data.ThrustMax.Length();
                    totalFlowRate += data.MassFlowRateMax;
                }
            }
            else
            {
                foreach (EngineController engine in _pooledEngines)
                {
                    totalThrust += engine.VacuumData.ThrustMax.Length();
                    totalFlowRate += engine.VacuumData.MassFlowRateMax;
                }
            }

            if (totalFlowRate < MinMassFlowRate)
                continue;

            float ve = totalThrust / totalFlowRate;
            float isp = (float)(ve / Constants.STANDARD_GRAVITY);

            var (fuelMass, maxFuelMass) = ComputeSequenceFuel(_pooledEngines, _pooledFuelClaimedTankIds, moleStates);
            float fuelFraction = maxFuelMass > 0f ? fuelMass / maxFuelMass : 0f;

            float burnableFuel = fuelMass;
            float maxBurnable = currentMass - MinDryMass;
            if (burnableFuel > maxBurnable)
                burnableFuel = Math.Max(0f, maxBurnable);

            float startMass = currentMass;
            float endMass = currentMass - burnableFuel;
            float dv = burnableFuel > 0f ? ve * MathF.Log(startMass / endMass) : 0f;
            float burnTime = burnableFuel / totalFlowRate;
            float initialTwr = surfaceGravity > 0f ? totalThrust / (startMass * surfaceGravity) : 0f;
            float maxTwr = surfaceGravity > 0f && endMass > 0f ? totalThrust / (endMass * surfaceGravity) : 0f;
            float cumulativeDeltaV = result.TotalDeltaV + dv;

            result.Sequences.Add(new SequenceBurnInfo
            {
                SequenceNumber = sequence.Number,
                IsActivated = sequence.Activated,
                DeltaV = dv,
                CumulativeDeltaV = cumulativeDeltaV,
                BurnTime = burnTime,
                Thrust = totalThrust,
                ExhaustVelocity = ve,
                Isp = isp,
                StartMass = startMass,
                EndMass = endMass,
                FuelMass = fuelMass,
                MaxFuelMass = maxFuelMass,
                FuelFraction = fuelFraction,
                MassFlowRate = totalFlowRate,
                InitialTwr = initialTwr,
                MaxTwr = maxTwr,
                WetMass = startMass,
                DryMass = currentDryMass,
                PartCount = CountRemainingParts(parts, _pooledJettisonedPartIds),
                JettisonedMass = jettisonedMass,
                EngineCount = _pooledEngines.Count
            });

            result.TotalDeltaV += dv;
            result.TotalBurnTime += burnTime;
            currentMass = endMass;
        }

        return result;
    }

    public static VehicleBurnAnalysis AnalyzeNative(
        PartTree parts, float surfaceGravity, bool recomputeIfDirty, out bool usedNativeResults)
    {
        SequencePerformanceList performanceList = parts.PerformanceSequences;
        if (recomputeIfDirty)
            performanceList.RecomputeIfDirty();

        ReadOnlySpan<Sequence> sequences = parts.SequenceList.Sequences;
        ReadOnlySpan<SequencePerformance> performance = performanceList.PerformanceSequences;
        if (performanceList.IsDirty || performance.Length != sequences.Length)
        {
            usedNativeResults = false;
            VehicleMassSummary mass = MassAnalyzer.Analyze(parts);
            return Analyze(parts, mass.WetMass, 0f, surfaceGravity);
        }

        _pooledSequences.Clear();
        var result = new VehicleBurnAnalysis
        {
            Sequences = _pooledSequences,
            TotalDeltaV = 0f,
            TotalBurnTime = 0f
        };

        usedNativeResults = true;
        for (int i = 0; i < sequences.Length; i++)
        {
            Sequence sequence = sequences[i];
            SequencePerformance native = performance[i];
            List<SequencePhaseInfo>? phases = native.Phases;
            float burnTime = 0f;
            if (phases != null)
            {
                foreach (SequencePhaseInfo phase in phases)
                    burnTime += phase.Duration;
            }
            if (burnTime <= 0f && native.MassFlowRate > 0f)
                burnTime = native.BurnedFuelMass / native.MassFlowRate;

            int engineCount = 0;
            HashSet<Part>? attachedParts = native.AttachedParts;
            if (attachedParts != null)
            {
                foreach (Part part in attachedParts)
                {
                    Span<EngineController> engines = part.Modules.Get<EngineController>();
                    for (int engineIndex = 0; engineIndex < engines.Length; engineIndex++)
                    {
                        if (engines[engineIndex].Sequence == sequence.Number)
                            engineCount++;
                    }
                }
            }
            if (phases != null)
            {
                foreach (SequencePhaseInfo phase in phases)
                    engineCount = Math.Max(engineCount, phase.ActiveEngineCount);
            }

            string feedSummary = BuildFeedSummary(native.Propellants);
            bool feedWarning = engineCount > 0 &&
                (native.DeltaV <= 0f || native.BurnedFuelMass <= 0f || native.Propellants == null || native.Propellants.Count == 0);
            if (feedWarning)
                feedSummary = "No usable propellant burn; check tanks, mix, and flow links";

            float endMass = Math.Max(0f, native.WetMass - native.BurnedFuelMass);
            float initialTwr = native.WetMass > 0f && surfaceGravity > 0f
                ? native.Thrust / (native.WetMass * surfaceGravity)
                : native.Twr;
            float maxTwr = initialTwr;
            if (phases != null && surfaceGravity > 0f)
            {
                float phaseMass = native.WetMass;
                foreach (SequencePhaseInfo phase in phases)
                {
                    if (phaseMass > 0f)
                        maxTwr = Math.Max(maxTwr, phase.Thrust / (phaseMass * surfaceGravity));
                    phaseMass = Math.Max(1f, phaseMass - phase.MassFlowRate * phase.Duration);
                    maxTwr = Math.Max(maxTwr, phase.Thrust / (phaseMass * surfaceGravity));
                }
            }
            result.Sequences.Add(new SequenceBurnInfo
            {
                SequenceNumber = sequence.Number,
                IsActivated = sequence.Activated,
                DeltaV = native.DeltaV,
                CumulativeDeltaV = result.TotalDeltaV + native.DeltaV,
                BurnTime = burnTime,
                Thrust = native.Thrust,
                ExhaustVelocity = (float)(native.Isp * KSA.Constants.STANDARD_GRAVITY),
                Isp = native.Isp,
                StartMass = native.WetMass,
                EndMass = endMass,
                FuelMass = native.FuelMass,
                MaxFuelMass = native.FuelMass,
                FuelFraction = native.FuelMass > 0f ? native.BurnedFuelMass / native.FuelMass : 0f,
                MassFlowRate = native.MassFlowRate,
                InitialTwr = initialTwr,
                MaxTwr = maxTwr,
                WetMass = native.WetMass,
                DryMass = native.InertMass,
                PartCount = CountAttachedParts(attachedParts),
                JettisonedMass = 0f,
                EngineCount = engineCount,
                HasFeedWarning = feedWarning,
                FeedSummary = feedSummary
            });
            result.TotalDeltaV += native.DeltaV;
            result.TotalBurnTime += burnTime;
        }
        return result;
    }

    private static string BuildFeedSummary(List<SequencePropellantInfo>? propellants)
    {
        if (propellants == null || propellants.Count == 0)
            return "No propellant feed sources reported";

        var summary = new StringBuilder();
        int shown = Math.Min(3, propellants.Count);
        for (int i = 0; i < shown; i++)
        {
            if (i > 0)
                summary.Append(", ");
            SequencePropellantInfo propellant = propellants[i];
            summary.Append(propellant.ReactantName);
            summary.Append(" @ ");
            summary.Append(propellant.TankPart.DisplayName);
            summary.Append(" (");
            summary.Append(propellant.ConsumedMass.ToString("F1"));
            summary.Append(" kg)");
        }
        if (propellants.Count > shown)
            summary.Append($", +{propellants.Count - shown} more");
        return summary.ToString();
    }

    private static int CountAttachedParts(HashSet<Part>? attachedParts)
    {
        if (attachedParts == null)
            return 0;

        return attachedParts.Count;
    }

    private static void CollectEngines(Sequence sequence, HashSet<uint> jettisonedPartIds, bool sequenceActivated)
    {
        _pooledEngines.Clear();
        ReadOnlySpan<Part> parts = sequence.Parts;
        for (int pi = 0; pi < parts.Length; pi++)
        {
            Part part = parts[pi];
            if (jettisonedPartIds.Contains(part.InstanceId))
                continue;

            Span<EngineController> engines = part.Modules.Get<EngineController>();
            for (int ei = 0; ei < engines.Length; ei++)
            {
                EngineController engine = engines[ei];
                if (sequenceActivated && !engine.IsActive)
                    continue;
                _pooledEngines.Add(engine);
            }
        }
    }

    private static (float current, float max) ComputeSequenceFuel(
        List<EngineController> engines, HashSet<ulong> fuelClaimedTankIds, ReadOnlySpan<MoleState> moleStates)
    {
        float totalCurrent = 0f;
        float totalMax = 0f;
        foreach (EngineController engine in engines)
        {
            foreach (RocketCore core in engine.Cores)
            {
                if (core is not PlumbedCore { ResourceManager: { } resourceManager })
                    continue;
                var (current, max) = WalkReachableTanks(resourceManager, fuelClaimedTankIds, moleStates);
                totalCurrent += current;
                totalMax += max;
            }
        }
        return (totalCurrent, totalMax);
    }

    private static (float current, float max) WalkReachableTanks(
        ResourceManager resourceManager, HashSet<ulong> fuelClaimedTankIds, ReadOnlySpan<MoleState> moleStates)
    {
        float current = 0f;
        float max = 0f;
        FlowOrder<Tank> order = FlowHelpers.SelectFlowNodes(resourceManager);
        if (!order.IsValid || order.LevelCount == 0)
            return (0f, 0f);

        for (int i = 0; i < order.LevelCount; i++)
        {
            ReadOnlySpan<Tank> tanks = order[i];
            if (tanks.IsEmpty)
                continue;

            for (int j = 0; j < tanks.Length; j++)
            {
                Tank tank = tanks[j];
                if (tank == null)
                    continue;
                if (!fuelClaimedTankIds.Add(tank.InstanceId))
                    continue;

                current += tank.ComputeSubstanceMass(moleStates);
                max += MassHelpers.ComputeTankMaxMass(tank);
            }
        }
        return (current, max);
    }

    private static (float Mass, float DryMass) ComputeJettisonedMass(
        Sequence sequence, ReadOnlySpan<MoleState> moleStates,
        HashSet<uint> jettisonedPartIds, HashSet<ulong> fuelClaimedTankIds)
    {
        float totalJettisoned = 0f;
        float totalJettisonedDryMass = 0f;
        ReadOnlySpan<Part> parts = sequence.Parts;
        for (int pi = 0; pi < parts.Length; pi++)
        {
            Part part = parts[pi];
            if (!part.Modules.HasAny<Decoupler>())
                continue;
            (float mass, float dryMass) = CollectSubtreeMass(part, moleStates, jettisonedPartIds, fuelClaimedTankIds);
            totalJettisoned += mass;
            totalJettisonedDryMass += dryMass;
        }
        return (totalJettisoned, totalJettisonedDryMass);
    }

    private static (float Mass, float DryMass) CollectSubtreeMass(
        Part part, ReadOnlySpan<MoleState> moleStates,
        HashSet<uint> jettisonedPartIds, HashSet<ulong> fuelClaimedTankIds)
    {
        if (!jettisonedPartIds.Add(part.InstanceId))
            return (0f, 0f);

        (float mass, float dryMass) = ComputePartMass(part, moleStates, fuelClaimedTankIds);
        List<Part> children = part.TreeChildren;
        for (int i = 0; i < children.Count; i++)
        {
            (float childMass, float childDryMass) = CollectSubtreeMass(
                children[i], moleStates, jettisonedPartIds, fuelClaimedTankIds);
            mass += childMass;
            dryMass += childDryMass;
        }
        return (mass, dryMass);
    }

    private static (float Mass, float DryMass) ComputePartMass(
        Part part, ReadOnlySpan<MoleState> moleStates, HashSet<ulong> fuelClaimedTankIds)
    {
        (float mass, float dryMass) = SumComponentMass(part.Modules, moleStates, fuelClaimedTankIds);
        ReadOnlySpan<Part> subParts = part.SubParts;
        for (int i = 0; i < subParts.Length; i++)
        {
            (float subPartMass, float subPartDryMass) = SumComponentMass(
                subParts[i].Modules, moleStates, fuelClaimedTankIds);
            mass += subPartMass;
            dryMass += subPartDryMass;
        }
        return (mass, dryMass);
    }

    private static (float Mass, float DryMass) SumComponentMass(
        ModuleList components, ReadOnlySpan<MoleState> moleStates, HashSet<ulong> fuelClaimedTankIds)
    {
        float dryMass = MassHelpers.SumInertMass(components);
        float mass = dryMass;
        Span<Tank> tanks = components.Get<Tank>();
        for (int i = 0; i < tanks.Length; i++)
        {
            if (!fuelClaimedTankIds.Contains(tanks[i].InstanceId))
                mass += tanks[i].ComputeSubstanceMass(moleStates);
        }
        return (mass, dryMass);
    }

    private static int CountRemainingParts(PartTree tree, HashSet<uint> jettisonedPartIds)
    {
        int count = 0;
        ReadOnlySpan<Part> parts = tree.Parts;
        for (int i = 0; i < parts.Length; i++)
        {
            if (!jettisonedPartIds.Contains(parts[i].InstanceId))
                count++;
        }
        return count;
    }
}