using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using CommunityToolkit.Mvvm.Messaging;
using System.Collections.ObjectModel;
using System.IO;
using Code7App.Models;
using Code7App.Services;
using Code7App.Messages;
using System.Windows.Input;

namespace Code7App.ViewModels;

public partial class OrderViewModel : ObservableObject
{
    private readonly ISettingsService _settingsService;
    public System.Windows.Input.ICommand CheckoutAndPrintCommand { get; }

    // --- 1. ตัวแปรสำหรับ Form ข้อมูลเพิ่มเติม ---
    private DateTime _orderDate = DateTime.Today;
    public DateTime OrderDate
    {
        get => _orderDate;
        set => SetProperty(ref _orderDate, value);
    }

    private ObservableCollection<string> _hourList = new(Enumerable.Range(0, 24).Select(i => i.ToString("D2")));
    public ObservableCollection<string> HourList
    {
        get => _hourList;
        set => SetProperty(ref _hourList, value);
    }

    private ObservableCollection<string> _minuteList = new(Enumerable.Range(0, 60).Select(i => i.ToString("D2")));
    public ObservableCollection<string> MinuteList
    {
        get => _minuteList;
        set => SetProperty(ref _minuteList, value);
    }

    private string _selectedHour = DateTime.Now.ToString("HH");
    public string SelectedHour
    {
        get => _selectedHour;
        set => SetProperty(ref _selectedHour, value);
    }

    private string _selectedMinute = DateTime.Now.ToString("mm");
    public string SelectedMinute
    {
        get => _selectedMinute;
        set => SetProperty(ref _selectedMinute, value);
    }

    private string _symptoms = string.Empty;
    public string Symptoms
    {
        get => _symptoms;
        set => SetProperty(ref _symptoms, value);
    }

    private string _allergy = string.Empty;
    public string Allergy
    {
        get => _allergy;
        set => SetProperty(ref _allergy, value);
    }

    // --- 2. ตัวแปรสำหรับ Picker (แผนก และ แพทย์) ---
    private string _selectedDepartment = string.Empty;
    public string SelectedDepartment
    {
        get => _selectedDepartment;
        set => SetProperty(ref _selectedDepartment, value);
    }

    private string _selectedDoctor = string.Empty;
    public string SelectedDoctor
    {
        get => _selectedDoctor;
        set => SetProperty(ref _selectedDoctor, value);
    }

    private ObservableCollection<string> _departmentList = [];
    public ObservableCollection<string> DepartmentList
    {
        get => _departmentList;
        set => SetProperty(ref _departmentList, value);
    }

    private ObservableCollection<string> _doctorList = [];
    public ObservableCollection<string> DoctorList
    {
        get => _doctorList;
        set => SetProperty(ref _doctorList, value);
    }

    // --- 3. ตัวแปรเดิมของระบบ ---
    [ObservableProperty]
    private string _filterLabel = "หมวดหมู่";

    [ObservableProperty]
    private ObservableCollection<string> _filterOptions = [];

    [ObservableProperty]
    private Dictionary<string, string> _patientRawData = [];

    [ObservableProperty]
    private string _csvPath = string.Empty;

    [ObservableProperty]
    private ObservableCollection<PatientDetailModel> _displayPatientDetails = [];

    private List<OrderItemModel> _allOrders = [];
    private List<string> _csvHeaders = [];
    private List<(string TargetName, int ActualIndex)> _displayColMappings = [];

    [ObservableProperty]
    private ObservableCollection<OrderItemModel> _availableOrders = [];

    [ObservableProperty]
    private ObservableCollection<CartItemModel> _cartItems = [];

    [ObservableProperty]
    private decimal _grandTotal;

    [ObservableProperty]
    private bool _isLoading;

    // 🌟 1. ใช้ Full Property เพื่อดักจับ SetProperty และเรียก FilterOrders (แก้ CS0759 ถาวร)
    private string _selectedFilterValue = "ทั้งหมด";
    public string SelectedFilterValue
    {
        get => _selectedFilterValue;
        set
        {
            if (SetProperty(ref _selectedFilterValue, value))
            {
                FilterOrders();
            }
        }
    }

