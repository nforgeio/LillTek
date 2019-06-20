//-----------------------------------------------------------------------------
// FILE:        MD5Hasher.cs
// CONTRIBUTOR: Jeff Lill
// COPYRIGHT:   Copyright (c) 2005-2015 by Jeffrey Lill.  All rights reserved.
// DESCRIPTION: Implements the MD5 algorithm to hash data into a digest.

using System;
using System.IO;
using System.Security;
using System.Security.Cryptography;

using LillTek.Common;

namespace LillTek.Cryptography
{
    /// <summary>
    /// Implements the MD5 algorithm to hash data into a digest.
    /// </summary>
    public static class MD5Hasher
    {
        /// <summary>
        /// Size of a MD5 hashed digest in bytes.
        /// </summary>
        public const int DigestSize = 16;

        // The inner and outer XOR constants defined in RFC2104.

        private const int iPad = 0x36;
        private const int oPad = 0x5C;

        /// <summary>
        /// Computes the MD5 hash of the data buffer passed and then folds it
        /// into a 64-bit number via XOR.
        /// </summary>
        /// <param name="data">The data to be hashed.</param>
        /// <returns>The single folded hash of the data.</returns>
        /// <remarks>
        /// <para>
        /// A normal MD5 hash returns 16 bytes of data.  This method folds
        /// these 8 bytes into 8 bytes by taking the first 4 bytes of data
        /// and XORing these with the last 8 bytes and then converting
        /// the result into a 64-bit integer.
        /// </para>
        /// </remarks>
        public static long FoldOnce(byte[] data)
        {
            byte[] hash;
            long result;

            hash = Compute(data);
            result = 0;
            result |= (long)(byte)(hash[0] ^ hash[08]) << 56;
            result |= (long)(byte)(hash[1] ^ hash[09]) << 48;
            result |= (long)(byte)(hash[2] ^ hash[10]) << 40;
            result |= (long)(byte)(hash[3] ^ hash[11]) << 32;
            result |= (long)(byte)(hash[4] ^ hash[12]) << 24;
            result |= (long)(byte)(hash[5] ^ hash[13]) << 16;
            result |= (long)(byte)(hash[6] ^ hash[14]) << 8;
            result |= (long)(byte)(hash[7] ^ hash[15]);

            return result;
        }

        /// <summary>
        /// Computes the MD5 hash of the data buffer passed and then double folds it
        /// into a 32-bit number via XOR.
        /// </summary>
        /// <param name="data">The data to be hashed.</param>
        /// <returns>The double folded hash of the data.</returns>
        /// <remarks>
        /// A normal MD5 hash returns 16 bytes of data.  This method folds
        /// these 8 bytes into 8 bytes by taking the first 4 bytes of data
        /// and XORing these with the last 8 bytes and folding the result
        /// again, producing a 32-bit integer.
        /// </remarks>
        public static int FoldTwice(byte[] data)
        {
            long v;

            v = FoldOnce(data);
            return (int)(v >> 32) ^ (int)v;
        }

        /// <summary>
        /// Folds the 64-bit integer passed into a 32-bit integer.
        /// </summary>
        /// <param name="v">The 64-bit value to be folded.</param>
        /// <returns>The 32-bit result.</returns>
        public static int Fold(long v)
        {
            return (int)(v >> 32) ^ (int)v;
        }

        /// <summary>
        /// Hashes data from a buffer and returns the result.
        /// </summary>
        /// <param name="data">The input buffer.</param>
        /// <returns>The hashed digest.</returns>
        public static byte[] Compute(byte[] data)
        {
            return new MD5CryptoServiceProvider().ComputeHash(data, 0, data.Length);
        }

        /// <summary>
        /// Computes the MD5 hash of a string encoded as UTF-8.
        /// </summary>
        /// <param name="data">The string to be hashed.</param>
        /// <returns>The hashed digest.</returns>
        public static byte[] Compute(string data)
        {
            return Compute(Helper.ToUTF8(data));
        }

        /// <summary>
        /// Uses the HMAC/MD5 algorithm to hash data from a buffer and returns the result.
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
        /// Uses the HMAC/MD5 algorithm to hash data from a buffer and returns the result.
        /// </summary>
        /// <param name="key">The secret key.</param>
        /// <param name="data">The input buffer.</param>
        /// <returns>The hashed digest.</returns>
        public static byte[] Compute(byte[] key, byte[] data)
        {
            return Compute(key, data, 0, data.Length);
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
            return new MD5CryptoServiceProvider().ComputeHash(data, pos, length);
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
            var     md5 = new MD5CryptoServiceProvider();
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

                md5.TransformBlock(buf, 0, cb, buf, 0);
                length -= cb;
            }

            md5.TransformFinalBlock(buf, 0, 0);
            es.Seek(streamPos, SeekOrigin.Begin);

            return md5.Hash;
        }

        /// <summary>
        /// Uses the HMAC/MD5 algorithm to hash data from a stream.
        /// </summary>
        /// <param name="key">The secret key.</param>
        /// <param name="es">The input stream.</param>
        /// <param name="length">The number of bytes to hash.</param>
        /// <returns>The hashed digest.</returns>
        /// <remarks>
        /// The method will hash length bytes of the stream from the current position.
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
