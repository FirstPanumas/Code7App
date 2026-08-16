using Code7App.Models;

using Code7App.Services;

using CommunityToolkit.Mvvm.ComponentModel;

using CommunityToolkit.Mvvm.Input;

using CommunityToolkit.Mvvm.Messaging;

using System.Collections.ObjectModel;

using System.Text;

namespace Code7App.ViewModels;

public record PrintHtmlMessage(string HtmlContent);

public partial class MainViewModel : ObservableObject

{

    private readonly ISettingsService _settingsService;

    private readonly ICsvReaderService _csvReaderService;

    private readonly IRegistrationService _registrationService;



    private List<Dictionary<string, string>> _allCsvData = [];

    private List<Dictionary<string, string>> _filteredCsvData = [];

    private const int PageSize = 100;

    private CancellationTokenSource? _searchCts;



    private bool _isInitialized = false;



    [ObservableProperty]
    private List<string> _filterOptions = [];

    [ObservableProperty]
    private string _filterLabel = "ตัวกรอง";

    [ObservableProperty]

    private string _selectedFilterValue = "ทั้งหมด";



    [ObservableProperty]

    private AppConfig _currentConfig = new();



    [ObservableProperty]

    private bool _isLoading;



    [ObservableProperty]

    private bool _isRegisterModalVisible;



    [ObservableProperty]

    private ObservableCollection<Dictionary<string, string>> _csvRows = [];



    [ObservableProperty]

    private List<string> _csvColumns = [];



    [ObservableProperty]

    private Dictionary<string, string>? _selectedPatient;



    [ObservableProperty]

    private ObservableCollection<KeyValuePair<string, string>> _displayPatientDetails = [];



    [ObservableProperty]

    private string _searchText = string.Empty;



    [ObservableProperty]

    private string _searchResultMessage = string.Empty;



    [ObservableProperty]

    private int _currentPage = 1;



    [ObservableProperty]

    private int _totalPages = 1;



    [ObservableProperty]

    private int _totalRecords = 0;



    // --- Properties สำหรับ Form Registration ---

    [ObservableProperty]

    private DateTime _appointmentDate = DateTime.Today;



    // สร้าง List ตัวเลือก 01-24 และ 00-59

    [ObservableProperty]

    private List<string> _hourItems = Enumerable.Range(1, 24).Select(h => h.ToString("D2")).ToList();



    [ObservableProperty]

    private List<string> _minuteItems = Enumerable.Range(0, 60).Select(m => m.ToString("D2")).ToList();



    // กำหนดค่าเริ่มต้นให้อยู่ใน List เสมอ

    [ObservableProperty]

    private string _selectedHour = (DateTime.Now.Hour == 0 ? 24 : DateTime.Now.Hour).ToString("D2");



    [ObservableProperty]

    private string _selectedMinute = DateTime.Now.Minute.ToString("D2");



    [ObservableProperty]

    private string _department = string.Empty;



    [ObservableProperty]

    private string _doctor = string.Empty;



    [ObservableProperty]

    private string _dx = string.Empty;



    [ObservableProperty]

    private string _allergy = string.Empty;



    [ObservableProperty]

    private List<string> _departmentItems = [];



    [ObservableProperty]

    private List<string> _doctorItems = [];

    // -------------------------------------------



    public MainViewModel(ISettingsService settingsService, ICsvReaderService csvReaderService, IRegistrationService registrationService)

    {

        _settingsService = settingsService;

        _csvReaderService = csvReaderService;

        _registrationService = registrationService;

    }



    public async Task InitializeAsync()

    {

        if (_isInitialized) return;

        _isInitialized = true;



        CurrentConfig = await _settingsService.LoadSettingsAsync();

        UpdateDropdownLists();



        var defaultPath = CurrentConfig.PatientRegister?.DefaultPath;

        if (!string.IsNullOrWhiteSpace(defaultPath) && File.Exists(defaultPath))

        {

            await LoadCsvFileAsync(defaultPath);

        }

    }



    private void UpdateDropdownLists()

    {

        var deptSetting = CurrentConfig?.PatientRegister?.DepartmentList ?? string.Empty;

        DepartmentItems = deptSetting.Split(',')

            .Select(s => s.Trim())

            .Where(s => !string.IsNullOrEmpty(s))

            .ToList();



        var docSetting = CurrentConfig?.PatientRegister?.DoctorList ?? string.Empty;

        DoctorItems = docSetting.Split(',')

            .Select(s => s.Trim())

            .Where(s => !string.IsNullOrEmpty(s))

            .ToList();



        if (DepartmentItems.Any() && !DepartmentItems.Contains(Department))

            Department = DepartmentItems.First();



        if (DoctorItems.Any() && !DoctorItems.Contains(Doctor))

            Doctor = DoctorItems.First();

    }



