using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using RetailPOS.Application.Checkout;
using RetailPOS.Application.Receipts;
using RetailPOS.Desktop.Workflow;

namespace RetailPOS.Desktop.ViewModels;

public sealed partial class ReceiptHistoryViewModel : ObservableObject, IDisposable
{
    private const int PageSize = 50;

    private readonly IReceiptHistoryQuery _receiptHistoryQuery;
    private readonly IReceiptPrinter _receiptPrinter;
    private readonly ICheckoutClock _clock;
    private readonly ReceiptPreviewState _receiptPreviewState;
    private readonly CashierWorkflowNavigator _workflowNavigator;
    private CancellationTokenSource? _loadCancellation;
    private CancellationTokenSource? _detailCancellation;
    private ReceiptPreview? _detail;
    private Guid? _lastCompletionOrderId;
    private int _loadVersion;
    private int _detailVersion;
    private bool _suppressSelectionLoad;
    private bool _disposed;

    public ReceiptHistoryViewModel(
        IReceiptHistoryQuery receiptHistoryQuery,
        IReceiptPrinter receiptPrinter,
        ICheckoutClock clock,
        ReceiptPreviewState receiptPreviewState,
        CashierWorkflowNavigator workflowNavigator)
    {
        _receiptHistoryQuery = receiptHistoryQuery;
        _receiptPrinter = receiptPrinter;
        _clock = clock;
        _receiptPreviewState = receiptPreviewState;
        _workflowNavigator = workflowNavigator;
        SelectedDate = _clock.UtcNow.ToLocalTime().Date;
        SearchCommand = new AsyncRelayCommand(
            cancellationToken => LoadPageAsync(reset: true, cancellationToken),
            CanLoad);
        RefreshCommand = new AsyncRelayCommand(
            cancellationToken => LoadPageAsync(reset: true, cancellationToken),
            CanLoad);
        LoadMoreCommand = new AsyncRelayCommand(
            cancellationToken => LoadPageAsync(reset: false, cancellationToken),
            CanLoadMore);
        PrintCommand = new AsyncRelayCommand(PrintAsync, CanPrint);
        BackCommand = new RelayCommand(GoBack);
    }

    public ObservableCollection<ReceiptHistoryItemViewModel> Receipts { get; } = [];
    public ObservableCollection<ReceiptLineViewModel> Lines { get; } = [];
    public ObservableCollection<ReceiptPaymentViewModel> Payments { get; } = [];
    public IAsyncRelayCommand SearchCommand { get; }
    public IAsyncRelayCommand RefreshCommand { get; }
    public IAsyncRelayCommand LoadMoreCommand { get; }
    public IAsyncRelayCommand PrintCommand { get; }
    public IRelayCommand BackCommand { get; }

    [ObservableProperty]
    private DateTime? _selectedDate;

    [ObservableProperty]
    private string? _searchText;

    [ObservableProperty]
    private ReceiptHistoryItemViewModel? _selectedReceipt;

    [ObservableProperty]
    private bool _isLoading;

    [ObservableProperty]
    private bool _isDetailLoading;

    [ObservableProperty]
    private bool _isPrinting;

    [ObservableProperty]
    private bool _hasMore;

    [ObservableProperty]
    private string? _statusMessage;

    [ObservableProperty]
    private string? _errorMessage;

    [ObservableProperty]
    private bool _isSuccessStatus;

    public bool HasReceipts => Receipts.Count > 0;
    public bool HasDetail => _detail is not null;
    public string StoreName => _detail?.StoreName ?? "Retail Store";
    public string StoreAddress => _detail?.StoreAddress ?? "Local POS Terminal";
    public string OrderNumber => _detail?.OrderNumber ?? "-";
    public string CashierName => _detail?.CashierName ?? "-";
    public string RegisterName => _detail?.RegisterName ?? "-";
    public string IssuedAtText =>
        _detail?.IssuedAtUtc.ToLocalTime().ToString("yyyy-MM-dd HH:mm") ?? "-";
    public string BusinessDateText => _detail?.BusinessDate.ToString("yyyy-MM-dd") ?? "-";
    public string SubtotalAmountText => $"{_detail?.SubtotalAmount ?? 0m:N0} KRW";
    public string DiscountAmountText => $"-{_detail?.DiscountAmount ?? 0m:N0} KRW";
    public string TotalAmountText => $"{_detail?.TotalAmount ?? 0m:N0} KRW";

