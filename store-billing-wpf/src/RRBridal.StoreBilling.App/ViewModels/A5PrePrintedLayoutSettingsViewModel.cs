using CommunityToolkit.Mvvm.ComponentModel;
using RRBridal.StoreBilling.App.Services.Invoicing;

namespace RRBridal.StoreBilling.App.ViewModels;

public partial class A5PrePrintedLayoutSettingsViewModel : ObservableObject
{
    [ObservableProperty] private double _offsetXMm;
    [ObservableProperty] private double _offsetYMm = -8;
    [ObservableProperty] private double _tableOffsetYMm = 4;
    [ObservableProperty] private double _bodyFontPt = 9.5;
    [ObservableProperty] private double _totalFontPt = 9;
    [ObservableProperty] private double _textBaselineNudgeMm;
    [ObservableProperty] private double _billToTopMm = 81;
    [ObservableProperty] private double _billToLeftMm = 34;
    [ObservableProperty] private double _billToWidthMm = 30;
    [ObservableProperty] private double _invNoTopMm = 81;
    [ObservableProperty] private double _invNoLeftMm = 79;
    [ObservableProperty] private double _invNoWidthMm = 32;
    [ObservableProperty] private double _dateTopMm = 81;
    [ObservableProperty] private double _dateLeftMm = 115;
    [ObservableProperty] private double _dateWidthMm = 26;
    [ObservableProperty] private double _metaRow2TopMm = 90;
    [ObservableProperty] private double _contactLeftMm = 40;
    [ObservableProperty] private double _contactWidthMm = 32;
    [ObservableProperty] private double _stitchingTickLeftMm = 97;
    [ObservableProperty] private double _stitchingTickTopMm = 91;
    [ObservableProperty] private double _stitchingTickSizeMm = 5;
    [ObservableProperty] private double _deliveryDateLeftMm = 113;
    [ObservableProperty] private double _deliveryDateTopMm = 91;
    [ObservableProperty] private double _deliveryDateWidthMm = 28;
    [ObservableProperty] private double _tableTopMm = 108;
    [ObservableProperty] private double _lineRowHeightMm = 7.2;
    [ObservableProperty] private int _maxLineRows = 10;
    [ObservableProperty] private double _colDescLeftMm = 25;
    [ObservableProperty] private double _colDescWidthMm = 56;
    [ObservableProperty] private double _colQtyLeftMm = 85;
    [ObservableProperty] private double _colQtyWidthMm = 9;
    [ObservableProperty] private double _colRateLeftMm = 90;
    [ObservableProperty] private double _colRateWidthMm = 15;
    [ObservableProperty] private double _colAlterationLeftMm = 78;
    [ObservableProperty] private double _colAlterationWidthMm = 12;
    [ObservableProperty] private double _colAmountLeftMm = 102;
    [ObservableProperty] private double _colAmountWidthMm = 18;
    [ObservableProperty] private double _totalAmountLeftMm = 102;
    [ObservableProperty] private double _totalAmountTopMm = 190;
    [ObservableProperty] private double _totalAmountWidthMm = 25;
    [ObservableProperty] private double _discountPercentLeftMm = 25;
    [ObservableProperty] private double _discountPercentTopMm = 176;
    [ObservableProperty] private double _discountPercentWidthMm = 56;
    [ObservableProperty] private double _discountAmountLeftMm = 102;
    [ObservableProperty] private double _discountAmountTopMm = 176;
    [ObservableProperty] private double _discountAmountWidthMm = 25;
    [ObservableProperty] private double _alterationAmountLeftMm = 25;
    [ObservableProperty] private double _alterationAmountTopMm = 183;
    [ObservableProperty] private double _alterationAmountWidthMm = 102;
    [ObservableProperty] private double _totalQtyLeftMm = 85;
    [ObservableProperty] private double _totalQtyTopMm = 176;
    [ObservableProperty] private double _totalQtyWidthMm = 9;
    [ObservableProperty] private string _continuedLabel = "Continued";
    [ObservableProperty] private double _continuedLeftMm;
    [ObservableProperty] private double _continuedWidthMm;
    [ObservableProperty] private string _printFontFamily = "Arial";
    [ObservableProperty] private int _billToMaxChars = 15;
    [ObservableProperty] private string _duplicateCopyLabel = "DUPLICATE COPY";
    [ObservableProperty] private double _duplicateCopyTopMm = 72;
    [ObservableProperty] private double _duplicateCopyLeftMm = 98;
    [ObservableProperty] private double _duplicateCopyWidthMm = 43;
    [ObservableProperty] private double _duplicateCopyFontPt = 9;

