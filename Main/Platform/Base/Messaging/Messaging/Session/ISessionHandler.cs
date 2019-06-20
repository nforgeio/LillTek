//-----------------------------------------------------------------------------
// FILE:        ISessionHandler.cs
// CONTRIBUTOR: Jeff Lill
// COPYRIGHT:   Copyright (c) 2005-2015 by Jeffrey Lill.  All rights reserved.
// DESCRIPTION: Defines types used for the service side implementation of advanced session 
//              scenarios.

using System;
using System.Reflection;

namespace LillTek.Messaging
{
    /// <summary>
    /// Defines types used for the service side implementation of advanced session scenarios.
    /// </summary>
    /// <remarks>
    /// See the <see cref="ISessionHandler" /> documentation for how this is used.
    /// </remarks>
    public interface ISessionHandler
    {
        /// <summary>
        /// Returns the associated <see cref="ISession" />.
        /// </summary>
        ISession Session { get; }
    }
}
