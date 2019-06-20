//-----------------------------------------------------------------------------
// FILE:        AppPackage.cs
// CONTRIBUTOR: Jeff Lill
// COPYRIGHT:   Copyright (c) 2005-2015 by Jeffrey Lill.  All rights reserved.
// DESCRIPTION: A ZIP archive holding one or more application code and/or data files.

using System;
using System.IO;
using System.Reflection;

using LillTek.Common;
using LillTek.Compression.Zip;
using LillTek.Cryptography;

namespace LillTek.Datacenter
{
    /// <summary>
    /// A ZIP archive holding one or more application code and/or data files.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Applications can be deployed across one or more datacenters using 
    /// the <b>AppStore Service</b> and the <see cref="AppStoreClient" /> class.
    /// These components work together to store, distribute, and cache application
    /// code and data files.
    /// </para>
    /// <para>
    /// The files necessary to run an application are collected together into
    /// a standard ZIP file so that they can be cached and delivered convienently.
    /// These are called <b>Application Package</b> files and the <see cref="AppPackage" />
    /// provides methods for creating and reading these packages.
    /// </para>
    /// <para>
    /// As mentioned above, application packages are simply standard format
    /// ZIP files and can be created or opened with any ZIP compatible utility.
    /// When an application package is deployed, its files are extracted to
    /// a directory and the application is started.
    /// </para>
    /// <para><b><u>Package Metadata File: Package.ini</u></b></para>
    /// <para>
    /// Application packages require the presence of one special file within
    /// the package.  This file is named <b>package.ini</b> and is used to
    /// hold the package's <see cref="AppRef" /> as well as any information needed
    /// to launch the application.
    /// </para>
    /// <para>
    /// The <b>package.ini</b> file is a UTF-8 text file formatted as a
    /// standard <see cref="Config" /> file with the package related settings
    /// located within the <b>AppPackage</b> section.  Here's an example:
    /// </para>
    /// <code language="none">
    /// #section AppPackage
    /// 
    ///     AppRef      = appref://myapps/server/serverapp.zip?version=1.0.0.1234
    ///     LaunchType  = MyNamespace.MyType:MyAssembly.dll
    ///     LunchMethod = Main
    ///     LaunchArgs  = 
    /// 
    /// #endsection
    /// </code>
    /// <para>
    /// Here are descriptions for the currently supported package metadata
    /// settings:
    /// </para>
    /// <div class="tablediv">
    /// <table class="dtTABLE" cellspacing="0" ID="Table1">
    /// <tr valign="top">
    /// <th width="1">Setting</th>        
    /// <th width="1">Default</th>
    /// <th width="90%">Description</th>
    /// </tr>
    /// <tr valign="top">
    ///     <td>AppRef</td>
    ///     <td>(required)</td>
    ///     <td>The <see cref="AppRef" /> URI.</td>
    ///  </tr>
    /// <tr valign="top">
    ///     <td>LaunchType</td>
    ///     <td>(required)</td>
    ///     <td>
    ///     Specifies the reference to an assemply file and type within
    ///     that assembly which implements the application entry point.
    ///     This is formatted as described in <see cref="Config.ParseValue(string,System.Type)" />.
    ///     </td>
    ///  </tr>
    /// <tr valign="top">
    ///     <td>LaunchMethod</td>
    ///     <td>Main</td>
    ///     <td>
    ///     Specifies the method within the <b>LaunchType</b> to be called
    ///     to launch the application.  This can be a static or instance 
    ///     method.  Static entry points will simply be called to invoke the
    ///     application.  If the entry point is an instance method then
    ///     the type will be instantiated using a parameterless constructor
    ///     and then the entry point will be called.
    ///     </td>
    ///  </tr>
    /// <tr valign="top">
    ///     <td>LaunchArgs</td>
    ///     <td>(null)</td>
    ///     <td>
    ///     An optional parameter string to be passed to the entry point.
    ///     Entry points can be defined with no parameters (in which case
    ///     this setting is ignored) or with a single string parameter.
    ///     </td>
    ///  </tr>
    /// </table>
    /// </div>
    /// </remarks>
    /// <threadsafety instance="false" />
    public class AppPackage : IDisposable
    {
        //---------------------------------------------------------------------
        // Static members

        /// <summary>
        /// The configuration kewy prefix for application package metadata settings.
        /// </summary>
        public const string ConfigPrefix = "AppPackage";

