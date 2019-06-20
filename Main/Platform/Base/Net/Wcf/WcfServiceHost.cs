//-----------------------------------------------------------------------------
// FILE:        WcfServiceHost.cs
// CONTRIBUTOR: Jeff Lill
// COPYRIGHT:   Copyright (c) 2005-2015 by Jeffrey Lill.  All rights reserved.
// DESCRIPTION: A simplified implementation of WCF ServiceHost that is
//              designed implementing LillTek Platform services.

using System;
using System.Net;
using System.Net.Sockets;
using System.Threading;
using System.Reflection;
using System.Collections.Generic;
using System.ServiceModel;
using System.ServiceModel.Channels;
using System.ServiceModel.Description;
using System.ServiceModel.Persistence;
using System.Runtime.Serialization;

using LillTek.Common;
using LillTek.Xml;

namespace LillTek.Net.Wcf
{
    /// <summary>
    /// A simplified implementation of Windows Communication Foundation (WCF)
    /// ServiceHost that is designed implementing LillTek Platform services.
    /// </summary>
    /// <remarks>
    /// <para>
    /// This class exposes the subset of the functionality of the WCF 
    /// <see cref="ServiceHost" /> class that is suitable for exposing
    /// web service interfaces in applications based on the LillTek
    /// Platform.
    /// </para>
    /// <para>
    /// LillTek Platform applications expose <see cref="LillTek.Messaging" />
    /// endpoints bound to specific object instances, where the service class
    /// can maintain internal state and is responsible for handling all 
    /// thread synchronization issues.  This requires that the service class
    /// must be tadded with the <b>[ServiceBehavior]</b> properties shown below:
    /// </para>
    /// <code language="cs">
    ///     [ServiceBehavior(InstanceContextMode = InstanceContextMode.Single,
    ///                      ConcurrencyMode = ConcurrencyMode.Multiple,
    ///                      IncludeExceptionDetailInFaults = true)]
    ///     public class MyService : IMyServiceContract
    ///     {
    ///     }
    /// </code>
    /// <para>
    /// <see cref="WcfServiceHost" /> ensures that these service behavior settings are 
    /// present to avoid unexpected behaviors.
    /// </para>
    /// <para>
    /// To function in a WCF environment, service classes must implement at least
    /// one interface that is tagged with the WCF <b>[ServiceContract]</b> attribute
    /// and any structured data types that will need to be serialized for transport
    /// will need to be tagged with the <b>[DataContract]</b> attribute.
    /// </para>
    /// <para>
    /// To use this class, create an instance passing your service object instance.
    /// This service instance may be any arbitrary type but it must implement at
    /// least once interface tagged with a <b>ServiceContract</b>.  Then call
    /// <see cref="AddServiceEndpoint(System.Type,WcfEndpoint)" /> or
    /// <see cref="AddServiceEndpoint(System.Type,WcfEndpoint[])" /> one or
    /// more times to specify the endpoints to be exposed for the interfaces
    /// implemented by the service.  Then call <see cref="Start" /> to start
    /// the service.  When it's time to shut the service down, call <see cref="Stop" />.
    /// </para>
    /// <para>
    /// By default, the service host <b>does not</b> expose the service description
    /// metadata.  Call <see cref="ExposeServiceDescription" /> to expose the 
    /// service description via HTTP/GET on a HTTP or HTTPS endpoint (or both).
    /// <see cref="ExposeServiceDescription" /> must be called before the service
    /// is started.
    /// </para>
    /// <para><b><u>Service Behavior Configutration</u></b></para>
    /// <para>
    /// Service specific behaviors can be specified in code by tagging the service
    /// class with attributes, by explicting adding
    /// a behavior instance to the underlying <see cref="ServiceDescription" />,
    /// or finally by specifying the behaviors as XML (presumably loaded from
    /// the application configuration).
    /// </para>
    /// <para>
    /// The current implementation supports the parsing of a limited set of possible
    /// service behaviors implemented by WCF.  Support for the other behaviors will be 
    /// added in a future release.  Here's the current implementation status:
    /// </para>
    /// <list type="table">
    ///     <item>
    ///         <term><see cref="DataContractSerializer" /></term>
    ///         <description><b>Not Implemented</b></description>
    ///     </item>
    ///     <item>
    ///         <term><see cref="PersistenceProvider" /></term>
    ///         <description><b>Not Implemented</b></description>
    ///     </item>
    ///     <item>
    ///         <term><see cref="ServiceAuthorizationBehavior" /></term>
    ///         <description><b>Not Implemented</b></description>
    ///     </item>
    ///     <item>
    ///         <term><see cref="ServiceCredentials" /></term>
    ///         <description><b>Not Implemented</b></description>
    ///     </item>
    ///     <item>
    ///         <term><see cref="ServiceDebugBehavior" /></term>
    ///         <description><b>Not Implemented</b></description>
    ///     </item>
    ///     <item>
    ///         <term><see cref="ServiceMetadataBehavior" /></term>
    ///         <description><b>Not Implemented</b></description>
    ///     </item>
    ///     <item>
    ///         <term><see cref="ServiceSecurityAuditBehavior" /></term>
    ///         <description><b>Implemented</b></description>
    ///     </item>
    ///     <item>
    ///         <term><see cref="ServiceThrottlingBehavior" /></term>
    ///         <description><b>Implemented</b></description>
    ///     </item>
    ///     <item>
    ///         <term><see cref="ServiceTimeoutsBehavior" /></term>
    ///         <description><b>Implemented</b></description>
    ///     </item>
    ///     <item>
    ///         <term><see cref="WorkflowRuntimeBehavior" /></term>
    ///         <description><b>Not Implemented</b></description>
    ///     </item>
    /// </list>
    /// <para>
    /// The XML format for a service behavior is the same as found in
    /// standard .NET configuration files.  Here's an example:
    /// </para>
    /// <code language="none">
    /// &lt;behavior&gt;
    ///     &lt;serviceThrottling maxConcurrentCalls="100" /&gt;
    ///     &lt;serviceTimeouts transactionTimeout="00:01:00" /&gt;
    /// &lt;/behavior&gt;
    /// </code>
    /// </remarks>
    /// <threadsafety instance="false" />
    public sealed class WcfServiceHost
    {
        private ServiceHost     host;
        private bool            isRunning;
        private object          service;
        private bool            mexEnabled;

