namespace RRBridal.StoreBilling.App.Services.Auth;

public sealed class StoreUserRecord
{
    public string CentralId { get; set; } = "";
    public string Name { get; set; } = "";
    public string Email { get; set; } = "";
    public string Role { get; set; } = "";
    public string StoreId { get; set; } = "";

    /// <summary>Max combined manual discount % (item % + cash ₹). Default 100 when unset.</summary>
    public decimal MaxDiscountPercent { get; set; } = 100m;

    public override string ToString() => Name;
}
