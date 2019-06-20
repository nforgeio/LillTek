//-----------------------------------------------------------------------------
// FILE:        WebTransferFile.cs
// CONTRIBUTOR: Jeff Lill
// COPYRIGHT:   Copyright (c) 2005-2015 by Jeffrey Lill.  All rights reserved.
// DESCRIPTION: Holds the state for a file being written to a WebTransferCache.

using System;
using System.IO;
using System.Collections.Generic;
using System.Security.Cryptography;

using LillTek.Common;
using LillTek.Cryptography;

namespace LillTek.Web
{
    /// <summary>
    /// Holds the state for a file being written to a <see cref="WebTransferCache" />.
    /// </summary>
    public class WebTransferFile
    {
        /// <summary>
        /// The globally unique ID for this file.
        /// </summary>
        public Guid ID { get; private set; }

        /// <summary>
        /// The fully qualified path to this file (in its non-uploading state).
        /// </summary>
        /// <remarks>
        /// This property <b>does not</b> include the <b>._uploading</b> file name
        /// extension if the file is in the processing of being uploaded.
        /// </remarks>
        public string Path { get; private set; }

        /// <summary>
        /// Returns <c>true</c> if the file is in the process of being uploaded.
        /// </summary>
        public bool IsUploading { get; private set; }

        /// <summary>
        /// Returns the absolute <see cref="Uri" /> to this file on the website.
        /// </summary>
        /// <remarks>
        /// This property <b>does not</b> include the <b>._uploading</b> file name
        /// extension while the file is still in the process of being uploaded.
        /// </remarks>
        public Uri Uri { get; private set; }

        /// <summary>
        /// Constructs an instance for a file being generated locally by the website.
        /// </summary>
        /// <param name="id">The file's globally unique ID</param>
        /// <param name="folderPath">Absolute path to the folder where the file is to be located.</param>
        /// <param name="folderUri">Absolute <see cref="Uri" /> to the virtual folder holding the file.</param>
        /// <param name="fileExtension">The file name extension.</param>
        /// <param name="create">Pass <c>true</c> if we're creating a new file, <c>false</c> to open an existing file.</param>
        /// <exception cref="FileNotFoundException">Thrown if opening an existing file that cannotbe found.</exception>
        public WebTransferFile(Guid id, string folderPath, Uri folderUri, string fileExtension, bool create)
            : this(id, folderPath, folderUri, fileExtension, create, false)
        {
        }

        /// <summary>
        /// Constructs an instance for a file that may be uploaded to the website.
        /// </summary>
        /// <param name="id">The file's globally unique ID.</param>
        /// <param name="folderPath">Absolute path to the folder where the file is to be located.</param>
        /// <param name="folderUri">Absolute <see cref="Uri" /> to the virtual folder holding the file.</param>
        /// <param name="fileExtension">The file name extension.</param>
        /// <param name="create">Pass <c>true</c> if we're creating a new file, <c>false</c> to open an existing file.</param>
        /// <param name="isUploading">
        /// Pass <c>true</c> if the file is being uploaded, <c>false</c> 
        /// if it is being generated locally or it has completed uploading.
        /// </param>
        /// /// <exception cref="FileNotFoundException">Thrown if opening an existing file that cannotbe found.</exception>
        public WebTransferFile(Guid id, string folderPath, Uri folderUri, string fileExtension, bool create, bool isUploading)
        {
            var fileName   = id.ToString("D") + fileExtension;
            var actualPath = System.IO.Path.Combine(folderPath, fileName);

            this.ID          = id;
            this.Path        = actualPath;
            this.Uri         = new Uri(folderUri, fileName);
            this.IsUploading = isUploading;

            if (isUploading)
                actualPath += WebTransferCache.UploadExtension;

            if (create)
            {
                Helper.CreateFileTree(actualPath);
                File.Create(actualPath).Close();
            }
            else if (!File.Exists(actualPath))
                throw new FileNotFoundException(actualPath);
        }

        /// <summary>
        /// Returns a read/write stream to the underlying file system file, creating
        /// the file if it doesn't already exist.
        /// </summary>
        /// <returns>The <see cref="EnhancedFileStream" />.</returns>
        /// <remarks>
        /// <note>
        /// The caller is responsible for closing the stream and also that the
        /// use of a stream returned by this method <b>should not</b> be mixed
        /// with calls to <see cref="Append(byte[])" /> or <see cref="Append(long,byte[])" />.
        /// </note>
        /// </remarks>
        public EnhancedFileStream GetStream()
        {
            var actualPath = this.Path;

            if (IsUploading)
                actualPath += WebTransferCache.UploadExtension;

            return new EnhancedFileStream(actualPath, FileMode.OpenOrCreate, FileAccess.ReadWrite);
        }

        /// <summary>
        /// Appends bytes to the end of the file.
        /// </summary>
        /// <param name="output">The bytes to be written.</param>
        /// <exception cref="IOException">Thrown if the file could not be accessed.</exception>
        /// <remarks>
        /// <note>
        /// Calls to this method should not be performed when a stream returned by
        /// <see cref="GetStream" /> is open.
        /// </note>
        /// </remarks>
        public void Append(byte[] output)
        {
            using (var stream = GetStream())
            {
                stream.Position = stream.Length;
                stream.WriteBytesNoLen(output);
            }
        }

