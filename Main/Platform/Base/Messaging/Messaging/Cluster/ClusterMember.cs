//-----------------------------------------------------------------------------
// FILE:        ClusterMember.cs
// CONTRIBUTOR: Jeff Lill
// COPYRIGHT:   Copyright (c) 2005-2015 by Jeffrey Lill.  All rights reserved.
// DESCRIPTION: Implements a mechanism where a cluster of related services instances
//              can elect a master instance that is responsible for handling cluster
//              wide duties such as periodicaly broadcasting information about
//              the cluster status.

using System;
using System.IO;
using System.Text;
using System.Threading;
using System.Reflection;
using System.Diagnostics;
using System.Collections.Generic;

using LillTek.Common;
using LillTek.Advanced;

// $todo(jeff.lill): 
//
// This protocol is pretty reasonable for relatively small clusters
// of servers: 10s to maybe as many as 100 servers.  Much beyond
// this, we'll see quite a bit of network traffic just to maintain
// the cluster state.  I estimate that maintaining a cluster of
// 100 servers would consume about 500Kb/sec or 1% of a 1Gb/sec
// network.
//
// The solution for this is to redesign the protocol a bit to be
// closer to what I did for replicating message routing tables.
// Rather than having the slaves send their entire state to the master,
// they would broadcast their state version number or timestamp.
// Then the master would compare the received timestamp with 
// the timestamp it has and decide whether to request the 
// full state state from the slave.
//
// Broadcasts from the master to the slaves would work the same,
// where timestamps would be sent and slaves would query the
// master for information they don't have.
//
// A scheme like this would reduce network traffic by something
// like 100x once a cluster has stablized.

