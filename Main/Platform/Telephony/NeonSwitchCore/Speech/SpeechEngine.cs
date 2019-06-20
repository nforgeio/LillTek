//-----------------------------------------------------------------------------
// FILE:        SpeechEngine.cs
// CONTRIBUTOR: Jeff Lill
// COPYRIGHT:   Copyright (c) 2005-2015 by Jeffrey Lill.  All rights reserved.
// DESCRIPTION: Provides global access to the speech services.

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Threading;

using Microsoft.Speech;
using Microsoft.Speech.AudioFormat;
using Microsoft.Speech.Synthesis;

using FreeSWITCH;
using FreeSWITCH.Native;

using LillTek.Advanced;
using LillTek.Common;
using LillTek.Telephony.Common;
using LillTek.Telephony.NeonSwitch;

using Switch = LillTek.Telephony.NeonSwitch.Switch;

// $todo(jeff.lill):
//
// Implement some performance counters, including the total number
// of TTS operations being performed in parallel.

// $todo(jeff.lill)
//
// The [Microsoft Anna] voice is not working on my Workstation.  
// Windows reports that it is installed and enabled, but then fails
// to load it into a speech synthesis instance.  This might be a
// 64/32-bit issue.

// $todo(jeff.lill)
//
// Need to look at how I want to handle SSML.  SSML can specify voices
// and I'd really like to be able to use the clean voice names rather
// than the crazy MSFT Speech Platform names.  I may need to process
// SSML files to replace friendly names with internal names before
// submission to the TTS engine.

namespace LillTek.Telephony.NeonSwitchCore
{
    /// <summary>
    /// Provides global access to the speech services.
    /// </summary>
    /// <threadsafety static="true" />
    public static class SpeechEngine
    {
        private static object                       syncLock  = new object();
        private static bool                         isRunning = false;
        private static SpeechEngineSettings         settings;           // Engine settings
        private static PhraseCache                  cache;              // Phrase audio cache
        private static string                       noVoicesPath;       // Path to no installed voices audio message file
        private static string                       synthErrorPath;     // Path to speech synth error audio message file
        private static SpeechAudioFormatInfo        format_8000KHz;     // Audio formats
        private static SpeechAudioFormatInfo        format_11025KHz;    // Audio formats
        private static SpeechAudioFormatInfo        format_16000KHz;    // Audio formats
        private static Dictionary<string, string>   voiceNameMap;       // Maps friendly voice names to the internal name

