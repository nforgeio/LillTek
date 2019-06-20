//-----------------------------------------------------------------------------
// FILE:        AuthServiceMsgs.cs
// CONTRIBUTOR: Jeff Lill
// COPYRIGHT:   Copyright (c) 2005-2015 by Jeffrey Lill.  All rights reserved.
// DESCRIPTION: Implements the messages and other classes used to for 
//              communication between Authenticator and AuthServiceHandler 
//              instances.

using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.Net;
using System.Text;

using LillTek.Common;
using LillTek.Cryptography;
using LillTek.Datacenter;
using LillTek.Messaging;

namespace LillTek.Datacenter.Msgs.AuthService
{
    /// <summary>
    /// Used to request the public key to be used by <see cref="Authenticator" /> instances
    /// to encrypt traffic to authentication servers.  This will be broadcast to the
    /// authentication server endpoint.
    /// </summary>
    public sealed class GetPublicKeyMsg : PropertyMsg
    {
        //---------------------------------------------------------------------
        // Static members

        /// <summary>
        /// Returns the type ID string to be used when serializing the message.
        /// </summary>
        public static new string GetTypeID()
        {
            return "LT.DC.Auth.GetPublicKey";
        }

        //---------------------------------------------------------------------
        // Instance members

        /// <summary>
        /// Constructor.
        /// </summary>
        public GetPublicKeyMsg()
        {
        }

        /// <summary>
        /// Protected constructor that does not instantiate the property hash table.
        /// </summary>
        /// <param name="param">Pass <see cref="Stub.Param" />.</param>
        private GetPublicKeyMsg(Stub param)
            : base(param)
        {
        }

        /// <summary>
        /// Generates a clone of this message instance, generating a new 
        /// <see cref="Msg._MsgID" /> property if the original ID is
        /// not empty.
        /// </summary>
        /// <returns>The cloned message.</returns>
        public override Msg Clone()
        {
            GetPublicKeyMsg clone;

            clone = new GetPublicKeyMsg(Stub.Param);
            clone.CopyBaseFields(this, true);

            return clone;
        }
    }

    /// <summary>
    /// Holds the response to a <see cref="GetPublicKeyMsg" /> from an authentication
    /// service instance to an <see cref="Authenticator" /> client.
    /// </summary>
    public sealed class GetPublicKeyAck : Ack
    {
        //---------------------------------------------------------------------
        // Static members

        /// <summary>
        /// Returns the type ID string to be used when serializing the message.
        /// </summary>
        public static new string GetTypeID()
        {
            return "LT.DC.Auth.GetPublicKeyAck";
        }

        //---------------------------------------------------------------------
        // Instance members

        /// <summary>
        /// Constructor.
        /// </summary>
        public GetPublicKeyAck()
        {
        }

        /// <summary>
        /// Constructor.
        /// </summary>
        /// <param name="publicKey">The authentication server's public key encoded as XML.</param>
        /// <param name="machineName">The Authentication server's machine name.</param>
        /// <param name="address">The Authentication server's IP address.</param>
        public GetPublicKeyAck(string publicKey, string machineName, IPAddress address)
        {
            base._Set("public-key", Helper.ToUTF8(publicKey));
            base._Set("machine-name", machineName);
            base._Set("ip-address", address);
        }

        /// <summary>
        /// Protected constructor that does not instantiate the property hash table.
        /// </summary>
        /// <param name="param">Pass <see cref="Stub.Param" />.</param>
        private GetPublicKeyAck(Stub param)
            : base(param)
        {
        }

        /// <summary>
        /// Returns the authentication service's public key encoded as XML.
        /// </summary>
        public string PublicKey
        {
            get { return Helper.FromUTF8(base._Get("public-key", (byte[])null)); }
        }

        /// <summary>
        /// Returns the machine name of the authentication service.
        /// </summary>
        public string MachineName
        {
            get { return base._Get("machine-name", string.Empty); }
        }

        /// <summary>
        /// Returns the IP address of the authentication service.
        /// </summary>
        public IPAddress Address
        {
            get { return base._Get("ip-address", IPAddress.Any); }
        }