namespace LillTek.Messaging
{
    /// <summary>
    /// Implements a mechanism where a cluster of related service instances elect 
    /// a master instance that is responsible for handling cluster wide duties such 
    /// as periodicaly broadcasting information about the cluster status.
    /// </summary>
    /// <remarks>
    /// <para>
    /// This class is pretty easy to use.  When your service starts, instantiate an
    /// instance with one of the constructors: <see cref="ClusterMember(MsgRouter,ClusterMemberSettings)" />
    /// or <see cref="ClusterMember(MsgRouter,string)" />.  Both constructors require
    /// a running <see cref="MsgRouter" />.  The first accepts a <see cref="ClusterMemberSettings" />
    /// instance with the settings to be used.  The second constructor loads the settings
    /// from the application configuration.
    /// </para>
    /// <para>
    /// Once the instance is constructed, call <see cref="Start()" /> to begin the process
    /// of joining the cluster.  <see cref="Stop" /> should be called when the application
    /// service terminates.  Call <see cref="JoinWait" /> to wait for the instance to
    /// fully join the cluster and is in the <see cref="ClusterMemberState.Master" /> or
    /// <see cref="ClusterMemberState.Slave" /> state.  The <see cref="IsOnline" /> 
    /// property can also be used to determine if the member has fully joined the cluster.
    /// </para>
    /// <para>
    /// Applications can add custom string name/value properties to the cluster member
    /// instance using the <see cref="Set(string,string)" />, <see cref="Remove(string)" />,
    /// <see cref="ContainsKey" />, <see cref="TryGetValue" />, and <see cref="Clear" /> 
    /// method as well as the class indexer.  These properties will be replicated across 
    /// the cluster and will be available for other cluster members via their 
    /// <see cref="ClusterStatus" /> properties.  Note that property names with 
    /// leading underscores are reserved and cannot be used by applications.
    /// </para>
    /// <para>
    /// Cluster members can exist in one of several states:
    /// </para>
    /// <list type="table">
    ///     <item>
    ///         <term>Stopped</term>
    ///         <definition>
    ///         This state indicates that the instance is not currently participating
    ///         in the cluster.
    ///         </definition>
    ///     </item>
    ///     <item>
    ///         <term>Warmup</term>
    ///         <definition>
    ///         Newly created <see crer="ClusterMember" /> instances start off and
    ///         remain in this state until the instance has been integrated into
    ///         the cluster and is considered to be online.
    ///         </definition>
    ///     </item>
    ///     <item>
    ///         <term>Master</term>
    ///         <definition>
    ///         One online cluster member will be elected to be the master.  This
    ///         instance will remain as master until it is stopped or a new election
    ///         is called due to a conflict.  The master is responsible for coordinating
    ///         any cluster wide activities.  The master is also responsible for
    ///         periodically broadcasting the <see cref="ClusterStatus" /> to each
    ///         server in the cluster.
    ///         </definition>
    ///     </item>
    ///     <item>
    ///         <term>Slave</term>
    ///         <definition>
    ///         All online and active cluster members besides the master will become slaves.
    ///         The slave instances monitor the master and call an election if
    ///         it appears that the master has gone down.  Slaves also periodically
    ///         transmit their state to the master so this can be included in the
    ///         replicated cluster state.
    ///         </definition>
    ///     </item>
    ///     <item>
    ///         <term>Election</term>
    ///         <definition>
    ///         The cluster is in the process of electing a new master.
    ///         </definition>
    ///     </item>
    ///     <item>
    ///         <term>Observer</term>
    ///         <definition>
    ///         The member is a passive cluster participant that will never be 
    ///         elected to be the master.  Member status for observers is
    ///         replicated across the cluster.
    ///         </definition>
    ///     </item>
    ///     <item>
    ///         <term>Monitor</term>
    ///         <definition>
    ///         The member eavesdrops on cluster status messages but does not
    ///         participate in the cluster.  No member status for monitor
    ///         instances will be replicated across the cluster so other
    ///         cluster members will not be aware of the existance of monitors.
    ///         </definition>
    ///     </item>
    ///     <item>
    ///         <term>Unknown</term>
    ///         <definition>
    ///         The cluster member's status is not known.  This should happen only
    ///         if there are different wildly different LillTek Platform version
    ///         running across the cluster. 
    ///         </definition>
    ///     </item>
    /// </list>
    /// <para>
    /// Clusters are identified on the network via a LillTek Messaging logical endpoint such as
    /// <b>logical://MyCluster</b>.  This is available in the <see cref="ClusterBaseEP" /> property.
    /// Each cluster member is assigned globally unique <see cref="InstanceID" /> when the instance 
    /// is started.  Each instance registers the <b>logical://MyCluster/&lt;instance-guid&gt;</b>
    /// with the messaging system and also saves this endpoint in <see cref="InstanceEP" />.  
    /// <see cref="ClusterEP" /> is then initialized to the endpoint to be used to broadcast or load
    /// balance across all cluster members (or <b>logical://MyCluster/*</b> in this example).
    /// </para>
    /// <para>
    /// Use <see cref="SendToMaster" /> to send a message to the cluster master instance, 
    /// <see cref="QueryMaster" /> to query the master instance, <see cref="SendTo" /> to
    /// send a message to a specific instance or <see cref="Broadcast" /> to broadcast a message 
    /// to all cluster instances.
    /// </para>
    /// <para>
    /// The class exposes several events that can be monitored by applications including:
    /// <see cref="StateChange" /> which is raised when the member's <see cref="State" />
    /// changes, <see cref="MasterTask" /> which is raised periodically for master instances
    /// so that they can implement background processing, <see cref="SlaveTask" /> which
    /// is raised periodically for slave instances for background processing,  <see cref="StatusTransmission" />
    /// which is call just before member or cluster status is transmitted, and 
    /// <see cref="ClusterStatusUpdate" /> which is raised when a <see cref="ClusterMemberStatus" /> 
    /// update is received from the master.  
    /// </para>
    /// <para>
    /// Note that a lock on the associated message router will be obtained before firing these 
    /// events and that this lock will be released after the event handler returns.
    /// </para>
    /// <para><b><u>Cluster Member Startup Modes</u></b></para>
    /// <para>
    /// Cluster members may be started in one of several modes.  These modes
    /// determine how the member participates in the cluster activities.
    /// The startup mode is specified by setting <see cref="ClusterMemberSettings" />.<see cref="ClusterMemberSettings.Mode" />
    /// to one of the <see cref="ClusterMemberMode" /> values:
    /// </para>
    /// <list type="table">
    ///     <item>
    ///         <term><see cref="ClusterMemberMode.Unknown" /></term>
    ///         <description>
    ///         Indicates that the mode is not known.  You may see this mode in
    ///         <see cref="ClusterMemberStatus" /> if cluster instances are running
    ///         on wildly different LillTek Platform versions.
    ///         </description>
    ///     </item>
    ///     <item>
    ///         <term><see cref="ClusterMemberMode.Normal" /></term>
    ///         <description>
    ///         Indicates that a <see cref="ClusterMember" /> should go through 
    ///         the normal master election cycle and eventually enter into the
    ///         <see cref="ClusterMemberState.Master" /> or <see cref="ClusterMemberState.Slave" />
    ///         state.
    ///         </description>
    ///     </item>
    ///     <item>
    ///         <term><see cref="ClusterMemberMode.Observer" /></term>
    ///         <description>
    ///         Indicates that a <see cref="ClusterMember" /> should immediately enter the
    ///         <see cref="ClusterMemberState.Observer" /> state and remain there.  Cluster
    ///         observer state is replicated across the cluster so other instances know
    ///         about these instances but observers will never be elected as master.
    ///         </description>
    ///     </item>
    ///     <item>
    ///         <term><see cref="ClusterMemberMode.Monitor" /></term>
    ///         <description>
    ///         Indicates that a <see cref="ClusterMember" /> should immediately enter the 
    ///         <see cref="ClusterMemberState.Monitor" /> state and remain there.  Monitors
    ///         collect and maintain cluster status but do not actively participate in the
    ///         cluster.  No member status information about a monitor will be replicated
    ///         across the cluster.
    ///         </description>
    ///     </item>
    ///     <item>
    ///         <term><see cref="ClusterMemberMode.PreferSlave" /></term>
    ///         <description>
    ///         Indicates that a <see cref="ClusterMember" /> instance prefers to be
    ///         started as a cluster slave if there's another instance running without
    ///         this preference.
    ///         </description>
    ///     </item>
    ///     <item>
    ///         <term><see cref="ClusterMemberMode.PreferMaster" /></term>
    ///         <description>
    ///         Indicates that a <see cref="ClusterMember" /> instances prefers to be
    ///         started as the cluster master.  If a master is already running and
    ///         it does not have this preference then a master election will be called.
    ///         </description>
    ///     </item>
    /// </list>
    /// <para><b><u>Cluster-wide and Member Properties</u></b></para>
    /// <para>
    /// One of the more useful features of the <see cref="ClusterMember" /> is its support
    /// for replicating global cluster as well as instance properties across all cluster
    /// member instances.  These properties are string based name/value pairs.  Property
    /// names are case insensitive and cannot include a leading underscore.
    /// </para>
    /// <para>
    /// Global cluster properties are maintained by the cluster master and are distributed
    /// to the cluster members via the periodic global status broadcast.  Member properties
    /// are maintained by each member instance and are delivered periodically to the
    /// cluster master instance to be included in the global status broadcast.
    /// </para>
    /// <para>
    /// Cluster members can modify their individual properties via the <see cref="Set" />,
    /// <see cref="Remove" />, <see cref="ContainsKey" />, <see cref="TryGetValue" />,
    /// <see cref="Clear" /> methods in addition to the class indexer.  Simply add or
    /// modify the desired properties and the value will be replicated across the 
    /// cluster.  Cluster members can access another member's properties via their
    /// <see cref="ClusterStatus" /> property and then indexing into the <see cref="Messaging.ClusterStatus.Members" />
    /// collection to return the member's <see cref="ClusterMemberStatus" /> record.
    /// The properties are available for reading via the <see cref="ClusterMemberStatus.TryGetValue" />
    /// and <see cref="ClusterMemberStatus.ContainsKey" /> as well as the class indexer.
    /// </para>
    /// <para>
    /// Only the cluster master instance may modify the cluster wide properties.
    /// It does so via the <see cref="GlobalSet" />, <see cref="GlobalRemove" />, 
    /// <see cref="GlobalContainsKey" />, <see cref="GlobalTryGetValue" />,
    /// and <see cref="GlobalClear" /> methods.  Simply add or modify the desired
    /// global cluster properties and these changes will be replicated across the
    /// cluster.  Global cluster properties are available on every member via
    /// the <see cref="ClusterStatus" /> property's <see cref="Messaging.ClusterStatus.ContainsKey" /> and
    /// <see cref="Messaging.ClusterStatus.TryGetValue" /> methods as well as the class indexer.
    /// </para>
    /// <para>
    /// Note that although all of the <see cref="ClusterMember" /> property related
    /// methods described above are threadsafe, it is sometimes useful to ensure
    /// that a set of property changes are replicated across the cluster together.
    /// A simple way to do this is to obtain a lock on the <see cref="ClusterMember" />
    /// instance's <see cref="SyncRoot" /> for <b>a very brief period of time</b> while 
    /// modifying the properties.  Doing this will block all cluster member background
    /// processing including the tasks that handle the replication transmissions.
    /// Here's an example:
    /// </para>
    /// <code language="cs">
    /// // Make sure that the two changes below are replicated 
    /// // across the cluster together
    /// 
    /// lock (myMember.SyncRoot) 
    /// {
    ///     myMember.GlobalSet("my-value1","10");
    ///     myMember.GlobalSet("my-value2","20")
    /// }
    /// </code>
    /// <para>
    /// Sometimes it is important for a cluster member or master to replicate 
    /// changes to the member or global properties ASAP.  <see cref="TransmitStatusNow" />
    /// is available for this purpose.  For cluster slave instances, this method
    /// schedules an immediate transmission of the member status to the cluster
    /// master.  For cluster masters, this method schedules an immediate broadcast
    /// of the cluster status across the cluster.
    /// </para>
    /// <para><b><u>Cluster Member Settings</u></b></para>
    /// <para>
    /// The cluster member settings are specified by the <see cref="ClusterMemberSettings" />
    /// class and can be specified programatically or loaded from the application configuration.
    /// Note that it <b>is critical that all cluster member instances share the same settings</b>.
    /// Mismatched timing settings in particular can introduce strange behavior typically 
    /// involving problems with electing the master instance. 
    /// </para>
    /// <para>
    /// To flag problems, each instance's settings will be included in the <see cref="ClusterMemberStatus" />
    /// information transmitted to the master and the master will log warnings when it sees 
    /// settings that differ from the master settings.
    /// </para>
    /// <para>
    /// Some care should be taken to ensure that the cluster configuration is the same
    /// across all servers.  One way to use this is the use the <b>LillTek Configuration Service</b>
    /// to have all instances load their configuration from a common source.
    /// </para>
    /// <para><b><u>Handling Application Messages for a Cluster Member Endpoint</u></b></para>
    /// <para>
    /// Most clustered applications will need to handle custom messages sent to
    /// to the cluster member's globally unique endpoint.  The easiest way to do
    /// this is to add the <b>DynamicScope</b> parameter to the <c>[MsgHandler]</c>
    /// attribute tagging the application's handler method and then passing the
    /// scope value to the <see cref="AddTarget" /> method and let the <see cref="ClusterMember" />
    /// class handle the mangling of the handler's endpoint.
    /// </para>
    /// <para>
    /// This provides a good way to expose application message handlers that listen on the
    /// cluster member's globally unique endpoint.  Here's a code fragment demonstrating
    /// how this would work:
    /// </para>
    /// <code language="none">
    /// public class MyApplication 
    /// {
    ///     MsgRouter       router;
    ///     ClusterMember   cluster;
    /// 
    ///     public void Start()
    ///     {
    ///         router.Start();
    ///         cluster.Start();
    ///         cluster.AddTarget(this,"MyAppScope");
    ///     }
    /// 
    ///     [MsgHandler(LogicalEP=MsgEP.Null,DynamicScope="MyAppScope")]
    ///     public void OnMsg(MyMsg msg) 
    ///     {
    ///         // Handle the message
    ///     }
    /// }
    /// </code>
    /// <para>
    /// Note that the <b>LogicalEP</b> parameter in the <b>MsgHandler</b> is ignored
    /// and can be set to anything (including <c>null</c>).  This will be replaced with
    /// the cluster member's unique logical endpoint by this method. 
    /// </para>
    /// <para><b><u>Cluster Member Protocol Overview</u></b></para>
    /// <para>
    /// The primary purpose behind the <see cref="ClusterMember" /> class and its underlying
    /// protocol is to provide a mechanism where a set of related service instances on the network
    /// can become aware of each other and also elect one of the instances to maintain global
    /// cluster state as well as to coordinate cluster wide activities.  The primary design goal for
    /// this class and its protocol is to have all of this happen dynamically at runtime, 
    /// without the need for manual configuration or intervention.  The protocol is designed
    /// to handle the following scenarios:
    /// </para>
    /// <blockquote>
    /// <para><b>Lone Instance</b></para>
    /// <para>
    /// This scenario begins with no cluster instances running on the network.
    /// A single new instance is started.  The instance broadcasts its presence
    /// to the cluster and waits for a master to respond with the cluster status.
    /// Since there is no master, the new instance broadcasts a call for a master
    /// election.  Since there are no other instances, the new instances will
    /// elect itself to be the master.
    /// </para>
    /// <para><b>Simultaneous Boot</b></para>
    /// <para>
    /// This scenario begins with no cluster instances running on the network.
    /// Multiple instances are started simultaniously.  All of the instances broadcast
    /// their presence and wait a master to repond with the cluster status.  Since
    /// there is no master, the instances will elect a master.
    /// </para>
    /// <para><b>Slave Starts</b></para>
    /// <para>
    /// This scanario begins with one or more cluster instances including
    /// a master.  A new instance starts and broadcasts its presence to the 
    /// cluster.  The master receives this and responds by updating its cluster
    /// state and broadcasting the new state to the cluster.  All cluster instances
    /// (including the new one) receive the new state, effectively adding the
    /// new instance to the cluster.
    /// </para>
    /// <para><b>Slave Stops Gracefully</b></para>
    /// <para>
    /// This scenario begins with one or more running cluster instances including
    /// a master and at least one slave.  The slave instance is shut down gracefully.  
    /// During the shut down process, the slave sends a shut down message to the master which
    /// removes the instance from its cluster state and then broadcasts this new state
    /// to the remaining cluster slaves, effectively removing the slave from the
    /// cluster.
    /// </para>
    /// <para><b>Master Stops Gracefully</b></para>
    /// <para>
    /// This scenario begins with one or more running cluster instances including
    /// a master.  The master begins a graceful shutdown.  Just before going
    /// offline, the master promotes one of the other instances to become the
    /// master and sends the nominee a message.  The nominee becomes the master 
    /// and begins broadcasting the cluster state to the cluster.  Note that the
    /// slave instance with the lexically greatest instance endpoint will be the
    /// one selected for promotion to provide some determinism for unit tests.
    /// </para>
    /// <para><b>Slave Fails</b></para>
    /// <para>
    /// This scenario begins with one or more running cluster instances including
    /// a master.  One of the slave instances abruptly fails without notifying
    /// the cluster.  After a period of time, the master realizes that it has
    /// not received a status update from the failed slave and removes the
    /// slave from the cluster status.
    /// </para>
    /// <para><b>Master Fails</b></para>
    /// <para>
    /// This scenario begins with one or more slaves and a master running as
    /// a cluster.  The master abruptly fails without notifing the cluster.
    /// After a period of time, one or more of the slaves notice that they
    /// have stopped receiving cluster status updates from the master and
    /// a new master election is called and the slaves elect a new master
    /// from their number.
    /// </para>
    /// <para><b>Multiple Masters</b></para>
    /// <para>
    /// This scenario begins with two clusters with the same base cluster enpoint
    /// running on disjoint networks.  Each cluster has a master and zero or more 
    /// slave instances.  At this point, the networks are connected and cluster
    /// status messages broadcast by the two masters are now received by the
    /// combined cluster instances.  The master instances observe the presence of 
    /// the other master via their cluster status update broadcast and compares
    /// its <see cref="InstanceEP" /> to that of the other master.  If it finds
    /// that its endpoint (converted to lowercase) is lexicially less than the 
    /// other master then the instance will demote itself into a slave.
    /// </para>
    /// <para>
    /// This situation is more common than you might think.  Here's how this could 
    /// happen:
    /// </para>
    /// <list type="number">
    ///     <item>Several machines are running a clustered service.</item>
    ///     <item>One of the machines is disconnected from the network for a few minutes.</item>
    ///     <item>
    ///     The service on the disconnected machine will elect itself to be master
    ///     since it sees no other instances.
    ///     </item>
    ///     <item>
    ///     The remaining service instances will notice that the instance on the
    ///     disconnected machine is no longer present and removes it from the 
    ///     cluster, electing a new master if necessary.
    ///     </item>
    ///     <item>The machine is reconnected to the network.</item>
    ///     <item>
    ///     There are now two cluster masters: the service running on the reconnected machine
    ///     and the other cluster master.
    ///     </item>
    /// </list>
    /// </blockquote>
    /// <para><b><u>Protocol Messages</u></b></para>
    /// <para>
    /// The Cluster Member protocol uses the somewhat general purpose <see cref="ClusterMemberMsg" /> 
    /// to communicate state between cluster instances.  This message type derives from
    /// <see cref="BlobPropertyMsg" /> and thus can serialize name value pairs as well as
    /// a single binary array.
    /// </para>
    /// <para>
    /// All protocol messages include two predefined name/value pairs including <b>command</b>
    /// and <b>senderEP</b>.  <b>command</b> indicates the purpose of the message and
    /// <b>senderEP</b> is the endpoint of the cluster instance that sent the message.
    /// The following message commands are supported by the cluster members:
    /// </para>
    /// <blockquote>
    /// <para><b>member-status</b></para>
    /// <para>
    /// This message is broadcast by a cluster member instance to the cluster when the instance
    /// first starts and is also send periodically by slave instances to the cluster master.
    /// Cluster masters use these transmissions to determine when new cluster instances
    /// start and use the lack of these transmissions to determine when cluster instances
    /// have gone offline.  The message binary data includes the serialized instance
    /// <see cref="ClusterMemberStatus" />.
    /// </para>
    /// <para><b>cluster-status</b></para>
    /// <para>
    /// This message is broadcast periodically by the master to the cluster so that global
    /// cluster state can be maintained across all cluster instances.  Slave instances
    /// use the lack of these transmissions to determine when the master has gone
    /// offline.  The message binary data includes the current serialized <see cref="ClusterStatus" />.
    /// </para>
    /// <para>
    /// Cluster slaves will verify that their individual status is present in the
    /// cluster status.  If this not the case, the slave will immediately sent a
    /// status update to the master.
    /// </para>
    /// <para><b>election</b></para>
    /// <para>
    /// This message is broadcast to the cluster when an instance notices that it
    /// is not receiving <b>cluster-status</b> messages from a master.  Cluster
    /// instances respond by setting the <see cref="ClusterMemberState.Election" /> state
    /// and broadcasting their <b>member-status</b> to the cluster.  The instances
    /// wait a period of time to collect the <b>member-status</b> messages from
    /// the other instances and the compares its <see cref="InstanceEP" /> (converted
    /// to lower case) to the endpoints of the other instances.  The instance whose
    /// endpoint is lexically greater than all of the others will win the election
    /// and promote its state to <see cref="ClusterMemberState.Master" /> and begin
    /// broadcasting <b>cluster-status</b> messages.  The remaining instances will
    /// set their states to <see cref="ClusterMemberState.Slave" /> when they receive
    /// the <b>cluster-status</b> messages.
    /// </para>
    /// <para><b>promote</b></para>
    /// <para>
    /// This message is sent by a master that is shutting down gracefully to the cluster
    /// service instance that is being promoted as the new cluster master.  The message
    /// binary data includes the current serialized <see cref="ClusterStatus" />.  The
    /// promoted instance sets its state to <see cref="ClusterMemberState.Master" />
    /// and begins broadcasting <b>cluster-status</b> messages.
    /// </para>
    /// </blockquote>
    /// </remarks>
    /// <threadsafety instance="true" />
    public class ClusterMember : IDynamicEPMunger
    {
        //---------------------------------------------------------------------
        // Private types

