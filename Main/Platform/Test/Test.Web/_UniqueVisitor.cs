//-----------------------------------------------------------------------------
// FILE:        _UniqueVisitor.cs
// CONTRIBUTOR: Jeff Lill
// COPYRIGHT:   Copyright (c) 2005-2015 by Jeffrey Lill.  All rights reserved.
// DESCRIPTION: Unit tests

using System;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Reflection;
using System.Text;
using System.Threading;
using System.Web;
using System.Xml.Linq;

using Microsoft.VisualStudio.TestTools.UnitTesting;

using LillTek.Common;
using LillTek.Cryptography;
using LillTek.Service;
using LillTek.Testing;

namespace LillTek.Web.Test
{
    [TestClass]
    public class _UniqueVisitor
    {
        [TestMethod]
        [TestProperty("Lib", "LillTek.Web")]
        public void UniqueVisitor_Basic()
        {
            UniqueVisitor visitor;

            visitor = new UniqueVisitor();
            Assert.AreNotEqual(Guid.Empty, visitor.ID);
            Assert.IsTrue(Helper.Within(visitor.IssueDateUtc, DateTime.UtcNow, TimeSpan.FromSeconds(1.0)));

            Assert.AreNotEqual(visitor, new UniqueVisitor());
            Assert.AreNotEqual(new UniqueVisitor().GetHashCode(), new UniqueVisitor().GetHashCode());    // OK, this could happen once every few billion test runs
        }

        [TestMethod]
        [TestProperty("Lib", "LillTek.Web")]
        public void UniqueVisitor_Serialize()
        {
            UniqueVisitor v1, v2;
            string cookie;

            v1 = new UniqueVisitor();
            cookie = v1.ToString();
            v2 = new UniqueVisitor(cookie);

            Assert.AreEqual(v1, v2);
            Assert.AreEqual(v1.ID, v2.ID);
            Assert.AreEqual(v1.IssueDateUtc, v2.IssueDateUtc);
        }
    }
}

