//-----------------------------------------------------------------------------
// FILE:        SecureFile.cs
// CONTRIBUTOR: Jeff Lill
// COPYRIGHT:   Copyright (c) 2005-2015 by Jeffrey Lill.  All rights reserved.
// DESCRIPTION: Implements a mechanism to encrypt a file along with its metadata using
//              a combination of asymmetric and symmetric keys.

using System;
using System.Collections.Generic;
using System.IO;
using System.Security.Cryptography;
using System.Text;

using LillTek.Common;

// $todo(jeff.lill): 
//
// At some point I'd like to implement instance based hashers
// so that it would be possible to calculate a hash digest
// incrementally as a stream is read or written rather than
// having to run another pass over the stream using the
// static methods as I do in this class implementation.

namespace LillTek.Cryptography
{
    /// <summary>
    /// Implements a mechanism to encrypt a file along with its metadata using
    /// a combination of asymmetric and symmetric keys.
    /// </summary>
    /// <remarks>
    /// <para>
    /// This class implements a mechanism for encrypting the contents and metadata
    /// associated with a file using a combination of asymmetric and symmetric
    /// encryption algorithms.
    /// </para>
    /// <para>
    /// Secure files can be opened in either <see cref="SecureFileMode.Decrypt" /> 
    /// or <see cref="SecureFileMode.Encrypt" /> mode
    /// by passing one of the <see cref="SecureFileMode" /> parameters to the
    /// one of the constructors.  The constructors also take an input stream or 
    /// the path to a file as well as an RSA encryption key encoded as XML or
    /// as a key container as detailed in <see cref="AsymmetricCrypto" />.
    /// For files opened in <see cref="SecureFileMode.Decrypt" /> mode, the key must be the private
    /// key.  For files opened in <see cref="SecureFileMode.Encrypt" /> mode, the key must be the
    /// public key.
    /// </para>
    /// <para>
    /// Metadata about the file can be added via the <see cref="ArgCollection" />
    /// returned by the <see cref="Properties" /> property.  This is also
    /// used internally by the class to store information about the file
    /// and the encryption used.  Internal property names are prefixed with
    /// the underscore (_) character.  Applications should avoid using
    /// property names beginning with an underscore.
    /// </para>
    /// <para>
    /// The <see cref="FileName" />, <see cref="FullPath" />, <see cref="CreateTimeUtc" />, 
    /// and <see cref="WriteTimeUtc" />, properties provide a way to access the underlying file
    /// metadata.
    /// </para>
    /// <para>
    /// For files opened in <see cref="SecureFileMode.Encrypt" /> mode, the <see cref="EncryptTo(string,string,int)" />
    /// and <see cref="EncryptTo(EnhancedStream,string,int)" /> methods are used to encrypt the file
    /// metadata and contents.  For files opened in <see cref="SecureFileMode.Decrypt" /> mode, the <see cref="DecryptTo(string)" />
    /// and <see cref="DecryptTo(EnhancedStream)" /> methods are used the decrypt
    /// the file contents.
    /// </para>
    /// <para>
    /// This class also implements the static <see cref="Validate(EnhancedStream,string)" /> 
    /// and <see cref="Validate(string,string)" /> methods.  These can be used to verify that 
    /// a secure file is intact and properly formatted.  <see cref="GetPublicKey(EnhancedStream)" />
    /// and <see cref="GetPublicKey(string)" /> can be used to extract the public RSA key
    /// used to encrypt the file (if the <see cref="SavePublicKey" /> property was set
    /// to <c>true</c> when the file was encrypted.
    /// </para>
    /// <para>
    /// <b>SecureFile</b> implements the <see cref="IDisposable" /> interface
    /// and must be explicitly disposed via a call to <see cref="Dispose" />
    /// or <see cref="Close" /> to ensure that all resources associated with
    /// the file are released promptly.
    /// </para>
    /// <para><b>File Format</b></para>
    /// <para>
    /// Secure files are stored using the following basic file format:
    /// </para>
    /// <code language="none">
    /// 
    ///     +------------------+
    ///     |    Magic Number  |    32-bits: Magic Number=0x41B563AA
    ///     +------------------+
    ///     |      Format      |    32-bits: Format version (0)
    ///     +------------------+
    ///     |   cbPublicKey    |    16-bits: Size of the UTF-8 encoded key
    ///     +------------------+             or ushort.MinValue if null
    ///     |                  |
    ///     |                  |
    ///     |                  |
    ///     |                  |    The RSA public key used to encrypt the
    ///     |    Public Key    |    symmetric encryption algorithm and 
    ///     |        XML       |    key.  Formatted as UTF-8 encoded XML.
    ///     |                  |
    ///     |                  |    May be empty. 
    ///     |                  |
    ///     |                  |
    ///     +------------------+
    ///     |    Encryption    |    Symmetric encryption algorithm
    ///     |       Info       |    and keys encrypted using RSA
    ///     +------------------+
    ///     |                  |    
    ///     |     Metadata     |    Metadata about the file encrypted
    ///     |                  |    using the symmetric algorithm
    ///     +------------------+
    ///     |                  |
    ///     |                  |
    ///     |                  |
    ///     |                  |    File contents encrypted using
    ///     |     Contents     |    the symmetric algorithm
    ///     |                  |
    ///     |                  |
    ///     |                  |
    ///     |                  |
    ///     +------------------+
    ///     |    SHA512 Hash   |    64-bytes: Hash of the metadata and contents
    ///     +------------------+              in their encrypted form
    /// 
    /// </code>
    /// <para>
    /// The <b>Magic Number</b> is provides a quick way to identify
    /// invalid secure files.  <b>Format</b> indicates the file format
    /// version (which is currently set to 0).
    /// </para>
    /// <para>
    /// The <b>Public Key</b> fields are used to store the public RSA key 
    /// used to encrypt the symmetric algorithm and key information that 
    /// follows.  It is often useful to be able to identify which key was 
    /// used to encrypt the file, especially when files may be archived for
    /// long periods of time and when there's the possibility that
    /// multiple keys may have been used over this period.  This field
    /// is initialized by default.  Set <see cref="SavePublicKey" /> to 
    /// <c>false</c> to save an empty string instead.
    /// </para>
    /// <para>
    /// The <b>Encryption Info</b> section specifies the symmetric algorithm, 
    /// key and initialization vector to be used to encrypt the rest of the file.  
    /// The format for this section is:
    /// </para>
    /// <code language="none">
    /// 
    ///     +------------------+
    ///     | cbEncryptionInfo |    32-bits: Size of the following encrypted
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
    /// The <b>Metadata</b> section is formatted as shown:
    /// </para>
    /// <code language="none">
    /// 
    ///     +------------------+
    ///     |    cbEncrypted   |    32-bits: Size of the encrypted metadata (unencrypted)
    ///     +------------------+
    /// 
    ///     +------------------+
    ///     |       Magic      |    32-bits: Magic Number=0x41B563AA
    ///     +------------------+
    ///     |    cbMetadata    |    32-bits: Size of the metadata string that follows
    ///     +------------------+
    ///     |                  |
    ///     |                  |             The metadata properties as generated
    ///     |    Properties    |             by <see cref="ArgCollection.ToString" /> using
    ///     |                  |             TAB separators then encoded as UTF-8.  Note that  
    ///     |                  |             one property contains 8 bytes of salt.
    ///     +------------------+
    /// 
    /// </code>
    /// <para>
    /// Note that the <b>cbEncrypted</b> field is not encrypted but the
    /// remaining fields are encrypted using the symmetric algorithm.
    /// The <b>Magic</b> field is present so that the class can quickly determine
    /// whether the asymmetric private key was valid.  <b>Properties</b> is
    /// simply the file's metadata encoded as name/value pairs via the
    /// <see cref="ArgCollection" /> class.
    /// </para>
    /// <para>
    /// The <b>Contents</b> section holds the encrypted contents of the file.
    /// This is a series of one or more 4102 byte blocks of encrypted data.
    /// Each block is encrypted separately and has the following decrypted
    /// format:
    /// </para>
    /// <code language="none">
    /// 
    ///     +------------------+
    ///     |    cbEncrypted   |    2-bytes: Size of the encrypted block
    ///     +-------------------
    /// 
    ///     +------------------+
    ///     |       Magic      |    32-bits: Magic number=0x41B563AA
    ///     +------------------+
    ///     |       Salt       |    4-bytes: Cryptographic salt
    ///     +------------------+
    ///     |      cbData      |    16-bits: Number of bytes of content in this block
    ///     +------------------+
    ///     |                  |
    ///     |      Content     |             The contents
    ///     |       Data       |
    ///     |                  |
    ///     +------------------+
    ///     |                  |
    ///     |   Zero Padding   |             Zero padding bytes to fill the block
    ///     |                  |             out to 4102 bytes total
    ///     +------------------+
    ///  
    /// </code>
    /// <note>
    /// A zero length input file will be encoded using a single 
    /// content block full of zero padding and the salt.  This will hide
    /// from bad guys the fact that the file had no content data.
    /// </note>
    /// </remarks>
    public class SecureFile : IDisposable
    {
        //---------------------------------------------------------------------
        // Static members

