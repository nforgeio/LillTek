//-----------------------------------------------------------------------------
// FILE:        ServerManagerHandler.cs
// CONTRIBUTOR: Jeff Lill
// COPYRIGHT:   Copyright (c) 2005-2015 by Jeffrey Lill.  All rights reserved.
// DESCRIPTION: Implements a message based interface that provides for remote
//              access to the current machine's services, file system, and other 
//              resources.

using System;
using System.Collections;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Management;
using System.Reflection;
using System.Runtime.Serialization;
using System.ServiceModel;
using System.ServiceProcess;
using System.Text;
using System.Threading;

using LillTek.Advanced;
using LillTek.Common;
using LillTek.Datacenter;
using LillTek.Datacenter.ServerManagement;
using LillTek.Messaging;
using LillTek.Net.Wcf;
using LillTek.Service;
using LillTek.Windows;

// $todo(jeff.lill): I have to implement some kind of authentication scheme.

// $todo(jeff.lill): 
//
// I'm not sure I should be throwing FaultException<T>s in the
// web service calls.  Perhaps I should be implementing a
// custom FaultContract.

namespace LillTek.Datacenter.Server
{
    /// <summary>
    /// Implements a message based interface that provides for remote
    /// access to the current machine's services, file system, and other 
    /// resources.
    /// </summary>
    /// <remarks>
    /// <para>
    /// This class implements a service manager message handler designed to
    /// implement the server side of the <see cref="ServerManager" /> class.
    /// To add this functionality to an application, simply create an instance
    /// and call <see cref="Start" />, passing the application's message router.
    /// <see cref="Stop" /> should be called when the application terminates.
    /// </para>
    /// <para><b><u>Configuration Settings</u></b></para>
    /// <para>
    /// By default, the service manager handler's settings are prefixed by 
    /// <b>LillTek.Datacenter.ServerManager</b> (a custom prefix can be
    /// passed to <see cref="Start" />).
    /// </para>
    /// <div class="tablediv">
    /// <table class="dtTABLE" cellspacing="0" ID="Table1">
    /// <tr valign="top">
    /// <th width="1">Setting</th>        
    /// <th width="1">Default</th>
    /// <th width="90%">Description</th>
    /// </tr>
    /// <tr valign="top">
    ///     <td>ServerID</td>
    ///     <td>(required)</td>
    ///     <td>
    ///     The globally unique domain name to be used to identify and remotely
    ///     access this server.
    ///     </td>
    /// </tr>
    /// <tr valign="top">
    ///     <td>WcfEndpoint[#]</td>
    ///     <td>(required)</td>
    ///     <td>
    ///     Specifies the WCF endpoint URIs and bindings exposed by the service.
    ///     </td>
    /// </tr>
    /// <tr valign="top">
    ///     <td>WcfEndpoint[#]</td>
    ///     <td>(required)</td>
    ///     <td>
    ///     Specifies the WCF endpoint URIs and bindings exposed by the service.
    ///     </td>
    /// </tr>
    /// <tr valign="top">
    ///     <td>WsdlUri</td>
    ///     <td>(none)</td>
    ///     <td>
    ///     Specifies whether the service's WSDL service description metadata should
    ///     be exposed.  Set the HTTP or HTTPS URI where the WSDL document should
    ///     be located.  Note that to actually retrieve the WSDL from the server,
    ///     you'll need to add the <b>"?wsdl"</b> query string in the browser.
    ///     </td>
    /// </tr>
    /// <tr valign="top">
    ///     <td>PurgeLog</td>
    ///     <td>no</td>
    ///     <td>
    ///     Controls whether local event log entries should be purged after being
    ///     replicated to Sentinel.
    ///     </td>
    /// </tr>    
    /// <tr valign="top">
    ///     <td>BkTaskInterval</td>
    ///     <td>1s</td>
    ///     <td>
    ///     The background task polling interval.
    ///     </td>
    /// </tr>    
    /// <tr valign="top">
    ///     <td>TempFolder</td>
    ///     <td>$(AppPath)\Temp</td>
    ///     <td>
    ///     Specifies the temporary folder where the server manager will maintain
    ///     files in the process of being remotely uploaded or downloaded.
    ///     </td>
    /// </tr>    
    /// <tr valign="top">
    ///     <td>TempPurgeInterval</td>
    ///     <td>5m</td>
    ///     <td>
    ///     The interval at which application's temporary folder will be checked
    ///     for old files to be purged.
    ///     </td>
    /// </tr>    
    /// <tr valign="top">
    ///     <td>MaxTempFileAge</td>
    ///     <td>1d</td>
    ///     <td>
    ///     Maximum age allowed for a file in the application's temporary folder.
    ///     Files in the process of being uploaded or downloaded that are older
    ///     than this are assumed to have ben orphaned and will be deleted.
    ///     </td>
    /// </tr>    
    /// </table>
    /// </div>
    /// <para><b><u>Remote File Implementation Note</u></b></para>
    /// <para>
    /// Remote file uploading and downloading to the server manager is performed
    /// by using <see cref="OpenRemoteFile" /> to open an existing file on the
    /// remote machine for reading or <see cref="CreateRemoteFile" /> to create
    /// a new file on the remote machine to accept written data.
    /// </para>
    /// <para>
    /// Each of these methods accept the fully qualified path to the location
    /// of the file on the remote machine, but the subsequent calls to 
    /// <see cref="RemoteFileRead" /> and <see cref="RemoteFileWrite" />
    /// do not access the file at this path directly.  Instead, <see cref="OpenRemoteFile" />
    /// copies the file to be read to to the temporary folder under the
    /// file name <b>{guid}.read</b> and <see cref="CreateRemoteFile" />
    /// creates two files in the temporary folder, one called <b>{guid}.write</b>
    /// and the other called <b>{guid}.write.path</b>, where <b>{guid}</b> is a
    /// globaly unique ID generated for the open or create file operation.
    /// <see cref="OpenRemoteFile" /> and <see cref="CreateRemoteFile" />
    /// return the <see cref="Guid" /> generated.
    /// </para>
    /// <para>
    /// <see cref="RemoteFileRead" /> accepts the file <see cref="Guid" />
    /// and simply returns a block of data from the <b>{guid}.read</b>
    /// file.  <see cref="RemoteFileWrite" /> works the same, but writes
    /// a block of data to the <b>{guid}.write</b> file and writes the
    /// full path of thje target as a line of text to the <b>{guid}.write.path</b>
    /// file.
    /// </para>
    /// <para>
    /// <see cref="CloseRemoteFile" /> works differently spending on whether
    /// the file was opened for reading or writing.  For read files, this
    /// method simply deletes the temporary file.  For write files, <see cref="CloseRemoteFile" />
    /// opens the <b>{guid}.write.path</b> file and copies the contents of 
    /// <b>{guid}.write</b> there and then deletes both temporary files.
    /// </para>
    /// <para>
    /// <see cref="PurgeRemoteFile" /> deletes all files named <b>{guid}.*</b>
    /// in the temporary folder.
    /// </para>
    /// <para>
    /// The reasoning behind all of this is to make remote file reading and
    /// writing into essentially atomic operations to reduce the risk that
    /// partially uploaded or locked files will impact the server.  Note
    /// that the service periodically scans the temporary folder for files
    /// that appear to have been orphaned and deletes them.
    /// </para>
    /// </remarks>
    [ServiceBehavior(InstanceContextMode = InstanceContextMode.Single, ConcurrencyMode = ConcurrencyMode.Multiple, IncludeExceptionDetailInFaults = true)]
    public class ServerManagerHandler : /* IServerManager, */ IServiceHandler, ILockable
    {
        //---------------------------------------------------------------------
        // Private classes

