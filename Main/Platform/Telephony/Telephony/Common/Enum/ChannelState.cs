//-----------------------------------------------------------------------------
// FILE:        ChannelState.cs
// CONTRIBUTOR: Jeff Lill
// COPYRIGHT:   Copyright (c) 2005-2015 by Jeffrey Lill.  All rights reserved.
// DESCRIPTION: Identifies the possible NeonSwitch channel states.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;

using FreeSWITCH;
using FreeSWITCH.Native;

using LillTek.Common;

namespace LillTek.Telephony.Common
{
    /// <summary>
    /// Identifies the possible NeonSwitch channel states.
    /// </summary>
    /// <remarks>
    /// These map directly to the underlying FreeSWITCH channel states and the documentation
    /// was taken from the <a href="http://wiki.freeswitch.org/wiki/Channel_States">FreeSWITCH wiki</a>.
    /// </remarks>
    public enum ChannelState
    {
        /// <summary>
        /// Channel is newly created. 
        /// </summary>
        New = (int)switch_channel_state_t.CS_NEW,

        /// <summary>
        /// Channel has been initialized. 
        /// </summary>
        Initialized = (int)switch_channel_state_t.CS_INIT,

        /// <summary>
        /// Channel is looking for an extension to execute. 
        /// </summary>
        Routing = (int)switch_channel_state_t.CS_ROUTING,

        /// <summary>
        /// Channel is ready to execute from 3rd party control. 
        /// </summary>
        SoftExecute = (int)switch_channel_state_t.CS_SOFT_EXECUTE,

        /// <summary>
        /// Channel is executing its dialplan. 
        /// </summary>
        Execute = (int)switch_channel_state_t.CS_EXECUTE,

        /// <summary>
        /// Channel is exchanging media with another channel.
        /// </summary>
        ExchangingMedia = (int)switch_channel_state_t.CS_EXCHANGE_MEDIA,

        /// <summary>
        /// Channel is accepting media awaiting commands. 
        /// </summary>
        Park = (int)switch_channel_state_t.CS_PARK,

        /// <summary>
        /// Channel is consuming all media and dropping it. 
        /// </summary>
        ConsumingMedia = (int)switch_channel_state_t.CS_CONSUME_MEDIA,

        /// <summary>
        /// Channel is in a sleep state. 
        /// </summary>
        Hibernate = (int)switch_channel_state_t.CS_HIBERNATE,

        /// <summary>
        /// Channel is in a reset state. 
        /// </summary>
        Reset = (int)switch_channel_state_t.CS_RESET,

        /// <summary>
        /// Channel is flagged for hangup and ready to end. Media will now end, and no 
        /// further call routing will occur. 
        /// </summary>
        Hangup = (int)switch_channel_state_t.CS_HANGUP,

        /// <summary>
        /// The channel is already hung up, media is already down, and now it's time 
        /// to do any sort of reporting processes such as CDR logging. 
        /// </summary>
        Reporting = (int)switch_channel_state_t.CS_REPORTING,

        /// <summary>
        /// Channel is ready to be destroyed and out of the state machine. Memory
        /// pools are returned to the core and utilized memory from the channel 
        /// is freed. 
        /// </summary>
        Destroy = (int)switch_channel_state_t.CS_DESTROY,

        /// <summary>
        /// Channel state has not been set or is unknown.
        /// </summary>
        None = (int)switch_channel_state_t.CS_NONE
    }
}
