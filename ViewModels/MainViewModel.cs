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
    private List<Dictionary<string, string>> _allCsvRows = new();
    private const int PageSize = 100;
    private bool _isInitialized = false;

    [ObservableProperty] private AppConfig _currentConfig = new();
    [ObservableProperty] private bool _isLoading;
    [ObservableProperty] private ObservableCollection<Dictionary<string, string>> _csvRows = [];
    [ObservableProperty] private ObservableCollection<string> _csvColumns = [];

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
    [ObservableProperty] private string _filterLabel = "ตัวกรอง";
    [ObservableProperty] private ObservableCollection<string> _filterOptions = [];
    [ObservableProperty] private string _searchResultMessage = string.Empty;

    private string _searchText = string.Empty;
    public string SearchText
    {
        get => _searchText;
        set { if (SetProperty(ref _searchText, value)) { CurrentPage = 1; ApplyFilters(); } }
    }

    private string _selectedFilterValue = string.Empty;
    public string SelectedFilterValue
    {
        get => _selectedFilterValue;
        set { if (SetProperty(ref _selectedFilterValue, value)) { CurrentPage = 1; ApplyFilters(); } }
    }

    // 🌟 1. เพิ่มตัวแปรสำหรับ Filter วันเกิด
    private bool _useDobFilter;
    public bool UseDobFilter
    {
        get => _useDobFilter;
        set { if (SetProperty(ref _useDobFilter, value)) { CurrentPage = 1; ApplyFilters(); } }
    }

    private DateTime _filterDob = DateTime.Today;
    public DateTime FilterDob
    {
        get => _filterDob;
        set { if (SetProperty(ref _filterDob, value)) { if (UseDobFilter) { CurrentPage = 1; ApplyFilters(); } } }
    }

    [ObservableProperty] private ObservableCollection<PatientDetailModel> _displayPatientDetails = [];
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
    [ObservableProperty] private string _payer = string.Empty;

    public MainViewModel(ISettingsService settingsService)
    {
        _settingsService = settingsService;
    }

    public async Task InitializeAsync()
    {
        if (_isInitialized) return;
        IsLoading = true;
        try
        {
            CurrentConfig = await _settingsService.LoadSettingsAsync() ?? new AppConfig();
            LoadDropdownConfigs();

            string defaultPath = CurrentConfig.PatientRegister?.DefaultPath ?? string.Empty;
            if (!string.IsNullOrWhiteSpace(defaultPath))
            {
                await LoadCsvDataAsync(defaultPath);
            }
            _isInitialized = true;
        }
        finally
        {
            IsLoading = false;
        }
    }

    private void LoadDropdownConfigs()
    {
        var deptConfig = CurrentConfig.PatientRegister?.DepartmentList ?? "OPD, IPD";
        var docConfig = CurrentConfig.PatientRegister?.DoctorList ?? "-";

        var depts = deptConfig.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        var docs = docConfig.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries).Where(d => d != "-").ToArray();

        MainThread.BeginInvokeOnMainThread(() =>
        {
            DepartmentItems = new ObservableCollection<string>(depts);
            if (DepartmentItems.Any()) Department = DepartmentItems.First();

            DoctorItems = new ObservableCollection<string>(docs);
            if (DoctorItems.Any()) Doctor = DoctorItems.First();
        });
    }

    private async Task LoadCsvDataAsync(string filePath)
    {
        try
        {
            IsLoading = true;

            // 1. อ่านและประมวลผลไฟล์ใน Background Thread อย่างสมบูรณ์
            var (headers, tempRows) = await Task.Run(() =>
            {
                var linesList = new List<string>();

                // ใช้ FileStream เพื่อป้องกันการโดน Lock จากโปรแกรมอื่น
                using (var stream = new FileStream(filePath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite))
                using (var reader = new StreamReader(stream, System.Text.Encoding.UTF8, true))
                {
                    string? line;
                    while ((line = reader.ReadLine()) != null)
                    {
                        linesList.Add(line);
                    }
                }

                if (linesList.Count == 0) return (Array.Empty<string>(), new List<Dictionary<string, string>>());

                var h = FastCsvSplit(linesList[0]);
                var rows = new List<Dictionary<string, string>>(linesList.Count);

                for (int i = 1; i < linesList.Count; i++)
                {
                    if (string.IsNullOrWhiteSpace(linesList[i])) continue;
                    var values = FastCsvSplit(linesList[i]);

                    var row = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

                    for (int j = 0; j < h.Length; j++)
                    {
                        row[h[j]] = j < values.Length ? values[j] : string.Empty;
                    }
                    rows.Add(row);
                }

                return (h, rows);
            });

            if (headers.Length == 0) return;

            // 2. อัปเดต UI (โค้ดจะทำงานบน Main Thread อัตโนมัติหลัง await)
            _allCsvRows = tempRows;

            string filterCol = CurrentConfig.PatientRegister?.DropdownColumn ?? string.Empty;
            string dropdownSetting = CurrentConfig.PatientRegister?.DropdownList ?? string.Empty;

            CsvColumns = new ObservableCollection<string>(headers);

            var options = new List<string>();
            if (!string.IsNullOrWhiteSpace(dropdownSetting))
            {
                options = dropdownSetting.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries).ToList();
            }
            else if (!string.IsNullOrWhiteSpace(filterCol) && headers.Contains(filterCol, StringComparer.OrdinalIgnoreCase))
            {
                options = _allCsvRows.Select(row => row.GetValueOrDefault(filterCol, string.Empty))
                                     .Where(val => !string.IsNullOrWhiteSpace(val))
                                     .Distinct()
                                     .OrderBy(val => val)
                                     .ToList();
            }

            options.Insert(0, "ทั้งหมด");
            FilterOptions = new ObservableCollection<string>(options);
            FilterLabel = !string.IsNullOrWhiteSpace(filterCol) ? filterCol : "ตัวกรอง";

            _selectedFilterValue = options.First();
            OnPropertyChanged(nameof(SelectedFilterValue));

            CurrentPage = 1;
            _searchText = string.Empty;
            OnPropertyChanged(nameof(SearchText));
            UseDobFilter = false;

            ApplyFilters();
        }
        catch (Exception ex)
        {
            if (Application.Current?.MainPage != null)
            {
                // แจ้งเตือน Alert เมื่อเกิด Error (เช่น Network เข้าไม่ได้)
                await Application.Current.MainPage.DisplayAlert("Error", $"ไม่สามารถโหลดไฟล์ CSV ได้:\n{ex.Message}", "OK");
            }
        }
        finally
        {
            // บังคับปิด Loading ไม่ว่าจะโหลดสำเร็จหรือเกิด Error
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

    private void ApplyFilters()
    {
        IEnumerable<Dictionary<string, string>> query = _allCsvRows;

        string filterCol = CurrentConfig.PatientRegister?.DropdownColumn ?? string.Empty;
        string dobCol = CurrentConfig.PatientRegister?.DobColumn ?? string.Empty;

        // 1. กรองด้วย Dropdown
        if (!string.IsNullOrEmpty(SelectedFilterValue) && SelectedFilterValue != "ทั้งหมด" && !string.IsNullOrEmpty(filterCol))
        {
            query = query.Where(row => row.TryGetValue(filterCol, out string? val) && val != null && val.Equals(SelectedFilterValue, StringComparison.OrdinalIgnoreCase));
        }

        // 🌟 2. กรองด้วย วันเกิด (บังคับ Format M/d/yyyy และ ค.ศ.)
        if (UseDobFilter && !string.IsNullOrEmpty(dobCol))
        {
            // สร้าง CultureInfo แบบ en-US เพื่อบังคับอ่านเป็น ค.ศ.
            var enUSCulture = new System.Globalization.CultureInfo("en-US");

            // รองรับความคลาดเคลื่อนของการพิมพ์ M/D/Y (เช่น 1/1/2026 หรือ 01/01/2026)
            string[] expectedFormats = { "M/d/yyyy", "MM/dd/yyyy", "M/dd/yyyy", "MM/d/yyyy" };

            query = query.Where(row =>
            {
                if (row.TryGetValue(dobCol, out string? val) && !string.IsNullOrWhiteSpace(val))
                {
                    if (DateTime.TryParseExact(val.Trim(), expectedFormats, enUSCulture, System.Globalization.DateTimeStyles.None, out DateTime csvDate))
                    {
                        return csvDate.Date == FilterDob.Date;
                    }

                    // Fallback กรณี String ไม่ตรงมาตรฐานเลย
                    return val.Contains(FilterDob.ToString("M/d/yyyy", enUSCulture)) ||
                           val.Contains(FilterDob.ToString("MM/dd/yyyy", enUSCulture));
                }
                return false;
            });
        }

        // 3. กรองด้วย Text Search
        if (!string.IsNullOrWhiteSpace(SearchText))
        {
            var keyword = SearchText.Trim().ToLower();
            query = query.Where(row => row.Values.Any(val => val != null && val.ToLower().Contains(keyword)));
        }

        // ... โค้ดส่วน Pagination ด้านล่างคงเดิม ...
        var filteredList = query.ToList();

        TotalPages = (int)Math.Ceiling(filteredList.Count / (double)PageSize);
        if (TotalPages == 0) TotalPages = 1;
        if (CurrentPage > TotalPages) CurrentPage = TotalPages;

        var pagedData = filteredList.Skip((CurrentPage - 1) * PageSize).Take(PageSize).ToList();

        CsvRows.Clear();
        foreach (var item in pagedData) CsvRows.Add(item);

        SearchResultMessage = $"พบข้อมูลทั้งหมด {filteredList.Count} รายการ";
    }

    private void UpdateDisplayPatientDetails()
    {
        DisplayPatientDetails.Clear();
        if (SelectedPatient == null) return;

        var formColumns = CurrentConfig.PatientRegister?.FormDisplayColumns ?? string.Empty;
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

    [RelayCommand]
    private async Task OpenCsv()
    {
        try
        {
            var customFileType = new FilePickerFileType(new Dictionary<DevicePlatform, IEnumerable<string>>
            {
                { DevicePlatform.WinUI, new[] { ".csv" } }, { DevicePlatform.Android, new[] { "text/csv" } },
                { DevicePlatform.iOS, new[] { "public.comma-separated-values-text" } }, { DevicePlatform.MacCatalyst, new[] { "public.comma-separated-values-text" } }
            });

            var result = await FilePicker.Default.PickAsync(new PickOptions { PickerTitle = "เลือกไฟล์ CSV ข้อมูลผู้ป่วย", FileTypes = customFileType });
            if (result != null) await LoadCsvDataAsync(result.FullPath);
        }
        catch (Exception ex)
        {
            await Application.Current.MainPage.DisplayAlert("แจ้งเตือน", $"ไม่สามารถเปิดไฟล์ได้: {ex.Message}", "ตกลง");
        }
    }

    [RelayCommand]
    private async Task Refresh()
    {
        string defaultPath = CurrentConfig.PatientRegister?.DefaultPath ?? string.Empty;
        if (!string.IsNullOrWhiteSpace(defaultPath) && File.Exists(defaultPath))
        {
            await LoadCsvDataAsync(defaultPath);
        }
    }

    [RelayCommand] private void PreviousPage() { if (CurrentPage > 1) { CurrentPage--; ApplyFilters(); } }
    [RelayCommand] private void NextPage() { if (CurrentPage < TotalPages) { CurrentPage++; ApplyFilters(); } }

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
        var navParams = new Dictionary<string, object> { { "PatientData", SelectedPatient }, { "CsvPath", orderCsvPath } };
        await Shell.Current.GoToAsync(nameof(OrderPage), navParams);
    }

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
        Payer = string.Empty;
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
            string displayValue = string.IsNullOrWhiteSpace(detail.Value) ? "-" : detail.Value;
            patientInfoBuilder.AppendLine($@"
            <tr>
                <td class='label-col'>{detail.Key}</td>
                <td class='colon-col'>:</td>
                <td class='value-col'>{displayValue}</td>
            </tr>");
        }

        string patientHtmlRows = patientInfoBuilder.ToString();

        string printContent = $@"
        <!DOCTYPE html>
        <html>
        <head>
            <meta charset='utf-8'>
            <style>
                @page {{ size: A5 landscape; margin: 1.0cm; }}
                body {{ font-family: 'Cordia New', 'Leelawadee UI', sans-serif; font-size: 14px; color: black; padding: 0; margin: 0; line-height: 1.15; }}
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
                {patientHtmlRows}
                <tr>
                    <td class='label-col'>Payer</td>
                    <td class='colon-col'>:</td>
                    <td class='value-col'>{(string.IsNullOrEmpty(Payer) ? "-" : Payer)}</td>
                </tr>
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