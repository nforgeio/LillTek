//-----------------------------------------------------------------------------
// FILE:        SwitchConnection.cs
// CONTRIBUTOR: Jeff Lill
// COPYRIGHT:   Copyright (c) 2005-2015 by Jeffrey Lill. All rights reserved.
// DESCRIPTION: Provides for connecting to a NeonSwitch or FreeSWITCH server via a 
//              TCP socket to subscribe to the events raised by the server and also 
//              to submit commands and events to the switch.

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Security;
using System.ServiceModel;
using System.Text;
using System.Threading;

using LillTek.Common;
using LillTek.Net.Sockets;

// $todo(jeff.lill):
//
// At some point I should introduce the concept of command timeouts.  Right now,
// the connection will wait forever.

namespace LillTek.Telephony.Common
{
    /// <summary>
    /// Provides for connecting to a NeonSwitch or FreeSWITCH server via a TCP socket to
    /// subscribe to the events raised by the server and also to submit commands and events 
    /// to the switch.  This class is threadsafe.
    /// </summary>
    /// <remarks>
    /// <note>
    /// This class relies on the <b>mod_event_socket</b> module being enabled on the switch.
    /// </note>
    /// <para>
    /// This class is pretty easy to use.  Simply construct an instance with the default
    /// network binding and password or specifies these parameters the constructor.  Then
    /// hook up event handlers to the <see cref="Connected" />, <see cref="Disconnected" />,
    /// <see cref="EventReceived" />, <see cref="JobCompleted" />, <see cref="LogEntryReceived" />,
    /// or <see cref="CommandReceived" /> events as required by your application.
    /// </para>
    /// <para>
    /// Then call the <see cref="Connect" /> or the asynchronous <see cref="BeginConnect" />
    /// method to establish a TCP socket connection to the NeonSwitch node.  You can also call
    /// the static <see cref="StartListener"/> to listen for inbound switch connections and
    /// <see cref="StopListener" /> stop listening.  The <see cref="InboundConnection" />
    /// event will be raise when an inbound connection is established.
    /// </para>
    /// <note>
    /// The listening functionality is is intended for internal unit testing as well as 
    /// implemening support for outbound NeonSwitch socket connections made from the
    /// dial plan.
    /// </note>
    /// <para>
    /// Once you're connected, you can handle events raised by the event handler and/or submit
    /// operations to the switch.  Use the various overrides of the <see cref="Execute(string) "/>
    /// to submit a command to the switch and wait for a reply or the <see cref="ExecuteBackground(string) "/>
    /// overrides to submit a background job.
    /// </para>
    /// <note>
    /// All events are raised on worker threads.
    /// </note>
    /// <para>
    /// The <see cref="Subscribe" /> and <see cref="Unsubscribe" /> methods are used tell NeonSwitch
    /// which events the application is interested in seeing.  You'll pass a <see cref="SwitchEventCodeSet" />
    /// to <see cref="Subscribe "/> to enable events based on their <see cref="SwitchEventCode" />.
    /// You can call <see cref="Subscribe" /> more than once to add additional event subscriptions.
    /// <see cref="Unsubscribe" /> clears all event subscriptions.
    /// </para>
    /// <para>
    /// Use <see cref="SetLogLevel" /> to indicate the level of detail desired for log related messages
    /// sent by the switch and <see cref="SendEvent(SwitchEventCode,ArgCollection)" /> to submit
    /// an event for processing by NeonSwitch and any installed modules.
    /// </para>
    /// <note>
    /// Note that the connection starts out with no event subscriptions so the connection events
    /// won't be raised until you subscribe to the underlying event.
    /// </note>
    /// <note>
    /// Most synchronous switch connection methods also have asynchronous versions that follow the
    /// <b>BeginXXX()</b>...<b>EndXXX()</b> pattern.
    /// </note>
    /// <para>
    /// Call <see cref="Close()" /> when you no longer need the connection.
    /// </para>
    /// </remarks>
    /// <threadsafety instance="true" />
    public class SwitchConnection : IDisposable
    {
        //---------------------------------------------------------------------
        // Private types

        /// <summary>
        /// Used to track asynchronous command operations submitted to the switch.
        /// </summary>
        private class AsyncCommandResult : AsyncResult<CommandDisposition, object>
        {
            /// <summary>
            /// Constructor.
            /// </summary>
            /// <param name="owner">The object that "owns" this operation (or <c>null</c>).</param>
            /// <param name="callback">The delegate to call when the operation completes.</param>
            /// <param name="state">The application defined state.</param>
            public AsyncCommandResult(object owner, AsyncCallback callback, object state)
                : base(owner, callback, state)
            {
            }
        }

        /// <summary>
        /// Used to track commands queued for submission to the switch.
        /// </summary>
        private class Command
        {
            /// <summary>
            /// The command packet.
            /// </summary>
            public SwitchPacket Packet;

            /// <summary>
            /// Used to track the operation.
            /// </summary>
            public AsyncCommandResult AsyncResult;

            /// <summary>
            /// Indications whether the connection is to wait for a full response from the switch.
            /// </summary>
            public bool ResponseExpected;

            /// <summary>
            /// Constructs a text only command.
            /// </summary>
            /// <param name="commandText">The command text.</param>
            /// <param name="ar">The <see cref="IAsyncResult" /> being used to track the operation.</param>
            /// <param name="responseExpected">Indications whether the connection is to wait for a full response from the switch.</param>
            public Command(string commandText, AsyncCommandResult ar, bool responseExpected)
            {
                this.Packet           = new SwitchPacket(commandText, null, null, null);
                this.AsyncResult      = ar;
                this.ResponseExpected = responseExpected;
            }

            /// <summary>
            /// Constructs a command from a switch packet.
            /// </summary>
            /// <param name="packet">The command packet.</param>
            /// <param name="ar">The <see cref="IAsyncResult" /> being used to track the operation.</param>
            /// <param name="responseExpected">Indications whether the connection is to wait for a full response from the switch.</param>
            public Command(SwitchPacket packet, AsyncCommandResult ar, bool responseExpected)
            {

                this.Packet          = packet;
                this.AsyncResult      = ar;
                this.ResponseExpected = responseExpected;
            }
        }

        //---------------------------------------------------------------------
        // Static members

        private static object           staticSyncRoot = new object();
        private static SocketListener   listener;

        /// <summary>
        /// The socket transmission buffer size in bytes.
        /// </summary>
        /// <remarks>
        /// This global property controls the size of the send buffer for the underlying
        /// TCP sockets created for both inbound and outbound NeonSwitch connections.
        /// This defaults to 32K.
        /// </remarks>
        public static int SendBufferSize { get; set; }

        /// <summary>
        /// The socket receive buffer size in bytes.
        /// </summary>
        /// <remarks>
        /// This global property controls the size of the receive buffer for the underlying
        /// TCP sockets created for both inbound and outbound NeonSwitch connections.
        /// This defaults to 32K.
        /// </remarks>
        public static int ReceiveBufferSize { get; set; }

        /// <summary>
        /// Raised when an inbound socket connection is established with the listener.
        /// </summary>
        public static EventHandler<SwitchInboundConnectionArgs> InboundConnection;

        /// <summary>
        /// Static constructor.
        /// </summary>
        static SwitchConnection()
        {
            ResetGlobals();
        }

