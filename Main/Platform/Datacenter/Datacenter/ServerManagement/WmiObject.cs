//-----------------------------------------------------------------------------
// FILE:        WmiObject.cs
// CONTRIBUTOR: Jeff Lill
// COPYRIGHT:   Copyright (c) 2005-2015 by Jeffrey Lill.  All rights reserved.
// DESCRIPTION: The WMI object returned in a <see cref="WmiResultSet" /> generated
//              for a WmiQuery submitted to a remote server.

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
    /// The WMI object returned in a <see cref="WmiResultSet" /> generated
    /// for a <see cref="WmiQuery" /> submitted to a remote server.
    /// </summary>
    [DataContract(Namespace = ServerManager.ContractNamespace)]
    public sealed class WmiObject : IEnumerable
    {
        /// <summary>
        /// The collection of the object's <see cref="WmiProperty" /> values.
        /// </summary>
        [DataMember]
        public Dictionary<string, WmiProperty> Properties;

        /// <summary>
        /// Default constructor used by serializers.
        /// </summary>
        public WmiObject()
        {
        }

        /// <summary>
        /// Initializes the object with the properties from the .NET Framework
        /// WMI object instance passed.
        /// </summary>
        /// <param name="wmiObj">The source object.</param>
        public WmiObject(ManagementObject wmiObj)
        {
            Properties = new Dictionary<string, WmiProperty>(wmiObj.Properties.Count);

            foreach (PropertyData prop in wmiObj.Properties)
                Properties.Add(prop.Name, new WmiProperty(prop.Name, prop.Value != null ? prop.Value.ToString() : null));
        }

        /// <summary>
        /// Returns the value associated with the specified property name (or <c>null</c>).
        /// </summary>
        /// <param name="name">The property name.</param>
        /// <remarks>
        /// <note>
        /// The property name is case senstive.
        /// </note>
        /// </remarks>
        public string this[string name]
        {
            get
            {
                WmiProperty value;

                if (Properties.TryGetValue(name, out value))
                    return value.Value;
                else
                    return null;
            }
        }

        /// <summary>
        /// Returns an enumerator over the set of <see cref="WmiProperty" /> instances
        /// associated with this object.
        /// </summary>
        public IEnumerator GetEnumerator()
        {
            return Properties.GetEnumerator();
        }
    }
}
