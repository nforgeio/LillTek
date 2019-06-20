//-----------------------------------------------------------------------------
// FILE:        _TextProperties.cs
// CONTRIBUTOR: Jeff Lill
// COPYRIGHT:   Copyright (c) 2005-2015 by Jeffrey Lill. All rights reserved.
// DESCRIPTION: UNIT tests.

using System;
using System.Collections;
using System.Collections.Generic;
using System.Configuration;
using System.Drawing;
using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Reflection;
using System.Threading;

using LillTek.Common;
using LillTek.Testing;

using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace LillTek.Common.Test
{
    [TestClass]
    public class _TextProperties
    {
        [TestMethod]
        [TestProperty("Lib", "LillTek.Common")]
        public void TextProperties_Basic()
        {
            TextProperties settings;

            settings = new TextProperties("Arial", 10, GraphicsUnit.Pixel, Color.White, FontStyle.Regular);
            Assert.AreEqual("Arial", settings.FontName);
            Assert.AreEqual(10, settings.Size);
            Assert.AreEqual(GraphicsUnit.Pixel, settings.Unit);
            Assert.AreEqual(Color.White.ToArgb(), settings.Color.ToArgb());
            Assert.AreEqual(FontStyle.Regular, settings.Style);

            Assert.AreEqual("Arial,10,Pixel,#ffffffff,Regular", settings.ToString());

            Assert.IsTrue(TextProperties.TryParse("Arial,10,Pixel,#ffffffff,Regular", out settings));
            Assert.AreEqual("Arial", settings.FontName);
            Assert.AreEqual(10, settings.Size);
            Assert.AreEqual(GraphicsUnit.Pixel, settings.Unit);
            Assert.AreEqual(Color.White.ToArgb(), settings.Color.ToArgb());
            Assert.AreEqual(FontStyle.Regular, settings.Style);

            Assert.IsTrue(TextProperties.TryParse("Tahoma,12,Point,White,Bold", out settings));
            Assert.AreEqual("Tahoma,12,Point,#ffffffff,Bold", settings.ToString());
            Assert.AreEqual("Tahoma", settings.FontName);
            Assert.AreEqual(12, settings.Size);
            Assert.AreEqual(GraphicsUnit.Point, settings.Unit);
            Assert.AreEqual(Color.White.ToArgb(), settings.Color.ToArgb());
            Assert.AreEqual(FontStyle.Bold, settings.Style);

            Assert.IsTrue(TextProperties.TryParse("Tahoma,12,Point,Black,Bold+Italic", out settings));
            Assert.AreEqual("Tahoma,12,Point,#ff000000,Bold+Italic", settings.ToString());
            Assert.AreEqual("Tahoma", settings.FontName);
            Assert.AreEqual(12, settings.Size);
            Assert.AreEqual(GraphicsUnit.Point, settings.Unit);
            Assert.AreEqual(Color.Black.ToArgb(), settings.Color.ToArgb());
            Assert.AreEqual(FontStyle.Bold | FontStyle.Italic, settings.Style);
        }
    }
}

