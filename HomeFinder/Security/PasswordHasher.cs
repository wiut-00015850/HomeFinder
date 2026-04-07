using System;
using System.Globalization;
using System.Security.Cryptography;
using System.Text;

namespace HomeFinder.Security
{
    /// <summary>
    /// Хеширование паролей.
    ///
    /// Новый формат (рекомендуемый): PBKDF2 (HMAC-SHA256) + уникальная соль на пользователя.
    /// Строка хранения:
    /// PBKDF2$SHA256$<iterations>$<saltBase64>$<subkeyBase64>
    ///
    /// Поддержка старого формата оставлена для плавной миграции:
    /// - SHA2-512 в HEX (128 символов)
    /// - "голый" пароль (если внезапно так лежит в БД)
    /// </summary>
    public static class PasswordHasher
    {
        private const int SaltSizeBytes = 16;     // 128-bit
        private const int SubkeySizeBytes = 32;   // 256-bit
        private const int DefaultIterations = 150_000;
        private const string Scheme = "PBKDF2";
        private const string Prf = "SHA256";

        public static string Hash(string password)
        {
            if (password == null) throw new ArgumentNullException(nameof(password));

            var salt = RandomNumberGenerator.GetBytes(SaltSizeBytes);
            var subkey = Rfc2898DeriveBytes.Pbkdf2(
                password,
                salt,
                DefaultIterations,
                HashAlgorithmName.SHA256,
                SubkeySizeBytes);

            return string.Create(
                CultureInfo.InvariantCulture,
                $"{Scheme}${Prf}${DefaultIterations}${Convert.ToBase64String(salt)}${Convert.ToBase64String(subkey)}");
        }

        /// <summary>
        /// Проверяет пароль и при необходимости возвращает upgradedHash для перезаписи в БД.
        /// </summary>
        public static bool VerifyAndUpgrade(string password, string stored, out string? upgradedHash)
        {
            upgradedHash = null;

            if (string.IsNullOrWhiteSpace(password) || string.IsNullOrWhiteSpace(stored))
                return false;

            // Новый формат: PBKDF2$SHA256$...
            if (TryVerifyPbkdf2(stored, password, out bool needRehash))
            {
                if (needRehash)
                    upgradedHash = Hash(password);

                return true;
            }

            // Старый формат: SHA2-512 HEX (128 символов) или открытый текст.
            if (VerifyLegacy(password, stored))
            {
                upgradedHash = Hash(password);
                return true;
            }

            return false;
        }

        public static bool Verify(string password, string stored)
        {
            return VerifyAndUpgrade(password, stored, out _);
        }

        private static bool TryVerifyPbkdf2(string stored, string password, out bool needRehash)
        {
            needRehash = false;

            // PBKDF2$SHA256$150000$<saltB64>$<subkeyB64>
            var parts = stored.Split('$', StringSplitOptions.TrimEntries);
            if (parts.Length != 5) return false;
            if (!string.Equals(parts[0], Scheme, StringComparison.Ordinal)) return false;
            if (!string.Equals(parts[1], Prf, StringComparison.OrdinalIgnoreCase)) return false;
            if (!int.TryParse(parts[2], NumberStyles.None, CultureInfo.InvariantCulture, out var iterations)) return false;

            byte[] salt;
            byte[] expectedSubkey;
            try
            {
                salt = Convert.FromBase64String(parts[3]);
                expectedSubkey = Convert.FromBase64String(parts[4]);
            }
            catch
            {
                return false;
            }

            if (salt.Length < 8 || expectedSubkey.Length < 16)
                return false;

            var actualSubkey = Rfc2898DeriveBytes.Pbkdf2(
                password,
                salt,
                iterations,
                HashAlgorithmName.SHA256,
                expectedSubkey.Length);

            var ok = CryptographicOperations.FixedTimeEquals(actualSubkey, expectedSubkey);
            if (!ok) return true; // формат распознан, но пароль неверный

            // Политика обновления параметров со временем
            if (iterations < DefaultIterations || salt.Length != SaltSizeBytes || expectedSubkey.Length != SubkeySizeBytes)
                needRehash = true;

            return true;
        }

        private static bool VerifyLegacy(string password, string stored)
        {
            // 1) Если в БД лежит "голый" пароль (как было сделано раньше в AccountController)
            if (string.Equals(password, stored, StringComparison.Ordinal))
                return true;

            // 2) Старый SHA2-512 HEX (без соли).
            // Алгоритм совпадает с прежним Hash(): SHA512(ASCII(password)) -> HEX.
            if (stored.Length != 128)
                return false;

            for (int i = 0; i < stored.Length; i++)
            {
                char c = stored[i];
                bool isHex = (c >= '0' && c <= '9') ||
                            (c >= 'a' && c <= 'f') ||
                            (c >= 'A' && c <= 'F');
                if (!isHex) return false;
            }

            using var sha = SHA512.Create();
            var bytes = Encoding.ASCII.GetBytes(password);
            var hash = sha.ComputeHash(bytes);

            var sb = new StringBuilder(hash.Length * 2);
            foreach (var b in hash)
                sb.Append(b.ToString("X2"));

            return string.Equals(sb.ToString(), stored, StringComparison.OrdinalIgnoreCase);
        }
    }
}

