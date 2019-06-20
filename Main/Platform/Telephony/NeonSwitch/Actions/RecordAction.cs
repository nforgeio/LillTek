//-----------------------------------------------------------------------------
// FILE:        RecordAction.cs
// CONTRIBUTOR: Jeff Lill
// COPYRIGHT:   Copyright (c) 2005-2015 by Jeffrey Lill.  All rights reserved.
// DESCRIPTION: Controls call recording.

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

namespace LillTek.Telephony.NeonSwitch
{
    /// <summary>
    /// Controls call recording.
    /// </summary>
    /// <remarks>
    /// <para>
    /// You can start and stop recording from a call to a file by passing a <b>start</b> as <c>true</c>
    /// or <c>false</c> to the constructor and specifying the file path.  You can restart recording
    /// to the same file and the switch will simply append to the existing file.  You mau also
    /// limit the length of the recording by passing a non-<c>null</c> value to the <b>limit</b>
    /// parameter.
    /// </para>
    /// <note>
    /// NeonSwitch only supports recording to WAV files.  The file type must be <b>.wav</b>.
    /// </note>
    /// </remarks>
    public class RecordAction : SwitchAction
    {
        private bool        start;
        private string      path;
        private TimeSpan?   limit;

        /// <summary>
        /// Constructs an action that controls recording of the current call on an executing dialplan.
        /// </summary>
        /// <param name="start">Pass <c>true</c> to start recording, <c>false</c> to stop.</param>
        /// <param name="path">
        /// <para>
        /// Path to the recorded file.
        /// </para>
        /// </param>
        /// <note>
        /// The file type must be <b>.wav</b>.  Only WAV files are supported by NeonSwitch.
        /// </note>
        /// <param name="limit">Pass a non-<c>null</c> to limit the recording duration.</param>
        /// <exception cref="ArgumentNullException">Thrown if <paramref name="path" /> is <c>null</c> or empty.</exception>
        /// <exception cref="ArgumentException">Thrown if the file type is not <b>.wav</b>.</exception>
        public RecordAction(bool start, string path, TimeSpan? limit)
        {
            if (string.IsNullOrWhiteSpace(path))
                throw new ArgumentNullException("path", "Cannot pass [path] as NULL or empty to [RecordAction].");

            if (String.Compare(Path.GetExtension(path), ".wav", true) != 0)
                throw new ArgumentException(string.Format("[RecordAction] cannot record to [{0}] since it does have the [.wav] file extension.", path));

            this.start = start;
            this.path  = path;
            this.limit = limit;
        }

        /// <summary>
        /// Constructs an action that controls recording of a specific call.
        /// </summary>
        /// <param name="callID">The target call ID.</param>
        /// <param name="start">Pass <c>true</c> to start recording, <c>false</c> to stop.</param>
        /// <param name="path">
        /// <para>
        /// Path to the recorded file.
        /// </para>
        /// </param>
        /// <note>
        /// The file type must be <b>.wav</b>.  Only WAV files are supported by NeonSwitch.
        /// </note>
        /// <param name="limit">Pass a non-<c>null</c> to limit the recording duration.</param>
        /// <exception cref="ArgumentNullException">Thrown if <paramref name="path" /> is <c>null</c> or empty.</exception>
        /// <exception cref="ArgumentException">Thrown if the file type is not <b>.wav</b>.</exception>
        public RecordAction(Guid callID, bool start, string path, TimeSpan? limit)
            : this(start, path, limit)
        {
            this.CallID = callID;
        }

        /// <summary>
        /// Renders the high-level switch action instance into zero or more <see cref="SwitchExecuteAction" />
        /// instances and then adds these to the <see cref="ActionRenderingContext" />.<see cref="ActionRenderingContext.Actions" />
        /// collection.
        /// </summary>
        /// <param name="context">The action rendering context.</param>
        /// <exception cref="NotSupportedException">Thrown if the action is being rendered outside of a dialplan.</exception>
        /// <remarks>
        /// <note>
        /// It is perfectly reasonable for an action to render no actions to the
        /// context or to render multiple actions based on its properties.
        /// </note>
        /// </remarks>
        public override void Render(ActionRenderingContext context)
        {
            var expandedPath = Switch.ExpandFilePath(path);
            var limitString  = string.Empty;

            if (start && limit.HasValue)
                limitString = " " + SwitchHelper.GetScheduleSeconds(limit.Value).ToString();

            if (context.IsDialplan)
                context.Actions.Add(new SwitchExecuteAction("uuid_record", "${{Unique-ID}} {0} '{1}'{2}", start ? "start" : "stop", limitString));
            else
            {
                CheckCallID();
                context.Actions.Add(new SwitchExecuteAction("uuid_record", "{0} {1} '{2}'{3}", start ? "start" : "stop", limitString));
            }
        }
    }
}
