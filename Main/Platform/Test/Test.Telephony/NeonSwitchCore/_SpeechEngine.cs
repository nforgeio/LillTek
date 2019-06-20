//-----------------------------------------------------------------------------
// FILE:        _SpeechEngine.cs
// CONTRIBUTOR: Jeff Lill
// COPYRIGHT:   Copyright (c) 2005-2015 by Jeffrey Lill.  All rights reserved.
// DESCRIPTION: UNIT tests.

using System;
using System.Diagnostics;
using System.IO;
using System.Media;
using System.Text;
using System.Threading;

using Microsoft.VisualStudio.TestTools.UnitTesting;

using LillTek.Common;
using LillTek.Telephony.Common;
using LillTek.Telephony.NeonSwitch;
using LillTek.Telephony.NeonSwitchCore;
using LillTek.Testing;

namespace LillTek.Telephony.NeonSwitchCore.NUnit
{
    [TestClass]
    public class _SpeechEngine
    {
        private const string PhraseCachePath = @"C:\\Temp\\PhraseCache";
        private const string OneTimePath = PhraseCachePath + @"\\OneTime";

        private void DeleteFolder()
        {
            Helper.DeleteFile(Path.Combine(PhraseCachePath, "*.*"), true);
        }

        private void PlayAudio(string path)
        {
            new SoundPlayer(path).PlaySync();
        }

        [TestMethod]
        [TestProperty("Lib", "LillTek.Telephony")]
        public void SpeechEngine_DefaultVoice()
        {
            DeleteFolder();

            try
            {
                var settings = new SpeechEngineSettings();

                settings.PhraseCacheFolder = PhraseCachePath;
                settings.OneTimePhraseFolder = OneTimePath;
                settings.PhraseFolderFanout = 0;

                SpeechEngine.Start(settings);

                PlayAudio(SpeechEngine.SpeakToFile(Phrase.PhoneText("Hello, I am {0}.  I am the default system voice.", SpeechEngine.DefaultVoice)));
            }
            finally
            {
                SpeechEngine.Stop();
                DeleteFolder();
            }
        }

        [TestMethod]
        [TestProperty("Lib", "LillTek.Telephony")]
        public void SpeechEngine_MicrosoftAnna()
        {
            //Assert.Ignore("[Microsofy Anna] is not working for some reason.");

            DeleteFolder();

            try
            {
                var settings = new SpeechEngineSettings();

                settings.PhraseCacheFolder = PhraseCachePath;
                settings.OneTimePhraseFolder = OneTimePath;
                settings.PhraseFolderFanout = 0;

                SpeechEngine.Start(settings);

                PlayAudio(SpeechEngine.SpeakToFile(Phrase.PhoneVoiceText("Microsoft Anna", "Hello, I am Microsoft Anna.  I am installed on all Windows operating systems.")));
            }
            finally
            {
                SpeechEngine.Stop();
                DeleteFolder();
            }
        }

        [TestMethod]
        [TestProperty("Lib", "LillTek.Telephony")]
        public void SpeechEngine_BadVoice()
        {
            DeleteFolder();

            try
            {
                var settings = new SpeechEngineSettings();

                settings.PhraseCacheFolder = PhraseCachePath;
                settings.OneTimePhraseFolder = OneTimePath;
                settings.PhraseFolderFanout = 0;

                SpeechEngine.Start(settings);

                PlayAudio(SpeechEngine.SpeakToFile(Phrase.PhoneVoiceText("Bad Voice", "An invalid voice was specified.  The default voice is being used instead.")));
            }
            finally
            {
                SpeechEngine.Stop();
                DeleteFolder();
            }
        }

        [TestMethod]
        [TestProperty("Lib", "LillTek.Telephony")]
        public void SpeechEngine_InstalledVoices()
        {
            DeleteFolder();

            try
            {
                var settings = new SpeechEngineSettings();

                settings.PhraseCacheFolder = PhraseCachePath;
                settings.OneTimePhraseFolder = OneTimePath;
                settings.PhraseFolderFanout = 0;

                SpeechEngine.Start(settings);

                foreach (var voice in SpeechEngine.InstalledVoices.Keys)
                {
                    PlayAudio(SpeechEngine.SpeakToFile(Phrase.PhoneVoiceText(voice, "Hello, my name is {0}.  I am one of the voices installed on your computer.", voice)));
                    Thread.Sleep(TimeSpan.FromSeconds(1.5));
                }
            }
            finally
            {
                SpeechEngine.Stop();
                DeleteFolder();
            }
        }

        [TestMethod]
        [TestProperty("Lib", "LillTek.Telephony")]
        public void SpeechEngine_VerifyCache()
        {
            DeleteFolder();

            try
            {
                var settings = new SpeechEngineSettings();
                string path1;
                string path2;

                settings.PhraseCacheFolder = PhraseCachePath;
                settings.OneTimePhraseFolder = OneTimePath;
                settings.PhraseFolderFanout = 0;

                SpeechEngine.Start(settings);

                path1 = SpeechEngine.SpeakToFile(Phrase.PhoneText("Hello cruel world!"));
                path2 = SpeechEngine.SpeakToFile(Phrase.PhoneText("Hello cruel world!"));

                Assert.AreEqual(path1, path2);
            }
            finally
            {
                SpeechEngine.Stop();
                DeleteFolder();
            }
        }

        [TestMethod]
        [TestProperty("Lib", "LillTek.Telephony")]
        public void SpeechEngine_SpeakOneTime()
        {
            DeleteFolder();

            try
            {
                var settings = new SpeechEngineSettings();

                settings.PhraseCacheFolder = PhraseCachePath;
                settings.OneTimePhraseFolder = OneTimePath;
                settings.PhraseFolderFanout = 0;

                SpeechEngine.Start(settings);

                var phrase = Phrase.PhoneText("This is a one-time phrase.");

                phrase.IsOneTime = true;

                PlayAudio(SpeechEngine.SpeakToFile(phrase));
            }
            finally
            {
                SpeechEngine.Stop();
                DeleteFolder();
            }
        }
    }
}

