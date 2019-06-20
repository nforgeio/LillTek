//-----------------------------------------------------------------------------
// FILE:        Crypto.cs
// CONTRIBUTOR: Jeff Lill
// COPYRIGHT:   Copyright (c) 2005-2015 by Jeffrey Lill.  All rights reserved.
// DESCRIPTION: Misc cryptographic utilities.

using System;
using System.IO;
using System.Text;
using System.Security;
using System.Security.Cryptography;

// $todo(jeff.lill): 
//
// I need to come back and figure out a way to clear RSA
// keys in memory after using them, to prevent these from
// appearing in memory dumps.  This probably means
// recoding to represent all RSA keys using SecureString.

using LillTek.Common;

namespace LillTek.Cryptography
{
    /// <summary>
    /// Cryptographic utilities.
    /// </summary>
    /// <remarks>
    /// The methods in this class are thread-safe unless otherwise noted.
    /// </remarks>
    public static class Crypto
    {
        /// <summary>
        /// Used internally when throwing exceptions for unknown algorithm names.
        /// </summary>
        internal const string UnknownAlgorithm = "Unknown or unimplemented encryption algorithm.";

        /// <summary>
        /// Magic number used to validated encrypted credentials.
        /// </summary>
        internal const int CredentialMagic = 0x1147F123;

        /// <summary>
        /// I/O buffer size for <see cref="StreamEncryptor" /> and <see cref="StreamDecryptor" />.
        /// </summary>
        internal const int StreamBufferSize = 32 * 1024;

        /// <summary>
        /// Corrupt credential error message.
        /// </summary>
        internal const string CorruptCredentialsMsg = "Access denied";

        private static SymmetricKey credentialsKey = new SymmetricKey("aes:kzBt3CuxKINY3pr1/VgThekpPhtkZXWAoYMe57hl5CA=:qDeewNyy+lXNvRxiFH/Evg==");
        private static RNGCryptoServiceProvider rand = new RNGCryptoServiceProvider();

        /// <summary>
        /// Generates a byte array suitable for use in challenge/response authentication.
        /// </summary>
        /// <param name="size">The size of the challenge to be create in bytes.</param>
        /// <returns>The string.</returns>
        public static byte[] GenerateChallenge(int size)
        {
            byte[] challenge;

            if (size <= 0)
                throw new ArgumentException("Challenge size must be positive.");

            challenge = new byte[size];

            lock (rand)
            {
#if !SILVERLIGHT
                rand.GetNonZeroBytes(challenge);
#else
                rand.GetBytes(challenge);
#endif
            }

            return challenge;
        }

        /// <summary>
        /// Returns 4 bytes of salt to be used in cryptographically safe communications.
        /// </summary>
        /// <returns>The salt bytes.</returns>
        public static byte[] GetSalt4()
        {
            var salt = new byte[4];

            lock (rand)
            {
#if !SILVERLIGHT
                rand.GetNonZeroBytes(salt);
#else
                rand.GetBytes(salt);
#endif
            }

            return salt;
        }

        /// <summary>
        /// Returns 8 bytes of salt to be used in cryptographically safe communications.
        /// </summary>
        /// <returns>The salt bytes.</returns>
        public static byte[] GetSalt8()
        {
            var salt = new byte[8];

            lock (rand)
            {
#if !SILVERLIGHT
                rand.GetNonZeroBytes(salt);
#else
                rand.GetBytes(salt);
#endif
            }

            return salt;
        }

        /// <summary>
        /// Copies up to 4 bytes of salt to the buffer at the specified position.
        /// </summary>
        /// <param name="buf">The buffer.</param>
        /// <param name="pos">The position.</param>
        /// <returns>The size data in the buffer after appending the salt.</returns>
        public static int AppendSalt4(byte[] buf, int pos)
        {
            byte[]  salt;
            int     cb;

            cb = buf.Length - pos;
            if (cb <= 0)
                return pos;
            else if (cb > 4)
                cb = 4;

            salt = GetSalt4();
            for (int i = 0; i < cb; i++)
                buf[pos + i] = salt[i];

            return pos + cb;
        }

