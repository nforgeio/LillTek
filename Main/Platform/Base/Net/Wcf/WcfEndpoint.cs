//-----------------------------------------------------------------------------
// FILE:        WcfEndpoint.cs
// CONTRIBUTOR: Jeff Lill
// COPYRIGHT:   Copyright (c) 2005-2015 by Jeffrey Lill.  All rights reserved.
// DESCRIPTION: Provides for specifying a WCF service endpoint as a
//              simple string suitable for placing in an application
//              configuration file.

using System;
using System.Xml;
using System.Net;
using System.Net.Sockets;
using System.Net.Security;
using System.Text;
using System.Threading;
using System.Collections.Generic;
using System.ServiceModel;
using System.ServiceModel.Security;
using System.ServiceModel.Channels;
using System.ServiceModel.Description;

using LillTek.Common;
using LillTek.ServiceModel;
using LillTek.ServiceModel.Channels;
using LillTek.Xml;

namespace LillTek.Net.Wcf
{
    /// <summary>
    /// Provides for specifying WCF endpoint binding information as a 
    /// simple string suitable for placing in an application configuration 
    /// file.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <see cref="WcfEndpoint" /> instances specify the information necessary
    /// to create a WCF service binding to a <see cref="WcfServiceHost" /> 
    /// instance.  This information includes the type of the binding (HTTP, TCP,
    /// MSMQ, etc) and the binding URI.
    /// </para>
    /// <para>
    /// WCF endpoints are formatted as standard <see cref="ArgCollection" />s using
    /// "=" as the assignment character and ";" as the separator.  The standard
    /// arguments parsed are:
    /// </para>
    /// <div class="tablediv">
    /// <table class="dtTABLE" cellspacing="0" ID="Table1">
    /// <tr valign="top">
    /// <th width="1">Argument</th>        
    /// <th width="1">Default</th>
    /// <th width="90%">Description</th>
    /// </tr>
    /// <tr valign="top">
    ///     <td>Binding</td>
    ///     <td>(required)</td>
    ///     <td>
    ///     This specifies the binding type.  The currently recognized
    ///     binding types are described below.  This argument is
    ///     required.
    ///     </td>
    /// </tr>
    /// <tr valign="top">
    ///     <td>Uri</td>
    ///     <td>(required)</td>
    ///     <td>
    ///     The endpoint URI.
    ///     </td>
    /// </tr>
    /// <tr valign="top">
    ///     <td>MaxMessageSize</td>
    ///     <td>64K</td>
    ///     <td>
    ///     The maximum size of the message that can be processed by the endpoint.
    ///     Not all bindings honor this argument.
    ///     </td>
    /// </tr>
    /// <tr valign="top">
    ///     <td>Settings</td>
    ///     <td>(see note)</td>
    ///     <td>
    ///     <para>
    ///     Specifies the custom binding settings.  The possible values
    ///     vary depending on the binding type and can be quite complex.  This is encoded
    ///     as XML using the same format as for WCF bindings in .NET configuration files.
    ///     The default settings vary depending on the binding type.
    ///     </para>
    ///     <para>
    ///     Here's an example:
    ///     </para>
    ///     <code lang="none">
    ///         &lt;basicHttpBinding messageEncoding="Text"&gt;
    ///             &lt;security mode="Message" /&gt;
    ///         &lt;/basicHttpBinding&gt;
    ///     </code>
    ///     </td>
    /// </tr>
    /// </table>
    /// </div>
    /// <para>
    /// The bindings currently supported are:
    /// </para>
    /// <list type="table">
    ///     <item>
    ///         <term>LillTek</term>
    ///         <description>Specifies the LillTek Messaging WCF binding.</description>
    ///     </item>
    ///     <item>
    ///         <term>BasicHttp</term>
    ///         <description>Specifies the built-in WCF <see cref="BasicHttpBinding" />.</description>
    ///     </item>
    ///     <item>
    ///         <term>Http</term>
    ///         <description>Specifies the built-in WCF <see cref="WSHttpBinding" />.</description>
    ///     </item>
    ///     <item>
    ///         <term>DualHttp</term>
    ///         <description>Specifies the built-in WCF <see cref="WSDualHttpBinding" />.</description>
    ///     </item>
    ///     <item>
    ///         <term>FederationHttp</term>
    ///         <description>Specifies the built-in WCF <see cref="WSFederationHttpBinding" />.</description>
    ///     </item>
    ///     <item>
    ///         <term>Tcp</term>
    ///         <description>Specifies the built-in WCF <see cref="NetTcpBinding" />.</description>
    ///     </item>
    ///     <item>
    ///         <term>NamedPipe</term>
    ///         <description>Specifies the built-in WCF <see cref="NetNamedPipeBinding" />.</description>
    ///     </item>
    ///     <item>
    ///         <term>Msmq</term>
    ///         <description>Specifies the built-in WCF <see cref="NetMsmqBinding" />.</description>
    ///     </item>
    ///     <item>
    ///         <term>PeerTcp</term>
    ///         <description>Specifies the built-in WCF <see cref="NetPeerTcpBinding" />.</description>
    ///     </item>
    ///     <item>
    ///         <term>MsmqIntegration</term>
    ///         <description>Specifies to the built-in WCF <see cref="System.ServiceModel.MsmqIntegration.MsmqIntegrationBinding" />.</description>
    ///     </item>
    /// </list>
    /// <para>
    /// Here's an example configuration as a configuration setting value:
    /// </para>
    /// <code language="none">
    /// MyEndpoint = {{
    /// 
    ///     Binding  = BasicHttp;
    ///     Uri      = http://test.com/MyEndPoint.svc;
    ///     Settings = 
    ///     
    ///         &lt;basicHttpBinding&gt;
    ///             &lt;binding name="Binding1"
    ///                       hostNameComparisonMode="StrongWildcard"
    ///                       receiveTimeout="00:10:00"
    ///                       sendTimeout="00:10:00"
    ///                       openTimeout="00:10:00"
    ///                       closeTimeout="00:10:00"
    ///                       maxReceivedMessageSize="65536"
    ///                       maxBufferSize="65536"
    ///                       maxBufferPoolSize="524288"
    ///                       transferMode="Buffered"
    ///                       messageEncoding="Text"
    ///                       textEncoding="utf-8"
    ///                       bypassProxyOnLocal="false"
    ///                       useDefaultWebProxy="true" &gt;
    ///                 &lt;security mode="None" /&gt;
    ///             &lt;/binding&gt;
    ///         &lt;/basicHttpBinding&gt;;
    /// </code>
    /// </remarks>
    public sealed class WcfEndpoint
    {
        //---------------------------------------------------------------------
        // Static members

