using System.Collections.Concurrent;
using System.Security.Cryptography;
using System.Text;

namespace ADHD.Backend.Sms;

public enum SmsVerifyResult
{
    Verified,
    NoActiveCode,
    Expired,
    Incorrect,
    TooManyAttempts
}

/// <summary>
/// Creates and checks SMS verification codes. Codes live in memory only
/// (like the email codes), so they are lost when the server restarts.
/// </summary>
public sealed class SmsOtpService
{
    private const int CodeLifetimeMinutes = 5;
    private const int MaxVerifyAttempts = 5;
    private static readonly TimeSpan ResendCooldown = TimeSpan.FromSeconds(60);

    private sealed class OtpEntry
    {
        public OtpEntry(byte[] codeHash, DateTime sentAt, DateTime expiresAt)
        {
            CodeHash = codeHash;
            SentAt = sentAt;
            ExpiresAt = expiresAt;
        }

        public byte[] CodeHash { get; }
        public DateTime SentAt { get; }
        public DateTime ExpiresAt { get; }

        // Updated with Interlocked, so it is a field and not a property.
        public int Attempts;
    }

    private readonly ConcurrentDictionary<string, OtpEntry> _entries = new();

    /// <summary>False while the previous code is still inside the cooldown.</summary>
    public bool CanSend(string phone, out TimeSpan retryAfter)
    {
        retryAfter = TimeSpan.Zero;

        if (!_entries.TryGetValue(phone, out var entry))
        {
            return true;
        }

        var remaining = entry.SentAt + ResendCooldown - DateTime.UtcNow;

        if (remaining <= TimeSpan.Zero)
        {
            return true;
        }

        retryAfter = remaining;
        return false;
    }

    /// <summary>Creates a new code, stores only its hash and returns the code.</summary>
    public string Issue(string phone)
    {
        PurgeExpired();

        var code = RandomNumberGenerator.GetInt32(100000, 1000000).ToString();
        var now = DateTime.UtcNow;

        _entries[phone] = new OtpEntry(
            Hash(phone, code),
            now,
            now.AddMinutes(CodeLifetimeMinutes));

        return code;
    }

    /// <summary>Forgets the code, for example when the SMS could not be sent.</summary>
    public void Discard(string phone) => _entries.TryRemove(phone, out _);

    public SmsVerifyResult Verify(string phone, string code)
    {
        if (!_entries.TryGetValue(phone, out var entry))
        {
            return SmsVerifyResult.NoActiveCode;
        }

        if (DateTime.UtcNow > entry.ExpiresAt)
        {
            _entries.TryRemove(phone, out _);
            return SmsVerifyResult.Expired;
        }

        if (Interlocked.Increment(ref entry.Attempts) > MaxVerifyAttempts)
        {
            _entries.TryRemove(phone, out _);
            return SmsVerifyResult.TooManyAttempts;
        }

        if (!CryptographicOperations.FixedTimeEquals(
                Hash(phone, code), entry.CodeHash))
        {
            return SmsVerifyResult.Incorrect;
        }

        _entries.TryRemove(phone, out _);
        return SmsVerifyResult.Verified;
    }

    private void PurgeExpired()
    {
        var now = DateTime.UtcNow;

        foreach (var pair in _entries)
        {
            if (pair.Value.ExpiresAt < now)
            {
                _entries.TryRemove(pair.Key, out _);
            }
        }
    }

    private static byte[] Hash(string phone, string code) =>
        SHA256.HashData(Encoding.UTF8.GetBytes($"{phone}:{code}"));
}