        /// <summary>
        /// Generates a clone of this message instance, generating a new 
        /// <see cref="Msg._MsgID" /> property if the original ID is
        /// not empty.
        /// </summary>
        /// <returns>The cloned message.</returns>
        public override Msg Clone()
        {
            GetPublicKeyAck clone;

            clone = new GetPublicKeyAck(Stub.Param);
            clone.CopyBaseFields(this, true);

            return clone;
        }
    }

    /// <summary>
    /// <see cref="Authenticator" /> instances broadcast this to all authentication
    /// servers after the client receives a <see cref="GetPublicKeyAck" />.  This
    /// message includes the public key received as well as the information about
    /// the sending authentication server.  Authentication servers will use this
    /// information to watch for misconfigured peers or possible man-in-the-middle
    /// security breaches.
    /// </summary>
    public sealed class AuthServerIDMsg : PropertyMsg
    {
        //---------------------------------------------------------------------
        // Static members

        /// <summary>
        /// Returns the type ID string to be used when serializing the message.
        /// </summary>
        public static new string GetTypeID()
        {
            return "LT.DC.Auth.AuthServerID";
        }

        //---------------------------------------------------------------------
        // Instance members

        /// <summary>
        /// Constructor.
        /// </summary>
        public AuthServerIDMsg()
        {
        }

        /// <summary>
        /// Constructor.
        /// </summary>
        /// <param name="publicKey">The public key returned by the authentication service.</param>
        /// <param name="machineName">The machine name for the authentication service.</param>
        /// <param name="address">The machine's IP address.</param>
        public AuthServerIDMsg(string publicKey, string machineName, IPAddress address)
        {
            base._Set("public-key", Helper.ToUTF8(publicKey));
            base._Set("machine-name", machineName);
            base._Set("ip-address", address);
        }

        /// <summary>
        /// Protected constructor that does not instantiate the property hash table.
        /// </summary>
        /// <param name="param">Pass <see cref="Stub.Param" />.</param>
        private AuthServerIDMsg(Stub param)
            : base(param)
        {
        }

        /// <summary>
        /// Returns the authentication service's public key encoded as XML.
        /// </summary>
        public string PublicKey
        {
            get { return Helper.FromUTF8(base._Get("public-key", (byte[])null)); }
        }

        /// <summary>
        /// Returns the machine name of the authentication service.
        /// </summary>
        public string MachineName
        {
            get { return base._Get("machine-name", string.Empty); }
        }

        /// <summary>
        /// Returns the IP address of the authentication service.
        /// </summary>
        public IPAddress Address
        {
            get { return base._Get("ip-address", IPAddress.Any); }
        }

        /// <summary>
        /// Generates a clone of this message instance, generating a new 
        /// <see cref="Msg._MsgID" /> property if the original ID is
        /// not empty.
        /// </summary>
        /// <returns>The cloned message.</returns>
        public override Msg Clone()
        {
            AuthServerIDMsg clone;

            clone = new AuthServerIDMsg(Stub.Param);
            clone.CopyBaseFields(this, true);

            return clone;
        }
    }

    /// <summary>
    /// Sent to request the authentication of a set of credentials.
    /// </summary>
    public sealed class AuthMsg : PropertyMsg
    {
        //---------------------------------------------------------------------
        // Static members

        /// <summary>
        /// Returns the type ID string to be used when serializing the message.
        /// </summary>
        public static new string GetTypeID()
        {
            return "LT.DC.Auth.Auth";
        }

        /// <summary>
        /// Encrypts the credentials using <see cref="SecureData" />, RSA, and AES/256-bit.
        /// </summary>
        /// <param name="publicKey">The authentication service's public key as XML.</param>
        /// <param name="realm">The authentication realm.</param>
        /// <param name="account">The account.</param>
        /// <param name="password">The password.</param>
        /// <param name="symmetricKey">Returns as the symmetric encryption key generated.</param>
        /// <returns>The encrypted credentials.</returns>
        /// <remarks>
        /// <para>
        /// The <paramref name="symmetricKey" /> returned will be necessary for decrypting the <see cref="AuthAck" />
        /// returned by the authentication server.
        /// </para>
        /// </remarks>
        public static byte[] EncryptCredentials(string publicKey, string realm, string account, string password,
                                                out SymmetricKey symmetricKey)
        {
            var args = new ArgCollection('=', '\t');

            args.Set("realm", realm);
            args.Set("account", account);
            args.Set("password", password);

            return SecureData.Encrypt(publicKey, Helper.ToUTF8(args.ToString()), CryptoAlgorithm.AES, 256, 256, out symmetricKey);
        }

