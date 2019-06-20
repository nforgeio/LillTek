//-----------------------------------------------------------------------------
// FILE:        Package.cs
// CONTRIBUTOR: Jeff Lill
// COPYRIGHT:   Copyright (c) 2005-2015 by Jeffrey Lill.  All rights reserved.
// DESCRIPTION: Packages a hierarchical collection of files into a simple archive 
//              file format.

using System;
using System.IO;
using System.Collections;
using System.Diagnostics;

using LillTek.Common;
using LillTek.Cryptography;

// $todo(jeff.lill): This is super old code.  This really should be converted to ZIP.

namespace LillTek.Install
{
    /// <summary>
    /// Packages a hierarchical collection of files into a simple archive file.
    /// </summary>
    /// <remarks>
    /// <para>
    /// The each package entry (or file) is named by a path similar to a
    /// file system path.  Folder hierarchy is indicated using the forward
    /// slash (/) character.  The root folder is indicated using a single
    /// forward slash.
    /// </para>
    /// <para>
    /// <b><u>Package File Format</u></b>
    /// </para>
    /// <para>
    /// Package files are formatted as binary records: a Package Header
    /// record followed a list of Package Entry records, terminated by
    /// a special end-of-list Package Entry.
    /// </para>
    /// <code language="none">
    /// Package Header
    /// --------------
    /// Header Cookie    int32       (0x70D37211) validates the file type
    ///
    /// Format Version   int32       File format version (1).
    ///
    /// MD5 Hash         byte[16]    MD5 hash of the package contents
    ///                              from the first byte after the header up
    ///                              to an including the special end-of-list
    ///                              Package Entry.
    ///
    /// Folder Package Entry
    /// --------------------
    /// Package Cookie   int32       (0x70D37212) validates the record
    /// Entry Type       byte        1 = Folder
    /// Name             int32:utf8  Full path name of the folder
    /// Content Size     int32       Number of bytes of content data (0)
    ///
    /// File Package Entry
    /// ------------------
    /// Package Cookie   int32       (0x70D37212) validates the record
    /// Entry Type       byte        2 = File
    /// Name             int32:utf8  Full path name of the file
    /// Content Size     int32       Number of bytes of content data
    /// Content Data     byte[]      File data
    ///
    /// End-Of-Packages Entry
    /// ---------------------
    /// Package Cookie   int32       (0x70D37212) validates the record
    /// Entry Type       byte        0 = End of list
    /// Name             int32:utf8  0 length UTF-8 string
    /// Content Size     int32       Number of bytes of content data (0)
    /// </code>
    /// <para>
    /// <note>
    /// The entries are not stored in any particular order.
    /// </note>
    /// </para>
    /// </remarks>
    public sealed class Package : IDisposable
    {
        //---------------------------------------------------------------------
        // Private classes

        /// <summary>
        /// Implements the header file format.
        /// </summary>
        private sealed class PackageHeader
        {
            private const int HeaderCookie = 0x70D37211;

            public int      FormatVersion;
            public byte[]   Hash;

            public PackageHeader()
            {
                this.FormatVersion = 1;
                this.Hash          = new byte[16];
            }

            public PackageHeader(EnhancedStream es)
            {
                if (es.ReadInt32() != HeaderCookie)
                    throw new PackageException("Invalid package file signature.");

                if (es.ReadInt32() != 1)
                    throw new PackageException("Unsupported package file format version.");

                this.FormatVersion = 1;
                this.Hash = es.ReadBytes(16);
            }

            public void Seralize(EnhancedStream es)
            {
                es.WriteInt32(HeaderCookie);
                es.WriteInt32(1);
                es.WriteBytesNoLen(Hash);
            }
        }

        //---------------------------------------------------------------------
        // Implementation

