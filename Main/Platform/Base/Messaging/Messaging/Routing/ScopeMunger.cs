//-----------------------------------------------------------------------------
// FILE:        ScopeMunger.cs
// CONTRIBUTOR: Jeff Lill
// COPYRIGHT:   Copyright (c) 2005-2015 by Jeffrey Lill.  All rights reserved.
// DESCRIPTION: Associates a dynamic scope name and an IDynamicEPMunger together
//              so they can be easily passed to IMsgDispatcher.AddTarget().

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;

using LillTek.Advanced;
using LillTek.Common;
using LillTek.Cryptography;
using LillTek.Messaging.Internal;
using LillTek.Net.Broadcast;
using LillTek.Net.Sockets;

namespace LillTek.Messaging
{
    /// <summary>
    /// Associates a dynamic scope name and an IDynamicEPMunger together so they can 
    /// be easily passed to <see cref="IMsgDispatcher" />.<see cref="IMsgDispatcher.AddTarget(object,IEnumerable{ScopeMunger},object)" />.
    /// </summary>
    public class ScopeMunger
    {
        /// <summary>
        /// Constructor.
        /// </summary>
        /// <param name="dynamicScope">The dynamic scope name.</param>
        /// <param name="munger">The associated <see cref="IDynamicEPMunger" /> implementation.</param>
        public ScopeMunger(string dynamicScope, IDynamicEPMunger munger)
        {
            this.DynamicScope = dynamicScope;
            this.Munger       = munger;
        }

        /// <summary>
        /// The dynamic scope name.
        /// </summary>
        public string DynamicScope { get; private set; }

        /// <summary>
        /// The associated <see cref="IDynamicEPMunger" /> implementation.
        /// </summary>
        public IDynamicEPMunger Munger { get; private set; }
    }
}
