//-----------------------------------------------------------------------------
// FILE:        _AuthServiceMsgs.cs
// CONTRIBUTOR: Jeff Lill
// COPYRIGHT:   Copyright (c) 2005-2015 by Jeffrey Lill.  All rights reserved.
// DESCRIPTION: Unit tests

using System;
using System.Net;
using System.Reflection;
using System.Threading;

using LillTek.Common;
using LillTek.Cryptography;
using LillTek.Datacenter.Msgs.AuthService;
using LillTek.Messaging;
using LillTek.Testing;

using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace LillTek.Datacenter.Test
{
    [TestClass]
    public class _AuthServiceMsgs
    {
        [TestInitialize]
        public void Initialize()
        {
            Msg.LoadTypes(typeof(LillTek.Datacenter.Global).Assembly);
        }

        [TestCleanup]
        public void Cleanup()
        {
            Msg.ClearTypes();
        }

        [TestMethod]
        [TestProperty("Lib", "LillTek.Datacenter")]
        public void AuthServiceMsgs_SerializeAuthenticationResult()
        {
            AuthenticationResult result;

            result = new AuthenticationResult(AuthenticationStatus.BadAccount, "Hello World!", TimeSpan.FromMinutes(55));
            result = AuthenticationResult.Parse(result.ToString());
            Assert.AreEqual(AuthenticationStatus.BadAccount, result.Status);
            Assert.AreEqual("Hello World!", result.Message);
            Assert.AreEqual(TimeSpan.FromMinutes(55), result.MaxCacheTime);
        }

        [TestMethod]
        [TestProperty("Lib", "LillTek.Datacenter")]
        public void AuthServiceMsgs_Msg_GetPublicKeyMsg()
        {
            EnhancedStream es = new EnhancedMemoryStream();
            GetPublicKeyMsg msgIn, msgOut;

            msgOut = new GetPublicKeyMsg();

            Msg.Save(es, msgOut);
            es.Position = 0;
            msgIn = (GetPublicKeyMsg)Msg.Load(es);

            Assert.IsNotNull(msgIn);
        }

        [TestMethod]
        [TestProperty("Lib", "LillTek.Datacenter")]
        public void AuthServiceMsgs_Msg_GetPublicKeyAck()
        {
            EnhancedStream es = new EnhancedMemoryStream();
            string rsaKey = AsymmetricCrypto.CreatePrivateKey(CryptoAlgorithm.RSA, 1024);
            GetPublicKeyAck msgIn, msgOut;

            msgOut = new GetPublicKeyAck(rsaKey, Helper.MachineName, IPAddress.Parse("10.20.30.40"));

            Msg.Save(es, msgOut);
            es.Position = 0;
            msgIn = (GetPublicKeyAck)Msg.Load(es);

            Assert.AreEqual(rsaKey, msgIn.PublicKey);
            Assert.AreEqual(Helper.MachineName, msgIn.MachineName);
            Assert.AreEqual(IPAddress.Parse("10.20.30.40"), msgIn.Address);
        }

        [TestMethod]
        [TestProperty("Lib", "LillTek.Datacenter")]
        public void AuthServiceMsgs_Msg_AuthMsgAndAck()
        {
            EnhancedStream es = new EnhancedMemoryStream();
            AuthMsg authMsgIn, authMsgOut;
            AuthAck authAckIn, authAckOut;
            string rsaKey = AsymmetricCrypto.CreatePrivateKey(CryptoAlgorithm.RSA, 1024);
            SymmetricKey saClient, saServer;
            string r, a, p;
            AuthenticationResult authResult;

            authMsgOut = new AuthMsg(AuthMsg.EncryptCredentials(rsaKey, "realm", "account", "password", out saClient));

            Msg.Save(es, authMsgOut);
            es.Position = 0;
            authMsgIn = (AuthMsg)Msg.Load(es);

            AuthMsg.DecryptCredentials(rsaKey, authMsgIn.EncryptedCredentials, out r, out a, out p, out saServer);

            Assert.AreEqual("realm", r);
            Assert.AreEqual("account", a);
            Assert.AreEqual("password", p);

            authAckOut = new AuthAck(AuthAck.EncryptResult(saServer, new AuthenticationResult(AuthenticationStatus.Authenticated, "Test", TimeSpan.FromMinutes(25))));

            es.SetLength(0);
            Msg.Save(es, authAckOut);
            es.Position = 0;
            authAckIn = (AuthAck)Msg.Load(es);

            authResult = AuthAck.DecryptResult(saClient, authAckIn.EncryptedResult);
        }

        [TestMethod]
        [TestProperty("Lib", "LillTek.Datacenter")]
        public void AuthServiceMsgs_Msg_AuthServerIDMsg()
        {
            EnhancedStream es = new EnhancedMemoryStream();
            AuthServerIDMsg msgIn, msgOut;
            string rsaKey = AsymmetricCrypto.CreatePrivateKey(CryptoAlgorithm.RSA, 1024);

            msgOut = new AuthServerIDMsg(rsaKey, Helper.MachineName, IPAddress.Parse("10.20.30.40"));

            Msg.Save(es, msgOut);
            es.Position = 0;
            msgIn = (AuthServerIDMsg)Msg.Load(es);

            Assert.AreEqual(rsaKey, msgIn.PublicKey);
            Assert.AreEqual(Helper.MachineName, msgIn.MachineName);
            Assert.AreEqual(IPAddress.Parse("10.20.30.40"), msgIn.Address);
        }

        [TestMethod]
        [TestProperty("Lib", "LillTek.Datacenter")]
        public void AuthServiceMsgs_Msg_AuthControlMsg()
        {
            EnhancedStream es = new EnhancedMemoryStream();
            AuthControlMsg msgIn, msgOut;

            msgOut = new AuthControlMsg("my command", "a=test1;b=test2;c=test3");

            Msg.Save(es, msgOut);
            es.Position = 0;
            msgIn = (AuthControlMsg)Msg.Load(es);

            Assert.AreEqual("my command", msgIn.Command);
            Assert.AreEqual("test1", msgIn.Get("a", null));
            Assert.AreEqual("test2", msgIn.Get("b", null));
            Assert.AreEqual("test3", msgIn.Get("c", null));
            Assert.AreEqual("foobar", msgIn.Get("d", "foobar"));

            msgOut = new AuthControlMsg("hello", null);

            Msg.Save(es, msgOut);
            es.Position = 0;
            msgIn = (AuthControlMsg)Msg.Load(es);

            Assert.AreEqual("my command", msgIn.Command);
        }

        [TestMethod]
        [TestProperty("Lib", "LillTek.Datacenter")]
        public void AuthServiceMsgs_Msg_SourceCredentials()
        {
            EnhancedStream es = new EnhancedMemoryStream();
            Guid sourceID = Helper.NewGuid();
            string rsaKey = AsymmetricCrypto.CreatePrivateKey(CryptoAlgorithm.RSA, 1024);
            SourceCredentialsMsg msgIn, msgOut;
            string r, a, p;

            msgOut = new SourceCredentialsMsg(sourceID, SourceCredentialsMsg.EncryptCredentials(rsaKey, "realm", "account", "password"), TimeSpan.FromMinutes(2));

            Msg.Save(es, msgOut);
            es.Position = 0;
            msgIn = (SourceCredentialsMsg)Msg.Load(es);

            Assert.AreEqual(sourceID, msgIn.SourceID);
            Assert.AreEqual(TimeSpan.FromMinutes(2), msgIn.TTL);

            SourceCredentialsMsg.DecryptCredentials(rsaKey, msgIn.EncryptedCredentials, out r, out a, out p);

            Assert.AreEqual("realm", r);
            Assert.AreEqual("account", a);
            Assert.AreEqual("password", p);
        }
    }
}

