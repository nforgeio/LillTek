//-----------------------------------------------------------------------------
// FILE:        _RingBuffer.cs
// CONTRIBUTOR: Jeff Lill
// COPYRIGHT:   Copyright (c) 2005-2015 by Jeffrey Lill.  All rights reserved.
// DESCRIPTION: UNIT tests for the Fixedbuffer class

using System;

using Microsoft.VisualStudio.TestTools.UnitTesting;

using LillTek.Common;
using LillTek.Testing;

namespace LillTek.Common.Test
{
    [TestClass]
    public class _RingBuffer
    {
        [TestMethod]
        [TestProperty("Lib", "LillTek.Common")]
        public void RingBuffer_Basic()
        {
            RingBuffer<int> buffer;

            buffer = new RingBuffer<int>(3);
            Assert.AreEqual(0, buffer.Count);

            buffer.Add(1);
            Assert.AreEqual(1, buffer.Count);
            Assert.AreEqual(1, buffer[0]);

            buffer.Add(2);
            Assert.AreEqual(2, buffer.Count);
            Assert.AreEqual(2, buffer[0]);
            Assert.AreEqual(1, buffer[1]);

            buffer.Add(3);
            Assert.AreEqual(3, buffer.Count);
            Assert.AreEqual(3, buffer[0]);
            Assert.AreEqual(2, buffer[1]);
            Assert.AreEqual(1, buffer[2]);

            buffer.Add(4);
            Assert.AreEqual(3, buffer.Count);
            Assert.AreEqual(4, buffer[0]);
            Assert.AreEqual(3, buffer[1]);
            Assert.AreEqual(2, buffer[2]);

            buffer.Add(5);
            Assert.AreEqual(3, buffer.Count);
            Assert.AreEqual(5, buffer[0]);
            Assert.AreEqual(4, buffer[1]);
            Assert.AreEqual(3, buffer[2]);

            buffer.Add(6);
            Assert.AreEqual(3, buffer.Count);
            Assert.AreEqual(6, buffer[0]);
            Assert.AreEqual(5, buffer[1]);
            Assert.AreEqual(4, buffer[2]);

            buffer.Add(7);
            Assert.AreEqual(3, buffer.Count);
            Assert.AreEqual(7, buffer[0]);
            Assert.AreEqual(6, buffer[1]);
            Assert.AreEqual(5, buffer[2]);
        }
    }
}

