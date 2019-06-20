//-----------------------------------------------------------------------------
// FILE:        AsymmetricCrypto.cs
// CONTRIBUTOR: Jeff Lill
// COPYRIGHT:   Copyright (c) 2005-2015 by Jeffrey Lill.  All rights reserved.
// DESCRIPTION: Misc asymmetric cryptographic utilities.

using System;
using System.Text;
using System.Security;
using System.Security.Cryptography;

using LillTek.Common;

// $todo(jeff.lill): Implement DSA support

namespace LillTek.Cryptography
{
    /// <summary>
    /// Misc asymmetric cryptographic utilities.
    /// </summary>
    /// <remarks>
    /// <para><b><u>Asymmetric Keys</u></b></para>
    /// <para>
    /// This implementation abstracts the concepts of asymmetric encryption
    /// keys represented as XML and keys that are stored securely within
    /// a smart card or other cryptographic provider's key container.  
    /// This makes it easy use either implementation without the need to 
    /// modify code.
    /// </para>
    /// <para>
    /// XML keys are simple encoded XML documents generated by the specific
    /// encryption algorithm implementation.  These keys can be generated
    /// by calls to methods such as <see cref="CreatePrivateKey" /> and
    /// <see cref="GetPublicKey" />.  These keys are easy to use because
    /// they can be passed directly to <see cref="Encrypt" /> and
    /// <see cref="Decrypt" /> methods.  The downside is using XML keys
    /// is that the key text will be present in memory and may be exposed
    /// in memory dumps to disk or to applications that hook the CLR.
    /// </para>
    /// <para>
    /// Key containers provide a more secure solution.  In this situation,
    /// the actual key is stored within a named key container implemented
    /// by a cryptographic provider.  The key container name is simply
    /// a string and the provider is identified by a unique name
    /// registered with the Windows CryptoAPI.
    /// </para>
    /// <para>
    /// Key containers are specified within a key string using the
    /// following syntax:
    /// </para>
    /// <code language="none">
    /// 
    ///         &lt;key name&gt; [ ":" &lt;provider name&gt; ]
    /// 
    /// </code>
    /// <para>
    /// where <b>key name</b> is the name of the key and <b>provider name</b>
    /// is the name of the provider.  Note that the provider name is optional.
    /// If this is not present then the default Windows key container provider
    /// will be used instead.  Key names are restricted to alpha numeric
    /// characters, periods, or dashes and key names are also restricted 
    /// a maximum of 16 characters in length.
    /// </para>
    /// <para>
    /// The class assumes that any key whose first non-whitespace character is 
    /// a "&lt;" is an XML key and that all other keys are specify a key
    /// container.
    /// </para>
    /// <para>
    /// For the sake of clarity, method parameters that can specify either an XML key or 
    /// a key container are referred to simply as "key".  Method parameters that that
    /// can accept only an XML keys will be referred to as "keyXml".
    /// </para>
    /// </remarks>
    public static class AsymmetricCrypto
    {
        // CryptoAPI constants

        internal const int PROV_RSA_FULL = 1;

        /// <summary>
        /// Throws an exception of the key container name passed is not valid.
        /// </summary>
        /// <param name="keyName">Thye key name.</param>
        private static void CheckKeyName(string keyName)
        {
            if (keyName.Length > 16)
                throw new CryptographicException("Key name cannot exceed 16 characters.");

            for (int i = 0; i < keyName.Length; i++)
                if (!Char.IsLetterOrDigit(keyName[i]) && keyName[i] != '.' && keyName[i] != '-')
                    throw new CryptographicException("Key names may include only letters, digits, dashes, or periods.");
        }

        /// <summary>
        /// Parses a key to determine if specifies a XML key 
        /// or a key container.
        /// </summary>
        /// <param name="key">The key to be tested.</param>
        /// <returns>
        /// The <see cref="CspParameters" /> instance to use if the key 
        /// specifies a key container or <c>null</c> if it specifies an XML key.
        /// </returns>
        /// <exception cref="CryptographicException">Thrown if the key string is not XML and is also not a valid key name.</exception>
        internal static CspParameters ParseKeyContainer(string key)
        {
            if (key.TrimStart().StartsWith("<"))
                return null;

            CspParameters   args;
            int             p;

            key = key.Trim();
            if (key.Length == 0)
                throw new CryptographicException("Invalid asymmetric key.");

            args              = new CspParameters();
            args.ProviderType = PROV_RSA_FULL;

            p = key.IndexOf(':');
            if (p == -1)
            {
                CheckKeyName(key);
                args.KeyContainerName = key;
            }
            else
            {
                args.KeyContainerName = key.Substring(0, p);
                args.ProviderName     = key.Substring(p + 1);

                CheckKeyName(args.KeyContainerName);
            }

            return args;
        }

