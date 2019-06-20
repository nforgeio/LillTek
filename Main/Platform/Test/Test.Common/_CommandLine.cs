//-----------------------------------------------------------------------------
// FILE:        _CommandLine.cs
// CONTRIBUTOR: Jeff Lill
// COPYRIGHT:   Copyright (c) 2005-2015 by Jeffrey Lill.  All rights reserved.
// DESCRIPTION: UNIT tests for the Config class.

using System;
using System.IO;
using System.Text;

using Microsoft.VisualStudio.TestTools.UnitTesting;

using LillTek.Common;
using LillTek.Testing;

namespace LillTek.Common.Test
{
    [TestClass]
    public class _CommandLine
    {
        [TestMethod]
        [TestProperty("Lib", "LillTek.Common")]
        public void CommandLine_Positional()
        {
            var cmd = new CommandLine(new string[] { "a", "b", "c", "d", "-e", "f" });

            Assert.AreEqual(6, cmd.Count);
            Assert.AreEqual("a", cmd[0]);
            Assert.AreEqual("b", cmd[1]);
            Assert.AreEqual("c", cmd[2]);
            Assert.AreEqual("d", cmd[3]);
            Assert.AreEqual("-e", cmd[4]);
            Assert.AreEqual("f", cmd[5]);
        }

        [TestMethod]
        [TestProperty("Lib", "LillTek.Common")]
        public void CommandLine_Options()
        {
            var cmd = new CommandLine(new string[] { "a", "b", "c", "-d", "-e:", "f", "-g:xx", "-", "-hello:world" });

            Assert.AreEqual(9, cmd.Count);
            CollectionAssert.AreEqual(new string[] { "a", "b", "c", "-d", "-e:", "f", "-g:xx", "-", "-hello:world" }, cmd.Arguments);

            Assert.IsNull(cmd[""]);
            Assert.IsNull(cmd["aaaaa"]);
            Assert.AreEqual(string.Empty, cmd["d"]);
            Assert.AreEqual(string.Empty, cmd["e"]);
            Assert.AreEqual("xx", cmd["g"]);
            Assert.AreEqual("world", cmd["hello"]);

            Assert.AreEqual("default", cmd.GetOption("", "default"));
            Assert.AreEqual("default", cmd.GetOption("aaaaa", "default"));
            Assert.IsNull(cmd.GetOption("aaaaa", null));
            Assert.AreEqual("world", cmd.GetOption("hello", "default"));
        }

        [TestMethod]
        [TestProperty("Lib", "LillTek.Common")]
        public void CommandLine_Values()
        {
            var cmd = new CommandLine(new string[] { "aa", "-o1:aaaa", "bb", "-o2:bbbb", "cc", "dd" });

            Assert.AreEqual(6, cmd.Count);
            CollectionAssert.AreEqual(new string[] { "aa", "-o1:aaaa", "bb", "-o2:bbbb", "cc", "dd" }, cmd.Arguments);

            CollectionAssert.AreEqual(new string[] { "aa", "bb", "cc", "dd" }, cmd.Values);

            Assert.AreEqual("aaaa", cmd["o1"]);
            Assert.AreEqual("bbbb", cmd["o2"]);
        }

