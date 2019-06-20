//-----------------------------------------------------------------------------
// FILE:        ConfigServiceProvider.cs
// CONTRIBUTOR: Jeff Lill
// COPYRIGHT:   Copyright (c) 2005-2015 by Jeffrey Lill.  All rights reserved.
// DESCRIPTION: A custom IConfigProvider implementation that uses LillTek.Messaging
//              to retrieve configuration settings from a Configuration Service
//              instance.

using System;
using System.IO;
using System.Net;

using LillTek.Common;
using LillTek.Messaging;
using LillTek.Datacenter.Msgs;

namespace LillTek.Datacenter
{
    /// <summary>
    /// A custom <see cref="IConfigProvider" /> implementation that uses LillTek.Messaging
    /// to retrieve configuration settings from a Configuration Service instance running
    /// somewhere on the network.
    /// </summary>
    /// <remarks>
    /// <para>
    /// The basic idea here is to provide a reasonably flexible mechanism for centralizing
    /// application configuration, where different configuration information can be
    /// returned based on the application's executable file name, the executable version,
    /// the specific server the application is running on and finally, the use to
    /// which the application is being put.
    /// </para>
    /// <para>
    /// The <see cref="GetConfig" /> method is implemented by each configuration
    /// provider to return the textual (.ini file) representation of configuration information
    /// as described in <see cref="Config" />.  The settings parameter specify the provider
    /// specific name/value parameters.  The cacheFile parameter specifies the name of the
    /// file where the provider can cache settings, and the remaining parameters are used to 
    /// describe the configuration to be returned.  Implementations of IConfigProvider are free to 
    /// ignore or interpret these parameters as they will.
    /// </para>
    /// </remarks>
    public sealed class ConfigServiceProvider : IConfigProvider
    {
        /// <summary>
        /// The message endpoint used to query for configuration information.
        /// </summary>
        public const string GetConfigEP = "abstract://LillTek/DataCenter/ConfigService";

