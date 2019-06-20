//-----------------------------------------------------------------------------
// FILE:        ICancelable.cs
// CONTRIBUTOR: Jeff Lill
// COPYRIGHT:   Copyright (c) 2005-2015 by Jeffrey Lill.  All rights reserved.
// DESCRIPTION: Defines the behavior of an object that can cancel an
//              asynchronous operation in progress.

using System;

namespace LillTek.Common
{
    /// <summary>
    /// Defines the behavior of an object that can cancel an
    /// asynchronous operation in progress.
    /// </summary>
    public interface ICancelable
    {
        /// <summary>
        /// Cancels the operation in progress if there is one.  No
        /// exceptions will be thrown if there is no operation
        /// currently in progress.
        /// </summary>
        void Cancel();
    }
}
