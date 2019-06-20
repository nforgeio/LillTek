//-----------------------------------------------------------------------------
// FILE:        FsConfigNotFound.cs
// CONTRIBUTOR: Jeff Lill
// COPYRIGHT:   Copyright (c) 2005-2015 by Jeffrey Lill.  All rights reserved.
// DESCRIPTION: The document to be returned to FreeSWITCH when the requested 
//              item could not be found by a configuration event handler.

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
    /// The document to be returned to FreeSWITCH when the requested item could not 
    /// be found by a configuration event handler.
    /// </summary>
    internal class FsConfigNotFound : FsConfigBase
    {
        //---------------------------------------------------------------------
        // Static members

        /// <summary>
        /// Returns the XML document indicating that the lookup operation failed.
        /// </summary>
        public static readonly string Xml = new FsConfigNotFound().ToXml();

        //---------------------------------------------------------------------
        // Instance members

        /// <summary>
        /// Constructor.
        /// </summary>
        private FsConfigNotFound()
            : base("result")
        {
            AddSectionChild(
                new XElement("result",
                    new XAttribute("status", "not found")));
        }
    }
}
