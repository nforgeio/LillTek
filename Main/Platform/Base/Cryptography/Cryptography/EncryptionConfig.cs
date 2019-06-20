//-----------------------------------------------------------------------------
// FILE:        EncryptionConfig.cs
// CONTRIBUTOR: Jeff Lill
// COPYRIGHT:   Copyright (c) 2005-2015 by Jeffrey Lill.  All rights reserved.
// DESCRIPTION: Class to identify an encryption algorithm and its possible key sizes.

using System;
using System.Security.Cryptography;

using LillTek.Common;

// $todo(jeff.lill): Add support for DSA asymmetric encryption.

namespace LillTek.Cryptography
{
    /// <summary>
    /// Identifies an encryption algorithm and its possible key sizes.
    /// </summary>
    public sealed class EncryptionConfig
    {

        //---------------------------------------------------------------------
        // Static members

        private const string AlgoritihmNotImplemented = "The [{0}] encryption algorithm is not available for the current platform.";

        private static EncryptionConfig[] platformSymmetric;
        private static EncryptionConfig[] platformAsymmetric;

        static EncryptionConfig()
        {
            platformSymmetric = new EncryptionConfig[]  {
                                                            EncryptionConfig.Parse("PlainText:0"),
                                                            EncryptionConfig.Parse("RC2:128,120,112,104,96,88,80,72,64,56,48,40"),
                                                            EncryptionConfig.Parse("TripleDES:192,128"),
                                                            EncryptionConfig.Parse("DES:64"),
                                                            EncryptionConfig.Parse("AES:256,192,128"),
            };

            platformAsymmetric = new EncryptionConfig[] {
                                                            EncryptionConfig.Parse("RSA:4096,2048,1024,512")
                                                        };
        }

        /// <summary>
        /// Parses an encryption configuration from the string passed.
        /// </summary>
        /// <param name="s">The encoded config.</param>
        public static EncryptionConfig Parse(string s)
        {
            string      name;
            string[]    sizes;
            int         pos;

            pos = s.IndexOf(':');
            if (pos == -1)
                throw new ArgumentException();

            name = s.Substring(0, pos).Trim();
            sizes = Helper.ParseStringList(s.Substring(pos + 1), ',');

            return new EncryptionConfig(name, sizes);
        }

        /// <summary>
        /// Returns the symmetric encryption configurations supported by the running
        /// instance of the .NET Framework.
        /// </summary>
        public static EncryptionConfig[] PlatformSymmetric
        {
            get { return platformSymmetric; }
        }

        /// <summary>
        /// Returns the asymmetric encryption configurations supported by the running
        /// instance of the .NET Framework.
        /// </summary>
        public static EncryptionConfig[] PlatformAsymmetric
        {
            get { return platformAsymmetric; }
        }

        /// <summary>
        /// Returns the largest valid key size (expressed in bits) for the 
        /// algorithm specified.
        /// </summary>
        /// <param name="algorithm">The algorithm name.</param>
        /// <returns>The maximum key size in bits.</returns>
        public static int MaxAlgorithmKeySize(string algorithm)
        {
            EncryptionConfig    config = null;
            int                 maxKey = 0;

            for (int i = 0; i < platformSymmetric.Length; i++)
                if (String.Compare(algorithm, platformSymmetric[i].Algorithm, StringComparison.OrdinalIgnoreCase) == 0)
                {
                    config = platformSymmetric[i];
                    break;
                }

            for (int i = 0; i < platformAsymmetric.Length; i++)
                if (String.Compare(algorithm, platformAsymmetric[i].Algorithm, StringComparison.OrdinalIgnoreCase) == 0)
                {
                    config = platformAsymmetric[i];
                    break;
                }

            if (config == null)
                throw new ArgumentException(Crypto.UnknownAlgorithm);

            for (int i = 0; i < config.KeySizes.Length; i++)
                if (config.KeySizes[i] > maxKey)
                    maxKey = config.KeySizes[i];

            Assertion.Test(maxKey > 0);
            return maxKey;
        }

        /// <summary>
        /// Returns the valid key sizes in bits for the specified encryption algorithm.
        /// </summary>
        /// <param name="algorithm">The algorithm name.</param>
        /// <returns>The array of possible key sizes in bits sorted in decending order.</returns>
        public static int[] GetValidKeySizes(string algorithm)
        {
            EncryptionConfig config = null;

            for (int i = 0; i < platformSymmetric.Length; i++)
                if (String.Compare(algorithm, platformSymmetric[i].Algorithm, StringComparison.OrdinalIgnoreCase) == 0)
                {
                    config = platformSymmetric[i];
                    break;
                }

            for (int i = 0; i < platformAsymmetric.Length; i++)
                if (String.Compare(algorithm, platformAsymmetric[i].Algorithm, StringComparison.OrdinalIgnoreCase) == 0)
                {
                    config = platformAsymmetric[i];
                    break;
                }

            if (config == null)
                throw new ArgumentException(Crypto.UnknownAlgorithm);

            return config.KeySizes;
        }

