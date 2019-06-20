//-----------------------------------------------------------------------------
// FILE:        AudioSource.cs
// CONTRIBUTOR: Jeff Lill
// COPYRIGHT:   Copyright (c) 2005-2015 by Jeffrey Lill.  All rights reserved.
// DESCRIPTION: Describes a NeonSwitch audio source.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

using LillTek.Common;
using LillTek.Telephony.Common;

// $todo(jeff.lill):
//
// I'd like to redo this class at some point.  It's a little weird that the
// leave nodes lose track of what kind of source they are.

namespace LillTek.Telephony.NeonSwitch
{
    /// <summary>
    /// Describes a NeonSwitch audio source.
    /// </summary>
    /// <remarks>
    /// <para>
    /// NeonSwitch provides access to underlying FreeSWITCH audio streams 
    /// via <see cref="AudioSource" />s.  These are really just URIs
    /// that describe an audio source that can be played on a call.
    /// </para>
    /// <para>
    /// The most basic source is an audio file represented by the path to
    /// the file in the file system.  Other sources include the text-to-speech
    /// engine, a tone generator, a silence generator, and music streaming. 
    /// </para>
    /// <para>
    /// Use the static class methods listed below to generate references to
    /// known audio source types:
    /// </para>
    /// <list type="table">
    ///     <item>
    ///         <term><see cref="File" /></term>
    ///         <definition>
    ///         This method returns a reference to an audio file based on its path
    ///         in the file system.
    ///         </definition>
    ///     </item>
    ///     <item>
    ///         <term><see cref="Silence" /></term>
    ///         <definition>
    ///         This method returns a reference to a stream of a specified 
    ///         duration of silence.
    ///         </definition>
    ///     </item>
    ///     <item>
    ///         <term><see cref="Tone" /></term>
    ///         <definition>
    ///         This method returns a reference to a tone stream that plays a
    ///         tone script specified by <see cref="TelephoneTone" />.
    ///         </definition>
    ///     </item>
    ///     <item>
    ///         <term><see cref="Speech(Phrase)" /></term>
    ///         <definition>
    ///         This method returns a reference to an audio stream generated
    ///         by a text-to-speech engine speaking specified text.
    ///         </definition>
    ///     </item>
    ///     <item>
    ///         <term><see cref="LocalStream" /></term>
    ///         <definition>
    ///         This method returns a reference to an local audio stream
    ///         that plays structured audio files.  This is suitable for
    ///         generating hold music, etc.
    ///         </definition>
    ///     </item>
    ///     <item>
    ///         <term><see cref="RemoteStream" /></term>
    ///         <definition>
    ///         This method returns a reference to an audio stream downloaded
    ///         from a remote source such as an Internet radio site.
    ///         </definition>
    ///     </item>
    ///     <item>
    ///         <term><see cref="Uri" /></term>
    ///         <definition>
    ///         This method returns a generalized audio reference from a
    ///         URI <b>scheme</b> and <b>path</b>.  This is used generate references
    ///         to audio stream types that are not directly supported by this class.
    ///         </definition>
    ///     </item>
    ///     <item>
    ///         <term><see cref="Combine" /></term>
    ///         <definition>
    ///         This method provides for composing multiple audio sources into a
    ///         list that will be played sequentially.
    ///         </definition>
    ///     </item>
    /// </list>
    /// <para>
    /// Use <see cref="ToString" /> to get the formatted reference that can be inserted
    /// directly into a low-level LillTek/FreeSwitch command.
    /// </para>
    /// </remarks>
    public class AudioSource : IEnumerable<string>
    {
        //---------------------------------------------------------------------
        // Static members

        /// <summary>
        /// Returns an audio source that plays a local file.
        /// </summary>
        /// <param name="path">Path to the file.</param>
        /// <returns>The audio source reference.</returns>
        /// <exception cref="ArgumentException">Thrown if the expanded file path includes a single quote.</exception>
        /// <remarks>
        /// <note>
        /// The file path may include global variables.  These will be expanded before the method
        /// returns.  The path may also include references to call variables.  These will be expanded
        /// when the audio is played.
        /// </note>
        /// </remarks>
        public static AudioSource File(string path)
        {
            path = Switch.ExpandFilePath(path);

            if (path.Contains('\''))
                throw new ArgumentException(string.Format("[AudioSource] cannot generate a reference to file [{0}] because it contains a single quote.", path), "path");

            return new AudioSource(string.Format("{0}", path));
        }

        /// <summary>
        /// Returns an audio source that plays silence for a period of time.
        /// </summary>
        /// <param name="duration">The audio duration.</param>
        /// <returns>The audio source reference.</returns>
        public static AudioSource Silence(TimeSpan duration)
        {
            int ms = (int)duration.TotalMilliseconds;

            if (ms < 0)
                ms = 0;

            return new AudioSource(string.Format("silence_stream://{0}", ms));
        }

        /// <summary>
        /// Returns an audio source that plays a generated tone stream
        /// </summary>
        /// <param name="tone">The tone definition.</param>
        /// <returns>The audio source reference.</returns>
        public static AudioSource Tone(TelephoneTone tone)
        {
            return new AudioSource(string.Format("tone_stream://{0}", tone));
        }

        /// <summary>
        /// Returns an audio source that speaks a specified phrase.
        /// </summary>
        /// <param name="phrase">The phrase to be spoken.</param>
        /// <returns>The audio source reference.</returns>
        public static AudioSource Speech(Phrase phrase)
        {
            return new AudioSource(string.Format("say:{0}", phrase.Encode()))
            {
                GeneratesSpeech = true
            };
        }

