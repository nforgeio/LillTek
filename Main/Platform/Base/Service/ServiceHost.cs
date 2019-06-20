//-----------------------------------------------------------------------------
// FILE:        ServiceHost.cs
// CONTRIBUTOR: Jeff Lill
// COPYRIGHT:   Copyright (c) 2005-2015 by Jeffrey Lill.  All rights reserved.
// DESCRIPTION: Hosts a forms, console, native, or ASP.NET service.

using System;
using System.ServiceProcess;
using System.Configuration;
using System.Runtime.InteropServices;
using System.Windows.Forms;

using LillTek.Common;
using LillTek.Windows;

using Handle = System.IntPtr;

namespace LillTek.Service
{
    /// <summary>
    /// Hosts a service as either a native Windows service, as a Windows
    /// forms application, a console application, or an ASP.NET application, 
    /// depending on command line parameters passed and the capabilities of 
    /// the current platform.
    /// </summary>
    /// <remarks>
    /// <para>
    /// This class is pretty easy to use.  For most implementations, you'll instantiate your 
    /// service implementation (derived from <see cref="IService" />) then pass this instance 
    /// and the application's command line arguments to <see cref="Run" />.  This static method 
    /// will parse the arguments to determine how top host the service, as a native Windows
    /// Service, as a Windows Forms application, or as a Console application and then
    /// start and run the service from there.  <see cref="Run" /> handles everything and
    /// returns when the service has stopped and the application should be terminated.
    /// </para>
    /// <para>
    /// Occasionally, it's necessary to explictly manage a service instance within the
    /// context of another process.  This is the case when deploying services within
    /// ASP.NET applications or as plug-ins to other applications.  To handle these
    /// situations you'll want to instantiate a <see cref="ServiceHost" /> instance,
    /// passing your service instance, and then use <see cref="Start" /> and <see cref="Stop" />
    /// to manage your service.
    /// </para>
    /// </remarks>
    public class ServiceHost
    {
        //---------------------------------------------------------------------
        // Static members

        /// <summary>
        /// Adds the installers that handle the installation of the application
        /// as a native Windows service.
        /// </summary>
        /// <param name="installer">The application's installer.</param>
        /// <param name="props">The service installation properties.</param>
        public static void AddInstallers(System.Configuration.Install.Installer installer, ServiceInstallProperties props)
        {
            var serviceInstaller             = new ServiceInstaller();
            var serviceProcessInstaller      = new ServiceProcessInstaller();

            serviceInstaller.DisplayName     = props.Name;
            serviceInstaller.ServiceName     = props.Name;
            serviceInstaller.StartType       = props.StartMode;

            serviceProcessInstaller.Account  = props.Account;
            serviceProcessInstaller.Username = props.UserName;
            serviceProcessInstaller.Password = props.Password;

            installer.Installers.AddRange(new System.Configuration.Install.Installer[] { serviceProcessInstaller, serviceInstaller });
        }

        private enum Mode
        {
            Console,
            Service,
            Form,
            Web
        }

        /// <summary>
        /// Runs the service passed as either a native service or a Windows
        /// forms application, depending on the command line arguments
        /// passed.
        /// </summary>
        /// <param name="service">The service instance.</param>
        /// <param name="logProvider">Specifies the system log provider (or <c>null</c>).</param>
        /// <param name="args">The command line arguments.</param>
        /// <remarks>
        /// <para>
        /// This method intreprets the following command-line arguments:
        /// "-mode" and "-start".
        /// </para>
        /// <para>
        /// The -mode parameter can be passed as:
        /// </para>
        /// <code language="none">
        ///     -mode:console       Runs as a console application
        ///     -mode:service       Runs as a native Windows service
        ///     -mode:form          Runs as a Windows Forms application
        /// </code>   
        /// <note>
        /// Windows CE supports only -mode:console and any
        /// other parameter will be ignored.  If none of these are
        /// passed then -mode:service will be assumed.
        /// </note>
        /// <para>
        /// The "-start" argument is necessary only if the service
        /// is running as a Windows form application.  If present, this
        /// indicates that the service should be started automatically
        /// by this method.
        /// </para>
        /// <para>
        /// The <paramref name="logProvider" /> parameter can be used to override the default
        /// system log provider implementation which will be provided
        /// if <paramref name="logProvider" /> as <c>null</c>.  By default, services hosted as a Windows Form
        /// will direct the system log to a control on the form as well as the
        /// Windows event log.  Native and internal services will use the 
        /// Windows event log, and console services will discard log postings by 
        /// default.
        /// </para>
        /// </remarks>
        public static void Run(IService service, ISysLogProvider logProvider, string[] args)
        {
            var mode      = Mode.Service;
            var autoStart = false;

            foreach (string arg in args)
            {
                switch (arg.ToLowerInvariant())
                {
                    case "-mode:console":

                        mode = Mode.Console;
                        break;

                    case "-mode:service":

                        mode = Mode.Service;
                        break;

                    case "-mode:form":

                        mode = Mode.Form;
                        break;

                    case "-start":

                        autoStart = true;
                        break;
                }
            }

            switch (mode)
            {
                case Mode.Console:
                    {
                        var host = new ConsoleServiceHost();

                        host.Initialize(args, service, logProvider, true);
                        service.Start(host, args);
                        host.WaitForStop();
                        break;
                    }

                case Mode.Service:
                    {
                        var host = new NativeServiceHost();

                        System.ServiceProcess.ServiceBase[] services = new System.ServiceProcess.ServiceBase[] { host };

                        host.Initialize(args, service, logProvider, false);
                        System.ServiceProcess.ServiceBase.Run(services);
                        break;
                    }

                case Mode.Form:
                    {
                        var host = new FormServiceHost();

                        host.Initialize(args, service, logProvider, autoStart);
                        Application.Run(host);
                        break;
                    }
            }
        }

