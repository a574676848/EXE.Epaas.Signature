using System;
using System.Collections.Concurrent;
using System.Threading;
using System.Threading.Tasks;

namespace EXE.Epaas.Signature
{
    /// <summary>
    /// 默认的内存 AccessToken 缓存实现。
    /// </summary>
    public class InMemoryAccessTokenCache : IAccessTokenCache
    {
        private static readonly Lazy<InMemoryAccessTokenCache> _instance = new Lazy<InMemoryAccessTokenCache>(() => new InMemoryAccessTokenCache());

        public static InMemoryAccessTokenCache Instance => _instance.Value;

        private class CacheEntry
        {
            public string Token { get; }
            public DateTime Expiration { get; }

            public CacheEntry(string token, int expiresInSeconds)
            {
                Token = token;
                Expiration = DateTime.UtcNow.AddSeconds(expiresInSeconds);
            }

            public bool IsValid() => DateTime.UtcNow < Expiration && !string.IsNullOrEmpty(Token);
        }

        private readonly ConcurrentDictionary<string, CacheEntry> _cache = new ConcurrentDictionary<string, CacheEntry>();

        // 将构造函数设为私有，以强制使用单例实例
        private InMemoryAccessTokenCache() { }

        /// <inheritdoc />
        public Task<string?> GetAsync(string key, CancellationToken cancellationToken = default)
        {
            if (_cache.TryGetValue(key, out var entry) && entry.IsValid())
            {
                return Task.FromResult<string?>(entry.Token);
            }
            return Task.FromResult<string?>(null);
        }

        /// <inheritdoc />
        public Task SetAsync(string key, string token, int expiresInSeconds, CancellationToken cancellationToken = default)
        {
            var entry = new CacheEntry(token, expiresInSeconds);
            _cache[key] = entry;
            return Task.CompletedTask;
        }
    }
}
