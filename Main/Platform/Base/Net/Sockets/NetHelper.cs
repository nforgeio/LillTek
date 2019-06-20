//-----------------------------------------------------------------------------
// FILE:        NetHelper.cs
// CONTRIBUTOR: Jeff Lill
// COPYRIGHT:   Copyright (c) 2005-2015 by Jeffrey Lill.  All rights reserved.
// DESCRIPTION: Misc network related utilities

using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.Net;
using System.Net.NetworkInformation;
using System.Net.Sockets;
using System.Text;
using System.Threading;

using LillTek.Common;

// $todo(jeff.lill): 
//
// I'm not supporting IPv6 addresses yet.  I played around with
// the .NET IPAddress class and on first look, it appears that
// they are broken relative to these addresses.  The code below
// will serializes the IPv4 addresses properly into IPv6 but
// actual IPv6 addresses will cause an exception.

namespace LillTek.Net.Sockets
{
    /// <summary>
    /// Misc network utility methods.
    /// </summary>
    public static class NetHelper
    {
        /// <summary>
        /// Parses an IPEndPoint from a string formatted as &lt;dotted quad&gt;:&lt;port&gt;
        /// </summary>
        /// <param name="s">The encoded string.</param>
        /// <returns>The parsed endpoint.</returns>
        public static IPEndPoint ParseIPEndPoint(string s)
        {
            int pos;

            pos = s.IndexOf(':');
            if (pos == -1)
                throw new FormatException("Invalid IPEndPoint");

            return new IPEndPoint(Helper.ParseIPAddress(s.Substring(0, pos)), int.Parse(s.Substring(pos + 1)));
        }

        private static IPAddress activeIP = IPAddress.Loopback;         // The cached active network adapter address
        private static TimeSpan activeIPCacheTime = TimeSpan.FromMinutes(60);   // Maximum time to cache an adapter
        private static DateTime activeIPTime = DateTime.MinValue;          // Time the current address was cached (UTC)

        /// <summary>
        /// The duration that an active network adapter's IP address found by
        /// <see cref="GetActiveAdapter()" /> will be cached by the class.  This defaults to
        /// 60 minutes.
        /// </summary>
        public static TimeSpan ActiveAdapterCacheTime
        {
            get { return activeIPCacheTime; }
            set { activeIPCacheTime = value; }
        }

        /// <summary>
        /// Determines whether an <see cref="IPAddress" /> is assigned to one of the local network adapters.
        /// </summary>
        /// <returns><c>true</c> if the <param name="address" /> is a local address.</returns>
        public static bool IsLocalAddress(IPAddress address)
        {
            if (address.Equals(IPAddress.Any) || IPAddress.IsLoopback(address))
                return true;

            if (!NetworkInterface.GetIsNetworkAvailable())
                return false;

            foreach (NetworkInterface adapter in NetworkInterface.GetAllNetworkInterfaces())
            {
                if (adapter.OperationalStatus != OperationalStatus.Up)
                    continue;

                var ipProps = adapter.GetIPProperties();

                if (ipProps == null)
                    continue;

                foreach (var adaptorAddressInfo in ipProps.UnicastAddresses)
                {
                    if (address.Equals(adaptorAddressInfo.Address))
                        return true;
                }
            }

            return false;
        }

        /// <summary>
        /// Returns the IP address of a DNS server configured for an
        /// active network interface.
        /// </summary>
        public static IPAddress GetDnsServer()
        {
            IPInterfaceProperties ipProps;

            if (!NetworkInterface.GetIsNetworkAvailable())
                return null;

            foreach (var adapter in NetworkInterface.GetAllNetworkInterfaces())
            {
                if (adapter.NetworkInterfaceType == NetworkInterfaceType.Loopback)
                    continue;

                if (adapter.OperationalStatus != OperationalStatus.Up)
                    continue;

                ipProps = adapter.GetIPProperties();
                if (ipProps == null)
                    continue;

                var addresses = ipProps.DnsAddresses.IPv4Only();

                if (addresses.Length > 0)
                    return addresses[0];
            }

            return null;
        }

