//-----------------------------------------------------------------------------
// FILE:        AbstractTransportBindingElement.cs
// CONTRIBUTOR: Jeff Lill
// COPYRIGHT:   Copyright (c) 2005-2015 by Jeffrey Lill.  All rights reserved.
// DESCRIPTION: Implements a TransportBindingElement capable of creating 
//              channel factories and listeners as appropriate for all LillTek 
//              Messaging based channel implementations that use the 
//              lilltek.abstract:// addressing scheme.

using System;
using System.Collections.Generic;
using System.Text;
using System.ServiceModel;
using System.ServiceModel.Channels;

using LillTek.Common;
using LillTek.Messaging;
using LillTek.ServiceModel;

namespace LillTek.ServiceModel.Channels
{
    /// <summary>
    /// Implements a <see cref="TransportBindingElement" /> capable of creating 
    /// channel factories and listeners as appropriate for all LillTek 
    /// Messaging based channel implementations that use the 
    /// <b>lilltek.abstract://</b> addressing scheme.
    /// </summary>
    public class AbstractTransportBindingElement : BaseTransportBindingElement
    {
        /// <summary>
        /// Default constructor.
        /// </summary>
        public AbstractTransportBindingElement()
            : base()
        {
        }

        /// <summary>
        /// Returns the addressing scheme implemented by the transport.
        /// </summary>
        public override string Scheme
        {
            get { return "lilltek.abstract"; }
        }

        /// <summary>
        /// Creates a clone of the <see cref="BindingElement" />.
        /// </summary>
        /// <returns></returns>
        public override BindingElement Clone()
        {
            return new AbstractTransportBindingElement();
        }
    }
}