        /// <summary>
        /// Returns the public RSA key used to encrypt a secure file.
        /// </summary>
        /// <param name="input">The input stream.</param>
        /// <returns>
        /// The public RSA key encoded as XML or <c>null</c> if 
        /// the public key was not saved to the file.
        /// </returns>
        /// <exception cref="CryptographicException">Thrown if the stream is not formatted as a valid secure file.</exception>
        public static string GetPublicKey(EnhancedStream input)
        {
            SecureFile  secureFile = null;
            long        orgPos     = input.Position;

            try
            {
                secureFile = new SecureFile(input, SecureFileMode.Decrypt, null);
                return secureFile.PublicKey;
            }
            finally
            {
                input.Position = orgPos;

                if (secureFile != null)
                    secureFile.Close();
            }
        }

        /// <summary>
        /// Returns the public RSA key used to encrypt a secure file.
        /// </summary>
        /// <param name="path">The path to the secure file.</param>
        /// <returns>
        /// The public RSA key encoded as XML or <c>null</c> if 
        /// the public key was not saved to the file.
        /// </returns>
        /// <exception cref="CryptographicException">Thrown if the file is not formatted as a valid secure file.</exception>
        public static string GetPublicKey(string path)
        {
            SecureFile secureFile = null;

            try
            {
                secureFile = new SecureFile(path, SecureFileMode.Decrypt, null);
                return secureFile.PublicKey;
            }
            finally
            {
                if (secureFile != null)
                    secureFile.Close();
            }
        }

