//-----------------------------------------------------------------------------
// FILE:        SharedMem.cs
// CONTRIBUTOR: Jeff Lill
// COPYRIGHT:   Copyright (c) 2005-2015 by Jeffrey Lill.  All rights reserved.
// DESCRIPTION: Defines the SharedMem class that can be used to share a block 
//              of memory across one or more processes.

#if WINCE

// Some Windows/CE implementations that don't implement demand paging also
// don't implement the CreateFileMapping() API that I use for implementing
// shared memory.  In these cases, I've implemented a shared memory device
// driver.  These functions are accessed via the WinApi.MEM_xxx APIs.
// Define the SHAREDMEM_DRIVER macro to enable this access.

#undef SHAREDMEM_DRIVER

#endif // WINCE

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
    /// The SharedMem class provides access to a shared block of memory using
    /// a low-level Windows API.  Shared memory blocks are identified by name
    /// and are protected by a Mutex.  These blocks can be accessed across both
    /// managed and unmanaged processes.
    /// </summary>
    public unsafe class SharedMem
    {
        //---------------------------------------------------------------------
        // Instance variables

        private object          syncLock = new object();
        private IntPtr          m_hMap;         // Handle of the file mapping object (or NULL)
        private void*           m_pvBlock;      // Pointer to the mapped block (or NULL)
        private int             m_cLock;        // Lock count
        private int             m_cbBlock;      // Size of the shared block
        private GlobalMutex     m_mutex;        // Global mutex

        //---------------------------------------------------------------------
        // Implementation

        /// <summary>
        /// Describes how to open a shared memory block.
        /// </summary>
        public enum OpenMode
        {
            /// <summary>
            /// Open the shared memory block if it already exists, otherwise create it.
            /// </summary>
            CREATE_OPEN,

            /// <summary>
            /// Create the shared memory block.
            /// </summary>
            CREATE_ONLY,

            /// <summary>
            /// Open the shared memory block.
            /// </summary>
            OPEN_ONLY
        }

        /// <summary>
        /// Default constructor.
        /// </summary>
        public SharedMem()
        {
            m_hMap    = IntPtr.Zero;
            m_pvBlock = null;
            m_cLock   = 0;
            m_cbBlock = 0;
            m_mutex   = null;
        }

        /// <summary>
        /// Finalizer.
        /// </summary>
        ~SharedMem()
        {
            Close();
        }

        /// <summary>
        /// This property returns <c>true</c> if the memory block is open.
        /// </summary>
        public bool IsOpen
        {
            get { return m_hMap != IntPtr.Zero; }
        }

        /// <summary>
        /// This method creates a shared memory object with the name passed in pszName
        /// and size of cbMem.  The method returns <c>true</c> if the memory was created 
        /// successfully, <c>false</c> if the operation failed for some reason.  If the shared 
        /// memory block is being created for the first time then this method will 
        /// zero the memory block.  If the shared memory block has already been created
        /// then this method will link to the existing block.  Note that any successfull 
        /// call to Open() needs to be matched by a call to <see cref="Close" />.  This method is
        /// threadsafe.
        /// </summary>
        /// <param name="name">
        /// Name of the inbox.  This can be a maximum of 128 characters and may
        /// not include the backslash (\) character.
        /// </param>
        /// <param name="cbMem">Size of the memory in bytes.</param>
        /// <param name="mode">The opening mode.</param>
        public void Open(string name, int cbMem, OpenMode mode)
        {
            string      memName;
            string      mutexName;
            bool        fExists;
            bool        createdNew;

            if (name.Length > 128)
                throw new ArgumentException("Name exceeds 128 characters.", "name");

            if (name.IndexOfAny(new char[] { '/', '\\' }) != -1)
                throw new ArgumentException("Name may not include forward or backslashes.");

            lock (syncLock)
            {
                Assertion.Test(m_hMap == IntPtr.Zero);

                // Here's what the abbreviations mean:
                //
                //      LT  = LillTek
                //      SM  = SharedMem

                memName = @"Global\LT:SM:" + name;

#if SHAREDMEM_DRIVER
                bool    fCreated;

                m_hMap  = WinApi.MEM_Open(memName,cbMem,out fCreated);
                fExists = !fCreated;
#else
                // Create the memory object

                SecurityAttributes sa;

                sa = new SecurityAttributes(SecurityAccess.Unrestricted);

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

                retry:

                    m_hMap = WinApi.CreateFileMapping(new IntPtr(-1), sa.AttributesPtr, WinApi.PAGE_READWRITE, 0, (uint)cbMem, memName);
                    if (m_hMap == IntPtr.Zero)
                    {
                        int error = WinApi.GetLastError();

                        if (memName.ToLowerInvariant().StartsWith(@"global\") && error == WinErr.ERROR_ACCESS_DENIED)
                        {
                            memName = "LT:SM:" + name;
                            goto retry;
                        }

                        throw new InvalidOperationException(string.Format(null, "Failed on Windows error [{0}].", error));
                    }

                    // For some weird reason, Marshal.GetLastWin32Error() is returning ERROR_IO_PENDING
                    // when CreateFileMapping() is called on an exising block of shared memory instead
                    // of returning ERROR_ALREADY_EXISTS.  So I'm going to call GetLastError() directly.
                    // This will be a bit of a performance hit but Open() will be called infrequently
                    // in real applications.

                    fExists = WinApi.GetLastError() == WinApi.ERROR_ALREADY_EXISTS;
                }
                finally
                {
                    sa.Close();
                }

#endif // SHAREDMEM_DRIVER

                m_pvBlock = null;
                m_cbBlock = cbMem;
                m_cLock   = 0;

                if (!fExists && mode == OpenMode.OPEN_ONLY)
                {
#if SHAREDMEM_DRIVER
                    WinApi.MEM_Close(m_hMap);
#else
                    WinApi.CloseHandle(m_hMap);
#endif
                    m_hMap = IntPtr.Zero;
                    throw new InvalidOperationException("Shared memory does not exist.");
                }
                else if (fExists && mode == OpenMode.CREATE_ONLY)
                {
#if SHAREDMEM_DRIVER
                    WinApi.MEM_Close(m_hMap);
#else
                    WinApi.CloseHandle(m_hMap);
#endif
                    m_hMap = IntPtr.Zero;
                    throw new InvalidOperationException("Shared memory already exists.");
                }

                // Here's what the abbreviations mean:
                //
                //      LT = LillTek
                //      SM = Shared Memory
                //      MX = Mutex

                mutexName = "LT:SM:MX:" + name;
                if (fExists)
                {
                    try
                    {
                        // Open the mutex

                        m_mutex = new GlobalMutex(mutexName);
                    }
                    catch
                    {
#if SHAREDMEM_DRIVER
                        WinApi.MEM_Close(m_hMap);
#else
                        WinApi.CloseHandle(m_hMap);
#endif
                        m_hMap = IntPtr.Zero;
                        throw;
                    }
                }
                else
                {
                    try
                    {
                        // Create the mutex

                        m_mutex = new GlobalMutex(mutexName, true, out createdNew);
                        if (createdNew)
                        {
                            // Map the shared memory and zero it.

                            byte*   p;

                            p = Lock();
                            for (int i = 0; i < m_cbBlock; p[i++] = 0) ;
                            Unlock();

                            m_mutex.ReleaseMutex();
                        }
                    }
                    catch
                    {
#if SHAREDMEM_DRIVER
                        WinApi.MEM_Close(m_hMap);
#else
                        WinApi.CloseHandle(m_hMap);
#endif
                        m_hMap = IntPtr.Zero;
                        throw;
                    }
                }
            }
        }

        /// <summary>
        /// This method disconnects the class from the shared memory block and if this
        /// is the last instance referencing the block, then the block will be
        /// deleted.  Note that it's OK to call Close() even if the object isn't
        /// currently open.  This method is thread safe.
        /// </summary>
        public void Close()
        {
            lock (syncLock)
            {
                if (m_hMap == IntPtr.Zero)
                    return;

                // Unlock the memory

                if (m_pvBlock != null)
                    while (m_cLock > 0)
                        Unlock();

                // Free the map

                if (m_hMap != IntPtr.Zero)
                {
#if SHAREDMEM_DRIVER
                    WinApi.MEM_Close(m_hMap);
#else
                    WinApi.CloseHandle(m_hMap);
#endif
                    m_hMap = IntPtr.Zero;
                }

                // Free the mutex

                m_mutex.Close();
            }
        }

        /// <summary>
        /// This property returns the size of the shared memory block in bytes.
        /// This property is threadsafe.
        /// </summary>
        public int Size
        {
            get { lock (syncLock) return m_cbBlock; }
        }

        /// <summary>
        /// This method locks the shared memory block by gaining exclusive access to
        /// it and returning its pointer (as mapped into the current process space).
        /// fWait determines how the method reacts if the block is already locked by
        /// another thread or process.  The method returns null if the object no longer
        /// references a valid shared memory block.  This method is threadsafe.
        /// </summary>
        /// <param name="fWait">
        /// If fWait is true then the method will block until the memory is unlocked 
        /// by the other process.  If fWait is false then the method will return NULL 
        /// without waiting.
        /// </param>
        /// <returns>
        /// A pointer to the shared memory block or <c>null</c> if the memory block is no
        /// longer open or exclusive access to it could not be obtained.
        /// </returns>
        public byte* Lock(bool fWait)
        {
            if (m_hMap == IntPtr.Zero || m_mutex == null)
                return null;

            if (fWait)
                m_mutex.WaitOne();
            else
                if (!m_mutex.WaitOne(0))
                    return null;

            if (m_cLock == 0)
            {
#if SHAREDMEM_DRIVER
                m_pvBlock = WinApi.MEM_Lock(m_hMap,m_cbBlock);
#else
                m_pvBlock = WinApi.MapViewOfFile(m_hMap, WinApi.FILE_MAP_READ | WinApi.FILE_MAP_WRITE, 0, 0, 0);
#endif
                if (m_pvBlock != null)
                    m_cLock++;

                return (byte*)m_pvBlock;
            }
            else
            {
                m_cLock++;
                Assertion.Test(m_pvBlock != null);
                return (byte*)m_pvBlock;
            }
        }

        /// <summary>
        /// This method locks the shared memory block by gaining exclusive access to
        /// it and returning its pointer (as mapped into the current process space).
        /// This method is threadsafe.
        /// </summary>
        /// <returns>
        /// A pointer to the shared memory block or <c>null</c> if the memory block is no
        /// longer open or exclusive access to it could not be obtained.
        /// </returns>
        public byte* Lock()
        {
            return Lock(true);
        }

        /// <summary>
        /// This method releases the lock on the shared memory block.  This method
        /// is threadsafe.
        /// </summary>
        public void Unlock()
        {
            Assertion.Test(m_hMap != IntPtr.Zero);
            Assertion.Test(m_mutex != null);
            Assertion.Test(m_cLock != 0);
            Assertion.Test(m_pvBlock != null);

            if (m_cLock == 0)
                return;

            m_cLock--;
            if (m_cLock == 0)
            {
#if SHAREDMEM_DRIVER
                WinApi.MEM_Unlock(m_hMap,m_cbBlock);
#else
                WinApi.UnmapViewOfFile(m_pvBlock);
#endif
            }

            m_mutex.ReleaseMutex();
        }
    }
}
