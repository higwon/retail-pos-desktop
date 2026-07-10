using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using RetailPOS.Application.Receipts;

namespace RetailPOS.Desktop.ViewModels;

public sealed partial class ReceiptViewModel : ObservableObject
{
    private readonly IReceiptPrinter _receiptPrinter;
    private readonly ReceiptPreviewState _receiptPreviewState;
    private ReceiptPreview? _receipt;

    public ReceiptViewModel(IReceiptPrinter receiptPrinter, ReceiptPreviewState receiptPreviewState)
    {
        _receiptPrinter = receiptPrinter;
        _receiptPreviewState = receiptPreviewState;
        PrintCommand = new AsyncRelayCommand(PrintAsync, () => HasReceipt && !IsBusy);
        LoadCurrentReceipt();
    }

    public ObservableCollection<ReceiptLineViewModel> Lines { get; } = [];
    public ObservableCollection<ReceiptPaymentViewModel> Payments { get; } = [];
    public IAsyncRelayCommand PrintCommand { get; }

    [ObservableProperty]
    private bool _isBusy;

    [ObservableProperty]
    private string? _statusMessage;

    [ObservableProperty]
    private string? _errorMessage;

    public bool HasReceipt => _receipt is not null;
    public string StoreName => _receipt?.StoreName ?? "Retail Store";
    public string StoreAddress => _receipt?.StoreAddress ?? "Local POS Terminal";
    public string OrderNumber => _receipt?.OrderNumber ?? "-";
    public string CashierName => _receipt?.CashierName ?? "-";
    public string RegisterName => _receipt?.RegisterName ?? "-";
    public string IssuedAtText => _receipt?.IssuedAtUtc.ToLocalTime().ToString("yyyy-MM-dd HH:mm") ?? "-";
    public string BusinessDateText => _receipt?.BusinessDate.ToString("yyyy-MM-dd") ?? "-";
    public string SubtotalAmount => $"{_receipt?.SubtotalAmount ?? 0m:N0} KRW";
    public string DiscountAmount => $"-{_receipt?.DiscountAmount ?? 0m:N0} KRW";
    public string TotalAmount => $"{_receipt?.TotalAmount ?? 0m:N0} KRW";
    public string PlainText => _receipt?.PlainText ?? "No receipt is available yet.";

    private void LoadCurrentReceipt()
    {
        _receipt = _receiptPreviewState.GetCurrent();
        Lines.Clear();
        Payments.Clear();

        if (_receipt is not null)
        {
            foreach (var line in _receipt.Lines.Select(line => new ReceiptLineViewModel(line)))
            {
                Lines.Add(line);
            }

            foreach (var payment in _receipt.Payments.Select(payment => new ReceiptPaymentViewModel(payment)))
            {
                Payments.Add(payment);
            }

            StatusMessage = "Receipt ready.";
        }
        else
        {
            StatusMessage = "No receipt is available yet.";
        }

        NotifyReceiptChanged();
    }

    private async Task PrintAsync()
    {
        if (_receipt is null)
        {
            return;
        }

        IsBusy = true;
        ErrorMessage = null;

        try
        {
            var result = await _receiptPrinter.PrintAsync(_receipt);
            if (result.Succeeded)
            {
                StatusMessage = result.Message;
            }
            else
            {
                ErrorMessage = result.Message;
            }
        }
        catch (Exception)
        {
            ErrorMessage = "Receipt could not be printed. The order is already completed; try again.";
        }
        finally
        {
            IsBusy = false;
            PrintCommand.NotifyCanExecuteChanged();
        }
    }

    partial void OnIsBusyChanged(bool value)
    {
        PrintCommand.NotifyCanExecuteChanged();
    }

    private void NotifyReceiptChanged()
    {
        OnPropertyChanged(nameof(HasReceipt));
        OnPropertyChanged(nameof(StoreName));
        OnPropertyChanged(nameof(StoreAddress));
        OnPropertyChanged(nameof(OrderNumber));
        OnPropertyChanged(nameof(CashierName));
        OnPropertyChanged(nameof(RegisterName));
        OnPropertyChanged(nameof(IssuedAtText));
        OnPropertyChanged(nameof(BusinessDateText));
        OnPropertyChanged(nameof(SubtotalAmount));
        OnPropertyChanged(nameof(DiscountAmount));
        OnPropertyChanged(nameof(TotalAmount));
        OnPropertyChanged(nameof(PlainText));
        PrintCommand.NotifyCanExecuteChanged();
    }
}

public sealed class ReceiptLineViewModel(ReceiptPreviewLine line)
{
    public string ProductName { get; } = line.ProductName;
    public string QuantityText => $"x {line.Quantity:N0}";
    public string UnitPriceText => $"{line.UnitPrice:N0} KRW";
    public string DiscountAmountText => line.DiscountAmount > 0 ? $"-{line.DiscountAmount:N0} KRW" : "-";
    public string TotalAmountText => $"{line.TotalAmount:N0} KRW";
}

public sealed class ReceiptPaymentViewModel(ReceiptPreviewPayment payment)
{
    public string MethodText => payment.Method.ToString();
    public string AmountText => $"{payment.Amount:N0} KRW";
    public string ApprovalCodeText => payment.ApprovalCode ?? "-";
}