        internal const string   BadFileFormat  = "Invalid package format.";
        internal const string   CorruptPackage = "Package is corrupt.";
        internal const string   ReadOnly       = "Package was opened for read.";
        internal const string   WriteOnly      = "Package was opened for write.";
        internal const string   AlreadyExists  = "[{0}] already exists.";
        internal const string   NotFolder      = "This is not a folder.";
        internal const string   NotFile        = "This is not a file.";
        internal const string   InvalidPath    = "Invalid path.";
        internal const string   MustBeFolder   = "[{0}] is not a folder.";

        internal const int      CopyBufSize       = 8192;
        internal static char[]  BadChars      = new char[] { '*', '&', '$', '>', '<', '|', '\\' };

        private PackageEntry    root;           // The root entry.
        private EnhancedStream  packageIn;      // Input stream (or null)
        private EnhancedStream  packageOut;     // Output stream (or null)
        private Hashtable       entries;        // PackageEntries keyed by full path (uppercased)
        private int             cbHeader;       // Size of the serialized package header

        /// <summary>
        /// Instantiates an empty file package for writing.  Call <see cref="Create(string)" />
        /// to indicate where the package should be written.
        /// </summary>
        public Package()
        {
            this.entries    = new Hashtable();
            this.root       = new PackageEntry(this, null, string.Empty, true);
            this.packageIn  = null;
            this.packageOut = null;

            entries.Add(string.Empty, root);
        }

        /// <summary>
        /// Opens a file package for reading.
        /// </summary>
        /// <param name="path">The package file name.</param>
        public Package(string path)
            : this()
        {
            this.packageIn = new EnhancedFileStream(path, FileMode.Open, FileAccess.Read);
            Load();
        }

        /// <summary>
        /// Opens a file package for reading.
        /// </summary>
        /// <param name="es">The package stream.</param>
        public Package(EnhancedStream es)
            : this()
        {
            this.packageIn = es;
            Load();
        }

        /// <summary>
        /// Writes a package header with a zeroed MD5 hash.  We'll come back
        /// and update this when we compute the hash during <see cref="Close()" />.
        /// </summary>
        private void Create()
        {
            var header = new PackageHeader();

            packageOut.SetLength(0);
            header.Seralize(packageOut);
            cbHeader = (int)packageOut.Position;
        }

        /// <summary>
        /// Associates the package with a file for writing.
        /// </summary>
        /// <param name="path">The output file name.</param>
        public void Create(string path)
        {
            this.packageOut = new EnhancedFileStream(path, FileMode.CreateNew, FileAccess.ReadWrite);
            Create();
        }

        /// <summary>
        /// Associates the package with a stream for writing.
        /// </summary>
        /// <param name="es">The output stream.</param>
        public void Create(EnhancedStream es)
        {
            this.packageOut = es;
            Create();
        }

        /// <summary>
        /// Closes the package.
        /// </summary>
        public void Close()
        {
            Close(false);
        }

        /// <summary>
        /// Closes the package, optionally leaving the output stream open.
        /// </summary>
        /// <param name="leaveOutputOpen"><c>true</c> to leave the output stream open.</param>
        public void Close(bool leaveOutputOpen)
        {
            if (packageOut != null)
            {
                // Append the terminating entry.

                new PackageEntry(this).Serialize(packageOut);

                // Before we close the package, we need to compute the MD5
                // hash of everything after the header in the output stream
                // and update the hash in the header.

                var header = new PackageHeader();

                packageOut.Position = cbHeader;
                header.Hash         = MD5Hasher.Compute(packageOut, packageOut.Length - cbHeader);

                packageOut.Position = 0;
                header.Seralize(packageOut);

                if (!leaveOutputOpen)
                    packageOut.Close();

                packageOut = null;
            }

            if (packageIn != null)
            {
                packageIn.Close();
                packageIn = null;
            }
        }

        /// <summary>
        /// Releases resources associated with this instance.
        /// </summary>
        public void Dispose()
        {
            Close();
        }

        /// <summary>
        /// Returns the package's underlying input stream.
        /// </summary>
        internal EnhancedStream Input
        {
            get
            {
                if (packageIn == null)
                    throw new PackageException(WriteOnly);

                return packageIn;
            }
        }

