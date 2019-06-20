//-----------------------------------------------------------------------------
// FILE:        HttpPrefixInstaller.cs
// CONTRIBUTOR: Jeff Lill
// COPYRIGHT:   Copyright (c) 2005-2015 by Jeffrey Lill.  All rights reserved.
// DESCRIPTION: Implements the reservation of HTTP.SYS URI prefixes for a
//              Windows account.

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
    /// Implements the reservation of HTTP.SYS URI prefixes for a Windows account.
    /// </summary>
    internal sealed class HttpPrefixInstaller : Installer
    {
        private string uriPrefix;
        private string account;

        /// <summary>
        /// Initializes an installer that adds a HTTP.SYS URI prefix for a Windows account.
        /// </summary>
        /// <param name="uriPrefix">The URI prefix.</param>
        /// <param name="account">The Windows account.</param>
        public HttpPrefixInstaller(string uriPrefix, string account)
        {
            this.uriPrefix = uriPrefix;
            this.account = account;
        }

        /// <summary>
        /// Installs the registry key.
        /// </summary>
        /// <param name="state">The installer state.</param>
        public override void Install(IDictionary state)
        {
            base.Install(state);

            state[InstallTools.GetStateKey(this, "UriPrefix")] = uriPrefix;
            state[InstallTools.GetStateKey(this, "Account")] = account;

            HttpSys.AddPrefixReservation(uriPrefix, account);
        }

        /// <summary>
        /// Restores the state to before the installation.
        /// </summary>
        /// <param name="state">The installer state.</param>
        private void Restore(IDictionary state)
        {
            string uriPrefix = (string)state[InstallTools.GetStateKey(this, "UriPrefix")];
            string account = (string)state[InstallTools.GetStateKey(this, "Account")];

            HttpSys.RemovePrefixReservation(uriPrefix, account);
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