using System;
using System.Diagnostics;
using System.IO;
using System.Text.Json;
using Brutal.Logging;
using Brutal.Numerics;
using KSA;
using KittenEngineerRedux.Editor;
using KittenEngineerRedux.Flight;

namespace KittenEngineerRedux.UI;

internal sealed class SectionStateData
{
    public bool Open { get; set; }
    public bool Popped { get; set; }
}

internal sealed class AlertRuleData
{
    public string Metric { get; set; } = string.Empty;
    public bool Enabled { get; set; }
    public bool Above { get; set; }
    public double Threshold { get; set; }
}

internal sealed class SettingsData
{
    public Dictionary<string, float[]> Colors { get; set; } = new();
    public float CornerRadius { get; set; } = 4f;
    public float TextScale { get; set; } = 1f;
    public List<string> Widgets { get; set; } = new();
    public bool AlertsEnabled { get; set; } = true;
    public List<AlertRuleData> Alerts { get; set; } = new();
    public Dictionary<string, SectionStateData> Sections { get; set; } = new();
    public string GraphMetric { get; set; } = "Altitude";
    public bool FlightHudVisible { get; set; } = true;
    public bool EditorPanelVisible { get; set; } = true;
    public bool UseNativeStaging { get; set; } = true;
    public bool UseAtmosphere { get; set; } = true;
    public double TargetOrbitAltitude { get; set; } = 200_000.0;
}

