//-----------------------------------------------------------------------------
// FILE:        WikipediaParser.cs
// CONTRIBUTOR: Jeff Lill
// COPYRIGHT:   Copyright (c) 2005-2015 by Jeffrey Lill.  All rights reserved.
// DESCRIPTION: Implements a MediaWiki parser.

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

// $todo(jeff.lill):
//
// This is a bit of a huge hack-o-rama just to see if I can get something working quickly
// for LookupPhoneNumber.com.  I need to come back to this later and really think
// this through.

// $todo(jeff.lill): Known Issues
//
//    * It would be cool to implement a scheme for intercepting templates and implementing
//      some of these (like linking geographical coordinates to Bing or Google) or the
//      {{Official website}} or {{cite}} templates.
//
//    * I'd like to pass flesh out the WikiParserOptions class to the parser to control some
//      functions (like customizing the CSS generated for embedded HTML tags, etc.
//
//    * It's a little weird to have the parser end up holding the WikiSections and WikiBlocks.
//      It might be better to have a separate document object be returned and perhaps to]
//      have this based on XDoc rather than rolling my own.

namespace LillTek.Advanced
{
    /// <summary>
    /// <b>Warninig: Hack-o-rama:</b> Implements a MediaWiki parser.
    /// </summary>
    public class WikipediaParser
    {
        //---------------------------------------------------------------------
        // Static members

        private const string WikipediaPrefix = "http://www.wikipedia.org/wiki/";

        internal static readonly Regex TitleRegex          = new Regex(@"<title>(?<title>.+)</title>", RegexOptions.Compiled | RegexOptions.Singleline);
        internal static readonly Regex WikiTextRegex       = new Regex(@"<text.*?>(?<text>.+)</text>", RegexOptions.Compiled | RegexOptions.Singleline);
        internal static readonly Regex HtmlCommentRegex    = new Regex(@"&lt;!--.*--&gt;", RegexOptions.Compiled | RegexOptions.Singleline);
        internal static readonly Regex RedirectRegex       = new Regex(@"^#REDIRECT.*\n", RegexOptions.Compiled | RegexOptions.IgnoreCase | RegexOptions.Multiline);
        internal static readonly Regex MagicWordRegex      = new Regex(@"(__NOTOC__|__FORCETOC__|__TOC__|__NOEDITSECTION__|__NEWSECTIONLINK__|__NONEWSECTIONLINK__|__NOGALLERY__|__HIDDENCAT__|__INDEX__|__NOINDEX__)", RegexOptions.Compiled | RegexOptions.Singleline);
        internal static readonly Regex H2Regex             = new Regex(@"^==(?<title>.+?)==\n?", RegexOptions.Compiled | RegexOptions.Multiline);
        internal static readonly Regex H3Regex             = new Regex(@"^===(?<title>.+?)===\n?", RegexOptions.Compiled | RegexOptions.Multiline);
        internal static readonly Regex H4Regex             = new Regex(@"^====(?<title>.+?)====\n?", RegexOptions.Compiled | RegexOptions.Multiline);
        internal static readonly Regex H5Regex             = new Regex(@"^=====(?<title>.+?)=====\n?", RegexOptions.Compiled | RegexOptions.Multiline);
        internal static readonly Regex HRRegex             = new Regex(@"(?<=(\n|^))(\ )*----(\ )*\n", RegexOptions.Compiled);
        internal static readonly Regex HxRegex             = new Regex(@"<h\d>\s*(?<title>.+?)\s*</h\d>", RegexOptions.Compiled | RegexOptions.IgnoreCase | RegexOptions.Singleline);
        internal static readonly Regex BoldRegex           = new Regex(@"'''(?<text>.+?)'''", RegexOptions.Compiled | RegexOptions.Singleline);
        internal static readonly Regex ItalicRegex         = new Regex(@"''(?<text>.+?)''", RegexOptions.Compiled | RegexOptions.Singleline);
        internal static readonly Regex BoldItalicRegex     = new Regex(@"'''''(?<text>.+?)'''''", RegexOptions.Compiled | RegexOptions.Singleline);
        internal static readonly Regex UnderlinedRegex     = new Regex(@"__(?<text>.+?)__", RegexOptions.Compiled | RegexOptions.Singleline);
        internal static readonly Regex UriRegex            = new Regex(@"(https?)://+[\w\d:#@%/;$()~_?\+-=\\\.&]*", RegexOptions.Compiled | RegexOptions.IgnoreCase | RegexOptions.Multiline);
        internal static readonly Regex UriOrLinkStartRegex = new Regex(@"(?<uri>(https?)://+[\w\d:#@%/;$()~_?\+-=\\\.&]*)|(?<link>\[\[|(?<link>\[))", RegexOptions.Compiled | RegexOptions.IgnoreCase | RegexOptions.Multiline);
        internal static readonly Regex LinkFieldsRegex     = new Regex(@"^\s*((?<namespace>(\w|\d|_)*):)*(?<linkref>[^|]*)\s*((?<altbar>\|)\s*(?<alttext>.*))?\s*", RegexOptions.Compiled | RegexOptions.Singleline);
        internal static readonly Regex LinkEndingRegex     = new Regex(@"\G(\w|\d)+", RegexOptions.Compiled | RegexOptions.Multiline);
        internal static readonly Regex RefRegex            = new Regex(@"&lt;ref.*?&gt;.*?&lt;/ref&gt;", RegexOptions.Compiled | RegexOptions.Singleline);
        internal static readonly Regex EscapesRegex        = new Regex(@"(&lt;|&gt;|&amp;)", RegexOptions.Compiled | RegexOptions.Singleline);
        internal static readonly Regex ArtifactsRegex      = new Regex(@"(\( \)|\(\))", RegexOptions.Compiled | RegexOptions.Multiline);

