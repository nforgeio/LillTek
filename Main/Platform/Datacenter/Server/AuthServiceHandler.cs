//-----------------------------------------------------------------------------
// FILE:        AuthServiceHandler.cs
// CONTRIBUTOR: Jeff Lill
// COPYRIGHT:   Copyright (c) 2005-2015 by Jeffrey Lill.  All rights reserved.
// DESCRIPTION: Implements the authentication service handler.

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Net;
using System.Reflection;
using System.Runtime.Serialization;
using System.ServiceModel;
using System.Text;
using System.Threading;

using LillTek.Advanced;
using LillTek.Common;
using LillTek.Cryptography;
using LillTek.Datacenter;
using LillTek.Datacenter.Msgs.AuthService;
using LillTek.Json;
using LillTek.Messaging;
using LillTek.Net.Http;
using LillTek.Net.Radius;
using LillTek.Net.Sockets;
using LillTek.Net.Wcf;

// $todo(jeff.lill): 
//
// There's currently no way for an new authentication service instance
// to discover the current set of locked accounts.  This is not a super
// high priority right now.

// $todo(jeff.lill): 
//
// Figure out how to implement the WarnUnencrypted setting
// for WCF calls.

namespace LillTek.Datacenter.Server
{
    /// <summary>
    /// The WCF authentication result.
    /// </summary>
    [DataContract(Namespace = "http://lilltek.com/platform/2007/03/10", Name = "AuthenticationResult")]
    public sealed class WcfAuthenticationResult
    {
        /// <summary>
        /// Indicates the result of the operation.
        /// </summary>
        [DataMember]
        public AuthenticationStatus Status;

        /// <summary>
        /// A human readable message describing what happened.
        /// </summary>
        [DataMember]
        public string Message;

        /// <summary>
        /// The maximum time the result of this operation should be cached.
        /// </summary>
        [DataMember]
        public TimeSpan MaxCacheTime;

        /// <summary>
        /// Constructor.
        /// </summary>
        public WcfAuthenticationResult()
        {
        }

        /// <summary>
        /// Constructor.
        /// </summary>
        /// <param name="result">The instance fields will be initialized from this parameter.</param>
        public WcfAuthenticationResult(AuthenticationResult result)
        {
            this.Status       = result.Status;
            this.Message      = result.Message;
            this.MaxCacheTime = result.MaxCacheTime;
        }
    }

    /// <summary>
    /// Defines the WCF interface exposed by <see cref="AuthServiceHandler" />.
    /// </summary>
    [ServiceContract(Namespace = "http://lilltek.com/platform/2007/03/10", Name = "IAuthServiceHandler")]
    public interface IWcfAuthServiceHandler
    {
        /// <summary>
        /// Attempts to authenticate the account credentials passed.
        /// </summary>
        /// <param name="realm">The authentication realm.</param>
        /// <param name="account">The account.</param>
        /// <param name="password">the password.</param>
        /// <returns>A <see cref="AuthenticationResult" /> instance describing the result of the attempt.</returns>
        [OperationContract(Name = "Authenticate")]
        WcfAuthenticationResult WcfAuthenticate(string realm, string account, string password);
    }

