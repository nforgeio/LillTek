//-----------------------------------------------------------------------------
// FILE:        _CsvTableReader.cs
// CONTRIBUTOR: Jeff Lill
// COPYRIGHT:   Copyright (c) 2005-2015 by Jeffrey Lill.  All rights reserved.
// DESCRIPTION: UNIT tests 

using System;
using System.IO;
using System.Text;
using System.Collections.Generic;

using Microsoft.VisualStudio.TestTools.UnitTesting;

using LillTek.Common;
using LillTek.Testing;

namespace LillTek.Common.Test
{
    [TestClass]
    public class _CsvTableReader
    {
        [TestMethod]
        [TestProperty("Lib", "LillTek.Common")]
        public void CsvTableReader_EmptyTable()
        {
            CsvTableReader reader;
            string table = "";

            reader = new CsvTableReader(new CsvReader(table));
            Assert.AreEqual(0, reader.ColumnMap.Count);
            Assert.IsNull(reader.ReadRow());
            Assert.IsNull(reader.ReadRow());
        }

        [TestMethod]
        [TestProperty("Lib", "LillTek.Common")]
        public void CsvTableReader_NoRows()
        {
            CsvTableReader reader;
            string table = "Col1,Col2,Col3";

            reader = new CsvTableReader(new CsvReader(table));
            Assert.AreEqual(3, reader.ColumnMap.Count);
            Assert.AreEqual(0, reader.ColumnMap["Col1"]);
            Assert.AreEqual(1, reader.ColumnMap["Col2"]);
            Assert.AreEqual(2, reader.ColumnMap["Col3"]);
            Assert.IsNull(reader.ReadRow());
            Assert.IsNull(reader.ReadRow());
        }

        [TestMethod]
        [TestProperty("Lib", "LillTek.Common")]
        public void CsvTableReader_Parsing()
        {
            CsvTableReader reader;
            string table =
@"Col1,Col2,Col3
10,true,25.20
no,10";

            reader = new CsvTableReader(new CsvReader(table));
            Assert.AreEqual(3, reader.ColumnMap.Count);
            Assert.AreEqual(0, reader.ColumnMap["Col1"]);
            Assert.AreEqual(1, reader.ColumnMap["Col2"]);
            Assert.AreEqual(2, reader.ColumnMap["Col3"]);

            Assert.AreEqual(3, reader.Columns.Count);
            Assert.AreEqual("Col1", reader.Columns[0]);
            Assert.AreEqual("Col2", reader.Columns[1]);
            Assert.AreEqual("Col3", reader.Columns[2]);

            Assert.IsNotNull(reader.ReadRow());
            Assert.AreEqual(10, reader.Parse("Col1", 0));
            Assert.AreEqual(true, reader.Parse("Col2", false));
            Assert.AreEqual(25.20, reader.Parse("Col3", 0.0));

            Assert.IsNotNull(reader.ReadRow());
            Assert.AreEqual(false, reader.Parse("Col1", true));
            Assert.AreEqual(10, reader.Parse("Col2", 0));

            Assert.IsNull(reader.ReadRow());
            Assert.IsNull(reader.ReadRow());
        }

        [TestMethod]
        [TestProperty("Lib", "LillTek.Common")]
        public void CsvTableReader_DuplicateColumns()
        {
            CsvTableReader reader;
            string table =
@"Col1,Col2,Col2
10,true,25.20
no,10";

            reader = new CsvTableReader(new CsvReader(table));
            Assert.AreEqual(2, reader.ColumnMap.Count);
            Assert.AreEqual(0, reader.ColumnMap["Col1"]);
            Assert.AreEqual(1, reader.ColumnMap["Col2"]);

            Assert.IsNotNull(reader.ReadRow());
            Assert.AreEqual(10, reader.Parse("Col1", 0));
            Assert.AreEqual(true, reader.Parse("Col2", false));

            Assert.IsNotNull(reader.ReadRow());
            Assert.AreEqual(false, reader.Parse("Col1", true));
            Assert.AreEqual(10, reader.Parse("Col2", 0));

            Assert.IsNull(reader.ReadRow());
            Assert.IsNull(reader.ReadRow());
        }

        [TestMethod]
        [TestProperty("Lib", "LillTek.Common")]
        public void CsvTableReader_MissingColumns()
        {
            CsvTableReader reader;
            string table =
@"Col1,Col2,Col2
10,true,25.20
no,10";

            reader = new CsvTableReader(new CsvReader(table));

            Assert.IsNotNull(reader.ReadRow());
            Assert.AreEqual(10, reader.Parse("Col1", 0));
            Assert.AreEqual(true, reader.Parse("Col2", false));
            Assert.AreEqual(100, reader.Parse("ColX", 100));
        }

        [TestMethod]
        [TestProperty("Lib", "LillTek.Common")]
        public void CsvTableReader_CaseInsensitivity()
        {
            CsvTableReader reader;
            string table =
@"Col1,Col2,Col3
10,true,25.20
no,10";

            reader = new CsvTableReader(new CsvReader(table));

            Assert.IsNotNull(reader.ReadRow());
            Assert.AreEqual(10, reader.Parse("col1", 0));
            Assert.AreEqual(true, reader.Parse("col2", false));
            Assert.AreEqual(25.20, reader.Parse("col3", 0.0));

            Assert.IsNotNull(reader.ReadRow());
            Assert.AreEqual(false, reader.Parse("col1", true));
            Assert.AreEqual(10, reader.Parse("col2", 0));

            Assert.IsNull(reader.ReadRow());
            Assert.IsNull(reader.ReadRow());
        }
    }
}