        /// <summary>
        /// Defines the state cluster member state processor behaviors.  Note that
        /// all methods will be called within a ClusterMember lock for threadsafety.
        /// </summary>
        private interface IStateMachine
        {
            /// <summary>
            /// Initializes the state processor, associating the cluster member.
            /// </summary>
            /// <param name="member">The cluster member.</param>
            void Initialize(ClusterMember member);

            /// <summary>
            /// Called periodically to handle background activities.
            /// </summary>
            void BkTask();

            /// <summary>
            /// Called when a cluster member protocol message is received.
            /// </summary>
            /// <param name="msg">The message received.</param>
            void OnMessage(ClusterMemberMsg msg);

            /// <summary>
            /// Schedules the immediate transmission of any status information 
            /// appropriate for the current state to the cluster.
            /// </summary>
            void TransmitStatusNow();

            /// <summary>
            /// Called when simulating the network failing or coming back online.
            /// </summary>
            /// <param name="fail"><c>true</c> if network is failed, <c>false</c> if it's back online.</param>
            void NetworkFailure(bool fail);
        }

        /// <summary>
        /// Used for tracking state information while the member is in the 
        /// <see cref="ClusterMemberState.Warmup" /> state.
        /// </summary>
        private class WarmupStateMachine : IStateMachine
        {
            // The parent member.

            private ClusterMember member;

            // Time limit (SYS) marking the end of the period of waiting for a 
            // cluster status update from the master before calling for an election.

            private DateTime expireTime;

            /// <summary>
            /// Initializes the state processor, associating the cluster member.
            /// </summary>
            /// <param name="member">The cluster member.</param>
            public void Initialize(ClusterMember member)
            {
                ClusterMemberMsg    msg;

                this.member     = member;
                this.expireTime = SysTime.Now + member.settings.MissingMasterInterval;

                // Broadcast a member-status to give any existing master the chance
                // to recognize this instance ASAP.

                msg = new ClusterMemberMsg(member.instanceEP, ClusterMemberProtocolCaps.Current, ClusterMemberMsg.MemberStatusCmd, member.GetMemberStatus().ToArray());
                member.Broadcast(msg);
            }

            /// <summary>
            /// Called periodically to handle background activities.
            /// </summary>
            public void BkTask()
            {
                if (SysTime.Now >= expireTime)
                {
                    // We've waited long enough for cluster status message
                    // so transition to the ELECTION state.

                    member.Broadcast(new ClusterMemberMsg(member.instanceEP, ClusterMemberMsg.ElectionCmd));
                    member.SetState(ClusterMemberState.Election);
                }
            }

            /// <summary>
            /// Called when a cluster member protocol message is received.
            /// </summary>
            /// <param name="msg">The received message.</param>
            public void OnMessage(ClusterMemberMsg msg)
            {
                switch (msg.Command.ToLowerInvariant())
                {
                    case ClusterMemberMsg.ClusterStatusCmd:

                        // Looks like there's a cluster master so save the
                        // cluster status and transition to the SLAVE state.

                        member.clusterStatus = new ClusterStatus(msg._Data);
                        member.SetState(ClusterMemberState.Slave);
                        break;

                    case ClusterMemberMsg.ElectionCmd:

                        // One of the other cluster members has called for an election
                        // so we'll join in too.

                        member.SetState(ClusterMemberState.Election);
                        break;
                }
            }

            /// <summary>
            /// Schedules the immediate transmission of any status information 
            /// appropriate for the current state to the cluster.
            /// </summary>
            public void TransmitStatusNow()
            {
            }

            /// <summary>
            /// Called when simulating the network failing or coming back online.
            /// </summary>
            /// <param name="fail"><c>true</c> if network is failed, <c>false</c> if it's back online.</param>
            public void NetworkFailure(bool fail)
            {
            }
        }

        /// <summary>
        /// Used for tracking state information while the member is in the 
        /// <see cref="ClusterMemberState.Election" /> state.
        /// </summary>
        private class ElectionStateMachine : IStateMachine
        {
            // The parent member.

            private ClusterMember member;

            // Time limit (SYS) marking the end of the period of waiting for
            // status broadcasts from other cluster members.  At the end
            // of this period, the instance will decide whether to promote
            // itself to master.

            private DateTime expireTime;

            // The set of known candidates keyed by cluster instance endpoint.

            private Dictionary<MsgEP, ClusterMemberStatus> candidates;

            /// <summary>
            /// Initializes the state processor, associating the cluster member.
            /// </summary>
            /// <param name="member">The cluster member.</param>
            public void Initialize(ClusterMember member)
            {
                ClusterMemberMsg    msg, msgClone;

                this.member     = member;
                this.candidates = new Dictionary<MsgEP, ClusterMemberStatus>();
                this.expireTime = SysTime.Now + member.settings.ElectionInterval;

                // An election has been called which means that the master
                // must have gone offline.  Update the cluster status to
                // reflect this.

                if (member.clusterStatus != null && member.clusterStatus.MasterEP != null)
                {
                    member.clusterStatus.Remove(member.clusterStatus.MasterEP);
                    member.clusterStatus.MasterEP = null;
                }

                // I'm going to broadcast the member status twice to give it
                // a good chance of getting through to all cluster members.
                // The first broadcast will be done immediately and then we'll
                // delay for a random period of time between 0 and 1000 milliseconds
                // and perform the second broadcast.

                msg      = new ClusterMemberMsg(member.instanceEP, ClusterMemberProtocolCaps.Current, ClusterMemberMsg.MemberStatusCmd, member.GetMemberStatus().ToArray());
                msgClone = (ClusterMemberMsg)msg.Clone();

                member.Broadcast(msg);
                Thread.Sleep(Helper.Rand(1000));
                member.Broadcast(msgClone);
            }

            /// <summary>
            /// Returns the lexically greatest <see cref="MsgEP" /> from the list of candidate
            /// status instances (or <c>null</c>).
            /// </summary>
            /// <param name="candidates">The candidate status instances.</param>
            /// <returns>The max EP or <c>null</c>.</returns>
            private MsgEP GetMaxEP(List<ClusterMemberStatus> candidates)
            {
                MsgEP maxEP;

                if (candidates.Count == 0)
                    return null;

                maxEP = candidates[0].InstanceEP;
                for (int i = 1; i < candidates.Count; i++)
                    if (MsgEP.Compare(maxEP, candidates[i].InstanceEP) < 0)
                        maxEP = candidates[i].InstanceEP;

                return maxEP;
            }

            /// <summary>
            /// Called periodically to handle background activities.
            /// </summary>
            public void BkTask()
            {
                if (SysTime.Now < expireTime)
                    return;

                // We've reached the end of the election period and
                // it's time to see if this instance has won.  How we
                // determine who one is based on the start modes of
                // this and the other instances as well as the lexical
                // comparison of the instance endpoints:
                //
                // Note that only instances started in Normal,
                // PreferMaster, and PreferSlave will be considered
                // as valid candidates.

                var     normalCandidates       = new List<ClusterMemberStatus>();
                var     preferMasterCandidates = new List<ClusterMemberStatus>();
                var     preferSlaveCandidates  = new List<ClusterMemberStatus>();
                MsgEP   winnerEP;

                foreach (ClusterMemberStatus candidate in candidates.Values)
                {
                    switch (candidate.Mode)
                    {
                        case ClusterMemberMode.Normal:

                            normalCandidates.Add(candidate);
                            break;

                        case ClusterMemberMode.PreferMaster:

                            preferMasterCandidates.Add(candidate);
                            break;

                        case ClusterMemberMode.PreferSlave:

                            preferSlaveCandidates.Add(candidate);
                            break;
                    }
                }

                winnerEP = GetMaxEP(preferMasterCandidates);
                if (winnerEP == null)
                {
                    winnerEP = GetMaxEP(normalCandidates);
                    if (winnerEP == null)
                        winnerEP = GetMaxEP(preferSlaveCandidates);
                }

                // Transition to the MASTER state if we won,
                // the SLAVE state if we didn't.

                if (member.instanceEP.Equals(winnerEP))
                {
                    // This instance has been elected as the new MASTER.
                    // Initialize the cluster state with the candidate
                    // information gathered during the election and
                    // then transition to the MASTER state.

                    var clusterStatus = new ClusterStatus(member.instanceEP);

                    clusterStatus.Members.Clear();
                    foreach (ClusterMemberStatus candidate in candidates.Values)
                        clusterStatus.Members.Add(candidate);

                    member.clusterStatus = clusterStatus;
                    member.SetState(ClusterMemberState.Master);
                }
                else
                {
                    // We lost, so transition to the slave state.

                    member.SetState(ClusterMemberState.Slave);
                }
            }

