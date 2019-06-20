//-----------------------------------------------------------------------------
// FILE:        EnvironmentVars.cs
// CONTRIBUTOR: Jeff Lill
// COPYRIGHT:   Copyright (c) 2005-2015 by Jeffrey Lill. All rights reserved.
// DESCRIPTION: Implements cross platform environment variables routines.

using System;
using System.IO;
using System.Net;
using System.Text;
using System.Collections;
using System.Collections.Generic;

namespace LillTek.Common
{
#if MOBILE_DEVICE
    /// <summary>
    /// Implements cross platform environment variables routines.
    /// </summary>
    /// <remarks>
    /// <para>
    /// The Compact Framework does not support environment variables.  This class
    /// provides a common implementation of environment variables that will
    /// work on both the Windows XP and Windows CE platforms.  
    /// </para>
    /// <para>
    /// Windows CE doesn't seem to support the concept of environment variables,
    /// at least the concept of global environment variables.
    /// </para>
    /// <para>
    /// The class loads environment variables specified by the 
    /// EnvironmentVars.txt file.  The class loads first attempts to load this
    /// file from the directory holding the currently running process.  If not 
    /// found there, the file will be loaded from <b>$(SystemDirectory)</b>.
    /// </para>
    /// <para>
    /// The file is formatted as lines of <c>[name]=[value]</c> pairs specifying the variables 
    /// and their values.
    /// </para>
    /// <para>
    /// Other files may be included using the <c>#include "filename"</c> syntax.
    /// </para>
    /// <para>
    /// Lines beginning with "//" are ignored as comments.  Note that environment 
    /// variables are case insensitive.  The file is expected to use ANSI encoding.
    /// </para>
    /// <para>
    /// Under Windows XP, environment variables are created in the System Control
    /// Panel or in a parent process and are inherited by the current process.
    /// The Windows XP implementation of this class will load all of the 
    /// environment variables known to the current process before loading the
    /// EnvironmentVars.txt file.  This means that values in this file may
    /// override the system values.
    /// </para>
    /// <para>
    /// In addition to variables loaded from EnvironmentVars.txt and the
    /// system, this class exposes some built-in variables:
    /// </para>
    /// <para>
    /// <div class="tablediv">
    /// <table class="dtTABLE" cellspacing="0" ID="Table1">
    /// <tr valign="top">
    /// <th width="1">Name</th>
    /// <th width="90%">Description</th>
    /// </tr>
    /// <tr valign="top"><td><b>$(SystemRoot)</b></td><td>Path to the Windows root directory.</td></tr>
    /// <tr valign="top"><td><b>$(SystemDirectory)</b></td><td>Path to the Windows system files directory</td></tr>
    /// <tr valign="top"><td><b>$(Temp)</b> &amp; <b>$(Tmp)</b></td><td>Path to a temporary directory</td></tr>
    /// <tr valign="top">
    ///     <td><b>$(AppPath)</b></td>
    ///     <td>
    ///     Directory containing the application's main executable if a normal application
    ///     was initialized with a call to <see cref="Helper.Init" /> or the website's root folder
    ///     if <see cref="Helper.InitWeb" /> was called when the website was started.  Note that the 
    ///     path does not include a terminating slash.
    ///     </td>
    /// </tr>
    /// <tr valign="top"><td><b>$(IsDebug)</b></td><td><c>true</c> for debug builds, <c>false</c> otherwise.</td></tr>
    /// <tr valign="top"><td><b>$(IsRelease)</b></td><td><c>true</c> for release builds, <c>false</c> otherwise.</td></tr>
    /// <tr valign="top"><td><b>$(WINFULL)</b></td><td>Defined for WINFULL operating systems (Windows/XP, Windows/Server,...)</td></tr>
    /// <tr valign="top"><td><b>$(WINCE)</b></td><td>Defined for Windows/CE</td></tr>
    /// <tr valign="top"><td><b>$(OS)</b></td><td>The operating system name (Platform Name)</td></tr>
    /// <tr valign="top"><td><b>$(OS.WinWorkstation)</b></td><td>Defined for worksation operating systems: Windows/XP, Vista, Windows 7, etc.</td></tr>
    /// <tr valign="top"><td><b>$(OS.WinServer)</b></td><td>Defined if the current operating system is Windows/Server</td></tr>
    /// <tr valign="top"><td><b>$(OS.Windows)</b></td><td>Defined if the current operating system is a Windows derivative.</td></tr>
    /// <tr valign="top"><td><b>$(OS.Unix)</b></td><td>Defined if the current operating system is a Unix/Linux derivative.</td></tr>
    /// <tr valign="top"><td><b>$(Mono)</b></td><td>Defined if the running under Mono.</td></tr>
    /// <tr valign="top"><td><b>$(Guid)</b></td><td>A globally unique identifier</td></tr>
    /// <tr valign="top"><td><b>$(IsMobileDevice)</b></td><td>Defined if the running on a mobile device.</td></tr>
    /// <tr valign="top"><td><b>$(WindowsPhone)</b></td><td>Defined if the running on a Windows Phone device.</td></tr>
    /// <tr valign="top"><td><b>$(Android)</b></td><td>Defined if the running on a Google Android device.</td></tr>
    /// <tr valign="top"><td><b>$(AppleIOS)</b></td><td>Defined if the running on an Apple iOS device.</td></tr>
    /// </table>
    /// </div>
    /// </para>
    /// </remarks>
#else
    /// <summary>
    /// Implements cross platform environment variables routines.
    /// </summary>
    /// <remarks>
    /// <para>
    /// The Compact Framework does not support environment variables.  This class
    /// provides a common implementation of environment variables that will
    /// work on both the Windows XP and Windows CE platforms.  
    /// </para>
    /// <para>
    /// Windows CE doesn't seem to support the concept of environment variables,
    /// at least the concept of global environment variables.
    /// </para>
    /// <para>
    /// The class loads environment variables specified by the 
    /// EnvironmentVars.txt file.  The class loads first attempts to load this
    /// file from the directory holding the currently running process.  If not 
    /// found there, the file will be loaded from <b>$(SystemDirectory)</b>.
    /// </para>
    /// <para>
    /// The file is formatted as lines of <c>[name]=[value]</c> pairs specifying the variables 
    /// and their values.
    /// </para>
    /// <para>
    /// Other files may be included using the <c>#include "filename"</c> syntax.
    /// </para>
    /// <para>
    /// Lines beginning with "//" are ignored as comments.  Note that environment 
    /// variables are case insensitive.  The file is expected to use ANSI encoding.
    /// </para>
    /// <para>
    /// Under Windows XP, environment variables are created in the System Control
    /// Panel or in a parent process and are inherited by the current process.
    /// The Windows XP implementation of this class will load all of the 
    /// environment variables known to the current process before loading the
    /// EnvironmentVars.txt file.  This means that values in this file may
    /// override the system values.
    /// </para>
    /// <para>
    /// In addition to variables loaded from EnvironmentVars.txt and the
    /// system, this class exposes some built-in variables:
    /// </para>
    /// <para>
    /// <div class="tablediv">
    /// <table class="dtTABLE" cellspacing="0" ID="Table1">
    /// <tr valign="top">
    /// <th width="1">Name</th>
    /// <th width="90%">Description</th>
    /// </tr>
    /// <tr valign="top"><td><b>$(SystemRoot)</b></td><td>Path to the Windows root directory.</td></tr>
    /// <tr valign="top"><td><b>$(SystemDirectory)</b></td><td>Path to the Windows system files directory</td></tr>
    /// <tr valign="top"><td><b>$(Temp)</b> &amp; <b>$(Tmp)</b></td><td>Path to a temporary directory</td></tr>
    /// <tr valign="top">
    ///     <td><b>$(AppPath)</b></td>
    ///     <td>
    ///     Directory containing the application's main executable if a normal application
    ///     was initialized with a call to <see cref="Helper.InitializeApp" /> or the website's root folder
    ///     if <see cref="Helper.InitializeWebApp" /> was called when the website was started.  Note that the 
    ///     path does not include a terminating slash.
    ///     </td>
    /// </tr>
    /// <tr valign="top"><td><b>$(IsDebug)</b></td><td><c>true</c> for debug builds, <c>false</c> otherwise.</td></tr>
    /// <tr valign="top"><td><b>$(IsRelease)</b></td><td><c>true</c> for release builds, <c>false</c> otherwise.</td></tr>
    /// <tr valign="top"><td><b>$(AppVersion)</b></td><td>Version number of the application's entry assembly.</td></tr>
    /// <tr valign="top"><td><b>$(ProgramDataPath)</b></td><td>Path to the common application data folder.</td></tr>
    /// <tr valign="top"><td><b>$(WINFULL)</b></td><td>Defined for WINFULL operating systems (Windows/XP, Windows/Server,...)</td></tr>
    /// <tr valign="top"><td><b>$(WINCE)</b></td><td>Defined for Windows/CE</td></tr>
    /// <tr valign="top"><td><b>$(AZURE)</b></td><td>Defined when hosted on Windows Azure.</td></tr>
    /// <tr valign="top"><td><b>$(AWS)</b></td><td>Defined when hosted on Amazon Web Services (AWS).</td></tr>
    /// <tr valign="top"><td><b>$(CLOUD)</b></td><td>Defined when hosted on Windows Azure or AWS.</td></tr>
    /// <tr valign="top"><td><b>$(OS)</b></td><td>The operating system name (Platform Name)</td></tr>
    /// <tr valign="top"><td><b>$(OS.WinWorkstation)</b></td><td>Defined if the current operating system is Windows/XP</td></tr>
    /// <tr valign="top"><td><b>$(OS.WinServer)</b></td><td>Defined for worksation operating systems: Windows/XP, Vista, Windows 7, etc.</td></tr>
    /// <tr valign="top"><td><b>$(OS.Windows)</b></td><td>Defined if the current operating system is a Windows derivative.</td></tr>
    /// <tr valign="top"><td><b>$(OS.Unix)</b></td><td>Defined if the current operating system is a Unix/Linux derivative.</td></tr>
    /// <tr valign="top"><td><b>$(Mono)</b></td><td>Defined if the running under Mono.</td></tr>
    /// <tr valign="top"><td><b>$(IsMobileDevice)</b></td><td>Defined if the running on a mobile device.</td></tr>
    /// <tr valign="top"><td><b>$(WindowsPhone)</b></td><td>Defined if the running on a Windows Phone device.</td></tr>
    /// <tr valign="top"><td><b>$(Android)</b></td><td>Defined if the running on a Google Android device.</td></tr>
    /// <tr valign="top"><td><b>$(AppleIOS)</b></td><td>Defined if the running on an Apple iOS device.</td></tr>
    /// <tr valign="top"><td><b>$(Guid)</b></td><td>A globally unique identifier</td></tr>
    /// <tr valign="top"><td><b>$(MachineName)</b></td><td>The NetBIOS computer name.</td></tr>
    /// <tr valign="top"><td><b>$(HostName)</b></td><td>The computer's host name (this is generally the same as <b>MachineName</b>) with the default domain suffix if any).</td></tr>
    /// <tr valign="top"><td><b>$(ServerID)</b></td><td>The globally unique domain name for this machine initialized by the Service Manager if present,  $(HostName) otherwise.</td></tr>
    /// <tr valign="top"><td><b>$(ProcessorCount)</b></td><td>The number of processor cores running on the current machine.</td></tr>
    /// <tr valign="top"><td><b>$(IP-Address)</b></td><td>A connected network adapter IP address or the loopback address (127.0.0.1).</td></tr>
    /// <tr valign="top"><td><b>$(IP-Mask)</b></td><td>A connected network adapter's subnet mask or 255.255.255.0.</td></tr>
    /// <tr valign="top"><td><b>$(IP-Subnet)</b></td><td>A connected network adapter's IP address and subnet expressed in slash notation.</td></tr>
    /// <tr valign="top"><td><b>$(Local-IP)</b></td><td>The internal cloud IP address for the current machine if hosted on Azure or AWS (otherwise $(IP-Address)).</td></tr>
    /// <tr valign="top"><td><b>$(Public-IP)</b></td><td>The public cloud IP address for the current machine if hosted on Azure or AWS (otherwise $(IP-Address)).</td></tr>
    /// <tr valign="top"><td><b>$(Public-HostName)</b></td><td>The public cloud host name for the current machine if hosted on Azure or AWS (otherwise empty).</td></tr>
    /// </table>
    /// </div>
    /// </para>
    /// <para>
    /// With the exception of <b>$(Temp)</b> and <b>$(Tmp)</b>, these variables cannot be
    /// overridden by editing EnvironmentVars.txt.
    /// </para>
    /// <para>
    /// The <b>$(ServerID)</b> variable is somewhat special.  The variable is used to specify a globally unique domain
    /// name that can be used to identify and access the current machine.  By default, this variable is the same
    /// as <b>$(HostName)</b>, but if the <b>Server Manager</b> service is running, this will be set to the
    /// <b>ServerID</b> host name loaded from the service manager's configuration file.  This value is persisted
    /// to the Windows registry so it will be available to all applications using the LillTek Platform.
    /// </para>
    /// <para>
    /// The service manager uses the <see cref="ServerID" /> property to persist a value to the registry.
    /// The registry location is:
    /// </para>
    /// <code language="none">
    ///     HKEY_LOCAL_MACHINE\SOFTWARE\LillTek\Platform\Common:ServerID
    /// </code>
    /// </remarks>
#endif
    public static class EnvironmentVars
    {
        private const string ServerIDRegPath = @"HKEY_LOCAL_MACHINE\SOFTWARE\LillTek\Platform\Common:ServerID";