        /// <summary>
        /// Holds the performance counters maintained by the service.
        /// </summary>
        private struct Perf
        {
            // Performance counter names

            const string Runtime_Name          = "Runtime (min)";
            const string WcfInboundCalls_Name  = "WCF Inbound Calls/sec";
            const string WcfOutboundCalls_Name = "WCF Outbound Calls/sec";

            /// <summary>
            /// Installs the service's performance counters by adding them to the
            /// performance counter set passed.
            /// </summary>
            /// <param name="perfCounters">The application's performance counter set (or <c>null</c>).</param>
            /// <param name="perfPrefix">The string to prefix any performance counter names (or <c>null</c>).</param>
            public static void Install(PerfCounterSet perfCounters, string perfPrefix)
            {
                if (perfCounters == null)
                    return;

                if (perfPrefix == null)
                    perfPrefix = string.Empty;

                perfCounters.Add(new PerfCounter(perfPrefix + Runtime_Name, "Service runtime in minutes", PerformanceCounterType.NumberOfItems32));
                perfCounters.Add(new PerfCounter(perfPrefix + WcfInboundCalls_Name, "Inbound WCF calls received per second", PerformanceCounterType.RateOfCountsPerSecond32));
                perfCounters.Add(new PerfCounter(perfPrefix + WcfOutboundCalls_Name, "Outbound WCF calls received per second", PerformanceCounterType.RateOfCountsPerSecond32));
            }

            //-----------------------------------------------------------------

            public PerfCounter Runtime;             // Service runtime in minutes
            public PerfCounter WcfInboundCalls;     // Inbound WCF calls received per second
            public PerfCounter WcfOutboundCalls;    // Inbound WCF calls received per second

            /// <summary>
            /// Initializes the service's performance counters from the performance
            /// counter set passed.
            /// </summary>
            /// <param name="perfCounters">The application's performance counter set (or <c>null</c>).</param>
            /// <param name="perfPrefix">The string to prefix any performance counter names (or <c>null</c>).</param>
            public Perf(PerfCounterSet perfCounters, string perfPrefix)
            {
                Install(perfCounters, perfPrefix);

                if (perfPrefix == null)
                    perfPrefix = string.Empty;

                if (perfCounters != null)
                {
                    Runtime          = perfCounters[perfPrefix + Runtime_Name];
                    WcfInboundCalls  = perfCounters[perfPrefix + WcfInboundCalls_Name];
                    WcfOutboundCalls = perfCounters[perfPrefix + WcfInboundCalls_Name];
                }
                else
                {
                    Runtime          =
                    WcfInboundCalls  =
                    WcfOutboundCalls = PerfCounter.Stub;
                }
            }
        }

        //---------------------------------------------------------------------
        // Static members

        /// <summary>
        /// Adds the performance counters managed by the class to the performance counter
        /// set passed (if not null).  This will be called during the application installation
        /// process when performance counters are being installed.
        /// </summary>
        /// <param name="perfCounters">The application's performance counter set (or <c>null</c>).</param>
        /// <param name="perfPrefix">The string to prefix any performance counter names (or <c>null</c>).</param>
        public static void InstallPerfCounters(PerfCounterSet perfCounters, string perfPrefix)
        {
            Perf.Install(perfCounters, perfPrefix);
        }

