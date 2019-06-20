//-----------------------------------------------------------------------------
// FILE:        IAuthenticationExtension.cs
// CONTRIBUTOR: Jeff Lill
// COPYRIGHT:   Copyright (c) 2005-2015 by Jeffrey Lill. All rights reserved.
// DESCRIPTION: Defines the behavior of classes that extend the
//              Authentication Service to authenticate against external 
//              authentication sources.

using System;
using System.Collections.Generic;

using LillTek.Advanced;
using LillTek.Common;

// $todo(jeff.lill): Think about implementing async BeginAuthenticate() and EndAuthenticate() calls.

namespace LillTek.Datacenter.Server
{
    /// <summary>
    /// Defines the behavior of classes that extend the
    /// Authentication Service to authenticate against external 
    /// authentication sources.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Use <see cref="Open" /> to initialize the extension with the extension
    /// specific arguments read from a <see cref="RealmMapping" /> instance.
    /// Then call <see cref="Authenticate" /> to authenticate account credentials
    /// against the authentication source.  This method returns a <see cref="AuthenticationResult" />
    /// structure that describes the result of the operation.
    /// </para>
    /// <para>
    /// The <see cref="AuthenticationResult.Status" /> property indicates the disposition
    /// of the authentication operation.  Extensions will return <see cref="AuthenticationStatus.Authenticated" />
    /// if the operation was successful.  Authentication failures due to the 
    /// sumbission of invalid credentials will be indicated by returning one of 
    /// the error codes.  Extensions may return specific error codes such as
    /// <see cref="AuthenticationStatus.BadPassword" /> and <see cref="AuthenticationStatus.BadAccount" />
    /// or the generic error code <see cref="AuthenticationStatus.AccessDenied" />.
    /// </para>
    /// <para>
    /// The <see cref="AuthenticationResult.MaxCacheTime" /> returns as the maximum time the
    /// results of the authentication operation should be cached.
    /// </para>
    /// <para>
    /// <see cref="Close" /> or <see cref="IDisposable.Dispose" /> should be called promptly
    /// when the extension is no longer needed to release any associated
    /// resources.  Note that if any authentication operations are still outstanding
    /// when either of these methods are called then the implementation must
    /// complete each outstanding request before releasing any shared resources.
    /// </para>
    /// </remarks>
    /// <threadsafety instance="true" />
    public interface IAuthenticationExtension : IDisposable
    {
        /// <summary>
        /// Establishes a session with the authentication source.
        /// </summary>
        /// <param name="args">The extension specific arguments.</param>
        /// <param name="query">The optional extension specific query template.</param>
        /// <param name="perfCounters">The application's performance counter set (or <c>null</c>).</param>
        /// <param name="perfPrefix">The string to prefix any performance counter names (or <c>null</c>).</param>
        /// <remarks>
        /// <para>
        /// Implementations that expose performance counters will pass a non-<c>null</c> <b>perfCounters</b>
        /// instance.  The service handler should add any counters it implements to this set.
        /// If <paramref name="perfPrefix" /> is not <c>null</c> then any counters added should prefix their
        /// names with this parameter.
        /// </para>
        /// <note>
        /// All calls to <see cref="Open" /> must be matched with a call
        /// to <see cref="Close" /> or <see cref="IDisposable.Dispose" />.
        /// </note>
        /// </remarks>
        void Open(ArgCollection args, string query, PerfCounterSet perfCounters, string perfPrefix);

        /// <summary>
        /// Releases all resources associated with the extension instance.
        /// </summary>
        void Close();

        /// <summary>
        /// Returns <c>true</c> if the extension is currently open.
        /// </summary>
        bool IsOpen { get; }

        /// <summary>
        /// Returns the number of authentications attempted against the
        /// extension.  This is useful for unit testing.
        /// </summary>
        int AuthenticationCount { get; }

        /// <summary>
        /// Authenticates the account credentials against the authentication
        /// extension.
        /// </summary>
        /// <param name="realm">The authentication realm.</param>
        /// <param name="account">The account ID.</param>
        /// <param name="password">The password.</param>
        /// <returns>A <see cref="AuthenticationResult" /> instance with the result of the operation.</returns>
        /// <remarks>
        /// <para>
        /// The <see cref="AuthenticationResult.Status" /> property indicates the disposition
        /// of the authentication operation.  Extensions will return <see cref="AuthenticationStatus.Authenticated" />
        /// if the operation was successful.  Authentication failures due to the 
        /// sumbission of invalid credentials will be indicated by returning one of 
        /// the error codes.  Extensions may return specific error codes such as
        /// <see cref="AuthenticationStatus.BadPassword" /> and <see cref="AuthenticationStatus.BadAccount" />
        /// or the generic error code <see cref="AuthenticationStatus.AccessDenied" />.
        /// </para>
        /// <para>
        /// The <see cref="AuthenticationResult.MaxCacheTime" /> returns as the maximum time the
        /// results of the authentication operation should be cached.
        /// </para>
        /// </remarks>
        /// <exception cref="AuthenticationException">Thrown for authentication related exception.</exception>
        AuthenticationResult Authenticate(string realm, string account, string password);
    }
}
