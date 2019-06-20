//-----------------------------------------------------------------------------
// FILE:        PackageEntry.cs
// CONTRIBUTOR: Jeff Lill
// COPYRIGHT:   Copyright (c) 2005-2015 by Jeffrey Lill.  All rights reserved.
// DESCRIPTION: Describes an entry in a package file.

using System;
using System.IO;
using System.Text;
using System.Collections;

using LillTek.Common;

namespace LillTek.Install
{
    /// <summary>
    /// Describes an entry in a package file.
    /// </summary>
    public sealed class PackageEntry : IEnumerable
    {
        private static ArrayList    emptyList = new ArrayList();

        private const int           EntryCookie = 0x70D37212;
        private const int           EOL = 0;
        private const int           FOLDER = 1;
        private const int           FILE = 2;

        private Package             package;    // The package that owns this entry
        private int                 type;       // Entry type
        private PackageEntry        parent;     // The parent folder (or null)
        private string              name;       // The entry name
        private string              fullName;   // The entry full name
        private int                 size;       // Content size in bytes
        private long                offset;     // Offset of the first byte of the content data
        private ArrayList           children;   // Folder child entries (or null)

        /// <summary>
        /// Reads the entry header from the current position of
        /// the stream passed, advancing the stream position to the
        /// next entry.
        /// </summary>
        /// <param name="package">The package that owns this entry.</param>
        /// <param name="es">The input stream.</param>
        internal PackageEntry(Package package, EnhancedStream es)
        {
            int pos;

            if (es.ReadInt32() != EntryCookie)
                throw new PackageException("Invalid package entry signature.");

            this.package  = package;
            this.children = null;

            this.type = es.ReadByte();
            switch (type)
            {
                case EOL:
                case FOLDER:
                case FILE:

                    break;

                default:

                    throw new PackageException(Package.BadFileFormat);
            }

            fullName     = es.ReadString32();
            size         = es.ReadInt32();
            offset       = es.Position;
            es.Position += size;

            if (fullName != null)
            {
                pos = fullName.LastIndexOf('/');
                if (pos == -1)
                    name = fullName;
                else
                    name = fullName.Substring(pos + 1);
            }
        }

        /// <summary>
        /// Instantiates a special package entry record that terminates the
        /// list of entries in a package.
        /// </summary>
        /// <param name="package">The package that owns this entry.</param>
        internal PackageEntry(Package package)
        {
            this.package = package;
            this.type    = EOL;
        }

        /// <summary>
        /// Returns the fully qualified name of this entry by walking the
        /// parent nodes.
        /// </summary>
        private string GetFullName()
        {
            ArrayList       reversed = new ArrayList();
            PackageEntry    entry;
            StringBuilder   sb;

            entry = this;
            while (entry != null)
            {
                reversed.Add(entry.Name);
                entry = entry.Parent;
            }

            sb = new StringBuilder();
            for (int i = reversed.Count - 1; i >= 0; i--)
                sb.AppendFormat("/{0}", (string)reversed[i]);

            return sb.ToString();
        }

        /// <summary>
        /// Instantiates a folder or file package entry.
        /// </summary>
        /// <param name="package">The package that owns this entry.</param>
        /// <param name="parent">The parent folder (or <c>null</c> for the root folder).</param>
        /// <param name="name">The entry's local name.</param>
        /// <param name="isFolder"><c>true</c> if this is a folder, false for a file.</param>
        internal PackageEntry(Package package, PackageEntry parent, string name, bool isFolder)
        {
            this.package  = package;
            this.type     = isFolder ? FOLDER : FILE;
            this.parent   = parent;
            this.name     = name;
            this.fullName = GetFullName();
            this.size     = 0;
            this.children = new ArrayList();
        }

        /// <summary>
        /// Instantiates a package entry with a fully qualified name.
        /// </summary>
        /// <param name="package">The package that owns this entry.</param>
        /// <param name="fullName">The entry's fully qualified name.</param>
        /// <param name="isFolder">Pass <c>true</c> if this entry is a folder.</param>
        internal PackageEntry(Package package, string fullName, bool isFolder)
        {
            int pos;

            pos = fullName.LastIndexOf('/');
            if (pos == -1)
                name = fullName;
            else
                name = fullName.Substring(pos + 1);

            this.package  = package;
            this.type     = isFolder ? FOLDER : FILE;
            this.parent   = null;
            this.fullName = fullName;
            this.size     = 0;
            this.children = new ArrayList();
        }

