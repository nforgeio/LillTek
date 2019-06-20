//-----------------------------------------------------------------------------
// FILE:        _JsonSerializer.cs
// CONTRIBUTOR: Jeff Lill
// COPYRIGHT:   Copyright (c) 2005-2015 by Jeffrey Lill.  All rights reserved.
// DESCRIPTION: Unit tests.

// $todo(jeff.lill): 
//
// I'm currently trusting that the underlying NewtonSoft
// library actually works so I'm only doing the most basic
// testing.

using System;

using Microsoft.VisualStudio.TestTools.UnitTesting;

using LillTek.Common;
using LillTek.Json;
using LillTek.Testing;

namespace LillTek.Json.Test
{
    [TestClass]
    public class _JsonSerializer
    {
        private class Simple
        {
            public int      Int;
            public double   Double;
            public string   String;
            public bool     Bool;

            public Simple()
            {
            }

            public Simple(int Int, double Double, string String, bool Bool)
            {
                this.Int    = Int;
                this.Double = Double;
                this.String = String;
                this.Bool   = Bool;
            }
        }

        [TestMethod]
        [TestProperty("Lib", "LillTek.Json")]
        public void JsonSerializer_ReadSimple()
        {
            string      output;
            Simple      o;

            output = JsonSerializer.ToString(new Simple(10, 123.456, "Hello World!", true));
            o      = (Simple)JsonSerializer.Read(output, typeof(Simple));

            Assert.AreEqual(10, o.Int);
            Assert.AreEqual(123.456, o.Double);
            Assert.AreEqual("Hello World!", o.String);
            Assert.IsTrue(o.Bool);
        }

        [TestMethod]
        [TestProperty("Lib", "LillTek.Json")]
        public void JsonSerializer_WriteSimple()
        {
            string output;

            output = JsonSerializer.ToString(new Simple(10, 123.456, "Hello World!", true));
            Assert.AreEqual("{\"Int\":10,\"Double\":123.456,\"String\":\"Hello World!\",\"Bool\":true}", output);
        }
    }
}