        /// <summary>
        /// Returns an audio source that streams local structured audio
        /// files.  See the FreeSWITCH <a href="http://wiki.freeswitch.org/wiki/Mod_local_stream">local_stream</a>
        /// documentation for more information.
        /// </summary>
        /// <param name="name">The local stream name.</param>
        /// <returns>The audio source reference.</returns>
        public static AudioSource LocalStream(string name)
        {
            return new AudioSource(string.Format("local_stream://{0}", name));
        }

        /// <summary>
        /// Returns an audio source capable of downloading and streaming files received from a server
        /// (e.g. Internet radio).  See the FreeSWITCH <a href="http://wiki.freeswitch.org/wiki/Mod_shout">shout</a> 
        /// documentation for more information.
        /// </summary>
        /// <param name="serverPath">The server source information.</param>
        /// <returns>The audio source reference.</returns>
        public static AudioSource RemoteStream(string serverPath)
        {
            return new AudioSource(string.Format("shout://{0}", serverPath));
        }

        /// <summary>
        /// Returns an audio source URI from a scheme and path.
        /// </summary>
        /// <param name="scheme">The URI scheme.</param>
        /// <param name="path">The URI path.</param>
        /// <returns>The audio source reference.</returns>
        /// <remarks>
        /// <para>
        /// Here's an example:
        /// </para>
        /// <code language="cs">
        /// var source = new AudioSource("local_stream://hold_music");
        /// </code>
        /// </remarks>
        public static AudioSource Uri(string scheme, string path)
        {
            return new AudioSource(string.Format("{0}://{1}", scheme, path));
        }

        /// <summary>
        /// Combines one or more audio sources into a single stream of audio that will
        /// be played sequentially.
        /// </summary>
        /// <param name="sources">The audio sources.</param>
        /// <returns>The composite source reference.</returns>
        /// <exception cref="ArgumentNullException">Thrown if <paramref name="sources" /> is <c>null</c>.</exception>
        /// <exception cref="ArgumentException">Thrown if <paramref name="sources"/> does not have at least one entry.</exception>
        public static AudioSource Combine(params AudioSource[] sources)
        {
            if (sources == null)
                throw new ArgumentNullException("sources");

            var     leaves          = new List<string>(sources.Length);
            bool    generatesSpeech = false;

            foreach (var source in sources)
            {
                if (source == null)
                    throw new ArgumentException("[sources] includes a NULL source.");

                if (source.GeneratesSpeech)
                    generatesSpeech = true;

                if (source.sourceRef != null)
                    leaves.Add(source.sourceRef);
                else
                {
                    foreach (var leaf in source.leaves)
                        leaves.Add(leaf);
                }
            }

            if (leaves.Count == 1)
                return new AudioSource(leaves[0]) { GeneratesSpeech = generatesSpeech };
            else
                return new AudioSource(leaves) { GeneratesSpeech = generatesSpeech };
        }

        /// <summary>
        /// Implements an implict cast of an <see cref="AudioSource" /> to a string suitable 
        /// for embedding into a low-level LilSwitch/FreeSWITCH command.
        /// </summary>
        /// <param name="source"></param>
        /// <returns></returns>
        public static implicit operator string(AudioSource source)
        {
            if (source == null)
                return null;

            return source.ToString();
        }

        //---------------------------------------------------------------------
        // Instance members

        private List<string>    leaves;     // Composite leaf URIs in the order they will be played
        private string          sourceRef;  // Formatted source URI for leaf nodes

        /// <summary>
        /// Internal constructor used by the static methods to initialize
        /// a leaf source.
        /// </summary>
        /// <param name="sourceRef">The formatted source reference.</param>
        internal AudioSource(string sourceRef)
        {
            this.leaves    = null;
            this.sourceRef = sourceRef;
        }

        /// <summary>
        /// Internal constructor used by the static methods to initialize
        /// a composite source.
        /// </summary>
        /// <param name="leaves">The leaf sources in the order they will be played.</param>
        internal AudioSource(List<string> leaves)
        {
            this.leaves    = leaves;
            this.sourceRef = null;
        }

        /// <summary>
        /// Returns <c>true</c> if the source uses a text-to-speech engine.
        /// </summary>
        public bool GeneratesSpeech { get; private set; }

        /// <summary>
        /// Returns <c>true</c> if the audio source is a combination of multiple sources.
        /// </summary>
        public bool IsCompositeSource
        {

            get { return leaves != null; }
        }

        /// <summary>
        /// For non-composite sources, this returns the audio source formatted such 
        /// that it can be embedded into low-level NeonSwitch/FreeSWITCH commands.
        /// For composite sources, this returns a debug string that cannot be
        /// passed to FreeSWITCH.  Use enumerator instead to retrieve the individual
        /// sources.
        /// </summary>
        /// <returns>The formatted audio source.</returns>
        public override string ToString()
        {
            if (sourceRef != null)
                return sourceRef;
            else
                return string.Format("CompositeAudio: [{0}] sources", leaves.Count);
        }

        //---------------------------------------------------------------------
        // IEnumerable<string> implementation

        /// <summary>
        /// Returns the enumerator for composite sources.
        /// </summary>
        /// <returns>The enumerator.</returns>
        /// <exception cref="NotSupportedException">Thrown if this is not a composite source.</exception>
        public IEnumerator<string> GetEnumerator()
        {
            if (leaves != null)
                throw new NotSupportedException("[AudioSource] cannot create an enumerator for non-composite sources.");

            return leaves.GetEnumerator();
        }

        /// <summary>
        /// Returns the enumerator for composite sources.
        /// </summary>
        /// <returns>The enumerator.</returns>
        /// <exception cref="NotSupportedException">Thrown if this is not a composite source.</exception>
        System.Collections.IEnumerator System.Collections.IEnumerable.GetEnumerator()
        {
            return GetEnumerator();
        }
    }
}
