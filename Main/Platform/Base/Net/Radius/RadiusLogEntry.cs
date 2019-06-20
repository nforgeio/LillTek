//-----------------------------------------------------------------------------
// FILE:        RadiusLogEntry.cs
// CONTRIBUTOR: Jeff Lill
// COPYRIGHT:   Copyright (c) 2005-2015 by Jeffrey Lill.  All rights reserved.
// DESCRIPTION: Used for communicating log entries from RADIUS servers.

using System;
using System.Net;
using System.Net.Sockets;

using LillTek.Common;

namespace LillTek.Net.Radius
{
    /// <summary>
    /// Used for communicating log entries from RADIUS servers.
    /// </summary>
    public sealed class RadiusLogEntry
    {
        /// <summary>
        /// Describes the type of log entry.
        /// </summary>
        public readonly RadiusLogEntryType EntryType;

        /// <summary>
        /// Indicates operation success.
        /// </summary>
        public readonly bool Success;

        /// <summary>
        /// The realm (or <c>null</c>).
        /// </summary>
        public readonly string Realm;

        /// <summary>
        /// The account (or <c>null</c>).
        /// </summary>
        public readonly string Account;

        /// <summary>
        /// The log message.
        /// </summary>
        public readonly string Message;

        /// <summary>
        /// Constructor.
        /// </summary>
        /// <param name="success">Success code</param>
        /// <param name="entryType">Indicates the type of log entry.</param>
        /// <param name="realm">The realm (or <c>null</c>).</param>
        /// <param name="account">The account (or <c>null</c>).</param>
        /// <param name="message">The log message.</param>
        public RadiusLogEntry(bool success, RadiusLogEntryType entryType, string realm, string account, string message)
        {
            this.EntryType = entryType;
            this.Success   = success;
            this.Realm     = realm;
            this.Account   = account;
            this.Message   = message;
        }
    }
}
