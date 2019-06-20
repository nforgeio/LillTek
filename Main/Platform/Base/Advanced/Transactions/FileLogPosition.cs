//-----------------------------------------------------------------------------
// FILE:        FileLogPosition.cs
// CONTRIBUTOR: Jeff Lill
// COPYRIGHT:   Copyright (c) 2005-2015 by Jeffrey Lill.  All rights reserved.
// DESCRIPTION: Describes the starting position of a serialized IOperation
//              within a FileOperationLog.

using System;

using LillTek.Common;
using LillTek.Advanced;

namespace LillTek.Transactions
{
    /// <summary>
    /// Describes the starting position of an <see cref="IOperation" /> within a <see cref="FileOperationLog" />.
    /// </summary>
    internal sealed class FileLogPosition : ILogPosition
    {
        /// <summary>
        /// The byte offset of the operation within the log file.
        /// </summary>
        public long Position;

        /// <summary>
        /// Constructor.
        /// </summary>
        /// <param name="position">The byte offset of the operation within the log file.</param>
        public FileLogPosition(long position)
        {
            this.Position = position;
        }
    }
}
