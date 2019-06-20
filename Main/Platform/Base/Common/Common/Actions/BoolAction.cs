//-----------------------------------------------------------------------------
// FILE:        BoolAction.cs
// CONTRIBUTOR: Jeff Lill
// COPYRIGHT:   Copyright (c) 2005-2015 by Jeffrey Lill. All rights reserved.
// DESCRIPTION: Defines the generic Action style delegates that return a boolean.

using System;
using System.IO;
using System.Text;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;

namespace LillTek.Common
{
    /// <summary>
    /// Defines a parameterless boolean delegate.
    /// </summary>
    /// <returns>The boolean result.</returns>
    public delegate bool BoolAction();

    /// <summary>
    /// Defines a single parameter boolean delegate. 
    /// </summary>
    /// <typeparam name="T1">Type of parameter 1.</typeparam>
    /// <param name="p1">Parameter 1.</param>
    /// <returns>The boolean result.</returns>
    public delegate bool BoolAction<T1>(T1 p1);

    /// <summary>
    /// Defines a two parameter boolean delegate. 
    /// </summary>
    /// <typeparam name="T1">Type of parameter 1.</typeparam>
    /// <typeparam name="T2">Type of parameter 2.</typeparam>
    /// <param name="p1">Parameter 1.</param>
    /// <param name="p2">Parameter 1.</param>
    /// <returns>The boolean result.</returns>
    public delegate bool BoolAction<T1, T2>(T1 p1, T2 p2);

    /// <summary>
    /// Defines a three parameter boolean delegate. 
    /// </summary>
    /// <typeparam name="T1">Type of parameter 1.</typeparam>
    /// <typeparam name="T2">Type of parameter 2.</typeparam>
    /// <typeparam name="T3">Type of parameter 3.</typeparam>
    /// <param name="p1">Parameter 1.</param>
    /// <param name="p2">Parameter 1.</param>
    /// <param name="p3">Parameter 1.</param>
    /// <returns>The boolean result.</returns>
    public delegate bool BoolAction<T1, T2, T3>(T1 p1, T2 p2, T3 p3);

    /// <summary>
    /// Defines a four parameter boolean delegate. 
    /// </summary>
    /// <typeparam name="T1">Type of parameter 1.</typeparam>
    /// <typeparam name="T2">Type of parameter 2.</typeparam>
    /// <typeparam name="T3">Type of parameter 3.</typeparam>
    /// <typeparam name="T4">Type of parameter 4.</typeparam>
    /// <param name="p1">Parameter 1.</param>
    /// <param name="p2">Parameter 1.</param>
    /// <param name="p3">Parameter 1.</param>
    /// <param name="p4">Parameter 1.</param>
    /// <returns>The boolean result.</returns>
    public delegate bool BoolAction<T1, T2, T3, T4>(T1 p1, T2 p2, T3 p3, T4 p4);
}
