//-----------------------------------------------------------------------------
// FILE:        _SpeechEngineSettings.cs
// CONTRIBUTOR: Jeff Lill
// COPYRIGHT:   Copyright (c) 2005-2015 by Jeffrey Lill.  All rights reserved.
// DESCRIPTION: UNIT tests.

using System;
using System.IO;
using System.Text;
using System.Diagnostics;

using Microsoft.VisualStudio.TestTools.UnitTesting;

using LillTek.Common;
using LillTek.Telephony.Common;
using LillTek.Telephony.NeonSwitch;
using LillTek.Telephony.NeonSwitchCore;
using LillTek.Testing;

namespace LillTek.Telephony.NeonSwitchCore.NUnit
{
    [TestClass]
    public class _SpeechEngineSettings
    {
        [TestMethod]
        [TestProperty("Lib", "LillTek.Telephony")]
        public void SpeechEngineSettings_Defaults()
        {
            // Verify the default settings.

            var settings = new SpeechEngineSettings();

            Assert.AreEqual(Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData), @"LillTek\NeonSwitch\PhraseCache"), settings.PhraseCacheFolder);
            Assert.AreEqual(Path.Combine(settings.PhraseCacheFolder, "OneTime"), settings.OneTimePhraseFolder);
            Assert.AreEqual(100, settings.PhraseFolderFanout);
            Assert.AreEqual(TimeSpan.FromMinutes(1), settings.PhrasePurgeInterval);
            Assert.AreEqual(TimeSpan.FromDays(1), settings.MaxPhraseTTL);
            Assert.AreEqual(TimeSpan.FromMinutes(5), settings.MaxOneTimePhraseTTL);
            Assert.AreEqual("auto", settings.DefaultVoice);
        }

        [TestMethod]
        [TestProperty("Lib", "LillTek.Telephony")]
        public void SpeechEngineSettings_LoadDefConfig()
        {
            // Verify that we can load default settings from an empty configuration file.

            try
            {
                Config.SetConfig(string.Empty);

                var settings = SpeechEngineSettings.LoadConfig("Test");

                Assert.AreEqual(Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData), @"LillTek\NeonSwitch\PhraseCache"), settings.PhraseCacheFolder);
                Assert.AreEqual(Path.Combine(settings.PhraseCacheFolder, "OneTime"), settings.OneTimePhraseFolder);
                Assert.AreEqual(100, settings.PhraseFolderFanout);
                Assert.AreEqual(TimeSpan.FromMinutes(1), settings.PhrasePurgeInterval);
                Assert.AreEqual(TimeSpan.FromDays(1), settings.MaxPhraseTTL);
                Assert.AreEqual(TimeSpan.FromMinutes(5), settings.MaxOneTimePhraseTTL);
                Assert.AreEqual("auto", settings.DefaultVoice);
            }
            finally
            {
                Config.SetConfig(null);
            }
        }

        [TestMethod]
        [TestProperty("Lib", "LillTek.Telephony")]
        public void SpeechEngineSettings_LoadConfig()
        {
            // Verify that we can load actual settings from an empty configuration file.

            string cfg = @"
&section Test

        PhraseCacheFolder   = C:\Cache
        OneTimePhraseFolder = C:\OneTime
        PhraseFolderFanout  = 10
        PhrasePurgeInterval = 5m
        MaxPhraseTTL        = 6m
        MaxOneTimePhraseTTL = 7m
        DefaultVoice        = Microsoft Anna

&endsection
";
            try
            {
                Config.SetConfig(cfg.Replace('&', '#'));

                var settings = SpeechEngineSettings.LoadConfig("Test");

                Assert.AreEqual("C:\\Cache", settings.PhraseCacheFolder);
                Assert.AreEqual("C:\\OneTime", settings.OneTimePhraseFolder);
                Assert.AreEqual(10, settings.PhraseFolderFanout);
                Assert.AreEqual(TimeSpan.FromMinutes(5), settings.PhrasePurgeInterval);
                Assert.AreEqual(TimeSpan.FromMinutes(6), settings.MaxPhraseTTL);
                Assert.AreEqual(TimeSpan.FromMinutes(7), settings.MaxOneTimePhraseTTL);
                Assert.AreEqual("Microsoft Anna", settings.DefaultVoice);
            }
            finally
            {
                Config.SetConfig(null);
            }
        }
    }
}