        /// <summary>
        /// Creates the asymmetric encryption algorithm specified by the parameters.
        /// </summary>
        /// <param name="algorithm">The algorithm name.</param>
        /// <param name="args">The provider parameters.</param>
        /// <returns>The algorithm instance.</returns>
        private static AsymmetricAlgorithm CreateAsymmetric(string algorithm, CspParameters args)
        {
            switch (algorithm.ToUpper().Trim())
            {
                case CryptoAlgorithm.RSA:

                    return new RSACryptoServiceProvider(args);

                default:

                    throw new ArgumentException(Crypto.UnknownAlgorithm);
            }
        }

        /// <summary>
        /// Generates an asymmetric private key and returns the result as XML.
        /// </summary>
        /// <param name="algorithm">The asymmetric algorithm name.</param>
        /// <param name="keySize">The key size in bits.</param>
        /// <returns>The private key encoded as XML.</returns>
        /// <remarks>
        /// The current implementation supports only the "RSA" provider.
        /// </remarks>
        public static string CreatePrivateKey(string algorithm, int keySize)
        {
            var asymmetric = EncryptionConfig.CreateAsymmetric(algorithm, keySize);

            try
            {
                return asymmetric.ToXmlString(true);
            }
            finally
            {
                asymmetric.Clear();
            }
        }

        /// <summary>
        /// Returns the public key for a private key.
        /// </summary>
        /// <param name="algorithm">The asymmetric algorithm.</param>
        /// <param name="privateKeyXml">The private key encoded as XML.</param>
        /// <returns>The public key encoded as XML.</returns>
        /// <remarks>
        /// The current implementation supports only the "RSA" provider.
        /// </remarks>
        public static string GetPublicKey(string algorithm, string privateKeyXml)
        {
            var asymmetric = EncryptionConfig.CreateAsymmetric(algorithm, 0);

            try
            {

                asymmetric.FromXmlString(privateKeyXml);
                return asymmetric.ToXmlString(false);
            }
            finally
            {

                asymmetric.Clear();
            }
        }

        /// <summary>
        /// Verifies that the key string passed contains either a valid XML
        /// key for the specified asymmetric encryption algorithm or 
        /// specifies a key container name.
        /// </summary>
        /// <param name="algorithm">The asymmetric algorithm.</param>
        /// <param name="key">The key string to test.</param>
        /// <returns><c>true</c> if the key is valid.</returns>
        /// <remarks>
        /// The current implementation supports only the "RSA" provider.
        /// </remarks>
        public static bool IsValidKey(string algorithm, string key)
        {
            AsymmetricAlgorithm asymmetric = null;

            try
            {
                if (ParseKeyContainer(key) != null)
                    return true;

                asymmetric = EncryptionConfig.CreateAsymmetric(algorithm, 0);
                asymmetric.FromXmlString(key);

                return true;
            }
            catch
            {
                return false;
            }
            finally
            {
                if (asymmetric != null)
                    asymmetric.Clear();
            }
        }

        /// <summary>
        /// Returns <c>true</c> if the key XML passed represents a valid private key.
        /// </summary>
        /// <param name="algorithm">The asymmetric algorithm.</param>
        /// <param name="key">The key to be tested.</param>
        /// <returns><c>true</c> if the key is a valid XML private key.</returns>
        /// <remarks>
        /// The current implementation supports only the "RSA" provider.
        /// </remarks>
        public static bool IsXmlPrivateKey(string algorithm, string key)
        {
            if (!IsXmlKey(algorithm, key))
                return false;

            return key.IndexOf("<InverseQ>") != -1;
        }

        /// <summary>
        /// Returns <c>true</c> if the key XML passed represents a valid public key.
        /// </summary>
        /// <param name="algorithm">The asymmetric algorithm.</param>
        /// <param name="key">The key to be tested.</param>
        /// <returns><c>true</c> if the key is a valid public key.</returns>
        /// <remarks>
        /// The current implementation supports only the "RSA" provider.
        /// </remarks>
        public static bool IsXmlPublicKey(string algorithm, string key)
        {
            if (!IsXmlKey(algorithm, key))
                return false;

            return key.IndexOf("<InverseQ>") == -1;
        }

