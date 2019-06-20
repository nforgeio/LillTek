//-----------------------------------------------------------------------------
// FILE:        _ConfigRewriter.cs
// CONTRIBUTOR: Jeff Lill
// COPYRIGHT:   Copyright (c) 2005-2015 by Jeffrey Lill. All rights reserved.
// DESCRIPTION: UNIT tests

using System;
using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Threading;
using System.Reflection;
using System.Configuration;
using System.Collections;
using System.Collections.Generic;

using Microsoft.VisualStudio.TestTools.UnitTesting;

using LillTek.Common;
using LillTek.Testing;

namespace LillTek.Common.Test
{
    [TestClass]
    public class _ConfigRewriter
    {
        [TestMethod]
        [TestProperty("Lib", "LillTek.Common")]
        public void ConfigRewriter_Test()
        {
            string fname = Path.GetTempPath() + "ConfigRewriteTest.ini";
            ConfigRewriter rewriter = new ConfigRewriter(fname);
            StreamReader reader = null;
            StreamWriter writer = null;

            string orgText =
@"
// This is a test
foo=bar
// $replace(tag1)
   -- $REPLACE(TAG2)
bar=foo
  // $(
";
            string newText =
@"
// This is a test
foo=bar
key1=10
key2=20
bar=foo
  // $(
";

            try
            {
                writer = new StreamWriter(fname);
                writer.Write(orgText);
                writer.Close();
                writer = null;

                rewriter.Restore();     // This shouldn't case a problem

                rewriter.Rewrite(new ConfigRewriteTag[] { new ConfigRewriteTag("tag1", "key1=10"), new ConfigRewriteTag("tag2", "key2=20\r\n") });

                reader = new StreamReader(fname);
                Assert.AreEqual(newText, reader.ReadToEnd());
                reader.Close();
                reader = null;

                rewriter.Restore();

                reader = new StreamReader(fname);
                Assert.AreEqual(orgText, reader.ReadToEnd());
                reader.Close();
                reader = null;

                rewriter.Restore();     // This shouldn't case a problem
            }
            finally
            {
                if (reader != null)
                    reader.Close();

                if (writer != null)
                    writer.Close();

                if (File.Exists(fname))
                    File.Delete(fname);
            }
        }
    }
}

