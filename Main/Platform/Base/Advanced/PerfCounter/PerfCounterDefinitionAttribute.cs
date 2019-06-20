//-----------------------------------------------------------------------------
// FILE:        PerfCounterDefineAttribute.cs
// CONTRIBUTOR: Jeff Lill
// COPYRIGHT:   Copyright (c) 2005-2015 by Jeffrey Lill.  All rights reserved.
// DESCRIPTION: Marks static methods that define the performance counters
//              for an assembly.

using System;
using System.Diagnostics;

using LillTek.Common;

namespace LillTek.Advanced
{
    /// <summary>
    /// Marks static methods that define the performance counters
    /// for an assembly.
    /// </summary>
    /// <remarks>
    /// <seealso cref="PerfCounterSet.DefineCounters"/>
    /// </remarks>
    [AttributeUsage(AttributeTargets.Method, Inherited = false, AllowMultiple = false)]
    public sealed class PerfCounterDefinitionAttribute : System.Attribute
    {
    }
}
