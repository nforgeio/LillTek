//-----------------------------------------------------------------------------
// FILE:        Helper.cs
// CONTRIBUTOR: Jeff Lill
// COPYRIGHT:   Copyright (c) 2005-2015 by Jeffrey Lill.  All rights reserved.
// DESCRIPTION: Misc utilities.

using System;
using System.ComponentModel;

#if WINFULL
using System.Drawing;
#endif

using System.IO;
using System.Xml;
using System.Net;

#if !SILVERLIGHT
using System.Net.Cache;
#endif

using System.Net.Sockets;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using System.Reflection;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.Diagnostics.Contracts;
using System.Globalization;
using System.Runtime.Serialization;
using System.Runtime.InteropServices;

#if !SILVERLIGHT
using System.IO.Compression;
using System.Net.NetworkInformation;
using System.Runtime.Serialization.Formatters.Binary;
#endif

#if !MOBILE_DEVICE
using LillTek.Windows;
#endif

namespace LillTek.Common
{
    /// <summary>
    /// Used by <see cref="Helper.SetLocalGuidMode" /> to specify the
    /// unit testing GUID generation mode.
    /// </summary>
    internal enum GuidMode {

        /// <summary>
        /// Generate a standard (real) GUID.
        /// </summary>
        Normal,

        /// <summary>
        /// Generate process local GUIDs starting at 0 and counting up.
        /// </summary>
        CountUp,

        /// <summary>
        /// Generate process local GUIDs starting at <see cref="int.MaxValue" /> and counting down.
        /// </summary>
        CountDown
    }

    /// <summary>
    /// Implements various low-level helper functions.
    /// </summary>
    /// <remarks>
    /// <note>
    /// Most applications will need to call one of <see cref="InitializeApp" /> or <see cref="InitializeWebApp" />
    /// and one of <see cref="Config.SetConfigPath(string)" /> or <see cref="Config.SetConfigPath(Assembly)" />
    /// early in the application startup process to ensure that other common classes such as <see cref="Config" /> 
    /// will be able to work properly.
    /// </note>
    /// </remarks>
    public static partial class Helper
    {
        //---------------------------------------------------------------------
        // Implementation

        private static object syncLock = new object();

#if !WINFULL
        // $hack(jeff.lill): 
        //
        // Silverlight, iOS and probably other devices don't support ANSI or ASCII 
		// encodings but I have implemented my own ASCII encoding, which we'll use instead.

        private static Encoding         ansiEncoding        = new ASCIIEncoding();
        private static Encoding         asciiEncoding       = new ASCIIEncoding();
#else
        private static Encoding         ansiEncoding        = Encoding.GetEncoding(1252);
        private static Encoding         asciiEncoding       = Encoding.ASCII;
#endif
        private static byte[]           utf8Preamble        = Encoding.UTF8.GetPreamble();

        private static Assembly         entryAssembly       = null;      // Assembly holding the application entry point
        private static string           exeFolder           = null;      // Path to the entry assembly's home directory (no terminating slash)
        private static string           exeFile             = null;      // The entry assembly's unqualified file name
        private static bool             isInitialized       = false;     // True if Init() or InitWeb() have already been called
        private static bool             isWebApp            = false;     // True if initialized as an ASP.NET application
        private static Random           rand                = null;
        private static char[]           csvEscapes          = new char[] { '\r', '\n', ',' };
        private static char[]           whitespace          = new char[] { ' ', '\t', '\r', '\n' };
        private static char[]           uriEscapes          = new char[] { '%', '+' };
        private static char[]           pathSep             = new char[] { '\\', '/' };
        private static char[]           fileWildcards       = new char[] { '*', '?' };
        private static char[]           crlfArray           = new char[] { '\r', '\n' };
        private static char             pathSepChar         = Path.DirectorySeparatorChar;
        private static string           pathSepString       = new String(pathSepChar, 1);
        private static Action<Action>   uiActionDispatcher  = null;      // Handles dispatching actions to the UI thread
#if !MOBILE_DEVICE
        private static GatedTimer       bkTimer             = null;      // Background activity timer
#endif
        private static RecurringTimer   gcTimer             = null;      // Timer for forcing heap garbage collection

        // These variables are used to detect information about the host environment.

        private static bool             monoCheck           = false;     // True if we've already checked for the Mono runtime
        private static bool             isMono              = false;     // True if we're running on Mono
        private static bool             osChecked           = false;     // True if we've already checked for the host OS
        private static bool             isWindows           = false;     // True if we're running on a Windows variant
        private static bool             isWindowsPhone      = false;     // True if we're running on a Windows Phone
        private static bool             isUnix              = false;     // True if we're running on a Unix/Linux variant
        private static bool             isMacOSX            = false;     // True if we're running on Mac/OSX
        private static bool             isMobileDevice      = false;     // True if we're running on a mobile device
        private static bool             isAndroid           = false;     // True if we're running on a Google Android device
        private static bool             isAppleIOS          = false;     // True if we're running on an Applie iOS device
        private static bool             isAzure             = false;     // True if we're running in the Azure cloud
        private static bool             isAWS               = false;     // True if we're running in the AWS cloud

        // Used by the Helper.EnqueueSerializedAction(...) methods.

        private static SerializedActionQueue globalSerializedActionQueue = new SerializedActionQueue();

#if WINFULL
        // Used by ParseColor()

        private static Dictionary<string, Color> colorNames;
#endif

#if !MOBILE_DEVICE
        private static AwsInstanceInfo awsInstanceInfo = null;
#endif

        // These members are used to implement proccess local GUIDs for unit testing.

        private static int              processGuid     = 0;
        private static GuidMode         guidMode        = GuidMode.Normal;

        /// <summary>
        /// Ordinal value of an ASCII carriage return.
        /// </summary>
        public const int CR = 0x0D;

        /// <summary>
        /// Ordinal value of an ASCII linefeed.
        /// </summary>
        public const int LF = 0x0A;

        /// <summary>
        /// Ordinal value of an ASCII horizontal TAB.
        /// </summary>
        public const int HT = 0x09;

        /// <summary>
        /// Ordinal value of an ASCII escape character.
        /// </summary>
        public const int ESC = 0x1B;

        /// <summary>
        /// Ordinal value of an ASCII TAB character.
        /// </summary>
        public const int TAB = 0x09;

        /// <summary>
        /// Returns the character to be substitied when displaying text
        /// in password text boxes.
        /// </summary>
        public const char PasswordChar = (char)0x25CF;   // Black circle

        /// <summary>
        /// A string consisting of a CRLF sequence.
        /// </summary>
        public const string CRLF = "\r\n";

        /// <summary>
        /// Regular expression used to validate URI fragments.
        /// </summary>
        public const string UriFragmentRegex = @"^[a-zA-Z\d-]+$";

        /// <summary>
        /// IPv4 address validation regular expression.
        /// </summary>
        public const string IP4AddressRegex = @"^(([01]?\d\d?|2[0-4]\d|25[0-5])\.){3}([01]?\d\d?|25[0-5]|2[0-4]\d)$";

        /// <summary>
        /// Returns the character used to separate segments of a file system 
        /// path for the current platform.
        /// </summary>
        public static char PathSepChar
        {
            get { return pathSepChar; }
        }

        /// <summary>
        /// Returns a string with the character used to separate segments of a file system 
        /// path for the current platform.
        /// </summary>
        public static string PathSepString
        {
            get { return pathSepString; }
        }

        /// <summary>
        /// Returns the characters used as wildcards for the current file system.
        /// </summary>
        public static char[] FileWildcards
        {
            get { return fileWildcards; }
        }

        /// <summary>
        /// The minimum valid SQL Server date.
        /// </summary>
        public static readonly DateTime SqlMinDate = new DateTime(1753, 1, 1);

        /// <summary>
        /// The maximum possible SQL Server date.
        /// </summary>
        public static readonly DateTime SqlMaxDate = new DateTime(9999, 12, 31, 11, 59, 59);

        /// <summary>
        /// This method should be called when the application initially starts to
        /// fully initialize the LillTek Platform.
        /// </summary>
        /// <param name="entryAssembly">The assembly containing the application's entry point.</param>
        public static void InitializeApp(Assembly entryAssembly)
        {
            if (isInitialized)
                return;     // Already initialized.

            if (entryAssembly == null)
                throw new ArgumentNullException("entryAssembly");

            Helper.entryAssembly = entryAssembly;

            // Get the path to the directory hosting the assembly by
            // stripping off the file URI scheme if present and the
            // assembly's file name and trailing slash.
            //
            // Note that this doesn't work for Windows Phone applications.

#if !WINDOWS_PHONE

            int pos;

            Helper.exeFolder = Helper.StripFileScheme(entryAssembly.CodeBase);

            pos = exeFolder.LastIndexOfAny(new char[] { '/', '\\' });
            if (pos == -1)
                throw new InvalidOperationException("Helper.Init() works only for assemblies loaded from disk.");

            Helper.exeFile   = exeFolder.Substring(pos + 1);
            Helper.exeFolder = exeFolder.Substring(0, pos);

#endif

            Helper.isWebApp      = false;
            Helper.isInitialized = true;

            DetectOS();
        }

        /// <summary>
        /// Use this method when an ASP.NET application is started.
        /// </summary>
        /// <param name="entryAssembly">The assembly containing the application's entry point.</param>
        /// <param name="rootPath">The fully qualified physical path to the application root folder.</param>
        /// <param name="aspAppName">The ASP.NET application name.</param>
        /// <remarks>
        /// <para>
        /// This method initializes the <see cref="EntryAssemblyFolder" /> property to the
        /// rootPath value passed.  Most applications will pass the application's physical
        /// root path as returned by <b>HostingEnvironment.ApplicationPhysicalPath</b>.
        /// Classes such as <see cref="Config" /> will use this path to locate important
        /// files such as the application's "Web.ini" file.  <see cref="Helper.GetEntryAssembly" /> will
        /// be initialized to the calling assembly, <see cref="EntryAssemblyFile" /> will be 
        /// initialized to <paramref name="aspAppName" /> and <see cref="IsWebApp" /> will be initialized to true.
        /// </para>
        /// <para>
        /// A typical ASP.NET implementation will look something like:
        /// </para>
        /// <code language="cs">
        /// void Application_Start(object sender,EventArgs args) {
        ///
        ///     LillTek.Web.WebHelper.PlatformInitialize(Assembly.GetExecutingAssembly());
        /// 
        ///     // Additional global initialization
        /// }
        ///
        /// void Application_End(object sender,EventArgs args) {
        ///
        ///     // Additional global termination
        /// 
        ///     LillTek.Web.WebHelper.PlatformTerminate();
        /// }
        /// </code>
        /// </remarks>
        public static void InitializeWebApp(Assembly entryAssembly, string rootPath, string aspAppName)
        {
            if (isInitialized)
                return;     // Already initialized.

#if WINDOWS_PHONE
            throw new NotImplementedException();
#else
            Helper.entryAssembly = entryAssembly;
            Helper.exeFolder     = Helper.StripTrailingSlash(rootPath);
            Helper.exeFile       = aspAppName;
            Helper.isWebApp      = true;
            Helper.isInitialized = true;

            DetectOS();
#endif
        }

        /// <summary>
        /// Called by the <see cref="Config" /> class after the global configuration
        /// system has been properly initialized.
        /// </summary>
        internal static void LoadGlobalConfig()
        {
#if !MOBILE_DEVICE

            // Load global configuration settings.

            var config = new Config("LillTek");

            if (bkTimer != null)
            {
                bkTimer.Dispose();
                bkTimer = null;
            }

            bkTimer = new GatedTimer(OnBkTask, null, config.Get("BkTaskInterval", TimeSpan.FromSeconds(15)));
            gcTimer = config.GetCustom<RecurringTimer>("GCTimer", RecurringTimer.Disabled);

#endif // !MOBILE_DEVICE
        }

        /// <summary>
        /// Handles global LillTek background activities.
        /// </summary>
        /// <param name="state">Not used.</param>
        private static void OnBkTask(object state)
        {
            try
            {
                if (gcTimer.HasFired(DateTime.UtcNow))
                    GC.Collect();
            }
            catch (Exception e)
            {
                SysLog.LogException(e);
            }
        }

        /// <summary>
        /// Returns <c>true</c> if <see cref="InitializeApp" /> or <see cref="InitializeWebApp" /> has been called
        /// for the current application domain.
        /// </summary>
        public static bool IsInitialized
        {
            get { return isInitialized; }
        }

        /// <summary>
        /// Returns <c>true</c> if <see cref="InitializeWebApp" /> was called, indicating that the
        /// current application is running as an ASP.NET application.
        /// </summary>
        public static bool IsWebApp
        {
            get { return isWebApp; }
        }

        /// <summary>
        /// Determines whether the code is being run in a design tool.
        /// </summary>
        public static bool IsInDesignTool
        {
            get
            {
#if SILVERLIGHT
                return DesignerProperties.IsInDesignTool;
#else
                return false;
#endif
            }
        }

        /// <summary>
        /// Returns <c>true</c> if the current application is running on Mono.
        /// </summary>
        public static bool IsMono
        {
            get
            {
                // Note that I'm deferring the reflection for the mono runtime
                // until this property as actually called for the first time.

                if (monoCheck)
                    return isMono;

                isMono = typeof(String).Assembly.GetType("Mono.Runtime") != null;
                monoCheck = true;

                return isMono;
            }
        }

        /// <summary>
        /// Detects the current operating system.
        /// </summary>
        private static void DetectOS()
        {
            if (osChecked)
                return;     // Already did a detect

            // Special-case Apple iOS and Google Android.

#if APPLE_IOS
            isAppleIOS     = true;
            isMono         = true;
            osChecked      = true;
            monoCheck      = true;
            isMobileDevice = true;

            return;
#endif

#if ANDROID
            isAndroid      = true;
            isMono         = true;
            osChecked      = true;
            monoCheck      = true;
            isMobileDevice = true;

            return;
#endif

#if !APPLE_IOS && !ANDROID

            // Examine the environment globals for other platforms.

            try
            {
                // Detect the base operating system.

                switch ((int)Environment.OSVersion.Platform)
                {
                    case 4:
                    case 128:

                        isWindows = false;
                        isUnix    = true;
                        isMacOSX  = false;
                        break;

#if !WINDOWS_PHONE
                    case (int)PlatformID.MacOSX:

                        isWindows = false;
                        isUnix    = false;
                        isMacOSX  = true;
                        break;
#endif

                    default:

#if WINDOWS_PHONE
                        isWindowsPhone = true;
                        isMobileDevice = true;
#else
                        isWindows = true;
#endif
                        isUnix    = false;
                        isMacOSX  = false;
                        break;
                }

#if SILVERLIGHT
                isAzure = false;
                isAWS   = false;
#else
                // Determine whether we're running in the AWS cloud or not.  This check
                // depends on the AWS AMI being configured to set the environment variable
                //
                //      AWS=1
                //
                // as described in the AWS AMI configuration document.

                if (Environment.GetEnvironmentVariable("AWS") == "1")
                    isAWS = true;
                else
                {
                    // Determine whether we're running in the Azure cloud or not.  We're going to look for 
                    // a reference to the Microsoft.WindowsAzure.ServiceRuntime assembly in the current 
                    // application's AppDomain references.  If this is not found then we can't possibly 
                    // be running on Azure.  If the assembly is found, then examine the RoleManager.IsRoleManagerRunning 
                    // property to determine whether we're running on Azure (Service or Development fabric).  The code 
                    // will set the AZURE environment variable if we detected Azure.

                    isAzure = false;

                    foreach (var assembly in AppDomain.CurrentDomain.GetAssemblies())
                    {
                        if (assembly.FullName.StartsWith("Microsoft.WindowsAzure.ServiceRuntime"))
                        {
                            Type            roleManagerType;
                            PropertyInfo    propertyInfo;
                            MethodInfo      getMethodInfo;

                            roleManagerType = assembly.GetType("Microsoft.WindowsAzure.ServiceRuntime.RoleManager");
                            if (roleManagerType == null)
                                break;

                            propertyInfo  = roleManagerType.GetProperty("IsRoleManagerRunning");
                            getMethodInfo = propertyInfo.GetGetMethod();

                            isAzure = (bool)getMethodInfo.Invoke(null, null);
                            break;
                        }
                    }
                }
#endif // SILVERLIGHT

                if (isAzure)
                {
                    isWindows = true;
                    isUnix = false;
                    isMacOSX = false;
#if !SILVERLIGHT
                    Environment.SetEnvironmentVariable("AZURE", "1");
#endif
                }
            }

            finally
            {
                // Set the global to true so we won't test anymore.

                osChecked = true;
            }
#endif
        }

        /// <summary>
        /// Returns <c>true</c> if the application is running on a Windows variant
        /// operating system.
        /// </summary>
        public static bool IsWindows
        {
            get
            {
                if (osChecked)
                    return isWindows;

                DetectOS();
                return isWindows;
            }
        }

        /// <summary>
        /// Returns <c>true</c> if the application is running on a mobile device.
        /// </summary>
        public static bool IsMobileDevice
        {
            get
            {
                if (osChecked)
                    return isMobileDevice;

                DetectOS();
                return isMobileDevice;
            }
        }

        /// <summary>
        /// Returns <c>true</c> if the application is running on a Window Phone.
        /// </summary>
        public static bool IsWindowsPhone
        {
            get
            {
                if (osChecked)
                    return isWindowsPhone;

                DetectOS();
                return isWindowsPhone;
            }
        }

        /// <summary>
        /// Returns <c>true</c> if the application is running on a Google Android device.
        /// </summary>
        public static bool IsAndroid
        {
            get { return isAndroid; }
        }

        /// <summary>
        /// Returns <c>true</c> if the application is running on an Apple iOS device.
        /// </summary>
        public static bool IsAppleIOS
        {
            get { return isAppleIOS; }
        }

        /// <summary>
        /// Returns <c>true</c> if the application is running on the Windows Azure production
        /// or development hosting environment.
        /// </summary>
        public static bool IsAzure
        {
            get
            {
                if (osChecked)
                    return isAzure;

                DetectOS();
                return isAzure;
            }
        }

        /// <summary>
        /// Returns <c>true</c> if the application is running in the Amazon Web Services (AWS) cloud.
        /// </summary>
        /// <remarks>
        /// <note>
        /// This property depends on the environment variable <b>AWS=1</b> being set propertly
        /// on the Amazon Machine Image (AMI) as described in the AMI Configuration Instructions.
        /// </note>
        /// </remarks>
        public static bool IsAWS
        {
            get
            {
                if (osChecked)
                    return isAWS;

                DetectOS();
                return isAWS;
            }
        }

        /// <summary>
        /// Returns <c>true</c> if the application is running on a cloud platform such as
        /// Windows Azure or Amazon Web Services.
        /// </summary>
        /// <remarks>
        /// <note>
        /// This property depends on the environment variable <b>AWS=1</b> being set propertly
        /// on the Amazon Machine Image (AMI) as described in the AMI Configuration Instructions.
        /// </note>
        /// </remarks>
        public static bool IsCloud
        {
            get { return isAWS || isAzure; }
        }

        /// <summary>
        /// Returns <c>true</c> if the application is running on a Unix/Linux variant
        /// operating system.
        /// </summary>
        public static bool IsUnix
        {
            get
            {
                if (osChecked)
                    return isUnix;

                DetectOS();
                return isUnix;
            }
        }

        /// <summary>
        /// Returns <c>true</c> if the application is running on a Mac/OSX operating system.
        /// </summary>
        public static bool IsMacOSX
        {
            get
            {
                if (osChecked)
                    return isMacOSX;

                DetectOS();
                return isMacOSX;
            }
        }

        /// <summary>
        /// Returns if the current computer is configured to be a development/test machine,
        /// as opposed to being a production machine.  Development/test machines are identified
        /// by determining if the <b>DEV_WORKSTATION</b> environment variable exists.
        /// </summary>
        public static bool IsDevWorkstation
        {
            get { return EnvironmentVars.Get("DEV_WORKSTATION") != null; }
        }

        /// <summary>
        /// Returns if the current computer is <b>not</b> configured to be a development/test machine,
        /// and should be assumed to production machine.  Development/test machines are identified
        /// by determining if the <b>DEV_WORKSTATION</b> environment variable does not exist.
        /// </summary>
        public static bool IsProductionEnvironment
        {
            get { return EnvironmentVars.Get("DEV_WORKSTATION") == null; }
        }

        /// <summary>
        /// This method is a hack to prevent the C# compiler from generating in-line
        /// code for calls to very simple methods that deal with <i>missing method</i>
        /// problems when running on Mono.  This method is essentially a NOP but
        /// generates enough object code to prevent the inline code generation.
        /// </summary>
        public static void MonoInlineHack()
        {
            if (!osChecked)
                DetectOS();
        }

#if !MOBILE_DEVICE

        /// <summary>
        /// Returns metadata for the AWS instance hosting the application.
        /// </summary>
        /// <exception cref="InvalidOperationException">Thrown if the machine is not hosted in the AWS cloud.</exception>
        public static AwsInstanceInfo AwsInstanceInfo
        {
            get
            {
                if (!osChecked)
                    DetectOS();

                if (!isAWS)
                    throw new InvalidOperationException("Cannot load AWS metadata when the application is not hosted by the AWS cloud.");

                if (awsInstanceInfo != null)
                    return awsInstanceInfo;

                awsInstanceInfo = new AwsInstanceInfo();
                return awsInstanceInfo;
            }
        }

#endif

        /// <summary>
        /// Returns the assembly containing the application's entry point.  The method returns 
        /// <c>null</c> if the entry assembly was never specified by a call to <see cref="Helper.InitializeApp" />
        /// </summary>
        /// <remarks>
        /// <note>
        /// This method requires that <see cref="Helper.InitializeApp" /> be called first.
        /// </note>
        /// </remarks>
        public static Assembly GetEntryAssembly()
        {
            return entryAssembly;
        }

        /// <summary>
        /// Returns the fully qualified path to the directory holding the assembly
        /// containing the application's entry point.  The method returns <c>null</c> if
        /// the entry assembly was never specified by a call to <see cref="Helper.InitializeApp" />
        /// or <see cref="Helper.InitializeWebApp" />.
        /// </summary>
        /// <remarks>
        /// <para>
        /// For web applications, this property returns the physical path to the root
        /// folder of the website.  For all other applications, the path to the folder
        /// with the entry assembly DLL is returned.
        /// </para>
        /// <note>
        /// Note that the path returned will not include a terminating back or 
        /// forward slash.
        /// </note>
        /// </remarks>
        public static string AppFolder
        {
            get { return EntryAssemblyFolder; }
        }

        /// <summary>
        /// Returns the fully qualified path to the directory holding the assembly
        /// containing the application's entry point.  The method returns <c>null</c> if
        /// the entry assembly was never specified by a call to <see cref="Helper.InitializeApp" />
        /// or <see cref="Helper.InitializeWebApp" />.
        /// </summary>
        /// <remarks>
        /// <para>
        /// For web applications, this property returns the physical path to the root
        /// folder of the website.  For all other applications, the path to the folder
        /// with the entry assembly DLL is returned.
        /// </para>
        /// <note>
        /// Note that the path returned will not include a terminating back or 
        /// forward slash.
        /// </note>
        /// </remarks>
        public static string EntryAssemblyFolder
        {
            get
            {
                if (entryAssembly == null)
                    return null;

                return exeFolder;
            }
        }

        /// <summary>
        /// Returns the unqualified name of the entry assembly file.  The method returns <c>null</c> if
        /// the entry assembly was never specified by a call to <see cref="Helper.InitializeApp" />.
        /// </summary>
        public static string EntryAssemblyFile
        {
            get
            {
                if (entryAssembly == null)
                    return null;

                return exeFile;
            }
        }

#if !WINDOWS_PHONE

        /// <summary>
        /// Returns the fully qualified path to the folder holding the
        /// assembly passed (includes the terminating "\").
        /// </summary>
        /// <param name="assembly">The assembly.</param>
        /// <returns>Path to the folder holding the assembly.</returns>
        public static string GetAssemblyFolder(Assembly assembly)
        {
            // Get the path to the directory hosting the assembly by
            // stripping off the file URI scheme if present and the
            // assembly's file name.

            string  path;
            int     pos;

            path = Helper.StripFileScheme(assembly.CodeBase);

            pos = path.LastIndexOfAny(new char[] { '/', '\\' });
            if (pos == -1)
                throw new InvalidOperationException("Helper.GetAssemblyFolder() works only for assemblies loaded from disk.");

            return path.Substring(0, pos + 1);
        }

        /// <summary>
        /// Returns the fully qualified path to the assembly file.
        /// </summary>
        /// <param name="assembly">The assembly.</param>
        /// <returns>The assembly's path.</returns>
        public static string GetAssemblyPath(Assembly assembly)
        {
            // Get the path to the directory hosting the assembly by
            // stripping off the file URI scheme if present and the
            // assembly's file name.

            return Helper.StripFileScheme(assembly.CodeBase);
        }

#endif

        /// <summary>
        /// Returns the fully qualified path to an embedded resource within an
        /// assembly.
        /// </summary>
        /// <param name="assembly">The host assembly.</param>
        /// <param name="path">The relative path to the resource (may include forward and back slashes).</param>
        /// <returns>The fullry qualified path.</returns>
        /// <remarks>
        /// <para>
        /// Embedded resource path names are a bit strange in .NET.  The syntax is:
        /// </para>
        /// <code language="none">
        /// {assembly name} "." {relative path}
        /// </code>
        /// <para>
        /// where <i>assembly name</i> is the name of the assembly file (without the extension)
        /// and relative path is the local path to the resource with any forward and back slashes
        /// replaced with periods.
        /// </para>
        /// <para>
        /// The return value is suitable for passing to methods such as 
        /// <see cref="Assembly.GetManifestResourceStream(System.Type,string)" />.
        /// </para>
        /// </remarks>
        public static string GetEmbeddedResourcePath(Assembly assembly, string path)
        {
            path = path.Replace('/', '.');
            path = path.Replace('\\', '.');

            string  name;
            int     pos;

            name = assembly.FullName;   //Extract the assembly name
            pos  = name.IndexOf(',');
            name = name.Substring(0, pos);

            return name + "." + path;
        }

        /// <summary>
        /// Returns a stream for reading a resource embedded in an assembly.
        /// </summary>
        /// <param name="assembly">The host assembly.</param>
        /// <param name="path">The relative path to the resource (may include forward and back slashes).</param>
        /// <returns>The open <see cref="Stream" /> or <c>null</c>.</returns>
        public static Stream GetEmbeddedResourceStream(Assembly assembly, string path)
        {
            return assembly.GetManifestResourceStream(GetEmbeddedResourcePath(assembly, path));
        }

        /// <summary>
        /// Strips the uri scheme (if there is one) from the uri passed
        /// and returns it.
        /// </summary>
        /// <param name="uri">The uri to strip.</param>
        public static string RemoveUriScheme(string uri)
        {
            int pos;

            pos = uri.IndexOf(':');
            if (pos == -1)
                return uri;
            else
                return uri.Substring(pos + 1);
        }

        /// <summary>
        /// Generates a URI fragment from the string passed.
        /// </summary>
        /// <param name="value">The input string.</param>
        /// <returns>The generated URI fragment.</returns>
        /// <exception cref="ArgumentNullException">Thrown if <c>null</c> is passed.</exception>
        /// <exception cref="ArgumentException">Thrown if the input string generates an empty fragment.</exception>
        /// <remarks>
        /// <para>
        /// A URI fragment is text that can appear without escaping as a segment within a URI.
        /// This method strips works by replacing any embedded whitespace or punctuation sequences 
        /// with a dash (-) and then stripping out any non-alphanumeric characters from the input string.
        /// </para>
        /// <para>
        /// This method is designed to be used to generate SEO friendly URIs from strings
        /// representing article titles, person names, etc.
        /// </para>
        /// <note>
        /// This method recognizes only ASCII letters; extended letters will be stripped from
        /// the output string.  Note also that apostrophes will always be removed even though
        /// they are considered to be punctuation characters.
        /// </note>
        /// </remarks>
        public static string GetUriFragment(string value)
        {
            if (value == null)
                throw new ArgumentNullException("value");

            StringBuilder   sb;
            string          result;
            bool            lastIsDash;

            // Replace any punctuation with spaces.

            sb = new StringBuilder(value.Length);

            foreach (var ch in value)
            {
                if (ch == '\'')
                    continue;       // Ignore apostrophes to keep the word together.
                else if (Char.IsPunctuation(ch))
                    sb.Append(' ');
                else
                    sb.Append(ch);
            }

            value = sb.ToString().Trim();

            // Replace runs of whitespace with dashes and weed out any invalid characters.

            sb = new StringBuilder(value.Length);
            value = value.Trim();
            lastIsDash = false;

            foreach (var ch in value)
            {
                if (Char.IsWhiteSpace(ch))
                {
                    if (lastIsDash)
                        continue;

                    sb.Append('-');
                    lastIsDash = true;
                    continue;
                }

                if (Char.IsNumber(ch) || ('a' <= ch && ch <= 'z') || ('A' <= ch && ch <= 'Z'))
                {
                    lastIsDash = false;
                    sb.Append(ch);
                }
            }

            result = sb.ToString();
            if (result.Length == 0)
                throw new ArgumentException("Empty URI fragment generated.");

            return result;
        }

        /// <summary>
        /// Extracts bytes from a buffer.
        /// </summary>
        /// <param name="buf">The source buffer.</param>
        /// <param name="index">Index of the first byte to be extracted.</param>
        /// <param name="length">Number of bytes to extract.</param>
        /// <returns>A new array with the extracted bytes.</returns>
        public static byte[] Extract(byte[] buf, int index, int length)
        {
            byte[] result;

            result = new byte[length];
            Array.Copy(buf, index, result, 0, length);

            return result;
        }

        /// <summary>
        /// Extracts bytes from the specified position in a buffer to
        /// the end of that buffer.
        /// </summary>
        /// <param name="buf">The source buffer.</param>
        /// <param name="index">Index of the first byte to be extracted.</param>
        /// <returns>A new array with the extracted bytes.</returns>
        public static byte[] Extract(byte[] buf, int index)
        {
            return Helper.Extract(buf, index, buf.Length - index);
        }

        /// <summary>
        /// Writes data into a buffer.
        /// </summary>
        /// <param name="buf">The output buffer.</param>
        /// <param name="index">
        /// Index where the first byte is to be written.  Will
        /// return indexing the first byte after the data
        /// written.
        /// </param>
        /// <param name="data">The data to write.</param>
        public static void Insert(byte[] buf, ref int index, byte[] data)
        {
            for (int i = 0; i < data.Length; i++)
                buf[index + i] = data[i];

            index += data.Length;
        }

        /// <summary>
        /// Fills the buffer with up to 4 bytes of data from the value passed.
        /// </summary>
        /// <param name="buf">The buffer.</param>
        /// <param name="value">The value.</param>
        /// <remarks>
        /// The least significant bits of the value are copied first.  This
        /// routine is designed for filling a buffer with a seed value.
        /// </remarks>
        public static void Fill32(byte[] buf, int value)
        {
            int cb;

            if (buf.Length >= 4)
                cb = 4;
            else
                cb = buf.Length;

            for (int i = 0; i < cb; i++)
            {
                buf[i] = (byte)(value & 0x00FF);
                value = value >> 8;
            }
        }

