//-----------------------------------------------------------------------------
// FILE:        SecureTicket.cs
// CONTRIBUTOR: Jeff Lill
// COPYRIGHT:   Copyright (c) 2005-2015 by Jeffrey Lill.  All rights reserved.
// DESCRIPTION: Implements a generic mechanism for generating time limited
//              secure tickets to be used for securing access to resources.

using System;
using System.Collections.Generic;
using System.IO;
using System.Security.Cryptography;
using System.Text;

using LillTek.Common;

namespace LillTek.Cryptography
{
    /// <summary>
    /// Implements a generic mechanism for generating time limited secure tickets 
    /// to be used for securing access to resources.
    /// </summary>
    /// <remarks>
    /// <para>
    /// A ticket is data to be passed along with a request to access a resource.
    /// The ticket encodes enough information so that the server can authorize
    /// access to the resource.  The ticket is encrypted in such a way that 
    /// it cannot be tampered with or used to gain access to another resource.
    /// </para>
    /// <para>
    /// All tickets include three fields: the ticket the <see cref="Lifespan" />, the
    /// <see cref="IssuerExpirationUtc" /> and <see cref="ClientExpirationUtc" /> properties 
    /// and the <see cref="Resource" /> which is a string identifying the resource being 
    /// accessed.  Applications can save additional name/value pairs using the indexer as well as 
    /// the <see cref="Set" /> and <see cref="Get" /> members.  The name keys are case insensitive.
    /// </para>
    /// <para>
    /// The <see cref="IssuerExpirationUtc" /> property is set by the issuer when the ticket
    /// is created and is relative to the issuer's clock.  <see cref="ClientExpirationUtc" />
    /// is set when a client instantiates a received ticket and is relative to the client's
    /// clock.  These properties can be used by the issuer to determine whether the ticket
    /// has expired and by the client to determine whether a ticket is close to being expired
    /// and should be renewed.
    /// </para>
    /// <note>
    /// Issuers should not cache tickets and reissue them to clients because clients
    /// compute the local expiration date based on the current client clock plus the 
    /// lifespan of the ticket.  This means that the local and issuer ticket expiration
    /// dates will be out of sync if the issuer has cached the ticket for some time.
    /// </note>
    /// <note>
    /// Issuers and clients should be aware that it may take some time for tickets
    /// to be delivered and that there may be some clock skew on the client and issuer
    /// as well skew between the clocks of multiple issuer servers.  Ticket lifespans should
    /// be large enough to account for this and clients should renew tickets well before
    /// they expire.
    /// </note>
    /// <para><b><u>Serialized Format</u></b></para>
    /// <para>
    /// Tickets are serialized to a byte array or base-64 encoded string representation
    /// of the byte array.  One portion of this byte array is the encrypted private
    /// ticket information.  A second part holds plaintext public information.
    /// </para>
    /// <para>
    /// The encrypted information available only to the ticket issuer includes:
    /// </para>
    /// <list type="bullet">
    ///     <item>
    ///     The <see cref="Resource" /> property.
    ///     </item>
    ///     <item>
    ///     The <see cref="IssuerExpirationUtc" /> property.
    ///     </item>
    ///     <item>
    ///     The <see cref="Lifespan" /> property.
    ///     </item>
    ///     <item>
    ///     All custom properties added to the ticket.
    ///     </item>
    /// </list>
    /// <para>
    /// The <see cref="Resource" />, <see cref="Lifespan" />, and <see cref="ClientExpirationUtc" />
    /// properties are also encoded into the plaintext section of the ticket so this information
    /// will be available to client applications.  The reason for this is that client applications
    /// may wish to use this information to decide when it is necessary to renew a ticket that
    /// will expire soon.
    /// </para>
    /// <para>
    /// The ticket is serialized into bytes as follows:
    /// </para>
    /// <code language="none">
    /// +-------------------+
    /// |    cbEncrypted    |   16-bits (big-endian)
    /// +-------------------+
    /// |                   |
    /// |    Encrypted      |
    /// |   ArgCollection   |
    /// |   with 8-bytes    |
    /// |     of salt       |
    /// |                   |
    /// +-------------------+
    /// |    cbPlainText    |   16-bits (big-endian)
    /// +-------------------+
    /// |                   |
    /// |     PlainText     |
    /// |   ArgCollection   |
    /// |                   |
    /// +-------------------+
    /// </code>
    /// <para>
    /// with the secured information rendered into UTF-8 by <see cref="ArgCollection" />
    /// and then encoded by <see cref="Crypto.EncryptStringWithSalt8(string,SymmetricKey)" /> and the unsecured information
    /// rendered into UTF-8 and simply appended to the ticket.
    /// </para>
    /// </remarks>
    public class SecureTicket
    {
        //---------------------------------------------------------------------
        // Static members

