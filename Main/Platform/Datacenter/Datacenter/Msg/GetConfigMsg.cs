//-----------------------------------------------------------------------------
// FILE:        GetConfigMsg.cs
// CONTRIBUTOR: Jeff Lill
// COPYRIGHT:   Copyright (c) 2005-2015 by Jeffrey Lill.  All rights reserved.
// DESCRIPTION: Implements GetConfigMsg.

using System;

using LillTek.Common;
using LillTek.Messaging;
using LillTek.Datacenter;

namespace LillTek.Datacenter.Msgs
{
    /// <summary>
    /// Implements the message used by <see cref="ConfigServiceProvider" /> to request
    /// configuration information from a Data Center Configuration service.
    /// The service responds with a <see cref="GetConfigAck" /> message.
    /// </summary>
    public sealed class GetConfigMsg : PropertyMsg
    {
        //---------------------------------------------------------------------
        // Static members

        /// <summary>
        /// Returns the type ID string to be used when serializing the message.
        /// </summary>
        public static new string GetTypeID()
        {
            return "LT.DC.Config.GetConfig";
        }

        //---------------------------------------------------------------------
        // Instance members

        /// <summary>
        /// Constructor.
        /// </summary>
        public GetConfigMsg()
        {
        }

        /// <summary>
        /// Constructor.
        /// </summary>
        /// <param name="machineName">The requesting machine's name.</param>
        /// <param name="exeFile">The unqualified name of the current process' executable file.</param>
        /// <param name="exeVersion">The version number of the current executable file.</param>
        /// <param name="usage">Used to indicate a non-default usage for this application instance.</param>
        public GetConfigMsg(string machineName, string exeFile, Version exeVersion, string usage)
        {

            this.MachineName = machineName;
            this.ExeFile     = exeFile;
            this.ExeVersion  = exeVersion;
            this.Usage       = usage;
        }

        /// <summary>
        /// Protected constructor that does not instantiate the property hash table.
        /// </summary>
        /// <param name="param">Pass <see cref="Stub.Param" />.</param>
        private GetConfigMsg(Stub param)
            : base(param)
        {
        }

        /// <summary>
        /// Generates a clone of this message instance, generating a new 
        /// <see cref="Msg._MsgID" /> property if the original ID is
        /// not empty.
        /// </summary>
        /// <returns>The cloned message.</returns>
        public override Msg Clone()
        {
            GetConfigMsg clone;

            clone = new GetConfigMsg(Stub.Param);
            clone.CopyBaseFields(this, true);

            return clone;
        }

        /// <summary>
        /// This host name of the client machine.
        /// </summary>
        public string MachineName
        {
            get { return base["machine-name"]; }
            set { base["machine-name"] = value; }
        }

        /// <summary>
        /// Unqualified name of the client's application executable.
        /// </summary>
        public string ExeFile
        {
            get { return base["exe-file"]; }
            set { base["exe-file"] = value; }
        }

        /// <summary>
        /// The client executable's version number.
        /// </summary>
        public Version ExeVersion
        {
            get { return new Version(base["exe-version"]); }
            set { base["exe-version"] = value.ToString(); }
        }

        /// <summary>
        /// Categories the client application instance's use.
        /// </summary>
        public string Usage
        {
            get { return base["usage"]; }
            set { base["usage"] = value; }
        }
    }
}
