//-----------------------------------------------------------------------------
// FILE:        ReliableTransferSession.cs
// CONTRIBUTOR: Jeff Lill
// COPYRIGHT:   Copyright (c) 2005-2015 by Jeffrey Lill.  All rights reserved.
// DESCRIPTION: Implements the client and server sides of a session capable
//              of transferring large volumes of data via LillTek Messaging.

using System;
using System.Diagnostics;
using System.Reflection;
using System.Threading;

using LillTek.Common;

// $todo(jeff.lill): Upgrade to a sliding window messaging protocol.

// $todo(jeff.lill): Add support for MD5 hash based delivery verifications.

// $todo(jeff.lill): Add support for suspend/resume.

// $todo(jeff.lill): Add support for some kind of percent complete indicator.

namespace LillTek.Messaging
{
    /// <summary>
    /// Implements the low-level client and server sides of a session capable
    /// of transferring large volumes of data via LillTek Messaging.  For a higher
    /// level implementation see <see cref="StreamTransferSession" />.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Using this class is somewhat complex.  Consider using <see cref="StreamTransferSession" />
    /// instead.  This class wraps <see cref="ReliableTransferSession" /> functionality to make 
    /// it easy to transfer to/from streams.
    /// </para>
    /// <para>
    /// Reliable transfers are initiated on the client side using the <see cref="MsgRouter" />'s 
    /// <see cref="MsgRouter.CreateReliableTransferSession" /> method.  This method creates the 
    /// client side session and registers it with the router's <see cref="ISessionManager" /> 
    /// implementation.  The client then instantiates the proper <see cref="ISessionHandler" /> type, 
    /// registers any event handlers, assigns the handler instance to the session's <see cref="SessionHandler" /> 
    /// property, and then calls a session type specific method to initiate the session.  Here's
    /// an client side example for the reliable transfer session:
    /// </para>
    /// <code language="cs">
    /// MsgRouter               router;     // Already initialized and started
    /// ReliableTransferSession session;
    /// ReliableTransferHandler handler;
    /// 
    /// session                = router.CreateReliableTransferSession();
    /// handler                = new ReliableTransferHandler(session);
    /// handler.SendEvent     += new ReliableTransferDelegate(MySendHandler);
    /// session.SessionHandler = handler;
    /// session.Transfer("logical://MyApp/Transfer",TransferDirection.Upload,null);
    /// </code>
    /// <para>
    /// On the server side, a message handler needs to be defined to accept
    /// the <see cref="ReliableTransferMsg" /> that is sent to initiate the transfer.
    /// The direction of the transfer can be determined by looking at the
    /// message's <see cref="ReliableTransferMsg.Direction" /> property and the application specific
    /// arguments can be obtained from the <see cref="ReliableTransferMsg.Args" /> property.  Note
    /// that the message handler can choose to reject the transfer by throwning
    /// an exception.  This will be converted into a <see cref="SessionException" />
    /// and will eventually be rethrown on the client.
    /// </para>
    /// <para>
    /// Server message handlers will need to allocate a <see cref="ReliableTransferHandler" />
    /// instance, and register handlers for the <see cref="ReliableTransferHandler.ReceiveEvent" /> 
    /// or <see cref="ReliableTransferHandler.SendEvent" /> event, depending on the direction of 
    /// the transfer.  <see cref="ReliableTransferHandler.ReceiveEvent" /> will be raised 
    /// when a data block is received from the client.  <see cref="ReliableTransferHandler.SendEvent" />
    /// is raised when the session is ready to send more data to the client.  Here's an
    /// example:
    /// </para>
    /// <code language="cs">
    /// [MsgHandler(LogicalEP="logical://MyApp/Transfer")]
    /// [MsgSession(Type=SessionTypeID.ReliableTransfer)]
    /// public void OnMsg(ReliableTransferMsg msg) 
    /// {
    ///     ReliableTransferSession     session = msg.Session;
    ///     ReliableTransferHandler     handler;
    /// 
    ///     if (msg.Direction == TransferDirection.Upload)
    ///         throw new Exception("Uploads are not accepted.");
    /// 
    ///     handler                = new ReliableTransferHandler(session);
    ///     handler.ReceiveEvent  += new ReliableTransferDelegate(MyReceiveHandler);
    ///     session.SessionHandler = handler;
    /// }
    /// </code>
    /// <para>
    /// Note that the reliable transfer session is implicitly asynchronous so the
    /// <see cref="MsgSessionAttribute" />'s <see cref="MsgSessionAttribute.IsAsync" />
    /// property does not need to be explicitly set to <c>true</c>.  By default, the session
    /// will use session settings loaded from the application configuration's 
    /// <b>MsgRouter.ReliableTransfer</b> section.   These values can be specified explicitly 
    /// for an message handler via the <see cref="MsgSessionAttribute" />'s 
    /// <see cref="MsgSessionAttribute.Parameters" /> property as shown below
    /// (see <see cref="ReliableTransferSettings.LoadConfig" /> for a list of the
    /// possible settings):
    /// </para>
    /// <code language="cs">
    /// [MsgHandler(LogicalEP="logical://MyApp/Transfer")]
    /// [MsgSession(Type=SessionTypeID.ReliableTransfer,Paramsters="RetryWaitTime=5s;MaxTries=2")]
    /// public void OnMsg(ReliableTransferMsg msg) {
    /// 
    /// }
    /// </code>
    /// <para><b><u>Configuration Settings</u></b></para>
    /// <para>
    /// Reliable transfer sessions load their configuration settings from the
    /// <b>MsgRouter.ReliableTransfer</b> section of the application configuration.
    /// The current supported settings are described at 
    /// <see cref="ReliableTransferSettings.LoadConfig" />.
    /// </para>
    /// <para><b><u>Reliable Transfer Protocol</u></b></para>
    /// <para>
    /// The current implementation of the reliable transfer protocol is very simplistic.
    /// In essence, the sending side if the session transmits one block of data
    /// at a time and waits for an receive acknowledgement before sending the next
    /// block.  The sender will attempt a few retransmissions if to acknowledgement
    /// is received before timing out.  Either side can cancel the transfer or
    /// signal an error at any time.
    /// </para>
    /// <para>
    /// This implementation will tend to be slow, especially when the network latency
    /// is high.  The can be somewhat compensated for by using large block sizes
    /// 64-128K bytes but the real solution is to implement sliding window scheme
    /// similar to how TCP/IP works.  The current implementation also lacks the
    /// ability to restart a partially complete transfer or a way to implement a
    /// CRC or MD5 test to verify that the data was not corrupted.  These issues
    /// will be addressed in the near future.
    /// </para>
    /// <para>
    /// The protocol works by transmitting <see cref="ReliableTransferMsg" /> messages
    /// between the client and server sides of the session.  Reliable transfer sessions
    /// are always initiated by a client but the data can be transfered in either
    /// direction.  The <see cref="TransferDirection" /> enumeration is used to 
    /// specify this direction.  <see cref="TransferDirection.Upload" /> indicates
    /// that data is being transferred from the client to the server. 
    /// <see cref="TransferDirection.Download" /> indicates that the data is
    /// being transferred other other way: from the server to the client.
    /// </para>
    /// <para>
    /// Note that each side of the session will set the <see cref="Msg._FromEP" />
    /// property of the message's it sends to its router's physical endpoint.  After
    /// the first session initiation message, all further messages will be targeted
    /// at these physical endpoints to ensure that messages will be forwarded 
    /// correctly.
    /// </para>
    /// <para>
    /// The message's <see cref="ReliableTransferMsg.Command" /> property indicates
    /// the purpose of the message.  This property will have one of these
    /// values:
    /// </para>
    /// <list type="table">
    ///     <item>
    ///         <term><b>start</b></term>
    ///         <description>
    ///         Message from the client to the server to initiate the transfer session.  This
    ///         message will also include valid <see cref="ReliableTransferMsg.Direction" />,
    ///         <see cref="ReliableTransferMsg.TransferID" />, <see cref="ReliableTransferMsg.Args" />, 
    ///         and <see cref="ReliableTransferMsg.BlockSize" /> properties.
    ///         </description>
    ///     </item>
    ///     <item>
    ///         <term><b>start-ack</b></term>
    ///         <description>
    ///         Response indicating that the transfer session has been accepted.  This message
    ///         includes a valid <see cref="ReliableTransferMsg.BlockSize" /> property which
    ///         indicates the actual block size to be used for the transfer.
    ///         </description>
    ///     </item>
    ///     <item>
    ///         <term><b>data</b></term>
    ///         <description>
    ///         Message sending a block of data in the <see cref="ReliableTransferMsg.BlockData" /> 
    ///         property.  A zero length array indicates that the transfer is complete.
    ///         The <see cref="ReliableTransferMsg.BlockIndex" /> property will be set to the zero-based
    ///         index of this block in the sequence.
    ///         </description>
    ///     </item>
    ///     <item>
    ///         <term><b>data-ack</b></term>
    ///         <description>
    ///         Response indicating that the data block specified by <see cref="ReliableTransferMsg.BlockIndex" />
    ///         has been received.  
    ///         </description>
    ///     </item>
    ///     <item>
    ///         <term><b>close</b></term>
    ///         <description>
    ///         Sent by the sending session indicating that it has seen the last <b>data-ack</b>
    ///         and is ready to to terminate the session.  The receiving session will respond 
    ///         will continue resending the last <b>data-ack</b> until it sees the <b>close</b> 
    ///         or times out.  Note that the receiving session will not throw an exception if it 
    ///         doesn't see a <b>closeo</b> message as long as it received the last <b>data</b>
    ///         message.
    ///         </description>
    ///     </item>
    ///     <item>
    ///         <term><b>cancel</b></term>
    ///         <description>
    ///         Message indicating that the transfer has been cancelled.
    ///         </description>
    ///     </item>
    ///     <item>
    ///         <term><b>error</b></term>
    ///         <description>
    ///         Message indicating that an error has occurred.  <see cref="ReliableTransferMsg.Exception" />
    ///         will be set to the exception message string.
    ///         </description>
    ///     </item>
    /// </list>
    /// <para>
    /// Here's a summary of how the current transfer protocol works:
    /// </para>
    /// <list type="number">
    ///     <item>
    ///     The client sends a <b>start</b> message to the server along with the
    ///     transfer <b>direction</b>, the <b>transfer ID</b>, the application specific 
    ///     <b>args</b>, and the <b>blocksize</b>.
    ///     </item>
    ///     <item>
    ///     The server validates the request and responds with a <b>send-ack</b> if
    ///     the transfer is accepted, <b>error</b> if it is not.  The server
    ///     chooses the actual <b>blocksize</b> to be used for the transfer.
    ///     This can be the same as the <b>blocksize</b> value passed in the
    ///     <b>start</b> message or another value determined by the server.
    ///     The actual <b>blocksize</b> will be included in the <b>send-ack</b>
    ///     response message.
    ///     </item>
    ///     <item>
    ///     The client waits for the <b>send-ack</b> or <b>error</b> response,
    ///     resending the <b>start</b> message a few times before giving up.
    ///     </item>
    ///     <item>
    ///     If the transfer was accepted by the server.  Then data transmission
    ///     will begin.  The sending side will one data block at a time via
    ///     a <b>data</b> command and wait for a <b>data-ack</b> before sending
    ///     the next block.  If the sender doesn't receive a <b>data-ack</b> 
    ///     then it will try resending the block a few times before giving up
    ///     and sending an <b>error</b>.
    ///     </item>
    ///     <item>
    ///     The receiving side will process the <b>data</b> packets, ignoring
    ///     any out of sequence packets and replying with <b>send-ack</b>s.
    ///     </item>
    ///     <item>
    ///     The sending side indicates that there's no more data by sending
    ///     a <b>send</b> with a zero length <b>data</b> block.
    ///     </item>
    ///     <item>
    ///     The receiving side replies with a final <b>data-ack</b>.  The sending side 
    ///     waits for this and responds with a single <b>close</b> message.  The receiving 
    ///     side continues to resend the last <b>data-ack</b> message until it sees the 
    ///     <b>close</b> or times out.
    ///     </item>
    ///     <item>
    ///     Either side can send a <b>cancel</b> or <b>error</b> message to the
    ///     other side to stop the transfer immediately.
    ///     </item>
    /// </list>
    /// </remarks>
    public class ReliableTransferSession : SessionBase, ISession
    {
        //---------------------------------------------------------------------
        // State machine related types

