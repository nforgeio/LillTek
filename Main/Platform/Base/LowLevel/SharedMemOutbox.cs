//-----------------------------------------------------------------------------
// FILE:        SharedMemOutbox.cs
// CONTRIBUTOR: Jeff Lill
// COPYRIGHT:   Copyright (c) 2005-2015 by Jeffrey Lill.  All rights reserved.
// DESCRIPTION: Defines the SharedMemOutbox class which implements a machanism
//              for transmitting interprocess byte array messages using 
//              shared memory.

using System;
using System.Collections;
using System.Diagnostics;
using System.Threading;

using LillTek.Common;
using LillTek.Windows;

namespace LillTek.LowLevel
{
    /// <summary>
    /// This class implements the send side of a SharedMemInbox/SharedMemOutbox
    /// pair.  Use the <see cref="Send" /> method to send a byte message to any named 
    /// <see cref="SharedMemInbox" /> instance.
    /// </summary>
    public class SharedMemOutbox
    {
        private sealed class InboxRef
        {
            private string                  name;
            private int                     maxMsgSize;
            private SharedMem               inbox;
            private GlobalAutoResetEvent    newMsgEvent;
            private GlobalAutoResetEvent    emptyBoxEvent;

            public InboxRef(string name, int maxMsgSize)
            {
                this.name          = name;
                this.maxMsgSize    = maxMsgSize;
                this.inbox         = null;
                this.emptyBoxEvent = null;
            }

            ~InboxRef()
            {
                Close();
            }

            public void Close()
            {
                lock (syncLock)
                {
                    if (inbox != null)
                    {
                        inbox.Close();
                        if (emptyBoxEvent != null)
                            emptyBoxEvent.Close();
                    }
                }
            }

            private object syncLock = new object();

            /// <summary>
            /// This method transmits the byte message array passed to the inbox.  Note that
            /// you may pass <paramref name="message" /> as <c>null</c>.  In this case, the method will simply detect
            /// whether or not the inbox exists and is able to accept a message transmission.
            /// </summary>
            /// <param name="message">The message array.</param>
            /// <param name="maxWait">The maximum time to wait for the transmission to complete.</param>
            /// <returns><c>true</c> if the operation was successful.</returns>
            public unsafe bool Send(byte[] message, TimeSpan maxWait)
            {
                // See the comment in SharedMemInbox for a description of the
                // shared memory block format.

                byte* p;

                if (message != null && message.Length > maxMsgSize)
                    throw new ArgumentException("Message is too large.", "message");

                lock (syncLock)
                {
                    if (inbox == null)
                    {
                        // Initialize the inbox reference.  Return <c>false</c> if the inbox
                        // shared memory does not exist or if there's no SharedMemInbox
                        // listening.

                        try
                        {
                            inbox = new SharedMem();
                            inbox.Open(name, maxMsgSize, SharedMem.OpenMode.OPEN_ONLY);
                        }
                        catch
                        {
                            inbox = null;
                            return false;
                        }

                        // Here's what the abbreviations mean:
                        //
                        //      LT  = LillTek
                        //      SMI = SharedMemInBox
                        //      NME = NewMessageEvent
                        //      EBE = EmptyBoxEvent

                        newMsgEvent   = new GlobalAutoResetEvent("LT:SMI:NME:" + name);
                        emptyBoxEvent = new GlobalAutoResetEvent("LT:SMI:EBE:" + name);
                    }

                    // Wait for exclusive access to an empty shared memory block and then
                    // send the message.

                    if (emptyBoxEvent == null || !emptyBoxEvent.WaitOne(maxWait, false))
                    {
                        // $todo: I really shouldn't have to close the inbox here but for
                        //        some reason, the emptyBoxEvent is never set in some
                        //        situations (like when a service router starts before the
                        //        zone or machine router on the computer).  At some point, 
                        //        I'd like to come back and investigate why this is happening.

                        inbox.Close();
                        newMsgEvent.Close();
                        emptyBoxEvent.Close();

                        inbox         = null;
                        newMsgEvent   = null;
                        emptyBoxEvent = null;

                        return false;
                    }

                    if (message == null)
                    {
                        emptyBoxEvent.Set();
                        return true;
                    }

                    p = inbox.Lock();
                    try
                    {
                        int cbMax;

                        if (p[SharedMemInbox.InboxListeningOffset] == 0)
                            return false;   // There's no inbox listening

                        cbMax = *(int*)&p[SharedMemInbox.MaxMsgSizeOffset];
                        if (cbMax != maxMsgSize)
                            throw new Exception("SharedMemInbox MaxMsgSize mismatch.");

                        for (int i = 0; i < message.Length; i++)
                            p[SharedMemInbox.MessageOffset + i] = message[i];

                        *(int*)&p[SharedMemInbox.CurMsgSizeOffset] = message.Length;
                        newMsgEvent.Set();
                    }
                    finally
                    {
                        inbox.Unlock();
                    }
                }

                return true;
            }
        }

