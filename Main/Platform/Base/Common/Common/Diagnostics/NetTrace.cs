//-----------------------------------------------------------------------------
// FILE:        NetTrace.cs
// CONTRIBUTOR: Jeff Lill
// COPYRIGHT:   Copyright (c) 2005-2015 by Jeffrey Lill.  All rights reserved.
// DESCRIPTION: Used for injecting trace information into a network wide trace
//              gathering infrastructure.

#undef DEBUGLOG         // Define this to write to the debug log

using System;
using System.Text;
using System.Net;
using System.Net.Sockets;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.Reflection;

using LillTek.Windows;

namespace LillTek.Common
{
    /// <summary>
    /// Used for injecting trace information into a network wide trace
    /// gathering infrastructure.
    /// </summary>
    /// <remarks>
    /// <para>
    /// The NetTrace and NetTraceSink classes work together to implement a
    /// general purpose network wide debug tracing mechanism.  Instances
    /// of these classes use UDP multicast to deliver NetTracePacket instance
    /// from trace sources to trace sinks.
    /// </para>
    /// <note>
    /// Tracing is available only if TRACE is defined at compile time.
    /// </note>
    /// </remarks>
    public sealed class NetTrace
    {
        internal const string   DefTraceGroup = MulticastGroup.NetTraceGroup;
        internal const int      DefTracePort  = NetworkPort.NetTrace;

        //---------------------------------------------------------------------
        // Static members

        private static object                       syncLock = new object();        // Synchronization root
        private static NetTrace                     traceSrc = null;                // The global trace source
        private static Dictionary<string, int>      traceEnable = null;             // Trace enable levels keyed by subsystem name
        private static bool                         tracePause = false;             // True if tracing is temporarily disabled

        /// <summary>
        /// Starts a global trace source.
        /// </summary>
        /// <remarks>
        /// <para>
        /// This method configures the source to broadcast information to the
        /// multicast group and port retrieved from the "Diagnostics.TraceEP"
        /// setting, or to a hardcoded default if this setting is not present.
        /// </para>
        /// <para>
        /// The method also loads any trace enablers found in the Diagnostics.TraceEnable[]
        /// configuration settings.  Each enabler setting specifies a subsystem name
        /// and detail level for which tracing should be enabled.  These configuration
        /// parameters should be formatted as [detail]:[subsystem], where [detail] is a
        /// detail level number in the range of 0..255 and [subsystem] is the subsystem
        /// name.  Here's an configuration file example:
        /// </para>
        /// <code language="none">
        /// #section Diagnostics
        /// 
        ///     TraceEnable[0] = 0:DEFAULT
        ///     TraceEnable[1] = 0:LillTek.Messaging
        ///     TraceEnable[2] = 1:MyApplication
        /// 
        /// #endsection
        /// </code>
        /// <para>
        /// These settings will enable tracing for all events from the LillTek.Messaging
        /// subsystem whose detail level is less than or equal to 0 as well as events
        /// from MyApplication whose detail level is less than or equal to 1.
        /// </para>
        /// <para>
        /// The <b>DEFAULT</b> subsystem is treated specially.  If present, this specifies
        /// the diagnostics level to be used for for tracing subsystems that are not
        /// specified in the configuration.  If <b>DEFAULT</b> and a particuliar subsystem
        /// isn't in the configureation, then no trace information will be generated for
        /// that subsystem.
        /// </para>
        /// <para>
        /// Two other trace related configuration settings may also be specified within
        /// the <b>Diagnostics</b> section: <b>TraceEP</b> and <b>TraceAdapter</b>.
        /// <b>TraceEP</b> can be used to override the multicast endpoint where trace
        /// packets are to be delivered and <b>TraceAdapter</b> specifies the IP address
        /// of the network adapter to be used for tranbsmitting the packets.
        /// </para>
        /// </remarks>
        [Conditional("TRACE")]
        public static void Start()
        {
            lock (syncLock)
            {
                IPEndPoint      traceEP;
                IPAddress       traceAdapter;
                Config          config;
                string[]        enableConfig;
                int             pos;
                int             detail;
                string          subsystem;

                config       = new Config("Diagnostics");
                traceEP      = config.Get("TraceEP", new IPEndPoint(Helper.ParseIPAddress(DefTraceGroup), DefTracePort));
                traceAdapter = config.Get("TraceAdapter", IPAddress.Any);

                if (traceSrc != null)
                {
                    traceSrc.Stop(null);
                    traceSrc = null;
                }

                traceSrc = new NetTrace();
                traceSrc.Start(traceEP, traceAdapter);

                tracePause   = false;
                traceEnable  = new Dictionary<string, int>();
                enableConfig = config.GetArray("TraceEnable");

                for (int i = 0; i < enableConfig.Length; i++)
                {
                    string s = enableConfig[i];

                    try
                    {
                        pos = s.IndexOf(':');
                        if (pos == -1)
                            throw new Exception();

                        detail = int.Parse(s.Substring(0, pos));
                        if (detail < 0 || detail > 255)
                            throw new Exception();

                        subsystem = s.Substring(pos + 1);

                        traceEnable[subsystem.ToLowerInvariant()] = detail;
                    }
                    catch
                    {
                        SysLog.LogError("Invalid trace config: Diagnostics.TraceEP[{0}] = {1}", i, s);
                    }
                }
            }
        }