        /// <summary>
        /// Returns <c>true</c> if the key string passed is a valid XML key
        /// and is NOT a key container name.
        /// </summary>
        /// <param name="algorithm">The asymmetric algorithm.</param>
        /// <param name="key">The key string to be tested.</param>
        /// <returns><c>true</c> if the key is a valid XML key.</returns>
        /// <remarks>
        /// The current implementation supports only the "RSA" provider.
        /// </remarks>
        public static bool IsXmlKey(string algorithm, string key)
        {
            AsymmetricAlgorithm asymmetric = null;

            try
            {
                if (ParseKeyContainer(key) != null)
                    return false;

                asymmetric = EncryptionConfig.CreateAsymmetric(algorithm, 0);
                asymmetric.FromXmlString(key);

                return true;
            }
            catch
            {
                return false;
            }
            finally
            {
                if (asymmetric != null)
                    asymmetric.Clear();
            }
        }

        /// <summary>
        /// Returns <c>true</c> if the key string passed is a key container name.
        /// </summary>
        /// <param name="algorithm">The asymmetric algorithm.</param>
        /// <param name="key">The key string to test.</param>
        /// <returns><c>true</c> if the key is a key container name.</returns>
        /// <remarks>
        /// The current implementation supports only the "RSA" provider.
        /// </remarks>
        public static bool IsKeyContainer(string algorithm, string key)
        {
            return !IsXmlKey(algorithm, key);
        }

        /// <summary>
        /// Returns the public key for a private key stored in the
        /// specified key container.
        /// </summary>
        /// <param name="algorithm">The asymmetric algorithm.</param>
        /// <param name="keyContainerName">The key container name.</param>
        /// <param name="keyProviderName">The key provider name (or <c>null</c> for the default provider).</param>
        /// <returns>The public key encoded as XML.</returns>
        /// <remarks>
        /// The current implementation supports only the "RSA" provider.
        /// </remarks>
        public static string LoadPublicKey(string algorithm, string keyContainerName, string keyProviderName)
        {
            AsymmetricAlgorithm     asymmetric;
            CspParameters           cspParams;

            cspParams                  = new CspParameters();
            cspParams.ProviderType     = PROV_RSA_FULL;
            cspParams.KeyContainerName = keyContainerName;

            if (keyProviderName != null)
                cspParams.ProviderName = keyProviderName;

            asymmetric = CreateAsymmetric(algorithm, cspParams);

            try
            {
                return asymmetric.ToXmlString(false);
            }
            finally
            {
                asymmetric.Clear();
            }
        }

        /// <summary>
        /// Returns the public key for a private key stored in the
        /// specified key container.
        /// </summary>
        /// <param name="algorithm">The asymmetric algorithm.</param>
        /// <param name="key">The encoded key container name.</param>
        /// <returns>The public key encoded as XML.</returns>
        /// <remarks>
        /// The current implementation supports only the "RSA" provider.
        /// </remarks>
        public static string LoadPublicKey(string algorithm, string key)
        {
            var cspParameters = ParseKeyContainer(key);

            return LoadPublicKey(algorithm, cspParameters.KeyContainerName, cspParameters.ProviderName);
        }

        /// <summary>
        /// Returns the private key stored in the specified key container.
        /// </summary>
        /// <param name="algorithm">The asymmetric algorithm.</param>
        /// <param name="keyContainerName">The key container name.</param>
        /// <param name="keyProviderName">The key provider name (or <c>null</c> for the default provider).</param>
        /// <returns>The public key encoded as XML.</returns>
        /// <remarks>
        /// The current implementation supports only the "RSA" provider.
        /// </remarks>
        public static string LoadPrivateKey(string algorithm, string keyContainerName, string keyProviderName)
        {
            AsymmetricAlgorithm     asymmetric;
            CspParameters           cspParams;

            cspParams                  = new CspParameters();
            cspParams.ProviderType     = PROV_RSA_FULL;
            cspParams.KeyContainerName = keyContainerName;

            if (keyProviderName != null)
                cspParams.ProviderName = keyProviderName;

            asymmetric = CreateAsymmetric(algorithm, cspParams);

            try
            {
                return asymmetric.ToXmlString(true);
            }
            finally
            {
                asymmetric.Clear();
            }
        }

