//-----------------------------------------------------------------------------
// FILE:        _TextTable.cs
// CONTRIBUTOR: Jeff Lill
// COPYRIGHT:   Copyright (c) 2005-2015 by Jeffrey Lill.  All rights reserved.
// DESCRIPTION: UNIT tests 

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
    public class _TextTable
    {
        [TestMethod]
        [TestProperty("Lib", "LillTek.Common")]
        public void TextTable_NoHeaders()
        {
            TextTable table;

            table = new TextTable();
            Assert.AreEqual("", table.ToString());

            table = new TextTable();
            table.AppendRow(1);
            Assert.AreEqual("1\r\n", table.ToString());

            table = new TextTable();
            table.AppendRow(1, 2);
            Assert.AreEqual("1 2\r\n", table.ToString());

            table = new TextTable();
            table.AppendRow(1, 2, 3);
            Assert.AreEqual("1 2 3\r\n", table.ToString());

            table = new TextTable();
            table.AppendRow(100);
            table.AppendRow(200, 201);
            table.AppendRow(300, 301, 302);
            Assert.AreEqual("100\r\n200 201\r\n300 301 302\r\n", table.ToString());

            table = new TextTable();
            table.AppendRow("a");
            table.AppendRow(200, 201);
            table.AppendRow(300, 301, 302);
            Assert.AreEqual("a  \r\n200 201\r\n300 301 302\r\n", table.ToString());
        }

        [TestMethod]
        [TestProperty("Lib", "LillTek.Common")]
        public void TextTable_Headers()
        {
            TextTable table;

            table = new TextTable();
            table.SetHeaders("ID", "Name", "Text");

            table.AppendRow("1", "Jeff Lill", "AA");
            table.AppendRow("20", "Joe Bloe", "BBB");
            table.AppendRow("300", "Jane Doe", "");

            string results = "ID  Name      Text\r\n--- --------- ----\r\n1   Jeff Lill AA  \r\n20  Joe Bloe  BBB \r\n300 Jane Doe      \r\n";

            Assert.AreEqual(results, table.ToString());
        }

        [TestMethod]
        [TestProperty("Lib", "LillTek.Common")]
        public void TextTable_Nulls()
        {
            TextTable table;

            table = new TextTable();
            table.SetHeaders("ID", "Name", "Text");

            table.AppendRow("1", "Jeff Lill", "AA");
            table.AppendRow(null, "Joe Bloe", "BBB");
            table.AppendRow("300", "Jane Doe", "");

            string results = "ID  Name      Text\r\n--- --------- ----\r\n1   Jeff Lill AA  \r\n    Joe Bloe  BBB \r\n300 Jane Doe      \r\n";

            Assert.AreEqual(results, table.ToString());
        }
    }
}

