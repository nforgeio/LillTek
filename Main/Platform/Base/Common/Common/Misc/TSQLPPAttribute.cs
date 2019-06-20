//-----------------------------------------------------------------------------
// FILE:        TSQLPPAttribute.cs
// CONTRIBUTOR: Jeff Lill
// COPYRIGHT:   Copyright (c) 2005-2015 by Jeffrey Lill.  All rights reserved.
// DESCRIPTION: Used to mark an enumeration type for processing
//              by the TSQLPP utility.

using System;
using System.Diagnostics;

namespace LillTek.Common
{
    /// <summary>
    /// Used to mark an enumeration type for processing
    /// by the TSQLPP utility.
    /// </summary>
    [AttributeUsage(AttributeTargets.Enum | AttributeTargets.Class, Inherited = false, AllowMultiple = false)]
    public sealed class TSQLPPAttribute : System.Attribute
    {
    }
}
