//-----------------------------------------------------------------------------
// FILE:        HeartbeatStatus.cs
// CONTRIBUTOR: Jeff Lill
// COPYRIGHT:   Copyright (c) 2005-2015 by Jeffrey Lill.  All rights reserved.
// DESCRIPTION: Defines the application or subsystem status information
//              returned by a heartbeat ping.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Xml.Linq;

using LillTek.Common;

namespace LillTek.Web
{
    /// <summary>
    /// Defines the application or subsystem status information 
    /// returned by a heartbeat ping.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Advanced LillTek based web applications will typically expose a <b>Heartbeat.aspx</b>
    /// page to return the health status of the application.  This page will typically verify the 
    /// local status on the server as well as global conditions such as the condition of
    /// a database server.
    /// </para>
    /// <para>
    /// By convention, the <b>Heartbeat.aspx</b> page will perform a full condition check
    /// if no query parameters are passed in thr URL.  The presence of a <b>global=0</b>
    /// query parameter will cause the page to avoid any global checks and perform
    /// only the local ones.
    /// </para>
    /// <para>
    /// <b>global=0</b> will be passed in situations where the continuous polling of a
    /// global resource such as a database would be costly in terms of performance 
    /// or cloud service fees.  In particular, this setting will often be used in
    /// URLs configured for the <b>Heartbeat Service</b> which typically polls local
    /// websites on a 15 second basis.  On SQL Azure, this would result in direct
    /// transaction fees, on AWS we'd see transaction fees for the I/Os to the Elastic
    /// Block Store hosting the database files.
    /// </para>
    /// </remarks>
    public class HeartbeatStatus
    {
        /// <summary>
        /// Constructor.
        /// </summary>
        /// <param name="status">The current status.</param>
        /// <param name="message">The status message (or <c>null</c>).</param>
        public HeartbeatStatus(HealthStatus status, string message)
        {
            this.Status  = status;
            this.Message = message ?? string.Empty;
        }

        /// <summary>
        /// Parses a health status instance from an XML element.
        /// </summary>
        /// <param name="element">The input element.</param>
        /// <exception cref="FormatException">Thrown if the input XML is not valid.</exception>
        public HeartbeatStatus(XElement element)
        {
            if (element.Name != "HeartbeatStatus")
                throw new FormatException("Root element must be [HeartbeatStatus].");

            if (element.ParseAttribute("version", -1) != 1)
                throw new FormatException("Unsupported version number.");

            this.Status = element.ParseElement<HealthStatus>("Status", HealthStatus.Healthy);
            this.Message = element.ParseElement("Message", string.Empty);
        }

        /// <summary>
        /// Returns the health status code.
        /// </summary>
        public HealthStatus Status { get; private set; }

        /// <summary>
        /// Returns the status message.
        /// </summary>
        public string Message { get; private set; }

        /// <summary>
        /// Renders the instance into and XML element.
        /// </summary>
        /// <returns>The generated output element.</returns>
        public XElement ToElement()
        {
            return new XElement("HeartbeatStatus",
                        new XAttribute("version", 1),
                        new XElement("Status", this.Status),
                        new XElement("Message", this.Message));
        }
    }
}
