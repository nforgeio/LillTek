//-----------------------------------------------------------------------------
// FILE:        _EnvironmentVars.cs
// CONTRIBUTOR: Jeff Lill
// COPYRIGHT:   Copyright (c) 2005-2015 by Jeffrey Lill. All rights reserved.
// DESCRIPTION: UNIT tests for the EnvironmentVars class.

using System;
using System.IO;
using System.Net;
using System.Reflection;

using Microsoft.VisualStudio.TestTools.UnitTesting;

using LillTek.Common;
using LillTek.Testing;

namespace LillTek.Common.Test
{
    [TestClass]
    public class _EnvironmentVars
    {
        [ClassInitialize]
        public static void Setup(TestContext context)
        {
            Helper.InitializeApp(Assembly.GetExecutingAssembly());
        }

        [TestMethod]
        [TestProperty("Lib", "LillTek.Common")]
        public void EnvironmentVars_Basic()
        {
            string cfg1 =
@"
var1=10
// This is a comment  
   var2  =  20   

VAR3=30

path=$(ProgramDataPath)\Foo
";
            EnvironmentVars.Load(cfg1);
            Assert.AreEqual("10", EnvironmentVars.Get("var1"));
            Assert.AreEqual("20", EnvironmentVars.Get("VAR2"));
            Assert.AreEqual("30", EnvironmentVars.Get("var3"));
            Assert.IsNull(EnvironmentVars.Get("var4"));
            Assert.AreEqual(Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData), "Foo"), EnvironmentVars.Expand(EnvironmentVars.Get("path")));
        }

        [TestMethod]
        [TestProperty("Lib", "LillTek.Common")]
        public void EnvironmentVars_BuiltIn()
        {
            EnvironmentVars.Load("");

            Assert.AreEqual(Environment.GetEnvironmentVariable("temp"), EnvironmentVars.Get("temp"));
            Assert.AreEqual(Environment.GetEnvironmentVariable("tmp"), EnvironmentVars.Get("tmp"));
            Assert.AreEqual(Environment.GetEnvironmentVariable("SystemRoot"), EnvironmentVars.Get("SystemRoot"));
            Assert.AreEqual((Environment.GetEnvironmentVariable("SystemRoot") + @"\system32").ToLowerInvariant(), EnvironmentVars.Get("SystemDirectory").ToLowerInvariant());
            Assert.AreEqual(Helper.EntryAssemblyFolder, EnvironmentVars.Get("AppPath"));
            Assert.IsNotNull(EnvironmentVars.Get("OS"));
            Assert.IsNotNull(EnvironmentVars.Get("WINFULL"));
            Assert.IsNull(EnvironmentVars.Get("WINCE"));
            Assert.AreEqual(Helper.GetVersionString(Assembly.GetExecutingAssembly()), EnvironmentVars.Get("appversion"));
            Assert.AreEqual(Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData), EnvironmentVars.ProgramDataPath);
            Assert.IsTrue(!EnvironmentVars.ProgramDataPath.EndsWith("/"));
            Assert.IsTrue(!EnvironmentVars.ProgramDataPath.EndsWith("\\"));
            Assert.AreNotEqual(EnvironmentVars.Get("guid"), EnvironmentVars.Get("Guid"));
            Assert.AreEqual(Helper.MachineName, EnvironmentVars.Get("MachineName"));
            Assert.AreEqual(Dns.GetHostName(), EnvironmentVars.Get("HostName"));
            Assert.AreEqual(EnvironmentVars.ServerID, EnvironmentVars.Get("ServerID"));

            Assert.AreEqual(Environment.GetEnvironmentVariable("temp"), EnvironmentVars.Expand("$(temp)"));
            Assert.AreEqual(Environment.GetEnvironmentVariable("tmp"), EnvironmentVars.Expand("$(tmp)"));
            Assert.AreEqual(Environment.GetEnvironmentVariable("SystemRoot"), EnvironmentVars.Expand("$(SystemRoot)"));
            Assert.AreEqual((Environment.GetEnvironmentVariable("SystemRoot") + @"\system32").ToLowerInvariant(), EnvironmentVars.Expand("$(SystemDirectory)").ToLowerInvariant());
            Assert.AreEqual(Helper.EntryAssemblyFolder, EnvironmentVars.Expand("$(AppPath)"));
            Assert.IsNotNull(EnvironmentVars.Expand("$(OS)"));
            Assert.IsNotNull(EnvironmentVars.Expand("$(WINFULL)"));
            Assert.AreEqual("$(WINCE)", EnvironmentVars.Expand("$(WINCE)"));
            Assert.AreNotEqual(EnvironmentVars.Expand("$(guid)"), EnvironmentVars.Expand("$(Guid)"));
            Assert.AreEqual(Helper.MachineName, EnvironmentVars.Expand("$(MachineName)"));
            Assert.AreEqual(Environment.ProcessorCount.ToString(), EnvironmentVars.Expand("$(ProcessorCount)"));

            Assert.AreEqual(Const.DCCloudEP.ToString(), EnvironmentVars.Expand("$(LillTek.DC.CloudEP)"));
            Assert.AreEqual(Const.DCCloudGroup.ToString(), EnvironmentVars.Expand("$(LillTek.DC.CloudGroup)"));
            Assert.AreEqual(Const.DCCloudPort.ToString(), EnvironmentVars.Expand("$(LillTek.DC.CloudPort)"));
            Assert.AreEqual(Const.DCRootPort.ToString(), EnvironmentVars.Expand("$(LillTek.DC.RootPort)"));
            Assert.AreEqual(Const.DCDefHubName, EnvironmentVars.Expand("$(LillTek.DC.DefHubName)"));