        /// <summary>
        /// Returns the IP addresses of the DNS servers configured for an
        /// active network interface.
        /// </summary>
        public static IPAddress[] GetDnsServers()
        {
            IPInterfaceProperties ipProps;

            if (!NetworkInterface.GetIsNetworkAvailable())
                return null;

            foreach (var adapter in NetworkInterface.GetAllNetworkInterfaces())
            {
                if (adapter.NetworkInterfaceType == NetworkInterfaceType.Loopback)
                    continue;

                if (adapter.OperationalStatus != OperationalStatus.Up)
                    continue;

                ipProps = adapter.GetIPProperties();
                if (ipProps == null)
                    continue;

                if (ipProps.DnsAddresses.Count > 0)
                    return ipProps.DnsAddresses.IPv4Only();
            }

            return new IPAddress[0];
        }

        /// <summary>
        /// Returns the IP address of the network adapter most likely to be
        /// connected to the network at large.  If no likely candidate exists,
        /// the loopback address (<b>127.0.0.1</b>) will be returned.
        /// </summary>
        /// <returns>The active network adapter's address.</returns>
        /// <remarks>
        /// <para>
        /// Values returned by this method will be cached for the duration specified
        /// by ActiveAdapterCacheTime.  Subsequent calls to the method will return 
        /// the cached value rather than looking for a new active network adapter.
        /// </para>
        /// <para>
        /// Pass <c>true</c> to <see cref="GetActiveAdapter(bool)" /> to force the
        /// the method to look for a new active adapter rather than using a
        /// cached value.
        /// </para>
        /// </remarks>
        public static IPAddress GetActiveAdapter()
        {
            return GetActiveAdapter(false);
        }

        /// <summary>
        /// Returns the IP address of the network adapter most likely to be
        /// connected to the network at large.  If no likely candidate exists,
        /// the loopback address (127.0.0.1) will be returned.
        /// </summary>
        /// <param name="forceCheck">Forces a check for a new active adapter by clearing any cached value.</param>
        /// <returns>The active network adapter's address.</returns>
        /// <remarks>
        /// <para>
        /// Values returned by this method will be cached for the duration specified
        /// by ActiveAdapterCacheTime and if forceCheck=false, subsequent calls to
        /// the method will return the cached value rather than looking for a new
        /// active network adapter.
        /// </para>
        /// </remarks>
        public static IPAddress GetActiveAdapter(bool forceCheck)
        {
            IPAddress    address;
            IPAddress   subnet;

            Helper.GetNetworkInfo(out address, out subnet);
            return address;
        }

        /// <summary>
        /// Returns the physical MAC address of the first connected network adapter.
        /// </summary>
        /// <returns>The array bytes holding the MAC address or <c>null</c> if no adapter appears to be connected.</returns>
        public static byte[] GetMacAddress()
        {
            NetworkInterface[]                      adapters = NetworkInterface.GetAllNetworkInterfaces();
            UnicastIPAddressInformationCollection   addrInfo;
            IPAddress                                address;

            foreach (var adapter in adapters)
            {
                if (adapter.OperationalStatus != OperationalStatus.Up ||
                    adapter.NetworkInterfaceType == NetworkInterfaceType.GenericModem ||
                    adapter.NetworkInterfaceType == NetworkInterfaceType.Tunnel ||
                    adapter.NetworkInterfaceType == NetworkInterfaceType.Loopback)

                    continue;

                addrInfo = adapter.GetIPProperties().UnicastAddresses;
                if (addrInfo.Count > 0)
                {
                    address = addrInfo[0].Address;
                    if (address == null)
                        continue;

                    return adapter.GetPhysicalAddress().GetAddressBytes();
                }
            }

            return null;
        }

        /// <summary>
        /// Serializes the IP address into a IPv6 compatible byte array.
        /// </summary>
        /// <param name="address">The address to be rendered.</param>
        /// <returns>The buffer holding the rendered address.</returns>
        public static byte[] SerializeIPv6(IPAddress address)
        {
            // $todo(jeff.lill): Not handling actual IPv6 addresses

            byte[] b1, b2;

            b1 = address.GetAddressBytes();
            if (b1.Length > 4)
                throw new NotImplementedException("IPv6 addresses not implemented.");

            b2     = new byte[] { 0, 0, 0, 0, 0xFF, 0xFF, 0xFF, 0xFF, 0, 0, 0, 0, 0, 0, 0, 0 };
            b2[9]  = b1[0];
            b2[11] = b1[1];
            b2[13] = b1[2];
            b2[15] = b1[3];

            return b2;
        }

