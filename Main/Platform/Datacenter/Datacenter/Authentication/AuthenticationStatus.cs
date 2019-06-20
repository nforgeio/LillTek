//-----------------------------------------------------------------------------
// FILE:        AuthenticationStatus.cs
// CONTRIBUTOR: Jeff Lill
// COPYRIGHT:   Copyright (c) 2005-2015 by Jeffrey Lill. All rights reserved.
// DESCRIPTION: Indicates the result of an authentication attempt.

using System;
using System.Collections.Generic;

namespace LillTek.Datacenter
{
    /// <summary>
    /// Indicates the result of an authentication attempt.
    /// </summary>
    /// <remarks>
    /// <note>
    /// These values will be hardcoded into ODBC authentication
    /// extension implementations so the ordinal values cannot be changed.
    /// </note>
    /// </remarks>
    public enum AuthenticationStatus : int
    {
        /// <summary>
        /// The credentials are authentic.
        /// </summary>
        Authenticated = 0,

        /// <summary>
        /// Authentication was denied for an unspecified reason.
        /// </summary>
        AccessDenied = 1,

        /// <summary>
        /// The realm specified does not exist.
        /// </summary>
        BadRealm = 2,

        /// <summary>
        /// The account specified does not exist.
        /// </summary>
        BadAccount = 3,

        /// <summary>
        /// The password is not valid.
        /// </summary>
        BadPassword = 4,

        /// <summary>
        /// The account is disabled.
        /// </summary>
        AccountDisabled = 5,

        /// <summary>
        /// The account is temporarily locked due to excessive unsuccessful authentication attempts.
        /// </summary>
        AccountLocked = 6,

        /// <summary>
        /// The authentication request is not valid.
        /// </summary>
        BadRequest = 7,

        /// <summary>
        /// The server encountered an error and was not able to process
        /// the authentication request.
        /// </summary>
        ServerError = 8,

        /// <summary>
        /// This status is used to only for determining the ordinal value
        /// of the last status code.  This must be the last enumeration.
        /// </summary>
        Last
    }
}
