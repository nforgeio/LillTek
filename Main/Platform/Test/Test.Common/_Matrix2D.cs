//-----------------------------------------------------------------------------
// FILE:        _Matrix.cs
// CONTRIBUTOR: Jeff Lill
// COPYRIGHT:   Copyright (c) 2005-2015 by Jeffrey Lill.  All rights reserved.
// DESCRIPTION: UNIT tests for the Matrix class

using System;
using System.Net;
using System.Net.Sockets;
using System.Threading;
using System.Configuration;

using Microsoft.VisualStudio.TestTools.UnitTesting;

using LillTek.Common;
using LillTek.Testing;

namespace LillTek.Common.Test
{
    [TestClass]
    public class _Matrix2D
    {
        [TestMethod]
        [TestProperty("Lib", "LillTek.Common")]
        public void Matrix2D_Basic()
        {
            var m = new Matrix2D<string>();

            Assert.AreEqual(0, m.RowCount);
            Assert.AreEqual(0, m.ColumnCount);

            m[0, 0] = "Hello World!";
            Assert.AreEqual(1, m.RowCount);
            Assert.AreEqual(1, m.ColumnCount);
            Assert.AreEqual("Hello World!", m[0, 0]);

            m[0, 1] = "0,1";
            Assert.AreEqual(2, m.RowCount);
            Assert.AreEqual(1, m.ColumnCount);
            Assert.AreEqual("Hello World!", m[0, 0]);
            Assert.AreEqual("0,1", m[0, 1]);

            m[1, 0] = "1,0";
            Assert.AreEqual(2, m.RowCount);
            Assert.AreEqual(2, m.ColumnCount);
            Assert.AreEqual("Hello World!", m[0, 0]);
            Assert.AreEqual("0,1", m[0, 1]);
            Assert.AreEqual("1,0", m[1, 0]);

            m[1, 1] = "1,1";
            Assert.AreEqual(2, m.RowCount);
            Assert.AreEqual(2, m.ColumnCount);
            Assert.AreEqual("Hello World!", m[0, 0]);
            Assert.AreEqual("0,1", m[0, 1]);
            Assert.AreEqual("1,0", m[1, 0]);
            Assert.AreEqual("1,1", m[1, 1]);

            m[0, 0] = null;
            m[0, 1] = null;
            m[1, 0] = null;
            m[1, 1] = null;
            m[100, 100] = "100,100";

            for (int x = 0; x < 100; x++)
                for (int y = 0; y < 100; y++)
                    Assert.IsNull(m[x, y]);

            Assert.AreEqual("100,100", m[100, 100]);
            Assert.AreEqual(101, m.RowCount);
            Assert.AreEqual(101, m.RowCount);
        }

        [TestMethod]
        [TestProperty("Lib", "LillTek.Common")]
        public void Matrix2D_Clear()
        {
            var m = new Matrix2D<string>();

            Assert.AreEqual(0, m.RowCount);
            Assert.AreEqual(0, m.ColumnCount);
            m.Clear();
            Assert.AreEqual(0, m.RowCount);
            Assert.AreEqual(0, m.ColumnCount);

            m[0, 0] = "Hello World!";
            Assert.AreEqual(1, m.RowCount);
            Assert.AreEqual(1, m.ColumnCount);
            m.Clear();
            Assert.AreEqual(0, m.RowCount);
            Assert.AreEqual(0, m.ColumnCount);
            Assert.IsNull(m[0, 0]);

            m[100, 100] = "100,100";
            m.Clear();
            Assert.AreEqual(0, m.RowCount);
            Assert.AreEqual(0, m.ColumnCount);
        }

        [TestMethod]
        [TestProperty("Lib", "LillTek.Common")]
        public void Matrix2D_Trim()
        {
            var m = new Matrix2D<object>();

            m.Trim();
            Assert.AreEqual(0, m.RowCount);
            Assert.AreEqual(0, m.ColumnCount);

            m[0, 0] = "foo";
            Assert.AreEqual(1, m.RowCount);
            Assert.AreEqual(1, m.ColumnCount);
            m.Trim();
            Assert.AreEqual(1, m.RowCount);
            Assert.AreEqual(1, m.ColumnCount);

            m[0, 0] = null;
            m.Trim();
            Assert.AreEqual(0, m.RowCount);
            Assert.AreEqual(0, m.ColumnCount);

            m[100, 100] = 10;
            Assert.AreEqual(101, m.RowCount);
            Assert.AreEqual(101, m.ColumnCount);
            m.Trim();
            Assert.AreEqual(101, m.RowCount);
            Assert.AreEqual(101, m.ColumnCount);

            m[100, 101] = null;
            m[101, 100] = null;
            Assert.AreEqual(102, m.RowCount);
            Assert.AreEqual(102, m.ColumnCount);

            m.Trim();
            Assert.AreEqual(101, m.RowCount);
            Assert.AreEqual(101, m.ColumnCount);

            m[10, 10] = "Hello";
            m[100, 100] = null;
            m.Trim();
            Assert.AreEqual(11, m.RowCount);
            Assert.AreEqual(11, m.ColumnCount);
        }
    }
}