        /// <summary>
        /// Resets the global properties such as <see cref="SendBufferSize" /> and
        /// <see cref="ReceiveBufferSize" /> to their default values.  This is intended
        /// mostly for use by unit tests.
        /// </summary>
        public static void ResetGlobals()
        {
            SendBufferSize    =
            ReceiveBufferSize = 32 * 1024;
        }

        /// <summary>
        /// Starts the global switch connection listener.
        /// </summary>
        /// <param name="binding">The network binding for the NIC and port to listen on.</param>
        /// <param name="acceptBacklog">The maximum number of inbound sockets that will be queued pending acceptance.</param>
        /// <exception cref="InvalidOperationException">Thrown if the listener has already started.</exception>
        /// <exception cref="SocketException">Thrown if the listener could not be started.</exception>
        public static void StartListener(NetworkBinding binding, int acceptBacklog)
        {
            lock (staticSyncRoot)
            {
                if (listener != null)
                    throw new InvalidOperationException("The global switch listener has already been started.");

                listener = new SocketListener();
                listener.SocketAcceptEvent +=
                    (socket, endPoint) =>
                    {
                        socket.SendBufferSize = SendBufferSize;
                        socket.ReceiveBufferSize = ReceiveBufferSize;

                        if (InboundConnection != null)
                        {
                            var connection = new SwitchConnection(socket);
                            var args = new SwitchInboundConnectionArgs(connection);

                            InboundConnection(null, args);

                            if (args.StartConnectionThread)
                                connection.StartThread();
                        }
                    };

                try
                {
                    listener.Start(binding, acceptBacklog);
                }
                catch
                {
                    listener = null;
                    throw;
                }
            }
        }

        /// <summary>
        /// Stops the switch listener if it is running.
        /// </summary>
        public static void StopListener()
        {
            lock (staticSyncRoot)
            {
                if (listener != null)
                {
                    listener.Dispose();
                    listener = null;
                }
            }
        }

        //---------------------------------------------------------------------
        // Instance members

        /// <summary>
        /// The default event socket port number.
        /// </summary>
        public const int DefaultPort = 8021;

        /// <summary>
        /// The default event socket password.
        /// </summary>
        public const string DefaultPassword = "ClueCon";

        private object              syncLock           = new object();
        private MemoryStream        recvBuf            = new MemoryStream();
        private byte[]              packetMarker       = new byte[] { Helper.LF, Helper.LF };
        private SwitchEventCodeSet  eventSubscriptions = new SwitchEventCodeSet();
        private NetworkBinding      binding;
        private string              password;
        private EnhancedSocket      socket;
        private bool                isConnected;
        private bool                isInbound;
        private Thread              thread;
        private AsyncResult         arConnect;
        private bool                closePending;

        // This queue tracks the commands that have been submitted to the switch connection
        // for execution by the switch.  Commands are queued internally and are submitted
        // one at a time to the switch.  When a command completes, the next command from
        // the queue will be submitted.  [pendingCommand] will be set internally by the send
        // thread to the the command currently being processed by the switch.

        private Queue<Command>  commandQueue;
        private Command         pendingCommand;

        /// <summary>
        /// Constructs an instance to connect to a switch running on the local machine with the
        /// default NeonSwitch port and password.
        /// </summary>
        public SwitchConnection()
            : this(new NetworkBinding("localhost", DefaultPort), DefaultPassword)
        {
            this.LogLevel = SwitchLogLevel.None;
        }

        /// <summary>
        /// Constructs an instance to connect to the switch using a specific network binding 
        /// and password.
        /// </summary>
        /// <param name="binding">The network binding.</param>
        /// <param name="password">The password.</param>
        public SwitchConnection(NetworkBinding binding, string password)
        {
            if (binding.Port == 0)
                this.binding = new NetworkBinding(binding.Host, DefaultPort);
            else
                this.binding = binding;

            this.password     = password;
            this.isConnected  = false;
            this.LogLevel     = SwitchLogLevel.None;
            this.isInbound    = false;
            this.closePending = false;
        }

        /// <summary>
        /// Constructs a connection for an inbound socket.
        /// </summary>
        /// <param name="socket">The inbound socket.</param>
        private SwitchConnection(EnhancedSocket socket)
        {
            this.socket      = socket;
            this.isConnected  = true;
            this.LogLevel     = SwitchLogLevel.None;
            this.isInbound    = true;
            this.closePending = false;
        }

        /// <summary>
        /// Raised when a connection with the switch has been established.
        /// </summary>
        public event EventHandler<EventArgs> Connected;

        /// <summary>
        /// Raised on a background thread when the connector loses its connection with the switch.
        /// </summary>
        /// <remarks>
        /// </remarks>
        public event EventHandler<SwitchDisconnectArgs> Disconnected;

        /// <summary>
        /// <para>
        /// Raised on a background thread when an event is received from the switch.
        /// </para>
        /// <note>
        /// You must subscribe to one or more events before this will be raised.
        /// </note>
        /// </summary>
        public event EventHandler<SwitchEventReceivedArgs> EventReceived;

        /// <summary>
        /// <para>
        /// Raised on a background thread when the asynchronous execution of command submitted to the switch
        /// via <see cref="ExecuteBackground" /> or by other switch components has completed.
        /// </para>
        /// <note>
        /// You must subscribe to <see cref="SwitchEventCode.BackgroundJob" /> before this
        /// event will be raised.
        /// </note>
        /// </summary>
        /// <remarks>
        /// <note>
        /// This event is raised when any background job has been completed by the switch, not just
        /// for jobs submitted by this connection.
        /// </note>
        /// </remarks>
        public event EventHandler<SwitchJobCompletedArgs> JobCompleted;

        /// <summary>
        /// <para>
        /// Raised when a log entry has been received from the switch.
        /// </para>
        /// <note>
        /// You must subscribe to <see cref="SwitchEventCode.Log" /> before this event
        /// will be raised.
        /// </note>
        /// </summary>
        public event EventHandler<SwitchLogEntryReceivedArgs> LogEntryReceived;

        /// <summary>
        /// Raised when a command has been received from the other side of the connection.
        /// </summary>
        /// <remarks>
        /// <para>
        /// This event is raised synchronously and NeonSwitch will assume that the
        /// command completed successfully unless the event argument's
        /// <see cref="SwitchCommandReceivedArgs" />.<see cref="SwitchCommandReceivedArgs.Success" />
        /// property is set to <c>false</c> or an exception is raised.  The
        /// handler can customize the reply by setting 
        /// <see cref="SwitchCommandReceivedArgs" />.<see cref="SwitchCommandReceivedArgs.ReplyText" />
        /// to a custom message.
        /// </para>
        /// <note>
        /// If an exception is raised by the handler, the exception message will be
        /// used as the reply text.
        /// </note>
        /// <note>
        /// If there is no handler set, NeonSwitch will still reply to the command with a 
        /// success message.
        /// </note>
        /// </remarks>
        public event EventHandler<SwitchCommandReceivedArgs> CommandReceived;

        /// <summary>
        /// Synchronously establishes and authenticates a connection to the switch.
        /// </summary>
        /// <exception cref="SocketException">Throw if the connection attempt failed.</exception>
        /// <exception cref="SecurityException">Thrown if authentication failed.</exception>
        /// <exception cref="ProtocolException">Thrown if we received something unexpected from the switch.</exception>
        /// <exception cref="InvalidOperationException">Thrown if <see cref="Close()" /> was called during the connection handshake process.</exception>
        /// <exception cref="InvalidOperationException">Thrown if the socket is already open or is an inbound connection.</exception>
        public void Connect()
        {
            var ar = BeginConnect(null, null);

            EndConnect(ar);
        }

