//-----------------------------------------------------------------------------
// FILE:        AzureHelper.cs
// CONTRIBUTOR: Jeff Lill
// COPYRIGHT:   Copyright (c) 2005-2015 by Jeffrey Lill.  All rights reserved.
// DESCRIPTION: Global Windows Azure utility methods.

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading;

using Microsoft.WindowsAzure.Diagnostics;
using Microsoft.WindowsAzure.ServiceRuntime;
using Microsoft.WindowsAzure.Storage;
using Microsoft.WindowsAzure.Storage.Blob;
using Microsoft.WindowsAzure.Storage.Queue;

using LillTek.Common;

namespace LillTek.Azure
{
    /// <summary>
    /// Global Windows Azure utility methods.
    /// </summary>
    /// <remarks>
    /// <para>
    /// This class is used by Azure roles and child processes launched by Azure roles to hold
    /// global state.  Azure roles should call <see cref="RoleInitialize"/> early in their 
    /// startup processing after performing basic LillTek initialization via a call to 
    /// <see cref="Helper.InitializeApp"/> or <b>WebHelper.PlatformInitialize()</b>.
    /// </para>
    /// </remarks>
    public static class AzureHelper
    {
        /// <summary>
        /// The maximum size in bytes possible for a binary message written to an Azure
        /// storage queue.
        /// </summary>
        /// <remarks>
        /// <para>
        /// Although Windows Azure claims to support messages up to 64K-1 bytes in size the actual
        /// limit is somewhat smaller, especially for binary messages.  Azure queues encode messages
        /// as XML within a root element that takes up several of the 65K bytes.  By default for text
        /// messages and always, for binary messages, the message payload is base-64 encoded.  The
        /// net result of this is that somewhat less than 65K bytes can actually be encoded in
        /// a queue message.
        /// </para>
        /// <para>
        /// This constant is set the actual maximum byte limit: <b>49152</b>.
        /// </para>
        /// </remarks>
        public const int MaxBinaryQueueMessage = 49152;

        /// <summary>
        /// The name of the role.
        /// </summary>
        public static string RoleName { get; set; }

        /// <summary>
        /// Identifies the type of the hosted role.
        /// </summary>
        public static AzureRoleType RoleType { get; private set; }

        /// <summary>
        /// The fully qualified path to the standard local folder for storing role data files.
        /// </summary>
        public static string RoleDataPath { get; private set; }

        /// <summary>
        /// Identifies the datacenter where the role instance is deployed.
        /// </summary>
        /// <remarks>
        /// <note>
        /// This value can also be obtained by querying the <b>AZURE_DATACENTER</b> environment variable
        /// within the current process or any child processes.
        /// </note>
        /// </remarks>
        public static string Datacenter { get; private set; }

        /// <summary>
        /// Identifies the Azure cloud service hosting the role.
        /// </summary>
        /// <remarks>
        /// <note>
        /// This value can also be obtained by querying the <b>AZURE_DEPLOYMENT</b> environment variable
        /// within the current process or any child processes.
        /// </note>
        /// </remarks>
        public static string Deployment { get; private set; }

        /// <summary>
        /// Identifies the role instance's deployment environment.
        /// </summary>
        /// <remarks>
        /// <note>
        /// This value can also be obtained by querying the <b>AZURE_ENVIRONMENT</b> environment variable
        /// within the current process or any child processes.
        /// </note>
        /// </remarks>
        public static AzureEnvironment Environment { get; private set; }

        /// <summary>
        /// Returns the currently deployed instance number of the current role or <b>-1</b>
        /// if this could not be determined.
        /// </summary>
        public static int RoleIndex { get; private set; }

        /// <summary>
        /// Returns a string that uniquely identifies the role instance as well as the datacenter
        /// and environment where the role is deployed.
        /// </summary>
        public static string RoleInstanceID { get; private set; }

