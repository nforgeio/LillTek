//-----------------------------------------------------------------------------
// FILE:        _ChannelVariableCollection.cs
// CONTRIBUTOR: Jeff Lill
// COPYRIGHT:   Copyright (c) 2005-2015 by Jeffrey Lill. All rights reserved.
// DESCRIPTION: Unit tests

using System;
using System.Configuration;
using System.Diagnostics;
using System.Net;
using System.Net.Sockets;
using System.Reflection;
using System.Security;
using System.Text;
using System.Threading;

using LillTek.Common;
using LillTek.Telephony.Common;
using LillTek.Testing;

using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace LillTek.Telephony.Common.NUnit
{
    [TestClass]
    public class _ChannelVariableCollection
    {
        [TestMethod]
        [TestProperty("Lib", "LillTek.Telephony")]
        public void ChannelVariableCollection_Basic()
        {
            var variables = new ChannelVariableCollection();
            string value;

            variables.Add("Hello", "World");
            Assert.AreEqual("World", variables["Hello"]);
            Assert.AreEqual("World", variables["HELLO"]);
            Assert.IsTrue(variables.TryGetValue("Hello", out value));
            Assert.AreEqual("World", value);
            Assert.IsTrue(variables.ContainsKey("Hello"));
            Assert.IsFalse(variables.ContainsKey("xxx"));

            variables["Hello"] = "test";
            Assert.AreEqual("test", variables["Hello"]);
        }

        [TestMethod]
        [TestProperty("Lib", "LillTek.Telephony")]
        public void ChannelVariableCollection_Exceptions()
        {
            var variables = new ChannelVariableCollection();

            ExtendedAssert.Throws<ArgumentNullException>(() => variables.Add(null, "value"));
            ExtendedAssert.Throws<ArgumentNullException>(() => variables.Add("name", null));
            ExtendedAssert.Throws<ArgumentException>(() => variables.Add("", "value"));
            ExtendedAssert.Throws<ArgumentException>(() => variables.Add("=", "value"));
            ExtendedAssert.Throws<ArgumentException>(() => variables.Add(",", "value"));
            ExtendedAssert.Throws<ArgumentException>(() => variables.Add("name", "^"));
        }
    }
}

