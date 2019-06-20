//-----------------------------------------------------------------------------
// FILE:        _HttpHeaderCollection.cs
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
    public class _HttpHeaderCollection
    {
        [TestMethod]
        [TestProperty("Lib", "LillTek.Net.Http")]
        public void HttpHeaderCollection_BasicRequest()
        {
            HttpHeaderCollection headers;

            headers = new HttpHeaderCollection("GET", "http://foobar.com");
            Assert.IsTrue(headers.IsRequest);
            Assert.IsFalse(headers.IsResponse);
            Assert.AreEqual("GET", headers.Method);
            Assert.AreEqual("http://foobar.com", headers.RawUri);
            Assert.AreEqual(HttpStack.Http11, headers.HttpVersion);

            headers = new HttpHeaderCollection("put", "http://foobar.com");
            Assert.AreEqual("PUT", headers.Method);
        }

        [TestMethod]
        [TestProperty("Lib", "LillTek.Net.Http")]
        public void HttpHeaderCollection_BasicResponse()
        {
            HttpHeaderCollection headers;

            headers = new HttpHeaderCollection(HttpStatus.OK, "OK");
            Assert.IsTrue(headers.IsResponse);
            Assert.IsFalse(headers.IsRequest);
            Assert.AreEqual(HttpStatus.OK, headers.Status);
            Assert.AreEqual("OK", headers.Reason);
            Assert.AreEqual(HttpStack.Http11, headers.HttpVersion);
        }

        [TestMethod]
        [TestProperty("Lib", "LillTek.Net.Http")]
        public void HttpHeaderCollection_BasicHeaders()
        {
            HttpHeaderCollection headers;

            headers = new HttpHeaderCollection(HttpStatus.OK, "OK");
            headers.Add("test1", "value1");
            Assert.AreEqual("value1", headers["test1"]);
            Assert.IsNull(headers["test2"]);
            headers.Add("test2", "value2");
            Assert.AreEqual("value2", headers["test2"]);
            headers.Add("test1", "new");
            Assert.AreEqual("value1, new", headers["test1"]);
            headers["test1"] = "value1";
            Assert.AreEqual("value1", headers["test1"]);
        }

        [TestMethod]
        [TestProperty("Lib", "LillTek.Net.Http")]
        public void HttpHeaderCollection_ParseRequest()
        {
            HttpHeaderCollection headers;
            byte[] buf;
            BlockArray blocks;
            int dataPos;

            headers = new HttpHeaderCollection(true);
            buf = Encoding.ASCII.GetBytes("GET /Foo/bar.htm HTTP/1.1\r\n\r\n");

            headers.BeginParse();
            Assert.IsTrue(headers.Parse(buf, buf.Length));
            blocks = headers.EndParse(out dataPos);
            CollectionAssert.AreEqual(buf, blocks.ToByteArray());
            Assert.AreEqual(buf.Length, dataPos);
            Assert.AreEqual("GET", headers.Method);
            Assert.AreEqual(HttpStack.Http11, headers.HttpVersion);
            Assert.AreEqual("/Foo/bar.htm", headers.RawUri);

            headers = new HttpHeaderCollection(true);
            buf = Encoding.ASCII.GetBytes("get /Foo/bar.htm HTTP/1.1\r\nHeader1: Test1\r\nHeader2:\tTest2   \r\nHeader3: Folded \r\n Here \r\n\r\n");

            headers.BeginParse();
            Assert.IsTrue(headers.Parse(buf, buf.Length));
            blocks = headers.EndParse(out dataPos);
            CollectionAssert.AreEqual(buf, blocks.ToByteArray());
            Assert.AreEqual(buf.Length, dataPos);
            Assert.AreEqual("GET", headers.Method);
            Assert.AreEqual(HttpStack.Http11, headers.HttpVersion);
            Assert.AreEqual("/Foo/bar.htm", headers.RawUri);
            Assert.AreEqual("Test1", headers["HEADER1"]);
            Assert.AreEqual("Test2", headers["HEADER2"]);
            Assert.AreEqual("Folded Here", headers["Header3"]);

            headers = new HttpHeaderCollection(true);
            buf = Encoding.ASCII.GetBytes("get /Foo/bar.htm HTTP/1.0\r\nHeader1: Test1\r\nHeader1:\tTest2   \r\nHeader3: Test3\r\n\r\nabcd");

            headers.BeginParse();
            Assert.IsTrue(headers.Parse(buf, buf.Length));
            blocks = headers.EndParse(out dataPos);
            CollectionAssert.AreEqual(buf, blocks.ToByteArray());
            Assert.AreEqual(buf.Length - 4, dataPos);
            Assert.AreEqual("GET", headers.Method);
            Assert.AreEqual(HttpStack.Http10, headers.HttpVersion);
            Assert.AreEqual("/Foo/bar.htm", headers.RawUri);
            Assert.AreEqual("Test1, Test2", headers["Header1"]);
            Assert.AreEqual("Test3", headers["Header3"]);
        }

        [TestMethod]
        [TestProperty("Lib", "LillTek.Net.Http")]
        public void HttpHeaderCollection_ParseResponse()
        {
            HttpHeaderCollection headers;
            byte[] buf;
            BlockArray blocks;
            int dataPos;

            headers = new HttpHeaderCollection(false);
            buf = Encoding.ASCII.GetBytes("HTTP/1.1 200 OK\r\n\r\n");

            headers.BeginParse();
            Assert.IsTrue(headers.Parse(buf, buf.Length));
            blocks = headers.EndParse(out dataPos);
            CollectionAssert.AreEqual(buf, blocks.ToByteArray());
            Assert.AreEqual(buf.Length, dataPos);
            Assert.AreEqual(HttpStatus.OK, headers.Status);
            Assert.AreEqual(HttpStack.Http11, headers.HttpVersion);
            Assert.AreEqual("OK", headers.Reason);

            headers = new HttpHeaderCollection(false);
            buf = Encoding.ASCII.GetBytes("HTTP/1.1 200 OK\r\nHeader1: Test1\r\nHeader2:\tTest2   \r\nHeader3: Folded \r\n Here \r\n\r\n");

            headers.BeginParse();
            Assert.IsTrue(headers.Parse(buf, buf.Length));
            blocks = headers.EndParse(out dataPos);
            CollectionAssert.AreEqual(buf, blocks.ToByteArray());
            Assert.AreEqual(buf.Length, dataPos);
            Assert.AreEqual(HttpStatus.OK, headers.Status);
            Assert.AreEqual(HttpStack.Http11, headers.HttpVersion);
            Assert.AreEqual("OK", headers.Reason);
            Assert.AreEqual("Test1", headers["Header1"]);
            Assert.AreEqual("Test2", headers["Header2"]);
            Assert.AreEqual("Folded Here", headers["Header3"]);

            headers = new HttpHeaderCollection(false);
            buf = Encoding.ASCII.GetBytes("HTTP/1.0 200 OK\r\nHeader1: Test1\r\nHeader1:\tTest2   \r\nHeader3: Test3\r\n\r\nabcd");

            headers.BeginParse();
            Assert.IsTrue(headers.Parse(buf, buf.Length));
            blocks = headers.EndParse(out dataPos);
            CollectionAssert.AreEqual(buf, blocks.ToByteArray());
            Assert.AreEqual(buf.Length - 4, dataPos);
            Assert.AreEqual(HttpStatus.OK, headers.Status);
            Assert.AreEqual(HttpStack.Http10, headers.HttpVersion);
            Assert.AreEqual("OK", headers.Reason);
            Assert.AreEqual("Test1, Test2", headers["Header1"]);
            Assert.AreEqual("Test3", headers["Header3"]);
        }

        [TestMethod]
        [TestProperty("Lib", "LillTek.Net.Http")]
        public void HttpHeaderCollection_CRLF_Boundary()
        {
            HttpHeaderCollection headers;
            byte[] buf1, buf2;
            BlockArray blocks;
            int dataPos;

            headers = new HttpHeaderCollection(false);
            buf1 = Encoding.ASCII.GetBytes("HTTP/1.1 200 OK");
            buf2 = Encoding.ASCII.GetBytes("\r\n\r\nabcd");

            headers.BeginParse();
            Assert.IsFalse(headers.Parse(buf1, buf1.Length));
            Assert.IsTrue(headers.Parse(buf2, buf2.Length));
            blocks = headers.EndParse(out dataPos);
            Assert.AreEqual(buf1.Length + buf2.Length - 4, dataPos);
            Assert.AreEqual(HttpStatus.OK, headers.Status);
            Assert.AreEqual(HttpStack.Http11, headers.HttpVersion);
            Assert.AreEqual("OK", headers.Reason);

            headers = new HttpHeaderCollection(false);
            buf1 = Encoding.ASCII.GetBytes("HTTP/1.1 200 OK\r");
            buf2 = Encoding.ASCII.GetBytes("\n\r\nabcd");

            headers.BeginParse();
            Assert.IsFalse(headers.Parse(buf1, buf1.Length));
            Assert.IsTrue(headers.Parse(buf2, buf2.Length));
            blocks = headers.EndParse(out dataPos);
            Assert.AreEqual(buf1.Length + buf2.Length - 4, dataPos);
            Assert.AreEqual(HttpStatus.OK, headers.Status);
            Assert.AreEqual(HttpStack.Http11, headers.HttpVersion);
            Assert.AreEqual("OK", headers.Reason);

            headers = new HttpHeaderCollection(false);
            buf1 = Encoding.ASCII.GetBytes("HTTP/1.1 200 OK\r\n");
            buf2 = Encoding.ASCII.GetBytes("\r\nabcd");

            headers.BeginParse();
            Assert.IsFalse(headers.Parse(buf1, buf1.Length));
            Assert.IsTrue(headers.Parse(buf2, buf2.Length));
            blocks = headers.EndParse(out dataPos);
            Assert.AreEqual(buf1.Length + buf2.Length - 4, dataPos);
            Assert.AreEqual(HttpStatus.OK, headers.Status);
            Assert.AreEqual(HttpStack.Http11, headers.HttpVersion);
            Assert.AreEqual("OK", headers.Reason);

            headers = new HttpHeaderCollection(false);
            buf1 = Encoding.ASCII.GetBytes("HTTP/1.1 200 OK\r\n\r");
            buf2 = Encoding.ASCII.GetBytes("\nabcd");

            headers.BeginParse();
            Assert.IsFalse(headers.Parse(buf1, buf1.Length));
            Assert.IsTrue(headers.Parse(buf2, buf2.Length));
            blocks = headers.EndParse(out dataPos);
            Assert.AreEqual(buf1.Length + buf2.Length - 4, dataPos);
            Assert.AreEqual(HttpStatus.OK, headers.Status);
            Assert.AreEqual(HttpStack.Http11, headers.HttpVersion);
            Assert.AreEqual("OK", headers.Reason);


            headers = new HttpHeaderCollection(false);
            buf1 = Encoding.ASCII.GetBytes("HTTP/1.1 200 OK\r\nHeader1: Test1\r\nHeader2 : Test2\r\n\r");
            buf2 = Encoding.ASCII.GetBytes("\nabcd");

            headers.BeginParse();
            Assert.IsFalse(headers.Parse(buf1, buf1.Length));
            Assert.IsTrue(headers.Parse(buf2, buf2.Length));
            blocks = headers.EndParse(out dataPos);
            Assert.AreEqual(buf1.Length + buf2.Length - 4, dataPos);
            Assert.AreEqual(HttpStatus.OK, headers.Status);
            Assert.AreEqual(HttpStack.Http11, headers.HttpVersion);
            Assert.AreEqual("OK", headers.Reason);
            Assert.AreEqual("Test1", headers["HEADER1"]);
            Assert.AreEqual("Test2", headers["HEADER2"]);
        }

        [TestMethod]
        [TestProperty("Lib", "LillTek.Net.Http")]
        public void HttpHeaderCollection_Get()
        {
            HttpHeaderCollection headers;
            DateTime date1 = new DateTime(2005, 11, 5, 11, 54, 15);
            DateTime date2 = new DateTime(2005, 11, 6, 11, 54, 15);

            headers = new HttpHeaderCollection("GET", "/foo.htm");
            headers.Add("String", "Hello World!");
            headers.Add("Int", "10");
            headers.Add("Date", "Sat, 05 Nov 2005 11:54:15 GMT");

            Assert.AreEqual("Hello World!", headers.Get("string", null));
            Assert.AreEqual("Hello World!", headers.Get("STRING", null));
            Assert.AreEqual("foobar", headers.Get("Foobar", "foobar"));
            Assert.IsNull(headers.Get("Foobar", null));

            Assert.AreEqual(10, headers.Get("int", 0));
            Assert.AreEqual(10, headers.Get("INT", 0));
            Assert.AreEqual(77, headers.Get("Foo", 77));
            Assert.AreEqual(88, headers.Get("String", 88));

            Assert.AreEqual(date1, headers.Get("date", DateTime.MinValue));
            Assert.AreEqual(date1, headers.Get("DATE", DateTime.MinValue));
            Assert.AreEqual(date2, headers.Get("foo", date2));
            Assert.AreEqual(date2, headers.Get("string", date2));
        }
    }
}

