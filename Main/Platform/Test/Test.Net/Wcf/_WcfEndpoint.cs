//-----------------------------------------------------------------------------
// FILE:        _WcfEndpoint.cs
// CONTRIBUTOR: Jeff Lill
// COPYRIGHT:   Copyright (c) 2005-2015 by Jeffrey Lill.  All rights reserved.
// DESCRIPTION: Unit tests 

using System;
using System.Net;
using System.Net.Sockets;
using System.Net.Security;
using System.Text;
using System.Threading;
using System.Diagnostics;
using System.Collections;
using System.Collections.Generic;
using System.ServiceModel;
using System.ServiceModel.Channels;
using System.ServiceModel.Description;

using Microsoft.VisualStudio.TestTools.UnitTesting;

using LillTek.Common;
using LillTek.Net.Wcf;
using LillTek.ServiceModel;
using LillTek.ServiceModel.Channels;
using LillTek.Testing;

namespace LillTek.Net.Wcf.Test
{
    [TestClass]
    public class _WcfEndpoint
    {
        [TestMethod]
        [TestProperty("Lib", "LillTek.Net.Wcf")]
        public void WcfEndpoint_Basic()
        {
            Assert.IsInstanceOfType(WcfEndpoint.Parse("binding=LILLTEK;uri=lilltek.logical://test").Binding, typeof(LillTekBinding));
            Assert.IsInstanceOfType(WcfEndpoint.Parse("binding=LILLTEK;uri=lilltek.abstract://test").Binding, typeof(LillTekBinding));
            Assert.IsInstanceOfType(WcfEndpoint.Parse("binding=BASICHTTP;uri=http://x.com/").Binding, typeof(BasicHttpBinding));
            Assert.IsInstanceOfType(WcfEndpoint.Parse("binding=HTTP;uri=http://x.com/").Binding, typeof(WSHttpBinding));
            Assert.IsInstanceOfType(WcfEndpoint.Parse("binding=DUALHTTP;uri=http://x.com/").Binding, typeof(WSDualHttpBinding));
            Assert.IsInstanceOfType(WcfEndpoint.Parse("binding=FEDERATIONHTTP;uri=http://x.com/").Binding, typeof(WSFederationHttpBinding));
            Assert.IsInstanceOfType(WcfEndpoint.Parse("binding=TCP;uri=http://x.com/").Binding, typeof(NetTcpBinding));
            Assert.IsInstanceOfType(WcfEndpoint.Parse("binding=NAMEDPIPE;uri=http://x.com/").Binding, typeof(NetNamedPipeBinding));
            Assert.IsInstanceOfType(WcfEndpoint.Parse("binding=MSMQ;uri=http://x.com/").Binding, typeof(NetMsmqBinding));
            Assert.IsInstanceOfType(WcfEndpoint.Parse("binding=PEERTCP;uri=http://x.com/").Binding, typeof(NetPeerTcpBinding));
            Assert.IsInstanceOfType(WcfEndpoint.Parse("binding=MSMQINTEGRATION;uri=http://x.com/").Binding, typeof(System.ServiceModel.MsmqIntegration.MsmqIntegrationBinding));
        }

