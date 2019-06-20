//-----------------------------------------------------------------------------
// FILE:        _Crc32.cs
// CONTRIBUTOR: Jeff Lill
// COPYRIGHT:   Copyright (c) 2005-2015 by Jeffrey Lill.  All rights reserved.
// DESCRIPTION: Unit tests

using System;

using Microsoft.VisualStudio.TestTools.UnitTesting;

using LillTek.Common;
using LillTek.Advanced;

namespace LillTek.Advanced.Test
{
    [TestClass]
    public class _Crc32
    {
        [TestMethod]
        [TestProperty("Lib", "LillTek.Advanced")]
        public void Crc32_Static()
        {
            Assert.AreNotEqual(0, Crc32.Compute(new byte[] { 0, 1, 2, 3, 4, 5 }));
            Assert.AreNotEqual(Crc32.Compute(new byte[] { 0, 1, 2, 3, 4, 5 }), Crc32.Compute(new byte[] { 0, 1, 2, 3, 4 }));
            Assert.AreEqual(Crc32.Compute(new byte[] { 1, 2, 3, 4, 5 }), Crc32.Compute(new byte[] { 0, 1, 2, 3, 4, 5 }, 1, 5));
            Assert.AreEqual(461707669, Crc32.Compute(Helper.ToAnsi("Hello world!")));
            Assert.AreEqual(-1959132156, Crc32.Compute(Helper.ToAnsi("Hello world.")));
        }

        [TestMethod]
        [TestProperty("Lib", "LillTek.Advanced")]
        public void Crc32_Dynamic()
        {
            Crc32 crc;

            crc = new Crc32();
            for (int i = 0; i <= 5; i++)
                crc.Update(new byte[] { (byte)i });

            Assert.AreEqual(Crc32.Compute(new byte[] { 0, 1, 2, 3, 4, 5 }), crc.Compute());

            crc = new Crc32();
            for (int i = 0; i <= 5; i++)
                crc.Update(new byte[] { (byte)i }, 0, 1);

            Assert.AreEqual(Crc32.Compute(new byte[] { 0, 1, 2, 3, 4, 5 }), crc.Compute());
        }

        [TestMethod]
        [TestProperty("Lib", "LillTek.Advanced")]
        public void Crc32_ReuseException()
        {
            Crc32 crc;

            crc = new Crc32();
            crc.Update(new byte[] { 0, 1, 2, 3, 4, 5 });
            crc.Compute();

            try
            {
                crc.Update(new byte[] { 0, 1, 2, 3, 4, 5 });
                Assert.Fail("Expected an InvalidOperationException");
            }
            catch (InvalidOperationException)
            {
                // Expecting this exception
            }

            try
            {
                crc.Compute();
                Assert.Fail("Expected an InvalidOperationException");
            }
            catch (InvalidOperationException)
            {
                // Expecting this exception
            }
        }
    }
}

