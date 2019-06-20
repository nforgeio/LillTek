//-----------------------------------------------------------------------------
// FILE:        XmlProp.cs
// CONTRIBUTOR: Jeff Lill
// COPYRIGHT:   Copyright (c) 2005-2015 by Jeffrey Lill.  All rights reserved.
// DESCRIPTION: Holds a XmlNode property (a name/value pair).

using System;
using System.Diagnostics;
using System.Collections;
using System.Xml;

namespace LillTek.Xml
{
    /// <summary>
    /// Holds a XmlNode property (a name/value pair).
    /// </summary>
    [Obsolete("This is ancient (2005) code originally developed for WinCE devices.  Use Linq-to-XML instead.")]
    public sealed class XmlProp
    {
        private string name;   // Name of the property name
        private string value;  // Value of the property as a string

        /// <summary>
        /// Constructs the XML property.
        /// </summary>
        /// <param name="name">Property name.</param>
        /// <param name="value">Property value.</param>
        public XmlProp(string name, string value)
        {
            this.name  = name;
            this.value = value;
        }

        /// <summary>
        /// Constructs the XML property.
        /// </summary>
        /// <param name="name">Property name.</param>
        /// <param name="value">Property value.</param>
        public XmlProp(string name, int value)
        {
            this.name  = name;
            this.value = value.ToString();
        }

        /// <summary>
        /// Constructs the XML property.
        /// </summary>
        /// <param name="name">Property name.</param>
        /// <param name="value">Property value.</param>
        public XmlProp(string name, uint value)
        {
            this.name  = name;
            this.value = value.ToString();
        }

        /// <summary>
        /// Constructs the XML property.
        /// </summary>
        /// <param name="name">Property name.</param>
        /// <param name="value">Property value.</param>
        public XmlProp(string name, ulong value)
        {
            this.name  = name;
            this.value = value.ToString();
        }

        /// <summary>
        /// Constructs the XML property.
        /// </summary>
        /// <param name="name">Property name.</param>
        /// <param name="value">Property value.</param>
        public XmlProp(string name, bool value)
        {
            this.name  = name;
            this.value = value ? "True" : "False";
        }

        internal XmlProp(XmlAttribute attr)
        {
            this.name  = attr.Name;
            this.value = attr.Value;
        }

        /// <summary>
        /// This property access's the XML property's name.
        /// </summary>
        public string Name
        {
            get { return this.name; }
            set { this.name = value; }
        }

        /// <summary>
        /// This property accesses the XML property's value.
        /// </summary>
        public string Value
        {
            get { return this.value; }
            set { this.value = value; }
        }

        /// <summary>
        /// This method return the property as a typed value.  The methods
        /// return true on success, <c>false</c> if the value could not be converted
        /// to the requested type. Signed decimal numbers.
        /// </summary>
        /// <param name="val">Destination variable.</param>
        public bool Get(out int val)
        {
            try
            {
                val = int.Parse(this.value);
                return true;
            }
            catch
            {
                val = 0;
                return false;
            }
        }

        /// <summary>
        /// This method return the property as a typed value.  The methods
        /// return true on success, <c>false</c> if the value could not be converted
        /// to the requested type. Unsigned decimal integers.
        /// </summary>
        /// <param name="val">Destination variable.</param>
        public bool Get(out uint val)
        {
            try
            {
                val = uint.Parse(this.value);
                return true;
            }
            catch
            {
                val = 0;
                return false;
            }
        }

        /// <summary>
        /// This method return the property as a typed value.  The methods
        /// return true on success, <c>false</c> if the value could not be converted
        /// to the requested type. 64-bit unsigned integers.
        /// </summary>
        /// <param name="val">Destination variable.</param>
        public bool Get(out ulong val)
        {
            try
            {
                val = ulong.Parse(this.value);
                return true;
            }
            catch
            {
                val = 0;
                return false;
            }
        }

        /// <summary>
        /// This method return the property as a typed value.  The methods
        /// return true on success, <c>false</c> if the value could not be converted
        /// to the requested type. 0/1, true/false, yes/no.
        /// </summary>
        /// <param name="val">Destination variable.</param>
        public bool Get(out bool val)
        {
            var upr = this.value.ToUpper();

            if (this.value == "1" || upr == "TRUE" || upr == "YES")
                val = true;
            else if (this.value == "0" || upr == "FALSE" || upr == "NO")
                val = false;
            else
            {
                val = false;
                return false;
            }

            return true;
        }

        /// <summary>
        /// This method return the property as a typed value.  The methods
        /// return true on success, <c>false</c> if the value could not be converted
        /// to the requested type. 0/1, true/false, yes/no.
        /// </summary>
        /// <param name="val">The destination variable.</param>
        /// <param name="str">The string to be assigned.</param>
        static internal bool Get(out bool val, string str)
        {
            var upr = str.ToUpper();

            if (upr == "1" || upr == "TRUE" || upr == "YES")
                val = true;
            else if (upr == "0" || upr == "FALSE" || upr == "NO")
                val = false;
            else
            {

                val = false;
                return false;
            }

            return true;
        }
    }
}
