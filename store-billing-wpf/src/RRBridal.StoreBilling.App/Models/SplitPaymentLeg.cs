using CommunityToolkit.Mvvm.ComponentModel;
using RRBridal.StoreBilling.App.Services.Payments;

namespace RRBridal.StoreBilling.App.Models;

public partial class SplitPaymentLeg : ObservableObject
{
    [ObservableProperty] private PaymentProviderKind _method = PaymentProviderKind.Cash;

    [ObservableProperty] private decimal _amount;

    [ObservableProperty] private string _reference = "";

    [ObservableProperty] private string _status = "Pending";
}