        /// <summary>
        /// Determines whether a file appears to be an encrypted <see cref="SecureFile" />.
        /// </summary>
        /// <param name="path">The path to the file.</param>
        /// <returns><c>true</c> if the file is a <see cref="SecureFile" />.</returns>
        public static bool IsSecureFile(string path)
        {
            using (var input = new EnhancedFileStream(path, FileMode.Open, FileAccess.Read))
            {
                return IsSecureFile(input);
            }
        }

        /// <summary>
        /// Determines whether a stream appears to contain an encrypted <see cref="SecureFile" />.
        /// </summary>
        /// <param name="input">The <see cref="EnhancedStream" /> to be tested.</param>
        /// <returns><c>true</c> if the stream is a <see cref="SecureFile" />.</returns>
        public static bool IsSecureFile(EnhancedStream input)
        {
            long orgPos = input.Position;

            try
            {
                return input.ReadInt32() == Magic &&    // Magic number
                       input.ReadInt32() == 0;          // Format version
            }
            catch
            {
                return false;
            }
            finally
            {
                input.Position = orgPos;
            }
        }

        /// <summary>
        /// Verifies that the stream passed holds an intact secure file
        /// that can be decrypted with the specified key.
        /// </summary>
        /// <param name="input">The input stream.</param>
        /// <param name="key">The RSA key encoded as XML or as a key container <see cref="AsymmetricCrypto" /></param>
        /// <returns><c>true</c> if the secure file is intact.</returns>
        public static bool Validate(EnhancedStream input, string key)
        {
            SecureFile  secureFile = null;
            long        orgPos     = input.Position;

            try
            {
                secureFile = new SecureFile(input, SecureFileMode.Decrypt, key);
                secureFile.DecryptTo(EnhancedStream.Null);
                return true;
            }
            catch
            {
                return false;
            }
            finally
            {
                input.Position = orgPos;

                if (secureFile != null)
                    secureFile.Close();
            }
        }

        /// <summary>
        /// Verifies that the file specified is an intact secure file
        /// that can be decrypted with the specified key.
        /// </summary>
        /// <param name="path">The path to the secure file.</param>
        /// <param name="key">The RSA key encoded as XML or as a key container <see cref="AsymmetricCrypto" /></param>
        /// <returns><c>true</c> if the secure file is intact.</returns>
        public static bool Validate(string path, string key)
        {
            SecureFile secureFile = null;

            try
            {
                secureFile = new SecureFile(path, SecureFileMode.Decrypt, key);
                secureFile.DecryptTo(EnhancedStream.Null);
                return true;
            }
            catch
            {
                return false;
            }
            finally
            {
                if (secureFile != null)
                    secureFile.Close();
            }
        }

        /// <summary>
        /// Deletes a file without leaving any readable traces by writing alternating
        /// ones and zeros to the file before deleting it.
        /// </summary>
        /// <param name="path">Path of the file to be deleted.</param>
        /// <param name="wipeCount">The number of times to overwrite the file before deleting it.</param>
        /// <remarks>
        /// <note>This method does not throw an exception if the file does not exist.</note>
        /// <note>
        /// The file will always be overwritten at least one time regardless of the 
        /// value passed in <paramref name="wipeCount" />.
        /// </note>
        /// </remarks>
        /// <exception cref="IOException">Thrown if there's an error wiping or deleting the file.</exception>
        public static void WipeAndDelete(string path, int wipeCount)
        {
            if (!File.Exists(path))
                return;

            const int   bufSize = 64 * 1024;
            byte[]      buf     = new byte[bufSize + 1];
            long        cbFile;
            long        cbRemain;

            // Initialize the buffer with alternating 0x00 and
            // 0xFF values.

            for (int i = 0; i < buf.Length; i++)
                buf[i] = (byte)((i & 1) == 0 ? 0x00 : 0xFF);

            // The buffer is one byte longer than the number of bytes
            // of data we're going to write to the file in one shot.
            // For each successive wipe pass, I'm going to alternate
            // between writing the data starting at offset 0 and 1
            // so that the actual bytes written to disk will alternate
            // between 0x00 and 0xFF at each pass.

            if (wipeCount < 1)
                wipeCount = 1;

            using (FileStream fs = new FileStream(path, FileMode.Open, FileAccess.Write))
            {
                cbFile = fs.Length;

                for (int i = 0; i < wipeCount; i++)
                {
                    cbRemain = cbFile;
                    fs.Position = 0;
                    while (cbRemain > 0)
                    {
                        int cb;

                        if (cbRemain < bufSize)
                            cb = (int)cbRemain;
                        else
                            cb = bufSize;

                        fs.Write(buf, i & 1, cb);
                        cbRemain -= cb;
                    }

                    // Make sure any file buffers are flushed to disk.

                    // $todo(jeff.lill)
                    //
                    // Note that advanced RAID arrays may retain the changes in battery
                    // backup RAM and ignore the flush request (to improve performance).
                    // I'm not quite sure how to handle this.  I know that the Windows
                    // CreateFile() API has a flag that is supposed to disable buffering.
                    // I'm not convinced though that the RAID board will honor this
                    // flag, so this may take some research to figure out.

                    fs.Flush();
                }
            }

            File.Delete(path);
        }

        //---------------------------------------------------------------------
        // Instance members

        private const int       Magic        = (int)0x41B563AA;
        private const int       HeaderSize   = 4 + 4 + 2;
        private const int       DataSize     = 4096;
        private const int       BlockSize    = HeaderSize + DataSize;
        private const string    BadFormatMsg = "Invalid secure file format.";
        private const char      PropAssign   = '=';
        private const char      PropSep      = (char)Helper.TAB;

