using Code7App.ViewModels;
using CommunityToolkit.Mvvm.Messaging;
using System.ComponentModel;
using System.Diagnostics; // สำคัญ: ใช้สำหรับ Process.Start()
using System.Globalization;

namespace Code7App.Views;

public partial class MainPage : ContentPage
{
    private readonly MainViewModel _viewModel;
    private int _currentColumnCount = 0;

    public MainPage(MainViewModel viewModel)
    {
        InitializeComponent();
        _viewModel = viewModel;
        BindingContext = _viewModel;

        // รอรับคำสั่งสร้าง PDF จาก ViewModel
        WeakReferenceMessenger.Default.Register<PrintHtmlMessage>(this, (r, m) =>
        {
            MainThread.BeginInvokeOnMainThread(async () =>
            {
                try
                {
                    // 1. นำข้อมูล HTML ใส่เข้าไปใน WebView
                    PrintHelperWebView.Source = new HtmlWebViewSource { Html = m.HtmlContent };

                    // 2. หน่วงเวลาให้ WebView2 สร้าง DOM และจัดรูปแบบ CSS ให้เสร็จสมบูรณ์
                    await Task.Delay(1000);

#if WINDOWS
                    if (PrintHelperWebView.Handler?.PlatformView is Microsoft.UI.Xaml.Controls.WebView2 webView2)
                    {
                        // บังคับ Initialize ให้แน่ใจว่า CoreWebView2 พร้อมทำงาน
                        await webView2.EnsureCoreWebView2Async();

                        // กำหนดชื่อและพาธสำหรับเก็บไฟล์ PDF ชั่วคราวใน Cache
                        string fileName = $"Appointment_{DateTime.Now:yyyyMMdd_HHmmss}.pdf";
                        string filePath = Path.Combine(FileSystem.CacheDirectory, fileName);

                        // สร้าง PrintSettings สำหรับกระดาษ A5 แนวนอน
                        var printSettings = webView2.CoreWebView2.Environment.CreatePrintSettings();
                        
                        // ตั้งค่าเป็นแนวนอน
                        printSettings.Orientation = Microsoft.Web.WebView2.Core.CoreWebView2PrintOrientation.Landscape;
                        
                        // แก้ไข: ใช้ PageWidth และ PageHeight (หน่วยเป็นนิ้ว)
                        printSettings.PageWidth = 5.827;
                        printSettings.PageHeight = 8.268;
                        
                        // อนุญาตให้พิมพ์สีพื้นหลัง CSS (ถ้ามี)
                        printSettings.ShouldPrintBackgrounds = true;

                        // สั่งแปลง HTML เป็น PDF โดยใช้การตั้งค่า printSettings
                        bool isSuccess = await webView2.CoreWebView2.PrintToPdfAsync(filePath, printSettings);

                        if (isSuccess)
                        {
                            // ส่งไฟล์ PDF ให้ Chrome ทำการเปิด
                            OpenPdfWithChrome(filePath);
                        }
                        else
                        {
                            await DisplayAlert("Error", "เกิดข้อผิดพลาดในการสร้างไฟล์ PDF", "OK");
                        }
                    }
#endif
                }
                catch (Exception ex)
                {
                    await DisplayAlert("Print System Error", ex.Message, "OK");
                }
            });
        });
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();
        _viewModel.PropertyChanged += OnViewModelPropertyChanged;

        // ต้องมีบรรทัดนี้เพื่อสั่ง ViewModel ให้ทำงาน
        await _viewModel.InitializeAsync();
    }

    protected override void OnDisappearing()
    {
        base.OnDisappearing();
        // Unsubscribe ป้องกัน Memory Leak
        _viewModel.PropertyChanged -= OnViewModelPropertyChanged;
    }

