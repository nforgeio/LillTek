//-----------------------------------------------------------------------------
// FILE:        StreamTransferSession.cs
// CONTRIBUTOR: Jeff Lill
// COPYRIGHT:   Copyright (c) 2005-2015 by Jeffrey Lill.  All rights reserved.
// DESCRIPTION: Implements an easy-to-use wrapper for client or server side
//              ReliableTransferSession implementations that need to transfer
//              data to and from streams.

using System;
using System.IO;
using System.Reflection;
using System.Threading;

using LillTek.Common;

namespace LillTek.Messaging
{
    /// <summary>
    /// Implements an easy-to-use wrapper for client or server side
    /// <see cref="ReliableTransferSession" /> implementations that need to transfer
    /// data to and from streams.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Client applications will use one of the static methods described below to
    /// create and initialize a <see cref="StreamTransferSession" /> ready to upload
    /// a file or <see cref="EnhancedStream" /> to a server.
    /// </para>
    /// <list type="bullet">
    ///     <item>
    ///     <see cref="ClientUpload(MsgRouter,MsgEP,string)" /> configures for 
    ///     uploading a file to the server.
    ///     </item>
    ///     <item>
    ///     <see cref="ClientUpload(MsgRouter,MsgEP,EnhancedStream)" /> configures 
    ///     for uploading an <see cref="EnhancedStream" /> to the server
    ///     </item>
    ///     <item>
    ///     <see cref="ClientDownload(MsgRouter,MsgEP,string)" /> configures for 
    ///     downloading data from the server into a file.</item>
    ///     <item>
    ///     <see cref="ClientDownload(MsgRouter,MsgEP,EnhancedStream)" /> configures 
    ///     for downloading data from the server to an <see cref="EnhancedStream" />.
    ///     </item>
    /// </list>
    /// <para>
    /// Then the client application will call <see cref="Transfer" /> to perform the operation synchronously
    /// or <see cref="BeginTransfer" /> to perform it asynchronously.  The <see cref="Args" /> property
    /// is also available for sending an application specific string to the server.  Here's some sample
    /// code showing how to initiate a transfer on the client:
    /// </para>
    /// <code language="cs">
    /// void UploadSync()
    /// {
    ///     StreamTransferSession   session;
    /// 
    ///     try 
    ///     {
    ///         session      = StreamTransferSession.ClientUpload(router,@"c:\MyFile.txt");
    ///         session.Args = "app info";
    ///         session.Transfer();
    ///     }
    ///     catch (Exception e)
    ///     {
    ///         Console.WriteLine("Error: {0}",e.Message);
    ///     }
    /// }
    /// 
    /// void OnDone(IAsyncResult ar)
    /// {
    ///     try
    ///     {
    ///         StreamTransferSession   session;
    /// 
    ///         session = (StreamTransferSession) ar.AsyncState;
    ///         session.EndTransfer();
    ///     }
    ///     catch (Exception e)
    ///     {
    ///         Console.WriteLine("Error: {0}",e.Message);
    ///     }
    /// }
    /// 
    /// void UploadAsync() 
    /// {
    ///     StreamTransferSession   session;
    /// 
    ///     session      = StreamTransferSession.ClientUpload(router,@"c:\MyFile.txt");
    ///     session.Args = "app info";
    ///     session.BeginTransfer(new AsyncCallback(OnDone),session);
    /// }
    /// </code>
    /// <para>
    /// Server side sessions are created in a similar manner but there are some differences
    /// in how the transfer works on the server.  Here are the static methods for creating
    /// a server side session:
    /// </para>
    /// <list type="bullet">
    ///     <item>
    ///     <see cref="ServerUpload(MsgRouter,ReliableTransferMsg,string)" /> configures 
    ///     for accepting data from the client and writing it to a file.
    ///     </item>
    ///     <item>
    ///     <see cref="ServerUpload(MsgRouter,ReliableTransferMsg,EnhancedStream)" /> configures 
    ///     for accepting data from the client and writing it to an <see cref="EnhancedStream" />.
    ///     </item>
    ///     <item>
    ///     <see cref="ServerDownload(MsgRouter,ReliableTransferMsg,string)" /> configures for 
    ///     downloading a file to the client.
    ///     </item>
    ///     <item>
    ///     <see cref="ServerDownload(MsgRouter,ReliableTransferMsg,EnhancedStream)" /> configures 
    ///     for downloading data from an <see cref="EnhancedStream" /> to the client.
    ///     </item>
    /// </list>
    /// <para>
    /// There are some restrictions on how transfer operations work on the server.  The
    /// first difference is that synchronous service side transfers are not possible.
    /// This means that <see cref="Transfer" /> cannot be called on the server.
    /// Doing so will throw a <see cref="InvalidOperationException" />.  The second 
    /// difference is that due to how sessions work on the server, the transfer will not
    /// actually start until after the server's message handler returns.  This means
    /// that <see cref="EndTransfer" /> cannot be called within the message handler.
    /// Calling this will also throw a <see cref="InvalidOperationException" />
    /// </para>
    /// <para>
    /// Server applications that don't need to monitor when an block transfer operation
    /// completes can call <see cref="BeginTransfer" /> passing <b>callback</b> as <c>null</c>.
    /// This indicates that the <see cref="StreamTransferSession" /> will
    /// automatically perform the <see cref="EndTransfer" /> call when the transfer
    /// completes, saving the application the trouble.
    /// </para>
    /// <para>
    /// Applications that need to track the transfer completion must pass a
    /// valid <b>callback</b> delegate to <see cref="BeginTransfer" /> and then
    /// call <see cref="EndTransfer" /> within the callback.
    /// </para>
    /// <para>
    /// Here's a server side example that tracks the transfer completion:
    /// </para>
    /// <code language="cs">
    /// void OnDone(IAsyncResult ar)
    /// {
    ///     try
    ///     {
    ///         StreamTransferSession   session;
    /// 
    ///         session = (StreamTransferSession) ar.AsyncState;
    ///         session.EndTransfer();
    ///     }
    ///     catch (Exception e)
    ///     {
    ///         Console.WriteLine("Error: {0}",e.Message);
    ///     }
    /// }
    /// 
    /// [MsgHandler(LogicalEP="logical://MyApp/Transfer")]
    /// [MsgSession(Type=SessionTypeID.ReliableTransfer)]
    /// public void OnMsg(ReliableTransferMsg msg) 
    /// {
    ///     StreamTransferSession   session;
    /// 
    ///     session      = StreamTransferSession.ServerUpload(router,msg,@"c:\MyFile.txt");
    ///     session.Args = "app info";
    ///     session.BeginTransfer(new AsyncCallback(OnDone),session);
    /// }
    /// </code>
    /// </remarks>
    public class StreamTransferSession
    {
        //---------------------------------------------------------------------
        // Static members

