using ApiGateway.Dtos;
using ApiGateway.Plugins;
using System.Security.Claims;
using System.Text;

namespace ApiGateway.Services
{
    public interface ISessionStore
    {
        Task<SessionData?> GetAsync(string id);
        Task DeleteAsync(string id);
        Task<bool> ExistsAsync(string id);
        Task SetASync(SessionData sessionData);
        Task<IEnumerable<SessionData>> GetAllAsync(string user);
    }

    public record UserGroups(bool User, bool Admin)
    {
        public bool HasAnyAccess => User || Admin;
    }

    public sealed class CurrentUser : ICurrentUser
    {
        private readonly ClaimsPrincipal _principal;
        public CurrentUser(IHttpContextAccessor http)
        {
            _principal = http.HttpContext?.User ?? new ClaimsPrincipal(new ClaimsIdentity());
        }

        public bool IsAuthenticated => _principal.Identity?.IsAuthenticated ?? false;
        public string? Sub => _principal.FindFirstValue(ClaimTypes.NameIdentifier) ?? _principal.FindFirstValue("sub");
        public string Email => _principal.FindFirstValue(ClaimTypes.Email) ?? _principal.FindFirstValue("email") ?? string.Empty;
        public string Name => _principal.FindFirstValue("name") ?? _principal.FindFirstValue(ClaimTypes.Name) ?? Email;
        public string UserId => Sub ?? Email;
        public UserGroups Groups
        {
            get
            {
                var g = _principal.FindAll("groups").Select(a => a.Value).ToHashSet(StringComparer.OrdinalIgnoreCase);
                return new UserGroups(g.Contains("user"), g.Contains("admin"));
            }
        }
    }
}
/// <summary>
/// A zero-dependency, <b>offline</b> <see cref="IEmbedder"/>. It turns text into a deterministic
/// vector using the "hashing trick" (feature hashing) over word tokens and character 3-grams, then
/// L2-normalizes the result. It captures <i>lexical</i> similarity (word / character overlap), not
/// deep semantic meaning — but that's enough to exercise the full RAG pipeline
/// (embed → cosine → top-K) with <b>no API key and no network</b>. Select it with
/// <c>Embeddings:Provider=inmemory</c>.
/// <para>
/// Determinism matters: the cache in <see cref="InMemoryRagSearch"/> keys on text, so the same text
/// must always embed to the same vector — across processes too. We therefore use a stable FNV-1a
/// hash, never <see cref="string.GetHashCode()"/> (which is randomized per run in .NET Core).
/// </para>
/// </summary>
public sealed class InMemoryEmbedder : IEmbedder
{
    private readonly int _dimensions;

    public InMemoryEmbedder(int dimensions = 256)
    {
        if (dimensions < 8) throw new ArgumentOutOfRangeException(nameof(dimensions), "Use at least 8 dimensions.");
        _dimensions = dimensions;
    }

    /// <summary>Always available — no configuration or network required.</summary>
    public bool IsEnabled => true;

    public Task<float[]?> EmbedAsync(string text, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(text)) return Task.FromResult<float[]?>(null);

        var vec = new float[_dimensions];
        foreach (var token in Tokenize(text))
        {
            AddFeature(vec, token);                        // whole-word feature (term frequency)
            foreach (var gram in CharTrigrams(token))      // char 3-grams: fuzzy / morphological overlap
                AddFeature(vec, gram);
        }

        Normalize(vec);                                    // unit length → cosine == dot product
        return Task.FromResult<float[]?>(vec);
    }

    // Hash a feature to a slot and accumulate. Signed hashing (a sign bit from the hash) lets
    // collisions tend to cancel out instead of always piling up — a standard feature-hashing trick.
    private void AddFeature(float[] vec, string feature)
    {
        var h = Fnv1a(feature);
        var idx = (int)(h % (uint)_dimensions);
        var sign = (h & 0x8000_0000u) == 0 ? 1f : -1f;
        vec[idx] += sign;
    }

    // Lowercase, split on anything that isn't a letter or digit.
    private static IEnumerable<string> Tokenize(string text)
    {
        var sb = new StringBuilder();
        foreach (var ch in text)
        {
            if (char.IsLetterOrDigit(ch))
            {
                sb.Append(char.ToLowerInvariant(ch));
            }
            else if (sb.Length > 0)
            {
                yield return sb.ToString();
                sb.Clear();
            }
        }
        if (sb.Length > 0) yield return sb.ToString();
    }

    private static IEnumerable<string> CharTrigrams(string token)
    {
        for (var i = 0; i + 3 <= token.Length; i++)
            yield return token.Substring(i, 3);
    }

    private static void Normalize(float[] vec)
    {
        double sumSq = 0;
        foreach (var v in vec) sumSq += (double)v * v;
        if (sumSq <= 0) return;
        var inv = (float)(1.0 / Math.Sqrt(sumSq));
        for (var i = 0; i < vec.Length; i++) vec[i] *= inv;
    }

    // FNV-1a (32-bit): tiny, fast, and stable across processes (unlike string.GetHashCode()).
    private static uint Fnv1a(string s)
    {
        const uint offset = 2166136261u, prime = 16777619u;
        var hash = offset;
        foreach (var ch in s)
        {
            hash ^= ch;
            hash *= prime;
        }
        return hash;
    }
}

/// <summary>
/// Prefers a <c>primary</c> embedder but transparently substitutes a <c>fallback</c> (typically
/// <see cref="InMemoryEmbedder"/>) when the primary isn't configured. The choice is made once, up
/// front: a corpus must be embedded by a single model, so we never mix vector spaces mid-stream.
/// This keeps RAG memory on real vectors (cosine ranking) instead of silently degrading to keyword
/// search when an API key is missing.
/// </summary>
public sealed class FallbackEmbedder : IEmbedder
{
    private readonly IEmbedder _active;

    public FallbackEmbedder(IEmbedder primary, IEmbedder fallback, ILogger<FallbackEmbedder> log)
    {
        if (primary.IsEnabled)
        {
            _active = primary;
        }
        else
        {
            _active = fallback;
            log.LogWarning("[Embedder] '{Primary}' is not configured — falling back to '{Fallback}'.",
                primary.GetType().Name, fallback.GetType().Name);
        }
    }

    public bool IsEnabled => _active.IsEnabled;

    public Task<float[]?> EmbedAsync(string text, CancellationToken ct) => _active.EmbedAsync(text, ct);
}
