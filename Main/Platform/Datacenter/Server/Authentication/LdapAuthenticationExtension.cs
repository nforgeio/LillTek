//-----------------------------------------------------------------------------
// FILE:        LdapAuthenticationExtension.cs
// CONTRIBUTOR: Jeff Lill
// COPYRIGHT:   Copyright (c) 2005-2015 by Jeffrey Lill. All rights reserved.
// DESCRIPTION: Implements an authentication extension that compares 
//              credentials against an LDAP directory.

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.DirectoryServices;
using System.DirectoryServices.Protocols;
using System.Net;
using System.Text;

using LillTek.Advanced;
using LillTek.Common;

// $todo(jeff.lill): 
//
// I haven't been able to get LdapConnection to connect automatically
// to a domain server associated with the machine.  This may be a
// configuration problem or perhaps a .NET bug.  I need to look into
// this further.

// $todo(jeff.lill): 
//
// Implement a mechanism to perform authentication via an LDAP query
// rather than an authenticated bind.  This will be useful for situations
// where users are just normal directory entries.

namespace LillTek.Datacenter.Server
{
    /// <summary>
    /// Implements an <see cref="IAuthenticationExtension" /> that authenticates 
    /// credentials against a LDAP directory.
    /// </summary>
    /// <remarks>
    /// <para>
    /// This authentication extension validates credentials against an LDAP
    /// directory.
    /// </para>
    /// <para>
    /// Use <see cref="Open" /> to initialize the extension with the extension
    /// specific arguments read from a <see cref="RealmMapping" /> instance.
    /// Then call <see cref="Authenticate" /> to authenticate account credentials
    /// against the authentication source.  This method returns a <see cref="AuthenticationResult" />
    /// structure that describes the result of the operation.
    /// </para>
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
    /// <para>
    /// <see cref="Close" /> or <see cref="IDisposable.Dispose" /> should be called promptly
    /// when the extension is no longer needed to release any associated
    /// resources.  Note that if any authentication operations are still outstanding
    /// when either of these methods are called then the implementation must
    /// complete each outstanding request before releasing any shared resources.
    /// </para>
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
    ///     <td>Source Queries/sec (LDAP)</td>
    ///     <td>Rate</td>
    ///     <td>LDAP Authentication Extension authentication queries/sec</td>
    /// </tr>
    /// <tr valign="top">
    ///     <td>Exceptions/sec (LDAP)</td>
    ///     <td>Rate</td>
    ///     <td>LDAP Authentication Extension exceptions/sec</td>
    /// </tr>
    /// </table>
    /// </div>
    /// </remarks>
    /// <threadsafety instance="true" />
    public sealed class LdapAuthenticationExtension : IAuthenticationExtension, ILockable
    {
        //---------------------------------------------------------------------
        // Private classes

        /// <summary>
        /// Holds the performance counters maintained by the service.
        /// </summary>
        private struct Perf
        {
            // Performance counter names

            private const string Queries_Name    = "Source Queries/sec (LDAP)";
            private const string Exceptions_Name = "Exceptions/sec (LDAP)";

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

                perfCounters.Add(new PerfCounter(perfPrefix + Queries_Name, "LDAP authentication queries/sec", PerformanceCounterType.RateOfCountsPerSecond32));
                perfCounters.Add(new PerfCounter(perfPrefix + Exceptions_Name, "LDAP exceptions/sec", PerformanceCounterType.RateOfCountsPerSecond32));
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

        private string[]    servers = null;
        private int         cAuthentications = 0;
        private bool        isAD;
        private AuthType    authType;
        private bool        dnsHosts;
        private bool        udp;
        private TimeSpan    maxCacheTime;
        private Perf        perf;

        /// <summary>
        /// Constructor.
        /// </summary>
        public LdapAuthenticationExtension()
        {
        }

