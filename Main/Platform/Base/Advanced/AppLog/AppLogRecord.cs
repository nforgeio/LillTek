//-----------------------------------------------------------------------------
// FILE:        AppLogRecord.cs
// CONTRIBUTOR: Jeff Lill
// COPYRIGHT:   Copyright (c) 2005-2015 by Jeffrey Lill.  All rights reserved.
// DESCRIPTION: Implements access to an application log record.

using System;
using System.Collections;
using System.Diagnostics;
using System.IO;
using System.Text;

using LillTek.Common;

namespace LillTek.Advanced
{
    /// <summary>
    /// Implements access to an application log record.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Note that field names are case insenstive and will be converted
    /// to lower case by this class.
    /// </para>
    /// <para><b><u>Serialization Format</u></b></para>
    /// <para>
    /// The <see cref="Read" /> and <see cref="Write" /> methods may be used by applications
    /// to serialize <see cref="AppLogRecord" />s to <see cref="EnhancedStream" />s.  This
    /// is useful for situations where individual records need to be serialized for some
    /// reason (like transmitting in a message).  Note that these methods are not used by 
    /// <see cref="AppLog" />s to serialzie records to log files since application logs produce 
    /// smaller files by optimizing how strings common across multiple log records are
    /// written.
    /// </para>
    /// <para>
    /// The <see cref="Read" /> and <see cref="Write" /> methods use the stream format
    /// described below.
    /// </para>
    /// <code language="none">
    /// 
    ///            Header
    ///     +------------------+
    ///     |   Magic Number   |    16-bit:     File format magic number (0xF177)
    ///     +------------------+
    ///     |  Format Version  |    STRING:     File format version string
    ///     +------------------+
    ///     |       Flags      |    32-bit:     Flag bits (set to 0 and reserved for future use)
    ///     +------------------+
    ///     |   Schema Name    |    STRING:     Record schema name
    ///     +------------------+
    ///     |  Schema Version  |    STRING:     Record schema version
    ///     +------------------+
    ///     |    Field Count   |    32-bit:     Number of fields to follow
    ///     +------------------+
    /// 
    ///          Each Field
    ///     +------------------+
    ///     |    Field Name    |    STRING:     Field name
    ///     +------------------+
    ///     |    Field Type    |    8-bit:      Indicates the data type: 0=string, 1=byte[]
    ///     +------------------+
    ///     |    Data Length   |    32-bit:     Length of the field data in bytes
    ///     +------------------+
    ///     |                  |
    ///     |                  |
    ///     |       Field      |
    ///     |       Data       |
    ///     |                  |
    ///     |                  |
    ///     +------------------+
    /// 
    /// </code>
    /// <para>
    /// All strings (except for the field value) are encoded in UTF-8 and are 
    /// prefixed by a 16-bit length (with length=-1 indicating a null string).  
    /// All multibyte integers are stored in network (or bigendian) byte order.
    /// </para>
    /// <para>
    /// The field data is prefixed with a 32-bit integer specifiying the number
    /// of bytes of data to follow.  For byte array fields, this data is simply 
    /// the array bytes.  For string values, this is the string characters 
    /// encoded as UTF-8.
    /// </para>
    /// </remarks>
    /// <threadsafety static="false" instance="false" />
    public class AppLogRecord : IEnumerable
    {
        private const int       Magic            = 0x7177;
        private static Version  formatVersion    = new Version("1.0.0.0");
        private static Version  defSchemaVersion = new Version("0.0.0.0");

        private string      schemaName;     // The record's schema name
        private Version     schemaVersion;  // The record's schema version
        private Hashtable   fields;         // Field values keyed by field name

        /// <summary>
        /// Constructor.
        /// </summary>
        public AppLogRecord()
        {
            this.fields        = new Hashtable();
            this.schemaName    = string.Empty;
            this.schemaVersion = defSchemaVersion;
        }

        /// <summary>
        /// Constructor.
        /// </summary>
        /// <param name="schemaName">The log schema name.</param>
        /// <param name="schemaVersion">The log schema version.</param>
        public AppLogRecord(string schemaName, Version schemaVersion)
        {
            this.fields        = new Hashtable();
            this.schemaName    = schemaName;
            this.schemaVersion = schemaVersion;
        }

        /// <summary>
        /// Adds a named string value to the record.
        /// </summary>
        /// <param name="name">The field name.</param>
        /// <param name="value">The value.</param>
        /// <remarks>
        /// <note>
        /// Field names are case insenstive and will be converted
        /// to lower case by this class.
        /// </note>
        /// </remarks>
        public void Add(string name, string value)
        {
            if (value == null)
                throw new ArgumentNullException("value");

            fields.Add(name.ToLowerInvariant(), value);
        }

        /// <summary>
        /// Adds a named string value to the record.
        /// </summary>
        /// <param name="name">The field name.</param>
        /// <param name="value">The value.</param>
        /// <remarks>
        /// <note>
        /// Field names are case insenstive and will be converted
        /// to lower case by this class.
        /// </note>
        /// </remarks>
        public void Add(string name, object value)
        {
            if (value == null)
                throw new ArgumentNullException("value");

            fields.Add(name.ToLowerInvariant(), value.ToString());
        }

        /// <summary>
        /// Adds a named byte array value to the record.
        /// </summary>
        /// <param name="name">The field name.</param>
        /// <param name="value">The value.</param>
        /// <remarks>
        /// <note>
        /// Field names are case insenstive and will be converted
        /// to lower case by this class.
        /// </note>
        /// </remarks>
        public void Add(string name, byte[] value)
        {
            if (value == null)
                throw new ArgumentNullException("value");

            fields.Add(name.ToLowerInvariant(), value);
        }

