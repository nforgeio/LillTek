//-----------------------------------------------------------------------------
// FILE:        _SipHeader.cs
// CONTRIBUTOR: Jeff Lill
// COPYRIGHT:   Copyright (c) 2005-2015 by Jeffrey Lill.  All rights reserved.
// DESCRIPTION: Unit tests

using System;
using System.Collections.Generic;
using System.Text;

using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace LillTek.Telephony.Sip.Test
{
    [TestClass]
    public class _SipHeader
    {
        [TestMethod]
        [TestProperty("Lib", "LillTek.Telephony.Sip")]
        public void SipHeader_SingleValue()
        {
            SipHeader h;
            StringBuilder sb;

            h = new SipHeader("Test", "Hello");
            Assert.IsFalse(h.IsSpecial);
            Assert.AreEqual("Test", h.Name);
            Assert.AreEqual("Hello", h.Text);
            Assert.AreEqual("Hello", h.FullText);
            CollectionAssert.AreEqual(new string[] { "Hello" }, h.Values);

            h.Text = "World";
            Assert.IsFalse(h.IsSpecial);
            Assert.AreEqual("Test", h.Name);
            Assert.AreEqual("World", h.Text);
            Assert.AreEqual("World", h.FullText);
            CollectionAssert.AreEqual(new string[] { "World" }, h.Values);

            Assert.AreEqual("Test: World\r\n", h.ToString());

            sb = new StringBuilder();
            h.Serialize(sb, false);
            Assert.AreEqual("Test: World\r\n", sb.ToString());
        }

        [TestMethod]
        [TestProperty("Lib", "LillTek.Telephony.Sip")]
        public void SipHeader_MultiValue()
        {
            SipHeader h;
            StringBuilder sb;

            h = new SipHeader("Test", new string[] { "Hello", "World", "Now" });
            Assert.IsFalse(h.IsSpecial);
            Assert.AreEqual("Test", h.Name);
            Assert.AreEqual("Hello", h.Text);
            Assert.AreEqual("Hello, World, Now", h.FullText);
            CollectionAssert.AreEqual(new string[] { "Hello", "World", "Now" }, h.Values);

            Assert.AreEqual("Test: Hello, World, Now\r\n", h.ToString());

            sb = new StringBuilder();
            h.Serialize(sb, false);
            Assert.AreEqual("Test: Hello, World, Now\r\n", sb.ToString());

            h.Values[0] = "Goodbye";
            Assert.AreEqual("Goodbye", h.Text);
            Assert.AreEqual("Goodbye, World, Now", h.FullText);

            Assert.AreEqual("Test: Goodbye, World, Now\r\n", h.ToString());

            h.Append("Foo");
            Assert.AreEqual("Test: Goodbye, World, Now, Foo\r\n", h.ToString());

            h.Prepend("xxx");
            Assert.AreEqual("Test: xxx, Goodbye, World, Now, Foo\r\n", h.ToString());
        }

        [TestMethod]
        [TestProperty("Lib", "LillTek.Telephony.Sip")]
        public void SipHeader_Special()
        {
            SipHeader h;

            h = new SipHeader("Test", "v0=1,v1=2", true);
            Assert.IsTrue(h.IsSpecial);
            CollectionAssert.AreEqual(new string[] { "v0=1,v1=2" }, h.Values);
            Assert.AreEqual("v0=1,v1=2", h.Text);
            Assert.AreEqual("v0=1,v1=2", h.FullText);

            Assert.AreEqual("Test: v0=1,v1=2\r\n", h.ToString());
        }

        [TestMethod]
        [TestProperty("Lib", "LillTek.Telephony.Sip")]
        public void SipHeader_CompactNames()
        {
            SipHeader h;
            StringBuilder sb;

            h = new SipHeader(SipHeader.CallID, "test");

            sb = new StringBuilder();
            h.Serialize(sb, false);
            Assert.AreEqual("Call-ID: test\r\n", sb.ToString());

            sb = new StringBuilder();
            h.Serialize(sb, true);
            Assert.AreEqual("i: test\r\n", sb.ToString());
        }

        [TestMethod]
        [TestProperty("Lib", "LillTek.Telephony.Sip")]
        public void SipHeader_RemoveFirst()
        {
            SipHeader h;

            h = new SipHeader("Test", new string[] { "Hello", "World", "Now" });
            CollectionAssert.AreEqual(new string[] { "Hello", "World", "Now" }, h.Values);

            h.RemoveFirst();
            CollectionAssert.AreEqual(new string[] { "World", "Now" }, h.Values);
            Assert.AreEqual("Test: World, Now\r\n", h.ToString());

            h.RemoveFirst();
            h.RemoveFirst();
            Assert.AreEqual("", h.ToString());
        }

        [TestMethod]
        [TestProperty("Lib", "LillTek.Telephony.Sip")]
        public void SipHeader_RemoveLast()
        {
            SipHeader h;

            h = new SipHeader("Test", new string[] { "Hello", "World", "Now" });
            CollectionAssert.AreEqual(new string[] { "Hello", "World", "Now" }, h.Values);

            h.RemoveLast();
            CollectionAssert.AreEqual(new string[] { "Hello", "World" }, h.Values);
            Assert.AreEqual("Test: Hello, World\r\n", h.ToString());

            h.RemoveLast();
            h.RemoveLast();
            Assert.AreEqual("", h.ToString());
        }
    }
}

