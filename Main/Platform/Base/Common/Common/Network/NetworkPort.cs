//-----------------------------------------------------------------------------
// FILE:        NetworkPort.cs
// CONTRIBUTOR: Jeff Lill
// COPYRIGHT:   Copyright (c) 2005-2015 by Jeffrey Lill.  All rights reserved.
// DESCRIPTION: Network port numbers.

using System;
using System.Net;
using System.Net.Sockets;
using System.Collections.Generic;

namespace LillTek.Common
{
    /// <summary>
    /// Defines some common network port numbers as well as the <see cref="TryParse" /> method.
    /// </summary>
    public static class NetworkPort
    {
        /// <summary>
        /// HyperText Transport Protocol (port <b>80</b>).
        /// </summary>
        public const int HTTP = 80;

        /// <summary>
        /// Secure HyperText Transport Protocol (port <b>443</b>).
        /// </summary>
        public const int HTTPS = 443;

        /// <summary>
        /// Secure Socket Layer (port <b>443</b>).
        /// </summary>
        public const int SSL = 443;

        /// <summary>
        /// Domain Name System (port <b>53</b>).
        /// </summary>
        public const int DNS = 53;

        /// <summary>
        /// Simple Message Transport Protocol (port <b>25</b>).
        /// </summary>
        public const int SMTP = 25;

        /// <summary>
        /// Post Office Protocol version 3 (port <b>110</b>).
        /// </summary>
        public const int POP3 = 110;

        /// <summary>
        /// Remote terminal protocol (port <b>23</b>).
        /// </summary>
        public const int TELNET = 23;

        /// <summary>
        /// File Transfer Protocol (control) (port <b>21</b>).
        /// </summary>
        public const int FTP = 21;

        /// <summary>
        /// File Transfer Protocol (data) (port <b>20</b>).
        /// </summary>
        public const int FTPDATA = 20;

        /// <summary>
        /// Secure File Transfer Protocol (port <b>22</b>).
        /// </summary>
        public const int SFTP = 22;

        /// <summary>
        /// RADIUS authentication and billing protocol (port <b>1812</b>).
        /// </summary>
        public const int RADIUS = 1812;

        /// <summary>
        /// Authentication, Authorization, and Accounting.  This port was
        /// originally used by the RADIUS protocol and is still used
        /// fairly widely (port <b>1645</b>).
        /// </summary>
        public const int AAA = 1645;

        /// <summary>
        /// PING (port <b>7</b>).
        /// </summary>
        public const int ECHO = 7;

        /// <summary>
        /// Daytime (RFC 867) (port <b>13</b>).
        /// </summary>
        public const int DAYTIME = 13;

        /// <summary>
        /// Trivial File Transfer Protocol (port <b>69</b>).
        /// </summary>
        public const int TFTP = 69;

        /// <summary>
        /// Secure Shell (port <b>22</b>).
        /// </summary>
        public const int SSH = 22;

        /// <summary>
        /// TIME protocol (port <b>37</b>).
        /// </summary>
        public const int TIME = 37;

        /// <summary>
        /// Network Time Protocol (port <b>123</b>).
        /// </summary>
        public const int NTP = 123;

        /// <summary>
        /// Internet Message Access Protocol (port <b>143</b>).
        /// </summary>
        public const int IMAP = 143;

        /// <summary>
        /// Simple Network Managenment Protocol (port <b>161</b>).
        /// </summary>
        public const int SNMP = 161;

        /// <summary>
        /// Simple Network Managenment Protocol (trap) (port <b>162</b>)
        /// </summary>
        public const int SNMPTRAP = 162;

        /// <summary>
        /// Lightweight Directory Access Protocol (port <b>389</b>).
        /// </summary>
        public const int LDAP = 389;

        /// <summary>
        /// Lightweight Directory Access Protocol over TLS/SSL (port <b>636</b>).
        /// </summary>
        public const int LDAPS = 636;

        /// <summary>
        /// Session Initiation Protocol (port <b>5060</b>).
        /// </summary>
        public const int SIP = 5060;

