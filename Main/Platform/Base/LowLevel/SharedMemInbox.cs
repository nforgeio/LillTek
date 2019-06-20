//-----------------------------------------------------------------------------
// FILE:        SharedMemInbox.cs
// CONTRIBUTOR: Jeff Lill
// COPYRIGHT:   Copyright (c) 2005-2015 by Jeffrey Lill.  All rights reserved.
// DESCRIPTION: Defines the SharedMemInbox class which implements a machanism
//              for transmitting interprocess byte array messages using 
//              shared memory.

// $note: 
//
// The implementation is pretty simple-minded and there's nothing
// to prevent a malicious application from trying to intercept
// messages to another process by opening another shared memory
// inbox.  I was thinking about putting the process ID of the
// first creator into the shared memory and doing a runtime check
// but this could be defeated by simply using the Windows API.

// $todo(jeff.lill): 
//
// This implementation uses SharedMem which implements its own
// Mutex to protect the memory block.  I believe this mutex
// is not really necessary for the implementation of the shared
// memory inbox/outbox.  I could get slightly better performance by
// recoding SharedMemInbox and SharedMemOutbox to implement a
// special internal version of SharedMem.

using System;
using System.Diagnostics;
using System.Threading;

using LillTek.Common;
using LillTek.Windows;

namespace LillTek.LowLevel
{
    /// <summary>
    /// This delegate describes the callback the shared memory inbox will
    /// use to notify the receiving process of a new message.
    /// </summary>
    /// <param name="message">The received message.</param>
    public delegate void SharedMemInboxReceiveDelegate(byte[] message);

    /// <summary>
    /// <para>
    /// This class implements a high performance shared memory-based mechanism
    /// for interprocess communication.  The idea is that the process receiving
    /// the message will create a SharedMemInbox specifying a unique name.
    /// Then the process sending messages will create a SharedMemOutbox and
    /// then call <see cref="SharedMemOutbox.Send" /> to send a byte array message to a
    /// named inbox.  The receiving process will be notified of the new
    /// message via a delegate call.  Note that this call will be made on
    /// an internal thread managed by this class.
    /// </para>
    /// <note>
    /// Message passing is one-way.  You'll need to create another set
    /// of inbox/outbox objects to send messages the other way.
    /// </note>
    /// </summary>
    public class SharedMemInbox
    {
        // Note: The shared memory block is formatted as follows:
        //
        // INT32    maxMsgSize;         // Maximum message size allowed
        // INT32    curMsgSize;         // Current message size
        // BYTE     inboxListening;     // non-zero if a SharedMemInbox instance is listening
        // BYTE[]   message;            // The current message

        internal const int MemHeaderSize        = 9;
        internal const int MaxMsgSizeOffset     = 0;
        internal const int CurMsgSizeOffset     = 4;
        internal const int InboxListeningOffset = 8;
        internal const int MessageOffset        = MemHeaderSize;

        private object                          syncLock = new object();
        private SharedMem                       sharedMem;      // The shared memory block used
        private int                             maxMsgSize;     // Max message size in bytes
        private SharedMemInboxReceiveDelegate   onReceive;      // Received message callback
        private WaitCallback onDispatch;                        // Dispatches the receive notification
        private GlobalAutoResetEvent            newMsgEvent;    // Event set by an outbox to signal the
                                                                // inbox of a new message
        private GlobalAutoResetEvent            emptyBoxEvent;  // Event set when the inbox is ready
                                                                // for a new message to be placed within
#if WINFULL
        private Thread                          recvThread;     // Background thread that waits for messages.
#else
        private CEThread                        recvThread;     // Background thread that waits for messages.
#endif
        private bool                            killThread;     // Signal to the background thread to die

        /// <summary>
        /// Default constructor.
        /// </summary>
        public SharedMemInbox()
        {
            sharedMem  = null;
            killThread = false;
        }

        /// <summary>
        /// Finalizer.
        /// </summary>
        ~SharedMemInbox()
        {
            Close();
        }

