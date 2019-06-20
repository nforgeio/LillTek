//-----------------------------------------------------------------------------
// FILE:        SwitchEventCodeSet.cs
// CONTRIBUTOR: Jeff Lill
// COPYRIGHT:   Copyright (c) 2005-2015 by Jeffrey Lill. All rights reserved.
// DESCRIPTION: Specifies a set of SwitchEventCodes to be used for operations
//              like SwitchConnection.Subscribe().

using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Security;
using System.ServiceModel;
using System.Text;
using System.Threading;

using LillTek.Advanced;
using LillTek.Common;
using LillTek.Net.Sockets;

namespace LillTek.Telephony.Common
{
    /// <summary>
    /// Specifies a set of <see cref="SwitchEventCode" />s to be used for operations
    /// like <see cref="SwitchConnection" />.<see cref="SwitchConnection.Subscribe" />.
    /// </summary>
    /// <threadsafety instance="false" />
    public class SwitchEventCodeSet : IEnumerable<SwitchEventCode>
    {
        //---------------------------------------------------------------------
        // Static members

        /// <summary>
        /// Returns the maximum possible switch event code.
        /// </summary>
        public static readonly SwitchEventCode MaxSwitchEventCode;

        /// <summary>
        /// Returns a <b>read-only</b> empty set.
        /// </summary>
        public static SwitchEventCodeSet Empty { get; private set; }

        /// <summary>
        /// Returns a <b>read-only</b> set of all known event codes.
        /// </summary>
        public static SwitchEventCodeSet AllEvents { get; private set; }

        /// <summary>
        /// Returns a <b>read-only</b> set of events that that cannot be subscribed to
        /// explicitly but appear to be implicitly raised by FreeSWITCH based on other
        /// event subscriptions.  This is used internally to mask off these events 
        /// from any subscription commands sent to FreeSWITCH.
        /// </summary>
        internal static SwitchEventCodeSet ImplicitEvents { get; set; }

        /// <summary>
        /// Returns a <b>read-only</b> set of all channel related events.
        /// </summary>
        public static SwitchEventCodeSet ChannelEvents { get; private set; }

        /// <summary>
        /// Compares two sets for equality.
        /// </summary>
        /// <param name="set1">Set #1.</param>
        /// <param name="set2">Set #2</param>
        /// <returns><c>true</c> if the sets are equal.</returns>
        public static bool operator ==(SwitchEventCodeSet set1, SwitchEventCodeSet set2)
        {
            var ref1 = (object)set1;
            var ref2 = (object)set2;

            if (object.ReferenceEquals(ref1, ref2))
                return true;
            else if (ref1 == null && ref2 == null)
                return true;
            else if (ref1 == null || ref2 == null)
                return false;

            for (int i = 0; i < set1.eventSet.Length; i++)
                if (set1.eventSet[i] != set2.eventSet[i])
                    return false;

            return true;
        }

        /// <summary>
        /// Compares two sets for inequality.
        /// </summary>
        /// <param name="set1">Set #1.</param>
        /// <param name="set2">Set #2</param>
        /// <returns><c>true</c> if the sets are not equal.</returns>
        public static bool operator !=(SwitchEventCodeSet set1, SwitchEventCodeSet set2)
        {
            return !(set1 == set2);
        }