        /// <summary>
        /// Returns configuration information formatted as an .ini file as
        /// specified by <see cref="Config" />.
        /// </summary>
        /// <param name="settings">Provider specific settings.</param>
        /// <param name="cacheFile">File path to use for cached settings.</param>
        /// <param name="machineName">The requesting machine's name.</param>
        /// <param name="exeFile">The unqualified name of the current process' executable file.</param>
        /// <param name="exeVersion">The version number of the current executable file.</param>
        /// <param name="usage">Used to indicate a non-default usage for this application instance.</param>
        /// <returns>The configuration file data (or <c>null</c>).</returns>
        /// <remarks>
        /// <para>
        /// The settings parameter must specify the <b>RouterEP</b> and <b>CloudEP</b> setting values, 
        /// (the rest are optional):
        /// </para>
        /// <code language="none">
        /// RouterEP  = &lt;Router Physical Endpoint&gt;
        /// CloudEP   = &lt;IP Endpoint&gt;
        /// ConfigEP  = &lt;logical or physical endpoint&gt; (optional)
        /// EnableP2P = 0 | 1 (optional)
        /// </code>
        /// <para>
        /// <b>RouterEP</b> specifies the unique physical endpoint to be assigned to the
        /// bootstrap router.  This must be unique across all routers so it's best to
        /// use the $(Guid) environment variable somewhere within the definition.
        /// </para>
        /// <para>
        /// <b>CloudEP</b> specifies the IP endpoint to be used by a bootstrap
        /// message router to discover the other routers on the network, and <b>ConfigEP</b>
        /// specifies the logical or physical messaging endpoint of the configuration service.
        /// The class uses this information to start a leaf bootstrap router which will
        /// be used to discover and communicate with the configuration service.
        /// </para>
        /// <para>
        /// The <b>EnableP2P</b> setting indicates whether the bootstrap router should be 
        /// peer-to-peer enabled.  This parameter is optional and defaults to "1".  This
        /// setting is present for scalability reasons.  P2P enabling the bootstrap router
        /// will cause a lot of network traffic in environments with a lot of servers since
        /// every other P2P enabled router will establish a connection to the bootstrap
        /// router to discover its logical routes.  This is a significant overhead for a
        /// router that exposes no routes.  <b>EnableP2P</b> should be set to "0" in production 
        /// environments with a lot of servers and with a hub server present to avoid this 
        /// problem.
        /// </para>
        /// <para>
        /// The cacheFile parameter specifies the name of the file where the provider
        /// may choose to cache configuration settings.  This implementation will save a copy
        /// of the last set of settings returned by a remote service in this file and
        /// then use these settings if the remote service is unavailable the next time
        /// the service is started.  This improves the operational robustness of a
        /// large network of servers.  Choosing to implement this behavior is completely
        /// up to the discretion of the provider.  Specify <b>(no-cache)</b> to disable
        /// caching behavior.
        /// </para>
        /// <para>
        /// Note that the method will return null if the requested configuration
        /// information could not be loaded for any reason.
        /// </para>
        /// <para>
        /// This class queries the configuration service via the following message
        /// endpoints:
        /// </para>
        /// <code language="none">
        /// abstract://LillTek/DataCenter/ConfigService
        /// </code>
        /// <para>
        /// This may be remapped to another logical endpoint via the message
        /// routers <b>MsgRouter.AbstractMap</b> configuation setting.
        /// </para>
        /// </remarks>
        public string GetConfig(ArgCollection settings, string cacheFile, string machineName, string exeFile, Version exeVersion, string usage)
        {
            LeafRouter      router = null;
            RouterSettings  rSettings;
            string          s;
            MsgEP           routerEP;
            IPEndPoint      cloudEP;
            MsgEP           configEP;
            bool            enableP2P;
            GetConfigAck    ack;
            string          configText = null;
            Exception       queryException = null;

            // Make sure that the assembly's message types have been
            // registered with the LillTek.Messaging subsystem.

            Global.RegisterMsgTypes();

            // Parse the settings

            s = settings["RouterEP"];
            if (s == null)
                throw new ArgumentException("[RouterEP] setting expected.", "settings");

            try
            {
                routerEP = MsgEP.Parse(s);
            }
            catch
            {
                throw new ArgumentException("[RouterEP] is invalid.", "settings");
            }

            if (!routerEP.IsPhysical)
                throw new ArgumentException("[RouterEP] must specify a physical endpoint.", "settings");

            s = settings["CloudEP"];
            if (s == null)
                throw new ArgumentException("[CloudEP] setting expected.", "settings");

            cloudEP = Serialize.Parse(s, new IPEndPoint(IPAddress.Any, 0));
            if (cloudEP == new IPEndPoint(IPAddress.Any, 0))
                throw new ArgumentException("[CloudEP] setting is invalid.", "settings");

            configEP = Serialize.Parse(settings["ConfigEP"], GetConfigEP);
            enableP2P = Serialize.Parse(settings["EnableP2P"], true);

            // Crank up a bootstrap leaf router and perform the query.

            try
            {
                router              = new LeafRouter();
                rSettings           = new RouterSettings(routerEP);
                rSettings.AppName   = rSettings.AppName + " (config boot)";
                rSettings.CloudEP   = cloudEP;
                rSettings.EnableP2P = enableP2P;

                router.Start(rSettings);

                ack = (GetConfigAck)router.Query(configEP, new GetConfigMsg(machineName, exeFile, exeVersion, usage));
                configText = ack.ConfigText;
            }
            catch (Exception e)
            {

                queryException = e;
                configText = null;
            }
            finally
            {
                if (router != null)
                    router.Stop();
            }

            if (configText != null)
            {
                // Cache the query result if requested

                if (!string.IsNullOrWhiteSpace(cacheFile) && String.Compare(cacheFile, "(no-cache)", true) != 0)
                {
                    StreamWriter writer;

                    try
                    {
                        writer = new StreamWriter(cacheFile);
                        writer.Write(configText);
                        writer.Close();
                    }
                    catch
                    {
                    }
                }
            }
            else
            {
                // Try loading a cached configuration

                if (string.IsNullOrWhiteSpace(cacheFile))
                {
                    SysLog.LogException(queryException, "GetConfig query failed with no cached settings.");
                }
                else
                {
                    StreamReader reader;

                    SysLog.LogWarning("GetConfig query failed: Loading cached settings.");

                    try
                    {
                        reader = new StreamReader(cacheFile);
                        configText = reader.ReadToEnd();
                        reader.Close();
                    }
                    catch
                    {
                        configText = null;
                    }
                }
            }

            return configText;
        }
    }
}