    partial void OnSelectedPatientChanged(Dictionary<string, string>? value)

    {

        DisplayPatientDetails.Clear();



        if (value != null)

        {

            var columnsSetting = CurrentConfig?.PatientRegister?.FormDisplayColumns;



            if (string.IsNullOrWhiteSpace(columnsSetting))

            {

                foreach (var kvp in value) DisplayPatientDetails.Add(kvp);

            }

            else

            {

                var allowedColumns = columnsSetting.Split(',')

                    .Select(c => c.Trim())

                    .Where(c => !string.IsNullOrEmpty(c))

                    .ToHashSet(StringComparer.OrdinalIgnoreCase);



                foreach (var kvp in value)

                {

                    if (allowedColumns.Contains(kvp.Key))

                    {

                        DisplayPatientDetails.Add(kvp);

                    }

                }

            }

        }

    }



    partial void OnSearchTextChanged(string value)

    {

        _searchCts?.Cancel();

        _searchCts = new CancellationTokenSource();

        var token = _searchCts.Token;



        Task.Run(async () =>

        {

            try

            {

                await Task.Delay(500, token);



                if (!token.IsCancellationRequested)

                {

                    PerformSearch(value);

                }

            }

            catch (TaskCanceledException) { }

        }, token);

    }



    [RelayCommand]

    private async Task OpenCsvAsync()

    {

        try

        {

            var customFileType = new FilePickerFileType(new Dictionary<DevicePlatform, IEnumerable<string>>

            {

                { DevicePlatform.WinUI, new[] { ".csv", ".txt" } },

                { DevicePlatform.MacCatalyst, new[] { "csv", "public.comma-separated-values" } }

            });



            var result = await FilePicker.Default.PickAsync(new PickOptions

            {

                PickerTitle = "Please select a CSV file",

                FileTypes = customFileType

            });



            if (result != null)

            {

                await LoadCsvFileAsync(result.FullPath);

            }

        }

        catch (Exception ex)

        {

            Console.WriteLine($"Error opening CSV: {ex.Message}");

        }

    }



    private async Task LoadCsvFileAsync(string filePath)
    {
        try
        {
            IsLoading = true;
            await Task.Delay(100);

            CurrentConfig = await _settingsService.LoadSettingsAsync();
            UpdateDropdownLists();

            var data = await Task.Run(() => _csvReaderService.ReadCsvAsync(filePath));

            CsvColumns = data.Columns;
            _allCsvData = data.Rows;

            // 1. เรียกคำสั่งอัปเดตรายการใน Dropdown ทันทีเมื่ออ่าน CSV เสร็จ
            UpdateFilterDropdown();

            // 2. เคลียร์ค่าที่เลือกไว้
            SelectedPatient = null;
            SearchText = string.Empty;

            // 3. ใช้ PerformSearch(ค่าว่าง) เพื่อบังคับให้ระบบคำนวณ Pagination และโหลดข้อมูลทั้งหมดเข้า CsvRows ทันที
            PerformSearch(string.Empty);
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error opening CSV: {ex.Message}");
        }
        finally
        {
            IsLoading = false;
        }
    }


    private void PerformSearch(string query)
    {
        var filtered = _allCsvData.AsEnumerable();

        // 1. กรองตาม Search Text
        if (!string.IsNullOrWhiteSpace(query))
        {
            var trimmedQuery = query.Trim();
            filtered = filtered.Where(row => row.Values.Any(val => val != null && val.Contains(trimmedQuery, StringComparison.OrdinalIgnoreCase)));
        }

        // 2. กรองตาม Dropdown Column
        var filterColConfig = CurrentConfig?.PatientRegister?.DropdownColumn?.Trim();
        if (!string.IsNullOrWhiteSpace(filterColConfig) && SelectedFilterValue != "ทั้งหมด" && !string.IsNullOrEmpty(SelectedFilterValue))
        {
            var actualColumn = CsvColumns.FirstOrDefault(c =>
                c?.Trim().Equals(filterColConfig, StringComparison.OrdinalIgnoreCase) == true);

            if (!string.IsNullOrEmpty(actualColumn))
            {
                filtered = filtered.Where(row => row.ContainsKey(actualColumn) && row[actualColumn]?.Trim() == SelectedFilterValue);
            }
        }

        _filteredCsvData = filtered.ToList();

        // 3. คำนวณ Pagination และอัปเดต UI State
        TotalRecords = _filteredCsvData.Count;
        TotalPages = (int)Math.Ceiling((double)TotalRecords / PageSize);
        if (TotalPages == 0) TotalPages = 1;
        CurrentPage = 1;

        SearchResultMessage = TotalRecords > 0 ? $"พบข้อมูลทั้งหมด {TotalRecords} รายการ" : "ไม่พบข้อมูลที่ค้นหา";

        // 4. ดึงข้อมูลหน้าแรกมาแสดง
        LoadPageData();
    }



