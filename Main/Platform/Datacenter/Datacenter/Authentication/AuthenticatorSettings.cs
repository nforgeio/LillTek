//-----------------------------------------------------------------------------
// FILE:        AuthenticatorSettings.cs
// CONTRIBUTOR: Jeff Lill
// COPYRIGHT:   Copyright (c) 2005-2015 by Jeffrey Lill.  All rights reserved.
// DESCRIPTION: Describes the settings for an Authenticator instance.

using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Threading;

using LillTek.Advanced;
using LillTek.Common;
using LillTek.Cryptography;
using LillTek.Datacenter.Msgs.AuthService;
using LillTek.Messaging;

namespace LillTek.Datacenter
{
    /// <summary>
    /// Describes the settings for an <see cref="Authenticator" /> instance.  These
    /// settings are passed to the <see cref="Authenticator.Open(MsgRouter,AuthenticatorSettings)" />
    /// method.
    /// </summary>
    /// <remarks>
    /// <para>
    /// The default constructor initializes an instance with reaasonable default values for all settings
    /// so application need only modify those settings that are important.  The <see cref="LoadConfig" />
    /// method can also be used to initialize an instance by reading from the application configuration.
    /// </para>
    /// <div class="tablediv">
    /// <table class="dtTABLE" cellspacing="0" ID="Table1">
    /// <tr valign="top">
    /// <th width="1">Setting</th>        
    /// <th width="1">Default</th>
    /// <th width="90%">Description</th>
    /// </tr>
    /// <tr valign="top">
    ///     <td>BkTaskInterval</td>
    ///     <td>1s</td>
    ///     <td>The interval at which background tasks are scheduled for the instance.</td>
    ///  </tr>
    /// <tr valign="top">
    ///     <td>CacheFlushInterval</td>
    ///     <td>1m</td>
    ///     <td>Time interval between scheduled cache flushes.</td>
    ///  </tr>
    /// <tr valign="top">
    ///     <td>MaxCacheSize</td>
    ///     <td>10000</td>
    ///     <td>Maximum number of authentication credentials to be cached.  Use 0 to disable caching.</td>
    ///  </tr>
    /// <tr valign="top">
    ///     <td>SuccessTTL</td>
    ///     <td>5m</td>
    ///     <td>Duration that successfully authenticated credentials will be cached.  Use 0 to disable success caching.</td>
    ///  </tr>
    /// <tr valign="top">
    ///     <td>FailTTL</td>
    ///     <td>5m</td>
    ///     <td>Duration that invalid credentials will be cached.  Use 0 to disable failure caching.</td>
    ///  </tr>
    /// </table>
    /// </div>
    /// </remarks>
    public sealed class AuthenticatorSettings
    {
        //---------------------------------------------------------------------
        // Static members

        /// <summary>
        /// Loads the <see cref="Authenticator" /> settings from the application configuration.
        /// </summary>
        /// <param name="keyPrefix">The configuration key prefix.</param>
        /// <returns>The settings.</returns>
        /// <remarks>
        /// <para>
        /// The settings will loaded are:
        /// </para>
        /// <div class="tablediv">
        /// <table class="dtTABLE" cellspacing="0" ID="Table1">
        /// <tr valign="top">
        /// <th width="1">Setting</th>        
        /// <th width="1">Default</th>
        /// <th width="90%">Description</th>
        /// </tr>
        /// <tr valign="top">
        ///     <td>BkTaskInterval</td>
        ///     <td>1s</td>
        ///     <td>The interval at which background tasks are scheduled for the instance.</td>
        ///  </tr>
        /// <tr valign="top">
        ///     <td>CacheFlushInterval</td>
        ///     <td>1m</td>
        ///     <td>Time interval between scheduled cache flushes.</td>
        ///  </tr>
        /// <tr valign="top">
        ///     <td>MaxCacheSize</td>
        ///     <td>10000</td>
        ///     <td>Maximum number of authentication credentials to be cached.  Use 0 to disable caching.</td>
        ///  </tr>
        /// <tr valign="top">
        ///     <td>SuccessTTL</td>
        ///     <td>5m</td>
        ///     <td>Duration that successfully authenticated credentials will be cached.  Use 0 to disable success caching.</td>
        ///  </tr>
        /// <tr valign="top">
        ///     <td>FailTTL</td>
        ///     <td>5m</td>
        ///     <td>Duration that invalid credentials will be cached.  Use 0 to disable failure caching.</td>
        ///  </tr>
        /// </table>
        /// </div>
        /// </remarks>
        public static AuthenticatorSettings LoadConfig(string keyPrefix)
        {
            var settings = new AuthenticatorSettings();
            var config = new Config(keyPrefix);

            settings.BkTaskInterval     = config.Get("BkTaskInterval", settings.BkTaskInterval);
            settings.CacheFlushInterval = config.Get("CacheFlushInterval", settings.CacheFlushInterval);
            settings.MaxCacheSize       = config.Get("MaxCacheSize", settings.MaxCacheSize);
            settings.SuccessTTL         = config.Get("SuccessTTL", settings.SuccessTTL);
            settings.FailTTL            = config.Get("FailTTL", settings.FailTTL);

            return settings;
        }

        //---------------------------------------------------------------------
        // Instance members

        /// <summary>
        /// The interval at which background tasks are scheduled for the instance.
        /// Default is 1s.
        /// </summary>
        public TimeSpan BkTaskInterval = TimeSpan.FromSeconds(1);

        /// <summary>
        /// Time interval between scheduled cache flushes.  Default is 1m.
        /// </summary>
        public TimeSpan CacheFlushInterval = TimeSpan.FromMinutes(1);

        /// <summary>
        /// Maximum number of authentication credentials to be cached.  Use 0 to disable caching.
        /// Default is 10000.
        /// </summary>
        public int MaxCacheSize = 10000;

        /// <summary>
        /// Duration that successfully authenticated credentials will be cached.  Use 0 to disable success caching.
        /// Default is 5m.
        /// </summary>
        public TimeSpan SuccessTTL = TimeSpan.FromMinutes(5);

        /// <summary>
        /// Duration that invalid credentials will be cached.  Use 0 to disable failure caching.
        /// Default is 5m.
        /// </summary>
        public TimeSpan FailTTL = TimeSpan.FromMinutes(5);

        /// <summary>
        /// Constructs an instance with default values.
        /// </summary>
        public AuthenticatorSettings()
        {
        }
    }
}