        //---------------------------------------------------------------------
        // Instance members

        /// <summary>
        /// The service's default configuration key prefix.
        /// </summary>
        public const string ConfigPrefix = "LillTek.Datacenter.ServerManager";

        private MsgRouter           router;             // The associated message router (or null if the handler is stopped)
        private object              syncLock;           // Instance used for thread synchronization
        private Perf                perf;               // Performance counters
        private DateTime            startTime;          // Time the service was started (UTC)
        private WcfServiceHost      wcfServiceHost;     // WCF service host
        private TimeSpan            bkTaskInterval;     // Background task interval
        private string              serverID;           // Globally unique server host name
        private PolledTimer         logPollTimer;       // Timer fires when it's time to poll for new event log entries
        private bool                purgeLog;           // True if event log entries should be purged after being replicated to Sentinel
        private GatedTimer          bkTimer;            // Background task timer
        private string              appFolder;          // The application installation folder (without a terminating slash)
        private string              tempFolder;         // Path to the temporary folder (without a terminating slash)
        private PolledTimer         tempPurgeTimer;     // Timer fires when it's time to purge old temporary files
        private TimeSpan            maxTempFileAge;     // Maximum age for a temporary file

        /// <summary>
        /// Constructs a service manager message handler.
        /// </summary>
        public ServerManagerHandler()
        {
            this.router         = null;
            this.bkTimer        = null;
            this.wcfServiceHost = null;
        }

        /// <summary>
        /// Associates the service handler with a message router by registering
        /// the necessary application message handlers.
        /// </summary>
        /// <param name="router">The message router.</param>
        /// <param name="keyPrefix">The configuration key prefix or (null to use <b>LillTek.Datacenter.ServerManager</b>).</param>
        /// <param name="perfCounters">The application's performance counter set (or <c>null</c>).</param>
        /// <param name="perfPrefix">The string to prefix any performance counter names (or <c>null</c>).</param>
        /// <remarks>
        /// <para>
        /// Applications that expose performance counters will pass a non-<c>null</c> <b>perfCounters</b>
        /// instance.  The service handler should add any counters it implements to this set.
        /// If <paramref name="perfPrefix" /> is not <c>null</c> then any counters added should prefix their
        /// names with this parameter.
        /// </para>
        /// </remarks>
        public void Start(MsgRouter router, string keyPrefix, PerfCounterSet perfCounters, string perfPrefix)
        {
            var config = new Config(keyPrefix != null ? keyPrefix : ConfigPrefix);

            // Make sure the syncLock is set early.

            this.syncLock = router.SyncRoot;

            // Verify the router parameter

            if (router == null)
                throw new ArgumentNullException("router", "Router cannot be null.");

            if (this.router != null)
                throw new InvalidOperationException("This handler has already been started.");

            // Load the application configuration

            bkTaskInterval = config.Get("BkTaskInterval", TimeSpan.FromSeconds(1));
            logPollTimer   = new PolledTimer(config.Get("LogPollInterval", TimeSpan.FromMinutes(1)));
            purgeLog       = config.Get("PurgeLog", false);
            tempPurgeTimer = new PolledTimer(config.Get("TempPurgeInterval", TimeSpan.FromMinutes(5)));
            maxTempFileAge = config.Get("MaxTempFileAge", TimeSpan.FromDays(1));
            appFolder      = Helper.StripTrailingSlash(Helper.GetAssemblyFolder(Helper.GetEntryAssembly()));
            tempFolder     = Helper.StripTrailingSlash(config.Get("TempFolder", appFolder + "\\Temp"));

            Helper.CreateFolderTree(tempFolder);

            serverID = config.Get("ServerID");
            if (!Helper.IsValidDomainName(serverID))
                throw new ConfigException(config, "ServerID");

            EnvironmentVars.ServerID = serverID;    // Publish the ServerID to all LillTek Platform applications

            // Initialize the performance counters

            startTime = DateTime.UtcNow;
            perf      = new Perf(perfCounters, perfPrefix);

            // Crank up the background task timer.

            bkTimer = new GatedTimer(new TimerCallback(OnBkTimer), null, bkTaskInterval);

            try
            {
                // Initialize the router

                this.router = router;

                // Initialize a WCF service host.

                WcfEndpoint[]   endpoints;
                string          wsdlUriString;
                Uri             wsdlUri;

                endpoints = WcfEndpoint.LoadConfigArray(config, "WcfEndpoint");
                if (endpoints.Length == 0)
                    throw new ConfigException(config, "WcfEndpoint", "No WCF service endpoints are configured.");

                wcfServiceHost = new WcfServiceHost(this);

                wcfServiceHost.AddBehaviors(config.Get("ServiceBehaviors"));
                wcfServiceHost.AddServiceEndpoint(typeof(IServerManager), endpoints);

                wsdlUriString = config.Get("WsdlUri");
                if (wsdlUriString != null)
                {
                    try
                    {
                        wsdlUri = new Uri(wsdlUriString);
                        if (wsdlUri.Scheme == Uri.UriSchemeHttp)
                            wcfServiceHost.ExposeServiceDescription(wsdlUriString, null);
                        else if (wsdlUri.Scheme == Uri.UriSchemeHttps)
                            wcfServiceHost.ExposeServiceDescription(null, wsdlUriString);
                        else
                            throw new Exception("URI must have one of [http://] or [https://] scheme.");
                    }
                    catch (Exception e)
                    {
                        SysLog.LogWarning("Invalid [WsdlUri] setting: " + e.Message);
                    }
                }

                wcfServiceHost.Start();
            }
            catch
            {
                if (wcfServiceHost != null)
                {
                    wcfServiceHost.Stop();
                    wcfServiceHost = null;
                }

                if (bkTimer != null)
                {
                    bkTimer.Dispose();
                    bkTimer = null;
                }

                router = null;
                throw;
            }
        }

