//-----------------------------------------------------------------------------
// FILE:        PollingThread.cs
// CONTRIBUTOR: Jeff Lill
// COPYRIGHT:   Copyright (c) 2005-2015 by Jeffrey Lill.  All rights reserved.
// DESCRIPTION: Implements a thread to be used for perodically polling a resource.

using System;
using System.Threading;

using LillTek.Common;

namespace LillTek.Advanced
{
    /// <summary>
    /// Defines a <see cref="PollingThread" />'s external thread function.
    /// </summary>
    /// <param name="thread">The polling thread.</param>
    public delegate void PollingThreadStart(PollingThread thread);

    /// <summary>
    /// Implements a thread to be used for perodically polling a resource.
    /// </summary>
    /// <remarks>
    /// <para>
    /// PollingThreads are used to periodically call the specified <see cref="MethodArg1Invoker" />
    /// delegate on a new background thread, passing an argument.  The polling interval and
    /// argument is passed to the constructor.  Use <see cref="Start" /> to start the thread
    /// after it has been constructed.
    /// </para>
    /// <para>
    /// PollingThreads can be constructed to use an internal thread function implemented by
    /// the class or an external function implemented by the application.  Use 
    /// <see cref="PollingThread(TimeSpan)" /> to construct an instance using an internal
    /// thread function and <see cref="PollingThread(TimeSpan,PollingThreadStart)" />
    /// for one using an external function.
    /// </para>
    /// <para>
    /// Instances using the interna thread function call the virtual <see cref="OnPoll" /> 
    /// method which raises <see cref="Poll" /> at the specified polling interval.  External
    /// thread functions should use the <see cref="WaitExit" /> method to wait for the
    /// polling interval and also to check whether the thread is being stopped.  The code
    /// for an external thread function should look something like:
    /// </para>
    /// <code language="cs">
    /// 
    ///     void ThreadFunc(PollingThread thread) {
    /// 
    ///         while (true) {
    /// 
    ///             // Implement the thread task.
    /// 
    ///             if (thread.WaitExit())
    ///                 return;
    ///         }
    ///     }
    /// 
    /// </code>
    /// <para>
    /// The class uses a <see cref="ManualResetEvent" /> to control the polling interval.  By
    /// default this event is reset, causing the event to timeout at the polling interval.
    /// Applications can access this event via the <see cref="PollEvent" /> property and 
    /// manually set or reset the event to modify the polling behavior.  Advanced implementations
    /// may also use <see cref="ClosePending" /> to determine if <see cref="Close" /> has been
    /// called to more promptly terminate thread processing and avoid a thread abort.
    /// </para>
    /// <para>
    /// One useful pattern is to manually set the <see cref="PollEvent" /> in the application
    /// and allow polling to progress without delay while there is work to be performed
    /// by the thread and then to reset the event once the thread runs out of work so as to
    /// poll for work at the slower interval.
    /// </para>
    /// <para>
    /// Use <see cref="Close" /> to stop the thread.
    /// </para>
    /// </remarks>
    public class PollingThread
    {
        //---------------------------------------------------------------------
        // Static members

        /// <summary>
        /// A reasonable default timeout to pass to <see cref="Close" />.
        /// </summary>
        public static readonly TimeSpan CloseTimeout = TimeSpan.FromSeconds(5);

        //---------------------------------------------------------------------
        // Instance members

        private object              arg;            // The thread argument
        private TimeSpan            pollTime;       // The polling interval
        private ManualResetEvent    pollEvent;      // The polling event
        private Thread              pollThread;     // The background thread
        private bool                abort;          // Set to true to exit the thread
        private PollingThreadStart  externalFunc;   // The external thread function (or null)

        /// <summary>
        /// Raised when the thread is polled.
        /// </summary>
        public event MethodArg1Invoker Poll;

