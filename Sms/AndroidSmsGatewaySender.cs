using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text;
using Microsoft.Extensions.Options;

namespace ADHD.Backend.Sms;

/// <summary>
/// Sends SMS through an Android phone running "SMS Gateway for Android"
/// in Local Server mode (POST {BaseUrl}/message with Basic authentication).
/// </summary>
public sealed class AndroidSmsGatewaySender : ISmsSender
{
    private readonly HttpClient _httpClient;
    private readonly SmsGatewayOptions _options;
    private readonly ILogger<AndroidSmsGatewaySender> _logger;

    public AndroidSmsGatewaySender(
        HttpClient httpClient,
        IOptions<SmsGatewayOptions> options,
        ILogger<AndroidSmsGatewaySender> logger)
    {
        _httpClient = httpClient;
        _options = options.Value;
        _logger = logger;
    }

    public async Task<bool> SendAsync(
        string phoneNumber,
        string text,
        CancellationToken cancellationToken = default)
    {
        if (!_options.IsConfigured)
        {
            _logger.LogWarning(
                "SMS gateway is not configured. Set the SmsGateway values " +
                "in User Secrets.");
            return false;
        }

        var baseUri = new Uri(_options.BaseUrl.TrimEnd('/') + "/");

        using var request = new HttpRequestMessage(
            HttpMethod.Post,
            new Uri(baseUri, "message"));

        var credentials = Convert.ToBase64String(
            Encoding.UTF8.GetBytes($"{_options.Username}:{_options.Password}"));

        request.Headers.Authorization =
            new AuthenticationHeaderValue("Basic", credentials);

        request.Content = JsonContent.Create(new
        {
            textMessage = new { text },
            phoneNumbers = new[] { phoneNumber }
        });

        try
        {
            using var response =
                await _httpClient.SendAsync(request, cancellationToken);

            if (response.IsSuccessStatusCode)
            {
                return true;
            }

            _logger.LogWarning(
                "SMS gateway rejected the message with status {StatusCode}.",
                (int)response.StatusCode);
            return false;
        }
        catch (Exception ex) when (
            ex is HttpRequestException or TaskCanceledException)
        {
            _logger.LogWarning(
                ex,
                "Could not reach the SMS gateway at {BaseUrl}.",
                _options.BaseUrl);
            return false;
        }
    }
}