        /// <summary>
        /// Copies up to 8 bytes of salt to the buffer at the specified position.
        /// </summary>
        /// <param name="buf">The buffer.</param>
        /// <param name="pos">The position.</param>
        /// <returns>The size data in the buffer after appending the salt.</returns>
        public static int AppendSalt8(byte[] buf, int pos)
        {
            byte[]  salt;
            int     cb;

            cb = buf.Length - pos;
            if (cb <= 0)
                return pos;
            else if (cb > 8)
                cb = 8;

            salt = GetSalt8();
            for (int i = 0; i < cb; i++)
                buf[pos + i] = salt[i];

            return pos + cb;
        }

        /// <summary>
        /// Returns a random number of random padding bytes.
        /// </summary>
        /// <param name="maxBytes">The maximum number of bytes to be generated.</param>
        /// <returns>The random padding bytes.</returns>
        public static byte[] GetPadding(int maxBytes)
        {
            var padding = new byte[Helper.RandIndex(maxBytes)];

            if (padding.Length == 0)
                return padding;

            lock (rand)
            {
#if !SILVERLIGHT
                rand.GetNonZeroBytes(padding);
#else
                rand.GetBytes(padding);
#endif
            }

            return padding;
        }

        /// <summary>
        /// Fills a byte array with random values.
        /// </summary>
        /// <param name="padding"></param>
        public static void FillPadding(byte[] padding)
        {
            lock (rand)
            {
#if !SILVERLIGHT
                rand.GetNonZeroBytes(padding);
#else
                rand.GetBytes(padding);
#endif
            }
        }

        /// <summary>
        /// Returns a byte array filled with cryptographically random bytes.
        /// </summary>
        /// <param name="size">The size of the buffer to be returned.</param>
        /// <returns>Byte array filled with random values.</returns>
        public static byte[] Rand(int size)
        {
            var buf = new byte[size];

            lock (rand)
            {
#if !SILVERLIGHT
                rand.GetNonZeroBytes(buf);
#else
                rand.GetBytes(buf);
#endif
            }

            return buf;
        }

        /// <summary>
        /// Returns an encrypted GUID.
        /// </summary>
        public static System.Guid NewSecureGuid()
        {
            return Guid.NewGuid();
        }

        /// <summary>
        /// Returns a pseudo random string suitable for use as a password.
        /// </summary>
        /// <param name="length">The maximum number of characters to return.</param>
        /// <param name="lowerCase"><c>true</c> if only lowercase characters are to be returned.</param>
        public static string GeneratePassword(int length, bool lowerCase)
        {
            byte[]          buf = Rand(length);
            char[]          translate;
            char[]          map;
            StringBuilder   sb;

            if (lowerCase)
            {
                map = new char[10 + 26];
                for (int i = 0; i < 10; i++)
                    map[i] = (char)((int)'0' + i);

                for (int i = 0; i < 26; i++)
                    map[i + 10] = (char)((int)'a' + i);
            }
            else
            {
                map = new char[10 + 26 + 26];
                for (int i = 0; i < 10; i++)
                    map[i] = (char)((int)'0' + i);

                for (int i = 0; i < 26; i++)
                    map[i + 10] = (char)((int)'a' + i);

                for (int i = 0; i < 26; i++)
                    map[i + 10 + 26] = (char)((int)'A' + i);
            }

            translate = new char[256];
            for (int i = 0; i < 256; i++)
                translate[i] = map[i % map.Length];

            sb = new StringBuilder();
            for (int i = 0; i < length; i++)
                sb.Append(translate[buf[i]]);

            return sb.ToString();
        }

        /// <summary>
        /// Generates a symmetric encryption key and initialization vector
        /// of a given size for the specified encryption algorithm.
        /// </summary>
        /// <param name="algorithm">The algorithm name.</param>
        /// <param name="keySize">The desired key size in bits.</param>
        /// <param name="key">Returns as the generated key.</param>
        /// <param name="IV">Returns as the generated initialization vector.</param>
        /// <remarks>
        /// The current supported cross platform encryption algorithms
        /// are: "DES", "RC2", "TripleDES", and "AES" (Rijndael).
        /// </remarks>
        public static void GenerateSymmetricKey(string algorithm, int keySize, out byte[] key, out byte[] IV)
        {
            if (String.Compare(algorithm, CryptoAlgorithm.PlainText, StringComparison.OrdinalIgnoreCase) == 0)
            {
                key = new byte[keySize];
                IV   = new byte[0];
                return;
            }

            var symmetric = EncryptionConfig.CreateSymmetric(algorithm);

            try
            {
                symmetric.KeySize = keySize;
                symmetric.GenerateKey();
                symmetric.GenerateIV();

                key = symmetric.Key;
                IV  = symmetric.IV;
            }
            finally
            {

                symmetric.Clear();
            }
        }

