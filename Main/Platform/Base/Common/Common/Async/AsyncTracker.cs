//-----------------------------------------------------------------------------
// FILE:        AsyncTracker.cs
// CONTRIBUTOR: Jeff Lill
// COPYRIGHT:   Copyright (c) 2005-2015 by Jeffrey Lill.  All rights reserved.
// DESCRIPTION: Implements a class for debugging pending async operations

using System;
using System.Text;
using System.Threading;
using System.Collections;
using System.Diagnostics;

namespace LillTek.Common
{
#if !MOBILE_DEVICE

#if DEBUG

    /// <summary>
    /// Holds the state of an async operation.
    /// </summary>
    public sealed class AsyncOperation
    {
        /// <summary>
        /// Name of the context for this operation (or null).
        /// </summary>
        public string Context;
        
        /// <summary>
        /// Time the operation started (UTC).
        /// </summary>
        public DateTime StartTime; 
        
        /// <summary>
        /// True if the operation has completed.
        /// </summary>
        public bool IsCompleted;
        
        /// <summary>
        /// The operation's async result.
        /// </summary>
        public IAsyncResultDiagnostics AsyncResult;

        /// <summary>
        /// Constructor.
        /// </summary>
        /// <param name="ar">The async result to associate with this instance.</param>
        public AsyncOperation(IAsyncResultDiagnostics ar)
        {
            this.Context     = AsyncTracker.Context;
            this.StartTime   = DateTime.UtcNow;
            this.IsCompleted = false;
            this.AsyncResult = ar;
        }
    }

#endif

    /// <summary>
    /// Used to track the progress and disposition of asynchronous operations
    /// for debugging purposes.
    /// </summary>
    public sealed class AsyncTracker
    {
        //---------------------------------------------------------------------
        // Static members

        /// <summary>
        /// Enables the AsyncTracker functionality.  This is set to false
        /// by default.
        /// </summary>
        public static bool Enable = false;

        /// <summary>
        /// Enables the gathering of actual call stack frames by <see cref="CallStack.AsyncTrace" />
        /// for DEBUG builds.  <c>false</c> by default.
        /// </summary>
        public static bool GatherCallStacks = false;

#if DEBUG
        /// <summary>
        /// The global <see cref="AsyncTracker"/> instance.
        /// </summary>
        public static AsyncTracker Global = null;

        /// <summary>
        /// The global context.
        /// </summary>
        public static string context = null;

        private static int          yield      = 0;
        private static int          yieldCount = 0;
#endif

        /// <summary>
        /// Starts the global AsyncTracker instance with default settings.
        /// </summary>
        public static void Start()
        {
            Start(TimeSpan.FromMinutes(6), TimeSpan.FromMinutes(6), 0);
        }

        /// <summary>
        /// Initializes a global AsyncTracker instance.
        /// </summary>
        /// <param name="holdTime">Maximum time to hold completed operations.</param>
        /// <param name="maxTimeout">Maximum time an async operation may remain uncompleted.</param>
        /// <param name="yieldCount">
        /// Indicates how often <see cref="Yield" /> should actually yield the thread or pass 
        /// 0 to disable yielding altogether.
        /// </param>
        /// <remarks>
        /// Pass maxTimeout=TimeSpan.Zero to disable checks for hung async operations.  Pass randomYield
        /// to command AsyncResult to randomly yield the processor to exercise the code looking for
        /// race conditions.  Note that this method does nothing if AsyncTracker.Enable=false.
        /// </remarks>
        [Conditional("DEBUG")]
        public static void Start(TimeSpan holdTime, TimeSpan maxTimeout, int yieldCount)
        {
#if DEBUG
            if (!Enable || Global != null)
                return;

            AsyncTracker.Global = new AsyncTracker(holdTime, maxTimeout, yieldCount);
#endif
        }