        /// <summary>
        /// Called by Azure roles early in their boot process to intialize global state.  Note that
        /// this method may also be safely called by processes launched by an Azure role.
        /// </summary>
        /// <param name="roleName">The role or process name.</param>
        /// <param name="roleType">The type of Azure role being hosted.</param>
        /// <exception cref="AzureHelperException">Thrown if initialization failed.</exception>
        /// <remarks>
        /// <para>
        /// This method should be called after <see cref="Helper.InitializeApp"/> or <b>WebHelper.PlatformInitialize()</b> is 
        /// called to perform basic LillTek initalization.
        /// </para>
        /// <para>
        /// This method will set the <b>AZURE_ROLENAME</b>, <b>AZURE_ROLETYPE</b>, <b>AZURE_DATACENTER</b>, 
        /// <b>AZURE_DEPLOYMENT</b>, <b>AZURE_ENVIRONMENT</b>, <b>AZURE_ROLEINDEX</b>, <b>AZURE_ROLEINSTANCEID</b>,
        /// and <b>AZURE_ROLEDATAPATH</b> environment variables so that child processes can have 
        /// access to this information.
        /// </para>
        /// <para>
        /// Applications can also specify that other Azure configuration settings be persisted
        /// to the environment by specifying the names of these settings as a comma separated list
        /// in the <b>EnvironmentSettings</b> Azure configuration setting.
        /// </para>
        /// <para>
        /// This method also requires that the role define the <b>RoleData</b> local folder
        /// resource in its Azure configuration file.  The path to this folder can be obtained
        /// globally via <see cref="RoleDataPath"/>.
        /// </para>
        /// </remarks>
        public static void RoleInitialize(string roleName, AzureRoleType roleType)
        {
            LocalResource resource;

            // Handle non-Azure role initialization separately.

            if (roleType == AzureRoleType.Process)
            {
                ProcessInitialize(roleName);
                return;
            }

            // Initialize the role type and role data path globals.  Note that the data path is required.

            AzureHelper.RoleName     = roleName;
            AzureHelper.RoleType     = roleType;
            AzureHelper.RoleDataPath = null;

            try
            {
                resource = RoleEnvironment.GetLocalResource("RoleData");
            }
            catch
            {
                resource = null;
            }

            if (resource != null)
                AzureHelper.RoleDataPath = resource.RootPath;

            if (AzureHelper.RoleDataPath == null)
                throw new AzureHelperException("Could not obtain the [RoleData] local resource. This resource must be specified in the Windows Azure role configuration settings.");

            // This indicates to the Config class how it can retrieve Windows Azure configuration settings.

            Config.SetAzureGetSettingMethod(typeof(Microsoft.WindowsAzure.CloudConfigurationManager).GetMethod("GetSetting"));

            // $hack: 
            //
            // I'm going to parse the role deployment index out of the RoleInstance.Id property.
            // The code below should be robust enough not to crash if the format changes, and
            // will set RoleInstance=-1 in this case.
            //
            // The code below assumes that the ID property looks someything like:
            //
            //      "deployment17(273).MyService.MyRole_IN_0"
            //
            // where the role instance number is the integer at the end.

            AzureHelper.RoleIndex = -1;

            try
            {
                var fields = RoleEnvironment.CurrentRoleInstance.Id.Split('_');
                int v;

                if (!int.TryParse(fields.Last(), out v) || v < 0)
                    throw new FormatException("RoleIndex: Last role instance ID field is not a valid instance number.");

                AzureHelper.RoleIndex = v;
            }
            catch (Exception e)
            {
                SysLog.LogException(e, "Unable to extract the role index from the Azure role ID [{0}].", RoleEnvironment.CurrentRoleInstance.Id);
            }

            // Load Azure deployment related settings.

            AzureHelper.Datacenter     = Config.Global.Get("Azure.Datacenter", "UNKNOWN");
            AzureHelper.Deployment     = Config.Global.Get("Azure.Deployment", "UNKNOWN");
            AzureHelper.Environment    = Config.Global.Get<AzureEnvironment>("Azure.Environment", AzureEnvironment.Unknown);
            AzureHelper.RoleInstanceID = string.Format("{0}[{1}]", AzureHelper.RoleName, AzureHelper.RoleIndex);

            if (AzureHelper.Environment == AzureEnvironment.Dev)
            {
                var machineName = Helper.MachineName;

                if (!string.IsNullOrWhiteSpace(machineName))
                {
                    AzureHelper.RoleInstanceID = machineName + "." + AzureHelper.RoleInstanceID;
                }
            }

            // Persist Azure deployment related settings to environment variables so 
            // external child processes can pick these up.

            System.Environment.SetEnvironmentVariable("AZURE_ROLENAME", AzureHelper.RoleName);
            System.Environment.SetEnvironmentVariable("AZURE_ROLETYPE", AzureHelper.RoleType.ToString());
            System.Environment.SetEnvironmentVariable("AZURE_DATACENTER", AzureHelper.Datacenter);
            System.Environment.SetEnvironmentVariable("AZURE_DEPLOYMENT", AzureHelper.Deployment.ToUpper());
            System.Environment.SetEnvironmentVariable("AZURE_ENVIRONMENT", AzureHelper.Environment.ToString().ToUpper());
            System.Environment.SetEnvironmentVariable("AZURE_ROLEINSTANCEID", AzureHelper.RoleInstanceID);
            System.Environment.SetEnvironmentVariable("AZURE_ROLEINDEX", AzureHelper.RoleIndex.ToString());
            System.Environment.SetEnvironmentVariable("AZURE_ROLEDATAPATH", AzureHelper.RoleDataPath);

            // The [Azure.EnvironmentSettings] configuration setting can be used to specify 
            // the names of all the settings to be saved to environment variables to be accessable 
            // by processes launched by the role.  Handle this here.

            var environmentSettings = Config.Global.Get("Azure.EnvironmentSettings", string.Empty).Split(',');

            foreach (var envSetting in environmentSettings)
            {
                var name  = envSetting.Trim().ToUpper();
                var value = System.Environment.GetEnvironmentVariable(name);

                if (name == string.Empty || value == null)
                    continue;

                name = "AZURE " + name;

                if (System.Environment.GetEnvironmentVariable(name) != null)
                    continue;   // Don't overwrite a setting that was saved above.

                System.Environment.SetEnvironmentVariable(name, value);
            }

            // Load/reload the LillTek environment variables and configuration here.

            if (roleType == AzureRoleType.Web)
                Config.SetConfigPath(Path.Combine(Helper.AppFolder, "Web.ini"));
            else
                Config.SetConfigPath(Path.Combine(Helper.AppFolder, Path.GetFileNameWithoutExtension(Helper.EntryAssemblyFile) + ".ini"));

            EnvironmentVars.Reload();
            Config.Load();
        }

