//-----------------------------------------------------------------------------
// FILE:        GlobalMutex.cs
// CONTRIBUTOR: Jeff Lill
// COPYRIGHT:   Copyright (c) 2005-2015 by Jeffrey Lill.  All rights reserved.
// DESCRIPTION: This class implements a Mutex that can be named and used
//              across processes.

using System;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Threading;

using LillTek.Common;
using LillTek.Windows;

// $todo(jeff.lill):
//
// Microsoft added new security restrictions for global objects such as 
// mutexes, memory mapped files, etc. starting with Vista.  This is 
// causing problems with this class (and probably other low level classes).
// Here are some links discussing this:
//
// http://social.msdn.microsoft.com/forums/en-US/windowssecurity/thread/08e18474-5f8c-4294-a9cf-7ede1ff8ae1f/
// http://msdn.microsoft.com/en-us/library/Aa382954
//
// There might not be a general fix for this since it looks like global
// objects can only be created by applications running in session 0
// which includes only services.

namespace LillTek.LowLevel
{
    /// <summary>
    /// This class implements a Mutex with unrestricted Windows security.  Note that
    /// this class is marked as unsafe.
    /// </summary>
    public unsafe class GlobalMutex
    {
        private IntPtr      hMutex = IntPtr.Zero;   // Win32 mutex handle
        private string      name;

        /// <summary>
        /// Constructor.  Specify whether the mutex it to be owned initially
        /// by the caller as well as the mutex's name.  Note that with this
        /// constructor, the mutex will not be owned initially by the caller.
        /// </summary>
        /// <param name="name">Name of the mutex.</param>
        public GlobalMutex(string name)
        {
            SecurityAttributes sa = new SecurityAttributes(SecurityAccess.Unrestricted);

            if (name.Length > 128)
                throw new ArgumentException("Name exceeds 128 characters.", "name");

            if (name.IndexOfAny(new char[] { '/', '\\' }) != -1)
                throw new ArgumentException("Name may not include forward or backslashes.");

            this.name = name;

            try
            {
                // $hack(jeff.lill):
                //
                // Beginning with a late service Windows XP service pack, applications not running in
                // Windows session 0 as a service cannot create global shared memory or other objects.
                // This results in the API below failing by returning a NULL handle and GetLastError()
                // returning ERROR_ACCESS_DENIED.  Windows added this restriction to prevent malicious
                // code from creating global objects that will be used by well known services and then 
                // squating on them.
                //
                // The work-around below detects this situation and tries creating a non-global object
                // instead.  This will work for most unit testing scenarios.

                string  mutexName = @"Global\LT:MX:" + name;
                int     error;

            retry:

                // Here's what the name abbreviations mean:
                //
                //      LT = LillTek
                //      MX = Mutex

                hMutex = WinApi.CreateMutex(sa.AttributesPtr, false, mutexName);
                error  = WinApi.GetLastError();

                if (hMutex == IntPtr.Zero)
                {
                    if (mutexName.ToLowerInvariant().StartsWith(@"global\") && error == WinErr.ERROR_ACCESS_DENIED)
                    {
                        mutexName = "LT:MX:" + name;
                        goto retry;
                    }

                    throw new Exception("Win32 mutex creation failed.");
                }
            }
            finally
            {
                sa.Close();
            }
        }

        /// <summary>
        /// Constructor.  Specify whether the mutex it to be owned initially
        /// by the caller as well as the mutex's name.
        /// </summary>
        /// <param name="requestedState">The requsted initial state of the mutex (<c>true</c> if this is to be owned by the caller).</param>
        /// <param name="name">Name of the mutex.</param>
        /// <param name="actualState">Set to <c>true</c> if ownership of the mutex was granted to the caller.</param>
        public GlobalMutex(string name, bool requestedState, out bool actualState)
        {
            SecurityAttributes  sa = new SecurityAttributes(SecurityAccess.Unrestricted);
            int                 err;

            if (name.Length > 128)
                throw new ArgumentException("Name exceeds 128 characters.", "name");

            if (name.IndexOfAny(new char[] { '/', '\\' }) != -1)
                throw new ArgumentException("Name may not include forward or backslashes.");

            this.name = name;

            actualState = false;
            try
            {
                // Here's what the name abbreviations mean:
                //
                //      LT = LillTek
                //      EV = Event

                hMutex = WinApi.CreateMutex(sa.AttributesPtr, requestedState, @"Global\LT:MX:" + name);
                err    = WinApi.GetLastError();
                if (hMutex == IntPtr.Zero)
                    throw new Exception("Win32 mutex creation failed.");

                if (requestedState)
                    actualState = err != WinApi.ERROR_ALREADY_EXISTS;
            }
            finally
            {
                sa.Close();
            }
        }

        /// <summary>
        /// This finalizer releases all unmanaged resources, but well behaved applications should
        /// call <see cref="Close" /> immediately after they're done with the object.
        /// </summary>
        ~GlobalMutex()
        {
            Close();
        }

        /// <summary>
        /// Returns the name of the mutex.
        /// </summary>
        public string Name
        {
            get { return name; }
        }

        /// <summary>
        /// This method releases all unmanaged resources associated with the mutex.  Well
        /// behaved applications will call this immediately after they are done with the
        /// mutex.
        /// </summary>
        public void Close()
        {
            if (hMutex != IntPtr.Zero)
            {
                WinApi.CloseHandle(hMutex);
                hMutex = IntPtr.Zero;
            }

            GC.SuppressFinalize(this);
        }

        /// <summary>
        /// This method waits indefinitely until it is able to acquire the mutex.
        /// </summary>
        /// <returns><c>true</c> if the mutex was acquired.</returns>
        public bool WaitOne()
        {
            return WinApi.WaitForSingleObject(hMutex, WinApi.INFINITE) == WinApi.WAIT_OBJECT_0;
        }

        /// <summary>
        /// This method waits up to a specified number of milliseconds to acquire
        /// the mutex.
        /// </summary>
        /// <param name="timeout">The time to wait in milliseconds or <b>-1</b> to wait indfinitely.</param>
        /// <returns><c>true</c> if the mutex was acquired.</returns>
        public bool WaitOne(int timeout)
        {
            return WinApi.WaitForSingleObject(hMutex, (uint)timeout) == WinApi.WAIT_OBJECT_0;
        }

        /// <summary>
        /// The thread that owns the mutex calls this method to release it.  Note that
        /// each call to <see cref="WaitOne()" /> or the constructor (with initiallyOwned=true) must 
        /// be matched with a call to ReleaseMutex().
        /// </summary>
        public void ReleaseMutex()
        {
            WinApi.ReleaseMutex(hMutex);
        }
    }
}