        /// <summary>
        /// Fills the buffer with up to 8 bytes of data from the value passed.
        /// </summary>
        /// <param name="buf">The buffer.</param>
        /// <param name="value">The value.</param>
        /// <remarks>
        /// The least significant bits of the value are copied first.  This
        /// routine is designed for filling a buffer with a seed value.
        /// </remarks>
        public static void Fill64(byte[] buf, long value)
        {
            int cb;

            if (buf.Length >= 8)
                cb = 8;
            else
                cb = buf.Length;

            for (int i = 0; i < cb; i++)
            {
                buf[i] = (byte)(value & 0x00FF);
                value = value >> 8;
            }
        }

        /// <summary>
        /// Returns the absolute value of the parameter passed taking special
        /// care to handle <b>int.MinValue</b> by returning <b>int.MaxValue</b>.
        /// </summary>
        /// <param name="v">The value to be processed.</param>
        /// <returns>The absolute value of the parameter.</returns>
        /// <remarks>
        /// The issue here is that in twos complement integer encoding, there
        /// is no valid value for Abs(int.MinValue).  The <see cref="Math.Abs(int)" />
        /// method throws an exception in this case.  This method returns
        /// <b>int.MaxValue instead.</b>
        /// </remarks>
        public static int AbsClip(int v)
        {
            if (v == int.MinValue)
                return int.MaxValue;

            return Math.Abs(v);
        }

        /// <summary>
        /// Returns the absolute value of the parameter passed taking special
        /// care to handle <b>short.MinValue</b> by returning <b>short.MaxValue</b>.
        /// </summary>
        /// <param name="v">The value to be processed.</param>
        /// <returns>The absolute value of the parameter.</returns>
        /// <remarks>
        /// The issue here is that in twos complement integer encoding, there
        /// is no valid value for Abs(int.MinValue).  The <see cref="Math.Abs(short)" />
        /// method throws an exception in this case.  This method returns
        /// <b>short.MaxValue instead.</b>
        /// </remarks>
        public static short AbsClip(short v)
        {
            if (v == short.MinValue)
                return short.MaxValue;

            return Math.Abs(v);
        }

        /// <summary>
        /// Maps an integer hash code into an array element index, taking
        /// into account that the hash code might be negative and also
        /// that twos-complement integers are not symetric around 0.
        /// </summary>
        /// <param name="count">The number of array elements.</param>
        /// <param name="hash">The hash code.</param>
        /// <returns>The hashed array element index.</returns>
        /// <remarks>
        /// <para>
        /// Many folks implement hash lookups using code that looks
        /// something like:
        /// </para>
        /// <code language="cs">
        ///     array[Math.Abs(hash) % array.Length];
        /// </code>
        /// <para>
        /// The problem with this is that Math.Abs(int.MinValue) will
        /// throw an OverflowException because int.MaxValue does not
        /// have a corresponding positive value due to how twos-complement
        /// integers are encoded.
        /// </para>
        /// <para>
        /// This method handles this complexity by mapping int.MinValue
        /// to 0 and also by handling the absolute value conversion.
        /// </para>
        /// </remarks>
        /// <exception cref="IndexOutOfRangeException">Thrown if length is &lt;= 0.</exception>
        public static int HashToIndex(int count, int hash)
        {
            if (count <= 0)
                throw new IndexOutOfRangeException();

            if (hash == int.MinValue)
                return 0;

            return Math.Abs(hash) % count;
        }

        /// <summary>
        /// Parses a list of values separated by a specified character from a string
        /// and returns the values in a string array.
        /// </summary>
        /// <param name="input">The input string.</param>
        /// <param name="separator">The separator character.</param>
        /// <returns>The array of values.</returns>
        /// <remarks>
        /// <note>
        /// The method will trim whitespace from the beginning and end
        /// of each value returned and also that the method will tolerate an extra
        /// separator character at the end of the input string.
        /// </note>
        /// </remarks>
        public static string[] ParseStringList(string input, char separator)
        {
            int         pos, posEnd;
            int         count;
            string[]    values;

            // Normalize the string by trimming any whitespace and removing
            // any extra separator character at the end of the string.

            input = input.Trim();
            if (input.Length == 0)
                return new string[0];

            if (input[input.Length - 1] == separator)
                input = input.Substring(0, input.Length - 1).Trim();

            if (input.Length == 0)
                return new string[0];

            // Count the number of values in the string.

            count = 1;
            for (int i = 0; i < input.Length; i++)
                if (input[i] == separator)
                    count++;

            // Allocate the result and extract the values

            values = new string[count];
            pos = 0;

            for (int i = 0; i < count; i++)
            {
                posEnd = input.IndexOf(separator, pos);
                if (posEnd == -1)
                {
                    values[i] = input.Substring(pos).Trim();
                    Assertion.Test(i == count - 1);
                }
                else
                {
                    values[i] = input.Substring(pos, posEnd - pos).Trim();
                    pos = posEnd + 1;
                }
            }

            return values;
        }

        /// <summary>
        /// Converts the byte buffer passed into a hex encoded string.
        /// </summary>
        /// <param name="buf">The buffer</param>
        /// <returns>The hex encoded string.</returns>
        /// <remarks>
        /// Note that the HEX digits A-F will be rendered in lowercase.
        /// </remarks>
        public static string ToHex(byte[] buf)
        {
            var sb = new StringBuilder(buf.Length * 2);

            for (int i = 0; i < buf.Length; i++)
            {
                int     v = buf[i];
                int     digit;
                char    ch;

                digit = v >> 4;
                if (digit < 10)
                    ch = Convert.ToChar('0' + digit);
                else
                    ch = Convert.ToChar('a' + digit - 10);

                sb.Append(ch);

                digit = v & 0x0F;
                if (digit < 10)
                    ch = Convert.ToChar('0' + digit);
                else
                    ch = Convert.ToChar('a' + digit - 10);

                sb.Append(ch);
            }

            return sb.ToString();
        }

        /// <summary>
        /// Parses the hex string passed and converts it a byte array.   
        /// </summary>
        /// <param name="s">The string to convert from hex.</param>
        /// <returns>The corresponding byte array.</returns>
        /// <exception cref="FormatException">Thrown if the input is not valid.</exception>
        /// <remarks>
        /// <note>
        /// The method ignores whitespace characters 
        /// (SP,CR,LF, and TAB) in the string so that HEX strings
        /// copied directly from typical hex dump outputs can
        /// be passed directly with minimal editing.
        /// </note>
        /// </remarks>
        public static byte[] FromHex(string s)
        {
            StringBuilder sb = null;

            // Normalize the input string by removing any whitespace
            // characters.

            for (int i = 0; i < s.Length; i++)
                if (Char.IsWhiteSpace(s[i]))
                {
                    sb = new StringBuilder(s.Length);
                    break;
                }

            if (sb != null)
            {
                for (int i = 0; i < s.Length; i++)
                    if (!Char.IsWhiteSpace(s[i]))
                        sb.Append(s[i]);

                s = sb.ToString();
            }

            // Parse the string.

            if ((s.Length & 1) != 0)    // Hex strings can't have an odd length
                throw new FormatException("HEX string may not have an odd length.");

            byte[]  buf = new byte[s.Length / 2];
            char    ch;
            int     v1, v2;

            for (int i = 0, j = 0; i < s.Length; )
            {
                ch = s[i++];
                if ('0' <= ch && ch <= '9')
                    v1 = ch - '0';
                else if ('a' <= ch && ch <= 'f')
                    v1 = ch - 'a' + 10;
                else if ('A' <= ch && ch <= 'F')
                    v1 = ch - 'A' + 10;
                else
                    throw new FormatException(string.Format("Invalid character [{0}] in HEX string.", ch));

                ch = s[i++];
                if ('0' <= ch && ch <= '9')
                    v2 = ch - '0';
                else if ('a' <= ch && ch <= 'f')
                    v2 = ch - 'a' + 10;
                else if ('A' <= ch && ch <= 'F')
                    v2 = ch - 'A' + 10;
                else
                    throw new FormatException(string.Format("Invalid character [{0}] in HEX string.", ch));

                buf[j++] = (byte)(v1 << 4 | v2);
            }

            return buf;
        }

        /// <summary>
        /// Attempts to parse a hex string into a byte array.   
        /// </summary>
        /// <param name="s">The string to convert from hex.</param>
        /// <param name="output">Returns as the parsed byte array on success.</param>
        /// <returns><c>true</c> if the string was parsed successfully.</returns>
        /// <remarks>
        /// <note>
        /// The method ignores whitespace characters 
        /// (SP,CR,LF, and TAB) in the string so that HEX strings
        /// copied directly from typical hex dump outputs can
        /// be passed directly with minimal editing.
        /// </note>
        /// </remarks>
        public static bool TryParseHex(string s, out byte[] output)
        {
            StringBuilder sb = null;

            output = null;

            if (s == null || s.Length == 0)
                return false;

            // Normalize the input string by removing any whitespace
            // characters.

            for (int i = 0; i < s.Length; i++)
                if (Char.IsWhiteSpace(s[i]))
                {
                    sb = new StringBuilder(s.Length);
                    break;
                }

            if (sb != null)
            {
                for (int i = 0; i < s.Length; i++)
                    if (!Char.IsWhiteSpace(s[i]))
                        sb.Append(s[i]);

                s = sb.ToString();
            }

            if (s.Length == 0)
                return false;

            // Parse the string.

            if ((s.Length & 1) != 0)    // Hex strings can't have an odd length
                return false;

            byte[]  buf = new byte[s.Length / 2];
            char    ch;
            int     v1, v2;

            for (int i = 0, j = 0; i < s.Length; )
            {
                ch = s[i++];
                if ('0' <= ch && ch <= '9')
                    v1 = ch - '0';
                else if ('a' <= ch && ch <= 'f')
                    v1 = ch - 'a' + 10;
                else if ('A' <= ch && ch <= 'F')
                    v1 = ch - 'A' + 10;
                else
                    return false;

                ch = s[i++];
                if ('0' <= ch && ch <= '9')
                    v2 = ch - '0';
                else if ('a' <= ch && ch <= 'f')
                    v2 = ch - 'a' + 10;
                else if ('A' <= ch && ch <= 'F')
                    v2 = ch - 'A' + 10;
                else
                    return false;

                buf[j++] = (byte)(v1 << 4 | v2);
            }

            output = buf;
            return true; ;
        }

        /// <summary>
        /// Returns <c>true</c> if the character passed is a hex digit.
        /// </summary>
        /// <param name="ch">The character to test.</param>
        /// <returns><c>true</c> if the character is in one of the ranges: 0..9, a..f or A..F.</returns>
        public static bool IsHex(char ch)
        {
            return '0' <= ch && ch <= '9' || 'a' <= ch && ch <= 'f' || 'A' <= ch && ch <= 'F';
        }

        /// <summary>
        /// Converts a single byte into its hexidecimal equivalent.
        /// </summary>
        /// <param name="value">The input byte.</param>
        /// <returns>The hex string.</returns>
        public static string ToHex(byte value)
        {
            int     digit;
            char    ch1;
            char    ch2;

            digit = value >> 4;
            if (digit < 10)
                ch1 = Convert.ToChar('0' + digit);
            else
                ch1 = Convert.ToChar('a' + digit - 10);

            digit = value & 0x0F;
            if (digit < 10)
                ch2 = Convert.ToChar('0' + digit);
            else
                ch2 = Convert.ToChar('a' + digit - 10);

            return new String(new char[] { ch1, ch2 });
        }

        /// <summary>
        /// Returns the decimal value of the hex digit passed.
        /// </summary>
        /// <param name="ch">The hex digit.</param>
        /// <returns>The corresponding decimal value.</returns>
        /// <remarks>
        /// Throws a FormatException if the character is not a hex digit.
        /// </remarks>
        public static int HexValue(char ch)
        {
            if ('0' <= ch && ch <= '9')
                return ch - '0';
            else if ('a' <= ch && ch <= 'f')
                return ch - 'a' + 10;
            else if ('A' <= ch && ch <= 'F')
                return ch - 'A' + 10;
            else
                throw new FormatException("Invalid hex character.");
        }

        /// <summary>
        /// Attempts to parse a hex encoded string into an integer.
        /// </summary>
        /// <param name="input">The input string.</param>
        /// <param name="value">The parsed integer.</param>
        /// <returns><c>true</c> if the input could be parsed successfully.</returns>
        public static bool TryParseHex(string input, out int value)
        {
            int v;

            value = 0;
            if (input.Length == 0)
                return false;

            v = 0;
            for (int i = 0; i < input.Length; i++)
            {
                if (!IsHex(input[i]))
                    return false;

                v *= 16;
                v += HexValue(input[i]);
            }

            value = v;
            return true;
        }

        /// <summary>
        /// Returns a byte array as a formatted hex dump.
        /// </summary>
        /// <param name="data">The buffer to be dumped.</param>
        /// <param name="start">The first byte to be dumped.</param>
        /// <param name="count">The number of bytes to be dumped.</param>
        /// <param name="bytesPerLine">The number of bytes to dump per output line.</param>
        /// <param name="options">The formatting options.</param>
        /// <returns>The hex dump string.</returns>
        public static string HexDump(byte[] data, int start, int count, int bytesPerLine, HexDumpOption options)
        {
            var     sb = new StringBuilder();
            int     offset;
            int     pos;

            if (count == 0)
                return string.Empty;

            if (bytesPerLine <= 0)
                throw new ArgumentException("bytesPerLine must be > 0", "bytesPerLine");

            offset = 0;

            if ((options & HexDumpOption.ShowOffsets) != 0)
            {
                if (count <= 0x0000FFFF)
                    sb.Append(offset.ToString("X4") + ": ");
                else
                    sb.Append(offset.ToString("X8") + ": ");
            }

            for (pos = start; pos < start + count; )
            {
                sb.Append(data[pos].ToString("X2") + " ");

                pos++;
                offset++;
                if (offset % bytesPerLine == 0)
                {
                    if (offset != 0)
                    {
                        if ((options & HexDumpOption.ShowAnsi) != 0)
                        {

                            sb.Append("- ");
                            for (int i = pos - bytesPerLine; i < pos; i++)
                            {

                                byte v = data[i];

                                if (v < 32 || v == 0x7F)
                                    v = (byte)'.';

                                sb.Append(Helper.AnsiEncoding.GetString(new byte[] { v }, 0, 1));
                            }
                        }

                        sb.Append("\r\n");
                    }

                    if ((options & HexDumpOption.ShowOffsets) != 0 && pos < start + count - 1)
                    {
                        if (count <= 0x0000FFFF)
                            sb.Append(offset.ToString("X4") + ": ");
                        else
                            sb.Append(offset.ToString("X8") + ": ");
                    }
                }
            }

            if ((options & HexDumpOption.ShowAnsi) != 0)
            {
                // Handle a final partial line

                int linePos = offset % bytesPerLine;

                if (linePos != 0)
                {
                    for (int i = 0; i < bytesPerLine - linePos; i++)
                        sb.Append("   ");

                    sb.Append("- ");
                    for (int i = pos - linePos; i < pos; i++)
                    {
                        byte v = data[i];

                        if (v < 32 || v == 0x7F)
                            v = (byte)'.';

                        sb.Append(Helper.AnsiEncoding.GetString(new byte[] { v }, 0, 1));
                    }

                    sb.Append("\r\n");
                }
            }

            if ((options & HexDumpOption.FormatHTML) != 0)
            {
                // $todo(jeff.lill): This isn't going to be terribly efficient.

                string output;

                output = EscapeHtml(sb.ToString(), true);
                output = output.Replace(" ", "&nbsp;");

                return output;
            }
            else
                return sb.ToString();
        }

        /// <summary>
        /// Returns a byte array as a formatted hex dump.
        /// </summary>
        /// <param name="data">The buffer to be dumped.</param>
        /// <param name="bytesPerLine">The number of bytes to dump per output line.</param>
        /// <param name="options">The formatting options.</param>
        /// <returns>The hex dump string.</returns>
        public static string HexDump(byte[] data, int bytesPerLine, HexDumpOption options)
        {
            return HexDump(data, 0, data.Length, bytesPerLine, options);
        }

        /// <summary>
        /// This method converts the string passed into a quoted C# style source string by
        /// adding the quotes and escaping any characters that need it.
        /// </summary>
        /// <param name="str">The string to quote.</param>
        public static string CSQuoteString(string str)
        {
            // $todo: I'm escaping only the most common control codes
            //        here.  At some point this should be completed.

            var sb = new StringBuilder();

            sb.Append('"');

            for (int i = 0; i < str.Length; i++)
            {
                switch (str[i])
                {
                    case '"':

                        sb.Append("\\\"");
                        break;

                    case '\r':

                        sb.Append("\\r");
                        break;

                    case '\n':

                        sb.Append("\\n");
                        break;

                    case '\t':

                        sb.Append("\\t");
                        break;

                    default:

                        sb.Append(str[i]);
                        break;
                }
            }

            sb.Append('"');
            return sb.ToString();
        }

        /// <summary>
        /// This method processes the string into safe XML by escaping any
        /// special XML characters it finds.
        /// </summary>
        /// <param name="s">The input string.</param>
        public static string EscapeXml(string s)
        {
            // $todo: I'm using this method for now.

            return EscapeHtml(s, false);
        }

        /// <summary>
        /// This method replaces any XML escape characters from the string
        /// passed with the corresponding character and returns the result.
        /// </summary>
        /// <param name="s"></param>
        public static string UnescapeXml(string s)
        {
            // $todo: I'm only going to handle the escape codes generated
            //        by the EscapeXML() method for now.  I need to come
            //        back and redo this at some point.

            string clean;

            clean = s;
            clean = clean.Replace("&quot;", "\"");
            clean = clean.Replace("&lt;", "<");
            clean = clean.Replace("&gt;", ">");
            clean = clean.Replace("&amp;", "&");

            return clean;
        }

        /// <summary>
        /// This method processes the string into safe HTML by escaping any
        /// special HTML characters it finds.  Note that the method also
        /// converts any LF sequences it finds into a &lt;br&gt; tag.
        /// The method returns the result.
        /// </summary>
        /// <param name="s">The input string.</param>
        public static string EscapeHtml(string s)
        {
            return EscapeHtml(s, true);
        }

        /// <summary>
        /// This method processes the string into safe HTML by escaping any
        /// special HTML characters it finds.  Note that the method also
        /// converts any LF sequences it finds into a &lt;br&gt; tag if convertCRLF=true.
        /// The method returns the result.
        /// </summary>
        /// <param name="value">The input string.</param>
        /// <param name="convertCRLF">Pass as <c>true</c> to enable CRLF -> &lt;br&gt; conversions.</param>
        public static string EscapeHtml(string value, bool convertCRLF)
        {
            // $todo: I'm only escaping '"', '<', '>', and '&' characters.  I should come back
            //        at some point and do a full implementation.

            if (value.IndexOfAny(new char[] { '<', '>', '&', '"', '\r', '\n' }) == -1)
                return value;

            var sb = new StringBuilder(value.Length + 100);

            for (int i = 0; i < value.Length; i++)
            {
                switch (value[i])
                {
                    case '"':

                        sb.Append("&quot;");
                        break;

                    case '<':

                        sb.Append("&lt;");
                        break;

                    case '>':

                        sb.Append("&gt;");
                        break;

                    case '&':

                        sb.Append("&amp;");
                        break;

                    case '\n':

                        if (!convertCRLF)
                            sb.Append('\n');
                        else
                            sb.Append("<br>");
                        break;

                    case '\r':

                        if (!convertCRLF)
                            sb.Append('\r');
                        break;

                    default:

                        sb.Append(value[i]);
                        break;
                }
            }

            return sb.ToString();
        }

#if !SILVERLIGHT

        /// <summary>
        /// Renders the XML document as formatted string.
        /// </summary>
        /// <param name="xmlDoc">The <see cref="XmlDocument" />.</param>
        /// <returns>The XML formated document string.</returns>
        public static string FromXmlDoc(XmlDocument xmlDoc)
        {
            return FromXmlDoc(xmlDoc, true);
        }

        /// <summary>
        /// Renders the XML document as a string.
        /// </summary>
        /// <param name="xmlDoc">The <see cref="XmlDocument" />.</param>
        /// <param name="format">Specifies if the XML is to be formatted with indented children.</param>
        /// <returns>The XML formated document string.</returns>
        public static string FromXmlDoc(XmlDocument xmlDoc, bool format)
        {
            var writer    = new StringWriter();
            var xmlWriter = new XmlTextWriter(writer);

            if (format)
            {
                xmlWriter.Formatting = Formatting.Indented;
                xmlWriter.Indentation = 1;
                xmlWriter.IndentChar = '\t';
            }
            else
                xmlWriter.Formatting = Formatting.None;

            using (xmlWriter)
            {
                xmlDoc.WriteTo(xmlWriter);
                xmlWriter.Flush();

                return writer.ToString();
            }
        }

#endif // !SILVERLIGHT

        /// <summary>
        /// Attempts to parse <see cref="Uri" /> from a string, returning
        /// <c>false</c> if the string does not represent a valid URI.
        /// </summary>
        /// <param name="uriText">The text to be parsed.</param>
        /// <param name="uri">Returns as the parsed <see cref="Uri" /> or <c>null</c>.</param>
        /// <returns>Returns <c>true</c> if the <see cref="Uri" /> was parsed successfuly.</returns>
        public static bool TryParseUri(string uriText, out Uri uri)
        {
            uri = null;

            try
            {
                uri = new Uri(uriText);
                return true;
            }
            catch
            {

                return false;
            }
        }

        /// <summary>
        /// Determins whether a string represents a valid DNS host name.
        /// </summary>
        /// <param name="host">The host name to be tested.</param>
        /// <returns><c>true</c> if the host name is valid.</returns>
        public static bool IsValidDomainName(string host)
        {
            if (host == null || host.Length == 0 || host.Length > 255)
                return false;

            if (host.StartsWith(".") || host.EndsWith("."))
                return false;

            foreach (string label in host.Split('.'))
            {
                if (label.Length == 0 || label.Length > 63)
                    return false;

                for (int i = 0; i < label.Length; i++)
                {
                    char ch = label[i];

                    if ('0' <= ch && ch <= '9')
                        continue;
                    else if ('a' <= ch && ch <= 'z')
                        continue;
                    else if ('A' <= ch && ch <= 'Z')
                        continue;
                    else if (ch == '-')
                        continue;

                    return false;
                }
            }

            return true;
        }

        /// <summary>
        /// Parses a URI query and returns a <see cref="ArgCollection" />
        /// holding the name/value pairs.
        /// </summary>
        /// <param name="uri">The URI to be parsed.</param>
        /// <returns>The name/value pairs.</returns>
        /// <remarks>
        /// <note>
        /// This method converts any embedded ASCII <b>DEL (0x7F)</b> characters
        /// within an parameter name or value to the ampersand (&amp;) character to
        /// support the escaping implemented by <see cref="ToUriQuery" />.
        /// </note>
        /// </remarks>
        public static ArgCollection ParseUriQuery(Uri uri)
        {
            ArgCollection   args  = new ArgCollection('\t', (char)0);
            string          query = uri.Query;
            int             p;
            int             pEnd;
            string          name;
            string          value;

            if (query.Length == 0 || query[0] != '?')
                return args;

            p = 1;  // Skip over the "?".
            while (true)
            {
                pEnd = query.IndexOf('=', p);
                if (pEnd == -1)
                    break;

                name = query.Substring(p, pEnd - p);
                p    = pEnd + 1;

                pEnd = query.IndexOf('&', p);
                if (pEnd == -1)
                    value = query.Substring(p);
                else
                    value = query.Substring(p, pEnd - p);

                if (name.Length > 0)
                {
                    name  = UnescapeUri(name).Replace((char)0x7F, '&');
                    value = UnescapeUri(value).Replace((char)0x7F, '&');

                    args.Set(name, value);
                }

                if (pEnd == -1)
                    break;

                p = pEnd + 1;
            }

            return args;
        }

        /// <summary>
        /// Renders a set of name/value pairs into a form suitable for
        /// appending to a URI.  Note that the result will include the
        /// leading "?" (if the collection passed is not empty) and that
        /// it will escape characters as necessary.
        /// </summary>
        /// <param name="args">The name/value pairs to be encoded.</param>
        /// <returns>The escaped URI query string.</returns>
        /// <remarks>
        /// <note>
        /// This method escapes ampersand (&amp;) characters within either a
        /// parameter name or value to <b>%7f</b> which the <see cref="ParseUriQuery" />
        /// method will restore back to the original ampersand.
        /// </note>
        /// </remarks>
        public static string ToUriQuery(ArgCollection args)
        {
            if (args.Count == 0)
                return string.Empty;

            var     sb = new StringBuilder(args.Count * 30);
            bool    first = true;
            string  name;
            string  value;

            sb.Append('?');
            foreach (string key in args)
            {
                if (first)
                    first = false;
                else
                    sb.Append('&');

                name = EscapeUri(key).Replace("&", "%7f");
                value = EscapeUri(args[key]).Replace("&", "%7f");

                sb.AppendFormat("{0}={1}", name, value);
            }

            return sb.ToString();
        }

        /// <summary>
        /// Escapes the string passed so that it will be suitiable
        /// for adding to a URI.
        /// </summary>
        /// <param name="s">The input string.</param>
        /// <returns>The escaped string.</returns>
        public static string EscapeUri(string s)
        {
            // $todo(jeff.lill): This code doesn't handle unicode characters

            var sb = new StringBuilder(s.Length + 100);
            char ch;
            int digit;

            for (int i = 0; i < s.Length; i++)
            {
                ch = s[i];
                if (ch <= ' ' || ch >= (char)127 || ch == '%' || ch == '+')
                {
                    sb.Append('%');

                    digit = (((int)ch) >> 4) & 0x0F;
                    if (digit < 10)
                        sb.Append((char)('0' + digit));
                    else
                        sb.Append((char)('a' + (digit - 10)));

                    digit = ((int)ch) & 0x0F;
                    if (digit < 10)
                        sb.Append((char)('0' + digit));
                    else
                        sb.Append((char)('a' + (digit - 10)));
                }
                else
                    sb.Append(ch);
            }

            return sb.ToString();
        }

        /// <summary>
        /// Converts the uri escaped string into its unescaped form.
        /// </summary>
        /// <param name="value">The uri escaped string.</param>
        /// <returns>The unescaped equivalent.</returns>
        public static string UnescapeUri(string value)
        {
            StringBuilder   sb;
            int             p, pEnd;

            if (value.IndexOfAny(uriEscapes) == -1)
                return value;

            sb = new StringBuilder(value.Length + 100);
            p  = 0;

            while (true)
            {
                pEnd = value.IndexOfAny(uriEscapes, p);
                if (pEnd == -1)
                {
                    sb.Append(value, p, value.Length - p);
                    break;
                }

                sb.Append(value, p, pEnd - p);
                p = pEnd + 1;

                if (value[pEnd] == '+')
                {
                    sb.Append(' ');
                }
                else
                {
                    // Must be a %## escape sequence

                    if (p + 1 > value.Length)
                        throw new FormatException("Invalid escape sequence.");

                    sb.Append((char)((Helper.HexValue(value[p]) << 4) | Helper.HexValue(value[p + 1])));
                    p += 2;
                }
            }

            return sb.ToString();
        }

        /// <summary>
        /// Escapes the string passed so that it will be suitable for 
        /// appending as a Uri parameter.  This is essentially the same
        /// as what <see cref="EscapeUri" /> does with the addition of escaping the
        /// ampersand and equal sign characters.
        /// </summary>
        /// <param name="value">The input string.</param>
        /// <returns>The escaped equilvalant.</returns>
        public static string EscapeUriParam(string value)
        {
            // $todo(jeff.lill): This code doesn't handle unicode characters

            var     sb = new StringBuilder(value.Length + 100);
            char    ch;
            int     digit;

            for (int i = 0; i < value.Length; i++)
            {
                ch = value[i];
                if (ch <= ' ' || ch >= (char)127 || ch == '%' || ch == '&' || ch == '=' || ch == '+')
                {
                    sb.Append('%');

                    digit = (((int)ch) >> 4) & 0x0F;
                    if (digit < 10)
                        sb.Append((char)('0' + digit));
                    else
                        sb.Append((char)('a' + (digit - 10)));

                    digit = ((int)ch) & 0x0F;
                    if (digit < 10)
                        sb.Append((char)('0' + digit));
                    else
                        sb.Append((char)('a' + (digit - 10)));
                }
                else
                    sb.Append(ch);
            }

            return sb.ToString();
        }

        /// <summary>
        /// Unescapes the uri parameter passed.
        /// </summary>
        /// <param name="value">The escaped string.</param>
        /// <returns>The equivalent unescaped string.</returns>
        /// <remarks>
        /// <note>
        /// This is the same as calling <see cref="UnescapeUri" />.
        /// </note>
        /// </remarks>
        public static string UnescapeUriParam(string value)
        {
            return UnescapeUri(value);
        }

        /// <summary>
        /// Converts a string to a <see cref="Uri" />.
        /// </summary>
        /// <param name="uriString">The input URI string.</param>
        /// <returns>The parsed URI or <c>null</c> if the parameter is <c>null</c>.</returns>
        public static Uri ToUri(string uriString)
        {
            if (uriString == null)
                return null;
            else
                return new Uri(uriString);
        }

#if !SILVERLIGHT
        /// <summary>
        /// Returns the file name (aka the last segment) from a <see cref="Uri" />.
        /// </summary>
        /// <param name="uri">The <see cref="Uri" />.</param>
        /// <returns>The extracted file name or <c>null</c> if no file name is present.</returns>
        public static string GetUriFileName(Uri uri)
        {
            if (uri.Segments.Length < 1)
                return null;

            string fileName = uri.Segments[uri.Segments.Length - 1];

            if (fileName.EndsWith("/"))
                return null;

            return fileName;
        }
#endif // !SILVERLIGHT

        /// <summary>
        /// Escapes a string passed so that is suitable for writing to
        /// a CSV file as a field.
        /// </summary>
        /// <param name="value">The field value.</param>
        /// <returns>The escaped string.</returns>
        /// <remarks>
        /// The method surrounds the value with double quotes if it contains
        /// a comma or CRLF as well as escaping any double quotes in the
        /// string with second double quote.
        /// </remarks>
        public static string EscapeCsv(string value)
        {
            bool needsQuotes = value.IndexOfAny(csvEscapes) != -1;

            if (value.IndexOf('"') != -1)
                value = value.Replace("\"", "\"\"");

            if (needsQuotes)
                return "\"" + value + "\"";
            else
                return value;
        }

