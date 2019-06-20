//-----------------------------------------------------------------------------
// FILE:        NetworkBinding.cs
// CONTRIBUTOR: Jeff Lill
// COPYRIGHT:   Copyright (c) 2005-2015 by Jeffrey Lill.  All rights reserved.
// DESCRIPTION: An enhanced implementation of IPEndPoint.

using System;
using System.Net;
using System.Net.Sockets;

namespace LillTek.Common
{
    /// <summary>
    /// An enhanced implementation of <see cref="IPEndPoint" />.
    /// </summary>
    /// <remarks>
    /// <para>
    /// This class is designed to make it easier to read TCP/UDP network
    /// bindings from application configuration files.  A network binding
    /// is simply the combination of an IP address and a port number or
    /// optionally a DNS host name and a port number.
    /// </para>
    /// <para>
    /// Note that the host name <b>ANY</b> has a special meaning.  This
    /// always resolves to the IP address <b>0.0.0.0</b> and is used
    /// for indicating that a socket will be bound to all network
    /// interfaces available on the computer.  Note that <see cref="IsHost" />
    /// will return <c>false</c> for bindings initialized with Host=ANY.
    /// </para>
    /// <para>
    /// The class is also able to parse some well known port names as
    /// well as integer port numbers.  See <see cref="NetworkPort" /> for the
    /// list of supported port constants.  Here are some examples of valid network
    /// bindings:
    /// </para>
    /// <code language="none">
    /// 127.0.0.1:80
    /// 127.0.0.1:HTTP
    /// LOCALHOST:FTP
    /// www.google.com:HTTPS
    /// ad.lilltek.com:RADIUS
    /// ANY:HTTP
    /// </code>
    /// <para>
    /// The <see cref="IsHost" /> property returns <c>true</c> if the network binding
    /// consists of a DNS host name rather than an IP address.  If this is the
    /// case then the <see cref="IPAddress" /> property will perform a DNS
    /// host name resolution every time it is called.
    /// </para>
    /// </remarks>
    public sealed class NetworkBinding
    {
        //---------------------------------------------------------------------
        // Static members

        /// <summary>
        /// Implicit conversion of a <see cref="NetworkBinding" /> into an <see cref="IPEndPoint" />.
        /// </summary>
        /// <param name="binding">The network binding to be converted.</param>
        /// <returns>The corresponding IP address.</returns>
        /// <remarks>
        /// If the binding was constructed from a host name rather than
        /// an IP address, then this operator will perform a DNS resolution.
        /// </remarks>
        /// <exception cref="SocketException">Thrown if the host name could not be resolved.</exception>
        public static implicit operator IPEndPoint(NetworkBinding binding)
        {
            if (binding == null)
                return null;

            return new IPEndPoint(binding.Address, binding.Port);
        }

        /// <summary>
        /// Implicit conversion of an <see cref="IPEndPoint" /> into a <see cref="NetworkBinding" />.
        /// </summary>
        /// <param name="endPoint">The endpoint to be converted.</param>
        /// <returns>The corresponding binding.</returns>
        public static implicit operator NetworkBinding(IPEndPoint endPoint)
        {
            if (endPoint == null)
                return null;

            return new NetworkBinding(endPoint.Address, endPoint.Port);
        }

        /// <summary>
        /// Returns <c>true</c> if the network bindings are equal.
        /// </summary>
        /// <param name="binding1">Binding #1</param>
        /// <param name="binding2">Binding #2</param>
        /// <returns>True on equality.</returns>
        public static bool operator ==(NetworkBinding binding1, NetworkBinding binding2)
        {
            bool isNull1 = (object)binding1 == null;
            bool isNull2 = (object)binding2 == null;

            if (isNull1 != isNull2)
                return false;
            else if (isNull1 && isNull2)
                return true;

            return binding1.host == binding2.host &&
                   binding1.address.Equals(binding2.address) &&
                   binding1.port == binding2.port;
        }

