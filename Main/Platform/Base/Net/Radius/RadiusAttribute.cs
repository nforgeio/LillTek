//-----------------------------------------------------------------------------
// FILE:        RadiusAttribute.cs
// CONTRIBUTOR: Jeff Lill
// COPYRIGHT:   Copyright (c) 2005-2015 by Jeffrey Lill.  All rights reserved.
// DESCRIPTION: The RADIUS message attribute base class.

using System;
using System.Net;
using System.Net.Sockets;

using LillTek.Common;

namespace LillTek.Net.Radius
{
    /// <summary>
    /// The RADIUS message attribute base class.
    /// </summary>
    public class RadiusAttribute
    {
        //---------------------------------------------------------------------
        // Static members

        /// <summary>
        /// The maximum length of an attribute value.
        /// </summary>
        internal const int MaxValueLen = 255 - 2;    // 255 - the byte type and length header fields

        /// <summary>
        /// Parses an attribute from the specified position in the byte array,
        /// advancing the position past the attribute.
        /// </summary>
        /// <param name="packet">The RADIUS packet.</param>
        /// <param name="pos">Index of the first attribute byte.</param>
        /// <returns>The parsed packet.</returns>
        internal static RadiusAttribute Parse(byte[] packet, ref int pos)
        {
            int     type;
            int     cb;
            byte[]  value;

            try
            {
                type  = packet[pos++];
                cb    = packet[pos++];
                value = Helper.Extract(packet, pos, cb);
                pos  += cb;

                return new RadiusAttribute((RadiusAttributeType)type, value);
            }
            catch (Exception e)
            {
                throw new RadiusException(packet, e, "Bad RADIUS packet");
            }
        }

        //---------------------------------------------------------------------
        // Instance members

        /// <summary>
        /// The atteibute type.
        /// </summary>
        public readonly RadiusAttributeType Type;

        /// <summary>
        /// The raw attribute data bytes.
        /// </summary>
        public readonly byte[] Value;

        /// <summary>
        /// Constructs an attribute with a binary value.
        /// </summary>
        /// <param name="type">The RADIUS attribute type code.</param>
        /// <param name="value">The raw attribute data.</param>
        /// <exception cref="RadiusException">Thrown if the value size exceeds 253 bytes.</exception>
        public RadiusAttribute(RadiusAttributeType type, byte[] value)
        {
            this.Type  = type;
            this.Value = value;

            if (value.Length > MaxValueLen)
                throw new RadiusException("Attribute data size exceeds 253 bytes.");
        }

        /// <summary>
        /// Constructs an attribute with a 32-bit integer value.
        /// </summary>
        /// <param name="type">The RADIUS attribute type code.</param>
        /// <param name="value">The integer attribute data.</param>
        public RadiusAttribute(RadiusAttributeType type, int value)
        {
            this.Type  = type;
            this.Value = new byte[] { (byte)((value >> 24) & 0xFF), (byte)((value >> 16) & 0xFF), (byte)((value >> 8) & 0xFF), (byte)(value & 0xFF) };
        }

        /// <summary>
        /// Constructs an attribute with a IP Address as the value.
        /// </summary>
        /// <param name="type">The RADIUS attribute type code.</param>
        /// <param name="value">The IP address.</param>
        /// <exception cref="RadiusException">Thrown for IPv6 addresses.</exception>
        public RadiusAttribute(RadiusAttributeType type, IPAddress value)
        {
            this.Type  = type;
            this.Value = value.GetAddressBytes();

            if (this.Value.Length != 4)
                throw new RadiusException("RADIUS does not support IPv6 addresses.");
        }

        /// <summary>
        /// Constructs an attribute with a string value encoded as UTF-8.
        /// </summary>
        /// <param name="type">The RADIUS attribute type code.</param>
        /// <param name="value">The attribute value.</param>
        /// <exception cref="RadiusException">Thrown if the value size exceeds 253 bytes.</exception>
        public RadiusAttribute(RadiusAttributeType type, string value)
        {
            this.Type  = type;
            this.Value = Helper.ToUTF8(value);

            if (value.Length > MaxValueLen)
                throw new RadiusException("Attribute data size exceeds 253 bytes.");
        }
    }
}