        /// <summary>
        /// Stops the tracking of async operations.
        /// </summary>
        [Conditional("DEBUG")]
        public static void Stop()
        {
#if DEBUG
            if (AsyncTracker.Global == null)
                return;

            AsyncTracker.Global.StopNow();
            AsyncTracker.Global = null;
            AsyncTracker.Context = null;
#endif
        }

        /// <summary>
        /// Returns <c>true</c> if AsyncTracker is enabled.
        /// </summary>
        public static bool Enabled
        {
            get
            {
#if DEBUG
                return AsyncTracker.Global != null;
#else
                return false;
#endif
            }
        }

        /// <summary>
        /// The string to use to identify the global context to be associated with all
        /// async operations added to the AsyncTracker.  This can be used to in test suites
        /// to associate the async operations with the suite that generated them.
        /// </summary>
        public static string Context
        {
#if DEBUG
            get { return context; }
            set { context = value; }
#else
            get {return null;}
            set {}
#endif
        }

        /// <summary>
        /// Adds an async operation to the tracker (if one has been started).
        /// </summary>
        /// <param name="ar">The operation.</param>
        [Conditional("DEBUG")]
        public static void Add(IAsyncResultDiagnostics ar)
        {
#if DEBUG
            if (AsyncTracker.Global == null)
                return;

            AsyncTracker.Global.Add(new AsyncOperation(ar));
#endif
        }

        /// <summary>
        /// Writes the status of the async result passed and its associated
        /// operation (if any) to the system log.
        /// </summary>
        /// <param name="ar">The result to dump.</param>
        /// <param name="title">Title line for the entry (or <c>null</c>).</param>
        [Conditional("DEBUG")]
        public static void Dump(IAsyncResultDiagnostics ar, string title)
        {
#if DEBUG
            if (AsyncTracker.Global == null)
            {

                // Write a light version of the dump to the log.

                SysLog.LogWarning("[{0}]: {1}\r\n\r\nCreate Stack:\r\n{2}", ar.GetType().FullName, title, ar.CreateStack);
                return;
            }

            AsyncTracker.Global.Dump(ar, title, true);
#endif
        }

        /// <summary>
        /// Writes information about async operations to the debugger's trace output.
        /// </summary>
        [Conditional("DEBUG")]
        public static void DumpPending()
        {
#if DEBUG
            if (AsyncTracker.Global != null)
                AsyncTracker.Global.InternalDumpPending();
#endif
        }

        /// <summary>
        /// Randomly yields the processor depending on whether a tracker has been
        /// started with yieldCount > 0.
        /// </summary>
        [Conditional("DEBUG")]
        public static void Yield()
        {
#if DEBUG
            if (yieldCount == 0)
                return;

            if (Interlocked.Increment(ref yield) % yieldCount == 0)
                Thread.Sleep(0);
#endif
        }

#if DEBUG

        //---------------------------------------------------------------------
        // Instance members

        private object      syncLock = new object();
        private TimeSpan    holdTime;
        private TimeSpan    maxTimeout;
        private GatedTimer  timer;
        private Hashtable   ht;
        private ArrayList   pending;

        /// <summary>
        /// Constructor.
        /// </summary>
        /// <param name="holdTime">Maximum time to hold completed operations.</param>
        /// <param name="maxTimeout">Maximum time an async operation may remain uncompleted.</param>
        /// <param name="yieldCount">
        /// Indicates how often <see cref="Yield" />  should actually yield the thread or pass 
        /// 0 to disable yielding altogether.
        /// </param>
        private AsyncTracker(TimeSpan holdTime, TimeSpan maxTimeout, int yieldCount)
        {
            AsyncTracker.yieldCount = yieldCount;

            this.holdTime   = holdTime;
            this.maxTimeout = maxTimeout;
            this.timer      = new GatedTimer(new TimerCallback(OnTimer), null, TimeSpan.FromSeconds(60), TimeSpan.FromSeconds(60));
            this.ht         = new Hashtable();
            this.pending    = new ArrayList();
        }

