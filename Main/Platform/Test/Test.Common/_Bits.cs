//-----------------------------------------------------------------------------
// FILE:        _Bits.cs
// CONTRIBUTOR: Jeff Lill
// COPYRIGHT:   Copyright (c) 2005-2015 by Jeffrey Lill.  All rights reserved.
// DESCRIPTION: UNIT tests for the Config class.

using System;
using System.Text;
using System.Diagnostics;

using Microsoft.VisualStudio.TestTools.UnitTesting;

using LillTek.Common;
using LillTek.Testing;

namespace LillTek.Common.Test
{
    [TestClass]
    public class _Bits
    {
        [TestMethod]
        [TestProperty("Lib", "LillTek.Common")]
        public void Bits_Basic()
        {
            Bits b1;
            Bits b2;
            Bits b3;

            b1 = new Bits(10);
            Assert.AreEqual(10, b1.Length);
            Assert.IsTrue(b1.IsAllZeros);
            Assert.IsFalse(b1.IsAllOnes);

            for (int i = 0; i < 10; i++)
                Assert.IsFalse(b1[i]);

            b1 = new Bits(new bool[] { true, false, true, true });
            Assert.AreEqual(4, b1.Length);
            Assert.IsTrue(b1[0]);
            Assert.IsFalse(b1[1]);
            Assert.IsTrue(b1[2]);
            Assert.IsTrue(b1[3]);
            Assert.IsFalse(b1.IsAllZeros);
            Assert.IsFalse(b1.IsAllOnes);

            b1 = new Bits(10);
            b2 = b1.Not();
            Assert.AreEqual(10, b2.Length);
            Assert.IsTrue(b2.IsAllOnes);

            b2 = new Bits(10);
            for (int i = 0; i < 10; i++)
                b2[i] = true;

            b3 = b1.Or(b2);
            Assert.AreEqual(10, b3.Length);
            Assert.IsTrue(b3.IsAllOnes);

            b1 = (Bits)"010100001111";
            Assert.AreEqual(12, b1.Length);
            Assert.IsFalse(b1[0]);
            Assert.IsTrue(b1[1]);
            Assert.IsFalse(b1[2]);
            Assert.IsTrue(b1[11]);

            Assert.AreEqual("010100001111", b1.ToString());
            Assert.AreEqual("010100001111", (string)b1);

            Assert.IsTrue(new Bits("000000000000000000000000000000000000000000000000000000000000000").IsAllZeros);
            Assert.IsFalse(new Bits("000000000000000000000000000000000000000000000000000000000000000").IsAllOnes);
            Assert.IsFalse(new Bits("000000000000000000000000000000000000000000000010000000000000000").IsAllZeros);
            Assert.IsTrue(new Bits("1111111111111111111111111111111111111111111111111111111111111111").IsAllOnes);
            Assert.IsFalse(new Bits("111111111111111111111111111111101111111111111111111111111111111").IsAllOnes);

            // Make sure we get correct bit positions for bitmaps with
            // lengths up to 256 bits.

            for (int i = 0; i < 256; i++)
                for (int j = 0; j < i; j++)
                {
                    bool[] array = new bool[i];
                    StringBuilder sb;

                    array[j] = true;
                    b1 = new Bits(array);

                    sb = new StringBuilder(i);
                    for (int k = 0; k < i; k++)
                        sb.Append(k == j ? '1' : '0');

                    Assert.AreEqual(sb.ToString(), (string)b1);
                    b2 = new Bits(sb.ToString());
                    Assert.AreEqual(b1, b2);

                    for (int k = 0; k < i; k++)
                        if (k == j)
                            Assert.IsTrue(b1[k]);
                        else
                            Assert.IsFalse(b1[k]);
                }
        }

        [TestMethod]
        [TestProperty("Lib", "LillTek.Common")]
        public void Bits_Not()
        {
            Bits b1;
            Bits b2;

            b1 = (Bits)"0000111100001111000011110000111100001111000011110000111100001111";
            b2 = b1.Not();
            Assert.AreEqual("1111000011110000111100001111000011110000111100001111000011110000", b2.ToString());

            b1 = (Bits)"0000111100001111000011110000111100001111000011110000111100001111";
            b2 = ~b1;
            Assert.AreEqual("1111000011110000111100001111000011110000111100001111000011110000", b2.ToString());
        }

