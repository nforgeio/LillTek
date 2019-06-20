//-----------------------------------------------------------------------------
// FILE:        ObjectGraphMsg.cs
// CONTRIBUTOR: Jeff Lill
// COPYRIGHT:   Copyright (c) 2005-2015 by Jeffrey Lill.  All rights reserved.
// DESCRIPTION: Implements a general purpose message that serializes an object
//              graph using Serialize.ToBinary(),

using System;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Reflection;
using System.Text;

using LillTek.Common;

namespace LillTek.Messaging
{
    /// <summary>
    /// Implements a general purpose message that serializes an object
    /// graph using <see cref="Serialize.ToBinary(object,Compress)" />.
    /// </summary>
    public class ObjectGraphMsg : Msg
    {
        //---------------------------------------------------------------------
        // Static members

        /// <summary>
        /// Returns the type ID string to be used when serializing the message.
        /// </summary>
        public static string GetTypeID()
        {
            return ".ObjectGraph";
        }

        //---------------------------------------------------------------------
        // Instance members

        private Compress    compress = Compress.Best;
        private object      graph    = null;

        /// <summary>
        /// Constructor.
        /// </summary>
        /// <remarks>
        /// This overload initializes the message for <see cref="LillTek.Common.Compress.Best" /> compression.
        /// </remarks>
        public ObjectGraphMsg()
        {
        }

        /// <summary>
        /// Constructor.
        /// </summary>
        /// <param name="graph">The graph to be serialized.</param>
        /// <remarks>
        /// This overload initializes the message for <see cref="LillTek.Common.Compress.Best" /> compression.
        /// </remarks>
        public ObjectGraphMsg(object graph)
        {
            this.graph = graph;
        }

        /// <summary>
        /// Constructor.
        /// </summary>
        /// <param name="graph">The graph to be serialized.</param>
        /// <param name="compress">A <see cref="Compress" /> setting to use when compressing the object graph for transport.</param>
        /// <remarks>
        /// This overload initializes the message for <see cref="LillTek.Common.Compress.Best" /> compression.
        /// </remarks>
        public ObjectGraphMsg(object graph, Compress compress)
        {
            this.graph    = graph;
            this.compress = compress;
        }

        /// <summary>
        /// Shallow copies the base fields from the source message to this instance.
        /// </summary>
        /// <param name="source">The source message.</param>
        /// <param name="regenMsgID">
        /// Pass as <c>true</c> to renegerate the <see cref="Msg._MsgID" /> property if the 
        /// source message ID property is not empty.
        /// </param>
        /// <remarks>
        /// Use this in overriden <see cref="Msg.Clone" /> method implementations
        /// to ensure that the base message fields are copied properly.
        /// </remarks>
        protected override void CopyBaseFields(Msg source, bool regenMsgID)
        {
            var src = (ObjectGraphMsg)source;

            base.CopyBaseFields(source, regenMsgID);

            this.graph = src.graph;
            this.compress = src.compress;
        }

        /// <summary>
        /// The object graph encapsulated by this message.
        /// </summary>
        public object Graph
        {
            get { return graph; }
            set { graph = value; }
        }

        /// <summary>
        /// A <see cref="Compress" /> value specifying how the object is to be compressed
        /// when serializing the message.  This defaults to <see cref="LillTek.Common.Compress.Best" />.
        /// </summary>
        /// <remarks>
        /// <note>
        /// This property is not serialized with the message.  This will always be initialized
        /// to the default value of <see cref="LillTek.Common.Compress.Best" /> after a message is deserialized.
        /// </note>
        /// </remarks>
        public Compress Compress
        {
            get { return compress; }
            set { compress = value; }
        }

        /// <summary>
        /// Serializes the payload of the base classes into the stream.
        /// </summary>
        /// <param name="es">The enhanced stream where the output is to be written.</param>
        /// <remarks>
        /// Classes that are designed to be derived from should implement
        /// this method to serialize their content.  Note that the base.WriteBase()
        /// method should be called before doing this to ensure that any 
        /// ancestor classes will be serialized properly.
        /// </remarks>
        protected override void WriteBase(EnhancedStream es)
        {
            base.WriteBase(es);
            Serialize.ToBinary(es, graph, Compress.Always);
        }

        /// <summary>
        /// Loads the message payload of the base classes from the stream passed.
        /// </summary>
        /// <param name="es">The enhanced stream holding the payload data.</param>
        /// <remarks>
        /// Classes that are designed to be derived from should implement
        /// this method to serialize their content.  Note that the base.ReadFrom()
        /// method should be called before doing this to ensure that any 
        /// ancestor classes will be unserialized properly.
        /// </remarks>
        protected override void ReadFrom(EnhancedStream es)
        {
            base.ReadFrom(es);
            graph = Serialize.FromBinary(es);
        }

        /// <summary>
        /// Add detailed trace information about this message to the
        /// StringBuilder passed.
        /// </summary>
        /// <param name="router">The associated router (or <c>null</c>).</param>
        /// <param name="sb">The string builder.</param>
        /// <remarks>
        /// Adds the name/value pairs to the information returned
        /// by the base class.
        /// </remarks>
        public override void _TraceDetails(MsgRouter router, StringBuilder sb)
        {
            base._TraceDetails(router, sb);

            sb.Append("\r\nObjectGraph");
        }
    }
}
