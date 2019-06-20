//-----------------------------------------------------------------------------
// FILE:        AsyncResultGeneric.cs
// CONTRIBUTOR: Jeff Lill
// COPYRIGHT:   Copyright (c) 2005-2015 by Jeffrey Lill.  All rights reserved.
// DESCRIPTION: A generic IAsyncResult implementation

using System;
using System.Net;
using System.Text;
using System.Threading;
using System.Diagnostics;
using System.Reflection;
using System.Runtime.InteropServices;

namespace LillTek.Common
{
#if DEBUG && !MOBILE_DEVICE

    /// <summary>
    /// Delegate used in DEBUG builds to hook <see cref="AsyncResult{TResult, TInternalState}.Notify()" /> and
    /// <see cref="AsyncResult{TResult, TInternalState}.Notify(Exception)" /> calls via the
    /// <see cref="AsyncResult{TResult, TInternalState}.NotifyHook" /> event.
    /// </summary>
    /// <param name="ar">The <see cref="IAsyncResultDiagnostics" /> instance.</param>
    /// <param name="e">The exception (or <c>null</c>).</param>
    public delegate void NotifyHookDelegate(IAsyncResultDiagnostics ar, Exception e);

    /// <summary>
    /// Used for generating unique trace IDs.
    /// </summary>
    internal static class AsyncTraceID
    {
        private static int nextTraceID = 0;

        /// <summary>
        /// Returns the next available trace ID.
        /// </summary>
        /// <returns></returns>
        public static int GetNextID()
        {
            return Interlocked.Increment(ref nextTraceID);
        }
    }

#endif // DEBUG && !MOBILE_DEVICE

    /// <summary>
    /// Utility generic class implementing common <see cref="IAsyncResult" /> behaviors.
    /// </summary>
    /// <typeparam name="TResult">The result type.</typeparam>
    /// <typeparam name="TInternalState">The internal state type.</typeparam>
    /// <remarks>
    /// <para>
    /// This class integrates with the <see cref="AsyncTracker" /> class to implement DEBUG related
    /// async operation tracking by exposing information useful for tracking down the
    /// reasons behind hung and orphaned operations.
    /// </para>
    /// <para>
    /// Use of this class should be pretty straight forward.  The main thing to note
    /// is that the <see cref="AsyncResult{TResult,TInternalState}.Started()" /> method should be called directly 
    /// after initiating the underlying async operation.  Doing the registers the async 
    /// operation with the current <see cref="AsyncTracker" /> instance (if any) and 
    /// prevents an assert from being fired in <see cref="AsyncResult{TResult,TInternalState}.Notify()" />.
    /// </para>
    /// </remarks>
    public class AsyncResult<TResult, TInternalState> : IAsyncResult, IAsyncResultDiagnostics, ILockable
    {
        //---------------------------------------------------------------------
        // Private classes

#if DEBUG && !MOBILE_DEVICE

        /// <summary>
        /// For DEBUG builds, I need to hook the <see cref="WaitOne()" /> method so that we
        /// can update the waitStack for the associated async result.
        /// </summary>
        private class AsyncEvent : WaitHandle
        {
            private IAsyncResultDiagnostics     owner;
            private ManualResetEvent            wait;
            private bool                        isWaiting;

            public AsyncEvent(IAsyncResultDiagnostics owner, bool initialState)
            {
                this.owner     = owner;
                this.wait      = new ManualResetEvent(initialState);
                this.isWaiting = false;
            }

            public WaitHandle WaitHandle
            {
                get { return wait; }
            }

            public override bool WaitOne()
            {
                using (TimedLock.Lock(owner))
                {
                    if (owner.WaitStack == null)
                        owner.WaitStack = CallStack.AsyncTrace(0, true);

                    if (isWaiting)
                        throw new InvalidOperationException("Another thread is already waiting.");

                    isWaiting = true;
                }

                return wait.WaitOne();
            }

#if WINFULL
            public override bool WaitOne(int millisecondTimeout, bool exitContext)
            {
                using (TimedLock.Lock(owner))
                {
                    if (owner.WaitStack == null)
                        owner.WaitStack = CallStack.AsyncTrace(0, true);

                    if (!isWaiting)
                        throw new InvalidOperationException("Another thread is already waiting.");

                    isWaiting = true;
                }

                return wait.WaitOne(millisecondTimeout, exitContext);
            }

