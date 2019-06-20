//-----------------------------------------------------------------------------
// FILE:        MsgEP.cs
// CONTRIBUTOR: Jeff Lill
// COPYRIGHT:   Copyright (c) 2005-2015 by Jeffrey Lill.  All rights reserved.
// DESCRIPTION: Defines the MsgEP class which specifies the information
//              necessary to route a message to a particular MsgRouter,
//              and device on the network.

using System;
using System.Collections.Generic;
using System.Net;
using System.Text;

using LillTek.Common;

namespace LillTek.Messaging
{
    /// <summary>
    /// Describes an endpoint capable of receiving a message from the messaging library.
    /// </summary>
    /// <remarks>
    /// <para>
    /// See the extensive comment for <see cref="OverviewDoc">LillTek.Messaging</see>
    /// for more information.
    /// </para>
    /// <para><b><u>Logical Endpoints</u></b></para>
    /// <para>
    /// Logical endpoints are used to identify the target of a message without
    /// regard to network topology or any other physical characteristics of
    /// the servers or applications.  Logical endpoints have the form 
    /// </para>
    /// <code language="none">
    /// logical://seg1/seg2/seg3/...
    /// </code>
    /// <para>
    /// Where "logical:" be any valid non-empty URI characters with the
    /// exception of the wildcard "*".  Logical endpoints also
    /// support a simple concept of wildcards.  A wildcard endpoint has
    /// a single "*" character in the last segment.
    /// </para>
    /// <para>
    /// Logical endpoints rooted at <b>logical://null</b> are treated
    /// specially by message routers.  Message routers will immediately
    /// discard any messages targeted at null endpoints.  <see cref="IsNull" />
    /// can be called to determine if an endpoint is a null endpoint.
    /// </para>
    /// <para><b><u>Physical Endpoints</u></b></para>
    /// <para>
    /// Physcial endpoints provide a low level mechanism for specifying
    /// the specific source or destination of a message.  Whereas a logical 
    /// endpoint might map to multiple endpoints on the network, a physical
    /// andpoint always maps to at most one network endpoint, similar to
    /// how an IP address maps to a specific host on the Internet.
    /// </para>
    /// <para>
    /// Physical endpoints are used to identify a specific <see cref="MsgRouter" />
    /// instance on the network, as well as its place in a heirarchy of routers.
    /// Router instances can be configured with a hardcoded physical endpoint
    /// (similar to assigining a specific IP address to a host machine), but 
    /// most deployments will use globally unique identifiers generated during
    /// router start to generate a unique address for the router.  Routers
    /// implement a multicast discovery protocol to identify their peers.
    /// </para>
    /// <para>
    /// Physical endpoints are used internally by the network of message
    /// routers to discover the other physical routers and the logical
    /// endpoints they expose as well as for routing messages between
    /// logical endpoints.
    /// </para>
    /// <para>
    /// Physical endpoints are represented as URI formatted as shown below:
    /// </para>
    /// <code language="none">
    /// physical://root/level0/level1/...?options
    /// </code>
    /// <para>
    /// Physical message endpoints are hierarchical, with zero or more levels 
    /// of static routing, an optional channel endpoint and an optional object ID.
    /// <para>
    /// </para>
    /// The root of the hierarchy is specified as a host name (or IP address)
    /// and a port of a Root router.  Root routers are designed to be located
    /// on the Internet and act as a routing relay between routers behind a
    /// firewall and those outside.
    /// </para>
    /// <para>
    /// It is also possible to specify the root router level as "DETACHED"
    /// as in physical://DETACHED/level0/level1.  This indicates that no 
    /// attempt should be made to route messages on the Internet.
    /// </para>
    /// <para>
    /// Beneath the root, you'll find zero or more levels of router names.
    /// Each router is responsible for handling the routing of messages
    /// to the routers beneath it in the hierarchy.  Messages to be delivered
    /// outside a router's heiararchy will be routed to the parent router.
    /// This is very much like the concept of a default gateway for a simple
    /// single router subnet.
    /// </para>
    /// <note>
    /// <para>
    /// The current LillTek Messaging implementaion is hardcoded to accept
    /// only physical addresses with a maximum of three levels.  The top level
    /// specifying the root router, the second level specifying a hub router,
    /// and the third level a leaf router.
    /// </para>
    /// <para>
    /// Future releases of the LillTek Platform will generalize physical addressing
    /// to support an arbitrarily addressing heiararchy.
    /// </para>
    /// </note>
    /// <para>
    /// The options section of the uri can currently specify up to two
    /// parameters: one specifying a target object instance, and the other
    /// specifying a low-level channel endpoint:
    /// </para>
    /// <code language="none">
    /// o=objectID
    /// c=channelEP
    /// </code>        
    /// <para>
    /// An object name is simply a string used by the leaf router to
    /// route the message to a specific object instance.  The format
    /// of this string is application dependant.
    /// </para>
    /// <para>
    /// The channel endpoint can be thought of as a hint to the router,
    /// suggesting the network transport and addressing to use to route
    /// the message.  There is one extreme example of this:
    /// </para>
    /// <code language="none">
    /// physical://?c=tcp://127.0.0.1:55
    /// </code>        
    /// <para>
    /// In this example, the root and sub router fields weren't specified
    /// in the message endpoint.  In this case, the router will honor the
    /// channel endpoint specified by the "c" parameter in the uri.
    /// </para>
    /// <para><b>Broadcast Endpoints</b></para>
    /// <para>
    /// LillTek message supports the concept of message broadcasting.  Normally,
    /// one of the <see cref="MsgRouter" />'s <b>Broadcast()</b> related methods
    /// will be used to initiate a broadcast.  The messaging layer attempts to
    /// deliver the message to all endpoints in the network that match the 
    /// target endpoint.
    /// </para>
    /// <para>
    /// Sometimes though, it is useful to be able to perform a broadcast 
    /// calling one of the <see cref="MsgRouter" /> <b>Send()</b> methods
    /// by indicating that a broadcast is desired in the target endpoint itself.
    /// </para>
    /// <para>
    /// A broadcast endpoint can be specified by adding the <b>broadcast</b>
    /// query parameter to the endpoint URI, as in:
    /// </para>
    /// <code language="none">
    /// logical://MyNamespace/MyService?broadcast
    /// </code>
    /// <para>
    /// When a message is sent to this endpoint, the messaging layer will attempt
    /// to deliver it to all endpoint instances on the network that match
    /// <b>logical://MyNamespace/MyService</b>.
    /// </para>
    /// <para>
    /// The <see cref="Broadcast" /> property can be used to determine whether the
    /// broadcast query parameter is present in an endpoint.
    /// </para>
    /// <para>
    /// This is used in the LillTek WCF transport channel implementation.
    /// The standard WCF channel shapes don't include the concept of
    /// broadcast.  Short of defining a new channel shape, this is the easiest way
    /// to expose LillTek messaging's broadcast capabilities to WCF applications.
    /// </para>
    /// <note>
    /// The <b>broadcast</b> query parameter is recognized only in calls to a
    /// message router's <b>Send()</b> methods.  Basically, all the router does
    /// is subsititute a call to <b>Broadcast()</b> in this case.  The broadcast
    /// flag will be ignored in all other cases (such as calls to a router's
    /// <b>Query()</b> methods.
    /// </note>
    /// </remarks>
    public sealed class MsgEP
    {
        /// <summary>
        /// Use this constant as the root host name in an physical
        /// endpoint to specify that a hub and its leaf routers
        /// are not attached to a root.
        /// </summary>
        public const string DetachedRoot = "DETACHED";

