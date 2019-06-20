//-----------------------------------------------------------------------------
// FILE:        SwitchApp.cs
// CONTRIBUTOR: Jeff Lill
// COPYRIGHT:   Copyright (c) 2005-2015 by Jeffrey Lill.  All rights reserved.
// DESCRIPTION: Base class for a NeonSwitch application.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;

using FreeSWITCH;
using FreeSWITCH.Native;

using LillTek.Common;
using LillTek.Telephony.Common;

namespace LillTek.Telephony.NeonSwitch
{
    /// <summary>
    /// Base class for NeonSwitch application.
    /// </summary>
    /// <remarks>
    /// <para>
    /// All NeonSwitch applications must define a <b>single</b> class that derives from
    /// <see cref="SwitchApp" />.  This class will form the global context for the 
    /// application and the <see cref="Main" /> method the application entry point.
    /// </para>
    /// <note>
    /// Applications should perform little or no initialization within their constructor
    /// since the underlying FreeSWITCH engine limits the amount of time a managed module
    /// may use to initialize itself to about 30 seconds.  Applications must wait
    /// for <see cref="Main" /> to be called before performing activties such as loading their
    /// configuration settings, creating a message router or establishing database connections
    /// since <see cref="Main" /> is called on global application thread, rather than the 
    /// FreeSWITCH managed loader thread.
    /// </note>
    /// <para>
    /// While within the <see cref="Main" /> method, applications must subscribe to any
    /// <see cref="Switch" /> events they wish to process (such as <see cref="Switch.CallSessionEvent" />
    /// and <see cref="Switch.ExecuteEvent" />.  Event subscription may only be performed 
    /// while the application is in the context of its <see cref="Main" /> method.  It is not 
    /// possible to add new event subscriptions after exiting main and it is also not possible
    /// to unscribe to a <see cref="Switch" /> event.
    /// </para>
    /// <para>
    /// NeonSwitch applications that need to perform actions to shut down gracefully should
    /// override the protected <see cref="Close" /> method.  This method will be called just 
    /// before the application is terminated, allowing for a graceful shutdown.
    /// </para>
    /// <note>
    /// It is <b>not necessary</b> for NeonSwitch application that override <see cref="Main" /> and
    /// <see cref="Close" /> to call the base class implementations of these methods.
    /// </note>
    /// </remarks>
    public class SwitchApp
    {
        //---------------------------------------------------------------------
        // Static members

        private static readonly TimeSpan MaxThreadStopTime = TimeSpan.FromSeconds(30);

        /// <summary>
        /// Returns <c>true</c> if the application running is the core NeonSwitch service.
        /// </summary>
        internal static bool IsCore { get; private set; }

        /// <summary>
        /// Returns <c>true</c> if the application is currently executing its <see cref="Main" /> method.
        /// </summary>
        internal static bool InMain { get; private set; }

        /// <summary>
        /// Returns the name of the application as specified in its loader INI file.
        /// </summary>
        public static string Name { get; private set; }

        /// <summary>
        /// Returns the name of the DLL file that <b>mod_managed</b> used to launch
        /// this application.
        /// </summary>
        public static string LoaderDllName { get; private set; }

        /// <summary>
        /// Returns the application's installation folder path.
        /// </summary>
        public static string ApplicationPath { get; private set; }

        /// <summary>
        /// Formats and throws a <see cref="NotImplementedException" /> for <see cref="ISwitchSubcommand"/>
        /// implementation that does not provide an <see cref="ISwitchSubcommand.Execute" /> implementation.
        /// </summary>
        /// <param name="command">The command name.</param>
        public static void ThrowExecuteNotImplemented(string command)
        {
            throw new NotImplementedException(string.Format("{0}: Execute({1}) is not implemented.", Name, command));
        }

        /// <summary>
        /// Formats and throws a <see cref="NotImplementedException" /> for <see cref="ISwitchSubcommand"/>
        /// implementation that does not provide an <see cref="ISwitchSubcommand.ExecuteBackground" /> implementation.
        /// </summary>
        /// <param name="command">The command name.</param>
        public static void ThrowExecuteBackgroundNotImplemented(string command)
        {
            throw new NotImplementedException(string.Format("{0}: ExecuteBackground({1}) is not implemented.", Name, command));
        }

        //---------------------------------------------------------------------
        // Instance members

        private object      syncLock = new object();
        private bool        mainCalled;
        private Thread      appThread;

        /// <summary>
        /// Default constructor.
        /// </summary>
        public SwitchApp()
        {
            if (Switch.Application != null)
                throw new InvalidOperationException("A NeonSwitch application has already been initialized in this AppDomain.");

            Switch.Application = this;
        }