            /// <summary>
            /// Called when a cluster member protocol message is received.
            /// </summary>
            /// <param name="msg">The received message.</param>
            public void OnMessage(ClusterMemberMsg msg)
            {
                switch (msg.Command.ToLowerInvariant())
                {
                    case ClusterMemberMsg.ClusterStatusCmd:

                        // It appears that another instance has promoted itself to
                        // master or perhaps the original master finally got around
                        // to broadcasting the cluster status.  We'll go with the
                        // flow and transition into the slave state.

                        member.SetState(ClusterMemberState.Slave);
                        break;

                    case ClusterMemberMsg.MemberStatusCmd:

                        // We've received status from another cluster member so
                        // add it to the candidates table.

                        ClusterMemberStatus memberStatus;

                        memberStatus = new ClusterMemberStatus(msg._Data);
                        if (memberStatus.State == ClusterMemberState.Master)
                        {
                            // Hey this guy thinks he's the master, so 
                            // transition back to the SLAVE state and
                            // forget the election.

                            member.SetState(ClusterMemberState.Slave);
                            return;
                        }

                        candidates[memberStatus.InstanceEP] = memberStatus;
                        break;
                }
            }

            /// <summary>
            /// Schedules the immediate transmission of any status information 
            /// appropriate for the current state to the cluster.
            /// </summary>
            public void TransmitStatusNow()
            {
            }

            /// <summary>
            /// Called when simulating the network failing or coming back online.
            /// </summary>
            /// <param name="fail"><c>true</c> if network is failed, <c>false</c> if it's back online.</param>
            public void NetworkFailure(bool fail)
            {
            }
        }

        /// <summary>
        /// Used for tracking state information while the member is in the 
        /// <see cref="ClusterMemberState.Slave" /> state.
        /// </summary>
        private class SlaveStateMachine : IStateMachine
        {
            private ClusterMember   member;             // The parent member.
            private bool            priority;           // True if the next status update has high priority
            private DateTime        nextStatusUpdate;   // Scheduled time for the next status
                                                        // transmission to the master (SYS)
            private DateTime        masterUpdateLimit;  // Max time to wait for the master
                                                        // status update (SYS)

            /// <summary>
            /// Initializes the state processor, associating the cluster member.
            /// </summary>
            /// <param name="member">The cluster member.</param>
            public void Initialize(ClusterMember member)
            {
                ClusterMemberMsg msg;

                this.member            = member;
                this.priority          = false;
                this.nextStatusUpdate  = SysTime.Now + member.settings.SlaveUpdateInterval;
                this.masterUpdateLimit = SysTime.Now + member.settings.MissingMasterInterval;

                // Send the master the initial status update

                msg = new ClusterMemberMsg(member.instanceEP, ClusterMemberProtocolCaps.Current, ClusterMemberMsg.MemberStatusCmd, member.GetMemberStatus().ToArray());
                member.SendToMaster(msg);
            }

            /// <summary>
            /// Called periodically to handle background activities.
            /// </summary>
            public void BkTask()
            {
                var now = SysTime.Now;

                if (now >= nextStatusUpdate)
                {
                    // It's time to send status to the master.

                    if (member.clusterStatus != null)
                    {
                        var msg = new ClusterMemberMsg(member.instanceEP, ClusterMemberProtocolCaps.Current, ClusterMemberMsg.MemberStatusCmd, member.GetMemberStatus().ToArray());

                        if (priority)
                            msg.Flags |= ClusterMemberMsgFlag.Priority;

                        member.SendToMaster(msg);
                    }

                    this.priority         = false;
                    this.nextStatusUpdate = now + member.settings.SlaveUpdateInterval;
                }

                if (now >= masterUpdateLimit)
                {
                    // It appears that the master has gone offline so broadcast
                    // an election call to the cluster and transition to the
                    // ELECTION state.

                    member.Broadcast(new ClusterMemberMsg(member.instanceEP, ClusterMemberMsg.ElectionCmd));
                    member.SetState(ClusterMemberState.Election);
                }
            }

            /// <summary>
            /// Called when a cluster member protocol message is received.
            /// </summary>
            /// <param name="msg">The received message.</param>
            public void OnMessage(ClusterMemberMsg msg)
            {
                switch (msg.Command.ToLowerInvariant())
                {
                    case ClusterMemberMsg.ClusterStatusCmd:

                        // Update the member's copy of the cluster status, raise the
                        // ClusterStatusUpdate event so the application can do something
                        // custom with this, and then reset the master update limit timer.

                        member.clusterStatus = new ClusterStatus(msg._Data);
                        member.clusterProperties = member.clusterStatus.CloneProperties();

                        if (member.ClusterStatusUpdate != null)
                            member.ClusterStatusUpdate(member, new ClusterMemberEventArgs());

                        // Verify that this instance's status is present in the global
                        // status.  If this is not the case, then transmit the status now.

                        if (member.clusterStatus.GetMemberStatus(member.instanceEP) == null)
                            member.SendToMaster(new ClusterMemberMsg(member.instanceEP, ClusterMemberProtocolCaps.Current, ClusterMemberMsg.MemberStatusCmd, member.GetMemberStatus().ToArray()));

                        // If the slave's startup mode indicated that the instance preferred to
                        // be the master and the current master did not indicate a preference
                        // to be the master then call a master election in the hope of winning 
                        // and becoming the master.

                        if (member.mode == ClusterMemberMode.PreferMaster &&
                            (member.clusterStatus.MasterStatus.Mode == ClusterMemberMode.Normal || member.clusterStatus.MasterStatus.Mode == ClusterMemberMode.PreferSlave))
                        {
                            member.Trace(1, "Slave calling election due to [PreferMaster]");
                            member.Broadcast(new ClusterMemberMsg(member.instanceEP, ClusterMemberMsg.ElectionCmd));
                            member.SetState(ClusterMemberState.Election);
                            return;
                        }

                        // Reset the limit timer

                        masterUpdateLimit = SysTime.Now + member.settings.MissingMasterInterval;
                        break;

                    case ClusterMemberMsg.ElectionCmd:

                        member.SetState(ClusterMemberState.Election);
                        break;

                    case ClusterMemberMsg.PromoteCmd:

                        ClusterMemberStatus memberStatus;

                        // This instance is being promoted to master.  We'll need to update
                        // the cached copy of the cluster state by removing the old master's
                        // state and then updating this instance's state to indicate that
                        // it is the new master.

                        if (member.clusterStatus.MasterEP != null)
                            member.clusterStatus.Remove(member.clusterStatus.MasterEP);

                        member.clusterStatus.Remove(member.instanceEP);

                        memberStatus = member.GetMemberStatus();
                        memberStatus.State = ClusterMemberState.Master;     // Need to force this since the actual state
                        member.clusterStatus.Update(memberStatus);          // transition won't happen until later

                        member.clusterStatus.MasterEP = member.InstanceEP;

                        member.SetState(ClusterMemberState.Master);
                        break;
                }
            }

            /// <summary>
            /// Schedules the immediate transmission of any status information 
            /// appropriate for the current state to the cluster.
            /// </summary>
            public void TransmitStatusNow()
            {
                priority         = true;
                nextStatusUpdate = SysTime.Now;
            }

            /// <summary>
            /// Called when simulating the network failing or coming back online.
            /// </summary>
            /// <param name="fail"><c>true</c> if network is failed, <c>false</c> if it's back online.</param>
            public void NetworkFailure(bool fail)
            {
                if (!fail)
                {
                    masterUpdateLimit = SysTime.Now + member.settings.MissingMasterInterval;
                    nextStatusUpdate = SysTime.Now;    // Schedule an immediate member-status broadcast
                    // when the network comes back online
                }
            }
        }

        /// <summary>
        /// Used for tracking state information while the member is in the 
        /// <see cref="ClusterMemberState.Master" /> state.
        /// </summary>
        private class MasterStateMachine : IStateMachine
        {
            private ClusterMember   member;         // The parent member.
            private DateTime        nextBroadcast;  // Time for the next cluster status
                                                    // broadcast (SYS)

            /// <summary>
            /// Initializes the state processor, associating the cluster member.
            /// </summary>
            /// <param name="member">The cluster member.</param>
            public void Initialize(ClusterMember member)
            {
                this.member = member;

                // Perform the initial cluster status broadcast

                member.Broadcast(new ClusterMemberMsg(member.instanceEP, ClusterMemberProtocolCaps.Current, ClusterMemberMsg.ClusterStatusCmd, member.GetMasterClusterStatus().ToArray()));

                // I'm scheduling the next broadcast for the lesser of the normal broadcast interval
                // and 10 seconds.  The idea here is that the first broadcast may not have status for
                // all of the cluster members.  The missing members will immediately send member-updates to the
                // master when they notice this.  The 10 second max broadcast time will ensure that any
                // missing members will be quickly broadcast as part of the cluster status.

                nextBroadcast = SysTime.Now + Helper.Min(member.settings.MasterBroadcastInterval, TimeSpan.FromSeconds(10));
            }

            /// <summary>
            /// Called periodically to handle background activities.
            /// </summary>
            public void BkTask()
            {
                // Remove any cluster member status records (except for the master)
                // that have not been updated recently.

                var now           = SysTime.Now;
                var delList       = new List<int>();
                var clusterStatus = member.GetMasterClusterStatus();

                for (int i = 0; i < clusterStatus.Members.Count; i++)
                {
                    if (clusterStatus.Members[i].State != ClusterMemberState.Master &&
                        now - clusterStatus.Members[i].ReceiveTime >= member.settings.MissingSlaveInterval)
                    {
                        delList.Add(i);
                    }
                }

                for (int i = delList.Count - 1; i >= 0; i--)
                    member.clusterStatus.Members.RemoveAt(delList[i]);

                // Broadcast a cluster status update immediately if any members
                // were removed, otherwise check to see if we reached the
                // scheduled broadcast time.

                if (delList.Count > 0 || now >= nextBroadcast)
                {
                    member.Broadcast(new ClusterMemberMsg(member.instanceEP, ClusterMemberProtocolCaps.Current, ClusterMemberMsg.ClusterStatusCmd, member.GetMasterClusterStatus().ToArray()));
                    nextBroadcast = SysTime.Now + member.settings.MasterBroadcastInterval;
                }
            }