        /// <summary>
        /// Returns the private key stored in the specified key container.
        /// </summary>
        /// <param name="algorithm">The asymmetric algorithm.</param>
        /// <param name="key">The encoded key container name.</param>
        /// <returns>The private key encoded as XML.</returns>
        /// <remarks>
        /// The current implementation supports only the "RSA" provider.
        /// </remarks>
        public static string LoadPrivateKey(string algorithm, string key)
        {
            var cspParameters = ParseKeyContainer(key);

            return LoadPrivateKey(algorithm, cspParameters.KeyContainerName, cspParameters.ProviderName);
        }

        /// <summary>
        /// Saves an XML encoded key into the specified key container,
        /// overwriting any existing key in the container.
        /// </summary>
        /// <param name="algorithm">The asymmetric algorithm.</param>
        /// <param name="keyContainerName">The key container name.</param>
        /// <param name="keyProviderName">The key provider name (or <c>null</c> for the default provider).</param>
        /// <param name="key">The public or private key encoded as XML.</param>
        /// <remarks>
        /// The current implementation supports only the "RSA" provider.
        /// </remarks>
        public static void SaveKey(string algorithm, string keyContainerName, string keyProviderName, string key)
        {
            AsymmetricAlgorithm     asymmetric;
            CspParameters           cspParams;

            cspParams                  = new CspParameters();
            cspParams.ProviderType     = PROV_RSA_FULL;
            cspParams.KeyContainerName = keyContainerName;

            if (keyProviderName != null)
                cspParams.ProviderName = keyProviderName;

            asymmetric = CreateAsymmetric(algorithm, cspParams);

            try
            {
                asymmetric.FromXmlString(key);
            }
            finally
            {
                asymmetric.Clear();
            }
        }

        /// <summary>
        /// Removes the named key container if it exists.
        /// </summary>
        /// <param name="algorithm">The asymmetric algorithm.</param>
        /// <param name="keyContainerName">The key container name.</param>
        /// <param name="keyProviderName">The key provider name (or <c>null</c> for the default provider).</param>
        /// <remarks>
        /// <para>
        /// The current implementation supports only the "RSA" provider.
        /// </para>
        /// <para>
        /// <note>
        /// The method does not throw an exception if the container does 
        /// not exist.
        /// </note>
        /// </para>
        /// </remarks>
        public static void DeleteKey(string algorithm, string keyContainerName, string keyProviderName)
        {
            CspParameters cspParams;

            cspParams                  = new CspParameters();
            cspParams.ProviderType     = PROV_RSA_FULL;
            cspParams.KeyContainerName = keyContainerName;

            if (keyProviderName != null)
                cspParams.ProviderName = keyProviderName;

            switch (algorithm.ToUpper().Trim())
            {
                case CryptoAlgorithm.RSA:

                    var rsa = new RSACryptoServiceProvider(cspParams);

                    rsa.PersistKeyInCsp = false;
                    rsa.Clear();
                    break;

                default:

                    throw new ArgumentException(Crypto.UnknownAlgorithm);
            }
        }

        /// <summary>
        /// Encrypts a byte array using the specified asymmetric encryption algorithm and the
        /// public key specified.
        /// </summary>
        /// <param name="algorithm">The encryption algorithm.</param>
        /// <param name="key">The key.</param>
        /// <param name="plainText">The unencrypted input array.</param>
        /// <returns>The encrypted results.</returns>
        /// <remarks>
        /// The current implementation supports only the "RSA" provider.
        /// </remarks>
        public static byte[] Encrypt(string algorithm, string key, byte[] plainText)
        {
            switch (algorithm.ToUpper().Trim())
            {
                case CryptoAlgorithm.RSA:

                    RSACryptoServiceProvider    rsa;
                    CspParameters               cspParams;
                    byte[]                      encrypted;

                    cspParams = ParseKeyContainer(key);
                    if (cspParams == null)
                    {
                        rsa = new RSACryptoServiceProvider();
                        rsa.FromXmlString(key);
                        encrypted = rsa.Encrypt(plainText, true);
                    }
                    else
                    {
                        rsa = new RSACryptoServiceProvider(cspParams);
                        encrypted = rsa.Encrypt(plainText, true);
                    }

                    rsa.Clear();
                    return encrypted;

                default:

                    throw new ArgumentException(Crypto.UnknownAlgorithm);
            }
        }

