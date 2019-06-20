//-----------------------------------------------------------------------------
// FILE:        AuthenticationEngine.cs
// CONTRIBUTOR: Jeff Lill
// COPYRIGHT:   Copyright (c) 2005-2015 by Jeffrey Lill. All rights reserved.
// DESCRIPTION: Performs authentications against mapped realms and
//              authentication extensions.

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Threading;

using LillTek.Advanced;
using LillTek.Common;

// $todo(jeff.lill): Look into implementing an async version of the Authenticate() method.

// $todo(jeff.lill): 
//
// This implementation is somewhat susceptible to compromising passwords
// if memory is dumped to disk during a fatal error.  I need to look
// into using the SecureString class.

namespace LillTek.Datacenter.Server
{
    /// <summary>
    /// Used by <see cref="AuthenticationEngine.AuthenticatedAccountEvent" /> to signal when
    /// an account has been authenticated by an authentication source.
    /// </summary>
    /// <param name="realm">The authentication realm.</param>
    /// <param name="account">The account.</param>
    /// <param name="password">The password.</param>
    /// <param name="ttl">The time-to-live to use when caching these credentials.</param>
    public delegate void AccountAuthenticatedDelegate(string realm, string account, string password, TimeSpan ttl);

    /// <summary>
    /// Used by <see cref="AuthenticationEngine.AccountLockStatusEvent" /> to signal when an account's
    /// lock status changes.
    /// </summary>
    /// <param name="realm">The authentication realm.</param>
    /// <param name="account">The locked account.</param>
    /// <param name="locked">Indicates whether the account has just been locked or unlocked.</param>
    /// <param name="lockTTL">The time a locked account should remain locked.</param>
    public delegate void AccountLockStatusDelegate(string realm, string account, bool locked, TimeSpan lockTTL);

    /// <summary>
    /// Performs authentications against mapped realms and authentication extensions.
    /// </summary>
    /// <remarks>
    /// <para>
    /// This class is designed to be used as the core of an authentication
    /// service.  After instantiating an instance, call <see cref="Start" /> 
    /// to start the engine.  This method accepts a <see cref="AuthenticationEngineSettings" /> instance
    /// with the configuration settings.  Once the engine is started, <see cref="Authenticate" />
    /// can be called to authenticate account credentials.  A call to <see cref="Stop" /> stops
    /// the engine, releasing all resources.
    /// </para>
    /// <para>
    /// Account credentials consist of a <b>realm</b>, <b>account</b> name, and <b>password</b>.
    /// Realm is a string that identifies the authentication source capable of authenticating the
    /// account.  The account and password are also strings.
    /// </para>
    /// <para>
    /// The engine works by periodically querying an <see cref="IRealmMapProvider" /> for the mapping
    /// of known realms to <see cref="IAuthenticationExtension" /> instances.  The built-in 
    /// <see cref="OdbcRealmMapProvider" />, <see cref="FileRealmMapProvider" /> and 
    /// <see cref="ConfigRealmMapProvider" /> classes provide for loading realm mappings from a 
    /// database or the application configuration.  Custom mapping sources can be enabled by creating 
    /// a class that implements <see cref="IRealmMapProvider" />.  Use <see cref="LoadRealmMap" /> to 
    /// schedule an immediate reloading of the realm map.  
    /// </para>
    /// <para>
    /// The realm map associates realm names with <see cref="IAuthenticationExtension" /> instances.
    /// Several built-in classes are available for common authentication scenerios.
    /// </para>
    /// <list type="table">
    ///     <item>
    ///         <term><see cref="FileAuthenticationExtension" /></term>
    ///         <description>Authenticates credentials against a text file.</description>
    ///     </item>
    ///     <item>
    ///         <term><see cref="ConfigAuthenticationExtension" /></term>
    ///         <description>Authenticates credentials against a credentials in the application's configuration.</description>
    ///     </item>
    ///     <item>
    ///         <term><see cref="LdapAuthenticationExtension" /></term>
    ///         <description>Authenticates against a LDAP Directory (including Microsoft Active Directory).</description>
    ///     </item>
    ///     <item>
    ///         <term><see cref="OdbcAuthenticationExtension" /></term>
    ///         <description>Authentications against an ODBC data source.</description>
    ///     </item>
    ///     <item>
    ///         <term><see cref="RadiusAuthenticationExtension" /></term>
    ///         <description>Authenticates against a RADIUS server.</description>
    ///     </item>
    ///     <item>
    ///         <term>Custom</term>
    ///         <description>Custom authentication sources can be supported by implementing <see cref="IAuthenticationExtension" />.</description>
    ///     </item>
    /// </list>
    /// <para>
    /// Call <see cref="Authenticate" /> to to authenticate user credentials, passing the
    /// realm, account, and password strings.  This method returns an <see cref="AuthenticationResult" />
    /// instance which indicates whether the attempt succeeded or not.
    /// </para>
    /// <para>
    /// The engine can be configured to cache successful as well as unsuccessful authentication 
    /// attempts to improve performance as well as to reduce the load on authentication
    /// sources.  When authentication of a set of credentials succeeds, the engine
    /// caches the credentials for a period of time and subsequent authentication attempts
    /// with the same credentials will succeed as long as the credentials are cached.
    /// When authentication fails, the bad credentials will be saved in a NAK cache
    /// for a period of time, and subsequent authentications with the same set of
    /// credentials will fail as long as the NAK cache entry exists.
    /// </para>
    /// <para>
    /// Caching behavior can be configured via a <see cref="AuthenticationEngineSettings" />
    /// instance passed to <see cref="Start" />.  This class also provides the 
    /// <see cref="ClearCache()" /> and <see cref="ClearNakCache()" /> methods to
    /// remove all items from a cache and the <see cref="FlushCache(string,string)" />
    /// and <see cref="FlushNakCache(string,string)" /> methods to flush specific
    /// cached items.  Use <see cref="AddCredentials" /> to explicitly add a set of
    /// credentials to the success cache.
    /// </para>
    /// <para>
    /// The engine can be configured to log successful authentications, failed
    /// authentications, or both by setting the <see cref="AuthenticationEngineSettings.LogAuthSuccess" />
    /// and <see cref="AuthenticationEngineSettings.LogAuthFailure" /> properties
    /// in the settings instance passed to <see cref="Start" />.
    /// </para>
    /// <para>
    /// The engine implements an account lockout feature that records the number of
    /// failed authentication attempts for a specific realm/account and then locks
    /// access to the account for a period of time after the number of attempts
    /// exceeds a limit.  The lockout time and failed attempt limit values are
    /// maintained individually for each authentication realm.  These properties
    /// default to the values specified by <see cref="AuthenticationEngineSettings.LockoutCount" />,
    /// <see cref="AuthenticationEngineSettings.LockoutThreshold" /> and
    /// <see cref="AuthenticationEngineSettings.LockoutTime" /> but may be overridden
    /// for individual realms via corresponding realm arguments.  The ultimate values
    /// for each realm are saved in the <see cref="RealmMapping.LockoutCount" />, 
    /// <see cref="RealmMapping.LockoutThreshold" />, and <see cref="RealmMapping.LockoutTime" /> 
    /// properties.
    /// </para>
    /// <para><b><u>Performance Counters</u></b></para>
    /// <para>
    /// The class can be configured to expose performance counters.  Call the
    /// static <see cref="InstallPerfCounters" /> method to add the class performance
    /// counters to a <see cref="PerfCounterSet" /> during application installation
    /// and then pass a set instance to the <see cref="Start" /> method.
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
    ///     <td>Source Queries/sec (Total)</td>
    ///     <td>Rate</td>
    ///     <td>Total Authentication source queries/sec</td>
    /// </tr>
    /// <tr valign="top">
    ///     <td>Auths/sec (Total)</td>
    ///     <td>Rate</td>
    ///     <td>Total authentication requests/sec</td>
    /// </tr>
    /// <tr valign="top">
    ///     <td>Auths/sec (Success)</td>
    ///     <td></td>
    ///     <td>Successful authentications/sec</td>
    /// </tr>
    /// <tr valign="top">
    ///     <td>Auths/sec (Fail)</td>
    ///     <td>Rate</td>
    ///     <td>Failed authentications/sec</td>
    /// </tr>
    /// <tr valign="top">
    ///     <td>Exceptions/sec</td>
    ///     <td>Rate</td>
    ///     <td>Exceptions/sec</td>
    /// </tr>
    /// <tr valign="top">
    ///     <td>Cache Size</td>
    ///     <td>Count</td>
    ///     <td>Number of cached authentications</td>
    /// </tr>
    /// <tr valign="top">
    ///     <td>Cache Utilization (%)</td>
    ///     <td>Percent</td>
    ///     <td>Number of cached authentications vs capacity</td>
    /// </tr>
    /// <tr valign="top">
    ///     <td>Cache Hit Ratio (%)</td>
    ///     <td>Percent</td>
    ///     <td>Cache hits vs total requests</td>
    /// </tr>
    /// </table>
    /// </div>
    /// </remarks>
    /// <threadsafety instance="true" />
    public class AuthenticationEngine
    {
        //---------------------------------------------------------------------
        // Private classes