        private static Dictionary<string, string> vars;
        private static char[] macroChars = new char[] { '$', '%' };
#if !MOBILE_DEVICE
        private static OsVersion osVersion = new OsVersion();
#endif

        /// <summary>
        /// Static constructor.
        /// </summary>
        static EnvironmentVars()
        {
            Reload();
        }

        /// <summary>
        /// Loads or reloads the environment variables.
        /// </summary>
        public static void Reload()
        {
            vars = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

            LoadGlobal();

#if !MOBILE_DEVICE

            // Process the EnvironmentVars.txt file if present.

            string      path;
            string      fileName;
            TextReader  reader;

            path = Helper.EntryAssemblyFolder;
            if (path == null)
                return;

            if (path != null && File.Exists(path + @"\EnvironmentVars.txt"))
                fileName = path + @"\EnvironmentVars.txt";
            else
            {

                fileName = SystemDirectory + @"\EnvironmentVars.txt";
                if (!File.Exists(fileName))
                    return;
            }

            reader = new StreamReader(fileName, Helper.AnsiEncoding);
            try
            {
                Load(reader);
            }
            finally
            {
                reader.Close();
            }

#endif // !MOBILE_DEVICE
        }

        /// <summary>
        /// Loads global environment variables.
        /// </summary>
        private static void LoadGlobal()
        {
#if WINFULL
            // Load the system and process environment variables.

            Hashtable dictionary;

            dictionary = (Hashtable)Environment.GetEnvironmentVariables();
            foreach (string key in dictionary.Keys)
                vars[key] = (string)dictionary[key];
#endif
        }