        /// <summary>
        /// Initiates an asynchronous operation to establish a connection with the remote switch
        /// using the network binding and credentials passed to the constructor.
        /// </summary>
        /// <param name="callback">The delegate to be called when the operation completes (or <c>null</c>).</param>
        /// <param name="state">Application specific state.</param>
        /// <returns>The <see cref="IAsyncResult" /> instance to be used to track the operation's progress.</returns>
        /// <exception cref="InvalidOperationException">
        /// Thrown if a connection is already established or is in the progress of
        /// being established.
        /// </exception>
        /// <remarks>
        /// <note>
        /// All successful calls to <see cref="BeginConnect" /> must eventually be
        /// matched with a call to <see cref="EndConnect" />.
        /// </note>
        /// </remarks>
        public IAsyncResult BeginConnect(AsyncCallback callback, object state)
        {
            lock (syncLock)
            {
                if (thread != null)
                    throw new InvalidOperationException("SwitchConnection connection attempt is already in progress.");

                if (isInbound)
                    throw new InvalidOperationException("Cannot connect using an inbound connection.");

                if (isConnected)
                    throw new InvalidOperationException("SwitchConnection is already connected.");

                // Start the send thread which will be responsible for connection handshake
                // and then starting the receive thread.

                arConnect      = new AsyncResult(null, callback, state);
                commandQueue   = new Queue<Command>();
                pendingCommand = null;

                StartThread();
                arConnect.Started();
                return arConnect;
            }
        }

        /// <summary>
        /// Completes the asynchronous operation initiated by a previous call to <see cref="BeginConnect" />.
        /// </summary>
        /// <param name="ar">The <see cref="IAsyncResult" /> instance returned by <see cref="BeginConnect" />.</param>
        public void EndConnect(IAsyncResult ar)
        {
            Assertion.Test(object.ReferenceEquals(ar, arConnect));
            arConnect.Wait();

            lock (syncLock)
            {
                try
                {
                    if (arConnect.Exception != null)
                    {
                        Close();
                        throw arConnect.Exception;
                    }
                    else
                        RaiseConnected();
                }
                finally
                {
                    arConnect = null;
                }
            }
        }

        /// <summary>
        /// Starts the connection's thread.
        /// </summary>
        internal void StartThread()
        {
            thread      = new Thread(new ThreadStart(ThreadLoop));
            thread.Name = "SwitchConnection";
            thread.Start();
        }

        /// <summary>
        /// Implements background thread processing for the connection.
        /// </summary>
        private void ThreadLoop()
        {
            SwitchPacket packet;

            // Perform the client side switch connection handshake.

            if (!isInbound)
            {
                try
                {
                    // Establish the connection issue and authentication command and then a status command 
                    // to verify that password was authenticated.

                    socket                   = new EnhancedSocket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);
                    socket.SendBufferSize    = SendBufferSize;
                    socket.ReceiveBufferSize = ReceiveBufferSize;

                    socket.Connect(binding);

                    Send("auth {0}\n\n", password);
                    packet = ReceivePacket();   // Command accepted reply

                    Send("api status\n\n");
                    packet = ReceivePacket();   // Command accepted reply

                    if (packet.PacketType != SwitchPacketType.ExecuteAck)
                        throw new ProtocolException("Expected a execute acknowledgement from the switch.");

                    if (!packet.ExecuteAccepted)
                    {
                        // Going to assume that a failure here is due to an authentication failure.

                        throw new SecurityException("Access denied.");
                    }

                    packet = ReceivePacket();   // Get the status command response.

                    // We're connected

                    isConnected = true;
                }
                catch (Exception e)
                {
                    arConnect.Notify(e);
                    Close(e);
                    return;
                }
            }

            lock (syncLock)
            {
                if (!isConnected)
                {
                    // Exit on the off-chance that Close() has been called.

                    if (arConnect != null)
                        arConnect.Notify(new InvalidOperationException("SwitchConnection.Close() was called during the connection handshake process."));

                    return;
                }

                // Finish getting the thread ready.

                isConnected = true;

                if (arConnect != null)
                    arConnect.Notify();
            }

            // Loop to receive packets from the switch.

            while (true)
            {
                try
                {
                    packet = ReceivePacket();

                    lock (syncLock)
                    {
                        switch (packet.PacketType)
                        {
                            case SwitchPacketType.Command:

                                RaiseCommandReceived(new SwitchCommandReceivedArgs(packet));
                                break;

                            case SwitchPacketType.Event:

                                // Raise ExecuteCompleted when background jobs complete.

                                if (packet.EventCode == SwitchEventCode.BackgroundJob)
                                    RaiseJobCompleted(new SwitchJobCompletedArgs(packet));

                                // Raise the general event received handler.

                                RaiseEventReceived(new SwitchEventReceivedArgs(packet));
                                break;

                            case SwitchPacketType.ExecuteAck:

                                if (pendingCommand == null)
                                    continue;   // There is no pending command so we're just going to 
                                                // ignore the reply.

                                if (!pendingCommand.ResponseExpected)
                                {
                                    // The command is not waiting for a full response so we'll signal
                                    // command completion and submit the next command to the switch
                                    // (if there is one).

                                    pendingCommand.AsyncResult.Result = new CommandDisposition(packet);
                                    pendingCommand.AsyncResult.Notify();
                                    pendingCommand = null;

                                    if (commandQueue.Count > 0)
                                    {

                                        pendingCommand = commandQueue.Dequeue();
                                        SendPacket(pendingCommand.Packet);
                                    }
                                }

                                break;

                            case SwitchPacketType.ExecuteResponse:

                                if (pendingCommand == null)
                                    continue;   // There is no pending command so we're just going to 
                                                // ignore the reply.

                                // [pendingCommand.ResponseExpected=true] indicates that the current execute
                                // expects a response (which we now have).
                                //
                                // We shouldn't ever see [pendingCommand.ResponseExpected=false] here but 
                                // we'll check and ignore just to be safe.

                                // Signal command completion.

                                if (pendingCommand.ResponseExpected)
                                {
                                    pendingCommand.AsyncResult.Result = new CommandDisposition(packet);
                                    pendingCommand.AsyncResult.Notify();
                                }

                                pendingCommand = null;

                                // Submit the next command to the switch (if there is one).

                                if (commandQueue.Count > 0)
                                {
                                    pendingCommand = commandQueue.Dequeue();
                                    SendPacket(pendingCommand.Packet);
                                }

                                break;

                            case SwitchPacketType.Log:

                                RaiseLogEntryReceived(packet);
                                break;

                            case SwitchPacketType.Unknown:

                                // Ignore unexpected packet types.

                                SysLog.LogWarning("Unexpected switch packet type [{0}].", packet.ContentType);
                                continue;
                        }
                    }
                }
                catch (Exception e)
                {
                    // Close the connection on all errors.

                    Close(e);
                    break;
                }
            }
        }

