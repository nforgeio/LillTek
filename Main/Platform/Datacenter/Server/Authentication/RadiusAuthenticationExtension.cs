//-----------------------------------------------------------------------------
// FILE:        RadiusAuthenticationExtension.cs
// CONTRIBUTOR: Jeff Lill
// COPYRIGHT:   Copyright (c) 2005-2015 by Jeffrey Lill. All rights reserved.
// DESCRIPTION: Implements a authentication extension that supports
//              the RADIUS protocol.

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Net;

using LillTek.Advanced;
using LillTek.Common;
using LillTek.Net.Radius;

// $todo(jeff.lill): Implement this

namespace LillTek.Datacenter.Server
{
    /// <summary>
    /// Implements an <see cref="IAuthenticationExtension" /> that supports
    /// the RADIUS protocol to authenticate against a RADIUS server.
    /// </summary>
    /// <remarks>
    /// <para><b><u>Performance Counters</u></b></para>
    /// <para>
    /// The class can be configured to expose performance counters.  Call the
    /// static <see cref="InstallPerfCounters" /> method to add the class performance
    /// counters to a <see cref="PerfCounterSet" /> during application installation
    /// and then pass a set instance to the <see cref="Open" /> method.
    /// </para>
    /// <para>
    /// The class exposes the following performance counters:
    /// </para>
    /// <div class="tablediv">
    /// <table class="dtTABLE" cellspacing="0" ID="Table1">
    /// <tr valign="top">
    /// <th width="1">Name</th>        
    /// <th width="1">Type</th>
    /// <th width="90%">Description</th>
    /// </tr>
    /// <tr valign="top">
    ///     <td>Source Queries/sec (RADIUS)</td>
    ///     <td>Rate</td>
    ///     <td>RADIUS Authentication Extension authentication queries/sec</td>
    /// </tr>
    /// <tr valign="top">
    ///     <td>Exceptions/sec (RADIUS)</td>
    ///     <td>Rate</td>
    ///     <td>RADIUS Authentication Extension exceptions/sec</td>
    /// </tr>
    /// </table>
    /// </div>
    /// </remarks>
    /// <threadsafety instance="true" />
    public sealed class RadiusAuthenticationExtension : IAuthenticationExtension, ILockable
    {
        //---------------------------------------------------------------------
        // Private classes

        /// <summary>
        /// Holds the performance counters maintained by the service.
        /// </summary>
        private struct Perf
        {

            // Performance counter names

            private const string Queries_Name    = "Source Queries/sec (RADIUS)";
            private const string Exceptions_Name = "Exceptions/sec (RADIUS)";

            /// <summary>
            /// Installs the service's performance counters by adding them to the
            /// performance counter set passed.
            /// </summary>
            /// <param name="perfCounters">The application's performance counter set (or <c>null</c>).</param>
            /// <param name="perfPrefix">The string to prefix any performance counter names (or <c>null</c>).</param>
            public static void Install(PerfCounterSet perfCounters, string perfPrefix)
            {
                if (perfCounters == null)
                    return;

                if (perfPrefix == null)
                    perfPrefix = string.Empty;

                perfCounters.Add(new PerfCounter(perfPrefix + Queries_Name, "RADIUS authentication queries/sec", PerformanceCounterType.RateOfCountsPerSecond32));
                perfCounters.Add(new PerfCounter(perfPrefix + Exceptions_Name, "RADIUS exceptions/sec", PerformanceCounterType.RateOfCountsPerSecond32));
            }

            //-----------------------------------------------------------------

            public PerfCounter Queries;                // Queries/sec
            public PerfCounter Exceptions;             // Exceptions/sec

            /// <summary>
            /// Initializes the service's performance counters from the performance
            /// counter set passed.
            /// </summary>
            /// <param name="perfCounters">The application's performance counter set (or <c>null</c>).</param>
            /// <param name="perfPrefix">The string to prefix any performance counter names (or <c>null</c>).</param>
            public Perf(PerfCounterSet perfCounters, string perfPrefix)
            {
                Install(perfCounters, perfPrefix);

                if (perfPrefix == null)
                    perfPrefix = string.Empty;

                if (perfCounters != null)
                {
                    Queries    = perfCounters[perfPrefix + Queries_Name];
                    Exceptions = perfCounters[perfPrefix + Exceptions_Name];
                }
                else
                {
                    Queries    =
                    Exceptions = PerfCounter.Stub;
                }
            }
        }

        //---------------------------------------------------------------------
        // Static members

        /// <summary>
        /// Adds the performance counters managed by the class to the performance counter
        /// set passed (if not null).  This will be called during the application installation
        /// process when performance counters are being installed.
        /// </summary>
        /// <param name="perfCounters">The application's performance counter set (or <c>null</c>).</param>
        /// <param name="perfPrefix">The string to prefix any performance counter names (or <c>null</c>).</param>
        public static void InstallPerfCounters(PerfCounterSet perfCounters, string perfPrefix)
        {
            Perf.Install(perfCounters, perfPrefix);
        }

        //---------------------------------------------------------------------
        // Instance members