        /// <summary>
        /// Starts the engine if it is not already running.
        /// </summary>
        /// <param name="settings">The engine settings.</param>
        /// <exception cref="ArgumentNullException">Thrown if <paramref name="settings"/> is <c>null</c>.</exception>
        public static void Start(SpeechEngineSettings settings)
        {
            if (settings == null)
                throw new ArgumentNullException("settings");

            lock (syncLock)
            {
                if (SpeechEngine.isRunning)
                    return;

                SpeechEngine.settings  = settings;
                SpeechEngine.cache     = new PhraseCache(settings);
                SpeechEngine.isRunning = true;

                // Create the default audio formats.

                format_8000KHz  = new SpeechAudioFormatInfo(8000, AudioBitsPerSample.Eight, AudioChannel.Mono);
                format_11025KHz = new SpeechAudioFormatInfo(11025, AudioBitsPerSample.Eight, AudioChannel.Mono);
                format_16000KHz = new SpeechAudioFormatInfo(16000, AudioBitsPerSample.Eight, AudioChannel.Mono);

                // Get the fully qualified paths to the error files.

                noVoicesPath   = Path.Combine(CoreApp.InstallPath, "Audio", "NoVoicesError.wav");
                synthErrorPath = Path.Combine(CoreApp.InstallPath, "Audio", "SpeechSynthError.wav");

                // Enumerate the installed voices and select the default voice.
                //
                // Note: The Microsoft Speech Platform voices have really clunky names like:
                //
                //    "Microsoft Server Speech Text to Speech Voice (en-AU, Hayley)"
                //
                // I'm going to simplify these to be just "Microsoft <name>" and maintain
                // a table that maps back to the original name.

                voiceNameMap = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

                using (var synth = new SpeechSynthesizer())
                {
                    var voices = new Dictionary<string, VoiceInfo>(StringComparer.OrdinalIgnoreCase);

                    foreach (var voice in synth.GetInstalledVoices())
                    {
                        var voiceName = voice.VoiceInfo.Name;

                        if (!voice.Enabled)
                            continue;

                        // $hack(jeff.lill):
                        //
                        // Make sure that the voice can actually be used.  I've run into
                        // situations where [Microsoft Anna] was present and enabled but
                        // could not be selected.  I believe this may be a 64-bit issue
                        // or perhaps installing Cepstral voices messes with Anna.

                        try
                        {
                            synth.SelectVoice(voice.VoiceInfo.Name);
                        }
                        catch
                        {
                            continue;
                        }

                        if (voiceName.StartsWith("Microsoft Server Speech Text to Speech Voice ("))
                        {
                            int p = voiceName.IndexOf(',');

                            if (p != -1)
                            {
                                voiceName = voiceName.Substring(p + 1);
                                voiceName = "Microsoft " + voiceName.Replace(")", string.Empty).Trim();

                                voiceNameMap[voiceName] = voice.VoiceInfo.Name;
                            }
                        }

                        voices.Add(voiceName, voice.VoiceInfo);
                    }

                    SpeechEngine.InstalledVoices  = voices.ToReadOnly();
                    SpeechEngine.DefaultVoice     = null;
                    SpeechEngine.DefaultVoiceInfo = null;

                    // First see if the desired default voice exists.

                    if (!string.IsNullOrWhiteSpace(settings.DefaultVoice) && String.Compare(settings.DefaultVoice, "auto") != 0)
                    {
                        VoiceInfo voiceInfo;

                        if (voices.TryGetValue(settings.DefaultVoice, out voiceInfo))
                            SpeechEngine.DefaultVoice = voiceInfo.Name;
                        else
                            SysLog.LogWarning("[SpeechEngine] was not able to locate the requested default voice [{0}].  Another voice will be selected automatically.", settings.DefaultVoice);
                    }

                    // If not look for an alternative 

                    if (SpeechEngine.DefaultVoice == null)
                    {
                        if (voices.ContainsKey("Microsoft Helen"))
                            SpeechEngine.DefaultVoice = "Microsoft Helen";
                        else if (voices.ContainsKey("Microsoft Anna"))
                            SpeechEngine.DefaultVoice = "Microsoft Anna";
                        else
                        {
                            SysLog.LogWarning("[SpeechEngine] was not able to locate the [Microsoft Anna] voice.");

                            var v = voices.Keys.FirstOrDefault();

                            if (v == null)
                            {
                                SpeechEngine.DefaultVoice = null;
                                SysLog.LogError("[SpeechEngine] was not able to locate any speech synthesis voices.  Speech synthesis will be disabled.");
                            }
                            else
                                SpeechEngine.DefaultVoice = v;
                        }
                    }

                    if (SpeechEngine.DefaultVoice != null)
                        SpeechEngine.DefaultVoiceInfo = SpeechEngine.InstalledVoices[GetVoice(SpeechEngine.DefaultVoice)];
                }
            }
        }

        /// <summary>
        /// Stops the engine if it is running.
        /// </summary>
        public static void Stop()
        {
            lock (syncLock)
            {
                if (!isRunning)
                    return;

                cache.Stop();
                cache = null;

                isRunning = false;
            }
        }

        /// <summary>
        /// <b>Unit Test Only:</b> Returns the engine's phrase cache.
        /// </summary>
        internal static PhraseCache PhraseCache
        {
            get { return cache; }
        }

        /// <summary>
        /// Returns a read-only dictionary of the installed and enabled speech syntheses voices 
        /// keyed by name.
        /// </summary>
        public static IDictionary<string, VoiceInfo> InstalledVoices { get; private set; }

        /// <summary>
        /// Returns the fully qualified path to the error audio file.
        /// </summary>
        public static string ErrorAudioPath
        {
            get { return synthErrorPath; }
        }

        /// <summary>
        /// Returns the name of the default voice used by the engine for synthesizing
        /// speech if no other voice is specified or <c>null</c> if no voices are
        /// installed or enabled.
        /// </summary>
        public static string DefaultVoice { get; private set; }

        /// <summary>
        /// Returns the information for the default voice or <c>null</c> if voices are
        /// installed or enabled.
        /// </summary>
        public static VoiceInfo DefaultVoiceInfo { get; private set; }

