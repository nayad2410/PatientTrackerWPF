using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Text;
using System.Threading.Tasks;

namespace PatientTrackerWPF.Services
{
    public class EncryptionService
    {
        private readonly string _encryptionKey = "ReconnectApp2024SecureKey32Char!"; // CHANGE THIS!

        public string EncryptText(string plainText)
        {
            if (string.IsNullOrEmpty(plainText)) return plainText;

            using var aes = Aes.Create();
            aes.Key = Encoding.UTF8.GetBytes(_encryptionKey);
            aes.GenerateIV();

            using var encryptor = aes.CreateEncryptor();
            using var ms = new MemoryStream();
            ms.Write(aes.IV, 0, aes.IV.Length);

            using (var cs = new CryptoStream(ms, encryptor, CryptoStreamMode.Write))
            using (var writer = new StreamWriter(cs))
            {
                writer.Write(plainText);
            }

            return Convert.ToBase64String(ms.ToArray());
        }

        public string DecryptText(string cipherText)
        {
            if (string.IsNullOrEmpty(cipherText)) return cipherText;

            try
            {
                var fullCipher = Convert.FromBase64String(cipherText);
                using var aes = Aes.Create();
                aes.Key = Encoding.UTF8.GetBytes(_encryptionKey);

                var iv = new byte[16];
                Array.Copy(fullCipher, 0, iv, 0, 16);
                aes.IV = iv;

                using var decryptor = aes.CreateDecryptor();
                using var ms = new MemoryStream(fullCipher, 16, fullCipher.Length - 16);
                using var cs = new CryptoStream(ms, decryptor, CryptoStreamMode.Read);
                using var reader = new StreamReader(cs);

                return reader.ReadToEnd();
            }
            catch
            {
                return cipherText; // Return original if decryption fails
            }
        }

        public string HashPatientId(string patientId)
        {
            using var sha256 = SHA256.Create();
            var hashedBytes = sha256.ComputeHash(Encoding.UTF8.GetBytes(patientId + "ReconnectSalt2024"));
            return Convert.ToBase64String(hashedBytes);
        }
    }
}