        /// <summary>
        /// Generates a 256-bit AES <see cref="SymmetricKey" />
        /// </summary>
        /// <returns>The <see cref="SymmetricKey" />.</returns>
        public static SymmetricKey GenerateSymmetricKey()
        {
            byte[] key;
            byte[] iv;

            GenerateSymmetricKey(CryptoAlgorithm.AES, 256, out key, out iv);
            return new SymmetricKey(CryptoAlgorithm.AES, key, iv);
        }

        /// <summary>
        /// Generates a <see cref="SymmetricKey" /> of a given size for the 
        /// specified encryption algorithm.
        /// </summary>
        /// <param name="algorithm">The algorithm name.</param>
        /// <param name="keySize">The desired key size in bits.</param>
        /// <returns>The <see cref="SymmetricKey" />.</returns>
        /// <remarks>
        /// The current supported cross platform encryption algorithms
        /// are: "DES", "RC2", "TripleDES", and "AES" (Rijndael).
        /// </remarks>
        public static SymmetricKey GenerateSymmetricKey(string algorithm, int keySize)
        {
            byte[] key;
            byte[] iv;

            GenerateSymmetricKey(algorithm, keySize, out key, out iv);
            return new SymmetricKey(algorithm, key, iv);
        }

        /// <summary>
        /// Generates a symmetric encryption key of the given size and algorithm
        /// from a shared secret and optional cryptographic salt.
        /// </summary>
        /// <param name="algorithm">The algorithm name.</param>
        /// <param name="keySize">The desired key size in bits.</param>
        /// <param name="secret">The shared secret as a byta array.</param>
        /// <param name="salt">An optional array of at least 8 salt bytes or <c>null</c>.</param>
        /// <returns>The <see cref="SymmetricKey" />.</returns>
        /// <exception cref="ArgumentException">Thrown if <paramref name="salt" /> is not <c>null</c> and contains less than 8 bytes of salt.</exception>
        /// <remarks>
        /// <para>
        /// This method is useful for situations where two endpoints wish to communicate
        /// securely when they each know a shared secret such as a password or the hash
        /// of a password.
        /// </para>
        /// <note>
        /// Endpoints that wish to generate a key from a password string should use 
        /// a text <see cref="Encoding"/> to convert the string into bytes.  Note also
        /// that the endpoints will need to agree on the same salt bytes to make sure
        /// that the same key is generated on both ends.
        /// </note>
        /// <note>
        /// If <paramref name="salt" /> is passed as <c>null</c> then the class will use
        /// 8 bytes of built-in salt instead.
        /// </note>
        /// </remarks>
        public static SymmetricKey GenerateSymmetricKeyFromSecret(string algorithm, int keySize, byte[] secret, byte[] salt)
        {
            if (salt != null && salt.Length < 8)
                throw new ArgumentException("At least eight bytes of cryptographic salt is required.", "salt");

            if (String.Compare(algorithm, CryptoAlgorithm.PlainText, StringComparison.OrdinalIgnoreCase) == 0)
                return new SymmetricKey(CryptoAlgorithm.PlainText, new byte[keySize], new byte[0]);

            SymmetricAlgorithm  symmetric = EncryptionConfig.CreateSymmetric(algorithm);
            Rfc2898DeriveBytes  key;

            if (salt == null)
                salt = new byte[] { 0x89, 0x55, 0x71, 0xEA, 0xD3, 0x16, 0x87, 0xFF };

            key = new Rfc2898DeriveBytes(secret, salt, 3);

            try
            {
                symmetric.KeySize = keySize;

                return new SymmetricKey(algorithm, key.GetBytes(symmetric.KeySize / 8), key.GetBytes(symmetric.BlockSize / 8));
            }
            finally
            {
                symmetric.Clear();
            }
        }