            public override bool WaitOne(TimeSpan timeout, bool exitContext)
            {
                using (TimedLock.Lock(owner))
                {
                    if (owner.WaitStack == null)
                        owner.WaitStack = CallStack.AsyncTrace(0, true);

                    if (!isWaiting)
                        throw new InvalidOperationException("Another thread is already waiting.");

                    isWaiting = true;
                }

                return wait.WaitOne(timeout, exitContext);
            }
#endif // WINFULL

            public void Set()
            {
                wait.Set();
            }
        }

#endif

        //---------------------------------------------------------------------
        // Static members

        /// <summary>
        /// Controls whether full stack tracing will be enabled for DEBUG builds.
        /// This is set to <c>false</c> by default.
        /// </summary>
        /// <remarks>
        /// <para>
        /// This option works only for DEBUG builds.  When enabled, the
        /// <see cref="AsyncResult{TResult,TInternalState}" /> class will record the
        /// call stack at the point where the asynchronous operation was created,
        /// started, waited on, signalled as complete, and completed.  These
        /// stacks are available as properties in the DEBUG build and will also
        /// be dumped to the event log under certain circumstances.
        /// </para>
        /// <para>
        /// This is set to <c>false</c> by default since enabling this will incur
        /// a substantial performance penalty.  When stack tracing is disabled,
        /// the internal stack method <see cref="CallStack.AsyncTrace" />
        /// will return a dummy stack trace.
        /// </para>
        /// </remarks>
        public static bool EnableStackTracing = false;

        //---------------------------------------------------------------------
        // Instance members

        static private WaitCallback onComplete = new WaitCallback(OnComplete);

        private object                  owner;
        private AsyncCallback           callback;
        private object                  asyncState;
        private TResult                 result;
        private TInternalState          internalState;
        private DateTime                ttd;
#if DEBUG && !MOBILE_DEVICE
        private AsyncEvent              wait;
#else
        private ManualResetEvent        wait;
#endif
        private bool                    isCompleted;
        private bool                    syncCompletion;
        private Exception               exception;
        private bool                    notifyQueued;

#if DEBUG && !MOBILE_DEVICE
        private bool                    isWaiting;
        private bool                    isReleased;
        private int                     traceID;
        private string                  traceName;
        private bool                    inCallback;
        private bool                    disposed;
        private CallStack               createStack;
        private CallStack               startStack;
        private CallStack               notifyStack;
        private CallStack               completeStack;
        private CallStack               waitStack;

        /// <summary>
        /// Available for DEBUG builds to hook calls to hook <see cref="AsyncResult{TResult, TInternalState}.Notify()" /> and
        /// <see cref="AsyncResult{TResult, TInternalState}.Notify(Exception)" /> for diagnostic purposes.
        /// </summary>
        public event NotifyHookDelegate NotifyHook;
#endif

        /// <summary>
        /// Initialize the instance.
        /// </summary>
        /// <param name="owner">The object that "owns" this operation (or <c>null</c>).</param>
        /// <param name="callback">The delegate to call when the operation completes.</param>
        /// <param name="state">The application defined state.</param>
        /// <remarks>
        /// The owner parameter is optionally used to identify the object that "owns"
        /// this operation.  This parameter may be null or any object type.  Additional
        /// information will be tracked by <see cref="AsyncTracker" /> if the object implements the
        /// <see cref="IAsyncResultOwner" /> interface.
        /// </remarks>
        public AsyncResult(object owner, AsyncCallback callback, object state)
        {
            this.owner          = owner;
            this.callback       = callback;
            this.asyncState     = state;
            this.internalState  = default(TInternalState);
            this.ttd            = DateTime.MaxValue;
            this.result         = default(TResult);
            this.wait           = null;
            this.isCompleted    = false;
            this.syncCompletion = false;
            this.exception      = null;
            this.notifyQueued   = false;
#if DEBUG && !MOBILE_DEVICE
            this.isWaiting      = false;
            this.isReleased     = false;
            this.traceID        = -1;
            this.traceName      = null;
            this.inCallback     = false;
            this.disposed       = false;
            this.createStack    = CallStack.AsyncTrace(1, true);
            this.startStack     = null;
            this.notifyStack    = null;
            this.completeStack  = null;
            this.waitStack      = null;
#endif
        }

