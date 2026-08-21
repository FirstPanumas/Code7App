using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using CommunityToolkit.Mvvm.Messaging;
using System.Collections.ObjectModel;
using System.IO;
using Code7App.Models;
using Code7App.Services;
using Code7App.Messages;
using Code7App.Views;

namespace Code7App.ViewModels;

public partial class MainViewModel : ObservableObject
{
    private readonly ISettingsService _settingsService;

    // --- ตัวแปรสำหรับเก็บข้อมูลทั้งหมดก่อนแบ่งหน้า/ค้นหา ---
    private List<Dictionary<string, string>> _allCsvRows = new();
    private const int PageSize = 100; // จำนวนแถวต่อหน้า

    // --- 1. Configuration & App State ---
    [ObservableProperty]
    private AppConfig _currentConfig = new();

    [ObservableProperty]
    private bool _isLoading;

    // --- 2. Data Grid & Pagination ---
    [ObservableProperty]
    private ObservableCollection<Dictionary<string, string>> _csvRows = [];

    [ObservableProperty]
    private ObservableCollection<string> _csvColumns = [];

    private Dictionary<string, string>? _selectedPatient;
    public Dictionary<string, string>? SelectedPatient
    {
        get => _selectedPatient;
        set
        {
            if (SetProperty(ref _selectedPatient, value))
            {
                UpdateDisplayPatientDetails();
            }
        }
    }

    [ObservableProperty] private int _currentPage = 1;
    [ObservableProperty] private int _totalPages = 1;

    // --- 3. Filter & Search ---
    [ObservableProperty] private string _filterLabel = "ตัวกรอง";
    [ObservableProperty] private ObservableCollection<string> _filterOptions = [];
    [ObservableProperty] private string _searchResultMessage = string.Empty;

    // ใช้ Full Property เพื่อดักจับ SetProperty แทนการใช้ partial void
    private string _searchText = string.Empty;
    public string SearchText
    {
        get => _searchText;
        set
        {
            if (SetProperty(ref _searchText, value))
            {
                CurrentPage = 1; // รีเซ็ตไปหน้าแรกเสมอเมื่อพิมพ์ค้นหา
                ApplyFilters();
            }
        }
    }

    private string _selectedFilterValue = string.Empty;
    public string SelectedFilterValue
    {
        get => _selectedFilterValue;
        set
        {
            if (SetProperty(ref _selectedFilterValue, value))
            {
                CurrentPage = 1;
                ApplyFilters();
            }
        }
    }

    // --- 4. Patient Details Display ---
    [ObservableProperty]
    private ObservableCollection<PatientDetailModel> _displayPatientDetails = [];

    // --- 5. Register Modal State ---
    [ObservableProperty] private bool _isRegisterModalVisible;
    [ObservableProperty] private DateTime _appointmentDate = DateTime.Today;

    [ObservableProperty] private ObservableCollection<string> _hourItems = new(Enumerable.Range(0, 24).Select(i => i.ToString("D2")));
    [ObservableProperty] private string _selectedHour = DateTime.Now.ToString("HH");

    [ObservableProperty] private ObservableCollection<string> _minuteItems = new(Enumerable.Range(0, 60).Select(i => i.ToString("D2")));
    [ObservableProperty] private string _selectedMinute = DateTime.Now.ToString("mm");

    [ObservableProperty] private ObservableCollection<string> _departmentItems = [];
    [ObservableProperty] private string _department = string.Empty;

    [ObservableProperty] private ObservableCollection<string> _doctorItems = [];
    [ObservableProperty] private string _doctor = string.Empty;

    [ObservableProperty] private string _dx = string.Empty;
    [ObservableProperty] private string _allergy = string.Empty;

    public MainViewModel(ISettingsService settingsService)
    {
        _settingsService = settingsService;
    }

