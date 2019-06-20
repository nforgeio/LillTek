//-----------------------------------------------------------------------------
// FILE:        Assertion.cs
// CONTRIBUTOR: Jeff Lill
// COPYRIGHT:   Copyright (c) 2005-2015 by Jeffrey Lill.  All rights reserved.
// DESCRIPTION: Implements a debug assertion class.

using System;
using System.Diagnostics;

namespace LillTek.Common
{
    /// <summary>
    /// Thrown when an assertion fails.
    /// </summary>
    public class AssertException : Exception
    {
        /// <summary>
        /// Constructs an assertion failure exception.
        /// </summary>
        public AssertException()
            : base()
        {
        }

        /// <summary>
        /// Constructs an assertion failure exception with a message.
        /// </summary>
        /// <remarks>
        /// The exception message.
        /// </remarks>
        public AssertException(string message)
            : base(message)
        {
        }
    }

    /// <summary>
    /// Global class that provides DEBUG builds with the ability to 
    /// implement assertion checks.
    /// </summary>
    /// <remarks>
    /// <para>
    /// This replaces Debug.Assert() which doesn't throw an exception
    /// on error which means that there's no way for a non-UI application
    /// to catch and log an assertion.  This implementation will throw
    /// a <see cref="AssertException" /> when an assertion fails unless
    /// a debugger is attached to the application, in which case, the 
    /// class will break into the debugger.
    /// </para>
    /// <para>
    /// Calls to the <see cref="Test(bool)" /> methods will be generate code only 
    /// for DEBUG builds whereas calls to <see cref="Validate(bool)" /> will generate
    /// code for all builds.
    /// </para>
    /// </remarks>
    public static class Assertion
    {
        /// <summary>
        /// Throws an <see cref="AssertException" /> or breaks into the attached debugger
        /// if the condition is not true.
        /// </summary>
        /// <param name="condition">The condition to test.</param>
        /// <remarks>
        /// Calls to this method generate code only for DEBUG builds.
        /// </remarks>
        [Conditional("DEBUG")]
        public static void Test(bool condition)
        {
            if (!condition)
            {
                if (Debugger.IsAttached)
                    Debugger.Break();
                else
                {
                    SysLog.LogErrorStackDump("Assertion failed.");
                    throw new AssertException();
                }
            }
        }

        /// <summary>
        /// Throws an <see cref="AssertException" /> or breaks into the attached debugger
        /// if the condition is not true.
        /// </summary>
        /// <param name="condition">The condition to test.</param>
        /// <param name="message">The message to include in the exception.</param>
        /// <remarks>
        /// <para>
        /// Calls to this method generate code only for DEBUG builds.
        /// </para>>
        /// </remarks>
        [Conditional("DEBUG")]
        public static void Test(bool condition, string message)
        {
            if (!condition)
            {
                if (Debugger.IsAttached)
                    Debugger.Break();
                else
                {
                    SysLog.LogErrorStackDump(message);
                    throw new AssertException(message);
                }
            }
        }

        /// <summary>
        /// Throws an <see cref="AssertException" /> or breaks into the attached debugger
        /// if the condition is not true.
        /// </summary>
        /// <param name="condition">The condition to test.</param>
        /// <param name="format">The message format string.</param>
        /// <param name="args">The message arguments.</param>
        /// <remarks>
        /// Calls to this method generate code only for DEBUG builds.
        /// </remarks>
        [Conditional("DEBUG")]
        public static void Test(bool condition, string format, params object[] args)
        {
            if (!condition)
            {
                if (Debugger.IsAttached)
                    Debugger.Break();
                else
                {
                    SysLog.LogErrorStackDump(format, args);
                    throw new AssertException(string.Format(format, args));
                }
            }
        }

        /// <summary>
        /// Throws an <see cref="AssertException" /> or breaks into the attached debugger
        /// unconditionally.
        /// </summary>
        /// <remarks>
        /// Calls to this method generate code only for DEBUG builds.
        /// </remarks>
        [Conditional("DEBUG")]
        public static void Fail()
        {
            Assertion.Test(false);
        }

        /// <summary>
        /// Throws an <see cref="AssertException" /> or breaks into the attached debugger
        /// unconditionally.
        /// </summary>
        /// <param name="message">The message to include in the exception.</param>
        /// <remarks>
        /// Calls to this method generate code only for DEBUG builds.
        /// </remarks>
        [Conditional("DEBUG")]
        public static void Fail(string message)
        {
            Assertion.Test(false, message);
        }

        /// <summary>
        /// Throws an <see cref="AssertException" /> or breaks into the attached debugger
        /// unconditionally.
        /// </summary>
        /// <param name="format">The message format string.</param>
        /// <param name="args">The message arguments.</param>
        /// <remarks>
        /// Calls to this method generate code only for DEBUG builds.
        /// </remarks>
        [Conditional("DEBUG")]
        public static void Fail(string format, params object[] args)
        {
            Assertion.Test(false, format, args);
        }

        /// <summary>
        /// Validates that the condition is true, throwing an <see cref="AssertException" />
        /// or breaking into the attached debugger if it is not.
        /// </summary>
        /// <param name="condition">The condition to test.</param>
        /// <remarks>
        /// Calls to this method generate code for all builds builds.
        /// </remarks>
        public static void Validate(bool condition)
        {
            if (!condition)
            {
                if (Debugger.IsAttached)
                    Debugger.Break();
                else
                {
                    SysLog.LogErrorStackDump("Assertion failed.");
                    throw new AssertException();
                }
            }
        }

        /// <summary>
        /// Validates that the condition is true, throwing an <see cref="AssertException" />
        /// or breaking into the attached debugger if it is not.
        /// </summary>
        /// <param name="condition">The condition to test.</param>
        /// <param name="message">The message to include in the exception.</param>
        /// <remarks>
        /// Calls to this method generate code for all builds builds.
        /// </remarks>
        public static void Validate(bool condition, string message)
        {
            if (!condition)
            {
                if (Debugger.IsAttached)
                    Debugger.Break();
                else
                {
                    SysLog.LogErrorStackDump(message);
                    throw new AssertException(message);
                }
            }
        }

        /// <summary>
        /// Validates that the condition is true, throwing an <see cref="AssertException" />
        /// or breaking into the attached debugger if it is not.
        /// </summary>
        /// <param name="condition">The condition to test.</param>
        /// <param name="format">The message format string.</param>
        /// <param name="args">The message arguments.</param>
        /// <remarks>
        /// Calls to this method generate code for all builds builds.
        /// </remarks>
        public static void Validate(bool condition, string format, params object[] args)
        {
            if (!condition)
            {
                if (Debugger.IsAttached)
                    Debugger.Break();
                else
                {
                    SysLog.LogErrorStackDump(format, args);
                    throw new AssertException(string.Format(format, args));
                }
            }
        }
    }
}