        /// <summary>
        /// Parses a <see cref="WcfEndpoint" /> from a string formatted as a standard
        /// <see cref="ArgCollection" />.
        /// </summary>
        /// <param name="input">The input string.</param>
        /// <returns>The <see cref="WcfEndpoint" /> instance.</returns>
        public static WcfEndpoint Parse(string input)
        {
            return new WcfEndpoint(input);
        }

        /// <summary>
        /// Parses a <see cref="WcfEndpoint" /> from a configuration setting.
        /// </summary>
        /// <param name="config">The configuration settings.</param>
        /// <param name="key">The setting name.</param>
        /// <returns>The endpoint or <c>null</c> if the setting was not present.</returns>
        /// <exception cref="ArgumentException">Thrown if the endpoint is not properly formatted.</exception>
        public static WcfEndpoint LoadConfig(Config config, string key)
        {
            string input;

            input = config.Get(key, (string)null);
            if (input == null)
                return null;

            return Parse(input);
        }

        /// <summary>
        /// Parses an array of <see cref="WcfEndpoint" /> instances from a configuration 
        /// setting array.
        /// </summary>
        /// <param name="config">The configuration settings.</param>
        /// <param name="key">The base setting key name.</param>
        /// <returns>The array of endpoints.</returns>
        /// /// <exception cref="ArgumentException">Thrown if any of the endpoints are not properly formatted.</exception>
        public static WcfEndpoint[] LoadConfigArray(Config config, string key)
        {
            WcfEndpoint[]   endpoints;
            string[]        input;

            input     = config.GetArray(key);
            endpoints = new WcfEndpoint[input.Length];

            for (int i = 0; i < input.Length; i++)
                endpoints[i] = Parse(input[i]);

            return endpoints;
        }

        //---------------------------------------------------------------------
        // Instance members

        private Binding binding;        // The channel binding
        private Uri uri;            // The endpoint URI
        private ArgCollection args;           // The endpoint arguments

