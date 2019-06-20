//-----------------------------------------------------------------------------
// FILE:        Phrase.cs
// CONTRIBUTOR: Jeff Lill
// COPYRIGHT:   Copyright (c) 2005-2015 by Jeffrey Lill.  All rights reserved.
// DESCRIPTION: Describes a spoken phrase or utterance.

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;

using FreeSWITCH;
using FreeSWITCH.Native;

using LillTek.Common;

// $todo(jeff.lill):
//
// Need to support SSML phrases.

namespace LillTek.Telephony.Common
{
    /// <summary>
    /// Describes a spoken phrase or utterance.
    /// </summary>
    public class Phrase
    {
        //---------------------------------------------------------------------
        // Static methods.

        /// <summary>
        /// Returns a phrase suitable for playing over a telephone that speaks the 
        /// specified text using the current voice.
        /// </summary>
        /// <param name="text">The text to be spoken.</param>
        /// <returns>The phrase.</returns>
        /// <exception cref="ArgumentNullException">Thrown if <paramref name="text"/> is <b>null.</b></exception>
        public static Phrase PhoneText(string text)
        {
            return new Phrase(PhraseType.Text, null, TtsEncoding.Pcm8, TtsSampleRate.KHz_8000, text);
        }

        /// <summary>
        /// Returns a phrase suitable for playing over a telephone that speaks the 
        /// specified formatted using the current voice.
        /// </summary>
        /// <param name="format">The format string.</param>
        /// <param name="args">The arguments.</param>
        /// <returns>The phrase.</returns>
        /// <exception cref="ArgumentNullException">Thrown if <paramref name="format"/> is <b>null.</b></exception>
        public static Phrase PhoneText(string format, params object[] args)
        {
            if (format == null)
                throw new ArgumentNullException("format");

            return new Phrase(PhraseType.Text, null, TtsEncoding.Pcm8, TtsSampleRate.KHz_8000, string.Format(format, args));
        }

        /// <summary>
        /// Returns a phrase suitable for playing over a telephone that speaks the 
        /// specified text and voice.
        /// </summary>
        /// <param name="voice">The voice to be used or <c>null</c> for the current or default voice.</param>
        /// <param name="text">The text to be spoken.</param>
        /// <returns>The phrase.</returns>
        /// <exception cref="ArgumentNullException">Thrown if <paramref name="text"/> is <b>null.</b></exception>
        public static Phrase PhoneVoiceText(string voice, string text)
        {
            return new Phrase(PhraseType.Text, voice, TtsEncoding.Pcm8, TtsSampleRate.KHz_8000, text);
        }

        /// <summary>
        /// Returns a phrase suitable for playing over a telephone that speaks the 
        /// formatted text and voice.
        /// </summary>
        /// <param name="voice">The voice to be used or <c>null</c> for the current or default voice.</param>
        /// <param name="format">The format string.</param>
        /// <param name="args">The arguments.</param>
        /// <returns>The phrase.</returns>
        /// <exception cref="ArgumentNullException">Thrown if <paramref name="format"/> is <b>null.</b></exception>
        public static Phrase PhoneVoiceText(string voice, string format, params object[] args)
        {
            if (format == null)
                throw new ArgumentNullException("format");

            return new Phrase(PhraseType.Text, voice, TtsEncoding.Pcm8, TtsSampleRate.KHz_8000, string.Format(format, args));
        }

        /// <summary>
        /// Returns a phrase suitable for playing over a telephone that speaks the 
        /// specified SSML prompt and the default voice.
        /// </summary>
        /// <param name="text">The text to be spoken.</param>
        /// <returns>The phrase.</returns>
        /// <exception cref="ArgumentNullException">Thrown if <paramref name="text"/> is <b>null.</b></exception>
        public static Phrase PhoneVoiceSsml(string text)
        {
            return new Phrase(PhraseType.Ssml, null, TtsEncoding.Pcm8, TtsSampleRate.KHz_8000, text);
        }

        /// <summary>
        /// Returns a phrase suitable for playing over a telephone that speaks the 
        /// specified SSML prompt and voice.
        /// </summary>
        /// <param name="voice">The voice to be used or <c>null</c> for the current or default voice.</param>
        /// <param name="text">The text to be spoken.</param>
        /// <returns>The phrase.</returns>
        /// <exception cref="ArgumentNullException">Thrown if <paramref name="text"/> is <b>null.</b></exception>
        public static Phrase PhoneVoiceSsml(string voice, string text)
        {
            return new Phrase(PhraseType.Ssml, voice, TtsEncoding.Pcm8, TtsSampleRate.KHz_8000, text);
        }

        /// <summary>
        /// Decodes a <see cref="Phrase" /> encoded as a string by the <see cref="Encode" /> method.
        /// </summary>
        /// <param name="encoded">The encoded phrase.</param>
        /// <returns>The decoded phrase.</returns>
        /// <exception cref="ArgumentNullException">Thrown if <paramref name="encoded" /> is <c>null</c>.</exception>
        /// <exception cref="ArgumentException">Thrown if the encoded string is not formatted correctly.</exception>
        public static Phrase Decode(string encoded)
        {
            if (encoded == null)
                throw new ArgumentNullException("encoded");

            try
            {
                Phrase          phrase = new Phrase();
                ArgCollection   args;
                int             p;

                p = encoded.IndexOf(";>>");     // This marks the end of the headers and the beginning of the 

                args = new ArgCollection(encoded.Substring(0, p + 1), ':', ';');

                phrase.PhraseType = args.Get<PhraseType>("Type", PhraseType.Unknown);
                if (phrase.PhraseType == PhraseType.Unknown)
                    throw new Exception();

                phrase.Voice = args.Get("Voice");
                if (string.IsNullOrWhiteSpace(phrase.Voice))
                    phrase.Voice = null;

                phrase.ActualVoice = args.Get("ActualVoice");
                if (string.IsNullOrWhiteSpace(phrase.ActualVoice))
                    phrase.ActualVoice = null;

                phrase.Encoding   = args.Get<TtsEncoding>("Encoding", TtsEncoding.Pcm8);
                phrase.SampleRate = args.Get<TtsSampleRate>("SampleRate", TtsSampleRate.KHz_8000);
                phrase.IsOneTime  = args.Get("IsOneTime", false);
                phrase.Text       = Helper.UrlDecode(encoded.Substring(p + 3));

                return phrase;
            }
            catch
            {
                throw new ArgumentException("Invalid encoded phrase.", "encoded");
            }
        }