        /// <summary>
        /// Normalizes the text so that only linefeeds are used to terminate all lines.
        /// </summary>
        /// <param name="wikiText">The input text.</param>
        /// <returns>The normalized text.</returns>
        internal static string NormalizeTextLines(string wikiText)
        {
            wikiText = wikiText.Replace("\r", string.Empty);

            if (wikiText.Length < 0 || wikiText[wikiText.Length - 1] != '\n')
                wikiText += '\n';

            return wikiText;
        }

        /// <summary>
        /// Removes the page XML surrounding the wiki text and the leading <b>#REDIRECT</b> line 
        /// if either or both are present.
        /// </summary>
        /// <param name="wikiText">The input text.</param>
        /// <returns>The processed output.</returns>
        internal static string StripXmlAndRedirect(string wikiText)
        {
            var match = WikiTextRegex.Match(wikiText);

            if (match.Success)
                wikiText = match.Groups["text"].Value;

            return RedirectRegex.Replace(wikiText, string.Empty);
        }

        /// <summary>
        /// Strips magic works from the text.
        /// </summary>
        /// <param name="wikiText">The input text.</param>
        /// <returns>The processed output.</returns>
        internal static string StripMagicWords(string wikiText)
        {
            return MagicWordRegex.Replace(wikiText, string.Empty);
        }

        private class NestableItem
        {
            public string               Source;
            public int                  Index;
            public int                  Length;
            public string               Value;
            public List<NestableItem>   Children;

            public NestableItem(string source, int index, int length)
            {
                this.Source   = source;
                this.Index    = index;
                this.Length   = length;
                this.Children = new List<NestableItem>();
            }

            public override string ToString()
            {
                return Source.Substring(Index, Length);
            }
        }

        /// <summary>
        /// Attempts to parse potentially nested items from the text at the specified position.
        /// </summary>
        /// <param name="wikiText">The input text.</param>
        /// <param name="itemPos">Pass as the position of the opening marker.  Returns as the position just after the closing marker.</param>
        /// <param name="openMarker">The opening marker (eg. "{{" or "[[").</param>
        /// <param name="closeMarker">The closing marker (eg. "}}" or "]]").</param>
        /// <returns>The item if one could be parsed, <c>null</c> otherwise.</returns>
        private static NestableItem ParseNestedItem(string wikiText, string openMarker, string closeMarker, ref int itemPos)
        {
            var     item = new NestableItem(wikiText, itemPos, 0);
            int     p;
            int     pOpening;
            int     pClosing;

            p = itemPos + openMarker.Length;
            while (true)
            {
                // $todo(jeff.lill): This is not very efficient.

                pOpening = wikiText.IndexOf(openMarker, p);
                pClosing = wikiText.IndexOf(closeMarker, p);

                if (pClosing == -1)
                {
                    // Unbalanced {{ .. }}

                    itemPos = p;
                    return null;
                }
                else if (pClosing < pOpening || pOpening == -1)
                    break;

                NestableItem child; ;

                child = ParseNestedItem(wikiText, openMarker, closeMarker, ref pOpening);
                if (child == null)
                    break;

                item.Children.Add(child);
                p = child.Index + child.Length;
            }

            itemPos = pClosing + closeMarker.Length;
            item.Length = itemPos - item.Index;
            item.Value = wikiText.Substring(item.Index, item.Length);

            return item;
        }

