//-----------------------------------------------------------------------------
// FILE:        CommunicationCanceledException.cs
// CONTRIBUTOR: Jeff Lill
// COPYRIGHT:   Copyright (c) 2005-2015 by Jeffrey Lill.  All rights reserved.
// DESCRIPTION: Exception thrown when one side of a communication pattern cancels
//              the processing of a request.

using System;
using System.Collections.Generic;
using System.Text;
using System.ServiceModel;
using System.ServiceModel.Channels;

using LillTek.Common;
using LillTek.Messaging;

namespace LillTek.ServiceModel
{
    /// <summary>
    /// Exception thrown when one side of a communication pattern cancels
    /// the processing of a request.
    /// </summary>
    public class CommunicationCanceledException : CommunicationException
    {
        /// <summary>
        /// Constructor.
        /// </summary>
        /// <param name="message">The exception message text.</param>
        public CommunicationCanceledException(string message)
            : base(message)
        {
        }

        /// <summary>
        /// Constructor.
        /// </summary>
        /// <param name="message">The exception message text.</param>
        /// <param name="innerException">The inner <see cref="Exception" />.</param>
        public CommunicationCanceledException(string message, Exception innerException)
            : base(message, innerException)
        {
        }
    }
}
