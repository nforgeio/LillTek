//-----------------------------------------------------------------------------
// FILE:        RadiusPacket.cs
// CONTRIBUTOR: Jeff Lill
// COPYRIGHT:   Copyright (c) 2005-2015 by Jeffrey Lill.  All rights reserved.
// DESCRIPTION: Implements a RADIUS UDP message packet.

using System;
using System.Collections.Generic;
using System.Net;
using System.Text;

using LillTek.Common;
using LillTek.Cryptography;

namespace LillTek.Net.Radius
{
    /// <summary>
    /// Implements a RADIUS UDP message packet.
    /// </summary>
    public sealed class RadiusPacket
    {
        private const int HeaderSize = 20;      // Byte size of a packet header including the 
                                                // code, identifier, length, and authenticator

        /// <summary>
        /// The RADIUS message code.
        /// </summary>
        public readonly RadiusCode Code;

        /// <summary>
        /// Used to aid matching requests and replies value (0..255).
        /// </summary>
        public readonly int Identifier;

        /// <summary>
        /// 16 byte autheticator used for hiding passwords.
        /// </summary>
        public byte[] Authenticator;

        /// <summary>
        /// The packet attributes.
        /// </summary>
        public readonly List<RadiusAttribute> Attributes;

        /// <summary>
        /// The source endpoint for received packets.
        /// </summary>
        public readonly IPEndPoint SourceEP;

        /// <summary>
        /// Constructs a RADIUS packet from the parameters passed.
        /// </summary>
        /// <param name="code">The RADIUS message code.</param>
        /// <param name="identifier">Used to aid matching requests and replies value (0..255).</param>
        /// <param name="authenticator">16 byte autheticator used for hiding passwords.</param>
        /// <param name="attributes">The packet attributes.</param>
        /// <remarks>
        /// <note>
        /// <b>authenticator</b> may be passed as null in situations where 
        /// a response authenticator will be computed after the packet is fully 
        /// initialized via the <see cref="ComputeResponseAuthenticator" /> method.
        /// </note>
        /// </remarks>
        public RadiusPacket(RadiusCode code, int identifier, byte[] authenticator, params RadiusAttribute[] attributes)
        {
            if (identifier < 0 || identifier > 255)
                throw new ArgumentException("[identifier] must be in the range of 0..255");

            if (authenticator == null)
                authenticator = new byte[16];

            if (authenticator.Length != 16)
                throw new ArgumentException("[authenticator] must be 16 bytes.");

            this.Code = code;
            this.Identifier = identifier;
            this.Authenticator = authenticator;
            this.SourceEP = null;

            this.Attributes = new List<RadiusAttribute>();
            for (int i = 0; i < attributes.Length; i++)
                this.Attributes.Add(attributes[i]);
        }

        /// <summary>
        /// Constructs a RADIUS packet by parsing the raw UDP packet bytes passed.
        /// </summary>
        /// <param name="sourceEP">The source endpoint for received packets.</param>
        /// <param name="raw">The raw UDP packet.</param>
        /// <param name="length">Size of the raw packet in bytes.</param>
        public RadiusPacket(IPEndPoint sourceEP, byte[] raw, int length)
        {
            int     pos;
            int     len;

            this.SourceEP = sourceEP;

            // Parse the packet header fields

            if (length < HeaderSize)
                throw new RadiusException(raw, "Bad RADIUS packet: length < 20");

            this.Code       = (RadiusCode)raw[0];
            this.Identifier = raw[1];

            len = (raw[2] << 8) | raw[3];
            if (len > length)
                throw new RadiusException(raw, "Bad RADIUS packet: truncated");

            length = len;

            this.Authenticator = Helper.Extract(raw, 4, 16);

            // Parse the packet attributes

            this.Attributes = new List<RadiusAttribute>();

            pos = HeaderSize;
            while (pos < length - 2)
            {
                RadiusAttributeType     aType;
                int                     aLen;
                byte[]                  aValue;

                aType = (RadiusAttributeType)raw[pos++];
                aLen = raw[pos++] - 2;

                if (aLen < 0)
                    throw new RadiusException(raw, "Bad RADIUS packet: attribute length < 2");

                if (pos + aLen > length)
                    throw new RadiusException(raw, "Bad RADIUS packet: attribute extends past packet");

                aValue = Helper.Extract(raw, pos, aLen);
                pos += aLen;

                this.Attributes.Add(new RadiusAttribute(aType, aValue));
            }
        }

