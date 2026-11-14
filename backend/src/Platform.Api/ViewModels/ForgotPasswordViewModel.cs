namespace Platform.Api.ViewModels;

/// <summary>
/// Presentation model returned from forgot password requests.
/// </summary>
public sealed class ForgotPasswordViewModel
{
    public string Message { get; }

    public ForgotPasswordViewModel(string message)
    {
        Message = message ?? string.Empty;
    }
}
