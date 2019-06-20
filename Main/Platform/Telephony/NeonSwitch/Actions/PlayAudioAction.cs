//-----------------------------------------------------------------------------
// FILE:        PlayAudioAction.cs
// CONTRIBUTOR: Jeff Lill
// COPYRIGHT:   Copyright (c) 2005-2015 by Jeffrey Lill.  All rights reserved.
// DESCRIPTION: Plays audio from an AudioSource to a call.

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Xml.Linq;

using FreeSWITCH;
using FreeSWITCH.Native;

using LillTek.Common;
using LillTek.Telephony.Common;

// $todo(jeff.lill):
//
// I'm going to hack the playback of composite audio sources for now by
// rendering a playback command for each source.  This may cause problems:
//
//      * It's possible that DTMF terminators will terminate only
//        the currently playing source and that the others will
//        continue playing in sequence.
//
//      * We'll see start/stop playback events generated for each
//        source rather than a [start] when the first source is started
//        and a [stop] when the last source finishes.
//
// Ultimately, I'd like to add a FreeSWITCH command to implement this
// natively.

namespace LillTek.Telephony.NeonSwitch
{
    /// <summary>
    /// Plays audio from an <see cref="AudioSource" /> to a call.
    /// </summary>
    /// <remarks>
    /// <para>
    /// The <see cref="AudioSource" /> class can be used obtain audio from files, tone and silence generators,
    /// text-to-speech engines, and local or remote audio streams as well as from composite sources.
    /// </para>
    /// </remarks>
    public class PlayAudioAction : SwitchAction
    {
        private AudioSource     source;
        private string          stopDtmf;

        /// <summary>
        /// Constructs a playback action for the current call on an executing dialplan.
        /// </summary>
        /// <param name="source">The audio source.</param>
        /// <exception cref="ArgumentNullException">Thrown if <paramref name="source" /> is <c>null</c>.</exception>
        public PlayAudioAction(AudioSource source)
        {
            if (source == null)
                throw new ArgumentNullException("source");

            this.source         = source;
            this.stopDtmf       = null;
            this.EventVariables = new ArgCollection();
        }

        /// <summary>
        /// Constructs a playback action for a specific call.
        /// </summary>
        /// <param name="callID">The target callID.</param>
        /// <param name="source">The audio source.</param>
        /// <exception cref="ArgumentNullException">Thrown if <paramref name="source" /> is <c>null</c>.</exception>
        public PlayAudioAction(Guid callID, AudioSource source)
            : this(source)
        {
            this.CallID = callID;
        }

        /// <summary>
        /// Specifies zero or more DTMF digits what will stop the file playback
        /// (may also be set to <c>null</c>).
        /// </summary>
        public string StopDtmf
        {
            get { return stopDtmf; }

            set
            {
                if (value == null)
                {

                    stopDtmf = null;
                    return;
                }

                stopDtmf = Dtmf.Validate(value);
            }
        }

        /// <summary>
        /// Specifies variables to be included in <see cref="SwitchEventCode.PlaybackStart" />
        /// and <see cref="SwitchEventCode.PlaybackStop" /> events generated from this action.
        /// </summary>
        public ArgCollection EventVariables { get; private set; }

        /// <summary>
        /// Renders the high-level switch action instance into zero or more <see cref="SwitchExecuteAction" />
        /// instances and then adds these to the <see cref="ActionRenderingContext" />.<see cref="ActionRenderingContext.Actions" />
        /// collection.
        /// </summary>
        /// <param name="context">The action rendering context.</param>
        /// <remarks>
        /// <note>
        /// It is perfectly reasonable for an action to render no actions to the
        /// context or to render multiple actions based on its properties.
        /// </note>
        /// </remarks>
        public override void Render(ActionRenderingContext context)
        {
            if (!context.IsDialplan)
                CheckCallID();

            // Set the DTMF terminators call variable.

            var terminators = !string.IsNullOrWhiteSpace(stopDtmf) ? stopDtmf : "none";

            if (context.IsDialplan)
                new SetVariableAction("playback_terminators", terminators).Render(context);
            else
                new SetVariableAction(CallID, "playback_terminators", terminators).Render(context);

            // $hack(jeff.lill):
            //
            // Hack the generation of composite sources for now.

            if (source.IsCompositeSource)
            {
                foreach (var leaf in source)
                {
                    var adjusted = HandleSpeech(leaf);

                    if (context.IsDialplan)
                        context.Actions.Add(new SwitchExecuteAction("playback", "{0}", adjusted));
                    else
                        context.Actions.Add(new SwitchExecuteAction(CallID, "playback", "{0}", adjusted));
                }

                return;
            }

            // Render the playback action for the case where there are no event
            // variables specifed.

            if (EventVariables.Count == 0)
            {
                if (context.IsDialplan)
                    context.Actions.Add(new SwitchExecuteAction("playback", "{0}", HandleSpeech(source.ToString())));
                else
                    context.Actions.Add(new SwitchExecuteAction(CallID, "playback", "{0}", HandleSpeech(source.ToString())));

                return;
            }

            // Render the playback action with event variables.

            var sb = new StringBuilder();

            sb.Append(HandleSpeech(source.ToString()));
            sb.Append('{');

            foreach (var key in EventVariables)
                sb.AppendFormat("{0}={1},", key, EventVariables[key]);

            if (sb.Length > 0)
                sb.Length--;

            sb.Append('}');

            if (context.IsDialplan)
                context.Actions.Add(new SwitchExecuteAction("playback", "{0}", sb));
            else
                context.Actions.Add(new SwitchExecuteAction(CallID, "playback", "{0}", sb));
        }

        /// <summary>
        /// This method handles text-to-speech audio source references by sending a <b>speak</b> command
        /// to the NeonSwitch core application to generate the audio file or obtained its cached
        /// location, and then return the path to the file. 
        /// </summary>
        /// <param name="source">The audio source reference.</param>
        /// <returns>
        /// The path to the audio file for speech audio references and the unchanged source reference
        /// for all other audio source.
        /// </returns>
        private string HandleSpeech(string source)
        {
            // $hack(jeff.lill):
            //
            // This is a bit of a hack since it has hardcoded knowledge of the format of the
            // speech audio source reference.

            if (!source.StartsWith("say:"))
                return source;      // Not speech

            // The text after "say:" is the encoded phrase.  We're going to execute a NeonSwitch speak
            // command that will render the phrase into an audio file (or perhaps an already cached
            // file) and then return the file path we get back from the NeonSwitch core.

            return Switch.ExecuteCore("speak", source.Substring("say:".Length));
        }
    }
}
