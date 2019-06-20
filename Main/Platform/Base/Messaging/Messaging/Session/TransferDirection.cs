//-----------------------------------------------------------------------------
// FILE:        TransferDirection.cs
// CONTRIBUTOR: Jeff Lill
// COPYRIGHT:   Copyright (c) 2005-2015 by Jeffrey Lill.  All rights reserved.
// DESCRIPTION: Specifies the direction of a reliable transfer.

using System;
using System.Reflection;

using LillTek.Common;

namespace LillTek.Messaging
{
    /// <summary>
    /// Specifies the direction of a reliable transfer.
    /// </summary>
    public enum TransferDirection
    {
        /// <summary>
        /// Data is being uploaded from the client to the server.
        /// </summary>
        Upload,

        /// <summary>
        /// Data is being downloaded from the server to the client.
        /// </summary>
        Download
    }

}