        /// <summary>
        /// This is a special logical endpoint.  Messages targetted at
        /// this endpoint will not be routed to a handler and will be
        /// discarded immediately by a <see cref="MsgRouter" />.
        /// </summary>
        public const string Null = "logical://null";

        private const int       cPhysicalScheme = 11;   // "physical://".Length;
        private const int       cLogicalScheme = 10;   // "logical://".Length
        private const int       cAbstractScheme = 11;   // "abstract://".Length

        private const string    NotPhysicalMsg = "This operation is allowed only for a physical endpoint.";
        private const string    NotLogicalMsg = "This operation is allowed only for a logical endpoint.";
        private const string    AlreadyInitMsg = "Endpoint has already been initialized.";

        //---------------------------------------------------------------------
        // Static members

        private static readonly char[] allDelimiters = new char[] { '/', ':', '?' };
        private static readonly char[] segDelimiters = new char[] { '/', '?' };
        private static readonly char[] equalAmp      = new char[] { '=', '&' };

        private static Dictionary<string, string> abstractMap;

        /// <summary>
        /// Implicit cast converting a message endpoint to a string.
        /// </summary>
        /// <param name="ep">The message endpoint.</param>
        /// <returns>The string representation of the endpoint.</returns>
        public static implicit operator string(MsgEP ep)
        {
            if (ep == null)
                return null;

            return ep.ToString();
        }

        /// <summary>
        /// Implicit cast converting a string into the corresponding
        /// message endpoint.
        /// </summary>
        /// <param name="value">The string representation.</param>
        /// <returns>The corresponding message endpoint.</returns>
        public static implicit operator MsgEP(string value)
        {
            if (value == null)
                return null;

            return MsgEP.Parse(value);
        }

        /// <summary>
        /// Implicit cast converting a <see cref="Uri" /> into the corresponding
        /// message endpoint.
        /// </summary>
        /// <param name="value">The <see cref="Uri" />.</param>
        /// <returns>The corresponding message endpoint.</returns>
        public static implicit operator MsgEP(Uri value)
        {
            if (value == null)
                return null;

            return MsgEP.Parse(value.ToString());
        }

        /// <summary>
        /// Parses the string and returns the corresponding endpoint object.
        /// </summary>
        /// <param name="value">The endpoint string.</param>
        /// <returns>The corresponding endpoint.</returns>
        /// <remarks>
        /// Returns null if the value passed is <c>null</c> or the empty string.
        /// </remarks>
        public static MsgEP Parse(string value)
        {
            if (value == null || value.Length == 0)
                return null;

            return new MsgEP(value);
        }

        /// <summary>
        /// Parses a string into a logical endpoint and then appends the
        /// instance string as an additional segment.
        /// </summary>
        /// <param name="root">The root endpoint string.</param>
        /// <param name="instance">The instance string.</param>
        /// <returns>
        /// Returns null if the root passed is <c>null</c> or the empty string.
        /// </returns>
        /// <remarks>
        /// <para>
        /// This method is useful for generating an endpoint to a particular
        /// service instance in a cluster.  The value string is first parsed into
        /// a logical endpoint, with any necessary abstract to logical mappings
        /// being performed.  Then the instance segment will be appended.
        /// </para>
        /// <code language="none">
        /// Example:    Parse("logical://foo","bar")
        /// 
        /// Returns:    logical://foo/bar
        /// </code>
        /// </remarks>
        public static MsgEP Parse(string root, string instance)
        {
            if (root == null || root.Length == 0)
                return null;

            return new MsgEP(root, instance);
        }

