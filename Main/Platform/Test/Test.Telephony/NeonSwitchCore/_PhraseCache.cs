//-----------------------------------------------------------------------------
// FILE:        _PhraseCache.cs
// CONTRIBUTOR: Jeff Lill
// COPYRIGHT:   Copyright (c) 2005-2015 by Jeffrey Lill.  All rights reserved.
// DESCRIPTION: UNIT tests.

using System;
using System.Diagnostics;
using System.IO;
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
    public class _PhraseCache
    {
        private const string PhraseCachePath = @"C:\\Temp\\PhraseCache";
        private const string OneTimePath = PhraseCachePath + @"\\OneTime";

        private void DeleteFolder()
        {
            Helper.DeleteFile(Path.Combine(PhraseCachePath, "*.*"), true);
        }

        [TestMethod]
        [TestProperty("Lib", "LillTek.Telephony")]
        public void PhraseCache_FanoutFolders_0()
        {
            // Verify that the cache creates the necessary folders for 0 fanout.

            string path;
            string fileName;

            DeleteFolder();

            try
            {
                var settings = new SpeechEngineSettings();

                settings.PhraseCacheFolder = PhraseCachePath;
                settings.OneTimePhraseFolder = OneTimePath;
                settings.PhraseFolderFanout = 0;

                SpeechEngine.Start(settings);

                Assert.IsTrue(Directory.Exists(PhraseCachePath));
                Assert.IsTrue(Directory.Exists(OneTimePath));

                // Make sure there are no subfolders.

                Assert.IsFalse(Directory.Exists(Path.Combine(PhraseCachePath, "0")));
                Assert.IsFalse(Directory.Exists(Path.Combine(PhraseCachePath, "00")));
                Assert.IsFalse(Directory.Exists(Path.Combine(PhraseCachePath, "000")));
                Assert.IsFalse(Directory.Exists(Path.Combine(PhraseCachePath, "0000")));

                Assert.IsFalse(Directory.Exists(Path.Combine(PhraseCachePath, "1")));
                Assert.IsFalse(Directory.Exists(Path.Combine(PhraseCachePath, "01")));
                Assert.IsFalse(Directory.Exists(Path.Combine(PhraseCachePath, "001")));
                Assert.IsFalse(Directory.Exists(Path.Combine(PhraseCachePath, "0001")));

                // Verify that audio files are created directly in the cache folder.

                path = SpeechEngine.PhraseCache.GetNextPhrasePath(Phrase.PhoneText("Hello World!"));
                fileName = Path.GetFileName(path);

                Assert.AreEqual(PhraseCachePath, path.Substring(0, path.Length - fileName.Length - 1));

                // Verify that one-time files are created directly within the one-time folder.

                path = SpeechEngine.PhraseCache.GetNextOneTimePath(Phrase.PhoneText("Hello World!"));
                fileName = Path.GetFileName(path);

                Assert.AreEqual(OneTimePath, path.Substring(0, path.Length - fileName.Length - 1));
            }
            finally
            {
                SpeechEngine.Stop();
                DeleteFolder();
            }
        }

        [TestMethod]
        [TestProperty("Lib", "LillTek.Telephony")]
        public void PhraseCache_FanoutFolders_10()
        {
            // Verify that the cache creates the necessary folders for 10 fanout.

            string path;
            string fileName;
            int pos;

            DeleteFolder();

            try
            {
                var settings = new SpeechEngineSettings();

                settings.PhraseCacheFolder = PhraseCachePath;
                settings.OneTimePhraseFolder = OneTimePath;
                settings.PhraseFolderFanout = 10;

                SpeechEngine.Start(settings);

                Assert.IsTrue(Directory.Exists(PhraseCachePath));
                Assert.IsTrue(Directory.Exists(OneTimePath));

                // Verify that the subfolders exist,

                for (int i = 0; i < 10; i++)
                    Assert.IsTrue(Directory.Exists(Path.Combine(PhraseCachePath, string.Format("{0}", i))));

                // Verify that audio files are created in a subfolder.

                path = SpeechEngine.PhraseCache.GetNextPhrasePath(Phrase.PhoneText("Hello World!"));
                fileName = Path.GetFileName(path);

                Assert.AreEqual(PhraseCachePath, path.Substring(0, path.Length - fileName.Length - 3));

                pos = PhraseCachePath.Length;   // Should index the "\" after the base cache path.

                Assert.AreEqual('\\', path[pos++]);
                Assert.IsTrue(Char.IsDigit(path[pos++]));
                Assert.AreEqual('\\', path[pos++]);

                // Verify that one-time files are created directly within the one-time folder.

                path = SpeechEngine.PhraseCache.GetNextOneTimePath(Phrase.PhoneText("Hello World!"));
                fileName = Path.GetFileName(path);

                Assert.AreEqual(OneTimePath, path.Substring(0, path.Length - fileName.Length - 1));
            }
            finally
            {
                SpeechEngine.Stop();
                DeleteFolder();
            }
        }

        [TestMethod]
        [TestProperty("Lib", "LillTek.Telephony")]
        public void PhraseCache_FanoutFolders_100()
        {
            // Verify that the cache creates the necessary folders for 100 fanout.

            string path;
            string fileName;
            int pos;

            DeleteFolder();

            try
            {
                var settings = new SpeechEngineSettings();

                settings.PhraseCacheFolder = PhraseCachePath;
                settings.OneTimePhraseFolder = OneTimePath;
                settings.PhraseFolderFanout = 100;

                SpeechEngine.Start(settings);

                Assert.IsTrue(Directory.Exists(PhraseCachePath));
                Assert.IsTrue(Directory.Exists(OneTimePath));

                // Verify that the subfolders exist,

                for (int i = 0; i < 100; i++)
                    Assert.IsTrue(Directory.Exists(Path.Combine(PhraseCachePath, string.Format("{0:0#}", i))));

                // Verify that audio files are created in a subfolder.

                path = SpeechEngine.PhraseCache.GetNextPhrasePath(Phrase.PhoneText("Hello World!"));
                fileName = Path.GetFileName(path);

                Assert.AreEqual(PhraseCachePath, path.Substring(0, path.Length - fileName.Length - 4));

                pos = PhraseCachePath.Length;   // Should index the "\" after the base cache path.

                Assert.AreEqual('\\', path[pos++]);
                Assert.IsTrue(Char.IsDigit(path[pos++]));
                Assert.IsTrue(Char.IsDigit(path[pos++]));
                Assert.AreEqual('\\', path[pos++]);

                // Verify that one-time files are created directly within the one-time folder.

                path = SpeechEngine.PhraseCache.GetNextOneTimePath(Phrase.PhoneText("Hello World!"));
                fileName = Path.GetFileName(path);

                Assert.AreEqual(OneTimePath, path.Substring(0, path.Length - fileName.Length - 1));
            }
            finally
            {
                SpeechEngine.Stop();
                DeleteFolder();
            }
        }

        [TestMethod]
        [TestProperty("Lib", "LillTek.Telephony")]
        public void PhraseCache_BasicPhrase()
        {
            // Verify that the cache actually caches phrases properly.

            Phrase phrase;

            DeleteFolder();

            try
            {
                var settings = new SpeechEngineSettings();

                settings.PhraseCacheFolder = PhraseCachePath;
                settings.OneTimePhraseFolder = OneTimePath;
                settings.PhraseFolderFanout = 0;
                settings.DefaultVoice = "Microsoft Anna";

                SpeechEngine.Start(settings);

                // Add a phrase to the cache with the [Microsoft Anna] voice.

                phrase = Phrase.PhoneText("Hello World!");
                phrase.Path = SpeechEngine.PhraseCache.GetNextPhrasePath(phrase);
                File.WriteAllBytes(phrase.Path, new byte[] { 0, 1, 2, 3, 4, 5, 6, 7, 8, 9 });
                SpeechEngine.PhraseCache.AddPhrase(phrase);

                // Verify that the file is persisted in the SpeechEngine.PhraseCache.

                Assert.AreEqual(phrase.Path, SpeechEngine.PhraseCache.FindPhrase(phrase).Path);
                Assert.IsNotNull(SpeechEngine.PhraseCache.FindPhrase(Phrase.PhoneVoiceText("Microsoft Anna", "Hello World!")));

                // Verify that character case doesn't matter for voice names.

                Assert.IsNotNull(SpeechEngine.PhraseCache.FindPhrase(Phrase.PhoneVoiceText("MICROSOFT ANNA", "Hello World!")));

                // Verify that a search for a phrase with the same text but
                // a different voice does not return a cache hit.

                Assert.IsNull(SpeechEngine.PhraseCache.FindPhrase(Phrase.PhoneVoiceText("Microsoft Hayley", "Hello World!")));

                // Verify that a search for a phrase with the same voice but
                // different text does not return a cache hit.

                Assert.IsNull(SpeechEngine.PhraseCache.FindPhrase(Phrase.PhoneVoiceText("Microsoft Anna", "This is a test!")));
            }
            finally
            {
                SpeechEngine.Stop();
                DeleteFolder();
            }
        }

        [TestMethod]
        [TestProperty("Lib", "LillTek.Telephony")]
        public void PhraseCache_PersistPhrase()
        {
            // Verify that the cache actually persists phrases after a restart.

            Phrase phrase;

            DeleteFolder();

            try
            {
                var settings = new SpeechEngineSettings();

                settings.PhraseCacheFolder = PhraseCachePath;
                settings.OneTimePhraseFolder = OneTimePath;
                settings.PhraseFolderFanout = 0;
                settings.DefaultVoice = "Microsoft Anna";

                SpeechEngine.Start(settings);

                // Add a phrase to the cache with the [Microsoft Anna] voice.

                phrase = Phrase.PhoneText("Hello World!");
                phrase.Path = SpeechEngine.PhraseCache.GetNextPhrasePath(phrase);
                File.WriteAllBytes(phrase.Path, new byte[] { 0, 1, 2, 3, 4, 5, 6, 7, 8, 9 });
                SpeechEngine.PhraseCache.AddPhrase(phrase);

                // Verify that the file is persisted in the SpeechEngine.PhraseCache.

                Assert.AreEqual(phrase.Path, SpeechEngine.PhraseCache.FindPhrase(phrase).Path);
                Assert.IsNotNull(SpeechEngine.PhraseCache.FindPhrase(Phrase.PhoneVoiceText("Microsoft Anna", "Hello World!")));

                // Verify that character case doesn't matter for voice names.

                Assert.IsNotNull(SpeechEngine.PhraseCache.FindPhrase(Phrase.PhoneVoiceText("MICROSOFT ANNA", "Hello World!")));

                // Verify that a search for a phrase with the same text but
                // a different voice does not return a cache hit.

                Assert.IsNull(SpeechEngine.PhraseCache.FindPhrase(Phrase.PhoneVoiceText("Microsoft Hayley", "Hello World!")));

                // Verify that a search for a phrase with the same voice but
                // different text does not return a cache hit.

                Assert.IsNull(SpeechEngine.PhraseCache.FindPhrase(Phrase.PhoneVoiceText("Microsoft Anna", "This is a test!")));

                // Stop and restart the speech engine and then verify that the cached phrase is still there.

                SpeechEngine.Stop();
                SpeechEngine.Start(settings);

                phrase = SpeechEngine.PhraseCache.FindPhrase(Phrase.PhoneText("Hello World!"));

                Assert.IsNotNull(phrase);
                Assert.IsNotNull(phrase.Path);
                Assert.IsTrue(File.Exists(phrase.Path));
            }
            finally
            {
                SpeechEngine.Stop();
                DeleteFolder();
            }
        }

        [TestMethod]
        [TestProperty("Lib", "LillTek.Telephony")]
        public void PhraseCache_BasicOneTime()
        {
            // Verify that the cache actually handles one-time phrases.

            Phrase phrase;
            string path;

            DeleteFolder();

            try
            {
                var settings = new SpeechEngineSettings();

                settings.PhraseCacheFolder = PhraseCachePath;
                settings.OneTimePhraseFolder = OneTimePath;
                settings.PhraseFolderFanout = 0;

                SpeechEngine.Start(settings);

                // Add a one-time phrase

                phrase = Phrase.PhoneText("Hello World!");
                path = SpeechEngine.PhraseCache.GetNextOneTimePath(phrase);

                // Verify that the path returned is in the one-time folder.

                Assert.AreEqual(OneTimePath, path.Substring(0, path.Length - (Path.GetFileName(path).Length + 1)));
            }
            finally
            {
                SpeechEngine.Stop();
                DeleteFolder();
            }
        }

        [TestMethod]
        [TestProperty("Lib", "LillTek.Telephony")]
        public void PhraseCache_CorruptIndex()
        {
            // Verify that the cache can start with a corrupt index file.

            Phrase phrase;

            DeleteFolder();

            Helper.CreateFileTree(PhraseCachePath);
            File.WriteAllBytes(Path.Combine(PhraseCachePath, PhraseCache.IndexFileName), new byte[] { 0 });

            try
            {
                var settings = new SpeechEngineSettings();

                settings.PhraseCacheFolder = PhraseCachePath;
                settings.OneTimePhraseFolder = OneTimePath;
                settings.PhraseFolderFanout = 0;
                settings.DefaultVoice = "Microsoft Anna";

                SpeechEngine.Start(settings);

                // Add a phrase to the cache with the [Microsoft Anna] voice.

                phrase = Phrase.PhoneText("Hello World!");
                phrase.Path = SpeechEngine.PhraseCache.GetNextPhrasePath(phrase);
                File.WriteAllBytes(phrase.Path, new byte[] { 0, 1, 2, 3, 4, 5, 6, 7, 8, 9 });
                SpeechEngine.PhraseCache.AddPhrase(phrase);

                // Verify that the file is persisted in the SpeechEngine.PhraseCache.

                Assert.AreEqual(phrase.Path, SpeechEngine.PhraseCache.FindPhrase(phrase).Path);
                Assert.IsNotNull(SpeechEngine.PhraseCache.FindPhrase(Phrase.PhoneVoiceText("Microsoft Anna", "Hello World!")));

                // Verify that character case doesn't matter for voice names.

                Assert.IsNotNull(SpeechEngine.PhraseCache.FindPhrase(Phrase.PhoneVoiceText("MICROSOFT ANNA", "Hello World!")));

                // Verify that a search for a phrase with the same text but
                // a different voice does not return a cache hit.

                Assert.IsNull(SpeechEngine.PhraseCache.FindPhrase(Phrase.PhoneVoiceText("Microsoft Hayley", "Hello World!")));

                // Verify that a search for a phrase with the same voice but
                // different text does not return a cache hit.

                Assert.IsNull(SpeechEngine.PhraseCache.FindPhrase(Phrase.PhoneVoiceText("Microsoft Anna", "This is a test!")));
            }
            finally
            {
                SpeechEngine.Stop();
                DeleteFolder();
            }
        }

        [TestMethod]
        [TestProperty("Lib", "LillTek.Telephony")]
        public void PhraseCache_PurgeCache()
        {
            // Verify that the cache purges expired phrases.

            Phrase phrase;

            DeleteFolder();

            try
            {
                var settings = new SpeechEngineSettings();

                settings.PhraseCacheFolder = PhraseCachePath;
                settings.OneTimePhraseFolder = OneTimePath;
                settings.PhraseFolderFanout = 0;
                settings.PhrasePurgeInterval = TimeSpan.FromSeconds(0.5);
                settings.MaxPhraseTTL = TimeSpan.FromSeconds(1);

                SpeechEngine.Start(settings);

                // Create a phrase and add it to the cache.

                phrase = Phrase.PhoneText("Hello World!");
                phrase.Path = SpeechEngine.PhraseCache.GetNextPhrasePath(phrase);
                File.WriteAllBytes(phrase.Path, new byte[] { 0, 1, 2, 3, 4, 5, 6, 7, 8, 9 });
                SpeechEngine.PhraseCache.AddPhrase(phrase);

                // Wait five seconds for the phrase to expire.

                Thread.Sleep(TimeSpan.FromSeconds(5));

                // Call the speech engine's background task handler so it
                // can handle cache purging and then verify that the 
                // phrase was actually purged.

                SpeechEngine.OnBkTask();
                Helper.WaitFor(() => !File.Exists(phrase.Path), TimeSpan.FromSeconds(5));

                Assert.IsNull(SpeechEngine.PhraseCache.FindPhrase(phrase));
                Assert.IsFalse(File.Exists(phrase.Path));
            }
            finally
            {
                SpeechEngine.Stop();
                DeleteFolder();
            }
        }

        [TestMethod]
        [TestProperty("Lib", "LillTek.Telephony")]
        public void PhraseCache_PurgeCache_WithSubfolders()
        {
            // Verify that the cache purges expired phrases within subfolders.

            Phrase phrase;

            DeleteFolder();

            try
            {
                var settings = new SpeechEngineSettings();

                settings.PhraseCacheFolder = PhraseCachePath;
                settings.OneTimePhraseFolder = OneTimePath;
                settings.PhraseFolderFanout = 10;
                settings.PhrasePurgeInterval = TimeSpan.FromSeconds(0.5);
                settings.MaxPhraseTTL = TimeSpan.FromSeconds(1);

                SpeechEngine.Start(settings);

                // Create a phrase and add it to the cache.

                phrase = Phrase.PhoneText("Hello World!");
                phrase.Path = SpeechEngine.PhraseCache.GetNextPhrasePath(phrase);
                File.WriteAllBytes(phrase.Path, new byte[] { 0, 1, 2, 3, 4, 5, 6, 7, 8, 9 });
                SpeechEngine.PhraseCache.AddPhrase(phrase);

                // Wait five seconds for the phrase to expire.

                Thread.Sleep(TimeSpan.FromSeconds(5));

                // Call the speech engine's background task handler so it
                // can handle cache purging and then verify that the 
                // phrase was actually purged.

                SpeechEngine.OnBkTask();
                Helper.WaitFor(() => !File.Exists(phrase.Path), TimeSpan.FromSeconds(5));

                Assert.IsNull(SpeechEngine.PhraseCache.FindPhrase(phrase));
                Assert.IsFalse(File.Exists(phrase.Path));
            }
            finally
            {
                SpeechEngine.Stop();
                DeleteFolder();
            }
        }

        [TestMethod]
        [TestProperty("Lib", "LillTek.Telephony")]
        public void PhraseCache_PurgeOneTime()
        {
            // Verify that the cache purges one-time files.

            string path;

            DeleteFolder();

            try
            {
                var settings = new SpeechEngineSettings();

                settings.PhraseCacheFolder = PhraseCachePath;
                settings.OneTimePhraseFolder = OneTimePath;
                settings.PhraseFolderFanout = 0;
                settings.PhrasePurgeInterval = TimeSpan.FromSeconds(0.5);
                settings.MaxOneTimePhraseTTL = TimeSpan.FromSeconds(1);

                SpeechEngine.Start(settings);

                // Create a one-time phrase and add it to the cache.

                path = SpeechEngine.PhraseCache.GetNextOneTimePath(Phrase.PhoneText("Hello World!"));
                File.WriteAllBytes(path, new byte[] { 0, 1, 2, 3, 4, 5, 6, 7, 8, 9 });

                // Wait five seconds for the phrase to expire.

                Thread.Sleep(TimeSpan.FromSeconds(5));

                // Call the speech engine's background task handler so it
                // can handle cache purging and then verify that the 
                // phrase was actually purged.

                SpeechEngine.OnBkTask();
                Helper.WaitFor(() => !File.Exists(path), TimeSpan.FromSeconds(5));

                Assert.IsFalse(File.Exists(path));
            }
            finally
            {
                SpeechEngine.Stop();
                DeleteFolder();
            }
        }

        [TestMethod]
        [TestProperty("Lib", "LillTek.Telephony")]
        public void PhraseCache_PurgeOneTime_OnStop()
        {
            // Verify that the cache purges one-time files when stopped.

            string path;

            DeleteFolder();

            try
            {
                var settings = new SpeechEngineSettings();

                settings.PhraseCacheFolder = PhraseCachePath;
                settings.OneTimePhraseFolder = OneTimePath;
                settings.PhraseFolderFanout = 0;

                SpeechEngine.Start(settings);

                // Create a one-time phrase and add it to the cache.

                path = SpeechEngine.PhraseCache.GetNextOneTimePath(Phrase.PhoneText("Hello World!"));
                File.WriteAllBytes(path, new byte[] { 0, 1, 2, 3, 4, 5, 6, 7, 8, 9 });

                SpeechEngine.Stop();
                Assert.IsFalse(File.Exists(path));
            }
            finally
            {
                SpeechEngine.Stop();
                DeleteFolder();
            }
        }

        [TestMethod]
        [TestProperty("Lib", "LillTek.Telephony")]
        public void PhraseCache_PurgeOrphaned()
        {
            // Verify that the cache deletes orhpaned files.

            string path1;
            string path2;

            DeleteFolder();

            try
            {
                var settings = new SpeechEngineSettings();

                settings.PhraseCacheFolder = PhraseCachePath;
                settings.OneTimePhraseFolder = OneTimePath;
                settings.PhraseFolderFanout = 10;
                settings.PhrasePurgeInterval = TimeSpan.FromSeconds(0.5);
                settings.MaxOneTimePhraseTTL = TimeSpan.FromSeconds(1);

                SpeechEngine.Start(settings);

                // Add couple files to the cache, one at the root and the second
                // in a subfolder.  Then force a cache purge and then verify that
                // the files were deleted and the index file still exists.

                path1 = Path.Combine(PhraseCachePath, "test1.dat");
                File.WriteAllText(path1, "test1");

                path2 = Path.Combine(PhraseCachePath, "0", "test2.dat");
                File.WriteAllText(path2, "test2");

                // Call the speech engine's background task handler so it
                // can handle cache purging and then verify that the 
                // phrase was actually purged.

                Thread.Sleep(TimeSpan.FromSeconds(2));
                SpeechEngine.OnBkTask();
                Helper.WaitFor(() => !File.Exists(path1), TimeSpan.FromSeconds(5));

                Assert.IsFalse(File.Exists(path1));
                Assert.IsFalse(File.Exists(path2));

                // Make sure we didn't purge the index file by accident.

                Assert.IsTrue(File.Exists(Path.Combine(PhraseCachePath, PhraseCache.IndexFileName)));
            }
            finally
            {
                SpeechEngine.Stop();
                DeleteFolder();
            }
        }

        [TestMethod]
        [TestProperty("Lib", "LillTek.Telephony")]
        public void PhraseCache_Blast()
        {
            // Blast the cache with traffic on multiple threads to make sure
            // that nothing bad happens.

            // Verify that the cache purges expired phrases.

            bool stop = false;
            int count = 0;

            DeleteFolder();

            try
            {
                var settings = new SpeechEngineSettings();

                settings.PhraseCacheFolder = PhraseCachePath;
                settings.OneTimePhraseFolder = OneTimePath;
                settings.PhraseFolderFanout = 100;
                settings.PhrasePurgeInterval = TimeSpan.FromSeconds(0.5);
                settings.MaxPhraseTTL = TimeSpan.FromSeconds(1);
                settings.MaxOneTimePhraseTTL = TimeSpan.FromSeconds(1);

                SpeechEngine.Start(settings);

                // Simulate a background task thread.

                Helper.EnqueueAction(
                    () =>
                    {
                        while (!stop)
                        {
                            SpeechEngine.OnBkTask();
                            Thread.Sleep(100);
                        }
                    });

                // Create 5 threads that cache phrases.

                for (int i = 0; i < 5; i++)
                    Helper.EnqueueAction(
                        () =>
                        {
                            while (!stop)
                            {
                                var phrase = Phrase.PhoneText("Hello World: {0}", Interlocked.Increment(ref count));

                                phrase.Path = SpeechEngine.PhraseCache.GetNextPhrasePath(phrase);
                                File.WriteAllBytes(phrase.Path, new byte[] { 0, 1, 2, 3, 4, 5, 6, 7, 8, 9 });
                                SpeechEngine.PhraseCache.AddPhrase(phrase);
                                Thread.Sleep(1000);
                            }
                        });

                // Create 5 threads that create one-time phrases.

                for (int i = 0; i < 5; i++)
                    Helper.EnqueueAction(
                        () =>
                        {
                            while (!stop)
                            {
                                var phrase = Phrase.PhoneText("Hello World: {0}", Interlocked.Increment(ref count));

                                phrase.Path = SpeechEngine.PhraseCache.GetNextOneTimePath(phrase);
                                File.WriteAllBytes(phrase.Path, new byte[] { 0, 1, 2, 3, 4, 5, 6, 7, 8, 9 });
                                Thread.Sleep(1000);
                            }
                        });

                // Let the thing run for a while before signalling the threads to stop.

                Thread.Sleep(TimeSpan.FromSeconds(60));
                stop = true;

                // Give the threads a chance to stop.

                Thread.Sleep(TimeSpan.FromSeconds(5));
            }
            finally
            {
                SpeechEngine.Stop();
                DeleteFolder();
            }
        }
    }
}

