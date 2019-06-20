//-----------------------------------------------------------------------------
// FILE:        NamespaceDoc.cs
// CONTRIBUTOR: Jeff Lill
// COPYRIGHT:   Copyright (c) 2005-2015 by Jeffrey Lill.  All rights reserved.
// DESCRIPTION: Namespace documentation

using System;
using System.Transactions;

using LillTek.Common;
using LillTek.Messaging.Internal;
using LillTek.Net.Broadcast;

namespace LillTek.Messaging
{
    /// <summary>
    /// Implements the LillTek network messaging stack.
    /// </summary>
    /// <remarks>
    /// <para><b><u>Overview</u></b></para>
    /// <para>
    ///     The LillTek.Messaging namespace provides an implementation of a high
    ///     performance, low footprint interprocess messaging layer.  This layer
    ///     facilitates the delivery and dispatching of messages via derivitives
    ///     of the base <see cref="MsgRouter"/> class.
    /// </para>
    /// <para>
    ///     All messages must derive from <see cref="Msg"/>.  This class
    ///     defines the message envelope properties as well as handles the serialization
    ///     and instantiation of messages.  Custom application messages must be derived
    ///     from <see cref="Msg"/> and message types located within application
    ///     assemblies must be registered by passing the assembly to <see cref="Msg.LoadTypes"/>
    /// </para>
    /// <para>
    ///     LillTek Messaging manages the delivery of message between message endpoints.
    ///     Two kinds of message endpoints are supported: physical and logical.
    ///     These endpoints can be represented by URIs and are implemented by the
    ///     <see cref="MsgEP"/> class.
    /// </para>
    /// <para>
    ///     Physical endpoints specify a specific <see cref="MsgRouter"/> instance
    ///     in a physical hierarchy of routers. This hierarchy consists of three levels:
    /// </para>
    /// <list>
    ///     <item>
    ///         The root hierarchy level represented by physical endpoints of the form
    ///         <b>physical://host:port</b>.  The root routers are
    ///          used to bridge messaging across multiple subnets.
    ///     </item>
    ///     <item>
    ///         The hub hierarchy level is represented by physical endpoints of the
    ///         form <b>physical://host:port/hubname</b>.  Hub routers are used to
    ///         route messages between leaf routers on the subnet (if peer-to-peer
    ///         routing is disabled) and also to route messages between the subnet
    ///         and root level routers.
    ///     </item>
    ///     <item>
    ///         The leaf hierarchy level is presented by physical endpoints of the
    ///         form <b>physical://host:port/hubname/leafname</b>.  Leaf routers
    ///         are typically embedded within the network applications and are
    ///         used to route messages between the application and other routers
    ///         on the network.
    ///     </item>
    /// </list>
    /// <para>
    ///     Although physical endpoints may be used by applications using this library,
    ///     their use will be typically be restricted to the internal implementation
    ///     of the library.  Applications will generally be written using logical
    ///     or abstract addressing, as described below.
    /// </para>
    /// <para>
    ///     The LillTek Messaging library also supports the concept of logical message
    ///     endpoints.  These endpoints specify message destinations that do not encode
    ///     any information about the network topology.  The messaging library handles
    ///     the discovery of logical endpoints on the network and then the routing
    ///     of messages to these endpoints.  The basic design goal for the library
    ///     was to automatically handle many of the difficult implementation requirements
    ///     of a complex scaleout application.  To this end, the library handles the
    ///     automatic discovery of servers and applications, load balancing, failover,
    ///     and message broadcasting.  The concept used to make this work is that
    ///     of logical message addresssing.
    /// </para>
    /// <para>
    ///     Note that physical endpoint URIs are case insensitive and in fact are
    ///     normalized to lower case automatically when they are instantiated.
    /// </para>
    /// <b><u>Logical and Abstract Message Addressing</u></b>
    /// <para>
    ///     Logical message endpoints are simply arbitrary URIs of the form: <b>logical://segment0/segment1...</b>
    ///     consisting of one or more URI segments with each segment consisting of
    ///     one or more valid URI characters.  The only exception to this is that the
    ///     last segment may specify a wildcarded logical URI via a single asterick [*]
    ///     character (more on wildcards below).  Logical endpoint URIs are case insensitive.
    /// </para>
    /// <para>
    ///     Abstract message endpoints are used to implement configurable logical endpoints.
    ///     These endpoints have the form <b>abstract://&lt;arbitrary text&gt;</b> and are
    ///     converted immediately into logical endpoints by the messaging library as
    ///     described further below.
    /// </para>
    /// <para>
    ///     Applications consume messages by associating a logical endpoint with an
    ///     application method accepting a single parameter derived from <see cref="Msg"/>.
    ///     The logical endpoint specified by the application completely abitrary.
    ///     As an example, let's assume that we've built an application called Speaker
    ///     that is designed to play various sounds when a message is received by the
    ///     application.  Let's assume that the application is capable of playing
    ///     three sounds: a phone ringing, a bell gonging, and a simple beep.
    /// </para>
    /// <para>
    ///     One way to implement this application would be to create three different
    ///     methods to handle the sound generation and to associate each method with
    ///     a different logical endpoint.  The three endpoints for this example are:
    /// </para>
    /// <code language="none">
    ///     logical://Speaker/Ring
    ///     logical://Speaker/Gong
    ///     logical://Speaker/Beep
    /// </code>
    /// <para>
    ///     Once the application has started, the various sounds can be played simply
    ///     by sending a message to one of the logical endpoints.
    /// </para>
    /// <code language="none">
    ///     SendTo("logical://Speaker/Ring"),msg);
    ///     SendTo("logical://Speaker/Gong"),msg);
    ///     SendTo("logical://Speaker/Beep"),msg);
    /// </code>
    /// <para>
    ///     The interesting thing is that it could be the same application process sending the
    ///     message, a different process on the same machine, or a process running on another
    ///     machine entirely.  The messaging layer handles all of the details of discovering
    ///     the available message endpoints on the network and then routing the message to
    ///     the correct destination.
    /// </para>
    /// <para>
    ///     It is entirely possible and in fact very interesting to have multiple application
    ///     instances advertise the same message endpoint.  Let's say we've deployed two
    ///     instances of our Speaker application on individual servers on the network.  Both
    ///     instances will advertise the three logical endpoints listed above.
    /// </para>
    /// <para>
    ///     The library handles Send() operations to a muliply defined logical endpoints by
    ///     randomly selecting one of the available endpoints and sending the message there.
    ///     This is essentially a form of load balancing with the messages being distributed
    ///     evenly over the set of available application instances.
    /// </para>
    /// <para>
    ///     I hinted above at the concept of wildcarded message endpoints.  These are specified
    ///     by placing a single asterick in the last segment of the endpoint URI.  Here are
    ///     some examples of wildcarded endpoints:
    /// </para>
    /// <code language="none">
    ///     "logical://Speaker/*"
    ///     "logical://*"
    ///     "logical://Foo/Bar/*"
    /// </code>
    /// <para>
    ///     The basic idea is that wildcarded endpoints specify a set of possible endpoints
    ///     rather than a specific endpoint.  A wildcarded endpoint is said to match another
    ///     endpoint if the segments are identical up to the wildcard.  Here are examples of
    ///     matching wildcarded endpoints:
    /// </para>
    /// <code language="none">
    ///     "logical://Speaker/*"     and "logical://Speaker/Beep"
    ///     "logical://Speaker/Ring"
    ///     "logical://Speaker/Gong"
    ///     "logical://Speaker/Control/*"
    ///     "logical://Speaker"
    ///
    ///     "logical://*"             and "logical://Speaker/Beep"
    ///     "logical://Speaker/Ring"
    ///     "logical://Speaker/Gong"
    ///     "logical://Foo/Bar"
    /// </code>
    /// <para>
    ///     The idea behind wildcarded endpoints is that they make it possible to specify
    ///     that a message be sent to a set of logical endpoints or that an application
    ///     message sink receive messages targeted at a set of endpoints.
    /// </para>
    /// <para>
    ///     Here's an example: say that we have a single instance of the Speaker application
    ///     described above running.  Sending a message to logical://Speaker/* would instruct
    ///     the messaging library to randomly select one of the three end points and send the
    ///     message there, randomly causing a Beep, Ring, or Gong to play.
    /// </para>
    /// <para>
    ///     The library also implements message broadcasting.  Message broadcasting works
    ///     a bit differently from message sending.  Whereas Send() selects one of the possible
    ///     endpoints and delivers the message there, Broadcast() attempts to deliver the
    ///     message to all matching endpoints.  For a single instance of the Speaker application,
    ///     Broadcast("logical://Speaker/*") will deliver the message to all three of the
    ///     endpoints resulting in the application playing all three sounds simultiniously.
    /// </para>
    /// <para>
    ///     <b><u>Abstract Message Endpoints</u></b>
    /// </para>
    /// <para>
    ///     As I mentioned somewhat cryptically above, abstract endpoints are a form of
    ///     logical endpoint.  Their purpose is to provide a way to add configurability to
    ///     the endpoints an application exposes or consumes without having to edit code.
    /// </para>
    /// <para>
    ///     Abstract endpoints are converted into logical endpoints at the time of their
    ///     instantiation via a configuration lookup mechanism.  The <see cref="MsgEP" /> class does this
    ///     loading the configuration dictionary via Config.GetDictionary("MsgRouter.AbstractMap")
    ///     and then using the string representation of the abstract endpoint (converted to
    ///     lower case) to lookup the corresponding logical endpoint.  If this lookup fails
    ///     then the <see cref="MsgEP" /> class will simply change the input scheme from
    ///     "abstract" to "logical" and attempt to parse the URI as a logical endpoint.
    /// </para>
    /// <para>
    ///     Let's work through some examples.  Assume that the application configuration file
    ///     contains the following settings:
    /// </para>
    /// <code language="none">
    ///     #set myroot JeffApps
    ///
    ///     MsgRouter.AbstractMap[abstract://foo]          = logical://foobar
    ///     MsgRouter.AbstractMap[abstract://Chat/Send]    = logical://$(myroot)/Chat/Send
    ///     MsgRouter.AbstractMap[abstract://Chat/Receive] = logical://$(myroot)/Chat/Receive
    /// </code>
    /// <para>
    ///     Based on these settings, the messaging library will convert the following
    ///     abstract endpoints into logical endpoints as shown.
    /// </para>
    /// <code language="none">
    ///     #1:	abstract://foo          --> logical://foobar
    ///     #2:	abstract://Chat/Send    --> logical://JeffApps/Chat/Send
    ///     #3:	abstract://Chat/Receive --> logical://JeffApps/Chat/Receive
    ///     #4:	abstract://foo/bar      --> logical://foo/bar
    /// </code>
    /// <para>
    ///     Example #1 worked by mapping abstract://foo to logical://foobar in the AbstractMap
    ///     dictionary.  Examples #2 and #3 worked much the same except that the configuration
    ///     macro $(myroot) was expanded in the logical endpoint before replacing it.
    /// </para>
    /// <para>
    ///     The abstract endpoint in example #4 didn't map to an entry in the AbstractMap
    ///     dictionary so its scheme was changed to logical and the endpoint was then
    ///     parsed from that.
    /// </para>
    /// <para>
    ///     The cool thing about this implementation is that it is possible to code and
    ///     test an	application or a class library entirely using abstract endpoints (as long
    ///     as they can be parsed as logical endpoints).  Then at deployment time, these
    ///     the application can be reconfigured to use a different set of endpoints, all
    ///     without recoding.
    /// </para>
    /// <para>
    ///     <b><u>Subnet Application Clusters and Automatic Router Discovery</u></b>
    /// </para>
    /// <para>
    ///     The LillTek Messaging library is designed to automatically handle the discovery
    ///     of the leaf and hub routers and applications available on the subnet below the
    ///     root level.  The idea is that each hub and the set of leaf routers beneath it
    ///     will be located on a separate subnet segment, perhaps behind a NAT or firewall
    ///     router.  These routers essentially form an application cluster.
    /// </para>
    /// <para>
    ///     Automatic discovery of the routers within a subnet cluster is an important
    ///     feature for easily scalable and maintainable data centers.  Router discovery is
    ///     implemented via UDP multicasting or the <see cref="UdpBroadcastClient"/>.  Note 
    ///     that multiple distinct application clusters can exist on the same subnet by 
    ///     specifying different multicast group addresses in the application configuration 
    ///     files.
    /// </para>
    /// <para>
    ///     Before I get into an overview of the multicast discovery protocol, I need
    ///     mention one thing: leaf routers may be enabled to operate in peer-to-peer
    ///     mode or not (with the default being peer-to-peer mode).  When operating in
    ///     peer-to-peer mode, leaf routers will establish connections directly to
    ///     other leaf routers within the subnet to deliver messages directly.  If
    ///     peer-to-peer routing is disabled, then leaf routers will route messages
    ///     to the hub which will then forward them onto the destination router.
    /// </para>
    /// <para>
    ///     Peer-to-peer routing has a few important advantages.  First, it is
    ///     not strictly necessary to even have a hub router present in the application
    ///     cluster for message routing to function well.  This can be very nice from
    ///     a development, test, and even deployment standpoint.  Second, performance
    ///     will be better since messages will pass directly from one leaf router to
    ///     another, without the overhead (and potential bottleneck) of going through
    ///     the hub.  Finally, peer-to-peer routing is more robust since there is
    ///     currently no support in the LillTek Messaging library for clustered
    ///     hub routers within a subnet.
    /// </para>
    /// <para>
    ///     The main advantage for non-peer-to-peer routing is the reduced memory
    ///     overhead of leaf routers having to maintain open socket connections
    ///     to a potentially large number of peers.  This can be especially
    ///     problematic on embedded devices running on something like Windows/CE.
    ///     Disabling peer-to-peer routing will result in each leaf maintaining
    ///     a single socket connection to the hub which will presumably be running
    ///     on a computer with enough resources to handle this.
    /// </para>
    /// <para>
    ///     Note that it is possible to run a mixed configuration of peer-to-peer
    ///     and non-peer-to-peer routers on the same subnet.  LillTek Messaging
    ///     is smart enough to adjust to this situation and do the right thing.
    /// </para>
    /// <para>
    ///     The multicast discovery protocol is pretty straight forward:
    /// </para>
    /// <list type="number">
    ///     <item>
    ///         Whenever a router starts, it immediately (and then periodically thereafter)
    ///         multicasts a <see cref="RouterAdvertiseMsg" />.  This message includes the
    ///         router's message endpoint, as well as its listening TCP and UDP ports and an
    ///         indication as to whether the router is peer-to-peer enabled.
    ///         The IP address of the router is determined by looking at the
    ///         <see cref="MsgEP.ChannelEP" /> of the message's <see cref="Msg._FromEP" />.
    ///         Finally, the message includes a GUID specifying the current version of the
    ///         router's logical endpoint set.
    ///     </item>
    ///     <item>
    ///         If a hub router is running, it will add the leaf router's
    ///         endpoint and port information from the <see cref="RouterAdvertiseMsg" /> to
    ///         its physical routing table and then respond by sending a
    ///         <see cref="LeafSettingsMsg" /> back to the leaf.  This message
    ///         identifies the hub router to the leaf and also includes settings
    ///         the leaf router should use.  The main setting is the interval at
    ///         which the leaf router should continue multicasting
    ///         <see cref="RouterAdvertiseMsg" /> messages.
    ///     </item>
    ///     <item>
    ///         Leaf routers with peer-to-peer routing enabled also process
    ///         <see cref="RouterAdvertiseMsg" /> messages.  The receiving router
    ///         adds the physical route to its physical routing table if both the
    ///         receiving and sending routers are peer-to-peer enabled.
    ///     </item>
    ///     <item>
    ///         If a hub router is not running, the leaf router will continue
    ///         multicasting <see cref="RouterAdvertiseMsg" /> messages at the
    ///         default interval of 1 minute, in the expectation that a hub will
    ///         eventually boot up and discover the leaf.
    ///     </item>
    ///     <item>
    ///         Just before a hub or leaf router shuts down normally, it will multicast
    ///         a <see cref="RouterStopMsg" />, notifying the hub router (and all other interested
    ///         stations) that the router is going offline.
    ///     </item>
    ///     <item>
    ///         Hub and peer-to-peer enabled leaf routers will continuously monitor
    ///         for <see cref="RouterAdvertiseMsg" /> and <see cref="RouterStopMsg" />
    ///         messages, updating its routing table as necessary.  The hub will also
    ///         purge routes for leaf routers that have not sent a recent
    ///         <see cref="RouterAdvertiseMsg" />.  Leaf routers should honor
    ///         the update interval it received in the <see cref="LeafSettingsMsg" />
    ///         it received from the hub to prevent this from happening inadvertently.
    ///     </item>
    ///     <item>
    ///         Leaf routers should montitor continuously for <see cref="LeafSettingsMsg" />
    ///         and update their settings as necessary.
    ///     </item>
    ///     <item>
    ///         When hub or peer-to-peer enabled leaf routers see a <see cref="RouterAdvertiseMsg" />
    ///         from a router that hasn't been seen before or if the logical route
    ///         collection GUID has changed, then the receiving router will send a
    ///         <see cref="RouterAdvertiseMsg" /> or <see cref="LeafSettingsMsg" /> with its
    ///         DiscoverLogcal property set to true back the sending router, requesting the
    ///         set of logical endpoints implemented from the sending router.  The sending
    ///         router responds by sending zero or more <see cref="LogicalAdvertiseMsg" /> messages
    ///         listing the router's logical routes.
    ///     </item>
    ///     <item>
    ///         Hub routers will monitor for changes to their network IP
    ///         addresses and will multicast a <see cref="LeafSettingsMsg" /> to all leaf
    ///         routers whenever such a change is detected.
    ///     </item>
    ///     <item>
    ///         Leaf routers will monitor for changes to their network IP
    ///         addresses and will multicast a <see cref="RouterStopMsg" /> and
    ///         then a <see cref="RouterAdvertiseMsg" /> all routers whenever such
    ///         a change is detected.
    ///     </item>
    /// </list>
    /// <para><b><u>Physical Message Routing</u></b></para>
    /// <para>
    ///     Although hub and leaf routers open multicast-UDP, UDP, and TCP socket ports,
    ///     most actual application message routing occurs only on TCP sockets.  The
    ///     multicast-UDP and UDP ports are used primarily for discovery purposes
    ///     (and the occasional application that simply must perform an actual UDP
    ///     message multicast).
    /// </para>
    /// <para>
    ///     As part of the discovery process, the TCP and UDP port numbers that each
    ///     socket is listening on will be broadcast to the other routers on the subnet.
    ///     When a message needs to be delivered from one router to another, the sending
    ///     router will establish a connection to the destination router's listening
    ///     TCP socket, and the message will be transmitted.  If a socket connection
    ///     already exists between the two routers then it will be reused.  Note
    ///     that the socket connection between each router operates in full asynchronous
    ///     duplex mode with message traffic in each direction being potentially
    ///     entirely unrelated.  Message routers will take care to queue outbound messages
    ///     if another message is in the process of being transmitted.
    /// </para>
    /// <para>
    ///     Router to router socket connections are established only when messages
    ///     actually need to be delivered between routers.  Each router tracks the
    ///     last time a message was actually delivered on these connection and if
    ///     a connection remains idle long enough, the router will close it.
    /// </para>
    /// <para>
    ///     As I mentioned above, routing within a subnet will be structured as either
    ///     a hub and spoke arrangement (if P2P is disabled) or a mesh network if
    ///     P2P is enabled (or a combination of both).
    /// </para>
    /// <para>
    ///     So far, we haven't discussed physical routing between the root router and the
    ///     hub routers beneath it in the hierarchy.  The simplest case is when there's
    ///     no root router at all.  In this case, the hub (and the subnet application cluster)
    ///     are said to be operating in <b>detached</b> mode and no routing up the hierarchy
    ///     will be attempted (this mode is specified by setting MsgRouter.ParentEP=DETACHED
    ///     or MsgRouter.RouterEP=physical://DETACHED/... in the hub router application's
    ///     configuration file ).
    /// </para>
    /// <para>
    ///     Non-detached hub routers will attempt to establish an uplink TCP connection to
    ///     the root router at startup, periodically reattempting the connection upon
    ///     a failure.  If the hub router applications MsgRouter.ParentEP contains a
    ///     valid socket endpoint, then this will be used for establishing the uplink.
    ///     If MsgRouter.ParentEP is not present in the configuration file, then
    ///     the root host name and port specified in the hub's physical endpoint will
    ///     be used for the uplink.  Since the uplink connection is established from
    ///     within the subnet, most issues with NAT and firewalls will be avoided.
    /// </para>
    /// <para>
    ///     Messages originating at leaf router that are targeted to a physical endpoint
    ///     outside of the subnet will be first routed to the hub and then through the
    ///     uplink to the root router which will pick up and handle delivery from there,
    ///     with the root routing the message for delivery itself, or down to one of
    ///     the hub routers for delivery within a subnet.
    /// </para>
    /// <para>
    ///     Physically routed message handler methods are specified by tagging a method
    ///     with the [<see cref="MsgHandlerAttribute">MsgHandler</see>] attribute.
    ///     This method must return void and accept a single parameter derived from the
    ///     <see cref="Msg"/> class.  The actual registration of handlers and dispatching
    ///     of messages to these handlers is implemented by the <see cref="MsgDispatcher"/>
    ///     class.
    /// </para>
    /// <para>
    ///     Message handlers defined in classes derived from <see cref="RootRouter"/>
    ///     <see cref="HubRouter"/>, and <see cref="LeafRouter"/>
    ///     classes will be automatically registered when the router is started.  Message
    ///     handlers defined in other object instances may be registered explicitly
    ///     by calling <see cref="MsgDispatcher.AddTarget(object)" />.
    /// </para>
    /// <code language="cs">
    ///     [MsgHandler]
    ///     public void MyHandler(MyMsgClass msg) {
    ///
    ///     }
    /// </code>
    /// <para><b><u>Logical Message Routing</u></b></para>
    /// <para>
    ///     Most if not all application messaging will be done via logical message
    ///     routing.  The primary advantage of logical routing is that the complexity
    ///     of sophisticated and scaleable application networking is handled almost
    ///     entirely by the messaging library.
    /// </para>
    /// <para>
    ///     Logical message handlers are specified much like physcial handlers.
    ///     The main difference is the specification of <see cref="MsgHandlerAttribute.LogicalEP" />
    ///     in the <c>[<see cref="MsgHandlerAttribute">MsgHandler</see>]</c> attribute as in:
    /// </para>
    /// <code language="cs">
    ///     [MsgHandler(LogicalEP="logical://MyApplication/MyHandler"]
    ///     public void MyHandler(MyMsgClass msg) {
    ///
    ///     }
    /// </code>
    /// <para>
    ///     Logical message handler registration and message dispatching is also handled
    ///     by the <see cref="MsgDispatcher"/> class.  Message handlers defined in classes
    ///     derived from <see cref="RootRouter"/>, <see cref="HubRouter"/>, and <see cref="LeafRouter"/>
    ///     classes will be automatically registered when the router is started.  Message
    ///     handlers defined in other object instances may be registered explicitly
    ///     by calling <see cref="MsgDispatcher.AddTarget(object)" />.
    /// </para>
    /// <para>
    ///     Logical message routing is pretty simple on the surface, although there
    ///     is some complexity underneath.  Whenever a router receives a message
    ///     targeted at a logical endpoint from another router or from the application
    ///     via a call to <see cref="MsgRouter.Send">MsgRouter.Send()</see> or
    ///     <see cref="MsgRouter.Send">MsgRouter.SendTo()</see>, the router will first
    ///     see if there are any local handlers defined for the logical endpoint and
    ///     message type.  The message is dispached immediately to a handler if one
    ///     is found.  Otherwise, the router's logical routing table will be queried
    ///     and if a matching route exists, the message will be forwarded to the
    ///     router specified by associated physical route (unless the router is a
    ///     <see cref="LeafRouter" /> with peer-to-peer routing disabled in which case
    ///     the message will be forwarded to the hub for delivery).
    /// </para>
    /// <para>
    ///     Broadcast messages are handled a bit differently. Whenever a router receives a
    ///     broadcast message targeted at a logical endpoint from another router or from the
    ///     application via a call to <see cref="MsgRouter.Broadcast" /> or
    ///     <see cref="MsgRouter.BroadcastTo(LillTek.Messaging.MsgEP, LillTek.Messaging.Msg)" />,
    ///     the router will first see if there are any local handlers defined for the logical
    ///     endpoint and message type.  The message is dispached immediately to a handler if one
    ///     is found.  Then the router's logical routing table will be queried
    ///     and if a matching routes exists, the message will be forwarded to the
    ///     routers specified by associated physical route (unless the router is a
    ///     <see cref="LeafRouter" /> with peer-to-peer routing disabled in which case
    ///     the message will be forwarded to the hub for delivery).  Note that it is
    ///     possible (although unlikely) for a broadcast message to be duplicated during
    ///     routing and be dispatched to a message handler more than once.
    /// </para>
    /// <para>
    ///     Logical endpoints are advertised by hub and leaf routers via periodic
    ///     broadcasts of <see cref="LogicalAdvertiseMsg" />.  These messages specify the
    ///     physical route to be associated with a set of logical routes.  Hub routers and
    ///     leaf routers operating in peer-to-peer mode will process these message
    ///     by adding the appropriate logical routes to their routing tables.
    /// </para>
    /// <para>
    ///     Logical routing between root and hub routers is currently statically
    ///     configured via configuration file entries.  Hub routers are configured
    ///     with a set of <b>downlink endpoints</b>.  This specifies the set of logical
    ///     endpoints to be passed to the root router, indicating that messages
    ///     targeted at these endpoints should be forwarded by the root to the
    ///     hub.
    /// </para>
    /// <para>
    ///     Root routers are configured with a set of <b>uplink endpoints</b>.  This
    ///     specifies the set of logical endpoints to be passed to connecting hub
    ///     routers, indicating that messages targeted at these endpoints should
    ///     be forwarded by the hub to the root.
    /// </para>
    /// <para>
    ///     This logical endpoint exchange happens just after the uplink connection
    ///     from the hub to router is established.  The hub sends a <see cref="HubAdvertiseMsg" />
    ///     to the router, followed by zero or more <see cref="LogicalAdvertiseMsg" />
    ///     messages with the downlink endpoints.  The root responds to the
    ///     <see cref="HubAdvertiseMsg" /> by replying with zero or more
    ///     <see cref="LogicalAdvertiseMsg" /> messages with the uplink endpoints.
    /// </para>
    /// <para><b><u>Logical Routing Locality</u></b></para>
    /// <para>
    ///     As described above, the LillTek Messaging system randomly selects
    ///     a physical route or message handler from the set of possibilities
    ///     when routing a message.  This provides for load-balancing and 
    ///     fail-over across servers in a datacenter and even across
    ///     datacenters.  The key idea with this kind of routing, the application
    ///     has no control over where the message ends up getting sent.  The message
    ///     could just as easily end up being handled by a message handler in
    ///     the current process or on machine in a remote datacenter.
    /// </para>
    /// <para>
    ///     In actual deployments, it's often useful to keep message routing
    ///     as local to the source as possible to increase performance, reduce
    ///     network load, and in some situations, to improve reliability.
    ///     LillTek Messaging supports logical routing locality via the
    ///     <see cref="MsgFlag.ClosestRoute" /> message flag and the 
    ///     <b>MsgRouter.LocalEP</b> configuration setting.
    /// </para>
    /// <para>
    ///     When a message without the <see cref="MsgFlag.ClosestRoute" /> message 
    ///     flag set is sent to a logical endpoint with multiple physical
    ///     routes available, the <see cref="MsgRouter" /> will randomly
    ///     select one of the physical routes.
    /// </para>
    /// <para>
    ///     This behavior changes when the <see cref="MsgFlag.ClosestRoute" /> 
    ///     bit it set.  In this case, the <see cref="MsgRouter" /> will
    ///     sort the physical routes by <i>closeness</i>, using the following
    ///     criteria:
    /// </para>
    /// <list type="bullet">
    ///     <item>Routes to the current process (ie. routes with message handlers).</item>
    ///     <item>
    ///     Routes to the current computer (ie. the route's physical endpoint is a 
    ///     peer to this router's endpoint and the IP addresses are the same).
    ///     </item>
    ///     <item>Routes to the current subnet.</item>
    ///     <item>All remaining routes.</item>
    /// </list>
    /// <para>
    /// Then the closest physical route will be selected.  If multiple routes
    /// are equal close, then one of these will be randomly selected.
    /// </para>
    /// <para>
    /// Applications can hardcode the setting of the <see cref="MsgFlag.ClosestRoute" />
    /// message flag bit or certain logical endpoints can be configured such that
    /// all messages sent to theses endpoints will automatically have this flag set
    /// by the <see cref="MsgRouter" />.  The <b>MsgRouter.RouteLocal</b>
    /// configuration setting is an array of zero or more logical routes for
    /// which messages should favor local destinations.  These routes may
    /// include wildcards.  Here's an example configuration fragment:
    /// </para>
    /// <code language="none">
    /// #section MsgRouter
    /// 
    ///     RouteLocal[-] = abstract://Test/Local
    ///     RouteLocal[-] = abstract://MyApps/*
    /// 
    /// #endsection
    /// </code>
    /// <para><b><u>LillTek Messaging Sessions</u></b></para>
    /// <para>
    ///     The LillTek Messaging library implements a concept called sessions.
    ///     A session is a set of related messages passed between two or more
    ///     message endpoints.  Messages are correlated to session via their
    ///     <see cref="Msg._SessionID" /> property.  This property will be set to
    ///     the session's GUID and then is mapped to the appropriate session state
    ///     by the router's session manager.
    /// </para>
    /// <para>
    ///     Session behavior is defined by the <see cref="ISession" /> interface.  Each
    ///     router has a session manager associated with it.  The behavior of this session
    ///     manager is defined by <see cref="ISessionManager" />.  Sessions implemented
    ///     by the messaging library derive from <see cref="SessionBase" /> which provides
    ///     a partial implementation of the ISession interface.  <see cref="SessionManager" />
    ///     is the default session manager implementation used by the messaging library.
    ///     Custom session manager implementations can be used by assigning the custom
    ///     session manager instance to the <see cref="MsgRouter.SessionManager" /> property
    ///     before starting the router.
    /// </para>
    /// <para>
    ///     The messaging library provides implementions for three basic session types:
    /// </para>
    /// <para>
    ///     <see cref="QuerySession" /> implements a basic query/response behavior where
    ///     a client sends a query message to a server endpoint and waits for a response.
    /// </para>
    /// <para>
    ///     <see cref="DuplexSession" /> provides for a long running session between two 
    ///     specific service instances where messages can be send or queries issued from
    ///     either end of the session.
    /// </para>
    /// <para>
    ///     <see cref="ReliableTransferSession" /> copies the contents of a stream from one endpoint
    ///     to another.
    /// </para>
    /// <para><b>Processing Queries Asynchronously on the Server.</b></para>
    /// <para>
    /// The LillTek <see cref="MsgRouter" /> and <see cref="DuplexSession" />
    /// classes provide mechansisms for processing request/reply transactions
    /// both synchronously and asynchronously.  The synchronous methods are
    /// safe and easy to use.  The asynchronous mechanisms are a bit more 
    /// challenging.
    /// </para>
    /// <para>
    /// There are two basic asynchronous implementation problems that 
    /// need to be addressed:
    /// </para>
    /// <list type="bullet">
    ///     <item>
    ///     State about the request needs to be maintained somewhere
    ///     so that when the time comes to send the response, the application
    ///     still knows where to send it.
    ///     </item>
    ///     <item>
    ///     Orphaned transactions need to be detected somehow so that 
    ///     the router can be told to cancel the transaction.
    ///     </item>
    /// </list>
    /// <para>
    ///     The first problem isn't too difficult to solve.  One strategy is
    ///     for the application to keep a copy of the request message holding
    ///     the return endpoint and session ID) during transaction processing.
    /// </para>
    /// <para>
    ///     The second problem is more challenging.  At issue here is the fact
    ///     that both the basic <see cref="MsgRouter" /> and more advanced
    ///     <see cref="DuplexSession" /> request/reply transaction implementations
    ///     do no rely on simple timeout mechanisms to detect when a transaction
    ///     has failed (enabling LillTek Messaging to support applications that implement
    ///     arbitrarily long running transactions).  Instead, keep-alive messages are 
    ///     periodically transmitted back to the client while it appears that the
    ///     transaction is still being processed by the server.
    /// </para>
    /// <para>
    ///     Servers can process transactions synchronously or asynchronously.
    ///     Synchronous transaction processing is considered to be complete
    ///     when the message handler returns.  Asynchronous transaction processing 
    ///     is completed when the <see cref="MsgRouter.ReplyTo(Msg,Msg)" /> is called
    ///     for normal request/reply transactions or  <see cref="DuplexSession.ReplyTo(Msg,Msg)" /> 
    ///     is called for transactions within a session.
    /// </para>
    /// <para>
    ///     The problem for asynchronous transactions implementations is making
    ///     sure that every transaction is ultimately completed, with orphaned
    ///     transactions being canceled.  It is very important that this behavior
    ///     be implemented correctly to avoid the network, processing, and memory
    ///     overhead for orphaned transactions that might accumulate into large
    ///     numbers for long running services.
    /// </para>
    /// <para>
    ///     The <see cref="MsgRequestContext" /> class provides a solid solution
    ///     for managing asynchronous transactions and also abstracts away the 
    ///     difference between processing transactions inside or outside of a 
    ///     session.
    /// </para>
    /// <para>
    ///     The class is easy to use:
    /// </para>
    ///  <list type="number">
    ///      <item>
    ///      Construct a <see cref="MsgRequestContext" /> instance for the
    ///      request message be calling <see cref="Msg" />.<see cref="Msg.CreateRequestContext" />.
    ///      </item>
    ///      <item>
    ///      Maintain a reference to <see cref="MsgRequestContext" /> instance 
    ///      while asynchronously processing the request.
    ///      </item>
    ///      <item>
    ///      When you have successfully completed processing, call <see cref="MsgRequestContext.Reply" />
    ///      to transmit the reply back to the client and then call <see cref="MsgRequestContext.Close" />
    ///      or <see cref="MsgRequestContext.Dispose" />.
    ///      </item>
    ///      <item>
    ///      Call <see cref="MsgRequestContext.Cancel" /> if you want to abort the transaction,
    ///      sending a <see cref="CancelException" /> back to the client, or call 
    ///      <see cref="MsgRequestContext.Abort" /> to abort the transaction without
    ///      sending a reply at all.
    ///      </item>
    ///      <item>
    ///      Orphaned transactions will be addressed when there are no more references to the
    ///      <see cref="MsgRequestContext" /> instance and the CLR garbage collector calls
    ///      its finalizer just before discarding the context.  The finalizer cancels the
    ///      transaction if it hasn't already been completed.
    ///      </item>
    ///  </list>
    /// <para>
    ///     <b><u>Dead Router Detection</u></b>
    /// </para>
    /// <para>
    ///     The problem is that routes known to hub and peer-to-peer routers may take
    ///     some time to expire when a router goes offline unexpectantly (without multicasting
    ///     a <see cref="RouterStopMsg" />).  This means that messages to a logical endpoint
    ///     exposed by the router may not failover as quickly as would otherwise be possible.
    /// </para>
    /// <para>
    ///     The idea is to extend the <see cref="Msg" /> and <see cref="MsgRouter" /> classes
    ///     to support the concept of message receipt notifications.  The <see cref="MsgFlag.ReceiptRequest" />
    ///     flag indicates to the router exposing the target endpoint that a
    ///     <see cref="ReceiptMsg" /> should be sent when the message is received.  The
    ///     ReceiptMsg will be delivered to the endpoint specified by the optional
    ///     <see cref="Msg._ReceiptEP" /> property.
    /// </para>
    /// <para>
    ///     MsgRouters routing a message with a <see cref="MsgFlag.ReceiptRequest" />
    ///     flag set will need to do some additional work, if the router is actually performing
    ///     the final mapping of a logical endpoint to a physical one.  In this case,
    ///     the router will add a <see cref="MsgTrack" /> to a table indicating that the
    ///     router is waiting a <see cref="ReceiptMsg" /> to be received by the router confirming
    ///     the reception of the orignal message by the target router.  If this confirmation
    ///     message	is not received for a configurable amount of time, then the router
    ///     will assume that the target router is dead and will multicast a
    ///     <see cref="DeadRouterMsg" /> to the subnet so that the other routers
    ///     present will stop forwarding messages to the failed router..
    /// </para>
    /// <para>
    ///     Routers receiving the <see cref="DeadRouterMsg" /> will remove the indicated router
    ///     from their routing tables.  A router that receives notification of its
    ///     own death will generate a new logical endpoint set ID and multicast a
    ///     new <see cref="RouterAdvertiseMsg" /> to the subnet.
    /// </para>
    /// <para>
    ///     Note that receipt tracking is implemented only for messages delivered
    ///     via a TCP channel.  Messages delivered via UDP unicast or multicast
    ///     will not be tracked.
    /// </para>
    /// <para>
    ///     Unit tests can use the <see cref="MsgRouter.Paused" /> property to simulate
    ///     a dead router by forcing a router to sit idle and ignore all message processing.
    /// </para>
    /// <para><b><u>Clustering Support</u></b></para>
    /// <para>
    /// The LillTek Messaging library provides some classes to support building of 
    /// sophisticated clustered applications.  The <see cref="ClusterMember" /> class
    /// along with related types can be used to quickly implement a self-organizing
    /// master/slave cluster that automatically replicates shared and instance properties 
    /// across the cluster.
    /// </para>
    /// <para><b><u>Configurable Service Topologies</u></b></para>
    /// <para>
    /// The messaging layer also provides for the implementation of plugable cluster
    /// topology implementations.  The <see cref="ITopologyProvider" /> interface defines
    /// the behavior for plug-in to enable the abstraction of services instances deployed
    /// in a cluster.  These plug-ins provide standard server side functionality that allow 
    /// services to organize into clusters using customized topologies as well as standard
    /// client side functionality that allows client applications to call on the servers.
    /// <see cref="TopologyHelper" /> provides some utility methods useful for implementing,
    /// testing, and using custom topology providers.
    /// </para>
    /// <para>
    /// <see cref="BasicTopology" /> is a simple implementation of <see cref="ITopologyProvider" />
    /// that relies on native LillTek Messaging features.  The <c>LillTek.Datacenter</c> library implements
    /// additional generic topology providers.  Custom application specific providers can 
    /// also be written by implementing <see cref="ITopologyProvider" />. 
    /// </para>
    /// <para><b><u>LillTek Message Queue (LMQ)</u></b></para>
    /// <para>
    /// The LillTek Messaging layer implements support for delivering messages via
    /// persistent message queues to provide for communication between applications
    /// that are not always online or available.  The queuing types are defined in
    /// the <b>LillTek.Messaging.Queuing</b> namespace.  See <see cref="LillTek.Messaging.Queuing.OverviewDoc" />
    /// for more information.
    /// </para>
    /// <para><b><u>LillTek.Library Implementation Limitations</u></b></para>
    /// <para>
    ///     The current implementation is actually quite robust.  The main deficiencies
    ///     at this point is the lack of redundancies at the hub and root router levels.
    ///     A future version of the library by implementing a scheme where multiple instances
    ///     of these routers will be clustered.
    /// </para>
    /// <para>
    ///     Another potential problem lies in the fact that the routers and the programming
    ///     model doesn't provide for any kind of flow control.  It is entirely possible for
    ///     and application to flood a router with outbound messages that will be queued
    ///     faster than they can be delivered, resulting in memory problems.
    /// </para>
    /// <para>
    ///     At some point, I'd like to revisit the static logical routing between
    ///     root and hub routers and perhaps implememt some kind of route exchange
    ///     protocol based the concept of routing zones.
    /// </para>
    /// </remarks>
    public static class OverviewDoc
    {

    }
}