        /// <summary>
        /// Consutucts a new logical endpoint from the endpoint
        /// passed by appending the instance string as an additional segment.
        /// </summary>
        /// <param name="root">The root endpoint.</param>
        /// <param name="instance">The instance string.</param>
        /// <returns>
        /// Returns the composite endpoint.
        /// </returns>
        public static MsgEP Parse(MsgEP root, string instance)
        {
            return new MsgEP(root.ToString() + "/" + instance);
        }

        /// <summary>
        /// Lexically compares two endpoints.
        /// </summary>
        /// <param name="ep1">Endpoint #1.</param>
        /// <param name="ep2">Endpoint #2</param>
        /// <returns>
        /// <list type="table">
        ///     <item>
        ///         <term>-1</term>
        ///         <description>If <b>ep1 &lt; ep2</b></description>
        ///     </item>
        ///     <item>
        ///         <term>0</term>
        ///         <description>If <b>ep1 == ep2</b></description>
        ///     </item>
        ///     <item>
        ///         <term>+1</term>
        ///         <description>If <b>ep1 &gt; ep2</b></description>
        ///     </item>
        /// </list>
        /// </returns>
        /// <remarks>
        /// <note>
        /// The value of the <see cref="Broadcast" /> property is
        /// <b>not ignored</b> for this comparision.
        /// </note>
        /// </remarks>
        public static int Compare(MsgEP ep1, MsgEP ep2)
        {
            return String.Compare(ep1.ToString(), ep2.ToString(), StringComparison.InvariantCultureIgnoreCase);
        }

        /// <summary>
        /// Clips the endpoint passed by limiting it to the specified number
        /// of segments and returning the result.
        /// </summary>
        /// <param name="ep">The input endpoint.</param>
        /// <param name="segmentCount">The maximum number of samples to be returned.;</param>
        /// <returns>The clipped endpoint.</returns>
        public static MsgEP CopyMaxSegments(MsgEP ep, int segmentCount)
        {
            var output = new MsgEP();

            output.isPhysical = ep.isPhysical;
            output.rootHost = ep.rootHost;
            output.rootPort = ep.rootPort;
#if WINFULL
            output.channelEP = ep.channelEP;
#endif
            output.objectID = ep.objectID;
            output.cachedUri = null;

            if (ep.segments != null)
            {
                int c = Math.Min(segmentCount, ep.segments.Length);

                output.segments = new string[c];
                Array.Copy(ep.segments, output.segments, c);
            }

            return output;
        }

        /// <summary>
        /// Used by Unit tests to reload the abstract endpoint map.
        /// </summary>
        internal static void ReloadAbstractMap()
        {
            LoadAbstractMap();
        }


        /// <summary>
        /// Static constructor that loads the abstract to logical endpoint
        /// map from the application's configuration.
        /// </summary>
        static MsgEP()
        {
            LoadAbstractMap();
        }

        /// <summary>
        /// Reloads the abstract to logical map from the application configuration.
        /// </summary>
        /// <para>
        /// This will typically be called internally by this class or in
        /// some rare cases by Unit tests.  This should never be called while
        /// an application is actively using the messaging library because 
        /// it is not thread safe.
        /// </para>
        internal static void LoadAbstractMap()
        {
            var config = new Config(MsgHelper.ConfigPrefix);

            abstractMap = config.GetDictionary("AbstractMap");

            // Verify that the logical endpoints are valid

            foreach (KeyValuePair<string, string> entry in abstractMap)
            {
                MsgEP   ep;
                bool    ok = false;

                try
                {
                    ep = MsgEP.Parse(entry.Value);
                    ok = ep.IsLogical;
                }
                catch
                {
                }

                if (!ok)
                    SysLog.LogError("Invalid logical endpoint configuration: {0}[{1}] = {2}", MsgHelper.ConfigPrefix, entry.Key, entry.Value);
            }
        }

        //---------------------------------------------------------------------
        // Instance members

        private bool        isPhysical = false;     // True for a physical endpoint, false for abstract
        private string      rootHost   = null;      // Root host name or dotted quad (or null)
        private int         rootPort   = -1;        // Root TCP port (or -1)
        private string[]    segments   = null;      // The router name hierarchy
#if WINFULL
        private ChannelEP   channelEP  = null;      // ChannelEP parameter (or null)
#endif
        private string      objectID   = null;      // Object ID parameter (or null)
        private string      cachedUri  = null;      // Cached ToString() representation (or null)
        private bool        isNull     = false;     // True if this is the logical://null endpoint
        private bool        broadcast  = false;     // True for broadcast endpoints

        /// <summary>
        /// Constructs an empty endpoint.
        /// </summary>
        public MsgEP()
        {
        }