        /// <summary>
        /// The transfer states.
        /// </summary>
        private enum State
        {
            /// <summary>
            /// The state machine has not been initialized.
            /// </summary>
            Unknown,

            /// <summary>
            /// The client is initiating the session by sending
            /// <b>start</b> messages to the server and waiting for
            /// a <b>start-ack</b> response.
            /// </summary>
            ClientStart,

            /// <summary>
            /// Handles the data transfer from the sender side.
            /// </summary>
            Sending,

            /// <summary>
            /// Handles the data transfer from the receiver side.
            /// </summary>
            Receiving,

            /// <summary>
            /// The transfer is complete.
            /// </summary>
            Done
        }

        /// <summary>
        /// Describes the behavior of a transfer state machine state.
        /// </summary>
        private interface IStateProcessor
        {
            /// <summary>
            /// Initializes the state processor, associating the transfer session.
            /// </summary>
            /// <param name="session">The transfer session.</param>
            void Initialize(ReliableTransferSession session);

            /// <summary>
            /// Called periodically to handle background activities.
            /// </summary>
            void BkTask();

            /// <summary>
            /// Called when a cluster member protocol message is received.
            /// </summary>
            /// <param name="msg">The message received.</param>
            void OnMessage(ReliableTransferMsg msg);
        }

