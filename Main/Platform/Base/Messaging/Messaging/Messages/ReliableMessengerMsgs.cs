//-----------------------------------------------------------------------------
// FILE:        ReliableMessengerMsgs.cs
// CONTRIBUTOR: Jeff Lill
// COPYRIGHT:   Copyright (c) 2005-2015 by Jeffrey Lill.  All rights reserved.
// DESCRIPTION: Implements the messages used by IReliableMessenger implementations.

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Text;

using LillTek.Common;
using LillTek.Messaging;

namespace LillTek.Messaging
{
    /// <summary>
    /// Defines the possible interpretations of a <see cref="DeliveryMsg" />.
    /// </summary>
    public enum DeliveryOperation
    {
        /// <summary>
        /// Indicates that the message was sent in an attempt to delivery
        /// the underlying query message.
        /// </summary>
        Attempt,

        /// <summary>
        /// Indicates that the message was sent to confirm a successful or
        /// failed delivery.
        /// </summary>
        Confirmation
    }

    /// <summary>
    /// Used to transmit query or response information from between client
    /// and server implementations of <see cref="IReliableMessenger" />.
    /// The instance receiving this message will respond with a
    /// <see cref="DeliveryAck" /> message.
    /// </summary>
    public sealed class DeliveryMsg : BlobPropertyMsg
    {
        //---------------------------------------------------------------------
        // Implementation Note
        //
        // I'm going to serialize the query and response messages to the
        // message's blob using Msg.Save().

        private Msg     query;
        private Msg     response;

        /// <summary>
        /// Returns the type ID string to be used when serializing the message.
        /// </summary>
        public new static string GetTypeID()
        {
            return "LT.DC.Reliable.Delivery";
        }

        /// <summary>
        /// Default constructor.
        /// </summary>
        public DeliveryMsg()
        {
        }

        /// <summary>
        /// Constuctor.
        /// </summary>
        /// <param name="operation">Specifies how the message should be interpreted.</param>
        /// <param name="timestamp">The time (UTC) when the delivery was completed successfully or was aborted.</param>
        /// <param name="targetEP">The original target or cluster endpoint.</param>
        /// <param name="confirmEP">The confirmation endpoint (or <c>null</c>).</param>
        /// <param name="query">The query message.</param>
        /// <param name="topologyID">The globally unique cluster topology provider instance ID or <see cref="Guid.Empty" />.</param>
        /// <param name="topologyInfo">The serialized cluster itopology nformation (or <c>null</c>).</param>
        /// <param name="topologyParam">The serialized topology parameter (or <c>null</c>).</param>
        /// <param name="exception">The exception for failed deliveries or queries.</param>
        /// <param name="response">The response message (or <c>null</c>).</param>
        public DeliveryMsg(DeliveryOperation operation, DateTime timestamp, MsgEP targetEP, MsgEP confirmEP, Msg query,
                           Guid topologyID, string topologyInfo, string topologyParam, Exception exception, Msg response)
        {

            this.Operation     = operation;
            this.Timestamp     = timestamp;
            this.TargetEP      = targetEP;
            this.ConfirmEP     = confirmEP;
            this.TopologyID    = topologyID;
            this.TopologyInfo  = topologyInfo;
            this.TopologyParam = topologyParam;
            this.Exception     = exception;
            this.query         = query;
            this.response      = response;

            // Serialize the query and responses to the message blob

            EnhancedBlockStream es = new EnhancedBlockStream();

            try
            {
                Msg.Save(es, query);

                if (response != null)
                    Msg.Save(es, response);

                base._Data = es.ToArray();
            }
            finally
            {
                es.Close();
            }
        }

        /// <summary>
        /// Protected constructor that does not instantiate the property hash table.
        /// </summary>
        /// <param name="param">Pass <see cref="Stub.Param" />.</param>
        private DeliveryMsg(Stub param)
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
            DeliveryMsg clone;

            clone = new DeliveryMsg(Stub.Param);
            clone.CopyBaseFields(this, true);

            clone.query = this.query;
            clone.response = this.response;

            return clone;
        }

        /// <summary>
        /// Handles the deserialization of the <see cref="Query" /> and <see cref="Response" />
        /// messages from the blob data.
        /// </summary>
        /// <param name="es">The enhanced stream holding the payload data.</param>
        protected override void ReadFrom(EnhancedStream es)
        {
            // Let the base message class load the properties and the data blob.

            base.ReadFrom(es);

            // Now parse the query and (optional) response messages from the blob

            es = new EnhancedBlockStream(base._Data);

            try
            {
                query = Msg.Load(es);

                if (es.Eof)
                    response = null;
                else
                    response = Msg.Load(es);
            }
            finally
            {
                es.Close();
            }
        }