        /// <summary>
        /// Used by issuers to decrypt and parse a ticket from a byte array.
        /// </summary>
        /// <param name="key">The symmetric encryption key.</param>
        /// <param name="encrypted">The encrypted ticket.</param>
        /// <returns>The decrypted ticket.</returns>
        /// <exception cref="CryptographicException">Thrown if the ticket is improperly formatted or has been tampered with.</exception>
        public static SecureTicket Parse(SymmetricKey key, byte[] encrypted)
        {
            return new SecureTicket(key, encrypted);
        }

        /// <summary>
        /// Used by issues to decrypt and parse a ticket from a base-64 encoded byte array.
        /// </summary>
        /// <param name="key">The symmetric encryption key.</param>
        /// <param name="base64Encrypted">The base 64 encoded encrypted ticket.</param>
        /// <returns>The decrypted ticket.</returns>
        /// <exception cref="CryptographicException">Thrown if the ticket is improperly formatted or has been tampered with.</exception>
        public static SecureTicket Parse(SymmetricKey key, string base64Encrypted)
        {
            return new SecureTicket(key, base64Encrypted);
        }

        /// <summary>
        /// Used by clients to parse the public properties of a ticket from a byte array.
        /// </summary>
        /// <param name="encrypted">The encrypted ticket.</param>
        /// <returns>The decrypted ticket.</returns>
        /// <exception cref="CryptographicException">Thrown if the ticket is improperly formatted or has been tampered with.</exception>
        public static SecureTicket Parse(byte[] encrypted)
        {
            return new SecureTicket(encrypted);
        }

        /// <summary>
        /// Used by clients to parse the public properties of a ticket from a base-64 encoded byte array.
        /// </summary>
        /// <param name="base64Encrypted">The base 64 encoded encrypted ticket.</param>
        /// <returns>The decrypted ticket.</returns>
        /// <exception cref="CryptographicException">Thrown if the ticket is improperly formatted or has been tampered with.</exception>
        public static SecureTicket Parse(string base64Encrypted)
        {
            return new SecureTicket(base64Encrypted);
        }

        //---------------------------------------------------------------------
        // Instance members

        private ArgCollection   args = null;
        private DateTime        clientExpirationUtc;    // Client-side expiration
        private byte[]          clientEncrypted;        // Client-side encrypted ticket

        /// <summary>
        /// Used by an issuer to construct a ticket.
        /// </summary>
        /// <param name="resource">Identifies resource being protected.</param>
        /// <param name="lifespan">Lifetime of the ticket.</param>
        public SecureTicket(string resource, TimeSpan lifespan)
        {
            DateTime issuerExpirationUtc;

            if (lifespan <= TimeSpan.Zero)
                throw new ArgumentException("Ticket lifespan must be positive.");

            issuerExpirationUtc = DateTime.UtcNow + lifespan;
            clientExpirationUtc = issuerExpirationUtc;

            args = new ArgCollection('=', '\t');

            args.Set("_resource", resource);
            args.Set("_lifespan", lifespan);
            args.Set("_issuerTTD", issuerExpirationUtc);
        }

        /// <summary>
        /// Used by an issuer to construct a ticket by decrypting one from a byte array.
        /// </summary>
        /// <param name="key">The symmetric encryption key.</param>
        /// <param name="encrypted">The encrypted ticket.</param>
        /// <exception cref="CryptographicException">Thrown if the ticket is improperly formatted or has been tampered with.</exception>
        /// <remarks>
        /// The <see cref="ClientExpirationUtc" /> property will be computed by adding 
        /// <see cref="Lifespan" /> to the local UTC time.
        /// </remarks>
        public SecureTicket(SymmetricKey key, byte[] encrypted)
        {
            try
            {
                int pos;

                pos       = 0;
                encrypted = Helper.ReadBytes16(encrypted, ref pos);
                args      = ArgCollection.Parse(Crypto.DecryptStringWithSalt8(encrypted, key), '=', '\t');

                clientExpirationUtc = DateTime.UtcNow + this.Lifespan;
            }
            catch (Exception e)
            {

                throw new CryptographicException("Invalid ticket", e);
            }
        }

        /// <summary>
        /// Used by an issuer to construct a ticket by decrypting one from a base 64 encoded byte array.
        /// </summary>
        /// <param name="key">The symmetric encryption key.</param>
        /// <param name="base64Encrypted">The base 64 encoded encrypted ticket.</param>
        /// <exception cref="CryptographicException">Thrown if the ticket is improperly formatted or has been tampered with.</exception>
        public SecureTicket(SymmetricKey key, string base64Encrypted)
            : this(key, Convert.FromBase64String(base64Encrypted))
        {
        }

        /// <summary>
        /// Used by a client to construct a ticket by reading the public section from a byte array.
        /// </summary>
        /// <param name="encrypted">The encrypted ticket.</param>
        /// <exception cref="CryptographicException">Thrown if the ticket is improperly formatted or has been tampered with.</exception>
        /// <remarks>
        /// The <see cref="ClientExpirationUtc" /> property will be computed by adding 
        /// <see cref="Lifespan" /> to the local UTC time.
        /// </remarks>
        public SecureTicket(byte[] encrypted)
        {
            try
            {
                byte[]  privateData;
                byte[]  publicData;
                int     pos;

                pos = 0;
                privateData = Helper.ReadBytes16(encrypted, ref pos);      // Skip over the encrypted section
                publicData = Helper.ReadBytes16(encrypted, ref pos);      // Read the plaintext section
                args = ArgCollection.Parse(Helper.FromUTF8(publicData), '=', '\t');

                clientExpirationUtc = DateTime.UtcNow + this.Lifespan;
                clientEncrypted = encrypted;
            }
            catch (Exception e)
            {
                throw new CryptographicException("Invalid ticket", e);
            }
        }

