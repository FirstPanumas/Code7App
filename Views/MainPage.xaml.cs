using Code7App.ViewModels;
using CommunityToolkit.Mvvm.Messaging;
using Code7App.Messages;
using System.ComponentModel;
using System.Diagnostics;
using System.Globalization;
using Microsoft.Maui.Graphics;

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

        WeakReferenceMessenger.Default.Register<PrintHtmlMessage>(this, (r, m) =>
        {
            MainThread.BeginInvokeOnMainThread(async () =>
            {
                try
                {
                    // 🌟 1. สร้าง TaskCompletionSource เพื่อดักรอ Event Navigated
                    var tcs = new TaskCompletionSource<bool>();

                    void OnNavigated(object? sender, WebNavigatedEventArgs e)
                    {
                        PrintHelperWebView.Navigated -= OnNavigated;
                        tcs.TrySetResult(true);
                    }

                    PrintHelperWebView.Navigated += OnNavigated;

                    // 2. นำข้อมูล HTML ใส่เข้าไปใน WebView
                    PrintHelperWebView.Source = new HtmlWebViewSource { Html = m.HtmlContent };

                    // 🌟 3. รอจนกว่า WebView จะโหลด HTML เสร็จ (หรือ Timeout 5 วินาทีป้องกันแอปค้าง)
                    await Task.WhenAny(tcs.Task, Task.Delay(5000));

                    // 🌟 4. หน่วงเวลาเพิ่มอีกเล็กน้อย เพื่อให้ WebView2 เรนเดอร์ UI และ CSS ลง DOM จนสมบูรณ์
                    await Task.Delay(500);

#if WINDOWS
                    if (PrintHelperWebView.Handler?.PlatformView is Microsoft.UI.Xaml.Controls.WebView2 webView2)
                    {
                        await webView2.EnsureCoreWebView2Async();

                        string fileName = $"Appointment_{DateTime.Now:yyyyMMdd_HHmmss}.pdf";
                        string filePath = Path.Combine(FileSystem.CacheDirectory, fileName);

                        var printSettings = webView2.CoreWebView2.Environment.CreatePrintSettings();
                        
                        printSettings.Orientation = Microsoft.Web.WebView2.Core.CoreWebView2PrintOrientation.Landscape;
                        printSettings.PageWidth = 8.268;
                        printSettings.PageHeight = 5.827;
                        printSettings.ShouldPrintBackgrounds = true;

                        bool isSuccess = await webView2.CoreWebView2.PrintToPdfAsync(filePath, printSettings);

                        if (isSuccess)
                        {
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

        // Unsubscribe ก่อนเสมอ
        _viewModel.PropertyChanged -= OnViewModelPropertyChanged;
        _viewModel.PropertyChanged += OnViewModelPropertyChanged;

        // โหลดข้อมูลเริ่มต้น (เช่น CSV)
        await _viewModel.InitializeAsync();

        // ?? บังคับดึงการตั้งค่า Dropdown ใหม่ทุกครั้งที่เปิดหน้านี้ (ใช้ _viewModel ตรงๆ ได้เลย)
        await _viewModel.ReloadSettingsAsync();
    }

    protected override void OnDisappearing()
    {
        base.OnDisappearing();
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

        CsvHeaderGrid.ColumnDefinitions.Clear();
        CsvHeaderGrid.Children.Clear();

        for (int i = 0; i < columns.Count; i++)
        {
            CsvHeaderGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(150) });

            var headerLabel = new Label
            {
                Text = columns[i],
                FontAttributes = FontAttributes.Bold,
                LineBreakMode = LineBreakMode.TailTruncation,
                TextColor = Colors.Black
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
                else
                {
                    var label = new Label
                    {
                        LineBreakMode = LineBreakMode.TailTruncation,
                        VerticalOptions = LayoutOptions.Center,
                        TextColor = Colors.Black
                    };
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
            Process.Start(new ProcessStartInfo
            {
                FileName = filePath,
                UseShellExecute = true
            });
        }
    }

    private async void OnSettingsClicked(object sender, EventArgs e)
    {
        await Shell.Current.GoToAsync(nameof(SettingsPage));
    }
}