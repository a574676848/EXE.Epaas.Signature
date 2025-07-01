using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Cryptography;
using System.Text;

namespace EXE.Epaas.Signature
{
    /// <summary>
    /// 签名工具类
    /// </summary>
    public static class SignatureUtil
    {
        /// <summary>
        /// 将参与签名的请求头拼接成字符串
        /// </summary>
        /// <param name="signHeaderMap">按字典顺序排序的签名请求头</param>
        /// <returns>拼接后的字符串</returns>
        public static string BuildHeaderString(SortedDictionary<string, string> signHeaderMap)
        {
            var sb = new StringBuilder();
            foreach (var entry in signHeaderMap)
            {
                sb.Append(entry.Key).Append(":").Append(entry.Value ?? string.Empty).Append("\n");
            }
            return sb.Length > 0 ? sb.ToString(0, sb.Length - 1) : string.Empty;
        }

        /// <summary>
        /// 对 POST 请求体进行处理
        /// </summary>
        /// <param name="body">请求体字符串</param>
        /// <returns>处理后的字符串</returns>
        public static string ProcessBody(string body)
        {
            if (string.IsNullOrEmpty(body))
            {
                return string.Empty;
            }

            using (var md5 = MD5.Create())
            {
                var bodyBytes = Encoding.UTF8.GetBytes(body);
                var hashBytes = md5.ComputeHash(bodyBytes);

                var sb = new StringBuilder();
                foreach (var b in hashBytes)
                {
                    sb.Append(b.ToString("x2"));
                }
                var hexString = sb.ToString();

                var plainTextBytes = Encoding.UTF8.GetBytes(hexString);
                return Convert.ToBase64String(plainTextBytes);
            }
        }

        /// <summary>
        /// 拼接所有需要签名的部分
        /// </summary>
        /// <param name="method">请求方法 (GET, POST, etc.)</param>
        /// <param name="uri">请求路径</param>
        /// <param name="queryParam">GET 查询参数字符串</param>
        /// <param name="headerStr">处理过的请求头字符串</param>
        /// <param name="bodyStr">处理过的请求体字符串</param>
        /// <param name="secretKey">密钥</param>
        /// <returns>待签名的字符串</returns>
        public static string Join(string method, string uri, string queryParam, string headerStr, string bodyStr, string secretKey)
        {
            var parts = new List<string>
            {
                method.ToUpper(),
                uri
            };

            var paramAndHeaderParts = new List<string>();
            if (!string.IsNullOrEmpty(queryParam))
            {
                paramAndHeaderParts.Add(queryParam);
            }
            if (!string.IsNullOrEmpty(headerStr))
            {
                paramAndHeaderParts.Add(headerStr);
            }
            if (!string.IsNullOrEmpty(bodyStr))
            {
                paramAndHeaderParts.Add(bodyStr);
            }
            
            parts.Add(string.Join("\n", paramAndHeaderParts));
            parts.Add(secretKey);

            return string.Join("\n", parts);
        }

        /// <summary>
        /// 生成最终的 MD5 签名
        /// </summary>
        /// <param name="stringToSign">待签名的字符串</param>
        /// <returns>MD5 签名 (十六进制)</returns>
        public static string GenerateMd5Sign(string stringToSign)
        {
            using (var md5 = MD5.Create())
            {
                var inputBytes = Encoding.UTF8.GetBytes(stringToSign);
                var hashBytes = md5.ComputeHash(inputBytes);

                var sb = new StringBuilder();
                for (int i = 0; i < hashBytes.Length; i++)
                {
                    sb.Append(hashBytes[i].ToString("x2"));
                }
                return sb.ToString();
            }
        }
    }
}
