//-----------------------------------------------------------------------------
// FILE:        RealmFormat.cs
// CONTRIBUTOR: Jeff Lill
// COPYRIGHT:   Copyright (c) 2005-2015 by Jeffrey Lill.  All rights reserved.
// DESCRIPTION: Indicates how realm strings are formatted into user names.

using System;

namespace LillTek.Common
{
    /// <summary>
    /// Indicates how realm strings are formatted into user names.
    /// </summary>
    /// <remarks>
    /// <para>
    /// This enumeration is used in situations where a user name string
    /// needs to be separated into <b>realm</b> and <b>account</b> components.
    /// There are currently ways to encode a user name:
    /// </para>
    /// <list type="table">
    ///     <item>
    ///         <term>
    ///         &lt;realm&gt;\&lt;account&gt;<br/>
    ///         &lt;realm&gt;/&lt;account&gt;
    ///         </term>
    ///         <description>
    ///         A forward or back slash is used to separate the
    ///         realm from the account.
    ///         </description>
    ///     </item>
    ///     <item>
    ///         <term>
    ///         &lt;account&gt;@&lt;realm&gt;
    ///         </term>
    ///         <description>
    ///         An @ sign is used to separate the account from
    ///         the realm using an email address like syntax.
    ///         </description>
    ///     </item>
    /// </list>
    /// <para>
    /// In both modes, it is possible to ommit the separator character
    /// from the user name.  In this case, then realm will be considered
    /// to be the empty string.
    /// </para>
    /// <para>
    /// <see cref="RealmFormat.Email" /> should be used in situations where
    /// users are organized into realms based on their email address, where
    /// the email domain corresponds to the realm.  This will be typical or
    /// corporate or deferated corporate applications.
    /// </para>
    /// <para>
    /// <see cref="RealmFormat.Slash" /> should be used in situations where 
    /// user email addresses do not correspond to realms.  This will be
    /// typical of consumer applications.
    /// </para>
    /// </remarks>
    public enum RealmFormat
    {
        /// <summary>
        /// User names are parsed using a forward or back slash to
        /// separate the realm from the account.
        /// </summary>
        Slash,

        /// <summary>
        /// User names are parsed using an @ to separate the account
        /// from the realm.
        /// </summary>
        Email,
    }
}
