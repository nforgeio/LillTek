//-----------------------------------------------------------------------------
// FILE:        FileAuthenticationExtension.cs
// CONTRIBUTOR: Jeff Lill
// COPYRIGHT:   Copyright (c) 2005-2015 by Jeffrey Lill. All rights reserved.
// DESCRIPTION: Implements a simple authentication extension that compares 
//              credentials against those stored in a file.

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;

using LillTek.Advanced;
using LillTek.Common;

namespace LillTek.Datacenter.Server
{
    /// <summary>
    /// Implements a simple <see cref="IAuthenticationExtension" /> that compares credentials against 
    /// those stored in a file.  This extension is intended only for tesing purposes
    /// since it is inherently insecure due to the lack of any encryption.
    /// </summary>
    /// <remarks>
    /// <para>
    /// This authentication extension validates credentials against a flat
    /// ANSI encoded text file.  The format for this file is simply the list of 
    /// accounts, one per line, with the account fields separated by semicolons.  
    /// The required fields are <b>Realm</b>, <b>Account</b>, and <b>Password</b> 
    /// in that order.
    /// </para>
    /// <para>
    /// Empty lines are ignored and lines beginning with "//" are ignored
    /// as comments.  Here's an example of a valid credential file:
    /// </para>
    /// <code language="none">
    ///     // Test Credentials
    /// 
    ///     lilltek.com;jeff.lill;foobar
    ///     lilltek.com;joe.blow;little.debbie
    ///     lilltek.com;jane.doe;fancy.pants
    /// 
    ///     // Customer Test Credentials
    /// 
    ///     amex.com;jane.doe@amex.com;password.123
    /// </code>
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
    ///     <td>Source Queries/sec (File)</td>
    ///     <td>Rate</td>
    ///     <td>FILE Authentication Extension authentication queries/sec</td>
    /// </tr>
    /// <tr valign="top">
    ///     <td>Exceptions/sec (File)</td>
    ///     <td>Rate</td>
    ///     <td>FILE Authentication Extension exceptions/sec</td>
    /// </tr>
    /// </table>
    /// </div>
    /// </remarks>
    /// <threadsafety instance="true" />
    public sealed class FileAuthenticationExtension : IAuthenticationExtension, ILockable
    {
        //---------------------------------------------------------------------
        // Private classes

        /// <summary>
        /// Holds the performance counters maintained by the service.
        /// </summary>
        private struct Perf
        {
            // Performance counter names

            private const string Queries_Name    = "Source Queries/sec (File)";
            private const string Exceptions_Name = "Exceptions/sec (File)";

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

                perfCounters.Add(new PerfCounter(perfPrefix + Queries_Name, "File authentication queries/sec", PerformanceCounterType.RateOfCountsPerSecond32));
                perfCounters.Add(new PerfCounter(perfPrefix + Exceptions_Name, "File exceptions/sec", PerformanceCounterType.RateOfCountsPerSecond32));
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

        private string                      path;
        private bool                        reload;
        private TimeSpan                    maxCacheTime;
        private Dictionary<string, string>  credentials;
        private int                         cAuthentications = 0;
        private Perf                        perf;

        /// <summary>
        /// Constructor.
        /// </summary>
        public FileAuthenticationExtension()
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
        ///         <term>Path</term>
        ///         <description>
        ///         The fully qualified path to the credential file.  This argument is required.
        ///         </description>
        ///     </item>
        ///     <item>
        ///         <term>Reload</term>
        ///         <description>
        ///         Indicates whether the credentials file should be reloaded before every authentication
        ///         attempt.  This can have values such as "yes"/"no" or "true"/"false".  This argument
        ///         defaults to "true".
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
            using (TimedLock.Lock(this))
            {
                if (IsOpen)
                    throw new AuthenticationException("Authentication extension is already open.");

                perf         = new Perf(perfCounters, perfPrefix);
                path         = EnvironmentVars.Expand(args["Path"]);
                reload       = args.Get("Reload", true);
                maxCacheTime = args.Get("MaxCacheTime", TimeSpan.FromMinutes(5));

                if (!reload)
                    credentials = LoadCredentials();
            }
        }

        /// <summary>
        /// Releases all resources associated with the extension instance.
        /// </summary>
        public void Close()
        {
            using (TimedLock.Lock(this))
            {
                path         = null;
                reload       = false;
                maxCacheTime = TimeSpan.Zero;
                credentials  = null;
            }
        }

        /// <summary>
        /// Returns <c>true</c> if the extension is currently open.
        /// </summary>
        public bool IsOpen
        {
            get { return path != null; }
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
        /// Loads the credentials from the credential file into a case insensitive
        /// hash table keyed by <b>&lt;realm&gt;:&lt;account&gt;</b> and where the value 
        /// is the associated password.  Duplicate accounts will be ignored.
        /// </summary>
        /// <returns>The credential hash table.</returns>
        private Dictionary<string, string> LoadCredentials()
        {
            Dictionary<string, string>  credentials = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            StreamReader                reader;
            string                      line;
            string[]                    fields;
            int                         lineNum;
            string                      key;

            reader = new StreamReader(path, Helper.AnsiEncoding);
            try
            {
                lineNum = 0;
                while (true)
                {
                    line = reader.ReadLine();
                    if (line == null)
                        break;

                    lineNum++;

                    line = line.Trim();
                    if (line.Length == 0 || line.StartsWith("//"))
                        continue;   // Ignore blank and comment lines

                    fields = line.Split(new char[] { ';' }, StringSplitOptions.None);
                    if (fields.Length != 3)
                        throw new AuthenticationException("Invalid account credentials at [{0}:{1}].", Path.GetFullPath(path), lineNum);

                    key = fields[0].Trim() + ":" + fields[1].Trim();
                    if (!credentials.ContainsKey(key))
                        credentials.Add(key, fields[2].Trim());
                }
            }
            finally
            {
                reader.Close();
            }

            return credentials;
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
            Dictionary<string, string>  accounts;
            string                      pwd;

            using (TimedLock.Lock(this))
            {
                if (!IsOpen)
                    throw new AuthenticationException("Authentication extension is closed.");

                perf.Queries.Increment();
                cAuthentications++;

                try
                {
                    if (reload)
                        accounts = LoadCredentials();
                    else
                        accounts = credentials;
                }
                catch (Exception e)
                {
                    perf.Exceptions.Increment();
                    throw new AuthenticationException(e);
                }
            }

            if (!accounts.TryGetValue(realm + ":" + account, out pwd) || pwd != password)
                return new AuthenticationResult(AuthenticationStatus.AccessDenied, maxCacheTime);
            else
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
