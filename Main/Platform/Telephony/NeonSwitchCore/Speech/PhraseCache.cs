//-----------------------------------------------------------------------------
// FILE:        PhraseCache.cs
// CONTRIBUTOR: Jeff Lill
// COPYRIGHT:   Copyright (c) 2005-2015 by Jeffrey Lill.  All rights reserved.
// DESCRIPTION: Manages the caching of phrases to the file system.

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;

using FreeSWITCH;
using FreeSWITCH.Native;

using LillTek.Common;
using LillTek.Telephony.Common;
using LillTek.Telephony.NeonSwitch;

using Switch = LillTek.Telephony.NeonSwitch.Switch;

// $todo(jeff.lill):
//
// This implementation is currently hardcoded to telephone quality
// audio sampling rates 8KHz 8-bit PCM.
//
// Also hardcoded to use the Cepstral engine and voices.
//
// I'd also like to persist the cache state in a local SQL Express
// database at somepoint rather than the custom file format.
//
// Implement a maximum cache size too (maximum number of files as
// well as maximum total file size).

namespace LillTek.Telephony.NeonSwitchCore
{
    /// <summary>
    /// Manages the caching of phrases to the file system.
    /// </summary>
    /// <threadsafety instance="true" />
    public class PhraseCache
    {
        internal const string IndexFileName = "Cache.index";  // Name of the cache index file
        private const int Magic = 0x71330001;     // Magic number for index file

        private object                      syncLock = new object();
        private bool                        isRunning;
        private SpeechEngineSettings        settings;
        private Dictionary<string, Phrase>  index;
        private PolledTimer                 purgeTimer;
        private PolledTimer                 oneTimePurgeTimer;

        /// <summary>
        /// Constructs and initializes the phrase cache using TTS engine
        /// settings passed.
        /// </summary>
        /// <param name="settings">The engine settings.</param>
        public PhraseCache(SpeechEngineSettings settings)
        {
            this.settings          = settings;
            this.isRunning         = true;
            this.index             = new Dictionary<string, Phrase>();
            this.purgeTimer        = new PolledTimer(settings.PhrasePurgeInterval, true);
            this.oneTimePurgeTimer = new PolledTimer(Helper.Divide(settings.MaxOneTimePhraseTTL, 2));

            // Make sure the cache folders exist.

            Helper.CreateFolderTree(settings.PhraseCacheFolder);
            Helper.CreateFolderTree(settings.OneTimePhraseFolder);

            for (int i = 0; i < settings.PhraseFolderFanout; i++)
                Helper.CreateFolderTree(Path.Combine(settings.PhraseCacheFolder, GetSubfolderName(i)));

            // Initialize the cache.

            PurgeOneTime();
            LoadIndex();
        }

        /// <summary>
        /// This must be called periodically manage phrase purging.
        /// </summary>
        public void OnBkTask()
        {
            lock (syncLock)
            {
                if (!isRunning)
                    return;

                if (oneTimePurgeTimer.HasFired)
                    Helper.EnqueueAction(() => PurgeOneTime());

                if (purgeTimer.HasFired)
                    PurgeCache();
            }
        }

        /// <summary>
        /// Stops the phrase cache if it's running.
        /// </summary>
        public void Stop()
        {
            lock (syncLock)
            {
                if (!isRunning)
                    return;

                isRunning = false;

                SaveIndex(false);
                PurgeCache();

                index = null;

                // Delete all one-time files.

                try
                {
                    foreach (var path in Directory.GetFiles(settings.OneTimePhraseFolder, "*.*"))
                        File.Delete(path);
                }
                catch
                {
                    // Ignore errors
                }
            }
        }