        /// <summary>
        /// Stops the global trace source (if one is running).
        /// </summary>
        [Conditional("TRACE")]
        public static void Stop()
        {
            lock (syncLock)
            {
                if (traceSrc != null)
                    traceSrc.Stop(null);

                traceSrc = null;
                traceEnable = null;
            }
        }

        /// <summary>
        /// Enables tracing for a subsystem and detail level.
        /// </summary>
        /// <param name="subsystem">The subsystem name (or "DEFAULT").</param>
        /// <param name="detail">The detail level (0..255).</param>
        /// <remarks>
        /// This method will enable tracing for the named subsystem
        /// and for events whose detail is less than or equal to the
        /// value passed.
        /// </remarks>
        [Conditional("TRACE")]
        public static void Enable(string subsystem, int detail)
        {
            lock (syncLock)
            {
                if (traceEnable == null)
                    return;

                if (detail < 0 || detail > 255)
                    throw new ArgumentException("Must be in the range 0..255.", "detail");

                traceEnable[subsystem.ToLowerInvariant()] = detail;
            }
        }

        /// <summary>
        /// Returns the trace detail level for the named subsystem or <b>-1</b> if tracing is disabled.
        /// </summary>
        /// <param name="subsystem">The subsystem name (or "DEFAULT").</param>
        /// <returns></returns>
        public static int TraceDetail(string subsystem)
        {
            int     detail;
            int     defDetail;

            lock (syncLock)
            {
                if (traceEnable == null)
                    return -1;

                if (!traceEnable.TryGetValue("default", out defDetail))
                    defDetail = -1;

                subsystem = subsystem.ToLowerInvariant();
                if (subsystem == "default")
                    return defDetail;
                else
                {
                    if (traceEnable.TryGetValue(subsystem, out detail))
                        return detail;
                    else
                        return defDetail;
                }
            }
        }

        /// <summary>
        /// Temporarily pauses tracing.
        /// </summary>
        [Conditional("TRACE")]
        public static void Pause()
        {
            tracePause = true;
        }

        /// <summary>
        /// Temporarily resumes tracing.
        /// </summary>
        [Conditional("TRACE")]
        public static void Resume()
        {
            tracePause = false;
        }

        /// <summary>
        /// Writes the specified trace strings to the trace log.
        /// </summary>
        /// <param name="subsystem">The subsystem name (limited to 32 characters).</param>
        /// <param name="detail">The detail level 0..255 (higher values indicate more detail).</param>
        /// <param name="tEvent">The trace event.</param>
        /// <param name="summary">The trace summary.</param>
        /// <param name="details">The trace details.</param>
        [Conditional("TRACE")]
        public static void Write(string subsystem, int detail, string tEvent, string summary, string details)
        {
            lock (syncLock)
            {
                if (tracePause)
                    return;

                if (traceSrc != null)
                    traceSrc.Log(subsystem, detail, tEvent, summary, details);
            }
        }

        /// <summary>
        /// Writes the exception to the trace log.
        /// </summary>
        /// <param name="subsystem">The subsystem name (limited to 32 characters).</param>
        /// <param name="detail">The detail level 0..255 (higher values indicate more detail).</param>
        /// <param name="tEvent">The trace event.</param>
        /// <param name="e">The exception.</param>
        [Conditional("TRACE")]
        public static void Write(string subsystem, int detail, string tEvent, Exception e)
        {
            lock (syncLock)
            {
                if (tracePause)
                    return;

                if (traceSrc != null)
                    traceSrc.Log(subsystem, detail, tEvent, e);
            }
        }

        //---------------------------------------------------------------------
        // Instance members

        private Socket          sock;           // Multicast socket
        private IPEndPoint      traceEP;        // The trace multicast endpoint
        private IPAddress       traceAdapter;   // IP address of the network adapter (or IPAddress.Any)
        private int             sourceID;       // The trace source's machine local unique ID
        private int             packetNum;      // The packet number for the next packet transmittedk

        /// <summary>
        /// Constructor.
        /// </summary>
        public NetTrace()
        {
            this.sock = null;
        }

