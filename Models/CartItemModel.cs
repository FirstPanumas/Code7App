using CommunityToolkit.Mvvm.ComponentModel;

namespace Code7App.Models;

public partial class CartItemModel : ObservableObject
{
    public string OrderCode { get; set; } = string.Empty;
    public string OrderName { get; set; } = string.Empty;
    public decimal UnitPrice { get; set; }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(TotalPrice))]
    private int _quantity;

    public decimal TotalPrice => UnitPrice * Quantity;

    // เก็บ RawData เพื่อนำไปใช้งานตอน Print หรือ Save
    public Dictionary<string, string> RawData { get; set; } = [];
}