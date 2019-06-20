//-----------------------------------------------------------------------------
// FILE:        WikipediaParserOptions.cs
// CONTRIBUTOR: Jeff Lill
// COPYRIGHT:   Copyright (c) 2005-2015 by Jeffrey Lill.  All rights reserved.
// DESCRIPTION: Holds the options that control how the WikiParser processes a
//              Wikipedia article.

using System;
using System.Diagnostics;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Xml.Linq;

using LillTek.Common;

namespace LillTek.Advanced
{
    /// <summary>
    /// Holds the options that control how the <see cref="WikipediaParser" /> processes a
    /// Wikipedia article.
    /// </summary>
    public class WikipediaParserOptions
    {
        /// <summary>
        /// Default constructor.
        /// </summary>
        public WikipediaParserOptions()
        {
            this.CssClass = null;
        }

        /// <summary>
        /// The CSS class to use when generating HTML elements (defaults to <c>null</c>).
        /// </summary>
        public string CssClass { get; set; }
    }
}