        /// <summary>
        /// Creates a symmetric encryption algorithm corresponding to the algorithm name
        /// passed.
        /// </summary>
        /// <param name="algorithm">The algorithm name.</param>
        /// <returns>The symmetric algorithm object.</returns>
        /// <remarks>
        /// The current supported cross platform encryption algorithms
        /// are: "PlainText", "DES", "RC2", "TripleDES", and "AES" (Rijndael).
        /// </remarks>
        public static SymmetricAlgorithm CreateSymmetric(string algorithm)
        {
            switch (algorithm.ToUpper().Trim())
            {
                case CryptoAlgorithm.PlainText:

                    return new PlainTextCryptoServiceProvider();

                case CryptoAlgorithm.DES:

#if SILVERLIGHT
                    throw new NotImplementedException(string.Format(AlgoritihmNotImplemented,algorithm));
#else
                    return new DESCryptoServiceProvider();
#endif

                case CryptoAlgorithm.RC2:

#if SILVERLIGHT
                    throw new NotImplementedException(string.Format(AlgoritihmNotImplemented,algorithm));
#else
                    return new RC2CryptoServiceProvider();
#endif

                case CryptoAlgorithm.TripleDES:

#if SILVERLIGHT
                    throw new NotImplementedException(string.Format(AlgoritihmNotImplemented,algorithm));
#else
                    return new TripleDESCryptoServiceProvider();
#endif

                case CryptoAlgorithm.AES:

#if SILVERLIGHT
                    return new AesManaged();
#else
                    // Mono doesn't implement AesManaged so we need to detect this
                    // at runtime and return RijndaelManaged instead.

                    // return new RijndaelManaged();

                    if (Helper.IsMono)
                        return new RijndaelManaged();
                    else
                        return CreateAesManaged();      // This needs to be constructed in a separate
                // method so the JIT won't fail when running
                // on Mono.
#endif
                default:

                    throw new ArgumentException(Crypto.UnknownAlgorithm);
            }
        }

        private static SymmetricAlgorithm CreateAesManaged()
        {
            Helper.MonoInlineHack();
            return new AesManaged();
        }

        /// <summary>
        /// Generates a random key and initialization vector for the 
        /// specified encryption algorithm.
        /// </summary>
        /// <param name="algorithm">The algorithm name.</param>
        /// <param name="keySize">The key size in bits.</param>
        /// <param name="key">Returns as the key.</param>
        /// <param name="IV">Returns as the initialization vector.</param>
        public static void GenKeyIV(string algorithm, int keySize, out byte[] key, out byte[] IV)
        {
#if WINFULL || MOBILE_DEVICE
            SymmetricAlgorithm provider = CreateSymmetric(algorithm);

            provider.KeySize = keySize;
            provider.GenerateKey();
            provider.GenerateIV();

            key = provider.Key;
            IV = provider.IV;
#endif
        }

#if !MOBILE_DEVICE
        /// <summary>
        /// Creates an asymmetric algorithm instance initialized with a newly generated key
        /// pair of the specified size.
        /// </summary>
        /// <param name="algorithm">The algorithm name.</param>
        /// <param name="keySize">The key size in bits (or 0 for a default key size).</param>
        /// <returns>The asymmetric algorithm instance.</returns>
        /// <remarks>
        /// The current implementation supports only the "RSA" provider.
        /// </remarks>
        public static AsymmetricAlgorithm CreateAsymmetric(string algorithm, int keySize)
        {
            switch (algorithm.ToUpper().Trim())
            {
                case CryptoAlgorithm.RSA:

                    if (keySize > 0)
                        return new RSACryptoServiceProvider(keySize);
                    else
                        return new RSACryptoServiceProvider();

                default:

                    throw new ArgumentException(Crypto.UnknownAlgorithm);
            }
        }

        /// <summary>
        /// Creates an asymmetric algorithm instance initialized with the specified key.
        /// </summary>
        /// <param name="algorithm">The algorithm name.</param>
        /// <param name="key">The key.</param>
        /// <returns>The asymmetric algorithm instance.</returns>
        /// <remarks>
        /// The current implementation supports only the "RSA" provider and the <b>key</b>
        /// parameter may specify and XML key or a key container as described in 
        /// <see cref="AsymmetricCrypto" />.
        /// </remarks>
        public static AsymmetricAlgorithm CreateAsymmetric(string algorithm, string key)
        {
            AsymmetricAlgorithm     asymmetric;
            CspParameters           args;

            switch (algorithm.ToUpper().Trim())
            {
                case CryptoAlgorithm.RSA:

                    args = AsymmetricCrypto.ParseKeyContainer(key);
                    if (args != null)
                        asymmetric = new RSACryptoServiceProvider(args);
                    else
                    {
                        asymmetric = new RSACryptoServiceProvider();
                        asymmetric.FromXmlString(key);
                    }

                    return asymmetric;

                default:

                    throw new ArgumentException(Crypto.UnknownAlgorithm);
            }
        }
#endif // MOBILE_DEVICE

        //---------------------------------------------------------------------
        // Instance members

        /// <summary>
        /// Name of the encryption algorithm.  These are the same names
        /// used by the .NET platform to identify crypto algorithms.
        /// </summary>
        public string Algorithm;

        /// <summary>
        /// The implemented key sizes expressed as the number of bits.
        /// </summary>
        public int[] KeySizes;

        /// <summary>
        /// Default constructor for the XML serializer.
        /// </summary>
        public EncryptionConfig()
        {
            this.Algorithm = null;
            this.KeySizes  = null;
        }

        /// <summary>
        /// Constructor.
        /// </summary>
        /// <param name="algorithm">The algorithm name.</param>
        /// <param name="keySizes">The available key sizes encoded as integer values.</param>
        public EncryptionConfig(string algorithm, string[] keySizes)
        {
            this.Algorithm = algorithm;

            this.KeySizes = new int[keySizes.Length];
            for (int i = 0; i < keySizes.Length; i++)
                this.KeySizes[i] = int.Parse(keySizes[i]);
        }

        /// <summary>
        /// Returns string representation of the encryption configuration .
        /// </summary>
        public override string ToString()
        {
            string s;

            s = Algorithm + ":";
            for (int i = 0; i < KeySizes.Length; i++)
            {
                if (i > 0)
                    s += ',';

                s += KeySizes[i].ToString();
            }

            return s;
        }
    }
}
