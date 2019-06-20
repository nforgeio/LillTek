//-----------------------------------------------------------------------------
// FILE:        SharedMemServer.cs
// CONTRIBUTOR: Jeff Lill
// COPYRIGHT:   Copyright (c) 2005-2015 by Jeffrey Lill.  All rights reserved.
// DESCRIPTION: Implements the server side of a high performance shared memory 
//              based interprocess communication mechanism.

using System;
using System.Collections;
using System.Diagnostics;
using System.Threading;

using LillTek.Common;

// $todo(jeffli): Consider implementing buffer pooling.

namespace LillTek.LowLevel
{
    /// <summary>
    /// Implements the server side of a high performance shared memory based interprocess communication
    /// mechanism.  <see cref="SharedMemClient{TMessageFactory}"/> implements the client side.
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
    public class SharedMemServer<TMessageFactory> : IDisposable
        where TMessageFactory : ISharedMemMessageFactory, new()
    {
        private object                                      syncLock       = new object();
        private TMessageFactory                             messageFactory = new TMessageFactory();
        private Func<SharedMemMessage, SharedMemMessage>    requestHandler;
        private SharedMemInbox                              inbox;
        private SharedMemOutbox                             outbox;
        
        /// <summary>
        /// Constructor.
        /// </summary>
        /// <param name="serverInboxName">The name of the server shared memory inbox.</param>
        /// <param name="requestHandler">The server's request handler.</param>
        /// <param name="requestCapacity">The maximum byte size of a serialized request (defaults to 1MB).</param>
        /// <param name="responseCapacity">The maximum byte size of a serialized response defaults to 1MB).</param>
        /// <remarks>
        /// <note>
        /// The <paramref name="requestCapacity"/> and <paramref name="responseCapacity"/> parameters must exactly
        /// match the values configured for the server.
        /// </note>
        /// </remarks>
        public SharedMemServer(string serverInboxName, Func<SharedMemMessage, SharedMemMessage> requestHandler, int requestCapacity = 1024*1024, int responseCapacity = 1024*1024)
        {
            if (string.IsNullOrEmpty(serverInboxName))
            {
                throw new ArgumentNullException("name");
            }

            if (requestHandler == null)
            {
                throw new ArgumentNullException("requestHandler");
            }

            if (requestCapacity <= 0)
            {
                throw new ArgumentException("requestCapacity");
            }

            if (responseCapacity <= 0)
            {
                throw new ArgumentException("responseCapacity");
            }

            this.ServerInboxName = serverInboxName;
            this.RequestCapacity = requestCapacity;
            this.requestHandler  = requestHandler;
            this.outbox          = new SharedMemOutbox(responseCapacity, TimeSpan.FromSeconds(5)); // $todo(jeffli): Make [maxWait] configurable?
            this.inbox           = new SharedMemInbox();

            this.inbox.Open(this.ServerInboxName, requestCapacity, new SharedMemInboxReceiveDelegate(OnRequest));
        }

        /// <summary>
        /// Finalizer.
        /// </summary>
        ~SharedMemServer()
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

                if (disposing)
                {
                    GC.SuppressFinalize(this);
                }
            }
        }

        /// <summary>
        /// Returns the name of the server shared memory inbox.
        /// </summary>
        public string ServerInboxName { get; private set; }

        /// <summary>
        /// Returns the maximum byte size allowed for a shared memory request.
        /// </summary>
        public int RequestCapacity { get; private set; }

        /// <summary>
        /// Returns the maximum byte size allowed for a shared memory response.
        /// </summary>
        public int ResponseCapacity { get; private set; }

        /// <summary>
        /// Called when the server receives an unserialized request.
        /// </summary>
        /// <param name="requestBytes">The serialized request.</param>
        private void OnRequest(byte[] requestBytes)
        {
            try
            {
                using (var input = new EnhancedMemoryStream(requestBytes))
                {
                    int                 typeCode = input.ReadInt32();
                    SharedMemMessage    request;
                    SharedMemMessage    response;

                    if (typeCode < 0)
                    {
                        request = new SharedMemErrorMessage();
                    }
                    else
                    {
                        request = messageFactory.Create(typeCode);
                    }

                    request.InternalReadFrom(input);
                    request.ReadFrom(input);

                    try
                    {
                        response = requestHandler(request);

                        if (response == null)
                        {
                            throw new NullReferenceException("Server request handler returned a NULL response message.");
                        }
                    }
                    catch (Exception e)
                    {
                        response               = new SharedMemErrorMessage();
                        response.InternalError = string.Format("{0}: {1}", e.GetType().FullName, e.Message);
                    }

                    response.InternalRequestId   = request.InternalRequestId;
                    response.InternalClientInbox = request.InternalClientInbox;

                    using (var output = new EnhancedMemoryStream(response.SerializedCapacityHint))
                    {
                        output.WriteInt32(response.TypeCode);
                        response.InternalWriteTo(output);
                        response.WriteTo(output);

                        // This call is synchronous but should execute very quickly (microseconds).

                        outbox.Send(response.InternalClientInbox, output.ToArray());
                    }
                }
            }
            catch (Exception e)
            {
                SysLog.LogException(e);
            }
        }
    }
}