        /// <summary>
        /// Handles initialization for processes launched by Azure roles. 
        /// </summary>
        /// <param name="processName">Identifies the process.</param>
        private static void ProcessInitialize(string processName)
        {
            // Load the known Azure related settings from environment variables set 
            // by the parent Azure role.

            AzureHelper.RoleName       = GetVar("AZURE_ROLENAME") ?? string.Empty;
            AzureHelper.RoleType       = Serialize.Parse<AzureRoleType>(GetVar("AZURE_ROLETYPE"), AzureRoleType.Process);
            AzureHelper.Datacenter     = GetVar("AZURE_DATACENTER") ?? "EMULATOR";
            AzureHelper.Deployment     = GetVar("AZURE_DEPLOYMENT") ?? "<unknown>";
            AzureHelper.Environment    = Serialize.Parse<AzureEnvironment>(GetVar("AZURE_ENVIRONMENT"), AzureEnvironment.Dev);
            AzureHelper.RoleInstanceID = GetVar("AZURE_ROLEINSTANCEID") ?? string.Empty;
            AzureHelper.RoleIndex      = Serialize.Parse(GetVar("AZURE_ROLEINDEX"), -1);
            AzureHelper.RoleDataPath   = GetVar("AZURE_ROLEDATAPATH") ?? string.Empty;

            if (string.IsNullOrWhiteSpace(AzureHelper.RoleName))
                AzureHelper.RoleName = processName;

            // Make sure the role data path points somewhere real in case the
            // process is running outside the context of an Azure role 
            // (e.g. during development or test).

            if (string.IsNullOrWhiteSpace(AzureHelper.RoleDataPath) || !Directory.Exists(AzureHelper.RoleDataPath))
                AzureHelper.RoleDataPath = Path.GetTempPath();

            // Make sure that the known Azure settings are persisted to environment variables.
            // We need to do this to ensure that the configuration settings are loaded properly.

            System.Environment.SetEnvironmentVariable("AZURE_ROLENAME", AzureHelper.RoleName);
            System.Environment.SetEnvironmentVariable("AZURE_ROLETYPE", AzureHelper.RoleType.ToString());
            System.Environment.SetEnvironmentVariable("AZURE_DATACENTER", AzureHelper.Datacenter);
            System.Environment.SetEnvironmentVariable("AZURE_DEPLOYMENT", AzureHelper.Deployment);
            System.Environment.SetEnvironmentVariable("AZURE_ENVIRONMENT", AzureHelper.Environment.ToString().ToUpper());
            System.Environment.SetEnvironmentVariable("AZURE_ROLEINSTANCEID", AzureHelper.RoleInstanceID);
            System.Environment.SetEnvironmentVariable("AZURE_ROLEINDEX", AzureHelper.RoleIndex.ToString());
            System.Environment.SetEnvironmentVariable("AZURE_ROLEDATAPATH", AzureHelper.RoleDataPath);

            // This indicates to the Config class how it can retrieve Windows Azure configuration settings.

            Config.SetAzureGetSettingMethod(typeof(AzureHelper).GetMethod("GetProcessAzureSetting"));

            // We need to reload the environment and configuration settings so that they will be processed
            // in the context of the Azure related settings.

            if (Helper.EntryAssemblyFile != null)
            {
                Config.SetConfigPath(Path.Combine(Helper.AppFolder, Path.GetFileNameWithoutExtension(Helper.EntryAssemblyFile) + ".ini"));
                EnvironmentVars.Reload();
                Config.Load();
            }
        }

