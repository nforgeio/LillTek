//-----------------------------------------------------------------------------
// FILE:        _TextLog.cs
// CONTRIBUTOR: Jeff Lill
// COPYRIGHT:   Copyright (c) 2005-2015 by Jeffrey Lill.  All rights reserved.
// DESCRIPTION: UNIT tests

using System;
using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Threading;
using System.Configuration;
using System.Collections.Generic;

using Microsoft.VisualStudio.TestTools.UnitTesting;

using LillTek.Common;
using LillTek.Testing;

namespace LillTek.Common.Test
{
    [TestClass]
    public class _TextLog
    {
        [TestMethod]
        [TestProperty("Lib", "LillTek.Common")]
        public void TextLog_Basic()
        {
            string folder = Path.GetTempPath() + Helper.NewGuid().ToString();
            TextLog log = null;

            try
            {
                log = new TextLog(folder, 128);

                for (int i = 0; i < 10; i++)
                    log.Log("category", "title", "details " + i.ToString());

                Thread.Sleep(2000);

                for (int i = 10; i < 20; i++)
                    log.Log("category", "title", "details " + i.ToString());

                // $todo(jeff.lill): For now, you need to set a breakpoint and
                //               manually verify that this worked.
            }
            finally
            {
                if (log != null)
                    log.Dispose();

                Helper.DeleteFile(folder, true);
            }
        }
    }
}