        /// <summary>
        /// Decrypts the credentials using <see cref="SecureData" />.
        /// </summary>
        /// <param name="privateKey">The authentication service's private key.</param>
        /// <param name="credentials">The encrypted credentials.</param>
        /// <param name="realm">Returns as the authentication realm.</param>
        /// <param name="account">Returns as the account.</param>
        /// <param name="password">Returns as the password.</param>
        /// <param name="symmetricKey">Returns as the symmetric encryption key generated.</param>
        /// <remarks>
        /// <para>
        /// The <paramref name="symmetricKey" /> returned will be necessary for encrypting the <see cref="AuthAck" />
        /// response returned by the authentication server.
        /// </para>
        /// </remarks>
        public static void DecryptCredentials(string privateKey, byte[] credentials,
                                              out string realm, out string account, out string password,
                                              out SymmetricKey symmetricKey)
        {
            ArgCollection args;

            args     = new ArgCollection(Helper.FromUTF8(SecureData.Decrypt(privateKey, credentials, out symmetricKey)), '=', '\t');
            realm    = args.Get("realm", string.Empty);
            account  = args.Get("account", string.Empty);
            password = args.Get("password", string.Empty);
        }

        //---------------------------------------------------------------------
        // Instance members

        /// <summary>
        /// Constructor.
        /// </summary>
        public AuthMsg()
        {
        }

        /// <summary>
        /// Constructor.
        /// </summary>
        /// <param name="encryptedCredentials">The encrypted credentials.</param>
        public AuthMsg(byte[] encryptedCredentials)
        {
            base._Set("credentials", encryptedCredentials);
        }

        /// <summary>
        /// Protected constructor that does not instantiate the property hash table.
        /// </summary>
        /// <param name="param">Pass <see cref="Stub.Param" />.</param>
        private AuthMsg(Stub param)
            : base(param)
        {
        }

        /// <summary>
        /// Returns the encrypted credentials.
        /// </summary>
        public byte[] EncryptedCredentials
        {
            get { return base._Get("credentials", (byte[])null); }
        }

        /// <summary>
        /// Generates a clone of this message instance, generating a new 
        /// <see cref="Msg._MsgID" /> property if the original ID is
        /// not empty.
        /// </summary>
        /// <returns>The cloned message.</returns>
        public override Msg Clone()
        {
            AuthMsg clone;

            clone = new AuthMsg(Stub.Param);
            clone.CopyBaseFields(this, true);

            return clone;
        }
    }

    /// <summary>
    /// Holds the response to an <see cref="AuthMsg" /> from an authentication
    /// service instance to an <see cref="Authenticator" /> client.
    /// </summary>
    public sealed class AuthAck : Ack
    {
        //---------------------------------------------------------------------
        // Static members

        /// <summary>
        /// Returns the type ID string to be used when serializing the message.
        /// </summary>
        public static new string GetTypeID()
        {
            return "LT.DC.Auth.AuthAck";
        }

        /// <summary>
        /// Encrypts an <see cref="AuthenticationResult" /> using <see cref="SecureData" />, RSA, and AES/256-bit.
        /// </summary>
        /// <param name="symmetricKey">The symmetric encryption key used to encrypt the <see cref="AuthMsg" />.</param>
        /// <param name="result">The authentication result.</param>
        /// <returns>The encrypted result.</returns>
        public static byte[] EncryptResult(SymmetricKey symmetricKey, AuthenticationResult result)
        {
            return SecureData.Encrypt(symmetricKey, Helper.ToUTF8(result.ToString()), 256);
        }

