//-----------------------------------------------------------------------------
// FILE:        IProgressManager.cs
// CONTRIBUTOR: Jeff Lill
// COPYRIGHT:   Copyright (c) 2005-2015 by Jeffrey Lill.  All rights reserved.
// DESCRIPTION: Manages an application progress indicator.

using System;

namespace LillTek.Client
{
    /// <summary>
    /// Manages an application progress indicator.
    /// </summary>
    public interface IProgressManager
    {
        /// <summary>
        /// Starts the progress indicator.
        /// </summary>
        /// <remarks>
        /// <note>
        /// This method will only be called on the UI thread.
        /// </note>
        /// </remarks>
        void StartProgress();

        /// <summary>
        /// Stops the progress indicator.
        /// </summary>
        /// <remarks>
        /// <note>
        /// This method will only be called on the UI thread.
        /// </note>
        /// </remarks>
        void StopProgress();
    }
}
