//-----------------------------------------------------------------------------
// FILE:        _Helper.cs
// CONTRIBUTOR: Jeff Lill
// COPYRIGHT:   Copyright (c) 2005-2015 by Jeffrey Lill.  All rights reserved.
// DESCRIPTION: UNIT tests for Helper

using System;
using System.Collections.Generic;
using System.Configuration;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Threading;
using System.Threading.Tasks;

using LillTek.Common;
using LillTek.Testing;

using Microsoft.VisualStudio.TestTools.UnitTesting;

// $todo(jeff.lill): Implement tests for the Read/Write methods.

namespace LillTek.Common.Test
{
    [TestClass]
    public class _Helper
    {
        [TestMethod]
        [TestProperty("Lib", "LillTek.Common")]
        public void Helper_ParseStringList()
        {
            string[] v;

            v = Helper.ParseStringList("", ',');
            Assert.AreEqual(0, v.Length);

            v = Helper.ParseStringList(" ", ',');
            Assert.AreEqual(0, v.Length);

            v = Helper.ParseStringList(",", ',');
            Assert.AreEqual(0, v.Length);

            v = Helper.ParseStringList("a", ',');
            Assert.AreEqual(1, v.Length);
            Assert.AreEqual("a", v[0]);

            v = Helper.ParseStringList(" a", ',');
            Assert.AreEqual(1, v.Length);
            Assert.AreEqual("a", v[0]);

            v = Helper.ParseStringList("a ", ',');
            Assert.AreEqual(1, v.Length);
            Assert.AreEqual("a", v[0]);

            v = Helper.ParseStringList("a,", ',');
            Assert.AreEqual(1, v.Length);
            Assert.AreEqual("a", v[0]);

            v = Helper.ParseStringList("a , ", ',');
            Assert.AreEqual(1, v.Length);
            Assert.AreEqual("a", v[0]);

            v = Helper.ParseStringList("a,b", ',');
            Assert.AreEqual(2, v.Length);
            Assert.AreEqual("a", v[0]);
            Assert.AreEqual("b", v[1]);

            v = Helper.ParseStringList(" a , b ", ',');
            Assert.AreEqual(2, v.Length);
            Assert.AreEqual("a", v[0]);
            Assert.AreEqual("b", v[1]);

            v = Helper.ParseStringList(" a , b , ", ',');
            Assert.AreEqual(2, v.Length);
            Assert.AreEqual("a", v[0]);
            Assert.AreEqual("b", v[1]);
        }

        [TestMethod]
        [TestProperty("Lib", "LillTek.Common")]
        public void Helper_IsAzure()
        {
            Assert.IsFalse(Helper.IsAzure);
        }

        [TestMethod]
        [TestProperty("Lib", "LillTek.Common")]
        public void Helper_EscapeUri()
        {
            Assert.AreEqual("", Helper.EscapeUri(""));
            Assert.AreEqual("abcd", Helper.EscapeUri("abcd"));
            Assert.AreEqual("ab%20cd", Helper.EscapeUri("ab cd"));
            Assert.AreEqual("ab&cd", Helper.EscapeUri("ab&cd"));
            Assert.AreEqual("ab%09cd", Helper.EscapeUri("ab\tcd"));
            Assert.AreEqual("abcd%7f", Helper.EscapeUri("abcd" + (char)0x7f));
            Assert.AreEqual("%25abc", Helper.EscapeUri("%abc"));
            Assert.AreEqual("hello%2bworld", Helper.EscapeUri("hello+world"));
        }

        [TestMethod]
        [TestProperty("Lib", "LillTek.Common")]
        public void Helper_UnescapeUri()
        {
            Assert.AreEqual("", Helper.UnescapeUri(""));
            Assert.AreEqual("abcd", Helper.UnescapeUri("abcd"));
            Assert.AreEqual("ab cd", Helper.UnescapeUri("ab%20cd"));
            Assert.AreEqual("ab&cd", Helper.UnescapeUri("ab&cd"));
            Assert.AreEqual("ab\tcd", Helper.UnescapeUri("ab%09cd"));
            Assert.AreEqual("abcd" + (char)0x7f, Helper.UnescapeUri("abcd%7f"));
            Assert.AreEqual("%abc", Helper.UnescapeUri("%25abc"));
            Assert.AreEqual("hello world", Helper.UnescapeUri("hello+world"));
        }

        [TestMethod]
        [TestProperty("Lib", "LillTek.Common")]
        public void Helper_EscapeUriParam()
        {
            Assert.AreEqual("", Helper.EscapeUri(""));
            Assert.AreEqual("abcd", Helper.EscapeUri("abcd"));
            Assert.AreEqual("ab%20cd", Helper.EscapeUri("ab cd"));
            Assert.AreEqual("ab&cd", Helper.EscapeUri("ab&cd"));
            Assert.AreEqual("ab%09cd", Helper.EscapeUri("ab\tcd"));
            Assert.AreEqual("abcd%7f", Helper.EscapeUri("abcd" + (char)0x7f));
            Assert.AreEqual("abc%3d", Helper.EscapeUriParam("abc="));
            Assert.AreEqual("%26abc", Helper.EscapeUriParam("&abc"));
            Assert.AreEqual("%25abc", Helper.EscapeUri("%abc"));
            Assert.AreEqual("hello%2bworld", Helper.EscapeUriParam("hello+world"));
        }

        [TestMethod]
        [TestProperty("Lib", "LillTek.Common")]
        public void Helper_UnescapeUriParam()
        {
            Assert.AreEqual("", Helper.UnescapeUri(""));
            Assert.AreEqual("abcd", Helper.UnescapeUri("abcd"));
            Assert.AreEqual("ab cd", Helper.UnescapeUri("ab%20cd"));
            Assert.AreEqual("ab&cd", Helper.UnescapeUri("ab%26cd"));
            Assert.AreEqual("ab=cd", Helper.UnescapeUri("ab%3dcd"));
            Assert.AreEqual("abcd&" + (char)0x7f, Helper.UnescapeUri("abcd%26%7f"));
            Assert.AreEqual("%abc", Helper.UnescapeUri("%25abc"));
            Assert.AreEqual("hello world", Helper.UnescapeUriParam("hello+world"));
        }

        [TestMethod]
        [TestProperty("Lib", "LillTek.Common")]
        public void Helper_ParseUriQuery()
        {
            ArgCollection args;

            args = Helper.ParseUriQuery(new Uri("http://test.com/index.cgi"));
            Assert.AreEqual(0, args.Count);
            args = Helper.ParseUriQuery(new Uri("http://test.com/index.cgi?"));
            Assert.AreEqual(0, args.Count);
            args = Helper.ParseUriQuery(new Uri("http://test.com/index.cgi?nonsense"));
            Assert.AreEqual(0, args.Count);

            args = Helper.ParseUriQuery(new Uri("http://test.com/index.cgi?a=1"));
            Assert.AreEqual(1, args.Count);
            Assert.AreEqual("1", args["a"]);

            args = Helper.ParseUriQuery(new Uri("http://test.com/index.cgi?arg1=foo&arg2=bar"));
            Assert.AreEqual(2, args.Count);
            Assert.AreEqual("foo", args["arg1"]);
            Assert.AreEqual("bar", args["arg2"]);

            args = Helper.ParseUriQuery(new Uri("http://test.com/index.cgi?arg1=foo&arg2=bar&"));
            Assert.AreEqual(2, args.Count);
            Assert.AreEqual("foo", args["arg1"]);
            Assert.AreEqual("bar", args["arg2"]);

            args = Helper.ParseUriQuery(new Uri("http://test.com/index.cgi?arg1=foo&arg2=bar&"));
            Assert.AreEqual(2, args.Count);
            Assert.AreEqual("foo", args["arg1"]);
            Assert.AreEqual("bar", args["arg2"]);

            args = Helper.ParseUriQuery(new Uri("http://test.com/index.cgi?arg1=foo%20bar&arg%202=bar&"));
            Assert.AreEqual(2, args.Count);
            Assert.AreEqual("foo bar", args["arg1"]);
            Assert.AreEqual("bar", args["arg 2"]);

            args = Helper.ParseUriQuery(new Uri("http://test.com/index.cgi?empty=&"));
            Assert.AreEqual(1, args.Count);
            Assert.AreEqual("", args["empty"]);

            args = Helper.ParseUriQuery(new Uri("http://test.com/index.cgi?=&"));
            Assert.AreEqual(0, args.Count);

            args = Helper.ParseUriQuery(new Uri("http://test.com/Download.aspx?fileUri=http://demo.cscloud.com/GetAppPackage.aspx?organization-id=1%7fapplication-id=193bdc37-537a-4a1c-9ebc-0dc2e5c21740%7fversion=4%7fcredentials=9e26ab33055efc8f5d980754640c63ca279188188319e25108490a74c3da804f%7fdecrypt=1&fileName=TestJob-Source"));
            Assert.AreEqual(2, args.Count);
            Assert.AreEqual("http://demo.cscloud.com/GetAppPackage.aspx?organization-id=1&application-id=193bdc37-537a-4a1c-9ebc-0dc2e5c21740&version=4&credentials=9e26ab33055efc8f5d980754640c63ca279188188319e25108490a74c3da804f&decrypt=1", args["fileUri"]);
            Assert.AreEqual("TestJob-Source", args["fileName"]);
        }

        [TestMethod]
        [TestProperty("Lib", "LillTek.Common")]
        public void Helper_ToUriQuery()
        {
            Assert.AreEqual(string.Empty, Helper.ToUriQuery(""));
            Assert.AreEqual("?a=1", Helper.ToUriQuery("a=1"));
            Assert.AreEqual("?a=1&b=2", Helper.ToUriQuery("a=1;b=2"));
            Assert.AreEqual("?foo%20bar=hello%20world", Helper.ToUriQuery("foo bar=hello world"));
            Assert.AreEqual("?test%7fvalue=hello%7fworld&b=2", Helper.ToUriQuery("test&value=hello&world;b=2"));

            Uri uri = new Uri("http://test.com" + Helper.ToUriQuery("test&value=hello&world;b=2"));
            ArgCollection args = Helper.ParseUriQuery(uri);

            Assert.AreEqual("hello&world", args["test&value"]);
            Assert.AreEqual("2", args["b"]);
        }

        [TestMethod]
        [TestProperty("Lib", "LillTek.Common")]
        public void Helper_UTF8()
        {
            Assert.AreEqual("Hello World!", Helper.FromUTF8(Helper.ToUTF8("Hello World!")));
            Assert.IsNull(Helper.ToUTF8(null));
            Assert.IsNull(Helper.FromUTF8(null));

            MemoryStream ms;
            byte[] buf;

            ms = new MemoryStream();
            ms.WriteByte(0);
            buf = Helper.ToUTF8("Hello World!");
            ms.Write(buf, 0, buf.Length);

            Assert.AreEqual("Hello World!", Helper.FromUTF8(ms.ToArray(), 1));
            ms.WriteByte(1);
            Assert.AreEqual("Hello World!", Helper.FromUTF8(ms.ToArray(), 1, (int)(ms.Length - 2)));
        }

        [TestMethod]
        [TestProperty("Lib", "LillTek.Common")]
        public void Helper_Ansi()
        {
            Assert.AreEqual("Hello World!", Helper.FromAnsi(Helper.ToAnsi("Hello World!")));
            Assert.IsNull(Helper.ToAnsi(null));
            Assert.IsNull(Helper.FromAnsi(null));

            MemoryStream ms;
            byte[] buf;

            ms = new MemoryStream();
            ms.WriteByte(0);
            buf = Helper.ToAnsi("Hello World!");
            ms.Write(buf, 0, buf.Length);

            Assert.AreEqual("Hello World!", Helper.FromAnsi(ms.ToArray(), 1));
            ms.WriteByte(1);
            Assert.AreEqual("Hello World!", Helper.FromAnsi(ms.ToArray(), 1, (int)(ms.Length - 2)));
        }

        [TestMethod]
        [TestProperty("Lib", "LillTek.Common")]
        public void Helper_FromHex()
        {
            CollectionAssert.AreEqual(new byte[0], Helper.FromHex(""));
            CollectionAssert.AreEqual(new byte[] { 0x00, 0x01, 0x02, 0xAA, 0xCF }, Helper.FromHex("000102AACF"));
            CollectionAssert.AreEqual(new byte[] { 0x00, 0x01, 0x02, 0xAA, 0xCF }, Helper.FromHex("000102aacf"));
            CollectionAssert.AreEqual(new byte[] { 0x00, 0x01, 0x02, 0xAA, 0xCF }, Helper.FromHex("00 010\r\n2AA\tCF"));
        }

        [TestMethod]
        [TestProperty("Lib", "LillTek.Common")]
        public void Helper_TryParseHex_Int()
        {
            int v;

            Assert.IsTrue(Helper.TryParseHex("0", out v));
            Assert.AreEqual(0, v);

            Assert.IsTrue(Helper.TryParseHex("0000", out v));
            Assert.AreEqual(0, v);

            Assert.IsTrue(Helper.TryParseHex("FFFF", out v));
            Assert.AreEqual(0xFFFF, v);

            Assert.IsTrue(Helper.TryParseHex("A", out v));
            Assert.AreEqual(0xA, v);

            Assert.IsTrue(Helper.TryParseHex("Abcd", out v));
            Assert.AreEqual(0xABCD, v);

            Assert.IsFalse(Helper.TryParseHex("", out v));
            Assert.IsFalse(Helper.TryParseHex("1q", out v));
        }

