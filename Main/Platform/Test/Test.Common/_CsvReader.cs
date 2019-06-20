//-----------------------------------------------------------------------------
// FILE:        _CsvReader.cs
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
    public class _CsvReader
    {
        [TestMethod]
        [TestProperty("Lib", "LillTek.Common")]
        public void CsvReader_Basic()
        {
            string input =
@"0-0,1-0,2-0
0-1,1-1,2-1
0-2,1-2,2-2";
            using (CsvReader reader = new CsvReader(input))
            {
                CollectionAssert.AreEqual(new string[] { "0-0", "1-0", "2-0" }, reader.Read());
                CollectionAssert.AreEqual(new string[] { "0-1", "1-1", "2-1" }, reader.Read());
                CollectionAssert.AreEqual(new string[] { "0-2", "1-2", "2-2" }, reader.Read());
                Assert.IsNull(reader.Read());
            }
        }

        [TestMethod]
        [TestProperty("Lib", "LillTek.Common")]
        public void CsvReader_NoRows()
        {
            Assert.IsNull(new CsvReader("").Read());
        }

        [TestMethod]
        [TestProperty("Lib", "LillTek.Common")]
        public void CsvReader_Ragged()
        {
            string input =
@"0-0,1-0,2-0
0-1,1-1
0-2,1-2,2-2
";
            using (CsvReader reader = new CsvReader(input))
            {
                CollectionAssert.AreEqual(new string[] { "0-0", "1-0", "2-0" }, reader.Read());
                CollectionAssert.AreEqual(new string[] { "0-1", "1-1" }, reader.Read());
                CollectionAssert.AreEqual(new string[] { "0-2", "1-2", "2-2" }, reader.Read());
                Assert.IsNull(reader.Read());
            }
        }

        [TestMethod]
        [TestProperty("Lib", "LillTek.Common")]
        public void CsvReader_EmptyFields()
        {
            string input =
@",1-0,2-0
0-1,,2-1
0-2,1-2,
";
            using (CsvReader reader = new CsvReader(input))
            {
                CollectionAssert.AreEqual(new string[] { "", "1-0", "2-0" }, reader.Read());
                CollectionAssert.AreEqual(new string[] { "0-1", "", "2-1" }, reader.Read());
                CollectionAssert.AreEqual(new string[] { "0-2", "1-2", "" }, reader.Read());
                Assert.IsNull(reader.Read());
            }
        }

        [TestMethod]
        [TestProperty("Lib", "LillTek.Common")]
        public void CsvReader_Quoted()
        {
            string input =
@"""Hello, """"World"""""",!,""Now""
Row,""Two""
";
            using (CsvReader reader = new CsvReader(input))
            {
                CollectionAssert.AreEqual(new string[] { "Hello, \"World\"", "!", "Now" }, reader.Read());
                CollectionAssert.AreEqual(new string[] { "Row", "Two" }, reader.Read());
                Assert.IsNull(reader.Read());
            }
        }

        [TestMethod]
        [TestProperty("Lib", "LillTek.Common")]
        public void CsvReader_QuotedMultiLine()
        {
            string input = "\"Hello\r\nWorld\",Col2\r\nRow,\"Two\"\r\n";

            using (CsvReader reader = new CsvReader(input))
            {
                CollectionAssert.AreEqual(new string[] { "Hello\r\nWorld", "Col2" }, reader.Read());
                CollectionAssert.AreEqual(new string[] { "Row", "Two" }, reader.Read());
                Assert.IsNull(reader.Read());
            }
        }

        [TestMethod]
        [TestProperty("Lib", "LillTek.Common")]
        public void CsvReader_LF_Terminated()
        {
            string input = "0-0,1-0,2-0\n0-1,1-1,2-1\n0-2,1-2,2-2";

            using (CsvReader reader = new CsvReader(input))
            {
                CollectionAssert.AreEqual(new string[] { "0-0", "1-0", "2-0" }, reader.Read());
                CollectionAssert.AreEqual(new string[] { "0-1", "1-1", "2-1" }, reader.Read());
                CollectionAssert.AreEqual(new string[] { "0-2", "1-2", "2-2" }, reader.Read());
                Assert.IsNull(reader.Read());
            }
        }

        [TestMethod]
        [TestProperty("Lib", "LillTek.Common")]
        public void CsvReader_QuotedMultiLine_LF_Terminated()
        {
            string input = "\"Hello\nWorld\",Col2\nRow,\"Two\"";

            using (CsvReader reader = new CsvReader(input))
            {
                CollectionAssert.AreEqual(new string[] { "Hello\nWorld", "Col2" }, reader.Read());
                CollectionAssert.AreEqual(new string[] { "Row", "Two" }, reader.Read());
                Assert.IsNull(reader.Read());
            }
        }
    }
}