        /// <summary>
        /// Verifies that a connection with the switch has been established.
        /// </summary>
        /// <exception cref="InvalidOperationException">Thrown if a connection to the switch has not been established.</exception>
        private void VerifyConnected()
        {
            if (!isConnected)
                throw new InvalidOperationException("Switch connection has not been established.");
        }

        /// <summary>
        /// Synchronously transmits text to the switch.
        /// </summary>
        /// <param name="text">The text.</param>
        internal void Send(string text)
        {
            socket.SendAll(Helper.ASCIIEncoding.GetBytes(text));
        }

        /// <summary>
        /// Synchronously transmits data to the switch.
        /// </summary>
        /// <param name="data">The data.</param>
        internal void Send(byte[] data)
        {
            socket.SendAll(data);
        }

        /// <summary>
        /// Synchronously transmits formatted text to the switch.
        /// </summary>
        /// <param name="format">The format string.</param>
        /// <param name="args">The arguments.</param>
        internal void Send(string format, params object[] args)
        {
            Send(string.Format(format, args));
        }

        /// <summary>
        /// Sends <b>command/reply</b>.
        /// </summary>
        /// <param name="replyText">The reply text or <c>null</c> for "+OK Success".</param>
        /// <param name="args">Additional name/values to be added to the reply's properties.</param>
        internal void SendReply(string replyText, params NameValue[] args)
        {
            var properties = new ArgCollection(ArgCollectionType.Unconstrained);

            properties["Content-Type"] = "command/reply";
            properties["Reply-Text"]   = replyText ?? "+OK Success";

            foreach (var pair in args)
                properties[pair.Name] = pair.Value;

            SendPacket(new SwitchPacket(properties, null));
        }

        /// <summary>
        /// Sends a <b>command/api</b> response.
        /// </summary>
        /// <param name="responseText">The response text.</param>
        /// /// <param name="args">Additional name/values to be added to the response's properties.</param>
        internal void SendResponse(string responseText, params NameValue[] args)
        {
            var properties = new ArgCollection(ArgCollectionType.Unconstrained);
            var content    = Helper.ASCIIEncoding.GetBytes(responseText ?? string.Empty);

            properties["Content-Type"] = "api/response";
            properties["Content-Length"] = content.Length.ToString();

            foreach (var pair in args)
                properties[pair.Name] = pair.Value;

            SendPacket(new SwitchPacket(properties, content));
        }

        /// <summary>
        /// Synchronously transmits a command packet to the switch.
        /// </summary>
        /// <param name="packet">The packet.</param>
        internal void SendPacket(SwitchPacket packet)
        {
            if (packet.Headers == null && packet.CommandText != null)
            {
                // Must be a simple command line.

                Send(packet.CommandText + "\n\n");
            }
            else
            {
                StringBuilder   sb = new StringBuilder();
                byte[]          data;

                if (packet.CommandText != null)
                    sb.Append(packet.CommandText + "\n");

                if (packet.Content != null && packet.Content.Length > 0)
                    packet.Headers.Set("Content-Length", packet.Content.Length);

                foreach (var key in packet.Headers)
                    sb.AppendFormat("{0}: {1}\n", key, Helper.UrlEncode(packet.Headers[key]));

                sb.Append('\n');

                data = Helper.ASCIIEncoding.GetBytes(sb.ToString());
                if (packet.Content != null)
                    data = Helper.Concat(data, packet.Content);

                socket.SendAll(data);
            }
        }

        /// <summary>
        /// Transmits or enqueues a command to the switch depending on whether
        /// there is already a command in the process of executing.
        /// </summary>
        /// <param name="command">The command.</param>
        private void SendOrEnqueueCommand(Command command)
        {
            if (pendingCommand == null)
            {
                pendingCommand = command;
                SendPacket(command.Packet);
            }
            else
                commandQueue.Enqueue(command);
        }

        /// <summary>
        /// Removes the specified number of bytes from the front of the
        /// receive buffer by shifting the remaining bytes left.
        /// </summary>
        /// <param name="cbRemove">The number of bytes to be removed.</param>
        private void ShiftRecvBuf(int cbRemove)
        {
            byte[]  recvBufferBytes = recvBuf.GetBuffer();
            int     cbRemain;

            cbRemain = (int)recvBuf.Length - cbRemove;
            Array.Copy(recvBufferBytes, cbRemove, recvBufferBytes, 0, cbRemain);
            recvBuf.SetLength(cbRemain);
            recvBuf.Seek(cbRemain, SeekOrigin.Begin);
        }

        /// <summary>
        /// Receives the specified number of bytes from the switch.
        /// </summary>
        /// <param name="count">The number of bytes.</param>
        /// <returns>The array of received bytes.</returns>
        private byte[] Receive(int count)
        {
            byte[]      buffer = new byte[count];
            int         offset = 0;
            int         cb;
            byte[]      recvBufferBytes;

            if (recvBuf.Length > 0)
            {
                // Process data buffered from a previous read.

                cb = Math.Min(count, (int)recvBuf.Length);

                recvBufferBytes = recvBuf.GetBuffer();
                Array.Copy(recvBufferBytes, buffer, cb);
                offset = cb;

                // Shift any remaining bytes in the receive buffer to the
                // beginning of the buffer.

                ShiftRecvBuf(cb);
            }

            // Continue receiving any remaining bytes.

            if (offset < buffer.Length)
                socket.ReceiveAll(buffer, offset, buffer.Length - offset);

            return buffer;
        }

        /// <summary>
        /// Receives and buffers ASCII characters from the switch until the matching
        /// sequence of bytes is encountered.  
        /// </summary>
        /// <param name="match">The matching bytes.</param>
        /// <returns>The received string <b>excluding</b> the matching bytes.</returns>
        private string ReceiveUntil(byte[] match)
        {
            byte[]      buffer = null;
            byte[]      recvBufferBytes;
            int         pos;
            int         cbRecv;
            string      result;

            while (true)
            {
                // Check to see if we already have a match in the receive buffer.

                if (recvBuf.Length > match.Length)
                {
                    recvBufferBytes = recvBuf.GetBuffer();
                    pos = Helper.IndexOf(recvBufferBytes, match);

                    if (pos != -1)
                    {
                        // We have a match.

                        result = Helper.ASCIIEncoding.GetString(recvBufferBytes, 0, pos);
                        ShiftRecvBuf(pos + match.Length);

                        return result;
                    }
                }

                // We don't have a match yet, so receive the next chunk of data from the 
                // the switch, append it to the receive buffer and then loop to see
                // if we a match now.

                if (buffer == null)
                    buffer = new byte[8192];

                cbRecv = socket.Receive(buffer, buffer.Length, SocketFlags.None);
                if (cbRecv == 0)
                    throw new SocketClosedException(SocketCloseReason.LocalClose);

                recvBuf.Write(buffer, 0, cbRecv);
            }
        }

        /// <summary>
        /// Receives the next chunk of data from the switch.
        /// </summary>
        /// <param name="buffer">The output buffer.</param>
        /// <param name="size">The maximum number of  bytes to receive.</param>
        /// <returns>The number of bytes actually received.</returns>
        private int Receive(byte[] buffer, int size)
        {
            int cbRecv;

            try
            {
                cbRecv = socket.Receive(buffer, size, SocketFlags.None);
                if (cbRecv == 0)
                    throw new SocketClosedException(SocketCloseReason.LocalClose);

                return cbRecv;
            }
            catch (Exception e)
            {
                Close(e);
                throw new Exception("Receive failure");
            }
        }

