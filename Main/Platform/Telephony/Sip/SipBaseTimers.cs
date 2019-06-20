//-----------------------------------------------------------------------------
// FILE:        SipBaseTimers.cs
// CONTRIBUTOR: Jeff Lill
// COPYRIGHT:   Copyright (c) 2005-2015 by Jeffrey Lill.  All rights reserved.
// DESCRIPTION: Holds the base timer intervals used for SIP transactions and
//              dialogs.

using System;
using System.Collections.Generic;
using System.Net;
using System.Text;

using LillTek.Common;

namespace LillTek.Telephony.Sip
{
    /// <summary>
    /// Holds the base timer intervals used for SIP transactions and dialogs.
    /// </summary>
    /// <remarks>
    /// <para>
    /// This class holds the base <b>T1</b>, <b>T2</b> and <b>T4</b> timer intervals as described on
    /// page 265 of <a href="http://www.ietf.org/rfc/rfc3261.txt?number=3261">RFC 3261</a>.  These
    /// values are used by SIP transactions and dialogs for computing and managing the various 
    /// required timers.
    /// </para>
    /// </remarks>
    public sealed class SipBaseTimers
    {
        //---------------------------------------------------------------------
        // Static members

        /// <summary>
        /// Loads the timers from the application configuration.
        /// </summary>
        /// <param name="keyPrefix">The configuration key prefix.</param>
        /// <returns>The loaded <see cref="SipBaseTimers" />.</returns>
        /// <remarks>
        /// <para>
        /// Here's the list of the timers loaded.
        /// </para>
        /// <div class="tablediv">
        /// <table class="dtTABLE" cellspacing="0" ID="Table1">
        /// <tr valign="top">
        /// <th width="1">Setting</th>        
        /// <th width="1">Default</th>
        /// <th width="90%">Description</th>
        /// </tr>
        /// <tr valign="top">
        ///     <td>T1</td>
        ///     <td>500ms</td>
        ///     <td>Round trip time estimate (RTT).</td>
        /// </tr>
        /// <tr valign="top">
        ///     <td>T2</td>
        ///     <td>4s</td>
        ///     <td>Maximum retransmit interval for non-INVITE requests and INVITE responses.</td>
        /// </tr>
        /// <tr valign="top">
        ///     <td>T4</td>
        ///     <td>5s</td>
        ///     <td>Maximum duration a message will remain in the network.</td>
        /// </tr>
        /// </table>
        /// </div>
        /// </remarks>
        public static SipBaseTimers LoadConfig(string keyPrefix)
        {
            var config = new Config(keyPrefix);
            var timers = new SipBaseTimers();

            timers.T1 = config.Get("T1", timers.T1);
            timers.T2 = config.Get("T2", timers.T2);
            timers.T4 = config.Get("T4", timers.T4);

            return timers;
        }

        //---------------------------------------------------------------------
        // Instance members

        /// <summary>
        /// Round trip time estimate (RTT).
        /// </summary>
        public TimeSpan T1;

        /// <summary>
        /// Maximum retransmit interval for non-INVITE requests and INVITE responses.
        /// </summary>
        public TimeSpan T2;

        /// <summary>
        /// Maximum duration a message will remain in the network.
        /// </summary>
        public TimeSpan T4;

        /// <summary>
        /// Constructs an instance with reasonable default settings.
        /// </summary>
        public SipBaseTimers()
        {
            T1 = TimeSpan.FromMilliseconds(500);
            T2 = TimeSpan.FromSeconds(4);
            T4 = TimeSpan.FromSeconds(5);
        }
    }
}
