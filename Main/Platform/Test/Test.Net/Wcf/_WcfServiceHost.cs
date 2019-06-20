//-----------------------------------------------------------------------------
// FILE:        _WcfServiceHost.cs
// CONTRIBUTOR: Jeff Lill
// COPYRIGHT:   Copyright (c) 2005-2015 by Jeffrey Lill.  All rights reserved.
// DESCRIPTION: Unit tests 

using System;
using System.IO;
using System.Net;
using System.Net.Sockets;
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
using LillTek.Testing;

namespace LillTek.Net.Wcf.Test
{
    [ServiceContract]
    public interface ITestService
    {
        [OperationContract]
        void Set(string value);

        [OperationContract]
        string Get();
    }

    // $todo(jeff.lill): 
    //
    // I shouldn't need to set AddressFilterMode=AddressFilterMode.Any.
    // Delete this after figuring how how to change the address filter
    // in LillTek.ServiceModel.

    [ServiceBehavior(
        InstanceContextMode = InstanceContextMode.Single,
        ConcurrencyMode = ConcurrencyMode.Multiple,
        IncludeExceptionDetailInFaults = true,
        AddressFilterMode = AddressFilterMode.Any)]
    public class TestService : ITestService
    {
        private string value = string.Empty;

        public void Set(string value)
        {
            this.value = value;
        }

        public string Get()
        {
            return value;
        }
    }

    public class TestServiceNoInterface
    {
    }

    public class TestServiceNoBehavior : ITestService
    {
        private string value = string.Empty;

        public void Set(string value)
        {
            this.value = value;
        }

        public string Get()
        {
            return value;
        }
    }

    [TestClass]
    public class _WfcServiceHost
    {
        [TestInitialize]
        public void Initialize()
        {

            // AsyncTracker.GatherCallStacks = true;
        }

        [TestCleanup]
        public void Cleanup()
        {
            // AsyncTracker.GatherCallStacks = false;
        }

