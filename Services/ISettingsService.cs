using Code7App.Models;

namespace Code7App.Services;

public interface ISettingsService
{
    Task<AppConfig> LoadSettingsAsync();
    Task SaveSettingsAsync(AppConfig config);
    Task ImportSettingsAsync(string filePath);
}