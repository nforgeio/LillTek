//-----------------------------------------------------------------------------
// FILE:        GlobalManualResetEvent.cs
// CONTRIBUTOR: Jeff Lill
// COPYRIGHT:   Copyright (c) 2005-2015 by Jeffrey Lill.  All rights reserved.
// DESCRIPTION: This class implements a ManualResetEvent that can be named and used
//              across processes.

using System;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Threading;

using Microsoft.Win32.SafeHandles;

using LillTek.Common;
using LillTek.Windows;

namespace LillTek.LowLevel
{
    /// <summary>
    /// This class implements a ManualResetEvent that can be named and used
    /// across processes.  For some reason, this capability was left out of 
    /// the .NET Framework class.
    /// </summary>
    public unsafe class GlobalManualResetEvent
    {
        //---------------------------------------------------------------------
        // Implementation.  Methods are identical to those defined by ManualResetEvent.

        private ManualResetEvent    evt;
        private string              name;

        /// <summary>
        /// This constructor initializes the event.  Note that this constructor
        /// will always return an event in the reset state.
        /// </summary>
        /// <param name="name">
        /// The name of the event.  The name is limited to 128 characters and
        /// is case sensitive.  May be passed as null.
        /// </param>
        public GlobalManualResetEvent(string name)
        {
            SafeWaitHandle      hEvent;
            SecurityAttributes  sa;

            if (name != null)
            {
                if (name.Length > 128)
                    throw new ArgumentException("Name exceeds 128 characters.", "name");

                if (name.IndexOfAny(new char[] { '/', '\\' }) != -1)
                    throw new ArgumentException("Name may not include forward or backslashes.");
            }

            sa = new SecurityAttributes(SecurityAccess.Unrestricted);
            try
            {
                // Here's what the name abbreviations mean:
                //
                //      LT = LillTek
                //      EV = Event

                if (name != null)
                    name = @"Global\LT:EV:" + name;

                // $hack(jeff.lill):
            //
            // Beginning with a late service Windows XP service pack, applications not running in
            // Windows session 0 as a service cannot create global shared merfetryory or other objects.
            // This results in the API below failing by returning a NULL handle and GetLastError()
            // returning ERROR_ACCESS_DENIED.  Windows added this restriction to prevent malicious
            // code from creating global objects that will be used by well known services and then 
            // squating on them.
            //
            // The work-around below detects this situation and tries creating a non-global object
            // instead.  This will work for most unit testing scenarios.

            retry:

                hEvent = WinApi.CreateEvent(sa.AttributesPtr, true, false, name);

                if (hEvent.IsInvalid)
                {
                    if (name.ToLowerInvariant().StartsWith(@"global\") && WinApi.GetLastError() == WinErr.ERROR_ACCESS_DENIED)
                    {
                        name = name.Substring(7);   // @"global\".Length
                        goto retry;
                    }

                    throw new Exception("Unable to create the global event.");
                }
            }
            finally
            {
                sa.Close();
            }

            this.name               = name;
            this.evt                = new ManualResetEvent(false);
            this.evt.SafeWaitHandle = hEvent;
        }

        /// <summary>
        /// This constructor initializes the event.
        /// </summary>
        /// <param name="name">
        /// The name of the event.  The name is limited to MAX_PATH characters and
        /// is case sensitive.  May be passed as null.
        /// </param>
        /// <param name="requestedState">The requested initial event state: true for signalled.</param>
        /// <param name="actualState">This will be set to the actual state of the created event.</param>
        public GlobalManualResetEvent(string name, bool requestedState, out bool actualState)
        {
            SafeWaitHandle      hEvent;
            SecurityAttributes  sa;
            int err;

            if (name != null)
            {
                if (name.Length > 128)
                    throw new ArgumentException("Name exceeds 128 characters.", "name");

                if (name.IndexOfAny(new char[] { '/', '\\' }) != -1)
                    throw new ArgumentException("Name may not include forward or backslashes.");
            }

            sa          = new SecurityAttributes(SecurityAccess.Unrestricted);
            actualState = false;
            try
            {
                // Here's what the name abbreviations mean:
                //
                //      LT = LillTek
                //      EV = Event

                if (name != null)
                    name = @"Global\LT:EV:" + name;

                hEvent = WinApi.CreateEvent(sa.AttributesPtr, true, requestedState, name);
                err    = WinApi.GetLastError();
                if (hEvent.IsInvalid)
                    throw new Exception("Unable to create the global event.");

                if (requestedState)
                    actualState = err != WinApi.ERROR_ALREADY_EXISTS;
            }
            finally
            {
                sa.Close();
            }

            this.name               = name;
            this.evt                = new ManualResetEvent(actualState);
            this.evt.SafeWaitHandle = hEvent;
        }

        /// <summary>
        /// Closes the event, releasing all resources.
        /// </summary>
        public void Close()
        {
            evt.Close();
        }

        /// <summary>
        /// Returns the name of the event.
        /// </summary>
        public string Name
        {
            get { return name; }
        }

        /// <summary>
        /// Resets the event to the non-signalled state.
        /// </summary>
        /// <returns></returns>
        public bool Reset()
        {
            return evt.Reset();
        }

        /// <summary>
        /// Sets the event to the signalled stated, releasing any waiting threads.
        /// </summary>
        /// <returns><c>true</c> if the operation succeeded.</returns>
        public bool Set()
        {
            return evt.Set();
        }

        /// <summary>
        /// Blocks the current thread until the event enters the signalled state.
        /// </summary>
        /// <returns><c>true</c> if the current thread receives the signal.</returns>
        public bool WaitOne()
        {
            return evt.WaitOne();
        }

#if WINFULL
        /// <summary>
        /// Blocks the current thread for a specified maximum time, waiting for
        /// the event to enter the signalled state.
        /// </summary>
        /// <param name="millisecondsTimeout">The maximum number of milliseconds to wait.</param>
        /// <param name="exitContext"><c>true</c> to exit the synchronization domain for the context before the wait (if in a synchronized context), and reacquire it; otherwise, false.</param>
        /// <returns><c>true</c> if the current thread receives the signal, <c>false</c> if the method timed out.</returns>
        public bool WaitOne(int millisecondsTimeout, bool exitContext)
        {
            return evt.WaitOne(millisecondsTimeout, exitContext);
        }


        /// <summary>
        /// Blocks the current thread for a specified maximum time, waiting for
        /// the event to enter the signalled state.
        /// </summary>
        /// <param name="timeout">The maximum time span to wait.</param>
        /// <param name="exitContext"><c>true</c> to exit the synchronization domain for the context before the wait (if in a synchronized context), and reacquire it; otherwise, false.</param>
        /// <returns><c>true</c> if the current thread receives the signal, <c>false</c> if the method timed out.</returns>
        public bool WaitOne(TimeSpan timeout, bool exitContext)
        {
            Assertion.Test(timeout.Ticks / (int)TimeSpan.TicksPerMillisecond <= 0x7FFFFFFFF, "Timespan too large.");
            return evt.WaitOne(timeout, exitContext);
        }
#else
        public bool WaitOne(int millisecondsTimeout, bool exitContext)
        {
            return WinApi.WaitForSingleObject(evt.Handle, (uint) millisecondsTimeout) == WinApi.WAIT_OBJECT_0;
        }

        public bool WaitOne(TimeSpan timeout, bool exitContext)
        {    
            Assertion.Test(timeout.Ticks/(int) TimeSpan.TicksPerMillisecond <= 0x7FFFFFFFF,"Timespan too large.");
            return WinApi.WaitForSingleObject(evt.Handle, (uint) (timeout.Ticks/TimeSpan.TicksPerMillisecond)) == WinApi.WAIT_OBJECT_0;
        }
#endif
    }
}