        /// <summary>
        /// Opens an <see cref="AppPackage" /> for reading.
        /// </summary>
        /// <param name="path">File system path to the package.</param>
        /// <returns>An <see cref="AppPackage" /> instance.</returns>
        public static AppPackage Open(string path)
        {
            return new AppPackage(path);
        }

        /// <summary>
        /// Opens an <see cref="AppPackage" /> for writing, overwritting
        /// any existing package file.
        /// </summary>
        /// <param name="path">File system path to the package.</param>
        /// <param name="appRef">The package's <see cref="AppRef" />.</param>
        /// <param name="settings">The fully qualified package metadata settings.</param>
        /// <remarks>
        /// <note>
        /// The standard arguments passed in the <paramref name="settings" /> parameter
        /// must include the leading <b>"AppPackage.</b> prefix.
        /// </note>
        /// </remarks>
        public static AppPackage Create(string path, AppRef appRef, ArgCollection settings)
        {
            return new AppPackage(path, appRef, settings);
        }

        //---------------------------------------------------------------------
        // Instance members

        private string              path;
        private bool                readMode;
        private AppRef              appRef;
        private ZipOutputStream     zipOutput;
        private ZipFile             zipArchive;
        private Config              settings;
        private byte[]              md5Hash;

        /// <summary>
        /// Private create constructor.
        /// </summary>
        /// <param name="path">The package path on the file system.</param>
        /// <param name="appRef">The package's <see cref="AppRef" />.</param>
        /// <param name="settings">The package metadata settings (or <c>null</c>).</param>
        /// <remarks>
        /// <note>
        /// No <b>Package.ini</b> entry will be created if <paramref name="settings" />
        /// is passed as <c>null</c>.
        /// </note>
        /// </remarks>
        private AppPackage(string path, AppRef appRef, ArgCollection settings)
        {
            this.path       = path;
            this.readMode   = false;
            this.appRef     = appRef;
            this.zipArchive = null;
            this.settings   = null;
            this.md5Hash    = null;

            // Create the archive

            Helper.CreateFileTree(path);
            if (File.Exists(path))
                File.Delete(path);

            zipOutput = new ZipOutputStream(new FileStream(path, FileMode.Create, FileAccess.ReadWrite));
            zipOutput.SetLevel(9);

            if (settings != null)
            {
                // Generate the Package.ini file and save it to the archive.

                StreamWriter    writer;
                MemoryStream    ms;
                ZipEntry        entry;
                DateTime        utcNow;

                utcNow = DateTime.UtcNow;
                ms     = new MemoryStream();
                writer = new StreamWriter(ms);

                try
                {
                    writer.WriteLine("// Application Package Metadata");
                    writer.WriteLine("//");
                    writer.WriteLine("// Generated by: {0} v{1}", Path.GetFileName(Helper.GetAssemblyPath(Assembly.GetExecutingAssembly())), Helper.GetVersion(Assembly.GetExecutingAssembly()));
                    writer.WriteLine("// Create Date:  {0}", Helper.ToInternetDate(utcNow));
                    writer.WriteLine();

                    writer.WriteLine("#section AppPackage");
                    writer.WriteLine();
                    writer.WriteLine("    appref = {0}", appRef.ToString());

                    foreach (string key in settings)
                        writer.WriteLine("    {0} = {1}", key, settings[key]);

                    writer.WriteLine();
                    writer.WriteLine("#endsection");
                    writer.WriteLine();
                    writer.Flush();

                    entry = new ZipEntry("Package.ini");
                    entry.DateTime = utcNow;
                    zipOutput.PutNextEntry(entry);
                    zipOutput.Write(ms.GetBuffer(), 0, (int)ms.Length);
                }
                finally
                {
                    writer.Close();
                }
            }
        }

        /// <summary>
        /// Private read constructor.
        /// </summary>
        /// <param name="path">The package path on the file system.</param>
        private AppPackage(string path)
        {
            this.path       = path;
            this.readMode   = true;
            this.zipArchive = new ZipFile(path);
            this.zipOutput  = null;
            this.md5Hash    = null;

            // Load the "Package.ini" file from the archive.

            if (!ContainsFile("Package.ini"))
                throw new FormatException("Application package cannot be opened. Package.ini file is missing.");

            StreamReader    reader = null;
            MemoryStream    ms;
            string          v;

            try
            {
                ms = new MemoryStream();
                CopyFile("Package.ini", ms);
                ms.Position = 0;
                reader = new StreamReader(ms);

                settings = new Config(ConfigPrefix, reader);

                v = settings.Get("AppRef");
                if (v == null)
                    throw new FormatException("[AppRef] property is missing in the application package metadata.");
                else
                    appRef = new AppRef(v);
            }
            finally
            {
                if (reader != null)
                    reader.Close();
            }
        }

