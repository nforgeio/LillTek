//-----------------------------------------------------------------------------
// FILE:        SdkInstaller.cs
// CONTRIBUTOR: Jeff Lill
// COPYRIGHT:   Copyright (c) 2005-2015 by Jeffrey Lill.  All rights reserved.
// DESCRIPTION: Supports the installation of the LillTek Platform SDK

using System;
using System.Text;
using System.Reflection;
using System.Collections;
using System.Diagnostics;
using System.ComponentModel;
using System.Configuration.Install;
using System.Windows.Forms;

using LillTek.Common;

namespace LillTek.Install
{
    /// <summary>
    /// Supports the installation of the LillTek Platform SDK (<b>not for application use</b>).
    /// </summary>
    /// <remarks>
    /// <para>
    /// This installer handles a few LillTek Platform SDK installation tasks including:
    /// </para>
    /// <list type="bullet">
    ///     <item>
    ///     Adding the <b>LILLTEK_SDK</b> and LILLTEK_SDKBIN environment variables.
    ///     </item>
    ///     <item>
    ///     Updating the registry so that Visual Studio will be able to display the LillTek 
    ///     assemblies in the <b>Add Reference...</b> dialog.
    ///     </item>
    ///     <item>
    ///     Display a message telling the user to restart the computer.
    ///     </item>
    /// </list>
    /// </remarks>
    [RunInstaller(true)]
    public partial class SdkInstaller : Installer
    {

        private string binFolder;
        private string sdkFolder;

        /// <summary>
        /// Constructor.
        /// </summary>
        public SdkInstaller()
        {
            InitializeComponent();

            binFolder = Helper.GetAssemblyFolder(Assembly.GetExecutingAssembly()).Replace('/', '\\');
            binFolder = binFolder.Substring(0, binFolder.Length - 1);
            sdkFolder = binFolder.Substring(0, binFolder.LastIndexOf('\\'));
        }

        /// <summary>
        /// Installs the registry key.
        /// </summary>
        /// <param name="state">The installer state.</param>
        public override void Install(IDictionary state)
        {
            base.Install(state);

            // Add the LILLTEK_SDK and LILLTEK_SDKBIN environment variables.

            using (RegKey key = RegKey.Create(@"HKEY_LOCAL_MACHINE\SYSTEM\CurrentControlSet\Control\Session Manager\Environment"))
            {
                key.Set("LILLTEK_SDK", sdkFolder);
            }

            using (RegKey key = RegKey.Create(@"HKEY_LOCAL_MACHINE\SYSTEM\CurrentControlSet\Control\Session Manager\Environment"))
            {
                key.Set("LILLTEK_BIN", binFolder);
            }

            // Add the path to the LillTek SDK Bin folder to the registry so that Visual Studio
            // and other tools will be able to find the assemblies.

            using (RegKey key = RegKey.Create(@"HKEY_LOCAL_MACHINE\Software\Microsoft\.NETFramework\AssemblyFolders\LillTek Platform"))
            {
                key.Set("", binFolder);
            }

            using (RegKey key = RegKey.Create(@"HKEY_LOCAL_MACHINE\Software\Microsoft\VisualStudio\8.0\AssemblyFolders\LillTek Platform"))
            {
                key.Set("", binFolder);
            }

            using (RegKey key = RegKey.Create(@"HKEY_LOCAL_MACHINE\Software\Microsoft\VisualStudio\8.0Exp\AssemblyFolders\LillTek Platform"))
            {
                key.Set("", binFolder);
            }

            // Display the restart message.

            MessageBox.Show("Please restart Windows after setup completes.", "LillTek Platform Setup", MessageBoxButtons.OK);
        }

        /// <summary>
        /// Restores the state to before the installation.
        /// </summary>
        /// <param name="state">The installer state.</param>
        private void Restore(IDictionary state)
        {
            try
            {
                RegKey.Delete(@"HKEY_LOCAL_MACHINE\SYSTEM\CurrentControlSet\Control\Session Manager\Environment:LILLTEK_SDK");
            }
            catch
            {
            }

            try
            {
                RegKey.Delete(@"HKEY_LOCAL_MACHINE\SYSTEM\CurrentControlSet\Control\Session Manager\Environment:LILLTEK_BIN");
            }
            catch
            {
            }

            try
            {
                RegKey.Delete(@"HKEY_LOCAL_MACHINE\Software\Microsoft\.NETFramework\AssemblyFolders\LillTek Platform");
            }
            catch
            {
            }

            try
            {
                RegKey.Delete(@"HKEY_LOCAL_MACHINE\Software\Microsoft\VisualStudio\8.0\AssemblyFolders\LillTek Platform");
            }
            catch
            {
            }

            try
            {
                RegKey.Delete(@"HKEY_LOCAL_MACHINE\Software\Microsoft\VisualStudio\8.0Exp\AssemblyFolders\LillTek Platform");
            }
            catch
            {
            }
        }

        /// <summary>
        /// Rolls back the registry key install.
        /// </summary>
        /// <param name="state">The installer state.</param>
        public override void Rollback(IDictionary state)
        {
            base.Rollback(state);
            Restore(state);
        }

        /// <summary>
        /// Uninstalls the registry key.
        /// </summary>
        /// <param name="state">The installer state.</param>
        public override void Uninstall(IDictionary state)
        {
            base.Uninstall(state);
            Restore(state);
        }
    }
}