        /// <summary>
        /// Renders the packet into a form suitable for transmission via UDP.
        /// </summary>
        /// <returns>The raw packet byte array.</returns>
        public byte[] ToArray()
        {
            var bs = new EnhancedBlockStream(0, 2048);

            bs.WriteByte((byte)this.Code);
            bs.WriteByte((byte)this.Identifier);
            bs.WriteInt16(0);   // Put a zero in for the length and come back and fill
            // this in after we know what the actual length is.

            bs.WriteBytesNoLen(this.Authenticator);

            for (int i = 0; i < this.Attributes.Count; i++)
            {
                var attr = this.Attributes[i];

                if (attr.Value.Length > RadiusAttribute.MaxValueLen)
                    throw new RadiusException("Attribute value size exceeds 253 bytes.");

                bs.WriteByte((byte)attr.Type);
                bs.WriteByte((byte)(attr.Value.Length + 2));
                bs.WriteBytesNoLen(attr.Value);
            }

            // Go back and write the actual length

            if (bs.Length > short.MaxValue)
                throw new RadiusException("RADIUS packet is too large.");

            bs.Position = 2;
            bs.WriteInt16((int)bs.Length);

            return bs.ToArray();
        }

        /// <summary>
        /// Searches the set of packet attributes for the first attribute with
        /// the specified type and returns its value as a TEXT string.
        /// </summary>
        /// <param name="type">The desired attribute type.</param>
        /// <param name="value">This will be filled in with the attribute value.</param>
        /// <returns><c>true</c> if the attribute was found.</returns>
        public bool GetAttributeAsText(RadiusAttributeType type, out string value)
        {
            for (int i = 0; i < this.Attributes.Count; i++)
                if (this.Attributes[i].Type == type)
                {
                    value = Helper.FromUTF8(this.Attributes[i].Value);
                    return true;
                }

            value = null;
            return false;
        }

        /// <summary>
        /// Searches the set of packet attributes for the first attribute with
        /// the specified type and returns its value as a TEXT string.
        /// </summary>
        /// <param name="type">The desired attribute type.</param>
        /// <param name="value">This will be filled in with the attribute value.</param>
        /// <returns><c>true</c> if the attribute was found.</returns>
        public bool GetAttributeAsBinary(RadiusAttributeType type, out byte[] value)
        {
            for (int i = 0; i < this.Attributes.Count; i++)
                if (this.Attributes[i].Type == type)
                {
                    value = Helper.Extract(this.Attributes[i].Value, 0);
                    return true;
                }

            value = null;
            return false;
        }

        /// <summary>
        /// Searches the set of packet attributes for the first attribute with
        /// the specified type and returns its value as a TEXT string.
        /// </summary>
        /// <param name="type">The desired attribute type.</param>
        /// <param name="value">This will be filled in with the attribute value.</param>
        /// <returns><c>true</c> if the attribute was found.</returns>
        public bool GetAttributeAsAddress(RadiusAttributeType type, out IPAddress value)
        {
            for (int i = 0; i < this.Attributes.Count; i++)
                if (this.Attributes[i].Type == type)
                {
                    value = new IPAddress(this.Attributes[i].Value);
                    return true;
                }

            value = IPAddress.Any;
            return false;
        }