        /// <summary>
        /// Constructor.
        /// </summary>
        /// <param name="service">The service instance.</param>
        /// <remarks>
        /// <para>
        /// This constructor associates a service object instance with the
        /// service host.  The object must be configured with the <see cref="InstanceContextMode.Single" /> 
        /// and <see cref="ConcurrencyMode.Multiple" /> service behaviors.
        /// </para>
        /// </remarks>
        /// <exception cref="ArgumentException">Thrown if the service doesn't meet the behavior constraints.</exception>
        public WcfServiceHost(object service)
        {
            System.Type                 type = service.GetType();
            ServiceBehaviorAttribute    behavior;

            this.service = service;

            behavior = (ServiceBehaviorAttribute)Helper.GetCustomAttribute(type, typeof(ServiceBehaviorAttribute), true);
            if (behavior == null ||
                behavior.InstanceContextMode != InstanceContextMode.Single ||
                behavior.ConcurrencyMode != ConcurrencyMode.Multiple)
            {
                throw new ArgumentException(string.Format("Service type [{0}] is must be tagged with [ServiceBehavior(InstanceContextMode=InstanceContextMode.Single,ConcurrencyMode=ConcurrencyMode.Multiple)].", type.FullName));
            }

            host       = new ServiceHost(service);
            isRunning  = false;
            mexEnabled = false;
        }

        /// <summary>
        /// Returns the underlying WCF <see cref="ServiceHost" /> instance.
        /// </summary>
        public ServiceHost Host
        {
            get { return host; }
        }

