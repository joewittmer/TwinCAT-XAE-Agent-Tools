using System.Text.Json;
using TwincatMcp.Tray.Models;

namespace TwincatMcp.Tray.Services;

internal sealed class SettingsStore
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
    {
        WriteIndented = true
    };

    public string SettingsPath { get; } = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
        "TwincatMcp",
        "traysettings.json");

    public TraySettings Load()
    {
        if (!File.Exists(SettingsPath))
        {
            return new TraySettings();
        }

        string json = File.ReadAllText(SettingsPath);
        return JsonSerializer.Deserialize<TraySettings>(json, JsonOptions) ?? new TraySettings();
    }

    public void Save(TraySettings settings)
    {
        ArgumentNullException.ThrowIfNull(settings);

        string? directory = Path.GetDirectoryName(SettingsPath);
        if (!string.IsNullOrWhiteSpace(directory))
        {
            Directory.CreateDirectory(directory);
        }

        string json = JsonSerializer.Serialize(settings, JsonOptions);
        File.WriteAllText(SettingsPath, json);
    }
}
