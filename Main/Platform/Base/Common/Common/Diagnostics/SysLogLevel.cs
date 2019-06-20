//-----------------------------------------------------------------------------
// FILE:        SysLogLevel.cs
// CONTRIBUTOR: Jeff Lill
// COPYRIGHT:   Copyright (c) 2005-2015 by Jeffrey Lill.  All rights reserved.
// DESCRIPTION: Used to describe the level of detail provided in a 
//              SysLog.Trace() call.

using System;
using System.Text;
using System.Reflection;
using System.ComponentModel;

namespace LillTek.Common
{
    /// <summary>
    /// Used to describe the level of detail provided in a 
    /// <see cref="SysLog.Trace(string,SysLogLevel,string,object[])" /> call.
    /// </summary>
    public enum SysLogLevel
    {
        /// <summary>
        /// The trace contains high level summary information.
        /// </summary>
        High = 0,

        /// <summary>
        /// The trace contains medium level information.
        /// </summary>
        Medium = 1,

        /// <summary>
        /// The trace contains very verbose information.
        /// </summary>
        Verbose = 2
    }
}
