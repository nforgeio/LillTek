//-----------------------------------------------------------------------------
// FILE:        BlockFile.cs
// CONTRIBUTOR: Jeff Lill
// COPYRIGHT:   Copyright (c) 2005-2015 by Jeffrey Lill.  All rights reserved.
// DESCRIPTION: Implements a class designed to randomly read and write fixed
//              size blocks over very large files.

using System;
using System.IO;

using LillTek.Common;

namespace LillTek.Advanced
{
    /// <summary>
    /// Implements a class designed to randomly read and write fixed size 
    /// blocks over very large files.
    /// </summary>
    /// <threadsafety instance="true" />
    public class BlockFile
    {

        private object      syncLock = new object();
        private FileStream  fs;         // The underlying stream
        private bool        isOpen;     // True if the file is open
        private int         cbBlock;    // Size of a file block

        /// <summary>
        /// Opens a BlockFile mapped to the specified file on the file system.
        /// </summary>
        /// <param name="path">Path to the file on the file system.</param>
        /// <param name="cbBlock">Size of a file block in bytes.</param>
        /// <param name="cAlloc">Number of blocks to preallocate (or 0).</param>
        /// <param name="mode">Specifies how the file is to be opened.</param>
        /// <remarks>
        /// <para>
        /// Note that for best performance cbBlock should selected such that it
        /// is either an integer multiple of an underlying file system block size or 
        /// that the file system's block be an integer multiple cbBlock.  
        /// Choosing a block size with this constraint avoids the possibility of the 
        /// underlying filesystem having to do an extra seek.
        /// </para>
        /// <para>
        /// When creating a block file, it is best to immediately specify the
        /// maximum number of blocks you expect to write to the file via the
        /// cAlloc parameter.  Doing this will help limit fragmentation of 
        /// the file on the file system.
        /// </para>
        /// </remarks>
        /// <seealso cref="StripedBlockFile"/>
        public BlockFile(string path, int cbBlock, int cAlloc, FileMode mode)
        {
            if (cbBlock <= 0)
                throw new ArgumentException("[cbBlock] must be greater than 0.");

            this.cbBlock = cbBlock;
            this.fs      = new FileStream(path, mode, FileAccess.ReadWrite, FileShare.None, cbBlock, true);

            if (cAlloc > 0)
            {
                try
                {
                    fs.SetLength((long)cbBlock * (long)cAlloc);
                }
                catch
                {
                    fs.Close();
                    fs = null;
                    throw;
                }
            }

            isOpen = true;
        }

        /// <summary>
        /// Closes the file if it's open.
        /// </summary>
        public void Close()
        {
            lock (syncLock)
            {
                if (isOpen)
                {
                    fs.Close();
                    isOpen = false;
                }
            }
        }

        /// <summary>
        /// Returns the block size for the file.
        /// </summary>
        public int BlockSize
        {
            get { return cbBlock; }
        }

        /// <summary>
        /// Synchronously reads block data.
        /// </summary>
        /// <param name="block">The zero-based block number to read.</param>
        /// <param name="buffer">The buffer to read the data into.</param>
        /// <param name="cbRead">The number of bytes to read.</param>
        /// <remarks>
        /// Note that a maximum of <see cref="BlockSize" /> bytes will be read from the block.
        /// Reads cannot be performed across blocks.
        /// </remarks>
        public void Read(int block, byte[] buffer, int cbRead)
        {
            var ar = BeginRead(block, buffer, cbRead, null, null);

            EndRead(ar);
        }