        /// <summary>
        /// Indicates how the message instance should be interpreted when received
        /// by an <see cref="IReliableMessenger" /> implementation.
        /// </summary>
        public DeliveryOperation Operation
        {
            get { return (DeliveryOperation)Enum.Parse(typeof(DeliveryOperation), base._Get("operation")); }
            set { base._Set("operation", value.ToString()); }
        }

        /// <summary>
        /// The time (UTC) when delivery was successfully achieved or aborted.
        /// </summary>
        public DateTime Timestamp
        {
            get { return base._Get("timestamp", DateTime.MinValue); }
            set { base._Set("timestamp", value); }
        }

        /// <summary>
        /// The original target or cluster endpoint.
        /// </summary>
        public MsgEP TargetEP
        {
            get { return base._Get("target-ep"); }
            set { base._Set("target-ep", value.ToString()); }
        }

        /// <summary>
        /// The confirmation endpoint (or <c>null</c>).
        /// </summary>
        public MsgEP ConfirmEP
        {
            get
            {
                string ep;

                ep = base._Get("confirm-ep");
                if (ep == null)
                    return null;

                return MsgEP.Parse(ep);
            }

            set
            {
                if (value != null)
                    base._Set("confirm-ep", value.ToString());
            }
        }

        /// <summary>
        /// The globally unique ID for the serialized cluster topology instance ID
        /// (or <see cref="Guid.Empty" />).
        /// </summary>
        /// <remarks>
        /// This property is used by <see cref="IReliableMessenger" /> implementations 
        /// to efficiently cache reconsitiuted cluster client topology instance references.
        /// </remarks>
        public Guid TopologyID
        {
            get { return base._Get("topology-id", Guid.Empty); }

            set
            {
                if (value != Guid.Empty)
                    base._Set("topology-id", value);
            }
        }

        /// <summary>
        /// The serialized client cluster topology information or <c>null</c> if the
        /// message is not targeted at a cluster.
        /// </summary>
        public string TopologyInfo
        {
            get { return base._Get("topology-info"); }
            set { base._Set("topology-info", value); }
        }

        /// <summary>
        /// The serialized topology parameter or <c>null</c>.
        /// </summary>
        public string TopologyParam
        {
            get { return base._Get("topology-param"); }
            set { base._Set("topology-param", value); }
        }

        /// <summary>
        /// The exception to be thrown for failed delivery confirmations.
        /// </summary>
        public Exception Exception
        {
            // I'm going to encode non-null exceptions as <type name>:<message> so
            // that I'll be able to distinuish between TimeoutExceptions and other
            // exception types.

            get
            {

                string      value;
                int         pos;
                string      typeName;
                string      message;

                value = base._Get("exception");
                if (value == null)
                    return null;

                pos = value.IndexOf(':');
                if (pos == -1)
                    return null;

                typeName = value.Substring(0, pos);
                message = value.Substring(pos + 1);

                if (typeName == typeof(TimeoutException).Name)
                    return new TimeoutException(message);
                else
                    return SessionException.Create(null, message);
            }

            set
            {
                if (value == null)
                    return;

                base._Set("exception", string.Format("{0}:{1}", value.GetType().Name, value.Message));
            }
        }

        /// <summary>
        /// Returns the original query message.
        /// </summary>
        public Msg Query
        {
            get { return query; }
        }

        /// <summary>
        /// Returns the reponse message or <c>null</c> if this is not a delivery confirmation
        /// or confirms a failed delivery.
        /// </summary>
        public Msg Response
        {
            get { return response; }
        }
    }

    /// <summary>
    /// The reply to a <see cref="DeliveryMsg" />.
    /// </summary>
    public sealed class DeliveryAck : Ack
    {
        /// <summary>
        /// Returns the type ID string to be used when serializing the message.
        /// </summary>
        public new static string GetTypeID()
        {
            return "LT.DC.Reliable.DeliveryAck";
        }

        /// <summary>
        /// Default constructor.
        /// </summary>
        public DeliveryAck()
        {
        }

        /// <summary>
        /// Use this constructor to pass an exception back to the caller.
        /// </summary>
        /// <param name="e">The exception.</param>
        public DeliveryAck(Exception e)
        {
            base.Exception = e.Message;
        }

        /// <summary>
        /// Protected constructor that does not instantiate the property hash table.
        /// </summary>
        /// <param name="param">Pass <see cref="Stub.Param" />.</param>
        private DeliveryAck(Stub param)
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
            DeliveryAck clone;

            clone = new DeliveryAck(Stub.Param);
            clone.CopyBaseFields(this, true);

            return clone;
        }
    }
}
