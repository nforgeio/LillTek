//-----------------------------------------------------------------------------
// FILE:        WikipediaBlock.cs
// CONTRIBUTOR: Jeff Lill
// COPYRIGHT:   Copyright (c) 2005-2015 by Jeffrey Lill.  All rights reserved.
// DESCRIPTION: Describes a text block within a within a Wiki page section.

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
    /// Describes a text block within a within a Wiki page section.
    /// </summary>
    public class WikipediaBlock
    {
        private WikipediaParser parser;     // The parent parser

        /// <summary>
        /// Returns the block type.
        /// </summary>
        public WikipediaBlockType Type { get; private set; }

        /// <summary>
        /// Returns the block text.
        /// </summary>
        public string Text { get; private set; }

        /// <summary>
        /// Returns the indent level for list item blocks.
        /// </summary>
        public int Indent { get; private set; }

        /// <summary>
        /// Returns the actual node depth for a list node.
        /// </summary>
        private int NodeDepth { get; set; }

        /// <summary>
        /// Returns the list of parent node types above a list item node
        /// in the heirarchy.
        /// </summary>
        private List<WikipediaBlockType> ParentNodeTypes { get; set; }

        /// <summary>
        /// Returns the collection of children for nested blocks.
        /// </summary>
        public List<WikipediaBlock> Children { get; private set; }

        /// <summary>
        /// Returns the HTML text for the block.
        /// </summary>
        public string Html { get; internal set; }

        /// <summary>
        /// Determines whether the block is a list node.
        /// </summary>
        public bool IsListNode
        {
            get
            {
                switch (Type)
                {

                    case WikipediaBlockType.RootList:
                    case WikipediaBlockType.BulletList:
                    case WikipediaBlockType.NumberedList:
                    case WikipediaBlockType.DefinitionList:

                        return true;

                    default:

                        return false;
                }
            }
        }

        /// <summary>
        /// Used to construct a non-list item node.
        /// </summary>
        /// <param name="parser">The parent parser.</param>
        /// <param name="type">Identifies the block type.</param>
        /// <param name="text">The block text (without any leading wikitext characters).</param>
        public WikipediaBlock(WikipediaParser parser, WikipediaBlockType type, string text)
        {
            this.parser    = parser;
            this.Type      = type;
            this.Text      = text;
            this.Indent    = 0;
            this.NodeDepth = 0;
            this.Children  = new List<WikipediaBlock>();
        }

        /// <summary>
        /// Used to construct a list item node.
        /// </summary>
        /// <param name="parser">The parent parser.</param>
        /// <param name="indentItemTypes">The list of item types parsed from the first few colums of the block text.</param>
        /// <param name="text">The block text (without any leading characters).</param>
        public WikipediaBlock(WikipediaParser parser, List<WikipediaBlockType> indentItemTypes, string text)
        {
            this.parser   = parser;
            this.Type     = indentItemTypes.Last();
            this.Indent   = indentItemTypes.Count;
            this.Text     = text;
            this.Children = new List<WikipediaBlock>();

            // Compute the actual node depth in the tree.  This is somewhat tricky because
            // the indent item types passed may imply the creation of new parent list nodes
            // depending on what is above the node in the heirarchy.  The algorithm below
            // walks the item node types passed and creates a list of the actual node types
            // that will need to exist below the root to host this node.

            var heirarchy = new List<WikipediaBlockType>();

            foreach (var itemType in indentItemTypes)
            {
                heirarchy.Add(GetParentType(itemType));
                heirarchy.Add(itemType);
            }

            this.NodeDepth = heirarchy.Count;

            // Create the heirarchy list by adding the virtual root and
            // removing the item type.

            this.ParentNodeTypes = new List<WikipediaBlockType>(heirarchy.Count);

            this.ParentNodeTypes.Add(WikipediaBlockType.RootList);
            for (int i = 0; i < heirarchy.Count - 1; i++)
                this.ParentNodeTypes.Add(heirarchy[i]);
        }

        /// <summary>
        /// Returns the list type required for the given list item type.
        /// </summary>
        /// <param name="itemType">The item type.</param>
        /// <returns>The parent node type.</returns>
        private static WikipediaBlockType GetParentType(WikipediaBlockType itemType)
        {
            switch (itemType)
            {
                case WikipediaBlockType.Bullet:

                    return WikipediaBlockType.BulletList;

                case WikipediaBlockType.Numbered:

                    return WikipediaBlockType.NumberedList;

                case WikipediaBlockType.Term:
                case WikipediaBlockType.Definition:

                    return WikipediaBlockType.DefinitionList;

                default:

                    // Should never happen.

                    throw new AssertException();
            }
        }

        /// <summary>
        /// Determines whether a block type is a list.
        /// </summary>
        /// <param name="type">The block type.</param>
        /// <returns><c>true</c> for lists.</returns>
        private static bool IsListType(WikipediaBlockType type)
        {
            switch (type)
            {
                case WikipediaBlockType.RootList:
                case WikipediaBlockType.BulletList:
                case WikipediaBlockType.NumberedList:
                case WikipediaBlockType.Definition:

                    return true;

                default:

                    return false;
            }
        }

        /// <summary>
        /// Appends the formatted block HTML to a string builder.
        /// </summary>
        /// <param name="sb">The output string builder.</param>
        public void Append(StringBuilder sb)
        {
            sb.ClearLine();

            switch (Type)
            {
                case WikipediaBlockType.Normal:

                    sb.AppendFormatLine("<p{0}>", parser.CssClassAttribute);
                    sb.Append(Text.Trim());
                    sb.ClearLine();
                    sb.AppendLine("</p>");
                    break;

                case WikipediaBlockType.Preformatted:

                    sb.AppendFormatLine("<pre{0}>", parser.CssClassAttribute);
                    sb.Append(Text);
                    sb.ClearLine();
                    sb.AppendLine("</pre>");
                    break;

                case WikipediaBlockType.BulletList:

                    sb.AppendFormatLine("<ul{0}>", parser.CssClassAttribute);
                    AppendContents(sb);
                    sb.AppendLine("</ul>");
                    break;

                case WikipediaBlockType.NumberedList:

                    sb.AppendFormatLine("<ol{0}>", parser.CssClassAttribute);
                    AppendContents(sb);
                    sb.AppendLine("</ol>");
                    break;

                case WikipediaBlockType.Bullet:
                case WikipediaBlockType.Numbered:

                    sb.AppendFormat("<li{0}>", parser.CssClassAttribute);
                    AppendContents(sb);
                    sb.AppendLine("</li>");
                    break;

                case WikipediaBlockType.DefinitionList:

                    sb.AppendFormatLine("<dl{0}>", parser.CssClassAttribute);
                    AppendContents(sb);
                    sb.AppendLine("</dl>");
                    break;

                case WikipediaBlockType.Term:

                    sb.AppendFormat("<dt{0}>", parser.CssClassAttribute);
                    AppendContents(sb);
                    sb.AppendLine("</dt>");
                    break;

                case WikipediaBlockType.Definition:

                    sb.AppendFormat("<dd{0}>", parser.CssClassAttribute);
                    AppendContents(sb);
                    sb.AppendLine("</dd>");
                    break;

                case WikipediaBlockType.RootList:

                    AppendContents(sb);
                    break;
            }
        }

        /// <summary>
        /// Appends the contents of the block to a string builder.
        /// </summary>
        /// <param name="sb">The output string builder.</param>
        private void AppendContents(StringBuilder sb)
        {
            if (Text != null)
                sb.Append(Text);

            if (Children.Count > 0)
            {

                sb.ClearLine();
                foreach (var child in Children)
                    child.Append(sb);
            }
        }

        /// <summary>
        /// Adds the item to a tree of lists and items, creating the list nodes
        /// as necessary and handling the level skipping allowed in the
        /// MediaWiki specification.
        /// </summary>
        /// <param name="item">The list item being added.</param>
        /// <param name="depth">The current recursion depth (root is at depth=0).</param>
        internal void AddListItem(WikipediaBlock item, int depth)
        {
            if (item.NodeDepth == depth + 1)
            {

                // The item is to be a child of this node.

                Children.Add(item);
                return;
            }

            // Look at the last child node.  If there isn't one then add a list 
            // node of the proper type and recurse into it to continue locating 
            // the new item.

            var lastChild = Children.LastOrDefault();

            if (lastChild == null)
            {
                lastChild = new WikipediaBlock(parser, item.ParentNodeTypes[depth + 1], string.Empty);
                Children.Add(lastChild);
            }

            // Recurse into the last child to continue locating the item.

            lastChild.AddListItem(item, depth + 1);
        }

        /// <summary>
        /// Recursively handles any additional wiki markup conversion and renders
        /// the block's HTML.
        /// </summary>
        /// <param name="parser">The parent parser.</param>
        internal void Format(WikipediaParser parser)
        {
            Text = parser.ProcessLinks(Text);
            Text = parser.RemoveReferences(Text);
            Text = parser.UnescapeHtmlChars(Text);
            Text = parser.RemoveArtifacts(Text);

            foreach (var child in Children)
                child.Format(parser);

            var sb = new StringBuilder();

            Append(sb);

            this.Html = sb.ToString();
        }
    }
}