        /// <summary>
        /// Processes any templates embedded in the text.
        /// </summary>
        /// <param name="wikiText">The input text.</param>
        /// <returns>The output text.</returns>
        internal static string ProcessTemplates(string wikiText)
        {
            var     items = new List<NestableItem>();
            var     sb    = new StringBuilder(wikiText.Length);
            int     p;
            int     pMatch;

            p = 0;
            while (true)
            {
                pMatch = wikiText.IndexOf("{{", p);
                if (pMatch == -1)
                    break;

                NestableItem item;

                item = ParseNestedItem(wikiText, "{{", "}}", ref pMatch);
                if (item == null)
                    break;

                items.Add(item);
                p = item.Index + item.Length;
            }

            p = 0;
            foreach (var item in items)
            {
                // Append any text from the last position up to the start of
                // the template.

                sb.Append(wikiText, p, item.Index - p);

                if (item.Children.Count > 0)
                {
                    // Strip templates with nested children

                    p = item.Index + item.Length;
                    continue;
                }

                // Process a few of the possible variables and strip the rest.

                var variable = wikiText.Substring(item.Index + 2, item.Length - 4).ToUpper();
                var localNow = DateTime.Now;
                var utcNow   = DateTime.UtcNow;

                switch (variable)
                {
                    case "CURRENTYEAR": sb.AppendFormat("{0:yyyy}", utcNow); break;
                    case "CURRENTMONTH": sb.AppendFormat("{0:MM}", utcNow); break;
                    case "CURRENTMONTHNAME": sb.AppendFormat("{0:MMMM}", utcNow); break;
                    case "CURRENTMONTHABBREV": sb.AppendFormat("{0:MMM}", utcNow); break;
                    case "CURRENTDAY": sb.AppendFormat("{0:d}", utcNow); break;
                    case "CURRENTDAY2": sb.AppendFormat("{0:dd}", utcNow); break;
                    case "CURRENTDAYNAME": sb.AppendFormat("{0:dddd}", utcNow); break;
                    case "CURRENTTIME": sb.AppendFormat("{0:HH:mm}", utcNow); break;
                    case "CURRENTHOUR": sb.AppendFormat("{0:HH}", utcNow); break;
                    case "LOCALYEAR": sb.AppendFormat("{0:yyyy}", localNow); break;
                    case "LOCALMONTH": sb.AppendFormat("{0:MM}", localNow); break;
                    case "LOCALMONTHNAME": sb.AppendFormat("{0:MMMM}", localNow); break;
                    case "LOCALMONTHABBREV": sb.AppendFormat("{0:MMM}", localNow); break;
                    case "LOCALDAY": sb.AppendFormat("{0:d}", localNow); break;
                    case "LOCALDAY2": sb.AppendFormat("{0:dd}", localNow); break;
                    case "LOCALDAYNAME": sb.AppendFormat("{0:dddd}", localNow); break;
                    case "LOCALTIME": sb.AppendFormat("{0:HH:mm}", localNow); break;
                    case "LOCALHOUR": sb.AppendFormat("{0:HH}", localNow); break;
                }

                p = item.Index + item.Length;
            }

            // Append any text remaining after the last template.

            sb.Append(wikiText, p, wikiText.Length - p);

            return sb.ToString();
        }