        /// <summary>
        /// handles processing for the <see cref="State.ClientStart" /> state.
        /// </summary>
        private sealed class ClientStartState : IStateProcessor
        {
            private ReliableTransferSession     session;
            private int                         cSend;
            private int                         cSendLimit;
            private TimeSpan                    retryInterval;
            private DateTime                    resendTime;

            /// <summary>
            /// Initializes the state processor, associating the transfer session.
            /// </summary>
            /// <param name="session">The transfer session.</param>
            public void Initialize(ReliableTransferSession session)
            {
                ReliableTransferMsg msg;

                this.session       = session;
                this.cSend         = 0;
                this.cSendLimit    = session.settings.MaxTries;
                this.retryInterval = session.settings.RetryWaitTime;
                this.resendTime    = SysTime.Now + retryInterval;

                msg            = new ReliableTransferMsg(ReliableTransferMsg.StartCmd);
                msg._FromEP    = session.localEP;
                msg._Flags    |= MsgFlag.OpenSession;

                msg.Direction  = session.direction;
                msg.TransferID = session.transferID;
                msg.Args       = session.args;
                msg.BlockSize  = session.blockSize;

                cSend++;
                session.SendTo(session.serverEP, msg);
            }

            /// <summary>
            /// Called periodically to handle background activities.
            /// </summary>
            public void BkTask()
            {
                const string TimeoutMsg = "Timeout establishing a session with the server.";

                ReliableTransferMsg msg;

                if (SysTime.Now >= resendTime)
                {
                    if (cSend >= cSendLimit)
                    {
                        session.Trace(0, "[start] retry exceeded");

                        session.SendError(TimeoutMsg);
                        session.Notify(new TimeoutException(TimeoutMsg));
                        session.SetState(State.Done);
                        return;
                    }

                    // Retry sending the start message

                    msg            = new ReliableTransferMsg(ReliableTransferMsg.StartCmd);
                    msg._FromEP    = session.localEP;
                    msg._Flags    |= MsgFlag.OpenSession;

                    msg.Direction  = session.direction;
                    msg.TransferID = session.transferID;
                    msg.Args       = session.args;
                    msg.BlockSize  = session.blockSize;
                    resendTime     = SysTime.Now + session.settings.RetryWaitTime;

                    cSend++;
                    session.Trace(0, "Resending [start]");
                    session.SendTo(session.serverEP, msg);
                }
            }

            /// <summary>
            /// Called when a cluster member protocol message is received.
            /// </summary>
            /// <param name="msg">The message received.</param>
            public void OnMessage(ReliableTransferMsg msg)
            {
                switch (msg.Command)
                {
                    case ReliableTransferMsg.StartAck:

                        session.blockSize = msg.BlockSize;
                        session.remoteEP = msg._FromEP.Clone(true);
                        session.SetState(session.GetTransferState());
                        break;

                    case ReliableTransferMsg.DataCmd:

                        if (session.direction == TransferDirection.Download)
                        {
                            if (msg.BlockIndex != 0)
                            {
                                session.Notify(SessionException.Create(null, "Missed the first data block."));
                                session.SetState(State.Done);
                            }
                            else
                            {
                                // The start-ack must have gotten lost but we do have the
                                // first data block.  We'll transition to the receiving state.

                                // $todo(jeff.lill): 
                                // 
                                // The first block is going to end up being retransmitted
                                // since there's no nice way to pass it to the receive
                                // state and I'm trying to keep the code clean.

                                session.remoteEP = msg._FromEP.Clone(true);
                                session.SetState(State.Receiving);
                            }
                        }
                        break;

                    case ReliableTransferMsg.CancelCmd:

                        session.Notify(new CancelException());
                        break;

                    case ReliableTransferMsg.ErrorCmd:

                        session.Notify(SessionException.Create(null, msg.Exception));
                        break;

                    default:

                        session.Trace(0, string.Format("Discarding [{0}] message", msg.Command));
                        return;
                }
            }
        }

        /// <summary>
        /// handles processing for the <see cref="State.Sending" /> state.
        /// </summary>
        private sealed class SendingState : IStateProcessor
        {
            private ReliableTransferSession     session;
            private byte[]                      blockData;
            private int                         blockIndex;
            private bool                        ackPending;
            private bool                        lastBlock;
            private int                         cSend;
            private DateTime                    ackLimit;

            /// <summary>
            /// Initializes the state processor, associating the transfer session.
            /// </summary>
            /// <param name="session">The transfer session.</param>
            public void Initialize(ReliableTransferSession session)
            {
                try
                {
                    session.handler.RaiseBeginTransfer(session.GetTransferArgs(ReliableTransferEvent.BeginTransfer));

                    this.session    = session;
                    this.blockIndex = 0;
                    this.ackPending = true;
                    this.lastBlock  = false;
                    this.cSend      = 0;

                    SendNextBlock();
                }
                catch (Exception e)
                {
                    SysLog.LogException(e);
                    session.SendError(e.Message);
                    session.Notify(e);
                    session.SetState(State.Done);
                    return;
                }
            }

            /// <summary>
            /// Gets the next block via the <see cref="ReliableTransferHandler" /> and
            /// initiates transmission.
            /// </summary>
            private void SendNextBlock()
            {
                ReliableTransferArgs    args = session.GetTransferArgs(ReliableTransferEvent.Send);
                ReliableTransferMsg     msg;

                session.handler.RaiseSend(args);
                if (args.BlockData == null)
                    args.BlockData = new byte[0];

                if (args.BlockData.Length > session.blockSize)
                    throw new ArgumentException("[SessionHandler.SendEvent] returned a block larger the maximum.");

                msg            = new ReliableTransferMsg(ReliableTransferMsg.DataCmd);
                msg.BlockIndex = blockIndex;
                msg.BlockData  =
                blockData      = args.BlockData;
                lastBlock      = blockData.Length == 0;

                ackLimit       = SysTime.Now + session.settings.RetryWaitTime;
                ackPending     = true;

                cSend = 1;
                session.SendTo(session.remoteEP, msg);
            }

