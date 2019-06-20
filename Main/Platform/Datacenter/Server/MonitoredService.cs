//-----------------------------------------------------------------------------
// FILE:        MonitoredService.cs
// CONTRIBUTOR: Jeff Lill
// COPYRIGHT:   Copyright (c) 2005-2015 by Jeffrey Lill.  All rights reserved.
// DESCRIPTION: Describes a service to be monitored by a HeartbeatHandler.

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Reflection;
using System.Runtime.Serialization;
using System.ServiceModel;
using System.Text;
using System.Threading;

using LillTek.Advanced;
using LillTek.Common;
using LillTek.Cryptography;
using LillTek.Datacenter;
using LillTek.Messaging;
using LillTek.Net.Http;
using LillTek.Net.Sockets;

namespace LillTek.Datacenter.Server
{
    /// <summary>
    /// Describes a service to be monitored by a <see cref="HeartbeatHandler" />.
    /// </summary>
    public class MonitoredService
    {
        /// <summary>
        /// The URI for the service to be monitored.
        /// </summary>
        public Uri Uri { get; set; }

        /// <summary>
        /// Indicates whether the service is to be considered critical for determining the
        /// health of the a machine.
        /// </summary>
        public bool IsCritical { get; set; }

        /// <summary>
        /// Constructs an uninitialized instance.
        /// </summary>
        public MonitoredService()
        {
        }

        /// <summary>
        /// Parses the instance from a configuration setting string.
        /// </summary>
        /// <param name="setting">The configuration setting.</param>
        /// <remarks>
        /// <para>
        /// The setting string is formatted as:
        /// </para>
        /// <code language="none">
        /// &lt;uri&gt; [ "," ( "CRITICAL" | "NONCRITICAL" ) ]
        /// </code>
        /// <para>
        /// Where the <b>CRITICAL</b> and <b>NONCRITICAL</b> attributes are optional.
        /// If none of these are specified then <b>cCRITICAL</b> will be assumed.
        /// </para>
        /// </remarks>
        public MonitoredService(string setting)
        {
            string[] fields;

            if (setting == null)
                throw new ArgumentNullException("setting");

            fields = setting.Split(',');
            for (int i = 0; i < fields.Length; i++)
                fields[i] = fields[i].Trim();

            this.Uri = new Uri(fields[0]);

            if (fields.Length == 1)
                this.IsCritical = true;
            else
            {
                switch (fields[1].ToUpper())
                {
                    case "CRITICAL":

                        this.IsCritical = true;
                        break;

                    case "NONCRITICAL":

                        this.IsCritical = false;
                        break;

                    default:

                        throw new ArgumentException(string.Format("Unexpected criticality attribute: {0}", fields[1]));
                }
            }
        }
    }
}
