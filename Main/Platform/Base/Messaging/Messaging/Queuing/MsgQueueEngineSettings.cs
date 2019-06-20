//-----------------------------------------------------------------------------
// FILE:        MsgQueueEngineSettings.cs
// CONTRIBUTOR: Jeff Lill
// COPYRIGHT:   Copyright (c) 2005-2015 by Jeffrey Lill.  All rights reserved.
// DESCRIPTION: Defines the settings used to configure a MsgQueueEngine instance.

using System;

using LillTek.Common;
using LillTek.Messaging;
using LillTek.Messaging.Internal;

namespace LillTek.Messaging.Queuing
{
    /// <summary>
    /// Defines the settings used to configure a <see cref="MsgQueueEngine" /> instance.
    /// </summary>
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
    ///     <td>QueueMap</td>
    ///     <td>(see note)</td>
    ///     <td>
    ///     <para>
    ///     Specifies the set of base logical queue endpoints associated with the 
    ///     <see cref="MsgQueueEngine" /> instance.  This set is expressed as
    ///     a configuration array as in:
    ///     </para>
    ///     <code lang="none">
    ///     QueueMap[0] = logical://LillTek/Queues/US
    ///     QueueMap[1] = logical://LillTek/Queues/Japan
    ///     </code>
    ///     <para>
    ///     This defaults to <see cref="MsgQueue.AbstractBaseEP" />.
    ///     </para>
    ///     </td>
    /// </tr>
    /// <tr valign="top">
    ///     <td>FlushInterval</td>
    ///     <td>5m</td>
    ///     <td>
    ///     Controls how often the message queues are checked for expired
    ///     messages.
    ///     </td>
    /// </tr>
    /// <tr valign="top">
    ///     <td>DeadLetterTTL</td>
    ///     <td>7d</td>
    ///     <td>
    ///     Controls how long messages are kept in the dead letter queue before
    ///     being purged. Use <see cref="TimeSpan.Zero" /> if messages are to
    ///     be archived indefinitely.  
    ///     </td>
    /// </tr>
    /// <tr valign="top">
    ///     <td>MaxDeliveryAttempts</td>
    ///     <td>3</td>
    ///     <td>
    ///     The maximum number of unconfirmed transacted deliveries will
    ///     be attempted before declaring the message as poison.
    ///     </td>
    /// </tr>
    /// <tr valign="top">
    ///     <td>KeepAliveInterval</td>
    ///     <td>30s</td>
    ///     <td>
    ///     Session keep-alive transmission interval.
    ///     </td>
    /// </tr>
    /// <tr valign="top">
    ///     <td>SessionTimeout</td>
    ///     <td>95s</td>
    ///     <td>
    ///     The maximum time either side of a duplex session with a 
    ///     message queue engine should wait for normal message traffic
    ///     or keep-alive transmission from the other side.
    ///     </td>
    /// </tr>
    /// <tr valign="top">
    ///     <td>PendingCheckInterval</td>
    ///     <td>60s</td>
    ///     <td>
    ///     Interval between background checks that ensure that sessions
    ///     with pending dequeue or peek operations will be satisfied 
    ///     if there's a problem doing so when a message is enqueued.
    ///     </td>
    /// </tr>
    /// <tr valign="top">
    ///     <td>BkTaskInterval</td>
    ///     <td>1s</td>
    ///     <td>
    ///     Controls the frequency of the scheduling of background tasks such
    ///     as cross queue service instance message routing.
    ///     </td>
    /// </tr>
    /// </table>
    /// </div>
    public class MsgQueueEngineSettings
    {
        //---------------------------------------------------------------------
        // Static members