            /// <summary>
            /// Called periodically to handle background activities.
            /// </summary>
            public void BkTask()
            {
                const string TimeoutMsg = "Timeout waiting for data-ack.";

                if (SysTime.Now < ackLimit)
                    return;

                if (ackPending)
                {
                    // Resend the message if we haven't exhausted the retries.

                    if (cSend >= session.settings.MaxTries)
                    {

                        session.SendError(TimeoutMsg);
                        session.Notify(new TimeoutException(TimeoutMsg));
                        session.SetState(State.Done);
                        return;
                    }

                    ReliableTransferMsg msg;

                    msg            = new ReliableTransferMsg(ReliableTransferMsg.DataCmd);
                    msg.BlockIndex = blockIndex;
                    msg.BlockData  = blockData;

                    ackLimit   = SysTime.Now + session.settings.RetryWaitTime;
                    ackPending = true;

                    cSend++;
                    session.Trace(0, "Resend Data");
                    session.SendTo(session.remoteEP, msg);
                }
            }

            /// <summary>
            /// Called when a cluster member protocol message is received.
            /// </summary>
            /// <param name="msg">The message received.</param>
            public void OnMessage(ReliableTransferMsg msg)
            {
                switch (msg.Command)
                {
                    case ReliableTransferMsg.CancelCmd:

                        session.Notify(new CancelException());
                        break;

                    case ReliableTransferMsg.ErrorCmd:

                        session.Notify(SessionException.Create(null, msg.Exception));
                        break;

                    case ReliableTransferMsg.DataAck:

                        if (msg.BlockIndex != blockIndex)
                        {
                            session.Trace(0, string.Format("Discarding [{0}] expected [index={1}] received [index={2}]", msg.Command, blockIndex, msg.BlockIndex));
                            return;
                        }

                        ackLimit = SysTime.Now + session.settings.RetryWaitTime;

                        if (lastBlock)
                        {
                            session.SendTo(session.remoteEP, new ReliableTransferMsg(ReliableTransferMsg.CloseCmd));
                            session.Notify();
                            session.SetState(State.Done);
                            return;
                        }

                        try
                        {
                            blockIndex++;
                            SendNextBlock();
                        }
                        catch (Exception e)
                        {
                            SysLog.LogException(e);
                            session.SendError(e.Message);
                            session.Notify(e);
                            session.SetState(State.Done);
                        }
                        break;

                    default:

                        session.Trace(0, string.Format("Discarding [{0}] message", msg.Command));
                        return;
                }
            }
        }

        /// <summary>
        /// Handles processing for the <see cref="State.Receiving" /> state.
        /// </summary>
        private sealed class ReceivingState : IStateProcessor
        {
            private ReliableTransferSession     session;
            private TimeSpan                    waitInterval;
            private DateTime                    waitLimit;
            private DateTime                    resendAckTime;
            private int                         blockIndex;
            private bool                        waitForClose;
            private bool                        started;

            /// <summary>
            /// Initializes the state processor, associating the transfer session.
            /// </summary>
            /// <param name="session">The transfer session.</param>
            public void Initialize(ReliableTransferSession session)
            {
                try
                {
                    session.handler.RaiseBeginTransfer(session.GetTransferArgs(ReliableTransferEvent.BeginTransfer));

                    this.session       = session;
                    this.blockIndex    = 0;
                    this.waitInterval  = Helper.Multiply(session.settings.RetryWaitTime, session.settings.MaxTries);
                    this.waitLimit     = SysTime.Now + waitInterval;
                    this.resendAckTime = SysTime.Now + session.settings.RetryWaitTime;
                    this.waitForClose  = false;
                    this.started       = false;
                }
                catch (Exception e)
                {
                    session.SendError(e.Message);
                    session.Notify(e);
                    session.SetState(State.Done);
                    return;
                }
            }

            /// <summary>
            /// Called periodically to handle background activities.
            /// </summary>
            public void BkTask()
            {
                var now = SysTime.Now;

                if (now >= waitLimit)
                {
                    if (waitForClose)
                    {
                        session.Notify();
                        session.SetState(State.Done);
                    }
                    else
                    {
                        session.SendError("Timeout waiting for data.");
                        session.Notify(new TimeoutException());
                        session.SetState(State.Done);
                    }

                    return;
                }
                else if (started && now >= resendAckTime)
                {
                    // Resend the data-ack for the last block in case
                    // it was locked.

                    ReliableTransferMsg ack;

                    ack = new ReliableTransferMsg(ReliableTransferMsg.DataAck);
                    ack.BlockIndex = blockIndex - 1;
                    session.SendTo(session.remoteEP, ack);

                    resendAckTime = now + session.settings.RetryWaitTime;
                }
            }

            /// <summary>
            /// Called when a cluster member protocol message is received.
            /// </summary>
            /// <param name="msg">The message received.</param>
            public void OnMessage(ReliableTransferMsg msg)
            {
                ReliableTransferMsg ack;

                switch (msg.Command)
                {
                    case ReliableTransferMsg.StartCmd:

                        ack = new ReliableTransferMsg(ReliableTransferMsg.StartAck);
                        ack.BlockSize = session.blockSize;

                        session.SendTo(session.remoteEP, ack);
                        break;

                    case ReliableTransferMsg.CancelCmd:

                        session.Notify(new CancelException());
                        break;

                    case ReliableTransferMsg.ErrorCmd:

                        session.Notify(SessionException.Create(null, msg.Exception));
                        break;

                    case ReliableTransferMsg.DataCmd:

                        if (msg.BlockIndex != blockIndex)
                        {
                            session.Trace(0, string.Format("Discarding [{0}] expected [index={1}] received [index={2}]", msg.Command, blockIndex, msg.BlockIndex));
                            return;
                        }

                        started = true;

                        ack = new ReliableTransferMsg(ReliableTransferMsg.DataAck);
                        ack.BlockIndex = msg.BlockIndex;
                        session.SendTo(session.remoteEP, ack);

                        blockIndex++;
                        waitLimit = SysTime.Now + waitInterval;
                        resendAckTime = SysTime.Now + session.settings.RetryWaitTime;

                        try
                        {
                            var args = session.GetTransferArgs(ReliableTransferEvent.Receive);

                            args.BlockData = msg.BlockData;

                            session.handler.RaiseReceive(args);
                        }
                        catch (Exception e)
                        {
                            SysLog.LogException(e);
                            session.Notify(e);
                            session.SetState(State.Done);
                            session.SendError(e.Message);
                            return;
                        }

                        if (msg.BlockData.Length == 0)
                            waitForClose = true;

                        break;

                    case ReliableTransferMsg.CloseCmd:

                        session.Notify();
                        session.SetState(State.Done);
                        break;

                    default:

                        session.Trace(0, string.Format("Discarding [{0}] message", msg.Command));
                        break;
                }
            }
        }