        /// <summary>
        /// Stops the tracker.
        /// </summary>
        private void StopNow()
        {
            Thread.Sleep(1100);

            lock (syncLock)
            {
                // Stop the background timer, purge all completed operations
                // and report any hung operations.

                timer.Dispose();
                timer = null;

                holdTime = TimeSpan.FromSeconds(1);
                OnTimer(null);
            }
        }

        /// <summary>
        /// Releases any managed resources associated with the object.
        /// </summary>
        private void Dispose()
        {
            if (timer != null)
            {
                timer.Dispose();
                timer = null;
            }
        }

        /// <summary>
        /// Called periodically to check for hung operations and to prune out 
        /// completed operations.
        /// </summary>
        /// <param name="state"></param>
        private void OnTimer(object state)
        {
            lock (syncLock)
            {
                // Add any pending operations to the main hash table

                lock (pending)
                {
                    foreach (AsyncOperation op in pending)
                        ht.Add(op, op);

                    pending.Clear();
                }

                // Delete all completed operations older than holdTime

                var deleted = new ArrayList();
                var now     = DateTime.UtcNow;

                foreach (AsyncOperation op in ht.Values)
                    if (op.AsyncResult.IsCompleted && now - op.StartTime >= holdTime)
                        deleted.Add(op);

                foreach (AsyncOperation op in deleted)
                    ht.Remove(op);

                // Dump information about any hung operations

                if (maxTimeout == TimeSpan.Zero)
                    return;

                var sb   = new StringBuilder();
                var hung = false;

                foreach (AsyncOperation op in ht.Values)
                    if (!op.AsyncResult.IsCompleted && now - op.StartTime >= maxTimeout)
                    {
                        IAsyncResultOwner owner;

                        owner = op.AsyncResult.Owner as IAsyncResultOwner;
                        if (owner != null && owner.DisableHangTest)
                            continue;

                        sb.Append("----------\r\n");
                        Dump(sb, op, op.AsyncResult);
                        hung = true;
                    }

                if (hung)
                {
                    SysLog.Trace("AsyncTracker", SysLogLevel.Verbose, sb.ToString());
                    Debugger.Break();
                }
            }
        }

        /// <summary>
        /// Adds an async operation to the tracker.
        /// </summary>
        /// <param name="op">The operation.</param>
        private void Add(AsyncOperation op)
        {
            // I'm going to add the new operation to the pending array here
            // and then add them to the hash table inside OnTimer() to avoid
            // deadlocks.

            lock (pending)
                pending.Add(op);
        }

        /// <summary>
        /// Writes a human readable description of the async result and associated
        /// operation to the system log.
        /// </summary>
        /// <param name="ar">The async result.</param>
        /// <param name="title">Title line for the entry (or <c>null</c>).</param>
        /// <param name="dummy">Just pass true.</param>
        private void Dump(IAsyncResultDiagnostics ar, string title, bool dummy)
        {
            lock (syncLock)
            {
                // Look up the operation that own's this result

                StringBuilder   sb = new StringBuilder();
                AsyncOperation  op = null;

                foreach (AsyncOperation o in ht.Values)
                    if (o.AsyncResult == ar)
                    {
                        op = o;
                        break;
                    }

                if (title != null)
                    sb.AppendFormat(null, "Message:   {0}\r\n", title);

                Dump(sb, op, ar);
                SysLog.Trace("AsyncTracker", SysLogLevel.Verbose, sb.ToString());
            }
        }


