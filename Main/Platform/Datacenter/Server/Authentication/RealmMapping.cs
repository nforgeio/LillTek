//-----------------------------------------------------------------------------
// FILE:        RealmMapping.cs
// CONTRIBUTOR: Jeff Lill
// COPYRIGHT:   Copyright (c) 2005-2015 by Jeffrey Lill. All rights reserved.
// DESCRIPTION: Defines an authentication realm mapping.

using System;

using LillTek.Common;

namespace LillTek.Datacenter.Server
{
    /// <summary>
    /// Defines an authentication realm mapping along with realm specific
    /// parameters.
    /// </summary>
    /// <remarks>
    /// <para>
    /// An <see cref="IRealmMapProvider" /> instance is used to query for the
    /// authentication realms, the associated <see cref="IAuthenticationExtension" />,
    /// and any realm specific settings.  The <see cref="IRealmMapProvider.GetMap" />
    /// will be called periodically by <see cref="AuthenticationEngine" /> instances.
    /// </para>
    /// <para>
    /// The realm and <see cref="IAuthenticationExtension" /> settings will be initialized
    /// from a <see cref="ArgCollection" /> parameter set passed to the constructor's
    /// <b>args</b> parameter.  The table below describes the realm settings retrieved
    /// from this parameter.
    /// </para>
    /// <list type="table">
    ///     <item>
    ///         <term>LockoutCount</term>
    ///         <description>
    ///         Specifies the limiting failed authentication count.  Accounts
    ///         will be locked when the fail count reaches this number.
    ///         </description>
    ///     </item>
    ///     <item>
    ///         <term>LockoutThreshold</term>
    ///         <description>
    ///         The period of time that can elapse between failed authentication 
    ///         attempts where the failed attempts will <b>not</b> be counted against the
    ///         <see cref="LockoutCount" />.  Set this to <see cref="TimeSpan.Zero" />
    ///         to disable account lockout for the realm.
    ///         </description>
    ///     </item>
    ///     <item>
    ///         <term>LockoutTime</term>
    ///         <description>
    ///         The period of time an account will remain locked after being locked
    ///         out due to too many failed authentication attempts. 
    ///         </description>
    ///     </item>
    /// </list>
    /// <para>
    /// These values, if present, override the corresponding values in the
    /// <see cref="AuthenticationEngine.Settings" /> property.
    /// </para>
    /// </remarks>
    public sealed class RealmMapping
    {
        /// <summary>
        /// The realm being mapped.
        /// </summary>
        public readonly string Realm;

        /// <summary>
        /// Specifies the <see cref="IAuthenticationExtension" /> implementation to 
        /// be used to perform actual authentications.
        /// </summary>
        public readonly System.Type ExtensionType;

        /// <summary>
        /// The human readable name of the <see cref="IAuthenticationExtension" /> (this will show up in
        /// security logs).
        /// </summary>
        public readonly string ExtensionName;

        /// <summary>
        /// Parameters describing how to connect to authentication source.
        /// The schema for these parameters are specified by the particular
        /// <see cref="IAuthenticationExtension" /> implementation.
        /// </summary>
        public readonly ArgCollection Args;

        /// <summary>
        /// The optional query string.  Some <see cref="IRealmMapProvider" />
        /// implementations may make use of this.
        /// </summary>
        public readonly string Query;

        /// <summary>
        /// The <see cref="IAuthenticationExtension" /> instance (or <c>null</c>).
        /// </summary>
        public IAuthenticationExtension AuthExtension;

        /// <summary>
        /// Indicates the maximum number of failed authentication requests
        /// to be allowed for a realm/account combination before the account
        /// will be locked out.
        /// </summary>
        public readonly int LockoutCount;

        /// <summary>
        /// The minimum period of time that can elapse between failed authentication 
        /// attempts where the failed attempts will not be counted against the
        /// <see cref="LockoutCount" />.  Set this to <see cref="TimeSpan.Zero" />
        /// to disable account lockout for the realm.
        /// </summary>
        public readonly TimeSpan LockoutThreshold;

        /// <summary>
        /// The period of time an account will remain locked after being locked
        /// out due to too many failed authentication attempts.
        /// </summary>
        public readonly TimeSpan LockoutTime;

        /// <summary>
        /// Constructor.
        /// </summary>
        /// <param name="engineSettings">The <see cref="AuthenticationEngineSettings" />.</param>
        /// <param name="realm">The realm string.</param>
        /// <param name="extensionType">The <see cref="IAuthenticationExtension" /> type.</param>
        /// <param name="args">The realm and provider arguments.</param>
        /// <param name="query">The optional provider query.</param>
        /// <remarks>
        /// See the <see cref="RealmMapping" /> comments for a description of the
        /// realm settings retrieved from the <b>args</b> parameter and the
        /// comments for the specific <see cref="IAuthenticationExtension" />
        /// implementation for a description of the provider specific arguments.
        /// </remarks>
        public RealmMapping(AuthenticationEngineSettings engineSettings, string realm, System.Type extensionType, ArgCollection args, string query)
        {
            this.Realm            = realm;
            this.ExtensionType    = extensionType;
            this.ExtensionName    = extensionType.Name;
            this.Args             = args;
            this.Query            = query;
            this.AuthExtension    = null;

            this.LockoutCount     = args.Get("LockoutCount", engineSettings.LockoutCount);
            this.LockoutThreshold = args.Get("LockoutThreshold", engineSettings.LockoutThreshold);
            this.LockoutTime      = args.Get("LockoutTime", engineSettings.LockoutTime);
        }
    }
}