        /// <summary>
        /// Returns the package's <see cref="AppRef" />.
        /// </summary>
        public AppRef AppRef
        {
            get { return appRef; }
        }

        /// <summary>
        /// Returns the package <see cref="Version" />.
        /// </summary>
        public Version Version
        {
            get { return appRef.Version; }
        }

        /// <summary>
        /// Returns a <see cref="Config" /> instance holding the package configuration settings
        /// loaded from the <c><b>Package.ini</b></c> file within the package.
        /// </summary>
        /// <remarks>
        /// <note>
        /// This property is available only for packages opened for reading.
        /// </note>
        /// </remarks>
        /// <exception cref="InvalidOperationException">Thrown if the package was opened for writing.</exception>
        public Config Settings
        {
            get
            {
                VerifyMode(true);
                return settings;
            }
        }

        /// <summary>
        /// Closes the archive it it's open.
        /// </summary>
        public void Close()
        {
            if (readMode)
            {
                if (zipArchive != null)
                {
                    zipArchive.Close();
                    zipArchive = null;
                }
            }
            else
            {
                if (zipOutput != null)
                {
                    zipOutput.Finish();
                    zipOutput.Close();
                    zipOutput = null;
                }
            }
        }

        /// <summary>
        /// Releases all resources associated with the package.
        /// </summary>
        public void Dispose()
        {
            Close();
        }

        /// <summary>
        /// Validates that an operation is valid for the current read/write mode.
        /// </summary>
        /// <param name="readOp">True for a read operation, false for write.</param>
        private void VerifyMode(bool readOp)
        {
            if (readOp == readMode)
                return;

            throw new InvalidOperationException(readMode ? "Operation not valid for packages opened for reading."
                                                         : "Operation not valid for packages opened for writing.");
        }

        /// <summary>
        /// Adds the contents of a file to the package.
        /// </summary>
        /// <param name="path">The input file path.</param>
        /// <param name="basePath">The base file path (or <c>null</c>).</param>
        /// <remarks>
        /// <note>
        /// This method is available only for packages opened for writing.
        /// </note>
        /// <para>
        /// The file name used for the entry added to the package can be absolute 
        /// or relative.  This is determined by the <paramref name="basePath" />
        /// parameter.  Pass <c>null</c> or the empty string to add an absolute
        /// path.  To add a relative path, pass <paramref name="basePath" /> as
        /// the leading portion of the path to be removed.
        /// </para>
        /// <example>
        /// <para>
        /// Let's say the fully qualified file path passed in <paramref name="path" />
        /// is:
        /// </para>
        /// <blockquote><c><b>path</b> = c:\folder1\folder2\testfile.txt</c></blockquote>
        /// <para>
        /// To add this file to the package with only the file name (<c>testfile.txt</c>), you'd
        /// need to pass <paramref name="basePath" /> as the leading folder
        /// path, which in this case would be:
        /// </para>
        /// <blockquote><c><b>basePath</b> = c:\folder1\folder2\</c></blockquote>
        /// </example>
        /// </remarks>
        /// <exception cref="InvalidOperationException">Thrown if the package was opened for reading.</exception>
        public void AddFile(string path, string basePath)
        {
            byte[]      buf = new byte[4096];
            string      entryPath;
            ZipEntry    entry;
            int         cbRead;

            VerifyMode(false);

            entryPath = path;
            if (basePath != null &&
                basePath.Length > 0 &&
                path.ToLowerInvariant().StartsWith(basePath.ToLowerInvariant()))
            {
                entryPath = entryPath.Substring(basePath.Length);
            }

            entry = new ZipEntry(entryPath);
            entry.DateTime = File.GetLastWriteTime(path);
            zipOutput.PutNextEntry(entry);

            using (FileStream fs = File.OpenRead(path))
            {
                while (true)
                {
                    cbRead = fs.Read(buf, 0, buf.Length);
                    if (cbRead == 0)
                        break;

                    zipOutput.Write(buf, 0, cbRead);
                }
            }
        }

