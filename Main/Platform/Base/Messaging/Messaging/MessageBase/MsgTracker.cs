//-----------------------------------------------------------------------------
// FILE:        MsgTracker.cs
// CONTRIBUTOR: Jeff Lill
// COPYRIGHT:   Copyright (c) 2005-2015 by Jeffrey Lill.  All rights reserved.
// DESCRIPTION: Holds tracking information about a message for which we're 
//              expecting a ReceiptMsg to be delivered back to the forwarding
//              router.

using System;
using System.Collections.Generic;
using System.Net;
using System.Text;

using LillTek.Common;
using LillTek.Messaging.Internal;

// $todo(jeff.lill): 
//
// It looks like there are some problems with dead router detection in real
// applications.  It looks like it might be too sensitive resulting in
// detections when there doesn't really seem to be a problem.  I need to
// rethink the entire implementation.

namespace LillTek.Messaging
{
    /// <summary>
    /// Holds tracking information about a message for which we're expecting a <see cref="ReceiptMsg" /> 
    /// to be delivered back to the forwarding router.
    /// </summary>
    /// <remarks>
    /// <para>
    /// The <see cref="MsgRouter" /> class instantiates a MsgTracker instance that tracks
    /// whether or not <see cref="ReceiptMsg" /> messages are not received in time, thus
    /// detecting what appears to be a dead router.
    /// </para>
    /// <para>
    /// The message router will call one of the <see cref="Track(LogicalRoute,Msg)" /> or
    /// <see cref="Track(PhysicalRoute,Msg)" /> methods for each message being routed through
    /// the router to what appears to be the destination router.  The MsgTracker records the
    /// fact that we're expecting to see a <see cref="ReceiptMsg" /> and the maximum time
    /// to wait for the message specified by <see cref="MsgRouter.DeadRouterTTL" />.  Note that
    /// setting <see cref="MsgRouter.DeadRouterTTL" /> to <see cref="TimeSpan.Zero" /> will
    /// disable dead router detection.
    /// </para>
    /// <para>
    /// The message router will call <see cref="OnReceiptMsg" /> for each <see cref="ReceiptMsg" />
    /// is receives.  The MsgTracker will record the fact the receipt was received and discontinue
    /// tracking for that message delivery.
    /// </para>
    /// <para>
    /// The message router will also call <see cref="DetectDeadRouters" /> periodically on
    /// a background thread.  This method looks for any messages being tracked where no
    /// <see cref="ReceiptMsg" /> was received in time.  In each of these instances, the
    /// MsgTracker will call the router's <see cref="MsgRouter.OnDeadRouterDetected" /> method, enabling
    /// the router to take further action.
    /// </para>
    /// </remarks>
    internal sealed class MsgTracker
    {
        private MsgRouter                   router;     // The assoicated message router
        private Dictionary<Guid, MsgTrack>  msgTracks;  // Hash table of MsgTrack instances keyed by MsgID

        /// <summary>
        /// Initializes a MsgTracker instance.
        /// </summary>
        /// <param name="router">The associated router.</param>
        public MsgTracker(MsgRouter router)
        {
            this.router    = router;
            this.msgTracks = new Dictionary<Guid, MsgTrack>();
        }

        /// <summary>
        /// Initiates tracking for <see cref="ReceiptMsg" /> messages received
        /// by the associated router.
        /// </summary>
        /// <param name="route">The logical route the message is being forwarded to.</param>
        /// <param name="msg">The message being tracked.</param>
        public void Track(LogicalRoute route, Msg msg)
        {
            MsgTrack track;

            if (!router.DeadRouterDetection)
                return;

            Assertion.Test((msg._Flags & MsgFlag.ReceiptRequest) != 0, "Message is not properly configured for receipt tracking");
            Assertion.Test(msg._MsgID != Guid.Empty, "Message is not properly configured for receipt tracking");

            track = new MsgTrack(route.PhysicalRoute.RouterEP, route.PhysicalRoute.LogicalEndpointSetID, msg._MsgID, SysTime.Now + router.DeadRouterTTL);
            using (TimedLock.Lock(router.SyncRoot))
            {
                msgTracks.Add(msg._MsgID, track);
            }
        }

        /// <summary>
        /// Initiates tracking for <see cref="ReceiptMsg" /> messages received
        /// by the associated router.
        /// </summary>
        /// <param name="route">The physical route the message is being forwarded to.</param>
        /// <param name="msg">The message being tracked.</param>
        public void Track(PhysicalRoute route, Msg msg)
        {
            MsgTrack track;

            if (!router.DeadRouterDetection)
                return;

            Assertion.Test((msg._Flags & MsgFlag.ReceiptRequest) != 0, "Message is not properly configured for receipt tracking");
            Assertion.Test(msg._MsgID != Guid.Empty, "Message is not properly configured for receipt tracking");

            track = new MsgTrack(route.RouterEP, route.LogicalEndpointSetID, msg._MsgID, SysTime.Now + router.DeadRouterTTL);
            using (TimedLock.Lock(router.SyncRoot))
            {
                msgTracks.Add(msg._MsgID, track);
            }
        }

        /// <summary>
        /// Resets the tracker by removing all MsgTrack entries.
        /// </summary>
        public void Clear()
        {
            using (TimedLock.Lock(router.SyncRoot))
            {
                msgTracks.Clear();
            }
        }

        /// <summary>
        /// Concludes tracking for the message whose <see cref="ReceiptMsg" /> is passed.
        /// </summary>
        /// <param name="msg">The received receipt message.</param>
        /// <remarks>
        /// This method is called by the associated <see cref="MsgRouter" /> instance
        /// whenever it receives a <see cref="ReceiptMsg" />.  Note that this method
        /// will call the router's <see cref="MsgRouter.OnDeadRouterDetected" /> method
        /// if a track is still active for the original message and the receipt message
        /// indicates a delivery failure.
        /// </remarks>
        public void OnReceiptMsg(ReceiptMsg msg)
        {
            MsgTrack track;

            using (TimedLock.Lock(router.SyncRoot))
            {
                if (!msgTracks.TryGetValue(msg.ReceiptID, out track))
                    return;

                msgTracks.Remove(msg.ReceiptID);
            }
        }

        /// <summary>
        /// Looks for what appear to be dead routers and calls the associated router's
        /// <see cref="MsgRouter.OnDeadRouterDetected" /> method for each dead router
        /// it finds.
        /// </summary>
        /// <remarks>
        /// This is to be called periodically as one of the router's background tasks.
        /// </remarks>
        public void DetectDeadRouters()
        {
            var deadTracks = new List<MsgTrack>();
            var now        = SysTime.Now;

            using (TimedLock.Lock(router.SyncRoot))
            {
                foreach (MsgTrack track in msgTracks.Values)
                    if (track.TTD <= now)
                        deadTracks.Add(track);

                for (int i = 0; i < deadTracks.Count; i++)
                    msgTracks.Remove(deadTracks[i].MsgID);
            }

            for (int i = 0; i < deadTracks.Count; i++)
                router.OnDeadRouterDetected(deadTracks[i].RouterEP, deadTracks[i].LogicalEndpointSetID);
        }
    }
}
