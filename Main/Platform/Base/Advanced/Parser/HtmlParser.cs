//-----------------------------------------------------------------------------
// FILE:        HtmlParser.cs
// CONTRIBUTOR: Jeff Lill
// COPYRIGHT:   Copyright (c) 2005-2015 by Jeffrey Lill.  All rights reserved.
// DESCRIPTION: Implements n HTML parser.

using System;
using System.IO;
using System.Xml;
using System.Text;
using System.Diagnostics;
using System.Collections;
using System.Collections.Generic;

using LillTek.Common;

// $todo(jeff.lill): 
//
// I need to put some work into this to make it more forgiving of
// common markup errors.

// $todo(jeff.lill): 
//
// I need to do some more work on handling formatting tags and 
// cconverting formatted text to plain text.  I'm also not
// handling whitespace super well either.

// $todo(jeff.lill)
//
// Look into converting this to generate a Linq-to-XML document
// instead of the old fashioned XmlDocument.

namespace LillTek.Advanced
{
    /// <summary>
    /// Enumerates the possible types of HTML data items that can be parsed.
    /// </summary>
    public enum HtmlItemType : int
    {
        /// <summary>
        /// TEXT within n HTML document.
        /// </summary>
        Text,

        /// <summary>
        /// An opening HTML tag.
        /// </summary>
        OpenTag,

        /// <summary>
        /// A closing HTML tag.
        /// </summary>
        CloseTag,

        /// <summary>
        /// Text within a HTML comment.
        /// </summary>
        Comment
    }

    /// <summary>
    /// Describes a HTML item.
    /// </summary>
    public sealed class HtmlItem
    {
        /// <summary>
        /// The item type.
        /// </summary>
        public readonly HtmlItemType ItemType;

        /// <summary>
        /// For opening and closing tags, this will hold the tag string
        /// converted to lowercase.  For text and comments, this will hold
        /// the text.
        /// </summary>
        public readonly string Text;

        /// <summary>
        /// For opening tags this will hold the name/value pairs defining
        /// the tag's attributes.  Note that the attribute names are stored in lowercase.
        /// Note that this set must never be modified directly by application
        /// code.
        /// </summary>
        public readonly Dictionary<string, string> Attributes;

        /// <summary>
        /// Indicates whether an opening or closing tag item is a HTML processing
        /// instruction.  These instructions have names beginning with '?' characters.
        /// </summary>
        public readonly bool Instruction;

        /// <summary>
        /// Constructs a HTML item.
        /// </summary>
        /// <param name="itemType">The item type.</param>
        /// <param name="text">The item text.</param>
        public HtmlItem(HtmlItemType itemType, string text)
        {
            this.ItemType   = itemType;
            this.Text       = text;
            this.Attributes = null;

            if ((itemType == HtmlItemType.OpenTag || itemType == HtmlItemType.CloseTag) && text.IndexOf('?') != -1)
            {
                this.Text = this.Text.Replace("?", "");
                this.Instruction = true;
            }
            else
                this.Instruction = false;
        }

        /// <summary>
        /// Constructs a HTML item.
        /// </summary>
        /// <param name="itemType">The item type.</param>
        /// <param name="text">The item text.</param>
        /// <param name="attributes">The item attributes.</param>
        public HtmlItem(HtmlItemType itemType, string text, Dictionary<string, string> attributes)
        {
            this.ItemType   = itemType;
            this.Text       = text;
            this.Attributes = attributes;

            if ((itemType == HtmlItemType.OpenTag || itemType == HtmlItemType.CloseTag) && text.IndexOf('?') != -1)
            {
                this.Text        = this.Text.Replace("?", "");
                this.Instruction = true;
            }
            else
                this.Instruction = false;
        }
    }

    /// <summary>
    /// Exception thrown by the <see cref="HtmlParser" /> class.
    /// </summary>
    public sealed class HtmlParserException : ApplicationException
    {
        /// <summary>
        /// Constructor.
        /// </summary>
        /// <param name="message">The exception message.</param>
        public HtmlParserException(string message)
            : base(message)
        {
        }
    }

    /// <summary>
    /// Implements a high performance HTML parser.
    /// </summary>
    /// <remarks>
    /// <para>
    /// This class can be used in two ways.  The static <see cref="Parse(string)" /> and <see cref="Parse(BlockArray,Encoding)" /> methods
    /// can be used to parse the HTML into a <see cref="XmlDocument" /> providing a rich (but somewhat slow) mechanism for
    /// parsing and traversing the HTML elements.  A quicker mechanism is is to construct an instance of the parser and then
    /// call <see cref="Read" /> to return each HTML item parsed from the source.  <see cref="Read" /> will return <c>null</c>
    /// when the end of the HTML has been reached.
    /// </para>
    /// <para>
    /// Note that this parser converts parsed tags of the form: <b>&lt;tag /&gt;</b> into the corresponsing
    /// opening and closing tags: <b>&lt;tag&gt;&lt;/tag&gt;</b>.
    /// </para>
    /// <para>
    /// The parser also handles improperly nested tags by ignoring end tags that don't having a matching
    /// start tag and also by adding ommitted end tags.
    /// </para>
    /// <para>
    /// The <see cref="IgnoreText" /> and <see cref="IgnoreComments" /> properties control whether the <see cref="HtmlItem.Text" />
    /// property will be parsed for <see cref="HtmlItemType.Text" /> and <see cref="HtmlItemType.Comment" /> items.  These
    /// properties are both initialized to <c>false</c> by the constructors but may be set to <c>true</c> before <see cref="Read" />
    /// is called for the first time.  If either of these properties are set to <c>true</c> then the corresponding types of HTML item
    /// will be ignored 
    /// </para>
    /// <para>
    /// The <see cref="AddTagFilter" /> method can be used to specify the set of tags to be returned by the parser.
    /// This should be called one or more times before the first call to <see cref="Read" /> passing the desired
    /// tag names.  <see cref="Read" /> will then return items only for the tag names specified.
    /// </para>
    /// <para>
    /// The parser performs special handling of &lt;!DOCTYPE ... &gt; comments found in the source.  Rather than
    /// parsing and returning these as comments, these will be parsed as &lt;doctype&gt;...&lt;/doctype&gt; where
    /// "..." will be the text parsed within the DOCTYPE comment.
    /// </para>
    /// <para>
    /// The HTML 4.01 recomendation indicates that the contents of the SCRIPT and STYLE elements should be
    /// intrepreted as SGML CDATA which is the text up to the first "&lt;/" character sequence encountered
    /// (or the end of the source).  This parser handles this correctly and returns a <see cref="HtmlItem.Text" />
    /// item holding the script or style text.
    /// </para>
    /// <para>
    /// This parser supports the SGML CDATA marked section by returning &lt;![CDATA[...]]&gt; into a text item
    /// where "..." will be parsed out as the next.  Tags and character entities within the CDATA text will 
    /// not be processed.  Note that this parser does not process INCLUDE and IGNORE or any other type of
    /// marked section at this time.  All marked sections other than CDATA will be ignored.
    /// </para>
    /// </remarks>
    public class HtmlParser
    {
        //---------------------------------------------------------------------
        // Static members

