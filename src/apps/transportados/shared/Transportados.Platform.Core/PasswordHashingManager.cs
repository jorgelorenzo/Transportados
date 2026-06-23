using System.Security.Cryptography;
using Microsoft.AspNetCore.Cryptography.KeyDerivation;

namespace Transportados.Platform.Core
{
    public sealed class PasswordHashingManager
    {
        private const int DefaultIterations = 10000;

        private sealed class HashVersion
        {
            public short Version { get; init; }
            public int SaltSize { get; init; }
            public int HashSize { get; init; }
            public KeyDerivationPrf KeyDerivation { get; init; }
        }

        private readonly Dictionary<short, HashVersion> versions = new()
        {
            {
                1, new HashVersion
                {
                    Version = 1,
                    KeyDerivation = KeyDerivationPrf.HMACSHA512,
                    HashSize = 256 / 8,
                    SaltSize = 128 / 8
                }
            }
        };

        private HashVersion DefaultVersion => versions[1];

        public string HashToString(string clearText, int iterations = DefaultIterations)
        {
            var currentVersion = DefaultVersion;
            var saltBytes = RandomNumberGenerator.GetBytes(currentVersion.SaltSize);
            var versionBytes = BitConverter.GetBytes(currentVersion.Version);
            var iterationBytes = BitConverter.GetBytes(iterations);
            var hashBytes = KeyDerivation.Pbkdf2(
                clearText,
                saltBytes,
                currentVersion.KeyDerivation,
                iterations,
                currentVersion.HashSize);

            var resultBytes = new byte[2 + 4 + currentVersion.SaltSize + currentVersion.HashSize];
            Array.Copy(versionBytes, 0, resultBytes, 0, 2);
            Array.Copy(iterationBytes, 0, resultBytes, 2, 4);
            Array.Copy(saltBytes, 0, resultBytes, 6, currentVersion.SaltSize);
            Array.Copy(hashBytes, 0, resultBytes, 6 + currentVersion.SaltSize, currentVersion.HashSize);

            return Convert.ToBase64String(resultBytes);
        }

        public bool Verify(string clearText, string hash)
        {
            var data = Convert.FromBase64String(hash);
            var currentVersion = versions[BitConverter.ToInt16(data, 0)];
            var iteration = BitConverter.ToInt32(data, 2);

            var saltBytes = new byte[currentVersion.SaltSize];
            var storedHashBytes = new byte[currentVersion.HashSize];

            Array.Copy(data, 6, saltBytes, 0, currentVersion.SaltSize);
            Array.Copy(data, 6 + currentVersion.SaltSize, storedHashBytes, 0, currentVersion.HashSize);

            var computedHashBytes = KeyDerivation.Pbkdf2(
                clearText,
                saltBytes,
                currentVersion.KeyDerivation,
                iteration,
                currentVersion.HashSize);

            return CryptographicOperations.FixedTimeEquals(storedHashBytes, computedHashBytes);
        }
    }
}