        [TestMethod]
        [TestProperty("Lib", "LillTek.Net.Wcf")]
        public void WcfEndpoint_Parse_BasicHttpBinding()
        {
            BasicHttpBinding binding;
            string config = @"

Binding  = BasicHttp;
Uri      = http://www.lilltek.com/test.svc;
Settings = 

<basicHttpBinding 
    allowCookies=""true""
    bypassProxyOnLocal=""false""
    closeTimeout=""00:00:55""
    hostNameComparisonMode=""Exact""
    maxBufferPoolSize=""10001""
    maxBufferSize=""10002""
    maxReceivedMessageSize=""10003""
    messageEncoding=""Mtom""
    name=""test""
    namespace=""http://test.com/""
    openTimeout=""00:00:56""
    proxyAddress=""http://proxy.com/""
    receiveTimeout=""00:00:57""
    sendTimeout=""00:00:58""
    textEncoding=""utf-16""
    transferMode=""Streamed""
    useDefaultWebProxy=""false""
    >

    <readerQuotas
        maxArrayLength=""10004""
        maxBytesPerRead=""10005""
        maxDepth=""10006""
        maxNameTableCharCount=""10007""
        maxStringContentLength=""10008""
        />

    <security mode=""Message"">
        <message 
            algorithmSuite=""Basic256Sha256Rsa15""
            clientCredentialType=""Certificate"" />
        <transport
            clientCredentialType=""Windows""
            proxyCredentialType=""Ntlm""
            realm=""LILLTEK""
            />
    </security>

</basicHttpBinding>
";
            binding = (BasicHttpBinding)WcfEndpoint.Parse(config).Binding;

            Assert.IsTrue(binding.AllowCookies);
            Assert.IsFalse(binding.BypassProxyOnLocal);
            Assert.AreEqual(TimeSpan.FromSeconds(55), binding.CloseTimeout);
            Assert.AreEqual(HostNameComparisonMode.Exact, binding.HostNameComparisonMode);
            Assert.AreEqual(10001, binding.MaxBufferPoolSize);
            Assert.AreEqual(10002, binding.MaxBufferSize);
            Assert.AreEqual(10003, binding.MaxReceivedMessageSize);
            Assert.AreEqual(WSMessageEncoding.Mtom, binding.MessageEncoding);
            Assert.AreEqual("test", binding.Name);
            Assert.AreEqual(new Uri("http://test.com/"), binding.Namespace);
            Assert.AreEqual(TimeSpan.FromSeconds(56), binding.OpenTimeout);
            Assert.AreEqual(new Uri("http://proxy.com/"), binding.ProxyAddress);
            Assert.AreEqual(TimeSpan.FromSeconds(57), binding.ReceiveTimeout);
            Assert.AreEqual(TimeSpan.FromSeconds(58), binding.SendTimeout);
            Assert.AreEqual("unicode", binding.TextEncoding.EncodingName.ToLowerInvariant());
            Assert.AreEqual(TransferMode.Streamed, binding.TransferMode);
            Assert.IsFalse(binding.UseDefaultWebProxy);

            Assert.AreEqual(10004, binding.ReaderQuotas.MaxArrayLength);
            Assert.AreEqual(10005, binding.ReaderQuotas.MaxBytesPerRead);
            Assert.AreEqual(10006, binding.ReaderQuotas.MaxDepth);
            Assert.AreEqual(10007, binding.ReaderQuotas.MaxNameTableCharCount);
            Assert.AreEqual(10008, binding.ReaderQuotas.MaxStringContentLength);

            Assert.AreEqual(BasicHttpSecurityMode.Message, binding.Security.Mode);
            Assert.AreEqual("Basic256Sha256Rsa15", binding.Security.Message.AlgorithmSuite.ToString());
            Assert.AreEqual(BasicHttpMessageCredentialType.Certificate, binding.Security.Message.ClientCredentialType);
            Assert.AreEqual(HttpClientCredentialType.Windows, binding.Security.Transport.ClientCredentialType);
            Assert.AreEqual(HttpProxyCredentialType.Ntlm, binding.Security.Transport.ProxyCredentialType);
            Assert.AreEqual("LILLTEK", binding.Security.Transport.Realm);
        }

