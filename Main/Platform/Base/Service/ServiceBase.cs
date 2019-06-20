//-----------------------------------------------------------------------------
// FILE:        ServiceBase.cs
// CONTRIBUTOR: Jeff Lill
// COPYRIGHT:   Copyright (c) 2005-2015 by Jeffrey Lill.  All rights reserved.
// DESCRIPTION: Base class for services that want enable control by
//              ServiceControl instance.

using System;

using LillTek.Common;
using LillTek.LowLevel;

namespace LillTek.Service
{
    /// <summary>
    /// Base class for services that want enable control by ServiceControl 
    /// instance.
    /// </summary>
    /// <remarks>
    /// <note>
    /// Derived classes must implement <see cref="IService" />
    /// </note>
    /// <para>
    /// For this to work properly, the derived class has to call
    /// <see cref="ServiceBase.Open" /> when the service is instantiated and 
    /// <see cref="ServiceBase.Close" /> when the service is stopped.  That's
    /// pretty much all there is to it.
    /// </para>
    /// <para>
    /// The derived class's <see cref="IService.Start" />, <see cref="IService.Stop" />, 
    /// <see cref="IService.Shutdown" />, <see cref="IService.Configure" />, 
    /// and <see cref="IService.State" /> members will be called by the base class as 
    /// commanded by ServiceControl instances.
    /// </para>
    /// </remarks>
    public class ServiceBase : ILockable
    {
        private IService            service;    // The derived service instance
        private SharedMemInbox      inbox;      // Accepts ServiceControl commands
        private SharedMemOutbox     outbox;     // Delivers the responses

        /// <summary>
        /// Default constructor.
        /// </summary>
        public ServiceBase()
        {
            this.service = (IService)this;
            this.inbox   = null;
            this.outbox  = null;
        }

        /// <summary>
        /// Makes sure that the shared memory resources are released
        /// if <see cref="Close" /> wasn't called properly.
        /// </summary>
        ~ServiceBase()
        {
            Close();
        }

        /// <summary>
        /// Readies the class to accept ServiceControl commands.
        /// </summary>
        /// <remarks>
        /// This must be called when the derived class is instantiated.
        /// </remarks>
        public void Open()
        {
            using (TimedLock.Lock(this))
            {
                inbox = new SharedMemInbox();
                inbox.Open(ServiceControl.ServiceMemPrefix + service.Name, ServiceControl.MaxMsgSize,
                           new SharedMemInboxReceiveDelegate(OnReceive));

                outbox = new SharedMemOutbox(ServiceControl.MaxMsgSize, ServiceControl.MaxWaitTime);
            }
        }

        /// <summary>
        /// Releases and resources associated with the ServiceControl
        /// infrastructure.
        /// </summary>
        /// <remarks>
        /// This must be called when the derived class is stopped.
        /// </remarks>
        public void Close()
        {
            using (TimedLock.Lock(this))
            {
                if (inbox != null)
                {
                    inbox.Close();
                    inbox = null;
                }

                if (outbox != null)
                {
                    outbox.Close();
                    outbox = null;
                }
            }
        }

        /// <summary>
        /// Sends a reply to the query passed.
        /// </summary>
        /// <param name="query">The query message received.</param>
        /// <param name="ack">The reply.</param>
        private void ReplyTo(ServiceMsg query, ServiceMsg ack)
        {
            ack.RefID = query.RefID;
            outbox.Send(query["Reply-To"], ack.ToBytes());
        }

        /// <summary>
        /// Handles messages received from the shared memory inbox.
        /// </summary>
        /// <param name="raw">The raw message.</param>
        private void OnReceive(byte[] raw)
        {
            ServiceMsg query;
            ServiceMsg ack = new ServiceMsg("Ack");

            try
            {
                query = new ServiceMsg(raw);
                switch (query.Command)
                {
                    case "GetStatus":

                        ack["Status"] = service.State.ToString();
                        break;

                    case "Stop":
                    case "Shutdown":

                        // $todo(jeff.lill): 
                        //
                        // For now, I'm going to treat Stop() and Shutdown() the 
                        // same by forcing the service to stop immediately.

                        // This should never be called for services running in Native mode
                        // since the ServiceControl instance should have used the native
                        // Windows control mechanisms.  For Forms and Console modes, we're
                        // going to call the Service's Stop() method and then terminate the 
                        // current process.

                        service.Stop();
                        Helper.Exit();
                        break;

                    case "Configure":

                        service.Configure();
                        break;

                    case "HeartBeat":

                        ack["Alive"] = OnHeartBeat() ? "1" : "0";
                        break;

                    default:

                        Assertion.Fail("Unexpected ServiceMsg command [{0}].", query.Command);
                        break;
                }

                ReplyTo(query, ack);
            }
            catch (Exception e)
            {

                SysLog.LogException(e);
            }
        }

        /// <summary>
        /// Called when the service receives a HeartBeat query from a ServiceControl
        /// instance.  The method should return <c>true</c> if the service is still alive.
        /// </summary>
        /// <returns><c>true</c> if the service is still alive.</returns>
        /// <remarks>
        /// The base implementation returns <c>true</c> which indicates that the service
        /// is still capable of sending and receiving shared memory messages.  
        /// Services may override this to implement more a sophisticated health check.
        /// </remarks>
        protected virtual bool OnHeartBeat()
        {
            return true;
        }

        /// <summary>
        /// Returns <c>true</c> if the service actually implements <see cref="IService.Configure" />.
        /// </summary>
        /// <remarks>
        /// The <see cref="ServiceBase" /> class implementation of this method 
        /// always returns <c>false</c>.  Services that actually implement <see cref="IService.Configure" />
        /// should override this property and return <c>true</c>.
        /// </remarks>
        public virtual bool IsConfigureImplemented
        {
            get { return false; }
        }

        //---------------------------------------------------------------------
        // ILockable implementation

        private object lockKey = TimedLock.AllocLockKey();

        /// <summary>
        /// Used by <see cref="TimedLock" /> to provide better deadlock
        /// diagnostic information.
        /// </summary>
        /// <returns>The process unique lock key for this instance.</returns>
        public object GetLockKey()
        {
            return lockKey;
        }
    }
}