        private SecureFileMode  mode;           // The mode
        private KeyChain        keyChain;       // The associated key chain (or null)
        private string          privateKey;     // The RSA private key
        private string          publicKey;      // The RSA public key
        private bool            savePubicKey;   // True to write and save the public key
        private ArgCollection   properties;     // The file metadata
        private EnhancedStream  input;          // The input stream
        private bool            closeInput;     // True if the input stream belongs to
                                                // the instance and should be closed
                                                // by the Close() method
        private BlockDecryptor  decryptor;      // The decryptor for Decrypt files
        private string          fileName;       // The file properties
        private string          fullPath;
        private DateTime        createTime;
        private DateTime        writeTime;
        private string          symmetricAlgorithm;
        private byte[]          symmetricKey;
        private byte[]          symmetricIV;

        /// <summary>
        /// Constructs a secure file instance and associates it with the specified
        /// file path.
        /// </summary>
        /// <param name="path">The path to the file.</param>
        /// <param name="mode">The file mode.</param>
        /// <param name="key">The RSA key encoded as XML or as a key container <see cref="AsymmetricCrypto" />.</param>
        /// <remarks>
        /// <para>
        /// The <paramref name="mode" /> parameter indicates whether the file is to be opened
        /// in <see cref="SecureFileMode.Decrypt" /> or <see cref="SecureFileMode.Encrypt" />
        /// mode.
        /// </para>
        /// <para>
        /// Opening a secure file in <see cref="SecureFileMode.Decrypt" /> mode means that the file
        /// being opened has already been encrypted using a combination of
        /// asymmetric and symmetric keys.  In this situation, the <pararef name="key" />
        /// parameter passed must be the private RSA key.  After the instance
        /// is constructed, the metadata properties will be initialized to
        /// the values decrypted from the file.  One of the <b>DecryptTo()</b>
        /// methods can be used to decrypt the file contents.
        /// </para>
        /// <para>
        /// Opening a secure file in <see cref="SecureFileMode.Encrypt" /> mode means that that the
        /// file has not yet been encrypted.  In this case the <paramref name="key" />
        /// parameter passed must be the public RSA key.  After the instance
        /// is constructed, the metadata properties will be initialized with
        /// the values associated with the specified file.  One of the <b>EncryptTo()</b>
        /// methods can be used to encrypt the file metadata and contents.
        /// </para>
        /// <note>
        /// <see cref="Dispose" /> or <see cref="Close" /> should
        /// be called to ensure that all resources are promptly released.
        /// </note>
        /// </remarks>
        /// <exception cref="CryptographicException">Thrown in <see cref="SecureFileMode.Decrypt" /> mode if the file format is not valid.</exception>
        public SecureFile(string path, SecureFileMode mode, string key)
        {
            this.mode         = mode;
            this.keyChain     = null;
            this.privateKey   = key;
            this.publicKey    = key == null ? null : AsymmetricCrypto.GetPublicKey(CryptoAlgorithm.RSA, key);
            this.savePubicKey = true;
            this.properties   = new ArgCollection(PropAssign, PropSep);
            this.input        = new EnhancedFileStream(path, FileMode.Open, FileAccess.Read);
            this.closeInput   = true;

            if (mode == SecureFileMode.Encrypt)
            {

                this.decryptor  = null;
                this.fileName   = Path.GetFileName(path);
                this.fullPath   = Path.GetFullPath(path);
                this.createTime = File.GetCreationTimeUtc(path);
                this.writeTime  = File.GetLastWriteTimeUtc(path);
            }
            else
                InitDecrypt();
        }

        /// <summary>
        /// Constructs a secure file instance in prepraration for decryption,
        /// associating it with a source file path and a <see cref="KeyChain" />.
        /// </summary>
        /// <param name="path">The path to the encrypted source file.</param>
        /// <param name="keyChain">The <see cref="KeyChain" />.</param>
        /// <remarks>
        /// <para>
        /// Opening a secure file in <see cref="SecureFileMode.Decrypt" /> mode means that the file
        /// being opened has already been encrypted using a combination of
        /// asymmetric and symmetric keys.  In this situation, the <paramref name="keyChain" />
        /// parameter must hold the private key matching the public key used to encrypt
        /// the file.  After the instance is constructed, the metadata properties
        /// will be initialized to the values decrypted from the file.  One of the 
        /// <b>DecryptTo()</b> methods can be used to decrypt the file contents.
        /// </para>
        /// <note>
        /// <see cref="Dispose" /> or <see cref="Close" /> should
        /// be called to ensure that all resources are promptly released.
        /// </note>
        /// </remarks>
        /// <exception cref="CryptographicException">
        /// Thrown if the file format is not valid, the public key was not 
        /// saved in the file, or a matching private key was not present in
        /// the key chain.
        /// </exception>
        public SecureFile(string path, KeyChain keyChain)
        {
            this.mode         = SecureFileMode.Decrypt;
            this.keyChain     = keyChain;
            this.privateKey   = null;
            this.publicKey    = null;
            this.savePubicKey = false;
            this.properties   = new ArgCollection(PropAssign, PropSep);
            this.input        = new EnhancedFileStream(path, FileMode.Open, FileAccess.Read);
            this.closeInput   = true;

            InitDecrypt();
        }

