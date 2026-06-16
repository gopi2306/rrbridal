using System.Threading.Tasks;
using RRBridal.StoreBilling.App.Services;

namespace RRBridal.StoreBilling.App.ViewModels;

public sealed class DuplicatePrintViewModel
{
    public DuplicateBillViewModel Bill { get; }

    public DuplicateCreditNoteViewModel CreditNote { get; }

    public DuplicatePrintViewModel(AppServices services)
    {
        Bill = new DuplicateBillViewModel(services);
        CreditNote = new DuplicateCreditNoteViewModel(services);
    }

    public Task OnPageOpenedAsync() => Bill.SearchCommand.ExecuteAsync(null);
}