        /// <summary>
        /// Scans the input stream, validating its structure and loading
        /// the package entries.
        /// </summary>
        private void Load()
        {
            PackageHeader   header;
            byte[]          hash;

            packageIn.Position = 0;
            header             = new PackageHeader(packageIn);
            cbHeader           = (int)packageIn.Position;

            // Compute a MD5 hash on the rest of then file and compare
            // the result to what we read from the header.

            hash = MD5Hasher.Compute(packageIn, packageIn.Length - cbHeader);
            if (hash.Length != header.Hash.Length)
                throw new PackageException(CorruptPackage);

            for (int i = 0; i < hash.Length; i++)
                if (hash[i] != header.Hash[i])
                    throw new PackageException(CorruptPackage);

            // Read the package entry headers.

            packageIn.Position = cbHeader;
            while (true)
            {
                var entry = new PackageEntry(this, packageIn);

                if (entry.IsEol)
                    break;

                entries.Add(entry.FullName.ToUpper(), entry);
            }

            // Walk back through the entries and link each entry to
            // its parent folder.

            foreach (PackageEntry entry in entries.Values)
            {
                PackageEntry    parent;
                int             pos;

                if (entry == root)
                    continue;

                pos = entry.FullName.LastIndexOf('/');
                if (pos == -1)
                    throw new PackageException(CorruptPackage);

                if (pos == 0)
                    parent = root;
                else
                {
                    parent = (PackageEntry)entries[entry.FullName.Substring(0, pos).ToUpper()];
                    if (parent == null)
                        throw new PackageException(CorruptPackage);
                }

                entry.SetParent(parent);
                parent.AddChild(entry);
            }
        }

        /// <summary>
        /// Verifies that a fully qualified package path is reasonable.
        /// </summary>
        /// <param name="packagePath">The path to check.</param>
        private void CheckPath(string packagePath)
        {
            if (packagePath.Length == 0 ||
                packagePath[0] != '/' ||
                packagePath.IndexOfAny(BadChars) != -1 ||
                packagePath.IndexOf("//") != -1)

                throw new PackageException(InvalidPath);
        }

        /// <summary>
        /// Adds the package passed to the entry hierarchy, creating folders
        /// as necessary.
        /// </summary>
        /// <param name="entry">The new entry.</param>
        private void AddEntry(PackageEntry entry)
        {
            PackageEntry    parent;
            string          path;
            int             pos, posEnd;

            // Make sure that all of the folders up 
            // the hierarchy exist.

            parent = root;
            pos    = 1;
            while (true)
            {

                posEnd = entry.FullName.IndexOf('/', pos);
                if (posEnd == -1)
                    break;

                path   = entry.FullName.Substring(0, posEnd);
                parent = this[path];
                if (parent != null && !parent.IsFolder)
                    throw new PackageException(MustBeFolder, path);
                else if (parent == null)
                    parent = AddFolder(path);

                pos = posEnd + 1;
            }

            entry.SetParent(parent);
            parent.AddChild(entry);

            entries.Add(entry.FullName.ToUpper(), entry);
        }

        /// <summary>
        /// Adds a folder entry to the package.
        /// </summary>
        /// <param name="packagePath">The fully wualified name of the folder to be added.</param>
        /// <returns>The package entry added.</returns>
        public PackageEntry AddFolder(string packagePath)
        {
            PackageEntry entry;

            if (packageOut == null)
                throw new PackageException(ReadOnly);

            CheckPath(packagePath);
            if (entries[packagePath.ToUpper()] != null)
                throw new PackageException(AlreadyExists, packagePath);

            entry = new PackageEntry(this, packagePath, true);
            entry.SetSize(0);

            AddEntry(entry);
            entry.Serialize(packageOut);

            return entry;
        }

