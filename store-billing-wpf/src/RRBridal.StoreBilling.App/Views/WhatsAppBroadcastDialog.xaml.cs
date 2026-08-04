using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using System.Windows;
using Microsoft.Win32;
using RRBridal.StoreBilling.App.Services;
using RRBridal.StoreBilling.App.Services.Ui;
using RRBridal.StoreBilling.App.Services.WhatsApp;

namespace RRBridal.StoreBilling.App.Views;

public partial class WhatsAppBroadcastDialog : Window
{
    private readonly AppServices _services;
    private readonly IReadOnlyList<(string Name, string Phone)> _recipients;
    private readonly WhatsAppSettingsSnapshot _settings;
    private string? _mediaPath;

    public WhatsAppBroadcastDialog(
        AppServices services,
        WhatsAppSettingsSnapshot settings,
        IReadOnlyList<(string Name, string Phone)> recipients)
    {
        InitializeComponent();
        _services = services;
        _settings = settings;
        _recipients = recipients;
        OfferDateBox.Text = DateTime.Now.AddDays(7).ToString("dd MMMM yyyy");
        SummaryText.Text =
            $"{recipients.Count} customer(s) with a valid phone will be queued. " +
            $"Template: {(string.IsNullOrWhiteSpace(settings.PromoTemplateName) ? "promo_offer" : settings.PromoTemplateName)} " +
            $"({settings.PromoTemplateLanguage}). Body: name, offer, scope, date + URL. Billing invoice WhatsApp is separate.";

        var header = (settings.PromoHeaderType ?? "none").Trim().ToLowerInvariant();
        MediaPanel.Visibility = header is "image" or "document" ? Visibility.Visible : Visibility.Collapsed;
        StatusText.Text = settings.PromoConfigured || !string.IsNullOrWhiteSpace(settings.PromoTemplateName)
            ? "Uses Central promo_* settings (promo_offer named params). Separate from invoice_send."
            : "Warning: promoTemplateName may be missing on Central — set promo_offer.";
        if (UrlSuffixBox != null)
            UrlSuffixBox.Visibility = settings.PromoHasUrlButton ? Visibility.Visible : Visibility.Collapsed;
        if (UrlLabel != null)
            UrlLabel.Visibility = settings.PromoHasUrlButton ? Visibility.Visible : Visibility.Collapsed;
        UpdateModePanels();
    }

    private void Mode_Changed(object sender, RoutedEventArgs e) => UpdateModePanels();

    private void UpdateModePanels()
    {
        // Checked can fire during InitializeComponent before later-named controls exist.
        if (ModeSession is null || TemplateFieldsPanel is null || SessionFieldsPanel is null)
            return;

        var session = ModeSession.IsChecked == true;
        TemplateFieldsPanel.Visibility = session ? Visibility.Collapsed : Visibility.Visible;
        SessionFieldsPanel.Visibility = session ? Visibility.Visible : Visibility.Collapsed;
        if (ModeHintText != null)
        {
            ModeHintText.Text = session
                ? "Session free-text only delivers if that customer messaged your business WhatsApp within the last 24 hours. For promotions use Marketing template."
                : "Uses approved promo_offer: {{1}} name, {{2}} offer, {{3}} scope, {{4}} date + URL button.";
        }
    }

    private void BrowseMedia_Click(object sender, RoutedEventArgs e)
    {
        var dlg = new OpenFileDialog
        {
            Filter = "Media|*.png;*.jpg;*.jpeg;*.pdf|All files|*.*",
            CheckFileExists = true,
        };
        if (dlg.ShowDialog(this) == true)
        {
            _mediaPath = dlg.FileName;
            MediaPathBox.Text = dlg.FileName;
        }
    }

    private void Cancel_Click(object sender, RoutedEventArgs e)
    {
        DialogResult = false;
        Close();
    }

