//-----------------------------------------------------------------------------
// FILE:        _HttpRequest.cs
// CONTRIBUTOR: Jeff Lill
// COPYRIGHT:   Copyright (c) 2005-2015 by Jeffrey Lill.  All rights reserved.
// DESCRIPTION: Unit tests

using System;
using System.Text;

using LillTek.Common;
using LillTek.Net.Http;
using LillTek.Testing;

using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace LillTek.Net.Http.Test
{
    [TestClass]
    public class _HttpRequest
    {
        [TestMethod]
        [TestProperty("Lib", "LillTek.Net.Http")]
        public void HttpRequest_Content_None()
        {
            HttpRequest r;
            byte[] buf1, buf2;

            r = new HttpRequest();
            buf1 = Encoding.ASCII.GetBytes("GET /foo.htm HTTP/1.1\r\nHeader1: Test1\r\nHeader2: Test2\r\n\r\n");
            r.BeginParse(80);
            Assert.IsTrue(r.Parse(buf1, buf1.Length));
            r.EndParse();

            Assert.AreEqual("Test1", r["Header1"]);
            Assert.AreEqual("Test2", r["Header2"]);
            Assert.AreEqual(0, r.Content.Size);
            Assert.AreEqual("http://localhost/foo.htm", r.Uri.ToString());
            Assert.AreEqual(80, r.Uri.Port);

            r = new HttpRequest();
            buf1 = Encoding.ASCII.GetBytes("GET /foo.htm HTTP/1.1\r\nHost: www.foo.com\r\n\r\n");
            r.BeginParse(90);
            Assert.IsTrue(r.Parse(buf1, buf1.Length));
            r.EndParse();

            Assert.AreEqual("http://www.foo.com:90/foo.htm", r.Uri.ToString());
            Assert.AreEqual(90, r.Uri.Port);

            r = new HttpRequest();
            buf1 = Encoding.ASCII.GetBytes("GET /foo.htm HTTP/1.1\r\nHeader1: Te");
            buf2 = Encoding.ASCII.GetBytes("st1\r\nHeader2: Test2\r\n\r\n");
            r.BeginParse(80);
            Assert.IsFalse(r.Parse(buf1, buf1.Length));
            Assert.IsTrue(r.Parse(buf2, buf2.Length));
            r.EndParse();

            Assert.AreEqual("Test1", r["Header1"]);
            Assert.AreEqual("Test2", r["Header2"]);
            Assert.AreEqual(0, r.Content.Size);
        }

        [TestMethod]
        [TestProperty("Lib", "LillTek.Net.Http")]
        public void HttpRequest_Content_NoneWithLength()
        {
            HttpRequest r;
            byte[] buf1, buf2;

            r = new HttpRequest();
            buf1 = Encoding.ASCII.GetBytes("GET /foo.htm HTTP/1.1\r\nHeader1: Test1\r\nHeader2: Test2\r\nContent-Length: 0\r\n\r\n");
            r.BeginParse(80);
            Assert.IsTrue(r.Parse(buf1, buf1.Length));
            r.EndParse();

            Assert.AreEqual("Test1", r["Header1"]);
            Assert.AreEqual("Test2", r["Header2"]);
            Assert.AreEqual(0, r.Content.Size);

            r = new HttpRequest();
            buf1 = Encoding.ASCII.GetBytes("GET /foo.htm HTTP/1.1\r\nHeader1: Te");
            buf2 = Encoding.ASCII.GetBytes("st1\r\nHeader2: Test2\r\nContent-Length: 0\r\n\r\n");
            r.BeginParse(80);
            Assert.IsFalse(r.Parse(buf1, buf1.Length));
            Assert.IsTrue(r.Parse(buf2, buf2.Length));
            r.EndParse();

            Assert.AreEqual("Test1", r["Header1"]);
            Assert.AreEqual("Test2", r["Header2"]);
            Assert.AreEqual(0, r.Content.Size);
        }

        [TestMethod]
        [TestProperty("Lib", "LillTek.Net.Http")]
        public void HttpRequest_Content_OneBlock()
        {
            HttpRequest r;
            byte[] buf1;

            r = new HttpRequest();
            buf1 = Encoding.ASCII.GetBytes("GET /foo.htm HTTP/1.1\r\nHeader1: Test1\r\nHeader2: Test2\r\nContent-Length: 4\r\n\r\nabcd");
            r.BeginParse(80);
            Assert.IsTrue(r.Parse(buf1, buf1.Length));
            r.EndParse();

            Assert.AreEqual("Test1", r["Header1"]);
            Assert.AreEqual("Test2", r["Header2"]);
            Assert.AreEqual(4, r.Content.Size);
            CollectionAssert.AreEqual(Encoding.ASCII.GetBytes("abcd"), r.Content.ToByteArray());
        }

        [TestMethod]
        [TestProperty("Lib", "LillTek.Net.Http")]
        public void HttpRequest_Content_MultipleBlocks()
        {
            HttpRequest r;
            byte[] buf1, buf2, buf3, buf4;

            r = new HttpRequest();
            buf1 = Encoding.ASCII.GetBytes("GET /foo.htm HTTP/1.1\r\nHeader1: Te");
            buf2 = Encoding.ASCII.GetBytes("st1\r\nHeader2: Test2\r\nContent-Length: 7\r\n\r\nab");
            buf3 = Encoding.ASCII.GetBytes("cd");
            buf4 = Encoding.ASCII.GetBytes("efg");
            r.BeginParse(80);
            Assert.IsFalse(r.Parse(buf1, buf1.Length));
            Assert.IsFalse(r.Parse(buf2, buf2.Length));
            Assert.IsFalse(r.Parse(buf3, buf3.Length));
            Assert.IsTrue(r.Parse(buf4, buf4.Length));
            r.EndParse();

            Assert.AreEqual("Test1", r["Header1"]);
            Assert.AreEqual("Test2", r["Header2"]);
            Assert.AreEqual(7, r.Content.Size);
            CollectionAssert.AreEqual(Encoding.ASCII.GetBytes("abcdefg"), r.Content.ToByteArray());
        }

        [TestMethod]
        [TestProperty("Lib", "LillTek.Net.Http")]
        public void HttpRequest_Content_ChunkedTransfer()
        {
            HttpRequest r;
            EnhancedMemoryStream ms;
            byte[] bufOut;
            byte[] buf;

            bufOut = new byte[300];
            for (int i = 0; i < bufOut.Length; i++)
                bufOut[i] = (byte)i;

            // Test parsing where the entire request is in a single input block.

            ms = new EnhancedMemoryStream();
            ms.WriteBytesNoLen(Encoding.ASCII.GetBytes("GET /foo.htm HTTP/1.1\r\nTransfer-Encoding: chunked\r\n\r\n"));

            ms.WriteBytesNoLen(Encoding.ASCII.GetBytes("12c; extra crap\r\n"));     // 12c = 300 as hex
            ms.WriteBytesNoLen(bufOut);
            ms.WriteBytesNoLen(Encoding.ASCII.GetBytes("\r\n"));

            ms.WriteBytesNoLen(Encoding.ASCII.GetBytes("0; extra crap\r\n\r\n"));

            buf = ms.ToArray();
            r = new HttpRequest();
            r.BeginParse(80);
            Assert.IsTrue(r.Parse(buf, buf.Length));
            r.EndParse();

            Assert.AreEqual(bufOut.Length, r.Content.Size);
            CollectionAssert.AreEqual(bufOut, r.Content.ToByteArray());

            // Test parsing of two chunks in a single input block.

            ms = new EnhancedMemoryStream();
            ms.WriteBytesNoLen(Encoding.ASCII.GetBytes("GET /foo.htm HTTP/1.1\r\nTransfer-Encoding: chunked\r\n\r\n"));

            ms.WriteBytesNoLen(Encoding.ASCII.GetBytes("FF\r\n"));     // FF = 255 as hex
            ms.Write(bufOut, 0, 255);
            ms.WriteBytesNoLen(Encoding.ASCII.GetBytes("\r\n"));

            ms.WriteBytesNoLen(Encoding.ASCII.GetBytes("2D\r\n"));     // 2D = 45 as hex (300 - 255)
            ms.Write(bufOut, 255, 45);
            ms.WriteBytesNoLen(Encoding.ASCII.GetBytes("\r\n"));

            ms.WriteBytesNoLen(Encoding.ASCII.GetBytes("0\r\n\r\n"));

            buf = ms.ToArray();
            r = new HttpRequest();
            r.BeginParse(80);
            Assert.IsTrue(r.Parse(buf, buf.Length));
            r.EndParse();

            Assert.AreEqual(bufOut.Length, r.Content.Size);
            CollectionAssert.AreEqual(bufOut, r.Content.ToByteArray());

            // Repeat the 2 chunk test but this time break the input into blocks of
            // only a single byte each.  This will torture test the parsing state machines.

            buf = ms.ToArray();
            r = new HttpRequest();
            r.BeginParse(80);

            for (int i = 0; i < buf.Length - 1; i++)
                Assert.IsFalse(r.Parse(new byte[] { buf[i] }, 1));

            Assert.IsTrue(r.Parse(new byte[] { buf[buf.Length - 1] }, 1));

            r.EndParse();

            Assert.AreEqual(bufOut.Length, r.Content.Size);
            CollectionAssert.AreEqual(bufOut, r.Content.ToByteArray());
        }

        [TestMethod]
        [TestProperty("Lib", "LillTek.Net.Http")]
        public void HttpRequest_Serialize()
        {
            HttpRequest r;
            byte[] buf;
            BlockArray blocks;

            //-------------------------

            r = new HttpRequest("get", "/foo.htm", null);
            CollectionAssert.AreEqual(Encoding.ASCII.GetBytes("GET /foo.htm HTTP/1.1\r\nContent-Length: 0\r\n\r\n"), r.Serialize(15).ToByteArray());

            //-------------------------

            r.Content = new BlockArray(Encoding.ASCII.GetBytes("abcdefg"));
            CollectionAssert.AreEqual(Encoding.ASCII.GetBytes("GET /foo.htm HTTP/1.1\r\nContent-Length: 7\r\n\r\nabcdefg"), r.Serialize(15).ToByteArray());

            //-------------------------

            r = new HttpRequest();
            buf = Encoding.ASCII.GetBytes("GET /foo.htm HTTP/1.1\r\nHeader1: Test1\r\nHeader2: Test2\r\nContent-Length: 4\r\n\r\nabcd");
            r.BeginParse(80);
            Assert.IsTrue(r.Parse(buf, buf.Length));
            r.EndParse();

            buf = r.Serialize(3).ToByteArray();
            blocks = new BlockArray(Encoding.ASCII.GetBytes("GET /foo.htm HTTP/1.1\r\nHeader1: Test1\r\nHeader2: Test2\r\nContent-Length: 4\r\n\r\nabcd"));

            r = new HttpRequest();
            r.BeginParse(80);

            for (int i = 0; i < blocks.Count; i++)
            {
                Block block = blocks.GetBlock(i);

                Assert.AreEqual(0, block.Offset);
                r.Parse(block.Buffer, block.Length);
            }

            r.EndParse();

            Assert.AreEqual("GET", r.Method);
            Assert.AreEqual("/foo.htm", r.RawUri);
            Assert.AreEqual("4", r["Content-Length"]);
            Assert.AreEqual("Test1", r["Header1"]);
            Assert.AreEqual("Test2", r["Header2"]);
            CollectionAssert.AreEqual(Encoding.ASCII.GetBytes("abcd"), r.Content.ToByteArray());
        }
    }
}

