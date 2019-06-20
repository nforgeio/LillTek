//-----------------------------------------------------------------------------
// FILE:        ServiceControl.cs
// CONTRIBUTOR: Jeff Lill
// COPYRIGHT:   Copyright (c) 2005-2015 by Jeffrey Lill.  All rights reserved.
// DESCRIPTION: Provides a cross platform mechanism providing remote access
//              to services, the file system, and other system resources.

using System;
using System.Collections;
using System.Diagnostics;
using System.ServiceProcess;

using LillTek.Common;
using LillTek.LowLevel;

namespace LillTek.Service
{
    /// <summary>
    /// Provides a cross platform mechanism providing remote access
    /// to services, the file system, and other system resources.
    /// </summary>
    /// <remarks>
    /// <para>
    /// On Win32, LillTek services may be instantiated as either a
    /// native Windows service, a Windows Form application, or a
    /// console application.  On WinCE, services are always instantiated
    /// as console applications.
    /// </para>
    /// <para>
    /// This class provides a standard way of controlling LillTek services
    /// using a combination of native Windows APIs and a custom shared
    /// memory interface.
    /// </para>
    /// <para>
    /// Here's a brief summary of how the control of LillTek service
    /// control works:
    /// </para>
    /// <para>
    /// Each service that dervies from the ServiceBase class exposes a
    /// SharedMemInbox named: Service-[service name]
    /// where [service name] is the name returned by the IService.Name
    /// property.  ServiceControl instances use these inboxes to send
    /// commands to the services.  Services reply to these commands by
    /// sending messages to the ServiceControl instances through a
    /// shared memory inbox named ServiceControl-[guid],
    /// where [guid] is a globally unique ID generated when the 
    /// ServiceControl instance was instantiated.
    /// </para>
    /// </remarks>
    /// <threadsafety instance="false" />
    public class ServiceControl : ILockable
    {
        /// <summary>
        /// Prefix for service instance shared memory inboxes.
        /// </summary>
        internal const string ServiceMemPrefix = @"Svc:";

        /// <summary>
        /// Prefix for service control instance shared memory inboxes.
        /// </summary>
        internal const string ControlMemPrefix = @"SvcCtl:";

        /// <summary>
        /// Maximum size of a message passed between ServiceControl and
        /// ServiceBase instances.
        /// </summary>
        internal const int MaxMsgSize = 512;

        /// <summary>
        /// Returns the maximum amount of time a ServiceControl or
        /// ServiceBase message should wait while attempting to 
        /// send a message via a SharedMemOutbox.
        /// </summary>
        internal static TimeSpan MaxWaitTime
        {
            get { return TimeSpan.FromSeconds(2); }
        }