        [TestMethod]
        [TestProperty("Lib", "LillTek.Net.Wcf")]
        public void WfcServiceHost_Parse_BehaviorXML()
        {
            // Verify that we can parse behavior XML.

            WcfServiceHost host;

            host = new WcfServiceHost(new TestService());

            host.AddServiceEndpoint(typeof(ITestService), @"binding=HTTP;uri=http://localhost:8008/Unit/Test.svc;settings=<wsHttpBinding><security mode=""None""/></wsHttpBinding>");
            host.ExposeServiceDescription(null, null);

            host.AddBehaviors(
@"<behavior>
    <serviceSecurityAudit auditLogLocation=""Application""
                          suppressAuditFailure=""true""
                          serviceAuthorizationAuditLevel=""Success""
                          messageAuthenticationAuditLevel=""SuccessOrFailure"" />
    <serviceThrottling maxConcurrentCalls=""121""
                       maxConcurrentInstances=""122""
                       maxConcurrentSessions=""123"" />
    <serviceTimeouts transactionTimeout=""10m"" />
</behavior>
");
            ServiceBehaviorAttribute serviceBehavior = (ServiceBehaviorAttribute)host.Host.Description.Behaviors[typeof(ServiceBehaviorAttribute)];
            ServiceSecurityAuditBehavior serviceSecurityAudit = (ServiceSecurityAuditBehavior)host.Host.Description.Behaviors[typeof(ServiceSecurityAuditBehavior)];
            ServiceThrottlingBehavior serviceThrottling = (ServiceThrottlingBehavior)host.Host.Description.Behaviors[typeof(ServiceThrottlingBehavior)];

            Assert.IsNotNull(serviceBehavior);
            Assert.IsNotNull(serviceSecurityAudit);
            Assert.IsNotNull(serviceThrottling);

            Assert.AreEqual("00:10:00", serviceBehavior.TransactionTimeout);

            Assert.AreEqual(AuditLogLocation.Application, serviceSecurityAudit.AuditLogLocation);
            Assert.IsTrue(serviceSecurityAudit.SuppressAuditFailure);
            Assert.AreEqual(AuditLevel.Success, serviceSecurityAudit.ServiceAuthorizationAuditLevel);
            Assert.AreEqual(AuditLevel.SuccessOrFailure, serviceSecurityAudit.MessageAuthenticationAuditLevel);

            Assert.AreEqual(121, serviceThrottling.MaxConcurrentCalls);
            Assert.AreEqual(122, serviceThrottling.MaxConcurrentInstances);
            Assert.AreEqual(123, serviceThrottling.MaxConcurrentSessions);
        }

        [TestMethod]
        [TestProperty("Lib", "LillTek.Net.Wcf")]
        public void WfcServiceHost_Http()
        {
            // Verify that we can create a service instance and then call it.

            WcfServiceHost host;

            host = new WcfServiceHost(new TestService());
            try
            {
                host.AddServiceEndpoint(typeof(ITestService), @"binding=HTTP;uri=http://localhost:8008/Unit/Test.svc;settings=<wsHttpBinding><security mode=""None""/></wsHttpBinding>");
                host.ExposeServiceDescription(null, null);
                host.Start();

                TestServiceClient client;

                client = new TestServiceClient(new WSHttpBinding(SecurityMode.None), new EndpointAddress("http://localhost:8008/Unit/Test.svc"));
                client.Open();
                try
                {
                    client.Set("Hello World!");
                    Assert.AreEqual("Hello World!", client.Get());
                }
                finally
                {
                    client.Close();
                }
            }
            finally
            {
                host.Stop();
            }
        }

        [TestMethod]
        [TestProperty("Lib", "LillTek.Net.Wcf")]
        public void WfcServiceHost_Http_WcfClientContext()
        {
            // Verify that we can create a service instance and then call it using WcfClientContext.

            WcfServiceHost host;

            host = new WcfServiceHost(new TestService());
            try
            {
                host.AddServiceEndpoint(typeof(ITestService), @"binding=HTTP;uri=http://localhost:8008/Unit/Test.svc;settings=<wsHttpBinding><security mode=""None""/></wsHttpBinding>");
                host.ExposeServiceDescription(null, null);
                host.Start();

                using (WcfChannelFactory<ITestService> factory = new WcfChannelFactory<ITestService>(@"binding=HTTP;uri=http://localhost:8008/Unit/Test.svc;settings=<wsHttpBinding><security mode=""None""/></wsHttpBinding>"))
                using (WcfClientContext<ITestService> client = new WcfClientContext<ITestService>(factory.CreateChannel()))
                {
                    client.Open();
                    client.Proxy.Set("Hello World!");
                    Assert.AreEqual("Hello World!", client.Proxy.Get());
                }
            }
            finally
            {
                host.Stop();
            }
        }

        [TestMethod]
        [TestProperty("Lib", "LillTek.Net.Wcf")]
        public void WfcServiceHost_Basic_Http_Via_Factory()
        {
            // Verify that we can create a service instance and then call it
            // using a client proxy generated by a WcfChannelFactory using 
            // a HTTP transport.

            WcfServiceHost host;
            ITestService client;

            host = new WcfServiceHost(new TestService());
            try
            {
                host.AddServiceEndpoint(typeof(ITestService), "binding=HTTP;uri=http://localhost:8008/Unit/Test.svc");
                host.ExposeServiceDescription(null, null);
                host.Start();

                using (WcfChannelFactory<ITestService> factory = new WcfChannelFactory<ITestService>("binding=HTTP;uri=http://localhost:8008/Unit/Test.svc"))
                {
                    client = factory.CreateChannel();
                    using (client as IDisposable)
                    {
                        client.Set("Hello World!");
                        Assert.AreEqual("Hello World!", client.Get());
                    }
                }
            }
            finally
            {
                host.Stop();
            }
        }

        [TestMethod]
        [TestProperty("Lib", "LillTek.Net.Wcf")]
        public void WfcServiceHost_NamedPipe_Via_Factory()
        {
            // Verify that we can create a service instance and then call it
            // using a client proxy generated by a WcfChannelFactory using 
            // a named pipe transport.

            WcfServiceHost host;
            ITestService client;

            host = new WcfServiceHost(new TestService());
            try
            {
                host.AddServiceEndpoint(typeof(ITestService), "binding=NamedPipe;uri=net.pipe://localhost/wcftest");
                host.ExposeServiceDescription(null, null);
                host.Start();

                using (WcfChannelFactory<ITestService> factory = new WcfChannelFactory<ITestService>("binding=NamedPipe;uri=net.pipe://localhost/wcftest"))
                {
                    client = factory.CreateChannel();
                    using (client as IDisposable)
                    {
                        client.Set("Hello World!");
                        Assert.AreEqual("Hello World!", client.Get());
                    }
                }
            }
            finally
            {
                host.Stop();
            }
        }

        [TestMethod]
        [TestProperty("Lib", "LillTek.Net.Wcf")]
        public void WfcServiceHost_Tcp_Via_Factory()
        {
            // Verify that we can create a service instance and then call it
            // using a client proxy generated by a WcfChannelFactory using 
            // a TCP transport.

            WcfServiceHost host;
            ITestService client;

            host = new WcfServiceHost(new TestService());
            try
            {
                host.AddServiceEndpoint(typeof(ITestService), "binding=tcp;uri=net.tcp://localhost/wcftest");
                host.ExposeServiceDescription(null, null);
                host.Start();

                using (WcfChannelFactory<ITestService> factory = new WcfChannelFactory<ITestService>("binding=tcp;uri=net.tcp://localhost/wcftest"))
                {
                    client = factory.CreateChannel();
                    using (client as IDisposable)
                    {
                        client.Set("Hello World!");
                        Assert.AreEqual("Hello World!", client.Get());
                    }
                }
            }
            finally
            {
                host.Stop();
            }
        }

        [TestMethod]
        [TestProperty("Lib", "LillTek.Net.Wcf")]
        public void WfcServiceHost_LillTek_Via_Factory()
        {
            // Verify that we can create a service instance and then call it
            // using a client proxy generated by a WcfChannelFactory using 
            // the LillTek transport.

            WcfServiceHost host;
            ITestService client;

            host = new WcfServiceHost(new TestService());
            try
            {
                host.AddServiceEndpoint(typeof(ITestService), "binding=lilltek;uri=lilltek.logical://wcftest");
                host.ExposeServiceDescription(null, null);
                host.Start();

                using (WcfChannelFactory<ITestService> factory = new WcfChannelFactory<ITestService>("binding=lilltek;uri=lilltek.logical://wcftest"))
                {
                    client = factory.CreateChannel();
                    using (client as IDisposable)
                    {
                        client.Set("Hello World!");
                        Assert.AreEqual("Hello World!", client.Get());
                    }
                }
            }
            finally
            {
                host.Stop();
            }
        }

        [TestMethod]
        [TestProperty("Lib", "LillTek.Net.Wcf")]
        public void WfcServiceHost_Verify_WSDL()
        {
            // Verify that we can obtain the service WSDL via a HTTP GET.

            WcfServiceHost host;
            string contents;
            HttpWebRequest request;
            HttpWebResponse response;
            StreamReader reader;

            host = new WcfServiceHost(new TestService());
            try
            {
                host.AddServiceEndpoint(typeof(ITestService), "binding=BasicHTTP;uri=http://localhost:8008/Unit/Test.svc");
                host.ExposeServiceDescription("http://localhost:8008/Unit/Test.wsdl", null);
                host.Start();

                request = (HttpWebRequest)WebRequest.Create("http://localhost:8008/Unit/Test.wsdl");
                response = null;
                reader = null;

                try
                {
                    response = (HttpWebResponse)request.GetResponse();
                    reader = new StreamReader(response.GetResponseStream());
                    contents = reader.ReadToEnd();
                }
                finally
                {
                    if (reader != null)
                        reader.Close();

                    if (response != null)
                        response.Close();
                }

                Assert.IsTrue(200 <= (int)response.StatusCode && (int)response.StatusCode <= 299);
                Assert.IsTrue(response.ContentType.ToUpper().StartsWith("TEXT"));
                Assert.IsTrue(contents.IndexOf("<wsdl:") != -1);
            }
            finally
            {
                host.Stop();
            }
        }

        //---------------------------------------------------------------------
        // Hardcoded proxy

        //------------------------------------------------------------------------------
        // <auto-generated>
        //     This code was generated by a tool.
        //     Runtime Version:2.0.50727.42
        //
        //     Changes to this file may cause incorrect behavior and will be lost if
        //     the code is regenerated.
        // </auto-generated>
        //------------------------------------------------------------------------------

        [System.CodeDom.Compiler.GeneratedCodeAttribute("System.ServiceModel", "3.0.0.0")]
        [System.ServiceModel.ServiceContractAttribute(ConfigurationName = "ITestService")]
        public interface IXTestService
        {
            [System.ServiceModel.OperationContractAttribute(Action = "http://tempuri.org/ITestService/Set", ReplyAction = "http://tempuri.org/ITestService/SetResponse")]
            void Set(string value);

            [System.ServiceModel.OperationContractAttribute(Action = "http://tempuri.org/ITestService/Get", ReplyAction = "http://tempuri.org/ITestService/GetResponse")]
            string Get();
        }

        [System.CodeDom.Compiler.GeneratedCodeAttribute("System.ServiceModel", "3.0.0.0")]
        public interface IXTestServiceChannel : IXTestService, System.ServiceModel.IClientChannel
        {
        }

        [System.Diagnostics.DebuggerStepThroughAttribute()]
        [System.CodeDom.Compiler.GeneratedCodeAttribute("System.ServiceModel", "3.0.0.0")]
        public partial class TestServiceClient : System.ServiceModel.ClientBase<IXTestService>, IXTestService
        {
            public TestServiceClient()
            {
            }

            public TestServiceClient(string endpointConfigurationName)
                :
                    base(endpointConfigurationName)
            {
            }

            public TestServiceClient(string endpointConfigurationName, string remoteAddress)
                :
                    base(endpointConfigurationName, remoteAddress)
            {
            }

            public TestServiceClient(string endpointConfigurationName, System.ServiceModel.EndpointAddress remoteAddress)
                :
                    base(endpointConfigurationName, remoteAddress)
            {
            }

            public TestServiceClient(System.ServiceModel.Channels.Binding binding, System.ServiceModel.EndpointAddress remoteAddress)
                :
                    base(binding, remoteAddress)
            {
            }

            public void Set(string value)
            {
                base.Channel.Set(value);
            }

            public string Get()
            {
                return base.Channel.Get();
            }
        }
    }
}