        [TestMethod]
        [TestProperty("Lib", "LillTek.Common")]
        public void Bits_Or()
        {
            Bits b1;
            Bits b2;

            b1 = (Bits)"11001100";
            b2 = (Bits)"00110011";
            Assert.AreEqual("11111111", (string)(b1 | b2));

            b1 = (Bits)"01010101";
            b2 = (Bits)"00000000";
            Assert.AreEqual("01010101", (string)(b1 | b2));

            b1 = (Bits)"01010101";
            b2 = (Bits)"00000000";
            Assert.AreEqual("01010101", (string)(b1 | b2));

            b1 = (Bits)"11110000111100001111000011110000111100001111000011110000";
            b2 = (Bits)"01010101010101010101010101010101010101010101010101010101";
            Assert.AreEqual("11110101111101011111010111110101111101011111010111110101", (string)b1.Or(b2));

            b1 = (Bits)"01010101";
            b2 = (Bits)"00000000";
            Assert.AreEqual("01010101", (string)b1.Or(b2));

            try
            {
                b1 = (Bits)"01" | (Bits)"0";
                Assert.Fail("InvalidOperationException expected.");
            }
            catch (Exception e)
            {
                Assert.IsInstanceOfType(e, typeof(InvalidOperationException));
            }

            try
            {
                b1 = ((Bits)"01").Or((Bits)"0");
                Assert.Fail("InvalidOperationException expected.");
            }
            catch (Exception e)
            {
                Assert.IsInstanceOfType(e, typeof(InvalidOperationException));
            }
        }

        [TestMethod]
        [TestProperty("Lib", "LillTek.Common")]
        public void Bits_And()
        {
            Bits b1;
            Bits b2;

            b1 = (Bits)"11001100";
            b2 = (Bits)"00110011";
            Assert.AreEqual("00000000", (string)(b1 & b2));

            b1 = (Bits)"01010101";
            b2 = (Bits)"01000100";
            Assert.AreEqual("01000100", (string)(b1 & b2));

            b1 = (Bits)"11001100";
            b2 = (Bits)"11111111";
            Assert.AreEqual("11001100", (string)(b1 & b2));

            b1 = (Bits)"11110000111100001111000011110000111100001111000011110000";
            b2 = (Bits)"01010101010101010101010101010101010101010101010101010101";
            Assert.AreEqual("01010000010100000101000001010000010100000101000001010000", (string)b1.And(b2));

            b1 = (Bits)"01010101";
            b2 = (Bits)"00001111";
            Assert.AreEqual("00000101", (string)b1.And(b2));

            try
            {
                b1 = (Bits)"01" & (Bits)"0";
                Assert.Fail("InvalidOperationException expected.");
            }
            catch (Exception e)
            {
                Assert.IsInstanceOfType(e, typeof(InvalidOperationException));
            }

            try
            {
                b1 = ((Bits)"01").And((Bits)"0");
                Assert.Fail("InvalidOperationException expected.");
            }
            catch (Exception e)
            {
                Assert.IsInstanceOfType(e, typeof(InvalidOperationException));
            }
        }

