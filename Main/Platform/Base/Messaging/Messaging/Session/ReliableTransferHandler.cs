//-----------------------------------------------------------------------------
// FILE:        ReliableTransferHandler.cs
// CONTRIBUTOR: Jeff Lill
// COPYRIGHT:   Copyright (c) 2005-2015 by Jeffrey Lill.  All rights reserved.
// DESCRIPTION: Implements the handler to be used by server side applications
//              to interact with a ReliableTransferSession.

using System;
using System.Diagnostics;
using System.Reflection;

using LillTek.Common;

// $todo(jeff.lill): 
//
// Implement support for restartable transfers where events are
// included so that the reliable transfer session can see if a
// transfer was partially completed in the past and continue
// from that point.

namespace LillTek.Messaging
{
    /// <summary>
    /// Implements the handler to be used by server side applications
    /// to interact with a <see cref="ReliableTransferSession" />.
    /// </summary>
    /// <remarks>
    /// <note>
    /// Event handlers must be registered before a <see cref="ReliableTransferHandler" />
    /// is registered with a session and should not be modified thereafter
    /// to avoid thread synchronization issues.
    /// </note>
    /// </remarks>
    public class ReliableTransferHandler : ISessionHandler
    {
        private ReliableTransferSession     session;
        private bool                        enabled;

        /// <summary>
        /// This can be used to hold application specific state.
        /// </summary>
        public object State = null;

        /// <summary>
        /// Raised when data is received from the client and is ready to
        /// be consumed by the application.
        /// </summary>
        /// <remarks>
        /// </remarks>
        public event ReliableTransferDelegate ReceiveEvent;

        /// <summary>
        /// Raises the <see cref="ReceiveEvent" />.
        /// </summary>
        /// <param name="args">The <see cref="ReliableTransferArgs" /> to be passed to the handlers.</param>
        internal void RaiseReceive(ReliableTransferArgs args)
        {
            if (ReceiveEvent != null && enabled)
                ReceiveEvent(this, args);
        }

        /// <summary>
        /// Raised when the session is ready for additional data to be
        /// sent to the client.
        /// </summary>
        /// <remarks>
        /// </remarks>
        public event ReliableTransferDelegate SendEvent;

        /// <summary>
        /// Raises the <see cref="SendEvent" />.
        /// </summary>
        /// <param name="args">The <see cref="ReliableTransferArgs" /> to be passed to the handlers.</param>
        internal void RaiseSend(ReliableTransferArgs args)
        {
            if (SendEvent != null && enabled)
                SendEvent(this, args);
        }

        /// <summary>
        /// Called when just before the session begins data transmission.
        /// </summary>
        public event ReliableTransferDelegate BeginTransferEvent;

        /// <summary>
        /// Raises the <see cref="BeginTransferEvent" />.
        /// </summary>
        /// <param name="args">The <see cref="ReliableTransferArgs" /> to be passed to the handlers.</param>
        internal void RaiseBeginTransfer(ReliableTransferArgs args)
        {
            if (enabled)
                return;

            if (BeginTransferEvent != null)
                BeginTransferEvent(this, args);

            enabled = true;
        }

        /// <summary>
        /// Raised when the session has completed the data transmission.
        /// </summary>
        public event ReliableTransferDelegate EndTransferEvent;

        /// <summary>
        /// Raises the <see cref="EndTransferEvent" />.
        /// </summary>
        /// <param name="args">The <see cref="ReliableTransferArgs" /> to be passed to the handlers.</param>
        internal void RaiseEndTransfer(ReliableTransferArgs args)
        {
            if (!enabled)
                return;

            if (args.ErrorMessage != null && args.Exception == null)
                args.Exception = SessionException.Create(null, args.ErrorMessage);

            if (EndTransferEvent != null)
                EndTransferEvent(this, args);

            enabled = false;
        }

        /// <summary>
        /// Constructor.
        /// </summary>
        /// <param name="session">The associated session.</param>
        public ReliableTransferHandler(ReliableTransferSession session)
        {
            this.session = session;
            this.enabled = false;
        }

        /// <summary>
        /// Returns the associated <see cref="ISession" />.
        /// </summary>
        public ISession Session
        {
            get { return session; }
        }
    }
}