        /// <summary>
        /// <b>Internal use only:</b> Used by processes launched by Azure roles to retrieve the values the known 
        /// Azure configuration settings.
        /// </summary>
        /// <param name="name">The setting name.</param>
        /// <returns>The setting value or <c>null</c>.</returns>
        /// <remarks>
        /// <note>
        /// This method is intended for internal use only.  Applications should not call this method.
        /// </note>
        /// </remarks>
        public static string GetProcessAzureSetting(string name)
        {
            return System.Environment.GetEnvironmentVariable("azure_" + name);
        }

        /// <summary>
        /// Loads the requested environment variable, logging a warning if the variable is not set.
        /// </summary>
        /// <param name="name">The variable name.</param>
        /// <returns>The variable's value or the empty string.</returns>
        private static string GetVar(string name)
        {
            var value = System.Environment.GetEnvironmentVariable(name);

            if (string.IsNullOrWhiteSpace(value))
                SysLog.LogWarning("AzureHelper.Initialize: Environment variable [{0}] is not set.", name);

            return value;
        }

        /// <summary>
        /// Caching this value so we'll avoid catching multiple [TypeInitializationException] throws.
        /// </summary>
        private static AzureExecutionEnvironment executionEnvironment = AzureExecutionEnvironment.Unknown;

        /// <summary>
        /// Describes the Azure execution environment hosting the current application.
        /// </summary>
        public static AzureExecutionEnvironment ExecutionEnvironment
        {
            get
            {
                if (AzureHelper.executionEnvironment != AzureExecutionEnvironment.Unknown)
                    return AzureHelper.executionEnvironment;

                try
                {
                    if (!RoleEnvironment.IsAvailable)
                    {
                        AzureHelper.executionEnvironment = AzureExecutionEnvironment.Process;
                    }

                    if (RoleEnvironment.IsEmulated)
                        return AzureHelper.executionEnvironment = AzureExecutionEnvironment.Emulator;
                    else
                        return AzureHelper.executionEnvironment = AzureExecutionEnvironment.AzureVM;
                }
                catch (TypeInitializationException)
                {
                    // RoleEnvironment throws a type initialization exception if its static
                    // constructor is called when not running as an Azure role.  We'll use
                    // this as an indicator we're just a regular process.

                    return AzureHelper.executionEnvironment = AzureExecutionEnvironment.Process;
                }
            }
        }

        /// <summary>
        /// Converts a <c>uint</c> into base-64 encoded data suitable for commiting
        /// an Azure blob block.
        /// </summary>
        /// <param name="blockID">The block ID <c>uint</c>.</param>
        /// <returns>The Azure block ID.</returns>
        public static string GetBlobBlockID(uint blockID)
        {
            return Convert.ToBase64String(Encoding.UTF8.GetBytes(blockID.ToString("0#########")));
        }
    }
}