        /// <summary>
        /// Parses a <see cref="WcfEndpoint" /> from a string formatted as a standard
        /// <see cref="ArgCollection" />.
        /// </summary>
        /// <param name="input">The input string.</param>
        public WcfEndpoint(string input)
        {
            string      bindingName;
            string      arg;
            int         cbMsgMax;
            int         cbBuffer;
            string      settings;

            args = ArgCollection.Parse(input);

            // Get the endpoint address URI

            arg = args.Get("uri");
            if (arg == null)
                throw new ArgumentException("[Uri] argument is required.");

            try
            {
                uri = new Uri(arg);
            }
            catch
            {
                throw new ArgumentException(string.Format("Invalid [Uri={0}].", arg));
            }

            // Initialize the binding

            arg = args.Get("binding");
            if (arg == null)
                throw new ArgumentException("[Binding] argument is required.");

            cbMsgMax = args.Get("MaxMessageSize", 64 * 1024);
            cbBuffer = Math.Min(64 * 1024, cbMsgMax);

            settings = args.Get("Settings");

            bindingName = arg.ToUpper();
            switch (bindingName)
            {
                case "LILLTEK":

                    LillTekBinding lilltekBinding;

                    binding = lilltekBinding = new LillTekBinding(uri);

                    // $todo(jeff.lill): The LillTek transport needs to implement settings.

                    break;

                case "BASICHTTP":

                    BasicHttpBinding basicBinding;

                    binding = basicBinding = new BasicHttpBinding();
                    basicBinding.MaxBufferSize = cbBuffer;
                    basicBinding.MaxReceivedMessageSize = cbMsgMax;

                    LoadSettings(basicBinding, settings);
                    break;

                case "HTTP":

                    WSHttpBinding wsHttpBinding;

                    binding = wsHttpBinding = new WSHttpBinding();
                    wsHttpBinding.MaxReceivedMessageSize = cbMsgMax;

                    LoadSettings(wsHttpBinding, settings);
                    break;

                case "DUALHTTP":

                    WSDualHttpBinding wsDualHttpBinding;

                    binding = wsDualHttpBinding = new WSDualHttpBinding();
                    wsDualHttpBinding.MaxReceivedMessageSize = cbMsgMax;

                    LoadSettings(wsDualHttpBinding, settings);
                    break;

                case "FEDERATIONHTTP":

                    WSFederationHttpBinding wsFederationHttpBinding;

                    binding = wsFederationHttpBinding = new WSFederationHttpBinding();
                    wsFederationHttpBinding.MaxReceivedMessageSize = cbMsgMax;

                    LoadSettings(wsFederationHttpBinding, settings);
                    break;

                case "TCP":

                    NetTcpBinding netTcpBinding;

                    binding = netTcpBinding = new NetTcpBinding();
                    netTcpBinding.MaxBufferSize = cbBuffer;
                    netTcpBinding.MaxReceivedMessageSize = cbMsgMax;

                    LoadSettings(netTcpBinding, settings);
                    break;

                case "NAMEDPIPE":

                    NetNamedPipeBinding netPipeBinding;

                    binding = netPipeBinding = new NetNamedPipeBinding();
                    netPipeBinding.MaxBufferSize = cbBuffer;
                    netPipeBinding.MaxReceivedMessageSize = cbMsgMax;

                    LoadSettings(netPipeBinding, settings);
                    break;

                case "MSMQ":

                    NetMsmqBinding netMsmqBinding;

                    binding = netMsmqBinding = new NetMsmqBinding();
                    netMsmqBinding.MaxReceivedMessageSize = cbMsgMax;

                    LoadSettings(netMsmqBinding, settings);
                    break;

                case "PEERTCP":

                    NetPeerTcpBinding netPeerTcpBinding;

                    binding = netPeerTcpBinding = new NetPeerTcpBinding();
                    netPeerTcpBinding.MaxReceivedMessageSize = cbMsgMax;

                    LoadSettings(netPeerTcpBinding, settings);
                    break;

                case "MSMQINTEGRATION":

                    System.ServiceModel.MsmqIntegration.MsmqIntegrationBinding msmqIntegrationBinding;

                    binding = msmqIntegrationBinding = new System.ServiceModel.MsmqIntegration.MsmqIntegrationBinding();
                    msmqIntegrationBinding.MaxReceivedMessageSize = cbMsgMax;

                    LoadSettings(msmqIntegrationBinding, settings);
                    break;

                default:

                    throw new ArgumentException(string.Format("Unsupported WcfEndpoint binding type [Binding={0}].", arg));
            }
        }

        /// <summary>
        /// Returns the underlying binding instance.
        /// </summary>
        public Binding Binding
        {
            get { return binding; }
        }

        /// <summary>
        /// Returns the binding URI.
        /// </summary>
        public Uri Uri
        {
            get { return uri; }
        }

        /// <summary>
        /// Renders the endpoint as a string.
        /// </summary>
        /// <returns>The endpoint string.</returns>
        public override string ToString()
        {
            return args.ToString();
        }

        private EnvelopeVersion Parse(string value, EnvelopeVersion def)
        {
            if (value == null)
                return def;

            switch (value.ToLowerInvariant())
            {
                case "soap11":  return EnvelopeVersion.Soap11;
                case "soap12":  return EnvelopeVersion.Soap12;
                default:        throw new ArgumentException(string.Format("Unknown WCF [EnvelopeVersion]: {0}", value));
            }
        }

