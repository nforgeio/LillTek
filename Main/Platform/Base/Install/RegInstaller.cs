//-----------------------------------------------------------------------------
// FILE:        RegInstaller.cs
// CONTRIBUTOR: Jeff Lill
// COPYRIGHT:   Copyright (c) 2005-2015 by Jeffrey Lill.  All rights reserved.
// DESCRIPTION: Implements the installation of registry values.

using System;
using System.Text;
using System.Reflection;
using System.Collections;
using System.Diagnostics;
using System.Configuration.Install;

using LillTek.Common;
using LillTek.Windows;

namespace LillTek.Install
{
    /// <summary>
    /// Implements the installation of registry values.
    /// </summary>
    internal sealed class RegInstaller : Installer
    {
        private string      keyPath;        // The value path.
        private int         intValue;
        private string      strValue;

        /// <summary>
        /// Initializes an installer that adds REG_DWORD value to the registry.
        /// </summary>
        /// <param name="keyPath">The registry path.</param>
        /// <param name="value">The value.</param>
        public RegInstaller(string keyPath, int value)
        {
            this.keyPath  = keyPath;
            this.intValue = value;
            this.strValue = null;
        }

        /// <summary>
        /// Initializes an installer that adds REG_SZ value to the registry.
        /// </summary>
        /// <param name="keyPath">The registry path.</param>
        /// <param name="value">The value.</param>
        public RegInstaller(string keyPath, string value)
        {
            this.keyPath  = keyPath;
            this.intValue = 0;
            this.strValue = value;
        }

        /// <summary>
        /// Installs the registry key.
        /// </summary>
        /// <param name="state">The installer state.</param>
        public override void Install(IDictionary state)
        {
            object orgValue;

            base.Install(state);

            switch (RegKey.GetValueType(keyPath))
            {
                case WinApi.REG_NONE:

                    orgValue = null;
                    break;

                case WinApi.REG_DWORD:

                    orgValue = RegKey.GetValue(keyPath, 0);
                    break;

                case WinApi.REG_SZ:

                    orgValue = RegKey.GetValue(keyPath, string.Empty);
                    break;

                default:

                    throw new InvalidOperationException("RegInstaller works only for REG_DWORD and REG_SZ registry values.");
            }

            state[InstallTools.GetStateKey(this, "KeyPath")]  = keyPath;
            state[InstallTools.GetStateKey(this, "OrgValue")] = orgValue;

            if (strValue != null)
                RegKey.SetValue(keyPath, strValue);
            else
                RegKey.SetValue(keyPath, intValue);
        }

        /// <summary>
        /// Restores the state to before the installation.
        /// </summary>
        /// <param name="state">The installer state.</param>
        private void Restore(IDictionary state)
        {
            try
            {
                string keyPath  = (string)state[InstallTools.GetStateKey(this, "KeyPath")];
                object orgValue = state[InstallTools.GetStateKey(this, "OrgValue")];

                if (orgValue == null)
                    RegKey.Delete(keyPath);
                else if (orgValue is int)
                    RegKey.SetValue(keyPath, (int)orgValue);
                else if (orgValue is string)
                    RegKey.GetValue(keyPath, (string)orgValue);
                else
                    throw new InvalidOperationException("RegInstaller works only for REG_DWORD and REG_SZ registry values.");
            }
            catch
            {
                // I'm going to ignore errors here since it'll often be the case
                // that the parent key has already been removed.
            }
        }

        /// <summary>
        /// Rolls back the registry key install.
        /// </summary>
        /// <param name="state">The installer state.</param>
        public override void Rollback(IDictionary state)
        {
            Restore(state);
            base.Rollback(state);
        }

        /// <summary>
        /// Uninstalls the registry key.
        /// </summary>
        /// <param name="state">The installer state.</param>
        public override void Uninstall(IDictionary state)
        {
            Restore(state);
            base.Uninstall(state);
        }
    }
}