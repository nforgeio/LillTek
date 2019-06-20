//-----------------------------------------------------------------------------
// FILE:        _GuidList.cs
// CONTRIBUTOR: Jeff Lill
// COPYRIGHT:   Copyright (c) 2005-2015 by Jeffrey Lill.  All rights reserved.
// DESCRIPTION: Unit tests

using System;

using Microsoft.VisualStudio.TestTools.UnitTesting;

using LillTek.Common;
using LillTek.Advanced;

namespace LillTek.Advanced.Test
{
    [TestClass]
    public class _GuidList
    {
        [TestMethod]
        [TestProperty("Lib", "LillTek.Advanced")]
        public void GuidList_Basic()
        {
            GuidList orgList;
            GuidList newList;
            byte[] bytes;

            orgList = new GuidList();
            orgList.Add(Helper.NewGuid());
            orgList.Add(Helper.NewGuid());
            orgList.Add(Helper.NewGuid());

            bytes = orgList.ToByteArray();
            Assert.AreEqual(16 * orgList.Count, bytes.Length);

            newList = new GuidList(bytes);
            CollectionAssert.AreEqual(orgList.ToArray(), newList.ToArray());
        }
    }
}

