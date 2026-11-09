namespace BuildingBlocks.Application;

/// <summary>
/// Service contract for sending transactional emails across the platform.
/// </summary>
public interface IEmailSender
{
    Task SendEmailAsync(string to, string subject, string body, CancellationToken cancellationToken = default);
}