    // เรียกใช้ตอน OnAppearing ของ MainPage
    public async Task InitializeAsync()
    {
        IsLoading = true;
        try
        {
            CurrentConfig = await _settingsService.LoadSettingsAsync() ?? new AppConfig();
            LoadDropdownConfigs();

            // โหลดข้อมูล CSV จาก Setting ทันทีที่เปิดหน้า
            string defaultPath = CurrentConfig.PatientRegister.DefaultPath;
            if (!string.IsNullOrWhiteSpace(defaultPath) && File.Exists(defaultPath))
            {
                await LoadCsvDataAsync(defaultPath);
            }
        }
        finally
        {
            IsLoading = false;
        }
    }

    private void LoadDropdownConfigs()
    {
        var deptConfig = CurrentConfig.PatientRegister.DepartmentList ?? "OPD, IPD";
        var docConfig = CurrentConfig.PatientRegister.DoctorList ?? "-";

        var depts = deptConfig.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        var docs = docConfig.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                          .Where(d => d != "-").ToArray();

        MainThread.BeginInvokeOnMainThread(() =>
        {
            DepartmentItems = new ObservableCollection<string>(depts);
            if (DepartmentItems.Any()) Department = DepartmentItems.First();

            DoctorItems = new ObservableCollection<string>(docs);
            if (DoctorItems.Any()) Doctor = DoctorItems.First();
        });
    }

    // ระบบอ่านไฟล์ CSV และประมวลผล (ทำงานบน Background Thread เพื่อไม่ให้ UI ค้าง)
    private async Task LoadCsvDataAsync(string filePath)
    {
        try
        {
            IsLoading = true;
            await Task.Run(() =>
            {
                var lines = File.ReadAllLines(filePath);
                if (lines.Length == 0) return;

                var headers = FastCsvSplit(lines[0]);
                var tempRows = new List<Dictionary<string, string>>(lines.Length);

                for (int i = 1; i < lines.Length; i++)
                {
                    if (string.IsNullOrWhiteSpace(lines[i])) continue;
                    var values = FastCsvSplit(lines[i]);
                    var row = new Dictionary<string, string>();

                    for (int j = 0; j < headers.Length; j++)
                    {
                        row[headers[j]] = j < values.Length ? values[j] : string.Empty;
                    }
                    tempRows.Add(row);
                }

                _allCsvRows = tempRows;

                MainThread.BeginInvokeOnMainThread(() =>
                {
                    // 🌟 แก้ไขตรงนี้: สร้าง Instance ใหม่แทนการ Clear/Add เพื่อบังคับให้ PropertyChanged ทำงาน
                    CsvColumns = new ObservableCollection<string>(headers);

                    CurrentPage = 1;
                    SearchText = string.Empty;
                    ApplyFilters();
                });
            });
        }
        catch (Exception ex)
        {
            MainThread.BeginInvokeOnMainThread(() =>
            {
                Application.Current?.MainPage?.DisplayAlert("Error", $"ไม่สามารถโหลดไฟล์ CSV ได้: {ex.Message}", "OK");
            });
        }
        finally
        {
            IsLoading = false;
        }
    }

    private static string[] FastCsvSplit(string line)
    {
        var result = new List<string>();
        bool inQuotes = false;
        int startIndex = 0;
        for (int i = 0; i < line.Length; i++)
        {
            if (line[i] == '\"')
            {
                inQuotes = !inQuotes;
            }
            else if (line[i] == ',' && !inQuotes)
            {
                result.Add(line.Substring(startIndex, i - startIndex).Trim(' ', '\"'));
                startIndex = i + 1;
            }
        }
        result.Add(line.Substring(startIndex).Trim(' ', '\"'));
        return result.ToArray();
    }

    // ระบบคัดกรองข้อมูล ค้นหา และคำนวณหน้า (Pagination)
    private void ApplyFilters()
    {
        IEnumerable<Dictionary<string, string>> query = _allCsvRows;

        if (!string.IsNullOrWhiteSpace(SearchText))
        {
            var keyword = SearchText.Trim().ToLower();
            query = query.Where(row => row.Values.Any(val => val != null && val.ToLower().Contains(keyword)));
        }

        var filteredList = query.ToList();

        TotalPages = (int)Math.Ceiling(filteredList.Count / (double)PageSize);
        if (TotalPages == 0) TotalPages = 1;
        if (CurrentPage > TotalPages) CurrentPage = TotalPages;

        var pagedData = filteredList.Skip((CurrentPage - 1) * PageSize).Take(PageSize).ToList();

        CsvRows.Clear();
        foreach (var item in pagedData)
        {
            CsvRows.Add(item);
        }

        SearchResultMessage = $"พบข้อมูลทั้งหมด {filteredList.Count} รายการ";
    }

