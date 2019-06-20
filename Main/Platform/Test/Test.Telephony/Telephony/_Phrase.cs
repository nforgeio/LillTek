//-----------------------------------------------------------------------------
// FILE:        _Phrase.cs
// CONTRIBUTOR: Jeff Lill
// COPYRIGHT:   Copyright (c) 2005-2015 by Jeffrey Lill. All rights reserved.
// DESCRIPTION: Unit tests

using System;
using System.Configuration;
using System.Diagnostics;
using System.Net;
using System.Net.Sockets;
using System.Reflection;
using System.Security;
using System.Text;
using System.Threading;

using LillTek.Common;
using LillTek.Telephony.Common;
using LillTek.Testing;

using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace LillTek.Telephony.Common.NUnit
{
    [TestClass]
    public class _Phrase
    {
        [TestMethod]
        [TestProperty("Lib", "LillTek.Telephony")]
        public void Phrase_EncodeDecode()
        {
            // Verify that phrases can be encoded and decoded into string form.

            Phrase phrase1;
            Phrase phrase2;
            string encoded;

            phrase1 = Phrase.PhoneVoiceText("Microsoft Anna", "This is a test.");
            encoded = phrase1.Encode();
            phrase2 = Phrase.Decode(encoded);

            Assert.AreEqual(PhraseType.Text, phrase2.PhraseType);
            Assert.AreEqual(false, phrase2.IsOneTime);
            Assert.AreEqual(TtsSampleRate.KHz_8000, phrase2.SampleRate);
            Assert.AreEqual(TtsEncoding.Pcm8, phrase2.Encoding);
            Assert.AreEqual("Microsoft Anna", phrase2.Voice);
            Assert.AreEqual("This is a test.", phrase2.Text);

            phrase1 = Phrase.PhoneVoiceSsml("Microsoft Haley", "Hello World!");
            phrase1.IsOneTime = true;
            encoded = phrase1.Encode();
            phrase2 = Phrase.Decode(encoded);

            Assert.AreEqual(PhraseType.Ssml, phrase2.PhraseType);
            Assert.AreEqual(true, phrase2.IsOneTime);
            Assert.AreEqual(TtsSampleRate.KHz_8000, phrase2.SampleRate);
            Assert.AreEqual(TtsEncoding.Pcm8, phrase2.Encoding);
            Assert.AreEqual("Microsoft Haley", phrase2.Voice);
            Assert.AreEqual("Hello World!", phrase2.Text);
        }
    }
}

