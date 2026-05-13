namespace RRBridal.StoreBilling.App.Services.Auth;

public sealed class UserSession
{
    public required StoreUserRecord LoggedInUser { get; init; }
    public StoreUserRecord? SelectedBillingUser { get; set; }
}
