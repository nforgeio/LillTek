//-----------------------------------------------------------------------------
// FILE:        LillTekBinding.cs
// CONTRIBUTOR: Jeff Lill
// COPYRIGHT:   Copyright (c) 2005-2015 by Jeffrey Lill.  All rights reserved.
// DESCRIPTION: WCF binding profilefor LillTek Messaging based WCF transports.

using System;
using System.Collections.Generic;
using System.Text;
using System.ServiceModel;
using System.ServiceModel.Channels;

using LillTek.Common;
using LillTek.Messaging;
using LillTek.ServiceModel;
using LillTek.ServiceModel.Channels;

namespace LillTek.ServiceModel
{
    /// <summary>
    /// WCF binding profile for LillTek Messaging based WCF transports.
    /// </summary>
    public class LillTekBinding : Binding
    {
        private BaseTransportBindingElement     transport;
        private MessageEncodingBindingElement   encoding;

        /// <summary>
        /// Constructor.
        /// </summary>
        /// <param name="uri">The transport endpoint address URI (or <c>null</c>).</param>
        public LillTekBinding(Uri uri)
        {
            if (uri == null)
                uri = ServiceModelHelper.CreateUniqueUri();

            switch (uri.Scheme.ToLowerInvariant())
            {
                case "lilltek.logical":

                    transport = new LogicalTransportBindingElement();
                    break;

                case "lilltek.abstract":

                    transport = new AbstractTransportBindingElement();
                    break;

                default:

                    throw new ArgumentException(string.Format("Invalid LillTek Transport scheme [{0}].", uri.Scheme), "uri");
            }

            encoding = new BinaryMessageEncodingBindingElement();
        }

        /// <summary>
        /// Returns the binding's URI scheme.
        /// </summary>
        public override string Scheme
        {
            get { return transport.Scheme; }
        }

        /// <summary>
        /// Creates the collection of binding elements for this binding.
        /// </summary>
        /// <returns>The <see cref="BindingElementCollection" />.</returns>
        public override BindingElementCollection CreateBindingElements()
        {
            var elements = new BindingElementCollection();

            elements.Add(encoding);
            elements.Add(transport);

            return elements.Clone();
        }
    }
}
