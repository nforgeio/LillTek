//-----------------------------------------------------------------------------
// FILE:        ApplicationHost.cs
// CONTRIBUTOR: Jeff Lill
// COPYRIGHT:   Copyright (c) 2005-2015 by Jeffrey Lill.  All rights reserved.
// DESCRIPTION: Application hosting globals for Windows Forms applications.

using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Net;
using System.Threading;
using System.Windows;
using System.Windows.Forms;

using LillTek.Common;

namespace LillTek.Client
{
    /// <summary>
    /// Application hosting globals for Windows Forms or XAML applications.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Applications must call <see cref="Initialize" /> from the user interface thread very early in the application's
    /// lifecycle (e.g. within the <b>App</b> class constructor).
    /// </para>
    /// </remarks>
    public static class ApplicationHost
    {
        private static int progressDepth = 0;

        /// <summary>
        /// Initializes the application hosting environment.  This should be called
        /// called from the user interface thread very early in the application's
        /// lifecycle (e.g. within the <b>App</b> class constructor).
        /// </summary>
        /// <exception cref="InvalidOperationException">Thrown if the method is not called from the user interface thread.</exception>
        public static void Initialize()
        {
            // $todo(jeff.lill):
            //
            // Verify that this is the UI thread.

            //if (control.InvokeRequired)
            //    throw new InvalidOperationException("[ApplicationHost.Initialize()] can be called only on the user interface thread.");

            // Misc intialization

            HostDeviceType = HostDeviceType.PC;

            // Configure the global UI action dispatcher.

            Helper.SetUIActionDispatcher(action => UIDispatch(action));
        }

        /// <summary>
        /// Identifies the type of device the application is running on (e.g. <see cref="LillTek.Client.HostDeviceType.PC" />,
        /// <see cref="LillTek.Client.HostDeviceType.Phone" />, or <see cref="LillTek.Client.HostDeviceType.Tablet" />).
        /// </summary>
        public static HostDeviceType HostDeviceType { get; private set; }

        /// <summary>
        /// Returns <c>true</c> if the calling code is executing on the main user interface thread.
        /// </summary>
        public static bool IsUIThread
        {
            get { throw new NotImplementedException(); }
        }

        /// <summary>
        /// Invokes a method on the UI thread that created the control specified.
        /// </summary>
        /// <param name="control">The control.</param>
        /// <param name="method">The method delegate.</param>
        /// <returns>The method result.</returns>
        /// <remarks>
        /// This method implements a workaround for a bug in the .NET Framework
        /// v1.1 SP1 where Control.Invoke() can hang under high loads.
        /// </remarks>
        public static object Invoke(Control control, Delegate method)
        {
            if (!control.InvokeRequired)
            {
                return control.Invoke(method);
            }

            var ar    = control.BeginInvoke(method);
            var hWait = ar.AsyncWaitHandle;

            Thread.MemoryBarrier();

            if (!ar.IsCompleted)
            {
                hWait.WaitOne();
            }

            return control.EndInvoke(ar);
        }

        /// <summary>
        /// Invokes a method on the UI thread that created the control specified.
        /// </summary>
        /// <param name="control">The control.</param>
        /// <param name="method">The method delegate.</param>
        /// <param name="args">The arguments to be passed.</param>
        /// <returns>The method result.</returns>
        /// <remarks>
        /// This method implements a workaround for a bug in the .NET Framework
        /// v1.1 SP1 where Control.Invoke() can hang under high loads.
        /// </remarks>
        public static object Invoke(Control control, Delegate method, object[] args)
        {
            if (!control.InvokeRequired)
                return control.Invoke(method, args);

            var ar    = control.BeginInvoke(method, args);
            var hWait = ar.AsyncWaitHandle;

            Thread.MemoryBarrier();

            if (!ar.IsCompleted)
            {
                hWait.WaitOne();
            }

            return control.EndInvoke(ar);
        }

        /// <summary>
        /// Invokes an action on the UI thread, logging any exceptions thrown.
        /// If the executing thread is the UI thread, then the action will
        /// be executed synchronously, if it is not the UI thread, then the
        /// action will be executed asynchronously.
        /// </summary>
        /// <param name="action">The action.</param>
        /// <exception cref="InvalidOperationException">Thrown if the global <see cref="Application" /> instance has not yet been created.</exception>
        /// <remarks>
        /// <note>
        /// This method handles unit testing scenarios where the test is not
        /// actually running in a WPF environment by simulating the dispatch 
        /// via <see cref="Helper" />.<see cref="Helper.EnqueueSerializedAction(Action)" />.
        /// </note>
        /// </remarks>
        public static void UIDispatch(Action action)
        {
            // $todo(jeff.lill): Implement this.

            throw new NotImplementedException("[UIDispatch] is not currently implemented for Windows Forms.");
        }

        /// <summary>
        /// Controls the application progress indicator (or <c>null</c> the application
        /// has not defined an indicator).
        /// </summary>
        public static IProgressManager ProgressManager { get; set; }

        /// <summary>
        /// Starts the progress indicator if necessary and increments the progress indicator depth.
        /// </summary>
        /// <remarks>
        /// Calls to this method must be matched with a call to <see cref="StopProgress" />.
        /// Calls may be made on non-UI threads and may also be nested.
        /// </remarks>
        public static void StartProgress()
        {
            ApplicationHost.UIDispatch(
                () =>
                {
                    if (progressDepth == 0 && ProgressManager != null)
                    {
                        progressDepth++;
                        ProgressManager.StartProgress();
                    }
                });
        }

        /// <summary>
        /// Decrements the progress indicator depth and stops the indicator if the depth goes to zero.
        /// </summary>
        /// <remarks>
        /// Calls may be made on non-UI threads and may also be nested.
        /// </remarks>
        public static void StopProgress()
        {
            ApplicationHost.UIDispatch(
                () =>
                {
                    if (progressDepth <= 0 && ProgressManager != null)
                    {
                        return;     // Ignore stack underflow
                    }

                    if (--progressDepth == 0)
                    {
                        ProgressManager.StopProgress();
                    }
                });
        }

        /// <summary>
        /// Stops the progress indicator if its running, resetting the progress depth to zero.
        /// </summary>
        /// <remarks>
        /// Calls may be made on non-UI threads and may also be nested.
        /// </remarks>
        public static void StopAllProgress()
        {
            ApplicationHost.UIDispatch(
                () =>
                {
                    progressDepth = 0;

                    if (ProgressManager != null)
                    {
                        ProgressManager.StopProgress();
                    }
                });
        }

        /// <summary>
        /// Closes the current application with an process exit code of zero.
        /// </summary>
        public static void Exit()
        {
            Application.Exit();
        }

        /// <summary>
        /// Closes the current application with the specific process exit code.
        /// </summary>
        /// <param name="exitCode">The process exit code.</param>
        public static void Exit(int exitCode)
        {
            // $todo(jeff.lill): Not sure how to set the exit code for a Forms application.

            Application.Exit();
        }
    }
}
