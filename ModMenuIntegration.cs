using ModMenu;

namespace KittenEngineerRedux.UI;

public static class ModMenuIntegration
{
    public static bool IsHandledByModMenu { get; private set; }

    [ModMenuEntry("Kitten Engineer Redux")]
    public static void DrawMenu()
    {
        IsHandledByModMenu = true;
        MenuContent.DrawToggles();
    }
}