internal static class SettingsStore
{
    private static readonly string BaseDirectory = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments),
        "My Games", "Kitten Space Agency", "KittenEngineerReduxData");

    private static readonly JsonSerializerOptions JsonOptions = new() { WriteIndented = true };
    private static readonly Stopwatch Timer = Stopwatch.StartNew();
    private static readonly Dictionary<string, SectionStateData> SectionStates = new();

    private static string _lastJson = string.Empty;
    private static bool _loaded;
    private static bool _saveErrorLogged;

    public static string DataDirectory => BaseDirectory;
    public static string ExportDirectory => Path.Combine(BaseDirectory, "exports");
    private static string SettingsPath => Path.Combine(BaseDirectory, "settings.json");

    public static void Tick()
    {
        if (!_loaded)
        {
            Load();
            return;
        }
        if (Timer.Elapsed.TotalSeconds < 2.0)
            return;
        Timer.Restart();
        Save();
    }

    public static void Load()
    {
        try
        {
            if (File.Exists(SettingsPath))
            {
                SettingsData? data = JsonSerializer.Deserialize<SettingsData>(File.ReadAllText(SettingsPath), JsonOptions);
                if (data != null)
                    Apply(data);
            }
        }
        catch (Exception ex)
        {
            DefaultCategory.Log.Info($"[KER] Could not load settings: {ex.Message}");
        }
        finally
        {
            _loaded = true;
            _lastJson = JsonSerializer.Serialize(Capture(), JsonOptions);
        }
    }

    public static void Save()
    {
        if (!_loaded)
            return;

        try
        {
            string json = JsonSerializer.Serialize(Capture(), JsonOptions);
            if (json == _lastJson)
                return;
            Directory.CreateDirectory(BaseDirectory);
            File.WriteAllText(SettingsPath, json);
            _lastJson = json;
        }
        catch (Exception ex)
        {
            if (!_saveErrorLogged)
            {
                _saveErrorLogged = true;
                DefaultCategory.Log.Info($"[KER] Could not save settings: {ex.Message}");
            }
        }
    }

    public static void ApplySection(HudSection section)
    {
        if (SectionStates.TryGetValue(section.Id, out SectionStateData? state))
        {
            section.Open = state.Open;
            section.Popped = state.Popped;
        }
    }

    public static void OpenFolder(string path)
    {
        try
        {
            Directory.CreateDirectory(path);
            Process.Start(new ProcessStartInfo { FileName = path, UseShellExecute = true });
        }
        catch (Exception ex)
        {
            DefaultCategory.Log.Info($"[KER] Could not open folder: {ex.Message}");
        }
    }

    private static SettingsData Capture()
    {
        var data = new SettingsData
        {
            CornerRadius = PanelKit.CornerRadius,
            TextScale = PanelKit.TextScale,
            Widgets = HudWidgets.GetSelectedNames(),
            AlertsEnabled = HudAlerts.Enabled,
            GraphMetric = FlightHud.HistoryMetricName,
            FlightHudVisible = FlightHud.Visible,
            EditorPanelVisible = EditorPanel.Visible,
            UseNativeStaging = FlightHud.UseNativeStaging,
            UseAtmosphere = FlightHud.UseAtmosphere,
            TargetOrbitAltitude = FlightHud.TargetOrbitAltitude,
        };

        data.Colors["WindowBg"] = ToArray(PanelKit.WindowBgColor);
        data.Colors["Border"] = ToArray(PanelKit.BorderColor);
        data.Colors["Header"] = ToArray(PanelKit.HeaderColor);
        data.Colors["Label"] = ToArray(PanelKit.LabelColor);
        data.Colors["Value"] = ToArray(PanelKit.ValueColor);
        data.Colors["Accent"] = ToArray(PanelKit.AccentColor);
        data.Colors["ToggleOff"] = ToArray(PanelKit.ToggleOffColor);
        data.Colors["Warning"] = ToArray(PanelKit.WarningColor);
        data.Colors["Good"] = ToArray(PanelKit.GoodColor);
        data.Colors["RowSeparator"] = ToArray(PanelKit.RowSeparatorColor);
        data.Colors["Highlight"] = ToArray(PanelKit.HighlightColor);
        data.Colors["GraphBg"] = ToArray(PanelKit.GraphBgColor);
        data.Colors["GraphLine"] = ToArray(PanelKit.GraphLineColor);
        data.Colors["GraphGrid"] = ToArray(PanelKit.GraphGridColor);

        foreach (AlertRule rule in HudAlerts.Rules)
        {
            data.Alerts.Add(new AlertRuleData
            {
                Metric = rule.Metric.ToString(),
                Enabled = rule.Enabled,
                Above = rule.Above,
                Threshold = rule.Threshold,
            });
        }

        foreach (KeyValuePair<string, SectionStateData> pair in SectionStates)
            data.Sections[pair.Key] = pair.Value;
        foreach (HudSection section in SectionRegistry.All)
            data.Sections[section.Id] = new SectionStateData { Open = section.Open, Popped = section.Popped };

        return data;
    }

    private static void Apply(SettingsData data)
    {
        PanelKit.CornerRadius = data.CornerRadius;
        PanelKit.TextScale = data.TextScale;

        PanelKit.WindowBgColor = ReadColor(data, "WindowBg", PanelKit.WindowBgColor);
        PanelKit.BorderColor = ReadColor(data, "Border", PanelKit.BorderColor);
        PanelKit.HeaderColor = ReadColor(data, "Header", PanelKit.HeaderColor);
        PanelKit.LabelColor = ReadColor(data, "Label", PanelKit.LabelColor);
        PanelKit.ValueColor = ReadColor(data, "Value", PanelKit.ValueColor);
        PanelKit.AccentColor = ReadColor(data, "Accent", PanelKit.AccentColor);
        PanelKit.ToggleOffColor = ReadColor(data, "ToggleOff", PanelKit.ToggleOffColor);
        PanelKit.WarningColor = ReadColor(data, "Warning", PanelKit.WarningColor);
        PanelKit.GoodColor = ReadColor(data, "Good", PanelKit.GoodColor);
        PanelKit.RowSeparatorColor = ReadColor(data, "RowSeparator", PanelKit.RowSeparatorColor);
        PanelKit.HighlightColor = ReadColor(data, "Highlight", PanelKit.HighlightColor);
        PanelKit.GraphBgColor = ReadColor(data, "GraphBg", PanelKit.GraphBgColor);
        PanelKit.GraphLineColor = ReadColor(data, "GraphLine", PanelKit.GraphLineColor);
        PanelKit.GraphGridColor = ReadColor(data, "GraphGrid", PanelKit.GraphGridColor);

        HudWidgets.SetSelected(data.Widgets);

        HudAlerts.Enabled = data.AlertsEnabled;
        foreach (AlertRuleData saved in data.Alerts)
        {
            foreach (AlertRule rule in HudAlerts.Rules)
            {
                if (rule.Metric.ToString() != saved.Metric)
                    continue;
                rule.Enabled = saved.Enabled;
                rule.Above = saved.Above;
                rule.Threshold = saved.Threshold;
            }
        }

        SectionStates.Clear();
        foreach (KeyValuePair<string, SectionStateData> pair in data.Sections)
            SectionStates[pair.Key] = pair.Value;
        foreach (HudSection section in SectionRegistry.All)
            ApplySection(section);

        FlightHud.HistoryMetricName = data.GraphMetric;
        FlightHud.Visible = data.FlightHudVisible;
        EditorPanel.Visible = data.EditorPanelVisible;
        FlightHud.UseNativeStaging = data.UseNativeStaging;
        FlightHud.UseAtmosphere = data.UseAtmosphere;
        FlightHud.TargetOrbitAltitude = data.TargetOrbitAltitude;
    }

    private static float[] ToArray(float4 color) => new[] { color.X, color.Y, color.Z, color.W };

    private static float4 ReadColor(SettingsData data, string key, float4 fallback)
    {
        if (data.Colors.TryGetValue(key, out float[]? values) && values != null && values.Length >= 4)
            return new float4(values[0], values[1], values[2], values[3]);
        return fallback;
    }
}