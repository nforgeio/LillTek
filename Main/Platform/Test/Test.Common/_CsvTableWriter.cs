//-----------------------------------------------------------------------------
// FILE:        _CsvTableWriter.cs
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
    public class _CsvTableWriter
    {
        [TestMethod]
        [TestProperty("Lib", "LillTek.Common")]
        public void CsvTableWriter_Basic()
        {
            string path = Path.GetTempFileName();

            try
            {
                using (var writer = new CsvTableWriter(new string[] { "Col0", "Col1", "Col2" }, new FileStream(path, FileMode.Create), Encoding.UTF8))
                {
                    Assert.AreEqual(0, writer.GetColumnIndex("Col0"));
                    Assert.AreEqual(1, writer.GetColumnIndex("Col1"));
                    Assert.AreEqual(2, writer.GetColumnIndex("Col2"));
                    Assert.AreEqual(-1, writer.GetColumnIndex("Col3"));

                    writer.Set("Col0", "(0,0)");
                    writer.Set("Col1", "(1,0)");
                    writer.Set("Col2", "(2,0)");
                    writer.WriteRow();

                    writer.Set("Col0", "(0,1)");
                    writer.Set("Col1", "(1,1)");
                    writer.Set("Col2", "(2,1)");
                    writer.WriteRow();
                }

                using (var reader = new CsvTableReader(new FileStream(path, FileMode.Open), Encoding.UTF8))
                {
                    Assert.IsTrue(reader.ColumnMap.ContainsKey("Col0"));
                    Assert.IsTrue(reader.ColumnMap.ContainsKey("Col1"));
                    Assert.IsTrue(reader.ColumnMap.ContainsKey("Col2"));

                    Assert.IsNotNull(reader.ReadRow());
                    Assert.AreEqual("(0,0)", reader.Parse("Col0", (string)null));
                    Assert.AreEqual("(1,0)", reader.Parse("Col1", (string)null));
                    Assert.AreEqual("(2,0)", reader.Parse("Col2", (string)null));

                    Assert.IsNotNull(reader.ReadRow());
                    Assert.AreEqual("(0,1)", reader.Parse("Col0", (string)null));
                    Assert.AreEqual("(1,1)", reader.Parse("Col1", (string)null));
                    Assert.AreEqual("(2,1)", reader.Parse("Col2", (string)null));

                    Assert.IsNull(reader.ReadRow());
                }
            }
            finally
            {
                Helper.DeleteFile(path);
            }
        }

        [TestMethod]
        [TestProperty("Lib", "LillTek.Common")]
        public void CsvTableWriter_NullColumns()
        {
            string path = Path.GetTempFileName();

            try
            {
                using (var writer = new CsvTableWriter(new string[] { "Col0", "Col1", "Col2" }, new FileStream(path, FileMode.Create), Encoding.UTF8))
                {
                    writer.Set("Col0", "(0,0)");
                    writer.Set("Col2", "(2,0)");
                    writer.WriteRow();

                    writer.Set("Col0", "(0,1)");
                    writer.Set("Col2", "(2,1)");
                    writer.WriteRow();
                }

                using (var reader = new CsvTableReader(new FileStream(path, FileMode.Open), Encoding.UTF8))
                {
                    Assert.IsTrue(reader.ColumnMap.ContainsKey("Col0"));
                    Assert.IsTrue(reader.ColumnMap.ContainsKey("Col1"));
                    Assert.IsTrue(reader.ColumnMap.ContainsKey("Col2"));

                    Assert.IsNotNull(reader.ReadRow());
                    Assert.AreEqual("(0,0)", reader.Parse("Col0", (string)null));
                    Assert.AreEqual("", reader.Parse("Col1", (string)null));
                    Assert.AreEqual("(2,0)", reader.Parse("Col2", (string)null));

                    Assert.IsNotNull(reader.ReadRow());
                    Assert.AreEqual("(0,1)", reader.Parse("Col0", (string)null));
                    Assert.AreEqual("", reader.Parse("Col1", (string)null));
                    Assert.AreEqual("(2,1)", reader.Parse("Col2", (string)null));

                    Assert.IsNull(reader.ReadRow());
                }
            }
            finally
            {
                Helper.DeleteFile(path);
            }
        }

        [TestMethod]
        [TestProperty("Lib", "LillTek.Common")]
        public void CsvTableWriter_MissingColumns()
        {
            string path = Path.GetTempFileName();

            try
            {
                using (var writer = new CsvTableWriter(new string[] { "Col0", "Col1", "Col2" }, new FileStream(path, FileMode.Create), Encoding.UTF8))
                {
                    writer.Set("Col0", "(0,0)");
                    writer.Set("Col1", "(1,0)");
                    writer.Set("Col2", "(2,0)");
                    writer.Set("XXXX", "YYYY");
                    writer.WriteRow();

                    writer.Set("Col0", "(0,1)");
                    writer.Set("Col1", "(1,1)");
                    writer.Set("Col2", "(2,1)");
                    writer.Set("XXXX", "YYYY");
                    writer.WriteRow();
                }

                using (var reader = new CsvTableReader(new FileStream(path, FileMode.Open), Encoding.UTF8))
                {
                    Assert.IsTrue(reader.ColumnMap.ContainsKey("Col0"));
                    Assert.IsTrue(reader.ColumnMap.ContainsKey("Col1"));
                    Assert.IsTrue(reader.ColumnMap.ContainsKey("Col2"));

                    Assert.IsNotNull(reader.ReadRow());
                    Assert.AreEqual("(0,0)", reader.Parse("Col0", (string)null));
                    Assert.AreEqual("(1,0)", reader.Parse("Col1", (string)null));
                    Assert.AreEqual("(2,0)", reader.Parse("Col2", (string)null));

                    Assert.IsNotNull(reader.ReadRow());
                    Assert.AreEqual("(0,1)", reader.Parse("Col0", (string)null));
                    Assert.AreEqual("(1,1)", reader.Parse("Col1", (string)null));
                    Assert.AreEqual("(2,1)", reader.Parse("Col2", (string)null));

                    Assert.IsNull(reader.ReadRow());
                }
            }
            finally
            {
                Helper.DeleteFile(path);
            }
        }

        [TestMethod]
        [TestProperty("Lib", "LillTek.Common")]
        public void CsvTableWriter_BlankRow()
        {
            string path = Path.GetTempFileName();

            try
            {
                using (var writer = new CsvTableWriter(new string[] { "Col0", "Col1", "Col2" }, new FileStream(path, FileMode.Create), Encoding.UTF8))
                {
                    writer.Set("Col0", "(0,0)");
                    writer.Set("Col1", "(1,0)");
                    writer.Set("Col2", "(2,0)");
                    writer.WriteRow();

                    writer.WriteRow();

                    writer.Set("Col0", "(0,2)");
                    writer.Set("Col1", "(1,2)");
                    writer.Set("Col2", "(2,2)");
                    writer.WriteRow();
                }

                using (var reader = new CsvTableReader(new FileStream(path, FileMode.Open), Encoding.UTF8))
                {
                    Assert.IsTrue(reader.ColumnMap.ContainsKey("Col0"));
                    Assert.IsTrue(reader.ColumnMap.ContainsKey("Col1"));
                    Assert.IsTrue(reader.ColumnMap.ContainsKey("Col2"));

                    Assert.IsNotNull(reader.ReadRow());
                    Assert.AreEqual("(0,0)", reader.Parse("Col0", (string)null));
                    Assert.AreEqual("(1,0)", reader.Parse("Col1", (string)null));
                    Assert.AreEqual("(2,0)", reader.Parse("Col2", (string)null));

                    Assert.IsNotNull(reader.ReadRow());
                    Assert.AreEqual("", reader.Parse("Col0", (string)null));
                    Assert.AreEqual("", reader.Parse("Col1", (string)null));
                    Assert.AreEqual("", reader.Parse("Col2", (string)null));

                    Assert.IsNotNull(reader.ReadRow());
                    Assert.AreEqual("(0,2)", reader.Parse("Col0", (string)null));
                    Assert.AreEqual("(1,2)", reader.Parse("Col1", (string)null));
                    Assert.AreEqual("(2,2)", reader.Parse("Col2", (string)null));

                    Assert.IsNull(reader.ReadRow());
                }
            }
            finally
            {
                Helper.DeleteFile(path);
            }
        }
    }
}