        private MessageVersion Parse(string value, MessageVersion def)
        {
            if (value == null)
                return def;

            switch (value.ToLowerInvariant())
            {
                case "default":                         return MessageVersion.Default;
                case "none":                            return MessageVersion.None;
                case "soap11":                          return MessageVersion.Soap11;
                case "soap11wsaddressing10":            return MessageVersion.Soap11WSAddressing10;
                case "soap11wsaddressingaugust2004":    return MessageVersion.Soap11WSAddressingAugust2004;
                case "soap12":                          return MessageVersion.Soap12;
                case "soap12wsaddressing10":            return MessageVersion.Soap12WSAddressing10;
                case "soap12wsaddressingaugust2004":    return MessageVersion.Soap12WSAddressingAugust2004;
                default:                                throw new ArgumentException(string.Format("Unknown WCF [MessageVersion]: {0}", value));
            }
        }

        private void Parse(XmlDictionaryReaderQuotas readerQuotas, LillTek.Xml.XmlNode root)
        {
            readerQuotas.MaxArrayLength         = Serialize.Parse(root["/readerQuotas/maxArrayLength"], readerQuotas.MaxArrayLength);
            readerQuotas.MaxBytesPerRead        = Serialize.Parse(root["/readerQuotas/maxBytesPerRead"], readerQuotas.MaxBytesPerRead);
            readerQuotas.MaxDepth               = Serialize.Parse(root["/readerQuotas/maxDepth"], readerQuotas.MaxDepth);
            readerQuotas.MaxNameTableCharCount  = Serialize.Parse(root["/readerQuotas/maxNameTableCharCount"], readerQuotas.MaxNameTableCharCount);
            readerQuotas.MaxStringContentLength = Serialize.Parse(root["/readerQuotas/maxStringContentLength"], readerQuotas.MaxStringContentLength);
        }

        private Encoding Parse(string value, Encoding def)
        {
            if (value == null)
                return def;

            return Encoding.GetEncoding(value);
        }

        private SecurityAlgorithmSuite Parse(string value, SecurityAlgorithmSuite def)
        {
            if (value == null)
                return def;

            switch (value.ToLowerInvariant())
            {
                case "basic128":                return SecurityAlgorithmSuite.Basic128;
                case "basic128rsa15":           return SecurityAlgorithmSuite.Basic128Rsa15;
                case "basic128sha256":          return SecurityAlgorithmSuite.Basic128Sha256;
                case "basic128sha256Rsa15":     return SecurityAlgorithmSuite.Basic128Sha256Rsa15;
                case "basic192":                return SecurityAlgorithmSuite.Basic192;
                case "basic192rsa15":           return SecurityAlgorithmSuite.Basic192Rsa15;
                case "basic192sha256":          return SecurityAlgorithmSuite.Basic192Sha256;
                case "basic192sha256rsa15":     return SecurityAlgorithmSuite.Basic192Sha256Rsa15;
                case "basic256":                return SecurityAlgorithmSuite.Basic256;
                case "basic256rsa15":           return SecurityAlgorithmSuite.Basic256Rsa15;
                case "basic256sha256":          return SecurityAlgorithmSuite.Basic256Sha256;
                case "basic256sha256rsa15":     return SecurityAlgorithmSuite.Basic256Sha256Rsa15;
                case "default":                 return SecurityAlgorithmSuite.Default;
                case "tripledes":               return SecurityAlgorithmSuite.TripleDes;
                case "tripledesrsa15":          return SecurityAlgorithmSuite.TripleDesRsa15;
                case "tripledessha256":         return SecurityAlgorithmSuite.TripleDesSha256;
                case "tripledessha256rsa15":    return SecurityAlgorithmSuite.TripleDesSha256Rsa15;
                default:                        throw new ArgumentException(string.Format("Unknown WCF [AlgorithmSuite]: {0}", value));
            }
        }

        private TransactionProtocol Parse(string value, TransactionProtocol def)
        {
            if (value == null)
                return def;

            switch (value.ToLowerInvariant())
            {
                case "default":                         return TransactionProtocol.Default;
                case "oletransactions":                 return TransactionProtocol.OleTransactions;
                case "wsatomictransaction11 ":          return TransactionProtocol.WSAtomicTransaction11;
                case "wsatomictransactionoctober2004":  return TransactionProtocol.WSAtomicTransactionOctober2004;
                default:                                throw new ArgumentException(string.Format("Unknown WCF [TransactionProtocol]: {0}", value));
            }
        }

