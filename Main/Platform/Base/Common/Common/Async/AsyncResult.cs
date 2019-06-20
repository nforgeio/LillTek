//-----------------------------------------------------------------------------
// FILE:        AsyncResult.cs
// CONTRIBUTOR: Jeff Lill
// COPYRIGHT:   Copyright (c) 2005-2015 by Jeffrey Lill.  All rights reserved.
// DESCRIPTION: A common IAsyncResult implementation

using System;
using System.Net;
using System.Text;
using System.Threading;
using System.Diagnostics;
using System.Runtime.InteropServices;

namespace LillTek.Common
{
    /// <summary>
    /// Utility class implementing common <see cref="IAsyncResult" /> behaviors.
    /// Consider using the generic <see cref="AsyncResult{TResult,TInternalState}" />.
    /// class instead for better performance and compile-time type safety.
    /// </summary>
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
    public class AsyncResult : AsyncResult<object, object>
    {
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
            : base(owner, callback, state)
        {
        }
    }
}