    private string _searchText = string.Empty;
    public string SearchText
    {
        get => _searchText;
        set
        {
            if (SetProperty(ref _searchText, value))
            {
                FilterOrders();
            }
        }
    }

    public OrderViewModel(ISettingsService settingsService)
    {
        _settingsService = settingsService;
        CheckoutAndPrintCommand = new CommunityToolkit.Mvvm.Input.RelayCommand(CheckoutAndPrint);
    }

    public void InitializeOrderData(Dictionary<string, string>? patientData, string? csvPath)
    {
        if (patientData != null)
        {
            PatientRawData = patientData;
            _ = UpdatePatientDisplayAsync(patientData);
        }
        else
        {
            _ = UpdatePatientDisplayAsync([]);
        }

        if (!string.IsNullOrWhiteSpace(csvPath))
        {
            CsvPath = csvPath;
            _ = LoadOrderItemsAsync(csvPath);
        }
    }

    private async Task UpdatePatientDisplayAsync(Dictionary<string, string> patientData)
    {
        try
        {
            var config = await _settingsService.LoadSettingsAsync();

            var formColumns = config?.PatientRegister?.FormDisplayColumns;
            var tempDetails = new List<PatientDetailModel>();

            if (string.IsNullOrWhiteSpace(formColumns))
            {
                tempDetails.Add(new PatientDetailModel { Key = "HN", Value = patientData.GetValueOrDefault("HN", "-") });
                tempDetails.Add(new PatientDetailModel { Key = "Name", Value = patientData.GetValueOrDefault("Name", patientData.GetValueOrDefault("First_Name", "-")) });
            }
            else
            {
                var columnsToDisplay = formColumns.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
                foreach (var colName in columnsToDisplay)
                {
                    var matchedKey = patientData.Keys.FirstOrDefault(k => k.Equals(colName, StringComparison.OrdinalIgnoreCase));
                    if (matchedKey != null && patientData.TryGetValue(matchedKey, out string? value))
                    {
                        tempDetails.Add(new PatientDetailModel { Key = colName, Value = value ?? string.Empty });
                    }
                }
            }

            string deptConfig = config?.PatientRegister?.DepartmentList ?? "OPD, IPD, ER";
            string docConfig = config?.PatientRegister?.DoctorList ?? "-";

            var depts = deptConfig.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries).ToList();
            var docs = docConfig.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries).ToList();

