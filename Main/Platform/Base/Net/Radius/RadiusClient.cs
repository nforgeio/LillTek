//-----------------------------------------------------------------------------
// FILE:        RadiusClient.cs
// CONTRIBUTOR: Jeff Lill
// COPYRIGHT:   Copyright (c) 2005-2015 by Jeffrey Lill.  All rights reserved.
// DESCRIPTION: Implements the client side of the RADIUS protocol.

using System;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;

using LillTek.Common;
using LillTek.Net.Sockets;

namespace LillTek.Net.Radius
{
    /// <summary>
    /// Implements the client side of the RADIUS protocol.
    /// </summary>
    /// <remarks>
    /// <para>
    /// This class is pretty easy to use.  Simply instantiate an instance, call one
    /// of the <b>Open()</b> methods and then use <see cref="Authenticate" /> to
    /// initiate synchronous authentication operation or <see cref="BeginAuthenticate" />
    /// and <see cref="EndAuthenticate" /> to initiate an asynchronous operation.
    /// Then call <see cref="Close" /> when the client instance is no longer needed. 
    /// </para>
    /// <para>
    /// The client settings can be initialized programatically by passing a
    /// <see cref="RadiusClientSettings" /> instance to <see cref="Open(RadiusClientSettings)" />
    /// or the client can be initialized from the application configuration by
    /// calling <see cref="Open(string)" />.
    /// </para>
    /// <para>
    /// Note that due to the fact that the RADIUS protocol provides for only an
    /// 8-bit identifier to be used to correlate request and response packets,
    /// it is very possible for this identifier to wrap-around under high
    /// loads.  To avoid this problem, set the <see cref="RadiusClientSettings.PortCount" /> 
    /// value high enough so that create enough source UDP ports are created such
    /// that this is unlikely to happen.
    /// </para>
    /// <para>
    /// At this point, the class implements only the SPAP authentication method.
    /// CHAP or any of the other more advanced methods are not currently supported.
    /// </para>
    /// </remarks>
    /// <threadsafety instance="true" />
    public sealed class RadiusClient : ILockable
    {
        private const string NotOpen = "RADIUS client is not open.";

        private RadiusClientPort[] ports = null;     // The RADUIS client ports
        private GatedTimer bkTimer = null;     // Background task timer
        private int nextPort;           // Index of the next round robin port

        /// <summary>
        /// Constructor.
        /// </summary>
        public RadiusClient()
        {
        }

        /// <summary>
        /// Opens the RADIUS client, initializing it with settings loaded from
        /// the application configuration.
        /// </summary>
        /// <param name="keyPrefix">
        /// The key prefix to use when loading client settings from the 
        /// application configuration.
        /// </param>
        /// <remarks>
        /// <para>
        /// The RADIUS client settings are loaded from the application
        /// configuration, using the specified key prefix.  See 
        /// <see cref="RadiusClientSettings.LoadConfig" /> for a description
        /// of the client application configuration settings.
        /// </para>
        /// <note>
        /// All successful calls to <b>Open()</b> must eventually be matched
        /// with a call to <see cref="Close" /> so that system resources will be 
        /// released promptly.
        /// </note>
        /// </remarks>
        public void Open(string keyPrefix)
        {
            Open(RadiusClientSettings.LoadConfig(keyPrefix));
        }

        /// <summary>
        /// Opens a RADIUS client using the <see cref="RadiusClientSettings" /> passed.
        /// </summary>
        /// <param name="settings">The client settings.</param>
        /// <remarks>
        /// <note>
        /// Note that all successful calls to <b>Open()</b> must eventually be matched
        /// with a call to <see cref="Close" /> so that system resources will be 
        /// released promptly.
        /// </note>
        /// </remarks>
        public void Open(RadiusClientSettings settings)
        {
            using (TimedLock.Lock(this))
            {
                if (IsOpen)
                    throw new RadiusException("RADIUS client is already open.");

                if (settings.PortCount > 1 && settings.NetworkBinding.Port != 0)
                    throw new RadiusException("RADIUS client [NetworkBinding.Port] must be zero if [PortCount] is greater than one.");

                ports = new RadiusClientPort[settings.PortCount];
                for (int i = 0; i < settings.PortCount; i++)
                {
                    ports[i] = new RadiusClientPort();
                    ports[i].Open(settings);
                }

                bkTimer = new GatedTimer(new TimerCallback(OnBkTimer), null, settings.BkTaskInterval, settings.BkTaskInterval);
                nextPort = 0;
            }
        }