        /// <summary>
        /// Writes information about async operations to the debugger's trace output.
        /// </summary>
        private void InternalDumpPending()
        {
            int count = 0;

            Debug.WriteLine("========================================");
            Debug.WriteLine("Pending AsyncResults");

            lock (pending)
            {
                foreach (AsyncOperation op in pending)
                {
                    StringBuilder sb;

                    if (!op.AsyncResult.NotifyCalled)
                        continue;

                    sb = new StringBuilder();
                    sb.AppendLine("----------");
                    Dump(sb, op, op.AsyncResult);
                    Debug.Write(sb.ToString());
                    count++;
                }
            }

            // $hack(jeff.lill): 
            //
            // It's possible an AsyncResult to be copied from the
            // pending list to the hash table here, resulting in the
            // AsyncResult being dumped twice.  I'm not going to
            // worry too much about this.

            lock (syncLock)
            {
                foreach (AsyncOperation op in ht.Values)
                {
                    StringBuilder sb;

                    if (!op.AsyncResult.NotifyCalled)
                        continue;

                    sb = new StringBuilder();
                    sb.AppendLine("----------");
                    Dump(sb, op, op.AsyncResult);
                    Debug.Write(sb.ToString());
                    count++;
                }
            }

            if (count == 0)
                Debug.WriteLine("(none)");

            Debug.WriteLine("========================================");
        }

        /// <summary>
        /// Writes a human readable description of the operation passed to the
        /// string builder.
        /// </summary>
        /// <param name="sb">The string builder.</param>
        /// <param name="op">The operation (or <c>null</c> if not known).</param>
        /// <param name="ar">The async result.</param>
        private void Dump(StringBuilder sb, AsyncOperation op, IAsyncResultDiagnostics ar)
        {
            lock (syncLock)
            {
                IAsyncResultOwner   owner;
                string              ownerName = string.Empty;
                string              status;
                string              custom = ar.CustomStatus;

                if (op != null)
                {
                    Assertion.Test(ar == op.AsyncResult);
                    owner = op.AsyncResult.Owner as IAsyncResultOwner;
                    if (owner != null && owner.OwnerName != null)
                        ownerName = owner.OwnerName;
                }
                else
                    ownerName = "[none]";

                if (ar.IsCompleted)
                {
                    status = "Complete";
                    if (ar.IsDisposed)
                        status += "/Disposed";
                    else if (ar.InCallback)
                        status += "/In Callback";
                    else
                        status += "/???";
                }
                else if (op != null && maxTimeout != TimeSpan.Zero && DateTime.UtcNow - op.StartTime >= maxTimeout)
                    status = "Hung";
                else if (ar.NotifyCalled)
                    status = "Notified";
                else
                    status = "Pending";

                sb.AppendFormat(null, "Operation: {0}({1})\r\n", ar.GetType().Name, ownerName);

                sb.AppendFormat(null, "Status:    {0}\r\n", status);

                if (op != null && op.Context != null)
                    sb.AppendFormat(null, "Context:   {0}\r\n", op.Context);

                if (custom != null)
                    sb.AppendFormat(null, "Custom:    {0}\r\n", ar.CustomStatus);

                sb.AppendFormat(null, "Disposed:  {0}\r\n", ar.IsDisposed ? "Yes" : "No");

                if (ar.Exception == null)
                    sb.AppendFormat(null, "Exception: None\r\n");
                else
                {
                    sb.AppendFormat(null, "Exception: {0}\r\n", ar.Exception.Message);
                    sb.Append("Exception Trace:\r\n");
                    new CallStack(ar.Exception, true).Dump(sb);
                }

                sb.Append("\r\nCreate Trace:\r\n");
                ar.CreateStack.Dump(sb);

                if (ar.StartStack != null)
                {
                    sb.Append("\r\nStart Trace:\r\n");
                    ar.StartStack.Dump(sb);
                }

                if (ar.WaitStack != null)
                {
                    sb.Append("\r\nWait Trace:\r\n");
                    ar.WaitStack.Dump(sb);
                }

                if (ar.NotifyStack != null)
                {
                    sb.Append("\r\nNotify Trace:\r\n");
                    ar.NotifyStack.Dump(sb);
                }

                if (ar.CompleteStack != null)
                {

                    sb.Append("\r\nComplete Trace:\r\n");
                    ar.CompleteStack.Dump(sb);
                }
            }
        }

#endif // DEBUG
    }

#else // MOBILE_DEVICE

    /// <summary>
    /// AsyncTracker is not implemented for mobile device builds.
    /// </summary>
    public class AsyncTracker
    {
    }

#endif
}
