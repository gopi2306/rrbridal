namespace RRBridal.StoreBilling.App.Services.Auth;

public sealed class StoreUserRecord
{
    public string CentralId { get; set; } = "";
    public string Name { get; set; } = "";
    public string Email { get; set; } = "";
    public string Role { get; set; } = "";
    public string StoreId { get; set; } = "";

    public override string ToString() => Name;
}