        /// <summary>
        /// Returns the formatted subfolder name to use to the specified folder index.
        /// </summary>
        /// <param name="index">The folder index.</param>
        /// <returns>The formatted folder name.</returns>
        private string GetSubfolderName(int index)
        {
            if (settings.PhraseFolderFanout <= 10)
                return string.Format("{0}", index);
            else if (settings.PhraseFolderFanout <= 100)
                return string.Format("{0:0#}", index);
            else if (settings.PhraseFolderFanout <= 1000)
                return string.Format("{0:00#}", index);
            else if (settings.PhraseFolderFanout <= 10000)
                return string.Format("{0:000#}", index);
            else
                return string.Format("{0}", index);
        }

        /// <summary>
        /// Returns the key to be used to identify the phrase text/voice combination in the cache.
        /// </summary>
        /// <param name="phrase">The phrase.</param>
        /// <returns>The cache key.</returns>
        private string GetCacheKey(Phrase phrase)
        {
            return string.Format("[voice={0}]{1}", phrase.ActualVoice.ToUpper(), phrase.Text);
        }

        /// <summary>
        /// Returns the fully qualified path where the next cached phrase audio file is to be written.
        /// </summary>
        /// <param name="phrase">The phrase information.</param>
        /// <returns>The audio file path.</returns>
        public string GetNextPhrasePath(Phrase phrase)
        {
            if (settings.PhraseFolderFanout == 0)
                return Path.Combine(settings.PhraseCacheFolder, string.Format("{0:D}.wav", Guid.NewGuid()));
            else
                return Path.Combine(settings.PhraseCacheFolder, string.Format(@"{0}\{1:D}.wav", GetSubfolderName(Helper.RandIndex(settings.PhraseFolderFanout)), Guid.NewGuid()));
        }

        /// <summary>
        /// Adds the genrated phrase audio file passed to the cache.
        /// </summary>
        /// <param name="phrase">The phrase being added.</param>
        /// <exception cref="ArgumentNullException">Thrown if <paramref name="phrase" /> is <c>null</c>.</exception>
        /// <exception cref="ArgumentException">Thrown if the <paramref name="phrase" />.<see cref="Path"/> property is <c>null</c>.</exception>
        /// <exception cref="InvalidOperationException">Thrown if the cache has been stopped or if a one-time phrase is passed.</exception>
        public void AddPhrase(Phrase phrase)
        {
            Phrase existing;

            if (phrase == null)
                throw new ArgumentNullException("phrase");

            if (phrase.Path == null)
                throw new ArgumentException("[Path] property cannot be NULL.", "phrase");

            if (phrase.IsOneTime)
                throw new InvalidOperationException("[AddPhrase] cannot accept a one-time phrase.");

            phrase = phrase.Clone();

            // The requested voice might not exist, so get the actual
            // voice that can be used.

            phrase.ActualVoice = SpeechEngine.GetVoice(phrase.Voice);
            if (phrase.ActualVoice == null)
                return;     // No installed voices

            lock (syncLock)
            {
                if (!isRunning)
                    throw new InvalidOperationException("[PhraseCache] is stopped.");

                phrase.LastAccessUtc = DateTime.UtcNow;

                if (index.TryGetValue(GetCacheKey(phrase), out existing))
                {
                    // The phrase is already in the index.

                    existing.LastAccessUtc = phrase.LastAccessUtc;
                }
                else
                    index.Add(GetCacheKey(phrase), phrase);
            }
        }

        /// <summary>
        /// Searches the cache for a particular phrase.
        /// </summary>
        /// <param name="phrase">The phrase information.</param>
        /// <returns>The phrase information if found in the cache or <c>null</c>.</returns>
        /// <exception cref="ArgumentNullException">Thrown if <pararef name="phrase" /> is <c>null</c>.</exception>
        /// <exception cref="InvalidOperationException">Thrown if the cache has been stopped.</exception>
        public Phrase FindPhrase(Phrase phrase)
        {
            Phrase existing;

            if (phrase == null)
                throw new ArgumentNullException("phrase");

            // The requested voice might not exist, so get the actual
            // voice that can be used.

            phrase.ActualVoice = SpeechEngine.GetVoice(phrase.Voice);
            if (phrase.ActualVoice == null)
                return null;    // No installed voices

            lock (syncLock)
            {
                if (!isRunning)
                    throw new InvalidOperationException("[PhraseCache] is stopped.");

                if (index.TryGetValue(GetCacheKey(phrase), out existing))
                    return existing;
                else
                    return null;
            }
        }

