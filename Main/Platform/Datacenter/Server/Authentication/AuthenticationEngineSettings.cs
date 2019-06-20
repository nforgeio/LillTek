//-----------------------------------------------------------------------------
// FILE:        AuthenticationEngineSettings.cs
// CONTRIBUTOR: Jeff Lill
// COPYRIGHT:   Copyright (c) 2005-2015 by Jeffrey Lill. All rights reserved.
// DESCRIPTION: Startup settings for the AuthenticationEngine class.

using System;
using System.Collections.Generic;
using System.IO;

using LillTek.Common;

namespace LillTek.Datacenter.Server
{
    /// <summary>
    /// Startup settings for the AuthenticationEngine class.
    /// </summary>
    public sealed class AuthenticationEngineSettings
    {
        //---------------------------------------------------------------------
        // Static members

        /// <summary>
        /// Loads the settings for a <see cref="AuthenticationEngine" /> instance
        /// from the application configuration, using the specified configuration
        /// key prefix.
        /// </summary>
        /// <param name="keyPrefix">The application configuration key prefix.</param>
        /// <returns>An <see cref="AuthenticationEngineSettings" /> instance.</returns>
        /// <remarks>
        /// <para>
        /// The <see cref="AuthenticationEngine" /> settings are loaded from the application
        /// configuration, under the specified key prefix.  The following
        /// settings are recognized by the class:
        /// </para>
        /// <div class="tablediv">
        /// <table class="dtTABLE" cellspacing="0" ID="Table1">
        /// <tr valign="top">
        /// <th width="1">Setting</th>        
        /// <th width="1">Default</th>
        /// <th width="90%">Description</th>
        /// </tr>
        /// <tr valign="top">
        ///     <td>RealmMapLoadInterval</td>
        ///     <td>10m</td>
        ///     <td>
        ///     The interval at which the realm map will be reloaded.
        ///     </td>
        /// </tr>
        /// <tr valign="top">
        ///     <td>CacheTTL</td>
        ///     <td>10m</td>
        ///     <td>
        ///     The maximuim time an authentication result should be cached by the engine.
        ///     Use 0 to disable caching.  This overrides the MaxCacheTime time returned by an 
        ///     authentication source if this number is smaller.
        ///     </td>
        /// </tr>
        /// <tr valign="top">
        ///     <td>MaxCacheSize</td>
        ///     <td>100000</td>
        ///     <td>
        ///     The maximum number of cached authentication success results.  Use 0 to disable
        ///     caching.
        ///     </td>
        /// </tr>
        /// <tr valign="top">
        ///     <td>NakCacheTTL</td>
        ///     <td>15m</td>
        ///     <td>
        ///     Specifies the maximum time a failed authentication attempt should be
        ///     cached.  Set this to <see cref="TimeSpan.Zero" /> to
        ///     disable NAK caching.  Note that the actual time a failed attempt will
        ///     be cached is the minimum of this value and <b>LockoutThreshold</b>
        ///     or <b>LockoutTime</b> depending on whether the account has been
        ///     locked. 
        ///     </td>
        /// </tr>
        /// <tr valign="top">
        ///     <td>MaxNakCacheSize</td>
        ///     <td>100000</td>
        ///     <td>
        ///     The maximum number of cached authentication failure results.  This overrides the 
        ///     MaxCacheTime time returned by an authentication source if this number is smaller.  
        ///     Use 0 to disable NAK caching.
        ///     </td>
        /// </tr>
        /// <tr valign="top">
        ///     <td>CacheFlushInterval</td>
        ///     <td>1m</td>
        ///     <td>
        ///     The interval between authentication success and failure cache flushes.
        ///     </td>
        /// </tr>
        /// <tr valign="top">
        ///     <td>BkTaskInterval</td>
        ///     <td>5s</td>
        ///     <td>
        ///     The interval at which engine background tasks are scheduled.  These
        ///     tasks include periodically reloading the realm map and flushing the
        ///     authentication cache.
        ///     </td>
        /// </tr>
        /// <tr valign="top">
        ///     <td>LogAuthSuccess</td>
        ///     <td>true</td>
        ///     <td>
        ///     Indicates whether successful login attempts should be logged. 
        ///     </td>
        /// </tr>
        /// <tr valign="top">
        ///     <td>LogAuthFailure</td>
        ///     <td>true</td>
        ///     <td>
        ///     Indicates whether failed login attempts should be logged.
        ///     </td>
        /// </tr>
        /// <tr valign="top">
        ///     <td>LockoutCount</td>
        ///     <td>5</td>
        ///     <td>
        ///     Indicates the default maximum number of failed authentication requests
        ///     to be allowed for a realm/account combinations for nonexistent realms
        ///     before the account will be locked out.  This parameter can be overridden
        ///     for specific realms.
        ///     </td>
        /// </tr>
        /// <tr valign="top">
        ///     <td>LockoutThreshold</td>
        ///     <td>1m</td>
        ///     <td>
        ///     The default period of time that can elapse between failed authentication 
        ///     attempts where the failed attempts will <b>not</b> be counted against the
        ///     <see cref="LockoutCount" />.  Set this to <see cref="TimeSpan.Zero" />
        ///     to disable account lockout for the realm.  This parameter can be overridden
        ///     for specific realms.
        ///     </td>
        /// </tr>
        /// <tr valign="top">
        ///     <td>LockoutTime</td>
        ///     <td>5m</td>
        ///     <td>
        ///     The default period of time an account will remain locked after being locked
        ///     out due to too many failed authentication attempts.  This parameter can be overridden
        ///     for specific realms.
        ///     </td>
        /// </tr>
        /// </table>
        /// </div>
        /// </remarks>
        public static AuthenticationEngineSettings LoadConfig(string keyPrefix)
        {
            var settings = new AuthenticationEngineSettings();
            var config   = new Config(keyPrefix);

            settings.RealmMapLoadInterval = config.Get("RealmMapLoadInterval", settings.RealmMapLoadInterval);
            settings.CacheTTL             = config.Get("CacheTTL", settings.CacheTTL);
            settings.MaxCacheSize         = config.Get("MaxCacheSize", settings.MaxCacheSize);
            settings.NakCacheTTL          = config.Get("NakCacheTTL", settings.NakCacheTTL);
            settings.MaxNakCacheSize      = config.Get("MaxNakCacheSize", settings.MaxNakCacheSize);
            settings.CacheFlushInterval   = config.Get("CacheFlushInterval", settings.CacheFlushInterval);
            settings.BkTaskInterval       = config.Get("BkTaskInterval", settings.BkTaskInterval);
            settings.LogAuthSuccess       = config.Get("LogAuthSuccess", settings.LogAuthSuccess);
            settings.LogAuthFailure       = config.Get("LogAuthFailure", settings.LogAuthFailure);
            settings.LockoutCount         = config.Get("LockoutCount", settings.LockoutCount);
            settings.LockoutThreshold     = config.Get("LockoutThreshold", settings.LockoutThreshold);
            settings.LockoutTime          = config.Get("LockoutTime", settings.LockoutTime);

            return settings;
        }

