//-----------------------------------------------------------------------------
// FILE:        ReliableTransferMsg.cs
// CONTRIBUTOR: Jeff Lill
// COPYRIGHT:   Copyright (c) 2005-2015 by Jeffrey Lill.  All rights reserved.
// DESCRIPTION: Implements the ReliableTransferSession protocol messages.

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Net;
using System.Text;

using LillTek.Common;

namespace LillTek.Messaging
{
    /// <summary>
    /// Used by <see cref="ReliableTransferSession" /> instances to coordinate the
    /// transfer of information between endpoints of a session.
    /// </summary>
    public sealed class ReliableTransferMsg : BlobPropertyMsg
    {
        //---------------------------------------------------------------------
        // Static members

        /// <summary>
        /// Message from the client to the server to initiate the transfer session.
        /// </summary>
        internal const string StartCmd = "start";

        /// <summary>
        /// Response indicating that the transfer session has been accepted.
        /// </summary>
        internal const string StartAck = "start-ack";

        /// <summary>
        /// Message sending a block of data in the <see cref="BlockData" /> property.
        /// A zero length array indicates that the transfer is complete.
        /// </summary>
        internal const string DataCmd = "data";

        /// <summary>
        /// Response indicating that a data block has been received.
        /// </summary>
        internal const string DataAck = "data-ack";

        /// <summary>
        /// Sent by the sending session when it has seen and processed the
        /// last <b>data-ack</b> message from the receiving session.
        /// </summary>
        internal const string CloseCmd = "close";

        /// <summary>
        /// Message indicating that the transfer has been cancelled.
        /// </summary>
        internal const string CancelCmd = "cancel";

        /// <summary>
        /// Message indicating that an error has occurred. 
        /// </summary>
        internal const string ErrorCmd = "error";

        /// <summary>
        /// Returns the type ID string to be used when serializing the message.
        /// </summary>
        public static new string GetTypeID()
        {
            return ".Transfer";
        }

        //---------------------------------------------------------------------
        // Instance members

        /// <summary>
        /// Constructor.
        /// </summary>
        public ReliableTransferMsg()
        {
        }

        /// <summary>
        /// Constructor.
        /// </summary>
        /// <param name="command">The message command string.</param>
        public ReliableTransferMsg(string command)
        {
            this.Command = command;
        }

        /// <summary>
        /// Protected constructor that does not instantiate the property hash table.
        /// </summary>
        /// <param name="param">Pass <see cref="Stub.Param" />.</param>
        private ReliableTransferMsg(Stub param)
            : base(param)
        {
        }

        /// <summary>
        /// Returns the <see cref="ReliableTransferSession" /> associated with this message.
        /// </summary>
        /// <remarks>
        /// <note>
        /// This property is valid only for message received by a message handler.
        /// </note>
        /// </remarks>
        /// <exception cref="InvalidOperationException">Thrown if the message was not received by a message handler.</exception>
        public ReliableTransferSession Session
        {
            get
            {
                if (base._Session == null)
                    throw new InvalidOperationException("Message was not received by a message handler.");

                return (ReliableTransferSession)base._Session;
            }
        }

        /// <summary>
        /// Specifies the purpose of the message.
        /// </summary>
        public string Command
        {
            get { return base._Get("cmd"); }
            set { base._Set("cmd", value); }
        }

        /// <summary>
        /// Returns the requested transfer direction.
        /// </summary>
        public TransferDirection Direction
        {
            get { return base._Get("download", true) ? TransferDirection.Download : TransferDirection.Upload; }
            set { base._Set("download", value == TransferDirection.Download); }
        }

        /// <summary>
        /// The globally unique ID for this transfer.
        /// </summary>
        public Guid TransferID
        {
            get { return base._Get("id", Guid.Empty); }
            set { base._Set("id", value); }
        }

        /// <summary>
        /// Returns the application specific transfer arguments.
        /// </summary>
        public string Args
        {
            get { return base._Get("args"); }
            set { base._Set("args", value); }
        }

        /// <summary>
        /// The block of data being transferred.  A zero length array indicates
        /// that the transfer has been completed.
        /// </summary>
        public byte[] BlockData
        {
            get { return base._Data; }
            set { base._Data = value; }
        }

        /// <summary>
        /// The zero based block number for a transmitted data block for an
        /// receive <see cref="DataAck" /> command.
        /// </summary>
        public int BlockIndex
        {
            get { return base._Get("index", 0); }
            set { base._Set("index", value); }
        }

        /// <summary>
        /// The maximum number of bytes in a data block.
        /// </summary>
        public int BlockSize
        {
            get { return base._Get("size", 0); }
            set { base._Set("size", value); }
        }

        /// <summary>
        /// The exception message for error or cancel commands.
        /// </summary>
        public string Exception
        {
            get { return base._Get("exception", "Unspecified Error"); }
            set { base._Set("exception", value); }
        }

        /// <summary>
        /// Generates a clone of this message instance, generating a new 
        /// <see cref="Msg._MsgID" /> property if the original ID is
        /// not empty.
        /// </summary>
        /// <returns>The cloned message.</returns>
        public override Msg Clone()
        {
            ReliableTransferMsg clone;

            clone = new ReliableTransferMsg(Stub.Param);
            clone.CopyBaseFields(this, true);

            return clone;
        }

#if TRACE
        /// <summary>
        /// Returns tracing details about the message.
        /// </summary>
        /// <returns>The trace string.</returns>
        public string GetTrace()
        {
            var sb = new StringBuilder(512);

            base._TraceDetails(null, sb);
            return sb.ToString();
        }
#endif
    }
}