    private async void Send_Click(object sender, RoutedEventArgs e)
    {
        var mode = ModeSession.IsChecked == true ? "session" : "template";
        string promoText;
        string offerScope = "";
        string offerDate = "";
        string urlSuffix = "";

        if (mode == "session")
        {
            promoText = (SessionTextBox.Text ?? "").Trim();
            if (promoText.Length == 0)
            {
                AppDialog.Show("Enter a free-text message.", "Broadcast", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            var confirm = AppDialog.Show(
                "Session free-text is NOT a marketing broadcast.\n\n" +
                "Meta only delivers these messages if each customer already messaged your business WhatsApp within the last 24 hours.\n" +
                "Cold customers will not receive the message even if the job shows Sent.\n\n" +
                "For offers/promotions, cancel and use Marketing template instead.\n\nContinue with session mode?",
                "Broadcast",
                MessageBoxButton.YesNo,
                MessageBoxImage.Warning);
            if (confirm != MessageBoxResult.Yes)
                return;
        }
        else
        {
            promoText = (OfferTextBox.Text ?? "").Trim();
            offerScope = (OfferScopeBox.Text ?? "").Trim();
            offerDate = (OfferDateBox.Text ?? "").Trim();
            urlSuffix = (UrlSuffixBox.Text ?? "").Trim();
            if (promoText.Length == 0)
            {
                AppDialog.Show("Enter the offer (template {{2}}), e.g. 20.", "Broadcast", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }
            if (offerScope.Length == 0)
            {
                AppDialog.Show("Enter the scope (template {{3}}), e.g. All Products.", "Broadcast", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }
            if (offerDate.Length == 0)
            {
                AppDialog.Show("Enter the valid-until date (template expiry_date).", "Broadcast", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }
            if (_settings.PromoHasUrlButton && urlSuffix.Length == 0)
            {
                AppDialog.Show("Enter the URL button value.", "Broadcast", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }
        }

        var header = (_settings.PromoHeaderType ?? "none").Trim().ToLowerInvariant();
        byte[]? bytes = null;
        string? fileName = null;
        string? mime = null;
        if (mode == "template" && header is "image" or "document")
        {
            if (string.IsNullOrWhiteSpace(_mediaPath) || !File.Exists(_mediaPath))
            {
                AppDialog.Show("Select a header media file for this promo template.", "Broadcast", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }
            bytes = await File.ReadAllBytesAsync(_mediaPath);
            fileName = Path.GetFileName(_mediaPath);
            mime = header == "document" || fileName.EndsWith(".pdf", StringComparison.OrdinalIgnoreCase)
                ? "application/pdf"
                : "image/png";
        }

        SendButton.IsEnabled = false;
        StatusText.Text = "Starting broadcast…";
        try
        {
            _services.CentralAuthSession.ApplyTo(_services.CentralApi);
            var (jobId, err) = await _services.WhatsAppClient.StartBroadcastAsync(
                _services.StoreContext.StoreId,
                mode,
                promoText,
                _recipients,
                bytes,
                fileName,
                mime,
                offerDate,
                urlSuffix,
                offerScope);
            if (jobId == null)
            {
                StatusText.Text = err ?? "Failed to start broadcast.";
                SendButton.IsEnabled = true;
                return;
            }

            StatusText.Text = $"Job {jobId} running…";
            WhatsAppBroadcastJobSnapshot? job = null;
            for (var i = 0; i < 600; i++)
            {
                await Task.Delay(1500);
                var (snap, jobErr) = await _services.WhatsAppClient.GetBroadcastJobAsync(jobId);
                if (snap == null)
                {
                    StatusText.Text = jobErr ?? "Could not load job status.";
                    continue;
                }
                job = snap;
                StatusText.Text = $"{snap.Status}: sent {snap.Sent}, failed {snap.Failed}, skipped {snap.Skipped} / {snap.Total}";
                if (snap.Status is "completed" or "failed")
                    break;
            }

            if (job == null)
            {
                AppDialog.Show("Broadcast started but status could not be confirmed.", "Broadcast", MessageBoxButton.OK, MessageBoxImage.Warning);
            }
            else
            {
                var failDetail = !string.IsNullOrWhiteSpace(job.FirstFailureReason)
                    ? job.FirstFailureReason
                    : job.Error;
                var modeNote = mode == "session"
                    ? "\n\nMode: session free-text. Sent = Meta accepted the API call. Delivery still requires an open 24h chat window — use Marketing template for promotions."
                    : "\n\nMode: marketing template.\n" +
                      "Sent = Meta accepted the message (not the same as phone delivery).\n" +
                      "Open WhatsApp on the customer phone and check chats from business name Trugotech (+91 96003 87958).\n" +
                      "Also check Business / Spam folders.";
                if (!string.IsNullOrWhiteSpace(job.FirstRecipientSummary))
                    modeNote += "\n\n" + job.FirstRecipientSummary;
                if (!string.IsNullOrWhiteSpace(failDetail))
                    modeNote += "\n\nMeta error:\n" + failDetail;
                AppDialog.Show(
                    $"Broadcast {job.Status}.\nSent: {job.Sent}\nFailed: {job.Failed}\nSkipped: {job.Skipped}\nTotal: {job.Total}" +
                    modeNote,
                    "Broadcast",
                    MessageBoxButton.OK,
                    job.Failed > 0 || job.Status == "failed" || mode == "session"
                        ? MessageBoxImage.Warning
                        : MessageBoxImage.Information);
            }

            DialogResult = true;
            Close();
        }
        catch (Exception ex)
        {
            StatusText.Text = ex.Message;
            SendButton.IsEnabled = true;
        }
    }
}
