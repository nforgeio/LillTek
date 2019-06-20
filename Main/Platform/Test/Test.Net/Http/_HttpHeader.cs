//-----------------------------------------------------------------------------
// FILE:        _HttpHeader.cs
// CONTRIBUTOR: Jeff Lill
// COPYRIGHT:   Copyright (c) 2005-2015 by Jeffrey Lill.  All rights reserved.
// DESCRIPTION: Unit tests

using System;

using LillTek.Common;
using LillTek.Net.Http;
using LillTek.Testing;

using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace LillTek.Net.Http.Test
{
    [TestClass]
    public class _HttpHeader
    {
        [TestMethod]
        [TestProperty("Lib", "LillTek.Net.Http")]
        public void HttpHeader_String()
        {
            HttpHeader h;

            h = new HttpHeader("name", "value");
            Assert.AreEqual("name", h.Name);
            Assert.AreEqual("value", h.Value);

            h = new HttpHeader("name", " \tvalue\t ");
            Assert.AreEqual("value", h.Value);
        }

        [TestMethod]
        [TestProperty("Lib", "LillTek.Net.Http")]
        public void HttpHeader_Int()
        {
            HttpHeader h;
            int v;

            h = new HttpHeader("name", 10);
            Assert.AreEqual("name", h.Name);
            Assert.AreEqual("10", h.Value);
            Assert.AreEqual(10, h.AsInt);

            h = new HttpHeader("name", -100);
            Assert.AreEqual("-100", h.Value);
            Assert.AreEqual(-100, h.AsInt);

            h = new HttpHeader("name", "-100");
            Assert.AreEqual(-100, h.AsInt);

            h = new HttpHeader("name", " \t 100 \t  ");
            Assert.AreEqual(100, h.AsInt);

            h = new HttpHeader("name", "x x xs");
            try
            {
                v = h.AsInt;
                Assert.Fail();
            }
            catch (FormatException)
            {
            }
        }

        [TestMethod]
        [TestProperty("Lib", "LillTek.Net.Http")]
        public void HttpHeader_Date()
        {
            DateTime test = new DateTime(2005, 11, 5, 11, 54, 15);
            HttpHeader h;
            DateTime v;

            h = new HttpHeader("name", test);
            Assert.AreEqual("name", h.Name);
            Assert.AreEqual("Sat, 05 Nov 2005 11:54:15 GMT", h.Value);
            Assert.AreEqual(test, h.AsDate);

            h = new HttpHeader("name", "Sat, 05 Nov 2005 11:54:15 GMT");
            Assert.AreEqual(test, h.AsDate);

            h = new HttpHeader("name", "Sat,  05  Nov  2005  11:54:15  GMT");
            Assert.AreEqual(test, h.AsDate);

            h = new HttpHeader("name", "Saturday, 05-Nov-05 11:54:15 GMT");
            Assert.AreEqual(test, h.AsDate);

            h = new HttpHeader("name", "Saturday,  05-Nov-05  11:54:15  GMT");
            Assert.AreEqual(test, h.AsDate);

            h = new HttpHeader("name", "Sat Nov 5 11:54:15 2005");
            Assert.AreEqual(test, h.AsDate);

            h = new HttpHeader("name", "Sat  Nov  5  11:54:15  2005");
            Assert.AreEqual(test, h.AsDate);

            try
            {
                h = new HttpHeader("name", "Sat xs s");
                v = h.AsDate;
                Assert.Fail();
            }
            catch (FormatException)
            {
            }
        }

        [TestMethod]
        [TestProperty("Lib", "LillTek.Net.Http")]
        public void HttpHeader_List()
        {
            HttpHeader h;

            h = new HttpHeader("name", "Foo");
            h.Append("Bar");
            Assert.AreEqual("Foo, Bar", h.Value);

            h = new HttpHeader("name", " \t ");
            h.Append(" \t Foo \t ");
            h.Append(" \t Bar \t ");
            Assert.AreEqual("Foo, Bar", h.Value);
        }

        [TestMethod]
        [TestProperty("Lib", "LillTek.Net.Http")]
        public void HttpHeader_Continue()
        {
            HttpHeader h;

            h = new HttpHeader("name", "foo");
            h.AppendContinuation(" \t bar \t ");
            Assert.AreEqual("foo bar", h.Value);
        }
    }
}

