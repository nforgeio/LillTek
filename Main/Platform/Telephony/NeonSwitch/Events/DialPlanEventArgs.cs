//-----------------------------------------------------------------------------
// FILE:        DialPlanEventArgs.cs
// CONTRIBUTOR: Jeff Lill
// COPYRIGHT:   Copyright (c) 2005-2015 by Jeffrey Lill.  All rights reserved.
// DESCRIPTION: Holds the state for NeonSwitch dial plan lookup event.

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
    /// Holds the state for NeonSwitch dial plan lookup event.
    /// </summary>
    /// <remarks>
    /// <para>
    /// This class defines the arguments passed when the <see cref="Switch.DialPlanEvent" />
    /// is raised.  The purpose for this event is to provide the application with opportunity to
    /// implement a totally customized dial plan.
    /// </para>
    /// <para>
    /// The <see cref="GenericSwitchEventArgs{TEvent}.SwitchEvent" /> property will be set to the 
    /// <see cref="SwitchEvent" /> that triggered the raising of this
    /// event.  The application should examine the headers of this class and the headers in the
    /// <see cref="SwitchEvent" /> to make its call routing decisions.
    /// If the application decides to route the call, it should set <see cref="Handled" /><c>=true</c>, 
    /// set <see cref="Context" /> to a valid dial plan context name, and add the 
    /// <see cref="SwitchAction" />s to be performed to the <see cref="Actions" /> list.
    /// </para>
    /// <para>
    /// Several commonly used switch event headers are made available as properties of this
    /// class.  These include: 
    /// </para>
    /// <para>
    /// A <see cref="SwitchAction" /> specifies the NeonSwitch application and optional parameters
    /// to be performed on the call.  See the FreeSWITCH <a href="http://wiki.freeswitch.org/wiki/Dialplan_XML">documentation</a> 
    /// for more information about actions.
    /// </para>
    /// <para>
    /// Applications that were not able to map the incoming event to dial plan actions
    /// should leave <see cref="Handled" /> set to <c>false</c> and return.
    /// </para>
    /// </remarks>
    public class DialPlanEventArgs : GenericSwitchEventArgs<SwitchEvent>
    {
        /// <summary>
        /// Returns the unique ID for the call.
        /// </summary>
        public Guid CallID { get; private set; }

        /// <summary>
        /// Returns the name of the call context for the caller.
        /// </summary>
        public string Context { get; private set; }

        /// <summary>
        /// The caller's phone number or <c>null</c> if this is unknown.
        /// </summary>
        /// <remarks>
        /// ANI stands for Automatic Number Identification which is just a fancy
        /// way of saying phone number.
        /// </remarks>
        public string CallerAni { get; private set; }

        /// <summary>
        /// Returns the caller ID number or <c>null</c> if unknown.
        /// </summary>
        public string CallerIDNumber { get; private set; }

        /// <summary>
        /// Returns the caller's name or <c>null</c> if unknown.
        /// </summary>
        public string CallerIDName { get; private set; }

        /// <summary>
        /// Returns the number being called.
        /// </summary>
        public string DialedNumber { get; private set; }

        /// <summary>
        /// Returns the caller's IP address or <see cref="IPAddress.Any"/> if not known.
        /// </summary>
        public IPAddress CallerNetworkAddress { get; private set; }

        /// <summary>
        /// Indicates that the event was handled by the handler.  
        /// </summary>
        public bool Handled { get; set; }

        /// <summary>
        /// Actions added by the event handler to be performed by NeonSwitch on the call.
        /// </summary>
        public List<SwitchAction> Actions { get; private set; }

        /// <summary>
        /// Variables defined by the event handler to be persisted into the call.
        /// </summary>
        public ArgCollection Variables { get; private set; }

        /// <summary>
        /// Constructor.
        /// </summary>
        internal DialPlanEventArgs(SwitchEvent switchEvent)
            : base(switchEvent)
        {
            this.Handled   = false;
            this.Actions   = new List<SwitchAction>();
            this.Variables = new ArgCollection(ArgCollectionType.Unconstrained);

            // Get common properties from the event headers.

            this.CallID               = switchEvent.Headers.Get("Caller-Unique-ID", Guid.Empty);
            this.Context              = switchEvent.Headers.Get("Caller-Context", string.Empty);
            this.CallerIDName         = switchEvent.Headers.Get("Caller-ANI");
            this.CallerIDName         = switchEvent.Headers.Get("Caller-Caller-ID-Name");
            this.CallerIDNumber       = switchEvent.Headers.Get("Caller-Caller-ID-Number");
            this.DialedNumber         = switchEvent.Headers.Get("Caller-Destination-Number");
            this.CallerNetworkAddress = switchEvent.Headers.Get("Caller-Network-Addr", IPAddress.Any);
        }
    }
}
