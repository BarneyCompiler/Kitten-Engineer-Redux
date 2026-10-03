using KSA;

namespace KittenEngineerRedux.Analysis;

internal static class FlowHelpers
{
    public static FlowOrder<Tank> SelectFlowNodes(ResourceManager rm) => rm.ConsumptionOrder;
}