        /// <summary>
        /// Used for caching information about successful authentications.
        /// </summary>
        private sealed class AuthState
        {
            public string       Password;
            public TimeSpan     MaxCacheTime;

            public AuthState(string password, TimeSpan maxCacheTime)
            {
                this.Password     = password;
                this.MaxCacheTime = maxCacheTime;
            }
        }

        /// <summary>
        /// Used to maintain the status of a locked account or failed
        /// authentication attempts.
        /// </summary>
        private sealed class LockoutState : IDisposable
        {
            public readonly AuthenticationEngine    Engine;             // The engine
            public readonly string                  Realm;              // The realm
            public readonly string                  Account;            // The account

            public AuthenticationStatus             Status;             // The authentication status to be returned to the client
            public int                              FailCount;          // # of failed authentications
            public int                              LockoutCount;       // Lockout count for this realm
            public TimeSpan                         LockoutThreshold;   // Lockout threshold for this realm
            public TimeSpan                         LockoutTime;        // Lockout time for this realm
            public bool                             IsLocked;           // True if the account is locked
            public TimeSpan                         TTL;                // Duration the state is to remain cached
            public Dictionary<string, bool>         Passwords;          // Hash table of passwords already tried

            /// <summary>
            /// Constructor.  Note that this constructor assumes that a <see cref="TimedLock" /> is
            /// already held on the engine instance.
            /// </summary>
            public LockoutState(AuthenticationEngine engine, RealmMapping realmMapping,
                                string realm, string account, string password, AuthenticationResult result)
            {
                DateTime now = SysTime.Now;

                this.Engine           = engine;
                this.Realm            = realm;
                this.Account          = account;
                this.Status           = result.Status;
                this.FailCount        = 1;
                this.LockoutCount     = realmMapping.LockoutCount;
                this.LockoutThreshold = realmMapping.LockoutThreshold;
                this.LockoutTime      = realmMapping.LockoutTime;
                this.Passwords        = new Dictionary<string, bool>();
                this.IsLocked         = false;
                this.TTL              = Helper.Min(realmMapping.LockoutThreshold, engine.settings.NakCacheTTL);

                this.Passwords.Add(password, true);

                // We might need to lock the account right off

                if (this.LockoutThreshold > TimeSpan.Zero && this.LockoutCount <= 1)
                {
                    this.IsLocked = true;
                    this.Status   = AuthenticationStatus.AccountLocked;
                    this.TTL      = Helper.Min(this.LockoutTime, engine.settings.NakCacheTTL);
                }
            }

