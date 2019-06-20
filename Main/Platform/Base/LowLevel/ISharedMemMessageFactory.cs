//-----------------------------------------------------------------------------
// FILE:        ISharedMemMessageFactory.cs
// CONTRIBUTOR: Jeff Lill
// COPYRIGHT:   Copyright (c) 2005-2015 by Jeffrey Lill.  All rights reserved.
// DESCRIPTION: Implemented by applications to construct application specific
//              [SharedMemMessage]s.

using System;
using System.Collections;
using System.Diagnostics;
using System.IO.MemoryMappedFiles;
using System.Threading;

using LillTek.Common;

namespace LillTek.LowLevel
{
    /// <summary>
    /// Implemented by applications to construct application specific
    /// <see cref="SharedMemMessage"/>s.
    /// </summary>
    public interface ISharedMemMessageFactory
    {
        /// <summary>
        /// Constructs an unitialized <see cref="SharedMemMessage"/> that corresponds to the
        /// message type code passed.
        /// </summary>
        /// <param name="typeCode">The message type code.</param>
        /// <returns>The constructed <see cref="SharedMemMessage"/>.</returns>
        /// <remarks>
        /// <note>
        /// <see cref="SharedMemClient{TMessageFactory}"/> and <see cref="SharedMemServer{TMessageFactory}"/>
        /// use this class to construct a received message instance just before deserialzing it via a call
        /// to <see cref="SharedMemMessage"/>.<see cref="SharedMemMessage.ReadFrom"/>.
        /// </note>
        /// <note>
        /// Type codes with values less than zero are reserved by the platform.
        /// </note>
        /// </remarks>
        SharedMemMessage Create(int typeCode);
    }
}