        /// <summary>
        /// Decrypts a byte array using the specified asymmetric encryption algorithm and
        /// and the private key specified.
        /// </summary>
        /// <param name="algorithm">The encryption algorithm.</param>
        /// <param name="key">The key.</param>
        /// <param name="encrypted">The encrypted bytes.</param>
        /// <returns>The decrypted results.</returns>
        /// <remarks>
        /// The current implementation supports only the "RSA" provider.
        /// </remarks>
        public static byte[] Decrypt(string algorithm, string key, byte[] encrypted)
        {
            switch (algorithm.ToUpper().Trim())
            {
                case CryptoAlgorithm.RSA:

                    RSACryptoServiceProvider    rsa;
                    CspParameters               cspParams;
                    byte[]                      decrypted;

                    cspParams = ParseKeyContainer(key);
                    if (cspParams == null)
                    {
                        rsa = new RSACryptoServiceProvider();
                        rsa.FromXmlString(key);
                        decrypted = rsa.Decrypt(encrypted, true);
                    }
                    else
                    {
                        rsa = new RSACryptoServiceProvider(cspParams);
                        decrypted = rsa.Decrypt(encrypted, true);
                    }

                    rsa.Clear();
                    return decrypted;

                default:

                    throw new ArgumentException(Crypto.UnknownAlgorithm);
            }
        }

        /// <summary>
        /// Returns <c>true</c> if the two XML keys are the same.
        /// </summary>
        /// <param name="algorithm">The encryption algorithm.</param>
        /// <param name="key1">The first XML key.</param>
        /// <param name="key2">The second XML key.</param>
        /// <returns><c>true</c> if the keys are the same.</returns>
        /// <remarks>
        /// The current implementation supports only the "RSA" provider.
        /// </remarks>
        public static bool KeyEquality(string algorithm, string key1, string key2)
        {
            // $todo(jeff.lill): 
            //
            // I need to look into normalizing the keys or 
            // perhaps parsing the XML and looking at the
            // properties.  For now, I'm going to remove
            // extra whitespace and then do a straight 
            // comparison.

            key1 = Helper.StripWhitespace(key1);
            key2 = Helper.StripWhitespace(key2);

            return key1 == key2;
        }

        /// <summary>
        /// Encrypts authentication <see cref="Credentials" /> into a byte
        /// array using the specified public asymmetric public key and
        /// algorithm.
        /// </summary>
        /// <param name="credentials">The <see cref="Credentials" />.</param>
        /// <param name="algorithm">The encryption algorithm.</param>
        /// <param name="key">The public key.</param>
        /// <returns>The encrypted credentials.</returns>
        /// <remarks>
        /// The current implementation supports only the "RSA" provider.
        /// </remarks>
        public static byte[] EncryptCredentials(Credentials credentials, string algorithm, string key)
        {
            using (var ms = new EnhancedMemoryStream(256))
            {
                ms.WriteInt32(Crypto.CredentialMagic);
                ms.WriteString16(credentials.Realm);
                ms.WriteString16(credentials.Account);
                ms.WriteString16(credentials.Password);
                ms.WriteBytesNoLen(Crypto.GetSalt4());

                return Encrypt(algorithm, key, ms.ToArray());
            }
        }

        /// <summary>
        /// Decrypts authentication <see cref="Credentials" /> from a
        /// byte array using the specified public asymmetric private key and
        /// algorithm.
        /// </summary>
        /// <param name="encrypted">The encrypted credential bytes.</param>
        /// <param name="algorithm">The encryption algorithm.</param>
        /// <param name="key">The private key.</param>
        /// <returns>The decrypted <see cref="Credentials" />.</returns>
        /// <remarks>
        /// <note>
        /// The current implementation supports only the "RSA" provider.
        /// </note>
        /// </remarks>
        /// <exception cref="SecurityException">Thrown if the credentials are corrupt.</exception>
        public static Credentials DecryptCredentials(byte[] encrypted, string algorithm, string key)
        {
            try
            {
                var decrypted = Decrypt(algorithm, key, encrypted);

                using (var ms = new EnhancedMemoryStream(decrypted))
                {

                    string realm;
                    string account;
                    string password;

                    if (ms.ReadInt32() != Crypto.CredentialMagic)
                        throw new SecurityException(Crypto.CorruptCredentialsMsg);

                    realm = ms.ReadString16();
                    account = ms.ReadString16();
                    password = ms.ReadString16();

                    return new Credentials(realm, account, password);
                }
            }
            catch (Exception e)
            {
                throw new SecurityException(Crypto.CorruptCredentialsMsg, e);
            }
        }
    }
}