        /// <summary>
        /// Loads the environment variables from the text reader passed.
        /// </summary>
        /// <param name="reader">The text reader.</param>
        internal static void Load(TextReader reader)
        {
            try
            {
                string      line;
                string      name;
                string      value;
                int         p;
                int         pEnd;

                while ((line = reader.ReadLine()) != null)
                {
                    line = line.Trim();
                    if (line == string.Empty || line.StartsWith("//"))
                        continue;

                    if (line.StartsWith("#include "))
                    {
                        p = line.IndexOf('"');
                        if (p == -1)
                            throw new FormatException("Invalid #include statement.");

                        pEnd = line.IndexOf('"', p + 1);
                        if (p == -1)
                            throw new FormatException("Invalid #include statement.");

                        Load(new StreamReader(line.Substring(p + 1, pEnd - p - 1)));
                        continue;
                    }

                    p = line.IndexOf('=');
                    if (p == -1)
                        continue;

                    name = line.Substring(0, p).Trim();
                    value = line.Substring(p + 1).Trim();

                    if (name == string.Empty)
                        continue;

                    vars[name] = value;
                }
            }
            finally
            {
                reader.Close();
            }
        }

        /// <summary>
        /// Reloads the contents of the environment variables from the global
        /// system settings (on WINFULL) and then from the string passed.
        /// </summary>
        /// <param name="config">The variable settings.</param>
        /// <remarks>
        /// <para>
        /// This method is used by unit tests to initialize the variables from
        /// the string parameter rather than from EnvironmentVars.txt.  The parameter
        /// should be formatted as described for EnvironmentVars.txt.
        /// </para>
        /// <note>
        /// This method will load the global environment variables
        /// if supported by the current platform.
        /// </note>
        /// </remarks>
        internal static void Load(string config)
        {
            vars.Clear();
            LoadGlobal();
            Load(new StringReader(config));
        }

