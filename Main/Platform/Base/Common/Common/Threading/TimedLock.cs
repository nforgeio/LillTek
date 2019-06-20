//-----------------------------------------------------------------------------
// FILE:        TimedLock.cs
// CONTRIBUTOR: Jeff Lill
// COPYRIGHT:   Copyright (c) 2005-2015 by Jeffrey Lill.  All rights reserved.
// DESCRIPTION: Implements an alternative to the C# lock statement that
//              will throw a DeadlockException if the lock cannot be acquired.

// $todo(jeff.lill): 
//
// There appears to be some problems with this
// so I'm disabling this for now.

#undef LEAK_DETECTOR    // Define this to enable leak detection

using System;
using System.Text;
using System.Threading;
using System.Diagnostics;
using System.Collections;
using System.Collections.Generic;
using System.Runtime.InteropServices;

#if !MOBILE_DEVICE
using LillTek.Windows;
#endif

namespace LillTek.Common
{
    /// <summary>
    /// Implements an alternative to the C# <c>lock</c> statement that provides a nice way of 
    /// obtaining a lock that will time out with a cleaner syntax than using 
    /// <see cref="Monitor.TryEnter(object,TimeSpan)" /> directly.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Although the C# <c>lock</c> statement is a an easy way to implement thread
    /// synchronization for multi-threaded applications, its use can be problematic
    /// for production applications.  The main problem is that <c>lock</c> waits
    /// indefinitely to obtain the lock.  If the application experiences a deadlock
    /// then all of the threads waiting for locks will hang without producing
    /// and diagnostic information or giving the application a chance to recover.
    /// </para>
    /// <para>
    /// The <see cref="TimedLock" /> class addresses this problem by providing an
    /// easy way to introduce time limited locking into an application.  The class
    /// attempts to obtain a lock on an instance just like the <c>lock</c> statement
    /// does, but <see cref="TimedLock" /> waits for a default or explicit time
    /// period before throwing a <see cref="DeadlockException" />.  Use of the
    /// <see cref="TimedLock" /> class assumes that legitimate locks will never
    /// be held as long as the time period selected.  This means that locks
    /// should never be held while an operation of indeterminate length is
    /// performed (holding locks for this long is bad programming practice anyway).
    /// </para>
    /// <para>
    /// This class is designed to use the C# <b>using</b> statement with a call to the 
    /// static <see cref="TimedLock.Lock(object)" />, <see cref="TimedLock.Lock(object,TimeSpan)" />, 
    /// or <see cref="TimedLock.Lock(object,int)" /> methods.  The first override waits 
    /// a maximum time specified by <see cref="DefaultTimeout" />.  The other two
    /// overrides accept a timeout parameter.  The idea here is to make it easy to
    /// use search and replace to modify existing code.  Here's an example:
    /// </para>
    /// <code language="cs">
    /// // Translate this:
    /// 
    /// lock (obj) {
    /// 
    ///     //Thread safe operation
    /// }
    /// 
    /// // to this:
    /// 
    /// using (TimedLock.Lock(obj)) {
    /// 
    ///		//Thread safe operations
    /// }
    /// </code>
    /// <para>
    /// Note that this class implements deadlock detection by waiting only a limited
    /// amount of time to acquire the lock on an instance before throwning a
    /// <see cref="DeadlockException" />.  By default, this exception will include
    /// only rudamentary information about the deadlock (ie. where it was detected).
    /// Much more extensive information can be collected if the lock target
    /// instance implements the <see cref="ILockable" /> interface and 
    /// the application sets <see cref="FullDiagnostics" /><c>=true</c>.
    /// The diagnostics collected in this case include the call stack for the
    /// thread that failed to obtain the lock, the thread and call stack
    /// where the lock was obtained, and the locks and call stacks for any
    /// other locks owned by either of the threads.
    /// </para>
    /// <note>
    /// <see cref="FullDiagnostics" /> defaults to <c>true</c> for WINFULL/DEBUG builds
    /// and to <c>false</c> for all other build configurations.
    /// </note>
    /// <para>
    /// The <see cref="Helper.InitializeApp" /> and <see cref="Helper.InitializeWebApp" /> methods
    /// look in the application's configuration file for settings to initialize
    /// this class' <see cref="DefaultTimeout" />, <see cref="FullDiagnostics" />,
    /// and <see cref="LockableWarning" /> settings. 
    /// </para>
    /// <para>
    /// <see cref="DefaultTimeout" /> will be loaded as a timespan from <b>Diagnostics.TimedLock.Timeout</b>,
    /// <see cref="FullDiagnostics" /> will be loaded as a boolean from <b>Diagnostics.TimedLock.FullDiagnostics</b>,
    /// and <see cref="LockableWarning" /> will be loaded as a boolean from <b>Diagnostics.TimedLock.LockableWarning.</b>
    /// </para>
    /// <para>
    /// This class is adapted from Ian Griffiths <a href="http://www.interact-sw.co.uk/iangblog/2004/03/23/locking">article</a>  
    /// and incorporating suggestions by Marek Malowidzki as outlined in this 
    /// <a href="http://www.interact-sw.co.uk/iangblog/2004/05/12/timedlockstacktrace">blog post</a>,
    /// along with my own diagnostic related enhancements.
    /// </para>
    /// </remarks>
    public struct TimedLock : IDisposable
    {
        //---------------------------------------------------------------------
        // Private types