        /// <summary>
        /// Receives the next packet from the switch.
        /// </summary>
        /// <returns>The packet received.</returns>
        internal SwitchPacket ReceivePacket()
        {
            var         properties = new ArgCollection(ArgCollectionType.Unconstrained);
            string      command = null;
            string      headers;
            string[]    lines;
            string[]    fields;
            string      name;
            string      value;
            byte[]      content;
            string      contentType;
            int         cbContent;

            // Read lines of text up to the next LFLF marker and look at the first line to 
            // determine whether the packet is a command.  We distinguish commands from 
            // headers by:
            //
            // * Commands do not have a ":" directly after the command word on the first
            //   line of text.

            headers = ReceiveUntil(packetMarker);
            lines   = headers.Split('\n');

            if (lines.Length >= 1)
            {
                int pos;

                command = lines[0].Trim();
                pos     = command.IndexOf(':');

                if (pos != -1)
                {
                    // Count the number of whitespace separated "words" from the beginning
                    // of the line up to the colon.  If this is greater than one then we have
                    // a command.

                    int cWords = 0;
                    int i      = 0;

                    while (i < pos)
                    {
                        // Skip over the word

                        while (i < pos && command[i] != ' ' && command[i] != '\t')
                            i++;

                        cWords++;

                        // Skip over the whitespace

                        while (i < pos && (command[i] != ' ' || command[i] != '\t'))
                            i++;
                    }

                    if (cWords <= 1)
                        command = null;     // Not a command
                }
            }

            for (int i = command == null ? 0 : 1; i < lines.Length; i++)
            {
                var line = lines[i];

                fields = line.Split(new char[] { ':' }, 2);
                if (fields.Length < 2)
                    continue;

                name             = fields[0].Trim();
                value            = Helper.UrlDecode(fields[1].Trim());
                properties[name] = value;
            }

            if (properties.TryGetValue("Content-Length", out value))
            {
                cbContent = int.Parse(value);
                content = Receive(cbContent);
            }
            else
                content = null;

            if (properties.TryGetValue("Content-Type", out value))
                contentType = value.ToLower();
            else
                contentType = null;

            if (command != null)
                return new SwitchPacket(command, properties, contentType, content);
            else
                return new SwitchPacket(properties, content);
        }

        /// <summary>
        /// Returns <c>true</c> if the connector is currently connected to a switch.
        /// </summary>
        public bool IsConnected
        {
            get { return isConnected; }
        }

        /// <summary>
        /// Returns a cloned <b>read-only</b> set of the NeonSwitch events currently subscribed to by the connection.
        /// </summary>
        public SwitchEventCodeSet EventSubscriptions
        {
            get
            {
                lock (syncLock)
                {
                    var clone = eventSubscriptions.Clone();

                    clone.IsReadOnly = true;
                    return clone;
                }
            }
        }

        /// <summary>
        /// Returns the current switch logging level.
        /// </summary>
        public SwitchLogLevel LogLevel { get; private set; }

        /// <summary>
        /// Closes the connection if one is established with the optional
        /// indication that there was an error.
        /// </summary>
        /// <param name="error">The error exception or <c>null</c>.</param>
        private void Close(Exception error)
        {
            lock (syncLock)
            {
                if (closePending)
                    return;     // Another in-progress on another thread.

                closePending = true;

                if (socket != null)
                {
                    socket.Close();
                    socket = null;
                }

                // Abort any queued or executing commands.

                if (pendingCommand != null)
                    pendingCommand.AsyncResult.Notify(new SocketClosedException(SocketCloseReason.LocalClose));

                if (commandQueue != null)
                {
                    while (commandQueue.Count > 0)
                        commandQueue.Dequeue().AsyncResult.Notify(new SocketClosedException(SocketCloseReason.LocalClose));
                }

                // Raise the Disconnected event if a connection is established.

                if (isConnected)
                {
                    isConnected = false;
                    RaiseDisconnected(new SwitchDisconnectArgs(error));
                }

                // Clear these globals

                commandQueue   = null;
                pendingCommand = null;
                LogLevel       = SwitchLogLevel.None;

                eventSubscriptions.Clear();

                // Do the thread abort last because we might be aborting
                // the current thread.

                if (thread != null)
                {
                    thread.Abort();
                    thread = null;
                }
            }
        }

        /// <summary>
        /// Closes the connection if one is established.
        /// </summary>
        public void Close()
        {
            Close(null);
        }

        /// <summary>
        /// Submits a synchronous (<b>api</b>) command to the switch and 
        /// blocks until it has been executed by the switch.
        /// </summary>
        /// <param name="command">The switch command text.</param>
        /// <returns>The command execution reply.</returns>
        public CommandDisposition Execute(string command)
        {
            var ar = BeginExecute(command, null, null);

            return EndExecute(ar);
        }

        /// <summary>
        /// Submits a formatted synchronous (<b>api</b>) command to the 
        /// switch and blocks until it has been executed by the switch.
        /// </summary>
        /// <param name="format">The switch command format string.</param>
        /// <param name="args">The command arguments.</param>
        /// <returns>The command execution reply.</returns>
        public CommandDisposition Execute(string format, params object[] args)
        {
            return Execute(string.Format(format, args));
        }

        /// <summary>
        /// Initiates an asynchronous operation to submit a command to the remote switch.
        /// The operation will be considered to be complete when the command has been 
        /// accepted and executed by the switch.
        /// </summary>
        /// <param name="command">The command text.</param>
        /// <param name="callback">The delegate to be called when the operation completes (or <c>null</c>).</param>
        /// <param name="state">Application defined state (or <c>null</c>).</param>
        /// <returns>The <see cref="IAsyncResult" /> instance to be used to track the operation's progress.</returns>
        /// <remarks>
        /// <note>
        /// All successful calls to <see cref="BeginExecute" /> must eventually be followed by
        /// a call to <see cref="EndExecute" />.
        /// </note>
        /// </remarks>
        public IAsyncResult BeginExecute(string command, AsyncCallback callback, object state)
        {
            var arExecute = new AsyncCommandResult(null, callback, state);

            lock (syncLock)
            {
                try
                {
                    SendOrEnqueueCommand(new Command(string.Format("api {0}", command), arExecute, true));
                    arExecute.Started();
                }
                catch (Exception e)
                {
                    arExecute.Notify(e);
                }
            }

            return arExecute;
        }

        /// <summary>
        /// Completes an asynchronous command execution operation.
        /// </summary>
        /// <param name="ar">The <see cref="IAsyncResult" /> instance returned by the initiating <see cref="BeginExecute" /> call.</param>
        /// <returns>The <see cref="CommandDisposition" />.</returns>
        public CommandDisposition EndExecute(IAsyncResult ar)
        {
            var arExecute = (AsyncCommandResult)ar;

            arExecute.Wait();
            lock (syncLock)
            {
                try
                {
                    if (arExecute.Exception != null)
                        throw arExecute.Exception;

                    return arExecute.Result;
                }
                finally
                {
                    arExecute = null;
                }
            }
        }

