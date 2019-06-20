//-----------------------------------------------------------------------------
// FILE:        _SwitchEventCodeSet.cs
// CONTRIBUTOR: Jeff Lill
// COPYRIGHT:   Copyright (c) 2005-2015 by Jeffrey Lill. All rights reserved.
// DESCRIPTION: Unit tests

using System;
using System.Collections.Generic;
using System.Configuration;
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
    public class _SwitchEventCodeSet
    {
        [TestMethod]
        [TestProperty("Lib", "LillTek.Telephony")]
        public void SwitchEventCodeSet_Basic()
        {
            var set = new SwitchEventCodeSet();
            var list = new List<SwitchEventCode>();

            Assert.IsFalse(set.IsReadOnly);

            Assert.IsTrue(set.IsEmpty);
            Assert.IsFalse(set.Contains(SwitchEventCode.Heartbeat));

            set.Add(SwitchEventCode.Heartbeat);
            Assert.IsFalse(set.IsEmpty);
            Assert.IsTrue(set.Contains(SwitchEventCode.Heartbeat));

            set.Clear();
            Assert.IsTrue(set.IsEmpty);
            Assert.IsFalse(set.Contains(SwitchEventCode.Heartbeat));

            list.Clear();
            foreach (var code in set)
                list.Add(code);

            Assert.AreEqual(0, list.Count);

            set.Add(SwitchEventCode.Heartbeat);
            list.Clear();
            foreach (var code in set)
                list.Add(code);

            Assert.AreEqual(1, list.Count);
            Assert.AreEqual(SwitchEventCode.Heartbeat, list[0]);

            set.Remove(SwitchEventCode.Heartbeat);
            Assert.IsTrue(set.IsEmpty);
        }

        [TestMethod]
        [TestProperty("Lib", "LillTek.Telephony")]
        public void SwitchEventCodeSet_Union()
        {
            var set1 = new SwitchEventCodeSet(SwitchEventCode.Heartbeat);
            var set2 = new SwitchEventCodeSet(SwitchEventCode.Heartbeat, SwitchEventCode.Dtmf);
            var result = set1.Union(set2);

            Assert.IsTrue(result.Contains(SwitchEventCode.Heartbeat));
            Assert.IsTrue(result.Contains(SwitchEventCode.Dtmf));
        }

        [TestMethod]
        [TestProperty("Lib", "LillTek.Telephony")]
        public void SwitchEventCodeSet_Intersect()
        {
            var set1 = new SwitchEventCodeSet(SwitchEventCode.Heartbeat, SwitchEventCode.Dtmf);
            var set2 = new SwitchEventCodeSet(SwitchEventCode.Dtmf);
            var result = set1.Intersect(set2);

            Assert.IsFalse(result.Contains(SwitchEventCode.Heartbeat));
            Assert.IsTrue(result.Contains(SwitchEventCode.Dtmf));
        }

        [TestMethod]
        [TestProperty("Lib", "LillTek.Telephony")]
        public void SwitchEventCodeSet_Difference()
        {
            var set1 = new SwitchEventCodeSet(SwitchEventCode.Heartbeat, SwitchEventCode.Dtmf);
            var set2 = new SwitchEventCodeSet(SwitchEventCode.Dtmf);
            var result = set1.Difference(set2);

            Assert.IsTrue(result.Contains(SwitchEventCode.Heartbeat));
            Assert.IsFalse(result.Contains(SwitchEventCode.Dtmf));
        }

        [TestMethod]
        [TestProperty("Lib", "LillTek.Telephony")]
        public void SwitchEventCodeSet_Not()
        {
            var set = new SwitchEventCodeSet(SwitchEventCode.Heartbeat, SwitchEventCode.Dtmf);
            var result = set.Not();

            foreach (SwitchEventCode code in Enum.GetValues(typeof(SwitchEventCode)))
            {
                if (code == SwitchEventCode.Heartbeat || code == SwitchEventCode.Dtmf)
                    Assert.IsFalse(result.Contains(code));
                else
                    Assert.IsTrue(result.Contains(code));
            }
        }

        [TestMethod]
        [TestProperty("Lib", "LillTek.Telephony")]
        public void SwitchEventCodeSet_ReadOnly()
        {
            var readOnly = new SwitchEventCodeSet(SwitchEventCode.Heartbeat);

            readOnly.IsReadOnly = true;
            Assert.IsTrue(readOnly.IsReadOnly);
            Assert.IsTrue(readOnly.Contains(SwitchEventCode.Heartbeat));

            ExtendedAssert.Throws<InvalidOperationException>(() => readOnly.IsReadOnly = false);
            ExtendedAssert.Throws<InvalidOperationException>(() => readOnly.Add(SwitchEventCode.Dtmf));
            ExtendedAssert.Throws<InvalidOperationException>(() => readOnly.Remove(SwitchEventCode.Dtmf));
            ExtendedAssert.Throws<InvalidOperationException>(() => readOnly.Clear());
        }

        [TestMethod]
        [TestProperty("Lib", "LillTek.Telephony")]
        public void SwitchEventCodeSet_Clone()
        {
            var set = new SwitchEventCodeSet(SwitchEventCode.Heartbeat, SwitchEventCode.Dtmf);
            var clone = set.Clone();

            Assert.IsTrue(clone.Contains(SwitchEventCode.Heartbeat));
            Assert.IsTrue(clone.Contains(SwitchEventCode.Dtmf));
        }

        [TestMethod]
        [TestProperty("Lib", "LillTek.Telephony")]
        public void SwitchEventCodeSet_Equality()
        {
            var set1 = new SwitchEventCodeSet();
            var set2 = new SwitchEventCodeSet();

            Assert.IsTrue(set1.Equals(set2));
            Assert.IsFalse(set1.Equals(null));
            Assert.IsFalse(set1.Equals("Hello World!"));
            Assert.AreEqual(set1.GetHashCode(), set2.GetHashCode());
            Assert.IsTrue(set1 == set2);
            Assert.IsFalse(set1 != set2);

            set1.Add(SwitchEventCode.Dtmf);
            set2.Add(SwitchEventCode.Dtmf);
            Assert.IsTrue(set1.Equals(set2));
            Assert.AreEqual(set1.GetHashCode(), set2.GetHashCode());
            Assert.IsTrue(set1 == set2);
            Assert.IsFalse(set1 != set2);

            set2.Add(SwitchEventCode.Heartbeat);
            Assert.IsFalse(set1.Equals(set2));
            Assert.AreNotEqual(set1.GetHashCode(), set2.GetHashCode());
            Assert.IsFalse(set1 == set2);
            Assert.IsTrue(set1 != set2);

            Assert.IsTrue((SwitchEventCodeSet)null == (SwitchEventCodeSet)null);
            Assert.IsFalse((SwitchEventCodeSet)null != (SwitchEventCodeSet)null);
            Assert.IsFalse(set1 == (SwitchEventCodeSet)null);
            Assert.IsFalse((SwitchEventCodeSet)null == set1);
            Assert.IsTrue(set1 != (SwitchEventCodeSet)null);
            Assert.IsTrue((SwitchEventCodeSet)null != set1);
        }
    }
}