        /// <summary>
        /// Used internally by <see cref="TimedLock" /> and <see cref="DeadlockException" />
        /// to track locks for deadlock detection.
        /// </summary>
        internal sealed class LockInfo
        {
            /// <summary>
            /// The lock target.
            /// </summary>
            public object Target;

            /// <summary>
            /// Stack where the lock was acquired.
            /// </summary>
            public CallStack Stack;

            /// <summary>
            /// Nested lock reference count.
            /// </summary>
            public int LockCount;

            /// <summary>
            /// Native Windows thread ID of the thread holding the lock (or 0).
            /// </summary>
            public int NativeThreadID = 0;

            /// <summary>
            /// Managed thread ID of the thread holding the lock (or 0).
            /// </summary>
            public int ManagedThreadID = 0;

            /// <summary>
            /// Native Windows thread ID of the thread that could not acquire
            /// the lock (or 0).
            /// </summary>
            public int FailNativeThreadID = 0;

            /// <summary>
            /// Managed thread ID  of the thread that could not acquire
            /// the lock (or 0).
            /// </summary>
            public int FailManagedThreadID = 0;

            /// <summary>
            /// List of the locks held by the thread that holds the lock (or <c>null</c>).
            /// </summary>
            public List<LockInfo> Locks = null;

            /// <summary>
            /// The call stack at the point where the lock attempted failed (or <c>null</c>).
            /// </summary>
            public CallStack FailStack = null;

            /// <summary>
            /// List of locks held by the thread that could not acquire the lock (or <c>null</c>).
            /// </summary>
            public List<LockInfo> FailLocks = null;

            /// <summary>
            /// Default constructor.
            /// </summary>
            public LockInfo()
            {
            }

            /// <summary>
            /// Constructor.
            /// </summary>
            /// <param name="target">The lock target instance.</param>
            /// <param name="stack">The stack when the lock was acquired.</param>
            public LockInfo(object target, CallStack stack)
            {
                this.Target          = target;
                this.Stack           = stack;
                this.LockCount       = 1;
                this.NativeThreadID  = GetCurrentThreadId();
                this.ManagedThreadID = Thread.CurrentThread.ManagedThreadId;
            }

            /// <summary>
            /// Creates a shallow clone of the instance.
            /// </summary>
            /// <returns>The clone.</returns>
            public LockInfo Clone()
            {
                var clone = new LockInfo();

                clone.Target              = this.Target;
                clone.Stack               = this.Stack;
                clone.LockCount           = this.LockCount;
                clone.NativeThreadID      = this.NativeThreadID;
                clone.ManagedThreadID     = this.ManagedThreadID;
                clone.FailNativeThreadID  = this.FailNativeThreadID;
                clone.FailManagedThreadID = this.FailNativeThreadID;
                clone.Locks               = this.Locks;
                clone.FailStack           = this.FailStack;
                clone.FailLocks           = this.FailLocks;

                return clone;
            }

