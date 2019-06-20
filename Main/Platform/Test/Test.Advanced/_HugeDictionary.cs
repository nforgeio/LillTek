//-----------------------------------------------------------------------------
// FILE:        _HugeDictionary.cs
// CONTRIBUTOR: Jeff Lill
// COPYRIGHT:   Copyright (c) 2005-2015 by Jeffrey Lill.  All rights reserved.
// DESCRIPTION: Unit tests

using System;
using System.Collections;
using System.Collections.Generic;

using Microsoft.VisualStudio.TestTools.UnitTesting;

using LillTek.Common;
using LillTek.Advanced;

namespace LillTek.Advanced.Test
{
    [TestClass]
    public class _HugeDictionary
    {
        [TestMethod]
        [TestProperty("Lib", "LillTek.Advanced")]
        public void HugeDictionary_Basic()
        {
            HugeDictionary<string, int> dictionary;
            int value;

            dictionary = new HugeDictionary<string, int>(10);
            Assert.AreEqual(0, dictionary.Count);
            Assert.IsFalse(dictionary.ContainsKey("0"));
            Assert.IsFalse(dictionary.ContainsKey("1"));

            Assert.IsFalse(dictionary.TryGetValue("0", out value));
            Assert.AreEqual(0, value);
            Assert.IsFalse(dictionary.TryGetValue("1", out value));
            Assert.AreEqual(0, value);

            dictionary.Add("0", 0);
            Assert.AreEqual(1, dictionary.Count);
            Assert.AreEqual(0, dictionary["0"]);
            Assert.IsTrue(dictionary.ContainsKey("0"));
            Assert.IsFalse(dictionary.ContainsKey("1"));

            Assert.IsTrue(dictionary.TryGetValue("0", out value));
            Assert.AreEqual(0, value);
            Assert.IsFalse(dictionary.TryGetValue("1", out value));
            Assert.AreEqual(0, value);

            dictionary.Add("1", 1);
            Assert.AreEqual(2, dictionary.Count);
            Assert.AreEqual(0, dictionary["0"]);
            Assert.AreEqual(1, dictionary["1"]);
            Assert.IsTrue(dictionary.ContainsKey("0"));
            Assert.IsTrue(dictionary.ContainsKey("1"));

            Assert.IsTrue(dictionary.TryGetValue("0", out value));
            Assert.AreEqual(0, value);
            Assert.IsTrue(dictionary.TryGetValue("1", out value));
            Assert.AreEqual(1, value);

            dictionary.Remove("0");
            Assert.AreEqual(1, dictionary.Count);
            Assert.AreEqual(1, dictionary["1"]);
            Assert.IsFalse(dictionary.ContainsKey("0"));
            Assert.IsTrue(dictionary.ContainsKey("1"));

            Assert.IsFalse(dictionary.TryGetValue("0", out value));
            Assert.AreEqual(0, value);
            Assert.IsTrue(dictionary.TryGetValue("1", out value));
            Assert.AreEqual(1, value);

            dictionary.Clear();
            Assert.AreEqual(0, dictionary.Count);
            Assert.IsFalse(dictionary.ContainsKey("0"));
            Assert.IsFalse(dictionary.ContainsKey("1"));
        }

        [TestMethod]
        [TestProperty("Lib", "LillTek.Advanced")]
        public void HugeDictionary_KeyComparer()
        {
            HugeDictionary<string, int> dictionary;

            dictionary = new HugeDictionary<string, int>(10, StringComparer.OrdinalIgnoreCase);
            Assert.AreEqual(0, dictionary.Count);
            Assert.IsFalse(dictionary.ContainsKey("one"));
            Assert.IsFalse(dictionary.ContainsKey("two"));

            dictionary.Add("one", 0);
            Assert.AreEqual(1, dictionary.Count);
            Assert.AreEqual(0, dictionary["one"]);
            Assert.AreEqual(0, dictionary["ONE"]);
            Assert.IsTrue(dictionary.ContainsKey("one"));
            Assert.IsTrue(dictionary.ContainsKey("One"));
        }

        [TestMethod]
        [TestProperty("Lib", "LillTek.Advanced")]
        public void HugeDictionary_Capacity()
        {
            HugeDictionary<string, int> dictionary;
            int value;

            dictionary = new HugeDictionary<string, int>(10, 5000);
            Assert.AreEqual(0, dictionary.Count);
            Assert.IsFalse(dictionary.ContainsKey("0"));
            Assert.IsFalse(dictionary.ContainsKey("1"));

            Assert.IsFalse(dictionary.TryGetValue("0", out value));
            Assert.AreEqual(0, value);
            Assert.IsFalse(dictionary.TryGetValue("1", out value));
            Assert.AreEqual(0, value);
        }

        [TestMethod]
        [TestProperty("Lib", "LillTek.Advanced")]
        public void HugeDictionary_EnumerateEntries()
        {
            HugeDictionary<string, int> dictionary;
            Dictionary<string, bool> enumerated;

            dictionary = new HugeDictionary<string, int>(10, 5000);
            enumerated = new Dictionary<string, bool>();

            for (int i = 0; i < 1000; i++)
                dictionary.Add(i.ToString(), i);

            foreach (var entry in dictionary)
            {
                Assert.AreEqual(entry.Key, entry.Value.ToString());
                enumerated[entry.Key] = true;
            }

            for (int i = 0; i < 1000; i++)
                Assert.IsTrue(enumerated.ContainsKey(i.ToString()));
        }

        [TestMethod]
        [TestProperty("Lib", "LillTek.Advanced")]
        public void HugeDictionary_LargeScale()
        {
            const int count = 10000000;

            HugeDictionary<int, int> dictionary;

            dictionary = new HugeDictionary<int, int>(100);

            for (int i = 0; i < count; i++)
                dictionary.Add(i, -i);

            Assert.AreEqual(count, dictionary.Count);
            for (int i = 0; i < count; i++)
                Assert.AreEqual(-i, dictionary[i]);

            for (int i = 0; i < count; i++)
                dictionary.Remove(i);

            Assert.AreEqual(0, dictionary.Count);
        }
    }
}

