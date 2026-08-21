using Code7App.Models;
using System.Text.Encodings.Web;
using System.Text.Json;

namespace Code7App.Services;

public class SettingsService : ISettingsService
{
    private readonly string _settingsFilePath;
    private readonly JsonSerializerOptions _jsonOptions;

    public SettingsService()
    {
        _settingsFilePath = Path.Combine(FileSystem.AppDataDirectory, "TotalSettings.json");
        _jsonOptions = new JsonSerializerOptions
        {
            WriteIndented = true,
            Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping
        };
    }

    public async Task<AppConfig> LoadSettingsAsync()
    {

        // หากไม่มีไฟล์ ให้สร้างไฟล์ Default ขึ้นมาทันที
        if (!File.Exists(_settingsFilePath))
        {
            var defaultConfig = new AppConfig();
            await SaveSettingsAsync(defaultConfig);
            return defaultConfig;
        }
        try
        {
            using var stream = File.OpenRead(_settingsFilePath); 
            var config = await JsonSerializer.DeserializeAsync<AppConfig>(stream, _jsonOptions);  
            return config ?? new AppConfig();  
        }
        catch (Exception)
        {
            return new AppConfig(); 
        }
    }

    public async Task SaveSettingsAsync(AppConfig config)
    {
        using var stream = File.Create(_settingsFilePath);
        await JsonSerializer.SerializeAsync(stream, config, _jsonOptions);
    }

    public Task ImportSettingsAsync(string sourceFilePath)
    {
        if (File.Exists(sourceFilePath))
        {
            File.Copy(sourceFilePath, _settingsFilePath, overwrite: true);
        }
        return Task.CompletedTask;
    }
}