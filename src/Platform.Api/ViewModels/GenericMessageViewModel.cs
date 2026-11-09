namespace Platform.Api.ViewModels;

/// <summary>
/// Presentation model carrying a generic confirmation or status message.
/// </summary>
public sealed class GenericMessageViewModel
{
    public string Message { get; }

    public GenericMessageViewModel(string message)
    {
        Message = message ?? string.Empty;
    }
}