        /// <summary>
        /// Adds a name/value pair to the set of environment variables.
        /// </summary>
        /// <param name="name">The name.</param>
        /// <param name="value">The value</param>
        public static void Add(string name, string value)
        {
            vars.Add(name, value);
        }

        /// <summary>
        /// Returns the value of a specified variable.
        /// </summary>
        /// <param name="name">The variable name.</param>
        /// <returns>The value of the variable (null).</returns>
        public static string Get(string name)
        {
            string      v;
#if !MOBILE_DEVICE
            IPAddress   address;
            IPAddress   subnet;
#endif
            // Handle true operation system environment variables.

            name = name.ToLowerInvariant();

            if (vars.TryGetValue(name, out v))
                return v;

            // Handle LillTek built-in variables.

            switch (name)
            {
                case "systemroot":
#if WINFULL
                    if (vars.TryGetValue(name, out v))
                        return v;

                    return null;
#else
#if MOBILE_DEVICE
                    return null;
#else
                    return OpenNETCF.Environment.SystemDirectory;
#endif
#endif
                case "systemdirectory":
#if WINFULL
                    return System.Environment.SystemDirectory;
#else
#if MOBILE_DEVICE
                    return null;
#else
                    return OpenNETCF.Environment.SystemDirectory;
#endif
#endif
                case "temp":
                case "tmp":

                    if (vars.TryGetValue(name, out v))
                        return v;
#if WINFULL
                    return @"c:\Temp";
#else
                    return @"\Temp";
#endif
                case "azure":

                    return Helper.IsAzure ? "1" : null;

                case "aws":

                    return Helper.IsAWS ? "1" : null;

                case "cloud":

                    return Helper.IsAzure || Helper.IsAWS ? "1" : null;

                case "local-ip":

#if MOBILE_DEVICE
                    return null;
#else
                    // $todo(jeff.lill): Need to implement this for Azure.

                    if (Helper.IsAWS)
                        return Helper.AwsInstanceInfo.LocalAddress.ToString();
                    else
                    {
                        Helper.GetNetworkInfo(out address, out subnet);
                        return address.ToString();
                    }
#endif

                case "public-ip":

#if MOBILE_DEVICE
                    return null;
#else
                    // $todo(jeff.lill): Need to implement this for Azure.

                    if (Helper.IsAWS)
                        return Helper.AwsInstanceInfo.PublicAddress.ToString();
                    else
                    {
                        Helper.GetNetworkInfo(out address, out subnet);
                        return address.ToString();
                    }
#endif

                case "public-hostname":

#if MOBILE_DEVICE
                    return null;
#else
                    // $todo(jeff.lill): Need to implement this for Azure.

                    if (Helper.IsAWS)
                        return Helper.AwsInstanceInfo.PublicHostName;
                    else
                        return string.Empty;
#endif
                case "os":

#if MOBILE_DEVICE
                    if (Helper.IsAndroid)
                        return "Android";
                    else if (Helper.IsAppleIOS)
                        return "Apple iOS";
                    else if (Helper.IsWindowsPhone)
                        return "Windows Phone";
                    else
                        return "Generic Mobile Device";
#else
                    return PlatformName;
#endif

                case "machinename":

#if MOBILE_DEVICE
                    return null;
#else
                    return Helper.MachineName;
#endif

                case "hostname":

#if MOBILE_DEVICE
                    return null;
#else
                    return Dns.GetHostName();
#endif

                case "serverid":

#if MOBILE_DEVICE
                    return null;
#else
                    return ServerID;
#endif

                case "processorcount":

                    return Environment.ProcessorCount.ToString();

                case "ip-address":

#if MOBILE_DEVICE
                    return null;
#else
                    Helper.GetNetworkInfo(out address, out subnet);
                    return address.ToString();
#endif

                case "ip-mask":

#if MOBILE_DEVICE
                    return null;
#else
                    Helper.GetNetworkInfo(out address, out subnet);
                    return subnet.ToString();
#endif

                case "ip-subnet":

#if MOBILE_DEVICE
                    return null;
#else
                    // Count the number of leading ones in the subnet
                    // mask to compute the slash number.

                    int cPrefix;
                    int ip;

                    Helper.GetNetworkInfo(out address, out subnet);
                    ip = Helper.IPAddressToInt32(subnet);

                    cPrefix = 0;
                    for (int i = 0; i < 32; i++)
                    {
                        if ((0x80000000 & ip) == 0)
                            break;

                        cPrefix++;
                        ip <<= 1;
                    }

                    return address.ToString() + "/" + cPrefix.ToString();
#endif

                case "winfull":
#if WINFULL
                    return "1";
#else
                    return null;
#endif
                case "wince":
#if WINCE
                    return "1";
#else
                    return null;
#endif
                case "appversion":

#if MOBILE_DEVICE
                    return null;
#else
                    return Helper.GetVersionString(AppVersion);
#endif

                case "programdatapath":

#if MOBILE_DEVICE
                    return null;
#else
                    return Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData);
#endif

                case "os.winworkstation":

#if MOBILE_DEVICE
                    return "1";
#else
                    if (osVersion.Workstation)
                        return "1";
                    else
                        return null;
#endif

                case "os.winserver":

#if MOBILE_DEVICE
                    return null;
#else
                    if (osVersion.Server)
                        return "1";
                    else
                        return null;
#endif

                case "os.windows":

                    if (Helper.IsWindows)
                        return "1";
                    else
                        return null;

                case "os.unix":

                    if (Helper.IsUnix)
                        return "1";
                    else
                        return null;

                case "mono":

                    if (Helper.IsMono)
                        return "1";
                    else
                        return null;

                case "ismobiledevice":

                    if (Helper.IsMobileDevice)
                        return "1";
                    else
                        return null;

                case "windowsphone":

                    if (Helper.IsWindowsPhone)
                        return "1";
                    else
                        return null;

                case "android":

                    if (Helper.IsAndroid)
                        return "1";
                    else
                        return null;

                case "appleios":

                    if (Helper.IsAppleIOS)
                        return "1";
                    else
                        return null;

                case "apppath":

#if MOBILE_DEVICE
                    return null;
#else
                    return Helper.EntryAssemblyFolder == null ? string.Empty : Helper.EntryAssemblyFolder;
#endif

                case "isdebug":
#if DEBUG
                    return "true";
#else
                    return "false";
#endif
                case "isrelease":
#if DEBUG
                    return "false";
#else
                    return "true";
#endif
                case "guid":

                    return Helper.NewGuid().ToString();

                case "lilltek.dc.cloudep":

                    return Const.DCCloudEP.ToString();

                case "lilltek.dc.cloudgroup":

                    return Const.DCCloudGroup.ToString();

                case "lilltek.dc.cloudport":

                    return Const.DCCloudPort.ToString();

                case "lilltek.dc.rootport":

                    return Const.DCRootPort.ToString();

                case "lilltek.dc.defhubname":

                    return Const.DCDefHubName;

                default:

                    return null;    // Not found
            }
        }