    /// <summary>
    /// Implements the LillTek Authentication Service Handler.
    /// </summary>
    /// <remarks>
    /// <para>
    /// This class implements the guts of the data center Authentication Service
    /// by processing authentication requests made via four protocols:
    /// </para>
    /// <list type="bullet">
    ///     <item>LillTek Messaging</item>
    ///     <item>RADIUS</item>
    ///     <item>HTTP/HTTPS</item>
    ///     <item>Windows Communication Foundation (WCF)</item>
    /// </list>
    /// <para>
    /// To use, simply create an instance and then call <see cref="Start" /> passing
    /// the application's message router instance.  Call <see cref="Stop" /> when 
    /// the application terminates.
    /// </para>
    /// <para><b><u>LillTek Message Protocol</u></b></para>
    /// <para>
    /// The handler will listen for messages directed to the abstract endpoints:
    /// </para>
    /// <code language="none">
    /// abstract://LillTek/DataCenter/Auth/Server
    /// abstract://LillTek/DataCenter/Auth/Client
    /// </code>
    /// <para>
    /// The endpoints can be remapped to another logical endpoint by adding an
    /// <b>AbstractMap</b> entry to the application router's <b>MsgRouter</b> 
    /// configuration section.  The first endpoint handles requests made to
    /// the authentication service.  The second endpoint handles cache control
    /// messages.
    /// </para>
    /// <para><b><u>RADIUS Protocol</u></b></para>
    /// <para>
    /// The <see cref="AuthServiceHandler" /> exposes a RADIUS server on one
    /// or more ports via the use of the <see cref="RadiusServer" /> class.
    /// This provides for authentication interoperability between a large
    /// number of standard service implementations and devices.
    /// </para>
    /// <para><b><u>Authentication via HTTP/HTTPS</u></b></para>
    /// <para>
    /// The <see cref="AuthServiceHandler" /> can be configured to provide
    /// authentication services via HTTP or HTTPS requests.  This is implemented
    /// using the .NET Framework <see cref="HttpListener" /> and the underlying
    /// Windows HTTP.SYS driver so the server can coexist with IIS 6.0 on the
    /// same Windows 2003 server.  Note that this does not work on Windows/XP.
    /// You'll need to stop the IIS service on Windows/XP to listen on
    /// ports 80 and 443.
    /// </para>
    /// <para>
    /// The <b>HttpEndpoint[#]</b> configuration settings determine the set of URIs
    /// the service listens on.  Authentication requests made to these endpoints
    /// may be either a GET request with the authentication credentials encoded
    /// as URI query parameters or as a POST request with the credentials 
    /// encoded as a JSON/UTF-8 object.  In either case, the response will be
    /// formatted as a JSON object.
    /// </para>
    /// <para>
    /// The HTTP/HTTPS endpoint specified in the configuration points to the
    /// folder for the authentication service and should end with a "/".  The
    /// authentication operation will be implemented by the <b>Auth.json</b>
    /// document within the directory.  So, to make an authentication query
    /// against an instances configured with <b>HttpEndpoint[0]=http://test/authservice/</b>
    /// you'd make your HTTP query against <b>http://test/authservice/Auth.json</b>.
    /// </para>
    /// <para>
    /// <b>HTTP GET Requests</b> accept three query parameters <b>realm</b>, <b>account</b>,
    /// and <b>password</b>.  The <b>realm</b> is used by the authentication service
    /// to map the request to the underlying authentication source.  The account
    /// and password are self-explainatory.  Note that the password is passed in
    /// the clear in this protocol.  This means HTTPS/SSL must be used for production
    /// configurations.  Unencrypted HTTP support is provided to simplify development
    /// and testing.  The realm and account parameters are case insensitive.  The
    /// password is case sensitive.  Here's an example GET url:
    /// </para>
    /// <code language="none">
    /// https://authserver:80/authservice/auth.json?realm=lilltek.com&amp;account=jeff&amp;password=mypassword
    /// </code>
    /// <para>
    /// The authentication service will respond with the JSON object.  Here's
    /// an example of a JSON authentication response (note that the content type
    /// will be <b>application/json</b>).
    /// </para>
    /// <code language="none">
    ///     {
    ///         "Status": "Authenticated",
    ///         "Message": "Authenticated",
    ///         "MaxCacheTime": 300
    ///     }
    /// </code>
    /// <para>
    /// where <b>Status</b> indicates the disposition the request, <b>Message</b>
    /// is a human readable (english) message, and <b>MaxCacheTime</b> is the
    /// maximum time in seconds the client should cache the result if it 
    /// implements caching.  <b>Status</b> may return as one of the following
    /// values:
    /// </para>
    /// <list type="table">  
    ///     <item>
    ///         <term>Authenticated</term>
    ///         <description>The credentials are authentic.</description>
    ///     </item>
    ///     <item>
    ///         <term>AccessDenied</term>
    ///         <description>Authentication was denied for an unspecified reason.</description>
    ///     </item>
    ///     <item>
    ///         <term>BadRealm</term>
    ///         <description>The realm specified does not exist.</description>
    ///     </item>
    ///     <item>
    ///         <term>BadAccount</term>
    ///         <description>The account specified does not exist.</description>
    ///     </item>
    ///     <item>
    ///         <term></term>
    ///         <description></description>
    ///     </item>
    ///     <item>
    ///         <term>BadPassword</term>
    ///         <description>The password is not valid.</description>
    ///     </item>
    ///     <item>
    ///         <term>AccountDisabled</term>
    ///         <description>The account is disabled.</description>
    ///     </item>
    ///     <item>
    ///         <term>AccountLocked</term>
    ///         <description>The account is temporarily locked due to excessive unsuccessful authentication attempts.</description>
    ///     </item>
    ///     <item>
    ///         <term>ServerError</term>
    ///         <description>
    ///         The server encountered an error and was not able to process
    ///         the authentication request.
    ///         </description>
    ///     </item>
    /// </list>
    /// <para>
    /// Note that many authentication sources hide the exact reason for
    /// an authentication failure and return only <b>AccessDenied</b>.
    /// </para>
    /// <para>
    /// Client applications that wish to cache authentication information 
    /// for performance must honor the <b>MaxCacheTime</b> value returned.
    /// This is the maximum time in seconds that the combination of the 
    /// credentials and authentication disposition can be cached by the
    /// client before a fresh authentication request must be made.
    /// </para>
    /// <para><b><u>HTTP POST Requests</u></b></para>
    /// <para>
    /// The authentication server also supports HTTP POST requests.  The 
    /// payload of the request must be formatted as a simple JSON object
    /// encoded as UTF-8.  The format for this object is:
    /// </para>
    /// <code language="none">
    /// {
    ///     "Realm": "lilltek.com",
    ///     "Account": "jeff",
    ///     "Password": "mypassword"
    /// }
    /// </code>
    /// <para>
    /// The server response will be the same JSON object descroibed above.
    /// </para>
    /// <para><b><u>Source Credential Broadcasts</u></b></para>
    /// <para>
    /// After a set of credentials are successfully authenticated against an
    /// authentication source, the authentication service instance will
    /// broadcast a <see cref="SourceCredentialsMsg" /> with the encrypted
    /// credentials to all other service instances in the cluster so that 
    /// they can add these credentials to their caches.
    /// </para>
    /// <para>
    /// This feature will reduce the load on the authentication sources 
    /// by avoiding having every authentication service instance having
    /// to perform separate queries and this will also improve application
    /// performance by reducing the latency for subsequent authentication
    /// requests.
    /// </para>
    /// <para><b><u>Authentication via Windows Communication Foundation</u></b></para>
    /// <para>
    /// The service exposes the <b>Authenticate()</b> method defined
    /// below via any of the build-in WCF bindings.  See the HTTP/JSON
    /// section above for a description of the object fields and codes.
    /// </para>
    /// <code language="cs">
    /// public enum AuthenticationStatus
    /// {
    ///     Authenticated,
    ///     AccessDenied,
    ///     BadRealm,
    ///     BadAccount,
    ///     BadPassword,
    ///     AccountDisabled,
    ///     AccountLocked,
    ///     BadRequest,
    ///     ServerError
    /// }
    ///
    /// public class AuthenticationResult 
    /// {
    ///     public AuthenticationStatus Status;
    ///     public string Message;
    ///     public TimeSpan MaxCacheTime;
    /// }
    /// 
    /// public AuthenticationResult Authenticate(string realm,string account,string password);
    /// </code>
    /// <para><b><u>Dynamic DNS Cluster Support</u></b></para>
    /// <para>
    /// The authentication service can be configured to enlist the services of
    /// a dynamic DNS service cluster to expose its JSON/WCF host endpoints to
    /// legacy applications, providing for load-balancing and fail-over.  You'll
    /// use the <b>DynDNS</b> subsection of the application configuration to
    /// enable this.
    /// </para>
    /// <para>
    /// The example below shows a configuration fragment that exposes a JSON
    /// HTTP endpoint using <b>auth.lilltek.com</b>.  To register this host
    /// and the current IP address with the dynamic DNS cluster, you'll need
    /// to add the <b>DynDNS</b> section, specifying host/IP mapping as
    /// well as the Dynamic DNS cluster base endpoint.
    /// </para>
    /// <code language="none">
    /// #section LillTek.Datacenter.AuthService
    /// 
    ///     HttpEndpoint[0] = http://auth.lilltek.com:80/AuthService/
    /// 
    ///     #section DynDNS
    /// 
    ///         Enabled = true
    ///         Host[0] = auth.lilltek.com,$(ip-address)
    /// 
    ///         #section Cluster
    /// 
    ///             ClusterBaseEP = abstract://LillTek/DataCenter/DynDNS
    /// 
    ///         #endsection
    /// 
    ///     #endsection
    /// 
    /// #endsection
    /// </code>
    /// <para><b><u>Cache Control Commands</u></b></para>
    /// <para>
    /// The Authentication service provides interfaces for non-LillTek Platform
    /// applications to clear cached authentications across the network.  These
    /// interfaces are exposed as JSON/HTTP and WCF endpoints.  LillTek Platform
    /// applications can get the same functiuonality via the <see cref="Authenticator" />
    /// class.
    /// </para>
    /// <para>
    /// The entry points exposes three commands:
    /// </para>
    /// <para>
    /// <b>cache-clear</b> clears all cached authentication information.
    /// </para>
    /// <para>
    /// <b>cache-remove-realm(realm)</b> removes all cached authentications for
    /// a specific realm.
    /// </para>
    /// <para>
    /// <b>cache-remove-account(realm,account)</b> removes any cached information
    /// for a specific account.
    /// </para>
    /// <para>
    /// The HTTP/HTTPS endpoint specified in the configuration points to the
    /// folder for the authentication service and should end with a "/".  The
    /// cache operations will be implemented by the <b>Cache.json</b>
    /// document within the directory.  So, to issue a cache query
    /// against an instances configured with <b>HttpEndpoint[0]=http://test/authservice/</b>
    /// you'd make your HTTP query against <b>http://test/authservice/Cache.json</b>.
    /// </para>
    /// <para>
    /// The request accepts parameters encoded in the URI query string.  The <b>command</b>
    /// parameter is required, and the <b>realm</b> and <b>account</b> parameters can be
    /// added as required.  Here's an example URI that commands all caches remove all
    /// accounts for the "lilltek.com" realm:
    /// </para>
    /// <para>
    /// <b>http://test/authservice/cache,json?command=cache-remove-realm&amp;realm=lilltek.com</b>
    /// </para>
    /// <para>
    /// The application should check the HTTP response status to verify that the operation
    /// succeeded.
    /// </para>
    /// <para><b><u>Configuration Settings</u></b></para>
    /// <para>
    /// By default, authentication service settings are prefixed by 
    /// <b>LillTek.Datacenter.AuthService</b> (a custom prefix can be
    /// passed to <see cref="Start" /> if desired).  Most of the application settings
    /// are related to an <see cref="AuthenticationEngine" /> instance created
    /// internally by the service.  See <see cref="AuthenticationEngineSettings" />
    /// for a description of these settings.  The remaining settings are described 
    /// in the table below:
    /// </para>
    /// <div class="tablediv">
    /// <table class="dtTABLE" cellspacing="0" ID="Table1">
    /// <tr valign="top">
    /// <th width="1">Setting</th>        
    /// <th width="1">Default</th>
    /// <th width="90%">Description</th>
    /// </tr>
    /// <tr valign="top">
    ///     <td>RealmMapProvider</td>
    ///     <td>(required)</td>
    ///     <td>
    ///     <para>
    ///     Specifies the <see cref="IRealmMapProvider" /> type to be instantiated
    ///     by the authentication service.  This instance will be called periodically
    ///     by the service to discover the realm/<see cref="IAuthenticationExtension" />
    ///     mappings.  This setting includes the fully qualified name of the type
    ///     as well as the path to the assembly formatted as described in
    ///     <see cref="Config.Get(string,System.Type)" />.
    ///     </para>
    ///     <para>
    ///     The <b>LillTek.Datacenter.Server.dll</b> assembly includes several built-in
    ///     <see cref="IRealmMapProvider" /> implementations including: <see cref="ConfigRealmMapProvider" />,
    ///     <see cref="FileRealmMapProvider" />, and <see cref="OdbcRealmMapProvider" />.
    ///     Custom realm map providers can also be implemented and configured for an
    ///     authentication service instance.
    ///     </para>
    ///     </td>
    /// </tr>
    /// <tr valign="top">
    ///     <td>RealmMapArgs</td>
    ///     <td>(required)</td>
    ///     <td>
    ///     This setting holds the realm map provider specific arguments.  See the 
    ///     provider implementation for the format and description of this
    ///     parameter.
    ///     </td>
    /// </tr>
    /// <tr valign="top">
    ///     <td>RSAKeyPair</td>
    ///     <td>(a new key is generated)</td>
    ///     <td>
    ///     <para>
    ///     This specifies the authentication service's private RSA key.  The 
    ///     key XML can be specified as the value of the
    ///     setting or the name of a secure key container can be specified for
    ///     better security.  See <see cref="AsymmetricCrypto" /> for a description
    ///     for how to format a secure key container.
    ///     </para>
    ///     <para>
    ///     Note that if multiple authentication service instances are configured
    ///     for load balancing and failover that the <b>same private key must be 
    ///     configured for all service instances</b>.
    ///     </para>
    ///     <para>
    ///     A new 1024-bit RSA private key will be generated if this setting is not 
    ///     present.
    ///     </para>
    ///     </td>
    /// </tr>
    /// <tr valign="top">
    ///     <td>CredentialBroadcast</td>
    ///     <td>yes</td>
    ///     <td>
    ///     Controls whether the service should broadcast encrypted authenticated
    ///     credentials to all other authentication service instances in the
    ///     cluster so they can add the credentials to their cache.
    ///     </td>
    /// </tr>
    /// <tr valign="top">
    ///     <td>WarnUnencrypted</td>
    ///     <td>yes</td>
    ///     <td>
    ///     <para>
    ///     Specifies that all requests made over an unencrypted channel will 
    ///     be logged as a warning.  This is set to true by default so that
    ///     misconfigured production servers will be noticed.  Test and development
    ///     may wish to set this to <b>no</b> when testing HTTP or SOAP Web
    ///     Services over unencrypted connections.  This should be set to
    ///     <b>yes</b> for production servers.
    ///     </para>
    ///     <para>
    ///     Note that at this time, unencrypted requests received WFC are
    ///     not detected by the service.  Only non-SSL HTTP requests will
    ///     be detected.
    ///     </para>
    ///     </td>
    /// </tr>
    /// <tr valign="top">
    ///     <td>HttpEndpoint[#]</td>
    ///     <td>(none)</td>
    ///     <td>
    ///     <para>
    ///     The array specifies one or more HTTP endpoints to be exposed by the
    ///     authentication service.  This endpoint is the fully qualified URI
    ///     of the root directory for the service, including the URI scheme,
    ///     host name, and port.  The endpoint should end with a "/" but one
    ///     will be appended if necessary.
    ///     </para>
    ///     <para>
    ///     Note that the <b>$(machinename)</b> macro can be useful for 
    ///     embedding the computer's WINS host name in these URIs.  The
    ///     "*" wildcard can also be used in place of the host name.  
    ///     This indicates that service will be exposed on the specified
    ///     port regardless of the host name specified in the request.
    ///     Here's an example:
    ///     </para>
    ///     <code lang="none">
    ///     HttpEndpoint[-] = http://$(machinename):80/AuthService/
    ///     HttpEndpoint[-] = https://*:443/AuthService/
    ///     </code>
    ///     <para>
    ///     This exposes the service on HTTP port 80 for requests made to
    ///     the computer's WINS host name as well as on HTTPS port 443
    ///     for all request host names.
    ///     </para>
    ///     </td>
    /// </tr>
    /// <tr valign="top">
    ///     <td>ServiceBehaviors</td>
    ///     <td>(none)</td>
    ///     <td>
    ///     Specifies the service behaviors as XML using the same format as implemented
    ///     for .NET configuration files.
    ///     </td>
    /// </tr>
    /// <tr valign="top">
    ///     <td>WcfEndpoint[#]</td>
    ///     <td>(none)</td>
    ///     <td>
    ///     <para>
    ///     Specifies zero or more Windows Communication Foundation (WCF)
    ///     endpoints.  Each endpoint specifies the WCF binding as well as
    ///     the endpoint URI.  See <see cref="WcfEndpoint" /> for a description
    ///     of the syntax.  The example below configures a <see cref="BasicHttpBinding" />
    ///     and a URI.
    ///     </para>
    ///     <code lang="none">
    ///     WcfEndpoint[0] = binding=BasicHTTP;uri=http://localhost:8080/WCF-AuthService/Auth.svc
    ///     </code>
    ///     </td>
    /// </tr>
    /// <tr valign="top">
    ///     <td>WsdlUri</td>
    ///     <td>(none)</td>
    ///     <td>
    ///     <para>
    ///     Specifies whether the service's WSDL service description metadata should
    ///     be exposed.  Set the HTTP or HTTPS URI where the WSDL document should
    ///     be located.  Note that to actually retrieve the WSDL from the server,
    ///     you'll need to add the <b>"?wsdl"</b> query string in the browser.
    ///     </para>
    ///     </td>
    /// </tr>
    /// <tr valign="top">
    ///     <td>Radius[#].*</td>
    ///     <td>(none)</td>
    ///     <td>
    ///     <para>
    ///     The authentication service can expose one or more RADIUS
    ///     server protocol network interfaces via an array of <b>Radius[#]</b>
    ///     configuration settings.  The RADIUS setting values are described at
    ///     <see cref="RadiusServerSettings.LoadConfig" />.  Here's
    ///     a sample configuration that will expose RADIUS on ports RADIUS (1812)
    ///     and AAA (1645) on all network interfaces.
    ///     </para>
    ///     <code lang="none">
    ///     #section LillTek.Datacenter.AuthService
    /// 
    ///         RealmMapProvider = ...
    ///         RealmMapArgs     = ...
    ///         RSAKeyPair       = ...
    /// 
    ///         #section Radius[0]
    /// 
    ///             NetworkBinding = ANY:RADIUS
    ///             DefaultSecret  = nas-password
    /// 
    ///         #endsection
    /// 
    ///         #section Radius[1]
    /// 
    ///             NetworkBinding = ANY:AAA
    ///             DefaultSecret  = nas-password
    /// 
    ///         #endsection
    /// 
    ///     #endsection
    ///     </code>
    ///     </td>
    /// </tr>
    /// <tr valign="top">
    ///     <td>DynDNS</td>
    ///     <td>(disabled)</td>
    ///     <td>
    ///     <para>
    ///     This subsection specifies the settings used to register service hosts 
    ///     with the dynamic DNS service.  Dynamic DNS support is disabled by
    ///     default.  See <see cref="DynDnsClientSettings" /> for more information.
    ///     </para>
    ///     </td>
    /// </tr>
    /// </table>
    /// </div>
    /// <para><b><u>Performance Counters</u></b></para>
    /// <para>
    /// The class can be configured to expose performance counters.  Call the
    /// static <see cref="InstallPerfCounters" /> method to add the class performance
    /// counters to a <see cref="PerfCounterSet" /> during application installation
    /// and then pass a set instance to the <see cref="Start" /> method.
    /// </para>
    /// <para>
    /// The class exposes the performance counters described for <see cref="AuthenticationEngine" />
    /// as well as the counters described below:
    /// </para>
    /// <div class="tablediv">
    /// <table class="dtTABLE" cellspacing="0" ID="Table1">
    /// <tr valign="top">
    /// <th width="1">Name</th>        
    /// <th width="1">Type</th>
    /// <th width="90%">Description</th>
    /// </tr>
    /// <tr valign="top">
    ///     <td>Runtime</td>
    ///     <td>Count</td>
    ///     <td>Elapsed service runtime in minutes.</td>
    /// </tr>
    /// <tr valign="top">
    ///     <td>Auths/sec (JSON)</td>
    ///     <td>Rate</td>
    ///     <td>Received HTTP/JSON authentication requests/sec.</td>
    /// </tr>
    /// <tr valign="top">
    ///     <td>Auths/sec (LillTek)</td>
    ///     <td>Rate</td>
    ///     <td>Received LillTek.Messaging authentication requests/sec.</td>
    /// </tr>
    /// <tr valign="top">
    ///     <td>Auths/sec (WCF)</td>
    ///     <td>Rate</td>
    ///     <td>Received Windows Communication Foundation authentication requests/sec.</td>
    /// </tr>
    /// </table>
    /// </div>
    /// </remarks>
    /// <threadsafety instance="true" />
    [ServiceBehavior(InstanceContextMode = InstanceContextMode.Single, ConcurrencyMode = ConcurrencyMode.Multiple, IncludeExceptionDetailInFaults = true)]
    public class AuthServiceHandler : IServiceHandler, IWcfAuthServiceHandler, ILockable
    {
        //---------------------------------------------------------------------
        // Private classes

