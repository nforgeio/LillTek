//-----------------------------------------------------------------------------
// FILE:        DBInstallResult.cs
// CONTRIBUTOR: Jeff Lill
// COPYRIGHT:   Copyright (c) 2005-2015 by Jeffrey Lill.  All rights reserved.
// DESCRIPTION: Describes the result of a DBPackageInstall installation.

using System;

namespace LillTek.Data.Install
{
    /// <summary>
    /// Describes the result of a DBPackageInstall installation.
    /// </summary>
    public enum DBInstallResult
    {
        /// <summary>
        /// The disposition of the install is unknown, probably due to 
        /// a programming error in this assembly.
        /// </summary>
        Unknown,

        /// <summary>
        /// The user cancelled the installation.
        /// </summary>
        Cancelled,

        /// <summary>
        /// A fresh database was configured successfully.
        /// </summary>
        Installed,

        /// <summary>
        /// An existing database was upgraded successfully.
        /// </summary>
        Upgraded,

        /// <summary>
        /// The existing database is up-to-date and did not need to be upgraded.
        /// </summary>
        UpToDate,

        /// <summary>
        /// The installation failed due to some environmental problem.
        /// </summary>
        Error
    }
}
