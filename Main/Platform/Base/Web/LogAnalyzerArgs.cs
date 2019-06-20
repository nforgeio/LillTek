//-----------------------------------------------------------------------------
// FILE:        LogAnalyzerArgs.cs
// CONTRIBUTOR: Jeff Lill
// COPYRIGHT:   Copyright (c) 2005-2015 by Jeffrey Lill.  All rights reserved.
// DESCRIPTION: Used by the LogAnalyzer.AnalyzeEvent to provide a way to extend
//              the base analysis capabilities.

using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Net;
using System.Reflection;
using System.Text;
using System.Web;
using System.Web.Hosting;
using System.Web.UI;
using System.Web.UI.HtmlControls;
using System.Web.UI.WebControls;
using System.Xml;
using System.Xml.Linq;

using LillTek.Common;
using LillTek.Service;

namespace LillTek.Web
{
    /// <summary>
    /// Used by <see cref="LogAnalyzer" />.<see cref="LogAnalyzer.AnalyzeEvent " /> to 
    /// provide a way to extend the base analysis capabilities.
    /// </summary>
    /// <remarks>
    /// <note>
    /// This class defines several properties that will be initialized if the
    /// log entry describes an HTTP request.  These fields will be set to <c>null</c>
    /// for other entry types.
    /// </note>
    /// </remarks>
    public class LogAnalyzerArgs : EventArgs
    {
        /// <summary>
        /// Constructor.
        /// </summary>
        /// <param name="visitorCookie">The unique visitor cookie or <c>null</c>.</param>
        /// <param name="logTag">The log tag.</param>
        /// <param name="logEntry">The log entry.</param>
        internal LogAnalyzerArgs(string visitorCookie, string logTag, string logEntry)
        {
            this.VisitorCookie = visitorCookie;
            this.LogTag        = logTag;
            this.LogEntry      = logEntry;

            if (!logTag.StartsWith("PageView") && !logTag.StartsWith("BotView"))
                return;     // Not for a HTTP request.

            // Process the HTTP request line and headers.

            using (var reader = new StringReader(logEntry))
            {
                string      line;
                string[]    fields;
                char[]      headerSplit = new char[] { ':' };

                line    = reader.ReadLine();
                fields  = line.Split(' ');

                Method  = fields[0].ToUpper();
                Path    = fields[1];
                Headers = new ArgCollection(ArgCollectionType.Unconstrained);

                for (line = reader.ReadLine(); line != null; line = reader.ReadLine())
                {
                    fields = line.Split(headerSplit, 2);
                    Headers[fields[0].Trim()] = fields[1].Trim();
                }

                Headers.IsReadOnly = true;
            }

            // Extract the common header values.

            this.Host = Headers["Host"];

            IPAddress address;

            if (Headers.ContainsKey("X-Remote-Address") && IPAddress.TryParse(Headers["X-Remote-Address"], out address))
                this.RemoteAddress = address;

            if (Headers.ContainsKey("Referer"))
            {
                try
                {
                    this.Referer = new Uri(Headers["Referer"]);
                }
                catch
                {
                    // Ignore
                }
            }

            // Try to extract the standard LillTek unique visitor cookie if we didn't have
            // one passed.

            string cookieHeader;

            if (VisitorCookie == null && Headers.TryGetValue("Cookie", out cookieHeader))
            {
                try
                {
                    var     cookies = new ArgCollection(cookieHeader, '=', ';');
                    string  visitor;

                    if (cookies.TryGetValue(WebHelper.UniqueVisitorCookie, out visitor))
                    {
                        try
                        {
                            VisitorCookie = new UniqueVisitor(visitor).ID.ToString("D").ToUpper();
                        }
                        catch
                        {
                            VisitorCookie = visitor;
                        }
                    }
                }
                catch (Exception e)
                {
                    SysLog.LogException(e);
                }
            }
        }

        /// <summary>
        /// Returns the log tag.
        /// </summary>
        public string LogTag { get; private set; }

        /// <summary>
        /// Returns the log entry.
        /// </summary>
        public string LogEntry { get; private set; }

        /// <summary>
        /// Returns the HTTP method converted to UPPERCASE.
        /// </summary>
        public string Method { get; private set; }

        /// <summary>
        /// Returns the virtual path to the requested file.
        /// </summary>
        public string Path { get; private set; }

        /// <summary>
        /// Returns the host.
        /// </summary>
        public string Host { get; private set; }

        /// <summary>
        /// The unique visitor cookie or <c>null</c>.
        /// </summary>
        /// <remarks>
        /// <note>
        /// Event handlers may also set this to a custom value if one can be determined from the log entry.
        /// </note>
        /// </remarks>
        public string VisitorCookie { get; set; }

        /// <summary>
        /// The referer <see cref="Uri" /> or <c>null</c>.
        /// </summary>
        public Uri Referer { get; private set; }

        /// <summary>
        /// Returns the IP address of the client if known or <c>null</c>.
        /// </summary>
        public IPAddress RemoteAddress { get; private set; }

        /// <summary>
        /// Returns a <b>read-only</b> collection of the HTTP request headers.
        /// </summary>
        public ArgCollection Headers { get; private set; }

        /// <summary>
        /// Handlers can set this to <c>true</c> to indicate that no further processing
        /// of the event should occur.
        /// </summary>
        public bool Handled { get; set; }

        /// <summary>
        /// Handlers should set this to a non-<c>null</c> value if the handler has 
        /// determined that the log entry definitely is or is not a page view.  Doing
        /// this overrides the default page view determination by the <see cref="LogAnalyzer" />.
        /// </summary>
        public bool? IsPageView { get; set; }
    }
}
