//-----------------------------------------------------------------------------
// FILE:        EnhancedMemoryStream.cs
// CONTRIBUTOR: Jeff Lill
// COPYRIGHT:   Copyright (c) 2005-2015 by Jeffrey Lill.  All rights reserved.
// DESCRIPTION: Implements an enhanced memory stream.

using System;
using System.IO;
using System.Text;

// $todo(jeff.lill): Implement all [*Async()] method overrides.

namespace LillTek.Common
{
    /// <summary>
    /// Implements an enhanced memory stream.
    /// </summary>
    public sealed class EnhancedMemoryStream : EnhancedStream
    {
        private MemoryStream innerStream;

        /// <summary>
        /// Constructs an enhanced resizable memory stream.
        /// </summary>
        public EnhancedMemoryStream()
            : base(new MemoryStream())
        {
            innerStream = (MemoryStream)base.InnerStream;
        }

        /// <summary>
        /// Constructs a resizable memory stream with the specified initial capacity.
        /// </summary>
        /// <param name="capacity">The initial capacity in bytes.</param>
        public EnhancedMemoryStream(int capacity)
            : base(new MemoryStream(capacity))
        {
            innerStream = (MemoryStream)base.InnerStream;
        }

        /// <summary>
        /// Constructs a non-resizable memory stream from the buffer passed.
        /// </summary>
        /// <param name="buf">The buffer.</param>
        public EnhancedMemoryStream(byte[] buf)
            : base(new MemoryStream(buf))
        {
            innerStream = (MemoryStream)base.InnerStream;
        }

        /// <summary>
        /// Constructs a non-resizable memory stream from the buffer passed.
        /// </summary>
        /// <param name="buf">The buffer.</param>
        /// <param name="length">The initial length of the stream.</param>
        public EnhancedMemoryStream(byte[] buf, int length)
            : base(new MemoryStream(buf))
        {
            innerStream = (MemoryStream)base.InnerStream;
            innerStream.SetLength(length);
        }

        /// <summary>
        /// Returns the contents of the stream as an array of bytes.
        /// </summary>
        /// <returns>The content bytes.</returns>
        public byte[] ToArray()
        {
            return innerStream.ToArray();
        }

        /// <summary>
        /// Returns the buffer used by the underlying stream.
        /// </summary>
        /// <returns>The buffer.</returns>
        public byte[] GetBuffer()
        {
            return innerStream.GetBuffer();
        }
    }
}
