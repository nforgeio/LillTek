//-----------------------------------------------------------------------------
// FILE:        AppRef.cs
// CONTRIBUTOR: Jeff Lill
// COPYRIGHT:   Copyright (c) 2005-2015 by Jeffrey Lill.  All rights reserved.
// DESCRIPTION: Wraps the Uri class to reference an application
//              package maintained by an AppStore cluster.

using System;
using System.Text;

using LillTek.Common;

// $todo(jeff.lill): 
//
// I need to figure out a way to specify appref://...?version=*
// or something which I can use to query for the most recent
// version rather than an explicit version number.

namespace LillTek.Datacenter
{
    /// <summary>
    /// Wraps the <see cref="Uri" /> class to reference an application
    /// package maintained by an <b>AppStore</b> cluster.
    /// </summary>
    /// <remarks>
    /// <para>
    /// An application package is a ZIP archive application code and/or 
    /// data files.  Application packages are stored or cached by 
    /// <b>AppStore</b> service instances and can also be cached locally on
    /// application servers via <see cref="AppStoreClient" />.  <b>AppStoreHandler</b>
    /// and <see cref="AppStoreClient" /> combine to implement a simple, yet
    /// powerful mechanism for delivering application code across one
    /// or more datacenters.
    /// </para>
    /// <para>
    /// Application packages are identified by a specialized URI where the
    /// URI scheme is <b>appref</b>, the port is ignored, and the host
    /// name and segments can be any arbitrary text that uniquely identifies
    /// the application.  The URI query string specifies the application
    /// version number, using standard .NET syntax.  <see cref="AppRef" />
    /// URIs must include at least one segment after the host name.
    /// </para>
    /// <example>
    /// <para>
    /// Here's a typical application reference:
    /// </para>
    /// <blockquote>
    /// appref://MyApps/Server/IMServer.zip?version=1.0.3.0123
    /// </blockquote>
    /// </example>
    /// <para>
    /// Other than conforming to the standard URI conventions, the only
    /// special thing to consider when creating an <see cref="AppRef" />
    /// is that the <b>AppStorehandler</b> and <see cref="AppStoreClient" /> implementations
    /// replace all forward slash ("/") characters with periods (".") when
    /// persisting these to the file system.  This will lead to naming
    /// conflicts if you're not careful about using periods within application
    /// references.
    /// </para>
    /// <example>
    /// <para>
    /// The following two apprefs will map to the same file causing a conflict:
    /// </para>
    /// <blockquote>
    /// appref://MyApps/Server/IMServer.zip?version=1.0.3.0123<br/>
    /// appref://MyApps.Server.IMServer.zip?version=1.0.3.0123<br/>
    /// </blockquote>
    /// </example>
    /// <note>
    /// Application reference URIs are normalized to lowercase.
    /// </note>
    /// </remarks>
    public sealed class AppRef
    {
        //---------------------------------------------------------------------
        // Static members

        /// <summary>
        /// The <see cref="AppRef" /> URI scheme (in lower case).
        /// </summary>
        public const string Scheme = "appref";

        /// <summary>
        /// Parses an <see cref="AppRef" /> from a string.
        /// </summary>
        /// <param name="input">The input string.</param>
        /// <returns>The parsed <see cref="AppRef" /> instance.</returns>
        /// <exception cref="FormatException">Thrown if the input string is not a valid application reference.</exception>
        public static AppRef Parse(string input)
        {
            return Parse(new Uri(input));
        }

        /// <summary>
        /// Parses an <see cref="AppRef" /> from a <see cref="Uri" />.
        /// </summary>
        /// <param name="uri">The input <see cref="Uri" />.</param>
        /// <returns>The parsed <see cref="AppRef" /> instance.</returns>
        /// <exception cref="FormatException">Thrown if the <see cref="Uri" /> is not a valid application reference.</exception>
        public static AppRef Parse(Uri uri)
        {
            return new AppRef(uri);
        }

        //---------------------------------------------------------------------
        // Instance members

        private string      uri;        // The URI string
        private string      fileName;   // The file name
        private Version     version;    // The version number

        /// <summary>
        /// Parses an <see cref="AppRef" /> from a string.
        /// </summary>
        /// <param name="input">The input string.</param>
        /// <exception cref="FormatException">Thrown if the input string is not a valid application reference.</exception>
        public AppRef(string input)
            : this(new Uri(input))
        {
        }