            /// <summary>
            /// Generates a string with formatted diagnostic information about
            /// the lock attempt.
            /// </summary>
            /// <returns>The formatted string.</returns>
            public string Dump()
            {
                var sb = new StringBuilder(2048);

                sb.AppendFormat("TimedLock: Timeout locking [{0}].\r\n", Target.GetType().FullName);
                sb.AppendFormat("Current [ThreadID={0}], Current Stack:\r\n", FailNativeThreadID);
                sb.AppendLine();

                if (FailStack == null)
                    sb.Append("Not available\r\n\r\n");
                else
                {
                    sb.Append(FailStack.ToString());
                    sb.AppendLine();
                }

                sb.AppendFormat("Lock on [{0}] acquired by [ThreadID={1}] at:\r\n", Target.GetType().FullName, NativeThreadID);
                sb.AppendLine();
                sb.Append(Stack.ToString());
                sb.AppendLine();

                if (Locks == null)
                    sb.AppendFormat("Other locks held by [ThreadID={0}]: Not available\r\n", NativeThreadID);
                else
                {
                    if (Locks.Count <= 1)
                        sb.AppendFormat("Other locks held by [ThreadID={0}]: None\r\n", NativeThreadID);
                    else
                    {
                        sb.AppendFormat("Other locks held by [ThreadID={0}]:\r\n", NativeThreadID);

                        foreach (LockInfo info in Locks)
                        {
                            if (object.ReferenceEquals(info.Target, this.Target))
                                continue;

                            sb.AppendLine();
                            sb.AppendFormat("Lock on [{0}] acquired at:\r\n\r\n", info.Target.GetType().FullName);
                            sb.Append(info.Stack.ToString());
                            sb.AppendLine();
                            sb.AppendLine();
                        }
                    }
                }

                if (this.FailLocks == null)
                    sb.AppendFormat("Other locks held by [ThreadID={0}]: Not available\r\n", FailNativeThreadID);
                else
                {
                    if (FailLocks.Count == 0)
                        sb.AppendFormat("Other locks held by [ThreadID={0}]: None\r\n", FailNativeThreadID);
                    else
                    {
                        sb.AppendFormat("Other locks held by [ThreadID={0}]:\r\n", FailNativeThreadID);

                        foreach (LockInfo info in FailLocks)
                        {
                            sb.AppendLine();
                            sb.AppendFormat("Lock on [{0}] acquired at:\r\n\r\n", info.Target.GetType().FullName);
                            sb.Append(info.Stack.ToString());
                            sb.AppendLine();
                            sb.AppendLine();
                        }
                    }
                }

                return sb.ToString();
            }
        }

        //---------------------------------------------------------------------
        // Implementation

#if LEAK_DETECTOR
        // (In Debug mode, we make it a class so that we can add a finalizer
        // in order to detect when the object is not freed.)
        private class Sentinel {

            private TimedLock   tLock;

            public Sentinel(TimedLock tLock) {

                this.tLock = tLock;
            }

            ~Sentinel() {

                // If this finalizer runs, someone somewhere failed to
                // call Dispose, which means we've failed to leave
                // a monitor!

                // throw new UndisposedLockException(tLock);
            }

            /// <summary>
            /// Supresses finalization of the instance.
            /// </summary>
            public void SuppressFinalize() {

                GC.SuppressFinalize(this);
            }
        }
#endif // LEAK_DETECTOR

#if DEBUG
        // The default lock acquisition timeout.

        private static TimeSpan defTimeout = TimeSpan.FromSeconds(1 * 60);
#else
        private static TimeSpan defTimeout = TimeSpan.FromSeconds(1*60);
#endif

        // Used for generating unique target lock keys

#if !MOBILE_DEVICE
        private static long nextLockKey = 0;
#else
        private static int  nextLockKey = 0;
#endif

