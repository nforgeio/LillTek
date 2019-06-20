//-----------------------------------------------------------------------------
// FILE:        ClusterMemberStatus.cs
// CONTRIBUTOR: Jeff Lill
// COPYRIGHT:   Copyright (c) 2005-2015 by Jeffrey Lill.  All rights reserved.
// DESCRIPTION: Describes the status of a particular cluster service instance.

using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Text;

using LillTek.Advanced;
using LillTek.Common;

namespace LillTek.Messaging
{
    /// <summary>
    /// Describes the status of a particular cluster service instance.
    /// </summary>
    /// <remarks>
    /// <para>
    /// The status includes a set of string name/value pairs describing the 
    /// overall state of the cluster service instance.
    /// </para>
    /// <para>
    /// Applications are free to add custom properties.  Note though that
    /// property keys with leading underscores are resevered for use by this class.
    /// </para>
    /// </remarks>
    /// <threadsafety instance="false" />
    public sealed class ClusterMemberStatus
    {
        private DateTime                    receiveTime = SysTime.Now;  // Creation time (SYS)
        private Dictionary<string, string>  properties;                 // Internal and application properties
        private ClusterMemberSettings       settings = null;            // Cached settings object

        /// <summary>
        /// The instance's endpoint.
        /// </summary>
        public MsgEP InstanceEP;

        /// <summary>
        /// The cluster member's state.
        /// </summary>
        public ClusterMemberState State;

        /// <summary>
        /// The cluster member's startup mode.
        /// </summary>
        public ClusterMemberMode Mode;

        /// <summary>
        /// The name of the machine where this information originated.
        /// </summary>
        public string MachineName;

        /// <summary>
        /// Indicates the cluster member protocol capabilities of the member implementation.
        /// </summary>
        public ClusterMemberProtocolCaps ProtocolCaps;

        /// <summary>
        /// Constructor.
        /// </summary>
        /// <param name="instanceEP">The member's instance endpoint.</param>
        /// <param name="state">The member's current state.</param>
        /// <param name="settings">The cluster member's settings.</param>
        public ClusterMemberStatus(MsgEP instanceEP, ClusterMemberState state, ClusterMemberSettings settings)
        {
            this.InstanceEP   = instanceEP;
            this.State        = state;
            this.Mode         = settings.Mode;
            this.MachineName  = Helper.MachineName;
            this.ProtocolCaps = ClusterMemberProtocolCaps.Current;
            this.properties   = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

            settings.SaveTo(this);
        }

        /// <summary>
        /// Constructor that gathers its state from a <see cref="ClusterMember" /> instance.
        /// </summary>
        /// <param name="member">The <see cref="ClusterMember" /> instance.</param>
        public ClusterMemberStatus(ClusterMember member)
            : this(member.InstanceEP, member.State, member.settings)
        {
            Helper.Copy(member.properties, this.properties);
        }

        /// <summary>
        /// Reads the member status from a stream.
        /// </summary>
        /// <param name="input">The input stream.</param>
        public ClusterMemberStatus(EnhancedStream input)
        {
            properties = input.ReadProperties(StringComparer.OrdinalIgnoreCase);

            try
            {
                string v;

                if (!properties.TryGetValue("_state", out v))
                    this.State = ClusterMemberState.Unknown;
                else
                    this.State = (ClusterMemberState)Enum.Parse(typeof(ClusterMemberState), v);
            }
            catch
            {
                this.State = ClusterMemberState.Unknown;
            }

            this.Mode         = this.Settings.Mode;
            this.InstanceEP   = properties["_instance-ep"];
            this.MachineName  = properties["_machine-name"];
            this.ProtocolCaps = (ClusterMemberProtocolCaps)int.Parse(properties["_caps"]);
        }

        /// <summary>
        /// Constructs the status by deserializing data from a byte array.
        /// </summary>
        /// <param name="input">The input array.</param>
        public ClusterMemberStatus(byte[] input)
            : this(new EnhancedMemoryStream(input))
        {
        }