            /// <summary>
            /// Returns <c>true</c> if the password passed is one of the bad passwords already cached.  Assumes that 
            /// a <see cref="TimedLock" /> is already held on the engine instance.
            /// </summary>
            public bool IsKnownBadPassword(string password)
            {
                return this.Passwords.ContainsKey(password);
            }

            /// <summary>
            /// Adds a password to the set of known bad passwords.
            /// </summary>
            public void AddBadPassword(string password)
            {
                if (!this.Passwords.ContainsKey(password))
                    this.Passwords.Add(password, true);
            }

            /// <summary>
            /// Call this on subsequent failed authentication attempts to determine whether an account needs 
            /// to be locked.  Assumes that a <see cref="TimedLock" /> is already held on the engine instance.
            /// </summary>
            public bool LockIfRequired(AuthenticationEngine engine)
            {
                this.FailCount++;
                if (this.LockoutThreshold > TimeSpan.Zero && this.FailCount >= this.LockoutCount)
                {
                    this.IsLocked = true;
                    this.Status   = AuthenticationStatus.AccountLocked;
                    this.TTL      = Helper.Min(this.LockoutTime, engine.settings.NakCacheTTL);

                    return true;
                }
                else
                    return false;
            }

            /// <summary>
            /// I'm going to use this as my opportunity to signal when locked accounts
            /// are finally flushed from the cache and become unlocked.
            /// </summary>
            public void Dispose()
            {
                if (this.Status == AuthenticationStatus.AccountLocked)
                    this.Engine.OnAccountUnlock(this);
            }
        }

        /// <summary>
        /// Holds the performance counters maintained by the service.
        /// </summary>
        private struct Perf
        {
            // Performance counter names

            private const string ExtensionAuths_Name = "Source Queries/sec (Total)";
            private const string TotalAuths_Name = "Auths/sec (Total)";
            private const string SuccessAuths_Name = "Auths/sec (Success)";
            private const string FailAuths_Name = "Auths/sec (Fail)";
            private const string Exceptions_Name = "Exceptions/sec";
            private const string CacheSize_Name = "Cache Size";
            private const string CacheUtilization_Name = "Cache Utilization (%)";
            private const string CacheHitRatio_Name = "Cache Hit Ratio (%)";

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

                perfCounters.Add(new PerfCounter(perfPrefix + ExtensionAuths_Name, "Authentication source queries/sec", PerformanceCounterType.RateOfCountsPerSecond32));
                perfCounters.Add(new PerfCounter(perfPrefix + TotalAuths_Name, "Total authentication requests/sec", PerformanceCounterType.RateOfCountsPerSecond32));
                perfCounters.Add(new PerfCounter(perfPrefix + SuccessAuths_Name, "Successful authentications/sec", PerformanceCounterType.RateOfCountsPerSecond32));
                perfCounters.Add(new PerfCounter(perfPrefix + FailAuths_Name, "Failed authentications/sec", PerformanceCounterType.RateOfCountsPerSecond32));
                perfCounters.Add(new PerfCounter(perfPrefix + Exceptions_Name, "Exceptions/sec", PerformanceCounterType.RateOfCountsPerSecond32));
                perfCounters.Add(new PerfCounter(perfPrefix + CacheSize_Name, "Number of cached authentications", PerformanceCounterType.NumberOfItems32));
                perfCounters.Add(new PerfCounter(perfPrefix + CacheUtilization_Name, "Number of cached authentications vs capacity", PerformanceCounterType.NumberOfItems32));
                perfCounters.Add(new PerfCounter(perfPrefix + CacheHitRatio_Name, "Cache hits vs total requests", PerformanceCounterType.NumberOfItems32));
            }

            //-----------------------------------------------------------------