        /// <summary>
        /// Called by <see cref="AppLoader" /> to complete the application load process.
        /// </summary>
        /// <param name="appName">The application name.</param>
        /// <param name="appPath">Path to the application folder.</param>
        /// <param name="loaderDllName">Name of the NeonSwitch application loader DLL file.</param>
        internal void Load(string appName, string loaderDllName, string appPath)
        {
            // Make sure that only one application per domain is loaded.

            if (ApplicationPath != null)
                throw new InvalidOperationException(string.Format("Attempt to load the application [{0}] into a domain that is already hosting [{1}].", appPath, ApplicationPath));

            // Save these.

            SwitchApp.Name            = appName;
            SwitchApp.IsCore          = String.Compare(SwitchApp.Name, Switch.CoreAppName, true) == 0;
            SwitchApp.LoaderDllName   = loaderDllName;
            SwitchApp.ApplicationPath = appPath;

            // Hook the application domain's DomainUnload event to determine when the
            // application is quitting so we can provide the application with the
            // opportunity to shut down gracefully.  We'll also use this as an oppertunity
            // to stop the application's background thread.

            AppDomain.CurrentDomain.DomainUnload +=
                (s, a) =>
                {
                    if (mainCalled)
                        Close();

                    Stop();
                };

            // Set the SysLog provider to reference the underyling FreeSWITCH log
            // subsystem by default.  Applications may change this if required.

            SysLog.LogProvider = new SwitchLogProvider();

            // Start the application background thread that actually implements
            // the application lifecycle.

            appThread = new Thread(new ThreadStart(AppThread));
            appThread.Start();
        }

        /// <summary>
        /// <para>
        /// The application entry point.
        /// </para>
        /// <note>
        /// It is not necessary for derived classes to call this in their method override.
        /// </note>
        /// </summary>
        protected virtual void Main()
        {
        }

        /// <summary>
        /// <para>
        /// Called just before the application is unloaded by NeonSwitch giving
        /// the application the opportunity to perform any termination related
        /// activities.
        /// </para>
        /// <note>
        /// It is not necessary for derived classes to call this in their method override.
        /// </note>
        /// </summary>
        protected virtual void Close()
        {
        }

        /// <summary>
        /// Terminates the application's thread if it is running and peforms other cleanup activities.
        /// </summary>
        private void Stop()
        {
            // Stop the thread.

            if (appThread != null)
            {
                Switch.StopEventLoop();

                if (!appThread.Join(MaxThreadStopTime))
                    appThread.Abort();

                Switch.Shutdown();

                appThread = null;
            }
        }

        /// <summary>
        /// Stops execution of the application including calling <see cref="Close" /> and
        /// releasing any associated resources.
        /// </summary>
        /// <exception cref="InvalidOperationException">Thrown if the application has not been properly initialized.</exception>
        /// <remarks>
        /// This is a somewhat specialized method that applications can use the shut themselves
        /// down and will not likely be called by applications in the normal course of execution.
        /// </remarks>
        public void Exit()
        {
            if (!mainCalled)
                throw new InvalidOperationException("Cannot terminate an application before Main() is called and has returned.");
            else
            {
                try
                {
                    Close();
                }
                catch (Exception e)
                {
                    SysLog.LogException(e);
                }
            }

            Stop();
        }

        /// <summary>
        /// The application worker thread.
        /// </summary>
        private void AppThread()
        {
            // All NeonSwitch applications (except for the core service) need to wait for
            // the core to be initialized before the application can be started.  We're
            // going to spin here until the core indicates that it's ready.

            var warningTimer = new PolledTimer(TimeSpan.FromSeconds(60));

            if (!SwitchApp.IsCore)
            {
                while (true)
                {
                    if (String.Compare(Switch.GetGlobal(SwitchGlobal.NeonSwitchReady), "true", true) == 0)
                        break;

                    if (warningTimer.HasFired)
                    {
                        warningTimer.Disable();
                        SysLog.LogWarning("NeonSwitch application [{0}] has waited [{1}] for the core NeonSwitch service to start.", SwitchApp.Name, warningTimer.Interval);
                    }

                    Thread.Sleep(TimeSpan.FromSeconds(1));
                }
            }

            // Continue performing wwitch initialization.  We needed to wait until after
            // the NeonSwitch core service started before calling this.

            Switch.Initialize();

            // Call the application entry point so it can initalize itself.

            try
            {
                InMain = true;

                Main();

                InMain = false;
                mainCalled = true;
            }
            catch (Exception e)
            {
                SysLog.LogException(e, "The NeonSwitch application's Main() method threw an exception.");
                throw;
            }
            finally
            {
                InMain = false;
            }

            // NeonSwitch handles the event dispatching.

            Switch.EventLoop();
        }

        /// <summary>
        /// Called by the <see cref="AppLoader" /> when the application has been assigned a new call session.
        /// </summary>
        /// <param name="context">The application context.</param>
        internal void OnNewCallSession(AppContext context)
        {
            Switch.RaiseCallSessionEvent(new CallSessionArgs(new CallSession(context.Session), context.Arguments));
        }

        /// <summary>
        /// Called by the <see cref="AppLoader" /> to execute a command synchronously.
        /// </summary>
        /// <param name="context">The command context.</param>
        internal void OnExecute(ApiContext context)
        {
            Switch.OnExecute(new ExecuteEventArgs(context));
        }

        /// <summary>
        /// Called by the <see cref="AppLoader" /> to execute a command asynchronously.
        /// </summary>
        /// <param name="context">The command context.</param>
        internal void OnExecuteBackground(ApiBackgroundContext context)
        {
            Switch.OnExecuteBackground(new ExecuteBackgroundEventArgs(context));
        }
    }
}