        /// <summary>
        /// Creates a client side <see cref="StreamTransferSession" /> configured to upload the contents of a file.
        /// </summary>
        /// <param name="router">The <see cref="MsgRouter" /> to associate with the session.</param>
        /// <param name="serverEP">The server endpoint.</param>
        /// <param name="inputPath">The path of the input file to be uploaded.</param>
        /// <returns>The new <see cref="StreamTransferSession" /> instance.</returns>
        /// <remarks>
        /// The application will need to call <see cref="Transfer" />, or <see cref="BeginTransfer" /> and
        /// <see cref="EndTransfer" /> on the session returned to perform the transfer.
        /// </remarks>
        public static StreamTransferSession ClientUpload(MsgRouter router, MsgEP serverEP, string inputPath)
        {
            return ClientUpload(router, serverEP, new EnhancedFileStream(inputPath, FileMode.Open, FileAccess.Read));
        }

        /// <summary>
        /// Creates a client side <see cref="StreamTransferSession" /> configured to upload the contents of a stream.
        /// </summary>
        /// <param name="router">The <see cref="MsgRouter" /> to associate with the session.</param>
        /// <param name="serverEP">The server endpoint.</param>
        /// <param name="input">The input <see cref="EnhancedStream" />.</param>
        /// <returns>The new <see cref="StreamTransferSession" /> instance.</returns>
        /// <remarks>
        /// The application will need to call <see cref="Transfer" />, or <see cref="BeginTransfer" /> and
        /// <see cref="EndTransfer" /> on the session returned to perform the transfer.
        /// </remarks>
        public static StreamTransferSession ClientUpload(MsgRouter router, MsgEP serverEP, EnhancedStream input)
        {
            return new StreamTransferSession(router, serverEP, TransferDirection.Upload, input);
        }

