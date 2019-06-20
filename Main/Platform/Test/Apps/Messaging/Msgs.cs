//-----------------------------------------------------------------------------
// FILE:        Msgs.cs
// OWNER:       Jeff Lill
// COPYRIGHT:   Copyright (c) 2005-2015 by Jeffrey Lill.  All rights reserved.
// DESCRIPTION: Application message classes.

using System;
using System.Collections.Generic;
using System.Reflection;
using System.Windows.Forms;

using LillTek.Common;
using LillTek.Messaging;

namespace LillTek.Test.Messaging
{
    /// <summary>
    /// Implements a test query message.
    /// </summary>
    public class QueryMsg : BlobPropertyMsg
    {
        /// <summary>
        /// Returns the string to be used to identify the class
        /// for message serialization.
        /// </summary>
        /// <returns></returns>
        public new static string GetTypeID()
        {
            return "LT.Test.Query";
        }

        /// <summary>
        /// Constructor.
        /// </summary>
        public QueryMsg()
            : this(0)
        {
        }

        /// <summary>
        /// Constructor.
        /// </summary>
        public QueryMsg(int cbPayload)
            : base()
        {
            if (cbPayload > 0)
                base._Data = new byte[cbPayload];
        }
    }

    /// <summary>
    /// Implements a test reponse message.
    /// </summary>
    public class ResponseMsg : BlobPropertyMsg
    {
        /// <summary>
        /// Returns the string to be used to identify the class
        /// for message serialization.
        /// </summary>
        /// <returns></returns>
        public new static string GetTypeID()
        {
            return "LT.Test.Response";
        }

        /// <summary>
        /// Constructor.
        /// </summary>
        public ResponseMsg()
            : this(0)
        {
        }

        /// <summary>
        /// Constructor.
        /// </summary>
        public ResponseMsg(int cbPayload)
            : base()
        {
            if (cbPayload > 0)
                base._Data = new byte[cbPayload];
        }
    }
}
