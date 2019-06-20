//-----------------------------------------------------------------------------
// FILE:        _CsvWriter.cs
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
    public class _CsvWriter
    {
        [TestMethod]
        [TestProperty("Lib", "LillTek.Common")]
        public void CsvWriter_Basic()
        {
            StringBuilder sb = new StringBuilder();
            StringWriter sw = new StringWriter(sb);
            CsvWriter writer = new CsvWriter(sw);

            writer.WriteLine(10, 20, 11.5, "Hello", "Hello,World", "Hello \"Cruel\" World", null);
            writer.WriteLine("End");
            writer.Close();

            Assert.AreEqual(
@"10,20,11.5,Hello,""Hello,World"",""Hello """"Cruel"""" World"",
End
", sb.ToString());
        }
    }
}