        /// <summary>
        /// Parses a physical endpoint from the string passed.
        /// </summary>
        /// <param name="uri">The encoded endpoint string (already in lowercase).</param>
        private void ParsePhysical(string uri)
        {
            string  name, value;
            int     p, pEnd;

            try
            {
                isPhysical = true;

                // Parse the root host and port

                p = cPhysicalScheme;
                pEnd = uri.IndexOfAny(allDelimiters, p);
                if (pEnd == -1)
                {
                    rootHost = Helper.UnescapeUri(uri.Substring(p));
                    rootPort = -1;
                    return;
                }

                if (uri[pEnd] == ':')
                {
                    rootHost = Helper.UnescapeUri(uri.Substring(p, pEnd - p));
                    p = pEnd + 1;

                    pEnd = uri.IndexOfAny(segDelimiters, p);

                    if (pEnd == -1)
                        value = uri.Substring(p);
                    else
                        value = uri.Substring(p, pEnd - p);

                    try
                    {
                        rootPort = int.Parse(value);
                    }
                    catch
                    {
                        throw new MsgException("Invalid root port.");
                    }

                    if (pEnd == -1)
                        return;

                    p = pEnd;
                }
                else
                {
                    // No port number found

                    rootHost = Helper.UnescapeUri(uri.Substring(p, pEnd - p));
                    rootPort = -1;

                    p = pEnd;
                }

                if (rootHost.Length == 0)
                    rootHost = null;

                // At this point uri[p] is either a '/' or '?' character.  If it's
                // a slash, then process the segments.

                if (uri[p] == '/')
                {
                    var segs = new List<string>();

                    if (rootHost == null)
                        throw new MsgException("Root segment cannot be empty.");

                    p++;
                    while (true)
                    {
                        pEnd = uri.IndexOfAny(segDelimiters, p);
                        if (pEnd == -1)
                            value = uri.Substring(p);
                        else
                            value = uri.Substring(p, pEnd - p);

                        if (value.Length > 0 || (pEnd != -1 && uri[pEnd] == '/'))
                            segs.Add(value);

                        p = pEnd;
                        if (p == -1 || uri[p] == '?')
                            break;

                        p++;
                    }

                    segments = new string[segs.Count];
                    for (int i = 0; i < segs.Count; i++)
                        segments[i] = Helper.UnescapeUri((string)segs[i]);
                }

                if (p == -1)
                    return;

                // Process the query string

                if (uri[p] != '?')
                    throw new ArgumentException("Invalid URI: '/' or '?' expected.", "uri");

                p++;
                if (p == uri.Length)
                    return;

                while (p < uri.Length)
                {
                    pEnd = uri.IndexOfAny(equalAmp, p);
                    if (pEnd == -1)
                    {
                        name  = uri.Substring(p);
                        value = string.Empty;
                        p     = uri.Length;
                    }
                    else if (uri[pEnd] == '&')
                    {
                        name = uri.Substring(p, pEnd - p);
                        value = string.Empty;
                        p = pEnd + 1;
                    }
                    else
                    {
                        Assertion.Test(uri[pEnd] == '=');

                        name = uri.Substring(p, pEnd - p);
                        p    = pEnd + 1;

                        pEnd = uri.IndexOf('&', p);
                        if (pEnd == -1)
                        {
                            value = uri.Substring(p);
                            p     = uri.Length;
                        }
                        else
                        {
                            value = uri.Substring(p, pEnd - p);
                            p     = pEnd + 1;
                        }
                    }

                    switch (name)
                    {
                        case "o":

                            objectID = Helper.UnescapeUriParam(value);
                            break;
#if WINFULL
                        case "c":

                            channelEP = Helper.UnescapeUriParam(value);
                            break;
#endif
                        case "broadcast":

                            broadcast = true;
                            break;
                    }
                }
            }
            finally
            {
                if (segments == null)
                    segments = new string[0];
            }
        }

        /// <summary>
        /// Parses a logical endpoint from the string passed.
        /// </summary>
        /// <param name="uri">The encoded endpoint string (already in lowercase).</param>
        private void ParseLogical(string uri)
        {
            var         segs  = new List<string>();
            string      query = null;
            int         p, pEnd;
            string      value;

            try
            {
                isPhysical = false;
                cachedUri  = uri;

                p = cLogicalScheme;
                if (p == uri.Length)
                    throw new ArgumentException("Empty logical endpoint.", "uri");

                // Parse the segments

                while (true)
                {
                    pEnd = uri.IndexOfAny(segDelimiters, p);
                    if (pEnd == -1)
                        value = uri.Substring(p);
                    else
                    {
                        if (uri[pEnd] == '?')
                            query = uri.Substring(pEnd);    // We have a query string

                        value = uri.Substring(p, pEnd - p);
                    }

                    if (value.Length == 0)
                        throw new ArgumentException("Logical endpoints don't allow empty URI segments.", "uri");

                    segs.Add(value);

                    p = pEnd;
                    if (p == -1 || query != null)
                        break;

                    p++;
                }

                segments = new string[segs.Count];
                for (int i = 0; i < segs.Count; i++)
                    segments[i] = Helper.UnescapeUri((string)segs[i]);

                if (segments.Length > 0)
                    isNull = String.Compare(segments[0], "null", StringComparison.InvariantCultureIgnoreCase) == 0;

                // Verify that wildcards are used correctly

                for (int i = 0; i < segments.Length - 1; i++)
                    if (segments[i].IndexOf('*') != -1)
                        throw new ArgumentException("Logical endpoints allow wildcards only in the last URI segment.", "uri");

                string lastSeg = segments[segments.Length - 1];

                if (lastSeg.Length > 1 && lastSeg.IndexOf('*') != -1)
                    throw new ArgumentException("The wildcard character [*] may appear only by itself in the last segment of a logical endpoint.", "uri");

                // Parse the query string (if there is one)

                if (query != null)
                {
                    if (query == "?broadcast")
                        broadcast = true;
                    else
                        throw new ArgumentException(string.Format("Illegal logical endpoint query string [{0}].", query), "uri");
                }
            }
            finally
            {
                if (segments == null)
                    segments = new string[0];
            }
        }