        /// <summary>
        /// Exposes the service description metadata (WSDL) via a standard HTTP/HTTPS GET.
        /// </summary>
        /// <param name="httpUri">The HTTP URI to be exposed (or <c>null</c>).</param>
        /// <param name="httpsUri">The HTTPS URI to be exposed (or <c>null</c>).</param>
        /// <remarks>
        /// <note>
        /// This method must be called before the service is started and also that
        /// any query string arguments passed will be ignored.
        /// </note>
        /// </remarks>
        /// <exception cref="InvalidOperationException">Thrown if the service has already started or if this method as already been called.</exception>
        public void ExposeServiceDescription(string httpUri, string httpsUri)
        {
            int pos;

            if (isRunning)
                throw new InvalidOperationException("ExposeServiceDescription() cannot be called after the service has started.");

            if (host.Description.Behaviors.Find<ServiceMetadataBehavior>() != null)
                throw new InvalidOperationException("ExposeServiceDescription() has already been called for this service.");

            if (httpUri == null && httpsUri == null)
                return;

            var behavior = new ServiceMetadataBehavior();

            if (httpUri != null)
            {
                pos = httpUri.IndexOf('?');    // Strip any query arguments
                if (pos != -1)
                    httpUri = httpUri.Substring(0, pos);

                behavior.HttpGetEnabled = true;
                behavior.HttpGetUrl     = new Uri(httpUri);
            }

            if (httpsUri != null)
            {
                pos = httpsUri.IndexOf('?');    // Strip any query arguments
                if (pos != -1)
                    httpsUri = httpsUri.Substring(0, pos);

                behavior.HttpsGetEnabled = true;
                behavior.HttpsGetUrl     = new Uri(httpsUri);
            }

            host.Description.Behaviors.Add(behavior);

            mexEnabled = true;
        }

        /// <summary>
        /// Exposes a service endpoint for the specified service interface type.
        /// </summary>
        /// <param name="interfaceType">The interface type.</param>
        /// <param name="endpoint">The WCF endpoint.</param>
        /// <remarks>
        /// <note>
        /// This method cannot be called after the service has started.
        /// </note>
        /// </remarks>
        /// <exception cref="InvalidOperationException">Thrown if the service has already started or the interface is not tagged with [ServiceContract].</exception>
        public void AddServiceEndpoint(System.Type interfaceType, WcfEndpoint endpoint)
        {
            if (isRunning)
                throw new InvalidOperationException("AddServiceEndpoint() cannot be called after the service has started.");

            if (Helper.GetCustomAttribute(interfaceType, typeof(ServiceContractAttribute), true) == null)
                throw new ArgumentException(string.Format("Service interface [{0}] is not tagged with [ServiceContract].", interfaceType.FullName));

            host.AddServiceEndpoint(interfaceType, endpoint.Binding, endpoint.Uri);
        }

        /// <summary>
        /// Exposes a service endpoint for the specified service interface type.
        /// </summary>
        /// <param name="interfaceType">The interface type.</param>
        /// <param name="endpoint">The WCF endpoint as a string.</param>
        /// <remarks>
        /// <note>
        /// This method cannot be called after the service has started.
        /// </note>
        /// </remarks>
        /// <exception cref="InvalidOperationException">Thrown if the service has already started or the interface is not tagged with [ServiceContract].</exception>
        public void AddServiceEndpoint(System.Type interfaceType, string endpoint)
        {
            AddServiceEndpoint(interfaceType, WcfEndpoint.Parse(endpoint));
        }

        /// <summary>
        /// Exposes a set of service endpoints for the specified service interface type.
        /// </summary>
        /// <param name="interfaceType">The interface type.</param>
        /// <param name="endpoints">The WCF endpoint array.</param>
        /// <remarks>
        /// <note>
        /// This method cannot be called after the service has started.
        /// </note>
        /// </remarks>
        /// <exception cref="InvalidOperationException">Thrown if the service has already started or the interface is not tagged with [ServiceContract].</exception>
        public void AddServiceEndpoint(System.Type interfaceType, WcfEndpoint[] endpoints)
        {
            for (int i = 0; i < endpoints.Length; i++)
                AddServiceEndpoint(interfaceType, endpoints[i]);
        }

