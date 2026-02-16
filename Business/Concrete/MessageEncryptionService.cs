using Business.Abstract;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using System;
using System.Security.Cryptography;
using System.Text;

namespace Business.Concrete
{
    public class MessageEncryptionService : IMessageEncryptionService
    {
        private readonly byte[] _key;
        private readonly ILogger<MessageEncryptionService> _logger;
        private readonly bool _isEnabled;

        public MessageEncryptionService(
            IConfiguration configuration,
            ILogger<MessageEncryptionService> logger)
        {
            _logger = logger;

            var keyBase64 = configuration["Encryption:MessageKey"];
            if (string.IsNullOrEmpty(keyBase64))
            {
                _logger.LogWarning("Encryption:MessageKey is not configured. Message encryption is disabled.");
                _isEnabled = false;
                _key = Array.Empty<byte>();
                return;
            }

            _key = Convert.FromBase64String(keyBase64);
            if (_key.Length != 32)
            {
                _logger.LogError("Encryption key must be 256 bits (32 bytes). Got {Length} bytes. Encryption disabled.", _key.Length);
                _isEnabled = false;
                _key = Array.Empty<byte>();
                return;
            }

            _isEnabled = true;
        }

        public string Encrypt(string plaintext)
        {
            if (string.IsNullOrEmpty(plaintext) || !_isEnabled)
                return plaintext;

            using var aes = Aes.Create();
            aes.Key = _key;
            aes.GenerateIV(); // Her mesaj için unique IV

            using var encryptor = aes.CreateEncryptor();
            var plaintextBytes = Encoding.UTF8.GetBytes(plaintext);
            var cipherBytes = encryptor.TransformFinalBlock(plaintextBytes, 0, plaintextBytes.Length);

            // Format: Base64( IV[16] + Ciphertext )
            var result = new byte[aes.IV.Length + cipherBytes.Length];
            Buffer.BlockCopy(aes.IV, 0, result, 0, aes.IV.Length);
            Buffer.BlockCopy(cipherBytes, 0, result, aes.IV.Length, cipherBytes.Length);

            return Convert.ToBase64String(result);
        }

        public string? Decrypt(string? ciphertext)
        {
            if (string.IsNullOrEmpty(ciphertext) || !_isEnabled)
                return ciphertext;

            try
            {
                var fullCipher = Convert.FromBase64String(ciphertext);

                // IV minimum 16 byte + en az 1 byte ciphertext olmalı
                if (fullCipher.Length < 17)
                    return ciphertext; // Şifrelenmemiş eski mesaj olabilir

                using var aes = Aes.Create();
                aes.Key = _key;

                // İlk 16 byte IV
                var iv = new byte[16];
                Buffer.BlockCopy(fullCipher, 0, iv, 0, 16);
                aes.IV = iv;

                // Geri kalan ciphertext
                var cipher = new byte[fullCipher.Length - 16];
                Buffer.BlockCopy(fullCipher, 16, cipher, 0, cipher.Length);

                using var decryptor = aes.CreateDecryptor();
                var plainBytes = decryptor.TransformFinalBlock(cipher, 0, cipher.Length);

                return Encoding.UTF8.GetString(plainBytes);
            }
            catch (FormatException)
            {
                // Base64 değilse şifrelenmemiş eski mesajdır, olduğu gibi döndür
                return ciphertext;
            }
            catch (CryptographicException)
            {
                // Şifre çözme hatası - eski/şifrelenmemiş mesaj olabilir
                _logger.LogWarning("Failed to decrypt message. Returning as-is (possibly unencrypted legacy message).");
                return ciphertext;
            }
        }
    }
}