        /// <summary>
        /// Parses an abstract endpoint from the string passed.
        /// </summary>
        /// <param name="uri">The encoded endpoint string (already in lowercase).</param>
        private void ParseAbstract(string uri)
        {
            string mapped;

            if (abstractMap.TryGetValue(uri, out mapped))
            {
                uri = mapped.ToLowerInvariant(); ;
                if (!uri.StartsWith("logical://"))
                    throw new ArgumentException(string.Format("Abstract endpoint [{0}] must map to a logical endpoint.", uri));
            }
            else
                uri = "logical://" + uri.Substring(cAbstractScheme);

            ParseLogical(uri);
        }

        /// <summary>
        /// Parses the endpoint URI.
        /// </summary>
        /// <param name="uri">The endpoint string.</param>
        private void Construct(string uri)
        {
            // Remove any terminating "/" and convert to lower case.

            if (uri.EndsWith("/"))
                uri = uri.Substring(0, uri.Length - 1);

            uri = uri.ToLowerInvariant().Trim();

            // Parse the uri scheme

            if (uri.StartsWith("physical://"))
                ParsePhysical(uri);
            else if (uri.StartsWith("logical://"))
                ParseLogical(uri);
            else if (uri.StartsWith("abstract://"))
                ParseAbstract(uri);
            else
                throw new ArgumentException("Unexpected uri scheme.", "uri");
        }

        /// <summary>
        /// Constructs an endpoint by parsing the string passed.
        /// </summary>
        /// <param name="uri">The endpoint string.</param>
        public MsgEP(string uri)
        {
            Construct(uri);
        }

        /// <summary>
        /// Constructs a string into a logical endpoint and then appends the
        /// instance string as additional segments.
        /// </summary>
        /// <param name="root">The root endpoint string.</param>
        /// <param name="instance">The instance string.</param>
        /// <remarks>
        /// <para>
        /// This method is useful for generating an endpoint to a particular
        /// service instance in a cluster.  The value string is first parsed into
        /// a logical endpoint, with any necessary abstract to logical mappings
        /// being performed.  Then the instance segment will be appended.
        /// </para>
        /// <code language="none">
        ///     Example:    Parse("logical://foo","bar")
        /// 
        ///     Returns:    logical://foo/bar
        /// </code>
        /// </remarks>
        public MsgEP(string root, string instance)
        {
            var ep = new MsgEP(root);

            if (!ep.IsLogical)
                throw new ArgumentException(NotLogicalMsg);

            if (instance.StartsWith("/"))
                Construct(ep.ToString() + instance);
            else
                Construct(ep.ToString() + "/" + instance);
        }

        /// <summary>
        /// Consutucts a new logical endpoint from the endpoint
        /// passed by appending the instance string as an additional segment.
        /// </summary>
        /// <param name="root">The root endpoint.</param>
        /// <param name="instance">The instance string.</param>
        /// <returns>
        /// Returns the composite endpoint.
        /// </returns>
        public MsgEP(MsgEP root, string instance)
            : this(root.ToString(), instance)
        {
        }

#if WINFULL
        /// <summary>
        /// Constructs an endpoint from the channel endpoint passed.
        /// </summary>
        /// <param name="channelEP">The channel endpoint.</param>
        public MsgEP(ChannelEP channelEP)
        {
            this.isPhysical = true;
            this.channelEP  = channelEP;
            this.segments   = new string[0];
        }
#endif

        /// <summary>
        /// Constructs an endpoint from the parameters passed.
        /// </summary>
        /// <param name="rootHost">The root host name, dotted-quad (or <c>null</c>).</param>
        /// <param name="rootPort">The root port (or 0).</param>
        /// <param name="segments">The router segments.</param>
        public MsgEP(string rootHost, int rootPort, params string[] segments)
        {
            if (rootHost.Trim() == string.Empty)
                throw new MsgException("Invalid root host.");

            this.isPhysical = true;
            this.rootHost   = rootHost;
            this.rootPort   = rootPort;
            this.segments   = segments;
        }

        /// <summary>
        /// Returns a shallow clone of this endpoint.
        /// </summary>
        public MsgEP Clone()
        {
            return Clone(false);
        }

        /// <summary>
        /// Returns a shallow clone of this endpoint, optionally
        /// resetting the <see cref="Broadcast" /> property.
        /// </summary>
        /// <param name="resetBroadcast">Pass <c>true</c> if the <see cref="Broadcast" /> property is to be reset.</param>
        public MsgEP Clone(bool resetBroadcast)
        {
            MsgEP clone;

            clone = new MsgEP();
            clone.isPhysical = this.isPhysical;
            clone.rootHost   = this.rootHost;
            clone.rootPort   = this.rootPort;
            clone.segments   = this.segments;
#if WINFULL
            clone.channelEP  = this.channelEP;
#endif
            clone.objectID   = this.objectID;
            clone.cachedUri  = this.cachedUri;
            clone.isNull     = this.isNull;
            clone.broadcast  = this.broadcast;

            if (resetBroadcast && broadcast)
            {
                clone.broadcast = false;
                clone.cachedUri = null;
            }

            return clone;
        }