        // Instance members.

        private object      syncLock = new object();
        private Hashtable   inboxes;        // Hash table of Inbox objects keyed by inbox name
        private int         maxMsgSize;     // Maximum message size allowed
        private TimeSpan    maxWait;        // Maximum interval we will wait for an empty inbox

        /// <summary>
        /// This constructor initializes the outbox.  Call <see cref="Close" /> when the 
        /// outbox is no longer needed to release unmanaged resources in
        /// a timely manner.
        /// </summary>
        /// <param name="maxMsgSize">
        /// The maximum message size that can be transmitted using this class.
        /// This value must be identical to that specified when any destination
        /// inbox referenced by this class was created.
        /// </param>
        /// <param name="maxWait">The maximum time to wait for inboxes to acknowledge the reception of a message.</param>
        public SharedMemOutbox(int maxMsgSize, TimeSpan maxWait)
        {
            Assertion.Test(maxWait.Ticks >= 0, "Invalid timespan.");
            Assertion.Test(maxWait.Ticks / TimeSpan.TicksPerMillisecond <= 0x7FFFFFFF, "Timespan too large.");

            this.inboxes    = new Hashtable();
            this.maxMsgSize = maxMsgSize;
            this.maxWait    = maxWait;
        }

        /// <summary>
        /// Finalizer.
        /// </summary>
        ~SharedMemOutbox()
        {
            Close();
        }

        /// <summary>
        /// This method releases any unmanaged resources associated with the outbox.
        /// Call this method when you're done with an instance to ensure that these
        /// resources are released in a timely manner.  Note that it's OK to call this
        /// method more than once.  This method is threadsafe.
        /// </summary>
        public void Close()
        {
            lock (syncLock)
            {
                if (inboxes == null)
                    return;

                foreach (InboxRef inbox in inboxes.Values)
                    inbox.Close();

                inboxes.Clear();
                inboxes = null;
            }
        }

        /// <summary>
        /// This method returns <c>true</c> if the named inbox exists and is ready to
        /// accept message transmissions.
        /// </summary>
        public bool IsReady(string name)
        {
            InboxRef inbox;

            lock (syncLock)
            {
                inbox = (InboxRef)inboxes[name];
                if (inbox == null)
                {
                    inbox = new InboxRef(name, maxMsgSize);
                    inboxes.Add(name, inbox);
                }
            }

            return inbox.Send(null, maxWait);
        }

        /// <summary>
        /// This method attempts to send the message passed to the named inbox.
        /// Note that there is no guarantee that the message is actually received
        /// by the inbox.  This method is threadsafe.
        /// </summary>
        /// <param name="name">Name of the destination inbox.  The name is case sensitive.</param>
        /// <param name="message">The message.</param>
        /// <returns><c>true</c> if there was an inbox listening for messages.</returns>
        public bool Send(string name, byte[] message)
        {
            InboxRef inbox;

            lock (syncLock)
            {
                inbox = (InboxRef)inboxes[name];
                if (inbox == null)
                {
                    inbox = new InboxRef(name, maxMsgSize);
                    inboxes.Add(name, inbox);
                }
            }

            return inbox.Send(message, maxWait);
        }
    }
}