        /// <summary>
        /// Holds the performance counters maintained by the service.
        /// </summary>
        private struct Perf
        {
            // Performance counter names

            const string JsonRequests_Name = "Auths/sec (JSON)";
            const string WcfRequests_Name  = "Auths/sec (WCF)";
            const string MsgRequests_Name  = "Auths/sec (LillTek)";
            const string Runtime_Name      = "Runtime (min)";

            /// <summary>
            /// Installs the service's performance counters by adding them to the
            /// performance counter set passed.
            /// </summary>
            /// <param name="perfCounters">The application's performance counter set (or <c>null</c>).</param>
            /// <param name="perfPrefix">The string to prefix any performance counter names (or <c>null</c>).</param>
            public static void Install(PerfCounterSet perfCounters, string perfPrefix)
            {
                if (perfCounters == null)
                    return;

                if (perfPrefix == null)
                    perfPrefix = string.Empty;

                perfCounters.Add(new PerfCounter(perfPrefix + JsonRequests_Name, "JSON Authentication requests received/sec", PerformanceCounterType.RateOfCountsPerSecond32));
                perfCounters.Add(new PerfCounter(perfPrefix + WcfRequests_Name, "Windows Communication Foundation Authentication requests received/sec", PerformanceCounterType.RateOfCountsPerSecond32));
                perfCounters.Add(new PerfCounter(perfPrefix + MsgRequests_Name, "LillTek Messaging Authentication requests received/sec", PerformanceCounterType.RateOfCountsPerSecond32));
                perfCounters.Add(new PerfCounter(perfPrefix + Runtime_Name, "Service runtime in minutes", PerformanceCounterType.NumberOfItems32));
            }