        /// <summary>
        /// Used by a client to construct a ticket by reading the public section from a base 64 encoded byte array.
        /// </summary>
        /// <param name="base64Encrypted">The base 64 encoded encrypted ticket.</param>
        /// <exception cref="CryptographicException">Thrown if the ticket is improperly formatted or has been tampered with.</exception>
        public SecureTicket(string base64Encrypted)
            : this(Convert.FromBase64String(base64Encrypted))
        {
        }

        /// <summary>
        /// Returns the application-defined string used to identify the resource being
        /// protected by the ticket.
        /// </summary>
        public string Resource
        {
            get { return args.Get("_resource", string.Empty); }
        }

        /// <summary>
        /// Returns the life span of the ticket.
        /// </summary>
        public TimeSpan Lifespan
        {
            get { return args.Get("_lifespan", TimeSpan.Zero); }
        }

        /// <summary>
        /// Returns the ticket's expiration time (UTC) relative to the issuer's clock.
        /// </summary>
        public DateTime IssuerExpirationUtc
        {
            get { return args.Get("_issuerTTD", DateTime.UtcNow); }
        }

        /// <summary>
        /// Returns the ticket's expiration time (UTC) relative to the client's clock.
        /// </summary>
        public DateTime ClientExpirationUtc
        {
            get { return clientExpirationUtc; }
        }

        /// <summary>
        /// Sets a name/value pair in the collection.
        /// </summary>
        /// <param name="name">The name.</param>
        /// <param name="value">The value being set.</param>
        public void Set(string name, string value)
        {
            args.Set(name, value);
        }

        /// <summary>
        /// Returns the named value or a default value.
        /// </summary>
        /// <param name="name">The value name.</param>
        /// <param name="def">The default value to be returned in case the named value isn't present.</param>
        /// <returns>The named value (or the default).</returns>
        public string Get(string name, string def)
        {
            return args.Get(name, def);
        }

        /// <summary>
        /// Indexer into the collection of name/value pairs.
        /// </summary>
        /// <param name="key">The item name.</param>
        /// <returns>The item value (or <c>null</c>).</returns>
        public string this[string key]
        {

            get { return args[key]; }
            set { args[key] = value; }
        }

        /// <summary>
        /// Returns the collection ticket arguments.
        /// </summary>
        public ArgCollection Args
        {
            get { return args; }
        }

        /// <summary>
        /// Encrypts the ticket using a combination of a symmetric key.
        /// </summary>
        /// <param name="key">The symmetric encryption key.</param>
        /// <returns>The base-64 encoded string of the encrypted ticket.</returns>
        public string ToBase64String(SymmetricKey key)
        {
            return Convert.ToBase64String(ToArray(key));
        }

        /// <summary>
        /// Encrypts the ticket using a symmetric key.
        /// </summary>
        /// <param name="key">The symmetric encryption key.</param>
        /// <returns>The encrypted ticket.</returns>
        public byte[] ToArray(SymmetricKey key)
        {
            byte[]          privateData = Crypto.EncryptStringWithSalt8(args.ToString(), key);
            ArgCollection   publicArgs  = new ArgCollection('=', '\t');
            byte[]          publicData;
            byte[]          encrypted;
            int             pos;

            publicArgs.Set("_resource", this.Resource);
            publicArgs.Set("_lifespan", this.Lifespan);
            publicData = Helper.ToUTF8(publicArgs.ToString());

            encrypted = new byte[2 + privateData.Length + 2 + publicData.Length];

            pos = 0;
            Helper.WriteBytes16(encrypted, ref pos, privateData);
            Helper.WriteBytes16(encrypted, ref pos, publicData);

            return encrypted;
        }

        /// <summary>
        /// Returns the encrypted serialized client side ticket.
        /// </summary>
        /// <returns>The serialized ticket.</returns>
        /// <exception cref="InvalidOperationException">Thrown for issuer-side tickets.</exception>
        public byte[] ToArray()
        {
            if (clientEncrypted == null)
                throw new InvalidOperationException("ToArray() works only for client-side tickets.");

            return clientEncrypted;
        }

        /// <summary>
        /// Returns the encrypted serialized client side ticket as a base-64 string.
        /// </summary>
        /// <returns>The base-64 string form of the ticket.</returns>
        /// <exception cref="InvalidOperationException">Thrown for issuer-side tickets.</exception>
        public string ToBase64String()
        {
            return Convert.ToBase64String(ToArray());
        }
    }
}