    public void ApplyFrom(A5PrePrintedLayoutSettings s)
    {
        s.EnsureAlignmentDefaults();
        OffsetXMm = s.OffsetXMm;
        OffsetYMm = s.OffsetYMm;
        TableOffsetYMm = s.TableOffsetYMm;
        BodyFontPt = s.BodyFontPt;
        TotalFontPt = s.TotalFontPt;
        TextBaselineNudgeMm = s.TextBaselineNudgeMm;
        BillToTopMm = s.BillToTopMm;
        BillToLeftMm = s.BillToLeftMm;
        BillToWidthMm = s.BillToWidthMm;
        InvNoTopMm = s.InvNoTopMm;
        InvNoLeftMm = s.InvNoLeftMm;
        InvNoWidthMm = s.InvNoWidthMm;
        DateTopMm = s.DateTopMm;
        DateLeftMm = s.DateLeftMm;
        DateWidthMm = s.DateWidthMm;
        MetaRow2TopMm = s.MetaRow2TopMm;
        ContactLeftMm = s.ContactLeftMm;
        ContactWidthMm = s.ContactWidthMm;
        StitchingTickLeftMm = s.StitchingTickLeftMm;
        StitchingTickTopMm = s.StitchingTickTopMm;
        StitchingTickSizeMm = s.StitchingTickSizeMm;
        DeliveryDateLeftMm = s.DeliveryDateLeftMm;
        DeliveryDateTopMm = s.DeliveryDateTopMm;
        DeliveryDateWidthMm = s.DeliveryDateWidthMm;
        TableTopMm = s.TableTopMm;
        LineRowHeightMm = s.LineRowHeightMm;
        MaxLineRows = s.MaxLineRows;
        ColDescLeftMm = s.ColDescLeftMm;
        ColDescWidthMm = s.ColDescWidthMm;
        ColQtyLeftMm = s.ColQtyLeftMm;
        ColQtyWidthMm = s.ColQtyWidthMm;
        ColRateLeftMm = s.ColRateLeftMm;
        ColRateWidthMm = s.ColRateWidthMm;
        ColAlterationLeftMm = s.ColAlterationLeftMm;
        ColAlterationWidthMm = s.ColAlterationWidthMm;
        ColAmountLeftMm = s.ColAmountLeftMm;
        ColAmountWidthMm = s.ColAmountWidthMm;
        TotalAmountLeftMm = s.TotalAmountLeftMm;
        TotalAmountTopMm = s.TotalAmountTopMm;
        TotalAmountWidthMm = s.TotalAmountWidthMm;
        DiscountPercentLeftMm = s.DiscountPercentLeftMm;
        DiscountPercentTopMm = s.DiscountPercentTopMm;
        DiscountPercentWidthMm = s.DiscountPercentWidthMm;
        DiscountAmountLeftMm = s.DiscountAmountLeftMm;
        DiscountAmountTopMm = s.DiscountAmountTopMm;
        DiscountAmountWidthMm = s.DiscountAmountWidthMm;
        AlterationAmountLeftMm = s.AlterationAmountLeftMm;
        AlterationAmountTopMm = s.AlterationAmountTopMm;
        AlterationAmountWidthMm = s.AlterationAmountWidthMm;
        TotalQtyLeftMm = s.TotalQtyLeftMm;
        TotalQtyTopMm = s.TotalQtyTopMm;
        TotalQtyWidthMm = s.TotalQtyWidthMm;
        ContinuedLabel = s.ContinuedLabel;
        ContinuedLeftMm = s.ContinuedLeftMm;
        ContinuedWidthMm = s.ContinuedWidthMm;
        PrintFontFamily = s.PrintFontFamily;
        BillToMaxChars = s.BillToMaxChars;
        DuplicateCopyLabel = s.DuplicateCopyLabel;
        DuplicateCopyTopMm = s.DuplicateCopyTopMm;
        DuplicateCopyLeftMm = s.DuplicateCopyLeftMm;
        DuplicateCopyWidthMm = s.DuplicateCopyWidthMm;
        DuplicateCopyFontPt = s.DuplicateCopyFontPt;
    }