        /// <summary>
        /// Returns <c>true</c> if this instance marks the end of the list
        /// of entries.
        /// </summary>
        internal bool IsEol
        {
            get { return type == EOL; }
        }

        /// <summary>
        /// Returns <c>true</c> for folder package entries, false for files.
        /// </summary>
        public bool IsFolder
        {
            get { return type == FOLDER; }
        }

        /// <summary>
        /// Returns <c>true</c> if the entry is a file.
        /// </summary>
        public bool IsFile
        {
            get { return type == FILE; }
        }

        /// <summary>
        /// Returns the entry's parent folder or <c>null</c> if this is the root
        /// folder.
        /// </summary>
        public PackageEntry Parent
        {
            get { return parent; }
        }

        /// <summary>
        /// Returns the local name of this entry.
        /// </summary>
        public string Name
        {
            get { return name; }
        }

        /// <summary>
        /// Returns the fully qualified name of this entry.
        /// </summary>
        public string FullName
        {
            get { return fullName; }
        }

        /// <summary>
        /// Returns the size in bytes of package files, 0 for folders.
        /// </summary>
        public int Size
        {
            get { return size; }
        }

        /// <summary>
        /// Offset of the first byte of file data within the stream.
        /// </summary>
        internal long Offset
        {
            get { return offset; }
            set { offset = value; }
        }

        /// <summary>
        /// Sets the entry's parent.
        /// </summary>
        /// <param name="parent">The parent being assigned.</param>
        internal void SetParent(PackageEntry parent)
        {
            this.parent = parent;
        }
        /// <summary>
        /// Sets the entry's size.
        /// </summary>
        /// <param name="size">The size.</param>
        internal void SetSize(int size)
        {
            this.size = size;
        }

        /// <summary>
        /// Adds the entry passed to the set of this folder's children.
        /// </summary>
        /// <param name="entry">The entry to be added.</param>
        internal void AddChild(PackageEntry entry)
        {
            if (type != FOLDER)
                throw new PackageException(Package.NotFolder);

            if (children == null)
                children = new ArrayList();

            children.Add(entry);
        }

        /// <summary>
        /// Writes the package entry's header to the stream.
        /// </summary>
        /// <param name="es">The output stream.</param>
        internal void Serialize(EnhancedStream es)
        {
            es.WriteInt32(EntryCookie);
            es.WriteByte((byte)type);
            es.WriteString32(FullName);
            es.WriteInt32(size);
        }

        /// <summary>
        /// Returns an enumerator over the entry's children.
        /// </summary>
        public IEnumerator GetEnumerator()
        {
            if (children == null)
                return emptyList.GetEnumerator();
            else
                return children.GetEnumerator();
        }

        /// <summary>
        /// Returns the child entries belonging to this entry.
        /// </summary>
        public PackageEntry[] Children
        {
            get
            {
                PackageEntry[] entries;

                if (children == null)
                    return new PackageEntry[0];

                entries = new PackageEntry[children.Count];
                children.CopyTo(0, entries, 0, children.Count);
                return entries;
            }
        }

        /// <summary>
        /// Searches the children of this folder for one whose
        /// name matches the local name passed.
        /// </summary>
        /// <param name="name">The name of the entry to retrieve.</param>
        /// <remarks>
        /// The entry if found, null otherwise.
        /// </remarks>
        public PackageEntry this[string name]
        {
            get
            {
                if (type != FOLDER)
                    throw new PackageException(Package.NotFolder);

                if (children == null)
                    return null;

                for (int i = 0; i < children.Count; i++)
                    if (String.Compare(name, ((PackageEntry)children[i]).Name, true) == 0)
                        return (PackageEntry)children[i];

                return null;
            }
        }

        /// <summary>
        /// Verifies that a fully local name is reasonable.
        /// </summary>
        /// <param name="name">The name to check.</param>
        private void CheckName(string name)
        {
            if (name.Length == 0 ||
                name.IndexOfAny(Package.BadChars) != -1 ||
                name.IndexOf('/') != -1)

                throw new PackageException(Package.InvalidPath);
        }

        /// <summary>
        /// Appends the local name onto the full path and returns the result.
        /// </summary>
        private string ComposeName(string fullPath, string name)
        {
            if (fullPath.EndsWith("/"))
                return fullPath + name;
            else
                return fullPath + "/" + name;
        }

