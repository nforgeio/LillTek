//-----------------------------------------------------------------------------
// FILE:        _Block.cs
// CONTRIBUTOR: Jeff Lill
// COPYRIGHT:   Copyright (c) 2005-2015 by Jeffrey Lill. All rights reserved.
// DESCRIPTION: UNIT tests for the Block class.

using System;

using Microsoft.VisualStudio.TestTools.UnitTesting;

using LillTek.Common;
using LillTek.Testing;

namespace LillTek.Common.Test
{
    [TestClass]
    public class _Block
    {
        [TestMethod]
        [TestProperty("Lib", "LillTek.Common")]
        public void Block_Construction()
        {
            Block block;

            block = new Block(new byte[10], 0, 10);
            Assert.AreEqual(10, block.Buffer.Length);
            Assert.AreEqual(0, block.Offset);
            Assert.AreEqual(10, block.Length);

            block = new Block(new byte[10], 2, 8);
            Assert.AreEqual(10, block.Buffer.Length);
            Assert.AreEqual(2, block.Offset);
            Assert.AreEqual(8, block.Length);

            block = new Block(new byte[10]);
            Assert.AreEqual(0, block.Offset);
            Assert.AreEqual(10, block.Length);

            block = new Block(10);
            Assert.AreEqual(0, block.Offset);
            Assert.AreEqual(10, block.Length);
        }

        [TestMethod]
        [TestProperty("Lib", "LillTek.Common")]
        public void Block_Indexing()
        {
            Block block;

            block = new Block(10);
            for (int i = 0; i < 10; i++)
                Assert.AreEqual(0, block[i]);

            for (int i = 0; i < block.Length; i++)
                block[i] = (byte)i;

            for (int i = 0; i < block.Length; i++)
                Assert.AreEqual((byte)i, block[i]);
        }

        [TestMethod]
        [TestProperty("Lib", "LillTek.Common")]
        public void Block_CopyTo()
        {
            Block block;
            byte[] arr = new byte[10];

            block = new Block(20);
            for (int i = 0; i < block.Length; i++)
                block[i] = (byte)i;

            for (int i = 0; i < arr.Length; i++)
                arr[i] = 0;

            block.CopyTo(0, arr, 0, 10);
            CollectionAssert.AreEqual(new byte[] { 0, 1, 2, 3, 4, 5, 6, 7, 8, 9 }, arr);

            block.CopyTo(5, arr, 0, 5);
            CollectionAssert.AreEqual(new byte[] { 5, 6, 7, 8, 9, 5, 6, 7, 8, 9 }, arr);

            block.SetRange(10, 10);
            block.CopyTo(0, arr, 0, 10);
            CollectionAssert.AreEqual(new byte[] { 10, 11, 12, 13, 14, 15, 16, 17, 18, 19 }, arr);

            block.CopyTo(5, arr, 0, 5);
            CollectionAssert.AreEqual(new byte[] { 15, 16, 17, 18, 19, 15, 16, 17, 18, 19 }, arr);
        }

        [TestMethod]
        [TestProperty("Lib", "LillTek.Common")]
        public void Block_CopyFrom()
        {
            Block block;
            byte[] arr = new byte[] { 0, 1, 2, 3, 4, 5, 6, 7, 8, 9 };

            block = new Block(20);
            for (int i = 0; i < 20; i++)
                Assert.AreEqual(0, block[i]);

            block.CopyFrom(arr, 0, 0, 10);
            for (int i = 0; i < 20; i++)
            {
                if (i < 10)
                    Assert.AreEqual(arr[i], block[i]);
                else
                    Assert.AreEqual(0, block[i]);
            }

            block.CopyFrom(arr, 0, 10, 10);
            for (int i = 0; i < 20; i++)
            {
                if (i < 10)
                    Assert.AreEqual(arr[i], block[i]);
                else
                    Assert.AreEqual(i - 10, block[i]);
            }

            block = new Block(new byte[20], 10, 10);

            block.CopyFrom(arr, 0, 5, 5);
            for (int i = 0; i < 10; i++)
            {
                if (i < 5)
                    Assert.AreEqual(0, block[i]);
                else
                    Assert.AreEqual(i - 5, block[i]);
            }
        }
    }
}

