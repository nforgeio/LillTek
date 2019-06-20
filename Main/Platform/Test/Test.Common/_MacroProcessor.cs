//-----------------------------------------------------------------------------
// FILE:        _MacroProcessor.cs
// CONTRIBUTOR: Jeff Lill
// COPYRIGHT:   Copyright (c) 2005-2015 by Jeffrey Lill. All rights reserved.
// DESCRIPTION: UNIT tests for the MacroProcessor class.

using System;
using System.Reflection;

using Microsoft.VisualStudio.TestTools.UnitTesting;

using LillTek.Common;
using LillTek.Testing;

namespace LillTek.Common.Test
{
    [TestClass]
    public class _MacroProcessor
    {
        [TestMethod]
        [TestProperty("Lib", "LillTek.Common")]
        public void MacroProcessor_Basic1()
        {
            var macros = new MacroProcessor();

            macros.Add("var1", "INSERT");

            Assert.AreEqual("", macros.Expand(""));
            Assert.AreEqual("INSERT", macros.Expand("INSERT"));
            Assert.AreEqual("INSERT", macros.Expand("$(var1)"));
            Assert.AreEqual("prefix INSERT suffix", macros.Expand("prefix $(VAR1) suffix"));
            Assert.AreEqual("$(none)", macros.Expand("$(none)"));
            Assert.AreEqual("prefix $(none) suffix", macros.Expand("prefix $(none) suffix"));
            Assert.AreEqual("$", macros.Expand("$"));
            Assert.AreEqual("$(", macros.Expand("$("));
            Assert.AreEqual("$hello", macros.Expand("$hello"));
            Assert.AreEqual("$(hello", macros.Expand("$(hello"));
            Assert.AreEqual("$(hello)", macros.Expand("$(hello)"));
            Assert.AreEqual("hello)", macros.Expand("hello)"));
            Assert.AreEqual("hello)world", macros.Expand("hello)world"));
        }

        [TestMethod]
        [TestProperty("Lib", "LillTek.Common")]
        public void MacroProcessor_Basic2()
        {
            var macros = new MacroProcessor();

            macros.Add("var1", "HELLO");
            macros.Add("var2", "WORLD");

            Assert.AreEqual("HELLO", macros["var1"]);
            Assert.AreEqual("HELLO", macros["VAR1"]);
            Assert.IsNull(macros["VAR3"]);

            Assert.AreEqual("HELLO", macros.Expand("$(var1)"));
            Assert.AreEqual("WORLD", macros.Expand("$(var2)"));
            Assert.AreEqual("HELLO WORLD", macros.Expand("$(var1) $(var2)"));
            Assert.AreEqual("prefix HELLO suffix", macros.Expand("prefix $(var1) suffix"));
            Assert.AreEqual("HELLO WORLD", macros.Expand("$(VAR1) $(VAR2)"));

            macros.Clear();
            Assert.AreEqual("$(var1)", macros.Expand("$(var1)"));

            macros["VAR3"] = "TEST";
            Assert.AreEqual("TEST", macros["var3"]);
            Assert.AreEqual("TEST", macros["VAR3"]);
            Assert.AreEqual("TEST", macros.Expand("$(var3)"));
            Assert.AreEqual("TEST", macros.Expand("$(VAR3)"));
        }

        [TestMethod]
        [TestProperty("Lib", "LillTek.Common")]
        public void MacroProcessor_Recursive()
        {
            var macros = new MacroProcessor();

            macros.Add("var1", "$(var2)");
            macros.Add("var2", "$(var3) $(var4)");
            macros.Add("var3", "HELLO");
            macros.Add("VAR4", "WORLD");

            Assert.AreEqual("HELLO WORLD", macros.Expand("$(var1)"));
        }

        [TestMethod]
        [TestProperty("Lib", "LillTek.Common")]
        public void MacroProcessor_InfiniteRecursion()
        {
            var macros = new MacroProcessor();

            macros.Add("var1", "$(var2)");
            macros.Add("var2", "$(var1)");

            try
            {
                macros.Expand("$(var1)");
                Assert.Fail();  // Expected a StackOverflowException
            }
            catch (StackOverflowException)
            {
            }
        }
    }
}

