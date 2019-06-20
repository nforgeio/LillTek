//-----------------------------------------------------------------------------
// FILE:        Ack.cs
// CONTRIBUTOR: Jeff Lill
// COPYRIGHT:   Copyright (c) 2005-2015 by Jeffrey Lill.  All rights reserved.
// DESCRIPTION: The default implementation of an IAck message, based on
//              the PropertyMsg class.

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
    /// The default implementation of an IAck message, based on
    /// the <see cref="PropertyMsg" /> class.
    /// </summary>
    public class Ack : PropertyMsg, IAck
    {
        //---------------------------------------------------------------------
        // Static members

        /// <summary>
        /// Returns the type ID string to be used when serializing the message.
        /// </summary>
        public static new string GetTypeID()
        {
            return ".Ack";
        }

        //---------------------------------------------------------------------
        // Instance members

        /// <summary>
        /// Constructor.
        /// </summary>
        public Ack()
            : base()
        {
        }

        /// <summary>
        /// Constructor.
        /// </summary>
        /// <param name="error">The error message.</param>
        public Ack(string error)
            : base()
        {
            this.Exception         = error;
            this.ExceptionTypeName = typeof(SessionException).FullName;
        }

        /// <summary>
        /// Constructor
        /// </summary>
        /// <param name="e">The exception.</param>
        public Ack(Exception e)
            : base()
        {
            this.Exception         = e.Message;
            this.ExceptionTypeName = e.GetType().FullName;
        }

        /// <summary>
        /// Protected constructor that does not instantiate the property hash table.
        /// </summary>
        /// <param name="param">Pass <see cref="Stub.Param" />.</param>
        /// <remarks>
        /// Derived classes should use this constructor passing false when overriding 
        /// <see cref="Msg.Clone" /> to avoid creating an extra field object instances.
        /// </remarks>
        protected Ack(Stub param)
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
            Ack clone;

            clone = new Ack(Stub.Param);
            clone.CopyBaseFields(this, true);

            return clone;
        }

        /// <summary>
        /// The exception's message string if the was an exception detected
        /// on by the server (null or the empty string if there was no error).
        /// </summary>
        public string Exception
        {
            get { return (string)base["_exception"]; }
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
