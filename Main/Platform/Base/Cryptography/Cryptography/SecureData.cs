//-----------------------------------------------------------------------------
// FILE:        SecureData.cs
// CONTRIBUTOR: Jeff Lill
// COPYRIGHT:   Copyright (c) 2005-2015 by Jeffrey Lill.  All rights reserved.
// DESCRIPTION: Implements a mechanism to encrypt a byte array using
//              a combination of asymmetric and symmetric keys.

using System;
using System.IO;
using System.Collections.Generic;
using System.Text;
using System.Security.Cryptography;

using LillTek.Common;

// $todo(jeff.lill): Modify this implementation to be able to use secure key containers.

namespace LillTek.Cryptography
{
    /// <summary>
    /// Implements a mechanism to encrypt a byte array using a combination of 
    /// asymmetric and symmetric keys, including cryptographic salt, padding,
    /// and data validation fields.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Use <see cref="Encrypt(string,byte[],string,int)" /> to encrypt a byte array using one half of a RSA
    /// private key and the specified symmetric algorithm.  This method generates a
    /// one time symmetric key whose size is specified and and encrypts the 
    /// algorithm and the key using the RSA key and adds this to the result.
    /// The source data is then encrypted using the symmetric key and is added
    /// to the result.
    /// </para>
    /// <para>
    /// <see cref="Decrypt(string,byte[])" /> decrypts byte array encrypted using 
    /// <see cref="Encrypt(string,byte[],string,int)" /> using the other half of a private RSA key.
    /// </para>
    /// <para>
    /// In some situations, it's necessary to obscure the size of an encrypted
    /// data block.  This can be done using the <see cref="Encrypt(string,byte[],string,int,int)" />
    /// variation of the <b>Encrypt</b> method.  This version accepts a <b>paddedSize</b>
    /// parameter which specifies the minimum size of the data to be encrypted.  The
    /// method seamlessly appends zeros to the source data.  <see cref="Decrypt(string,byte[])" />
    /// will remove this padding before returning the decrypted information.
    /// </para>
    /// <para>
    /// The <see cref="Encrypt(string,byte[],string,int,int,out SymmetricKey)" /> and  
    /// <see cref="Decrypt(string,byte[],out SymmetricKey)" /> variations return the
    /// symmetric algorithm information necessary to perform additional symmetric
    /// encryptions and decryptions using the key and IV generated.  You can pass
    /// these arguments to a <see cref="BlockEncryptor" /> or <see cref="BlockDecryptor" />
    /// or use the <see cref="Encrypt(SymmetricKey,byte[],int)" /> and 
    /// <see cref="Decrypt(SymmetricKey,byte[])" /> methods.
    /// </para>
    /// <para><b><u>Encrypted Block Format</u></b></para>
    /// <para>
    /// Secure data blocks are formatted as described below:
    /// </para>
    /// <code language="none">
    /// 
    ///     +------------------+
    ///     |    Magic Number  |    32-bits: Magic Number=0x41B563AA
    ///     +------------------+
    ///     |      Format      |    32-bits: Format version (0)
    ///     +------------------+
    ///     |                  |    Symmetric encryption algorithm
    ///     |    Encryption    |    and keys encrypted using RSA
    ///     |       Info       |    
    ///     |                  |    (present only if using RSA)
    ///     +------------------+
    ///     |                  |
    ///     |                  |
    ///     |                  |
    ///     |                  |    Source data encrypted using
    ///     |     Contents     |    the symmetric algorithm
    ///     |                  |
    ///     |                  |
    ///     |                  |
    ///     |                  |
    ///     +------------------+
    /// 
    /// </code>
    /// <para>
    /// The <b>Magic Number</b> is provides a quick way to identify
    /// invalid secure data blocks.  <b>Format</b> indicates the block format
    /// version (which is currently set to 0).  The <b>Encryption Info</b>
    /// section specifies the symmetric algorithm, key and initialization
    /// vector to be used to encrypt the rest of the block.  Note that the
    /// <b>Encryption Info</b> section is not present for data blocks encrypted
    /// using the symmetric variation of <see cref="Encrypt(SymmetricKey,byte[],int)" />.
    /// The format for this section is:
    /// </para>
    /// <code language="none">
    /// 
    ///     +------------------+
    ///     | cbEncryptionInfo |    16-bits: Size of the following encrypted
    ///     +------------------+             algorithm and key information
    ///     
    ///     +------------------+
    ///     |   cbAlgorithm    |    16-bits: Size of the UTF-8 encoded algorithm name
    ///     +------------------+
    ///     |                  |
    ///     |     Algorithm    |             The symmetric algorithm name
    ///     |       Name       |             encoded as UTF-8 
    ///     |                  |
    ///     +------------------+
    ///     |      cbKey       |    16-bits: Size of the symmetric key
    ///     +------------------+
    ///     |                  |
    ///     |       Key        |             The encryption key bytes
    ///     |                  |
    ///     +------------------+
    ///     |       cbIV       |    16-bits: Size of the symmetric initialization vector
    ///     +------------------+
    ///     |                  |
    ///     |       IV         |             The encryption initialization vector bytes
    ///     |                  |
    ///     +------------------+
    ///     |                  |
    ///     |       Salt       |    8-bytes: Cryptographic salt
    ///     |                  |
    ///     +------------------+
    ///  
    /// </code>
    /// <para>
    /// All of the encryption information except for the size field
    /// is encrypted using RSA and a public key.
    /// </para>
    /// <para>
    /// The <b>Contents</b> section holds the encrypted contents of the source.
    /// This is encrypted using the symmetric algorithm and key specified in
    /// the Encrypted Info section.  The decrypted format of the content section
    /// is:
    /// </para>
    /// <code language="none">
    /// 
    ///     +------------------+
    ///     |   cbContentInfo  |    32-bits: Size of the following encrypted
    ///     +------------------+             content information
    ///
    ///     +------------------+
    ///     |       Magic      |    32-bits: Magic number=0x41B563AB
    ///     +------------------+
    ///     |       Salt       |    8-bytes: Cryptographic salt
    ///     +------------------+
    ///     |      cbData      |    32-bits: Number of bytes of content 
    ///     +------------------+
    ///     |                  |
    ///     |      Content     |             The contents
    ///     |       Data       |
    ///     |                  |
    ///     +------------------+
    ///     |                  |
    ///     |      Padding     |             Optional padding
    ///     |                  |
    ///     +------------------+
    ///  
    /// </code>
    /// <note>
    /// Note that <b>cbContentInfo</b> is not encrypted and <b>cbContent</b> is 
    /// the size of the actual source content not counting any zero padding added 
    /// to the content block.
    /// </note>
    /// </remarks>
    public static class SecureData
    {
        private const int       Magic        = (int)0x41B563AB;
        private const string    BadFormatMsg = "Invalid secure data format.";