        /// <summary>
        /// Parses the service behaviors encoded as XML and applies them to the hosted service.
        /// </summary>
        /// <param name="behaviors">The service behavior XML (or <c>null</c> or an empty string).</param>
        /// <remarks>
        /// <para>
        /// The current implementation supports the parsing of a limited set of possible
        /// service behaviors implemented by WCF.  Support for the other behaviors will be 
        /// added in a future release.  Here's the current implementation status:
        /// </para>
        /// <list type="table">
        ///     <item>
        ///         <term><see cref="DataContractSerializer" /></term>
        ///         <description><b>Not Implemented</b></description>
        ///     </item>
        ///     <item>
        ///         <term><see cref="PersistenceProvider" /></term>
        ///         <description><b>Not Implemented</b></description>
        ///     </item>
        ///     <item>
        ///         <term><see cref="ServiceAuthorizationBehavior" /></term>
        ///         <description><b>Not Implemented</b></description>
        ///     </item>
        ///     <item>
        ///         <term><see cref="ServiceCredentials" /></term>
        ///         <description><b>Not Implemented</b></description>
        ///     </item>
        ///     <item>
        ///         <term><see cref="ServiceDebugBehavior" /></term>
        ///         <description><b>Not Implemented</b></description>
        ///     </item>
        ///     <item>
        ///         <term><see cref="ServiceMetadataBehavior" /></term>
        ///         <description><b>Not Implemented</b></description>
        ///     </item>
        ///     <item>
        ///         <term><see cref="ServiceSecurityAuditBehavior" /></term>
        ///         <description><b>Implemented</b></description>
        ///     </item>
        ///     <item>
        ///         <term><see cref="ServiceThrottlingBehavior" /></term>
        ///         <description><b>Implemented</b></description>
        ///     </item>
        ///     <item>
        ///         <term><see cref="ServiceTimeoutsBehavior" /></term>
        ///         <description><b>Implemented</b></description>
        ///     </item>
        ///     <item>
        ///         <term><see cref="WorkflowRuntimeBehavior" /></term>
        ///         <description><b>Not Implemented</b></description>
        ///     </item>
        /// </list>
        /// <para>
        /// The XML format for a service behavior is the same as found in
        /// standard .NET configuration files.  Here's an example:
        /// </para>
        /// <code language="none">
        /// &lt;behavior&gt;
        ///     &lt;serviceThrottling maxConcurrentCalls="100" /&gt;
        ///     &lt;serviceTimeouts transactionTimeout="00:01:00" /&gt;
        /// &lt;/behavior&gt;
        /// </code>
        /// </remarks>
        public void AddBehaviors(string behaviors)
        {
            if (string.IsNullOrWhiteSpace(behaviors))
                return;

            LillTek.Xml.XmlNode root = LillTek.Xml.XmlNode.Parse(behaviors);

            if (root.Name != "behavior")
                throw new ArgumentException("<behavior> expected as the root XML element.");

            try
            {
                // Default service behavior attributes.

                ServiceBehaviorAttribute serviceBehavior = (ServiceBehaviorAttribute)host.Description.Behaviors[typeof(ServiceBehaviorAttribute)];

                if (serviceBehavior != null)
                {
                    string      sTimeout;
                    TimeSpan    timeout;

                    sTimeout = root["/serviceTimeouts/transactionTimeout"];
                    if (sTimeout != null)
                    {
                        timeout = Serialize.Parse(sTimeout, TimeSpan.Zero);
                        if (timeout > TimeSpan.Zero)
                            serviceBehavior.TransactionTimeout = timeout.ToString();
                    }
                }

                // ServiceSecurityAudit

                var serviceSecurityAudit = new ServiceSecurityAuditBehavior();

                serviceSecurityAudit.AuditLogLocation                = Serialize.Parse<AuditLogLocation>(root["/serviceSecurityAudit/auditLogLocation"], serviceSecurityAudit.AuditLogLocation);
                serviceSecurityAudit.MessageAuthenticationAuditLevel = Serialize.Parse<AuditLevel>(root["/serviceSecurityAudit/messageAuthenticationAuditLevel"], serviceSecurityAudit.MessageAuthenticationAuditLevel);
                serviceSecurityAudit.ServiceAuthorizationAuditLevel  = Serialize.Parse<AuditLevel>(root["/serviceSecurityAudit/serviceAuthorizationAuditLevel"], serviceSecurityAudit.ServiceAuthorizationAuditLevel);
                serviceSecurityAudit.SuppressAuditFailure            = Serialize.Parse(root["/serviceSecurityAudit/suppressAuditFailure"], serviceSecurityAudit.SuppressAuditFailure);

                host.Description.Behaviors.Add(serviceSecurityAudit);

                // ServiceThrottling

                var serviceThrottling = new ServiceThrottlingBehavior();

                serviceThrottling.MaxConcurrentCalls     = Serialize.Parse(root["/serviceThrottling/maxConcurrentCalls"], serviceThrottling.MaxConcurrentCalls);
                serviceThrottling.MaxConcurrentInstances = Serialize.Parse(root["/serviceThrottling/maxConcurrentInstances"], serviceThrottling.MaxConcurrentInstances);
                serviceThrottling.MaxConcurrentSessions  = Serialize.Parse(root["/serviceThrottling/maxConcurrentSessions"], serviceThrottling.MaxConcurrentSessions);

                host.Description.Behaviors.Add(serviceThrottling);

                // Check for unsupported behaviors

                var unsupported = new string[] 
                {    
                    "dataContractSerializer",
                    "persistenceProvider",
                    "serviceAuthorization",
                    "serviceCredentials",
                    "serviceDebug",
                    "serviceMetadata",
                    "workflowRuntime"
                };

                foreach (string behavior in unsupported)
                    if (root.GetNode(behavior) != null)
                        throw new NotImplementedException(string.Format("<{0}> behavior parsing is not supported by the LillTek Platform at this time.", behavior));
            }
            catch (Exception e)
            {
                throw new ArgumentException("Error parsing WcfServiceHost behavior: " + e.Message, e);
            }
        }

