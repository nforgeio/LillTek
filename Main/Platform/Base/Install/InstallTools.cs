//-----------------------------------------------------------------------------
// FILE:        InstallTools.cs
// CONTRIBUTOR: Jeff Lill
// COPYRIGHT:   Copyright (c) 2005-2015 by Jeffrey Lill.  All rights reserved.
// DESCRIPTION: Implements various installation extensions.

using System;
using System.IO;
using System.Text;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.Configuration.Install;
using System.Windows.Forms;

using LillTek.Common;
using LillTek.Windows;
using LillTek.Cryptography;

namespace LillTek.Install
{
    /// <summary>
    /// Implements various installation extensions.
    /// </summary>
    public class InstallTools : Installer
    {
        //---------------------------------------------------------------------
        // Static members

        /// <summary>
        /// Used internally for marshaling calls to <b>DllRegisterServer()</b> and <b>DllUnregisterServer()</b>
        /// methods in unmanaged DLLs.
        /// </summary>
        /// <returns></returns>
        private delegate int RegDelegate();

        /// <summary>
        /// Loads the specified DLL file and calls its <b>DllRegisterServer()</b> function (if one is present).
        /// </summary>
        /// <param name="fileName">The name of the DLL file.</param>
        /// <remarks>
        /// <note>
        /// This method will use the same rules for finding the DLL
        /// file as are used by the platform <b>LoadLibary()</b> function.
        /// </note>
        /// </remarks>
        public static void RegisterDLL(string fileName)
        {
            IntPtr          hInstance;
            IntPtr          procAddr;
            int             error;
            string          funcName;
            RegDelegate     regDelegate;

            hInstance = WinApi.LoadLibrary(fileName);
            error     = Marshal.GetLastWin32Error();

            if (hInstance == IntPtr.Zero)
                throw new InstallException("Error [{0}] loading [{1}].", error, fileName);

            try
            {
                funcName = "DllRegisterServer";
                procAddr = WinApi.GetProcAddress(hInstance, funcName);
                if (procAddr == IntPtr.Zero)
                    throw new InstallException("Error [{0}] getting pointer to function [{1}].", error, fileName);

                regDelegate = (RegDelegate)Marshal.GetDelegateForFunctionPointer(procAddr, typeof(RegDelegate));

                error = regDelegate();
                if (error != 0)
                    throw new InstallException("Error [{0}] calling [{1}.{2}()].", error, fileName, funcName);
            }
            finally
            {
                WinApi.FreeLibrary(hInstance);
            }
        }

        /// <summary>
        /// Loads the specified DLL file and calls its <b>DllUnregisterServer()</b> function (if one is present).
        /// </summary>
        /// <param name="fileName">The name of the DLL file.</param>
        /// <remarks>
        /// <note>
        /// This method will use the same rules for finding the DLL
        /// file as are used by the platform <b>LoadLibary()</b> function.
        /// </note>
        /// </remarks>
        public static void UnregisterDLL(string fileName)
        {
            IntPtr          hInstance;
            IntPtr          procAddr;
            int             error;
            string          funcName;
            RegDelegate     regDelegate;

            hInstance = WinApi.LoadLibrary(fileName);
            error     = Marshal.GetLastWin32Error();

            if (hInstance == IntPtr.Zero)
                throw new InstallException("Error [{0}] loading [{1}].", error, fileName);

            try
            {

                funcName = "DllUnregisterServer";
                procAddr = WinApi.GetProcAddress(hInstance, funcName);
                if (procAddr == IntPtr.Zero)
                    throw new InstallException("Error [{0}] getting pointer to function [{1}].", error, fileName);

                regDelegate = (RegDelegate)Marshal.GetDelegateForFunctionPointer(procAddr, typeof(RegDelegate));

                error = regDelegate();
                if (error != 0)
                    throw new InstallException("Error [{0}] calling [{1}.{2}()].", error, fileName, funcName);
            }
            finally
            {
                WinApi.FreeLibrary(hInstance);
            }
        }

        //---------------------------------------------------------------------
        // Private command classes

        private sealed class StartServiceCmd
        {
            public string ServiceName;

            public StartServiceCmd(string serviceName)
            {
                this.ServiceName = serviceName;
            }
        }

        private sealed class ConfigureDBCmd
        {
            public string   Package;
            public string   ConfigFile;
            public string   ConStringKey;
            public string   ConStringMacro;
            public string   DefaultDB;

