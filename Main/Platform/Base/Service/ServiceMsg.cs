//-----------------------------------------------------------------------------
// FILE:        ServiceMsg.cs
// CONTRIBUTOR: Jeff Lill
// COPYRIGHT:   Copyright (c) 2005-2015 by Jeffrey Lill.  All rights reserved.
// DESCRIPTION: Internal class that implements the serialization of a general 
//              name/value message to be used for passing information between
//              ServiceControl and ServiceBase instances.

using System;
using System.Collections;

using LillTek.Common;

namespace LillTek.Service
{
    /// <summary>
    /// Implements the serialization of a general name/value message to be 
    /// used for passing information between ServiceControl and ServiceBase 
    /// instances.
    /// </summary>
    internal sealed class ServiceMsg
    {
        private const int Magic = 0x735F;

        private ArrayList properties;

        /// <summary>
        /// A name/value pair
        /// </summary>
        private sealed class Entry
        {
            public string Key;
            public string Value;

            public Entry(string key, string value)
            {
                this.Key   = key;
                this.Value = value;
            }
        }

        /// <summary>
        /// Constructs an empty message.
        /// </summary>
        public ServiceMsg()
        {
            properties = new ArrayList();
        }

        /// <summary>
        /// Constructs message with the command string passed.
        /// </summary>
        /// <param name="command">The command.</param>
        public ServiceMsg(string command)
        {
            this.properties = new ArrayList();
            this.Command    = command;
        }

        /// <summary>
        /// Deserializes the message from the raw byte buffer passed.
        /// </summary>
        /// <param name="buf">The serialized message.</param>
        public ServiceMsg(byte[] buf)
        {
            int     magic;
            int     count;
            int     pos;

            try
            {
                properties = new ArrayList();

                pos   = 0;
                magic = Helper.ReadInt16(buf, ref pos);
                if (magic != Magic)
                {
                    throw new FormatException();
                }

                count = Helper.ReadInt16(buf, ref pos);
                for (int i = 0; i < count; i++)
                {
                    var key   = Helper.ReadString16(buf, ref pos);
                    var value = Helper.ReadString16(buf, ref pos);

                    properties.Add(new Entry(key, value));
                }
            }
            catch
            {
                throw new FormatException("Error deserializing ServiceMsg.");
            }
        }

        /// <summary>
        /// Serializes the message into a byte array.
        /// </summary>
        /// <returns>The serialized message.</returns>
        public byte[] ToBytes()
        {
            byte[]  buf;
            byte[]  output;
            int     pos;

            buf = new byte[ServiceControl.MaxMsgSize];
            pos = 0;

            Helper.WriteInt16(buf, ref pos, Magic);
            Helper.WriteInt16(buf, ref pos, properties.Count);

            for (int i = 0; i < properties.Count; i++)
            {
                var entry = (Entry)properties[i];

                Helper.WriteString16(buf, ref pos, entry.Key);
                Helper.WriteString16(buf, ref pos, entry.Value);
            }

            output = new byte[pos];
            Array.Copy(buf, 0, output, 0, pos);

            return output;
        }

        /// <summary>
        /// Implements a name/value table.  Note that the keys are
        /// case sensitive.
        /// </summary>
        public string this[string key]
        {
            get
            {
                for (int i = 0; i < properties.Count; i++)
                {
                    var entry = (Entry)properties[i];

                    if (entry.Key == key)
                    {
                        return entry.Value;
                    }
                }

                return null;
            }

            set
            {
                for (int i = 0; i < properties.Count; i++)
                {
                    var entry = (Entry)properties[i];

                    if (entry.Key == key)
                    {
                        entry.Value = value;
                        return;
                    }
                }

                properties.Add(new Entry(key, value));
            }
        }

        /// <summary>
        /// Hardcoded message property specifying the message command.
        /// </summary>
        public string Command
        {
            get { return this["_Command"]; }
            set { this["_Command"] = value; }
        }

        /// <summary>
        /// Hardcoded message property specifying the transaction ID.
        /// </summary>
        /// <remarks>
        /// This ID is used to correlate queries and responses.
        /// </remarks>
        public Guid RefID
        {
            get
            {
                var v = this["_RefID"];

                if (v == null)
                {
                    return Guid.Empty;
                }

                return new Guid(v);
            }

            set { this["_RefID"] = value.ToString(); }
        }
    }
}
