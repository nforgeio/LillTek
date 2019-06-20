//-----------------------------------------------------------------------------
// FILE:        _ServiceModelHelper.cs
// CONTRIBUTOR: Jeff Lill
// COPYRIGHT:   Copyright (c) 2005-2015 by Jeffrey Lill.  All rights reserved.
// DESCRIPTION: Unit tests

using System;
using System.Collections.Generic;
using System.Text;

using LillTek.Common;
using LillTek.Messaging;
using LillTek.ServiceModel;
using LillTek.ServiceModel.Channels;

using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace LillTek.ServiceModel.Test
{
    [TestClass]
    public class _ServiceModelHelper
    {
        [TestMethod]
        [TestProperty("Lib", "LillTek.ServiceModel")]
        public void ServiceModelHelper_ToMsgEP()
        {
            MsgEP ep;

            try
            {
                ep = ServiceModelHelper.ToMsgEP("lilltek.physical://root/0/1");
                Assert.Fail("Expected ArgumentException");  // physical addressing is not supported
            }
            catch (ArgumentException)
            {
            }

            ep = ServiceModelHelper.ToMsgEP("lilltek.logical://myservice/1");
            Assert.AreEqual("logical://myservice/1", ep.ToString());

            ep = ServiceModelHelper.ToMsgEP("lilltek.abstract://myservice/2");
            Assert.AreEqual("logical://myservice/2", ep.ToString());     // "abstract" gets converted to "logical"

            ep = ServiceModelHelper.ToMsgEP(new Uri("lilltek.logical://myservice/1"));
            Assert.AreEqual("logical://myservice/1", ep.ToString());

            ep = ServiceModelHelper.ToMsgEP(new Uri("lilltek.abstract://myservice/2"));
            Assert.AreEqual("logical://myservice/2", ep.ToString());     // "abstract" gets converted to "logical"

            try
            {
                ep = ServiceModelHelper.ToMsgEP((string)null);
                Assert.Fail("Expected an ArgumentNullException");
            }
            catch (ArgumentNullException)
            {
            }

            try
            {
                ep = ServiceModelHelper.ToMsgEP((Uri)null);
                Assert.Fail("Expected an ArgumentNullException");
            }
            catch (ArgumentNullException)
            {
            }

            try
            {
                ep = ServiceModelHelper.ToMsgEP("http://www.foo.com");
                Assert.Fail("Expected an ArgumentException");
            }
            catch (ArgumentException)
            {
            }

            try
            {
                ep = ServiceModelHelper.ToMsgEP(new Uri("http://www.foo.com"));
                Assert.Fail("Expected an ArgumentException");
            }
            catch (ArgumentException)
            {
            }
        }

        [TestMethod]
        [TestProperty("Lib", "LillTek.ServiceModel")]
        public void ServiceModelHelper_ValidateEP()
        {
            ServiceModelHelper.ValidateEP(new Uri("lilltek.logical://test"));
            ServiceModelHelper.ValidateEP(new Uri("lilltek.abstract://test"));

            try
            {
                ServiceModelHelper.ValidateEP(new Uri("lilltek.physical://test"));
                Assert.Fail("Expected an ArgumentException");
            }
            catch (ArgumentException)
            {
            }

            try
            {
                ServiceModelHelper.ValidateEP(new Uri("http://test"));
                Assert.Fail("Expected an ArgumentException");
            }
            catch (ArgumentException)
            {
            }

            try
            {
                ServiceModelHelper.ValidateEP(new Uri("lilltek.logical://test:80"));
                Assert.Fail("Expected an ArgumentException");
            }
            catch (ArgumentException)
            {
            }

            try
            {
                ServiceModelHelper.ValidateEP(new Uri("lilltek.abstract://test:80"));
                Assert.Fail("Expected an ArgumentException");
            }
            catch (ArgumentException)
            {
            }
        }

        [TestMethod]
        [TestProperty("Lib", "LillTek.ServiceModel")]
        public void ServiceModelHelper_ValidateTimeout()
        {
            TimeSpan timeout;

            ServiceModelHelper.ValidateTimeout(TimeSpan.Zero);
            ServiceModelHelper.ValidateTimeout(TimeSpan.FromMinutes(100));

            try
            {
                ServiceModelHelper.ValidateTimeout(TimeSpan.FromMinutes(-100));
                Assert.Fail("Expected an ArgumentException");
            }
            catch (ArgumentException)
            {
            }

            timeout = ServiceModelHelper.ValidateTimeout(TimeSpan.MaxValue);
            Assert.IsTrue(SysTime.Now + timeout < DateTime.MaxValue);
        }
    }
}

