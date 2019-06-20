//-----------------------------------------------------------------------------
// FILE:        WmiProperty.cs
// CONTRIBUTOR: Jeff Lill
// COPYRIGHT:   Copyright (c) 2005-2015 by Jeffrey Lill.  All rights reserved.
// DESCRIPTION: The WMI name/value pair associated with a WmiObject
//              in a WmiResult.

using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Management;
using System.Runtime.Serialization;
using System.ServiceModel;
using System.Text;

using LillTek.Common;
using LillTek.Service;

namespace LillTek.Datacenter.ServerManagement
{
    /// <summary>
    /// The WMI name/value pair associated with a <see cref="WmiObject" />
    /// in a <see cref="WmiResult" />.
    /// </summary>
    [DataContract(Namespace = ServerManager.ContractNamespace)]
    public sealed class WmiProperty
    {
        /// <summary>
        /// The property name.
        /// </summary>
        public string Name { get; set; }

        /// <summary>
        /// The property value encoded as a string (note that this may also be
        /// set to null).
        /// </summary>
        public string Value { get; set; }

        /// <summary>
        /// Default constructor used by serializers.
        /// </summary>
        public WmiProperty()
        {
        }

        /// <summary>
        /// Constructs an instance from the parameters passed.
        /// </summary>
        /// <param name="name">Property name.</param>
        /// <param name="value">Property value (encoded as a string or <c>null</c>).</param>
        public WmiProperty(string name, string value)
        {
            this.Name  = name;
            this.Value = value;
        }
    }
}