        private const int TextSize           = 32 * 1024;
        private const int CommentSize        = 32 * 1024;
        private const int ScriptStyleSize    = 32 * 1024;
        private const int TagSize            = 25;
        private const int AttributeNameSize  = 25;
        private const int AttributeValueSize = 1024;

        private static HtmlItem                     emptyText;      // A text item with no next
        private static HtmlItem                     emptyComment;   // A comment item with no next
        private static Dictionary<string, char>     entities;       // Maps entity names to the corresponding character

        /// <summary>
        /// Static constructor.
        /// </summary>
        static HtmlParser()
        {
            emptyText    = new HtmlItem(HtmlItemType.Text, string.Empty);
            emptyComment = new HtmlItem(HtmlItemType.Comment, string.Empty);

            // Initialize the common entities map.

            entities = new Dictionary<string, char>();

            entities.Add("nbsp", (char)32);
            entities.Add("iexcl", (char)161);
            entities.Add("cent", (char)162);
            entities.Add("pound", (char)163);
            entities.Add("curren", (char)164);
            entities.Add("yen", (char)165);
            entities.Add("brvbar", (char)166);
            entities.Add("sect", (char)167);
            entities.Add("uml", (char)168);
            entities.Add("copy", (char)169);
            entities.Add("ordf", (char)170);
            entities.Add("laquo", (char)171);
            entities.Add("not", (char)172);
            entities.Add("shy", (char)173);
            entities.Add("reg", (char)174);
            entities.Add("macr", (char)175);
            entities.Add("deg", (char)176);
            entities.Add("plusmn", (char)177);
            entities.Add("sup2", (char)178);
            entities.Add("sup3", (char)179);
            entities.Add("acute", (char)180);
            entities.Add("micro", (char)181);
            entities.Add("para", (char)182);
            entities.Add("middot", (char)183);
            entities.Add("cedil", (char)184);
            entities.Add("sup1", (char)185);
            entities.Add("ordm", (char)186);
            entities.Add("raquo", (char)187);
            entities.Add("frac14", (char)188);
            entities.Add("frac12", (char)189);
            entities.Add("frac34", (char)190);
            entities.Add("iquest", (char)191);
            entities.Add("Agrave", (char)192);
            entities.Add("Aacute", (char)193);
            entities.Add("Acirc", (char)194);
            entities.Add("Atilde", (char)195);
            entities.Add("Auml", (char)196);
            entities.Add("Aring", (char)197);
            entities.Add("AElig", (char)198);
            entities.Add("Ccedil", (char)199);
            entities.Add("Egrave", (char)200);
            entities.Add("Eacute", (char)201);
            entities.Add("Ecirc", (char)202);
            entities.Add("Euml", (char)203);
            entities.Add("Igrave", (char)204);
            entities.Add("Iacute", (char)205);
            entities.Add("Icirc", (char)206);
            entities.Add("Iuml", (char)207);
            entities.Add("ETH", (char)208);
            entities.Add("Ntilde", (char)209);
            entities.Add("Ograve", (char)210);
            entities.Add("Oacute", (char)211);
            entities.Add("Ocirc", (char)212);
            entities.Add("Otilde", (char)213);
            entities.Add("Ouml", (char)214);
            entities.Add("times", (char)215);
            entities.Add("Oslash", (char)216);
            entities.Add("Ugrave", (char)217);
            entities.Add("Uacute", (char)218);
            entities.Add("Ucirc", (char)219);
            entities.Add("Uuml", (char)220);
            entities.Add("Yacute", (char)221);
            entities.Add("THORN", (char)222);
            entities.Add("szlig", (char)223);
            entities.Add("agrave", (char)224);
            entities.Add("aacute", (char)225);
            entities.Add("acirc", (char)226);
            entities.Add("atilde", (char)227);
            entities.Add("auml", (char)228);
            entities.Add("aring", (char)229);
            entities.Add("aelig", (char)230);
            entities.Add("ccedil", (char)231);
            entities.Add("egrave", (char)232);
            entities.Add("eacute", (char)233);
            entities.Add("ecirc", (char)234);
            entities.Add("euml", (char)235);
            entities.Add("igrave", (char)236);
            entities.Add("iacute", (char)237);
            entities.Add("icirc", (char)238);
            entities.Add("iuml", (char)239);
            entities.Add("eth", (char)240);
            entities.Add("ntilde", (char)241);
            entities.Add("ograve", (char)242);
            entities.Add("oacute", (char)243);
            entities.Add("ocirc", (char)244);
            entities.Add("otilde", (char)245);
            entities.Add("ouml", (char)246);
            entities.Add("divide", (char)247);
            entities.Add("oslash", (char)248);
            entities.Add("ugrave", (char)249);
            entities.Add("uacute", (char)250);
            entities.Add("ucirc", (char)251);
            entities.Add("uuml", (char)252);
            entities.Add("yacute", (char)253);
            entities.Add("thorn", (char)254);
            entities.Add("yuml", (char)255);
            entities.Add("quot", (char)34);
            entities.Add("amp", (char)38);
            entities.Add("lt", (char)60);
            entities.Add("gt", (char)62);
            entities.Add("OElig", (char)338);
            entities.Add("oelig", (char)339);
            entities.Add("Scaron", (char)352);
            entities.Add("scaron", (char)353);
            entities.Add("Yuml", (char)376);
            entities.Add("circ", (char)710);
            entities.Add("tilde", (char)732);
            entities.Add("ensp", (char)8194);
            entities.Add("emsp", (char)8195);
            entities.Add("thinsp", (char)8201);
            entities.Add("zwnj", (char)8204);
            entities.Add("zwj", (char)8205);
            entities.Add("lrm", (char)8206);
            entities.Add("rlm", (char)8207);
            entities.Add("ndash", (char)8211);
            entities.Add("mdash", (char)8212);
            entities.Add("lsquo", (char)8216);
            entities.Add("rsquo", (char)8217);
            entities.Add("sbquo", (char)8218);
            entities.Add("ldquo", (char)8220);
            entities.Add("rdquo", (char)8221);
            entities.Add("bdquo", (char)8222);
            entities.Add("dagger", (char)8224);
            entities.Add("Dagger", (char)8225);
            entities.Add("permil", (char)8240);
            entities.Add("lsaquo", (char)8249);
            entities.Add("rsaquo", (char)8250);
            entities.Add("euro", (char)8364);
            entities.Add("fnof", (char)402);
            entities.Add("Alpha", (char)913);
            entities.Add("Beta", (char)914);
            entities.Add("Gamma", (char)915);
            entities.Add("Delta", (char)916);
            entities.Add("Epsilon", (char)917);
            entities.Add("Zeta", (char)918);
            entities.Add("Eta", (char)919);
            entities.Add("Theta", (char)920);
            entities.Add("Iota", (char)921);
            entities.Add("Kappa", (char)922);
            entities.Add("Lambda", (char)923);
            entities.Add("Mu", (char)924);
            entities.Add("Nu", (char)925);
            entities.Add("Xi", (char)926);
            entities.Add("Omicron", (char)927);
            entities.Add("Pi", (char)928);
            entities.Add("Rho", (char)929);
            entities.Add("Sigma", (char)931);
            entities.Add("Tau", (char)932);
            entities.Add("Upsilon", (char)933);
            entities.Add("Phi", (char)934);
            entities.Add("Chi", (char)935);
            entities.Add("Psi", (char)936);
            entities.Add("Omega", (char)937);
            entities.Add("alpha", (char)945);
            entities.Add("beta", (char)946);
            entities.Add("gamma", (char)947);
            entities.Add("delta", (char)948);
            entities.Add("epsilon", (char)949);
            entities.Add("zeta", (char)950);
            entities.Add("eta", (char)951);
            entities.Add("theta", (char)952);
            entities.Add("iota", (char)953);
            entities.Add("kappa", (char)954);
            entities.Add("lambda", (char)955);
            entities.Add("mu", (char)956);
            entities.Add("nu", (char)957);
            entities.Add("xi", (char)958);
            entities.Add("omicron", (char)959);
            entities.Add("pi", (char)960);
            entities.Add("rho", (char)961);
            entities.Add("sigmaf", (char)962);
            entities.Add("sigma", (char)963);
            entities.Add("tau", (char)964);
            entities.Add("upsilon", (char)965);
            entities.Add("phi", (char)966);
            entities.Add("chi", (char)967);
            entities.Add("psi", (char)968);
            entities.Add("omega", (char)969);
            entities.Add("thetasym", (char)977);
            entities.Add("upsih", (char)978);
            entities.Add("piv", (char)982);
            entities.Add("bull", (char)8226);
            entities.Add("hellip", (char)8230);
            entities.Add("prime", (char)8242);
            entities.Add("Prime", (char)8243);
            entities.Add("oline", (char)8254);
            entities.Add("frasl", (char)8260);
            entities.Add("weierp", (char)8472);
            entities.Add("image", (char)8465);
            entities.Add("real", (char)8476);
            entities.Add("trade", (char)8482);
            entities.Add("alefsym", (char)8501);
            entities.Add("larr", (char)8592);
            entities.Add("uarr", (char)8593);
            entities.Add("rarr", (char)8594);
            entities.Add("darr", (char)8595);
            entities.Add("harr", (char)8596);
            entities.Add("crarr", (char)8629);
            entities.Add("lArr", (char)8656);
            entities.Add("uArr", (char)8657);
            entities.Add("rArr", (char)8658);
            entities.Add("dArr", (char)8659);
            entities.Add("hArr", (char)8660);
            entities.Add("forall", (char)8704);
            entities.Add("part", (char)8706);
            entities.Add("exist", (char)8707);
            entities.Add("empty", (char)8709);
            entities.Add("nabla", (char)8711);
            entities.Add("isin", (char)8712);
            entities.Add("notin", (char)8713);
            entities.Add("ni", (char)8715);
            entities.Add("prod", (char)8719);
            entities.Add("sum", (char)8721);
            entities.Add("minus", (char)8722);
            entities.Add("lowast", (char)8727);
            entities.Add("radic", (char)8730);
            entities.Add("prop", (char)8733);
            entities.Add("infin", (char)8734);
            entities.Add("ang", (char)8736);
            entities.Add("and", (char)8743);
            entities.Add("or", (char)8744);
            entities.Add("cap", (char)8745);
            entities.Add("cup", (char)8746);
            entities.Add("int", (char)8747);
            entities.Add("there4", (char)8756);
            entities.Add("sim", (char)8764);
            entities.Add("cong", (char)8773);
            entities.Add("asymp", (char)8776);
            entities.Add("ne", (char)8800);
            entities.Add("equiv", (char)8801);
            entities.Add("le", (char)8804);
            entities.Add("ge", (char)8805);
            entities.Add("sub", (char)8834);
            entities.Add("sup", (char)8835);
            entities.Add("nsub", (char)8836);
            entities.Add("sube", (char)8838);
            entities.Add("supe", (char)8839);
            entities.Add("oplus", (char)8853);
            entities.Add("otimes", (char)8855);
            entities.Add("perp", (char)8869);
            entities.Add("sdot", (char)8901);
            entities.Add("lceil", (char)8968);
            entities.Add("rceil", (char)8969);
            entities.Add("lfloor", (char)8970);
            entities.Add("rfloor", (char)8971);
            entities.Add("lang", (char)9001);
            entities.Add("rang", (char)9002);
            entities.Add("loz", (char)9674);
            entities.Add("spades", (char)9824);
            entities.Add("clubs", (char)9827);
            entities.Add("hearts", (char)9829);
            entities.Add("diams", (char)9830);
        }