        /// <summary>
        /// Parses a CSV encoded string into its component fields.
        /// </summary>
        /// <param name="value">The encoded CSV string.</param>
        /// <returns>The decoded fields.</returns>
        /// <exception cref="FormatException">Thrown if the CSV file format is not valid.</exception>
        public static string[] ParseCsv(string value)
        {
            List<string>    fields = new List<string>(20);
            int             pStart, pos;
            string          field;

            pStart = 0;
            while (true)
            {

                if (pStart < value.Length && value[pStart] == '"')
                {
                    var sb = new StringBuilder(100);

                    // We have a quoted CSV field

                    pStart++;
                    while (true)
                    {
                        pos = value.IndexOf('"', pStart);
                        if (pos == -1)
                            throw new FormatException("Missing terminating quote in quoted CSV field.  The row may be span multiple lines.  Consider using the CsvReader class.");
                        else if (pos < value.Length - 1 && value[pos + 1] == '"')
                        {
                            // We found an escaped quote ("")

                            sb.Append(value, pStart, pos - pStart + 1);
                            pStart = pos + 2;
                        }
                        else
                        {
                            sb.Append(value, pStart, pos - pStart);
                            field = sb.ToString();
                            fields.Add(field);

                            pStart = pos + 1;
                            break;
                        }
                    }

                    if (pStart >= value.Length || value[pStart] != ',')
                        break;

                    pStart++;
                }
                else
                {
                    // We have an unquoted CSV field.

                    pos = value.IndexOf(',', pStart);
                    if (pos == -1)
                    {
                        field = value.Substring(pStart);
                        fields.Add(field);
                        break;
                    }
                    else
                    {
                        field = value.Substring(pStart, pos - pStart);
                        fields.Add(field);

                        pStart = pos + 1;
                    }
                }
            }

            return fields.ToArray();
        }

        /// <summary>
        /// This method clips the string passed so that its length does not exceed
        /// a specified number of characters.  If the string is longer than the
        /// maximum allowed it will be truncated and "..." will be appended to it.
        /// Note that string will be truncated such that the total length of the
        /// string returned (including the "..." will be maxChars).
        /// </summary>
        /// <param name="value">The string to clip.</param>
        /// <param name="maxChars">The maximum length of the clipped string.</param>
        public static string Clip(string value, int maxChars)
        {
            if (value.Length <= maxChars)
                return value;

            return value.Substring(0, maxChars - 3) + "...";
        }

        /// <summary>
        /// Returns <c>null</c> if the value passed is <c>null</c> otherwise
        /// returns the result of the object's <see cref="object.ToString()" /> method.
        /// </summary>
        /// <param name="value">The value whose string representation is to be returned (or <c>null</c>).</param>
        /// <returns>The string value (or <c>null</c>).</returns>
        public static string ToString(object value)
        {
            if (value == null)
                return null;
            else
                return value.ToString();
        }

        /// <summary>
        /// This method returns an empty string if the value passed is <c>null</c>
        /// otherwise it returns the value.
        /// </summary>
        /// <param name="value">The value to be normalized.</param>
        /// <returns>A non-<c>null</c> string.</returns>
        public static string Normalize(string value)
        {
            if (value == null)
                return string.Empty;
            else
                return value;
        }

        /// <summary>
        /// This method returns an empty string if the value passed is <c>null</c>
        /// otherwise if returns the value converted to a string.
        /// </summary>
        /// <param name="value">The value to be normalized.</param>
        /// <returns>A non-<c>null</c> string.</returns>
        public static string Normalize(object value)
        {
            if (value == null)
                return string.Empty;
            else
                return value.ToString();
        }

        /// <summary>
        /// Specifies whether <see cref="NewGuid" /> should return
        /// easy-to-read process local GUIDs rather than true GUIDs.  This 
        /// method is available only for unit test builds.
        /// </summary>
        /// <param name="mode">The <see cref="GuidMode" />.</param>
        /// <remarks>
        /// <para>
        /// Process local GUIDs are useful for debugging unit tests that
        /// use a lot of GUIDs.  The problem is that real GUIDs are very hard
        /// to read and follow in log files.  Process local GUIDs simply start
        /// at 0 and count upwards by one for each GUID created or start at
        /// <see cref="int.MaxValue" /> and count down.  These GUIDs will be 
        /// unique within the process, but will be easy to read.
        /// </para>
        /// <note>
        /// Passing <c>true</c> to this method will reset the
        /// process local GUID counter to 0.
        /// </note>
        /// </remarks>
        internal static void SetLocalGuidMode(GuidMode mode) 
        {
            switch (mode)
            {
                case GuidMode.CountUp :

                    processGuid = 0;
                    break;

                case GuidMode.CountDown :

                    processGuid = int.MaxValue;
                    break;
            }

            guidMode = mode;
        }

        /// <summary>
        /// Generates a globally unique ID.
        /// </summary>
        /// <remarks>
        /// <para>
        /// Windows CE doesn't implement Guid.NewGuid() so call this
        /// instead for cross platform capability.
        /// </para>
        /// <para>
        /// Unit tests can also make use of the <b>SetLocalGuidMode()</b> method
        /// to return easy to debug process local GUIDs.
        /// </para>
        /// </remarks>
        public static Guid NewGuid()
        {
            if (guidMode != GuidMode.Normal) {

                byte[]  guid = new byte[16];
                int     c;

                if (guidMode == GuidMode.CountUp)
                    c = Interlocked.Increment(ref processGuid);
                else 
                    c = Interlocked.Decrement(ref processGuid);

                guid[15] = (byte) (c);
                guid[14] = (byte) (c>>8);
                guid[13] = (byte) (c>>16);
                guid[12] = (byte) (c>>24);

                return new Guid(guid);
            }

            return Guid.NewGuid();
        }

        /// <summary>
        /// Returns the ANSI text encoding.
        /// </summary>
        public static Encoding AnsiEncoding
        {
            get { return ansiEncoding; }
        }

        /// <summary>
        /// Returns the ASCII text encoding.
        /// </summary>
        public static Encoding ASCIIEncoding
        {
            get { return asciiEncoding; }
        }

        /// <summary>
        /// Queues the callback and state to the current thread pool for asynchronous
        /// processing.
        /// </summary>
        /// <param name="callback">The callback.</param>
        /// <param name="state">State to be passed to the callback.</param>
        /// <remarks>
        /// This method abstracts away the fact that Windows CE doesn't
        /// support this method.  ThreadPool.QueueUserWorkItem() will be called in
        /// this case.
        /// </remarks>
        public static void UnsafeQueueUserWorkItem(WaitCallback callback, object state)
        {
#if SILVERLIGHT
            ThreadPool.QueueUserWorkItem(callback,state);
#else
            ThreadPool.UnsafeQueueUserWorkItem(callback, state);
#endif
        }

        /// <summary>
        /// Sets the action that <see cref="Helper.UIDispatch" /> uses to dispatch 
        /// actions to the user interface thread.  The dispatcher defaults to <c>null</c>
        /// and is typically set by downstream class libraries such as the various
        /// flavors of <b>LillTek.Xaml</b>.
        /// </summary>
        /// <param name="actionDispatcher">The action dispatcher.</param>
        /// <exception cref="ArgumentNullException">Thrown if <paramref name="actionDispatcher" /> is <c>null</c>.</exception>
        /// <exception cref="InvalidOperationException">Thrown if the UI action dispatcher has already been set.</exception>
        public static void SetUIActionDispatcher(Action<Action> actionDispatcher)
        {
            if (actionDispatcher == null)
                throw new ArgumentNullException("actionDispatcher");

            if (uiActionDispatcher != null)
                throw new InvalidOperationException("[Helper.SetUIActionDispatcher()] has already been called.  This may only be called once per application domain.");

            Helper.uiActionDispatcher = actionDispatcher;
        }

        /// <summary>
        /// Performs an action on the UI thread, performing it synchronously if the executing
        /// code is on the UI thread, or performing it asynchronously if the code is not running
        /// on the UI thread.
        /// </summary>
        /// <param name="action">The action to be executed.</param>
        /// <exception cref="ArgumentNullException">Thrown if <paramref name="action" /> is <c>null</c>.</exception>
        /// <remarks>
        /// <note>
        /// <see cref="SetUIActionDispatcher" /> must be called to configure the global user interface 
        /// action dispatcher first, before actions may be dispatched to the UI thread.  This is typically 
        /// performed by downstream class libraries such as the various flavors of <b>LillTek.Xaml</b>.
        /// </note>
        /// </remarks>
        public static void UIDispatch(Action action)
        {
            if (action == null)
                throw new ArgumentNullException("action");

            if (uiActionDispatcher == null)
            {
                SysLog.LogWarning("Cannot dispatch UI actions until [Helper.SetUIActionDispatcher()] has been called to initialize the dispatcher.");
                return;
            }

            uiActionDispatcher(action);
        }

        /// <summary>
        /// Queues an <see cref="Action" /> to be executed asynchronously
        /// on a worker pool thread.
        /// </summary>
        /// <param name="action">The action.</param>
        /// <remarks>
        /// <note>
        /// Any exceptions thrown by the action will be logged to the <see cref="SysLog" />.
        /// </note>
        /// </remarks>
        public static void EnqueueAction(Action action)
        {
            Helper.UnsafeQueueUserWorkItem(
                s =>
                {
                    try
                    {
                        ((Action)s)();
                    }
                    catch (Exception e)
                    {

                        SysLog.LogException(e);
                    }

                }, action);
        }

        /// <summary>
        /// Queues an <see cref="Action{T1}" /> to be executed asynchronously
        /// on a worker pool thread.
        /// </summary>
        /// <param name="p1">The first action parameter.</param>
        /// <param name="action">The action.</param>
        /// <typeparam name="T1">Type of the first action parameter.</typeparam>
        /// <remarks>
        /// <note>
        /// Any exceptions thrown by the action will be logged to the <see cref="SysLog" />.
        /// </note>
        /// </remarks>
        public static void EnqueueAction<T1>(T1 p1, Action<T1> action)
        {
            Helper.UnsafeQueueUserWorkItem(
                s =>
                {
                    try
                    {
                        ((Action<T1>)s)(p1);
                    }
                    catch (Exception e)
                    {
                        SysLog.LogException(e);
                    }

                }, action);
        }

        /// <summary>
        /// Queues an <see cref="Action{T1,T2}" /> to be executed asynchronously
        /// on a worker pool thread.
        /// </summary>
        /// <param name="p1">The first action parameter.</param>
        /// <param name="p2">The second action parameter.</param>
        /// <param name="action">The action.</param>
        /// <typeparam name="T1">Type of the first action parameter.</typeparam>
        /// <typeparam name="T2">Type of the second action parameter.</typeparam>
        /// <remarks>
        /// <note>
        /// Any exceptions thrown by the action will be logged to the <see cref="SysLog" />.
        /// </note>
        /// </remarks>
        public static void EnqueueAction<T1, T2>(T1 p1, T2 p2, Action<T1, T2> action)
        {
            Helper.UnsafeQueueUserWorkItem(
                s =>
                {
                    try
                    {
                        ((Action<T1, T2>)s)(p1, p2);
                    }
                    catch (Exception e)
                    {
                        SysLog.LogException(e);
                    }

                }, action);
        }

        /// <summary>
        /// Queues an <see cref="Action{T1,T2,T3}" /> to be executed asynchronously
        /// on a worker pool thread.
        /// </summary>
        /// <param name="p1">The first action parameter.</param>
        /// <param name="p2">The second action parameter.</param>
        /// <param name="p3">The third action parameter.</param>
        /// <param name="action">The action.</param>
        /// <typeparam name="T1">Type of the first action parameter.</typeparam>
        /// <typeparam name="T2">Type of the second action parameter.</typeparam>
        /// <typeparam name="T3">Type of the third action parameter.</typeparam>
        /// <remarks>
        /// <note>
        /// Any exceptions thrown by the action will be logged to the <see cref="SysLog" />.
        /// </note>
        /// </remarks>
        public static void EnqueueAction<T1, T2, T3>(T1 p1, T2 p2, T3 p3, Action<T1, T2, T3> action)
        {
            Helper.UnsafeQueueUserWorkItem(
                s =>
                {
                    try
                    {
                        ((Action<T1, T2, T3>)s)(p1, p2, p3);
                    }
                    catch (Exception e)
                    {
                        SysLog.LogException(e);
                    }

                }, action);
        }

        /// <summary>
        /// Queues an <see cref="Action{T1,T2,T3,T4}" /> to be executed asynchronously
        /// on a worker pool thread.
        /// </summary>
        /// <param name="p1">The first action parameter.</param>
        /// <param name="p2">The second action parameter.</param>
        /// <param name="p3">The third action parameter.</param>
        /// <param name="p4">The fourth action parameter.</param>
        /// <param name="action">The action.</param>
        /// <typeparam name="T1">Type of the first action parameter.</typeparam>
        /// <typeparam name="T2">Type of the second action parameter.</typeparam>
        /// <typeparam name="T3">Type of the third action parameter.</typeparam>
        /// <typeparam name="T4">Type of the fourth action parameter.</typeparam>
        /// <remarks>
        /// <note>
        /// Any exceptions thrown by the action will be logged to the <see cref="SysLog" />.
        /// </note>
        /// </remarks>
        public static void EnqueueAction<T1, T2, T3, T4>(T1 p1, T2 p2, T3 p3, T4 p4, Action<T1, T2, T3, T4> action)
        {
            Helper.UnsafeQueueUserWorkItem(
                s =>
                {
                    try
                    {
                        ((Action<T1, T2, T3, T4>)s)(p1, p2, p3, p4);
                    }
                    catch (Exception e)
                    {
                        SysLog.LogException(e);
                    }

                }, action);
        }


        /// <summary>
        /// Purges all queued serialized actions.
        /// </summary>
        public static void ClearPendingSerializedActions()
        {
            globalSerializedActionQueue.Clear();
        }

        /// <summary>
        /// Clears all queued serialized actions and sets a flag to disable the
        /// processing of additional work.
        /// </summary>
        /// <remarks>
        /// <para>
        /// This needs to be called in some situations when an application has detected
        /// that its being terminated to prevent <see cref="ObjectDisposedException" />s
        /// being thrown by asynchronous code that get executed after the process has 
        /// starting to release underlying resources (such as synchronization events).
        /// </para>
        /// <note>
        /// All calls to any of the <b>EnqueueSerializedAction()</b> variants will do nothing
        /// after this method is called.
        /// </note>
        /// </remarks>
        public static void ShutdownSerializedActions()
        {
            globalSerializedActionQueue.Shutdown();
        }

        /// <summary>
        /// Queues an <see cref="Action" /> to be queued and executed asynchronously
        /// on a single background thread.
        /// </summary>
        /// <param name="action">The action.</param>
        /// <remarks>
        /// <note>
        /// This method differs from <see cref="EnqueueAction(Action)" /> because this method
        /// guarantees that actions are executed in the order submitted where as <see cref="EnqueueAction(Action)" />
        /// simply executes the action on the first available worker pool thread.
        /// </note>
        /// <note>
        /// Any exceptions thrown by the action will be logged to the <see cref="SysLog" />.
        /// </note>
        /// </remarks>
        public static void EnqueueSerializedAction(Action action)
        {
            globalSerializedActionQueue.EnqueueAction(action);
        }

        /// <summary>
        /// Queues a parameterized <see cref="Action" /> to be queued and executed asynchronously
        /// on a single background thread.
        /// </summary>
        /// <param name="p1">The first action parameter.</param>
        /// <param name="action">The action.</param>
        /// <typeparam name="T1">Type of the first action parameter.</typeparam>
        /// <remarks>
        /// <note>
        /// This method differs from <see cref="EnqueueAction(Action)" /> because this method
        /// guarantees that actions are executed in the order submitted where as <see cref="EnqueueAction(Action)" />
        /// simply executes the action on the first available worker pool thread.
        /// </note>
        /// <note>
        /// Any exceptions thrown by the action will be logged to the <see cref="SysLog" />.
        /// </note>
        /// </remarks>
        public static void EnqueueSerializedAction<T1>(T1 p1, Action<T1> action)
        {
            globalSerializedActionQueue.EnqueueAction<T1>(p1, action);
        }

        /// <summary>
        /// Queues a parameterized <see cref="Action" /> to be queued and executed asynchronously
        /// on a single background thread.
        /// </summary>
        /// <param name="p1">The first action parameter.</param>
        /// <param name="p2">The second action parameter.</param>
        /// <param name="action">The action.</param>
        /// <typeparam name="T1">Type of the first action parameter.</typeparam>
        /// <typeparam name="T2">Type of the second action parameter.</typeparam>
        /// <remarks>
        /// <note>
        /// This method differs from <see cref="EnqueueAction(Action)" /> because this method
        /// guarantees that actions are executed in the order submitted where as <see cref="EnqueueAction(Action)" />
        /// simply executes the action on the first available worker pool thread.
        /// </note>
        /// <note>
        /// Any exceptions thrown by the action will be logged to the <see cref="SysLog" />.
        /// </note>
        /// </remarks>
        public static void EnqueueSerializedAction<T1, T2>(T1 p1, T2 p2, Action<T1, T2> action)
        {
            globalSerializedActionQueue.EnqueueAction<T1, T2>(p1, p2, action);
        }

        /// <summary>
        /// Queues a parameterized <see cref="Action" /> to be queued and executed asynchronously
        /// on a single background thread.
        /// </summary>
        /// <param name="p1">The first action parameter.</param>
        /// <param name="p2">The second action parameter.</param>
        /// <param name="p3">The third action parameter.</param>
        /// <param name="action">The action.</param>
        /// <typeparam name="T1">Type of the first action parameter.</typeparam>
        /// <typeparam name="T2">Type of the second action parameter.</typeparam>
        /// <typeparam name="T3">Type of the third action parameter.</typeparam>
        /// <remarks>
        /// <note>
        /// This method differs from <see cref="EnqueueAction(Action)" /> because this method
        /// guarantees that actions are executed in the order submitted where as <see cref="EnqueueAction(Action)" />
        /// simply executes the action on the first available worker pool thread.
        /// </note>
        /// <note>
        /// Any exceptions thrown by the action will be logged to the <see cref="SysLog" />.
        /// </note>
        /// </remarks>
        public static void EnqueueSerializedAction<T1, T2, T3>(T1 p1, T2 p2, T3 p3, Action<T1, T2, T3> action)
        {
            globalSerializedActionQueue.EnqueueAction<T1, T2, T3>(p1, p2, p3, action);
        }

        /// <summary>
        /// Queues a parameterized <see cref="Action" /> to be queued and executed asynchronously
        /// on a single background thread.
        /// </summary>
        /// <param name="p1">The first action parameter.</param>
        /// <param name="p2">The second action parameter.</param>
        /// <param name="p3">The third action parameter.</param>
        /// <param name="p4">The fourth action parameter.</param>
        /// <param name="action">The action.</param>
        /// <typeparam name="T1">Type of the first action parameter.</typeparam>
        /// <typeparam name="T2">Type of the second action parameter.</typeparam>
        /// <typeparam name="T3">Type of the third action parameter.</typeparam>
        /// <typeparam name="T4">Type of the fourth action parameter.</typeparam>
        /// <remarks>
        /// <note>
        /// This method differs from <see cref="EnqueueAction(Action)" /> because this method
        /// guarantees that actions are executed in the order submitted where as <see cref="EnqueueAction(Action)" />
        /// simply executes the action on the first available worker pool thread.
        /// </note>
        /// <note>
        /// Any exceptions thrown by the action will be logged to the <see cref="SysLog" />.
        /// </note>
        /// </remarks>
        public static void EnqueueSerializedAction<T1, T2, T3, T4>(T1 p1, T2 p2, T3 p3, T4 p4, Action<T1, T2, T3, T4> action)
        {
            globalSerializedActionQueue.EnqueueAction<T1, T2, T3, T4>(p1, p2, p3, p4, action);
        }

        /// <summary>
        /// Executes an <see cref="Action"/> within an exception handler that catches and logs
        /// any exceptions thrown to the <see cref="SysLog"/>.
        /// </summary>
        /// <param name="action">The action.</param>
        /// <exception cref="ArgumentNullException">Thrown if <paramref name="action" /> is <c>null</c>.</exception>
        /// <remarks>
        /// <para>
        /// This calling method is equivalent to:
        /// </para>
        /// <code language="cs">
        /// try {
        ///    
        ///     action();
        /// }
        /// catch (Exception e) {
        ///
        ///     SysLog.LogException(e);
        /// }
        /// </code>
        /// </remarks>
        public static void InvokeWithCatch(Action action)
        {
            if (action == null)
                throw new ArgumentNullException("action");

            try
            {
                action();
            }
            catch (Exception e)
            {
                SysLog.LogException(e);
            }
        }

        /// <summary>
        /// Writes the boolean value to the buffer at the specified position,
        /// advancing the position past the written value.
        /// </summary>
        /// <param name="buf">The buffer.</param>
        /// <param name="pos">The position.</param>
        /// <param name="value">The value.</param>
        public static void WriteBool(byte[] buf, ref int pos, bool value)
        {
            buf[pos++] = value ? (byte)1 : (byte)0;
        }

        /// <summary>
        /// Writes the byte value to the buffer at the specified position,
        /// advancing the position past the written value.
        /// </summary>
        /// <param name="buf">The buffer.</param>
        /// <param name="pos">The position.</param>
        /// <param name="value">The value.</param>
        public static void WriteByte(byte[] buf, ref int pos, byte value)
        {
            buf[pos++] = value;
        }

        /// <summary>
        /// Writes the <see cref="Guid" /> value to the buffer at the specified position,
        /// advancing the position past the written value.
        /// </summary>
        /// <param name="buf">The buffer.</param>
        /// <param name="pos">The position.</param>
        /// <param name="value">The value.</param>
        public static void WriteGuid(byte[] buf, ref int pos, Guid value)
        {
            WriteBytes(buf, ref pos, value.ToByteArray());
        }

        /// <summary>
        /// Writes a byte array to the buffer without encoding a length,
        /// advancing the position past the written value.
        /// </summary>
        /// <param name="buf">The buffer.</param>
        /// <param name="pos">The position.</param>
        /// <param name="bytes">The byte array.</param>
        public static void WriteBytes(byte[] buf, ref int pos, byte[] bytes)
        {
            Array.Copy(bytes, 0, buf, pos, bytes.Length);
            pos += bytes.Length;
        }

        /// <summary>
        /// Writes the byte array to the buffer at the specified position
        /// in big-endian order, advancing the position past the written value.
        /// </summary>
        /// <param name="buf">The buffer.</param>
        /// <param name="pos">The position.</param>
        /// <param name="bytes">The byte array.</param>
        /// <remarks>
        /// <para>
        /// The array is written as a 16-bit length (in network or
        /// big-endian order) followed by the array bytes.
        /// null arrays may be also be written.  These are encoded
        /// using a length of ushort.MaxValue.
        /// </para>
        /// <para>
        /// The maximum array length that can be written is 
        /// (ushort.MaxValue-1).
        /// </para>
        /// </remarks>
        public static void WriteBytes16(byte[] buf, ref int pos, byte[] bytes)
        {
            if (bytes == null)
            {
                WriteInt16(buf, ref pos, ushort.MaxValue);
                return;
            }

            if (bytes.Length >= ushort.MaxValue)
                throw new ArgumentException("Byte array is too large.");

            WriteInt16(buf, ref pos, bytes.Length);
            Array.Copy(bytes, 0, buf, pos, bytes.Length);
            pos += bytes.Length;
        }

        /// <summary>
        /// Writes the 16-bit integer value to the buffer at the specified position,
        /// advancing the position past the written value.  The integer is written
        /// in network (big-endian) order.
        /// </summary>
        /// <param name="buf">The buffer.</param>
        /// <param name="pos">The position.</param>
        /// <param name="value">The value.</param>
        public static void WriteInt16(byte[] buf, ref int pos, int value)
        {
            buf[pos++] = (byte)(value >> 8);
            buf[pos++] = (byte)value;
        }

        /// <summary>
        /// Writes the 32-bit integer value to the buffer at the specified position,
        /// advancing the position past the written value.  The integer is written
        /// in network (big-endian) order.
        /// </summary>
        /// <param name="buf">The buffer.</param>
        /// <param name="pos">The position.</param>
        /// <param name="value">The value.</param>
        public static void WriteInt32(byte[] buf, ref int pos, int value)
        {
            buf[pos++] = (byte)(value >> 24);
            buf[pos++] = (byte)(value >> 16);
            buf[pos++] = (byte)(value >> 8);
            buf[pos++] = (byte)value;
        }

        /// <summary>
        /// Writes the 64-bit integer value to the buffer at the specified position,
        /// advancing the position past the written value.  The integer is written
        /// in network (big-endian) order.
        /// </summary>
        /// <param name="buf">The buffer.</param>
        /// <param name="pos">The position.</param>
        /// <param name="value">The value.</param>
        public static void WriteInt64(byte[] buf, ref int pos, long value)
        {
            buf[pos++] = (byte)(value >> 56);
            buf[pos++] = (byte)(value >> 48);
            buf[pos++] = (byte)(value >> 40);
            buf[pos++] = (byte)(value >> 32);
            buf[pos++] = (byte)(value >> 24);
            buf[pos++] = (byte)(value >> 16);
            buf[pos++] = (byte)(value >> 8);
            buf[pos++] = (byte)value;
        }

        /// <summary>
        /// Writes the float value to the buffer at the specified position,
        /// advancing the position past the written value.
        /// </summary>
        /// <param name="buf">The buffer.</param>
        /// <param name="pos">The position.</param>
        /// <param name="value">The value.</param>
        public static void WriteFloat(byte[] buf, ref int pos, float value)
        {
            WriteString16(buf, ref pos, value.ToString());
        }

        /// <summary>
        /// Writes the string value to the buffer at the specified position,
        /// advancing the position past the written value.
        /// </summary>
        /// <param name="buf">The buffer.</param>
        /// <param name="pos">The position.</param>
        /// <param name="value">The value.</param>
        /// <remarks>
        /// <note>
        /// <c>null</c> strings may be written using this method and that
        /// up to ushort.MaxValue-1 UTF-8 bytes may be written.
        /// </note>
        /// </remarks>
        public static void WriteString16(byte[] buf, ref int pos, string value)
        {
            if (value == null)
            {
                WriteInt16(buf, ref pos, ushort.MaxValue);
                return;
            }

            int cb;
            int cbPos;

            cbPos = pos;
            pos += 2;
            cb = Encoding.UTF8.GetBytes(value, 0, value.Length, buf, pos);

            if (cb >= ushort.MaxValue)
                throw new ArgumentException("String size exceeds ushort.MaxValue-1 after converting to UTF-8.");

            WriteInt16(buf, ref cbPos, cb);
            pos += cb;
        }

        /// <summary>
        /// Writes the string passed as ANSI 8-bit characters with a preceeding length
        /// byte to a buffer, advancing the position past the written value.
        /// </summary>
        /// <param name="buf">The buffer.</param>
        /// <param name="pos">The position.</param>
        /// <param name="value">The string to be written.</param>
        /// <remarks>
        /// This method can write strings of length up to 256 characters.  Note
        /// that null strings cannot be written by this method.
        /// </remarks>
        public static void Write8ANSIString(byte[] buf, ref int pos, string value)
        {
            if (value == null || value.Length > 256)
                throw new ArgumentException("Invalid string.", "value");

            WriteByte(buf, ref pos, (byte)value.Length);
            ansiEncoding.GetBytes(value, 0, value.Length, buf, pos);
            pos += value.Length;
        }

        /// <summary>
        /// Reads the boolean value from the position specified, advancing
        /// the position past the value read.
        /// </summary>
        /// <param name="buf">The buffer.</param>
        /// <param name="pos">The position.</param>
        /// <returns>
        /// The boolean value.
        /// </returns>
        public static bool ReadBool(byte[] buf, ref int pos)
        {
            return buf[pos++] == 1 ? true : false;
        }

        /// <summary>
        /// Reads the byte value from the position specified, advancing
        /// the position past the value read.
        /// </summary>
        /// <param name="buf">The buffer.</param>
        /// <param name="pos">The position.</param>
        /// <returns>
        /// The byte value.
        /// </returns>
        public static byte ReadByte(byte[] buf, ref int pos)
        {
            return buf[pos++];
        }

        /// <summary>
        /// Reads the <see cref="Guid" /> value from the position specified, advancing
        /// the position past the value read.
        /// </summary>
        /// <param name="buf">The buffer.</param>
        /// <param name="pos">The position.</param>
        /// <returns>
        /// The <see cref="Guid" /> value.
        /// </returns>
        public static Guid ReadGuid(byte[] buf, ref int pos)
        {
            return new Guid(ReadBytes(buf, ref pos, 16));
        }

        /// <summary>
        /// Reads a specified number of bytes from a buffer and returns the
        /// result as a byte array, advancing the position past the bytes read.
        /// </summary>
        /// <param name="buf">The buffer.</param>
        /// <param name="pos">The position.</param>
        /// <param name="count">The number of bytes to be read.</param>
        /// <returns>
        /// The bytes read.
        /// </returns>
        public static byte[] ReadBytes(byte[] buf, ref int pos, int count)
        {
            byte[] result;

            if (pos + count > buf.Length)
                throw new IndexOutOfRangeException();

            result = new byte[count];
            Array.Copy(buf, pos, result, 0, count);
            pos += count;

            return result;
        }

        /// <summary>
        /// Reads the byte array from the position specified, advancing
        /// the position past the value read.
        /// </summary>
        /// <param name="buf">The buffer.</param>
        /// <param name="pos">The position.</param>
        /// <returns>
        /// The byte array.
        /// </returns>
        /// <remarks>
        /// <para>
        /// This methods reads byte arrays encoded as a 16-bit length
        /// (big-endian) followed by the array bytes.  null byte
        /// arrays are encoded using a length of (ushort.MaxValue-1).
        /// </para>
        /// </remarks>
        public static byte[] ReadBytes16(byte[] buf, ref int pos)
        {
            byte[]  bytes;
            int     cb;

            cb = ReadInt16(buf, ref pos);
            if (cb == ushort.MaxValue)
                return null;

            cb = (ushort)cb;

            bytes = new byte[cb];
            Array.Copy(buf, pos, bytes, 0, cb);
            pos += cb;

            return bytes;
        }

        /// <summary>
        /// Reads the 16-bit integer value in big-endian order from the position specified, advancing
        /// the position past the value read.
        /// </summary>
        /// <param name="buf">The buffer.</param>
        /// <param name="pos">The position.</param>
        /// <returns>
        /// The integer value.
        /// </returns>
        public static int ReadInt16(byte[] buf, ref int pos)
        {
            int i;

            i    = (buf[pos + 0] << 8) | buf[pos + 1];
            pos += 2;

            return i;
        }

        /// <summary>
        /// Reads the 32-bit integer value in big-endian order from the position specified, advancing
        /// the position past the value read.
        /// </summary>
        /// <param name="buf">The buffer.</param>
        /// <param name="pos">The position.</param>
        /// <returns>
        /// The integer value.
        /// </returns>
        public static int ReadInt32(byte[] buf, ref int pos)
        {
            int i;

            i    = (buf[pos + 0] << 24) | (buf[pos + 1] << 16) | (buf[pos + 2] << 8) | buf[pos + 3];
            pos += 4;

            return i;
        }

        /// <summary>
        /// Reads the 64-bit integer value in big-endian order from the position specified, advancing
        /// the position past the value read.
        /// </summary>
        /// <param name="buf">The buffer.</param>
        /// <param name="pos">The position.</param>
        /// <returns>
        /// The integer value.
        /// </returns>
        public static long ReadInt64(byte[] buf, ref int pos)
        {
            long l;

            l = ((long)buf[pos + 0] << 56) |
                ((long)buf[pos + 1] << 48) |
                ((long)buf[pos + 2] << 40) |
                ((long)buf[pos + 3] << 32) |
                ((long)buf[pos + 4] << 24) |
                ((long)buf[pos + 5] << 16) |
                ((long)buf[pos + 6] << 8) |
                ((long)buf[pos + 7]);

            pos += 8;
            return l;
        }