        /// <summary>
        /// Initiates a graceful shut down of the service handler by ignoring
        /// new client requests.
        /// </summary>
        public void Shutdown()
        {
            Stop();
        }

        /// <summary>
        /// Immediately terminates the processing of all client messages.
        /// </summary>
        public void Stop()
        {
            if (router == null)
                return;

            using (TimedLock.Lock(syncLock))
            {
                if (wcfServiceHost != null)
                {
                    wcfServiceHost.Stop();
                    wcfServiceHost = null;
                }

                if (bkTimer != null)
                {
                    bkTimer.Dispose();
                    bkTimer = null;
                }

                router = null;
            }
        }

        /// <summary>
        /// Returns the current number of client requests currently being processed.
        /// </summary>
        public int PendingCount
        {
            get { return 0; }
        }

        /// <summary>
        /// Implements background task processing.
        /// </summary>
        /// <param name="state">Not used.</param>
        private void OnBkTimer(object state)
        {
            perf.Runtime.RawValue = (int)(DateTime.UtcNow - startTime).TotalMinutes;

            if (tempPurgeTimer.HasFired)
            {
                PurgeTempFolder();
                tempPurgeTimer.Reset();
            }
        }

        /// <summary>
        /// Purges old temporary files.
        /// </summary>
        private void PurgeTempFolder()
        {
            DateTime        minCreateTime = DateTime.UtcNow - maxTempFileAge;
            StringBuilder   sb            = new StringBuilder();
            int             cDelete       = 0;

            foreach (string file in Directory.GetFiles(tempFolder, "*.*", SearchOption.TopDirectoryOnly))
            {
                var info = new FileInfo(file);

                if (info.CreationTimeUtc <= minCreateTime)
                {
                    cDelete++;
                    sb.AppendFormat("{0}\r\n", info.FullName);
                    info.Delete();
                }
            }

            if (cDelete > 0)
                SysLog.LogWarning("[{0}] files purged from the temporary folder:\r\n\r\n{1}", sb.ToString());
        }

        //---------------------------------------------------------------------
        // IServerManager Implementation

        /// <summary>
        /// Verifies that the server manager instance is present, running, 
        /// and any required security credentials are valid.
        /// </summary>
        public void Ping()
        {
        }

        /// <summary>
        /// Lists the disk drives available on the remote machine.
        /// </summary>
        /// <returns>Array of drive information.</returns>
        public RemoteDrive[] ListDrives()
        {
            try
            {
                RemoteDrive[]               drives;
                ManagementObjectCollection  objects;
                int                         i;

                objects = new ManagementObjectSearcher("select * from Win32_LogicalDisk").Get();
                drives  = new RemoteDrive[objects.Count];

                i = 0;
                foreach (ManagementObject o in objects)
                {
                    RemoteDriveType type;

                    switch (Helper.Normalize(o["Description"]).ToLowerInvariant())
                    {
                        case "cd-rom disc":

                            type = RemoteDriveType.CDROM;
                            break;

                        case "local fixed disk":

                            type = RemoteDriveType.LocalFixed;
                            break;

                        case "removable disk":

                            type = RemoteDriveType.Removable;
                            break;

                        default:

                            type = RemoteDriveType.Unknown;
                            break;
                    }

                    drives[i++] = new RemoteDrive(Helper.Normalize(o["Name"]), type, long.Parse((string)o["Size"]), long.Parse((string)o["FreeSpace"]));
                }

                return drives;
            }
            catch (Exception e)
            {

                throw new FaultException<Exception>(e);
            }
        }

        /// <summary>
        /// Returns the fully qualified path to a special folder on the remote machine.
        /// </summary>
        /// <param name="folder">Identifies the requested folder.</param>
        public string GetFolderPath(RemoteSpecialFolder folder)
        {
            string path;

            try
            {
                switch (folder)
                {
                    case RemoteSpecialFolder.Temporary:

                        path = Path.GetTempPath();
                        break;

                    case RemoteSpecialFolder.System:

                        path = Environment.GetFolderPath(Environment.SpecialFolder.System);
                        break;

                    case RemoteSpecialFolder.ProgramFiles:

                        path = Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles);
                        break;

                    case RemoteSpecialFolder.ServerManager:

                        path = appFolder;
                        break;

                    default:

                        throw new Exception("Unknown folder idenifier.");
                }

                return path;
            }
            catch (Exception e)
            {
                throw new FaultException<Exception>(e);
            }
        }

