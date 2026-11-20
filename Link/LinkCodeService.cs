using System.Collections.Concurrent;
using System.Security.Cryptography;

namespace ADHD.Backend.Link;

/// <summary>
/// Holds the data a parent's phone wants to hand to a child's phone, under
/// a short code the child types. A code works once and expires after ten
/// minutes. Nothing is written to disk.
/// </summary>
public sealed class LinkCodeService
{
    // No 0/O or 1/I/L, which are easy to mix up when typing.
    private const string Alphabet = "ABCDEFGHJKMNPQRSTUVWXYZ23456789";
    public const int CodeLength = 8;
    public const int MaxPayloadLength = 4000;
    private const int MaxStored = 5000;

    public static readonly TimeSpan Lifetime = TimeSpan.FromMinutes(10);

    private readonly ConcurrentDictionary<string, (string Payload, DateTime ExpiresAt)> _items = new();

    /// <summary>Returns the new code, or null when the server is full.</summary>
    public string? Create(string payload)
    {
        RemoveExpired();
        if (_items.Count >= MaxStored) return null;

        for (var attempt = 0; attempt < 5; attempt++)
        {
            var code = NewCode();
            if (_items.TryAdd(code, (payload, DateTime.UtcNow + Lifetime)))
            {
                return code;
            }
        }

        return null;
    }

    /// <summary>Returns the data and forgets the code, or null.</summary>
    public string? Claim(string code)
    {
        var key = Normalize(code);
        if (!_items.TryRemove(key, out var item)) return null;
        return DateTime.UtcNow <= item.ExpiresAt ? item.Payload : null;
    }

    public static string Normalize(string code) =>
        new string(code.Where(char.IsLetterOrDigit).ToArray()).ToUpperInvariant();

    private static string NewCode() =>
        string.Create(CodeLength, 0, (span, _) =>
        {
            for (var i = 0; i < span.Length; i++)
            {
                span[i] = Alphabet[RandomNumberGenerator.GetInt32(Alphabet.Length)];
            }
        });

    private void RemoveExpired()
    {
        var now = DateTime.UtcNow;
        foreach (var pair in _items)
        {
            if (pair.Value.ExpiresAt < now) _items.TryRemove(pair.Key, out _);
        }
    }
}
