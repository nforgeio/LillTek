//-----------------------------------------------------------------------------
// FILE:        SipRegistrationStateArgs.cs
// CONTRIBUTOR: Jeff Lill
// COPYRIGHT:   Copyright (c) 2005-2015 by Jeffrey Lill.  All rights reserved.
// DESCRIPTION: Defines the arguments passed when a SipCore raises its RegistrationChanged event.

using System;
using System.Collections.Generic;
using System.Net;
using System.Text;

using LillTek.Common;

namespace LillTek.Telephony.Sip
{
    /// <summary>
    /// Defines delegates called when a <see cref="SipCore" /> raises its <see cref="SipCore.RegistrationChanged" /> event.
    /// </summary>
    public sealed class SipRegistrationStateArgs
    {
        /// <summary>
        /// <c>true</c> if automatic registration is enabled and the last registration
        /// attempt succeeded.
        /// </summary>
        public readonly bool IsRegistered;

        /// <summary>
        /// <c>true</c> if automatic registration is enabled.
        /// </summary>
        public readonly bool AutoRegistration;

        /// <summary>
        /// The registrar's URI (or <b>null)</b>.
        /// </summary>
        public readonly string ServiceUri;

        /// <summary>
        /// The URI of the entity being registered (or <b>null)</b>.
        /// </summary>
        public readonly string FromUri;

        /// <summary>
        /// The current registration refresh interval.
        /// </summary>
        public readonly TimeSpan RefreshInterval;

        /// <summary>
        /// Constructor.
        /// </summary>
        /// <param name="isRegistered"><c>true</c> if automatic registration is enabled and the last registration attempt succeeded.</param>
        /// <param name="autoRegistration"><c>true</c> if automatic registration is enabled.</param>
        /// <param name="serviceUri">The registrar's URI (or <b>null)</b>.</param>
        /// <param name="fromUri">The URI of the entity being registered (or <b>null)</b>.</param>
        /// <param name="refreshInterval">he current registration refresh interval.</param>
        internal SipRegistrationStateArgs(bool isRegistered, bool autoRegistration, string serviceUri, string fromUri, TimeSpan refreshInterval)
        {
            this.IsRegistered     = isRegistered && autoRegistration;
            this.AutoRegistration = autoRegistration;
            this.ServiceUri       = serviceUri;
            this.FromUri          = fromUri;
            this.RefreshInterval  = refreshInterval;
        }
    }
}
