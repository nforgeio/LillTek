//-----------------------------------------------------------------------------
// FILE:        OdbcAuthenticationExtension.cs
// CONTRIBUTOR: Jeff Lill
// COPYRIGHT:   Copyright (c) 2005-2015 by Jeffrey Lill. All rights reserved.
// DESCRIPTION: Implements an authentication extension that authenticates 
//              credentials against an ODBC data source.

using System;
using System.Collections.Generic;
using System.Data;
using System.Data.Odbc;
using System.Diagnostics;
using System.Text;

using LillTek.Advanced;
using LillTek.Common;
using LillTek.Cryptography;
using LillTek.Data;

namespace LillTek.Datacenter.Server
{
    /// <summary>
    /// Implements an <see cref="IAuthenticationExtension" /> that authenticates 
    /// credentials against an ODBC data source.
    /// </summary>
    /// <remarks>
    /// <para>
    /// This authentication extension validates credentials against an ODBC
    /// data source.  The extension is intialized with the database connection
    /// string and a query template.
    /// </para>
    /// <para>
    /// Use <see cref="Open" /> to initialize the extension with the extension
    /// specific arguments read from a <see cref="RealmMapping" /> instance.
    /// Then call <see cref="Authenticate" /> to authenticate account credentials
    /// against the authentication source.  This method returns a <see cref="AuthenticationResult" />
    /// structure that describes the result of the operation.
    /// </para>
    /// <para>
    /// The query template is ODBC command text that can include one or
    /// more of the case insensitive macro identifiers:
    /// </para>
    /// <list type="table">
    ///     <item>
    ///         <term><b>$(realm)</b></term>
    ///         <description>
    ///         This macro will be replaced with the realm as a literal string.
    ///         </description>
    ///     </item>
    ///     <item>
    ///         <term><b>$(account)</b></term>
    ///         <description>
    ///         This macro will be replaced with the account as a literal string.
    ///         </description>
    ///     </item>
    ///     <item>
    ///         <term><b>$(password)</b></term>
    ///         <description>
    ///         This macro will be replaced with the password as a literal string.
    ///         </description>
    ///     </item>
    ///     <item>
    ///         <term><b>$(md5-password)</b></term>
    ///         <description>
    ///         This macro will be replaced with the MD5 hash of the UTF-8 encoded
    ///         password as a literal binary value.
    ///         </description>
    ///     </item>
    ///     <item>
    ///         <term><b>$(sha1-password)</b></term>
    ///         <description>
    ///         This macro will be replaced with the SHA1 hash of the UTF-8 encoded
    ///         password as a literal binary value.
    ///         </description>
    ///     </item>
    ///     <item>
    ///         <term><b>$(sha256-password)</b></term>
    ///         <description>
    ///         This macro will be replaced with the SHA256 hash of the UTF-8 encoded
    ///         password as a literal binary value.
    ///         </description>
    ///     </item>
    ///     <item>
    ///         <term><b>$(sha512-password)</b></term>
    ///         <description>
    ///         This macro will be replaced with the SHA512 hash of the UTF-8 encoded
    ///         password as a literal binary value.
    ///         </description>
    ///     </item>
    /// </list>
    /// <para>
    /// The <see cref="Authenticate" /> method works by
    /// converting the credential parameters into T-SQL literals and then replacing the 
    /// corresponding macro in the template with the literal value and the executing
    /// the query on the data.  The query must return a scalar integer value in the first
    /// column of the first row of the result set.  The value indicates the result of the 
    /// authentication attempt.  The possible return values are:
    /// </para>
    /// <list type="table">
    ///     <item>
    ///         <term>0</term>
    ///         <description><b>Authenticated:</b> The credentials were authenticated successfully.</description>
    ///     </item>
    ///     <item>
    ///         <term>1</term>
    ///         <description><b>Access Denied:</b> Authentication was denied for an unspecified reason.</description>
    ///     </item>
    ///     <item>
    ///         <term>2</term>
    ///         <description><b>Bad Realm:</b> The realm specified does not exist.</description>
    ///     </item>
    ///     <item>
    ///         <term>3</term>
    ///         <description><b>Bad Account:</b> The account specified does not exist.</description>
    ///     </item>
    ///     <item>
    ///         <term>4</term>
    ///         <description><b>Bad Password:</b> The password is not valid.</description>
    ///     </item>
    ///     <item>
    ///         <term>5</term>
    ///         <description><b>Account Disabled:</b> The account is disabled.</description>
    ///     </item>
    ///     <item>
    ///         <term>6</term>
    ///         <description><b>Account Locked:</b> The account is temporarily locked due to excessive unsuccessful authentication attempts.</description>
    ///     </item>
    ///     <item>
    ///         <term>7</term>
    ///         <description><b>Bad Request:</b> The authentication request is not valid.</description>
    ///     </item>
    ///     <item>
    ///         <term>8</term>
    ///         <description><b>Server Error:</b> The server encountered an error while processing the request.</description>
    ///     </item>
    /// </list>
    /// <para>
    /// Note that the database can also return an empty result set.  This will be
    /// interpreted by the extension as <b>Access Denied</b>.
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
    ///     <td>Source Queries/sec (ODBC)</td>
    ///     <td>Rate</td>
    ///     <td>ODBC AuthenticationExtension authentication queries/sec</td>
    /// </tr>
    /// <tr valign="top">
    ///     <td>Exceptions/sec (ODBC)</td>
    ///     <td>Rate</td>
    ///     <td>ODBC AuthenticationExtension exceptions/sec</td>
    /// </tr>
    /// </table>
    /// </div>
    /// </remarks>
    /// <threadsafety instance="true" />
    public sealed class OdbcAuthenticationExtension : IAuthenticationExtension, ILockable
    {
        //---------------------------------------------------------------------
        // Private classes

