//-----------------------------------------------------------------------------
// FILE:        TriState.cs
// CONTRIBUTOR: Jeff Lill
// COPYRIGHT:   Copyright (c) 2005-2015 by Jeffrey Lill.  All rights reserved.
// DESCRIPTION: Implements a three-value boolean that encodes True, False,
//              and Unknown.

using System;

namespace LillTek.Common
{
    /// <summary>
    /// Implements a three-value boolean that encodes <see cref="True" />, <see cref="False" />, 
    /// and <see cref="Unknown" />.
    /// </summary>
    /// <threadsafety instance="true" />
    [Obsolete("Use the nullable [bool?] type now supported by C#.")]
    public struct TriState
    {
        //---------------------------------------------------------------------
        // Static members

        /// <summary>
        /// The <see cref="TriState" /> <c>true</c> value.
        /// </summary>
        public readonly static TriState True = new TriState(TRUE);

        /// <summary>
        /// The <see cref="TriState" /> <c>false</c> value.
        /// </summary>
        public readonly static TriState False = new TriState(FALSE);

        /// <summary>
        /// The <see cref="TriState" /> <b>unknown</b> value.
        /// </summary>
        public readonly static TriState Unknown = new TriState(UNKNOWN);

        /// <summary>
        /// Implicitly casts a <see cref="Boolean" /> to a <see cref="TriState" />.
        /// </summary>
        /// <param name="v">The input value.</param>
        /// <returns>The converted output.</returns>
        public static implicit operator TriState(bool v)
        {
            return v ? True : False;
        }

        /// <summary>
        /// Explicit cast of a <see cref="TriState" /> to a <see cref="Boolean" />.
        /// </summary>
        /// <param name="v">The input value.</param>
        /// <returns>The converted output.</returns>
        /// <exception cref="ArgumentException">Thrown if the input value is <see cref="Unknown" />.</exception>
        public static explicit operator bool(TriState v)
        {
            if (v.value == UNKNOWN)
                throw new ArgumentException("Cannot convert TriState.Unknown to a boolean.");

            return v.value == TRUE;
        }

        /// <summary>
        /// Compares two <see cref="TriState" /> instances and returns <c>true</c> if
        /// they are equal.
        /// </summary>
        /// <param name="param1">The first value.</param>
        /// <param name="param2">The second value.</param>
        /// <returns><c>true</c> if the parameters are equal.</returns>
        public static bool operator ==(TriState param1, TriState param2)
        {
            return param1.value == param2.value;
        }

        /// <summary>
        /// Compares two <see cref="TriState" /> instances and returns <c>true</c> if
        /// they are not equal.
        /// </summary>
        /// <param name="param1">The first value.</param>
        /// <param name="param2">The second value.</param>
        /// <returns><c>true</c> if the parameters are not equal.</returns>
        public static bool operator !=(TriState param1, TriState param2)
        {
            return param1.value != param2.value;
        }

        //---------------------------------------------------------------------
        // Instance members

        private const int UNKNOWN = 0;
        private const int FALSE = 1;
        private const int TRUE = 2;

        private int value;

        /// <summary>
        /// Constructor.
        /// </summary>
        /// <param name="v">The low-level value.</param>
        private TriState(int v)
        {
            this.value = v;
        }

        /// <summary>
        /// Constructs an instance using the boolean value.
        /// </summary>
        /// <param name="v">The boolean state.</param>
        public TriState(bool v)
        {
            this.value = v ? TRUE : FALSE;
        }

        /// <summary>
        /// Intializes this instance with the value from the parameter passed.
        /// </summary>
        /// <param name="v">The tristate value.</param>
        public TriState(TriState v)
        {
            value = v.value;
        }

        /// <summary>
        /// Returns <c>true</c> if the instance value is <see cref="Unknown" />.
        /// </summary>
        public bool IsUnknown
        {
            get { return value == UNKNOWN; }
        }

        /// <summary>
        /// Returns <c>true</c> if the instance value is <see cref="True" />.
        /// </summary>
        public bool IsTrue
        {
            get { return value == TRUE; }
        }

        /// <summary>
        /// Returns <c>true</c> if the instance value is <see cref="False" />.
        /// </summary>
        public bool IsFalse
        {
            get { return value == FALSE; }
        }

        /// <summary>
        /// Returns <c>true</c> if the object passed equals this instance.
        /// </summary>
        /// <param name="obj">The object to be compared.</param>
        /// <returns><c>true</c> if the objects are equal.</returns>
        public override bool Equals(object obj)
        {
            return this.value == ((TriState)obj).value;
        }

        /// <summary>
        /// Copmputes a hash value for the instance.
        /// </summary>
        /// <returns>The hash.</returns>
        public override int GetHashCode()
        {
            return value;
        }
    }
}
