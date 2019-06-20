//-----------------------------------------------------------------------------
// FILE:        _SipBaseTimers.cs
// CONTRIBUTOR: Jeff Lill
// COPYRIGHT:   Copyright (c) 2005-2015 by Jeffrey Lill.  All rights reserved.
// DESCRIPTION: Unit tests

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;

using LillTek.Common;
using LillTek.Net.Sockets;

using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace LillTek.Telephony.Sip.Test
{
    [TestClass]
    public class _SipBaseTimers
    {
        [TestMethod]
        [TestProperty("Lib", "LillTek.Telephony.Sip")]
        public void SipBaseTimers_LoadConfig()
        {
            try
            {
                Config.SetConfig(@"
&section Timers
    T1     = 1s
    T2     = 2s
    T4     = 3s
&endsection
".Replace('&', '#'));
                SipBaseTimers timer = SipBaseTimers.LoadConfig("Timers");

                Assert.AreEqual(TimeSpan.FromSeconds(1), timer.T1);
                Assert.AreEqual(TimeSpan.FromSeconds(2), timer.T2);
                Assert.AreEqual(TimeSpan.FromSeconds(3), timer.T4);
            }
            finally
            {
                Config.SetConfig("");
            }
        }
    }
}

