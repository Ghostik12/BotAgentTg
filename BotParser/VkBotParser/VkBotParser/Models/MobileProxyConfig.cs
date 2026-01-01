using System;
using System.Collections.Generic;
using System.Text;

namespace VkBotParser.Models
{
    public class MobileProxyConfig
    {
        public bool Enabled { get; set; } = true;
        public string Host { get; set; } = "";
        public int Port { get; set; }
        public string Username { get; set; } = "";
        public string Password { get; set; } = "";
        public string ChangeIpUrl { get; set; } = "";
        public string CheckIpUrl { get; set; } = "";
        public string BearerToken { get; set; } = "";
    }
}