        /// <summary>
        /// Static constructor.
        /// </summary>
        static SwitchEventCodeSet()
        {
            // Compute the maximum possible event code.

            int maxCode = 0;

            foreach (var code in Enum.GetValues(typeof(SwitchEventCode)))
                if ((int)code > maxCode)
                    maxCode = (int)code;

            MaxSwitchEventCode = (SwitchEventCode)maxCode;

            //---------------------------------------------
            // Construct Empty

            Empty = new SwitchEventCodeSet() { IsReadOnly = true };

            //---------------------------------------------
            // Construct AllEvents

            AllEvents = new SwitchEventCodeSet();

            foreach (SwitchEventCode code in Enum.GetValues(typeof(SwitchEventCode)))
                if (code != SwitchEventCode.AllEvents)
                    AllEvents.Add(code);

            AllEvents.IsReadOnly = true;

            //---------------------------------------------
            // Construct ChannelEvents

            ChannelEvents = new SwitchEventCodeSet(

                SwitchEventCode.ChannelCreate,
                SwitchEventCode.ChannelDestroy,
                SwitchEventCode.ChannelState,
                SwitchEventCode.ChannelCallState,
                SwitchEventCode.ChannelAnswer,
                SwitchEventCode.ChannelHangup,
                SwitchEventCode.ChannelHangupComplete,
                SwitchEventCode.ChannelExecute,
                SwitchEventCode.ChannelExecuteComplete,
                SwitchEventCode.ChannelHold,
                SwitchEventCode.ChannelUnhold,
                SwitchEventCode.ChannelBridge,
                SwitchEventCode.ChannelUnbridge,
                SwitchEventCode.ChannelProgress,
                SwitchEventCode.ChannelProgressMedia,
                SwitchEventCode.ChannelOutgoing,
                SwitchEventCode.ChannelPark,
                SwitchEventCode.ChannelUnpark,
                SwitchEventCode.ChannelAction,
                SwitchEventCode.ChannelOriginate,
                SwitchEventCode.ChannelUUID)
                {

                    IsReadOnly = true
                };

            //-----------------------------------------------------------------
            // Construct ImplicitEvents

            ImplicitEvents = new SwitchEventCodeSet(

                SwitchEventCode.Custom,
                SwitchEventCode.InboundChannel,
                SwitchEventCode.OutboundChannel,
                SwitchEventCode.SessionCrash,
                SwitchEventCode.ModuleLoad,
                SwitchEventCode.ModuleUnload,
                SwitchEventCode.PresenceIn,
                SwitchEventCode.PresenceNotifyIn,
                SwitchEventCode.PresenceOut,
                SwitchEventCode.PresenceProbe,
                SwitchEventCode.MessageWaiting,
                SwitchEventCode.MessageWaitingQuery,
                SwitchEventCode.Roster,
                SwitchEventCode.Codec,
                SwitchEventCode.DetectedSpeech,
                SwitchEventCode.DetectedTone,
                SwitchEventCode.PrivateCommand,
                SwitchEventCode.CriticalError,
                SwitchEventCode.AddSchedule,
                SwitchEventCode.RemoveSchedule,
                SwitchEventCode.ExecuteSchedule,
                SwitchEventCode.Reschedule,
                SwitchEventCode.SendMessage,
                SwitchEventCode.RequestParams,
                SwitchEventCode.ChannelData,
                SwitchEventCode.SessionHeartbeat,
                SwitchEventCode.ClientDisconnected,
                SwitchEventCode.ServerDisconnected,
                SwitchEventCode.SendInfo,
                SwitchEventCode.ReceiveInfo,
                SwitchEventCode.ReceiveRTCPMessage,
                SwitchEventCode.CallSecure,
                SwitchEventCode.RecordStart,
                SwitchEventCode.RecordStop,
                SwitchEventCode.PlaybackStart,
                SwitchEventCode.PlaybackStop,
                SwitchEventCode.CallUpdate,
                SwitchEventCode.SocketData,
                SwitchEventCode.MediaBugStart,
                SwitchEventCode.MediaBugStop)
                {
                    IsReadOnly = true
                };
        }

        //---------------------------------------------------------------------
        // Instance members

        private const string    BadEventCodeMsg = "Invalid NeonSwitch event code.";
        private const string    ReadOnlyMsg = "Event set is read-only.";

        private bool[]          eventSet;
        private bool            isReadOnly;

        /// <summary>
        /// Constructs an empty event code set.
        /// </summary>
        public SwitchEventCodeSet()
        {
            this.eventSet   = new bool[(int)MaxSwitchEventCode + 1];
            this.isReadOnly = false;
        }

        /// <summary>
        /// Initializes an event code set with specific values.
        /// </summary>
        /// <param name="eventCodes">The event codes.</param>
        public SwitchEventCodeSet(params SwitchEventCode[] eventCodes)
        {
            this.eventSet = new bool[(int)MaxSwitchEventCode + 1];

            foreach (var code in eventCodes)
            {
                if (code < (SwitchEventCode)0 || code > MaxSwitchEventCode)
                    throw new ArgumentOutOfRangeException("eventCodes", code, BadEventCodeMsg);

                Add(code);
            }
        }