        /// <summary>
        /// Parses the HTML string passed into an XML document.
        /// </summary>
        /// <param name="html">The HTML document.</param>
        /// <returns>The XML document.</returns>
        /// <remarks>
        /// <para>
        /// All parsed HTML tags will be added to the XML document using the
        /// lowercased HTML tag name.  The root node of the document returned 
        /// will be the &lt;root&gt; tag.
        /// </para>
        /// <para>
        /// HTML text will be added to the document as a <see cref="XmlText" />
        /// node and comments will be added as a <see cref="XmlComment" /> node.
        /// </para>
        /// </remarks>
        public static XmlDocument Parse(string html)
        {
            var         parser   = new HtmlParser(html);
            var         xmlDoc   = new XmlDocument();
            var         xmlStack = new Stack<XmlNode>();
            HtmlItem    item;
            XmlNode     root;
            XmlNode     parent;

            root = xmlDoc.CreateElement("root");
            xmlDoc.AppendChild(root);

            xmlStack.Push(root);
            for (item = parser.Read(); item != null; item = parser.Read())
            {
                parent = xmlStack.Peek();
                switch (item.ItemType)
                {
                    case HtmlItemType.OpenTag:

                        XmlElement element;
                        XmlAttribute attribute;

                        // $hack(jeff.lill): 
                        //
                        // Converting "#" characters in element names to "-" because
                        // an exception will be thrown otherwise.  This issue may go
                        // away if/when we convert this class to generate Linq-to-Xml.

                        element = xmlDoc.CreateElement(item.Text.Replace('#', '-'));
                        if (item.Attributes.Count > 0)
                            foreach (string name in item.Attributes.Keys)
                            {
                                attribute       = xmlDoc.CreateAttribute(name);
                                attribute.Value = item.Attributes[name];
                                element.Attributes.Append(attribute);
                            }

                        parent.AppendChild(element);
                        xmlStack.Push(element);
                        break;

                    case HtmlItemType.CloseTag:

                        xmlStack.Pop();
                        break;

                    case HtmlItemType.Text:

                        parent.AppendChild(xmlDoc.CreateTextNode(item.Text));
                        break;

                    case HtmlItemType.Comment:

                        parent.AppendChild(xmlDoc.CreateComment(item.Text));
                        break;
                }
            }

            Assertion.Test(xmlStack.Count == 1);    // Only root should remain on the stack
            return xmlDoc;
        }

