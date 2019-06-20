//-----------------------------------------------------------------------------
// FILE:        DynDnsHostEntry.cs
// CONTRIBUTOR: Jeff Lill
// COPYRIGHT:   Copyright (c) 2005-2015 by Jeffrey Lill.  All rights reserved.
// DESCRIPTION: Describes a dynamic DNS host to entry.

using System;
using System.Net;

using LillTek.Common;

namespace LillTek.Datacenter
{
    /// <summary>
    /// <para>
    /// Describes a dynamic DNS host to entry.
    /// </para>
    /// <note>
    /// Although this class is <b>public</b> it is not direct intended for use outside
    /// of the LillTek Platform codebase.
    /// </note>
    /// </summary>
    public sealed class DynDnsHostEntry
    {
        //---------------------------------------------------------------------
        // Static members

        /// <summary>
        /// Parses an <see cref="DynDnsHostEntry" /> by parsing the host name and
        /// IP address from a string.
        /// </summary>
        /// <param name="input">The input string.</param>
        /// <remarks>
        /// <para>
        /// Host registration strings are formatted as:
        /// </para>
        /// <code language="none">
        /// &lt;host name&gt; "," &lt;ip or cname&gt; [ "," &lt;TTL&gt; [ "," &lt;host-mode&gt; [ ";" "NAT" ] ] ]
        /// </code>
        /// </remarks>
        /// <returns>The parsed <see cref="DynDnsHostEntry" />.</returns>
        /// <exception cref="FormatException">Thrown if the input string is not valid.</exception>
        public static DynDnsHostEntry Parse(string input)
        {

            return new DynDnsHostEntry(input);
        }

        //---------------------------------------------------------------------
        // Instance members

        private string          host;
        private string          cname;
        private IPAddress       address;
        private TimeSpan        ttl;
        private DynDnsHostMode  hostMode;
        private bool            isNAT;
        private string          cachedSerialized;       // Cached ToString() representation (or null)

        /// <summary>
        /// The host name, including a terminating period (.).
        /// </summary>
        /// <remarks>
        /// <note>
        /// A period (.) will be appended to the host name set if the value
        /// is not already terminated with a period.
        /// </note>
        /// </remarks>
        public string Host
        {
            get { return host; }

            set
            {
                cachedSerialized = null;

                if (value == null)
                {
                    host = null;
                    return;
                }

                host = value;
                if (!host.EndsWith("."))
                    host += ".";
            }
        }

        /// <summary>
        /// The <see cref="IPAddress" /> associated with the host name.
        /// </summary>
        public IPAddress Address
        {
            get { return address; }

            set
            {
                cachedSerialized = null;
                address = value;
            }
        }

        /// <summary>
        /// The CNAME reference to be associated with the host name, including a
        /// terminating period (.).
        /// </summary>
        /// <remarks>
        /// <note>
        /// A period (.) will be appended to the host name set if the value
        /// is not already terminated with a period.
        /// </note>
        /// </remarks>
        public string CName
        {
            get { return cname; }

            set
            {
                cachedSerialized = null;

                if (value == null)
                {
                    cname = null;
                    return;
                }

                cname = value;
                if (!cname.EndsWith("."))
                    cname += ".";
            }
        }

        /// <summary>
        /// The time-to-live (TTL) value to use for this host entry or
        /// a negative value if the default DNS server TTL setting is to
        /// be used.
        /// </summary>
        public TimeSpan TTL
        {
            get { return ttl; }

            set
            {
                cachedSerialized = null;
                ttl = value;
            }
        }

        /// <summary>
        /// The scheduled time-to-die (SYS) for this entry (or <see cref="DateTime.MinValue" />).
        /// </summary>
        public DateTime TTD { get; set; }

        /// <summary>
        /// Specifies whether the DNS server is to return single or multiple A records a
        /// single CNAME record, or multiple MX records for queries for this entry's host name.
        /// </summary>
        /// <remarks>
        /// <note>
        /// A host mode of <b>ADDRESS</b> or <b>ADDRESSLIST</b> can only be specified for IP
        /// addresses and <b>CNAME</b> can only be specified for CNAME and MX entries.
        /// </note>
        /// </remarks>
        public DynDnsHostMode HostMode
        {
            get { return hostMode; }

            set
            {
                cachedSerialized = null;
                hostMode = value;
            }
        }

        /// <summary>
        /// <c>true</c> if the address embedded in the entry should be ignored and the
        /// source IP address for the received UDP packet should be used instead.
        /// </summary>
        /// <remarks>
        /// This is useful for situations where a host behind an upstream NAT needs
        /// to register the NAT's public address with the DNS.
        /// </remarks>
        public bool IsNAT
        {
            get { return isNAT; }

            set
            {
                cachedSerialized = null;
                isNAT = value;
            }
        }

        /// <summary>
        /// Constructs an IP address based host entry.
        /// </summary>
        /// <param name="host">The host name.</param>
        /// <param name="address">The IP address.</param>
        public DynDnsHostEntry(string host, IPAddress address)
            : this(host, address, TimeSpan.FromSeconds(-1), DynDnsHostMode.Address, false)
        {
        }