        [TestMethod]
        [TestProperty("Lib", "LillTek.Net.Wcf")]
        public void WcfEndpoint_Parse_WSHttpBinding()
        {
            WSHttpBinding binding;
            string config = @"

Binding  = Http;
Uri      = http://www.lilltek.com/test.svc;
Settings = 

<wsHttpBinding 
    allowCookies=""true""
    bypassProxyOnLocal=""false""
    closeTimeout=""00:00:55""
    hostNameComparisonMode=""Exact""
    maxBufferPoolSize=""10001""
    maxReceivedMessageSize=""10003""
    messageEncoding=""Mtom""
    name=""test""
    namespace=""http://test.com/""
    openTimeout=""00:00:56""
    proxyAddress=""http://proxy.com/""
    receiveTimeout=""00:00:57""
    sendTimeout=""00:00:58""
    textEncoding=""utf-16""
    transactionFlow=""true""
    useDefaultWebProxy=""false""
    >

    <readerQuotas
        maxArrayLength=""10004""
        maxBytesPerRead=""10005""
        maxDepth=""10006""
        maxNameTableCharCount=""10007""
        maxStringContentLength=""10008""
        />

    <reliableSession
        enabled=""true""
        inactivityTimeout=""00:01:30""
        ordered=""true""
        />

    <security mode=""TransportWithMessageCredential"">
        <message 
            algorithmSuite=""Basic256Sha256Rsa15""
            clientCredentialType=""Certificate"" 
            establishSecurityContext=""false""
            negotiateServiceCredential=""false""
            />
        <transport
            clientCredentialType=""Windows""
            proxyCredentialType=""Ntlm""
            realm=""LILLTEK""
            />
    </security>

</wsHttpBinding>
";
            binding = (WSHttpBinding)WcfEndpoint.Parse(config).Binding;

            Assert.IsTrue(binding.AllowCookies);
            Assert.IsFalse(binding.BypassProxyOnLocal);
            Assert.AreEqual(TimeSpan.FromSeconds(55), binding.CloseTimeout);
            Assert.AreEqual(HostNameComparisonMode.Exact, binding.HostNameComparisonMode);
            Assert.AreEqual(10001, binding.MaxBufferPoolSize);
            Assert.AreEqual(10003, binding.MaxReceivedMessageSize);
            Assert.AreEqual(WSMessageEncoding.Mtom, binding.MessageEncoding);
            Assert.AreEqual("test", binding.Name);
            Assert.AreEqual(new Uri("http://test.com/"), binding.Namespace);
            Assert.AreEqual(TimeSpan.FromSeconds(56), binding.OpenTimeout);
            Assert.AreEqual(new Uri("http://proxy.com/"), binding.ProxyAddress);
            Assert.AreEqual(TimeSpan.FromSeconds(57), binding.ReceiveTimeout);
            Assert.AreEqual(TimeSpan.FromSeconds(58), binding.SendTimeout);
            Assert.AreEqual("unicode", binding.TextEncoding.EncodingName.ToLowerInvariant());
            Assert.AreEqual(true, binding.TransactionFlow);
            Assert.IsFalse(binding.UseDefaultWebProxy);

            Assert.AreEqual(10004, binding.ReaderQuotas.MaxArrayLength);
            Assert.AreEqual(10005, binding.ReaderQuotas.MaxBytesPerRead);
            Assert.AreEqual(10006, binding.ReaderQuotas.MaxDepth);
            Assert.AreEqual(10007, binding.ReaderQuotas.MaxNameTableCharCount);
            Assert.AreEqual(10008, binding.ReaderQuotas.MaxStringContentLength);

            Assert.IsTrue(binding.ReliableSession.Enabled);
            Assert.AreEqual(TimeSpan.FromSeconds(90), binding.ReliableSession.InactivityTimeout);
            Assert.IsTrue(binding.ReliableSession.Ordered);

            Assert.AreEqual(SecurityMode.TransportWithMessageCredential, binding.Security.Mode);
            Assert.AreEqual("Basic256Sha256Rsa15", binding.Security.Message.AlgorithmSuite.ToString());
            Assert.AreEqual(MessageCredentialType.Certificate, binding.Security.Message.ClientCredentialType);
            Assert.IsFalse(binding.Security.Message.EstablishSecurityContext);
            Assert.IsFalse(binding.Security.Message.NegotiateServiceCredential);
            Assert.AreEqual(HttpClientCredentialType.Windows, binding.Security.Transport.ClientCredentialType);
            Assert.AreEqual(HttpProxyCredentialType.Ntlm, binding.Security.Transport.ProxyCredentialType);
            Assert.AreEqual("LILLTEK", binding.Security.Transport.Realm);
        }