    partial void OnSelectedFilterValueChanged(string value) => PerformSearch(SearchText);

    public void UpdateFilterDropdown()
    {
        var filterColConfig = CurrentConfig?.PatientRegister?.DropdownColumn?.Trim();

        if (string.IsNullOrWhiteSpace(filterColConfig) || CsvColumns == null || !CsvColumns.Any())
        {
            FilterLabel = "ตัวกรอง";
            FilterOptions = [];
            SelectedFilterValue = "ทั้งหมด";
            return;
        }

        var actualColumn = CsvColumns.FirstOrDefault(c =>
            c?.Trim().Equals(filterColConfig, StringComparison.OrdinalIgnoreCase) == true);

        if (string.IsNullOrEmpty(actualColumn))
        {
            FilterLabel = "ตัวกรอง";
            FilterOptions = [];
            SelectedFilterValue = "ทั้งหมด";
            return;
        }

        FilterLabel = actualColumn;

        var options = _allCsvData
            .Where(r => r.ContainsKey(actualColumn) && !string.IsNullOrWhiteSpace(r[actualColumn]))
            .Select(r => r[actualColumn].Trim())
            .Distinct()
            .OrderBy(v => v)
            .ToList();

        options.Insert(0, "ทั้งหมด");

        // Re-assign List กลับเข้าไปเพื่อให้ UI รับรู้การเปลี่ยนแปลง
        FilterOptions = options;

        // บังคับให้ SelectedItem ชี้ไปที่ Index 0 ของ List ตัวใหม่เพื่ออัปเดต UI ให้แสดงคำว่า "ทั้งหมด" ทันที
        SelectedFilterValue = FilterOptions.FirstOrDefault() ?? "ทั้งหมด";
    }

    private void LoadPageData()

    {

        if (_filteredCsvData.Count == 0)

        {

            CsvRows = [];

            return;

        }



        var pagedData = _filteredCsvData

            .Skip((Math.Max(1, CurrentPage) - 1) * PageSize)

            .Take(PageSize)

            .ToList();



        CsvRows = new ObservableCollection<Dictionary<string, string>>(pagedData);

    }



    [RelayCommand]

    private void NextPage()

    {

        if (CurrentPage < TotalPages)

        {

            CurrentPage++;

            LoadPageData();

        }

    }



    [RelayCommand]

    private void PreviousPage()

    {

        if (CurrentPage > 1)

        {

            CurrentPage--;

            LoadPageData();

        }

    }



    [RelayCommand]

    private void OpenRegisterModal()

    {

        if (SelectedPatient == null) return;

        IsRegisterModalVisible = true;

    }



    [RelayCommand]

    private void CloseRegisterModal() => IsRegisterModalVisible = false;



    [RelayCommand]

    private async Task SaveRegistrationAsync()

