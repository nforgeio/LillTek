//-----------------------------------------------------------------------------
// FILE:        _WikipediaParser.cs
// CONTRIBUTOR: Jeff Lill
// COPYRIGHT:   Copyright (c) 2005-2015 by Jeffrey Lill.  All rights reserved.
// DESCRIPTION: Unit tests

using System;
using System.Diagnostics;
using System.IO;
using System.Reflection;
using System.Text;
using System.Text.RegularExpressions;

using Microsoft.VisualStudio.TestTools.UnitTesting;

using LillTek.Common;
using LillTek.Advanced;

namespace LillTek.Advanced.Test
{
    [TestClass]
    public class _WikipediaParser
    {
        [TestMethod]
        [TestProperty("Lib", "LillTek.Advanced")]
        public void WikipediaParser_ParsePageXml()
        {
            var parser = new WikipediaParser(
@"  <page>
    <title>Lynnwood, Washington</title>
    <id>138213</id>
    <revision>
      <id>421566368</id>
      <timestamp>2011-03-30T23:40:34Z</timestamp>
      <contributor>
        <username>L5gcw0b</username>
        <id>14294239</id>
      </contributor>
      <minor />
      <comment>/* Neighborhood parks */</comment>
      <text xml:space=""preserve"">{{redirect|Lynnwood}}
Hello World!

==History==
This is the history

==Geography==
This is the geography.
      </text>
    </revision>
  </page>");

            Assert.AreEqual(1, parser.Sections[0].Level);
            Assert.AreEqual("Lynnwood, Washington", parser.Sections[0].Title);

            Assert.AreEqual(2, parser.Sections[1].Level);
            Assert.AreEqual("History", parser.Sections[1].Title);

            Assert.AreEqual(2, parser.Sections[2].Level);
            Assert.AreEqual("Geography", parser.Sections[2].Title);
        }

        [TestMethod]
        [TestProperty("Lib", "LillTek.Advanced")]
        public void WikipediaParser_StripXml()
        {
            Assert.AreEqual("Hello World!", WikipediaParser.StripXmlAndRedirect("<page><text>Hello World!</text></page>").Trim());
            Assert.AreEqual("Hello World!", WikipediaParser.StripXmlAndRedirect("Hello World!"));
            Assert.AreEqual("Hello World!", WikipediaParser.StripXmlAndRedirect("#REDIRECT [xxx]\nHello World!"));
            Assert.AreEqual("Hello World!", WikipediaParser.StripXmlAndRedirect("#redirect [xxx]\nHello World!"));
            Assert.AreEqual("Hello World!", WikipediaParser.StripXmlAndRedirect("<page><text>#REDIRECT [xxx]\nHello World!</text></page>").Trim());
            Assert.AreEqual("Hello World!", WikipediaParser.StripXmlAndRedirect(
@"<page>
<text>
Hello World!
</text>
</page>").Trim());

            Assert.AreEqual("Hello World!", WikipediaParser.StripXmlAndRedirect(
@"<page>
<text foo=""bar"">
Hello World!
</text>
</page>").Trim());
        }

        [TestMethod]
        [TestProperty("Lib", "LillTek.Advanced")]
        public void WikipediaParser_StripMagicWords()
        {
            var text = "This is a test__NOTOC__ of the__NONEWSECTIONLINK__ emergency__NOINDEX__ broadcasting system.";

            Assert.AreEqual("This is a test of the emergency broadcasting system.", WikipediaParser.StripMagicWords(text));
        }

        [TestMethod]
        [TestProperty("Lib", "LillTek.Advanced")]
        public void WikipediaParser_ProcessTemplates()
        {
            Assert.AreEqual("This is a test.", WikipediaParser.ProcessTemplates("This is a test."));
            Assert.AreEqual("This is a {{test.", WikipediaParser.ProcessTemplates("This is a {{test."));      // Unbalanced
            Assert.AreEqual("This is a {{test{{.", WikipediaParser.ProcessTemplates("This is a {{test{{."));  // Unbalanced
            Assert.AreEqual("This is a }}test.", WikipediaParser.ProcessTemplates("This is a }}test."));      // Unbalanced
            Assert.AreEqual("This is a test.", WikipediaParser.ProcessTemplates("This is a test."));
            Assert.AreEqual("This is a test.", WikipediaParser.ProcessTemplates("This is {{XXX {{YYY}} }}a test."));
            Assert.AreEqual("This is a test.", WikipediaParser.ProcessTemplates("This is a test.{{XXX {{YYY}} }}"));
            Assert.AreEqual("This is a test.", WikipediaParser.ProcessTemplates("{{XXX}}This is a test."));
            Assert.AreEqual("This is a test.", WikipediaParser.ProcessTemplates("This{{XXX}} is a test."));
            Assert.AreEqual("This is a test.", WikipediaParser.ProcessTemplates("This is a test.{{XXX}}"));
            Assert.AreEqual(string.Format("This is a test: {0:yyyy}", DateTime.UtcNow), WikipediaParser.ProcessTemplates("This is a test: {{CURRENTYEAR}}"));
            Assert.AreEqual("This is a test.", WikipediaParser.ProcessTemplates("This is {{XXX {{CURRENTYEAR}} }}a test."));
        }

        [TestMethod]
        [TestProperty("Lib", "LillTek.Advanced")]
        public void WikipediaParser_List_Bullet_OneLevel()
        {
            var wikiText =
@"
* one
* two
* three
";
            var sectionHtml =
@"<ul>
<li>one</li>
<li>two</li>
<li>three</li>
</ul>
";
            var parser = new WikipediaParser(wikiText);

            Assert.AreEqual(1, parser.Sections.Count);
            Assert.AreEqual(sectionHtml, parser.Sections[0].Html);
        }

        [TestMethod]
        [TestProperty("Lib", "LillTek.Advanced")]
        public void WikipediaParser_List_Bullet_TwoLevels()
        {
            var wikiText =
@"
* one
**1.1
**1.2
* two
* three
**3.1
** 3.2
";
            var sectionHtml =
@"<ul>
<li>one
<ul>
<li>1.1</li>
<li>1.2</li>
</ul>
</li>
<li>two</li>
<li>three
<ul>
<li>3.1</li>
<li>3.2</li>
</ul>
</li>
</ul>
";
            var parser = new WikipediaParser(wikiText);

            Assert.AreEqual(1, parser.Sections.Count);
            Assert.AreEqual(sectionHtml, parser.Sections[0].Html);
        }

        [TestMethod]
        [TestProperty("Lib", "LillTek.Advanced")]
        public void WikipediaParser_List_Bullet_ThreeLevels()
        {
            var wikiText =
@"
* one
**1.1
*** 1.1.1
*** 1.1.2
**1.2
* two
* three
**3.1
** 3.2
";
            var sectionHtml =
@"<ul>
<li>one
<ul>
<li>1.1
<ul>
<li>1.1.1</li>
<li>1.1.2</li>
</ul>
</li>
<li>1.2</li>
</ul>
</li>
<li>two</li>
<li>three
<ul>
<li>3.1</li>
<li>3.2</li>
</ul>
</li>
</ul>
";
            var parser = new WikipediaParser(wikiText);

            Assert.AreEqual(1, parser.Sections.Count);
            Assert.AreEqual(sectionHtml, parser.Sections[0].Html);
        }

        [TestMethod]
        [TestProperty("Lib", "LillTek.Advanced")]
        public void WikipediaParser_List_BulletsWithLinks()
        {
            var wikiText =
@"
* [[ Link1 ]]
* [[ Link2 ]]
* [[ Link3 ]]
";
            var sectionHtml =
@"<ul>
<li><a href=""http://www.wikipedia.org/wiki/Link1"" target=""_blank"" rel=""nofollow"">Link1</a></li>
<li><a href=""http://www.wikipedia.org/wiki/Link2"" target=""_blank"" rel=""nofollow"">Link2</a></li>
<li><a href=""http://www.wikipedia.org/wiki/Link3"" target=""_blank"" rel=""nofollow"">Link3</a></li>
</ul>
";
            var parser = new WikipediaParser(wikiText);

            Assert.AreEqual(1, parser.Sections.Count);
            Assert.AreEqual(sectionHtml, parser.Sections[0].Html);
        }

        [TestMethod]
        [TestProperty("Lib", "LillTek.Advanced")]
        public void WikipediaParser_List_Numbered_OneLevel()
        {
            var wikiText =
@"
& one
& two
& three
";
            var sectionHtml =
@"<ol>
<li>one</li>
<li>two</li>
<li>three</li>
</ol>
";
            var parser = new WikipediaParser(wikiText.Replace('&', '#'));

            Assert.AreEqual(1, parser.Sections.Count);
            Assert.AreEqual(sectionHtml, parser.Sections[0].Html);
        }

        [TestMethod]
        [TestProperty("Lib", "LillTek.Advanced")]
        public void WikipediaParser_List_Numbered_TwoLevels()
        {
            var wikiText =
@"
& one
&&1.1
&&1.2
& two
& three
&&3.1
&& 3.2
";
            var sectionHtml =
@"<ol>
<li>one
<ol>
<li>1.1</li>
<li>1.2</li>
</ol>
</li>
<li>two</li>
<li>three
<ol>
<li>3.1</li>
<li>3.2</li>
</ol>
</li>
</ol>
";
            var parser = new WikipediaParser(wikiText.Replace('&', '#'));

            Assert.AreEqual(1, parser.Sections.Count);
            Assert.AreEqual(sectionHtml, parser.Sections[0].Html);
        }

        [TestMethod]
        [TestProperty("Lib", "LillTek.Advanced")]
        public void WikipediaParser_List_NumberedList()
        {
            var wikiText =
@"
& one
&&1.1
&&& 1.1.1
&&& 1.1.2
&&1.2
& two
& three
&&3.1
&& 3.2
";
            var sectionHtml =
@"<ol>
<li>one
<ol>
<li>1.1
<ol>
<li>1.1.1</li>
<li>1.1.2</li>
</ol>
</li>
<li>1.2</li>
</ol>
</li>
<li>two</li>
<li>three
<ol>
<li>3.1</li>
<li>3.2</li>
</ol>
</li>
</ol>
";
            var parser = new WikipediaParser(wikiText.Replace('&', '#'));

            Assert.AreEqual(1, parser.Sections.Count);
            Assert.AreEqual(sectionHtml, parser.Sections[0].Html);
        }

        [TestMethod]
        [TestProperty("Lib", "LillTek.Advanced")]
        public void WikipediaParser_List_Definition()
        {
            var wikiText =
@"
; term1
:     definition1
:     definition2
; term2
:     definition3
:     definition4
";
            var sectionHtml =
@"<dl>
<dt>term1</dt>
<dd>definition1</dd>
<dd>definition2</dd>
<dt>term2</dt>
<dd>definition3</dd>
<dd>definition4</dd>
</dl>
";
            var parser = new WikipediaParser(wikiText);

            Assert.AreEqual(1, parser.Sections.Count);
            Assert.AreEqual(sectionHtml, parser.Sections[0].Html);
        }

        [TestMethod]
        [TestProperty("Lib", "LillTek.Advanced")]
        public void WikipediaParser_List_Indentation()
        {
            var wikiText =
@"
: Single indent
:: Double indent
::::: Multiple indent
";
            var sectionHtml =
@"<dl>
<dd>Single indent
<dl>
<dd>Double indent
<dl>
<dd>
<dl>
<dd>
<dl>
<dd>Multiple indent</dd>
</dl>
</dd>
</dl>
</dd>
</dl>
</dd>
</dl>
</dd>
</dl>
";
            var parser = new WikipediaParser(wikiText);

            Assert.AreEqual(1, parser.Sections.Count);
            Assert.AreEqual(sectionHtml, parser.Sections[0].Html);
        }

        [TestMethod]
        [TestProperty("Lib", "LillTek.Advanced")]
        public void WikipediaParser_List_Mixing()
        {
            var wikiText =
@"
& one
& two
&* two point one
&* two point two
& three
&; three item one
&: three def one
& four
&: four def one
&: this looks like a continuation
&: and is often used
&: instead of the BR tag
& five
&& five sub 1
&&& five sub 1 sub 1
&& five sub 2
";
            var sectionHtml =
@"<ol>
<li>one</li>
<li>two
<ul>
<li>two point one</li>
<li>two point two</li>
</ul>
</li>
<li>three
<dl>
<dt>three item one</dt>
<dd>three def one</dd>
</dl>
</li>
<li>four
<dl>
<dd>four def one</dd>
<dd>this looks like a continuation</dd>
<dd>and is often used</dd>
<dd>instead of the BR tag</dd>
</dl>
</li>
<li>five
<ol>
<li>five sub 1
<ol>
<li>five sub 1 sub 1</li>
</ol>
</li>
<li>five sub 2</li>
</ol>
</li>
</ol>
";
            var parser = new WikipediaParser(wikiText.Replace('&', '#'));

            Assert.AreEqual(1, parser.Sections.Count);
            Assert.AreEqual(sectionHtml, parser.Sections[0].Html);
        }

        [TestMethod]
        [TestProperty("Lib", "LillTek.Advanced")]
        public void WikipediaParser_List_LevelJumping()
        {
            var wikiText =
@"
& One
& Two
&&& Level Jump
";
            var sectionHtml =
@"<ol>
<li>One</li>
<li>Two
<ol>
<li>
<ol>
<li>Level Jump</li>
</ol>
</li>
</ol>
</li>
</ol>
";
            var parser = new WikipediaParser(wikiText.Replace('&', '#'));

            Assert.AreEqual(1, parser.Sections.Count);
            Assert.AreEqual(sectionHtml, parser.Sections[0].Html);
        }

        [TestMethod]
        [TestProperty("Lib", "LillTek.Advanced")]
        public void WikipediaParser_List_Complex()
        {
            var wikiText =
@"
& Start each line
& with a number sign.
&& More number signs gives deeper
&&& and deeper
&&& levels.
& Line breaks don't break levels.
&&& But jumping levels creates empty space.
& Blank lines end the list
";
            var sectionHtml =
@"<ol>
<li>Start each line</li>
<li>with a number sign.
<ol>
<li>More number signs gives deeper
<ol>
<li>and deeper</li>
<li>levels.</li>
</ol>
</li>
</ol>
</li>
<li>Line breaks don't break levels.
<ol>
<li>
<ol>
<li>But jumping levels creates empty space.</li>
</ol>
</li>
</ol>
</li>
<li>Blank lines end the list</li>
</ol>
";
            var parser = new WikipediaParser(wikiText.Replace('&', '#'));

            Assert.AreEqual(1, parser.Sections.Count);
            Assert.AreEqual(sectionHtml, parser.Sections[0].Html);
        }

        [TestMethod]
        [TestProperty("Lib", "LillTek.Advanced")]
        public void WikipediaParser_Paragraph_SingleLine()
        {
            var parser = new WikipediaParser(
@"
This is a test of the emergency broadcasting system.
");

            string sectionHtml =
@"<p>
This is a test of the emergency broadcasting system.
</p>
";
            Assert.AreEqual(1, parser.Sections.Count);
            Assert.AreEqual(sectionHtml, parser.Sections[0].Html);
        }

        [TestMethod]
        [TestProperty("Lib", "LillTek.Advanced")]
        public void WikipediaParser_Paragraph_MultiLine()
        {
            var parser = new WikipediaParser(
@"
This is a test of the emergency broadcasting system.
This is only a test.  In the event of a real emergency
we'd be pretty much screwed right now.
");

            string sectionHtml =
@"<p>
This is a test of the emergency broadcasting system.
This is only a test.  In the event of a real emergency
we'd be pretty much screwed right now.
</p>
";
            Assert.AreEqual(1, parser.Sections.Count);
            Assert.AreEqual(sectionHtml, parser.Sections[0].Html);
        }

        [TestMethod]
        [TestProperty("Lib", "LillTek.Advanced")]
        public void WikipediaParser_Preformatted()
        {
            var parser = new WikipediaParser(
@"
 This is a test of the emergency broadcasting system.
 This is only a test.  In the event of a real emergency
 we'd be pretty much screwed right now.
");

            string sectionHtml =
@"<pre>
This is a test of the emergency broadcasting system.
This is only a test.  In the event of a real emergency
we'd be pretty much screwed right now.
</pre>
";
            Assert.AreEqual(1, parser.Sections.Count);
            Assert.AreEqual(sectionHtml, parser.Sections[0].Html);
        }

        [TestMethod]
        [TestProperty("Lib", "LillTek.Advanced")]
        public void WikipediaParser_Link_Basic()
        {
            var parser = new WikipediaParser(
@"
[[ Test Link ]]
");

            string sectionHtml =
@"<p>
<a href=""http://www.wikipedia.org/wiki/Test_Link"" target=""_blank"" rel=""nofollow"">Test Link</a>
</p>
";
            Assert.AreEqual(1, parser.Sections.Count);
            Assert.AreEqual(sectionHtml, parser.Sections[0].Html);
        }

        [TestMethod]
        [TestProperty("Lib", "LillTek.Advanced")]
        public void WikipediaParser_Link_AltText()
        {
            var parser = new WikipediaParser(
@"
[[ Test Link | Hello World! ]]
");

            string sectionHtml =
@"<p>
<a href=""http://www.wikipedia.org/wiki/Test_Link"" target=""_blank"" rel=""nofollow"">Hello World!</a>
</p>
";
            Assert.AreEqual(1, parser.Sections.Count);
            Assert.AreEqual(sectionHtml, parser.Sections[0].Html);
        }

        [TestMethod]
        [TestProperty("Lib", "LillTek.Advanced")]
        public void WikipediaParser_Link_Namespace()
        {
            var parser = new WikipediaParser(
@"
[[ Help:Test Link ]]
");

            string sectionHtml =
@"<p>
<a href=""http://www.wikipedia.org/wiki/Help:Test_Link"" target=""_blank"" rel=""nofollow"">Help:Test Link</a>
</p>
";
            Assert.AreEqual(1, parser.Sections.Count);
            Assert.AreEqual(sectionHtml, parser.Sections[0].Html);
        }

        [TestMethod]
        [TestProperty("Lib", "LillTek.Advanced")]
        public void WikipediaParser_Link_DiscardImage()
        {
            var parser = new WikipediaParser(
@"
[[ Image:Test Link ]] This is a test.
");

            string sectionHtml =
@"<p>
This is a test.
</p>
";
            Assert.AreEqual(1, parser.Sections.Count);
            Assert.AreEqual(sectionHtml, parser.Sections[0].Html);
        }

        [TestMethod]
        [TestProperty("Lib", "LillTek.Advanced")]
        public void WikipediaParser_Link_DiscardFile()
        {
            var parser = new WikipediaParser(
@"
[[ File:Test Link ]] This is a test.
");

            string sectionHtml =
@"<p>
This is a test.
</p>
";
            Assert.AreEqual(1, parser.Sections.Count);
            Assert.AreEqual(sectionHtml, parser.Sections[0].Html);
        }

        [TestMethod]
        [TestProperty("Lib", "LillTek.Advanced")]
        public void WikipediaParser_Link_Endings()
        {
            var parser = new WikipediaParser(
@"
[[ Help ]]ers
");

            string sectionHtml =
@"<p>
<a href=""http://www.wikipedia.org/wiki/Help"" target=""_blank"" rel=""nofollow"">Helpers</a>
</p>
";
            Assert.AreEqual(1, parser.Sections.Count);
            Assert.AreEqual(sectionHtml, parser.Sections[0].Html);
        }

        [TestMethod]
        [TestProperty("Lib", "LillTek.Advanced")]
        public void WikipediaParser_Link_LocalAnchor()
        {
            var parser = new WikipediaParser(
@"
<page><title>Test Page</title><text>
[[ #anchor ]]
</text></page>
");

            string sectionHtml =
@"<p>
<a href=""http://www.wikipedia.org/wiki/Test_Page#anchor"" target=""_blank"" rel=""nofollow"">Test Page</a>
</p>
";
            Assert.AreEqual(1, parser.Sections.Count);
            Assert.AreEqual(sectionHtml, parser.Sections[0].Html);
        }

        [TestMethod]
        [TestProperty("Lib", "LillTek.Advanced")]
        public void WikipediaParser_Link_LocalAnchorAltText()
        {
            var parser = new WikipediaParser(
@"
<page><title>Test Page</title><text>
[[ #anchor | My Page ]]
</text></page>
");

            string sectionHtml =
@"<p>
<a href=""http://www.wikipedia.org/wiki/Test_Page#anchor"" target=""_blank"" rel=""nofollow"">My Page</a>
</p>
";
            Assert.AreEqual(1, parser.Sections.Count);
            Assert.AreEqual(sectionHtml, parser.Sections[0].Html);
        }

        [TestMethod]
        [TestProperty("Lib", "LillTek.Advanced")]
        public void WikipediaParser_Link_PageLinkAnchor()
        {
            var parser = new WikipediaParser(
@"
<page><title>Test Page</title><text>
[[ Another Page#anchor ]]
</text></page>
");

            string sectionHtml =
@"<p>
<a href=""http://www.wikipedia.org/wiki/Another_Page#anchor"" target=""_blank"" rel=""nofollow"">Another Page</a>
</p>
";
            Assert.AreEqual(1, parser.Sections.Count);
            Assert.AreEqual(sectionHtml, parser.Sections[0].Html);
        }

        [TestMethod]
        [TestProperty("Lib", "LillTek.Advanced")]
        public void WikipediaParser_Link_PageLinkAnchorAltText()
        {

            var parser = new WikipediaParser(
@"
<page><title>Test Page</title><text>
[[ Another Page#anchor | My Page ]]
</text></page>
");

            string sectionHtml =
@"<p>
<a href=""http://www.wikipedia.org/wiki/Another_Page#anchor"" target=""_blank"" rel=""nofollow"">My Page</a>
</p>
";
            Assert.AreEqual(1, parser.Sections.Count);
            Assert.AreEqual(sectionHtml, parser.Sections[0].Html);
        }

        [TestMethod]
        [TestProperty("Lib", "LillTek.Advanced")]
        public void WikipediaParser_Link_Http()
        {
            var parser = new WikipediaParser(
@"
Link: http://www.lilltek.com
");

            string sectionHtml =
@"<p>
Link: <a href=""http://www.lilltek.com"" target=""_blank"" rel=""nofollow"">http://www.lilltek.com</a>
</p>
";
            Assert.AreEqual(1, parser.Sections.Count);
            Assert.AreEqual(sectionHtml, parser.Sections[0].Html);
        }

        [TestMethod]
        [TestProperty("Lib", "LillTek.Advanced")]
        public void WikipediaParser_Link_Https()
        {
            var parser = new WikipediaParser(
@"
Link: https://www.lilltek.com
");

            string sectionHtml =
@"<p>
Link: <a href=""https://www.lilltek.com"" target=""_blank"" rel=""nofollow"">https://www.lilltek.com</a>
</p>
";
            Assert.AreEqual(1, parser.Sections.Count);
            Assert.AreEqual(sectionHtml, parser.Sections[0].Html);
        }

        [TestMethod]
        [TestProperty("Lib", "LillTek.Advanced")]
        public void WikipediaParser_Link_MultipleHttp()
        {
            var parser = new WikipediaParser(
@"
Link1: http://www.lilltek.com
Link2: http://www.google.com/test.aspx?hello=world
Link3: https://microsoft.com/
");

            string sectionHtml =
@"<p>
Link1: <a href=""http://www.lilltek.com"" target=""_blank"" rel=""nofollow"">http://www.lilltek.com</a>
Link2: <a href=""http://www.google.com/test.aspx?hello=world"" target=""_blank"" rel=""nofollow"">http://www.google.com/test.aspx?hello=world</a>
Link3: <a href=""https://microsoft.com/"" target=""_blank"" rel=""nofollow"">https://microsoft.com/</a>
</p>
";
            Assert.AreEqual(1, parser.Sections.Count);
            Assert.AreEqual(sectionHtml, parser.Sections[0].Html);
        }

        [TestMethod]
        [TestProperty("Lib", "LillTek.Advanced")]
        public void WikipediaParser_Link_MultipleWiki()
        {
            var parser = new WikipediaParser(
@"
Link1: [[ Test Link 1 ]]
Link2: [[ Test Link 2 ]]
Link3: [[ Test Link 3 ]]
");

            string sectionHtml =
@"<p>
Link1: <a href=""http://www.wikipedia.org/wiki/Test_Link_1"" target=""_blank"" rel=""nofollow"">Test Link 1</a>
Link2: <a href=""http://www.wikipedia.org/wiki/Test_Link_2"" target=""_blank"" rel=""nofollow"">Test Link 2</a>
Link3: <a href=""http://www.wikipedia.org/wiki/Test_Link_3"" target=""_blank"" rel=""nofollow"">Test Link 3</a>
</p>
";
            Assert.AreEqual(1, parser.Sections.Count);
            Assert.AreEqual(sectionHtml, parser.Sections[0].Html);
        }

        [TestMethod]
        [TestProperty("Lib", "LillTek.Advanced")]
        public void WikipediaParser_Link_MultipleMixed()
        {
            var parser = new WikipediaParser(
@"
Link1: [[ Test Link 1 ]]
Link2: http://www.google.com/test.aspx?hello=world
Link3: [[ Test Link 3 ]]
Link4: https://microsoft.com/
Link5: [[ Test Link 5 ]]
");

            string sectionHtml =
@"<p>
Link1: <a href=""http://www.wikipedia.org/wiki/Test_Link_1"" target=""_blank"" rel=""nofollow"">Test Link 1</a>
Link2: <a href=""http://www.google.com/test.aspx?hello=world"" target=""_blank"" rel=""nofollow"">http://www.google.com/test.aspx?hello=world</a>
Link3: <a href=""http://www.wikipedia.org/wiki/Test_Link_3"" target=""_blank"" rel=""nofollow"">Test Link 3</a>
Link4: <a href=""https://microsoft.com/"" target=""_blank"" rel=""nofollow"">https://microsoft.com/</a>
Link5: <a href=""http://www.wikipedia.org/wiki/Test_Link_5"" target=""_blank"" rel=""nofollow"">Test Link 5</a>
</p>
";
            Assert.AreEqual(1, parser.Sections.Count);
            Assert.AreEqual(sectionHtml, parser.Sections[0].Html);
        }

        [TestMethod]
        [TestProperty("Lib", "LillTek.Advanced")]
        public void WikipediaParser_Link_Nested()
        {
            // Make sure that links with other links nested within are removed.

            var parser = new WikipediaParser(
@"
Hello
Link1: [[ Test Link 1 | [[ Nested Link ]] [[ Another Nested ]] ]]
Link2: [[ Test Link 2 ]]
World
");

            string sectionHtml =
@"<p>
Hello
Link1: 
Link2: <a href=""http://www.wikipedia.org/wiki/Test_Link_2"" target=""_blank"" rel=""nofollow"">Test Link 2</a>
World
</p>
";
            Assert.AreEqual(1, parser.Sections.Count);
            Assert.AreEqual(sectionHtml, parser.Sections[0].Html);
        }

        [TestMethod]
        [TestProperty("Lib", "LillTek.Advanced")]
        public void WikipediaParser_Link_Empty()
        {
            var parser = new WikipediaParser(
@"
Hello
Link: [[ ]]
World
");

            string sectionHtml =
@"<p>
Hello
Link: 
World
</p>
";
            Assert.AreEqual(1, parser.Sections.Count);
            Assert.AreEqual(sectionHtml, parser.Sections[0].Html);
        }

        [TestMethod]
        [TestProperty("Lib", "LillTek.Advanced")]
        public void WikipediaParser_Link_External()
        {
            var parser = new WikipediaParser(
@"
Link1: [ http://www.microsoft.com/ Microsoft ]
Link2: [ http://google.com ]
Link3: [ https://www.lilltek.com/ LillTek ]
Link4: [ https://forbes.com ]
");

            string sectionHtml =
@"<p>
Link1: <a href=""http://www.microsoft.com/"" target=""_blank"" rel=""nofollow"">Microsoft</a>
Link2: [<a href=""http://google.com"" target=""_blank"" rel=""nofollow"">1</a>]
Link3: <a href=""https://www.lilltek.com/"" target=""_blank"" rel=""nofollow"">LillTek</a>
Link4: [<a href=""https://forbes.com"" target=""_blank"" rel=""nofollow"">2</a>]
</p>
";
            Assert.AreEqual(1, parser.Sections.Count);
            Assert.AreEqual(sectionHtml, parser.Sections[0].Html);
        }

        [TestMethod]
        [TestProperty("Lib", "LillTek.Advanced")]
        public void WikipediaParser_Artifact_Removal()
        {
            var parser = new WikipediaParser(
@"
before()-after
before( )-after
");

            string sectionHtml =
@"<p>
before-after
before-after
</p>
";
            Assert.AreEqual(1, parser.Sections.Count);
            Assert.AreEqual(sectionHtml, parser.Sections[0].Html);
        }

        [TestMethod]
        [TestProperty("Lib", "LillTek.Advanced")]
        public void WikipediaParser_HRTag()
        {
            var parser = new WikipediaParser(
@"
Test
----
");

            string sectionHtml =
@"<p>
Test
<hr />
</p>
";
            Assert.AreEqual(1, parser.Sections.Count);
            Assert.AreEqual(sectionHtml, parser.Sections[0].Html);
        }

        [TestMethod]
        [TestProperty("Lib", "LillTek.Advanced")]
        public void WikipediaParser_CssClass()
        {
            var wikiText =
@"<page>
<title>Hello World</title>
<text>
== Section 1 ==
This is some text.
& Start each line
& with a number sign.
&& More number signs gives deeper
&&& and deeper
&&& levels.
& Line breaks don't break levels.
&&& But jumping levels creates empty space.
& Blank lines end the list
=== Section 2 ===
==== Section 3 ====
===== Section 4 =====
</text>
</page>
";
            var sectionHtml =
@"<html>
<head>
<title>Hello World</title>
</head>
<body class=""wiki"">
<h1 class=""wiki"">Hello World</h1>
<h2 class=""wiki"">Section 1</h2>
<p class=""wiki"">
This is some text.
</p>
<ol class=""wiki"">
<li class=""wiki"">Start each line</li>
<li class=""wiki"">with a number sign.
<ol class=""wiki"">
<li class=""wiki"">More number signs gives deeper
<ol class=""wiki"">
<li class=""wiki"">and deeper</li>
<li class=""wiki"">levels.</li>
</ol>
</li>
</ol>
</li>
<li class=""wiki"">Line breaks don't break levels.
<ol class=""wiki"">
<li class=""wiki"">
<ol class=""wiki"">
<li class=""wiki"">But jumping levels creates empty space.</li>
</ol>
</li>
</ol>
</li>
<li class=""wiki"">Blank lines end the list</li>
</ol>
<h3 class=""wiki"">Section 2</h3>
<h4 class=""wiki"">Section 3</h4>
<h5 class=""wiki"">Section 4</h5>
</body>
</html>
";
            var options = new WikipediaParserOptions() { CssClass = "wiki" };
            var parser = new WikipediaParser(wikiText.Replace('&', '#'), options);

            Assert.AreEqual(sectionHtml, parser.ToHtml());
        }

        private string ReadResourceText(string name)
        {
            var assembly = Assembly.GetExecutingAssembly();

            using (var reader = new StreamReader(assembly.GetManifestResourceStream("LillTek.Advanced.Test.WikipediaFiles." + name)))
            {
                return reader.ReadToEnd();
            }
        }

        [TestMethod]
        [TestProperty("Lib", "LillTek.Advanced")]
        public void WikipediaParser_Wikipedia_Test()
        {
            var inputFile  = @"Seattle.txt";
            var outputPath = string.Format(@"C:\Temp\WikipediaTest\{0}.htm", Path.GetFileNameWithoutExtension(inputFile));
            var parser     = new WikipediaParser(ReadResourceText(inputFile));

            Assert.AreEqual("http://www.wikipedia.org/wiki/Seattle", parser.SourceUri);

            parser.RenderAsHtmlPage(outputPath);

            inputFile  = @"Lynnwood_Washington.txt";
            outputPath = string.Format(@"C:\Temp\WikipediaTest\{0}.htm", Path.GetFileNameWithoutExtension(inputFile));
            parser     = new WikipediaParser(ReadResourceText(inputFile));

            Assert.AreEqual("http://www.wikipedia.org/wiki/Lynnwood,_Washington", parser.SourceUri);

            parser.RenderAsHtmlPage(outputPath);
        }
    }
}

