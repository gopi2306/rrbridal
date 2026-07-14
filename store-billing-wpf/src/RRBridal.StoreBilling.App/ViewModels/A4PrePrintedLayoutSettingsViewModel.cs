using CommunityToolkit.Mvvm.ComponentModel;
using RRBridal.StoreBilling.App.Services.Invoicing;

namespace RRBridal.StoreBilling.App.ViewModels;

public partial class A4PrePrintedLayoutSettingsViewModel : ObservableObject
{
    [ObservableProperty] private double _offsetXMm;
    [ObservableProperty] private double _offsetYMm;
    [ObservableProperty] private double _tableOffsetYMm;
    [ObservableProperty] private double _bodyFontPt = 9;
    [ObservableProperty] private double _totalFontPt = 9.5;
    [ObservableProperty] private double _textBaselineNudgeMm;
    [ObservableProperty] private double _billToTopMm = 57;
    [ObservableProperty] private double _billToLeftMm = 20;
    [ObservableProperty] private double _billToWidthMm = 106;
    [ObservableProperty] private double _invNoTopMm = 57;
    [ObservableProperty] private double _invNoLeftMm = 140;
    [ObservableProperty] private double _invNoWidthMm = 56;
    [ObservableProperty] private double _dateTopMm = 65;
    [ObservableProperty] private double _dateLeftMm = 140;
    [ObservableProperty] private double _dateWidthMm = 56;
    [ObservableProperty] private double _orderNoTopMm = 73;
    [ObservableProperty] private double _orderNoLeftMm = 140;
    [ObservableProperty] private double _orderNoWidthMm = 56;
    [ObservableProperty] private double _metaRowHeightMm = 8;
    [ObservableProperty] private double _tableTopMm = 90;
    [ObservableProperty] private double _lineRowHeightMm = 7;
    [ObservableProperty] private int _maxLineRows = 13;
    [ObservableProperty] private double _colSrLeftMm = 20;
    [ObservableProperty] private double _colSrWidthMm = 8;
    [ObservableProperty] private double _colParticularLeftMm = 28;
    [ObservableProperty] private double _colParticularWidthMm = 42;
    [ObservableProperty] private double _colHsnLeftMm = 70;
    [ObservableProperty] private double _colHsnWidthMm = 14;
    [ObservableProperty] private double _colPcsLeftMm = 84;
    [ObservableProperty] private double _colPcsWidthMm = 10;
    [ObservableProperty] private double _colMeterLeftMm = 94;
    [ObservableProperty] private double _colMeterWidthMm = 12;
    [ObservableProperty] private double _colBasicRateLeftMm = 106;
    [ObservableProperty] private double _colBasicRateWidthMm = 16;
    [ObservableProperty] private double _colCdPercentLeftMm = 122;
    [ObservableProperty] private double _colCdPercentWidthMm = 10;
    [ObservableProperty] private double _colLessPercentLeftMm = 132;
    [ObservableProperty] private double _colLessPercentWidthMm = 10;
    [ObservableProperty] private double _colTdPercentLeftMm = 142;
    [ObservableProperty] private double _colTdPercentWidthMm = 10;
    [ObservableProperty] private double _colNetRateLeftMm = 152;
    [ObservableProperty] private double _colNetRateWidthMm = 16;
    [ObservableProperty] private double _colAmountLeftMm = 168;
    [ObservableProperty] private double _colAmountWidthMm = 22;
    [ObservableProperty] private double _totalAmountLeftMm = 168;
    [ObservableProperty] private double _totalAmountTopMm = 248;
    [ObservableProperty] private double _totalAmountWidthMm = 22;
    [ObservableProperty] private string _continuedLabel = "Continued";
    [ObservableProperty] private double _continuedLeftMm;
    [ObservableProperty] private double _continuedWidthMm;
    [ObservableProperty] private string _printFontFamily = "Arial";
    [ObservableProperty] private int _billToMaxChars = 40;
    [ObservableProperty] private string _duplicateCopyLabel = "DUPLICATE COPY";
    [ObservableProperty] private double _duplicateCopyTopMm = 52;
    [ObservableProperty] private double _duplicateCopyLeftMm = 140;
    [ObservableProperty] private double _duplicateCopyWidthMm = 50;
    [ObservableProperty] private double _duplicateCopyFontPt = 10;

