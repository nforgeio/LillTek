//-----------------------------------------------------------------------------
// FILE:        UserStateBase.cs
// CONTRIBUTOR: Jeff Lill
// COPYRIGHT:   Copyright (c) 2005-2015 by Jeffrey Lill.  All rights reserved.
// DESCRIPTION: Base class for managing user related information for Silverlight client
//              applications.

using System;
using System.Net;
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
    /// Base class for managing user related information for Silverlight client
    /// applications.
    /// </summary>
    [DataContract(Namespace = "http://schemas.lilltek.com/platform/LillTek.Common.UserState/2008-07-24")]
    public class UserStateBase
    {
        /// <summary>
        /// Default constructor.
        /// </summary>
        public UserStateBase()
        {
        }

        /// <summary>
        /// The internal database ID for the user.
        /// </summary>
        [DataMember]
        public long UserID { get; set; }

        /// <summary>
        /// The internal database ID for the user's home organization.
        /// </summary>
        [DataMember]
        public long OrganizationID { get; set; }

        /// <summary>
        /// The authentication realm paths.
        /// </summary>
        [DataMember]
        public string[] Realms { get; set; }

        /// <summary>
        /// The user's account ID.
        /// </summary>
        [DataMember]
        public string Account { get; set; }

        /// <summary>
        /// The user's display name.
        /// </summary>
        [DataMember]
        public string DisplayName { get; set; }

        /// <summary>
        /// The set of security roles associated with the user.
        /// </summary>
        [DataMember]
        public string[] Roles { get; set; }

        /// <summary>
        /// The collection of user profile information keyed by case-insensitive 
        /// property names.
        /// </summary>
        [DataMember]
        public Dictionary<string, string> Profile { get; set; }

        /// <summary>
        /// Expresses the authentication realm paths as a list of realm
        /// names separated by colons.
        /// </summary>
        public string RealmPath
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

            set
            {
                if (value == null)
                    throw new ArgumentNullException("value");

                Realms = value.Split(':');
            }
        }

        /// <summary>
        /// Returns the <see cref="IPrincipal" /> for this user.
        /// </summary>
        /// <returns>The <see cref="IPrincipal" />.</returns>
        public IPrincipal GetPrincipal()
        {
            return new GenericPrincipal(new GenericIdentity(Account + "@" + RealmPath), Roles);
        }

        /// <summary>
        /// Returns a shallow clone.
        /// </summary>
        /// <returns>A cloned <see cref="UserStateBase" />.</returns>
        public UserStateBase Clone()
        {
            return new UserStateBase()
            {
                UserID         = this.UserID,
                OrganizationID = this.OrganizationID,
                Realms         = this.Realms,
                Account        = this.Account,
                DisplayName    = this.DisplayName,
                Roles          = this.Roles,
                Profile        = this.Profile
            };
        }

        /// <summary>
        /// Sets a name/value pair in the user's profile.
        /// </summary>
        /// <param name="name">The name.</param>
        /// <param name="value">The value.</param>
        public void Set(string name, string value)
        {
            if (value == null)
            {
                if (Profile.ContainsKey(name))
                    Profile.Remove(name);

                return;
            }

            this[name] = value;
        }

        /// <summary>
        /// Sets a name/value pair in the user's profile.
        /// </summary>
        /// <param name="name">The name.</param>
        /// <param name="value">The value.</param>
        public void Set(string name, bool value)
        {
            Set(name, Serialize.ToString(value));
        }

        /// <summary>
        /// Sets a name/value pair in the user's profile.
        /// </summary>
        /// <param name="name">The name.</param>
        /// <param name="value">The value.</param>
        public void Set(string name, TimeSpan value)
        {
            Set(name, Serialize.ToString(value));
        }

        /// <summary>
        /// Sets a name/value pair in the user's profile.
        /// </summary>
        /// <param name="name">The name.</param>
        /// <param name="value">The value.</param>
        public void Set(string name, DateTime value)
        {
            Set(name, Serialize.ToString(value));
        }

        /// <summary>
        /// Sets a name/value pair in the user's profile.
        /// </summary>
        /// <param name="name">The name.</param>
        /// <param name="value">The value.</param>
        public void Set(string name, int value)
        {
            Set(name, Serialize.ToString(value));
        }

        /// <summary>
        /// Sets a name/value pair in the user's profile.
        /// </summary>
        /// <param name="name">The name.</param>
        /// <param name="value">The value.</param>
        public void Set(string name, long value)
        {
            Set(name, Serialize.ToString(value));
        }

        /// <summary>
        /// Sets a name/value pair in the user's profile.
        /// </summary>
        /// <param name="name">The name.</param>
        /// <param name="value">The value.</param>
        public void Set(string name, double value)
        {
            Set(name, Serialize.ToString(value));
        }

        /// <summary>
        /// Sets a name/value pair in the user's profile.
        /// </summary>
        /// <param name="name">The name.</param>
        /// <param name="value">The value.</param>
        public void Set(string name, IPAddress value)
        {
            Set(name, Serialize.ToString(value));
        }

        /// <summary>
        /// Sets a name/value pair in the user's profile.
        /// </summary>
        /// <param name="name">The name.</param>
        /// <param name="value">The value.</param>
        public void Set(string name, NetworkBinding value)
        {
            Set(name, Serialize.ToString(value));
        }

        /// <summary>
        /// Sets a name/value pair in the user's profile.
        /// </summary>
        /// <param name="name">The name.</param>
        /// <param name="value">The value.</param>
        public void Set(string name, Guid value)
        {
            Set(name, Serialize.ToString(value));
        }

        /// <summary>
        /// Sets a name/value pair in the user's profile.
        /// </summary>
        /// <param name="name">The name.</param>
        /// <param name="value">The value.</param>
        public void Set(string name, Uri value)
        {
            Set(name, Serialize.ToString(value));
        }

        /// <summary>
        /// Sets a name/value pair in the user's profile.
        /// </summary>
        /// <param name="name">The name.</param>
        /// <param name="value">The value.</param>
        public void Set(string name, object value)
        {
            Set(name, value.ToString());
        }

        /// <summary>
        /// Accesses the named value from the user's profile, returning 
        /// <c>null</c> if the value is not present.
        /// </summary>
        public string this[string name]
        {
            get
            {
                string value;

                if (Profile.TryGetValue(name, out value))
                    return value;

                return null;
            }

            set { Profile[name] = value; }
        }

        /// <summary>
        /// Returns <c>true</c> if the user's profile contains a specific key.
        /// </summary>
        /// <param name="name">The key.</param>
        /// <returns><c>true</c> if the key exists.</returns>
        public bool ContainsKey(string name)
        {
            return Profile.ContainsKey(name);
        }

        /// <summary>
        /// Looks for a named value in the user's profile and returns
        /// if it is present.
        /// </summary>
        /// <param name="name">The key.</param>
        /// <param name="value">Returns as the value if found.</param>
        /// <returns><c>true</c> if the named value is found.</returns>
        public bool TryGetValue(string name, out string value)
        {
            return Profile.TryGetValue(name, out value);
        }

        /// <summary>
        /// Returns the value of the named string if it's present
        /// in the user's profile, <c>null</c> otherwise.
        /// </summary>
        /// <param name="name">The name.</param>
        /// <returns>The value or <c>null</c>.</returns>
        public string Get(string name)
        {
            return this[name];
        }

        /// <summary>
        /// Returns the named value if it exists in the user's profile otherwise
        /// returns a specified default value.
        /// </summary>
        /// <param name="name">The name.</param>
        /// <param name="def">The default value.</param>
        /// <returns>The named value from the user's profile if it is present, the default value otherwise.</returns>
        public string Get(string name, string def)
        {
            var value = this[name];

            return value != null ? value : def;
        }

        /// <summary>
        /// Returns the named value if it exists in the user's profile otherwise
        /// returns a specified default value.
        /// </summary>
        /// <param name="name">The name.</param>
        /// <param name="def">The default value.</param>
        /// <returns>The named value from the user's profile if it is present, the default value otherwise.</returns>
        public bool Get(string name, bool def)
        {
            var value = this[name];

            return value != null ? Config.ParseValue(value, def) : def;
        }

        /// <summary>
        /// Returns the named value if it exists in the user's profile otherwise
        /// returns a specified default value.
        /// </summary>
        /// <param name="name">The name.</param>
        /// <param name="def">The default value.</param>
        /// <returns>The named value from the user's profile if it is present, the default value otherwise.</returns>
        public TimeSpan Get(string name, TimeSpan def)
        {
            var value = this[name];

            return value != null ? Config.ParseValue(value, def) : def;
        }

        /// <summary>
        /// Returns the named value if it exists in the user's profile otherwise
        /// returns a specified default value.
        /// </summary>
        /// <param name="name">The name.</param>
        /// <param name="def">The default value.</param>
        /// <returns>The named value from the user's profile if it is present, the default value otherwise.</returns>
        public DateTime Get(string name, DateTime def)
        {
            var value = this[name];

            return value != null ? Serialize.Parse(value, def) : def;
        }

        /// <summary>
        /// Returns the named value if it exists in the user's profile otherwise
        /// returns a specified default value.
        /// </summary>
        /// <param name="name">The name.</param>
        /// <param name="def">The default value.</param>
        /// <returns>The named value from the user's profile if it is present, the default value otherwise.</returns>
        /// <remarks>
        /// <note>
        /// <para>
        /// This method supports "K", "M", and "G" suffixes
        /// where "K" multiples the value parsed by 1024, "M multiplies
        /// the value parsed by 1048576, and "G" multiples the value
        /// parsed by 1073741824.  The "T" suffix is not supported
        /// by this method because it exceeds the capacity of a
        /// 32-bit integer.
        /// </para>
        /// <para>
        /// The following constant values are also supported:
        /// </para>
        /// <list type="table">
        ///     <item><term><b>short.min</b></term><description>-32768</description></item>
        ///     <item><term><b>short.max</b></term><description>32767</description></item>
        ///     <item><term><b>ushort.max</b></term><description>65533</description></item>
        ///     <item><term><b>int.min</b></term><description>-2147483648</description></item>
        ///     <item><term><b>int.max</b></term><description>2147483647</description></item>
        /// </list>
        /// </note>
        /// </remarks>
        public int Get(string name, int def)
        {
            var value = this[name];

            return value != null ? Config.ParseValue(value, def) : def;
        }

        /// <summary>
        /// Returns the named value if it exists in the user's profile otherwise
        /// returns a specified default value.
        /// </summary>
        /// <param name="name">The name.</param>
        /// <param name="def">The default value.</param>
        /// <returns>The named value from the user's profile if it is present, the default value otherwise.</returns>
        /// <remarks>
        /// <note>
        /// <para>
        /// This method supports "K", "M", "G", and "T" suffixes
        /// where "K" multiples the value parsed by 1024, "M multiplies
        /// the value parsed by 1048576, "G" multiples the value
        /// parsed by 1073741824, and "T" multiplies the value by 
        /// 1099511627776.
        /// </para>
        /// <para>
        /// The following constant values are also supported:
        /// </para>
        /// <list type="table">
        ///     <item><term><b>short.min</b></term><description>-32768</description></item>
        ///     <item><term><b>short.max</b></term><description>32767</description></item>
        ///     <item><term><b>ushort.max</b></term><description>65533</description></item>
        ///     <item><term><b>int.min</b></term><description>-2147483648</description></item>
        ///     <item><term><b>int.max</b></term><description>2147483647</description></item>
        ///     <item><term><b>uint.max</b></term><description>4294967295</description></item>
        ///     <item><term><b>long.max</b></term><description>2^63 - 1</description></item>
        /// </list>
        /// </note>
        /// </remarks>
        public long Get(string name, long def)
        {
            var value = this[name];

            return value != null ? Config.ParseValue(value, def) : def;
        }

        /// <summary>
        /// Returns the named value if it exists in the user's profile otherwise
        /// returns a specified default value.
        /// </summary>
        /// <param name="name">The name.</param>
        /// <param name="def">The default value.</param>
        /// <returns>The named value from the user's profile if it is present, the default value otherwise.</returns>
        /// <remarks>
        /// <note>
        /// <para>
        /// This method supports "K", "M", "G", and "T" suffixes
        /// where "K" multiples the value parsed by 1024, "M multiplies
        /// the value parsed by 1048576, "G" multiples the value
        /// parsed by 1073741824, and "T" multiplies the value by 
        /// 1099511627776.
        /// </para>
        /// <para>
        /// The following constant values are also supported:
        /// </para>
        /// <list type="table">
        ///     <item><term><b>short.min</b></term><description>-32768</description></item>
        ///     <item><term><b>short.max</b></term><description>32767</description></item>
        ///     <item><term><b>ushort.max</b></term><description>65533</description></item>
        ///     <item><term><b>int.min</b></term><description>-2147483648</description></item>
        ///     <item><term><b>int.max</b></term><description>2147483647</description></item>
        ///     <item><term><b>uint.max</b></term><description>4294967295</description></item>
        ///     <item><term><b>long.max</b></term><description>2^63 - 1</description></item>
        /// </list>
        /// </note>
        /// </remarks>
        public double Get(string name, double def)
        {
            var value = this[name];

            return value != null ? Config.ParseValue(value, def) : def;
        }

        /// <summary>
        /// Returns the named value if it exists in the user's profile otherwise
        /// returns a specified default value.
        /// </summary>
        /// <param name="name">The name.</param>
        /// <param name="def">The default value.</param>
        /// <returns>The named value from the user's profile if it is present, the default value otherwise.</returns>
        public IPAddress Get(string name, IPAddress def)
        {
            var value = this[name];

            return value != null ? Config.ParseValue(value, def) : def;
        }

        /// <summary>
        /// Returns the named value if it exists in the user's profile otherwise
        /// returns a specified default value.
        /// </summary>
        /// <param name="name">The name.</param>
        /// <param name="def">The default value.</param>
        /// <returns>The named value from the user's profile if it is present, the default value otherwise.</returns>
        public NetworkBinding Get(string name, NetworkBinding def)
        {
            var value = this[name];

            return value != null ? Config.ParseValue(value, def) : def;
        }

        /// <summary>
        /// Returns the named value if it exists in the user's profile otherwise
        /// returns a specified default value.
        /// </summary>
        /// <param name="name">The name.</param>
        /// <param name="def">The default value.</param>
        /// <returns>The named value from the user's profile if it is present, the default value otherwise.</returns>
        public Guid Get(string name, Guid def)
        {
            var value = this[name];

            return value != null ? Config.ParseValue(value, def) : def;
        }

        /// <summary>
        /// Returns the named value if it exists in the user's profile otherwise
        /// returns a specified default value.
        /// </summary>
        /// <param name="name">The name.</param>
        /// <param name="def">The default value.</param>
        /// <returns>The named value from the user's profile if it is present, the default value otherwise.</returns>
        public Uri Get(string name, Uri def)
        {
            var value = this[name];

            return value != null ? Config.ParseValue(value, def) : def;
        }

        /// <summary>
        /// Returns the named value if it exists in the user's profile otherwise
        /// returns a specified default value.
        /// </summary>
        /// <param name="name">The name.</param>
        /// <param name="def">The default value.</param>
        /// <returns>The named value from the user's profile if it is present, the default value otherwise.</returns>
        public System.Type Get(string name, System.Type def)
        {
            var value = this[name];

            return value != null ? Config.ParseValue(value, def) : def;
        }

        /// <summary>
        /// Parses the named enumeration argument from the user's profile where the value is case insenstive.
        /// </summary>
        /// <param name="name">The name.</param>
        /// <param name="enumType">The enumeration type.</param>
        /// <param name="def">The default enumeration value.</param>
        /// <returns>The named value from the user's profile if it is present, the default value otherwise.</returns>
        public object Get(string name, System.Type enumType, object def)
        {
            var value = this[name];

            return value != null ? Config.ParseValue(value, enumType, def) : def;
        }

        /// <summary>
        /// Parses the named enumeration argument from the user's profile where the value is case insenstive.
        /// </summary>
        /// <param name="name">The name.</param>
        /// <param name="def">The default enumeration value.</param>
        /// <returns>The named value from the user's profile if it is present, the default value otherwise.</returns>
        public TEnum Get<TEnum>(string name, object def)
        {
            var value = this[name];

            return Config.ParseValue<TEnum>(value, def);
        }

        /// <summary>
        /// Determines whether the user has at least one of the specified roles.
        /// </summary>
        /// <param name="roles">The roles being checked.</param>
        /// <returns><c>true</c> if the user has one of the roles.</returns>
        public bool HasRole(params string[] roles)
        {
            foreach (var r1 in this.Roles)
                foreach (var r2 in roles)
                    if (String.Compare(r1, r2, StringComparison.OrdinalIgnoreCase) == 0)
                        return true;

            return false;
        }

        /// <summary>
        /// Verifies that the user has at least one of the specifed roles.
        /// </summary>
        /// <param name="roles">The roles being checked.</param>
        /// <exception cref="SecurityException">Thrown if the user does not hold the role.</exception>
        public void VerifyRole(params string[] roles)
        {
            if (!HasRole(roles))
            {
                var sb = new StringBuilder();

                for (int i = 0; i < roles.Length; i++)
                {
                    if (i > 0)
                        sb.Append(", ");

                    sb.Append(roles[i]);
                }

                throw new SecurityException(string.Format("Operation failed because the account does not hold any of the [{0}] roles.", sb));
            }
        }
    }
}
