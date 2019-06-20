//-----------------------------------------------------------------------------
// FILE:        FsConfigDirectory.cs
// CONTRIBUTOR: Jeff Lill
// COPYRIGHT:   Copyright (c) 2005-2015 by Jeffrey Lill.  All rights reserved.
// DESCRIPTION: Generates the XML document to be returned to FreeSWITCH when a 
//              directory configuration event handler returns successfully.

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
    /// Generates the XML document to be returned to FreeSWITCH when a 
    /// directory configuration event handler returns successfully.
    /// </summary>
    internal class FsConfigDirectory : FsConfigBase
    {
        /// <summary>
        /// Constructs an instance with the user's authentication credentials.
        /// </summary>
        /// <param name="domain">The authentication domain (or <c>null</c>).</param>
        /// <param name="userID">The user ID.</param>
        /// <param name="password">The password.</param>
        public FsConfigDirectory(string domain, string userID, string password)
            : base("directory")
        {
            if (domain == null)
                domain = string.Empty;

            if (userID == null)
                throw new ArgumentNullException("userID");

            if (password == null)
                throw new ArgumentNullException("password");

            AddSectionChild(
                new XElement("domain",
                    new XAttribute("name", domain),
                    new XElement("user",
                        new XAttribute("id", userID),
                        new XElement("params",
                            new XElement("param",
                                new XAttribute("name", "password"),
                                new XAttribute("value", password))))));
        }

        /// <summary>
        /// Constructs an instance with the user's authentication credentials and optional
        /// parameters and variables.
        /// </summary>
        /// <param name="domain">The authentication domain (or <c>null</c>).</param>
        /// <param name="userID">The user ID.</param>
        /// <param name="password">The password.</param>
        /// <param name="parameters">The user parameters (or <c>null</c>).</param>
        /// <param name="variables">The user variables (or <c>null</c>).</param>
        public FsConfigDirectory(string domain, string userID, string password,
                                 ArgCollection parameters, ArgCollection variables)
            : this(domain, userID, password)
        {
            if (parameters != null)
            {
                Root.ElementPath("section/domain/user/params").
                    Add(from key in parameters
                        select new XElement("param",
                            new XAttribute("name", key),
                            new XAttribute("value", parameters[key])));
            }

            if (variables != null)
            {
                Root.ElementPath("section/domain/user").
                    Add(new XElement("variables",
                        from key in variables
                        select new XElement("variable",
                            new XAttribute("name", key),
                            new XAttribute("value", variables[key]))));
            }
        }
    }
}