        /// <summary>
        /// Lists the files within a directory on the remote machine.
        /// </summary>
        /// <param name="path">Fully qualified path to the directory on the remote machine.</param>
        /// <param name="pattern">The search pattern (or <c>null</c>).</param>
        /// <param name="searchOption">Indicates whether files in child folders should also be listed.</param>
        /// <returns>Array of file information records.</returns>
        public RemoteFile[] ListFiles(string path, string pattern, SearchOption searchOption)
        {
            RemoteFile[]    remoteFiles;
            string[]        files;

            try
            {
                files       = Directory.GetFiles(path, pattern != null ? pattern : "*.*", searchOption);
                remoteFiles = new RemoteFile[files.Length];

                for (int i = 0; i < files.Length; i++)
                    remoteFiles[i] = new RemoteFile(new FileInfo(files[i]));

                return remoteFiles;
            }
            catch (Exception e)
            {
                throw new FaultException<Exception>(e);
            }
        }

        /// <summary>
        /// Lists the folders within a directory on the remote machine.
        /// </summary>
        /// <param name="path">Fully qualified path to the directory on the remote machine.</param>
        /// <returns>Array of directory information records.</returns>
        public RemoteFile[] ListFolders(string path)
        {
            RemoteFile[]    remoteFolders;
            string[]        folders;

            try
            {
                folders       = Directory.GetDirectories(path);
                remoteFolders = new RemoteFile[folders.Length];

                for (int i = 0; i < folders.Length; i++)
                    remoteFolders[i] = new RemoteFile(new FileInfo(folders[i]));

                return remoteFolders;
            }
            catch (Exception e)
            {
                throw new FaultException<Exception>(e);
            }
        }

        /// <summary>
        /// Determines whether a file exists on the remote machine.
        /// </summary>
        /// <param name="path">The fully qualified path to the file.</param>
        /// <returns><c>true</c> if the file exists.</returns>
        public bool FileExists(string path)
        {
            try
            {
                return File.Exists(path);
            }
            catch (Exception e)
            {
                throw new FaultException<Exception>(e);
            }
        }

        /// <summary>
        /// Determines whether a folder exists on the remote machine.
        /// </summary>
        /// <param name="path">The fully qualified path to the folder.</param>
        /// <returns><c>true</c> if the folder exists.</returns>
        public bool FolderExists(string path)
        {
            try
            {
                return Directory.Exists(path);
            }
            catch (Exception e)
            {
                throw new FaultException<Exception>(e);
            }
        }

        /// <summary>
        /// Deletes a file or directory on the remote machine.
        /// </summary>
        /// <param name="path">The fully qualified name of the file (including optional wildcards).</param>
        /// <remarks>
        /// This method supports wildcards and also implements the
        /// recursive deletion of directories.
        /// </remarks>
        public void DeleteFile(string path)
        {
            try
            {
                Helper.DeleteFile(path, true);
            }
            catch (Exception e)
            {
                throw new FaultException<Exception>(e);
            }
        }

        /// <summary>
        /// Creates a directory on the remote machine.
        /// </summary>
        /// <param name="path">The fully qualfied path of the directory.</param>
        /// <remarks>
        /// <note>
        /// It is not an error to call this method for a directory
        /// that already exists.
        /// </note>
        /// </remarks>
        public void CreateDirectory(string path)
        {
            try
            {
                Helper.CreateFolderTree(path);
            }
            catch (Exception e)
            {
                throw new FaultException<Exception>(e);
            }
        }

        /// <summary>
        /// Deletes a directory on the remote machine, recursively deleting any files
        /// and subdirectories contained within.
        /// </summary>
        /// <param name="path">The fully qualfied name of the directory.</param>
        public void DeleteDirectory(string path)
        {
            try
            {
                Helper.DeleteFile(path, true);
            }
            catch (Exception e)
            {
                throw new FaultException<Exception>(e);
            }
        }

        /// <summary>
        /// Copies files on the remote machine.
        /// </summary>
        /// <param name="source">The fully qualified name of the source file with optional wildcards.</param>
        /// <param name="destination">The fully qualified name of the destination file.</param>
        /// <param name="recursive"><c>true</c> to recursively copy directories.</param>
        /// <remarks>
        /// This method is able to copy a single file, or a set of files that
        /// match a wildcard pattern.  The method can also tree copy directories
        /// from one location to another if <paramref name="recursive" /> is <c>true</c>.
        /// This method implements the same behavior as <see cref="Helper.CopyFile(string,string,bool)" />.
        /// </remarks>
        public void CopyFile(string source, string destination, bool recursive)
        {
            try
            {
                Helper.CopyFile(source, destination, recursive);
            }
            catch (Exception e)
            {
                throw new FaultException<Exception>(e);
            }
        }

        /// <summary>
        /// Opens an existing file on the remote machine for reading.
        /// </summary>
        /// <param name="path">The fully qualified remote file path.</param>
        /// <returns>The file ID to be used for subsequent read operations.</returns>
        /// <remarks>
        /// This method actually copies the remote file to a temporary location
        /// and any subsequent <see cref="RemoteFileRead" /> operations will be
        /// performed from the copy.  The temporary file will be deleted when
        /// <see cref="CloseRemoteFile" /> or if the file has not been accessed
        /// for a long period of time.
        /// </remarks>
        public Guid OpenRemoteFile(string path)
        {
            try
            {
                Guid fileID = Guid.NewGuid();

                File.Copy(path, string.Format(@"{0}\{1}.read", tempFolder, fileID), true);
                return fileID;
            }
            catch (Exception e)
            {
                throw new FaultException<Exception>(e);
            }
        }