            //-----------------------------------------------------------------

            public PerfCounter JsonRequests;       // # JSON auths/sec
            public PerfCounter WcfRequests;        // # WCF auths/sec
            public PerfCounter MsgRequests;        // # LillTek Messaging auths/sec
            public PerfCounter Runtime;            // Service runtime in minutes

            /// <summary>
            /// Initializes the service's performance counters from the performance
            /// counter set passed.
            /// </summary>
            /// <param name="perfCounters">The application's performance counter set (or <c>null</c>).</param>
            /// <param name="perfPrefix">The string to prefix any performance counter names (or <c>null</c>).</param>
            public Perf(PerfCounterSet perfCounters, string perfPrefix)
            {
                Install(perfCounters, perfPrefix);

                if (perfPrefix == null)
                    perfPrefix = string.Empty;

                if (perfCounters != null)
                {

                    JsonRequests = perfCounters[perfPrefix + JsonRequests_Name];
                    WcfRequests  = perfCounters[perfPrefix + WcfRequests_Name];
                    MsgRequests  = perfCounters[perfPrefix + MsgRequests_Name];
                    Runtime      = perfCounters[perfPrefix + Runtime_Name];
                }
                else
                {

                    JsonRequests =
                    WcfRequests  =
                    MsgRequests  =
                    Runtime      = PerfCounter.Stub;
                }
            }
        }

        //---------------------------------------------------------------------
        // Static members

        /// <summary>
        /// Adds the performance counters managed by the class to the performance counter
        /// set passed (if not null).  This will be called during the application installation
        /// process when performance counters are being installed.
        /// </summary>
        /// <param name="perfCounters">The application's performance counter set (or <c>null</c>).</param>
        /// <param name="perfPrefix">The string to prefix any performance counter names (or <c>null</c>).</param>
        public static void InstallPerfCounters(PerfCounterSet perfCounters, string perfPrefix)
        {
            Perf.Install(perfCounters, perfPrefix);

            LdapAuthenticationExtension.InstallPerfCounters(perfCounters, perfPrefix);
            ConfigAuthenticationExtension.InstallPerfCounters(perfCounters, perfPrefix);
            OdbcAuthenticationExtension.InstallPerfCounters(perfCounters, perfPrefix);
            FileAuthenticationExtension.InstallPerfCounters(perfCounters, perfPrefix);
            RadiusAuthenticationExtension.InstallPerfCounters(perfCounters, perfPrefix);
        }

        //---------------------------------------------------------------------
        // Instance members

        /// <summary>
        /// The service's default configuration key prefix.
        /// </summary>
        public const string ConfigPrefix = "LillTek.Datacenter.AuthService";

        private MsgRouter               router;                 // The associated router (or null if the handler is stopped).
        private object                  syncLock;               // Instance used for thread synchronization
        private Guid                    instanceID;             // Used to distinguish this service instance from all others
        private DateTime                startTime;              // Time the service started (UTC)
        private IRealmMapProvider       realmMapper;            // Maps realms to IAuthenticationExtension instances.
        private AuthenticationEngine    engine;                 // The authentication engine
        private string                  rsaKeyPair;             // The service's RSA private key
        private string                  rsaPublicKey;           // The service's RSA public key
        private RadiusServer[]          radiusServers;          // The RADIUS server instances (one per server port)
        private EnhancedHttpListener    httpListener;           // The HTTP listener (or null)
        private bool                    isListenerStarted;      // True if the HTTP listen is running
        private AsyncCallback           onHttpRequest;          // Delegate called when an HTTP request is received
        private bool                    credentialBroadcast;    // True to broadcast authenticated credentials
        private bool                    warnUnencrypted;        // True if unencrypted requests should be logged
        private WcfServiceHost          wcfServiceHost;         // The WCF service host (or null)
        private DynDnsClient            dynDns;                 // The dynamic DNS client
        private GatedTimer              bkTimer;                // The background task timer
        private Perf                    perf;                   // Performance counters
        private int                     cAuth;                  // Number of AuthMsgs received
        private int                     cGetPublicKey;          // Number of GetPublicKey messages received

        /// <summary>
        /// Constructs a authentication service handler instance.
        /// </summary>
        public AuthServiceHandler()
        {
            this.router            = null;
            this.syncLock          = null;
            this.httpListener      = null;
            this.wcfServiceHost    = null;
            this.dynDns            = null;
            this.isListenerStarted = false;
            this.onHttpRequest     = new AsyncCallback(OnHttpRequest);
            this.bkTimer           = null;
        }

