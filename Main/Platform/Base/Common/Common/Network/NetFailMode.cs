//-----------------------------------------------------------------------------
// FILE:        NetFailMode.cs
// CONTRIBUTOR: Jeff Lill
// COPYRIGHT:   Copyright (c) 2005-2015 by Jeffrey Lill.  All rights reserved.
// DESCRIPTION: Defines the possible simulated network failure modes for
//              use by UNIT tests.

using System;
using System.Text;
using System.Reflection;
using System.Diagnostics;

namespace LillTek.Common
{
    /// <summary>
    /// Defines the possible simulated network failure modes for
    /// use by UNIT tests.
    /// </summary>
    public enum NetFailMode
    {
        /// <summary>
        /// Operate the network normally.
        /// </summary>
        Normal,

        /// <summary>
        /// Simulate a fully disconnected network (no messages get through).
        /// </summary>
        Disconnected,

        /// <summary>
        /// Simulate a highly loaded or noisy network where every other
        /// message gets through.
        /// </summary>
        Intermittent,

        /// <summary>
        /// Simulate the duplication of messages.
        /// </summary>
        Duplicate,

        /// <summary>
        /// Introduce 100ms message transmission delays to exercise
        /// threading and timing issues.
        /// </summary>
        Delay,
    }
}
