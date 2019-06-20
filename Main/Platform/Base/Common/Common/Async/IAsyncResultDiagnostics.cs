//-----------------------------------------------------------------------------
// FILE:        IAsyncResultDiagnostics.cs
// CONTRIBUTOR: Jeff Lill
// COPYRIGHT:   Copyright (c) 2005-2015 by Jeffrey Lill.  All rights reserved.
// DESCRIPTION: Defines the diagnostics capabilities of the AsyncResult classes.

using System;
using System.Net;
using System.Text;
using System.Threading;
using System.Diagnostics;
using System.Runtime.InteropServices;

namespace LillTek.Common
{
    /// <summary>
    /// Defines the diagnostics capabilities of the <see cref="AsyncResult" />
    /// and <see cref="AsyncResult{TResult,TInternalState}" /> classes.
    /// </summary>
    public interface IAsyncResultDiagnostics
    {
        /// <summary>
        /// Release any resource associated with this object.
        /// </summary>
        void Dispose();

        /// <summary>
        /// Returns the object that owns this operation (used for debugging).
        /// </summary>
        object Owner { get; }

        /// <summary>
        /// Returns the stack trace of the code that created this operation
        /// or <c>null</c> for non-DEBUG builds.
        /// </summary>
        CallStack CreateStack { get; }

        /// <summary>
        /// Returns the stack trace of the code that initiated this operation
        /// or <c>null</c> for non-DEBUG builds.
        /// </summary>
        CallStack StartStack { get; }

        /// <summary>
        /// Returns the stack trace when <see cref="AsyncResult{TResult,TInternalState}.Notify()" /> was called
        /// or <c>null</c> for non-DEBUG builds.
        /// </summary>
        CallStack NotifyStack { get; }

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
        CallStack CompleteStack { get; }

        /// <summary>
        /// Returns the stack trace of the code that is that called the 
        /// <see cref="AsyncResult{TResult,TInternalState}.Wait" /> or the AsyncWaitHandle.WaitOne() method.  
        /// This will return null if neither of these methods have been called.
        /// </summary>
        CallStack WaitStack { get; set; }

        /// <summary>
        /// Returns the exception to be thrown by the when the operation
        /// completes (or <c>null</c>).
        /// </summary>
        Exception Exception { get; }

        /// <summary>
        /// Returns <c>true</c> if the operation has been completed.
        /// </summary>
        bool IsCompleted { get; }

        /// <summary>
        /// Returns <c>true</c> if <see cref="AsyncResult{TResult,TInternalState}.Notify(Exception)" />
        /// or <see cref="AsyncResult{TResult,TInternalState}.Notify()" /> has been called.
        /// </summary>
        bool NotifyCalled { get; }

#if DEBUG
        /// <summary>
        /// Reports custom status information to AsyncTracker or returns null.
        /// </summary>
        string CustomStatus { get; }

        /// <summary>
        /// Returns <c>true</c> if <see cref="Dispose" /> has been called on this instance.
        /// </summary>
        bool IsDisposed { get; }
        /// <summary>
        /// Returns <c>true</c> if the operation is inside the callback.
        /// </summary>
        bool InCallback { get; }
#endif

    }
}
