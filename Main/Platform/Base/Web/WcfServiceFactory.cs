//-----------------------------------------------------------------------------
// FILE:        WcfServiceFactory.cs
// CONTRIBUTOR: Jeff Lill
// COPYRIGHT:   Copyright (c) 2005-2015 by Jeffrey Lill.  All rights reserved.
// DESCRIPTION: Implements a custom ServiceHost factory for use by WCF 
//              applications hosted by IIS.

using System;
using System.Text;
using System.Web;
using System.Diagnostics;
using System.Collections.Generic;
using System.Web.Hosting;
using System.ServiceModel;
using System.ServiceModel.Activation;

using LillTek.Common;

namespace LillTek.Web
{
    /// <summary>
    /// Implements a custom <see cref="ServiceHostFactory" /> for use by WCF 
    /// applications hosted by IIS.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Windows Communication Foundation does not function properly when hosted
    /// on IIS under more than one host name.  This situation is very common
    /// since many sites will configured to be addressed with addresses such
    /// as <b>http://mydomain.com</b> and <b>http://www.mydomain.com</b>.  
    /// WCF service activation will fail by default in this situation with an 
    /// error something like:
    /// </para>
    /// <para>
    /// <i>
    /// This collection already contains an address with scheme http.  There can 
    /// be at most one address per scheme in this collection.
    /// </i>
    /// </para>
    /// <para>
    /// The solution is to configure this class as the custom <see cref="ServiceHostFactory" /> 
    /// for each web service file that selects only one of the host names
    /// condigured for the IIS site and then use this when constructing the
    /// <see cref="ServiceHost" /> instance to be used to process the request
    /// and then set the <see cref="UriPrefix" /> property to the desired
    /// URI schema and host name in your IIS application's <b>Application_Start()</b> 
    /// method within the <b>Global.asax.cs</b> source file. 
    /// </para>
    /// <para>
    /// To use this class, you'll need to add a reference to the <b>LillTek.Web</b> 
    /// assembly to your IIS application and then edit the markup for each of your
    /// service (<b>.svc</b>) files, adding <b>Factory</b> parameter to the 
    /// <b>ServiceHost</b> processing directive, as shown below:
    /// </para>
    /// <code>
    /// &lt;%@ ServiceHost Language="C#" 
    ///                 Debug="true" 
    ///                 Service="MyNamespace.MyService" 
    ///                 Factory="LillTek.Web.WcfServiceFactory"
    ///                 CodeBehind="MyService.svc.cs" %&gt;
    /// </code>
    /// <note>
    /// You'll need to add this parameter to <b>all services</b> in your application.
    /// </note>
    /// <para>
    /// Then set the <see cref="UriPrefix" /> to the desired scheme and host name
    /// in your site initialization code.
    /// </para>
    /// <note>
    /// If <see cref="UriPrefix" /> is not set then this class will use the
    /// first available IIS base address as the prefix.
    /// </note>
    /// </remarks>
    public sealed class WcfServiceFactory : ServiceHostFactory
    {
        //---------------------------------------------------------------------
        // Static members

        /// <summary>
        /// Specifies the URI prefix to use when selecting the IIS base addresses
        /// to use when instantiating the <see cref="ServiceHost" />.
        /// </summary>
        /// <remarks>
        /// <para>
        /// This should be set to the desired URI scheme and host name within 
        /// the IIS application's <b>Application_Start()</b> method within the 
        /// <b>Global.asax.cs</b> source file.
        /// </para>
        /// </remarks>
        public static string UriPrefix { get; set; }

        //---------------------------------------------------------------------
        // Instance members

        /// <summary>
        /// Constructs the <see cref="ServiceHost" /> instance to be used to 
        /// process a received WCF request.
        /// </summary>
        /// <param name="serviceType">The service type.</param>
        /// <param name="baseAddresses">The base addresses for the service.</param>
        /// <returns>The <see cref="ServiceHost" /> instance.</returns>
        protected override ServiceHost CreateServiceHost(Type serviceType, Uri[] baseAddresses)
        {
            if (baseAddresses.Length == 0)
                throw new ArgumentException(string.Format("Cannot activate service [{0}] because there are no base addresses specified.", serviceType.FullName));

            if (UriPrefix == null)
                return new ServiceHost(serviceType, baseAddresses[0]);

            string prefix;

            prefix = UriPrefix.ToLowerInvariant();
            if (prefix.EndsWith("/"))
                prefix = prefix.Substring(0, prefix.Length - 1);

            foreach (var baseAddress in baseAddresses)
                if (baseAddress.ToString().ToLowerInvariant().StartsWith(prefix))
                    return new ServiceHost(serviceType, baseAddress);

            var sb = new StringBuilder();

            sb.AppendFormat("Cannot activate service [{0}] because the URI prefix requested [{1}] does not match any of the IIS base addresses:", serviceType.FullName, UriPrefix);
            foreach (var baseAddress in baseAddresses)
                sb.AppendFormat(" {0}", baseAddress);

            throw new ArgumentException(sb.ToString());
        }
    }
}