        /// <summary>
        /// Associates the service handler with a message router by registering
        /// the necessary application message handlers.
        /// </summary>
        /// <param name="router">The message router.</param>
        /// <param name="keyPrefix">The configuration key prefix or (null to use <b>LillTek.Datacenter.AuthService</b>).</param>
        /// <param name="perfCounters">The application's performance counter set (or <c>null</c>).</param>
        /// <param name="perfPrefix">The string to prefix any performance counter names (or <c>null</c>).</param>
        /// <remarks>
        /// <para>
        /// Applications that expose performance counters will pass a non-<c>null</c> <b>perfCounters</b>
        /// instance.  The service handler should add any counters it implements to this set.
        /// If <paramref name="perfPrefix" /> is not <c>null</c> then any counters added should prefix their
        /// names with this parameter.
        /// </para>
        /// </remarks>
        public void Start(MsgRouter router, string keyPrefix, PerfCounterSet perfCounters, string perfPrefix)
        {
            AuthenticationEngineSettings    settings;
            Config                          config;
            System.Type                     realmMapperType;
            string                          realmMapperArgs;
            string                          keyPair;

            this.syncLock      = router.SyncRoot;
            this.instanceID    = Helper.NewGuid();
            this.cAuth         = 0;
            this.cGetPublicKey = 0;

            // Make sure that the LillTek.Datacenter message types have been
            // registered with the LillTek.Messaging subsystem.

            LillTek.Datacenter.Global.RegisterMsgTypes();

            // Verify the router parameter

            if (router == null)
                throw new ArgumentNullException("router", "Router cannot be null.");

            if (this.router != null)
                throw new InvalidOperationException("This handler has already been started.");

            // Initialize the performance counters

            startTime = DateTime.UtcNow;
            perf      = new Perf(perfCounters, perfPrefix);

            // Crank up the background task timer.  I'm hardcoding this to 
            // be raised every 5 seconds since all we're using this for right now
            // is updating the runtime performance counter.

            bkTimer = new GatedTimer(new TimerCallback(OnBkTimer), null, TimeSpan.FromSeconds(5));

            // Crank up the realm map provider and the authentication engine.

            if (keyPrefix == null)
                keyPrefix = ConfigPrefix;

            config = new Config(keyPrefix);

            credentialBroadcast = config.Get("CredentialBroadcast", true);
            warnUnencrypted     = config.Get("WarnUnencrypted", true);
            keyPair             = config.Get("RSAKeyPair", (string)null);

            if (keyPair == null)
            {
                keyPair      = AsymmetricCrypto.CreatePrivateKey(CryptoAlgorithm.RSA, 1024);
                rsaKeyPair   = keyPair;
                rsaPublicKey = AsymmetricCrypto.GetPublicKey(CryptoAlgorithm.RSA, keyPair);
            }
            else
            {
                if (AsymmetricCrypto.IsKeyContainer(CryptoAlgorithm.RSA, keyPair))
                    keyPair = AsymmetricCrypto.LoadPrivateKey(CryptoAlgorithm.RSA, keyPair);

                rsaKeyPair   = keyPair;
                rsaPublicKey = AsymmetricCrypto.GetPublicKey(CryptoAlgorithm.RSA, keyPair);
            }

            settings = AuthenticationEngineSettings.LoadConfig(ConfigPrefix);

            realmMapperArgs = config.Get("RealmMapArgs", (string)null);
            realmMapperType = config.Get("RealmMapProvider", (System.Type)null);
            if (realmMapperType == null)
                throw new ArgumentException("[RealmMapProvider] configuration setting is required.");

            if (realmMapperType.GetInterface(typeof(IRealmMapProvider).FullName) == null)
                throw new ArgumentException(string.Format("Type [{0}] specified in the [RealmMapProvider] setting does not implement [IRealmMapProvider].", realmMapperType.FullName));

            realmMapper = Helper.CreateInstance<IRealmMapProvider>(realmMapperType);
            realmMapper.Open(settings, realmMapperArgs);

            engine = new AuthenticationEngine(this);
            engine.AuthenticatedAccountEvent += new AccountAuthenticatedDelegate(OnAuthenticatedAccountEvent);
            engine.AccountLockStatusEvent += new AccountLockStatusDelegate(OnAccountLockStatusEvent);

            engine.Start(realmMapper, settings, perfCounters, perfPrefix);

            try
            {
                // Register the message handlers.

                this.router = router;

                router.Dispatcher.AddTarget(this);

                // Initialize and start the RADIUS servers

                string[] radiusKeys;

                radiusKeys         = config.GetSectionKeyArray("Radius");
                this.radiusServers = new RadiusServer[radiusKeys.Length];

                for (int i = 0; i < radiusKeys.Length; i++)
                {
                    RadiusServer server;

                    radiusServers[i]          = server = new RadiusServer();
                    server.LogEvent          += new RadiusLogDelegate(OnRadiusLogEvent);
                    server.AuthenticateEvent += new RadiusAuthenticateDelegate(OnRadiusAuthenticateEvent);

                    try
                    {
                        server.Start(radiusKeys[i]);
                    }
                    catch (Exception eInner)
                    {
                        SysLog.LogException(eInner);
                        SysLog.LogWarning("Cannot start radius server: [{0}].", radiusKeys[i]);
                    }
                }

                // Initialize and start the HTTP listener.

                string[]    uris;

                uris = config.GetArray("HttpEndpoint");
                if (uris.Length > 0)
                {
                    httpListener = new EnhancedHttpListener(string.Format("{0}/{1}", Const.AuthServiceName, Helper.GetVersion(Assembly.GetExecutingAssembly())));
                    foreach (string uri in uris)
                    {
                        string u = uri;

                        if (!uri.EndsWith("/"))
                            u = uri + "/";

                        httpListener.Prefixes.Add(u);
                    }

                    try
                    {
                        httpListener.Start();
                        isListenerStarted = true;
                        httpListener.BeginGetContext(onHttpRequest, null);
                    }
                    catch (Exception eInner)
                    {
                        SysLog.LogException(eInner);
                        if (new OsVersion().Workstation)
                            SysLog.LogWarning("A HttpListener cannot be created for the HTTP endpoints, probably because another process has the network port open.  Windows/XP does not support sharing HTTP ports across processes.");
                    }
                }

                // Initialize a WCF service host if required.

                WcfEndpoint[]   endpoints;
                string          wsdlUriString;
                Uri             wsdlUri;

                endpoints = WcfEndpoint.LoadConfigArray(config, "WcfEndpoint");
                if (endpoints.Length > 0)
                {
                    wcfServiceHost = new WcfServiceHost(this);

                    wcfServiceHost.AddBehaviors(config.Get("ServiceBehaviors"));
                    wcfServiceHost.AddServiceEndpoint(typeof(IWcfAuthServiceHandler), endpoints);

                    wsdlUriString = config.Get("WsdlUri");
                    if (wsdlUriString != null)
                    {
                        try
                        {
                            wsdlUri = new Uri(wsdlUriString);
                            if (wsdlUri.Scheme == Uri.UriSchemeHttp)
                                wcfServiceHost.ExposeServiceDescription(wsdlUriString, null);
                            else if (wsdlUri.Scheme == Uri.UriSchemeHttps)
                                wcfServiceHost.ExposeServiceDescription(null, wsdlUriString);
                            else
                                throw new Exception("URI must have one of [http://] or [https://] scheme.");
                        }
                        catch (Exception e)
                        {
                            SysLog.LogWarning("Invalid [WsdlUri] setting: " + e.Message);
                        }
                    }

                    try
                    {
                        wcfServiceHost.Start();
                    }
                    catch (Exception eInner)
                    {
                        SysLog.LogException(eInner);
                        if (new OsVersion().Workstation)
                            SysLog.LogWarning("A HttpListener cannot be created for the WCF endpoints, probably because another process has the network port open.  Windows/XP does not support sharing HTTP ports across processes.");
                    }
                }

                // Start the dynamic DNS client

                dynDns = new DynDnsClient();
                dynDns.Open(router, new DynDnsClientSettings(config.KeyPrefix + "DynDNS"));
            }
            catch
            {
                if (bkTimer != null)
                {
                    bkTimer.Dispose();
                    bkTimer = null;
                }

                router.Dispatcher.RemoveTarget(this);

                if (dynDns != null)
                {
                    dynDns.Close();
                    dynDns = null;
                }

                if (radiusServers != null)
                {
                    for (int i = 0; i < radiusServers.Length; i++)
                        if (radiusServers[i] != null)
                            radiusServers[i].Stop();

                    radiusServers = null;
                }

                if (httpListener != null)
                {
                    if (isListenerStarted)
                        httpListener.Stop();

                    httpListener = null;
                }

                if (wcfServiceHost != null)
                {
                    wcfServiceHost.Stop();
                    wcfServiceHost = null;
                }

                throw;
            }
        }

        /// <summary>
        /// Called when the authentication engine successfully authenticates a set of
        /// credentials against an authentication source.
        /// </summary>
        /// <param name="realm">The authentication realm.</param>
        /// <param name="account">The account.</param>
        /// <param name="password">The password.</param>
        /// <param name="ttl">The time-to-live to use when caching these credentials.</param>
        private void OnAuthenticatedAccountEvent(string realm, string account, string password, TimeSpan ttl)
        {
            SourceCredentialsMsg msg;

            using (TimedLock.Lock(syncLock))
            {
                if (router == null || !credentialBroadcast)
                    return;

                msg = new SourceCredentialsMsg(instanceID, SourceCredentialsMsg.EncryptCredentials(rsaPublicKey, realm, account, password), ttl);
                router.BroadcastTo(Authenticator.AbstractAuthServerEP, msg);
            }
        }

        /// <summary>
        /// Called by the <see cref="AuthenticationEngine" /> when an account's lock status
        /// changes.  This method handles the broadcasting of this information to the
        /// other authentication service instances.
        /// </summary>
        /// <param name="realm">The authentication realm.</param>
        /// <param name="account">The locked account.</param>
        /// <param name="locked">Indicates whether the account has just been locked or unlocked.</param>
        /// <param name="lockTTL">The time a locked account should remain locked.</param>
        private void OnAccountLockStatusEvent(string realm, string account, bool locked, TimeSpan lockTTL)
        {
            AuthControlMsg msg;

            using (TimedLock.Lock(syncLock))
            {
                if (router == null)
                    return;

                if (locked)
                {
                    msg = new AuthControlMsg("lock-account", string.Format("realm={0};account={1};source-id={2};lock-ttl={3}",
                                                                          realm, account, instanceID, Serialize.ToString(lockTTL)));
                }
                else
                {
                    // Note that I'm passing lock-report=no so to avoid a situation where the receiving
                    // authentication service instances perform additional broadcasts reporting that
                    // the account has been unlocked.

                    msg = new AuthControlMsg("cache-remove-account", string.Format("realm={0};account={1};lock-report=no", realm, account));
                }

                router.BroadcastTo(Authenticator.AbstractAuthEP, msg);
            }
        }

