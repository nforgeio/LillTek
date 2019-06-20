//-----------------------------------------------------------------------------
// FILE:        TelephoneTone.cs
// CONTRIBUTOR: Jeff Lill
// COPYRIGHT:   Copyright (c) 2005-2015 by Jeffrey Lill.  All rights reserved.
// DESCRIPTION: Describes tones that can be played on a call.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

using LillTek.Common;

// $todo(jeff.lill):
//
// It's possible that user applications might be able to specify very high loop
// counts that generate huge tone sample buffers.  I may need to figure out
// a way to constrain the size of these buffers so that applications cannot
// impact server performance or verify that FreeSWITCH is already doing this.

namespace LillTek.Telephony.Common
{
    /// <summary>
    /// Describes tones that can be played on a call.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Telephone tones are specified by textual tone scripts as defined by FreeSWITCH
    /// <a href="http://wiki.freeswitch.org/wiki/TGML">TGML</a>.  These scripts consist
    /// one or more commands separated by semi-colons.  NeonSwitch supports the following
    /// tone commands:
    /// </para>
    /// <list type="table">
    ///     <item>
    ///         <term><b><b>DTMF Tones</b></b></term>
    ///         <description>
    ///         You can specify one or more standard DTMF tones by simply including
    ///         a digit <b>0-9</b>, <b>*</b>, <b>#</b>, or menu selection keys <b>A-D</b>.
    ///         </description>
    ///     </item>
    ///     <item>
    ///         <term><b>c=&lt;int&gt;</b></term>
    ///         <description>
    ///         Sets the number of channels to an integer value.
    ///         </description>
    ///     </item>
    ///     <item>
    ///         <term><b>r=&lt;int&gt;</b></term>
    ///         <description>
    ///         Sets the sample rate in samples per second.
    ///         </description>
    ///     </item>
    ///     <item>
    ///         <term><b>d=&lt;int&gt;</b></term>
    ///         <description>
    ///         Sets the default tone duration in milliseconds.
    ///         </description>
    ///     </item>
    ///     <item>
    ///         <term><b>v=&lt;float&gt;</b></term>
    ///         <description>
    ///         Sets the default volume in decibels (-63db to 0.0db).
    ///         </description>
    ///     </item>
    ///     <item>
    ///         <term><b>&gt;=&lt;int&gt;</b></term>
    ///         <description>
    ///         Sets the number of milliseconds per volume increase step.
    ///         </description>
    ///     </item>
    ///     <item>
    ///         <term><b>&lt;=&lt;int&gt;</b></term>
    ///         <description>
    ///         Sets the number of milliseconds per volume decrease step.
    ///         </description>
    ///     </item>
    ///     <item>
    ///         <term><b>+=&lt;float&gt;</b></term>
    ///         <description>
    ///         Sets the volume increase or decrease in decibels for each 
    ///         <b>&gt;=</b> or <b>&lt;=</b> step.
    ///         </description>
    ///     </item>
    ///     <item>
    ///         <term><b>w=&lt;int&gt;</b></term>
    ///         <description>
    ///         Default silence in milliseconds after each tone.
    ///         </description>
    ///     </item>
    ///     <item>
    ///         <term><b>l=&lt;int&gt;</b></term>
    ///         <description>
    ///         The number of times to repeat each tone in the script.
    ///         </description>
    ///     </item>
    ///     <item>
    ///         <term><b>L=&lt;int&gt;</b></term>
    ///         <description>
    ///         The number of times to repeat the entire script.
    ///         </description>
    ///     </item>
    ///     <item>
    ///         <term><b>%(d,w,f0,f1,...)</b></term>
    ///         <description>
    ///         A generic tone described by a <b>d</b>, the tone duration in milliseconds,
    ///         <b>w</b> the wait time between tones in milliseconds, and then a list of
    ///         tone frequencies in Hz.
    ///         </description>
    ///     </item>
    /// </list>
    /// <para>
    /// Here are some examples:
    /// </para>
    /// <list type="table">
    ///     <item>
    ///         <term>Dial Information</term>
    ///         <description>
    ///         <b>d=300;w=200;2065551212</b>: Sets tone duration to 300ms, wait time to 200ms and then dials the 
    ///         phone number as DTMF digits.
    ///         </description>
    ///     </item>
    ///     <item>
    ///         <term>US Ring Tone</term>
    ///         <description>
    ///         <b>%(2000,4000,440,480)</b>: Play 440Hz and 480Hz tones with a duration of 2000ms and a wait of 4000ms
    ///         </description>
    ///     </item>
    ///     <item>
    ///         <term>Busy Tone</term>
    ///         <description>
    ///         <b>%(500,500,480,620)</b>: Play 480Hz and 620Hz tones with a duration and wait of 500ms.
    ///         </description>
    ///     </item>
    ///     <item>
    ///         <term>Repeated Busy</term>
    ///         <description>
    ///         <b>L=5;%(500,500,480,620)</b>: Play the busy tone five times.
    ///         </description>
    ///     </item>
    ///     <item>
    ///         <term>Not in Service</term>
    ///         <description>
    ///         <b>%(274,0,913.8);%(274,0,1370.6);%(380,0,1776.7)</b>: 913.8Hz for 274ms with no wait, 1370.6Hz 
    ///         for 274ms with no wait, 1776.7Hz for 380ms with no wait 
    ///         </description>
    ///     </item>
    /// </list>
    /// <para>
    /// This class also defines script constants for several common tones.
    /// </para>
    /// </remarks>
    public class TelephoneTone
    {
        //---------------------------------------------------------------------
        // Static members.