        /// <summary>
        /// Encrypts a byte array using a combination of an asymmetric RSA key and the
        /// specified symmetric encryption algorithm and a one-time key generated by
        /// the method.
        /// </summary>
        /// <param name="rsaKey">The encrypting RSA key as XML or as a secure key container name.</param>
        /// <param name="plainText">The data to be encrypted.</param>
        /// <param name="algorithm">The symmetric encryption algorithm name.</param>
        /// <param name="keySize">The one-time symmetric key size to generate in bits.</param>
        /// <returns>The encrypted result.</returns>
        /// <remarks>
        /// <para>
        /// The current supported cross platform encryption algorithms
        /// are: "DES", "RC2", "TripleDES", and "AES" (Rijndael).
        /// </para>
        /// </remarks>
        /// <exception cref="ArgumentException">Thrown if the requested encryption algorithm is unknown.</exception>
        public static byte[] Encrypt(string rsaKey, byte[] plainText, string algorithm, int keySize)
        {
            SymmetricKey symmetricKey = null;

            try
            {
                return Encrypt(rsaKey, plainText, algorithm, keySize, 0, out symmetricKey);
            }
            finally
            {
                if (symmetricKey != null)
                    symmetricKey.Dispose();
            }
        }

        /// <summary>
        /// Encrypts a byte array using a combination of an asymmetric RSA key and the
        /// specified symmetric encryption algorithm and a one-time key generated by
        /// the method.
        /// </summary>
        /// <param name="rsaKey">The encrypting RSA key as XML or as a secure key container name.</param>
        /// <param name="plainText">The data to be encrypted.</param>
        /// <param name="algorithm">The symmetric encryption algorithm name.</param>
        /// <param name="keySize">The one-time symmetric key size to generate in bits.</param>
        /// <param name="paddedSize">Specifies the minimum padded size of the encrypted content.</param>
        /// <returns>The encrypted result.</returns>
        /// <remarks>
        /// <para>
        /// The current supported cross platform encryption algorithms
        /// are: "DES", "RC2", "TripleDES", and "AES" (Rijndael).
        /// </para>
        /// </remarks>
        /// <exception cref="ArgumentException">Thrown if the requested encryption algorithm is unknown.</exception>
        public static byte[] Encrypt(string rsaKey, byte[] plainText, string algorithm, int keySize, int paddedSize)
        {
            SymmetricKey symmetricKey = null;

            try
            {
                return Encrypt(rsaKey, plainText, algorithm, keySize, paddedSize, out symmetricKey);
            }
            finally
            {
                if (symmetricKey != null)
                    symmetricKey.Dispose();
            }
        }