            /// <summary>
            /// Called when a cluster member protocol message is received.
            /// </summary>
            /// <param name="msg">The received message.</param>
            public void OnMessage(ClusterMemberMsg msg)
            {
                switch (msg.Command.ToLowerInvariant())
                {
                    case ClusterMemberMsg.MemberStatusCmd:

                        // Update the status information for the sender.

                        var memberStatus = new ClusterMemberStatus(msg._Data);

                        if (memberStatus.State == ClusterMemberState.Stopped)
                        {
                            member.clusterStatus.Remove(memberStatus);
                            TransmitStatusNow();    // Notify the cluster of stopped instances ASAP.
                        }
                        else
                        {
                            bool newMember = member.clusterStatus.GetMemberStatus(memberStatus.InstanceEP) != null;

                            member.clusterStatus.Update(memberStatus);

                            if (newMember || memberStatus.State == ClusterMemberState.Warmup)
                            {
                                // If the master's startup mode indicated that the instance preferred to
                                // be a slave and the new instance did not indicate a preference
                                // to be a slave then call a master election in the hope transferring the 
                                // responsibilities to the new instance.

                                if (member.mode == ClusterMemberMode.PreferSlave &&
                                    (memberStatus.Mode == ClusterMemberMode.Normal || memberStatus.Mode == ClusterMemberMode.PreferMaster))
                                {
                                    member.Trace(1, "Master calling election due to [PreferSlave]");
                                    member.Broadcast(new ClusterMemberMsg(member.instanceEP, ClusterMemberMsg.ElectionCmd));
                                    member.SetState(ClusterMemberState.Election);
                                    return;
                                }

                                // If this is a new cluster member or if it's still in warmup state then 
                                // send it the current cluster status.

                                member.SendTo(memberStatus.InstanceEP,
                                              new ClusterMemberMsg(member.instanceEP,
                                                                   ClusterMemberProtocolCaps.Current,
                                                                   ClusterMemberMsg.ClusterStatusCmd,
                                                                   member.GetMasterClusterStatus().ToArray()));
                            }
                        }

                        if ((msg.Flags & ClusterMemberMsgFlag.Priority) != 0)
                            TransmitStatusNow();    // Schedule an immediate update broadcast for priority changes

                        break;

                    case ClusterMemberMsg.ClusterStatusCmd:

                        // If the message is not from this instance then we've detected
                        // another master.  We need to call an election in this case.

                        if (!msg.SenderEP.Equals(member.instanceEP))
                        {
                            member.Trace(0, string.Format("Mutiple Masters [other MasterEP={0}]", msg.SenderEP));
                            member.Broadcast(new ClusterMemberMsg(member.instanceEP, ClusterMemberMsg.ElectionCmd));
                            member.SetState(ClusterMemberState.Election);
                        }

                        // Update the cluster wide properties and then call the
                        // ClusterStatusUpdate event so the application can do something
                        // custom with this.

                        var clusterStatus = new ClusterStatus(msg._Data);

                        member.clusterProperties = clusterStatus.CloneProperties();

                        if (member.ClusterStatusUpdate != null)
                            member.ClusterStatusUpdate(member, new ClusterMemberEventArgs());

                        break;

                    case ClusterMemberMsg.ElectionCmd:

                        member.SetState(ClusterMemberState.Election);
                        break;
                }
            }

            /// <summary>
            /// Schedules the immediate transmission of any status information 
            /// appropriate for the current state to the cluster.
            /// </summary>
            public void TransmitStatusNow()
            {
                nextBroadcast = SysTime.Now;
            }

            /// <summary>
            /// Called when simulating the network failing or coming back online.
            /// </summary>
            /// <param name="fail"><c>true</c> if network is failed, <c>false</c> if it's back online.</param>
            public void NetworkFailure(bool fail)
            {
                if (!fail)
                {
                    nextBroadcast = SysTime.Now;    // Schedule an immediate cluster-status broadcast
                                                    // when the network comes back online
                }
            }
        }

        /// <summary>
        /// Used for tracking state information while the member is in the 
        /// <see cref="ClusterMemberState.Observer" /> state.
        /// </summary>
        private class ObserverStateMachine : IStateMachine
        {
            private ClusterMember   member;             // The parent member.
            private bool            priority;           // True if the next status update has high priority
            private DateTime        nextStatusUpdate;   // Scheduled time for the next status
                                                        // transmission to the master (SYS)

            /// <summary>
            /// Initializes the state processor, associating the cluster member.
            /// </summary>
            /// <param name="member">The cluster member.</param>
            public void Initialize(ClusterMember member)
            {
                ClusterMemberMsg msg;

                this.member           = member;
                this.priority         = false;
                this.nextStatusUpdate = SysTime.Now + member.settings.SlaveUpdateInterval;

                // Send the master the initial status update

                msg = new ClusterMemberMsg(member.instanceEP, ClusterMemberProtocolCaps.Current, ClusterMemberMsg.MemberStatusCmd, member.GetMemberStatus().ToArray());
                member.SendToMaster(msg);
            }

            /// <summary>
            /// Called periodically to handle background activities.
            /// </summary>
            public void BkTask()
            {
                var now = SysTime.Now;

                if (now >= nextStatusUpdate)
                {
                    // It's time to send status to the master.

                    var msg = new ClusterMemberMsg(member.instanceEP, ClusterMemberProtocolCaps.Current, ClusterMemberMsg.MemberStatusCmd, member.GetMemberStatus().ToArray());

                    if (priority)
                        msg.Flags |= ClusterMemberMsgFlag.Priority;

                    member.SendToMaster(msg);

                    this.priority         = false;
                    this.nextStatusUpdate = now + member.settings.SlaveUpdateInterval;
                }
            }

            /// <summary>
            /// Called when a cluster member protocol message is received.
            /// </summary>
            /// <param name="msg">The received message.</param>
            public void OnMessage(ClusterMemberMsg msg)
            {
                switch (msg.Command.ToLowerInvariant())
                {
                    case ClusterMemberMsg.ClusterStatusCmd:

                        // Update the member's copy of the cluster status, raise the
                        // ClusterStatusUpdate event so the application can do something
                        // custom with this.

                        member.clusterStatus     = new ClusterStatus(msg._Data);
                        member.clusterProperties = member.clusterStatus.CloneProperties();

                        if (member.ClusterStatusUpdate != null)
                            member.ClusterStatusUpdate(member, new ClusterMemberEventArgs());

                        // Verify that this instance's status is present in the global
                        // status.  If this is not the case, then transmit the status now.

                        if (member.clusterStatus.GetMemberStatus(member.instanceEP) == null)
                            member.SendToMaster(new ClusterMemberMsg(member.instanceEP, ClusterMemberProtocolCaps.Current, ClusterMemberMsg.MemberStatusCmd, member.GetMemberStatus().ToArray()));

                        break;
                }
            }

            /// <summary>
            /// Schedules the immediate transmission of any status information 
            /// appropriate for the current state to the cluster.
            /// </summary>
            public void TransmitStatusNow()
            {
                priority = true;
                nextStatusUpdate = SysTime.Now;
            }

            /// <summary>
            /// Called when simulating the network failing or coming back online.
            /// </summary>
            /// <param name="fail"><c>true</c> if network is failed, <c>false</c> if it's back online.</param>
            public void NetworkFailure(bool fail)
            {
                if (!fail)
                {
                    nextStatusUpdate = SysTime.Now;     // Schedule an immediate member-status broadcast
                                                        // when the network comes back online
                }
            }
        }

        /// <summary>
        /// Used for tracking state information while the member is in the 
        /// <see cref="ClusterMemberState.Monitor" /> state.
        /// </summary>
        private class MonitorStateMachine : IStateMachine
        {
            private ClusterMember member;             // The parent member.

            /// <summary>
            /// Initializes the state processor, associating the cluster member.
            /// </summary>
            /// <param name="member">The cluster member.</param>
            public void Initialize(ClusterMember member)
            {
                this.member = member;
            }

            /// <summary>
            /// Called periodically to handle background activities.
            /// </summary>
            public void BkTask()
            {
            }

            /// <summary>
            /// Called when a cluster member protocol message is received.
            /// </summary>
            /// <param name="msg">The received message.</param>
            public void OnMessage(ClusterMemberMsg msg)
            {
                switch (msg.Command.ToLowerInvariant())
                {
                    case ClusterMemberMsg.ClusterStatusCmd:

                        // Update the member's copy of the cluster status, raise the
                        // ClusterStatusUpdate event so the application can do something
                        // custom with this.

                        member.clusterStatus     = new ClusterStatus(msg._Data);
                        member.clusterProperties = member.clusterStatus.CloneProperties();

                        if (member.ClusterStatusUpdate != null)
                            member.ClusterStatusUpdate(member, new ClusterMemberEventArgs());

                        break;
                }
            }

            /// <summary>
            /// Schedules the immediate transmission of any status information 
            /// appropriate for the current state to the cluster.
            /// </summary>
            public void TransmitStatusNow()
            {
            }

            /// <summary>
            /// Called when simulating the network failing or coming back online.
            /// </summary>
            /// <param name="fail"><c>true</c> if network is failed, <c>false</c> if it's back online.</param>
            public void NetworkFailure(bool fail)
            {
            }
        }

        //---------------------------------------------------------------------
        // Implementation

        /// <summary>
        /// The trace subsystem name for the <see cref="ClusterMember" /> and related classes.
        /// </summary>
        public const string     TraceSubsystem = "Messaging.ClusterMember";

        private const string    NotStartedMsg        = "Cluster member has not been started.";
        private const string    LeadingUnderscoreMsg = "Application property names cannot have leading underscores.";

        private MsgRouter                   router;             // The associated message router
        private ClusterMemberMode           mode;               // The startup mode
        private object                      syncLock;           // Thread synchronization instance
        private ClusterMemberState          state;              // The current instance state
        private Guid                        instanceID;         // The instance GUID
        private MsgEP                       instanceEP;         // This instance's logical endpoint
        private MsgEP                       clusterEP;          // The cluster's broadcast endpoint
        private ClusterStatus               clusterStatus;      // The current cluster status information
        private GatedTimer                  bkTimer;            // Background task timer
        private DateTime                    nextTaskTime;       // Next scheduled master or slave task time (SYS)
        private IStateMachine               stateMachine;       // State machine for the current member state (or null)
        private bool                        paused;             // True to disable message transmission & reception for UNIT testing
        internal ClusterMemberSettings      settings;           // The cluster settings
        internal Dictionary<string, string> properties;         // Instance application properties
        internal Dictionary<string, string> clusterProperties;  // The cluster properties

        /// <summary>
        /// Raised when the instance's <see cref="State" /> changes.  See
        /// <see cref="ClusterMemberState" /> for the list of possible states.
        /// </summary>
        public event ClusterMemberEventHandler StateChange;

        /// <summary>
        /// Raised periodically when the instance is in the <see ref="ClusterMemberState.Master" />
        /// state to provide a mechanism for applications to perform periodic background tasks.
        /// </summary>
        public event ClusterMemberEventHandler MasterTask;

        /// <summary>
        /// Raised periodically when the instance is in the <see ref="ClusterMemberState.Slave" />
        /// or <see cref="ClusterMemberState.Election" /> states to provide a mechanism for 
        /// applications to perform periodic background tasks.
        /// </summary>
        public event ClusterMemberEventHandler SlaveTask;

        /// <summary>
        /// Raised periodically when a cluster status update is received from the master.
        /// This event is raised only when the member is in the <see ref="ClusterMemberState.Master" />
        /// or <see ref="ClusterMemberState.Slave" /> states.
        /// </summary>
        public event ClusterMemberEventHandler ClusterStatusUpdate;