            MainThread.BeginInvokeOnMainThread(() =>
            {
                DisplayPatientDetails.Clear();
                foreach (var item in tempDetails) DisplayPatientDetails.Add(item);

                DepartmentList.Clear();
                foreach (var d in depts) DepartmentList.Add(d);
                if (DepartmentList.Any()) SelectedDepartment = DepartmentList.First();

                DoctorList.Clear();
                foreach (var d in docs) DoctorList.Add(d);
                if (DoctorList.Any()) SelectedDoctor = DoctorList.First();
            });
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Error Update Patient: {ex.Message}");
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
        return [.. result];
    }

    private async Task LoadOrderItemsAsync(string filePath)
    {
        if (!File.Exists(filePath)) return;

        try
        {
            IsLoading = true;
            var config = await _settingsService.LoadSettingsAsync();

            await Task.Run(() =>
            {
                using var stream = new FileStream(filePath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
                using var reader = new StreamReader(stream);

                var headerLine = reader.ReadLine();
                if (headerLine == null) return;

                _csvHeaders = [.. FastCsvSplit(headerLine)];

                string filterColConfig = config?.OrderItem?.DropdownColumn?.Trim() ?? string.Empty;
                string dropdownListSetting = config?.OrderItem?.DropdownList ?? string.Empty;
                string priceColConfig = config?.OrderItem?.CalculateColumn?.Trim() ?? string.Empty;
                string displaySetting = config?.OrderItem?.FormDisplayColumns;

                List<string> columnsToDisplay = string.IsNullOrWhiteSpace(displaySetting)
                    ? _csvHeaders.Take(3).ToList()
                    : [.. displaySetting.Split(',').Select(c => c.Trim()).Where(c => !string.IsNullOrEmpty(c))];

                string codeColName = columnsToDisplay.Count > 0 ? columnsToDisplay[0] : _csvHeaders.FirstOrDefault() ?? "";
                string nameColName = columnsToDisplay.Count > 1 ? columnsToDisplay[1] : _csvHeaders.Skip(1).FirstOrDefault() ?? "";

                int filterIdx = _csvHeaders.FindIndex(c => c.Equals(filterColConfig, StringComparison.OrdinalIgnoreCase));
                int priceIdx = _csvHeaders.FindIndex(c => c.Equals(priceColConfig, StringComparison.OrdinalIgnoreCase));
                int codeIdx = _csvHeaders.FindIndex(c => c.Equals(codeColName, StringComparison.OrdinalIgnoreCase));
                int nameIdx = _csvHeaders.FindIndex(c => c.Equals(nameColName, StringComparison.OrdinalIgnoreCase));

                _displayColMappings.Clear();
                foreach (var colName in columnsToDisplay)
                {
                    int actualIdx = _csvHeaders.FindIndex(c => c.Equals(colName.Trim(), StringComparison.OrdinalIgnoreCase));
                    _displayColMappings.Add((colName, actualIdx));
                }

                var tempOrders = new List<OrderItemModel>(10000);

                while (!reader.EndOfStream)
                {
                    var line = reader.ReadLine();
                    if (string.IsNullOrWhiteSpace(line)) continue;

                    var cols = FastCsvSplit(line);

                    decimal price = 0;
                    if (priceIdx >= 0 && priceIdx < cols.Length)
                    {
                        _ = decimal.TryParse(cols[priceIdx], out price);
                    }

                    string filterVal = (filterIdx >= 0 && filterIdx < cols.Length) ? cols[filterIdx] : string.Empty;
                    string orderCode = (codeIdx >= 0 && codeIdx < cols.Length) ? cols[codeIdx] : "-";
                    string orderName = (nameIdx >= 0 && nameIdx < cols.Length) ? cols[nameIdx] : "ไม่ระบุ";

                    tempOrders.Add(new OrderItemModel
                    {
                        OrderCode = orderCode,
                        OrderName = orderName,
                        Price = price,
                        FilterValue = filterVal,
                        RawCsvLine = line
                    });
                }

                _allOrders = tempOrders;

                MainThread.BeginInvokeOnMainThread(() =>
                {
                    FilterLabel = !string.IsNullOrWhiteSpace(filterColConfig) ? filterColConfig : "ตัวกรอง";

                    var options = new List<string>();

                    // 🌟 1. ตรวจสอบว่ามีการตั้งค่า DropdownList ไว้หรือไม่
                    if (!string.IsNullOrWhiteSpace(dropdownListSetting))
                    {
                        options = dropdownListSetting.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries).ToList();
                    }
                    // 🌟 2. ถ้าไม่มี ให้สกัดข้อมูลที่ไม่ซ้ำกันจาก CSV อัตโนมัติ
                    else if (!string.IsNullOrWhiteSpace(filterColConfig))
                    {
                        options = _allOrders.Select(o => o.FilterValue)
                                            .Where(v => !string.IsNullOrWhiteSpace(v))
                                            .Distinct()
                                            .OrderBy(v => v)
                                            .ToList();
                    }

                    options.Insert(0, "ทั้งหมด");
                    FilterOptions = new ObservableCollection<string>(options);

                    // เซ็ตค่า Backing field โดยตรงเพื่อลดการยิง Event ซ้ำซ้อน
                    _selectedFilterValue = options.First();
                    OnPropertyChanged(nameof(SelectedFilterValue));

                    FilterOrders();
                });
            });

            MainThread.BeginInvokeOnMainThread(() => FilterOrders());
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Load Order CSV Error: {ex.Message}");
        }
        finally
        {
            IsLoading = false;
        }
    }

    // 🌟 2. กู้คืนเมธอด FilterOrders และฟังก์ชันตะกร้าสินค้าที่หายไปกลับมา
    private void FilterOrders()
    {
        IEnumerable<OrderItemModel> query = _allOrders;

        if (!string.IsNullOrEmpty(SelectedFilterValue) && SelectedFilterValue != "ทั้งหมด")
        {
            query = query.Where(o => o.FilterValue.Equals(SelectedFilterValue, StringComparison.OrdinalIgnoreCase));
        }

        if (!string.IsNullOrWhiteSpace(SearchText))
        {
            var keyword = SearchText.Trim();
            query = query.Where(o =>
                o.OrderCode.Contains(keyword, StringComparison.CurrentCultureIgnoreCase) ||
                o.OrderName.Contains(keyword, StringComparison.CurrentCultureIgnoreCase));
        }

        var targetList = query.Take(100).ToList();
        var tempCollection = new ObservableCollection<OrderItemModel>();

        foreach (var item in targetList)
        {
            if (item.DisplayFields.Count == 0)
            {
                var cols = FastCsvSplit(item.RawCsvLine);
                foreach (var mapping in _displayColMappings)
                {
                    string val = (mapping.ActualIndex >= 0 && mapping.ActualIndex < cols.Length) ? cols[mapping.ActualIndex] : string.Empty;
                    item.DisplayFields.Add(new PatientDetailModel { Key = mapping.TargetName, Value = val });
                }
            }
            tempCollection.Add(item);
        }

        AvailableOrders = tempCollection;
    }

    [RelayCommand]
    private async Task GoBackAsync() => await Shell.Current.GoToAsync("..");

    [RelayCommand]
    private void AddToCart(OrderItemModel item)
    {
        var existingItem = CartItems.FirstOrDefault(c => c.OrderCode == item.OrderCode);
        if (existingItem != null)
        {
            existingItem.Quantity++;
        }
        else
        {
            var rawDict = new Dictionary<string, string>();
            var cols = FastCsvSplit(item.RawCsvLine);
            for (int i = 0; i < _csvHeaders.Count; i++)
            {
                rawDict[_csvHeaders[i]] = i < cols.Length ? cols[i] : string.Empty;
            }

            CartItems.Add(new CartItemModel
            {
                OrderCode = item.OrderCode,
                OrderName = item.OrderName,
                UnitPrice = item.Price,
                Quantity = 1,
                RawData = rawDict
            });
        }
        CalculateGrandTotal();
    }

    [RelayCommand]
    private void RemoveFromCart(CartItemModel item)
    {
        if (CartItems.Contains(item))
        {
            CartItems.Remove(item);
            CalculateGrandTotal();
        }
    }

    private void CalculateGrandTotal() => GrandTotal = CartItems.Sum(x => x.TotalPrice);

    private void CheckoutAndPrint()
    {
        if (!CartItems.Any()) return;

        var filteredPatientData = new Dictionary<string, string>();
        foreach (var detail in DisplayPatientDetails)
        {
            filteredPatientData[detail.Key] = detail.Value;
        }

        filteredPatientData["OrderDate"] = OrderDate.ToString("dd/MM/yyyy");
        filteredPatientData["OrderTime"] = $"{SelectedHour}:{SelectedMinute}";
        filteredPatientData["Department"] = SelectedDepartment?.Trim() ?? string.Empty;
        filteredPatientData["Doctor"] = SelectedDoctor?.Trim() ?? string.Empty;
        filteredPatientData["Symptoms"] = Symptoms?.Trim() ?? string.Empty;
        filteredPatientData["Allergy"] = Allergy?.Trim() ?? string.Empty;

        PatientRawData = filteredPatientData;

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

        var cartItemsBuilder = new System.Text.StringBuilder();
        foreach (var item in CartItems)
        {
            cartItemsBuilder.AppendLine($@"
            <tr>
                <td style='text-align: center;'>{item.OrderCode}</td>
                <td>{item.OrderName}</td>
                <td style='text-align: center;'>{item.Quantity}</td>
                <td class='num-col'>{item.UnitPrice:N2}</td>
                <td class='num-col'>{item.TotalPrice:N2}</td>
            </tr>");
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
                .info-table {{ width: 100%; border-collapse: collapse; }}
                .info-table td {{ padding: 3px 0; vertical-align: top; }}
                .label-col {{ width: 120px; white-space: nowrap; font-weight: bold; }}
                .colon-col {{ width: 15px; text-align: center; }}
                .value-col {{ width: auto; }}
                .item-table {{ width: 100%; border-collapse: collapse; margin-top: 15px; }}
                .item-table th, .item-table td {{ border: 1px solid #ccc; padding: 6px; }}
                .item-table th {{ background-color: #f2f2f2; text-align: center; }}
                .num-col {{ text-align: right; }}
                .total-row {{ font-weight: bold; font-size: 16px; text-align: right; }}
                .footer {{ text-align: center; font-size: 12px; margin-top: 15px; }}
            </style>
        </head>
        <body>
            <h2>ใบสั่งรายการ (Order Receipt)</h2>
            <hr/>
            <table class='info-table'>
                {patientInfoBuilder}
                <tr>
                    <td class='label-col'>วันที่ / Date</td>
                    <td class='colon-col'>:</td>
                    <td class='value-col'>{OrderDate:dd/MM/yyyy} &nbsp;&nbsp;&nbsp;&nbsp; เวลา / Time : {SelectedHour}:{SelectedMinute} น.</td>
                </tr>
                <tr>
                    <td class='label-col'>แผนก / Dept</td>
                    <td class='colon-col'>:</td>
                    <td class='value-col'>{SelectedDepartment}</td>
                </tr>
                <tr>
                    <td class='label-col'>แพทย์ / Doctor</td>
                    <td class='colon-col'>:</td>
                    <td class='value-col'>{SelectedDoctor}</td>
                </tr>
                <tr>
                    <td class='label-col'>อาการเบื้องต้น</td>
                    <td class='colon-col'>:</td>
                    <td class='value-col'>{(string.IsNullOrEmpty(Symptoms) ? "-" : Symptoms)}</td>
                </tr>
                <tr>
                    <td class='label-col'>Allergies (แพ้ยา)</td>
                    <td class='colon-col'>:</td>
                    <td class='value-col'>{(string.IsNullOrEmpty(Allergy) ? "-" : Allergy)}</td>
                </tr>
            </table>

            <table class='item-table'>
                <tr>
                    <th>รหัส</th>
                    <th>รายการ</th>
                    <th>จำนวน</th>
                    <th>ราคา/หน่วย</th>
                    <th>รวม</th>
                </tr>
                {cartItemsBuilder}
                <tr>
                    <td colspan='4' class='total-row'>ยอดรวมทั้งสิ้น:</td>
                    <td class='total-row'>{GrandTotal:N2}</td>
                </tr>
            </table>

            <hr/>
            <div class='footer'>*** กรุณานำใบสั่งรายการนี้ไปชำระเงินที่การเงิน ***</div>
        </body>
        </html>";

        WeakReferenceMessenger.Default.Send(new PrintHtmlMessage(printContent));
    }

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
                PickerTitle = "เลือกไฟล์ CSV รายการ Order",
                FileTypes = customFileType
            });

            if (result != null)
            {
                CsvPath = result.FullPath;
                await LoadOrderItemsAsync(result.FullPath);
            }
        }
        catch (Exception ex)
        {
            if (Application.Current?.MainPage != null)
            {
                await Application.Current.MainPage.DisplayAlert("แจ้งเตือน", $"ไม่สามารถเปิดไฟล์ได้: {ex.Message}", "ตกลง");
            }
        }
    }

   
}