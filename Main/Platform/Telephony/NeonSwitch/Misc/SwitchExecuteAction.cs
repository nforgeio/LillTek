﻿//-----------------------------------------------------------------------------
// FILE:        SwitchExecuteAction.cs
// CONTRIBUTOR: Jeff Lill
// COPYRIGHT:   Copyright (c) 2005-2015 by Jeffrey Lill.  All rights reserved.
// DESCRIPTION: The structure describing a switch-level action.  These
//              are generated by a SwitchAction's Render() method.

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
    /// The structure describing a switch-level action.  These
    /// are generated by a <see cref="SwitchAction" />'s <see cref="SwitchAction.Render" />
    /// method.
    /// </summary>
    public struct SwitchExecuteAction
    {
        /// <summary>
        /// Exception message used by derived class that required a call ID when not executing within a dialplan.
        /// </summary>
        private const string CallIDRequiredMsg = "Action requires [CallID] when not executing in a dialplan.";

        /// <summary>
        /// The call ID to use when executing outside of a dialplan.
        /// </summary>
        public Guid CallID;

        /// <summary>
        /// Returns the name of the application to be invoked on the call.
        /// </summary>
        public string Application;

        /// <summary>
        /// Returns the parameter/data to be passed to the application (or <b>empty</b>).
        /// </summary>
        public string Data;

        /// <summary>
        /// Constructs an action that calls a NeonSwitch application.
        /// </summary>
        /// <param name="application">The application name.</param>
        /// <exception cref="ArgumentNullException">Thrown if <paramref name="application" /> is <c>null</c>.</exception>
        public SwitchExecuteAction(string application)
        {
            if (application == null)
                throw new ArgumentNullException("application");

            this.CallID      = Guid.Empty;
            this.Application = application;
            this.Data        = string.Empty;
        }

        /// <summary>
        /// Constructs an action that calls a NeonSwitch application passing application
        /// parameters/data.
        /// </summary>
        /// <param name="application">The application name.</param>
        /// <param name="data">The parameter/data.</param>
        /// <exception cref="ArgumentNullException">Thrown if <paramref name="application" /> is <c>null</c>.</exception>
        public SwitchExecuteAction(string application, string data)
        {
            if (application == null)
                throw new ArgumentNullException("application");

            if (data == null)
                data = string.Empty;

            this.CallID      = Guid.Empty;
            this.Application = application;
            this.Data        = data;
        }

        /// <summary>
        /// Constructs an action that calls a NeonSwitch application passing application
        /// and formatted parameters/data.
        /// </summary>
        /// <param name="application">The application name.</param>
        /// <param name="format">The parameter/data format string.</param>
        /// <param name="args">The parameter/data format string.</param>
        /// <exception cref="ArgumentNullException">Thrown if <paramref name="application" /> is <c>null</c>.</exception>
        public SwitchExecuteAction(string application, string format, params object[] args)
        {
            if (application == null)
                throw new ArgumentNullException("application");

            this.CallID      = Guid.Empty;
            this.Application = application;
            this.Data        = string.Format(format, args);
        }

        /// <summary>
        /// Constructs an action that calls a NeonSwitch application.
        /// </summary>
        /// <param name="callID">The call ID.</param>
        /// <param name="application">The application name.</param>
        /// <exception cref="ArgumentException">Thrown if <paramref name="callID" /> is <see cref="Guid.Empty" />.</exception>
        /// <exception cref="ArgumentNullException">Thrown if <paramref name="application" /> is <c>null</c>.</exception>
        public SwitchExecuteAction(Guid callID, string application)
        {
            if (callID == Guid.Empty)
                throw new ArgumentNullException(CallIDRequiredMsg);

            if (application == null)
                throw new ArgumentNullException("application");

            this.CallID      = callID;
            this.Application = application;
            this.Data        = string.Empty;

            ConvertToBroadcast();
        }

        /// <summary>
        /// Constructs an action that calls a NeonSwitch application passing application
        /// parameters/data.
        /// </summary>
        /// <param name="callID">The call ID.</param>
        /// <param name="application">The application name.</param>
        /// <param name="data">The parameter/data.</param>
        /// <exception cref="ArgumentException">Thrown if <paramref name="callID" /> is <see cref="Guid.Empty" />.</exception>
        /// <exception cref="ArgumentNullException">Thrown if <paramref name="application" /> is <c>null</c>.</exception>
        public SwitchExecuteAction(Guid callID, string application, string data)
        {
            if (callID == Guid.Empty)
                throw new ArgumentNullException(CallIDRequiredMsg);

            if (application == null)
                throw new ArgumentNullException("application");

            if (data == null)
                data = string.Empty;

            this.CallID      = callID;
            this.Application = application;
            this.Data        = data;

            ConvertToBroadcast();
        }

        /// <summary>
        /// Constructs an action that calls a NeonSwitch application passing application
        /// and formatted parameters/data.
        /// </summary>
        /// /// <param name="callID">The call ID.</param>
        /// <param name="application">The application name.</param>
        /// <param name="format">The parameter/data format string.</param>
        /// <param name="args">The parameter/data format string.</param>
        /// <exception cref="ArgumentException">Thrown if <paramref name="callID" /> is <see cref="Guid.Empty" />.</exception>
        /// <exception cref="ArgumentNullException">Thrown if <paramref name="application" /> is <c>null</c>.</exception>
        public SwitchExecuteAction(Guid callID, string application, string format, params object[] args)
        {
            if (callID == Guid.Empty)
                throw new ArgumentNullException(CallIDRequiredMsg);

            if (application == null)
                throw new ArgumentNullException("application");

            this.CallID      = callID;
            this.Application = application;
            this.Data        = string.Format(format, args);

            ConvertToBroadcast();
        }

        /// <summary>
        /// Converts the action to use <b>uuid_broadcast2</b> to execute as dialplan tool.
        /// </summary>
        private void ConvertToBroadcast()
        {
            // This generates a somewhat screwy FreeSwitch syntax for executing
            // an arbitray dialplan tool on a call.

            if (Data == null)
                Data = string.Empty;

            Data        = string.Format("{0:D} {1}::{2}", CallID, Application, SwitchHelper.UrlEncode(Data));
            Application = "uuid_broadcast2";
        }
    }
}
