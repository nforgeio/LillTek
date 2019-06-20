//-----------------------------------------------------------------------------
// FILE:        SharedMemClient.cs
// CONTRIBUTOR: Jeff Lill
// COPYRIGHT:   Copyright (c) 2005-2015 by Jeffrey Lill.  All rights reserved.
// DESCRIPTION: Defines the SharedMemOutbox class which implements a machanism
//              for transmitting interprocess byte array messages using 
//              shared memory.

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;

using LillTek.Common;

// $todo(jeffli): Consider implementing buffer pooling.

namespace LillTek.LowLevel
{
    /// <summary>
    /// Implements the server side of a high performance shared memory based interprocess communication
    /// mechanism.  <see cref="SharedMemServer{TMessageFactory}"/> implements the server side. 
    /// </summary>
    /// <typeparam name="TMessageFactory">The <see cref="ISharedMemMessageFactory"/> implementation.</typeparam>
    /// <remarks>
    /// <para>
    /// The <see cref="SharedMemServer{TMessageFactory}"/> and <see cref="SharedMemClient{TMessageFactory}"/> classes
    /// work together to implement a simple and very high performance single-box interprocess RPC-style communication
    /// mechanism based on shared memory.  The server application will create a <see cref="SharedMemServer{TMessageFactory}"/>,
    /// specifying the base name for the server, the maximum size allowed for server requests,
    /// as well as the function to be invoked to process received client requests.
    /// </para>
    /// <para>
    /// The application client and server must provide <see cref="SharedMemMessage"/> implementations for
    /// the client side request message as well as the server side response.  These implementations handle
    /// the serialization and deserialization of the messages from shared memory.
    /// </para>
    /// <para>
    /// The client application will instantiate a <see cref="SharedMemClient{TMessageFactory}"/>
    /// instance, using the same base name used to identify the server.  Client applications submit 
    /// requests to the server via the <see cref="SharedMemClient{TMessageFactory}.CallAsync"/>
    /// method which will use shared memory to marshal the request to the server and then retrieve
    /// the response.
    /// </para>
    /// <para><b><u>Implementation Notes</u></b></para>
    /// <para>
    /// Underneath the covers, the client and server classes use <see cref="SharedMemInbox"/> and
    /// <see cref="SharedMemOutbox"/> instances to transfer data in both directions.  The client and
    /// server each create inboxes to receive data and outboxes to transmit data.  The server inbox
    /// name will be the name passed to the constructor.  The client inbox name is formed by the 
    /// name passed to the constructor along with an added GUID to disambiguate multiple clients.
    /// </para>
    /// <para>
    /// Here are the steps these classes perform to implement RPC-style request/response IPC via
    /// shared memory:
    /// </para>
    /// <list type="number">
    /// <item>
    /// The client application makes a call to <see cref="SharedMemClient{TMessageFactory}.CallAsync"/>,
    /// passing an application request message derived from <see cref="SharedMemMessage"/>.
    /// </item>
    /// <item>
    /// The shared memory client will serialize the request to bytes (including a request GUID
    /// as well as the name of the client's inbox) and then send the request to the server's inbox. 
    /// </item>
    /// <item>
    /// The shared memory server will receive the request from inbox, call the application's message
    /// factory to construct a message instance, deserialize the message, and then call the server 
    /// application's request handler.  The handler will process the request and then return aresponse
    /// message or throw an exception.
    /// </item>
    /// <item>
    /// The server will transmit the response message back to the client's inbox including the
    /// request message GUID (allowing the client to correlate responses with requests).
    /// If an exception was thrown, a special response message will be delivered that will
    /// cause a <see cref="SharedMemException"/> to be thrown by the client.
    /// </item>
    /// <item>
    /// The client will receive the message and correlate it with the pending request task
    /// and return the response message (or throw the exception), completing the transaction.
    /// </item>
    /// </list>
    /// </remarks>
    /// <threadsafety instance="true" static="true"/>
    public class SharedMemClient<TMessageFactory> : IDisposable
        where TMessageFactory : ISharedMemMessageFactory, new()
    {
        //---------------------------------------------------------------------
        // Private types

        /// <summary>
        /// Used to track a pending server call.
        /// </summary>
        private struct PendingOperation
        {
            /// <summary>
            /// The operation's request message.
            /// </summary>
            public SharedMemMessage RequestMessage;

            /// <summary>
            /// The <see cref="TaskCompletionSource{SharedMemMessage}"/> to be used to complete the operation.
            /// </summary>
            public TaskCompletionSource<SharedMemMessage> Tcs;

            /// <summary>
            /// The elapsed time as compared to the client's stopwatch when the
            /// operation should be terminated with a timeout.
            /// </summary>
            public TimeSpan TTD;

            /// <summary>
            /// Constructor.
            /// </summary>
            /// <param name="request">The operation's request message.</param>
            /// <param name="tcs">The <see cref="TaskCompletionSource{SharedMemMessage}"/> to be used to complete the operation.</param>
            /// <param name="ttd">
            /// The elapsed time as compared to the client's stopwatch when the
            /// operation should be terminated with a timeout (time-to-die).
            /// </param>
            public PendingOperation(SharedMemMessage request, TaskCompletionSource<SharedMemMessage> tcs, TimeSpan ttd)
            {
                this.RequestMessage = request;
                this.Tcs            = tcs;
                this.TTD            = ttd;
            }
        }

        //---------------------------------------------------------------------
        // Implementation

        private object                              syncLock          = new object();
        private TMessageFactory                     messageFactory    = new TMessageFactory();
        private Stopwatch                           stopwatch         = new Stopwatch();
        private Dictionary<Guid, PendingOperation>  pendingOperations = new Dictionary<Guid, PendingOperation>();
        private SharedMemInbox                      inbox;
        private SharedMemOutbox                     outbox;
        private CancellationTokenSource             timeoutCts;
        private Task                                timeoutTask;

        /// <summary>
        /// Constructor.
        /// </summary>
        /// <param name="serverInboxName">The name of the server shared memory inbox.</param>
        /// <param name="requestCapacity">The maximum byte size of a serialized request (defaults to 1MB).</param>
        /// <param name="responseCapacity">The maximum byte size of a serialized response (defaults to 1MB).</param>
        /// <remarks>
        /// <note>
        /// The <paramref name="requestCapacity"/> and <paramref name="responseCapacity"/> parameters must exactly
        /// match the values configured for the server.
        /// </note>
        /// </remarks>
        public SharedMemClient(string serverInboxName, int requestCapacity = 1024*1024, int responseCapacity = 1024*1024)
        {
            if (string.IsNullOrEmpty(serverInboxName))
            {
                throw new ArgumentNullException("name");
            }

            if (requestCapacity <= 0)
            {
                throw new ArgumentException("requestCapacity");
            }

            if (responseCapacity <= 0)
            {
                throw new ArgumentException("responseCapacity");
            }

            this.ServerName       = serverInboxName;
            this.ClientName       = serverInboxName + ":" + Guid.NewGuid().ToString("D");
            this.RequestCapacity  = requestCapacity;
            this.ResponseCapacity = responseCapacity;
            this.outbox           = new SharedMemOutbox(requestCapacity, TimeSpan.FromSeconds(5)); // $todo(jeffli): Make [maxWait] configurable?
            this.inbox            = new SharedMemInbox();
            this.timeoutCts       = new CancellationTokenSource();
            this.timeoutTask      = Task.Run(async () => await TimeoutTask());
            this.Timeout          = TimeSpan.FromSeconds(5);

            this.stopwatch.Start();
            this.inbox.Open(this.ClientName, responseCapacity, new SharedMemInboxReceiveDelegate(OnResponse));
        }

        /// <summary>
        /// Finalizer.
        /// </summary>
        ~SharedMemClient()
        {
            Dispose(false);
        }

        /// <summary>
        /// Releases any important resources associated with the instance.
        /// </summary>
        public void Dispose()
        {
            Dispose(true);
        }

        /// <summary>
        /// Releases any important resources assocated with the instance.
        /// </summary>
        /// <param name="disposing"><c>true</c> if the instance is being disposed as opposed to being finalized.</param>
        protected void Dispose(bool disposing)
        {
            lock (syncLock)
            {
                if (outbox != null)
                {
                    outbox.Close();
                    outbox = null;
                }

                if (inbox != null)
                {
                    inbox.Close();
                    inbox = null;
                }

                if (timeoutTask != null)
                {
                    timeoutCts.Cancel();

                    try
                    {
                        timeoutTask.Wait(TimeSpan.FromSeconds(1));
                    }
                    catch (TimeoutException)
                    {
                        // Ignore these.
                    }

                    timeoutTask = null;
                }

                if (disposing)
                {
                    GC.SuppressFinalize(this);
                }
            }
        }

        /// <summary>
        /// Returns the name of the server shared memory inbox.
        /// </summary>
        public string ServerName { get; private set; }

        /// <summary>
        /// Returns the name of this client instance's inbox.
        /// </summary>
        public string ClientName { get; private set; }

        /// <summary>
        /// Returns the maximum byte size allowed for a shared memory request.
        /// </summary>
        public int RequestCapacity { get; private set; }

        /// <summary>
        /// Returns the maximum byte size allowed for a shared memory response.
        /// </summary>
        public int ResponseCapacity { get; private set; }

        /// <summary>
        /// The default call operation timeout (defaults to 5 seconds).
        /// </summary>
        public TimeSpan Timeout { get; set; }

        /// <summary>
        /// Asynchronously submits a <see cref="SharedMemMessage"/> request message to the server
        /// and waits for and returns the response <see cref="SharedMemMessage"/>.
        /// </summary>
        /// <param name="request">The request message.</param>
        /// <param name="timeout">The optional timeout to override the <see cref="Timeout"/> property.
        /// </param>
        /// <returns>The response <see cref="SharedMemMessage"/>.</returns>
        public Task<SharedMemMessage> CallAsync(SharedMemMessage request, TimeSpan? timeout = null)
        {
            if (request == null)
            {
                throw new ArgumentNullException("request");
            }

            if (request.InternalRequestId != Guid.Empty)
            {
                throw new InvalidOperationException("Cannot reuse a [SharedMemMessage] request instance previously submitted for a call operation.");
            }

            request.InternalRequestId   = Guid.NewGuid();
            request.InternalClientInbox = this.ClientName;

            if (!timeout.HasValue)
            {
                timeout = this.Timeout;
            }

            var operation = new PendingOperation(request, new TaskCompletionSource<SharedMemMessage>(), stopwatch.Elapsed + timeout.Value);

            lock (syncLock)
            {
                pendingOperations.Add(request.InternalRequestId, operation);
            }

            try
            {
                using (var output = new EnhancedMemoryStream(request.SerializedCapacityHint))
                {
                    output.WriteInt32(request.TypeCode);
                    request.InternalWriteTo(output);
                    request.WriteTo(output);

                    // This call is synchronous but should execute very quickly (microseconds).

                    outbox.Send(ServerName, output.ToArray());
                }
            }
            catch (Exception e)
            {
                lock (syncLock)
                {
                    pendingOperations.Remove(operation.RequestMessage.InternalRequestId);
                }

                operation.Tcs.TrySetException(e);
            }

            return operation.Tcs.Task;
        }

        /// <summary>
        /// Asynchronously submits a <see cref="SharedMemMessage"/> request message to the server
        /// and waits for and returns a response of type <typeparamref name="TResponse"/>.
        /// </summary>
        /// <typeparam name="TResponse">The response message type.</typeparam>
        /// <param name="request">The request message.</param>
        /// <param name="timeout">The optional timeout to override the <see cref="Timeout"/> property.
        /// </param>
        /// <returns>The response <see cref="SharedMemMessage"/>.</returns>
        public async Task<TResponse> CallAsync<TResponse>(SharedMemMessage request, TimeSpan? timeout = null)
            where TResponse : SharedMemMessage, new()
        {
            return (TResponse)(await CallAsync(request, timeout));
        }

        /// <summary>
        /// Called when the client receives a response.
        /// </summary>
        /// <param name="responseBytes">The serialized response.</param>
        private void OnResponse(byte[] responseBytes)
        {
            try
            {
                using (var input = new EnhancedMemoryStream(responseBytes))
                {
                    int                 typeCode = input.ReadInt32();
                    SharedMemMessage    response;
                    PendingOperation    operation;

                    if (typeCode < 0)
                    {
                        response = new SharedMemErrorMessage();
                    }
                    else
                    {
                        response = messageFactory.Create(typeCode);
                    }

                    response.InternalReadFrom(input);
                    response.ReadFrom(input);

                    lock (syncLock)
                    {
                        if (!pendingOperations.TryGetValue(response.InternalRequestId, out operation))
                        {
                            // The response received does not correlate to a pending operation
                            // (probably due to the client side timing it out).  We'll just
                            // ignore it.

                            return;
                        }
                    }

                    if (response.InternalError != null)
                    {
                        operation.Tcs.TrySetException(new SharedMemException(response.InternalError));
                    }
                    else
                    {
                        operation.Tcs.TrySetResult(response);
                    }
                }
            }
            catch (Exception e)
            {
                SysLog.LogException(e);
            }
        }

        /// <summary>
        /// Implements the operation timeout background task.
        /// </summary>
        private async Task TimeoutTask()
        {
            var delList = new List<PendingOperation>();

            while (!timeoutCts.IsCancellationRequested)
            {
                try
                {
                    delList.Clear();

                    lock (syncLock)
                    {
                        foreach (var operation in pendingOperations.Values)
                        {
                            if (operation.TTD <= stopwatch.Elapsed)
                            {
                                delList.Add(operation);
                            }
                        }

                        foreach (var operation in delList)
                        {
                            pendingOperations.Remove(operation.RequestMessage.InternalRequestId);
                            operation.Tcs.TrySetException(new TimeoutException());
                        }
                    }
                }
                catch (Exception e)
                {
                    SysLog.LogException(e);
                }

                await Task.Delay(TimeSpan.FromSeconds(1));
            }
        }
    }
}