        /// <summary>
        /// Submits a background (<b>bgapi</b> command to the switch and blocks 
        /// until a response is received indicating that the command was accepted 
        /// for processing by the switch but before it has actually been executed.  
        /// </summary>
        /// <param name="command">The switch command text.</param>
        /// <returns>The command execution reply including the job ID.</returns>
        /// <remarks>
        /// <para>
        /// The switch will indicate that has completed executing the command
        /// by raising a <see cref="SwitchEventCode.BackgroundJob" /> event that
        /// can be trapped via <see cref="EventReceived" /> or better yet,
        /// <see cref="JobCompleted" />.
        /// </para>
        /// <note>
        /// You'll need to call <see cref="Subscribe" /> to enable
        /// reception of <see cref="SwitchEventCode.BackgroundJob" /> events
        /// receive the events via <see cref="EventReceived" /> or
        /// <see cref="JobCompleted" />
        /// </note>
        /// </remarks>
        public CommandDisposition ExecuteBackground(string command)
        {
            var ar = BeginExecuteBackground(command, null, null);

            return EndExecuteBackground(ar);
        }

        /// <summary>
        /// Initiates an asynchronous operation to submit a background (<b>bgapi</b> 
        /// command to the switch and blocks until a response is received indicating 
        /// that the command was accepted for processing by the switch but before it has 
        /// actually been executed. 
        /// </summary>
        /// <param name="command">The command text.</param>
        /// <param name="callback">The delegate to be called when the operation completes (or <c>null</c>).</param>
        /// <param name="state">Application defined state (or <c>null</c>).</param>
        /// <returns>The <see cref="IAsyncResult" /> instance to be used to track the operation's progress.</returns>
        /// <remarks>
        /// <note>
        /// All successful calls to <see cref="BeginExecuteBackground" /> must eventually be followed by
        /// a call to <see cref="EndExecuteBackground" />.
        /// </note>
        /// <note>
        /// Background command execution operations are considered to be complete once
        /// the command has been accepted by the remote switch, not when the have actually
        /// been executed.  Interested applications will need to monitor the events raised
        /// by <see cref="RaiseJobCompleted" /> for a <see cref="SwitchEventCode.BackgroundJob" />
        /// event with the same <see cref="CommandDisposition" />.<see cref="CommandDisposition.JobID" /> returned 
        /// by <see cref="EndExecuteBackground" /> to discover the ultimate disposition of the job.
        /// </note>
        /// <note>
        /// You'll need to call <see cref="Subscribe" /> to enable
        /// reception of <see cref="SwitchEventCode.BackgroundJob" /> events
        /// receive the events via <see cref="EventReceived" /> or
        /// <see cref="JobCompleted" />
        /// </note>
        /// </remarks>
        public IAsyncResult BeginExecuteBackground(string command, AsyncCallback callback, object state)
        {
            var arExecute = new AsyncCommandResult(null, callback, state);

            lock (syncLock)
            {
                try
                {
                    SendOrEnqueueCommand(new Command(string.Format("bgapi {0}", command), arExecute, false));
                    arExecute.Started();
                }
                catch (Exception e)
                {
                    arExecute.Notify(e);
                }
            }

            return arExecute;

        }

        /// <summary>
        /// Completes an asynchronous background command execution operation.
        /// </summary>
        /// <param name="ar">The <see cref="IAsyncResult" /> instance returned by the initiating <see cref="BeginExecuteBackground" /> call.</param>
        /// <returns>The <see cref="CommandDisposition" />.</returns>
        /// <remarks>
        /// <note>
        /// Background command execution operations are considered to be complete once
        /// the command has been accepted by the remote switch, not when the have actually
        /// been executed.  Interested applications will need to monitor the events raised
        /// by <see cref="RaiseJobCompleted" /> for a <see cref="SwitchEventCode.BackgroundJob" />
        /// event with the same <see cref="CommandDisposition" />.<see cref="CommandDisposition.JobID" /> returned 
        /// by this method to discover the ultimate disposition of the job.
        /// </note>
        /// </remarks>
        public CommandDisposition EndExecuteBackground(IAsyncResult ar)
        {
            return EndExecute(ar);
        }

        /// <summary>
        /// Synchronously submits a command to register to receive switch events on the connection.
        /// </summary>
        /// <param name="eventSet">The set of switch event codes to be enabled.</param>
        /// <returns>The command execution reply.</returns>
        /// <exception cref="ArgumentNullException">Thrown if <paramref name="eventSet"/> is <c>null</c>.</exception>
        /// <remarks>
        /// <para>
        /// This method adds the event codes in the set passed to any existing subscriptions 
        /// made for the connection.  Use <see cref="Unsubscribe" /> to clear all
        /// subscriptions.
        /// </para>
        /// </remarks>
        public CommandDisposition Subscribe(SwitchEventCodeSet eventSet)
        {
            var ar = BeginSubscribe(eventSet, null, null);

            return EndSubscribe(ar);
        }

        /// <summary>
        /// Initiates an asynchronous operation to submits a command to register to receive switch
        /// events on the connection.
        /// </summary>
        /// <param name="eventSet">The set of switch event codes to be enabled.</param>
        /// <param name="callback">The delegate to be called when the operation completes (or <c>null</c>).</param>
        /// <param name="state">Application defined state (or <c>null</c>).</param>
        /// <returns>The <see cref="IAsyncResult" /> instance to be used to track the operation's progress.</returns>
        /// <exception cref="ArgumentNullException">Thrown if <paramref name="eventSet"/> is <c>null</c>.</exception>
        /// <remarks>
        /// <para>
        /// This method adds the event codes in the set passed to any existing subscriptions 
        /// made for the connection.  Use <see cref="Unsubscribe" /> to clear all
        /// subscriptions.
        /// </para>
        /// <note>
        /// All successful calls to <see cref="BeginSubscribe" /> must eventually be followed by
        /// a call to <see cref="EndSubscribe" />.
        /// </note>
        /// </remarks>
        public IAsyncResult BeginSubscribe(SwitchEventCodeSet eventSet, AsyncCallback callback, object state)
        {
            StringBuilder sb;

            if (eventSet == null)
                throw new ArgumentNullException("eventSet");

            sb = new StringBuilder(1024);

            // Note that we're going to internally strip off any implicit events
            // because sending these to the switch will result in an error.

            foreach (var eventCode in eventSet.Difference(SwitchEventCodeSet.ImplicitEvents))
                sb.AppendFormat(" SWITCH_EVENT_{0}", SwitchHelper.GetEventCodeString(eventCode));

            var arExecute = new AsyncCommandResult(null, callback, state);

            lock (syncLock)
            {
                try
                {
                    SendOrEnqueueCommand(new Command(string.Format("event plain{0}", sb), arExecute, false));
                    eventSubscriptions = eventSubscriptions.Union(eventSet);
                    arExecute.Started();
                }
                catch (Exception e)
                {
                    arExecute.Notify(e);
                }
            }

            return arExecute;
        }

        /// <summary>
        /// Completes an asynchronous event subscription operation.
        /// </summary>
        /// <param name="ar">The <see cref="IAsyncResult" /> instance returned by the initiating <see cref="BeginSubscribe" /> call.</param>
        /// <returns>The <see cref="CommandDisposition" />.</returns>
        public CommandDisposition EndSubscribe(IAsyncResult ar)
        {
            return EndExecute(ar);
        }