        /// <summary>
        /// Initiates an asynchronous operation to read block data.
        /// </summary>
        /// <param name="block">The zero-based block number to read.</param>
        /// <param name="buffer">The buffer to read the data into.</param>
        /// <param name="cbRead">The number of bytes to read.</param>
        /// <param name="callback">The delegate to be called when the operation completes (or <c>null</c>).</param>
        /// <param name="state">Application specific state (or <c>null</c>).</param>
        /// <returns>The <see cref="IAsyncResult" /> instance to be used to track the operation.</returns>
        /// <remarks>
        /// <para>
        /// Note that a maximum of <see cref="BlockSize" /> bytes will be read from the block.
        /// Reads cannot be performed across blocks.
        /// </para>
        /// <para>
        /// An exception will be thrown if the block number specified is past the
        /// end of the file.
        /// </para>
        /// </remarks>
        public IAsyncResult BeginRead(int block, byte[] buffer, int cbRead, AsyncCallback callback, object state)
        {
            int cb;

            cb = cbRead;
            if (cb > cbBlock)
                cb = cbBlock;

            lock (syncLock)
            {
                fs.Position = (long)block * (long)cbBlock;
                return fs.BeginRead(buffer, 0, cb, callback, state);
            }
        }

        /// <summary>
        /// Completes an asynchronous <see cref="BeginRead" /> operation.
        /// </summary>
        /// <param name="ar">The <see cref="IAsyncResult" /> instance returned by <see cref="BeginRead" />.</param>
        public void EndRead(IAsyncResult ar)
        {
            lock (syncLock)
                fs.EndRead(ar);
        }

        /// <summary>
        /// Synchronously writes block data.
        /// </summary>
        /// <param name="block">The zero-based block number to read.</param>
        /// <param name="buffer">The buffer of data to be written.</param>
        /// <param name="cbWrite">The number of bytes to write.</param>
        /// <remarks>
        /// <para>
        /// Note that a maximum of <see cref="BlockSize" /> bytes will be written to the block.
        /// Writes cannot be performed across blocks.
        /// </para>
        /// <para>
        /// An attempt to write to a block past the end of the file will extend
        /// the file's length as necessary to accomodate the request.
        /// </para>
        /// </remarks>
        public void Write(int block, byte[] buffer, int cbWrite)
        {
            var ar = BeginWrite(block, buffer, cbWrite, null, null);

            EndWrite(ar);
        }

        /// <summary>
        /// Initiates an asynchronous operation to write data to a block.
        /// </summary>
        /// <param name="block">The zero-based block number to read.</param>
        /// <param name="buffer">The buffer of data to be written.</param>
        /// <param name="cbWrite">The number of bytes to write.</param>
        /// <param name="callback">The delegate to be called when the operation completes (or <c>null</c>).</param>
        /// <param name="state">Application specific state (or <c>null</c>).</param>
        /// <returns>The <see cref="IAsyncResult" /> instance to be used to track the operation.</returns>
        /// <remarks>
        /// <para>
        /// Note that a maximum of <see cref="BlockSize" /> bytes will be written to the block.
        /// Writes cannot be performed across blocks.
        /// </para>
        /// <para>
        /// An attempt to write to a block past the end of the file will extend
        /// the file's length as necessary to accomodate the request.
        /// </para>
        /// </remarks>
        public IAsyncResult BeginWrite(int block, byte[] buffer, int cbWrite, AsyncCallback callback, object state)
        {

            long    cbFile;
            int     cb;

            cbFile = (long)(block + 1) * (long)cbBlock;
            if (fs.Length < cbFile)
                fs.SetLength(cbFile);

            cb = cbWrite;
            if (cb > cbBlock)
                cb = cbBlock;

            lock (syncLock)
            {
                fs.Position = (long)block * (long)cbBlock;
                return fs.BeginWrite(buffer, 0, cb, callback, state);
            }
        }

        /// <summary>
        /// Completes an asynchronous <see cref="BeginWrite" /> operation.
        /// </summary>
        /// <param name="ar">The <see cref="IAsyncResult" /> instance returned by <see cref="BeginWrite" />.</param>
        public void EndWrite(IAsyncResult ar)
        {
            lock (syncLock)
                fs.EndWrite(ar);
        }
    }
}
