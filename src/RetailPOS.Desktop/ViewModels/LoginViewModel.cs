using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using RetailPOS.Application.Authentication;

namespace RetailPOS.Desktop.ViewModels;

public sealed partial class LoginViewModel : ObservableObject
{
    private readonly ILoginService _loginService;

    public LoginViewModel(ILoginService loginService)
    {
        _loginService = loginService;
        SignInCommand = new AsyncRelayCommand(SignInAsync, () => !IsBusy);
    }

    public event EventHandler? SignedIn;

    public IAsyncRelayCommand SignInCommand { get; }

    [ObservableProperty]
    private string _employeeCode = "E0001";

    [ObservableProperty]
    private string _password = string.Empty;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasError))]
    private string? _errorMessage;

    [ObservableProperty]
    private bool _isBusy;

    public bool HasError => !string.IsNullOrWhiteSpace(ErrorMessage);

    private async Task SignInAsync()
    {
        ErrorMessage = null;

        if (string.IsNullOrWhiteSpace(EmployeeCode) || string.IsNullOrEmpty(Password))
        {
            ErrorMessage = "Enter employee code and password.";
            return;
        }

        IsBusy = true;
        try
        {
            var result = await _loginService.SignInAsync(new LoginRequest(EmployeeCode, Password));
            if (!result.IsSuccess)
            {
                ErrorMessage = result.FailureMessage ?? "Sign-in failed. Check your employee code and password.";
                return;
            }

            Password = string.Empty;
            SignedIn?.Invoke(this, EventArgs.Empty);
        }
        catch (OperationCanceledException)
        {
            ErrorMessage = "Sign-in was cancelled. Try again.";
        }
        catch (Exception)
        {
            ErrorMessage = "Sign-in could not be completed. Try again.";
        }
        finally
        {
            IsBusy = false;
        }
    }

    partial void OnIsBusyChanged(bool value) => SignInCommand.NotifyCanExecuteChanged();
}