        /// <summary>
        /// Reads the 16-bit integer value in little-endian order from the position specified, advancing
        /// the position past the value read.
        /// </summary>
        /// <param name="buf">The buffer.</param>
        /// <param name="pos">The position.</param>
        /// <returns>
        /// The integer value.
        /// </returns>
        public static int ReadInt16Le(byte[] buf, ref int pos)
        {
            int i;

            i    = (buf[pos + 1] << 8) | buf[pos + 0];
            pos += 2;

            return i;
        }

        /// <summary>
        /// Reads the 32-bit integer value in little-endian order from the position specified, advancing
        /// the position past the value read.
        /// </summary>
        /// <param name="buf">The buffer.</param>
        /// <param name="pos">The position.</param>
        /// <returns>
        /// The integer value.
        /// </returns>
        public static int ReadInt32Le(byte[] buf, ref int pos)
        {
            int i;

            i    = (buf[pos + 3] << 24) | (buf[pos + 2] << 16) | (buf[pos + 1] << 8) | buf[pos + 0];
            pos += 4;

            return i;
        }

        /// <summary>
        /// Reads the 64-bit integer value in little-endian order from the position specified, advancing
        /// the position past the value read.
        /// </summary>
        /// <param name="buf">The buffer.</param>
        /// <param name="pos">The position.</param>
        /// <returns>
        /// The integer value.
        /// </returns>
        public static long ReadInt64Le(byte[] buf, ref int pos)
        {
            long l;

            l = ((long)buf[pos + 7] << 56) |
                ((long)buf[pos + 6] << 48) |
                ((long)buf[pos + 5] << 40) |
                ((long)buf[pos + 4] << 32) |
                ((long)buf[pos + 3] << 24) |
                ((long)buf[pos + 2] << 16) |
                ((long)buf[pos + 1] << 8) |
                ((long)buf[pos + 0]);

            pos += 8;
            return l;
        }

        /// <summary>
        /// Reads the float value from the position specified, advancing
        /// the position past the value read.
        /// </summary>
        /// <param name="buf">The buffer.</param>
        /// <param name="pos">The position.</param>
        /// <returns>
        /// The float value.
        /// </returns>
        public static float ReadFloat(byte[] buf, ref int pos)
        {
            return float.Parse(ReadString16(buf, ref pos));
        }

        /// <summary>
        /// Reads the string value from the position specified, advancing
        /// the position past the value read.
        /// </summary>
        /// <param name="buf">The buffer.</param>
        /// <param name="pos">The position.</param>
        /// <returns>
        /// The string value.
        /// </returns>
        public static string ReadString16(byte[] buf, ref int pos)
        {
            int     cb;
            string  s;

            cb = ReadInt16(buf, ref pos);
            if (cb == ushort.MaxValue)
                return null;

            cb = (ushort)cb;

            s = Encoding.UTF8.GetString(buf, pos, cb);
            pos += cb;

            return s;
        }

        /// <summary>
        /// Reads an ANSI string preceeded with a length byte from a buffer,
        /// advancing the position past the value read.
        /// </summary>
        /// <param name="buf">The buffer.</param>
        /// <param name="pos">The position.</param>
        /// <returns>The string read from the buffer.</returns>
        public static string Read8ANSIString(byte[] buf, ref int pos)
        {
            int     cb;
            string  s;

            cb   = ReadByte(buf, ref pos);
            s    = ansiEncoding.GetString(buf, pos, cb);
            pos += cb;

            return s;
        }

        /// <summary>
        /// Strips all whitespace from a string.
        /// </summary>
        /// <param name="s">The input string.</param>
        /// <returns>The string with all whitespace removed.</returns>
        public static string StripWhitespace(string s)
        {
            StringBuilder sb;

            if (s.IndexOfAny(whitespace) == -1)
                return s;

            sb = new StringBuilder(s.Length);
            for (int i = 0; i < s.Length; i++)
            {
                char ch = s[i];

                switch (ch)
                {

                    case ' ':
                    case '\t':
                    case '\r':
                    case '\n':

                        break;

                    default:

                        sb.Append(ch);
                        break;
                }
            }

            return sb.ToString();
        }

#if !MOBILE_DEVICE

        /// <summary>
        /// Immediately kills the current process.
        /// </summary>
        /// <remarks>
        /// This method abstracts the differences between the Win32 and WinCE
        /// frameworks.
        /// </remarks>
        public static void Exit()
        {
            Environment.Exit(0);
        }

        /// <summary>
        /// Restarts the machine.  This method does not return.
        /// </summary>
        public static void RestartComputer()
        {
            if (IsWindows)
                ShutDownWindows(true);
            else
                ShutDownUnix(true);

            // This method never returns.  Keep spinning until the
            // computer shuts down.

            while (true)
                Thread.Sleep(1000);
        }

        /// <summary>
        /// Restarts the machine.  This method does not return.
        /// </summary>
        public static void PowerDownComputer()
        {
            if (IsWindows)
                ShutDownWindows(false);
            else
                ShutDownUnix(false);

            // This method never returns.  Keep spinning until the
            // computer shuts down.

            while (true)
                Thread.Sleep(1000);
        }

        /// <summary>
        /// Shuts a Windows operating system down, optionally restarting it.
        /// </summary>
        /// <param name="restart">Pass <c>true</c> to restart the operating system.</param>
        private static void ShutDownWindows(bool restart)
        {
            if (restart)
            {
                WinApi.ExitWindows(true);
            }
            else
            {
                WinApi.ExitWindows(false);
            }
        }

        /// <summary>
        /// Shuts a Unix/Linx derivitive operating system down, optionally restarting it.
        /// </summary>
        /// <param name="restart">Pass <c>true</c> to restart the operating system.</param>
        private static void ShutDownUnix(bool restart)
        {
            // Note that I'm giving processes 30 seconds to cleanup
            // before the OS stops.

            Execute("shutdown", restart ? "-t 30 -r now"
                                       : "-t 30 -h now");
        }

#endif // !MOBILE_DEVICE

        /// <summary>
        /// Creates and starts a non-parameterized thread.
        /// </summary>
        /// <param name="name">The thread name (or <c>null</c>).</param>
        /// <param name="target">The thread target method.</param>
        /// <returns>The created <see cref="Thread" />.</returns>
        public static Thread StartThread(string name, Action target)
        {
            var thread = new Thread(new ThreadStart(target));

            if (!string.IsNullOrWhiteSpace(name))
                thread.Name = name;

            thread.Start();
            return thread;
        }

        /// <summary>
        /// Creates and starts a parameterized thread.
        /// </summary>
        /// <param name="name">The thread name (or <c>null</c>).</param>
        /// <param name="parameter">The parameter to be passed to the thread.</param>
        /// <param name="target">The thread target method.</param>
        /// <returns>The created <see cref="Thread" />.</returns>
        public static Thread StartThread(string name, object parameter, Action<object> target)
        {
            var thread = new Thread(new ParameterizedThreadStart(target));

            if (!string.IsNullOrWhiteSpace(name))
                thread.Name = name;

            thread.Start(parameter);
            return thread;
        }

        /// <summary>
        /// Waits up to a specified timeout duration for a thread to terminate
        /// normally before forcing the thread to abort.
        /// </summary>
        /// <param name="thread">The thread.</param>
        /// <param name="timeout">The maximum time to wait.</param>
        public static void JoinThread(Thread thread, TimeSpan timeout)
        {
#if SILVERLIGHT
            if (!thread.Join((int) timeout.TotalMilliseconds))
                thread.Abort();
#else
            if (!thread.Join(timeout))
                thread.Abort();
#endif
        }

        /// <summary>
        /// Waits up to a specified timeout duration for a thread to terminate
        /// normally before forcing the thread to abort.
        /// </summary>
        /// <param name="thread">The thread.</param>
        /// <param name="milliseconds">The maximum time to wait in milliseconds.</param>
        public static void JoinThread(Thread thread, int milliseconds)
        {
            if (!thread.Join(milliseconds))
                thread.Abort();
        }

        /// <summary>
        /// This method converts a string containing simple '?' and '*' wildcards into
        /// the corresponding Regex expression.
        /// </summary>
        /// <param name="pattern">The input pattern.</param>
        public static string WildcardRegex(string pattern)
        {
            var sb = new StringBuilder(pattern.Length + 32);

            sb.Append("^(");

            for (int i = 0; i < pattern.Length; i++)
            {
                char ch = pattern[i];

                switch (ch)
                {
                    case '[':

                        sb.Append(@"\[");
                        break;

                    case '|':

                        sb.Append(@"\|");
                        break;

                    case '\\':

                        sb.Append(@"\\");
                        break;

                    case '.':

                        sb.Append(@"\.");
                        break;

                    case '$':

                        sb.Append(@"\$");
                        break;

                    case '^':

                        sb.Append(@"\^");
                        break;

                    case '+':

                        sb.Append(@"\+");
                        break;

                    case '(':

                        sb.Append(@"\(");
                        break;

                    case ')':

                        sb.Append(@"\)");
                        break;

                    case '*':

                        sb.Append(@".*");
                        break;

                    case '?':

                        sb.Append(@".?");
                        break;

                    default:

                        sb.Append(ch);
                        break;
                }
            }

            sb.Append(")$");

            return sb.ToString();
        }

        /// <summary>
        /// This method converts the patterns containing simple '?' and '*' wildcards
        /// into a Regex expression that looks for the OR of all the patterns.
        /// </summary>
        /// <param name="patterns">The array of wildcarded patterns.</param>
        public static string WildcardRegex(string[] patterns)
        {
            var sb = new StringBuilder();

            for (int i = 0; i < patterns.Length; i++)
                sb.AppendFormat(null, "{0}{1}", i > 0 ? "|" : "", WildcardRegex(patterns[i]));

            return sb.ToString();
        }

#if !SILVERLIGHT

        /// <summary>
        /// Implements a cross platform version of Environment.MachineName and for
        /// Windows/CE this property also implements a setter.
        /// </summary>
        /// <remarks>
        /// <note>
        /// You'll need to restart the machine after setting a new
        /// MachineName for the change to take effect.
        /// </note>
        /// </remarks>
        public static string MachineName
        {
            get
            {
                return Environment.MachineName;
            }
        }

#endif // !SILVERLIGHT

        /// <summary>
        /// Parses the string passed as a list of substrings separated by the
        /// character passed.
        /// </summary>
        /// <param name="source">The source string.</param>
        /// <param name="separator">The separator character.</param>
        /// <returns>The array of list elements parsed.</returns>
        /// <remarks>
        /// <note>
        /// The elements of the will be trimmed and also that this
        /// method will always return an array with at least one element.
        /// </note>
        /// </remarks>
        public static string[] ParseList(string source, char separator)
        {

            List<string>    list = new List<string>();
            int             p, pEnd;

            p = 0;
            while (true)
            {
                pEnd = source.IndexOf(separator, p);
                if (pEnd == -1)
                {
                    list.Add(source.Substring(p).Trim());
                    break;
                }

                if (pEnd > p)
                {
                    string trimmed = source.Substring(p, pEnd - p).Trim();

                    if (trimmed.Length > 0)
                        list.Add(source.Substring(p, pEnd - p).Trim());
                }

                p = pEnd + 1;
                if (p >= source.Length)
                    break;
            }

            if (list.Count == 0)
                list.Add(string.Empty);

            return list.ToArray();
        }

        /// <summary>
        /// Reflects the type passed for the custom attribute of a specific type.
        /// </summary>
        /// <param name="type">The type to reflect.</param>
        /// <param name="attributeType">The type of attribute instances to be returned.</param>
        /// <param name="inherit"><c>true</c> to consider inherited attributes.</param>
        /// <returns>The first matching attribute found or <c>null</c>.</returns>
        public static System.Attribute GetCustomAttribute(System.Type type, System.Type attributeType, bool inherit)
        {
            object[] attrs;

            attrs = type.GetCustomAttributes(attributeType, inherit);
            if (attrs.Length == 0)
                return null;
            else
                return (System.Attribute)attrs[0];
        }

        /// <summary>
        /// Reflects the type passed for custom attributes of a specific type.
        /// </summary>
        /// <param name="type">The type to reflect.</param>
        /// <param name="attributeType">The type of attribute instances to be returned.</param>
        /// <param name="inherit"><c>true</c> to consider inherited attributes.</param>
        /// <returns>The set of matchings attributes will be returned.</returns>
        public static System.Attribute[] GetCustomAttributes(System.Type type, System.Type attributeType, bool inherit)
        {
            object[]        attrs;
            Attribute[]     result;

            attrs = type.GetCustomAttributes(attributeType, inherit);
            result = new Attribute[attrs.Length];
            attrs.CopyTo(result, 0);

            return result;
        }

        /// <summary>
        /// Converts a value passed into UTF-8 bytes.
        /// </summary>
        /// <param name="value">The string to be converted.</param>
        /// <returns>The array of UTF-8 bytes.</returns>
        /// <remarks>
        /// <note>
        /// The method returns <c>null</c> if the parameter is <c>null</c>.
        /// </note>
        /// </remarks>
        public static byte[] ToUTF8(string value)
        {
            if (value == null)
                return null;

            return Encoding.UTF8.GetBytes(value);
        }

        /// <summary>
        /// Converts UTF-8 bytes into a string.
        /// </summary>
        /// <param name="buffer">The UTF-8 encoded string.</param>
        /// <returns>The converted string.</returns>
        /// <remarks>
        /// <note>
        /// The method returns <c>null</c> if the parameter is <c>null</c>.
        /// </note>
        /// <note>
        /// This method removes the UTF-8 preamble bytes if present.
        /// </note>
        /// </remarks>
        public static string FromUTF8(byte[] buffer)
        {
            if (buffer == null)
                return null;

            if (buffer.Length >= utf8Preamble.Length)
            {
                bool match = true;

                for (int i = 0; i < utf8Preamble.Length; i++)
                    if (buffer[i] != utf8Preamble[i])
                    {
                        match = false;
                        break;
                    }

                if (match)
                    return FromUTF8(buffer, utf8Preamble.Length);
            }

            return Encoding.UTF8.GetString(buffer, 0, buffer.Length);
        }

        /// <summary>
        /// Converts UTF-8 bytes from the specified position in a buffer to
        /// the end of the buffer into a string.
        /// </summary>
        /// <param name="buffer">The UTF-8 encoded string.</param>
        /// <param name="index">Index of the first byte to be converted.</param>
        /// <returns>The converted string.</returns>
        /// <remarks>
        /// <note>
        /// The method returns <c>null</c> if the parameter is <c>null</c>.
        /// </note>
        /// </remarks>
        public static string FromUTF8(byte[] buffer, int index)
        {
            if (buffer == null)
                return null;

            return Encoding.UTF8.GetString(buffer, index, buffer.Length - index);
        }

        /// <summary>
        /// Converts UTF-8 bytes from the specified range of a buffer into a string.
        /// </summary>
        /// <param name="buffer">The UTF-8 encoded string.</param>
        /// <param name="index">Index of the first byte to be converted.</param>
        /// <param name="length">The number of bytes to convert.</param>
        /// <returns>The converted string.</returns>
        /// <remarks>
        /// <note>
        /// The method returns <c>null</c> if the parameter is <c>null</c>.
        /// </note>
        /// </remarks>
        public static string FromUTF8(byte[] buffer, int index, int length)
        {
            if (buffer == null)
                return null;

            return Encoding.UTF8.GetString(buffer, index, length);
        }

        /// <summary>
        /// Converts a value passed into ANSI bytes.
        /// </summary>
        /// <param name="value">The string to be converted.</param>
        /// <returns>The array of ANSI bytes.</returns>
        /// <remarks>
        /// <note>
        /// The method returns <c>null</c> if the parameter is <c>null</c>.
        /// </note>
        /// </remarks>
        public static byte[] ToAnsi(string value)
        {
            if (value == null)
                return null;

            return ansiEncoding.GetBytes(value);
        }

        /// <summary>
        /// Converts ANSI bytes into a string.
        /// </summary>
        /// <param name="buffer">The ANSI encoded string.</param>
        /// <returns>The converted string.</returns>
        /// <remarks>
        /// <note>
        /// The method returns <c>null</c> if the parameter is <c>null</c>.
        /// </note>
        /// </remarks>
        public static string FromAnsi(byte[] buffer)
        {
            if (buffer == null)
                return null;

            return ansiEncoding.GetString(buffer, 0, buffer.Length);
        }

        /// <summary>
        /// Converts ANSI bytes from the specified position in a buffer to
        /// the end of the buffer into a string.
        /// </summary>
        /// <param name="buffer">The UTF-8 encoded string.</param>
        /// <param name="index">Index of the first byte to be converted.</param>
        /// <returns>The converted string.</returns>
        /// <remarks>
        /// <note>
        /// The method returns <c>null</c> if the parameter is <c>null</c>.
        /// </note>
        /// </remarks>
        public static string FromAnsi(byte[] buffer, int index)
        {
            if (buffer == null)
                return null;

            return ansiEncoding.GetString(buffer, index, buffer.Length - index);
        }

        /// <summary>
        /// Converts ANSI bytes from the specified range of a buffer into a string.
        /// </summary>
        /// <param name="buffer">The UTF-8 encoded string.</param>
        /// <param name="index">Index of the first byte to be converted.</param>
        /// <param name="length">The number of bytes to convert.</param>
        /// <returns>The converted string.</returns>
        /// <remarks>
        /// <note>
        /// The method returns <c>null</c> if the parameter is <c>null</c>.
        /// </note>
        /// </remarks>
        public static string FromAnsi(byte[] buffer, int index, int length)
        {
            if (buffer == null)
                return null;

            return ansiEncoding.GetString(buffer, index, length);
        }

        /// <summary>
        /// Returns an integer pseudo random number.
        /// </summary>
        public static int Rand()
        {
            lock (syncLock)
            {
                if (rand == null)
                    rand = new Random(Environment.TickCount ^ (int)DateTime.Now.Ticks);

                return rand.Next();
            }
        }

        /// <summary>
        /// Returns a double pseudo random number between 0.0 and +1.0
        /// </summary>
        public static double RandDouble()
        {
            lock (syncLock)
            {
                if (rand == null)
                    rand = new Random(Environment.TickCount ^ (int)DateTime.Now.Ticks);

                return rand.NextDouble();
            }
        }

        /// <summary>
        /// Returns a double pseudo random number between 0.0 and the specified limit.
        /// </summary>
        /// <param name="limit">The limit.</param>
        public static double RandDouble(double limit)
        {
            lock (syncLock)
            {
                if (rand == null)
                    rand = new Random(Environment.TickCount ^ (int)DateTime.Now.Ticks);

                return rand.NextDouble() * limit;
            }
        }

        /// <summary>
        /// Returns a pseudo random number in the range of 0..limit-1.
        /// </summary>
        /// <param name="limit">The value returned will not exceed one less than this value.</param>
        /// <returns>The random number.</returns>
        public static int Rand(int limit)
        {
            int v;

            v = Rand();
            if (v == int.MinValue)
                v = 0;
            else if (v < 0)
                v = -v;

            return v % limit;
        }

        /// <summary>
        /// Returns a random index into an array whose length is specified.
        /// </summary>
        /// <param name="length">The array length.</param>
        /// <returns>The random index.</returns>
        /// <exception cref="IndexOutOfRangeException">Thrown if length is &lt;= 0.</exception>
        public static int RandIndex(int length)
        {
            if (length <= 0)
                throw new IndexOutOfRangeException();

            return Rand() % length;
        }

        /// <summary>
        /// Returns a random <see cref="TimeSpan"/> between zero and a specified maximum.
        /// </summary>
        /// <param name="maxInterval">The maximum interval.</param>
        /// <returns>The random timespan.</returns>
        /// <remarks>
        /// This method is useful for situations where its desirable to have some variation
        /// in a delay before performing an activity like retrying an operation or performing
        /// a background task.
        /// </remarks>
        public static TimeSpan RandTimespan(TimeSpan maxInterval)
        {
            return TimeSpan.FromSeconds(maxInterval.TotalSeconds * RandDouble());
        }

        /// <summary>
        /// Returns a <see cref="TimeSpan"/> between the specified base interval
        /// plus a random period of the specified fraction of the value.
        /// </summary>
        /// <param name="baseInterval">The base interval.</param>
        /// <param name="fraction">The fractional multiplier for the random component.</param>
        /// <returns>The random timespan.</returns>
        /// <remarks>
        /// <para>
        /// The value returned is at least as large as <paramref name="baseInterval" /> with an
        /// added random fractional interval if <paramref name="fraction" /> is positive or the value
        /// returned may be less than <paramref name="baseInterval" /> for a negative <paramref name="fraction" />.  
        /// This is computed via:
        /// </para>
        /// <code language="cs">
        /// baseInterval + Helper.RandTimespan(TimeSpan.FromSeconds(baseInterval.TotalSeconds * fraction));
        /// </code>
        /// <para>
        /// This method is useful for situations where its desirable to have some variation
        /// in a delay before performing an activity like retrying an operation or performing
        /// a background task.
        /// </para>
        /// </remarks>
        public static TimeSpan RandTimespan(TimeSpan baseInterval, double fraction)
        {
            if (fraction == 0.0)
                return baseInterval;

            return baseInterval + Helper.RandTimespan(TimeSpan.FromSeconds(baseInterval.TotalSeconds * fraction));
        }

        /// <summary>
        /// Returns a random <see cref="TimeSpan" /> value between the min/max
        /// values specified.
        /// </summary>
        /// <param name="minInterval">The minimum interval.</param>
        /// <param name="maxInterval">The maximum interval.</param>
        /// <returns>The randomized time span.</returns>
        public static TimeSpan RandTimespan(TimeSpan minInterval, TimeSpan maxInterval)
        {
            if (maxInterval < minInterval)
            {
                // Just being safe.

                var tmp = maxInterval;

                maxInterval = minInterval;
                minInterval = maxInterval;
            }

            return minInterval + TimeSpan.FromSeconds((maxInterval - minInterval).TotalSeconds * rand.NextDouble());
        }

        /// <summary>
        /// Converts the encoded RTF bytes passed into an RTF string
        /// suitable for assigning to a RichTextBox control.
        /// </summary>
        /// <param name="buffer">The encoded RTF.</param>
        /// <returns>The decoded RTF string.</returns>
        public static string ToRtf(byte[] buffer)
        {
            // RTF is basically just ANSI encoded text, possibly
            // terminated by a zero byte.

            int cb;

            cb = buffer.Length;
            if (buffer[cb - 1] == 0)
                cb--;

            return ansiEncoding.GetString(buffer, 0, cb);
        }

        /// <summary>
        /// Replaces any CRLF, CR, or LF character sequences in the string passed with a space.
        /// </summary>
        /// <param name="value">The string to be processed.</param>
        /// <returns>The string after processing.</returns>
        /// <remarks>
        /// <note>
        /// This method tolerates <c>null</c> input by return <see cref="string.Empty" />.
        /// </note>
        /// </remarks>
        public static string StripCRLF(string value)
        {
            if (value == null)
                return string.Empty;

            if (value.IndexOfAny(crlfArray) == -1)
                return value;

            value = value.Replace("\r\n", " ");
            value = value.Replace("\r", " ");
            value = value.Replace("\n", " ");

            return value;
        }

#if !SILVERLIGHT

        /// <summary>
        /// Returns the version of an assembly.
        /// </summary>
        /// <param name="assembly">The assembly.</param>
        /// <returns>The assemby's version number.</returns>
        public static Version GetVersion(Assembly assembly)
        {
            return assembly.GetName().Version;
        }

        /// <summary>
        /// Returns the formatted version of an assembly.
        /// </summary>
        /// <param name="assembly">The assembly.</param>
        /// <returns>The assembly's version number.</returns>
        public static string GetVersionString(Assembly assembly)
        {
            var v = GetVersion(assembly);

            return string.Format("{0}.{1}.{2:0###}.{3}", v.Major, v.Minor, v.Build, v.Revision);
        }

        /// <summary>
        /// Returns the version string for the entry assembly if one was set via
        /// a call to <see cref="InitializeApp" /> or <see cref="InitializeWebApp" /> otherwise the
        /// version string for the calling assembly will be returned.
        /// </summary>
        /// <returns>The formatted version.</returns>
        public static string GetVersionString()
        {
            if (entryAssembly != null)
                return GetVersionString(entryAssembly);
            else
                return GetVersionString(Assembly.GetCallingAssembly());
        }

#endif // !SILVERLIGHT

        /// <summary>
        /// Renders a <see cref="Version" /> instance into a formatted string.
        /// </summary>
        /// <param name="v">The <see cref="Version" />.</param>
        /// <returns>The formatted version.</returns>
        /// <remarks>
        /// <note>
        /// This method formats the build number using 4 digits (as in <b>1.0.0001.0</b>) as opposed
        /// to what <see cref="Version" />'s implementation of <see cref="Version.ToString()" /> 
        /// does, which does not expand the build number.
        /// </note>
        /// </remarks>
        public static string GetVersionString(Version v)
        {
            return string.Format("{0}.{1}.{2:0###}.{3}", v.Major, v.Minor, v.Build, v.Revision);
        }

        /// <summary>
        /// Returns the copyright statement from an assembly.
        /// </summary>
        /// <param name="assembly">The assembly.</param>
        /// <returns>The assembly's copyright statement.</returns>
        public static string GetCopyright(Assembly assembly)
        {
            AssemblyCopyrightAttribute attr;

            attr = (AssemblyCopyrightAttribute)AssemblyCopyrightAttribute.GetCustomAttribute(assembly, typeof(AssemblyCopyrightAttribute));
            if (attr == null)
                return string.Empty;
            else
                return attr.Copyright;
        }

        /// <summary>
        /// Sttempts to parse the value passed as an Internet standard UTC date.
        /// </summary>
        /// <param name="value">The value to be parsed.</param>
        /// <remarks>
        /// <para>
        /// This property attempts to parse the string value 
        /// using the commonly used date formats:
        /// </para>
        /// <code language="none">
        ///     Sun, 06 Nov 1994 08:49:37 GMT  ; RFC 822, updated by RFC 1123
        ///     Sunday, 06-Nov-94 08:49:37 GMT ; RFC 850, obsoleted by RFC 1036
        ///     Sun Nov  6 08:49:37 1994       ; ANSI C's asctime() format
        /// </code>
        /// </remarks>
        public static DateTime ParseInternetDate(string value)
        {
            string  v;
            int     pos;

            v = value;
            if (v.IndexOf("GMT") != -1)
                v = v.Replace("GMT", string.Empty).Trim();

            // I'm going to distinguish between date formats by looking
            // at how the day of week is formatted.

            pos = v.IndexOf(' ');
            if (pos == -1)
                throw new FormatException("Invalid date.");

            switch (pos)
            {
                case 3:        // Sun Nov  6 08:49:37 1994       ; ANSI C's asctime() format

                    return DateTime.ParseExact(v, "ddd MMM d HH:mm:ss yyyy", null, DateTimeStyles.AllowWhiteSpaces);

                case 4:        // Sun, 06 Nov 1994 08:49:37 GMT  ; RFC 822, updated by RFC 1123

                    return DateTime.ParseExact(v, "ddd, dd MMM yyyy HH:mm:ss", null, DateTimeStyles.AllowWhiteSpaces);

                case 7:        // Sunday, 06-Nov-94 08:49:37 GMT ; RFC 850, obsoleted by RFC 1036

                    return DateTime.ParseExact(v, "dddd, dd-MMM-yy HH:mm:ss", null, DateTimeStyles.AllowWhiteSpaces);

                default:

                    // Allow the .NET framework to do the best it can.

                    return DateTime.Parse(v);
            }
        }

        /// <summary>
        /// Renders the UTC <see cref="DateTime" /> value passed as a standard Internet date string.
        /// </summary>
        /// <param name="time">The time value to be rendered.</param>
        /// <returns>A formatted string as described in RFC 822, and updated by RFC 1123.</returns>
        public static string ToInternetDate(DateTime time)
        {
            return time.ToString("r");
        }

        /// <summary>
        /// Parses an ISO 8601 formatted date string and returns the resulting
        /// <see cref="DateTime" /> value.
        /// </summary>
        /// <param name="value">The input string.</param>
        /// <returns>The parsed  <see cref="DateTime" /> value.</returns>
        /// <remarks>
        /// </remarks>
        public static DateTime ParseIsoDate(string value)
        {
            int pLastColon;
            int pDecimal;

            value = value.Trim();
            if (value.EndsWith("z") || value.EndsWith("Z"))
                value = value.Substring(0, value.Length - 1);

            // Normalize the date to have at exactly three digits
            // of fractional seconds.

            pLastColon = value.LastIndexOf(':');
            pDecimal = value.IndexOf('.', pLastColon);

            if (pDecimal == -1)
                value += ".000";
            else
            {
                int cDigits = value.Length - pDecimal - 1;

                switch (cDigits)
                {

                    case 0: value += "000"; break;
                    case 1: value += "00"; break;
                    case 2: value += "0"; break;
                    case 3: break;
                    default:

                        value = value.Substring(0, value.Length - cDigits + 3);
                        break;
                }
            }

            return DateTime.ParseExact(value, "yyyy-MM-ddTHH:mm:ss.fff", null);
        }

        /// <summary>
        /// Renders the UTC <see cref="DateTime" /> value passed in standard ISO 8601
        /// format <b>2007-07-20T14:52:15Z</b>.
        /// </summary>
        /// <param name="time">The time value to be rendered.</param>
        /// <returns>The ISO formatted date string.</returns>
        public static string ToIsoDate(DateTime time)
        {
            return time.ToString("yyyy-MM-ddTHH:mm:ssZ");
        }

        /// <summary>
        /// Renders the <see cref="DateTime" /> value passed in a form suitable for
        /// tracing output: HH:MM:SS:mmm.
        /// </summary>
        /// <param name="time">The time.</param>
        /// <returns>The formatted output.</returns>
        public static string ToTrace(DateTime time)
        {
            return string.Format("{0:0#}:{1:0#}:{2:0#}:{3:0##}", time.Hour, time.Minute, time.Second, time.Millisecond);
        }

        /// <summary>
        /// Returns the current time (UTC) rounded so that it can be converted back and
        /// forth into its RFC 123 format without changing.  This is useful mostly
        /// for unit testing.
        /// </summary>
        public static DateTime UtcNowRounded
        {
            get { return Helper.ParseInternetDate(Helper.ToInternetDate(DateTime.UtcNow)); }
        }

        /// <summary>
        /// Converts the string passed into a form suitable for use as an
        /// expression constant in one of the System.Data classes by adding
        /// single quotes and adding the appropriate escape codes.
        /// </summary>
        /// <param name="value">The value to be converted.</param>
        /// <returns>The quoted string.</returns>
        public static string QuoteQueryString(string value)
        {
            var     sb = new StringBuilder(value.Length + 2 + value.Length / 10);
            char    ch;

            sb.Append('\'');

            for (int i = 0; i < value.Length; i++)
            {
                ch = value[i];
                switch (ch)
                {
                    case (char)0x0D:

                        sb.Append("\\r");
                        break;

                    case (char)0x0A:

                        sb.Append("\\n");
                        break;

                    case (char)0x09:

                        sb.Append("\\t");
                        break;

                    case '\\':
                    case '\'':
                    case '"':

                        sb.Append('\\');
                        sb.Append(ch);
                        break;

                    default:

                        sb.Append(ch);
                        break;
                }
            }

            sb.Append('\'');

            return sb.ToString();
        }

