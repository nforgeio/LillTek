//-----------------------------------------------------------------------------
// FILE:        CustomAttribute.cs
// CONTRIBUTOR: Jeff Lill
// COPYRIGHT:   Copyright (c) 2005-2015 by Jeffrey Lill.  All rights reserved.
// DESCRIPTION: Defines the CustomAttribute class which is used to abstract
//              an attribute located in an external assembly.

using System;
using System.IO;
using System.Reflection;
using System.Diagnostics;
using System.Collections;
using System.Collections.Specialized;

namespace LillTek.Common
{
    /// <summary>
    /// Abstracts an attribute located in an external assembly.  This is handy mainly
    /// for quering the properties of the attribute.
    /// </summary>
    public class CustomAttribute
    {
        private object attribute;

        /// <summary>
        /// This static method scans the attribute array passed for the 
        /// attribute whose fully qualified name matches the name passed.  
        /// The method returns the custom attribute if found, null
        /// otherwise.
        /// </summary>
        /// <param name="attributes">The attributes to search.</param>
        /// <param name="fullName">The fully qualified name of the desired attribute.</param>
        public static CustomAttribute Get(object[] attributes, string fullName)
        {
            for (int i = 0; i < attributes.Length; i++)
                if (((System.Attribute)attributes[i]).GetType().FullName == fullName)
                    return new CustomAttribute(attributes[i]);

            return null;
        }

        /// <summary>
        /// This method initializes the custom attribute to reference
        /// the attribute passed.
        /// </summary>
        /// <param name="attribute">The attribute.</param>
        public CustomAttribute(object attribute)
        {
            this.attribute = attribute;
        }

        /// <summary>
        /// This method returns the object value of the named attribute property.
        /// The method throws an exception if the property named does not exist.
        /// </summary>
        /// <param name="name">The property name.</param>
        public object GetProp(string name)
        {
            return attribute.GetType().InvokeMember(name, BindingFlags.Public | BindingFlags.Instance | BindingFlags.GetProperty, null, attribute, null);
        }

        /// <summary>
        /// This method returns the integer value of the named attribute property.
        /// The method throws an exception if the property named does not exist.
        /// </summary>
        /// <param name="name">The property name.</param>
        public int GetPropInt(string name)
        {
            return (int)GetProp(name);
        }

        /// <summary>
        /// This method returns the string value of the named attribute property.
        /// The method throws an exception if the property named does not exist.
        /// </summary>
        /// <param name="name">The property name.</param>
        public string GetPropStr(string name)
        {
            return (string)GetProp(name);
        }

        /// <summary>
        /// This method returns the boolean value of the named attribute property.
        /// The method throws an exception if the property named does not exist.
        /// </summary>
        /// <param name="name">The property name.</param>
        public bool GetPropBool(string name)
        {
            return (bool)GetProp(name);
        }
    }
}