namespace LillTek.Messaging.Queuing
{

    /// <summary>
    /// Implements the LillTek Message Queue (LMQ) that supports the delivery of messages 
    /// via persistent message queues to provide for disconnected communication between 
    /// applications that are not always online or available. 
    /// </summary>
    /// <remarks>
    /// <para><b><u>Overview</u></b></para>
    /// <para>
    /// The <see cref="MsgQueue" /> class implements client side access to a LillTek message
    /// queuing cluster composed of Message Queue Service instances.  Message queues are
    /// identified using logical messaging endpoints.  Applications use <see cref="MsgQueue" />
    /// to enqueue and dequeue messages from queue services.
    /// </para>
    /// <para>
    /// Applications that need to send or receive queued messages need to instantiate
    /// a <see cref="MsgQueue" /> instance, establishing connection to a message queue
    /// service instance.  The class provides three constructors for doing this.  They
    /// all accept the <see cref="MsgRouter" /> to be used for sending and receivig messages.
    /// An optional <see cref="MsgQueueSettings" /> instance used to customize the instance 
    /// settings, including the base endpoint to use when connecting to the message queue service
    /// and also for enqueuing and dequeuing messages.  <see cref="MsgQueue.Close()" /> or <see cref="MsgQueue.Dispose" />
    /// should be called promptly once a queue is no longer needed top ensure that any 
    /// resources held are released.
    /// </para>
    /// <para>
    /// The <b>queueEP</b> parameter can be either a fully qualified logical ar abstract <see cref="MsgEP" />
    /// string or a relative queue name.  Relative queue names will be appended to the 
    /// queue base endpoint <see cref="MsgQueueSettings.BaseEP" /> from the <see cref="MsgQueueSettings" />.
    /// The fully qualified queue endpoint is used to establish a connection with a message queue
    /// service instance as well as to specify that target message queue when one isn't explicitly
    /// specified to one of the enqueue, dequeue, or peek methods.
    /// </para>
    /// <note>
    /// Once a <see cref="MsgQueue" /> establishes a session with queue service instance, all 
    /// subsequent messaging operations will be directed to that instance regardless of whether
    /// it is configured to manage the target queue or not.
    /// </note>
    /// <para>
    /// Messages are sent synchronously to the queue by creating a <see cref="QueuedMsg" /> 
    /// and setting its <see cref="QueuedMsg.Body" /> property to a serializable object instance
    /// and then calling <see cref="MsgQueue.Enqueue" />.  Messages can be read synchronously from the queue
    /// by calling <see cref="MsgQueue.Dequeue()" /> or <see cref="MsgQueue.Dequeue(TimeSpan)" />.  
    /// Messages can be sent asynchronously using <see cref="MsgQueue.BeginEnqueue" /> and 
    /// <see cref="MsgQueue.EndEnqueue" /> and received asynchronously using <see cref="MsgQueue.BeginDequeue" /> 
    /// and <see cref="MsgQueue.EndDequeue" />.
    /// </para>
    /// <para>
    /// Here's a simple example that opens a queue and enqueues a message.
    /// </para>
    /// <code language="cs">
    /// void Test() {
    /// 
    ///     LeafRouter      router;
    /// 
    ///     router = new LeafRouter();
    ///     router.Start();
    /// 
    ///     using (var queue = new MsgQueue(router,"logical://MyQueues/Test")) {
    /// 
    ///         queue.Enqueue(new QueuedMsg("Hello World!"));
    ///     }
    /// }
    /// </code>
    /// <para>
    /// It is possible to enqueue and dequeue messages from any arbitrary message
    /// queue using the <see cref="MsgQueue.EnqueueTo" />, <see cref="MsgQueue.EnqueueTo" />,
    /// <see cref="MsgQueue.DequeueFrom(string)" /> and <see cref="MsgQueue.DequeueFrom(string,TimeSpan)" />
    /// methods and their asynchronous equivalents: <see cref="MsgQueue.BeginEnqueueTo" />,
    /// <see cref="MsgQueue.EndEnqueueTo" />, <see cref="MsgQueue.BeginDequeueFrom" />, and
    /// <see cref="MsgQueue.EndDequeueFrom" />. 
    /// </para>
    /// <para>
    /// <see cref="MsgQueue" /> also provides <see cref="MsgQueue.Peek()" />,
    /// <see cref="MsgQueue.Peek(TimeSpan)" />, <see cref="MsgQueue.PeekFrom(string)" />, and
    /// <see cref="MsgQueue.PeekFrom(string,TimeSpan)" /> to check to see if 
    /// there's a message waiting in a queue without removing it.  Note that applications should not
    /// assume that just because <see cref="MsgQueue.Peek()" /> returned a message that
    /// the next call to <see cref="MsgQueue.Dequeue()" /> will succeed or return the
    /// same message.  This class implements some asynchronous peek methods:
    /// <see cref="MsgQueue.BeginPeek" />, <see cref="MsgQueue.EndPeek" />, 
    /// <see cref="MsgQueue.BeginPeekFrom" />, and <see cref="MsgQueue.EndPeekFrom" />.
    /// </para>
    /// <para><b><u>Transaction Support</u></b></para>
    /// <para>
    /// <see cref="MsgQueue" /> supports the .NET Framework <see cref="TransactionScope" />
    /// defined in the <b>System.Transactions</b> namespace so message queuing can 
    /// implictly particpate in distributed transactions. <see cref="MsgQueue" /> instances 
    /// check to whether <see cref="MsgQueue.Enqueue" />, <see cref="MsgQueue.Dequeue()" />, 
    /// or <see cref="MsgQueue.Peek()" />   are being called within an ambient transaction.  If 
    /// this is the case, the <see cref="MsgQueue" /> enlists the operation in the transaction and
    /// then handles the transaction callbacks to prepare, commit or rollback the transaction.
    /// </para>
    /// <para>
    /// Here's a transacted example showing the dequeuing of a message from one queue 
    /// and forwarding it to two other queues.  If any of these operations fail then 
    /// the dequeued message will be restored and the enqueued messages will be removed.
    /// </para>
    /// <code language="cs">
    /// using System;
    /// using System.Transactions;
    /// 
    /// using LillTek.Common;
    /// using LillTek.Messaging;
    /// 
    /// void ForwardMessage() {
    /// 
    ///     LeafRouter  router;
    /// 
    ///     router = new LeafRouter();
    ///     router.Start();
    /// 
    ///     using (var scope = new TransactionScope()) {
    /// 
    ///         using (MsgQueue queue = new MsgQueue(router,"logical://queues/*")) {
    /// 
    ///             QueuedMsg   msg;
    /// 
    ///             msg = queue.DequeueFrom("Input);
    ///             queue.EnqueueTo("output1",msg);
    ///             queue.EnqueueTo("output2",msg);
    /// 
    ///             scope.Commit();
    ///         }
    ///     }
    /// }
    /// </code>
    /// <para>
    /// The example above dequeues a message from <b>logical://queues/Input</b> and copies it to 
    /// <b>logical://queues/Output1</b> and <b>logical://queues/Output2</b>
    /// </para>
    /// <para>
    /// <see cref="MsgQueue" /> also implement the simple built-in transaction
    /// methods: <see cref="MsgQueue.BeginTransaction" />, <see cref="MsgQueue.Commit" />,  
    /// <see cref="MsgQueue.Rollback" />, and <see cref="MsgQueue.RollbackAll" />.  These methods are used 
    /// internally to implement <see cref="TransactionScope" /> support but they can also be used 
    /// explicitly.  Note that these explicit methods do allow the asynchronous enqueue, dequeue, and
    /// peek methods to be included within transactions (as opposed to  <see cref="TransactionScope" />
    /// which does not support asynchronous methods).  Here's a transaction example using the
    /// built-in methods:
    /// </para>
    /// <code language="cs">
    /// void ForwardMessage() {
    /// 
    ///     LeafRouter  router;
    /// 
    ///     router = new LeafRouter();
    ///     router.Start();
    /// 
    ///     using (MsgQueue queue = new MsgQueue(router,"logical://queues/*")) {
    /// 
    ///         QueuedMsg   msg;
    ///         bool        commited = false;
    /// 
    ///         queue.BeginTransaction();
    /// 
    ///         try {
    /// 
    ///             msg = queue.DequeueFrom("Input);
    ///             queue.EnqueueTo("output1",msg);
    ///             queue.EnqueueTo("output2",msg);
    ///             queue.Commit();
    ///             comitted = true;
    ///         }
    ///         finally {
    /// 
    ///             if (!committed)
    ///                 queue.RollbackAll();
    ///         }
    ///     }
    /// }
    /// </code>
    /// <para><b><u>Delivery Order Guarantees</u></b></para>
    /// <para>
    /// The LillTek Message Queuing platform currently makes no guarantees as to the order
    /// in which messages will be ultimately delivered.  In general, messages with the
    /// same <see cref="DeliveryPriority" /> will be delivered in the order they were
    /// submitted and messages with higher priorities will be delivered before messages
    /// with lower priorities but applications should not expect this behavior to be
    /// enforced consistently for all messages.
    /// </para>
    /// <para><b><u>Asynchronous Method Limitations</u></b></para>
    /// <para>
    /// Only one operation may be outstanding at any given
    /// time on a <see cref="MsgQueue" /> instance.  This restriction
    /// is due to the fact that <see cref="MsgQueue" /> uses <see cref="DuplexSession" />
    /// to communicate with the message queue service instances and
    /// duplex sessions support only one query at a time.  You'll see
    /// <see cref="InvalidOperationException" /> if you attempt to perform
    /// more than one operation in parallel on a queue.
    /// </para>
    /// <para>
    /// </para>
    /// <para>
    /// Since the <see cref="TransactionScope" /> implementation is inherently synchronous in
    /// design, the asynchronous queue methods (<see cref="MsgQueue.BeginEnqueue" /> etc) <b>do not</b>
    /// participate in ambient transactions.
    /// </para>
    /// </remarks>
    public static class OverviewDoc
    {
    }
}