        /// <summary>
        /// Returns a deep clone of the instance.
        /// </summary>
        /// <returns>The cloned instance.</returns>
        /// <remarks>
        /// <note>
        /// The clone of a read-only set is <b>not read-only</b>.
        /// </note>
        /// </remarks>
        public SwitchEventCodeSet Clone()
        {
            var clone = new SwitchEventCodeSet();

            clone.isReadOnly = false;

            for (int i = 0; i < this.eventSet.Length; i++)
                clone.eventSet[i] = this.eventSet[i];

            return clone;
        }

        /// <summary>
        /// Returns a new set that combines the event codes from the current set and
        /// the set passed.
        /// </summary>
        /// <param name="set">The event set to be combined.</param>
        public SwitchEventCodeSet Union(SwitchEventCodeSet set)
        {
            var result = new SwitchEventCodeSet();

            for (int i = 0; i < this.eventSet.Length; i++)
                result.eventSet[i] = this.eventSet[i] || set.eventSet[i];

            return result;
        }

        /// <summary>
        /// Returns a new set that combines the event codes from the current set and
        /// the event codes passed.
        /// </summary>
        /// <param name="eventCodes">The event codes to be combined.</param>
        public SwitchEventCodeSet Union(params SwitchEventCode[] eventCodes)
        {
            return Union(new SwitchEventCodeSet(eventCodes));
        }

        /// <summary>
        /// Returns a new set that includes the event codes that are in
        /// both the current set and the set passed.
        /// </summary>
        /// <param name="set">The event set to be combined.</param>
        public SwitchEventCodeSet Intersect(SwitchEventCodeSet set)
        {
            var result = new SwitchEventCodeSet();

            for (int i = 0; i < this.eventSet.Length; i++)
                result.eventSet[i] = this.eventSet[i] && set.eventSet[i];

            return result;
        }

        /// <summary>
        /// Returns a new set that includes the event codes that are in
        /// both the current set and the array passed.
        /// </summary>
        /// <param name="eventCodes">The event codes to be combined.</param>
        public SwitchEventCodeSet Intersect(params SwitchEventCode[] eventCodes)
        {
            return Intersect(new SwitchEventCodeSet(eventCodes));
        }

        /// <summary>
        /// Returns a new set that includes the event codes that are in
        /// both the current set but not in the set passed.
        /// </summary>
        /// <param name="set">The event set to be combined.</param>
        public SwitchEventCodeSet Difference(SwitchEventCodeSet set)
        {
            var result = new SwitchEventCodeSet();

            for (int i = 0; i < this.eventSet.Length; i++)
                result.eventSet[i] = this.eventSet[i] && !set.eventSet[i];

            return result;
        }

        /// <summary>
        /// Returns a new set that includes the event codes that are in
        /// both the current set but not in the array passed.
        /// </summary>
        /// <param name="eventCodes">The event codes to be combined.</param>
        public SwitchEventCodeSet Difference(params SwitchEventCode[] eventCodes)
        {
            return Difference(new SwitchEventCodeSet(eventCodes));
        }

        /// <summary>
        /// Returns the set of event codes that are not in the current set.
        /// </summary>
        /// <returns></returns>
        public SwitchEventCodeSet Not()
        {
            var result = new SwitchEventCodeSet();

            for (int i = 0; i < this.eventSet.Length; i++)
                result.eventSet[i] = !this.eventSet[i];

            return result;
        }

        /// <summary>
        /// Determines whether the set contains an event code.
        /// </summary>
        /// <param name="eventCode">The code being tested.</param>
        /// <returns><c>true</c> if the event code is in the set.</returns>
        public bool Contains(SwitchEventCode eventCode)
        {
            if (eventCode < (SwitchEventCode)0 || eventCode > MaxSwitchEventCode)
                throw new ArgumentOutOfRangeException("eventCode", eventCode, BadEventCodeMsg);

            return eventSet[(int)eventCode];
        }