        //---------------------------------------------------------------------
        // Instance methods.

        /// <summary>
        /// Private constructor.
        /// </summary>
        private Phrase()
        {
        }

        /// <summary>
        /// Constructs a textual phrase.
        /// </summary>
        /// <param name="phraseType">Identifies the phrase type.</param>
        /// <param name="voice">Identifies the voice.</param>
        /// <param name="encoding">Specifies the audio encoding.</param>
        /// <param name="sampleRate">Specfies the audio sampling rate.</param>
        /// <param name="text">The text or SSML to be spoken.</param>
        /// <exception cref="ArgumentNullException">Thrown if <paramref name="text"/> is <b>null.</b></exception>
        /// <remarks>
        /// <note>
        /// It's often more convenient to use one of the static methods.
        /// </note>
        /// </remarks>
        public Phrase(PhraseType phraseType, string voice, TtsEncoding encoding, TtsSampleRate sampleRate, string text)
        {
            if (text == null)
                throw new ArgumentNullException("text");

            this.PhraseType = phraseType;
            this.IsOneTime  = false;
            this.Voice      = voice;
            this.Encoding   = encoding;
            this.SampleRate = sampleRate;
            this.Text       = text;
        }

        /// <summary>
        /// Returns a clone of the phrase.
        /// </summary>
        /// <returns>The clone.</returns>
        public Phrase Clone()
        {
            return new Phrase()
            {
                PhraseType    = this.PhraseType,
                IsOneTime     = this.IsOneTime,
                Voice         = this.Voice,
                ActualVoice   = this.ActualVoice,
                Encoding      = this.Encoding,
                SampleRate    = this.SampleRate,
                Text          = this.Text,
                LastAccessUtc = this.LastAccessUtc,
                Path          = this.Path,
            };
        }

        /// <summary>
        /// Returns a clone of the phrase with all properties copied except
        /// for the voice, which will be replaced with the voice passed.
        /// </summary>
        /// <returns>The clone.</returns>
        public Phrase Clone(string voice)
        {
            var clone = this.Clone();

            clone.Voice = voice;
            clone.ActualVoice = null;

            return clone;
        }

        /// <summary>
        /// Indicates whether the phrase is simple text or Speech Synthesis Markup Language (SSML).
        /// </summary>
        public PhraseType PhraseType { get; private set; }

        /// <summary>
        /// Returns the desired voice to be used or <c>null</c> to use the current or default voice.
        /// </summary>
        public string Voice { get; private set; }

        /// <summary>
        /// The actual voice used to render the phrase or <c>null</c> if there are no installed
        /// voices on the current machine.
        /// </summary>
        public string ActualVoice { get; internal set; }

        /// <summary>
        /// Returns the audio encoding.
        /// </summary>
        public TtsEncoding Encoding { get; private set; }

        /// <summary>
        /// Returns the audio sampling rate.
        /// </summary>
        public TtsSampleRate SampleRate { get; private set; }

        /// <summary>
        /// Returns the text to be spoken.
        /// </summary>
        public string Text { get; private set; }

        /// <summary>
        /// Indicates whether the phrase will only be used once and must not be cached.  This defaults to <c>false</c>.
        /// </summary>
        /// <remarks>
        /// Applications will typical set <see cref="IsOneTime"/>=<c>true</c> for phrases that are so unique
        /// that they are highly unlikely to be repeated 
        /// </remarks>
        public bool IsOneTime { get; set; }

        /// <summary>
        /// Indicates the last time the phrase was used (UTC).  This defaults to <see cref="DateTime"/>.<see cref="DateTime.Now"/>.
        /// Used by the phrase cache.
        /// </summary>
        internal DateTime LastAccessUtc { get; set; }

        /// <summary>
        /// The absolute path to the phrase audio file on the file system. This defaults to <c>null</c>.
        /// Used by the phrase cache.
        /// </summary>
        internal string Path { get; set; }

        /// <summary>
        /// Serializes the phrase into a string form suitable for encoding into a Switch command argument.
        /// The value returned is also suitable for use as a cache key.
        /// </summary>
        /// <returns>The encoded phrase.</returns>
        internal string Encode()
        {
            var sb = new StringBuilder();

            sb.AppendFormat("Type:{0};", PhraseType);
            sb.AppendFormat("Voice:{0};", Voice ?? string.Empty);
            sb.AppendFormat("ActualVoice:{0};", ActualVoice ?? string.Empty);
            sb.AppendFormat("Encoding:{0};", Encoding);
            sb.AppendFormat("SampleRate:{0};", SampleRate);
            sb.AppendFormat("IsOneTime:{0};", IsOneTime);
            sb.AppendFormat(">>{0}", Helper.UrlEncode(Text));

            return sb.ToString();
        }
    }
}