        /// <summary>
        /// Called just before the instance is going to transmit a <see cref="ClusterMemberStatus" />
        /// or a <see cref="ClusterStatus" /> to one or all of the cluster members.
        /// </summary>
        /// <remarks>
        /// This event gives application a chance to modify custom instance properties to be included
        /// in the transmission or if the this instance is the <see cref="ClusterMemberState.Master" />,
        /// the chance to modify cluster wide properties.
        /// </remarks>
        public event ClusterMemberEventHandler StatusTransmission;

        /// <summary>
        /// Creates an instance using the settings passed.
        /// </summary>
        /// <param name="router">The message router to associate with this instance.</param>
        /// <param name="settings">The <see cref="ClusterMemberSettings" />.</param>
        public ClusterMember(MsgRouter router, ClusterMemberSettings settings)
        {
            if (settings.ClusterBaseEP == null)
                throw new NullReferenceException("[ClusterMemberSettings.ClusterBaseEP] must be initialized.");

            if (settings.Mode == ClusterMemberMode.Unknown)
                throw new ArgumentException("[ClusterMemberSettings.Mode] cannot be set to [Unknown.]");

            this.router            = router;
            this.syncLock          = router.SyncRoot;
            this.state             = ClusterMemberState.Stopped;
            this.settings          = settings;
            this.mode              = settings.Mode;
            this.properties        = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            this.clusterProperties = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            this.paused            = false;
        }

        /// <summary>
        /// Creates an instance loading <see cref="ClusterMemberSettings" /> from
        /// the application configuration.
        /// </summary>
        /// <param name="router">The message router to associate with this instance.</param>
        /// <param name="keyPrefix">The configuration key prefix.</param>
        public ClusterMember(MsgRouter router, string keyPrefix)
            : this(router, ClusterMemberSettings.LoadConfig(keyPrefix))
        {
        }

        /// <summary>
        /// Returns the instance's globally unique ID if the instance has been
        /// started, <see cref="Guid.Empty" /> otherwise.
        /// </summary>
        public Guid InstanceID
        {
            get { return state != ClusterMemberState.Stopped ? instanceID : Guid.Empty; }
        }

        /// <summary>
        /// Returns the cluster's base endpoint.
        /// </summary>
        public MsgEP ClusterBaseEP
        {
            get { return settings.ClusterBaseEP; }
        }

        /// <summary>
        /// Returns the cluster's broadcast endpoint.
        /// </summary>
        public MsgEP ClusterEP
        {
            get { return clusterEP; }
        }

        /// <summary>
        /// Returns the instance's logical cluster endpoint.
        /// </summary>
        public MsgEP InstanceEP
        {
            get { return instanceEP; }
        }

        /// <summary>
        /// Returns the current cluster master instance endpoint or <c>null</c> if
        /// there is no known master.
        /// </summary>
        public MsgEP MasterEP
        {
            get
            {
                if (clusterStatus == null)
                    return null;

                return clusterStatus.MasterEP;
            }
        }

        /// <summary>
        /// Returns the cluster member's startup mode.
        /// </summary>
        public ClusterMemberMode Mode
        {
            get { return mode; }
        }

        /// <summary>
        /// Returns the current instance state.  Note that for unit tests,
        /// you may also change the instance state using the setter.
        /// </summary>
        public ClusterMemberState State
        {
            get { return state; }
            internal set { SetState(value); }
        }

        /// <summary>
        /// Set to <c>true</c> to simulate a network failure by disabling all message
        /// processing by the instance.  This is available only for unit testing.
        /// </summary>
        internal bool Paused
        {
            get { return paused; }

            set 
            {
                using (TimedLock.Lock(syncLock)) 
                {
                    if (paused == value)
                        return;

                    paused = value;
                    if (stateMachine != null)
                        stateMachine.NetworkFailure(paused);
                }
            }
        }

        /// <summary>
        /// Returns a shallow clone of the current cluster status or <c>null</c> if 
        /// no status update has yet been received.
        /// </summary>
        /// <remarks>
        /// <para>
        /// Note that a clone of the status is returned for thread safety.
        /// The calling thread is free to use the result any way it wishes
        /// but any changes made will not impact the cluster and any status
        /// updates received after this property returns will not be reflected in 
        /// the returned value.
        /// </para>
        /// <para>
        /// Applications that need to perform multiple operations against
        /// the cluster status should copy the property result to a local
        /// variable and perform the requests against the variable to
        /// avoid the overhead of cloning the status for each access.
        /// </para>
        /// </remarks>
        public ClusterStatus ClusterStatus
        {
            get
            {
                if (clusterStatus == null)
                    return null;

                return clusterStatus.Clone();
            }
        }

        /// <summary>
        /// Returns <c>true</c> if the member has cluster status information.
        /// </summary>
        public bool HasClusterStatus
        {
            get { return clusterStatus != null; }
        }

        /// <summary>
        /// Returns <c>true</c> if this instance is the cluster master.
        /// </summary>
        public bool IsMaster
        {
            get { return state == ClusterMemberState.Master; }
        }

        /// <summary>
        /// Returns <c>true</c> if the member was started in one of the passive modes:
        /// <see cref="ClusterMemberMode.Observer" /> or <see cref="ClusterMemberMode.Monitor" />.
        /// </summary>
        public bool IsPassive
        {
            get { return state == ClusterMemberState.Observer || state == ClusterMemberState.Monitor; }
        }

        /// <summary>
        /// Returns the synchronized cluster time (UTC).
        /// </summary>
        /// <remarks>
        /// <para>
        /// This property provides a mechanism for keeping a reasonably synchronized
        /// common time base across a set of cluster instances.  The cluster master
        /// includes its current time (UTC) in the cluster status broadcasts.  Client
        /// instances calculate the current cluster time by adding the interval since
        /// the last status broadcast to the cluster time in that broadcast.  Instances
        /// that are still in the <see cref="ClusterMemberState.Warmup" /> state will
        /// return the local machine UTC time.
        /// </para>
        /// <note>
        /// Applications using this time need to recognize that the cluster
        /// time will not always monatomically increase over time.  This can happen
        /// when there are significant differences in local server time settings and
        /// a new cluster master is elected.
        /// </note>
        /// </remarks>
        public DateTime ClusterTime
        {
            get
            {
                using (TimedLock.Lock(syncLock))
                {
                    if ((state == ClusterMemberState.Master || state == ClusterMemberState.Slave) && clusterStatus != null)
                    {
                        return clusterStatus.ClusterTime + (DateTime.UtcNow - clusterStatus.ReceiveTime);
                    }

                    return DateTime.UtcNow;
                }
            }
        }

        /// <summary>
        /// Returns the object to be used to synchronize multi-threaded access to 
        /// instances of this class.
        /// </summary>
        public object SyncRoot
        {
            get { return syncLock; }
        }

        /// <summary>
        /// Writes information to the <see cref="NetTrace" />, adding some member
        /// state information.
        /// </summary>
        /// <param name="detail">The detail level 0..255 (higher values indicate more detail).</param>
        /// <param name="summary">The summary string.</param>
        /// <param name="details">The trace details (or <c>null</c>).</param>
        [Conditional("TRACE")]
        internal void Trace(int detail, string summary, string details)
        {
            const string headerFmt = @"
ep      = {0}
state   = {1}
members = {2}
----------
";
            string header;

            if (clusterStatus == null)
                header = string.Format(headerFmt, instanceEP, state, "na");
            else
                header = string.Format(headerFmt, instanceEP, state, clusterStatus.Members.Count);

            if (details == null)
                details = string.Empty;

            NetTrace.Write(TraceSubsystem, detail, string.Format("Cluster: [state={0} ep={1}]", state, instanceEP), summary, header + details);
        }

        /// <summary>
        /// Writes information to the <see cref="NetTrace" />, adding some member
        /// state information.
        /// </summary>
        /// <param name="detail">The detail level 0..255 (higher values indicate more detail).</param>
        /// <param name="summary">The summary string.</param>
        [Conditional("TRACE")]
        internal void Trace(int detail, string summary)
        {
            Trace(detail, summary, null);
        }

        /// <summary>
        /// Adds the target object's message endpoints to the associated <see cref="MsgRouter" />'s
        /// <see cref="MsgDispatcher" />, substituting any message handler's whose dynamic scope
        /// matches the <paramref name="dynamicScope" /> parameter with this cluster instance's unique 
        /// endpoint.
        /// </summary>
        /// <param name="target">The target object.</param>
        /// <param name="dynamicScope">The dynamic scope name.</param>
        /// <remarks>
        /// <note><see cref="Start" /> must be called first before calling this method.</note>
        /// <note>
        /// Logical message handlers will be grouped with the cluster member's message handlers
        /// in the physical routing table.
        /// </note>
        /// <para>
        /// This provides a good way to expose application message handlers that listen on the
        /// cluster member's globally unique endpoint.  Here's a code fragment demonstrating
        /// how this would work:
        /// </para>
        /// <code language="cs">
        /// public class MyApplication {
        /// 
        ///     MsgRouter       router;
        ///     ClusterMember   cluster;
        /// 
        ///     public void Start() {
        /// 
        ///         router.Start();
        ///         cluster.Start();
        ///         cluster.AddTarget(this,"MyAppScope");
        ///     }
        /// 
        ///     [MsgHandler(LogicalEP=MsgEP.Null,DynamicScope="MyAppScope")]
        ///     public void OnMsg(MyMsg msg) {
        /// 
        ///     }
        /// }
        /// </code>
        /// <note>
        /// The <b>LogicalEP</b> parameter in the <b>MsgHandler</b> is ignored
        /// and can be set to anything.  This will be replaced with the cluster member's
        /// unique logical endpoint by this method. 
        /// </note>
        /// </remarks>
        /// <exception cref="InvalidOperationException">Thrown if <see cref="Start" /> has not already been called.</exception>
        public void AddTarget(object target, string dynamicScope)
        {
            using (TimedLock.Lock(syncLock))
            {
                if (state == ClusterMemberState.Stopped)
                    throw new InvalidOperationException("ClusterMember.Start() must be called before AddTarget().");

                router.Dispatcher.AddTarget(target, dynamicScope, this, this);
            }
        }

        /// <summary>
        /// Starts the cluster member instance.
        /// </summary>
        public void Start()
        {
            using (TimedLock.Lock(syncLock))
            {
                if (state != ClusterMemberState.Stopped)
                    throw new InvalidOperationException("Cluster member already started.");

                this.instanceID    = Helper.NewGuid();
                this.instanceEP    = settings.ClusterBaseEP + "/" + instanceID.ToString();
                this.clusterEP     = settings.ClusterBaseEP + "/*";
                this.state         = ClusterMemberState.Stopped;
                this.clusterStatus = null;
                this.bkTimer       = new GatedTimer(new TimerCallback(OnBkTask), null, settings.BkInterval);
                this.nextTaskTime =  SysTime.Now;
                this.stateMachine  = null;

                // Register the message handler and instance endpoint with router.

                router.Dispatcher.AddLogical(new MsgHandlerDelegate(OnMsg), instanceEP, typeof(ClusterMemberMsg), false, null);

                Trace(1, "Starting");

                switch (mode)
                {
                    case ClusterMemberMode.Normal:
                    case ClusterMemberMode.PreferMaster:
                    case ClusterMemberMode.PreferSlave:

                        SetState(ClusterMemberState.Warmup);
                        break;

                    case ClusterMemberMode.Observer:

                        SetState(ClusterMemberState.Observer);
                        break;

                    case ClusterMemberMode.Monitor:

                        SetState(ClusterMemberState.Monitor);
                        break;

                    default:

                        throw new NotImplementedException();
                }
            }
        }