            public ConfigureDBCmd(string package, string configFile, string conStringKey, string conStringMacro, string defaultDB)
            {
                this.Package        = package;
                this.ConfigFile     = configFile;
                this.ConStringKey   = conStringKey;
                this.ConStringMacro = conStringMacro;
                this.DefaultDB      = defaultDB;
            }
        }

        //---------------------------------------------------------------------
        // Implementation

        private string                      setupTitle;             // Title to be displayed in any user interface
        private string                      installIniFolder;       // Path to the folder with the INI file
        private string                      installBinFolder;       // Path to the binary installation folder
        private ArrayList                   commands;               // The commands to be executed
        private IDictionary                 installState;           // Global installation state
        private Dictionary<string, string>  iniMacroReplacements;   // Replacement values for INI file macros
        private string                      cmdFileName;            // File name for the file that will hold the
                                                                    // installation commands

        /// <summary>
        /// Use this constructor for installing a normal application.
        /// </summary>
        /// <param name="setupTitle">
        /// The string to be displayed in any user interface elements presented 
        /// by this class.
        /// </param>
        /// <param name="installFolder">Path to the installation folder.</param>
        /// <param name="cmdFileName">
        /// The local name of the file to be used to record the installation commands
        /// to be executed after the rest of setup has completed.
        /// </param>
        /// <remarks>
        /// To use, simply instantiate an instance in your application installer's
        /// constructor, call the methods to implement the desired setup tasks,
        /// and then add the instance to the application installer's Installers
        /// property.  The installer's <see cref="Installer.Install" />, 
        /// <see cref="Installer.Commit" />, <see cref="Installer.Rollback" />, 
        /// and <see cref="Installer.Uninstall" /> methods will be called during 
        /// the course of the application's installation to perform the requested tasks.
        /// </remarks>
        public InstallTools(string setupTitle, string installFolder, string cmdFileName)
        {
            this.setupTitle           = setupTitle;
            this.installIniFolder     =
            this.installBinFolder     = Helper.AddTrailingSlash(installFolder);
            this.cmdFileName          = cmdFileName;
            this.commands             = new ArrayList();
            this.installState         = null;
            this.iniMacroReplacements = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            this.AfterInstall        += new InstallEventHandler(OnAfterInstall);

            // Delete any existing install command file.

            if (File.Exists(installFolder + cmdFileName))
                File.Delete(installFolder + cmdFileName);
        }

        /// <summary>
        /// Use this constructor for installing web applications.
        /// </summary>
        /// <param name="setupTitle">
        /// The string to be displayed in any user interface elements presented 
        /// by this class.
        /// </param>
        /// <param name="installRootFolder">Path to the <b>root</b> website folder.</param>
        /// <param name="installBinFolder">Path to the website's <b>bin</b> folder.</param>
        /// <param name="cmdFileName">
        /// The local name of the file to be used to record the installation commands
        /// to be executed after the rest of setup has completed.
        /// </param>
        /// <remarks>
        /// To use, simply instantiate an instance in your application installer's
        /// constructor, call the methods to implement the desired setup tasks,
        /// and then add the instance to the application installer's Installers
        /// property.  The installer's <see cref="Installer.Install" />, 
        /// <see cref="Installer.Commit" />, <see cref="Installer.Rollback" />, 
        /// and <see cref="Installer.Uninstall" /> methods will be called during 
        /// the course of the application's installation to perform the requested tasks.
        /// </remarks>
        public InstallTools(string setupTitle, string installRootFolder, string installBinFolder, string cmdFileName)
        {
            this.setupTitle       = setupTitle;
            this.installIniFolder = Helper.AddTrailingSlash(installRootFolder);
            this.installBinFolder = Helper.AddTrailingSlash(installBinFolder);
            this.cmdFileName      = cmdFileName;
            this.commands         = new ArrayList();
            this.installState     = null;
            this.AfterInstall    += new InstallEventHandler(OnAfterInstall);

            // Delete any existing install command file.

            if (File.Exists(installBinFolder + cmdFileName))
                File.Delete(installBinFolder + cmdFileName);
        }

        /// <summary>
        /// Returns the caption used for any dialogs displayed by the instance.
        /// </summary>
        public string Caption
        {
            get { return setupTitle; }
        }

