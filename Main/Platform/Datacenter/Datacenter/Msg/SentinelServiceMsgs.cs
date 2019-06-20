//-----------------------------------------------------------------------------
// FILE:        SentinelServiceMsgs.cs
// CONTRIBUTOR: Jeff Lill
// COPYRIGHT:   Copyright (c) 2005-2015 by Jeffrey Lill.  All rights reserved.
// DESCRIPTION: Implements the messages and other classes used to for 
//              communication between SentinelService client and SentinelServiceHandler 
//              instances.

using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.Text;

using LillTek.Common;
using LillTek.Messaging;

namespace LillTek.Datacenter.Msgs.SentinelService
{
    /// <summary>
    /// Sent by the client to initiate a connection to the service.
    /// </summary>
    public sealed class ConnectMsg : PropertyMsg
    {
        /// <summary>
        /// Returns the type ID string to be used when serializing the message.
        /// </summary>
        public new static string GetTypeID()
        {
            return "LT.DC.Sentinel.Connect";
        }

        /// <summary>
        /// Default constructor.
        /// </summary>
        public ConnectMsg()
        {
        }

        /// <summary>
        /// Protected constructor that does not instantiate the property hash table.
        /// </summary>
        /// <param name="param">Pass <see cref="Stub.Param" />.</param>
        private ConnectMsg(Stub param)
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
            ConnectMsg clone;

            clone = new ConnectMsg(Stub.Param);
            clone.CopyBaseFields(this, true);

            return clone;
        }
    }

    /// <summary>
    /// The reply to a <see cref="ConnectMsg" />.
    /// </summary>
    public sealed class ConnectAck : Ack
    {
        /// <summary>
        /// Returns the type ID string to be used when serializing the message.
        /// </summary>
        public new static string GetTypeID()
        {
            return "LT.DC.Sentinel.ConnectAck";
        }

        /// <summary>
        /// Default constructor.
        /// </summary>
        public ConnectAck()
        {
        }

        /// <summary>
        /// Constructor.
        /// </summary>
        /// <param name="instanceEP">The endpoint the client should use for subsequent communication.</param>
        public ConnectAck(MsgEP instanceEP)
        {
            this.InstanceEP = instanceEP;
        }

        /// <summary>
        /// Use this constructor to pass an exception back to the caller.
        /// </summary>
        /// <param name="e">The exception.</param>
        public ConnectAck(Exception e)
        {
            base.Exception = e.Message;
        }

        /// <summary>
        /// Protected constructor that does not instantiate the property hash table.
        /// </summary>
        /// <param name="param">Pass <see cref="Stub.Param" />.</param>
        private ConnectAck(Stub param)
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
            ConnectAck clone;

            clone = new ConnectAck(Stub.Param);
            clone.CopyBaseFields(this, true);

            return clone;
        }

        /// <summary>
        /// The endpoint the client should use for subsequent communication with
        /// the service instance.
        /// </summary>
        public MsgEP InstanceEP
        {
            get { return MsgEP.Parse(base._Get("instance-ep")); }
            set { base._Set("instance-ep", value.ToString()); }
        }
    }

    /// <summary>
    /// Sent by the client to archive an event.
    /// </summary>
    public sealed class LogEventMsg : BlobPropertyMsg
    {
        /// <summary>
        /// Returns the type ID string to be used when serializing the message.
        /// </summary>
        public new static string GetTypeID()
        {
            return "LT.DC.Sentinel.LogEvent";
        }

        /// <summary>
        /// Default constructor.
        /// </summary>
        public LogEventMsg()
        {
        }

        /// <summary>
        /// Constructor.
        /// </summary>
        /// <param name="logName">The event log name.</param>
        /// <param name="logEntry">The log entry.</param>
        public LogEventMsg(string logName, EventLogEntry logEntry)
        {
            this.LogName     = logName;
            this.EntryType   = logEntry.EntryType;
            this.Time        = logEntry.TimeWritten.ToUniversalTime();
            this.MachineName = logEntry.MachineName;
            this.Message     = logEntry.Message;
            this.Data        = logEntry.Data;
        }

        /// <summary>
        /// Protected constructor that does not instantiate the property hash table.
        /// </summary>
        /// <param name="param">Pass <see cref="Stub.Param" />.</param>
        private LogEventMsg(Stub param)
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
            LogEventMsg clone;

            clone = new LogEventMsg(Stub.Param);
            clone.CopyBaseFields(this, true);

            return clone;
        }

        /// <summary>
        /// The source log name.
        /// </summary>
        public string LogName
        {
            get { return base._Get("log-name"); }
            set { base._Set("log-name", value); }
        }

        /// <summary>
        /// Indicates the type of log entry: Error, warning, information, etc.
        /// </summary>
        public EventLogEntryType EntryType
        {
            get { return (EventLogEntryType)base._Get("entry-type", (int)EventLogEntryType.Information); }
            set { base._Set("entry-type", (int)value); }
        }

        /// <summary>
        /// The time the event was originally logged (UTC).
        /// </summary>
        public DateTime Time
        {
            get { return Helper.ParseInternetDate(base._Get("time")); }
            set { base._Set("time", Helper.ToInternetDate(value)); }
        }

        /// <summary>
        /// The source machine.
        /// </summary>
        public string MachineName
        {
            get { return base._Get("machine-name"); }
            set { base._Set("machine-name", value); }
        }

        /// <summary>
        /// The event message.
        /// </summary>
        public string Message
        {

            get { return base._Get("message"); }
            set { base._Set("message", value); }
        }

        /// <summary>
        /// Returns any binary data associated with the event (or <c>null</c>).
        /// </summary>
        public byte[] Data
        {
            get { return base._Data; }
            set { base._Data = value; }
        }
    }

    /// <summary>
    /// The reply to a <see cref="LogEventMsg" />.
    /// </summary>
    public sealed class LogEventAck : Ack
    {
        /// <summary>
        /// Returns the type ID string to be used when serializing the message.
        /// </summary>
        public new static string GetTypeID()
        {
            return "LT.DC.Sentinel.LogEventAck";
        }

        /// <summary>
        /// Default constructor.
        /// </summary>
        public LogEventAck()
        {
        }

        /// <summary>
        /// Use this constructor to pass an exception back to the caller.
        /// </summary>
        /// <param name="e">The exception.</param>
        public LogEventAck(Exception e)
        {
            base.Exception = e.Message;
        }

        /// <summary>
        /// Protected constructor that does not instantiate the property hash table.
        /// </summary>
        /// <param name="param">Pass <see cref="Stub.Param" />.</param>
        private LogEventAck(Stub param)
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
            LogEventAck clone;

            clone = new LogEventAck(Stub.Param);
            clone.CopyBaseFields(this, true);

            return clone;
        }
    }
}
