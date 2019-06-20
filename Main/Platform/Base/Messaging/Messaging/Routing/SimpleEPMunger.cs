//-----------------------------------------------------------------------------
// FILE:        SimpleEPMunger.cs
// CONTRIBUTOR: Jeff Lill
// COPYRIGHT:   Copyright (c) 2005-2015 by Jeffrey Lill.  All rights reserved.
// DESCRIPTION: A simple IDynamicEPMunger implementation that just changes message
//              handler endpoints to a specified endpoint.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

using LillTek.Common;
using LillTek.Messaging;

namespace LillTek.Messaging
{
    /// <summary>
    /// A simple <see cref="IDynamicEPMunger" /> implementation that just changes message
    /// handler endpoints to a specified endpoint.
    /// </summary>
    public class SimpleEPMunger : IDynamicEPMunger
    {
        private MsgEP mungeEP;

        /// <summary>
        /// Constructor.
        /// </summary>
        /// <param name="ep">The endpoint that will be used to replaced existing message handler endpoints.</param>
        public SimpleEPMunger(MsgEP ep)
        {
            this.mungeEP = ep;
        }

        /// <summary>
        /// Dynamically modifies a message handler's endpoint just before it is registered
        /// with a <see cref="MsgRouter" />'s <see cref="IMsgDispatcher" />.
        /// </summary>
        /// <param name="logicalEP">The message handler's logical endpoint.</param>
        /// <param name="handler">The message handler information.</param>
        /// <returns>The logical endpoint to actually register for the message handler.</returns>
        public MsgEP Munge(MsgEP logicalEP, MsgHandler handler)
        {
            return mungeEP;
        }
    }
}
