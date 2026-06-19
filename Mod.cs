// Kitten Engineer Redux v0.5.1
// Made by BarneyTheGod

// This mod uses a few snippets from other KSA mods as helper methods for this Mod.
// StageInfo : Maximilian-Nesslauer
// Advanced Flight Computer : Maximilian-Nesslauer

using KSA;
using Brutal.Logging;
using HarmonyLib;
using KittenEngineerRedux.Analysis;
using StarMap.API;

namespace KittenEngineerRedux;

[StarMapMod]
public sealed class Mod
{
    private static Harmony? _harmony;

    [StarMapAllModsLoaded]
    public void OnFullyLoaded()
    {
        _harmony = new Harmony("com.matthew.kittenengineerredux");
        _harmony.PatchAll(typeof(Mod).Assembly);
        DefaultCategory.Log.Info("[KER] Loaded and patched.");
    }

    [StarMapUnload]
    public void Unload()
    {
        _harmony?.UnpatchAll(_harmony.Id);
        _harmony = null;
        SequenceAnalyzer.ResetPools();
        DefaultCategory.Log.Info("[KER] Unloaded.");
    }
}