        /// <summary>
        /// Loads the binding's security settings from a XML encoded string.
        /// </summary>
        /// <param name="binding">The binding.</param>
        /// <param name="settings">The settings.</param>
        /// <exception cref="ArgumentException">Thrown if the settings are not valid.</exception>
        private void LoadSettings(BasicHttpBinding binding, string settings)
        {
            if (string.IsNullOrWhiteSpace(settings))
                return;

            LillTek.Xml.XmlNode root = LillTek.Xml.XmlNode.Parse(settings);

            if (root.Name != "basicHttpBinding")
                throw new ArgumentException("<basicHttpBinding> XML element expected for BasicHttpBinding configuration settings.");

            try
            {
                // Parse the basic properties
 
                binding.AllowCookies           = Serialize.Parse(root["/allowCookies"], binding.AllowCookies);
                binding.BypassProxyOnLocal     = Serialize.Parse(root["/bypassProxyOnLocal"], binding.BypassProxyOnLocal);
                binding.CloseTimeout           = Serialize.Parse(root["/closeTimeout"], binding.CloseTimeout);
                binding.HostNameComparisonMode = Serialize.Parse<HostNameComparisonMode>(root["/hostNameComparisonMode"], binding.HostNameComparisonMode);
                binding.MaxBufferPoolSize      = Serialize.Parse(root["/maxBufferPoolSize"], binding.MaxBufferPoolSize);
                binding.MaxBufferSize          = Serialize.Parse(root["/maxBufferSize"], binding.MaxBufferSize);
                binding.MaxReceivedMessageSize = Serialize.Parse(root["/maxReceivedMessageSize"], binding.MaxReceivedMessageSize);
                binding.MessageEncoding        = Serialize.Parse<WSMessageEncoding>(root["/messageEncoding"], binding.MessageEncoding);
                binding.Name                   = Serialize.Parse(root["/name"], binding.Name);
                binding.Namespace              = Serialize.Parse(root["/namespace"], binding.Namespace);
                binding.OpenTimeout            = Serialize.Parse(root["/openTimeout"], binding.OpenTimeout);
                binding.ProxyAddress           = Serialize.Parse(root["/proxyAddress"], binding.ProxyAddress);
                binding.ReceiveTimeout         = Serialize.Parse(root["/receiveTimeout"], binding.ReceiveTimeout);
                binding.SendTimeout            = Serialize.Parse(root["/sendTimeout"], binding.SendTimeout);
                binding.TextEncoding           = Parse(root["/textEncoding"], binding.TextEncoding);
                binding.TransferMode           = Serialize.Parse<TransferMode>(root["/transferMode"], binding.TransferMode);
                binding.UseDefaultWebProxy     = Serialize.Parse(root["/useDefaultWebProxy"], binding.UseDefaultWebProxy);

                // Parse the reader quotas

                Parse(binding.ReaderQuotas, root);

                // Parse the Security settings

                binding.Security.Message.AlgorithmSuite         = Parse(root["/security/message/algorithmSuite"], binding.Security.Message.AlgorithmSuite);
                binding.Security.Message.ClientCredentialType   = Serialize.Parse<BasicHttpMessageCredentialType>(root["/security/message/clientCredentialType"], binding.Security.Message.ClientCredentialType);

                binding.Security.Mode                           = Serialize.Parse<BasicHttpSecurityMode>(root["/security/mode"], binding.Security.Mode);

                binding.Security.Transport.ClientCredentialType = Serialize.Parse<HttpClientCredentialType>(root["/security/transport/clientCredentialType"], binding.Security.Transport.ClientCredentialType);
                binding.Security.Transport.ProxyCredentialType  = Serialize.Parse<HttpProxyCredentialType>(root["/security/transport/proxyCredentialType"], binding.Security.Transport.ProxyCredentialType);
                binding.Security.Transport.Realm                = root.GetPropStr("/security/transport/realm", binding.Security.Transport.Realm);
            }
            catch (Exception e)
            {
                throw new ArgumentException("Error parsing WcfEndpoint settings: " + e.Message, e);
            }
        }

