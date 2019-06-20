//-----------------------------------------------------------------------------
// FILE:        OsVersion.cs
// CONTRIBUTOR: Jeff Lill
// COPYRIGHT:   Copyright (c) 2005-2015 by Jeffrey Lill.  All rights reserved.
// DESCRIPTION: Returns extended operating system version and capability
//              information.

using System;
using System.Runtime.InteropServices;

using LillTek.Windows;

namespace LillTek.Common
{
    /// <summary>
    /// Returns extended operating system version and capability information.
    /// </summary>
    public sealed class OsVersion
    {
        private Version     win7Version            = new Version(7, 0);
        private Version     longhornServerVersion  = new Version(6, 0);
        private Version     vistaVersion           = new Version(6, 0);
        private Version     winServer2003R2Version = new Version(5, 2);
        private Version     winServer2003Version   = new Version(5, 2);
        private Version     winXPVersion           = new Version(5, 1);
        private Version     win2000Version         = new Version(5, 0);
        private Version     oSVersion;
        private bool        backOffice;
        private bool        webEdition;
        private bool        computeCluster;
        private bool        datacenterEdition;
        private bool        enterpriseEdition;
        private bool        embeddedXP;
        private bool        personal;
        private bool        singleUserTerminalServices;
        private bool        smallBusinessEdition;
        private bool        smallBusinessRestricted;
        private bool        storageServer;
        private bool        terminalServices;
        private bool        homeServer;
        private bool        server;
        private string      servicePack;
        private bool        workstation;
        private bool        activeDirectory;
        private bool        mediaCenter;
        private bool        tabletPC;

        /// <summary>
        /// Windows 7 version number.
        /// </summary>
        public Version Win7Version
        {
            get { return win7Version; }
        }

        /// <summary>
        /// Windows Server "Longhorn" version number.
        /// </summary>
        public Version LonghornServerVersion
        {
            get { return longhornServerVersion; }
        }

        /// <summary>
        /// Windows Vista version number.
        /// </summary>
        public Version VistaVersion
        {
            get { return vistaVersion; }
        }

        /// <summary>
        /// Windows Server 2003 R2 version number.
        /// </summary>
        public Version WinServer2003R2Version
        {
            get { return winServer2003R2Version; }
        }

        /// <summary>
        /// Windows Server 2003 version number.
        /// </summary>
        public Version WinServer2003Version
        {
            get { return winServer2003Version; }
        }

        /// <summary>
        /// Windows XP version number.
        /// </summary>
        public Version WinXPVersion
        {
            get { return winXPVersion; }
        }

        /// <summary>
        /// Windows 2000 version number.
        /// </summary>
        public Version Win2000Version
        {

            get { return win2000Version; }
        }

        /// <summary>
        /// The current Windows version number.
        /// </summary>
        public Version OSVersion
        {
            get { return oSVersion; }
        }

        /// <summary>
        /// Microsoft BackOffice components are installed.
        /// </summary>
        public bool BackOffice
        {
            get { return backOffice; }
        }

        /// <summary>
        /// Windows Server 2003, Web Edition is installed.
        /// </summary>
        public bool WebEdition
        {
            get { return webEdition; }
        }

        /// <summary>
        /// Windows Server 2003, Compute Cluster Edition is installed.
        /// </summary>
        public bool ComputeCluster
        {
            get { return computeCluster; }
        }

        /// <summary>
        /// Windows Server "Longhorn", Datacenter Edition, Windows Server 2003, Datacenter Edition or 
        /// Windows 2000 Datacenter Server is installed.
        /// </summary>
        public bool DatacenterEdition
        {
            get { return datacenterEdition; }
        }

        /// <summary>
        /// Windows Server "Longhorn", Enterprise Edition, Windows Server 2003, Enterprise Edition, 
        /// Windows 2000 Advanced Server, or Windows NT Server 4.0 Enterprise Edition is installed.
        /// </summary>
        public bool EnterpriseEdition
        {
            get { return enterpriseEdition; }
        }

