using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Text;

namespace VkBotParser.Services
{
    public interface IProxyProvider
    {
        bool IsEnabled { get; }
        string Host { get; }
        int Port { get; }
        string Username { get; }
        string Password { get; }
    }
}