        /// <summary>
        /// Parses the 16 byte buffer passed holding an IPv6 address.
        /// </summary>
        /// <param name="buf">The serialized address.</param>
        /// <returns>The corresponding IP address.</returns>
        public static IPAddress ParseIPv6(byte[] buf)
        {
            // $todo(jeff.lill): Not handling actual IPv6 addresses

            bool IPv6 = false;

            if (buf.Length != 16)
                throw new ArgumentException();

            for (int i = 0; i < 4; i++)
                if (buf[i] != 0)
                {
                    IPv6 = true;
                    break;
                }

            for (int i = 4; i < 8; i++)
                if (buf[i] != 0xFF)
                {
                    IPv6 = true;
                    break;
                }

            if (IPv6)
                throw new NotImplementedException("IPv6 addresses not implemented.");

            return new IPAddress((buf[9] << 24) | (buf[11] << 16) | (buf[13] << 8) | buf[15]);
        }

        /// <summary>
        /// Returns <c>true</c> if the string passed is formatted as a valid IP address.
        /// </summary>
        /// <param name="value">The value to test.</param>
        /// <returns><c>true</c> if the value is formatted properly.</returns>
        /// <remarks>
        /// <para>
        /// This method will have better performance than calling IPAddress.Parse()
        /// for non-IP addresses since this method will avoid throwing an exception.
        /// </para>
        /// <para>
        /// Currently this method supports only IPv4 address formatted
        /// as dotted quads.
        /// </para>
        /// </remarks>
        public static bool IsIPAddress(string value)
        {
            // $todo(jeff.lill): Add support for IPv6 formatting.

            if (value == null || value.Length > 15)
                return false;   // Too many characters.

            int     v;
            int     pos;
            char    ch;

            pos = 0;
            for (int i = 0; i < 4; i++)
            {
                v = -1;
                if (pos >= value.Length)
                    return false;

                do
                {
                    ch = value[pos++];
                    if (ch == '.')
                        break;
                    else if (!char.IsDigit(ch))
                        return false;

                    if (v == -1)
                        v = 0;

                    v = v * 10 + (int)(ch - '0');

                } while (pos < value.Length);

                if (v == -1 || v > 255)
                    return false;
            }

            return pos == value.Length;
        }

        /// <summary>
        /// Converts an IPv4 address into an integer.
        /// </summary>
        /// <param name="address">The IPv4 address.</param>
        /// <returns>The IP address as a 32-bit integer.</returns>
        /// <exception cref="ArgumentException">Thrown for IPv6 addresses.</exception>
        public static int ToInt32(IPAddress address)
        {
            byte[] v;

            v = address.GetAddressBytes();
            if (v.Length != 4)
                throw new ArgumentException("Only IPv4 addresses may be converted to 32-bit integers", "address");

            return (v[0] << 24) | (v[1] << 16) | (v[2] << 8) | v[3];
        }

        /// <summary>
        /// Returns an <see cref="IPAddress" /> decoded from a 32-bit integer.
        /// </summary>
        /// <param name="address">The IPv4 address encoded as an integer.</param>
        public static IPAddress FromInt32(int address)
        {
            return new IPAddress(new byte[] { (byte)(address >> 24), (byte)(address >> 16), (byte)(address >> 8), (byte)address });
        }