        /// <summary>
        /// Constructs a secure file instance and associates it with the specified
        /// file path.
        /// </summary>
        /// <param name="input">The input stream.</param>
        /// <param name="mode">The file mode.</param>
        /// <param name="key">The RSA key encoded as XML or as a key container <see cref="AsymmetricCrypto" /></param>
        /// <remarks>
        /// <para>
        /// The <paramref name="mode" /> parameter indicates whether the file is to be opened
        /// in <see cref="SecureFileMode.Decrypt" /> or <see cref="SecureFileMode.Encrypt" />
        /// mode.
        /// </para>
        /// <para>
        /// Opening a secure file in <see cref="SecureFileMode.Decrypt" /> mode means that the file
        /// being opened has already been encrypted using a combination of
        /// asymmetric and symmetric keys.  In this situation, the <pararef name="key" />
        /// parameter passed must be the private RSA key.  After the instance
        /// is constructed, the metadata properties will be initialized to
        /// the values decrypted from the file.  One of the <b>DecryptTo()</b>
        /// methods can be used to decrypt the stream contents.
        /// </para>
        /// <para>
        /// Opening a secure file in <see cref="SecureFileMode.Encrypt" /> mode means that that the
        /// file has not yet been encrypted.  In this case the <pararef name="key" />
        /// parameter passed must be the public RSA key.  After the instance
        /// is constructed, the metadata properties will be initialized with
        /// the current time.  One of the <b>EncryptTo()</b> methods can be used 
        /// to encrypt the stream metadata and contents.
        /// </para>
        /// <note>
        /// <see cref="Dispose" /> or <see cref="Close" /> should
        /// be called to ensure that all resources are promptly released.
        /// </note>
        /// </remarks>
        /// <exception cref="CryptographicException">Thrown in <see cref="SecureFileMode.Decrypt" /> mode if the file format is not valid.</exception>
        public SecureFile(EnhancedStream input, SecureFileMode mode, string key)
        {
            this.mode         = mode;
            this.keyChain     = null;
            this.privateKey   = key;
            this.publicKey    = key == null ? null : AsymmetricCrypto.GetPublicKey(CryptoAlgorithm.RSA, key);
            this.savePubicKey = true;
            this.properties   = new ArgCollection(PropAssign, PropSep);
            this.input        = input;
            this.closeInput   = false;

            if (mode == SecureFileMode.Encrypt)
            {

                this.decryptor  = null;
                this.fileName   = string.Empty;
                this.fullPath   = string.Empty;
                this.createTime =
                this.writeTime  = DateTime.UtcNow;
            }
            else
                InitDecrypt();
        }

        /// <summary>
        /// Constructs a secure file instance in prepraration for decryption,
        /// associating it with a source stream and a <see cref="KeyChain" />.
        /// </summary>
        /// <param name="input">The encrypted input stream.</param>
        /// <param name="keyChain">The <see cref="KeyChain" />.</param>
        /// <remarks>
        /// <para>
        /// Opening a secure file in <see cref="SecureFileMode.Decrypt" /> mode means that the file
        /// being opened has already been encrypted using a combination of
        /// asymmetric and symmetric keys.  In this situation, the <paramref name="keyChain" />
        /// parameter must hold the private key matching the public key used to encrypt
        /// the file.  After the instance is constructed, the metadata properties
        /// will be initialized to the values decrypted from the file.  One of the 
        /// <b>DecryptTo()</b> methods can be used to decrypt the file contents.
        /// </para>
        /// <note>
        /// <see cref="Dispose" /> or <see cref="Close" /> should
        /// be called to ensure that all resources are promptly released.
        /// </note>
        /// </remarks>
        /// <exception cref="CryptographicException">
        /// Thrown if the file format is not valid, the public key was not 
        /// saved in the file, or a matching private key was not present in
        /// the key chain.
        /// </exception>
        public SecureFile(EnhancedStream input, KeyChain keyChain)
        {
            this.mode         = SecureFileMode.Decrypt;
            this.keyChain     = keyChain;
            this.privateKey   = null;
            this.publicKey    = null;
            this.savePubicKey = false;
            this.properties   = new ArgCollection(PropAssign, PropSep);
            this.input        = input;
            this.closeInput   = false;

            InitDecrypt();
        }

        /// <summary>
        /// Implement a finalizer to ensure that the symmetric key and IV
        /// are zeroed even if the instance isn't explicitly closed.
        /// </summary>
        ~SecureFile()
        {
            Close();
        }

