//-----------------------------------------------------------------------------
// FILE:        CryptoAlgorithm.cs
// CONTRIBUTOR: Jeff Lill
// COPYRIGHT:   Copyright (c) 2005-2015 by Jeffrey Lill.  All rights reserved.
// DESCRIPTION: Defines the common cryptographic algorithm names.

using System;
using System.Text;
using System.Security.Cryptography;

using LillTek.Common;

namespace LillTek.Cryptography
{
    /// <summary>
    /// Defines the common cryptographic algorithm names.
    /// </summary>
    public static class CryptoAlgorithm
    {
        // Important Implementation Note:
        //
        // These algorithm names must all be in UPPERCASE.

        /// <summary>
        /// Plain Text encryption performs no encryption at all.  This is useful
        /// for testing scenarios or when applications can be configured to
        /// disable encryption.
        /// </summary>
        public const string PlainText = "PLAINTEXT";

        /// <summary>
        /// <i>Symmetric</i>: RSA RC2 algorithm.
        /// </summary>
        public const string RC2 = "RC2";

        /// <summary>
        /// <i>Symmetric</i>: Enhanced Data Encryption Standard (DES) algorithm.
        /// </summary>
        public const string TripleDES = "TRIPLEDES";

        /// <summary>
        /// <i>Symmetric</i>: Data Encryption Standard (DES) algorithm.
        /// </summary>
        public const string DES = "DES";

        /// <summary>
        /// <i>Symmetric</i>: Andvanced Encryption Standard (AES) algoirthmn (also
        /// known as <b>Rijndael</b>).
        /// </summary>
        public const string AES = "AES";

        /// <summary>
        /// <i>Asymmetric</i>: RSA public/private key encryption.
        /// </summary>
        public const string RSA = "RSA";
    }
}
