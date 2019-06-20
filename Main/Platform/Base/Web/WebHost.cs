//-----------------------------------------------------------------------------
// FILE:        WebHost.cs
// CONTRIBUTOR: Jeff Lill
// COPYRIGHT:   Copyright (c) 2005-2015 by Jeffrey Lill.  All rights reserved.
// DESCRIPTION: Hosts ASP.NET based applications within a process.

using System;
using System.IO;
using System.Text;
using System.Net;
using System.Web;
using System.Web.Hosting;
using System.Reflection;
using System.Diagnostics;
using System.Collections.Generic;
using System.Runtime.Remoting.Lifetime;

using LillTek.Common;
using LillTek.Service;

namespace LillTek.Web
{
    /// <summary>
    /// Hosts ASP.NET based applications within a process.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Sometimes it's desirable to host an ASP.NET application within
    /// a custom application process rather than via IIS.  Typical reasons
    /// for doing this include making application deployment easier by 
    /// having to deploy only a single application that doesn't require IIS,
    /// or running applications on platforms (like Windows Home) that don't
    /// include IIS.
    /// </para>
    /// <para>
    /// This class is pretty easy to use.  Simply instantiate an instance
    /// using <see cref="WebHost(string)" />, passing the configuration key
    /// specifying the location of the configuration parameters and the physical 
    /// path where the ASP.NET application is to be located.  The constructor
    /// starts ASP.NET application and begins processing requests.  Call 
    /// <see cref="Close" /> or <see cref="Dispose" /> to stop the ASP.NET
    /// application.  Alternatively, the <see cref="WebHost(string[],string,bool)" />
    /// constructor can be used to specified the hosting parameters explicitly.
    /// </para>
    /// <para>
    /// To operate correctly, the <see cref="WebHost" /> class needs to know the
    /// external URIs where HTTP requests will be submitted as well as the 
    /// physical folder where the ASP.NET application resides.  In addition to this,
    /// <see cref="WebHost" /> requires the <b>LillTek.Web</b> along with any 
    /// non-GAC assemblies it references exist within a <b>Bin</b> subfolder within
    /// the ASP.NET application folder.  The <b>copyBinAssemblies</b> parameter
    /// indicates whether the <see cref="WebHost" /> class should attempt to copy
    /// these assemblies or whether the class should assume that these assemblies
    /// have already been copied during application setup.
    /// </para>
    /// <para><b><u>Configuration Settings</u></b></para>
    /// <para>
    /// The <see cref="WebHost(string)" /> constructor override initializes the
    /// ASP.NET host using settings gathered from the application configuration.
    /// These settings are described below:
    /// </para>
    /// <div class="tablediv">
    /// <table class="dtTABLE" cellspacing="0" ID="Table1">
    /// <tr valign="top">
    /// <th width="1">Setting</th>        
    /// <th width="1">Default</th>
    /// <th width="90%">Description</th>
    /// </tr>
    /// <tr valign="top">
    ///     <td>URIs</td>
    ///     <td>(required)</td>
    ///     <td>
    ///     <para>
    ///     One or more external URIs specifying the endpoints to be exposed by
    ///     the ASP.NET application.  URIs must be separated with semicolon (;)
    ///     characters.  URIs may also include the <b>*</b> and <b>+</b> wildcard
    ///     characters as described in <see cref="HttpListener" />.
    ///     </para>
    ///     </td>
    /// </tr>
    /// <tr valign="top">
    ///     <td>PhysicalPath</td>
    ///     <td>(required)</td>
    ///     <td>
    ///     <para>
    ///     Path to the folder holding the ASP.NET application files.
    ///     </para>
    ///     </td>
    /// </tr>
    /// <tr valign="top">
    ///     <td>CopyBinAssemblies</td>
    ///     <td>yes</td>
    ///     <td>
    ///     <para>
    ///     Specifies whether or not the <see cref="WebHost" /> instance should
    ///     copy the <see cref="LillTek.Web" /> and any non-GAC referenced assemblies
    ///     to a <b>Bin</b> folder immediately within the ASP.NET application's
    ///     <b>PhysicalPath</b>.
    ///     </para>
    ///     </td>
    /// </tr>
    /// </table>
    /// </div>
    /// </remarks>
    /// <threadsafety instance="true" />
    public class WebHost : IDisposable
    {
        private object              syncLock = new object();
        private HttpListenerWrapper listener = null;
        private ClientSponsor       sponser;

        /// <summary>
        /// Starts an in-process hosted ASP.NET application using settings loaded
        /// from the application configuration.
        /// </summary>
        /// <param name="keyPrefix">The application configuration key.</param>
        public WebHost(string keyPrefix)
        {
            Config      config = new Config(keyPrefix);
            string      s;
            string[]    uris;
            string      physicalPath;
            bool        copyBinAssemblies;

            s = config.Get("URIs");
            if (s == null)
                throw new ArgumentException("[URIs] configuration setting is required.");

            uris = Helper.ParseStringList(s, ';');
            if (uris.Length == 0)
                throw new ArgumentException("[URIs] configuration setting must specify at least one valid URI.");

            physicalPath = config.Get("PhysicalPath");
            if (physicalPath == null)
                throw new ArgumentException("[PhysicalPath] configuration setting is required.");

            physicalPath = Helper.StripTrailingSlash(physicalPath);
            if (!Directory.Exists(physicalPath))
                throw new ArgumentException(string.Format("[PhysicalPath] configuration setting references the [{0}] folder which does not exist.", physicalPath));

            copyBinAssemblies = config.Get("CopyBinAssemblies", true);

            Start(uris, physicalPath, copyBinAssemblies);
        }