        /// <summary>
        /// Encrypts a byte array using the specified encryption algorithm,
        /// key, and initialization vector.
        /// </summary>
        /// <param name="input">The clear text array.</param>
        /// <param name="algorithm">The symmetric algorithm name.</param>
        /// <param name="key">The encryption key.</param>
        /// <param name="IV">The initialization vector.</param>
        /// <returns>The encrypted output.</returns>
        public static byte[] Encrypt(byte[] input, string algorithm, byte[] key, byte[] IV)
        {
            using (BlockEncryptor encryptor = new BlockEncryptor(algorithm, key, IV))
                return encryptor.Encrypt(input);
        }

        /// <summary>
        /// Encrypts a byte array using the specified <see cref="SymmetricKey" />.
        /// </summary>
        /// <param name="input">The clear text array.</param>
        /// <param name="symmetricKey">The symmetric key.</param>
        /// <returns>The encrypted output.</returns>
        public static byte[] Encrypt(byte[] input, SymmetricKey symmetricKey)
        {
            return Encrypt(input, symmetricKey.Algorithm, symmetricKey.Key, symmetricKey.IV);
        }

        /// <summary>
        /// Encrypts a byte array using the specified encryption algorithm,
        /// key, and initialization vector after adding four bytes of
        /// cryptographic salt.
        /// </summary>
        /// <param name="input">The clear text array.</param>
        /// <param name="algorithm">The symmetric algorithm name.</param>
        /// <param name="key">The encryption key.</param>
        /// <param name="IV">The initialization vector.</param>
        /// <returns>The encrypted output.</returns>
        /// <remarks>
        /// <note>
        /// This method returns an zero length result if the input 
        /// array has zero length.
        /// </note>
        /// </remarks>
        public static byte[] EncryptWithSalt4(byte[] input, string algorithm, byte[] key, byte[] IV)
        {
            if (input.Length == 0)
                return new byte[0];

            using (BlockEncryptor encryptor = new BlockEncryptor(algorithm, key, IV))
                return encryptor.Encrypt(Helper.Concat(Crypto.GetSalt4(), input));
        }

        /// <summary>
        /// Encrypts a byte array using the specified <see cref="SymmetricKey" /> after
        /// adding four bytes of cryptographic salt.
        /// </summary>
        /// <param name="input">The clear text array.</param>
        /// <param name="symmetricKey">The symmetric key.</param>
        /// <returns>The encrypted output.</returns>
        public static byte[] EncryptWithSalt4(byte[] input, SymmetricKey symmetricKey)
        {
            return EncryptWithSalt4(input, symmetricKey.Algorithm, symmetricKey.Key, symmetricKey.IV);
        }

        /// <summary>
        /// Encrypts a byte array using the specified encryption algorithm,
        /// key, and initialization vector after adding eight bytes of
        /// cryptographic salt.
        /// </summary>
        /// <param name="input">The clear text array.</param>
        /// <param name="algorithm">The symmetric algorithm name.</param>
        /// <param name="key">The encryption key.</param>
        /// <param name="IV">The initialization vector.</param>
        /// <returns>The encrypted output.</returns>
        /// <remarks>
        /// <note>
        /// This method returns an zero length result if the input 
        /// array has zero length.
        /// </note>
        /// </remarks>
        public static byte[] EncryptWithSalt8(byte[] input, string algorithm, byte[] key, byte[] IV)
        {
            if (input.Length == 0)
                return new byte[0];

            using (BlockEncryptor encryptor = new BlockEncryptor(algorithm, key, IV))
                return encryptor.Encrypt(Helper.Concat(Crypto.GetSalt8(), input));
        }

        /// <summary>
        /// Encrypts a byte array using the specified <see cref="SymmetricKey" /> after
        /// adding eight bytes of cryptographic salt.
        /// </summary>
        /// <param name="input">The clear text array.</param>
        /// <param name="symmetricKey">The symmetric key.</param>
        /// <returns>The encrypted output.</returns>
        public static byte[] EncryptWithSalt8(byte[] input, SymmetricKey symmetricKey)
        {
            return EncryptWithSalt8(input, symmetricKey.Algorithm, symmetricKey.Key, symmetricKey.IV);
        }

