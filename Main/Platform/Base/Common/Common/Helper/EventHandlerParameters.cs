//-----------------------------------------------------------------------------
// FILE:        EventHandlerParameters.cs
// CONTRIBUTOR: Jeff Lill
// COPYRIGHT:   Copyright (c) 2005-2015 by Jeffrey Lill.  All rights reserved.
// DESCRIPTION: Used to hold the parameters passed to an EventHandler.

using System;

namespace LillTek.Common
{
    /// <summary>
    /// Used to hold the parameters passed to an <see cref="EventHandler" />.
    /// </summary>
    public class EventHandlerParameters
    {
        /// <summary>
        /// Identifies the source of the raised event.
        /// </summary>
        public object Sender { get; private set; }

        /// <summary>
        /// The raised event arguments.
        /// </summary>
        public EventArgs Args { get; private set; }

        /// <summary>
        /// Constructor.
        /// </summary>
        /// <param name="sender">Identifies the source of the raised event.</param>
        /// <param name="args">The raised event arguments.</param>
        public EventHandlerParameters(object sender, EventArgs args)
        {
            this.Sender = sender;
            this.Args   = args;
        }
    }
}