        /// <summary>
        /// Returns the key to be used to reference a value from the IDictionary
        /// for a particular installer instance and property name.
        /// </summary>
        /// <param name="instance">The installer instance.</param>
        /// <param name="property">The property name.</param>
        /// <returns>The key to be used to reference the value.</returns>
        /// <remarks>
        /// This returns: &lt;type&gt;:&lt;assembly path&gt; where &lt;type&gt; is the full type
        /// name of the instance passed, and &lt;property&gt; is the property name.
        /// </remarks>
        internal static string GetStateKey(Installer instance, string property)
        {
            return instance.GetType().FullName + ":" + property;
        }

        /// <summary>
        /// Adds a REG_DWORD registry value at the path specified.
        /// </summary>
        /// <param name="path">
        /// The registry value path see <see cref="LillTek.Common.RegKey">LillTek.Common.RegKey</see>
        ///  for a description of the format for a registry key path.
        /// </param>
        /// <param name="value">The value to be set.</param>
        /// <remarks>
        /// This is useful for situations where the VS Deployment Project implementation
        /// doesn't cut it such as when the value of a registry key is determined
        /// at setup time.
        /// </remarks>
        public void AddRegValue(string path, int value)
        {
            this.Installers.Add(new RegInstaller(path, value));
        }

        /// <summary>
        /// Adds a REG_STRING registry value at the path specified.
        /// </summary>
        /// <param name="path">
        /// The registry value path see <see cref="LillTek.Common.RegKey">LillTek.Common.RegKey</see>
        ///  for a description of the format for a registry key path.
        /// </param>
        /// <param name="value">The value to be set.</param>
        /// <remarks>
        /// This is useful for situations where the VS Deployment Project implementation
        /// doesn't cut it such as when the value of a registry key is determined
        /// at setup time.
        /// </remarks>
        public void AddRegValue(string path, string value)
        {
            this.Installers.Add(new RegInstaller(path, value));
        }

        /// <summary>
        /// Add the directory passed to the environment's PATH variable
        /// if it's not already present.
        /// </summary>
        /// <param name="filePath">The path to the directory.</param>
        /// <param name="allUsers">
        /// <c>true</c> if the path is to be passed added for all users on the 
        /// computer or just the current user.
        /// </param>
        public void AddPathFolder(string filePath, bool allUsers)
        {
            // $todo(jeff.lill): Implement this

            throw new NotImplementedException();
        }

        /// <summary>
        /// Specifies that the named macro's value in the application's INI file be replaced
        /// with the new value specified.
        /// </summary>
        /// <param name="macroName">The macro name (case insensitive).</param>
        /// <param name="value">The new value.</param>
        /// <remarks>
        /// This is a useful for inserting values specified by the user during setup
        /// into the application's configuration file.  The actual configuration file
        /// modification will be performed by <see cref="ReplaceIniMacro" />.
        /// </remarks>
        public void ReplaceIniMacro(string macroName, string value)
        {
            iniMacroReplacements[macroName] = value;
        }