        [TestMethod]
        [TestProperty("Lib", "LillTek.Common")]
        public void Bits_Xor()
        {
            Bits b1;
            Bits b2;

            b1 = (Bits)"11001100";
            b2 = (Bits)"00110011";
            Assert.AreEqual("11111111", (string)(b1 ^ b2));

            b1 = (Bits)"01010101";
            b2 = (Bits)"01000100";
            Assert.AreEqual("00010001", (string)(b1 ^ b2));

            b1 = (Bits)"11001100";
            b2 = (Bits)"11111111";
            Assert.AreEqual("00110011", (string)(b1 ^ b2));

            b1 = (Bits)"11110000111100001111000011110000111100001111000011110000";
            b2 = (Bits)"01010101010101010101010101010101010101010101010101010101";
            Assert.AreEqual("10100101101001011010010110100101101001011010010110100101", (string)b1.Xor(b2));

            b1 = (Bits)"01010101";
            b2 = (Bits)"00001111";
            Assert.AreEqual("01011010", (string)b1.Xor(b2));

            try
            {
                b1 = (Bits)"01" ^ (Bits)"0";
                Assert.Fail("InvalidOperationException expected.");
            }
            catch (Exception e)
            {
                Assert.IsInstanceOfType(e, typeof(InvalidOperationException));
            }

            try
            {
                b1 = ((Bits)"01").Xor((Bits)"0");
                Assert.Fail("InvalidOperationException expected.");
            }
            catch (Exception e)
            {
                Assert.IsInstanceOfType(e, typeof(InvalidOperationException));
            }
        }

        [TestMethod]
        [TestProperty("Lib", "LillTek.Common")]
        public void Bits_EQU()
        {
            Assert.IsTrue((Bits)null == (Bits)null);
            Assert.IsFalse((Bits)null == (Bits)"0011");
            Assert.IsFalse((Bits)"0011" == (Bits)null);
            Assert.IsFalse((Bits)"0011" == (Bits)"00110");
            Assert.IsTrue((Bits)"0011" == (Bits)"0011");
            Assert.IsTrue((Bits)"0011001100110011001100110011000000000000" == (Bits)"0011001100110011001100110011000000000000");
            Assert.IsFalse((Bits)"0011001100110011001100110011000000000000" == (Bits)"0011001100110011001100110011000000000001");
        }

        [TestMethod]
        [TestProperty("Lib", "LillTek.Common")]
        public void Bits_NEQ()
        {
            Assert.IsFalse((Bits)null != (Bits)null);
            Assert.IsTrue((Bits)null != (Bits)"0011");
            Assert.IsTrue((Bits)"0011" != (Bits)null);
            Assert.IsTrue((Bits)"0011" != (Bits)"00110");
            Assert.IsFalse((Bits)"0011" != (Bits)"0011");
            Assert.IsFalse((Bits)"0011001100110011001100110011000000000000" != (Bits)"0011001100110011001100110011000000000000");
            Assert.IsTrue((Bits)"0011001100110011001100110011000000000000" != (Bits)"0011001100110011001100110011000000000001");
        }

        [TestMethod]
        [TestProperty("Lib", "LillTek.Common")]
        public void Bits_Set()
        {
            var b = new Bits(64);

            b.SetAll();
            Assert.AreEqual("1111111111111111111111111111111111111111111111111111111111111111", (string)b);

            b.ClearAll();
            b.SetRange(1, 5);
            Assert.AreEqual("0111110000000000000000000000000000000000000000000000000000000000", (string)b);
            b.SetRange(10, 5);
            Assert.AreEqual("0111110000111110000000000000000000000000000000000000000000000000", (string)b);
        }

        [TestMethod]
        [TestProperty("Lib", "LillTek.Common")]
        public void Bits_Clear()
        {
            var b = new Bits(64);

            b.SetAll();
            Assert.AreEqual("1111111111111111111111111111111111111111111111111111111111111111", (string)b);
            b.ClearAll();
            Assert.AreEqual("0000000000000000000000000000000000000000000000000000000000000000", (string)b);

            b.SetAll();
            b.ClearRange(1, 5);
            Assert.AreEqual("1000001111111111111111111111111111111111111111111111111111111111", (string)b);
            b.ClearRange(10, 5);
            Assert.AreEqual("1000001111000001111111111111111111111111111111111111111111111111", (string)b);
        }

        [TestMethod]
        [TestProperty("Lib", "LillTek.Common")]
        public void Bits_ShiftLeft()
        {
            var b = new Bits("001100111001111001111100111111001111111");

            Assert.AreEqual("001100111001111001111100111111001111111", (string)(b << 0));
            Assert.AreEqual("011001110011110011111001111110011111110", (string)(b << 1));
            Assert.AreEqual("110011100111100111110011111100111111100", (string)(b << 2));
            Assert.AreEqual("100111001111001111100111111001111111000", (string)(b << 3));
            Assert.AreEqual("001110011110011111001111110011111110000", (string)(b << 4));
            Assert.AreEqual("011100111100111110011111100111111100000", (string)(b << 5));

            var s = b.ToString();

            for (int i = 0; i < 100; i++)
            {
                Assert.AreEqual(s, (string)(b << i));
                s = s.Substring(1) + "0";
            }

            ExtendedAssert.Throws<ArgumentException>(() => { var output = b << -1; });
        }

