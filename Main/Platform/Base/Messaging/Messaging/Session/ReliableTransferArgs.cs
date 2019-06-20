//-----------------------------------------------------------------------------
// FILE:        ReliableTransferArgs.cs
// CONTRIBUTOR: Jeff Lill
// COPYRIGHT:   Copyright (c) 2005-2015 by Jeffrey Lill.  All rights reserved.
// DESCRIPTION: Defines the arguments passed to ReliableTransferHandler event handlers.

using System;
using System.Reflection;

using LillTek.Common;

namespace LillTek.Messaging
{

    /// <summary>
    /// Defines the <see cref="ReliableTransferHandler" /> event delegate type.
    /// </summary>
    /// <param name="sender">The <see cref="ReliableTransferHandler" /> that raised this event.</param>
    /// <param name="args">The event arguments.</param>
    public delegate void ReliableTransferDelegate(ReliableTransferHandler sender, ReliableTransferArgs args);

    /// <summary>
    /// <see cref="ReliableTransferHandler" /> sets the <see cref="ReliableTransferArgs.TransferEvent" />
    /// property to one of these values before it passes the <see cref="ReliableTransferArgs" />
    /// instance to the event handler.
    /// </summary>
    public enum ReliableTransferEvent
    {
        /// <summary>
        /// This should never be used.
        /// </summary>
        Unknown,

        /// <summary>
        /// Set when <see cref="ReliableTransferHandler.BeginTransferEvent" /> is raised.
        /// </summary>
        BeginTransfer,

        /// <summary>
        /// Set when <see cref="ReliableTransferHandler.EndTransferEvent" /> is raised.
        /// </summary>
        EndTransfer,

        /// <summary>
        /// Set when <see cref="ReliableTransferHandler.SendEvent" /> is raised.
        /// </summary>
        Send,

        /// <summary>
        /// Set when <see cref="ReliableTransferHandler.ReceiveEvent" /> is raised.
        /// </summary>
        Receive
    }

    /// <summary>
    /// Defines the arguments passed to <see cref="ReliableTransferHandler" /> event handlers.
    /// </summary>
    public sealed class ReliableTransferArgs
    {
        /// <summary>
        /// One of the <see cref="ReliableTransferEvent" /> values identifying the
        /// event being raised.
        /// </summary>
        public ReliableTransferEvent TransferEvent;

        /// <summary>
        /// The globally unique ID identifying this transfer.  This is initialized for
        /// all events.
        /// </summary>
        /// <remarks>
        /// <note>
        /// This is not necessarily the same as the session's <see cref="ISession.SessionID" />.
        /// This value is generated separately and may be used across multiple sessions by
        /// various <see cref="ISession" /> implementations.  For example, <see cref="ReliableTransferSession" />
        /// may use the same <see cref="TransferID" /> if it is resuming a partially
        /// completed transfer.
        /// </note>
        /// </remarks>
        public Guid TransferID;

        /// <summary>
        /// A <see cref="TransferDirection" /> value indicating the direction of the
        /// transfer.
        /// </summary>
        public TransferDirection Direction;

        /// <summary>
        /// Application specific arguments.  This is intialized for all events.
        /// </summary>
        public string Args;

        /// <summary>
        /// The maximum number of bytes of data to be transferred in each block.
        /// Initialized by the <see cref="ReliableTransferHandler.ReceiveEvent" /> and 
        /// <see cref="ReliableTransferHandler.SendEvent" /> events.
        /// </summary>
        public int BlockSize;

        /// <summary>
        /// The data being transferred or a zero length array if the end of the
        /// data has been reached.  Initialized by the <see cref="ReliableTransferHandler.ReceiveEvent" />.
        /// </summary>
        public byte[] BlockData;

        /// <summary>
        /// non-<c>null</c> if an error has been detected during the session.  Initialized for the
        /// <see cref="ReliableTransferHandler.EndTransferEvent" />.
        /// </summary>
        public string ErrorMessage;

        /// <summary>
        /// non-<c>null</c> if an error has been detected during the session.  Initialized for the
        /// <see cref="ReliableTransferHandler.EndTransferEvent" />.
        /// </summary>
        public Exception Exception;

        /// <summary>
        /// Constructor.
        /// </summary>
        /// <param name="transferEvent">The <see cref="TransferEvent" /> code identifying the event being raised.</param>
        public ReliableTransferArgs(ReliableTransferEvent transferEvent)
        {
            this.TransferEvent = transferEvent;
        }
    }
}