        /// <summary>
        /// Converts the string passed into a form suitable for use as an
        /// expression column name in one of the System.Data classes by adding
        /// single quotes and adding the appropriate escape codes.
        /// </summary>
        /// <param name="value">The value to be converted.</param>
        /// <returns>The quoted string.</returns>
        public static string QuoteQueryColumn(string value)
        {
            var     sb = new StringBuilder(value.Length + 2 + value.Length / 10);
            char    ch;

            sb.Append('[');

            for (int i = 0; i < value.Length; i++)
            {

                ch = value[i];
                switch (ch)
                {

                    case (char)0x0D:

                        sb.Append("\\r");
                        break;

                    case (char)0x0A:

                        sb.Append("\\n");
                        break;

                    case (char)0x09:

                        sb.Append("\\t");
                        break;

                    case '~':
                    case '(':
                    case ')':
                    case '#':
                    case '\\':
                    case '/':
                    case '=':
                    case '>':
                    case '<':
                    case '+':
                    case '-':
                    case '*':
                    case '%':
                    case '&':
                    case '|':
                    case '^':
                    case '\'':
                    case '"':
                    case '[':
                    case ']':

                        sb.Append('\\');
                        sb.Append(ch);
                        break;

                    default:

                        sb.Append(ch);
                        break;
                }
            }

            sb.Append(']');

            return sb.ToString();
        }

#if !MOBILE_DEVICE

        /// <summary>
        /// Returns the fully qualified names of the files and folders at
        /// the specified path in the file system.
        /// </summary>
        /// <param name="path">The path.</param>
        public static string[] GetFilesAndFolders(string path)
        {
            string[]    files;
            string[]    folders;
            string[]    result;

            files   = Directory.GetFiles(path);
            folders = Directory.GetDirectories(path);

            result = new string[files.Length + folders.Length];
            Array.Copy(files, 0, result, 0, files.Length);
            Array.Copy(folders, 0, result, files.Length, folders.Length);

            return result;
        }

        /// <summary>
        /// Returns the fully qualified names of the files and folders at
        /// the specified path in the file system that match a pattern.
        /// </summary>
        /// <param name="path">The path.</param>
        /// <param name="pattern">The search pattern.</param>
        public static string[] GetFilesAndFolders(string path, string pattern)
        {
            string[]    files;
            string[]    folders;
            string[]    result;

            files   = Directory.GetFiles(path, pattern);
            folders = Directory.GetDirectories(path, pattern);

            result = new string[files.Length + folders.Length];
            Array.Copy(files, 0, result, 0, files.Length);
            Array.Copy(folders, 0, result, files.Length, folders.Length);

            return result;
        }

        /// <summary>
        /// Returns the drive specification for disk drive holding the
        /// installed operating system (typically "C:").
        /// </summary>
        public static string SystemDrive
        {
            get
            {
                string  systemFolder;
                int     pos;

                systemFolder = Environment.SystemDirectory;
                pos          = systemFolder.IndexOf(':');

                if (pos == -1)
                {

                    // I'm not sure we'll ever see this in real life.  The only
                    // situation I could see this happening is when booting on a
                    // diskless workstation and the system drive is actually a
                    // network drive.

                    throw new NotImplementedException();
                }
                else
                    return systemFolder.Substring(0, pos + 1);
            }
        }

        /// <summary>
        /// Used internally by unit tests to disable actual file deletion in
        /// <see cref="DeleteFile(string)" /> when performing particularily dangerous
        /// tests.
        /// </summary>
        internal static bool DisableDeleteFile = false;

        /// <summary>
        /// Deletes the file specified by the absolute or relative
        /// path passed.
        /// </summary>
        /// <param name="path">The path and optional pattern specifying the files.</param>
        /// <remarks>
        /// <para>
        /// If the path does not contain wildcard characters (*) or (?), then the method
        /// will delete the referenced file or directory.  If the path is to a directory,
        /// then the directory will be deleted if it's empty.
        /// </para>
        /// <para>
        /// If the path contains wildcards, then all files matching the pattern will be
        /// deleted.
        /// </para>
        /// <para>
        /// To avoid accidental damage to a computer, this method will not perform
        /// the following deletions:
        /// </para>
        /// <list type="table">
        ///     <item>
        ///         <term>&lt;drive letter&gt;:\*.*</term>
        ///         <description>
        ///         This will prevent the wiping of an entire disk drive.  It's suprising
        ///         how easy it is for this to happen accidentally (learned from hard
        ///         experience).
        ///         </description>
        ///     </item>
        ///     <item>
        ///         <term>&lt;system&gt;\*.*</term>
        ///         <description>
        ///         where &lt;system&gt; is the operating system installation directory.
        ///         </description>
        ///     </item>
        ///     <item>
        ///         <term>&lt;program files&gt;\*.*</term>
        ///         <description>
        ///         where &lt;program files&gt; is the root directory where programs
        ///         are installed by default.
        ///         </description>
        ///     </item>
        /// </list>
        /// <note>
        /// This method will delete read-only files and it will not throw
        /// an exception if the file specified does not exist.
        /// </note>
        /// </remarks>
        public static void DeleteFile(string path)
        {
            DeleteFile(path, false);
        }

        /// <summary>
        /// Deletes the file specified by the absolute or relative
        /// path passed.
        /// </summary>
        /// <param name="path">The path and optional pattern specifying the files.</param>
        /// <param name="recursive">Indicates whether directories matching a pattern should be deleted.</param>
        /// <remarks>
        /// <para>
        /// If the path does not contain wildcard characters (*) or (?), then the method
        /// will delete the referenced file or directory.  If the path is to a directory,
        /// and recursive=true, then the contents of the directory will be deleted as well.
        /// </para>
        /// <para>
        /// If the path contains wildcards, then all files matching the pattern will be
        /// deleted.  If recursive=true, then matching directories and their contents
        /// will also be deleted.
        /// </para>
        /// <para>
        /// To avoid accidental damage to a computer, this method will not perform
        /// the following deletions:
        /// </para>
        /// <list type="table">
        ///     <item>
        ///         <term>&lt;drive letter&gt;:\*.*</term>
        ///         <description>
        ///         This will prevent the wiping of an entire disk drive.  It's suprising
        ///         how easy it is for this to happen accidentally (learned from hard
        ///         experience).
        ///         </description>
        ///     </item>
        ///     <item>
        ///         <term>&lt;system&gt;\*.*</term>
        ///         <description>
        ///         where &lt;system&gt; is the operating system installation directory.
        ///         </description>
        ///     </item>
        ///     <item>
        ///         <term>&lt;program files&gt;\*.*</term>
        ///         <description>
        ///         where &lt;program files&gt; is the root directory where programs
        ///         are installed by default.
        ///         </description>
        ///     </item>
        /// </list>
        /// <note>
        /// This method will delete read-only files and it will not throw
        /// an exception if the file specified does not exist.
        /// </note>
        /// </remarks>
        public static void DeleteFile(string path, bool recursive)
        {
            string[]        files;
            string          folder;
            string          pattern;
            int             pos;
            FileAttributes  fa;

            // Expand any wildcards and call the method recursively

            if (path.IndexOfAny(Helper.FileWildcards) != -1)
            {
                pos = path.LastIndexOfAny(new char[] { ':', PathSepChar });
                if (pos == -1)
                {
                    pattern = path;
                    folder  = "";
                }
                else
                {
                    pattern = path.Substring(pos + 1);
                    folder  = path.Substring(0, pos);
                }

                if (folder.IndexOfAny(Helper.FileWildcards) != -1)
                    throw new ArgumentException("Illegal file path.", "path");

                if (folder.EndsWith(":") && (pattern == "*" || pattern == "*.*"))
                    throw new InvalidOperationException("Cannot delete the root contents of a drive.");

                try
                {
                    files = Helper.GetFilesAndFolders(folder, pattern);
                    for (int i = 0; i < files.Length; i++)
                        DeleteFile(files[i], recursive);
                }
                catch (Exception e)
                {
                    SysLog.LogException(e);
                }

                return;
            }

            // Normalize the folder path

            if (path.IndexOfAny(Helper.FileWildcards) == -1)
            {
                folder  = path;
                pattern = string.Empty;
            }
            else
            {
                pos = path.LastIndexOf(PathSepChar);
                if (pos == -1)
                {
                    pattern = path;
                    folder  = "";
                }
                else
                {
                    pattern = path.Substring(pos + 1);
                    folder  = path.Substring(0, pos);
                }

                folder = Path.GetFullPath(folder);
            }

            // Check for special folders

            if (pattern == string.Empty || pattern == "*" || pattern == "*.*")
            {
                if (folder.EndsWith(":\\") || folder.EndsWith(":") ||
                    String.Compare(folder, Environment.GetFolderPath(Environment.SpecialFolder.System), true) == 0 ||
                    String.Compare(folder, Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles), true) == 0)
                {
                    throw new InvalidOperationException("Cannot delete a special folder.");
                }
            }

            try
            {
                if (!File.Exists(path) && !Directory.Exists(path))
                    return;

                fa = File.GetAttributes(path);
            }
            catch
            {
                return;     // The file must not exist
            }

            if ((fa & FileAttributes.Directory) != 0)
            {

                // The path is to a directory.  If recursive=true then recursively delete the
                // contents of the directory and then delete the directory itself.

                if (recursive)
                {
                    pos    = path.LastIndexOf(PathSepChar);
                    folder = path.Substring(pos + 1);
                    path   = path.Substring(0, pos);

                    if (folder == "." || folder == "..")
                        return;     // Ignore the "." and ".." directories.

                    try
                    {
                        DeleteFile(path + PathSepString + folder + PathSepString + "*.*", true);
                        if (!DisableDeleteFile)
                            Directory.Delete(path + PathSepString + folder);
                    }
                    catch (Exception e)
                    {
                        SysLog.LogException(e);
                    }
                }
            }
            else
            {
                if ((fa & FileAttributes.ReadOnly) != 0)
                    File.SetAttributes(path, FileAttributes.Normal);

                try
                {
                    if (!DisableDeleteFile)
                        File.Delete(path);
                }
                catch (Exception e)
                {
                    SysLog.LogException(e);
                }
            }
        }

        /// <summary>
        /// Compares two files to determine if the file contents are the same.
        /// </summary>
        /// <param name="path1">Path to the first file.</param>
        /// <param name="path2">Path to the second file.</param>
        /// <returns><c>true</c> if the file contents are identical.</returns>
        public static bool CompareFiles(string path1, string path2)
        {
            const int cbBlock = 1024 * 64;

            byte[]  block1 = new byte[cbBlock];
            byte[]  block2 = new byte[cbBlock];
            int     cb;

            using (FileStream fs1 = new FileStream(path1, FileMode.Open, FileAccess.Read),
                              fs2 = new FileStream(path2, FileMode.Open, FileAccess.Read))
            {
                if (fs1.Length != fs2.Length)
                    return false;

                while (true)
                {
                    cb = fs1.Read(block1, 0, cbBlock);
                    if (fs2.Read(block2, 0, cb) != cb)
                        return false;

                    if (cb == 0)
                        break;

                    for (int i = 0; i < cb; i++)
                        if (block1[i] != block2[i])
                            return false;
                }
            }

            return true;
        }

        /// <summary>
        /// Copies a file from one location to another using the specified block
        /// size and bandwidth constraints.
        /// </summary>
        /// <param name="source">The source file or pattern.</param>
        /// <param name="destination">The destination file name or folder.</param>
        /// <param name="blockSize">The block size to use when copying the file (0 for a reasonable default).</param>
        /// <param name="bandwidth">
        /// The approximate maximum bandwidth to use for the operation in bits/second 
        /// (0 to disable the bandwidth constraint).
        /// </param>
        /// <remarks>
        /// <note>
        /// If either <paramref name="blockSize"/> or <paramref name="bandwidth"/> is zero then
        /// the method will use the base .NET Framework's <see cref="File.Copy(string,string)" /> method to 
        /// copy the file without any constraints.
        /// </note>
        /// <note>
        /// This method copies only files, not folders.  Existing files will
        /// be overwritten.
        /// </note>
        /// </remarks>
        private static void CopyFile(string source, string destination, int blockSize, int bandwidth)
        {
            if (blockSize <= 0 || bandwidth <= 0)
            {

                File.Copy(source, destination, true);
                return;
            }

            try
            {
                using (FileStream fsIn = new FileStream(source, FileMode.Open, FileAccess.Read),
                                  fsOut = new FileStream(destination, FileMode.Create, FileAccess.ReadWrite))
                {

                    const int cBlockRun = 10;     // Number of send blocks used to measure actual bandwidth


                    int         timeSlice = WinApi.SleepTimerResolution;
                    byte[]      block = new byte[blockSize];
                    int[]       delay = new int[cBlockRun];         // Delay after each block in a run (milliseconds)
                    long        actualBandwidth;                    // Bits copied/second
                    int         cbBlock;
                    int         cSent;
                    TimeSpan    actualTime;
                    TimeSpan    limitTime;
                    long        startTime;

                    while (fsIn.Position < fsIn.Length)
                    {
                        // Send the blocks in the run, accumulating the time taken
                        // to actually write the blocks.  Note that the first time
                        // this is run, there will be no delay introducted after
                        // writing each block.

                        cSent      = 0;
                        actualTime = TimeSpan.Zero;

                        while (true)
                        {
                            startTime = HiResTimer.Count;

                            cbBlock = fsIn.Read(block, 0, blockSize);
                            if (cbBlock == 0)
                                break;

                            fsOut.Write(block, 0, cbBlock);

                            actualTime += HiResTimer.CalcTimeSpan(startTime);

                            if (delay[cSent] > 0)
                                Thread.Sleep(delay[cSent]);

                            cSent++;
                            if (cSent >= cBlockRun)
                                break;
                        }

                        if (actualTime.Ticks == 0)
                            actualTime = TimeSpan.FromMilliseconds(1);

                        actualBandwidth = (long)((cSent * blockSize * 8) / actualTime.TotalSeconds);

                        if (actualBandwidth <= (long)bandwidth)
                        {
                            // The actual bandwidth is less than limit so don't
                            // use a delay.

                            for (int i = 0; i < cBlockRun; delay[i++] = 0) ;
                        }
                        else
                        {
                            // The actual bandwidth exceeds the limit so calculate the
                            // total delay to be introduced per run.

                            int runDelay;   // milliseconds
                            int remainingDelay;

                            limitTime = TimeSpan.FromSeconds((double)(cSent * blockSize * 8) / (double)bandwidth);
                            Assertion.Test(actualTime < limitTime);
                            runDelay = (int)(limitTime.TotalMilliseconds - actualTime.TotalMilliseconds);

                            // Spread the delay across the all blocks except the last
                            // in chunks equal to the approximate processor timeslice.

                            for (int i = 0; i < cBlockRun; delay[i++] = 0) ;

                            remainingDelay = runDelay;
                            for (int i = 0; i < cBlockRun - 1; i++)
                            {
                                int blockDelay;

                                if (remainingDelay <= 0)
                                    break;

                                // Compute the block delay, rounding up to the nearest timeslice

                                blockDelay = (remainingDelay / (cBlockRun - i) / timeSlice) * timeSlice;
                                if (blockDelay == 0)
                                    blockDelay = timeSlice;

                                delay[i] = blockDelay;
                                remainingDelay -= blockDelay;
                            }

                            // Schedule whatever delay remains for after the last block

                            if (remainingDelay > 0)
                                delay[cBlockRun - 1] = remainingDelay;
                        }
                    }
                }
            }
            catch
            {
                // Delete the destination file if there was an error.

                try
                {
                    if (File.Exists(destination))
                        File.Delete(destination);
                }
                catch
                {
                    // Ignore deletion errors.
                }

                throw;
            }
        }

        /// <summary>
        /// Copies files and folders.
        /// </summary>
        /// <param name="source">The source file or pattern.</param>
        /// <param name="destination">The destination file name or folder.</param>
        /// <param name="recursive">Indicates whether folders should also be copied.</param>
        /// <remarks>
        /// <para>
        /// This method implements functionality that is roughly equivalent to the
        /// old XCOPY DOS command.  It can copy a single file from one location
        /// to another, a set of files matching a pattern, or an entire directory
        /// tree.
        /// </para>
        /// <note>
        /// The method overwrites destination files (including readonly files).
        /// </note>
        /// </remarks>
        public static void CopyFile(string source, string destination, bool recursive)
        {
            CopyFile(source, destination, recursive, 0, 0);
        }

        /// <summary>
        /// Copies files and folders, using the specified block size and bandwidth constraints. 
        /// </summary>
        /// <param name="source">The source file or pattern.</param>
        /// <param name="destination">The destination file name or folder.</param>
        /// <param name="recursive">Indicates whether folders should also be copied.</param>
        /// <param name="blockSize">The block size to use when copying the file (0 for a reasonable default).</param>
        /// <param name="bandwidth">
        /// The approximate maximum bandwidth to use for the operation in bits/second 
        /// (0 to disable the bandwidth constraint).
        /// </param>
        /// <remarks>
        /// <para>
        /// This method implements functionality that is roughly equivalent to the
        /// old XCOPY DOS command.  It can copy a single file from one location
        /// to another, a set of files matching a pattern, or an entire directory
        /// tree.
        /// </para>
        /// <note>
        /// The method overwrites destination files (including readonly files).
        /// </note>
        /// <note>
        /// If either <paramref name="blockSize"/> or <paramref name="bandwidth"/> is zero then
        /// the method will use the base .NET Framework's <see cref="File.Copy(string,string)" /> method to 
        /// copy the file without any constraints.
        /// </note>
        /// </remarks>
        public static void CopyFile(string source, string destination, bool recursive, int blockSize, int bandwidth)
        {
            string      srcFolder;
            string      dstFolder;
            int         pos;
            string[]    files;
            string      fname;
            bool        srcIsFolder;
            bool        dstIsFolder;
            string      pattern;

            destination = destination.Trim();
            if (destination == string.Empty || destination.IndexOfAny(Helper.FileWildcards) != -1)
                throw new ArgumentException("Invalid destination.", "destination");

            try
            {
                srcIsFolder = (File.GetAttributes(source) & FileAttributes.Directory) != 0;
                srcFolder   = source;
                pattern     = string.Empty;
            }
            catch
            {
                srcIsFolder = false;

                pos = source.LastIndexOfAny(new char[] { ':', PathSepChar });
                if (pos == -1)
                {
                    srcFolder = Path.GetFullPath("");
                    pattern   = string.Empty;
                }
                else
                {

                    srcFolder = Path.GetFullPath(source.Substring(0, pos));
                    pattern   = source.Substring(pos + 1);
                }
            }

            try
            {
                dstIsFolder = (File.GetAttributes(destination) & FileAttributes.Directory) != 0;
            }
            catch
            {
                dstIsFolder = false;
            }

            if (source.IndexOfAny(Helper.FileWildcards) != -1)
            {
                // The source has wildcards so copy the matching files
                // and folders (if recursive=true).

                if (!dstIsFolder)
                    throw new InvalidOperationException("Destination must be a folder if wildcards are used.");

                if (!srcFolder.EndsWith(PathSepString))
                    srcFolder += PathSepString;

                dstFolder = Path.GetFullPath(destination);
                if (!dstFolder.EndsWith(PathSepString))
                    dstFolder += PathSepString;

                files = Helper.GetFilesAndFolders(srcFolder, pattern);
                for (int i = 0; i < files.Length; i++)
                {
                    pos   = files[i].LastIndexOf(PathSepChar);
                    fname = files[i].Substring(pos + 1);
                    if ((File.GetAttributes(files[i]) & FileAttributes.Directory) != 0)
                    {
                        if (!recursive)
                            continue;

                        Directory.CreateDirectory(dstFolder + fname);
                        CopyFile(srcFolder + fname + PathSepString + "*.*", dstFolder + fname, true);
                    }
                    else
                        CopyFile(srcFolder + fname, dstFolder + fname, false);
                }
            }
            else if (srcIsFolder)
            {
                if (!recursive)
                    return;

                // If the source is a folder then create a folder with the same
                // name at the destination path and then recursively copy the
                // contents.

                if (srcFolder.EndsWith(PathSepString))
                    fname = srcFolder.Substring(0, srcFolder.Length - 1);
                else
                    fname = srcFolder;

                pos = fname.LastIndexOf(PathSepChar);
                if (pos != -1)
                    fname = fname.Substring(pos + 1);

                dstFolder = Path.GetFullPath(destination);
                if (!Directory.Exists(dstFolder))
                    Directory.CreateDirectory(dstFolder);
                else
                {
                    dstFolder += PathSepString + fname;
                    if (!Directory.Exists(dstFolder))
                        Directory.CreateDirectory(dstFolder);
                }

                if (!srcFolder.EndsWith(PathSepString))
                    srcFolder += PathSepString;

                CopyFile(srcFolder + "*.*", dstFolder, true);
            }
            else if (dstIsFolder)
            {
                // The destination is a folder so copy the source file
                // to the destination, using the same file name.

                dstFolder = Path.GetFullPath(destination);
                if (!dstFolder.EndsWith(PathSepString))
                    dstFolder += PathSepString;

                pos = source.LastIndexOfAny(new char[] { ':', PathSepChar });
                if (pos == -1)
                    fname = source;
                else
                    fname = source.Substring(pos + 1);

                CopyFile(source, dstFolder + fname, blockSize, bandwidth);
            }
            else
            {
                // The source and destination represent file names
                // so copy the source to the destination.

                if (File.Exists(destination) && (File.GetAttributes(destination) & FileAttributes.ReadOnly) != 0)
                    File.SetAttributes(destination, FileAttributes.Normal);

                CopyFile(source, destination, blockSize, bandwidth);
            }
        }

#endif // !MOBILE_DEVICE

        /// <summary>
        /// Ensures that all the directories in the path passed exist by creating
        /// any directories that are missing.
        /// </summary>
        /// <param name="path">The directory path.</param>
        public static void CreateFolderTree(string path)
        {
            string  prefix;
            int     pos, posEnd;

            path = Path.GetFullPath(path);

            if (path.StartsWith("\\\\"))
            {
                // Must be a UNC path.  Advance the position past the "\\share\folder" and
                // the next "\" or "/" if there is one.

                pos = 2;
                pos = path.IndexOfAny(pathSep, pos);
                if (pos == -1)
                    throw new ArgumentException(@"UNC path must have the form \\share\folder.");

                pos = path.IndexOfAny(pathSep, pos + 1);
                if (pos == -1)
                    pos = path.Length;
                else
                    pos++;

                if (pos == path.Length)
                    return;

                pos++;
            }
            else
            {
                if (Helper.IsWindows)
                {
                    pos = path.IndexOf(':');
                    if (pos == -1)
                        throw new ArgumentException(string.Format("Invalid directory path [{0}].", path));

                    pos += 2;   // Skip past the colon and the root "\" or "/"
                }
                else
                {
                    pos = path.IndexOf('/');
                    if (pos == -1)
                        throw new ArgumentException(string.Format("Invalid directory path [{0}].", path));

                    // Skip past the leading "/"

                    pos++;
                }
            }

            posEnd = path.IndexOfAny(pathSep, pos + 1);
            while (true)
            {
                if (posEnd == -1)
                {
                    prefix = path.Substring(pos).Trim();
                    if (prefix == string.Empty)
                        break;

                    prefix = path.Substring(0).Trim();
                }
                else
                    prefix = path.Substring(0, posEnd).Trim();

                if (!Directory.Exists(prefix))
                    Directory.CreateDirectory(prefix);

                if (posEnd == -1)
                    break;

                pos    = posEnd + 1;
                posEnd = path.IndexOfAny(pathSep, pos);
            }
        }

        /// <summary>
        /// Ensures that all the directories specified in the 
        /// file path passed exist, creating any missing directories as necessary.
        /// </summary>
        /// <param name="path">The file path.</param>
        public static void CreateFileTree(string path)
        {
            int pos;

            path = Path.GetFullPath(path);
            pos  = path.LastIndexOfAny(new char[] { '\\', '/', ':' });
            if (pos == -1)
                return;

            CreateFolderTree(path.Substring(0, pos));
        }

        /// <summary>
        /// Determines whether a file exists and whether it can currently
        /// be accessed.
        /// </summary>
        /// <param name="path">The file path.</param>
        /// <param name="access">The desired <see cref="FileAccess" />.</param>
        /// <returns><c>true</c> if the file exists and can be accessed.</returns>
        public static bool IsFileAvailable(string path, FileAccess access)
        {
            if (!File.Exists(path))
                return false;

            try
            {
                using (FileStream fs = new FileStream(path, FileMode.Open, access))
                    return true;
            }
            catch
            {
                return false;
            }
        }

        /// <summary>
        /// Adds a leading period to the file extension is one is not already present.
        /// </summary>
        /// <param name="extension">The extension to be normalized.</param>
        /// <returns>The normalized extension.</returns>
        public static string NormalizeFileExtension(string extension)
        {
            if (extension == null)
                return null;

            if (extension.StartsWith("."))
                return extension;
            else
                return "." + extension;
        }

        /// <summary>
        /// Removes a leading period from the file extension is one is present.
        /// </summary>
        /// <param name="extension">The extension to be denormalized.</param>
        /// <returns>The denormalized extension.</returns>
        public static string DenormalizeFileExtension(string extension)
        {
            if (extension == null)
                return null;

            if (extension.StartsWith("."))
                return extension.Substring(1);
            else
                return extension;
        }

        /// <summary>
        /// Returns the path passed, stripping any trailing forward or backslash
        /// character found.
        /// </summary>
        /// <param name="path">The path to be checked.</param>
        /// <returns>The path without a trailing forward or backslash.</returns>
        public static string StripTrailingSlash(string path)
        {
            if (path.Length == 0)
                return path;

            if (path[path.Length - 1] == '\\' || path[path.Length - 1] == '/')
                return path.Substring(0, path.Length - 1);
            else
                return path;
        }

        /// <summary>
        /// Strips any leading and/or trailing forward or backward slashes
        /// from a path and returns the result.
        /// </summary>
        /// <param name="path">The path to be checked.</param>
        /// <returns>The path without leading or traling slashes.</returns>
        public static string StripSlashes(string path)
        {
            if (path.Length == 0)
                return path;

            if (path[0] == '\\' || path[0] == '/')
                path = path.Substring(1);

            if (path.Length == 0)
                return path;

            if (path[path.Length - 1] == '\\' || path[path.Length - 1] == '/')
                path = path.Substring(0, path.Length - 1);

            return path;
        }

        /// <summary>
        /// Adds a trailing slash to the string passed if one isn't already present.
        /// </summary>
        /// <param name="path">The path to be checked.</param>
        /// <returns>The path with a trailing slash.</returns>
        public static string AddTrailingSlash(string path)
        {
            if (path.EndsWith("\\") || path.EndsWith("/"))
                return path;
            else
                return path + Helper.PathSepString;
        }

        /// <summary>
        /// Gets the fully qualified file path for a path that may already be
        /// fully qualified or may be relative to a specified root path.
        /// </summary>
        /// <param name="path">The path to be resolved.</param>
        /// <param name="rootFolder">The root folder to use for relative paths.</param>
        /// <returns>The resolved fully qualified path.</returns>
        /// <remarks>
        /// This method is useful for situations where a path may be absolute or
        /// relative to another path that <b>is not</b> the current directory.
        /// </remarks>
        public static string ResolveFullPath(string path, string rootFolder)
        {
            if (Path.IsPathRooted(path))
                return path;

            return Path.GetFullPath(Path.Combine(rootFolder, path));
        }

        /// <summary>
        /// Constructs a folder path from a set of folder names handling.  The result
        /// will include a terminating slash.
        /// </summary>
        /// <param name="slash">The slash character to be used (typically '\' or '/').</param>
        /// <param name="args">The folder names.</param>
        /// <returns>The constructed path.</returns>
        /// <remarks>
        /// The main value of this method is that it examines the string for
        /// any leading and/or trailing slashes and adds or removes any as necessary
        /// to generate a valid path.  The method also ignores blank folders.
        /// </remarks>
        public static string ConcatFolderPath(char slash, params string[] args)
        {
            var     sb    = new StringBuilder(256);
            int     cSegs = 0;
            bool    first = true;

            foreach (string s in args)
            {
                string folder = s;

                folder = StripSlashes(folder);
                if (string.IsNullOrWhiteSpace(folder))
                    continue;

                if (first)
                {
                    if (s.StartsWith("\\") || s.StartsWith("/"))
                        sb.Append(slash);

                    first = false;
                }

                sb.Append(folder);
                sb.Append(slash);
                cSegs++;
            }

            if (cSegs == 0)
                sb.Append(slash);

            return sb.ToString();
        }

        /// <summary>
        /// Constructs a file path from a set of zero or more folder names followed
        /// by a single file name.
        /// </summary>
        /// <param name="slash">The slash character to be used (typically '\' or '/').</param>
        /// <param name="args">The folder(s) and file name.</param>
        /// <returns>The constructed path.</returns>
        /// <remarks>
        /// The main value of this method is that it examines the string for
        /// any leading and/or trailing slashes and adds or removes any as necessary
        /// to generate a valid path.  The method also ignores blank folders.
        /// </remarks>
        public static string ConcatFilePath(char slash, params string[] args)
        {
            var     sb = new StringBuilder(256);
            int     cSegs = 0;
            bool    first = true;

            if (args.Length == 0)
                throw new ArgumentException("At least one string must be present.", "args");

            for (int i = 0; i < args.Length - 1; i++)
            {
                string s      = args[i];
                string folder = s;

                folder = StripSlashes(folder);
                if (string.IsNullOrWhiteSpace(folder))
                    continue;

                if (first)
                {
                    if (s.StartsWith("\\") || s.StartsWith("/"))
                        sb.Append(slash);

                    first = false;
                }

                sb.Append(folder);
                sb.Append(slash);
                cSegs++;
            }

            if (cSegs == 0)
                sb.Append(slash);

            sb.Append(StripSlashes(args[args.Length - 1]));

            return sb.ToString();
        }

        /// <summary>
        /// Returns the fully qualified path for a file whose path is passed
        /// without the file extension.
        /// </summary>
        /// <param name="path">The file path.</param>
        /// <returns>The fully qualified file name without the extension.</returns>
        public static string GetFullNameWithoutExtension(string path)
        {
            string  fullPath = Path.GetFullPath(path);
            int     pos;

            if (!Path.HasExtension(path))
                return fullPath;

            pos = path.LastIndexOf('.');
            Assertion.Test(pos != -1);

            return fullPath.Substring(0, pos);
        }

        /// <summary>
        /// Strips the folder path and extension from a file path
        /// and returns just the file name.
        /// </summary>
        /// <param name="path">The path.</param>
        /// <returns>Just the file name portion of the path without extension.</returns>
        public static string GetFileNameWithoutExtension(string path)
        {
            int pos;

            path = Path.GetFileName(path);
            pos  = path.LastIndexOf('.');
            if (pos == -1)
                return path;
            else
                return path.Substring(0, pos);
        }

