using System.Windows;

namespace RRBridal.StoreBilling.App.Views;

public partial class CustomerSaveSuccessDialog : Window
{
    public CustomerSaveSuccessDialog(string? centralSyncWarning)
    {
        InitializeComponent();
        if (!string.IsNullOrWhiteSpace(centralSyncWarning))
        {
            SyncWarningBlock.Text = centralSyncWarning;
            SyncWarningBlock.Visibility = Visibility.Visible;
        }
    }

    private void ContinueBilling_OnClick(object sender, RoutedEventArgs e)
    {
        DialogResult = true;
        Close();
    }

    private void CloseButton_OnClick(object sender, RoutedEventArgs e)
    {
        DialogResult = false;
        Close();
    }
}