        /// <summary>
        /// Performs the authentication for the RADIUS clients.
        /// </summary>
        /// <param name="realm">The authentication realm.</param>
        /// <param name="account">The account.</param>
        /// <param name="password">The password.</param>
        /// <returns><c>true</c> if the credentials are authentic.</returns>
        private bool OnRadiusAuthenticateEvent(string realm, string account, string password)
        {
            return engine.Authenticate(realm, account, password).Status == AuthenticationStatus.Authenticated;
        }

        /// <summary>
        /// Logs RADIUS related security events.
        /// </summary>
        /// <param name="logEntry">The RADIUS log information.</param>
        private void OnRadiusLogEvent(RadiusLogEntry logEntry)
        {
            engine.LogSecurityEvent(logEntry.Success, logEntry.Realm, logEntry.Account, logEntry.Message);
        }

        /// <summary>
        /// Initiates a graceful shut down of the service handler by ignoring
        /// new client requests.
        /// </summary>
        public void Shutdown()
        {
            Stop();
        }

        /// <summary>
        /// Immediately terminates the processing of all client messages.
        /// </summary>
        public void Stop()
        {
            if (router == null)
                return;

            using (TimedLock.Lock(router.SyncRoot))
            {
                if (bkTimer != null)
                {
                    bkTimer.Dispose();
                    bkTimer = null;
                }

                if (dynDns != null)
                {
                    dynDns.Close();
                    dynDns = null;
                }

                if (router != null)
                {
                    router.Dispatcher.RemoveTarget(this);
                    router = null;
                }

                if (radiusServers != null)
                {
                    foreach (RadiusServer server in radiusServers)
                    {
                        if (server != null)
                            server.Stop();
                    }

                    radiusServers = null;
                }

                if (httpListener != null)
                {
                    if (isListenerStarted)
                        httpListener.Stop();

                    httpListener = null;
                }

                if (wcfServiceHost != null)
                {
                    wcfServiceHost.Stop();
                    wcfServiceHost = null;
                }

                if (engine != null)
                {
                    engine.Stop();
                    engine = null;
                }

                if (realmMapper != null)
                {
                    realmMapper.Close();
                    realmMapper = null;
                }
            }
        }

        /// <summary>
        /// Handles background tasks.
        /// </summary>
        /// <param name="o">Not used.</param>
        private void OnBkTimer(object o)
        {
            perf.Runtime.RawValue = (int)(DateTime.UtcNow - startTime).TotalMinutes;
        }

        /// <summary>
        /// Returns the current number of client requests currently being processed.
        /// </summary>
        public int PendingCount
        {
            get { return 0; }
        }

        /// <summary>
        /// Broadcasts an <see cref="AuthControlMsg" /><b>(command=auth-failed)</b> message
        /// to the authentication service instances, passing the failed credentials.
        /// </summary>
        /// <param name="realm">The authentication realm.</param>
        /// <param name="account">The account.</param>
        /// <param name="status">The failed status.</param>
        private void BroadcastAuthFailure(string realm, string account, AuthenticationStatus status)
        {
            ArgCollection args;

            if (status == AuthenticationStatus.AccountLocked)
                return;     // We don't need to broadcast these

            args              = new ArgCollection();
            args["source-id"] = instanceID.ToString();
            args["realm"]     = realm;
            args["account"]   = account;
            args["status"]    = status.ToString();

            router.BroadcastTo(Authenticator.AbstractAuthServerEP, new AuthControlMsg("auth-failed", args));
        }

        /// <summary>
        /// Broadcasts an <see cref="AuthControlMsg" /> message to all authentication client
        /// and service instances.
        /// </summary>
        /// <param name="sourceID">The message source ID.</param>
        /// <param name="command">The command string.</param>
        /// <param name="realm">The authentication realm.</param>
        /// <param name="account">The account.</param>
        private void BroadcastCommand(Guid sourceID, string command, string realm, string account)
        {
            var args = new ArgCollection();

            if (sourceID != Guid.Empty)
                args["source-id"] = sourceID.ToString();

            if (realm != null)
                args["realm"] = realm;

            if (account != null)
                args["account"] = account;

            router.BroadcastTo(Authenticator.AbstractAuthEP, new AuthControlMsg(command, args));
        }

        /// <summary>
        /// Returns the internal <see cref="AuthenticationEngine" /> for use
        /// by unit tests.
        /// </summary>
        internal AuthenticationEngine Engine
        {
            get { return engine; }
        }

        /// <summary>
        /// Returns the number of received authentication messages by
        /// unit tests.
        /// </summary>
        internal int AuthCount
        {
            get { return cAuth; }
        }

        /// <summary>
        /// Returns the number of received public get request messages
        /// by unit tests.
        /// </summary>
        internal int GetPublicKeyCount
        {
            get { return cGetPublicKey; }
        }

        //---------------------------------------------------------------------
        // HTTP GET/POST Handler

        /// <summary>
        /// Used for serializing JSON authentication requests.
        /// </summary>
        private class JsonAuthRequest
        {
            public string   Realm;
            public string   Account;
            public string   Password;

            public JsonAuthRequest()
            {
            }

            public JsonAuthRequest(string realm, string account, string password)
            {
                this.Realm    = realm;
                this.Account  = account;
                this.Password = password;
            }
        }

        /// <summary>
        /// Used for serializing JSON cache commands.
        /// </summary>
        private class JsonCacheCommand
        {
            public string   Command;
            public string   Realm;
            public string   Account;

            public JsonCacheCommand()
            {
            }

            public JsonCacheCommand(string command, string realm, string account)
            {
                this.Command = command;
                this.Realm   = realm;
                this.Account = account;
            }
        }

        /// <summary>
        /// Used for serializing JSON authentication responses.
        /// </summary>
        private class JsonAuthResponse
        {
            public string   Status;
            public string   Message;
            public int      MaxCacheTime;

            public JsonAuthResponse()
            {
            }

            public JsonAuthResponse(AuthenticationStatus status, string message, TimeSpan maxCacheTime)
            {
                this.Status       = status.ToString();
                this.Message      = message;
                this.MaxCacheTime = (int)maxCacheTime.TotalSeconds;
            }

            public JsonAuthResponse(AuthenticationResult result)
            {
                this.Status       = result.Status.ToString();
                this.Message      = result.Message;
                this.MaxCacheTime = (int)result.MaxCacheTime.TotalSeconds;
            }
        }

        /// <summary>
        /// Delivers the JSON authentication response back to the <see cref="HttpListenerResponse" />'s
        /// client application.
        /// </summary>
        /// <param name="response">The <see cref="HttpListenerResponse" />.</param>
        /// <param name="authResponse">The authentication response.</param>
        private static void DeliverHttpResponse(HttpListenerResponse response, JsonAuthResponse authResponse)
        {
            byte[] content = Helper.ToUTF8(JsonSerializer.ToString(authResponse));

            response.ContentType     = "application/json";
            response.ContentLength64 = content.Length;

            response.OutputStream.Write(content, 0, content.Length);
            response.OutputStream.Close();
        }

        /// <summary>
        /// Delivers an HTTP error code and message to the client.
        /// </summary>
        /// <param name="response">The <see cref="HttpListenerResponse" />.</param>
        /// <param name="status">The <see cref="HttpStatus" /> code.</param>
        /// <param name="message">A human readable status message.</param>
        private static void DeliverHttpError(HttpListenerResponse response, HttpStatus status, string message)
        {
            byte[] content = Helper.ToUTF8(message);

            response.StatusCode        = (int)status;
            response.StatusDescription = Helper.StripCRLF(message);
            response.ContentType       = "text";
            response.ContentLength64   = content.Length;

            response.OutputStream.Write(content, 0, content.Length);
            response.OutputStream.Close();
        }

