//-----------------------------------------------------------------------------
// FILE:        AuthenticationResult.cs
// CONTRIBUTOR: Jeff Lill
// COPYRIGHT:   Copyright (c) 2005-2015 by Jeffrey Lill. All rights reserved.
// DESCRIPTION: Information returned for an authentication request.

using System;
using System.Collections.Generic;

using LillTek.Common;

namespace LillTek.Datacenter
{
    /// <summary>
    /// Information returned from an authentication request made to
    /// <b>IAuthenticationExtension.Authenticate()</b>.
    /// </summary>
    public sealed class AuthenticationResult
    {
        //---------------------------------------------------------------------
        // Static members

        private static string[] defMsgs;    // The default status messages

        static AuthenticationResult()
        {
            defMsgs = new string[(int)AuthenticationStatus.Last];

            defMsgs[(int)AuthenticationStatus.Authenticated]   = "Authenticated";
            defMsgs[(int)AuthenticationStatus.AccessDenied]    = "Access denied";
            defMsgs[(int)AuthenticationStatus.BadRealm]        = "Unknown realm";
            defMsgs[(int)AuthenticationStatus.BadAccount]      = "Unknown account";
            defMsgs[(int)AuthenticationStatus.BadPassword]     = "Invalid password";
            defMsgs[(int)AuthenticationStatus.AccountDisabled] = "Account is disabled";
            defMsgs[(int)AuthenticationStatus.BadRequest]      = "Bad request";
            defMsgs[(int)AuthenticationStatus.ServerError]     = "Server Error";
        }

        /// <summary>
        /// Parses and authentication result from a string.
        /// </summary>
        /// <param name="input">
        /// The serialized result (see <see cref="ToString" /> for a description
        /// of the format.
        /// </param>
        /// <returns>The parsed result.</returns>
        /// <exception cref="FormatException">Thrown if the input is improperly formatted.</exception>
        public static AuthenticationResult Parse(string input)
        {
            var args = new ArgCollection(input, '=', '\t');

            return new AuthenticationResult(args.Get<AuthenticationStatus>("Status", AuthenticationStatus.AccessDenied),
                                            args.Get("Message", string.Empty),
                                            args.Get("MaxCacheTime", TimeSpan.FromMinutes(5)));
        }

        //---------------------------------------------------------------------
        // Instance members

        /// <summary>
        /// A human readable message indicating the access has been denied.
        /// </summary>
        public const string AccessDeniedMsg = "Access denied";

        /// <summary>
        /// A human readable message indicating authentication success.
        /// </summary>
        public const string AuthenticatedMsg = "Authenticated";

        /// <summary>
        /// Indicates the result of the operation.
        /// </summary>
        public readonly AuthenticationStatus Status;

        /// <summary>
        /// A human readable message describing what happened.
        /// </summary>
        public readonly string Message;

        /// <summary>
        /// The maximum time the result of this operation should be cached.
        /// </summary>
        public readonly TimeSpan MaxCacheTime;

        /// <summary>
        /// Constructor.
        /// </summary>
        /// <param name="status"><c>true</c> if the authentication attempt was successful.</param>
        /// <param name="maxCacheTime">The maximum time the result of this operation should be cached.</param>
        /// <remarks>
        /// This constructor initializes <see cref="Message" /> with a default message
        /// for the particular status passed.  Use <see cref="AuthenticationResult(AuthenticationStatus,string,TimeSpan)" />
        /// if a customized message is necessary.
        /// </remarks>
        public AuthenticationResult(AuthenticationStatus status, TimeSpan maxCacheTime)
        {
            this.Status       = status;
            this.Message      = defMsgs[(int)status];
            this.MaxCacheTime = maxCacheTime;
        }

        /// <summary>
        /// Constructor.
        /// </summary>
        /// <param name="status"><c>true</c> if the authentication attempt was successful.</param>
        /// <param name="message">A human readable message describing what happened.</param>
        /// <param name="maxCacheTime">The maximum time the result of this operation should be cached.</param>
        public AuthenticationResult(AuthenticationStatus status, string message, TimeSpan maxCacheTime)
        {
            this.Status       = status;
            this.Message      = message;
            this.MaxCacheTime = maxCacheTime;
        }

        /// <summary>
        /// Serializes the result to a string.
        /// </summary>
        /// <returns>The serialized string.</returns>
        /// <remarks>
        /// The result is formatted as a <see cref="ArgCollection" /> using TABs as
        /// separator characters.
        /// </remarks>
        public override string ToString()
        {
            var args = new ArgCollection('=', '\t');

            args.Set("Status", this.Status.ToString());
            args.Set("Message", this.Message);
            args.Set("MaxCacheTime", this.MaxCacheTime);

            return args.ToString();
        }
    }
}
