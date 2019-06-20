//-----------------------------------------------------------------------------
// FILE:        Stub.cs
// CONTRIBUTOR: Jeff Lill
// COPYRIGHT:   Copyright (c) 2005-2015 by Jeffrey Lill.  All rights reserved.
// DESCRIPTION: A class to be used for defining a unique method signature
//              within base classes.

using System;

namespace LillTek.Common
{
    /// <summary>
    /// A class to be used for defining a unique method signature within virtual classes.
    /// </summary>
    /// <remarks>
    /// <para>
    /// It's often useful to be able to create a specialized parameterless virtual class 
    /// method and then call it from derived classes.  The problem is that this can often
    /// confliict with other parameter-less methods in the class, so a dummy parameter
    /// of a particular type is used to create an distinguisable method.
    /// </para>
    /// <para>
    /// This type is designed to be used for that purpose.  Typically, the method will
    /// be defined will a single method of this type, and then the method will be called,
    /// passing <see cref="Param" /> (or <c>null</c>).
    /// </para>
    /// </remarks>
    public sealed class Stub
    {
        //---------------------------------------------------------------------
        // Private types

        private class DoNothingDisposable : IDisposable
        {
            public void Dispose()
            {
            }
        }

        //---------------------------------------------------------------------
        // Implementation

        /// <summary>
        /// Used to specify a special but meaningless parameter value.
        /// </summary>
        public static readonly Stub Param = null;

        /// <summary>
        /// Returns a do-nothing object that implements <see cref="IDisposable"/>.
        /// </summary>
        public static readonly IDisposable Disposable = new DoNothingDisposable();
    }
}