    public A5PrePrintedLayoutSettings ToSettings()
    {
        var maxRows = MaxLineRows;
        if (maxRows < 1) maxRows = 1;
        if (maxRows > 24) maxRows = 24;
        var billToMax = BillToMaxChars;
        if (billToMax < 1) billToMax = 1;
        if (billToMax > 40) billToMax = 40;

        return new A5PrePrintedLayoutSettings
        {
            OffsetXMm = OffsetXMm,
            OffsetYMm = OffsetYMm,
            TableOffsetYMm = TableOffsetYMm,
            BodyFontPt = BodyFontPt,
            TotalFontPt = TotalFontPt,
            TextBaselineNudgeMm = TextBaselineNudgeMm,
            BillToTopMm = BillToTopMm,
            BillToLeftMm = BillToLeftMm,
            BillToWidthMm = BillToWidthMm,
            InvNoTopMm = InvNoTopMm,
            InvNoLeftMm = InvNoLeftMm,
            InvNoWidthMm = InvNoWidthMm,
            DateTopMm = DateTopMm,
            DateLeftMm = DateLeftMm,
            DateWidthMm = DateWidthMm,
            MetaRow2TopMm = MetaRow2TopMm,
            ContactLeftMm = ContactLeftMm,
            ContactWidthMm = ContactWidthMm,
            StitchingTickLeftMm = StitchingTickLeftMm,
            StitchingTickTopMm = StitchingTickTopMm,
            StitchingTickSizeMm = StitchingTickSizeMm,
            DeliveryDateLeftMm = DeliveryDateLeftMm,
            DeliveryDateTopMm = DeliveryDateTopMm,
            DeliveryDateWidthMm = DeliveryDateWidthMm,
            TableTopMm = TableTopMm,
            LineRowHeightMm = LineRowHeightMm,
            MaxLineRows = maxRows,
            ColDescLeftMm = ColDescLeftMm,
            ColDescWidthMm = ColDescWidthMm,
            ColQtyLeftMm = ColQtyLeftMm,
            ColQtyWidthMm = ColQtyWidthMm,
            ColRateLeftMm = ColRateLeftMm,
            ColRateWidthMm = ColRateWidthMm,
            ColAlterationLeftMm = ColAlterationLeftMm,
            ColAlterationWidthMm = ColAlterationWidthMm,
            ColAmountLeftMm = ColAmountLeftMm,
            ColAmountWidthMm = ColAmountWidthMm,
            TotalAmountLeftMm = TotalAmountLeftMm,
            TotalAmountTopMm = TotalAmountTopMm,
            TotalAmountWidthMm = TotalAmountWidthMm,
            DiscountPercentLeftMm = DiscountPercentLeftMm,
            DiscountPercentTopMm = DiscountPercentTopMm,
            DiscountPercentWidthMm = DiscountPercentWidthMm,
            DiscountAmountLeftMm = DiscountAmountLeftMm,
            DiscountAmountTopMm = DiscountAmountTopMm,
            DiscountAmountWidthMm = DiscountAmountWidthMm,
            AlterationAmountLeftMm = AlterationAmountLeftMm,
            AlterationAmountTopMm = AlterationAmountTopMm,
            AlterationAmountWidthMm = AlterationAmountWidthMm,
            TotalQtyLeftMm = TotalQtyLeftMm,
            TotalQtyTopMm = TotalQtyTopMm,
            TotalQtyWidthMm = TotalQtyWidthMm,
            ContinuedLabel = string.IsNullOrWhiteSpace(ContinuedLabel)
                ? A5PrePrintedText.DefaultContinuedLabel
                : ContinuedLabel.Trim(),
            ContinuedLeftMm = ContinuedLeftMm,
            ContinuedWidthMm = ContinuedWidthMm,
            PrintFontFamily = string.IsNullOrWhiteSpace(PrintFontFamily) ? "Arial" : PrintFontFamily.Trim(),
            BillToMaxChars = billToMax,
            DuplicateCopyLabel = string.IsNullOrWhiteSpace(DuplicateCopyLabel)
                ? "DUPLICATE COPY"
                : DuplicateCopyLabel.Trim(),
            DuplicateCopyTopMm = DuplicateCopyTopMm,
            DuplicateCopyLeftMm = DuplicateCopyLeftMm,
            DuplicateCopyWidthMm = DuplicateCopyWidthMm,
            DuplicateCopyFontPt = DuplicateCopyFontPt > 0 ? DuplicateCopyFontPt : 9,
        };
    }
}