        /// <summary>
        /// Determines whether the name passed is an environment variable.
        /// </summary>
        /// <param name="name">The variable name.</param>
        /// <returns><c>true</c> if the name is an environment variable.</returns>
        public static bool IsVariable(string name)
        {
            return Get(name) != null;
        }

        /// <summary>
        /// Handles the actual recursive expansion of a string.
        /// </summary>
        /// <param name="input">The string containing the variables to expand.</param>
        /// <param name="nesting">The nesting level.</param>
        /// <returns>The input string with exapanded variables.</returns>
        private static string Expand(string input, int nesting)
        {
            if (nesting >= 16)
                throw new StackOverflowException("Too many nested environment variable expansions.");

            StringBuilder   sb;
            int             p, pStart, pEnd;
            string          name;
            string          value;

            // Return right away if there's no macro characters in the string.

            if (input.IndexOfAny(macroChars) == -1)
                return input;

            // Process variables of the form %name%

            sb = new StringBuilder(input.Length + 64);
            p = 0;
            while (true)
            {
                // Scan for the next environment variable name quoted with percent (%) characters.

                pStart = input.IndexOf('%', p);
                if (pStart == -1)
                {
                    // No starting quote so append the rest of the string and exit.

                    sb.Append(input.Substring(p));
                    break;
                }

                pEnd = input.IndexOf('%', pStart + 1);
                if (pEnd == -1)
                {
                    // No terminating quote so append the rest of the string and exit.

                    sb.Append(input.Substring(p));
                    break;
                }

                name = input.Substring(pStart + 1, pEnd - pStart - 1);

                // Append any text from the beginning of this scan up
                // to the start of the variable.

                sb.Append(input.Substring(p, pStart - p));

                // Look up the variable's value.  If found, then recursively 
                // expand and then insert the value.  If not found then 
                // append the variable name without change.

                value = Get(name);
                if (value == null)
                    sb.Append(input.Substring(pStart, pEnd - pStart + 1));
                else
                    sb.Append(Expand(value, nesting + 1));

                // Advance past this definition

                p = pEnd + 1;
            }

            input = sb.ToString();

            // Process variables of the form $(name)

            sb = new StringBuilder(input.Length + 64);
            p = 0;
            while (true)
            {
                // Scan for the next environment variable name quoted with percent $(name) characters.

                pStart = input.IndexOf("$(", p);
                if (pStart == -1)
                {
                    // No starting quote so append the rest of the string and exit.

                    sb.Append(input.Substring(p));
                    break;
                }

                pEnd = input.IndexOf(')', pStart + 1);
                if (pEnd == -1)
                {
                    // No terminating quote so append the rest of the string and exit.

                    sb.Append(input.Substring(p));
                    break;
                }

                name = input.Substring(pStart + 2, pEnd - pStart - 2);

                // Append any text from the beginning of this scan up
                // to the start of the variable.

                sb.Append(input.Substring(p, pStart - p));

                // Look up the variable's value.  If found, then recursively 
                // expand and then insert the value.  If not found then 
                // append the variable name without change.

                value = Get(name);
                if (value == null)
                    sb.Append(input.Substring(pStart, pEnd - pStart + 1));
                else
                    sb.Append(Expand(value, nesting + 1));

                // Advance past this definition

                p = pEnd + 1;
            }

            return sb.ToString();
        }

