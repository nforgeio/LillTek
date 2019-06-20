//-----------------------------------------------------------------------------
// FILE:        SpeakCommand.cs
// CONTRIBUTOR: Jeff Lill
// COPYRIGHT:   Copyright (c) 2005-2015 by Jeffrey Lill.  All rights reserved.
// DESCRIPTION: Generates an audio file by speaking a phrase using speech synthesis.

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Reflection;

using FreeSWITCH;
using FreeSWITCH.Native;

using LillTek.Advanced;
using LillTek.Common;
using LillTek.Telephony.Common;
using LillTek.Telephony.NeonSwitch;

using Switch = LillTek.Telephony.NeonSwitch.Switch;

namespace LillTek.Telephony.NeonSwitchCore
{
    /// <summary>
    /// Generates an audio file by speaking a <see cref="Phrase" /> using speech synthesis.
    /// </summary>
    /// <remarks>
    /// <para>
    /// This command is used internally by NeonSwitch to generate speech from a <see cref="Phrase" />
    /// writing the output to an audio file within the switch's global phrase cache.  The command
    /// returns the fully qualified path to the generated file.
    /// </para>
    /// <para>
    /// The command parameter is a <see cref="Phrase" /> instance as serialized via its
    /// <see cref="Phrase.Encode()" /> method.
    /// </para>
    /// </remarks>
    public class SpeakCommand : ISwitchSubcommand
    {
        /// <summary>
        /// Called by NeonSwitch to execute a synchronous subcommand.
        /// </summary>
        /// <param name="args">The command execution context including the arguments.</param>
        /// <remarks>
        /// <note>
        /// Implementations can use the various <see cref="ExecuteEventArgs.Write(string)"/> method 
        /// overloads in <see cref="ExecuteEventArgs"/> to stream text back to the caller.
        /// </note>
        /// </remarks>
        public void Execute(ExecuteEventArgs args)
        {
            try
            {
                var phrase = Phrase.Decode(args.SubcommandArgs);

                args.Write(SpeechEngine.SpeakToFile(phrase));
            }
            catch (Exception e)
            {
                SysLog.LogException(e);

                args.Write(SpeechEngine.ErrorAudioPath);
            }
        }

        /// <summary>
        /// Called by NeonSwitch to execute an asynchronous subcommand.
        /// </summary>
        /// <param name="args">The command execution context including the arguments.</param>
        public void ExecuteBackground(ExecuteBackgroundEventArgs args)
        {
            SwitchApp.ThrowExecuteNotImplemented(args.Subcommand);
        }
    }
}