        /// <summary>
        /// Parses an <see cref="AppRef" /> from a <see cref="Uri" />.
        /// </summary>
        /// <param name="uri">The input <see cref="Uri" />.</param>
        /// <exception cref="FormatException">Thrown if the <see cref="Uri" /> is not a valid application reference.</exception>
        public AppRef(Uri uri)
        {
            ArgCollection   args;
            string          ver;
            StringBuilder   sb;
            string          path;
            int             pos;

            if (uri.Segments.Length == 0)
                throw new FormatException("AppRef URIs must include at least one segment after the host name.");

            if (uri.Scheme != Scheme)
                throw new FormatException(string.Format("AppRef URI scheme must be [{0}://].", Scheme));

            args = Helper.ParseUriQuery(uri);
            ver = args["version"];
            if (ver == null || ver == string.Empty)
                throw new FormatException("AppRef URI requires the [version] query parameter.");

            try
            {
                this.version = new Version(ver);
            }
            catch
            {
                throw new FormatException("Invalid AppRef URI version query parameter.");
            }

            // Generate a padded version number so that file lists (etc) will
            // sort nicely.

            sb = new StringBuilder(20);
            sb.AppendFormat("{0:0###}.{1:0###}.", version.Major, version.Minor);

            if (version.Build == -1)
                sb.Append("-1.");
            else
                sb.AppendFormat("{0:0###}.", version.Build);

            if (version.Revision == -1)
                sb.Append("-1");
            else
                sb.AppendFormat("{0:0###}", version.Revision);

            ver = sb.ToString();

            // Generate the file name.

            sb = new StringBuilder(uri.OriginalString.Length);
            sb.Append(uri.Host);

            for (int i = 0; i < uri.Segments.Length; i++)
            {
                if (i < uri.Segments.Length - 1)
                    sb.Append(uri.Segments[i]);
                else
                {
                    path = uri.Segments[i];
                    pos = path.LastIndexOf('.');
                    if (pos != -1)
                        path = path.Substring(0, pos);

                    sb.Append(path);
                }
            }

            sb.Append('-');
            sb.Append(ver);
            sb.Append(".zip");

            this.fileName = sb.ToString().Replace('/', '.').ToLowerInvariant();

            // Reconstruct a normalized URI.

            path = uri.AbsolutePath.ToLowerInvariant();  // Strip the file extension (if there is one)
            pos = path.LastIndexOfAny(new char[] { '.', '\\', '/' });
            if (pos != -1 && path[pos] == '.')
                path = path.Substring(0, pos);

            this.uri = string.Format("{0}://{1}{2}.zip?version={3}", Scheme, uri.Host, path, version.ToString());
        }

        /// <summary>
        /// Returns the normalized <see cref="Uri" />.
        /// </summary>
        public Uri Uri
        {
            get { return new Uri(uri); }
        }

        /// <summary>
        /// Returns the file name to be used when persisting an application
        /// package with this URI to the file system.
        /// </summary>
        /// <remarks>
        /// <para>
        /// Note that file names will always be converted to lowercase, slashes
        /// in the URI will be convered to periods, and the file name will
        /// will include the version number.  Here's an example of an appref
        /// URI and the corresponding file name that will be returned by this
        /// property.
        /// </para>
        /// <example>
        /// <c>URI:  appref://MyApps/Server/MyApp.zip?version=1.2.3.4</c><br/>
        /// <c>File: myapps.server.myapp-0001.0002.0003.0004.zip</c>
        /// </example>
        /// </remarks>
        public string FileName
        {
            get { return fileName; }
        }

        /// <summary>
        /// Returns the version number encoded into the <see cref="AppRef" />.
        /// </summary>
        public Version Version
        {
            get { return version; }
        }

        /// <summary>
        /// Returns <c>true</c> if the <see cref="AppRef" /> object passed equals
        /// this instance.
        /// </summary>
        /// <param name="obj">The object to test.</param>
        /// <returns><c>true</c> if the two objects are equal.</returns>
        public override bool Equals(object obj)
        {
            AppRef appRef = obj as AppRef;

            if (appRef == null)
                return false;

            return String.Compare(this.uri, appRef.uri, true) == 0;
        }

        /// <summary>
        /// Computes a hash code for this instance.
        /// </summary>
        /// <returns>The hash code.</returns>
        public override int GetHashCode()
        {
            return uri.GetHashCode();
        }

        /// <summary>
        /// Renders the <see cref="AppRef" /> instance as a string.
        /// </summary>
        /// <returns>The formatted string.</returns>
        public override string ToString()
        {
            return uri;
        }
    }
}