        /// <summary>
        /// Performs common initialization for <see cref="SecureFileMode.Decrypt" /> mode files.
        /// </summary>
        /// <remarks>
        /// Reads the file header, encryption information, and metadata.
        /// </remarks>
        private void InitDecrypt()
        {
            var ms = new EnhancedMemoryStream(512);

            // Read the file header info

            if (input.ReadInt32() != Magic)
                throw new CryptographicException(BadFormatMsg);

            if (input.ReadInt32() != 0)
                throw new CryptographicException("Unsupported secure file format version.");

            try
            {
                // Read the public key

                if (privateKey != null)
                    input.ReadString16();
                else
                    publicKey = input.ReadString16();

                savePubicKey = !string.IsNullOrWhiteSpace(publicKey);

                if (keyChain != null)
                {
                    if (string.IsNullOrWhiteSpace(publicKey))
                        throw new CryptographicException("Key chain lookup failed because the public key was not saved to the secure file.");

                    privateKey = keyChain.GetPrivateKey(publicKey);
                }
                else if (privateKey == null)
                    return;     // This happens only within GetPublicKey()

                // Read the encryption info

                ms.WriteBytesNoLen(AsymmetricCrypto.Decrypt(CryptoAlgorithm.RSA, privateKey, input.ReadBytes32()));
                ms.Position = 0;

                symmetricAlgorithm = ms.ReadString16();
                symmetricKey       = ms.ReadBytes16();
                symmetricIV        = ms.ReadBytes16();
                decryptor          = new BlockDecryptor(symmetricAlgorithm, symmetricKey, symmetricIV);

                // Decrypt the metadata section

                ms.Position = 0;
                ms.WriteBytesNoLen(decryptor.Decrypt(input.ReadBytes32()));
                ms.Position = 0;

                // Read and verify the magic number

                if (ms.ReadInt32() != Magic)
                    throw new CryptographicException("Invalid asymmetric key.");

                // Read the metadata

                properties = new ArgCollection(ms.ReadString32(), PropAssign, PropSep);

                fileName = properties["_FileName"];
                if (fileName == null)
                    throw new Exception();

                fullPath = properties["_FullPath"];
                if (fullPath == null)
                    throw new Exception();

                createTime = Helper.ParseInternetDate(properties["_CreateTime"]);
                writeTime = Helper.ParseInternetDate(properties["_WriteTime"]);
            }
            catch (CryptographicException)
            {
                if (input != null)
                    input.Close();

                throw;
            }
            catch (Exception e)
            {
                if (input != null)
                    input.Close();

                throw new CryptographicException(BadFormatMsg, e);
            }
            finally
            {
                if (ms != null)
                    ms.Close();
            }
        }

        /// <summary>
        /// Releases all resources associated with the file.
        /// </summary>
        /// <remarks>
        /// <note>
        /// It is not an error to call this more than once for
        /// an instance.
        /// </note>
        /// </remarks>
        public void Close()
        {
            if (decryptor != null)
            {
                decryptor.Dispose();
                decryptor = null;
            }

            if (input != null && closeInput)
            {
                input.Close();
                input = null;
            }

            // Zero the symmetric key and IV if present.

            if (symmetricKey != null)
                Array.Clear(symmetricKey, 0, symmetricKey.Length);

            if (symmetricIV != null)
                Array.Clear(symmetricIV, 0, symmetricIV.Length);

            GC.SuppressFinalize(this);
        }

        /// <summary>
        /// Releases all resources associated with the file.
        /// </summary>
        /// <remarks>
        /// <note>
        /// It is not an error to call this more than once for
        /// an instance.
        /// </note>
        /// </remarks>
        public void Dispose()
        {
            Close();
        }

        /// <summary>
        /// Indicates whether the public RSA key used to encrypt the
        /// symmetric algorithm and key is to be present in the 
        /// secured file.
        /// </summary>
        /// <remarks>
        /// <para>
        /// The RSA public key used to encrypt the file is saved
        /// to the file in clear text by default.  This is useful
        /// for situations where files may be archived over a long
        /// period of time and when multiple RSA private keys may be
        /// used during this period.
        /// </para>
        /// <para>
        /// This is not really a security problem, since the private
        /// key is still needed to decrypt the file, but some security
        /// policies may require that no part of the private key be
        /// stored in the file.
        /// </para>
        /// <para>
        /// Set the <see cref="SavePublicKey" /> property to <c>false</c>
        /// when encrypting a file in this situation.  For decrypted
        /// files, this property will return <c>true</c> if the
        /// public key is available.
        /// </para>
        /// </remarks>
        public bool SavePublicKey
        {
            get { return savePubicKey; }
            set { savePubicKey = value; }
        }

        /// <summary>
        /// Returns the private RSA key.
        /// </summary>
        public string PrivateKey
        {
            get { return privateKey; }
        }

        /// <summary>
        /// Returns the RSA public key.
        /// </summary>
        /// <remarks>
        /// <para>
        /// For instances created to encrypt a file, this returns as the XML for
        /// the public key to be used to encrypt it.
        /// </para>
        /// <para>
        /// For instances created to decrypt a file, this returns as the XML
        /// for the public key if the key was saved to the file, <c>null</c>
        /// if <see cref="SavePublicKey" /> was set to <c>false</c> when the file
        /// was encrypted and the key was not saved.
        /// </para>
        /// </remarks>
        public string PublicKey
        {
            get { return publicKey; }
        }

        /// <summary>
        /// The original file name.
        /// </summary>
        public string FileName
        {
            get { return fileName; }
            set { fileName = value; }
        }

        /// <summary>
        /// The original file's fully qualified path.
        /// </summary>
        public string FullPath
        {
            get { return fullPath; }
            set { fullPath = value; }
        }

        /// <summary>
        /// The original file creation time.
        /// </summary>
        public DateTime CreateTimeUtc
        {
            get { return createTime; }
            set { createTime = value; }
        }

        /// <summary>
        /// The original file's last modification time.
        /// </summary>
        public DateTime WriteTimeUtc
        {
            get { return writeTime; }
            set { writeTime = value; }
        }

        /// <summary>
        /// A <see cref="ArgCollection" /> instance holding the file properties
        /// as a set of name/value pairs.
        /// </summary>
        public ArgCollection Properties
        {
            get { return properties; }
        }

