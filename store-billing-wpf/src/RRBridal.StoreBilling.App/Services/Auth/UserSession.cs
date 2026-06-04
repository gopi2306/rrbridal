namespace RRBridal.StoreBilling.App.Services.Auth;

public sealed class UserSession
{
    public required StoreUserRecord LoggedInUser { get; set; }
    public StoreUserRecord? SelectedBillingUser { get; set; }
}