        /// <summary>
        /// Synchronously submits a command to remove some or all switch event subscriptions from the connection.
        /// </summary>
        /// <param name="eventSet">The set of events to be removed or <c>null</c> to remove all events.</param>
        /// <returns>The command execution reply.</returns>
        public CommandDisposition Unsubscribe(SwitchEventCodeSet eventSet)
        {
            var ar = BeginUnsubscribe(eventSet, null, null);

            return EndUnsubscribe(ar);
        }

        /// <summary>
        /// Initiates an asynchronous operation to remove some or all switch event subscriptions from the connection.
        /// </summary>
        /// <param name="eventSet">The set of events to be removed or <c>null</c> to remove all events.</param>
        /// <param name="callback">The delegate to be called when the operation completes (or <c>null</c>).</param>
        /// <param name="state">Application defined state (or <c>null</c>).</param>
        /// <returns>The <see cref="IAsyncResult" /> instance to be used to track the operation's progress.</returns>
        /// <remarks>
        /// <note>
        /// All successful calls to <see cref="BeginUnsubscribe" /> must eventually be followed by
        /// a call to <see cref="EndUnsubscribe" />.
        /// </note>
        /// </remarks>
        public IAsyncResult BeginUnsubscribe(SwitchEventCodeSet eventSet, AsyncCallback callback, object state)
        {
            var arExecute = new AsyncCommandResult(null, callback, state);

            lock (syncLock)
            {
                try
                {
                    string  commandText;
                    bool    isEmpty;

                    if (eventSet == null || eventSet.IsEmpty)
                    {
                        commandText = "noevents";
                        isEmpty     = true;
                    }
                    else
                    {
                        var sb = new StringBuilder();

                        foreach (var eventCode in eventSet)
                            sb.AppendFormat(" SWITCH_EVENT_{0}", SwitchHelper.GetEventCodeString(eventCode));

                        commandText = string.Format("nixevent{0}", sb);
                        isEmpty     = false;
                    }

                    SendOrEnqueueCommand(new Command(commandText, arExecute, false));

                    if (isEmpty)
                        eventSubscriptions.Clear();
                    else
                        eventSubscriptions = eventSubscriptions.Difference(eventSet);

                    arExecute.Started();
                }
                catch (Exception e)
                {
                    arExecute.Notify(e);
                }
            }

            return arExecute;

        }

        /// <summary>
        /// Completes an asynchronous event unsubscription operation.
        /// </summary>
        /// <param name="ar">The <see cref="IAsyncResult" /> instance returned by the initiating <see cref="BeginUnsubscribe" /> call.</param>
        /// <returns>The <see cref="CommandDisposition" />.</returns>
        public CommandDisposition EndUnsubscribe(IAsyncResult ar)
        {
            return EndExecute(ar);
        }

        /// <summary>
        /// Synchronously submits a command to set the logging level for the connection.
        /// </summary>
        /// <param name="level">The new <see cref="SwitchLogLevel" />.</param>
        /// <returns>The command execution reply.</returns>
        /// <remarks>
        /// <note>
        /// Pass <see cref="SwitchLogLevel.None" /> to disable logging.
        /// </note>
        /// </remarks>
        public CommandDisposition SetLogLevel(SwitchLogLevel level)
        {
            var ar = BeginSetLogLevel(level, null, null);

            return EndSetLogLevel(ar);
        }

        /// <summary>
        /// Initiates an asynchronous operation to set the logging level for the connection.
        /// </summary>
        /// <param name="level">The new <see cref="SwitchLogLevel" />.</param>
        /// <param name="callback">The delegate to be called when the operation completes (or <c>null</c>).</param>
        /// <param name="state">Application defined state (or <c>null</c>).</param>
        /// <returns>The <see cref="IAsyncResult" /> instance to be used to track the operation's progress.</returns>
        public IAsyncResult BeginSetLogLevel(SwitchLogLevel level, AsyncCallback callback, object state)
        {
            var arExecute = new AsyncCommandResult(null, callback, state);

            lock (syncLock)
            {
                try
                {
                    string commandText;

                    if (level == SwitchLogLevel.None)
                        commandText = "nolog";
                    else
                        commandText = string.Format("log {0}", (int)level);

                    SendOrEnqueueCommand(new Command(commandText, arExecute, true));
                    LogLevel = level;
                    arExecute.Started();
                }
                catch (Exception e)
                {
                    arExecute.Notify(e);
                }
            }

            return arExecute;
        }

        /// <summary>
        /// Completes an asynchronous event unsubscription operation.
        /// </summary>
        /// <param name="ar">The <see cref="IAsyncResult" /> instance returned by the initiating <see cref="BeginSetLogLevel" /> call.</param>
        /// <returns>The <see cref="CommandDisposition" />.</returns>
        public CommandDisposition EndSetLogLevel(IAsyncResult ar)
        {
            return EndExecute(ar);
        }

        /// <summary>
        /// Synchronously transmits an event to the switch for submission to the switch's event queue.
        /// </summary>
        /// <param name="eventCode">The event code.</param>
        /// <param name="properties">The event properties (or <c>null</c>).</param>
        /// <exception cref="ArgumentNullException">Thrown if <paramref name="properties"/> is <c>null</c>.</exception>
        public CommandDisposition SendEvent(SwitchEventCode eventCode, ArgCollection properties)
        {
            var ar = BeginSendEvent(eventCode, properties, null, null, null, null);

            return EndSendEvent(ar);
        }

        /// <summary>
        /// Synchronously transmits an event with optional content properties to the switch for submission
        /// to the switch's event queue.
        /// </summary>
        /// <param name="eventCode">The event code.</param>
        /// <param name="properties">The event properties.</param>
        /// <param name="contentProperties">The content properties or <c>null</c>.</param>
        /// <returns>The <see cref="CommandDisposition" />.</returns>
        /// <exception cref="ArgumentNullException">Thrown if <paramref name="properties"/> is <c>null</c>.</exception>
        /// <remarks>
        /// <note>
        /// If <paramref name="contentProperties" /> is not <c>null</c> then the application should
        /// specify a valid <b>Content-Type</b> value within the event <paramref name="properties" />
        /// collection.
        /// </note>
        /// </remarks>
        public CommandDisposition SendEvent(SwitchEventCode eventCode, ArgCollection properties, ArgCollection contentProperties)
        {
            StringBuilder   sb      = new StringBuilder();
            byte[]          content = null;
            IAsyncResult    ar;

            if (contentProperties != null)
            {
                foreach (var key in contentProperties)
                    sb.AppendFormat("{0}: {1}\n", key, Helper.UrlEncode(contentProperties[key]));

                content = Helper.ASCIIEncoding.GetBytes(sb.ToString());
            }

            ar = BeginSendEvent(eventCode, properties, null, content, null, null);
            return EndSendEvent(ar);
        }

