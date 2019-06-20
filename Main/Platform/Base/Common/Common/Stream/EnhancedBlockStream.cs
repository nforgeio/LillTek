//-----------------------------------------------------------------------------
// FILE:        EnhancedBlockStream.cs
// CONTRIBUTOR: Jeff Lill
// COPYRIGHT:   Copyright (c) 2005-2015 by Jeffrey Lill.  All rights reserved.
// DESCRIPTION: Implements an enhanced BlockStream.

using System;
using System.IO;
using System.Text;

// $todo(jeff.lill): Implement all [*Async()] method overrides.

namespace LillTek.Common
{
    /// <summary>
    /// Implements an enhanced BlockStream.
    /// </summary>
    public sealed class EnhancedBlockStream : EnhancedStream
    {

        private BlockStream innerStream;

        /// <summary>
        /// Constructs a zero length stream with default block size.
        /// </summary>
        public EnhancedBlockStream()
            : base(new BlockStream())
        {
            innerStream = (BlockStream)base.InnerStream;
        }

        /// <summary>
        /// Constructs a stream of the specified size using the default
        /// block size.
        /// </summary>
        /// <param name="size">The stream size in bytes.</param>
        public EnhancedBlockStream(int size)
            : base(new BlockStream(size))
        {
            innerStream = (BlockStream)base.InnerStream;
        }

        /// <summary>
        /// Constructs a stream of the specified size using the 
        /// specified block size.
        /// </summary>
        /// <param name="size">The stream size in bytes.</param>
        /// <param name="blockSize">The block size in bytes.</param>
        public EnhancedBlockStream(int size, int blockSize)
            : base(new BlockStream(size, blockSize))
        {
            innerStream = (BlockStream)base.InnerStream;
        }

        /// <summary>
        /// Constructs a stream of the specified size using the 
        /// specified block size and offset.
        /// </summary>
        /// <param name="size">The stream size in bytes.</param>
        /// <param name="blockSize">The block size in bytes.</param>
        /// <param name="blockOffset">Bytes to be reserved at the beginning of each new block.</param>
        /// <remarks>
        /// See <see cref="LillTek.Common.BlockArray"/> for more information on
        /// the value and use of the blockOffset prarmeter.
        /// </remarks>
        public EnhancedBlockStream(int size, int blockSize, int blockOffset)
            : base(new BlockStream(size, blockSize, blockOffset))
        {
            innerStream = (BlockStream)base.InnerStream;
        }

        /// <summary>
        /// Constructs a stream from the blocks passed.
        /// </summary>
        /// <param name="blocks">The blocks.</param>
        /// <remarks>
        /// The stream size will be set to the size of the blocks.
        /// </remarks>
        public EnhancedBlockStream(BlockArray blocks)
            : base(new BlockStream(blocks))
        {
            innerStream = (BlockStream)base.InnerStream;
        }

        /// <summary>
        /// Constructs a stream from the blocks passed.
        /// </summary>
        /// <param name="blocks">The blocks.</param>
        /// <remarks>
        /// The stream size will be set to the size of the blocks.
        /// </remarks>
        public EnhancedBlockStream(params Block[] blocks)
            : base(new BlockStream(blocks))
        {
            innerStream = (BlockStream)base.InnerStream;
        }

        /// <summary>
        /// Constructs a stream from a byte array..
        /// </summary>
        /// <param name="buffer">The byte array.</param>
        /// <remarks>
        /// The stream size will be set to the size of the array.
        /// </remarks>
        public EnhancedBlockStream(byte[] buffer)
            : base(new BlockStream(buffer))
        {
            innerStream = (BlockStream)base.InnerStream;
        }

        /// <summary>
        /// Appends a block to the end of the underlying BlockArray.
        /// </summary>
        /// <param name="block">The block to append.</param>
        /// <remarks>
        /// <para>
        /// The underyling block array's <see cref="BlockArray.SetExactSize" /> 
        /// method will be called before appending the block.  The stream 
        /// position will be set to the end of the stream before the method returns.
        /// </para>
        /// <para>
        /// This method is a performance improvement over writing the
        /// a buffer to the stream via one of the write methods.
        /// </para>
        /// </remarks>
        public void Append(Block block)
        {
            innerStream.Append(block);
        }

        /// <summary>
        /// Appends a block array to the end of the underlying BlockArray.
        /// </summary>
        /// <param name="blocks">The array to append.</param>
        /// <remarks>
        /// <para>
        /// The underyling block array's S<see cref="BlockArray.SetExactSize" />
        /// method will be called before appending the block.  The stream 
        /// position will be set to the end of the stream before the method 
        /// returns.
        /// </para>
        /// <para>
        /// This method is a performance improvement over writing the
        /// a buffer to the stream via one of the write methods.
        /// </para>
        /// </remarks>
        public void Append(BlockArray blocks)
        {
            innerStream.Append(blocks);
        }

        /// <summary>
        /// Returns the unmodified underlying buffer array.
        /// </summary>
        public BlockArray RawBlockArray
        {
            get { return innerStream.RawBlockArray; }
        }

        /// <summary>
        /// Returns the underlying block array.
        /// </summary>
        /// <param name="truncate">
        /// <c>true</c> if the method will truncate the underlying BlockArray
        /// to the actual length of the stream before returning the array.
        /// </param>
        public BlockArray ToBlocks(bool truncate)
        {
            return innerStream.ToBlocks(truncate);
        }

        /// <summary>
        /// Assembles a contiguous a byte array from the underlying
        /// block array.
        /// </summary>
        /// <returns>The assembled byte array.</returns>
        public byte[] ToArray()
        {
            return innerStream.ToArray();
        }

        /// <summary>
        /// Returns requested bytes from the underlying block array as
        /// as a new block array.
        /// </summary>
        /// <param name="cb">The nunber of bytes to read.</param>
        /// <returns>
        /// A new block array referencing the requested bytes in the
        /// same underlying buffers as managed by then stream.
        /// </returns>
        /// <remarks>
        /// <para>
        /// This provides a high performance way for code that knows
        /// how to handle block arrays to extract a portion of a stream.
        /// </para>
        /// <para>
        /// The array returned will be truncated to the length of the
        /// underlying stream.  The stream position will be advanced
        /// past the requested bytes.
        /// </para>
        /// </remarks>
        public BlockArray ReadBlocks(int cb)
        {
            return innerStream.ReadBlocks(cb);
        }
    }
}