        /// <summary>
        /// Creates a file on the remote machine for writing.
        /// </summary>
        /// <param name="path">The fully qualified remote file path.</param>
        /// <returns>The file ID to be used for subsequent write operations.</returns>
        /// <remarks>
        /// This method actually creates a temporary file on the remote machine,
        /// where subsequent calls to <see cref="RemoteFileWrite" /> will write their
        /// data.  The temporary file will be copied to the specified path
        /// when <see cref="CloseRemoteFile" /> is called.
        /// </remarks>
        public Guid CreateRemoteFile(string path)
        {
            try
            {
                Guid fileID = Guid.NewGuid();

                using (var fs = new FileStream(string.Format(@"{0}\{1}.write", tempFolder, fileID), FileMode.Create, FileAccess.Write))
                {
                }

                using (var writer = new StreamWriter((string.Format(@"{0}\{1}.write.path", tempFolder, fileID, false, Encoding.UTF8))))
                {
                    writer.WriteLine(path);
                }

                return fileID;
            }
            catch (Exception e)
            {
                throw new FaultException<Exception>(e);
            }
        }

        /// <summary>
        /// Reads a block of data from a remote file.
        /// </summary>
        /// <param name="fileID">The <see cref="Guid" /> returned by <see cref="OpenRemoteFile" />.</param>
        /// <param name="position">The file position of the first byte to be read.</param>
        /// <param name="count">The number of bytes to read.</param>
        /// <returns>The data actually read or <c>null</c> if the end of the file has been reached.</returns>
        public byte[] RemoteFileRead(Guid fileID, long position, int count)
        {
            try
            {
                using (var fs = new FileStream(string.Format(@"{0}\{1}.read", tempFolder, fileID), FileMode.Open, FileAccess.Read))
                {

                    if (position >= fs.Length)
                        return null;    // EOF

                    byte[]      block;
                    long        cbRemain;
                    int         cb;

                    if (count <= 0)
                        return new byte[0];

                    cbRemain = fs.Length - fs.Position;
                    if (cbRemain < count)
                        count = (int)cbRemain;

                    if (count == 0)
                        return null;

                    block = new byte[count];
                    cb = fs.Read(block, 0, count);

                    if (cb == count)
                        return block;
                    else
                        return Helper.Extract(block, cb);
                }
            }
            catch (Exception e)
            {
                throw new FaultException<Exception>(e);
            }
        }

        /// <summary>
        /// Writes a block of data to a remote file.
        /// </summary>
        /// <param name="fileID">The <see cref="Guid" /> returned by <see cref="CreateRemoteFile" />.</param>
        /// <param name="position">The file position of the first byte to be written.</param>
        /// <param name="data">The data to be written.</param>
        public void RemoteFileWrite(Guid fileID, long position, byte[] data)
        {
            try
            {
                using (var fs = new FileStream(string.Format(@"{0}\{1}.write", tempFolder, fileID), FileMode.Open, FileAccess.Write))
                {
                    if (position > fs.Length)
                        throw new Exception("Write past end of file.");

                    fs.Position = position;
                    fs.Write(data, 0, data.Length);
                }
            }
            catch (Exception e)
            {
                throw new FaultException<Exception>(e);
            }
        }

        /// <summary>
        /// Closes a remote file.
        /// </summary>
        /// <param name="fileID">The <see cref="Guid" /> returned by <see cref="OpenRemoteFile" /> or <see cref="CreateRemoteFile" />.</param>
        /// <remarks>
        /// Files created on the remote machine via <see cref="CreateRemoteFile" /> will be copied
        /// to the target path by this method.  Note that any missing parent directories on the
        /// remote machine will be created as necessary before copying the file.
        /// </remarks>
        public void CloseRemoteFile(Guid fileID)
        {
            string      readFile  = string.Format(@"{0}\{1}.read", tempFolder, fileID);
            string      writeFile = string.Format(@"{0}\{1}.write", tempFolder, fileID);
            string      pathFile  = writeFile + ".path";
            string      targetPath;

            try
            {
                if (!File.Exists(readFile) && !File.Exists(writeFile))
                    throw new Exception("File does not exist.");

                if (File.Exists(writeFile))
                {
                    using (StreamReader reader = new StreamReader(pathFile, Encoding.UTF8))
                        targetPath = reader.ReadLine();

                    File.Copy(writeFile, targetPath, true);
                }
            }
            catch (Exception e)
            {
                throw new FaultException<Exception>(e);
            }
            finally
            {
                Helper.DeleteFile(string.Format(@"{0}\{1}.*", tempFolder, fileID));
            }
        }

        /// <summary>
        /// Closes and deletes a remote file being written.
        /// </summary>
        /// <param name="fileID">The <see cref="Guid" /> returned by <see cref="CreateRemoteFile" />.</param>
        /// <remarks>
        /// Use this method to abort file uploading.  This method closes the temporary remote file and deletes
        /// it without copying it to its file destination.
        /// </remarks>
        public void PurgeRemoteFile(Guid fileID)
        {
            try
            {
                Helper.DeleteFile(string.Format(@"{0}\{1}.*", tempFolder, fileID));
            }
            catch (Exception e)
            {
                throw new FaultException<Exception>(e);
            }
        }

        /// <summary>
        /// Executes a program on the remote machine.
        /// </summary>
        /// <param name="path">The fully qualified path of the program on the remote machine.</param>
        /// <param name="args">The arguments to be passed.</param>
        /// <param name="timeout">
        /// The maximum time to wait for the process to complete or <c>null</c>
        /// to wait indefinitely.
        /// </param>
        /// <returns>The <see cref="ExecuteResult"/>.</returns>
        public ExecuteResult Execute(string path, string args, TimeSpan? timeout = null)
        {
            try
            {
                return Helper.ExecuteCaptureStreams(path, args, timeout);
            }
            catch (Exception e)
            {
                throw new FaultException<Exception>(e);
            }
        }