        /// <summary>
        /// Handles the installation of an application's configuration file.
        /// </summary>
        /// <param name="iniFile">The application INI file name (e.g. "MyApp.ini").</param>
        /// <remarks>
        /// <para>
        /// This method provides the mechanism by which setup won't overwrite 
        /// user changes to an configuration file when installing an updated
        /// version of the application.  The method compares the MD5 hash of the
        /// base version of the INI file to the hash of the currently installed
        /// INI file.  If the hashes match, the installed version will be overwritten
        /// by the base version.  If the hashes differ, then the user will be
        /// notified and given the opportunity to choose whether to overwrite 
        /// the installed INI.  In either case, any installed INI file will be
        /// backed up.
        /// </para>
        /// <para>
        /// This method assumes that the base version of the INI file is named
        /// <b>&lt;file&gt;.ini.base</b> and the file with the base MD5 hash is
        /// named <b>&lt;file&gt;.ini.md5</b>.  The method generates a new MD5
        /// file as necessary.
        /// </para>
        /// </remarks>
        public void InstallIniFile(string iniFile)
        {
            string      baseFile = iniFile + ".base";
            byte[]      baseMD5;
            byte[]      curMD5;

            if (!File.Exists(installIniFolder + baseFile))
                throw new IOException(string.Format("[{0}] file needs to be added to the setup project.", baseFile));

            if (!File.Exists(installIniFolder + iniFile))
            {
                // No INI file exists, so simply copy the base file over and generate
                // the MD5 hash.

                File.Copy(installIniFolder + baseFile, installIniFolder + iniFile);

                using (EnhancedStream es = new EnhancedFileStream(installIniFolder + iniFile, FileMode.Open, FileAccess.Read))
                {
                    curMD5 = MD5Hasher.Compute(es, es.Length);
                }

                using (StreamWriter writer = new StreamWriter(installIniFolder + iniFile + ".md5", false, Helper.AnsiEncoding))
                {
                    writer.WriteLine("MD5={0}", Helper.ToHex(curMD5));
                }

                return;
            }

            // Compute the MD5 hash for the existing file and compare it to the
            // hash for the original base file (if there is one).

            using (EnhancedFileStream es = new EnhancedFileStream(installIniFolder + iniFile, FileMode.Open, FileAccess.Read))
            {
                curMD5 = MD5Hasher.Compute(es, es.Length);
            }

            try
            {
                using (var reader = new StreamReader(installIniFolder + iniFile + ".md5", Helper.AnsiEncoding))
                {
                    string line;

                    line = reader.ReadLine();
                    if (line == null)
                        throw new Exception();

                    if (!line.StartsWith("MD5="))
                        throw new Exception();

                    baseMD5 = Helper.FromHex(line.Substring(4));
                }
            }
            catch
            {
                baseMD5 = new byte[0];  // The hashes will always differ if the MD5 file doesn't exist or isn't valid.
            }

            // Make a backup copy of the current INI file.

            File.Copy(installIniFolder + iniFile, installIniFolder + Helper.ToIsoDate(DateTime.UtcNow).Replace(':', '-') + "-" + iniFile);

            if (Helper.ArrayEquals(baseMD5, curMD5))
            {
                // If the base and current MD5 hashes are the same, we can be assured that the
                // user has not customized the INI file.  In this case, we'll simply overwrite
                // the existing INI file with the new version.

                File.Delete(installIniFolder + iniFile);
                File.Copy(installIniFolder + baseFile, installIniFolder + iniFile);
            }
            else
            {
                // It looks like the user has modified the INI file.  So prompt the user
                // to see if he wants to overwrite or not.

                if (MessageBox.Show(string.Format("The application configuration file [{0}] has been customized.\r\n\r\nDo you want to overwrite this with the new version?", iniFile),
                                    setupTitle, MessageBoxButtons.YesNo, MessageBoxIcon.Warning, MessageBoxDefaultButton.Button2) == DialogResult.Yes)
                {
                    File.Delete(installIniFolder + iniFile);
                    File.Copy(installIniFolder + baseFile, installIniFolder + iniFile);
                }
            }

            // Generate the MD5 hash file from the base INI file.

            using (var es = new EnhancedFileStream(installIniFolder + baseFile, FileMode.Open, FileAccess.Read))
            {
                baseMD5 = MD5Hasher.Compute(es, es.Length);
            }

            using (var writer = new StreamWriter(installIniFolder + iniFile + ".md5", false, Helper.AnsiEncoding))
            {
                writer.WriteLine("MD5={0}", Helper.ToHex(baseMD5));
            }

            // Perform any macro replacements.

            if (iniMacroReplacements.Count > 0)
            {
                using (var fs = new EnhancedFileStream(installIniFolder + iniFile, FileMode.Open, FileAccess.ReadWrite))
                {
                    foreach (var replacement in iniMacroReplacements)
                    {
                        fs.Position = 0;
                        Config.EditMacro(fs, Helper.AnsiEncoding, replacement.Key, replacement.Value);
                    }
                }
            }
        }

        /// <summary>
        /// Specifies that the named service should be started after a 
        /// successful installation.
        /// </summary>
        /// <param name="serviceName">The service name.</param>
        /// <remarks>
        /// <para>
        /// This is currently implemented with a work-around.  For some reason,
        /// the framework ServiceInstaller class doesn't fully install the
        /// service when the AfterInstall event is raised so I am not
        /// able to start the service at that time.
        /// </para>
        /// <para>
        /// This method works by launching the InstallHelper.exe
        /// application, passing the service name.  This application must be 
        /// included in the setup project and must be located in the file 
        /// folder specified.
        /// </para>
        /// </remarks>
        public void StartService(string serviceName)
        {
            commands.Add(new StartServiceCmd(serviceName));
        }

