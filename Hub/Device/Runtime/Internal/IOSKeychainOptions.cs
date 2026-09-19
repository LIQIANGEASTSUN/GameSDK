using System;
using System.Collections.Generic;

namespace GameSDK
{
    /// <summary>
    /// 本次调用解析得到的不可变 Keychain 配置。沿用旧项时必须保持三项定位字符串一致。
    /// 独立项须使用不同的 Account 或 Service，不能仅依赖 Identifier 隔离。
    /// </summary>
    internal sealed class IOSKeychainOptions
    {
        public string Account { get; }
        public string Service { get; }
        public string Identifier { get; }
        public string Description { get; }

        private IOSKeychainOptions(string account, string service, string identifier, string description)
        {
            Account = account;
            Service = service;
            Identifier = identifier;
            Description = description;
        }

        internal static IOSKeychainOptions FromParameters(Dictionary<string, object> parameters)
        {
            if (parameters == null)
                throw new ArgumentNullException(nameof(parameters));

            return new IOSKeychainOptions(
                ReadParameter(parameters, "account", true),
                ReadParameter(parameters, "service", true),
                ReadParameter(parameters, "identifier", true),
                ReadParameter(parameters, "description", false));
        }

        private static string ReadParameter(Dictionary<string, object> parameters, string key, bool required)
        {
            if (!parameters.TryGetValue(key, out object rawValue))
            {
                if (required)
                    throw new ArgumentException("Missing required Keychain parameter: " + key + ".", nameof(parameters));
                return string.Empty;
            }

            if (!(rawValue is string value) || (required && string.IsNullOrWhiteSpace(value)) || value.IndexOf('\0') >= 0)
                throw new ArgumentException("Invalid Keychain parameter: " + key + ".", nameof(parameters));
            return value;
        }
    }
}
