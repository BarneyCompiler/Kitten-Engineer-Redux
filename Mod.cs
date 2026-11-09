// Kitten Engineer Redux 
// Made by BarneyTheGod

using KSA;
using Brutal.Logging;
using HarmonyLib;
using KittenEngineerRedux.Analysis;
using KittenEngineerRedux.Editor;
using KittenEngineerRedux.Flight;
using StarMap.API;
using StarMap.API.Services;

namespace KittenEngineerRedux;

[StarMapMod]
public sealed class Mod
{
    private static Harmony? _harmony;
    private readonly IModMenuService _menuService;
    private IDisposable? _flightHudMenuItem;
    private IDisposable? _editorPanelMenuItem;

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
        
        // StarMap invokes these actions on click; they are not submenu renderers.
        _flightHudMenuItem = _menuService.AddModMenuItem(
            "Kitten Engineer Redux: Flight HUD",
            () => FlightHud.Visible = !FlightHud.Visible);
        _editorPanelMenuItem = _menuService.AddModMenuItem(
            "Kitten Engineer Redux: Editor Panel",
            () => EditorPanel.Visible = !EditorPanel.Visible);

        DefaultCategory.Log.Info("[KER] Loaded and patched.");
    }

    [StarMapUnload]
    public void Unload()
    {
        _flightHudMenuItem?.Dispose();
        _editorPanelMenuItem?.Dispose();
        _harmony?.UnpatchAll(_harmony.Id);
        _harmony = null;
        SequenceAnalyzer.ResetPools();
        DefaultCategory.Log.Info("[KER] Unloaded.");
    }
}
