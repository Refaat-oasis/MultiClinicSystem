namespace Platform.Api.ViewModels;

/// <summary>
/// Presentation model returned after issuing an invitation.
/// </summary>
public sealed class InviteUserViewModel
{
    public Guid UserId { get; }
    public string Message { get; }

    public InviteUserViewModel(Guid userId, string message)
    {
        UserId = userId;
        Message = message ?? string.Empty;
    }
}
