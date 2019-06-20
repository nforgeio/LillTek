//-----------------------------------------------------------------------------
// FILE:        WebFileStore.cs
// CONTRIBUTOR: Jeff Lill
// COPYRIGHT:   Copyright (c) 2005-2015 by Jeffrey Lill.  All rights reserved.
// DESCRIPTION: Defines the behavior of a file store that can be used by websites
//              to store and deliver user generated content.  Concrete implementations
//              of this will include stores build on the Windows file system as
//              well as stores built on AWS S3 or Windows Azure.

using System;
using System.IO;
using System.Collections.Generic;

using LillTek.Common;
using LillTek.Cryptography;

namespace LillTek.Web
{
    /// <summary>
    /// Defines the behavior of a file store that can be used by websites
    /// to store and deliver user generated content.  Concrete implementations
    /// of this will include stores build on the Windows file system as
    /// well as stores built on AWS S3 or Windows Azure.
    /// </summary>
    /// <remarks>
    /// <para>
    /// This abstract class defines several methods that are passed local file system
    /// paths as well as file store path.  Local file system paths are just standard
    /// Windows file system paths.  File store paths follow more of a Linux style
    /// convention where forward slashes are used as directory separators and the
    /// paths are rooted with a leading forward slash.
    /// </para>
    /// <note>
    /// All store paths must be absolute, meaning that they must have a leading
    /// forward slash.  File stores do not have the concept of a current directory
    /// and have no concept of a relative path.
    /// </note>
    /// <para>
    /// The case insensitivity of store file paths is indeterminate.  Some implementations,
    /// such as <see cref="NativeWebFileStore" /> actually store the files in a
    /// Windows file system so in this case the file paths will be case insensitive.
    /// Other implementations such as one for AWS S3 will be case sensitive because
    /// S3 is inherently case insenstive.  This means that applications should take
    /// care to define file paths with consistent character casing and not depend
    /// on being able to have two files with names that differ only by character case.
    /// </para>
    /// <para>
    /// In addition to forward slashes, store file paths may include of letters,
    /// digits, underscores, periods, and dashes.  No other characters are allowed.
    /// </para>
    /// </remarks>
    public abstract class WebFileStore : IDisposable
    {
        /// <summary>
        /// Copies a file from the local file system to the file store.
        /// </summary>
        /// <param name="localPath">Local file system path.</param>
        /// <param name="storePath">Store file system path.</param>
        /// <exception cref="InvalidOperationException">Thrown when the store has been closed.</exception>
        /// <exception cref="ArgumentException">Thrown when an invalid parameter is passed.</exception>
        /// <exception cref="IOException">Thrown if an I/O operation failed.</exception>
        /// <remarks>
        /// <note>
        /// This method will create any folders required before copying the file.
        /// </note>
        /// </remarks>
        public abstract void CopyTo(string localPath, string storePath);

        /// <summary>
        /// Copies a file from the store to the local file system.
        /// </summary>
        /// <param name="storePath">Store file system path.</param>
        /// <param name="localPath">Local file system path.</param>
        /// <exception cref="InvalidOperationException">Thrown when the store has been closed.</exception>
        /// <exception cref="ArgumentException">Thrown when an invalid parameter is passed.</exception>
        /// <exception cref="IOException">Thrown if an I/O operation failed.</exception>
        public abstract void CopyFrom(string storePath, string localPath);

        /// <summary>
        /// Determines whether a file exists in the file store.
        /// </summary>
        /// <param name="storePath">Store file system path.</param>
        /// <returns><c>true</c> if the file exists.</returns>
        /// <exception cref="InvalidOperationException">Thrown when the store has been closed.</exception>
        /// <exception cref="ArgumentException">Thrown when an invalid parameter is passed.</exception>
        /// <exception cref="IOException">Thrown if an I/O operation failed.</exception>
        public abstract bool Exists(string storePath);

        /// <summary>
        /// Deletes a file from the store.
        /// </summary>
        /// <param name="storePath">Store file system path.</param>
        /// <exception cref="InvalidOperationException">Thrown when the store has been closed.</exception>
        /// <exception cref="ArgumentException">Thrown when an invalid parameter is passed.</exception>
        /// <exception cref="IOException">Thrown if an I/O operation failed.</exception>
        /// <remarks>
        /// <note>
        /// This method <b>does not</b> throw an exception if the file doesn't exist.
        /// </note>
        /// </remarks>
        public abstract void Delete(string storePath);

        /// <summary>
        /// Enumerates files in the specified directory in the store.
        /// </summary>
        /// <param name="storeDirectory">The store directory path.</param>
        /// <param name="searchPattern">The file name search pattern including <b>?</b> and <b>*</b> wildcards.</param>
        /// <returns>A collection of the fully qualified store paths of the matching files.</returns>
        /// <exception cref="InvalidOperationException">Thrown when the store has been closed.</exception>
        /// <exception cref="ArgumentException">Thrown when an invalid parameter is passed.</exception>
        /// <exception cref="IOException">Thrown if an I/O operation failed.</exception>
        public abstract IEnumerable<string> EnumerateFiles(string storeDirectory, string searchPattern);

        /// <summary>
        /// Returns the <see cref="Uri" /> that can be used to access this file from
        /// the public Internet.
        /// </summary>
        /// <param name="storePath">Store file system path.</param>
        /// <returns>The access <see cref="Uri" />.</returns>
        /// <remarks>
        /// <note>
        /// This method <b>does not</b> check to see if the referenced file actually exists.
        /// </note>
        /// </remarks>
        /// <exception cref="InvalidOperationException">Thrown when the store has been closed.</exception>
        /// <exception cref="ArgumentException">Thrown when an invalid parameter is passed.</exception>
        /// <exception cref="IOException">Thrown if an I/O operation failed.</exception>
        public abstract Uri GetUri(string storePath);

        /// <summary>
        /// Releases an resources associated with the store.
        /// </summary>
        /// <remarks>
        /// <note>
        /// This method will not throw an exception if the instance is already closed.
        /// </note>
        /// </remarks>
        public abstract void Close();

        /// <summary>
        /// Releases all resources associate with the store.
        /// </summary>
        /// <remarks>
        /// <note>
        /// This is equivalent to calling <see cref="Close" />.
        /// </note>
        /// </remarks>
        public void Dispose()
        {
            Close();
        }

        //---------------------------------------------------------------------
        // Helper methods

        /// <summary>
        /// Validates the store path passed.
        /// </summary>
        /// <param name="storePath">The store path.</param>
        /// <exception cref="ArgumentException">Thrown if the path is not valid.</exception>
        protected void ValidateStorePath(string storePath)
        {
            if (storePath == null)
                throw new ArgumentException("storePath");

            if (string.IsNullOrWhiteSpace(storePath))
                throw new ArgumentException("storePath", "WebFileStore path cannot be empty.");

            if (storePath[0] != '/')
                throw new ArgumentException("storePath", "WebFileStore path must start with a forward slash (/).");

            // Make sure there are no empty directories.

            if (storePath.IndexOf("//") != -1)
                throw new ArgumentException("storePath", "WebFileStore path contains an empty directory name.");

            // Verify the characters

            foreach (var ch in storePath)
            {
                if (Char.IsLetterOrDigit(ch) || ch == '_' || ch == '-' || ch == '.' || ch == '/')
                    continue;
                else
                    throw new ArgumentException("storePath", string.Format("Illegal character [{0}] in WebFileStorePath.", ch));
            }
        }
    }
}