        /// <summary>
        /// For secure files opened in <see cref="SecureFileMode.Encrypt" /> mode,
        /// this method can be used to encrypt the file and metadata to a new
        /// file at the path specified.
        /// </summary>
        /// <param name="path">The path of the encrypted file to be written.</param>
        /// <param name="algorithm">The symmetric encryption algorithm name.</param>
        /// <param name="keySize">The one-time symmetric key size to generate in bits.</param>
        /// <remarks>
        /// <note>
        /// <b>EncryptTo()</b> may be called only once for each secure file instance.
        /// </note>
        /// <para>
        /// The current supported cross platform encryption algorithms
        /// are: "DES", "RC2", "TripleDES", and "AES" (Rijndael).
        /// </para>
        /// </remarks>
        /// <exception cref="InvalidOperationException">Thrown if the file was not opened in <see cref="SecureFileMode.Encrypt" /> mode.</exception>
        /// <exception cref="ObjectDisposedException">Thrown if <b>EncryptTo()</b> has already been called or the file has been closed.</exception>
        /// <exception cref="ArgumentException">Thrown if the requested encryption algorithm is unknown.</exception>
        public void EncryptTo(string path, string algorithm, int keySize)
        {
            var output = new EnhancedFileStream(path, FileMode.Create, FileAccess.ReadWrite);

            try
            {
                EncryptTo(output, algorithm, keySize);
            }
            catch
            {
                output.Close();
                output = null;
                File.Delete(path);

                throw;
            }
            finally
            {
                if (output != null)
                    output.Close();
            }
        }

        /// <summary>
        /// For secure files opened in <see cref="SecureFileMode.Encrypt" /> mode,
        /// this method can be used to encrypt the file and metadata to a new
        /// file at the path specified using a one-time 256-bit AES key.
        /// </summary>
        /// <param name="path">The path of the encrypted file to be written.</param>
        /// <remarks>
        /// <note>
        /// <b>EncryptTo()</b> may be called only once for each secure file instance.
        /// </note>
        /// </remarks>
        /// <exception cref="InvalidOperationException">Thrown if the file was not opened in <see cref="SecureFileMode.Encrypt" /> mode.</exception>
        /// <exception cref="ObjectDisposedException">Thrown if <b>EncryptTo()</b> has already been called or the file has been closed.</exception>
        public void EncryptTo(string path)
        {
            EncryptTo(path, CryptoAlgorithm.AES, 256);
        }

        /// <summary>
        /// For secure files opened in <see cref="SecureFileMode.Encrypt" /> mode,
        /// this method can be used to encrypt the file and metadata to a
        /// stream.
        /// </summary>
        /// <param name="output">The stream to receive the encrypted output.</param>
        /// <param name="algorithm">The symmetric encryption algorithm name.</param>
        /// <param name="keySize">The one-time symmetric key size to generate in bits.</param>
        /// <remarks>
        /// <note>
        /// <b>EncryptTo()</b> may be called only once for each secure file instance.
        /// </note>
        /// <para>
        /// The current supported cross platform encryption algorithms
        /// are: "DES", "RC2", "TripleDES", and "AES" (Rijndael).
        /// </para>
        /// </remarks>
        /// <exception cref="InvalidOperationException">Thrown if the file was not opened in <see cref="SecureFileMode.Encrypt" /> mode.</exception>
        /// <exception cref="ObjectDisposedException">Thrown if <b>EncryptTo()</b> has already been called or the file has been closed.</exception>
        /// <exception cref="ArgumentException">Thrown if the requested encryption algorithm is unknown.</exception>
        public void EncryptTo(EnhancedStream output, string algorithm, int keySize)
        {
            BlockEncryptor          encryptor = null;
            EnhancedMemoryStream    ms        = new EnhancedMemoryStream(256);
            byte[]                  block     = new byte[BlockSize];
            EnhancedBlockStream     bs        = new EnhancedBlockStream(block);
            byte[]                  hash;
            int                     cb;
            byte[]                  key;
            byte[]                  IV;
            long                    contentPos;
            long                    contentLen;
            long                    hashPos;
            bool                    firstBlock;

            Crypto.GenerateSymmetricKey(algorithm, keySize, out key, out IV);
            encryptor = new BlockEncryptor(algorithm, key, IV);

            try
            {
                // Write the magic number and format version

                output.WriteInt32(Magic);
                output.WriteInt32(0);

                // Write the public key

                if (savePubicKey)
                    output.WriteString16(AsymmetricCrypto.GetPublicKey(CryptoAlgorithm.RSA, privateKey));
                else
                    output.WriteString16(null);

                // Write the symmetric encryption information

                ms.WriteString16(algorithm);
                ms.WriteBytes16(key);
                ms.WriteBytes16(IV);
                ms.WriteBytesNoLen(Crypto.GetSalt8());
                output.WriteBytes32(AsymmetricCrypto.Encrypt(CryptoAlgorithm.RSA, privateKey, ms.ToArray()));

                // Initialize the properties and then write the metadata section.

                properties["_FileName"]   = fileName;
                properties["_FullPath"]   = fullPath;
                properties["_CreateTime"] = Helper.ToInternetDate(createTime);
                properties["_WriteTime"]  = Helper.ToInternetDate(writeTime);
                properties["_Salt"]       = Helper.ToHex(Crypto.GetSalt8());

                ms.SetLength(0);
                ms.WriteInt32(Magic);
                ms.WriteBytes32(Helper.ToUTF8(properties.ToString()));
                output.WriteBytes32(encryptor.Encrypt(ms.ToArray()));

                // Remember the first position of the first byte of the
                // encrypted contents so we'll be able to go back and
                // compute the hash.

                contentPos = output.Position;

                // Write the encrypted contents

                firstBlock = true;
                while (true)
                {
                    Array.Clear(block, 0, BlockSize);
                    cb = input.Read(block, HeaderSize, DataSize);
                    if (cb == 0 && !firstBlock)
                        break;

                    bs.Position = 0;
                    bs.WriteInt32(Magic);
                    bs.WriteBytesNoLen(Crypto.GetSalt4());
                    bs.WriteInt16(cb);
                    output.WriteBytes16(encryptor.Encrypt(block, 0, BlockSize));

                    firstBlock = false;
                }

                // Remember the hash record position and then go back
                // and calculate the SHA512 hash for the contents and
                // then write the hash.

                hashPos = output.Position;
                contentLen = hashPos - contentPos;

                if (contentLen > int.MaxValue)
                    throw new NotSupportedException("Content size exceeds 2GB.");

                output.Position = contentPos;
                hash = SHA512Hasher.Compute(output, contentLen);
                output.Position = hashPos;

                output.WriteBytesNoLen(hash);
            }
            finally
            {
                if (ms != null)
                    ms.Close();

                if (bs != null)
                    bs.Close();

                if (encryptor != null)
                    encryptor.Dispose();

                Close();
            }
        }