        /// <summary>
        /// Synchronously transmits an event with optional content text to the switch for submission
        /// to the switch's event queue.
        /// </summary>
        /// <param name="eventCode">The event code.</param>
        /// <param name="properties">The event properties.</param>
        /// <param name="contentText">The content text or <c>null</c>.</param>
        /// <returns>The <see cref="CommandDisposition" />.</returns>
        /// <exception cref="ArgumentNullException">Thrown if <paramref name="properties"/> is <c>null</c>.</exception>
        /// <remarks>
        /// <note>
        /// If <paramref name="contentText" /> is not <c>null</c> then the application should
        /// specify a valid <b>Content-Type</b> value within the event <paramref name="properties" />
        /// collection.
        /// </note>
        /// <note>
        /// The <paramref name="contentText" /> passed will be submitted to the switch using the
        /// <b>ASCII</b> encoding.  To use another encoding you'll need to encode the text
        /// into bytes yourself and call <see cref="SendEvent(SwitchEventCode,ArgCollection,string,byte[])" />.
        /// </note>
        /// </remarks>
        public CommandDisposition SendEvent(SwitchEventCode eventCode, ArgCollection properties, string contentText)
        {
            StringBuilder   sb = new StringBuilder();
            byte[]          content = null;
            string          contentType = null;
            IAsyncResult    ar;

            if (contentText != null)
            {
                content     = Helper.ASCIIEncoding.GetBytes(contentText);
                contentType = "text";
            }

            ar = BeginSendEvent(eventCode, properties, contentType, content, null, null);
            return EndSendEvent(ar);
        }

        /// <summary>
        /// Synchronously transmits an event with optional content data to the switch for submission
        /// to the switch's event queue.
        /// </summary>
        /// <param name="eventCode">The event code.</param>
        /// <param name="properties">The event properties.</param>
        /// <param name="contentType">The content type or <c>null</c>.</param>
        /// <param name="content">The content data or <c>null</c>.</param>
        /// <returns>The <see cref="CommandDisposition" />.</returns>
        /// <exception cref="ArgumentNullException">Thrown if <paramref name="properties"/> is <c>null</c>.</exception>
        /// <remarks>
        /// <note>
        /// If <paramref name="content" /> is not <c>null</c> then the application should
        /// specify a valid <b>Content-Type</b> value within the event <paramref name="properties" />
        /// collection.
        /// </note>
        /// </remarks>
        public CommandDisposition SendEvent(SwitchEventCode eventCode, ArgCollection properties, string contentType, byte[] content)
        {
            var ar = BeginSendEvent(eventCode, properties, contentType, content, null, null);

            return EndSendEvent(ar);
        }

        /// <summary>
        /// Initiates an asynchronous operation to submit an event to the remote switch.
        /// The operation will be considered to be complete when the event has been 
        /// accepted by the switch.
        /// </summary>
        /// <param name="eventCode">The event code.</param>
        /// <param name="properties">The event properties.</param>
        /// <param name="contentType">The content type or <c>null</c>.</param>
        /// <param name="content">The content data or <c>null</c>.</param>
        /// <param name="callback">The delegate to be called when the operation completes (or <c>null</c>).</param>
        /// <param name="state">Application defined state (or <c>null</c>).</param>
        /// <returns>The <see cref="IAsyncResult" /> instance to be used to track the operation's progress.</returns>
        /// <exception cref="ArgumentNullException">Thrown if <paramref name="properties"/> is <c>null</c>.</exception>
        /// <remarks>
        /// <note>
        /// If <paramref name="content" /> is not <c>null</c> then the application should
        /// specify a valid <b>Content-Type</b> value within the event <paramref name="properties" />
        /// collection.
        /// </note>
        /// <note>
        /// All successful calls to <see cref="BeginSendEvent" /> must eventually be followed by
        /// a call to <see cref="EndSendEvent" />.
        /// </note>
        /// </remarks>
        public IAsyncResult BeginSendEvent(SwitchEventCode eventCode, ArgCollection properties, string contentType, byte[] content, AsyncCallback callback, object state)
        {
            if (properties == null)
                throw new ArgumentNullException("properties");

            var arExecute = new AsyncCommandResult(null, callback, state);

            lock (syncLock)
            {
                try
                {
                    var commandText = string.Format("sendevent {0}", SwitchHelper.GetEventCodeString(eventCode));
                    var command     = new Command(new SwitchPacket(commandText, properties, contentType, content), arExecute, true);

                    SendOrEnqueueCommand(command);
                    arExecute.Started();
                }
                catch (Exception e)
                {
                    arExecute.Notify(e);
                }
            }

            return arExecute;
        }

        /// <summary>
        /// Completes an asynchronous event send operation.
        /// </summary>
        /// <param name="ar">The <see cref="IAsyncResult" /> instance returned by the initiating <see cref="BeginSendEvent" /> call.</param>
        /// <returns>The <see cref="CommandDisposition" />.</returns>
        public CommandDisposition EndSendEvent(IAsyncResult ar)
        {
            var arExecute = (AsyncCommandResult)ar;

            arExecute.Wait();
            lock (syncLock)
            {
                try
                {
                    if (arExecute.Exception != null)
                        throw arExecute.Exception;

                    return arExecute.Result;
                }
                finally
                {
                    arExecute = null;
                }
            }
        }

        /// <summary>
        /// Raises the <see cref="Connected" /> event.
        /// </summary>
        private void RaiseConnected()
        {
            if (Connected != null)
                Helper.EnqueueAction(() => Connected(this, EventArgs.Empty));
        }

        /// <summary>
        /// Raises the <see cref="Disconnected" /> event.
        /// </summary>
        /// <param name="args">The event arguments.</param>
        private void RaiseDisconnected(SwitchDisconnectArgs args)
        {
            if (Disconnected != null)
                Helper.EnqueueAction(() => Disconnected(this, args));
        }

        /// <summary>
        /// Raises the <see cref="EventReceived" /> event.
        /// </summary>
        /// <param name="args">The event arguments.</param>
        private void RaiseEventReceived(SwitchEventReceivedArgs args)
        {
            if (EventReceived != null)
                Helper.EnqueueAction(() => EventReceived(this, args));
        }

        /// <summary>
        /// Raises the <see cref="SwitchJobCompletedArgs" /> event.
        /// </summary>
        /// <param name="args">The event arguments.</param>
        private void RaiseJobCompleted(SwitchJobCompletedArgs args)
        {
            if (JobCompleted != null)
                Helper.EnqueueAction(() => JobCompleted(this, args));
        }

        /// <summary>
        /// Raises the <see cref="LogEntryReceived" /> event.
        /// </summary>
        /// <param name="packet">The received packet.</param>
        private void RaiseLogEntryReceived(SwitchPacket packet)
        {
            if (LogEntryReceived != null)
                Helper.EnqueueAction(() => LogEntryReceived(this, new SwitchLogEntryReceivedArgs(packet)));
        }

        /// <summary>
        /// Raises the <see cref="CommandReceived" /> event.
        /// </summary>
        /// <param name="args">The event arguments.</param>
        private void RaiseCommandReceived(SwitchCommandReceivedArgs args)
        {
            try
            {
                if (CommandReceived != null)
                    CommandReceived(this, args);
            }
            catch (Exception e)
            {
                args.Success   = false;
                args.ReplyText = e.Message;
            }

            // Figure out what we're going to reply, making sure that there's only
            // one line of text.

            var     replyText = args.ReplyText ?? (args.Success ? "Success" : "Fail");
            int     pos;

            pos = replyText.IndexOfAny(new char[] { '\r', '\n' });
            if (pos != -1)
                replyText = replyText.Substring(9, pos);

            SendReply(replyText);
            SendResponse(args.ResponseText);
        }

        //---------------------------------------------------------------------
        // IDisposable implementation

        /// <summary>
        /// Releases all resources owned by the instance.k
        /// </summary>
        public void Dispose()
        {
            Close(null);
        }
    }
}
