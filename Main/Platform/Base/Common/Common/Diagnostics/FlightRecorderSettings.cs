//-----------------------------------------------------------------------------
// FILE:        FlightRecorderSettings.cs
// CONTRIBUTOR: Jeff Lill
// COPYRIGHT:   Copyright (c) 2005-2015 by Jeffrey Lill.  All rights reserved.
// DESCRIPTION: Typical settings for applications making use of a FlightRecorder.

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;

namespace LillTek.Common
{
    /// <summary>
    /// Typical settings for applications making use of a <see cref="FlightRecorder" />.
    /// </summary>
    public class FlightRecorderSettings
    {
        /// <summary>
        /// Constructs an instance with reasonable default values.
        /// </summary>
        public FlightRecorderSettings()
        {
            this.Path           = null;
            this.IsEnabled      = true;
            this.MaxEvents      = 1000;
            this.LoginDelay     = TimeSpan.FromSeconds(15);
            this.UploadInterval = TimeSpan.FromSeconds(60);
            this.UploadMax      = 100;
        }

        /// <summary>
        /// Constructs an instance by reading the configuration settings
        /// under a configuration section.
        /// </summary>
        /// <param name="section">The configuration section.</param>
        /// <remarks>
        /// <para>
        /// This method recognizes the following configuration key values:
        /// <b>IsEnabled</b>, <b>MaxEvents</b>, <b>LoginDelay</b>, 
        /// <b>UploadInterval</b>, and <b>UploadMax</b>.
        /// </para>
        /// </remarks>
        public FlightRecorderSettings(Config section)
            : this()
        {
            this.Path           = section.Get("Path", this.Path);
            this.IsEnabled      = section.Get("IsEnabled", this.IsEnabled);
            this.MaxEvents      = section.Get("MaxEvents", this.MaxEvents);
            this.LoginDelay     = section.Get("LoginDelay", this.LoginDelay);
            this.UploadInterval = section.Get("UploadInterval", this.UploadInterval);
            this.UploadMax      = section.Get("UploadMax", this.UploadMax);
        }

        /// <summary>
        /// File system path to the recorder's backing file (or <c>null</c>).
        /// </summary>
        public string Path { get; set; }

        /// <summary>
        /// Controls whether the flight recorder should record or ignore logged events.
        /// </summary>
        public bool IsEnabled { get; set; }

        /// <summary>
        /// The maximum number of flight events to cache on the client side
        /// between uploads to the server.
        /// </summary>
        public int MaxEvents { get; set; }

        /// <summary>
        /// Interval to wait after login before beginning flight recorder uploads.
        /// </summary>
        public TimeSpan LoginDelay { get; set; }

        /// <summary>
        /// Interval at which the client should upload the cached flight events.
        /// </summary>
        public TimeSpan UploadInterval { get; set; }

        /// <summary>
        /// Maximum number of events to be uploaded as a batch.
        /// </summary>
        public int UploadMax { get; set; }
    }
}