    private void OnViewModelPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(MainViewModel.CsvColumns))
        {
            Dispatcher.Dispatch(() => GenerateDynamicGrid());
        }
    }

    private void GenerateDynamicGrid()
    {
        var columns = _viewModel.CsvColumns;
        if (columns == null || columns.Count == 0) return;

        if (_currentColumnCount == columns.Count) return;
        _currentColumnCount = columns.Count;

        var config = _viewModel.CurrentConfig.PatientRegister;
        var stringToBoolConv = new StringToBoolConverter();

        var defaultPickerItems = new List<string> { "OPD", "IPD", "ER", "General", "VIP" };

        CsvHeaderGrid.ColumnDefinitions.Clear();
        CsvHeaderGrid.Children.Clear();

        for (int i = 0; i < columns.Count; i++)
        {
            CsvHeaderGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(150) });

            var headerLabel = new Label
            {
                Text = columns[i],
                FontAttributes = FontAttributes.Bold,
                LineBreakMode = LineBreakMode.TailTruncation
            };
            CsvHeaderGrid.Add(headerLabel, i, 0);
        }

        CsvDataGrid.ItemTemplate = new DataTemplate(() =>
        {
            var rowGrid = new Grid { Padding = new Thickness(10) };

            var visualStateGroupList = new VisualStateGroupList();
            var commonStatesGroup = new VisualStateGroup { Name = "CommonStates" };

            var normalState = new VisualState { Name = "Normal" };
            normalState.Setters.Add(new Setter { Property = VisualElement.BackgroundColorProperty, Value = Colors.Transparent });

            var pointerOverState = new VisualState { Name = "PointerOver" };
            pointerOverState.Setters.Add(new Setter { Property = VisualElement.BackgroundColorProperty, Value = Color.FromArgb("#F2F2F2") });

            var selectedState = new VisualState { Name = "Selected" };
            selectedState.Setters.Add(new Setter { Property = VisualElement.BackgroundColorProperty, Value = Color.FromArgb("#E3F2FD") });

            commonStatesGroup.States.Add(normalState);
            commonStatesGroup.States.Add(pointerOverState);
            commonStatesGroup.States.Add(selectedState);
            visualStateGroupList.Add(commonStatesGroup);
            VisualStateManager.SetVisualStateGroups(rowGrid, visualStateGroupList);

            for (int i = 0; i < columns.Count; i++)
            {
                rowGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(150) });
                string colName = columns[i];
                View cellView;

                if (!string.IsNullOrWhiteSpace(config.FlagColumn) && colName.Equals(config.FlagColumn, StringComparison.OrdinalIgnoreCase))
                {
                    var cb = new CheckBox { HorizontalOptions = LayoutOptions.Start, VerticalOptions = LayoutOptions.Center };
                    cb.SetBinding(CheckBox.IsCheckedProperty, new Binding($"[{colName}]", BindingMode.TwoWay, stringToBoolConv));
                    cellView = cb;
                }
                //else if (!string.IsNullOrWhiteSpace(config.DropdownColumn) && colName.Equals(config.DropdownColumn, StringComparison.OrdinalIgnoreCase))
                //{
                //    var picker = new Picker { ItemsSource = defaultPickerItems, HorizontalOptions = LayoutOptions.Fill, VerticalOptions = LayoutOptions.Center };
                //    picker.SetBinding(Picker.SelectedItemProperty, $"[{colName}]");
                //    cellView = picker;
                //}
                else
                {
                    var label = new Label { LineBreakMode = LineBreakMode.TailTruncation, VerticalOptions = LayoutOptions.Center };
                    label.SetBinding(Label.TextProperty, $"[{colName}]");

                    if (!string.IsNullOrWhiteSpace(config.CalculateColumn) && colName.Equals(config.CalculateColumn, StringComparison.OrdinalIgnoreCase))
                    {
                        label.TextColor = Color.FromArgb("#17859F");
                        label.FontAttributes = FontAttributes.Bold;
                        label.HorizontalTextAlignment = TextAlignment.End;
                        label.Margin = new Thickness(0, 0, 10, 0);
                    }

                    cellView = label;
                }

                rowGrid.Add(cellView, i, 0);
            }
            return rowGrid;
        });
    }

    private async void OnSettingsClicked(object sender, EventArgs e)
    {
        await Shell.Current.GoToAsync(nameof(SettingsPage));
    }

    public class StringToBoolConverter : IValueConverter
    {
        public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
        {
            if (value is string str)
            {
                return str.Equals("true", StringComparison.OrdinalIgnoreCase) ||
                       str.Equals("1") ||
                       str.Equals("y", StringComparison.OrdinalIgnoreCase) ||
                       str.Equals("yes", StringComparison.OrdinalIgnoreCase);
            }
            return false;
        }

        public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        {
            if (value is bool b)
            {
                return b ? "true" : "false";
            }
            return "false";
        }
    }

    // เพิ่มเมธอดนี้สำหรับจัดการ Process การเปิดไฟล์ผ่าน Chrome พร้อม Fallback
    private void OpenPdfWithChrome(string filePath)
    {
        try
        {
            Process.Start(new ProcessStartInfo
            {
                FileName = "chrome",
                Arguments = $"\"{filePath}\"",
                UseShellExecute = true
            });
        }
        catch
        {
            // Fallback: หากเครื่องไม่มี Chrome จะเปิดด้วยแอปอ่าน PDF ค่าเริ่มต้นของ Windows (เช่น Edge)
            Process.Start(new ProcessStartInfo
            {
                FileName = filePath,
                UseShellExecute = true
            });
        }
    }
}