        /// <summary>
        /// Converts the wikitext bold/italic/underline formatting to HTML.
        /// </summary>
        /// <param name="wikiText">The input text.</param>
        /// <returns>The output text.</returns>
        internal static string FormatText(string wikiText)
        {
            wikiText = BoldItalicRegex.Replace(wikiText, match => string.Format("<b><i>{0}</i></b>", match.Groups["text"]));
            wikiText = BoldRegex.Replace(wikiText, match => string.Format("<b>{0}</b>", match.Groups["text"]));
            wikiText = ItalicRegex.Replace(wikiText, match => string.Format("<i>{0}</i>", match.Groups["text"]));
            wikiText = UnderlinedRegex.Replace(wikiText, match => string.Format("<u>{0}</u>", match.Groups["text"]));

            return wikiText;
        }

        /// <summary>
        /// Determines if a character is a block indent character.
        /// </summary>
        /// <param name="ch">The character being tested.</param>
        /// <returns><c>true</c> for block indent characters.</returns>
        private static bool IsIndentChar(char ch)
        {
            switch (ch)
            {
                case '*':
                case '#':
                case ';':
                case ':':

                    return true;

                default:

                    return false;
            }
        }

        //---------------------------------------------------------------------
        // Instance members.

        private int nextLinkNum = 1;    // # to use for the next unnamed external link

        /// <summary>
        /// Returns the parser options.
        /// </summary>
        public WikipediaParserOptions Options { get; private set; }

        /// <summary>
        /// Returns the title of the page.
        /// </summary>
        public string Title { get; private set; }

        /// <summary>
        /// Returns the URI to the source article on Wikipedia.
        /// </summary>
        public string SourceUri { get; private set; }

        /// <summary>
        /// The list of page sections in the order parsed from the wiki text.
        /// </summary>
        public List<WikipediaSection> Sections { get; private set; }

        /// <summary>
        /// Returns the CSS class attribute to be embedded into genrated HTML tags.
        /// </summary>
        /// <remarks>
        /// <para>
        /// This will be blank if no class was specified in the <see cref="WikipediaParserOptions" /> or 
        /// a string of the form:
        /// </para>
        /// <example>
        /// class="myClass"
        /// </example>
        /// <note>
        /// The generated attribute string will include a leading space.
        /// </note>
        /// </remarks>
        public string CssClassAttribute { get; private set; }

        /// <summary>
        /// Constructs a parser with default options.
        /// </summary>
        /// <param name="wikiText">The wiki page text in MediaWiki page format.</param>
        /// <remarks>
        /// This constructor parses the wiki page text passed into local properties that
        /// can be examined and also rendered into HTML.
        /// </remarks>
        public WikipediaParser(string wikiText)
            : this(wikiText, null)
        {
        }

        /// <summary>
        /// Constructs a parser with specific options.
        /// </summary>
        /// <param name="wikiText">The wiki page text in MediaWiki page format.</param>
        /// <param name="options">The parser options or <c>null</c> to use default settings.</param>
        /// <remarks>
        /// This constructor parses the wiki page text passed into local properties that
        /// can be examined and also rendered into HTML.
        /// </remarks>
        public WikipediaParser(string wikiText, WikipediaParserOptions options)
        {
            if (options == null)
                options = new WikipediaParserOptions();

            this.Sections          = new List<WikipediaSection>();
            this.Options           = options;
            this.CssClassAttribute = options.CssClass == null ? string.Empty : string.Format(" class=\"{0}\"", options.CssClass);

            ParsePageXml(wikiText);

            wikiText = NormalizeTextLines(wikiText);
            wikiText = StripHtmlComments(wikiText);
            wikiText = StripXmlAndRedirect(wikiText);
            wikiText = StripMagicWords(wikiText);
            wikiText = ProcessTemplates(wikiText);

            ParseSections(wikiText);
        }

        /// <summary>
        /// Parses the page XML properties (if present).
        /// </summary>
        /// <param name="wikiText">The input text.</param>
        internal void ParsePageXml(string wikiText)
        {
            var match = TitleRegex.Match(wikiText);

            if (match.Success)
            {
                this.Title = match.Groups["title"].Value;
                this.SourceUri = WikipediaPrefix + Title.Replace(' ', '_');
            }
        }

        /// <summary>
        /// Removes any embedded HTML comments within wiki markup. 
        /// </summary>
        /// <param name="wikiText">The input wiki markup.</param>
        /// <returns>The processed text.</returns>
        internal string StripHtmlComments(string wikiText)
        {
            return HtmlCommentRegex.Replace(wikiText, string.Empty);
        }