    {

        if (SelectedPatient == null) return;



        IsLoading = true;



        // แปลงชั่วโมงและนาทีจาก Picker ให้เป็น TimeSpan

        int hour = int.TryParse(SelectedHour, out int h) ? h : 0;

        int minute = int.TryParse(SelectedMinute, out int m) ? m : 0;



        // จัดการกรณี 24 นาฬิกา (ปรับเป็น 00:00 ตามมาตรฐาน TimeSpan)

        if (hour == 24) hour = 0;



        TimeSpan appointmentTime = new TimeSpan(hour, minute, 0);



        bool isSuccess = await _registrationService.SaveRegistrationAsync(

            SelectedPatient,

            AppointmentDate,

            appointmentTime,

            Department,

            Doctor,

            Dx,

            Allergy);



        IsLoading = false;

        IsRegisterModalVisible = false;



        // Reset ข้อมูลฟอร์มกลับเป็นค่าเริ่มต้น

        Allergy = string.Empty;

        Dx = string.Empty;

        AppointmentDate = DateTime.Today;

        SelectedHour = (DateTime.Now.Hour == 0 ? 24 : DateTime.Now.Hour).ToString("D2");

        SelectedMinute = DateTime.Now.Minute.ToString("D2");

        Department = DepartmentItems.FirstOrDefault() ?? string.Empty;

        Doctor = DoctorItems.FirstOrDefault() ?? string.Empty;



        if (isSuccess)

            await Application.Current!.MainPage!.DisplayAlert("Success", "บันทึกข้อมูลสำเร็จ", "OK");

        else

            await Application.Current!.MainPage!.DisplayAlert("Error", "เกิดข้อผิดพลาด", "OK");

    }
    [RelayCommand]
    private async Task RefreshAsync()
    {
        IsLoading = true;

        try
        {
            // 1. โหลดการตั้งค่า (Settings) ล่าสุด
            CurrentConfig = await _settingsService.LoadSettingsAsync();
            UpdateDropdownLists();

            // 2. ตรวจสอบว่ามี DefaultPath ใน Settings หรือไม่ และไฟล์มีอยู่จริงหรือไม่
            var defaultPath = CurrentConfig?.PatientRegister?.DefaultPath;
            if (!string.IsNullOrWhiteSpace(defaultPath) && System.IO.File.Exists(defaultPath))
            {
                // สั่งโหลดข้อมูลใหม่ทั้งหมด
                await LoadCsvFileAsync(defaultPath);
            }
            else
            {
                // หากไม่มีการตั้งค่าไฟล์เริ่มต้น ให้เคลียร์ข้อมูลหน้าจอ
                _allCsvData = [];
                _filteredCsvData = [];
                CsvRows.Clear();
                CsvColumns.Clear();
                FilterOptions.Clear();

                SearchText = string.Empty;
                TotalRecords = 0;
                TotalPages = 1;
                CurrentPage = 1;
                SearchResultMessage = "ไม่พบไฟล์ข้อมูลตั้งต้น (Default Path) กรุณาตั้งค่าหรือเปิดไฟล์ CSV ใหม่";
            }
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Refresh Error: {ex.Message}");
            SearchResultMessage = "เกิดข้อผิดพลาดในการรีเฟรชข้อมูล";
        }
        finally
        {
            // ป้องกัน Loader ค้างกรณีไม่ได้เข้า Block ของ LoadCsvFileAsync
            if (!string.IsNullOrWhiteSpace(CurrentConfig?.PatientRegister?.DefaultPath) && !System.IO.File.Exists(CurrentConfig.PatientRegister.DefaultPath))
            {
                IsLoading = false;
            }
        }
    }

    [RelayCommand]