        /// <summary>
        /// Decrypts an <see cref="AuthenticationResult" /> using <see cref="SecureData" />.
        /// </summary>
        /// <param name="symmetricKey">The symmetric encryption key used to encrypt the <see cref="AuthMsg" />.</param>
        /// <param name="result">The encrypted authentication result.</param>
        /// <returns>The decrypted result.</returns>
        public static AuthenticationResult DecryptResult(SymmetricKey symmetricKey, byte[] result)
        {
            return AuthenticationResult.Parse(Helper.FromUTF8(SecureData.Decrypt(symmetricKey, result)));
        }

        //---------------------------------------------------------------------
        // Instance members

        /// <summary>
        /// Constructor.
        /// </summary>
        public AuthAck()
        {
        }

        /// <summary>
        /// Constructor.
        /// </summary>
        /// <param name="encryptedResult">The encrypted <see cref="AuthenticationResult" />.</param>
        public AuthAck(byte[] encryptedResult)
        {
            base._Set("result", encryptedResult);
        }

        /// <summary>
        /// Protected constructor that does not instantiate the property hash table.
        /// </summary>
        /// <param name="param">Pass <see cref="Stub.Param" />.</param>
        private AuthAck(Stub param)
            : base(param)
        {
        }

        /// <summary>
        /// Returns the encrypted <see cref="AuthenticationResult" />.
        /// </summary>
        public byte[] EncryptedResult
        {
            get { return base._Get("result", (byte[])null); }
        }

        /// <summary>
        /// Generates a clone of this message instance, generating a new 
        /// <see cref="Msg._MsgID" /> property if the original ID is
        /// not empty.
        /// </summary>
        /// <returns>The cloned message.</returns>
        public override Msg Clone()
        {
            AuthAck clone;

            clone = new AuthAck(Stub.Param);
            clone.CopyBaseFields(this, true);

            return clone;
        }
    }

    /// <summary>
    /// Authentication control message.
    /// </summary>
    public sealed class AuthControlMsg : PropertyMsg
    {
        //---------------------------------------------------------------------
        // Static members

        /// <summary>
        /// Returns the type ID string to be used when serializing the message.
        /// </summary>
        public static new string GetTypeID()
        {
            return "LT.DC.Auth.Control";
        }

        //---------------------------------------------------------------------
        // Instance members

        /// <summary>
        /// Constructor.
        /// </summary>
        public AuthControlMsg()
        {
        }

        /// <summary>
        /// Constructor.
        /// </summary>
        /// <param name="command">The command name.</param>
        /// <param name="args">The command arguments (or <c>null</c>).</param>
        public AuthControlMsg(string command, ArgCollection args)
        {
            base._Set("command", command);

            if (args != null)
            {
                foreach (string key in args)
                    base._Set("arg." + key, args[key]);
            }
        }

        /// <summary>
        /// Protected constructor that does not instantiate the property hash table.
        /// </summary>
        /// <param name="param">Pass <see cref="Stub.Param" />.</param>
        private AuthControlMsg(Stub param)
            : base(param)
        {
        }

        /// <summary>
        /// Returns the command name.
        /// </summary>
        public string Command
        {
            get { return base._Get("command"); }
        }

        /// <summary>
        /// Returns the requested argument value or a default
        /// value if the argument is not present.
        /// </summary>
        /// <param name="name">The argument name</param>
        /// <param name="def">The default value.</param>
        /// <returns>The requested value (or the default).</returns>
        public string Get(string name, string def)
        {
            return base._Get("arg." + name, def);
        }

        /// <summary>
        /// Generates a clone of this message instance, generating a new 
        /// <see cref="Msg._MsgID" /> property if the original ID is
        /// not empty.
        /// </summary>
        /// <returns>The cloned message.</returns>
        public override Msg Clone()
        {
            AuthControlMsg clone;

            clone = new AuthControlMsg(Stub.Param);
            clone.CopyBaseFields(this, true);

            return clone;
        }
    }