        /// <summary>
        /// Parses the section text into paragraph or list blocks.
        /// </summary>
        /// <param name="wikiText">The input wiki markup.</param>
        /// <returns>The block list.</returns>
        internal List<WikipediaBlock> FormatBlocks(string wikiText)
        {
            //-----------------------------------------------------------------
            // Render any the <hr /> elements within the section.

            wikiText = HRRegex.Replace(wikiText, string.Format("<hr{0} />", this.CssClassAttribute));

            //-----------------------------------------------------------------
            // Process paragraphs and lists.

            var             blocks = new List<WikipediaBlock>();
            var             reader = new StringReader(wikiText);
            StringBuilder   sb;

            for (var line = reader.ReadLine(); line != null; line = reader.ReadLine())
            {
                if (line.Length == 0)
                    continue;   // Ignore leading blank lines

                if (line[0] == ' ')
                {
                    // We have a preformatted block.  Accumulate lines that start with a space
                    // until we reach one that doesn't or we reach the end of the text.

                    sb = new StringBuilder();

                    sb.AppendLine(line.Substring(1));
                    while (reader.Peek() == (int)' ')
                        sb.AppendLine(reader.ReadLine().Substring(1));

                    blocks.Add(new WikipediaBlock(this, WikipediaBlockType.Preformatted, sb.ToString()));
                    continue;
                }

                if (IsIndentChar(line[0]))
                {
                    // We have some sort of list element.  Scan through the prefixes
                    // and accumulate them in the list.

                    List<WikipediaBlockType> indentItemTypes = new List<WikipediaBlockType>();
                    string nodeText;

                    for (int i = 0; i < line.Length; i++)
                    {
                        if (IsIndentChar(line[i]))
                        {
                            switch (line[i])
                            {
                                case '*':

                                    indentItemTypes.Add(WikipediaBlockType.Bullet);
                                    break;

                                case '#':

                                    indentItemTypes.Add(WikipediaBlockType.Numbered);
                                    break;

                                case ';':

                                    indentItemTypes.Add(WikipediaBlockType.Term);
                                    break;

                                case ':':

                                    indentItemTypes.Add(WikipediaBlockType.Definition);
                                    break;

                                default:

                                    throw new AssertException();  // Should never happen
                            }
                        }
                        else
                            break;
                    }

                    // The rest of the line is the block text.

                    nodeText = line.Substring(indentItemTypes.Count).TrimStart();

                    // If the last block is not a RootList then create and add one
                    // before submitting this new block to the list to be added to
                    // the correct position.

                    if (blocks.Count == 0 || blocks.Last().Type != WikipediaBlockType.RootList)
                        blocks.Add(new WikipediaBlock(this, WikipediaBlockType.RootList, string.Empty));

                    blocks.Last().AddListItem(new WikipediaBlock(this, indentItemTypes, nodeText), 0);
                    continue;
                }

                // We must have a normal paragraph.  Accumulate lines until we reach one
                // that is empty, begins with a space or one of the block indent characters.

                sb = new StringBuilder();

                sb.AppendLine(line);
                while (true)
                {
                    var chPeek = reader.Peek();

                    if (chPeek == -1 || chPeek == (int)'\n' || chPeek == (int)' ' || IsIndentChar((char)chPeek))
                        break;

                    line = reader.ReadLine();
                    sb.AppendLine(line);
                }

                blocks.Add(new WikipediaBlock(this, WikipediaBlockType.Normal, sb.ToString()));
            }

            return blocks;
        }