        /// <summary>
        /// Windows XP Embedded is installed.
        /// </summary>
        public bool EmbeddedXP
        {
            get { return embeddedXP; }
        }

        /// <summary>
        /// Windows Vista Home Premium, Windows Vista Home Basic, or Windows XP Home Edition is installed.
        /// </summary>
        public bool Personal
        {

            get { return personal; }
        }

        /// <summary>
        /// Remote Desktop is supported, but only one interactive session is supported. 
        /// This value is set unless the system is running in application server mode.
        /// </summary>
        public bool SingleUserTerminalServices
        {

            get { return singleUserTerminalServices; }
        }

        /// <summary>
        /// Microsoft Small Business Server was once installed on the system, but may have been 
        /// upgraded to another version of Windows.
        /// </summary>
        public bool SmallBusinessEdition
        {
            get { return smallBusinessEdition; }
        }

        /// <summary>
        /// Microsoft Small Business Server is installed with the restrictive client license in force.
        /// </summary>
        public bool SmallBusinessRestricted
        {
            get { return smallBusinessRestricted; }
        }

        /// <summary>
        /// Windows Storage Server 2003 R2 or Windows Storage Server 2003 is installed.
        /// </summary>
        public bool StorageServer
        {
            get { return storageServer; }
        }

        /// <summary>
        /// Terminal Services is installed. This value is always set. If <see cref="TerminalServices" /> is set but 
        /// <see cref="SingleUserTerminalServices" /> is not set, the system is running in application server mode.
        /// </summary>
        public bool TerminalServices
        {
            get { return terminalServices; }
        }

        /// <summary>
        /// Windows Home Server is installed.
        /// </summary>
        public bool HomeServer
        {
            get { return homeServer; }
        }

        /// <summary>
        /// The system is a server. 
        /// </summary>
        public bool Server
        {
            get { return server; }
        }

        /// <summary>
        /// Name of the installed operating system service pack (if any).
        /// </summary>
        public string ServicePack
        {
            get { return servicePack; }
        }

        /// <summary>
        /// The operating system is Windows Vista, Windows XP Professional, Windows XP Home Edition, 
        /// Windows 2000 Professional, or Windows NT Workstation 4.0.
        /// </summary>
        public bool Workstation
        {
            get { return workstation; }
        }

        /// <summary>
        /// The system is a domain controller.
        /// </summary>
        public bool ActiveDirectory
        {
            get { return activeDirectory; }
        }

        /// <summary>
        /// The system is a media center.
        /// </summary>
        public bool MediaCenter
        {
            get { return mediaCenter; }
        }

        /// <summary>
        /// The system is a tablet PC.
        /// </summary>
        public bool TabletPC
        {
            get { return tabletPC; }
        }

        /// <summary>
        /// Returns <c>true</c> if the host operating system is a Windows variant.
        /// </summary>
        public bool IsWindows
        {
            get { return Helper.IsWindows; }
        }

        /// <summary>
        /// Returns <c>true</c> if the host operating system is a Unix/Linux variant.
        /// </summary>
        public bool IsUnix
        {
            get { return Helper.IsUnix; }
        }

        /// <summary>
        /// This constructor queries the operating system for extended version and
        /// capability information and initializes the class members with the
        /// values returned.
        /// </summary>
        public OsVersion()
        {
            if (Helper.IsWindows)
                InitWindows();
            else
                InitUnix();
        }

