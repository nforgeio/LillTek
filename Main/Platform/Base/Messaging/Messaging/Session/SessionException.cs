//-----------------------------------------------------------------------------
// FILE:        SessionException.cs
// CONTRIBUTOR: Jeff Lill
// COPYRIGHT:   Copyright (c) 2005-2015 by Jeffrey Lill.  All rights reserved.
// DESCRIPTION: Thrown for faults detected within a session with a remote application.

using System;

using LillTek.Common;

namespace LillTek.Messaging
{
    /// <summary>
    /// Thrown for faults detected within a session with a remote application.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Use the static <see cref="Create(string,string)" /> method to create exception instances.
    /// Note that this method treats certain exception types such as <see cref="TimeoutException" />,
    /// <see cref="NotAvailableException"/>, and <see cref="CancelException" /> specially by instantiating 
    /// exceptions with those types rather then returning a <see cref="SessionException" />.
    /// </para>
    /// </remarks>
    public sealed class SessionException : ApplicationException
    {
        //---------------------------------------------------------------------
        // Static members

        private static string cancelName       = typeof(CancelException).FullName;
        private static string timeoutName      = typeof(TimeoutException).FullName;
        private static string notAvailableName = typeof(NotAvailableException).FullName;

        /// <summary>
        /// Returns the appropriate exception from the parameter passed.
        /// </summary>
        /// <param name="typeName">The fully qualified name of the exception type (or <c>null</c>).</param>
        /// <param name="message">The exception message.</param>
        /// <returns>The exception.</returns>
        /// <remarks>
        /// <note>
        /// Note that this method treats certain exception types such as <see cref="TimeoutException" />
        /// and <see cref="CancelException" /> specially by instantiating exceptions
        /// with those types rather then returning a <see cref="SessionException" />.
        /// </note>
        /// </remarks>
        public static Exception Create(string typeName, string message)
        {
            if (typeName == null)
                return new SessionException(typeName, message);

            if (typeName == cancelName)
                return new CancelException(message);
            else if (typeName == timeoutName)
                return new TimeoutException(message);
            else if (typeName == notAvailableName)
                return new NotAvailableException(message);
            else
                return new SessionException(typeName, message);
        }

        /// <summary>
        /// Return the appropriate exception from the parameter passed.
        /// </summary>
        /// <param name="typeName">The fully qualified name of the exception type (or <c>null</c>).</param>
        /// <param name="format">The exception message format string.</param>
        /// <param name="args">The message arguments.</param>
        /// <returns>The exception.</returns>
        /// <remarks>
        /// <note>
        /// Note that this method treats certain exception types such as <see cref="TimeoutException" />
        /// and <see cref="CancelException" /> specially by instantiating exceptions
        /// with those types rather then returning a <see cref="SessionException" />.
        /// </note>
        /// </remarks>
        public static Exception Create(string typeName, string format, params object[] args)
        {
            return Create(typeName, string.Format(format, args));
        }

        //---------------------------------------------------------------------
        // Instance members

        /// <summary>
        /// The fully qualified name of the exception type thrown by the server.
        /// </summary>
        public readonly string ExceptionTypeName;

        /// <summary>
        /// Constructor.
        /// </summary>
        /// <param name="typeName">The fully qualified name of the exception type thrown by the server.</param>
        /// <param name="message">The exception message.</param>
        /// <remarks>
        /// <note>
        /// If <paramref name="typeName" /> is passed as <c>null</c> then the <see cref="ExceptionTypeName" />
        /// property will be set to the fully qualified name for <see cref="SessionException" />.
        /// </note>
        /// </remarks>
        private SessionException(string typeName, string message)
            : base(message)
        {
            this.ExceptionTypeName = typeName != null ? typeName : typeof(SessionException).FullName;
        }
    }
}
