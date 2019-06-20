//-----------------------------------------------------------------------------
// FILE:        MethodArgXInvoker.cs
// CONTRIBUTOR: Jeff Lill
// COPYRIGHT:   Copyright (c) 2005-2015 by Jeffrey Lill.  All rights reserved.
// DESCRIPTION: Defines delegate types with zero to four arguments.

using System;

namespace LillTek.Common
{
    /// <summary>
    /// Used for delegating to parameter-less methods.
    /// </summary>
    public delegate void MethodDelegate();

    /// <summary>
    /// Used for delegating to to a single parameter method.
    /// </summary>
    /// <param name="arg">The argument</param>
    public delegate void MethodArg1Invoker(object arg);

    /// <summary>
    /// Used for delegating to to a one parameter method.
    /// </summary>
    /// <param name="arg1">The first argument</param>
    /// <param name="arg2">The second argument</param>
    public delegate void MethodArg2Invoker(object arg1, object arg2);

    /// <summary>
    /// Used for delegating to to a two parameter method.
    /// </summary>
    /// <param name="arg1">The first argument</param>
    /// <param name="arg2">The second argument</param>
    /// <param name="arg3">The third argument</param>
    public delegate void MethodArg3Invoker(object arg1, object arg2, object arg3);

    /// <summary>
    /// Used for delegating to to a three parameter method.
    /// </summary>
    /// <param name="arg1">The first argument</param>
    /// <param name="arg2">The second argument</param>
    /// <param name="arg3">The third argument</param>
    /// <param name="arg4">The fourth argument</param>
    public delegate void MethodArg4Invoker(object arg1, object arg2, object arg3, object arg4);
}