        /// <summary>
        /// Normalizes the <see cref="MsgEP" /> by removing the <b>broadcast</b> query
        /// parameter if present.
        /// </summary>
        /// <returns>
        /// The current instance if <see cref="Broadcast" /> is <c>true</c>, a clone
        /// with <see cref="Broadcast" /> set to <c>false</c> otherwise.
        /// </returns>
        public MsgEP GetNoBroadcast()
        {
            if (!broadcast)
                return this;

            var clone = this.Clone();

            clone.Broadcast = false;
            return clone;
        }

        private class NameValue
        {
            public string Name;
            public string Value;

            public NameValue(string name, string value)
            {
                this.Name  = name;
                this.Value = value;
            }
        }

        /// <summary>
        /// Returns the endpoint encoded as a URI string.
        /// </summary>
        /// <param name="segmentCount">The number of segments to render or <b>-1</b> to render all.</param>
        /// <param name="includeQuery"><c>true</c> to include the query string.</param>
        /// <remarks>
        /// This method is valid only for physical endpoints.
        /// </remarks>
        public string ToString(int segmentCount, bool includeQuery)
        {
            var sb    = new StringBuilder();
            var query = new List<NameValue>();

            if (!isPhysical)
                throw new MsgException(NotPhysicalMsg);

            if (cachedUri != null && (segmentCount == -1 || segmentCount == segments.Length) && includeQuery)
                return cachedUri;

            sb.Append("physical://");
            if (rootHost != null)
            {
                sb.Append(Helper.EscapeUri(rootHost));
                if (rootPort != -1)
                    sb.AppendFormat(null, ":{0}", rootPort);
            }

            if (segmentCount == -1)
                segmentCount = segments.Length;

            for (int i = 0; i < segmentCount; i++)
                sb.AppendFormat(null, "/{0}", Helper.EscapeUri(segments[i]));

            if (includeQuery)
            {
                if (objectID != null)
                    query.Add(new NameValue("o", objectID));
#if WINFULL
                if (channelEP != null)
                    query.Add(new NameValue("c", channelEP));
#endif
                if (broadcast)
                    query.Add(new NameValue("broadcast", string.Empty));

                for (int i = 0; i < query.Count; i++)
                {
                    var nv = query[i];

                    if (i == 0)
                        sb.Append('?');
                    else
                        sb.Append('&');

                    if (!string.IsNullOrWhiteSpace(nv.Value))
                        sb.AppendFormat(null, "{0}={1}", Helper.EscapeUriParam(nv.Name), Helper.EscapeUriParam(nv.Value));
                    else
                        sb.AppendFormat(null, "{0}", Helper.EscapeUriParam(nv.Name));
                }
            }

            cachedUri = sb.ToString();
            return cachedUri;
        }

        /// <summary>
        /// Returns the endpoint encoded as a URI string.
        /// </summary>
        public override string ToString()
        {
            if (cachedUri != null)
                return cachedUri;

            if (isPhysical)
                return cachedUri = ToString(-1, true);
            else
            {
                var sb = new StringBuilder();

                sb.Append("logical://");
                for (int i = 0; i < segments.Length; i++)
                {
                    sb.Append(Helper.EscapeUri(segments[i]));
                    if (i < segments.Length - 1)
                        sb.Append('/');
                }

                if (broadcast)
                    sb.Append("?broadcast");

                return cachedUri = sb.ToString();
            }
        }

        /// <summary>
        /// Set to <c>true</c> if this is a physical endpoint.
        /// </summary>
        /// <remarks>
        /// Physical endpoints specify a router hierarchy and use the physical:// uri scheme.
        /// </remarks>
        public bool IsPhysical
        {
            get { return isPhysical; }

            set
            {
                if (cachedUri != null)
                    throw new MsgException(AlreadyInitMsg);

                isPhysical = value;
            }
        }

        /// <summary>
        /// Set to <c>true</c> if this is a logical endpoint.
        /// </summary>
        /// <remarks>
        /// Abstract endpoints specify an logical message endpoint and use the logical:// uri scheme.
        /// </remarks>
        public bool IsLogical
        {
            get { return !isPhysical; }

            set
            {
                if (cachedUri != null)
                    throw new MsgException(AlreadyInitMsg);

                isPhysical = !value;
            }
        }

        /// <summary>
        /// Returns <c>true</c> if this is a <b>logical://null</b> endpoint.
        /// </summary>
        public bool IsNull
        {
            get { return isNull; }
        }

        /// <summary>
        /// Returns <c>true</c> if this is a channel endpoint.
        /// </summary>
        /// <remarks>
        /// This returns <c>true</c> for physical endpoints that specify no root or sub routers
        /// and specific a channel endpoint.
        /// </remarks>
        public bool IsChannel
        {
            get
            {
                return IsPhysical && rootHost == null && segments.Length == 0
#if WINFULL
 && channelEP != null
#endif
;
            }
        }

        /// <summary>
        /// Indicates a broadcast endpoint.
        /// </summary>
        /// <remarks>
        /// <para>
        /// Broadcast endpoints are specified in the URI using the <b>broadcast</b> query
        /// parameter, as in:
        /// </para>
        /// <code language="none">
        /// logical://MyNamespace/MyService/*?broadcast
        /// </code>
        /// </remarks>
        public bool Broadcast
        {
            get { return broadcast; }

            set
            {
                if (broadcast == value)
                    return;

                cachedUri = null;
                broadcast = value;
            }
        }

        /// <summary>
        /// Returns <c>true</c> if this is a physical endpoint with a detached root.
        /// </summary>
        public bool IsDetachedRoot
        {
            get { return IsPhysical && String.Compare(rootHost, DetachedRoot, StringComparison.InvariantCultureIgnoreCase) == 0; }
        }