        /// <summary>
        /// Release any resources associated with this object.
        /// </summary>
        public void Dispose()
        {
            using (TimedLock.Lock(this))
            {
#if DEBUG && !MOBILE_DEVICE
                if (!isCompleted)
                {
                    AsyncTracker.Dump(this, "Disposing AsyncResult before operation completes.");
                    SysLog.LogErrorStackDump("Disposing AsyncResult before operation completes.");
                    return;
                }

                if (disposed)
                {
                    var sb = new StringBuilder(1024);

                    AsyncTracker.Dump(this, "AsyncResult has already been disposed.");

                    sb.AppendLine("AsyncResult has already been disposed.");
                    sb.AppendLine();
                    sb.AppendLine("Current stack:");
                    CallStack.AsyncTrace(1, true).Dump(sb);
                    sb.AppendLine();
                    sb.AppendLine("Completion stack:");
                    completeStack.Dump(sb);

                    SysLog.LogError(sb.ToString());
                    return;
                }

                this.disposed = true;
                this.completeStack = CallStack.AsyncTrace(1, true);
#endif
                if (wait != null)
                {
                    wait.Close();
                    wait = null;
                }
            }

#if !MOBILE_DEVICE
            AsyncTracker.Yield();
#endif
        }

        /// <summary>
        /// Call this method after the async operation has been started successfully.
        /// </summary>
        /// <param name="traceEnable">Pass <c>true</c> to enable tracing.</param>
        [Conditional("DEBUG")]
        public void Started(bool traceEnable)
        {
#if DEBUG && !MOBILE_DEVICE
            if (traceEnable)
            {
                // The trace name will be the name of the object type and method
                // that called this method.

                var stack = new CallStack(1, false);
                var method = stack.GetFrame(0).GetMethod();

                this.traceName = method.DeclaringType.Name + "." + method.Name;
                this.traceID = AsyncTraceID.GetNextID();
            }

            Trace("Started()");
            this.startStack = CallStack.AsyncTrace(1, true);

            AsyncTracker.Add(this);
            AsyncTracker.Yield();
#endif
        }

        /// <summary>
        /// Call this method after the async operation has been started successfully.
        /// </summary>
        [Conditional("DEBUG")]
        public void Started()
        {
#if DEBUG && !MOBILE_DEVICE
            Trace("Started()");
            this.startStack = CallStack.AsyncTrace(1, true);

            AsyncTracker.Add(this);
            AsyncTracker.Yield();
#endif
        }

#if DEBUG && !MOBILE_DEVICE
        /// <summary>
        /// Writes a debugging trace.
        /// </summary>
        /// <param name="format">The message format string.</param>
        /// <param name="args">The message arguments.</param>
        private void Trace(string format, params object[] args)
        {
            if (traceName != null)
                Debug.WriteLine(string.Format("{0}: [id={1},op={2}]", traceName, traceID, string.Format(format, args)));
        }
#endif // DEBUG

        /// <summary>
        /// Returns the object that owns this operation (used for debugging).
        /// </summary>
        public object Owner
        {
            get { return owner; }
        }

        /// <summary>
        /// The scheduled time-to-die for the operation (SYS).
        /// </summary>
        /// <remarks>
        /// <para>
        /// This is available for applications that want to track pending
        /// asynchronous operations and abort those that have exceeded a
        /// specified timeout.
        /// </para>
        /// <note>This defaults to <b>DateTime.MaxValue</b>.</note>
        /// </remarks>
        public DateTime TTD
        {
            get { return ttd; }
            set { ttd = value; }
        }

