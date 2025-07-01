using Microsoft.Extensions.Options;
using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace EXE.Epaas.Signature
{
    /// <summary>
    /// API 客户端
    /// </summary>
    public class ApiClient : IDisposable
    {
        // Constants for magic strings
        private const string AuthEndpoint = "auth";
        private const string AccessIdHeader = "x-access-id";
        private const string NonceHeader = "x-nonce";
        private const string SignVersionHeader = "x-sign-version";
        private const string TimestampHeader = "x-timestamp";
        private const string SignatureHeader = "x-signature";
        private const string SignatureHeadersHeader = "x-signature-headers";
        private const string OpenAccessKeyHeader = "open-access-key";
        private const string SignVersion = "V3";

        private readonly HttpClient _httpClient;
        private readonly string _accessId;
        private readonly string _secretKey;
        private readonly string _baseUri;
        private readonly IAccessTokenCache _tokenCache;
        private readonly bool _disposeClient;

        // Semaphore for thread-safe token refreshing
        private readonly SemaphoreSlim _tokenRefreshLock = new SemaphoreSlim(1, 1);
        // Use a single Random instance for better performance and randomness
        private static readonly Random _random = new Random();

        /// <summary>
        /// 构造函数 (推荐在生产环境中使用 IHttpClientFactory 注入 HttpClient)。
        /// </summary>
        /// <param name="httpClient">由外部管理的 HttpClient 实例</param>
        /// <param name="options">客户端配置</param>
        /// <param name="tokenCache">可选的自定义 AccessToken 缓存实现</param>
        public ApiClient(HttpClient httpClient, IOptions<ApiClientOptions> options, IAccessTokenCache? tokenCache = null)
        {
            if (options?.Value == null) throw new ArgumentNullException(nameof(options));
            var opts = options.Value;
            if (string.IsNullOrEmpty(opts.BaseUri)) throw new ArgumentException("BaseUri cannot be null or empty.", nameof(opts.BaseUri));
            if (string.IsNullOrEmpty(opts.AccessId)) throw new ArgumentException("AccessId cannot be null or empty.", nameof(opts.AccessId));
            if (string.IsNullOrEmpty(opts.SecretKey)) throw new ArgumentException("SecretKey cannot be null or empty.", nameof(opts.SecretKey));

            _httpClient = httpClient;
            _disposeClient = false; // HttpClient is managed externally
            _baseUri = opts.BaseUri.EndsWith("/") ? opts.BaseUri : opts.BaseUri + "/";
            _accessId = opts.AccessId;
            _secretKey = opts.SecretKey;
            _tokenCache = tokenCache ?? InMemoryAccessTokenCache.Instance;
        }

        /// <summary>
        /// 构造函数 (用于简单场景或测试)。
        /// 此构造函数会创建一个新的 HttpClient 实例。
        /// </summary>
        /// <param name="options">客户端配置</param>
        /// <param name="tokenCache">可选的自定义 AccessToken 缓存实现</param>
        public ApiClient(ApiClientOptions options, IAccessTokenCache? tokenCache = null)
        {
            if (options == null) throw new ArgumentNullException(nameof(options));
            if (string.IsNullOrEmpty(options.BaseUri)) throw new ArgumentException("BaseUri cannot be null or empty.", nameof(options.BaseUri));
            if (string.IsNullOrEmpty(options.AccessId)) throw new ArgumentException("AccessId cannot be null or empty.", nameof(options.AccessId));
            if (string.IsNullOrEmpty(options.SecretKey)) throw new ArgumentException("SecretKey cannot be null or empty.", nameof(options.SecretKey));

            _httpClient = new HttpClient
            {
                Timeout = TimeSpan.FromSeconds(options.TimeoutSeconds)
            };
            _disposeClient = true; // HttpClient is managed internally
            _baseUri = options.BaseUri.EndsWith("/") ? options.BaseUri : options.BaseUri + "/";
            _accessId = options.AccessId;
            _secretKey = options.SecretKey;
            _tokenCache = tokenCache ?? InMemoryAccessTokenCache.Instance;
        }

        private async Task<string> GetAccessTokenAsync(CancellationToken cancellationToken = default)
        {
            var cacheKey = $"epaas_token::{_accessId}";
            var token = await _tokenCache.GetAsync(cacheKey, cancellationToken);
            if (!string.IsNullOrEmpty(token))
            {
                return token;
            }

            // Wait for the lock to ensure only one thread refreshes the token
            await _tokenRefreshLock.WaitAsync(cancellationToken);
            try
            {
                // Double-check if another thread has already refreshed the token while we were waiting
                token = await _tokenCache.GetAsync(cacheKey, cancellationToken);
                if (!string.IsNullOrEmpty(token))
                {
                    return token;
                }

                var requestUri = AuthEndpoint;
                var method = "GET";
                var timestamp = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds().ToString();
                var nonce = _random.Next(100000, 999999).ToString();

                var headersToSign = new SortedDictionary<string, string>
                {
                    { AccessIdHeader, _accessId },
                    { NonceHeader, nonce },
                    { SignVersionHeader, SignVersion },
                    { TimestampHeader, timestamp }
                };

                var headerStr = SignatureUtil.BuildHeaderString(headersToSign);
                var stringToSign = SignatureUtil.Join(method, "/" + requestUri, string.Empty, headerStr, string.Empty, _secretKey);
                var signature = SignatureUtil.GenerateMd5Sign(stringToSign);

                using (var request = new HttpRequestMessage(HttpMethod.Get, _baseUri + requestUri))
                {
                    foreach (var header in headersToSign)
                    {
                        request.Headers.Add(header.Key, header.Value);
                    }
                    request.Headers.Add(SignatureHeadersHeader, string.Join(",", headersToSign.Keys));
                    request.Headers.Add(SignatureHeader, signature);

                    var response = await _httpClient.SendAsync(request, cancellationToken);
                    response.EnsureSuccessStatusCode();

                    var responseBody = await response.Content.ReadAsStringAsync();
                    var accessKey = HttpUtils.ParseJsonResponse(responseBody, "accessKey");
                    var expireSecondsStr = HttpUtils.ParseJsonResponse(responseBody, "expireSeconds");

                    if (string.IsNullOrEmpty(accessKey) || string.IsNullOrEmpty(expireSecondsStr) || !int.TryParse(expireSecondsStr, out var expireSeconds))
                    {
                        throw new InvalidOperationException("无法从响应中解析 accessKey 或 expireSeconds。响应内容: " + responseBody);
                    }

                    // 减去60秒作为缓冲，防止因网络延迟或服务器时间略有偏差导致token提前失效
                    await _tokenCache.SetAsync(cacheKey, accessKey, expireSeconds - 60, cancellationToken);

                    return accessKey;
                }
            }
            finally
            {
                _tokenRefreshLock.Release();
            }
        }

        /// <summary>
        /// 发起 POST 请求
        /// </summary>
        /// <param name="requestUri">请求路径 (e.g., oapi/paas/data/base/prod/p_base_user__sys/paginate)</param>
        /// <param name="body">请求体 (JSON string)</param>
        /// <param name="queryParams">查询参数</param>
        /// <param name="headers">其他请求头</param>
        /// <param name="cancellationToken">用于取消操作的令牌</param>
        /// <returns>响应内容</returns>
        public async Task<string> PostAsync(string requestUri, string body, Dictionary<string, string>? queryParams = null, Dictionary<string, string>? headers = null, CancellationToken cancellationToken = default)
        {
            var token = await GetAccessTokenAsync(cancellationToken);

            var method = "POST";
            var timestamp = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds().ToString();
            var nonce = _random.Next(100000, 999999).ToString();

            var headersToSign = new SortedDictionary<string, string>
            {
                { AccessIdHeader, _accessId },
                { NonceHeader, nonce },
                { SignVersionHeader, SignVersion },
                { TimestampHeader, timestamp }
            };

            var headerStr = SignatureUtil.BuildHeaderString(headersToSign);
            var processedBody = SignatureUtil.ProcessBody(body);
            var queryString = HttpUtils.BuildQueryString(queryParams);
            var stringToSign = SignatureUtil.Join(method, "/" + requestUri, queryString, headerStr, processedBody, _secretKey);
            var signature = SignatureUtil.GenerateMd5Sign(stringToSign);

            var fullRequestUri = string.IsNullOrEmpty(queryString) ? requestUri : $"{requestUri}?{queryString}";
            using (var request = new HttpRequestMessage(HttpMethod.Post, _baseUri + fullRequestUri))
            {
                request.Content = new StringContent(body, Encoding.UTF8, "application/json");

                request.Headers.Add(OpenAccessKeyHeader, token);
                foreach (var header in headersToSign)
                {
                    request.Headers.Add(header.Key, header.Value);
                }
                request.Headers.Add(SignatureHeadersHeader, string.Join(",", headersToSign.Keys));
                request.Headers.Add(SignatureHeader, signature);

                if (headers != null)
                {
                    foreach (var header in headers)
                    {
                        request.Headers.Add(header.Key, header.Value);
                    }
                }

                var response = await _httpClient.SendAsync(request, cancellationToken);
                if (!response.IsSuccessStatusCode)
                {
                    var curlCommand = await HttpUtils.BuildCurlCommandAsync(request).ConfigureAwait(false);
                    Console.WriteLine("--- Begin CURL Info for failed request ---");
                    Console.WriteLine(curlCommand);
                    Console.WriteLine($"Response Status: {response.StatusCode}");
                    var responseBody = await response.Content.ReadAsStringAsync().ConfigureAwait(false);
                    Console.WriteLine($"Response Body: {responseBody}");
                    Console.WriteLine("--- End CURL Info ---");
                }
                response.EnsureSuccessStatusCode();

                return await response.Content.ReadAsStringAsync();
            }
        }

        public void Dispose()
        {
            if (_disposeClient)
            {
                _httpClient?.Dispose();
            }
            _tokenRefreshLock?.Dispose();
        }
    }
}