        /// <summary>
        /// Holds the performance counters maintained by the service.
        /// </summary>
        private struct Perf
        {
            // Performance counter names

            private const string Queries_Name = "Source Queries/sec (ODBC)";
            private const string Exceptions_Name = "Exceptions/sec (ODBC)";

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

                perfCounters.Add(new PerfCounter(perfPrefix + Queries_Name, "ODBC authentication queries/sec", PerformanceCounterType.RateOfCountsPerSecond32));
                perfCounters.Add(new PerfCounter(perfPrefix + Exceptions_Name, "ODBC exceptions/sec", PerformanceCounterType.RateOfCountsPerSecond32));
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

        private string      conString        = null;
        private string      queryTemplate    = null;
        private int         cAuthentications = 0;
        private TimeSpan    maxCacheTime;
        private Perf        perf;

        /// <summary>
        /// Constructor.
        /// </summary>
        public OdbcAuthenticationExtension()
        {
        }

        /// <summary>
        /// Establishes a session with the authentication extension.
        /// </summary>
        /// <param name="args">The extension specific arguments (see the remarks).</param>
        /// <param name="query">The database query template (see the remarks).</param>
        /// <param name="perfCounters">The application's performance counter set (or <c>null</c>).</param>
        /// <param name="perfPrefix">The string to prefix any performance counter names (or <c>null</c>).</param>
        /// <remarks>
        /// <para>
        /// This extension recognises the following arguments:
        /// </para>
        /// <list type="table">
        ///     <item>
        ///         <term>Connection String</term>
        ///         <description>
        ///         All of the arguments that are not explicitly described below will be
        ///         collected together as the ODBC connection string.
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
            var sb = new StringBuilder(128);

            using (TimedLock.Lock(this))
            {
                if (IsOpen)
                    throw new AuthenticationException("Authentication extension is already open.");

                perf          = new Perf(perfCounters, perfPrefix);
                queryTemplate = query;

                // Process the built-in arguments

                maxCacheTime = args.Get("MaxCacheTime", TimeSpan.FromMinutes(5));

                // Build the connection string

                foreach (string key in args)
                {
                    switch (key.ToLowerInvariant())
                    {

                        case "maxcachetime":
                        case "LockoutCount":
                        case "LockoutThreshold":
                        case "LockoutTime":

                            break;      // Ignore the built-in arguments.

                        default:

                            sb.AppendFormat("{0}={1};", key, args[key]);
                            break;
                    }
                }

                conString = sb.ToString();
            }
        }

        /// <summary>
        /// Releases all resources associated with the extension instance.
        /// </summary>
        public void Close()
        {
            using (TimedLock.Lock(this))
            {
                conString = null;
                queryTemplate = null;
            }
        }

        /// <summary>
        /// Returns <c>true</c> if the extension is currently open.
        /// </summary>
        public bool IsOpen
        {
            get { return conString != null; }
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
            OdbcConnection      dbCon;
            OdbcCommand         cmd;
            OdbcDataReader      reader = null;
            MacroProcessor      processor;
            string              _conString;
            string              query;
            int                 authCode;

            using (TimedLock.Lock(this))
            {
                if (!IsOpen)
                    throw new AuthenticationException("Authentication extension is closed.");

                cAuthentications++;
                _conString = conString;

                // Substitute the credentials into the query template.

                processor = new MacroProcessor();
                processor.Add("realm", SqlHelper.Literal(realm));
                processor.Add("account", SqlHelper.Literal(account));
                processor.Add("password", SqlHelper.Literal(password));
                processor.Add("md5-password", SqlHelper.Literal(MD5Hasher.Compute(password)));
                processor.Add("sha1-password", SqlHelper.Literal(SHA1Hasher.Compute(password)));
                processor.Add("sha256-password", SqlHelper.Literal(SHA256Hasher.Compute(password)));
                processor.Add("sha512-password", SqlHelper.Literal(SHA512Hasher.Compute(password)));
                query = processor.Expand(queryTemplate);
            }

            // Perform the query.

            dbCon = new OdbcConnection(_conString);
            dbCon.Open();

            try
            {
                cmd             = dbCon.CreateCommand();
                cmd.CommandText = query;
                cmd.CommandType = CommandType.Text;

                perf.Queries.Increment();
                reader = cmd.ExecuteReader();
                if (!reader.Read())
                    authCode = (int)AuthenticationStatus.AccessDenied; // Empty result set
                else
                {
                    object      o    = reader[0];
                    Type        type = o.GetType();

                    if (type == typeof(byte))
                        authCode = (int)(byte)o;
                    else if (type == typeof(int))
                        authCode = (int)o;
                    else if (type == typeof(long))
                        authCode = (int)(long)o;
                    else
                        throw new AuthenticationException("ODBC authenticate query returned a [{0}] instead of the expected [integer].", type.Name);

                    if (authCode < 0 || authCode > 5)
                        throw new AuthenticationException("ODBC authenticate query returned the invalid return code [{0}]. Valid codes range from 0..5", authCode);
                }
            }
            catch (Exception e)
            {
                perf.Exceptions.Increment();
                throw new AuthenticationException(e);
            }
            finally
            {
                if (reader != null)
                    reader.Close();

                dbCon.Close();
            }

            return new AuthenticationResult((AuthenticationStatus)authCode, maxCacheTime);
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
