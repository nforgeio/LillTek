//-----------------------------------------------------------------------------
// FILE:        PayloadSizeEstimator.cs
// CONTRIBUTOR: Jeff Lill
// COPYRIGHT:   Copyright (c) 2005-2015 by Jeffrey Lill.  All rights reserved.
// DESCRIPTION: Used to track the payload sizes for the past few messages sent
//              on a channel to estimate the initial capacity of the stream 
//              used to serialize the next message.

using System;
using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Threading;
using System.Collections.Generic;
using System.ServiceModel;
using System.ServiceModel.Channels;
using System.ServiceModel.Description;

using LillTek.Common;
using LillTek.Messaging;
using LillTek.ServiceModel;

namespace LillTek.ServiceModel.Channels
{
    /// <summary>
    /// Used to track the payload sizes for the past few messages sent
    /// on a channel to estimate the initial capacity of the stream 
    /// used to serialize the next message.
    /// </summary>
    internal sealed class PayloadSizeEstimator
    {
        private object          syncLock = new object();
        private RingBuffer<int> payloadSizes;   // Tracks the byte size last few message payloads

        /// <summary>
        /// Constructor.
        /// </summary>
        /// <param name="cPayloadSamples">The number of payload size samples to track.</param>
        public PayloadSizeEstimator(int cPayloadSamples)
        {
            payloadSizes = new RingBuffer<int>(cPayloadSamples);
        }

        /// <summary>
        /// Estimates the initial capacity to use for the next message serialization buffer
        /// for a channel.
        /// </summary>
        /// <returns>The buffer size in bytes.</returns>
        /// <remarks>
        /// <para>
        /// This method performs a (primitive) calculation to balance between allocating
        /// too large a buffer and wasting memory or allocation too small a buffer and 
        /// incuring reallocation overhead.
        /// </para>
        /// <para>
        /// This mechanism works pretty well for situations where there isn't a huge
        /// variation in message sizes.
        /// </para>
        /// </remarks>
        public int EstimateNextBufferSize()
        {
            // The current algorithm returns the maximum of the last
            // 10 payload sizes, rounded up to the nearest 1K.

            int cbMax = 0;

            lock (syncLock)
            {
                for (int i = 0; i < payloadSizes.Count; i++)
                    if (payloadSizes[i] > cbMax)
                        cbMax = payloadSizes[i];
            }

            if (cbMax % 1024 == 0)
                return cbMax;
            else
                return (cbMax / 1024 + 1) * 1024;
        }

        /// <summary>
        /// Channels should call this after serializing a message sent on the
        /// wire, passing the byte size of the message.
        /// </summary>
        /// <param name="cbLastPayload">Size of the last message payload serialized in bytes.</param>
        public void LastPayloadSize(int cbLastPayload)
        {
            lock (syncLock)
                this.payloadSizes.Add(cbLastPayload);
        }
    }
}
