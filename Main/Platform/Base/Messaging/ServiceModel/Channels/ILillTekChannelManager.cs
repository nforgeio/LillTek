//-----------------------------------------------------------------------------
// FILE:        ILillTekChannelManager.cs
// CONTRIBUTOR: Jeff Lill
// COPYRIGHT:   Copyright (c) 2005-2015 by Jeffrey Lill.  All rights reserved.
// DESCRIPTION: Defines some internal channel manager extensions.

using System;
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
    /// Defines some internal channel manager extensions.
    /// </summary>
    interface ILillTekChannelManager
    {
        /// <summary>
        /// Called by LillTek channels accepted by this listener when the
        /// channel is closed or aborted to terminate any pending operations.
        /// </summary>
        /// <param name="channel">The closed or aborted channel.</param>
        /// <param name="e">The exception to be used to terminate the operation.</param>
        void OnChannelCloseOrAbort(LillTekChannelBase channel, Exception e);
    }
}
