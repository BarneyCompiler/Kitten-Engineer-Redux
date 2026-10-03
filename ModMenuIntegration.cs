using ModMenu;

namespace KittenEngineerRedux.UI;

public static class ModMenuIntegration
{
    public static bool IsHandledByModMenu { get; set; }

    [ModMenuEntry("Kitten Engineer Redux", nameof(IsHandledByModMenu))]
    public static void DrawMenu()
    {
        MenuContent.DrawToggles();
    }
}