        /// <summary>
        /// Subnet masks for each CIDR leading bits count.
        /// </summary>
        public static int[] SubnetMasks = new int[] {

            unchecked((int) 0x00000000),
            unchecked((int) 0x80000000),
            unchecked((int) 0xC0000000),
            unchecked((int) 0xE0000000),
            unchecked((int) 0xF0000000),
            unchecked((int) 0xF8000000),
            unchecked((int) 0xFC000000),
            unchecked((int) 0xFE000000),
            unchecked((int) 0xFF000000),
            unchecked((int) 0xFF800000),
            unchecked((int) 0xFFC00000),
            unchecked((int) 0xFFE00000),
            unchecked((int) 0xFFF00000),
            unchecked((int) 0xFFF80000),
            unchecked((int) 0xFFFC0000),
            unchecked((int) 0xFFFE0000),
            unchecked((int) 0xFFFF0000),
            unchecked((int) 0xFFFF8000),
            unchecked((int) 0xFFFFC000),
            unchecked((int) 0xFFFFE000),
            unchecked((int) 0xFFFFF000),
            unchecked((int) 0xFFFFF800),
            unchecked((int) 0xFFFFFC00),
            unchecked((int) 0xFFFFFE00),
            unchecked((int) 0xFFFFFF00),
            unchecked((int) 0xFFFFFF80),
            unchecked((int) 0xFFFFFFC0),
            unchecked((int) 0xFFFFFFE0),
            unchecked((int) 0xFFFFFFF0),
            unchecked((int) 0xFFFFFFF8),
            unchecked((int) 0xFFFFFFFC),
            unchecked((int) 0xFFFFFFFE),
            unchecked((int) 0xFFFFFFFF),
        };

        /// <summary>
        /// Returns the IPv4 subnet mask for the specified number of leading network bits.
        /// </summary>
        /// <param name="networkBits">The number of leading network bits in the address.</param>
        /// <returns>The requested subnet around the IP address passed.</returns>
        /// <remarks>
        /// <b>networkBits</b> is the number of bits specified after the slash (/) in the 
        /// classless Inter-Domain Routing (CIDR) notation for a subnet (ie. the 16 in
        /// 206.44.123.10/16).
        /// </remarks>
        /// <exception cref="ArgumentException">Thrown if the number of networkBits requested is not in the range of 0..32 (inclusive).</exception>
        public static int GetSubnetMask(int networkBits)
        {
            if (networkBits < 0 || networkBits > 32)
                throw new ArgumentException("[networkBits] must be in the range of 0..32 (inclusive).", "networkBits");

            return SubnetMasks[networkBits];
        }

        /// <summary>
        /// Returns the network adapter index for the NIC configured with a specific <see cref="IPAddress" />.
        /// </summary>
        /// <param name="address">The <see cref="IPAddress" />.</param>
        /// <returns>The network adapter index.</returns>
        /// <exception cref="SocketException">
        /// Thrown with the <see cref="SocketError.AddressNotAvailable" /> error code if 
        /// the IP address is not configured for any NIC.
        /// </exception>
        public static int GetNetworkAdapterIndex(IPAddress address)
        {
            return Helper.GetNetworkAdapterIndex(address);
        }

        /// <summary>
        /// Ensures that the DNS host name passed is returned with a terminating period (.).
        /// </summary>
        /// <param name="host">The DNS host name.</param>
        /// <returns>The canonical version.</returns>
        public static string GetCanonicalHost(string host)
        {
            if (host == null)
                throw new ArgumentException("host");

            if (host.EndsWith("."))
                return host;
            else
                return host + ".";
        }

        /// <summary>
        /// Strips off any third or greater host name segments from the host name
        /// passed and returns the result, ensuring that it is terminated with a
        /// period (.).
        /// </summary>
        /// <param name="host">The host name.</param>
        /// <returns>The canonical second level domain.</returns>
        /// <exception cref="ArgumentException">Thrown if the host passed has too few levels.</exception>
        public static string GetCanonicalSecondLevelHost(string host)
        {
            string[] segments;

            if (host == null)
                throw new ArgumentNullException("host");

            segments = host.Split('.');
            if (segments.Length < 2)
                throw new ArgumentException("Minimum of second level host name expected.", "host");

            if (segments[segments.Length - 1] == string.Empty)
            {

                if (segments.Length < 3)
                    throw new ArgumentException("Minimum of second level host name expected.", "host");

                return segments[segments.Length - 3] + "." + segments[segments.Length - 2] + ".";
            }
            else
            {
                if (segments.Length < 2)
                    throw new ArgumentException("Minimum of second level host name expected.", "host");

                return segments[segments.Length - 2] + "." + segments[segments.Length - 1] + ".";
            }
        }
    }
}