        /// <summary>
        /// Returns the stack trace of the code that created this operation
        /// or <c>null</c> for non-DEBUG builds.
        /// </summary>
        public CallStack CreateStack
        {
            get
            {
#if DEBUG && !MOBILE_DEVICE
                return createStack;
#else
                return null;
#endif
            }
        }

        /// <summary>
        /// Returns the stack trace of the code that initiated this operation
        /// or <c>null</c> for non-DEBUG builds.
        /// </summary>
        public CallStack StartStack
        {
            get
            {
#if DEBUG && !MOBILE_DEVICE
                return startStack;
#else
                return null;
#endif
            }
        }

        /// <summary>
        /// Returns the stack trace when <see cref="Notify()" /> was called
        /// or <c>null</c> for non-DEBUG builds.
        /// </summary>
        public CallStack NotifyStack
        {
            get
            {
#if DEBUG && !MOBILE_DEVICE
                return notifyStack;
#else
                return null;
#endif
            }
        }

        /// <summary>
        /// Returns the stack trace of the code that completed this operation
        /// or <c>null</c> if the operation is still pending or if this is a non-DEBUG
        /// build.
        /// </summary>
        /// <remarks>
        /// <note>
        /// The operation is considered to be complete when the
        /// <see cref="Dispose" /> method is called.
        /// </note>
        /// </remarks>
        public CallStack CompleteStack
        {
            get
            {
#if DEBUG && !MOBILE_DEVICE
                return completeStack;
#else
                return null;
#endif
            }
        }

        /// <summary>
        /// Returns the stack trace of the code that is that called the 
        /// <see cref="Wait" /> or the AsyncWaitHandle.WaitOne() method.  
        /// This will return null if neither of these methods have been called.
        /// </summary>
        public CallStack WaitStack
        {
            get
            {
#if DEBUG && !MOBILE_DEVICE
                return waitStack;
#else
                return null;
#endif
            }

            set
            {
#if DEBUG && !MOBILE_DEVICE
                waitStack = value;
#endif
            }
        }

#if DEBUG
        /// <summary>
        /// Reports custom status information to AsyncTracker or returns null.
        /// </summary>
        public virtual string CustomStatus
        {
            get { return null; }
        }

        /// <summary>
        /// Returns <c>true</c> if <see cref="Dispose" /> has been called on this instance.
        /// </summary>
        public bool IsDisposed
        {
            get
            {
#if !MOBILE_DEVICE
                return disposed;
#else
                return false;
#endif
            }
        }

        /// <summary>
        /// Returns <c>true</c> if the operation is inside the callback.
        /// </summary>
        public bool InCallback
        {
            get
            {
#if !MOBILE_DEVICE
                return inCallback;
#else
                return false;
#endif
            }
        }
#endif

        /// <summary>
        /// The application state associated with the operation.
        /// </summary>
        public object AsyncState
        {
            get { return asyncState; }
            set { asyncState = value; }
        }

        /// <summary>
        /// This property is available for async operations that wish to use this
        /// for communicating an operation result from the point where the operation
        /// was completed to the point where EndXXX() is called.
        /// </summary>
        public TResult Result
        {
            get { return result; }
            set { result = value; }
        }

        /// <summary>
        /// This property is available for async operations that need to track
        /// internal state while the operation progresses.
        /// </summary>
        public TInternalState InternalState
        {
            get { return internalState; }
            set { internalState = value; }
        }

        /// <summary>
        /// The handler to be invoked when the operation completes (or <c>null</c>).
        /// </summary>
        public AsyncCallback Callback
        {
            get { return callback; }
            set { callback = value; }
        }

        /// <summary>
        /// Returns the wait object to be used to explicitly wait for 
        /// the operation to complete.
        /// </summary>
        public WaitHandle AsyncWaitHandle
        {
            get
            {
#if !MOBILE_DEVICE
                AsyncTracker.Yield();
#endif
                using (TimedLock.Lock(this))
                {
                    if (wait != null)
                        return wait;
#if DEBUG && !MOBILE_DEVICE
                    wait = new AsyncEvent(this, isCompleted);
#else
                    wait = new ManualResetEvent(isCompleted);
#endif
                    return wait;
                }
            }
        }

