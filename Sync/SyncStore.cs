using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace ADHD.Backend.Sync;

/// <summary>One test result sent by a child's phone for the parent to read.</summary>
public sealed class SyncResult
{
    public string Id { get; set; } = "";
    public string Kind { get; set; } = "";
    public JsonElement Data { get; set; }
    public DateTime ReceivedAt { get; set; }
}

/// <summary>
/// Holds test results between a child's phone and the parent's phone.
/// The parent's phone makes a secret key and puts it in the QR code; the
/// child's phone sends results with it and the parent's phone reads them
/// with it. Only a hash of the key is stored. A JSON file is enough for a
/// graduation project.
/// </summary>
public sealed class SyncStore
{
    public const int MaxKeys = 3000;
    public const int MaxResultsPerKey = 500;
    public const int MaxDataLength = 4096;

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true
    };

    private readonly object _lock = new();
    private readonly string _file;
    private Dictionary<string, List<SyncResult>> _items;

    public SyncStore(IConfiguration configuration)
    {
        var folder = Path.GetFullPath(
            configuration["Shadows:DataPath"] ?? "App_Data");
        Directory.CreateDirectory(folder);
        _file = Path.Combine(folder, "sync.json");
        _items = Load();
    }

    private Dictionary<string, List<SyncResult>> Load()
    {
        if (!File.Exists(_file)) return new();

        try
        {
            return JsonSerializer
                .Deserialize<Dictionary<string, List<SyncResult>>>(
                    File.ReadAllText(_file)) ?? new();
        }
        catch (JsonException)
        {
            return new();
        }
    }

    public static string Hash(string key) =>
        Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(key)));

    /// <summary>
    /// Stores a result. Sending the same id again changes nothing.
    /// Returns false when the server is full.
    /// </summary>
    public bool Add(string key, string kind, string id, JsonElement data)
    {
        var hash = Hash(key);

        lock (_lock)
        {
            if (!_items.TryGetValue(hash, out var list))
            {
                if (_items.Count >= MaxKeys) return false;
                list = new List<SyncResult>();
                _items[hash] = list;
            }

            if (list.Any(r => r.Kind == kind && r.Id == id)) return true;
            if (list.Count >= MaxResultsPerKey) return false;

            list.Add(new SyncResult
            {
                Id = id,
                Kind = kind,
                Data = data.Clone(),
                ReceivedAt = DateTime.UtcNow
            });

            File.WriteAllText(_file, JsonSerializer.Serialize(_items, JsonOptions));
            return true;
        }
    }

    public List<SyncResult> Results(string key)
    {
        lock (_lock)
        {
            return _items.TryGetValue(Hash(key), out var list)
                ? list.ToList()
                : new List<SyncResult>();
        }
    }
}