        [TestMethod]
        [TestProperty("Lib", "LillTek.Net.Wcf")]
        public void WcfEndpoint_Parse_NetTcpBinding()
        {
            NetTcpBinding binding;
            string config = @"

Binding  = TCP;
Uri      = http://www.lilltek.com/test.svc;
Settings = 

<netTcpBinding 
    closeTimeout=""00:00:55""
    hostNameComparisonMode=""Exact""
    listenBacklog=""666""
    maxBufferPoolSize=""10001""
    maxConnections=""667""
    maxReceivedMessageSize=""10003""
    name=""test""
    namespace=""http://test.com/""
    openTimeout=""00:00:56""
    portSharingEnabled=""false""
    receiveTimeout=""00:00:57""
    sendTimeout=""00:00:58""
    transactionFlow=""true""
    transactionProtocol=""OleTransactions""
    transferMode=""StreamedResponse""
    >

    <readerQuotas
        maxArrayLength=""10004""
        maxBytesPerRead=""10005""
        maxDepth=""10006""
        maxNameTableCharCount=""10007""
        maxStringContentLength=""10008""
        />

    <reliableSession
        enabled=""true""
        inactivityTimeout=""00:01:30""
        ordered=""true""
        />

    <security mode=""TransportWithMessageCredential"">
        <message 
            algorithmSuite=""Basic256Sha256Rsa15""
            clientCredentialType=""Certificate"" 
            />
        <transport
            clientCredentialType=""Windows""
            protectionLevel=""EncryptAndSign""
            />
    </security>

</netTcpBinding>
";
            binding = (NetTcpBinding)WcfEndpoint.Parse(config).Binding;

            Assert.AreEqual(TimeSpan.FromSeconds(55), binding.CloseTimeout);
            Assert.AreEqual(HostNameComparisonMode.Exact, binding.HostNameComparisonMode);
            Assert.AreEqual(666, binding.ListenBacklog);
            Assert.AreEqual(10001, binding.MaxBufferPoolSize);
            Assert.AreEqual(667, binding.MaxConnections);
            Assert.AreEqual(10003, binding.MaxReceivedMessageSize);
            Assert.AreEqual("test", binding.Name);
            Assert.AreEqual(new Uri("http://test.com/"), binding.Namespace);
            Assert.AreEqual(TimeSpan.FromSeconds(56), binding.OpenTimeout);
            Assert.IsFalse(binding.PortSharingEnabled);
            Assert.AreEqual(TimeSpan.FromSeconds(57), binding.ReceiveTimeout);
            Assert.AreEqual(TimeSpan.FromSeconds(58), binding.SendTimeout);
            Assert.AreEqual(true, binding.TransactionFlow);
            Assert.AreEqual("OleTransactionsProtocol", binding.TransactionProtocol.GetType().Name);
            Assert.AreEqual(TransferMode.StreamedResponse, binding.TransferMode);

            Assert.AreEqual(10004, binding.ReaderQuotas.MaxArrayLength);
            Assert.AreEqual(10005, binding.ReaderQuotas.MaxBytesPerRead);
            Assert.AreEqual(10006, binding.ReaderQuotas.MaxDepth);
            Assert.AreEqual(10007, binding.ReaderQuotas.MaxNameTableCharCount);
            Assert.AreEqual(10008, binding.ReaderQuotas.MaxStringContentLength);

            Assert.IsTrue(binding.ReliableSession.Enabled);
            Assert.AreEqual(TimeSpan.FromSeconds(90), binding.ReliableSession.InactivityTimeout);
            Assert.IsTrue(binding.ReliableSession.Ordered);

            Assert.AreEqual(SecurityMode.TransportWithMessageCredential, binding.Security.Mode);
            Assert.AreEqual("Basic256Sha256Rsa15", binding.Security.Message.AlgorithmSuite.ToString());
            Assert.AreEqual(MessageCredentialType.Certificate, binding.Security.Message.ClientCredentialType);
            Assert.AreEqual(TcpClientCredentialType.Windows, binding.Security.Transport.ClientCredentialType);
            Assert.AreEqual(ProtectionLevel.EncryptAndSign, binding.Security.Transport.ProtectionLevel);
        }

