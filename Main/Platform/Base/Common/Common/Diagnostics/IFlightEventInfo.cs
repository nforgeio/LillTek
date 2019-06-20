//-----------------------------------------------------------------------------
// FILE:        IFlightEventInfo.cs
// CONTRIBUTOR: Jeff Lill
// COPYRIGHT:   Copyright (c) 2005-2015 by Jeffrey Lill.  All rights reserved.
// DESCRIPTION: Interface used by the FlightRecorder to serialize and deseralize
//              flight event operation and details into plain-old object instances.

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;

namespace LillTek.Common
{
    /// <summary>
    /// Interface used by the FlightRecorder to serialize and deseralize
    /// flight event operation and details into plain-old object instances.
    /// </summary>
    public interface IFlightEventInfo
    {
        /// <summary>
        /// Returns the flight event operation.
        /// </summary>
        string SerializeOperation();

        /// <summary>
        /// Serializes the instance into a string that can be saved in a
        /// <see cref="FlightEvent" />.
        /// </summary>
        /// <returns>The serialized instance.</returns>
        string SerializeDetails();

        /// <summary>
        /// Unserializes the operation and details from a <see cref="FlightEvent" />
        /// into the current instance.
        /// </summary>
        /// <param name="flightEvent">The event being deseralized.</param>
        void Deserialize(FlightEvent flightEvent);
    }
}
