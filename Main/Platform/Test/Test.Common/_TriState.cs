//-----------------------------------------------------------------------------
// FILE:        _TriState.cs
// CONTRIBUTOR: Jeff Lill
// COPYRIGHT:   Copyright (c) 2005-2015 by Jeffrey Lill.  All rights reserved.
// DESCRIPTION: UNIT tests 

using System;

using Microsoft.VisualStudio.TestTools.UnitTesting;

using LillTek.Common;
using LillTek.Testing;

namespace LillTek.Common.Test
{
    [TestClass]
    public class _TriState
    {
        [TestMethod]
        [TestProperty("Lib", "LillTek.Common")]
        public void TriState_Constructor()
        {
            TriState v;

            v = TriState.Unknown;
            Assert.IsTrue(v.IsUnknown);
            Assert.IsFalse(v.IsTrue);
            Assert.IsFalse(v.IsFalse);

            v = TriState.True;
            Assert.IsFalse(v.IsUnknown);
            Assert.IsTrue(v.IsTrue);
            Assert.IsFalse(v.IsFalse);

            v = TriState.False;
            Assert.IsFalse(v.IsUnknown);
            Assert.IsFalse(v.IsTrue);
            Assert.IsTrue(v.IsFalse);

            v = true;
            Assert.IsFalse(v.IsUnknown);
            Assert.IsTrue(v.IsTrue);
            Assert.IsFalse(v.IsFalse);

            v = false;
            Assert.IsFalse(v.IsUnknown);
            Assert.IsFalse(v.IsTrue);
            Assert.IsTrue(v.IsFalse);

            v = new TriState(true);
            Assert.IsFalse(v.IsUnknown);
            Assert.IsTrue(v.IsTrue);
            Assert.IsFalse(v.IsFalse);

            v = new TriState(false);
            Assert.IsFalse(v.IsUnknown);
            Assert.IsFalse(v.IsTrue);
            Assert.IsTrue(v.IsFalse);
        }

        [TestMethod]
        [TestProperty("Lib", "LillTek.Common")]
        public void TriState_Casting()
        {
            TriState v;
            bool b;

            v = true;
            Assert.IsFalse(v.IsUnknown);
            Assert.IsTrue(v.IsTrue);
            Assert.IsFalse(v.IsFalse);

            v = false;
            Assert.IsFalse(v.IsUnknown);
            Assert.IsFalse(v.IsTrue);
            Assert.IsTrue(v.IsFalse);

            Assert.IsTrue((bool)new TriState(true));
            Assert.IsFalse((bool)new TriState(false));

            try
            {
                b = (bool)TriState.Unknown;
                Assert.Fail("Expected an ArgumentException");
            }
            catch (Exception e)
            {
                Assert.IsInstanceOfType(e, typeof(ArgumentException));
            }
        }

        [TestMethod]
        [TestProperty("Lib", "LillTek.Common")]
        public void TriState_Operators()
        {
            Assert.AreEqual(new TriState(true), new TriState(true));
            Assert.AreNotEqual(new TriState(true), new TriState(false));
            Assert.AreNotEqual(TriState.True, TriState.False);
            Assert.AreNotEqual(TriState.True, TriState.Unknown);
            Assert.AreNotEqual(TriState.False, TriState.Unknown);

            Assert.IsTrue(new TriState(TriState.True) == TriState.True);
            Assert.IsTrue(new TriState(TriState.False) == TriState.False);
            Assert.IsTrue(new TriState(TriState.Unknown) == TriState.Unknown);
            Assert.IsFalse(TriState.True == TriState.False);
            Assert.IsFalse(TriState.True == TriState.Unknown);
            Assert.IsFalse(TriState.Unknown == TriState.True);
            Assert.IsFalse(TriState.Unknown == TriState.False);

            Assert.IsFalse(new TriState(TriState.True) != TriState.True);
            Assert.IsFalse(new TriState(TriState.False) != TriState.False);
            Assert.IsFalse(new TriState(TriState.Unknown) != TriState.Unknown);
            Assert.IsTrue(TriState.True != TriState.False);
            Assert.IsTrue(TriState.True != TriState.Unknown);
            Assert.IsTrue(TriState.Unknown != TriState.True);
            Assert.IsTrue(TriState.Unknown != TriState.False);
        }
    }
}