        /// <summary>
        /// Returns the fully qualified path to the file where the next one-time phrase is to be written. 
        /// </summary>
        /// <param name="phrase">The phrase information.</param>
        /// <returns>Path to the audio file.</returns>
        /// <exception cref="ArgumentNullException">Thrown if <paramref name="phrase" /> is <c>null</c>.</exception>
        /// <exception cref="InvalidOperationException">Thrown if the cache has been stopped.</exception>
        public string GetNextOneTimePath(Phrase phrase)
        {
            return Path.Combine(settings.OneTimePhraseFolder, string.Format("{0:D}.wav", Guid.NewGuid()));
        }

        /// <summary>
        /// Loads the cache index file.
        /// </summary>
        private void LoadIndex()
        {
            var indexPath = Path.Combine(settings.PhraseCacheFolder, IndexFileName);

            try
            {
                if (!File.Exists(indexPath))
                {
                    // No index exists yet, so create an empty one.

                    SaveIndex(false);
                    return;
                }

                using (var fs = new EnhancedFileStream(indexPath, FileMode.Open))
                {
                    int count;

                    if (fs.ReadInt32() != Magic)
                        throw new SwitchException("[PhraseCache] cannot read index file [{0}] due to an invalid magic number.  The existing cache will be purged.", indexPath);

                    count = fs.ReadInt32();
                    for (int i = 0; i < count; i++)
                    {
                        string          text;
                        string          voice;
                        string          actualVoice;
                        PhraseType      phraseType;
                        TtsEncoding     encoding;
                        TtsSampleRate   rate;
                        DateTime        lastAccessUtc;
                        string          path;
                        Phrase          phrase;

                        phraseType    = (PhraseType)Enum.Parse(typeof(PhraseType), fs.ReadString16());
                        text          = fs.ReadString32();
                        voice         = fs.ReadString16();
                        actualVoice   = fs.ReadString16();
                        encoding      = (TtsEncoding)Enum.Parse(typeof(TtsEncoding), fs.ReadString16());
                        rate          = (TtsSampleRate)Enum.Parse(typeof(TtsSampleRate), fs.ReadString16());
                        lastAccessUtc = new DateTime(fs.ReadInt64());
                        path          = fs.ReadString16();

                        phrase = new Phrase(phraseType, voice, encoding, rate, text)
                        {
                            Path          = path,
                            ActualVoice   = actualVoice,
                            LastAccessUtc = lastAccessUtc,
                        };

                        index[GetCacheKey(phrase)] = phrase;
                    }
                }
            }
            catch (Exception e)
            {
                // We're going to handle all exceptions leaving the loaded phrase
                // table empty.  This will have the effect of starting the cache
                // from scratch.  Eventually, all of the existing files will be
                // purged and the presumably bad index file will be overwritten.

                index.Clear();
                SysLog.LogException(e);
            }
        }

        /// <summary>
        /// Persists the cache index file (optionally on a background thread).
        /// </summary>
        /// <param name="background">Pass <c>true</c> to persist on a background thread.</param>
        private void SaveIndex(bool background)
        {
            var indexPath = Path.Combine(settings.PhraseCacheFolder, IndexFileName);
            var phrases   = index.Values.ToArray();

            if (background)
            {
                Helper.EnqueueAction(
                    () =>
                    {
                        SaveIndexTo(indexPath, phrases);
                    });
            }
            else
                SaveIndexTo(indexPath, phrases);
        }