        /// <summary>
        /// Constructs a polling thread that implements an internal thread
        /// function.
        /// </summary>
        /// <param name="pollTime">The polling time.</param>
        public PollingThread(TimeSpan pollTime)
        {

            this.arg          = null;
            this.pollTime     = pollTime;
            this.pollEvent    = new ManualResetEvent(false);
            this.pollThread   = new Thread(new ThreadStart(ThreadFunc));
            this.abort        = false;
            this.externalFunc = null;
        }

        /// <summary>
        /// Constructs a polling thread that used an external thread
        /// function.
        /// </summary>
        /// <param name="pollTime">The polling time.</param>
        /// <param name="threadFunc">The external thread function.</param>
        public PollingThread(TimeSpan pollTime, PollingThreadStart threadFunc)
        {
            this.arg          = null;
            this.pollTime     = pollTime;
            this.pollEvent    = new ManualResetEvent(false);
            this.pollThread   = new Thread(new ThreadStart(ExternalThreadFunc));
            this.abort        = false;
            this.externalFunc = threadFunc;
        }

        /// <summary>
        /// Starts the thread.
        /// </summary>
        /// <param name="arg">The thread argument.</param>
        public void Start(object arg)
        {
            if (pollThread == null)
                throw new ObjectDisposedException(this.GetType().Name);

            this.arg = arg;
            pollThread.Start();
        }

        /// <summary>
        /// The thread name.
        /// </summary>
        public string Name
        {
            get
            {
                if (pollThread == null)
                    throw new ObjectDisposedException(this.GetType().Name);

                return pollThread.Name;
            }

            set
            {
                if (pollThread == null)
                    throw new ObjectDisposedException(this.GetType().Name);

                pollThread.Name = value;
            }
        }

        /// <summary>
        /// Returns the thread argument.
        /// </summary>
        public object Arg
        {
            get { return arg; }
        }

        /// <summary>
        /// Returns the <see cref="ManualResetEvent" /> used to control the thread polling.
        /// </summary>
        public ManualResetEvent PollEvent
        {
            get { return pollEvent; }
        }

        /// <summary>
        /// Returns <c>true</c> if <see cref="Close" /> has been called for this thread.
        /// </summary>
        public bool ClosePending
        {
            get { return abort; }
        }

        /// <summary>
        /// Called when the thread is polled.
        /// </summary>
        /// <remarks>
        /// The base implementation raises <see cref="PollEvent" />.
        /// </remarks>
        protected virtual void OnPoll()
        {
            if (Poll != null)
                Poll(arg);
        }

        /// <summary>
        /// Stops the thread and releases all resources.
        /// </summary>
        /// <param name="timeout">The maximum time to wait for the thread to stop cleanly.</param>
        /// <remarks>
        /// This method first attempts to stop the thread cleanly by signalling that
        /// it should stop and then waiting for up to the interval specified by <b>timeout</b>.
        /// If this time is exceeded, then the method will force a thread abort.
        /// </remarks>
        public void Close(TimeSpan timeout)
        {
            try
            {
                abort = true;
                pollEvent.Set();
                Helper.JoinThread(pollThread, timeout);
            }
            finally
            {
                pollThread = null;
                pollEvent = null;
            }
        }

        /// <summary>
        /// Waits for the thread's poll event to be set or to timeout.
        /// </summary>
        /// <remarks>
        /// This method should be used within external thread functions to 
        /// throttle polling.  The method return <c>true</c> if the thread is being
        /// stopped.
        /// </remarks>
        public bool WaitExit()
        {
            if (abort)
                return true;

            pollEvent.WaitOne(pollTime, false);

            return abort;
        }

        /// <summary>
        /// Implements the internal thread function.
        /// </summary>
        private void ThreadFunc()
        {
            while (true)
            {
                OnPoll();

                if (WaitExit())
                    return;
            }
        }

        /// <summary>
        /// Launches the external thread function.
        /// </summary>
        private void ExternalThreadFunc()
        {
            externalFunc(this);
        }
    }
}
