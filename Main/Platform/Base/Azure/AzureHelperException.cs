//-----------------------------------------------------------------------------
// FILE:        AzureHelperException.cs
// CONTRIBUTOR: Jeff Lill
// COPYRIGHT:   Copyright (c) 2005-2015 by Jeffrey Lill.  All rights reserved.
// DESCRIPTION: Thrown by various LillTek.Azure utilities.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading;

using Microsoft.WindowsAzure;
using Microsoft.WindowsAzure.Diagnostics;
using Microsoft.WindowsAzure.ServiceRuntime;
using Microsoft.WindowsAzure.StorageClient;

using LillTek.Common;

namespace LillTek.Azure
{
    /// <summary>
    /// Thrown by various <b>LillTek.Azure</b> utilities.
    /// </summary>
    public class AzureHelperException : Exception
    {
        /// <summary>
        /// Constructs an exception with a message.
        /// </summary>
        /// <param name="message">The message.</param>
        public AzureHelperException(string message)
            : base(message)
        {
        }

        /// <summary>
        /// Constructs an exception with a message an an inner exception.
        /// </summary>
        /// <param name="message">The message.</param>
        /// <param name="innerException">The inner exception.</param>
        public AzureHelperException(string message, Exception innerException)
            : base(message, innerException)
        {
        }
    }
}
