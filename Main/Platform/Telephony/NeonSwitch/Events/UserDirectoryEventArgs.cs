//-----------------------------------------------------------------------------
// FILE:        UserDirectoryEventArgs.cs
// CONTRIBUTOR: Jeff Lill
// COPYRIGHT:   Copyright (c) 2005-2015 by Jeffrey Lill.  All rights reserved.
// DESCRIPTION: Holds the state for a NeonSwitch user directory lookup event.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Text;
using System.Threading;

using FreeSWITCH;
using FreeSWITCH.Native;

using LillTek.Common;
using LillTek.Telephony.Common;

namespace LillTek.Telephony.NeonSwitch
{
    /// <summary>
    /// Holds the state for a NeonSwitch user directory lookup event.
    /// </summary>
    /// <remarks>
    /// <para>
    /// This class defines the arguments passed when the <see cref="Switch.UserDirectoryEvent" />
    /// is raised.  The purpose for this event is to provide the application with the opportunity to
    /// customize how user authentication information is stored.
    /// </para>
    /// <para>
    /// The <see cref="GenericSwitchEventArgs{TEvent}.SwitchEvent" /> property will be set to the 
    /// <see cref="SwitchEvent" /> that triggered the directory lookup and
    /// the <see cref="UserDirectoryEventArgs.Action" />, <see cref="UserDirectoryEventArgs.Domain" />, 
    /// <see cref="UserDirectoryEventArgs.UserID" />,  and <see cref="UserDirectoryEventArgs.IPAddress" /> 
    /// properties will be set to the query parameters.
    /// </para>
    /// <para>
    /// The event handler should use these input properties to identify the user.  If a valid user
    /// is identified, the handler should set <see cref="UserDirectoryEventArgs.Handled" /> to 
    /// <c>true</c>, set <see cref="UserDirectoryEventArgs.Password" /> to the user's password and 
    /// optionally specify custom parameters and variables for the user by adding them to the 
    /// <see cref="UserDirectoryEventArgs.Parameters" /> and <see cref="UserDirectoryEventArgs.Variables" /> collections.
    /// </para>
    /// <para>
    /// If the handler wishes to deny access to the user it should set <see cref="UserDirectoryEventArgs.Handled" />
    /// and <see cref="UserDirectoryEventArgs.AccessDenied" /> to <c>true</c>.
    /// </para>
    /// <para>
    /// Handlers may also decline to look up a user by leaving <see cref="UserDirectoryEventArgs.Handled" /> 
    /// set to  <c>false</c>.  This will allow other handlers, including the the default XML directory
    /// implementation to perform the lookup.
    /// </para>
    /// <para>
    /// Some common parameters and variables can be set using the <see cref="UserDirectoryEventArgs.VoiceMailPassword" />,
    /// <see cref="UserDirectoryEventArgs.CallingRights" />, <see cref="UserDirectoryEventArgs.AccountCode" />,
    /// <see cref="UserDirectoryEventArgs.CallerContext" />, <see cref="UserDirectoryEventArgs.EffectiveCallerIDName" />,
    /// <see cref="UserDirectoryEventArgs.EffectiveCallerIDNumber" />, <see cref="UserDirectoryEventArgs.OutboundCallerIDName" />,
    /// <see cref="UserDirectoryEventArgs.OutboundCallerIDNumber" />, and <see cref="UserDirectoryEventArgs.CallGroup" />
    /// properties.  The switch will automatically add these values to the appropriate collection before
    /// processing the result.
    /// </para>
    /// </remarks>
    public class UserDirectoryEventArgs : GenericSwitchEventArgs<SwitchEvent>
    {
        /// <summary>
        /// Identifies the action that triggered the authentication.
        /// </summary>
        /// <remarks>
        /// This property can probably be safely ignored.  At this point in time,
        /// the only known value passed by the switch is <b>sip_auth</b>.
        /// </remarks>
        public string Action { get; private set; }

        /// <summary>
        /// Returns the user's authentication domain.
        /// </summary>
        public string Domain { get; private set; }

        /// <summary>
        /// Returns the user ID to be queried.
        /// </summary>
        public string UserID { get; private set; }

        /// <summary>
        /// Returns the IP address for the user.
        /// </summary>
        public IPAddress IPAddress { get; private set; }

        /// <summary>
        /// The password to be returned to the switch.
        /// </summary>
        public string Password { get; set; }

        /// <summary>
        /// Indicates that the event was handled by the handler.  
        /// </summary>
        public bool Handled { get; set; }

        /// <summary>
        /// Indicates that the user should be denied access.
        /// </summary>
        public bool AccessDenied { get; set; }

        /// <summary>
        /// This may be set to the digits of the user's voice mail password.
        /// </summary>
        public string VoiceMailPassword { get; set; }

        /// <summary>
        /// This may be set to identify the user's outbound calling rights. 
        /// </summary>
        public CallingRight CallingRights { get; set; }

        /// <summary>
        /// This may be set to identify the accounting code for the user.  This
        /// is used to identify the template to be used for generating CDRs.
        /// </summary>
        public string AccountCode { get; set; }

        /// <summary>
        /// The dialplan context to use when this user places a call.
        /// </summary>
        public string CallerContext { get; set; }

        /// <summary>
        /// This may be set to associate the caller ID name for the user 
        /// while the switch processes the call.
        /// </summary>
        public string EffectiveCallerIDName { get; set; }

        /// <summary>
        /// This may be set to associate the caller ID number for the user 
        /// while the switch processes the call.
        /// </summary>
        public string EffectiveCallerIDNumber { get; set; }

        /// <summary>
        /// This may be set to the caller ID name for outbound calls bridged
        /// by the switch.
        /// </summary>
        public string OutboundCallerIDName { get; set; }

        /// <summary>
        /// This may be set to the caller ID number for outbound calls bridged
        /// by the switch.
        /// </summary>
        public string OutboundCallerIDNumber { get; set; }

        /// <summary>
        /// This may be set to indicate that this user is part of a group whose
        /// phones will all ring on an inbound call and any of the users may pick up.
        /// </summary>
        public string CallGroup { get; set; }

        /// <summary>
        /// The parameters to be associated with the user.
        /// </summary>
        public ArgCollection Parameters { get; private set; }

        /// <summary>
        /// The variables to be associated with the user.
        /// </summary>
        public ArgCollection Variables { get; private set; }

        /// <summary>
        /// Constructor.
        /// </summary>
        internal UserDirectoryEventArgs(SwitchEvent switchEvent)
            : base(switchEvent)
        {
            this.Action                  = switchEvent.Headers.Get("action", string.Empty).ToLower();
            this.Domain                  = switchEvent.Headers.Get("domain", string.Empty);
            this.UserID                  = switchEvent.Headers.Get("user", string.Empty);
            this.IPAddress               = switchEvent.Headers.Get("ip", IPAddress.Any);
            this.Password                = null;
            this.Handled                 = false;
            this.AccessDenied            = false;
            this.VoiceMailPassword       = null;
            this.CallingRights           = CallingRight.None;
            this.AccountCode             = null;
            this.EffectiveCallerIDName   = null;
            this.EffectiveCallerIDNumber = null;
            this.OutboundCallerIDName    = null;
            this.OutboundCallerIDNumber  = null;
            this.CallGroup               = null;
            this.Parameters              = new ArgCollection(ArgCollectionType.Unconstrained);
            this.Variables               = new ArgCollection(ArgCollectionType.Unconstrained);
        }
    }
}
