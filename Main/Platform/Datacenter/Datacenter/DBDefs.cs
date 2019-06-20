//-----------------------------------------------------------------------------
// FILE:        DBDefs.cs
// CONTRIBUTOR: Jeff Lill
// COPYRIGHT:   Copyright (c) 2005-2015 by Jeffrey Lill.  All rights reserved.
// DESCRIPTION: Internal LillTek data center server database definitions.

using System;
using System.Net;
using System.Reflection;
using System.Diagnostics;

using LillTek.Common;
using LillTek.Messaging;
using LillTek.Net.Sockets;

namespace LillTek.Datacenter.DBDefs
{
    /// <summary>
    /// Defines the possible sentinel service log entry type IDs.
    /// </summary>
    /// <remarks>
    /// <note>
    /// These values will be encoded into database fields so care should
    /// be taken to avoid changing existing values when adding new type codes.
    /// </note>
    /// </remarks>
    [TSQLPP]
    public enum DBLogEntryTypeID
    {
        /// <summary>
        /// </summary>
        Unknown = 0,

        /// <summary></summary>
        Error = 1,

        /// <summary></summary>
        FailureAudit = 2,

        /// <summary></summary>
        Information = 3,

        /// <summary></summary>
        SuccessAudit = 4,

        /// <summary></summary>
        Warning = 5
    }
}
