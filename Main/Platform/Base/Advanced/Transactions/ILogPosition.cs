//-----------------------------------------------------------------------------
// FILE:        ILogPosition.cs
// CONTRIBUTOR: Jeff Lill
// COPYRIGHT:   Copyright (c) 2005-2015 by Jeffrey Lill.  All rights reserved.
// DESCRIPTION: Describes the starting position of a serialized IOperation within a
//              IOperationLog.

using System;

using LillTek.Common;
using LillTek.Advanced;

namespace LillTek.Transactions
{
    /// <summary>
    /// Describes the starting position of a serialized <see cref="IOperation" /> within
    /// a <see cref="IOperationLog" />.  The actual implementation of this is specific 
    /// to <see cref="ITransactionLog" />.
    /// </summary>
    public interface ILogPosition
    {
    }
}