        // Table of current lock information including the call stack where the
        // lock was originally acquired keyed by the target object's ILockable.GetLockKey().

        private static Dictionary<object, LockInfo> locks = new Dictionary<object, LockInfo>();

        // Table of fully qualified type names that have already been logged,
        // warning that the type doesn't implement ILockable.

        private static Dictionary<string, bool> prevWarnings = new Dictionary<string, bool>();

#if DEBUG && WINFULL

        // Set this to a positive value in DEBUG builds to enable checking
        // for code that holds a lock for an excessive period of time (milliseconds).

        private static int excessiveHoldTime = 0;

        private static bool fullDiagnostics  = true;
        private static bool lockableWarning  = true;
#else
        private static bool                         fullDiagnostics   = false;
        private static bool                         lockableWarning   = false;
#endif
        private object target;
#if LEAK_DETECTOR
        private Sentinel leakDetector;
#endif

        /// <summary>
        /// Returns the current thread ID if running on Windows,
        /// <b>0</b> if running on Unix/Linux/Mobile Device.
        /// </summary>
        private static int GetCurrentThreadId()
        {
#if MOBILE_DEVICE
            return 0;
#else
            if (Helper.IsWindows)
                return WinApi.GetCurrentThreadId();
            else
                return 0;
#endif
        }

        /// <summary>
        /// The global default timeout for all TimedLock instances.
        /// This defaults to 1 minute.
        /// </summary>
        public static TimeSpan DefaultTimeout
        {
            get { return defTimeout; }
            set { defTimeout = value; }
        }

        /// <summary>
        /// Specifies whether <see cref="TimedLock" />s should keep track of the call stack
        /// where the locks were acquired so that useful diagnostic information
        /// can be included in thrown <see cref="DeadlockException" />s.  Note that this is
        /// set to <c>true</c> by default for DEBUG builds and <c>false</c> for release
        /// builds.
        /// </summary>
        public static bool FullDiagnostics
        {
            get { return fullDiagnostics; }
            set { fullDiagnostics = value; }
        }

        /// <summary>
        /// Indicates whether calls to <see cref="TimedLock.Lock(object)" />
        /// with lock targets that do not implement <see cref="ILockable" />
        /// should be logged as warnings.  Note that this is set to <c>true</c> 
        /// by default for DEBUG builds and <c>false</c> for release builds.
        /// </summary>
        /// <remarks>
        /// <see cref="FullDiagnostics" /> must also be set to <c>true</c>
        /// for warnings to be logged.
        /// </remarks>
        public static bool LockableWarning
        {
            get { return lockableWarning; }
            set { lockableWarning = value; }
        }

        /// <summary>
        /// For DEBUG builds, this method verifies that a lock on an object is 
        /// currently held by the current thread, throwing an <see cref="AssertException" />
        /// if this is not the case.
        /// </summary>
        /// <remarks>
        /// <note>
        /// This method works only if <see cref="FullDiagnostics" /> is
        /// enabled, <b>target</b> implements <see cref="ILockable" /> and
        /// only for DEBUG builds.  The method does nothing if any of these
        /// conditions is not true.
        /// </note>
        /// <note>
        /// This method works only when running on Windows.  It does nothing
        /// whgen running on Unix/Linux.
        /// </note>
        /// </remarks>
        [Conditional("DEBUG")]
        public static void AssertLocked(object target)
        {
            ILockable lockable;

            if (!fullDiagnostics || !Helper.IsWindows)
                return;

            lockable = target as ILockable;
            if (lockable == null)
                return;

            lock (locks)
            {
                LockInfo info;

#if !MOBILE_DEVICE
                if (!locks.TryGetValue(lockable.GetLockKey(), out info) ||
                    info.NativeThreadID != WinApi.GetCurrentThreadId())
                {
                    Assertion.Fail("Lock not held by this thread.");
                }
#else
                if (!locks.TryGetValue(lockable.GetLockKey(),out info))
                    Assertion.Fail("Lock not held by this thread.");
#endif
            }
        }

