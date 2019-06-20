//-----------------------------------------------------------------------------
// FILE:        ChannelEP.cs
// CONTRIBUTOR: Jeff Lill
// COPYRIGHT:   Copyright (c) 2005-2015 by Jeffrey Lill.  All rights reserved.
// DESCRIPTION: Defines the ChannelEP class which specifies the information
//              necessary to route a message to a particular MsgRouter
//              instance on the network.

using System;
using System.Net;

using LillTek.Common;

namespace LillTek.Messaging
{
    /// <summary>
    /// Describes the information necessary to route a message to a 
    /// particular MsgRouter instance on the network.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Channel endpoints may be represented as strings:
    /// </para>
    /// <code language="none">
    ///     tcp://IP:port   - TCP
    ///     udp://IP:port   - Unicast UDP
    ///     mcast://IP:port - Multicast UDP
    /// </code>    
    /// <para>
    /// Where IP is the IP address in dotted quad notation and port is
    /// the TCP port number.  IP addresses of ANY are valid and will
    /// be translated internally to the loop back address of 127.0.0.1.
    /// Multicast endpoints also support an IP address of "*" which 
    /// is translated internally to 255.255.255.255.
    /// </para>
    /// </remarks>
    public sealed class ChannelEP
    {
        //---------------------------------------------------------------------
        // Static members

        /// <summary>
        /// Implicit cast converting a channel endpoint to a string.
        /// </summary>
        /// <param name="ep">The channel endpoint.</param>
        /// <returns>The string representation of the endpoint.</returns>
        public static implicit operator string(ChannelEP ep)
        {
            return ep.ToString();
        }

        /// <summary>
        /// Implicit cast converting a string into the corresponding
        /// channel endpoint.
        /// </summary>
        /// <param name="value">The string representation.</param>
        /// <returns>The corresponding channel endpoint.</returns>
        public static implicit operator ChannelEP(string value)
        {
            return ChannelEP.Parse(value);
        }

        /// <summary>
        /// Parses the a channel endpoint from the uri string passed.
        /// </summary>
        /// <param name="uri">The channel uri.</param>
        public static ChannelEP Parse(string uri)
        {
            return new ChannelEP(uri);
        }

        //---------------------------------------------------------------------
        // Instance members

        /// <summary>
        /// Specifies the network transport options.
        /// </summary>
        public Transport Transport;

        /// <summary>
        /// The network endpoint (IP,port) of the source or destination router.
        /// </summary>
        public IPEndPoint NetEP;

        /// <summary>
        /// Constructor.
        /// </summary>
        /// <param name="transport">The transport instance.</param>
        /// <param name="netEP">IP endpoint MsgRouter.</param>
        public ChannelEP(Transport transport, IPEndPoint netEP)
        {
            this.Transport = transport;
            this.NetEP     = netEP;

            Assertion.Test(this.NetEP != null);
        }

        /// <summary>
        /// Parses the endpoint from the uri string.
        /// </summary>
        /// <param name="uri">The uri.</param>
        public ChannelEP(string uri)
        {
            IPAddress   addr;
            int         port;
            int         pos;
            string      scheme;
            string      body;

            if (uri.IndexOf("*") != -1)
                uri = uri.Replace("*", "255.255.255.255");

            uri = uri.ToLowerInvariant();
            pos = uri.IndexOf("://");
            if (pos == -1)
                throw new FormatException("Missing uri scheme.");

            scheme = uri.Substring(0, pos);
            body = uri.Substring(pos + 3);    // ("://").Length

            pos = body.IndexOf(':');
            if (pos == -1)
                throw new FormatException("Missing port.");

            try
            {
                addr = IPAddress.Parse(body.Substring(0, pos));
            }
            catch
            {
                throw new FormatException("Invalid IP address.");
            }

            try
            {
                port = int.Parse(body.Substring(pos + 1));
            }
            catch
            {
                throw new FormatException("Invalid port.");
            }

            if (port < 0 || port > ushort.MaxValue)
                throw new FormatException("Invalid port.");

            switch (scheme)
            {
                case "tcp":

                    this.Transport = Transport.Tcp;
                    break;

                case "udp":

                    this.Transport = Transport.Udp;
                    break;

                case "mcast":

                    this.Transport = Transport.Multicast;
                    break;

                default:

                    throw new FormatException("Invalid uri scheme.");
            }

            this.NetEP = new IPEndPoint(addr, port);
            Assertion.Test(this.NetEP != null);
        }

        /// <summary>
        /// Tests this instance to the object passed for equality.
        /// </summary>
        /// <param name="obj">The object being compared.</param>
        /// <returns><c>true</c> if the objects represent the same value.</returns>
        public override bool Equals(object obj)
        {
            ChannelEP ep;

            ep = obj as ChannelEP;
            if (ep == null)
                return false;

            if (ep == this)
                return true;

            return this.Transport == ep.Transport &&
                   this.NetEP.Port == ep.NetEP.Port &&
                   this.NetEP.Address.Equals(ep.NetEP.Address);
        }

        /// <summary>
        /// Returns the hash code for this instance.
        /// </summary>
        /// <returns></returns>
        public override int GetHashCode()
        {
            return (int)Transport ^ NetEP.Address.GetHashCode() ^ NetEP.Port;
        }

        /// <summary>
        /// Returns the string form of the endpoint.
        /// </summary>
        public override string ToString()
        {
            switch (Transport)
            {
                case Transport.Udp:

                    return "udp://" + NetEP.ToString();

                case Transport.Tcp:

                    return "tcp://" + NetEP.ToString();

                case Transport.Multicast:

                    if (NetEP.Address.Equals(IPAddress.Broadcast))
                        return "mcast://*:" + NetEP.Port.ToString();
                    else
                        return "mcast://" + NetEP.ToString();

                default:

                    Assertion.Fail("Unexpected transport.");
                    return null;
            }
        }
    }
}