        /// <summary>
        /// Replaces environment variables in the string passed with the variable
        /// values.
        /// </summary>
        /// <param name="input">The string containing the variables to expand.</param>
        /// <returns>The input string with expanded variables.</returns>
        /// <remarks>
        /// <para>
        /// Looks for environment variables in the string of the form $(name) or %name% 
        /// and replaces them with the corresponding value if there is one.  This works
        /// recursively for up to 16 levels of nesting.  Strings that match these forms
        /// that don't map to an environment variable will remain untouched.
        /// </para>
        /// <note>
        /// Note that the <b>%name%</b> form is depreciated and is implemented only for 
        /// compatibility with older applications.  The <b>$(name)</b> form should be used
        /// for new applications.
        /// </note>
        /// </remarks>
        public static string Expand(string input)
        {
            return Expand(input, 0);
        }

#if !MOBILE_DEVICE

        /// <summary>
        /// Returns a collection of all known environment variables and their
        /// values.
        /// </summary>
        /// <returns>The variable collection.</returns>
        public static IDictionary<string, string> GetAll()
        {
            string[]                    builtIn = new string[] { "SystemRoot", "SystemDirectory", "Temp", "Tmp", "AppPath", "OS" };
            Dictionary<string, string>  dictionary;

            dictionary = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            foreach (string key in vars.Keys)
                dictionary[key] = vars[key];

            foreach (string key in builtIn)
                dictionary[key] = EnvironmentVars.Get(key);

            return dictionary;
        }