        /// <summary>
        /// For secure files opened in <see cref="SecureFileMode.Decrypt" /> mode,
        /// this method can be used to decrypt the file contents to a new
        /// file at the path specified.
        /// </summary>
        /// <param name="path">The path where the decrypted contents are to be written.</param>
        /// <remarks>
        /// <para>
        /// The method will set the output file's creation and modification dates to
        /// the values retained from the encrypted input file metadata.
        /// </para>
        /// <note>
        /// <b>DecryptTo()</b> may be called only once for each secure file instance.
        /// </note>
        /// </remarks>
        /// <exception cref="InvalidOperationException">Thrown if the file was not opened in <see cref="SecureFileMode.Decrypt" /> mode.</exception>
        /// <exception cref="CryptographicException">Thrown in <see cref="SecureFileMode.Decrypt" /> mode if the file format is not valid.</exception>
        /// <exception cref="ObjectDisposedException">Thrown if <b>DecryptTo()</b> has already been called or the file has been closed.</exception>
        public void DecryptTo(string path)
        {
            var output = new EnhancedFileStream(path, FileMode.Create, FileAccess.ReadWrite);

            try
            {
                DecryptTo(output);
            }
            catch
            {
                output.Close();
                output = null;
                File.Delete(path);

                throw;
            }
            finally
            {
                if (output != null)
                {
                    output.Close();

                    File.SetCreationTimeUtc(path, createTime);
                    File.SetLastWriteTimeUtc(path, writeTime);
                }
            }
        }

        /// <summary>
        /// For secure files opened in <see cref="SecureFileMode.Decrypt" /> mode,
        /// this method can be used to decrypt the file contents to a stream.
        /// </summary>
        /// <param name="output">The stream to receive the decrypted output.</param>
        /// <remarks>
        /// <note>
        /// <b>DecrypFromt()</b> may be called only once for each secure file instance.
        /// </note>
        /// </remarks>
        /// <exception cref="InvalidOperationException">Thrown if the file was not opened in <see cref="SecureFileMode.Decrypt" /> mode.</exception>
        /// <exception cref="CryptographicException">Thrown in <see cref="SecureFileMode.Decrypt" /> mode if the file format is not valid.</exception>
        /// <exception cref="ObjectDisposedException">Thrown if <b>DecryptTo()</b> has already been called or the file has been closed.</exception>
        public void DecryptTo(EnhancedStream output)
        {
            byte[]                  block = new byte[BlockSize];
            EnhancedBlockStream     bs    = new EnhancedBlockStream(block);
            long                    contentLen;
            long                    contentPos;
            long                    hashPos;
            byte[]                  hash, calcHash;
            int                     cbData;

            try
            {
                // Determine the position of the hash record in the file then
                // compute the SHA512 hash for the encrypted content, read the
                // hash record, and then make sure that the two hashes match.

                contentPos = input.Position;
                hashPos    = input.Length - SHA512Hasher.DigestSize;
                contentLen = hashPos - contentPos;

                if (contentLen < 0 || contentLen > int.MaxValue)
                    throw new CryptographicException(BadFormatMsg);

                calcHash       = SHA512Hasher.Compute(input, contentLen);
                input.Position = hashPos;
                hash           = input.ReadBytes(SHA512Hasher.DigestSize);

                for (int i = 0; i < SHA512Hasher.DigestSize; i++)
                    if (hash[i] != calcHash[i])
                        throw new CryptographicException("Invalid file SHA512 hash digest.");

                // Now go back and decrypt the file and write the results
                // to the output stream.

                input.Position = contentPos;
                while (true)
                {
                    if (input.Position >= hashPos)
                        break;

                    bs.Position = 0;
                    bs.WriteBytesNoLen(decryptor.Decrypt(input.ReadBytes16()));

                    bs.Position = 0;
                    if (bs.ReadInt32() != Magic)
                        throw new CryptographicException("Corrupt content block.");

                    bs.Position = 8;    // Skip over the magic number and salt
                    cbData = bs.ReadInt16();
                    output.Write(block, 8 + 2, cbData);
                }
            }
            catch
            {
                throw new CryptographicException(BadFormatMsg);
            }
            finally
            {
                if (bs != null)
                    bs.Close();
            }
        }
    }
}
