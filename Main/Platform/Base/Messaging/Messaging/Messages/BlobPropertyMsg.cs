//-----------------------------------------------------------------------------
// FILE:        BlobPropertyMsg.cs
// CONTRIBUTOR: Jeff Lill
// COPYRIGHT:   Copyright (c) 2005-2015 by Jeffrey Lill.  All rights reserved.
// DESCRIPTION: Implements a general purpose message that holds name/value pairs
//              along with a single binary blob.

using System;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Reflection;
using System.Text;

using LillTek.Common;

// $todo(jeff.lill): 
//
// I should optmize this by using a hash table only if there's
// more than maybe 10 properties in a message and using some
// sort of linear ArrayList search if there are fewer properties.

namespace LillTek.Messaging
{
    /// <summary>
    /// Implements a general purpose message that holds name/value pairs
    /// along with a single binary blob in the <see cref="_Data" /> property.
    /// </summary>
    /// <remarks>
    /// <para><b><u>Message Encoding</u></b></para>
    /// <para>
    /// The serialization format is pretty straightforward:
    /// </para>
    /// <code language="none">
    ///   BLOB Encoding
    /// 
    /// +--------------+
    /// |    cbBlob    |        DWORD: Size of the blob in bytes
    /// |--------------|               (-1 indicates a NULL blob)
    /// |              |
    /// |              |
    /// |              |
    /// |              |
    /// |     Blob     |    
    /// |     Data     |        Blob data bytes
    /// |              |
    /// |              |
    /// |              |
    /// |              |
    /// |              |
    /// +--------------+
    /// 
    /// Name/value Properties Encoding
    /// 
    /// +--------------+
    /// |    Count     |        WORD: Number of key/value pairs
    /// |--------------|
    /// |    Key 0     |        STRING: Key
    /// |--------------|
    /// |   Value 0    |        STRING: Value
    /// |--------------|
    /// |    Key 1     |
    /// |--------------|
    /// |   Value 1    |
    /// |--------------|
    ///
    ///        ...
    ///
    /// |--------------|
    /// |    Key N     |
    /// |--------------|
    /// |   Value N    |
    /// +--------------+
    /// </code>
    /// </remarks>
    public class BlobPropertyMsg : Msg
    {
        //---------------------------------------------------------------------
        // Static members

        /// <summary>
        /// Returns the type ID string to be used when serializing the message.
        /// </summary>
        public static string GetTypeID()
        {
            return ".BlobProperty";
        }

        //---------------------------------------------------------------------
        // Instance members

        private Dictionary<string, string> properties;     // The name/value properties
        private byte[] blob;           // The blob data (or null)

        /// <summary>
        /// Constructor.
        /// </summary>
        public BlobPropertyMsg()
        {
            properties = new Dictionary<string, string>(StringComparer.InvariantCultureIgnoreCase);
            blob       = null;
        }

        /// <summary>
        /// Protected constructor that does not instantiate the property hash table.
        /// </summary>
        /// <param name="param">Pass <see cref="Stub.Param" />.</param>
        /// <remarks>
        /// Derived classes should use this constructor passing false when overriding 
        /// <see cref="Msg.Clone" /> to avoid creating an extra field object instances.
        /// </remarks>
        protected BlobPropertyMsg(Stub param)
        {
        }

        /// <summary>
        /// Shallow copies the base fields from the source message to this instance.
        /// </summary>
        /// <param name="source">The source message.</param>
        /// <param name="regenMsgID">
        /// Pass as <c>true</c> to renegerate the <see cref="Msg._MsgID" /> property if the 
        /// source message ID property is not empty.
        /// </param>
        /// <remarks>
        /// Use this in overriden <see cref="Msg.Clone" /> method implementations
        /// to ensure that the base message fields are copied properly.
        /// </remarks>
        protected override void CopyBaseFields(Msg source, bool regenMsgID)
        {
            var blobMsg = (BlobPropertyMsg)source;

            base.CopyBaseFields(source, regenMsgID);

            this.properties = (blobMsg.properties.Clone<string, string>(StringComparer.InvariantCultureIgnoreCase));
            this.blob       = blobMsg.blob;
        }