        /// <summary>
        /// Adds the contents of a stream to the package.
        /// </summary>
        /// <param name="name">The file name to be used for the entry.</param>
        /// <param name="input">The input stream.</param>
        /// <remarks>
        /// <note>
        /// This method is available only for packages opened for writing.
        /// </note>
        /// <para>
        /// Data from the current stream position to the end of the
        /// stream will be added to the archive.
        /// </para>
        /// </remarks>
        /// <exception cref="InvalidOperationException">Thrown if the package was opened for reading.</exception>
        public void AddFile(string name, Stream input)
        {
            byte[]      buf = new byte[4096];
            ZipEntry    entry;
            int         cbRead;

            VerifyMode(false);

            entry          = new ZipEntry(name);
            entry.DateTime = DateTime.UtcNow;
            zipOutput.PutNextEntry(entry);

            while (true)
            {
                cbRead = input.Read(buf, 0, buf.Length);
                if (cbRead == 0)
                    break;

                zipOutput.Write(buf, 0, cbRead);
            }
        }

        /// <summary>
        /// Adds a byte array to the package.
        /// </summary>
        /// <param name="name">The file name to be used for the entry.</param>
        /// <param name="data">The input data.</param>
        /// <remarks>
        /// <note>
        /// This method is available only for packages opened for writing.
        /// </note>
        /// </remarks>
        /// <exception cref="InvalidOperationException">Thrown if the package was opened for reading.</exception>
        public void AddFile(string name, byte[] data)
        {
            using (var ms = new MemoryStream(data))
            {
                AddFile(name, ms);
            }
        }

        /// <summary>
        /// Returns <c>true</c> if the archive contains a specific file.
        /// </summary>
        /// <param name="name">The case insensitive file name.</param>
        /// <returns><c>true</c> if the archive contains the file.</returns>
        /// <remarks>
        /// <note>
        /// This method is available only for packages opened for reading.
        /// </note>
        /// </remarks>
        /// <exception cref="InvalidOperationException">Thrown if the package was opened for writing.</exception>
        public bool ContainsFile(string name)
        {
            VerifyMode(true);
            return zipArchive.GetEntry(name) != null;
        }

        /// <summary>
        /// Copies the contents of an archive file to a stream.
        /// </summary>
        /// <param name="name">The case insensitive archive file name.</param>
        /// <param name="output">The output stream.</param>
        /// <remarks>
        /// <note>
        /// This method is available only for packages opened for reading.
        /// </note>
        /// </remarks>
        /// <exception cref="InvalidOperationException">Thrown if the package was opened for writing.</exception>
        /// <exception cref="FileNotFoundException">Thrown if the file cannot be found in the package.</exception>
        public void CopyFile(string name, Stream output)
        {
            ZipEntry    entry;
            Stream      input;

            VerifyMode(true);

            entry = zipArchive.GetEntry(name);
            if (entry == null)
                throw new FileNotFoundException(string.Format("File [{0}] not found in application package.", name));

            using (input = zipArchive.GetInputStream(entry))
            {
                byte[]  buf = new byte[4096];
                int     cbRead;

                while (true)
                {
                    cbRead = input.Read(buf, 0, buf.Length);
                    if (cbRead == 0)
                        break;

                    output.Write(buf, 0, cbRead);
                }
            }
        }

        /// <summary>
        /// Extracts all of the files contained in the package to a folder.
        /// </summary>
        /// <param name="path">The output folder path.</param>
        /// <remarks>
        /// <note>
        /// This method is available only for packages opened for reading.
        /// </note>
        /// </remarks>
        public void ExtractTo(string path)
        {
            VerifyMode(true);

            foreach (ZipEntry entry in zipArchive)
            {
                string outPath;

                outPath = Path.GetFullPath(path);
                outPath = Helper.AddTrailingSlash(outPath);

                if (entry.Name.StartsWith("/"))
                    outPath += entry.Name.Substring(1);
                else
                    outPath += entry.Name;

                Helper.CreateFileTree(outPath);

                using (FileStream fs = new FileStream(outPath, FileMode.Create, FileAccess.ReadWrite))
                {
                    CopyFile(entry.Name, fs);
                }
            }
        }

        /// <summary>
        /// Computes the MD5 hash for the package.
        /// </summary>
        /// <remarks>
        /// <note>
        /// This method is available only for packages opened for reading.
        /// </note>
        /// </remarks>
        public byte[] MD5
        {
            get
            {
                VerifyMode(true);

                if (md5Hash != null)
                    return md5Hash;

                using (var es = new EnhancedFileStream(path, FileMode.Open, FileAccess.Read))
                {
                    md5Hash = MD5Hasher.Compute(es, es.Length);
                    return md5Hash;
                }
            }
        }
    }
}