        /// <summary>
        /// Starts an in-process hosted ASP.NET application using explicit settings
        /// passed as parameters.
        /// </summary>
        /// <param name="uris">One or more external URIs specifying the ASP.NET application endpoints.</param>
        /// <param name="physicalPath">Path to the folder holding the ASP.NET application files.</param>
        /// <param name="copyBinAssemblies">
        /// Specifies whether or not the <see cref="WebHost" /> instance should
        /// copy the <see cref="LillTek.Web" /> and any non-GAC referenced assemblies
        /// to a <b>Bin</b> folder immediately within the ASP.NET application's
        /// <paramref name="physicalPath" />.
        /// </param>
        public WebHost(string[] uris, string physicalPath, bool copyBinAssemblies)
        {
            if (uris.Length == 0)
                throw new ArgumentException("At least one URI must be passed.", "uris");

            physicalPath = Helper.StripTrailingSlash(physicalPath);
            if (!Directory.Exists(physicalPath))
                throw new ArgumentException(string.Format("[PhysicalPath] configuration setting references the [{0}] folder which does not exist.", physicalPath), "physicalPath");

            Start(uris, physicalPath, copyBinAssemblies);
        }

        /// <summary>
        /// Replaces "*" and "+" wildcards in the URI string with the "A" character.
        /// </summary>
        /// <param name="uri">The URI to be converted.</param>
        /// <returns>The converted URI.</returns>
        private string ConvertHostWildcards(string uri)
        {
            return uri.Replace('*', 'A').Replace('+', 'A');
        }

        /// <summary>
        /// Starts an in-process hosted ASP.NET application using explicit settings
        /// passed as parameters.
        /// </summary>
        /// <param name="uris">One or more external URIs specifying the ASP.NET application endpoints.</param>
        /// <param name="physicalPath">Path to the folder holding the ASP.NET application files.</param>
        /// <param name="copyBinAssemblies">
        /// Specifies whether or not the <see cref="WebHost" /> instance should
        /// copy the <see cref="LillTek.Web" /> and any non-GAC referenced assemblies
        /// to a <b>Bin</b> folder immediately within the ASP.NET application's
        /// <paramref name="physicalPath" />.
        /// </param>
        private void Start(string[] uris, string physicalPath, bool copyBinAssemblies)
        {
            string virtualPath;

            // Make sure that all URIs passed end with "/".

            for (int i = 0; i < uris.Length; i++)
                if (!uris[i].EndsWith("/"))
                    uris[i] += "/";

            // Verify that the virtual path for all of the URIs are the same.
            // 
            // Note that I'm replacing any embedded "*" or "+" HttpListener wildcards
            // in the URI string with the "a" character so the Uri() constructor won't
            // throw exceptions. 

            virtualPath = new Uri(ConvertHostWildcards(uris[0])).AbsolutePath;
            for (int i = 1; i < uris.Length; i++)
                if (virtualPath != new Uri(ConvertHostWildcards(uris[i])).AbsolutePath)
                    throw new ArgumentException("The virtual path for all URIs must be identical.");

            // Make sure the physical path ends with a slash.

            physicalPath = Helper.AddTrailingSlash(physicalPath);

            // Handle the assembly copying if requested.

            if (copyBinAssemblies)
            {
                var assemblies  = new List<Assembly>();
                var curAssembly = Assembly.GetExecutingAssembly();

                // $todo(jeff.lill): 
                //
                // If I was going to be really tricky here, I'd look
                // recursively for referenced assemblies as well.
                // This isn't going to be an issue right at the
                // moment so I'm not going to worry about this.

                assemblies.Add(curAssembly);
                foreach (AssemblyName assemblyName in curAssembly.GetReferencedAssemblies())
                {
                    var reference = Assembly.Load(assemblyName);

                    if (reference.GlobalAssemblyCache)
                        continue;

                    assemblies.Add(reference);
                }

                if (!Directory.Exists(physicalPath + Helper.PathSepString + "Bin"))
                    Directory.CreateDirectory(physicalPath + Helper.PathSepString + "Bin");

                foreach (Assembly assembly in assemblies)
                {
                    var srcPath = Helper.GetAssemblyPath(assembly);
                    var dstPath = physicalPath + "Bin" + Helper.PathSepString + Path.GetFileName(srcPath);

                    if (File.Exists(dstPath))
                        File.Delete(dstPath);

                    File.Copy(srcPath, dstPath);
                }
            }

            // Crank up a HttpListenerWrapper instance in its own AppDomain.

            physicalPath = physicalPath.Replace('/', Helper.PathSepChar);
            listener     = (HttpListenerWrapper)ApplicationHost.CreateApplicationHost(typeof(HttpListenerWrapper),
                                                                                      virtualPath,
                                                                                      physicalPath);
            listener.Start(uris, virtualPath, physicalPath);

            // Configure the client side sponser to manage 
            // lifetime lease renewal requests from the server's
            // application domain.

            ILease lease;

            sponser = new ClientSponsor(TimeSpan.FromMinutes(1));
            lease = (ILease)listener.GetLifetimeService();
            lease.Register(sponser);
        }

        /// <summary>
        /// Stops the hosted ASP.NET application if one is running.
        /// </summary>
        public void Close()
        {
            lock (syncLock)
            {
                if (listener != null)
                {
                    try
                    {
                        var lease = (ILease)listener.GetLifetimeService();

                        lease.Unregister(sponser);
                        sponser = null;

                        listener.Stop();
                        listener = null;
                    }
                    catch (AppDomainUnloadedException)
                    {
                        // Ignore these
                    }
                }
            }
        }

        /// <summary>
        /// Stops the hosted ASP.NET application if one is running.
        /// </summary>
        public void Dispose()
        {
            Close();
        }
    }
}