        /// <summary>
        /// The root host name.
        /// </summary>
        public string RootHost
        {
            get { return rootHost; }

            set
            {
                if (value == null || value.Trim() == string.Empty)
                    throw new MsgException("Invalid root host.");

                if (cachedUri != null)
                    throw new MsgException(AlreadyInitMsg);

                rootHost = value;
            }
        }

        /// <summary>
        /// The root port (or <b>-1</b>).
        /// </summary>
        public int RootPort
        {
            get { return rootPort; }

            set
            {
                if (cachedUri != null)
                    throw new MsgException(AlreadyInitMsg);

                rootPort = value;
            }
        }

        /// <summary>
        /// Returns <c>true</c> if the endpoint represents a physical root.
        /// </summary>
        /// <remarks>
        /// <note>
        /// Physical endpoints that do not specify a host name
        /// (e.g. "physical://?c=tcp://127.0.0.1:55" are not considered to 
        /// be root endpoints.
        /// </note>
        /// </remarks>
        public bool IsPhysicalRoot
        {
            get { return isPhysical && rootHost != null && segments.Length == 0; }
        }

        /// <summary>
        /// Returns <c>true</c> if the endpoint passed is a descendant of this
        /// endpoint in the physical hierarchy.
        /// </summary>
        /// <param name="ep">The endpoint to test.</param>
        /// <returns><c>true</c> if the endpoint is a descendant of this endpoint.</returns>
        /// <remarks>
        /// This method is supported only for physical endpoints.
        /// </remarks>
        public bool IsPhysicalDescendant(MsgEP ep)
        {
            if (!ep.IsPhysical || !this.IsPhysical)
                throw new MsgException(NotPhysicalMsg);

            if (ep.segments.Length <= this.segments.Length)
                return false;

            if (Helper.Normalize(ep.rootHost) != Helper.Normalize(this.rootHost) || ep.RootPort != this.rootPort)
                return false;

            for (int i = 0; i < this.segments.Length; i++)
                if (ep.segments[i] != this.segments[i])
                    return false;

            return true;
        }

        /// <summary>
        /// Returns <c>true</c> if the endpoint passed is a peer to this endpoint
        /// in the physical hierarchy.
        /// </summary>
        /// <param name="ep">The endpoint to test.</param>
        /// <returns><c>true</c> if the endpoint passed and this endpoint are physical peers.</returns>
        /// <remarks>
        /// This method is supported only for physical endpoints.
        /// </remarks>
        public bool IsPhysicalPeer(MsgEP ep)
        {
            if (!ep.IsPhysical || !this.IsPhysical)
                throw new MsgException(NotPhysicalMsg);

            if (ep.segments.Length != this.segments.Length)
                return false;

            if (Helper.Normalize(ep.rootHost) != Helper.Normalize(this.rootHost) || ep.RootPort != this.rootPort)
                return false;

            for (int i = 0; i < this.segments.Length - 1; i++)
                if (ep.segments[i] != this.segments[i])
                    return false;

            return true;
        }

        /// <summary>
        /// Returns <c>true</c> if the endpoints passed are both physical and
        /// that they match up to, but not including the query string.
        /// </summary>
        /// <param name="ep">The endpoint to test.</param>
        /// <returns><c>true</c> if the endpoints match.</returns>
        /// <remarks>
        /// <note>
        /// This method is supported only for physical endpoints.
        /// </note>
        /// <note>
        /// The value of the <see cref="Broadcast" /> property is
        /// ignored for this comparision.
        /// </note>
        /// </remarks>
        public bool IsPhysicalMatch(MsgEP ep)
        {
            if (!ep.IsPhysical || !this.IsPhysical)
                throw new MsgException(NotPhysicalMsg);

            if (ep.segments.Length != this.segments.Length)
                return false;

            if (Helper.Normalize(ep.rootHost) != Helper.Normalize(this.rootHost) || ep.RootPort != this.rootPort)
                return false;

            for (int i = 0; i < this.segments.Length; i++)
                if (ep.segments[i] != this.segments[i])
                    return false;

            return true;
        }

        /// <summary>
        /// The array of router names encoded in a physical uri.
        /// </summary>
        /// <remarks>
        /// This works somewhat differently than how the .NET System.Uri class works.
        /// These segments will have the forward slash (/) characters removed which
        /// means that Segment[0] will be the name of the first router after the 
        /// root host name and port.
        /// 
        /// So for the endpoint: "physical://root:80/router0/router1" Segments[0] will be "router0"
        /// and Segments[1] will be "router1".  The endpoint "physical://root:80 will have a zero
        /// length Segments array.
        /// </remarks>
        public string[] Segments
        {
            get { return segments; }

            set
            {
                if (cachedUri != null)
                    throw new MsgException(AlreadyInitMsg);

                segments = value;
            }
        }

        /// <summary>
        /// The object ID (or <c>null</c>).
        /// </summary>
        public string ObjectID
        {
            get { return objectID; }

            set
            {
                cachedUri = null;
                objectID = value;
            }
        }

#if WINFULL
        /// <summary>
        /// The low level channel endpoint (or <c>null</c>).
        /// </summary>
        public ChannelEP ChannelEP
        {
            get { return channelEP; }

            set
            {
                cachedUri = null;
                channelEP = value;
            }
        }
#endif

        /// <summary>
        /// Returns <c>true</c> if the logical endpoint has a wildcard
        /// in its last URI segment.
        /// </summary>
        public bool HasWildCard
        {
            get
            {
                if (isPhysical)
                    throw new MsgException(NotLogicalMsg);

                return segments[segments.Length - 1] == "*";
            }
        }

