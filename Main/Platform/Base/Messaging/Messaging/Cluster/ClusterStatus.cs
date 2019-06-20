//-----------------------------------------------------------------------------
// FILE:        ClusterStatus.cs
// CONTRIBUTOR: Jeff Lill
// COPYRIGHT:   Copyright (c) 2005-2015 by Jeffrey Lill.  All rights reserved.
// DESCRIPTION: Describes the cluster status information broadcast to the periodically by
//              the cluster master to the cluster instances.

using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Text;

using LillTek.Advanced;
using LillTek.Common;

// $todo(jeff.lill): 
//
// At some point I should consider changing the member list into
// a dictionary hashed by instance EP for better performance
// for very large clusters.

namespace LillTek.Messaging
{
    /// <summary>
    /// Describes the cluster status broadcast to the periodically by the cluster 
    /// master to the cluster instances.
    /// </summary>
    /// <remarks>
    /// <para>
    /// The status includes a set of string name/value pairs describing the 
    /// overall state of the cluster and then a set of <see cref="ClusterMemberStatus" />
    /// instances that describe the state of each cluster service instance.
    /// </para>
    /// <para>
    /// Cluster master implementations maintain the set of cluster wide properties
    /// and applications are free to add custom properties.  Note though that
    /// property keys with leading underscores are resevered for use by this class.
    /// </para>
    /// </remarks>
    public sealed class ClusterStatus
    {
        private Dictionary<string, string>  properties;             // Cluster properties
        private List<ClusterMemberStatus>   members;                // Cluster member information
        private ClusterMemberStatus         masterStatus = null;    // Cached master status

        /// <summary>
        /// Time this cluster status was received (local UTC).
        /// </summary>
        public DateTime ReceiveTime;

        /// <summary>
        /// The cluster master instance endpoint or <c>null</c> if there is no known master.
        /// </summary>
        public MsgEP MasterEP;

        /// <summary>
        /// The synchronized cluster time (UTC).
        /// </summary>
        public DateTime ClusterTime;

        /// <summary>
        /// The name of the machine hosting the cluster master.
        /// </summary>
        public string MasterMachine;

        /// <summary>
        /// Indicates the cluster protocol capabilities of the master instance.
        /// </summary>
        public ClusterMemberProtocolCaps MasterProtocolCaps;

        /// <summary>
        /// Constructor.
        /// </summary>
        /// <param name="masterEP">The master member endpoint.</param>
        /// <remarks>
        /// <note>
        /// <see cref="ReceiveTime" /> will be set to <see cref="DateTime.UtcNow" />.
        /// </note>
        /// </remarks>
        public ClusterStatus(MsgEP masterEP)
        {
            this.MasterEP           = masterEP;
            this.MasterMachine      = Helper.MachineName;
            this.MasterProtocolCaps = ClusterMemberProtocolCaps.Current;
            this.ReceiveTime        =
            this.ClusterTime        = DateTime.UtcNow;
            this.properties         = new Dictionary<string, string>();
            this.members            = new List<ClusterMemberStatus>();
        }

        /// <summary>
        /// Constructs a <see cref="ClusterStatus" /> instance by reading from a stream.
        /// </summary>
        /// <param name="input">The input stream.</param>
        /// <remarks>
        /// <note>
        /// <see cref="ReceiveTime" /> will be initialized to <see cref="DateTime.UtcNow" />.
        /// </note>
        /// </remarks>
        public ClusterStatus(EnhancedStream input)
        {
            int count;

            this.properties         = input.ReadProperties(StringComparer.OrdinalIgnoreCase);
            this.MasterEP           = properties["_master-ep"];
            this.MasterMachine      = properties["_master-machine"];
            this.MasterProtocolCaps = (ClusterMemberProtocolCaps)int.Parse(properties["_master-caps"]);
            this.ClusterTime        = Helper.ParseInternetDate(properties["_cluster-time"]);
            this.ReceiveTime        = DateTime.UtcNow;

            count = input.ReadInt16();
            members = new List<ClusterMemberStatus>(count);

            for (int i = 0; i < count; i++)
                members.Add(new ClusterMemberStatus(input));
        }

        /// <summary>
        /// Constructs the status by deserializing data from a byte array.
        /// </summary>
        /// <param name="input">The input array.</param>
        public ClusterStatus(byte[] input)
            : this(new EnhancedMemoryStream(input))
        {
        }

        /// <summary>
        /// Used by <see cref="Clone" />.
        /// </summary>
        private ClusterStatus()
        {
        }

        /// <summary>
        /// Returns a shallow clone of the instance.
        /// </summary>
        /// <returns>The clone.</returns>
        public ClusterStatus Clone()
        {

            var clone = new ClusterStatus();

            clone.properties         = Helper.Clone(this.properties);
            clone.ReceiveTime        = this.ReceiveTime;
            clone.MasterEP           = this.MasterEP;
            clone.ClusterTime        = this.ClusterTime;
            clone.MasterMachine      = this.MasterMachine;
            clone.MasterProtocolCaps = this.MasterProtocolCaps;

            clone.members = new List<ClusterMemberStatus>(this.members.Count);
            for (int i = 0; i < this.members.Count; i++)
                clone.members.Add(this.members[i]);

            return clone;
        }