        /// <summary>
        /// Closes the client, releasing all resources.
        /// </summary>
        public void Close()
        {
            using (TimedLock.Lock(this))
            {
                if (bkTimer != null)
                {
                    bkTimer.Dispose();
                    bkTimer = null;
                }

                if (ports != null)
                {
                    foreach (RadiusClientPort port in ports)
                        port.Close();

                    ports = null;
                }
            }
        }

        /// <summary>
        /// Returns <c>true</c> if the client is currently open.
        /// </summary>
        public bool IsOpen
        {
            get { return ports != null; }
        }

        /// <summary>
        /// Initiates an asynchronous operation to authenticate user credentials. 
        /// </summary>
        /// <param name="realm">Specifies the authentication scope.</param>
        /// <param name="account">The account.</param>
        /// <param name="password">The password.</param>
        /// <param name="callback">The delegate to be called when the operation completes (or <c>null</c>).</param>
        /// <param name="state">Application defined state (or <c>null</c>).</param>
        /// <returns>The <see cref="IAsyncResult" /> instance to be used to track the operation.</returns>
        /// <remarks>
        /// <note>
        /// All successful calls to <see cref="BeginAuthenticate" /> must eventually be
        /// matched by a call to <see cref="EndAuthenticate" />.
        /// </note>
        /// </remarks>
        public IAsyncResult BeginAuthenticate(string realm, string account, string password, AsyncCallback callback, object state)
        {
            RadiusClientPort port;

            using (TimedLock.Lock(this))
            {
                if (!IsOpen)
                    throw new RadiusException(NotOpen);

                port     = ports[nextPort];
                nextPort = (++nextPort) % ports.Length;

                return port.BeginAuthenticate(realm, account, password, null, port);
            }
        }

        /// <summary>
        /// Completes an asynchronous authentication operation.
        /// </summary>
        /// <param name="ar">The <see cref="IAsyncResult" /> instance returned by <see cref="BeginAuthenticate" />.</param>
        /// <returns><c>true</c> if the credentials were authenticated.</returns>
        /// <exception cref="TimeoutException">Thrown if the retry interval and maximum transmissions have been reached.</exception>
        /// <exception cref="RadiusException">Thrown for RADIUS related problems.</exception>
        public bool EndAuthenticate(IAsyncResult ar)
        {
            RadiusClientPort port;

            port = (RadiusClientPort)ar.AsyncState;
            return port.EndAuthenticate(ar);
        }

        /// <summary>
        /// Performs a synchronous operation to authenticate user credentials.
        /// </summary>
        /// <param name="realm"></param>
        /// <param name="account"></param>
        /// <param name="password"></param>
        /// <returns><c>true</c> if the credentials were authenticated.</returns>
        /// <exception cref="TimeoutException">Thrown if the retry interval and maximum transmissions have been reached.</exception>
        /// <exception cref="RadiusException">Thrown for RADIUS related problems.</exception>
        public bool Authenticate(string realm, string account, string password)
        {
            var ar = BeginAuthenticate(realm, account, password, null, null);

            return EndAuthenticate(ar);
        }

        /// <summary>
        /// Implements background task processing.
        /// </summary>
        /// <param name="o">Not used.</param>
        private void OnBkTimer(object o)
        {
            using (TimedLock.Lock(this))
            {
                if (!IsOpen)
                    return;

                for (int i = 0; i < ports.Length; i++)
                    ports[i].OnBkTask();
            }
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
