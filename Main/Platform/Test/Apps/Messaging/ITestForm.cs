//-----------------------------------------------------------------------------
// FILE:        ITest.cs
// OWNER:       Jeff Lill
// COPYRIGHT:   Copyright (c) 2005-2015 by Jeffrey Lill.  All rights reserved.
// DESCRIPTION: Defines the implementation of a test form.

using System;
using System.Collections.Generic;
using System.Text;

namespace LillTek.Test.Messaging
{
    interface ITestForm
    {
        /// <summary>
        /// Stops execution of the test if one is running.
        /// </summary>
        void Stop();

        /// <summary>
        /// Called periodically by the main form allowing statistics to
        /// be rendered.  This will be called on the UI thread.
        /// </summary>
        void OnTimer();
    }
}
