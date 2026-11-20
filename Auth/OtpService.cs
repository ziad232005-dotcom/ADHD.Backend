using System.Collections.Concurrent;
using System.Security.Cryptography;
using System.Text;

namespace ADHD.Backend.Auth;

public enum OtpVerifyResult
{
    Verified,
    NoActiveCode,
    Expired,
    Incorrect,
    TooManyAttempts
}

/// <summary>
/// Creates and checks verification codes for an email address or a phone
/// number (the "destination"). Codes live in memory only, so they are lost
/// when the server restarts. A code can be tried 5 times, and a new one
/// can be requested once a minute.
/// </summary>
public sealed class OtpService
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
    public bool CanSend(string destination, out TimeSpan retryAfter)
    {
        retryAfter = TimeSpan.Zero;

        if (!_entries.TryGetValue(destination, out var entry))
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
    public string Issue(string destination)
    {
        PurgeExpired();

        var code = RandomNumberGenerator.GetInt32(100000, 1000000).ToString();
        var now = DateTime.UtcNow;

        _entries[destination] = new OtpEntry(
            Hash(destination, code),
            now,
            now.AddMinutes(CodeLifetimeMinutes));

        return code;
    }

    /// <summary>Forgets the code, for example when the SMS could not be sent.</summary>
    public void Discard(string destination) => _entries.TryRemove(destination, out _);

    public OtpVerifyResult Verify(string destination, string code)
    {
        if (!_entries.TryGetValue(destination, out var entry))
        {
            return OtpVerifyResult.NoActiveCode;
        }

        if (DateTime.UtcNow > entry.ExpiresAt)
        {
            _entries.TryRemove(destination, out _);
            return OtpVerifyResult.Expired;
        }

        if (Interlocked.Increment(ref entry.Attempts) > MaxVerifyAttempts)
        {
            _entries.TryRemove(destination, out _);
            return OtpVerifyResult.TooManyAttempts;
        }

        if (!CryptographicOperations.FixedTimeEquals(
                Hash(destination, code), entry.CodeHash))
        {
            return OtpVerifyResult.Incorrect;
        }

        _entries.TryRemove(destination, out _);
        return OtpVerifyResult.Verified;
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

    private static byte[] Hash(string destination, string code) =>
        SHA256.HashData(Encoding.UTF8.GetBytes($"{destination}:{code}"));
}
