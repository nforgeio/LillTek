//-----------------------------------------------------------------------------
// FILE:        ISwitchSubcommand.cs
// CONTRIBUTOR: Jeff Lill
// COPYRIGHT:   Copyright (c) 2005-2015 by Jeffrey Lill. All rights reserved.
// DESCRIPTION: Implemented by applications that wish to register NeonSwitch
//              subcommand classes via Switch.RegisterAssemblySubcommnds.

using System;

using LillTek.Common;
using LillTek.Telephony.Common;

namespace LillTek.Telephony.NeonSwitch
{
    /// <summary>
    /// Implemented by applications that wish to register NeonSwitch subcommand
    /// classes via <see cref="Switch" />.<see cref="Switch.RegisterAssemblySubcommands" />.
    /// </summary>
    public interface ISwitchSubcommand
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
        void Execute(ExecuteEventArgs args);

        /// <summary>
        /// Called by NeonSwitch to execute an asynchronous subcommand.
        /// </summary>
        /// <param name="args">The command execution context including the arguments.</param>
        void ExecuteBackground(ExecuteBackgroundEventArgs args);
    }
}