        /// <summary>
        /// Parses the page sections.
        /// </summary>
        /// <param name="wikiText">The input text.</param>
        private void ParseSections(string wikiText)
        {
            // Replace the wikitext section header markers with HTML <h#>..</h#> tags.

            wikiText = H5Regex.Replace(wikiText, match => string.Format("<h5>{0}</h5>", match.Groups["title"]));
            wikiText = H4Regex.Replace(wikiText, match => string.Format("<h4>{0}</h4>", match.Groups["title"]));
            wikiText = H3Regex.Replace(wikiText, match => string.Format("<h3>{0}</h3>", match.Groups["title"]));
            wikiText = H2Regex.Replace(wikiText, match => string.Format("<h2>{0}</h2>", match.Groups["title"]));

            // Split the sections

            var matches = HxRegex.GetMatches(wikiText);

            if (matches.Count == 0)
            {
                // There are no subsections so simply add a single level 1
                // section for the page.

                Sections.Add(new WikipediaSection(this, 1, this.Title, wikiText));
                return;
            }

            // The text between the top of the page and the first section header
            // is the the page level 1 section.

            Sections.Add(new WikipediaSection(this, 1, this.Title, wikiText.Substring(0, matches[0].Index)));

            // Extract the subsections.

            for (int i = 0; i < matches.Count; i++)
            {
                var match    = matches[i];
                var matchEnd = match.Index + match.Length;
                var title    = match.Groups["title"].Value;
                int level;

                // Figure out the level from the digit in the <h#> tag.

                switch (match.Value[2])
                {
                    case '1':

                        level = 1;
                        break;

                    default:
                    case '2':

                        level = 2;
                        break;

                    case '3':

                        level = 3;
                        break;

                    case '4':

                        level = 4;
                        break;

                    case '5':

                        level = 5;
                        break;
                }

                if (i < matches.Count - 1)
                {
                    // This is not the last section so the text from the end of
                    // the match up to the beginning of the next match belongs
                    // to this section.

                    Sections.Add(new WikipediaSection(this, level, title, wikiText.Substring(matchEnd, matches[i + 1].Index - matchEnd)));
                }
                else
                {
                    // This is the last section so the text remaining after the match
                    // is the section text.

                    Sections.Add(new WikipediaSection(this, level, title, wikiText.Substring(matchEnd)));
                }
            }
        }

        /// <summary>
        /// Removes any footnotes and other references from the section text.
        /// </summary>
        /// <param name="wikiText">The input wiki markup text.</param>
        /// <returns>The processed text.</returns>
        internal string RemoveReferences(string wikiText)
        {
            return RefRegex.Replace(wikiText,
                match =>
                {
                    return string.Empty;
                });
        }

        /// <summary>
        /// Unescapes the escaped <b>&lt;</b>, <b>&gt;</b> and <b>&amp;</b> characters embedded
        /// in the text.
        /// </summary>
        /// <param name="wikiText">The input wiki markup text.</param>
        /// <returns>The processed text.</returns>
        internal string UnescapeHtmlChars(string wikiText)
        {
            return EscapesRegex.Replace(wikiText,
                match =>
                {
                    switch (match.Value)
                    {
                        case "&lt;": return "<";
                        case "&gt;": return ">";
                        case "&amp;": return "&";
                        default: return match.Value;
                    }
                });
        }

        private struct LinkMatch
        {
            public int      Index;
            public int      Length;
            public string   Value;
            public string   Uri;
            public string   WikiLink;
            public string   LinkEnding;
            public bool     HasChildren;
            public bool     IsExternal;

            public LinkMatch(Match match, string uri)
            {

                this.Index       = match.Index;
                this.Length      = match.Length;
                this.Value       = match.Value;
                this.Uri         = uri;

                this.WikiLink    = null;
                this.LinkEnding  = null;
                this.HasChildren = false;
                this.IsExternal  = true;
            }

            public LinkMatch(NestableItem item, string wikiLink, bool isExternal, string linkEnding)
            {
                this.Index       = item.Index;
                this.Length      = item.Length;
                this.Value       = item.Value;
                this.WikiLink    = wikiLink;
                this.LinkEnding  = linkEnding;
                this.HasChildren = item.Children.Count > 0;

                if (linkEnding != null)
                    this.Length += linkEnding.Length;

                this.Uri        = null;
                this.IsExternal = isExternal;
            }
        }

