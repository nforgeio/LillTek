//------------------------------------ -----------------------------------------
// FILE:        BridgeAction.cs
// CONTRIBUTOR: Jeff Lill
// COPYRIGHT:   Copyright (c) 2005-2015 by Jeffrey Lill.  All rights reserved.
// DESCRIPTION: Directs the switch to bridge a call to one or more endpoints.

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Xml.Linq;

using FreeSWITCH;
using FreeSWITCH.Native;

using LillTek.Common;
using LillTek.Telephony.Common;

namespace LillTek.Telephony.NeonSwitch
{
    /// <summary>
    /// Directs the switch to bridge a call to one or more endpoints.
    /// </summary>
    /// <remarks>
    /// <para>
    /// NeonSwitch dialplan handlers may use this action to instruct NeonSwitch
    /// to bridge the call to a specified endpoint or one of multiple possible
    /// endpoints.  
    /// </para>
    /// <para>
    /// Set the <see cref="QueuedDtmfDigits" /> property to specify DTMF digits to be
    /// played in-band after the bridge completes.  You may also set the
    /// <see cref="ToneDuration" /> property to control the time each tone
    /// will be played.
    /// </para>
    /// </remarks>
    public class BridgeAction : SwitchAction
    {
        private string                      dialstring;     // FreeSWITCH style dialstring (or null)
        private ChannelVariableCollection   variables;
        private DialedEndpointList          endpoints;
        private string                      queuedDtmfDigits;
        private Guid                        callID1, callID2;

        /// <summary>
        /// Use this constructor to build a dialplan bridge action based on the other class
        /// properties such as the <see cref="Endpoints" /> list.
        /// </summary>
        public BridgeAction()
        {
            this.dialstring   = null;
            this.Mode         = BridgeMode.LinearHunt;
            this.variables    = new ChannelVariableCollection();
            this.endpoints    = new DialedEndpointList();
            this.ToneDuration = Switch.MinDtmfDuration;
        }

        /// <summary>
        /// Use this constructor to build a bridge action to bridge the specified call
        /// to another endpoint based on the other class properties such as the 
        /// <see cref="Endpoints" /> list.
        /// </summary>
        public BridgeAction(Guid callID)
        {
            this.CallID       = callID;
            this.dialstring   = null;
            this.Mode         = BridgeMode.LinearHunt;
            this.variables    = new ChannelVariableCollection();
            this.endpoints    = new DialedEndpointList();
            this.ToneDuration = Switch.MinDtmfDuration;
        }

        /// <summary>
        /// Use this constructor to bridge two calls based on their
        /// call IDs.
        /// </summary>
        /// <exception cref="ArgumentException">Thrown if either of <paramref name="callID1" /> or <paramref name="callID1" /> are empty.</exception>
        /// <remarks>
        /// <note>
        /// At least one call must have already been answered.
        /// </note>
        /// </remarks>
        public BridgeAction(Guid callID1, Guid callID2)
        {
            const string msg = "Cannot bridge to an empty call ID.";

            if (callID1 == Guid.Empty)
                throw new ArgumentException(msg, "callID1");

            if (callID2 == Guid.Empty)
                throw new ArgumentException(msg, "callID2");

            this.callID1 = callID1;
            this.callID2 = callID2;
        }

        /// <summary>
        /// <b>Dialplan only:</b> Use this constructor to build a bridge action
        /// from a FreeSWITCH style dial string.  <b>All other class properties 
        /// will be ignored</b> when rendering the action.
        /// </summary>
        /// <param name="dialstring">The FreeSWITCH style dialstring.</param>
        /// <remarks>
        /// <note>
        /// Actions created with this constructor may only be used when 
        /// executing a dial plan.  Use <see cref="BridgeAction(Guid,string)" />
        /// instead when executing within an application.
        /// </note>
        /// <para>
        /// This constructor is designed to allow the use of a FreeSWITCH
        /// dial string potentially including the specification of channel
        /// variables using the <b>{...}</b> and <b>[...]</b> syntax.
        /// </para>
        /// </remarks>
        public BridgeAction(string dialstring)
        {
            this.dialstring   = dialstring;
            this.variables    = null;
            this.endpoints    = null;
            this.ToneDuration = Switch.MinDtmfDuration;
        }

