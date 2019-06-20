//-----------------------------------------------------------------------------
// FILE:        KeyChain.cs
// CONTRIBUTOR: Jeff Lill
// COPYRIGHT:   Copyright (c) 2005-2015 by Jeffrey Lill.  All rights reserved.
// DESCRIPTION: Holds a collection of private RSA keys.

using System;
using System.IO;
using System.Collections.Generic;
using System.Text;
using System.Security.Cryptography;

using LillTek.Common;

namespace LillTek.Cryptography
{
    /// <summary>
    /// Holds a collection of private RSA keys.
    /// </summary>
    /// <remarks>
    /// <para>
    /// This class is typically used with <see cref="SecureFile" /> to decrypt a
    /// file that was encrypted with one of a set of known public keys.
    /// </para>
    /// </remarks>
    /// <threadsafety instance="true" />
    public sealed class KeyChain
    {
        private const int  Magic = 0x51239961;      // Magic number used to verify a decrypted keychain

        private object                      syncLock = new object();
        private Dictionary<string, string>  keys;   // The collection of private keys indexed by normalized public key.

        /// <summary>
        /// Constructs an empty key chain.
        /// </summary>
        public KeyChain()
        {
            this.keys = new Dictionary<string, string>();
        }

        /// <summary>
        /// Constructs a <see cref="KeyChain" /> from an array of RSA key strings.
        /// </summary>
        /// <param name="keys">The RSA keys.</param>
        public KeyChain(params string[] keys)
            : this()
        {
            foreach (string key in keys)
                Add(key);
        }

        /// <summary>
        /// Constructs a key chain by decrypting bytes returned by a previous
        /// call to <see cref="Encrypt" />.
        /// </summary>
        /// <param name="key">The <see cref="SymmetricKey" /> to be used for decrypting.</param>
        /// <param name="encrypted">The encrypted key chain.</param>
        /// <exception cref="CryptographicException">Thrown if the decrypted key chain is malformed.</exception>
        public KeyChain(SymmetricKey key, byte[] encrypted)
        {
            try
            {
                using (var ms = new EnhancedMemoryStream(Crypto.Decrypt(encrypted, key)))
                {
                    if (ms.ReadInt32() != Magic)
                        throw new Exception();

                    int count;

                    count = ms.ReadInt32();
                    keys = new Dictionary<string, string>(count);

                    for (int i = 0; i < count; i++)
                        Add(ms.ReadString16());
                }
            }
            catch (Exception e)
            {
                throw new CryptographicException("Key chain is malformed.", e);
            }
        }

        /// <summary>
        /// Normalizes a key by removing all whitespace.
        /// </summary>
        /// <param name="key">The input key XML.</param>
        /// <returns>The normalized XML.</returns>
        private static string Normalize(string key)
        {
            var sb = new StringBuilder(key.Length);

            for (int i = 0; i < key.Length; i++)
                if (!Char.IsWhiteSpace(key[i]))
                    sb.Append(key[i]);

            return sb.ToString();
        }

        /// <summary>
        /// Adds an RSA private key to the key chain.
        /// </summary>
        /// <param name="privateKey">The private key encoded as XML.</param>
        /// <exception cref="CryptographicException">Thrown if the key passed is not a private key.</exception>
        public void Add(string privateKey)
        {
            var publicKey = Normalize(AsymmetricCrypto.GetPublicKey(CryptoAlgorithm.RSA, privateKey));

            if (!AsymmetricCrypto.IsXmlPrivateKey(CryptoAlgorithm.RSA, privateKey))
                throw new CryptographicException("Key XML passed is not a private key.");

            lock (syncLock)
                keys[publicKey] = privateKey;
        }

        /// <summary>
        /// Removes a private key from the chain if it is present.
        /// </summary>
        /// <param name="privateKey">The private key to be removed.</param>
        public void Remove(string privateKey)
        {
            var publicKey = Normalize(AsymmetricCrypto.GetPublicKey(CryptoAlgorithm.RSA, privateKey));

            lock (syncLock)
            {

                if (keys.ContainsKey(publicKey))
                    keys.Remove(publicKey);
            }
        }

        /// <summary>
        /// Returns the RSA private key from the key chain matching the
        /// public or private key.
        /// </summary>
        /// <param name="key">The public or private key formatted as XML.</param>
        /// <returns>The private key formatted as XML.</returns>
        /// <exception cref="CryptographicException">Thrown if a matching key could not be found.</exception>
        public string GetPrivateKey(string key)
        {

            string publicKey = Normalize(AsymmetricCrypto.GetPublicKey(CryptoAlgorithm.RSA, key));
            string privateKey;

            lock (syncLock)
            {
                if (keys.TryGetValue(publicKey, out privateKey))
                    return privateKey;
                else
                    throw new CryptographicException("Key not found in key chain.");
            }
        }

        /// <summary>
        /// Returns the number of keys in the chain.
        /// </summary>
        public int Count
        {
            get { return keys.Count; }
        }

        /// <summary>
        /// Removes all keys from the chain.
        /// </summary>
        public void Clear()
        {
            lock (syncLock)
                keys.Clear();
        }

        /// <summary>
        /// Encrypts the keys using the symmetric key passed.
        /// </summary>
        /// <returns>The encrypted key chain.</returns>
        public byte[] Encrypt(SymmetricKey key)
        {
            using (var ms = new EnhancedMemoryStream())
            {
                ms.WriteInt32(Magic);
                ms.WriteInt32(keys.Count);

                foreach (string privateKey in keys.Values)
                    ms.WriteString16(privateKey);

                ms.WriteBytesNoLen(Crypto.GetSalt8());

                return Crypto.Encrypt(ms.ToArray(), key);
            }
        }

        /// <summary>
        /// Returns the RSA keys as an array of strings.
        /// </summary>
        /// <returns>The key array.</returns>
        public string[] ToArray()
        {
            string[]    array = new string[this.Count];
            int         i;

            i = 0;
            foreach (string key in keys.Values)
                array[i++] = key;

            return array;
        }
    }
}
