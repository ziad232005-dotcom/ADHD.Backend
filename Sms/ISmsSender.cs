namespace ADHD.Backend.Sms;

/// <summary>
/// Sends a text message. Implement this interface to switch to another
/// provider (for example Twilio) without touching the endpoints.
/// </summary>
public interface ISmsSender
{
    /// <returns>True when the provider accepted the message.</returns>
    Task<bool> SendAsync(
        string phoneNumber,
        string text,
        CancellationToken cancellationToken = default);
}