        /// <summary>
        /// Constructs an IP address based host entry.
        /// </summary>
        /// <param name="host">The host name.</param>
        /// <param name="address">The IP address.</param>
        /// <param name="ttl">The time-to-live interval (or a negative value to use the server default).</param>
        /// <param name="hostMode">The entry's mode.</param>
        /// <param name="isNAT">
        /// <c>true</c> if the address embedded in the entry should be ignored and the
        /// source IP address for the received UDP packet should be used instead.
        /// </param>
        public DynDnsHostEntry(string host, IPAddress address, TimeSpan ttl, DynDnsHostMode hostMode, bool isNAT)
        {
            if (hostMode == DynDnsHostMode.CName || hostMode == DynDnsHostMode.MX)
                throw new ArgumentException("[CNAME or MX] cannot be used for IP address entries.");

            this.Host     = host;
            this.CName    = null;
            this.Address  = address;
            this.TTL      = ttl;
            this.HostMode = hostMode;
            this.IsNAT    = IsNAT;
        }

        /// <summary>
        /// Constructs a CNAME based host entry.
        /// </summary>
        /// <param name="host">The host name.</param>
        /// <param name="cname">The referenced host name.</param>
        public DynDnsHostEntry(string host, string cname)
            : this(host, cname, TimeSpan.FromSeconds(-1), DynDnsHostMode.CName, false)
        {
        }

        /// <summary>
        /// Constructs a CNAME based host entry.
        /// </summary>
        /// <param name="host">The host name.</param>
        /// <param name="cname">The referenced host name.</param>
        /// <param name="ttl">The time-to-live interval (or a negative value to use the server default).</param>
        /// <param name="hostMode">The entry's mode.</param>
        /// <param name="isNAT">
        /// <c>true</c> if the address embedded in the entry should be ignored and the
        /// source IP address for the received UDP packet should be used instead.
        /// </param>
        public DynDnsHostEntry(string host, string cname, TimeSpan ttl, DynDnsHostMode hostMode, bool isNAT)
        {
            if (hostMode == DynDnsHostMode.Address)
                throw new ArgumentException("[ADDRESS] cannot be used for CNAME address entries.");
            else if (hostMode == DynDnsHostMode.AddressList)
                throw new ArgumentException("[ADDRESSLIST] cannot be used for CNAME address entries.");

            this.Host     = host;
            this.CName    = cname;
            this.Address  = IPAddress.Any;
            this.TTL      = ttl;
            this.HostMode = hostMode;
            this.IsNAT    = isNAT;
        }

        /// <summary>
        /// Constructs a host entry by parsing a string.
        /// </summary>
        /// <param name="input">The input string.</param>
        /// <remarks>
        /// <para>
        /// Host registration strings are formatted as:
        /// </para>
        /// <code language="none">
        /// &lt;host name&gt; "," &lt;ip or cname&gt; [ "," &lt;TTL&gt; [ "," &lt;host-mode&gt; [ "," "NAT" ] ]
        /// </code>
        /// </remarks>
        /// <exception cref="FormatException">Thrown if the input string is not valid.</exception>
        public DynDnsHostEntry(string input)
        {
            string[]    fields = input.Split(',');
            IPAddress   address;

            for (int i = 0; i < fields.Length; i++)
                fields[i] = fields[i].Trim();

            if (fields.Length < 2)
                throw new FormatException("Invalid host registration.");

            this.Host = fields[0];
            if (Host.Length == 0)
                throw new FormatException("Invalid host in host registration.");

            if (Helper.TryParseIPAddress(fields[1], out address))
            {
                this.Address = address;
                this.CName   = null;
            }
            else
            {
                this.Address = IPAddress.Any;
                this.CName   = fields[1];
            }

            this.TTL = TimeSpan.FromSeconds(-1);
            this.HostMode = this.CName != null ? DynDnsHostMode.CName : DynDnsHostMode.Address;

            if (fields.Length > 2)
            {
                this.TTL = Serialize.Parse(fields[2], this.TTL);

                if (fields.Length > 3)
                {
                    this.HostMode = Serialize.Parse<DynDnsHostMode>(fields[3], this.HostMode);

                    if ((this.HostMode == DynDnsHostMode.CName || this.HostMode == DynDnsHostMode.MX) && this.CName == null)
                        throw new FormatException("[CNAME] cannot be used for IP address entries.");
                    else if ((HostMode == DynDnsHostMode.Address || this.HostMode == DynDnsHostMode.AddressList) && this.CName != null)
                        throw new FormatException("[ADDRESS or ADDRESSLIST] cannot be used for CNAME address entries.");

                    if (fields.Length > 4)
                        this.IsNAT = fields[4].ToLowerInvariant() == "nat";
                }
            }
        }

        /// <summary>
        /// Returns <c>true</c> if the object passed equals
        /// this instance.
        /// </summary>
        /// <param name="obj">The object to be tested.</param>
        /// <returns><c>true</c> if the objects are equal.</returns>
        public override bool Equals(object obj)
        {
            DynDnsHostEntry mapping;

            mapping = obj as DynDnsHostEntry;
            if (mapping == null)
                return false;

            return String.Compare(this.ToString(), mapping.ToString(), true) == 0;
        }

        /// <summary>
        /// Computes a hash code for the instance.
        /// </summary>
        /// <returns>The hash code.</returns>
        public override int GetHashCode()
        {
            return StringComparer.OrdinalIgnoreCase.GetHashCode(this.ToString());
        }

        /// <summary>
        /// Renders the host registration as a string.
        /// </summary>
        /// <returns>The string.</returns>
        public override string ToString()
        {
            if (cachedSerialized == null)
                cachedSerialized = string.Format("{0},{1},{2},{3}{4}", Host, CName != null ? CName : Address.ToString(), TTL.TotalSeconds, HostMode, IsNAT ? ",NAT" : string.Empty);

            return cachedSerialized;
        }
    }
}