        /// <summary>
        /// Returns the endpoint corresponding to the physical parent of this
        /// endpoint.  Returns null if this is already a root.
        /// </summary>
        /// <remarks>
        /// This method is supported only for physical, non-channel endpoints.
        /// </remarks>
        public MsgEP GetPhysicalParent()
        {
            MsgEP ep;

            if (!this.IsPhysical || this.IsChannel)
                throw new MsgException("Endpoint must be a physical, non-channel endpoint.");

            if (this.rootHost == null || this.segments.Length == 0)
                return null;

            ep            = new MsgEP();
            ep.isPhysical = true;
            ep.RootHost   = this.RootHost;
            ep.RootPort   = this.RootPort;
#if WINFULL
            ep.channelEP  = null;
#endif
            ep.objectID   = null;

            ep.segments = new string[segments.Length - 1];
            for (int i = 0; i < segments.Length - 1; i++)
                ep.segments[i] = segments[i];

            return ep;
        }

        /// <summary>
        /// Returns <c>true</c> if the logical endpoints match.
        /// </summary>
        /// <param name="ep">The endpoint to compare against this instance.</param>
        /// <returns><c>true</c> if the endpoints match.</returns>
        /// <remarks>
        /// <para>
        /// Two logical endpoints match if the endpoint's URI
        /// segments are the same, taking wildcards into account.
        /// Here are some examples of matching logical endpoints:
        /// </para>
        /// <code language="none">
        /// logical://*         matches: logical://foo
        ///                              logical://foo/bar
        ///                              logical://foo/bar/*
        /// 
        /// logical://foo/bar   matches: logical://foo/bar
        /// 
        /// logical://foo/*     matches: logical://foo
        ///                              logical://foo/bar
        ///                              logical://foo/foobar
        ///                              logical://foo/bar/foobar
        /// </code>
        /// <note>
        /// The value of the <see cref="Broadcast" /> property is
        /// ignored for this comparision.
        /// </note>
        /// </remarks>
        public bool LogicalMatch(MsgEP ep)
        {
            if (this.isPhysical && ep.isPhysical)
                throw new MsgException(NotLogicalMsg);

            if (!this.HasWildCard && !ep.HasWildCard)
            {
                if (this.segments.Length != ep.segments.Length)
                    return false;

                for (int i = 0; i < this.segments.Length; i++)
                    if (this.segments[i] != ep.segments[i])
                        return false;

                return true;
            }

            MsgEP   minEP;      // Will hold the ep with the fewest segments
            MsgEP   otherEP;    // Will hold the other ep

            if (this.segments.Length < ep.segments.Length)
            {
                minEP   = this;
                otherEP = ep;
            }
            else if (segments.Length == ep.segments.Length)
            {
                if (ep.HasWildCard)
                {

                    minEP   = ep;
                    otherEP = this;
                }
                else
                {
                    minEP   = this;
                    otherEP = ep;
                }
            }
            else
            {
                minEP   = ep;
                otherEP = this;
            }

            if (minEP.HasWildCard)
            {
                for (int i = 0; i < minEP.segments.Length - 1; i++)
                    if (minEP.segments[i] != otherEP.segments[i])
                        return false;

                return true;
            }
            else
            {
                if (otherEP.HasWildCard)
                {
                    if (otherEP.segments.Length > minEP.segments.Length + 1)
                        return false;
                }
                else
                {
                    if (otherEP.segments.Length > minEP.segments.Length)
                        return false;
                }

                for (int i = 0; i < minEP.segments.Length; i++)
                    if (minEP.segments[i] != otherEP.segments[i])
                        return false;

                return true;
            }
        }

        /// <summary>
        /// Tests this instance to the object passed for equality.
        /// </summary>
        /// <param name="obj">The object being compared.</param>
        /// <returns><c>true</c> if the objects represent the same value.</returns>
        /// <remarks>
        /// <note>
        /// The value of the <see cref="Broadcast" /> property is
        /// ignored for this comparision.
        /// </note>
        /// </remarks>
        public override bool Equals(object obj)
        {
            MsgEP       ep;
            string      cep1, cep2;

            ep = obj as MsgEP;
            if (ep == null)
                return false;

            if (ep == this)
                return true;

            if (this.isPhysical != ep.isPhysical)
                return false;

            if (this.isPhysical)
            {
#if WINFULL
                if (this.channelEP == null)
                    cep1 = string.Empty;
                else
                    cep1 = this.channelEP.ToString();

                if (ep.channelEP == null)
                    cep2 = string.Empty;
                else
                    cep2 = ep.channelEP.ToString();
#else
                cep1 = cep2 = string.Empty;
#endif

                if (this.rootHost != ep.rootHost ||
                    this.rootPort != ep.rootPort ||
                    this.objectID != ep.objectID ||
                    cep1 != cep2)

                    return false;
            }

            if (this.segments.Length != ep.segments.Length)
                return false;

            for (int i = 0; i < this.segments.Length; i++)
                if (this.segments[i] != ep.segments[i])
                    return false;

            return true;
        }

        /// <summary>
        /// Returns the hash code for this instance.
        /// </summary>
        /// <returns>The computed integer hash code.</returns>
        public override int GetHashCode()
        {
            if (cachedUri != null)
                return cachedUri.GetHashCode();
            else
                return this.ToString().GetHashCode();
        }
    }
}
