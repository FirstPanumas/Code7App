using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Code7App.Models;
using Code7App.Services;
using System.Text.Json;

namespace Code7App.ViewModels;

public partial class SettingsViewModel : ObservableObject
{
    private readonly ISettingsService _settingsService;

    [ObservableProperty]
    private AppConfig _config = new();

    public SettingsViewModel(ISettingsService settingsService)
    {
        _settingsService = settingsService;
        LoadSettingsAsync().ConfigureAwait(false);
    }

    private async Task LoadSettingsAsync()
    {
        Config = await _settingsService.LoadSettingsAsync();
    }

    [RelayCommand]
    private async Task SaveSettingsAsync()
    {
        await _settingsService.SaveSettingsAsync(Config);
        await Application.Current!.MainPage!.DisplayAlert("Success", "บันทึกการตั้งค่าเรียบร้อยแล้ว", "OK");
    }

    [RelayCommand]
    private async Task ImportTotalSettingAsync()
    {
        try
        {
            var customFileType = new FilePickerFileType(new Dictionary<DevicePlatform, IEnumerable<string>>
            {
                { DevicePlatform.WinUI, new[] { ".json", ".txt" } },
                { DevicePlatform.MacCatalyst, new[] { "json", "txt", "public.json", "public.plain-text" } }
            });

            var result = await FilePicker.Default.PickAsync(new PickOptions
            {
                PickerTitle = "Select Total Setting File",
                FileTypes = customFileType
            });

            if (result != null)
            {
                // นำเข้าไฟล์และโหลดค่าใหม่ขึ้นมาแสดงบน UI ทันที
                await _settingsService.ImportSettingsAsync(result.FullPath);
                await LoadSettingsAsync();
                await Application.Current!.MainPage!.DisplayAlert("Success", "โหลดการตั้งค่าสำเร็จ", "OK");
            }
        }
        catch (Exception ex)
        {
            await Application.Current!.MainPage!.DisplayAlert("Error", $"ไม่สามารถโหลดไฟล์ได้: {ex.Message}", "OK");
        }
    }

    [RelayCommand]
    private async Task BrowseCsvPathAsync(string section)
    {
        var result = await FilePicker.Default.PickAsync(new PickOptions { PickerTitle = "Select Default CSV" });
        if (result != null)
        {
            // อัปเดต Path ตาม Section ที่ส่งมา
            switch (section)
            {
                case "Patient": Config.PatientRegister.DefaultPath = result.FullPath; break;
                case "Item": Config.OrderItem.DefaultPath = result.FullPath; break;
                case "Set": Config.OrderSet.DefaultPath = result.FullPath; break;
            }
            // บังคับให้ UI อัปเดตค่า (เนื่องจากไม่ได้ใช้ ObservableProperty ภายใน Model ย่อย)
            OnPropertyChanged(nameof(Config));
        }
    }
}