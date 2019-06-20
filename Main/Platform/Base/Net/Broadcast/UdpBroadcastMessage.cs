//-----------------------------------------------------------------------------
// FILE:        UdpBroadcastMessage.cs
// CONTRIBUTOR: Jeff Lill
// COPYRIGHT:   Copyright (c) 2005-2015 by Jeffrey Lill.  All rights reserved.
// DESCRIPTION: Internal class used by UdpBroadcastClients and UdpBroadcastServers
//              to communicate with each other.

using System;
using System.IO;
using System.Text;
using System.Net;
using System.Net.Sockets;
using System.Threading;
using System.Diagnostics;
using System.Collections.Generic;

using LillTek.Common;
using LillTek.Cryptography;
using LillTek.Net.Sockets;

namespace LillTek.Net.Broadcast
{
    /// <summary>
    /// Internal class used by <see cref="UdpBroadcastClient" />s and <see cref="UdpBroadcastServer" />s
    /// to communicate with each other.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Messages passed between UDP broadcast clients and servers are encrypted with a shared key
    /// and include a timestamp as well as 32-bits of cryptographic salt as a way to avoid
    /// message tampering and replay attacks.  The unencrypted format for these messages on the wire 
    /// depicted below.
    /// </para>
    /// <code language="none">
    ///     +------------------+
    ///     |   Magic Number   |    32-bits: 0x7BB1AA21
    ///     +------------------+
    ///     |    Timestamp     |    64-bits: .NET tick count (UTC)
    ///     +------------------+
    ///     |  Source Address  |    32-bits: IPv4 address of the packet source
    ///     +------------------+
    ///     |   Message Type   |    8-bits: UdpBroadcastMessageType
    ///     +------------------+
    ///     |    Broadcast     |    8-bits: Broadcast group
    ///     |      Group       |
    ///     +------------------+
    ///     |      Length      |    16-bits: Length of payload data
    ///     +------------------+
    ///     |                  |
    ///     |                  |
    ///     |     Payload      |
    ///     |                  |
    ///     |                  |
    ///     +------------------+
    ///     |       Salt       |    32-bits: Cryptographic salt
    ///     +----------------- +
    /// </code>
    /// </remarks>
    internal class UdpBroadcastMessage
    {
        private const int Magic = 0x7BB1AA21;

        private static byte[] EmptyPayload = new byte[0];

        /// <summary>
        /// Mazimum message envelope size in bytes.
        /// </summary>
        public const int EnvelopeSize = 4   // Magic Number
                                      + 8   // Timestamp
                                      + 4   // Source Address
                                      + 1   // Message Type
                                      + 1   // Broadcast Group
                                      + 2   // Payload length
                                      + 4   // Salt
                                      + 8;  // Maximum encryption padding (a guess)

        /// <summary>
        /// Specifies the message type.
        /// </summary>
        public UdpBroadcastMessageType MessageType { get; private set; }

        /// <summary>
        /// The time the message was sent (UTC).
        /// </summary>
        public DateTime TimeStampUtc { get; set; }

        /// <summary>
        /// IP address of the packet source.
        /// </summary>
        public IPAddress SourceAddress { get; set; }

        /// <summary>
        /// The broadcast group identifier (only values between 0..255 are valid).
        /// </summary>
        public int BroadcastGroup { get; private set; }

        /// <summary>
        /// The broadcast packet payload.
        /// </summary>
        public byte[] Payload { get; private set; }

        /// <summary>
        /// Decrypts and deserializes a message from a byte buffer.
        /// </summary>
        /// <param name="buffer">The buffer.</param>
        /// <param name="sharedKey">The shared encryption key.</param>
        /// <exception cref="FormatException">Thrown if the buffer does not contain a valid message.</exception>
        public UdpBroadcastMessage(byte[] buffer, SymmetricKey sharedKey)
        {
            try
            {
                using (var ms = new EnhancedMemoryStream(Crypto.Decrypt(buffer, sharedKey)))
                {
                    if (ms.ReadInt32Le() != Magic)
                        throw new Exception();

                    this.TimeStampUtc   = new DateTime(ms.ReadInt64Le());
                    this.SourceAddress  = new IPAddress(ms.ReadBytes(4));
                    this.MessageType    = (UdpBroadcastMessageType)ms.ReadByte();
                    this.BroadcastGroup = ms.ReadByte();
                    this.Payload        = ms.ReadBytes16();
                }
            }
            catch (Exception e)
            {
                throw new FormatException("Invalid UDP broadcast message.", e);
            }
        }

        /// <summary>
        /// Constructor.
        /// </summary>
        /// <param name="messageType">The message type.</param>
        public UdpBroadcastMessage(UdpBroadcastMessageType messageType)
            : this(messageType, IPAddress.Any, 0, null)
        {
        }

        /// <summary>
        /// Constructor.
        /// </summary>
        /// <param name="messageType">The message type.</param>
        /// <param name="broadcastGroup">The broadcast group.</param>
        public UdpBroadcastMessage(UdpBroadcastMessageType messageType, int broadcastGroup)
            : this(messageType, IPAddress.Any, broadcastGroup, null)
        {
        }

        /// <summary>
        /// Constructor.
        /// </summary>
        /// <param name="messageType">The message type.</param>
        /// <param name="sourceAddress">The IPv4 address of the packet source.</param>
        /// <param name="broadcastGroup">The broadcast group.</param>
        /// <param name="payload">The broadcast packet payload.</param>
        /// <exception cref="InvalidOperationException">Thrown if an non IPv4 address is set as the packet source.</exception>
        public UdpBroadcastMessage(UdpBroadcastMessageType messageType, IPAddress sourceAddress, int broadcastGroup, byte[] payload)
        {
            if (payload == null)
                payload = EmptyPayload;

            this.MessageType    = messageType;
            this.TimeStampUtc   = DateTime.UtcNow;
            this.SourceAddress  = sourceAddress;
            this.BroadcastGroup = broadcastGroup;
            this.Payload        = payload;
        }

        /// <summary>
        /// Serializes and encrypts the message into a byte array.
        /// </summary>
        /// <param name="sharedKey">The shared encryption key.</param>
        /// <returns>The serialized message.</returns>
        public byte[] ToArray(SymmetricKey sharedKey)
        {
            using (var ms = new EnhancedMemoryStream(2048))
            {
                var addressBytes = SourceAddress.GetAddressBytes();

                if (addressBytes.Length != 4)
                    throw new InvalidOperationException("UdpBroadcastMessage supports only IPv4 address.");

                ms.WriteInt32Le(Magic);
                ms.WriteInt64Le(TimeStampUtc.Ticks);
                ms.WriteBytesNoLen(SourceAddress.GetAddressBytes());
                ms.WriteByte((byte)MessageType);
                ms.WriteByte((byte)BroadcastGroup);
                ms.WriteBytes16(Payload);
                ms.WriteBytesNoLen(Crypto.GetSalt4());

                return Crypto.Encrypt(ms.ToArray(), sharedKey);
            }
        }
    }
}