        /// <summary>
        /// Use this constructor to build a bridge an existing call to
        /// endpoint specified by a FreeSWITCH style dial string.  <b>All other
        /// class properties will be ignored</b> when rendering the action.
        /// </summary>
        /// <param name="callID">The ID of an answered call.</param>
        /// <param name="dialstring">The FreeSWITCH style dialstring.</param>
        /// <remarks>
        /// <para>
        /// This constructor is designed to allow the use of a FreeSWITCH
        /// dial string potentially including the specification of channel
        /// variables using the <b>{...}</b> and <b>[...]</b> syntax.
        /// </para>
        /// </remarks>
        public BridgeAction(Guid callID, string dialstring)
        {
            this.CallID       = callID;
            this.dialstring   = dialstring;
            this.variables    = null;
            this.endpoints    = null;
            this.ToneDuration = Switch.MinDtmfDuration;
        }

        /// <summary>
        /// Returns the variables to be set on all channels.
        /// </summary>
        /// <remarks>
        /// <note>
        /// This collection is not available for bridge actions created with a FreeSWITCH dial string.
        /// </note>
        /// </remarks>
        public ChannelVariableCollection Variables
        {
            get
            {
                if (variables == null)
                    throw new InvalidOperationException("Cannot access the variables in a BridgeAction instance created with a FreeSWITCH dial string.");

                return variables;
            }
        }

        /// <summary>
        /// Returns the collection of endpoints to be bridged.  Applications can multiple
        /// more endpoints to implement hunting or simultaneous calling.
        /// </summary>
        public DialedEndpointList Endpoints
        {
            get
            {
                if (endpoints == null)
                    throw new InvalidOperationException("Cannot access the endpoints in a BridgeAction instance created with a FreeSWITCH dial string.");

                return endpoints;
            }
        }

        /// <summary>
        /// Describes the behavior to use when bridging to multiple endpoints.
        /// (Defaults to <see cref="BridgeMode.LinearHunt" />.
        /// </summary>
        public BridgeMode Mode { get; private set; }

        /// <summary>
        /// Specifies the answer timeout.
        /// </summary>
        /// <remarks>
        /// <para>
        /// This defaults to <see cref="TimeSpan.Zero" /> which indicates that there is no
        /// timeout.  You may set this to a positive timespan to indicate the maximum time
        /// the bridge will wait for an answer.
        /// </para>
        /// <note>
        /// NeonSwitch will round the time out down to the nearest integer seconds.
        /// </note>
        /// <note>
        /// This property is provided for convienence only, you may also set this
        /// explictly in the <see cref="Variables" /> collection.
        /// </note>
        /// </remarks>
        public TimeSpan? AnswerTimeout { get; set; }

        /// <summary>
        /// Indicates that the call media will bypass the switch and be routed directly'
        /// from one endpoint to the other.
        /// </summary>
        /// <remarks>
        /// <note>
        /// This property is provided for convienence only, you may also set this
        /// explictly in the <see cref="Variables" /> collection.
        /// </note>
        /// </remarks>
        public bool? BypassMedia { get; set; }

        /// <summary>
        /// Used to simulate ringback to internal users while dialing a provider.
        /// </summary>
        /// <remarks>
        /// <note>
        /// This property is provided for convienence only, you may also set this
        /// explictly in the <see cref="Variables" /> collection.
        /// </note>
        /// </remarks>
        public bool? Ringback { get; set; }

        /// <summary>
        /// Used to override the default outbound caller ID name for the caller.
        /// </summary>
        /// <remarks>
        /// <note>
        /// This property is provided for convienence only, you may also set this
        /// explictly in the <see cref="Variables" /> collection.
        /// </note>
        /// </remarks>
        public string CallerIDName { get; set; }

        /// <summary>
        /// Used to override the default outbound caller ID number for the caller.
        /// </summary>
        /// <remarks>
        /// <note>
        /// This property is provided for convienence only, you may also set this
        /// explictly in the <see cref="Variables" /> collection.
        /// </note>
        /// </remarks>
        public string CallerIDNumber { get; set; }

        /// <summary>
        /// Specifies DTMF digits to be played in-band after the bridge operation completes.
        /// </summary>
        public string QueuedDtmfDigits
        {
            get { return queuedDtmfDigits; }

            set
            {
                if (value == null)
                {
                    queuedDtmfDigits = null;
                    return;
                }

                queuedDtmfDigits = Dtmf.Validate(value);
            }
        }

        /// <summary>
        /// Specifies the length of time each DTMF tone will be played.  This defaults to <see cref="Switch.MinDtmfDuration" />.
        /// </summary>
        public TimeSpan ToneDuration { get; private set; }