        /// <summary>
        /// Returns <c>true</c> if the set contains one or more event codes.
        /// </summary>
        public bool IsEmpty
        {
            get
            {
                for (int i = 0; i < eventSet.Length; i++)
                    if (eventSet[i])
                        return false;

                return true;
            }
        }

        /// <summary>
        /// Indicates whether the set is <b>read-only</b>.
        /// </summary>
        /// <exception cref="InvalidOperationException">
        /// Thrown if the set is <b>read-only</b> and <c>false</c> is passed in an
        /// attempt to make it <b>read-write</b>.
        /// </exception>
        public bool IsReadOnly
        {
            get { return isReadOnly; }

            set
            {
                if (isReadOnly && !value)
                    throw new InvalidOperationException(ReadOnlyMsg);

                isReadOnly = value;
            }
        }

        /// <summary>
        /// Adds an event code to the set.
        /// </summary>
        /// <param name="eventCode">The event code being added.</param>
        /// <exception cref="InvalidOperationException">Thrown if the set is read-only.</exception>
        public void Add(SwitchEventCode eventCode)
        {
            if (isReadOnly)
                throw new InvalidOperationException(ReadOnlyMsg);

            if (eventCode < (SwitchEventCode)0 || eventCode > MaxSwitchEventCode)
                throw new ArgumentOutOfRangeException("eventCode", eventCode, BadEventCodeMsg);

            eventSet[(int)eventCode] = true;
        }

        /// <summary>
        /// Removes an event code from the set.
        /// </summary>
        /// <param name="eventCode">The event code being removed.</param>
        /// <exception cref="InvalidOperationException">Thrown if the set is read-only.</exception>
        public void Remove(SwitchEventCode eventCode)
        {
            if (isReadOnly)
                throw new InvalidOperationException(ReadOnlyMsg);

            if (eventCode < (SwitchEventCode)0 || eventCode > MaxSwitchEventCode)
                throw new ArgumentOutOfRangeException("eventCode", eventCode, BadEventCodeMsg);

            eventSet[(int)eventCode] = false;
        }

        /// <summary>
        /// Removes all event codes from the set.
        /// </summary>
        /// <exception cref="InvalidOperationException">Thrown if the set is read-only.</exception>
        public void Clear()
        {
            if (isReadOnly)
                throw new InvalidOperationException(ReadOnlyMsg);

            for (int i = 0; i < eventSet.Length; i++)
                eventSet[i] = false;
        }

        /// <summary>
        /// Creates a type-safe enumerator over the event codes in the set.
        /// </summary>
        /// <returns>The enumerator.</returns>
        IEnumerator<SwitchEventCode> IEnumerable<SwitchEventCode>.GetEnumerator()
        {
            var list = new List<SwitchEventCode>();

            for (int i = 0; i < eventSet.Length; i++)
                if (eventSet[i])
                    list.Add((SwitchEventCode)i);

            return list.GetEnumerator();
        }

        /// <summary>
        /// Creates an enumerator over the event codes in the set.
        /// </summary>
        /// <returns></returns>
        IEnumerator IEnumerable.GetEnumerator()
        {
            return ((IEnumerable<SwitchEventCode>)this).GetEnumerator();
        }

        /// <summary>
        /// Determines whether the object passed equals this object.
        /// </summary>
        /// <param name="obj">The object to be compared.</param>
        /// <returns><c>true</c> if the objects are equal.</returns>
        public override bool Equals(object obj)
        {
            return this == obj as SwitchEventCodeSet;
        }

        /// <summary>
        /// Computes a hash code for the instance.
        /// </summary>
        /// <returns>The hash code.</returns>
        public override int GetHashCode()
        {
            int hash = 0x7F1D1367;

            foreach (var code in this)
                hash ^= code.GetHashCode();

            return hash;
        }

        /// <summary>
        /// Renders the set as a human readable string.
        /// </summary>
        /// <returns>The rendered string.</returns>
        public override string ToString()
        {
            var sb = new StringBuilder();

            foreach (var code in this)
                sb.AppendFormat("{0} ", code);

            if (sb.Length > 0)
            {
                sb.Length--;
                return sb.ToString();
            }
            else
                return "<empty>";
        }
    }
}