        /// <summary>
        /// Attempts to obtain a lock on an object for the default timeout.
        /// </summary>
        /// <param name="target">The object whose lock is to be acquired.</param>
        public static TimedLock Lock(object target)
        {
            var tLock = new TimedLock(target);

            tLock._Lock(target, defTimeout);
            return tLock;
        }

        /// <summary>
        /// Attempts to obtain a lock on an object for the specified timeout.
        /// </summary>
        /// <param name="target">The object whose lock is to be acquired.</param>
        /// <param name="timeout">The maximum time to wait.</param>
        public static TimedLock Lock(object target, TimeSpan timeout)
        {
            var tLock = new TimedLock(target);

            tLock._Lock(target, timeout);
            return tLock;
        }

        /// <summary>
        /// Attempts to obtain a lock on an object for the specified timeout.
        /// </summary>
        /// <param name="target">The object whose lock is to be acquired.</param>
        /// <param name="milliseconds">The maximum time to wait in milliseconds.</param>
        public static TimedLock Lock(object target, int milliseconds)
        {
            var tLock = new TimedLock(target);

            tLock._Lock(target, TimeSpan.FromMilliseconds(milliseconds));
            return tLock;
        }

        /// <summary>
        /// Intializes the TimedLock instance.
        /// </summary>
        /// <param name="target">The object whose lock is to be acquired.</param>
        private TimedLock(object target)
        {
            this.target = target;
#if LEAK_DETECTOR
            this.leakDetector = null;
            this.leakDetector = new Sentinel(this);
#endif
        }

        /// <summary>
        /// Adds additional information about the current thread (the thread that
        /// could not acquire the lock) and other locks held by the current thread and the
        /// thread that did acquire the lock to the <see cref="LockInfo" /> instance
        /// so that errors can be reported.
        /// </summary>
        /// <param name="info">The source lock information.</param>
        /// <param name="curStack">The current call stack.</param>
        /// <returns>A cloned version of the source with the additonal information.</returns>
        private LockInfo GetDiagnosticInfo(LockInfo info, CallStack curStack)
        {
            info                     = info.Clone();
            info.FailNativeThreadID  = GetCurrentThreadId();
            info.FailManagedThreadID = Thread.CurrentThread.ManagedThreadId;
            info.FailStack           = curStack;
            info.Locks               = new List<LockInfo>();
            info.FailLocks           = new List<LockInfo>();

            foreach (LockInfo l in locks.Values)
            {
                if (l.ManagedThreadID == info.ManagedThreadID)
                    info.Locks.Add(l);
                else if (l.ManagedThreadID == info.FailManagedThreadID)
                    info.FailLocks.Add(l);
            }

            return info;
        }

        /// <summary>
        /// Dumps information about a possible deadlock to the debugger
        /// output and then breaks into the debugger it one is attached.
        /// </summary>
        /// <param name="target">The lock target.</param>
        /// <param name="info">Information about the thread holding the lock (or <c>null</c>).</param>
        private void DumpAndBreak(object target, LockInfo info)
        {
            if (Debugger.IsAttached)
            {
                if (info != null)
                {
                    Debug.WriteLine("");
                    Debug.WriteLine(info.Dump());
                }

                Debugger.Break();
            }
        }

