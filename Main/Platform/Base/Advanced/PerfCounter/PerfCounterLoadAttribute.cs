//-----------------------------------------------------------------------------
// FILE:        PerfCounterLoadAttribute.cs
// CONTRIBUTOR: Jeff Lill
// COPYRIGHT:   Copyright (c) 2005-2015 by Jeffrey Lill.  All rights reserved.
// DESCRIPTION: Marks static methods that initialize the performance counters
//              for an assembly.

using System;
using System.Diagnostics;

using LillTek.Common;

namespace LillTek.Advanced
{
    /// <summary>
    /// Marks static methods that initialize the performance counters
    /// for an assembly.
    /// </summary>
    /// <remarks>
    /// <seealso cref="PerfCounterSet.LoadCounters"/>
    /// </remarks>
    [AttributeUsage(AttributeTargets.Method, Inherited = false, AllowMultiple = false)]
    public sealed class PerfCounterLoadAttribute : System.Attribute
    {
    }
}