        /// <summary>
        /// Returns the name of the actual voice to be used to speak.
        /// </summary>
        /// <param name="voice">The desired voice name.</param>
        /// <returns>The actual voice to be used or <c>null</c> if there are no installed or enabled voices.</returns>
        /// <remarks>
        /// <note>
        /// This method returns the default voice if <c>null</c> is passed or the desired voice
        /// does not exist.
        /// </note>
        /// </remarks>
        internal static string GetVoice(string voice)
        {
            if (voice == null)
                return SpeechEngine.DefaultVoice;
            else if (SpeechEngine.InstalledVoices.ContainsKey(voice))
                return voice;

            return SpeechEngine.DefaultVoice;
        }

        /// <summary>
        /// Maps a friendly voice name into an internal voice name.
        /// </summary>
        /// <param name="voice">The friendly voice name.</param>
        /// <returns>The internal voice name.</returns>
        private static string GetInternalVoice(string voice)
        {
            string internalVoice;

            if (voice == null)
                voice = SpeechEngine.DefaultVoice;

            if (voiceNameMap.TryGetValue(voice, out internalVoice))
                return internalVoice;   // Mapped a friendly voice name into an internal name.
            else if (SpeechEngine.InstalledVoices.ContainsKey(voice))
                return voice;
            else
                return GetInternalVoice(SpeechEngine.DefaultVoice);
        }

        /// <summary>
        /// Synthesizes speech audio from the phrase passed and renders it to a file.
        /// </summary>
        /// <param name="phrase">Describes the phrase to be spoken.</param>
        /// <returns>The fully qualified path to the generated audio file.</returns>
        /// <exception cref="ArgumentNullException">Thrown if <paramref name="phrase"/> is <c>null</c>.</exception>
        /// <exception cref="InvalidOperationException">Thrown if the SpeechEngine is stopped.</exception>
        /// <remarks>
        /// This method may actually return the path to a previously synthesized 
        /// file for the phrase in a local cache.
        /// </remarks>
        public static string SpeakToFile(Phrase phrase)
        {
            Phrase                  existing;
            SpeechAudioFormatInfo   audioFormat;

            if (phrase == null)
                throw new ArgumentNullException("phrase");

            // Check to see whether we've already cached this phrase.

            lock (syncLock)
            {

                if (!isRunning)
                    throw new InvalidOperationException("[SpeechEngine] is not running.");

                existing = cache.FindPhrase(phrase);
            }

            if (existing != null)
                return existing.Path;

            if (SpeechEngine.DefaultVoice == null)
                return noVoicesPath;    // Returns the path to an audio message that says there are no voices.

            // Generate the audio file.

            using (var synth = new SpeechSynthesizer())
            {
                switch (phrase.SampleRate)
                {
                    case TtsSampleRate.KHz_8000:

                        audioFormat = format_8000KHz;
                        break;

                    case TtsSampleRate.KHz_11025:

                        audioFormat = format_11025KHz;
                        break;

                    case TtsSampleRate.KHz_16000:

                        audioFormat = format_16000KHz;
                        break;

                    default:

                        SysLog.LogWarning("[SpeechEngine] encountered the unexpected audio sample rate [{0}].  Using [{1}] instead.", phrase.SampleRate, TtsSampleRate.KHz_8000);
                        audioFormat = format_8000KHz;
                        break;
                }

                if (phrase.IsOneTime)
                    phrase.Path = cache.GetNextOneTimePath(phrase);
                else
                    phrase.Path = cache.GetNextPhrasePath(phrase);

                Helper.CreateFileTree(phrase.Path);

                try
                {
                    synth.SelectVoice(GetInternalVoice(phrase.Voice));
                    synth.SetOutputToWaveFile(phrase.Path, audioFormat);
                    synth.Speak(phrase.Text);

                    if (!phrase.IsOneTime)
                        cache.AddPhrase(phrase);
                }
                catch (Exception e)
                {
                    SysLog.LogException(e);
                    return synthErrorPath;  // Returns the path to an audio message that says there was a speech synthesis error.
                }
            }

            return phrase.Path;
        }

        /// <summary>
        /// This must be called periodically manage phrase purging.
        /// </summary>
        public static void OnBkTask()
        {
            try
            {
                lock (syncLock)
                {
                    if (!isRunning)
                        return;

                    cache.OnBkTask();
                }
            }
            catch (Exception e)
            {
                SysLog.LogException(e);
            }
        }
    }
}
