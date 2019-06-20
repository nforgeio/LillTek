//-----------------------------------------------------------------------------
// FILE:        MemoryLogPosition.cs
// CONTRIBUTOR: Jeff Lill
// COPYRIGHT:   Copyright (c) 2005-2015 by Jeffrey Lill.  All rights reserved.
// DESCRIPTION: Describes the starting position of a serialized IOperation
//              within a MemoryeOperationLog.

using LillTek.Common;
using LillTek.Advanced;

namespace LillTek.Transactions
{
    /// <summary>
    /// Describes the starting position of an <see cref="IOperation" /> within a <see cref="MemoryOperationLog" />.
    /// </summary>
    internal sealed class MemoryLogPosition : ILogPosition
    {
        /// <summary>
        /// The index of the operation within the log.
        /// </summary>
        public int Index;

        /// <summary>
        /// Constructor.
        /// </summary>
        /// <param name="index">The index of the operation within the log.</param>
        public MemoryLogPosition(int index)
        {

            this.Index = index;
        }
    }
}
