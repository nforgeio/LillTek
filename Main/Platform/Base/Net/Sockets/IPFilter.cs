//-----------------------------------------------------------------------------
// FILE:        IPFilter.cs
// CONTRIBUTOR: Jeff Lill
// COPYRIGHT:   Copyright (c) 2005-2015 by Jeffrey Lill.  All rights reserved.
// DESCRIPTION: Implements a mechanism for granting or denying access to
//              clients based on their IP address.

using System;
using System.Collections;
using System.Collections.Generic;
using System.Net;

using LillTek.Common;

namespace LillTek.Net.Sockets
{
    /// <summary>
    /// Implements a mechanism for granting or denying access to clients based on 
    /// their IP address.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Each <see cref="IPFilter" /> consists of zero or more <see cref="IPFilterItem" />s,
    /// where these filter items specify whether access is to be granted or denied to
    /// a specific IP address or to all IP addresses within a specified subnet.
    /// </para>
    /// <para>
    /// There are two ways to construct and initialize an <see cref="IPFilter" />.  The first
    /// involves using the <see cref="IPFilter(bool)" /> constructor to create an empty
    /// filter and then calling <see cref="Add" /> to append <see cref="IPFilterItem" />s
    /// onto the list.  The second method involves passing a properly formatted string to
    /// <see cref="Parse" /> or <see cref="IPFilter(string)" /> and having the class 
    /// handle the creation of the filter items.
    /// </para>
    /// <para>
    /// Use the <see cref="GrantAccess" /> method to determine whether a specific IP
    /// address should be granted or denied access based on the filter items in the
    /// collection.  This method works by searching the list of filter items from
    /// start to end, looking for any that explicitly grant or deny access for
    /// the IP address, returning the appropriate value when a match is found.
    /// If none of the filter items match the IP address then <see cref="GrantDefault" />
    /// value will be returned.  This default is specified explicitly in the 
    /// <see cref="IPFilter(bool)" /> constructor or is determined via the string 
    /// parsing constructor.
    /// </para>
    /// <para><b><u>IP Filter String Format</u></b></para>
    /// <para>
    /// The class supports parsing a complete IP filter from a string that can
    /// be conveniently loaded from the application configuration or a file.  This
    /// string specifies the <see cref="GrantDefault" /> along with the set of
    /// IP addresses or subnets and grant values.  The string is formatted as
    /// zero or more items separated by commas.  Three types of items are parsed:
    /// </para>
    /// <list type="table">
    ///     <item>
    ///         <term>Default</term>
    ///         <description>
    ///         Indicates whether the table should grant or deny access
    ///         to IP addresses that do not map to an IP filter item.  This
    ///         is formatted as <b>default:grant</b> or <b>default:deny</b>.
    ///         Note that <b>default:grant</b> will be assumed if this is
    ///         not explicitly specified in the input.
    ///         </description>
    ///     </item>
    ///     <item>
    ///         <term>IPAddress</term>
    ///         <description>
    ///         Indicates whether a specific IP address should be granted or
    ///         denied access.  This is formatted as <b>&lt;IP address&gt;:grant</b>
    ///         or <b>&lt;IP address&gt;:deny</b> where the IP address is formatted
    ///         as a dotted quad.
    ///         </description>
    ///     </item>
    ///     <item>
    ///         <term>Subnet</term>
    ///         <description>
    ///         Indicates whether all IP addresses within a subnet should be
    ///         granted or denied access.  This is formatted as <b>&lt;IP address&gt;/&lt;count&gt;:grant</b>
    ///         or <b>&lt;IP address&gt;/&lt;count&gt;:deny</b> where the IP address is formatted
    ///         as a dotted quad and <b>&lt;count&gt;</b> is a number in the range of
    ///         0..32 (inclusive) that specifies the number of bits in subnet
    ///         network prefix as described in <a href="http://en.wikipedia.org/wiki/Subnetwork">Wikipedia</a>.
    ///         </description>
    ///     </item>
    /// </list>
    /// <para>
    /// The application configuration setting example below grants access to 
    /// specific IP addresses and subnets denies access to all other clients.
    /// </para>
    /// <code language="none">
    /// #section MyApplication
    /// 
    ///     IPFilter = {{
    /// 
    ///         default:deny,
    ///         206.23.5.10:grant,
    ///         10.0.0.0/24:grant,
    ///         20.5.10.33/16:grant
    ///     }}
    /// 
    /// #endsection
    /// </code>
    /// <note>
    /// This class is thread-safe for read-only applications.
    /// </note>
    /// </remarks>
    /// <threadsafety instance="false" />
    public sealed class IPFilter : IEnumerable
    {
        //---------------------------------------------------------------------
        // Static members

        /// <summary>
        /// Parses an IP filter list from a formatted string.  See <see cref="IPFilter" />
        /// for a description of the string format.
        /// </summary>
        /// <param name="value">The input string.</param>
        /// <returns>The <see cref="IPFilter" /> instance.</returns>
        /// <exception cref="ArgumentException">Thrown if the input string is improperly formatted.</exception>
        public static IPFilter Parse(string value)
        {
            return new IPFilter(value);
        }

        //---------------------------------------------------------------------
        // Instance members