        /// <summary>
        /// Implicitly casts a string into a <see cref="TelephoneTone" /> instance.
        /// </summary>
        /// <param name="script">The tone script.</param>
        /// <returns>The <see cref="TelephoneTone" />.</returns>
        public static implicit operator TelephoneTone(string script)
        {
            return new TelephoneTone(script);
        }

        /// <summary>
        /// United States ring tone.
        /// </summary>
        public const string USRing = "%(2000,4000,440,480)";

        /// <summary>
        /// Calling card bong tone.
        /// </summary>
        public const string CallingCard = "v=-7;%(100,0,941.0,1477.0);v=-7;>=2;+=.1;%(1400,0,350,440)";

        /// <summary>
        /// Phone not in service (fast busy).
        /// </summary>
        public const string NotInService = "%(274,0,913.8);%(274,0,1370.6);%(380,0,1776.7);%(0,350,0)";

        /// <summary>
        /// Call waiting tome.
        /// </summary>
        public const string CallerWaiting = "%(300,10000,440);L=2";

        /// <summary>
        /// Distinctive ring.
        /// </summary>
        public const string DistinctiveRing = "%(100,100,440);%(100,0,440)";

        /// <summary>
        /// Busy tone.
        /// </summary>
        public const string Busy = "%(500,500,480,620);%(0,250,0)";

        /// <summary>
        /// PSTN dial tone (10 seconds long).
        /// </summary>
        public const string DialTone = "%(10000,0,350,440)";

        /// <summary>
        /// PBX dial tone (10 seconds long).
        /// </summary>
        public const string PbxDialTone = "%(10000,0,250,400)";

        //---------------------------------------------------------------------
        // Instance members.

        /// <summary>
        /// Returns the tone script.
        /// </summary>
        public string Script { get; private set; }

        /// <summary>
        /// Constructs a tone from a script.
        /// </summary>
        /// <param name="script">The tone script.</param>
        /// <remarks>
        /// <note>
        /// See the <see cref="TelephoneTone"/>  class reference for information
        /// on the format of the <paramref name="script" /> string.
        /// </note>
        /// </remarks>
        public TelephoneTone(string script)
        {
            this.Script = script;
        }

        /// <summary>
        /// Constructs a tone from a script and then adds a command to loop
        /// the tone.
        /// </summary>
        /// <param name="script">The tone script.</param>
        /// <param name="count">The number of times to play the script.</param>
        /// <exception cref="ArgumentException">Thrown if <paramref name="count" /> is less than or equal to zero.</exception>
        /// <remarks>
        /// <note>
        /// See the <see cref="TelephoneTone"/>  class reference for information
        /// on the format of the <paramref name="script" /> string.
        /// </note>
        /// <note>
        /// This method does nothing if the script already contains a <b>L=&lt;int&gt;</b>
        /// command.
        /// </note>
        /// </remarks>
        public TelephoneTone(string script, int count)
        {
            this.Script = script;
            Loop(count);
        }

        /// <summary>
        /// Modifies the tone script so that it will loop the specified
        /// number of times.
        /// </summary>
        /// <param name="count">The number of times to play the script.</param>
        /// <exception cref="ArgumentException">Thrown if <paramref name="count" /> is less than or equal to zero.</exception>
        /// <remarks>
        /// <note>
        /// This method does nothing if the script already contains a <b>L=&lt;int&gt;</b>
        /// command.
        /// </note>
        /// </remarks>
        public void Loop(int count)
        {
            if (Script.Contains("L="))
                return;     // Already has a loop command.

            if (count <= 0)
                throw new ArgumentException(string.Format("[TelephoneTone] can only accept positive loop counts. [count={0}] is not acceptable.", count), "count");

            this.Script = string.Format("L={0};{1}", count, this.Script);
        }

        /// <summary>
        /// Renders the tone as a string.
        /// </summary>
        /// <returns>The tone script.</returns>
        public override string ToString()
        {
            return Script;
        }
    }
}