        [TestMethod]
        [TestProperty("Lib", "LillTek.Common")]
        public void Helper_TryParseHex_Array()
        {
            byte[] v;

            Assert.IsTrue(Helper.TryParseHex("0000", out v));
            CollectionAssert.AreEqual(new byte[] { 0, 0 }, v);

            Assert.IsTrue(Helper.TryParseHex("FFFF", out v));
            CollectionAssert.AreEqual(new byte[] { 0xFF, 0xFF }, v);

            Assert.IsTrue(Helper.TryParseHex("A1", out v));
            CollectionAssert.AreEqual(new byte[] { 0xA1 }, v);

            Assert.IsTrue(Helper.TryParseHex("Abcd", out v));
            CollectionAssert.AreEqual(new byte[] { 0xAB, 0xCD }, v);

            Assert.IsTrue(Helper.TryParseHex("000102030405060708090A0B0C0D0E0F", out v));
            CollectionAssert.AreEqual(new byte[] { 0x00, 0x01, 0x02, 0x03, 0x04, 0x05, 0x06, 0x07, 0x08, 0x09, 0x0A, 0x0B, 0x0C, 0x0D, 0x0E, 0x0F }, v);

            Assert.IsFalse(Helper.TryParseHex("", out v));
            Assert.IsFalse(Helper.TryParseHex("1", out v));
            Assert.IsFalse(Helper.TryParseHex("1q", out v));
        }

        [TestMethod]
        [TestProperty("Lib", "LillTek.Common")]
        public void Helper_HexDump()
        {
            byte[] data = Helper.ToAnsi("0123456789ABCDEF");

            Assert.AreEqual("", Helper.HexDump(data, 0, 0, 16, HexDumpOption.None));
            Assert.AreEqual("", Helper.HexDump(data, 0, 0, 16, HexDumpOption.ShowAll));

            Assert.AreEqual("30 31 32 33 \r\n", Helper.HexDump(data, 0, 4, 4, HexDumpOption.None));
            Assert.AreEqual("30 31 \r\n32 33 \r\n", Helper.HexDump(data, 0, 4, 2, HexDumpOption.None));
            Assert.AreEqual("0000: 31 32 33 34 - 1234\r\n", Helper.HexDump(data, 1, 4, 4, HexDumpOption.ShowAll));
            Assert.AreEqual("0000: 30 31 32 33 34 35 36 37 - 01234567\r\n", Helper.HexDump(data, 0, 8, 8, HexDumpOption.ShowAll));
            Assert.AreEqual("0000: 30 31 32 33 34 35 36 37 - 01234567\r\n0008: 38 39 41 42 43 44 45 46 - 89ABCDEF\r\n", Helper.HexDump(data, 0, 16, 8, HexDumpOption.ShowAll));
            Assert.AreEqual("0000: 30 31 32 - 012\r\n0003: 33 34    - 34\r\n", Helper.HexDump(data, 0, 5, 3, HexDumpOption.ShowAll));
        }

        [TestMethod]
        [TestProperty("Lib", "LillTek.Common")]
        public void Helper_QuoteQueryString()
        {
            Assert.AreEqual("''", Helper.QuoteQueryString(""));
            Assert.AreEqual("'\\r'", Helper.QuoteQueryString("\r"));
            Assert.AreEqual("'\\n'", Helper.QuoteQueryString("\n"));
            Assert.AreEqual("'\\t'", Helper.QuoteQueryString("\t"));
            Assert.AreEqual("'\\\\'", Helper.QuoteQueryString("\\"));
            Assert.AreEqual("'\\''", Helper.QuoteQueryString("'"));

            Assert.AreEqual("'Hello\\r\\nWorld!'", Helper.QuoteQueryString("Hello\r\nWorld!"));
        }

        [TestMethod]
        [TestProperty("Lib", "LillTek.Common")]
        public void Helper_QuoteQueryColumn()
        {
            Assert.AreEqual("[]", Helper.QuoteQueryColumn(""));
            Assert.AreEqual("[\\r]", Helper.QuoteQueryColumn("\r"));
            Assert.AreEqual("[\\n]", Helper.QuoteQueryColumn("\n"));
            Assert.AreEqual("[\\t]", Helper.QuoteQueryColumn("\t"));
            Assert.AreEqual("[\\~]", Helper.QuoteQueryColumn("~"));
            Assert.AreEqual("[\\(]", Helper.QuoteQueryColumn("("));
            Assert.AreEqual("[\\)]", Helper.QuoteQueryColumn(")"));
            Assert.AreEqual("[\\\\]", Helper.QuoteQueryColumn("\\"));
            Assert.AreEqual("[\\/]", Helper.QuoteQueryColumn("/"));
            Assert.AreEqual("[\\=]", Helper.QuoteQueryColumn("="));
            Assert.AreEqual("[\\>]", Helper.QuoteQueryColumn(">"));
            Assert.AreEqual("[\\<]", Helper.QuoteQueryColumn("<"));
            Assert.AreEqual("[\\+]", Helper.QuoteQueryColumn("+"));
            Assert.AreEqual("[\\-]", Helper.QuoteQueryColumn("-"));
            Assert.AreEqual("[\\*]", Helper.QuoteQueryColumn("*"));
            Assert.AreEqual("[\\%]", Helper.QuoteQueryColumn("%"));
            Assert.AreEqual("[\\&]", Helper.QuoteQueryColumn("&"));
            Assert.AreEqual("[\\^]", Helper.QuoteQueryColumn("^"));
            Assert.AreEqual("[\\']", Helper.QuoteQueryColumn("'"));
            Assert.AreEqual("[\\\"]", Helper.QuoteQueryColumn("\""));
            Assert.AreEqual("[\\[]", Helper.QuoteQueryColumn("["));
            Assert.AreEqual("[\\]]", Helper.QuoteQueryColumn("]"));

            Assert.AreEqual("[Hello\\r\\nWorld!]", Helper.QuoteQueryColumn("Hello\r\nWorld!"));
        }

        [TestMethod]
        [TestProperty("Lib", "LillTek.Common")]
        public void Helper_DeleteFile_NoFile()
        {
            string folder = Path.GetTempPath() + Helper.NewGuid().ToString();

            Helper.DeleteFile(folder, true);
        }

        private void CreateFile(string folder, string fname, bool readOnly)
        {
            StreamWriter writer;
            string path;

            if (folder.EndsWith("\\"))
                path = folder + fname;
            else
                path = folder + "\\" + fname;

            writer = new StreamWriter(path);
            writer.WriteLine("Hello World!");
            writer.Close();

            if (readOnly)
                File.SetAttributes(path, FileAttributes.ReadOnly);
        }

        [TestMethod]
        [TestProperty("Lib", "LillTek.Common")]
        public void Helper_DeleteFile_File()
        {
            string folder = Path.GetTempPath() + Helper.NewGuid().ToString();

            Directory.CreateDirectory(folder);
            CreateFile(folder, "test.txt", false);
            Assert.IsTrue(File.Exists(folder + "\\test.txt"));
            Helper.DeleteFile(folder + "\\test.txt", true);
            Assert.IsFalse(File.Exists(folder + "\\test.txt"));

            Directory.Delete(folder);
        }

        [TestMethod]
        [TestProperty("Lib", "LillTek.Common")]
        public void Helper_DeleteFile_ReadOnly()
        {
            string folder = Path.GetTempPath() + Helper.NewGuid().ToString();

            Directory.CreateDirectory(folder);
            CreateFile(folder, "test.txt", true);
            Assert.IsTrue(File.Exists(folder + "\\test.txt"));
            Helper.DeleteFile(folder + "\\test.txt", true);
            Assert.IsFalse(File.Exists(folder + "\\test.txt"));

            Directory.Delete(folder);
        }

        [TestMethod]
        [TestProperty("Lib", "LillTek.Common")]
        public void Helper_DeleteFile_WildCard()
        {
            string folder = Path.GetTempPath() + Helper.NewGuid().ToString();

            Directory.CreateDirectory(folder);
            Directory.CreateDirectory(folder + "\\foo.dat");
            CreateFile(folder, "test1.txt", true);
            CreateFile(folder, "test2.txt", true);
            CreateFile(folder, "test3.txt", true);
            CreateFile(folder, "test4.dat", true);

            Helper.DeleteFile(folder + "\\*.txt", false);
            Assert.IsFalse(File.Exists(folder + "\\test1.txt"));
            Assert.IsFalse(File.Exists(folder + "\\test2.txt"));
            Assert.IsFalse(File.Exists(folder + "\\test3.txt"));
            Assert.IsTrue(File.Exists(folder + "\\test4.dat"));
            Assert.IsTrue(Directory.Exists(folder + "\\foo.dat"));

            Helper.DeleteFile(folder + "\\*.*", true);
            Assert.IsFalse(File.Exists(folder + "\\test4.dat"));
            Assert.IsFalse(Directory.Exists(folder + "\\foo.dat"));

            Directory.Delete(folder);
        }

        [TestMethod]
        [TestProperty("Lib", "LillTek.Common")]
        public void Helper_DeleteFile_Recursive_WildCard()
        {
            string folder = Path.GetTempPath() + Helper.NewGuid().ToString();

            Directory.CreateDirectory(folder);
            CreateFile(folder, "test1.txt", true);
            CreateFile(folder, "test2.txt", true);
            CreateFile(folder, "test3.txt", true);
            CreateFile(folder, "test4.dat", true);

            Directory.CreateDirectory(folder + "\\test");
            CreateFile(folder, "test\\test1.txt", true);
            CreateFile(folder, "test\\test2.txt", true);
            CreateFile(folder, "test\\test3.txt", true);
            CreateFile(folder, "test\\test4.dat", true);

            Helper.DeleteFile(folder + "\\*.*", true);
            Assert.IsFalse(Directory.Exists(folder + "\\test"));

            Helper.DeleteFile(folder, true);
            Assert.IsFalse(Directory.Exists(folder));
        }

        [TestMethod]
        [TestProperty("Lib", "LillTek.Common")]
        public void Helper_DeleteFile_Folder()
        {
            string folder = Path.GetTempPath() + Helper.NewGuid().ToString();

            Directory.CreateDirectory(folder);
            Directory.CreateDirectory(folder + "\\test");
            Directory.CreateDirectory(folder + "\\test\\nested");
            CreateFile(folder, "test\\test1.txt", true);
            CreateFile(folder, "test\\test2.txt", true);
            CreateFile(folder, "test\\test3.txt", true);
            CreateFile(folder, "test\\test4.dat", true);

            Helper.DeleteFile(folder + "\\test", false);
            Assert.IsTrue(Directory.Exists(folder + "\\test"));

            Helper.DeleteFile(folder + "\\test", true);
            Assert.IsFalse(Directory.Exists(folder + "\\test"));
            Assert.IsFalse(Directory.Exists(folder + "\\test\\nested"));

            Helper.DeleteFile(folder, true);
            Assert.IsFalse(Directory.Exists(folder));
        }

        [TestMethod]
        [TestProperty("Lib", "LillTek.Common")]
        public void Helper_DeleteFile_Special()
        {
            Helper.DisableDeleteFile = true;

            try
            {
                try
                {
                    Helper.DeleteFile("c:\\", true);
                    Assert.Fail("Expected special folder exception.");
                }
                catch
                {
                }

                try
                {
                    Helper.DeleteFile("c:\\*.*", true);
                    Assert.Fail("Expected special folder exception.");
                }
                catch
                {
                }

                try
                {
                    Helper.DeleteFile("c:\\*", true);
                    Assert.Fail("Expected special folder exception.");
                }
                catch
                {
                }

                try
                {
                    Helper.DeleteFile(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles), true);
                    Assert.Fail("Expected special folder exception.");
                }
                catch
                {
                }

                try
                {
                    Helper.DeleteFile(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles) + "*.*", true);
                    Assert.Fail("Expected special folder exception.");
                }
                catch
                {
                }

                try
                {
                    Helper.DeleteFile(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles) + "*", true);
                    Assert.Fail("Expected special folder exception.");
                }
                catch
                {
                }

                try
                {
                    Helper.DeleteFile(Environment.GetFolderPath(Environment.SpecialFolder.System), true);
                    Assert.Fail("Expected special folder exception.");
                }
                catch
                {
                }

                try
                {
                    Helper.DeleteFile(Environment.GetFolderPath(Environment.SpecialFolder.System) + "*.*", true);
                    Assert.Fail("Expected special folder exception.");
                }
                catch
                {
                }