        /// <summary>
        /// Attempts to obtain a lock on an object with the specified timeout.
        /// </summary>
        /// <param name="target">The object whose lock is to be acquired.</param>
        /// <param name="timeout">The maximum time to wait.</param>
        private void _Lock(object target, TimeSpan timeout)
        {
            if (fullDiagnostics && lockableWarning && target as ILockable == null)
            {
                string  typeName = target.GetType().FullName;
                bool    warn;

                lock (locks)
                {
                    warn = !prevWarnings.ContainsKey(typeName);
                    if (warn)
                        prevWarnings.Add(typeName, true);
                }

                if (warn)
                    SysLog.LogWarning("Type [{0}] does not implement [ILockable] at:\r\n\r\n" + new CallStack(2, true).ToString(), typeName);
            }

#if !WINFULL

            // There appears to be some problems with [Monitor.TryEnter()] on Windows Phone and possibly
            // also on Silverlight where exclusive access cannot be obtained sometimes when there
            // are no other locks.  Simply replacing [TryEnter()] with [Enter()] fixes the problem and
            // since we were using [TryEnter()] only for enhanced diagnostics, removing this will
            // really doesn't change the programming model.  The only difference will be that WINFULL
            // builds will throw a [DeadlockException] when the lock timeout has been exceeded whereas
            // SILVERLIGHT and MOBILE_DEVICE builds will simply block forever in [Enter()]
            // for deadlocks.

            Monitor.Enter(target);
#else
            if (!Monitor.TryEnter(target, timeout))
            {
                var failStack = new CallStack(2, true);

#if LEAK_DETECTOR
                leakDetector.SuppressFinalize();
#endif
                if (fullDiagnostics)
                {
                    var lockable = target as ILockable;

                    if (lockable == null)
                    {
                        DumpAndBreak(target, null);
                        throw new DeadlockException(this, failStack, null, "#1");
                    }

                    lock (locks)
                    {
                        LockInfo info;

                        if (locks.TryGetValue(lockable.GetLockKey(), out info))
                        {
                            var diagnostics = GetDiagnosticInfo(info, failStack);

                            DumpAndBreak(target, diagnostics);
                            throw new DeadlockException(this, failStack, diagnostics, "#2");
                        }
                        else
                        {
                            DumpAndBreak(target, null);
                            throw new DeadlockException(this, failStack, null, "#3");
                        }
                    }
                }
                else
                {
                    DumpAndBreak(target, null);
                    throw new DeadlockException(this, failStack, null, "#4");
                }
            }
            else
            {
#if DEBUG
                var lockTime = DateTime.MinValue;

                if (excessiveHoldTime > 0)
                    lockTime = SysTime.Now;
#endif
                if (fullDiagnostics)
                {
                    lock (locks)
                    {
                        var lockable = target as ILockable;

                        if (lockable != null)
                        {
                            object lockKey = lockable.GetLockKey();
                            LockInfo info;

                            if (!locks.TryGetValue(lockKey, out info))
                                locks.Add(lockKey, new LockInfo(target, new CallStack(2, true)));
                            else
                                info.LockCount++;
                        }
                    }
                }
#if DEBUG
                if (excessiveHoldTime > 0 && SysTime.Now - lockTime >= TimeSpan.FromMilliseconds(excessiveHoldTime))
                    SysLog.LogErrorStackDump("Warning: Lock held for an excessive period of time: {0}ms", (SysTime.Now - lockTime).TotalMilliseconds);
#endif
            }
#endif // WINFULL
        }

        /// <summary>
        /// Releases the lock.
        /// </summary>
        public void Dispose()
        {
#if LEAK_DETECTOR
            // It's a bad error if someone forgets to call Dispose,
            // so in Debug builds, we put a finalizer in to detect
            // the error. If Dispose is called, we suppress the
            // finalizer.

            leakDetector.SuppressFinalize();
#endif
            try
            {
                if (fullDiagnostics)
                {
                    lock (locks)
                    {
                        ILockable   lockable = target as ILockable;
                        object      lockKey;
                        LockInfo    info;

                        if (lockable != null)
                        {

                            lockKey = lockable.GetLockKey();
                            if (locks.TryGetValue(lockKey, out info) && --info.LockCount <= 0)
                                locks.Remove(lockKey);
                        }
                    }
                }
            }
            finally
            {
                Monitor.Exit(target);
            }
        }

        /// <summary>
        /// Returns the lock target instance.
        /// </summary>
        public object Target
        {
            get { return target; }
        }

        /// <summary>
        /// Allocates and returns a process unique lock key to be used by
        /// <see cref="ILockable" /> implementations.
        /// </summary>
        /// <returns>The lock key.</returns>
        public static object AllocLockKey()
        {
            return Interlocked.Increment(ref nextLockKey).ToString();
        }
    }
}