        /// <summary>
        /// Converts wikitext links and embedded URIs to HTML links.
        /// </summary>
        /// <param name="wikiText">The input wiki markup.</param>
        /// <returns>The processed text.</returns>
        internal string ProcessLinks(string wikiText)
        {
            // Note that wiki links may be nested so I can't use Regex to parse these.  It appears
            // the the only use for this is to add links to the caption of image links.  Since
            // we strip image links at this point, I won't worry about processing the sublinks
            // right now.  Note also that I have to process embedded URIs at the same time.
            //
            // My approach will be to first scan the text looking for wiki links or embedded
            // URIs and build a list of the top-level matches as LinkMatch records.
            //
            // Then I'll scan through the text again, processing the matched items and writing
            // the output as I go to a string builder.

            var sb = new StringBuilder(wikiText.Length);
            var matches = new List<LinkMatch>();
            int p;

            p = 0;
            while (true)
            {
                var match = UriOrLinkStartRegex.Match(wikiText, p);

                if (!match.Success)
                    break;

                if (match.Groups["uri"].Success)
                {
                    matches.Add(new LinkMatch(match, match.Groups["uri"].Value));
                    p = match.Index + match.Length;
                }
                else
                {
                    string          openMarker;
                    string          closeMarker;
                    NestableItem    item;
                    bool            isExternal = false;

                    p = match.Index;

                    switch (match.Groups["link"].Value)
                    {
                        case "[":

                            openMarker = "[";
                            closeMarker = "]";
                            isExternal = true;
                            break;

                        case "[[":

                            openMarker = "[[";
                            closeMarker = "]]";
                            isExternal = false;
                            break;

                        default:

                            throw new AssertException();    // Should never happen
                    }

                    item = ParseNestedItem(wikiText, openMarker, closeMarker, ref p);
                    if (item == null)
                        continue;

                    // Extract the contents of the link as well as any potential
                    // ending text before adding the match.

                    var wikiLink    = item.Value.Substring(2, item.Length - 4).Trim();
                    var endingMatch = LinkEndingRegex.Match(wikiText, item.Index + item.Length);

                    matches.Add(new LinkMatch(item, wikiLink, isExternal, endingMatch.Value));
                }
            }

            // Process the link matches.

            if (matches.Count == 0)
                return wikiText;        // No matches

            p = 0;
            foreach (var linkMatch in matches)
            {
                // Append the text before the match.

                sb.Append(wikiText.Substring(p, linkMatch.Index - p));

                if (linkMatch.Uri != null)
                {
                    // Process URIs

                    sb.AppendFormat("<a href=\"{0}\" target=\"_blank\" rel=\"nofollow\">{0}</a>", linkMatch.Uri);
                }
                else
                {
                    // Process wiki links

                    if (string.IsNullOrWhiteSpace(linkMatch.WikiLink))
                    {
                        // This can happen for links that had templates that
                        // removed by previous processing

                        goto skipThisLink;
                    }

                    if (linkMatch.HasChildren)
                    {
                        // We don't handle nested links right now

                        goto skipThisLink;
                    }

                    // Right now I'm going to handle the following link formats:
                    //
                    //  [[ Page Link ]]
                    //  [[ Page Link | Alt Text ]]
                    //  [[ Help:Page Link| ]]               * hide namespace 
                    //  [[ Help ]]ers                       * word endings
                    //  [[ #anchor ]]
                    //  [[ #anchor | Alt Text ]]
                    //  [[ Page Link#anchor ]]              * anchor on other page
                    //  [[ Page Link#anchor | Alt Text ]]   * anchor on other page
                    //  [ URI ]                             * numbered external link
                    //  [ URI link text ]                   * named external link
                    //
                    // Other Notes:
                    //
                    //  * Image and File links will be discarded.

                    var fieldsMatch = LinkFieldsRegex.Match(linkMatch.WikiLink);

                    if (!fieldsMatch.Success)
                        continue;       // Link must be badly formatted.

                    var     linkNamespace = fieldsMatch.Groups.GetValue("namespace", string.Empty).Trim();
                    var     linkRef       = fieldsMatch.Groups.GetValue("linkref", string.Empty).Trim();
                    var     altText       = fieldsMatch.Groups.GetValue("alttext", string.Empty).Trim();
                    var     altBar        = fieldsMatch.Groups.GetValue("altbar");
                    string  linkText      = null;
                    string  pageRef;

                    // Build the page reference.

                    if (linkRef.StartsWith("#"))
                    {
                        // Send anchor links to the current article back to the original Wikipedia
                        // page because there's an excellent chance that the anchor doesn't actually
                        // exist on generated pages.

                        linkRef = this.Title + linkRef;

                        if (string.IsNullOrWhiteSpace(altText))
                            linkText = this.Title;
                        else
                            linkText = altText;
                    }

                    if (linkMatch.IsExternal)
                    {
                        // This is an external link

                        var linkContents = linkMatch.Value.Substring(1, linkMatch.Value.Length - 2).Trim();

                        linkRef = UriRegex.Match(linkContents).Value;
                        altText = linkContents.Substring(linkRef.Length).Trim();

                        if (linkRef == string.Empty)
                            sb.AppendFormat("{0} ", altText);
                        else if (altText != string.Empty)
                            sb.AppendFormat("<a href=\"{0}\" target=\"_blank\" rel=\"nofollow\">{1}</a>", linkRef, altText);
                        else
                            sb.AppendFormat("[<a href=\"{0}\" target=\"_blank\" rel=\"nofollow\">{1}</a>]", linkRef, nextLinkNum++);
                    }
                    else
                    {
                        // This is an internal wiki link

                        if (!string.IsNullOrWhiteSpace(linkNamespace))
                        {
                            if (String.Compare(linkNamespace, "File", true) == 0 || String.Compare(linkNamespace, "Image", true) == 0)
                                goto skipThisLink;

                            pageRef = linkNamespace + ":" + linkRef;
                        }
                        else
                            pageRef = linkRef;

                        pageRef = WikipediaPrefix + pageRef.Replace(' ', '_');

                        // Build the link text (if we haven't already done so above).

                        if (linkText == null)
                        {
                            if (string.IsNullOrWhiteSpace(altText))
                            {
                                if (!string.IsNullOrWhiteSpace(altBar))
                                    linkText = linkRef;
                                else if (!string.IsNullOrWhiteSpace(linkNamespace))
                                    linkText = linkNamespace + ":" + linkRef;
                                else
                                    linkText = linkRef;

                                // Strip off any anchor text

                                var pAnchor = linkText.IndexOf('#');

                                if (pAnchor != -1)
                                    linkText = linkText.Substring(0, pAnchor);
                            }
                            else
                                linkText = altText;
                        }

                        if (!string.IsNullOrWhiteSpace(linkMatch.LinkEnding))
                            linkText += linkMatch.LinkEnding;

                        // Generate the link.

                        sb.AppendFormat("<a href=\"{0}\" target=\"_blank\" rel=\"nofollow\">{1}</a>", pageRef, linkText);
                    }
                }

            skipThisLink:

                p = linkMatch.Index + linkMatch.Length;
            }

            // Append any text after the last match.

            var last = matches.Last();

            sb.Append(wikiText.Substring(last.Index + last.Length));

            return sb.ToString();
        }

