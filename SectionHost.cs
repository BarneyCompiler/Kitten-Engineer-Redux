using System.Text;
using Brutal.ImGuiApi;
using Brutal.Numerics;
using KSA;

namespace KittenEngineerRedux.UI;

internal delegate float SectionDraw(ImDrawListPtr dl, float2 origin, float width, float y);

internal sealed class HudSection
{
    public HudSection(string id, string title, SectionDraw draw)
    {
        Id = id;
        Title = title;
        PoppedTitle = $"{title} (separate window)";
        Draw = draw;
        WindowTitle = Encoding.UTF8.GetBytes($"{title}###KerSection_{id}");
        SectionRegistry.Register(this);
    }

    public string Id { get; }
    public string Title { get; }
    public string PoppedTitle { get; }
    public SectionDraw Draw { get; }
    public byte[] WindowTitle { get; }
    public bool Open { get; set; }
    public bool Popped { get; set; }
}

internal static class SectionRegistry
{
    public static readonly List<HudSection> All = new();

    public static void Register(HudSection section)
    {
        All.Add(section);
        SettingsStore.ApplySection(section);
    }
}

internal static class SectionHost
{
    private const float FloatingWidth = 360f;
    private const float FloatingHeight = 320f;

    public static float DrawDocked(HudSection section, ImDrawListPtr dl, float2 origin, float width, float y)
    {
        y = DrawBar(dl, origin, width, y, section, false, out bool toggleOpen, out bool togglePopped);
        if (toggleOpen)
            section.Open = !section.Open;
        if (togglePopped)
            section.Popped = !section.Popped;

        if (section.Open && !section.Popped)
        {
            y = section.Draw(dl, origin, width, y);
            y += 4f;
        }
        return y;
    }

    public static void DrawFloating(HudSection section, GameViewport viewport, int index)
    {
        if (!section.Popped)
            return;

        float2 defaultPos = viewport.Position + new float2(380f + index * 28f, 80f + index * 28f);
        bool open = PanelKit.BeginWindow(section.WindowTitle, defaultPos, new float2(FloatingWidth, FloatingHeight));
        if (open)
        {
            float width = ImGui.GetContentRegionAvail().X;
            ImDrawListPtr dl = ImGui.GetWindowDrawList();
            float2 origin = ImGui.GetCursorScreenPos();
            PanelKit.DrawAccentStrip(dl, origin, ImGui.GetWindowSize().Y);

            float y = origin.Y;
            y = DrawBar(dl, origin, width, y, section, true, out _, out bool dock);
            if (dock)
                section.Popped = false;

            y = section.Draw(dl, origin, width, y);
            PanelKit.EndContent(origin, width, y);
        }
        PanelKit.EndWindow();
    }

    private static float DrawBar(ImDrawListPtr dl, float2 origin, float width, float y,
        HudSection section, bool floating, out bool toggleOpen, out bool togglePopped)
    {
        float lineHeight = ImGui.GetTextLineHeight();
        float height = Math.Max(PanelKit.RowHeight, lineHeight + 8f);
        string buttonLabel = section.Popped ? "Dock" : "Pop out";
        float buttonWidth = ImGui.CalcTextSize("Pop out").X + 14f;
        float buttonHeight = height - 6f;

        float2 barMin = new float2(origin.X, y);
        float2 barMax = new float2(origin.X + width, y + height);
        dl.AddRectFilled(in barMin, in barMax, PanelKit.ToColor(PanelKit.ToggleOffColor), PanelKit.CornerRadius);

        ImGui.SetCursorScreenPos(new float2(origin.X, y));
        bool barClicked = ImGui.InvisibleButton("##KerBar" + section.Id, new float2(Math.Max(10f, width - buttonWidth - 8f), height));
        toggleOpen = barClicked && !section.Popped;

        float2 buttonPos = new float2(origin.X + width - buttonWidth - 3f, y + 3f);
        ImGui.SetCursorScreenPos(buttonPos);
        togglePopped = ImGui.InvisibleButton("##KerPop" + section.Id, new float2(buttonWidth, buttonHeight));

        float2 buttonMax = new float2(buttonPos.X + buttonWidth, buttonPos.Y + buttonHeight);
        dl.AddRectFilled(in buttonPos, in buttonMax,
            PanelKit.ToColor(section.Popped ? PanelKit.AccentColor : PanelKit.BorderColor), PanelKit.CornerRadius);
        float2 buttonTextSize = ImGui.CalcTextSize(buttonLabel);
        float2 buttonTextPos = new float2(
            buttonPos.X + (buttonWidth - buttonTextSize.X) * 0.5f,
            buttonPos.Y + (buttonHeight - buttonTextSize.Y) * 0.5f);
        dl.AddText(in buttonTextPos, PanelKit.ToColor(PanelKit.ValueColor), buttonLabel);

        float textY = y + (height - lineHeight) * 0.5f;
        float labelX = origin.X + 8f;
        if (!section.Popped)
        {
            float2 arrowPos = new float2(labelX, textY);
            dl.AddText(in arrowPos, PanelKit.ToColor(PanelKit.HeaderColor), section.Open ? "v" : ">");
            labelX += 16f;
        }

        bool dimmed = section.Popped && !floating;
        string label = dimmed ? section.PoppedTitle : section.Title;
        float2 labelPos = new float2(labelX, textY);
        dl.AddText(in labelPos, PanelKit.ToColor(dimmed ? PanelKit.LabelColor : PanelKit.HeaderColor), label);

        return y + height + 4f;
    }
}