        /// <summary>
        /// Returns the startup mode for a service.
        /// </summary>
        /// <param name="serviceName">The service name.</param>
        /// <returns>The start mode.</returns>
        public static ServiceStartMode GetStartMode(string serviceName)
        {
            RegKey      key;
            int         raw;

            key = RegKey.Open(@"HKEY_LOCAL_MACHINE\SYSTEM\CurrentControlSet\Services\" + serviceName);

            if (key == null)
            {
                throw new InvalidOperationException(string.Format("Service [{0}] does not exist.", serviceName));
            }

            key.Close();

            raw = RegKey.GetValue(@"HKEY_LOCAL_MACHINE\SYSTEM\CurrentControlSet\Services\" + serviceName + ":Start", 0);

            switch (raw)
            {
                case 2:     return ServiceStartMode.Automatic;
                case 3:     return ServiceStartMode.Manual;

                default:
                case 4:     return ServiceStartMode.Disabled;
            }
        }

        /// <summary>
        /// Sets the startup mode for a service.
        /// </summary>
        /// <param name="serviceName">The service name.</param>
        /// <param name="mode">Thye new start mode.</param>
        public static void SetStartMode(string serviceName, ServiceStartMode mode)
        {
            RegKey      key;
            int         raw = 0;

            key = RegKey.Open(@"HKEY_LOCAL_MACHINE\SYSTEM\CurrentControlSet\Services\" + serviceName);
            
            if (key == null)
            {
                throw new InvalidOperationException(string.Format("Service [{0}] does not exist.", serviceName));
            }

            key.Close();

            switch (mode)
            {
                case ServiceStartMode.Automatic:    raw = 2; break;
                case ServiceStartMode.Manual:       raw = 3; break;
                case ServiceStartMode.Disabled:     raw = 4; break;
            }

            if (raw != 0)
            {
                RegKey.SetValue(@"HKEY_LOCAL_MACHINE\SYSTEM\CurrentControlSet\Services\" + serviceName + ":Start", raw);
            }
        }

        private Guid                id;             // Globally unique ID for this instance
        private bool                isOpen;         // True if the instance is open.
        private SharedMemInbox      inbox;          // Receives response from services
        private SharedMemOutbox     outbox;         // Delivers messages to services
        private ArrayList           services;       // Array of ServiceInfo instances
        private string              keyPrefix;      // Configuration key prefix (or null)
        private string              setting;        // Configuration setting (or null)

        // These members are used to manage the correlation of queries to services
        // to the response from the service.  The query methods below will generate
        // a unique query ID and assign this value to the query message's RefID property
        // and the refID field below.  The method will send the query to the service
        // and then wait on the onReply event.  The OnReceive() method below will receive
        // the response from the service, and if its RefID matches the refID field then
        // it will assign the reply to the reply field and then signal the event.

        private Guid                    refID;
        private ServiceMsg              reply;
        private GlobalAutoResetEvent    onReply;

        /// <summary>
        /// Information about a service loaded from the configuration settings.
        /// </summary>
        private sealed class ServiceInfo
        {
            /// <summary>
            /// The service name.
            /// </summary>
            public string Name;

            /// <summary>
            /// The fully qualified path to the service's executable.
            /// </summary>
            public string Path;

            /// <summary>
            /// Indicates how the service was started.
            /// </summary>
            public StartAs Mode;

            /// <summary>
            /// <c>true</c> if the service is supposed to be running, ie. 
            /// it was started by the service manager.
            /// </summary>
            public bool Started;

            /// <summary>
            /// Initializes the service information.
            /// </summary>
            /// <param name="name">The service name.</param>
            /// <param name="path">The fully qualified path to the service's executable.</param>
            /// <param name="mode">Indicates how the service was started.</param>
            public ServiceInfo(string name, string path, StartAs mode)
            {
                this.Name    = name;
                this.Path    = path;
                this.Mode    = mode;
                this.Started = false;
            }
        }

        /// <summary>
        /// Default constructor.
        /// </summary>
        /// <remarks>
        /// <note>
        /// Instances created with this constructor will only be able to
        /// start native Windows services.  Use the constructor below to load information
        /// about the location and type of the service executables to be able to
        /// start other types of services.
        /// </note>
        /// </remarks>
        public ServiceControl()
        {
            this.id        = Guid.Empty;
            this.isOpen    = false;
            this.inbox     = null;
            this.outbox    = null;
            this.keyPrefix = null;
            this.setting   = null;
            this.services  = null;
        }

        /// <summary>
        /// This constructor initializes the service control instance by reading
        /// information about the location and type of service executables from
        /// the current application configuration settings. 
        /// </summary>
        /// <param name="keyPrefix">The settings prefix string (or <c>null</c>).</param>
        /// <param name="setting">The configuration string prefix.</param>
        /// <remarks>
        /// <para>
        /// Instances created with this constructor will be able to start non-native
        /// Windows services.  This will be used primarly in situations where
        /// the current platform doesn't support native services (such as Windows CE
        /// and WindowME and earlier).  For this to work, the constructor needs
        /// to know the service name, the location of the service executable in the 
        /// file system as well as how the service is to be launched.
        /// </para>
        /// <para>
        /// The constructor gets this information from the current application
        /// configuration settings.  These settings should include zero or more
        /// settings named using the array syntax:
        /// </para>
        /// <code language="none">
        /// [keyPrefix][setting]"[" # "]"=[name];[executable];[mode]
        /// </code>        
        /// <para>
        /// where [setting] is the parameter passed, # is a count (from 0 to
        /// the number of services-1, [name] is the name of the service, [executable]
        /// is the fully qualified path to the service's executable file, and [mode]
        /// is one of "native", "form", or "console" indicating how the service should
        /// be launched.  [keyPrefix] is the optional configuration setting prefix string.
        /// </para>
        /// <para>
        /// Note that the [name] and [mode] fields are required. [executable] may be 
        /// set to the empty string if [mode]=native.
        /// </para>
        /// <para>
        /// Here's an example of some settings:
        /// </para>
        /// <code language="none">
        /// Services[0]=Foo;c:\program files\foo\foo.exe;console
        /// Services[1]=Bar;c:\program files\bar\bar.exe;native
        /// Services[2]=FooBar;c:\program files\foobar\foobar.exe;form
        /// </code>        
        /// </remarks>
        public ServiceControl(string keyPrefix, string setting)
            : this()
        {
            this.keyPrefix = keyPrefix;
            this.setting   = setting;
        }

        /// <summary>
        /// This finalizer releases any resources in case <see cref="Close" /> wasn't called.
        /// </summary>
        ~ServiceControl()
        {
            Close();
        }

        /// <summary>
        /// Readies the service control instance for use.
        /// </summary>
        public void Open()
        {
            if (isOpen)
                return;

            // Load the configuration settings

            Config      config = new Config(keyPrefix);
            string[]    settings;
            int         p, pEnd;
            string      name, path, s, m;
            StartAs     mode;

            settings = config.GetArray(setting);
            services = new ArrayList(settings.Length);

            for (int i = 0; i < settings.Length; i++)
            {
                s = settings[i];
                try
                {
                    p = 0;

                    pEnd = s.IndexOf(';', p);

                    if (pEnd == -1)
                    {
                        throw new Exception();
                    }

                    name = s.Substring(0, pEnd - p).Trim();
                    p    = pEnd + 1;

                    if (string.IsNullOrWhiteSpace(name))
                    {
                        throw new Exception();
                    }

                    pEnd = s.IndexOf(';', p);

                    if (pEnd == -1)
                    {
                        throw new Exception();
                    }

                    path = s.Substring(p, pEnd - p).Trim();
                    p    = pEnd + 1;
                    m    = s.Substring(p);

                    switch (m.ToLowerInvariant())
                    {
                        case "native":

                            mode = StartAs.Native;
                            break;

                        case "form":

                            mode = StartAs.Form;
                            break;

                        case "console":

                            mode = StartAs.Console;
                            break;

                        default:

                            throw new Exception();
                    }
                }
                catch
                {
                    throw new FormatException(string.Format(null, "Invalid service configuration [{0}].", s));
                }

                services.Add(new ServiceInfo(name, path, mode));
            }

            // Initialize the shared memory and events necessary to communicate
            // with the services.

            id    = Helper.NewGuid();
            inbox = new SharedMemInbox();
            inbox.Open(ControlMemPrefix + id.ToString(), ServiceControl.MaxMsgSize,
                       new SharedMemInboxReceiveDelegate(OnReceive));

            outbox  = new SharedMemOutbox(MaxMsgSize, MaxWaitTime);
            onReply = new GlobalAutoResetEvent(null);
            isOpen  = true;

            // Poll the services to find out which ones are already running.

            foreach (ServiceInfo info in services)
            {
                var state = this.GetStatus(info.Name);

                info.Started = state == ServiceState.Running || state == ServiceState.Starting;
            }
        }

        /// <summary>
        /// Releases any resources associated with the service control instance.
        /// </summary>
        public void Close()
        {
            id       = Guid.Empty;
            isOpen   = false;
            services = null;

            if (inbox != null)
            {
                inbox.Close();
                inbox = null;
            }

            if (outbox != null)
            {
                outbox.Close();
                outbox = null;
            }

            if (onReply != null)
            {
                onReply.Close();
                onReply = null;
            }
        }

        /// <summary>
        /// Sends the query message to the named service instance and waits for
        /// a reply.
        /// </summary>
        /// <param name="serviceName">The service name.</param>
        /// <param name="query">The query message.</param>
        /// <returns>The reply message.</returns>
        /// <remarks>
        /// Throws a TimeoutException if the service doesn't respond by MaxWaitTime.
        /// </remarks>
        private ServiceMsg Query(string serviceName, ServiceMsg query)
        {
            using (TimedLock.Lock(this))
            {
                refID       = Helper.NewGuid();
                query.RefID = refID;
            }

            query["Reply-To"] = ControlMemPrefix + id.ToString();

            outbox.Send(ServiceMemPrefix + serviceName, query.ToBytes());
            if (!onReply.WaitOne(MaxWaitTime, false))
            {
                throw new System.TimeoutException();
            }

            if (reply.Command != "Ack")
            {
                throw new InvalidOperationException("Invalid Service response.");
            }

            return reply;
        }

        /// <summary>
        /// Handles messages received from the shared memory inbox.
        /// </summary>
        /// <param name="raw">The raw message.</param>
        private void OnReceive(byte[] raw)
        {
            using (TimedLock.Lock(this))
            {
                var msg = new ServiceMsg(raw);

                if (msg.RefID != refID)
                {
                    return;     // Ignore message that aren't part of the transaction
                }

                reply = msg;
                onReply.Set();  // Wake up the query thread
            }
        }

        /// <summary>
        /// Returns the service configuration information for the named
        /// service.
        /// </summary>
        /// <param name="serviceName">The service name.</param>
        /// <returns>The service configuration information (or <c>null</c>).</returns>
        private ServiceInfo GetServiceInfo(string serviceName)
        {
            serviceName = serviceName.ToLowerInvariant();

            for (int i = 0; i < services.Count; i++)
            {
                var info = (ServiceInfo)services[i];

                if (info.Name.ToLowerInvariant() == serviceName)
                {
                    return info;
                }
            }

            return null;
        }

        /// <summary>
        /// Verifies that the actual status of the named service matches 
        /// the state the ServiceControl instance expects and then sends
        /// a HeartBeat query to the service to verify that it is still
        /// alive.
        /// </summary>
        /// <param name="serviceName">The service name.</param>
        /// <returns><c>true</c> if the service state is valid.</returns>
        /// <remarks>
        /// This can be used to to help implement heartbeat functionality
        /// to monitor the health of a service.
        /// </remarks>
        public bool VerifyService(string serviceName)
        {
            var info = GetServiceInfo(serviceName);

            if (info == null)
            {
                throw new InvalidOperationException(string.Format(null, "Service [{0}] is not managed by this instance.", serviceName));
            }

            if (!info.Started)
            {
                return true;
            }

            if (GetStatus(serviceName) != ServiceState.Running)
            {
                return false;
            }

            var ack = Query(serviceName, new ServiceMsg("HeartBeat"));

            return ack["Alive"] == "1";
        }

        /// <summary>
        /// Verifies that the actual status of the all services match
        /// the state the ServiceControl instance expects and then sends
        /// a HeartBeat query to the service to verify that it is still
        /// alive.
        /// </summary>
        /// <returns><c>true</c> if all services are healthy.</returns>
        public bool VerifyAll()
        {
            foreach (ServiceInfo info in services)
            {
                if (!VerifyService(info.Name))
                {
                    return false;
                }
            }

            return true;
        }

        /// <summary>
        /// Returns the current status of the named service.
        /// </summary>
        /// <param name="serviceName">The service name.</param>
        /// <returns>The status.</returns>
        /// <remarks>
        /// <note>
        /// The service must be present in the configuration 
        /// settings for this to work.
        /// </note>
        /// </remarks>
        public ServiceState GetStatus(string serviceName)
        {
            try
            {
                var ack = Query(serviceName, new ServiceMsg("GetStatus"));

                switch (ack["Status"].ToLowerInvariant())
                {

                    default:
                    case "unknown":     return ServiceState.Unknown;
                    case "stopped":     return ServiceState.Stopped;
                    case "starting":    return ServiceState.Starting;
                    case "running":     return ServiceState.Running;
                    case "shutdown":    return ServiceState.Shutdown;
                    case "stopping":    return ServiceState.Stopping;
                }
            }
            catch
            {
                return ServiceState.Unknown;
            }
        }

        /// <summary>
        /// Starts all services managed by the service controller.
        /// </summary>
        public void StartAll()
        {
            foreach (ServiceInfo info in services)
            {
                Start(info.Name);
            }
        }

        /// <summary>
        /// Attempts to start the named service if its not already running.
        /// </summary>
        /// <param name="serviceName">The service name.</param>
        /// <remarks>
        /// <para>
        /// The service will first be polled to see if its already running.
        /// If not, the method will attempt to start the service in the
        /// mode specified in the configuration settings for the named
        /// service.  Note that the service must be present in the
        /// configuration settings for this to work.
        /// </para>
        /// <para>
        /// For WinCE builds, mode will be ignored and services will always
        /// be started in console mode.
        /// </para>
        /// <para>
        /// All modes are supported on Win32 platforms.
        /// </para>
        /// </remarks>
        public void Start(string serviceName)
        {
            var info = GetServiceInfo(serviceName);

            if (info == null)
            {
                throw new InvalidOperationException(string.Format(null, "Service [{0}] not found in the configuration settings.", serviceName));
            }

            var state = GetStatus(serviceName);

            if (state != ServiceState.Unknown && state != ServiceState.Stopped)
            {
                return;
            }

            switch (info.Mode)
            {
                case StartAs.Console:

                    Process.Start(info.Path, "-mode:console -start");
                    break;

                case StartAs.Form:

                    Process.Start(info.Path, "-mode:form -start");
                    break;

                case StartAs.Native:

                    ServiceController controller;

                    controller = new ServiceController(serviceName);
                    controller.Start();
                    break;

                default:

                    Assertion.Fail("Unexpected service mode.");
                    break;
            }
        }

        /// <summary>
        /// Stops all services managed by the service controller.
        /// </summary>
        public void StopAll()
        {
            foreach (ServiceInfo info in services)
            {
                Stop(info.Name);
            }
        }

        /// <summary>
        /// Attempts to stop the named service if its not already stopped.
        /// </summary>
        /// <param name="serviceName">The service name.</param>
        /// <remarks>
        /// <note>
        /// The service must be present in the configuration 
        /// settings for this to work.
        /// </note>
        /// </remarks>
        public void Stop(string serviceName)
        {
            var info = GetServiceInfo(serviceName);

            if (info == null)
            {
                throw new InvalidOperationException(string.Format(null, "Service [{0}] not found in the configuration settings.", serviceName));
            }

            var state = GetStatus(serviceName);

            if (state == ServiceState.Unknown || state == ServiceState.Stopped)
            {
                return;
            }

            switch (info.Mode)
            {
                case StartAs.Console:
                case StartAs.Form:

                    Query(serviceName, new ServiceMsg("Stop"));
                    break;

                case StartAs.Native:

                    ServiceController controller;

                    controller = new ServiceController(serviceName);
                    controller.Stop();
                    break;

                default:

                    Assertion.Fail("Unexpected service mode.");
                    break;
            }
        }

        /// <summary>
        /// Attempts to shut down the named service if its not already stopped.
        /// </summary>
        /// <param name="serviceName">The service name.</param>
        /// <remarks>
        /// <note>
        /// The service must be present in the configuration 
        /// settings for this to work.
        /// </note>
        /// </remarks>
        public void Shutdown(string serviceName)
        {
            var info = GetServiceInfo(serviceName);

            if (info == null)
            {
                throw new InvalidOperationException(string.Format(null, "Service [{0}] not found in the configuration settings.", serviceName));
            }

            var state = GetStatus(serviceName);

            if (state == ServiceState.Unknown || state == ServiceState.Stopped)
            {
                return;
            }

            switch (info.Mode)
            {
                case StartAs.Console:
                case StartAs.Form:

                    Query(serviceName, new ServiceMsg("Shutdown"));
                    break;

                case StartAs.Native:

                    ServiceController controller;

                    controller = new ServiceController(serviceName);
                    controller.Stop();
                    break;

                default:

                    Assertion.Fail("Unexpected service mode.");
                    break;
            }
        }

        /// <summary>
        /// Commands the service to reload its configuration settings.
        /// </summary>
        /// <param name="serviceName">The service name.</param>
        /// <remarks>
        /// <note>
        /// The service must be present in the configuration 
        /// settings for this to work.
        /// </note>
        /// </remarks>
        public void Configure(string serviceName)
        {
            Query(serviceName, new ServiceMsg("Configure"));
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
