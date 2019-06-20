//-----------------------------------------------------------------------------
// FILE:        BlockEncryptor.cs
// CONTRIBUTOR: Jeff Lill
// COPYRIGHT:   Copyright (c) 2005-2015 by Jeffrey Lill.  All rights reserved.
// DESCRIPTION: Implements block encryption using a symmetric encryption algorithm.

using System;
using System.IO;
using System.Security;
using System.Security.Cryptography;

using LillTek.Common;

namespace LillTek.Cryptography
{
    /// <summary>
    /// Implements block encryption using a symmetric encryption algorithm.
    /// </summary>
    /// <threadsafety instance="false" />
    public sealed class BlockEncryptor : IDisposable
    {
        private string              algorithm;
        private byte[]              key;
        private byte[]              IV;
        private SymmetricAlgorithm  provider;
        private ICryptoTransform    encryptor;

        /// <summary>
        /// Initializes the encryptor to use the specified encryption algorithm,
        /// key, and initialization vector.
        /// </summary>
        /// <param name="algorithm">The symmetric algorithm name.</param>
        /// <param name="key">The encryption key.</param>
        /// <param name="IV">The initialization vector.</param>
        public BlockEncryptor(string algorithm, byte[] key, byte[] IV)
        {
            this.algorithm    = algorithm;
            this.key          = key;
            this.IV           = IV;

            this.provider     = EncryptionConfig.CreateSymmetric(algorithm);
            this.provider.Key = key;
            this.provider.IV  = IV;

            this.encryptor    = null;
        }

        /// <summary>
        /// Initializes the encryptor to use the specified 
        /// <see cref="SymmetricKey" />.
        /// </summary>
        /// <param name="symmetricKey">The symmetric key.</param>
        public BlockEncryptor(SymmetricKey symmetricKey)
            : this(symmetricKey.Algorithm, symmetricKey.Key, symmetricKey.IV)
        {
        }

        /// <summary>
        /// Closes the encryptor, proactively releasing any associated unmanaged resources.
        /// </summary>
        public void Dispose()
        {
            if (provider != null)
            {
                provider.Clear();
                provider = null;
            }

            if (encryptor != null)
            {
                encryptor.Dispose();
                encryptor = null;
            }
        }

        /// <summary>
        /// Encrypts the input buffer passed and returns the result.  Note that the
        /// size of the output may exceed the size of the input.
        /// </summary>
        /// <param name="input">The cleartext input.</param>
        public byte[] Encrypt(byte[] input)
        {
            if (input.Length == 0)
                return input;

            if (encryptor == null)
                encryptor = provider.CreateEncryptor();

            var ms = new MemoryStream(input.Length + 64);
            var cs = new CryptoStream(ms, encryptor, CryptoStreamMode.Write);

            try
            {
                cs.Write(input, 0, input.Length);
                cs.FlushFinalBlock();

                return ms.ToArray();
            }
            finally
            {
                if (!encryptor.CanReuseTransform)
                {
                    encryptor.Dispose();
                    encryptor = null;
                }
            }
        }

        /// <summary>
        /// Encrypts the specified range of bytes from the buffer passed.
        /// </summary>
        /// <param name="input">The clear text input.</param>
        /// <param name="index">The starting index.</param>
        /// <param name="count">The number of bytes to encrypt.</param>
        /// <returns>The encrypted data.</returns>
        public byte[] Encrypt(byte[] input, int index, int count)
        {
            if (count == 0)
                return new byte[0];

            if (encryptor == null)
                encryptor = provider.CreateEncryptor();

            var ms = new MemoryStream(count + 64);
            var cs = new CryptoStream(ms, encryptor, CryptoStreamMode.Write);

            try
            {
                cs.Write(input, index, count);
                cs.FlushFinalBlock();

                return ms.ToArray();
            }
            finally
            {
                if (!encryptor.CanReuseTransform)
                {
                    encryptor.Dispose();
                    encryptor = null;
                }
            }
        }
    }
}
