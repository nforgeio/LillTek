//-----------------------------------------------------------------------------
// FILE:        FileInfoList.cs
// CONTRIBUTOR: Jeff Lill
// COPYRIGHT:   Copyright (c) 2005-2015 by Jeffrey Lill.  All rights reserved.
// DESCRIPTION: Implements a sortable collection of FileInfo instances.

using System;
using System.IO;
using System.Collections;
using System.Collections.Generic;

namespace LillTek.Common
{
    /// <summary>
    /// Implements a sortable collection of <see cref="FileInfo" /> instances.
    /// </summary>
    /// <threadsafety instance="false" />
    public class FileInfoList : List<FileInfo>
    {
        /// <summary>
        /// Constucts an empty list.
        /// </summary>
        public FileInfoList()
            : base()
        {
        }

        /// <summary>
        /// Constructs and empty list with the specified initial capacity.
        /// </summary>
        /// <param name="capacity">The initial list capacity.</param>
        public FileInfoList(int capacity)
            : base(capacity)
        {
        }

        /// <summary>
        /// Constructs a list from an array of file paths.
        /// </summary>
        /// <param name="files">The file paths.</param>
        public FileInfoList(string[] files)
            : base(files.Length)
        {
            foreach (string file in files)
                base.Add(new FileInfo(file));
        }

        private class PathComparer : Comparer<FileInfo>
        {
            private bool ascending;

            public PathComparer(bool ascending)
            {
                this.ascending = ascending;
            }

            public override int Compare(FileInfo x, FileInfo y)
            {
                if (ascending)
                    return String.Compare(x.FullName, y.FullName, true);
                else
                    return String.Compare(y.FullName, x.FullName, true);
            }
        }

        /// <summary>
        /// Sorts the list by file path.
        /// </summary>
        /// <param name="ascending">Pass <c>true</c> for ascending order, <c>false</c> for descending.</param>
        public void SortByPath(bool ascending)
        {
            base.Sort(new PathComparer(ascending));
        }

        private class LengthComparer : Comparer<FileInfo>
        {
            private bool ascending;

            public LengthComparer(bool ascending)
            {
                this.ascending = ascending;
            }

            public override int Compare(FileInfo x, FileInfo y)
            {
                if (x.Length == y.Length)
                    return 0;

                if (ascending)
                {
                    if (x.Length < y.Length)
                        return -1;
                    else
                        return +1;
                }
                else
                {
                    if (x.Length < y.Length)
                        return +1;
                    else
                        return -1;
                }
            }
        }

        /// <summary>
        /// Sorts the list by file size.
        /// </summary>
        /// <param name="ascending">Pass <c>true</c> for ascending order, <c>false</c> for descending.</param>
        public void SortByLength(bool ascending)
        {
            base.Sort(new LengthComparer(ascending));
        }

        private class CreationComparer : Comparer<FileInfo>
        {
            private bool ascending;

            public CreationComparer(bool ascending)
            {
                this.ascending = ascending;
            }

            public override int Compare(FileInfo x, FileInfo y)
            {
                if (x.CreationTime == y.CreationTime)
                    return 0;

                if (ascending)
                {
                    if (x.CreationTime < y.CreationTime)
                        return -1;
                    else
                        return +1;
                }
                else
                {
                    if (x.CreationTime < y.CreationTime)
                        return +1;
                    else
                        return -1;
                }
            }
        }

        /// <summary>
        /// Sorts the list by file creation time.
        /// </summary>
        /// <param name="ascending">Pass <c>true</c> for ascending order, <c>false</c> for descending.</param>
        public void SortByCreationTime(bool ascending)
        {
            base.Sort(new CreationComparer(ascending));
        }

        private class AccessComparer : Comparer<FileInfo>
        {
            private bool ascending;

            public AccessComparer(bool ascending)
            {
                this.ascending = ascending;
            }

            public override int Compare(FileInfo x, FileInfo y)
            {
                if (x.LastAccessTime == y.LastAccessTime)
                    return 0;

                if (ascending)
                {
                    if (x.LastAccessTime < y.LastAccessTime)
                        return -1;
                    else
                        return +1;
                }
                else
                {
                    if (x.LastAccessTime < y.LastAccessTime)
                        return +1;
                    else
                        return -1;
                }
            }
        }

        /// <summary>
        /// Sorts the list by last file access time.
        /// </summary>
        /// <param name="ascending">Pass <c>true</c> for ascending order, <c>false</c> for descending.</param>
        public void SortByLastAccessTime(bool ascending)
        {
            base.Sort(new AccessComparer(ascending));
        }

        private class WriteComparer : Comparer<FileInfo>
        {
            private bool ascending;

            public WriteComparer(bool ascending)
            {
                this.ascending = ascending;
            }

            public override int Compare(FileInfo x, FileInfo y)
            {
                if (x.LastWriteTime == y.LastWriteTime)
                    return 0;

                if (ascending)
                {
                    if (x.LastWriteTime < y.LastWriteTime)
                        return -1;
                    else
                        return +1;
                }
                else
                {
                    if (x.LastWriteTime < y.LastWriteTime)
                        return +1;
                    else
                        return -1;
                }
            }
        }

        /// <summary>
        /// Sorts the list by last file write time.
        /// </summary>
        /// <param name="ascending">Pass <c>true</c> for ascending order, <c>false</c> for descending.</param>
        public void SortByLastWriteTime(bool ascending)
        {
            base.Sort(new WriteComparer(ascending));
        }
    }
}