        /// <summary>
        /// Returns <c>true</c> if the string passed is a valid file name.
        /// </summary>
        /// <param name="fileName">The file name to test.</param>
        /// <returns><c>true</c> if the file name is valid.</returns>
        /// <remarks>
        /// The file name cannot include any directory or device related
        /// characters.
        /// </remarks>
        public static bool IsValidFileName(string fileName)
        {
            return !(fileName.Length == 0 ||
                     fileName.Length > 260 ||
                     fileName.EndsWith(".") ||
                     fileName.EndsWith(" ") ||
                     fileName.IndexOfAny(new char[] { '\\', '/', ':', '*', '?', '<', '>', '|' }) != -1);
        }

        /// <summary>
        /// Determine whether a file path has a file extension.
        /// </summary>
        /// <param name="path">The file path.</param>
        /// <returns><c>true</c> if the path has an extension.</returns>
        public static bool HasExtension(string path)
        {
            return Path.GetFileName(path).IndexOf('.') != -1;
        }

        /// <summary>
        /// Determines whether a file path has the specified extension.
        /// </summary>
        /// <param name="path">The file path.</param>
        /// <param name="extension">The case insensitive file extension to check for (may or may not include the leading period).</param>
        /// <returns><c>true</c> if the file name has the given extension.</returns>
        /// <remarks>
        /// <note>
        /// That the extension comparision is case insensitive and also that 
        /// the leading period in the <paramref name="extension"/> parameter
        /// is optional.
        /// </note>
        /// </remarks>
        public static bool HasExtension(string path, string extension)
        {
            string s = Path.GetExtension(path);

            if (extension == null || extension == string.Empty)
            {
                if (s == null || s == "" || s == ".")
                    return true;
            }

            if (!extension.StartsWith("."))
                extension = "." + extension;

            return String.Compare(s, extension, StringComparison.OrdinalIgnoreCase) == 0;
        }

        /// <summary>
        /// Removes a <b>file://</b> scheme from the path URI if this is scheme
        /// is present.  The result will be a valid file system path.
        /// </summary>
        /// <param name="path">The path/URI to be converted.</param>
        /// <returns>The file system path.</returns>
        /// <remarks>
        /// <note>
        /// <para>
        /// This method behaves slightly differently when running on Windows and
        /// when running on Unix/Linux.  On Windows, file URIs are absolute file
        /// paths of the form:
        /// </para>
        /// <code language="none">
        /// FILE:///C:/myfolder/myfile
        /// </code>
        /// <para>
        /// To convert this into a valid file system path this method strips the
        /// <b>file://</b> scheme <i>and</i> the following forward slash.  On
        /// Unix/Linux, file URIs will have the form:
        /// </para>
        /// <code language="none">
        /// FILE:///myfolder/myfile
        /// </code>
        /// <para>
        /// In this case, the forward shlash following the <b>file://</b> scheme
        /// is part of the file system path and will not be removed.
        /// </para>
        /// </note>
        /// </remarks>
        public static string StripFileScheme(string path)
        {
            if (!path.ToLowerInvariant().StartsWith("file://"))
                return path;

            return path.Substring(Helper.IsWindows ? 8 : 7);
        }

#if !SILVERLIGHT

        /// <summary>
        /// Determines whether a file is hidden or is located
        /// within a hidden folder.
        /// </summary>
        /// <returns><c>true</c> if the file is hidden.</returns>
        public static bool IsFileHidden(string path)
        {
            // Check to see if the file itself is hidden.

            path = Path.GetFullPath(path);
            if ((File.GetAttributes(path) & FileAttributes.Hidden) != 0)
                return true;

            // Strip the file name from the path.

            path = path.Substring(0, path.Length - Path.GetFileName(path).Length);

            // Look down the directory heirarchy to determine if any of these are hidden.

            string  folder;
            int     p, pEnd;

            path = path.Replace('/', Helper.PathSepChar);
            p    = path.IndexOf(Helper.PathSepChar);

            if (p == -1)
                return false;

            p = path.IndexOf(Helper.PathSepChar, p + 1);

            if (p == -1)
                return false;

            folder = path.Substring(0, p++); // folder is now set to the root folder

            while (true)
            {
                pEnd = path.IndexOf(Helper.PathSepChar, p);

                if (pEnd == -1)
                    return false;

                folder += Helper.PathSepString + path.Substring(p, pEnd - p);

                if ((File.GetAttributes(folder) & FileAttributes.Hidden) != 0)
                    return true;

                p = pEnd + 1;
            }
        }

        /// <summary>
        /// This method is an enhancement of <see cref="Directory.GetFiles(string,string,SearchOption)" /> that
        /// accepts a single path string the specifies both the directory and pattern.
        /// </summary>
        /// <param name="path">The folder path and file pattern.</param>
        /// <param name="searchOption">
        /// One of the <see cref="SearchOption" /> values indicating whether all subdirectories
        /// should be included in the search.
        /// </param>
        /// <returns>The set of matching file names.</returns>
        /// <remarks>
        /// <note>
        /// This method also handles relative paths correctly.
        /// </note>
        /// </remarks>
        public static string[] GetFilesByPattern(string path, SearchOption searchOption)
        {
            string  folder;
            string  pattern;
            int     pos;

            pos = path.LastIndexOfAny(new char[] { ':', '\\', '/' });
            if (pos == -1)
            {
                folder  = Environment.CurrentDirectory;
                pattern = path.Substring(pos + 1);
            }
            else
            {
                folder  = Path.GetFullPath(path.Substring(0, pos));
                pattern = path.Substring(pos + 1);
            }

            if (pattern == string.Empty)
                throw new ArgumentException("Invalid file pattern", "path");

            return Directory.GetFiles(folder, pattern, searchOption);
        }

#endif // !SILVERLIGHT

        /// <summary>
        /// Obliterates the file specified by first overwriting it with ones
        /// and then zeros, before deleting it.
        /// </summary>
        /// <param name="path">The file to be wiped.</param>
        public static void WipeFile(string path)
        {
            const int cbBlock = 8192;

            FileStream  fs = null;
            byte[]      zeros;
            byte[]      ones;
            int         cBlocks;
            int         cRemain;

            zeros = new byte[cbBlock];
            for (int i = 0; i < cbBlock; zeros[i++] = 0x00) ;
            ones = new byte[cbBlock];
            for (int i = 0; i < cbBlock; ones[i++] = 0xFF) ;

            try
            {
                fs = new FileStream(path, FileMode.Open);

                cBlocks = (int)(fs.Length / cbBlock);
                cRemain = (int)(fs.Length % cbBlock);

                // Overwrite with zeros

                fs.Position = 0;
                for (int i = 0; i < cBlocks; i++)
                    fs.Write(zeros, 0, cbBlock);

                fs.Write(zeros, 0, cRemain);
                fs.Flush();

                // Overwrite with ones

                fs.Position = 0;
                for (int i = 0; i < cBlocks; i++)
                    fs.Write(ones, 0, cbBlock);

                fs.Write(ones, 0, cRemain);
                fs.Flush();
            }
            finally
            {
                if (fs != null)
                    fs.Close();
            }

            // Delete the file and we're done

            File.Delete(path);
        }

        /// <summary>
        /// Creates a new file and writes a string to the file using
        /// ANSI encoding.
        /// </summary>
        /// <param name="path">The file path.</param>
        /// <param name="contents">The string to be written to the file.</param>
        public static void WriteToFile(string path, string contents)
        {
            var writer = new StreamWriter(path, false, ansiEncoding);

            try
            {
                writer.Write(contents);
            }
            finally
            {
                writer.Close();
            }
        }

        /// <summary>
        /// Creates a new file and writes a string to the file using
        /// the specified encoding.
        /// </summary>
        /// <param name="path">The file path.</param>
        /// <param name="contents">The string to be written to the file.</param>
        /// <param name="encoding">The character encoding to use.</param>
        public static void WriteToFile(string path, string contents, Encoding encoding)
        {
            var writer = new StreamWriter(path, false, encoding);

            try
            {
                writer.Write(contents);
            }
            finally
            {
                writer.Close();
            }
        }

        /// <summary>
        /// Appends a string to the end of a file (creating the file if
        /// necessary) using ANSI encoding.
        /// </summary>
        /// <param name="path">The file path.</param>
        /// <param name="contents">The string to be written to the file.</param>
        public static void AppendToFile(string path, string contents)
        {
            var writer = new StreamWriter(path, true, ansiEncoding);

            try
            {
                writer.Write(contents);
            }
            finally
            {
                writer.Close();
            }
        }

        /// <summary>
        /// Appends a string to the end of a file (creating the file if
        /// necessary) using the specified encoding.
        /// </summary>
        /// <param name="path">The file path.</param>
        /// <param name="contents">The string to be written to the file.</param>
        /// <param name="encoding">The character encoding to use.</param>
        public static void AppendToFile(string path, string contents, Encoding encoding)
        {
            var writer = new StreamWriter(path, true, encoding);

            try
            {
                writer.Write(contents);
            }
            finally
            {
                writer.Close();
            }
        }

#if !SILVERLIGHT

        /// <summary>
        /// Starts a process to run an executable file and waits for the process to terminate.
        /// </summary>
        /// <param name="path">Path to the executable file.</param>
        /// <param name="args">Command line arguments (or <c>null</c>).</param>
        /// <param name="timeout">
        /// Optional maximum time to wait for the process to complete or <c>null</c> to wait
        /// indefinitely.
        /// </param>
        /// <param name="process">
        /// The optional <see cref="Process"/> instance to use to launch the process.
        /// </param>
        /// <returns>The process exit code.</returns>
        /// <exception cref="TimeoutException">Thrown if the process did not exit within the <paramref name="timeout"/> limit.</exception>
        /// <remarks>
        /// <note>
        /// If <paramref name="timeout"/> is and execution has not commpleted in time then
        /// a <see cref="TimeoutException"/> will be thrown and the process will be killed
        /// if it was created by this method.  Process instances passed via the <paramref name="process"/>
        /// parameter will not be killed in this case.
        /// </note>
        /// </remarks>
        public static int Execute(string path, string args, TimeSpan? timeout = null, Process process = null)
        {
            var processInfo   = new ProcessStartInfo(path, args != null ? args : string.Empty);
            var killOnTimeout = process == null;

            if (process == null)
            {
                process = new Process();
            }

            try
            {
                processInfo.UseShellExecute        = false;
                processInfo.RedirectStandardError  = false;
                processInfo.RedirectStandardOutput = false;
                processInfo.CreateNoWindow         = true;
                process.StartInfo                  = processInfo;
                process.EnableRaisingEvents        = true;

                process.Start();

                if (!timeout.HasValue || timeout.Value >= TimeSpan.FromDays(1))
                    process.WaitForExit();
                else
                {
                    process.WaitForExit((int)timeout.Value.TotalMilliseconds);

                    if (!process.HasExited)
                    {
                        if (killOnTimeout)
                            process.Kill();

                        throw new TimeoutException(string.Format("Process [{0}] execute has timed out.", path));
                    }
                }

                return process.ExitCode;
            }
            finally
            {
                process.Close();
            }
        }

        /// <summary>
        /// Asyncrhonously starts a process to run an executable file and waits for the process to terminate.
        /// </summary>
        /// <param name="path">Path to the executable file.</param>
        /// <param name="args">Command line arguments (or <c>null</c>).</param>
        /// <param name="timeout">
        /// Optional maximum time to wait for the process to complete or <c>null</c> to wait
        /// indefinitely.
        /// </param>
        /// <param name="process">
        /// The optional <see cref="Process"/> instance to use to launch the process.
        /// </param>
        /// <returns>The process exit code.</returns>
        /// <exception cref="TimeoutException">Thrown if the process did not exit within the <paramref name="timeout"/> limit.</exception>
        /// <remarks>
        /// <note>
        /// If <paramref name="timeout"/> is and execution has not commpleted in time then
        /// a <see cref="TimeoutException"/> will be thrown and the process will be killed
        /// if it was created by this method.  Process instances passed via the <paramref name="process"/>
        /// parameter will not be killed in this case.
        /// </note>
        /// </remarks>
        public static async Task<int> ExecuteAsync(string path, string args, TimeSpan? timeout = null, Process process = null)
        {
            return await Task.Run(() => Execute(path, args, timeout, process));
        }

        /// <summary>
        /// Used by <see cref="ExecuteCaptureStreams(string, string, TimeSpan?, Process)"/> to redirect process output streams.
        /// </summary>
        private sealed class StreamRedirect
        {
            private object          syncLock       = new object();
            public StringBuilder    sbOutput       = new StringBuilder();
            public StringBuilder    sbError        = new StringBuilder();
            public bool             isOutputClosed = false;
            public bool             isErrorClosed  = false;

            public void OnOutput(object sendingProcess, DataReceivedEventArgs args)
            {
                lock (syncLock)
                {
                    if (string.IsNullOrWhiteSpace(args.Data))
                        isOutputClosed = true;
                    else
                        sbOutput.AppendLine(args.Data);
                }
            }

            public void OnError(object sendingProcess, DataReceivedEventArgs args)
            {
                lock (syncLock)
                {
                    if (string.IsNullOrWhiteSpace(args.Data))
                        isErrorClosed = true;
                    else
                        sbError.AppendLine(args.Data);
                }
            }

            public void Wait()
            {
                while (!isOutputClosed || !isErrorClosed)
                    Thread.Sleep(100);
            }
        }

        /// <summary>
        /// Starts a process to run an executable file and waits for the process to terminate
        /// while capturing any output written to the standard output and error streams.
        /// </summary>
        /// <param name="path">Path to the executable file.</param>
        /// <param name="args">Command line arguments (or <c>null</c>).</param>
        /// <param name="timeout">
        /// Optional maximum time to wait for the process to complete or <c>null</c> to wait
        /// indefinitely.
        /// </param>
        /// <param name="process">
        /// The optional <see cref="Process"/> instance to use to launch the process.
        /// </param>
        /// <returns>
        /// The <see cref="ExecuteResult"/> including the process exit code and capture 
        /// standard output and error streams.
        /// </returns>
        /// <exception cref="TimeoutException">Thrown if the process did not exit within the <paramref name="timeout"/> limit.</exception>
        /// <remarks>
        /// <note>
        /// If <paramref name="timeout"/> is and execution has not commpleted in time then
        /// a <see cref="TimeoutException"/> will be thrown and the process will be killed
        /// if it was created by this method.  Process instances passed via the <paramref name="process"/>
        /// parameter will not be killed in this case.
        /// </note>
        /// </remarks>
        public static ExecuteResult ExecuteCaptureStreams(string path, string args, TimeSpan? timeout = null, Process process = null)
        {
            var processInfo     = new ProcessStartInfo(path, args != null ? args : string.Empty);
            var redirect        = new StreamRedirect();
            var externalProcess = process != null;

            if (process == null)
            {
                process = new Process();
            }

            try
            {
                processInfo.UseShellExecute        = false;
                processInfo.RedirectStandardError  = true;
                processInfo.RedirectStandardOutput = true;
                processInfo.CreateNoWindow         = true;
                process.StartInfo                  = processInfo;
                process.OutputDataReceived        += new DataReceivedEventHandler(redirect.OnOutput);
                process.ErrorDataReceived         += new DataReceivedEventHandler(redirect.OnError);
                process.EnableRaisingEvents        = true;

                process.Start();
                process.BeginOutputReadLine();
                process.BeginErrorReadLine();

                if (!timeout.HasValue || timeout.Value >= TimeSpan.FromDays(1))
                    process.WaitForExit();
                else
                {
                    process.WaitForExit((int)timeout.Value.TotalMilliseconds);

                    if (!process.HasExited)
                    {
                        if (!externalProcess)
                            process.Kill();

                        throw new TimeoutException(string.Format("Process [{0}] execute has timed out.", path));
                    }
                }

                redirect.Wait();    // Wait for the standard output/error streams
                                    // to receive all the data

                return new ExecuteResult()
                    {
                        ExitCode       = process.ExitCode,
                        StandardOutput = redirect.sbOutput.ToString(),
                        StandardError  = redirect.sbError.ToString()
                    };
            }
            finally
            {
                if (!externalProcess)
                    process.Close();
            }
        }

        /// <summary>
        /// Asynchronously starts a process to run an executable file and waits for the process to terminate
        /// while capturing any output written to the standard output and error streams.
        /// </summary>
        /// <param name="path">Path to the executable file.</param>
        /// <param name="args">Command line arguments (or <c>null</c>).</param>
        /// <param name="timeout">
        /// Maximum time to wait for the process to complete or <c>null</c> to wait
        /// indefinitely.
        /// </param>
        /// <param name="process">
        /// The optional <see cref="Process"/> instance to use to launch the process.
        /// </param>
        /// <returns>
        /// The <see cref="ExecuteResult"/> including the process exit code and capture 
        /// standard output and error streams.
        /// </returns>
        /// <exception cref="TimeoutException">Thrown if the process did not exit within the <paramref name="timeout"/> limit.</exception>
        /// <remarks>
        /// <note>
        /// If <paramref name="timeout"/> is and execution has not commpleted in time then
        /// a <see cref="TimeoutException"/> will be thrown and the process will be killed
        /// if it was created by this method.  Process instances passed via the <paramref name="process"/>
        /// parameter will not be killed in this case.
        /// </note>
        /// </remarks>
        public static async Task<ExecuteResult> ExecuteCaptureStreamsAsync(string path, string args, 
                                                                           TimeSpan? timeout = null, Process process = null)
        {
            return await Task.Run(() => ExecuteCaptureStreams(path, args, timeout, process));
        }

        /// <summary>
        /// Starts a process for an <see cref="Assembly" /> by calling the assembly's <b>main()</b>
        /// entry point method. 
        /// </summary>
        /// <param name="assembly">The assembly to be started.</param>
        /// <param name="args">The command line arguments (or <c>null</c>).</param>
        /// <returns>The process started.</returns>
        /// <remarks>
        /// <note>
        /// This method works only for executable assemblies with
        /// an appropriate <b>main</b> entry point that reside on the
        /// local file system.
        /// </note>
        /// </remarks>
        public static Process StartProcess(Assembly assembly, string args)
        {
            string path = assembly.CodeBase;

            if (!path.StartsWith("file://"))
                throw new ArgumentException("Assembly must reside on the local file system.", "assembly");

            return Process.Start(Helper.StripFileScheme(path), args != null ? args : string.Empty);
        }

#endif // !SILVERLIGHT

        /// <summary>
        /// Normalizes the URI passed by converting as much of it as possible
        /// to lower case, without affecting its meaning.
        /// </summary>
        /// <param name="uri">The URI to be normalized.</param>
        /// <param name="ignoreCase">
        /// <c>true</c> if it is known that the server targeted by the URI implements
        /// a case insensitive file system (such as Windows IIS).
        /// </param>
        /// <returns>The normalized URI as a string.</returns>
        /// <remarks>
        /// By default, this method converts the URI schema and host name sections
        /// of the URI passed to lower case.  If <b>ignoreCase=true</b> then 
        /// portion of the URI up to but not including the query string will
        /// also be converted to lower case.
        /// </remarks>
        public static string Normalize(Uri uri, bool ignoreCase)
        {
            string  sUri;
            int     pos;

            sUri = uri.ToString();

            if (ignoreCase)
            {
                // Normalize everything up to the query string.

                pos = sUri.IndexOf('?');
            }
            else
            {
                // Normalize just the host.

                pos = sUri.IndexOf('/', uri.Scheme.Length);
            }

            if (pos == -1)
                return sUri.ToLowerInvariant();
            else
                return sUri.Substring(0, pos).ToLowerInvariant() + sUri.Substring(pos);
        }

#if !SILVERLIGHT

        /// <summary>
        /// Uses <see cref="BinaryFormatter" /> to serialize an object instance to
        /// a binary stream.
        /// </summary>
        /// <param name="output">The output stream.</param>
        /// <param name="graph">The graph of objects to be serialized.</param>
        /// <exception cref="SerializationException">Thrown if a serialization error occurs.</exception>
        public static void Serialize(Stream output, object graph)
        {
            new BinaryFormatter().Serialize(output, graph);
        }

        /// <summary>
        /// Uses <see cref="BinaryFormatter" /> to serialize an object instance to
        /// a byte array.
        /// </summary>
        /// <param name="graph">The graph of objects to be serialized.</param>
        /// <returns>The serialized bytes.</returns>
        /// <exception cref="SerializationException">Thrown if a serialization error occurs.</exception>
        public static byte[] Serialize(object graph)
        {
            var output = new MemoryStream(512);

            try
            {
                new BinaryFormatter().Serialize(output, graph);
                return output.ToArray();
            }
            finally
            {
                output.Close();
            }
        }

        /// <summary>
        /// Deserializes an object graph from a byte stream and returns the root.
        /// </summary>
        /// <param name="input">The input stream.</param>
        /// <returns>The root object in the graph.</returns>
        /// <exception cref="SerializationException">Thrown if a serialization error occurs.</exception>
        public static object Deserialize(Stream input)
        {
            return new BinaryFormatter().Deserialize(input);
        }

        /// <summary>
        /// Deserializes an object graph from a byte array and returns the root.
        /// </summary>
        /// <param name="buffer">The input array.</param>
        /// <returns>The root object in the graph.</returns>
        /// <exception cref="SerializationException">Thrown if a serialization error occurs.</exception>
        public static object Deserialize(byte[] buffer)
        {
            var input = new MemoryStream(buffer);

            try
            {
                return new BinaryFormatter().Deserialize(input);
            }
            finally
            {
                input.Close();
            }
        }

#endif // !SILVERLIGHT

        /// <summary>
        /// Returns <c>true</c> if the two arrays passed are equal.
        /// </summary>
        /// <param name="array1">The first array to be compared.</param>
        /// <param name="array2">The second array to be compared.</param>
        /// <returns><c>true</c> if the arrays are equal.</returns>
        /// <remarks>
        /// <para>
        /// Two arrays are considered to be equal if:
        /// </para>
        /// <list type="bullet">
        ///     <item>Both array references are <c>null</c>.</item>
        ///     <item>
        ///     The arrays have the same length and the corresponding elements are either both <c>null</c>
        ///     or their <see cref="Object.Equals(object)" /> override returns <c>true</c>.
        ///     </item>
        /// </list>
        /// </remarks>
        public static bool ArrayEquals(Array array1, Array array2)
        {
            if (array1 == null && array2 == null)
                return true;

            if (array1 == null || array2 == null)
                return false;

            if (array1.Length != array2.Length)
                return false;

            for (int i = 0; i < array1.Length; i++)
            {
                object element1 = array1.GetValue(i);
                object element2 = array2.GetValue(i);

                if (element1 == null && element2 == null)
                    continue;

                if (element1 == null || element2 == null)
                    return false;

                if (!element1.Equals(element2))
                    return false;
            }

            return true;
        }

        /// <summary>
        /// Compares two integer arrays and returns <c>true</c> if they are equal.
        /// </summary>
        /// <param name="array1">The first array.</param>
        /// <param name="array2">The second array.</param>
        /// <returns><c>true</c> if the arrays are the same.</returns>
        /// <remarks>
        /// <para>
        /// This override will have better performance than the generic
        /// <see cref="ArrayEquals(Array,Array)" /> method by avoiding
        /// unnecessary boxing.
        /// </para>
        /// <note>
        /// The two arrays are considered to be equal if both are <c>null</c>.
        /// </note>
        /// </remarks>
        public static bool ArrayEquals(int[] array1, int[] array2)
        {
            if (array1 == null && array2 == null)
                return true;

            if (array1 == null || array2 == null)
                return false;

            if (array1.Length != array2.Length)
                return false;

            for (int i = 0; i < array1.Length; i++)
                if (array1[i] != array2[i])
                    return false;

            return true;
        }

        /// <summary>
        /// Compares two short arrays and returns <c>true</c> if they are equal.
        /// </summary>
        /// <param name="array1">The first array.</param>
        /// <param name="array2">The second array.</param>
        /// <returns><c>true</c> if the arrays are the same.</returns>
        /// <remarks>
        /// <para>
        /// This override will have better performance than the generic
        /// <see cref="ArrayEquals(Array,Array)" /> method by avoiding
        /// unnecessary boxing.
        /// </para>
        /// <note>
        /// The two arrays are consider to be equal if both are <c>null</c>.
        /// </note>
        /// </remarks>
        public static bool ArrayEquals(short[] array1, short[] array2)
        {
            if (array1 == null && array2 == null)
                return true;

            if (array1 == null || array2 == null)
                return false;

            if (array1.Length != array2.Length)
                return false;

            for (int i = 0; i < array1.Length; i++)
                if (array1[i] != array2[i])
                    return false;

            return true;
        }

        /// <summary>
        /// Compares two long arrays and returns <c>true</c> if they are equal.
        /// </summary>
        /// <param name="array1">The first array.</param>
        /// <param name="array2">The second array.</param>
        /// <returns><c>true</c> if the arrays are the same.</returns>
        /// <remarks>
        /// <para>
        /// This override will have better performance than the generic
        /// <see cref="ArrayEquals(Array,Array)" /> method by avoiding
        /// unnecessary boxing.
        /// </para>
        /// <note>
        /// The two arrays are consider to be equal if both are <c>null</c>.
        /// </note>
        /// </remarks>
        public static bool ArrayEquals(long[] array1, long[] array2)
        {
            if (array1 == null && array2 == null)
                return true;

            if (array1 == null || array2 == null)
                return false;

            if (array1.Length != array2.Length)
                return false;

            for (int i = 0; i < array1.Length; i++)
                if (array1[i] != array2[i])
                    return false;

            return true;
        }

        /// <summary>
        /// Compares two byte arrays and returns <c>true</c> if they are equal.
        /// </summary>
        /// <param name="array1">The first array.</param>
        /// <param name="array2">The second array.</param>
        /// <returns><c>true</c> if the arrays are the same.</returns>
        /// <remarks>
        /// <para>
        /// This override will have better performance than the generic
        /// <see cref="ArrayEquals(Array,Array)" /> method by avoiding
        /// unnecessary boxing.
        /// </para>
        /// <note>
        /// The two arrays are consider to be equal if both are <c>null</c>.
        /// </note>
        /// </remarks>
        public static bool ArrayEquals(byte[] array1, byte[] array2)
        {
            if (array1 == null && array2 == null)
                return true;

            if (array1 == null || array2 == null)
                return false;

            if (array1.Length != array2.Length)
                return false;

            for (int i = 0; i < array1.Length; i++)
                if (array1[i] != array2[i])
                    return false;

            return true;
        }

        /// <summary>
        /// Returns <c>true</c> if the byte array passed is <c>null</c>, 
        /// empty or consists solely of zeros.
        /// </summary>
        /// <param name="array">The array to be tested.</param>
        /// <returns><c>true</c> if the array is zeroed.</returns>
        public static bool IsZeros(byte[] array)
        {
            if (array == null || array.Length == 0)
                return true;

            for (int i = 0; i < array.Length; i++)
                if (array[i] != 0)
                    return false;

            return true;
        }

        /// <summary>
        /// Concatenates a set of byte arrays and returns the result.
        /// </summary>
        /// <param name="arrays">The arrays to append to the first.</param>
        /// <returns>array0 + array1 + ...arrayN</returns>
        public static byte[] Concat(params byte[][] arrays)
        {
            byte[]  result;
            int     c;
            int     pos;

            c = 0;
            for (int i = 0; i < arrays.Length; i++)
                c += arrays[i].Length;

            result = new byte[c];

            pos = 0;
            for (int i = 0; i < arrays.Length; i++)
            {
                Array.Copy(arrays[i], 0, result, pos, arrays[i].Length);
                pos += arrays[i].Length;
            }

            return result;
        }

        /// <summary>
        /// Concatenates a set of string arrays and returns the result.
        /// </summary>
        /// <param name="arrays">The arrays to append to the first.</param>
        /// <returns>array0 + array1 + ...arrayN</returns>
        public static string[] Concat(params string[][] arrays)
        {
            string[]    result;
            int         c;
            int         pos;

            c = 0;
            for (int i = 0; i < arrays.Length; i++)
                c += arrays[i].Length;

            result = new string[c];

            pos = 0;
            for (int i = 0; i < arrays.Length; i++)
            {
                Array.Copy(arrays[i], 0, result, pos, arrays[i].Length);
                pos += arrays[i].Length;
            }

            return result;
        }

        /// <summary>
        /// Returns a shallow clone of an array.
        /// </summary>
        /// <typeparam name="TElement">The array element type.</typeparam>
        /// <param name="source">The source array (or <c>null</c>).</param>
        /// <returns>The cloned array or <c>null</c>.</returns>
        public static TElement[] ShallowClone<TElement>(TElement[] source)
        {
            TElement[] clone;

            if (source == null)
                return null;

            clone = new TElement[source.Length];
            Array.Copy(source, clone, source.Length);

            return clone;
        }

        /// <summary>
        /// Generates a user name string by combining realm and account strings
        /// as specified by a <see cref="RealmFormat" /> parameter.
        /// </summary>
        /// <param name="mode">The <see cref="RealmFormat" /> to be used.</param>
        /// <param name="realm">The realm string (or <c>null</c>).</param>
        /// <param name="account">The account string.</param>
        /// <returns>The complete user name.</returns>
        /// <remarks>
        /// <note>
        /// For <see cref="RealmFormat.Slash" /> this method always
        /// uses a forward slash because backslash can cause problems in some
        /// software components.
        /// </note>
        /// </remarks>
        public static string GetUserName(RealmFormat mode, string realm, string account)
        {
            if (realm == null || realm.Length == 0)
                return account;

            switch (mode)
            {
                case RealmFormat.Email:

                    return account + "@" + realm;

                case RealmFormat.Slash:

                    return realm + "/" + account;

                default:

                    Assertion.Fail("Unexpected Realm Format");
                    return null;
            }
        }

        private static char[] slashes = new char[] { '\\', '/' };

        /// <summary>
        /// Parses a user name into its realm and account components as specified
        /// by the <see cref="RealmFormat" /> parameter.
        /// </summary>
        /// <param name="mode">The <see cref="RealmFormat" /> to be used.</param>
        /// <param name="userName">The user name.</param>
        /// <param name="realm">Receives the parsed realm component.</param>
        /// <param name="account">Receives the parsed account component.</param>
        public static void ParseUserName(RealmFormat mode, string userName, out string realm, out string account)
        {
            int pos;

            switch (mode)
            {
                case RealmFormat.Email:

                    pos = userName.IndexOf('@');
                    if (pos == -1)
                    {
                        realm   = string.Empty;
                        account = userName;
                    }
                    else
                    {
                        realm   = userName.Substring(pos + 1);
                        account = userName.Substring(0, pos);
                    }
                    break;

                case RealmFormat.Slash:

                    pos = userName.IndexOfAny(slashes);
                    if (pos == -1)
                    {
                        realm   = string.Empty;
                        account = userName;
                    }
                    else
                    {
                        realm = userName.Substring(0, pos);
                        account = userName.Substring(pos + 1);
                    }
                    break;

                default:

                    Assertion.Fail("Unexpected Realm Format");
                    realm = null;
                    account = null;
                    break;
            }
        }

