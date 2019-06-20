//-----------------------------------------------------------------------------
// FILE:        SecurityTokenStatus.cs
// CONTRIBUTOR: Jeff Lill
// COPYRIGHT:   Copyright (c) 2005-2015 by Jeffrey Lill.  All rights reserved.
// DESCRIPTION: Describes the status of a security ticket.

using System;
using System.IO;
using System.Collections.Generic;
using System.Text;

namespace LillTek.Common
{
    /// <summary>
    /// Describes the status of a security ticket.
    /// </summary>
    public enum SecurityTokenStatus
    {
        /// <summary>
        /// The ticket is not valid.
        /// </summary>
        Invalid,

        /// <summary>
        /// The ticket is valid.
        /// </summary>
        Valid,

        /// <summary>
        /// The ticket is valid but has expired.
        /// </summary>
        Expired
    }
}