        /// <summary>
        /// handles processing for the <see cref="State.Done" /> state.
        /// </summary>
        private sealed class DoneState : IStateProcessor
        {
            private ReliableTransferSession session;

            /// <summary>
            /// Initializes the state processor, associating the transfer session.
            /// </summary>
            /// <param name="session">The transfer session.</param>
            public void Initialize(ReliableTransferSession session)
            {
                ReliableTransferArgs args;

                this.session = session;

                args = session.GetTransferArgs(ReliableTransferEvent.EndTransfer);
                args.ErrorMessage = session.errorMessage;
                session.handler.RaiseEndTransfer(args);
            }

            /// <summary>
            /// Called periodically to handle background activities.
            /// </summary>
            public void BkTask()
            {
            }

            /// <summary>
            /// Called when a cluster member protocol message is received.
            /// </summary>
            /// <param name="msg">The message received.</param>
            public void OnMessage(ReliableTransferMsg msg)
            {
                session.Trace(0, string.Format("Discarding [{0}] message", msg.Command));
            }
        }

        //---------------------------------------------------------------------
        // Implementation

        /// <summary>
        /// The trace subsystem name for the <see cref="ReliableTransferSession" /> and related classes.
        /// </summary>
        public const string TraceSubsystem = "Messaging.ReliableTransfer";

        /// <summary>
        /// The standard configuration key prefix for the <see cref="ReliableTransferSettings" />.
        /// </summary>
        public const string ConfigPrefix = "MsgRouter.ReliableTransfer";

        // It's going to be too slow to load the reliable transfer settings from the
        // application configuration each time a session is constructed so I'm
        // going to load these once and cache them globally here.

        private static ReliableTransferSettings cachedSettings = null;

        /// <summary>
        /// Clears the cached <see cref="ReliableTransferSettings" />.
        /// </summary>
        /// <remarks>
        /// For performance reasons, <see cref="ReliableTransferSession" /> statically caches the
        /// settings loaded from the application configuration across instance creations.
        /// Occasionally, it will be useful (primarily for UNIT testing) to clear this cache 
        /// to force new settings to be loaded.
        /// </remarks>
        public static void ClearCachedSettings()
        {
            cachedSettings = null;
        }

        private Guid                        transferID;         // The transfer ID
        private ReliableTransferHandler     handler;            // The transfer handler
        private ReliableTransferSettings    settings;           // The transfer settings
        private State                       state;              // The current state
        private IStateProcessor             stateProcessor;     // The current state processor
        private MsgRouter                   router;             // The associated router
        private object                      syncLock;           // The thread synchronization object
        private MsgEP                       localEP;            // Physical endpoint of this side of the session
        private MsgEP                       remoteEP;           // Physical endpoint of the other side of the session
        private TransferDirection           direction;          // The transfer direction
        private string                      args;               // The application specific arguments
        private int                         blockSize;          // The application specific block size
        private MsgEP                       serverEP;           // The server side session endpoint (valid only for client side)
        private AsyncResult                 arTransfer;         // The transfer IAsyncResult (valid only for client side)
        private string                      errorMessage;       // The error message to be reported to ISessionHandler.EndTransferEvent.
        private bool                        inMsgHandler;       // True if a server session is currently executing the message handler
        private NetFailMode                 networkMode;        // Used by UNIT tests to simulate network failures.

        /// <summary>
        /// Constructs a reliable transfer session with explicit configuration settings.
        /// </summary>
        /// <param name="settings">
        /// The <see cref="ReliableTransferSettings" /> to use or <c>null</c> to
        /// load settings from the application configuration (see 
        /// <see cref="ReliableTransferSettings.LoadConfig" />for more information).
        /// </param>
        public ReliableTransferSession(ReliableTransferSettings settings)
            : base()
        {
            if (settings == null)
            {
                lock (typeof(ReliableTransferSession))
                {
                    if (cachedSettings == null)
                        cachedSettings = ReliableTransferSettings.LoadConfig(ConfigPrefix);

                    settings = cachedSettings.Clone();
                }
            }

            this.handler        = null;
            this.settings       = settings;
            this.state          = State.Unknown;
            this.stateProcessor = null;
            this.remoteEP       = null;
            this.errorMessage   = null;
            this.arTransfer     = null;
            this.inMsgHandler   = false;
            this.networkMode    = NetFailMode.Normal;
        }

        /// <summary>
        /// Constructs a reliable transfer session, loading settings from the 
        /// application configuration.  See <see cref="ReliableTransferSettings.LoadConfig" />
        /// for more information.
        /// </summary>
        public ReliableTransferSession()
            : this(null)
        {
        }

        /// <summary>
        /// Sets the specified member state, performing the necessary initialization
        /// and cleanup required for the state transition.
        /// </summary>
        /// <param name="newState">The new member state.</param>
        private void SetState(State newState)
        {
            if (this.state == newState)
                return;

            Trace(0, string.Format("{0} --> {1}", this.state, newState), new CallStack(1, true).ToString());

            State   orgState;

            orgState = this.state;
            if (stateProcessor != null)
                stateProcessor = null;

            this.state = newState;

            switch (newState)
            {

               case State.ClientStart:

                    stateProcessor = new ClientStartState();
                    break;

                case State.Sending:

                    stateProcessor = new SendingState();
                    break;

                case State.Receiving:

                    stateProcessor = new ReceivingState();
                    break;

                case State.Done:

                    stateProcessor = new DoneState();
                    break;

                default:

                    throw new NotImplementedException();
            }

            if (stateProcessor != null)
                stateProcessor.Initialize(this);
        }

        /// <summary>
        /// Writes information to the <see cref="NetTrace" />, adding some member
        /// state information.
        /// </summary>
        /// <param name="detail">The detail level 0..255 (higher values indicate more detail).</param>
        /// <param name="summary">The summary string.</param>
        /// <param name="details">The trace details (or <c>null</c>).</param>
        [Conditional("TRACE")]
        internal void Trace(int detail, string summary, string details)
        {
            const string headerFmt = "state = {0}\r\n----------\r\n";
            string header;

            header = string.Format(headerFmt, state, "na");
            if (details == null)
                details = string.Empty;

            NetTrace.Write(TraceSubsystem, detail, string.Format("Transfer: [mode={0} state={1}]", base.IsClient ? "CLIENT" : "SERVER", state), summary, header + details);
        }