    public Task ActivateAsync()
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        var completedReceipt = _receiptPreviewState.GetCurrent();
        if (_workflowNavigator.Current == CashierWorkflowScreen.ReceiptDetail &&
            completedReceipt is { LocalOrderId: var orderId } &&
            orderId != Guid.Empty)
        {
            SelectedDate = completedReceipt.BusinessDate.ToDateTime(TimeOnly.MinValue);
            SearchText = null;
            return ActivateCompletedReceiptAsync(completedReceipt);
        }

        return LoadPageAsync(reset: true, CancellationToken.None);
    }

    private async Task ActivateCompletedReceiptAsync(ReceiptPreview completedReceipt)
    {
        await LoadDetailAsync(completedReceipt.LocalOrderId);
        if (_disposed || _workflowNavigator.Current != CashierWorkflowScreen.ReceiptDetail)
        {
            return;
        }

        var resolvedDetail = _detail?.LocalOrderId == completedReceipt.LocalOrderId
            ? _detail
            : completedReceipt;

        await LoadPageAsync(
            reset: true,
            CancellationToken.None,
            completedReceipt.LocalOrderId,
            preserveDetailWhenSelectionMissing: true);

        if (!_disposed &&
            _workflowNavigator.Current == CashierWorkflowScreen.ReceiptDetail)
        {
            SetDetail(resolvedDetail);
        }
    }

    public void Deactivate()
    {
        Interlocked.Increment(ref _loadVersion);
        Interlocked.Increment(ref _detailVersion);
        CancelAndDispose(ref _loadCancellation);
        CancelAndDispose(ref _detailCancellation);
        PrintCommand.Cancel();
    }

    public void ResetSession()
    {
        Deactivate();
        _lastCompletionOrderId = null;
        SearchText = null;
        SelectedDate = _clock.UtcNow.ToLocalTime().Date;
        SetSelectedReceipt(null, loadDetail: false);
        Receipts.Clear();
        HasMore = false;
        IsLoading = false;
        IsDetailLoading = false;
        IsPrinting = false;
        IsSuccessStatus = false;
        StatusMessage = null;
        ErrorMessage = null;
        SetDetail(null);
        OnPropertyChanged(nameof(HasReceipts));
    }

    private bool CanLoad() => !_disposed && !IsLoading;
    private bool CanLoadMore() => CanLoad() && HasMore;
    private bool CanPrint() => !_disposed && HasDetail && !IsPrinting && !IsDetailLoading;

    private async Task LoadPageAsync(
        bool reset,
        CancellationToken cancellationToken,
        Guid? requiredSelectionId = null,
        bool preserveDetailWhenSelectionMissing = false)
    {
        if (_disposed)
        {
            return;
        }

        var version = Interlocked.Increment(ref _loadVersion);
        var linkedCancellation = ReplaceCancellation(ref _loadCancellation, cancellationToken);
        IsLoading = true;
        ErrorMessage = null;
        SearchCommand.NotifyCanExecuteChanged();
        RefreshCommand.NotifyCanExecuteChanged();
        LoadMoreCommand.NotifyCanExecuteChanged();

        try
        {
            var offset = reset ? 0 : Receipts.Count;
            var page = await _receiptHistoryQuery.SearchAsync(
                new ReceiptHistoryRequest(
                    DateOnly.FromDateTime(
                        SelectedDate ?? _clock.UtcNow.ToLocalTime().Date),
                    SearchText,
                    offset,
                    PageSize),
                linkedCancellation.Token);
            if (_disposed || version != Volatile.Read(ref _loadVersion))
            {
                return;
            }

            if (reset)
            {
                Receipts.Clear();
            }

            foreach (var item in page.Items)
            {
                Receipts.Add(new ReceiptHistoryItemViewModel(item));
            }

            HasMore = page.HasMore;
            OnPropertyChanged(nameof(HasReceipts));
            var completedReceipt = _receiptPreviewState.GetCurrent();
            var newCompletionId = completedReceipt?.LocalOrderId;
            var preferredId = requiredSelectionId ?? (newCompletionId != _lastCompletionOrderId
                ? newCompletionId
                : SelectedReceipt?.LocalOrderId);
            _lastCompletionOrderId = newCompletionId;
            var preferredReceipt = preferredId is { } id
                ? Receipts.FirstOrDefault(item => item.LocalOrderId == id)
                : null;
            if (requiredSelectionId is not null)
            {
                SetSelectedReceipt(preferredReceipt, loadDetail: false);
            }
            else
            {
                SelectedReceipt = preferredReceipt ?? Receipts.FirstOrDefault();
            }

            if (SelectedReceipt is null && !preserveDetailWhenSelectionMissing)
            {
                SetDetail(null);
            }
        }
        catch (OperationCanceledException) when (linkedCancellation.IsCancellationRequested)
        {
        }
        catch (Exception) when (!_disposed && version == Volatile.Read(ref _loadVersion))
        {
            ErrorMessage = "Receipts could not be loaded. Try again.";
        }
        finally
        {
            if (!_disposed && version == Volatile.Read(ref _loadVersion))
            {
                IsLoading = false;
                SearchCommand.NotifyCanExecuteChanged();
                RefreshCommand.NotifyCanExecuteChanged();
                LoadMoreCommand.NotifyCanExecuteChanged();
            }
        }
    }

    partial void OnSelectedReceiptChanged(ReceiptHistoryItemViewModel? value)
    {
        var completedReceipt = _receiptPreviewState.GetCurrent();
        if (value is null &&
            _workflowNavigator.Current == CashierWorkflowScreen.ReceiptDetail &&
            completedReceipt?.LocalOrderId == _detail?.LocalOrderId)
        {
            return;
        }

        if (!_disposed && !_suppressSelectionLoad)
        {
            PrintCommand.Cancel();
            _ = LoadDetailAsync(value);
        }
    }

    private Task LoadDetailAsync(ReceiptHistoryItemViewModel? selected) =>
        LoadDetailAsync(selected?.LocalOrderId);

    private async Task LoadDetailAsync(Guid? localOrderId)
    {
        var version = Interlocked.Increment(ref _detailVersion);
        CancelAndDispose(ref _detailCancellation);
        if (localOrderId is null)
        {
            SetDetail(null);
            return;
        }

        _detailCancellation = new CancellationTokenSource();
        var cancellation = _detailCancellation;
        IsDetailLoading = true;
        IsSuccessStatus = false;
        StatusMessage = null;
        ErrorMessage = null;
        PrintCommand.NotifyCanExecuteChanged();
        try
        {
            var detail = await _receiptHistoryQuery.GetDetailAsync(
                localOrderId.Value,
                cancellation.Token);
            if (_disposed || version != Volatile.Read(ref _detailVersion))
            {
                return;
            }

            SetDetail(detail);
            if (detail is null)
            {
                ErrorMessage = "The selected receipt is no longer available.";
            }
        }
        catch (OperationCanceledException) when (cancellation.IsCancellationRequested)
        {
        }
        catch (Exception) when (!_disposed && version == Volatile.Read(ref _detailVersion))
        {
            SetDetail(null);
            ErrorMessage = "Receipt details could not be loaded. Try again.";
        }
        finally
        {
            if (!_disposed && version == Volatile.Read(ref _detailVersion))
            {
                IsDetailLoading = false;
                PrintCommand.NotifyCanExecuteChanged();
            }
        }
    }

    private void SetSelectedReceipt(
        ReceiptHistoryItemViewModel? receipt,
        bool loadDetail)
    {
        _suppressSelectionLoad = !loadDetail;
        try
        {
            SelectedReceipt = receipt;
        }
        finally
        {
            _suppressSelectionLoad = false;
        }
    }

    private async Task PrintAsync(CancellationToken cancellationToken)
    {
        if (_detail is null)
        {
            return;
        }

        IsPrinting = true;
        IsSuccessStatus = false;
        StatusMessage = "Print request sent. Waiting for simulator response...";
        ErrorMessage = null;
        try
        {
            var result = await _receiptPrinter.PrintAsync(_detail, cancellationToken);
            if (_disposed || cancellationToken.IsCancellationRequested)
            {
                return;
            }

            if (result.Succeeded)
            {
                IsSuccessStatus = true;
                ErrorMessage = null;
                StatusMessage = result.Message;
            }
            else
            {
                IsSuccessStatus = false;
                StatusMessage = null;
                ErrorMessage = result.Message;
            }
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
        }
        catch (Exception) when (!_disposed)
        {
            IsSuccessStatus = false;
            StatusMessage = null;
            ErrorMessage = "Receipt could not be printed. The completed order was not changed.";
        }
        finally
        {
            if (!_disposed)
            {
                IsPrinting = false;
                PrintCommand.NotifyCanExecuteChanged();
            }
        }
    }

    partial void OnIsLoadingChanged(bool value)
    {
        SearchCommand.NotifyCanExecuteChanged();
        RefreshCommand.NotifyCanExecuteChanged();
        LoadMoreCommand.NotifyCanExecuteChanged();
    }

    partial void OnHasMoreChanged(bool value) => LoadMoreCommand.NotifyCanExecuteChanged();

    partial void OnIsPrintingChanged(bool value) => PrintCommand.NotifyCanExecuteChanged();

    partial void OnIsDetailLoadingChanged(bool value) => PrintCommand.NotifyCanExecuteChanged();

    private void SetDetail(ReceiptPreview? detail)
    {
        _detail = detail;
        Lines.Clear();
        Payments.Clear();
        if (detail is not null)
        {
            foreach (var line in detail.Lines.Select(item => new ReceiptLineViewModel(item)))
            {
                Lines.Add(line);
            }

            foreach (var payment in detail.Payments.Select(item => new ReceiptPaymentViewModel(item)))
            {
                Payments.Add(payment);
            }
        }

        OnPropertyChanged(nameof(HasDetail));
        OnPropertyChanged(nameof(StoreName));
        OnPropertyChanged(nameof(StoreAddress));
        OnPropertyChanged(nameof(OrderNumber));
        OnPropertyChanged(nameof(CashierName));
        OnPropertyChanged(nameof(RegisterName));
        OnPropertyChanged(nameof(IssuedAtText));
        OnPropertyChanged(nameof(BusinessDateText));
        OnPropertyChanged(nameof(SubtotalAmountText));
        OnPropertyChanged(nameof(DiscountAmountText));
        OnPropertyChanged(nameof(TotalAmountText));
        PrintCommand.NotifyCanExecuteChanged();
    }

    private void GoBack()
    {
        if (_workflowNavigator.Current == CashierWorkflowScreen.ReceiptDetail)
        {
            _workflowNavigator.Navigate(
                CashierWorkflowScreen.ReceiptHistory,
                CashierWorkflowNavigationKind.Replace);
            return;
        }

        _workflowNavigator.Reset(CashierWorkflowScreen.Register);
    }

    private static CancellationTokenSource ReplaceCancellation(
        ref CancellationTokenSource? target,
        CancellationToken cancellationToken)
    {
        CancelAndDispose(ref target);
        target = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        return target;
    }

    private static void CancelAndDispose(ref CancellationTokenSource? target)
    {
        var cancellation = target;
        target = null;
        cancellation?.Cancel();
        cancellation?.Dispose();
    }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        Interlocked.Increment(ref _loadVersion);
        Interlocked.Increment(ref _detailVersion);
        Deactivate();
    }
}

public sealed class ReceiptHistoryItemViewModel(ReceiptHistorySummary summary)
{
    public Guid LocalOrderId { get; } = summary.LocalOrderId;
    public string OrderNumber { get; } = summary.OrderNumber;
    public string CompletedTimeText { get; } = summary.CompletedAtUtc.ToLocalTime().ToString("HH:mm");
    public string CompletedDateTimeText { get; } = summary.CompletedAtUtc.ToLocalTime().ToString("yyyy-MM-dd HH:mm");
    public string PaymentMethodText { get; } = summary.PaymentMethod.ToString();
    public string TotalAmountText { get; } = $"{summary.TotalAmount:N0} KRW";
    public string CashierName { get; } = summary.CashierName;
}
