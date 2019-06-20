//-----------------------------------------------------------------------------
// FILE:        ExecuteBackgroundEventArgs.cs
// CONTRIBUTOR: Jeff Lill
// COPYRIGHT:   Copyright (c) 2005-2015 by Jeffrey Lill.  All rights reserved.
// DESCRIPTION: The arguments for a Switch.ExecuteBackgroundEvent.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;

using FreeSWITCH;
using FreeSWITCH.Native;

using LillTek.Common;
using LillTek.Telephony.Common;

namespace LillTek.Telephony.NeonSwitch
{
    /// <summary>
    /// The arguments for a <see cref="Switch"/>.<see cref="Switch.ExecuteBackgroundEvent" />.
    /// </summary>
    /// <remarks>
    /// <para>
    /// The <see cref="Switch"/>.<see cref="Switch.ExecuteBackgroundEvent" /> is raised when the application
    /// receives an asynchronous command from another NeonSwitch application.  The <see cref="RawArgs" />
    /// property will hold the untouched command arguments as they were received with the command and
    /// <see cref="Subcommand" /> and <see cref="SubcommandArgs"/> will hold the the parsed command string and
    /// arguments.
    /// </para>
    /// <para>
    /// Applications that actually implement commands directly in the event handler will will set the 
    /// <see cref="Handled" /> property to <c>true</c> to indicate to the switch that no further
    /// processing of the command should be performed.
    /// </para>
    /// </remarks>
    public class ExecuteBackgroundEventArgs : EventArgs
    {
        private static char[] whitespace = new char[] { ' ', '\t' };

        private ApiBackgroundContext context;

        /// <summary>
        /// Constructor.
        /// </summary>
        /// <param name="context">The FreeSWITCH API context</param>
        internal ExecuteBackgroundEventArgs(ApiBackgroundContext context)
        {
            this.context = context;

            // Extract the subcommand and arguments if present.

            var fields = context.Arguments.Split(whitespace, 2);

            this.Subcommand     = string.Empty;
            this.SubcommandArgs = string.Empty;

            if (fields.Length == 0)
                return;

            this.Subcommand = fields[0];
            if (fields.Length > 1)
                this.SubcommandArgs = fields[1];
        }

        /// <summary>
        /// Returns the arguments received with the command.
        /// </summary>
        public string RawArgs
        {
            get { return context.Arguments; }
        }

        /// <summary>
        /// Returns the received subcommand if there is one or an empty string.
        /// </summary>
        /// <remarks>
        /// NeonSwitch considers the text from the beginning of the raw arguments
        /// up to the first space or the end of the string to be the command.
        /// </remarks>
        public string Subcommand { get; private set; }

        /// <summary>
        /// Returns the received command arguments located after the command
        /// in the raw arguments.
        /// </summary>
        public string SubcommandArgs { get; private set; }

        /// <summary>
        /// Indicates that the application event handler has executed the
        /// command and that no default command processing should be 
        /// performed.  Defaults to <c>false</c>.
        /// </summary>
        public bool Handled { get; set; }
    }
}