        /// <summary>
        /// Changes the user account used by the named native Windows service.
        /// </summary>
        /// <param name="serviceName">The service name.</param>
        /// <param name="account">Specifies the account type.</param>
        /// <param name="userID">The user ID (or <c>null</c> if <b>account!=ServiceAccount.User)</b>.</param>
        /// <param name="password">The password (or <c>null</c> if <b>account!=ServiceAccount.User)</b>.</param>
        /// <exception cref="ServiceException">Thrown if there's a problem completing the operation.</exception>
        public static void ChangeServiceAccount(string serviceName, ServiceAccount account, string userID, string password)
        {
            var hScManager = IntPtr.Zero;
            var hScLock    = IntPtr.Zero;
            var hService   = IntPtr.Zero;

            try
            {
                hScManager = WinSvc.OpenSCManager(null, null, WinSvc.ServiceControlManagerType.SC_MANAGER_ALL_ACCESS);

                if (hScManager.ToInt64() <= 0)
                {
                    throw new ServiceException("Error [{0}] opening the service manager.", Marshal.GetLastWin32Error());
                }

                hScLock = WinSvc.LockServiceDatabase(hScManager);

                if (hScLock.ToInt64() <= 0)
                {
                    throw new ServiceException("Error [{0}] locking the service manager.", Marshal.GetLastWin32Error());
                }

                hService = WinSvc.OpenService(hScManager, serviceName, WinSvc.ACCESS_TYPE.SERVICE_ALL_ACCESS);

                if (hService.ToInt64() <= 0)
                {
                    throw new ServiceException("Error [{0}] opening service [{1}].", Marshal.GetLastWin32Error(), serviceName);
                }

                switch (account)
                {
                    case ServiceAccount.LocalService:

                        userID   = @"NT Authority\LocalService";
                        password = null;
                        break;

                    case ServiceAccount.LocalSystem:

                        userID   = @".\LocalSystem";
                        password = null;
                        break;

                    case ServiceAccount.NetworkService:

                        userID   = @"NT Authority\NetworkService";
                        password = null;
                        break;

                    case ServiceAccount.User:

                        break;

                    default:

                        throw new NotImplementedException();
                }

                if (!WinSvc.ChangeServiceConfig(hService,
                                                WinSvc.ServiceType.SERVICE_WIN32_OWN_PROCESS,
                                                WinSvc.SERVICE_NO_CHANGE,
                                                WinSvc.SERVICE_NO_CHANGE,
                                                null,
                                                null,
                                                IntPtr.Zero,
                                                null,
                                                userID,
                                                password,
                                                null))
                {
                    throw new ServiceException("Error [{0}] updating the [{1}] service configuration.", Marshal.GetLastWin32Error(), serviceName);
                }
            }
            finally
            {
                if (hService.ToInt64() < 0)
                {
                    WinSvc.CloseServiceHandle(hService);
                }

                if (hScLock.ToInt64() > 0)
                {
                    WinSvc.UnlockServiceDatabase(hScLock);
                }

                if (hScManager.ToInt64() > 0)
                {
                    WinSvc.CloseServiceHandle(hScManager);
                }
            }
        }

        //---------------------------------------------------------------------
        // Instance members

        private IService            service     = null;
        private IServiceHost        host        = null;
        private ISysLogProvider     logProvider = null;
        private string[]            args        = null;

        /// <summary>
        /// Instantiates a service host that can be explicitly controlled by the application.
        /// </summary>
        /// <param name="service">The service instance.</param>
        /// <param name="logProvider">Specifies the system log provider (or <c>null</c>).</param>
        /// <param name="args">The command line arguments to be passed to the service (or <c>null</c>).</param>
        public ServiceHost(IService service, ISysLogProvider logProvider, string[] args)
        {
            if (logProvider == null)
            {
                logProvider = new NativeSysLogProvider(service.Name);
            }

            this.service     = service;
            this.logProvider = logProvider;
            this.args        = args != null ? args : new string[0];
        }

        /// <summary>
        /// Starts the hosted service.
        /// </summary>
        public void Start()
        {
            if (host != null)
            {
                throw new InvalidOperationException("ServiceHost instances cannot be reused.");
            }

            SysLog.LogProvider = logProvider;

            host = new ApplicationServiceHost();
            host.Initialize(args, service, logProvider, true);
            service.Start(host, args);
        }

        /// <summary>
        /// Stops the hosted service.
        /// </summary>
        public void Stop()
        {
            if (host == null)
            {
                throw new InvalidOperationException("Service has not started.");
            }

            service.Stop();
        }

        /// <summary>
        /// Returns <see cref="StartAs.Application" /> indicating that the service is
        /// hosted within an application.
        /// </summary>
        public StartAs StartedAs
        {
            get { return StartAs.Application; }
        }
    }
}
