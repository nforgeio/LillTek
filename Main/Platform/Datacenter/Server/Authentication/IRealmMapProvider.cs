//-----------------------------------------------------------------------------
// FILE:        IRealmMapProvider.cs
// CONTRIBUTOR: Jeff Lill
// COPYRIGHT:   Copyright (c) 2005-2015 by Jeffrey Lill. All rights reserved.
// DESCRIPTION: Defines the behavior necessary for classes that extend the  
//              Authentication Service to load a realm map from a custom source.

using System;
using System.Collections.Generic;

// $todo(jeff.lill): Think about implementing async BeginGetMap() and EndGetMap() calls.

namespace LillTek.Datacenter.Server
{
    /// <summary>
    /// Defines the behavior necessary for classes that extend the 
    /// Authentication Service to load a realm map from a custom source.
    /// </summary>
    /// <threadsafety instance="true" />
    public interface IRealmMapProvider : IDisposable
    {
        /// <summary>
        /// Establishes a session with the realm map provider.
        /// </summary>
        /// <param name="engineSettings">The associated authentication engine's settings.</param>
        /// <param name="args">Argument string whose format is determined by the provider implementation.</param>
        /// <remarks>
        /// <note>
        /// Every call to <see cref="Open" /> should be matched by a call to
        /// <see cref="Close" /> or <see cref="IDisposable.Dispose" />.
        /// </note>
        /// </remarks>
        void Open(AuthenticationEngineSettings engineSettings, string args);

        /// <summary>
        /// Closes the session with the realm map provider.
        /// </summary>
        void Close();

        /// <summary>
        /// Returns <c>true</c> if the provider is currently open.
        /// </summary>
        bool IsOpen { get; }

        /// <summary>
        /// Queries the realm map provider for the current set of realm mappings.
        /// </summary>
        /// <returns>The list of realm mappings.</returns>
        List<RealmMapping> GetMap();
    }
}