        /// <summary>
        /// Parses the encoded HTML block array passed into an XML document.
        /// </summary>
        /// <param name="blocks">The HTML document.</param>
        /// <param name="encoding">The HTML text character encoding.</param>
        /// <returns>The XML document.</returns>
        /// <remarks>
        /// <para>
        /// All parsed HTML tags will be added to the XML document using the
        /// lowercased HTML tag name.  The root node of the document returned 
        /// will be root tag parsed from the source text (typically &lt;html&gt;).
        /// </para>
        /// <para>
        /// HTML text will be added to the document as a <see cref="XmlText" />
        /// node and comments will be added as a <see cref="XmlComment" /> node.
        /// </para>
        /// </remarks>
        public static XmlDocument Parse(BlockArray blocks, Encoding encoding)
        {
            return Parse(encoding.GetString(blocks.ToByteArray()));
        }

        /// <summary>
        /// Replaces any character entities in the string passed with their
        /// equivalent character value.
        /// </summary>
        /// <param name="input">The input string with entities.</param>
        /// <returns>The output string.</returns>
        internal static string ConvertEntities(string input)
        {
            if (input.IndexOf('&') == -1)
                return input;

            var     sb = new StringBuilder(input.Length + 1024);
            int     p, pEnd;
            int     pNext = 0;
            char    ch;

            p = 0;
            while (true)
            {
                pEnd = input.IndexOf('&', p);
                if (pEnd == -1)
                {
                    sb.Append(input, p, input.Length - p);
                    break;
                }

                sb.Append(input, p, pEnd - p);

                p = pEnd + 1;

                // Scan forward for up to 10 characters looking for the terminating
                // semicolon.  If we don't find one then we're going to assume thatg
                // this is a markup error and simply process the ampersand as normal 
                // text.

                pEnd = p + 10;
                if (pEnd > input.Length)
                    pEnd = input.Length;

                ch = (char)0;
                for (int i = p; i < pEnd; i++)
                {
                    ch = input[i];
                    if (ch == ';')
                    {
                        pNext = i + 1;
                        break;
                    }
                }

                if (ch != ';')
                {
                    sb.Append('&');
                    continue;
                }

                // Looks like we have a reasonable entity.  "p" indexes the first
                // character after the ampersand and pNext indexes the first character
                // after the semicolon.
                //
                // Entities come in three flavors:
                //
                //      Decimal:    &#201;
                //      Hex:        &#x1fB2;
                //      Named:      &quot;
                //
                // If the entity isn't one of these valid forms then it will be ignored.

                if (input[p] == '#')
                {
                    string  number;
                    int     code;

                    p++;
                    if (input[p] == 'x' || input[p] == 'X')
                    {
                        // Expecting a hex value

                        p++;
                        number = input.Substring(p, pNext - p - 1);
                        if (Helper.TryParseHex(number, out code))
                        {
                            if (0 <= code && code <= 0x0000FFFF)
                                sb.Append((char)code);
                        }
                    }
                    else
                    {
                        // Expecting a decimal value

                        number = input.Substring(p, pNext - p - 1);
                        if (int.TryParse(number, out code))
                        {
                            if (0 <= code && code <= 0x0000FFFF)
                                sb.Append((char)code);
                        }
                    }
                }
                else
                {
                    // Must be a named entity.

                    string name = input.Substring(p, pNext - p - 1);
                    char value;

                    if (entities.TryGetValue(name, out value))
                        sb.Append(value);
                    else
                        sb.AppendFormat("&{0};", name);
                }

                p = pNext;
            }

            return sb.ToString();
        }

