//-----------------------------------------------------------------------------
// FILE:        GetConfigAck.cs
// CONTRIBUTOR: Jeff Lill
// COPYRIGHT:   Copyright (c) 2005-2015 by Jeffrey Lill.  All rights reserved.
// DESCRIPTION: Implements GetConfigAck.

using System;

using LillTek.Common;
using LillTek.Messaging;
using LillTek.Datacenter;

namespace LillTek.Datacenter.Msgs
{
    /// <summary>
    /// Implements the message used by <see cref="ConfigServiceProvider" /> to return
    /// configuration information from a Data Center Configuration service in
    /// response to a <see cref="GetConfigMsg" />.
    /// </summary>
    public sealed class GetConfigAck : BlobPropertyMsg, IAck
    {
        /// <summary>
        /// Returns the type ID string to be used when serializing the message.
        /// </summary>
        public new static string GetTypeID()
        {
            return "LT.DC.Config.GetConfigAck";
        }

        /// <summary>
        /// Default constructor.
        /// </summary>
        public GetConfigAck()
        {
        }

        /// <summary>
        /// Constructs the ack instance to be used to communicate an
        /// exception back to the client.
        /// </summary>
        /// <param name="e">The exception.</param>
        public GetConfigAck(Exception e)
        {
            base["_exception"]      = e.Message;
            base["_exception-type"] = e.GetType().FullName;
        }

        /// <summary>
        /// Constructs the ack instance to be used to communicate the configuration text
        /// text back to the client.
        /// </summary>
        /// <param name="config">
        /// The configuration text as described for *.ini files in <see cref="Config" />.
        /// </param>
        public GetConfigAck(string config)
        {
            this.ConfigText = config;
        }

        /// <summary>
        /// Protected constructor that does not instantiate the property hash table.
        /// </summary>
        /// <param name="param">Pass <see cref="Stub.Param" />.</param>
        private GetConfigAck(Stub param)
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
            GetConfigAck clone;

            clone = new GetConfigAck(Stub.Param);
            clone.CopyBaseFields(this, true);

            return clone;
        }

        /// <summary>
        /// The configuration settings formatted as decribed for *.ini files
        /// in <see cref="Config" />.
        /// </summary>
        public string ConfigText
        {
            get { return Helper.FromUTF8(base._Data); }
            set { base._Data = Helper.ToUTF8(value); }
        }

        //---------------------------------------------------------------------
        // IAck Implementation

        /// <summary>
        /// The exception's message string if the was an exception detected
        /// on by the server (null or the empty string if there was no error).
        /// </summary>
        public string Exception
        {
            get { return base["_exception"]; }
            set { base["_exception"] = value; }
        }

        /// <summary>
        /// The fully qualified name of the exception type.
        /// </summary>
        public string ExceptionTypeName
        {
            get { return (string)base["_exception-type"]; }
            set { base["_exception-type"] = value; }
        }
    }
}