        /// <summary>
        /// Searches the set of packet attributes for the first attribute with
        /// the specified type and returns its value as a TEXT string.
        /// </summary>
        /// <param name="type">The desired attribute type.</param>
        /// <param name="value">This will be filled in with the attribute value.</param>
        /// <returns><c>true</c> if the attribute was found.</returns>
        public bool GetAttributeAsInteger(RadiusAttributeType type, out int value)
        {
            for (int i = 0; i < this.Attributes.Count; i++)
                if (this.Attributes[i].Type == type)
                {
                    int pos = 0;

                    value = Helper.ReadInt32(this.Attributes[i].Value, ref pos);
                    return true;
                }

            value = 0;
            return false;
        }

        /// <summary>
        /// Searches the set of packet attributes for the first attribute with
        /// the specified type and returns its value as a TEXT string.
        /// </summary>
        /// <param name="type">The desired attribute type.</param>
        /// <param name="value">This will be filled in with the attribute value.</param>
        /// <returns><c>true</c> if the attribute was found.</returns>
        public bool GetAttributeAsTime(RadiusAttributeType type, out DateTime value)
        {
            for (int i = 0; i < this.Attributes.Count; i++)
                if (this.Attributes[i].Type == type)
                {
                    int     pos = 0;
                    int     time;

                    time = Helper.ReadInt32(this.Attributes[i].Value, ref pos);
                    value = UnixTime.FromSeconds(time);

                    return true;
                }

            value = UnixTime.TimeZero;
            return false;
        }

        /// <summary>
        /// Computes, sets, and returns the response authenticator for the packet.
        /// </summary>
        /// <param name="request">The corresponding request packet.</param>
        /// <param name="secret">The shared secret for the NAS.</param>
        /// <returns>The computed response authenticator.</returns>
        /// <remarks>
        /// This is computed via a MD5 hash over the serialized request 
        /// packet plus the serialized response attributes, plus the 
        /// shared secret.
        /// </remarks>
        public byte[] ComputeResponseAuthenticator(RadiusPacket request, string secret)
        {
            byte[]      serialized = this.ToArray();
            byte[]      bytes;

            bytes = Helper.Concat(Helper.Extract(serialized, 0, 4), request.Authenticator);
            bytes = Helper.Concat(bytes, Helper.Extract(serialized, HeaderSize));
            bytes = Helper.Concat(bytes, Helper.ToAnsi(secret));

            return this.Authenticator = MD5Hasher.Compute(bytes);
        }

        /// <summary>
        /// Verifies that the response authenticator in this packet (the response
        /// packet) is valid for the specified request packet and shared secret.
        /// </summary>
        /// <param name="request">The corresponding request packet.</param>
        /// <param name="secret">The shared secret.</param>
        /// <returns><c>true</c> if the response authenticator is valid.</returns>
        public bool VerifyResponseAuthenticator(RadiusPacket request, string secret)
        {
            return Helper.ArrayEquals(this.Authenticator, ComputeResponseAuthenticator(request, secret));
        }

