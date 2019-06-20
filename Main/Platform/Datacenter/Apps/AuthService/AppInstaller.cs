//-----------------------------------------------------------------------------
// FILE:        AppInstaller.cs
// CONTRIBUTOR: Jeff Lill
// COPYRIGHT:   Copyright (c) 2005-2015 by Jeffrey Lill. All rights reserved.
// DESCRIPTION: Handles the installation of the Authentication service.

using System;
using System.Collections;
using System.ComponentModel;
using System.Configuration.Install;
using System.Diagnostics;
using System.IO;
using System.Reflection;
using System.ServiceProcess;
using System.Text;

using LillTek.Advanced;
using LillTek.Common;
using LillTek.Datacenter;
using LillTek.Datacenter.Server;
using LillTek.Install;
using LillTek.Service;

namespace LillTek.Datacenter.AuthService
{
    /// <summary>
    /// Handles the installation of the Authentication service.
    /// </summary>
    [RunInstaller(true)]
    public class AppInstaller : System.Configuration.Install.Installer
    {
        private Container                   components = null;
        private ServiceInstaller            serviceInstaller;
        private ServiceProcessInstaller     serviceProcessInstaller;
        private InstallTools                installTools;
        private string                      installFolder;

        /// <summary>
        /// Constructs an AppInstaller instance.
        /// </summary>
        public AppInstaller()
        {
            InitializeComponent();
            Helper.Init(Assembly.GetExecutingAssembly());

            installFolder = Helper.GetAssemblyFolder(Assembly.GetExecutingAssembly());

            //-----------------------------------------------------------------
            // Add a LillTek InstallTools installer instance.

            installTools = new InstallTools(Const.AuthServiceName + " Setup", installFolder, "AuthServiceInstall.cmd");
            this.Installers.Add(installTools);

            //-----------------------------------------------------------------
            // Add the Windows Service Installer

            this.serviceInstaller                  = new ServiceInstaller();
            this.serviceProcessInstaller           = new ServiceProcessInstaller();

            this.serviceInstaller.DisplayName      = Const.AuthServiceName;
            this.serviceInstaller.ServiceName      = Const.AuthServiceName;
            this.serviceInstaller.StartType        = ServiceStartMode.Automatic;
            this.serviceInstaller.DelayedAutoStart = true;

            this.serviceProcessInstaller.Account   = ServiceAccount.LocalSystem;
            this.serviceProcessInstaller.Password  = null;
            this.serviceProcessInstaller.Username  = null;

            this.Installers.AddRange(new System.Configuration.Install.Installer[] 
            {
                this.serviceProcessInstaller,
                this.serviceInstaller
            });

            //-----------------------------------------------------------------
            // Do some work that the service installer is too stupid to do.

            // Start the service after setup terminates.

            installTools.StartService(Const.AuthServiceName);

            // Add the service description to the registry.

            installTools.AddRegValue(@"HKEY_LOCAL_MACHINE\SYSTEM\CurrentControlSet\Services\" + Const.AuthServiceName + @":Description",
                                     "Implements an extensible authentication service.");
        }

        /// <summary>
        /// Handles the performance counter installation if they don't
        /// already exist.
        /// </summary>
        /// <returns>The application's <see cref="PerfCounterSet" />.</returns>
        public static PerfCounterSet InstallPerfCounters()
        {
            bool            exists = PerformanceCounterCategory.Exists(Const.AuthServicePerf);
            PerfCounterSet  perfCounters;

            perfCounters = new PerfCounterSet(false, true, Const.AuthServicePerf, Const.AuthServiceName);

            if (!exists)
            {
                AuthServiceHandler.InstallPerfCounters(perfCounters, null);
                AuthenticationEngine.InstallPerfCounters(perfCounters, null);

                perfCounters.Install();
            }

            return perfCounters;
        }

        /// <summary>
        /// Handle the installation activities.
        /// </summary>
        /// <param name="state">The install state.</param>
        public override void Install(IDictionary state)
        {
            base.Install(state);

            installTools.InstallIniFile("LillTek.Datacenter.AuthService.ini");
            InstallPerfCounters();
            NativeSysLogProvider.CreateLogs(Const.AuthServiceName);
        }

        /// <summary>
        /// Handle the rollback activities.
        /// </summary>
        /// <param name="state">The install state.</param>
        public override void Rollback(IDictionary state)
        {
            base.Rollback(state);
        }

        /// <summary>
        /// Handle the commit activities.
        /// </summary>
        /// <param name="state">The install state.</param>
        public override void Commit(IDictionary state)
        {
            base.Commit(state);
        }

        /// <summary>
        /// Handle the uninstall activities.
        /// </summary>
        /// <param name="state">The install state.</param>
        public override void Uninstall(IDictionary state)
        {
            try
            {
                PerformanceCounterCategory.Delete(Const.AuthServicePerf);
                NativeSysLogProvider.RemoveLogs(Const.AuthServiceName);
            }
            catch
            {
                // Ignore any errors
            }

            base.Uninstall(state);
        }

        /// <summary>
        /// Clean up any resources being used.
        /// </summary>
        protected override void Dispose(bool disposing)
        {
            if (disposing && components != null)
                components.Dispose();

            base.Dispose(disposing);
        }

        #region Component Designer generated code
        /// <summary>
        /// Required method for Designer support - do not modify
        /// the contents of this method with the code editor.
        /// </summary>
        private void InitializeComponent()
        {
            components = new System.ComponentModel.Container();
        }
        #endregion
    }
}