        /// <summary>
        /// Loads settings from the application configuration using the specified
        /// configuration key prefix.
        /// </summary>
        /// <param name="keyPrefix">The configuration key prefix.</param>
        /// <returns>The loaded <see cref="MsgQueueEngineSettings" /> instance.</returns>
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
        ///     <td>QueueMap</td>
        ///     <td>(see note)</td>
        ///     <td>
        ///     <para>
        ///     Specifies the set of base logical queue endpoints associated with the 
        ///     <see cref="MsgQueueEngine" /> instance.  This set is expressed as
        ///     a configuration array as in:
        ///     </para>
        ///     <code lang="none">
        ///     QueueMap[-] = logical://LillTek/Queues/US
        ///     QueueMap[-] = logical://LillTek/Queues/Japan
        ///     </code>
        ///     <para>
        ///     This defaults to <see cref="MsgQueue.AbstractBaseEP" />.
        ///     </para>
        ///     </td>
        /// </tr>
        /// <tr valign="top">
        ///     <td>FlushInterval</td>
        ///     <td>5m</td>
        ///     <td>
        ///     Controls how often the message queues are checked for expired messages.
        ///     </td>
        /// </tr>
        /// <tr valign="top">
        ///     <td>DeadLetterTTL</td>
        ///     <td>7d</td>
        ///     <td>
        ///     Controls how long messages are kept in the dead letter queue before
        ///     being purged. Use <see cref="TimeSpan.Zero" /> if messages are to
        ///     be archived indefinitely.  
        ///     </td>
        /// </tr>
        /// <tr valign="top">
        ///     <td>MaxDeliveryAttempts</td>
        ///     <td>3</td>
        ///     <td>
        ///     The maximum number of unconfirmed transacted deliveries will
        ///     be attempted before declaring the message as poison.
        ///     </td>
        /// </tr>
        /// <tr valign="top">
        ///     <td>KeepAliveInterval</td>
        ///     <td>60s</td>
        ///     <td>
        ///     Session keep-alive transmission interval.
        ///     </td>
        /// </tr>
        /// <tr valign="top">
        ///     <td>SessionTimeout</td>
        ///     <td>95s</td>
        ///     <td>
        ///     The maximum time either side of a duplex session with a 
        ///     message queue engine should wait for normal message traffic
        ///     or keep-alive transmission from the other side.
        ///     </td>
        /// </tr>
        /// <tr valign="top">
        ///     <td>PendingCheckInterval</td>
        ///     <td>60s</td>
        ///     <td>
        ///     Interval between background checks that ensure that sessions
        ///     with pending dequeue or peek operations will be satisfied 
        ///     if there's a problem doing so when a message is enqueued.
        ///     </td>
        /// </tr>
        /// <tr valign="top">
        ///     <td>BkTaskInterval</td>
        ///     <td>1s</td>
        ///     <td>
        ///     Controls the frequency of the scheduling of background tasks such
        ///     as cross queue service instance message routing.
        ///     </td>
        /// </tr>
        /// </table>
        /// </div>
        /// </remarks>
        public static MsgQueueEngineSettings LoadConfig(string keyPrefix)
        {
            var         settings = new MsgQueueEngineSettings();
            var         config   = new Config(keyPrefix);
            string[]    arr;

            arr = config.GetArray("QueueMap");
            if (arr.Length > 0)
            {
                settings.QueueMap = new MsgEP[arr.Length];
                for (int i = 0; i < arr.Length; i++)
                    settings.QueueMap[i] = arr[i];
            }

            settings.FlushInterval        = config.Get("FlushInterval", settings.FlushInterval);
            settings.DeadLetterTTL        = config.Get("DeadLetterTTL", settings.DeadLetterTTL);
            settings.MaxDeliveryAttempts  = config.Get("MaxDeliveryAttempts", settings.MaxDeliveryAttempts);
            settings.SessionTimeout       = config.Get("SessionTimeout", settings.SessionTimeout);
            settings.KeepAliveInterval    = config.Get("KeepAliveInterval", settings.KeepAliveInterval);
            settings.PendingCheckInterval = config.Get("PendingCheckInterval", settings.PendingCheckInterval);
            settings.BkTaskInterval       = config.Get("BkTaskInterval", settings.BkTaskInterval);

            return settings;
        }

        //---------------------------------------------------------------------
        // Instance members

        /// <summary>
        /// The set of message queue base endpoints the engine should listen on.
        /// This defaults to a single entry of <see cref="MsgQueue.AbstractBaseEP" />.
        /// </summary>
        public MsgEP[] QueueMap = new MsgEP[] { MsgEP.Parse(MsgQueue.AbstractBaseEP) };

        /// <summary>
        /// Controls how often the message queues are checked for messages whose
        /// delivery time limit has expired.  Defaults to <b>5m</b>.
        /// </summary>
        public TimeSpan FlushInterval = TimeSpan.FromMinutes(5);

        /// <summary>
        /// Controls how long messages are kept in the dead letter queue before
        /// being purged.  Use <see cref="TimeSpan.Zero" /> if messages are to
        /// be archived indefinitely.  Defaults to <b>7d</b>.
        /// </summary>
        public TimeSpan DeadLetterTTL = TimeSpan.FromDays(7);

        /// <summary>
        /// The maximum number of unconfirmed transacted deliveries will
        /// be attempted before declaring the message as poison.  The
        /// default is <b>3</b>.
        /// </summary>
        public int MaxDeliveryAttempts = 3;

        /// <summary>
        /// Session keep-alive transmission interval.  The default
        /// is <b>30s</b>.
        /// </summary>
        public TimeSpan KeepAliveInterval = TimeSpan.FromSeconds(30);

        /// <summary>
        /// The maximum time either side of a duplex session with a 
        /// message queue engine should wait for normal message traffic
        /// or keep-alive transmission from the other side.
        /// </summary>
        public TimeSpan SessionTimeout = TimeSpan.FromSeconds(95);

        /// <summary>
        /// Interval between background checks that ensure that sessions
        /// with pending dequeue or peek operations will be satisfied 
        /// if there's a problem doing so when a message is enqueued.
        /// </summary>
        public TimeSpan PendingCheckInterval = TimeSpan.FromSeconds(60);

        /// <summary>
        /// Controls the frequency of the scheduling of background tasks such
        /// as cross queue service instance message routing.  Defaults to <b>1s</b>.
        /// </summary>
        public TimeSpan BkTaskInterval = TimeSpan.FromSeconds(1);

        /// <summary>
        /// Constructs an instance with reasonable default settings.
        /// </summary>
        public MsgQueueEngineSettings()
        {
        }
    }
}
