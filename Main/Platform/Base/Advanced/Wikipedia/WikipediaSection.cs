//-----------------------------------------------------------------------------
// FILE:        WikipediaSection.cs
// CONTRIBUTOR: Jeff Lill
// COPYRIGHT:   Copyright (c) 2005-2015 by Jeffrey Lill.  All rights reserved.
// DESCRIPTION: Describes a section within a Wiki page.

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
    /// Describes a section within a Wiki page.
    /// </summary>
    public class WikipediaSection
    {
        private WikipediaParser parser;     // The parent parser

        /// <summary>
        /// Returns the section level where level=1 is the page level.
        /// </summary>
        public int Level { get; private set; }

        /// <summary>
        /// Returns the section (or page) title.
        /// </summary>
        public string Title { get; private set; }

        /// <summary>
        /// Returns the formatted section HTML.
        /// </summary>
        public string Html { get; private set; }

        /// <summary>
        /// Returns the list of the section blocks (paragraphs, lists, etc).
        /// </summary>
        public List<WikipediaBlock> Blocks { get; private set; }

        /// <summary>
        /// Constructor.
        /// </summary>
        /// <param name="parser">The parent parser.</param>
        /// <param name="level">The section nesting level (1=page level).</param>
        /// <param name="title">The section title.</param>
        /// <param name="wikiText">The partially converted section content.</param>
        public WikipediaSection(WikipediaParser parser, int level, string title, string wikiText)
        {
            this.parser = parser;
            this.Level  = level;
            this.Title  = title;

            wikiText    = WikipediaParser.FormatText(wikiText);
            this.Blocks = parser.FormatBlocks(wikiText);

            // Process each of the blocks.

            foreach (var block in this.Blocks)
                block.Format(parser);

            //-----------------------------------------------------------------
            // Render the block and section HTML.

            var sbSection = new StringBuilder(Math.Min(2048, wikiText.Length));
            var sbBlock   = new StringBuilder(wikiText.Length);

            foreach (var block in this.Blocks)
            {
                sbBlock.Clear();
                block.Append(sbBlock);
                block.Html = sbBlock.ToString();

                sbSection.Append(block.Html);
            }

            this.Html = sbSection.ToString();
        }

        /// <summary>
        /// Returns <c>true</c> if the section is empty.
        /// </summary>
        public bool IsEmpty
        {
            get { return string.IsNullOrWhiteSpace(this.Html); }
        }
    }
}