    public void ApplyFrom(A4PrePrintedLayoutSettings s)
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
        OrderNoTopMm = s.OrderNoTopMm;
        OrderNoLeftMm = s.OrderNoLeftMm;
        OrderNoWidthMm = s.OrderNoWidthMm;
        MetaRowHeightMm = s.MetaRowHeightMm;
        TableTopMm = s.TableTopMm;
        LineRowHeightMm = s.LineRowHeightMm;
        MaxLineRows = s.MaxLineRows;
        ColSrLeftMm = s.ColSrLeftMm;
        ColSrWidthMm = s.ColSrWidthMm;
        ColParticularLeftMm = s.ColParticularLeftMm;
        ColParticularWidthMm = s.ColParticularWidthMm;
        ColHsnLeftMm = s.ColHsnLeftMm;
        ColHsnWidthMm = s.ColHsnWidthMm;
        ColPcsLeftMm = s.ColPcsLeftMm;
        ColPcsWidthMm = s.ColPcsWidthMm;
        ColMeterLeftMm = s.ColMeterLeftMm;
        ColMeterWidthMm = s.ColMeterWidthMm;
        ColBasicRateLeftMm = s.ColBasicRateLeftMm;
        ColBasicRateWidthMm = s.ColBasicRateWidthMm;
        ColCdPercentLeftMm = s.ColCdPercentLeftMm;
        ColCdPercentWidthMm = s.ColCdPercentWidthMm;
        ColLessPercentLeftMm = s.ColLessPercentLeftMm;
        ColLessPercentWidthMm = s.ColLessPercentWidthMm;
        ColTdPercentLeftMm = s.ColTdPercentLeftMm;
        ColTdPercentWidthMm = s.ColTdPercentWidthMm;
        ColNetRateLeftMm = s.ColNetRateLeftMm;
        ColNetRateWidthMm = s.ColNetRateWidthMm;
        ColAmountLeftMm = s.ColAmountLeftMm;
        ColAmountWidthMm = s.ColAmountWidthMm;
        TotalAmountLeftMm = s.TotalAmountLeftMm;
        TotalAmountTopMm = s.TotalAmountTopMm;
        TotalAmountWidthMm = s.TotalAmountWidthMm;
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

    public A4PrePrintedLayoutSettings ToSettings()
    {
        var maxRows = MaxLineRows;
        if (maxRows < 1) maxRows = 1;
        if (maxRows > 30) maxRows = 30;
        var billToMax = BillToMaxChars;
        if (billToMax < 1) billToMax = 1;
        if (billToMax > 120) billToMax = 120;

        return new A4PrePrintedLayoutSettings
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
            OrderNoTopMm = OrderNoTopMm,
            OrderNoLeftMm = OrderNoLeftMm,
            OrderNoWidthMm = OrderNoWidthMm,
            MetaRowHeightMm = MetaRowHeightMm > 0 ? MetaRowHeightMm : 8,
            TableTopMm = TableTopMm,
            LineRowHeightMm = LineRowHeightMm,
            MaxLineRows = maxRows,
            ColSrLeftMm = ColSrLeftMm,
            ColSrWidthMm = ColSrWidthMm,
            ColParticularLeftMm = ColParticularLeftMm,
            ColParticularWidthMm = ColParticularWidthMm,
            ColHsnLeftMm = ColHsnLeftMm,
            ColHsnWidthMm = ColHsnWidthMm,
            ColPcsLeftMm = ColPcsLeftMm,
            ColPcsWidthMm = ColPcsWidthMm,
            ColMeterLeftMm = ColMeterLeftMm,
            ColMeterWidthMm = ColMeterWidthMm,
            ColBasicRateLeftMm = ColBasicRateLeftMm,
            ColBasicRateWidthMm = ColBasicRateWidthMm,
            ColCdPercentLeftMm = ColCdPercentLeftMm,
            ColCdPercentWidthMm = ColCdPercentWidthMm,
            ColLessPercentLeftMm = ColLessPercentLeftMm,
            ColLessPercentWidthMm = ColLessPercentWidthMm,
            ColTdPercentLeftMm = ColTdPercentLeftMm,
            ColTdPercentWidthMm = ColTdPercentWidthMm,
            ColNetRateLeftMm = ColNetRateLeftMm,
            ColNetRateWidthMm = ColNetRateWidthMm,
            ColAmountLeftMm = ColAmountLeftMm,
            ColAmountWidthMm = ColAmountWidthMm,
            TotalAmountLeftMm = TotalAmountLeftMm,
            TotalAmountTopMm = TotalAmountTopMm,
            TotalAmountWidthMm = TotalAmountWidthMm,
            ContinuedLabel = string.IsNullOrWhiteSpace(ContinuedLabel)
                ? A4PrePrintedText.DefaultContinuedLabel
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
            DuplicateCopyFontPt = DuplicateCopyFontPt > 0 ? DuplicateCopyFontPt : 10,
        };
    }
}
