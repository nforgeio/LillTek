//-----------------------------------------------------------------------------
// FILE:        _SqlHelper.cs
// CONTRIBUTOR: Jeff Lill
// COPYRIGHT:   Copyright (c) 2005-2015 by Jeffrey Lill.  All rights reserved.
// DESCRIPTION: Unit tests.

using System;
using System.Data;
using System.Data.SqlClient;
using System.Threading;
using System.Configuration;

using Microsoft.VisualStudio.TestTools.UnitTesting;

using LillTek.Common;
using LillTek.Data;
using LillTek.Testing;

namespace LillTek.Data.Test
{
    [TestClass]
    public class _SqlHelper
    {
        [TestMethod]
        [TestProperty("Lib", "LillTek.Data")]
        public void SqlHelper_Literal_String()
        {
            Assert.AreEqual("'abc ''d'' efg'", SqlHelper.Literal("abc 'd' efg"));
            Assert.AreEqual("'abcdefg'", SqlHelper.Literal("abcdefg"));
            Assert.AreEqual("'\\a'", SqlHelper.Literal("\a"));
            Assert.AreEqual("'\\b'", SqlHelper.Literal("\b"));
            Assert.AreEqual("'\\f'", SqlHelper.Literal("\f"));
            Assert.AreEqual("'\\r'", SqlHelper.Literal("\r"));
            Assert.AreEqual("'\\t'", SqlHelper.Literal("\t"));
            Assert.AreEqual("'\\v'", SqlHelper.Literal("\v"));
            Assert.AreEqual("' '", SqlHelper.Literal(""));
        }

        [TestMethod]
        [TestProperty("Lib", "LillTek.Data")]
        public void SqlHelper_Literal_Bool()
        {
            Assert.AreEqual("0", SqlHelper.Literal(false));
            Assert.AreEqual("1", SqlHelper.Literal(true));
        }

        [TestMethod]
        [TestProperty("Lib", "LillTek.Data")]
        public void SqlHelper_Literal_DateTime()
        {
            Assert.AreEqual("'2004-10-01 13:25:15.123'", SqlHelper.Literal(new DateTime(2004, 10, 1, 13, 25, 15, 123)));
        }

        [TestMethod]
        [TestProperty("Lib", "LillTek.Data")]
        public void SqlHelper_Literal_Binary()
        {
            Assert.AreEqual("0x", SqlHelper.Literal(new byte[0]));
            Assert.AreEqual("0x00", SqlHelper.Literal(new byte[] { 0 }));
            Assert.AreEqual("0x000102030405", SqlHelper.Literal(new byte[] { 0, 1, 2, 3, 4, 5 }));
        }

        [TestMethod]
        [TestProperty("Lib", "LillTek.Data")]
        public void SqlHelper_ValidDate()
        {
            Assert.AreEqual(new DateTime(2000, 1, 1), SqlHelper.ValidDate(new DateTime(2000, 1, 1)));
            Assert.AreEqual(SqlHelper.MinDate, SqlHelper.ValidDate(new DateTime(1700, 1, 1)));
            Assert.AreEqual(SqlHelper.MaxDate, SqlHelper.ValidDate(DateTime.MaxValue));
        }

        private enum TestEnum
        {
            Zero = 0,
            One  = 1,
            Two  = 2
        }

        [TestMethod]
        [TestProperty("Lib", "LillTek.Data")]
        public void SqlHelper_AsEnum()
        {
            object o;

            o = (int)1;
            Assert.AreEqual(TestEnum.One, SqlHelper.AsEnum<TestEnum>(o));
            o = (byte)1;
            Assert.AreEqual(TestEnum.One, SqlHelper.AsEnum<TestEnum>(o));
            o = (sbyte)1;
            Assert.AreEqual(TestEnum.One, SqlHelper.AsEnum<TestEnum>(o));
            o = (short)1;
            Assert.AreEqual(TestEnum.One, SqlHelper.AsEnum<TestEnum>(o));
            o = (ushort)1;
            Assert.AreEqual(TestEnum.One, SqlHelper.AsEnum<TestEnum>(o));
            o = (int)1;
            Assert.AreEqual(TestEnum.One, SqlHelper.AsEnum<TestEnum>(o));
            o = (uint)1;
            Assert.AreEqual(TestEnum.One, SqlHelper.AsEnum<TestEnum>(o));
        }

        [TestMethod]
        [TestProperty("Lib", "LillTek.Data")]
        public void SqlHelper_AsString()
        {
            Assert.AreEqual("2010-10-24 12:24:25.023", SqlHelper.AsString(new DateTime(2010, 10, 24, 12, 24, 25, 023)));
        }
    }
}

