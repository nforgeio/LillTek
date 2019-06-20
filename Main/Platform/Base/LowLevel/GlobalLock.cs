//-----------------------------------------------------------------------------
// FILE:        GlobalLock.cs
// CONTRIBUTOR: Jeff Lill
// COPYRIGHT:   Copyright (c) 2005-2015 by Jeffrey Lill.  All rights reserved.
// DESCRIPTION: Implements a mechanism for ensuring exclusive access to a named
//              resource across processes.

using System;
using System.Diagnostics;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Threading;

using LillTek.Common;

namespace LillTek.LowLevel
{
    /// <summary>
    /// Thrown when there's a problem acquiring an application lock.
    /// </summary>
    public sealed class GlobalLockException : ApplicationException
    {
        /// <summary>
        /// Constructs an instance from an inner exception.
        /// </summary>
        /// <param name="inner"></param>
        public GlobalLockException(Exception inner)
            : base(inner.Message, inner)
        {
        }

        /// <summary>
        /// Constructs an instance from a message string.
        /// </summary>
        /// <param name="message">The message text.</param>
        public GlobalLockException(string message)
            : base(message)
        {
        }

        /// <summary>
        /// Constructs and instance from a format string and arguments.
        /// </summary>
        /// <param name="format">The format string.</param>
        /// <param name="args">The message arguments.</param>
        public GlobalLockException(string format, params object[] args)
            : base(string.Format(format, args))
        {
        }
    }

    /// <summary>
    /// Implements a mechanism for ensuring exclusive access to a named resource 
    /// across processes.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Call <see cref="Lock" /> to attempt to acquire the application lock.  The
    /// method will return normally if the lock was acquired or throw a
    /// <see cref="GlobalLockException" /> if there was a problem.  Application
    /// locks are released by calling <see cref="Release" />.  The <see cref="IsHeld" />
    /// property returns <c>true</c> if the lock is currently held by the application.
    /// </para>
    /// <para>
    /// GlobalLock instances keep track of a lock count.  This is the number
    /// of times <see cref="Lock" /> has been called without a corresponding call
    /// to <see cref="Release" />.  <see cref="ReleaseAll" /> will force the
    /// release of the lock, regardless of the current lock count.
    /// </para>
    /// <para>
    /// Although this class could be used for controlling access to a resource
    /// within a single process, it is really intended for cross process use.
    /// Specifically, the class records the path of the application that acquired
    /// the lock so that this can be reported in exceptions thrown when the
    /// lock cannot be acquired.
    /// </para>
    /// </remarks>
    public class GlobalLock : IDisposable
    {
        private object      syncLock = new object();
        private SharedMem   initLock = null;
        private string      appName  = null;
        private int         cLock    = 0;

        /// <summary>
        /// Constructor.
        /// </summary>
        /// <param name="appName">The application name.</param>
        /// <remarks>
        /// Some attempt should be made to choose a reasonably unique application name
        /// to avoid conflicts with other applications that might be installed on the
        /// machine.  One technique would be to use GUIDs.  Another would be to use some
        /// sort of naming standard such as COMPANY.PRODUCT.EXECUTABLE.
        /// </remarks>
        public GlobalLock(string appName)
        {
            this.appName = appName;
        }

        /// <summary>
        /// Attempts to acquire the global application lock.
        /// </summary>
        /// <remarks>
        /// <note>
        /// <see cref="Release" /> should be called promptly when the
        /// application terminates to release the lock.
        /// </note>
        /// </remarks>
        /// <exception cref="GlobalLockException">Thrown when there's a problem acquiring the lock.</exception>
        public unsafe void Lock()
        {
            EnhancedMemoryStream    ms;
            byte*                   pMem;
            byte[]                  buf;
            Assembly                assembly;
            string                  path;

            lock (syncLock)
            {
                if (initLock != null)
                {
                    cLock++;    // The lock is already acquired
                    return;
                }

                try
                {
                    // Use a global shared memory block to enforce the lock.

                    assembly = Assembly.GetEntryAssembly();
                    if (assembly == null)
                        assembly = Assembly.GetCallingAssembly();

                    path = Helper.StripFileScheme(assembly.CodeBase);

                    ms = new EnhancedMemoryStream(4096);
                    ms.WriteString16(path);

                    initLock = new SharedMem();
                    initLock.Open("LT.Lock." + appName, 4096, SharedMem.OpenMode.CREATE_OPEN);
                    pMem = initLock.Lock();

                    if (pMem[0] != 0 || pMem[1] != 0)
                    {
                        buf = new byte[initLock.Size];
                        for (int i = 0; i < buf.Length; i++)
                            buf[i] = pMem[i];

                        ms = new EnhancedMemoryStream(buf);

                        initLock.Unlock();
                        initLock.Close();
                        initLock = null;

                        throw new GlobalLockException("Global lock is already acquired by [{0}].", ms.ReadString16());
                    }

                    buf = ms.ToArray();
                    for (int i = 0; i < buf.Length; i++)
                        pMem[i] = buf[i];

                    initLock.Unlock();
                }
                catch (Exception e)
                {
                    throw new GlobalLockException(e);
                }
            }

            cLock++;
        }

        /// <summary>
        /// Returns <c>true</c> if the instance holds the application lock.
        /// </summary>
        public bool IsHeld
        {
            get
            {
                lock (syncLock)
                    return initLock != null;
            }
        }

        /// <summary>
        /// Releases the application lock if the current instance holds it.
        /// </summary>
        public void Release()
        {
            lock (syncLock)
            {
                if (cLock == 0)
                    throw new GlobalLockException("Release() called with no corresponding Lock().");

                if (--cLock == 0)
                    ReleaseAll();
            }
        }

        /// <summary>
        /// Forces the lock release, regardless of the current lock count.
        /// </summary>
        /// <remarks>
        /// It is not an error to call this if the lock is not currently held.
        /// </remarks>
        public void ReleaseAll()
        {
            lock (syncLock)
            {
                if (initLock == null)
                    return;

                initLock.Close();
                initLock = null;
                cLock    = 0;
            }
        }

        /// <summary>
        /// Implement IDisposable.
        /// </summary>
        public void Dispose()
        {
            ReleaseAll();
            GC.SuppressFinalize(this);
        }

        /// <summary>
        /// Be sure to release the lock on finalization.
        /// </summary>
        ~GlobalLock()
        {
            ReleaseAll();
        }
    }
}
