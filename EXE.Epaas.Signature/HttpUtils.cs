using System.Collections.Generic;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;

namespace EXE.Epaas.Signature
{
    /// <summary>
    /// HTTP 相关的工具类
    /// </summary>
    public static class HttpUtils
    {
        /// <summary>
        /// 根据 HttpRequestMessage 构建 cURL 命令字符串，用于调试。
        /// </summary>
        /// <param name="request">HTTP 请求</param>
        /// <returns>cURL 命令字符串</returns>
        public static async Task<string> BuildCurlCommandAsync(HttpRequestMessage request)
        {
            var curl = new StringBuilder();
            curl.AppendLine($"curl -X {request.Method.Method} \"{request.RequestUri}\"");

            foreach (var header in request.Headers)
            {
                // PowerShell needs quotes around header values if they contain special characters
                curl.AppendLine($" -H \"{header.Key}: {string.Join(", ", header.Value)}\"");
            }

            if (request.Content != null)
            {
                foreach (var header in request.Content.Headers)
                {
                    curl.AppendLine($" -H \"{header.Key}: {string.Join(", ", header.Value)}\"");
                }

                string body = await request.Content.ReadAsStringAsync().ConfigureAwait(false);
                if (!string.IsNullOrEmpty(body))
                {
                    // For PowerShell, escape single quotes by doubling them if using single-quoted string,
                    // but since body is complex JSON, it's better to use a variable or here-string.
                    // For simplicity here, we'll just wrap in single quotes and escape internal single quotes.
                    string escapedBody = body.Replace("'", "''");
                    curl.AppendLine($" -d '{escapedBody}'");
                }
            }

            return curl.ToString();
        }

        /// <summary>
        /// 从字典构建查询字符串。
        /// </summary>
        /// <param name="queryParams">查询参数字典</param>
        /// <returns>URL 编码的查询字符串</returns>
        public static string BuildQueryString(Dictionary<string, string>? queryParams)
        {
            if (queryParams == null || queryParams.Count == 0)
            {
                return string.Empty;
            }

            var sortedParams = new SortedDictionary<string, string>(queryParams);
            var queryStringBuilder = new StringBuilder();

            foreach (var param in sortedParams)
            {
                if (!string.IsNullOrEmpty(param.Value))
                {
                    if (queryStringBuilder.Length > 0)
                    {
                        queryStringBuilder.Append("&");
                    }
                    queryStringBuilder.Append($"{param.Key}={param.Value}");
                }
            }

            return queryStringBuilder.ToString();
        }

        /// <summary>
        /// 简单的 JSON 解析器，用于避免在此示例中引入外部依赖。
        /// </summary>
        /// <param name="json">JSON 字符串</param>
        /// <param name="key">要查找的键</param>
        /// <returns>找到的值，否则为 null</returns>
        public static string? ParseJsonResponse(string json, string key)
        {
            // 尝试解析字符串类型的值: "key":"value"
            var stringSearch = $"\"{key}\":\"";
            var keyIndex = json.IndexOf(stringSearch);
            if (keyIndex != -1)
            {
                var valueStartIndex = keyIndex + stringSearch.Length;
                var valueEndIndex = json.IndexOf('"', valueStartIndex);
                if (valueEndIndex != -1)
                {
                    return json.Substring(valueStartIndex, valueEndIndex - valueStartIndex);
                }
            }

            // 尝试解析数字或布尔类型的值: "key":value
            var numericSearch = $"\"{key}\":";
            keyIndex = json.IndexOf(numericSearch);
            if (keyIndex != -1)
            {
                var valueStartIndex = keyIndex + numericSearch.Length;
                var valueEndIndex = json.IndexOf(',', valueStartIndex);
                if (valueEndIndex == -1)
                {
                    valueEndIndex = json.IndexOf('}', valueStartIndex);
                }
                if (valueEndIndex != -1)
                {
                    return json.Substring(valueStartIndex, valueEndIndex - valueStartIndex).Trim();
                }
            }

            return null; // 如果两种模式都找不到，则返回 null
        }
    }
}
