//-----------------------------------------------------------------------------
// FILE:        ICloneable2.cs
// CONTRIBUTOR: Jeff Lill
// COPYRIGHT:   Copyright (c) 2005-2015 by Jeffrey Lill.  All rights reserved.
// DESCRIPTION: Defines the advanced cloneable interface.

using System;

namespace System
{
    /// <summary>
    /// Defines the advanced cloneable interface.
    /// </summary>
    /// <remarks>
    /// <para>
    /// The original .NET <b>System.ICloneable</b> interface is somewhat controversial
    /// since it had no way of indicating whether a deep or shallow clone should
    /// be returned.  To make matters worse, Microsoft decided that they were
    /// explicitly restricting the use of <b>ICloneable</b> for Silverlight
    /// applications by defining this interface but making it <b>private</b>.
    /// This means that it is not possible to add a custom <b>ICloneable</b>
    /// interface for backwards compatibility.
    /// </para>
    /// <para>
    /// This interface is being defined to deal with both of these problems.
    /// All applications going forward should implement this interface rather 
    /// than <b>ICloneable</b>.
    /// </para>
    /// </remarks>
    public interface ICloneable2
    {
        /// <summary>
        /// Returns <c>true</c> if the object implements <see cref="DeepClone" />.
        /// </summary>
        bool IsDeepCloneable { get; }

        /// <summary>
        /// Returns <c>true</c> if the object implements <see cref="ShallowClone" />.
        /// </summary>
        bool IsShallowCloneable { get; }

        /// <summary>
        /// Returns a deep clone of the object.
        /// </summary>
        /// <returns>The clone.</returns>
        /// <exception cref="NotImplementedException">Thrown if the deep cloning is not implemented.</exception>
        object DeepClone();

        /// <summary>
        /// Returns a shallow clone of the object.
        /// </summary>
        /// <returns>The clone.</returns>
        /// <exception cref="NotImplementedException">Thrown if the shallow cloning is not implemented.</exception>
        object ShallowClone();

        /// <summary>
        /// Creates a new object that is a copy of the current instance. 
        /// </summary>
        /// <returns>The new object that is a copy of this instance.</returns>
        /// <remarks>
        /// This method is provided for backwards compatability and does not
        /// specify whether a deep or shallow clone is desired.  The implementation
        /// may decide which method will be used to create the clone.
        /// </remarks>
        object Clone();
    }
}