        /// <summary>
        /// Renders the high-level switch action instance into zero or more <see cref="SwitchExecuteAction" />
        /// instances and then adds these to the <see cref="ActionRenderingContext" />.<see cref="ActionRenderingContext.Actions" />
        /// collection.
        /// </summary>
        /// <param name="context">The action rendering context.</param>
        /// <exception cref="NotSupportedException">Thrown for parameters that are not supported for the current execution context.</exception>
        /// <remarks>
        /// <note>
        /// It is perfectly reasonable for an action to render no actions to the
        /// context or to render multiple actions based on its properties.
        /// </note>
        /// </remarks>
        public override void Render(ActionRenderingContext context)
        {
            if (callID1 != Guid.Empty && callID2 != Guid.Empty)
            {
                context.Actions.Add(new SwitchExecuteAction("uuid_bridge", "{0:D} {1:D}", callID1, callID2));
                return;
            }

            // Special case FreeSWITCH style dialstrings.

            if (dialstring != null)
            {
                if (context.IsDialplan)
                    context.Actions.Add(new SwitchExecuteAction("bridge", dialstring));
                else
                    context.Actions.Add(new SwitchExecuteAction(CallID, "bridge", dialstring));

                return;
            }

            if (endpoints.Count == 0)
                throw new InvalidOperationException("Attempt to render a BridgeAction with no endpoints.");

            // Add an action queue any DTMF digits.

            if (queuedDtmfDigits != null && queuedDtmfDigits.Length > 0)
            {
                var duration = ToneDuration;

                if (duration < Switch.MinDtmfDuration)
                    duration = Switch.MinDtmfDuration;
                else if (duration > Switch.MaxDtmfDuration)
                    duration = Switch.MaxDtmfDuration;

                if (context.IsDialplan)
                    context.Actions.Add(new SwitchExecuteAction("queue_dtmf", "{0}@{1}", queuedDtmfDigits, (int)duration.TotalMilliseconds));
                else
                    context.Actions.Add(new SwitchExecuteAction(CallID, "queue_dtmf", "{0}@{1}", queuedDtmfDigits, (int)duration.TotalMilliseconds));
            }

            // Add any propety variables to the variables collection.

            if (AnswerTimeout.HasValue)
                Variables["call_timeout"] = SwitchHelper.GetScheduleSeconds(AnswerTimeout.Value).ToString();

            if (BypassMedia.HasValue)
                Variables["bypass_media"] = BypassMedia.Value ? "true" : "false";

            if (Ringback.HasValue && Ringback.Value)
                Variables["ringback"] = "${us-ring}";

            if (CallerIDName != null)
                Variables["effective_caller_id_name"] = CallerIDName;

            if (CallerIDNumber != null)
                Variables["effective_caller_id_number"] = CallerIDNumber;

            // I'm not going to use the FreeSWITCH global channel variable syntax {..} at the beginning of
            // the dialstring since this is really just a human friendly shortcut.  Instead, I'm going
            // to explicitly add any global variables that aren't overridden to each endpoint.

            if (Variables.Count > 0)
            {
                foreach (var endpoint in Endpoints)
                {
                    foreach (var variable in Variables)
                        if (!endpoint.Variables.ContainsKey(variable.Key))
                            endpoint.Variables[variable.Key] = variable.Value;
                }
            }

            // Render the dialstring if there's only one endpoint.

            if (endpoints.Count == 1)
            {
                if (context.IsDialplan)
                    context.Actions.Add(new SwitchExecuteAction("bridge", endpoints[0].ToString()));
                else
                    context.Actions.Add(new SwitchExecuteAction(CallID, "bridge", endpoints[0].ToString()));

                return;
            }

            // We have multiple endpoints so we need to render based on the bridge mode.

            var sb = new StringBuilder();

            switch (Mode)
            {
                case BridgeMode.LinearHunt:

                    foreach (var endpoint in endpoints)
                        sb.AppendFormat("{0},", endpoint);

                    break;

                case BridgeMode.RandomHunt:

                    endpoints.Shuffle();
                    foreach (var endpoint in endpoints)
                        sb.AppendFormat("{0},", endpoint);

                    break;

                case BridgeMode.RingAll:

                    foreach (var endpoint in endpoints)
                        sb.AppendFormat("{0}|", endpoint);

                    break;
            }

            if (sb.Length > 0)
                sb.Length--;

            if (context.IsDialplan)
                context.Actions.Add(new SwitchExecuteAction("bridge", sb.ToString()));
            else
                context.Actions.Add(new SwitchExecuteAction(CallID, "bridge", sb.ToString()));
        }
    }
}