        /// <summary>
        /// Loads the binding's settings from a XML encoded string.
        /// </summary>
        /// <param name="binding">The binding.</param>
        /// <param name="settings">The settings.</param>
        /// <exception cref="ArgumentException">Thrown if the settings are not valid.</exception>
        private void LoadSettings(WSHttpBinding binding, string settings)
        {
            if (string.IsNullOrWhiteSpace(settings))
                return;

            LillTek.Xml.XmlNode root = LillTek.Xml.XmlNode.Parse(settings);

            if (root.Name != "wsHttpBinding")
                throw new ArgumentException("<wsHttpBinding> XML element expected for WSHttpBinding configuration settings.");

            try
            {
                // Parse the basic properties

                binding.AllowCookies           = Serialize.Parse(root["/allowCookies"], binding.AllowCookies);
                binding.BypassProxyOnLocal     = Serialize.Parse(root["/bypassProxyOnLocal"], binding.BypassProxyOnLocal);
                binding.CloseTimeout           = Serialize.Parse(root["/closeTimeout"], binding.CloseTimeout);
                binding.HostNameComparisonMode = Serialize.Parse<HostNameComparisonMode>(root["/hostNameComparisonMode"], binding.HostNameComparisonMode);
                binding.MaxBufferPoolSize      = Serialize.Parse(root["/maxBufferPoolSize"], binding.MaxBufferPoolSize);
                binding.MaxReceivedMessageSize = Serialize.Parse(root["/maxReceivedMessageSize"], binding.MaxReceivedMessageSize);
                binding.MessageEncoding        = Serialize.Parse<WSMessageEncoding>(root["/messageEncoding"], binding.MessageEncoding);
                binding.Name                   = Serialize.Parse(root["/name"], binding.Name);
                binding.Namespace              = Serialize.Parse(root["/namespace"], binding.Namespace);
                binding.OpenTimeout            = Serialize.Parse(root["/openTimeout"], binding.OpenTimeout);
                binding.ProxyAddress           = Serialize.Parse(root["/proxyAddress"], binding.ProxyAddress);
                binding.ReceiveTimeout         = Serialize.Parse(root["/receiveTimeout"], binding.ReceiveTimeout);
                binding.SendTimeout            = Serialize.Parse(root["/sendTimeout"], binding.SendTimeout);
                binding.TextEncoding           = Parse(root["/textEncoding"], binding.TextEncoding);
                binding.TransactionFlow        = Serialize.Parse(root["/transactionFlow"], binding.TransactionFlow);
                binding.UseDefaultWebProxy     = Serialize.Parse(root["/useDefaultWebProxy"], binding.UseDefaultWebProxy);

                // Parse the reader quotas

                Parse(binding.ReaderQuotas, root);

                // Parse the reliable session settings

                binding.ReliableSession.Enabled           = Serialize.Parse(root["/reliableSession/enabled"], binding.ReliableSession.Enabled);
                binding.ReliableSession.InactivityTimeout = Serialize.Parse(root["/reliableSession/inactivityTimeout"], binding.ReliableSession.InactivityTimeout);
                binding.ReliableSession.Ordered           = Serialize.Parse(root["/reliableSession/ordered"], binding.ReliableSession.Ordered);

                // Parse the Security settings

                binding.Security.Message.AlgorithmSuite             = Parse(root["/security/message/algorithmSuite"], binding.Security.Message.AlgorithmSuite);
                binding.Security.Message.ClientCredentialType       = Serialize.Parse<MessageCredentialType>(root["/security/message/clientCredentialType"], binding.Security.Message.ClientCredentialType);
                binding.Security.Message.EstablishSecurityContext   = Serialize.Parse(root["/security/message/establishSecurityContext"], binding.Security.Message.EstablishSecurityContext);
                binding.Security.Message.NegotiateServiceCredential = Serialize.Parse(root["/security/message/negotiateServiceCredential"], binding.Security.Message.NegotiateServiceCredential);

                binding.Security.Mode                               = Serialize.Parse<SecurityMode>(root["/security/mode"], binding.Security.Mode);

                binding.Security.Transport.ClientCredentialType     = Serialize.Parse<HttpClientCredentialType>(root["/security/transport/clientCredentialType"], binding.Security.Transport.ClientCredentialType);
                binding.Security.Transport.ProxyCredentialType      = Serialize.Parse<HttpProxyCredentialType>(root["/security/transport/proxyCredentialType"], binding.Security.Transport.ProxyCredentialType);
                binding.Security.Transport.Realm                    = root.GetPropStr("/security/transport/realm", binding.Security.Transport.Realm);
            }
            catch (Exception e)
            {
                throw new ArgumentException("Error parsing WcfEndpoint settings: " + e.Message, e);
            }
        }

        /// <summary>
        /// Loads the binding's settings from a XML encoded string.
        /// </summary>
        /// <param name="binding">The binding.</param>
        /// <param name="settings">The settings.</param>
        /// <exception cref="ArgumentException">Thrown if the settings are not valid.</exception>
        private void LoadSettings(WSDualHttpBinding binding, string settings)
        {
            if (string.IsNullOrWhiteSpace(settings))
                return;

            throw new NotImplementedException();    // $todo(jeff.lill): Implement this
        }