        /// <summary>
        /// Multiplies a timespan by an integer value and returns the 
        /// resulting timespan.
        /// </summary>
        /// <param name="timespan">The input timespan.</param>
        /// <param name="multiplier">The multiplier.</param>
        /// <returns>The scaled timespan.</returns>
        public static TimeSpan Multiply(TimeSpan timespan, int multiplier)
        {
            return TimeSpan.FromTicks(timespan.Ticks * multiplier);
        }

        /// <summary>
        /// Divides a timespan by an integer value and returns the 
        /// resulting timespan.
        /// </summary>
        /// <param name="timespan">The input timespan.</param>
        /// <param name="divisor">The multiplier.</param>
        /// <returns>The scaled timespan.</returns>
        public static TimeSpan Divide(TimeSpan timespan, int divisor)
        {
            return TimeSpan.FromTicks(timespan.Ticks / divisor);
        }

        /// <summary>
        /// Increments a <see cref="DateTime" /> by a <see cref="TimeSpan" />,
        /// handling timespan <see cref="TimeSpan.MinValue" /> and <see cref="TimeSpan.MaxValue" />
        /// by pegging the result to the beginning or end of time.
        /// </summary>
        /// <param name="time">The <see cref="DateTime" />.</param>
        /// <param name="timespan">The <see cref="TimeSpan" />.</param>
        /// <returns>The computed <see cref="DateTime" />.</returns>
        /// <remarks>
        /// <para>
        /// If the timespan passed is <see cref="TimeSpan.MinValue" /> then 
        /// the result will be the beginning of time.  If the timespan is
        /// <see cref="TimeSpan.MaxValue" /> then the result will be the
        /// end of time.  Otherwise, the method will return the computed
        /// result.
        /// </para>
        /// </remarks>
        public static DateTime Add(DateTime time, TimeSpan timespan)
        {
            if (timespan == TimeSpan.MinValue)
                return DateTime.MinValue;
            else if (timespan == TimeSpan.MaxValue)
                return DateTime.MaxValue;
            else
                return time + timespan;
        }

        /// <summary>
        /// Returns the minimum of two date values.
        /// </summary>
        /// <param name="date1">Date value 1.</param>
        /// <param name="date2">Date value 2.</param>
        /// <returns>The minimum date.</returns>
        public static DateTime Min(DateTime date1, DateTime date2)
        {
            if (date1 <= date2)
                return date1;
            else
                return date2;
        }

        /// <summary>
        /// Returns the maximum of two date values.
        /// </summary>
        /// <param name="date1">Date value 1.</param>
        /// <param name="date2">Date value 2.</param>
        /// <returns>The maximum date.</returns>
        public static DateTime Max(DateTime date1, DateTime date2)
        {
            if (date1 >= date2)
                return date1;
            else
                return date2;
        }

        /// <summary>
        /// Returns <c>true</c> if two dates are within the specified timespan
        /// from each other (inclusive).
        /// </summary>
        /// <param name="date1">Date value 1.</param>
        /// <param name="date2">Date value 2.</param>
        /// <param name="delta">The maximum timespan allowed.</param>
        /// <returns><c>true</c> if the difference between the dates is less than or equal to the timespan.</returns>
        /// <remarks>
        /// <note>
        /// The ordering of the dates passed doesn't matter.
        /// </note>
        /// </remarks>
        public static bool Within(DateTime date1, DateTime date2, TimeSpan delta)
        {
            if (date1 == date2)
                return true;
            else if (date1 < date2)
                return date2 - date1 <= delta;
            else
                return date1 - date2 <= delta;
        }

        /// <summary>
        /// Returns the minimum of two timespan values.
        /// </summary>
        /// <param name="timespan1">Timespan value 1.</param>
        /// <param name="timespan2">Timespan value 2.</param>
        /// <returns>The minimum timespan.</returns>
        public static TimeSpan Min(TimeSpan timespan1, TimeSpan timespan2)
        {
            if (timespan1 <= timespan2)
                return timespan1;
            else
                return timespan2;
        }

        /// <summary>
        /// Returns the minimum of two timespan values.
        /// </summary>
        /// <param name="timespan1">Timespan value 1.</param>
        /// <param name="timespan2">Timespan value 2.</param>
        /// <returns>The minimum timespan.</returns>
        public static TimeSpan Max(TimeSpan timespan1, TimeSpan timespan2)
        {
            if (timespan1 >= timespan2)
                return timespan1;
            else
                return timespan2;
        }

        /// <summary>
        /// Calculates an integer percentage by dividing the numerator
        /// and denominator passed.
        /// </summary>
        /// <param name="numerator">The numerator.</param>
        /// <param name="denominator">The deniminator.</param>
        /// <returns>The calculated ratio as a percentage.</returns>
        /// <remarks>
        /// <note>
        /// This returns 0 if the denominator passed is zero.
        /// </note>
        /// </remarks>
        public static int CalcPercent(int numerator, int denominator)
        {
            if (denominator == 0)
                return 0;

            return (int)((double)numerator / (double)denominator * 100.0);
        }

        /// <summary>
        /// Attempts to parse an IP address from a string adding the ability
        /// to parse the two predefined addresses <b>ANY</b> and <b>LOOPBACK</b>.
        /// </summary>
        /// <param name="input">The IP address string.</param>
        /// <param name="address">Returns as the parsed address.</param>
        /// <returns><c>true</c> if the address was parsed successfully.</returns>
        public static bool TryParseIPAddress(string input, out IPAddress address)
        {
            if (String.Compare(input, "ANY", StringComparison.OrdinalIgnoreCase) == 0)
            {
                address = IPAddress.Any;
                return true;
            }
            else if (String.Compare(input, "LOOPBACK", StringComparison.OrdinalIgnoreCase) == 0)
            {
                address = IPAddress.Loopback;
                return true;
            }
            else
                return IPAddress.TryParse(input, out address);
        }

        /// <summary>
        /// Parses an IP address from a string adding the ability
        /// to parse the two predefined addresses <b>ANY</b> and <b>LOOPBACK</b>.
        /// </summary>
        /// <param name="input">The IP address string.</param>
        /// <returns>The parsed address.</returns>
        public static IPAddress ParseIPAddress(string input)
        {
            IPAddress address;

            if (String.Compare(input, "ANY", StringComparison.OrdinalIgnoreCase) == 0)
            {
                return IPAddress.Any;
            }
            else if (String.Compare(input, "LOOPBACK", StringComparison.OrdinalIgnoreCase) == 0)
            {
                return IPAddress.Loopback;
            }
            else
            {
                if (IPAddress.TryParse(input, out address))
                    return address;

                throw new ArgumentException("Invalid IP address");
            }
        }

        /// <summary>
        /// Converts an IPv4 address into an integer.
        /// </summary>
        /// <param name="address">The IPv4 address.</param>
        /// <returns>The IP address as a 32-bit integer.</returns>
        /// <exception cref="ArgumentException">Thrown for IPv6 addresses.</exception>
        public static int IPAddressToInt32(IPAddress address)
        {
            byte[] v;

            v = address.GetAddressBytes();
            if (v.Length != 4)
                throw new ArgumentException("Only IPv4 addresses may be converted to 32-bit integers", "address");

            return (v[0] << 24) | (v[1] << 16) | (v[2] << 8) | v[3];
        }

        /// <summary>
        /// Returns an <see cref="IPAddress" /> decoded from a 32-bit integer.
        /// </summary>
        /// <param name="address">The IPv4 address encoded as an integer.</param>
        public static IPAddress IPAddressFromInt32(int address)
        {
            return new IPAddress(new byte[] { (byte)(address >> 24), (byte)(address >> 16), (byte)(address >> 8), (byte)address });
        }

#if !SILVERLIGHT

        /// <summary>
        /// Returns the IP address and subnet for the first network
        /// adapter that appears to be connected.  The loopback address
        /// (127.0.0.1) and 255.255.255.0 will be returned if there
        /// are no connected adapters.
        /// </summary>
        /// <param name="address">Returns as the adapter's IP address.</param>
        /// <param name="subnet">Returns as the adapter's subnet mask.</param>
        /// <remarks>
        /// <note>
        /// This method currently ignores IPv6 addresses.
        /// </note>
        /// </remarks>
        public static void GetNetworkInfo(out IPAddress address, out IPAddress subnet)
        {
            NetworkInterface[]                      adapters = NetworkInterface.GetAllNetworkInterfaces();
            UnicastIPAddressInformationCollection   addrInfo;

            foreach (NetworkInterface adapter in adapters)
            {
                if (adapter.OperationalStatus != OperationalStatus.Up ||
                    adapter.NetworkInterfaceType == NetworkInterfaceType.GenericModem ||
                    adapter.NetworkInterfaceType == NetworkInterfaceType.Tunnel ||
                    adapter.NetworkInterfaceType == NetworkInterfaceType.Loopback ||
                    adapter.Description == "Microsoft Loopback Adapter")

                    continue;

                addrInfo = adapter.GetIPProperties().UnicastAddresses;
                foreach (var info in addrInfo)
                {
                    if (info.Address == null)
                        continue;

                    if (info.Address.AddressFamily != AddressFamily.InterNetwork)
                        continue;   // Ignore IPv6 addresses

                    address = info.Address;
                    subnet  = info.IPv4Mask;

                    if (address == null)
                        continue;

                    if (subnet == null)
                        subnet = IPAddress.Parse("255.255.255.0");

                    return;
                }
            }

            address = IPAddress.Loopback;
            subnet  = IPAddress.Parse("255.255.255.0");
        }

        /// <summary>
        /// Determines whether two IP addresses are within the same subnet.
        /// </summary>
        /// <param name="address1">The first IP address.</param>
        /// <param name="address2">The second IP address.</param>
        /// <param name="subnet">The subnet mask.</param>
        /// <returns><c>true</c> is the two addresses are within the same subnet.</returns>
        /// <remarks>
        /// <note>This works only for IPv4 addresses.</note>
        /// </remarks>
        /// <exception cref="ArgumentException">Thrown if any of the parameters are not IPv4 addresses.</exception>
        public static bool InSameSubnet(IPAddress address1, IPAddress address2, IPAddress subnet)
        {
            if (address1.AddressFamily != AddressFamily.InterNetwork)
                throw new ArgumentException("Only IPv4 addresses are allowed.", "address1");

            if (address2.AddressFamily != AddressFamily.InterNetwork)
                throw new ArgumentException("Only IPv4 addresses are allowed.", "address2");

            if (subnet.AddressFamily != AddressFamily.InterNetwork)
                throw new ArgumentException("Only IPv4 subnet masks are allowed.", "subnet");

            byte[] addr1 = address1.GetAddressBytes();
            byte[] addr2 = address2.GetAddressBytes();
            byte[] mask  = subnet.GetAddressBytes();

            for (int i = 0; i < 4; i++)
                if ((addr1[i] & mask[i]) != (addr2[i] & mask[i]))
                    return false;

            return true;
        }

        /// <summary>
        /// Returns the network adapter index for the NIC configured with a specific <see cref="IPAddress" />.
        /// </summary>
        /// <param name="address">The <see cref="IPAddress" />.</param>
        /// <returns>The network adapter index.</returns>
        /// <remarks>
        /// <note>
        /// If <see cref="IPAddress.Any" /> is passed then the index of the first active
        /// network card will be returned.
        /// </note>
        /// </remarks>
        /// <exception cref="SocketException">
        /// Thrown with the <see cref="SocketError.AddressNotAvailable" /> error code if 
        /// the IP address is not configured for any NIC.
        /// </exception>
        public static int GetNetworkAdapterIndex(IPAddress address)
        {
            NetworkInterface[] adapters = NetworkInterface.GetAllNetworkInterfaces();

            if (address.Equals(IPAddress.Any))
            {
                for (int index = 0; index < adapters.Length; index++)
                {
                    NetworkInterface adapter = adapters[index];

                    if (adapter.OperationalStatus != OperationalStatus.Up ||
                        adapter.NetworkInterfaceType == NetworkInterfaceType.GenericModem ||
                        adapter.NetworkInterfaceType == NetworkInterfaceType.Tunnel ||
                        adapter.NetworkInterfaceType == NetworkInterfaceType.Loopback)

                        continue;

                    return index;
                }
            }
            else
            {
                for (int index = 0; index < adapters.Length; index++)
                {
                    NetworkInterface        adapter = adapters[index];
                    IPInterfaceProperties   ipInfo = adapter.GetIPProperties();

                    foreach (UnicastIPAddressInformation uIPInfo in ipInfo.UnicastAddresses)
                    {
                        if (uIPInfo.Address.Equals(address))
                            return index;
                    }
                }
            }

            throw new SocketException((int)SocketError.AddressNotAvailable);
        }

#endif // !SILVERLIGHT

        /// <summary>
        /// Returns a shallow clone of a string name/value dictionary.
        /// </summary>
        /// <param name="input">The input dictionary.</param>
        /// <returns>The clone.</returns>
        public static Dictionary<string, string> Clone(Dictionary<string, string> input)
        {
            var clone = new Dictionary<string, string>(input.Comparer);

            foreach (string key in input.Keys)
                clone[key] = input[key];

            return clone;
        }

        /// <summary>
        /// Copies the contents of the source name/value dictionary to the 
        /// destination dictionary.
        /// </summary>
        /// <param name="source">The source dictionary.</param>
        /// <param name="destination">The destination dictionary.</param>
        public static void Copy(Dictionary<string, string> source, Dictionary<string, string> destination)
        {
            foreach (string key in source.Keys)
                destination[key] = source[key];
        }

        /// <summary>
        /// Rethrows an exception.
        /// </summary>
        /// <param name="e">The exception to be rethrown.</param>
        /// <remarks>
        /// <note>
        /// This method was written way back in the .NET Framework 1.1 days when a <c>throw</c>
        /// statement without an expression incorrectly set a new call stack for the exception.
        /// This behavior has been corrected now, so new code should simply use <c>throw;</c>
        /// to accomplish this function.  This method is being retained for backwards compatibility
        /// and a few situations where it has real value.
        /// </note>
        /// <para>
        /// This method is used when an application needs catch exceptions, do some processing
        /// (such as logging), and then rethrow the exception.  This method attempts to construct
        /// a new exception instance of the same type, using the exception passed as the
        /// inner exception.  This will ensure that the call stack where the original
        /// exception was thrown will be available for logging purposes.
        /// </para>
        /// <para>
        /// In some cases, the a new instance cannot be constructed.  This can happen if
        /// the there is no public constructor available that accepts a message string and
        /// inner exception as parameters.  In this case, the method simply rethrows the
        /// original exception.
        /// </para>
        /// </remarks>
        public static void Rethrow(Exception e)
        {
            Type        type = e.GetType();
            Exception   outer;

            try
            {

#if SILVERLIGHT
                outer = (Exception) type.Assembly.CreateInstance(type.FullName);
#else
                outer = (Exception)type.Assembly.CreateInstance(type.FullName, false, BindingFlags.CreateInstance, null, new object[] { e.Message, e }, null, null);
#endif
            }
            catch
            {

                outer = null;
            }

            if (outer != null)
                throw outer;
            else
                throw e;
        }

#if !SILVERLIGHT

        private const int CompressBufSize = 4096;

        /// <summary>
        /// Compresses a byte array using <b>Deflate</b> <a href="http://www.ietf.org/rfc/rfc1951.txt?number=1951">LZ77: RFC 1951</a>.
        /// </summary>
        /// <param name="source">The uncompressed source array.</param>
        /// <returns>The compressed output.</returns>
        public static byte[] Compress(byte[] source)
        {
            var ms = new MemoryStream(source.Length / 2);
            var cs = new DeflateStream(ms, CompressionMode.Compress, true);

            cs.Write(source, 0, source.Length);
            cs.Close();
            return ms.ToArray();
        }

        /// <summary>
        /// Compresses a byte array using <b>Deflate</b> <a href="http://www.ietf.org/rfc/rfc1951.txt?number=1951">LZ77: RFC 1951</a>
        /// to a stream.
        /// </summary>
        /// <param name="source">The uncompressed source array.</param>
        /// <param name="output">The output stream.</param>
        /// <returns>The compressed output.</returns>
        public static void Compress(byte[] source, Stream output)
        {
            var cs = new DeflateStream(output, CompressionMode.Compress, true);

            cs.Write(source, 0, source.Length);
            cs.Close();
        }

        /// <summary>
        /// Compresses the contents of the input stream from the current position to the end
        /// using <b>Deflate</b> <a href="http://www.ietf.org/rfc/rfc1951.txt?number=1951">LZ77: RFC 1951</a>
        /// to a stream.
        /// </summary>
        /// <param name="input">The decompressed input stream.</param>
        /// <param name="output">The compressed output stream.</param>
        public static void Compress(Stream input, Stream output)
        {
            var cs     = new DeflateStream(output, CompressionMode.Compress, true);
            var buffer = new byte[CompressBufSize];
            int cb;

            while (true)
            {
                cb = input.Read(buffer, 0, CompressBufSize);
                if (cb == 0)
                    break;

                cs.Write(buffer, 0, cb);
            }

            cs.Close();
        }

        /// <summary>
        /// Decompresses a <b>Deflate</b> <a href="http://www.ietf.org/rfc/rfc1951.txt?number=1951">LZ77: RFC 1951</a> compressed 
        /// byte array.
        /// </summary>
        /// <param name="source">The compressed source bytes.</param>
        /// <returns>The uncompressed output.</returns>
        public static byte[] Decompress(byte[] source)
        {
            var msCompressed   = new MemoryStream(source);
            var msDecompressed = new MemoryStream(source.Length);
            var cs             = new DeflateStream(msCompressed, CompressionMode.Decompress);
            var buf            = new byte[CompressBufSize];
            int cb;

            while (true)
            {
                cb = cs.Read(buf, 0, CompressBufSize);
                if (cb == 0)
                    break;

                msDecompressed.Write(buf, 0, cb);
            }

            return msDecompressed.ToArray();
        }

        /// <summary>
        /// Decompresses a <b>Deflate</b> <a href="http://www.ietf.org/rfc/rfc1951.txt?number=1951">LZ77: RFC 1951</a> compressed 
        /// byte array to a stream.
        /// </summary>
        /// <param name="source">The compressed source bytes.</param>
        /// <param name="output">The uncompressed output stream.</param>
        /// <returns>The decompressed output.</returns>
        public static void Decompress(byte[] source, Stream output)
        {
            var msCompressed = new MemoryStream(source);
            var cs           = new DeflateStream(msCompressed, CompressionMode.Decompress);
            var buf          = new byte[CompressBufSize];
            int cb;

            while (true)
            {
                cb = cs.Read(buf, 0, CompressBufSize);
                if (cb == 0)
                    break;

                output.Write(buf, 0, cb);
            }
        }

        /// <summary>
        /// Decompresses a <b>Deflate</b> <a href="http://www.ietf.org/rfc/rfc1951.txt?number=1951">LZ77: RFC 1951</a> compressed 
        /// stream from the current position to an output stream.
        /// </summary>
        /// <param name="input">The compressed input stream.</param>
        /// <param name="output">The unecompressed output stream.</param>
        public static void Decompress(Stream input, Stream output)
        {
            var cs  = new DeflateStream(input, CompressionMode.Decompress);
            var buf = new byte[CompressBufSize];
            int cb;

            while (true)
            {
                cb = cs.Read(buf, 0, CompressBufSize);
                if (cb == 0)
                    break;

                output.Write(buf, 0, cb);
            }
        }

        /// <summary>
        /// Compresses a byte array using <b>GZIP</b>.
        /// </summary>
        /// <param name="source">The uncompressed source array.</param>
        /// <returns>The compressed output.</returns>
        public static byte[] CompressGZip(byte[] source)
        {
            var ms = new MemoryStream(source.Length / 2);
            var cs = new GZipStream(ms, CompressionMode.Compress, true);

            cs.Write(source, 0, source.Length);
            cs.Close();
            return ms.ToArray();
        }

        /// <summary>
        /// Compresses a byte array using <b>GZIP</b> to a stream.
        /// </summary>
        /// <param name="source">The uncompressed source array.</param>
        /// <param name="output">The output stream.</param>
        /// <returns>The compressed output.</returns>
        public static void CompressGZip(byte[] source, Stream output)
        {
            var cs = new GZipStream(output, CompressionMode.Compress, true);

            cs.Write(source, 0, source.Length);
            cs.Close();
        }

        /// <summary>
        /// Compresses the contents of the input stream from the current position to the end
        /// using <b>GZIP</b> to a stream.
        /// </summary>
        /// <param name="input">The decompressed input stream.</param>
        /// <param name="output">The compressed output stream.</param>
        public static void CompressGZip(Stream input, Stream output)
        {
            var cs     = new GZipStream(output, CompressionMode.Compress, true);
            var buffer = new byte[CompressBufSize];
            int cb;

            while (true)
            {
                cb = input.Read(buffer, 0, CompressBufSize);
                if (cb == 0)
                    break;

                cs.Write(buffer, 0, cb);
            }

            cs.Close();
        }

        /// <summary>
        /// Decompresses a <b>GZIP</b> compressed byte array.
        /// </summary>
        /// <param name="source">The compressed source bytes.</param>
        /// <returns>The uncompressed output.</returns>
        public static byte[] DecompressGZip(byte[] source)
        {

            var msCompressed   = new MemoryStream(source);
            var msDecompressed = new MemoryStream(source.Length);
            var cs             = new GZipStream(msCompressed, CompressionMode.Decompress);
            var buf            = new byte[CompressBufSize];
            int cb;

            while (true)
            {
                cb = cs.Read(buf, 0, CompressBufSize);
                if (cb == 0)
                    break;

                msDecompressed.Write(buf, 0, cb);
            }

            return msDecompressed.ToArray();
        }

        /// <summary>
        /// Decompresses <b>GZIP</b> compressed byte array to a stream.
        /// </summary>
        /// <param name="source">The compressed source bytes.</param>
        /// <param name="output">The uncompressed output stream.</param>
        /// <returns>The decompressed output.</returns>
        public static void DecompressGZip(byte[] source, Stream output)
        {
            var msCompressed = new MemoryStream(source);
            var cs           = new GZipStream(msCompressed, CompressionMode.Decompress);
            var buf          = new byte[CompressBufSize];
            int cb;

            while (true)
            {
                cb = cs.Read(buf, 0, CompressBufSize);
                if (cb == 0)
                    break;

                output.Write(buf, 0, cb);
            }
        }

        /// <summary>
        /// Decompresses a <b>GZIP</b> compressed stream from the current position to an output stream.
        /// </summary>
        /// <param name="input">The compressed input stream.</param>
        /// <param name="output">The unecompressed output stream.</param>
        public static void DecompressGZip(Stream input, Stream output)
        {
            var cs  = new GZipStream(input, CompressionMode.Decompress);
            var buf = new byte[CompressBufSize];
            int cb;

            while (true)
            {
                cb = cs.Read(buf, 0, CompressBufSize);
                if (cb == 0)
                    break;

                output.Write(buf, 0, cb);
            }
        }

#endif // !SILVERLIGHT

        /// <summary>
        /// Searches for the first occurance of a pattern of bytes within a source
        /// array, returning -1 if the pattern cannot be found.
        /// </summary>
        /// <param name="source">The source byte array.</param>
        /// <param name="pattern">The desired pattern.</param>
        /// <returns>The index of the first byte of the pattern if found, -1 otherwise.</returns>
        public static int IndexOf(byte[] source, byte[] pattern)
        {
            int     pos;
            byte    patFirst;
            byte    patLast;

            if (source == null)
                throw new ArgumentException("[source] cannot be null.", "source");

            if (pattern == null)
                throw new ArgumentException("[pattern] cannot be null.", "pattern");

            if (pattern.Length == 0)
                throw new ArgumentException("[pattern] must have a non-zero length.", "pattern");

            if (pattern.Length > source.Length)
                return -1;

            if (pattern.Length == 1)
            {
                patFirst = pattern[0];
                for (int i = 0; i < source.Length; i++)
                    if (source[i] == patFirst)
                        return i;

                return -1;
            }

            patFirst = pattern[0];
            patLast = pattern[pattern.Length - 1];

            pos = 0;
            while (pos < source.Length - pattern.Length + 1)
            {
                if (source[pos] != patFirst)
                {
                    pos++;
                    continue;
                }

                if (source[pos + pattern.Length - 1] != patLast)
                {
                    pos++;
                    continue;
                }

                bool match = true;

                for (int i = 0; i < pattern.Length; i++)
                    if (source[pos + i] != pattern[i])
                    {
                        match = false;
                        break;
                    }

                if (match)
                    return pos;

                pos++;
            }

            return -1;
        }

        /// <summary>
        /// Searches for the first occurance of a pattern of bytes within this
        /// specified section of a source array, returning -1 if the pattern 
        /// cannot be found.
        /// </summary>
        /// <param name="source">The source byte array.</param>
        /// <param name="pattern">The desired pattern.</param>
        /// <param name="startPos">Starting index of the section to be searched.</param>
        /// <param name="count">Number of bytes to be searched.</param>
        /// <returns>
        /// <para>
        /// The index of the first byte of the pattern if found, -1 otherwise.
        /// </para>
        /// <note>
        /// The index returned is relative to the beginning of the source array
        /// not the <paramref name="startPos" /> parameter.
        /// </note>
        /// </returns>
        public static int IndexOf(byte[] source, byte[] pattern, int startPos, int count)
        {
            int     pos;
            byte    patFirst;
            byte    patLast;

            if (source == null)
                throw new ArgumentException("[source] cannot be null.", "source");

            if (pattern == null)
                throw new ArgumentException("[pattern] cannot be null.", "pattern");

            if (pattern.Length == 0)
                throw new ArgumentException("[pattern] must have a non-zero length.", "pattern");

            if (source.Length == 0 || count == 0)
                return -1;

            if (startPos < 0 || startPos >= source.Length || count < 0 || startPos + count > source.Length)
                throw new IndexOutOfRangeException("startPos and count must be within the array bounds.");

            if (pattern.Length > count)
                return -1;

            if (pattern.Length == 1)
            {
                patFirst = pattern[0];
                for (int i = startPos; i < startPos + count; i++)
                    if (source[i] == patFirst)
                        return i;

                return -1;
            }

            patFirst = pattern[0];
            patLast = pattern[pattern.Length - 1];

            pos = startPos;
            while (pos < startPos + count - pattern.Length + 1)
            {
                if (source[pos] != patFirst)
                {
                    pos++;
                    continue;
                }

                if (source[pos + pattern.Length - 1] != patLast)
                {
                    pos++;
                    continue;
                }

                bool match = true;

                for (int i = 0; i < pattern.Length; i++)
                    if (source[pos + i] != pattern[i])
                    {
                        match = false;
                        break;
                    }

                if (match)
                    return pos;

                pos++;
            }

            return -1;
        }

        /// <summary>
        /// Formats an integer specifying a number of bytes into a string the approximate
        /// number of bytes and one of the suffixes: <b>KB</b>, <b>MB</b>, or 
        /// <b>GB</b> depending on the magnitude of the number passed.
        /// </summary>
        /// <param name="size">The byte size.</param>
        /// <returns>The formatted string.</returns>
        /// <exception cref="ArgumentException">Thrown if the size passed is less than zero.</exception>
        public static string FormatAsByteSize(long size)
        {
            const long K = 1024L;
            const long M = 1024L * 1024L;
            const long G = 1024L * 1024L * 1024L;

            if (size < 0)
                throw new ArgumentException("Size is less than zero.");

            if (size < M)
            {
                if (size < K)
                    return string.Format("{0}", size);
                else if (size % K == 0)
                    return string.Format("{0}KB", size / K);
                else if (size < 10 * K)
                    return string.Format("{0:#.#}KB", (double)size / K);
                else
                    return string.Format("{0}KB", size / K);
            }
            else if (size < G)
            {
                if (size % M == 0)
                    return string.Format("{0}MB", size / M);
                else if (size < 10 * M)
                    return string.Format("{0:#.#}MB", (double)size / M);
                else
                    return string.Format("{0}MB", size / M);
            }
            else if (size % G == 0)
                return string.Format("{0}GB", size / G);
            else if (size < 10 * G)
                return string.Format("{0:#.#}GB", (double)size / G);
            else
                return string.Format("{0}GB", size / G);
        }

        /// <summary>
        /// Formats the 10 digit string passed as a human readable phone number
        /// surrounding the area code with parentheses.  Note that empty strings may 
        /// also be passed.
        /// </summary>
        /// <param name="value">The raw phone number.</param>
        /// <returns>The formatted phone number.</returns>
        /// <exception cref="FormatException">Thrown if the input is not valid.</exception>
        public static string FormatPhone(string value)
        {
            return FormatPhone(value, true);
        }

        /// <summary>
        /// Formats the 10 digit string passed as a human readable phone number
        /// surrounding the area code with parentheses.  Note that empty strings may 
        /// also be passed.
        /// </summary>
        /// <param name="value">The raw phone number.</param>
        /// <param name="npaParens">
        /// Pass <c>true</c> to format the area code with parentheses, <c>false</c>
        /// to use a dash seperator.
        /// </param>
        /// <returns>The formatted phone number.</returns>
        /// <exception cref="FormatException">Thrown if the input is not valid.</exception>
        public static string FormatPhone(string value, bool npaParens)
        {
            value = value.Trim();
            if (value == string.Empty)
                return value;

            value = ParsePhone(value);

            foreach (char ch in value)
                if (!Char.IsDigit(ch))
                    throw new FormatException("Illegal character in phone number.");

            if (npaParens)
                return string.Format("({0}) {1}-{2}", value.Substring(0, 3), value.Substring(3, 3), value.Substring(6, 4));
            else
                return string.Format("{0}-{1}-{2}", value.Substring(0, 3), value.Substring(3, 3), value.Substring(6, 4));
        }

        /// <summary>
        /// Parses the formatted phone number passed into a 10 digit string.
        /// </summary>
        /// <param name="value">The formatted phone number.</param>
        /// <returns>The unformatted digits.</returns>
        /// <exception cref="FormatException">Thrown if the input is not valid.</exception>
        public static string ParsePhone(string value)
        {
            if (value == null)
                throw new FormatException("Phone number cannot be null.");

            value = value.Trim();
            if (value.Length == 0)
                throw new FormatException("Phone number must have 10 digits.");

            var sb = new StringBuilder(value.Length);

            // Strip out any spaces, periods, params, dashes, or commas and
            // verify that the remaining characters are all digits.

            for (int i = 0; i < value.Length; i++)
            {
                var ch = value[i];

                switch (ch)
                {

                    case ' ':
                    case '.':
                    case '(':
                    case ')':
                    case '-':
                    case ',':

                        continue;

                    default:

                        if (!Char.IsDigit(ch))
                            throw new FormatException(string.Format("Non-digit character in phone number at position [{0}].", i + 1));

                        sb.Append(ch);
                        break;
                }
            }

            if (sb.Length != 10)
                throw new FormatException("Phone number must have 10 digits.");

            return sb.ToString();
        }