        /// <summary>
        /// Specifies that a database installation package should be installed
        /// after a successful installation.
        /// </summary>
        /// <param name="packageFile">Fully qualified name of the database package file.</param>
        /// <param name="configFile">Fully qualified name of the application's configuration file.</param>
        /// <param name="conStringKey">The fully qualified name of the application's database connection string setting.</param>
        /// <param name="conStringMacro">The name of the #define macro in the application configation file with the database connection string.</param>
        /// <param name="defaultDB">The default database name (or <c>null</c>).</param>
        public void ConfigureDatabase(string packageFile, string configFile, string conStringKey, string conStringMacro, string defaultDB)
        {
            if (defaultDB == null)
                defaultDB = string.Empty;

            commands.Add(new ConfigureDBCmd(packageFile, configFile, conStringKey, conStringMacro, defaultDB));
        }

        /// <summary>
        /// Adds an HTTP.SYS prefix registration for a Windows account.
        /// </summary>
        /// <param name="uriPrefix">The URI prefix with optional wildcards.</param>
        /// <param name="account">The Windows account name.</param>
        public void HttpUriPrefixReservation(string uriPrefix, string account)
        {
            this.Installers.Add(new HttpPrefixInstaller(uriPrefix, account));
        }

        //---------------------------------------------------------------------
        // Installation Handlers

        /// <summary>
        /// Handle the installation activities.
        /// </summary>
        /// <param name="state">The install state.</param>
        public override void Install(IDictionary state)
        {
            installState = state;
            base.Install(state);
        }

        /// <summary>
        /// Handles the post install actions.
        /// </summary>
        private void OnAfterInstall(object sender, InstallEventArgs args)
        {
            StreamWriter writer;

            // Return if there's nothing for InstallHelper.exe to do.

            if (commands.Count == 0)
                return;

            // Append the commands to the .ini file so everything will
            // work properly when multiple InstallTools instances were
            // created during an installation.

            writer = new StreamWriter(installBinFolder + cmdFileName, true, Helper.AnsiEncoding);

            // Write the accumulated commands to the .ini file.

            try
            {
                // Build up the command line for InstallHelper.

                writer.WriteLine("-wait:{0}", ((uint)Process.GetCurrentProcess().Id).ToString());
                writer.WriteLine("-title:{0}", setupTitle);

                foreach (object command in commands)
                {
                    if (command is StartServiceCmd)
                    {
                        var cmd = (StartServiceCmd)command;

                        writer.WriteLine("-start:{0}", cmd.ServiceName);
                    }
                    else if (command is ConfigureDBCmd)
                    {
                        var             cmd = (ConfigureDBCmd)command;
                        StreamWriter    dbWriter;

                        writer.WriteLine("-configdb:@" + installBinFolder + "DBPackage.ini");

                        dbWriter = new StreamWriter(installBinFolder + "DBPackage.ini", false, Helper.AnsiEncoding);

                        try
                        {
                            dbWriter.WriteLine("-install:{0}", cmd.Package);
                            dbWriter.WriteLine("-config:{0}", cmd.ConfigFile);
                            dbWriter.WriteLine("-setting:{0}", cmd.ConStringKey);
                            dbWriter.WriteLine("-macro:{0}", cmd.ConStringMacro);
                            dbWriter.WriteLine("-defdb:{0}", cmd.DefaultDB);
                        }
                        finally
                        {
                            dbWriter.Close();
                        }
                    }
                }
            }
            finally
            {
                writer.Close();
            }

            // Launch only one instance of the install tools application.

            if (installState["InstallTools.Launched"] == null)
            {
                // Probe for the existence of the "InstallHelper.exe" or "_InstallHelper.exe" file
                // in the installation folder and launch the executable found.  Setup for LillTek
                // applications will typically include the version with the leading underscore since
                // these setup projects typically add the tool's build output directly.  Non-LillTek
                // application setup will typically include the standalone executable generated by 
                // ILMerge which does not have the underscore.

                string exeName;

                exeName = "_InstallHelper.exe";
                if (!File.Exists(installBinFolder + exeName))
                {
                    exeName = "InstallHelper.exe";
                    if (!File.Exists(installBinFolder + exeName))
                        throw new InvalidOperationException("You need to add the LillTek [InstallHelper.exe] tool to the application folder.");
                }

                Process.Start(new ProcessStartInfo(installBinFolder + exeName, "\"@" + installBinFolder + cmdFileName + "\""));
                installState["InstallTools.Launched"] = "yes";
            }
        }
    }
}
