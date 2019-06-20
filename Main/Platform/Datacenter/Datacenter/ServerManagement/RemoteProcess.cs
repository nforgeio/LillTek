//-----------------------------------------------------------------------------
// FILE:        RemoteProcess.cs
// CONTRIBUTOR: Jeff Lill
// COPYRIGHT:   Copyright (c) 2005-2015 by Jeffrey Lill.  All rights reserved.
// DESCRIPTION: Information about a remote process.

using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Management;
using System.Runtime.Serialization;
using System.ServiceModel;
using System.Text;

using LillTek.Common;
using LillTek.Service;

namespace LillTek.Datacenter.ServerManagement
{
    /// <summary>
    /// Information about a remote process.
    /// </summary>
    [DataContract(Namespace = ServerManager.ContractNamespace)]
    public sealed class RemoteProcess
    {
        /// <summary>
        /// The process ID.
        /// </summary>
        [DataMember]
        public int ProcessID { get; set; }

        /// <summary>
        /// The process name.
        /// </summary>
        [DataMember]
        public string ProcessName { get; set; }

        /// <summary>
        /// Bytes of virtual memory allocated to the process.
        /// </summary>
        [DataMember]
        public long VirtualMemorySize { get; set; }

        /// <summary>
        /// Bytes of physical memory allocated to the process.
        /// </summary>
        [DataMember]
        public long WorkingSetSize { get; set; }

        /// <summary>
        /// The process CPU utilization expressed as a percentage between
        /// 0 and 100%.
        /// </summary>
        [DataMember]
        public int CpuUtilization { get; set; }

        private Process     process;        // The underlying process
        private long        orgTicks;       // Used for calculating CPU utilization

        /// <summary>
        /// Default constructor used by serializers.
        /// </summary>
        public RemoteProcess()
        {
        }

        /// <summary>
        /// Initializes the instance from the process passed.
        /// </summary>
        /// <param name="process">The source process.</param>
        /// <remarks>
        /// <note>
        /// CPU utilization is is not calculated by this constructor and will
        /// be initialized to zero.  To compute CPU utilization, the caller must record
        /// the DateTime when the <see cref="Process" /> instance passed was returned
        /// from the .NET Framework before calling this constructor.  Then the thread
        /// should sleep for a period of time (probably a second or two) and then call
        /// <see cref="CalcCpuUtilization" />, passing the actual timespan that elapsed.
        /// </note>
        /// </remarks>
        public RemoteProcess(Process process)
        {
            this.process           = process;
            this.ProcessID         = process.Id;
            this.ProcessName       = process.ProcessName;
            this.VirtualMemorySize = process.VirtualMemorySize64;
            this.WorkingSetSize    = process.WorkingSet64;
            this.CpuUtilization    = 0;

            if (this.ProcessName != "Idle")
                this.orgTicks = process.TotalProcessorTime.Ticks;
        }

        /// <summary>
        /// Calculates the CPU utilization for the process based on the difference
        /// between the elapsed time passed and the total processor time recorded
        /// for the process (adjusting for the number of processors).
        /// </summary>
        /// <param name="elapsed">
        /// The actual elapsed time between when the underlying process was returned
        /// by the .NET Framework and this method was called.
        /// </param>
        public void CalcCpuUtilization(TimeSpan elapsed)
        {
            long utilization;

            if (process == null)
                throw new InvalidOperationException();

            if (this.ProcessName == "Idle")
            {
                this.CpuUtilization = 0;
                return;
            }

            try
            {
                process.Refresh();
            }
            catch
            {
                // This can throw an exception if the process has quit.

                return;
            }

            if (process.HasExited)
                utilization = 0;
            else
            {
                utilization  = (100 * (process.TotalProcessorTime.Ticks - orgTicks));
                utilization /= elapsed.Ticks;
                utilization /= Environment.ProcessorCount;
            }

            this.CpuUtilization = (int)utilization;
        }
    }
}