        /// <summary>
        /// Returns the service instance.
        /// </summary>
        public object Service
        {
            get { return service; }
        }

        /// <summary>
        /// Used internally by the Start() method.
        /// </summary>
        private struct MexBindingInfo
        {
            public Binding      Binding;
            public string       Address;

            public MexBindingInfo(Binding binding, string address)
            {
                this.Binding = binding;
                this.Address = address;
            }
        }

        /// <summary>
        /// Starts the service.
        /// </summary>
        public void Start()
        {
            if (isRunning)
                throw new InvalidOperationException("The service is already running.");

            // If metadata exchange is enabled then create MEX service endpoints for each 
            // hosted HTTP service endpoint.

            if (mexEnabled)
            {
                var mexEndpoints = new List<MexBindingInfo>();

                foreach (ServiceEndpoint endPoint in host.Description.Endpoints)
                {
                    var     basicHttpEP = endPoint.Binding as BasicHttpBinding;
                    var     wsHttpEP    = endPoint.Binding as WSHttpBinding;
                    string  mexUri      = null;

                    if (basicHttpEP != null || wsHttpEP != null)
                        mexUri = endPoint.ListenUri.ToString() + "/mex";

                    if (mexUri != null)
                        mexEndpoints.Add(new MexBindingInfo(MetadataExchangeBindings.CreateMexHttpBinding(), mexUri));
                }

                foreach (MexBindingInfo mbi in mexEndpoints)
                    host.AddServiceEndpoint(typeof(IMetadataExchange), mbi.Binding, mbi.Address);
            }

            // Start the service

            host.Open();
            isRunning = true;
        }

        /// <summary>
        /// Returns <c>true</c> if the service is running.
        /// </summary>
        public bool IsRunning
        {
            get { return isRunning; }
        }

        /// <summary>
        /// Stops the service if it is running.
        /// </summary>
        public void Stop()
        {
            if (isRunning)
            {
                host.Close();
                isRunning = false;
            }
        }
    }
}
