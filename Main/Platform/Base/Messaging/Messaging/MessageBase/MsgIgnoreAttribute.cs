//-----------------------------------------------------------------------------
// FILE:        MsgIgnoreAttribute.cs
// CONTRIBUTOR: Jeff Lill
// COPYRIGHT:   Copyright (c) 2005-2015 by Jeffrey Lill.  All rights reserved.
// DESCRIPTION: Defines the MsgIgnore attribute used to control whether
//              Msg.LoadTypes() should skip mapping the message class.

using System;

namespace LillTek.Messaging
{
    /// <summary>
    /// Instructs <see cref="Msg.LoadTypes" /> to skip mapping the tagged class.
    /// </summary>
    [AttributeUsage(AttributeTargets.Class, AllowMultiple = false, Inherited = false)]
    public sealed class MsgIgnoreAttribute : System.Attribute
    {
        /// <summary>
        /// Constructor.
        /// </summary>
        public MsgIgnoreAttribute()
        {

        }
    }
}
