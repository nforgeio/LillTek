//-----------------------------------------------------------------------------
// FILE:        StripedBlockFile.cs
// CONTRIBUTOR: Jeff Lill
// COPYRIGHT:   Copyright (c) 2005-2015 by Jeffrey Lill.  All rights reserved.
// DESCRIPTION: Implements a class designed to randomly read and write fixed
//              size blocks striped via software over a BlockFile instances.

using System;
using System.IO;

using LillTek.Common;

namespace LillTek.Advanced
{
    /// <summary>
    /// Implements a class designed to randomly read and write fixed size blocks 
    /// striped via software over a set of <see cref="BlockFile" /> instances.
    /// </summary>
    /// <threadsafety instance="true" />
    public class StripedBlockFile
    {
        private BlockFile[]     stripeFiles;
        private int             cStripes;
        private int             cbBlock;
        private AsyncCallback   onRead;
        private AsyncCallback   onWrite;

        /// <summary>
        /// Opens a <see cref="StripedBlockFile" /> mapped to the specified files on the 
        /// file system.
        /// </summary>
        /// <param name="paths">Paths to the block files on the file system.</param>
        /// <param name="cbBlock">Size of a file block in bytes.</param>
        /// <param name="cAlloc">Number of blocks to preallocate (or 0).</param>
        /// <param name="mode">Specifies how the file is to be opened.</param>
        /// <remarks>
        /// <para>
        /// This class is designed so that data blocks can be distributed evenly over
        /// files located on different physical hard drives to dramatically improve
        /// read/write performance, especially when using the simultaneous asynchronous 
        /// read/write calls on multiple threads.
        /// </para>
        /// <para>
        /// This is similiar to the performance advantage gained from deploying two drives 
        /// in a RAID0 configuration.  The problem with RAID0 striping is that it is
        /// available on low-cost servers only for 2 physical drives.  Implementing a
        /// striped array across more than two drives requires the costly addition of
        /// a high-end RAID controller board.  The <see cref="StripedBlockFile" /> class
        /// provides for easily configured striping across an unlimited number of physical
        /// drives using software only.
        /// </para>
        /// <para>
        /// Blocks are distributed across the files sequentially.  If the <see cref="StripedBlockFile" />
        /// is striped across four files, then the blocks will be allocated across the
        /// files as shown in the table below.
        /// </para>
        /// <code language="none">
        /// 
        ///         File 0          File 1          File 2          File 3
        ///         ------          ------          ------          ------
        ///   block:   0               1               2               3
        ///            4               5               6               7
        ///            8               9              10              11
        ///           ...             ...             ...             ...
        /// 
        /// </code>
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
        /// <seealso cref="BlockFile"/>
        public StripedBlockFile(string[] paths, int cbBlock, int cAlloc, FileMode mode)
        {
            int cAllocEach;

            if (paths.Length == 0)
                throw new ArgumentException("At least one file path must be specified.", "paths");

            // Calculate how many blocks we're going to need to preallocate for
            // each stripe file.

            if (cAlloc == 0)
                cAllocEach = 0;
            else
            {
                cAllocEach = cAlloc / paths.Length;
                if (cAllocEach * paths.Length < cAlloc)
                    cAllocEach++;
            }

            // Misc initialization

            this.cbBlock = cbBlock;
            this.onRead  = new AsyncCallback(OnRead);
            this.onWrite = new AsyncCallback(OnWrite);

            // Open the block stripe files

            this.cStripes = paths.Length;
            this.stripeFiles = new BlockFile[cStripes];

            try
            {
                for (int i = 0; i < cStripes; i++)
                    stripeFiles[i] = new BlockFile(paths[i], cbBlock, cAllocEach, mode);
            }
            catch
            {
                for (int i = 0; i < cStripes; i++)
                {
                    if (stripeFiles[i] != null)
                    {
                        stripeFiles[i].Close();
                        break;
                    }
                }

                throw;
            }
        }

        /// <summary>
        /// Closes the file if it's open.
        /// </summary>
        public void Close()
        {
            for (int i = 0; i < cStripes; i++)
                stripeFiles[i].Close();
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

            var         arOp = new AsyncResult(null, callback, state);
            BlockFile   bf;

            bf = stripeFiles[block % cStripes];
            arOp.Result = bf;

            bf.BeginRead(block / cStripes, buffer, cbRead, onRead, arOp);
            arOp.Started();

            return arOp;
        }

        /// <summary>
        /// Handles read completions.
        /// </summary>
        /// <param name="ar">The async result.</param>
        private void OnRead(IAsyncResult ar)
        {
            var arOp = (AsyncResult)ar.AsyncState;

            try
            {
                ((BlockFile)arOp.Result).EndRead(ar);
                arOp.Notify();
            }
            catch (Exception e)
            {
                arOp.Notify(e);
            }
        }

        /// <summary>
        /// Completes an asynchronous <see cref="BeginRead" /> operation.
        /// </summary>
        /// <param name="ar">The <see cref="IAsyncResult" /> instance returned by <see cref="BeginRead" />.</param>
        public void EndRead(IAsyncResult ar)
        {
            var arOp = (AsyncResult)ar;

            arOp.Wait();
            try
            {
                if (arOp.Exception != null)
                    throw arOp.Exception;
            }
            finally
            {
                arOp.Dispose();
            }
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
            var         arOp = new AsyncResult(null, callback, state);
            BlockFile   bf;

            bf = stripeFiles[block % cStripes];
            arOp.Result = bf;

            bf.BeginWrite(block / cStripes, buffer, cbWrite, onWrite, arOp);
            arOp.Started();

            return arOp;
        }

        /// <summary>
        /// Handles write completions.
        /// </summary>
        /// <param name="ar">The async result.</param>
        private void OnWrite(IAsyncResult ar)
        {
            var arOp = (AsyncResult)ar.AsyncState;

            try
            {
                ((BlockFile)arOp.Result).EndWrite(ar);
                arOp.Notify();
            }
            catch (Exception e)
            {
                arOp.Notify(e);
            }
        }

        /// <summary>
        /// Completes an asynchronous <see cref="BeginWrite" /> operation.
        /// </summary>
        /// <param name="ar">The <see cref="IAsyncResult" /> instance returned by <see cref="BeginWrite" />.</param>
        public void EndWrite(IAsyncResult ar)
        {
            var arOp = (AsyncResult)ar;

            arOp.Wait();
            try
            {
                if (arOp.Exception != null)
                    throw arOp.Exception;
            }
            finally
            {
                arOp.Dispose();
            }
        }
    }
}