        /// <summary>
        /// Encrypts a byte array using a combination of an asymmetric RSA key and the
        /// specified symmetric encryption algorithm and a one-time key generated by
        /// the method.
        /// </summary>
        /// <param name="rsaKey">The encrypting RSA key as XML or as a secure key container name.</param>
        /// <param name="plainText">The data to be encrypted.</param>
        /// <param name="algorithm">The symmetric encryption algorithm name.</param>
        /// <param name="keySize">The one-time symmetric key size to generate in bits.</param>
        /// <param name="paddedSize">Specifies the minimum padded size of the encrypted content.</param>
        /// <param name="symmetricKey">Returns as the symmetric encryption algorithm arguments.</param>
        /// <returns>The encrypted result.</returns>
        /// <remarks>
        /// <para>
        /// Note that applications should take some care to ensure that the <paramref name="symmetricKey" />
        /// value return is disposed so that the symmetric encryption key will be cleared.
        /// </para>
        /// <para>
        /// The current supported cross platform encryption algorithms
        /// are: "DES", "RC2", "TripleDES", and "AES" (Rijndael).
        /// </para>
        /// </remarks>
        /// <exception cref="ArgumentException">Thrown if the requested encryption algorithm is unknown.</exception>
        public static byte[] Encrypt(string rsaKey, byte[] plainText, string algorithm, int keySize, int paddedSize,
                                     out SymmetricKey symmetricKey)
        {
            EnhancedMemoryStream    output    = new EnhancedMemoryStream(Math.Max(plainText.Length, paddedSize) + 512);
            EnhancedMemoryStream    ms        = new EnhancedMemoryStream(512);
            BlockEncryptor          encryptor = null;
            byte[]                  symKey;
            byte[]                  symIV;

            Crypto.GenerateSymmetricKey(algorithm, keySize, out symKey, out symIV);

            encryptor    = new BlockEncryptor(algorithm, symKey, symIV);
            symmetricKey = new SymmetricKey(algorithm, (byte[])symKey.Clone(), (byte[])symIV.Clone());

            try
            {
                // Write header fields

                output.WriteInt32(Magic);
                output.WriteInt32(0);

                // Write encryption Info

                ms.WriteString16(algorithm);
                ms.WriteBytes16(symKey);
                ms.WriteBytes16(symIV);
                ms.WriteBytesNoLen(Crypto.GetSalt8());
                output.WriteBytes16(AsymmetricCrypto.Encrypt(CryptoAlgorithm.RSA, rsaKey, ms.ToArray()));

                // Write encrypted contents

                ms.SetLength(0);
                ms.WriteInt32(Magic);
                ms.WriteBytesNoLen(Crypto.GetSalt8());
                ms.WriteBytes32(plainText);

                for (int i = plainText.Length; i < paddedSize; i++)
                    ms.WriteByte((byte)i);     // Padding bytes

                output.WriteBytes32(encryptor.Encrypt(ms.ToArray()));

                // That's it, we're done.

                return output.ToArray();
            }
            finally
            {
                if (symKey != null)
                    Array.Clear(symKey, 0, symKey.Length);

                if (symIV != null)
                    Array.Clear(symIV, 0, symIV.Length);

                if (encryptor != null)
                    encryptor.Dispose();

                output.Close();
                ms.Close();
            }
        }

        /// <summary>
        /// Decrypts a byte array encrypted using <see cref="Encrypt(string,byte[],string,int,int,out SymmetricKey)" />.
        /// </summary>
        /// <param name="rsaKey">The decrypting RSA key as XML or as a secure key container name.</param>
        /// <param name="cipherText">The encrypted data.</param>
        /// <returns>The decrypted data.</returns>
        /// <exception cref="CryptographicException">Thrown is the encrypted data block is incorrectly formatted.</exception>
        public static byte[] Decrypt(string rsaKey, byte[] cipherText)
        {
            SymmetricKey symmetricKey = null;

            try
            {
                return Decrypt(rsaKey, cipherText, out symmetricKey);
            }
            finally
            {
                if (symmetricKey != null)
                    symmetricKey.Dispose();
            }
        }