        /// <summary>
        /// Writes information to the <see cref="NetTrace" />, adding some member
        /// state information.
        /// </summary>
        /// <param name="detail">The detail level 0..255 (higher values indicate more detail).</param>
        /// <param name="summary">The summary string.</param>
        [Conditional("TRACE")]
        internal void Trace(int detail, string summary)
        {
            Trace(detail, summary, null);
        }

        /// <summary>
        /// Returns the globally unique ID for this transfer.
        /// </summary>
        public Guid TransferID
        {
            get { return transferID; }
        }

        /// <summary>
        /// The <see cref="ISessionHandler" /> used to implement advanced multi-message
        /// session scenarios.
        /// </summary>
        /// <remarks>
        /// <note>
        /// <see cref="ISession" /> implementations must verify that
        /// the session assigned is valid for the implementation and throw an
        /// <see cref="InvalidOperationException" /> if this is not the case.
        /// </note>
        /// </remarks>
        /// <exception cref="InvalidOperationException">
        /// Thrown if a instance with a type other than <see cref="ReliableTransferHandler" /> is
        /// passed or if a different object is being assigned.
        /// </exception>
        public override ISessionHandler SessionHandler
        {
            get { return handler; }

            set
            {
                if (value != null && handler != null && !object.ReferenceEquals(handler, value))
                    throw new InvalidOperationException("Cannot reassign the [SessionHandler] property after it has been initialized.");

                handler = value as ReliableTransferHandler;
                if (handler == null)
                    throw new InvalidOperationException(string.Format("Only instances derived from [{0}] can be assigned to [SessionHandler].", typeof(ReliableTransferHandler).Name));
            }
        }

        /// <summary>
        /// Returns <c>true</c> a server side session is currently executing the session
        /// initiation message handler.
        /// </summary>
        public bool InMsgHandler
        {
            get { return inMsgHandler; }
        }

        /// <summary>
        /// Returns the object instance used for thread synchronization.
        /// </summary>
        public object SyncRoot
        {
            get { return syncLock; }
        }

        /// <summary>
        /// Figures out which transfer state to use for this session.
        /// </summary>
        /// <returns><see cref="State.Sending" /> or <see cref="State.Receiving" /></returns>
        private State GetTransferState()
        {
            if (IsClient)
                return direction == TransferDirection.Upload ? State.Sending : State.Receiving;
            else
                return direction == TransferDirection.Upload ? State.Receiving : State.Sending;
        }

        /// <summary>
        /// Returns the new <see cref="ReliableTransferArgs" /> instance initialized with
        /// from global session state.
        /// </summary>
        /// <param name="transferEvent">The <see cref="ReliableTransferEvent" /> that's being raised.</param>
        private ReliableTransferArgs GetTransferArgs(ReliableTransferEvent transferEvent)
        {
            var args = new ReliableTransferArgs(transferEvent);

            args.Direction    = direction;
            args.TransferID   = transferID;
            args.Args         = this.args;
            args.BlockSize    = blockSize;
            args.BlockData    = null;
            args.ErrorMessage = null;
            args.Exception    = null;

            return args;
        }

        /// <summary>
        /// Set to <c>true</c> to simulate a network failures.  This is available only for 
        /// unit tests.
        /// </summary>
        internal NetFailMode NetworkMode
        {
            get { return networkMode; }
            set { networkMode = value; }
        }

        /// <summary>
        /// Cancels the session.  This can be called on either the client or server side of a session.
        /// </summary>
        public override void Cancel()
        {
            Cancel(null);
        }

        /// <summary>
        /// Cancels the session.  This can be called on either the client or server side of a session.
        /// </summary>
        /// <param name="error">The error message (or <c>null</c>).</param>
        /// <remarks>
        /// <note>
        /// If <paramref name="error" /> is passed as null then a <see cref="CancelException" />
        /// will be thrown on the client side.  If this parameter is not <c>null</c> then a <see cref="SessionException" />
        /// will be thrown on the client side
        /// </note>
        /// </remarks>
        public void Cancel(string error)
        {
            using (TimedLock.Lock(syncLock))
            {
                SendTo(remoteEP, new ReliableTransferMsg(ReliableTransferMsg.CancelCmd));
                Notify(new CancelException());
                SetState(State.Done);
            }
        }

        /// <summary>
        /// Handles any received messages (not including the first message directed to the server) 
        /// associated with this session.
        /// </summary>
        /// <param name="msg">The message.</param>
        /// <param name="sessionInfo">The session information associated with the handler.</param>
        /// <remarks>
        /// <note>
        /// The first message sent to server side session will result in a
        /// call to <see cref="StartServer" /> rather than a call to this method.
        /// </note>
        /// </remarks>
        public override void OnMsg(Msg msg, SessionHandlerInfo sessionInfo)
        {
            var transferMsg = msg as ReliableTransferMsg;

            if (transferMsg == null)
                return;     // This shouldn't ever happen but we'll check
                            // just to be sure.

            try
            {
#if TRACE
                Trace(0, string.Format("Receive: [{0}] [FromEP={1}]", transferMsg.Command, transferMsg._FromEP), transferMsg.GetTrace());
#endif
                using (TimedLock.Lock(syncLock))
                {
                    if (stateProcessor == null)
                    {
                        Trace(0, string.Format("Discarding: [{0}]", transferMsg.Command));
                        return;
                    }

                    stateProcessor.OnMessage(transferMsg);
                }
            }
            catch (Exception e)
            {
                NetTrace.Write(TraceSubsystem, 0, string.Format("[state={0}] Exception", state), e);
                SysLog.LogException(e);
            }
        }

        /// <summary>
        /// Called periodically on a worker thread providing a mechanism
        /// for the sessions to perform any background work.
        /// </summary>
        public override void OnBkTimer()
        {
            using (TimedLock.Lock(syncLock))
            {
                if (stateProcessor != null)
                    stateProcessor.BkTask();
            }
        }