        /// <summary>
        /// Restarts the remote machine.
        /// </summary>
        /// <remarks>
        /// <note>
        /// This method will notify the server to restart, then close
        /// the session and then return immediately, without waiting for any
        /// kind of acknowledgment from the server.
        /// </note>
        /// </remarks>
        public void Reboot()
        {
            try
            {
                Helper.RestartComputer();
            }
            catch (Exception e)
            {
                throw new FaultException<Exception>(e);
            }
        }

        /// <summary>
        /// Shuts down the remote machine.
        /// </summary>
        /// <remarks>
        /// <note>
        /// This method will notify the server to shutdown, then close
        /// the session and then return immediately, without waiting for any
        /// kind of acknowledgment from the server.
        /// </note>
        /// </remarks>
        public void PowerDown()
        {
            try
            {
                Helper.PowerDownComputer();
            }
            catch (Exception e)
            {
                throw new FaultException<Exception>(e);
            }
        }

        /// <summary>
        /// Returns information about the services running on the remote machine.
        /// </summary>
        /// <returns>A <see cref="RemoteServiceInfo" /> array describing the installed services.</returns>
        public RemoteServiceInfo[] ListServices()
        {
            try
            {
                ServiceController[]     services;
                RemoteServiceInfo[]     rsi;

                services = ServiceController.GetServices();
                rsi      = new RemoteServiceInfo[services.Length];

                for (int i = 0; i < services.Length; i++)
                {
                    ServiceState            state;
                    RemoteServiceStartMode  startMode;

                    switch (services[i].Status)
                    {
                        case ServiceControllerStatus.Running:
                        case ServiceControllerStatus.ContinuePending:
                        case ServiceControllerStatus.Paused:
                        case ServiceControllerStatus.PausePending:

                            state = ServiceState.Running;
                            break;

                        case ServiceControllerStatus.StartPending:

                            state = ServiceState.Starting;
                            break;

                        case ServiceControllerStatus.Stopped:

                            state = ServiceState.Stopped;
                            break;

                        case ServiceControllerStatus.StopPending:

                            state = ServiceState.Stopping;
                            break;

                        default:

                            state = ServiceState.Unknown;
                            break;
                    }

                    switch (ServiceControl.GetStartMode(services[i].ServiceName))
                    {
                        case ServiceStartMode.Automatic:

                            startMode = RemoteServiceStartMode.Automatic;
                            break;

                        case ServiceStartMode.Disabled:

                            startMode = RemoteServiceStartMode.Disabled;
                            break;

                        case ServiceStartMode.Manual:

                            startMode = RemoteServiceStartMode.Manual;
                            break;

                        default:

                            startMode = RemoteServiceStartMode.Unknown;
                            break;
                    }

                    rsi[i] = new RemoteServiceInfo(services[i].ServiceName, state, startMode);
                    services[i].Dispose();
                }

                return rsi;
            }
            catch (Exception e)
            {
                throw new FaultException<Exception>(e);
            }
        }

        /// <summary>
        /// Returns information about a particular service on the remote machine. 
        /// </summary>
        /// <param name="serviceName">Name of the service.</param>
        /// <returns>
        /// A <see cref="RemoteServiceInfo" /> instance describing the service if 
        /// it exists, <c>null</c> if the requested service is not installed.
        /// </returns>
        public RemoteServiceInfo GetServiceStatus(string serviceName)
        {
            try
            {
                ServiceController[] services;
                RemoteServiceInfo[] rsi;

                services = ServiceController.GetServices();
                rsi      = new RemoteServiceInfo[services.Length];

                for (int i = 0; i < services.Length; i++)
                {
                    ServiceState            state;
                    RemoteServiceStartMode  startMode;

                    switch (services[i].Status)
                    {

                        case ServiceControllerStatus.Running:
                        case ServiceControllerStatus.ContinuePending:
                        case ServiceControllerStatus.Paused:
                        case ServiceControllerStatus.PausePending:

                            state = ServiceState.Running;
                            break;

                        case ServiceControllerStatus.StartPending:

                            state = ServiceState.Starting;
                            break;

                        case ServiceControllerStatus.Stopped:

                            state = ServiceState.Stopped;
                            break;

                        case ServiceControllerStatus.StopPending:

                            state = ServiceState.Stopping;
                            break;

                        default:

                            state = ServiceState.Unknown;
                            break;
                    }

                    switch (ServiceControl.GetStartMode(services[i].ServiceName))
                    {
                        case ServiceStartMode.Automatic:

                            startMode = RemoteServiceStartMode.Automatic;
                            break;

                        case ServiceStartMode.Disabled:

                            startMode = RemoteServiceStartMode.Disabled;
                            break;

                        case ServiceStartMode.Manual:

                            startMode = RemoteServiceStartMode.Manual;
                            break;

                        default:

                            startMode = RemoteServiceStartMode.Unknown;
                            break;
                    }

                    rsi[i] = new RemoteServiceInfo(services[i].ServiceName, state, startMode);
                    services[i].Dispose();
                }

                foreach (RemoteServiceInfo serviceInfo in rsi)
                    if (String.Compare(serviceInfo.ServiceName, serviceName, true) == 0)
                        return serviceInfo;

                return null;
            }
            catch (Exception e)
            {
                throw new FaultException<Exception>(e);
            }
        }