        private List<IPFilterItem>  filterList;
        private bool                grantDefault;

        /// <summary>
        /// Constructs an empty IP filter list with the specified default access.
        /// </summary>
        /// <param name="grantDefault">
        /// Indicates whether access is to be granted or denied for IP address not
        /// found in the filter.
        /// </param>
        public IPFilter(bool grantDefault)
        {
            this.filterList   = new List<IPFilterItem>();
            this.grantDefault = grantDefault;
        }

        /// <summary>
        /// Constructs an IP filter list from a formatted string.  See <see cref="IPFilter" />
        /// for a description of the string format.
        /// </summary>
        /// <param name="value">The input string.</param>
        /// <exception cref="ArgumentException">Thrown if the input string is improperly formatted.</exception>
        public IPFilter(string value)
        {
            string[]    items;
            string      s;
            int         pos;
            string      sAddress;
            string      sCount;
            string      sAccess;
            IPAddress   ip;
            int         count;
            bool        grant;

            this.filterList   = new List<IPFilterItem>();
            this.grantDefault = true;

            items = value.Split(',');
            for (int i = 0; i < items.Length; i++)
            {
                string item = items[i].Trim();

                if (item == string.Empty)
                    continue;

                pos = item.IndexOf(':');
                if (pos == -1)
                    throw new ArgumentException(string.Format("IP filter item [{0}] is missing a colon.", value), "value");

                s = item.Substring(0, pos).Trim();
                sAccess = item.Substring(pos + 1).Trim();

                switch (sAccess.ToLowerInvariant())
                {
                    case "grant":

                        grant = true;
                        break;

                    case "deny":

                        grant = false;
                        break;

                    default:

                        throw new ArgumentException(string.Format("Access [{0}] is not valid for filter item [{1}].", sAccess, value), "value");
                }

                if (String.Compare(s, "default", true) == 0)
                {
                    grantDefault = grant;
                }
                else
                {
                    pos = s.IndexOf('/');
                    if (pos == -1)
                    {
                        sAddress = s;
                        if (!IPAddress.TryParse(sAddress, out ip))
                            throw new ArgumentException(string.Format("[{0}] not a valid IP address in filter item [{1}].", sAddress, value), "value");

                        filterList.Add(new IPFilterItem(grant, ip));
                    }
                    else
                    {
                        sAddress = s.Substring(0, pos).Trim();
                        sCount   = s.Substring(pos + 1).Trim();

                        if (!IPAddress.TryParse(sAddress, out ip))
                            throw new ArgumentException(string.Format("[{0}] not a valid IP address in filter item [{1}].", sAddress, value), "value");

                        if (!int.TryParse(sCount, out count))
                            throw new ArgumentException(string.Format("[{0}] is not network prefix count for IP filter item [{1}].", sCount, value), "value");

                        if (count < 0 || count > 32)
                            throw new ArgumentException(string.Format("Network prefix count is not in the range of 0..32 in IP filter item [{0}].", value), "value");

                        filterList.Add(new IPFilterItem(grant, ip, count));
                    }
                }
            }
        }

        /// <summary>
        /// Removes all items from the filter.
        /// </summary>
        public void Clear()
        {
            filterList.Clear();
        }

        /// <summary>
        /// Indicates whether or not client IP addresses that so not match
        /// a filter item will be granted access.
        /// </summary>
        public bool GrantDefault
        {
            get { return grantDefault; }
        }

        /// <summary>
        /// Returns the number of filter items in the collection.
        /// </summary>
        public int Count
        {
            get { return filterList.Count; }
        }

        /// <summary>
        /// Appends a filter item to the list.
        /// </summary>
        /// <param name="item">The <see cref="IPFilterItem" /> to be appended.</param>
        public void Add(IPFilterItem item)
        {
            filterList.Add(item);
        }

        /// <summary>
        /// Returns an enumerator over the filter items in the collection.
        /// </summary>
        /// <returns></returns>
        public IEnumerator GetEnumerator()
        {
            return filterList.GetEnumerator();
        }

        /// <summary>
        /// Returns the filter item at the specified index in the collection.
        /// </summary>
        /// <param name="index">The index.</param>
        /// <returns>The <see cref="IPFilterItem" /> at the specified index.</returns>
        public IPFilterItem this[int index]
        {
            get { return filterList[index]; }
        }

        /// <summary>
        /// Searches the filter list for the access rule for the client IP address
        /// passed.
        /// </summary>
        /// <param name="address">The client IP address.</param>
        /// <returns>
        /// <c>true</c> if access is granted for the IP address, <c>false</c> 
        /// if access is denied.
        /// </returns>
        /// <remarks>
        /// This method performs a simple linear search of the filter items in
        /// the collection and returns the access specified for the first matching
        /// filter item, if there is a match.  If no matching filter item is found,
        /// then <see cref="GrantDefault" /> will be returned.
        /// </remarks>
        public bool GrantAccess(IPAddress address)
        {
            for (int i = 0; i < filterList.Count; i++)
            {
                var grant = filterList[i].GrantAccess(address);

                if (grant != TriState.Unknown)
                    return (bool)grant;
            }

            return GrantDefault;
        }
    }
}
