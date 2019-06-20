//-----------------------------------------------------------------------------
// FILE:        CoreAppInstaller.cs
// CONTRIBUTOR: Jeff Lill
// COPYRIGHT:   Copyright (c) 2005-2015 by Jeffrey Lill. All rights reserved.
// DESCRIPTION: Handles the installation of the NeonSwitch Core service.

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
using LillTek.Telephony.Common;
using LillTek.Telephony.NeonSwitch;

namespace LillTek.Telephony.NeonSwitchCore
{
    /// <summary>
    /// Handles the installation of the NeonSwitch Core service.
    /// </summary>
    [RunInstaller(true)]
    public class CoreAppInstaller : System.Configuration.Install.Installer
    {
        private Container                   components = null;
        private ServiceInstaller            serviceInstaller;
        private ServiceProcessInstaller     serviceProcessInstaller;
        private InstallTools                installTools;
        private string                      installFolder;

        /// <summary>
        /// Constructs a CoreAppInstaller instance.
        /// </summary>
        public CoreAppInstaller()
        {
            InitializeComponent();
            Helper.InitializeApp(Assembly.GetExecutingAssembly());

            installFolder = Helper.GetAssemblyFolder(Assembly.GetExecutingAssembly());

            //-----------------------------------------------------------------
            // Add a LillTek InstallTools installer instance.

            installTools = new InstallTools(SwitchConst.NeonSwitchName + " Setup", installFolder, "NeonSwitchInstall.cmd");
            this.Installers.Add(installTools);

            //-----------------------------------------------------------------
            // Add the Windows Service Installer

            this.serviceInstaller                  = new ServiceInstaller();
            this.serviceProcessInstaller           = new ServiceProcessInstaller();

            this.serviceInstaller.DisplayName      = SwitchConst.NeonSwitchName;
            this.serviceInstaller.ServiceName      = SwitchConst.NeonSwitchName;
            this.serviceInstaller.StartType        = ServiceStartMode.Automatic;
            this.serviceInstaller.DelayedAutoStart = true;

            this.serviceProcessInstaller.Account   = ServiceAccount.LocalSystem;
            this.serviceProcessInstaller.Password  = null;
            this.serviceProcessInstaller.Username  = null;

            this.Installers.AddRange(new System.Configuration.Install.Installer[] {
                    this.serviceProcessInstaller,
                    this.serviceInstaller
                });

            //-----------------------------------------------------------------
            // Do some work that the service installer is too stupid to do.

            // Start the service after setup terminates.

            installTools.StartService(SwitchConst.NeonSwitchName);

            // Add the service description to the registry.

            installTools.AddRegValue(@"HKEY_LOCAL_MACHINE\SYSTEM\CurrentControlSet\Services\" + SwitchConst.NeonSwitchName + @":Description",
                                     "NeonSwitch Telephony Platform.");
        }

        /// <summary>
        /// Handles the performance counter installation if they don't
        /// already exist.
        /// </summary>
        /// <returns>The application's <see cref="PerfCounterSet" />.</returns>
        public static PerfCounterSet InstallPerfCounters()
        {
            bool            exists = PerformanceCounterCategory.Exists(SwitchConst.NeonSwitchPerf);
            PerfCounterSet  perfCounters;

            perfCounters = new PerfCounterSet(false, true, SwitchConst.NeonSwitchPerf, SwitchConst.NeonSwitchName);

            if (!exists)
            {
                CorePerf.Install(perfCounters);

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

            installTools.InstallIniFile("LillTek.Telephony.NeonSwitchCore.ini");
            InstallPerfCounters();
            NativeSysLogProvider.CreateLogs(SwitchConst.NeonSwitchName);
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
                PerformanceCounterCategory.Delete(SwitchConst.NeonSwitchPerf);
                NativeSysLogProvider.RemoveLogs(SwitchConst.NeonSwitchName);
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