        /// <summary>
        /// Returns <c>true</c> if the network bindings are not equal.
        /// </summary>
        /// <param name="binding1">Binding #1</param>
        /// <param name="binding2">Binding #2</param>
        /// <returns>True on inequality.</returns>
        public static bool operator !=(NetworkBinding binding1, NetworkBinding binding2)
        {
            return !(binding1 == binding2);
        }

        /// <summary>
        /// Parses a network binding string.
        /// </summary>
        /// <param name="text">The binding string to be parsed.</param>
        /// <returns>The network binding.</returns>
        /// <remarks>
        /// The method is also able to parse some well known port names as
        /// well as integer port numbers.  See <see cref="NetworkPort" /> for the
        /// list of supported port constants.
        /// </remarks>
        public static NetworkBinding Parse(string text)
        {
            return new NetworkBinding(text);
        }

        /// <summary>
        /// Attempts to parse a <see cref="NetworkBinding" />.
        /// </summary>
        /// <param name="text">The binding string to be parsed.</param>
        /// <param name="binding">Returns as the parsed binding.</param>
        /// <returns><c>true</c> if the operation was successful.</returns>
        public static bool TryParse(string text, out NetworkBinding binding)
        {
            // $todo(jeff.lill): 
            //
            // Implement this without having to catch exceptions
            // to improve performance

            binding = null;

            try
            {

                binding = Parse(text);
                return true;
            }
            catch
            {

                return false;
            }
        }

        /// <summary>
        /// The constant network binding: <b>IPAddress.Any:0</b> which is used to
        /// specify that a socket will listen on all network interfaces and where
        /// the operating system is to choose the port.
        /// </summary>
        public static readonly NetworkBinding Any = new NetworkBinding(IPAddress.Any, 0);

        //---------------------------------------------------------------------
        // Instance members

        private string      host;       // The host name (or "")
        private IPAddress   address;    // The IP address (or IPAddress.Any)
        private int         port;       // The port number

        /// <summary>
        /// Constructs a network binding from an <see cref="IPAddress" />
        /// and port number.
        /// </summary>
        /// <param name="address">The IP address.</param>
        /// <param name="port">The port number.</param>
        public NetworkBinding(IPAddress address, int port)
        {
            this.host    = string.Empty;
            this.address = address;
            this.port    = port;
        }

        /// <summary>
        /// Constructs a network binding from a DNS host name and a port number.
        /// </summary>
        /// <param name="host">The host name.</param>
        /// <param name="port">The port number.</param>
        public NetworkBinding(string host, int port)
        {
            if (String.Compare(host, "ANY", StringComparison.OrdinalIgnoreCase) == 0)
            {
                this.host    = string.Empty;
                this.address = IPAddress.Any;
                this.port    = port;

                return;
            }

            this.host    = host;
            this.address = IPAddress.Any;
            this.port    = port;
        }

        /// <summary>
        /// Used by Clone().
        /// </summary>
        private NetworkBinding()
        {
        }

        /// <summary>
        /// Creates a deep clone of this instance.
        /// </summary>
        /// <returns>The cloned <see cref="NetworkBinding" />.</returns>
        public NetworkBinding Clone()
        {
            var clone = new NetworkBinding();

            clone.host    = this.host;
            clone.address = this.address;
            clone.port    = this.port;

            return clone;
        }

        /// <summary>
        /// Parses a network binding from a string formatted as
        /// <b>host:port</b>, <b>ipaddress:port</b>, or just <b>ANY</b>.
        /// </summary>
        /// <param name="binding">The binding string.</param>
        /// <remarks>
        /// The method is also able to parse some well known port names as
        /// well as integer port numbers.  See <see cref="NetworkPort" /> for the
        /// list of supported port constants.
        /// </remarks>
        /// <exception cref="FormatException">Thrown if the string is improperly formatted.</exception>
        public NetworkBinding(string binding)
        {
            const string BadBinding = "Invalid network binding";

            int         pos;
            string      host;
            string      port;

            if (String.Compare(binding, "ANY", StringComparison.OrdinalIgnoreCase) == 0)
            {
                this.host    = string.Empty;
                this.address = IPAddress.Any;
                this.port    = 0;
                return;
            }

            pos = binding.IndexOf(':');
            if (pos == -1)
                throw new FormatException(BadBinding);

            host = binding.Substring(0, pos).Trim();
            port = binding.Substring(pos + 1).Trim();

            if (host.Length == 0 || port.Length == 0)
                throw new FormatException(BadBinding);

            if (Helper.TryParseIPAddress(host, out this.address))
                this.host = string.Empty;
            else
            {
                if (String.Compare(host, "ANY", StringComparison.OrdinalIgnoreCase) != 0)
                    this.host = host;
                else
                    this.host = string.Empty;

                this.address = IPAddress.Any;
            }

            if (!NetworkPort.TryParse(port, out this.port))
                throw new FormatException(BadBinding);
        }