        /// <summary>
        /// Implements an HTTP/JSON authentication request.
        /// </summary>
        /// <param name="ctx">The HTTP request context.</param>
        /// <param name="request">The HTTP request.</param>
        private void OnHttpAuth(HttpListenerContext ctx, HttpListenerRequest request)
        {
            HttpListenerResponse    response;
            AuthenticationResult    result;

            // Process this request.  Note that I'm not going to worry about
            // looking at the request's content type, etc.  This should give me
            // compatibility with old and possibly buggy JSON libraries.

            perf.JsonRequests.Increment();

            request  = ctx.Request;
            response = ctx.Response;

            httpListener.AddResponseHeaders(response, HttpHeaderFlag.ApiTransient);

            try
            {
                if (!request.IsSecureConnection && warnUnencrypted)
                {
                    SysLog.LogWarning("Unencrypted HTTP authentication attempt from [{0}] on port [{1}].",
                                      request.RemoteEndPoint.Address, request.LocalEndPoint.Port);
                }

                switch (request.HttpMethod.ToUpper())
                {
                    case "GET":

                        string realm    = request.QueryString["realm"];
                        string account  = request.QueryString["account"];
                        string password = request.QueryString["password"];

                        if (realm == null)
                        {
                            DeliverHttpResponse(response, new JsonAuthResponse(AuthenticationStatus.ServerError, "[realm] parameter is required.", TimeSpan.Zero));
                            return;
                        }

                        if (account == null)
                        {
                            DeliverHttpResponse(response, new JsonAuthResponse(AuthenticationStatus.ServerError, "[account] parameter is required.", TimeSpan.Zero));
                            return;
                        }

                        if (password == null)
                        {
                            DeliverHttpResponse(response, new JsonAuthResponse(AuthenticationStatus.ServerError, "[password] parameter is required.", TimeSpan.Zero));
                            return;
                        }

                        result = engine.Authenticate(realm, account, password);
                        if (result.Status != AuthenticationStatus.Authenticated)
                            BroadcastAuthFailure(realm, account, result.Status);

                        DeliverHttpResponse(response, new JsonAuthResponse(result));
                        break;

                    case "POST":

                        Stream          input = null;
                        byte[]          buf = new byte[4096];
                        int             cb;
                        JsonAuthRequest authRequest;

                        // I'm going to process a maximum of 4K bytes of posted data.

                        try
                        {
                            input = request.InputStream;
                            if (input == null)
                            {
                                DeliverHttpResponse(response, new JsonAuthResponse(AuthenticationStatus.ServerError, "POST request must send content data.", TimeSpan.Zero));
                                return;
                            }

                            if (request.ContentLength64 > 4096)
                            {
                                DeliverHttpResponse(response, new JsonAuthResponse(AuthenticationStatus.ServerError, "POST content data exceeds 4K bytes.", TimeSpan.Zero));
                                return;
                            }

                            cb = input.Read(buf, 0, buf.Length);
                            input.Close();
                            input = null;

                            authRequest = (JsonAuthRequest)JsonSerializer.Read(Helper.FromUTF8(buf), typeof(JsonAuthRequest));

                            result = engine.Authenticate(authRequest.Realm, authRequest.Account, authRequest.Password);
                            if (result.Status != AuthenticationStatus.Authenticated)
                                BroadcastAuthFailure(authRequest.Realm, authRequest.Account, result.Status);

                            DeliverHttpResponse(response, new JsonAuthResponse(result));
                        }
                        finally
                        {
                            if (input != null)
                                input.Close();
                        }
                        break;

                    default:

                        DeliverHttpResponse(response, new JsonAuthResponse(AuthenticationStatus.ServerError, "HTTP method must be GET or POST.", TimeSpan.Zero));
                        break;
                }
            }
            catch (HttpListenerException e)
            {
                SysLog.LogException(e);
            }
            catch (Exception e)
            {
                SysLog.LogException(e);
                DeliverHttpResponse(response, new JsonAuthResponse(AuthenticationStatus.ServerError, e.Message, TimeSpan.Zero));
            }
        }

        /// <summary>
        /// Implements an HTTP/JSON command request.
        /// </summary>
        /// <param name="ctx">The HTTP request context.</param>
        /// <param name="request">The HTTP request.</param>
        private void OnHttpCacheCommand(HttpListenerContext ctx, HttpListenerRequest request)
        {
            HttpListenerResponse    response;
            string                  command;
            string                  realm;
            string                  account;

            // Process this request.  Note that I'm not going to worry about
            // looking at the request's content type, etc.  This should give me
            // compatibility with old and possibly buggy JSON libraries.

            request  = ctx.Request;
            response = ctx.Response;

            httpListener.AddResponseHeaders(response, HttpHeaderFlag.ApiTransient);

            try
            {
                if (!request.IsSecureConnection && warnUnencrypted)
                {
                    SysLog.LogWarning("Unencrypted HTTP authentication attempt from [{0}] on port [{1}].",
                                      request.RemoteEndPoint.Address, request.LocalEndPoint.Port);
                }

                switch (request.HttpMethod.ToUpper())
                {
                    case "GET":

                        command = request.QueryString["command"];
                        realm   = request.QueryString["realm"];
                        account = request.QueryString["account"];

                        break;

                    case "POST":

                        Stream              input = null;
                        byte[]              buf   = new byte[4096];
                        int                 cb;
                        JsonCacheCommand    cacheCmd;

                        // I'm going to process a maximum of 4K bytes of posted data.

                        try
                        {
                            input = request.InputStream;
                            if (input == null)
                            {
                                DeliverHttpError(response, HttpStatus.BadRequest, "POST request must send content data.");
                                return;
                            }

                            if (request.ContentLength64 > 4096)
                            {
                                DeliverHttpError(response, HttpStatus.BadRequest, "POST content data exceeds 4K bytes.");
                                return;
                            }

                            cb = input.Read(buf, 0, buf.Length);
                            input.Close();
                            input = null;

                            cacheCmd = (JsonCacheCommand)JsonSerializer.Read(Helper.FromUTF8(buf), typeof(JsonCacheCommand));

                            command = cacheCmd.Command;
                            realm   = cacheCmd.Realm;
                            account = cacheCmd.Account;
                        }
                        finally
                        {

                            if (input != null)
                                input.Close();
                        }
                        break;

                    default:

                        DeliverHttpError(response, HttpStatus.BadRequest, "HTTP method must be GET or POST.");
                        return;
                }

                // Note that I'm explicitly setting the "source-id" parameter in the
                // cache control messages to Guid.Empty so that this instance will
                // perform the command as well.

                switch (command.ToLowerInvariant())
                {
                    case "cache-clear":

                        BroadcastCommand(Guid.Empty, command, null, null);
                        break;

                    case "cache-remove-realm":

                        if (realm == null)
                        {
                            DeliverHttpError(response, HttpStatus.BadRequest, "[realm] parameter is required.");
                            return;
                        }

                        BroadcastCommand(Guid.Empty, command, realm, null);
                        break;

                    case "cache-remove-account":

                        if (realm == null)
                        {
                            DeliverHttpError(response, HttpStatus.BadRequest, "[realm] parameter is required.");
                            return;
                        }

                        if (account == null)
                        {
                            DeliverHttpError(response, HttpStatus.BadRequest, "[account] parameter is required.");
                            return;
                        }

                        BroadcastCommand(Guid.Empty, command, realm, account);
                        break;

                    default:

                        DeliverHttpError(response, HttpStatus.BadRequest, string.Format("Unexpected cache command [{0}].", command));
                        return;
                }
            }
            catch (HttpListenerException e)
            {
                SysLog.LogException(e);
            }
            catch (Exception e)
            {
                SysLog.LogException(e);
                DeliverHttpError(response, HttpStatus.InternalServerError, e.Message);
            }

            DeliverHttpError(response, HttpStatus.OK, "OK");
        }

        /// <summary>
        /// Handles received HTTP requests.
        /// </summary>
        /// <param name="ar">The operation's async result.</param>
        private void OnHttpRequest(IAsyncResult ar)
        {
            HttpListenerContext     ctx;
            HttpListenerRequest     request;
            HttpListenerResponse    response;
            string                  fileName;

            // Finish receiving the context

            try
            {
                if (httpListener == null)
                    return;

                ctx = httpListener.EndGetContext(ar);
            }
            catch (Exception e)
            {
                SysLog.LogException(e);
                return;
            }

            // Intiate an async receive of the next request before
            // we start processing this one.

            httpListener.BeginGetContext(onHttpRequest, null);

            // Process this request.  Note that I'm not going to worry about
            // looking at the request's content type, etc.  This should give me
            // compatibility with old and possibly buggy JSON libraries.

            perf.JsonRequests.Increment();

            request  = ctx.Request;
            response = ctx.Response;

            httpListener.AddResponseHeaders(response, HttpHeaderFlag.ApiTransient);

            try
            {
                fileName = Path.GetFileName(request.Url.AbsolutePath).ToUpper();
                switch (fileName)
                {
                    case "AUTH.JSON":

                        OnHttpAuth(ctx, request);
                        break;

                    case "CACHE.JSON":

                        OnHttpCacheCommand(ctx, request);
                        break;

                    default:

                        DeliverHttpError(response, HttpStatus.NotFound, "File not found");
                        break;
                }
            }
            catch (HttpListenerException e)
            {
                SysLog.LogException(e);
            }
            catch (Exception e)
            {
                SysLog.LogException(e);
                DeliverHttpError(response, HttpStatus.InternalServerError, e.Message);
            }
        }