        /// <summary>
        /// Returns the value of $(SystemRoot).
        /// </summary>
        public static string SystemRoot
        {
            get
            {
#if WINFULL
                return Get("SystemRoot");
#else
                return OpenNETCF.Environment.SystemDirectory;
#endif
            }
        }

        /// <summary>
        /// Returns the value of $(SystemDirectory).
        /// </summary>
        public static string SystemDirectory
        {
            get
            {
#if WINFULL
                return System.Environment.SystemDirectory;
#else
                return OpenNETCF.Environment.SystemDirectory;
#endif
            }
        }

        /// <summary>
        /// Returns the value of $(TEMP).
        /// </summary>
        public static string TempDirectory
        {
            get { return Get("temp"); }
        }

        /// <summary>
        /// Returns the value of $(OS).
        /// </summary>
        public static string PlatformName
        {
            get
            {
#if WINFULL
                return Environment.OSVersion.Platform.ToString();
#else
                return OpenNETCF.Environment.PlatformName;
#endif
            }
        }

        /// <summary>
        /// Gets an OperatingSystem object that contains the current platform 
        /// identifier and version number. 
        /// </summary>
        public static OperatingSystem OSVersion
        {
            get
            {
#if WINFULL
                return System.Environment.OSVersion;
#else
                return OpenNETCF.Environment.OSVersion;
#endif
            }
        }