        /// <summary>
        /// Secure Session Initiation Protocol (over TLS) (port <b>5061</b>).
        /// </summary>
        public const int SIPS = 5061;

        /// <summary>
        /// The LillTek UDP Broadcast Service (UDP-BROADCAST) (port <b>9165</b>).
        /// </summary>
        public const int UdpBroadcast = 9165;

        /// <summary>
        /// The LillTek Dynamic DNS service's UDP messaging port (DYNAMIC-DNS) (port <b>9166</b>).
        /// </summary>
        public const int DynamicDns = 9166;

        /// <summary>
        /// Used used by the LillTek Heartbeat service to accept inbound
        /// HTTP health requests (port <b>9167</b>).
        /// </summary>
        public const int HttpHeartbeat = 9167;

        /// <summary>
        /// The default port for the <a href="http://en.wikipedia.org/wiki/Squid_%28software%29">Squid</a>
        /// open source proxy project.
        /// </summary>
        public const int SQUID = 3128;

        /// <summary>
        /// The SOCKS (Socket Secure) proxy port.
        /// </summary>
        public const int SOCKS = 1080;

        /// <summary>
        /// The default LillTek NetTrace port.
        /// </summary>
        public const int NetTrace = 47743;

        /// <summary>
        /// LillTek Communication Gateway default TCP and UDP port (port <b>4530</b>).
        /// </summary>
        /// <remarks>
        /// <note>
        /// This port was chosen to be within the range of <b>4502-4534</b> since these
        /// are the only ports accessible to non-elevated Silverlight applications.
        /// </note>
        /// </remarks>
        public const int LillCom = 4530;

        private static Dictionary<string, int> wellKnownMap;

        private struct Map
        {
            public string   Name;
            public int      Port;

            public Map(string name, int Port)
            {
                this.Name = name;
                this.Port = Port;
            }
        }

        static NetworkPort()
        {
            // Initialize the well known port map.

            var ports = new Map[] {

                new Map("ANY", 0),
                new Map("HTTP", HTTP),
                new Map("HTTPS", HTTPS),
                new Map("SSL", SSL),
                new Map("DNS", DNS),
                new Map("SMTP", SMTP),
                new Map("POP3", POP3),
                new Map("TELNET", TELNET),
                new Map("FTP", FTP),
                new Map("FTPDATA", FTPDATA),
                new Map("SFTP", SFTP),
                new Map("RADIUS", RADIUS),
                new Map("AAA", AAA),
                new Map("ECHO", ECHO),
                new Map("DAYTIME", DAYTIME),
                new Map("TFTP", TFTP),
                new Map("SSH", SSH),
                new Map("TIME", TIME),
                new Map("NTP", NTP),
                new Map("IMAP", IMAP),
                new Map("SNMP", SNMP),
                new Map("SNMTRAP", SNMPTRAP),
                new Map("LDAP", LDAP),
                new Map("LDAPS", LDAPS),
                new Map("SIP", SIP),
                new Map("SIPS", SIPS),
                new Map("SQUID", SQUID),
                new Map("SOCKS", SOCKS),
                new Map("UDP-BROADCAST", UdpBroadcast),
                new Map("DYNAMIC-DNS", DynamicDns),
                new Map("HTTP-HEARTBEAT", HttpHeartbeat),
                new Map("NETTRACE", NetTrace),
                new Map("LILLCOM", LillCom)
            };

            wellKnownMap = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
            foreach (Map map in ports)
                wellKnownMap.Add(map.Name, map.Port);
        }

        /// <summary>
        /// Attempts to parse an integer or well known port name from a string
        /// and return the integer TCP port number.
        /// </summary>
        /// <param name="input">The port number or name as as string.</param>
        /// <param name="port">Receives the parsed port number.</param>
        /// <returns><c>true</c> if a port was successfulyy parsed.</returns>
        public static bool TryParse(string input, out int port)
        {
            port = 0;
            input = input.Trim();

            if (int.TryParse(input, out port))
                return true;

            return wellKnownMap.TryGetValue(input, out port);
        }
    }
}
