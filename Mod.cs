// Kitten Engineer Redux 
// Made by BarneyTheGod

using KSA;
using Brutal.Logging;
using HarmonyLib;
using KittenEngineerRedux.Analysis;
using StarMap.API;
using StarMap.API.Services;

namespace KittenEngineerRedux;

[StarMapMod]
public sealed class Mod
{
    private static Harmony? _harmony;
    private readonly IModMenuService? _menuService;
    private IDisposable? _menuItem;

    // Constructor injection for StarMap services
    public Mod(IModMenuService menuService)
    {
        _menuService = menuService;
    }

    [StarMapBeforeMain]
    public void Initialize()
    {
        _harmony = new Harmony("com.matthew.kittenengineerredux");
        _harmony.PatchAll(typeof(Mod).Assembly);
        
        // Register the in-game menu item using StarMap's native API
        _menuItem = _menuService?.AddModMenuItem(
            "Kitten Engineer Redux", 
            () => KittenEngineerRedux.UI.MenuContent.DrawToggles()
        );

        DefaultCategory.Log.Info("[KER] Loaded and patched.");
    }

    [StarMapUnload]
    public void Unload()
    {
        _menuItem?.Dispose();
        _harmony?.UnpatchAll(_harmony.Id);
        _harmony = null;
        SequenceAnalyzer.ResetPools();
        DefaultCategory.Log.Info("[KER] Unloaded.");
    }
}