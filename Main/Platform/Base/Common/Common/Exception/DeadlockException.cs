//-----------------------------------------------------------------------------
// FILE:        DeadlockException.cs
// CONTRIBUTOR: Jeff Lill
// COPYRIGHT:   Copyright (c) 2005-2015 by Jeffrey Lill.  All rights reserved.
// DESCRIPTION: Exception thrown when a TimedLock.Lock() attempt timesout.

using System;
using System.Text;

namespace LillTek.Common
{
    /// <summary>
    /// Exception thrown when a TimedLock.Lock() attempt timesout.
    /// </summary>
    public sealed class DeadlockException : ApplicationException, ICustomExceptionLogger
    {
        private TimedLock           tLock;
        private TimedLock.LockInfo  info;
        private CallStack           failStack;
        private string              internalInfo;

        /// <summary>
        /// Constructor.
        /// </summary>
        /// <param name="tLock">The lock instance.</param>
        /// <param name="failStack">Stack where the attemped lock failed.</param>
        /// <param name="info">Diagnostic information about the lock attempt (or <c>null</c>).</param>
        /// <param name="internalInfo">Additional internal debug information (or <c>null</c>).</param>
        internal DeadlockException(TimedLock tLock, CallStack failStack, TimedLock.LockInfo info, string internalInfo)
            : base(string.Format("Likely deadlock on [{0}] detected.", tLock.Target.GetType().FullName))
        {
            this.tLock       = tLock;
            this.failStack    = failStack;
            this.info         = info;
            this.internalInfo = internalInfo ?? string.Empty;
        }

        /// <summary>
        /// Returns the failed lock target instance.
        /// </summary>
        public object Target
        {
            get { return tLock.Target; }
        }

        /// <summary>
        /// Implements the ICustomExceptionLogger.Log() method.
        /// </summary>
        public void Log(StringBuilder sb)
        {
            if (info != null)
            {
                sb.Append(info.Dump());
            }
            else
            {
                sb.Append(failStack.ToString());
                sb.Append("\r\n");
                sb.Append(string.Format(@"

Information detailing where the lock was acquired is not available.
Try setting TimedLock.FullDiagnostics=true and implementing [ILockable]
for the target class [{0}].
", tLock.Target.GetType().FullName));
            }

            if (!string.IsNullOrWhiteSpace(internalInfo))
            {
                sb.Append("\r\n\r\n");
                sb.AppendFormatLine("Internal Info: {0}", internalInfo);
            }
        }
    }
}