    /// <summary>
    /// Broadcast between authentication service instances after a set of 
    /// credentials has been successfully authenticated against an authenication
    /// source, giving all instances the chance to precache the credentials.
    /// </summary>
    /// <remarks>
    /// Source credentials are encrypted using a combination of the shared
    /// authentication service public RSA key and a one time symmetric key.
    /// </remarks>
    public sealed class SourceCredentialsMsg : PropertyMsg
    {
        //---------------------------------------------------------------------
        // Static members

        /// <summary>
        /// Returns the type ID string to be used when serializing the message.
        /// </summary>
        public static new string GetTypeID()
        {
            return "LT.DC.Auth.SourceCredentials";
        }

        /// <summary>
        /// Encrypts the credentials using <see cref="SecureData" />, RSA, and AES/256-bit.
        /// </summary>
        /// <param name="publicKey">The authentication service's public key as XML.</param>
        /// <param name="realm">The authentication realm.</param>
        /// <param name="account">The account.</param>
        /// <param name="password">The password.</param>
        /// <returns>The encrypted credentials.</returns>
        public static byte[] EncryptCredentials(string publicKey, string realm, string account, string password)
        {
            var args = new ArgCollection('=', '\t');

            args.Set("realm", realm);
            args.Set("account", account);
            args.Set("password", password);

            return SecureData.Encrypt(publicKey, Helper.ToUTF8(args.ToString()), CryptoAlgorithm.AES, 256, 256);
        }

        /// <summary>
        /// Decrypts the credentials using <see cref="SecureData" />.
        /// </summary>
        /// <param name="privateKey">The authentication service's private key.</param>
        /// <param name="credentials">The encrypted credentials.</param>
        /// <param name="realm">Returns as the authentication realm.</param>
        /// <param name="account">Returns as the account.</param>
        /// <param name="password">Returns as the password.</param>
        public static void DecryptCredentials(string privateKey, byte[] credentials,
                                              out string realm, out string account, out string password)
        {
            ArgCollection args;

            args     = new ArgCollection(Helper.FromUTF8(SecureData.Decrypt(privateKey, credentials)), '=', '\t');
            realm    = args.Get("realm", string.Empty);
            account  = args.Get("account", string.Empty);
            password = args.Get("password", string.Empty);
        }

        //---------------------------------------------------------------------
        // Instance members

        /// <summary>
        /// Constructor.
        /// </summary>
        public SourceCredentialsMsg()
        {
        }

        /// <summary>
        /// Constructor.
        /// </summary>
        /// <param name="sourceID">GUID identifying the originating authentication server instance.</param>
        /// <param name="encryptedCredentials">The encrypted credentials.</param>
        /// <param name="ttl">The time-to-live to use when caching these credentials.</param>
        public SourceCredentialsMsg(Guid sourceID, byte[] encryptedCredentials, TimeSpan ttl)
        {
            base._Set("source-id", sourceID);
            base._Set("credentials", encryptedCredentials);
            base._Set("ttl", ttl);
        }

        /// <summary>
        /// Protected constructor that does not instantiate the property hash table.
        /// </summary>
        /// <param name="param">Pass <see cref="Stub.Param" />.</param>
        private SourceCredentialsMsg(Stub param)
            : base(param)
        {
        }

        /// <summary>
        /// Returns the GUID identifying the originating authentication server instance.
        /// </summary>
        public Guid SourceID
        {
            get { return base._Get("source-id", Guid.Empty); }
        }

        /// <summary>
        /// Returns the encrypted credentials.
        /// </summary>
        public byte[] EncryptedCredentials
        {
            get { return base._Get("credentials", (byte[])null); }
        }

        /// <summary>
        /// Returns the time-to-live to use when caching these credentials.
        /// </summary>
        public TimeSpan TTL
        {
            get { return base._Get("ttl", TimeSpan.Zero); }
        }

        /// <summary>
        /// Generates a clone of this message instance, generating a new 
        /// <see cref="Msg._MsgID" /> property if the original ID is
        /// not empty.
        /// </summary>
        /// <returns>The cloned message.</returns>
        public override Msg Clone()
        {
            SourceCredentialsMsg clone;

            clone = new SourceCredentialsMsg(Stub.Param);
            clone.CopyBaseFields(this, true);

            return clone;
        }
    }
}