        /// <summary>
        /// The message's data blob (or <c>null</c>).
        /// </summary>
        public byte[] _Data
        {
            get { return blob; }
            set { blob = value; }
        }

        /// <summary>
        /// Returns the set of property keys saved in the message.
        /// </summary>
        public ICollection<string> _Keys
        {
            get { return properties.Keys; }
        }

        /// <summary>
        /// Shallow copies the properties from this message to another.
        /// </summary>
        /// <param name="msg">The message to receive the copied properties.</param>
        public void _CopyPropertiesTo(BlobPropertyMsg msg)
        {
            foreach (string key in properties.Keys)
                msg[key] = this[key];

            msg.blob = this.blob;
        }

        /// <summary>
        /// Serializes the payload of the base classes into the stream.
        /// </summary>
        /// <param name="es">The enhanced stream where the output is to be written.</param>
        /// <remarks>
        /// Classes that are designed to be derived from should implement
        /// this method to serialize their content.  Note that the base.WriteBase()
        /// method should be called before doing this to ensure that any 
        /// ancestor classes will be serialized properly.
        /// </remarks>
        protected override void WriteBase(EnhancedStream es)
        {
            base.WriteBase(es);

            es.WriteBytes32(blob);

            es.WriteInt16(properties.Count);
            foreach (string key in properties.Keys)
            {
                es.WriteString16(key);
                es.WriteString16((string)properties[key]);
            }
        }

        /// <summary>
        /// Loads the message payload of the base classes from the stream passed.
        /// </summary>
        /// <param name="es">The enhanced stream holding the payload data.</param>
        /// <remarks>
        /// Classes that are designed to be derived from should implement
        /// this method to serialize their content.  Note that the base.ReadFrom()
        /// method should be called before doing this to ensure that any 
        /// ancestor classes will be unserialized properly.
        /// </remarks>
        protected override void ReadFrom(EnhancedStream es)
        {
            base.ReadFrom(es);

            int         count;
            string      key;
            string      value;

            blob = es.ReadBytes32();

            properties.Clear();

            count = es.ReadInt16();
            for (int i = 0; i < count; i++)
            {
                key   = es.ReadString16();
                value = es.ReadString16();
                properties.Add(key, value);
            }
        }

#if WINFULL
        /// <summary>
        /// Add detailed trace information about this message to the
        /// StringBuilder passed.
        /// </summary>
        /// <param name="router">The associated router (or <c>null</c>).</param>
        /// <param name="sb">The string builder.</param>
        /// <remarks>
        /// Adds the name/value pairs to the information returned
        /// by the base class.
        /// </remarks>
        public override void _TraceDetails(MsgRouter router, StringBuilder sb)
        {
            base._TraceDetails(router, sb);

            foreach (string key in properties.Keys)
                sb.AppendFormat(null, "{0}={1}\r\n", key, properties[key]);

            if (blob == null)
                sb.Append("\r\nBlob: NULL");
            else
            {
                sb.AppendFormat("\r\nBLOB: cb={0}\r\n", blob.Length);
                sb.Append(Helper.HexDump(blob, 32, HexDumpOption.ShowAll));
            }
        }
#endif

        /// <summary>
        /// Gets/sets the properties whose key is passed.
        /// </summary>
        /// <param name="key">The case insensitive key.</param>
        public string this[string key]
        {
            get
            {
                string value;

                if (properties.TryGetValue(key, out value))
                    return value;
                else
                    return null;
            }

            set { properties[key] = value; }
        }

        /// <summary>
        /// Sets the named property to the value passed.
        /// </summary>
        /// <param name="key">The case insensitive key.</param>
        /// <param name="value">The value.</param>
        public void _Set(string key, string value)
        {
            this[key] = value;
        }

