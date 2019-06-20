//-----------------------------------------------------------------------------
// FILE:        GetVoicesCommand.cs
// CONTRIBUTOR: Jeff Lill
// COPYRIGHT:   Copyright (c) 2005-2015 by Jeffrey Lill.  All rights reserved.
// DESCRIPTION: Returns the names of the text-to-speech voices installed on the switch.

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Reflection;

using FreeSWITCH;
using FreeSWITCH.Native;

using LillTek.Advanced;
using LillTek.Common;
using LillTek.Telephony.NeonSwitch;

using Switch = LillTek.Telephony.NeonSwitch.Switch;

namespace LillTek.Telephony.NeonSwitchCore
{
    /// <summary>
    /// Returns the names of the text-to-speech voices installed on the switch.
    /// </summary>
    /// <remarks>
    /// <para>
    /// This command is used internally by NeonSwitch applications to obtain the
    /// voice names to be saved to the <see cref="Switch" />.<see cref="Switch.InstalledVoices" />
    /// collection.
    /// </para>
    /// <para>
    /// The commands returns the list of installed voices, one per line.
    /// </para>
    /// </remarks>
    public class GetVoicesCommand : ISwitchSubcommand
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
            foreach (var voice in SpeechEngine.InstalledVoices.Keys)
                args.WriteLine(voice);
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