#if DEBUG
            Assert.AreEqual("true", EnvironmentVars.Expand("$(IsDebug)"));
            Assert.AreEqual("false", EnvironmentVars.Expand("$(IsRelease)"));
#else
            Assert.AreEqual("false",EnvironmentVars.Expand("$(IsDebug)"));
            Assert.AreEqual("true",EnvironmentVars.Expand("$(IsRelease)"));
#endif
        }

        [TestMethod]
        [TestProperty("Lib", "LillTek.Common")]
        public void EnvironmentVars_System()
        {
            EnvironmentVars.Load("");
            Assert.AreEqual(Environment.GetEnvironmentVariable("path"), EnvironmentVars.Get("path"));
            Assert.AreEqual(Environment.GetEnvironmentVariable("systemdrive"), EnvironmentVars.Get("systemdrive"));
            Assert.AreEqual(Environment.GetEnvironmentVariable("windir"), EnvironmentVars.Get("windir"));
            Assert.AreEqual(Environment.GetEnvironmentVariable("comspec"), EnvironmentVars.Get("comspec"));
        }

        [TestMethod]
        [TestProperty("Lib", "LillTek.Common")]
        public void EnvironmentVars_Expand()
        {
            string cfg1 =
@"
var1=INSERT
";
            EnvironmentVars.Load(cfg1);

            // Old style

            Assert.AreEqual("", EnvironmentVars.Expand(""));
            Assert.AreEqual("INSERT", EnvironmentVars.Expand("INSERT"));
            Assert.AreEqual("INSERT", EnvironmentVars.Expand("%var1%"));
            Assert.AreEqual("prefix INSERT suffix", EnvironmentVars.Expand("prefix %VAR1% suffix"));
            Assert.AreEqual("%none%", EnvironmentVars.Expand("%none%"));
            Assert.AreEqual("prefix %none% suffix", EnvironmentVars.Expand("prefix %none% suffix"));
            Assert.AreEqual("%", EnvironmentVars.Expand("%"));
            Assert.AreEqual("%hello", EnvironmentVars.Expand("%hello"));
            Assert.AreEqual("hello%", EnvironmentVars.Expand("hello%"));
            Assert.AreEqual("hello%world", EnvironmentVars.Expand("hello%world"));

            // New style

            Assert.AreEqual("", EnvironmentVars.Expand(""));
            Assert.AreEqual("INSERT", EnvironmentVars.Expand("INSERT"));
            Assert.AreEqual("INSERT", EnvironmentVars.Expand("$(var1)"));
            Assert.AreEqual("prefix INSERT suffix", EnvironmentVars.Expand("prefix $(VAR1) suffix"));
            Assert.AreEqual("$(none)", EnvironmentVars.Expand("$(none)"));
            Assert.AreEqual("prefix $(none) suffix", EnvironmentVars.Expand("prefix $(none) suffix"));
            Assert.AreEqual("$", EnvironmentVars.Expand("$"));
            Assert.AreEqual("$(", EnvironmentVars.Expand("$("));
            Assert.AreEqual("$hello", EnvironmentVars.Expand("$hello"));
            Assert.AreEqual("$(hello", EnvironmentVars.Expand("$(hello"));
            Assert.AreEqual("$(hello)", EnvironmentVars.Expand("$(hello)"));
            Assert.AreEqual("hello)", EnvironmentVars.Expand("hello)"));
            Assert.AreEqual("hello)world", EnvironmentVars.Expand("hello)world"));
        }

        [TestMethod]
        [TestProperty("Lib", "LillTek.Common")]
        public void EnvironmentVars_Expand_Recursive()
        {
            string cfg1;

            // Old style

            cfg1 =
@"
var1=HELLO
var2=%var1%
var3=WORLD
var4=%var2% %var3%
";
            EnvironmentVars.Load(cfg1);
            Assert.AreEqual("HELLO", EnvironmentVars.Expand("%var2%"));
            Assert.AreEqual("prefix HELLO suffix", EnvironmentVars.Expand("prefix %var2% suffix"));
            Assert.AreEqual("HELLO WORLD", EnvironmentVars.Expand("%var4%"));

            // New style

            cfg1 =
@"
var1=HELLO
var2=$(var1)
var3=WORLD
var4=$(var2) $(var3)
";
            EnvironmentVars.Load(cfg1);
            Assert.AreEqual("HELLO", EnvironmentVars.Expand("$(var2)"));
            Assert.AreEqual("prefix HELLO suffix", EnvironmentVars.Expand("prefix $(var2) suffix"));
            Assert.AreEqual("HELLO WORLD", EnvironmentVars.Expand("$(var4)"));
        }

        [TestMethod]
        [TestProperty("Lib", "LillTek.Common")]
        public void EnvironmentVars_Expand_InfiniteRecursion()
        {
            string cfg1;

            // Old style

            cfg1 =
                @"
var1=%var2%
var2=%var1%
";
            EnvironmentVars.Load(cfg1);

            try
            {
                EnvironmentVars.Expand("%var2%");
                Assert.Fail();  // Expected a StackOverflowException
            }
            catch (StackOverflowException)
            {
            }
            catch
            {
                Assert.Fail();  // Expected a StackOverflowException
            }

            // New style

            cfg1 =
@"
var1=$(var2)
var2=$(var1)
";
            EnvironmentVars.Load(cfg1);

            try
            {
                EnvironmentVars.Expand("$(var2)");
                Assert.Fail();  // Expected a StackOverflowException
            }
            catch (StackOverflowException)
            {
            }
            catch
            {
                Assert.Fail();  // Expected a StackOverflowException
            }
        }

        [TestMethod]
        [TestProperty("Lib", "LillTek.Common")]
        public void EnvironmentVars_IPAddressing()
        {
            string output;

            // This test exercises the IP address macros without actually checking
            // their values (since they'll differ from machine to machine).  This
            // will test for crashes and also provide a place where these values
            // can be manually verified.

            output = EnvironmentVars.Expand(@"

ip-address = $(ip-address)
ip-mask    = $(ip-mask)
ip-subnet  = $(ip-subnet)
");
        }

        [TestMethod]
        [TestProperty("Lib", "LillTek.Common")]
        public void EnvironmentVars_ServerID()
        {
            string orgServerID;

            orgServerID = EnvironmentVars.ServerID;
            if (String.Compare(orgServerID, Dns.GetHostName(), true) == 0)
                orgServerID = null;

            try
            {
                EnvironmentVars.ServerID = "www.lilltek.com";
                Assert.AreEqual("www.lilltek.com", EnvironmentVars.Expand("$(ServerID)"));

                EnvironmentVars.ServerID = null;
                Assert.AreEqual(Dns.GetHostName(), EnvironmentVars.Expand("$(ServerID)"));
            }
            finally
            {
                EnvironmentVars.ServerID = orgServerID;
            }
        }

        [TestMethod]
        [TestProperty("Lib", "LillTek.Common")]
        public void EnvironmentVars_IsVariable()
        {
            // Actual environment vairabl;es

            Assert.IsTrue(EnvironmentVars.IsVariable("path"));
            Assert.IsTrue(EnvironmentVars.IsVariable("temp"));
            Assert.IsTrue(EnvironmentVars.IsVariable("tmp"));

            // Handle LillTek built-in variables.

            Assert.IsTrue(EnvironmentVars.IsVariable("os"));
            Assert.IsTrue(EnvironmentVars.IsVariable("appversion"));
        }
    }
}