        /// <summary>
        /// Returns the IP address of the binding, performing a DNS
        /// resolution if <see cref="IsHost" /> is true.
        /// </summary>
        /// <exception cref="SocketException">Thrown if the DNS resolution fails.</exception>
        public IPAddress Address
        {
            get
            {
#if SILVERLIGHT
                throw new NotImplementedException("DNS is not implemented in Silverlight");
#else
                if (!string.IsNullOrWhiteSpace(host))
                {
                    try
                    {
                        foreach (var addr in Dns.GetHostEntry(host).AddressList)
                        {
                            if (addr.AddressFamily != AddressFamily.InterNetwork)
                                continue;

                            return addr;
                        }
                    }
                    catch (SocketException)
                    {
                        SysLog.LogWarning("Host lookup for [{0}] failed.", host);
                        throw;
                    }
                }

                return this.address;
#endif
            }
        }

        /// <summary>
        /// Returns <c>true</c> if the binding was initialized with a host name
        /// rather than an IP address.
        /// </summary>
        public bool IsHost
        {
            get { return host.Length != 0; }
        }

        /// <summary>
        /// Returns the binding's host name or <c>null</c>.
        /// </summary>
        public string Host
        {
            get { return IsHost ? host : null; }
        }

        /// <summary>
        /// Returns the host name if one was specified, otherwise returns the IP address 
        /// rendered as a string.
        /// </summary>
        public string HostOrAddress
        {
            get { return IsHost ? host : address.ToString(); }
        }

        /// <summary>
        /// Returns the binding's TCP/UDP port number.
        /// </summary>
        public int Port
        {
            get { return port; }
        }

        /// <summary>
        /// Returns <c>true</c> if the host is empty and <see cref="Address" /> is
        /// <see cref="IPAddress.Any" /> or <see cref="Port" /> is zero.
        /// </summary>
        public bool IsAny
        {
            get { return (!IsHost && address == IPAddress.Any) || port == 0; }
        }

        /// <summary>
        /// Returns <c>true</c> if the host is empty and <see cref="Address" /> is
        /// <see cref="IPAddress.Any" />.
        /// </summary>
        public bool IsAnyAddress
        {
            get { return (!IsHost && address == IPAddress.Any); }
        }

        /// <summary>
        /// Renders the binding as a string.
        /// </summary>
        /// <returns>The binding string.</returns>
        public override string ToString()
        {
            return string.Format("{0}:{1}", IsHost ? host.ToString() : address.ToString(), port);
        }

        /// <summary>
        /// Returns <c>true</c> if the object passed equals this object.
        /// </summary>
        /// <param name="o">The object to be compared.</param>
        /// <returns><c>true</c> if the objects are equal.</returns>
        public override bool Equals(object o)
        {
            var binding = o as NetworkBinding;

            if ((object)binding == null)
                return false;

            return this.host == binding.host &&
                   this.address.Equals(binding.address) &&
                   this.port == binding.port;
        }

        /// <summary>
        /// Computes a hash code for the instance.
        /// </summary>
        /// <returns>The hash code.</returns>
        public override int GetHashCode()
        {
            if (IsHost)
                return host.GetHashCode() ^ port;
            else
                return address.GetHashCode() ^ port;
        }
    }
}
