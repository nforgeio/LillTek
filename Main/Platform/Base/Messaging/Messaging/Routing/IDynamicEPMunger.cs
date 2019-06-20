//-----------------------------------------------------------------------------
// FILE:        IDynamicEPMunger.cs
// CONTRIBUTOR: Jeff Lill
// COPYRIGHT:   Copyright (c) 2005-2015 by Jeffrey Lill.  All rights reserved.
// DESCRIPTION: Defines the behavior of a class that capable of modifying
//              the endpoint registered with a dispatcher for a message handler 
//              tagged with [MsgSession(DynamicScope="scope-name")].

using System;

namespace LillTek.Messaging
{
    /// <summary>
    /// Defines the behavior of a class that capable of modifying the endpoint registered 
    /// with a dispatcher for a message handler tagged with <c>[MsgHandler(DynamicScope="scope-name")]</c>.
    /// </summary>
    /// <remarks>
    /// <para>
    /// This concept is used by the <see cref="IMsgDispatcher.AddTarget(object,string,IDynamicEPMunger,object)" />
    /// method to allow a way for message handler endpoints to modified dynamically at runtime
    /// before being added to a <see cref="MsgRouter" />'s <see cref="IMsgDispatcher" />.  This
    /// functionality is required to implement the LillTek.Datacenter plugable topology capabilities.
    /// </para>
    /// <para>
    /// The interface exposes only one method: <see cref="Munge" />.  This method will be called 
    /// within <see cref="IMsgDispatcher.AddTarget(object,string,IDynamicEPMunger,object)" /> for message 
    /// handlers with logical endpoints that match the specified dynamic scope name.
    /// </para>
    /// </remarks>
    public interface IDynamicEPMunger
    {
        /// <summary>
        /// Dynamically modifies a message handler's endpoint just before it is registered
        /// with a <see cref="MsgRouter" />'s <see cref="IMsgDispatcher" />.
        /// </summary>
        /// <param name="logicalEP">The message handler's logical endpoint.</param>
        /// <param name="handler">The message handler information.</param>
        /// <returns>The logical endpoint to actually register for the message handler.</returns>
        MsgEP Munge(MsgEP logicalEP, MsgHandler handler);
    }
}
