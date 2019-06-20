//-----------------------------------------------------------------------------
// FILE:        ArgCollectionType.cs
// CONTRIBUTOR: Jeff Lill
// COPYRIGHT:   Copyright (c) 2005-2015 by Jeffrey Lill.  All rights reserved.
// DESCRIPTION: Enumerates the possible ArgCollection types.

using System;
using System.Text;
using System.Net;
using System.Collections;
using System.Collections.Generic;

namespace LillTek.Common
{
    /// <summary>
    /// Enumerates the possible ArgCollection types.
    /// </summary>
    public enum ArgCollectionType
    {

        /// <summary>
        /// Used to construct a normal <see cref="ArgCollection" /> with the default 
        /// assignment (<b>=</b>) and separator (<b>;</b>) characters.
        /// </summary>
        Normal,

        /// <summary>
        /// Used to construct a specialized <see cref="ArgCollection" /> that disables
        /// the assignment and separator characters so that the collection can hold
        /// arbitrary keys and values.
        /// </summary>
        Unconstrained
    }
}