        //---------------------------------------------------------------------
        // Instance members

        private string                      source;             // The source HTML
        private int                         pos;                // Current parse character position
        private bool                        ignoreText;         // Indicates that text should be parsed
        private bool                        ignoreComments;     // Indicates that comments should be parsed
        private Dictionary<string, string>  tagFilter;          // The tag filter (or null)
        private Queue<HtmlItem>             itemQueue;          // Set of pre-parsed items waiting to be returned
        private StackArray<string>          nesting;            // Indicates the current tag nesting
        private Dictionary<string, string>  emptyAttributes;    // Invariant empty set of attributes.  This is
                                                                // used as a performance optimization so we don't
                                                                // have to allocate empty sets for all of the 
                                                                // parsed tags with no attributes.
        /// <summary>
        /// Constructs a HTML parser for parsing the string passed.
        /// </summary>
        /// <param name="html">The HTML text.</param>
        public HtmlParser(string html)
        {
            this.source          = html;
            this.pos             = 0;
            this.ignoreText      = false;
            this.ignoreComments  = false;
            this.tagFilter       = null;
            this.itemQueue       = new Queue<HtmlItem>(25);
            this.nesting         = new StackArray<string>(25);
            this.emptyAttributes = new Dictionary<string, string>();
        }

        /// <summary>
        /// Constructs a HTML parser for parsing the encoded HTML passed
        /// in a block array.
        /// </summary>
        /// <param name="blocks">The HTML text.</param>
        /// <param name="encoding">The HTML text character encoding.</param>
        public HtmlParser(BlockArray blocks, Encoding encoding)
            : this(encoding.GetString(blocks.ToByteArray()))
        {
        }

        /// <summary>
        /// Indicates whether parsed HTML text should be ignored.  This defaults to <c>false</c>.
        /// </summary>
        /// <remarks>
        /// <para>
        /// Setting this property to <c>false</c> will significantly improve the performance
        /// of the <see cref="Read" /> method if the application is not going to process HTML text.
        /// </para>
        /// <note>
        /// This property can only be set before <see cref="Read" /> is called for the
        /// first time.
        /// </note>
        /// </remarks>
        public bool IgnoreText
        {
            get { return ignoreText; }

            set
            {
                if (pos > 0)
                    throw new HtmlParserException("Cannot modify after Read() has been called.");

                ignoreText = value;
            }
        }

        /// <summary>
        /// Indicates whether parsed HTML comments should ignored.  This defaults to <c>false</c>.
        /// </summary>
        /// <remarks>
        /// <para>
        /// Setting this property to <c>true</c> will significantly improve the performance
        /// of the <see cref="Read" /> method if the application is not going to process HTML comments.
        /// </para>
        /// <note>
        /// This property can only be set before <see cref="Read" /> is called for the
        /// first time.
        /// </note>
        /// </remarks>
        public bool IgnoreComments
        {
            get { return ignoreComments; }

            set
            {
                if (pos > 0)
                    throw new HtmlParserException("Cannot modify after Read() has been called.");

                ignoreComments = value;
            }
        }

        /// <summary>
        /// Specifies a tag name to the set of tags to be filtered by the parser.
        /// </summary>
        /// <param name="tagName">The tag name.</param>
        /// <remarks>
        /// <note>
        /// This method may be called only before <see cref="Read" /> is
        /// called for the first time.
        /// </note>
        /// </remarks>
        public void AddTagFilter(string tagName)
        {
            if (pos > 0)
                throw new HtmlParserException("Cannot modify after Read() has been called.");

            if (tagFilter == null)
                tagFilter = new Dictionary<string, string>();

            tagName = tagName.ToLowerInvariant();
            tagFilter[tagName] = tagName;
        }

