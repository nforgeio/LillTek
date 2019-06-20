//-----------------------------------------------------------------------------
// FILE:        DynDnsMessage.cs
// CONTRIBUTOR: Jeff Lill
// COPYRIGHT:   Copyright (c) 2005-2015 by Jeffrey Lill.  All rights reserved.
// DESCRIPTION: Internal class used by DynDnsClient to encode host registration
//              messages sent to Dynamic DNS servers via UDP.

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;

using LillTek.Common;
using LillTek.Cryptography;
using LillTek.Net.Sockets;

namespace LillTek.Datacenter
{

    /// <summary>
    /// <para>
    /// Internal class used by <see cref="DynDnsClient" /> to encode host registration
    /// messages sent to Dynamic DNS servers via UDP.
    /// </para>
    /// <note>
    /// Although this class is <b>public</b> it is not direct intended for use outside
    /// of the LillTek Platform codebase.
    /// </note>
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
    ///     |   Magic Number   |    32-bits: 0x317F1A11
    ///     +------------------+
    ///     |     Version      |    8-bits: Message format version (0)
    ///     +------------------+
    ///     |    Timestamp     |    64-bits: .NET tick count (UTC)
    ///     +------------------+
    ///     |      Flags       |    32-bits: DynDnsMessageFlag bits
    ///     +------------------+
    ///     |      Length      |    16-bits: Length of the serialized host entry
    ///     +------------------+
    ///     |                  |
    ///     |       Host       |    The host entry serialized to a 
    ///     |       Entry      |    string and encoded as UTF-8
    ///     |                  |
    ///     +------------------+
    ///     |       Salt       |    32-bits: Cryptographic salt
    ///     +----------------- +
    /// </code>
    /// </remarks>
    public class DynDnsMessage
    {
        private const int Magic     = 0x317F1A11;
        private const int FormatVer = 0;

        /// <summary>
        /// The time the message was sent (UTC).
        /// </summary>
        public DateTime TimeStampUtc { get; set; }

        /// <summary>
        /// Message format version number.
        /// </summary>
        public int Version { get; private set; }

        /// <summary>
        /// Specifies the operation to be performed in addition to 
        /// operation related options.
        /// </summary>
        public DynDnsMessageFlag Flags { get; set; }

        /// <summary>
        /// The DNS host information to be registered.
        /// </summary>
        public DynDnsHostEntry HostEntry { get; set; }

        /// <summary>
        /// Decrypts and deserializes a message from a byte buffer.
        /// </summary>
        /// <param name="buffer">The buffer.</param>
        /// <param name="sharedKey">The shared encryption key.</param>
        /// <exception cref="FormatException">Thrown if the buffer does not contain a valid message.</exception>
        public DynDnsMessage(byte[] buffer, SymmetricKey sharedKey)
        {
            try
            {
                using (var ms = new EnhancedMemoryStream(Crypto.Decrypt(buffer, sharedKey)))
                {
                    if (ms.ReadInt32Le() != Magic)
                        throw new Exception();

                    this.Version = ms.ReadByte();
                    if (this.Version < FormatVer)
                        throw new FormatException(string.Format("DynDnsMessage version [{0}] is not supported.", this.Version));

                    this.TimeStampUtc = new DateTime(ms.ReadInt64Le());
                    this.Flags        = (DynDnsMessageFlag)ms.ReadInt32Le();
                    this.HostEntry    = new DynDnsHostEntry(ms.ReadString16());
                }
            }
            catch (Exception e)
            {
                throw new FormatException("Invalid Dynamic DNS message.", e);
            }
        }

        /// <summary>
        /// Default constructor.
        /// </summary>
        public DynDnsMessage()
            : this(DynDnsMessageFlag.OpRegister, null)
        {
        }

        /// <summary>
        /// Constructor.
        /// </summary>
        /// <param name="flags">Specifies the operation to be performed in addition to operation related options.</param>
        /// <param name="hostEntry">The host entry information.</param>
        public DynDnsMessage(DynDnsMessageFlag flags, DynDnsHostEntry hostEntry)
        {
            this.TimeStampUtc = DateTime.UtcNow;
            this.Version      = FormatVer;
            this.Flags        = flags;
            this.HostEntry    = hostEntry;
        }

        /// <summary>
        /// Serializes and encrypts the message into a byte array.
        /// </summary>
        /// <param name="sharedKey">The shared encryption key.</param>
        /// <returns>The serialized message.</returns>
        public byte[] ToArray(SymmetricKey sharedKey)
        {
            if (this.HostEntry == null)
                throw new InvalidOperationException("HostEntry property cannot be null.");

            using (var ms = new EnhancedMemoryStream(2048))
            {
                ms.WriteInt32Le(Magic);
                ms.WriteByte((byte)FormatVer);
                ms.WriteInt64Le(TimeStampUtc.Ticks);
                ms.WriteInt32Le((int)Flags);
                ms.WriteString16(this.HostEntry.ToString());
                ms.WriteBytesNoLen(Crypto.GetSalt4());

                return Crypto.Encrypt(ms.ToArray(), sharedKey);
            }
        }
    }
}
