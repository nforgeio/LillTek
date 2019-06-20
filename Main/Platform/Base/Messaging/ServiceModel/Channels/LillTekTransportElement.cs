//-----------------------------------------------------------------------------
// FILE:        LillTekTransportElement.cs
// CONTRIBUTOR: Jeff Lill
// COPYRIGHT:   Copyright (c) 2005-2015 by Jeffrey Lill.  All rights reserved.
// DESCRIPTION: Implements a BindingElementExtensionElement that can create
//              bindings for any of the LillTek Messaging based WCF transports.

using System;
using System.Collections.Generic;
using System.Text;
using System.Configuration;
using System.ServiceModel;
using System.ServiceModel.Channels;
using System.ServiceModel.Configuration;

using LillTek.Common;
using LillTek.Messaging;
using LillTek.ServiceModel;

namespace LillTek.ServiceModel.Channels
{
    /// <summary>
    /// Implements a <see cref="BindingElementExtensionElement" /> that can create
    /// bindings for any of the LillTek Messaging based WCF transports.
    /// </summary>
    /// <remarks>
    /// <para>
    /// This class is used in .NET application configuration files to extend WCF
    /// by adding the LillTek Messaging based transports.  Here's an example of
    /// how this would be used in a configuration file:
    /// </para>
    /// <code language="none">
    /// 
    /// // $todo(jeff.lill): This example is almost certainly wrong.  Redo this
    /// //                   after figuring out what will actually work.
    /// 
    /// &lt;?xml version="1.0" encoding="utf-8" ?&gt;
    /// &lt;configuration&gt;
    ///     &lt;system.serviceModel&gt;
    ///         &lt;services&gt;
    ///             &lt;service name="MyService"&gt;
    /// 				&lt;host&gt;
    /// 					&lt;baseAddresses&gt;
    /// 						&lt;add baseAddress="lilltek.logical://MyService"/&gt;
    /// 					&lt;/baseAddresses&gt;
    /// 				&lt;/host&gt;
    ///                 &lt;endpoint address="Endpoint"
    ///                     binding="customBinding" 
    /// 					bindingConfiguration="lillTekBinding" 
    /// 					contract="IMyServiceContract" /&gt;
    ///             &lt;/service&gt;
    ///         &lt;/services&gt;
    /// 		&lt;bindings&gt;
    /// 			&lt;customBinding&gt;
    /// 				&lt;binding name="lillTekBinding"&gt;
    /// 					&lt;logicalTransport/&gt;
    /// 					&lt;abstractTransport/&gt;
    /// 				&lt;/binding&gt;
    /// 			&lt;/customBinding&gt;
    /// 		&lt;/bindings&gt;
    /// 		&lt;extensions&gt;
    /// 			&lt;bindingExtensions&gt;
    /// 				&lt;add name="lilltekTransport"  type="LillTek.ServiceModel.Channels, LillTekTransportElement"/&gt;
    /// 			&lt;/bindingExtensions&gt;
    /// 			&lt;bindingElementExtensions&gt;
    /// 				&lt;add name="logicalTransport"  type="LillTek.ServiceModel.Channels, LogicalTransportElement"/&gt;
    /// 				&lt;add name="abstractTransport" type="LillTek.ServiceModel.Channels, AbstractTransportElement"/&gt;
    /// 			&lt;/bindingElementExtensions&gt;
    /// 		&lt;/extensions&gt;
    ///     &lt;/system.serviceModel&gt;
    /// &lt;/configuration&gt;
    /// </code>
    /// </remarks>
    public sealed class LillTekTransportElement : BindingElementExtensionElement
    {
        /// <summary>
        /// Default constructor.
        /// </summary>
        public LillTekTransportElement()
        {
        }

        /// <summary>
        /// Returns the type of the custom extension element associated with this class.
        /// </summary>
        public override System.Type BindingElementType
        {
            get { return typeof(LogicalTransportBindingElement); }
        }

        /// <summary>
        /// Creates the custom extension binding element associated with this class.
        /// </summary>
        /// <returns>The new <see cref="BindingElement" />.</returns>
        protected override BindingElement CreateBindingElement()
        {
            return new LogicalTransportBindingElement();
        }
    }
}
