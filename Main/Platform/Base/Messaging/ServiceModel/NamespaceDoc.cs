//-----------------------------------------------------------------------------
// FILE:        NamespaceDoc.cs
// CONTRIBUTOR: Jeff Lill
// COPYRIGHT:   Copyright (c) 2005-2015 by Jeffrey Lill.  All rights reserved.
// DESCRIPTION: Namespace documentation.

using System;
using System.Collections.Generic;
using System.IO;
using System.ServiceModel;
using System.ServiceModel.Channels;
using System.Text;
using System.Threading;

using LillTek.Common;
using LillTek.Messaging;

namespace LillTek.ServiceModel
{
    /// <summary>
    /// Extends the .NET Framework Windows Communication Foundation
    /// with custom message transports based on LillTek Messaging.
    /// </summary>
    /// <remarks>
    /// <para>
    /// The <b>LillTek.ServiceModel</b> namespace implements Windows Communication
    /// Foundation (WCF) compatible transport channels on top of 
    /// <see cref="LillTek.Messaging.OverviewDoc">LillTek Messaging</see>.  This
    /// combines all of WCFs capabilities with all of the benefits of LillTek Messaging,
    /// including:
    /// </para>
    /// <list type="bullet">
    ///     <item>Integrated service discovery</item>
    ///     <item>Built-in load balancing</item>
    ///     <item>Automatic failover at the request level</item>
    ///     <item>Logical endpoint addressing</item>
    ///     <item>By-directional firewall traversal</item>
    ///     <item>Cross datacenter routing</item>
    ///     <item>Message broadcasting</item>
    ///     <item>And <see cref="LillTek.Messaging.OverviewDoc">more</see></item>
    /// </list>
    /// <para>
    /// $todo(jeff.lill): Complete the overview on how to use this.
    /// </para>
    /// <para><b><u>Important Notes</u></b></para>
    /// <para>
    /// LillTek channel listener implementations do not completely adhere to
    /// the defined WCF semantics.  Specifically, closing a LillTek channel listener
    /// will cause message receive operations to fail for all open channels accepted
    /// by the listener.  In some cases, these operations will fail with a <see cref="TimeoutException" />.
    /// In other cases, these will fail with an <see cref="ObjectDisposedException" />.
    /// </para>
    /// <para>
    /// This conflicts slightly with my understanding of the required listener
    /// samantics as described on MSDN, which states that accepted channels must
    /// remain open after their channel listener has been closed.  Although the
    /// LillTek implementation doesn't explicitly close these channels, they
    /// will no longer be able to receive messages.  Send operations will continue
    /// to function.
    /// </para>
    /// <para>
    /// Another issue reovolves around timeouts.  The current implementation will
    /// honor most timeout value passed to the transports.  The exceptions to this
    /// include timeouts passed to sessionful channel's <b>Open()</b> and <b>Request()</b>
    /// related methods.  LillTek messaging currently implements only global router
    /// timeouts and it is not possible to specify specific timeout for specific
    /// calls to these methods.  This will be corrected in a future build. 
    /// </para>
    /// </remarks>
    public sealed class OverviewDoc
    {
    }
}