        /// <summary>
        /// Adds a subfolder entry to the folder.
        /// </summary>
        /// <param name="name">The local name of the folder to be added.</param>
        /// <returns>The package entry added.</returns>
        public PackageEntry AddFolder(string name)
        {
            if (type != FOLDER)
                throw new PackageException(Package.NotFolder);

            CheckName(name);
            return package.AddFolder(ComposeName(fullName, name));
        }

        /// <summary>
        /// Adds a file entry to the folder.
        /// </summary>
        /// <param name="name">The local name of the file to be added.</param>
        /// <param name="buffer">The entry data to be added.</param>
        /// <returns>The package entry added.</returns>
        public PackageEntry AddFile(string name, byte[] buffer)
        {
            if (type != FOLDER)
                throw new PackageException(Package.NotFolder);

            CheckName(name);
            return package.AddFile(ComposeName(fullName, name), buffer);
        }

        /// <summary>
        /// Adds a file entry to the folder by copying the contents of a
        /// file system file.
        /// </summary>
        /// <param name="name">The local name of the file to be added.</param>
        /// <param name="path">The path to the file system file to be copied.</param>
        /// <returns>The package entry added.</returns>
        public PackageEntry AddFile(string name, string path)
        {
            if (type != FOLDER)
                throw new PackageException(Package.NotFolder);

            CheckName(name);
            return package.AddFile(ComposeName(fullName, name), path);
        }

        /// <summary>
        /// Adds a file entry to the folder by copying data from a stream.
        /// </summary>
        /// <param name="name">The local name of the file to be added.</param>
        /// <param name="input">The input stream.</param>
        /// <param name="size">The number of bytes to copy.</param>
        /// <returns>The package entry added.</returns>
        /// <remarks>
        /// The input stream's position will be advanced past the last
        /// byte read.
        /// </remarks>
        public PackageEntry AddFile(string name, Stream input, int size)
        {
            if (type != FOLDER)
                throw new PackageException(Package.NotFolder);

            CheckName(name);
            return package.AddFile(ComposeName(fullName, name), input, size);
        }

        /// <summary>
        /// Adds files from a file system directory to this folder.
        /// </summary>
        /// <param name="path">The directory path.</param>
        /// <param name="pattern">The search pattern or <c>null</c> to add all files.</param>
        /// <remarks>
        /// The search pattern may include the '*' and '?' wildcards.
        /// </remarks>
        public void AddFiles(string path, string pattern)
        {
            if (type != FOLDER)
                throw new PackageException(Package.NotFolder);

            package.AddFiles(fullName, path, pattern);
        }

        /// <summary>
        /// Return the contents of this entry as a byte array.
        /// </summary>
        public byte[] GetContents()
        {
            byte[] buffer;

            if (package.Input == null)
                throw new PackageException(Package.WriteOnly);

            if (type != FILE)
                throw new PackageException(Package.NotFile);

            buffer = new byte[size];
            package.Input.Position = offset;
            package.Input.Read(buffer, 0, size);

            return buffer;
        }

        /// <summary>
        /// Writes the contents of this entry to a stream.
        /// </summary>
        /// <param name="output">The output stream.</param>
        public void GetContents(Stream output)
        {
            byte[]      buffer;
            int         cb;
            int         cbRemain;

            if (package.Input == null)
                throw new PackageException(Package.WriteOnly);

            if (type != FILE)
                throw new PackageException(Package.NotFile);

            buffer = new byte[Package.CopyBufSize];
            cbRemain = size;

            package.Input.Position = offset;
            while (cbRemain > 0)
            {
                cb = cbRemain;
                if (cb > Package.CopyBufSize)
                    cb = Package.CopyBufSize;

                package.Input.Read(buffer, 0, cb);
                output.Write(buffer, 0, cb);

                cbRemain -= cb;
            }
        }

        /// <summary>
        /// Writes the contents of this entry to a file. 
        /// </summary>
        /// <param name="path">The path to the output file.</param>
        public void GetContents(string path)
        {
            FileStream file;

            if (package.Input == null)
                throw new PackageException(Package.WriteOnly);

            if (type != FILE)
                throw new PackageException(Package.NotFile);

            file = new FileStream(path, FileMode.Create);
            using (file)
            {
                GetContents(file);
            }
        }
    }
}
