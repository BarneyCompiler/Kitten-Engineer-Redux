using System;
using Brutal.Numerics;
using KSA;

namespace KittenEngineerRedux.Analysis;

internal readonly record struct ActiveEngineThrustInfo(float TotalThrust, float TotalMassFlowRate);
internal readonly record struct ActualEnginePerformanceInfo(float TotalThrust, float TotalMassFlowRate);

internal static class ActiveEngineThrust
{
    public static ActualEnginePerformanceInfo ComputeActual(PartTree parts)
    {
        float totalThrust = 0f;
        float totalFlowRate = 0f;
        Span<EngineController> engines = parts.Modules.Get<EngineController>();
        for (int i = 0; i < engines.Length; i++)
        {
            EngineController engine = engines[i];
            if (!engine.IsActive)
                continue;

            foreach (RocketCore core in engine.Cores)
            {
                var nozzles = parts.RocketNozzles.GetModulesAndStates(core.Rocket.Nozzles.AsSpan()).GetEnumerator();
                while (nozzles.MoveNext())
                {
                    var nozzle = nozzles.Current;
                    totalThrust += nozzle.State.Performance.TotalThrust;
                    totalFlowRate += nozzle.State.Performance.MassFlowRate;
                }
            }
        }
        return new ActualEnginePerformanceInfo(totalThrust, totalFlowRate);
    }

    public static ActiveEngineThrustInfo Compute(PartTree parts, float ambientPressure)
    {
        float totalThrust = 0f;
        float totalFlowRate = 0f;

        Span<EngineController> engines = parts.Modules.Get<EngineController>();
        for (int i = 0; i < engines.Length; i++)
        {
            EngineController engine = engines[i];
            if (!engine.IsActive)
                continue;

            if (ambientPressure > 0f)
            {
                var data = RocketControllerData.ComputeFromCores(engine.Cores.AsSpan(), ambientPressure, 1f);
                totalThrust += data.ThrustMax.Length();
                totalFlowRate += data.MassFlowRateMax;
            }
            else
            {
                totalThrust += engine.VacuumData.ThrustMax.Length();
                totalFlowRate += engine.VacuumData.MassFlowRateMax;
            }
        }

        return new ActiveEngineThrustInfo(totalThrust, totalFlowRate);
    }
}