//-----------------------------------------------------------------------------
// FILE:        ZipUtil.cs
// CONTRIBUTOR: Jeff Lill
// COPYRIGHT:   Copyright (c) 2005-2015 by Jeffrey Lill.  All rights reserved.
// DESCRIPTION: Implements ZIP file utilities.

#if !MOBILE_DEVICE

using System;
using System.IO;
using System.Threading;
using System.Collections;
using System.Collections.Generic;

using LillTek.Common;
using LillTek.Compression.Zip;

// $todo(jeff.lill):
//
// The ZipUtil.Zip() methods aren't super useful if you don't use wildcards. 
// Added a couple new methods that accept an optional clipPath parameter that 
// specifies the path prefix to to be clipped from paths added to the ZIP archive. 

namespace LillTek.Compression.Zip
{
    /// <summary>
    /// Implements ZIP file utilities.
    /// </summary>
    /// <remarks>
    /// Use <see cref="Zip(string,bool,bool,string[])" /> to generate a ZIP archive from one or more files or folders
    /// and <see cref="Unzip(string,string,bool)" /> to extract files from a ZIP archive.
    /// </remarks>
    public static class ZipUtil
    {
        private static void AddFile(ZipOutputStream zipStream, string path, string basePath)
        {
            byte[]      buf = new byte[4096];
            ZipEntry    entry;
            int         cbRead;

            entry          = new ZipEntry(path.Substring(basePath.Length));
            entry.DateTime = File.GetLastWriteTime(path);
            zipStream.PutNextEntry(entry);

            using (var fs = File.OpenRead(path))
            {
                do
                {
                    cbRead = fs.Read(buf, 0, buf.Length);
                    zipStream.Write(buf, 0, cbRead);

                } while (cbRead > 0);
            }
        }

        /// <summary>
        /// Creates a ZIP archive file from one or more file path specifications.
        /// </summary>
        /// <param name="zipPath">Path to the output ZIP archive.</param>
        /// <param name="recursive">Indicates whether the file paths are to be searched recusively.</param>
        /// <param name="hidden">Pass <c>true</c> if hidden files should be included in the archive.</param>
        /// <param name="files">The paths to the files and folders to be archived (these may include <b>?</b> and <b>*</b> wildcards).</param>
        public static void Zip(string zipPath, bool recursive, bool hidden, params string[] files)
        {
            ZipOutputStream zipStream = null;

            if (files.Length == 0)
                throw new ArgumentException("At least one file must be specified.");

            if (Path.GetExtension(zipPath) == string.Empty)
                zipPath += ".zip";

            Helper.CreateFileTree(zipPath);
            if (File.Exists(zipPath))
                File.Delete(zipPath);

            zipStream = new ZipOutputStream(File.Create(zipPath));

            try
            {
                zipStream.SetLevel(9);

                foreach (var path in files)
                {
                    bool        hasWildcards;
                    bool        isFolder;
                    string      basePath;
                    int         pos;

                    hasWildcards = path.IndexOfAny(Helper.FileWildcards) != -1;
                    if (!hasWildcards)
                        isFolder = (File.GetAttributes(path) & FileAttributes.Directory) != 0;
                    else
                        isFolder = false;

                    basePath = Path.GetFullPath(path.Replace('?', 'x').Replace('*', 'x'));
                    pos = basePath.LastIndexOf(Helper.PathSepChar);
                    if (pos == -1)
                        throw new IOException(string.Format("Invalid file path [{0}].", path));

                    basePath = basePath.Substring(0, pos + 1);

                    if (isFolder)
                    {
                        if (!hidden && Helper.IsFileHidden(path))
                            continue;

                        foreach (var file in Helper.GetFilesByPattern(path + Helper.PathSepString + "*.*", SearchOption.AllDirectories))
                        {
                            if (String.Compare(zipPath, file, true) == 0)
                                continue;   // Ignore the zip archive being created

                            if (!hidden && Helper.IsFileHidden(file))
                                continue;

                            AddFile(zipStream, file, basePath);
                        }
                    }
                    else
                    {
                        foreach (var file in Helper.GetFilesByPattern(path, recursive ? SearchOption.AllDirectories : SearchOption.TopDirectoryOnly))
                        {
                            if (String.Compare(zipPath, file, true) == 0)
                                continue;   // Ignore the zip archive being created

                            if (!hidden && Helper.IsFileHidden(file))
                                continue;

                            AddFile(zipStream, file, basePath);
                        }
                    }
                }

                zipStream.Finish();
                zipStream.Close();
            }
            finally
            {

                zipStream.Dispose();
            }
        }

