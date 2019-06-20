//-----------------------------------------------------------------------------
// FILE:        XmlPropArray.cs
// CONTRIBUTOR: Jeff Lill
// COPYRIGHT:   Copyright (c) 2005-2015 by Jeffrey Lill.  All rights reserved.
// DESCRIPTION: Implements an expandable typesafe array of XmlProperties.

using System;
using System.Diagnostics;
using System.Collections;

namespace LillTek.Xml
{
    /// <summary>
    /// Implements an expandable typesafe array of XmlProperties.
    /// </summary>
    [Obsolete("This is ancient (2005) code originally developed for WinCE devices.  Use Linq-to-XML instead.")]
    public sealed class XmlPropArray : ArrayList
    {
        /// <summary>
        /// Constructs an empty array.
        /// </summary>
        public XmlPropArray()
            : base()
        {
        }

        /// <summary>
        /// Constructs and empty array preallocated to hold count items.
        /// </summary>
        /// <param name="count">Number of items to preallocate for.</param>
        public XmlPropArray(int count)
            : base(count)
        {
        }

        /// <summary>
        /// This method should never be called.  Its purpose is to override the
        /// base class's implementation so as to be able signal invalid uses of 
        /// the class.
        /// </summary>
        /// <param name="o">The object to add.</param>
        public new void Add(object o)
        {
            throw new ArgumentException("Invalid argument type");
        }

        /// <summary>
        /// This method appends the node passed to the array.
        /// </summary>
        /// <param name="node"></param>
        public void Add(XmlProp node)
        {
            base.Add(node);
        }

        /// <summary>
        /// This indexer accesses the item at the zero-based index in the array.
        /// </summary>
        /// <param name="index">Zero-based index of the desired item.</param>
        public new XmlProp this[int index]
        {
            get { return (XmlProp)base[index]; }
            set { base[index] = value; }
        }

        /// <summary>
        /// This indexer returns the first item found with the name passed,
        /// null if there is no node with this name in the list.
        /// </summary>
        /// <param name="name">Name of the desired item.</param>
        public XmlProp this[string name]
        {
            get
            {
                for (int i = 0; i < Count; i++)
                    if (this[i].Name == name)
                        return this[i];

                return null;
            }
        }
    }
}
