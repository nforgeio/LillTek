//-----------------------------------------------------------------------------
// FILE:        ParseAction.cs
// CONTRIBUTOR: Jeff Lill
// COPYRIGHT:   Copyright (c) 2005-2015 by Jeffrey Lill.  All rights reserved.
// DESCRIPTION: Defines the delegate that can be used to implement custom 
//              string parsing actions.

using System;

namespace LillTek.Common
{
    /// <summary>
    /// Defines the delegate that can be used to implement custom 
    /// string parsing actions.
    /// </summary>
    /// <typeparam name="TValue">Type of the parsed value.</typeparam>
    /// <param name="serialized">The serialized value.</param>
    /// <returns>The parsed value.</returns>
    /// <remarks>
    /// <note>
    /// The delegate implemention should throw an exception if the serialized
    /// string passed does not represent a valid value.
    /// </note>
    /// </remarks>
    public delegate TValue ParseAction<TValue>(string serialized);
}