        /// <summary>
        /// Copies all properties from the collection passed to this instance's
        /// properties, after removing all properties in this instance that
        /// don't begin with a leading underscore.
        /// </summary>
        /// <param name="appProperties">The global application properties to be copied.</param>
        internal void LoadProperties(Dictionary<string, string> appProperties)
        {
            var delList = new List<string>(properties.Count);

            foreach (string key in properties.Keys)
                if (!key.StartsWith("_"))
                    delList.Add(key);

            for (int i = 0; i < delList.Count; i++)
                properties.Remove(delList[i]);

            foreach (string key in appProperties.Keys)
                properties[key] = appProperties[key];
        }

        /// <summary>
        /// Returns a clone of the cluster's global properties.
        /// </summary>
        /// <returns></returns>
        internal Dictionary<string, string> CloneProperties()
        {
            return Helper.Clone(properties);
        }

        /// <summary>
        /// Writes the <see cref="ClusterStatus" /> to a stream.
        /// </summary>
        /// <param name="output">The output stream.</param>
        public void Write(EnhancedStream output)
        {
            properties["_master-ep"]      = this.MasterEP;
            properties["_master-machine"] = this.MasterMachine;
            properties["_master-caps"]    = ((int)this.MasterProtocolCaps).ToString();
            properties["_cluster-time"]   = Helper.ToInternetDate(this.ClusterTime);

            output.WriteProperties(properties);

            output.WriteInt16(members.Count);
            for (int i = 0; i < members.Count; i++)
                members[i].Write(output);
        }

        /// <summary>
        /// Serializes the instance to a byte array.
        /// </summary>
        /// <returns>The serialized array.</returns>
        public byte[] ToArray()
        {
            var ms = new EnhancedMemoryStream();

            try
            {
                Write(ms);
                return ms.ToArray();
            }
            finally
            {
                ms.Close();
            }
        }

        /// <summary>
        /// Gets global cluster property value.
        /// </summary>
        /// <param name="key">The propery name.</param>
        /// <returns>The property value.</returns>
        /// <remarks>
        /// <note>
        /// Property names with leading underscores are reserved for use by this class.
        /// </note>
        /// </remarks>
        public string this[string key]
        {
            get { return properties[key]; }
        }

        /// <summary>
        /// Returns <c>true</c> if a named global cluster property is present in the collection.
        /// </summary>
        /// <param name="key">The key.</param>
        /// <returns><c>true</c> if the key is present.</returns>
        public bool ContainsKey(string key)
        {
            return properties.ContainsKey(key);
        }

        /// <summary>
        /// Attempts to retrieve a named global cluster property value from the collection.
        /// </summary>
        /// <param name="key">The key.</param>
        /// <param name="value">Returns as the value (or <c>null</c>).</param>
        /// <returns><c>true</c> if the key was present and the value was returned.</returns>
        public bool TryGetValue(string key, out string value)
        {
            return properties.TryGetValue(key, out value);
        }

        /// <summary>
        /// Returns the <see cref="ClusterMemberStatus" /> for the cluster master.
        /// </summary>
        public ClusterMemberStatus MasterStatus
        {
            get
            {
                if (masterStatus != null)
                    return masterStatus;

                masterStatus = GetMemberStatus(MasterEP);
                return masterStatus;
            }
        }

        /// <summary>
        /// Returns a list of <see cref="ClusterMemberStatus" /> instances
        /// describing each member of the cluster.
        /// </summary>
        public List<ClusterMemberStatus> Members
        {
            get { return members; }
        }

        /// <summary>
        /// Adds or updates the information for a cluster member.
        /// </summary>
        /// <param name="memberStatus">The member status information.</param>
        public void Update(ClusterMemberStatus memberStatus)
        {
            for (int i = 0; i < members.Count; i++)
                if (memberStatus.InstanceEP.Equals(members[i].InstanceEP))
                {
                    members[i] = memberStatus;
                    return;
                }

            members.Add(memberStatus);
        }

        /// <summary>
        /// Removes the information for a cluster member. 
        /// </summary>
        /// <param name="memberStatus">The member status information.</param>
        public void Remove(ClusterMemberStatus memberStatus)
        {
            Remove(memberStatus.InstanceEP);
        }

        /// <summary>
        /// Removes the information for a cluster member. 
        /// </summary>
        /// <param name="instanceEP">The member's instance endpoint.</param>
        public void Remove(MsgEP instanceEP)
        {
            for (int i = 0; i < members.Count; i++)
                if (instanceEP.Equals(members[i].InstanceEP))
                {
                    members.RemoveAt(i);
                    return;
                }
        }

        /// <summary>
        /// Searches the member status for the information for a specifiec
        /// cluster member.
        /// </summary>
        /// <param name="instanceEP">The member's instance endpoint.</param>
        /// <returns>The <see cref="ClusterMemberStatus" /> or <c>null</c> if the requested member is not present.</returns>
        public ClusterMemberStatus GetMemberStatus(MsgEP instanceEP)
        {
            for (int i = 0; i < members.Count; i++)
                if (instanceEP.Equals(members[i].InstanceEP))
                    return members[i];

            return null;
        }

#if TRACE
        /// <summary>
        /// Appends tracing information to a <see cref="StringBuilder" />.
        /// </summary>
        /// <param name="sb">The <see cref="StringBuilder" />.</param>
        internal void AppendTrace(StringBuilder sb)
        {
            sb.AppendFormat("-- Cluster --\r\n");

            foreach (string key in properties.Keys)
                if (!key.StartsWith("_"))
                    sb.AppendFormat("{0} = {1}\r\n", key, properties[key]);

            foreach (ClusterMemberStatus memberStatus in members)
                memberStatus.AppendTrace(sb);
        }
#endif
    }
}