        /// <summary>
        /// Removes strange artifacts that may be left over due to the fact that we don't
        /// currently process most variables or parser functions.
        /// </summary>
        /// <param name="wikiText">The input wiki markup.</param>
        /// <returns>The processed text.</returns>
        public string RemoveArtifacts(string wikiText)
        {
            return ArtifactsRegex.Replace(wikiText, string.Empty);
        }

        /// <summary>
        /// Writes the parsed page as a full HTML page to a text writer.
        /// </summary>
        /// <param name="writer">The output writer.</param>
        public void RenderAsHtmlPage(TextWriter writer)
        {
            writer.WriteLine("<html>");
            writer.WriteLine("<head>");
            writer.WriteLine("<title>{0}</title>", this.Title);
            writer.WriteLine("</head>");

            writer.WriteLine("<body{0}>", CssClassAttribute);

            foreach (var section in Sections)
            {

                writer.WriteLine("<h{0}{1}>{2}</h{0}>", section.Level, CssClassAttribute, section.Title);
                writer.Write(section.Html);
            }

            writer.WriteLine("</body>");
            writer.WriteLine("</html>");
        }

        /// <summary>
        /// Writes the parsed page as a full HTML page toi a file.
        /// </summary>
        /// <param name="path"></param>
        public void RenderAsHtmlPage(string path)
        {
            Helper.CreateFileTree(path);
            using (var writer = new StreamWriter(path, false, Encoding.UTF8))
            {
                RenderAsHtmlPage(writer);
            }
        }

        /// <summary>
        /// Renders parsed page as a full HTML page.
        /// </summary>
        /// <returns>The HTML text.</returns>
        public string ToHtml()
        {
            var writer = new StringWriter();

            RenderAsHtmlPage(writer);
            return writer.ToString();
        }
    }
}