        /// <summary>
        /// Loads the binding's settings from a XML encoded string.
        /// </summary>
        /// <param name="binding">The binding.</param>
        /// <param name="settings">The settings.</param>
        /// <exception cref="ArgumentException">Thrown if the settings are not valid.</exception>
        private void LoadSettings(WSFederationHttpBinding binding, string settings)
        {
            if (string.IsNullOrWhiteSpace(settings))
                return;

            throw new NotImplementedException();    // $todo(jeff.lill): Implement this
        }

        /// <summary>
        /// Loads the binding's settings from a XML encoded string.
        /// </summary>
        /// <param name="binding">The binding.</param>
        /// <param name="settings">The settings.</param>
        /// <exception cref="ArgumentException">Thrown if the settings are not valid.</exception>
        private void LoadSettings(NetTcpBinding binding, string settings)
        {
            if (string.IsNullOrWhiteSpace(settings))
                return;

            LillTek.Xml.XmlNode root = LillTek.Xml.XmlNode.Parse(settings);

            if (root.Name != "netTcpBinding")
                throw new ArgumentException("<netTcpBinding> XML element expected for NetTcpBinding configuration settings.");

            try
            {
                // Parse the basic properties

                binding.CloseTimeout           = Serialize.Parse(root["/closeTimeout"], binding.CloseTimeout);
                binding.HostNameComparisonMode = Serialize.Parse<HostNameComparisonMode>(root["/hostNameComparisonMode"], binding.HostNameComparisonMode);
                binding.ListenBacklog          = Serialize.Parse(root["/listenBacklog"], binding.ListenBacklog);
                binding.MaxBufferPoolSize      = Serialize.Parse(root["/maxBufferPoolSize"], binding.MaxBufferPoolSize);
                binding.MaxBufferSize          = Serialize.Parse(root["/maxBufferSize"], binding.MaxBufferSize);
                binding.MaxConnections         = Serialize.Parse(root["/maxConnections"], binding.MaxConnections);
                binding.MaxReceivedMessageSize = Serialize.Parse(root["/maxReceivedMessageSize"], binding.MaxReceivedMessageSize);
                binding.Name                   = Serialize.Parse(root["/name"], binding.Name);
                binding.Namespace              = Serialize.Parse(root["/namespace"], binding.Namespace);
                binding.OpenTimeout            = Serialize.Parse(root["/openTimeout"], binding.OpenTimeout);
                binding.PortSharingEnabled     = Serialize.Parse(root["/portSharingEnabled"], binding.PortSharingEnabled);
                binding.ReceiveTimeout         = Serialize.Parse(root["/receiveTimeout"], binding.ReceiveTimeout);
                binding.SendTimeout            = Serialize.Parse(root["/sendTimeout"], binding.SendTimeout);
                binding.TransactionFlow        = Serialize.Parse(root["/transactionFlow"], binding.TransactionFlow);
                binding.TransactionProtocol    = Parse(root["/transactionProtocol"], binding.TransactionProtocol);
                binding.TransferMode           = Serialize.Parse<TransferMode>(root["/transferMode"], binding.TransferMode);

                // Parse the reader quotas

                Parse(binding.ReaderQuotas, root);

                // Parse the reliable session settings

                binding.ReliableSession.Enabled           = Serialize.Parse(root["/reliableSession/enabled"], binding.ReliableSession.Enabled);
                binding.ReliableSession.InactivityTimeout = Serialize.Parse(root["/reliableSession/inactivityTimeout"], binding.ReliableSession.InactivityTimeout);
                binding.ReliableSession.Ordered           = Serialize.Parse(root["/reliableSession/ordered"], binding.ReliableSession.Ordered);

                // Parse the Security settings

                binding.Security.Message.AlgorithmSuite         = Parse(root["/security/message/algorithmSuite"], binding.Security.Message.AlgorithmSuite);
                binding.Security.Message.ClientCredentialType   = Serialize.Parse<MessageCredentialType>(root["/security/message/clientCredentialType"], binding.Security.Message.ClientCredentialType);

                binding.Security.Mode                           = Serialize.Parse<SecurityMode>(root["/security/mode"], binding.Security.Mode);

                binding.Security.Transport.ClientCredentialType = Serialize.Parse<TcpClientCredentialType>(root["/security/transport/clientCredentialType"], binding.Security.Transport.ClientCredentialType);
                binding.Security.Transport.ProtectionLevel      = Serialize.Parse<ProtectionLevel>(root["/security/transport/protectionLevel"], binding.Security.Transport.ProtectionLevel);
            }
            catch (Exception e)
            {
                throw new ArgumentException("Error parsing WcfEndpoint settings: " + e.Message, e);
            }
        }

