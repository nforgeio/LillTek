//-----------------------------------------------------------------------------
// FILE:        _TextGrid.cs
// CONTRIBUTOR: Jeff Lill
// COPYRIGHT:   Copyright (c) 2005-2015 by Jeffrey Lill.  All rights reserved.
// DESCRIPTION: UNIT tests for the TextGrid class

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
    public class _TextGrid
    {
        [TestMethod]
        [TestProperty("Lib", "LillTek.Common")]
        public void TextGrid_Basic()
        {
            var grid = new TextGrid();

            // Assert.AreEqual("",grid.ToString());

            grid[0, 0] = null;
            Assert.AreEqual("-na-\r\n", grid.ToString());

            grid.NAString = "(null)";
            Assert.AreEqual("(null)\r\n", grid.ToString());

            grid[0, 0] = "Foo";
            Assert.AreEqual("Foo\r\n", grid.ToString());

            grid[1, 0] = "Bar";
            Assert.AreEqual("Foo Bar\r\n", grid.ToString());

            grid[0, 1] = "xxx";
            grid[1, 1] = "yyy";
            Assert.AreEqual("Foo Bar\r\nxxx yyy\r\n", grid.ToString());

            grid[0, 1] = "x";
            grid[1, 1] = "y";
            Assert.AreEqual("Foo Bar\r\nx   y  \r\n", grid.ToString());
        }

        [TestMethod]
        [TestProperty("Lib", "LillTek.Common")]
        public void TextGrid_CellNames()
        {
            var grid = new TextGrid();

            grid.NameCell("one", 0, 0);
            grid.NameCell("two", 0, 1);

            grid["one"] = "hello";
            grid["two"] = "world";
            Assert.AreEqual("hello", grid["one"]);
            Assert.AreEqual("hello", grid["ONE"]);
            Assert.AreEqual("world", grid["two"]);
            Assert.AreEqual("world", grid["TWO"]);
            Assert.AreEqual("hello\r\nworld\r\n", grid.ToString());
        }

        [TestMethod]
        [TestProperty("Lib", "LillTek.Common")]
        public void TextGrid_ColumnFormats()
        {
            var grid = new TextGrid();

            grid.NAString = "null";
            grid.SetColumnFormat(0, new TextGridFormat(TextGridAlign.Default, 5));
            grid.SetColumnFormat(1, new TextGridFormat(TextGridAlign.Default, 10));
            grid[0, 0] = null;
            grid[1, 0] = null;
            grid[2, 0] = "foo";
            Assert.AreEqual("null  null       foo\r\n", grid.ToString());

            grid = new TextGrid();
            grid.NAString = "null";
            grid.SetColumnFormat(0, new TextGridFormat(TextGridAlign.Default, 5));
            grid[0, 0] = "a";
            grid[0, 1] = "ab";
            grid[0, 2] = "abc";
            grid[0, 3] = "abcd";
            grid[0, 4] = "abcde";
            grid[0, 5] = "abcdef";
            Assert.AreEqual("a    \r\nab   \r\nabc  \r\nabcd \r\nabcde\r\nab...\r\n", grid.ToString());

            grid.SetColumnFormat(0, new TextGridFormat(TextGridAlign.Default, 0));
            Assert.AreEqual("\r\n\r\n\r\n\r\n\r\n\r\n", grid.ToString());

            grid.ClearCells();
            grid.SetColumnFormat(0, new TextGridFormat(TextGridAlign.Default, 1));
            grid[0, 0] = "a";
            Assert.AreEqual("a\r\n", grid.ToString());
            grid[0, 0] = "ab";
            Assert.AreEqual(".\r\n", grid.ToString());

            grid.SetColumnFormat(0, new TextGridFormat(TextGridAlign.Default, 2));
            Assert.AreEqual("ab\r\n", grid.ToString());
            grid[0, 0] = "abc";
            Assert.AreEqual("..\r\n", grid.ToString());

            grid.SetColumnFormat(0, new TextGridFormat(TextGridAlign.Default, 3));
            Assert.AreEqual("abc\r\n", grid.ToString());
            grid[0, 0] = "abcd";
            Assert.AreEqual("...\r\n", grid.ToString());

            grid.SetColumnFormat(0, new TextGridFormat(TextGridAlign.Default, 4));
            Assert.AreEqual("abcd\r\n", grid.ToString());
            grid[0, 0] = "abcde";
            Assert.AreEqual("a...\r\n", grid.ToString());

            grid.Clear();
            grid.SetColumnFormat(0, new TextGridFormat(TextGridAlign.Left, -1));
            grid[0, 0] = "ab";
            grid[0, 1] = "a";
            Assert.AreEqual("ab\r\na \r\n", grid.ToString());

            grid.SetColumnFormat(0, new TextGridFormat(TextGridAlign.Right, -1));
            Assert.AreEqual("ab\r\n a\r\n", grid.ToString());

            grid.SetColumnFormat(0, new TextGridFormat(TextGridAlign.Left, 4));
            grid[0, 0] = "ab";
            grid[0, 1] = "a";
            Assert.AreEqual("ab  \r\na   \r\n", grid.ToString());

            grid.SetColumnFormat(0, new TextGridFormat(TextGridAlign.Center, 4));
            Assert.AreEqual(" ab \r\n a  \r\n", grid.ToString());

            grid.Clear();
            grid.NAString = "";
            grid.SetColumnFormat(0, new TextGridFormat(TextGridAlign.Center, 4));
            grid[1, 0] = "foo";
            Assert.AreEqual("     foo\r\n", grid.ToString());
        }

        [TestMethod]
        [TestProperty("Lib", "LillTek.Common")]
        public void TextGrid_CellFormats()
        {
            var grid = new TextGrid();

            grid.SetCellFormat(0, 0, new TextGridFormat(TextGridAlign.Default, 5));
            grid[0, 0] = "a";
            Assert.AreEqual("a    \r\n", grid.ToString());

            grid.SetColumnFormat(0, new TextGridFormat(TextGridAlign.Default, 1));
            Assert.AreEqual("a    \r\n", grid.ToString());

            grid.SetColumnFormat(0, new TextGridFormat(TextGridAlign.Right, 1));
            Assert.AreEqual("    a\r\n", grid.ToString());

            grid.SetColumnFormat(0, new TextGridFormat(TextGridAlign.Right, 5));
            grid.SetCellFormat(0, 0, new TextGridFormat(TextGridAlign.Default, 5));
            grid[0, 1] = "bb";
            Assert.AreEqual("    a\r\n   bb\r\n", grid.ToString());

            grid.SetCellFormat(0, 0, new TextGridFormat(TextGridAlign.Left, 5));
            Assert.AreEqual("a    \r\n   bb\r\n", grid.ToString());

            grid.SetCellFormat(0, 0, new TextGridFormat(TextGridAlign.Right, 5));
            Assert.AreEqual("    a\r\n   bb\r\n", grid.ToString());

            grid.SetCellFormat(0, 1, new TextGridFormat(TextGridAlign.Left, 5));
            Assert.AreEqual("    a\r\nbb   \r\n", grid.ToString());
        }

        [TestMethod]
        [TestProperty("Lib", "LillTek.Common")]
        public void TextGrid_FillChar()
        {
            var grid = new TextGrid();

            grid.SetCellFormat(0, 0, new TextGridFormat('-'));
            grid[0, 0] = "";
            grid[0, 1] = "abcd";
            Assert.AreEqual("----\r\nabcd\r\n", grid.ToString());

            grid.Clear();
            grid.SetCellFormat(0, 1, new TextGridFormat('-'));
            grid[0, 0] = "abcd";
            grid[0, 1] = "efghijklmno";
            Assert.AreEqual("abcd\r\n----\r\n", grid.ToString());
        }
    }
}

