//-----------------------------------------------------------------------------
// FILE:        MsgQueueSettings.cs
// CONTRIBUTOR: Jeff Lill
// COPYRIGHT:   Copyright (c) 2005-2015 by Jeffrey Lill.  All rights reserved.
// DESCRIPTION: Defines the settings used to configure a MsgQueue instance.

using System;

using LillTek.Common;
using LillTek.Messaging;
using LillTek.Messaging.Internal;

namespace LillTek.Messaging.Queuing
{
    /// <summary>
    /// Defines the settings used to configure a <see cref="MsgQueue" /> instance.
    /// </summary>
    /// <remarks>
    /// <para>
    /// This class defines the following settings:
    /// </para>
    /// <div class="tablediv">
    /// <table class="dtTABLE" cellspacing="0" ID="Table1">
    /// <tr valign="top">
    /// <th width="1">Setting</th>        
    /// <th width="1">Default</th>
    /// <th width="90%">Description</th>
    /// </tr>
    /// <tr valign="top">
    ///     <td>BaseEP</td>
    ///     <td><see cref="MsgQueue.AbstractBaseEP" /></td>
    ///     <td>
    ///     The message queue service's base endpoint.
    ///     </td>
    /// </tr>
    /// <tr valign="top">
    ///     <td>Timeout</td>
    ///     <td>infinite</td>
    ///     <td>
    ///     The maximum time to wait for a response from a queue service before
    ///     terminating a transaction with a <see cref="TimeoutException" />.
    ///     Set <b>infinite</b> to wait indefinitely.
    ///     </td>
    /// </tr>
    /// <tr valign="top">
    ///     <td>MessageTTL</td>
    ///     <td><see cref="TimeSpan.Zero" /></td>
    ///     <td>
    ///     The default message expiration time.  This value is used to caclulate
    ///     the expiration time for for messages whose <see cref="QueuedMsg.ExpireTime" />
    ///     property was not explicitly set by the application.  Use <see cref="TimeSpan.Zero" />
    ///     to set the expiration date to the maximum in this case.
    ///     </td>
    /// </tr>
    /// <tr valign="top">
    ///     <td>Compress</td>
    ///     <td>BEST</td>
    ///     <td>
    ///     Specifes if serialized message bodies should be compressed before being
    ///     submitted to the queue service.  The possible values are <B>NONE</B>,
    ///     <b>COMPRESS</b>, and <b>BEST</b>.
    ///     </td>
    /// </tr>
    /// </table>
    /// </div>
    /// </remarks>
    public sealed class MsgQueueSettings
    {
        //---------------------------------------------------------------------
        // Static members

        /// <summary>
        /// Loads message queue settings from the application configuration using
        /// the specified key prefix.
        /// </summary>
        /// <param name="keyPrefix">The configuration key prefix.</param>
        /// <returns>A <see cref="MsgQueueSettings" /> instance.</returns>
        /// <remarks>
        /// <para>
        /// This method loads the settings described below from the application
        /// configuration using the <paramref name="keyPrefix" />.
        /// </para>
        /// <div class="tablediv">
        /// <table class="dtTABLE" cellspacing="0" ID="Table1">
        /// <tr valign="top">
        /// <th width="1">Setting</th>        
        /// <th width="1">Default</th>
        /// <th width="90%">Description</th>
        /// </tr>
        /// <tr valign="top">
        ///     <td>BaseEP</td>
        ///     <td><see cref="MsgQueue.AbstractBaseEP" /></td>
        ///     <td>
        ///     The message queue service's base endpoint.
        ///     </td>
        /// </tr>
        /// <tr valign="top">
        ///     <td>Timeout</td>
        ///     <td>infinite</td>
        ///     <td>
        ///     The maximum time to wait for a response from a queue service before
        ///     terminating a transaction with a <see cref="TimeoutException" />.
        ///     Set <b>infinite</b> to wait indefinitely.
        ///     </td>
        /// </tr>
        /// <tr valign="top">
        ///     <td>MessageTTL</td>
        ///     <td><see cref="TimeSpan.Zero" /></td>
        ///     <td>
        ///     The default message expiration time.  This value is used to caclulate
        ///     the expiration time for for messages whose <see cref="QueuedMsg.ExpireTime" />
        ///     property was not explicitly set by the application.  Use <see cref="TimeSpan.Zero" />
        ///     to set the expiration date to the maximum in this case.
        ///     </td>
        /// </tr>
        /// <tr valign="top">
        ///     <td>Compress</td>
        ///     <td>BEST</td>
        ///     <td>
        ///     Specifes if serialized message bodies should be compressed before being
        ///     submitted to the queue service.  The possible values are <B>NONE</B>,
        ///     <b>COMPRESS</b>, and <b>BEST</b>.
        ///     </td>
        /// </tr>
        /// </table>
        /// </div>
        /// </remarks>
        public static MsgQueueSettings LoadConfig(string keyPrefix)
        {
            var settings = new MsgQueueSettings();
            var config   = new Config(keyPrefix);

            settings.BaseEP     = config.Get("BaseEP", settings.BaseEP);
            settings.Timeout    = config.Get("Timeout", settings.Timeout);
            settings.MessageTTL = config.Get("MessageTTL", settings.MessageTTL);
            settings.Compress   = config.Get<Compress>("Compress", settings.Compress);

            return settings;
        }

        /// <summary>
        /// Returns the default settings.
        /// </summary>
        public static readonly MsgQueueSettings Default = new MsgQueueSettings();

        //---------------------------------------------------------------------
        // Instance members

        /// <summary>
        /// The message queue service's base endpoint.
        /// </summary>
        public MsgEP BaseEP = MsgQueue.AbstractBaseEP;

        /// <summary>
        /// The maximum time to wait for a response from a queue service before
        /// terminating a transaction with a <see cref="TimeoutException" />.
        /// Set <see cref="TimeSpan.MaxValue" /> to wait indefinitely.
        /// The default value is <see cref="TimeSpan.MaxValue" />.
        /// </summary>
        public TimeSpan Timeout = TimeSpan.MaxValue;

        /// <summary>
        /// The default message expiration time.  This value is used to caclulate
        /// the expiration time for for messages whose <see cref="QueuedMsg.ExpireTime" />
        /// property was not explicitly set by the application.  Use <see cref="TimeSpan.Zero" />
        /// to set the expiration date to the maximum in this case.  The default
        /// value is <see cref="TimeSpan.Zero" />.
        /// </summary>
        public TimeSpan MessageTTL = TimeSpan.Zero;

        /// <summary>
        /// Specifes if serialized message bodies should be compressed before being
        /// submitted to the queue service.  This is specified using one of the
        /// <see cref="Compress" /> enumeration values and defaults to 
        /// <see cref="LillTek.Common.Compress.Best" />.
        /// </summary>
        public Compress Compress = Compress.Best;

        /// <summary>
        /// Constructs an instance with reasonable default settings.
        /// </summary>
        public MsgQueueSettings()
        {
        }
    }
}
