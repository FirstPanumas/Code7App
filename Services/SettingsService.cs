using System.Text.Json;
using Code7App.Models;

namespace Code7App.Services;

public class SettingsService : ISettingsService
{
    private readonly string _settingsFilePath;
    private readonly JsonSerializerOptions _jsonOptions;

    public SettingsService()
    {
        // กำหนด Path มาตรฐานที่ระบบ OS อนุญาตให้อ่าน/เขียนไฟล์ได้
        _settingsFilePath = Path.Combine(FileSystem.AppDataDirectory, "TotalSettings.json");
        _jsonOptions = new JsonSerializerOptions { WriteIndented = true };
    }

    public async Task<AppConfig> LoadSettingsAsync()
    {
        if (!File.Exists(_settingsFilePath))
            return new AppConfig(); // ส่งคืนค่าว่าง (Default) หากยังไม่มีไฟล์

        try
        {
            using var stream = File.OpenRead(_settingsFilePath);
            var config = await JsonSerializer.DeserializeAsync<AppConfig>(stream, _jsonOptions);
            return config ?? new AppConfig();
        }
        catch (Exception)
        {
            // หากไฟล์ Corrupt หรือ JSON ผิดรูปแบบ ให้คืนค่าเริ่มต้น
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