        /// <summary>
        /// Adds a file entry to the package.
        /// </summary>
        /// <param name="packagePath">The fully qualified name of the file to be added.</param>
        /// <param name="buffer">The entry data to be added.</param>
        /// <returns>The package entry added.</returns>
        public PackageEntry AddFile(string packagePath, byte[] buffer)
        {
            PackageEntry entry;

            if (packageOut == null)
                throw new PackageException(ReadOnly);

            CheckPath(packagePath);
            if (entries[packagePath.ToUpper()] != null)
                throw new PackageException(AlreadyExists, packagePath);

            entry = new PackageEntry(this, packagePath, false);
            entry.SetSize(buffer.Length);

            AddEntry(entry);
            entry.Serialize(packageOut);
            packageOut.Write(buffer, 0, buffer.Length);

            return entry;
        }

        /// <summary>
        /// Adds a file entry to the package by copying the contents of a
        /// file system file.
        /// </summary>
        /// <param name="packagePath">The fully qualified name of the file to be added.</param>
        /// <param name="path">The path to the file system file to be copied.</param>
        public PackageEntry AddFile(string packagePath, string path)
        {
            FileStream file;

            if (packageOut == null)
                throw new PackageException(ReadOnly);

            file = new FileStream(path, FileMode.Open, FileAccess.Read);
            try
            {
                return AddFile(packagePath, file, (int)file.Length);
            }
            finally
            {
                file.Close();
            }
        }

        /// <summary>
        /// Adds a file entry to the package by copying data from a stream.
        /// </summary>
        /// <param name="packagePath">The fully qualified name of the file to be added.</param>
        /// <param name="input">The input stream.</param>
        /// <param name="size">The number of bytes to copy.</param>
        /// <returns>The package entry added.</returns>
        /// <remarks>
        /// The input stream's position will be advanced past the last
        /// byte read.
        /// </remarks>
        public PackageEntry AddFile(string packagePath, Stream input, int size)
        {
            PackageEntry    entry;
            byte[]          buffer;
            int             cb, cbRemain;

            if (packageOut == null)
                throw new PackageException(ReadOnly);

            CheckPath(packagePath);
            if (entries[packagePath.ToUpper()] != null)
                throw new PackageException(AlreadyExists, packagePath);

            entry = new PackageEntry(this, packagePath, false);
            entry.SetSize(size);

            AddEntry(entry);
            entry.Serialize(packageOut);

            buffer = new byte[CopyBufSize];
            cbRemain = entry.Size;

            while (cbRemain > 0)
            {
                cb = cbRemain;
                if (cb > CopyBufSize)
                    cb = CopyBufSize;

                input.Read(buffer, 0, cb);
                packageOut.Write(buffer, 0, cb);

                cbRemain -= cb;
            }

            return entry;
        }

        /// <summary>
        /// Adds files from a file system directory.
        /// </summary>
        /// <param name="packagePath">The destination package folder.</param>
        /// <param name="path">The directory path.</param>
        /// <param name="pattern">The search pattern or <c>null</c> to add all files.</param>
        /// <remarks>
        /// The search pattern may include the '*' and '?' wildcards.
        /// </remarks>
        public void AddFiles(string packagePath, string path, string pattern)
        {
            string[]    files;

            if (!packagePath.EndsWith("/"))
                packagePath += "/";

            if (pattern == null)
                files = Directory.GetFiles(path);
            else
                files = Directory.GetFiles(path, pattern);

            foreach (string fullPath in files)
            {
                string name;

                if ((File.GetAttributes(fullPath) & FileAttributes.Directory) != 0)
                    continue;   // Ignoring directories for now

                name = fullPath.Substring(fullPath.LastIndexOf(Helper.PathSepString) + 1);
                AddFile(packagePath + name, fullPath);
            }
        }

        /// <summary>
        /// Returns the package entry whose fully qualified path is passed
        /// if one exists, null otherwise.
        /// </summary>
        public PackageEntry this[string packagePath]
        {
            get
            {
                if (packagePath == "/")
                    return root;

                return (PackageEntry)entries[packagePath.ToUpper()];
            }
        }

        /// <summary>
        /// Returns the root folder.
        /// </summary>
        public PackageEntry RootFolder
        {
            get { return root; }
        }
    }
}
