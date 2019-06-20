//-----------------------------------------------------------------------------
// FILE:        SharedMemErrorMessage.cs
// CONTRIBUTOR: Jeff Lill
// COPYRIGHT:   Copyright (c) 2005-2015 by Jeffrey Lill.  All rights reserved.
// DESCRIPTION: Internal message class used by to communicate error responses
//              from the server back to the client.

using System;
using System.Collections;
using System.Diagnostics;
using System.IO.MemoryMappedFiles;
using System.Threading;

using LillTek.Common;

namespace LillTek.LowLevel
{
    /// <summary>
    /// Internal message class used by to communicate error responses
    /// from the server back to the client.
    /// </summary>
    internal class SharedMemErrorMessage : SharedMemMessage
    {
        /// <summary>
        /// Returns the integer code identifying the message type.  Applications that
        /// define more than one message type should override this so the application's
        /// <see cref="ISharedMemMessageFactory"/> will be able to identify which type
        /// of message to create.
        /// </summary>
        public override int TypeCode
        {
            get { return -1; }
        }

        /// <summary>
        /// Deserializes the message.
        /// </summary>
        /// <param name="input">The input stream.</param>
        public override void ReadFrom(EnhancedStream input)
        {
        }

        /// <summary>
        /// Writes the message to shared memory.
        /// </summary>
        /// <param name="output">The output stream.</param>
        public override void WriteTo(EnhancedStream output)
        {
        }
    }
}
