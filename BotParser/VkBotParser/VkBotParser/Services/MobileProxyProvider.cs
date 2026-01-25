using VkBotParser.Models;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using System;
using System.Collections.Generic;
using System.Text;

namespace VkBotParser.Services
{
    public class MobileProxyProvider : IProxyProvider
    {
        private readonly MobileProxyConfig _config;

        public MobileProxyProvider(IOptions<MobileProxyConfig> config) => _config = config.Value;

        public bool IsEnabled => _config.Enabled;
        public string Host => _config.Host;
        public int Port => _config.Port;
        public string Username => _config.Username;
        public string Password => _config.Password;
    }
}
