//-----------------------------------------------------------------------------
// FILE:        IDLElement.cs
// CONTRIBUTOR: Jeff Lill
// COPYRIGHT:   Copyright (c) 2005-2015 by Jeffrey Lill.  All rights reserved.
// DESCRIPTION: Defines the interface for an object to be saved in a doubly 
//              linked list.

using System;
using System.IO;
using System.Text;

namespace LillTek.Common
{
    /// <summary>
    /// Defines the interface for an object to be saved in a doubly linked list.
    /// </summary>
    /// <remarks>
    /// <seealso cref="LillTek.Common.DoubleList"/>
    /// </remarks>
    public interface IDLElement
    {
        /// <summary>
        /// The previous object in the list (or the list instance).
        /// </summary>
        object Previous { get; set; }

        /// <summary>
        /// The next object in the list (or the list instance).
        /// </summary>
        object Next { get; set; }
    }
}