        private RadiusClient    radiusClient     = new RadiusClient();
        private int             cAuthentications = 0;
        private TimeSpan        maxCacheTime;
        private Perf            perf;

        /// <summary>
        /// Constructor.
        /// </summary>
        public RadiusAuthenticationExtension()
        {
        }

        /// <summary>
        /// Establishes a session with the authentication extension.
        /// </summary>
        /// <param name="args">The extension specific arguments (see the remarks).</param>
        /// <param name="query">Ignored for this extension.</param>
        /// <param name="perfCounters">The application's performance counter set (or <c>null</c>).</param>
        /// <param name="perfPrefix">The string to prefix any performance counter names (or <c>null</c>).</param>
        /// <remarks>
        /// <para>
        /// This extension recognises the following arguments:
        /// </para>
        /// <list type="table">
        ///     <item>
        ///         <term>Servers</term>
        ///         <description>
        ///         Specifies the list of RADIUS server network bindings specifying the
        ///         IP address or host name of the server as well as the port number
        ///         or well-known port name.  Each server binding is formatted as 
        ///         described by <see cref="NetworkBinding.Parse" /> and the server
        ///         bindings are separated by commas.  This argument must be present.
        ///         </description>
        ///     </item>
        ///     <item>
        ///         <term>Secret</term>
        ///         <description>
        ///         The shared secret to be used to secure RADIUS packets delivered
        ///         between this client and the any of the RADIUS servers.  This
        ///         string may include any valid characters besides semi-colons.
        ///         This argument must be present.
        ///         </description>
        ///     </item>
        ///     <item>
        ///         <term>SocketBuffer</term>
        ///         <description>
        ///         Byte size of the client socket's send and receive buffers.  Default
        ///         value is 32K.
        ///         </description>
        ///     </item>
        ///     <item>
        ///         <term>NetworkBinding</term>
        ///         <description>
        ///         <para>
        ///         Specifies the IP address of the network card the client is
        ///         and port bindings.  Use an IP address of ANY to bind to 
        ///         all network interfaces.  ANY is suitable for single homed machines.
        ///         Machines that are actually connected to multiple networks should 
        ///         specify a specific network binding here to ensure that the NAS-IP-Address
        ///         of RADIUS authentication packets are initialized properly.
        ///         </para>
        ///         <para>
        ///         A specific port number may be selected or 0 can be specified,
        ///         indicating that the operating system should select a free port.
        ///         </para>
        ///         <para>
        ///         Default value is ANY:0.
        ///         </para>
        ///         </description>
        ///     </item>
        ///     <item>
        ///         <term>PortCount</term>
        ///         <description>
        ///         The number of RADIUS client UDP ports to open.  Multiple ports may
        ///         be required under high authentication loads.  Default value is 4.
        ///         </description>
        ///     </item>
        ///     <item>
        ///         <term>RetryInterval</term>
        ///         <description>
        ///         Maximum time to wait for a response packet from a RADIUS before retransmitting
        ///         an authentication request.  Default is 5 seconds.
        ///         </description>
        ///     </item>
        ///     <item>
        ///         <term></term>
        ///         <description>
        ///         </description>
        ///     </item>
        ///     <item>
        ///         <term>BkTaskInterval</term>
        ///         <description>
        ///         The interval at which background tasks such as retransmitting
        ///         a request should be processed.  Default is 1 second.
        ///         </description>
        ///     </item>
        ///     <item>
        ///         <term>MaxTransmissions</term>
        ///         <description>
        ///         The maximum number of authentication transmission attempts before aborting 
        ///         with an authentication with a timeout.  Default is 4.
        ///         </description>
        ///     </item>
        ///     <item>
        ///         <term>RealmFormat</term>
        ///         <description>
        ///         Specifies how user names are to be generated from the
        ///         <b>realm</b> and <b>account</b> components.  See 
        ///         <see cref="RealmFormat" /> for more information.
        ///         The possible values are: <b>Slash</b> and <b>Email</b>.
        ///         Default is <b>Email</b>.
        ///         </description>
        ///     </item>
        ///     <item>
        ///         <term>MaxCacheTime</term>
        ///         <description>
        ///         Specifies the maximum time clients should retain authentication information.
        ///         This is expressed in the same format as timespans parsed by <see cref="Config.Parse(string,TimeSpan)" />.
        ///         This argument defaults to "5m".
        ///         </description>
        ///     </item>
        ///     <item>
        ///         <term>LockoutCount</term>
        ///         <description>
        ///         Specifies the limiting failed authentication count.  Accounts
        ///         will be locked when the fail count reaches this number.
        ///         </description>
        ///     </item>
        ///     <item>
        ///         <term>LockoutThreshold</term>
        ///         <description>
        ///         The period of time that can elapse between failed authentication 
        ///         attempts where the failed attempts will <b>not</b> be counted against the
        ///         <b>LockoutCount</b>.  Set this to <see cref="TimeSpan.Zero" />
        ///         to disable account lockout for the realm.
        ///         </description>
        ///     </item>
        ///     <item>
        ///         <term>LockoutTime</term>
        ///         <description>
        ///         The period of time an account will remain locked after being locked
        ///         out due to too many failed authentication attempts. 
        ///         </description>
        ///     </item>
        /// </list>
        /// <note>
        /// All calls to <see cref="Open" /> must be matched with a call
        /// to <see cref="Close" /> or <see cref="Dispose" />.
        /// </note>
        /// </remarks>
        public void Open(ArgCollection args, string query, PerfCounterSet perfCounters, string perfPrefix)
        {
            RadiusClientSettings    settings;
            string[]                rawBindings;
            List<NetworkBinding>    bindings;
            string                  secret;

            using (TimedLock.Lock(this))
            {
                if (IsOpen)
                    throw new AuthenticationException("Authentication extension is already open.");

                perf = new Perf(perfCounters, perfPrefix);

                rawBindings = args.Get("servers", string.Empty).Split(new char[] { ',' }, StringSplitOptions.RemoveEmptyEntries);
                if (rawBindings.Length == 0)
                    throw new AuthenticationException("RADUIS authentication extension requires at least one server network binding.");

                bindings = new List<NetworkBinding>(rawBindings.Length);
                for (int i = 0; i < rawBindings.Length; i++)
                    bindings.Add(NetworkBinding.Parse(rawBindings[i]));

                secret = args.Get("secret", (string)null);
                if (secret == null || secret.Length == 0)
                    throw new AuthenticationException("RADIUS authentication extension requires a shared NAS secret.");

                settings                  = new RadiusClientSettings(bindings.ToArray(), secret);
                settings.SocketBuffer     = args.Get("SocketBuffer", settings.SocketBuffer);
                settings.NetworkBinding   = args.Get("NetworkBinding", settings.NetworkBinding);
                settings.PortCount        = args.Get("PortCount", settings.PortCount);
                settings.RetryInterval    = args.Get("RetryInterval", settings.RetryInterval);
                settings.BkTaskInterval   = args.Get("BkTaskInterval", settings.BkTaskInterval);
                settings.MaxTransmissions = args.Get("settings.MaxTransmissions", settings.MaxTransmissions);
                settings.RealmFormat      = args.Get<RealmFormat>("RealmFormat", settings.RealmFormat);

                maxCacheTime              = args.Get("MaxCacheTime", TimeSpan.FromMinutes(5));

                radiusClient.Open(settings);
            }
        }