        /// <summary>
        /// Sends a message to an endpoint, setting the message's <see cref="Msg._FromEP" />
        /// to the router's physical endpoint.  This method also participates in the
        /// network failure simulation.
        /// </summary>
        /// <param name="toEP">The target endpoint.</param>
        /// <param name="msg">The message.</param>
        private void SendTo(MsgEP toEP, ReliableTransferMsg msg)
        {
            switch (networkMode)
            {
                case NetFailMode.Disconnected :

                    Trace(0,string.Format("Send: [{0}] simulated net failure",msg.Command));
                    return;

                case NetFailMode.Intermittent :

                    if (Helper.Rand()%3 == 0)
                    {
                        Trace(0,string.Format("Send: [{0}] simulated intermittent net failure",msg.Command));
                        return;
                    }

                    break;

                case NetFailMode.Duplicate :

                    ReliableTransferMsg     clone;

                    Trace(0,string.Format("Send: [{0}] simulated duplicate packet",msg.Command));

                    clone = (ReliableTransferMsg) msg.Clone();
                    if (base.IsClient)
                        clone._Flags |= MsgFlag.ServerSession;

                    clone._SessionID = SessionID;
                    router.SendTo(toEP,localEP,clone);
                    break;

                case NetFailMode.Delay :

                    Thread.Sleep(100);
                    break;
            }

            if (base.IsClient)
                msg._Flags |= MsgFlag.ServerSession;

            msg._SessionID = SessionID;
            router.SendTo(toEP, localEP, msg);
#if TRACE
            Trace(0, string.Format("Send: [{0}]", msg.Command), msg.GetTrace());
#endif
        }

        /// <summary>
        /// Sends an error message to the remote side of the session.
        /// </summary>
        /// <param name="message">The message text.</param>
        private void SendError(string message)
        {
            ReliableTransferMsg msg;

            if (remoteEP == null)
                return;

            msg = new ReliableTransferMsg(ReliableTransferMsg.ErrorCmd);
            msg.Exception = message;
            SendTo(remoteEP, msg);
        }

        /// <summary>
        /// Handles a successful transfer completion notification.
        /// </summary>
        private void Notify()
        {
            Notify(null);
        }

        /// <summary>
        /// Handles a transfer completion notification.
        /// </summary>
        /// <param name="e">The exception if there was an error, null for success.</param>
        private void Notify(Exception e)
        {
            if (e != null)
            {
                Trace(0, string.Format("Notify({0})", e.Message));
                errorMessage = e.Message;
            }
            else
            {
                Trace(0, "Notify(ok)");
                errorMessage = null;
            }

            if (base.IsClient)
            {
                base.IsRunning = false;
                arTransfer.Notify(e);
            }
            else
            {
                var args = GetTransferArgs(ReliableTransferEvent.EndTransfer);

                args.Exception = e;

                handler.RaiseEndTransfer(args);

                base.IsRunning = false;
            }
        }

        //---------------------------------------------------------------------
        // Client side implementation

        /// <summary>
        /// Initializes a client side session.
        /// </summary>
        /// <param name="router">The associated message router.</param>
        /// <param name="sessionMgr">The associated session manager.</param>
        /// <param name="ttl">Session time-to-live.</param>
        /// <param name="sessionID">The session ID to assign to this session</param>
        public override void InitClient(MsgRouter router, ISessionManager sessionMgr, TimeSpan ttl, Guid sessionID)
        {
            base.InitClient(router, sessionMgr, ttl, sessionID);

            this.router   = router;
            this.syncLock = router.SyncRoot;
        }

        /// <summary>
        /// Initiates an asynchronous reliable transfer from the client side of a session.
        /// </summary>
        /// <param name="serverEP">The server side session endpoint.</param>
        /// <param name="direction">A <see cref="TransferDirection" /> value specifying the transfer direction.</param>
        /// <param name="transferID">
        /// The globally unique ID for this transfer.  Pass <see cref="Guid.Empty" /> if this method
        /// should generate this ID.
        /// </param>
        /// <param name="blockSize">The transfer block size (or 0 for a reasonable default).</param>
        /// <param name="args">
        /// A string containing application specific arguments (or <c>null</c>) to be transmitted 
        /// with the <see cref="ReliableTransferMsg" /> session initiation message.
        /// </param>
        /// <returns>The <see cref="IAsyncResult" /> to be used to track the operation.</returns>
        /// <remarks>
        /// <para>
        /// Note that <see cref="SessionHandler" /> must be set before calling this method and also that
        /// all calls to <see cref="BeginTransfer" /> must eventually matched with a call to
        /// <see cref="EndTransfer" />.
        /// </para>
        /// <para>
        /// The <paramref name="blockSize" /> parameter indicates the desired transfer block size
        /// or may be passed as zero if a reasonable default is to be selected.  Note that there
        /// is an underlying limit to the size of a transfer block.  This method will adjust the
        /// requested value so that this limit is not exceeded.  Note that the server ultimately
        /// chooses the blocksize, so consider this parameter to be more of a recommendation.
        /// </para>
        /// <param name="callback">The delegate to be called when the operation completes (or <c>null</c>).</param>
        /// <param name="state">Application state (or <c>null</c>).</param>
        /// </remarks>
        /// <exception cref="InvalidOperationException">Thown for server side sessions or if <see cref="SessionHandler" /> has not been set.</exception>
        public IAsyncResult BeginTransfer(MsgEP serverEP,
                                          TransferDirection direction,
                                          Guid transferID,
                                          int blockSize,
                                          string args,
                                          AsyncCallback callback,
                                          object state)
        {
            if (this.router == null)
                throw new InvalidOperationException("Session is not associated with a router. Verify that the session is constructed via MsgRouter.CreateSession().");

            if (this.SessionHandler == null)
                throw new InvalidOperationException("[SessionHandler] property must be initialized before starting a reliable transfer.");

            if (transferID.Equals(Guid.Empty))
                transferID = Helper.NewGuid();

            this.transferID = transferID;
            this.serverEP   = serverEP.Clone(true);
            this.localEP    = router.RouterEP.Clone(true);
            this.remoteEP   = null;         // We'll get this with the first response from the server
            this.direction  = direction;
            this.args       = args;

            if (blockSize <= 0)
                blockSize = settings.DefBlockSize;
            else if (blockSize > settings.MaxBlockSize)
                blockSize = settings.MaxBlockSize;

            this.blockSize = blockSize;

            if (!base.IsClient)
                throw new InvalidOperationException("Not a client session.");

            if (handler == null)
                throw new InvalidOperationException("[SessionHandler] property has not been set.");

            if (args == null)
                args = string.Empty;

            arTransfer = new AsyncResult(null, callback, state);
            arTransfer.InternalState = this;
            arTransfer.Started();