        /// <summary>
        /// Parses the email address passed.
        /// </summary>
        /// <param name="value">The email address.</param>
        /// <returns>The parsed address.</returns>
        /// <exception cref="FormatException">Thrown if the input is not valid.</exception>
        public static string ParseEmail(string value)
        {
            if (value == null)
                throw new FormatException("Email address cannot be null.");

            const string EMailRegEx = @"^"                                               // Start anchor
                                    + @"(([\w-]+\.)+[\w-]+|([a-zA-Z]{1}|[\w-]{2,}))@"    // User name and "@" sign
                                    + @"([a-zA-Z]+[\w-]+\.)+"                            // Host name
                                    + @"[a-zA-Z]{2,4}"                                   // Top-level domain
                                    + @"$";                                              // End anchor

            var regex = new Regex(EMailRegEx);

            value = value.Trim();
            if (!regex.IsMatch(value))
                throw new FormatException("Invalid Email address.");

            return value;
        }

        /// <summary>
        /// Converts a date/time into a string suitable for display.
        /// </summary>
        /// <param name="time">The time.</param>
        /// <returns>The display string.</returns>
        /// <remarks>
        /// <note>
        /// This is currently hardcoded to return US style time non-24 hour times.
        /// </note>
        /// </remarks>
        public static string ToTimeString(DateTime time)
        {
            int     hour = time.Hour;
            int     minute = time.Minute;
            string  amPM;

            if (hour == 0)
                return "12am";
            else if (hour < 12)
                amPM = "am";
            else if (hour == 12)
                amPM = "pm";
            else
            {
                amPM = "pm";
                hour -= 12;
            }

            if (minute == 0)
                return string.Format("{0}{1}", hour, amPM);
            else
                return string.Format("{0}:{1:0#}{2}", hour, minute, amPM);
        }

        /// <summary>
        /// Converts a time offset from 12:00am into a string suitable for display.
        /// </summary>
        /// <param name="timeOffset">The time offset.</param>
        /// <returns>The display string.</returns>
        /// <remarks>
        /// <note>
        /// This is currently hardcoded to return US style time non-24 hour times.
        /// </note>
        /// </remarks>
        /// <exception cref="ArgumentException">Thrown if the time offset is negative or greater than or equal to 24 hours.</exception>
        public static string ToTimeString(TimeSpan timeOffset)
        {
            var oneDay = TimeSpan.FromHours(24);

            if (timeOffset < TimeSpan.Zero || timeOffset > Helper.Multiply(oneDay, 2))
                throw new ArgumentException("Invalid time offset", "timeOffset");

            if (timeOffset >= oneDay)
                timeOffset -= oneDay;   // Handle time offsets that cross midnight

            return ToTimeString(DateTime.Today + timeOffset);
        }

        /// <summary>
        /// Converts the date passed into a nicely date plus time string.
        /// </summary>
        /// <param name="date">The date to be converted.</param>
        /// <returns>The formatted string.</returns>
        public static string ToDateTimeString(DateTime date)
        {
            return date.ToShortDateString() + " " + ToTimeString(date);
        }

        /// <summary>
        /// Converts the time offset from 12:00am and the duration into a string
        /// suitable for display.
        /// </summary>
        /// <param name="startOffset">The start time.</param>
        /// <param name="duration">The duration.</param>
        /// <returns>The formatted string.</returns>
        public static string ToTimeRange(TimeSpan startOffset, TimeSpan duration)
        {
            if (duration == TimeSpan.Zero)
                return ToTimeString(startOffset);

            if (startOffset == TimeSpan.Zero && duration == TimeSpan.FromDays(1))
                return "All Day";
            else
                return string.Format("{0}-{1}", ToTimeString(startOffset), ToTimeString(startOffset + duration));
        }

        /// <summary>
        /// Converts a <see cref="DateTime" /> and a duration into a string
        /// suitable for display.
        /// </summary>
        /// <param name="start">The start date.</param>
        /// <param name="duration">The duration.</param>
        /// <returns>The formatted string.</returns>
        public static string ToTimeRange(DateTime start, TimeSpan duration)
        {
            return ToTimeRange(start - start.Date, duration);
        }

        private static string[] monthLabels = new string[] {

                "January",
                "February",
                "March",
                "April",
                "May",
                "June",
                "July",
                "August",
                "September",
                "October",
                "November",
                "December"
            };

        /// <summary>
        /// Returns a date range as a nicely formatted string.
        /// </summary>
        /// <param name="start">The starting date.</param>
        /// <param name="end">The ending date.</param>
        /// <returns>The formatted date range.</returns>
        public static string ToDateRange(DateTime start, DateTime end)
        {
            if (start.Year == end.Year)
            {
                if (start.Month == end.Month)
                    return string.Format("{0} {1}-{2}, {3}", monthLabels[start.Month - 1], start.Day, end.Day, start.Year);
                else
                    return string.Format("{0} {1} - {2} {3}, {4}", monthLabels[start.Month - 1], start.Day, monthLabels[end.Month - 1], end.Day, start.Year);
            }
            else
                return string.Format("{0} {1}, {2} - {3} {4}, {5}", monthLabels[start.Month - 1], start.Day, start.Year, monthLabels[end.Month - 1], end.Day, end.Year);
        }

        /// <summary>
        /// Returns the date range for a weekly schedule beginning on the specified day
        /// as a nicely formatted string.
        /// </summary>
        /// <param name="start">The start date for the schedule.</param>
        public static string ToWeekDateRange(DateTime start)
        {
            DateTime end;

            start = start.Date;
            end = start + TimeSpan.FromDays(6);

            return ToDateRange(start, end);
        }

        /// <summary>
        /// Waits for a boolean delegate to return <c>true</c>.
        /// </summary>
        /// <param name="action">The boolean delegate.</param>
        /// <param name="timeout">The maximum time to wait.</param>
        /// <param name="pollTime">The time to wait between polling or <c>null</c> for a reasonable default.</param>
        /// <exception cref="TimeoutException">Thrown if the never returned <c>true</c> before the timeout.</exception>
        /// <remarks>
        /// This method periodically calls <paramref name="action"/> until it
        /// returns <c>true</c> or <pararef name="timeout"/> exceeded.
        /// </remarks>
        public static void WaitFor(Func<bool> action, TimeSpan timeout, TimeSpan? pollTime = null)
        {
            var timeLimit = DateTimeOffset.UtcNow + timeout;

            if (!pollTime.HasValue)
            {
                pollTime = TimeSpan.FromMilliseconds(250);
            }

            while (true)
            {
                if (action())
                {
                    return;
                }

                Thread.Sleep(pollTime.Value);

                if (DateTimeOffset.UtcNow >= timeLimit)
                {
                    throw new TimeoutException();
                }
            }
        }

        /// <summary>
        /// Asynchronously waits for a boolean delegate to return <c>true</c>.
        /// </summary>
        /// <param name="action">The boolean delegate.</param>
        /// <param name="timeout">The maximum time to wait.</param>
        /// <param name="pollTime">The time to wait between polling or <c>null</c> for a reasonable default.</param>
        /// <exception cref="TimeoutException">Thrown if the never returned <c>true</c> before the timeout.</exception>
        /// <remarks>
        /// This method periodically calls <paramref name="action"/> until it
        /// returns <c>true</c> or <pararef name="timeout"/> exceeded.
        /// </remarks>
        public static async Task WaitForAsync(Func<Task<bool>> action, TimeSpan timeout, TimeSpan? pollTime = null)
        {
            var timeLimit = DateTimeOffset.UtcNow + timeout;

            if (!pollTime.HasValue)
            {
                pollTime = TimeSpan.FromMilliseconds(250);
            }

            while (true)
            {
                if (await action())
                {
                    return;
                }

                await Task.Delay(pollTime.Value);

                if (DateTimeOffset.UtcNow >= timeLimit)
                {
                    throw new TimeoutException();
                }
            }
        }

        /// <summary>
        /// Calculates the dimensions to use when resizing an image while maintaining its aspect ration.
        /// </summary>
        /// <param name="desiredWidth">The new desired image width.</param>
        /// <param name="desiredHeight">The new desired image width.</param>
        /// <param name="imageWidth">The image's current width.</param>
        /// <param name="imageHeight">The image's current height.</param>
        /// <param name="outputWidth">Returns as the width to use for the output image.</param>
        /// <param name="outputHeight">Returns as the height to use for the output image.</param>
        public static void CalcImageDimensions(int desiredWidth, int desiredHeight, int imageWidth, int imageHeight, out int outputWidth, out int outputHeight)
        {
            int     inputWidth  = imageWidth;
            int     inputHeight = imageHeight;
            float   nPercent    = 0;
            float   nPercentW   = 0;
            float   nPercentH   = 0;

            nPercentW = ((float)desiredWidth / (float)inputWidth);
            nPercentH = ((float)desiredHeight / (float)inputHeight);

            if (nPercentH < nPercentW)
                nPercent = nPercentH;
            else
                nPercent = nPercentW;

            outputWidth = (int)(inputWidth * nPercent);
            outputHeight = (int)(inputHeight * nPercent);
        }

        /// <summary>
        /// Creates an instance of a type.
        /// </summary>
        /// <typeparam name="T">The type being returned.</typeparam>
        /// <param name="type">The type instance.</param>
        /// <returns>The created object instance.</returns>
        /// <remarks>
        /// <note>
        /// The type must implement a parameter-less constructor.
        /// </note>
        /// </remarks>
        public static T CreateInstance<T>(Type type)
        {
            return (T)type.Assembly.CreateInstance(type.FullName);
        }

        /// <summary>
        /// Attempts to recursively deaggregate an <see cref="AggregateException"/>.
        /// </summary>
        /// <param name="e">The candidate exception.</param>
        /// <returns>
        /// If <paramref name="e"/> is a <see cref="AggregateException"/> and it has only a 
        /// single inner exception, then that inner exception will be recursively deaggregated 
        /// (if possible) and returned.  Otherwise, the <paramref name="e"/> parameter value
        /// will be returned.
        /// </returns>
        public static Exception TryDeaggregateException(Exception e)
        {
            var aggregateException = e as AggregateException;

            if (aggregateException == null || aggregateException.InnerExceptions.Count > 1)
            {
                return e;
            }

            return TryDeaggregateException(aggregateException.InnerException);
        }


        /// <summary>
        /// Implements the <see cref="AggregateException"/> de-aggregation behavior.
        /// </summary>
        /// <param name="e">The aggregate exception.</param>
        /// <param name="alwaysThrowSingle">Specifies that even aggregate exceptions with multiple values should be converted.</param>
        /// <param name="favorTimeout">
        /// Indicates that any nested <see cref="TimeoutException"/>s should be thrown over any other
        /// exception.  This implies <paramref name="alwaysThrowSingle"/>=<c>true</c>.
        /// </param>
        /// <remarks>
        /// <note>
        /// This method just returns if the caller should rethrow the original exception.
        /// </note>
        /// </remarks>
        private static void HandleAggregateTryThrow(AggregateException e, bool alwaysThrowSingle = false, bool favorTimeout = false)
        {
            e = e.Flatten();

            if (e.InnerExceptions.Count == 1)
            {
                throw e.InnerException;
            }

            if (favorTimeout)
            {
                foreach (var innerException in e.Flatten().InnerExceptions)
                {
                    if (innerException is TimeoutException)
                    {
                        throw innerException;
                    }
                }
            }

            if (alwaysThrowSingle)
            {
                throw e.InnerException;
            }
        }

        /// <summary>
        /// Wraps a call to an <paramref name="action"/> with an exception handler that
        /// can convert thrown <see cref="AggregateException"/>s into the single base 
        /// exception.
        /// </summary>
        /// <param name="action">The action to be performed.</param>
        /// <param name="alwaysThrowSingle">Specifies that even aggregate exceptions with multiple values should be converted.</param>
        /// <param name="favorTimeout">
        /// Indicates that any nested <see cref="TimeoutException"/>s should be thrown over any other
        /// exception.  This implies <paramref name="alwaysThrowSingle"/>=<c>true</c>.
        /// </param>
        public static void TryThrowSingle(Action action, bool alwaysThrowSingle = false, bool favorTimeout = false)
        {
            Contract.Requires<ArgumentNullException>(action != null);

            try
            {
                action();
            }
            catch (AggregateException e)
            {
                HandleAggregateTryThrow(e, alwaysThrowSingle, favorTimeout);
                throw;
            }
        }

        /// <summary>
        /// Wraps a call to an <paramref name="action"/> function with an exception handler that
        /// can convert thrown <see cref="AggregateException"/>s into the single base 
        /// exception.
        /// </summary>
        /// <typeparam name="TResult">The action result type.</typeparam>
        /// <param name="action">The action to be performed.</param>
        /// <param name="alwaysThrowSingle">Specifies that even aggregate exceptions with multiple values should be converted.</param>
        /// <param name="favorTimeout">
        /// Indicates that any nested <see cref="TimeoutException"/>s should be thrown over any other
        /// exception.  This implies <paramref name="alwaysThrowSingle"/>=<c>true</c>.
        /// </param>
        /// <returns>The action result.</returns>
        public static TResult TryThrowSingle<TResult>(Func<TResult> action, bool alwaysThrowSingle = false, bool favorTimeout = false)
        {
            Contract.Requires<ArgumentNullException>(action != null);

            try
            {
                return action();
            }
            catch (AggregateException e)
            {
                HandleAggregateTryThrow(e, alwaysThrowSingle, favorTimeout);
                throw;
            }
        }

        /// <summary>
        /// Wraps a call to an <paramref name="action"/> with an exception handler that
        /// can convert thrown <see cref="AggregateException"/>s into the single base 
        /// exception.
        /// </summary>
        /// <param name="action">The action to be performed.</param>
        /// <param name="alwaysThrowSingle">Specifies that even aggregate exceptions with multiple values should be converted.</param>
        /// <param name="favorTimeout">
        /// Indicates that any nested <see cref="TimeoutException"/>s should be thrown over any other
        /// exception.  This implies <paramref name="alwaysThrowSingle"/>=<c>true</c>.
        /// </param>
        public static async Task TryThrowSingleAsync(Func<Task> action, bool alwaysThrowSingle = false, bool favorTimeout = false)
        {
            Contract.Requires<ArgumentNullException>(action != null);

            try
            {
                await action();
            }
            catch (AggregateException e)
            {
                HandleAggregateTryThrow(e, alwaysThrowSingle, favorTimeout);
                throw;
            }
        }

        /// <summary>
        /// Wraps a call to an <paramref name="action"/> function with an exception handler that
        /// can convert thrown <see cref="AggregateException"/>s into the single base 
        /// exception.
        /// </summary>
        /// <param name="action">The action to be performed.</param>
        /// <param name="alwaysThrowSingle">Specifies that even aggregate exceptions with multiple values should be converted.</param>
        /// <param name="favorTimeout">
        /// Indicates that any nested <see cref="TimeoutException"/>s should be thrown over any other
        /// exception.  This implies <paramref name="alwaysThrowSingle"/>=<c>true</c>.
        /// </param>
        /// <returns>The action result.</returns>
        public static async Task<TResult> TryThrowSingleAsync<TResult>(Func<Task<TResult>> action, bool alwaysThrowSingle = false, bool favorTimeout = false)
        {
            Contract.Requires<ArgumentNullException>(action != null);

            try
            {
                return await action();
            }
            catch (AggregateException e)
            {
                HandleAggregateTryThrow(e, alwaysThrowSingle, favorTimeout);
                throw;
            }
        }

        /// <summary>
        /// Asynchronously waits for all of the <see cref="Task"/>s passed to complete.
        /// </summary>
        /// <param name="tasks">The tasks to wait on.</param>
        public static async Task WaitAllAsync(IEnumerable<Task> tasks)
        {
            foreach (var task in tasks)
            {
                await task;
            }
        }

        /// <summary>
        /// Asynchronously waits for all of the <see cref="Task"/>s passed to complete.
        /// </summary>
        /// <param name="tasks">The tasks to wait on.</param>
        public static async Task WaitAllAsync(params Task[] tasks)
        {
            foreach (var task in tasks)
            {
                await task;
            }
        }

        /// <summary>
        /// Asynchronously waits for all of the <see cref="Task"/>s passed to complete.
        /// </summary>
        /// <param name="tasks">The tasks to wait for.</param>
        /// <param name="timeout">The optional timeout.</param>
        /// <param name="cancellationToken">The optional cancellation token.</param>
        /// <exception cref="TimeoutException">Thrown if the <paramref name="timeout"/> was exceeded.</exception>
        public static async Task WaitAllAsync(IEnumerable<Task> tasks, TimeSpan? timeout = null, CancellationToken? cancellationToken = null)
        {
            // There isn't a super clean way to implement this other than polling.

            if (!timeout.HasValue)
            {
                timeout = TimeSpan.FromDays(365); // Set an essentially infinite timeout
            }

            if (!cancellationToken.HasValue)
            {
                cancellationToken = CancellationToken.None;
            }

            var stopwatch = new Stopwatch();

            stopwatch.Start();

            while (true)
            {
                var isCompleted = true;

                foreach (var task in tasks)
                {
                    if (!task.IsCompleted)
                    {
                        isCompleted = false;
                    }
                }

                if (isCompleted)
                {
                    return;
                }

                cancellationToken.Value.ThrowIfCancellationRequested();

                if (stopwatch.Elapsed >= timeout)
                {
                    throw new TimeoutException();
                }

                await Task.Delay(250);
            }
        }

        /// <summary>
        /// Promotes the object passed the the specified (or maximum) heap object generation.
        /// </summary>
        /// <param name="instance">The object instance.</param>
        /// <param name="generation">The optional generation number or <see cref="GC.MaxGeneration"/> by default.</param>
        /// <remarks>
        /// <para>
        /// This method is intended for use by applications early during initialization to ensure
        /// that very long-lived objects are proactively moved to the oldest heap object generation
        /// so they won't impact the garbarge of the ephermal generations.  This is particuarily
        /// important for objects like network or file I/O buffers that are large and will typically 
        /// be pinned in place while I/O operations are in progress.
        /// </para>
        /// <para>
        /// This method works by running the garbage collector for the specified generation (and younger)
        /// until the object passed becomes a member of the desired generation.  Doing this for one
        /// object implies that all objects that were constructed before this object and are still live
        /// will also become a member of the generation.  This means that applications need only call
        /// this method once for the last object to be promoted is constructed.
        /// </para>
        /// </remarks>
        public static void GCPromote(object instance, int? generation = null)
        {
            if (instance == null)
            {
                throw new ArgumentNullException("instance");
            }

            var gen   = generation ?? GC.MaxGeneration;
            var count = 0;

            do
            {
                GC.Collect(gen, GCCollectionMode.Forced, blocking: true);

            } while (GC.GetGeneration(instance) < gen && ++count < gen);
        }

#if !MOBILE_DEVICE

        /// <summary>
        /// Downloads the data referenced by a <see cref="Uri" /> to a file.
        /// </summary>
        /// <param name="uri">The target URI.</param>
        /// <param name="path">Path to the output file.</param>
        /// <param name="timeout">The operation timeout.</param>
        /// <param name="response">Returns as the <see cref="HttpWebResponse" /> instance.</param>
        /// <returns>The number of bytes downloaded.</returns>
        /// <remarks>
        /// <para>
        /// Applications should examine the <paramref name="response" /> returned to examine status
        /// code to determine whether the operation succeeded or failed.  The response headers may
        /// also be useful.
        /// </para>
        /// </remarks>
        public static long WebDownload(Uri uri, string path, TimeSpan timeout, out HttpWebResponse response)
        {
            long cb;

            try
            {
                using (var fs = new FileStream(path, FileMode.Create, FileAccess.ReadWrite))
                {
                    cb = WebDownload(uri, fs, timeout, out response);
                }

                if (response.StatusCode != HttpStatusCode.OK)
                {
                    Helper.DeleteFile(path);
                    return 0;
                }

                return cb;
            }
            catch
            {
                Helper.DeleteFile(path);
                throw;
            }
        }

        /// <summary>
        /// Downloads the data referenced by a <see cref="Uri" /> to a <see cref="Stream" />.
        /// </summary>
        /// <param name="uri">The target URI.</param>
        /// <param name="output">Path to the output stream.</param>
        /// <param name="timeout">The operation timeout.</param>
        /// <param name="response">Returns as the <see cref="HttpWebResponse" /> instance on success.</param>
        /// <returns>The number of bytes downloaded.</returns>
        /// <remarks>
        /// <para>
        /// Applications should examine the <paramref name="response" /> returned to examine status
        /// code to determine whether the operation succeeded or failed.  The response headers may
        /// also be useful.
        /// </para>
        /// </remarks>
        public static long WebDownload(Uri uri, Stream output, TimeSpan timeout, out HttpWebResponse response)
        {
            HttpWebRequest request;

            request                  = (HttpWebRequest)WebRequest.Create(uri);
            request.Timeout          = (int)timeout.TotalMilliseconds;
            request.ReadWriteTimeout = 10000;
            response = (HttpWebResponse)request.GetResponse();
            request.CachePolicy = new HttpRequestCachePolicy(HttpRequestCacheLevel.NoCacheNoStore);

            using (var download = response.GetResponseStream())
            {
                byte[]  buffer = new byte[16184];
                long    cbTotal = 0;
                int     cb;

                while (true)
                {
                    cb = download.Read(buffer, 0, buffer.Length);
                    if (cb == 0)
                        break;  // EOF

                    cbTotal += cb;
                    output.Write(buffer, 0, cb);
                }

                return response.StatusCode == HttpStatusCode.OK ? cbTotal : 0;
            }
        }

#endif // !MOBILE_DEVICE

#if WINFULL

        /// <summary>
        /// Attempts to parse a color from a string.
        /// </summary>
        /// <param name="input">The color definition.</param>
        /// <returns>The parsed <see cref="Color" />.</returns>
        /// <exception cref="ArgumentNullException">Thrown if <paramref name="input" /> is <c>null</c>.</exception>
        /// <exception cref="FormatException">Thrown if the <paramref name="input" /> could not be parsed.</exception>
        /// <remarks>
        /// This method parses color names as defined by the <see cref="Color" /> class
        /// as well as RGB values formatted as three RGB or four byte RGBA HEX strings with or
        /// without a leading "#" character.
        /// </remarks>
        public static Color ParseColor(string input)
        {
            if (input == null)
                throw new ArgumentNullException("input");

            Color color;

            if (TryParseColor(input, out color))
                return color;

            throw new FormatException(string.Format("Cannot parse color [{0}].", input));
        }

        /// <summary>
        /// Attempts to parse a color from a string.
        /// </summary>
        /// <param name="input">The color definition.</param>
        /// <param name="color">Returns as the parsed color on success.</param>
        /// <returns><c>true</c> on success.</returns>
        /// <remarks>
        /// This method parses color names as defined by the <see cref="Color" /> class
        /// as well as RGB values formatted as three RGB or four byte RGBA HEX strings with or
        /// without a leading "#" character.
        /// </remarks>
        public static bool TryParseColor(string input, out Color color)
        {
            color = Color.Transparent;

            if (input == null || input.Length == 0)
                return false;

            if (colorNames == null)
            {
                // Initialize the global color name mappings.

                var colors = new Dictionary<string, Color>(StringComparer.InvariantCultureIgnoreCase);

                colors["AliceBlue"] = Color.AliceBlue;
                colors["AntiqueWhite"] = Color.AntiqueWhite;
                colors["Aqua"] = Color.Aqua;
                colors["Aquamarine"] = Color.Aquamarine;
                colors["Azure"] = Color.Azure;
                colors["Beige"] = Color.Beige;
                colors["Bisque"] = Color.Bisque;
                colors["Black"] = Color.Black;
                colors["BlanchedAlmond"] = Color.BlanchedAlmond;
                colors["Blue"] = Color.Blue;
                colors["BlueViolet"] = Color.BlueViolet;
                colors["Brown"] = Color.Brown;
                colors["BurlyWood"] = Color.BurlyWood;
                colors["CadetBlue"] = Color.CadetBlue;
                colors["Chartreuse"] = Color.Chartreuse;
                colors["Chocolate"] = Color.Chocolate;
                colors["Coral"] = Color.Coral;
                colors["CornflowerBlue"] = Color.CornflowerBlue;
                colors["Cornsilk"] = Color.Cornsilk;
                colors["Crimson"] = Color.Crimson;
                colors["Cyan"] = Color.Cyan;
                colors["DarkBlue"] = Color.DarkBlue;
                colors["DarkCyan"] = Color.DarkCyan;
                colors["DarkGoldenrod"] = Color.DarkGoldenrod;
                colors["DarkGray"] = Color.DarkGray;
                colors["DarkGreen"] = Color.DarkGreen;
                colors["DarkKhaki"] = Color.DarkKhaki;
                colors["DarkMagenta"] = Color.DarkMagenta;
                colors["DarkOliveGreen"] = Color.DarkOliveGreen;
                colors["DarkOrange"] = Color.DarkOrange;
                colors["DarkOrchid"] = Color.DarkOrchid;
                colors["DarkRed"] = Color.DarkRed;
                colors["DarkSalmon"] = Color.DarkSalmon;
                colors["DarkSeaGreen"] = Color.DarkSeaGreen;
                colors["DarkSlateBlue"] = Color.DarkSlateBlue;
                colors["DarkSlateGray"] = Color.DarkSlateGray;
                colors["DarkTurquoise"] = Color.DarkTurquoise;
                colors["DarkViolet"] = Color.DarkViolet;
                colors["DeepPink"] = Color.DeepPink;
                colors["DeepSkyBlue"] = Color.DeepSkyBlue;
                colors["DimGray"] = Color.DimGray;
                colors["DodgerBlue"] = Color.DodgerBlue;
                colors["Firebrick"] = Color.Firebrick;
                colors["FloralWhite"] = Color.FloralWhite;
                colors["ForestGreen"] = Color.ForestGreen;
                colors["Fuchsia"] = Color.Fuchsia;
                colors["Gainsboro"] = Color.Gainsboro;
                colors["GhostWhite"] = Color.GhostWhite;
                colors["Gold"] = Color.Gold;
                colors["Goldenrod"] = Color.Goldenrod;
                colors["Gray"] = Color.Gray;
                colors["Green"] = Color.Green;
                colors["GreenYellow"] = Color.GreenYellow;
                colors["Honeydew"] = Color.Honeydew;
                colors["HotPink"] = Color.HotPink;
                colors["IndianRed"] = Color.IndianRed;
                colors["Indigo"] = Color.Indigo;
                colors["Ivory"] = Color.Ivory;
                colors["Khaki"] = Color.Khaki;
                colors["Lavender"] = Color.Lavender;
                colors["LavenderBlush"] = Color.LavenderBlush;
                colors["LawnGreen"] = Color.LawnGreen;
                colors["LemonChiffon"] = Color.LemonChiffon;
                colors["LightBlue"] = Color.LightBlue;
                colors["LightCoral"] = Color.LightCoral;
                colors["LightCyan"] = Color.LightCyan;
                colors["LightGoldenrodYellow"] = Color.LightGoldenrodYellow;
                colors["LightGray"] = Color.LightGray;
                colors["LightGreen"] = Color.LightGreen;
                colors["LightPink"] = Color.LightPink;
                colors["LightSalmon"] = Color.LightSalmon;
                colors["LightSeaGreen"] = Color.LightSeaGreen;
                colors["LightSkyBlue"] = Color.LightSkyBlue;
                colors["LightSlateGray"] = Color.LightSlateGray;
                colors["LightSteelBlue"] = Color.LightSteelBlue;
                colors["LightYellow"] = Color.LightYellow;
                colors["Lime"] = Color.Lime;
                colors["LimeGreen"] = Color.LimeGreen;
                colors["Linen"] = Color.Linen;
                colors["Magenta"] = Color.Magenta;
                colors["Maroon"] = Color.Maroon;
                colors["MediumAquamarine"] = Color.MediumAquamarine;
                colors["MediumBlue"] = Color.MediumBlue;
                colors["MediumOrchid"] = Color.MediumOrchid;
                colors["MediumPurple"] = Color.MediumPurple;
                colors["MediumSeaGreen"] = Color.MediumSeaGreen;
                colors["MediumSlateBlue"] = Color.MediumSlateBlue;
                colors["MediumSpringGreen"] = Color.MediumSpringGreen;
                colors["MediumTurquoise"] = Color.MediumTurquoise;
                colors["MediumVioletRed"] = Color.MediumVioletRed;
                colors["MidnightBlue"] = Color.MidnightBlue;
                colors["MintCream"] = Color.MintCream;
                colors["MistyRose"] = Color.MistyRose;
                colors["Moccasin"] = Color.Moccasin;
                colors["NavajoWhite"] = Color.NavajoWhite;
                colors["Navy"] = Color.Navy;
                colors["OldLace"] = Color.OldLace;
                colors["Olive"] = Color.Olive;
                colors["OliveDrab"] = Color.OliveDrab;
                colors["Orange"] = Color.Orange;
                colors["OrangeRed"] = Color.OrangeRed;
                colors["Orchid"] = Color.Orchid;
                colors["PaleGoldenrod"] = Color.PaleGoldenrod;
                colors["PaleGreen"] = Color.PaleGreen;
                colors["PaleTurquoise"] = Color.PaleTurquoise;
                colors["PaleVioletRed"] = Color.PaleVioletRed;
                colors["PapayaWhip"] = Color.PapayaWhip;
                colors["PeachPuff"] = Color.PeachPuff;
                colors["Peru"] = Color.Peru;
                colors["Pink"] = Color.Pink;
                colors["Plum"] = Color.Plum;
                colors["PowderBlue"] = Color.PowderBlue;
                colors["Purple"] = Color.Purple;
                colors["Red"] = Color.Red;
                colors["RosyBrown"] = Color.RosyBrown;
                colors["RoyalBlue"] = Color.RoyalBlue;
                colors["SaddleBrown"] = Color.SaddleBrown;
                colors["Salmon"] = Color.Salmon;
                colors["SandyBrown"] = Color.SandyBrown;
                colors["SeaGreen"] = Color.SeaGreen;
                colors["SeaShell"] = Color.SeaShell;
                colors["Sienna"] = Color.Sienna;
                colors["Silver"] = Color.Silver;
                colors["SkyBlue"] = Color.SkyBlue;
                colors["SlateBlue"] = Color.SlateBlue;
                colors["SlateGray"] = Color.SlateGray;
                colors["Snow"] = Color.Snow;
                colors["SpringGreen"] = Color.SpringGreen;
                colors["SteelBlue"] = Color.SteelBlue;
                colors["Tan"] = Color.Tan;
                colors["Teal"] = Color.Teal;
                colors["Thistle"] = Color.Thistle;
                colors["Tomato"] = Color.Tomato;
                colors["Transparent"] = Color.Transparent;
                colors["Turquoise"] = Color.Turquoise;
                colors["Violet"] = Color.Violet;
                colors["Wheat"] = Color.Wheat;
                colors["White"] = Color.White;
                colors["WhiteSmoke"] = Color.WhiteSmoke;
                colors["Yellow"] = Color.Yellow;
                colors["YellowGreen"] = Color.YellowGreen;

                colorNames = colors;
            }

            if (input[0] != '#' && colorNames.TryGetValue(input, out color))
                return true;

            if (input[0] == '#')
                input = input.Substring(1);

            byte[] bytes;

            if (!Helper.TryParseHex(input, out bytes))
                return false;

            if (bytes.Length == 3)
            {

                color = Color.FromArgb(bytes[0], bytes[1], bytes[2]);
                return true;
            }
            else if (bytes.Length == 4)
            {

                color = Color.FromArgb(bytes[0], bytes[1], bytes[2], bytes[3]);
                return true;
            }
            else
                return false;
        }

#endif // WINFULL
    }
}