        /// <summary>
        /// Releases all resources associated with the extension instance.
        /// </summary>
        public void Close()
        {
            using (TimedLock.Lock(this))
            {
                radiusClient.Close();
            }
        }

        /// <summary>
        /// Returns <c>true</c> if the extension is currently open.
        /// </summary>
        public bool IsOpen
        {
            get { return radiusClient.IsOpen; }
        }

        /// <summary>
        /// Returns the number of authentications attempted against the
        /// extension.  This is useful for unit testing.
        /// </summary>
        public int AuthenticationCount
        {
            get { return cAuthentications; }
        }

        /// <summary>
        /// Releases all resources associated with the extension instance.
        /// </summary>
        public void Dispose()
        {
            Close();
        }

        /// <summary>
        /// Authenticates the account credentials against the authentication extension.
        /// </summary>
        /// <param name="realm">The authentication realm.</param>
        /// <param name="account">The account ID.</param>
        /// <param name="password">The password.</param>
        /// <returns>A <see cref="AuthenticationResult" /> instance with the result of the operation.</returns>
        /// <remarks>
        /// <para>
        /// The <see cref="AuthenticationResult.Status" /> property indicates the disposition
        /// of the authentication operation.  Extensions will return <see cref="AuthenticationStatus.Authenticated" />
        /// if the operation was successful.  Authentication failures due to the 
        /// sumbission of invalid credentials will be indicated by returning one of 
        /// the error codes.  Extensions may return specific error codes such as
        /// <see cref="AuthenticationStatus.BadPassword" /> and <see cref="AuthenticationStatus.BadAccount" />
        /// or the generic error code <see cref="AuthenticationStatus.AccessDenied" />.
        /// </para>
        /// <para>
        /// The <see cref="AuthenticationResult.MaxCacheTime" /> returns as the maximum time the
        /// results of the authentication operation should be cached.
        /// </para>
        /// </remarks>
        /// <exception cref="AuthenticationException">Thrown for authentication related exception.</exception>
        public AuthenticationResult Authenticate(string realm, string account, string password)
        {
            bool success;

            using (TimedLock.Lock(this))
            {
                if (!IsOpen)
                    throw new AuthenticationException("Authentication extension is closed.");

                try
                {
                    perf.Queries.Increment();
                    cAuthentications++;
                    success = radiusClient.Authenticate(realm, account, password);
                }
                catch (Exception e)
                {
                    perf.Exceptions.Increment();
                    throw new AuthenticationException(e);
                }

                if (success)
                    return new AuthenticationResult(AuthenticationStatus.Authenticated, maxCacheTime);
                else
                    return new AuthenticationResult(AuthenticationStatus.AccessDenied, maxCacheTime);
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