        /// <summary>
        /// Establishes a session with the authentication extension.
        /// </summary>
        /// <param name="args">The extension specific arguments (see the remarks).</param>
        /// <param name="query">Not used.</param>
        /// <param name="perfCounters">The application's performance counter set (or <c>null</c>).</param>
        /// <param name="perfPrefix">The string to prefix any performance counter names (or <c>null</c>).</param>
        /// <remarks>
        /// <para>
        /// This extension recognises the following arguments:
        /// </para>
        /// <list type="table">
        ///     <item>
        ///         <term>IsAD</term>
        ///         <description>
        ///         Set this to "true" or "yes" if the directory server is Microsoft Active Directory.
        ///         This argument defaults to "true".
        ///         </description>
        ///     </item>
        ///     <item>
        ///         <term>AuthType</term>
        ///         <description>
        ///         Specifies the authentication method.  The possible values are <b>Basic</b>, <b>Digest</b>,
        ///         <b>DPA</b>, <b>External</b>, <b>Kerberos</b>, <b>MSN</b>, <b>Negotiate</b>, <b>NTLM</b>,
        ///         or <b>Sicily</b>.  This argument defaults to "Digest".
        ///         </description>
        ///     </item>
        ///     <item>
        ///         <term>Servers</term>
        ///         <description>
        ///         Specifies zero or more LDAP server host  names or IP addresses separated by commas.
        ///         This argument defaults an empty list.  <b>Note that due to an unresolved bug with
        ///         default Active Directory support, at least one LDAP server must be explicitly
        ///         specified in the current build.</b>
        ///         </description>
        ///     </item>
        ///     <item>
        ///         <term>DNSHost</term>
        ///         <description>
        ///         Set this to "true" or "yes" if the host names specified by <b>Servers</b> should be
        ///         interpreted as fully qualified DNS host names.  If set to "no" or "false", the
        ///         host names can be interpreted as an IP address or a DNS host name and if no host
        ///         is specified, then an Active Directory server associated with the computer account
        ///         will be used.  This argument defaults to "false".
        ///         </description>
        ///     </item>
        ///     <item>
        ///         <term>UDP</term>
        ///         <description>
        ///         Set this to "true" or "yes" to enable UDP the connection over UDP, rather than over TCP/IP.
        ///         This argument defaults to "false".
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
        /// <para>
        /// The defaults values for the arguments were selected so that ommiting all arguments
        /// will configure the extension so that it authenticates against the Active Directory
        /// domain the computer belongs to.  There are some problems with some combinations of
        /// parameters.
        /// </para>
        /// <note>
        /// All calls to <see cref="Open" /> must be matched with a call
        /// to <see cref="Close" /> or <see cref="Dispose" />.
        /// </note>
        /// </remarks>
        public void Open(ArgCollection args, string query, PerfCounterSet perfCounters, string perfPrefix)
        {
            using (TimedLock.Lock(this))
            {
                if (IsOpen)
                    throw new AuthenticationException("Authentication extension is already open.");

                perf = new Perf(perfCounters, perfPrefix);

                // Load the arguments

                isAD = args.Get("IsAD", true);

                switch (args.Get("AuthType", "Digest").ToUpper())
                {
                    case "BASIC":       authType = AuthType.Basic; break;
                    case "DIGEST":      authType = AuthType.Digest; break;
                    case "DPA":         authType = AuthType.Dpa; break;
                    case "EXTERNAL":    authType = AuthType.External; break;
                    case "KERBEROS":    authType = AuthType.Kerberos; break;
                    case "MSN":         authType = AuthType.Msn; break;
                    case "NEGOTIATE":   authType = AuthType.Negotiate; break;
                    case "NTLM":        authType = AuthType.Ntlm; break;
                    case "SICILY":      authType = AuthType.Sicily; break;

                    default: throw new AuthenticationException("Unexpected authentication type argument [{0}].", args.Get("AuthType", "Negotiate"));
                }

                servers = args.Get("Servers", string.Empty).Split(new char[] { ',' }, StringSplitOptions.RemoveEmptyEntries);
                for (int i = 0; i < servers.Length; i++)
                    servers[i] = servers[i].Trim();

                dnsHosts     = args.Get("DNSHost", false);
                udp          = args.Get("UDP", false);
                maxCacheTime = args.Get("MaxCacheTime", TimeSpan.FromMinutes(5));
            }
        }

        /// <summary>
        /// Releases all resources associated with the extension instance.
        /// </summary>
        public void Close()
        {
            using (TimedLock.Lock(this))
            {
                servers = null;
            }
        }

        /// <summary>
        /// Returns <c>true</c> if the extension is currently open.
        /// </summary>
        public bool IsOpen
        {
            get { return servers != null; }
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
            LdapConnection          con = null;
            LdapDirectoryIdentifier identifier;
            AuthType                _authType;

            using (TimedLock.Lock(this))
            {
                if (!IsOpen)
                    throw new AuthenticationException("Authentication extension is not open.");

                cAuthentications++;

                if (servers.Length == 0)
                    identifier = new LdapDirectoryIdentifier((string)null, dnsHosts, udp);
                else
                    identifier = new LdapDirectoryIdentifier(servers, dnsHosts, udp);

                _authType = authType;
            }

            try
            {
                perf.Queries.Increment();

                // Connect to the server and authenticate the account.

                con = new LdapConnection(identifier, new NetworkCredential(account, password, realm), _authType);
                con.Bind();

                // $todo(jeff.lill): 
                //
                // At some point come back and verify that the realm passed
                // actually matches the DN of the directory root.
            }
            catch (LdapException e)
            {
                if (e.ErrorCode == (int)LdapError.INVALID_CREDENTIALS)
                    return new AuthenticationResult(AuthenticationStatus.AccessDenied, maxCacheTime);

                perf.Exceptions.Increment();
                throw new AuthenticationException(e);
            }
            catch (Exception e)
            {
                perf.Exceptions.Increment();
                throw new AuthenticationException(e);
            }
            finally
            {
                if (con != null)
                    con.Dispose();
            }

            return new AuthenticationResult(AuthenticationStatus.Authenticated, maxCacheTime);
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
