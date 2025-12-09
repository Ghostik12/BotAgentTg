using Microsoft.AspNetCore.DataProtection;
using System;
using System.Collections.Generic;
using System.Text;

namespace BotParser.Services
{
    public interface IEncryptionService
    {
        string Encrypt(string plainText);
        string Decrypt(string cipherText);
    }

    public class EncryptionService : IEncryptionService
    {
        private readonly IDataProtector _protector;

        public EncryptionService(IDataProtectionProvider provider)
        {
            _protector = provider.CreateProtector("ProfiRu.UserPassword.v1");
        }

        public string Encrypt(string plainText)
            => string.IsNullOrEmpty(plainText) ? null : _protector.Protect(plainText);

        public string Decrypt(string cipherText)
            => string.IsNullOrEmpty(cipherText) ? null : _protector.Unprotect(cipherText);
    }
}