        /// <summary>
        /// Truncates the file to the length specified and the appends
        /// bytes to the end of the file.
        /// </summary>
        /// <param name="length">The truncate length.</param>
        /// <param name="output">The bytes to be written.</param>
        /// <exception cref="IOException">
        /// Thrown if the file could not be accessed or the length of the file 
        /// is less than th value passed.
        /// </exception>
        /// <remarks>
        /// This is useful for situations where the website may support uploading
        /// blocks with retries and its important to ensure that the same block 
        /// is not appended multiple times.
        /// </remarks>
        /// <remarks>
        /// <note>
        /// Calls to this method should not be performed when a stream returned by
        /// <see cref="GetStream" /> is open.
        /// </note>
        /// </remarks>
        public void Append(long length, byte[] output)
        {
            using (var stream = GetStream())
            {
                if (stream.Length < length)
                    throw new IOException(string.Format("Append failed for [{0}] due to file length mismatch.", this.Path));

                if (stream.Length > length)
                    stream.SetLength(length);

                stream.Position = stream.Length;
                stream.WriteBytesNoLen(output);
            }
        }

        /// <summary>
        /// Completes the uploading of the file by stripping the temporary
        /// upload extension from its file name.
        /// </summary>
        /// <exception cref="IOException">Thrown if the file does not exist.</exception>
        /// <exception cref="ArgumentException">Thrown if is is not a upload file.</exception>
        /// <remarks>
        /// <note>
        /// This method <b>does not</b> throw an exception if this method has already
        /// been called to complete the upload.
        /// </note>
        /// </remarks>
        public void Commit()
        {
            string uploadPath = this.Path + WebTransferCache.UploadExtension;

            if (!IsUploading)
                throw new ArgumentException("File is not being uploaded.");

            IsUploading = false;

            if (File.Exists(uploadPath))
                File.Move(uploadPath, this.Path);
            else if (!File.Exists(this.Path))
                throw new IOException(string.Format("Upload file [{0}] does not exist.", this.Path));
        }

        /// <summary>
        /// Completes the uploading of the file by verifying that its contents
        /// match the MD5 signature passed and then stripping the temporary
        /// upload extension from its file name.
        /// </summary>
        /// <param name="md5Signature">A 16-byte array with the file's MD-5 signature.</param>
        /// <exception cref="IOException">Thrown if the file does not exist or the signature doesn't match.</exception>
        /// <exception cref="ArgumentException">Thrown if is is not a upload file.</exception>
        /// <remarks>
        /// <note>
        /// This method <b>does not</b> throw an exception if this method has already
        /// been called to complete the upload and the method will delete the file if 
        /// the signature doesn't match.
        /// </note>
        /// </remarks>
        public void Commit(byte[] md5Signature)
        {
            string      uploadPath = this.Path + WebTransferCache.UploadExtension;
            byte[]      actualSignature;

            if (!IsUploading)
                throw new ArgumentException("File is not being uploaded.");

            if (md5Signature.Length != MD5Hasher.DigestSize)
                throw new ArgumentException("Invalid MD5 signature.", "md5Signature");

            if (File.Exists(uploadPath))
            {
                using (var stream = GetStream())
                {
                    actualSignature = MD5Hasher.Compute(stream, stream.Length);
                }

                if (!Helper.ArrayEquals(md5Signature, actualSignature))
                {
                    Helper.DeleteFile(uploadPath);
                    throw new IOException("Upload failure: MD5 signature mismatch.");
                }

                IsUploading = false;

                File.Move(uploadPath, this.Path);
            }
            else if (!File.Exists(this.Path))
                throw new IOException(string.Format("Upload file [{0}] does not exist.", this.Path));
        }

        /// <summary>
        /// Encrypts the committed file using the specified symmetric encryption key.
        /// </summary>
        /// <param name="key">The symmetric encryption key.</param>
        /// <exception cref="ArgumentNullException">Thrown if <paramref name="key" /> is <c>null</c>.</exception>
        /// <exception cref="InvalidOperationException">Thrown if the file has not been committed to the cache.</exception>
        /// <exception cref="IOException">Thrown if there's a problem reading or writing file data.</exception>
        /// <exception cref="CryptographicException">Thrown if there's any encryption failure.</exception>
        /// <remarks>
        /// <note>
        /// This method may be called only on files that have been already been committed to the cache.
        /// </note>
        /// </remarks>
        public void Encrypt(SymmetricKey key)
        {
            if (key == null)
                throw new ArgumentNullException("key");

            if (IsUploading)
                throw new ArgumentNullException(string.Format("Cannot encrypt file [{0}] until it has been committed to the cache.", ID));

            if (!File.Exists(this.Path))
                throw new InvalidOperationException(string.Format("Cannot encrypt web transfer file with ID [{0}] because it does not exist.", ID));

            var encryptedTempPath = this.Path + ".encrypting";

            using (var encryptor = new StreamEncryptor(key))
            {
                encryptor.Encrypt(this.Path, encryptedTempPath);
            }

            File.Delete(this.Path);
            File.Move(encryptedTempPath, this.Path);
        }
    }
}
