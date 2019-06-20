//-----------------------------------------------------------------------------
// FILE:        MsgHandlerAttribute.cs
// CONTRIBUTOR: Jeff Lill
// COPYRIGHT:   Copyright (c) 2005-2015 by Jeffrey Lill.  All rights reserved.
// DESCRIPTION: Defines the [MsgHandler] attribute used to tag methods of an
//              instance that handle a specific message type.

using System;

namespace LillTek.Messaging
{
    /// <summary>
    /// Used for tagging methods to be called as handlers for specific
    /// message types.
    /// </summary>
    /// <remarks>
    /// <para>
    /// This attribute can be used to tag methods for handling messages
    /// targeted at both logical and physical endpoints.  By default, the 
    /// attribute tags handlers for messages targeted at physical endpoints
    /// as in:
    /// </para>
    /// <code language="cs">
    /// [MsgHandler]
    /// public void MyHandler1(MyMsgType1 msg) {
    /// 
    /// }
    /// </code>
    /// <para>
    /// This example tags the MyHandler1() method so that any messages of type
    /// MyMsgType1 targeted the router's physical endpoint will be dispatched
    /// to this method.
    /// </para>
    /// <para> 
    /// For logical endpoints, specify a valid logical endpoint in the LogicalEP
    /// parameter as in:
    /// </para>
    /// <code language="cs">
    /// [MsgHandler(LogicalEP="logical://MyApplication/*"]
    /// public void MyHandler2(MsgType2 msg) {
    /// 
    /// }
    /// </code>
    /// <para>
    /// This example tags the MyHandler2() method so that messages of
    /// type MsgType2 targeted at a physical endpoint that matches
    /// logical://MyApplication/* will be dispatched to this method.
    /// </para>
    /// <para>
    /// The attribute supports the additional Default parameters. Pass 
    /// <b>Default=true</b> to indicate that messages with types that 
    /// don't match any other tagged method parameters should be dispatched
    /// to the method.
    /// </para>
    /// <note>
    /// Default is valid for methods tagged to handle both 
    /// logical and physical endpoints.
    /// </note>
    /// </remarks>
    [AttributeUsage(AttributeTargets.Method, AllowMultiple = true, Inherited = false)]
    public sealed class MsgHandlerAttribute : System.Attribute
    {
        private string logicalEP;      // The logical endpoint (or null)

        /// <summary>
        /// Set to <c>true</c> if the associated method should receive all messages
        /// not specifically mapped to another method (defaults to false).
        /// </summary>
        public bool Default;

        /// <summary>
        /// Set to a non-<c>null</c> dynamic scope name if the logical endpoint has the 
        /// potential to be modified dynamically at runtime just before the message 
        /// handler is registered with an <see cref="IMsgDispatcher" />.  This name will
        /// be presented to the <see cref="IDynamicEPMunger.Munge" /> method so that
        /// the method can determine whether the tagged message handler belongs to
        /// the munger our not.  See <see cref="IDynamicEPMunger" /> for more information. 
        /// (Defaults to null).
        /// </summary>
        public string DynamicScope;

        /// <summary>
        /// The logical endpoint associated with the message handler or <c>null</c>
        /// if the handler is to be used for processing messages targeted
        /// at physcial endpoints.
        /// </summary>
        public string LogicalEP
        {
            get { return logicalEP; }

            set
            {
                MsgEP ep;

                try
                {
                    ep = MsgEP.Parse(value);
                }
                catch
                {
                    throw new FormatException("Logical endpoint in [MsgHandler] attribute is not valid.");
                }

                if (!ep.IsLogical)
                    throw new FormatException("Only logical endpoints are valid in [MsgHandler] attributes.");

                logicalEP = value;
            }
        }

        /// <summary>
        /// Returns <c>true</c> if the tagged method handles messages targeted at logical endpoints.
        /// </summary>
        public bool IsLogicalHandler
        {
            get { return logicalEP != null; }
        }

        /// <summary>
        /// Returns <c>true</c> if the tagged method handles messages targeted at physical endpoints.
        /// </summary>
        public bool IsPhysicalHandler
        {
            get { return logicalEP == null; }
        }

        /// <summary>
        /// This constructor intalizes the attribute to tag an application
        /// message handler.
        /// </summary>
        public MsgHandlerAttribute()
        {
            this.Default      = false;
            this.DynamicScope = null;
            this.logicalEP    = null;
        }
    }
}
