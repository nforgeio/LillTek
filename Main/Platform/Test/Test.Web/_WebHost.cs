//-----------------------------------------------------------------------------
// FILE:        _WebHost.cs
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
    public class _WebHost
    {
        [TestMethod]
        [TestProperty("Lib", "LillTek.Web")]
        public void WebHost_Test()
        {
            string physicalPath = Path.GetTempPath() + Helper.NewGuid().ToString("D");
            WebHost host = null;

            Directory.CreateDirectory(physicalPath);

            try
            {
                Helper.AppendToFile(physicalPath + Helper.PathSepString + "test.htm", "<html><body>Hello World!</body></html>");
                host = new WebHost(new string[] { "http://localhost:9000/test" }, physicalPath, true);

                Thread.Sleep(10000);
            }
            finally
            {
                if (host != null)
                    host.Close();

                Helper.DeleteFile(physicalPath + Helper.PathSepString + "*.*", true);
                Helper.DeleteFile(physicalPath);
            }
        }
    }
}