        /// <summary>
        /// Gets a <see cref="Version" /> object that describes the major, minor, build, and 
        /// revision numbers of the common language runtime (CLR). 
        /// </summary>
        public static Version Version
        {
            get
            {
#if WINFULL
                return System.Environment.Version;
#else
                return OpenNETCF.Environment.Version;
#endif
            }
        }

        /// <summary>
        /// Returns the <see cref="Version" /> of the current application's entry assembly
        /// as returned by <see cref="Helper.GetEntryAssembly" />.
        /// </summary>
        /// <remarks>
        /// This will return a zeroed version if the entry assembly has not been specified
        /// to a <see cref="Helper" /> class initialization method.
        /// </remarks>
        public static Version AppVersion
        {
            get
            {
                var assembly = Helper.GetEntryAssembly();

                if (assembly == null)
                    return new Version();
                else
                    return Helper.GetVersion(assembly);
            }
        }

        /// <summary>
        /// Returns the path to the global application data folder.
        /// </summary>
        public static string ProgramDataPath
        {
            get { return Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData); }
        }

        /// <summary>
        /// Returns a globally unique identifier.
        /// </summary>
        public static string Guid
        {
            get { return Helper.NewGuid().ToString(); }
        }

        /// <summary>
        /// Returns the computer's name.
        /// </summary>
        public static string MachineName
        {
            get { return Helper.MachineName; }
        }

        /// <summary>
        /// Accesses the machine's server ID as stored in the Windows registry.
        /// </summary>
        /// <remarks>
        /// <para>
        /// The <b>$(ServerID)</b> variable is somewhat special.  The variable is used to specify a globally unique domain
        /// name that can be used to identify and access the current machine.  By default, this variable is the same
        /// as <b>$(HostName)</b>, but if the <b>Server Manager</b> service is running, this will be set to the
        /// <b>ServerID</b> host name loaded from the service manager's configuration file.  This value is persisted
        /// to the Windows registry so it will be available to all applications using the LillTek Platform.
        /// </para>
        /// <para>
        /// The service manager uses the <see cref="ServerID" /> property to persist a value to the registry.
        /// The registry location is:
        /// </para>
        /// <code language="none">
        ///     HKEY_LOCAL_MACHINE\SOFTWARE\LillTek\Platform\Common:ServerID
        /// </code>
        /// <para>
        /// This property returns the current value of the server ID from the registry if
        /// present or the machine's domain name if no value is persisted to the registry.
        /// </para>
        /// <para>
        /// Set the property to a non-<c>null</c> string to persist it to the redistry.  Pass
        /// <c>null</c> to delete any persisted value.
        /// </para>
        /// </remarks>
        public static string ServerID
        {
            get
            {
                string value;

                value = RegKey.GetValue(ServerIDRegPath);
                if (value == null)
                    return Dns.GetHostName();

                return value;
            }

            set
            {
                if (value == null)
                    RegKey.Delete(ServerIDRegPath);
                else
                    RegKey.SetValue(ServerIDRegPath, value);
            }
        }

#endif // !MOBILE_DEVICE
    }
}
