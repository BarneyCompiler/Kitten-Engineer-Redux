using System;
using System.Collections.Generic;
using Brutal.Numerics;
using KSA;

namespace KittenEngineerRedux.Analysis;

internal record struct SequenceBurnInfo
{
    public int SequenceNumber;
    public bool IsActivated;
    public float DeltaV;
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
    public float Twr;
    public float JettisonedMass;
    public int EngineCount;
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

        for (int si = 0; si < sequences.Length; si++)
        {
            Sequence sequence = sequences[si];
            if (sequence.Parts.IsEmpty)
                continue;

            float jettisonedMass = ComputeJettisonedMass(sequence, moleStates, _pooledJettisonedPartIds, _pooledFuelClaimedTankIds);
            currentMass -= jettisonedMass;

            CollectEngines(sequence, _pooledJettisonedPartIds, sequence.Activated);
            if (_pooledEngines.Count == 0)
                continue;

            float totalThrust = 0f;
            float totalFlowRate = 0f;
            if (ambientPressure > 0f)
            {
                foreach (EngineController engine in _pooledEngines)
                {
                    var data = RocketControllerData.ComputeFromCores(engine.Cores.AsSpan(), float3.Zero, ambientPressure);
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
            float twr = surfaceGravity > 0f ? totalThrust / (startMass * surfaceGravity) : 0f;

            result.Sequences.Add(new SequenceBurnInfo
            {
                SequenceNumber = sequence.Number,
                IsActivated = sequence.Activated,
                DeltaV = dv,
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
                Twr = twr,
                JettisonedMass = jettisonedMass,
                EngineCount = _pooledEngines.Count
            });

            result.TotalDeltaV += dv;
            result.TotalBurnTime += burnTime;
            currentMass = endMass;
        }

        return result;
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
                if (core.ResourceManager == null)
                    continue;
                var (current, max) = WalkReachableTanks(core.ResourceManager, fuelClaimedTankIds, moleStates);
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
        var nodes = FlowHelpers.SelectFlowNodes(resourceManager);
        if (nodes == null || nodes.Length == 0)
            return (0f, 0f);

        Span<CommunityToolkit.HighPerformance.Buffers.MemoryOwner<Tank>> nodeSpan = nodes.Span;
        for (int i = 0; i < nodeSpan.Length; i++)
        {
            if (nodeSpan[i] == null || nodeSpan[i].Length == 0)
                continue;

            Span<Tank> tanks = nodeSpan[i].Span;
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

    private static float ComputeJettisonedMass(
        Sequence sequence, ReadOnlySpan<MoleState> moleStates,
        HashSet<uint> jettisonedPartIds, HashSet<ulong> fuelClaimedTankIds)
    {
        float totalJettisoned = 0f;
        ReadOnlySpan<Part> parts = sequence.Parts;
        for (int pi = 0; pi < parts.Length; pi++)
        {
            Part part = parts[pi];
            if (!part.Modules.HasAny<Decoupler>())
                continue;
            totalJettisoned += CollectSubtreeMass(part, moleStates, jettisonedPartIds, fuelClaimedTankIds);
        }
        return totalJettisoned;
    }

    private static float CollectSubtreeMass(
        Part part, ReadOnlySpan<MoleState> moleStates,
        HashSet<uint> jettisonedPartIds, HashSet<ulong> fuelClaimedTankIds)
    {
        if (!jettisonedPartIds.Add(part.InstanceId))
            return 0f;

        float mass = ComputePartMass(part, moleStates, fuelClaimedTankIds);
        List<Part> children = part.TreeChildren;
        for (int i = 0; i < children.Count; i++)
            mass += CollectSubtreeMass(children[i], moleStates, jettisonedPartIds, fuelClaimedTankIds);
        return mass;
    }

    private static float ComputePartMass(Part part, ReadOnlySpan<MoleState> moleStates, HashSet<ulong> fuelClaimedTankIds)
    {
        float mass = SumComponentMass(part.Modules, moleStates, fuelClaimedTankIds);
        ReadOnlySpan<Part> subParts = part.SubParts;
        for (int i = 0; i < subParts.Length; i++)
            mass += SumComponentMass(subParts[i].Modules, moleStates, fuelClaimedTankIds);
        return mass;
    }

    private static float SumComponentMass(ModuleList components, ReadOnlySpan<MoleState> moleStates, HashSet<ulong> fuelClaimedTankIds)
    {
        float mass = MassHelpers.SumInertMass(components);
        Span<Tank> tanks = components.Get<Tank>();
        for (int i = 0; i < tanks.Length; i++)
        {
            if (!fuelClaimedTankIds.Contains(tanks[i].InstanceId))
                mass += tanks[i].ComputeSubstanceMass(moleStates);
        }
        return mass;
    }
}