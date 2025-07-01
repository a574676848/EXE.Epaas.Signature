using System;

namespace EXE.Epaas.Signature
{
    /// <summary>
    /// ApiClient 的配置选项
    /// </summary>
    public class ApiClientOptions
    {
        /// <summary>
        /// API 基础地址 (e.g., https://t-open-cloud.exexm.com)
        /// </summary>
        public string? BaseUri { get; set; }

        /// <summary>
        /// 应用唯一标识
        /// </summary>
        public string? AccessId { get; set; }

        /// <summary>
        /// 应用密钥
        /// </summary>
        public string? SecretKey { get; set; }

        /// <summary>
        /// 请求超时时间 (秒)，默认为 30
        /// </summary>
        public int TimeoutSeconds { get; set; } = 30;
    }
}
