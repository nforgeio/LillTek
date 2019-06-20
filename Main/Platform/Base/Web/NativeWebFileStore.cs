//-----------------------------------------------------------------------------
// FILE:        NativeWebFileStore.cs
// CONTRIBUTOR: Jeff Lill
// COPYRIGHT:   Copyright (c) 2005-2015 by Jeffrey Lill.  All rights reserved.
// DESCRIPTION: Implements a simple IWebFileStore using the native file system.

using System;
using System.IO;
using System.Collections.Generic;

using LillTek.Common;
using LillTek.Cryptography;

namespace LillTek.Web
{
    /// <summary>
    /// Implements a simple <see cref="WebFileStore" /> using the native file system.
    /// </summary>
    public class NativeWebFileStore : WebFileStore
    {
        private bool    isOpen;
        private string  rootPath;
        private Uri     rootUri;

        /// <summary>
        /// Constructor.
        /// </summary>
        /// <param name="rootPath">The path to the root directory of the store in the file system.</param>
        /// <param name="rootUri">The root <see cref="Uri"/> to the file system.</param>
        /// <remarks>
        /// <note>
        /// This method will create the root folder if it doesn't already exist.
        /// </note>
        /// </remarks>
        /// <exception cref="ArgumentException">Thrown when an invalid parameter is passed.</exception>
        /// <exception cref="IOException">Thrown if an I/O operation failed.</exception>
        public NativeWebFileStore(string rootPath, Uri rootUri)
        {
            if (rootPath == null)
                throw new ArgumentNullException("rootPath");

            if (rootUri == null)
                throw new ArgumentNullException("rootUri");

            rootPath = Path.GetFullPath(rootPath);  // Make sure we have an absolute path
            Helper.CreateFolderTree(rootPath);

            this.isOpen   = true;
            this.rootPath = rootPath;
            this.rootUri  = rootUri;
        }

        /// <summary>
        /// Throws an exception if the instance is in an invalid state.
        /// </summary>
        private void ValidateState()
        {
            if (!isOpen)
                throw new InvalidOperationException("NativeWebFileStore is closed.");
        }

        /// <summary>
        /// Converts a file store path into an absolute file system path.
        /// </summary>
        /// <param name="storePath">The file store path.</param>
        /// <returns>The absolute file system path.</returns>
        private string GetNativePath(string storePath)
        {
            return Path.Combine(rootPath, storePath.Substring(1).Replace('/', '\\'));
        }

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
        public override void CopyTo(string localPath, string storePath)
        {
            string path;

            ValidateState();
            ValidateStorePath(storePath);

            path = GetNativePath(storePath);

            Helper.CreateFileTree(path);
            File.Copy(localPath, GetNativePath(storePath));
        }

        /// <summary>
        /// Copies a file from the store to the local file system.
        /// </summary>
        /// <param name="storePath">Store file system path.</param>
        /// <param name="localPath">Local file system path.</param>
        /// <exception cref="InvalidOperationException">Thrown when the store has been closed.</exception>
        /// <exception cref="ArgumentException">Thrown when an invalid parameter is passed.</exception>
        /// <exception cref="IOException">Thrown if an I/O operation failed.</exception>
        public override void CopyFrom(string storePath, string localPath)
        {
            ValidateState();
            ValidateStorePath(storePath);

            File.Copy(GetNativePath(storePath), localPath);
        }

        /// <summary>
        /// Determines whether a file exists in the file store.
        /// </summary>
        /// <param name="storePath">Store file system path.</param>
        /// <returns><c>true</c> if the file exists.</returns>
        /// <exception cref="InvalidOperationException">Thrown when the store has been closed.</exception>
        /// <exception cref="ArgumentException">Thrown when an invalid parameter is passed.</exception>
        /// <exception cref="IOException">Thrown if an I/O operation failed.</exception>
        public override bool Exists(string storePath)
        {
            ValidateState();
            ValidateStorePath(storePath);

            return File.Exists(GetNativePath(storePath));
        }

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
        public override void Delete(string storePath)
        {
            ValidateState();
            ValidateStorePath(storePath);

            var path = GetNativePath(storePath);

            try
            {
                if (File.Exists(path))
                    File.Delete(path);
            }
            catch (IOException)
            {

                // Ignoring
            }
        }

        /// <summary>
        /// Enumerates files in the specified directory in the store.
        /// </summary>
        /// <param name="storeDirectory">The store directory path.</param>
        /// <param name="searchPattern">The file name search pattern including <b>?</b> and <b>*</b> wildcards.</param>
        /// <returns>A collection of the fully qualified store paths of the matching files.</returns>
        /// <exception cref="InvalidOperationException">Thrown when the store has been closed.</exception>
        /// <exception cref="ArgumentException">Thrown when an invalid parameter is passed.</exception>
        /// <exception cref="IOException">Thrown if an I/O operation failed.</exception>
        public override IEnumerable<string> EnumerateFiles(string storeDirectory, string searchPattern)
        {
            ValidateState();
            ValidateStorePath(storeDirectory);

            // $todo(jeff.lill):
            //
            // Right now I'm simply returning a list of strings.  At some point, I'd
            // like to come back and implement a custom enumerator that will take
            // advantage of the File.EnumerateFiles() capabilities.

            List<string> files = new List<string>();
            string nativePath = GetNativePath(storeDirectory);

            foreach (var path in Directory.EnumerateFiles(nativePath))
            {
                // Basically all we need to do is strip the leading native rootPath from
                // the results and replace back slashes with forward slashes.

                Assertion.Test(path.Length > rootPath.Length);

                files.Add(path.Substring(rootPath.Length).Replace('\\', '/'));
            }

            return files;
        }

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
        public override Uri GetUri(string storePath)
        {
            ValidateState();
            ValidateStorePath(storePath);

            return new Uri(rootUri, storePath.Substring(1));     // Strip the leading forward slash
        }

        /// <summary>
        /// Releases an resources associated with the store.
        /// </summary>
        /// <remarks>
        /// <note>
        /// This method will not throw an exception if the instance is already closed.
        /// </note>
        /// </remarks>
        public override void Close()
        {
            isOpen = false;
        }
    }
}
