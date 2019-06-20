//-----------------------------------------------------------------------------
// FILE:        _ReadOnlyDictionary.cs
// CONTRIBUTOR: Jeff Lill
// COPYRIGHT:   Copyright (c) 2005-2015 by Jeffrey Lill. All rights reserved.
// DESCRIPTION: UNIT tests.

using System;
using System.Collections.Generic;
using System.Configuration;
using System.Linq;

using Microsoft.VisualStudio.TestTools.UnitTesting;

using LillTek.Common;
using LillTek.Testing;

namespace LillTek.Common.Test
{
    [TestClass]
    public class _ReadOnlyDictionary
    {
        [TestMethod]
        [TestProperty("Lib", "LillTek.Common")]
        public void ReadOnlyDictionary_Basic()
        {
            Dictionary<string, string> dictionary = new Dictionary<string, string>();
            IDictionary<string, string> readOnly;
            string value;

            readOnly = new ReadOnlyDictionary<string, string>(dictionary);
            Assert.IsTrue(readOnly.IsReadOnly);
            Assert.AreEqual(0, readOnly.Count);
            Assert.IsFalse(readOnly.ContainsKey("test"));
            Assert.IsFalse(readOnly.TryGetValue("test", out value));

            dictionary.Add("foo", "bar");
            dictionary.Add("hello", "world");

            readOnly = new ReadOnlyDictionary<string, string>(dictionary);
            Assert.AreEqual(2, readOnly.Count);
            Assert.IsTrue(readOnly.ContainsKey("hello"));
            Assert.IsTrue(readOnly.TryGetValue("hello", out value));
            Assert.AreEqual("world", value);
            Assert.AreEqual("world", readOnly["hello"]);

            readOnly = dictionary.ToReadOnly();
            Assert.AreEqual(2, readOnly.Count);
            Assert.IsTrue(readOnly.ContainsKey("hello"));
            Assert.IsTrue(readOnly.TryGetValue("hello", out value));
            Assert.AreEqual("world", value);
            Assert.AreEqual("world", readOnly["hello"]);
        }

        [TestMethod]
        [TestProperty("Lib", "LillTek.Common")]
        public void ReadOnlyDictionary_TryModify()
        {
            var dictionary = new Dictionary<string, string>();
            var readOnly = new ReadOnlyDictionary<string, string>(dictionary);

            ExtendedAssert.Throws<NotSupportedException>(() => readOnly.Clear());
            ExtendedAssert.Throws<NotSupportedException>(() => readOnly.Add("hello", "world"));
            ExtendedAssert.Throws<NotSupportedException>(() => readOnly["test"] = "fail");
            ExtendedAssert.Throws<NotSupportedException>(() => readOnly.Remove("hello"));
            ExtendedAssert.Throws<NotSupportedException>(() => readOnly.Remove(new KeyValuePair<string, string>("hello", "world")));
        }
    }
}

