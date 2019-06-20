//-----------------------------------------------------------------------------
// FILE:        Credentials.cs
// CONTRIBUTOR: Jeff Lill
// COPYRIGHT:   Copyright (c) 2005-2015 by Jeffrey Lill.  All rights reserved.
// DESCRIPTION: Holds a user's credentials.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Runtime.Serialization;
using System.Security;
using System.Security.Cryptography;
using System.Security.Principal;

namespace LillTek.Common
{
    /// <summary>
    /// Holds a user's credentials.
    /// </summary>
    /// <remarks>
    /// <para>
    /// User credentials consist of a <see cref="Realm" /> specification, an <see cref="Account" /> identifier and a
    /// <see cref="Password" />.  The realm specification indicates the organization to which the account
    /// belongs.  This can be a simple organization name or the path to an organization within a heirarchy of
    /// organizations, where the organization names in the path are separated by semicolons.
    /// </para>
    /// <para>
    /// For example, assume that the we have the following organization heirarchy:
    /// </para>
    /// <code language="none">
    /// Root
    ///     LillTek
    ///         Production
    ///         Test
    ///         Development
    ///     AbcCorp
    ///         Development
    ///         Corp
    /// </code>
    /// <para>
    /// Here, we have a heirarchy of organizations, with the <b>Root</b> organization having
    /// the two suborganizations: <b>LillTek</b> and <b>AbcCorp</b>, with these having their
    /// own suborganizations.  Each of the organizations define their own authentication 
    /// account namespace and each organization is an authentication realm.
    /// </para>
    /// <para>
    /// You'll need to set the <see cref="Realm" /> or <see cref="Realms" /> properties to specify the
    /// authentication realm the account belongs to.  <see cref="Realm" /> specifies the path to the
    /// realm as the names of the organizations from the root of the heirarchy to the target organization,
    /// separated by semicolon characters.  Here are some examples:
    /// </para>
    /// <code language="none">
    /// Root
    /// Root:LillTek
    /// Root:LillTek:Test
    /// Root:AbcCorp:Corp
    /// </code>
    /// <para>
    /// The <see cref="Realms" /> property can be used to do the same thing by specifying the
    /// realm path as an array of strings rather than as names separated by semicolons.
    /// </para>
    /// </remarks>
    [DataContract(Namespace = "http://schemas.lilltek.com/platform/LillTek.Common.Credentials/2008-09-27")]
    public sealed class Credentials
    {
        private string[] realms;

        /// <summary>
        /// Default constructor.
        /// </summary>
        public Credentials()
        {
            this.realms = new string[0];
        }

        /// <summary>
        /// Constructs credential initialized with a realm path array, a user ID and a password.
        /// </summary>
        /// <param name="realms">The authentication realm path (or <c>null</c>).</param>
        /// <param name="account">The user's account ID.</param>
        /// <param name="password">The user's account password.</param>
        public Credentials(string[] realms, string account, string password)
        {
            this.Realms   = realms == null ? new string[0] : realms;
            this.Account  = account;
            this.Password = password;
        }

        /// <summary>
        /// Constructs credential initialized with a realm path string, a user ID and a password.
        /// </summary>
        /// <param name="realm">The authentication realm path (or <c>null</c>).</param>
        /// <param name="account">The user's account ID.</param>
        /// <param name="password">The user's account password.</param>
        /// <remarks>
        /// <para>
        /// The <paramref name="realm" /> parameters specifies a particular realm in
        /// a heirarchy of realms by naming each realm from the root with each name
        /// separated by colon (:) characters.  Here's an example:
        /// </para>
        /// <example>root:myorganization:myteam</example>
        /// </remarks>
        public Credentials(string realm, string account, string password)
        {
            this.Realms   = realm == null ? new string[0] : realm.Split(':');
            this.Account  = account;
            this.Password = password;
        }

        /// <summary>
        /// The authentication path string (the heirarchy of realm names separated by colons).
        /// </summary>
        [DataMember]
        public string Realm
        {
            get
            {
                var sb = new StringBuilder(64);

                for (int i = 0; i < Realms.Length; i++)
                {
                    if (i > 0)
                        sb.Append(':');

                    sb.Append(Realms[i]);
                }

                return sb.ToString();
            }

            set { Realms = value == null ? new string[0] : value.Split(':'); }
        }

        /// <summary>
        /// The authentication realm path array (the heirarchy of realm names).
        /// </summary>
        public string[] Realms
        {
            get { return realms; }

            set
            {
                foreach (string realm in value)
                    if (realm.Contains(':'))
                        throw new ArgumentException("Realm cannot include a semicolon (:).");

                realms = value;
            }
        }

        /// <summary>
        /// The user's account ID.
        /// </summary>
        [DataMember]
        public string Account { get; set; }

        /// <summary>
        /// The user's account password.
        /// </summary>
        [DataMember]
        public string Password { get; set; }

        /// <summary>
        /// Performs a shallow copy of the current instance fields
        /// to the <paramref name="target" /> instance.
        /// </summary>
        /// <param name="target">The target <see cref="Credentials" /> instance.</param>
        public void CopyTo(Credentials target)
        {
            target.realms = new string[this.Realms.Length];
            Array.Copy(this.realms, target.realms, this.realms.Length);

            target.Account  = this.Account;
            target.Password = this.Password;
        }
    }
}
