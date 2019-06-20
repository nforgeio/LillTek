//-----------------------------------------------------------------------------
// FILE:        PlainTextCryptoServiceProvider.cs
// CONTRIBUTOR: Jeff Lill
// COPYRIGHT:   Copyright (c) 2005-2015 by Jeffrey Lill.  All rights reserved.
// DESCRIPTION: Implements an encryption provider that does no encryption.

using System;
using System.Security.Cryptography;

using LillTek.Common;

namespace LillTek.Cryptography
{
    internal sealed class PlainTextTransform : ICryptoTransform
    {
        private const int BlockSize = 256;

        public void Dispose()
        {
        }

        public bool CanReuseTransform
        {
            get { return true; }
        }

        public bool CanTransformMultipleBlocks
        {
            get { return true; }
        }

        public int InputBlockSize
        {
            get { return BlockSize; }
        }

        public int OutputBlockSize
        {
            get { return BlockSize; }
        }

        public int TransformBlock(byte[] inBuf, int inPos, int cbIn, byte[] outBuf, int outPos)
        {
            Array.Copy(inBuf, inPos, outBuf, outPos, cbIn);
            return cbIn;
        }

        public byte[] TransformFinalBlock(byte[] inBuf, int inPos, int cbIn)
        {
            byte[] outBuf;

            outBuf = new byte[cbIn];
            Array.Copy(inBuf, inPos, outBuf, 0, cbIn);
            return outBuf;
        }
    }

    /// <summary>
    /// Implements a symmetric encryption provider that does no encryption.
    /// </summary>
    public sealed class PlainTextCryptoServiceProvider : SymmetricAlgorithm
    {
        private byte[] key = new byte[] { 0 };
        private byte[] iv = new byte[] { 0 };

        /// <summary>
        /// Creates a plain text encryptor.
        /// </summary>
        public override ICryptoTransform CreateEncryptor()
        {
            return new PlainTextTransform();
        }

        /// <summary>
        /// Creates a plain next encryptor.
        /// </summary>
        /// <param name="key">The encryption key.</param>
        /// <param name="IV">The initialization vector.</param>
        public override ICryptoTransform CreateEncryptor(byte[] key, byte[] IV)
        {
            return CreateEncryptor();
        }

        /// <summary>
        /// Creates a plain text decryptor.
        /// </summary>
        public override ICryptoTransform CreateDecryptor()
        {
            return new PlainTextTransform();
        }

        /// <summary>
        /// Creates a plain text decryptor.
        /// </summary>
        /// <param name="key">The encryption key.</param>
        /// <param name="IV">The initialization vector.</param>
        public override ICryptoTransform CreateDecryptor(byte[] key, byte[] IV)
        {
            return CreateDecryptor();
        }

        /// <summary>
        /// Returns the set of legal key sizes for this provider.
        /// </summary>
        public override KeySizes[] LegalKeySizes
        {
            get { return new KeySizes[] { new KeySizes(8, 8, 0) }; }
        }

        /// <summary>
        /// Generates an encryption key.
        /// </summary>
        public override void GenerateKey()
        {
        }

        /// <summary>
        /// Generates an initialization vector.
        /// </summary>
        public override void GenerateIV()
        {
        }

        /// <summary>
        /// The encryption key.
        /// </summary>
        public override byte[] Key
        {
            get { return key; }
            set { key = value; }
        }

        /// <summary>
        /// The initialization vector.
        /// </summary>
        public override byte[] IV
        {
            get { return iv; }
            set { iv = value; }
        }
    }
}