        /// <summary>
        /// Encrypts a user password string by combining it with the shared NAS
        /// secret and the message authenticator as described in RFC 2865 page 27.
        /// </summary>
        /// <param name="userPassword">The user password to be encrypted.</param>
        /// <param name="secret">The shared NAS secret.</param>
        /// <returns>The encrypted password.</returns>
        /// <exception cref="RadiusException">Thrown if the password is too large or too small.</exception>
        public byte[] EncryptUserPassword(string userPassword, string secret)
        {
            var     bs          = new EnhancedBlockStream(128, 128);
            byte[]  secretBytes = Helper.ToAnsi(secret);
            byte[]  cypherBlock = new byte[16];
            byte[]  rawPwd;
            byte[]  xorHash;
            byte[]  pwd;
            int     pos;

            try
            {
                rawPwd = Helper.ToAnsi(userPassword);
                if (rawPwd.Length == 0)
                    throw new RadiusException("Zero length password is not allowed.");

                // Copy userPassword into pwd, padding the result out with zeros
                // to a multiple of 16 bytes

                if (rawPwd.Length % 16 == 0)
                    pwd = rawPwd;
                else
                {
                    pwd = new byte[(rawPwd.Length / 16 + 1) * 16];
                    Array.Copy(rawPwd, pwd, rawPwd.Length);
                }

                // The first XOR hash is MD5(secret + authenticator)

                xorHash = MD5Hasher.Compute(Helper.Concat(secretBytes, this.Authenticator));

                // Perform the encryption

                pos = 0;
                while (true)
                {
                    // Cyperblock = XOR hash ^ next 16 bytes of password

                    for (int i = 0; i < 16; i++)
                        cypherBlock[i] = (byte)(xorHash[i] ^ pwd[pos + i]);

                    bs.WriteBytesNoLen(cypherBlock);

                    pos += 16;
                    if (pos >= pwd.Length)
                        break;

                    // Next XOR hash is MD5(secret + cypherblock)

                    xorHash = MD5Hasher.Compute(Helper.Concat(secretBytes, cypherBlock));
                }

                if (bs.Length > 128)
                    throw new RadiusException("Encrypted password exceeds 128 bytes.");

                return bs.ToArray();
            }
            finally
            {

                bs.Close();
            }
        }

        /// <summary>
        /// Decrypts a user password by encrypted using <see cref="EncryptUserPassword" />
        /// using the message authenticator and the shared NAS secret.
        /// </summary>
        /// <param name="encryptedPassword">The encrypted password.</param>
        /// <param name="secret">The shared NAS secret.</param>
        /// <returns>The decrypted password.</returns>
        /// <exception cref="RadiusException">Thrown if the encrypted password is invalid.</exception>
        public string DecryptUserPassword(byte[] encryptedPassword, string secret)
        {
            var     bs          = new EnhancedBlockStream(128, 128);
            byte[]  secretBytes = Helper.ToAnsi(secret);
            byte[]  clearBlock  = new byte[16];
            byte[]  xorHash;
            byte[]  decrypted;
            int     pos;
            int     zeroPos;

            try
            {
                // The encrypted password length must be a non-zero multiple of 16 bytes.

                if (encryptedPassword.Length == 0 || encryptedPassword.Length % 16 != 0)
                    throw new RadiusException("Encrypted user password length must be a positive multiple of 16 bytes.");

                // The first XOR hash is MD5(secret + authenticator)

                xorHash = MD5Hasher.Compute(Helper.Concat(secretBytes, this.Authenticator));

                // Perform the decryption.  The trick here is to unmunge the 16 byte
                // blocks by XORing them with the current XOR hash.

                pos = 0;
                while (true)
                {
                    // clearBlock = XOR hash ^ next 16 encrypted bytes

                    for (int i = 0; i < 16; i++)
                        clearBlock[i] = (byte)(xorHash[i] ^ encryptedPassword[pos + i]);

                    bs.WriteBytesNoLen(clearBlock);

                    pos += 16;
                    if (pos >= encryptedPassword.Length)
                        break;

                    // Next XOR hash = MD5(secret + last cypherblock)

                    xorHash = MD5Hasher.Compute(Helper.Concat(secretBytes, Helper.Extract(encryptedPassword, pos - 16, 16)));
                }

                // Scan forward to the first zero byte.  If we find one, we're going
                // to assume that it was a padding byte.

                decrypted = bs.ToArray();
                zeroPos = -1;

                for (int i = 0; i < decrypted.Length; i++)
                    if (decrypted[i] == 0)
                    {
                        zeroPos = i;
                        break;
                    }

                if (zeroPos == -1)
                    return Helper.FromAnsi(decrypted);
                else
                    return Helper.FromAnsi(decrypted, 0, zeroPos);
            }
            finally
            {
                bs.Close();
            }
        }
    }
}