        /// <summary>
        /// Decrypts a byte array using the specified encryption algorithm,
        /// key, and initialization vector.
        /// </summary>
        /// <param name="input">The encrypted array,</param>
        /// <param name="algorithm">The symmetric algorithm name.</param>
        /// <param name="key">The encryption key.</param>
        /// <param name="IV">The initialization vector.</param>
        /// <returns>The decrypted output.</returns>
        public static byte[] Decrypt(byte[] input, string algorithm, byte[] key, byte[] IV)
        {
            if (input.Length == 0)
                return new byte[0];

            using (BlockDecryptor decryptor = new BlockDecryptor(algorithm, key, IV))
                return decryptor.Decrypt(input);
        }

        /// <summary>
        /// Decrypts a byte array using the specified <see cref="SymmetricKey" />.
        /// </summary>
        /// <param name="input">The encrypted array.</param>
        /// <param name="symmetricKey">The symmetric key.</param>
        /// <returns>The encrypted output.</returns>
        public static byte[] Decrypt(byte[] input, SymmetricKey symmetricKey)
        {
            return Decrypt(input, symmetricKey.Algorithm, symmetricKey.Key, symmetricKey.IV);
        }

        /// <summary>
        /// Decrypts a byte array with four bytes of cryptographic salt using 
        /// the specified encryption algorithm, key, and initialization vector.
        /// </summary>
        /// <param name="input">The encrypted array,</param>
        /// <param name="algorithm">The symmetric algorithm name.</param>
        /// <param name="key">The encryption key.</param>
        /// <param name="IV">The initialization vector.</param>
        /// <returns>The decrypted output.</returns>
        /// <remarks>
        /// <note>
        /// This method returns an zero length result if the input 
        /// array has zero length.
        /// </note>
        /// </remarks>
        public static byte[] DecryptWithSalt4(byte[] input, string algorithm, byte[] key, byte[] IV)
        {
            if (input.Length == 0)
                return new byte[0];
            else if (input.Length < 4)
                throw new ArgumentException("Block does not contain 4 bytes of salt.");

            using (BlockDecryptor decryptor = new BlockDecryptor(algorithm, key, IV))
                return Helper.Extract(decryptor.Decrypt(input), 4);
        }

        /// <summary>
        /// Encrypts a byte array with four bytes of cryptogrpahic salt
        /// using the specified <see cref="SymmetricKey" />.
        /// </summary>
        /// <param name="input">The encrypted array.</param>
        /// <param name="symmetricKey">The symmetric key.</param>
        /// <returns>The encrypted output.</returns>
        public static byte[] DecryptWithSalt4(byte[] input, SymmetricKey symmetricKey)
        {
            return DecryptWithSalt4(input, symmetricKey.Algorithm, symmetricKey.Key, symmetricKey.IV);
        }

        /// <summary>
        /// Decrypts a byte array with eight bytes of cryptographic salt using 
        /// the specified encryption algorithm, key, and initialization vector.
        /// </summary>
        /// <param name="input">The encrypted array,</param>
        /// <param name="algorithm">The symmetric algorithm name.</param>
        /// <param name="key">The encryption key.</param>
        /// <param name="IV">The initialization vector.</param>
        /// <returns>The decrypted output.</returns>
        /// <remarks>
        /// <note>
        /// This method returns an zero length result if the input 
        /// array has zero length.
        /// </note>
        /// </remarks>
        public static byte[] DecryptWithSalt8(byte[] input, string algorithm, byte[] key, byte[] IV)
        {
            if (input.Length == 0)
                return new byte[0];
            else if (input.Length < 8)
                throw new ArgumentException("Block does not contain 8 bytes of salt.");

            using (BlockDecryptor decryptor = new BlockDecryptor(algorithm, key, IV))
                return Helper.Extract(decryptor.Decrypt(input), 8);
        }

        /// <summary>
        /// Encrypts a byte array with eight bytes of cryptogrpahic salt
        /// using the specified <see cref="SymmetricKey" />.
        /// </summary>
        /// <param name="input">The encrypted array.</param>
        /// <param name="symmetricKey">The symmetric key.</param>
        /// <returns>The encrypted output.</returns>
        public static byte[] DecryptWithSalt8(byte[] input, SymmetricKey symmetricKey)
        {
            return DecryptWithSalt8(input, symmetricKey.Algorithm, symmetricKey.Key, symmetricKey.IV);
        }

