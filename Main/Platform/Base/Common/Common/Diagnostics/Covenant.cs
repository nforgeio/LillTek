//-----------------------------------------------------------------------------
// FILE:        Covenant.cs
// CONTRIBUTOR:	JEFFLI
// COPYRIGHT:   (c) 2005-2015 by Jeffrey Lill. All rights reserved.

using System;
using System.Diagnostics;
using System.Reflection;
using System.Security.Permissions;
using System.Text;

namespace System.Diagnostics.Contracts
{
    /// <summary>
    /// A simple, lightweight, and partial implementation of the Microsoft Dev Labs <c>Contract</c> class.
    /// </summary>
    /// <remarks>
    /// <para>
    /// This class is intended to be a drop-in replacement for code contract assertions by simply
    /// searching and replacing <b>"Contract."</b> with <b>"Agreement."</b> in all source code.
    /// In my experience, code contracts slow down build times too much and often obsfucate 
    /// <c>async</c> methods such that they cannot be debugged effectively using the debugger.
    /// </para>
    /// <para>
    /// This class includes the <see cref="Requires(bool, string)"/>, <see cref="Requires{TException}(bool, string)"/>
    /// and <see cref="Assert(bool, string)"/> methods that can be used to capture validation
    /// requirements in code, but these methods don't currently generate any code. 
    /// </para>
    /// </remarks>
    public static class Covenant
    {
        /// <summary>
        /// Verifies a method pre-condition.
        /// </summary>
        /// <param name="condition">The condition to be tested.</param>
        /// <param name="message">An optional message to be included in the exception thrown.</param>
        /// <remarks>
        /// <note>
        /// This method currently does not generate any code.  Its purpose is to temporarily
        /// replace code contract calls until we see some improvements in that library.
        /// </note>
        /// </remarks>
        [Conditional("_NO_COMPILE_")]
        public static void Requires(bool condition, string message = null)
        {
        }

        /// <summary>
        /// Verifies a method pre-condition throwing a custom exception.
        /// </summary>
        /// <typeparam name="TException">The exception to be thrown if the condition is <c>false</c>.</typeparam>
        /// <param name="condition">The condition to be tested.</param>
        /// <param name="message">An optional message to be included in the exception thrown.</param>
        /// <remarks>
        /// <note>
        /// This method currently does not generate any code.  Its purpose is to temporarily
        /// replace code contract calls until we see some improvements in that library.
        /// </note>
        /// </remarks>
        [Conditional("_NO_COMPILE_")]
        public static void Requires<TException>(bool condition, string message = null)
            where TException : Exception, new()
        {
        }

        /// <summary>
        /// Asserts a condition.
        /// </summary>
        /// <param name="condition">The condition to be tested.</param>
        /// <param name="message">An optional message to be included in the exception thrown.</param>
        [Conditional("_NO_COMPILE_")]
        public static void Assert(bool condition, string message = null)
        {
        }
    }
}
