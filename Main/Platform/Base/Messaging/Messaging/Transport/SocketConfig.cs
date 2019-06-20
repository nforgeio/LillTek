//-----------------------------------------------------------------------------
// FILE:        SocketConfig.cs
// CONTRIBUTOR: Jeff Lill
// COPYRIGHT:   Copyright (c) 2005-2015 by Jeffrey Lill.  All rights reserved.
// DESCRIPTION: Holds socket configuration information.

using System;

using LillTek.Common;

namespace LillTek.Messaging
{
    /// <summary>
    /// Holds socket configuration information.
    /// </summary>
    public sealed class SocketConfig
    {
        private int cbSendBuf;
        private int cbRecvBuf;

        /// <summary>
        /// Constructor.
        /// </summary>
        /// <param name="cbSendBuf">Size of a socket's send buffer in bytes.</param>
        /// <param name="cbRecvBuf">Size of a socket's receive buffer in bytes.</param>
        public SocketConfig(int cbSendBuf, int cbRecvBuf)
        {
            this.cbSendBuf = cbSendBuf;
            this.cbRecvBuf = cbRecvBuf;
        }

        /// <summary>
        /// Returns the send buffer size in bytes.
        /// </summary>
        public int SendBufferSize
        {
            get { return cbSendBuf; }
        }

        /// <summary>
        /// Returns the receive buffer size in bytes.
        /// </summary>
        public int ReceiveBufferSize
        {
            get { return cbRecvBuf; }
        }
    }
}