    private void UpdateDisplayPatientDetails()
    {
        DisplayPatientDetails.Clear();
        if (SelectedPatient == null) return;

        var formColumns = CurrentConfig.PatientRegister.FormDisplayColumns;
        if (string.IsNullOrWhiteSpace(formColumns))
        {
            DisplayPatientDetails.Add(new PatientDetailModel { Key = "HN", Value = SelectedPatient.GetValueOrDefault("HN", "-") });
            DisplayPatientDetails.Add(new PatientDetailModel { Key = "Name", Value = SelectedPatient.GetValueOrDefault("Name", SelectedPatient.GetValueOrDefault("First_Name", "-")) });
        }
        else
        {
            var colsToDisplay = formColumns.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
            foreach (var col in colsToDisplay)
            {
                var matchedKey = SelectedPatient.Keys.FirstOrDefault(k => k.Equals(col, StringComparison.OrdinalIgnoreCase));
                if (matchedKey != null && SelectedPatient.TryGetValue(matchedKey, out var val))
                {
                    DisplayPatientDetails.Add(new PatientDetailModel { Key = col, Value = val ?? string.Empty });
                }
            }
        }
    }

    // --- Commands สำหรับ Data Grid ---
    [RelayCommand]
    private async Task OpenCsv()
    {
        try
        {
            var customFileType = new FilePickerFileType(new Dictionary<DevicePlatform, IEnumerable<string>>
            {
                { DevicePlatform.WinUI, new[] { ".csv" } },
                { DevicePlatform.Android, new[] { "text/csv" } },
                { DevicePlatform.iOS, new[] { "public.comma-separated-values-text" } },
                { DevicePlatform.MacCatalyst, new[] { "public.comma-separated-values-text" } }
            });

            var result = await FilePicker.Default.PickAsync(new PickOptions
            {
                PickerTitle = "เลือกไฟล์ CSV ข้อมูลผู้ป่วย",
                FileTypes = customFileType
            });

            if (result != null)
            {
                await LoadCsvDataAsync(result.FullPath);
            }
        }
        catch (Exception ex)
        {
            await Application.Current.MainPage.DisplayAlert("แจ้งเตือน", $"ไม่สามารถเปิดไฟล์ได้: {ex.Message}", "ตกลง");
        }
    }

    [RelayCommand]
    private async Task Refresh()
    {
        string defaultPath = CurrentConfig.PatientRegister.DefaultPath;
        if (!string.IsNullOrWhiteSpace(defaultPath) && File.Exists(defaultPath))
        {
            await LoadCsvDataAsync(defaultPath);
        }
    }

    [RelayCommand]
    private void PreviousPage()
    {
        if (CurrentPage > 1)
        {
            CurrentPage--;
            ApplyFilters();
        }
    }

    [RelayCommand]
    private void NextPage()
    {
        if (CurrentPage < TotalPages)
        {
            CurrentPage++;
            ApplyFilters();
        }
    }

    // --- Command นำทางไป OrderPage พร้อมส่ง Parameter ---
    [RelayCommand]
    private async Task OpenOrderPage()
    {
        if (SelectedPatient == null)
        {
            if (Application.Current?.MainPage != null)
                await Application.Current.MainPage.DisplayAlert("แจ้งเตือน", "กรุณาเลือกข้อมูลผู้ป่วยก่อนสร้าง Order", "ตกลง");
            return;
        }

        string orderCsvPath = CurrentConfig?.OrderItem?.DefaultPath ?? string.Empty;

        var navParams = new Dictionary<string, object>
        {
            { "PatientData", SelectedPatient },
            { "CsvPath", orderCsvPath }
        };

        await Shell.Current.GoToAsync(nameof(OrderPage), navParams);
    }