            base.IsRunning = true;
            base.TTD = DateTime.MaxValue;

            base.SessionManager.ClientStart(this);
            SetState(State.ClientStart);

            return arTransfer;
        }

        /// <summary>
        /// Completes an asynchronous reliable transfer.
        /// </summary>
        /// <param name="ar">The <see cref="IAsyncResult" /> instance returned by the matching 
        /// call to <see cref="BeginTransfer"/>.
        /// </param>
        public void EndTransfer(IAsyncResult ar)
        {
            var arSession = (AsyncResult)ar;

            Assertion.Test(object.ReferenceEquals(arSession.InternalState, this));
            arSession.Wait();

            try
            {
                if (arSession.Exception != null)
                    throw arSession.Exception;
            }
            finally
            {
                arSession.Dispose();
                base.IsRunning = false;
            }
        }

        /// <summary>
        /// Performs a synchronous reliable transfer from the client side of a session.
        /// </summary>
        /// <param name="serverEP">The server side session endpoint.</param>
        /// <param name="direction">A <see cref="TransferDirection" /> value specifying the transfer direction.</param>
        /// <param name="blockSize">The transfer block size (or 0 for a reasonable default).</param>
        /// <param name="transferID">
        /// The globally unique ID for this transfer.  Pass <see cref="Guid.Empty" /> if this method
        /// should generate this ID.
        /// </param>
        /// <param name="args">
        /// A string containing application specific arguments (or <c>null</c>) to be transmitted 
        /// with the <see cref="ReliableTransferMsg" /> session initiation message.
        /// </param>
        /// <remarks>
        /// <para>
        /// Note that <see cref="SessionHandler" /> must be set before calling this method .
        /// </para>
        /// <para>
        /// The <paramref name="blockSize" /> parameter indicates the desired transfer block size
        /// or may be passed as zero if a reasonable default is to be selected.  Note that there
        /// is an underlying limit to the size of a transfer block.  This method will adjust the
        /// requested value so that this limit is not exceeded.  Note that the server ultimately
        /// chooses the blocksize, so consider this parameter to be more of a recommendation.
        /// </para>
        /// </remarks>
        /// <exception cref="InvalidOperationException">Thown for server side sessions or if <see cref="SessionHandler" /> has not been set.</exception>
        public void Transfer(MsgEP serverEP,
                             TransferDirection direction,
                             int blockSize,
                             Guid transferID,
                             string args)
        {
            var ar = BeginTransfer(serverEP, direction, transferID, blockSize, args, null, null);

            EndTransfer(ar);
        }

        //---------------------------------------------------------------------
        // Server side implementation

        /// <summary>
        /// Initializes a server side session.
        /// </summary>
        /// <param name="router">The associated message router.</param>
        /// <param name="sessionMgr">The associated session manager.</param>
        /// <param name="ttl">Session time-to-live.</param>
        /// <param name="msg">The message that triggered this session.</param>
        /// <param name="target">The dispatch target instance.</param>
        /// <param name="method">The dispatch method.</param>
        /// <param name="sessionInfo">
        /// The session information associated with the handler or <c>null</c>
        /// to use session defaults.
        /// </param>
        public override void InitServer(MsgRouter router, ISessionManager sessionMgr, TimeSpan ttl, Msg msg, object target,
                                        MethodInfo method, SessionHandlerInfo sessionInfo)
        {
            ReliableTransferMsg transferMsg = (ReliableTransferMsg)msg;

            base.InitServer(router, sessionMgr, ttl, msg, target, method, sessionInfo);
            base.IsAsync    = true;
            this.IsRunning  = true;

            this.router     = router;
            this.syncLock   = router.SyncRoot;
            this.direction  = transferMsg.Direction;
            this.transferID = transferMsg.TransferID;
            this.blockSize  = transferMsg.BlockSize;
            this.args       = transferMsg.Args;
        }

        /// <summary>
        /// Starts the server session initialized with InitServer().
        /// </summary>
        public override void StartServer()
        {
            bool                exceptionThrown = false;
            DateTime            now = SysTime.Now;
            ReliableTransferMsg startMsg;
            ReliableTransferMsg startAck;

            try
            {
                startMsg = base.ServerInitMsg as ReliableTransferMsg;
                if (startMsg == null)
                    throw new InvalidOperationException(string.Format("[ReliableTransferSession] message handler [{0}.{1}()] cannot accept a [{0}] message type.",
                                                                      base.Target.GetType().FullName, base.Method.Name, base.ServerInitMsg.GetType().FullName));
                base.IsRunning = true;
                base.TTD       = DateTime.MaxValue;
                this.localEP    = base.Router.RouterEP.Clone(true);
                this.remoteEP = base.ServerInitMsg._FromEP.Clone(true);

                this.inMsgHandler = true;
                try
                {
                    base.Method.Invoke(base.Target, new object[] { base.ServerInitMsg });
                }
                finally
                {
                    this.inMsgHandler = false;
                }

                if (handler == null)
                    throw new InvalidOperationException(string.Format("[ReliableTransferSession] message handler [{0}.{1}()] did not initialize [SessionHandler].",
                                                                      base.Target.GetType().FullName, base.Method.Name));

                // Load any custom settings specified in the [MsgSession] attribute.

                settings.LoadCustom(base.SessionInfo.Parameters);

                // Send the start-ack reply

                startAck = new ReliableTransferMsg(ReliableTransferMsg.StartAck);
                this.blockSize =
                startAck.BlockSize = startMsg.BlockSize;

                SendTo(remoteEP, startAck);
                SetState(GetTransferState());
            }
            catch (TargetInvocationException eInvoke)
            {
                exceptionThrown = true;
                SysLog.LogException(eInvoke.InnerException);

                startAck = new ReliableTransferMsg(ReliableTransferMsg.ErrorCmd);
                startAck.Exception = eInvoke.InnerException.Message;
                SendTo(remoteEP, startAck);
            }
            catch (Exception e)
            {
                exceptionThrown = true;
                SysLog.LogException(e);

                startAck = new ReliableTransferMsg(ReliableTransferMsg.ErrorCmd);
                startAck.Exception = e.Message;
                SendTo(remoteEP, startAck);
            }
            finally
            {
                if (exceptionThrown)
                    base.IsRunning = false;
                else
                {
                    if (base.SessionInfo.MaxAsyncKeepAliveTime == TimeSpan.MaxValue)
                        base.TTD = DateTime.MaxValue;
                    else
                        base.TTD = SysTime.Now + SessionInfo.MaxAsyncKeepAliveTime;
                }
            }
        }
    }
}