        /// <summary>
        /// Indicates whether or not the operation was completed synchronously.
        /// </summary>
        public bool CompletedSynchronously
        {
            get { return syncCompletion; }
            set { syncCompletion = value; }
        }

        /// <summary>
        /// Returns <c>true</c> if the operation has been completed.
        /// </summary>
        public bool IsCompleted
        {
            get { return isCompleted; }
        }

        /// <summary>
        /// Returns <c>true</c> if <see cref="AsyncResult{TResult,TInternalState}.Notify(Exception)" />
        /// or <see cref="AsyncResult{TResult,TInternalState}.Notify()" /> has been called.
        /// </summary>
        /// <remarks>
        /// This is useful in those unfortunate instances where the .NET
        /// Framework (actually Windows I/O completion ports), returns
        /// more than one completion event.  Use this to avoid calling
        /// <see cref="Notify()" /> more than once in these situations to avoid the 
        /// performance hit of logging an exception in DEBUG builds.
        /// </remarks>
        public bool NotifyCalled
        {
            get { return notifyQueued; }
        }

        /// <summary>
        /// Notify the application that the operation is complete
        /// via the call back delegate.
        /// </summary>
        /// <param name="e">
        /// The exception to be thrown in the <see cref="Finish" /> method
        /// or <c>null</c> if the operation was completed successfully.
        /// </param>
        /// <remarks>
        /// <note>
        /// Although this method may be called more than once for
        /// a particular async result instance, all but the first call
        /// will be ignored.
        /// </note>
        /// </remarks>
        public void Notify(Exception e)
        {
#if DEBUG && !MOBILE_DEVICE
            // Wait up to a second to verify that Started() has been
            // called before reporting a warning.

            DateTime    endTime = SysTime.Now + TimeSpan.FromSeconds(1);
            bool        started = this.startStack != null;

            while (!started && SysTime.Now < endTime)
            {
                Thread.Sleep(10);
                started = this.startStack != null;
            }

            if (!started)
            {
                Trace("Notify({0}): Warning: Started() not called", exception != null ? exception.GetType().Name : (result == null ? "null" : result.ToString()));
                AsyncTracker.Dump(this, "Warning: Started() not called before Notifiy()");
            }

            if (notifyQueued)
            {
                Trace("Notify({0}: Already queued)", exception != null ? exception.GetType().Name : (result == null ? "null" : result.ToString()));
                return;
            }

            Trace("Notify({0})", exception != null ? exception.GetType().Name : (result == null ? "null" : result.ToString()));
            notifyStack = CallStack.AsyncTrace(1, true);

            if (NotifyHook != null)
                NotifyHook(this, e);
#endif

#if !MOBILE_DEVICE
            AsyncTracker.Yield();
#endif
            using (TimedLock.Lock(this))
            {
                notifyQueued = true;
                if (isCompleted)
                    return;     // We've already signaled completion

                exception = e;

                if (callback != null)
                {
                    if (syncCompletion)
                        callback(this);
                    else
                        Helper.UnsafeQueueUserWorkItem(onComplete, this);
                }
                else
                {
                    isCompleted = true;
                    if (wait != null)
                        wait.Set();
                }
            }

#if !MOBILE_DEVICE
            AsyncTracker.Yield();
#endif
        }

        /// <summary>
        /// Notify the application that the operation has completed
        /// successfully by queuing a call to the delegate.
        /// </summary>
        /// <remarks>
        /// <note>
        /// Although this method may be called more than
        /// once, all but the first call will be ignored.
        /// </note>
        /// </remarks>
        public void Notify()
        {
            Notify(null);
        }