        /// <summary>
        /// Writes a ZIP archive to a stream from one or more file path specifications.
        /// </summary>
        /// <param name="output">The ourput stream.</param>
        /// <param name="recursive">Indicates whether the file paths are to be searched recusively.</param>
        /// <param name="hidden">Pass <c>true</c> if hidden files should be included in the archive.</param>
        /// <param name="files">The paths to the files and folders to be archived (these may include <b>?</b> and <b>*</b> wildcards).</param>
        public static void Zip(Stream output, bool recursive, bool hidden, params string[] files)
        {
            ZipOutputStream zipStream = null;

            if (files.Length == 0)
                throw new ArgumentException("At least one file must be specified.");

            zipStream = new ZipOutputStream(output);

            try
            {
                zipStream.SetLevel(9);
                zipStream.IsStreamOwner = false;

                foreach (string path in files)
                {
                    bool        hasWildcards;
                    bool        isFolder;
                    string      basePath;
                    int         pos;

                    hasWildcards = path.IndexOfAny(Helper.FileWildcards) != -1;
                    if (!hasWildcards)
                        isFolder = (File.GetAttributes(path) & FileAttributes.Directory) != 0;
                    else
                        isFolder = false;

                    basePath = Path.GetFullPath(path.Replace('?', 'x').Replace('*', 'x'));
                    pos = basePath.LastIndexOf(Helper.PathSepChar);
                    if (pos == -1)
                        throw new IOException(string.Format("Invalid file path [{0}].", path));

                    basePath = basePath.Substring(0, pos + 1);

                    if (isFolder)
                    {
                        if (!hidden && Helper.IsFileHidden(path))
                            continue;

                        foreach (var file in Helper.GetFilesByPattern(path + Helper.PathSepString + "*.*", SearchOption.AllDirectories))
                        {

                            if (!hidden && Helper.IsFileHidden(file))
                                continue;

                            AddFile(zipStream, file, basePath);
                        }
                    }
                    else
                    {
                        foreach (var file in Helper.GetFilesByPattern(path, recursive ? SearchOption.AllDirectories : SearchOption.TopDirectoryOnly))
                        {
                            if (!hidden && Helper.IsFileHidden(file))
                                continue;

                            AddFile(zipStream, file, basePath);
                        }
                    }
                }

                zipStream.Finish();
                zipStream.Close();
            }
            finally
            {
                zipStream.Dispose();
            }
        }

        /// <summary>
        /// Extracts files from a ZIP archive file.
        /// </summary>
        /// <param name="zipPath">Path to the input ZIP archive.</param>
        /// <param name="targetFolder">Path to the output folder.</param>
        /// <param name="createFolder">Pass <c>true</c> to place the output in a subfolder named for the ZIP archive.</param>
        public static void Unzip(string zipPath, string targetFolder, bool createFolder)
        {
            ZipFile     archive = null;
            string  outPath;

            try
            {
                archive = new ZipFile(zipPath);
                outPath = targetFolder;

                if (createFolder)
                    outPath = Path.Combine(outPath, Helper.GetFileNameWithoutExtension(zipPath));

                foreach (ZipEntry entry in archive)
                {
                    Stream              inStream = null;
                    EnhancedStream      outStream = null;
                    string              outFile;

                    outFile = Path.Combine(outPath, entry.Name);
                    Helper.CreateFileTree(outFile);

                    try
                    {
                        inStream  = archive.GetInputStream(entry);
                        outStream = new EnhancedFileStream(outFile, FileMode.Create, FileAccess.ReadWrite);

                        outStream.CopyFrom(inStream, -1);
                    }
                    finally
                    {
                        if (inStream != null)
                            inStream.Close();

                        if (outStream != null)
                            outStream.Close();

                        File.SetCreationTime(outFile, entry.DateTime);
                        File.SetLastWriteTime(outFile, entry.DateTime);
                    }
                }
            }
            finally
            {
                if (archive != null)
                    archive.Close();
            }
        }

        /// <summary>
        /// Extracts files from a ZIP archive stream.
        /// </summary>
        /// <param name="input">The input stream.</param>
        /// <param name="targetFolder">Path to the output folder.</param>
        public static void Unzip(Stream input, string targetFolder)
        {
            ZipFile     archive = null;
            string      outPath;

            try
            {
                archive = new ZipFile(input);
                outPath = targetFolder;

                foreach (ZipEntry entry in archive)
                {
                    Stream          inStream = null;
                    EnhancedStream  outStream = null;
                    string          outFile;

                    outFile = Path.Combine(outPath, entry.Name);
                    Helper.CreateFileTree(outFile);

                    try
                    {
                        inStream  = archive.GetInputStream(entry);
                        outStream = new EnhancedFileStream(outFile, FileMode.Create, FileAccess.ReadWrite);

                        outStream.CopyFrom(inStream, -1);
                    }
                    finally
                    {
                        if (inStream != null)
                            inStream.Close();

                        if (outStream != null)
                            outStream.Close();

                        File.SetCreationTime(outFile, entry.DateTime);
                        File.SetLastWriteTime(outFile, entry.DateTime);
                    }
                }
            }
            finally
            {
                if (archive != null)
                    archive.Close();
            }
        }
    }
}

#endif // !MOBILE_DEVICE