        /// <summary>
        /// Returns the next HTML item parsed from the source or <c>null</c> if the
        /// end of the source has been reached.
        /// </summary>
        /// <exception cref="HtmlParserException">Thrown when a parsing error is encountered.</exception>
        public HtmlItem Read()
        {
            HtmlItem    item;
            char        ch;

            Assertion.Test(emptyAttributes.Count == 0, "A HtmlItem.Attributes set has been illegally modified.");

            // If there are any queued items then return the next one.

            if (itemQueue.Count > 0)
                return itemQueue.Dequeue();

            while (true)
            {
                // Skip over any whitespace to the beginning of the next token
                // and then figure out what kind of token we have and then parse
                // it.

                SkipWhitespace();
                ch = Next();

                switch (ch)
                {
                    case (char)0:

                        // We've reached the end of the source.  Queue any omitted closing
                        // tags and then return the first one from the queue.  Return
                        // null otherwise.

                        if (nesting.Count == 0)
                            return null;

                        for (int i = 0; i < nesting.Count; i++)
                            itemQueue.Enqueue(new HtmlItem(HtmlItemType.CloseTag, nesting.Pop()));

                        return itemQueue.Dequeue();

                    case '<':

                        // We've got either a tag, a HTML comment, or a marked section.

                        if (pos + 3 <= source.Length && source[pos] == '!' && source[pos + 1] == '-' && source[pos + 2] == '-')
                        {
                            // It's a comment

                            Back();
                            item = ReadComment();
                            if (!ignoreComments)
                                return item;
                        }
                        else if (pos + 3 <= source.Length && source[pos] == '!' && source[pos + 1] == '[')
                        {
                            // It's a marked section

                            Back();
                            item = ReadSection();
                            if (!ignoreText && item != null)
                                return item;
                        }
                        else
                        {
                            // It's a tag

                            Back();
                            item = ReadTag();
                            if (item.ItemType != HtmlItemType.OpenTag && item.ItemType != HtmlItemType.CloseTag)
                                continue;

                            return item;
                        }
                        break;

                    default:

                        // We've got a HTML text block

                        Back();
                        item = ReadText();
                        if (!ignoreText && item.Text != string.Empty)
                            return item;

                        break;
                }
            }
        }

        /// <summary>
        /// Returns the next character from the source or 0 if we've reached the end.
        /// This method advances past the character returned.
        /// </summary>
        private char Next()
        {
            if (pos >= source.Length)
                return (char)0;
            else
                return source[pos++];
        }

        /// <summary>
        /// Returns the next character from the source or 0 if we've reached the end.
        /// This method does not advance past the character returned.
        /// </summary>
        private char Peek()
        {
            if (pos >= source.Length)
                return (char)0;
            else
                return source[pos];
        }

        /// <summary>
        /// Moves the source position back one character.
        /// </summary>
        private void Back()
        {
            pos--;
        }

        /// <summary>
        /// Advances past any whitespace characters.
        /// </summary>
        private void SkipWhitespace()
        {
            while (pos < source.Length)
                if (char.IsWhiteSpace(source[pos]))
                    pos++;
                else
                    break;
        }

        /// <summary>
        /// Parses a text item from the source.  The next input character
        /// should be the first character of the text.
        /// </summary>
        /// <returns>The text item.</returns>
        private HtmlItem ReadText()
        {
            StringBuilder   sb;
            char            ch;

            if (!ignoreText)
            {
                sb = new StringBuilder(TextSize);
                for (ch = Next(); ch != (char)0; ch = Next())
                {
                    if (ch == '<')
                    {
                        Back();
                        break;
                    }

                    sb.Append(ch);
                }

                return new HtmlItem(HtmlItemType.Text, ConvertEntities(sb.ToString().Trim()));
            }
            else
            {
                for (ch = Next(); ch != (char)0; ch = Next())
                {
                    if (ch == '<')
                    {
                        Back();
                        break;
                    }
                }

                return emptyText;
            }
        }

