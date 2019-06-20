//-----------------------------------------------------------------------------
// FILE:        CallStack.cs
// CONTRIBUTOR: Jeff Lill
// COPYRIGHT:   Copyright (c) 2005-2015 by Jeffrey Lill.  All rights reserved.
// DESCRIPTION: Extends the StackTrace class with a Dump() method.

using System;
using System.Diagnostics;
using System.Reflection;
using System.Security.Permissions;
using System.Text;

namespace LillTek.Common
{
#if MOBILE_DEVICE
    /// <summary>
    /// Extends the .NET <see cref="StackTrace" /> class with a <see cref="Dump()" /> method .
    /// </summary>
#else
    /// <summary>
    /// Extends the .NET <see cref="StackTrace" /> class with a <see cref="Dump()" /> method and also provides some
    /// integration with the <see cref="AsyncTracker" /> class.
    /// </summary>
    [SecurityPermission(SecurityAction.InheritanceDemand, UnmanagedCode = true)]
#endif
    public class CallStack : StackTrace
    {
        //---------------------------------------------------------------------
        // Static members

        /// <summary>
        /// Dummy callstack returned when AsyncTracker is not running.
        /// </summary>
        private static CallStack Dummy = DummyStackTrace();

        /// <summary>
        /// Returns the dummy call stack.
        /// </summary>
        /// <returns>The <see cref="CallStack" />.</returns>
        private static CallStack DummyStackTrace()
        {
            // I'm doing this here so the method name "DummStackTrace" will appear
            // at the top of the dump, making it easy to see what's going on if
            // tracing is disabled when looking at an event log, etc.

            return new CallStack(0, false);
        }

#if MOBILE_DEVICE
        /// <summary>
        /// Returns a dummy stack frame.
        /// </summary>
        /// <param name="skipFrames">Number of frames to skip.</param>
        /// <param name="fNeedFileInfo"><c>true</c> to gather file and line number information if present.</param>
#else
        /// <summary>
        /// Returns a stack trace from the current stack frame for use by 
        /// <see cref="AsyncTracker" /> and <see cref="AsyncResult{TResult,TInternalState}" />.  
        /// This will return a dummy frame if <see cref="AsyncTracker.GatherCallStacks" /> is not <c>true</c>.
        /// </summary>
        /// <param name="skipFrames">Number of frames to skip.</param>
        /// <param name="fNeedFileInfo"><c>true</c> to gather file and line number information if present.</param>
#endif
        public static CallStack AsyncTrace(int skipFrames, bool fNeedFileInfo)
        {
#if !MOBILE_DEVICE
            if (AsyncTracker.GatherCallStacks)
                return new CallStack(skipFrames + 1, fNeedFileInfo);
            else
                return Dummy;
#else
            return Dummy;
#endif
        }

        //---------------------------------------------------------------------
        // Instance members

        /// <summary>
        /// Default constructor.
        /// </summary>
        public CallStack()
            : base()
        {
        }

        /// <summary>
        /// Initializes a new instance from the caller's stack frame, skipping the specified
        /// number of frames.
        /// </summary>
        /// <param name="skipFrames">Number of frames to skip.</param>
        /// <param name="fNeedFileInfo"><c>true</c> to gather file and line number information if present.</param>
        [System.Security.SecuritySafeCritical]
        public CallStack(int skipFrames, bool fNeedFileInfo)
            : base(skipFrames + 1, fNeedFileInfo)
        {
        }

        /// <summary>
        /// Initializes a new instance from exception.
        /// </summary>
        /// <param name="e">The exception.</param>
        /// <param name="fNeedFileInfo"><c>true</c> to gather file and line number information if present.</param>
        [System.Security.SecuritySafeCritical]
        public CallStack(Exception e, bool fNeedFileInfo)
            : base(e, fNeedFileInfo)
        {
        }

        /// <summary>
        /// Writes the stack trace in human readable form to the string builder.
        /// </summary>
        /// <param name="sb">The string builder.</param>
        public void Dump(StringBuilder sb)
        {
            if (object.ReferenceEquals(this, Dummy))
            {
                sb.AppendLine("*** Dummy Stack ***");
                return;
            }

            for (int i = 0; i < this.FrameCount; i++)
            {
                StackFrame      frame    = this.GetFrame(i);
                MethodBase      method   = frame.GetMethod();
                Type            type     = method.DeclaringType;
                string          typeName = type != null ? type.FullName : "[unknown]";
                string          fileName;
                int             lineNum;

                fileName = frame.GetFileName();
                lineNum  = frame.GetFileLineNumber();

                if (fileName == string.Empty)
                    fileName = null;

                if (fileName == null)
                    sb.AppendFormat("   at {0}.{1}()\r\n", typeName, method.Name);
                else
                    sb.AppendFormat("   at {0}.{1}() {2}:{3}\r\n", typeName, method.Name, fileName, lineNum);
            }
        }

        /// <summary>
        /// Dumps the stack trace to the system log.
        /// </summary>
        public void Dump()
        {
            var sb = new StringBuilder();

            Dump(sb);
            SysLog.Trace("AsyncTracker", SysLogLevel.Verbose, sb.ToString());
        }

        /// <summary>
        /// Renders the call stack into a human readable string.
        /// </summary>
        /// <returns>The rendered stack.</returns>
        public override string ToString()
        {
            var sb = new StringBuilder(512);

            Dump(sb);
            return sb.ToString();
        }
    }
}