        /// <summary>
        /// Returns the number of fields in the record.
        /// </summary>
        public int Count
        {
            get { return fields.Count; }
        }

        /// <summary>
        /// Associates a value with a field name.
        /// </summary>
        /// <param name="name">The field name.</param>
        /// <returns>
        /// The string or byte array value associated with the field name.
        /// </returns>
        /// <remarks>
        /// <note>
        /// Field names are case insenstive.
        /// </note>
        /// </remarks>
        public object this[string name]
        {
            get { return fields[name.ToLowerInvariant()]; }

            set
            {
                if (value == null)
                    throw new ArgumentNullException("value");

                if (!(value is byte[]))
                    value = value.ToString();

                fields[name.ToLowerInvariant()] = value;
            }
        }

        /// <summary>
        /// Returns the application specific schema name for records
        /// read from a log.
        /// </summary>
        public string SchemaName
        {
            get { return schemaName; }
        }

        /// <summary>
        /// Returns the application schema version for for records 
        /// read from a log.
        /// </summary>
        public Version SchemaVersion
        {
            get { return schemaVersion; }
        }

        /// <summary>
        /// Enumerators over the name/value pairs in the record.
        /// </summary>
        /// <returns></returns>
        public IEnumerator GetEnumerator()
        {
            return fields.GetEnumerator();
        }

        /// <summary>
        /// Calculates a has code for the instance.
        /// </summary>
        /// <returns>The integer hash code.</returns>
        public override int GetHashCode()
        {
            return base.GetHashCode();
        }

        /// <summary>
        /// Returns <c>true</c> if the object passed equals this instance.
        /// </summary>
        /// <param name="o">The object to be compared.</param>
        /// <returns><c>true</c> if the objects are equal.</returns>
        /// <remarks>
        /// <note>
        /// Only the field values are compared.  The SchemaName
        /// and SchemaVersion properties are not considered.
        /// </note>
        /// </remarks>
        public override bool Equals(object o)
        {
            AppLogRecord rec;

            rec = o as AppLogRecord;
            if (rec == null)
                return false;

            if (this.fields.Count != rec.fields.Count)
                return false;

            foreach (DictionaryEntry entry in this.fields)
            {
                string      key;
                object      thisValue;
                object      recValue;
                string      thisString;
                string      recString;
                byte[]      thisBytes;
                byte[]      recBytes;

                key       = (string)entry.Key;
                thisValue = entry.Value;
                recValue  = rec[key];

                if (recValue == null)
                    return false;

                thisString = thisValue as string;
                if (thisString != null)
                {
                    recString = recValue as string;
                    if (recString == null)
                        return false;

                    if (thisString != recString)
                        return false;
                }
                else
                {
                    thisBytes = (byte[])thisValue;
                    recBytes  = recValue as byte[];

                    if (recBytes == null)
                        return false;

                    if (thisBytes.Length != recBytes.Length)
                        return false;

                    for (int i = 0; i < thisBytes.Length; i++)
                        if (thisBytes[i] != recBytes[i])
                            return false;
                }
            }

            return true;
        }

        /// <summary>
        /// Unserializes the next record from an enhanced stream.
        /// </summary>
        /// <param name="es">The input stream.</param>
        public void Read(EnhancedStream es)
        {
            int         count;
            string      fieldName;

            try
            {
                if (es.ReadInt16() != Magic)
                    throw new FormatException("Corrupt log record.");

                if (new Version(es.ReadString16()) < formatVersion)
                    throw new FormatException("Record format is newer than this codebase.");

                es.ReadInt32();     // Flags
                schemaName    = es.ReadString16();
                schemaVersion = new Version(es.ReadString16());
                count         = es.ReadInt32();

                for (int i = 0; i < count; i++)
                {
                    fieldName = es.ReadString16();

                    switch (es.ReadByte())
                    {
                        case 0:

                            Add(fieldName, es.ReadString32());
                            break;

                        case 1:

                            Add(fieldName, es.ReadBytes32());
                            break;

                        default:

                            throw new FormatException("Corrupt log record.");
                    }
                }
            }
            catch (Exception e)
            {
                throw new FormatException(string.Format("Log record read error: {0}", e.Message));
            }
        }

        /// <summary>
        /// Serializes the record to an enhanced stream.
        /// </summary>
        /// <param name="es">The output stream.</param>
        public void Write(EnhancedStream es)
        {
            es.WriteInt16(Magic);
            es.WriteString16(formatVersion.ToString());
            es.WriteInt32(0);   // Flags
            es.WriteString16(schemaName);
            es.WriteString16(schemaVersion.ToString());
            es.WriteInt32(fields.Count);

            foreach (string fieldName in fields.Keys)
            {
                object      value;
                string      s;
                byte[]      arr;

                es.WriteString16(fieldName);
                value = fields[fieldName];

                if (value == null)
                {
                    // Encode null values as a null string.

                    es.WriteByte(0);
                    es.WriteString32(null);
                    continue;
                }

                s = value as string;
                if (s != null)
                {
                    es.WriteByte(0);
                    es.WriteString32(s);
                    continue;
                }

                arr = value as byte[];
                Assertion.Test(arr != null);

                es.WriteByte(1);
                es.WriteBytes32(arr);
            }
        }
    }
}