        /// <summary>
        /// Starts a named service if it's not already running on a remote machine.
        /// </summary>
        /// <param name="serviceName">Name of the service to start.</param>
        public void StartService(string serviceName)
        {
            ServiceController sc = null;

            try
            {
                sc = new ServiceController(serviceName);
                if (sc.Status == ServiceControllerStatus.Stopped)
                    sc.Start();
                else if (sc.Status == ServiceControllerStatus.Paused)
                    sc.Continue();
            }
            catch (Exception e)
            {
                throw new FaultException<Exception>(e);
            }
            finally
            {
                if (sc != null)
                    sc.Dispose();
            }
        }

        /// <summary>
        /// Stops a service if it is running on a remote machine.
        /// </summary>
        /// <param name="serviceName">Name of the service to be stopped.</param>
        public void StopService(string serviceName)
        {
            ServiceController sc = null;

            try
            {
                sc = new ServiceController(serviceName);
                if (sc.Status == ServiceControllerStatus.Running || sc.Status == ServiceControllerStatus.Paused)
                    sc.Stop();
            }
            catch (Exception e)
            {
                throw new FaultException<Exception>(e);
            }
            finally
            {
                if (sc != null)
                    sc.Dispose();
            }
        }

        /// <summary>
        /// Sets a service's start mode on a remote machine.
        /// </summary>
        /// <param name="serviceName">The service name.</param>
        /// <param name="startMode">The start mode to set.</param>
        public void SetServiceStartMode(string serviceName, RemoteServiceStartMode startMode)
        {
            try
            {
                ServiceStartMode mode;

                switch (startMode)
                {
                    case RemoteServiceStartMode.Automatic:

                        mode = ServiceStartMode.Automatic;
                        break;

                    case RemoteServiceStartMode.Disabled:

                        mode = ServiceStartMode.Disabled;
                        break;

                    case RemoteServiceStartMode.Manual:

                        mode = ServiceStartMode.Manual;
                        break;

                    default:

                        throw new InvalidOperationException("Invalid start mode.");
                }

                ServiceControl.SetStartMode(serviceName, mode);
            }
            catch (Exception e)
            {
                throw new FaultException<Exception>(e);
            }
        }

        /// <summary>
        /// Returns information about the processes running on a remote machine.
        /// </summary>
        /// <param name="computeCpuUtilization">Pass <c>true</c> to calculate CPU utilization for the processes.</param>
        /// <returns>The array of <see cref="RemoteProcess" /> values.</returns>
        public RemoteProcess[] ListProcesses(bool computeCpuUtilization)
        {
            Process[]           processes = null;
            RemoteProcess[]     rpi;
            DateTime            getTime;
            TimeSpan            elapsed;

            try
            {
                processes = Process.GetProcesses();
                getTime   = SysTime.Now;
                rpi       = new RemoteProcess[processes.Length];

                for (int i = 0; i < processes.Length; i++)
                    rpi[i] = new RemoteProcess(processes[i]);

                if (computeCpuUtilization)
                {
                    Thread.Sleep(1000);
                    elapsed = SysTime.Now - getTime;

                    for (int i = 0; i < processes.Length; i++)
                        rpi[i].CalcCpuUtilization(elapsed);
                }

                return rpi;
            }
            catch (Exception e)
            {
                throw new FaultException<Exception>(e);
            }
            finally
            {
                if (processes != null)
                    for (int i = 0; i < processes.Length; i++)
                        processes[i].Close();
            }
        }

        /// <summary>
        /// Kills a process running on a remote machine.
        /// </summary>
        /// <param name="processID">The ID of the process to be killed.</param>
        public void KillProcess(int processID)
        {
            Process process = null;

            try
            {
                process = Process.GetProcessById(processID);
                process.Kill();
            }
            catch (Exception e)
            {
                throw new FaultException<Exception>(e);
            }
            finally
            {
                if (process != null)
                    process.Close();
            }
        }

        /// <summary>
        /// Sets the time on the remote machine.
        /// </summary>
        /// <param name="time">The time to set (UTC).</param>
        public void SetTime(DateTime time)
        {
            try
            {
                WinApi.SetSystemTime(new SYSTEMTIME(time));
            }
            catch (Exception e)
            {
                throw new FaultException<Exception>(e);
            }
        }

        /// <summary>
        /// Performs one or more WMI queries on the remote machine.
        /// </summary>
        /// <param name="queries">The set of queries to be performed.</param>
        /// <returns>The query results.</returns>
        public WmiResultSet WmiQuery(params WmiQuery[] queries)
        {
            var wmiResults = new WmiResultSet();

            try
            {
                foreach (WmiQuery wmiQuery in queries)
                    wmiResults.Add(new WmiResult(wmiQuery.Name, new ManagementObjectSearcher(wmiQuery.Query)));

                return wmiResults;
            }
            catch (Exception e)
            {
                throw new FaultException<Exception>(e);
            }
        }

        //---------------------------------------------------------------------
        // ILockable implementation

        private object lockKey = TimedLock.AllocLockKey();

        /// <summary>
        /// Used by <see cref="TimedLock" /> to provide better deadlock
        /// diagnostic information.
        /// </summary>
        /// <returns>The process unique lock key for this instance.</returns>
        public object GetLockKey()
        {
            return lockKey;
        }
    }
}