        [TestMethod]
        [TestProperty("Lib", "LillTek.Common")]
        public void Bits_ShiftRight()
        {
            var b = new Bits("001100111001111001111100111111001111111");

            Assert.AreEqual("001100111001111001111100111111001111111", (string)(b >> 0));
            Assert.AreEqual("000110011100111100111110011111100111111", (string)(b >> 1));
            Assert.AreEqual("000011001110011110011111001111110011111", (string)(b >> 2));
            Assert.AreEqual("000001100111001111001111100111111001111", (string)(b >> 3));
            Assert.AreEqual("000000110011100111100111110011111100111", (string)(b >> 4));
            Assert.AreEqual("000000011001110011110011111001111110011", (string)(b >> 5));

            var s = b.ToString();

            for (int i = 0; i < 100; i++)
            {
                Assert.AreEqual(s, (string)(b >> i));
                s = "0" + s.Substring(0, s.Length - 1);
            }

            ExtendedAssert.Throws<ArgumentException>(() => { var output = b >> -1; });
        }

        [TestMethod]
        [TestProperty("Lib", "LillTek.Common")]
        public void Bits_SerializeBytes()
        {
            byte[] input;
            Bits bits;

            input = new byte[0];
            bits = new Bits(input, 0);
            Assert.AreEqual(0, input.Length);
            Assert.AreEqual("", bits.ToString());
            CollectionAssert.AreEqual(input, bits.ToBytes());

            input = new byte[] { 0x80 };
            bits = new Bits(input, 4);
            Assert.AreEqual(4, bits.Length);
            Assert.AreEqual("1000", bits.ToString());
            CollectionAssert.AreEqual(input, bits.ToBytes());

            input = new byte[] { 0x80 };
            bits = new Bits(input, 8);
            Assert.AreEqual(8, bits.Length);
            Assert.AreEqual("10000000", bits.ToString());
            CollectionAssert.AreEqual(input, bits.ToBytes());

            input = new byte[] { 0x83 };
            bits = new Bits(input, 8);
            Assert.AreEqual(8, bits.Length);
            Assert.AreEqual("10000011", bits.ToString());
            CollectionAssert.AreEqual(input, bits.ToBytes());

            input = new byte[] { 0x83, 0x0F };
            bits = new Bits(input, 16);
            Assert.AreEqual(16, bits.Length);
            Assert.AreEqual("1000001100001111", bits.ToString());
            CollectionAssert.AreEqual(input, bits.ToBytes());

            input = new byte[] { 0x01, 0x02, 0x04, 0x08, 0x10, 0x20, 0x40, 0x80, 0xFF };
            bits = new Bits(input, input.Length * 8);
            Assert.AreEqual(input.Length * 8, bits.Length);
            Assert.AreEqual("000000010000001000000100000010000001000000100000010000001000000011111111", bits.ToString());
            CollectionAssert.AreEqual(input, bits.ToBytes());
        }

        [TestMethod]
        [TestProperty("Lib", "LillTek.Common")]
        public void Bits_Resize()
        {
            Bits input;
            Bits output;

            input = new Bits("10101010");

            output = input.Resize(0);
            Assert.AreEqual("", output.ToString());

            output = input.Resize(1);
            Assert.AreEqual("1", output.ToString());

            output = input.Resize(2);
            Assert.AreEqual("10", output.ToString());

            output = input.Resize(4);
            Assert.AreEqual("1010", output.ToString());

            output = input.Resize(8);
            Assert.AreEqual("10101010", output.ToString());

            output = input.Resize(16);
            Assert.AreEqual("1010101000000000", output.ToString());
        }
    }
}