        /// <summary>
        /// Decrypts a byte array encrypted using <see cref="Encrypt(string ,byte[],string,int,int,out SymmetricKey)" />.
        /// </summary>
        /// <param name="rsaKey">The decrypting RSA key as XML or as a secure key container name.</param>
        /// <param name="cipherText">The encrypted data.</param>
        /// <param name="symmetricKey">Returns as the symmetric encryption algorithm arguments.</param>
        /// <returns>The decrypted data.</returns>
        /// <exception cref="CryptographicException">Thrown is the encrypted data block is incorrectly formatted.</exception>
        /// <remarks>
        /// Note that applications should take some care to ensure that the <paramref name="symmetricKey" />
        /// value return is disposed so that the symmetric encryption key will be cleared.
        /// </remarks>
        public static byte[] Decrypt(string rsaKey, byte[] cipherText, out SymmetricKey symmetricKey)
        {
            EnhancedMemoryStream    input     = new EnhancedMemoryStream(cipherText);
            EnhancedMemoryStream    ms        = new EnhancedMemoryStream(cipherText.Length);
            BlockDecryptor          decryptor = null;
            byte[]                  symKey;
            byte[]                  symIV;
            string                  algorithm;

            try
            {
                // Read the header fields

                if (input.ReadInt32() != Magic)
                    throw new CryptographicException(BadFormatMsg);

                if (input.ReadInt32() != 0)
                    throw new CryptographicException("Unsupported secure data format version.");

                // Decrypt the encryption info

                ms.WriteBytesNoLen(AsymmetricCrypto.Decrypt(CryptoAlgorithm.RSA, rsaKey, input.ReadBytes16()));
                ms.Position = 0;

                algorithm    = ms.ReadString16();
                symKey       = ms.ReadBytes16();
                symIV        = ms.ReadBytes16();
                symmetricKey = new SymmetricKey(algorithm, symKey, symIV);
                decryptor = new BlockDecryptor(algorithm, symKey, symIV);

                // Decrypt the contents

                ms.SetLength(0);
                ms.WriteBytesNoLen(decryptor.Decrypt(input.ReadBytes32()));
                ms.Position = 0;

                if (ms.ReadInt32() != Magic)
                    throw new CryptographicException("Secure data content is corrupt.");

                ms.Position += 8;   // Skip over the salt

                return ms.ReadBytes32();
            }
            finally
            {
                if (decryptor != null)
                    decryptor.Dispose();

                input.Close();
                ms.Close();
            }
        }

        /// <summary>
        /// Performs a secure symmetric encryption including cryptographic salt, padding, and
        /// data validation.
        /// </summary>
        /// <param name="symmetricKey">The symmetric algorithm arguments.</param>
        /// <param name="plainText">The unencrypted data.</param>
        /// <param name="paddedSize">Specifies the minimum padded size of the encrypted content.</param>
        /// <returns>The encrypted result.</returns>
        public static byte[] Encrypt(SymmetricKey symmetricKey, byte[] plainText, int paddedSize)
        {

            EnhancedMemoryStream    output    = new EnhancedMemoryStream(Math.Max(plainText.Length, paddedSize) + 512);
            EnhancedMemoryStream    ms        = new EnhancedMemoryStream(512);
            BlockEncryptor          encryptor = new BlockEncryptor(symmetricKey);

            try
            {
                // Write header fields

                output.WriteInt32(Magic);
                output.WriteInt32(0);

                // Write encrypted contents

                ms.WriteInt32(Magic);
                ms.WriteBytesNoLen(Crypto.GetSalt8());
                ms.WriteBytes32(plainText);

                for (int i = plainText.Length; i < paddedSize; i++)
                    ms.WriteByte((byte)i);     // Padding bytes

                output.WriteBytes32(encryptor.Encrypt(ms.ToArray()));

                // That's it, we're done.

                return output.ToArray();
            }
            finally
            {
                if (encryptor != null)
                    encryptor.Dispose();

                output.Close();
                ms.Close();
            }
        }

        /// <summary>
        /// Decrypts data encrypted using <see cref="Encrypt(SymmetricKey,byte[],int)" />.
        /// </summary>
        /// <param name="symmetricKey">The symmetric algorithm arguments.</param>
        /// <param name="cipherText">The encrypted data.</param>
        /// <returns>The decrypted result.</returns>
        public static byte[] Decrypt(SymmetricKey symmetricKey, byte[] cipherText)
        {
            EnhancedMemoryStream    input     = new EnhancedMemoryStream(cipherText);
            EnhancedMemoryStream    ms        = new EnhancedMemoryStream(cipherText.Length);
            BlockDecryptor          decryptor = null;

            try
            {
                // Read the header fields

                if (input.ReadInt32() != Magic)
                    throw new CryptographicException(BadFormatMsg);

                if (input.ReadInt32() != 0)
                    throw new CryptographicException("Unsupported secure data format version.");

                decryptor = new BlockDecryptor(symmetricKey);

                // Decrypt the contents

                ms.WriteBytesNoLen(decryptor.Decrypt(input.ReadBytes32()));
                ms.Position = 0;

                if (ms.ReadInt32() != Magic)
                    throw new CryptographicException("Secure data content is corrupt.");

                ms.Position += 8;   // Skip over the salt

                return ms.ReadBytes32();
            }
            finally
            {
                if (decryptor != null)
                    decryptor.Dispose();

                input.Close();
                ms.Close();
            }
        }
    }
}