                try
                {
                    Helper.DeleteFile(Environment.GetFolderPath(Environment.SpecialFolder.System) + "*", true);
                    Assert.Fail("Expected special folder exception.");
                }
                catch
                {
                }
            }
            finally
            {
                Helper.DisableDeleteFile = false;
            }
        }

        [TestMethod]
        [TestProperty("Lib", "LillTek.Common")]
        public void Helper_CompareFiles()
        {
            string folder = Path.GetTempPath() + Helper.NewGuid().ToString();
            FileStream fs;
            byte[] block;

            Directory.CreateDirectory(folder);

            try
            {
                // Compare files of zero length

                fs = new FileStream(folder + "\\1.dat", FileMode.Create);
                fs.Close();
                fs = new FileStream(folder + "\\2.dat", FileMode.Create);
                fs.Close();
                Assert.IsTrue(Helper.CompareFiles(folder + "\\1.dat", folder + "\\2.dat"));

                // Compare files of differing lengths

                Helper.AppendToFile(folder + "\\2.dat", "Hello World!");
                Assert.IsFalse(Helper.CompareFiles(folder + "\\1.dat", folder + "\\2.dat"));

                // Compare two large identical files

                block = new byte[1024 * 64];
                for (int i = 0; i < block.Length; i++)
                    block[i] = (byte)i;

                fs = new FileStream(folder + "\\1.dat", FileMode.Create);
                for (int i = 0; i < 1024; i++)
                    fs.Write(block, 0, block.Length);
                fs.Close();

                fs = new FileStream(folder + "\\2.dat", FileMode.Create);
                for (int i = 0; i < 1024; i++)
                    fs.Write(block, 0, block.Length);
                fs.Close();

                Assert.IsTrue(Helper.CompareFiles(folder + "\\1.dat", folder + "\\2.dat"));

                // Munge a couple bytes in the middle of one of the files and
                // verify that the comparison fails

                fs = new FileStream(folder + "\\1.dat", FileMode.Open);
                fs.Position = fs.Length / 2;
                fs.WriteByte((byte)0xFF);
                fs.WriteByte((byte)0xFF);
                fs.Close();

                Assert.IsFalse(Helper.CompareFiles(folder + "\\1.dat", folder + "\\2.dat"));
            }
            finally
            {
                Helper.DeleteFile(folder, true);
            }
        }

        [TestMethod]
        [TestProperty("Lib", "LillTek.Common")]
        public void Helper_CopyFile_File2File()
        {
            string folder = Path.GetTempPath() + Helper.NewGuid().ToString();
            StreamReader reader;
            string s1, s2;

            Directory.CreateDirectory(folder);

            try
            {
                CreateFile(folder, "test1.txt", false);
                Helper.CopyFile(folder + "\\test1.txt", folder + "\\test2.txt", false);
                Assert.IsTrue(File.Exists(folder + "\\test2.txt"));

                reader = new StreamReader(folder + "\\test1.txt");
                s1 = reader.ReadToEnd();
                reader.Close();

                reader = new StreamReader(folder + "\\test2.txt");
                s2 = reader.ReadToEnd();
                reader.Close();

                Assert.AreEqual(s1, s2);
            }
            finally
            {
                Helper.DeleteFile(folder, true);
            }
        }

        [TestMethod]
        [TestProperty("Lib", "LillTek.Common")]
        public void Helper_CopyFile_File2File_Overwrite()
        {
            string folder = Path.GetTempPath() + Helper.NewGuid().ToString();
            StreamWriter writer;
            StreamReader reader;
            string s1, s2;

            Directory.CreateDirectory(folder);

            try
            {
                CreateFile(folder, "test1.txt", false);

                writer = new StreamWriter(folder + "\\test2.txt");
                writer.WriteLine("This is a test of the emergency broadcasting system.");
                writer.Close();

                Assert.IsTrue(File.Exists(folder + "\\test2.txt"));
                Helper.CopyFile(folder + "\\test1.txt", folder + "\\test2.txt", false);
                Assert.IsTrue(File.Exists(folder + "\\test2.txt"));

                reader = new StreamReader(folder + "\\test1.txt");
                s1 = reader.ReadToEnd();
                reader.Close();

                reader = new StreamReader(folder + "\\test2.txt");
                s2 = reader.ReadToEnd();
                reader.Close();

                Assert.AreEqual(s1, s2);

                writer = new StreamWriter(folder + "\\test2.txt");
                writer.WriteLine("This is a test of the emergency broadcasting system.");
                writer.Close();
                File.SetAttributes(folder + "\\test2.txt", FileAttributes.ReadOnly);

                Assert.IsTrue(File.Exists(folder + "\\test2.txt"));
                Helper.CopyFile(folder + "\\test1.txt", folder + "\\test2.txt", false);
                Assert.IsTrue(File.Exists(folder + "\\test2.txt"));

                reader = new StreamReader(folder + "\\test1.txt");
                s1 = reader.ReadToEnd();
                reader.Close();

                reader = new StreamReader(folder + "\\test2.txt");
                s2 = reader.ReadToEnd();
                reader.Close();

                Assert.AreEqual(s1, s2);
            }
            finally
            {
                Helper.DeleteFile(folder, true);
            }
        }

        [TestMethod]
        [TestProperty("Lib", "LillTek.Common")]
        public void Helper_CopyFile_File2Folder()
        {
            string folder = Path.GetTempPath() + Helper.NewGuid().ToString();

            Directory.CreateDirectory(folder);

            try
            {
                CreateFile(folder, "test1.txt", false);
                Directory.CreateDirectory(folder + "\\folder1");
                Helper.CopyFile(folder + "\\test1.txt", folder + "\\folder1", true);
                Assert.IsTrue(File.Exists(folder + "\\folder1\\test1.txt"));
            }
            finally
            {
                Helper.DeleteFile(folder, true);
            }
        }

        [TestMethod]
        [TestProperty("Lib", "LillTek.Common")]
        public void Helper_CopyFile_FolderTree_Create()
        {
            string folder = Path.GetTempPath() + Helper.NewGuid().ToString();

            Directory.CreateDirectory(folder);

            try
            {
                Directory.CreateDirectory(folder + "\\src");
                CreateFile(folder + "\\src", "test1.txt", false);
                CreateFile(folder + "\\src", "test2.txt", false);
                CreateFile(folder + "\\src", "test3.txt", false);

                Directory.CreateDirectory(folder + "\\src\\0");
                CreateFile(folder + "\\src\\0", "test1.txt", false);
                CreateFile(folder + "\\src\\0", "test2.txt", false);
                CreateFile(folder + "\\src\\0", "test3.txt", false);

                Directory.CreateDirectory(folder + "\\src\\1");
                CreateFile(folder + "\\src\\1", "test1.txt", false);
                CreateFile(folder + "\\src\\1", "test2.txt", false);
                CreateFile(folder + "\\src\\1", "test3.txt", false);

                Directory.CreateDirectory(folder + "\\src\\2");
                CreateFile(folder + "\\src\\2", "test1.txt", false);
                CreateFile(folder + "\\src\\2", "test2.txt", false);
                CreateFile(folder + "\\src\\2", "test3.txt", false);

                Directory.CreateDirectory(folder + "\\src\\3");
                CreateFile(folder + "\\src\\3", "test1.txt", false);
                CreateFile(folder + "\\src\\3", "test2.txt", false);
                CreateFile(folder + "\\src\\3", "test3.txt", false);

                Helper.CopyFile(folder + "\\src", folder + "\\dst", true);

                Assert.IsTrue(File.Exists(folder + "\\dst\\test1.txt"));
                Assert.IsTrue(File.Exists(folder + "\\dst\\test2.txt"));
                Assert.IsTrue(File.Exists(folder + "\\dst\\test3.txt"));

                Assert.IsTrue(File.Exists(folder + "\\dst\\0\\test1.txt"));
                Assert.IsTrue(File.Exists(folder + "\\dst\\0\\test2.txt"));
                Assert.IsTrue(File.Exists(folder + "\\dst\\0\\test3.txt"));

                Assert.IsTrue(File.Exists(folder + "\\dst\\1\\test1.txt"));
                Assert.IsTrue(File.Exists(folder + "\\dst\\1\\test2.txt"));
                Assert.IsTrue(File.Exists(folder + "\\dst\\1\\test3.txt"));

                Assert.IsTrue(File.Exists(folder + "\\dst\\2\\test1.txt"));
                Assert.IsTrue(File.Exists(folder + "\\dst\\2\\test2.txt"));
                Assert.IsTrue(File.Exists(folder + "\\dst\\2\\test3.txt"));

                Assert.IsTrue(File.Exists(folder + "\\dst\\3\\test1.txt"));
                Assert.IsTrue(File.Exists(folder + "\\dst\\3\\test2.txt"));
                Assert.IsTrue(File.Exists(folder + "\\dst\\3\\test3.txt"));
            }
            finally
            {
                Helper.DeleteFile(folder, true);
            }
        }

        [TestMethod]
        [TestProperty("Lib", "LillTek.Common")]
        public void Helper_CopyFile_FolderTree_Exists1()
        {
            string folder = Path.GetTempPath() + Helper.NewGuid().ToString();

            Directory.CreateDirectory(folder);

            try
            {
                Directory.CreateDirectory(folder + "\\src");
                CreateFile(folder + "\\src", "test1.txt", false);
                CreateFile(folder + "\\src", "test2.txt", false);
                CreateFile(folder + "\\src", "test3.txt", false);

                Directory.CreateDirectory(folder + "\\src\\0");
                CreateFile(folder + "\\src\\0", "test1.txt", false);
                CreateFile(folder + "\\src\\0", "test2.txt", false);
                CreateFile(folder + "\\src\\0", "test3.txt", false);

                Directory.CreateDirectory(folder + "\\src\\1");
                CreateFile(folder + "\\src\\1", "test1.txt", false);
                CreateFile(folder + "\\src\\1", "test2.txt", false);
                CreateFile(folder + "\\src\\1", "test3.txt", false);

                Directory.CreateDirectory(folder + "\\src\\2");
                CreateFile(folder + "\\src\\2", "test1.txt", false);
                CreateFile(folder + "\\src\\2", "test2.txt", false);
                CreateFile(folder + "\\src\\2", "test3.txt", false);

                Directory.CreateDirectory(folder + "\\src\\3");
                CreateFile(folder + "\\src\\3", "test1.txt", false);
                CreateFile(folder + "\\src\\3", "test2.txt", false);
                CreateFile(folder + "\\src\\3", "test3.txt", false);

                Directory.CreateDirectory(folder + "\\dst");
                Helper.CopyFile(folder + "\\src\\*.*", folder + "\\dst", true);

                Assert.IsTrue(File.Exists(folder + "\\dst\\test1.txt"));
                Assert.IsTrue(File.Exists(folder + "\\dst\\test2.txt"));
                Assert.IsTrue(File.Exists(folder + "\\dst\\test3.txt"));

                Assert.IsTrue(File.Exists(folder + "\\dst\\0\\test1.txt"));
                Assert.IsTrue(File.Exists(folder + "\\dst\\0\\test2.txt"));
                Assert.IsTrue(File.Exists(folder + "\\dst\\0\\test3.txt"));

                Assert.IsTrue(File.Exists(folder + "\\dst\\1\\test1.txt"));
                Assert.IsTrue(File.Exists(folder + "\\dst\\1\\test2.txt"));
                Assert.IsTrue(File.Exists(folder + "\\dst\\1\\test3.txt"));

                Assert.IsTrue(File.Exists(folder + "\\dst\\2\\test1.txt"));
                Assert.IsTrue(File.Exists(folder + "\\dst\\2\\test2.txt"));
                Assert.IsTrue(File.Exists(folder + "\\dst\\2\\test3.txt"));

                Assert.IsTrue(File.Exists(folder + "\\dst\\3\\test1.txt"));
                Assert.IsTrue(File.Exists(folder + "\\dst\\3\\test2.txt"));
                Assert.IsTrue(File.Exists(folder + "\\dst\\3\\test3.txt"));
            }
            finally
            {
                Helper.DeleteFile(folder, true);
            }
        }

        [TestMethod]
        [TestProperty("Lib", "LillTek.Common")]
        public void Helper_CopyFile_FolderTree_Exists2()
        {
            string folder = Path.GetTempPath() + Helper.NewGuid().ToString();

            Directory.CreateDirectory(folder);

            try
            {
                Directory.CreateDirectory(folder + "\\src");
                CreateFile(folder + "\\src", "test1.txt", false);
                CreateFile(folder + "\\src", "test2.txt", false);
                CreateFile(folder + "\\src", "test3.txt", false);

                Directory.CreateDirectory(folder + "\\src\\0");
                CreateFile(folder + "\\src\\0", "test1.txt", false);
                CreateFile(folder + "\\src\\0", "test2.txt", false);
                CreateFile(folder + "\\src\\0", "test3.txt", false);

                Directory.CreateDirectory(folder + "\\src\\1");
                CreateFile(folder + "\\src\\1", "test1.txt", false);
                CreateFile(folder + "\\src\\1", "test2.txt", false);
                CreateFile(folder + "\\src\\1", "test3.txt", false);

                Directory.CreateDirectory(folder + "\\src\\2");
                CreateFile(folder + "\\src\\2", "test1.txt", false);
                CreateFile(folder + "\\src\\2", "test2.txt", false);
                CreateFile(folder + "\\src\\2", "test3.txt", false);

                Directory.CreateDirectory(folder + "\\src\\3");
                CreateFile(folder + "\\src\\3", "test1.txt", false);
                CreateFile(folder + "\\src\\3", "test2.txt", false);
                CreateFile(folder + "\\src\\3", "test3.txt", false);

                Directory.CreateDirectory(folder + "\\dst");
                Helper.CopyFile(folder + "\\src", folder + "\\dst", true);

                Assert.IsTrue(File.Exists(folder + "\\dst\\src\\test1.txt"));
                Assert.IsTrue(File.Exists(folder + "\\dst\\src\\test2.txt"));
                Assert.IsTrue(File.Exists(folder + "\\dst\\src\\test3.txt"));

                Assert.IsTrue(File.Exists(folder + "\\dst\\src\\0\\test1.txt"));
                Assert.IsTrue(File.Exists(folder + "\\dst\\src\\0\\test2.txt"));
                Assert.IsTrue(File.Exists(folder + "\\dst\\src\\0\\test3.txt"));

                Assert.IsTrue(File.Exists(folder + "\\dst\\src\\1\\test1.txt"));
                Assert.IsTrue(File.Exists(folder + "\\dst\\src\\1\\test2.txt"));
                Assert.IsTrue(File.Exists(folder + "\\dst\\src\\1\\test3.txt"));

                Assert.IsTrue(File.Exists(folder + "\\dst\\src\\2\\test1.txt"));
                Assert.IsTrue(File.Exists(folder + "\\dst\\src\\2\\test2.txt"));
                Assert.IsTrue(File.Exists(folder + "\\dst\\src\\2\\test3.txt"));

                Assert.IsTrue(File.Exists(folder + "\\dst\\src\\3\\test1.txt"));
                Assert.IsTrue(File.Exists(folder + "\\dst\\src\\3\\test2.txt"));
                Assert.IsTrue(File.Exists(folder + "\\dst\\src\\3\\test3.txt"));
            }
            finally
            {
                Helper.DeleteFile(folder, true);
            }
        }

        [TestMethod]
        [TestProperty("Lib", "LillTek.Common")]
        public void Helper_CopyFile_FolderTree_Overwrite()
        {
            string folder = Path.GetTempPath() + Helper.NewGuid().ToString();

            Directory.CreateDirectory(folder);

            try
            {
                Directory.CreateDirectory(folder + "\\src");
                CreateFile(folder + "\\src", "test1.txt", true);
                CreateFile(folder + "\\src", "test2.txt", true);
                CreateFile(folder + "\\src", "test3.txt", true);

                Directory.CreateDirectory(folder + "\\src\\0");
                CreateFile(folder + "\\src\\0", "test1.txt", true);
                CreateFile(folder + "\\src\\0", "test2.txt", true);
                CreateFile(folder + "\\src\\0", "test3.txt", true);

                Directory.CreateDirectory(folder + "\\src\\1");
                CreateFile(folder + "\\src\\1", "test1.txt", true);
                CreateFile(folder + "\\src\\1", "test2.txt", true);
                CreateFile(folder + "\\src\\1", "test3.txt", true);

                Directory.CreateDirectory(folder + "\\src\\2");
                CreateFile(folder + "\\src\\2", "test1.txt", true);
                CreateFile(folder + "\\src\\2", "test2.txt", true);
                CreateFile(folder + "\\src\\2", "test3.txt", true);

                Directory.CreateDirectory(folder + "\\src\\3");
                CreateFile(folder + "\\src\\3", "test1.txt", true);
                CreateFile(folder + "\\src\\3", "test2.txt", true);
                CreateFile(folder + "\\src\\3", "test3.txt", true);

                Directory.CreateDirectory(folder + "\\dst");
                Helper.CopyFile(folder + "\\src", folder + "\\dst", true);

                Assert.IsTrue((File.GetAttributes(folder + "\\dst\\src\\test1.txt") & FileAttributes.ReadOnly) != 0);
                Assert.IsTrue((File.GetAttributes(folder + "\\dst\\src\\test2.txt") & FileAttributes.ReadOnly) != 0);
                Assert.IsTrue((File.GetAttributes(folder + "\\dst\\src\\test3.txt") & FileAttributes.ReadOnly) != 0);

                Assert.IsTrue((File.GetAttributes(folder + "\\dst\\src\\0\\test1.txt") & FileAttributes.ReadOnly) != 0);
                Assert.IsTrue((File.GetAttributes(folder + "\\dst\\src\\0\\test2.txt") & FileAttributes.ReadOnly) != 0);
                Assert.IsTrue((File.GetAttributes(folder + "\\dst\\src\\0\\test3.txt") & FileAttributes.ReadOnly) != 0);

                Assert.IsTrue((File.GetAttributes(folder + "\\dst\\src\\1\\test1.txt") & FileAttributes.ReadOnly) != 0);
                Assert.IsTrue((File.GetAttributes(folder + "\\dst\\src\\1\\test2.txt") & FileAttributes.ReadOnly) != 0);
                Assert.IsTrue((File.GetAttributes(folder + "\\dst\\src\\1\\test3.txt") & FileAttributes.ReadOnly) != 0);

                Assert.IsTrue((File.GetAttributes(folder + "\\dst\\src\\2\\test1.txt") & FileAttributes.ReadOnly) != 0);
                Assert.IsTrue((File.GetAttributes(folder + "\\dst\\src\\2\\test2.txt") & FileAttributes.ReadOnly) != 0);
                Assert.IsTrue((File.GetAttributes(folder + "\\dst\\src\\2\\test3.txt") & FileAttributes.ReadOnly) != 0);

                Assert.IsTrue((File.GetAttributes(folder + "\\dst\\src\\3\\test1.txt") & FileAttributes.ReadOnly) != 0);
                Assert.IsTrue((File.GetAttributes(folder + "\\dst\\src\\3\\test2.txt") & FileAttributes.ReadOnly) != 0);
                Assert.IsTrue((File.GetAttributes(folder + "\\dst\\src\\3\\test3.txt") & FileAttributes.ReadOnly) != 0);

                File.SetAttributes(folder + "\\src\\test1.txt", FileAttributes.Normal);
                File.SetAttributes(folder + "\\src\\test2.txt", FileAttributes.Normal);
                File.SetAttributes(folder + "\\src\\test3.txt", FileAttributes.Normal);

                File.SetAttributes(folder + "\\src\\0\\test1.txt", FileAttributes.Normal);
                File.SetAttributes(folder + "\\src\\0\\test2.txt", FileAttributes.Normal);
                File.SetAttributes(folder + "\\src\\0\\test3.txt", FileAttributes.Normal);

                File.SetAttributes(folder + "\\src\\1\\test1.txt", FileAttributes.Normal);
                File.SetAttributes(folder + "\\src\\1\\test2.txt", FileAttributes.Normal);
                File.SetAttributes(folder + "\\src\\1\\test3.txt", FileAttributes.Normal);

                File.SetAttributes(folder + "\\src\\2\\test1.txt", FileAttributes.Normal);
                File.SetAttributes(folder + "\\src\\2\\test2.txt", FileAttributes.Normal);
                File.SetAttributes(folder + "\\src\\2\\test3.txt", FileAttributes.Normal);

                File.SetAttributes(folder + "\\src\\3\\test1.txt", FileAttributes.Normal);
                File.SetAttributes(folder + "\\src\\3\\test2.txt", FileAttributes.Normal);
                File.SetAttributes(folder + "\\src\\3\\test3.txt", FileAttributes.Normal);

                Helper.CopyFile(folder + "\\src", folder + "\\dst", true);

                Assert.IsTrue((File.GetAttributes(folder + "\\dst\\src\\test1.txt") & FileAttributes.ReadOnly) == 0);
                Assert.IsTrue((File.GetAttributes(folder + "\\dst\\src\\test2.txt") & FileAttributes.ReadOnly) == 0);
                Assert.IsTrue((File.GetAttributes(folder + "\\dst\\src\\test3.txt") & FileAttributes.ReadOnly) == 0);

                Assert.IsTrue((File.GetAttributes(folder + "\\dst\\src\\0\\test1.txt") & FileAttributes.ReadOnly) == 0);
                Assert.IsTrue((File.GetAttributes(folder + "\\dst\\src\\0\\test2.txt") & FileAttributes.ReadOnly) == 0);
                Assert.IsTrue((File.GetAttributes(folder + "\\dst\\src\\0\\test3.txt") & FileAttributes.ReadOnly) == 0);

                Assert.IsTrue((File.GetAttributes(folder + "\\dst\\src\\1\\test1.txt") & FileAttributes.ReadOnly) == 0);
                Assert.IsTrue((File.GetAttributes(folder + "\\dst\\src\\1\\test2.txt") & FileAttributes.ReadOnly) == 0);
                Assert.IsTrue((File.GetAttributes(folder + "\\dst\\src\\1\\test3.txt") & FileAttributes.ReadOnly) == 0);

                Assert.IsTrue((File.GetAttributes(folder + "\\dst\\src\\2\\test1.txt") & FileAttributes.ReadOnly) == 0);
                Assert.IsTrue((File.GetAttributes(folder + "\\dst\\src\\2\\test2.txt") & FileAttributes.ReadOnly) == 0);
                Assert.IsTrue((File.GetAttributes(folder + "\\dst\\src\\2\\test3.txt") & FileAttributes.ReadOnly) == 0);

                Assert.IsTrue((File.GetAttributes(folder + "\\dst\\src\\3\\test1.txt") & FileAttributes.ReadOnly) == 0);
                Assert.IsTrue((File.GetAttributes(folder + "\\dst\\src\\3\\test2.txt") & FileAttributes.ReadOnly) == 0);
                Assert.IsTrue((File.GetAttributes(folder + "\\dst\\src\\3\\test3.txt") & FileAttributes.ReadOnly) == 0);
            }
            finally
            {
                Helper.DeleteFile(folder, true);
            }
        }

        private void CreateTestFile(string path, int size)
        {
            using (FileStream fs = new FileStream(path, FileMode.Create, FileAccess.ReadWrite))
            {
                byte[] block = new byte[1024 * 64];

                for (int i = 0; i < block.Length; i++)
                    block[i] = (byte)i;

                for (int i = 0; i < size / block.Length; i++)
                    fs.Write(block, 0, block.Length);

                if (size % block.Length != 0)
                    fs.Write(block, 0, size % block.Length);
            }
        }

        [TestMethod]
        [TestProperty("Lib", "LillTek.Common")]
        public void Helper_CopyFile_BandwidthLimit()
        {
            string folder = Path.GetTempPath() + Helper.NewGuid().ToString();
            string srcPath = folder + "\\input.dat";
            string dstPath = folder + "\\output.dat";
            int cbFile = 1024 * 1024;                            // 1MB

            //dstPath = @"\\s00\test\output.dat";                       // Manually set this to a network share
            //cbFile  = 1024*1024*1024;                                 // 1GB

            Directory.CreateDirectory(folder);

            try
            {
                // Test without bandwidth contraints

                CreateTestFile(srcPath, 0);                              // Zero length file
                Helper.CopyFile(srcPath, dstPath, false, 0, 0);
                Assert.IsTrue(Helper.CompareFiles(srcPath, dstPath));

                CreateTestFile(srcPath, cbFile);
                Helper.CopyFile(srcPath, dstPath, false, 0, 0);
                Assert.IsTrue(Helper.CompareFiles(srcPath, dstPath));

                // Test with bandwidth contraints

                CreateTestFile(srcPath, 0);                              // Zero length file
                Helper.CopyFile(srcPath, dstPath, false, 8092, 1000000);
                Assert.IsTrue(Helper.CompareFiles(srcPath, dstPath));

                CreateTestFile(srcPath, cbFile);
                Helper.CopyFile(srcPath, dstPath, false, 8092, 1000000);
                Assert.IsTrue(Helper.CompareFiles(srcPath, dstPath));

                Assert.Inconclusive("Perform test manually against a remote share while looking at bandwidth via PerfMon.");
            }
            finally
            {
                Helper.DeleteFile(folder, true);
            }
        }

        [TestMethod]
        [TestProperty("Lib", "LillTek.Common")]
        public void Helper_CreateFolderTree()
        {
            string folder = Path.GetTempPath() + Helper.NewGuid().ToString();

            Directory.CreateDirectory(folder);

            try
            {
                Helper.CreateFolderTree(folder + @"\level0\level1\level2\level3");
                Assert.IsTrue(Directory.Exists(folder + @"\level0"));
                Assert.IsTrue(Directory.Exists(folder + @"\level0\level1"));
                Assert.IsTrue(Directory.Exists(folder + @"\level0\level1\level2"));
                Assert.IsTrue(Directory.Exists(folder + @"\level0\level1\level2\level3"));

                Helper.CreateFolderTree(folder + @"\xxx\level1\level2\level3\");
                Assert.IsTrue(Directory.Exists(folder + @"\xxx"));
                Assert.IsTrue(Directory.Exists(folder + @"\xxx\level1"));
                Assert.IsTrue(Directory.Exists(folder + @"\xxx\level1\level2"));
                Assert.IsTrue(Directory.Exists(folder + @"\xxx\level1\level2\level3"));
            }
            finally
            {
                Helper.DeleteFile(folder, true);
            }
        }

        [TestMethod]
        [TestProperty("Lib", "LillTek.Common")]
        public void Helper_GetFileNameWithoutExtension()
        {
            Assert.AreEqual("test", Helper.GetFileNameWithoutExtension(@"c:\test.dat"));
            Assert.AreEqual("test", Helper.GetFileNameWithoutExtension(@"c:\path\test.dat"));
            Assert.AreEqual("test", Helper.GetFileNameWithoutExtension(@"test"));
            Assert.AreEqual("test", Helper.GetFileNameWithoutExtension(@"test.dat"));
            Assert.AreEqual("test", Helper.GetFileNameWithoutExtension(@"..\test.dat"));
            Assert.AreEqual("test.dat", Helper.GetFileNameWithoutExtension(@"test.dat.dat"));
        }

        [TestMethod]
        [TestProperty("Lib", "LillTek.Common")]
        public void Helper_Execute()
        {
            var result = Helper.ExecuteCaptureStreams("sc.exe", "query", TimeSpan.MaxValue);

            Assert.AreEqual(0, result.ExitCode);
            Assert.IsTrue(result.StandardOutput != string.Empty);
            Assert.IsTrue(string.IsNullOrWhiteSpace(result.StandardError));
        }

        [TestMethod]
        [TestProperty("Lib", "LillTek.Common")]
        public void Helper_IsEmpty()
        {
            Assert.IsTrue(string.IsNullOrWhiteSpace(null));
            Assert.IsTrue(string.IsNullOrWhiteSpace(string.Empty));
            Assert.IsTrue(string.IsNullOrWhiteSpace(" \t\r\n\t "));
            Assert.IsFalse(string.IsNullOrWhiteSpace("x"));
        }

        [TestMethod]
        [TestProperty("Lib", "LillTek.Common")]
        public void Helper_StripCRLF()
        {
            Assert.AreEqual(string.Empty, Helper.StripCRLF(null));
            Assert.AreEqual(string.Empty, Helper.StripCRLF(string.Empty));
            Assert.AreEqual("test0 test1 test2 test3", Helper.StripCRLF("test0\rtest1\ntest2\r\ntest3"));
        }

        [TestMethod]
        [TestProperty("Lib", "LillTek.Common")]
        public void Helper_NormalizeUri()
        {
            Assert.AreEqual("http://foo.com/", Helper.Normalize(new Uri("http://foo.com/"), false));
            Assert.AreEqual("http://foo.com/", Helper.Normalize(new Uri("HTTP://foo.com/"), false));
            Assert.AreEqual("http://foo.com:77/", Helper.Normalize(new Uri("HTTP://FOO.COM:77/"), false));
            Assert.AreEqual("http://foo.com/TEST.ASPX", Helper.Normalize(new Uri("http://foo.com/TEST.ASPX"), false));
            Assert.AreEqual("http://foo.com/A/B/TEST.ASPX?aBcD", Helper.Normalize(new Uri("http://foo.com/A/B/TEST.ASPX?aBcD"), false));

            Assert.AreEqual("http://foo.com/", Helper.Normalize(new Uri("http://foo.com/"), true));
            Assert.AreEqual("http://foo.com/", Helper.Normalize(new Uri("HTTP://foo.com/"), true));
            Assert.AreEqual("http://foo.com:77/", Helper.Normalize(new Uri("HTTP://FOO.COM:77/"), true));
            Assert.AreEqual("http://foo.com/test.aspx", Helper.Normalize(new Uri("http://foo.com/TEST.ASPX"), true));
            Assert.AreEqual("http://foo.com/a/b/test.aspx?aBcD", Helper.Normalize(new Uri("http://foo.com/A/B/TEST.ASPX?aBcD"), true));
        }

        [TestMethod]
        [TestProperty("Lib", "LillTek.Common")]
        public void Helper_ParseCsv()
        {
            string[] fields;

            fields = Helper.ParseCsv("");
            CollectionAssert.AreEqual(new string[] { "" }, fields);

            fields = Helper.ParseCsv("1");
            CollectionAssert.AreEqual(new string[] { "1" }, fields);

            fields = Helper.ParseCsv("1,2,3,4");
            CollectionAssert.AreEqual(new string[] { "1", "2", "3", "4" }, fields);

            fields = Helper.ParseCsv("abc,def");
            CollectionAssert.AreEqual(new string[] { "abc", "def" }, fields);

            fields = Helper.ParseCsv("abc,def,");
            CollectionAssert.AreEqual(new string[] { "abc", "def", "" }, fields);

            fields = Helper.ParseCsv("\"\"");
            CollectionAssert.AreEqual(new string[] { "" }, fields);

            fields = Helper.ParseCsv("\"abc\"");
            CollectionAssert.AreEqual(new string[] { "abc" }, fields);

            fields = Helper.ParseCsv("\"abc,def\"");
            CollectionAssert.AreEqual(new string[] { "abc,def" }, fields);

            fields = Helper.ParseCsv("\"a,b\",\"c,d\"");
            CollectionAssert.AreEqual(new string[] { "a,b", "c,d" }, fields);

            fields = Helper.ParseCsv("\"a,b\",\"c,d\",e");
            CollectionAssert.AreEqual(new string[] { "a,b", "c,d", "e" }, fields);

            fields = Helper.ParseCsv("\"abc\r\ndef\"");
            CollectionAssert.AreEqual(new string[] { "abc\r\ndef" }, fields);

            fields = Helper.ParseCsv("0,1,,,4");
            CollectionAssert.AreEqual(new string[] { "0", "1", "", "", "4" }, fields);

            fields = Helper.ParseCsv(",,,,");
            CollectionAssert.AreEqual(new string[] { "", "", "", "", "" }, fields);

            try
            {
                fields = Helper.ParseCsv("\"abc");
                Assert.Fail("Expected an FormatException");
            }
            catch (FormatException)
            {
                // Expecting to catch an terminated quoted field.
            }
        }

        [TestMethod]
        [TestProperty("Lib", "LillTek.Common")]
        public void Helper_ArrayEquals_Generic()
        {
            Assert.IsTrue(Helper.ArrayEquals((Array)null, (Array)null));
            Assert.IsFalse(Helper.ArrayEquals((Array)null, (Array)new int[0]));
            Assert.IsFalse(Helper.ArrayEquals((Array)new int[0], (Array)null));
            Assert.IsTrue(Helper.ArrayEquals((Array)new int[] { 0, 1, 2, 3 }, (Array)new int[] { 0, 1, 2, 3 }));
            Assert.IsFalse(Helper.ArrayEquals((Array)new int[] { 0, 1, 2, 3 }, (Array)new int[] { 0, 1, 2 }));
            Assert.IsFalse(Helper.ArrayEquals((Array)new int[] { 0, 1, 2, 3 }, (Array)new int[] { 0, 1, 2, 4 }));
            Assert.IsTrue(Helper.ArrayEquals((Array)new string[] { "a", "b" }, (Array)new string[] { "a", "b" }));
            Assert.IsFalse(Helper.ArrayEquals((Array)new string[] { "a", "b" }, (Array)new string[] { "a", "b", "c" }));
            Assert.IsTrue(Helper.ArrayEquals((Array)new string[] { "a", "b", null }, (Array)new string[] { "a", "b", null }));
            Assert.IsFalse(Helper.ArrayEquals((Array)new string[] { "a", "b", "c" }, (Array)new string[] { "a", "b", null }));
            Assert.IsFalse(Helper.ArrayEquals((Array)new string[] { "a", "b", null }, (Array)new string[] { "a", "b", "c" }));
            Assert.IsFalse(Helper.ArrayEquals((Array)new string[] { "a", "b", "c" }, (Array)new string[] { "a", "b", "d" }));
            Assert.IsFalse(Helper.ArrayEquals((Array)new string[] { "a", "b", "c" }, (Array)new int[] { 1, 2, 3 }));
        }

        [TestMethod]
        [TestProperty("Lib", "LillTek.Common")]
        public void Helper_ArrayEquals_Int()
        {
            Assert.IsTrue(Helper.ArrayEquals((int[])null, (int[])null));
            Assert.IsFalse(Helper.ArrayEquals((int[])null, new int[0]));
            Assert.IsFalse(Helper.ArrayEquals(new int[0], (int[])null));
            Assert.IsTrue(Helper.ArrayEquals(new int[0], new int[0]));
            Assert.IsTrue(Helper.ArrayEquals(new int[] { 0, 1, 2 }, new int[] { 0, 1, 2 }));
            Assert.IsFalse(Helper.ArrayEquals(new int[] { 0, 1, 2 }, new int[] { 0, 1, 3 }));
            Assert.IsFalse(Helper.ArrayEquals(new int[] { 0, 1, 2 }, new int[] { 0, 1, 2, 4 }));
        }

        [TestMethod]
        [TestProperty("Lib", "LillTek.Common")]
        public void Helper_ArrayEquals_Short()
        {
            Assert.IsTrue(Helper.ArrayEquals((short[])null, (short[])null));
            Assert.IsFalse(Helper.ArrayEquals((short[])null, new short[0]));
            Assert.IsFalse(Helper.ArrayEquals(new short[0], (short[])null));
            Assert.IsTrue(Helper.ArrayEquals(new short[0], new short[0]));
            Assert.IsTrue(Helper.ArrayEquals(new short[] { 0, 1, 2 }, new short[] { 0, 1, 2 }));
            Assert.IsFalse(Helper.ArrayEquals(new short[] { 0, 1, 2 }, new short[] { 0, 1, 3 }));
            Assert.IsFalse(Helper.ArrayEquals(new short[] { 0, 1, 2 }, new short[] { 0, 1, 2, 4 }));
        }

        [TestMethod]
        [TestProperty("Lib", "LillTek.Common")]
        public void Helper_ArrayEquals_Long()
        {
            Assert.IsTrue(Helper.ArrayEquals((long[])null, (long[])null));
            Assert.IsFalse(Helper.ArrayEquals((long[])null, new long[0]));
            Assert.IsFalse(Helper.ArrayEquals(new long[0], (long[])null));
            Assert.IsTrue(Helper.ArrayEquals(new long[0], new long[0]));
            Assert.IsTrue(Helper.ArrayEquals(new long[] { 0, 1, 2 }, new long[] { 0, 1, 2 }));
            Assert.IsFalse(Helper.ArrayEquals(new long[] { 0, 1, 2 }, new long[] { 0, 1, 3 }));
            Assert.IsFalse(Helper.ArrayEquals(new long[] { 0, 1, 2 }, new long[] { 0, 1, 2, 4 }));
        }

        [TestMethod]
        [TestProperty("Lib", "LillTek.Common")]
        public void Helper_ArrayEquals_Byte()
        {
            Assert.IsTrue(Helper.ArrayEquals((byte[])null, (byte[])null));
            Assert.IsFalse(Helper.ArrayEquals((byte[])null, new byte[0]));
            Assert.IsFalse(Helper.ArrayEquals(new byte[0], (byte[])null));
            Assert.IsTrue(Helper.ArrayEquals(new byte[0], new byte[0]));
            Assert.IsTrue(Helper.ArrayEquals(new byte[] { 0, 1, 2 }, new byte[] { 0, 1, 2 }));
            Assert.IsFalse(Helper.ArrayEquals(new byte[] { 0, 1, 2 }, new byte[] { 0, 1, 3 }));
            Assert.IsFalse(Helper.ArrayEquals(new byte[] { 0, 1, 2 }, new byte[] { 0, 1, 2, 4 }));
        }

        [TestMethod]
        [TestProperty("Lib", "LillTek.Common")]
        public void Helper_ArrayConcat_Byte()
        {
            CollectionAssert.AreEqual(new byte[] { 0, 1 }, Helper.Concat(new byte[] { 0, 1 }));
            CollectionAssert.AreEqual(new byte[] { 0, 1, 2, 3, 4, 5, 6 }, Helper.Concat(new byte[] { 0, 1, 2, 3 }, new byte[] { 4, 5, 6 }));
            CollectionAssert.AreEqual(new byte[] { 0, 1, 2, 3, 4 }, Helper.Concat(new byte[] { 0 }, new byte[] { 1 }, new byte[] { 2 }, new byte[] { 3, 4 }));
        }

        [TestMethod]
        [TestProperty("Lib", "LillTek.Common")]
        public void Helper_ArrayConcat_String()
        {
            CollectionAssert.AreEqual(new string[] { "a", "b" }, Helper.Concat(new string[] { "a", "b" }));
            CollectionAssert.AreEqual(new string[] { "0", "1", "2", "3", "4", "5", "6" }, Helper.Concat(new string[] { "0", "1", "2", "3" }, new string[] { "4", "5", "6" }));
            CollectionAssert.AreEqual(new string[] { "0", "1", "2", "3", "4" }, Helper.Concat(new string[] { "0" }, new string[] { "1" }, new string[] { "2" }, new string[] { "3", "4" }));
        }

        [TestMethod]
        [TestProperty("Lib", "LillTek.Common")]
        public void Helper_GetUserName()
        {
            Assert.AreEqual("account", Helper.GetUserName(RealmFormat.Email, null, "account"));
            Assert.AreEqual("account", Helper.GetUserName(RealmFormat.Email, "", "account"));
            Assert.AreEqual("account@realm", Helper.GetUserName(RealmFormat.Email, "realm", "account"));

            Assert.AreEqual("account", Helper.GetUserName(RealmFormat.Slash, null, "account"));
            Assert.AreEqual("account", Helper.GetUserName(RealmFormat.Slash, "", "account"));
            Assert.AreEqual("realm/account", Helper.GetUserName(RealmFormat.Slash, "realm", "account"));
        }

        [TestMethod]
        [TestProperty("Lib", "LillTek.Common")]
        public void Helper_ParseUserName()
        {
            string realm;
            string account;

            Helper.ParseUserName(RealmFormat.Email, "account", out realm, out account);
            Assert.AreEqual("", realm);
            Assert.AreEqual("account", account);

            Helper.ParseUserName(RealmFormat.Email, "account@realm", out realm, out account);
            Assert.AreEqual("realm", realm);
            Assert.AreEqual("account", account);

            Helper.ParseUserName(RealmFormat.Slash, "account", out realm, out account);
            Assert.AreEqual("", realm);
            Assert.AreEqual("account", account);

            Helper.ParseUserName(RealmFormat.Slash, "realm/account", out realm, out account);
            Assert.AreEqual("realm", realm);
            Assert.AreEqual("account", account);

            Helper.ParseUserName(RealmFormat.Slash, "realm\\account", out realm, out account);
            Assert.AreEqual("realm", realm);
            Assert.AreEqual("account", account);
        }

        [TestMethod]
        [TestProperty("Lib", "LillTek.Common")]
        public void Helper_Within()
        {
            Assert.IsTrue(Helper.Within(new DateTime(2000, 10, 10, 12, 0, 0), new DateTime(2000, 10, 10, 12, 0, 0), TimeSpan.Zero));
            Assert.IsTrue(Helper.Within(new DateTime(2000, 10, 10, 12, 1, 0), new DateTime(2000, 10, 10, 12, 0, 0), TimeSpan.FromMinutes(1)));
            Assert.IsTrue(Helper.Within(new DateTime(2000, 10, 10, 12, 0, 0), new DateTime(2000, 10, 10, 12, 1, 0), TimeSpan.FromMinutes(1)));
            Assert.IsFalse(Helper.Within(new DateTime(2000, 10, 10, 12, 1, 1), new DateTime(2000, 10, 10, 12, 0, 0), TimeSpan.FromMinutes(1)));
            Assert.IsFalse(Helper.Within(new DateTime(2000, 10, 10, 12, 0, 0), new DateTime(2000, 10, 10, 12, 1, 1), TimeSpan.FromMinutes(1)));
        }

        [TestMethod]
        [TestProperty("Lib", "LillTek.Common")]
        public void Helper_MinMax_Date()
        {
            Assert.AreEqual(new DateTime(2000, 1, 1), Helper.Min(new DateTime(2000, 1, 1), new DateTime(2001, 1, 1)));
            Assert.AreEqual(new DateTime(2000, 1, 1), Helper.Min(new DateTime(2001, 1, 1), new DateTime(2000, 1, 1)));

            Assert.AreEqual(new DateTime(2001, 1, 1), Helper.Max(new DateTime(2000, 1, 1), new DateTime(2001, 1, 1)));
            Assert.AreEqual(new DateTime(2001, 1, 1), Helper.Max(new DateTime(2001, 1, 1), new DateTime(2000, 1, 1)));
        }

        [TestMethod]
        [TestProperty("Lib", "LillTek.Common")]
        public void Helper_MinMax_TimeSpan()
        {
            Assert.AreEqual(TimeSpan.FromMinutes(1), Helper.Min(TimeSpan.FromMinutes(1), TimeSpan.FromMinutes(2)));
            Assert.AreEqual(TimeSpan.FromMinutes(1), Helper.Min(TimeSpan.FromMinutes(2), TimeSpan.FromMinutes(1)));

            Assert.AreEqual(TimeSpan.FromMinutes(2), Helper.Max(TimeSpan.FromMinutes(1), TimeSpan.FromMinutes(2)));
            Assert.AreEqual(TimeSpan.FromMinutes(2), Helper.Max(TimeSpan.FromMinutes(2), TimeSpan.FromMinutes(1)));
        }

        [TestMethod]
        [TestProperty("Lib", "LillTek.Common")]
        public void Helper_StripWhitespace()
        {
            Assert.AreEqual("", Helper.StripWhitespace(""));
            Assert.AreEqual("", Helper.StripWhitespace("     "));
            Assert.AreEqual("abcdefg", Helper.StripWhitespace("abcdefg"));
            Assert.AreEqual("abcdefg", Helper.StripWhitespace("  abcdefg"));
            Assert.AreEqual("abcdefg", Helper.StripWhitespace("   abcdefg   "));
            Assert.AreEqual("abcdefg", Helper.StripWhitespace(" \t\t\r\n  abcd \t\r\n efg \r\r\n"));
        }

        [TestMethod]
        [TestProperty("Lib", "LillTek.Common")]
        public void Helper_TryParseIPAddress()
        {
            IPAddress addr;

            Assert.IsTrue(Helper.TryParseIPAddress("1.2.3.4", out addr));
            Assert.AreEqual(IPAddress.Parse("1.2.3.4"), addr);

            Assert.IsTrue(Helper.TryParseIPAddress("ANY", out addr));
            Assert.AreEqual(IPAddress.Any, addr);

            Assert.IsTrue(Helper.TryParseIPAddress("loopback", out addr));
            Assert.AreEqual(IPAddress.Loopback, addr);
        }

        [TestMethod]
        [TestProperty("Lib", "LillTek.Common")]
        public void Helper_ParseIPAddress()
        {
            Assert.AreEqual(IPAddress.Parse("1.2.3.4"), Helper.ParseIPAddress("1.2.3.4"));
            Assert.AreEqual(IPAddress.Any, Helper.ParseIPAddress("ANY"));
            Assert.AreEqual(IPAddress.Loopback, Helper.ParseIPAddress("loopback"));
        }

        [TestMethod]
        [TestProperty("Lib", "LillTek.Common")]
        public void Helper_Dictionary_Clone()
        {
            Dictionary<string, string> input, output;

            input = new Dictionary<string, string>();
            input["foo"] = "bar";
            input["hello"] = "world!";

            output = Helper.Clone(input);
            Assert.AreNotSame(input, output);

            Assert.AreEqual(2, output.Count);
            Assert.AreEqual("bar", output["foo"]);
            Assert.AreEqual("world!", output["hello"]);
            Assert.IsFalse(output.ContainsKey("FOO"));

            input = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            input["foo"] = "bar";
            Assert.IsTrue(input.ContainsKey("FOO"));

            output = Helper.Clone(input);
            Assert.AreSame(input.Comparer, output.Comparer);
            Assert.IsTrue(output.ContainsKey("FOO"));
        }

        [TestMethod]
        [TestProperty("Lib", "LillTek.Common")]
        public void Helper_ReadWriteBuf()
        {
            // $todo(jeff.lill): I need to implement some more tests here.

            byte[] buf = new byte[1024];
            int pos;

            pos = 0;
            Helper.WriteBytes(buf, ref pos, new byte[] { 0, 1, 2, 3, 4 });
            Assert.AreEqual(5, pos);
            pos = 0;
            CollectionAssert.AreEqual(new byte[] { 0, 1, 2, 3, 4 }, Helper.ReadBytes(buf, ref pos, 5));
            Assert.AreEqual(5, pos);

            Guid guid = Helper.NewGuid();

            pos = 0;
            Helper.WriteGuid(buf, ref pos, guid);
            Assert.AreEqual(16, pos);
            pos = 0;
            Assert.AreEqual(guid, Helper.ReadGuid(buf, ref pos));
            Assert.AreEqual(16, pos);
        }

        private class TestException1 : Exception
        {
            public TestException1(string message)
                : base(message)
            {
            }

            public TestException1(string message, Exception innerException)
                : base(message, innerException)
            {
            }
        }

        private class TestException2 : Exception
        {
            public TestException2()
                : base("Test")
            {
            }
        }

        [TestMethod]
        [TestProperty("Lib", "LillTek.Common")]
        public void Helper_Rethrow()
        {
            Exception org = null;

            // Test an exception type that can be rethrown.

            try
            {
                try
                {
                    throw org = new TestException1("Hello World!");
                }
                catch (Exception e)
                {
                    Helper.Rethrow(e);
                }
            }
            catch (Exception e)
            {
                Assert.IsInstanceOfType(e, typeof(TestException1));
                Assert.AreEqual("Hello World!", e.Message);
                Assert.AreNotSame(org, e);
                Assert.IsNotNull(e.InnerException);
            }

            // Test an exception type that cannot be rethrown.

            try
            {
                try
                {
                    throw org = new TestException2();
                }
                catch (Exception e)
                {
                    Helper.Rethrow(e);
                }
            }
            catch (Exception e)
            {
                Assert.IsInstanceOfType(e, typeof(TestException2));
                Assert.AreEqual("Test", e.Message);
                Assert.AreSame(org, e);
                Assert.IsNull(e.InnerException);
            }
        }

        [TestMethod]
        [TestProperty("Lib", "LillTek.Common")]
        public void Helper_ToIsoDate()
        {
            Assert.AreEqual("2007-07-20T14:52:15Z", Helper.ToIsoDate(new DateTime(2007, 7, 20, 14, 52, 15)));
        }

        [TestMethod]
        [TestProperty("Lib", "LillTek.Common")]
        public void Helper_ParseIsoDate()
        {
            Assert.AreEqual(new DateTime(2007, 7, 20, 14, 52, 15), Helper.ParseIsoDate("2007-07-20T14:52:15Z"));
        }

        [TestMethod]
        [TestProperty("Lib", "LillTek.Common")]
        public void Helper_StripSlashes()
        {
            Assert.AreEqual("", Helper.StripSlashes(""));
            Assert.AreEqual("", Helper.StripSlashes("\\"));
            Assert.AreEqual("", Helper.StripSlashes("/"));
            Assert.AreEqual("test", Helper.StripSlashes("/test/"));
            Assert.AreEqual("test", Helper.StripSlashes("\\test\\"));
            Assert.AreEqual("test", Helper.StripSlashes("/test"));
            Assert.AreEqual("test", Helper.StripSlashes("test/"));
            Assert.AreEqual("test", Helper.StripSlashes("\\test"));
            Assert.AreEqual("test", Helper.StripSlashes("test\\"));
            Assert.AreEqual("test", Helper.StripSlashes("\\test/"));
            Assert.AreEqual("test", Helper.StripSlashes("/test\\"));
            Assert.AreEqual("hello/world", Helper.StripSlashes("\\hello/world\\"));
        }

        [TestMethod]
        [TestProperty("Lib", "LillTek.Common")]
        public void Helper_HasExtension()
        {
            Assert.IsTrue(Helper.HasExtension("test.foo", "foo"));
            Assert.IsTrue(Helper.HasExtension("test.foo", "FOO"));
            Assert.IsTrue(Helper.HasExtension("test.FOO", "foo"));
            Assert.IsTrue(Helper.HasExtension("test.", ""));
            Assert.IsTrue(Helper.HasExtension("test", ""));
            Assert.IsTrue(Helper.HasExtension("test.", null));

            Assert.IsFalse(Helper.HasExtension(@"c:\test.foo\hello.world", "foo"));

            Assert.IsTrue(Helper.HasExtension(@"c:\test.txt"));
            Assert.IsFalse(Helper.HasExtension(@"c:\test"));
            Assert.IsFalse(Helper.HasExtension(@"c:\test.txt\test"));
        }

        [TestMethod]
        [TestProperty("Lib", "LillTek.Common")]
        public void Helper_ConcatFolderPath()
        {
            Assert.AreEqual("/", Helper.ConcatFolderPath('/'));
            Assert.AreEqual("\\", Helper.ConcatFolderPath('\\'));
            Assert.AreEqual("/", Helper.ConcatFolderPath('/', ""));
            Assert.AreEqual("/", Helper.ConcatFolderPath('/', "/"));
            Assert.AreEqual("c:/hello/world/", Helper.ConcatFolderPath('/', "c:", "hello", "world"));
            Assert.AreEqual("c:/hello/world/", Helper.ConcatFolderPath('/', "c:", "/hello/", "/world/"));
            Assert.AreEqual("c:/hello/world/", Helper.ConcatFolderPath('/', "c:", "\\hello\\", "\\world\\"));
        }

        [TestMethod]
        [TestProperty("Lib", "LillTek.Common")]
        public void Helper_ConcatFilePath()
        {
            Assert.AreEqual("/test.doc", Helper.ConcatFilePath('/', "test.doc"));
            Assert.AreEqual("/test.doc", Helper.ConcatFilePath('/', "/", "test.doc"));
            Assert.AreEqual("/a/test.doc", Helper.ConcatFilePath('/', "/a", "test.doc"));
            Assert.AreEqual("/a/b/c/test.doc", Helper.ConcatFilePath('/', "/a", "b", "c", "test.doc"));
            Assert.AreEqual("/a/b/test.doc", Helper.ConcatFilePath('/', "/a", "", "b", "test.doc"));
        }

        [TestMethod]
        [TestProperty("Lib", "LillTek.Common")]
        public void Helper_CompressBuffer()
        {
            byte[] source;
            byte[] compressed;
            byte[] decompressed;

            source = new byte[512];
            for (int i = 0; i < 256; i++)
                source[i] = 1;

            for (int i = 256; i < 512; i++)
                source[i] = 2;

            compressed = Helper.Compress(source);
            Assert.IsTrue(compressed.Length < source.Length);

            decompressed = Helper.Decompress(compressed);
            CollectionAssert.AreEqual(source, decompressed);
        }

        [TestMethod]
        [TestProperty("Lib", "LillTek.Common")]
        public void Helper_CompressBufferToStream()
        {
            byte[] source = new byte[16384];

            for (int i = 0; i < 256; i++)
                source[i] = 1;

            for (int i = 256; i < 512; i++)
                source[i] = 2;

            for (int i = 512; i < 16384; i++)
                source[i] = 3;

            using (var output = new MemoryStream())
            {

                Helper.Compress(source, output);
                Assert.IsTrue((int)output.Length < source.Length);

                using (var decompressed = new MemoryStream())
                {

                    Helper.Compress(source, output);
                    Assert.IsTrue((int)output.Length < source.Length);

                    output.Position = 0;
                    Helper.Decompress(output, decompressed);
                    CollectionAssert.AreEqual(source, decompressed.ToArray());
                }
            }
        }

        [TestMethod]
        [TestProperty("Lib", "LillTek.Common")]
        public void Helper_CompressStreamToStream()
        {
            byte[] source = new byte[16384];

            for (int i = 0; i < 256; i++)
                source[i] = 1;

            for (int i = 256; i < 512; i++)
                source[i] = 2;

            for (int i = 512; i < 16384; i++)
                source[i] = 3;

            using (var input = new MemoryStream(source))
            {
                using (var output = new MemoryStream())
                {
                    using (var decompressed = new MemoryStream())
                    {
                        Helper.Compress(input, output);
                        Assert.IsTrue((int)output.Length < source.Length);

                        output.Position = 0;
                        Helper.Decompress(output, decompressed);
                        CollectionAssert.AreEqual(source, decompressed.ToArray());
                    }
                }
            }
        }

        [TestMethod]
        [TestProperty("Lib", "LillTek.Common")]
        public void Helper_CompressGZipBuffer()
        {
            byte[] source;
            byte[] compressed;
            byte[] decompressed;

            source = new byte[512];
            for (int i = 0; i < 256; i++)
                source[i] = 1;

            for (int i = 256; i < 512; i++)
                source[i] = 2;

            compressed = Helper.CompressGZip(source);
            Assert.IsTrue(compressed.Length < source.Length);

            decompressed = Helper.DecompressGZip(compressed);
            CollectionAssert.AreEqual(source, decompressed);
        }

        [TestMethod]
        [TestProperty("Lib", "LillTek.Common")]
        public void Helper_CompressGZipBufferToStream()
        {
            byte[] source = new byte[16384];

            for (int i = 0; i < 256; i++)
                source[i] = 1;

            for (int i = 256; i < 512; i++)
                source[i] = 2;

            for (int i = 512; i < 16384; i++)
                source[i] = 3;

            using (var output = new MemoryStream())
            {

                Helper.CompressGZip(source, output);
                Assert.IsTrue((int)output.Length < source.Length);

                using (var decompressed = new MemoryStream())
                {

                    Helper.Compress(source, output);
                    Assert.IsTrue((int)output.Length < source.Length);

                    output.Position = 0;
                    Helper.DecompressGZip(output, decompressed);
                    CollectionAssert.AreEqual(source, decompressed.ToArray());
                }
            }
        }

        [TestMethod]
        [TestProperty("Lib", "LillTek.Common")]
        public void Helper_CompressGZipStreamToStream()
        {
            byte[] source = new byte[16384];

            for (int i = 0; i < 256; i++)
                source[i] = 1;

            for (int i = 256; i < 512; i++)
                source[i] = 2;

            for (int i = 512; i < 16384; i++)
                source[i] = 3;

            using (var input = new MemoryStream(source))
            {
                using (var output = new MemoryStream())
                {
                    using (var decompressed = new MemoryStream())
                    {
                        Helper.CompressGZip(input, output);
                        Assert.IsTrue((int)output.Length < source.Length);

                        output.Position = 0;
                        Helper.DecompressGZip(output, decompressed);
                        CollectionAssert.AreEqual(source, decompressed.ToArray());
                    }
                }
            }
        }

        [TestMethod]
        [TestProperty("Lib", "LillTek.Common")]
        public void Helper_IsFileHidden()
        {
            string folder = "C:\\UnitTest";

            try
            {
                Helper.CreateFolderTree(folder);
                Directory.CreateDirectory(folder + "\\Hidden");
                Directory.CreateDirectory(folder + "\\Visible");

                Helper.AppendToFile(folder + "\\visible.txt", "test");
                Helper.AppendToFile(folder + "\\hidden.txt", "test");

                Helper.AppendToFile(folder + "\\Hidden\\hidden.txt", "test");
                Helper.AppendToFile(folder + "\\Visible\\visible.txt", "test");

                File.SetAttributes(folder + "\\Hidden", File.GetAttributes(folder + "\\Hidden") | FileAttributes.Hidden);
                File.SetAttributes(folder + "\\hidden.txt", File.GetAttributes(folder + "\\hidden.txt") | FileAttributes.Hidden);

                Assert.IsFalse(Helper.IsFileHidden(folder + "\\visible.txt"));
                Assert.IsTrue(Helper.IsFileHidden(folder + "\\hidden.txt"));
                Assert.IsFalse(Helper.IsFileHidden(folder + "\\Visible\\visible.txt"));
                Assert.IsTrue(Helper.IsFileHidden(folder + "\\Hidden\\hidden.txt"));
            }
            finally
            {
                Helper.DeleteFile(folder + "\\*.*", true);
                Helper.DeleteFile(folder, true);
            }
        }

        [TestMethod]
        [TestProperty("Lib", "LillTek.Common")]
        public void Helper_ParseList()
        {
            string[] arr;

            arr = Helper.ParseList("", ';');
            CollectionAssert.AreEqual(new string[] { "" }, arr);

            arr = Helper.ParseList("test", ';');
            CollectionAssert.AreEqual(new string[] { "test" }, arr);

            arr = Helper.ParseList("test;", ';');
            CollectionAssert.AreEqual(new string[] { "test" }, arr);

            arr = Helper.ParseList("test1;test2", ';');
            CollectionAssert.AreEqual(new string[] { "test1", "test2" }, arr);

            arr = Helper.ParseList("test1;test2;", ';');
            CollectionAssert.AreEqual(new string[] { "test1", "test2" }, arr);

            arr = Helper.ParseList("test1;test2;test3", ';');
            CollectionAssert.AreEqual(new string[] { "test1", "test2", "test3" }, arr);

            arr = Helper.ParseList("test1;test2;;test3", ';');
            CollectionAssert.AreEqual(new string[] { "test1", "test2", "test3" }, arr);

            arr = Helper.ParseList(" test1 ; test2 ; ; test3 ", ';');
            CollectionAssert.AreEqual(new string[] { "test1", "test2", "test3" }, arr);
        }

        [TestMethod]
        [TestProperty("Lib", "LillTek.Common")]
        public void Helper_IndexOf()
        {
            Assert.AreEqual(-1, Helper.IndexOf(new byte[] { }, new byte[] { 1 }));

            Assert.AreEqual(0, Helper.IndexOf(new byte[] { 0, 1, 2, 3 }, new byte[] { 0 }));
            Assert.AreEqual(1, Helper.IndexOf(new byte[] { 0, 1, 2, 3 }, new byte[] { 1 }));
            Assert.AreEqual(3, Helper.IndexOf(new byte[] { 0, 1, 2, 3 }, new byte[] { 3 }));

            Assert.AreEqual(0, Helper.IndexOf(new byte[] { 0, 1, 2, 3 }, new byte[] { 0, 1, 2 }));
            Assert.AreEqual(1, Helper.IndexOf(new byte[] { 0, 1, 2, 3 }, new byte[] { 1, 2, 3 }));
            Assert.AreEqual(2, Helper.IndexOf(new byte[] { 0, 1, 2, 3 }, new byte[] { 2, 3 }));
            Assert.AreEqual(-1, Helper.IndexOf(new byte[] { 0, 1, 2, 3 }, new byte[] { 2, 3, 4 }));
        }

        [TestMethod]
        [TestProperty("Lib", "LillTek.Common")]
        public void Helper_IndexOfSection()
        {
            Assert.AreEqual(-1, Helper.IndexOf(new byte[] { }, new byte[] { 1 }, 0, 0));

            Assert.AreEqual(0, Helper.IndexOf(new byte[] { 0, 1, 2, 3 }, new byte[] { 0 }, 0, 4));
            Assert.AreEqual(1, Helper.IndexOf(new byte[] { 0, 1, 2, 3 }, new byte[] { 1 }, 0, 4));
            Assert.AreEqual(3, Helper.IndexOf(new byte[] { 0, 1, 2, 3 }, new byte[] { 3 }, 0, 4));

            Assert.AreEqual(0, Helper.IndexOf(new byte[] { 0, 1, 2, 3 }, new byte[] { 0, 1, 2 }, 0, 4));
            Assert.AreEqual(1, Helper.IndexOf(new byte[] { 0, 1, 2, 3 }, new byte[] { 1, 2, 3 }, 0, 4));
            Assert.AreEqual(2, Helper.IndexOf(new byte[] { 0, 1, 2, 3 }, new byte[] { 2, 3 }, 0, 4));
            Assert.AreEqual(-1, Helper.IndexOf(new byte[] { 0, 1, 2, 3 }, new byte[] { 2, 3, 4 }, 0, 4));

            Assert.AreEqual(-1, Helper.IndexOf(new byte[] { 10, 20 }, new byte[] { 1 }, 1, 0));

            Assert.AreEqual(1, Helper.IndexOf(new byte[] { 10, 0, 1, 2, 3, 20 }, new byte[] { 0 }, 1, 4));
            Assert.AreEqual(2, Helper.IndexOf(new byte[] { 10, 0, 1, 2, 3, 20 }, new byte[] { 1 }, 1, 4));
            Assert.AreEqual(4, Helper.IndexOf(new byte[] { 10, 0, 1, 2, 3, 20 }, new byte[] { 3 }, 1, 4));

            Assert.AreEqual(1, Helper.IndexOf(new byte[] { 10, 0, 1, 2, 3, 20 }, new byte[] { 0, 1, 2 }, 1, 4));
            Assert.AreEqual(2, Helper.IndexOf(new byte[] { 10, 0, 1, 2, 3, 20 }, new byte[] { 1, 2, 3 }, 1, 4));
            Assert.AreEqual(3, Helper.IndexOf(new byte[] { 10, 0, 1, 2, 3, 20 }, new byte[] { 2, 3 }, 1, 4));
            Assert.AreEqual(-1, Helper.IndexOf(new byte[] { 10, 0, 1, 2, 3, 20 }, new byte[] { 2, 3, 4 }, 1, 4));
            Assert.AreEqual(-1, Helper.IndexOf(new byte[] { 10, 0, 1, 2, 3, 20 }, new byte[] { 3, 20 }, 1, 4));
        }

        [TestMethod]
        [TestProperty("Lib", "LillTek.Common")]
        public void Helper_ToHex()
        {
            Assert.AreEqual("0123456789abcdef", Helper.ToHex(new byte[] { 0x01, 0x23, 0x45, 0x67, 0x89, 0xab, 0xcd, 0xef }));

            Assert.AreEqual("20", Helper.ToHex((byte)' '));
            Assert.AreEqual("7f", Helper.ToHex(0x7F));
            Assert.AreEqual("5f", Helper.ToHex(0x5F));
        }

        [TestMethod]
        [TestProperty("Lib", "LillTek.Common")]
        public void Helper_InSameSubnet()
        {
            Assert.IsTrue(Helper.InSameSubnet(IPAddress.Parse("127.0.0.1"), IPAddress.Parse("127.0.0.2"), IPAddress.Parse("255.255.255.0")));
            Assert.IsFalse(Helper.InSameSubnet(IPAddress.Parse("127.0.1.1"), IPAddress.Parse("127.0.0.2"), IPAddress.Parse("255.255.255.0")));
            Assert.IsTrue(Helper.InSameSubnet(IPAddress.Parse("127.0.0.1"), IPAddress.Parse("127.0.0.2"), IPAddress.Parse("255.255.0.0")));
        }

        [TestMethod]
        [TestProperty("Lib", "LillTek.Common")]
        public void Helper_TryParseUri()
        {
            Uri uri;

            Assert.IsTrue(Helper.TryParseUri("http://www.lilltek.com:80/default.htm", out uri));
            Assert.AreEqual(new Uri("http://www.lilltek.com:80/default.htm"), uri);

            Assert.IsFalse(Helper.TryParseUri("http://:80/default.htm", out uri));
            Assert.IsFalse(Helper.TryParseUri("http://test.com:tt/default.htm", out uri));
            Assert.IsFalse(Helper.TryParseUri("//:80/default.htm", out uri));
        }

        [TestMethod]
        [TestProperty("Lib", "LillTek.Common")]
        public void Helper_IsValidDomainName()
        {
            Assert.IsTrue(Helper.IsValidDomainName("test"));
            Assert.IsTrue(Helper.IsValidDomainName("0test"));
            Assert.IsTrue(Helper.IsValidDomainName("0123456789"));
            Assert.IsTrue(Helper.IsValidDomainName("abcdefghijklmnopqrstuvwxyz"));
            Assert.IsTrue(Helper.IsValidDomainName("ABCDEFGHIJKLMNOPQRSTUVWXYZ"));
            Assert.IsTrue(Helper.IsValidDomainName("test-01"));
            Assert.IsTrue(Helper.IsValidDomainName("test.lilltek.com"));
            Assert.IsTrue(Helper.IsValidDomainName("test-01.lilltek.com"));

            Assert.IsFalse(Helper.IsValidDomainName(null));
            Assert.IsFalse(Helper.IsValidDomainName(""));
            Assert.IsFalse(Helper.IsValidDomainName("   "));
            Assert.IsFalse(Helper.IsValidDomainName("."));
            Assert.IsFalse(Helper.IsValidDomainName(".test"));
            Assert.IsFalse(Helper.IsValidDomainName("test."));
            Assert.IsFalse(Helper.IsValidDomainName("123456789.123456789.123456789.123456789.123456789.123456789.123456789.123456789.123456789.123456789.123456789.123456789.123456789.123456789.123456789.123456789.123456789.123456789.123456789.123456789.123456789.123456789.123456789.123456789.123456789.123456"));
            Assert.IsFalse(Helper.IsValidDomainName("0123456789012345678901234567890123456789012345678901234567890123.test.com"));
            Assert.IsFalse(Helper.IsValidDomainName("test_01.com"));
            Assert.IsFalse(Helper.IsValidDomainName("jeff@lilltek.com"));
        }

        [TestMethod]
        [TestProperty("Lib", "LillTek.Common")]
        public void Helper_GetUriFileName()
        {
            Assert.IsNull(Helper.GetUriFileName(new Uri("http://test.com")));
            Assert.IsNull(Helper.GetUriFileName(new Uri("http://test.com/")));
            Assert.IsNull(Helper.GetUriFileName(new Uri("http://test.com/folder1/")));
            Assert.IsNull(Helper.GetUriFileName(new Uri("http://test.com/folder1/folder2/")));
            Assert.AreEqual("test.dat", Helper.GetUriFileName(new Uri("http://test.com/test.dat")));
            Assert.AreEqual("test.dat", Helper.GetUriFileName(new Uri("http://test.com/folder1/test.dat")));
            Assert.AreEqual("test.dat", Helper.GetUriFileName(new Uri("http://test.com/folder1/folder2/test.dat")));
            Assert.AreEqual("test.dat", Helper.GetUriFileName(new Uri("http://test.com/test.dat?query=1")));
        }

        [TestMethod]
        [TestProperty("Lib", "LillTek.Common")]
        public void Helper_FormatAsByteSize()
        {
            const long K = 1024L;
            const long M = 1024L * 1024L;
            const long G = 1024L * 1024L * 1024L;

            Assert.AreEqual("0", Helper.FormatAsByteSize(0));
            Assert.AreEqual("122", Helper.FormatAsByteSize(122));
            Assert.AreEqual("1023", Helper.FormatAsByteSize(1023));

            Assert.AreEqual("1KB", Helper.FormatAsByteSize(K));
            Assert.AreEqual("1.5KB", Helper.FormatAsByteSize(K + K / 2));
            Assert.AreEqual("5KB", Helper.FormatAsByteSize(5 * K));
            Assert.AreEqual("9.9KB", Helper.FormatAsByteSize((long)(9.9 * K)));

            Assert.AreEqual("10KB", Helper.FormatAsByteSize(10 * K));
            Assert.AreEqual("15KB", Helper.FormatAsByteSize(15 * K));
            Assert.AreEqual("750KB", Helper.FormatAsByteSize(750 * K));

            Assert.AreEqual("1MB", Helper.FormatAsByteSize(M));
            Assert.AreEqual("1.5MB", Helper.FormatAsByteSize(M + M / 2));
            Assert.AreEqual("5MB", Helper.FormatAsByteSize(5 * M));
            Assert.AreEqual("100MB", Helper.FormatAsByteSize(100 * M));
            Assert.AreEqual("100MB", Helper.FormatAsByteSize(100 * M + 5 * K));

            Assert.AreEqual("1GB", Helper.FormatAsByteSize(G));
            Assert.AreEqual("1.5GB", Helper.FormatAsByteSize(G + G / 2));
            Assert.AreEqual("15GB", Helper.FormatAsByteSize(15 * G));
        }

        [TestMethod]
        [TestProperty("Lib", "LillTek.Common")]
        public void Helper_NormalizeFileExtension()
        {
            Assert.IsNull(Helper.NormalizeFileExtension(null));
            Assert.AreEqual(".test", Helper.NormalizeFileExtension("test"));
            Assert.AreEqual(".test", Helper.NormalizeFileExtension(".test"));
        }

        [TestMethod]
        [TestProperty("Lib", "LillTek.Common")]
        public void Helper_DenormalizeFileExtension()
        {
            Assert.IsNull(Helper.DenormalizeFileExtension(null));
            Assert.AreEqual("test", Helper.DenormalizeFileExtension(".test"));
            Assert.AreEqual("test", Helper.DenormalizeFileExtension("test"));
        }

        [TestMethod]
        [TestProperty("Lib", "LillTek.Common")]
        public void Helper_GetUriFragment()
        {
            // Valid tests

            Assert.AreEqual("abcd", Helper.GetUriFragment("abcd"));
            Assert.AreEqual("abcd", Helper.GetUriFragment("  abcd  "));
            Assert.AreEqual("Jeff-Lill", Helper.GetUriFragment("Jeff Lill"));
            Assert.AreEqual("Jeff-Lill", Helper.GetUriFragment("Jeff Lill"));
            Assert.AreEqual("Jeff-Lill", Helper.GetUriFragment("\"Jeff Lill\""));
            Assert.AreEqual("Jeff-Lill", Helper.GetUriFragment("(Jeff)Lill"));
            Assert.AreEqual("Jeff-Lill", Helper.GetUriFragment("Jeff    Lill"));
            Assert.AreEqual("Jeff-Lill", Helper.GetUriFragment("Jeff.Lill"));
            Assert.AreEqual("Jeff-Lill", Helper.GetUriFragment("Jeff.-.Lill"));
            Assert.AreEqual("Jeff-Lill", Helper.GetUriFragment("Jeff/Lill"));
            Assert.AreEqual("This-is-a-test-of-the-5th-emergency-broadcasting-system", Helper.GetUriFragment("This is a test of the 5th emergency broadcasting system."));
            Assert.AreEqual("Youre-the-best", Helper.GetUriFragment("You're the best"));

            // Error tests

            try
            {
                Helper.GetUriFragment(null);
                Assert.Fail("Expected a ArgumentNullException");
            }
            catch (ArgumentNullException)
            {
                // Expected
            }

            try
            {
                Helper.GetUriFragment("");
                Assert.Fail("Expected a ArgumentException");
            }
            catch (ArgumentException)
            {
                // Expected
            }

            try
            {
                Helper.GetUriFragment("    ");
                Assert.Fail("Expected a ArgumentException");
            }
            catch (ArgumentException)
            {
                // Expected
            }
        }

        [TestMethod]
        [TestProperty("Lib", "LillTek.Common")]
        public void Helper_WaitFor()
        {
            bool done;

            done = true;
            Helper.WaitFor(() => done, TimeSpan.FromMilliseconds(1000));

            done = false;
            Helper.EnqueueAction(() =>
            {
                Thread.Sleep(500);
                done = true;
            });

            Helper.WaitFor(() => done, TimeSpan.FromMilliseconds(1000));

            try
            {
                Helper.WaitFor(() => false, TimeSpan.FromMilliseconds(1000));
                Assert.Fail("TimeoutException expected");
            }
            catch (TimeoutException)
            {
                // Expected
            }
        }

        [TestMethod]
        [TestProperty("Lib", "LillTek.Common")]
        public async Task Helper_WaitForAsync()
        {
            bool done;

            done = true;
            await Helper.WaitForAsync(
                async () => 
                {
                    await Task.Delay(0);
                    return done;
                },
                TimeSpan.FromMilliseconds(1000));

            done = false;
            Helper.EnqueueAction(() =>
            {
                Thread.Sleep(500);
                done = true;
            });

            await Helper.WaitForAsync(
                async () =>
                {
                    await Task.Delay(0);
                    return done;
                },
                TimeSpan.FromMilliseconds(1000));

            try
            {
                await Helper.WaitForAsync(
                    async () =>
                    {
                        await Task.Delay(0);
                        return false;
                    },
                    TimeSpan.FromMilliseconds(1000));

                Assert.Fail("TimeoutException expected");
            }
            catch (TimeoutException)
            {
                // Expected
            }
        }

        [TestMethod]
        [TestProperty("Lib", "LillTek.Common")]
        public void Helper_EnqueueAction()
        {
            int v1, v2, v3, v4;
            bool done;

            done = false;
            Helper.EnqueueAction(() => done = true);
            Helper.WaitFor(() => done, TimeSpan.FromMilliseconds(1000));

            v1 = 0;
            done = false;
            Helper.EnqueueAction<int>(1, (p1) =>
            {
                v1 = p1;
                done = true;
            });

            Helper.WaitFor(() => done, TimeSpan.FromMilliseconds(1000));
            Assert.AreEqual(1, v1);

            v1 = v2 = 0;
            done = false;
            Helper.EnqueueAction<int, int>(1, 2, (p1, p2) =>
            {
                v1 = p1;
                v2 = p2;
                done = true;
            });

            Helper.WaitFor(() => done, TimeSpan.FromMilliseconds(1000));
            Assert.AreEqual(1, v1);
            Assert.AreEqual(2, v2);

            v1 = v2 = v3 = 0;
            done = false;
            Helper.EnqueueAction<int, int, int>(1, 2, 3, (p1, p2, p3) =>
            {
                v1 = p1;
                v2 = p2;
                v3 = p3;
                done = true;
            });

            Helper.WaitFor(() => done, TimeSpan.FromMilliseconds(1000));
            Assert.AreEqual(1, v1);
            Assert.AreEqual(2, v2);
            Assert.AreEqual(3, v3);

            v1 = v2 = v3 = v4 = 0;
            done = false;
            Helper.EnqueueAction<int, int, int, int>(1, 2, 3, 4, (p1, p2, p3, p4) =>
            {
                v1 = p1;
                v2 = p2;
                v3 = p3;
                v4 = p4;
                done = true;
            });

            Helper.WaitFor(() => done, TimeSpan.FromMilliseconds(1000));
            Assert.AreEqual(1, v1);
            Assert.AreEqual(2, v2);
            Assert.AreEqual(3, v3);
            Assert.AreEqual(4, v4);
        }

        [TestMethod]
        [TestProperty("Lib", "LillTek.Common")]
        public void Helper_EnqueueSerializedAction()
        {
            // Make sure that actions passed to Helper.EnqueueSerializedAction() are executed in order.

            try
            {
                int count = 0;
                int action1Count = -1;
                int action2Count = -1;
                int action3Count = -1;
                int action4Count = -1;

                Helper.EnqueueSerializedAction(() =>
                {
                    Interlocked.Increment(ref count);
                    action1Count = count;
                });

                Helper.EnqueueSerializedAction(() =>
                {
                    Thread.Sleep(1000);
                    Interlocked.Increment(ref count);
                    action2Count = count;
                });

                Helper.EnqueueSerializedAction(() =>
                {
                    Interlocked.Increment(ref count);
                    Thread.Sleep(1000);
                    action3Count = count;
                    Thread.Sleep(1000);
                });

                Helper.EnqueueSerializedAction(() =>
                {
                    Interlocked.Increment(ref count);
                    action4Count = count;
                    Thread.Sleep(1000);
                });

                Helper.WaitFor(() => action1Count != -1 && action2Count != -1 && action3Count != -1 && action4Count != -1, TimeSpan.FromMilliseconds(5000));

                Assert.AreEqual(1, action1Count);
                Assert.AreEqual(2, action2Count);
                Assert.AreEqual(3, action3Count);
                Assert.AreEqual(4, action4Count);
            }
            finally
            {
                Helper.ClearPendingSerializedActions();
            }
        }

        [TestMethod]
        [TestProperty("Lib", "LillTek.Common")]
        public void Helper_QueueParamerizedSerializedWork()
        {
            try
            {
                int v1;
                int v2;
                int v3;
                int v4;

                // Test an action with 1 parameter.

                v1 = v2 = v3 = v4 = 0;
                Helper.EnqueueSerializedAction(
                    1,
                    (p1) =>
                    {

                        v1 = p1;
                    });

                Thread.Sleep(1000);
                Assert.AreEqual(1, v1);

                // Test an action with 2 parameters.

                v1 = v2 = v3 = v4 = 0;
                Helper.EnqueueSerializedAction(
                    1, 2,
                    (p1, p2) =>
                    {

                        v1 = p1;
                        v2 = p2;
                    });

                Thread.Sleep(1000);
                Assert.AreEqual(1, v1);
                Assert.AreEqual(2, v2);

                // Test an action with 3 parameters.

                v1 = v2 = v3 = v4 = 0;
                Helper.EnqueueSerializedAction(
                    1, 2, 3,
                    (p1, p2, p3) =>
                    {

                        v1 = p1;
                        v2 = p2;
                        v3 = p3;
                    });

                Thread.Sleep(1000);
                Assert.AreEqual(1, v1);
                Assert.AreEqual(2, v2);
                Assert.AreEqual(3, v3);

                // Test an action with 4 parameters.

                v1 = v2 = v3 = v4 = 0;
                Helper.EnqueueSerializedAction(
                    1, 2, 3, 4,
                    (p1, p2, p3, p4) =>
                    {

                        v1 = p1;
                        v2 = p2;
                        v3 = p3;
                        v4 = p4;
                    });

                Thread.Sleep(1000);
                Assert.AreEqual(1, v1);
                Assert.AreEqual(2, v2);
                Assert.AreEqual(3, v3);
                Assert.AreEqual(4, v4);
            }
            finally
            {
                Helper.ClearPendingSerializedActions();
            }
        }

        [TestMethod]
        [TestProperty("Lib", "LillTek.Common")]
        public void Helper_DownloadFile()
        {
            string tempPath = Path.GetTempFileName();
            long cb, cbFile;
            HttpWebResponse response;

            try
            {
                cb = Helper.WebDownload(new Uri("http://www.lilltek.com/Config/GeoTracker/IP2City.encrypted.dat"), tempPath, TimeSpan.FromMinutes(3), out response);

                Assert.IsTrue(cb > 0);

                using (var fs = new FileStream(tempPath, FileMode.Open, FileAccess.Read))
                {
                    cbFile = fs.Length;
                }

                Assert.AreEqual(HttpStatusCode.OK, response.StatusCode);
                Assert.AreEqual(cb, cbFile);
            }
            finally
            {
                Helper.DeleteFile(tempPath);
            }
        }

        [TestMethod]
        [TestProperty("Lib", "LillTek.Common")]
        public void Helper_DownloadStream()
        {
            HttpWebResponse response;
            long cb;

            using (var ms = new MemoryStream())
            {
                cb = Helper.WebDownload(new Uri("http://www.lilltek.com/Config/GeoTracker/IP2City.encrypted.dat"), ms, TimeSpan.FromMinutes(3), out response);

                Assert.AreEqual(HttpStatusCode.OK, response.StatusCode);
                Assert.IsTrue(cb > 0);
                Assert.AreEqual(cb, ms.Length);
            }
        }

        private class TestType
        {
            public TestType()
            {
            }

            public string Hello
            {
                get { return "World"; }
            }
        }

        [TestMethod]
        [TestProperty("Lib", "LillTek.Common")]
        public void Helper_CreateInstance()
        {
            TestType value;

            value = Helper.CreateInstance<TestType>(typeof(TestType));
            Assert.IsNotNull(value);
            Assert.AreEqual("World", value.Hello);
        }

        [TestMethod]
        [TestProperty("Lib", "LillTek.Common")]
        public void Helper_StartThread()
        {
            Thread thread;
            bool fail;

            fail = true;
            thread = Helper.StartThread("Test",
                () =>
                {
                    fail = Thread.CurrentThread.Name != "Test";
                });

            thread.Join();
            Assert.IsFalse(fail);

            fail = true;
            thread = Helper.StartThread(null,
                () =>
                {
                    fail = false;
                });

            thread.Join();
            Assert.IsFalse(fail);

            fail = true;
            thread = Helper.StartThread("Test", 10,
                param =>
                {
                    fail = Thread.CurrentThread.Name != "Test" || (int)param != 10;
                });

            thread.Join();
            Assert.IsFalse(fail);

            fail = true;
            thread = Helper.StartThread(null, 20,
                param =>
                {
                    fail = (int)param != 20;
                });

            thread.Join();
            Assert.IsFalse(fail);
        }

        [TestMethod]
        [TestProperty("Lib", "LillTek.Common")]
        public void Helper_ReadInt16()
        {
            var pos = 0;

            Assert.AreEqual(0x0011, Helper.ReadInt16(new byte[] { 0x00, 0x11 }, ref pos));
        }

        [TestMethod]
        [TestProperty("Lib", "LillTek.Common")]
        public void Helper_ReadInt32()
        {
            var pos = 0;

            Assert.AreEqual(0x00112233, Helper.ReadInt32(new byte[] { 0x00, 0x11, 0x22, 0x33 }, ref pos));
        }

        [TestMethod]
        [TestProperty("Lib", "LillTek.Common")]
        public void Helper_ReadInt64()
        {
            var pos = 0;

            Assert.AreEqual(0x0011223344556677, Helper.ReadInt64(new byte[] { 0x00, 0x11, 0x22, 0x33, 0x44, 0x55, 0x66, 0x77 }, ref pos));
        }

        [TestMethod]
        [TestProperty("Lib", "LillTek.Common")]
        public void Helper_ReadInt16Le()
        {
            var pos = 0;

            Assert.AreEqual(0x0011, Helper.ReadInt16Le(new byte[] { 0x11, 0x00 }, ref pos));
        }

        [TestMethod]
        [TestProperty("Lib", "LillTek.Common")]
        public void Helper_ReadInt32Le()
        {
            var pos = 0;

            Assert.AreEqual(0x33221100, Helper.ReadInt32Le(new byte[] { 0x00, 0x11, 0x22, 0x33 }, ref pos));
        }

        [TestMethod]
        [TestProperty("Lib", "LillTek.Common")]
        public void Helper_ReadInt64Le()
        {
            var pos = 0;

            Assert.AreEqual(0x7766554433221100, Helper.ReadInt64Le(new byte[] { 0x00, 0x11, 0x22, 0x33, 0x44, 0x55, 0x66, 0x77 }, ref pos));
        }

        [TestMethod]
        [TestProperty("Lib", "LillTek.Common")]
        public void Helper_TryParseColor()
        {
            Color c;

            Assert.IsFalse(Helper.TryParseColor("", out c));
            Assert.IsFalse(Helper.TryParseColor("#", out c));
            Assert.IsFalse(Helper.TryParseColor("xx", out c));
            Assert.IsFalse(Helper.TryParseColor("#QQ", out c));

            Assert.IsTrue(Helper.TryParseColor("white", out c));
            Assert.AreEqual(Color.White, c);

            Assert.IsTrue(Helper.TryParseColor("WHITE", out c));
            Assert.AreEqual(Color.White, c);

            Assert.IsTrue(Helper.TryParseColor("maroon", out c));
            Assert.AreEqual(Color.Maroon, c);

            Assert.IsTrue(Helper.TryParseColor("Transparent", out c));
            Assert.AreEqual(Color.Transparent, c);

            Assert.IsTrue(Helper.TryParseColor("Transparent", out c));
            Assert.AreEqual(Color.Transparent, c);

            Assert.IsTrue(Helper.TryParseColor("#B22222", out c));
            Assert.AreEqual(Color.Firebrick.ToArgb(), c.ToArgb());

            Assert.IsTrue(Helper.TryParseColor("#FFB22222", out c));
            Assert.AreEqual(Color.Firebrick.ToArgb(), c.ToArgb());

            Assert.IsTrue(Helper.TryParseColor("B22222", out c));
            Assert.AreEqual(Color.Firebrick.ToArgb(), c.ToArgb());

            Assert.IsTrue(Helper.TryParseColor("FFB22222", out c));
            Assert.AreEqual(Color.Firebrick.ToArgb(), c.ToArgb());

            Assert.IsTrue(Helper.TryParseColor("#00FFFFFF", out c));
            Assert.AreEqual(Color.Transparent.ToArgb(), c.ToArgb());

            Assert.IsTrue(Helper.TryParseColor("00FFFFFF", out c));
            Assert.AreEqual(Color.Transparent.ToArgb(), c.ToArgb());
        }
    }
}