        /// <summary>
        /// Starts the trace source by opening a multicast socket.
        /// </summary>
        /// <param name="traceEP">The multicast group and port for the trace sources and sinks.</param>
        /// <param name="traceAdapter">
        /// The IP address of the network adapter to be used for transmitting trace 
        /// packets or <see cref="IPAddress.Any" />.
        /// </param>
        [Conditional("TRACE")]
        public void Start(IPEndPoint traceEP, IPAddress traceAdapter)
        {
            lock (syncLock)
            {
                this.sourceID     = Environment.TickCount;
                this.packetNum    = 0;
                this.traceEP      = traceEP;
                this.traceAdapter = traceAdapter;

                sock = new Socket(AddressFamily.InterNetwork, SocketType.Dgram, ProtocolType.Udp);
                sock.SetSocketOption(SocketOptionLevel.Socket, SocketOptionName.ReuseAddress, 1);
                sock.SetSocketOption(SocketOptionLevel.Socket, SocketOptionName.Broadcast, 1);
                sock.Bind(new IPEndPoint(traceAdapter, traceEP.Port));

                try
                {
                    if (traceAdapter.Equals(IPAddress.Any))
                        sock.SetSocketOption(SocketOptionLevel.IP, SocketOptionName.AddMembership, new MulticastOption(traceEP.Address));
                    else
                        sock.SetSocketOption(SocketOptionLevel.IP, SocketOptionName.AddMembership,
                                             new MulticastOption(traceEP.Address, traceAdapter));

                    sock.SetSocketOption(SocketOptionLevel.IP, SocketOptionName.MulticastTimeToLive, 5);
                    sock.SetSocketOption(SocketOptionLevel.IP, SocketOptionName.MulticastLoopback, 1);
                }
                catch
                {
                }
            }
        }

        /// <summary>
        /// Stops the trace source.
        /// </summary>
        /// <param name="o">Not used (pass as null).</param>
        [Conditional("TRACE")]
        public void Stop(object o)
        {
            lock (syncLock)
            {
                if (sock != null)
                {
                    sock.Close();
                    sock = null;
                }
            }
        }

        /// <summary>
        /// Writes the specified formated string to the trace log.
        /// </summary>
        /// <param name="subsystem">The subsystem name (limited to 32 characters).</param>
        /// <param name="detail">The detail level 0..255 (higher values indicate more detail).</param>
        /// <param name="tEvent">The trace event.</param>
        /// <param name="summary">The trace summary.</param>
        /// <param name="details">The trace details.</param>
        [Conditional("TRACE")]
        public void Log(string subsystem, int detail, string tEvent, string summary, string details)
        {
            if (tracePause)
                return;

            if (subsystem == null)
                subsystem = string.Empty;

            if (tEvent == null)
                tEvent = string.Empty;

            if (summary == null)
                summary = string.Empty;

            if (details == null)
                details = string.Empty;

#if DEBUGLOG
            StringBuilder   sb = new StringBuilder(1024);

            sb.Append("==========\r\n");
            sb.AppendFormat(null,"Event:   {0}\r\n",tEvent);
            sb.AppendFormat(null,"Summary: {0}\r\n\r\n",summary);
            sb.Append(details);
            if (!details.EndsWith("\r\n"))
                sb.Append("\r\n");

            Debug.Write(sb.ToString());
#else
            NetTracePacket  packet;
            byte[]          buf;
            int             cb;
            int             traceDetail;

            lock (syncLock)
            {
                if ((!traceEnable.TryGetValue(subsystem.ToLowerInvariant(), out traceDetail) || detail > traceDetail) &&
                    (!traceEnable.TryGetValue("default", out traceDetail) || detail > traceDetail))
                {
                    return;
                }

                try
                {
                    packet = new NetTracePacket(sourceID, packetNum++, subsystem, detail, tEvent, summary, details);
                    buf    = new byte[NetTracePacket.MaxPacket];
                    cb     = packet.Write(buf);

                    sock.SendTo(buf, cb, SocketFlags.None, traceEP);
                }
                catch
                {
                }
            }
#endif
        }

        /// <summary>
        /// Writes the exception to the trace log.
        /// </summary>
        /// <param name="subsystem">The subsystem name (limited to 32 characters).</param>
        /// <param name="detail">The detail level 0..255 (higher values indicate more detail).</param>
        /// <param name="tEvent">The trace event.</param>
        /// <param name="e">The exception.</param>
        [Conditional("TRACE")]
        public void Log(string subsystem, int detail, string tEvent, Exception e)
        {
            const string format =
@"Exception: {0}
Message:   {1}
Stack:

";
            if (tracePause)
                return;

            StringBuilder               sb = new StringBuilder();
            TargetInvocationException   eInvoke;

            eInvoke = e as TargetInvocationException;
            if (eInvoke != null)
                e = eInvoke.InnerException;

            sb.AppendFormat(null, format, e.GetType().ToString(), e.Message);
            if (e.StackTrace == null)
                sb.Append("Not available (null)");
            else
                sb.AppendFormat(e.StackTrace);
            Log(subsystem, detail, tEvent, e.GetType().Name, sb.ToString());
        }
    }
}