    // --- Commands สำหรับ Register Modal ---
    [RelayCommand]
    private void OpenRegisterModal()
    {
        if (SelectedPatient == null)
        {
            Application.Current?.MainPage?.DisplayAlert("แจ้งเตือน", "กรุณาเลือกข้อมูลผู้ป่วยก่อนสร้างฟอร์ม", "ตกลง");
            return;
        }

        Dx = string.Empty;
        Allergy = string.Empty;
        AppointmentDate = DateTime.Today;
        SelectedHour = DateTime.Now.ToString("HH");
        SelectedMinute = DateTime.Now.ToString("mm");

        IsRegisterModalVisible = true;
    }

    [RelayCommand]
    private void CloseRegisterModal()
    {
        IsRegisterModalVisible = false;
    }

    [RelayCommand]
    private void PrintRegistration()
    {
        var patientInfoBuilder = new System.Text.StringBuilder();
        foreach (var detail in DisplayPatientDetails)
        {
            if (!string.IsNullOrWhiteSpace(detail.Value))
            {
                patientInfoBuilder.AppendLine($@"
                <tr>
                    <td class='label-col'>{detail.Key}</td>
                    <td class='colon-col'>:</td>
                    <td class='value-col'>{detail.Value}</td>
                </tr>");
            }
        }

        string printContent = $@"
        <!DOCTYPE html>
        <html>
        <head>
            <meta charset='utf-8'>
            <style>
                @page {{ size: A4 portrait; margin: 1.5cm; }}
                body {{
                    font-family: 'Cordia New', 'Leelawadee UI', 'Segoe UI', sans-serif;
                    font-size: 14px; 
                    color: black;
                    padding: 0;
                    margin: 0;
                    line-height: 1.15; 
                }}
                h2 {{ text-align: center; font-size: 18px; margin-bottom: 5px; }}
                hr {{ border: 0; border-top: 1px solid #ccc; margin: 8px 0; }}
                table {{ width: 100%; border-collapse: collapse; }}
                td {{ padding: 3px 0; vertical-align: top; }}
                .label-col {{ width: 120px; white-space: nowrap; font-weight: bold; }}
                .colon-col {{ width: 15px; text-align: center; }}
                .value-col {{ width: auto; }}
                .footer {{ text-align: center; font-size: 12px; margin-top: 15px; }}
            </style>
        </head>
        <body>
            <h2>ใบจองคิว / นัดหมายแพทย์</h2>
            <hr/>
            <table>
                <tr>
                    <td class='label-col'>วันที่ / Date</td>
                    <td class='colon-col'>:</td>
                    <td class='value-col'>{AppointmentDate:dd/MM/yyyy} &nbsp;&nbsp;&nbsp;&nbsp; เวลา / Time : {SelectedHour}:{SelectedMinute} น.</td>
                </tr>
                {patientInfoBuilder}
                <tr>
                    <td class='label-col'>อาการเบื้องต้น</td>
                    <td class='colon-col'>:</td>
                    <td class='value-col'>{(string.IsNullOrEmpty(Dx) ? "-" : Dx)}</td>
                </tr>
                <tr>
                    <td class='label-col'>Allergies (แพ้ยา)</td>
                    <td class='colon-col'>:</td>
                    <td class='value-col'>{(string.IsNullOrEmpty(Allergy) ? "-" : Allergy)}</td>
                </tr>
                <tr>
                    <td class='label-col'>แผนก / Dept</td>
                    <td class='colon-col'>:</td>
                    <td class='value-col'>{Department}</td>
                </tr>
                <tr>
                    <td class='label-col'>แพทย์ / Doctor</td>
                    <td class='colon-col'>:</td>
                    <td class='value-col'>{Doctor}</td>
                </tr>
            </table>
            <hr/>
            <div class='footer'>*** กรุณานำใบนี้มายื่นที่หน้าเคาน์เตอร์ ***</div>
        </body>
        </html>";

        WeakReferenceMessenger.Default.Send(new PrintHtmlMessage(printContent));
    }
}