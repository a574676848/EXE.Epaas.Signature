
using System.Threading;
using System.Threading.Tasks;

namespace EXE.Epaas.Signature
{
    /// <summary>
    /// 定义 AccessToken 缓存的接口。
    /// 调用方可以实现此接口以提供自定义的缓存策略 (如 Redis)。
    /// </summary>
    public interface IAccessTokenCache
    {
        /// <summary>
        /// 从缓存中异步获取 AccessToken。
        /// </summary>
        /// <param name="key">用于标识 AccessToken 的唯一键 (通常基于 AccessId)。</param>
        /// <param name="cancellationToken">用于取消操作的令牌。</param>
        /// <returns>如果缓存中存在且未过期，则返回 AccessToken；否则返回 null。</returns>
        Task<string?> GetAsync(string key, CancellationToken cancellationToken = default);

        /// <summary>
        /// 将 AccessToken 异步存入缓存。
        /// </summary>
        /// <param name="key">用于标识 AccessToken 的唯一键。</param>
        /// <param name="token">要缓存的 AccessToken。</param>
        /// <param name="expiresInSeconds">令牌的有效期(秒)。</param>
        /// <param name="cancellationToken">用于取消操作的令牌。</param>
        Task SetAsync(string key, string token, int expiresInSeconds, CancellationToken cancellationToken = default);
    }
}