        /// <summary>
        /// Encodes a string as UTF8 and encryptes it using the specified 
        /// encryption algorithm, key, and initialization vector.
        /// </summary>
        /// <param name="input">The clear text string.</param>
        /// <param name="algorithm">The symmetric algorithm name.</param>
        /// <param name="key">The encryption key.</param>
        /// <param name="IV">The initialization vector.</param>
        /// <returns>The encrypted output.</returns>
        public static byte[] EncryptString(string input, string algorithm, byte[] key, byte[] IV)
        {
            using (BlockEncryptor encryptor = new BlockEncryptor(algorithm, key, IV))
                return encryptor.Encrypt(Helper.ToUTF8(input));
        }

        /// <summary>
        /// Encrypts a string using the specified <see cref="SymmetricKey" />.
        /// </summary>
        /// <param name="input">The clear text string.</param>
        /// <param name="symmetricKey">The symmetric key.</param>
        /// <returns>The encrypted output.</returns>
        public static byte[] EncryptString(string input, SymmetricKey symmetricKey)
        {
            return EncryptString(input, symmetricKey.Algorithm, symmetricKey.Key, symmetricKey.IV);
        }

        /// <summary>
        /// Encodes a string as UTF8 and encryptes it using the specified 
        /// encryption algorithm, key, and initialization vector after 
        /// adding four bytes of cryptographic salt.
        /// </summary>
        /// <param name="input">The clear text string.</param>
        /// <param name="algorithm">The symmetric algorithm name.</param>
        /// <param name="key">The encryption key.</param>
        /// <param name="IV">The initialization vector.</param>
        /// <returns>The encrypted output.</returns>
        public static byte[] EncryptStringWithSalt4(string input, string algorithm, byte[] key, byte[] IV)
        {
            using (BlockEncryptor encryptor = new BlockEncryptor(algorithm, key, IV))
                return encryptor.Encrypt(Helper.Concat(Crypto.GetSalt4(), Helper.ToUTF8(input)));
        }

        /// <summary>
        /// Encrypts a string with four bytes of cryptogrpahic salt
        /// using the specified <see cref="SymmetricKey" />.
        /// </summary>
        /// <param name="input">The clear text string.</param>
        /// <param name="symmetricKey">The symmetric key.</param>
        /// <returns>The encrypted output.</returns>
        public static byte[] EncryptStringWithSalt4(string input, SymmetricKey symmetricKey)
        {
            return EncryptStringWithSalt4(input, symmetricKey.Algorithm, symmetricKey.Key, symmetricKey.IV);
        }

        /// <summary>
        /// Encodes a string as UTF8 and encryptes it using the specified 
        /// encryption algorithm, key, and initialization vector after 
        /// adding eight bytes of cryptographic salt.
        /// </summary>
        /// <param name="input">The clear text string.</param>
        /// <param name="algorithm">The symmetric algorithm name.</param>
        /// <param name="key">The encryption key.</param>
        /// <param name="IV">The initialization vector.</param>
        /// <returns>The encrypted output.</returns>
        public static byte[] EncryptStringWithSalt8(string input, string algorithm, byte[] key, byte[] IV)
        {
            using (BlockEncryptor encryptor = new BlockEncryptor(algorithm, key, IV))
                return encryptor.Encrypt(Helper.Concat(Crypto.GetSalt8(), Helper.ToUTF8(input)));
        }

        /// <summary>
        /// Encrypts a string with eight bytes of cryptogrpahic salt
        /// using the specified <see cref="SymmetricKey" />.
        /// </summary>
        /// <param name="input">The clear text string.</param>
        /// <param name="symmetricKey">The symmetric key.</param>
        /// <returns>The encrypted output.</returns>
        public static byte[] EncryptStringWithSalt8(string input, SymmetricKey symmetricKey)
        {
            return EncryptStringWithSalt8(input, symmetricKey.Algorithm, symmetricKey.Key, symmetricKey.IV);
        }

        /// <summary>
        /// Decrypts a byte array using the specified encryption algorithm,
        /// key, and initialization vector and then converts it to a string
        /// using UTF-8 encoding.
        /// </summary>
        /// <param name="input">The encrypted string,</param>
        /// <param name="algorithm">The symmetric algorithm name.</param>
        /// <param name="key">The encryption key.</param>
        /// <param name="IV">The initialization vector.</param>
        /// <returns>The decrypted string.</returns>
        public static string DecryptString(byte[] input, string algorithm, byte[] key, byte[] IV)
        {
            using (BlockDecryptor decryptor = new BlockDecryptor(algorithm, key, IV))
                return Helper.FromUTF8(decryptor.Decrypt(input));
        }