        /// <summary>
        /// Broadcasts a message to the cluster indicating that the instance
        /// is stopping and then stops the instance.
        /// </summary>
        /// <remarks>
        /// <note>
        /// It is not an error to call this method if the instance
        /// has not been started.
        /// </note>
        /// </remarks>
        public void Stop()
        {
            using (TimedLock.Lock(syncLock))
            {
                if (bkTimer != null)
                {
                    bkTimer.Dispose();
                    bkTimer = null;
                }

                if (state == ClusterMemberState.Stopped)
                    return;

                Trace(1, "Stopping");
                stateMachine = null;   // Disable the state processor

                if (state == ClusterMemberState.Master)
                {
                    // The instance being stopped is the master then select one of the slaves 
                    // (if there is one) to be promoted to be the next master.  Otherwise, 
                    // broadcast an election call.  To provide a bit of determinism for
                    // unit testing, the slave promoted will have the lexically greatest
                    // instanceEP.

                    ClusterMemberStatus promotee = null;

                    for (int i = 0; i < clusterStatus.Members.Count; i++)
                        if (clusterStatus.Members[i].State == ClusterMemberState.Slave)
                        {
                            if (promotee == null || MsgEP.Compare(clusterStatus.Members[i].InstanceEP, promotee.InstanceEP) > 0)
                                promotee = clusterStatus.Members[i];
                        }

                    if (promotee != null)
                        SendTo(promotee.InstanceEP, new ClusterMemberMsg(instanceEP, ClusterMemberMsg.PromoteCmd));
                    else
                        Broadcast(new ClusterMemberMsg(instanceEP, ClusterMemberMsg.ElectionCmd));

                    SetState(ClusterMemberState.Stopped);
                }
                else
                {
                    // For all other states, transition to the 
                    // STOPPED state and send a status update to 
                    // the master so it can remove this instance
                    // from the cluster.

                    ClusterMemberStatus memberStatus;

                    memberStatus = GetMemberStatus();
                    memberStatus.State = ClusterMemberState.Stopped;

                    SendToMaster(new ClusterMemberMsg(instanceEP, ClusterMemberProtocolCaps.Current, ClusterMemberMsg.MemberStatusCmd, memberStatus.ToArray()));
                    SetState(ClusterMemberState.Stopped);
                }

                // Disassociate this instance with the router.

                router.Dispatcher.RemoveTarget(this);
            }
        }

        /// <summary>
        /// Returns <c>true</c> if the member is considered to be online.  Exactly how this is determined
        /// depends on the startup <see cref="ClusterMemberMode" /> used.
        /// </summary>
        /// <remarks>
        /// <para>
        /// Instances started as <see cref="ClusterMemberMode.Normal" />, <see cref="ClusterMemberMode.PreferMaster" />,
        /// or <see cref="ClusterMemberMode.PreferSlave" /> then the instance will be considered to be online
        /// when the master election has been completed) meaning that one of the instances has been
        /// elected as master) and that the instance has received the at least one cluster status broadcast.
        /// </para>
        /// <para>
        /// Instances started as <see cref="ClusterMemberMode.Observer" /> then this property always
        /// returns <c>true</c> and for <see cref="ClusterMemberMode.Monitor" /> then this property
        /// always returns <c>false</c>.
        /// </para>
        /// </remarks>
        public bool IsOnline
        {
            get
            {
                using (TimedLock.Lock(syncLock))
                {
                    switch (mode)
                    {
                        case ClusterMemberMode.Normal:
                        case ClusterMemberMode.PreferMaster:
                        case ClusterMemberMode.PreferSlave:

                            return (state == ClusterMemberState.Master || state == ClusterMemberState.Slave) &&
                                   clusterStatus != null &&
                                   clusterStatus.MasterEP != null;

                        case ClusterMemberMode.Observer:

                            return true;

                        case ClusterMemberMode.Monitor:

                            return false;

                        default:

                            return false;
                    }
                }
            }
        }

        /// <summary>
        /// Waits until the member has fully joined the cluster.
        /// </summary>
        public void JoinWait()
        {
            while (!IsOnline)
                Thread.Sleep(100);
        }

        /// <summary>
        /// Forces the immediate transmission of any status appropriate for the current
        /// state of the member to the cluster.
        /// </summary>
        /// <remarks>
        /// <para>
        /// This is useful for unit testing and also for indicating when cluster
        /// or cluster instance properties have been modified and the rest of the
        /// cluster should be be notified of the changes ASAP.
        /// </para>
        /// <para>
        /// If the member is in the <see cref="ClusterMemberState.Slave" /> state then
        /// this method schedules an immediate transmission of the local instance
        /// state to the master.  If the member is in the <see cref="ClusterMemberState.Master" />
        /// state then this method schedules the immediate broadcasting of the cluster
        /// status to the entire cluster.
        /// </para>
        /// <note>
        /// The uninspired use of this method may generate a significant
        /// amount of network traffic for large clusters.
        /// </note>
        /// </remarks>
        public void TransmitStatusNow()
        {
            using (TimedLock.Lock(syncLock))
            {
                if (stateMachine != null)
                    stateMachine.TransmitStatusNow();
            }
        }

        /// <summary>
        /// Broadcasts a message to the cluster.
        /// </summary>
        /// <param name="msg">The message to be sent.</param>
        public void Broadcast(Msg msg)
        {

            using (TimedLock.Lock(syncLock))
            {

                if (state == ClusterMemberState.Stopped)
                    throw new InvalidOperationException(NotStartedMsg);

                if (paused)
                    return;
#if TRACE
                var clusterMsg = msg as ClusterMemberMsg;

                if (clusterMsg != null)
                {

                    Trace(clusterMsg.Command == ClusterMemberMsg.ClusterStatusCmd ? 0 : 1,
                          string.Format("Broadcast: cmd={0}", clusterMsg.Command), clusterMsg.GetTrace());
                }
                else
                    Trace(1, string.Format("Broadcast: {0}", msg.GetType().Name));
#endif
                router.BroadcastTo(clusterEP, msg);
            }
        }

        /// <summary>
        /// Sends a message to the cluster master instance if one is known.
        /// </summary>
        /// <param name="msg">The message.</param>
        public void SendToMaster(Msg msg)
        {
            using (TimedLock.Lock(syncLock))
            {
                if (state == ClusterMemberState.Stopped)
                    throw new InvalidOperationException(NotStartedMsg);

                if (paused)
                    return;

                if (this.MasterEP != null)
                {
#if TRACE
                    var clusterMsg = msg as ClusterMemberMsg;

                    if (clusterMsg != null)
                        Trace(1, string.Format("Sending: cmd={0} [To=MASTER]", clusterMsg.Command, clusterMsg.GetTrace()));
                    else
                        Trace(1, string.Format("Sending: {0} [To=MASTER]", msg.GetType().Name));
#endif
                    router.SendTo(this.MasterEP, msg);
                }
            }
        }

        /// <summary>
        /// Sends a message to the specified member instance.
        /// </summary>
        /// <param name="instanceEP">The member endpoint.</param>
        /// <param name="msg">The message.</param>
        public void SendTo(MsgEP instanceEP, Msg msg)
        {
            using (TimedLock.Lock(syncLock))
            {
                if (state == ClusterMemberState.Stopped)
                    throw new InvalidOperationException(NotStartedMsg);

                if (paused)
                    return;
#if TRACE
                var clusterMsg = msg as ClusterMemberMsg;

                if (clusterMsg != null)
                    Trace(1, string.Format("Sending: cmd={0} [To={1}]", clusterMsg.Command, instanceEP, clusterMsg.GetTrace()));
                else
                    Trace(1, string.Format("Sending: {0} [To={1}]", msg.GetType().Name, instanceEP));
#endif
                router.SendTo(instanceEP, msg);
            }
        }

        /// <summary>
        /// Queries the cluster master if one exists, throws an immediate
        /// <see cref="TimeoutException" /> if there is no known cluster master.
        /// </summary>
        /// <param name="msg">The query message.</param>
        /// <returns>The response message.</returns>
        public Msg QueryMaster(Msg msg)
        {
            using (TimedLock.Lock(syncLock))
            {
                if (state == ClusterMemberState.Stopped)
                    throw new InvalidOperationException(NotStartedMsg);

                if (paused)
                    throw new TimeoutException("ClusterMember is paused.");

                if (this.MasterEP == null)
                    throw new TimeoutException("No known cluster master.");
#if TRACE
                var clusterMsg = msg as ClusterMemberMsg;

                if (clusterMsg != null)
                    Trace(1, string.Format("Query: cmd={0} [To=MASTER]", clusterMsg.Command), clusterMsg.GetTrace());
                else
                    Trace(1, string.Format("Query: {0} [To=MASTER]", msg.GetType().Name));
#endif
                return router.Query(this.MasterEP, msg);
            }
        }

        /// <summary>
        /// Sets the specified member state, firing <see cref="StateChange" />
        /// as necessary and also by performing the necessary initialization
        /// and cleanup required for the state transition.
        /// </summary>
        /// <param name="newState">The new member state.</param>
        private void SetState(ClusterMemberState newState)
        {
            if (this.state == newState)
                return;

            Trace(1, string.Format("{0} --> {1}", this.state, newState));

            ClusterMemberState      orgState;
            ClusterMemberEventArgs  args;

            orgState = this.state;
            if (stateMachine != null)
                stateMachine = null;

            this.state = newState;
            switch (newState)
            {
                case ClusterMemberState.Warmup:

                    stateMachine = new WarmupStateMachine();
                    break;

                case ClusterMemberState.Election:

                    stateMachine = new ElectionStateMachine();
                    break;

                case ClusterMemberState.Slave:

                    stateMachine = new SlaveStateMachine();
                    break;

                case ClusterMemberState.Master:

                    stateMachine = new MasterStateMachine();
                    break;

                case ClusterMemberState.Observer:

                    stateMachine = new ObserverStateMachine();
                    break;

                case ClusterMemberState.Monitor:

                    stateMachine = new MonitorStateMachine();
                    break;
            }

            if (stateMachine != null)
                stateMachine.Initialize(this);

            if (StateChange != null)
            {
                args               = new ClusterMemberEventArgs();
                args.OriginalState = orgState;
                args.NewState      = newState;

                StateChange(this, args);
            }
        }

