//-----------------------------------------------------------------------------
// FILE:        FsConfigBase.cs
// CONTRIBUTOR: Jeff Lill
// COPYRIGHT:   Copyright (c) 2005-2015 by Jeffrey Lill.  All rights reserved.
// DESCRIPTION: Base FreeSWITCH configuration document.

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Xml.Linq;

using FreeSWITCH;
using FreeSWITCH.Native;

using LillTek.Common;
using LillTek.Telephony.Common;

namespace LillTek.Telephony.NeonSwitch
{
    /// <summary>
    /// Base FreeSWITCH configuration document.
    /// </summary>
    internal abstract class FsConfigBase
    {
        public XElement Root;

        /// <summary>
        /// Constructs an empty configuration document for the named configuation section.
        /// </summary>
        /// <param name="sectionName">The section name.</param>
        protected FsConfigBase(string sectionName)
        {
            this.Root =
                new XElement("document",
                    new XAttribute("type", "freeswitch/xml"),
                    new XElement("section",
                    new XAttribute("name", sectionName),
                    new XAttribute("description", "generated")));
        }

        /// <summary>
        /// Renders the document as XML.
        /// </summary>
        /// <returns>The XML string.</returns>
        public String ToXml()
        {
            return Root.ToString();
        }

        /// <summary>
        /// Appends a <see cref="XElement" /> to the list of elements beneath the document's
        /// <b>section</b> element.
        /// </summary>
        /// <param name="element">The element being added.</param>
        protected void AddSectionChild(XElement element)
        {
            if (Root != null)
                Root.Element("section").Add(element);
        }
    }
}