        /// <summary>
        /// Decrypts a string using the specified <see cref="SymmetricKey" />.
        /// </summary>
        /// <param name="input">The encrypted string.</param>
        /// <param name="symmetricKey">The symmetric key.</param>
        /// <returns>The encrypted output.</returns>
        public static string DecryptString(byte[] input, SymmetricKey symmetricKey)
        {
            return DecryptString(input, symmetricKey.Algorithm, symmetricKey.Key, symmetricKey.IV);
        }

        /// <summary>
        /// Decrypts a byte array with four bytes of cryptographic salt using
        /// the specified encryption algorithm, key, and initialization vector 
        /// and then converts it to a string using UTF-8 encoding.
        /// </summary>
        /// <param name="input">The encrypted string,</param>
        /// <param name="algorithm">The symmetric algorithm name.</param>
        /// <param name="key">The encryption key.</param>
        /// <param name="IV">The initialization vector.</param>
        /// <returns>The decrypted string.</returns>
        public static string DecryptStringWithSalt4(byte[] input, string algorithm, byte[] key, byte[] IV)
        {
            using (BlockDecryptor decryptor = new BlockDecryptor(algorithm, key, IV))
                return Helper.FromUTF8(Helper.Extract(decryptor.Decrypt(input), 4));
        }

        /// <summary>
        /// Decrypts a string with four bytes of cryptogrpahic salt using
        /// the specified <see cref="SymmetricKey" />.
        /// </summary>
        /// <param name="input">The encrypted string.</param>
        /// <param name="symmetricKey">The symmetric key.</param>
        /// <returns>The encrypted output.</returns>
        public static string DecryptStringWithSalt4(byte[] input, SymmetricKey symmetricKey)
        {
            return DecryptStringWithSalt4(input, symmetricKey.Algorithm, symmetricKey.Key, symmetricKey.IV);
        }

        /// <summary>
        /// Decrypts a byte array with eight bytes of cryptographic salt using
        /// the specified encryption algorithm, key, and initialization vector 
        /// and then converts it to a string using UTF-8 encoding.
        /// </summary>
        /// <param name="input">The encrypted string,</param>
        /// <param name="algorithm">The symmetric algorithm name.</param>
        /// <param name="key">The encryption key.</param>
        /// <param name="IV">The initialization vector.</param>
        /// <returns>The decrypted string.</returns>
        public static string DecryptStringWithSalt8(byte[] input, string algorithm, byte[] key, byte[] IV)
        {
            using (BlockDecryptor decryptor = new BlockDecryptor(algorithm, key, IV))
                return Helper.FromUTF8(Helper.Extract(decryptor.Decrypt(input), 8));
        }

        /// <summary>
        /// Decrypts a string with eight bytes of cryptogrpahic salt using
        /// the specified <see cref="SymmetricKey" />.
        /// </summary>
        /// <param name="input">The encrypted string.</param>
        /// <param name="symmetricKey">The symmetric key.</param>
        /// <returns>The encrypted output.</returns>
        public static string DecryptStringWithSalt8(byte[] input, SymmetricKey symmetricKey)
        {
            return DecryptStringWithSalt8(input, symmetricKey.Algorithm, symmetricKey.Key, symmetricKey.IV);
        }

        /// <summary>
        /// The <see cref="SymmetricKey" /> used to casually encrypt authentication <see cref="Credentials" />.
        /// </summary>
        public static SymmetricKey CredentialsKey
        {
            get { return credentialsKey; }
            set { credentialsKey = value; }
        }