        /// <summary>
        /// Parses a HTML tag from the source.  The next input character
        /// should be the "&lt;" character of the tag.
        /// </summary>
        /// <returns>The tag item.</returns>
        /// <remarks>
        /// <note>
        /// The method will return an empty comment item if an
        /// error was encountered while parsing or if the tag parsed was
        /// not in the set of filtered tags.
        /// </note>
        /// </remarks>
        private HtmlItem ReadTag()
        {
            StringBuilder               sb;
            string                      tag;
            string                      name  = null;
            string                      value = null;
            char                        ch;
            HtmlItem                    item;
            Dictionary<string, string>  attributes = null;
            bool                        filtered;
            string                      s;

            ch = Next();
            Assertion.Test(ch == '<');
            Assertion.Test(itemQueue.Count == 0);

            // Parse the tag name

            sb = new StringBuilder(TagSize);
            for (ch = Next(); ch != (char)0; ch = Next())
            {
                if (ch == '>' || ch == '<' || Char.IsWhiteSpace(ch))
                    break;

                sb.Append(ch);
            }

            tag = sb.ToString().ToLowerInvariant();
            if (tag == "!doctype")
            {
                // We're going to special-case the <!doctype ...> comment by returning
                // HTML items for <doctype>...</doctype> where "..." will be rendered
                // as a text node containing the unparsed contents of the comment.

                tag      = "doctype";
                filtered = tagFilter == null || tagFilter.TryGetValue(tag.Replace("/", ""), out s);

                if (filtered)
                {
                    sb = new StringBuilder(256);
                    while (true)
                    {
                        ch = Next();
                        if (ch == (char)0 || ch == '>')
                            break;

                        sb.Append(ch);
                    }

                    itemQueue.Enqueue(new HtmlItem(HtmlItemType.Text, sb.ToString()));
                    itemQueue.Enqueue(new HtmlItem(HtmlItemType.CloseTag, tag));
                    return new HtmlItem(HtmlItemType.OpenTag, tag, emptyAttributes);
                }
                else
                {
                    while (true)
                    {
                        ch = Next();
                        if (ch == (char)0 || ch == '>')
                            break;
                    }
                }

                // Return empty comments for filtered tags.

                return emptyComment;
            }

            filtered = tagFilter == null || tagFilter.TryGetValue(tag.Replace("/", ""), out s);
            if (ch == '>')
                goto done;
            else if (ch == '<')
            {
                Back();
                goto done;
            }

            // Parse any attributes

            while (true)
            {
                SkipWhitespace();
                ch = Peek();
                if (ch == '/')
                {
                    // Expecting "/>".

                    Next();
                    ch = Peek();
                    if (ch != '>')
                    {
                        // Ignore the backslash and continue processing attributes

                        continue;
                    }

                    tag += '/';
                    Next();
                    goto done;
                }
                else if (ch == '?')
                {
                    // Expecting "?>".

                    Next();
                    ch = Peek();
                    if (ch != '>')
                    {
                        // Ignore the question mark and continue processing attributes

                        continue;
                    }

                    tag += '?';
                    Next();
                    goto done;
                }
                else if (ch == '>')
                {
                    Next();
                    goto done;
                }
                else if (ch == '<')
                    goto done;
                else if (ch == (char)0)
                {
                    // We've reached the end of the source

                    return emptyComment;
                }

                // Looks like we have an attribute.  I'm going to assume that
                // attributes may or may not have a value specified using an "=" sign.

                // Parse the attribute name

                if (filtered)
                    sb = new StringBuilder(AttributeNameSize);

                while (true)
                {
                    ch = Next();
                    if (ch == (char)0)
                        return emptyComment;
                    else if (ch == '=' || ch == '>' || ch == '/' || ch == '<' || ch == '?')
                        break;
                    else if (Char.IsWhiteSpace(ch))
                        break;

                    if (filtered)
                        sb.Append(ch);
                }

                if (filtered)
                    name = sb.ToString().ToLowerInvariant();

                Back();
                SkipWhitespace();
                ch = Next();

                if (ch == '>' || ch == '/' || ch == '?')
                {
                    if (filtered && name != string.Empty)
                    {
                        if (attributes == null)
                            attributes = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

                        attributes[name] = string.Empty;
                    }

                    Back();
                    continue;
                }

                if (ch == '<')
                {
                    Back();
                    goto done;
                }
                else if (ch != '=')
                {
                    // The attribute doesn't have a value so use an empty string

                    if (filtered)
                    {
                        if (attributes == null)
                            attributes = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

                        attributes[name] = string.Empty;
                    }

                    continue;
                }

                // Parse the attribute value.

                ch = Next();
                if (ch == (char)0)
                    return emptyComment;

                switch (ch)
                {
                    case '<':

                        // The tag must be missing the closing ">".

                        Back();
                        value = string.Empty;
                        break;

                    case '>':

                        // Looks like we've hit the end of the tag

                        value = string.Empty;
                        break;

                    case '"':

                        // Double quoted value

                        if (filtered)
                            sb = new StringBuilder(AttributeValueSize);

                        while (true)
                        {
                            ch = Next();
                            if (ch == (char)0)
                                return emptyComment;
                            else if (ch == '"')
                                break;

                            if (filtered)
                                sb.Append(ch);
                        }

                        if (filtered)
                            value = ConvertEntities(sb.ToString());
                        break;

                    case '\'':

                        // Single quoted value

                        if (filtered)
                            sb = new StringBuilder(AttributeValueSize);

                        while (true)
                        {
                            ch = Next();
                            if (ch == (char)0)
                                return emptyComment;
                            else if (ch == '\'')
                                break;

                            if (filtered)
                                sb.Append(ch);
                        }

                        if (filtered)
                            value = ConvertEntities(sb.ToString());
                        break;

                    case ' ':

                        // We've got an empty value

                        value = string.Empty;
                        break;

                    default:

                        // $hack(jeff.lill): 
                        //
                        // I'm going to assume that all characters except for whitespace,
                        // '<' and '>' are part of unquoted values.  The HTML 4.01 specification
                        // says that only alphanumeric, periods, underscores, hypens, and colons
                        // are allowed but I know that IE accepts forward slashes and percent
                        // signs, tildas and probably equal signs and question marks as well.
                        // so I'm going to be more flexable on this as well.

                        if (filtered)
                        {
                            sb = new StringBuilder(AttributeValueSize);
                            sb.Append(ch);
                        }

                        while (true)
                        {
                            ch = Next();
                            if (ch == (char)0)
                                return emptyComment;

                            if (ch != '<' && ch != '>' && ch != (char)0 && !Char.IsWhiteSpace(ch))
                            {
                                if (filtered)
                                    sb.Append(ch);
                            }
                            else
                            {
                                Back();
                                break;
                            }
                        }

                        if (filtered)
                            value = ConvertEntities(sb.ToString());
                        break;
                }

                if (filtered)
                {
                    if (attributes == null)
                        attributes = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

                    attributes[name] = value;
                }
            }

        done:       // I need to special case SCRIPT and STYLE elements.  The element contents for these
            // are considered to be SGML CDATA and are not to interpreted as HTML elements.
            // The HTML 4.01, specification says that the element contents consists of the text
            // up to but not including the first "</" character sequence.

            if (tag == "script" || tag == "style")
            {
                if (filtered)
                {
                    if (!ignoreText)
                        sb = new StringBuilder(ScriptStyleSize);

                    while (true)
                    {
                        if (pos + 2 <= source.Length && source[pos] == '<' && source[pos + 1] == '/')
                            break;

                        ch = Next();
                        if (ch == (char)0)
                            break;

                        if (!ignoreText)
                            sb.Append(ch);
                    }

                    if (!ignoreText)
                        itemQueue.Enqueue(new HtmlItem(HtmlItemType.Text, sb.ToString()));

                    nesting.Push(tag);
                    return new HtmlItem(HtmlItemType.OpenTag, tag, attributes != null ? attributes : emptyAttributes);
                }
                else
                {
                    while (true)
                    {
                        if (pos + 2 <= source.Length && source[pos] == '<' && source[pos + 1] == '/')
                            break;

                        ch = Next();
                        if (ch == (char)0)
                            break;
                    }
                }
            }

            // Return empty comments for filtered tags.

            if (!filtered)
                return emptyComment;

            // We've completed parsing the tag so figure out whether this is
            // a start tag, an end tag or a standalone tag.  Standalone tags
            // will be handled by returning a start tag and queuing an end tag.

            if (tag.Length == 0 || (tag[0] == '/' && tag[tag.Length - 1] == '/'))
                return emptyComment;

            if (tag[0] == '/')
            {
                // An end tag: 
                //
                // Look up tag nesting stack for the first matching start tag.  If we don't
                // find a match, then return an empty text item.  If we do find a match and its
                // at the top of the stack then pop the stack and return a closing tag.  If the
                // match is not at the top of the stack, then any tags on the stack from the top
                // of the stack to the matching tag have omitted closing tags.  In this case, we're
                // going to return a closing tag for the first omitted tag, queue closing tags
                // for the rest of the omitted tags, and then queue a closing tag for the item
                // just parsed.

                int match = -1;

                tag = tag.Substring(1);
                for (int i = 0; i < nesting.Count; i++)
                    if (tag == nesting[i])
                    {
                        match = i;
                        break;
                    }

                if (match == -1)
                    return emptyComment;
                else if (match == 0)
                {
                    nesting.Pop();
                    return new HtmlItem(HtmlItemType.CloseTag, tag);
                }
                else
                {
                    for (int i = 1; i < match; i++)
                        itemQueue.Enqueue(new HtmlItem(HtmlItemType.CloseTag, nesting[i]));

                    itemQueue.Enqueue(new HtmlItem(HtmlItemType.CloseTag, tag));
                    item = new HtmlItem(HtmlItemType.CloseTag, nesting.Peek());
                    nesting.Discard(match + 1);
                    return item;
                }
            }
            else if (tag[tag.Length - 1] == '/' || tag[tag.Length - 1] == '?')
            {
                // A standalone tag like <tag /> or <?tag ?>
                //
                // Return an opening tag and queue a closing tag.

                tag = tag.Substring(0, tag.Length - 1);
                itemQueue.Enqueue(new HtmlItem(HtmlItemType.CloseTag, tag));
                return new HtmlItem(HtmlItemType.OpenTag, tag, attributes != null ? attributes : emptyAttributes);
            }
            else
            {
                // Must be an opening tag

                nesting.Push(tag);
                return new HtmlItem(HtmlItemType.OpenTag, tag, attributes != null ? attributes : emptyAttributes);
            }
        }