        /// <summary>
        /// Forwards queued completion notifications to the proper
        /// async result handler. 
        /// </summary>
        /// <param name="state">The queued async result.</param>
        private static void OnComplete(object state)
        {
            AsyncResult<TResult, TInternalState> ar = (AsyncResult<TResult, TInternalState>)state;
#if DEBUG && !MOBILE_DEVICE
            ar.inCallback = true;
#endif
            try
            {
#if !MOBILE_DEVICE
                AsyncTracker.Yield();
#endif
                using (TimedLock.Lock(ar))
                {
#if DEBUG && !MOBILE_DEVICE
                    if (ar.disposed)
                    {
                        ar.Trace("OnComplete(): AsyncResult has been disposed before operation completes");
                        AsyncTracker.Dump(ar, "AsyncResult has been disposed before operation completes.");
                        SysLog.LogErrorStackDump("AsyncResult has been disposed before operation completes.");

                        if (Debugger.IsAttached)
                            Debugger.Break();
                    }

                    ar.Trace("Callback({0})", ar.callback == null ? "null" : "");
#endif
                    if (ar.callback == null)
                        return;

                    ar.isCompleted = true;
                    if (ar.wait != null)
                        ar.wait.Set();

                    ar.callback(ar);
                }
            }
            catch (Exception e)
            {
                SysLog.LogException(e);
            }
            finally
            {
#if DEBUG && !MOBILE_DEVICE
                ar.inCallback = false;
#endif

#if !MOBILE_DEVICE
                AsyncTracker.Yield();
#endif
            }
        }

        /// <summary>
        /// Returns the exception to thrown by the <see cref="Finish" /> method
        /// handling the completion (or <c>null</c> if none).
        /// </summary>
        public Exception Exception
        {
            get { return exception; }
        }

        /// <summary>
        /// Blocks the current thread until the operation completes.
        /// This is more efficient than calling <b>AsyncWaitHandle.WaitOne()</b>
        /// since it won't allocate an event if the operation has already
        /// completed.
        /// </summary>
        public void Wait()
        {
#if DEBUG && !MOBILE_DEVICE
            using (TimedLock.Lock(this))
            {

                if (waitStack == null)
                    waitStack = CallStack.AsyncTrace(0, true);

                if (isWaiting)
                {
                    Trace("Wait(exception): Another thread is already waiting.");
                    throw new InvalidOperationException("Another thread is already waiting.");
                }

                if (isReleased)
                {
                    Trace("Wait(release): Already released");
                    return;
                }

                if (isCompleted)
                {
                    isReleased = true;
                    Trace("Wait(no wait): Result={0} Sync={1}", result == null ? "null" : result.ToString(), syncCompletion);
                    return;
                }

                isWaiting = true;
            }
#endif

#if !MOBILE_DEVICE
            AsyncTracker.Yield();
#endif

#if DEBUG && !MOBILE_DEVICE
            Trace("Wait(block)");
            ((AsyncEvent)this.AsyncWaitHandle).WaitOne();
            isReleased = true;
            Trace("Wait(release): Result={0} Sync={1}", result == null ? "null" : result.ToString(), syncCompletion);
#else
            this.AsyncWaitHandle.WaitOne();
#endif

#if !MOBILE_DEVICE
            AsyncTracker.Yield();
#endif
        }

        /// <summary>
        /// Blocks the current thread until the operation completes and
        /// then throws any pending exceptions.  The method also takes
        /// care of disposing the async result.
        /// </summary>
        /// <remarks>
        /// This is useful for situations where there are no return
        /// values saved in the async result instance.
        /// </remarks>
        public void Finish()
        {
            Wait();
            try
            {
                if (exception != null)
                    Helper.Rethrow(exception);
            }
            finally
            {

                this.Dispose();
            }
        }

#if DEBUG && !MOBILE_DEVICE
        /// <summary>
        /// Writes the async result's state in human readable form to the
        /// system log.
        /// </summary>
        /// <param name="title">An optional title line (or <c>null</c>).</param>
        public void Dump(string title)
        {
            AsyncTracker.Dump(this, title);
        }

        /// <summary>
        /// Writes the async result's state in human readable form to the
        /// system log.
        /// </summary>
        public void Dump()
        {
            AsyncTracker.Dump(this, null);
        }
#endif

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