            public PerfCounter ExtensionAuths;         // Calls to all authentication extensions/sec
            public PerfCounter TotalAuths;             // Total authentications/sec
            public PerfCounter SuccessAuths;           // Successful authentications/sec
            public PerfCounter FailAuths;              // Failed authentications/sec
            public PerfCounter Exceptions;             // Exceptions/sec
            public PerfCounter CacheSize;              // # cached auths
            public PerfCounter CacheUtilization;       // % cached auths vs capacity
            public PerfCounter CacheHitRatio;          // % cache hits vs requests

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
                    ExtensionAuths   = perfCounters[perfPrefix + ExtensionAuths_Name];
                    TotalAuths       = perfCounters[perfPrefix + TotalAuths_Name];
                    SuccessAuths     = perfCounters[perfPrefix + SuccessAuths_Name];
                    FailAuths        = perfCounters[perfPrefix + FailAuths_Name];
                    Exceptions       = perfCounters[perfPrefix + Exceptions_Name];
                    CacheSize        = perfCounters[perfPrefix + CacheSize_Name];
                    CacheUtilization = perfCounters[perfPrefix + CacheUtilization_Name];
                    CacheHitRatio    = perfCounters[perfPrefix + CacheHitRatio_Name];
                }
                else
                {
                    ExtensionAuths   =
                    TotalAuths       =
                    SuccessAuths     =
                    FailAuths        =
                    Exceptions       =
                    CacheSize        =
                    CacheUtilization =
                    CacheHitRatio    = PerfCounter.Stub;
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
        // Instance Members

        private const string NotRunningMsg       = "Authentication Engine is not running";
        private const string AccountNowLockedMsg = "-ACCOUNT IS BEING LOCKED";

        private object                                  syncLock;               // The thread sync instance
        private GatedTimer                              bkTimer;                // The background timer
        private bool                                    isRunning;              // True if the engine is running
        private AuthenticationEngineSettings            settings;               // The engine settings
        private IRealmMapProvider                       realmMapProvider;       // The realm map provider
        private Dictionary<string, RealmMapping>        realmMap;               // The current realm map
        private TimedLRUCache<string, AuthState>        cache;                  // Cached good authentications
        private TimedLRUCache<string, LockoutState>     nakCache;               // Cached authentication failures
        private TimeSpan                                cacheTTL;               // Maximum TTL for cached auths
        private TimeSpan                                mapLoadInterval;        // Interval to reload the realm map
        private DateTime                                mapLoadTime;            // Next scheduled map load time (SYS)
        private TimeSpan                                cacheFlushInterval;     // Interval to flush the auth cache
        private DateTime                                cacheFlushTime;         // Next scheduled auth cache flush time (SYS)
        private bool                                    logAuthSuccess;         // Log successful authentication attempts
        private bool                                    logAuthFailure;         // Log failed authentication attempts
        private Perf                                    perf;                   // Performance counters
        private int                                     cTotalAuths;            // Total authentication count (used for cache hit stats).
        private bool                                    lockReportEnabled;      // True to report unlocked accounts

        /// <summary>
        /// Raised when the when an account's lock status has been changed.
        /// </summary>
        /// <remarks>
        /// <note>
        /// A lock will be held on the engine instance when this call
        /// is made so be very careful not to create a deadlock situation
        /// when handling this event.
        /// </note>
        /// </remarks>
        public event AccountLockStatusDelegate AccountLockStatusEvent;

        /// <summary>
        /// Raised when an account has been authenticated by an authentication source.
        /// </summary>
        public event AccountAuthenticatedDelegate AuthenticatedAccountEvent;

        // Implementation Note:
        // --------------------
        // The authentication result caches are keyed by the case insenstive
        // combination of the realm and account name formatted as:
        //
        //      <realm> "/" <account>

        /// <summary>
        /// Constructor.
        /// </summary>
        /// <param name="syncLock">The object to be used for thread synchronization (or <c>null</c> to set this instance).</param>
        public AuthenticationEngine(object syncLock)
        {
            this.syncLock  = syncLock != null ? syncLock : this;
            this.isRunning = false;
            this.bkTimer   = null;
            this.cache     = null;
            this.nakCache  = null;
        }

        /// <summary>
        /// Starts the engine using the settings passed.
        /// </summary>
        /// <param name="realmMapProvider">The realm mapper instance.</param>
        /// <param name="settings">The authentication engine settings.</param>
        /// <param name="perfCounters">The application's performance counter set (or <c>null</c>).</param>
        /// <param name="perfPrefix">The string to prefix any performance counter names (or <c>null</c>).</param>
        /// <remarks>
        /// <para>
        /// The settings passed include the <see cref="IRealmMapProvider" /> instance to
        /// be used to obtain realm/<see cref="IAuthenticationExtension" /> pairs along
        /// with other engine settings.
        /// </para>
        /// <para>
        /// Applications that expose performance counters will pass a non-<c>null</c> <b>perfCounters</b>
        /// instance.  The service handler should add any counters it implements to this set.
        /// If <paramref name="perfPrefix" /> is not <c>null</c> then any counters added should prefix their
        /// names with this parameter.
        /// </para>
        /// <note>
        /// This method blocks until the first realm map is loaded. 
        /// </note>
        /// </remarks>
        /// <exception cref="AuthenticationException">The engine has already been started.</exception>
        public void Start(IRealmMapProvider realmMapProvider, AuthenticationEngineSettings settings,
                          PerfCounterSet perfCounters, string perfPrefix)
        {
            using (TimedLock.Lock(syncLock))
            {
                if (IsRunning)
                    throw new AuthenticationException("Authentication Engine has already been started.");

                this.settings = settings;

                if (settings.CacheTTL > TimeSpan.Zero && settings.MaxCacheSize > 0)
                {
                    cache          = new TimedLRUCache<string, AuthState>(StringComparer.OrdinalIgnoreCase);
                    cache.MaxItems = settings.MaxCacheSize;
                }

                if (settings.NakCacheTTL > TimeSpan.Zero && settings.MaxNakCacheSize > 0)
                {
                    nakCache             = new TimedLRUCache<string, LockoutState>(StringComparer.OrdinalIgnoreCase);
                    nakCache.MaxItems    = settings.MaxNakCacheSize;
                    nakCache.AutoDispose = true;    // Set this so LockoutState.Dispose() will be able to signal
                                                    // when accounts are unlocked
                }

                this.realmMapProvider   = realmMapProvider;
                this.mapLoadInterval    = settings.RealmMapLoadInterval;
                this.cacheTTL           = settings.CacheTTL;
                this.cacheFlushInterval = settings.CacheFlushInterval;
                this.logAuthSuccess     = settings.LogAuthSuccess;
                this.logAuthFailure     = settings.LogAuthFailure;
                this.isRunning          = true;
                this.perf               = new Perf(perfCounters, perfPrefix);
                this.cTotalAuths        = 0;
                this.lockReportEnabled  = true;

                LoadRealmMap();

                this.mapLoadTime    = SysTime.Now + mapLoadInterval;
                this.cacheFlushTime = SysTime.Now + cacheFlushInterval;
                this.bkTimer        = new GatedTimer(new TimerCallback(OnBkTask), null, settings.BkTaskInterval, settings.BkTaskInterval);
            }
        }

        /// <summary>
        /// Stops the engine if it is running.
        /// </summary>
        /// <remarks>
        /// It is not an error to call this method if the engine is not currently running.
        /// </remarks>
        public void Stop()
        {
            using (TimedLock.Lock(syncLock))
            {

                if (IsRunning)
                {
                    if (bkTimer != null)
                    {

                        bkTimer.Dispose();
                        bkTimer = null;
                    }

                    realmMapProvider.Close();

                    cache            = null;
                    nakCache         = null;
                    realmMap         = null;
                    realmMapProvider = null;
                }
            }
        }

        /// <summary>
        /// Returns <c>true</c> if the engine is currently running.
        /// </summary>
        private bool IsRunning
        {
            get { return isRunning; }
        }

        /// <summary>
        /// Returns the engine's thread synchronization object.
        /// </summary>
        public object SyncRoot
        {
            get { return syncLock; }
        }

        /// <summary>
        /// Returns the engine settings.  The instance values must be considered to be
        /// read-only.
        /// </summary>
        /// <exception cref="AuthenticationException">Thrown if the engine isn't running.</exception>
        public AuthenticationEngineSettings Settings
        {
            get
            {
                using (TimedLock.Lock(syncLock))
                {
                    if (!IsRunning)
                        throw new AuthenticationException(NotRunningMsg);

                    return Settings;
                }
            }
        }

        /// <summary>
        /// Returns the hash table key for a realm/account combination.
        /// </summary>
        /// <param name="realm">The realm.</param>
        /// <param name="account">The account.</param>
        /// <returns>&lt;realm&gt; + "/" + &lt;account&gt;</returns>
        private static string GetAccountKey(string realm, string account)
        {
            return realm + "/" + account;
        }

        /// <summary>
        /// Adds an authentication event to the security log (if logging is enabled).
        /// </summary>
        /// <param name="success"><c>true</c> if the authentication was successful.</param>
        /// <param name="realm">The authentication realm.</param>
        /// <param name="account">The account.</param>
        /// <param name="message">The log message.</param>
        public void LogSecurityEvent(bool success, string realm, string account, string message)
        {
            if (success)
            {
                if (this.logAuthSuccess)
                    SysLog.LogSecuritySuccess("{0}/{1}: {2}", realm, account, message);
            }
            else
            {
                if (this.logAuthFailure)
                    SysLog.LogSecurityFailure("{0}/{1}: {2}", realm, account, message);
            }
        }

        /// <summary>
        /// Increments the failed authentication attempt count for a specified
        /// account.  This can be used to implement account lockout functionality
        /// across multiple authentication service instances.
        /// </summary>
        /// <param name="realm">The authentication realm.</param>
        /// <param name="account">The account.</param>
        public void IncrementFailCount(string realm, string account)
        {
            string                  accountKey = GetAccountKey(realm, account);
            RealmMapping            realmMapping;
            LockoutState            nakInfo;
            AuthenticationResult    result;
            bool                    justLocked = false;

            using (TimedLock.Lock(syncLock))
            {
                if (nakCache == null)
                    return;

                if (!realmMap.TryGetValue(realm, out realmMapping))
                    return;     // Don't bother if the realm isn't valid

                if (nakCache.TryGetValue(accountKey, out nakInfo))
                {
                    if (nakInfo.LockIfRequired(this) && AccountLockStatusEvent != null)
                        justLocked = true;
                }
                else
                {
                    result = new AuthenticationResult(AuthenticationStatus.AccessDenied, settings.NakCacheTTL);
                    nakCache.Add(accountKey, new LockoutState(this, realmMapping, realm, account, "", result));
                }
            }

            if (justLocked)
                AccountLockStatusEvent(realm, account, true, nakInfo.TTL);
        }

        /// <summary>
        /// Called when a locked account is being removed from the NAK cache.
        /// </summary>
        /// <param name="nakInfo">The lockout information.</param>
        private void OnAccountUnlock(LockoutState nakInfo)
        {
            using (TimedLock.Lock(syncLock))
            {
                if (this.AccountLockStatusEvent != null && lockReportEnabled)
                    this.AccountLockStatusEvent(nakInfo.Realm, nakInfo.Account, false, TimeSpan.Zero);
            }
        }

        /// <summary>
        /// This method is designed to temporarily enable/disable account lock state
        /// reporting via <see cref="AccountLockStatusEvent" /> while calling one
        /// or more of the cache flush methods.  The engine will be locked through 
        /// out the entire operation the engine's <see cref="SyncRoot" /> instance.
        /// </summary>
        /// <param name="timedLock">A <see cref="TimedLock" /> instance or <c>null</c> (see the note below).</param>
        /// <param name="enable"><c>true</c> to enable reporting.</param>
        /// <remarks>
        /// <para>
        /// This is a pretty specialized method.  The lock will be acquired when
        /// reporting is disabled and released when reporting is reenabled.
        /// Code using this must be patterned after this example:
        /// </para>
        /// <code language="cs">
        /// TimedLock   timedLock;
        /// 
        /// timedLock = engine.SetLockReportEnable(timedLock,false);
        /// engine.FlushCache(realm,account);
        /// engine.FlushNakCache(realm,account);
        /// engine.SetLockReportEnable(timedLock,true);
        /// </code>
        /// </remarks>
        public TimedLock SetLockReportEnable(TimedLock timedLock, bool enable)
        {
            if (enable)
            {
                lockReportEnabled = true;
                timedLock.Dispose();
                return timedLock;
            }
            else
            {
                TimedLock lk;

                lk = TimedLock.Lock(syncLock);
                lockReportEnabled = false;
                return lk;
            }
        }

        /// <summary>
        /// Explicitly locks an account for the specified time.
        /// </summary>
        /// <param name="realm">The authentication realm.</param>
        /// <param name="account">The account.</param>
        /// <param name="lockTTL">The lock duration.</param>
        public void LockAccount(string realm, string account, TimeSpan lockTTL)
        {
            string accountKey = GetAccountKey(realm, account);

            using (TimedLock.Lock(syncLock))
            {
                // Remove the account from the success cache

                if (cache != null)
                    cache.Remove(accountKey);

                // Add a lockout record to the NAK cache

                if (nakCache == null)
                    return;

                AuthenticationResult    result;
                RealmMapping            realmMapping;
                LockoutState            nakInfo;

                if (!realmMap.TryGetValue(realm, out realmMapping))
                    return;     // Don't bother if there's no realm mapping

                if (nakCache.TryGetValue(accountKey, out nakInfo))
                {
                    nakInfo.Status   = AuthenticationStatus.AccountLocked;
                    nakInfo.IsLocked = true;
                    nakInfo.TTL      = lockTTL;

                    nakCache.Touch(accountKey, lockTTL);
                }
                else
                {
                    result           = new AuthenticationResult(AuthenticationStatus.AccountLocked, lockTTL);
                    nakInfo          = new LockoutState(this, realmMapping, realm, account, "", result);
                    nakInfo.Status   = AuthenticationStatus.AccountLocked;
                    nakInfo.IsLocked = true;
                    nakInfo.TTL      = lockTTL;

                    nakCache.Add(accountKey, nakInfo, lockTTL);
                }
            }
        }

        /// <summary>
        /// Authenticates user credentials against the currently mapped realms.
        /// </summary>
        /// <param name="realm">The authentication realm.</param>
        /// <param name="account">The account name.</param>
        /// <param name="password">The password.</param>
        /// <returns>An <see cref="AuthenticationResult" /> instance describing the result of the operation.</returns>
        /// <exception cref="AuthenticationException">Thrown if the engine is not running or if there was a problem querying the authentication source.</exception>
        public AuthenticationResult Authenticate(string realm, string account, string password)
        {
            string                  accountKey = GetAccountKey(realm, account);
            bool                    justLocked = false;
            bool                    logSuccess = false;
            string                  logMsg     = null;
            TimeSpan                lockTTL    = TimeSpan.Zero;
            RealmMapping            realmMapping;
            AuthenticationResult    authResult;
            AuthState               authState;
            LockoutState            nakInfo;

            try
            {
                using (TimedLock.Lock(syncLock))
                {
                    cTotalAuths++;
                    perf.TotalAuths.Increment();

                    // Make sure that the realm is known and get its mapping information.

                    if (!realmMap.TryGetValue(realm, out realmMapping))
                    {
                        logSuccess = false;
                        logMsg = "Unknown realm";

                        perf.FailAuths.Increment();
                        return new AuthenticationResult(AuthenticationStatus.BadRealm, settings.CacheTTL);
                    }
                    else
                    {
                        // Blank passwords are not allowed.

                        if (password == string.Empty)
                            return new AuthenticationResult(AuthenticationStatus.BadPassword, "Blank passwords are not allowed.", nakCache.DefaultTTL);

                        // Look for cached matching authentications.

                        if (cache != null && cache.TryGetValue(accountKey, out authState) && authState.Password == password)
                        {
                            logSuccess = true;
                            logMsg     = "Cached authentication";

                            perf.SuccessAuths.Increment();
                            return new AuthenticationResult(AuthenticationStatus.Authenticated, authState.MaxCacheTime);
                        }

                        // Look for cached NAKs

                        if (nakCache != null && nakCache.TryGetValue(accountKey, out nakInfo))
                        {
                            if (nakInfo.IsLocked)
                            {
                                logSuccess = false;
                                logMsg     = "Account is locked";

                                perf.FailAuths.Increment();
                                return new AuthenticationResult(nakInfo.Status, nakInfo.TTL);
                            }
                            else if (nakInfo.IsKnownBadPassword(password))
                            {
                                logSuccess = false;
                                logMsg     = "Cached authentication failure";

                                if (nakInfo.LockIfRequired(this))
                                {
                                    justLocked = true;
                                    lockTTL    = nakInfo.TTL;
                                    logMsg    += AccountNowLockedMsg;
                                }

                                if (nakInfo.Status == AuthenticationStatus.Authenticated)
                                    perf.SuccessAuths.Increment();
                                else
                                    perf.FailAuths.Increment();

                                return new AuthenticationResult(nakInfo.Status, nakInfo.TTL);
                            }
                        }
                    }
                }

                // Call the authentication extension outside of the lock to
                // query the against the actual authentication source.

                perf.ExtensionAuths.Increment();
                authResult = realmMapping.AuthExtension.Authenticate(realm, account, password);

                // Raise the AuthenticatedAccountEvent if initialized and the 
                // credentials are authentic.

                if (AuthenticatedAccountEvent != null && authResult.Status == AuthenticationStatus.Authenticated)
                    AuthenticatedAccountEvent(realm, account, password, authResult.MaxCacheTime);

                // Handle result caching

                using (TimedLock.Lock(syncLock))
                {
                    if (authResult.Status == AuthenticationStatus.Authenticated)
                    {
                        perf.SuccessAuths.Increment();

                        logSuccess = true;
                        logMsg = string.Format("[{0}]: {1}", realmMapping.ExtensionName, authResult.Message);

                        // Cache successful authentications

                        if (cache != null && !cache.ContainsKey(accountKey))
                            cache.Add(accountKey, new AuthState(password, authResult.MaxCacheTime), Helper.Min(this.cacheTTL, authResult.MaxCacheTime));
                    }
                    else
                    {
                        perf.FailAuths.Increment();

                        logSuccess = false;
                        logMsg = string.Format("[{0}]: {1}", realmMapping.ExtensionName, authResult.Message);

                        // Add failed authentications to the NAK cache

                        if (nakCache != null)
                        {
                            if (!nakCache.TryGetValue(accountKey, out nakInfo))
                            {
                                // Add a NAK to the cache

                                nakInfo = new LockoutState(this, realmMapping, realm, account, password, authResult);
                                if (nakInfo.IsLocked)
                                {
                                    justLocked = true;
                                    lockTTL    = nakInfo.TTL;
                                    logMsg    += AccountNowLockedMsg;
                                }

                                nakCache.Add(accountKey, nakInfo, nakInfo.TTL);
                            }
                            else
                            {
                                // Add new bad passwords to the cached NAK 
                                // and check to see if the account should be
                                // locked.

                                if (!nakInfo.IsKnownBadPassword(password))
                                    nakInfo.Passwords.Add(password, true);

                                if (nakInfo.LockIfRequired(this))
                                {
                                    justLocked = true;
                                    lockTTL = nakInfo.TTL;
                                    logMsg += AccountNowLockedMsg;
                                }
                            }
                        }
                    }
                }

                // Signal when accounts are locked 

                if (justLocked && AccountLockStatusEvent != null)
                    AccountLockStatusEvent(realm, account, true, lockTTL);

                return authResult;
            }
            catch (Exception e)
            {
                SysLog.LogException(e);

                perf.Exceptions.Increment();
                throw;
            }
            finally
            {
                // Log the security event.  Note that I'm taking
                // care to do this outside of the lock.

                if (logMsg != null)
                    LogSecurityEvent(logSuccess, realm, account, logMsg);
            }
        }

        /// <summary>
        /// Synchonously loads the realm map.
        /// </summary>
        public void LoadRealmMap()
        {
            Dictionary<string, RealmMapping>    newMap;
            List<RealmMapping>                  realms;
            IRealmMapProvider                   provider;

            using (TimedLock.Lock(syncLock))
            {
                if (!IsRunning)
                    throw new AuthenticationException(NotRunningMsg);

                provider = realmMapProvider;
            }

            realms = provider.GetMap();
            newMap = new Dictionary<string, RealmMapping>();

            foreach (RealmMapping mapping in realms)
            {
                if (newMap.ContainsKey(mapping.Realm))
                {
                    SysLog.LogWarning("Authentication Engine: Duplicate realm [{0}].", mapping.Realm);
                    continue;
                }

                mapping.AuthExtension = Helper.CreateInstance<IAuthenticationExtension>(mapping.ExtensionType);
                mapping.AuthExtension.Open(mapping.Args, mapping.Query, null, null);

                newMap.Add(mapping.Realm, mapping);
            }

            using (TimedLock.Lock(syncLock))
            {
                if (IsRunning)
                    this.realmMap = newMap;
            }
        }

        /// <summary>
        /// Adds a set of authenticated credentials to the success cache (if caching
        /// is enabled).
        /// </summary>
        /// <param name="realm">The authentication cache.</param>
        /// <param name="account">The account.</param>
        /// <param name="password">The password.</param>
        /// <param name="ttl">The requested time-to-live for the cached item.</param>
        /// <remarks>
        /// <para>
        /// If the credentials are already in the cache then the TTL for the
        /// entry will be reset to the new value.
        /// </para>
        /// <note>
        /// The actual time-to-live for the item will be the minimum
        /// of the TTL requested and the time configured for the cache.
        /// </note>
        /// </remarks>
        public void AddCredentials(string realm, string account, string password, TimeSpan ttl)
        {
            string      key = GetAccountKey(realm, account);
            AuthState   authState;

            using (TimedLock.Lock(syncLock))
            {
                if (!IsRunning)
                    throw new AuthenticationException(NotRunningMsg);

                if (cache == null)
                    return;

                ttl = Helper.Min(ttl, cache.DefaultTTL);
                if (cache.TryGetValue(key, out authState))
                {
                    authState.Password     = password;  // It is possible for the password to have
                    authState.MaxCacheTime = ttl;       // been updated at the source
                    cache.Touch(key, ttl);
                }
                else
                    cache.Add(key, new AuthState(password, ttl));
            }
        }

        /// <summary>
        /// Clears all entries from the authentication success cache.
        /// </summary>
        public void ClearCache()
        {
            using (TimedLock.Lock(syncLock))
            {
                if (!IsRunning)
                    throw new AuthenticationException(NotRunningMsg);

                if (cache != null)
                    cache.Clear();
            }
        }

        /// <summary>
        /// Flushes all entries for a specific realm/account from the authentication
        /// success cache.
        /// </summary>
        /// <param name="realm">The realm.</param>
        /// <param name="account">The account (or <c>null</c> to flush the entire realm).</param>
        public void FlushCache(string realm, string account)
        {
            using (TimedLock.Lock(syncLock))
            {
                if (!IsRunning)
                    throw new AuthenticationException(NotRunningMsg);

                if (cache != null)
                {
                    if (account != null)
                        cache.Remove(GetAccountKey(realm, account));
                    else
                    {

                        List<string>    delKeys = new List<string>();
                        string          match   = realm.ToUpper() + "/";

                        foreach (string key in cache.Keys)
                            if (key.ToUpper().StartsWith(match))
                                delKeys.Add(key);

                        for (int i = 0; i < delKeys.Count; i++)
                            cache.Remove(delKeys[i]);
                    }
                }
            }
        }

        /// <summary>
        /// Clears all entries from the authentication failure cache.
        /// </summary>
        public void ClearNakCache()
        {
            using (TimedLock.Lock(syncLock))
            {
                if (!IsRunning)
                    throw new AuthenticationException(NotRunningMsg);

                if (nakCache != null)
                    nakCache.Clear();
            }
        }

        /// <summary>
        /// Flushes all entries for a realm/account from the authentication
        /// failure cache.
        /// </summary>
        /// <param name="realm">The realm.</param>
        /// <param name="account">The account (or <c>null</c> to flush the entire realm).</param>
        public void FlushNakCache(string realm, string account)
        {
            using (TimedLock.Lock(syncLock))
            {
                if (!IsRunning)
                    throw new AuthenticationException(NotRunningMsg);

                if (nakCache != null)
                {
                    if (account != null)
                        nakCache.Remove(GetAccountKey(realm, account));
                    else
                    {
                        List<string>    delKeys = new List<string>();
                        string          match   = realm.ToUpper() + "/";

                        foreach (string key in nakCache.Keys)
                            if (key.ToUpper().StartsWith(match))
                                delKeys.Add(key);

                        for (int i = 0; i < delKeys.Count; i++)
                            nakCache.Remove(delKeys[i]);
                    }
                }
            }
        }

        /// <summary>
        /// Used by unit tests to get the <see cref="RealmMapping" /> instance
        /// for a particular authentication realm.
        /// </summary>
        /// <param name="realm">The realm.</param>
        /// <returns>The realm mapping or <c>null</c>.</returns>
        internal RealmMapping GetRealmMapping(string realm)
        {
            RealmMapping mapping;

            if (realmMap.TryGetValue(realm, out mapping))
                return mapping;
            else
                return null;
        }

        /// <summary>
        /// Used by unit tests to determine whether a set of credentials
        /// are currently pressent in the authentication success cache.
        /// </summary>
        /// <param name="realm">The realm.</param>
        /// <param name="account">The account.</param>
        /// <param name="password">The password.</param>
        /// <returns><c>true</c> if the credentials are cached.</returns>
        internal bool IsCached(string realm, string account, string password)
        {
            AuthState authState;

            using (TimedLock.Lock(syncLock))
            {
                if (!IsRunning)
                    throw new AuthenticationException(NotRunningMsg);

                return cache != null &&
                       cache.TryGetValue(GetAccountKey(realm, account), out authState) &&
                       authState.Password == password;
            }
        }

        /// <summary>
        /// Used by unit tests to determine whether a set of credentials
        /// are currently pressent in the authentication NAK cache.
        /// </summary>
        /// <param name="realm">The realm.</param>
        /// <param name="account">The account.</param>
        /// <param name="password">The password.</param>
        /// <returns><c>true</c> if the credentials are present in the NAK cache.</returns>
        internal bool IsNakCached(string realm, string account, string password)
        {
            LockoutState nakInfo;

            using (TimedLock.Lock(syncLock))
            {
                if (!IsRunning)
                    throw new AuthenticationException(NotRunningMsg);

                return nakCache != null &&
                       nakCache.TryGetValue(GetAccountKey(realm, account), out nakInfo) &&
                       nakInfo.Passwords.ContainsKey(password);
            }
        }

        /// <summary>
        /// Used by unit tests to determine if an account is currently locked.
        /// </summary>
        /// <param name="realm">The realm.</param>
        /// <param name="account">The account.</param>
        /// <returns><c>true</c> if the account is locked.</returns>
        internal bool IsNakLocked(string realm, string account)
        {
            LockoutState nakInfo;

            using (TimedLock.Lock(syncLock))
            {
                if (!IsRunning)
                    throw new AuthenticationException(NotRunningMsg);

                return nakCache != null &&
                       nakCache.TryGetValue(GetAccountKey(realm, account), out nakInfo) &&
                       nakInfo.IsLocked;
            }
        }

        /// <summary>
        /// Handles periodic background tasks.
        /// </summary>
        /// <param name="state">Not used.</param>
        private void OnBkTask(object state)
        {
            using (TimedLock.Lock(syncLock))
            {
                // Calculate the cache performance counters

                int     cSuccessHits, cSuccessMisses;
                int     cNakHits, cNakMisses;
                int     cacheCount, cacheMaxItems;
                int     nakCount, nakMaxItems;

                if (cache != null)
                {
                    cache.GetHitStats(out cSuccessHits, out cSuccessMisses);

                    cacheCount    = cache.Count;
                    cacheMaxItems = cache.MaxItems;
                }
                else
                {
                    cSuccessHits   = 0;
                    cSuccessMisses = 0;
                    cacheCount     = 0;
                    cacheMaxItems  = 0;
                }

                if (nakCache != null)
                {
                    nakCache.GetHitStats(out cNakHits, out cNakMisses);

                    nakCount    = nakCache.Count;
                    nakMaxItems = nakCache.MaxItems;
                }
                else
                {
                    cNakHits    = 0;
                    cNakMisses  = 0;
                    nakCount    = 0;
                    nakMaxItems = 0;
                }

                perf.CacheSize.RawValue        = cacheCount + nakCount;
                perf.CacheUtilization.RawValue = Helper.CalcPercent(cacheCount + nakCount, cacheMaxItems + nakMaxItems);
                perf.CacheHitRatio.RawValue    = Helper.CalcPercent(cSuccessHits + cNakHits, cTotalAuths);

                cTotalAuths = 0;

                // Handle cache flushing

                if (SysTime.Now >= cacheFlushTime)
                {
                    if (cache != null)
                        cache.Flush();

                    if (nakCache != null)
                        nakCache.Flush();

                    cacheFlushTime = SysTime.Now + cacheFlushInterval;
                }

                // Handle realm map polling

                if (SysTime.Now >= mapLoadTime)
                {
                    try
                    {
                        LoadRealmMap();
                    }
                    catch (Exception e)
                    {
                        perf.Exceptions.Increment();
                        SysLog.LogException(e);
                    }

                    mapLoadTime = SysTime.Now + mapLoadInterval;
                }
            }
        }
    }
}