    private async Task PrintRegistrationAsync()
    {
        if (SelectedPatient == null) return;

        IsLoading = true;

        // จัดการเรื่องเวลา
        int hour = int.TryParse(SelectedHour, out int h) ? h : 0;
        int minute = int.TryParse(SelectedMinute, out int m) ? m : 0;
        if (hour == 24) hour = 0;
        TimeSpan appointmentTime = new TimeSpan(hour, minute, 0);

        IsLoading = false;

        // 1. สร้าง HTML ข้อมูลผู้ป่วยแบบ Dynamic
        StringBuilder patientInfoBuilder = new StringBuilder();
        if (DisplayPatientDetails != null && DisplayPatientDetails.Any())
        {
            foreach (var detail in DisplayPatientDetails)
            {
                patientInfoBuilder.Append($@"
                <tr>
                    <td class='label-col'>{detail.Key}</td>
                    <td class='colon-col'>:</td>
                    <td class='value-col'>{detail.Value}</td>
                </tr>");
            }
        }
        else
        {
            patientInfoBuilder.Append("<tr><td colspan='3' style='color:red;'>ไม่พบข้อมูลผู้ป่วยที่ตั้งค่าไว้ใน Settings</td></tr>");
        }

        // 2. ประกอบร่าง HTML ทั้งหมด พร้อมปรับ CSS ใหม่
        string printContent = $@"
        <html>
        <head>
            <style>
                body {{
                    /* เปลี่ยนฟอนต์ให้เรียวคมขึ้น (Cordia New, Leelawadee UI หรือ Segoe UI) */
                    font-family: 'Cordia New', 'Leelawadee UI', 'Segoe UI', sans-serif;
                    
                    /* ปรับขนาดฟอนต์ให้เหมาะกับ Cordia/Leelawadee */
                    font-size: 14px; 
                    color: black;
                    padding: 15px;
                    
                    /* ลดระยะห่างระหว่างบรรทัด (จากเดิม 1.4) */
                    line-height: 1.15; 
                }}
                h2 {{
                    text-align: center;
                    font-size: 18px;
                    margin-bottom: 5px;
                }}
                hr {{
                    border: 0;
                    border-top: 1px solid #ccc;
                    margin: 8px 0;
                }}
                table {{
                    width: 100%;
                    border-collapse: collapse;
                }}
                td {{
                    /* ปรับลดระยะห่างบน-ล่างของแต่ละแถวในตารางให้ชิดกันมากขึ้น */
                    padding: 3px 0; 
                    vertical-align: top;
                }}
                .label-col {{
                    width: 120px;
                    white-space: nowrap;
                }}
                .colon-col {{
                    width: 15px;
                    text-align: center;
                }}
                .value-col {{
                    width: auto;
                }}
                .footer {{
                    text-align: center;
                    font-size: 12px;
                    margin-top: 15px;
                }}
            </style>
        </head>
        <body>
            <h2>ใบจองคิว / นัดหมายแพทย์</h2>
            <hr/>
            
            <table>
                <tr>
                    <td class='label-col'>วันที่ / Date</td>
                    <td class='colon-col'>:</td>
                    <td class='value-col'>{AppointmentDate:dd/MM/yyyy} &nbsp;&nbsp;&nbsp;&nbsp; เวลา / Time : {appointmentTime:hh\:mm} น.</td>
                </tr>
                
                {patientInfoBuilder.ToString()}
                
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

        // 3. ส่ง Message นำ HTML ไปให้ View เพื่อสร้าง PDF
        WeakReferenceMessenger.Default.Send(new PrintHtmlMessage(printContent));

        // 4. ปิดฟอร์มและเคลียร์ค่า
        IsRegisterModalVisible = false;
        Allergy = string.Empty;
        Dx = string.Empty;
        AppointmentDate = DateTime.Today;
        SelectedHour = (DateTime.Now.Hour == 0 ? 24 : DateTime.Now.Hour).ToString("D2");
        SelectedMinute = DateTime.Now.Minute.ToString("D2");
        Department = DepartmentItems.FirstOrDefault() ?? string.Empty;
        Doctor = DoctorItems.FirstOrDefault() ?? string.Empty;
    }

    private async Task GenerateAndPrintReceiptAsync(TimeSpan time)

    {

        try

        {

            string patientName = SelectedPatient != null && SelectedPatient.ContainsKey("Name") ? SelectedPatient["Name"] : "ไม่ระบุ";

            string hn = SelectedPatient != null && SelectedPatient.ContainsKey("HN") ? SelectedPatient["HN"] : "-";



            // 1. สร้างเอกสารในรูปแบบ HTML เพื่อให้หน้าต่าง Print ของ Windows จัดหน้าได้สวยงาม

            string printContent = $@"

            <html>

            <body style='font-family: Tahoma, sans-serif; padding: 20px; color: black;'>

                <h2 style='text-align: center;'>ใบจองคิว / นัดหมายแพทย์</h2>

                <hr/>

                <p><strong>วันที่:</strong> {AppointmentDate:dd/MM/yyyy} &nbsp;&nbsp;&nbsp; <strong>เวลา:</strong> {time:hh\:mm} น.</p>

                <h3>ข้อมูลผู้ป่วย</h3>

                <p><strong>HN:</strong> {hn}</p>

                <p><strong>ชื่อ-สกุล:</strong> {patientName}</p>

                <p><strong>อาการเบื้องต้น:</strong> {(string.IsNullOrEmpty(Dx) ? "-" : Dx)}</p>

                <p><strong>ประวัติแพ้ยา:</strong> {(string.IsNullOrEmpty(Allergy) ? "-" : Allergy)}</p>

                <h3>ข้อมูลการนัดหมาย</h3>

                <p><strong>แผนก:</strong> {Department}</p>

                <p><strong>แพทย์:</strong> {Doctor}</p>

                <hr/>

                <p style='text-align: center; font-size: 12px;'>*** กรุณานำใบนี้มายื่นที่หน้าเคาน์เตอร์ ***</p>

            </body>

            </html>";



            // 2. ส่ง Message ไปหา View (MainPage.xaml.cs) เพื่อให้สั่ง WebView เปิดหน้า Print

            WeakReferenceMessenger.Default.Send(new PrintHtmlMessage(printContent));

        }

        catch (Exception ex)

        {

            await Application.Current!.MainPage!.DisplayAlert("Print Error", $"ไม่สามารถเตรียมเอกสารได้: {ex.Message}", "OK");

        }

    }



}