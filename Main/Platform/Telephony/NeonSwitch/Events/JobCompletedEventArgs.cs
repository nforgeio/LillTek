//-----------------------------------------------------------------------------
// FILE:        JobCompletedEventArgs.cs
// CONTRIBUTOR: Jeff Lill
// COPYRIGHT:   Copyright (c) 2005-2015 by Jeffrey Lill.  All rights reserved.
// DESCRIPTION: The arguments for a Switch.JobCompletedEvent. 

using System;
using System.Collections.Generic;
using System.Diagnostics;
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
    /// The arguments for a <see cref="Switch" />.<see cref="Switch.JobCompletedEvent" />. 
    /// </summary>
    /// <remarks>
    /// <para>
    /// The <see cref="Switch"/>.<see cref="Switch.JobCompletedEvent" /> is raised when NeonSwitch
    /// detects that a background job has completed.
    /// </para>
    /// </remarks>
    public class JobCompletedEventArgs : EventArgs
    {
        /// <summary>
        /// Constrctor.
        /// </summary>
        /// <param name="jobID">The unique ID for the completed job.</param>
        internal JobCompletedEventArgs(Guid jobID)
        {
        }

        /// <summary>
        /// Returns the unique ID for the completed job.
        /// </summary>
        public Guid JobID { get; private set; }
    }
}
