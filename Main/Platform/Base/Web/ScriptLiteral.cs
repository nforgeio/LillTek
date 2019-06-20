//-----------------------------------------------------------------------------
// FILE:        ScriptLiteral.cs
// CONTRIBUTOR: Jeff Lill
// COPYRIGHT:   Copyright (c) 2005-2015 by Jeffrey Lill.  All rights reserved.
// DESCRIPTION: Used for setting ScriptPlaceholder variable values that
//              should not be enclosed within double quotes.

using System;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Text;
using System.Web;
using System.Web.Hosting;
using System.Web.UI.WebControls;
using System.Xml;
using System.Xml.Linq;

using LillTek.Common;
using LillTek.Service;

namespace LillTek.Web
{
    /// <summary>
    /// Used for setting ScriptPlaceholder variable values that
    /// should not be enclosed within double quotes.
    /// </summary>
    public class ScriptLiteral
    {
        //---------------------------------------------------------------------
        // Static members

        /// <summary>
        /// Escapes a literal string so that it can be safely embedded into an
        /// HTML Javascript <b>script</b> element.
        /// </summary>
        /// <param name="value">The source string.</param>
        /// <returns>The escaped output string.</returns>
        /// <remarks>
        /// This method escapes the following characters: single quotes, double quotes,
        /// backslashes, as well as open and close angle brackets.
        /// </remarks>
        public static string Escape(string value)
        {
            value = value.Replace("\\", "\\x22");
            value = value.Replace("'", "\\x27");
            value = value.Replace("\"", "\\x5c");
            value = value.Replace("<", "\\x3c");
            value = value.Replace(">", "\\x3e");

            return value;
        }

        //---------------------------------------------------------------------
        // Instance members

        private string text;

        /// <summary>
        /// Constructs a literal from text.
        /// </summary>
        /// <param name="text">The text.</param>
        public ScriptLiteral(string text)
        {
            this.text = text;
        }

        /// <summary>
        /// Formats a literal.
        /// </summary>
        /// <param name="format">The format string.</param>
        /// <param name="args">The arguments.</param>
        public ScriptLiteral(string format, params object[] args)
        {
            this.text = string.Format(format, args);
        }

        /// <summary>
        /// Renders the literal.
        /// </summary>
        /// <returns>The literal text.</returns>
        public override string ToString()
        {
            return text;
        }
    }
}