        /// <summary>
        /// Parses a HTML comment from the source.  The next input character
        /// should be the "&lt;" character of the comment.
        /// </summary>
        /// <returns>The tag item.</returns>
        private HtmlItem ReadComment()
        {
            var     sb = ignoreComments ? null : new StringBuilder(CommentSize);
            char    ch;

            ch = Next();
            Assertion.Test(ch == '<');
            ch = Next();
            Assertion.Test(ch == '!');
            ch = Next();
            Assertion.Test(ch == '-');
            ch = Next();
            Assertion.Test(ch == '-');

            // $hack(jeff.lill): 
            //
            // The HTML 4.0 specification indicates that comments are terminated 
            // by "-->" where it is valid to have whitespace between the "--" and 
            // the ">" strings.  This is going to be a pain to parse and I have
            // a sneaking suspicion that there will be web pages with script embedded
            // in comments that may have "--" sequences embedded in it so I'm going
            // to require that comments be terminated by "-->" without whitespace.
            //
            // OK, I've confirmed that both Netscape and IE do not accept whitespace
            // between the "--" and ">" in the comment terminators (as described in
            // http://www.webreference.com/dev/html4nsie/sgml.html) so I'm going to
            // stick with this implementation.

            while (true)
            {
                if (pos + 3 >= source.Length)
                {
                    // We've reached the end of the source without seeing the
                    // comment terminator so append everything to the end of
                    // the source and break.

                    while (true)
                    {
                        ch = Next();
                        if (ch == (char)0)
                            break;
                        else if (sb != null)
                            sb.Append(ch);
                    }

                    break;
                }
                else if (source[pos] == '-' && source[pos + 1] == '-' && source[pos + 2] == '>')
                {
                    // We're at the comment terminator

                    pos += 3;
                    break;
                }
                else
                {
                    // Continue appending comment characters

                    ch = Next();
                    if (ch == (char)0)
                        break;

                    if (sb != null)
                        sb.Append(ch);
                }
            }

            return sb == null ? emptyComment
                              : new HtmlItem(HtmlItemType.Comment, sb.ToString());
        }

        /// <summary>
        /// Parses a marked section from the source.  The next input character
        /// should be the "&lt;" character of the comment.  Note that only CDATA
        /// sections will be returned as text items.  The method will return null
        /// for all other section types.
        /// </summary>
        /// <returns>The tag item.</returns>
        private HtmlItem ReadSection()
        {
            StringBuilder   sb;
            char            ch;
            string          type;

            ch = Next();
            Assertion.Test(ch == '<');
            ch = Next();
            Assertion.Test(ch == '!');
            ch = Next();
            Assertion.Test(ch == '[');

            // Parse the section type

            sb = new StringBuilder(TextSize);
            while (true)
            {
                ch = Next();
                if (ch == (char)0 || ch == '[')
                    break;

                sb.Append(ch);
            }

            type = sb.ToString();
            if (!ignoreText && type == "CDATA")
            {
                // Parse text until we see "]]>".

                sb = new StringBuilder(TextSize);
                while (true)
                {
                    ch = Next();
                    if (ch == (char)0)
                        break;

                    if (ch == ']' && pos + 2 <= source.Length && source[pos] == ']' && source[pos + 1] == '>')
                    {
                        pos += 2;
                        break;
                    }

                    sb.Append(ch);
                }

                return new HtmlItem(HtmlItemType.Text, sb.ToString());
            }
            else
            {
                // Skip over text until we see "]]>".

                while (true)
                {
                    ch = Next();
                    if (ch == (char)0)
                        break;

                    if (ch == ']' && pos + 2 <= source.Length && source[pos] == ']' && source[pos + 1] == '>')
                    {
                        pos += 2;
                        break;
                    }
                }

                return null;
            }
        }
    }
}
