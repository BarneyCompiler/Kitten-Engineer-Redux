using CommunityToolkit.HighPerformance.Buffers;
using KSA;

namespace KittenEngineerRedux.Analysis;

internal static class FlowHelpers
{
    public static MemoryOwner<MemoryOwner<Tank>>? SelectFlowNodes(ResourceManager rm) => rm.FlowRule switch
    {
        FlowRule.FurtherestToNearest => rm.FurtherestToNearestNode,
        FlowRule.NearestToFurtherest => rm.NearestToFurtherestNode,
        FlowRule.FurtherestToNearestSameStage => rm.FurtherestToNearestNodeSameStage,
        FlowRule.NearestToFurtherestSameStage => rm.NearestToFurtherestNodeSameStage,
        _ => rm.NearestToFurtherestNode,
    };
}