        [TestMethod]
        [TestProperty("Lib", "LillTek.Net.Wcf")]
        public void WcfEndpoint_Parse_NetNamedPipeBinding()
        {
            NetNamedPipeBinding binding;
            string config = @"

Binding  = NamedPipe;
Uri      = http://www.lilltek.com/test.svc;
Settings = 

<netNamedPipeBinding 
    closeTimeout=""00:00:55""
    hostNameComparisonMode=""Exact""
    maxBufferPoolSize=""10001""
    maxConnections=""667""
    maxReceivedMessageSize=""10003""
    name=""test""
    namespace=""http://test.com/""
    openTimeout=""00:00:56""
    receiveTimeout=""00:00:57""
    sendTimeout=""00:00:58""
    transactionFlow=""true""
    transactionProtocol=""OleTransactions""
    transferMode=""StreamedResponse""
    >

    <readerQuotas
        maxArrayLength=""10004""
        maxBytesPerRead=""10005""
        maxDepth=""10006""
        maxNameTableCharCount=""10007""
        maxStringContentLength=""10008""
        />

    <security mode=""Transport"">
        <transport
            protectionLevel=""EncryptAndSign""
            />
    </security>

</netNamedPipeBinding>
";
            binding = (NetNamedPipeBinding)WcfEndpoint.Parse(config).Binding;

            Assert.AreEqual(TimeSpan.FromSeconds(55), binding.CloseTimeout);
            Assert.AreEqual(HostNameComparisonMode.Exact, binding.HostNameComparisonMode);
            Assert.AreEqual(10001, binding.MaxBufferPoolSize);
            Assert.AreEqual(667, binding.MaxConnections);
            Assert.AreEqual(10003, binding.MaxReceivedMessageSize);
            Assert.AreEqual("test", binding.Name);
            Assert.AreEqual(new Uri("http://test.com/"), binding.Namespace);
            Assert.AreEqual(TimeSpan.FromSeconds(56), binding.OpenTimeout);
            Assert.AreEqual(TimeSpan.FromSeconds(57), binding.ReceiveTimeout);
            Assert.AreEqual(TimeSpan.FromSeconds(58), binding.SendTimeout);
            Assert.AreEqual(true, binding.TransactionFlow);
            Assert.AreEqual("OleTransactionsProtocol", binding.TransactionProtocol.GetType().Name);
            Assert.AreEqual(TransferMode.StreamedResponse, binding.TransferMode);

            Assert.AreEqual(10004, binding.ReaderQuotas.MaxArrayLength);
            Assert.AreEqual(10005, binding.ReaderQuotas.MaxBytesPerRead);
            Assert.AreEqual(10006, binding.ReaderQuotas.MaxDepth);
            Assert.AreEqual(10007, binding.ReaderQuotas.MaxNameTableCharCount);
            Assert.AreEqual(10008, binding.ReaderQuotas.MaxStringContentLength);

            Assert.AreEqual(NetNamedPipeSecurityMode.Transport, binding.Security.Mode);
            Assert.AreEqual(ProtectionLevel.EncryptAndSign, binding.Security.Transport.ProtectionLevel);
        }

        [TestMethod]
        [TestProperty("Lib", "LillTek.Net.Wcf")]
        public void WcfEndpoint_LoadConfig()
        {
            Config config;
            WcfEndpoint ep;
            WcfEndpoint[] array;

            try
            {
                Config.SetConfig(@"

    ep       = binding=http;uri=http://ep.com/

    array[0] = binding=http;uri=http://foo0.com/
    array[1] = binding=http;uri=http://foo1.com/
    array[2] = binding=http;uri=http://foo2.com/

");
                config = new Config();

                ep = WcfEndpoint.LoadConfig(config, "ep");
                Assert.IsInstanceOfType(ep.Binding, typeof(WSHttpBinding));
                Assert.AreEqual("http://ep.com/", ep.Uri.ToString());

                array = WcfEndpoint.LoadConfigArray(config, "array");
                Assert.AreEqual(3, array.Length);

                for (int i = 0; i < array.Length; i++)
                {
                    Assert.IsInstanceOfType(array[i].Binding, typeof(WSHttpBinding));
                    Assert.AreEqual(string.Format("http://foo{0}.com/", i), array[i].Uri.ToString());
                }
            }
            finally
            {
                Config.SetConfig(null);
            }
        }
    }
}