        /// <summary>
        /// This method initializes a shared memory inbox by creating one if
        /// it doesn't already exist or opening it if it does exist.  Note that
        /// all successful calls to Open() must be matched with a call to 
        /// <see cref="Close" />. Note that to avoid problems, only one process 
        /// may open an inbox using the SharedMemInbox class.  This method is threadsafe.
        /// </summary>
        /// <param name="name">
        /// Name of the inbox.  This can be a maximum of 128 characters and may
        /// not include the backslash (\) character.  The name is case sensitive.
        /// </param>
        /// <param name="maxMsgSize">
        /// Maximum message size allowed in bytes.  Note that this parameter must be
        /// the same across all instances of SharedMemInbox and SharedMemOutBox
        /// classes accessing this inbox.
        /// </param>
        /// <param name="onReceive">
        /// Delegate to be called when a message is placed in the inbox.  Note that
        /// this method will be called on a pool thread.
        /// </param>
        public unsafe void Open(string name, int maxMsgSize, SharedMemInboxReceiveDelegate onReceive)
        {
            if (name.Length > 128)
                throw new ArgumentException("Name exceeds 128 characters.", "name");

            if (name.IndexOfAny(new char[] { '/', '\\' }) != -1)
                throw new ArgumentException("Name may not include forward or backslashes.");

            if (maxMsgSize <= 0)
                throw new ArgumentException("Invalid maximum message size.", "maxMsgSize");

            lock (syncLock)
            {
                if (this.sharedMem != null)
                    return;     // Already open

                this.maxMsgSize = maxMsgSize;
                this.onReceive  = onReceive;
                this.onDispatch = new WaitCallback(OnDispatch);
                this.sharedMem  = new SharedMem();
                this.sharedMem.Open(name, maxMsgSize + MemHeaderSize, SharedMem.OpenMode.CREATE_OPEN);

                // Here's what the abbreviations mean:
                //
                //      LT  = LillTek
                //      SMI = SharedMemInBox
                //      NME = NewMessageEvent
                //      EBE = EmptyBoxEvent

                this.newMsgEvent   = new GlobalAutoResetEvent("LT:SMI:NME:" + name);
                this.emptyBoxEvent = new GlobalAutoResetEvent("LT:SMI:EBE:" + name);
                this.killThread    = false;
#if WINFULL
                this.recvThread    = new Thread(new ThreadStart(ReceiveThreadProc));
#else
                this.recvThread    = new CEThread(new ThreadStart(ReceiveThreadProc));
#endif
                this.recvThread.Start();

                byte*       p;

                p = sharedMem.Lock();
                *(int*)&p[MaxMsgSizeOffset] = maxMsgSize;
                *(int*)&p[CurMsgSizeOffset] = 0;
                p[InboxListeningOffset]     = 1;
                sharedMem.Unlock();

                emptyBoxEvent.Set();
            }
        }

        /// <summary>
        /// This method closes the inbox.  All successful calls to <see cref="Open" /> must
        /// be matched with a call to Close() to ensure that system resources
        /// are released in a timely manner.  Note that it is OK to call Close()
        /// even if the shared memory block is not open.  This method is threadsafe.
        /// </summary>
        public unsafe void Close()
        {
            lock (syncLock)
            {
                if (sharedMem == null)
                    return;

                byte* p;

                p = sharedMem.Lock();       // Indicate that we're not listening any more
                if (p != null)
                {
                    p[InboxListeningOffset] = 0;
                    sharedMem.Unlock();
                }
#if WINFULL
                // Wait up to 5 seconds for the receive thread to stop normally before
                // forcing the issue.

                int cWaits = 0;

                do
                {
                    killThread = true;      // Kill the receive thread
                    newMsgEvent.Set();
                    cWaits++;

                    if (cWaits == 5)
                        recvThread.Abort();

                } while (!recvThread.Join(1000));
#else
                newMsgEvent.Set();
                killThread = true;
                Thread.Sleep(5000);
                recvThread.Abort();
                recvThread.Join();
#endif
                sharedMem.Close();          // Release unmanaged resources
                newMsgEvent.Close();
                emptyBoxEvent.Close();

                sharedMem  = null;
                onReceive  = null;
                onDispatch = null;
            }
        }

        /// <summary>
        /// This method implements the background thread that waits for inbound messages.
        /// </summary>
        private unsafe void ReceiveThreadProc()
        {
            byte[]      msg;
            int         cb;
            byte*       p;

            while (!killThread)
            {
                msg = null;
#if WINCE
                newMsgEvent.WaitOne();
                if (killThread)
                    break;
#else
                bool signal;

                signal = newMsgEvent.WaitOne(1000, false);
                if (killThread)
                    break;

                if (!signal)
                    continue;
#endif
                p = sharedMem.Lock();

                cb = *(int*)&p[CurMsgSizeOffset];
                if (0 < cb && cb <= maxMsgSize)
                {
                    msg = new byte[cb];
                    for (int i = 0; i < cb; i++)
                        msg[i] = p[i + MessageOffset];
                }

                sharedMem.Unlock();
                emptyBoxEvent.Set();

                if (msg != null)
                    Helper.UnsafeQueueUserWorkItem(onDispatch, msg);
            }
        }

        /// <summary>
        /// Dispatches the receive notification on a pool thread.
        /// </summary>
        /// <param name="state">The message.</param>
        private void OnDispatch(object state)
        {
            onReceive((byte[])state);
        }
    }
}