        /// <summary>
        /// Casually encrypts authentication <see cref="Credentials" /> into a byte
        /// array using using the <see cref="CredentialsKey" />.
        /// </summary>
        /// <param name="credentials">The <see cref="Credentials" />.</param>
        /// <returns>The encrypted credentials.</returns>
        /// <remarks>
        /// <note>
        /// This method is designed to provide a low level of security during development
        /// and testing, when the overhead of configuring SSL certificates is not worth
        /// the trouble.  Do not rely on this method as your only mechanism for securing 
        /// credentials during transmission in production environments.
        /// </note>
        /// </remarks>
        public static byte[] EncryptCredentials(Credentials credentials)
        {
            using (var ms = new EnhancedMemoryStream(256))
            {
                ms.WriteInt32(CredentialMagic);
                ms.WriteString16(credentials.Realm);
                ms.WriteString16(credentials.Account);
                ms.WriteString16(credentials.Password);

                return EncryptWithSalt8(ms.ToArray(), credentialsKey);
            }
        }

        /// <summary>
        /// Casually decrypts authentication <see cref="Credentials" /> from a
        /// byte array using the <see cref="CredentialsKey" />.
        /// </summary>
        /// <param name="encrypted">The encrypted credential bytes.</param>
        /// <returns>The decrypted <see cref="Credentials" />.</returns>
        /// <remarks>
        /// <note>
        /// This method is designed to provide a low level of security during development
        /// and testing, when the overhead of configuring SSL certificates is not worth
        /// the trouble.  Do not rely on this method as your only mechanism for securing 
        /// credentials during transmission in production environments.
        /// </note>
        /// </remarks>
        /// <exception cref="SecurityException">Thrown if the credentials are corrupt.</exception>
        public static Credentials DecryptCredentials(byte[] encrypted)
        {
            try
            {
                var decrypted = DecryptWithSalt8(encrypted, credentialsKey);

                using (var ms = new EnhancedMemoryStream(decrypted))
                {
                    string realm;
                    string account;
                    string password;

                    if (ms.ReadInt32() != CredentialMagic)
                        throw new SecurityException(CorruptCredentialsMsg);

                    realm = ms.ReadString16();
                    account = ms.ReadString16();
                    password = ms.ReadString16();

                    return new Credentials(realm, account, password);
                }
            }
            catch (Exception e)
            {
                throw new SecurityException(CorruptCredentialsMsg, e);
            }
        }

        /// <summary>
        /// Casually encrypts the old and new password to be used to during
        /// a change password operation.
        /// </summary>
        /// <param name="originalPassword">The original password.</param>
        /// <param name="newPassword">The new password.</param>
        /// <returns>The encrypted password change parameters.</returns>
        /// <remarks>
        /// <note>
        /// This method is designed to provide a low level of security during development
        /// and testing, when the overhead of configuring SSL certificates is not worth
        /// the trouble.  Do not rely on this method as your only mechanism for securing 
        /// credentials during transmission in production environments.
        /// </note>
        /// </remarks>
        public static byte[] EncryptPasswordChange(string originalPassword, string newPassword)
        {
            using (var ms = new EnhancedMemoryStream(256))
            {
                ms.WriteInt32(CredentialMagic);
                ms.WriteString16(originalPassword);
                ms.WriteString16(newPassword);

                return EncryptWithSalt8(ms.ToArray(), credentialsKey);
            }
        }

        /// <summary>
        /// Decrypts the password change parameters encrypted by <see cref="EncryptPasswordChange" />.
        /// </summary>
        /// <param name="encryptedPasswords">The encryoted password change parameters.</param>
        /// <param name="originalPassword">Returns as the original password.</param>
        /// <param name="newPassword">Returns as the new password.</param>
        /// <remarks>
        /// <note>
        /// This method is designed to provide a low level of security during development
        /// and testing, when the overhead of configuring SSL certificates is not worth
        /// the trouble.  Do not rely on this method as your only mechanism for securing 
        /// credentials during transmission in production environments.
        /// </note>
        /// </remarks>
        /// <exception cref="SecurityException">Thrown if the parameters are corrupt.</exception>
        public static void DecryptPasswordChange(byte[] encryptedPasswords, out string originalPassword, out string newPassword)
        {
            try
            {
                var decrypted = DecryptWithSalt8(encryptedPasswords, credentialsKey);

                using (var ms = new EnhancedMemoryStream(decrypted))
                {
                    if (ms.ReadInt32() != CredentialMagic)
                        throw new SecurityException(CorruptCredentialsMsg);

                    originalPassword = ms.ReadString16();
                    newPassword = ms.ReadString16();
                }
            }
            catch (Exception e)
            {
                throw new SecurityException(CorruptCredentialsMsg, e);
            }
        }
    }
}