        /// <summary>
        /// Returns the value for the key passed if it exists in the configuration
        /// or <c>null</c> if it cannot be found.
        /// </summary>
        /// <param name="key">The key.</param>
        /// <returns>The value or <c>null</c>.</returns>
        public string _Get(string key)
        {
            string value;

            if (properties.TryGetValue(key, out value))
                return value;
            else
                return null;
        }

        /// <summary>
        /// Returns the value for the key passed if it exists in the configuration
        /// or def if it cannot be found.
        /// </summary>
        /// <param name="key">The key.</param>
        /// <param name="def">The default value.</param>
        public string _Get(string key, string def)
        {
            string value;

            value = _Get(key);
            if (value == null)
                return def;
            else
                return value;
        }

        /// <summary>
        /// Sets the named property to the value passed.
        /// </summary>
        /// <param name="key">The case insensitive key.</param>
        /// <param name="value">The value.</param>
        public void _Set(string key, bool value)
        {
            this[key] = Serialize.ToString(value);
        }

        /// <summary>
        /// Returns the value for the key passed if it exists in the configuration
        /// or def if it cannot be found or if there is a problem parsing the value.
        /// </summary>
        /// <param name="key">The key.</param>
        /// <param name="def">The default value.</param>
        /// <remarks>
        /// Boolean values may be encoded using the following literals: 0/1, on/off,
        /// yes/no, true/false, enable/disable.
        /// </remarks>
        public bool _Get(string key, bool def)
        {
            return Serialize.Parse(_Get(key), def);
        }

        /// <summary>
        /// Sets the named property to the value passed.
        /// </summary>
        /// <param name="key">The case insensitive key.</param>
        /// <param name="value">The value.</param>
        public void _Set(string key, int value)
        {
            this[key] = Serialize.ToString(value);
        }

        /// <summary>
        /// Returns the value for the key passed if it exists in the configuration
        /// or def if it cannot be found or if there is a problem parsing the value.
        /// </summary>
        /// <param name="key">The key.</param>
        /// <param name="def">The default value.</param>
        public int _Get(string key, int def)
        {
            return Serialize.Parse(_Get(key), def);
        }

        /// <summary>
        /// Sets the named property to the value passed.
        /// </summary>
        /// <param name="key">The case insensitive key.</param>
        /// <param name="value">The value.</param>
        public void _Set(string key, TimeSpan value)
        {
            this[key] = Serialize.ToString(value);
        }

        /// <summary>
        /// Returns the value for the key passed if it exists in the configuration
        /// or def if it cannot be found or if there is a problem parsing the value.
        /// </summary>
        /// <param name="key">The key.</param>
        /// <param name="def">The default value.</param>
        /// <remarks>
        /// Timespan values are encoded as floating point values terminated with
        /// one of the unit codes: "d" (days), "h" (hours), "m" (minutes), "s"
        /// (seconds), or "ms" (milliseconds).  If the unit code is missing then 
        /// seconds will be assumed.  An infinite timespan is encoded using the 
        /// literal "infinite".
        /// </remarks>
        public TimeSpan _Get(string key, TimeSpan def)
        {
            return Serialize.Parse(_Get(key), def);
        }

        /// <summary>
        /// Sets the named property to the value passed.
        /// </summary>
        /// <param name="key">The case insensitive key.</param>
        /// <param name="value">The value.</param>
        /// <remarks>
        /// Dates are encoded into strings as described in RFC 1123.
        /// </remarks>
        public void _Set(string key, DateTime value)
        {
            this[key] = Serialize.ToString(value);
        }

        /// <summary>
        /// Returns the value for the key passed if it exists in the configuration
        /// or def if it cannot be found or if there is a problem parsing the value.
        /// </summary>
        /// <param name="key">The key.</param>
        /// <param name="def">The default value.</param>
        /// <remarks>
        /// Dates are encoded into strings as described in RFC 1123.
        /// </remarks>
        public DateTime _Get(string key, DateTime def)
        {
            return Serialize.Parse(_Get(key), def);
        }