        /// <summary>
        /// Loads Windows version information.
        /// </summary>
        private void InitWindows()
        {
            OSVERSIONINFOEX info = new OSVERSIONINFOEX();

            info.dwOSVersionInfoSize = Marshal.SizeOf(typeof(OSVERSIONINFOEX));
            if (!WinApi.GetVersionEx(ref info))
                throw new ApplicationException("WinApi.GetVersionEx() call failed.");

            this.oSVersion                  = new Version(info.dwMajorVersion, info.dwMinorVersion, info.dwBuildNumber);
            this.backOffice                 = (info.wSuiteMask & (int)WinApi.OsSuite.VER_SUITE_BACKOFFICE) != 0;
            this.webEdition                 = (info.wSuiteMask & (int)WinApi.OsSuite.VER_SUITE_BLADE) != 0;
            this.computeCluster             = (info.wSuiteMask & (int)WinApi.OsSuite.VER_SUITE_COMPUTE_SERVER) != 0;
            this.datacenterEdition          = (info.wSuiteMask & (int)WinApi.OsSuite.VER_SUITE_DATACENTER) != 0;
            this.enterpriseEdition          = (info.wSuiteMask & (int)WinApi.OsSuite.VER_SUITE_ENTERPRISE) != 0;
            this.embeddedXP                 = (info.wSuiteMask & (int)WinApi.OsSuite.VER_SUITE_EMBEDDEDNT) != 0;
            this.personal                   = (info.wSuiteMask & (int)WinApi.OsSuite.VER_SUITE_PERSONAL) != 0;
            this.singleUserTerminalServices = (info.wSuiteMask & (int)WinApi.OsSuite.VER_SUITE_SINGLEUSERTS) != 0;
            this.smallBusinessEdition       = (info.wSuiteMask & (int)WinApi.OsSuite.VER_SUITE_SMALLBUSINESS) != 0;
            this.smallBusinessRestricted    = (info.wSuiteMask & (int)WinApi.OsSuite.VER_SUITE_SMALLBUSINESS_RESTRICTED) != 0;
            this.storageServer              = (info.wSuiteMask & (int)WinApi.OsSuite.VER_SUITE_STORAGE_SERVER) != 0;
            this.terminalServices           = (info.wSuiteMask & (int)WinApi.OsSuite.VER_SUITE_TERMINAL) != 0;
            this.homeServer                 = (info.wSuiteMask & (int)WinApi.OsSuite.VER_SUITE_WH_SERVER) != 0;

            this.server                     = info.wProductType == (int)WinApi.OsProduct.VER_NT_SERVER ||
                                              info.wProductType == (int)WinApi.OsProduct.VER_NT_DOMAIN_CONTROLLER;
            this.workstation                = info.wProductType == (int)WinApi.OsProduct.VER_NT_WORKSTATION;
            this.activeDirectory            = info.wProductType == (int)WinApi.OsProduct.VER_NT_DOMAIN_CONTROLLER;

            this.mediaCenter                = WinApi.GetSystemMetrics((int)SystemMetricsCodes.SM_MEDIACENTER) != 0;
            this.tabletPC                   = WinApi.GetSystemMetrics((int)SystemMetricsCodes.SM_TABLETPC) != 0;

            this.servicePack                = info.szCSDVersion;
        }

        /// <summary>
        /// Loads Unix/Linux version information.
        /// </summary>
        private void InitUnix()
        {
            // $todo(jeff.lill): 
            //
            // I need to come back and load information about the Linux kernel
            // and distribution versions.

            this.oSVersion                  = new Version(0, 0, 0);
            this.backOffice                 = false;
            this.webEdition                 = false;
            this.computeCluster             = false;
            this.datacenterEdition          = false;
            this.enterpriseEdition          = false;
            this.embeddedXP                 = false;
            this.personal                   = false;
            this.singleUserTerminalServices = false;
            this.smallBusinessEdition       = false;
            this.smallBusinessRestricted    = false;
            this.storageServer              = false;
            this.terminalServices           = false;
            this.homeServer                 = false;
            this.server                     = true;     // assuming that all Unix/Linux boxes are deployed as servers
            this.workstation                = false;
            this.activeDirectory            = false;
            this.mediaCenter                = false;
            this.tabletPC                   = false;
            this.servicePack                = string.Empty;
        }
    }
}
