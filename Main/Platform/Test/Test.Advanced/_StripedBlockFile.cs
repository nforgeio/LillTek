//-----------------------------------------------------------------------------
// FILE:        _StripedBlockFile.cs
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
    public class _StripedBlockFile
    {
        private const int cBlocks = 5000;
        private const int cbBlock = 4096;

        private string[] files;

        private string[] GetFiles()
        {
            string prefix;

            prefix = Path.GetTempPath() + Helper.NewGuid().ToString();
            files = new string[] {

                prefix + "-0.blocks",
                prefix + "-1.blocks",
                prefix + "-2.blocks",
                prefix + "-3.blocks"
            };

            return files;
        }

        private void DeleteFiles()
        {
            foreach (string file in files)
                if (File.Exists(file))
                    File.Delete(file);
        }

        [TestMethod]
        [TestProperty("Lib", "LillTek.Advanced")]
        public void StripedBlockFile_ReadWrite_Forward()
        {
            StripedBlockFile bf;
            byte[] buf = new byte[cbBlock];
            byte[] test = new byte[cbBlock];

            bf = new StripedBlockFile(GetFiles(), cbBlock, 0, FileMode.Create);

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

                DeleteFiles();
            }
        }

        [TestMethod]
        [TestProperty("Lib", "LillTek.Advanced")]
        public void StripedBlockFile_ReadWrite_Reverse()
        {
            StripedBlockFile bf;
            byte[] buf = new byte[cbBlock];
            byte[] test = new byte[cbBlock];

            bf = new StripedBlockFile(GetFiles(), cbBlock, 0, FileMode.Create);

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

                DeleteFiles();
            }
        }

        [TestMethod]
        [TestProperty("Lib", "LillTek.Advanced")]
        public void StripedBlockFile_Preallocate()
        {
            StripedBlockFile bf;
            byte[] buf = new byte[cbBlock];
            byte[] test = new byte[cbBlock];

            bf = new StripedBlockFile(GetFiles(), cbBlock, cBlocks, FileMode.Create);

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

                DeleteFiles();
            }
        }

        [TestMethod]
        [TestProperty("Lib", "LillTek.Advanced")]
        public void StripedBlockFile_Async()
        {
            StripedBlockFile bf;
            byte[] test = new byte[cbBlock];
            IAsyncResult[] ars = new IAsyncResult[cBlocks];
            byte[][] bufs;

            bf = new StripedBlockFile(GetFiles(), cbBlock, cBlocks, FileMode.Create);

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

                DeleteFiles();
            }
        }
    }
}

