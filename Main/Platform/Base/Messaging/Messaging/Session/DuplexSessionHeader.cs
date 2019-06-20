//-----------------------------------------------------------------------------
// FILE:        DuplexSessionHeader.cs
// CONTRIBUTOR: Jeff Lill
// COPYRIGHT:   Copyright (c) 2005-2015 by Jeffrey Lill.  All rights reserved.
// DESCRIPTION: Encodes DuplexSessionHeader related information into a message's 
//              MsgHeaderID.DuplexSession extension header.

using System;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Reflection;
using System.Text;
using LillTek.Common;
using LillTek.Messaging;
using LillTek.Messaging.Queuing;

namespace LillTek.Messaging
{
    /// <summary>
    /// Indicates whether a message is a query or a response.
    /// </summary>
    internal enum DuplexMessageType
    {
        /// <summary>
        /// Not used.
        /// </summary>
        Unknown = 0,

        /// <summary>
        /// The message is a query.
        /// </summary>
        Query = 1,

        /// <summary>
        /// The message is a query response.
        /// </summary>
        Response = 2
    }

    /// <summary>
    /// Encodes <see cref="DuplexSessionHeader" /> related information
    /// into a message's <see cref="MsgHeaderID.DuplexSession" /> extension
    /// header.
    /// </summary>
    /// <remarks>
    /// <para>
    /// The current <see cref="DuplexSessionHeader" /> implementation uses this
    /// class to encode session query related information.
    /// </para>
    /// <para>
    /// This class can be instantiated by parsing the contents of a 18 byte array
    /// returned by a call to <see cref="ToArray" />.  This array is formatted
    /// as (with integers stored in big-endian order):
    /// </para>
    /// <code language="none">
    /// +------------------+
    /// |  Format Version  |    8-bits:   Format version (0)
    /// +------------------+
    /// |       Type       |    8-bits:   <see cref="DuplexMessageType" /> value
    /// +------------------+
    /// |                  |
    /// |                  |
    /// |     Query ID     |    16-bytes: The session query GUID
    /// |                  |
    /// |                  |
    /// +------------------+
    /// </code>
    /// </remarks>
    internal sealed class DuplexSessionHeader
    {
        private const int Size = 18;

        /// <summary>
        /// The session relative ID used to correlate queries and responses.
        /// </summary>
        public readonly Guid QueryID;

        /// <summary>
        /// The <see cref="DuplexMessageType" /> value indicating whether the message 
        /// is a query or a response.
        /// </summary>
        public readonly DuplexMessageType Type;

        /// <summary>
        /// Constructor.
        /// </summary>
        /// <param name="queryID">The session relative ID used to correlate queries and responses.</param>
        /// <param name="type">
        /// The <see cref="DuplexMessageType" /> value indicating whether the message 
        /// is a query or a response.
        /// </param>
        public DuplexSessionHeader(Guid queryID, DuplexMessageType type)
        {
            this.QueryID = queryID;
            this.Type    = type;
        }

        /// <summary>
        /// Constructs the instance by parsing the serialized byte array passed.
        /// </summary>
        /// <param name="input">A byte array formatted as return by <see cref="ToArray" />.</param>
        /// <exception cref="FormatException">Thrown if the byte array passed is not valid.</exception>
        public DuplexSessionHeader(byte[] input)
        {
            int pos;

            if (input.Length != Size || input[0] != 0)
                throw new FormatException("Invalid DuplexSessionHeader.");

            this.Type    = (DuplexMessageType)input[1];
            pos          = 2;
            this.QueryID = Helper.ReadGuid(input, ref pos);
        }

        /// <summary>
        /// Serializes the instance into a byte array.
        /// </summary>
        /// <returns>The serialized instance.</returns>
        public byte[] ToArray()
        {
            byte[]  arr = new byte[Size];
            int     pos = 0;

            Helper.WriteByte(arr, ref pos, 0);
            Helper.WriteByte(arr, ref pos, (byte)Type);
            Helper.WriteGuid(arr, ref pos, QueryID);

            return arr;
        }
    }
}