        /// <summary>
        /// Raised the <see cref="StatusTransmission" /> event and then constructs
        /// a <see cref="ClusterMemberStatus" /> from the current member's state.
        /// </summary>
        /// <returns>A <see cref="ClusterMemberStatus" /> instance.</returns>
        private ClusterMemberStatus GetMemberStatus()
        {
            if (StatusTransmission != null)
                StatusTransmission(this, new ClusterMemberEventArgs());

            return new ClusterMemberStatus(this);
        }

        /// <summary>
        /// Used internally by master instances to get the cluster status
        /// including the current master status.
        /// </summary>
        /// <returns>A <see cref="ClusterStatus" /> instance.</returns>
        private ClusterStatus GetMasterClusterStatus()
        {
            Assertion.Test(state == ClusterMemberState.Master);

            if (clusterStatus == null)
                clusterStatus = new ClusterStatus(this.instanceEP);

            clusterStatus.ReceiveTime =
            clusterStatus.ClusterTime = DateTime.UtcNow;

            clusterStatus.LoadProperties(clusterProperties);
            clusterStatus.Update(this.GetMemberStatus());
            return clusterStatus;
        }

        /// <summary>
        /// Called periodically to handle member background tasks.
        /// </summary>
        /// <param name="na">Not used.</param>
        private void OnBkTask(object na)
        {
            using (TimedLock.Lock(syncLock))
            {
                // Give the current state processor a chance to handle
                // background tasks.

                if (stateMachine != null)
                    stateMachine.BkTask();

                // Handle application background tasks.

                if (SysTime.Now >= nextTaskTime)
                {
                    switch (state)
                    {
                        case ClusterMemberState.Master:

                            if (MasterTask != null)
                                MasterTask(this, new ClusterMemberEventArgs());

                            nextTaskTime = SysTime.Now + settings.MasterBkInterval;
                            break;

                        case ClusterMemberState.Warmup:
                        case ClusterMemberState.Slave:
                        case ClusterMemberState.Election:

                            if (SlaveTask != null)
                                SlaveTask(this, new ClusterMemberEventArgs());

                            nextTaskTime = SysTime.Now + settings.SlaveBkInterval;
                            break;
                    }
                }
            }
        }

        /// <summary>
        /// Handles cluster member protocol messages. 
        /// </summary>
        /// <param name="msg">the message received.</param>
        private void OnMsg(Msg msg)
        {
            ClusterMemberMsg clusterMsg;

            if (paused)     // Ignore received messages while simulating a
                return;     // network failure.

            try
            {
                clusterMsg = msg as ClusterMemberMsg;
                if (clusterMsg == null)
                    return;     // This shouldn't ever happen but we'll check
                                // just to be sure.
#if TRACE
                Trace(1, string.Format("Receive: {0} [SenderEP={1}]", clusterMsg.Command, clusterMsg.SenderEP), clusterMsg.GetTrace());
#endif
                using (TimedLock.Lock(syncLock))
                {
                    if (stateMachine == null)
                    {
                        Trace(1, string.Format("Discarding: {0}", clusterMsg.Command));
                        return;
                    }

                    stateMachine.OnMessage(clusterMsg);
                }
            }
            catch (Exception e)
            {
                NetTrace.Write(TraceSubsystem, 0, string.Format("[state={0} ep={1}] Exception", state, instanceEP), e);
                SysLog.LogException(e);
            }
        }

        //---------------------------------------------------------------------
        // Cluster-wide property related methods

        private const string GlobalMasterMsg     = "Global properties can only be modified while in the Master state.";
        private const string GlobalUnderScoreMsg = "Applications cannot use property names with leading underscores.";

        /// <summary>
        /// Adds or updates a cluster-wide named value.
        /// </summary>
        /// <param name="key">The property name.</param>
        /// <param name="value">The property value.</param>
        /// <remarks>
        /// <note>
        /// This method may be called by application only when the
        /// cluster member is in the <see cref="ClusterMemberState.Master" /> 
        /// state and also that property names beginning with leading underscores
        /// are reserved for internal use by this class.
        /// </note>
        /// </remarks>
        /// <exception cref="InvalidOperationException">Thrown if the member is not in the <see cref="ClusterMemberState.Master" /> state.</exception>
        /// <exception cref="ArgumentException">Thrown if the property name has a leading underscore.</exception>
        public void GlobalSet(string key, string value)
        {
            using (TimedLock.Lock(syncLock))
            {
                if (state != ClusterMemberState.Master)
                    throw new InvalidOperationException(GlobalMasterMsg);

                if (key.StartsWith("_"))
                    throw new ArgumentException(GlobalUnderScoreMsg, "key");

                clusterProperties[key] = value;
            }
        }

        /// <summary>
        /// Returns a cluster-wide property value.
        /// </summary>
        /// <param name="key">The property name.</param>
        /// <returns>The property value.</returns>
        /// <exception cref="KeyNotFoundException">Thrown if the property is not present.</exception>
        /// <exception cref="ArgumentException">Thrown if the property name has a leading underscore.</exception>
        public string GlobalGet(string key)
        {
            using (TimedLock.Lock(syncLock))
            {
                if (key.StartsWith("_"))
                    throw new ArgumentException(GlobalUnderScoreMsg, "key");

                return clusterProperties[key];
            }
        }

        /// <summary>
        /// Returns <c>true</c> if the cluster-wide properties include a specific key.
        /// </summary>
        /// <param name="key">The property name.</param>
        /// <returns><c>true</c> if the property exists.</returns>
        /// <exception cref="ArgumentException">Thrown if the property name has a leading underscore.</exception>
        public bool GlobalContainsKey(string key)
        {
            using (TimedLock.Lock(syncLock))
            {
                if (key.StartsWith("_"))
                    throw new ArgumentException(GlobalUnderScoreMsg, "key");

                return clusterProperties.ContainsKey(key);
            }
        }

        /// <summary>
        /// Attempts to return a named cluster-wide property value.
        /// </summary>
        /// <param name="key">The property name.</param>
        /// <param name="value">Returns as the property value or <c>null</c>.</param>
        /// <returns><c>true</c> if a property value was returned.</returns>
        /// <exception cref="ArgumentException">Thrown if the property name has a leading underscore.</exception>
        public bool GlobalTryGetValue(string key, out string value)
        {
            using (TimedLock.Lock(syncLock))
            {
                if (key.StartsWith("_"))
                    throw new ArgumentException(GlobalUnderScoreMsg, "key");

                return clusterProperties.TryGetValue(key, out value);
            }
        }

        /// <summary>
        /// Removes the named cluster wide property.
        /// </summary>
        /// <param name="key">The propert name.</param>
        /// <exception cref="InvalidOperationException">Thrown if the member is not in the <see cref="ClusterMemberState.Master" /> state.</exception>
        /// <exception cref="ArgumentException">Thrown if the property name has a leading underscore.</exception>
        public void GlobalRemove(string key)
        {
            using (TimedLock.Lock(syncLock))
            {
                if (state != ClusterMemberState.Master)
                    throw new InvalidOperationException(GlobalMasterMsg);

                if (key.StartsWith("_"))
                    throw new ArgumentException(GlobalUnderScoreMsg, "key");

                clusterProperties.Remove(key);
            }
        }

        /// <summary>
        /// Removes all cluster-wide properties.
        /// </summary>
        /// <exception cref="InvalidOperationException">Thrown if the member is not in the <see cref="ClusterMemberState.Master" /> state.</exception>
        public void GlobalClear()
        {
            using (TimedLock.Lock(syncLock))
            {
                if (state != ClusterMemberState.Master)
                    throw new InvalidOperationException(GlobalMasterMsg);

                clusterProperties.Clear();
            }
        }

        //---------------------------------------------------------------------
        // Cluster member instance property related methods

        /// <summary>
        /// Sets an application property, adding a new property or overwriting an existing 
        /// property as necessary.
        /// </summary>
        /// <param name="key">The property name.</param>
        /// <param name="value">The property value.</param>
        public void Set(string key, string value)
        {
            if (key.StartsWith("_"))
                throw new ArgumentException(LeadingUnderscoreMsg);

            using (TimedLock.Lock(syncLock))
            {
                properties[key] = value;
            }
        }

        /// <summary>
        /// Removes an application property.
        /// </summary>
        /// <param name="key">The property name.</param>
        public void Remove(string key)
        {
            if (key.StartsWith("_"))
                throw new ArgumentException(LeadingUnderscoreMsg);

            using (TimedLock.Lock(syncLock))
            {
                properties.Remove(key);
            }
        }

        /// <summary>
        /// Returns <c>true</c> if the requested application property exists.
        /// </summary>
        /// <param name="key">The property name.</param>
        /// <returns><c>true</c> if the property exists.</returns>
        public bool ContainsKey(string key)
        {
            if (key.StartsWith("_"))
                throw new ArgumentException(LeadingUnderscoreMsg);

            using (TimedLock.Lock(syncLock))
            {
                return properties.ContainsKey(key);
            }
        }

        /// <summary>
        /// Attempts to return a specific application property.
        /// </summary>
        /// <param name="key">The property name.</param>
        /// <param name="value">Returns as the property value (or <c>null</c>).</param>
        /// <returns><c>true</c> if the property exists and was returned.</returns>
        public bool TryGetValue(string key, out string value)
        {
            if (key.StartsWith("_"))
                throw new ArgumentException(LeadingUnderscoreMsg);

            using (TimedLock.Lock(syncLock))
            {
                return properties.TryGetValue(key, out value);
            }
        }

        /// <summary>
        /// Accesses the application defined properties.
        /// </summary>
        /// <param name="key">The property name.</param>
        /// <returns>The property value.</returns>
        public string this[string key]
        {
            get
            {
                if (key.StartsWith("_"))
                    throw new ArgumentException(LeadingUnderscoreMsg);

                using (TimedLock.Lock(syncLock))
                {
                    return properties[key];
                }
            }

            set
            {
                if (key.StartsWith("_"))
                    throw new ArgumentException(LeadingUnderscoreMsg);

                using (TimedLock.Lock(syncLock))
                {
                    properties[key] = value;
                }
            }
        }

        /// <summary>
        /// Removes all application defined properties.
        /// </summary>
        public void Clear()
        {
            using (TimedLock.Lock(syncLock))
            {
                properties.Clear();
            }
        }

        //---------------------------------------------------------------------
        // IDynamicEPMangler implementation

        /// <summary>
        /// Dynamically modifies a message handler's endpoint just before it is registered
        /// with a <see cref="MsgRouter" />'s <see cref="IMsgDispatcher" />.
        /// </summary>
        /// <param name="logicalEP">The message handler's logical endpoint.</param>
        /// <param name="handler">The message handler information.</param>
        /// <returns>The logical endpoint to actually register for the message handler.</returns>
        public MsgEP Munge(MsgEP logicalEP, MsgHandler handler)
        {
            if (!logicalEP.IsLogical)
                throw new ArgumentException(TopologyHelper.LogicalEPMsg, "logicalEP");

            return instanceEP;
        }
    }
}