        /// <summary>
        /// Sets the named property to the value passed.
        /// </summary>
        /// <param name="key">The case insensitive key.</param>
        /// <param name="value">The value.</param>
        public void _Set(string key, Guid value)
        {
            this[key] = Serialize.ToString(value);
        }

        /// <summary>
        /// Returns the GUID parsed from the key passed or
        /// the default value if the key doesn't exist or if
        /// there was a problem parsing the value.
        /// </summary>
        /// <param name="key">The key.</param>
        /// <param name="def">The default value.</param>
        /// <returns>The associated GUID value.</returns>
        public Guid _Get(string key, Guid def)
        {
            return Serialize.Parse(_Get(key), def);
        }

        /// <summary>
        /// Sets the named property to the value passed.
        /// </summary>
        /// <param name="key">The case insensitive key.</param>
        /// <param name="value">The value.</param>
        public void _Set(string key, IPEndPoint value)
        {
            this[key] = Serialize.ToString(value);
        }

        /// <summary>
        /// Returns the IP endpoint parsed from the key passed or
        /// the default value if the key doesn't exist or if there 
        /// was a problem parsing the value. 
        /// </summary>
        /// <param name="key">The key.</param>
        /// <param name="def">The default value.</param>
        /// <remarks>
        /// Endpoints are formatted as &lt;dotted-quad&gt;:&lt;port&gt;.
        /// </remarks>
        public IPEndPoint _Get(string key, IPEndPoint def)
        {
            return Serialize.Parse(_Get(key), def);
        }

        /// <summary>
        /// Sets the named property to the value passed.
        /// </summary>
        /// <param name="key">The case insensitive key.</param>
        /// <param name="value">The value.</param>
        public void _Set(string key, byte[] value)
        {
            this[key] = Serialize.ToString(value);
        }

        /// <summary>
        /// Returns the array of bytes for the key whose value is
        /// a hex encoded byte array.
        /// </summary>
        /// <param name="key">The key.</param>
        /// <param name="def">The default value.</param>
        /// <returns>The byte array or <c>null</c> if the key was not found.</returns>
        public byte[] _Get(string key, byte[] def)
        {
            return Serialize.Parse(_Get(key), def);
        }

        /// <summary>
        /// Returns an array values for the key passed.
        /// </summary>
        /// <param name="key">The key.</param>
        /// <remarks>
        /// This works by looking for the values for keys that
        /// have the form: key[#] where # is the zero-based index
        /// of the array.  The method starts at index 0 and looks for
        /// keys the match the generated value until we don't find
        /// a match.  The method returns the resulting set of values.
        /// </remarks>
        public string[] _GetArray(string key)
        {
            int         c;
            string[]    values;

            c = 0;
            while (true)
            {
                if (_Get(string.Format("{0}[{1}]", key, c)) == null)
                    break;

                c++;
            }

            values = new string[c];
            for (int i = 0; i < c; i++)
                values[i] = _Get(string.Format("{0}[{1}]", key, i));

            return values;
        }

        /// <summary>
        /// Returns an array IPEndPoint values for the key passed.
        /// </summary>
        /// <param name="key">The key.</param>
        /// <remarks>
        /// This works by looking for the values for keys that
        /// have the form: key[#] where # is the zero-based index
        /// of the array.  The method starts at index 0 and looks for
        /// keys the match the generated value until we don't find
        /// a match.  The method returns the resulting set of values.
        /// </remarks>
        public IPEndPoint[] _GetIPEndPointArray(string key)
        {
            int             c;
            IPEndPoint[]    values;

            c = 0;
            while (true)
            {
                if (_Get(string.Format("{0}[{1}]", key, c), (IPEndPoint)null) == null)
                    break;

                c++;
            }

            values = new IPEndPoint[c];
            for (int i = 0; i < c; i++)
                values[i] = _Get(string.Format("{0}[{1}]", key, i), (IPEndPoint)null);

            return values;
        }
    }
}
