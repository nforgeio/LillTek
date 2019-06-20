//-----------------------------------------------------------------------------
// FILE:        HealthStatus.cs
// CONTRIBUTOR: Jeff Lill
// COPYRIGHT:   Copyright (c) 2005-2015 by Jeffrey Lill.  All rights reserved.
// DESCRIPTION: Defines the status codes for website heartbeat data.

using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Web;
using System.Web.Hosting;
using System.Xml;
using System.Xml.Linq;

using LillTek.Common;
using LillTek.Service;

namespace LillTek.Web
{
    /// <summary>
    /// Defines the health of an application or subsystem within a <see cref="HeartbeatStatus" />.
    /// </summary>
    public enum HealthStatus
    {
        /// <summary>
        /// Indicates that the application is running normally.
        /// </summary>
        Healthy,

        /// <summary>
        /// Indicates that the application is in a warning state.
        /// </summary>
        Warning,

        /// <summary>
        /// Indicates that the application has serious problems.
        /// </summary>
        Dead
    }
}