        /// <summary>
        /// Loads the binding's settings from a XML encoded string.
        /// </summary>
        /// <param name="binding">The binding.</param>
        /// <param name="settings">The settings.</param>
        /// <exception cref="ArgumentException">Thrown if the settings are not valid.</exception>
        private void LoadSettings(NetNamedPipeBinding binding, string settings)
        {
            if (string.IsNullOrWhiteSpace(settings))
                return;

            LillTek.Xml.XmlNode root = LillTek.Xml.XmlNode.Parse(settings);

            if (root.Name != "netNamedPipeBinding")
                throw new ArgumentException("<netNamedPipeBinding> XML element expected for NetNamedPipeBinding configuration settings.");

            try
            {
                // Parse the basic properties

                binding.CloseTimeout           = Serialize.Parse(root["/closeTimeout"], binding.CloseTimeout);
                binding.HostNameComparisonMode = Serialize.Parse<HostNameComparisonMode>(root["/hostNameComparisonMode"], binding.HostNameComparisonMode);
                binding.MaxBufferPoolSize      = Serialize.Parse(root["/maxBufferPoolSize"], binding.MaxBufferPoolSize);
                binding.MaxBufferSize          = Serialize.Parse(root["/maxBufferSize"], binding.MaxBufferSize);
                binding.MaxConnections         = Serialize.Parse(root["/maxConnections"], binding.MaxConnections);
                binding.MaxReceivedMessageSize = Serialize.Parse(root["/maxReceivedMessageSize"], binding.MaxReceivedMessageSize);
                binding.Name                   = Serialize.Parse(root["/name"], binding.Name);
                binding.Namespace              = Serialize.Parse(root["/namespace"], binding.Namespace);
                binding.OpenTimeout            = Serialize.Parse(root["/openTimeout"], binding.OpenTimeout);
                binding.ReceiveTimeout         = Serialize.Parse(root["/receiveTimeout"], binding.ReceiveTimeout);
                binding.SendTimeout            = Serialize.Parse(root["/sendTimeout"], binding.SendTimeout);
                binding.TransactionFlow        = Serialize.Parse(root["/transactionFlow"], binding.TransactionFlow);
                binding.TransactionProtocol    = Parse(root["/transactionProtocol"], binding.TransactionProtocol);
                binding.TransferMode           = Serialize.Parse<TransferMode>(root["/transferMode"], binding.TransferMode);

                // Parse the reader quotas

                Parse(binding.ReaderQuotas, root);

                // Parse the Security settings

                binding.Security.Mode = Serialize.Parse<NetNamedPipeSecurityMode>(root["/security/mode"], binding.Security.Mode);

                binding.Security.Transport.ProtectionLevel = Serialize.Parse<ProtectionLevel>(root["/security/transport/protectionLevel"], binding.Security.Transport.ProtectionLevel);
            }
            catch (Exception e)
            {
                throw new ArgumentException("Error parsing WcfEndpoint settings: " + e.Message, e);
            }
        }

        /// <summary>
        /// Loads the binding's settings from a XML encoded string.
        /// </summary>
        /// <param name="binding">The binding.</param>
        /// <param name="settings">The settings.</param>
        /// <exception cref="ArgumentException">Thrown if the settings are not valid.</exception>
        private void LoadSettings(NetMsmqBinding binding, string settings)
        {
            if (string.IsNullOrWhiteSpace(settings))
                return;

            throw new NotImplementedException();    // $todo(jeff.lill): Implement this
        }

        /// <summary>
        /// Loads the binding's settings from a XML encoded string.
        /// </summary>
        /// <param name="binding">The binding.</param>
        /// <param name="settings">The settings.</param>
        /// <exception cref="ArgumentException">Thrown if the settings are not valid.</exception>
        private void LoadSettings(NetPeerTcpBinding binding, string settings)
        {
            if (string.IsNullOrWhiteSpace(settings))
                return;

            throw new NotImplementedException();    // $todo(jeff.lill): Implement this
        }

        /// <summary>
        /// Loads the binding's settings from a XML encoded string.
        /// </summary>
        /// <param name="binding">The binding.</param>
        /// <param name="settings">The settings.</param>
        /// <exception cref="ArgumentException">Thrown if the settings are not valid.</exception>
        private void LoadSettings(System.ServiceModel.MsmqIntegration.MsmqIntegrationBinding binding, string settings)
        {
            if (string.IsNullOrWhiteSpace(settings))
                return;

            throw new NotImplementedException();    // $todo(jeff.lill): Implement this
        }
    }
}