        //---------------------------------------------------------------------
        // WCF Handlers

        /// <summary>
        /// Attempts to authenticate the account credentials passed.
        /// </summary>
        /// <param name="realm">The authentication realm.</param>
        /// <param name="account">The account.</param>
        /// <param name="password">the password.</param>
        /// <returns>A <see cref="AuthenticationResult" /> instance describing the result of the attempt.</returns>
        public WcfAuthenticationResult WcfAuthenticate(string realm, string account, string password)
        {
            AuthenticationResult result;

            try
            {
                perf.WcfRequests.Increment();

                result = engine.Authenticate(realm, account, password);
                if (result.Status != AuthenticationStatus.Authenticated)
                    BroadcastAuthFailure(realm, account, result.Status);

                return new WcfAuthenticationResult(result);
            }
            catch (Exception e)
            {
                SysLog.LogException(e);
                return new WcfAuthenticationResult(new AuthenticationResult(AuthenticationStatus.ServerError, TimeSpan.Zero));
            }
        }

        //---------------------------------------------------------------------
        // LillTek Message Handlers

        /// <summary>
        /// Returns the authentication service's public key to requesting clients.
        /// </summary>
        /// <param name="msg">The request message.</param>
        [MsgHandler(LogicalEP = Authenticator.AbstractAuthServerEP)]
        [MsgSession(Type = SessionTypeID.Query, Idempotent = false)]
        public void OnMsg(GetPublicKeyMsg msg)
        {
            try
            {
                using (TimedLock.Lock(syncLock))
                {
                    cGetPublicKey++;
                    router.ReplyTo(msg, new GetPublicKeyAck(rsaPublicKey, Helper.MachineName, NetHelper.GetActiveAdapter()));
                }
            }
            catch (Exception e)
            {
                SysLog.LogException(e);
            }
        }

        /// <summary>
        /// Verifies that the public key supposedly associated with the authentication
        /// service is actually valid.
        /// </summary>
        /// <param name="msg">The request message.</param>
        [MsgHandler(LogicalEP = Authenticator.AbstractAuthServerEP)]
        [MsgSession(Type = SessionTypeID.Query, Idempotent = false)]
        public void OnMsg(AuthServerIDMsg msg)
        {
            try
            {
                using (TimedLock.Lock(syncLock))
                {
                    if (router != null && !AsymmetricCrypto.KeyEquality(CryptoAlgorithm.RSA, msg.PublicKey, rsaPublicKey))
                    {
                        SysLog.LogSecurityFailure("Possible authentication security breach or misconfigured authentication service instance: [machine={0}] [address={1}].",
                                                  msg.MachineName, msg.Address);
                    }
                }
            }
            catch (Exception e)
            {
                SysLog.LogException(e);
            }
        }

        /// <summary>
        /// Performs an authentication.
        /// </summary>
        /// <param name="msg">The request message.</param>
        [MsgHandler(LogicalEP = Authenticator.AbstractAuthServerEP)]
        [MsgSession(Type = SessionTypeID.Query, Idempotent = false)]
        public void OnMsg(AuthMsg msg)
        {
            AuthenticationResult    result;
            SymmetricKey            symmetricKey;
            AuthAck                 ack;
            string                  realm;
            string                  account;
            string                  password;

            try
            {
                perf.MsgRequests.Increment();
                using (TimedLock.Lock(syncLock))
                {
                    if (router == null)
                        return;

                    cAuth++;
                    AuthMsg.DecryptCredentials(rsaKeyPair, msg.EncryptedCredentials, out realm, out account, out password, out symmetricKey);

                    result = engine.Authenticate(realm, account, password);
                    if (result.Status != AuthenticationStatus.Authenticated)
                        BroadcastAuthFailure(realm, account, result.Status);

                    ack = new AuthAck(AuthAck.EncryptResult(symmetricKey, result));

                    router.ReplyTo(msg, ack);
                }
            }
            catch (Exception e)
            {
                SysLog.LogException(e);
                throw;
            }
        }

        /// <summary>
        /// Handles the caching of credentials authenticated by other 
        /// service instances.
        /// </summary>
        /// <param name="msg">Holds the source credentials.</param>
        [MsgHandler(LogicalEP = Authenticator.AbstractAuthServerEP)]
        public void OnMsg(SourceCredentialsMsg msg)
        {
            string      realm;
            string      account;
            string      password;

            try
            {
                if (msg.SourceID == this.instanceID)
                    return;     // Ignore messages from self

                SourceCredentialsMsg.DecryptCredentials(rsaKeyPair, msg.EncryptedCredentials, out realm, out account, out password);
                engine.AddCredentials(realm, account, password, msg.TTL);
            }
            catch (Exception e)
            {
                SysLog.LogException(e);
            }
        }

        /// <summary>
        /// Implements authentication control messages against the 
        /// authentication server's cache.
        /// </summary>
        /// <param name="msg">The command message.</param>
        [MsgHandler(LogicalEP = Authenticator.AbstractAuthServerEP)]
        public void OnMsg(AuthControlMsg msg)
        {
            string      realm      = msg.Get("realm", null);
            string      account    = msg.Get("account", null);
            bool        lockReport = Serialize.Parse(msg.Get("lock-report", "yes"), true);
            TimedLock   timedLock   = new TimedLock();

            try
            {
                using (TimedLock.Lock(syncLock))
                {
                    if (router == null)
                        return;

                    switch (msg.Command)
                    {
                        case "auth-key-update":

                            break;      // Ignored by authentication services

                        case "load-realm-map":

                            engine.LoadRealmMap();
                            break;

                        case "cache-clear":

                            if (!lockReport)
                                timedLock = engine.SetLockReportEnable(timedLock, false);

                            engine.ClearCache();
                            engine.ClearNakCache();

                            if (!lockReport)
                                engine.SetLockReportEnable(timedLock, true);

                            break;

                        case "cache-remove-realm":

                            if (!lockReport)
                                timedLock = engine.SetLockReportEnable(timedLock, false);

                            engine.FlushCache(realm, null);
                            engine.FlushNakCache(realm, null);

                            if (!lockReport)
                                engine.SetLockReportEnable(timedLock, true);

                            break;

                        case "cache-remove-account":

                            if (!lockReport)
                                timedLock = engine.SetLockReportEnable(timedLock, false);

                            engine.FlushCache(realm, account);
                            engine.FlushNakCache(realm, account);

                            if (!lockReport)
                                engine.SetLockReportEnable(timedLock, true);

                            break;

                        case "auth-failed":

                            Guid sourceID = new Guid(msg.Get("source-id", Guid.Empty.ToString()));
                            AuthenticationStatus status = (AuthenticationStatus)Enum.Parse(typeof(AuthenticationStatus),
                                                                                                 msg.Get("status", AuthenticationStatus.AccessDenied.ToString()),
                                                                                                 true);
                            if (sourceID == this.instanceID)
                                break;      // Messages from self

                            if (status == AuthenticationStatus.Authenticated || status == AuthenticationStatus.AccountLocked)
                                break;      // Ignore these status codes

                            engine.IncrementFailCount(realm, account);
                            break;

                        case "lock-account":

                            TimeSpan lockTTL;

                            lockTTL = Serialize.Parse(msg.Get("lock-ttl", "5m"), TimeSpan.FromMinutes(5));
                            engine.LockAccount(realm, account, lockTTL);
                            break;

                        default:

                            SysLog.LogWarning("Unexpected authentication control command [{0}].", msg.Command);
                            break;
                    }
                }
            }
            catch (Exception e)
            {
                SysLog.LogException(e);
            }
        }

        //---------------------------------------------------------------------
        // ILockable implementation

        private object lockKey = TimedLock.AllocLockKey();

        /// <summary>
        /// Used by <see cref="TimedLock" /> to provide better deadlock
        /// diagnostic information.
        /// </summary>
        /// <returns>The process unique lock key for this instance.</returns>
        public object GetLockKey()
        {
            return lockKey;
        }
    }
}