        /// <summary>
        /// Creates a client side <see cref="StreamTransferSession" /> configured to download data
        /// to a file..
        /// </summary>
        /// <param name="router">The <see cref="MsgRouter" /> to associate with the session.</param>
        /// <param name="serverEP">The server endpoint.</param>
        /// <param name="outputPath">The path of the output file where the downloaded data will be written.</param>
        /// <returns>The new <see cref="StreamTransferSession" /> instance.</returns>
        /// <remarks>
        /// The application will need to call <see cref="Transfer" />, or <see cref="BeginTransfer" /> and
        /// <see cref="EndTransfer" /> on the session returned to perform the transfer.
        /// </remarks>
        public static StreamTransferSession ClientDownload(MsgRouter router, MsgEP serverEP, string outputPath)
        {
            return ClientDownload(router, serverEP, new EnhancedFileStream(outputPath, FileMode.Create, FileAccess.ReadWrite));
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="router">The <see cref="MsgRouter" /> to associate with the session.</param>
        /// <param name="serverEP">The server endpoint.</param>
        /// <param name="output">The output stream where the downloaded data will be written.</param>
        /// <returns>The new <see cref="StreamTransferSession" /> instance.</returns>
        /// <remarks>
        /// The application will need to call <see cref="Transfer" />, or <see cref="BeginTransfer" /> and
        /// <see cref="EndTransfer" /> on the session returned to perform the transfer.
        /// </remarks>
        public static StreamTransferSession ClientDownload(MsgRouter router, MsgEP serverEP, EnhancedStream output)
        {
            return new StreamTransferSession(router, serverEP, TransferDirection.Download, output);
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="router">The <see cref="MsgRouter" /> to associate with the session.</param>
        /// <param name="msg">The <see cref="ReliableTransferMsg" /> that initiated this session.</param>
        /// <param name="outputPath">Path of the output file where the uploaded data will be written.</param>
        /// <returns>The new <see cref="StreamTransferSession" /> instance.</returns>
        /// <remarks>
        /// The application will need to <see cref="BeginTransfer" /> to initiate the transfer.
        /// </remarks>
        /// <exception cref="InvalidOperationException">
        /// Thrown if the client requested transfer direction does not match the 
        /// direction implied by this method.
        /// </exception>
        public static StreamTransferSession ServerUpload(MsgRouter router, ReliableTransferMsg msg, string outputPath)
        {
            return ServerUpload(router, msg, new EnhancedFileStream(outputPath, FileMode.Create, FileAccess.ReadWrite));
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="router">The <see cref="MsgRouter" /> to associate with the session.</param>
        /// <param name="msg">The <see cref="ReliableTransferMsg" /> that initiated this session.</param>
        /// <param name="output">The output stream where the uploaded data will be written.</param>
        /// <returns>The new <see cref="StreamTransferSession" /> instance.</returns>
        /// <remarks>
        /// The application will need to <see cref="BeginTransfer" /> to initiate the transfer.
        /// </remarks>
        /// <exception cref="InvalidOperationException">
        /// Thrown if the client requested transfer direction does not match the 
        /// direction implied by this method.
        /// </exception>
        public static StreamTransferSession ServerUpload(MsgRouter router, ReliableTransferMsg msg, EnhancedStream output)
        {
            return new StreamTransferSession(router, msg, TransferDirection.Upload, output);
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="router">The <see cref="MsgRouter" /> to associate with the session.</param>
        /// <param name="msg">The <see cref="ReliableTransferMsg" /> that initiated this session.</param>
        /// <param name="inputPath">The path of the file to be downloaded.</param>
        /// <returns>The new <see cref="StreamTransferSession" /> instance.</returns>
        /// <remarks>
        /// The application will need to <see cref="BeginTransfer" /> to initiate the transfer.
        /// </remarks>
        /// <exception cref="InvalidOperationException">
        /// Thrown if the client requested transfer direction does not match the 
        /// direction implied by this method.
        /// </exception>
        public static StreamTransferSession ServerDownload(MsgRouter router, ReliableTransferMsg msg, string inputPath)
        {
            return ServerDownload(router, msg, new EnhancedFileStream(inputPath, FileMode.Open, FileAccess.Read));
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="router">The <see cref="MsgRouter" /> to associate with the session.</param>
        /// <param name="msg">The <see cref="ReliableTransferMsg" /> that initiated this session.</param>
        /// <param name="input">The input stream whose data is to be downloaded.</param>
        /// <returns>The new <see cref="StreamTransferSession" /> instance.</returns>
        /// <remarks>
        /// The application will need to <see cref="BeginTransfer" /> to initiate the transfer.
        /// </remarks>
        /// <exception cref="InvalidOperationException">
        /// Thrown if the client requested transfer direction does not match the 
        /// direction implied by this method.
        /// </exception>
        public static StreamTransferSession ServerDownload(MsgRouter router, ReliableTransferMsg msg, EnhancedStream input)
        {
            return new StreamTransferSession(router, msg, TransferDirection.Download, input);
        }

        //---------------------------------------------------------------------
        // Instance members

        private object                      syncLock = new object();
        private MsgRouter                   router;
        private MsgEP                       serverEP;
        private TransferDirection           direction;
        private string                      args;
        private EnhancedStream              stream;
        private ReliableTransferSession     reliableSession;
        private bool                        started;
        private bool                        closed;
        private bool                        streamClosed;
        private AsyncResult                 arTransfer;
        private bool                        simError;
        private bool                        simCancel;
        private int                         delay;

        /// <summary>
        /// Private constructor for initializing <b>client side</b> sessions.
        /// </summary>
        /// <param name="router">The message router.</param>
        /// <param name="serverEP">The server endpoint.</param>
        /// <param name="direction">The transfer direction.</param>
        /// <param name="stream">The input or output stream.</param>
        private StreamTransferSession(MsgRouter router, MsgEP serverEP, TransferDirection direction, EnhancedStream stream)
        {
            ReliableTransferHandler handler;

            reliableSession                = router.CreateReliableTransferSession();
            reliableSession.SessionHandler =
            handler                        = new ReliableTransferHandler(reliableSession);
            handler.BeginTransferEvent    += new ReliableTransferDelegate(OnBeginTransfer);
            handler.EndTransferEvent      += new ReliableTransferDelegate(OnEndTransfer);

            if (direction == TransferDirection.Upload)
                handler.SendEvent += new ReliableTransferDelegate(OnSend);
            else
                handler.ReceiveEvent += new ReliableTransferDelegate(OnReceive);

            this.router       = router;
            this.serverEP     = serverEP;
            this.direction    = direction;
            this.args         = null;
            this.stream       = stream;
            this.started      = false;
            this.closed       = false;
            this.streamClosed = false;
            this.arTransfer   = null;
            this.simError     = false;
            this.simCancel    = false;
            this.delay        = 0;
        }

        /// <summary>
        /// Private constructor for initializing <b>server side</b> sessions.
        /// </summary>
        /// <param name="router">The message router.</param>
        /// <param name="msg">The <see cref="ReliableTransferMsg" /> that initiated this session.</param>
        /// <param name="direction">The transfer direction supported by the server.</param>
        /// <param name="stream">The input or output stream.</param>
        /// <exception cref="InvalidOperationException">
        /// Thrown if the client requested transfer direction does not match the 
        /// direction implied by this method.
        /// </exception>
        private StreamTransferSession(MsgRouter router, ReliableTransferMsg msg, TransferDirection direction, EnhancedStream stream)
        {
            if (direction != msg.Direction)
                throw new InvalidOperationException(string.Format("Transfer direction [{0}] is not supported.", msg.Direction));

            ReliableTransferHandler handler;

            reliableSession                = msg.Session;
            reliableSession.SessionHandler =
            handler                        = new ReliableTransferHandler(reliableSession);
            handler.BeginTransferEvent    += new ReliableTransferDelegate(OnBeginTransfer);
            handler.EndTransferEvent      += new ReliableTransferDelegate(OnEndTransfer);

            if (direction == TransferDirection.Upload)
                handler.ReceiveEvent += new ReliableTransferDelegate(OnReceive);
            else
                handler.SendEvent += new ReliableTransferDelegate(OnSend);

            this.router     = router;
            this.direction  = direction;
            this.args       = msg.Args;
            this.stream     = stream;
            this.started    = false;
            this.closed     = false;
            this.arTransfer = null;
            this.simError   = false;
            this.simCancel  = false;
            this.delay      = 0;
        }

        /// <summary>
        /// Available for unit tests to force a simulated exception when
        /// data is received or sent on the session.  This defaults to <c>false</c>.
        /// </summary>
        internal bool SimulateError 
        {
            get { return simError; }
            set { simError = value; }
        }

        /// <summary>
        /// Available for unit tests to force a simulated cancel when
        /// data is received or sent on the session.  This defaults to <c>false</c>.
        /// </summary>
        internal bool SimulateCancel 
        {
            get { return simCancel; }
            set { simCancel = value; }
        }

        /// <summary>
        /// Available for unit tests to specify the milliseconds of delay to
        /// be introduced into each session stream I/O call.  This defaults to zero.
        /// </summary>
        internal int Delay 
        {
            get { return delay; }
            set { delay = value; }
        }

        /// <summary>
        /// Application specific transfer arguments (or <c>null</c>).
        /// </summary>
        /// <remarks>
        /// This value will be passed to the server in the <see cref="ReliableTransferMsg" />
        /// message in its <see cref="ReliableTransferMsg.Args" /> property.
        /// </remarks>
        public string Args
        {
            get { return args; }
            set { args = value; }
        }

        /// <summary>
        /// Performs a synchronous transfer operation.  <b>Note that this cannot be used for
        /// server side sessions</b>.
        /// </summary>
        public void Transfer()
        {
            IAsyncResult ar;

            if (reliableSession.IsServer)
                throw new InvalidOperationException("StreamTransferSession cannot perform synchronous server side transfers.");

            try
            {
                ar = BeginTransfer(null, null);
                EndTransfer(ar);
            }
            catch
            {
                CloseStream();
                throw;
            }
        }

        /// <summary>
        /// Closes the stream if it's not already closed.
        /// </summary>
        private void CloseStream()
        {
            lock (syncLock)
            {
                if (!streamClosed && stream != null)
                {
                    try
                    {
                        stream.Close();
                    }
                    catch
                    {
                        // Ignore errors
                    }

                    streamClosed = true;
                }
            }
        }

        /// <summary>
        /// Initiates an asynchronous transfer.
        /// </summary>
        /// <param name="callback">The delegate to be called when the operation completes (or <c>null</c>).</param>
        /// <param name="state">Application defined state (or <c>null</c>).</param>
        /// <returns>The <see cref="IAsyncResult" /> to be used to track the operation.</returns>
        /// <remarks>
        /// <note>
        /// For server sessions, passing <paramref name="callback" /> as <c>null</c> indicates
        /// that the session will automatically call <see cref="EndTransfer" /> when the operation completes.
        /// Applications that wish to track the transfer completion must pass a non-<c>null</c> callback.
        /// </note>
        /// </remarks>
        public IAsyncResult BeginTransfer(AsyncCallback callback, object state)
        {
            if (started)
                throw new InvalidOperationException("Cannot reuse a StreamTransferSession instance.");

            started = true;

            if (reliableSession.IsClient)
                return reliableSession.BeginTransfer(serverEP, direction, Helper.NewGuid(), 0, args, null, null);

            // Server side transfers start automatically when the message
            // handler returns.

            arTransfer = new AsyncResult(null, callback, state);

            arTransfer.Started();
            return arTransfer;
        }

        /// <summary>
        /// Internal transfer completion callback.
        /// </summary>
        /// <param name="ar">The reliable transfer async result.</param>
        private void OnDone(IAsyncResult ar)
        {
            var arTransfer = (AsyncResult)ar.AsyncState;

            try
            {
                reliableSession.EndTransfer(ar);
                CloseStream();
                arTransfer.Notify();
            }
            catch (Exception e)
            {
                arTransfer.Notify(e);
            }
        }

        /// <summary>
        /// Waits for an asynchronous transfer to complete.
        /// </summary>
        /// <param name="ar">The <see cref="IAsyncResult" /> instance returned by <see cref="BeginTransfer" />.</param>
        /// <remarks>
        /// <note>
        /// With the exception of the special server side case where <see cref="BeginTransfer" />
        /// was called with <b>callback</b> as <c>null</c>, all calls to <see cref="BeginTransfer" /> must be
        /// matched with a call to <see cref="EndTransfer" />.
        /// </note>
        /// <note>
        /// Servers cannot call <see cref="EndTransfer" /> within 
        /// the session's message handler.
        /// </note>
        /// </remarks>
        public void EndTransfer(IAsyncResult ar)
        {
            try
            {
                if (reliableSession.IsClient)
                {
                    reliableSession.EndTransfer(ar);
                    return;
                }

                var arTransfer = (AsyncResult)ar;

                if (reliableSession.InMsgHandler)
                    throw new InvalidOperationException("StreamTransferSession.EndTransfer() cannot called within the session message handler.");

                if (closed)
                    throw new InvalidOperationException("StreamTransferSession.EndTransfer() has already been called.");

                arTransfer.Wait();
                try
                {
                    if (arTransfer.Exception != null)
                        throw arTransfer.Exception;
                }
                finally
                {
                    arTransfer.Dispose();
                    closed = true;
                }
            }
            finally
            {
                CloseStream();
            }
        }

        /// <summary>
        /// Cancels a transfer if it's still in progress.
        /// </summary>
        public void Cancel()
        {
            reliableSession.Cancel();
        }

        //---------------------------------------------------------------------
        // ReliableTransferHandler event handlers

        private void OnBeginTransfer(ReliableTransferHandler sender, ReliableTransferArgs args)
        {
        }

        private void OnEndTransfer(ReliableTransferHandler sender, ReliableTransferArgs args)
        {
            CloseStream();

            if (reliableSession.IsServer)
            {
                if (arTransfer.Callback != null)
                    arTransfer.Notify(args.Exception);
                else
                {
                    // No callback so we'll handle the termination ourselves.

                    arTransfer.Dispose();
                    closed = true;
                }
            }

            // Note that client side notification is handled by the 
            // ReliableTransferSession class.
        }

        private void OnSend(ReliableTransferHandler sender, ReliableTransferArgs args)
        {
            if (simError)
                throw new Exception("Simulated error.");

            if (simCancel) {

                sender.Session.Cancel();
                return;
            }

            if (delay > 0)
                Thread.Sleep(delay);

            args.BlockData = stream.ReadBytes(args.BlockSize);
        }

        private void OnReceive(ReliableTransferHandler sender, ReliableTransferArgs args)
        {
            if (simError)
                throw new Exception("Simulated error.");

            if (simCancel) {

                sender.Session.Cancel();
                return;
            }

            if (delay > 0)
                Thread.Sleep(delay);

            stream.WriteBytesNoLen(args.BlockData);
        }
    }
}
