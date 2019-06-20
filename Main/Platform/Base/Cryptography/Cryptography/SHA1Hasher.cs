//-----------------------------------------------------------------------------
// FILE:        SHA1Hasher.cs
// CONTRIBUTOR: Jeff Lill
// COPYRIGHT:   Copyright (c) 2005-2015 by Jeffrey Lill.  All rights reserved.
// DESCRIPTION: Implements the SHA-1 algorithm to hash data into a digest.

using System;
using System.Collections.Generic;
using System.IO;
using System.Security.Cryptography;
using System.Text;

using LillTek.Common;

namespace LillTek.Cryptography
{
    /// <summary>
    /// Implements the SHA-1 algorithm to hash data into a digest.
    /// </summary>
    public static class SHA1Hasher
    {
        /// <summary>
        /// Size of a SHA1 hashed digest in bytes.
        /// </summary>
        public const int DigestSize = 20;

        // The inner and outer XOR constants defined in RFC2104.

        private const int iPad = 0x36;
        private const int oPad = 0x5C;

        /// <summary>
        /// Hashes data from a buffer and returns the result.
        /// </summary>
        /// <param name="data">The input buffer.</param>
        /// <returns>The hashed digest.</returns>
        public static byte[] Compute(byte[] data)
        {
            return new SHA1Managed().ComputeHash(data, 0, data.Length);
        }

        /// <summary>
        /// Hashes data from a buffer and returns the result.
        /// </summary>
        /// <param name="data">The input buffer.</param>
        /// <param name="pos">Index of the first byte to be hashed.</param>
        /// <param name="length">The number of bytes to hash.</param>
        /// <returns>The hashed digest.</returns>
        public static byte[] Compute(byte[] data, int pos, int length)
        {
            return new SHA1Managed().ComputeHash(data, pos, length);
        }

        /// <summary>
        /// Computes the SHA-1 hash of a string encoded as UTF-8.
        /// </summary>
        /// <param name="data">The string to be hashed.</param>
        /// <returns>The hashed digest.</returns>
        public static byte[] Compute(string data)
        {
            return Compute(Helper.ToUTF8(data));
        }

        /// <summary>
        /// Uses the HMAC/SHA-1 algorithm to hash data from a buffer and returns the result.
        /// </summary>
        /// <param name="key">The secret key.</param>
        /// <param name="data">The input buffer.</param>
        /// <param name="pos">Index of the first byte to be hashed.</param>
        /// <param name="length">The number of bytes to hash.</param>
        /// <returns>The hashed digest.</returns>
        public static byte[] Compute(byte[] key, byte[] data, int pos, int length)
        {
            byte[]              xorBuf = new byte[64];
            byte[]              hash;
            EnhancedBlockStream es;

            // If the key length is greater than 64 bytes then hash the key
            // first to get it down to a reasonable size.

            if (key.Length > 64)
                key = Compute(key);

            // Pad the key with zeros to 64 bytes in length.

            if (key.Length < 64)
            {
                byte[] newKey = new byte[64];

                for (int i = 0; i < newKey.Length; i++)
                {
                    if (i < key.Length)
                        newKey[i] = key[i];
                    else
                        newKey[i] = 0;
                }

                key = newKey;
            }

            // XOR the key with iPad and put the result in xorBuf

            for (int i = 0; i < 64; i++)
                xorBuf[i] = (byte)(key[i] ^ iPad);

            // Hash the result of the data appended to xorBuf

            es   = new EnhancedBlockStream(new Block(xorBuf), new Block(data, pos, length));
            hash = Compute(es, xorBuf.Length + length);

            // XOR the key with oPad and put the result in xorBuf

            for (int i = 0; i < 64; i++)
                xorBuf[i] = (byte)(key[i] ^ oPad);

            // The result is the hash of the combination of hash appended to xorBuf

            es = new EnhancedBlockStream(new Block(xorBuf), new Block(hash));

            return Compute(es, xorBuf.Length + hash.Length);
        }

        /// <summary>
        /// Uses the HMAC/SHA-1 algorithm to hash data from a buffer and returns the result.
        /// </summary>
        /// <param name="key">The secret key.</param>
        /// <param name="data">The input buffer.</param>
        /// <returns>The hashed digest.</returns>
        public static byte[] Compute(byte[] key, byte[] data)
        {
            return Compute(key, data, 0, data.Length);
        }

        /// <summary>
        /// Hashes data from a stream.
        /// </summary>
        /// <param name="es">The input stream.</param>
        /// <param name="length">The number of bytes to hash.</param>
        /// <returns>The hashed digest.</returns>
        /// <remarks>
        /// The method will hash length bytes of the stream from the current position
        /// and the stream position will be restored before the method
        /// returns.
        /// </remarks>
        public static byte[] Compute(EnhancedStream es, long length)
        {
            var     sha1 = new SHA1Managed();
            long    streamPos;
            byte[]  buf;
            int     cb;

            streamPos = es.Position;
            buf = new byte[8192];

            while (length > 0)
            {
                cb = (int)(length > buf.Length ? buf.Length : length);
                if (es.Read(buf, 0, cb) < cb)
                    throw new InvalidOperationException("Read past end of stream.");

                sha1.TransformBlock(buf, 0, cb, buf, 0);
                length -= cb;
            }

            sha1.TransformFinalBlock(buf, 0, 0);
            es.Seek(streamPos, SeekOrigin.Begin);

            return sha1.Hash;
        }

        /// <summary>
        /// Uses the HMAC/SHA-1 algorithm to hash data from a stream.
        /// </summary>
        /// <param name="key">The secret key.</param>
        /// <param name="es">The input stream.</param>
        /// <param name="length">The number of bytes to hash.</param>
        /// <returns>The hashed digest.</returns>
        /// <remarks>
        /// The method will hash length bytes of the stream from the current position
        /// and the stream position will be restored before the method
        /// returns.
        /// </remarks>
        public static byte[] Compute(byte[] key, EnhancedStream es, int length)
        {
            byte[]  hash;
            long    pos;

            pos = es.Position;
            hash = Compute(key, es.ReadBytes(length));
            es.Seek(pos, SeekOrigin.Begin);

            return hash;
        }
    }
}