        [TestMethod]
        [TestProperty("Lib", "LillTek.Common")]
        public void CommandLine_File()
        {
            var path = Environment.CurrentDirectory + "\\test.ini";

            try
            {
                using (var writer = new StreamWriter(path, false, Encoding.UTF8))
                {
                    writer.Write(@"
$$ This is a comment

-param1
-param2
     
-param3
   -param4   
".Replace("$$", "#"));   // Added the Replace() to work around a C# parsing error for noUNIT builds
                }

                var args = CommandLine.ExpandFiles(new string[] { "@" + path });

                Assert.AreEqual(4, args.Length);
                Assert.AreEqual("-param1", args[0]);
                Assert.AreEqual("-param2", args[1]);
                Assert.AreEqual("-param3", args[2]);
                Assert.AreEqual("-param4", args[3]);
            }
            finally
            {
                Helper.DeleteFile(path);
            }
        }

        [TestMethod]
        [TestProperty("Lib", "LillTek.Common")]
        public void CommandLine_File_MultiLine()
        {
            var path = Environment.CurrentDirectory + "\\test.ini";

            try
            {
                using (var writer = new StreamWriter(path, false, Encoding.UTF8))
                {
                    writer.Write(@"
$$ This is a comment

-param1
-param2
     
-param3
   -param4   

   {{
        -param5:
        Hello
        World
   }}
".Replace("$$", "#"));   // Added the Replace() to work around a C# parsing error for noUNIT builds
                }

                var args = CommandLine.ExpandFiles(new string[] { "@" + path });

                Assert.AreEqual(5, args.Length);
                Assert.AreEqual("-param1", args[0]);
                Assert.AreEqual("-param2", args[1]);
                Assert.AreEqual("-param3", args[2]);
                Assert.AreEqual("-param4", args[3]);
                Assert.AreEqual("-param5: Hello World", args[4]);
            }
            finally
            {
                Helper.DeleteFile(path);
            }
        }

        [TestMethod]
        [TestProperty("Lib", "LillTek.Common")]
        public void CommandLine_Format()
        {
            Assert.AreEqual("1 2 3 4", CommandLine.Format(1, 2, 3, 4));
            Assert.AreEqual("1 \"Hello World\" 2", CommandLine.Format(1, "Hello World", 2));
            Assert.AreEqual("1 \"Hello \" World 2", CommandLine.Format(1, "Hello ", "World", 2));

            try
            {
                CommandLine.Format("\"");
                Assert.Fail("Expected a FormatException");
            }
            catch (FormatException)
            {
                // Expected
            }

            try
            {
                CommandLine.Format("\t");
                Assert.Fail("Expected a FormatException");
            }
            catch (FormatException)
            {
                // Expected
            }
        }

        [TestMethod]
        [TestProperty("Lib", "LillTek.Common")]
        public void CommandLine_Parse()
        {
            var cmdLine = CommandLine.Parse("");

            CollectionAssert.AreEqual(new string[0], cmdLine.Arguments);

            cmdLine = CommandLine.Parse("hello");
            CollectionAssert.AreEqual(new string[] { "hello" }, cmdLine.Arguments);

            cmdLine = CommandLine.Parse("   hello   ");
            CollectionAssert.AreEqual(new string[] { "hello" }, cmdLine.Arguments);

            cmdLine = CommandLine.Parse("\"hello\"");
            CollectionAssert.AreEqual(new string[] { "hello" }, cmdLine.Arguments);

            cmdLine = CommandLine.Parse("  \"hello\"  ");
            CollectionAssert.AreEqual(new string[] { "hello" }, cmdLine.Arguments);

            cmdLine = CommandLine.Parse("\"hello");
            CollectionAssert.AreEqual(new string[] { "hello" }, cmdLine.Arguments);

            cmdLine = CommandLine.Parse("\"hello world\"");
            CollectionAssert.AreEqual(new string[] { "hello world" }, cmdLine.Arguments);

            cmdLine = CommandLine.Parse("hello world");
            CollectionAssert.AreEqual(new string[] { "hello", "world" }, cmdLine.Arguments);

            cmdLine = CommandLine.Parse("  hello \t world");
            CollectionAssert.AreEqual(new string[] { "hello", "world" }, cmdLine.Arguments);

            cmdLine = CommandLine.Parse("hello\tworld");
            CollectionAssert.AreEqual(new string[] { "hello", "world" }, cmdLine.Arguments);

            cmdLine = CommandLine.Parse("\"arg 1\" \"arg 2\" arg3");
            CollectionAssert.AreEqual(new string[] { "arg 1", "arg 2", "arg3" }, cmdLine.Arguments);
            Assert.AreEqual("\"arg 1\" \"arg 2\" arg3", cmdLine.ToString());
        }

        [TestMethod]
        [TestProperty("Lib", "LillTek.Common")]
        public void CommandLine_Subset()
        {
            CommandLine cmdLine = new CommandLine(new string[] { "zero", "one", "two", "three" }, false);
            CommandLine subset;

            subset = cmdLine.Subset(0);
            CollectionAssert.AreEqual(new string[] { "zero", "one", "two", "three" }, subset.Arguments);

            subset = cmdLine.Subset(1);
            CollectionAssert.AreEqual(new string[] { "one", "two", "three" }, subset.Arguments);

            subset = cmdLine.Subset(2);
            CollectionAssert.AreEqual(new string[] { "two", "three" }, subset.Arguments);

            subset = cmdLine.Subset(3);
            CollectionAssert.AreEqual(new string[] { "three" }, subset.Arguments);

            subset = cmdLine.Subset(4);
            CollectionAssert.AreEqual(new string[0], subset.Arguments);
        }

        [TestMethod]
        [TestProperty("Lib", "LillTek.Common")]
        public void CommandLine_GetOptionValues()
        {
            CommandLine cmdLine = new CommandLine(new string[] { "-t:hello", "-t:world", "-t", "t" });
            string[] values;

            values = cmdLine.GetOptionValues("t");
            Assert.AreEqual(2, values.Length);
            Assert.AreEqual("hello", values[0]);
            Assert.AreEqual("world", values[1]);
        }
    }
}