        /// <summary>
        /// Writes the index file.
        /// </summary>
        /// <param name="path">The target file path.</param>
        /// <param name="phrases">The phrases to be written.</param>
        private void SaveIndexTo(string path, Phrase[] phrases)
        {
            using (var fs = new EnhancedFileStream(path, FileMode.Create))
            {
                fs.WriteInt32(Magic);
                fs.WriteInt32(phrases.Length);

                foreach (var phrase in phrases)
                {
                    fs.WriteString16(phrase.PhraseType.ToString());
                    fs.WriteString32(phrase.Text);
                    fs.WriteString16(phrase.Voice);
                    fs.WriteString16(phrase.ActualVoice);
                    fs.WriteString16(phrase.Encoding.ToString());
                    fs.WriteString16(phrase.SampleRate.ToString());
                    fs.WriteInt64(phrase.LastAccessUtc.Ticks);
                    fs.WriteString16(phrase.Path);
                }
            }
        }

        /// <summary>
        /// Deletes all one-time files that are not currently being accessed by NeonSwitch.
        /// </summary>
        private void PurgeOneTime()
        {
            var utcNow = DateTime.UtcNow;

            try
            {
                foreach (var path in Directory.EnumerateFiles(settings.OneTimePhraseFolder, "*.wav", SearchOption.TopDirectoryOnly))
                {
                    try
                    {
                        if (File.GetCreationTimeUtc(path) - utcNow < settings.MaxOneTimePhraseTTL)
                            continue;   // Leave files that were created recently because their
                        // use may still be pending.
                    }
                    catch
                    {
                        // I'm going to ignore these with the assumption that NeonSwitch must have
                        // the file open for reading.
                    }
                }
            }
            catch (Exception e)
            {
                SysLog.LogException(e);
            }
        }

        /// <summary>
        /// Deletes all cached phrase audio files that have not been referenced within
        /// the configured time-to-live.  Also removes any audio files that do not have
        /// an entry in the index and updates the index file on disk.
        /// </summary>
        private void PurgeCache()
        {
            var delList      = new List<Phrase>();
            var orphanList   = new List<string>();
            var minAccessUtc = DateTime.UtcNow - settings.MaxPhraseTTL;

            // Create a list of the phrases whose TTL has expired.

            foreach (var phrase in index.Values)
                if (phrase.LastAccessUtc < minAccessUtc)
                    delList.Add(phrase);

            // Remove the expired phrases from the index and persist the index.;

            foreach (var phrase in delList)
                index.Remove(GetCacheKey(phrase));

            SaveIndex(true);

            // Now look for orphaned files that are not in the index.  Note that
            // we're going to take care to exclude the one-time files and the index.

            var indexedFiles  = new Dictionary<string, bool>(StringComparer.OrdinalIgnoreCase);
            var oneTimePrefix = settings.OneTimePhraseFolder.ToUpper();

            foreach (var phrase in index.Values)
                indexedFiles[Path.GetFileNameWithoutExtension(phrase.Path)] = true;

            foreach (var path in Directory.GetFiles(settings.PhraseCacheFolder, "*.*", SearchOption.AllDirectories))
            {
                if (path.ToUpper().StartsWith(oneTimePrefix))
                    continue;   // Ignore files in the one-time folder.

                if (String.Compare(Path.GetFileName(path), IndexFileName, true) == 0)
                    continue;   // Ignore the index file

                if (!indexedFiles.ContainsKey(Path.GetFileNameWithoutExtension(path)))
                    orphanList.Add(path);
            }

            // Delete the expired and orphaned files on a background thread.

            Helper.EnqueueAction(
                () =>
                {
                    foreach (var phrase in delList)
                    {
                        try
                        {
                            File.Delete(phrase.Path);
                        }
                        catch (Exception e)
                        {

                            SysLog.LogException(e);
                        }
                    }

                    foreach (var path in orphanList)
                    {
                        try
                        {
                            File.Delete(path);
                        }
                        catch (Exception e)
                        {
                            SysLog.LogException(e);
                        }
                    }
                });
        }
    }
}