        //---------------------------------------------------------------------
        // Instance members

        /// <summary>
        /// The interval at which the realm map will be reloaded.  Default is 10 minutes.
        /// </summary>
        public TimeSpan RealmMapLoadInterval = TimeSpan.FromMinutes(10);

        /// <summary>
        /// The maximuim time an authentication result should be cached by the engine.
        /// Use 0 to disable caching.  This overrides the MaxCacheTime time returned by an 
        /// authentication source.  Default is 10 minutes.
        /// </summary>
        public TimeSpan CacheTTL = TimeSpan.FromMinutes(10);

        /// <summary>
        /// The maximum number of allowed cached authentication success results.  Use 0 to disable
        /// caching.  Default is 100000.
        /// </summary>
        public int MaxCacheSize = 100000;

        /// <summary>
        /// Specifies the time a failed authentication attempt should be
        /// cached.  Set this to <see cref="TimeSpan.Zero" /> to
        /// disable NAK caching.  Default is 5 minutes.
        /// </summary>
        public TimeSpan NakCacheTTL = TimeSpan.FromMinutes(5);

        /// <summary>
        /// The maximum number of allowed cached authentication failure results.  Use 0 to disable
        /// caching.  Default is 100000.
        /// </summary>
        public int MaxNakCacheSize = 100000;

        /// <summary>
        /// The interval between authentication cache flushes.  Default is 1 minute.
        /// </summary>
        public TimeSpan CacheFlushInterval = TimeSpan.FromMinutes(1);

        /// <summary>
        /// The interval at which engine background tasks are scheduled.  These
        /// tasks include periodically reloading the realm map and flushing the
        /// authentication cache.  Default is 5 seconds.
        /// </summary>
        public TimeSpan BkTaskInterval = TimeSpan.FromSeconds(5);

        /// <summary>
        /// Indicates whether successful login attempts should be logged. 
        /// Default is true.
        /// </summary>
        public bool LogAuthSuccess = true;

        /// <summary>
        /// Indicates whether failed login attempts should be logged.
        /// Default is true.
        /// </summary>
        public bool LogAuthFailure = true;

        /// <summary>
        /// Indicates the default maximum number of failed authentication requests
        /// to be allowed for a realm/account combinations for nonexistent realms
        /// before the account will be locked out.  This parameter can be overridden
        /// for specific realms.
        /// </summary>
        public int LockoutCount = 5;

        /// <summary>
        /// The default period of time that can elapse between failed authentication 
        /// attempts where the failed attempts will <b>not</b> be counted against the
        /// <see cref="LockoutCount" />.  Set this to <see cref="TimeSpan.Zero" />
        /// to disable account lockout for the realm.  This parameter can be overridden
        /// for specific realms.
        /// </summary>
        public TimeSpan LockoutThreshold = TimeSpan.FromMinutes(5);

        /// <summary>
        /// The default period of time an account will remain locked after being locked
        /// out due to too many failed authentication attempts.  This parameter can be overridden
        /// for specific realms.
        /// </summary>
        public TimeSpan LockoutTime = TimeSpan.FromMinutes(5);

        /// <summary>
        /// Constructor.
        /// </summary>
        public AuthenticationEngineSettings()
        {
        }
    }
}