        /// <summary>
        /// Used by <see cref="Clone" />.
        /// </summary>
        private ClusterMemberStatus()
        {
        }

        /// <summary>
        /// Returns a shallow clone of the instance.
        /// </summary>
        /// <returns>The cloned instance.</returns>
        public ClusterMemberStatus Clone()
        {
            var clone = new ClusterMemberStatus();

            clone.settings     = null;
            clone.properties   = Helper.Clone(this.properties);
            clone.InstanceEP   = this.InstanceEP;
            clone.MachineName  = this.MachineName;
            clone.ProtocolCaps = this.ProtocolCaps;
            clone.State        = this.State;
            clone.Mode         = this.Mode;

            return clone;
        }

        /// <summary>
        /// Returns the time this information was received from the cluster
        /// member (SYS).
        /// </summary>
        public DateTime ReceiveTime
        {
            get { return receiveTime; }
        }

        /// <summary>
        /// Returns the member's configuration settings.  Note that the object
        /// returned and its properties should be considered to be read-only.
        /// </summary>
        public ClusterMemberSettings Settings
        {
            get
            {
                if (settings == null)
                    settings = new ClusterMemberSettings(this);

                return settings;
            }
        }

        /// <summary>
        /// Serializes the member status to a stream.
        /// </summary>
        /// <param name="output">The output stream.</param>
        public void Write(EnhancedStream output)
        {
            properties["_state"]        = State.ToString();
            properties["_instance-ep"]  = InstanceEP;
            properties["_machine-name"] = MachineName;
            properties["_caps"]         = ((int)ProtocolCaps).ToString();

            output.WriteProperties(properties);
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
        /// Gets a named property value.
        /// </summary>
        /// <param name="key">The propery name.</param>
        /// <returns>The property value.</returns>
        /// <remarks>
        /// <note>
        /// Property names with leading underscores are
        /// reserved for use by this class.
        /// </note>
        /// </remarks>
        public string this[string key]
        {
            get { return properties[key]; }
            internal set { properties[key] = value; }
        }

        /// <summary>
        /// Returns <c>true</c> if a named key is present in the collection.
        /// </summary>
        /// <param name="key">The key.</param>
        /// <returns><c>true</c> if the key is present.</returns>
        public bool ContainsKey(string key)
        {
            return properties.ContainsKey(key);
        }

        /// <summary>
        /// Attempts to retrieve a named value from the collection.
        /// </summary>
        /// <param name="key">The key.</param>
        /// <param name="value">Returns as the value (or <c>null</c>).</param>
        /// <returns><c>true</c> if the key was present.</returns>
        public bool TryGetValue(string key, out string value)
        {
            return properties.TryGetValue(key, out value);
        }

        /// <summary>
        /// Returns the property key collection.
        /// </summary>
        public ICollection<String> Keys
        {
            get { return properties.Keys; }
        }

        /// <summary>
        /// Returns the property value collection.
        /// </summary>
        public ICollection<String> Values
        {
            get { return properties.Values; }
        }

        /// <summary>
        /// Clears the application defined cluster properties, leaving any
        /// internal properties whose names have leading underscores intact.
        /// </summary>
        internal void Clear()
        {
            var delKeys = new List<string>();

            foreach (string key in properties.Keys)
                if (!key.StartsWith("_"))
                    delKeys.Add(key);

            for (int i = 0; i < delKeys.Count; i++)
                properties.Remove(delKeys[i]);
        }

#if TRACE
        /// <summary>
        /// Appends tracing information to a <see cref="StringBuilder" />.
        /// </summary>
        /// <param name="sb">The <see cref="StringBuilder" />.</param>
        internal void AppendTrace(StringBuilder sb)
        {
            sb.AppendFormat("--- {0,-8}: {1}\r\n", State.ToString().ToUpper(), InstanceEP);

            foreach (string key in properties.Keys)
                if (!key.StartsWith("_"))
                    sb.AppendFormat("{0} = {1}\r\n", key, properties[key]);
        }
#endif
    }
}
