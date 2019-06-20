//-----------------------------------------------------------------------------
// FILE:        _BlockFile.cs
// CONTRIBUTOR: Jeff Lill
// COPYRIGHT:   Copyright (c) 2005-2015 by Jeffrey Lill.  All rights reserved.
// DESCRIPTION: Unit tests

using System;
using System.IO;

using Microsoft.VisualStudio.TestTools.UnitTesting;

using LillTek.Common;
using LillTek.Advanced;

namespace LillTek.Advanced.Test
{
    [TestClass]
    public class _BlockFile
    {
        const int cBlocks = 5000;
        const int cbBlock = 4096;

        [TestMethod]
        [TestProperty("Lib", "LillTek.Advanced")]
        public void BlockFile_ReadWrite_Forward()
        {
            BlockFile bf;
            string path;
            byte[] buf = new byte[cbBlock];
            byte[] test = new byte[cbBlock];

            path = Path.GetTempPath() + Helper.NewGuid().ToString() + ".blocks";
            bf = new BlockFile(path, cbBlock, 0, FileMode.Create);

            try
            {
                // Write blocks forward from block 0 on and then
                // read them back stepping forward as well.

                for (int i = 0; i < cBlocks; i++)
                {
                    for (int j = 0; j < cbBlock; j++)
                        buf[j] = (byte)(j + i);

                    bf.Write(i, buf, cbBlock);
                }

                for (int i = 0; i < cBlocks; i++)
                {
                    for (int j = 0; j < cbBlock; j++)
                        test[j] = (byte)(j + i);

                    bf.Read(i, buf, cbBlock);
                    CollectionAssert.AreEqual(test, buf);
                }
            }
            finally
            {
                if (bf != null)
                    bf.Close();

                if (File.Exists(path))
                    File.Delete(path);
            }
        }

        [TestMethod]
        [TestProperty("Lib", "LillTek.Advanced")]
        public void BlockFile_ReadWrite_Reverse()
        {
            BlockFile bf;
            string path;
            byte[] buf = new byte[cbBlock];
            byte[] test = new byte[cbBlock];

            path = Path.GetTempPath() + Helper.NewGuid().ToString() + ".blocks";
            bf = new BlockFile(path, cbBlock, 0, FileMode.Create);

            try
            {
                // Write blocks backwards read them back stepping backwards
                // as well.

                for (int i = cBlocks - 1; i >= 0; i--)
                {
                    for (int j = 0; j < cbBlock; j++)
                        buf[j] = (byte)(j + i + 7);

                    bf.Write(i, buf, cbBlock);
                }

                for (int i = cBlocks - 1; i >= 0; i--)
                {
                    for (int j = 0; j < cbBlock; j++)
                        test[j] = (byte)(j + i + 7);

                    bf.Read(i, buf, cbBlock);
                    CollectionAssert.AreEqual(test, buf);
                }
            }
            finally
            {
                if (bf != null)
                    bf.Close();

                if (File.Exists(path))
                    File.Delete(path);
            }
        }

        [TestMethod]
        [TestProperty("Lib", "LillTek.Advanced")]
        public void BlockFile_Preallocate()
        {
            BlockFile bf;
            string path;
            byte[] buf = new byte[cbBlock];
            byte[] test = new byte[cbBlock];

            path = Path.GetTempPath() + Helper.NewGuid().ToString() + ".blocks";
            bf = new BlockFile(path, cbBlock, cBlocks, FileMode.Create);

            try
            {
                // Write blocks forward from block 0 on and then
                // read them back stepping forward as well.

                for (int i = 0; i < cBlocks; i++)
                {
                    for (int j = 0; j < cbBlock; j++)
                        buf[j] = (byte)(j + i);

                    bf.Write(i, buf, cbBlock);
                }

                for (int i = 0; i < cBlocks; i++)
                {
                    for (int j = 0; j < cbBlock; j++)
                        test[j] = (byte)(j + i);

                    bf.Read(i, buf, cbBlock);
                    CollectionAssert.AreEqual(test, buf);
                }
            }
            finally
            {
                if (bf != null)
                    bf.Close();

                if (File.Exists(path))
                    File.Delete(path);
            }
        }

        [TestMethod]
        [TestProperty("Lib", "LillTek.Advanced")]
        public void BlockFile_Async()
        {
            BlockFile bf;
            string path;
            byte[] test = new byte[cbBlock];
            IAsyncResult[] ars = new IAsyncResult[cBlocks];
            byte[][] bufs;

            path = Path.GetTempPath() + Helper.NewGuid().ToString() + ".blocks";
            bf = new BlockFile(path, cbBlock, cBlocks, FileMode.Create);

            bufs = new byte[cBlocks][];
            for (int i = 0; i < cBlocks; i++)
                bufs[i] = new byte[cbBlock];

            try
            {
                // Initiate asynchronous write operations for all of
                // the blocks.

                for (int i = 0; i < cBlocks; i++)
                    for (int j = 0; j < cbBlock; j++)
                        bufs[i][j] = (byte)(j + i);

                for (int i = 0; i < cBlocks; i++)
                    ars[i] = bf.BeginWrite(i, bufs[i], cbBlock, null, null);

                // Wait for all of the writes to complete.

                for (int i = 0; i < cBlocks; i++)
                    bf.EndWrite(ars[i]);

                // Clear the buffers

                for (int i = 0; i < cBlocks; i++)
                    for (int j = 0; j < cbBlock; j++)
                        bufs[i][j] = 0;

                // Now go back and initiate async operations to read the blocks
                // back.

                for (int i = cBlocks - 1; i >= 0; i--)
                    ars[i] = bf.BeginRead(i, bufs[i], cbBlock, null, null);

                // Wait for all of the reads to complete.

                for (int i = 0; i < cBlocks; i++)
                    bf.EndRead(ars[i]);

                // Now verify the data read.

                for (int i = 0; i < cBlocks; i++)
                {
                    for (int j = 0; j < cbBlock; j++)
                        test[j] = (byte)(j + i);

                    CollectionAssert.AreEqual(test, bufs[i]);
                }
            }
            finally
            {
                if (bf != null)
                    bf.Close();

                if (File.Exists(path))
                    File.Delete(path);
            }
        }
    }
}

