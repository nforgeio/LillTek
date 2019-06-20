//-----------------------------------------------------------------------------
// FILE:        SharedMemMessage.cs
// CONTRIBUTOR: Jeff Lill
// COPYRIGHT:   Copyright (c) 2005-2015 by Jeffrey Lill.  All rights reserved.
// DESCRIPTION: Base class for application messages to be transmitted between
//              a [SharedMemoryClient] and a [SharedMemoryServer].

using System;
using System.Collections;
using System.Diagnostics;
using System.IO;
using System.IO.MemoryMappedFiles;
using System.Threading;

using LillTek.Common;

namespace LillTek.LowLevel
{
    /// <summary>
    /// Base class for application messages to be transmitted between a
    /// <see cref="SharedMemClient{TMessageFactory}"/> and a 
    /// <see cref="SharedMemServer{TMessageFactory}"/>.
    /// </summary>
    public abstract class SharedMemMessage
    {
        /// <summary>
        /// <b>Internal use only:</b> The unique message ID.
        /// </summary>
        internal Guid InternalRequestId { get; set; }

        /// <summary>
        /// <b>Internal use only:</b> The name of the client inbox where the response is to be delivered.
        /// </summary>
        internal string InternalClientInbox { get; set; }

        /// <summary>
        /// <b>Internal use only:</b> Optionally holds a response error message.
        /// </summary>
        internal string InternalError { get; set; }

        /// <summary>
        /// Deserializes the internal message properties.
        /// </summary>
        /// <param name="input">The input stream.</param>
        internal void InternalReadFrom(EnhancedStream input)
        {
            this.InternalRequestId   = new Guid(input.ReadBytes16());
            this.InternalClientInbox = input.ReadString16();
            this.InternalError       = input.ReadString32();
        }

        /// <summary>
        /// Serializes the internal message properties.
        /// </summary>
        /// <param name="output">The output stream.</param>
        internal void InternalWriteTo(EnhancedStream output)
        {
            output.WriteBytes16(this.InternalRequestId.ToByteArray());
            output.WriteString16(this.InternalClientInbox);
            output.WriteString32(this.InternalError);
        }

        /// <summary>
        /// Returns the typical maximum serialized size for a message of this type.  
        /// </summary>
        /// <remarks>
        /// This is used to initialize the capacity of the <see cref="MemoryStream"/>s
        /// allocated for message serialization for efficiency.  This defaults to
        /// 2KB but may be overridden by the application.
        /// </remarks>
        public virtual int SerializedCapacityHint
        {
            get { return 2048; }
        }

        /// <summary>
        /// Returns the integer code identifying the message type.  Applications that
        /// define more than one message type should override this so the application's
        /// <see cref="ISharedMemMessageFactory"/> will be able to identify which type
        /// of message to create.
        /// </summary>
        public virtual int TypeCode
        {
            get { return 0; }
        }

        /// <summary>
        /// Deserializes the message.
        /// </summary>
        /// <param name="input">The input stream.</param>
        /// <remarks>
        /// <note>
        /// The message <see cref="InternalRequestId"/> and <see cref="InternalClientInbox"/> properties will
        /// have already been deserialized from memory before this is called.
        /// </note>
        /// </remarks>
        public abstract void ReadFrom(EnhancedStream input);

        /// <summary>
        /// Writes the message to shared memory.
        /// </summary>
        /// <param name="output">The output stream.</param>
        /// <remarks>
        /// <note>
        /// The message <see cref="InternalRequestId"/> and <see cref="InternalClientInbox"/> properties will
        /// have already been serialized to memory before this is called.
        /// </note>
        /// </remarks>
        public abstract void WriteTo(EnhancedStream output);
    }
}
