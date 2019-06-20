//-----------------------------------------------------------------------------
// FILE:        _HtmlParser.cs
// CONTRIBUTOR: Jeff Lill
// COPYRIGHT:   Copyright (c) 2005-2015 by Jeffrey Lill.  All rights reserved.
// DESCRIPTION: Unit tests

using System;
using System.Xml;
using System.Xml.Linq;

using Microsoft.VisualStudio.TestTools.UnitTesting;

using LillTek.Common;
using LillTek.Advanced;

namespace LillTek.Advanced.Test
{
    [TestClass]
    public class _HtmlParser
    {
        [TestMethod]
        [TestProperty("Lib", "LillTek.Advanced")]
        public void HtmlParser_Entities()
        {
            Assert.AreEqual("", HtmlParser.ConvertEntities(""));
            Assert.AreEqual("a", HtmlParser.ConvertEntities("a"));
            Assert.AreEqual("abcdefg", HtmlParser.ConvertEntities("abcdefg"));
            Assert.AreEqual(" ", HtmlParser.ConvertEntities("&nbsp;"));
            Assert.AreEqual("<", HtmlParser.ConvertEntities("&lt;"));
            Assert.AreEqual(">", HtmlParser.ConvertEntities("&gt;"));
            Assert.AreEqual("\"", HtmlParser.ConvertEntities("&quot;"));
            Assert.AreEqual("\"This is a test\"", HtmlParser.ConvertEntities("&quot;This is a test&quot;"));
            Assert.AreEqual("10 < 20", HtmlParser.ConvertEntities("10 &lt; 20"));

            Assert.AreEqual("Hello " + (char)55, HtmlParser.ConvertEntities("Hello &#55;"));
            Assert.AreEqual("Hello " + (char)0x55, HtmlParser.ConvertEntities("Hello &#x55;"));

            Assert.AreEqual("&", HtmlParser.ConvertEntities("&"));
            Assert.AreEqual("&abc", HtmlParser.ConvertEntities("&abc"));
            Assert.AreEqual("&abcdefghijklmnop", HtmlParser.ConvertEntities("&abcdefghijklmnop"));
            Assert.AreEqual("&abcdef;", HtmlParser.ConvertEntities("&abcdef;"));
        }

        private void Match(HtmlItem item, HtmlItemType itemType)
        {
            Assert.AreEqual(itemType, item);
        }

        private void Match(HtmlItem item, HtmlItemType itemType, string text)
        {
            Assert.AreEqual(itemType, item.ItemType);
            Assert.AreEqual(text, item.Text);
        }

        private void MatchAttribute(HtmlItem item, string name, string value)
        {
            string v;

            Assert.IsTrue(item.Attributes.TryGetValue(name, out v));
            Assert.AreEqual(value, v);
        }

        [TestMethod]
        [TestProperty("Lib", "LillTek.Advanced")]
        public void HtmlParser_PlainTags()
        {
            const string source =
@"
<a/>
<a></a>
<HTML 
></HTML 
>
";
            HtmlParser parser = new HtmlParser(source);
            HtmlItem item;

            item = parser.Read();
            Assert.AreEqual(0, item.Attributes.Count);
            Match(item, HtmlItemType.OpenTag, "a");
            Match(parser.Read(), HtmlItemType.CloseTag, "a");
            Match(parser.Read(), HtmlItemType.OpenTag, "a");
            Match(parser.Read(), HtmlItemType.CloseTag, "a");
            Match(parser.Read(), HtmlItemType.OpenTag, "html");
            Match(parser.Read(), HtmlItemType.CloseTag, "html");

            Assert.IsNull(parser.Read());
            Assert.IsNull(parser.Read());
        }

        [TestMethod]
        [TestProperty("Lib", "LillTek.Advanced")]
        public void HtmlParser_Attributes()
        {
            const string source =
@"
<a href=""http://foo-1_2.com"" />
<a href='http://foo-2_3.com' />
<a HREF=http://foo-3_4.com />
<a HREF=http://foo-3_4.com?test=a />
<a 
id=10 
href=20
/>
<test param/>
<test param= />
<test param=""Mac &amp; Jack""/>
<test param=/foo.htm />
<test param =""foo"" />
";
            HtmlParser parser = new HtmlParser(source);
            HtmlItem item;

            item = parser.Read();
            Match(item, HtmlItemType.OpenTag, "a");
            Assert.AreEqual(1, item.Attributes.Count);
            MatchAttribute(item, "href", "http://foo-1_2.com");
            Match(parser.Read(), HtmlItemType.CloseTag, "a");

            item = parser.Read();
            Match(item, HtmlItemType.OpenTag, "a");
            Assert.AreEqual(1, item.Attributes.Count);
            MatchAttribute(item, "href", "http://foo-2_3.com");
            Match(parser.Read(), HtmlItemType.CloseTag, "a");

            item = parser.Read();
            Match(item, HtmlItemType.OpenTag, "a");
            Assert.AreEqual(1, item.Attributes.Count);
            MatchAttribute(item, "href", "http://foo-3_4.com");
            Match(parser.Read(), HtmlItemType.CloseTag, "a");

            item = parser.Read();
            Match(item, HtmlItemType.OpenTag, "a");
            Assert.AreEqual(1, item.Attributes.Count);
            MatchAttribute(item, "href", "http://foo-3_4.com?test=a");
            Match(parser.Read(), HtmlItemType.CloseTag, "a");

            item = parser.Read();
            Match(item, HtmlItemType.OpenTag, "a");
            Assert.AreEqual(2, item.Attributes.Count);
            MatchAttribute(item, "id", "10");
            MatchAttribute(item, "href", "20");
            Match(parser.Read(), HtmlItemType.CloseTag, "a");

            item = parser.Read();
            Match(item, HtmlItemType.OpenTag, "test");
            MatchAttribute(item, "param", string.Empty);
            Match(parser.Read(), HtmlItemType.CloseTag, "test");

            item = parser.Read();
            Match(item, HtmlItemType.OpenTag, "test");
            MatchAttribute(item, "param", string.Empty);
            Match(parser.Read(), HtmlItemType.CloseTag, "test");

            item = parser.Read();
            Match(item, HtmlItemType.OpenTag, "test");
            MatchAttribute(item, "param", "Mac & Jack");
            item = parser.Read();
            Match(item, HtmlItemType.CloseTag, "test");

            item = parser.Read();
            Match(item, HtmlItemType.OpenTag, "test");
            MatchAttribute(item, "param", "/foo.htm");
            item = parser.Read();
            Match(item, HtmlItemType.CloseTag, "test");

            item = parser.Read();
            Match(item, HtmlItemType.OpenTag, "test");
            MatchAttribute(item, "param", "foo");
            item = parser.Read();
            Match(item, HtmlItemType.CloseTag, "test");

            Assert.IsNull(parser.Read());
            Assert.IsNull(parser.Read());
        }

        [TestMethod]
        [TestProperty("Lib", "LillTek.Advanced")]
        public void HtmlParser_BadTag_Unclosed()
        {
            HtmlParser parser;
            HtmlItem item;

            parser = new HtmlParser("<test1 <test2></test2></test1>");
            Match(parser.Read(), HtmlItemType.OpenTag, "test1");
            Match(parser.Read(), HtmlItemType.OpenTag, "test2");
            Match(parser.Read(), HtmlItemType.CloseTag, "test2");
            Match(parser.Read(), HtmlItemType.CloseTag, "test1");
            Assert.IsNull(parser.Read());

            parser = new HtmlParser("<test1<test2></test2></test1>");
            Match(parser.Read(), HtmlItemType.OpenTag, "test1");
            Match(parser.Read(), HtmlItemType.OpenTag, "test2");
            Match(parser.Read(), HtmlItemType.CloseTag, "test2");
            Match(parser.Read(), HtmlItemType.CloseTag, "test1");
            Assert.IsNull(parser.Read());

            parser = new HtmlParser("<test1 src=foo<test2></test2></test1>");
            item = parser.Read();
            Match(item, HtmlItemType.OpenTag, "test1");
            MatchAttribute(item, "src", "foo");
            Match(parser.Read(), HtmlItemType.OpenTag, "test2");
            Match(parser.Read(), HtmlItemType.CloseTag, "test2");
            Match(parser.Read(), HtmlItemType.CloseTag, "test1");
            Assert.IsNull(parser.Read());

            parser = new HtmlParser("<test1 src=foo <test2></test2></test1>");
            item = parser.Read();
            Match(item, HtmlItemType.OpenTag, "test1");
            MatchAttribute(item, "src", "foo");
            Match(parser.Read(), HtmlItemType.OpenTag, "test2");
            Match(parser.Read(), HtmlItemType.CloseTag, "test2");
            Match(parser.Read(), HtmlItemType.CloseTag, "test1");
            Assert.IsNull(parser.Read());

            parser = new HtmlParser("<test1 src=\"foo\"<test2></test2></test1>");
            item = parser.Read();
            Match(item, HtmlItemType.OpenTag, "test1");
            MatchAttribute(item, "src", "foo");
            Match(parser.Read(), HtmlItemType.OpenTag, "test2");
            Match(parser.Read(), HtmlItemType.CloseTag, "test2");
            Match(parser.Read(), HtmlItemType.CloseTag, "test1");
            Assert.IsNull(parser.Read());
        }

        [TestMethod]
        [TestProperty("Lib", "LillTek.Advanced")]
        public void HtmlParser_NestedElements()
        {
            const string source =
@"
<html>
    <head>
        <title>Test</title>
    </head>
    <body>
        <a href=www.foo.com>Foo.com</a>
    </body>
</html>
";
            HtmlParser parser = new HtmlParser(source);
            HtmlItem item;

            Match(parser.Read(), HtmlItemType.OpenTag, "html");
            Match(parser.Read(), HtmlItemType.OpenTag, "head");
            Match(parser.Read(), HtmlItemType.OpenTag, "title");
            Match(parser.Read(), HtmlItemType.Text, "Test");
            Match(parser.Read(), HtmlItemType.CloseTag, "title");
            Match(parser.Read(), HtmlItemType.CloseTag, "head");
            Match(parser.Read(), HtmlItemType.OpenTag, "body");

            item = parser.Read();
            Match(item, HtmlItemType.OpenTag, "a");
            MatchAttribute(item, "href", "www.foo.com");
            Match(parser.Read(), HtmlItemType.Text, "Foo.com");
            Match(parser.Read(), HtmlItemType.CloseTag, "a");
            Match(parser.Read(), HtmlItemType.CloseTag, "body");
            Match(parser.Read(), HtmlItemType.CloseTag, "html");

            Assert.IsNull(parser.Read());
            Assert.IsNull(parser.Read());
        }

        [TestMethod]
        [TestProperty("Lib", "LillTek.Advanced")]
        public void HtmlParser_OmittedElements()
        {
            HtmlParser parser;

            parser = new HtmlParser("<a><b><c><d></C></B></A>");
            Match(parser.Read(), HtmlItemType.OpenTag, "a");
            Match(parser.Read(), HtmlItemType.OpenTag, "b");
            Match(parser.Read(), HtmlItemType.OpenTag, "c");
            Match(parser.Read(), HtmlItemType.OpenTag, "d");
            Match(parser.Read(), HtmlItemType.CloseTag, "d");
            Match(parser.Read(), HtmlItemType.CloseTag, "c");
            Match(parser.Read(), HtmlItemType.CloseTag, "b");
            Match(parser.Read(), HtmlItemType.CloseTag, "a");
            Assert.IsNull(parser.Read());

            parser = new HtmlParser("<a><b><c><d></a>");
            Match(parser.Read(), HtmlItemType.OpenTag, "a");
            Match(parser.Read(), HtmlItemType.OpenTag, "b");
            Match(parser.Read(), HtmlItemType.OpenTag, "c");
            Match(parser.Read(), HtmlItemType.OpenTag, "d");
            Match(parser.Read(), HtmlItemType.CloseTag, "d");
            Match(parser.Read(), HtmlItemType.CloseTag, "c");
            Match(parser.Read(), HtmlItemType.CloseTag, "b");
            Match(parser.Read(), HtmlItemType.CloseTag, "a");
            Assert.IsNull(parser.Read());

            parser = new HtmlParser("<a><b><c><d>");
            Match(parser.Read(), HtmlItemType.OpenTag, "a");
            Match(parser.Read(), HtmlItemType.OpenTag, "b");
            Match(parser.Read(), HtmlItemType.OpenTag, "c");
            Match(parser.Read(), HtmlItemType.OpenTag, "d");
            Match(parser.Read(), HtmlItemType.CloseTag, "d");
            Match(parser.Read(), HtmlItemType.CloseTag, "c");
            Match(parser.Read(), HtmlItemType.CloseTag, "b");
            Match(parser.Read(), HtmlItemType.CloseTag, "a");
            Assert.IsNull(parser.Read());
        }

        [TestMethod]
        [TestProperty("Lib", "LillTek.Advanced")]
        public void HtmlParser_Text()
        {
            HtmlParser parser;

            parser = new HtmlParser(
@"
This is a test
of the emergency
broadcasting system.
<a>
This is only a test.
");

            Match(parser.Read(), HtmlItemType.Text, "This is a test\r\nof the emergency\r\nbroadcasting system.");
            Match(parser.Read(), HtmlItemType.OpenTag, "a");
            Match(parser.Read(), HtmlItemType.Text, "This is only a test.");
            Match(parser.Read(), HtmlItemType.CloseTag, "a");
            Assert.IsNull(parser.Read());

            parser = new HtmlParser("Mac &amp; Jack");
            Match(parser.Read(), HtmlItemType.Text, "Mac & Jack");
            Assert.IsNull(parser.Read());
        }

        [TestMethod]
        [TestProperty("Lib", "LillTek.Advanced")]
        public void HtmlParser_CDATA()
        {
            HtmlParser parser;

            parser = new HtmlParser(
@"
<test>
<![CDATA[ this is a <test> of the system. ]]>
</test>
");
            Match(parser.Read(), HtmlItemType.OpenTag, "test");
            Match(parser.Read(), HtmlItemType.Text, " this is a <test> of the system. ");
            Match(parser.Read(), HtmlItemType.CloseTag, "test");

            parser = new HtmlParser(
@"
<test>
<![CDATA[ this is a <test> of the system. 
");
            Match(parser.Read(), HtmlItemType.OpenTag, "test");
            Match(parser.Read(), HtmlItemType.Text, " this is a <test> of the system. \r\n");
            Match(parser.Read(), HtmlItemType.CloseTag, "test");

            parser = new HtmlParser(
@"
<test>
<![CDATA[ this is a <test> of the system. ]
");
            Match(parser.Read(), HtmlItemType.OpenTag, "test");
            Match(parser.Read(), HtmlItemType.Text, " this is a <test> of the system. ]\r\n");
            Match(parser.Read(), HtmlItemType.CloseTag, "test");

            parser = new HtmlParser(
@"
<test>
<![CDATA[ this is a <test> of the system. ]]
");
            Match(parser.Read(), HtmlItemType.OpenTag, "test");
            Match(parser.Read(), HtmlItemType.Text, " this is a <test> of the system. ]]\r\n");
            Match(parser.Read(), HtmlItemType.CloseTag, "test");
            parser = new HtmlParser(
@"
<test>
<![CDATA[ this is a <test> of the system. ]]>
</test>
");
            parser.IgnoreText = true;
            Match(parser.Read(), HtmlItemType.OpenTag, "test");
            Match(parser.Read(), HtmlItemType.CloseTag, "test");
        }

        [TestMethod]
        [TestProperty("Lib", "LillTek.Advanced")]
        public void HtmlParser_MarkedSections()
        {
            HtmlParser parser;

            parser = new HtmlParser(
@"
<test>
<![IGNORE[ this is a <test> of the system. ]]>
</test>
");
            Match(parser.Read(), HtmlItemType.OpenTag, "test");
            Match(parser.Read(), HtmlItemType.CloseTag, "test");

            parser = new HtmlParser(
@"
<test>
<![INCLUDE[ this is a <test> of the system. ]]>
</test>
");
            Match(parser.Read(), HtmlItemType.OpenTag, "test");
            Match(parser.Read(), HtmlItemType.CloseTag, "test");
        }

        [TestMethod]
        [TestProperty("Lib", "LillTek.Advanced")]
        public void HtmlParser_Comments()
        {
            HtmlParser parser;

            parser = new HtmlParser(
@"
<!--This is a comment
that extends over multiple
lines.-->
");
            Match(parser.Read(), HtmlItemType.Comment, "This is a comment\r\nthat extends over multiple\r\nlines.");
            Assert.IsNull(parser.Read());

            parser = new HtmlParser(
@"
<a><!-- Comment --></a>
");
            Match(parser.Read(), HtmlItemType.OpenTag, "a");
            Match(parser.Read(), HtmlItemType.Comment, " Comment ");
            Match(parser.Read(), HtmlItemType.CloseTag, "a");
            Assert.IsNull(parser.Read());
        }

        [TestMethod]
        [TestProperty("Lib", "LillTek.Advanced")]
        public void HtmlParser_BadTag()
        {
            HtmlParser parser;

            parser = new HtmlParser("<");
            Assert.IsNull(parser.Read());

            parser = new HtmlParser("<>");
            Assert.IsNull(parser.Read());

            parser = new HtmlParser("</>");
            Assert.IsNull(parser.Read());

            parser = new HtmlParser("<a");
            Assert.IsNull(parser.Read());

            parser = new HtmlParser("<a/");
            Assert.IsNull(parser.Read());

            parser = new HtmlParser("<a /");
            Assert.IsNull(parser.Read());

            parser = new HtmlParser("<a param");
            Assert.IsNull(parser.Read());

            parser = new HtmlParser("<a param=");
            Assert.IsNull(parser.Read());

            parser = new HtmlParser("<a param=abcd");
            Assert.IsNull(parser.Read());

            parser = new HtmlParser("<a param=abcd ");
            Assert.IsNull(parser.Read());

            parser = new HtmlParser("<><a>");
            Match(parser.Read(), HtmlItemType.OpenTag, "a");
            Match(parser.Read(), HtmlItemType.CloseTag, "a");
            Assert.IsNull(parser.Read());

            parser = new HtmlParser("</><a>");
            Match(parser.Read(), HtmlItemType.OpenTag, "a");
            Match(parser.Read(), HtmlItemType.CloseTag, "a");
            Assert.IsNull(parser.Read());

            parser = new HtmlParser("<a><b><c></d>Test");
            Match(parser.Read(), HtmlItemType.OpenTag, "a");
            Match(parser.Read(), HtmlItemType.OpenTag, "b");
            Match(parser.Read(), HtmlItemType.OpenTag, "c");
            Match(parser.Read(), HtmlItemType.Text, "Test");
            Match(parser.Read(), HtmlItemType.CloseTag, "c");
            Match(parser.Read(), HtmlItemType.CloseTag, "b");
            Match(parser.Read(), HtmlItemType.CloseTag, "a");
            Assert.IsNull(parser.Read());
        }

        [TestMethod]
        [TestProperty("Lib", "LillTek.Advanced")]
        public void HtmlParser_BadComments()
        {
            HtmlParser parser;

            parser = new HtmlParser("<!--");
            Match(parser.Read(), HtmlItemType.Comment, string.Empty);
            Assert.IsNull(parser.Read());

            parser = new HtmlParser("<!-- Test -");
            Match(parser.Read(), HtmlItemType.Comment, " Test -");
            Assert.IsNull(parser.Read());

            parser = new HtmlParser("<!-- Test --");
            Match(parser.Read(), HtmlItemType.Comment, " Test --");
            Assert.IsNull(parser.Read());

            parser = new HtmlParser("<a><!-- Test --");
            Match(parser.Read(), HtmlItemType.OpenTag, "a");
            Match(parser.Read(), HtmlItemType.Comment, " Test --");
            Match(parser.Read(), HtmlItemType.CloseTag, "a");
            Assert.IsNull(parser.Read());
        }

        [TestMethod]
        [TestProperty("Lib", "LillTek.Advanced")]
        public void HtmlParser_HtmlDOM()
        {
            XmlDocument doc;
            XmlElement root;

            doc = HtmlParser.Parse("<html><head><title>Test</title></head><body>Hello World!</body></html>");
            root = doc["root"]["html"];
            Assert.IsNotNull(root);

            Assert.IsNotNull(root["head"]);
            Assert.IsNotNull(root["head"]["title"]);
            Assert.AreEqual("Test", root["head"]["title"].InnerText);

            Assert.IsNotNull(root["body"]);
            Assert.AreEqual("Hello World!", root["body"].InnerText);
        }

        [TestMethod]
        [TestProperty("Lib", "LillTek.Advanced")]
        public void HtmlParser_IgnoreText()
        {
            HtmlParser parser;

            parser = new HtmlParser("<a>text1<b>text2<c>text3");
            parser.IgnoreText = true;

            Match(parser.Read(), HtmlItemType.OpenTag, "a");
            Match(parser.Read(), HtmlItemType.OpenTag, "b");
            Match(parser.Read(), HtmlItemType.OpenTag, "c");
            Match(parser.Read(), HtmlItemType.CloseTag, "c");
            Match(parser.Read(), HtmlItemType.CloseTag, "b");
            Match(parser.Read(), HtmlItemType.CloseTag, "a");
            Assert.IsNull(parser.Read());
        }

        [TestMethod]
        [TestProperty("Lib", "LillTek.Advanced")]
        public void HtmlParser_IgnoreComments()
        {
            HtmlParser parser;

            parser = new HtmlParser("<a><!-- comment1 --><b><!-- comment2 --><c><!-- comment3 -->");
            parser.IgnoreComments = true;

            Match(parser.Read(), HtmlItemType.OpenTag, "a");
            Match(parser.Read(), HtmlItemType.OpenTag, "b");
            Match(parser.Read(), HtmlItemType.OpenTag, "c");
            Match(parser.Read(), HtmlItemType.CloseTag, "c");
            Match(parser.Read(), HtmlItemType.CloseTag, "b");
            Match(parser.Read(), HtmlItemType.CloseTag, "a");
            Assert.IsNull(parser.Read());
        }

        [TestMethod]
        [TestProperty("Lib", "LillTek.Advanced")]
        public void HtmlParser_TagFilter()
        {
            HtmlParser parser;

            parser = new HtmlParser("<a><b><c><d></d></c></b></a");
            parser.AddTagFilter("a");
            parser.AddTagFilter("b");
            parser.AddTagFilter("c");

            Match(parser.Read(), HtmlItemType.OpenTag, "a");
            Match(parser.Read(), HtmlItemType.OpenTag, "b");
            Match(parser.Read(), HtmlItemType.OpenTag, "c");
            Match(parser.Read(), HtmlItemType.CloseTag, "c");
            Match(parser.Read(), HtmlItemType.CloseTag, "b");
            Match(parser.Read(), HtmlItemType.CloseTag, "a");
            Assert.IsNull(parser.Read());

            parser = new HtmlParser("<a><b><c><d></d></c></b></a");
            parser.AddTagFilter("b");

            Match(parser.Read(), HtmlItemType.OpenTag, "b");
            Match(parser.Read(), HtmlItemType.CloseTag, "b");
            Assert.IsNull(parser.Read());

            parser = new HtmlParser("<a p=1><b p1='1' p2=\"p2\" p3 p4=><c><d></d></c></b></a");
            parser.AddTagFilter("a");

            Match(parser.Read(), HtmlItemType.OpenTag, "a");
            Match(parser.Read(), HtmlItemType.CloseTag, "a");
            Assert.IsNull(parser.Read());
        }

        [TestMethod]
        [TestProperty("Lib", "LillTek.Advanced")]
        public void HtmlParser_Doctype()
        {
            HtmlParser parser;

            parser = new HtmlParser(@"<!DOCTYPE HTML PUBLIC ""-//W3C//DTD HTML 4.0 Transitional//EN"" >");
            Match(parser.Read(), HtmlItemType.OpenTag, "doctype");
            Match(parser.Read(), HtmlItemType.Text, @"HTML PUBLIC ""-//W3C//DTD HTML 4.0 Transitional//EN"" ");
            Match(parser.Read(), HtmlItemType.CloseTag, "doctype");
            Assert.IsNull(parser.Read());

            // Verify that an unclosed DOCTYPE doesn't blow up.

            parser = new HtmlParser(@"<!DOCTYPE HTML PUBLIC ""-//W3C//DTD HTML 4.0 Transitional//EN"" ");
            Match(parser.Read(), HtmlItemType.OpenTag, "doctype");
            Match(parser.Read(), HtmlItemType.Text, @"HTML PUBLIC ""-//W3C//DTD HTML 4.0 Transitional//EN"" ");
            Match(parser.Read(), HtmlItemType.CloseTag, "doctype");
            Assert.IsNull(parser.Read());
        }

        [TestMethod]
        [TestProperty("Lib", "LillTek.Advanced")]
        public void HtmlParser_Script()
        {
            HtmlParser parser;

            parser = new HtmlParser("<script/>");
            Match(parser.Read(), HtmlItemType.OpenTag, "script");
            Match(parser.Read(), HtmlItemType.CloseTag, "script");
            Assert.IsNull(parser.Read());

            parser = new HtmlParser("<script>document.Write('<html>' + '<' + '/html>');</script>");
            Match(parser.Read(), HtmlItemType.OpenTag, "script");
            Match(parser.Read(), HtmlItemType.Text, "document.Write('<html>' + '<' + '/html>');");
            Match(parser.Read(), HtmlItemType.CloseTag, "script");
            Assert.IsNull(parser.Read());

            parser = new HtmlParser("<script>document.Write('<html>' + '<' + '/html>');");
            Match(parser.Read(), HtmlItemType.OpenTag, "script");
            Match(parser.Read(), HtmlItemType.Text, "document.Write('<html>' + '<' + '/html>');");
            Match(parser.Read(), HtmlItemType.CloseTag, "script");
            Assert.IsNull(parser.Read());

            parser = new HtmlParser("<script>");
            Match(parser.Read(), HtmlItemType.OpenTag, "script");
            Match(parser.Read(), HtmlItemType.Text, string.Empty);
            Match(parser.Read(), HtmlItemType.CloseTag, "script");
            Assert.IsNull(parser.Read());

            parser = new HtmlParser("<script>Test");
            Match(parser.Read(), HtmlItemType.OpenTag, "script");
            Match(parser.Read(), HtmlItemType.Text, "Test");
            Match(parser.Read(), HtmlItemType.CloseTag, "script");
            Assert.IsNull(parser.Read());

            parser = new HtmlParser("<script>Test<");
            Match(parser.Read(), HtmlItemType.OpenTag, "script");
            Match(parser.Read(), HtmlItemType.Text, "Test<");
            Match(parser.Read(), HtmlItemType.CloseTag, "script");
            Assert.IsNull(parser.Read());

            parser = new HtmlParser("<script>Test<");
            Match(parser.Read(), HtmlItemType.OpenTag, "script");
            Match(parser.Read(), HtmlItemType.Text, "Test<");
            Match(parser.Read(), HtmlItemType.CloseTag, "script");
            Assert.IsNull(parser.Read());

            parser = new HtmlParser("<script>document.Write('<html>' + '<' + '/html>');");
            parser.AddTagFilter("a");
            Assert.IsNull(parser.Read());

            parser = new HtmlParser("<script>Test<");
            parser.IgnoreText = true;
            Match(parser.Read(), HtmlItemType.OpenTag, "script");
            Match(parser.Read(), HtmlItemType.CloseTag, "script");
            Assert.IsNull(parser.Read());
        }

        [TestMethod]
        [TestProperty("Lib", "LillTek.Advanced")]
        public void HtmlParser_Style()
        {
            HtmlParser parser;

            parser = new HtmlParser("<style/>");
            Match(parser.Read(), HtmlItemType.OpenTag, "style");
            Match(parser.Read(), HtmlItemType.CloseTag, "style");
            Assert.IsNull(parser.Read());

            parser = new HtmlParser("<style>document.Write('<html>' + '<' + '/html>');</style>");
            Match(parser.Read(), HtmlItemType.OpenTag, "style");
            Match(parser.Read(), HtmlItemType.Text, "document.Write('<html>' + '<' + '/html>');");
            Match(parser.Read(), HtmlItemType.CloseTag, "style");
            Assert.IsNull(parser.Read());

            parser = new HtmlParser("<style>document.Write('<html>' + '<' + '/html>');");
            Match(parser.Read(), HtmlItemType.OpenTag, "style");
            Match(parser.Read(), HtmlItemType.Text, "document.Write('<html>' + '<' + '/html>');");
            Match(parser.Read(), HtmlItemType.CloseTag, "style");
            Assert.IsNull(parser.Read());

            parser = new HtmlParser("<style>");
            Match(parser.Read(), HtmlItemType.OpenTag, "style");
            Match(parser.Read(), HtmlItemType.Text, string.Empty);
            Match(parser.Read(), HtmlItemType.CloseTag, "style");
            Assert.IsNull(parser.Read());

            parser = new HtmlParser("<style>Test");
            Match(parser.Read(), HtmlItemType.OpenTag, "style");
            Match(parser.Read(), HtmlItemType.Text, "Test");
            Match(parser.Read(), HtmlItemType.CloseTag, "style");
            Assert.IsNull(parser.Read());

            parser = new HtmlParser("<style>Test<");
            Match(parser.Read(), HtmlItemType.OpenTag, "style");
            Match(parser.Read(), HtmlItemType.Text, "Test<");
            Match(parser.Read(), HtmlItemType.CloseTag, "style");
            Assert.IsNull(parser.Read());

            parser = new HtmlParser("<style>Test<");
            Match(parser.Read(), HtmlItemType.OpenTag, "style");
            Match(parser.Read(), HtmlItemType.Text, "Test<");
            Match(parser.Read(), HtmlItemType.CloseTag, "style");
            Assert.IsNull(parser.Read());

            parser = new HtmlParser("<style>document.Write('<html>' + '<' + '/html>');");
            parser.AddTagFilter("a");
            Assert.IsNull(parser.Read());

            parser = new HtmlParser("<style>Test<");
            parser.IgnoreText = true;
            Match(parser.Read(), HtmlItemType.OpenTag, "style");
            Match(parser.Read(), HtmlItemType.CloseTag, "style");
            Assert.IsNull(parser.Read());
        }

        [TestMethod]
        [TestProperty("Lib", "LillTek.Advanced")]
        public void HtmlParser_Parse_Microsoft()
        {
            // Generate a DOM source scraped from www.microsoft.com and 
            // make sure we don't see an exception.

            const string source =
@"
<!DOCTYPE HTML PUBLIC ""-//W3C//DTD HTML 4.0 Transitional//EN"" >

<html dir=""ltr"" lang=""en"">
<head>
<META http-equiv=""Content-Type"" content=""text/html; charset=utf-8"" >
<!--TOOLBAR_EXEMPT-->
<meta http-equiv=""PICS-Label"" content=""(PICS-1.1 &quot;http://www.rsac.org/ratingsv01.html&quot; l gen true r (n 0 s 0 v 0 l 0))"" >
<meta name=""KEYWORDS"" content=""products; headlines; downloads; news; Web site; what's new; solutions; services; software; contests; corporate news;"" >
<meta name=""DESCRIPTION"" content=""The entry page to Microsoft's Web site. Find software, solutions, answers, support, and Microsoft news."" >
<meta name=""MS.LOCALE"" content=""EN-US"" >
<meta name=""CATEGORY"" content=""home page"" >
<title>Microsoft Corporation</title>
<base href=""http://g.msn.com/mh_mshp/98765"" >
<style type=""text/css"" media=""all"">
@import ""http://i.microsoft.com/h/all/s/hp_ltr.css"";
</style>
<script type='text/javascript'>var msvi_qllc = 'en-us';var msvi_qldir = 'LTR';var msvi_qlhost = 'http://www.microsoft.com';var tdel = 1000;var tlids = new Array();tlids[1] = 'TL071SUS2108;TL071SUS2113;TL06Q4S00020;TL071SUS2110;TL071MXX0101';tlids[2] = 'TU071SUS2113;TU071MUS1601;TU071MUS1001;TU071MUS1501;TU071SUS2121';tlids[3] = 'TB071MXX0701;TB071MUS1201;TB071MUS1401;TB071MUS1701;TB071SUS2122';tlids[4] = 'TI071MUS0201;TI071MXX0601;TI071SUS2123;TI071SUS2124;TI071SUS2125';tlids[5] = 'TD071MXX0501;TD071SUS2126;TD071SUS2127;TD071SUS2128;TD071SUS2129';</script>
<script type=""text/javascript"" src=""http://i2.microsoft.com/h/all/s/13.5_rtw/hp.js""></script>
<meta name='WT.sp' content='_us_'>
<meta name='DCSext.wt_target' content='IE 6;Windows XP SP2;Windows XP;Generic'>
<script type=""text/javascript"">wrT()</script><meta name='DCSext.wt_linkid' content='FT071MXX0401;FT071MUS1401;FT071MXX0301;NHenusenus;TH071SUS2110;TH071MXX0701;TH071MXX0601;TH071MXX0501;TH071MUS1101;TL071SUS2108;TL071SUS2113;TL06Q4S00020;TL071SUS2110;TL071MXX0101;TU071SUS2113;TU071MUS1601;TU071MUS1001;TU071MUS1501;TU071SUS2121;TB071MXX0701;TB071MUS1201;TB071MUS1401;TB071MUS1701;TB071SUS2122;TI071MUS0201;TI071MXX0601;TI071SUS2123;TI071SUS2124;TI071SUS2125;TD071MXX0501;TD071SUS2126;TD071SUS2127;TD071SUS2128;TD071SUS2129'><!-- --><script type=""text/javascript"">document.write(""<meta name='DCSext.wt_linkid' content='FT071MXX0401;FT071MUS1401;FT071MXX0301;NHenusenus;TH071SUS2110;TH071MXX0701;TH071MXX0601;TH071MXX0501;TH071MUS1101'>"")</script>
<script type=""text/javascript"" src=""http://i.microsoft.com/h/en-us/r/SiteRecruit_PageConfiguration_HomePage_Page.js""></script>
</head>
<body>
<script type=""text/javascript"">
<!--
var isW;isW=(document&&document.body.clientWidth&&document.body.clientWidth>=895&&document.getElementById);
//-->
</script>
<a href=""http://www.microsoft.com/default.aspx#cArea"" class=""hide"">Click here to jump to main page content</a>
<div id=""dPage"" class=""page"">
<table cellpadding=""0"" width=""100%""><tr><td colspan=""2"">
<table cellpadding=""0"" width=""100%"" style=""height: 22px""><tr>
<td width=""50%"" style=""filter:progid:DXImageTransform.Microsoft.Gradient(startColorStr='#4B92D9', endColorStr='#CEDFF6', gradientType='1')""></td>
<td width=""50%"" style=""filter:progid:DXImageTransform.Microsoft.Gradient(startColorStr='#CEDFF6', endColorStr='#1E77D3', gradientType='1')""></td>
</tr></table></td>
<td id=""msviGlobalToolbar"" height=""22"" nowrap align=""left"">
<table cellpadding=""0""><tr>
<td class=""gt0"" nowrap onmouseover=""this.className='gt1'"" onmouseout=""this.className='gt0'""  id='panelTd'><a href=""?DD6A043D&amp;http://go.microsoft.com/?linkid=4412889&amp;&amp;HL=Quick+Links&amp;CM=Masthead&amp;CE=h"">Quick Links</a><script type='text/javascript'>document.write('<img src=""http://i2.microsoft.com/library/mnp/2/gif/ql.gif"" width=""11px"" height=""4px"" alt=""""/>');</script><div id='panelDiv' style='position:absolute;visibility:hidden;z-index:100;'></div></td>
<td class=""gtsep"">|</td>
<td class=""gt0"" nowrap onmouseover=""this.className='gt1'"" onmouseout=""this.className='gt0'"" ><a href=""?894A82C6&amp;http://go.microsoft.com/?linkid=4412891&amp;&amp;HL=Worldwide&amp;CM=Masthead&amp;CE=h"">Worldwide</a></td>
</tr>
</table></td></tr></table>
<table cellpadding=""0"" width=""100%"" bgcolor=""#FFFFFF""><tr valign=""top"">
<td><script type=""text/javascript"">rT()</script><img alt=""Microsoft"" border=""0"" src=""http://i.microsoft.com/h/all/i/ms_masthead_8x6a_ltr.jpg""><!-- --><script type=""text/javascript"">wI(""http://i2.microsoft.com/h/all/i/ms_masthead_10x7a_ltr.jpg"",""Microsoft"","""","""")</script></td>
<td id=""msviDualGlobalSearch"" bgcolor=""#FFFFFF"" nowrap><form id=""msviDualSearchForm"" action=""http://www.microsoft.com/h/all/s/13.5_rtw/sr.aspx""><input type=""hidden"" name=""Track"" value=""true""><input type=""hidden"" name=""locale"" value=""en-us""><div><input name=""qu"" id=""msviDualSearchBox"" maxlength=""255""><input id=""msviSearchButton"" type=""submit"" name=""msviGoButton"" value=""Search""></div><div id=""msviRadioButtons""><input type=""radio"" id=""mscomSearch"" checked name=""searchTarget"" value=""microsoft""><label for=""mscomSearch"">Microsoft.com</label><input type=""radio"" id=""msnSearch"" name=""searchTarget"" value=""msn""><label for=""msnSearch""><img onClick=""msnSearch.checked=true"" src=""http://i.microsoft.com/h/en-us/i/msnlogo.gif"" alt=""MSN""> Web Search</label></div></form></td>
</tr></table>
<table id=""tMain"" class=""main"" cellpadding=""0"" width=""100%""><tr valign=""top"">
<td id=""NavTd"" >
<div id=""Nav""><h4>Product Families</h4><ul><li><a href=""?D75BC992&amp;http://www.microsoft.com/windows/default.mspx&amp;&amp;HL=Windows&amp;CM=Navigation&amp;CE=productFamilies"">Windows</a></li><li><a href=""?8D839E6D&amp;http://office.microsoft.com/home/default.aspx&amp;&amp;HL=Office&amp;CM=Navigation&amp;CE=productFamilies"">Office</a></li><li><a href=""?97F6BB94&amp;http://www.microsoft.com/windowsserversystem/default.mspx&amp;&amp;HL=Servers+&amp;CM=Navigation&amp;CE=productFamilies"">Servers</a></li><li><a href=""?D4D75315&amp;http://msdn.microsoft.com/&amp;&amp;HL=Developer+Tools&amp;CM=Navigation&amp;CE=productFamilies"">Developer Tools</a></li><li><a href=""?1EC9ED1D&amp;http://www.microsoft.com/businesssolutions/default.mspx&amp;&amp;HL=Business+Solutions&amp;CM=Navigation&amp;CE=productFamilies"">Business Solutions</a></li><li><a href=""?59FDCC6B&amp;http://www.microsoft.com/games/default.aspx&amp;&amp;HL=Games+%26+Xbox&amp;CM=Navigation&amp;CE=productFamilies"">Games &amp; Xbox</a></li><li><a href=""?05D6D5E1&amp;http://www.msn.com&amp;&amp;HL=MSN&amp;CM=Navigation&amp;CE=productFamilies"">MSN</a></li><li><a href=""?F0D77026&amp;http://www.microsoft.com/windowsmobile/default.mspx&amp;&amp;HL=Windows+Mobile&amp;CM=Navigation&amp;CE=productFamilies"">Windows Mobile</a></li><li><a href=""?27C350E5&amp;http://go.microsoft.com/?LinkID=319190&amp;&amp;HL=All+Products&amp;CM=Navigation&amp;CE=productFamilies"">All Products</a></li></ul><div class=""line""></div><h4>Resources</h4><ul><li><a href=""?09769308&amp;http://www.microsoft.com/downloads/search.aspx&amp;&amp;HL=Downloads&amp;CM=Navigation&amp;CE=Resources"">Downloads</a></li><li><a href=""?DF04CF8E&amp;http://update.microsoft.com/microsoftupdate/&amp;&amp;HL=Microsoft+Update&amp;CM=Navigation&amp;CE=Resources"">Microsoft Update</a></li><li><a href=""?1EAC08ED&amp;http://office.microsoft.com/OfficeUpdate/default.aspx&amp;&amp;HL=Office+Update&amp;CM=Navigation&amp;CE=Resources"">Office Update</a></li><li><a href=""?10209F9D&amp;http://www.microsoft.com/security/default.mspx&amp;&amp;HL=Security&amp;CM=Navigation&amp;CE=Resources"">Security</a></li><li><a href=""?E4B627DF&amp;http://support.microsoft.com/&amp;&amp;HL=Support&amp;CM=Navigation&amp;CE=Resources"">Support</a></li><li><a href=""?72109C19&amp;http://support.microsoft.com/search/?adv=0&amp;&amp;HL=Knowledge+Base&amp;CM=Navigation&amp;CE=Resources"">Knowledge Base</a></li><li><a href=""?37717B4A&amp;http://partner.microsoft.com/&amp;&amp;HL=For+Partners&amp;CM=Navigation&amp;CE=Resources"">For Partners</a></li><li><a href=""?BF0477D5&amp;http://www.microsoft.com/learning/default.asp&amp;&amp;HL=Learning+Tools&amp;CM=Navigation&amp;CE=Resources"">Learning Tools</a></li><li><a href=""?5C2C7203&amp;http://www.microsoft.com/events/default.mspx&amp;&amp;HL=Events+%26+Webcasts&amp;CM=Navigation&amp;CE=Resources"">Events &amp; Webcasts</a></li></ul><div class=""line""></div><h4>Microsoft Worldwide</h4><ul><li><a href=""?E1E07A8C&amp;http://www.microsoft.com/worldwide&amp;&amp;HL=Microsoft%2bWorldwide&amp;CM=Navigation&amp;CE=geotargeting"">Countries &amp; Regions</a></li></ul><div class=""line""></div></div>
<div class=""ad""><script type=""text/javascript"">wrT()</script><a href=""?17028B08&amp;http://www.microsoft.com/athome/security/protect/default.aspx&amp;&amp;HL=Protect+your+PC+in+3+steps&amp;CM=Ad&amp;CE=Ad""><img src=""http://i2.microsoft.com/h/en-us/i/Promo_PYPC4.gif"" alt=""Protect your PC in 3 steps""></a><!-- --><script type=""text/javascript"">var AdHtml='<iframe frameborder=""0"" scrolling=""no"" marginheight=""0px"" marginwidth=""0px"" allowtransparency=""true"" style=""background:#F1F1F1"" width=""120"" height=""240"" src=""http://rad.microsoft.com/ADSAdClient31.dll?GetAd=&PG=CMSIE4&SC=F3&AP=1164""><'+'/iframe>';document.writeln(AdHtml);</script></div>
</td><td id=""ca"">
<a name=""cArea""></a>
<div id=""fa"">
<table cellpadding='0'><tr><td id='hp' rowspan='2'><script type=""text/javascript"">rT()</script><img src=""http://i3.microsoft.com/h/en-us/i/HP_13.5/SQLBigData_2_8.jpg"" alt=""Wide"" class=""hpi"" usemap=""#Content_HP""><!-- --><script type=""text/javascript"">
<!--
wI(""http://i3.microsoft.com/h/en-us/i/HP_13.5/SQLBigData_2_10.jpg"",""Wide"",""hpiW"",""#Content_HPW"")
//-->
</script></td><td id='qpt'><script type=""text/javascript"">rT()</script><img src=""http://i3.microsoft.com/h/en-us/i/HP_13.5/Dynamics_4_8.jpg"" alt=""Quarter Top"" class=""qpi"" usemap=""#Content_QPT""><!-- --><script type=""text/javascript"">
<!--
wI(""http://i3.microsoft.com/h/en-us/i/HP_13.5/Dynamics_4_10.jpg"",""Quarter Top"",""qpiW"",""#Content_QPTW"")
//-->
</script></td></tr><tr><td id='qpb'><script type=""text/javascript"">rT()</script><img src=""http://i3.microsoft.com/h/en-us/i/HP_13.5/ITSave_4_8.jpg"" alt=""Quarter Bottom"" class=""qpi"" usemap=""#Content_QPB""><!-- --><script type=""text/javascript"">
<!--
wI(""http://i3.microsoft.com/h/en-us/i/HP_13.5/ITSave_4_10.jpg"",""Quarter Bottom"",""qpiW"",""#Content_QPBW"")
//-->
</script></td></tr></table></div><div id=""nh"" dir=""ltr""><span>NEWS:</span> <a href=""?E2AF1D29&amp;http://www.microsoft.com/athome/security/update/bulletins/200609.mspx&amp;&amp;HL=Download+the+latest+security+update+for+Windows+now&amp;CM=News&amp;CE=en-us&amp;wt_linkid=NHenusenus"">Download the latest security update for Windows now</a></div>

<table id='tbt' cellspacing='0' cellpadding='0'>
<tr id='tabtop'>
	<td class=""tbh"">&nbsp;</td>
	<td id=""tbc"" class=""tbc"">&nbsp;</td>
	<td id=""tbp"" class=""tbp"">&nbsp;</td>
</tr>
<tr><td id='tbh_0' class='Title trb' onmouseover='FT(0);CI(0)'><div><span tabindex='0' onfocus='ctb=0;' onkeydown='return KP(event);'>Highlights</span></div></td><td id='tbc_0' class='Content'><div class='Marquee'><table border=""0"" cellpadding=""0"" cellspacing=""0""><tr><td><a id=""mrq_0"" href=""?981C6B3A&amp;http://www.microsoft.com/hardware/digitalcommunication/default.mspx&amp;&amp;HL=Introducing+Microsoft+LifeCams&amp;CM=Tabs&amp;CE=Highlights&amp;wt_linkid=TH071SUS2110""><img src=""http://i.microsoft.com/h/en-us/i/HP_13.5/LiveMessenger_S.jpg"" alt=""Introducing Microsoft LifeCams""><span>Introducing Microsoft LifeCams</span>Optimized for Windows Live Messenger</a></td></tr></table></div><ul><li><a href=""?590AA639&amp;http://clk.atdmt.com/MRT/go/mcrssaub0020000057mrt/direct/01/&amp;&amp;HL=Are+your+people+ready%3f+Give+them+the+tools+to+succeed&amp;CM=Tabs&amp;CE=Highlights&amp;wt_linkid=TH071MXX0701""><span>Are your people ready?</span> Give them the tools to succeed</a></li><li><a href=""?522CF238&amp;http://www.microsoft.com/windows/desktopsearch/default.mspx&amp;&amp;HL=Download+Windows+Desktop+Search%3a+Retrieve+over+200+file+types+on+a+PC+or+across+a+network&amp;CM=Tabs&amp;CE=Highlights&amp;wt_linkid=TH071MXX0601""><span>Download Windows Desktop Search:</span> Retrieve over 200 file types on a PC or across a network</a></li><li><a href=""?BE90E265&amp;http://msdn.microsoft.com/vstudio/products/trial/&amp;&amp;HL=Download+or+order+a+trial+of+Visual+Studio+2005&amp;CM=Tabs&amp;CE=Highlights&amp;wt_linkid=TH071MXX0501"">Download or order a <span>trial of Visual Studio 2005</span></a></li><li><a href=""?6D841857&amp;http://ad.doubleclick.net/clk;41577284;13780079;q?http://www.officelive.com?XID=AUS0707010930000F0A08M2746080allmicrosft&amp;&amp;HL=Microsoft+Office+Live%3a+Sign+up+for+the+beta+and+get+a+free+Web+site%2c+e-mail+accounts%2c+and+more&amp;CM=Tabs&amp;CE=Highlights&amp;wt_linkid=TH071MUS1101""><span>Microsoft Office Live</span>: Sign up for the beta and get a free Web site, e-mail accounts, and more</a></li></ul></td><td id='tbpop' class='Popular' rowspan='7'><div class='popDest'><div class='heading'>Popular Searches</div><ul><li><a href=""?DB41A8E8&amp;http://search.microsoft.com/results.aspx?mkt=en-US&amp;setlang=en-US&amp;q=templates&amp;&amp;HL=Templates&amp;CM=popular&amp;CE=Searches"">Templates</a></li><li><a href=""?86BF3B1C&amp;http://search.microsoft.com/results.aspx?q=activesync&amp;l=1&amp;mkt=en-US&amp;FORM=QBME1&amp;&amp;HL=ActiveSync&amp;CM=popular&amp;CE=Searches"">ActiveSync</a></li><li><a href=""?3F99775B&amp;http://search.microsoft.com/results.aspx?q=clip+art&amp;l=1&amp;mkt=en-US&amp;FORM=QBME1&amp;&amp;HL=Clip+art&amp;CM=popular&amp;CE=Searches"">Clip art</a></li></ul></div><div class='popDest'><div class='heading'>Popular Downloads</div><ul><li><a href=""?327A892C&amp;http://www.microsoft.com/downloads/details.aspx?FamilyID=435bfce7-da2b-4a6a-afa4-f7f14e605a0d&amp;displaylang=en&amp;&amp;HL=Windows+Defender+Beta+2&amp;CM=popular&amp;CE=Downloads"">Windows Defender Beta 2</a></li><li><a href=""?5A098AEA&amp;http://www.microsoft.com/downloads/details.aspx?FamilyID=2da43d38-db71-4c1b-bc6a-9b6652cd92a3&amp;displaylang=en&amp;&amp;HL=DirectX+End-User+Runtime&amp;CM=popular&amp;CE=Downloads"">DirectX End-User Runtime</a></li><li><a href=""?FD820A40&amp;http://www.microsoft.com/downloads/search.aspx?displaylang=en&amp;&amp;HL=More+Popular+Downloads&amp;CM=popular&amp;CE=Downloads"">More Popular Downloads</a></li></ul></div><script type=""text/javascript""><!--
if(isW){document.write('<br clear=""all"">')}
--></script><div class='popDest'><div class='heading'>Popular Destinations</div><ul><li><a href=""?DDF20A10&amp;http://www.microsoft.com/athome/default.mspx&amp;&amp;HL=At+Home&amp;CM=popular&amp;CE=Dest"">At Home</a></li><li><a href=""?3D1D00E2&amp;http://www.microsoft.com/atwork/default.mspx&amp;&amp;HL=At+Work&amp;CM=popular&amp;CE=Dest"">At Work</a></li><li><a href=""?EE7CF3F5&amp;http://www.microsoft.com/business/default.mspx&amp;&amp;HL=Business+%26+Industry&amp;CM=popular&amp;CE=Dest"">Business &amp; Industry</a></li><li><a href=""?323AA19B&amp;http://msdn.microsoft.com/&amp;&amp;HL=MSDN&amp;CM=popular&amp;CE=Dest"">MSDN</a></li><li><a href=""?1C9AFB8B&amp;http://technet.microsoft.com/default.aspx&amp;&amp;HL=TechNet+for+IT+Pros&amp;CM=popular&amp;CE=Dest"">TechNet for IT Pros</a></li></ul></div></td></tr><tr><td id='tbh_1' class='Title trb' onmouseover='FT(1);CI(1)'><div><span tabindex='0' onfocus='ctb=1;' onkeydown='return KP(event);'>Latest releases</span></div></td><td id='tbc_1' class='Content'><div class='Marquee'><table border=""0"" cellpadding=""0"" cellspacing=""0""><tr><td><a id=""mrq_1"" href=""?B5B32149&amp;http://get.live.com/messenger/overview&amp;&amp;HL=Download+Windows+Live+Messenger&amp;CM=Tabs&amp;CE=Releases&amp;wt_linkid=TL071SUS2108""><img src=""http://i2.microsoft.com/h/en-us/i/HP_13.5/MSNmsgr_S.gif"" alt=""Download Windows Live Messenger""><span>Download Windows Live Messenger</span>The next generation of MSN Messenger helps you connect and share with voice, video, and more</a></td></tr></table></div><ul><li><a href=""?46018D46&amp;http://www.microsoft.com/student/default.mspx&amp;&amp;HL=Microsoft+Student%3a+Tools+and+information+to+help+you+succeed+academically&amp;CM=Tabs&amp;CE=Releases&amp;wt_linkid=TL071SUS2113""><span>Microsoft Student:</span> Tools and information to help you succeed academically</a></li><li><a href=""?FC23D9D1&amp;http://www.microsoft.com/dynamics/ax/default.mspx&amp;&amp;HL=Microsoft+Dynamics+AX+4.0%3a+New+version+of+Microsoft+Axapta+scales+better%2c+connects+better&amp;CM=Tabs&amp;CE=Releases&amp;wt_linkid=TL06Q4S00020""><span>Microsoft Dynamics AX 4.0:</span> New version of Microsoft Axapta scales better, connects better</a></li><li><a href=""?B409E2D7&amp;http://www.microsoft.com/hardware/digitalcommunication/default.mspx&amp;&amp;HL=Introducing+Microsoft+LifeCams%3a+Optimized+for+Windows+Live+Messenger&amp;CM=Tabs&amp;CE=Releases&amp;wt_linkid=TL071SUS2110""><span>Introducing Microsoft LifeCams:</span> Optimized for Windows Live Messenger</a></li><li><a href=""?E7341003&amp;http://clk.atdmt.com/MRT/go/mcrssitp0070000030mrt/direct/01/&amp;&amp;HL=Download+Exchange+Server+2007+Beta+2+today&amp;CM=Tabs&amp;CE=Releases&amp;wt_linkid=TL071MXX0101"">Download <span>Exchange Server 2007</span> Beta 2 today</a></li></ul></td></tr><tr><td id='tbh_2' class='Title trb' onmouseover='FT(2);CI(2)'><div><span tabindex='0' onfocus='ctb=2;' onkeydown='return KP(event);'>Using your computer</span></div></td><td id='tbc_2' class='Content'><div class='Marquee'><table border=""0"" cellpadding=""0"" cellspacing=""0""><tr><td><a id=""mrq_2"" href=""?C586197F&amp;http://www.microsoft.com/resources/discover/default.mspx&amp;&amp;HL=Family+favorites&amp;CM=Tabs&amp;CE=usingComp&amp;wt_linkid=TU071SUS2113""><img src=""http://i.microsoft.com/h/en-us/i/HP_13.5/FamFaves_S.jpg"" alt=""Family favorites""><span>Family favorites</span>Products to help with photos, finance, homework, and more</a></td></tr></table></div><ul><li><a href=""?6ABBB5F2&amp;http://www.windowsonecare.com/purchase/trial.aspx?sc_cid=mscom_hp&amp;&amp;HL=Windows+Live+OneCare%3a+Download+the+free+90-day+trial+today&amp;CM=Tabs&amp;CE=usingComp&amp;wt_linkid=TU071MUS1601"">Windows Live OneCare: <span>Download the free 90-day trial</span> today</a></li><li><a href=""?05689872&amp;http://clk.atdmt.com/MRT/go/mcrssinf0010000027mrt/direct/01/&amp;&amp;HL=Save+time%3a+Tips+%26+tricks+for+Microsoft+Office&amp;CM=Tabs&amp;CE=usingComp&amp;wt_linkid=TU071MUS1001""><span>Save time:</span> Tips &amp; tricks for Microsoft Office</a></li><li><a href=""?D64F6F87&amp;http://clk.atdmt.com/GBL/go/mcrsshlc0020000053gbl/direct/01/&amp;&amp;HL=Get+maps+and+directions+in+Outlook%3a+Download+the+Windows+Live+Local+add-in&amp;CM=Tabs&amp;CE=usingComp&amp;wt_linkid=TU071MUS1501""><span>Get maps and directions in Outlook:</span> Download the Windows Live Local add-in</a></li><li><a href=""?24939B2A&amp;http://office.microsoft.com/en-us/assistance/HP052001271033.aspx&amp;&amp;HL=Common+Excel+formulas%3a+Quickly+add+formulas+to+your+Excel+worksheets+&amp;CM=Tabs&amp;CE=usingComp&amp;wt_linkid=TU071SUS2121""><span>Common Excel formulas:</span> Quickly add formulas to your Excel worksheets</a></li></ul></td></tr><tr><td id='tbh_3' class='Title trb' onmouseover='FT(3);CI(3)'><div><span tabindex='0' onfocus='ctb=3;' onkeydown='return KP(event);'>For Business</span></div></td><td id='tbc_3' class='Content'><div class='Marquee'><table border=""0"" cellpadding=""0"" cellspacing=""0""><tr><td><a id=""mrq_3"" href=""?3A76A847&amp;http://clk.atdmt.com/MRT/go/mcrssaub0020000057mrt/direct/01/&amp;&amp;HL=Are+your+people+ready%3f&amp;CM=Tabs&amp;CE=Business&amp;wt_linkid=TB071MXX0701""><img src=""http://i2.microsoft.com/h/en-us/i/HP_13.5/PeopleReady_S.jpg"" alt=""Are your people ready?""><span>Are your people ready?</span>Learn how to give them the tools to succeed</a></td></tr></table></div><ul><li><a href=""?44D67685&amp;http://www.microsoft.com/uc/livemeeting/default.mspx&amp;&amp;HL=Meet%2c+share%2c+work+online%3a+Try+Office+Live+Meeting+free+for+14+days&amp;CM=Tabs&amp;CE=Business&amp;wt_linkid=TB071MUS1201""><span>Meet, share, work online:</span> Try Office Live Meeting free for 14 days</a></li><li><a href=""?DBF78187&amp;http://www.microsoft.com/microsoftdynamics&amp;&amp;HL=Microsoft+Dynamics%3a+Easy-to-use+business+management+solutions&amp;CM=Tabs&amp;CE=Business&amp;wt_linkid=TB071MUS1401"">Microsoft Dynamics: Easy-to-use <span>business management solutions</span></a></li><li><a href=""?344E570C&amp;http://clk.atdmt.com/MRT/go/mcrssaub0130000040mrt/direct/01/&amp;&amp;HL=Small+Business+%2b%3a+Free%2c+personalized+online+resource&amp;CM=Tabs&amp;CE=Business&amp;wt_linkid=TB071MUS1701""><span>Small Business +:</span> Free, personalized online resource</a></li><li><a href=""?923C170E&amp;http://www.microsoft.com/midsizebusiness/solutions/inventory_tracking.mspx&amp;&amp;HL=Track+inventory+instantly+and+reduce+your+operating+costs&amp;CM=Tabs&amp;CE=Business&amp;wt_linkid=TB071SUS2122""><span>Track inventory instantly</span> and reduce your operating costs</a></li></ul></td></tr><tr><td id='tbh_4' class='Title trb' onmouseover='FT(4);CI(4)'><div><span tabindex='0' onfocus='ctb=4;' onkeydown='return KP(event);'>For IT Professionals</span></div></td><td id='tbc_4' class='Content'><div class='Marquee'><table border=""0"" cellpadding=""0"" cellspacing=""0""><tr><td><a id=""mrq_4"" href=""?6688AB7F&amp;http://www.microsoft.com/securemessaging/default.mspx&amp;&amp;HL=Protect+your+e-mail+servers+with+multiple+layers+of+defense&amp;CM=Tabs&amp;CE=ITPros&amp;wt_linkid=TI071MUS0201""><img src=""http://i.microsoft.com/h/en-us/i/HP_13.5/SecurityCenter_S.jpg"" alt=""Protect your e-mail servers with multiple layers of defense""><span>Protect your e-mail servers with multiple layers of defense</span>Download the evaluation software today</a></td></tr></table></div><ul><li><a href=""?63AD41F8&amp;http://www.microsoft.com/windows/desktopsearch/default.mspx&amp;&amp;HL=Download+Windows+Desktop+Search%3a+Retrieve+over+200+file+types+on+a+PC+or+across+a+network&amp;CM=Tabs&amp;CE=ITPros&amp;wt_linkid=TI071MXX0601""><span>Download Windows Desktop Search:</span> Retrieve over 200 file types on a PC or across a network</a></li><li><a href=""?B73DDDD8&amp;http://www.microsoft.com/technet/desktopdeployment/default.mspx&amp;&amp;HL=Desktop+Deployment+Center%3a+Resources+to+deploy+Windows+and+Office+in+your+organization&amp;CM=Tabs&amp;CE=ITPros&amp;wt_linkid=TI071SUS2123"">Desktop Deployment Center: Resources to <span>deploy Windows and Office</span> in your organization</a></li><li><a href=""?18E4329A&amp;http://www.microsoft.com/technet/itsolutions/howto/default.mspx&amp;&amp;HL=How-to+Index%3a+Find+step-by-step%2c+task-oriented+technical+articles+&amp;CM=Tabs&amp;CE=ITPros&amp;wt_linkid=TI071SUS2124""><span>How-to Index:</span> Find step-by-step, task-oriented technical articles</a></li><li><a href=""?02C93DB4&amp;http://www.microsoft.com/technet/technetmag/issues/2006/09/HighStandards/default.aspx&amp;&amp;HL=Avoid+unplanned+service+outages%3a+Powerful+tools+for+configuration+management&amp;CM=Tabs&amp;CE=ITPros&amp;wt_linkid=TI071SUS2125""><span>Avoid unplanned service outages:</span> Powerful tools for configuration management</a></li></ul></td></tr><tr><td id='tbh_5' class='Title trb' onmouseover='FT(5);CI(5)'><div><span tabindex='0' onfocus='ctb=5;' onkeydown='return KP(event);'>For Developers</span></div></td><td id='tbc_5' class='Content'><div class='Marquee'><table border=""0"" cellpadding=""0"" cellspacing=""0""><tr><td><a id=""mrq_5"" href=""?2B6B1260&amp;http://msdn.microsoft.com/vstudio/products/trial/&amp;&amp;HL=Try+Visual+Studio+2005+for+free&amp;CM=Tabs&amp;CE=Dev&amp;wt_linkid=TD071MXX0501""><img src=""http://i2.microsoft.com/h/en-us/i/HP_13.5/VisualStudio_S.jpg"" alt=""Try Visual Studio 2005 for free""><span>Try Visual Studio 2005 for free</span>Choose from Express editions to trials to the online hosted experience</a></td></tr></table></div><ul><li><a href=""?F94302D3&amp;http://msdn.microsoft.com/directx/xna/&amp;&amp;HL=XNA+Game+Studio+Express%3a+Easily+create+games+for+both+Windows+and+Xbox+360&amp;CM=Tabs&amp;CE=Dev&amp;wt_linkid=TD071SUS2126"">XNA Game Studio Express: <span>Easily create games</span> for both Windows and Xbox 360</a></li><li><a href=""?0F7753DF&amp;http://msdn.microsoft.com/vstudio/downloads/powertoys/&amp;&amp;HL=Power+Toys+for+Visual+Studio%3a+Small+tools+that+address+developer+pain-points+and+help+diagnose+problems&amp;CM=Tabs&amp;CE=Dev&amp;wt_linkid=TD071SUS2127""><span>Power Toys for Visual Studio:</span> Small tools that address developer pain-points and help diagnose problems</a></li><li><a href=""?AEB8BFE0&amp;http://msdn.microsoft.com/msdnmag/issues/06/09/EarthlyDelights/default.aspx&amp;&amp;HL=Virtual+Earth+APIs%3a+Code+your+applications+to+deliver+the+world&amp;CM=Tabs&amp;CE=Dev&amp;wt_linkid=TD071SUS2128""><span>Virtual Earth APIs:</span> Code your applications to deliver the world</a></li><li><a href=""?E15C2B2E&amp;http://msdn.microsoft.com/msdnmag/issues/06/09/SmartClients/default.aspx&amp;&amp;HL=Smart+Clients%3a+New+guidance+and+tools+for+building+integrated+desktop+applications&amp;CM=Tabs&amp;CE=Dev&amp;wt_linkid=TD071SUS2129""><span>Smart Clients:</span> New guidance and tools for building integrated desktop applications</a></li></ul></td></tr>
<tr id='rwph'>
	<td id='tbph'>&nbsp;</td>
</tr>
</table>

</td></tr></table>
<table id=""msviFooter"" width=""100%"" cellpadding=""0"" cellspacing=""0""><tr valign=""bottom""><td id=""msviFooter2"">
<div id=""msviLocalFooter""><span><a href=""?D635CE61&amp;http://go.microsoft.com/?linkid=317027&amp;&amp;HL=Manage+Your+Profile&amp;CM=Footer&amp;CE=h"">Manage Your Profile</a> |</span><span><a href=""?0F6CC033&amp;http://go.microsoft.com/?linkid=2028351&amp;&amp;HL=Contact+Us&amp;CM=Footer&amp;CE=h"">Contact Us</a> |</span><span><a href=""?32AADE83&amp;http://www.microsoft.com/about/default.mspx&amp;&amp;HL=About+Microsoft&amp;CM=Footer&amp;CE=h"">About Microsoft</a> |</span><span><a href=""?7E09E87E&amp;http://www.microsoft.com/careers/default.mspx&amp;&amp;HL=Careers&amp;CM=Footer&amp;CE=h"">Careers</a> |</span><span><a href=""?60AE0447&amp;http://www.microsoft.com/about/legal/&amp;&amp;HL=Legal&amp;CM=Footer&amp;CE=h"">Legal</a> |</span><span><a href=""?A1D0D5DD&amp;http://www.microsoft.com/presspass/&amp;&amp;HL=For+Journalists&amp;CM=Footer&amp;CE=h"">For Journalists</a> |</span><span><a href='?C24BE48F&amp;http://www.microsoft.com/rss/Default.aspx&amp;&amp;HL=Subscribe+to+Microsoft+Web+Feeds&amp;CM=Footer&amp;CE=h'><img src='http://i.microsoft.com/h/all/i/webfeed.png' alt='Subscribe to Microsoft Web Feeds'>Subscribe to Microsoft Web Feeds</a></span></div>
<div id=""msviGlobalFooter"">&#169; 2006 Microsoft Corporation. All rights reserved. <span><a href=""?6AAD5E20&amp;http://go.microsoft.com/?linkid=4412892&amp;&amp;HL=Terms+of+Use&amp;CM=Footer&amp;CE=h"">Terms of Use</a> |</span><span><a href=""?13119908&amp;http://go.microsoft.com/?linkid=4412893&amp;&amp;HL=Trademarks&amp;CM=Footer&amp;CE=h"">Trademarks</a> |</span><span><a href=""?1D1701AC&amp;http://go.microsoft.com/?linkid=4412894&amp;&amp;HL=Privacy+Statement&amp;CM=Footer&amp;CE=h"">Privacy Statement</a></span></div>
</td></tr></table>
</div>
<map name=""Content_HP""><area shape=""poly"" alt=""Power your enterprise with SQL Server 2005"" coords=""0,0,361,0,361,236,335,236,335,220,210,220,210,253,335,253,335,236,361,236,361,278,0,278"" href=""?BE2B0E7E&amp;http://www.microsoft.com/sql/bigdata/default.mspx&amp;&amp;HL=Power+your+enterprise+with+SQL+Server+2005&amp;CM=Features&amp;CE=HP&amp;wt_linkid=FT071MXX0401""><area shape=""rect"" alt=""Power your enterprise with SQL Server 2005"" coords=""210,220,335,237"" href=""?BE2B0E7E&amp;http://www.microsoft.com/sql/bigdata/default.mspx&amp;&amp;HL=Power+your+enterprise+with+SQL+Server+2005&amp;CM=Features&amp;CE=HP&amp;wt_linkid=FT071MXX0401""><area shape=""rect"" alt=""Power your enterprise with SQL Server 2005"" coords=""210,236,335,253"" href=""?F1D42581&amp;http://www.microsoft.com/sql/downloads/trial-software.mspx&amp;&amp;HL=Power+your+enterprise+with+SQL+Server+2005&amp;CM=Features&amp;CE=HP&amp;wt_linkid=FT071MXX0401""></map><map name=""Content_HPW""><area shape=""poly"" alt=""Power your enterprise with SQL Server 2005"" coords=""0,0,498,0,498,326,460,326,460,305,290,305,290,348,460,348,460,326,498,326,498,384,0,384"" href=""?BE2B0E7E&amp;http://www.microsoft.com/sql/bigdata/default.mspx&amp;&amp;HL=Power+your+enterprise+with+SQL+Server+2005&amp;CM=Features&amp;CE=HP&amp;wt_linkid=FT071MXX0401""><area shape=""rect"" alt=""Power your enterprise with SQL Server 2005"" coords=""290,305,460,327"" href=""?BE2B0E7E&amp;http://www.microsoft.com/sql/bigdata/default.mspx&amp;&amp;HL=Power+your+enterprise+with+SQL+Server+2005&amp;CM=Features&amp;CE=HP&amp;wt_linkid=FT071MXX0401""><area shape=""rect"" alt=""Power your enterprise with SQL Server 2005"" coords=""290,326,460,348"" href=""?F1D42581&amp;http://www.microsoft.com/sql/downloads/trial-software.mspx&amp;&amp;HL=Power+your+enterprise+with+SQL+Server+2005&amp;CM=Features&amp;CE=HP&amp;wt_linkid=FT071MXX0401""></map><map name=""Content_QPT""><area shape=""rect"" alt=""Microsoft Dynamics: Easy-to-use management solutions"" coords=""0,0,228,138"" href=""?C79DE95C&amp;http://clk.atdmt.com/MRT/go/mcrssaub0040000015mrt/direct/01/&amp;&amp;HL=Microsoft+Dynamics%3a+Easy-to-use+management+solutions&amp;CM=Features&amp;CE=QP&amp;wt_linkid=FT071MUS1401""></map><map name=""Content_QPTW""><area shape=""rect"" alt=""Microsoft Dynamics: Easy-to-use management solutions"" coords=""0,0,315,191"" href=""?C79DE95C&amp;http://clk.atdmt.com/MRT/go/mcrssaub0040000015mrt/direct/01/&amp;&amp;HL=Microsoft+Dynamics%3a+Easy-to-use+management+solutions&amp;CM=Features&amp;CE=QP&amp;wt_linkid=FT071MUS1401""></map><map name=""Content_QPB""><area shape=""rect"" alt=""Save the day: Free security tools &amp; training for IT pros"" coords=""0,0,228,138"" href=""?F5B6AACC&amp;http://www.microsoft.com/technet/security/default.mspx&amp;&amp;HL=Save+the+day%3a+Free+security+tools+%26+training+for+IT+pros&amp;CM=Features&amp;CE=QP&amp;wt_linkid=FT071MXX0301""></map><map name=""Content_QPBW""><area shape=""rect"" alt=""Save the day: Free security tools &amp; training for IT pros"" coords=""0,0,315,191"" href=""?F5B6AACC&amp;http://www.microsoft.com/technet/security/default.mspx&amp;&amp;HL=Save+the+day%3a+Free+security+tools+%26+training+for+IT+pros&amp;CM=Features&amp;CE=QP&amp;wt_linkid=FT071MXX0301""></map>
<script type='text/javascript'>
<!--
if(isW){w(e(""tbc""));w(e(""tbp""));w(e(""tMain""));}
TI();
//--></script>
<script type=""text/javascript"">
<!--
if(isW){w(e(""al""));w(e(""dPage""));w(e(""re""));w(e(""tdRotSec""));w(e(""tdPopDest""));w(e(""dFL""));w(e(""dFS""));w(e(""dFS1""));w(e(""dFS2""));w(e(""nTable""));recW(e(""fa""));if(e(""dS3"")){e(""dS3"").style.clear=""none"";}if(e(""dS5"")){e(""dS5"").style.clear=""none"";}if(e(""dS4"")){e(""dS4"").style.clear=""left"";}rE(""br2"");rE(""br4"");if(e(""br3"")){e(""br3"").className="""";}}
//-->
</script><div style=""display:none"" id=""WebMetrixDiv"">
<script type=""text/javascript"">
<!--
var wmGif='<'+'img width=""0"" height=""0"" alt="""" src=""http://c.microsoft.com'+'/trans_pixel.asp?source=www&amp;TYPE=PV&amp;P=';if(''!=window.document.referrer){wmGif=wmGif+""&amp;r=""+escape(window.document.referrer);}wmGif=wmGif+'""/>';document.writeln(wmGif);
//-->
</script></div><div style=""display:none"" id=""IDSSDiv"">
<script type=""text/javascript"">
<!--
var msnGif='<'+'img width=""0"" height=""0"" alt="""" src=""http://c1.microsoft.com'+'/c.gif?DI=4050&amp;PS=81555&amp;PI=40472&amp;TP=http%3a%2f%2fwww.microsoft.com&amp;RF='+escape(window.document.referrer)+'"">';document.writeln(msnGif);
//-->
</script></div>
<SCRIPT TYPE=""text/javascript""><!--
var gDomain=""m.webtrends.com"";var gDcsId=""dcsjwb9vb00000c932fd0rjc7_5p3t"";var gTrackEvents=1;var gFpc=""WT_FPC"";if(document.cookie.indexOf(gFpc+""="")==-1){document.write(""<SCR""+""IPT TYPE='text/javascript' SRC='""+""http""+(window.location.protocol.indexOf('https:')==0?'s':'')+""://""+gDomain+""/""+gDcsId+""/wtid.js""+""'><\/SCR""+""IPT>"");}
//-->
</SCRIPT>
<SCRIPT SRC=""http://i.microsoft.com/h/all/s/13.5_rtw/webtrends.js"" TYPE=""text/javascript""></SCRIPT>
<NOSCRIPT><IMG ALT="""" BORDER=""0"" ID=""DCSIMG"" WIDTH=""1"" HEIGHT=""1"" SRC=""http://m.webtrends.com/dcsjwb9vb00000c932fd0rjc7_5p3t/njs.gif?dcsuri=/nojavascript&amp;WT.js=No""></NOSCRIPT></body></html>
";

            XmlDocument doc;

            doc = HtmlParser.Parse(source);
            Assert.IsNotNull(doc["root"]["html"]["head"]);
            Assert.IsNotNull(doc["root"]["html"]["body"]);
        }

        [TestMethod]
        [TestProperty("Lib", "LillTek.Advanced")]
        public void HtmlParser_Parse_Google()
        {
            // Generate a DOM source scraped from www.google.com and 
            // make sure we don't see an exception.

            const string source =
@"
<html><head><meta http-equiv=""content-type"" content=""text/html; charset=UTF-8""><title>Google</title><style><!--
body,td,a,p,.h{font-family:arial,sans-serif}
.h{font-size:20px}
.q{color:#00c}
--></style>
<script>
<!--
function sf(){document.f.q.focus();}
function clk(url,oi,cad,ct,cd,sg){if(document.images){var e = window.encodeURIComponent ? encodeURIComponent : escape;var u="""";var oi_param="""";var cad_param="""";if (url) u=""&url=""+e(url.replace(/#.*/,"""")).replace(/\+/g,""%2B"");if (oi) oi_param=""&oi=""+e(oi);if (cad) cad_param=""&cad=""+e(cad);new Image().src=""/url?sa=T""+oi_param+cad_param+""&ct=""+e(ct)+""&cd=""+e(cd)+u+""&ei=LQscRduwH5esYdf_3c8H""+sg;}return true;}
// -->
</script>
</head><body bgcolor=#ffffff text=#000000 link=#0000cc vlink=#551a8b alink=#ff0000 onLoad=sf() topmargin=3 marginheight=3><center><table border=0 cellspacing=0 cellpadding=0 width=100%><tr><td align=right nowrap><font size=-1><a href=""/url?sa=p&pref=ig&pval=3&q=http://www.google.com/ig%3Fhl%3Den&sig=__yvmOvIrk79QYmDkrJAeuYO8jTmo="" onmousedown=""return clk('/url?sa=p&pref=ig&pval=3&q=http://www.google.com/ig%3Fhl%3Den&sig=__yvmOvIrk79QYmDkrJAeuYO8jTmo=','promos','hppphnu:en_us','pro','1','')"">Personalized Home</a>&nbsp;|&nbsp;<a href=""https://www.google.com/accounts/Login?continue=http://www.google.com/&hl=en"">Sign in</a></font></td></tr><tr height=4><td><img alt="""" width=1 height=1></td></tr></table><img src=""/intl/en/images/logo.gif"" width=276 height=110 alt=""Google""><br><br>
<form action=/search name=f><script><!--
function qs(el) {if (window.RegExp && window.encodeURIComponent) {var ue=el.href;var qe=encodeURIComponent(document.f.q.value);if(ue.indexOf(""q="")!=-1){el.href=ue.replace(new RegExp(""q=[^&$]*""),""q=""+qe);}else{el.href=ue+""&q=""+qe;}}return 1;}
// -->
</script><table border=0 cellspacing=0 cellpadding=4><tr><td nowrap><font size=-1><b>Web</b>&nbsp;&nbsp;&nbsp;&nbsp;<a class=q href=""/imghp?hl=en&tab=wi"" onClick=""return qs(this);"">Images</a>&nbsp;&nbsp;&nbsp;&nbsp;<a class=q href=""http://video.google.com/?hl=en&tab=wv"" onClick=""return qs(this);"">Video<a        style=""text-decoration:none""><sup><font	color=red>New!</font></sup></a></a>&nbsp;&nbsp;&nbsp;&nbsp;<a class=q href=""http://news.google.com/nwshp?hl=en&tab=wn"" onClick=""return qs(this);"">News</a>&nbsp;&nbsp;&nbsp;&nbsp;<a class=q href=""/maps?hl=en&tab=wl"" onClick=""return qs(this);"">Maps</a>&nbsp;&nbsp;&nbsp;&nbsp;<b><a href=""/intl/en/options/"" class=q onclick=""this.blur();return togDisp(event);"">more&nbsp;&raquo;</a></b><script><!--
function togDisp(e){stopB(e);var elems=document.getElementsByName('more');for(var i=0;i<elems.length;i++){var obj=elems[i];var dp="""";if(obj.style.display==""""){dp=""none"";}obj.style.display=dp;}return false;}
function stopB(e){if(!e)e=window.event;e.cancelBubble=true;}
document.onclick=function(event){var elems=document.getElementsByName('more');if(elems[0].style.display == """"){togDisp(event);}}
//-->
</script><style><!--
.cb{margin:.5ex}
--></style>
<span name=more id=more style=""display:none;position:absolute;background:#fff;border:1px solid #369;margin:-.5ex 1.5ex;padding:0 0 .5ex .8ex;width:16ex;line-height:1.9;z-index:1000"" onclick=""stopB(event);""><a href=# onclick=""return togDisp(event);""><img border=0 src=/images/x2.gif width=12 height=12 alt=""Close menu"" align=right class=cb></a><a class=q href=""http://books.google.com/bkshp?hl=en&tab=wp"" onClick=""return qs(this);"">Books</a><br><a class=q href=""http://froogle.google.com/frghp?hl=en&tab=wf"" onClick=""return qs(this);"">Froogle</a><br><a class=q href=""http://groups.google.com/grphp?hl=en&tab=wg"" onClick=""return qs(this);"">Groups</a><br><a href=""/intl/en/options/"" class=q><b>even more &raquo;</b></a></span></font></td></tr></table><table cellspacing=0 cellpadding=0><tr><td width=25%>&nbsp;</td><td align=center><input type=hidden name=hl value=en><input maxlength=2048 size=55 name=q value="""" title=""Google Search""><br><input type=submit value=""Google Search"" name=btnG><input type=submit value=""I'm Feeling Lucky"" name=btnI></td><td valign=top nowrap width=25%><font size=-2>&nbsp;&nbsp;<a href=/advanced_search?hl=en>Advanced Search</a><br>&nbsp;&nbsp;<a href=/preferences?hl=en>Preferences</a><br>&nbsp;&nbsp;<a href=/language_tools?hl=en>Language Tools</a></font></td></tr></table></form><br><br><font size=-1><a href=""/intl/en/ads/"">Advertising&nbsp;Programs</a> - <a href=/services/>Business Solutions</a> - <a href=/intl/en/about.html>About Google</a><span id=hp style=""behavior:url(#default#homepage)""></span>
<script>
//<!--
if (!hp.isHomePage('http://www.google.com/')) {document.write(""<p><a href=\""/mgyhp.html\"" onClick=\""style.behavior='url(#default#homepage)';setHomePage('http://www.google.com/');\"">Make Google Your Homepage!</a>"");}
//-->
</script></font><p><font size=-2>&copy;2006 Google</font></p></center></body></html>
";
            XmlDocument doc;

            doc = HtmlParser.Parse(source);
            Assert.IsNotNull(doc["root"]["html"]["head"]);
            Assert.IsNotNull(doc["root"]["html"]["body"]);
        }

        [TestMethod]
        [TestProperty("Lib", "LillTek.Advanced")]
        public void HtmlParser_Parse_CNN()
        {
            // Generate a DOM source scraped from www.google.com and 
            // make sure we don't see an exception.

            const string source =
@"
<html><head><meta http-equiv=""content-type"" content=""text/html; charset=UTF-8""><meta name=""description"" content=""Daily news and features from MSNBC.com and its team of news partners, including NBC News, Newsweek, the Washington Post, the Associated Press and Reuters. Breaking news and personalized stocks and weather.""><meta name=""Search.Document"" content=""front""><meta name=""Search.Updated"" content=""Thu, 28 Sep 2006 18:27:40 GMT""><meta name=""Search.Section"" content=""MSNBC COVER""><title>Today's News from MSNBC - MSNBC.com</title><link rel=""stylesheet"" type=""text/css"" href=""/css/html40.css""><link rel=""stylesheet"" type=""text/css"" href=""/id/7181924/""><link rel=""stylesheet"" type=""text/css"" href=""/default.ashx/id/3403310/""><link rel=""alternate"" type=""application/rss+xml"" title=""MSNBC - Top Stories"" href=""http://rss.msnbc.msn.com/id/3032091/device/rss/rss.xml"">
<link rel=""alternate"" type=""application/rss+xml"" title=""MSNBC - Most Viewed"" href=""http://rss.msnbc.msn.com/id/3058960/displaymode/1026/eventType/1/rss/rss.xml""><script src=""/js/std.js""></script><script type=""text/javascript"">gEnabled=false;</script></head><body leftmargin=""0"" topmargin=""0""><script src=""http://Ads1.msn.com/library/dap.js""></script><a href=""#skipnav"" style=""display:none;visibility:hidden"">Skip navigation</a><script>pd_top('Front','html40','3053415','Today\'s News from MSNBC Front Page','','MSNBC COVER','','','','','14:59, 28/09/06','','cover','','','3103848','70102','', '1',0);pd_om('msnbcom','100');var surveyList = new Array();checkSurvey();
</script><img src=""http://c.msn.com/c.gif?NC=1255&NA=1154&PS=70102&PI=7329&DI=305&TP=http%3a%2f%2fmsnbc.msn.com%2f"" width=""0"" height=""0"" border=""0"" alt="""" /><!-- SiteCatalyst code version: G.9. Copyright 1997-2004 Omniture, Inc. More info available at http://www.omniture.com --><script src=""http://www.msnbc.msn.com/js/s_code_remote.js""></script><div id=""nm_c1"" class=""nmX""></div><div id=""nm_c2"" class=""nmX""></div><div id=""nm_c3"" class=""nmX""></div><div id=""nm_c4"" class=""nmX""></div><div class=""clr""><div class=""dMSNME_1""><table cellpadding=""0"" cellspacing=""0"" class=""w779 pb9""><tr><td class=""container"" nowrap=""true""><form action=""http://www.msnbc.msn.com/"" id=""search"" method=""get"" onsubmit=""return sr_DoSearch()""><fieldset id=""searchset""><input type=""radio"" class=""ml0t10"" name=""search"" value=""Web"" tabindex=""3"" /><label class=""v4"" accesskey=""s"" for=""q"" tabindex=""4""> Web</label><input class=""ml0t10"" type=""radio"" name=""search"" value=""MSNBC"" checked=""true"" tabindex=""5"" /><label class=""v4"" accesskey=""s"" for=""q"" tabindex=""6""> MSNBC</label><input type=""text"" id=""q"" name=""q"" size=""14"" maxlength=""150"" tabindex=""1"" /><input type=""submit"" class=""button"" name=""submit"" value=""Search"" tabindex=""2"" /><input type=""hidden"" name=""id"" value=""11881780"" /><input name=""FORM"" value=""AE"" type=""hidden"" /><input type=""hidden"" name=""os"" value=""0"" /><input type=""hidden"" name=""gs"" value=""1"" /><input type=""hidden"" name=""p"" value=""1"" /></fieldset></form></td><td width=""25px""></td><a id=""ieshp""></a><td class=""container netnav pt9"" nowrap=""true""><div id=""udtD"">Updated: 2:59 p.m. ET Sept. 28, 2006</div><span id=""spnHome""><a href=""http://alerts.msnbc.com/"" rel=""nofollow"">Alerts</a> | <a href=""http://www.msnbc.msn.com/id/7422001/"" rel=""nofollow"">Newsletters</a> | <a href=""http://www.msnbc.msn.com/id/5216556/"" rel=""nofollow"">RSS</a> | <a href=""http://www.msnbc.msn.com/id/3303511/"" rel=""nofollow"">Help</a></span><a id=""anHome"" href=""#"" onClick=""setHome('onclick');"" style=""display:none"">Make MSNBC Your Homepage</a> | <a href=""http://g.msn.com/0nwenus0/AE/00"" rel=""nofollow"">MSN Home</a> | <a href=""http://g.msn.com/0nwenus0/AE/02?SU=http://msnbc.msn.com/"" rel=""nofollow"">Hotmail</a> | <a href=""http://login.passport.com/login.srf?lc=1033&id=41762&ru=http%3a%2f%2fwww.msnbc.msn.com%2f&tw=1800&kv=7&ct=1159470095&ems=1&ver=2.1.6000.1&rn=C!m*Puvr&tpf=87fe17a869aa21328c81add79f07dcce"">Sign In</a><script type=""text/javascript"">setHome();</script></td><td align=""right""><table cellpadding=""0"" cellspacing=""0""><tr><td><a href=""http://g.msn.com/0nwenus0/AE/14"" rel=""nofollow""><noscript><img alt=""MSN.com"" id=""logo"" src=""http://sc.msn.com/global/c/lgpos/MSFT_pos.gif"" height=""35"" width=""118"" /></noscript> <script type=""text/javascript"">sr_UpdHead();</script></a></td></tr></table></td></tr></table></div><script type=""text/javascript"">var os=new UberSniff();if (os.netscape){var set=document.getElementById(""searchset"");set.parentNode.style.width=set.parentNode.parentNode.style.width=set.offsetWidth + ""px"";}else if(os.ie){var frm=document.getElementById(""search"");frm.parentNode.style.width=frm.offsetWidth + ""px"";}</script></div><script language=""javascript"">
		function UpdateTimeStamp(pdt) {
			var n = document.getElementById(""udtD"");
			if(pdt != '' && n && window.DateTime) {
				var dt = new DateTime();
				pdt = dt.T2D(pdt);
				if(dt.GetTZ(pdt)) {n.innerHTML = dt.D2S(pdt,((''.toLowerCase()=='false')?false:true));}
			}
		}
		UpdateTimeStamp('632950667624170000');
	</script><div class=""w779 fL""><div class=""w130 fL""><table border=""0"" cellpadding=""0"" cellspacing=""0"" bgcolor=""#EEEEEE""><tr><td><a href=""/""><img width=""130"" height=""90"" border=""0"" alt=""MSNBC News"" src=""/images/msnbc/logo01.gif""></a></td></tr><tr><td align=""right""></td></tr><tr><td align=""right""><table border=""0"" cellpadding=""0"" cellspacing=""0"" width=""130"" class=""nmTS""><tr><td height=""20"" class=""nmIS"" id=""nmb"" name=""nmb"" nm_sn=""4001724"" nm_suf="""" CM_sf="""" CM=""NewsMenuL1""><a class=""nmLS"" href=""/id/8004316/"">Video</a></td></tr><tr><td height=""20"" class=""nmIS"" id=""nmb"" name=""nmb"" nm_sn=""3032524"" nm_suf="""" CM_sf="""" CM=""NewsMenuL1""><a class=""nmLS"" href=""/id/3032525/"">U.S. News</a></td></tr><tr><td height=""20"" class=""nmIS"" id=""nmb"" name=""nmb"" nm_sn=""3032552"" nm_suf="""" CM_sf="""" CM=""NewsMenuL1""><a class=""nmLS"" href=""/id/3032553/"">Politics</a></td></tr><tr><td height=""20"" class=""nmIS"" id=""nmb"" name=""nmb"" nm_sn=""3032506"" nm_suf="""" CM_sf="""" CM=""NewsMenuL1""><a class=""nmLS"" href=""/id/3032507/"">World News</a></td></tr><tr><td height=""20"" class=""nmIS"" id=""nmb"" name=""nmb"" nm_sn=""3032071"" nm_suf="""" CM_sf="""" CM=""NewsMenuL1""><a class=""nmLS"" href=""/id/3032072/"">Business</a></td></tr><tr><td height=""20"" class=""nmIS"" id=""nmb"" name=""nmb"" nm_sn=""3032112"" nm_suf="""" CM_sf="""" CM=""NewsMenuL1""><a class=""nmLS"" href=""/id/3032113/"">Sports</a></td></tr><tr><td height=""20"" class=""nmIS"" id=""nmb"" name=""nmb"" nm_sn=""3032083"" nm_suf="""" CM_sf="""" CM=""NewsMenuL1""><a class=""nmLS"" href=""/id/3032084/"">Entertainment</a></td></tr><tr><td height=""20"" class=""nmIS"" id=""nmb"" name=""nmb"" nm_sn=""3088327"" nm_suf="""" CM_sf="""" CM=""NewsMenuL1""><a class=""nmLS"" href=""/id/3032076/"">Health</a></td></tr><tr><td height=""20"" class=""nmIS"" id=""nmb"" name=""nmb"" nm_sn=""3032117"" nm_suf="""" CM_sf="""" CM=""NewsMenuL1""><a class=""nmLS"" href=""/id/3032118/"">Tech / Science</a></td></tr><tr><td height=""20"" class=""nmIS"" id=""nmb"" name=""nmb"" nm_sn=""3032127"" nm_suf="""" CM_sf="""" CM=""NewsMenuL1""><a class=""nmLS"" href=""/id/3362034/"">Weather</a></td></tr><tr><td height=""20"" class=""nmIS"" id=""nmb"" name=""nmb"" nm_sn=""3032122"" nm_suf="""" CM_sf="""" CM=""NewsMenuL1""><a class=""nmLS"" href=""/id/3032123/"">Travel</a></td></tr><tr><td height=""20"" class=""nmIS"" id=""nmb"" name=""nmb"" nm_sn=""3032104"" nm_suf="""" CM_sf="""" CM=""NewsMenuL1""><a class=""nmLS"" href=""/id/3032105/"">Blogs Etc.</a></td></tr><tr><td height=""20"" class=""nmIS"" id=""nmb"" name=""nmb"" nm_sn=""3104486"" nm_suf="""" CM_sf="""" CM=""NewsMenuL1""><a class=""nmLS"" href=""/id/3948437/"">Local News</a></td></tr><tr><td height=""20"" class=""nmIS"" id=""nmb"" name=""nmb"" nm_sn=""3032541"" nm_suf=""/site/newsweek"" CM_sf="""" CM=""NewsMenuL1""><a class=""nmLS"" href=""/id/3032542/site/newsweek/"">Newsweek</a></td></tr><tr><td height=""20"" class=""nmIS"" id=""nmb"" name=""nmb"" nm_sn=""5183445"" nm_suf="""" CM_sf="""" CM=""NewsMenuL1""><a class=""nmLS"" href=""/id/4999736/"">Multimedia</a></td></tr><tr><td height=""20"" class=""nmIS"" id=""nmb"" name=""nmb"" nm_sn=""7956945"" nm_suf="""" CM_sf="""" CM=""NewsMenuL1""><a class=""nmLS"" href=""/id/7468311/"">Most Popular</a></td></tr></table><script>nm_init('/id/', '1', 'useropt', '');</script></td></tr><script>nm_st('1');</script></table><div style=""padding-bottom:15;""><map name=""map14028136""><area coords=""0,110,130,132"" shape=""rect"" alt=""Autos"" href=""http://www.cars.com/go/index.jsp?aff=msnbc""><area coords=""0,91,130,110"" shape=""rect"" alt=""Jobs"" href=""http://msn.careerbuilder.com/PLI/R/FindJobs.htm?siteid=CBMSNBC017""><area coords=""0,62,130,91"" shape=""rect"" alt=""Real Estate with HomePages.com"" href=""http://clk.atdmt.com/USE/go/msnnkhpg0040000006use/direct/01/""><area coords=""0,43,130,61"" shape=""rect"" alt=""Dating"" href=""http://www.perfectmatch.com/trk.asp?CID=6172""><area coords=""0,24,129,42"" shape=""rect"" alt=""Shopping"" href=""http://shopping.msn.com/partnercategory.aspx?catId=646&amp;ptnrId=1&amp;ptnrdata=134&amp;0dm=C102P""><area coords=""0,0,130,22"" shape=""rect"" alt=""MSNBC Classifieds"" href=""/id/5289372/"" id=""gted"" CE=""1-6-6""></map><img border=""0"" src=""http://msnbcmedia.msn.com/i/msnbc/Sections/zClassifieds/NwsMenu_060726.gif"" height=""132"" width=""130"" vspace=""0"" hspace=""0"" usemap=""#map14028136""></div><div style=""padding-bottom:15;""><link href=""/css/html40.css"" rel=""stylesheet"" type=""text/css""><script src=""/js/std.js""></script><script>var cssList = new Array();</script><script>getCSS(""3032045"")</script><div class=""box_3032045"" style=""width:122;""><script></script><table width=""122"" cellspacing=""0"" cellpadding=""0"" class=""boxH_3032045""><tr><td width=""*"" nowrap=""true"" class=""boxHC_3032045""><div class=""textSmallBold"">  ALSO ON MSNBC.com</div></td></tr></table><table width=""122"" cellspacing=""0"" cellpadding=""0"" class=""boxB_3032045""><tr valign=""top""><td></td></tr><tr valign=""top""><td class=""boxBI_3032045""><div class=""textHang""><div style=""padding-bottom:3px""><span class=""bulletRedSmall"">• </span><span class=""textSmall""><a href=""http://g.msn.com/0MNPAR00/27"">Corrections</a></span></div><div style=""padding-bottom:3px""><span class=""bulletRedSmall"">• </span><span class=""textSmall""><a href=""/id/14394865/"">Message boards</a></span></div><div style=""padding-bottom:3px""><span class=""bulletRedSmall"">• </span><span class=""textSmall""><a href=""/id/11701889/"" target=""_blank"">Quizzes</a></span></div><div style=""padding-bottom:3px""><span class=""bulletRedSmall"">• </span><span class=""textSmall""><a href=""http://www.msnbc.com/comics/games/sudoku.asp"">Sudoku</a></span></div><div style=""padding-bottom:3px""><span class=""bulletRedSmall"">• </span><span class=""textSmall""><a href=""http://g.msn.com/0MNPAR00/30"">Crossword</a></span></div><div style=""padding-bottom:3px""><span class=""bulletRedSmall"">• </span><span class=""textSmall""><a href=""http://g.msn.com/0MNPAR00/33"">Gossip</a></span></div><div style=""padding-bottom:3px""><span class=""bulletRedSmall"">• </span><span class=""textSmall""><a href=""http://msnbc.com/modules/take3/sept/default.htm"" target=""_blank"">Take 3 magazine</a></span></div><div style=""padding-bottom:3px""><span class=""bulletRedSmall"">• </span><span class=""textSmall""><a href=""http://g.msn.com/0MNPAR00/38"">Comics</a></span></div><div style=""padding-bottom:3px""><span class=""bulletRedSmall"">• </span><span class=""textSmall""><a href=""http://g.msn.com/0MNPAR00/31"">Horoscope</a></span></div><div style=""padding-bottom:3px""><span class=""bulletRedSmall"">• </span><span class=""textSmall""><a href=""/id/14187811/"" target=""_blank"">Lottery</a></span></div><div style=""padding-bottom:3px""><span class=""bulletRedSmall"">• </span><span class=""textSmall""><a href=""http://g.msn.com/0MNPAR00/32"">Sports scores</a></span></div><div style=""padding-bottom:3px""><span class=""bulletRedSmall"">• </span><span class=""textSmall""><a href=""http://g.msn.com/0MNPAR00/36"">Fantasy sports</a></span></div><div style=""padding-bottom:3px""><span class=""bulletRedSmall"">• </span><span class=""textSmall""><a href=""http://g.msn.com/0MNPAR00/34"">Stock quotes</a></span></div><div style=""padding-bottom:3px""><span class=""bulletRedSmall"">• </span><span class=""textSmall""><a href=""http://g.msn.com/0MNPAR00/35"">The Week in Pictures</a></span></div><div style=""padding-bottom:3px""><span class=""bulletRedSmall"">• </span><span class=""textSmall""><a href=""http://g.msn.com/0MNPAR00/37"">MSNBC Alerts</a></span></div></div></td></tr></table></div></div><div style=""text-align:center;padding-top:15;""></div><div style=""text-align:center;padding-top:15;""></div></div><div class=""w649 fL h1 clrR""><map name=""map7888277""><area coords=""98,1,211,35"" shape=""rect"" alt=""Today Show"" href=""/id/3032633/?ta=y""><area coords=""378,0,505,34"" shape=""rect"" alt=""Dateline NBC"" href=""/id/3032600/""><area coords=""505,0,649,34"" shape=""rect"" alt=""Meet The Press with Tim Russert"" href=""/id/3032608/""><area coords=""215,0,378,34"" shape=""rect"" alt=""NBC Nightly News with Brian Williams"" href=""/id/3032619/""><area coords=""0,0,98,34"" shape=""rect"" alt=""MSNBC TV"" href=""/id/3096434/""></map><img border=""0"" src=""http://msnbcmedia.msn.com/i/msnbc/Components/Art/COVER/z_evergreen/nbcnews_shows4.jpg"" height=""35"" width=""649"" vspace=""0"" hspace=""0"" usemap=""#map7888277""><div class=""clr""></div><a name=""skipnav""></a><span CM=""MSNBC COVERTSM""><table cellspacing=""0"" cellpadding=""0"" border=""0"" width=""649"" class=""tsm""><tr><td valign=""top"" rowspan=""2"" width=""1%""><map name=""map15046437""><area coords=""160,143,370,180"" shape=""rect"" alt=""Police academy a ‘disaster’"" href=""/id/15022363/"" id=""gted"" CE=""MainArt-1""><area coords=""0,0,369,179"" shape=""rect"" alt=""‘MONTH OF HOLY WAR’ - ‘Unconventional’ attacks urged; Iraq insurgent losses tallied"" href=""/id/15044435/"" id=""gted"" CE=""MainArt-2""></map><img border=""0"" src=""http://msnbcmedia.msn.com/i/msnbc/Components/Art/COVER/060928/STG_HZ_MonthHolyWar_950a.jpg"" height=""180"" width=""370"" vspace=""0"" hspace=""0"" usemap=""#map15046437""></td><td rowspan=""2"" width=""4"" bgcolor=""#ffffff""><spacer type=""horizontal"" width=""4""></spacer></td><td valign=""top"" class=""tsmLinkList""><table cellspacing=""0"" cellpadding=""0"" border=""0"" class=""tsmLinksTable""><tr><td><table cellspacing=""0"" cellpadding=""0"" border=""0"" width=""1"" align=""Right""><tr><td><a id=""gted"" CE=""StoryArt"" href=""/id/15041037/""><img border=""0"" src=""http://msnbcmedia.msn.com/j/msnbc/Components/Art/COVER/060928/w_060928_cvr_colokiller_8a.tsm68x68.jpg"" style=""border:1px solid #000000;"" align=""Right"" vspace=""0"" hspace=""0""></a></td></tr></table><span class=""tsmHeadlineSmallReverse""><a id=""gted"" CE=""StoryHeadlineLink"" href=""/id/15041037/"">Face of a killer</a></span><br><span class=""tsmtextMedLt""><a id=""gted"" CE=""StoryLink"" href=""/id/15041037/"">Police ID Colo. school gunman, say he sexually assaulted hostages.</a></span> <span class=""tsmbullet"">•</span> <span class=""tsmFullStoryLink""><a href=""/id/15041037/"" id=""gted"" CE=""StoryFullStoryLink1"">STORY</a></span> <b><span class=""tsmHeadlineList1ReverseBold"">|</span></b> <span class=""tsmbullet"">•</span> <span class=""tsmFullStoryLink""><a href=""javascript:msnvDwd('00','280324b7-ebf4-45b5-b271-2c9e9694c0e2','us','hotvideo_m_edpicks','','msnbc','','15042311','Shooting%20survivor')"">VIDEO</a></span><br clear=""all""><div style=""width:255px;height:10px;font-size:0px;"">.</div><span class=""tsmtextSmallLt"">MORE TOP STORIES</span><br><span class=""tsmbullet"">•</span> <span class=""tsmHeadlineList1ReverseBold""><a href=""/id/15043741/"" id=""gted"" CE=""StoryLink1"">Blaze threatens many Calif. homes</a></span><br><span class=""tsmbullet"">•</span> <span class=""tsmHeadlineList1ReverseBold""><a href=""/id/15047117/"" id=""gted"" CE=""StoryLink2"">Shooting, manhunt near Fla. school</a></span><br><span class=""tsmbullet"">•</span> <span class=""tsmHeadlineList1ReverseBold""><a href=""/id/15044215/"" id=""gted"" CE=""StoryLink3"">GOP moves to seal terror trial plan</a></span><br></td></tr></table></td></tr><tr><td valign=""bottom"" class=""tsmLinkList""><table cellspacing=""0"" cellpadding=""3"" border=""0""><tr><td><span class=""tsmcredit"">Jalal Mudha / AP file
</span></td></tr></table></td></tr></table><table width=""649"" cellspacing=""0"" cellpadding=""0"" border=""0""><tr valign=""top""><td width=""215""><span subCM=""PartnerBox1""></span></td><td width=""2""></td></tr></table><table width=""649"" cellspacing=""0"" cellpadding=""0"" border=""0""><tr><td bgcolor=""#666666"" height=""2""><spacer type=""block"" width=""649"" height=""2""></spacer></td></tr></table></span><div class=""w349 fL h1""><div class=""w320 p1""><div class=""
				p7
			""><script>getCSS(""3054092"")</script><span CM=""MSNBC COVERBColumnAboveCB1""><div class=""box_3054092"" style=""width:320;""><script></script><table width=""320"" cellspacing=""0"" cellpadding=""0"" class=""boxH_3054092""><tr><td width=""*"" nowrap=""true"" class=""boxHC_3054092""><div class=""textSmallBold"">  IN THE NEWS</div></td></tr></table><table width=""320"" cellspacing=""0"" cellpadding=""0"" class=""boxB_3054092""><tr valign=""top""><td class=""boxBI_3054092""><div class=""textHang""><div style=""padding-bottom:3px""><span class=""bulletRedSmall"">• </span><span class=""textMed""><a href=""/id/15042929/"" id=""gted"" CE=""Link-1"">Panel compares HP to Watergate, Enron</a> | <a href=""javascript:msnvDwd('00','cc3da169-2072-4500-87df-77e9005fb38d','us','Source_NBC%20News','','msnbc','','5736426','HP%20hearings')"">Watch live</a></span></div><div style=""padding-bottom:3px""><span class=""bulletRedSmall"">• </span><span class=""textMed""><a href=""/id/15047076/"" id=""gted"" CE=""Link-1"">IBM, Lenovo recall 526,000 laptop batteries</a></span></div><div style=""padding-bottom:3px""><span class=""bulletRedSmall"">• </span><span class=""textMed""><a href=""/id/15035711/"" id=""gted"" CE=""Link-1"">Kids, parents often both on ADHD meds, study says</a></span></div><div style=""padding-bottom:3px""><span class=""bulletRedSmall"">• </span><span class=""textMed""><a href=""/id/15044065/"" id=""gted"" CE=""Link-1"">Scientists find why 1918 Spanish flu was so deadly</a></span></div><div style=""padding-bottom:3px""><span class=""bulletRedSmall"">• </span><span class=""textMed""><a href=""/id/15046524/"" id=""gted"" CE=""Link-1"">Eagle on 18 gives Woods the lead at AmEx</a> | <a href=""http://boards.live.com/MSNBCboards/board.aspx?BoardID=452"">Discuss</a></span></div><div style=""padding-bottom:3px""><span class=""bulletRedSmall"">• </span><span class=""textMed""><a href=""/id/15043899/"" id=""gted"" CE=""Link-1"">Smells good! Cheese workers win $208M lottery prize</a></span></div></div></td></tr></table></div></span></div><div class=""
				p7
			""><script>getCSS(""6630167"")</script><span CM=""MSNBC COVERBColumnAboveCB2""><div class=""box_6630167"" style=""width:300;""><script></script><table width=""300"" cellspacing=""0"" cellpadding=""0"" class=""boxH_6630167""><tr><td width=""1%"" class=""boxHI_6630167""><map name=""map8321952""><area coords=""0,0,86,19"" shape=""rect"" alt=""Newsweek.com"" href=""/id/3032542/site/newsweek/""></map><img border=""0"" src=""http://msnbcmedia.msn.com/i/msnbc/Cover/Cover Elements/nwl_boxheaderogo.gif"" align=""Top"" height=""20"" width=""87"" vspace=""0"" hspace=""0"" usemap=""#map8321952""></td><td width=""*"" nowrap=""true"" class=""boxHC_6630167""><div class=""textSmallBold"">  </div></td><td width=""80%"" class=""boxH2C_6630167""><div class=""textSmall"">  <a href=""/id/3032542/site/newsweek/"">DAILY EDITION</a></div></td></tr></table><table width=""300"" cellspacing=""0"" cellpadding=""0"" class=""boxB_6630167""><tr valign=""top""><td class=""boxBI_6630167""><div class=""textHang""><div style=""padding-bottom:3px""><span class=""bulletRedSmall"">• </span><span class=""textMed""><a href=""/id/15046056/site/newsweek/"" id=""gted"" CE=""Link-1"">In Nevada senate race, Bush is a key issue</a> </span></div><div style=""padding-bottom:3px""><span class=""bulletRedSmall"">• </span><span class=""textMed""><a href=""/id/14957769/site/newsweek/"" id=""gted"" CE=""Link-1"">Life after AOL: Steve Case's new travel empire </a></span></div><div style=""padding-bottom:3px""><span class=""bulletRedSmall"">• </span><span class=""textMed""><a href=""/id/15035936/site/newsweek/"" id=""gted"" CE=""Link-1"">Analysis: Bush makes bad intel news sound better</a></span></div><div style=""padding-bottom:3px""><span class=""bulletRedSmall"">• </span><span class=""textMed""><a href=""/id/8760263/site/newsweek/"" id=""gted"" CE=""Link-1"">Vote for your favorite celebrity photo</a> </span></div></div></td></tr></table></div></span></div><div class=""
				p7
			""><script type='text/javascript' src='http://msnbc.msn.com/databox/data.aspx?dbid=7424393&s=&js=1&' ></script></div><div class=""
				p7
			""><script>getCSS(""3054092"")</script><span CM=""MSNBC COVERBColumnAboveCB4""><div class=""box_3054092"" style=""width:320;""><script></script><table width=""320"" cellspacing=""0"" cellpadding=""0"" class=""boxH_3054092""><tr><td width=""*"" nowrap=""true"" class=""boxHC_3054092""><div class=""textSmallBold"">  INSIDE MSNBC.COM</div></td></tr></table><table width=""320"" cellspacing=""0"" cellpadding=""0"" class=""boxB_3054092""><tr valign=""top""><td class=""boxBI_3054092""><div class=""textHang""><div style=""padding-bottom:3px""><span class=""bulletRedSmall"">• </span><span class=""textMed""><a href=""/id/15046254/"" id=""gted"" CE=""Link-1"">Owens at practice, may play Sunday</a> | <a href=""/id/15047279/"" id=""gted"" CE=""Link-2"">Police irate</a></span></div><div style=""padding-bottom:3px""><span class=""bulletRedSmall"">• </span><span class=""textMed""><a href=""/id/15045920/"" id=""gted"" CE=""Link-1"">MySpace could be worth $15 billion, analyst says</a></span></div><div style=""padding-bottom:3px""><span class=""bulletRedSmall"">• </span><span class=""textMed""><a href=""/id/15042299/"" id=""gted"" CE=""Link-1"">Lieberman stays ahead of Lamont in Connecticut poll</a></span></div><div style=""padding-bottom:3px""><span class=""bulletRedSmall"">• </span><span class=""textMed""><a href=""/id/15043507/"" id=""gted"" CE=""Link-1"">Former czarina buried in Russia 78 years after death</a></span></div><div style=""padding-bottom:3px""><span class=""bulletRedSmall"">• </span><span class=""textMed""><a href=""/id/15036342/"" id=""gted"" CE=""Link-1"">Officials: Anna Nicole Smith probe not closed</a></span></div><div style=""padding-bottom:3px""><span class=""bulletRedSmall"">• </span><span class=""textMed""><a href=""/id/15043759/"" id=""gted"" CE=""Link-1"">Want to live cheaply? See the most-affordable cities</a> </span></div><div style=""padding-bottom:3px""><span class=""bulletRedSmall"">• </span><span class=""textMed""><a href=""/id/14061438/"" id=""gted"" CE=""Link-1"">America Unzipped: Not your father's sex shop</a></span></div><div style=""padding-bottom:3px""><span class=""bulletRedSmall"">• </span><span class=""textMed""><a href=""/id/15038135/"" id=""gted"" CE=""Link-1"">Is she set to be a sports star? Measure her ring finger</a></span></div></div></td></tr></table></div></span></div><div class=""
				p7
			""><script>getCSS(""3054092"")</script><span CM=""MSNBC COVERBColumnAboveCB5""><div class=""box_3054092"" style=""width:300;""><script></script><table width=""300"" cellspacing=""0"" cellpadding=""0"" class=""boxH_3054092""><tr><td width=""*"" nowrap=""true"" class=""boxHC_3054092""><div class=""textSmallBold"">  ONLY ON MSNBC.COM</div></td></tr></table><table width=""300"" cellspacing=""0"" cellpadding=""0"" class=""boxB_3054092""><tr valign=""top""><td><table cellspacing=""0"" cellpadding=""0"" align=""left"" style=""padding-right:18px;padding-bottom:5px;""><tr><td><a href=""/id/15000898/"" id=""gted"" CE=""Link-1""><img border=""0"" src=""http://msnbcmedia.msn.com/j/msnbc/Components/Art/COVER/060928/tz100_milkshake_060928_10a.thumb.jpg"" style=""border:1px solid #000000;"" vspace=""0"" hspace=""0"" alt=""Z4-41 Chocolate milkshake""></a><div class=""credit"" style=""text-align:right;"">Index Stock Imagery</div></td></tr></table><div class=""boxBI_3054092""><div class=""textHang mgbtm""><span class=""bulletRedSmall"">• </span><span class=""textMed""><b><a href=""/id/15000898/"" id=""gted"" CE=""Link-1"">No spoon needed</a></b></span></div><div style=""margin-left:11px;""><p class=""textMed""><a href=""/id/15000898/"">Looking to create the perfect milkshake? Here are some secrets.<br></a>__________________________________</p><div class=""textHang""><div style=""padding-bottom:3px""><span class=""bulletRedSmall"">• </span><span class=""textMed""><a href=""/id/14626190/"" id=""gted"" CE=""Link-1"">Scoop: Baby gift ends Aguilera-Spears feud</a></span></div><div style=""padding-bottom:3px""><span class=""bulletRedSmall"">• </span><span class=""textMed""><a href=""/id/15035472/"" id=""gted"" CE=""Link-1"">Opinion: T.O. telling the truth?</a></span></div><div style=""padding-bottom:3px""><span class=""bulletRedSmall"">• </span><span class=""textMed""><a href=""/id/14508342/"" id=""gted"" CE=""Link-1"">Predictions 101: No. 2 Auburn to roll</a></span></div><div style=""padding-bottom:3px""><span class=""bulletRedSmall"">• </span><span class=""textMed""><a href=""javascript:SSOpen('6020616','0');"">Slide show: Animal Tracks</a></span></div><div style=""padding-bottom:3px""><span class=""bulletRedSmall"">• </span><span class=""textMed""><a href=""/id/15002281/"" id=""gted"" CE=""Link-1"">Top ten dark comedies: Do you agree?</a></span></div><div style=""padding-bottom:3px""><span class=""bulletRedSmall"">• </span><span class=""textMed""><a href=""http://cosmiclog.msnbc.msn.com/archive/2006/09/28/4985.aspx"">Cosmic log: Spaceship dream revived</a></span></div><div style=""padding-bottom:3px""><span class=""bulletRedSmall"">• </span><span class=""textMed""><a href=""/id/15004014/"" id=""gted"" CE=""Link-1"">How 'Detroit disease' spread to Chrysler</a></span></div><div style=""padding-bottom:3px""><span class=""bulletRedSmall"">• </span><span class=""textMed""><a href=""/id/4326967/"" id=""gted"" CE=""Link-1"">Test Pattern: Novelty tunes that will never die</a></span></div><div style=""padding-bottom:3px""><span class=""bulletRedSmall"">• </span><span class=""textMed""><a href=""/id/5498022/"" id=""gted"" CE=""Link-1"">Read more of the Web’s best reporting</a></span></div></div></div></div></td></tr></table></div></span></div><div class=""
				p7
			""><span CM=""MSNBC COVERBColumnAboveAutoBox6""><script>getCSS(""3089180"")</script><div class=""box_3089180"" style=""width:320;""><script></script><table width=""320"" cellspacing=""0"" cellpadding=""0"" class=""boxH_3089180""><tr><td width=""*"" nowrap=""true"" class=""boxHC_3089180""><div class=""textSmallBold"">  VIDEO  </div></td><td width=""80%"" class=""boxH2C_3089180""><div class=""textSmall"">  <a href=""/id/8004316/"" id=""gted"" CE=""Tab1"">MORE</a></div></td></tr></table><table width=""320"" cellspacing=""0"" cellpadding=""0"" class=""boxB_3089180""><tr valign=""top""><td><div style=""float:right;margin-left:10px;text-align:right;""><a href=""javascript:msnvDwd('00','29f9df1e-4ae6-46a8-a59c-0a6b82b026cd','us','hotvideo_m_edpicks','','msnbc','','15046749','Gunman\u2018s motive still a mystery')""><a href=""javascript:msnvDwd('00','29f9df1e-4ae6-46a8-a59c-0a6b82b026cd','us','hotvideo_m_edpicks','','msnbc','','15046749','Gunman\u2018s motive still a mystery')""><img border=""0"" src=""http://msnbcmedia.msn.com/j/msnbc/Components/Video/060928/n_london_colo_060928.thumb.jpg"" style=""border:1px solid #000000;"" vspace=""0"" hspace=""0""></a></a></div><div><div class=""textHang"" style=""padding:5px;""><span class=""bulletRedSmall"">• </span><span class=""textMed""><b><a href=""javascript:msnvDwd('00','29f9df1e-4ae6-46a8-a59c-0a6b82b026cd','us','hotvideo_m_edpicks','','msnbc','','15046749','Gunman\u2018s motive still a mystery')"">Gunman's motive still a mystery</a></b></span><br><span class=""textMed"">Sept. 28: Investigators are trying to determine why a gunman entered a high school in Bailey, Colo., on Wednesday and shot and killed a hostage before turning the gun on himself. NBC's Jennifer London reports.</span></div></div><hr size=""1"" width=""95%"" align=""center"" color=""#cccccc"" style=""clear:all""></td></tr><tr valign=""top""><td class=""boxBI_3089180""><div class=""textHang"" style=""padding-bottom:3px;""><span class=""bulletRedSmall"">• </span><span class=""headlineList2""><a href=""javascript:msnvDwd('00','adb1cf98-8178-426f-ae8b-aed9e0dcae0a','us','hotvideo_m_edpicks','','msnbc','','15045926','Raging wildfire threatens homes')"">Raging wildfire threatens homes</a></span></div><div class=""textHang"" style=""padding-bottom:3px;""><span class=""bulletRedSmall"">• </span><span class=""headlineList2""><a href=""javascript:msnvDwd('00','ec730c0e-e852-4ee8-ae46-e5b9a440efeb','us','hotvideo_m_edpicks','','msnbc','','15042322','Is bin Laden being hunted hard enough?')"">Is bin Laden being hunted hard enough?</a></span></div><div class=""textHang"" style=""padding-bottom:3px;""><span class=""bulletRedSmall"">• </span><span class=""headlineList2""><a href=""javascript:msnvDwd('00','6a801ecb-55e9-48e8-8fe0-e58af77b7e1e','us','hotvideo_m_edpicks','','msnbc','','15036158','3-year-old found passed out drunk')"">3-year-old found passed out drunk</a></span></div><div class=""textHang"" style=""padding-bottom:3px;""><span class=""bulletRedSmall"">• </span><span class=""headlineList2""><a href=""javascript:msnvDwd('00','182474f0-dd54-4b1a-ae96-ba684ed6e3ae','us','hotvideo_m_edpicks','','msnbc','','15042315','Cowboys\u2018 Owens denies suicide attempt')"">Cowboys' Owens denies suicide attempt</a></span></div></td></tr></table></div></span></div><hr width=""320"" size=""2"" noshade=""true""><table width=""320"" border=""0"" cellspacing=""0"" cellpadding=""0""><tr><td><span CM=""MSNBC COVERPromoBCol""><div style=""padding-bottom:20;""><map name=""map14719036""><area coords=""0,0,120,163"" shape=""rect"" alt=""http://www.msnbc.msn.com/id/3032619/"" href=""/id/3032619/""></map><img border=""0"" src=""http://msnbcmedia.msn.com/i/msnbc/Components/Art/SITEWIDE/In_house_promos/120_Ads/NBC/120x163_nightly_news.gif"" height=""163"" width=""120"" vspace=""0"" hspace=""0"" usemap=""#map14719036""></div></span></td><td valign=""top"" width=""5""> </td><td valign=""top"" align=""center"" class=""textSmallGrey"">advertisement<br><script>ad_dap(150,180,'&PG=NBCFC2&AP=1087');</script></td></tr></table><hr width=""320"" size=""2"" noshade=""true""><div class=""
				p7
			""><meta http-equiv=""refresh"" content=""1200; URL=http://g.msn.com/0MNPAR00/2""></div><div class=""
				p7
			""><span CM=""MSNBC COVERBColumnBelowAutoBox2""><script>getCSS(""3089180"")</script><div class=""box_3089180"" style=""width:320;""><script></script><table width=""320"" cellspacing=""0"" cellpadding=""0"" class=""boxH_3089180""><tr><td width=""*"" nowrap=""true"" class=""boxHC_3089180""><div class=""textSmallBold"">  U.S. NEWS  </div></td><td width=""80%"" class=""boxH2C_3089180""><div class=""textSmall"">  <a href=""/id/4327893/"" id=""gted"" CE=""Tab1"">CRIME</a> | <a href=""/id/3032572/"" id=""gted"" CE=""Tab2"">SECURITY</a> | <a href=""/id/3032525/"" id=""gted"" CE=""Tab3"">MORE</a> | <a href=""http://rss.msnbc.msn.com/id/3032524/device/rss/rss.xml""><img border=""0"" src=""http://msnbcmedia.msn.com/i/msnbc/Sections/zNews Tools/Newstools/RSS/rss_icon.gif"" height=""11"" width=""29"" vspace=""0"" hspace=""0""></a></div></td></tr></table><table width=""320"" cellspacing=""0"" cellpadding=""0"" class=""boxB_3089180""><tr valign=""top""><td class=""boxBI_3089180""><div class=""textHang"" style=""padding-bottom:3px;""><span class=""bulletRedSmall"">• </span><span class=""headlineList2""><a href=""/id/15041037/"" id=""gted"" CE=""1"">Police ID gunman in Colo. shooting</a></span></div><div class=""textHang"" style=""padding-bottom:3px;""><span class=""bulletRedSmall"">• </span><span class=""headlineList2""><a href=""/id/15043741/"" id=""gted"" CE=""2"">Blaze nears Calif. homes</a></span></div><div class=""textHang"" style=""padding-bottom:3px;""><span class=""bulletRedSmall"">• </span><span class=""headlineList2""><a href=""/id/15037066/"" id=""gted"" CE=""3"">WP: Warming trend hatches a business</a></span></div><div class=""textHang"" style=""padding-bottom:3px;""><span class=""bulletRedSmall"">• </span><span class=""headlineList2""><a href=""/id/15043899/"" id=""gted"" CE=""4"">Cheese workers celebrate lottery win</a></span></div><div class=""textHang"" style=""padding-bottom:3px;""><span class=""bulletRedSmall"">• </span><span class=""headlineList2""><a href=""/id/15024576/"" id=""gted"" CE=""5"">White House refuses to release full NIE report</a></span></div></td></tr></table></div></span></div><div class=""
				p7
			""><span CM=""MSNBC COVERBColumnBelowAutoBox3""><script>getCSS(""3089180"")</script><div class=""box_3089180"" style=""width:320;""><script></script><table width=""320"" cellspacing=""0"" cellpadding=""0"" class=""boxH_3089180""><tr><td width=""*"" nowrap=""true"" class=""boxHC_3089180""><div class=""textSmallBold"">  <a href=""/id/3032553/"">U.S. POLITICS</a>  </div></td><td width=""80%"" class=""boxH2C_3089180""><div class=""textSmall"">  <a href=""/id/3036697/"" id=""gted"" CE=""Tab1"">HARDBALL</a> | <a href=""http://firstread.msnbc.msn.com/default.aspx"">FIRST READ</a> | <a href=""http://rss.msnbc.msn.com/id/3032552/device/rss/rss.xml""><img border=""0"" src=""http://msnbcmedia.msn.com/i/msnbc/Sections/zNews Tools/Newstools/RSS/rss_icon.gif"" height=""11"" width=""29"" vspace=""0"" hspace=""0""></a></div></td></tr></table><table width=""320"" cellspacing=""0"" cellpadding=""0"" class=""boxB_3089180""><tr valign=""top""><td class=""boxBI_3089180""><div class=""textHang"" style=""padding-bottom:3px;""><span class=""bulletRedSmall"">• </span><span class=""headlineList2""><a href=""/id/15047679/"" id=""gted"" CE=""1"">Iraq, terror pop up in governors' races</a></span></div><div class=""textHang"" style=""padding-bottom:3px;""><span class=""bulletRedSmall"">• </span><span class=""headlineList2""><a href=""/id/15047374/"" id=""gted"" CE=""2"">NY A.G. candidate vows to stay in race</a></span></div><div class=""textHang"" style=""padding-bottom:3px;""><span class=""bulletRedSmall"">• </span><span class=""headlineList2""><a href=""/id/15047016/"" id=""gted"" CE=""3"">Bolton nomination vote unlikely before recess</a></span></div><div class=""textHang"" style=""padding-bottom:3px;""><span class=""bulletRedSmall"">• </span><span class=""headlineList2""><a href=""/id/15045996/"" id=""gted"" CE=""4"">Libby’s closed-door hearings continue</a></span></div><div class=""textHang"" style=""padding-bottom:3px;""><span class=""bulletRedSmall"">• </span><span class=""headlineList2""><a href=""/id/15044215/"" id=""gted"" CE=""5"">Republicans move to seal terror trial plan</a></span></div></td></tr></table></div></span></div><div class=""
				p7
			""><span CM=""MSNBC COVERBColumnBelowAutoBox4""><script>getCSS(""3089180"")</script><div class=""box_3089180"" style=""width:320;""><script></script><table width=""320"" cellspacing=""0"" cellpadding=""0"" class=""boxH_3089180""><tr><td width=""*"" nowrap=""true"" class=""boxHC_3089180""><div class=""textSmallBold"">  WORLD NEWS  </div></td><td width=""80%"" class=""boxH2C_3089180""><div class=""textSmall"">  <a href=""/id/3042924/"" id=""gted"" CE=""Tab1"">IRAQ</a> | <a href=""/id/8885163/"" id=""gted"" CE=""Tab2"">TERRORISM</a> | <a href=""/id/3032507/"" id=""gted"" CE=""Tab3"">MORE</a> | <a href=""http://rss.msnbc.msn.com/id/3032506/device/rss/rss.xml""><img border=""0"" src=""http://msnbcmedia.msn.com/i/msnbc/Sections/zNews Tools/Newstools/RSS/rss_icon.gif"" height=""11"" width=""29"" vspace=""0"" hspace=""0""></a></div></td></tr></table><table width=""320"" cellspacing=""0"" cellpadding=""0"" class=""boxB_3089180""><tr valign=""top""><td class=""boxBI_3089180""><div class=""textHang"" style=""padding-bottom:3px;""><span class=""bulletRedSmall"">• </span><span class=""headlineList2""><a href=""/id/15041886/"" id=""gted"" CE=""1"">40 tortured bodies found in Baghdad</a></span></div><div class=""textHang"" style=""padding-bottom:3px;""><span class=""bulletRedSmall"">• </span><span class=""headlineList2""><a href=""/id/15022363/"" id=""gted"" CE=""2"">WP: Iraq police academy a 'disaster'</a></span></div><div class=""textHang"" style=""padding-bottom:3px;""><span class=""bulletRedSmall"">• </span><span class=""headlineList2""><a href=""/id/15044435/"" id=""gted"" CE=""3"">Report: Al-Qaida in Iraq acknowledges losses</a></span></div><div class=""textHang"" style=""padding-bottom:3px;""><span class=""bulletRedSmall"">• </span><span class=""headlineList2""><a href=""/id/15037065/"" id=""gted"" CE=""4"">WP: In tribal Pakistan, an uneasy quiet</a></span></div><div class=""textHang"" style=""padding-bottom:3px;""><span class=""bulletRedSmall"">• </span><span class=""headlineList2""><a href=""/id/15046160/"" id=""gted"" CE=""5"">Russia-Georgia relations hit new low</a></span></div></td></tr></table></div></span></div><div class=""
				p7
			""><span CM=""MSNBC COVERBColumnBelowAutoBox5""><script>getCSS(""3089180"")</script><div class=""box_3089180"" style=""width:320;""><script></script><table width=""320"" cellspacing=""0"" cellpadding=""0"" class=""boxH_3089180""><tr><td width=""*"" nowrap=""true"" class=""boxHC_3089180""><div class=""textSmallBold"">  SPORTS  </div></td><td width=""80%"" class=""boxH2C_3089180""><div class=""textSmall"">  <a href=""/id/3104018/"" id=""gted"" CE=""Tab1"">SCORES</a> | <a href=""/id/3243963/"" id=""gted"" CE=""Tab2"">TEAMS</a> | <a href=""/id/3032113/"" id=""gted"" CE=""Tab3"">MORE</a> | <a href=""http://rss.msnbc.msn.com/id/3032112/device/rss/rss.xml""><img border=""0"" src=""http://msnbcmedia.msn.com/i/msnbc/Sections/zNews Tools/Newstools/RSS/rss_icon.gif"" height=""11"" width=""29"" vspace=""0"" hspace=""0""></a></div></td></tr></table><table width=""320"" cellspacing=""0"" cellpadding=""0"" class=""boxB_3089180""><tr valign=""top""><td class=""boxBI_3089180""><div class=""textHang"" style=""padding-bottom:3px;""><span class=""bulletRedSmall"">• </span><span class=""headlineList2""><a href=""/id/15021367/"" id=""gted"" CE=""1"">Borges: Historic collapse might be in the Cards</a></span></div><div class=""textHang"" style=""padding-bottom:3px;""><span class=""bulletRedSmall"">• </span><span class=""headlineList2""><a href=""/id/15046524/"" id=""gted"" CE=""2"">Eagle on 18 gives Tiger lead at AmEx</a></span></div><div class=""textHang"" style=""padding-bottom:3px;""><span class=""bulletRedSmall"">• </span><span class=""headlineList2""><a href=""/id/15046254/"" id=""gted"" CE=""3"">T.O. returns to practice, might play Sunday</a></span></div><div class=""textHang"" style=""padding-bottom:3px;""><span class=""bulletRedSmall"">• </span><span class=""headlineList2""><a href=""/id/15046897/"" id=""gted"" CE=""4"">Walsh says T.O. ignored his advice</a></span></div><div class=""textHang"" style=""padding-bottom:3px;""><span class=""bulletRedSmall"">• </span><span class=""headlineList2""><a href=""/id/15035460/"" id=""gted"" CE=""5"">Celizic: T.O. story might fade, not his problems</a></span></div></td></tr></table></div></span></div><div class=""
				p7
			""><span CM=""MSNBC COVERBColumnBelowAutoBox6""><script>getCSS(""3089180"")</script><div class=""box_3089180"" style=""width:320;""><script></script><table width=""320"" cellspacing=""0"" cellpadding=""0"" class=""boxH_3089180""><tr><td width=""*"" nowrap=""true"" class=""boxHC_3089180""><div class=""textSmallBold"">  BUSINESS  </div></td><td width=""80%"" class=""boxH2C_3089180""><div class=""textSmall"">  <a href=""/id/3683270/"" id=""gted"" CE=""Tab1"">STOCKS</a> | <a href=""/id/3033509/"" id=""gted"" CE=""Tab2"">CNBC</a> | <a href=""/id/3032072/"" id=""gted"" CE=""Tab3"">MORE</a> | <a href=""http://rss.msnbc.msn.com/id/3032071/device/rss/rss.xml""><img border=""0"" src=""http://msnbcmedia.msn.com/i/msnbc/Sections/zNews Tools/Newstools/RSS/rss_icon.gif"" height=""11"" width=""29"" vspace=""0"" hspace=""0""></a></div></td></tr></table><table width=""320"" cellspacing=""0"" cellpadding=""0"" class=""boxB_3089180""><tr valign=""top""><td class=""boxBI_3089180""><div class=""textHang"" style=""padding-bottom:3px;""><span class=""bulletRedSmall"">• </span><span class=""headlineList2""><a href=""/id/3683270/"" id=""gted"" CE=""1"">Dow briefly moves above milestone</a></span></div><div class=""textHang"" style=""padding-bottom:3px;""><span class=""bulletRedSmall"">• </span><span class=""headlineList2""><a href=""/id/15043041/"" id=""gted"" CE=""2"">HP’s top lawyer resigns ahead of hearing</a></span></div><div class=""textHang"" style=""padding-bottom:3px;""><span class=""bulletRedSmall"">• </span><span class=""headlineList2""><a href=""/id/15042929/"" id=""gted"" CE=""3"">Lawmakers compare HP to Watergate, Enron</a></span></div><div class=""textHang"" style=""padding-bottom:3px;""><span class=""bulletRedSmall"">• </span><span class=""headlineList2""><a href=""/id/15043594/"" id=""gted"" CE=""4"">Second-quarter economic growth slows</a></span></div><div class=""textHang"" style=""padding-bottom:3px;""><span class=""bulletRedSmall"">• </span><span class=""headlineList2""><a href=""/id/15043689/"" id=""gted"" CE=""5"">U.S. jobless claims down slightly</a></span></div></td></tr></table></div></span></div><div class=""
				p7
			""><span CM=""MSNBC COVERBColumnBelowAutoBox7""><script>getCSS(""3089180"")</script><div class=""box_3089180"" style=""width:320;""><script></script><table width=""320"" cellspacing=""0"" cellpadding=""0"" class=""boxH_3089180""><tr><td width=""*"" nowrap=""true"" class=""boxHC_3089180""><div class=""textSmallBold"">  ENTERTAINMENT  </div></td><td width=""80%"" class=""boxH2C_3089180""><div class=""textSmall"">  <a href=""/id/3032387/"" id=""gted"" CE=""Tab1"">GOSSIP</a> | <a href=""/id/3032084/"" id=""gted"" CE=""Tab2"">MORE</a> | <a href=""http://rss.msnbc.msn.com/id/3032083/device/rss/rss.xml""><img border=""0"" src=""http://msnbcmedia.msn.com/i/msnbc/Sections/zNews Tools/Newstools/RSS/rss_icon.gif"" height=""11"" width=""29"" vspace=""0"" hspace=""0""></a></div></td></tr></table><table width=""320"" cellspacing=""0"" cellpadding=""0"" class=""boxB_3089180""><tr valign=""top""><td class=""boxBI_3089180""><div class=""textHang"" style=""padding-bottom:3px;""><span class=""bulletRedSmall"">• </span><span class=""headlineList2""><a href=""/id/14626190/"" id=""gted"" CE=""1"">Scoop: Aguilera and Spears end their feud</a></span></div><div class=""textHang"" style=""padding-bottom:3px;""><span class=""bulletRedSmall"">• </span><span class=""headlineList2""><a href=""/id/15036529/"" id=""gted"" CE=""2"">Hamlin heads home on a surprising ‘Dancing’</a></span></div><div class=""textHang"" style=""padding-bottom:3px;""><span class=""bulletRedSmall"">• </span><span class=""headlineList2""><a href=""/id/13578140/"" id=""gted"" CE=""3"">Four go to the finals on ‘Project Runway’</a></span></div><div class=""textHang"" style=""padding-bottom:3px;""><span class=""bulletRedSmall"">• </span><span class=""headlineList2""><a href=""/id/15036342/"" id=""gted"" CE=""4"">Officials: Anna Nicole probe not closed</a></span></div><div class=""textHang"" style=""padding-bottom:3px;""><span class=""bulletRedSmall"">• </span><span class=""headlineList2""><a href=""/id/15000898/"" id=""gted"" CE=""5"">Perfect milkshakes: No spoon needed</a></span></div></td></tr></table></div></span></div><div class=""
				p7
			""><span CM=""MSNBC COVERBColumnBelowAutoBox8""><script>getCSS(""3089180"")</script><div class=""box_3089180"" style=""width:320;""><script></script><table width=""320"" cellspacing=""0"" cellpadding=""0"" class=""boxH_3089180""><tr><td width=""*"" nowrap=""true"" class=""boxHC_3089180""><div class=""textSmallBold"">  TECH AND SCIENCE  </div></td><td width=""80%"" class=""boxH2C_3089180""><div class=""textSmall"">  <a href=""/id/3033063/"" id=""gted"" CE=""Tab1"">SPACE</a> | <a href=""/id/3032118/"" id=""gted"" CE=""Tab2"">MORE</a> | <a href=""http://rss.msnbc.msn.com/id/3032117/device/rss/rss.xml""><img border=""0"" src=""http://msnbcmedia.msn.com/i/msnbc/Sections/zNews Tools/Newstools/RSS/rss_icon.gif"" height=""11"" width=""29"" vspace=""0"" hspace=""0""></a></div></td></tr></table><table width=""320"" cellspacing=""0"" cellpadding=""0"" class=""boxB_3089180""><tr valign=""top""><td class=""boxBI_3089180""><div class=""textHang"" style=""padding-bottom:3px;""><span class=""bulletRedSmall"">• </span><span class=""headlineList2""><a href=""/id/15045311/"" id=""gted"" CE=""1"">Virgin Galactic unveils SpaceShipTwo interior</a></span></div><div class=""textHang"" style=""padding-bottom:3px;""><span class=""bulletRedSmall"">• </span><span class=""headlineList2""><a href=""/id/15047076/"" id=""gted"" CE=""2"">IBM, Lenovo recall laptop batteries</a></span></div><div class=""textHang"" style=""padding-bottom:3px;""><span class=""bulletRedSmall"">• </span><span class=""headlineList2""><a href=""/id/15043992/"" id=""gted"" CE=""3"">Microsoft sets $250 price for Zune</a></span></div><div class=""textHang"" style=""padding-bottom:3px;""><span class=""bulletRedSmall"">• </span><span class=""headlineList2""><a href=""/id/15031167/"" id=""gted"" CE=""4"">Price a key weapon in next-gen console war</a></span></div><div class=""textHang"" style=""padding-bottom:3px;""><span class=""bulletRedSmall"">• </span><span class=""headlineList2""><a href=""/id/15046826/"" id=""gted"" CE=""5"">Photos reveal 1935 airship at bottom of ocean</a></span></div></td></tr></table></div></span></div><div class=""
				p7
			""><span CM=""MSNBC COVERBColumnBelowAutoBox9""><script>getCSS(""3089180"")</script><div class=""box_3089180"" style=""width:320;""><script></script><table width=""320"" cellspacing=""0"" cellpadding=""0"" class=""boxH_3089180""><tr><td width=""*"" nowrap=""true"" class=""boxHC_3089180""><div class=""textSmallBold"">  HEALTH  </div></td><td width=""80%"" class=""boxH2C_3089180""><div class=""textSmall"">  <a href=""/id/3034511/"" id=""gted"" CE=""Tab1"">DIET &amp; FITNESS</a> | <a href=""/id/3032076/"" id=""gted"" CE=""Tab2"">MORE</a> | <a href=""http://rss.msnbc.msn.com/id/3088327/device/rss/rss.xml""><img border=""0"" src=""http://msnbcmedia.msn.com/i/msnbc/Sections/zNews Tools/Newstools/RSS/rss_icon.gif"" height=""11"" width=""29"" vspace=""0"" hspace=""0""></a></div></td></tr></table><table width=""320"" cellspacing=""0"" cellpadding=""0"" class=""boxB_3089180""><tr valign=""top""><td class=""boxBI_3089180""><div class=""textHang"" style=""padding-bottom:3px;""><span class=""bulletRedSmall"">• </span><span class=""headlineList2""><a href=""/id/14061438/"" id=""gted"" CE=""1"">Not your father's sex shop</a></span></div><div class=""textHang"" style=""padding-bottom:3px;""><span class=""bulletRedSmall"">• </span><span class=""headlineList2""><a href=""/id/15047380/"" id=""gted"" CE=""2"">Suicide rate dropping among young and old</a></span></div><div class=""textHang"" style=""padding-bottom:3px;""><span class=""bulletRedSmall"">• </span><span class=""headlineList2""><a href=""/id/15035711/"" id=""gted"" CE=""3"">Kid, parent often both on ADHD meds</a></span></div><div class=""textHang"" style=""padding-bottom:3px;""><span class=""bulletRedSmall"">• </span><span class=""headlineList2""><a href=""/id/15038135/"" id=""gted"" CE=""4"">Finger length points to athletic ability</a></span></div><div class=""textHang"" style=""padding-bottom:3px;""><span class=""bulletRedSmall"">• </span><span class=""headlineList2""><a href=""/id/15044105/"" id=""gted"" CE=""5"">Easy on the eye? Maybe on the brain, too</a></span></div></td></tr></table></div></span></div><div class=""
				p7
			""><span CM=""MSNBC COVERBColumnBelowAutoBox10""><script>getCSS(""3089180"")</script><div class=""box_3089180"" style=""width:320;""><script></script><table width=""320"" cellspacing=""0"" cellpadding=""0"" class=""boxH_3089180""><tr><td width=""*"" nowrap=""true"" class=""boxHC_3089180""><div class=""textSmallBold"">  TV NEWS  </div></td><td width=""80%"" class=""boxH2C_3089180""><div class=""textSmall"">  <a href=""http://www.today.msnbc.com/"">TODAY</a> | <a href=""http://www.nightlynews.msnbc.com/"">NIGHTLY NEWS</a> | <a href=""http://www.dateline.msnbc.com/"">DATELINE</a> </div></td></tr></table><table width=""320"" cellspacing=""0"" cellpadding=""0"" class=""boxB_3089180""><tr valign=""top""><td class=""boxBI_3089180""><div class=""textHang"" style=""padding-bottom:3px;""><span class=""bulletRedSmall"">• </span><span class=""headlineList2""><a href=""/id/3032633/?ta=y"" id=""gted"" CE=""1"">'Today': School shooting survivor recalls ordeal</a></span></div><div class=""textHang"" style=""padding-bottom:3px;""><span class=""bulletRedSmall"">• </span><span class=""headlineList2""><a href=""/id/15036633/"" id=""gted"" CE=""2"">Olbermann: Threatening letter no joke</a></span></div><div class=""textHang"" style=""padding-bottom:3px;""><span class=""bulletRedSmall"">• </span><span class=""headlineList2""><a href=""/id/3032633/?ta=y"" id=""gted"" CE=""3"">'Today' wedding: Vote for your favorite cake!</a></span></div><div class=""textHang"" style=""padding-bottom:3px;""><span class=""bulletRedSmall"">• </span><span class=""headlineList2""><a href=""/id/3032633/?ta=y"" id=""gted"" CE=""4"">'Today': Do men really want a June Cleaver wife?</a></span></div><div class=""textHang"" style=""padding-bottom:3px;""><span class=""bulletRedSmall"">• </span><span class=""headlineList2""><a href=""/id/3041478/"" id=""gted"" CE=""5"">Janet Jackson on the Plaza Friday: E-mail us!</a></span></div></td></tr></table></div></span></div><div class=""
				p7
			""><span CM=""MSNBC COVERBColumnBelowAutoBox11""><script>getCSS(""3089180"")</script><div class=""box_3089180"" style=""width:320;""><script></script><table width=""320"" cellspacing=""0"" cellpadding=""0"" class=""boxH_3089180""><tr><td width=""*"" nowrap=""true"" class=""boxHC_3089180""><div class=""textSmallBold"">  <a href=""/id/3032123/?ta=y"">TRAVEL</a>  </div></td><td width=""80%"" class=""boxH2C_3089180""><div class=""textSmall"">  <a href=""/id/3032123/?ta=y"" id=""gted"" CE=""Tab1"">MSNBC TRAVEL</a> | <a href=""http://rss.msnbc.msn.com/id/3032122/device/rss/rss.xml""><img border=""0"" src=""http://msnbcmedia.msn.com/i/msnbc/Sections/zNews Tools/Newstools/RSS/rss_icon.gif"" height=""11"" width=""29"" vspace=""0"" hspace=""0""></a></div></td></tr></table><table width=""320"" cellspacing=""0"" cellpadding=""0"" class=""boxB_3089180""><tr valign=""top""><td class=""boxBI_3089180""><div class=""textHang"" style=""padding-bottom:3px;""><span class=""bulletRedSmall"">• </span><span class=""headlineList2""><a href=""/id/15021506/"" id=""gted"" CE=""1"">Noble retreats: Stylish sanctuaries in Europe</a></span></div><div class=""textHang"" style=""padding-bottom:3px;""><span class=""bulletRedSmall"">• </span><span class=""headlineList2""><a href=""/id/15035542/"" id=""gted"" CE=""2"">Voyage to Hawaii: Weeklong flings from $649</a></span></div><div class=""textHang"" style=""padding-bottom:3px;""><span class=""bulletRedSmall"">• </span><span class=""headlineList2""><a href=""/id/14591059/"" id=""gted"" CE=""3"">Journey to a land that will tug your soul</a></span></div><div class=""textHang"" style=""padding-bottom:3px;""><span class=""bulletRedSmall"">• </span><span class=""headlineList2""><a href=""/id/12726803/"" id=""gted"" CE=""4"">The amazing spas of tropical Australia</a></span></div><div class=""textHang"" style=""padding-bottom:3px;""><span class=""bulletRedSmall"">• </span><span class=""headlineList2""><a href=""/id/15001307/"" id=""gted"" CE=""5"">24-Hour Layover: Sensational Seattle</a></span></div><div class=""textHang"" style=""padding-bottom:3px;""><span class=""bulletRedSmall"">• </span><span class=""headlineList2""><a href=""javascript:SSOpen('14929601','0');"">Slide show: The Emerald City</a></span></div></td></tr></table></div></span></div><div class=""
				p7
			""><span CM=""MSNBC COVERBColumnBelowAutoBox12""><script>getCSS(""3089180"")</script><div class=""box_3089180"" style=""width:320;""><script></script><table width=""320"" cellspacing=""0"" cellpadding=""0"" class=""boxH_3089180""><tr><td width=""*"" nowrap=""true"" class=""boxHC_3089180""><div class=""textSmallBold"">  BLOGS ETC.  </div></td><td width=""80%"" class=""boxH2C_3089180""><div class=""textSmall"">  <a href=""/id/4838957/"" id=""gted"" CE=""Tab1"">LETTERS</a> | <a href=""/id/14394865/"" id=""gted"" CE=""Tab2"">MESSAGE BOARDS</a> | <a href=""http://rss.msnbc.msn.com/id/3032104/device/rss/rss.xml""><img border=""0"" src=""http://msnbcmedia.msn.com/i/msnbc/Sections/zNews Tools/Newstools/RSS/rss_icon.gif"" height=""11"" width=""29"" vspace=""0"" hspace=""0""></a></div></td></tr></table><table width=""320"" cellspacing=""0"" cellpadding=""0"" class=""boxB_3089180""><tr valign=""top""><td class=""boxBI_3089180""><div class=""textHang"" style=""padding-bottom:3px;""><span class=""bulletRedSmall"">• </span><span class=""headlineList2""><a href=""http://clicked.msnbc.msn.com"">Clicked:  Photowalk with me</a></span></div><div class=""textHang"" style=""padding-bottom:3px;""><span class=""bulletRedSmall"">• </span><span class=""headlineList2""><a href=""http://cosmiclog.msnbc.msn.com"">Cosmic Log: Spaceship dream revived</a></span></div><div class=""textHang"" style=""padding-bottom:3px;""><span class=""bulletRedSmall"">• </span><span class=""headlineList2""><a href=""http://firstread.msnbc.msn.com/"">First Read: One step closer to warrantless surveillance?</a></span></div><div class=""textHang"" style=""padding-bottom:3px;""><span class=""bulletRedSmall"">• </span><span class=""headlineList2""><a href=""http://dailynightly.msnbc.com"">Daily Nightly:  Varying degrees of intelligence</a></span></div><div class=""textHang"" style=""padding-bottom:3px;""><span class=""bulletRedSmall"">• </span><span class=""headlineList2""><a href=""/id/4326967/"" id=""gted"" CE=""5"">Test Pattern: Multi-link Monday</a></span></div><div class=""textHang"" style=""padding-bottom:3px;""><span class=""bulletRedSmall"">• </span><span class=""headlineList2""><a href=""javascript:OCW('http://baghdadblog.msnbc.com/', '_blank','resizable=yes,status=yes,scrollbars=yes,fullscreen=no,location=yes,menubar=yes,titlebar=yes,toolbar=yes,');"">Blogging Baghdad: Hope elementary</a></span></div><div class=""textHang"" style=""padding-bottom:3px;""><span class=""bulletRedSmall"">• </span><span class=""headlineList2""><a href=""/id/6330851/"" id=""gted"" CE=""7"">Regular Joe: “Idol” thoughts</a></span></div></td></tr></table></div></span></div></div></div><div class=""w300 p2 pS fL clrR""><div class=""p7""><script>getCSS(""3054092"")</script><span CM=""MSNBC COVERCColumnAbove
				CB1""><div class=""box_3054092"" style=""width:300;""><script></script><table width=""300"" cellspacing=""0"" cellpadding=""0"" class=""boxH_3054092""><tr><td width=""*"" nowrap=""true"" class=""boxHC_3054092""><div class=""textSmallBold"">  NBC NEWS HIGHLIGHTS  </div></td><td width=""80%"" class=""boxH2C_3054092""><div class=""textSmall"">  <img border=""0"" src=""http://msnbcmedia.msn.com/i/msnbc/Components/ColorBoxes/Styles/ColorBoxImages_GlobalOnlyPlease/video_icon.gif"" height=""14"" width=""28"" vspace=""0"" hspace=""0""><a href=""javascript:msnvDwd('00','7de0cd3e-486a-4072-9476-92357f3126b6','us','hotvideo_m_edpicks','','msnbc','','3677250','Watch the headlines')"">Video headlines</a></div></td></tr></table><table width=""300"" cellspacing=""0"" cellpadding=""0"" class=""boxB_3054092""><tr valign=""top""><td class=""boxBI_3054092""><div class=""textHang""><div style=""padding-bottom:3px""><span class=""bulletRedSmall"">• </span><span class=""textMed""><a href=""/id/15036633/"" id=""gted"" CE=""Link-1"">Olbermann: Threatening letter no joke</a></span></div><div style=""padding-bottom:3px""><span class=""bulletRedSmall"">• </span><span class=""textMed""><a href=""javascript:msnvDwd('00','6bff15ef-1f31-46e7-9675-efefee5a10dc','us','Source_Scarborough_Country','c1151','msnbc','','15038138','Religious camp stirs controversy')"">Video: 'Jesus Camp' brainwashing kids?</a></span></div><div style=""padding-bottom:3px""><span class=""bulletRedSmall"">• </span><span class=""textMed""><a href=""/id/13083094/"" id=""gted"" CE=""Link-1"">'Today' wedding: Vote for your favorite cake</a></span></div></div></td></tr></table></div></span></div><div class=""w300 aC textSmallGrey p7"">
			advertisement<br><script>ad_dap(250,300,'&PG=NBCFC1&AP=1089');</script></div><div class=""p7""><script type='text/javascript' src='http://msnbc.msn.com/databox/data.aspx?dbid=3242551&s=&js=1&' ></script></div><div class=""p7""><script>getCSS(""3060335"")</script><div class=""box_3060335"" style=""width:300;""><script></script><table width=""300"" cellspacing=""0"" cellpadding=""0"" class=""boxH_3060335""><tr><td width=""*"" nowrap=""true"" class=""boxHC_3060335""><div class=""textSmallBold"">  MARKET UPDATE</div></td></tr></table><table width=""300"" cellspacing=""0"" cellpadding=""0"" class=""boxB_3060335""><tr valign=""top""><td class=""boxBI_3060335""><script type='text/javascript' src='http://msnbc.msn.com/databox/data.aspx?dbid=3060654&s=$INDU,$COMPX,$INX&js=1&' ></script></td></tr></table><table width=""300"" cellspacing=""0"" cellpadding=""0"" class=""boxF_3060335""><tr><td class=""boxFI_3060335""><div class=""textSmall"">Data: <a href=""http://go.msn.com/intg/397.asp?target=http://moneycentral.msn.com/partner/redir/msnbc.asp%3Fsource%3Dstockdata"">MSN Money</a> and <a href=""http://www.comstock-interactivedata.com/"">ComStock</a> </div></td></tr></table></div></div><div class=""p7""><span CM=""MSNBC COVERCColumnBelow
					AutoBox3""><script>getCSS(""3216310"")</script><div class=""box_3216310"" style=""width:300;""><script></script><table width=""300"" cellspacing=""0"" cellpadding=""0"" class=""boxH_3216310""><tr><td width=""*"" nowrap=""true"" class=""boxHC_3216310""><div class=""textSmallBold"">  WASHINGTONPOST.COM HIGHLIGHTS  </div></td><td width=""80%"" class=""boxH2C_3216310""><div class=""textSmall"">  <a href=""/id/3032586/"" id=""gted"" CE=""Tab1"">MORE</a></div></td></tr></table><table width=""300"" cellspacing=""0"" cellpadding=""0"" class=""boxB_3216310""><tr valign=""top""><td class=""boxBI_3216310""><div class=""textHang"" style=""padding-bottom:3px;""><span class=""bulletRedSmall"">• </span><span class=""headlineList2""><a href=""/id/15022363/"" id=""gted"" CE=""1"">Iraq police academy a 'disaster'</a></span></div><div class=""textHang"" style=""padding-bottom:3px;""><span class=""bulletRedSmall"">• </span><span class=""headlineList2""><a href=""/id/15037065/"" id=""gted"" CE=""2"">WP: In tribal Pakistan, an uneasy quiet</a></span></div><div class=""textHang"" style=""padding-bottom:3px;""><span class=""bulletRedSmall"">• </span><span class=""headlineList2""><a href=""/id/15037066/"" id=""gted"" CE=""3"">Warming trend is hatching a business</a></span></div><div class=""textHang"" style=""padding-bottom:3px;""><span class=""bulletRedSmall"">• </span><span class=""headlineList2""><a href=""/id/15036070/"" id=""gted"" CE=""4"">WP: Most Iraqis favor U.S. pullout, polls find</a></span></div><div class=""textHang"" style=""padding-bottom:3px;""><span class=""bulletRedSmall"">• </span><span class=""headlineList2""><a href=""/id/15022356/"" id=""gted"" CE=""5"">Distrust divides Iraqi neighbors</a></span></div></td></tr></table></div></span></div><div class=""p7""><script>getCSS(""3553566"")</script><span CM=""MSNBC COVERCColumnBelow
					CB4""><div class=""box_3553566"" style=""width:300;overflow:hidden;""><script></script><table width=""300"" cellspacing=""0"" cellpadding=""0"" class=""boxH_3553566""><tr><td width=""*"" nowrap=""true"" class=""boxHC_3553566""><div class=""textSmallBold"">  SPECIAL REPORTS</div></td></tr></table><table width=""300"" cellspacing=""0"" cellpadding=""0"" class=""boxB_3553566""><tr valign=""top""><td style=""padding:10px;""><a href=""/id/13636317/"" id=""gted"" CE=""1""><a href=""/id/13636317/""><img border=""0"" src=""http://msnbcmedia.msn.com/i/msnbc/Components/Art/COVER/PartnerBoxHeaders/TZ100_candles.jpg"" height=""74"" width=""98"" vspace=""0"" hspace=""0""></a></a></td><td class=""boxBI_3553566""><div class=""textHang""><div><span class=""bulletRedSmall"">• </span><span class=""textMed""><b><a href=""/id/13636317/"" id=""gted"" CE=""1"">MSNBC.COM TURNS 10: Travel back in time to see how it all began</a></b><br></span></div></div></td></tr><tr valign=""top""><td style=""padding:10px;""><a href=""http://risingfromruin.msnbc.com/stories.html""><a href=""http://risingfromruin.msnbc.com/stories.html""><img border=""0"" src=""http://msnbcmedia.msn.com/j/msnbc/Components/Art/COVER/051102/oneTown_tease.thumb.jpg"" style=""border:1px solid black;"" vspace=""0"" hspace=""0""></a></a></td><td class=""boxBI_3553566""><div class=""textHang""><div><span class=""bulletRedSmall"">• </span><span class=""textMed""><b><a href=""http://risingfromruin.msnbc.com/stories.html"">RISING FROM RUIN: Two towns rebuild in Katrina's aftermath</a></b><br></span></div></div></td></tr><tr valign=""top""><td style=""padding:10px;""><a href=""/id/11369578/"" id=""gted"" CE=""3""><a href=""/id/11369578/""><img border=""0"" src=""http://msnbcmedia.msn.com/i/msnbc/Components/Art/COVER/060711/TZ100_IslamEurope.gif"" style=""border:1px solid #000000;"" height=""74"" width=""98"" vspace=""0"" hspace=""0""></a></a></td><td class=""boxBI_3553566""><div class=""textHang""><div><span class=""bulletRedSmall"">• </span><span class=""textMed""><b><a href=""/id/11369578/"" id=""gted"" CE=""3"">CULTURE CLASH: Europe's growing challenge with Islam.</a></b><br></span></div></div></td></tr><tr valign=""top""><td style=""padding:10px;""><a href=""/id/12837221/"" id=""gted"" CE=""4""><img border=""0"" src=""http://msnbcmedia.msn.com/i/msnbc/Components/Art/BUSINESS/Projects/Nest_Egg/TZ98_68_NestEgg.jpg"" height=""68"" width=""98"" vspace=""0"" hspace=""0""></a></td><td class=""boxBI_3553566""><div class=""textHang""><div><span class=""bulletRedSmall"">• </span><span class=""textMed""><b><a href=""/id/12837221/"" id=""gted"" CE=""4"">CRACKED NEST EGG: Navigating retirement in an uncertain financial world</a></b><br></span></div></div></td></tr><tr valign=""top""><td style=""padding:10px;""><a href=""/id/13154507/"" id=""gted"" CE=""5""><img border=""0"" src=""http://msnbcmedia.msn.com/j/msnbc/Components/Art/HEALTH/PROJECTS/LowBlow_ProstateCancer/TZ100_lowBlow.gif,thumb.jpg"" vspace=""0"" hspace=""0""></a></td><td class=""boxBI_3553566""><div class=""textHang""><div><span class=""bulletRedSmall"">• </span><span class=""textMed""><b><a href=""/id/13154507/"" id=""gted"" CE=""5"">LOW BLOW: One man's battle with prostate cancer</a></b><br></span></div></div></td></tr><tr valign=""top""><td style=""padding:10px;""><a href=""http://techtour.msnbc.com/""><a href=""http://techtour.msnbc.com""><img border=""0"" src=""http://msnbcmedia.msn.com/j/msnbc/Components/Interactives/tech_tour/8_InflatableTents/TZ100_techtour8_inflatabletent.thumb.jpg"" style=""border:1px solid #000000;"" vspace=""0"" hspace=""0""></a></a></td><td class=""boxBI_3553566""><div class=""textHang""><div><span class=""bulletRedSmall"">• </span><span class=""textMed""><b><a href=""http://techtour.msnbc.com/"">TECH TOUR: A youthful inventor makes award-winning inflatable tents</a></b><br></span></div></div></td></tr><tr valign=""top""><td style=""padding:10px;""><a href=""http://msnbc.com/modules/take3/sept/default.htm""><img border=""0"" src=""http://msnbcmedia.msn.com/j/msnbc/Components/Photos/060901/tz100_take3_circus_centerpiece.thumb.jpg"" style=""border:1px solid #000000;"" vspace=""0"" hspace=""0""></a></td><td class=""boxBI_3553566""><div class=""textHang""><div><span class=""bulletRedSmall"">• </span><span class=""textMed""><b><a href=""http://msnbc.com/modules/take3/sept/default.htm"">TAKE 3 MAGAZINE: The circus is in town, and it's the adults who are getting all excited</a></b><br></span></div></div></td></tr><tr valign=""top""><td style=""padding:10px;""><a href=""/id/12157286/"" id=""gted"" CE=""8""><a href=""/id/12157286/""><img border=""0"" src=""http://msnbcmedia.msn.com/i/msnbc/Components/Art/HEALTH/PROJECTS/06_ElleSexLoveSurvey/tz98_sexlove.gif"" style=""border:1px solid #000000;"" height=""74"" width=""98"" vspace=""0"" hspace=""0"" alt=""Elle Sex and Love Survey ""></a></a></td><td class=""boxBI_3553566""><div class=""textHang""><div><span class=""bulletRedSmall"">• </span><span class=""textMed""><b><a href=""/id/12157286/"" id=""gted"" CE=""8"">HOT MONOGAMY: An intimate look at love and sex, including a special poll</a></b><br></span></div></div></td></tr></table></div></span></div></div></div><div class=""w779 p5 aC clr""><div style=""padding-bottom:20;""><hr id=""foothr"" width=""758"" color=""#000000"" noshade=""true"" size=""1""/><div class=""bb"" style=""padding: 10 0 8 0;""><a href=""/"">Cover</a> | <a href=""/id/3032525/"">U.S. News</a> | <a href=""/id/3032553/"">Politics</a> | <a href=""/id/3032507/"">World News</a> | <a href=""/id/3032072/"">Business</a> | <a href=""/id/3032113/"">Sports</a> | <a href=""/id/3032118/"">Tech/Science</a> | <a href=""/id/3032084/"">Entertainment</a> | <a href=""/id/3032123/"">Travel</a> | <a href=""/id/3032076/"">Health</a> | <a href=""/id/3032105/"">Blogs Etc.</a> | <a href=""http://www.msnbc.msn.com/id/3362034/"">Weather</a> | <a href=""http://msnbc.msn.com/id/3098358/"">Local News</a></div><div class=""bb""><a href=""/id/3032542/site/newsweek/"">Newsweek</a> | <a href=""/id/3032633/"">Today Show</a> | <a href=""/id/3032619/"">Nightly News</a> | <a href=""/id/3032600/"">Dateline NBC</a> | <a href=""/id/3032608/"">Meet the Press</a> | <a href=""/id/3096434/"">MSNBC TV</a></div><div class=""bb"" style=""padding: 22 0 8 0;""><a href=""/id/3303510/"">About MSNBC.com </a> | <a href=""/id/7422001/"">Newsletters</a> | <a href=""/id/5216556/"">RSS</a> | <a href=""/id/8132577/"">Podcasts</a>  | <a href=""/id/3303511/"">Help</a> | <a href=""/id/3152772/"">News Tools</a> | <a href=""/id/3303596/"">Jobs at MSNBC.com</a> | <a href=""/id/10285339/"">Contact Us</a> | <a href=""/id/3303540/"">Terms & Conditions</a> | <a href=""http://privacy.msn.com"">Privacy</a></div><div class=""bb"" style=""padding=0 0 8 0;""><a href=""/"">&#0169; 2006 MSNBC.com</a></div></div></div><div class=""container"" id=""foot""><ul id=""legal""><li>&#169; 2006 Microsoft</li><li><a href=""http://g.msn.com/0nwenus0/AE/20?SU=http://msnbc.msn.com/"" rel=""nofollow"">MSN Privacy</a></li><li><a href=""http://g.msn.com/0nwenus0/AE/18?SU=http://msnbc.msn.com/"" rel=""nofollow"">Legal</a></li><li class=""last""><a href=""http://g.msn.com/0nwenus0/AE/19?SU=http://msnbc.msn.com/"" rel=""nofollow"">Advertise</a></li></ul><ul id=""support""><li><script language=""javascript"" src=""http://hp.msn.com/scr/op/ol-fdbkv3_r1.js""></script><a href=""javascript:O_LC();"">Feedback</a></li><li class=""last""><span class=""bar"">|</span>&#160;&#160;&#160;&#160;<a href=""http://www.msnbc.msn.com/id/3303511/"">Help</a></li></ul></div></div><div id=""DCol"" style=""position:absolute;width:160px;left:794px;""><span CM=""PromoDCol""></span><div class=""p2""></div><script>msnSideBar();</script></div><div id=""AdOverThePageDiv"" style=""position:absolute; left:990; top:0;""><script>ad_dap(1,1,'&PG=NBCPLB&AP=1402');</script></div></body></html>";
            XmlDocument doc;

            doc = HtmlParser.Parse(source);
            Assert.IsNotNull(doc["root"]["html"]["head"]);
            Assert.IsNotNull(doc["root"]["html"]["body"]);
        }

        [TestMethod]
        [TestProperty("Lib", "LillTek.Advanced")]
        public void HtmlParser_Parse_MSNBC()
        {
            // Generate a DOM source scraped from www.google.com and 
            // make sure we don't see an exception.

            const string source =
@"
<!DOCTYPE HTML PUBLIC ""-//W3C//DTD HTML 4.01 Transitional//EN""><html lang=""en""><head><title>CNN.com - Breaking News, U.S., World, Weather, Entertainment &amp; Video News</title>    <meta http-equiv=""content-type"" content=""text/html; charset=iso-8859-1"">
<meta http-equiv=""refresh"" content=""1800"">
<meta name=""Title"" content=""CNN.com - Breaking News, U.S., World, Weather, Entertainment &amp; Video News"">
<meta name=""Description"" content=""CNN.com delivers the latest breaking news and information on the latest top stories, weather, business, entertainment, politics, and more. For in-depth coverage, CNN.com provides special reports, video, audio, photo galleries, and interactive guides."">
<meta name=""Keywords"" content=""CNN, CNN news, CNN.com, CNN TV, news, news online, breaking news, U.S. news, world news, weather, business, CNN Money, sports, politics, law, technology, entertainment, education, travel, health, special reports, autos, developing story, news video, CNN Intl"">
<link rel=""Start"" href=""/"">
<link rel=""Search"" href=""/search/"">
<link rel=""stylesheet"" href=""http://i.a.cnn.net/cnn/.element/ssi/css/1.3/common.css"" type=""text/css"">
<link rel=""stylesheet"" href=""http://i.a.cnn.net/cnn/.element/ssi/css/1.5/main.css"" type=""text/css"">
<style type=""text/css"">


.cnnSplitLk
{width:980px;}
</style>

<script language=""JavaScript1.2"" src=""http://i.a.cnn.net/cnn/.element/ssi/js/1.3/main.js"" type=""text/javascript""></script>
<script language=""JavaScript1.2"" src=""http://i.a.cnn.net/cnn/.element/ssi/js/1.5/mainVideoMod.js"" type=""text/javascript""></script>
<script language=""JavaScript1.2"" src=""http://i.a.cnn.net/cnn/.element/ssi/js/1.3/flash_detect.js"" type=""text/javascript""></script>


<script language=""javascript"" type=""text/javascript"">
<!--
	if (typeof(cnnPreloadImages) != ""undefined"") {
		cnnPreloadImages(""http://i.a.cnn.net/cnn/.element/img/1.5/ceiling/shows/am_over.gif"",""http://i.a.cnn.net/cnn/.element/img/1.5/ceiling/shows/ac_over.gif"",""http://i.a.cnn.net/cnn/.element/img/1.5/ceiling/shows/ld_over.gif"",""http://i.a.cnn.net/cnn/.element/img/1.5/ceiling/shows/lkl_over.gif"",""http://i.a.cnn.net/cnn/.element/img/1.5/ceiling/shows/ng_over.gif"",""http://i.a.cnn.net/cnn/.element/img/1.5/ceiling/shows/pz_over.gif"",""http://i.a.cnn.net/cnn/.element/img/1.5/ceiling/shows/sched_over.gif"",""http://i.a.cnn.net/cnn/.element/img/1.5/ceiling/shows/sr_over.gif"",""http://i.a.cnn.net/cnn/.element/img/1.5/main/tabs/topstories.gif"",""http://i.a.cnn.net/cnn/.element/img/1.5/main/tabs/topstories_over.gif"",""http://i.a.cnn.net/cnn/.element/img/1.5/main/tabs/mostpop.gif"",""http://i.a.cnn.net/cnn/.element/img/1.5/main/tabs/mostpop_over.gif"",""http://i.a.cnn.net/cnn/.element/img/1.5/main/tabs/bestvideo.gif"",""http://i.a.cnn.net/cnn/.element/img/1.5/main/tabs/bestvideo_over.gif"",""http://i.a.cnn.net/cnn/.element/img/1.5/main/tabs/live.gif"",""http://i.a.cnn.net/cnn/.element/img/1.5/main/tabs/live_over.gif"",""http://i.a.cnn.net/cnn/.element/img/1.5/main/tabs/what.gif"",""http://i.a.cnn.net/cnn/.element/img/1.5/main/tabs/what_over.gif"",""http://i.a.cnn.net/cnn/.element/img/1.4/main/biz/markets.gif"",""http://i.a.cnn.net/cnn/.element/img/1.4/main/biz/markets_over.gif"",""http://i.a.cnn.net/cnn/.element/img/1.4/main/biz/quote.gif"",""http://i.a.cnn.net/cnn/.element/img/1.4/main/biz/quote_over.gif"");
	}

	
// -->
</script>
<script language=""JavaScript"" type=""text/javascript"">var cnnCurrTime = new Date(1159470314816); var cnnCurrHour = 15;</script>
    <link rel=""alternate"" type=""application/rss+xml"" title=""CNN - Top Stories [RSS]"" href=""http://rss.cnn.com/rss/cnn_topstories.rss"">
<link rel=""alternate"" type=""application/rss+xml"" title=""CNN - Recent Stories [RSS]"" href=""http://rss.cnn.com/rss/cnn_latest.rss""> <script language=""JavaScript"" type=""text/javascript"">cnnSiteWideCurrDate = new Date(2006, 8, 28);</script><script type=""text/javascript"" language=""JavaScript1.1"" src=""http://ar.atwola.com/file/adsWrapper.js""></script>
<style type=""text/css"">
<!--
.aoltextad { text-align: justify; font-size: 12px; color: black; font-family: Georgia, sans-serif }
-->
</style>
<script type=""text/javascript"" src=""http://i.a.cnn.net/cnn/.element/ssi/js/1.3/ad_head0.js""></script>
<script type=""text/javascript"" src=""http://i.a.cnn.net/cnn/cnn_adspaces/cnn_adspaces.js""></script>
</head><body id=""cnnMainPage"" onload=""CNN_initVideoModules();cnnHandleCSIs()""><a name=""top_of_page""></a><a href=""#ContentArea""><img src=""http://i.cnn.net/cnn/images/1.gif"" alt=""Click here to skip to main content."" width=""10"" height=""1"" border=""0"" align=""right""></a><div id=""header"">    <!-- include virtual=""/editionssi/sect/1.5/MAIN/ceiling_ad.html""-->
<style type=""text/css"">
<!--
TABLE.cnnCeilnav TD.cnnNavHorRtBg
{background: #37618d url(http://i.a.cnn.net/cnn/.element/img/1.5/ceiling/nav.rt.end.bg.sprite.gif) top right no-repeat;}
-->
</style>
<table width=""980"" cellpadding=""0"" cellspacing=""0"" border=""0"" class=""cnnCeilTop"">
			<tr valign=""top"">
				<td class=""sides""></td>
				<td>
					<div id=""cnnCeil"">
						<!--ceiling.html-->
						<table width=""978"" cellpadding=""0"" cellspacing=""0"" border=""0"">
							<tr valign=""top"">
								<td width=""206""><a href=""/""><img src=""http://i.a.cnn.net/cnn/.element/img/1.5/ceiling/logo_cnn.gif"" width=""206"" height=""64"" hspace=""0"" vspace=""0"" border=""0"" alt=""CNN.com""></a></td>
								<td width=""772"">
									<table cellpadding=""0"" cellspacing=""0"" border=""0"" class=""cnnCeilShows"">
										<tr valign=""top"">
											<td class=""LeftEnd""><a href=""/CNN/Programs/american.morning/"" onmouseover=""cnnShowImgSwap('showImgAM',1);"" onmouseout=""cnnShowImgSwap('showImgAM',0);""><img src=""http://i.a.cnn.net/cnn/.element/img/1.5/ceiling/shows/am.gif"" alt="""" width=""95"" height=""6"" hspace=""0"" vspace=""0"" border=""0"" id=""showImgAM""></a></td>
											<td class=""spacer""></td>
											<td><a href=""/CNN/Programs/situation.room/"" onmouseover=""cnnShowImgSwap('showImgSR',1);"" onmouseout=""cnnShowImgSwap('showImgSR',0);""><img src=""http://i.a.cnn.net/cnn/.element/img/1.5/ceiling/shows/sr.gif"" alt="""" width=""79"" height=""6"" hspace=""0"" vspace=""0"" border=""0"" id=""showImgSR""></a></td>
											<td class=""spacer""></td>
											<td><a href=""/CNN/Programs/lou.dobbs.tonight/"" onmouseover=""cnnShowImgSwap('showImgLD',1);"" onmouseout=""cnnShowImgSwap('showImgLD',0);""><img src=""http://i.a.cnn.net/cnn/.element/img/1.5/ceiling/shows/ld.gif"" alt="""" width=""96"" height=""6"" hspace=""0"" vspace=""0"" border=""0"" id=""showImgLD""></a></td>
											<td class=""spacer""></td>
											<td><a href=""/CNN/Programs/paula.zahn.now/"" onmouseover=""cnnShowImgSwap('showImgPZ',1);"" onmouseout=""cnnShowImgSwap('showImgPZ',0);""><img src=""http://i.a.cnn.net/cnn/.element/img/1.5/ceiling/shows/pz.gif"" alt="""" width=""82"" height=""6"" hspace=""0"" vspace=""0"" border=""0"" id=""showImgPZ""></a></td>
											<td class=""spacer""></td>
											<td><a href=""/CNN/Programs/larry.king.live/"" onmouseover=""cnnShowImgSwap('showImgLK',1);"" onmouseout=""cnnShowImgSwap('showImgLK',0);""><img src=""http://i.a.cnn.net/cnn/.element/img/1.5/ceiling/shows/lkl.gif"" alt="""" width=""79"" height=""6"" hspace=""0"" vspace=""0"" border=""0"" id=""showImgLK""></a></td>
											<td class=""spacer""></td>
											<td><a href=""/CNN/Programs/anderson.cooper.360/"" onmouseover=""cnnShowImgSwap('showImgAC',1);"" onmouseout=""cnnShowImgSwap('showImgAC',0);""><img src=""http://i.a.cnn.net/cnn/.element/img/1.5/ceiling/shows/ac.gif"" alt="""" width=""111"" height=""6"" hspace=""0"" vspace=""0"" border=""0"" id=""showImgAC""></a></td>
											<td class=""spacer""></td>
											<td><a href=""/CNN/Programs/nancy.grace/"" onmouseover=""cnnShowImgSwap('showImgNG',1);"" onmouseout=""cnnShowImgSwap('showImgNG',0);""><img src=""http://i.a.cnn.net/cnn/.element/img/1.5/ceiling/shows/ng.gif"" alt="""" width=""64"" height=""6"" hspace=""0"" vspace=""0"" border=""0"" id=""showImgNG""></a></td>
											<td class=""spacer""></td>
											<td class=""RightEnd""><a href=""/CNN/Programs/"" onmouseover=""cnnShowImgSwap('showImgSC',1);"" onmouseout=""cnnShowImgSwap('showImgSC',0);""><img src=""http://i.a.cnn.net/cnn/.element/img/1.5/ceiling/shows/sched.gif"" alt="""" width=""54"" height=""6"" hspace=""0"" vspace=""0"" border=""0"" id=""showImgSC""></a></td>
										</tr>
									</table>
									<div class=""cnnMember"">
										<script language=""javascript"" type=""text/javascript"">
											if (typeof(CNN_returnUserName) != ""undefined"") {
												CNN_returnUserName('firstName');
											} else {
												document.write('Member Center: <a href=""http://audience.cnn.com/services/cnn/memberservices/regwall/member_profile.jsp?source=cnn"">Sign In<\/a> | <a href=""http://audience.cnn.com/services/cnn/memberservices/member_register.jsp?pid=&source=cnn&url=http%3A%2F%2Faudience.cnn.com%2Fservices%2Fcnn%2Fmemberservices%2Fregwall%2Fmember_profile.jsp%3Fsource%3Dcnn"">Register<\/a>');
											}
										</script>
										<noscript>
											Member Center: <a href=""http://audience.cnn.com/services/cnn/memberservices/regwall/member_profile.jsp?source=cnn"">Sign In</a> | <a href=""http://audience.cnn.com/services/cnn/memberservices/member_register.jsp?pid=&source=cnn&url=http%3A%2F%2Faudience.cnn.com%2Fservices%2Fcnn%2Fmemberservices%2Fregwall%2Fmember_profile.jsp%3Fsource%3Dcnn"">Register</a>
										</noscript>

									</div>
								</td>
							</tr>
						</table>
						<div class=""cnn8pxLpad""><img src=""http://i.a.cnn.net/cnn/.element/img/1.5/ceiling/cnnCeildivider.gif"" alt="""" width=""962"" height=""2"" hspace=""0"" vspace=""0"" border=""0""></div>
						<!--/ceiling.html-->
						<!--searchbar.txt-->
						<table width=""978"" cellpadding=""0"" cellspacing=""0"" border=""0"" id=""cnnCeilSearch"">
							<tr valign=""middle"">
								<td><div class=""cnnNoWrap""><form action=""http://search.cnn.com/cnn/search"" method=""get"" onsubmit=""return CNN_validateSearchForm(this);""><input type=""hidden"" name=""source"" value=""cnn""><input type=""hidden"" name=""invocationType"" value=""search/top""><img src=""http://i.a.cnn.net/cnn/.element/img/1.5/ceiling/search/hdr_search.gif"" width=""71"" height=""14"" alt=""Search"" class=""cnnSrch""><input type=""radio"" name=""sites"" value=""web"" checked class=""cnnR""><img src=""http://i.a.cnn.net/cnn/.element/img/1.5/ceiling/search/hdr_the_web.gif"" width=""39"" height=""6"" alt="""" class=""cnnWeb""><input type=""radio"" name=""sites"" value=""cnn"" class=""cnnR""><img src=""http://i.a.cnn.net/cnn/.element/img/1.5/ceiling/search/hdr_cnn_com.gif"" width=""39"" height=""6"" alt="""" class=""cnnCNN""><input type=""text"" name=""query"" value="""" title=""Enter text to search for and click 'Search'"" size=""30"" maxlength=""40"" class=""cnnInput""><input type=""submit"" value=""Search"" class=""cnnFormButtonSearch""><img src=""http://i.a.cnn.net/cnn/.element/img/1.5/ceiling/search/hdr_yahoo.gif"" width=""164"" height=""13"" alt="""" class=""cnnYahoo""></form></div></td>
							</tr>
						</table>
						<div class=""cnnSearchBot""><img src=""http://i.a.cnn.net/cnn/images/1.gif"" width=""1"" height=""1"" hspace=""0"" vspace=""0"" border=""0"" alt=""""></div>
						<!--searchbar.txt-->
						<div class=""cnnCeilIntlEd""><a href=""/linkto/intl.html"">International Edition</a><img src=""http://i.a.cnn.net/cnn/.element/img/1.5/ceiling/circle.divider.gif"" border=""0"" alt="""" height=""6"" width=""6"" style=""margin:0px 8px;""><a href=""/pipeline/""><img src=""http://i.a.cnn.net/cnn/.element/img/1.5/ceiling/logo.pipeline.gif"" width=""70"" height=""19"" border=""0"" alt="""" style=""vertical-align:text-top;margin-top:-2px;""></a></div>
					</div>
				</td>
				<td class=""sides""></td>
			</tr>
		</table>
		<!--ceiling nav-->
		<div id=""cnnBreakingNewsBanner"">
		<script type=""text/javascript"">
		cnnAddCSI('cnnBreakingNewsBanner','/.element/ssi/www/breaking_news/1.5/banner.exclude.html');
		</script></div>
		<script type=""text/javascript"">
		cnnPreloadImages('http://i.a.cnn.net/cnn/.element/img/1.5/ceiling/nav.rt.end.bg_over.gif');
		</script>
		<div class=""cnnCeilnavCont"">
			<table cellspacing=""0"" cellpadding=""0"" border=""0"" class=""cnnCeilnav"">
				<tr valign=""middle"" height=""22"">
					<td class=""cnnNavLeft""><img src=""http://i.a.cnn.net/cnn/.element/img/1.5/ceiling/nav_left_end_red.gif"" alt="""" width=""4"" height=""22"" hspace=""0"" vspace=""0"" border=""0""></td>
					<td class=""cnnNavHome""><a href=""/"">Home</a></td>
					<td onMouseOver=""CNN_navHor(this,1,1)"" onMouseOut=""CNN_navHor(this,0,1)""><a href=""/WORLD/"">World</a></td>
					<td onMouseOver=""CNN_navHor(this,1,1)"" onMouseOut=""CNN_navHor(this,0,1)""><a href=""/US/"">U.S.</a></td>
					<td onMouseOver=""CNN_navHor(this,1,1)"" onMouseOut=""CNN_navHor(this,0,1)""><a href=""/WEATHER/"">Weather</a></td>
					<td onMouseOver=""CNN_navHor(this,1,1)"" onMouseOut=""CNN_navHor(this,0,1)""><a href=""http://money.cnn.com/index.html?cnn=yes"">Business</a></td>
					<td onMouseOver=""CNN_navHor(this,1,1)"" onMouseOut=""CNN_navHor(this,0,1)""><a href=""/si/"">Sports</a></td>
					<td onMouseOver=""CNN_navHor(this,1,1)"" onMouseOut=""CNN_navHor(this,0,1)""><a href=""/time/index.html?cnn=yes"">Analysis</a></td>
					<td onMouseOver=""CNN_navHor(this,1,1)"" onMouseOut=""CNN_navHor(this,0,1)""><a href=""/POLITICS/"">Politics</a></td>
					<td onMouseOver=""CNN_navHor(this,1,1)"" onMouseOut=""CNN_navHor(this,0,1)""><a href=""/LAW/"">Law</a></td>
					<td onMouseOver=""CNN_navHor(this,1,1)"" onMouseOut=""CNN_navHor(this,0,1)""><a href=""/TECH/"">Tech</a></td>
					<td onMouseOver=""CNN_navHor(this,1,1)"" onMouseOut=""CNN_navHor(this,0,1)""><a href=""/TECH/space/"">Science</a></td>
					<td onMouseOver=""CNN_navHor(this,1,1)"" onMouseOut=""CNN_navHor(this,0,1)""><a href=""/HEALTH/"">Health</a></td>
					<td onMouseOver=""CNN_navHor(this,1,1)"" onMouseOut=""CNN_navHor(this,0,1)""><a href=""/SHOWBIZ/"">Entertainment</a></td>
					<td onMouseOver=""CNN_navHor(this,1,1)"" onMouseOut=""CNN_navHor(this,0,1)""><a href=""/offbeat/"">Offbeat</a></td>
					<td onMouseOver=""CNN_navHor(this,1,1)"" onMouseOut=""CNN_navHor(this,0,1)""><a href=""/TRAVEL/"">Travel</a></td>
					<td onMouseOver=""CNN_navHor(this,1,1)"" onMouseOut=""CNN_navHor(this,0,1)""><a href=""/EDUCATION/"">Education</a></td>
					<td onMouseOver=""CNN_navHor(this,1,1)"" onMouseOut=""CNN_navHor(this,0,1)""><a href=""/SPECIALS/"">Specials</a></td>
					<td onMouseOver=""CNN_navHor(this,1,1)"" onMouseOut=""CNN_navHor(this,0,1)""><a href=""/AUTOS/"">Autos</a></td>
					<td class=""cnnNavHorRtBg"" onMouseOver=""this.style.backgroundPosition = 'bottom right';"" onMouseOut=""this.style.backgroundPosition = 'top right';""><a href=""/exchange/"">Exchange</a></td>
					</tr>
				</table>
			</div>
		<!--/ceiling nav-->


</div> <div id=""CNN_homeTop""><div class=""CNN_homeTime""><b>UPDATED:</b> 2:54&nbsp;p.m.&nbsp;EDT,&nbsp;September 28, 2006</div><div class=""CNN_homeIntl""><a href=""javascript:CNN_openPopup('/feedback/help/homepage/frameset.exclude.html','620x364','toolbar=no,location=no,directories=no,status=no,menubar=no,scrollbars=auto,resizable=no,width=620,height=430');"">Make CNN Your Home Page</a></div></div><div id=""CNN_homeContainer""><div id=""CNN_Float2ColLeft""><div id=""CNN_homeLeftCol""><div id=""CNN_t1"">    <div><a href=""/2006/US/09/28/school.shooting/index.html""><img src=""http://i.a.cnn.net/cnn/2006/US/09/28/school.shooting/newt1.school.06.thurs.ap.jpg"" width=""306"" height=""245"" alt=""Girls' screams forced SWAT to storm classroom"" border=""0"" hspace=""0"" vspace=""0""></a></div><div class=""CNN_homeBox""><h2><a href=""/2006/US/09/28/school.shooting/index.html"">Girls' screams forced SWAT to storm classroom</a></h2><p>A gunman who invaded a Colorado high school used a 16-year-old girl as a shield and shot her before killing himself, according to the Colorado Department of Public Safety. Police say they heard the hostages screaming before storming the classroom. The suspect  &quot;traumatized and assaulted&quot; his hostages and the attack &quot;was of a sexual nature,&quot; said Sheriff Fred Wegener.</p><p><a href=""/2006/US/09/28/school.shooting/index.html"" class=""cnnT1"">DEVELOPING STORY</a></p><p><div class=""cnnT1Bullets"">&#8226;&nbsp;<a href=""javascript:cnnVideo('play','/video/us/2006/09/28/sot.co.school.shooting.police.kmgh','2006/10/05');"">School shooting suspect identified</a> <a href=""javascript:cnnVideo('play','/video/us/2006/09/28/sot.co.school.shooting.police.kmgh','2006/10/05');""><img src=""http://i.a.cnn.net/cnn/.element/img/1.5/main/icon_video.gif"" alt=""Video"" width=""19"" height=""12"" vspace=""1"" hspace=""0"" border=""0"" class=""cnnVideoIcon""></a><br></div><div class=""cnnT1Bullets"">&#8226;&nbsp;<a href=""/2006/US/09/28/keyes.profile/index.html"">Town mourns 'sweet girl'</a> | <a href=""javascript:CNN_openPopup('/interactive/us/0609/gallery.school.hostages/frameset.exclude.html','620x430','toolbar=no,location=no,directories=no,status=no,menubar=no,scrollbars=no,resizable=no,width=620,height=430');"">Gallery</a><br></div></p></div></div><style type=""text/css"">
<!--  #cnnIreportBox
{margin-top:18px;} #cnnIreportBox TD
{font-size:12px;}  #cnnIreportBox TR
{vertical-align:top;}
.cnnSplitLk
{width:980px;}
-->
</style>

<div id=""cnnIreportBox"">
	<div><img src=""http://i.a.cnn.net/cnn/.element/img/1.3/misc/dotted.line.gif"" style=""margin-bottom:6px;"" width=""306"" height=""2"" border=""0"" alt=""""></div>

	<table cellspacing=""0"" cellpadding=""0"" border=""0"" width=""306"">

	<tr>
		<td rowspan=""3"" style=""padding:0 3px 0 0;""><a href=""/exchange""><img src=""http://i.a.cnn.net/cnn/.element/img/1.3/misc/ireport.gif"" alt=""iReport"" width=""33"" height=""46"" border=""0""></a></td>
		<td colspan=""2""><img src=""http://i.a.cnn.net/cnn/.element/img/1.3/misc/send.hdr.gif"" width=""255"" height=""13"" style=""margin-bottom:2px;"" alt=""Send, Share, See YOUR stories on CNN""></td>
	</tr>

	

	<tr>
		<td width=""26""><img src=""http://i.a.cnn.net/cnn/.element/img/1.3/misc/see.gif"" width=""26"" style=""margin-top:2px;"" height=""9"" border=""0"" alt=""""></td>
		<td width=""280"" style=""padding:0 0 0 3px;font-size:12px;""><img src=""http://i.a.cnn.net/cnn/.element/img/1.5/main/icon_video.gif""> <a href=""javascript:cnnVideo('play','/video/showbiz/2006/09/28/daily.show.i.report.come','2006/10/05');"">Jon Stewart's I-Report</a></td>
	</tr>
		<tr>
		<td width=""26"" height=""12""><img src=""http://i.a.cnn.net/cnn/.element/img/1.3/misc/send.gif"" width=""26"" style=""margin-top:2px;"" height=""9"" border=""0"" alt=""""></td>
		<td width=""280"" style=""padding:0 0 0 3px;font-size:12px;""><a href=""/exchange/ireports/topics/index.html "">What's happening where you are?</a></td>
	</tr>
	

	</table>
</div>


    <div class=""cnn20pxTMargin""><!-- ADSPACE: home/left.306x60 --><div align=""center"" style=""padding: 0; margin: 0; border: 0;""><script type=""text/javascript""> 

cnnad_createAd(""871815"",""http://ads.cnn.com/html.ng/site=cnn&cnn_position=306x60_lft&cnn_rollup=homepage&params.styles=fs"",""60"",""306"");

</script></div></div></div><div id=""CNN_homeCenterCol""><div class=""cnn321pxBlock""><div class=""CNN_homeBox"">			<div class=""CNN_homeBoxHeader""><img src=""http://i.a.cnn.net/cnn/.element/img/1.5/main/hdr_latest_news.gif"" alt=""Latest News"" width=""89"" height=""10""></div>
<div id=""cnnTopStoriesModule""><div class=""cnnTabBox""><table cellpadding=""0"" cellspacing=""0"" border=""0"" width=""300""><tr valign=""top""><td><img src=""http://i.a.cnn.net/cnn/.element/img/1.5/main/hdr_end.gif"" alt="""" width=""1"" height=""26"" border=""0""></td><td class=""cnntabLeft""></td><td><img src=""http://i.a.cnn.net/cnn/.element/img/1.4/main/biz/tab_left.gif"" alt="""" width=""2"" height=""26"" border=""0""></td><td class=""cnntabLeftCont""><img src=""http://i.a.cnn.net/cnn/.element/img/1.5/main/tabs/txt_top_stories_blue_wt.gif"" alt=""txt_top_stories_blue_wt.gif"" width=""58"" height=""8""/></td><td><img src=""http://i.a.cnn.net/cnn/.element/img/1.4/main/biz/tab_right.gif"" alt="""" width=""2"" height=""26"" border=""0""></td><td class=""cnntabRightCont"" onmouseover=""cnnImgSwap('MostPop',1);"" onmouseout=""cnnImgSwap('MostPop',0);"" onclick=""CNN_flipModule('cnnMPModule','cnnTopStoriesModule');cnnImgSwap('TopStories',0);""><img src=""http://i.a.cnn.net/cnn/.element/img/1.5/main/tabs/mostpop.gif"" alt=""txt_most_pop_grey.gif"" width=""70"" height=""8"" id=""MostPop""/></td><td><img src=""http://i.a.cnn.net/cnn/.element/img/1.4/main/biz/hdr_line.gif"" alt="""" width=""1"" height=""26"" border=""0""></td><td class=""cnntabRight""></td><td><img src=""http://i.a.cnn.net/cnn/.element/img/1.5/main/hdr_end.gif"" alt="""" width=""1"" height=""26"" border=""0""></td></tr></table></div><ul><li><b></b><a href=""/2006/US/09/28/deputies.shot.ap/index.html"">Manhunt on in police shooting</a> <br></li><li><b><span class=""cnnWOOL"">SI.com: </span></b><a href=""/si/2006/football/nfl/09/28/bc.fbn.cowboys.t.o.ap/index.html?cnn=yes"">T.O.: Suicide saga a 'misunderstanding'</a> <br></li><li><b><span class=""cnnWOOL"">CNNMoney: </span></b><a href=""http://money.cnn.com/2006/09/28/markets/markets_1130/index.htm?cnn=yes"">Dow flirts with record highs again</a> <br></li><li><b></b><a href=""/2006/WORLD/meast/09/28/iraq.main/index.html"">Kidnap Christians, urges purported al Qaeda tape</a> <br></li><li><b><span class=""cnnWOOL"">CNNMoney: </span></b><a href=""http://money.cnn.com/2006/09/28/technology/hp_hearing/index.htm?cnn=yes"">Ex-HP exec grilled</a>  | <a href=""javascript:cnnVideo('live','3');""></a> <a href=""javascript:cnnVideo('live','3');"">Now</a> <a href=""javascript:cnnVideo('live','3');""><script type=""text/javascript"">cnnInsertPipelineIcon('3')</script></a><br></li><li><b></b><a href=""/2006/TECH/space/09/28/mars.opportunity/index.html"">Mars rover reaches new milestone</a>  | <a href=""javascript:cnnVideo('play','/video/tech/2006/09/28/callas.mars.rover.crater.nasa','2006/10/05');"">Video</a> <a href=""javascript:cnnVideo('play','/video/tech/2006/09/28/callas.mars.rover.crater.nasa','2006/10/05');""><img src=""http://i.a.cnn.net/cnn/.element/img/1.5/main/icon_video.gif"" alt=""Video"" width=""19"" height=""12"" vspace=""1"" hspace=""0"" border=""0"" class=""cnnVideoIcon""></a><br></li><li><b></b> <a href=""javascript:cnnVideo('play','/video/us/2006/09/28/johnson.ca.beware.of.squirrels.kgo','2006/10/05');"">Squirrel jumps boy in park; rabies suspected</a> <a href=""javascript:cnnVideo('play','/video/us/2006/09/28/johnson.ca.beware.of.squirrels.kgo','2006/10/05');""><img src=""http://i.a.cnn.net/cnn/.element/img/1.5/main/icon_video.gif"" alt=""Video"" border=""0"" width=""19"" height=""12"" class=""cnnVideoIcon""></a><br></li><li><b></b> <a href=""javascript:cnnVideo('play','/video/world/2006/09/28/ware.iraq.3.8.marines.cnn','2006/10/05');"">Bullets never stop in frontline view of gunfight</a> <a href=""javascript:cnnVideo('play','/video/world/2006/09/28/ware.iraq.3.8.marines.cnn','2006/10/05');""><img src=""http://i.a.cnn.net/cnn/.element/img/1.5/main/icon_video.gif"" alt=""Video"" border=""0"" width=""19"" height=""12"" class=""cnnVideoIcon""></a><br></li><li><b></b><a href=""/2006/HEALTH/09/28/dentist.coma.ap/index.html"">Girl left comatose after dental visit dies </a>  | <a href=""javascript:cnnVideo('play','/video/health/2006/09/28/lukadis.il.dentist.coma.death.wfld','2006/10/05');"">Video</a> <a href=""javascript:cnnVideo('play','/video/health/2006/09/28/lukadis.il.dentist.coma.death.wfld','2006/10/05');""><img src=""http://i.a.cnn.net/cnn/.element/img/1.5/main/icon_video.gif"" alt=""Video"" width=""19"" height=""12"" vspace=""1"" hspace=""0"" border=""0"" class=""cnnVideoIcon""></a><br></li><li><b></b> <a href=""javascript:cnnVideo('play','/video/us/2006/09/28/emerald.porn.star.fogernor.kgtv','2006/10/05');"">Porn star candidate campaigns on campus</a> <a href=""javascript:cnnVideo('play','/video/us/2006/09/28/emerald.porn.star.fogernor.kgtv','2006/10/05');""><img src=""http://i.a.cnn.net/cnn/.element/img/1.5/main/icon_video.gif"" alt=""Video"" border=""0"" width=""19"" height=""12"" class=""cnnVideoIcon""></a><br></li><li><b></b><a href=""/2006/SHOWBIZ/TV/09/28/television.sheen.reut/index.html"">Charlie Sheen to become highest-paid sitcom star</a> <br></li><li><b></b> <a href=""javascript:cnnVideo('play','/video/showbiz/2006/09/28/daily.show.i.report.come','2006/10/05');"">Jon Stewart's I-Report makes him a hottie</a> <a href=""javascript:cnnVideo('play','/video/showbiz/2006/09/28/daily.show.i.report.come','2006/10/05');""><img src=""http://i.a.cnn.net/cnn/.element/img/1.5/main/icon_video.gif"" alt=""Video"" border=""0"" width=""19"" height=""12"" class=""cnnVideoIcon""></a><br></li>
<li><b><span class=""cnnWOOL"">CNN Wire: </span></b><a href=""/linkto/news.update.html"">Latest updates on world's top stories</a></span></li>

</ul></div><div id=""cnnMPModule""><div class=""cnnTabBox""><table cellpadding=""0"" cellspacing=""0"" border=""0"" width=""300""><tr valign=""top""><td><img src=""http://i.a.cnn.net/cnn/.element/img/1.5/main/hdr_end.gif"" alt="""" width=""1"" height=""26"" border=""0""></td><td class=""cnntabLeft""></td><td><img src=""http://i.a.cnn.net/cnn/.element/img/1.4/main/biz/hdr_line.gif"" alt="""" width=""1"" height=""26"" border=""0""></td><td class=""cnntabLeftCont"" onmouseover=""cnnImgSwap('TopStories',1);"" onmouseout=""cnnImgSwap('TopStories',0);"" onclick=""CNN_flipModule('cnnTopStoriesModule','cnnMPModule');cnnImgSwap('MostPop',0);""><img src=""http://i.a.cnn.net/cnn/.element/img/1.5/main/tabs/topstories.gif"" alt=""Pipeline"" width=""58"" height=""8"" border=""0"" id=""TopStories"" /></td><td><img src=""http://i.a.cnn.net/cnn/.element/img/1.4/main/biz/tab_left.gif"" alt="""" width=""2"" height=""26"" border=""0""></td><td class=""cnntabRightCont""><img src=""http://i.a.cnn.net/cnn/.element/img/1.5/main/tabs/txt_most_pop_blue_wt.gif"" alt=""txt_most_pop_blue_wt.gif"" width=""70"" height=""8""/></td><td><img src=""http://i.a.cnn.net/cnn/.element/img/1.4/main/biz/tab_right.gif"" alt="""" width=""2"" height=""26"" border=""0""></td><td class=""cnntabRight""></td><td><img src=""http://i.a.cnn.net/cnn/.element/img/1.5/main/hdr_end.gif"" alt="""" width=""1"" height=""26"" border=""0""></td></tr></table></div><?xml version=""1.0"" encoding=""UTF-8""?>
<ol class=""cnnMostPopular""><li><a href=""/2006/US/09/28/school.shooting/index.html"">School attack of 'sexual nature'</a></li><li><a href=""/2006/US/09/28/deputies.shot.ap/index.html"">Fla. manhunt for cop shooter</a></li><li><a href=""/2006/HEALTH/09/28/dentist.coma.ap/index.html"">Girl dies after dentist visit</a></li><li><a href=""/2006/SHOWBIZ/TV/09/28/people.amandapeet.ap/index.html"">Peet pregnant, getting married</a></li><li><a href=""/2006/SHOWBIZ/TV/09/28/television.sheen.reut/index.html"">Sheen set to become highest-paid sitcom star</a></li><li><a href=""/2006/TECH/space/09/28/mars.opportunity/index.html"">What an Opportunity! Mars rover reaches new milestone</a></li><li><a href=""/2006/WORLD/meast/09/28/iraq.main/index.html"">Tape urges kidnappings in Iraq</a></li><li><a href=""/2006/LAW/09/28/inmate.tattoo.ap/index.html"">Inmate got tattoo of victim's name</a></li><li><a href=""/2006/LAW/09/28/dyleski.profile/index.html"">Teen took pleasure in killing</a></li><li><a href=""/2006/SHOWBIZ/TV/09/28/tv.dexter.ap/index.html"">Serial killer fights crime his way</a></li></ol><div class=""cnn20pxTMargin""><div id=""cnnTopStoriesFooter""><div id=""cnnTopStoriesFooterBlurb"" style=""font-size:11px;color:#666;"">Most read stories. Updated every 20 minutes. <a href=""/mostpopular/"">View Details</a></div></div></div></div></div></div> <div class=""CNN_homeBox""><div class=""CNN_homeBoxHeader""><img src=""http://i.a.cnn.net/cnn/.element/img/1.5/main/icon_video.gif"" alt=""icon_video.gif"" width=""19"" height=""12"" class=""cnnIconWt""/><img src=""http://i.a.cnn.net/cnn/.element/img/1.5/main/hdr_watch_vid.gif"" alt=""hdr_watch_vid.gif"" width=""123"" height=""10"" /></div><div id=""cnnMPBestFreeVideoModule""><div class=""cnnTabBox""><table cellpadding=""0"" cellspacing=""0"" border=""0"" width=""300""><tr valign=""top""><td><img src=""http://i.a.cnn.net/cnn/.element/img/1.5/main/hdr_end.gif"" alt="""" width=""1"" height=""26"" border=""0""></td><td class=""cnntabLeft""></td><td><img src=""http://i.a.cnn.net/cnn/.element/img/1.4/main/biz/tab_left.gif"" alt="""" width=""2"" height=""26"" border=""0""></td><td class=""cnntabLeftCont""><img src=""http://i.a.cnn.net/cnn/.element/img/1.5/main/tabs/txt_most_pop_blue_wt.gif"" alt=""txt_most_pop_blue_wt.gif"" width=""70"" height=""8""/></td><td><img src=""http://i.a.cnn.net/cnn/.element/img/1.4/main/biz/tab_right.gif"" alt="""" width=""2"" height=""26"" border=""0""></td><td class=""cnntabRightCont"" onmouseover=""cnnImgSwap('BestVideo',1);"" onmouseout=""cnnImgSwap('BestVideo',0);"" onclick=""CNN_flipModule('cnnBestFreeVideoModule','cnnMPBestFreeVideoModule');cnnImgSwap('MostPopVid',0);""><img src=""http://i.a.cnn.net/cnn/.element/img/1.5/main/tabs/bestvideo.gif"" alt=""txt_best_video_grey.gif"" width=""54"" height=""8"" id=""BestVideo""/></td><td><img src=""http://i.a.cnn.net/cnn/.element/img/1.4/main/biz/hdr_line.gif"" alt="""" width=""1"" height=""26"" border=""0""></td><td class=""cnntabRight""></td><td><img src=""http://i.a.cnn.net/cnn/.element/img/1.5/main/hdr_end.gif"" alt="""" width=""1"" height=""26"" border=""0""></td></tr></table></div><div id=""cnnFreevideoMPModule"">
<div class=""cnnPipelineCollapsed"" id=""freeVidMPCntr1"">
<div class=""cnnPipelineHead"">
<div class=""cnnPipelineHeadlineBar"" id=""cnnfreeVidMPHeadlineBar1"">
<img title=""Expand"" alt=""Expand"" class=""cnnPlayListPlusBtn"" border=""0"" height=""14"" width=""14"" src=""http://i.a.cnn.net/cnn/.element/img/1.5/main/video/plus.gif""><img title=""Collapse"" alt=""Collapse"" class=""cnnPlayListMinusBtn"" border=""0"" height=""14"" width=""14"" src=""http://i.a.cnn.net/cnn/.element/img/1.5/main/video/minus.gif""></div>
<span id=""cnnfreeVidMPHeadline1"" onclick=""cnnVideo('play','/video/us/2006/09/28/emerald.porn.star.fogernor.kgtv');""><em class=""cnnPipelineModuleWool"">1. </em>Porn actress for governor?<em class=""cnnPipelineModuleTRT""> (1:27)</em></span>
<br>
</div>
<div class=""cnnPipeLineTeaseImage"">
<a href=""javascript:cnnVideo('play','/video/us/2006/09/28/emerald.porn.star.fogernor.kgtv');""><img border=""0"" height=""49"" width=""88"" src=""http://i.cnn.net/cnn/images/1.gif"" lowsrc=""http://i.a.cnn.net/cnn/video/us/2006/09/28/emerald.fp.affl.jpg"" id=""freeVidMPCntr1Image"" alt=""Porn actress for governor?""></a>
</div>
<div class=""cnnPipelineBlurb"" id=""cnnfreeVidMPBlurb1"">
<p>Adult film actress Mary Carey runs for California governor again. Affiliate KGTV reports. (September 28)</p>
</div>
</div>
<div class=""cnnPipelineCollapsed"" id=""freeVidMPCntr2"">
<div class=""cnnPipelineHead"">
<div class=""cnnPipelineHeadlineBar"" id=""cnnfreeVidMPHeadlineBar2"">
<img title=""Expand"" alt=""Expand"" class=""cnnPlayListPlusBtn"" border=""0"" height=""14"" width=""14"" src=""http://i.a.cnn.net/cnn/.element/img/1.5/main/video/plus.gif""><img title=""Collapse"" alt=""Collapse"" class=""cnnPlayListMinusBtn"" border=""0"" height=""14"" width=""14"" src=""http://i.a.cnn.net/cnn/.element/img/1.5/main/video/minus.gif""></div>
<span id=""cnnfreeVidMPHeadline2"" onclick=""cnnVideo('play','/video/bestoftv/2006/09/27/lkl.sot.ashton.kutcher.thu.cnn');""><em class=""cnnPipelineModuleWool"">2. </em>Seacrest nearly Punk'd<em class=""cnnPipelineModuleTRT""> (1:09)</em></span>
<br>
</div>
<div class=""cnnPipeLineTeaseImage"">
<a href=""javascript:cnnVideo('play','/video/bestoftv/2006/09/27/lkl.sot.ashton.kutcher.thu.cnn');""><img border=""0"" height=""49"" width=""88"" src=""http://i.cnn.net/cnn/images/1.gif"" lowsrc=""http://i.a.cnn.net/cnn/video/bestoftv/2006/09/27/lkl.kutcher.preview.fp.jpg"" id=""freeVidMPCntr2Image"" alt=""Seacrest nearly Punk'd""></a>
</div>
<div class=""cnnPipelineBlurb"" id=""cnnfreeVidMPBlurb2"">
<p>This is a behind the scenes moment caught on tape when Ryan Seacrest was guest host of Larry King Live.    (Sep ...</p>
</div>
</div>
<div class=""cnnPipelineCollapsed"" id=""freeVidMPCntr3"">
<div class=""cnnPipelineHead"">
<div class=""cnnPipelineHeadlineBar"" id=""cnnfreeVidMPHeadlineBar3"">
<img title=""Expand"" alt=""Expand"" class=""cnnPlayListPlusBtn"" border=""0"" height=""14"" width=""14"" src=""http://i.a.cnn.net/cnn/.element/img/1.5/main/video/plus.gif""><img title=""Collapse"" alt=""Collapse"" class=""cnnPlayListMinusBtn"" border=""0"" height=""14"" width=""14"" src=""http://i.a.cnn.net/cnn/.element/img/1.5/main/video/minus.gif""></div>
<span id=""cnnfreeVidMPHeadline3"" onclick=""cnnVideo('play','/video/showbiz/2006/09/28/daily.show.i.report.come');""><em class=""cnnPipelineModuleWool"">3. </em>Jon Stewart: 'I-Report for CNN'<em class=""cnnPipelineModuleTRT""> (2:48)</em></span>
<br>
</div>
<div class=""cnnPipeLineTeaseImage"">
<a href=""javascript:cnnVideo('play','/video/showbiz/2006/09/28/daily.show.i.report.come');""><img border=""0"" height=""49"" width=""88"" src=""http://i.cnn.net/cnn/images/1.gif"" lowsrc=""http://i.a.cnn.net/cnn/video/showbiz/2006/09/28/daily.fp.affl.jpg"" id=""freeVidMPCntr3Image"" alt=""Jon Stewart: 'I-Report for CNN'""></a>
</div>
<div class=""cnnPipelineBlurb"" id=""cnnfreeVidMPBlurb3"">
<p>""The Daily Show"" host jumps on the I-Report bandwagon. (September 28)</p>
</div>
</div>
<div class=""cnnPipelineCollapsed"" id=""freeVidMPCntr4"">
<div class=""cnnPipelineHead"">
<div class=""cnnPipelineHeadlineBar"" id=""cnnfreeVidMPHeadlineBar4"">
<img title=""Expand"" alt=""Expand"" class=""cnnPlayListPlusBtn"" border=""0"" height=""14"" width=""14"" src=""http://i.a.cnn.net/cnn/.element/img/1.5/main/video/plus.gif""><img title=""Collapse"" alt=""Collapse"" class=""cnnPlayListMinusBtn"" border=""0"" height=""14"" width=""14"" src=""http://i.a.cnn.net/cnn/.element/img/1.5/main/video/minus.gif""></div>
<span id=""cnnfreeVidMPHeadline4"" onclick=""cnnVideo('play','/video/us/2006/09/28/johnson.ca.beware.of.squirrels.kgo');""><em class=""cnnPipelineModuleWool"">4. </em>When good squirrels go bad<em class=""cnnPipelineModuleTRT""> (1:33)</em></span>
<br>
</div>
<div class=""cnnPipeLineTeaseImage"">
<a href=""javascript:cnnVideo('play','/video/us/2006/09/28/johnson.ca.beware.of.squirrels.kgo');""><img border=""0"" height=""49"" width=""88"" src=""http://i.cnn.net/cnn/images/1.gif"" lowsrc=""http://i.a.cnn.net/cnn/video/us/2006/09/28/johnson.fp.kgo.jpg"" id=""freeVidMPCntr4Image"" alt=""When good squirrels go bad""></a>
</div>
<div class=""cnnPipelineBlurb"" id=""cnnfreeVidMPBlurb4"">
<p>A California park warns about squirrels attacking people. KGO's Carolyn Johnson reports (September 29)</p>
</div>
</div>
</div>
<div id=""cnnPipelineMPFooter"">
<div id=""cnnPipelineMPFooterBlurb"">
<b><a href=""javascript:cnnVideo('browse','/mostwatched');"">More most watched video.</a></b> Updated every 2 hours.
					<div class=""cnn4pxTpad"">
<a href=""/video/"">More CNN.com video</a> | <a href=""javascript:cnnVideo('browse','/mostwatched');"">Browse player</a>
</div>
</div>
</div>
</div><div id=""cnnBestFreeVideoModule""><div class=""cnnTabBox""><table cellpadding=""0"" cellspacing=""0"" border=""0"" width=""300""><tr valign=""top""><td><img src=""http://i.a.cnn.net/cnn/.element/img/1.5/main/hdr_end.gif"" alt="""" width=""1"" height=""26"" border=""0""></td><td class=""cnntabLeft""></td><td><img src=""http://i.a.cnn.net/cnn/.element/img/1.4/main/biz/hdr_line.gif"" alt="""" width=""1"" height=""26"" border=""0""></td><td class=""cnntabLeftCont"" onmouseover=""cnnImgSwap('MostPopVid',1);"" onmouseout=""cnnImgSwap('MostPopVid',0);"" onclick=""CNN_flipModule('cnnMPBestFreeVideoModule','cnnBestFreeVideoModule');cnnImgSwap('BestVideo',0);""><img src=""http://i.a.cnn.net/cnn/.element/img/1.5/main/tabs/mostpop.gif"" alt=""txt_most_pop_grey.gif"" width=""70"" height=""8"" id=""MostPopVid""/></td><td><img src=""http://i.a.cnn.net/cnn/.element/img/1.4/main/biz/tab_left.gif"" alt="""" width=""2"" height=""26"" border=""0""></td><td class=""cnntabRightCont""><img src=""http://i.a.cnn.net/cnn/.element/img/1.5/main/tabs/txt_best_video_blue_wt.gif"" alt=""txt_best_video_blue_wt.gif"" width=""54"" height=""8""/></td><td><img src=""http://i.a.cnn.net/cnn/.element/img/1.4/main/biz/tab_right.gif"" alt="""" width=""2"" height=""26"" border=""0""></td><td class=""cnntabRight""></td><td><img src=""http://i.a.cnn.net/cnn/.element/img/1.5/main/hdr_end.gif"" alt="""" width=""1"" height=""26"" border=""0""></td></tr></table></div><div id=""cnnFreevideoModule"">                                     <div id=""freeVidCntr1"" class=""cnnPipelineCollapsed""><div class=""cnnPipelineHead""><div class=""cnnPipelineHeadlineBar"" id=""cnnfreeVidHeadlineBar1""><img src=""http://i.a.cnn.net/cnn/.element/img/1.5/main/video/plus.gif"" width=""14"" height=""14"" border=""0"" class=""cnnPlayListPlusBtn"" alt=""Expand"" title=""Expand""><img src=""http://i.a.cnn.net/cnn/.element/img/1.5/main/video/minus.gif"" width=""14"" height=""14"" border=""0"" class=""cnnPlayListMinusBtn"" alt=""Collapse"" title=""Collapse""></div><span id=""cnnfreeVidHeadline1"" onclick=""cnnVideo('play','/video/us/2006/09/28/hamel.nh.clerk.fights.off.robber.wmur','2006/10/05');"">Clerk beats back robber <em class=""cnnPipelineModuleTRT"">(1:37)</em></span><br></div><div class=""cnnPipeLineTeaseImage""><a href=""javascript:cnnVideo('play','/video/us/2006/09/28/hamel.nh.clerk.fights.off.robber.wmur','2006/10/05');""><img src=""http://i.cnn.net/cnn/images/1.gif"" lowsrc=""http://i.a.cnn.net/cnn/video/us/2006/09/28/clerk.fp.wmur.jpg"" width=""88"" height=""49"" id=""freeVidCntr1Image"" alt=""Clerk beats back robber"" border=""0""></a></div><div id=""cnnfreeVidBlurb1"" class=""cnnPipelineBlurb""><p>A store clerk used a baseball bat to fend off a robber. WMUR's Heather Hamel reports. (September 28)</p></div></div><div id=""freeVidCntr2"" class=""cnnPipelineCollapsed""><div class=""cnnPipelineHead""><div class=""cnnPipelineHeadlineBar"" id=""cnnfreeVidHeadlineBar2""><img src=""http://i.a.cnn.net/cnn/.element/img/1.5/main/video/plus.gif"" width=""14"" height=""14"" border=""0"" class=""cnnPlayListPlusBtn"" alt=""Expand"" title=""Expand""><img src=""http://i.a.cnn.net/cnn/.element/img/1.5/main/video/minus.gif"" width=""14"" height=""14"" border=""0"" class=""cnnPlayListMinusBtn"" alt=""Collapse"" title=""Collapse""></div><span id=""cnnfreeVidHeadline2"" onclick=""cnnVideo('play','/video/showbiz/2006/09/28/daily.show.i.report.come','2006/10/05');"">Jon Stewart: 'I-Report for CNN' <em class=""cnnPipelineModuleTRT"">(2:48)</em></span><br></div><div class=""cnnPipeLineTeaseImage""><a href=""javascript:cnnVideo('play','/video/showbiz/2006/09/28/daily.show.i.report.come','2006/10/05');""><img src=""http://i.cnn.net/cnn/images/1.gif"" lowsrc=""http://i.a.cnn.net/cnn/video/showbiz/2006/09/28/daily.fp.affl.jpg"" width=""88"" height=""49"" id=""freeVidCntr2Image"" alt=""Jon Stewart: 'I-Report for CNN'"" border=""0""></a></div><div id=""cnnfreeVidBlurb2"" class=""cnnPipelineBlurb""><p>""The Daily Show"" host jumps on the I-Report bandwagon. (September 28)</p></div></div><div id=""freeVidCntr3"" class=""cnnPipelineCollapsed""><div class=""cnnPipelineHead""><div class=""cnnPipelineHeadlineBar"" id=""cnnfreeVidHeadlineBar3""><img src=""http://i.a.cnn.net/cnn/.element/img/1.5/main/video/plus.gif"" width=""14"" height=""14"" border=""0"" class=""cnnPlayListPlusBtn"" alt=""Expand"" title=""Expand""><img src=""http://i.a.cnn.net/cnn/.element/img/1.5/main/video/minus.gif"" width=""14"" height=""14"" border=""0"" class=""cnnPlayListMinusBtn"" alt=""Collapse"" title=""Collapse""></div><span id=""cnnfreeVidHeadline3"" onclick=""cnnVideo('play','/video/showbiz/2006/09/28/anderson.anna.nicole.baby.lkl','2006/10/05');"">The Anna Nicole sideshow <em class=""cnnPipelineModuleTRT"">(4:30)</em></span><br></div><div class=""cnnPipeLineTeaseImage""><a href=""javascript:cnnVideo('play','/video/showbiz/2006/09/28/anderson.anna.nicole.baby.lkl','2006/10/05');""><img src=""http://i.cnn.net/cnn/images/1.gif"" lowsrc=""http://i.a.cnn.net/cnn/video/showbiz/2006/09/28/anderson.fp.jpg"" width=""88"" height=""49"" id=""freeVidCntr3Image"" alt=""The Anna Nicole sideshow"" border=""0""></a></div><div id=""cnnfreeVidBlurb3"" class=""cnnPipelineBlurb""><p>Life, death and daddy drama define Anna Nicole Smith. Showbiz Tonight's Brooke Anderson reports (September 28)</p></div></div><div id=""freeVidCntr4"" class=""cnnPipelineCollapsed""><div class=""cnnPipelineHead""><div class=""cnnPipelineHeadlineBar"" id=""cnnfreeVidHeadlineBar4""><img src=""http://i.a.cnn.net/cnn/.element/img/1.5/main/video/plus.gif"" width=""14"" height=""14"" border=""0"" class=""cnnPlayListPlusBtn"" alt=""Expand"" title=""Expand""><img src=""http://i.a.cnn.net/cnn/.element/img/1.5/main/video/minus.gif"" width=""14"" height=""14"" border=""0"" class=""cnnPlayListMinusBtn"" alt=""Collapse"" title=""Collapse""></div><span id=""cnnfreeVidHeadline4"" onclick=""cnnVideo('play','/video/tech/2006/09/28/callas.mars.rover.crater.nasa','2006/10/05');"">'Opportunity' knocks <em class=""cnnPipelineModuleTRT"">(2:56)</em></span><br></div><div class=""cnnPipeLineTeaseImage""><a href=""javascript:cnnVideo('play','/video/tech/2006/09/28/callas.mars.rover.crater.nasa','2006/10/05');""><img src=""http://i.cnn.net/cnn/images/1.gif"" lowsrc=""http://i.a.cnn.net/cnn/video/tech/2006/09/28/callas.fp.nasa.jpg"" width=""88"" height=""49"" id=""freeVidCntr4Image"" alt=""'Opportunity' knocks"" border=""0""></a></div><div id=""cnnfreeVidBlurb4"" class=""cnnPipelineBlurb""><p>The Mars rover 'Opportunity' reaches a crater that may unlock the history of Mars. (September 28)</p></div></div><div id=""freeVidCntr5"" class=""cnnPipelineCollapsed""><div class=""cnnPipelineHead""> <div class=""cnnPipelineHeadlineBar"" id=""cnnfreeVidHeadlineBar5""><img src=""http://i.a.cnn.net/cnn/.element/img/1.5/main/video/plus.gif"" width=""14"" height=""14"" border=""0"" class=""cnnPlayListPlusBtn"" alt=""Expand"" title=""Expand""><img src=""http://i.a.cnn.net/cnn/.element/img/1.5/main/video/minus.gif"" width=""14"" height=""14"" border=""0"" class=""cnnPlayListMinusBtn"" alt=""Collapse"" title=""Collapse""></div><span id=""cnnfreeVidHeadline5"" onclick=""cnnVideo('play','/video/nitn/latest');"">Now In The News<em class=""cnnPipelineModuleTRT""> (Updated: 2:45 p.m. ET)</em></span><br></div><div class=""cnnPipeLineTeaseImage""><a href=""javascript:cnnVideo('play','/video/nitn/latest');""><img src=""http://i.cnn.net/cnn/images/1.gif"" lowsrc=""http://i.a.cnn.net/cnn/.element/img/1.3/pipeline/static/NITN_frame.jpg"" width=""88"" height=""49"" id=""freeVidCntr5Image"" alt=""Now in the News"" border=""0""></a></div><div id=""cnnfreeVidBlurb5"" class=""cnnPipelineBlurb""> <p>Your quick news update</p></div></div></div></div></div><div id=""cnnOnly""><div class=""cnn20pxTMargin""><div class=""CNN_homeBox""><div class=""CNN_homeBoxHeader""><img src=""http://i.a.cnn.net/cnn/.element/img/1.5/main/hdr_only_on_cnn.gif"" alt=""Only on CNN"" width=""84"" height=""10""></div><ul><li><b></b> <a href=""javascript:cnnVideo('play','/video/bestoftv/2006/09/27/lkl.sot.ashton.kutcher.thu.cnn','2006/10/05');"">Ashton Kutcher tells secret to Ryan Seacrest</a> <a href=""javascript:cnnVideo('play','/video/bestoftv/2006/09/27/lkl.sot.ashton.kutcher.thu.cnn','2006/10/05');""><img src=""http://i.a.cnn.net/cnn/.element/img/1.5/main/icon_video.gif"" alt=""Video"" border=""0"" width=""19"" height=""12"" class=""cnnVideoIcon""></a><br></li><li><b></b><a href=""/2006/US/09/26/Dobbs.Sept27/index.html"">Dobbs: Keep religion out of politics</a> <br></li><li><b></b> <a href=""javascript:cnnVideo('play','/video/world/2006/09/27/blitzer.karzai.intv.cnn','2006/10/04');"">Afghanistan's Karzai fires back at &quot;ostrich&quot; attack</a> <a href=""javascript:cnnVideo('play','/video/world/2006/09/27/blitzer.karzai.intv.cnn','2006/10/04');""><img src=""http://i.a.cnn.net/cnn/.element/img/1.5/main/icon_video.gif"" alt=""Video"" border=""0"" width=""19"" height=""12"" class=""cnnVideoIcon""></a><br></li></ul></div></div></div></div>  <div id=""CNN_2ColFooter"">
   <div class=""cnnRCPod"">
    <table cellpadding=""0"" cellspacing=""0"" border=""0"" width=""621"">
     <colgroup>
      <col width=""189"">
      <col width=""426"">
      <col width=""6"">
     </colgroup>
     <tr valign=""top"">
      <td><img src=""http://i.cnn.net/cnn/.element/img/1.5/main/podcasts/podcasts_radio.gif"" alt="""" width=""189"" height=""41"" hspace=""0"" vspace=""0"" border=""0""></td>
      <td class=""cnnRCPodLnks""><a href=""/services/podcasting/"">Get Podcasts</a> | <a href=""javascript:CNN_openPopup('/audio/radio/preferences.html','radioplayer','toolbar=no,location=no,directories=no,status=no,menubar=no,scrollbars=no,resizable=no,width=200,height=124');"">Listen to CNN Radio for updates from around the world</a></td>
      <td><img src=""http://i.cnn.net/cnn/.element/img/1.5/main/podcasts/podcasts_rt_end.gif"" alt="""" width=""6"" height=""41"" hspace=""0"" vspace=""0"" border=""0""></td>
     </tr>
    </table>
   </div>
  </div></div><div id=""CNN_homeRightCol""><div class=""cnn321pxBlock"">     			<div class=""CNN_homeAdBox"">
				<div class=""CNN_homeBoxHeader""><img src=""http://i.a.cnn.net/cnn/.element/img/1.5/main/advertisement.gif"" alt=""advertisement.gif"" width=""61"" height=""7"" class=""CNN_Advert"" /></div>			
				<!-- ADSPACE: home/right.336x280 --><div align=""center"" style=""padding: 0; margin: 0; border: 0;""><script type=""text/javascript""> 

cnnad_createAd(""444160"",""http://ads.cnn.com/html.ng/site=cnn&cnn_pagetype=main&cnn_position=336x280_rgt&cnn_rollup=homepage&params.styles=fs"",""280"",""336"");

</script></div>
			</div> </div><div id=""cnnPipelineModBox"" class=""CNN_homeBox"">			<div class=""cnn6pxTmar"">
				<div class=""cnnWatchPipeLft"">
					<img src=""http://i.a.cnn.net/cnn/.element/img/1.5/main/pipeline_mod_hdr.jpg"" alt=""CNN Pipeline: Live and commercial free video"" width=""236"" height=""17"">
				</div>
				<div class=""cnnWatchPipeRt"">
					<script language=""javascript"" type=""text/javascript"">
						if (cnnPipelineHeaderLinks[0][1] && cnnPipelineHeaderLinks[1][1]) {
							document.write('<a href=""'+cnnPipelineHeaderLinks[0][0]+'"">'+cnnPipelineHeaderLinks[0][1]+'<\a> | <a href=""'+cnnPipelineHeaderLinks[1][0]+'"">'+cnnPipelineHeaderLinks[1][1]+'<\/a>');
						}
					</script>
				</div>
			</div><br clear=all><div id=""cnnLivePipeModule""><div class=""cnnTabBox""><table cellpadding=""0"" cellspacing=""0"" border=""0"" width=""336""><tr valign=""top""><td><img src=""http://i.a.cnn.net/cnn/.element/img/1.5/main/tabs/pipeline_hdr_end.gif"" alt="""" width=""1"" height=""26"" border=""0""></td><td class=""cnntabLeft""></td><td><img src=""http://i.a.cnn.net/cnn/.element/img/1.4/main/biz/tab_left.gif"" alt="""" width=""2"" height=""26"" border=""0""></td><td class=""cnntabLeftCont""><img src=""http://i.a.cnn.net/cnn/.element/img/1.5/main/tabs/txt_live_blue_wt.gif"" alt=""txt_live_blue_wt.gif"" width=""20"" height=""8""/></td><td><img src=""http://i.a.cnn.net/cnn/.element/img/1.4/main/biz/tab_right.gif"" alt="""" width=""2"" height=""26"" border=""0""></td><td class=""cnntabRightCont"" onmouseover=""cnnImgSwap('PipeWhat',1);"" onmouseout=""cnnImgSwap('PipeWhat',0);"" onclick=""CNN_flipModule('cnnWhatPipeModule','cnnLivePipeModule');cnnImgSwap('PipeLive',0); var s=s_gi('cnnglobal');s.tl(this,'o','Mainpage: Pipeline What is Pipeline Tab');""><img src=""http://i.a.cnn.net/cnn/.element/img/1.5/main/tabs/what.gif"" alt=""txt_what_is_pipe_grey.gif"" width=""89"" height=""8"" id=""PipeWhat""/></a></td><td><img src=""http://i.a.cnn.net/cnn/.element/img/1.4/main/biz/hdr_line.gif"" alt="""" width=""1"" height=""26"" border=""0""></td><td class=""cnntabRight""></td><td><img src=""http://i.a.cnn.net/cnn/.element/img/1.5/main/tabs/pipeline_hdr_end.gif"" alt="""" width=""1"" height=""26"" border=""0""></td></tr></table></div><div class=""cnnPipelineSubscriber"" id=""cnnPipelineModule"">
<div id=""plineCntr1"" class=""cnnPipelineCollapsed"">
<div class=""cnnBreakingNewsBlock"">
<img alt="""" src=""http://i.a.cnn.net/cnn/.element/img/1.5/main/video/cnn_bg_red.gif""></div>
<div class=""cnnAlertBlock"">
<img alt="""" src=""http://i.a.cnn.net/cnn/.element/img/1.5/main/video/cnn_bg_orange.gif""></div>
<div class=""cnnPipelineHead"">
<div class=""cnnPipelineTryIt"">
<a href=""/pipeline/"">Try it Free</a>
</div>
<div class=""cnnPipelineHeadlineBar"" id=""cnnPipelineHeadlineBar1"">
<img title=""Expand"" alt=""Expand"" class=""cnnPlayListPlusBtn"" border=""0"" height=""14"" width=""14"" src=""http://i.a.cnn.net/cnn/.element/img/1.5/main/video/plus.gif""><img title=""Collapse"" alt=""Collapse"" class=""cnnPlayListMinusBtn"" border=""0"" height=""14"" width=""14"" src=""http://i.a.cnn.net/cnn/.element/img/1.5/main/video/minus.gif""></div>
<span id=""cnnPipelineHeadline1"" onclick=""cnnVideo('live','1');""><img title=""Live"" alt=""Live"" border=""0"" height=""10"" width=""43"" src=""http://i.a.cnn.net/cnn/.element/img/1.5/main/video/icon.pipe.module.gif"">&nbsp;'Showbiz Tonight'</span>
<br>
</div>
<div class=""cnnPipeLineTeaseImage"">
<a href=""javascript:cnnVideo('live','1');""><img border=""0"" alt=""CNN Pipeline stream image"" height=""49"" width=""88"" src=""http://i.cnn.net/cnn/images/1.gif"" lowsrc=""http://i.cnn.net/cnn/.element/img/1.3/pipeline/keyframes/88x49/stream1.jpg"" id=""plineCntr1Image""></a>
</div>
<div class=""cnnPipelineBlurb"" id=""cnnPipelineBlurb1"">
<p>CNN's A.J. Hammer goes over stories featured on tonight's edition of the show.</p>
</div>
</div>
<div id=""plineCntr2"" class=""cnnPipelineCollapsed"">
<div class=""cnnBreakingNewsBlock"">
<img alt="""" src=""http://i.a.cnn.net/cnn/.element/img/1.5/main/video/cnn_bg_red.gif""></div>
<div class=""cnnAlertBlock"">
<img alt="""" src=""http://i.a.cnn.net/cnn/.element/img/1.5/main/video/cnn_bg_orange.gif""></div>
<div class=""cnnPipelineHead"">
<div class=""cnnPipelineTryIt"">
<a href=""/pipeline/"">Try it Free</a>
</div>
<div class=""cnnPipelineHeadlineBar"" id=""cnnPipelineHeadlineBar2"">
<img title=""Expand"" alt=""Expand"" class=""cnnPlayListPlusBtn"" border=""0"" height=""14"" width=""14"" src=""http://i.a.cnn.net/cnn/.element/img/1.5/main/video/plus.gif""><img title=""Collapse"" alt=""Collapse"" class=""cnnPlayListMinusBtn"" border=""0"" height=""14"" width=""14"" src=""http://i.a.cnn.net/cnn/.element/img/1.5/main/video/minus.gif""></div>
<span id=""cnnPipelineHeadline2"" onclick=""cnnVideo('live','2');""><img title=""Live"" alt=""Live"" border=""0"" height=""10"" width=""43"" src=""http://i.a.cnn.net/cnn/.element/img/1.5/main/video/icon.pipe.module.gif"">&nbsp;Bush-Riley Event</span>
<br>
</div>
<div class=""cnnPipeLineTeaseImage"">
<a href=""javascript:cnnVideo('live','2');""><img border=""0"" alt=""CNN Pipeline stream image"" height=""49"" width=""88"" src=""http://i.cnn.net/cnn/images/1.gif"" lowsrc=""http://i.cnn.net/cnn/.element/img/1.3/pipeline/keyframes/88x49/stream2.jpg"" id=""plineCntr2Image""></a>
</div>
<div class=""cnnPipelineBlurb"" id=""cnnPipelineBlurb2"">
<p>President Bush appears at a fund-raiser for Alabama Gov. Bob Riley.</p>
</div>
</div>
<div id=""plineCntr3"" class=""cnnPipelineCollapsed"">
<div class=""cnnBreakingNewsBlock"">
<img alt="""" src=""http://i.a.cnn.net/cnn/.element/img/1.5/main/video/cnn_bg_red.gif""></div>
<div class=""cnnAlertBlock"">
<img alt="""" src=""http://i.a.cnn.net/cnn/.element/img/1.5/main/video/cnn_bg_orange.gif""></div>
<div class=""cnnPipelineHead"">
<div class=""cnnPipelineTryIt"">
<a href=""/pipeline/"">Try it Free</a>
</div>
<div class=""cnnPipelineHeadlineBar"" id=""cnnPipelineHeadlineBar3"">
<img title=""Expand"" alt=""Expand"" class=""cnnPlayListPlusBtn"" border=""0"" height=""14"" width=""14"" src=""http://i.a.cnn.net/cnn/.element/img/1.5/main/video/plus.gif""><img title=""Collapse"" alt=""Collapse"" class=""cnnPlayListMinusBtn"" border=""0"" height=""14"" width=""14"" src=""http://i.a.cnn.net/cnn/.element/img/1.5/main/video/minus.gif""></div>
<span id=""cnnPipelineHeadline3"" onclick=""cnnVideo('live','3');""><img title=""Live"" alt=""Live"" border=""0"" height=""10"" width=""43"" src=""http://i.a.cnn.net/cnn/.element/img/1.5/main/video/icon.pipe.module.gif"">&nbsp;House HP Scandal Hearing</span>
<br>
</div>
<div class=""cnnPipeLineTeaseImage"">
<a href=""javascript:cnnVideo('live','3');""><img border=""0"" alt=""CNN Pipeline stream image"" height=""49"" width=""88"" src=""http://i.cnn.net/cnn/images/1.gif"" lowsrc=""http://i.cnn.net/cnn/.element/img/1.3/pipeline/keyframes/88x49/stream3.jpg"" id=""plineCntr3Image""></a>
</div>
<div class=""cnnPipelineBlurb"" id=""cnnPipelineBlurb3"">
<p>House Energy &amp; Commerce subcommittee holds a hearing on Hewlett-Packard's pre-texting scandal.</p>
</div>
</div>
<div id=""plineCntr4"" class=""cnnPipelineCollapsed"">
<div class=""cnnBreakingNewsBlock"">
<img alt="""" src=""http://i.a.cnn.net/cnn/.element/img/1.5/main/video/cnn_bg_red.gif""></div>
<div class=""cnnAlertBlock"">
<img alt="""" src=""http://i.a.cnn.net/cnn/.element/img/1.5/main/video/cnn_bg_orange.gif""></div>
<div class=""cnnPipelineHead"">
<div class=""cnnPipelineTryIt"">
<a href=""/pipeline/"">Try it Free</a>
</div>
<div class=""cnnPipelineHeadlineBar"" id=""cnnPipelineHeadlineBar4"">
<img title=""Expand"" alt=""Expand"" class=""cnnPlayListPlusBtn"" border=""0"" height=""14"" width=""14"" src=""http://i.a.cnn.net/cnn/.element/img/1.5/main/video/plus.gif""><img title=""Collapse"" alt=""Collapse"" class=""cnnPlayListMinusBtn"" border=""0"" height=""14"" width=""14"" src=""http://i.a.cnn.net/cnn/.element/img/1.5/main/video/minus.gif""></div>
<span id=""cnnPipelineHeadline4"" onclick=""cnnVideo('live','4');""><img title=""Live"" alt=""Live"" border=""0"" height=""10"" width=""43"" src=""http://i.a.cnn.net/cnn/.element/img/1.5/main/video/icon.pipe.module.gif"">&nbsp;Florida Officer Shooting</span>
<br>
</div>
<div class=""cnnPipeLineTeaseImage"">
<a href=""javascript:cnnVideo('live','4');""><img border=""0"" alt=""CNN Pipeline stream image"" height=""49"" width=""88"" src=""http://i.cnn.net/cnn/images/1.gif"" lowsrc=""http://i.cnn.net/cnn/.element/img/1.3/pipeline/keyframes/88x49/stream4.jpg"" id=""plineCntr4Image""></a>
</div>
<div class=""cnnPipelineBlurb"" id=""cnnPipelineBlurb4"">
<p>WTVT reports two Polk County officers were involved in a shooting. Suspect search underway.</p>
</div>
</div>
</div>
</div> <div id=""cnnWhatPipeModule""><div class=""cnnTabBox""><table cellpadding=""0"" cellspacing=""0"" border=""0"" width=""336""><tr valign=""top""><td><img src=""http://i.a.cnn.net/cnn/.element/img/1.5/main/tabs/pipeline_hdr_end.gif"" alt="""" width=""1"" height=""26"" border=""0""></td><td class=""cnntabLeft""></td><td><img src=""http://i.a.cnn.net/cnn/.element/img/1.4/main/biz/hdr_line.gif"" alt="""" width=""1"" height=""26"" border=""0""></td><td class=""cnntabLeftCont"" onmouseover=""cnnImgSwap('PipeLive',1);"" onmouseout=""cnnImgSwap('PipeLive',0);"" onclick=""CNN_flipModule('cnnLivePipeModule','cnnWhatPipeModule');cnnImgSwap('PipeWhat',0); var s=s_gi('cnnglobal');s.tl(this,'o','Mainpage: Pipeline Live Tab');""><img src=""http://i.cnn.net/cnn/.element/img/1.5/main/tabs/live.gif"" alt=""live.gif"" width=""20"" height=""8"" id=""PipeLive""/></a></td><td><img src=""http://i.a.cnn.net/cnn/.element/img/1.4/main/biz/tab_left.gif"" alt="""" width=""2"" height=""26"" border=""0""></td><td class=""cnntabRightCont""><img src=""http://i.a.cnn.net/cnn/.element/img/1.5/main/tabs/txt_what_is_pipe_blue_wt.gif"" alt=""txt_what_is_pipe_blue_wt.gif"" width=""89"" height=""8""/></td><td><img src=""http://i.a.cnn.net/cnn/.element/img/1.4/main/biz/tab_right.gif"" alt="""" width=""2"" height=""26"" border=""0""></td><td class=""cnntabRight""></td><td><img src=""http://i.a.cnn.net/cnn/.element/img/1.5/main/tabs/pipeline_hdr_end.gif"" alt="""" width=""1"" height=""26"" border=""0""></td></tr></table></div><div id=""cnnWhatIsPipeLine"">
<table width=""324"" border=""0"" cellspacing=""0"" cellpadding=""0"">
  <tr>
    <td width=""150""><a href=""/pipeline/"" onClick=""var s=s_gi('cnnglobal');s.tl(this,'o','Mainpage: Pipeline What is Pipeline Button');""><img src=""http://i.a.cnn.net/cnn/.element/img/1.5/main/video/explainer/Live_screen.gif"" alt=""Live Video"" border=""0"" width=""150"" height=""143"" /></a></td>
    <td width=""174""><a href=""/pipeline/"" onClick=""var s=s_gi('cnnglobal');s.tl(this,'o','Mainpage: Pipeline What is Pipeline Button');""><img src=""http://i.a.cnn.net/cnn/.element/img/1.5/main/video/explainer/move_Beyond.gif"" alt=""Move beyond free video"" width=""174"" height=""143"" border=""0"" ></a></td>
  </tr>
</table>	
</div>
</div> <div id=""cnnPipelineFooter"">
				<div id=""cnnPipelineSponsoredBy"">
					<!-- ADSPACE: home/sponsor1.88x31 --><div align=""center"" style=""padding: 0; margin: 0; border: 0;""><script type=""text/javascript""> 

cnnad_createAd(""552357"",""http://ads.cnn.com/html.ng/site=cnn&cnn_pagetype=main&cnn_position=88x31_spon1&cnn_rollup=homepage&params.styles=fs"",""31"",""88"");

</script></div>
				</div>
				<div class=""cnnPipelineUpsellSmWoolText"">
					<script language=""javascript"" type=""text/javascript"">
						document.write(cnnPipelineUpsellSmWoolText);
					</script>
				</div>		
				<div class=""cnnPipelineUpsellSmText"">
					<script language=""javascript"" type=""text/javascript"">
						document.write('<b><a href=""'+cnnPipelineUpsellLrgTextLink+'"" style=""color:#666;"" onClick=""var s=s_gi(\'cnnglobal\');s.tl(this,\'o\',cnnPipelineOmnitureText);"" >'+cnnPipelineUpsellLrgText+'<\/a></b>')
					</script>
				</div>
	
			</div>				
		</div>
<script type=""text/javascript"" language=""javascript"">

		var freeVidCntr = new cnnPipeContainer();
		freeVidCntr.setObjName('freeVidCntr');
		freeVidCntr.addContainer('freeVidCntr1');
		freeVidCntr.addContainer('freeVidCntr2');
		freeVidCntr.addContainer('freeVidCntr3');
		freeVidCntr.addContainer('freeVidCntr4');
		freeVidCntr.addContainer('freeVidCntr5');		
		freeVidCntr.zipNode('freeVidCntr1');
	
		var freeVidMPCntr = new cnnPipeContainer();
		freeVidMPCntr.setObjName('freeVidMPCntr');
		freeVidMPCntr.addContainer('freeVidMPCntr1');
		freeVidMPCntr.addContainer('freeVidMPCntr2');
		freeVidMPCntr.addContainer('freeVidMPCntr3');
		freeVidMPCntr.addContainer('freeVidMPCntr4');
		freeVidMPCntr.zipNode('freeVidMPCntr1');	
	
		var pipeCtrl = new cnnPipeContainer();
		pipeCtrl.setObjName('pipeCtrl');
		pipeCtrl.addContainer('plineCntr1');
		pipeCtrl.addContainer('plineCntr2');
		pipeCtrl.addContainer('plineCntr3');
		pipeCtrl.addContainer('plineCntr4');
		//pipeCtrl.zipNode('plineCntr1');
		pipeCtrl.zipNode('plineCntr4');
		pipeCtrl.setRefreshRate(30);
		pipeCtrl.setDataUrl('/.element/ssi/auto/1.4/pipeline_mp/live.mhtml');
		pipeCtrl.setParsingFunction(updateContents);
		window.setTimeout(""pipeCtrl.refresh()"",30000);
		
	</script>	
<iframe src=""about:blank"" name=""pipeCtrlFrame"" id=""pipeCtrlFrame"" width=""0"" height=""0"" frameborder=""0"" onload=""if(pipeCtrl){pipeCtrl.handleResponse();}"" onreadystatechange=""if(pipeCtrl){pipeCtrl.handleResponse();}""></iframe>
 <div class=""CNN_homeBox"" style=""border-top:none;""><div class=""cnnTab4Visible"" id=""tab""><div class=""cnnMarketsUpdate""><div class=""left""><img src=""http://i.a.cnn.net/cnn/.element/img/1.4/main/biz/hdr_market_update.gif"" alt="""" width=""107"" height=""10"" hspace=""0"" vspace=""0"" border=""0""></div><div class=""right"" align=""right""><div><a href=""http://money.cnn.com"">CNNMoney</a></div></div></div><div class=""cnnTab3Container""><table cellpadding=""0"" cellspacing=""0"" border=""0"" width=""336""><tr valign=""top""><td><img src=""http://i.a.cnn.net/cnn/.element/img/1.4/main/biz/hdr_end.gif"" alt="""" width=""1"" height=""26"" border=""0""></td><td class=""cnnBigChartsTabSpace""></td><td><img src=""http://i.a.cnn.net/cnn/.element/img/1.4/main/biz/tab_left.gif"" alt="""" width=""2"" height=""26"" border=""0""></td><td class=""cnnBigChartsTabMK""><img src=""http://i.a.cnn.net/cnn/.element/img/1.4/main/biz/txt_markets_blue_wt.gif"" alt="""" width=""45"" height=""8"" hspace=""0"" vspace=""0"" border=""0""></td><td><img src=""http://i.a.cnn.net/cnn/.element/img/1.4/main/biz/tab_right.gif"" alt="""" width=""2"" height=""26"" border=""0""></td><td class=""cnnBigChartsTabGQ"" onmouseover=""cnnImgSwap('GetQuote',1);"" onmouseout=""cnnImgSwap('GetQuote',0);"" onclick=""showTab('tab', '4');cnnImgSwap('Markets',0);""><img src=""http://i.a.cnn.net/cnn/.element/img/1.4/main/biz/quote.gif"" alt="""" width=""51"" height=""8"" hspace=""0"" vspace=""0"" border=""0"" id=""GetQuote""></a></td><td><img src=""http://i.a.cnn.net/cnn/.element/img/1.4/main/biz/hdr_line.gif"" alt="""" width=""1"" height=""26"" border=""0""></td><td class=""cnnBigChartsTabSpace2""></td><td><img src=""http://i.a.cnn.net/cnn/.element/img/1.4/main/biz/hdr_end.gif"" alt="""" width=""1"" height=""26"" border=""0""></td></tr></table>	<div class=""cnnBigCharts"">
		<table cellpadding=""0"" cellspacing=""0"" border=""0"">
			<tr valign=""middle"">
				<td class=""cnnBigChartsDow""><a href=""http://money.cnn.com/data/markets/dow/"">DOW</a><div class=""cnn4pxTpad""><img src=""http://i.a.cnn.net/cnn/.element/img/1.3/main/biz/arrow.up.gif"" alt="""" width=""11"" height=""10"" hspace=""0"" vspace=""0"" border=""0""></div></td>
				<td class=""cnnBigChartsData1""><b>11,707.49</b><br>+ 18.25</td>
				<td class=""cnnBigChartsSpace""><img src=""http://i.cnn.net/cnn/.element/img/1.4/main/biz/ddd.gif"" alt="""" width=""1"" height=""20"" hspace=""0"" vspace=""0"" border=""0""></td>	
				<td class=""cnnBigChartsNas""><a href=""http://money.cnn.com/data/markets/nasdaq/"">NAS</a><div class=""cnn4pxTpad""><img src=""http://i.a.cnn.net/cnn/.element/img/1.3/main/biz/arrow.up.gif"" alt="""" width=""11"" height=""10"" hspace=""0"" vspace=""0"" border=""0""></div></td>
				<td class=""cnnBigChartsData2""><b>2,269.25</b><br>+ 5.86</td>
				<td class=""cnnBigChartsSpace""><img src=""http://i.cnn.net/cnn/.element/img/1.4/main/biz/ddd.gif"" alt="""" width=""1"" height=""20"" hspace=""0"" vspace=""0"" border=""0""></td>
				<td class=""cnnBigChartsSP""><a href=""http://money.cnn.com/data/markets/sandp/"">S&amp;P</a><div class=""cnn4pxTpad""><img src=""http://i.a.cnn.net/cnn/.element/img/1.3/main/biz/arrow.up.gif"" alt="""" width=""11"" height=""10"" hspace=""0"" vspace=""0"" border=""0""></div></td>
				<td class=""cnnBigChartsData3""><b>1,339.50</b><br>+ 2.91</td>
			</tr>
		</table>
	</div>
</div>
<div class=""cnnTab4Container"">
	<table cellpadding=""0"" cellspacing=""0"" border=""0"" width=""336"">
		<tr valign=""top"">
			<td><img src=""http://i.cnn.net/cnn/.element/img/1.4/main/biz/hdr_end.gif"" alt="""" width=""1"" height=""26"" border=""0""></td>
			<td class=""cnnBigChartsTabSpace""></td>
			<td><img src=""http://i.cnn.net/cnn/.element/img/1.4/main/biz/hdr_line.gif"" alt="""" width=""1"" height=""26"" border=""0""></td>
			<td class=""cnnBigChartsTabMK"" onmouseover=""cnnImgSwap('Markets',1);"" onmouseout=""cnnImgSwap('Markets',0);"" onclick=""showTab('tab', '3');cnnImgSwap('GetQuote',0);""><img src=""http://i.cnn.net/cnn/.element/img/1.4/main/biz/markets.gif"" alt="""" width=""45"" height=""8"" hspace=""0"" vspace=""0"" border=""0"" id=""Markets""></a></td>
			<td><img src=""http://i.cnn.net/cnn/.element/img/1.4/main/biz/tab_left.gif"" alt="""" width=""2"" height=""26"" border=""0""></td>
			<td class=""cnnBigChartsTabGQ""><img src=""http://i.cnn.net/cnn/.element/img/1.4/main/biz/txt_get_quote_blue_wt.gif"" alt="""" width=""51"" height=""8"" hspace=""0"" vspace=""0"" border=""0""></td>
			<td><img src=""http://i.cnn.net/cnn/.element/img/1.4/main/biz/tab_right.gif"" alt="""" width=""2"" height=""26"" border=""0""></td>
			<td class=""cnnBigChartsTabSpace2""></td>
			<td><img src=""http://i.cnn.net/cnn/.element/img/1.4/main/biz/hdr_end.gif"" alt="""" width=""1"" height=""26"" border=""0""></td>
		</tr>
	</table>
	<div class=""cnnBigCharts"">
	<form action=""http://cgi.money.cnn.com/servlets/quote_redirect"" method=""post"">
			<table cellpadding=""0"" cellspacing=""0"" border=""0"">		
				<tr valign=""middle"" height=""33"">
					<td class=""cnnBigChartsEntSym"">Enter&nbsp;Symbol:</td>
					<td class=""cnnBigChartsSymField""><input name=""query"" type=""text"" size=""7"" maxlength=""40"" class=""cnnStockField""></td>
					<td class=""cnnBigChartsSymBtn""><input type=""submit"" class=""cnnFormButtonSm"" value=""GET""></td>
					<td class=""cnnBigChartsSym"">or <a href=""/money/quote/lookup/index.html"">Symbol Look-up</a></td>																								
				</tr>
			</table></form>
	</div>
</div>
<table cellpadding=""0"" cellspacing=""0"" border=""0"" class=""cnnBigChartsFoot"">
	<tr valign=""top"" height=""31"">
		<td class=""cnnBizpdated"">Updated: 2:45 p.m. ET, Sep 28<div class=""cnn3pxTPad""><img src=""http://i.cnn.net/cnn/.element/img/1.3/main/logo_bigcharts.gif"" alt="""" width=""50"" height=""9"" hspace=""0"" vspace=""0"" border=""0""></div></td>
		<td align=""right"" class=""cnnBizSponsor"">sponsored by:</td>
		<td id=""business0909"" align=""right""><div><!-- ADSPACE: home/sponsor2.88x31 --><div align=""center"" style=""padding: 0; margin: 0; border: 0;""><script type=""text/javascript""> 

cnnad_createAd(""757565"",""http://ads.cnn.com/html.ng/site=cnn&cnn_pagetype=main&cnn_position=88x31_spon2&cnn_rollup=homepage&params.styles=fs"",""31"",""88"");

</script></div></div></td>
	</tr>
</table>
</div></div></div></div></div><div style=""clear:both;""><img src=""http://i.cnn.net/cnn/images/1.gif"" width=""1"" height=""1"" hspace=""0"" vspace=""0"" border=""0"" alt=""""></div><table cellpadding=""0"" cellspacing=""0"" border=""0"" id=""cnnBelowFold""><colgroup><col width=""694""><col width=""1""><col width=""249""></colgroup><tr><td colspan=""3"" id=""cnnHDash""><img src=""http://i.cnn.net/cnn/images/1.gif"" width=""1"" height=""1"" hspace=""0"" vspace=""0"" border=""0"" alt=""""></td></tr><tr valign=""top""><td id=""cnnBulletBins""><table cellpadding=""0"" cellspacing=""0"" border=""0"" width=""598""><colgroup><col width=""15""><col width=""319""> <col width=""26""><col width=""15""><col width=""319""></colgroup><tr><td colspan=""2""><img src=""http://i.a.cnn.net/cnn/.element/img/1.5/main/fb.top.334.gif"" width=""334"" height=""10"" hspace=""0"" vspace=""0"" border=""0"" alt=""""></td><td rowspan=""3""><img src=""http://i.cnn.net/cnn/images/1.gif"" width=""26"" height=""1"" hspace=""0"" vspace=""0"" border=""0"" alt=""""></td><td colspan=""2""><img src=""http://i.a.cnn.net/cnn/.element/img/1.5/main/fb.top.334.gif"" width=""334"" height=""10"" hspace=""0"" vspace=""0"" border=""0"" alt=""""></td></tr><tr><td class=""cnnFBidSPECIALS""><a href=""/SPECIALS""><img src=""http://i.a.cnn.net/cnn/.element/img/1.3/main/fb.spec.reports.gif"" width=""15"" height=""70"" hspace=""0"" vspace=""0"" border=""0"" alt=""Special Reports""></a></td><td class=""cnnFB""><div class=""cnnFbSPECIALS""><div class=""cnnBoxTitle"">FALL TRAVEL</div><span class=""cnnFBTz"">    <a href=""/SPECIALS/2006/exploring.autumn/""><img src=""http://www.cnn.com/SPECIALS/2006/exploring.autumn/interactive/gallery.fall.drives/tz.virginia.jpg"" alt=""Exploring autumn's true colors"" width=""65"" height=""49"" align=""right"" hspace=""0"" vspace=""0"" border=""0""></a></span><b><a href=""/SPECIALS/2006/exploring.autumn/"">Exploring autumn's true colors</a><br></b>Journey across the country to see all of fall's colors</div>    </td><td class=""cnnFBidBUSINESS""><a href=""/money/index.html?cnn=yes""><img src=""http://i.a.cnn.net/cnn/.element/img/1.3/main/fb.business.gif"" width=""15"" height=""70"" hspace=""0"" vspace=""0"" border=""0"" alt=""Business""></a></td><td class=""cnnFB""><div class=""cnnFbBUSINESS""><div class=""cnnBoxTitle"">SLEEP FOR SALE</div><span class=""cnnFBTz"">    <a href=""http://money.cnn.com/magazines/business2/business2_archive/2006/10/01/8387112/index.htm?postversion=2006092517?cnn=yes""><img src=""http://i.a.cnn.net/cnn/2006/images/09/27/tz.sleep.gi.jpg"" alt=""Shut-eye shortage"" width=""65"" height=""49"" align=""right"" hspace=""0"" vspace=""0"" border=""0""></a></span><b><span class=""cnnWOOL"">Business 2.0: </span></b> <b><a href=""http://money.cnn.com/magazines/business2/business2_archive/2006/10/01/8387112/index.htm?postversion=2006092517?cnn=yes"">Shut-eye shortage</a><br></b>In our non-stop world, a good night's rest has become a $20 billion industry</div>    </td></tr><tr><td colspan=""2""><img src=""http://i.a.cnn.net/cnn/.element/img/1.5/main/fb.bottom.334.gif"" width=""334"" height=""10"" hspace=""0"" vspace=""0"" border=""0"" alt=""""></td><td colspan=""2""><img src=""http://i.a.cnn.net/cnn/.element/img/1.5/main/fb.bottom.334.gif"" width=""334"" height=""10"" hspace=""0"" vspace=""0"" border=""0"" alt=""""></td></tr></table><table cellpadding=""0"" cellspacing=""0"" border=""0"" class=""cnnBins""><colgroup><col width=""334""><col width=""26""><col width=""334""></colgroup><tr><td valign=""top""><a href=""/US/""><img src=""http://i.a.cnn.net/cnn/.element/img/1.5/main/shdr.wd.us.home.gif"" width=""334"" height=""16"" hspace=""0"" vspace=""0"" border=""0"" alt=""US"" class=""cnnBinHd""></a>
<div class=""cnnBinNav""><img src=""http://i.a.cnn.net/cnn/images/1.gif"" width=""2"" height=""1"" hspace=""4"" vspace=""0"" border=""0"" alt=""""><a href=""/US/"">Section Page</a><img src=""http://i.a.cnn.net/cnn/.element/img/1.3/misc/bbin.vert.div.gif"" width=""2"" height=""15"" hspace=""4"" vspace=""0"" alt="""" border=""0""><a href=""javascript:cnnVideo('browse','/us');"">Video</a><img src=""http://i.a.cnn.net/cnn/.element/img/1.3/misc/bbin.vert.div.gif"" width=""2"" height=""15"" hspace=""4"" vspace=""0"" alt="""" border=""0""><a href=""javascript:cnnFlipDisplayIdNode('cnnLocalDHTML','132px');"">Local News</a><img src=""http://i.a.cnn.net/cnn/.element/img/1.3/misc/bbin.gray.arrow.gif"" width=""6"" height=""4"" hspace=""2"" vspace=""0"" alt="""" border=""0"" align=""absmiddle""></div>
<div id=""cnnLocalDHTML"">
	<div class=""cnnDHTMLnav"">
		<div onmouseover=""cnnSetHoverClass(this,'cnnHover',1)"" onmouseout=""cnnSetHoverClass(this,'cnnHover',0)""><a href=""/LOCAL/northeast/"">Northeast</a></div>
		<div onmouseover=""cnnSetHoverClass(this,'cnnHover',1)"" onmouseout=""cnnSetHoverClass(this,'cnnHover',0)""><a href=""/LOCAL/west/"">West</a></div>
		<div onmouseover=""cnnSetHoverClass(this,'cnnHover',1)"" onmouseout=""cnnSetHoverClass(this,'cnnHover',0)""><a href=""/LOCAL/south/"">South</a></div>
		<div onmouseover=""cnnSetHoverClass(this,'cnnHover',1)"" onmouseout=""cnnSetHoverClass(this,'cnnHover',0)""><a href=""/LOCAL/midwest/"">Midwest</a></div>
		<div onmouseover=""cnnSetHoverClass(this,'cnnHover',1)"" onmouseout=""cnnSetHoverClass(this,'cnnHover',0)""><a href=""/LOCAL/southwest/"">Southwest</a></div>
		<div onmouseover=""cnnSetHoverClass(this,'cnnHover',1)"" onmouseout=""cnnSetHoverClass(this,'cnnHover',0)""><a href=""/LOCAL/central/"">Central</a></div>
	</div>
</div><div class=""cnnBulletBins""><div>&#8226;&nbsp;<a href=""/2006/HEALTH/09/28/dentist.coma.ap/index.html"">Sedated girl dies after dental visit</a> </div><div>&#8226;&nbsp;<a href=""/2006/POLITICS/09/28/congress.terrorism.ap/index.html"">House passes terror detainee bill; Senate OK expected</a> </div><div>&#8226;&nbsp;<span class=""cnnWOOL"">CNNMoney: </span><a href=""http://money.cnn.com/2006/09/28/markets/markets_0945/index.htm?postversion=2006092810"">Dow briefly treks into uncharted territory</a> </div></div></td><td rowspan=""6""><img src=""http://i.cnn.net/cnn/images/1.gif"" width=""26"" height=""1"" hspace=""0"" vspace=""0"" border=""0"" alt=""""></td><td valign=""top""><a href=""/WORLD/""><img src=""http://i.a.cnn.net/cnn/.element/img/1.5/main/shdr.wd.world.home.gif"" width=""334"" height=""16"" hspace=""0"" vspace=""0"" border=""0"" alt=""WORLD"" class=""cnnBinHd""></a>
<div class=""cnnBinNav""><img src=""http://i.a.cnn.net/cnn/images/1.gif"" width=""2"" height=""1"" hspace=""4"" vspace=""0"" border=""0"" alt=""""><a href=""/WORLD/"">Section Page</a><img src=""http://i.a.cnn.net/cnn/.element/img/1.3/misc/bbin.vert.div.gif"" width=""2"" height=""15"" hspace=""4"" vspace=""0"" alt="""" border=""0""><a href=""javascript:cnnVideo('browse','/world');"">Video</a><img src=""http://i.a.cnn.net/cnn/.element/img/1.3/misc/bbin.vert.div.gif"" width=""2"" height=""15"" hspace=""4"" vspace=""0"" alt="""" border=""0""><a href=""/linkto/intl.html"">CNN.com International Edition</a></div><div class=""cnnBulletBins""><div>&#8226;&nbsp;<a href=""/2006/WORLD/meast/09/28/iran.nuclear.reut/index.html"">Iran nuclear talks 'made progress'</a> </div><div>&#8226;&nbsp;<a href=""/2006/WEATHER/09/27/typhoon.xangsane.ap/index.html"">Typhoon Xangsane hits Philippines</a> </div><div>&#8226;&nbsp;<a href=""/2006/WORLD/americas/09/27/argentina.warwitness.ap/index.html"">Mass rally for 'dirty war' witness</a> </div></div></td></tr><tr><td valign=""top""><a href=""/TECH/""><img src=""http://i.a.cnn.net/cnn/.element/img/1.5/main/shdr.wd.tech.gif"" width=""334"" height=""16"" hspace=""0"" vspace=""0"" border=""0"" alt=""TECHNOLOGY"" class=""cnnBinHd""></a>
<div class=""cnnBinNav""><img src=""http://i.a.cnn.net/cnn/images/1.gif"" width=""2"" height=""1"" hspace=""4"" vspace=""0"" border=""0"" alt=""""><a href=""/TECH/"">Section Page</a><img src=""http://i.a.cnn.net/cnn/.element/img/1.3/misc/bbin.vert.div.gif"" width=""2"" height=""15"" hspace=""4"" vspace=""0"" alt="""" border=""0""><a href=""javascript:cnnVideo('browse','/tech');"">Video</a><img src=""http://i.a.cnn.net/cnn/.element/img/1.3/misc/bbin.vert.div.gif"" width=""2"" height=""15"" hspace=""4"" vspace=""0"" alt="""" border=""0""><a href=""/business2/"">Business 2.0</a><img src=""http://i.a.cnn.net/cnn/.element/img/1.3/misc/icon.offsite.gif"" width=""12"" height=""9"" hspace=""0"" vspace=""0"" border=""0"" alt="""" class=""cnnOffsite""><img src=""http://i.a.cnn.net/cnn/.element/img/1.3/misc/bbin.vert.div.gif"" width=""2"" height=""15"" hspace=""4"" vspace=""0"" alt="""" border=""0""><a href=""/fortune/"">Fortune</a><img src=""http://i.a.cnn.net/cnn/.element/img/1.3/misc/icon.offsite.gif"" width=""12"" height=""9"" hspace=""0"" vspace=""0"" border=""0"" alt="""" class=""cnnOffsite""></div><div class=""cnnBulletBins""><div>&#8226;&nbsp;<a href=""/2006/TECH/ptech/09/28/microsoft.zune.reut/index.html"">Microsoft sets price for Zune</a> </div><div>&#8226;&nbsp;<a href=""/2006/TECH/biztech/09/28/craigslist.reut/index.html"">Craigslist founder says he won't cash in</a> </div></div></td><td valign=""top""><a href=""/SHOWBIZ/""><img src=""http://i.a.cnn.net/cnn/.element/img/1.5/main/shdr.wd.entertainment.gif"" width=""334"" height=""16"" hspace=""0"" vspace=""0"" border=""0"" alt=""ENTERTAINMENT"" class=""cnnBinHd""></a>
<div class=""cnnBinNav""><img src=""http://i.a.cnn.net/cnn/images/1.gif"" width=""2"" height=""1"" hspace=""4"" vspace=""0"" border=""0"" alt=""""><a href=""/SHOWBIZ/"">Section Page</a><img src=""http://i.a.cnn.net/cnn/.element/img/1.3/misc/bbin.vert.div.gif"" width=""2"" height=""15"" hspace=""4"" vspace=""0"" alt="""" border=""0""><a href=""javascript:cnnVideo('browse','/showbiz');"">Video</a><img src=""http://i.a.cnn.net/cnn/.element/img/1.3/misc/bbin.vert.div.gif"" width=""2"" height=""15"" hspace=""4"" vspace=""0"" alt="""" border=""0""><a href=""/ew/"">EntertainmentWeekly.com</a><img src=""http://i.a.cnn.net/cnn/.element/img/1.3/misc/icon.offsite.gif"" width=""12"" height=""9"" hspace=""0"" vspace=""0"" border=""0"" alt="""" class=""cnnOffsite""></div><div class=""cnnBulletBins""><div>&#8226;&nbsp;<a href=""/2006/SHOWBIZ/TV/09/28/tv.dexter.ap/index.html"">'Good' serial killer fights crime his way</a> </div><div>&#8226;&nbsp;<a href=""/2006/SHOWBIZ/TV/09/28/people.amandapeet.ap/index.html"">Amanda Peet pregnant, getting married</a> </div></div></td></tr><tr><td valign=""top""><a href=""/POLITICS/""><img src=""http://i.a.cnn.net/cnn/.element/img/1.5/main/shdr.wd.politics.gif"" width=""334"" height=""16"" hspace=""0"" vspace=""0"" border=""0"" alt=""POLITICS"" class=""cnnBinHd""></a>
<div class=""cnnBinNav""><img src=""http://i.a.cnn.net/cnn/images/1.gif"" width=""2"" height=""1"" hspace=""4"" vspace=""0"" border=""0"" alt=""""><a href=""/POLITICS/"">Section Page</a><img src=""http://i.a.cnn.net/cnn/.element/img/1.3/misc/bbin.vert.div.gif"" width=""2"" height=""15"" hspace=""4"" vspace=""0"" alt="""" border=""0""><a href=""javascript:cnnVideo('browse','/politics');"">Video</a><img src=""http://i.a.cnn.net/cnn/.element/img/1.3/misc/bbin.vert.div.gif"" width=""2"" height=""15"" hspace=""4"" vspace=""0"" alt="""" border=""0""><a href=""/POLITICS/analysis/toons/archive.html"">Cartoons</a><img src=""http://i.a.cnn.net/cnn/.element/img/1.3/misc/bbin.vert.div.gif"" width=""2"" height=""15"" hspace=""4"" vspace=""0"" alt="""" border=""0""><a href=""/time/"">TIME</a><img src=""http://i.a.cnn.net/cnn/.element/img/1.3/misc/icon.offsite.gif"" width=""12"" height=""9"" hspace=""0"" vspace=""0"" border=""0"" alt="""" class=""cnnOffsite""></div><div class=""cnnBulletBins""><div>&#8226;&nbsp;<a href=""/2006/POLITICS/09/28/congress.terrorism.ap/index.html"">Senate rejects terror suspect challenges</a> </div><div>&#8226;&nbsp;<a href=""/2006/POLITICS/09/28/connecticut.senate.ap/index.html"">Lieberman challenger struggles to close gap</a> </div></div></td><td valign=""top""><a href=""/LAW/""><img src=""http://i.a.cnn.net/cnn/.element/img/1.5/main/shdr.wd.law.gif"" width=""334"" height=""16"" hspace=""0"" vspace=""0"" border=""0"" alt=""LAW"" class=""cnnBinHd""></a>
<div class=""cnnBinNav""><img src=""http://i.a.cnn.net/cnn/images/1.gif"" width=""2"" height=""1"" hspace=""4"" vspace=""0"" border=""0"" alt=""""><a href=""/LAW/"">Section Page</a><img src=""http://i.a.cnn.net/cnn/.element/img/1.3/misc/bbin.vert.div.gif"" width=""2"" height=""15"" hspace=""4"" vspace=""0"" alt="""" border=""0""><a href=""javascript:cnnVideo('browse','/law');"">Video</a></div><div class=""cnnBulletBins""><div>&#8226;&nbsp;<a href=""/2006/LAW/09/28/dyleski.profile/index.html"">How a neglected boy grew up to enjoy killing</a> </div><div>&#8226;&nbsp;<a href=""/2006/LAW/09/28/inmate.tattoo.ap/index.html"">Prison looks into 'Katie's Revenge' tattoo</a> </div></div></td></tr><tr><td valign=""top""><a href=""/HEALTH/""><img src=""http://i.a.cnn.net/cnn/.element/img/1.5/main/shdr.wd.health.gif"" width=""334"" height=""16"" hspace=""0"" vspace=""0"" border=""0"" alt=""HEALTH"" class=""cnnBinHd""></a>
<div class=""cnnBinNav""><img src=""http://i.a.cnn.net/cnn/images/1.gif"" width=""2"" height=""1"" hspace=""4"" vspace=""0"" border=""0"" alt=""""><a href=""/HEALTH/"">Section Page</a><img src=""http://i.a.cnn.net/cnn/.element/img/1.3/misc/bbin.vert.div.gif"" width=""2"" height=""15"" hspace=""4"" vspace=""0"" alt="""" border=""0""><a href=""javascript:cnnVideo('browse','/health');"">Video</a><img src=""http://i.a.cnn.net/cnn/.element/img/1.3/misc/bbin.vert.div.gif"" width=""2"" height=""15"" hspace=""4"" vspace=""0"" alt="""" border=""0""><a href=""/HEALTH/library/"">Health Library</a></div><div class=""cnnBulletBins""><div>&#8226;&nbsp;<a href=""/2006/HEALTH/conditions/01/27/rare.conditions/index.html"">World without pain is hell, parent says</a> </div><div>&#8226;&nbsp;<a href=""/2006/HEALTH/09/27/portion.confusion.ap/index.html"">Serving size a pitfall for label-readers </a> </div></div></td><td valign=""top""><a href=""/TECH/space/""><img src=""http://i.a.cnn.net/cnn/.element/img/1.5/main/shdr.wd.science.gif"" width=""334"" height=""16"" hspace=""0"" vspace=""0"" border=""0"" alt=""SCIENCE &amp; SPACE"" class=""cnnBinHd""></a>
<div class=""cnnBinNav""><img src=""http://i.a.cnn.net/cnn/images/1.gif"" width=""2"" height=""1"" hspace=""4"" vspace=""0"" border=""0"" alt=""""><a href=""/TECH/space/"">Section Page</a><img src=""http://i.a.cnn.net/cnn/.element/img/1.3/misc/bbin.vert.div.gif"" width=""2"" height=""15"" hspace=""4"" vspace=""0"" alt="""" border=""0""><a href=""javascript:cnnVideo('browse','/tech');"">Video</a></div><div class=""cnnBulletBins""><div>&#8226;&nbsp;<a href=""/2006/TECH/space/09/28/space.tourist.miles.ap/index.html"">Frequent-flyer cashes in miles for space trip</a> </div><div>&#8226;&nbsp;<a href=""/2006/TECH/space/09/28/massive.star/index.html"">Cosmic doughnuts linked to massive stars</a> </div></div></td></tr><tr><td valign=""top""><a href=""/TRAVEL/""><img src=""http://i.a.cnn.net/cnn/.element/img/1.5/main/shdr.wd.travel.gif"" width=""334"" height=""16"" hspace=""0"" vspace=""0"" border=""0"" alt=""TRAVEL"" class=""cnnBinHd""></a>
<div class=""cnnBinNav""><img src=""http://i.a.cnn.net/cnn/images/1.gif"" width=""2"" height=""1"" hspace=""4"" vspace=""0"" border=""0"" alt=""""><a href=""/TRAVEL/"">Section Page</a><img src=""http://i.a.cnn.net/cnn/.element/img/1.3/misc/bbin.vert.div.gif"" width=""2"" height=""15"" hspace=""4"" vspace=""0"" alt="""" border=""0""><a href=""/WEATHER/"">Weather Forecast</a></div><div class=""cnnBulletBins""><div>&#8226;&nbsp;Cooking Light: <a href=""/2006/TRAVEL/DESTINATIONS/09/28/postcard.seattle/index.html"">A culinary postcard from Seattle</a> </div><div>&#8226;&nbsp;<a href=""/2006/TRAVEL/DESTINATIONS/09/26/women.golf.ap/index.html"">Wooing women to the greens</a> </div></div></td><td valign=""top""><a href=""/EDUCATION/""><img src=""http://i.a.cnn.net/cnn/.element/img/1.5/main/shdr.wd.education.gif"" width=""334"" height=""16"" hspace=""0"" vspace=""0"" border=""0"" alt=""EDUCATION"" class=""cnnBinHd""></a>
<div class=""cnnBinNav""><img src=""http://i.a.cnn.net/cnn/images/1.gif"" width=""2"" height=""1"" hspace=""4"" vspace=""0"" border=""0"" alt=""""><a href=""/EDUCATION/"">Section Page with CNN Student News</a></div><div class=""cnnBulletBins""><div>&#8226;&nbsp;<a href=""/2006/EDUCATION/09/28/college.web.help.ap/index.html"">States offer one-stop Web sites for college applicants</a> </div><div>&#8226;&nbsp;<a href=""/2006/EDUCATION/09/28/education.india.reut/index.html"">U.S. homework outsourced as 'e-tutoring' grows</a> </div></div></td></tr><tr><td valign=""top""><a href=""/si/""><img src=""http://i.a.cnn.net/cnn/.element/img/1.5/main/shdr.wd.sports.gif"" width=""334"" height=""16"" hspace=""0"" vspace=""0"" border=""0"" alt=""SPORTS"" class=""cnnBinHd""></a>
<div class=""cnnBinNav""><img src=""http://i.a.cnn.net/cnn/images/1.gif"" width=""2"" height=""1"" hspace=""4"" vspace=""0"" border=""0"" alt=""""><a href=""/si/"">SI.com Home Page</a><img src=""http://i.a.cnn.net/cnn/.element/img/1.3/misc/bbin.vert.div.gif"" width=""2"" height=""15"" hspace=""4"" vspace=""0"" alt="""" border=""0""><a href=""javascript:cnnVideo('browse','/sports');"">Video</a></div> &#8226;&nbsp;<a href=""/si/multimedia/photo_gallery/0609/campus.trendsetters/content.1.html?cnn=yes"">The fashion trendsetters in college sports</a>
<br> &#8226;&nbsp;<a href=""/si/multimedia/photo_gallery/0609/gallery.nfl.terrell.owens.timeline/content.1.html#?cnn=yes"">A look at Terrell Owens' roller-coaster career</a>
<br>
</td><td valign=""top""><a href=""http://money.cnn.com/index.html?cnn=yes""><img src=""http://i.a.cnn.net/cnn/.element/img/1.5/main/shdr.wd.business.gif"" width=""334"" height=""16"" hspace=""0"" vspace=""0"" border=""0"" alt=""BUSINESS"" class=""cnnBinHd""></a>
<div class=""cnnBinNav""><img src=""http://i.a.cnn.net/cnn/images/1.gif"" width=""2"" height=""1"" hspace=""4"" vspace=""0"" border=""0"" alt=""""><a href=""http://money.cnn.com/index.html?cnn=yes"">CNNMoney.com Home Page</a><img src=""http://i.a.cnn.net/cnn/.element/img/1.3/misc/bbin.vert.div.gif"" width=""2"" height=""15"" hspace=""4"" vspace=""0"" alt="""" border=""0""><a href=""javascript:cnnVideo('browse','/business');"">Video</a></div> &#8226;&nbsp;<a href=""http://money.cnn.com/2006/09/28/technology/hp_hearing/index.htm?cnn=yes"">Dunn expresses 'deep regret'</a>
<br> &#8226;&nbsp;<a href=""http://money.cnn.com/magazines/fortune/fortune_archive/2006/10/02/8387409/index.htm?cnn=yes"">Housing starts fall, confusion rises</a>
<br>
</td></tr></table><div id=""cnnPartners""><div class=""cnnPartnersHd""><img src=""http://i.a.cnn.net/cnn/.element/img/1.5/main/hdr_partners.gif"" alt="""" width=""138"" height=""10"" hspace=""0"" vspace=""0"" border=""0""><img src=""http://i.a.cnn.net/cnn/.element/img/1.3/misc/icon.offsite.gif"" width=""12"" height=""9"" hspace=""0"" vspace=""0"" border=""0"" alt="""" class=""cnnOffsite""></div><table cellpadding=""0"" cellspacing=""0"" border=""0"" width=""694""><colgroup><col width=""334""><col width=""26""><col width=""334""></colgroup><tr valign=""top""><td><div class=""cnnPartner1""><div class=""cnnPartnersLft"">
<a target=""new"" href=""/time/?cnn=yes""><img height=""17"" width=""70"" alt=""Time: "" border=""0"" class=""cnnPartHead"" src=""http://i.a.cnn.net/cnn/.element/img/1.0/main/partner_time.gif""></a>
</div>
<div class=""cnnPartnersRt"">
<a href=""/linkto/time.main.html"" target=""new"">Subscribe</a>
</div>
<br clear=""all"">
<div class=""cnn2pxBpad"">&#8226;&nbsp;<a target=""new"" href=""http://www.time.com/time/world/article/0,8599,1539999,00.html?cnn=yes"">Kazakhstan Comes On Strong</a>
</div>
<div class=""cnn2pxBpad"">&#8226;&nbsp;<a target=""new"" href=""http://www.time.com/time/nation/article/0,8599,1539992,00.html?cnn=yes"">Why the Fight Over Intelligence May Be a Wash</a>
</div>
</div></td><td><div class=""LinePad""><div class=""Line""></div></div></td><td><div class=""cnnPartner2""><?xml version=""1.0"" encoding=""UTF-8""?>
<div class=""cnnPartnersLft""><a target=""new"" href=""/cnnsi/""><img class=""cnnPartHead"" border=""0"" height=""17"" width=""138"" alt=""SI: "" src=""http://i.a.cnn.net/cnn/.element/img/1.0/main/partner_si.gif""/></a></div><div class=""cnnPartnersRt""><a href=""https://subs.timeinc.net/CampaignHandler/si_cnnsi?source_id=19"" target=""new"">Subscribe</a></div><br clear=""all""/><div class=""cnn2pxBpad"">&#8226;&nbsp;<a target=""new"" href=""/cnnsi/2006/writers/pete_mcentegart/09/28/ten.spot/index.html?cnn=yes"">10 Spot: Shining the light on T.O.'s inept publicist</a></div><div class=""cnn2pxBpad"">&#8226;&nbsp;<a target=""new"" href=""/cnnsi/scorecard/?cnn=yes"">Truth &amp; Rumors: Titans player says Young should start</a></div></div></td></tr><tr valign=""top"" class=""cnnPartnerBot""><td><div class=""cnnPartner3""><?xml version=""1.0"" encoding=""UTF-8""?>
<div class=""cnnPartnersLft""><a target=""new"" href=""/ew/""><img class=""cnnPartHead"" border=""0"" height=""17"" width=""123"" alt=""Entertainment Weekly: "" src=""http://i.a.cnn.net/cnn/.element/img/1.0/main/partner_ew.gif""/></a></div><div class=""cnnPartnersRt""><a href=""http://subs.timeinc.net/CampaignHandler/ewlinks?source_id=29"" target=""new"">Subscribe</a></div><br clear=""all""/><div class=""cnn2pxBpad"">&#8226;&nbsp;<a target=""new"" href=""/ewhome/article/commentary/0,6115,1540005_3_0_,00.html?cnn=yes"">Steel Cage! Pick your winners in 10 tough time slots</a></div><div class=""cnn2pxBpad"">&#8226;&nbsp;<a target=""new"" href=""/ewhome/inspirations/video?cnn=yes"">Video interview: Forest Whitaker on playing Idi Amin</a></div></div></td><td><div class=""LinePad""><div class=""Line""></div></div></td><td><div class=""cnnPartner4""><?xml version=""1.0"" encoding=""UTF-8""?>
<div class=""cnnPartnersLft""><a target=""new"" href=""http://www.cnn.com/money/index.html?cnn=yes""><img class=""cnnPartHead"" border=""0"" height=""17"" width=""111"" alt=""CNNMoney: "" src=""http://i.a.cnn.net/cnn/.element/img/1.0/main/partner_money.gif""/></a></div><div class=""cnnPartnersRt""><a href=""/money/services/bridge/contact.us.html"" target=""new"">Subscribe</a></div><br clear=""all""/><div class=""cnn2pxBpad"">&#8226;&nbsp;<a target=""new"" href=""http://money.cnn.com/magazines/moneymag/moneymag_archive/2006/10/01/8387562/index.htm?postversion=2006092717?cnn=yes"">Ringing up baby: What infants really cost</a></div><div class=""cnn2pxBpad"">&#8226;&nbsp;<a target=""new"" href=""http://money.cnn.com/2006/09/27/news/economy/newhomes/index.htm?postversion=2006092712?cnn=yes"">New home sales up, but weakness persists</a></div></div></td></tr></table></div> <table cellspacing=""0"" cellpadding=""0"" border=""0"" width=""694"" id=""cnnContextualLinks""><tr valign=""bottom""><td width=""80"" background=""http://i.a.cnn.net/cnn/.element/img/1.1/misc/cl/cl_bar.gif""><img src=""http://i.a.cnn.net/cnn/.element/img/1.1/misc/cl/advlinks2.gif"" width=""80"" height=""10"" alt="""" border=""0""></td><td align=""right"" background=""http://i.a.cnn.net/cnn/.element/img/1.1/misc/cl/cl_bar.gif""><a href=""javascript:CNN_openPopup('/services/overture/cl/frameset.exclude.html','620x430','toolbar=no,location=no,directories=no,status=no,menubar=no,scrollbars=no,resizable=no,width=620,height=430')""><img src=""http://i.a.cnn.net/cnn/.element/img/1.1/misc/cl/whatsthis_white_2.gif"" width=""58"" height=""10"" alt="""" border=""0""></a></td></tr><tr><td colspan=""2"" width=""100%""><div class=""cnnCLbox""><table cellspacing=""0"" cellpadding=""0"" border=""0""><tr valign=""top""><td width=""200"" class=""cnnCL"" style=""background-color:#DADADA; border:1px solid #999999;""><div id=""mainCLLinkSpots""></div></td><td width=""494"" class=""cnnCL"" style=""padding: 0;""><div id=""mainCLSponsoredLinks""></div></td></tr></table></div></td></tr></table><script type=""text/javascript"">if ( cnnEnableCL ) {if (location.hostname.indexOf('cnn.com') < 0) {cnnAddCSI( 'mainCLLinkSpots', '/.element/ssi/www/sect/1.3/misc/contextual/MAIN.html', '' );cnnAddCSI( 'mainCLSponsoredLinks', '/.element/ssi/www/sect/1.3/misc/contextual/MAIN-EMPTY.html', '' );}else {cnnAddCSI( 'mainCLLinkSpots', 'http://cl.cnn.com/ctxtlink/jsp/cnn/cl/1.3/cnn-linkspot-main.jsp', 'id=cnn_home&site=cnn_homepage_ctxt&origin=cnn' );cnnAddCSI( 'mainCLSponsoredLinks', '/.element/ssi/www/sect/1.3/misc/contextual/MAIN-EMPTY.html', '' );}}</script></td><td id=""cnnVDash""><img src=""http://i.cnn.net/cnn/images/1.gif"" width=""1"" height=""1"" hspace=""0"" vspace=""0"" border=""0"" alt=""""></td><td id=""cnnRightRail"" valign=""top""><!-- Begin AC360 -->

<div id=""cnnOnCnnTvBox"">
<div class=""cnnOnCnnTvBoxContent""><a href=""/CNN/Programs/anderson.cooper.360/""><img src=""http://i.a.cnn.net/cnn/.element/img/1.3/main/tv/ac360.jpg"" border=""0"" alt="""" width=""247"" height=""60"" border=""0""></a><div class=""cnnOnCnnTvBoxText"">What did the war in Lebanon accomplish? Is Hezbollah back in business?  ""360°"" goes back to the war zone.  </div>
	<div class=""cnnOnCnnTvBoxFooter"">
		<div class=""cnnOnCnnTvBoxShowTime""><img src=""http://i.a.cnn.net/cnn/.element/img/1.3/main/tv/10pm.gif"" border=""0"" alt="""" width=""28"" height=""19""></div>
		<div class=""cnnOnCnnTvBoxBtnRow""><!-- <a href=""javascript:cnnVideo('play','/video/bestoftv/2006/04/18/ac360.murder.in.belmont.cnn','2006/04/25');""><img src=""http://i.a.cnn.net/cnn/.element/img/1.3/main/tv/watch_preview_btn.gif"" border=""0"" alt=""Watch Preview"" width=""80"" height=""17"" style=""margin-right:4px;""></a> --><a href=""/CNN/Programs/""><img src=""http://i.a.cnn.net/cnn/.element/img/1.3/main/tv/schedule_btn.gif"" border=""0"" alt=""Full Schedule"" width=""80"" height=""17""></a></div>

	</div>
</div>
</div>

<!-- end AC360 --><div class=""cnn4pxBmar""><div class=""cnn4pxBmar""><div class=""cnnRRbox""><div class=""cnnRRcontent""><div class=""cnnBoxTitleR"">INSIDE THE INDIE SCENE</div><b><span class=""cnnWOOL"">Quiz: </span></b> <b><a href=""javascript:CNN_openPopup('/SPECIALS/2006/indie.scene/interactive/quiz.indie/frameset.exclude.html','770x576','toolbar=no,location=no,directories=no,status=no,menubar=no,scrollbars=no,resizable=no,width=770,height=576')"">How indie are you?</a><br></b><table cellpadding=""0"" cellspacing=""0"" border=""0"" class=""cnnRRblurb""><tr valign=""top""><td>    <a href=""javascript:CNN_openPopup('/SPECIALS/2006/indie.scene/interactive/quiz.indie/frameset.exclude.html','770x576','toolbar=no,location=no,directories=no,status=no,menubar=no,scrollbars=no,resizable=no,width=770,height=576')""><img src=""http://i.a.cnn.net/cnn/2006/images/09/25/tz.superchunk.gi.jpg"" alt=""How indie are you?"" width=""65"" height=""49"" align=""right"" hspace=""0"" vspace=""0"" border=""0""></a>Do you have what it takes to walk the walk and talk the talk in the indie scene?<div class=""cnnRRbullet"">&#8226;&nbsp;<a href=""/SPECIALS/2006/indie.scene/"">Special Report</a><br></div></td></tr></table>    </div></div></div><div class=""cnn4pxBmar""><div class=""cnnRRbox""><div class=""cnnRRcontent""><div class=""cnnBoxTitleR"">RUMSFELD: MAN OF WAR</div><b><a href=""/exchange/ireports/topics/forms/2006/09/manofwar.html"">Send us your thoughts</a><br></b><table cellpadding=""0"" cellspacing=""0"" border=""0"" class=""cnnRRblurb""><tr valign=""top""><td>    <a href=""/exchange/ireports/topics/forms/2006/09/manofwar.html""><img src=""http://i.a.cnn.net/cnn/2006/images/09/27/tz.rumsfeld2.ap.jpg"" alt=""Send us your thoughts"" width=""65"" height=""49"" align=""right"" hspace=""0"" vspace=""0"" border=""0""></a>How well do you feel Rumsfeld has performed? Should he stay on or resign?</td></tr></table>    </div></div></div><div class=""cnnRRbox""><div class=""cnnRRcontent""><div class=""cnnBoxTitleR"">TEDDY TERROR</div><b><a href=""/2006/US/09/26/killer.teddy.ap/index.html"">Cute, cuddly killer</a><br></b><table cellpadding=""0"" cellspacing=""0"" border=""0"" class=""cnnRRblurb""><tr valign=""top""><td>    <a href=""/2006/US/09/26/killer.teddy.ap/index.html""><img src=""http://i.a.cnn.net/cnn/2006/US/09/26/killer.teddy.ap/tz.teddy.ap.jpg"" alt=""Cute, cuddly killer"" width=""65"" height=""49"" align=""right"" hspace=""0"" vspace=""0"" border=""0""></a>A toy has been implicated in 2,500 deaths -- trout deaths, that is</td></tr></table>    </div></div></div><div><a href=""/WEATHER/""><img src=""http://i.a.cnn.net/cnn/.element/img/1.3/misc/hdr.wd.weather.gif"" width=""249"" height=""18"" border=""0"" hspace=""0"" vspace=""0"" alt=""""></a></div><table class=""cnnBorderedBox"" border=""0"" cellpadding=""0"" cellspacing=""0"" width=""249""><tr><td class=""cnnWeather""><div id=""weatherBox""></div> <script type=""text/javascript"">if(!(location.hostname.indexOf('cnn.com')>-1)) {cnnAddCSI('weatherBox','/.element/ssi/sect/1.3/MAIN/staticWeatherBox.html','');}else{ cnnAddCSI('weatherBox','http://cnn.dyn.cnn.com/weatherBox.html');}</script></td></tr><tr><td class=""cnnWeatherBot""><img src=""http://i.cnn.net/cnn/images/1.gif"" alt="""" border=""0"" height=""4"" hspace=""0"" vspace=""0"" width=""1""></td></tr><tr><td class=""cnnQV""><img src=""http://i.a.cnn.net/cnn/.element/img/1.3/misc/hdr.quickvote.gif"" width=""86"" height=""15"" hspace=""0"" vspace=""0"" alt=""quickvote""><form target=""popuppoll"" method=""post"" action=""http://polls.cnn.com/poll"">
<INPUT TYPE=HIDDEN NAME=""poll_id"" VALUE=""27661"">
<table cellpadding=""0"" cellspacing=""0"" border=""0"" class=""cnnPoll"">
	<tr>
		<td class=""cnnPollQ"">Are you surprised that Charlie Sheen is set to become the highest-paid sitcom star?</td>
	</tr>
	<tr>
		<td><input value=""1"" id=""cnnPollA1"" type=""radio"" name=""question_1""> <label for=""cnnPollA1"">Yes</label></td>
	</tr>
	<tr>
		<td><input value=""2"" id=""cnnPollA2"" type=""radio"" name=""question_1""> <label for=""cnnPollA2"">No</label></td>
	</tr>
<!-- /end Question 1 -->
	<tr>
		<td class=""cnnPollBtn"" align=""center""><input class=""cnnFormButton"" onclick=""CNN_openPopup('','popuppoll','toolbar=no,location=no,directories=no,status=no,menubar=no,scrollbars=no,resizable=no,width=770,height=567')"" value=""VOTE"" type=""SUBMIT""> or <a href=""javascript:CNN_openPopup('/POLLSERVER/results/27661.exclude.html','popuppoll','toolbar=no,location=no,directories=no,status=no,menubar=no,scrollbars=no,resizable=no,width=770,height=567')"">View Results</a></td>
	</tr>
</table>
</form>


     <table cellpadding=""0"" cellspacing=""0"" border=""0"" class=""cnnSponsor"" style=""width:153px"">
    <colgroup>
        <col width=""65"">
        <col width=""88"">
    </colgroup>
    <tr valign=""top"">
        <td class=""cnnSponsorTxt"">sponsored by:</td>
        <td><!-- ADSPACE: home/quickvote/sponsor.88x31 --><div align=""center"" style=""padding: 0; margin: 0; border: 0;""><script type=""text/javascript""> 

cnnad_createAd(""33090"",""http://ads.cnn.com/html.ng/site=cnn&cnn_pagetype=main&cnn_position=88x31_qv&cnn_rollup=homepage&params.styles=fs"",""31"",""88"");

</script></div></td>
    </tr>
</table></td></tr></table><div class=""cnn20pxTpad"">    <table cellspacing=""0"" cellpadding=""0"" border=""0"" width=""239"" id=""cnnRRad"">
	<tr valign=""bottom"">
		<td width=""98"" background=""http://i.a.cnn.net/cnn/.element/img/1.1/misc/cl/cl_bar.gif""><img src=""http://i.a.cnn.net/cnn/.element/img/1.5/main/featured.sponsors.gif"" width=""98"" height=""10"" alt="""" border=""0""></td>
		<td align=""right"" background=""http://i.a.cnn.net/cnn/.element/img/1.1/misc/cl/cl_bar.gif"" width=""241""><img src=""http://i.a.cnn.net/cnn/.element/img/1.5/main/sponsors.rt.corner.gif"" width=""6"" height=""10"" alt="""" border=""0""></td>
	</tr>
	<tr>
		<td colspan=""2"" class=""cnnRRadContent""><!-- ADSPACE: home/right1.120x90 --><div align=""center"" style=""padding: 0; margin: 0; border: 0;""><script type=""text/javascript""> 

cnnad_createAd(""34956"",""http://ads.cnn.com/html.ng/site=cnn&cnn_pagetype=main&cnn_position=120x90_rgt&cnn_rollup=homepage&params.styles=fs"",""90"",""120"");

</script></div>
		<div class=""divider""><img src=""http://i.a.cnn.net/cnn/.element/img/1.5/ceiling/ccc.gif"" alt="""" width=""235"" height=""1"" hspace=""0"" vspace=""0"" border=""0""></div>
		<!-- ADSPACE: home/right2.120x90 --><div align=""center"" style=""padding: 0; margin: 0; border: 0;""><script type=""text/javascript""> 

cnnad_createAd(""462479"",""http://ads.cnn.com/html.ng/site=cnn&cnn_pagetype=main&cnn_position=120x90_rgt2&cnn_rollup=homepage&params.styles=fs"",""90"",""120"");

</script></div></td>
	</tr>
</table></div></td></tr></table><div id=""footer""><div class=""cnn25pxT10BPad""><div id=""cnnFootNav"">
<table width=""100%"" border=""0"" cellpadding=""0"" cellspacing=""0"">
	<tr valign=""middle"">
		<td class=""cnnFootNavPadL""><a href=""http://edition.cnn.com"" onClick=""clickEdLink()"">International Edition</a></td>
		<td>
			<form>
				<select title=""CNN.com is available in different languages"" name=""languages"" size=""1"" onChange=""if (this.options[selectedIndex].value != '') location.href=this.options[selectedIndex].value"" class=""cnnFormSelectSm"">
					<option value="""" disabled selected>Languages</option>
					<option value="""" disabled>---------</option>
					<option value=""http://arabic.cnn.com/"">Arabic</option>				
					<option value=""http://www.CNN.co.jp/"">Japanese</option>
					<option value=""http://www.joins.com/cnn/"">Korean</option>
					<option value=""http://cnnturk.com/"">Turkish</option>				
				</select>
			</form>
		</td>
		<td><a href=""/CNN/Programs/"">CNN TV</a></td>
		<td><a href=""/CNNI/"">CNN International</a></td>
		<td><a href=""/HLN/"">Headline News</a></td>
		<td><a href=""/TRANSCRIPTS/"">Transcripts</a></td>
		<td><a href=""/services/advertise/"">Advertise with Us</a></td>
		<td><a href=""/INDEX/about.us/"">About Us</a></td>
<td class=""cnnFootNavPadR""><a href=""/feedback/"">Contact Us</a></td>
	</tr>
</table>
</div>
<table id=""cnnFootSearch"" border=""0"" cellpadding=""0"" cellspacing=""0"" width=""100%"">
	<tr valign=""middle""><form action=""http://search.cnn.com/cnn/search"" method=""get"" onsubmit=""return CNN_validateSearchForm(this);"">
		<td><input name=""source"" value=""cnn"" type=""hidden""><input name=""invocationType"" value=""search/bottom"" type=""hidden""><img src=""http://i.a.cnn.net/cnn/.element/img/1.5/ceiling/search/hdr_search.gif"" alt=""Search"" class=""cnnSrch"" height=""14"" width=""71""><img src=""http://i.a.cnn.net/cnn/images/1.gif"" alt="""" border=""0"" height=""30"" hspace=""15"" vspace=""0"" width=""2""><input name=""sites"" value=""web"" checked=""checked"" class=""cnnR"" type=""radio""><img src=""http://i.a.cnn.net/cnn/.element/img/1.5/ceiling/search/hdr_the_web.gif"" alt="""" class=""cnnWeb"" height=""6"" width=""39""><input name=""sites"" value=""cnn"" class=""cnnR"" type=""radio""><img src=""http://i.a.cnn.net/cnn/.element/img/1.5/ceiling/search/hdr_cnn_com.gif"" alt="""" class=""cnnCNN"" height=""6"" width=""39""><input name=""query"" value="""" title=""Enter text to search for and click 'Search'"" size=""60"" maxlength=""80"" class=""cnnInput"" type=""text""><input value=""Search"" class=""cnnFormButtonSearch"" type=""submit""><img src=""http://i.a.cnn.net/cnn/.element/img/1.5/ceiling/search/hdr_yahoo.gif"" alt="""" class=""cnnYahoo"" height=""13"" width=""164""></form></td>
	</tr>
</table>
<table width=""100%"" border=""0"" cellpadding=""0"" cellspacing=""10"" id=""cnnFoot"">
	<colgroup>
		<col width=""271"">
		<col width=""1"">
		<col width=""616"">
		<col width=""1"">
		<col width=""91"">
	</colgroup>
	<tr valign=""top"">
		<td><b>&copy; 2006 Cable News Network LP, LLLP.</b><br>A Time Warner Company. All Rights Reserved.<br><a href=""/interactive_legal.html"">Terms</a> under which this service is provided to you.<br>Read our <a href=""/privacy.html"">privacy guidelines</a>. <a href=""/feedback/"">Contact us</a>.</td>
		<td valign=""middle""><img src=""http://i.a.cnn.net/cnn/.element/img/1.3/floor/dots.gif"" width=""1"" height=""64"" hspace=""0"" vspace=""0"" border=""0"" alt=""""></td>
		<td>
			<table border=""0"" cellpadding=""0"" cellspacing=""0"" id=""cnnIconMap"">
				<tr>
					<td valign=""bottom"" class=""cnn4pxRpad"">
					<span class=""cnnFooterSrvHead"">SERVICES &#187;</span> <span class=""cnnFooterServiceLinks""><a href=""/EMAIL/"" style=""margin-right:20px;"">Emails</a> <a href=""/services/rss/"">RSS</a><a href=""/services/rss/""><img src=""http://i.a.cnn.net/cnn/.element/img/1.3/floor/icon.wd.xml.gif"" alt=""RSS Feed"" border=""0"" height=""11"" width=""24"" class=""cnnFooterXmlBtn"" ></a> <a href=""/services/podcasting/"">Podcasts</a><a href=""/services/podcasting/""><img src=""http://i.a.cnn.net/cnn/.element/img/1.3/floor/icon.wd.pod.gif"" alt=""Radio News Icon"" border=""0"" height=""11"" width=""23"" class=""cnnFooterXmlBtn"" ></a> <a href=""/togo/"" style=""margin-right:20px;"">CNNtoGo</a> <a href=""/pipeline/"" style=""margin-right:20px;"">CNN Pipeline</a> <a href=""http://www.costore.com/turnerstoreonline/welcome.asp"" target=""new"">CNN Shop</a></span>
					</td>
				</tr>
				<tr>
					<td valign=""bottom"" class=""cnn4pxRpad""><img src=""http://i.a.cnn.net/cnn/.element/img/1.3/misc/icon.offsite.gif"" alt=""Offsite Icon"" width=""12"" height=""9"" border=""0"">
					External sites open in new window; not endorsed by CNN.com</td>
				</tr>
				<tr>
					<td valign=""bottom"" class=""cnn4pxRpad""><img src=""http://i.a.cnn.net/cnn/.element/img/1.3/floor/icon.wd.pipe.gray.gif"" alt=""Pipeline Icon"" width=""47"" height=""9"" border=""0"">
					Pay service with live and archived video. <a href=""/pipeline/"">Learn more</a></td>
				</tr>
			</table>
		</td>
		<td valign=""middle""><img src=""http://i.a.cnn.net/cnn/.element/img/1.3/floor/dots.gif"" width=""1"" height=""64"" hspace=""0"" vspace=""0"" border=""0"" alt=""""></td>
		<td valign=""middle""><a href=""http://www.novell.com/products/"" target=""new""><img src=""http://i.a.cnn.net/cnn/.element/img/1.3/main/novell.gif"" width=""88"" height=""31"" border=""0"" hspace=""0"" vspace=""0"" alt=""Novell""></a></td>
	</tr>
</table>

<style type=""text/css""> #cnnFooterAttri
{padding:6px 10px;margin-top:3px;background:#efefef;font-size:10px;}
</style>
<div id=""cnnFooterAttri"">
&copy; 2006 <a href=""http://www.bigcharts.com"" target=""new"">BigCharts.com</a> Inc. All rights reserved. Please see our <a href=""http://www.bigcharts.com/custom/docs/useragreement2.asp"" target=""new"">Terms of Use</a>.<br>
MarketWatch, the MarketWatch logo, and BigCharts are registered trademarks of MarketWatch, Inc.<br>
Intraday data is at least 15-minutes delayed. All Times are ET.<br>
Intraday data provided by <a href=""http://www.comstock-interactivedata.com"" target=""new"">ComStock</a>, an Interactive Data Company and subject to the <a href=""http://custom.marketwatch.com/custom/docs/comstock-terms.asp"" target=""new"">Terms of Use</a>. Historical, current end-of-day data, and splits data provided by <a href=""http://www.ftinteractivedata.com"" target=""new"">FT Interactive Data</a>.
</div></div></div><div id=""csiIframe""></div>    
<img src=""http://cnn.dyn.cnn.com/cookie.crumb"" alt="""" width=""1"" height=""1"" >
<!-- ADSPACE: home/bottom.1x1 --><div align=""center"" style=""padding: 0; margin: 0; border: 0;""><script type=""text/javascript"">


//Checks for google in query string - if 'google', no popunder, else, launch popunder
function check_for_google(variable) {
var query = window.location.search.substring(1);
var vars = query.split(""&"");
for (var i=0;i<vars.length;i++) {
var pair = vars[i].split(""="");
if (pair[0] == variable) {
return pair[1];
}
}
//alert('Query Variable ' + variable + ' not found');
} 

referrer = check_for_google(""ref"");
if(referrer == ""google""){
//alert(""yep""); 
}


else{
//alert(""nope"");
cnnad_createAd(""565468"",""http://ads.cnn.com/html.ng/site=cnn&cnn_pagetype=main&cnn_position=1x1_bot&cnn_rollup=homepage&params.styles=fs"",""1"",""1"");

}

</script></div>

<img src=""http://i.a.cnn.net/cnn/1.gif"" alt="""" id=""TargetImage"" name=""TargetImage"" width=""1"" height=""1"" onLoad=""getAdHeadCookie(this)"">

<img src=""http://i.a.cnn.net/cnn/1.gif"" alt="""" id=""TargetImageDE"" name=""TargetImageDE"" width=""1"" height=""1"" onLoad=""getDEAdHeadCookie(this)"">	

<img src=""http://leadback.advertising.com/adcedge/lb?site=695501&srvc=1&betr=alcnn_cs=1&betq=2374=369897"" width=""1"" height=""1"" border=""0"">

<script language=""JavaScript1.1"" src=""http://ar.atwola.com/file/adsEnd.js""></script>
<script language=""JavaScript"" src=""http://i.cnn.net/cnn/.element/ssi/js/1.3/flash_detect.js""></script>
<script language=""JavaScript1.2"" src=""http://i.a.cnn.net/cnn/.element/ssi/js/1.3/omniture.js""></script>
<!-- SiteCatalyst code version: H.1.
Copyright 1997-2005 Omniture, Inc. More info available at
http://www.omniture.com -->
<script language=""JavaScript"" src=""http://i.a.cnn.net/cnn/.element/ssi/js/1.3/s_code.js""></script>
<script language=""JavaScript""><!--
/* You may give each page an identifying name, server, and channel on
the next lines. */
s.pageName=pageName;
s.server="""";
s.channel=channel;
s.pageType="""";
if (typeof(cnnAuthor) != ""undefined"") {
    s.prop1=cnnAuthor;
} else {
    s.prop1="""";
}
s.prop2=location.hostname;
if (typeof(cnnBrandingValue) != ""undefined"") {
    s.prop3=cnnBrandingValue;
} else {
    s.prop3="""";
}
if (typeof(cnnStoryDate) != ""undefined"") {
    s.prop4=cnnStoryDate;
} else {
    s.prop4="""";
}
if (typeof(cnnStoryHeadline) != ""undefined"") {
    s.prop5=cnnStoryHeadline;
} else {
    s.prop5="""";
}
if (typeof(CNN_getCookies) != ""undefined"") {
var allCookies = CNN_getCookies();
var adHeadCookie = allCookies[""firstName""] || null;
if ( adHeadCookie ) {
    s.prop6=""member"";
	s.eVar3=""member"";
} else {
    s.prop6=""non-member"";
	s.eVar3=""non-member"";
}
} else {
    s.prop6="""";
	s.eVar3="""";
}
if (typeof(cnnCurrHour) != ""undefined"") {
 s.prop7=cnnCurrHour;
} else {
 s.prop7="""";
}
if (typeof(cnnRefString) != ""undefined"") {
    s.prop8=cnnRefString;
} else {
    s.prop8="""";
}
s.prop9 = parseInt(CNN_FlashDetect.prototype.getVersion());

/************* DO NOT ALTER ANYTHING BELOW THIS LINE ! **************/
var s_code=s.t();if(s_code)document.write(s_code)//--></script>
<script language=""JavaScript""><!--
if(navigator.appVersion.indexOf('MSIE')>=0)document.write(unescape('%3C')+'\!-'+'-')
//--></script><noscript><img
src=""http://cnnglobal.122.2O7.net/b/ss/cnnglobal/1/H.1--NS/0""
height=""1"" width=""1"" border=""0"" alt="""" /></noscript><!--/DO NOT REMOVE/-->
<!-- End SiteCatalyst code version: H.1. -->




</body></html>";
            XmlDocument doc;

            doc = HtmlParser.Parse(source);
            Assert.IsNotNull(doc["root"]["html"]["head"]);
            Assert.IsNotNull(doc["root"]["html"]["body"]);
        }

        [TestMethod]
        [TestProperty("Lib", "LillTek.Advanced")]
        public void HtmlParser_Parse_Amazon()
        {
            // Generate a DOM source scraped from www.google.com and 
            // make sure we don't see an exception.

            const string source =
@"

<html>
  <head>
    
    
    
  
<style type=""text/css""><!--
BODY { font-family: verdana,arial,helvetica,sans-serif; font-size: x-small; background-color: #FFFFFF; color: #000000; margin-top: 0px; }
TD, TH { font-family: verdana,arial,helvetica,sans-serif; font-size: x-small; }
a:link { font-family: verdana,arial,helvetica,sans-serif; color: #003399; }
a:visited { font-family: verdana,arial,helvetica,sans-serif; color: #996633; }
a:active { font-family: verdana,arial,helvetica,sans-serif; color: #FF9933; }
.serif { font-family: times,serif; font-size: small; }
.sans { font-family: verdana,arial,helvetica,sans-serif; font-size: small; }
.small { font-family: verdana,arial,helvetica,sans-serif; font-size: x-small; }
.h1 { font-family: verdana,arial,helvetica,sans-serif; color: #CC6600; font-size: small; }
.h3color { font-family: verdana,arial,helvetica,sans-serif; color: #CC6600; font-size: x-small; }
.tiny { font-family: verdana,arial,helvetica,sans-serif; font-size: xx-small; }
.listprice { font-family: arial,verdana,helvetica,sans-serif; text-decoration: line-through; }
.attention { background-color: #FFFFD5; }
.price { font-family: arial,verdana,helvetica,sans-serif; color: #990000; }
.tinyprice { font-family: verdana,arial,helvetica,sans-serif; color: #990000; font-size: xx-small; }
.highlight { font-family: verdana,arial,helvetica,sans-serif; color: #990000; font-size: x-small; } 
.alertgreen { color: #009900; font-weight: bold; }
.alert { color: #FF0000; font-weight: bold; }
.topnav { font-family: verdana,arial,helvetica,sans-serif; font-size: 12px; text-decoration: none; }
.topnav a:link, .topnav a:visited { text-decoration: none; color: #003399; }
.topnav a:hover { text-decoration: none; color: #CC6600; }
.topnav-active a:link, .topnav-active a:visited { font-family: verdana,arial,helvetica,sans-serif; font-size: 12px; color: #CC6600; text-decoration: none; }
.eyebrow { font-family: verdana,arial,helvetica,sans-serif; font-size: 10px; font-weight: bold;text-transform: uppercase; text-decoration: none; color: #FFFFFF; }
.eyebrow a:link { text-decoration: none; }
.popover-tiny { font-size: xx-small; font-family: verdana,arial,helvetica,sans-serif; }
.popover-tiny a, .popover-tiny a:visited { text-decoration: none; color: #003399; }
.popover-tiny a:hover { text-decoration: none; color: #CC6600; }
.tabon a:hover, .taboff a:hover { text-decoration: underline; }
.tabon div, .taboff div { margin-top: 7px; margin-left: 9px; margin-bottom: 5px; }
.tabon a, .tabon a:visited  { font-size: 10px; color: #FFCC66; font-family: verdana,arial,helvetica,sans-serif; text-decoration: none; text-transform: uppercase; font-weight: bold; line-height: 10px; }
.taboff a, .taboff a:visited { font-size: 10px; color: #000000; font-family: verdana,arial,helvetica,sans-serif; text-decoration: none; text-transform: uppercase; font-weight: bold; line-height: 10px; }
.indent { margin-left: 1em; }
.half { font-size: .5em; }
.list div { margin-bottom: 0.25em; text-decoration: none; }
.hr-center { margin: 15px; border-top-width: 1px; border-right-width: 1px; border-bottom-width: 1px; border-left-width: 1px; border-top-style: dotted; border-right-style: none; border-bottom-style: none; border-left-style: none; border-top-color: #999999; border-right-color: #999999; border-bottom-color: #999999; border-left-color: #999999; }
.horizontal-search { font-weight: bold; font-size: x-small; color: #FFFFFF; font-family: verdana,arial,helvetica,sans-serif; }
.horizontal-websearch { font-size: xx-small; font-family: verdana,arial,helvetica,sans-serif; padding-left: 12px; }
.big { font-size: x-large; font-family: verdana,arial,helvetica,sans-serif; }
.amabot_right .h1 { color: #c60; font-size: .92em; }
.amabot_right .amabot_widget .headline, .amabot_left .amabot_widget .headline { color: #c60; font-size: .92em; display: block; font-weight: bold; }
.amabot_widget .headline { color: #c60; font-size: small; display: block; font-weight: bold; }
.amabot_left .h1 { color: #c60; font-size: .92em; }
.amabot_left .amabot_widget, .amabot_right .amabot_widget, .tigerbox {  padding-top: 8px;  padding-bottom: 8px;  padding-left: 8px;  padding-right: 8px;  border-bottom: 1px solid #ADD2E2;   border-left: 1px solid #ADD2E2;  border-right: 1px solid #ADD2E2;  border-top: 1px solid #ADD2E2; }
.amabot_center {  font-size: 12px; }
.amabot_right {  font-size: 12px; }
.amabot_left {  font-size: 12px; }
.rightArrow { color: #c60; font-weight: bold; padding-right: 6px; }
.nobullet { list-style-type: none }
.homepageTitle { font-size: 28pt; font-family: 'Arial Bold', Arial; font-weight: 800; font-variant: normal; font-style: bold; color: #80B6CE; }
--></style>
<style type=""text/css"">
<!--
.leftNav { font-family: tahoma, sans-serif; 	margin-bottom: 5px; margin-left: 6px; line-height: 1em;
}
.leftNavTitle { font-family: tahoma, sans-serif; margin-top: 10px;
 margin-bottom: 6px; color: #c60; font-weight: bold; line-height: 1em;
}
.hr-leftbrowse { border-top-width: 1px;	border-right-width: 1px;	border-bottom-width: 1px; border-left-width: 1px;
 border-top-style: dashed; border-right-style: none; border-bottom-style: none; border-left-style: none; 
 border-top-color: #999999; border-right-color: #999999; border-bottom-color: #999999; border-left-color: #999999;
 margin-top: 10px; margin-right: 5px; margin-bottom: 0px; margin-left: 5px;
}
.leftNav a:link, .leftNav a:visited, .amabot_left .amabot_widget a:link, .amabot_left .amabot_widget a:visited  { text-decoration: none; 
	font-family: tahoma, sans-serif;
}
.leftNav a:hover,  .amabot_left .amabot_widget a:hover { color: #c60; text-decoration: underline; }
}
-->
</style>
<script language=""Javascript1.1"" type=""text/javascript"">
<!--
function amz_js_PopWin(url,name,options){
  var ContextWindow = window.open(url,name,options);
  ContextWindow.focus();
  return false;
}
//-->
</script>
<title>Amazon.com: Online Shopping for Electronics, Apparel, Computers, Books,  DVDs & more</title>
<meta name=""description"" content=""Online shopping from the earth's biggest selection of books, magazines, music, DVDs, videos, electronics, computers, software, apparel & accessories, shoes, jewelry, tools & hardware, housewares, furniture, sporting goods, beauty & personal care, broadband & dsl, gourmet food & just about anything else."" />
<meta name=""keywords"" content=""Amazon, Amazon.com, Books, Online Shopping, Book Store, Magazine, Subscription, Music, CDs, DVDs, Videos, Electronics, Video Games, Computers, Cell Phones, Toys, Games, Apparel, Accessories, Shoes, Jewelry, Watches, Office Products, Sports & Outdoors, Sporting Goods, Baby Products, Health, Personal Care, Beauty, Home, Garden, Bed & Bath, Furniture, Tools, Hardware, Vacuums, Outdoor Living, Automotive Parts, Pet Supplies, Broadband, DSL"" />
  </head>
  <body>
    
    
    
  
<a href=""http://www.amazon.com/access""><img src=""http://images.amazon.com/images/G/01/x-locale/common/transparent-pixel.gif"" align=""left"" alt=""A different version of this web site containing similar content optimized for screen readers and mobile devices may be found at the web address: www.amazon.com/access"" border=""0""/></a>
<a name=""top""></a>
    
<style type=""text/css"">
<!--
.subnav { 
	font-family: Verdana, Arial, Helvetica, sans-serif;
	font-size: 9px;
	line-height: 10px;
	font-weight: bold;
	text-transform: uppercase;
	color: #FFFFFF ;
	}
A.subnav:link { 
	text-decoration: none;
	color: #FFFFFF ;
	}
A.subnav:hover { 
	text-decoration: underline;
	color: #FFFFFF ;
	}
A.subnav:visited {
	text-decoration: none;
	color: #FFFFFF ;
}
.currentlink {
	font-family: Verdana, Arial, Helvetica, sans-serif;
	font-size: 9px;
	line-height: 10px;
	font-weight: bold;
	text-transform: uppercase;
	color: #FFCC66;
	}
A.currentlink:link {
	text-decoration: none;
	color: #FFCC66;
	}	
A.currentlink:hover { 
	text-decoration: underline;
	color: #FFCC66;
	}
A.currentlink:visited { 
	text-decoration: none;
	color: #FFCC66;
	}
-->
</style>
<style type=""text/css"">
<!--
.header,
.header a:link, 
.header a:active, 
.header a:visited, 
.secondary a,
.searchtitle,
.secondsearch .title,
.gcsecondsearch .title,
.secondsearch2 .title {
  font-family: tahoma,sans-serif;
  color: #333;
  font-size: 11px;
  line-height: 11px;
  text-decoration: none;
}
.tabs a:link, .tabs a:visited, .tabs a:active, .header .navspacer {
  font-size: 11px;
  line-height: 11px;
  color: #333;
  text-decoration: none;
  font-family: tahoma, sans-serif;
}
.tabs .tools a:link,
.tabs .tools a:visited,
.tabs .tools a:active  {
	color: #039;
	font-size: 12px;
}
.tools .h3color  {
	color: #c60;
	font-size: 12px;
}
.tabs .tools a:hover {
  text-decoration: underline;
	color: #c60;
}
.tools a:link.on, .tools a:visited.on, .tools a:active.on {
 color: #c60;
}
.secondary a:link,
.secondary a:visited,
.secondary a:active {
  text-decoration: none;
  text-transform: capitalize;
  color: #333;
}
.searchtitle {
	font-size: 14px;
	font-weight: bold;
	color: white;
}
.secondsearch .title, .gcsecondsearch .title, .secondsearch2 .title {
	font-size: 11px;
	color: black;
}
.header .secondary {
	background-image:url(""http://ec1.images-amazon.com/images/G/01/nav2/images/skins/teal/subnav-bg.gif"");
}
.secondary td {
  background-image: none;
}
.secondary .secondsearch {
	background-image:url(""http://ec1.images-amazon.com/images/G/01/nav2/images/skins/teal/subnav-secondary-bg.gif"");
	background-repeat: no-repeat;
}
.secondary .secondsearch2 {
	background-image:url(""http://ec1.images-amazon.com/images/G/01/nav2/images/skins/teal/subnav-secondary-bg-lite.gif"");
	background-repeat: no-repeat;
}
.secondary .gcsecondsearch {
	background-repeat: no-repeat;
}
.header .tabs {
	background-image:url(""http://ec1.images-amazon.com/images/G/01/nav2/images/skins/teal/tabs-line.gif"");
}
.tabs .leftoff {
	background-image:url(""http://ec1.images-amazon.com/images/G/01/nav2/images/skins/teal/tabs-left-off.gif"");
	background-repeat: no-repeat;
}
.tabs .lefton {
	background-image:url(""http://ec1.images-amazon.com/images/G/01/nav2/images/skins/teal/tabs-left-on.gif"");
	background-repeat: no-repeat;
}
.tabs .middleoff {
	background-image:url(""http://ec1.images-amazon.com/images/G/01/nav2/images/skins/teal/tabs-middle-off.gif"");
	background-repeat: no-repeat;
}
.tabs .middleon {
	background-image:url(""http://ec1.images-amazon.com/images/G/01/nav2/images/skins/teal/tabs-middle-on.gif"");
	background-repeat: no-repeat;
	white-space: nowrap;
}
.tabs .middleoffonleft {
	background-image:url(""http://ec1.images-amazon.com/images/G/01/nav2/images/skins/teal/tabs-middle-off-onleft.gif"");
	background-repeat: no-repeat;
}
.tabs .rightoff {
	background-image:url(""http://ec1.images-amazon.com/images/G/01/nav2/images/skins/teal/tabs-right-off.gif"");
	background-repeat: no-repeat;
}
.tabs .righton {
	background-image:url(""http://ec1.images-amazon.com/images/G/01/nav2/images/skins/teal/tabs-right-on.gif"");
	background-repeat: no-repeat;
}
.tabs .middleoff a, .tabs .middleon a, .tabs .middleoffonleft a {
    display: block;
}
.tabs div {
	margin-left: 23px;
}
.leftoff div, .lefton div {
	margin-left: 13px;
}
.secondary .on, .tertiary .on {
    font-weight: bold;
}
.header .tertiary, .tertiary .navspacer {
	background-color: #006699;
	color: white;
}
.tertiary a:link,
.tertiary a:visited,
.tertiary a:active {
  text-decoration: none;
  text-transform: capitalize;
  color: white;
}
.tertiary .secondsearch, .tertiary .secondsearch2 {
	background-image:url(""http://ec1.images-amazon.com/images/G/01/nav2/images/skins/teal/subnav-secondary-bg-tan.gif"");
	background-repeat: no-repeat;
}
.tertiary .gcsecondsearch {
	background-repeat: no-repeat;
}
.secondary .gca9websearch {
        background-image:url(""http://ec1.images-amazon.com/images/G/01/nav2/images/skins/teal/a9-table-bg-1._V51437488_.gif"");
	background-color:#f4f4e3;
	border-bottom:1px solid #757549;
	border-top:1px solid #a0a078;
}
.secondary .advsearch {
  line-height: 11px;
}
.advsearch a:link, .advsearch a:visited, .advsearch a:active {
  color: white;
  font-size: 11px;
  text-decoration: none;
}
.header a:hover,
.header .tabs a:hover,
.secondary a:hover,
.tertiary a:hover,
.advsearch a:hover,
.popover-tiny a:hover {
	text-decoration: underline;
}
.popover-tiny a:visited {
 color: #963;
}
-->
</style>
<link href='http://g-images.amazon.com/images/G/01/nav2/gamma/n2CoreLibs/n2CoreLibs-n2v1-57871.css' type='text/css' rel='stylesheet'>
<script type='text/javascript'>
//! ======= JSF Bootstrap (1) =======
// $Revision: #13 $
var gbN2Loaded = N2Loaded = false;
var n2LMStart = new Date();
var gaN2JSLibs = [];
var gaN2JSLibPaths = [];
var gaN2JSLibIds = [];
var gaN2CSSLibs = [];
var gaN2CSSLibPaths = [];
var n2sRTW1='onload';
var n2sRTWTBS='simplepopoverloaded';
var goN2Initializer = {
      aHandlers: [],
      aEventsRun: [],
      bCoreLoaded: false,
	runThisWhen: function (sWhen, fFn, sComment) {
	  if ( (typeof fFn != 'function') || fFn == null) return false;
	  sWhen = sWhen.toLowerCase();
	
	  this.aHandlers[this.aHandlers.length] = { sWhen: sWhen, fFn: fFn, sComment: sComment };
	  return true
	},
	run: function() {},
	isReady: function() {return false;}
};
goN2Initializer.initializeThis = goN2Initializer.runThisWhen;
function n2RunThisWhen(sWhen, fFn, sComment) {
  goN2Initializer.runThisWhen(sWhen, fFn, sComment);
}
function n2RunIfLoaded(sLibID, fFn, sComment) {
	goN2Initializer.runThisWhen(sLibID+'loaded', fFn, 'sequenced init of '+ sComment);
}
var goN2LibMon = {
	aLibs: {},
	nMONITORLOAD: -1,
	monitorLoad: function (sLibID) {
		this.aLibs[sLibID] = { sID: sLibID, nDuration: this.nMONITORLOAD };
	},
	stats: function() {}
};
//! ======= JSF Bootstrap (2) =======
gsN2ImageHost='http://g-images.amazon.com/images/G/01/';
var goCust = new Object();
goCust.isLoggedIn = function() { return false; }
n2RunThisWhen(n2sRTWTBS, 
              function() {
                  oAllCatPopover = new N2SimplePopover();
                  goN2Events.registerFeature('two-tabs', 'oAllCatPopover', 'n2MouseOverHotspot', 'n2MouseOutHotspot'); 
                  goN2Events.setFeatureDelays('two-tabs',200, 400, 200);
                  oAllCatPopover.initialize('AllCatPopoverDiv', 'oAllCatPopover',null,null,'below','c'); 
              }, 
              'All Categories popover');
n2RunThisWhen(n2sRTW1, 
  function() {
}, ""n2CoreLibsExt Init "");
gaN2JSLibPaths.push(
    'http://g-images.amazon.com/images/G/01/nav2/gamma/n2CoreLibs/n2CoreLibs-utilities-25439.js',
    'http://g-images.amazon.com/images/G/01/nav2/gamma/n2CoreLibs/n2CoreLibs-events-5044.js',
    'http://g-images.amazon.com/images/G/01/nav2/gamma/n2CoreLibs/n2CoreLibs-simplePopover-15418.js');
gaN2JSLibIds.push(
    'utilities',
    'events',
    'simplePopover');
(function()
{
  var i;
  var sTags = """";
  var bIsSafari = navigator.userAgent.match(/Safari/);
  for (i in gaN2CSSLibPaths)
  {
    sTags += '<link href=""'+gaN2CSSLibPaths[i]+'"" type=""text/css"" rel=""stylesheet"">\n';
  }
  for (i in gaN2JSLibPaths)
  {
    goN2LibMon.monitorLoad(gaN2JSLibIds[i]);
    var sScript = '<script src=""'+gaN2JSLibPaths[i]+'"" type=""text/javascript""><\/script>\n';
    if (bIsSafari) document.write(sScript);
    else sTags += sScript;
  }
  document.write(sTags);
}());
gaN2CSSLibPaths.push(
  'http://g-images.amazon.com/images/G/01/nav2/gamma/n2CoreLibs/n2CoreLibs-n2v1-57871.css' );
n2LLStop = new Date();
//! ======= JSF Bootstrap (End) =======
</script>
      <table width=""100%"" border=""0"" cellpadding=""0"" cellspacing=""0"" class=""header"" style=""margin-top:5px"">
        <tr>
          <td class=""tabs"">
            <table border=""0"" cellspacing=""0"" cellpadding=""0"" align=""center"">
              <tr id=""twotabtop"">
                
<td class=""lefton"" height=""33""><div><a href=""/ref=topnav_gw_gw/002-3729295-8812824""><img src=""http://ec1.images-amazon.com/images/G/01/nav2/images/skins/teal/logo-on.gif"" width=""109"" height=""25"" border=""0"" /></a></div></td><td class=""middleoffonleft"" height=""33""><div align=""center""><a href=""/gp/yourstore/home/ref=topnav_ys_gw/002-3729295-8812824"" >Your<br />Store</a></div></td>
<td class=""middleoff"" height=""33""><div align=""center""><a href=""/exec/obidos/subst/home/all-stores.html/ref=topnav_dir_gw/002-3729295-8812824"" name=""two-tabs|he|all-categories"">See All&nbsp;35<br />Product&nbsp;Categories</a></div></td><td class=""rightoff"" width=""15"" height=""33""><img src=""http://ec1.images-amazon.com/images/G/01/x-locale/common/transparent-pixel.gif"" width=""15"" height=""8"" border=""0"" /></td>
                
<td class=""tools"" nowrap=""nowrap"" height=""33"">&nbsp;&nbsp;<a href=""/gp/css/homepage.html/ref=topnav_ya_gw/002-3729295-8812824?ie=UTF8"" >Your Account</a> <span class=""navspacer""><span class=""light"">&#124;</span></span> <a href=""/gp/cart/view.html/ref=topnav_cart_gw/002-3729295-8812824?ie=UTF8"" ><img src=""http://ec1.images-amazon.com/images/G/01/x-locale/common/icons/topnav-cart.gif"" width=""19"" alt=""Cart"" height=""16"" border=""0"" /></a> <a href=""/gp/cart/view.html/ref=topnav_cart_gw/002-3729295-8812824?ie=UTF8"" >Cart</a> <span class=""navspacer""><span class=""light"">&#124;</span></span> <a href=""/gp/lists/homepage.html/ref=topnav_lists_gw/002-3729295-8812824?ie=UTF8""  id=""lolPop_1"" name=""lolPop|he|listoflists_data"">Your Lists</a> <span class=""navspacer""><span class=""light"">&#124;</span></span> 
<style type=""text/css"">
.lol-hr-center { margin: 5px; border-top-width: 1px; border-right-width: 1px; border-bottom-width: 1px; border-left-width: 1px; 
border-top-style: dotted; border-right-style: none; border-bottom-style: none; border-left-style: none; border-top-color: #999999; 
border-right-color: #999999; border-bottom-color: #999999; border-left-color: #999999; 
}
</style> 
<div id=""listoflists_data"" style=""display:none"">
<div style=""padding:8px; border:1px solid #ACA976; background-color:#FFFFFF; text-transform: none"">
<div class=""popover-tiny"">
<div nowrap=""nowrap""><a href=""/gp/registry/wishlist/ref=yourlists_pop_1/002-3729295-8812824"">Wish List</a></div><div nowrap=""nowrap""><a href=""/gp/gift-central/organizer/ref=yourlists_pop_2/002-3729295-8812824"">Gift Idea List</a></div><div class=""lol-hr-center""></div><div nowrap=""nowrap""><a href=""/gp/rsl/shoppinglist/yourshoppinglist/ref=yourlists_pop_3/002-3729295-8812824"">Shopping List</a></div><div class=""lol-hr-center""></div><div nowrap=""nowrap""><a href=""/gp/registry/wedding-homepage.html/ref=yourlists_pop_4/002-3729295-8812824"">Wedding Registry</a></div><div nowrap=""nowrap""><a href=""/gp/registry/babyreg/ref=yourlists_pop_5/002-3729295-8812824"">Baby Registry</a></div>
</div>
</div>      
</div>
<script type=""text/javascript"">//<![CDATA[
  n2RunThisWhen(
    n2sRTWTBS,
    function() {
      goLolPop = new N2SimplePopover();
      goN2Events.registerFeature('lolPop', 'goLolPop', 'n2MouseOverHotspot', 'n2MouseOutHotspot');
      goN2Events.setFeatureDelays('lolPop', 200, 400, 200);
      goLolPop.initialize('lolPopDiv', 'goLolPop', null, null, 'below', 'c');
      goN2U.insertAdjacentHTML(document.getElementById('lolPop_1'), 'beforeEnd', '&nbsp\;<img src=""http://ec1.images-amazon.com/images/G/01/x-locale/common/icons/drop-down-icon-small-arrow.gif"" width=""11"" style=""margin:0px 2px -1px 4px;"" height=""11"" border=""0"" />');
    },
    'Your Lists popover' );
//]]></script>
<a href=""/exec/obidos/tg/browse/-/508510/ref=topnav_help_gw/002-3729295-8812824?%5Fencoding=UTF8"" >Help</a>
 <span class=""navspacer""><span class=""light"">&#124;</span></span> 
<script language=""javascript1.2"">
  n2RunThisWhen('onload', function () {
    goGoldboxPop = new N2SimplePopover();
    goN2Events.registerFeature('goldboxPop', 'goGoldboxPop', 'n2MouseOverHotspot', 'n2MouseOutHotspot');
    goGoldboxPop.initialize('goldboxPopDiv', 'goGoldboxPop');
  }, 'init popover' );
</script>
<div style=""display: none"">
 <div id=""goldboxPopDiv_1"">
  <div class=""popover-tiny"" style=""width:160px; padding:8px; border:1px solid #ACA976; background-color:#FFFFFF; text-transform: none"">10 new deals await you in your <a href=""/gp/goldbox/ref=cs_top_nav_pop_gb27/002-3729295-8812824"">Gold Box</a>&#8482;</div>
 </div>
</div>
<a href=""/gp/goldbox/ref=cs_top_nav_gb27/002-3729295-8812824"" name=""goldboxPop|he|goldboxPopDiv_1"" id=""goldboxPop_1""><img src=""http://ec1.images-amazon.com/images/G/01/goldbox/gb27/gb-closed.gif"" width=""30"" align=""middle"" alt=""Gold Box"" title=""Gold Box"" height=""27"" border=""0"" /></a></td>
              </tr>
            </table>
          </td>
        </tr>
        <tr>
          <td class=""secondary"" height=""62"">
              
      <table border=""0"" cellpadding=""0"" cellspacing=""0"" align=""center"">
        <tr id=""twotabsubnav"">
        
<td class=""navspacer"">&nbsp; &nbsp;</td><td align=""center"" height=""28""><a href=""/gp/product/B00067L6TQ/ref=gw_subnav_egc/002-3729295-8812824?ie=UTF8"" class="""">Gift Certificates</a></td><td class=""navspacer"">&nbsp; | &nbsp;</td><td align=""center"" height=""28""><a href=""/exec/obidos/tg/stores/static/-/gateway/international-gateway/ref=gw_subnav_in/002-3729295-8812824?%5Fencoding=UTF8"" class="""">International</a></td><td class=""navspacer"">&nbsp; | &nbsp;</td><td align=""center"" height=""28""><a href=""/exec/obidos/tg/new-for-you/new-releases/-/main/ref=gw_subnav_nr/002-3729295-8812824?%5Fencoding=UTF8"" class="""">New Releases</a></td><td class=""navspacer"">&nbsp; | &nbsp;</td><td align=""center"" height=""28""><a href=""/exec/obidos/tg/new-for-you/top-sellers/-/main/ref=gw_subnav_ts/002-3729295-8812824?%5Fencoding=UTF8"" class="""">Top Sellers</a></td><td class=""navspacer"">&nbsp; | &nbsp;</td><td align=""center"" height=""28""><a href=""/exec/obidos/tg/browse/-/909656/ref=stuffandsubnav_td1_/002-3729295-8812824"" class="""">Today's Deals</a></td><td class=""navspacer"">&nbsp; | &nbsp;</td><td align=""center"" height=""28""><a href=""/exec/obidos/subst/misc/sell-your-stuff.html/ref=subnav_sys_/002-3729295-8812824?%5Fencoding=UTF8"" class="""">Sell Your Stuff</a></td><td class=""navspacer"">&nbsp; &nbsp;</td>
        </tr>
      </table>
              <table width=""100%"" border=""0"" cellspacing=""0"" cellpadding=""0"">
                <tr id=""twotabsearch"">
                  
  <td align=""center"">
  
  
  
<form style=""margin-bottom:0;"" method=""get"" action=""/s/ref=br_ss_hs/002-3729295-8812824"" name=""searchBrick"">
<table border=""0"" cellpadding=""0"" cellspacing=""0"" align=""center"">
  <tr>
    <td class=""searchtitle"" width=""50"" align=""center"">Search&nbsp;</td>
    <td width=""100"">
<input type=""hidden"" name=""platform"" value=""gurupa"" />
<select name=""url"">
<option value=""index=blended"">Amazon.com</option><option value=""index=stripbooks:relevance-above"">Books</option><option value=""index=music"">Popular Music</option><option value=""index=music-dd"">Music Downloads</option><option value=""index=classical"">Classical Music</option><option value=""index=dvd"">DVD</option><option value=""index=amazontv&amp;platform=gurupa"">Video Downloads</option><option value=""index=vhs"">VHS</option><option value=""index=apparel-index&amp;platform=gurupa"">Apparel</option><option value=""index=grocery"">Grocery</option><option value=""index=local-index&amp;platform=gurupa"">Yellow Pages</option><option value=""index=toys-and-games"">Toys</option><option value=""index=baby-products"">Baby</option><option value=""index=pc-hardware"">Computers</option><option value=""index=videogames"">Video Games</option><option value=""index=electronics-aps"">Electronics</option><option value=""index=photo"">Camera &amp; Photo</option><option value=""index=software"">Software</option><option value=""index=tools"">Tools &amp; Hardware</option><option value=""index=office-products"">Office Products</option><option value=""index=magazines"">Magazines</option><option value=""index=sporting-index&amp;platform=gurupa"">Sports &amp; Outdoors</option><option value=""index=garden"">Outdoor Living</option><option value=""index=kitchen"">Kitchen</option><option value=""index=jewelry-index&amp;platform=gurupa"">Jewelry &amp; Watches</option><option value=""index=beauty-index&amp;platform=gurupa"">Beauty</option><option value=""index=gourmet-index&amp;platform=gurupa"">Gourmet Food</option><option value=""index=mi-index&amp;platform=gurupa"">Musical Instruments</option><option value=""index=hpc-index&amp;platform=gurupa"">Health/Personal Care</option><option value=""index=pet-supplies&amp;store-name=kitchen&amp;search-type=ss"">Pet Supplies</option><option value=""index=stripbooks:relevance-above&amp;field-browse=27"">Travel</option><option value=""index=wireless-phones"">Cell Phones &amp; Service</option><option value=""index=outlet"">Outlet</option><option value=""index=auction-redirect"">Auctions</option><option value=""index=fixed-price-redirect"">zShops</option><option value=""index=automotive-index&amp;platform=gurupa"">Automotive</option><option value=""index=industrial-index&amp;platform=gurupa"">Industrial &amp; Scientific</option></select></td><td width=""5"">&nbsp;</td><td width=""325""><input type=""text"" id=""twotabsearchtextbox"" name=""keywords"" value="""" size=""25"" style=""width:100%"" /><img src=""http://ec1.images-amazon.com/images/G/01/x-locale/common/transparent-pixel.gif"" width=""200"" height=""1"" border=""0"" /></td>
<td width=""5"">&nbsp;</td>
<td><input type=""image"" src=""http://ec1.images-amazon.com/images/G/01/buttons/go-orange-trans.gif""  width=""21"" alt=""Go"" value=""Go"" name=""Go"" height=""21"" border=""0"" />
</td>
</tr>
</table>
</form>
  </td>
<td>
  <table width=""100%"" border=""0"" cellspacing=""0"" cellpadding=""0"">
   <tr>
   <td class =""gcsecondsearch"" height=""34"">
   <table width=""100%"" border=""0"" cellspacing=""2"" cellpadding=""0"" align=""center"">
   <tr>
   <td align=""center"" style=""padding-right:3px;"">
<a href=""/gp/gift-central/gift-guides/ref=cm_gift_button_gg_lp/002-3729295-8812824"" id=""find-gifts"" name=""findGift|he|findGifts""><img src=""http://ec1.images-amazon.com/images/G/01/gifts/giftcentral/1/buybox-button-find-gifts-2._V51869628_.gif"" width=""99"" alt=""Find Gifts"" height=""32"" border=""0"" /></a>
<script>
n2RunThisWhen(n2sRTWTBS,
 function() {
  FGSimplePop = new N2SimplePopover();
  goN2Events.registerFeature('findGift', 'FGSimplePop', 'n2MouseOverHotspot', 'n2MouseOutHotspot');
  FGSimplePop.initialize('FGSimplePopDiv', 'FGSimplePop', gaTD, null, 'below', 'c');
 },
 'init popover' );
</script>
<style>
.gc-popover-tiny { font-size: 10px; font-family: verdana,arial,helvetica,sans-serif; }
.gc-popover-tiny a, .gc-popover-tiny a:visited { text-decoration: none; color: #003399; }
.gc-popover-tiny a:hover { text-decoration: underline; color: #CC6600; }
.gc-tiny { font-family: verdana,arial,helvetica,sans-serif; font-size: 1em; }
</style>
<div id=""findGifts"" style=""display:none"">
<table cellpadding=""0"" cellspacing=""0"" border=""0"" style=""border:2px solid #A5C6DE;"">
<tr>
<td>
<table width=""100%"" cellpadding=""0"" cellspacing=""0"" border=""0"" style=""border:1px solid #9C0010;"">
<tr>
<td style=""padding:8px;"">
<table width=""100%"" cellpadding=""0"" cellspacing=""0"" border=""0"">
<tr>
<td valign=""top"">
<table width=""100%"" cellpadding=""0"" cellspacing=""0"" border=""0"" style=""border-collapse:collapse;"">
<tr>
<td style=""padding-bottom:3px;""><span class=""gc-popover-tiny"" style=""color:#9C0010;""><strong>GIFT GUIDES</strong></span></td>
<td style=""background-color:#ffffff;padding-bottom:3px;padding-left:6px;border-left:1px dashed #b9c6a9;""><span class=""gc-popover-tiny"" style=""color:#8D4958;""><strong>MORE TO EXPLORE</strong></span></td>
</tr>
<tr>
<td valign=""top"" class=""gc-popover-tiny"" style=""line-height:2em;padding-right:20px;padding-top:3px;background-image:url(http://ec1.images-amazon.com/images/G/01/gifts/giftcentral/06/vday/popover-vday-6._V54633395_.gif);background-repeat:no-repeat;"">
<nobr><a href=""/gp/gift-central/gift-guides/am/seasonal-guides/ref=cm_gift_topnav_gg_fall06_lp/002-3729295-8812824"" style=color:#cc6600;>Fall Fun</a> &#40;<a href=""/gp/gift-central/gift-guides/rc/R2EO78BGT597DR/ref=cm_gift_topnav_gg_hal_kid/002-3729295-8812824"">Halloween for Kids</a>&#44;
<a href=""/gp/gift-central/gift-guides/rc/R3B00J6RSGL537/ref=cm_gift_topnav_gg_hal_gu/002-3729295-8812824"">and Grown-Ups</a>&nbsp;&#151;
<a href=""/gp/gift-central/gift-guides/am/seasonal-guides/ref=cm_gift_topnav_gg_fall06_lp/002-3729295-8812824"">see all 3</a>&#41;</nobr><br />
<nobr><a href=""/gp/gift-central/gift-guides/am/price-guides/ref=cm_gift_topnav_gg_price_lp/002-3729295-8812824"" style=color:#cc6600;>By Price</a> &#40;<a href=""/gp/gift-central/gift-guides/rc/R288WAUZAVHHY1/ref=cm_gift_topnav_gg_u25/002-3729295-8812824"">Under $25</a>&#44;
<a href=""/gp/gift-central/gift-guides/rc/R31AGFH2H5LVQB/ref=cm_gift_topnav_gg_u50/002-3729295-8812824"">Under $50</a>&nbsp;&#151;
<a href=""/gp/gift-central/gift-guides/am/price-guides/ref=cm_gift_topnav_gg_price_lp/002-3729295-8812824"">see all 4</a>&#41;</nobr><br />
<nobr><a href=""/gp/gift-central/gift-guides/am/relationship-guides/ref=cm_gift_topnav_gg_rel_lp/002-3729295-8812824"" style=color:#cc6600;>By Relationship</a> &#40;<a href=""/gp/gift-central/gift-guides/rc/R2P88QQ4PDK85W/ref=cm_gift_topnav_gg_mom/002-3729295-8812824"">Mom</a>&#44;
<a href=""/gp/gift-central/gift-guides/rc/R14Q6D7V34DRHZ/ref=cm_gift_topnav_gg_gfw/002-3729295-8812824"">Girlfriend/Wife</a>&nbsp;&#151;
<a href=""/gp/gift-central/gift-guides/am/relationship-guides/ref=cm_gift_topnav_gg_rel_lp/002-3729295-8812824"">see all 9</a>&#41;</nobr><br />
<nobr><a href=""/gp/gift-central/gift-guides/am/kids-guides/ref=cm_gift_topnav_gg_kids_lp/002-3729295-8812824"" style=color:#cc6600;>For Kids and Teens</a> &#40;<a href=""/gp/gift-central/gift-guides/rc/R1AE6AM0UJQ8PX/ref=cm_gift_topnav_gg_tod/002-3729295-8812824"">Toddler</a>&#44;
<a href=""/gp/gift-central/gift-guides/rc/R39B23BI1GJIYP/ref=cm_gift_topnav_gg_prg/002-3729295-8812824"">Preteen Girl</a>&nbsp;&#151;
<a href=""/gp/gift-central/gift-guides/am/kids-guides/ref=cm_gift_topnav_gg_kids_lp/002-3729295-8812824"">see all 9</a>&#41;</nobr><br />
<nobr><a href=""/gp/gift-central/gift-guides/am/recipient-guides/ref=cm_gift_topnav_gg_recip_lp/002-3729295-8812824"" style=color:#cc6600;>By Recipient</a> &#40;<a href=""/gp/gift-central/gift-guides/rc/R6MP3PVFKXTLK/ref=cm_gift_topnav_gg_gou/002-3729295-8812824"">Foodie</a>&#44;
<a href=""/gp/gift-central/gift-guides/rc/RRIVGB3VNL2DD/ref=cm_gift_topnav_gg_grt/002-3729295-8812824"">Green Thumb</a>&nbsp;&#151;
<a href=""/gp/gift-central/gift-guides/am/recipient-guides/ref=cm_gift_topnav_gg_recip_lp/002-3729295-8812824"">see all 15</a>&#41;</nobr><br />
<nobr><a href=""/gp/gift-central/gift-guides/am/occasion-guides/ref=cm_gift_topnav_gg_occ_lp/002-3729295-8812824"" style=color:#cc6600;>By Occasion</a> &#40;<a href=""/gp/gift-central/gift-guides/rc/R2TILULJFOGJSG/ref=cm_gift_topnav_gg_wed/002-3729295-8812824"">Wedding</a>&#44;
<a href=""/gp/gift-central/gift-guides/rc/R1DORYEZRJWR4K/ref=cm_gift_topnav_gg_bsh/002-3729295-8812824"">Baby Shower</a>&nbsp;&#151;
<a href=""/gp/gift-central/gift-guides/am/occasion-guides/ref=cm_gift_topnav_gg_occ_lp/002-3729295-8812824"">see all 9</a>&#41;</nobr><br />
<nobr><a href=""/gp/gift-central/gift-guides/am/type-guides/ref=cm_gift_topnav_gg_type_lp/002-3729295-8812824"" style=color:#cc6600;>By Type</a> &#40;<a href=""/gp/gift-central/gift-guides/rc/R8LGKH2XRE7D6/ref=cm_gift_topnav_gg_cur/002-3729295-8812824"">Curiosities</a>&#44;
<a href=""/gp/gift-central/gift-guides/rc/R3JSX96K43XDDN/ref=cm_gift_topnav_gg_lmg/002-3729295-8812824"">Last-Minute Gifts</a>&#44;
<a href=""/gp/gift-central/gift-guides/rc/R2HHZ51GVHT6OT/ref=cm_gift_topnav_gg_cor/002-3729295-8812824"">Corporate Gifts</a>&#41;</nobr><br />
<nobr><a href=""/gp/gift-central/gift-guides/am/popular-displayed/ref=cm_gift_topnav_gg_mpd_lp/002-3729295-8812824"" style=color:#cc6600;>Most Popular</a> &#40;<a href=""/gp/gift-central/gift-guides/most-wished-for/ref=cm_gift_topnav_gg_wish/002-3729295-8812824"">Most Wished For</a>&#44;
<a href=""/gp/gift-central/gift-guides/most-gifted/ref=cm_gift_topnav_gg_gift/002-3729295-8812824"">Most Gifted</a>&#44;
<a href=""/gp/gift-central/gift-guides/top-sellers/ref=cm_gift_topnav_gg_tops/002-3729295-8812824"">Top Sellers</a>&#41;</nobr><br />
</td>
<td valign=""top"" class=""gc-popover-tiny"" style=""line-height:1.7em;padding-right:none;padding-top:3px;padding-left:6px;border-left:1px dashed #b9c6a9;"">
<nobr><a href=""/gp/gift-central/gift-guides/ref=cm_gift_topnav_gg_lp/002-3729295-8812824"">See all Gift Guides</a></nobr><br />
<nobr><a href=""/gp/product/B00067L6TQ/ref=cm_gift_topnav_gcert/002-3729295-8812824"">Gift Certificates</a></nobr><br />
<nobr><a href=""/gp/gift-central/organizer/ref=cm_gift_topnav_organizer/002-3729295-8812824"">Gift Organizer</a></nobr><br />
<nobr><a href=""/gp/registry/wishlist/ref=cm_gift_topnav_wl_hp/002-3729295-8812824"">Wish Lists</a></nobr><br />
<nobr><a href=""/gp/registry/babyreg/ref=cm_gift_topnav_babyreg/002-3729295-8812824"">Baby Registry</a></nobr><br />
<nobr><a href=""/gp/wedding/homepage/ref=cm_gift_topnav_wedreg/002-3729295-8812824"">Wedding Registry</a>
</nobr><br />
<span class=""gc-tiny"" style=""color:#cc6600;""><nobr>Find someone's Wish List:</nobr></span><br />
<form style=""margin:0px;"" method=""get"" name=""wishlist-search"" action=""/gp/registry/search.html/ref=cm_gift_topnav_wlsearch/002-3729295-8812824"">
<input type=""hidden"" name=""type"" value=""wishlist"">
<nobr><input type=""text"" style=""border:1px solid #999999;height:16px;width:130px;font-family:verdana,arial,sans-serif;font-size:9px;color:#666666;"" value=""name or e-mail"" name=""field-name"" onclick=""this.value='';"" />&nbsp;
<input type=""image"" src=""http://ec1.images-amazon.com/images/G/01/gifts/registries/wishlist/v2/go-button.gif"" style=""vertical-align:middle"" value=""Go"" alt=""Go""/></nobr>
</form>
<a href=""/gp/gift-central/ref=cm_gift_topnav_gc_logo/002-3729295-8812824""><img src=""http://ec1.images-amazon.com/images/G/01/gifts/giftcentral/1/gc-logo-tiny.gif"" width=""120"" alt=""Gift Central"" style=""margin-top:10px;float:right;"" height=""24"" border=""0"" /></a>
</td>
</tr>
</table>
</td>
</tr>
</table>
</td>
</tr>
</table>
</td>
</tr>
</table>
</div>
</td>
  <td valign=""bottom"" align=""right"">
 <script language=""javascript"" type=""text/javascript"">
  <!--
  function submitSearch(form) {
    var value = form.q.value;
    if (value) {
      if (value == ""robots.txt"" || value == ""favicon.ico"") {
        value = '""' + value + '""';
      }
      if (typeof(encodeURIComponent) != ""undefined"") {
        value = encodeURIComponent(value);
      } else {
        value = escape(value);
      }
      location.href = ""http://a9.amazon.com/?dns=www&src=amz&qs=""+ value;
    }
    return false;
  }
  -->
  </script>
            <form method=""get"" action=""http://a9.amazon.com/?dns=www&src=amz"" target=""new_window"" onsubmit="" return submitSearch(this)"" style=""margin:0"" name=""webSearchForm"">
	      <table cellpadding=""0"" cellspacing=""0"" border=""0"">
          <tr>
        <td width=""91""><a href=""/gp/redirect.html/002-3729295-8812824?location=http://a9.com&token=AA2B4D6888E513CDF7C22811766DED1A3F07D572""><img src=""http://ec1.images-amazon.com/images/G/01/gifts/giftcentral/1/cap-a9-3.gif"" width=""91"" height=""25"" border=""0"" /></a></td>
          <td class=""gca9websearch"" width=""100"">
	<input type=""text""  name=""q"" maxlength=""256"" style=""height:20px;width:100%;""/><img src=""http://ec1.images-amazon.com/images/G/01/x-locale/common/transparent-pixel.gif"" width=""40"" alt="""" height=""1"" border=""0"" /></td>
	<td width=""25""><input type=""image"" src=""http://ec1.images-amazon.com/images/G/01/gifts/giftcentral/1/endcap-a9-go-2.gif""  width=""25"" align=""middle"" name=""Go"" height=""25"" border=""0"" /></td>
          </tr>
            </table>
            </form>
  </td>
   </tr>
   </table>
   </td>
   </tr>
  </table>
</td>
                </tr>
              </table>
            </td>
        </tr>
      </table>
  <div style=""display:none""><div id=""all-categories"">    <div style=""width:586px; padding:12px; border:1px solid #ACA976; background-color:#FFFFFF;"">
<table width=""100%"" border=""0"" cellspacing=""0"" cellpadding=""1""><tr valign=""top""><td class=""popover-tiny""><div class=""indent""><div class=""list""><div nowrap=""nowrap""><a href=""/books-used-books-textbooks/b/ref=sd_allcatpop_bo/002-3729295-8812824?ie=UTF8&node=283155"">Books</a></div><div nowrap=""nowrap""><a href=""/music-rock-classical-pop-jazz/b/ref=sd_allcatpop_mu/002-3729295-8812824?ie=UTF8&node=5174"">Music</a></div><div nowrap=""nowrap""><a href=""/dvds-used-dvd-boxed-sets/b/ref=sd_allcatpop_dvd/002-3729295-8812824?ie=UTF8&node=130"">DVD</a></div>
  <div nowrap=""nowrap""><a href=""/b/ref=sd_allcatpop_atv/002-3729295-8812824?ie=UTF8&node=16261631"">Unbox Video Downloads</a></div>
<div nowrap=""nowrap""><a href=""/video-vhs-used-videos/b/ref=sd_allcatpop_vi/002-3729295-8812824?ie=UTF8&node=404272"">VHS</a></div><div nowrap=""nowrap""><a href=""/magazine-newspaper-subscriptions/b/ref=sd_allcatpop_magazines/002-3729295-8812824?ie=UTF8&node=599858"">Magazines &amp; Newspapers</a></div><div nowrap=""nowrap""><a href=""/computer-video-games-hardware-accessories/b/ref=sd_allcatpop_cvg/002-3729295-8812824?ie=UTF8&node=468642"">Computer &amp; Video Games</a></div><div nowrap=""nowrap""><a href=""/software-business-education-finance-childrens/b/ref=sd_allcatpop_sw/002-3729295-8812824?ie=UTF8&node=229534"">Software</a></div><div nowrap=""nowrap""><a href=""/amazon-shorts-digital-shorts/b/ref=sd_allcatpop_sh/002-3729295-8812824?ie=UTF8&node=13993911"">Amazon Shorts</a></div></div></div></td><td class=""popover-tiny""><div class=""indent""><div class=""list""><div nowrap=""nowrap""><a href=""/consumer-electronics-tvs-cameras-phones/b/ref=sd_allcatpop_el/002-3729295-8812824?ie=UTF8&node=172282"">Electronics</a></div><div nowrap=""nowrap""><a href=""/audio-video-portable-accessories/b/ref=sd_allcatpop_av/002-3729295-8812824?ie=UTF8&node=1065836"">Audio &amp; Video</a></div><div nowrap=""nowrap""><a href=""/b/ref=sd_allcatpop_p/002-3729295-8812824?ie=UTF8&node=502394"">Camera &amp; Photo</a></div><div nowrap=""nowrap""><a href=""/cell-phones-service-plans-accessories/b/ref=sd_allcatpop_wi/002-3729295-8812824?ie=UTF8&node=301185"">Cell Phones &amp; Service</a></div><div nowrap=""nowrap""><a href=""/computer-pc-hardware-accessories-add-ons/b/ref=sd_allcatpop_pc/002-3729295-8812824?ie=UTF8&node=541966"">Computers &amp; PC Hardware</a></div><div nowrap=""nowrap""><a href=""/office-products-supplies-electronics-furniture/b/ref=sd_allcatpop_op/002-3729295-8812824?ie=UTF8&node=1064954"">Office Products</a></div><div nowrap=""nowrap""><a href=""/musical-instruments-accessories-sound-recording/b/ref=sd_allcatpop_mi/002-3729295-8812824?ie=UTF8&node=11091801"">Musical Instruments</a></div></div></div></td><td class=""popover-tiny""><div class=""indent""><div class=""list""><div nowrap=""nowrap""><a href=""/home-garden-kitchen-housewares-outdoor/b/ref=sd_allcatpop_hg/002-3729295-8812824?ie=UTF8&node=1055398"">Home &amp; Garden</a></div><div nowrap=""nowrap""><a href=""/bed-bath-bedding-bathroom-accessories/b/ref=sd_allcatpop_bb/002-3729295-8812824?ie=UTF8&node=1057792"">Bed &amp; Bath</a></div><div nowrap=""nowrap""><a href=""/furniture-decor-dining-bedroom-patio/b/ref=sd_allcatpop_fd/002-3729295-8812824?ie=UTF8&node=1057794"">Furniture &amp; D&#233;cor</a></div><div nowrap=""nowrap""><a href=""/gourmet-food-gifts-chocolate-seafood/b/ref=sd_allcatpop_gf/002-3729295-8812824?ie=UTF8&node=3370831"">Gourmet Food</a></div><div nowrap=""nowrap""><a href=""/kitchen-housewares-small-appliances-cookware/b/ref=sd_allcatpop_ki/002-3729295-8812824?ie=UTF8&node=284507"">Kitchen &amp; Housewares</a></div><div nowrap=""nowrap""><a href=""/outdoor-living-grills-patio-furniture/b/ref=sd_allcatpop_lp/002-3729295-8812824?ie=UTF8&node=286168"">Outdoor Living</a></div><div nowrap=""nowrap""><a href=""/pet-supplies-birds-cats-dogs/b/ref=sd_allcatpop_ps/002-3729295-8812824?ie=UTF8&node=12923371"">Pet Supplies</a></div><div nowrap=""nowrap""><a href=""/automotive-auto-truck-replacements-parts/b/ref=sd_allcatpop_cpc/002-3729295-8812824?ie=UTF8&node=15684181"">Automotive</a></div><div nowrap=""nowrap""><a href=""/tools-hardware-garden-tools-lawn-mowers/b/ref=sd_allcatpop_hi/002-3729295-8812824?ie=UTF8&node=228013"">Tools & Hardware</a></div><div nowrap=""nowrap""><a href=""/industrial-scientific-fastners-raw-materials/b/ref=sd_allcatpop_biss/002-3729295-8812824?ie=UTF8&node=16310091"">Industrial & Scientific</a></div></div></div></td><td class=""popover-tiny""><div class=""indent""><div class=""list""><div nowrap=""nowrap""><a href=""/apparel-accessories-men-women-kids/b/ref=sd_allcatpop_apr/002-3729295-8812824?ie=UTF8&node=1036592"">Apparel &amp; Accessories</a></div><div nowrap=""nowrap""><a href=""/shoes-men-women-kids-baby/b/ref=sd_allcatpop_shoe/002-3729295-8812824?ie=UTF8&node=1040668"">Shoes</a></div><div nowrap=""nowrap""><a href=""/jewelry-watches-engagements-rings-diamonds/b/ref=sd_allcatpop_jewelry/002-3729295-8812824?ie=UTF8&node=3367581"">Jewelry &amp; Watches</a></div><div nowrap=""nowrap""><a href=""/grocery-breakfast-foods-snacks-organic/b/ref=sd_allcatpop_gro/002-3729295-8812824?ie=UTF8&node=16310101"">Grocery</a></div><div nowrap=""nowrap""><a href=""/beauty-makeup-fragrance-skin-care/b/ref=sd_allcatpop_bty/002-3729295-8812824?ie=UTF8&node=3760911"">Beauty</a></div><div nowrap=""nowrap""><a href=""/health-personal-care-nutrition-fitness/b/ref=sd_allcatpop_hpc/002-3729295-8812824?ie=UTF8&node=3760901"">Health &amp; Personal Care</a></div><div nowrap=""nowrap""><a href=""/sporting-goods-apparel-cycling-exercise/b/ref=sd_allcatpop_sg/002-3729295-8812824?ie=UTF8&node=3375251"">Sports &amp; Outdoors</a></div>
<div nowrap=""nowrap""><a href=""/toys-games-electronics-action-figures/b/ref=sd_allcatpop_tg/002-3729295-8812824?ie=UTF8&node=165793011"">Toys &amp; Games</a></div><div nowrap=""nowrap""><a href=""/baby-car-seats-strollers-bedding/b/ref=sd_allcatpop_ba/002-3729295-8812824?ie=UTF8&node=165796011"">Baby</a></div>
</div></div></td></tr><tr><td colspan=""4""><div class=""hr-center""></div><div class=half><div class=half><div class=half></div></div></div></td></tr><tr valign=""top""><td class=""popover-tiny""><div class=""indent""><div class=""list""><div nowrap=""nowrap""><a href=""/gp/registry/registry.html/ref=sd_allcatpop_wl/002-3729295-8812824?ie=UTF8&type=wishlist"">Wish List</a></div><div nowrap=""nowrap""><a href=""/b/ref=sd_allcatpop_gft/002-3729295-8812824?ie=UTF8&node=229220"">Gift Ideas</a></div><div nowrap=""nowrap""><a href=""/Fresh-Flowers-Indoor-Plants/b/ref=sd_allcatpop_ffp/002-3729295-8812824?ie=UTF8&node=3745171"">Fresh Flowers &amp; Indoor Plants</a></div><div nowrap=""nowrap""><a href=""/gp/registry/wedding-homepage.html/ref=sd_allcatpop_wreg/002-3729295-8812824"">Wedding Registry</a></div><div nowrap=""nowrap""><a href=""/gp/registry/babyreg/ref=sd_allcatpop_breg/002-3729295-8812824"">Baby Registry</a></div><div nowrap=""nowrap""><a href=""/Free-e-Cards/b/ref=sd_allcatpop_ecard/002-3729295-8812824?ie=UTF8&node=225840"">Free e-Cards</a></div><div nowrap=""nowrap""><a href=""/gp/pdp/profile/ref=sd_allcatpop_ff/002-3729295-8812824"">Your Profile</a></div></div></div></td><td class=""popover-tiny""><div class=""indent""><div class=""list""><div nowrap=""nowrap""><a href=""/b/ref=sd_allcatpop_ida/002-3729295-8812824?ie=UTF8&node=230659011"">International Direct</a></div><div nowrap=""nowrap""><a href=""http://s1.amazon.com/exec/varzea/subst/home/home.html/ref=sd_allcatpop_au/002-3729295-8812824"">Auctions</a></div><div nowrap=""nowrap""><a href=""/Outlet/b/ref=sd_allcatpop_ou/002-3729295-8812824?ie=UTF8&node=517808"">Outlet</a></div><div nowrap=""nowrap""><a href=""http://s1.amazon.com/exec/varzea/subst/home/fixed.html/ref=sd_allcatpop_zs/002-3729295-8812824"">zShops</a></div><div nowrap=""nowrap""><a href=""/gp/library/ref=sd_allcatpop_yml/002-3729295-8812824"">Your Media Library</a></div><div nowrap=""nowrap""><a href=""/gp/arms/directory/ref=sd_allcatpop_ac/002-3729295-8812824"">AmazonConnect</a></div><div nowrap=""nowrap""><a href=""/gp/entertainment/fishbowl/ref=sd_allcatpop_af/002-3729295-8812824"">Amazon Fishbowl</a></div>
</div></div></td><td class=""popover-tiny""><div class=""indent""><div class=""list""><div nowrap=""nowrap""><a href=""/Broadband-Services/b/ref=sd_allcatpop_brd/002-3729295-8812824?ie=UTF8&node=51117011"">Broadband Services</a></div><div nowrap=""nowrap""><a href=""/gp/gss/ref=sd_allcatpop_gss/002-3729295-8812824"">E-mail Subscriptions</a></div><div nowrap=""nowrap""><a href=""/gp/redirect.html/ref=sd_allcatpop_stf/002-3729295-8812824?location=http://www.shutterfly.com&token=48F72A281FEE6F487D61AF8E4F6386BB3B53BDAC"">Photo Services</a></div><div nowrap=""nowrap""><a href=""/b/ref=sd_allcatpop_yp/002-3729295-8812824?ie=UTF8&node=3999141"">Yellow Pages</a></div><div nowrap=""nowrap""><a href=""/Travel/b/ref=sd_allcatpop_tr/002-3729295-8812824?ie=UTF8&node=605012"">Travel</a></div><div nowrap=""nowrap""><a href=""/Financial-Services/b/ref=sd_allcatpop_fs/002-3729295-8812824?ie=UTF8&node=16292601"">Financial Services</a></div></div></div></td><td class=""popover-tiny""><div class=""indent""><div class=""list""><div nowrap=""nowrap""><a href=""/exec/obidos/subst/misc/sell-your-stuff.html/ref=sd_allcatpop_mp/002-3729295-8812824"">Sell Your Stuff</a></div><div nowrap=""nowrap""><a href=""/Associates-join-page-Money-home/b/ref=sd_allcatpop_assoc/002-3729295-8812824?ie=UTF8&node=3435371"">Associates Program</a></div><div nowrap=""nowrap""><a href=""/exec/obidos/subst/partners/direct/direct-application.html/ref=sd_allcatpop_adv/002-3729295-8812824"">Advantage Program</a></div><div nowrap=""nowrap""><a href=""/exec/obidos/subst/misc/co-op/small-vendor-info.html/ref=sd_allcatpop_pp/002-3729295-8812824"">Paid Placements</a></div><div nowrap=""nowrap""><a href=""/AWS-home-page-Money/b/ref=sd_allcatpop_ws/002-3729295-8812824?ie=UTF8&node=3435361"">Web Services</a></div><div nowrap=""nowrap""><a href=""/Corporate-Accounts/b/ref=sd_allcatpop_corpacc/002-3729295-8812824?ie=UTF8&node=600460"">Corporate Accounts</a></div></div></div></td></tr><tr><td colspan=""4"">&nbsp;</td></tr>
<tr><td colspan=""4"" align=""right""><div nowrap=""nowrap""><a href=""/ref=sd_allcatpop_gw/002-3729295-8812824""><img src=""http://ec1.images-amazon.com/images/G/01/nav/amazon/amzn-logo-118w.gif"" width=""118"" alt=""Amazon.com"" height=""23"" border=""0"" /></a>&nbsp;</div></td></tr></table>  </div></div></div>
<table border=""0"" cellpadding=""0"" cellspacing=""0"" width=""100%"">
<tbody><tr>
      <td style=""background-color: rgb(245, 245, 230);"" align=""center""> <font size=""-1""> 
        <span class=""small""><strong>Hello.</strong> Sign in to get <a href=""/gp/amabot/?pf_rd_url=%2Fgp%2Fyourstore%2Fref%3Dpd_irl_gw%2F002-3729295-8812824%3Fie%3DUTF8%26signIn%3D1&pf_rd_p=169949401&pf_rd_s=ilm&pf_rd_t=101&pf_rd_i=507846&pf_rd_m=ATVPDKIKX0DER&pf_rd_r=14989TMFGQ7QJ53ESP6Z"">personalized recommendations</a>. New customer? <a href=""/gp/amabot/?pf_rd_url=%2Fgp%2Fyourstore%2Frw%2Fref%3Dpd_irl_gw_r%2F002-3729295-8812824%3Fie%3DUTF8%26rwRedirect%3D1&pf_rd_p=169949401&pf_rd_s=ilm&pf_rd_t=101&pf_rd_i=507846&pf_rd_m=ATVPDKIKX0DER&pf_rd_r=14989TMFGQ7QJ53ESP6Z"">Start here</a>.</span>
        </td>
</tr>
</tbody></table>
<br />
    <table width=""100%"" border=""0"" cellpadding=""5"" cellspacing=""0"">
      <tr>
        <td valign=""top"" width=""180"" class=""amabot_left"">
          
    
    
  
    
<table border=""0"" cellpadding=""0"" cellspacing=""0"" width=100%""><tr><td background=""http://ec1.images-amazon.com/images/G/01/nav2/images/box-shade-tl-6pts-tall._V63800021_.jpg"" class=""searchtitle"" style=""padding-left: 10px; padding-top: 6px; padding-bottom: 5px; font-size: small"">Browse</td><td background=""http://ec1.images-amazon.com/images/G/01/nav2/images/box-shade-tr2-6pts-tall._V63800021_.jpg"" width=""12"">&nbsp;</td></tr><tr>
<td style=""padding-left:8px; padding-top:3px;	border-left:1px solid #5C9EBF;"">
<div class=""leftNavTitle"">Books, Music & Movies</div><div class=""leftNav""><a href=""/gp/amabot/?pf_rd_url=%2Fbooks-used-books-textbooks%2Fb%2Fref%3Dgw_br_bo%2F002-3729295-8812824%3Fie%3DUTF8%26node%3D283155&pf_rd_p=247034601&pf_rd_s=left-nav-1&pf_rd_t=101&pf_rd_i=507846&pf_rd_m=ATVPDKIKX0DER&pf_rd_r=14989TMFGQ7QJ53ESP6Z"">Books</a></div><div class=""leftNav""><a href=""/gp/amabot/?pf_rd_url=%2Fdvds-used-dvd-boxed-sets%2Fb%2Fref%3Dgw_br_dvd%2F002-3729295-8812824%3Fie%3DUTF8%26node%3D130&pf_rd_p=247034601&pf_rd_s=left-nav-1&pf_rd_t=101&pf_rd_i=507846&pf_rd_m=ATVPDKIKX0DER&pf_rd_r=14989TMFGQ7QJ53ESP6Z"">DVD</a></div><div class=""leftNav""><a href=""/gp/amabot/?pf_rd_url=%2Fmagazine-newspaper-subscriptions%2Fb%2Fref%3Dgw_br_zi%2F002-3729295-8812824%3Fie%3DUTF8%26node%3D599858&pf_rd_p=247034601&pf_rd_s=left-nav-1&pf_rd_t=101&pf_rd_i=507846&pf_rd_m=ATVPDKIKX0DER&pf_rd_r=14989TMFGQ7QJ53ESP6Z"">Magazines & Newspapers</a></div><div class=""leftNav""><a href=""/gp/amabot/?pf_rd_url=%2Fmusic-rock-classical-pop-jazz%2Fb%2Fref%3Dgw_br_mu%2F002-3729295-8812824%3Fie%3DUTF8%26node%3D5174&pf_rd_p=247034601&pf_rd_s=left-nav-1&pf_rd_t=101&pf_rd_i=507846&pf_rd_m=ATVPDKIKX0DER&pf_rd_r=14989TMFGQ7QJ53ESP6Z"">Music</a></div><div class=""leftNav""><a href=""/gp/amabot/?pf_rd_url=%2FNew-Used-Textbooks-Books%2Fb%2Fref%3Dgw_br_txb%2F002-3729295-8812824%3Fie%3DUTF8%26node%3D465600&pf_rd_p=247034601&pf_rd_s=left-nav-1&pf_rd_t=101&pf_rd_i=507846&pf_rd_m=ATVPDKIKX0DER&pf_rd_r=14989TMFGQ7QJ53ESP6Z"">Textbooks</a></div><div class=""leftNav""><a href=""/gp/amabot/?pf_rd_url=%2Fb%2Fref%3Dgw_br_unbox%2F002-3729295-8812824%3Fie%3DUTF8%26node%3D16261631&pf_rd_p=247034601&pf_rd_s=left-nav-1&pf_rd_t=101&pf_rd_i=507846&pf_rd_m=ATVPDKIKX0DER&pf_rd_r=14989TMFGQ7QJ53ESP6Z"">Unbox Video Downloads</a></div><div class=""leftNav""><a href=""/gp/amabot/?pf_rd_url=%2Fvideo-vhs-used-videos%2Fb%2Fref%3Dgw_br_vi%2F002-3729295-8812824%3Fie%3DUTF8%26node%3D404272&pf_rd_p=247034601&pf_rd_s=left-nav-1&pf_rd_t=101&pf_rd_i=507846&pf_rd_m=ATVPDKIKX0DER&pf_rd_r=14989TMFGQ7QJ53ESP6Z"">VHS</a></div><div class=""leftNavTitle"">Clothing & Accessories</div><div class=""leftNav""><a href=""/gp/amabot/?pf_rd_url=%2Fapparel-accessories-men-women-kids%2Fb%2Fref%3Dgw_br_ap%2F002-3729295-8812824%3Fie%3DUTF8%26node%3D1036592&pf_rd_p=247034601&pf_rd_s=left-nav-1&pf_rd_t=101&pf_rd_i=507846&pf_rd_m=ATVPDKIKX0DER&pf_rd_r=14989TMFGQ7QJ53ESP6Z"">Apparel & Accessories</a></div><div class=""leftNav""><a href=""/gp/amabot/?pf_rd_url=%2Fjewelry-watches-engagements-rings-diamonds%2Fb%2Fref%3Dgw_br_jw%2F002-3729295-8812824%3Fie%3DUTF8%26node%3D3367581&pf_rd_p=247034601&pf_rd_s=left-nav-1&pf_rd_t=101&pf_rd_i=507846&pf_rd_m=ATVPDKIKX0DER&pf_rd_r=14989TMFGQ7QJ53ESP6Z"">Jewelry & Watches</a></div><div class=""leftNav""><a href=""/gp/amabot/?pf_rd_url=%2Fshoes-men-women-kids-baby%2Fb%2Fref%3Dgw_br_sh%2F002-3729295-8812824%3Fie%3DUTF8%26node%3D1040668&pf_rd_p=247034601&pf_rd_s=left-nav-1&pf_rd_t=101&pf_rd_i=507846&pf_rd_m=ATVPDKIKX0DER&pf_rd_r=14989TMFGQ7QJ53ESP6Z"">Shoes</a></div><div class=""leftNavTitle"">Computer & Office</div><div class=""leftNav""><a href=""/gp/amabot/?pf_rd_url=%2Fcomputer-pc-hardware-accessories-add-ons%2Fb%2Fref%3Dgw_br_pc%2F002-3729295-8812824%3Fie%3DUTF8%26node%3D541966&pf_rd_p=247034601&pf_rd_s=left-nav-1&pf_rd_t=101&pf_rd_i=507846&pf_rd_m=ATVPDKIKX0DER&pf_rd_r=14989TMFGQ7QJ53ESP6Z"">Computers</a></div><div class=""leftNav""><a href=""/gp/amabot/?pf_rd_url=%2Foffice-products-supplies-electronics-furniture%2Fb%2Fref%3Dgw_br_op%2F002-3729295-8812824%3Fie%3DUTF8%26node%3D1064954&pf_rd_p=247034601&pf_rd_s=left-nav-1&pf_rd_t=101&pf_rd_i=507846&pf_rd_m=ATVPDKIKX0DER&pf_rd_r=14989TMFGQ7QJ53ESP6Z"">Office Products</a></div><div class=""leftNav""><a href=""/gp/amabot/?pf_rd_url=%2Fsoftware-business-education-finance-childrens%2Fb%2Fref%3Dgw_br_sw%2F002-3729295-8812824%3Fie%3DUTF8%26node%3D229534&pf_rd_p=247034601&pf_rd_s=left-nav-1&pf_rd_t=101&pf_rd_i=507846&pf_rd_m=ATVPDKIKX0DER&pf_rd_r=14989TMFGQ7QJ53ESP6Z"">Software</a></div><div class=""leftNavTitle"">Consumer Electronics</div><div class=""leftNav""><a href=""/gp/amabot/?pf_rd_url=%2Faudio-video-portable-accessories%2Fb%2Fref%3Dgw_br_av%2F002-3729295-8812824%3Fie%3DUTF8%26node%3D1065836&pf_rd_p=247034601&pf_rd_s=left-nav-1&pf_rd_t=101&pf_rd_i=507846&pf_rd_m=ATVPDKIKX0DER&pf_rd_r=14989TMFGQ7QJ53ESP6Z"">Audio & Video</a></div><div class=""leftNav""><a href=""/gp/amabot/?pf_rd_url=%2Fb%2Fref%3Dgw_br_p%2F002-3729295-8812824%3Fie%3DUTF8%26node%3D502394&pf_rd_p=247034601&pf_rd_s=left-nav-1&pf_rd_t=101&pf_rd_i=507846&pf_rd_m=ATVPDKIKX0DER&pf_rd_r=14989TMFGQ7QJ53ESP6Z"">Camera & Photo</a></div><div class=""leftNav""><a href=""/gp/amabot/?pf_rd_url=%2Fcell-phones-service-plans-accessories%2Fb%2Fref%3Dgw_br_wi%2F002-3729295-8812824%3Fie%3DUTF8%26node%3D301185&pf_rd_p=247034601&pf_rd_s=left-nav-1&pf_rd_t=101&pf_rd_i=507846&pf_rd_m=ATVPDKIKX0DER&pf_rd_r=14989TMFGQ7QJ53ESP6Z"">Cell Phones & Service</a></div><div class=""leftNav""><a href=""/gp/amabot/?pf_rd_url=%2Fcomputer-video-games-hardware-accessories%2Fb%2Fref%3Dgw_br_cvg%2F002-3729295-8812824%3Fie%3DUTF8%26node%3D468642&pf_rd_p=247034601&pf_rd_s=left-nav-1&pf_rd_t=101&pf_rd_i=507846&pf_rd_m=ATVPDKIKX0DER&pf_rd_r=14989TMFGQ7QJ53ESP6Z"">Computer & Video Games</a></div><div class=""leftNav""><a href=""/gp/amabot/?pf_rd_url=%2Fmusical-instruments-accessories-sound-recording%2Fb%2Fref%3Dgw_br_musinst%2F002-3729295-8812824%3Fie%3DUTF8%26node%3D11091801&pf_rd_p=247034601&pf_rd_s=left-nav-1&pf_rd_t=101&pf_rd_i=507846&pf_rd_m=ATVPDKIKX0DER&pf_rd_r=14989TMFGQ7QJ53ESP6Z"">Musical Instruments</a></div><div class=""leftNav""><a href=""/gp/amabot/?pf_rd_url=%2Fconsumer-electronics-tvs-cameras-phones%2Fb%2Fref%3Dgw_br_el%2F002-3729295-8812824%3Fie%3DUTF8%26node%3D172282&pf_rd_p=247034601&pf_rd_s=left-nav-1&pf_rd_t=101&pf_rd_i=507846&pf_rd_m=ATVPDKIKX0DER&pf_rd_r=14989TMFGQ7QJ53ESP6Z"">All Consumer Electronics</a></div><div class=""leftNavTitle"">Food & Household</div><div class=""leftNav""><a href=""/gp/amabot/?pf_rd_url=%2Fgourmet-food-gifts-chocolate-seafood%2Fb%2Fref%3Dgw_br_gf%2F002-3729295-8812824%3Fie%3DUTF8%26node%3D3370831&pf_rd_p=247034601&pf_rd_s=left-nav-1&pf_rd_t=101&pf_rd_i=507846&pf_rd_m=ATVPDKIKX0DER&pf_rd_r=14989TMFGQ7QJ53ESP6Z"">Gourmet Food</a></div><div class=""leftNav""><a href=""/gp/amabot/?pf_rd_url=%2Fgrocery-breakfast-foods-snacks-organic%2Fb%2Fref%3Dgw_br_gro%2F002-3729295-8812824%3Fie%3DUTF8%26node%3D16310101&pf_rd_p=247034601&pf_rd_s=left-nav-1&pf_rd_t=101&pf_rd_i=507846&pf_rd_m=ATVPDKIKX0DER&pf_rd_r=14989TMFGQ7QJ53ESP6Z"">Grocery</a></div><div class=""leftNav""><a href=""/gp/amabot/?pf_rd_url=%2Fpet-supplies-birds-cats-dogs%2Fb%2Fref%3Dgw_br_ps%2F002-3729295-8812824%3Fie%3DUTF8%26node%3D12923371&pf_rd_p=247034601&pf_rd_s=left-nav-1&pf_rd_t=101&pf_rd_i=507846&pf_rd_m=ATVPDKIKX0DER&pf_rd_r=14989TMFGQ7QJ53ESP6Z"">Pet Supplies</a></div><div class=""leftNavTitle"">Health & Beauty</div><div class=""leftNav""><a href=""/gp/amabot/?pf_rd_url=%2Fbeauty-makeup-fragrance-skin-care%2Fb%2Fref%3Dgw_br_bty%2F002-3729295-8812824%3Fie%3DUTF8%26node%3D3760911&pf_rd_p=247034601&pf_rd_s=left-nav-1&pf_rd_t=101&pf_rd_i=507846&pf_rd_m=ATVPDKIKX0DER&pf_rd_r=14989TMFGQ7QJ53ESP6Z"">Beauty</a></div><div class=""leftNav""><a href=""/gp/amabot/?pf_rd_url=%2Fhealth-personal-care-nutrition-fitness%2Fb%2Fref%3Dgw_br_hpc%2F002-3729295-8812824%3Fie%3DUTF8%26node%3D3760901&pf_rd_p=247034601&pf_rd_s=left-nav-1&pf_rd_t=101&pf_rd_i=507846&pf_rd_m=ATVPDKIKX0DER&pf_rd_r=14989TMFGQ7QJ53ESP6Z"">Health & Personal Care</a></div><div class=""leftNavTitle"">Home & Garden</div><div class=""leftNav""><a href=""/gp/amabot/?pf_rd_url=%2Fbed-bath-bedding-bathroom-accessories%2Fb%2Fref%3Dgw_br_bb%2F002-3729295-8812824%3Fie%3DUTF8%26node%3D1057792&pf_rd_p=247034601&pf_rd_s=left-nav-1&pf_rd_t=101&pf_rd_i=507846&pf_rd_m=ATVPDKIKX0DER&pf_rd_r=14989TMFGQ7QJ53ESP6Z"">Bed & Bath</a></div><div class=""leftNav""><a href=""/gp/amabot/?pf_rd_url=%2Ffurniture-decor-dining-bedroom-patio%2Fb%2Fref%3Dgw_br_fd%2F002-3729295-8812824%3Fie%3DUTF8%26node%3D1057794&pf_rd_p=247034601&pf_rd_s=left-nav-1&pf_rd_t=101&pf_rd_i=507846&pf_rd_m=ATVPDKIKX0DER&pf_rd_r=14989TMFGQ7QJ53ESP6Z"">Furniture & D&eacute;cor</a></div><div class=""leftNav""><a href=""/gp/amabot/?pf_rd_url=%2FHome-Improvement-Tools-Hardware%2Fb%2Fref%3Dgw_br_hi%2F002-3729295-8812824%3Fie%3DUTF8%26node%3D119541011&pf_rd_p=247034601&pf_rd_s=left-nav-1&pf_rd_t=101&pf_rd_i=507846&pf_rd_m=ATVPDKIKX0DER&pf_rd_r=14989TMFGQ7QJ53ESP6Z"">Home Improvement</a></div><div class=""leftNav""><a href=""/gp/amabot/?pf_rd_url=%2Fkitchen-housewares-small-appliances-cookware%2Fb%2Fref%3Dgw_br_ki%2F002-3729295-8812824%3Fie%3DUTF8%26node%3D284507&pf_rd_p=247034601&pf_rd_s=left-nav-1&pf_rd_t=101&pf_rd_i=507846&pf_rd_m=ATVPDKIKX0DER&pf_rd_r=14989TMFGQ7QJ53ESP6Z"">Kitchen & Housewares</a></div><div class=""leftNav""><a href=""/gp/amabot/?pf_rd_url=%2Foutdoor-living-grills-patio-furniture%2Fb%2Fref%3Dgw_br_lp%2F002-3729295-8812824%3Fie%3DUTF8%26node%3D286168&pf_rd_p=247034601&pf_rd_s=left-nav-1&pf_rd_t=101&pf_rd_i=507846&pf_rd_m=ATVPDKIKX0DER&pf_rd_r=14989TMFGQ7QJ53ESP6Z"">Outdoor Living</a></div><div class=""leftNav""><a href=""/gp/amabot/?pf_rd_url=%2Fhome-garden-kitchen-housewares-outdoor%2Fb%2Fref%3Dgw_br_hg%2F002-3729295-8812824%3Fie%3DUTF8%26node%3D1055398&pf_rd_p=247034601&pf_rd_s=left-nav-1&pf_rd_t=101&pf_rd_i=507846&pf_rd_m=ATVPDKIKX0DER&pf_rd_r=14989TMFGQ7QJ53ESP6Z"">All Home & Garden</a></div><div class=""leftNavTitle"">Kids & Baby</div><div class=""leftNav""><a href=""/gp/amabot/?pf_rd_url=%2Fbaby-car-seats-strollers-bedding%2Fb%2Fref%3Dgw_br_ba%2F002-3729295-8812824%3Fie%3DUTF8%26node%3D165796011&pf_rd_p=247034601&pf_rd_s=left-nav-1&pf_rd_t=101&pf_rd_i=507846&pf_rd_m=ATVPDKIKX0DER&pf_rd_r=14989TMFGQ7QJ53ESP6Z"">Baby</a></div><div class=""leftNav""><a href=""/gp/amabot/?pf_rd_url=%2Ftoys-games-electronics-action-figures%2Fb%2Fref%3Dgw_br_tg%2F002-3729295-8812824%3Fie%3DUTF8%26node%3D165793011&pf_rd_p=247034601&pf_rd_s=left-nav-1&pf_rd_t=101&pf_rd_i=507846&pf_rd_m=ATVPDKIKX0DER&pf_rd_r=14989TMFGQ7QJ53ESP6Z"">Toys & Games</a></div><div class=""leftNavTitle"">Sports & Fitness</div><div class=""leftNav""><a href=""/gp/amabot/?pf_rd_url=%2FExercise-Fitness-Sports-Outdoors%2Fb%2Fref%3Dgw_br_ef%2F002-3729295-8812824%3Fie%3DUTF8%26node%3D3407731&pf_rd_p=247034601&pf_rd_s=left-nav-1&pf_rd_t=101&pf_rd_i=507846&pf_rd_m=ATVPDKIKX0DER&pf_rd_r=14989TMFGQ7QJ53ESP6Z"">Exercise & Fitness</a></div><div class=""leftNav""><a href=""/gp/amabot/?pf_rd_url=%2Fsporting-goods-apparel-cycling-exercise%2Fb%2Fref%3Dgw_br_sg%2F002-3729295-8812824%3Fie%3DUTF8%26node%3D3375251&pf_rd_p=247034601&pf_rd_s=left-nav-1&pf_rd_t=101&pf_rd_i=507846&pf_rd_m=ATVPDKIKX0DER&pf_rd_r=14989TMFGQ7QJ53ESP6Z"">Sports & Outdoors</a></div><div class=""leftNavTitle"">Tools & Automotive</div><div class=""leftNav""><a href=""/gp/amabot/?pf_rd_url=%2Fautomotive-auto-truck-replacements-parts%2Fb%2Fref%3Dgw_br_au%2F002-3729295-8812824%3Fie%3DUTF8%26node%3D15684181&pf_rd_p=247034601&pf_rd_s=left-nav-1&pf_rd_t=101&pf_rd_i=507846&pf_rd_m=ATVPDKIKX0DER&pf_rd_r=14989TMFGQ7QJ53ESP6Z"">Automotive</a></div><div class=""leftNav""><a href=""/gp/amabot/?pf_rd_url=%2Findustrial-scientific-fastners-raw-materials%2Fb%2Fref%3Dgw_br_ind%2F002-3729295-8812824%3Fie%3DUTF8%26node%3D16310091&pf_rd_p=247034601&pf_rd_s=left-nav-1&pf_rd_t=101&pf_rd_i=507846&pf_rd_m=ATVPDKIKX0DER&pf_rd_r=14989TMFGQ7QJ53ESP6Z"">Industrial & Scientific</a></div><div class=""leftNav""><a href=""/gp/amabot/?pf_rd_url=%2Ftools-hardware-garden-tools-lawn-mowers%2Fb%2Fref%3Dgw_br_tools%2F002-3729295-8812824%3Fie%3DUTF8%26node%3D228013&pf_rd_p=247034601&pf_rd_s=left-nav-1&pf_rd_t=101&pf_rd_i=507846&pf_rd_m=ATVPDKIKX0DER&pf_rd_r=14989TMFGQ7QJ53ESP6Z"">Tools & Hardware</a></div><hr class=""hr-leftbrowse"" noshade=""true"" size=""1"" /><div class=""leftNavTitle"">Bargains</div><div class=""leftNav""><a href=""/gp/amabot/?pf_rd_url=%2FFriday-Sale%2Fb%2Fref%3Dgw_br_frisale%2F002-3729295-8812824%3Fie%3DUTF8%26node%3D548166&pf_rd_p=247034601&pf_rd_s=left-nav-1&pf_rd_t=101&pf_rd_i=507846&pf_rd_m=ATVPDKIKX0DER&pf_rd_r=14989TMFGQ7QJ53ESP6Z"">Friday Sale</a></div><div class=""leftNav""><a href=""/gp/amabot/?pf_rd_url=%2FOutlet%2Fb%2Fref%3Dgw_br_ou%2F002-3729295-8812824%3Fie%3DUTF8%26node%3D517808&pf_rd_p=247034601&pf_rd_s=left-nav-1&pf_rd_t=101&pf_rd_i=507846&pf_rd_m=ATVPDKIKX0DER&pf_rd_r=14989TMFGQ7QJ53ESP6Z"">Outlet</a></div><div class=""leftNav""><a href=""/gp/amabot/?pf_rd_url=%2FDeals%2Fb%2Fref%3Dgw_br_todaydeals%2F002-3729295-8812824%3Fie%3DUTF8%26node%3D909656&pf_rd_p=247034601&pf_rd_s=left-nav-1&pf_rd_t=101&pf_rd_i=507846&pf_rd_m=ATVPDKIKX0DER&pf_rd_r=14989TMFGQ7QJ53ESP6Z"">Today's Deals</a></div><div class=""leftNavTitle"">Gifts & Lists</div><div class=""leftNav""><a href=""/gp/amabot/?pf_rd_url=%2Fgp%2Fregistry%2Fbabyreg%2Fref%3Dbr_breg%2F002-3729295-8812824&pf_rd_p=247034601&pf_rd_s=left-nav-1&pf_rd_t=101&pf_rd_i=507846&pf_rd_m=ATVPDKIKX0DER&pf_rd_r=14989TMFGQ7QJ53ESP6Z"">Baby Registry</a></div><div class=""leftNav""><a href=""/gp/amabot/?pf_rd_url=%2FFree-e-Cards%2Fb%2Fref%3Dgw_br_fecars%2F002-3729295-8812824%3Fie%3DUTF8%26node%3D225840&pf_rd_p=247034601&pf_rd_s=left-nav-1&pf_rd_t=101&pf_rd_i=507846&pf_rd_m=ATVPDKIKX0DER&pf_rd_r=14989TMFGQ7QJ53ESP6Z"">Free e-Cards</a></div><div class=""leftNav""><a href=""/gp/amabot/?pf_rd_url=%2FFresh-Flowers-Indoor-Plants%2Fb%2Fref%3Dgw_br_flowers%2F002-3729295-8812824%3Fie%3DUTF8%26node%3D3745171&pf_rd_p=247034601&pf_rd_s=left-nav-1&pf_rd_t=101&pf_rd_i=507846&pf_rd_m=ATVPDKIKX0DER&pf_rd_r=14989TMFGQ7QJ53ESP6Z"">Fresh Flowers & Indoor Plants</a></div><div class=""leftNav""><a href=""/gp/amabot/?pf_rd_url=%2Fgp%2Fgift-central%2Fhomepage%2Fref%3Dgw_br_gifts%2F002-3729295-8812824&pf_rd_p=247034601&pf_rd_s=left-nav-1&pf_rd_t=101&pf_rd_i=507846&pf_rd_m=ATVPDKIKX0DER&pf_rd_r=14989TMFGQ7QJ53ESP6Z"">Gift Central</a></div><div class=""leftNav""><a href=""/gp/amabot/?pf_rd_url=%2Fgp%2Fproduct%2FB00067L6TQ%2Fref%3Dgw_br_gcer%2F002-3729295-8812824&pf_rd_p=247034601&pf_rd_s=left-nav-1&pf_rd_t=101&pf_rd_i=507846&pf_rd_m=ATVPDKIKX0DER&pf_rd_r=14989TMFGQ7QJ53ESP6Z"">Gift Certificates</a></div><div class=""leftNav""><a href=""/gp/amabot/?pf_rd_url=%2Fgp%2Frsl%2Fshoppinglist%2Fyourshoppinglist%2Fref%3Dgw_br_shop%2F002-3729295-8812824&pf_rd_p=247034601&pf_rd_s=left-nav-1&pf_rd_t=101&pf_rd_i=507846&pf_rd_m=ATVPDKIKX0DER&pf_rd_r=14989TMFGQ7QJ53ESP6Z"">Shopping List</a></div><div class=""leftNav""><a href=""/gp/amabot/?pf_rd_url=%2Fgp%2Fregistry%2Fwedding-homepage.html%2Fref%3Dbr_wedr%2F002-3729295-8812824&pf_rd_p=247034601&pf_rd_s=left-nav-1&pf_rd_t=101&pf_rd_i=507846&pf_rd_m=ATVPDKIKX0DER&pf_rd_r=14989TMFGQ7QJ53ESP6Z"">Wedding Registry</a></div><div class=""leftNav""><a href=""/gp/amabot/?pf_rd_url=%2Fgp%2Fregistry%2Fregistry.html%2Fref%3Dgw_br_wl%2F002-3729295-8812824%3Fie%3DUTF8%26type%3Dwishlist&pf_rd_p=247034601&pf_rd_s=left-nav-1&pf_rd_t=101&pf_rd_i=507846&pf_rd_m=ATVPDKIKX0DER&pf_rd_r=14989TMFGQ7QJ53ESP6Z"">Wish List</a></div><div class=""leftNav""><a href=""/gp/amabot/?pf_rd_url=%2Fgp%2Flibrary%2Fref%3Dgw_br_library%2F002-3729295-8812824&pf_rd_p=247034601&pf_rd_s=left-nav-1&pf_rd_t=101&pf_rd_i=507846&pf_rd_m=ATVPDKIKX0DER&pf_rd_r=14989TMFGQ7QJ53ESP6Z"">Your Media Library</a></div></td><td style=""font-size: 4px; border-right:1px solid #5C9EBF;"">&nbsp;</td></tr><tr><td background=""http://ec1.images-amazon.com/images/G/01/nav2/images/box-line-bl-6pts._V50452336_.gif""></td><td background=""http://ec1.images-amazon.com/images/G/01/nav2/images/box-line-br-6pts._V50452336_.gif"" height=""12""></td></tr><tr><td><img src=""http://ec1.images-amazon.com/images/G/01/x-locale/common/transparent-pixel.gif"" width=""1"" height=""1"" border=""0"" /></td><td><img src=""http://ec1.images-amazon.com/images/G/01/x-locale/common/transparent-pixel.gif"" width=""12"" height=""1"" border=""0"" /></td></tr></table><br clear=""all"" /><br />
    
<table border=""0"" cellpadding=""0"" cellspacing=""0"" width=100%""><tr><td background=""http://ec1.images-amazon.com/images/G/01/nav2/images/box-shade-tl-6pts-tall._V63800021_.jpg"" class=""searchtitle"" style=""padding-left: 10px; padding-top: 6px; padding-bottom: 5px; font-size: small"">Amazon Services</td><td background=""http://ec1.images-amazon.com/images/G/01/nav2/images/box-shade-tr2-6pts-tall._V63800021_.jpg"" width=""12"">&nbsp;</td></tr><tr>
<td style=""padding-left:8px; padding-top:3px;	border-left:1px solid #5C9EBF;"">
<div class=""leftNavTitle"">Make Money</div><div class=""leftNav""><a href=""/gp/amabot/?pf_rd_url=%2Fgp%2Fredirect.html%2Fref%3Dgw_br_adv%2F002-3729295-8812824%3Flocation%3Dhttp%3A%2F%2Fadvantage.amazon.com%2Fgp%2Fvendor%2Fpublic%2Fjoin%2F%26token%3D8A42249D1B51779DE51C03226E939CB7EF5FF354&pf_rd_p=233773001&pf_rd_s=left-nav-2&pf_rd_t=101&pf_rd_i=507846&pf_rd_m=ATVPDKIKX0DER&pf_rd_r=14989TMFGQ7QJ53ESP6Z"">Advantage</a></div><div class=""leftNav""><a href=""/gp/amabot/?pf_rd_url=%2Fgp%2Fredirect.html%2Fref%3Dgw_br_assoc%2F002-3729295-8812824%3Flocation%3Dhttp%3A%2F%2Fassociates.amazon.com%2Fgp%2Fassociates%2Fjoin%26token%3D7528600194FF91C7B31919BB060E9948E888519F&pf_rd_p=233773001&pf_rd_s=left-nav-2&pf_rd_t=101&pf_rd_i=507846&pf_rd_m=ATVPDKIKX0DER&pf_rd_r=14989TMFGQ7QJ53ESP6Z"">Associates</a></div><div class=""leftNav""><a href=""/gp/amabot/?pf_rd_url=%2FCorporate-Accounts%2Fb%2Fref%3Dgw_br_corpacc%2F002-3729295-8812824%3Fie%3DUTF8%26node%3D600460&pf_rd_p=233773001&pf_rd_s=left-nav-2&pf_rd_t=101&pf_rd_i=507846&pf_rd_m=ATVPDKIKX0DER&pf_rd_r=14989TMFGQ7QJ53ESP6Z"">Corporate Accounts</a></div><div class=""leftNav""><a href=""/gp/amabot/?pf_rd_url=%2Fexec%2Fobidos%2Fsubst%2Fmisc%2Fco-op%2Fsmall-vendor-info.html%2Fref%3Dgw_br_paidpl%2F002-3729295-8812824&pf_rd_p=233773001&pf_rd_s=left-nav-2&pf_rd_t=101&pf_rd_i=507846&pf_rd_m=ATVPDKIKX0DER&pf_rd_r=14989TMFGQ7QJ53ESP6Z"">Paid Placements</a></div><div class=""leftNav""><a href=""/gp/amabot/?pf_rd_url=%2Fexec%2Fobidos%2Fsubst%2Fmisc%2Fsell-your-stuff.html%2Fref%3Dgw_br_sell%2F002-3729295-8812824&pf_rd_p=233773001&pf_rd_s=left-nav-2&pf_rd_t=101&pf_rd_i=507846&pf_rd_m=ATVPDKIKX0DER&pf_rd_r=14989TMFGQ7QJ53ESP6Z"">Sell Your Stuff</a></div><div class=""leftNavTitle"">For the Community</div><div class=""leftNav""><a href=""/gp/amabot/?pf_rd_url=%2Fgp%2Farms%2Fdirectory%2Fref%3Dgw_br_connect%2F002-3729295-8812824&pf_rd_p=233773001&pf_rd_s=left-nav-2&pf_rd_t=101&pf_rd_i=507846&pf_rd_m=ATVPDKIKX0DER&pf_rd_r=14989TMFGQ7QJ53ESP6Z"">Amazon Connect</a></div><div class=""leftNav""><a href=""/gp/amabot/?pf_rd_url=%2Fgp%2Fentertainment%2Ffishbowl%2Fref%3Dgw_br_fishbowl%2F002-3729295-8812824&pf_rd_p=233773001&pf_rd_s=left-nav-2&pf_rd_t=101&pf_rd_i=507846&pf_rd_m=ATVPDKIKX0DER&pf_rd_r=14989TMFGQ7QJ53ESP6Z"">Amazon Fishbowl</a></div><div class=""leftNav""><a href=""/gp/amabot/?pf_rd_url=%2Fgp%2Fgss%2Fdetail%2F10840%2Fref%3Dgw_br_podcast%2F002-3729295-8812824&pf_rd_p=233773001&pf_rd_s=left-nav-2&pf_rd_t=101&pf_rd_i=507846&pf_rd_m=ATVPDKIKX0DER&pf_rd_r=14989TMFGQ7QJ53ESP6Z"">Amazon Wire Podcast</a></div><div class=""leftNav""><a href=""/gp/amabot/?pf_rd_url=%2Fgp%2Fgss%2Fref%3Dgw_br_gss%2F002-3729295-8812824&pf_rd_p=233773001&pf_rd_s=left-nav-2&pf_rd_t=101&pf_rd_i=507846&pf_rd_m=ATVPDKIKX0DER&pf_rd_r=14989TMFGQ7QJ53ESP6Z"">E-Mail Subscriptions</a></div><div class=""leftNav""><a href=""/gp/amabot/?pf_rd_url=%2Fb%2Fref%3Dgw_br_giving%2F002-3729295-8812824%3Fie%3DUTF8%26node%3D13786321&pf_rd_p=233773001&pf_rd_s=left-nav-2&pf_rd_t=101&pf_rd_i=507846&pf_rd_m=ATVPDKIKX0DER&pf_rd_r=14989TMFGQ7QJ53ESP6Z"">Giving at Amazon</a></div><div class=""leftNavTitle"">For Developers</div><div class=""leftNav""><a href=""/gp/amabot/?pf_rd_url=%2FAWS-home-page-Money%2Fb%2Fref%3Dgw_br_websvcs%2F002-3729295-8812824%3Fie%3DUTF8%26node%3D3435361&pf_rd_p=233773001&pf_rd_s=left-nav-2&pf_rd_t=101&pf_rd_i=507846&pf_rd_m=ATVPDKIKX0DER&pf_rd_r=14989TMFGQ7QJ53ESP6Z"">Amazon Web Services</a></div><div class=""leftNavTitle"">Local</div><div class=""leftNav""><a href=""/gp/amabot/?pf_rd_url=%2Fb%2Fref%3Dgw_br_yp_new%2F002-3729295-8812824%3Fie%3DUTF8%26node%3D3999141&pf_rd_p=233773001&pf_rd_s=left-nav-2&pf_rd_t=101&pf_rd_i=507846&pf_rd_m=ATVPDKIKX0DER&pf_rd_r=14989TMFGQ7QJ53ESP6Z"">Yellow Pages</a></div><div class=""leftNavTitle"">Partner Services</div><div class=""leftNav""><a href=""/gp/amabot/?pf_rd_url=%2FBroadband-Services%2Fb%2Fref%3Dgw_br_bband%2F002-3729295-8812824%3Fie%3DUTF8%26node%3D51117011&pf_rd_p=233773001&pf_rd_s=left-nav-2&pf_rd_t=101&pf_rd_i=507846&pf_rd_m=ATVPDKIKX0DER&pf_rd_r=14989TMFGQ7QJ53ESP6Z"">Broadband Services</a></div><div class=""leftNav""><a href=""/gp/amabot/?pf_rd_url=%2FFinancial-Services%2Fb%2Fref%3Dgw_br_financial%2F002-3729295-8812824%3Fie%3DUTF8%26node%3D16292601&pf_rd_p=233773001&pf_rd_s=left-nav-2&pf_rd_t=101&pf_rd_i=507846&pf_rd_m=ATVPDKIKX0DER&pf_rd_r=14989TMFGQ7QJ53ESP6Z"">Financial Services</a></div><div class=""leftNav""><a href=""/gp/amabot/?pf_rd_url=%2FTravel%2Fb%2Fref%3Dgw_br_travel%2F002-3729295-8812824%3Fie%3DUTF8%26node%3D605012&pf_rd_p=233773001&pf_rd_s=left-nav-2&pf_rd_t=101&pf_rd_i=507846&pf_rd_m=ATVPDKIKX0DER&pf_rd_r=14989TMFGQ7QJ53ESP6Z"">Travel Services</a></div></td><td style=""font-size: 4px; border-right:1px solid #5C9EBF;"">&nbsp;</td></tr><tr><td background=""http://ec1.images-amazon.com/images/G/01/nav2/images/box-line-bl-6pts._V50452336_.gif""></td><td background=""http://ec1.images-amazon.com/images/G/01/nav2/images/box-line-br-6pts._V50452336_.gif"" height=""12""></td></tr><tr><td><img src=""http://ec1.images-amazon.com/images/G/01/x-locale/common/transparent-pixel.gif"" width=""1"" height=""1"" border=""0"" /></td><td><img src=""http://ec1.images-amazon.com/images/G/01/x-locale/common/transparent-pixel.gif"" width=""12"" height=""1"" border=""0"" /></td></tr></table><br clear=""all"" /><br />
        </td>
        <td valign=""top"" class=""amabot_center"">
          
    
    
  
  
    
    
          
    
    
  <table border=""0"" cellspacing=""0"" cellpadding=""0"" class=""amabot_widget"" align=""center""><tr><td><center><a href=""/gp/amabot/?pf_rd_url=%2Fgp%2Fcobrandcard%2Fmarketing.html%2Fref%3Dcobrand_ch_gw_tcg%2F002-3729295-8812824%3Fie%3DUTF8%26source%3Dh%26type%3Dp&pf_rd_p=246314701&pf_rd_s=center-1&pf_rd_t=101&pf_rd_i=507846&pf_rd_m=ATVPDKIKX0DER&pf_rd_r=14989TMFGQ7QJ53ESP6Z""><img src=""http://ec1.images-amazon.com/images/G/01/marketing/visa/weblab/gateway_ccwl_t3.gif"" width=""380"" align=""center"" height=""120"" border=""0"" /></a></center></td></tr></table><br clear=""all""><br><table border=""0"" cellspacing=""0"" cellpadding=""0"" class=""amabot_widget""><tr><td><b class=""h1"">This Pearl Means More Than Business</b><br clear=""left""><a href=""/gp/amabot/?pf_rd_url=%2Fexec%2Fobidos%2FASIN%2FB000ID10JE%2Fref%3Damb_link_3562992_1%2F002-3729295-8812824&pf_rd_p=245978901&pf_rd_s=center-2&pf_rd_t=101&pf_rd_i=507846&pf_rd_m=ATVPDKIKX0DER&pf_rd_r=14989TMFGQ7QJ53ESP6Z""><img src=""http://ec1.images-amazon.com/images/P/B000ID10JE.01._PE_SCTZZZZZZZ_V41035660_.jpg"" width=""59"" align=""left"" alt=""Learn more about the Pearl"" height=""121"" border=""0"" /></a> 
         
The hyper-sleek <a href=""/gp/amabot/?pf_rd_url=%2Fexec%2Fobidos%2FASIN%2FB000ID10JE%2Fref%3Damb_link_3562992_2%2F002-3729295-8812824&pf_rd_p=245978901&pf_rd_s=center-2&pf_rd_t=101&pf_rd_i=507846&pf_rd_m=ATVPDKIKX0DER&pf_rd_r=14989TMFGQ7QJ53ESP6Z"">BlackBerry Pearl</a> from <a href=""/gp/amabot/?pf_rd_url=%2FT-Mobile-Carrier-Cell-Phones%2Fb%2Fref%3Damb_link_3562992_3%2F002-3729295-8812824%3Fie%3DUTF8%26node%3D301197&pf_rd_p=245978901&pf_rd_s=center-2&pf_rd_t=101&pf_rd_i=507846&pf_rd_m=ATVPDKIKX0DER&pf_rd_r=14989TMFGQ7QJ53ESP6Z"">T-Mobile</a> has arrived. Not just a business device, it comes loaded with a megapixel camera, Bluetooth, video, MP3, and more.<br clear=""all""><ul><li><a href=""/gp/amabot/?pf_rd_url=%2Fexec%2Fobidos%2FASIN%2FB000ID10JE%2Fref%3Damb_link_3562992_4%2F002-3729295-8812824&pf_rd_p=245978901&pf_rd_s=center-2&pf_rd_t=101&pf_rd_i=507846&pf_rd_m=ATVPDKIKX0DER&pf_rd_r=14989TMFGQ7QJ53ESP6Z"">BlackBerry Pearl: price too low to show</a></li><li><a href=""/gp/amabot/?pf_rd_url=%2Fexec%2Fobidos%2Fsearch-handle-url%2F002-3729295-8812824%3F%255Fencoding%3DUTF8%26index%3Dwireless-phones%26field-keywords%3DT-Mobile%2520BlackBerry&pf_rd_p=245978901&pf_rd_s=center-2&pf_rd_t=101&pf_rd_i=507846&pf_rd_m=ATVPDKIKX0DER&pf_rd_r=14989TMFGQ7QJ53ESP6Z"">See all BlackBerry devices from T-Mobile</a></li></ul><p><strong><font color=""cc6600"">&#8250;</font></strong>&nbsp;
            <font face=""verdana,arial,helvetica"" size=""-1""><a href=""/gp/amabot/?pf_rd_url=%2Fexec%2Fobidos%2Ftg%2Ffeature%2F-%2F1270801%2Fref%3Damb_link_3562992_5%2F002-3729295-8812824&pf_rd_p=245978901&pf_rd_s=center-2&pf_rd_t=101&pf_rd_i=507846&pf_rd_m=ATVPDKIKX0DER&pf_rd_r=14989TMFGQ7QJ53ESP6Z"">See information regarding your equipment
		  discount</a></font></p></td></tr></table><br clear=""all""><br><div class=""small""><table border=""0"" cellpadding=""8"" cellspacing=""0"" width=""100%""><tr><td><table border=""0"" cellpadding=""8"" cellspacing=""0"" width=""100%""><tr><td><b class=""h1"">Instant $20 Off and FREE Shipping Direct from Amazon.com</b></td></tr></table><table border=""0"" cellpadding=""8"" cellspacing=""0"" width=""100%""><tr><td class=""small"" width=""33%"" valign=""top"" align=""left""><a href=""/gp/amabot/?pf_rd_url=%2Fgp%2Fredirect.html%2Fref%3Damb_link_1701432_1%2F002-3729295-8812824%3Fie%3DUTF8%26location%3Dhttp%253A%252F%252Fwww.amazon.com%252Fgp%252Fsearch%252F%253Fnode%253D1045744%2526emi%253DATVPDKIKX0DER%26token%3D3A0F170E7CEFE27BDC730D3D7344512BC1296B83&pf_rd_p=185219501&pf_rd_s=center-3&pf_rd_t=101&pf_rd_i=507846&pf_rd_m=ATVPDKIKX0DER&pf_rd_r=14989TMFGQ7QJ53ESP6Z""><img src=""http://ec1.images-amazon.com/images/G/01/shoes/gateway/B0007MCQRG.01._V40418960_.jpg"" width=""110"" height=""110"" border=""0"" /></a><span class=""tiny""></span><div style=""margin-top: 6px; margin-left: 1px;"" class=""tiny""><strong><font color=""cc6600"">&#8250;</font></strong>&nbsp;
            <a href=""/gp/amabot/?pf_rd_url=%2Fgp%2Fredirect.html%2Fref%3Damb_link_1701432_2%2F002-3729295-8812824%3Fie%3DUTF8%26location%3Dhttp%253A%252F%252Fwww.amazon.com%252Fgp%252Fsearch%252F%253Fnode%253D1045744%2526emi%253DATVPDKIKX0DER%26token%3D3A0F170E7CEFE27BDC730D3D7344512BC1296B83&pf_rd_p=185219501&pf_rd_s=center-3&pf_rd_t=101&pf_rd_i=507846&pf_rd_m=ATVPDKIKX0DER&pf_rd_r=14989TMFGQ7QJ53ESP6Z"">Men's</a></div></td><td class=""small"" width=""33%"" valign=""top"" align=""left""><a href=""/gp/amabot/?pf_rd_url=%2Fgp%2Fredirect.html%2Fref%3Damb_link_1701432_3%2F002-3729295-8812824%3Fie%3DUTF8%26location%3Dhttp%253A%252F%252Fwww.amazon.com%252Fgp%252Fsearch%252F%253Fnode%253D1044764%2526emi%253DATVPDKIKX0DER%26token%3D3A0F170E7CEFE27BDC730D3D7344512BC1296B83&pf_rd_p=185219501&pf_rd_s=center-3&pf_rd_t=101&pf_rd_i=507846&pf_rd_m=ATVPDKIKX0DER&pf_rd_r=14989TMFGQ7QJ53ESP6Z""><img src=""http://ec1.images-amazon.com/images/G/01/shoes/gateway/B000F8UCSW.01._V40418960_.jpg"" width=""110"" height=""110"" border=""0"" /></a><span class=""tiny""></span><div style=""margin-top: 6px; margin-left: 1px;"" class=""tiny""><strong><font color=""cc6600"">&#8250;</font></strong>&nbsp;
            <a href=""/gp/amabot/?pf_rd_url=%2Fgp%2Fredirect.html%2Fref%3Damb_link_1701432_4%2F002-3729295-8812824%3Fie%3DUTF8%26location%3Dhttp%253A%252F%252Fwww.amazon.com%252Fgp%252Fsearch%252F%253Fnode%253D1044764%2526emi%253DATVPDKIKX0DER%26token%3D3A0F170E7CEFE27BDC730D3D7344512BC1296B83&pf_rd_p=185219501&pf_rd_s=center-3&pf_rd_t=101&pf_rd_i=507846&pf_rd_m=ATVPDKIKX0DER&pf_rd_r=14989TMFGQ7QJ53ESP6Z"">Women's</a></div></td><td class=""small"" width=""33%"" valign=""top"" align=""left""><a href=""/gp/amabot/?pf_rd_url=%2Ftp%3A%2F%2Fwww.amazon.com%2Fgp%2Fsearch%2F%2Fref%3Damb_link_1701432_5%2F002-3729295-8812824%3F%255Fencoding%3DUTF8%26node%3D1044476%26emi%3DATVPDKIKX0DER&pf_rd_p=185219501&pf_rd_s=center-3&pf_rd_t=101&pf_rd_i=507846&pf_rd_m=ATVPDKIKX0DER&pf_rd_r=14989TMFGQ7QJ53ESP6Z""><img src=""http://ec1.images-amazon.com/images/G/01/shoes/gateway/B000GK38UI.01._V40418960_.jpg"" width=""110"" height=""110"" border=""0"" /></a><span class=""tiny""></span><div style=""margin-top: 6px; margin-left: 1px;"" class=""tiny""><strong><font color=""cc6600"">&#8250;</font></strong>&nbsp;
            <a href=""/gp/amabot/?pf_rd_url=%2Fgp%2Fredirect.html%2Fref%3Damb_link_1701432_6%2F002-3729295-8812824%3Fie%3DUTF8%26location%3Dhttp%253A%252F%252Fwww.amazon.com%252Fgp%252Fsearch%252F%253Fnode%253D1044476%2526emi%253DATVPDKIKX0DER%26token%3D3A0F170E7CEFE27BDC730D3D7344512BC1296B83&pf_rd_p=185219501&pf_rd_s=center-3&pf_rd_t=101&pf_rd_i=507846&pf_rd_m=ATVPDKIKX0DER&pf_rd_r=14989TMFGQ7QJ53ESP6Z"">Children's</a></div></td></tr></table></td></tr></table><br></div><br clear=""all""><div class=""small""><table border=""0"" cellpadding=""8"" cellspacing=""0"" width=""100%""><tr><td><table border=""0"" cellpadding=""8"" cellspacing=""0"" width=""100%""><tr><td><b class=""h1"">Jewelry with a Twist</b></td></tr></table><table border=""0"" cellpadding=""8"" cellspacing=""0"" width=""100%""><tr><td class=""small"" width=""33%"" valign=""top"" align=""left""><a href=""/gp/amabot/?pf_rd_url=%2Fexec%2Fobidos%2FASIN%2FB000CME2DM%2Fref%3Damb_link_794722_1%2F002-3729295-8812824&pf_rd_p=194577501&pf_rd_s=center-4&pf_rd_t=101&pf_rd_i=507846&pf_rd_m=ATVPDKIKX0DER&pf_rd_r=14989TMFGQ7QJ53ESP6Z""><img src=""http://ec1.images-amazon.com/images/G/01/jewelry/110/B000CME2DM._V58563486_.jpg"" width=""110"" height=""110"" border=""0"" /></a><span class=""tiny""></span><div style=""margin-top: 6px; margin-left: 1px;"" class=""tiny""><strong><font color=""cc6600"">&#8250;</font></strong>&nbsp;
            <a href=""/gp/amabot/?pf_rd_url=%2Fgp%2Fredirect.html%2Fref%3Damb_link_794722_2%2F002-3729295-8812824%3Fie%3DUTF8%26location%3Dhttp%253A%252F%252Fwww.amazon.com%252Fgp%252Fsearch.html%252F%253Fnode%253D3880591%2526x%253D4%2526keywords%253DFiligree%252520-Bonn%2526index%253Djewelry%2526y%253D9%2526rank%253D-launch-date%26token%3D3A0F170E7CEFE27BDC730D3D7344512BC1296B83&pf_rd_p=194577501&pf_rd_s=center-4&pf_rd_t=101&pf_rd_i=507846&pf_rd_m=ATVPDKIKX0DER&pf_rd_r=14989TMFGQ7QJ53ESP6Z"">Filigree Jewelry</a></div></td><td class=""small"" width=""33%"" valign=""top"" align=""left""><a href=""/gp/amabot/?pf_rd_url=%2Fexec%2Fobidos%2FASIN%2FB00081NCCY%2Fref%3Damb_link_794722_3%2F002-3729295-8812824&pf_rd_p=194577501&pf_rd_s=center-4&pf_rd_t=101&pf_rd_i=507846&pf_rd_m=ATVPDKIKX0DER&pf_rd_r=14989TMFGQ7QJ53ESP6Z""><img src=""http://ec1.images-amazon.com/images/G/01/jewelry/110/B00081NCCY._V55616874_.jpg"" width=""110"" height=""110"" border=""0"" /></a><span class=""tiny""></span><div style=""margin-top: 6px; margin-left: 1px;"" class=""tiny""><strong><font color=""cc6600"">&#8250;</font></strong>&nbsp;
            <a href=""/gp/amabot/?pf_rd_url=%2Fgp%2Fredirect.html%2Fref%3Damb_link_794722_4%2F002-3729295-8812824%3Fie%3DUTF8%26location%3Dhttp%253A%252F%252Fwww.amazon.com%252Fgp%252Fsearch.html%252F%253Fnode%253D3880591%2526keywords%253DGeometric%252520-Bonn%2526rank%253D-launch-date%26token%3D3A0F170E7CEFE27BDC730D3D7344512BC1296B83&pf_rd_p=194577501&pf_rd_s=center-4&pf_rd_t=101&pf_rd_i=507846&pf_rd_m=ATVPDKIKX0DER&pf_rd_r=14989TMFGQ7QJ53ESP6Z"">Geometric Jewelry</a></div></td><td class=""small"" width=""33%"" valign=""top"" align=""left""><a href=""/gp/amabot/?pf_rd_url=%2Fexec%2Fobidos%2FASIN%2FB0007MF3QC%2Fref%3Damb_link_794722_5%2F002-3729295-8812824&pf_rd_p=194577501&pf_rd_s=center-4&pf_rd_t=101&pf_rd_i=507846&pf_rd_m=ATVPDKIKX0DER&pf_rd_r=14989TMFGQ7QJ53ESP6Z""><img src=""http://ec1.images-amazon.com/images/G/01/jewelry/110/B0007MF3QC._V58584475_.jpg"" width=""90"" height=""110"" border=""0"" /></a><span class=""tiny""></span><div style=""margin-top: 6px; margin-left: 1px;"" class=""tiny""><strong><font color=""cc6600"">&#8250;</font></strong>&nbsp;
            <a href=""/gp/amabot/?pf_rd_url=%2Fgp%2Fredirect.html%2Fref%3Damb_link_794722_6%2F002-3729295-8812824%3Fie%3DUTF8%26location%3Dhttp%253A%252F%252Fwww.amazon.com%252Fgp%252Fsearch.html%252F%253Fnode%253D3880591%2526x%253D17%2526keywords%253DCluster%252520-Bonn%2526index%253Djewelry%2526y%253D10%2526rank%253D-launch-date%26token%3D3A0F170E7CEFE27BDC730D3D7344512BC1296B83&pf_rd_p=194577501&pf_rd_s=center-4&pf_rd_t=101&pf_rd_i=507846&pf_rd_m=ATVPDKIKX0DER&pf_rd_r=14989TMFGQ7QJ53ESP6Z"">Cluster Jewelry</a></div></td></tr></table></td></tr></table><br></div><br clear=""all""><table border=""0"" cellspacing=""0"" cellpadding=""0"" class=""amabot_widget""><tr><td><b class=""h1"">Hundreds of DVDs up to 50% Off in the Big DVD Sale</b><br clear=""left""><a href=""/gp/amabot/?pf_rd_url=%2Fexec%2Fobidos%2FASIN%2FB00009NHB6%2Fref%3Damb_link_3444792_%2F002-3729295-8812824&pf_rd_p=241951501&pf_rd_s=center-5&pf_rd_t=101&pf_rd_i=507846&pf_rd_m=ATVPDKIKX0DER&pf_rd_r=14989TMFGQ7QJ53ESP6Z""><img src=""http://ec1.images-amazon.com/images/P/B00009NHB6.01._PE50_OU01_SCTZZZZZZZ_.jpg"" width=""67"" align=""left"" height=""99"" border=""0"" /></a><a href=""/gp/amabot/?pf_rd_url=%2Fexec%2Fobidos%2FASIN%2FB000EGEJI4%2Fref%3Damb_link_3444792_%2F002-3729295-8812824&pf_rd_p=241951501&pf_rd_s=center-5&pf_rd_t=101&pf_rd_i=507846&pf_rd_m=ATVPDKIKX0DER&pf_rd_r=14989TMFGQ7QJ53ESP6Z""><img src=""http://ec1.images-amazon.com/images/P/B000EGEJI4.01._PE50_OU01_SCTZZZZZZZ_V65934062_.jpg"" width=""92"" align=""left"" height=""99"" border=""0"" /></a>
Save on hundreds of DVDs in the <a href=""/gp/amabot/?pf_rd_url=%2FBig-DVD-Sale%2Fb%2Fref%3Damb_link_3444792_1%2F002-3729295-8812824%3Fie%3DUTF8%26node%3D1061354&pf_rd_p=241951501&pf_rd_s=center-5&pf_rd_t=101&pf_rd_i=507846&pf_rd_m=ATVPDKIKX0DER&pf_rd_r=14989TMFGQ7QJ53ESP6Z"">Big DVD Sale</a>!  Choose from theatrical hits, complete TV seasons, special editions, and recently repriced DVDs for up to 50% off.<br clear=""all""><ul><li><a href=""/gp/amabot/?pf_rd_url=%2FDVDs-Low-4-97-Big-Sale%2Fb%2Fref%3Damb_link_3444792_2%2F002-3729295-8812824%3Fie%3DUTF8%26node%3D3151151&pf_rd_p=241951501&pf_rd_s=center-5&pf_rd_t=101&pf_rd_i=507846&pf_rd_m=ATVPDKIKX0DER&pf_rd_r=14989TMFGQ7QJ53ESP6Z"">80 DVDs for as low as $5.47, including <i>The Magic School Bus</i></a></li><li><a href=""/gp/amabot/?pf_rd_url=%2FDVDs-Low-7-47-Big-Sale%2Fb%2Fref%3Damb_link_3444792_3%2F002-3729295-8812824%3Fie%3DUTF8%26node%3D3151161&pf_rd_p=241951501&pf_rd_s=center-5&pf_rd_t=101&pf_rd_i=507846&pf_rd_m=ATVPDKIKX0DER&pf_rd_r=14989TMFGQ7QJ53ESP6Z"">150 DVDs as low as $7.47, including <i>The Haunting</i></a></li><li><a href=""/gp/amabot/?pf_rd_url=%2FDVDs-Low-9-97-Big-Sale%2Fb%2Fref%3Damb_link_3444792_4%2F002-3729295-8812824%3Fie%3DUTF8%26node%3D3151171&pf_rd_p=241951501&pf_rd_s=center-5&pf_rd_t=101&pf_rd_i=507846&pf_rd_m=ATVPDKIKX0DER&pf_rd_r=14989TMFGQ7QJ53ESP6Z"">130 DVDs as low as $9.97, including <i>Unforgiven</i></a></li><li><a href=""/gp/amabot/?pf_rd_url=%2Fexec%2Fobidos%2Ftg%2Ffeature%2F-%2F306190%2Fref%3Damb_link_3444792_5%2F002-3729295-8812824&pf_rd_p=241951501&pf_rd_s=center-5&pf_rd_t=101&pf_rd_i=507846&pf_rd_m=ATVPDKIKX0DER&pf_rd_r=14989TMFGQ7QJ53ESP6Z"">50 television DVDs for up to 50% off, including <i>The West Wing</i></a></li></ul><p><strong><font color=""cc6600"">&#8250;</font></strong>&nbsp;
            <font face=""verdana,arial,helvetica"" size=""-1""><a href=""/gp/amabot/?pf_rd_url=%2FBig-DVD-Sale%2Fb%2Fref%3Damb_link_3444792_6%2F002-3729295-8812824%3Fie%3DUTF8%26node%3D1061354&pf_rd_p=241951501&pf_rd_s=center-5&pf_rd_t=101&pf_rd_i=507846&pf_rd_m=ATVPDKIKX0DER&pf_rd_r=14989TMFGQ7QJ53ESP6Z"">Go to the sale</a></font></p></td></tr></table><br clear=""all""><br><table border=""0"" cellspacing=""0"" cellpadding=""0"" class=""amabot_widget""><tr><td><b class=""h1"">""D"" is for Delectable Data Device</b><br clear=""left""><a href=""/gp/amabot/?pf_rd_url=%2FT-Mobile-Carrier-Cell-Phones%2Fb%2Fref%3Damb_link_1809832_1%2F002-3729295-8812824%3Fie%3DUTF8%26node%3D301197&pf_rd_p=241841601&pf_rd_s=center-6&pf_rd_t=101&pf_rd_i=507846&pf_rd_m=ATVPDKIKX0DER&pf_rd_r=14989TMFGQ7QJ53ESP6Z""><img src=""http://ec1.images-amazon.com/images/G/01/wireless/90/tmo-data._V60047995_.jpg"" width=""195"" alt=""Get your favorite T-Mobile data device now"" align=""left"" height=""110"" border=""0"" /></a> 
	  
         
Want a superior data device that won't cost a fortune each month? Check out the <a href=""/gp/amabot/?pf_rd_url=%2Fexec%2Fobidos%2FASIN%2FB000EM6MQ0%2Fref%3Damb_link_1809832_2%2F002-3729295-8812824&pf_rd_p=241841601&pf_rd_s=center-6&pf_rd_t=101&pf_rd_i=507846&pf_rd_m=ATVPDKIKX0DER&pf_rd_r=14989TMFGQ7QJ53ESP6Z"">SDA</a>, <a href=""/gp/amabot/?pf_rd_url=%2Fexec%2Fobidos%2FASIN%2FB000EM8REU%2Fref%3Damb_link_1809832_3%2F002-3729295-8812824&pf_rd_p=241841601&pf_rd_s=center-6&pf_rd_t=101&pf_rd_i=507846&pf_rd_m=ATVPDKIKX0DER&pf_rd_r=14989TMFGQ7QJ53ESP6Z"">MDA</a>, or <a href=""/gp/amabot/?pf_rd_url=%2Fexec%2Fobidos%2FASIN%2FB000FEHG76%2Fref%3Damb_link_1809832_4%2F002-3729295-8812824&pf_rd_p=241841601&pf_rd_s=center-6&pf_rd_t=101&pf_rd_i=507846&pf_rd_m=ATVPDKIKX0DER&pf_rd_r=14989TMFGQ7QJ53ESP6Z"">BlackBerry 8700g</a>  with a service plan from <a href=""/gp/amabot/?pf_rd_url=%2FT-Mobile-Carrier-Cell-Phones%2Fb%2Fref%3Damb_link_1809832_5%2F002-3729295-8812824%3Fie%3DUTF8%26node%3D301197&pf_rd_p=241841601&pf_rd_s=center-6&pf_rd_t=101&pf_rd_i=507846&pf_rd_m=ATVPDKIKX0DER&pf_rd_r=14989TMFGQ7QJ53ESP6Z"">T-Mobile</a> and get more from your cell phone.<br clear=""all""><ul><li><a href=""/gp/amabot/?pf_rd_url=%2Fexec%2Fobidos%2FASIN%2FB000EM6MQ0%2Fref%3Damb_link_1809832_6%2F002-3729295-8812824&pf_rd_p=241841601&pf_rd_s=center-6&pf_rd_t=101&pf_rd_i=507846&pf_rd_m=ATVPDKIKX0DER&pf_rd_r=14989TMFGQ7QJ53ESP6Z"">T-Mobile SDA: price too low to show</a></li><li><a href=""/gp/amabot/?pf_rd_url=%2Fexec%2Fobidos%2FASIN%2FB000EM8REU%2Fref%3Damb_link_1809832_7%2F002-3729295-8812824&pf_rd_p=241841601&pf_rd_s=center-6&pf_rd_t=101&pf_rd_i=507846&pf_rd_m=ATVPDKIKX0DER&pf_rd_r=14989TMFGQ7QJ53ESP6Z"">T-Mobile MDA: price too low to show</a></li><li><a href=""/gp/amabot/?pf_rd_url=%2Fexec%2Fobidos%2FASIN%2FB000FEHG76%2Fref%3Damb_link_1809832_8%2F002-3729295-8812824&pf_rd_p=241841601&pf_rd_s=center-6&pf_rd_t=101&pf_rd_i=507846&pf_rd_m=ATVPDKIKX0DER&pf_rd_r=14989TMFGQ7QJ53ESP6Z"">BlackBerry 8700g: price too low to show</a></li></ul><p><strong><font color=""cc6600"">&#8250;</font></strong>&nbsp;
            <font face=""verdana,arial,helvetica"" size=""-1""><a href=""/gp/amabot/?pf_rd_url=%2Fexec%2Fobidos%2Ftg%2Ffeature%2F-%2F1270801%2Fref%3Damb_link_1809832_9%2F002-3729295-8812824&pf_rd_p=241841601&pf_rd_s=center-6&pf_rd_t=101&pf_rd_i=507846&pf_rd_m=ATVPDKIKX0DER&pf_rd_r=14989TMFGQ7QJ53ESP6Z"">See information regarding your equipment
		  discount</a></font></p></td></tr></table><br clear=""all""><br><div class=""small""><table border=""0"" cellpadding=""8"" cellspacing=""0"" width=""100%""><tr><td><table border=""0"" cellpadding=""8"" cellspacing=""0"" width=""100%""><tr><td><b class=""h1"">Discover Diamonds, Accessories, and More in Jewelry &amp; Watches</b></td></tr></table><table border=""0"" cellpadding=""8"" cellspacing=""0"" width=""100%""><tr><td class=""small"" width=""33%"" valign=""top"" align=""left""><a href=""/gp/amabot/?pf_rd_url=%2Fexec%2Fobidos%2FASIN%2FB0009EYJJU%2Fref%3Damb_link_1842732_2%2F002-3729295-8812824&pf_rd_p=218409901&pf_rd_s=center-7&pf_rd_t=101&pf_rd_i=507846&pf_rd_m=ATVPDKIKX0DER&pf_rd_r=14989TMFGQ7QJ53ESP6Z""><img src=""http://ec1.images-amazon.com/images/P/B0009EYJJU.01._PE_SCTZZZZZZZ_.jpg"" width=""100"" height=""121"" border=""0"" /></a><br clear=""all""><b class=""small""><a href=""/gp/amabot/?pf_rd_url=%2Fb%2Fref%3Damb_link_1842732_1%2F002-3729295-8812824%3Fie%3DUTF8%26node%3D3888131&pf_rd_p=218409901&pf_rd_s=center-7&pf_rd_t=101&pf_rd_i=507846&pf_rd_m=ATVPDKIKX0DER&pf_rd_r=14989TMFGQ7QJ53ESP6Z"">Cufflinks</a></b><br><span class=""tiny""></span></td><td class=""small"" width=""33%"" valign=""top"" align=""left""><a href=""/gp/amabot/?pf_rd_url=%2Fexec%2Fobidos%2FASIN%2FB000ELYHG8%2Fref%3Damb_link_1842732_4%2F002-3729295-8812824&pf_rd_p=218409901&pf_rd_s=center-7&pf_rd_t=101&pf_rd_i=507846&pf_rd_m=ATVPDKIKX0DER&pf_rd_r=14989TMFGQ7QJ53ESP6Z""><img src=""http://ec1.images-amazon.com/images/G/01/00/10/00/19/28/90/100019289013._V50759116_.jpg"" width=""121"" height=""121"" border=""0"" /></a><br clear=""all""><b class=""small""><a href=""/gp/amabot/?pf_rd_url=%2FDiamonds-Materials-Jewelry-Watches%2Fb%2Fref%3Damb_link_1842732_3%2F002-3729295-8812824%3Fie%3DUTF8%26node%3D15959421&pf_rd_p=218409901&pf_rd_s=center-7&pf_rd_t=101&pf_rd_i=507846&pf_rd_m=ATVPDKIKX0DER&pf_rd_r=14989TMFGQ7QJ53ESP6Z"">Diamond Jewelry</a></b><br><span class=""tiny""></span></td><td class=""small"" width=""33%"" valign=""top"" align=""left""><a href=""/gp/amabot/?pf_rd_url=%2Fexec%2Fobidos%2FASIN%2FB000BEZTKG%2Fref%3Damb_link_1842732_6%2F002-3729295-8812824&pf_rd_p=218409901&pf_rd_s=center-7&pf_rd_t=101&pf_rd_i=507846&pf_rd_m=ATVPDKIKX0DER&pf_rd_r=14989TMFGQ7QJ53ESP6Z""><img src=""http://ec1.images-amazon.com/images/P/B000BEZTKG.01._PE69_OU01_SCTZZZZZZZ_V59812940_.jpg"" width=""121"" height=""121"" border=""0"" /></a><br clear=""all""><b class=""small""><a href=""/gp/amabot/?pf_rd_url=%2Fb%2Fref%3Damb_link_1842732_5%2F002-3729295-8812824%3Fie%3DUTF8%26node%3D3889331&pf_rd_p=218409901&pf_rd_s=center-7&pf_rd_t=101&pf_rd_i=507846&pf_rd_m=ATVPDKIKX0DER&pf_rd_r=14989TMFGQ7QJ53ESP6Z"">Men's Watches</a></b><br><span class=""tiny""></span></td></tr><tr><td class=""small"" width=""33%"" valign=""top"" align=""left""><a href=""/gp/amabot/?pf_rd_url=%2Fexec%2Fobidos%2FASIN%2FB0007RTBXS%2Fref%3Damb_link_1842732_8%2F002-3729295-8812824&pf_rd_p=218409901&pf_rd_s=center-7&pf_rd_t=101&pf_rd_i=507846&pf_rd_m=ATVPDKIKX0DER&pf_rd_r=14989TMFGQ7QJ53ESP6Z""><img src=""http://ec1.images-amazon.com/images/P/B0007RTBXS.01._PE54_OU01_SCTZZZZZZZ_.jpg"" width=""85"" height=""121"" border=""0"" /></a><br clear=""all""><b class=""small""><a href=""/gp/amabot/?pf_rd_url=%2Fb%2Fref%3Damb_link_1842732_7%2F002-3729295-8812824%3Fie%3DUTF8%26node%3D3889801&pf_rd_p=218409901&pf_rd_s=center-7&pf_rd_t=101&pf_rd_i=507846&pf_rd_m=ATVPDKIKX0DER&pf_rd_r=14989TMFGQ7QJ53ESP6Z"">Women's Watches</a></b><br><span class=""tiny""></span><div style=""margin-top: 6px; margin-left: 1px;"" class=""tiny""><strong><font color=""cc6600"">&#8250;</font></strong>&nbsp;
            <a href=""/gp/amabot/?pf_rd_url=%2Fjewelry-watches-engagements-rings-diamonds%2Fb%2Fref%3Damb_link_1842732_9%2F002-3729295-8812824%3Fie%3DUTF8%26node%3D3367581&pf_rd_p=218409901&pf_rd_s=center-7&pf_rd_t=101&pf_rd_i=507846&pf_rd_m=ATVPDKIKX0DER&pf_rd_r=14989TMFGQ7QJ53ESP6Z"">More Jewelry &amp; Watches</a></div></td><td class=""small"" width=""33%"" valign=""top"" align=""left""><a href=""/gp/amabot/?pf_rd_url=%2Fexec%2Fobidos%2FASIN%2FB0009EYJJ0%2Fref%3Damb_link_1842732_11%2F002-3729295-8812824&pf_rd_p=218409901&pf_rd_s=center-7&pf_rd_t=101&pf_rd_i=507846&pf_rd_m=ATVPDKIKX0DER&pf_rd_r=14989TMFGQ7QJ53ESP6Z""><img src=""http://ec1.images-amazon.com/images/G/01/00/10/00/19/28/90/100019289037._V50759089_.jpg"" width=""121"" height=""121"" border=""0"" /></a><br clear=""all""><b class=""small""><a href=""/gp/amabot/?pf_rd_url=%2Fb%2Fref%3Damb_link_1842732_10%2F002-3729295-8812824%3Fie%3DUTF8%26node%3D3888561&pf_rd_p=218409901&pf_rd_s=center-7&pf_rd_t=101&pf_rd_i=507846&pf_rd_m=ATVPDKIKX0DER&pf_rd_r=14989TMFGQ7QJ53ESP6Z"">Key Rings</a></b><br><span class=""tiny""></span></td><td class=""small"" width=""33%"" valign=""top"" align=""left""><a href=""/gp/amabot/?pf_rd_url=%2Fexec%2Fobidos%2FASIN%2FB000204DJC%2Fref%3Damb_link_1842732_13%2F002-3729295-8812824&pf_rd_p=218409901&pf_rd_s=center-7&pf_rd_t=101&pf_rd_i=507846&pf_rd_m=ATVPDKIKX0DER&pf_rd_r=14989TMFGQ7QJ53ESP6Z""><img src=""http://ec1.images-amazon.com/images/G/01/watches/121/B000204DJC._V63924335_.jpg"" width=""121"" height=""121"" border=""0"" /></a><br clear=""all""><b class=""small""><a href=""/gp/amabot/?pf_rd_url=%2Fb%2Fref%3Damb_link_1842732_12%2F002-3729295-8812824%3Fie%3DUTF8%26node%3D15326681&pf_rd_p=218409901&pf_rd_s=center-7&pf_rd_t=101&pf_rd_i=507846&pf_rd_m=ATVPDKIKX0DER&pf_rd_r=14989TMFGQ7QJ53ESP6Z"">Watch Cases</a></b><br><span class=""tiny""></span></td></tr></table></td></tr></table><br></div><br clear=""all""><table border=""0"" style=""margin-bottom:20px;"" width=""100%""><tr><td style=""text-align:center; width:300px; height:125px""><iframe marginheight=""0"" marginwidth=""0"" style=""width:300px; height:125px;"" height=""125"" width=""300"" src=""javascript:location.replace('http://ad.doubleclick.net/adi/amazon.pilot/;cid=3423932;sz=300x125;ord=' + Math.floor(Math.random()*1000) + '?')"" scrolling=""no"" frameborder=""0""></iframe></td></tr></table>
        </td>
        <td valign=""top"" width=""300"" class=""amabot_right"">
          
    
    
  
          
    
    
  <table border=""0"" cellspacing=""0"" cellpadding=""0"" class=""amabot_widget""><tr><td><b class=""h1"">New Slingboxes Are Here</b><br clear=""left""><a href=""/gp/amabot/?pf_rd_url=%2Fgp%2Ffeature.html%2Fref%3Damb_link_3607982_1%2F002-3729295-8812824%3Fie%3DUTF8%26docId%3D1000009071&pf_rd_p=245069501&pf_rd_s=right-1&pf_rd_t=101&pf_rd_i=507846&pf_rd_m=ATVPDKIKX0DER&pf_rd_r=14989TMFGQ7QJ53ESP6Z""><img src=""http://ec1.images-amazon.com/images/P/B000IVDIL4.01._PE_SCTHUMBZZZ_V40508197_.jpg"" width=""82"" align=""left"" alt=""Announcing three new Slingboxes from Sling Media"" height=""40"" border=""0"" /></a>Sling Media just announced <a href=""/gp/amabot/?pf_rd_url=%2Fgp%2Ffeature.html%2Fref%3Damb_link_3607982_2%2F002-3729295-8812824%3Fie%3DUTF8%26docId%3D1000009071&pf_rd_p=245069501&pf_rd_s=right-1&pf_rd_t=101&pf_rd_i=507846&pf_rd_m=ATVPDKIKX0DER&pf_rd_r=14989TMFGQ7QJ53ESP6Z"">three new Slingbox models</a>. Order yours from Amazon.com today and start enjoying your TV anywhere.
<br clear=""left""><p><strong><font color=""cc6600"">&#8250;</font></strong>&nbsp;
            <font face=""verdana,arial,helvetica"" size=""-1""><a href=""/gp/amabot/?pf_rd_url=%2Fexec%2Fobidos%2Ftg%2Ffeature%2F-%2F1000009071%2Fref%3Damb_link_3607982_3%2F002-3729295-8812824&pf_rd_p=245069501&pf_rd_s=right-1&pf_rd_t=101&pf_rd_i=507846&pf_rd_m=ATVPDKIKX0DER&pf_rd_r=14989TMFGQ7QJ53ESP6Z"">See the new Slingboxes</a></font></p></td></tr></table><br clear=""all""><br><table border=""0"" cellspacing=""0"" cellpadding=""0"" class=""amabot_widget""><tr><td><b class=""h1""><i>X-Men - The Last Stand</i> 50% Off</b><br clear=""left""><a href=""/gp/amabot/?pf_rd_url=%2Fexec%2Fobidos%2FASIN%2FB000HCO83Q%2Fref%3Damb_link_3605332_%2F002-3729295-8812824&pf_rd_p=246018001&pf_rd_s=right-2&pf_rd_t=101&pf_rd_i=507846&pf_rd_m=ATVPDKIKX0DER&pf_rd_r=14989TMFGQ7QJ53ESP6Z""><img src=""http://ec1.images-amazon.com/images/P/B000HCO83Q.01._PE50_OU01_SCTZZZZZZZ_V61199661_.jpg"" width=""88"" align=""left"" height=""121"" border=""0"" /></a><a href=""/gp/amabot/?pf_rd_url=%2Fexec%2Fobidos%2FASIN%2FB000HCO83Q%2Fref%3Damb_link_3605332_1%2F002-3729295-8812824&pf_rd_p=246018001&pf_rd_s=right-2&pf_rd_t=101&pf_rd_i=507846&pf_rd_m=ATVPDKIKX0DER&pf_rd_r=14989TMFGQ7QJ53ESP6Z""><i>X-Men - The Last Stand</i></a> is now  
available on DVD. Get your copy today for 50% off and visit the 
cool <a href=""/gp/amabot/?pf_rd_url=%2Fgp%2Fredirect.html%2Fref%3Damb_link_3605332_2%2F002-3729295-8812824%3Flocation%3Dhttp%3A%2F%2Fwww.foxhome.com%2Fx3%2Fminisite%26token%3D37606C512365C8E15DD31D547791E740DE0044F9&pf_rd_p=246018001&pf_rd_s=right-2&pf_rd_t=101&pf_rd_i=507846&pf_rd_m=ATVPDKIKX0DER&pf_rd_r=14989TMFGQ7QJ53ESP6Z"" onClick=""window.open(this.href,null,'width=900,height=1200'); return false;""><i>X-Men - The Last Stand</i> microsite</a>. 
</td></tr></table><br clear=""all""><br><table border=""0"" cellspacing=""0"" cellpadding=""0"" class=""amabot_widget""><tr><td><b class=""h1"">Save 30% on the New <i>Pride and Prejudice</i> DVD Collector's Set</b><br clear=""left""><a href=""/gp/amabot/?pf_rd_url=%2Fexec%2Fobidos%2FASIN%2FB000F0UUT6%2Fref%3Damb_link_3606342_%2F002-3729295-8812824&pf_rd_p=245051701&pf_rd_s=right-3&pf_rd_t=101&pf_rd_i=507846&pf_rd_m=ATVPDKIKX0DER&pf_rd_r=14989TMFGQ7QJ53ESP6Z""><img src=""http://ec1.images-amazon.com/images/P/B000F0UUT6.01._PE42_OU01_SCTHUMBZZZ_V60348143_.jpg"" width=""82"" align=""left"" height=""55"" border=""0"" /></a>
Save 30% on the limited-edition collector's set of <a href=""/gp/amabot/?pf_rd_url=%2Fexec%2Fobidos%2FASIN%2FB000F0UUT6%2Fref%3Damb_link_3606342_1%2F002-3729295-8812824&pf_rd_p=245051701&pf_rd_s=right-3&pf_rd_t=101&pf_rd_i=507846&pf_rd_m=ATVPDKIKX0DER&pf_rd_r=14989TMFGQ7QJ53ESP6Z""><i>Pride and Prejudice</i></a>, featuring an extra DVD, a deluxe book, and gorgeous packaging.</td></tr></table><br clear=""all""><br><table border=""0"" cellspacing=""0"" cellpadding=""0"" class=""amabot_widget""><tr><td><b class=""h1"">Smart Phone, Smart Price</b><br clear=""left""><a href=""/gp/amabot/?pf_rd_url=%2Fexec%2Fobidos%2Fsearch-handle-url%2F002-3729295-8812824%3F%255Fencoding%3DUTF8%26index%3Dwireless-accessories%26field-keywords%3Di-mate%2520Jam&pf_rd_p=247568501&pf_rd_s=right-4&pf_rd_t=101&pf_rd_i=507846&pf_rd_m=ATVPDKIKX0DER&pf_rd_r=14989TMFGQ7QJ53ESP6Z""><img src=""http://ec1.images-amazon.com/images/P/B000BNH022.01._PE57_OU01_SCTHUMBZZZ_.jpg"" width=""51"" align=""left"" alt=""Get a smart phone at a smart price"" height=""82"" border=""0"" /></a>Pick up a 64 MB or 128 MB <a href=""/gp/amabot/?pf_rd_url=%2Fexec%2Fobidos%2Fsearch-handle-url%2F002-3729295-8812824%3F%255Fencoding%3DUTF8%26index%3Dwireless-accessories%26field-keywords%3Di-mate%2520Jam&pf_rd_p=247568501&pf_rd_s=right-4&pf_rd_t=101&pf_rd_i=507846&pf_rd_m=ATVPDKIKX0DER&pf_rd_r=14989TMFGQ7QJ53ESP6Z"">unlocked i-mate Jam</a>  Windows Mobile smartphone at a new, super-low price.<br clear=""left""><p><strong><font color=""cc6600"">&#8250;</font></strong>&nbsp;
            <font face=""verdana,arial,helvetica"" size=""-1""><a href=""/gp/amabot/?pf_rd_url=%2Fexec%2Fobidos%2Fsearch-handle-url%2F002-3729295-8812824%3F%255Fencoding%3DUTF8%26index%3Dwireless-accessories%26field-keywords%3Di-mate%2520Jam&pf_rd_p=247568501&pf_rd_s=right-4&pf_rd_t=101&pf_rd_i=507846&pf_rd_m=ATVPDKIKX0DER&pf_rd_r=14989TMFGQ7QJ53ESP6Z"">Offers good only while supplies last</a></font></p></td></tr></table><br clear=""all""><br><table border=""0"" cellspacing=""0"" cellpadding=""0"" class=""amabot_widget""><tr><td><b class=""h1"">Light Up the Night</b><br clear=""left""><a href=""/gp/amabot/?pf_rd_url=%2Fgp%2Fredirect.html%2Fref%3Damb_link_1804132_1%2F002-3729295-8812824%3Fie%3DUTF8%26location%3Dhttp%253A%252F%252Fwww.amazon.com%252Fgp%252Fsearch%253Fsearch-alias%253Dautomotive%2526field-keywords%253DStreetGlow%26token%3D3A0F170E7CEFE27BDC730D3D7344512BC1296B83&pf_rd_p=233160201&pf_rd_s=right-5&pf_rd_t=101&pf_rd_i=507846&pf_rd_m=ATVPDKIKX0DER&pf_rd_r=14989TMFGQ7QJ53ESP6Z""><img src=""http://ec1.images-amazon.com/images/G/01/Automotive/rotos/streetglowwheelsmall._V65217386_.jpg"" width=""89"" align=""left"" height=""75"" border=""0"" /></a>If your ride needs some eye candy, check out our selection of <a href=""/gp/amabot/?pf_rd_url=%2Fgp%2Fredirect.html%2Fref%3Damb_link_1804132_2%2F002-3729295-8812824%3Fie%3DUTF8%26location%3Dhttp%253A%252F%252Fwww.amazon.com%252Fgp%252Fsearch%253Fsearch-alias%253Dautomotive%2526field-keywords%253DStreetGlow%26token%3D3A0F170E7CEFE27BDC730D3D7344512BC1296B83&pf_rd_p=233160201&pf_rd_s=right-5&pf_rd_t=101&pf_rd_i=507846&pf_rd_m=ATVPDKIKX0DER&pf_rd_r=14989TMFGQ7QJ53ESP6Z"">StreetGlow neon and LED lighting accessories</a>. They're easy to install and look great.
</td></tr></table><br clear=""all""><br><table border=""0"" cellspacing=""0"" cellpadding=""0"" class=""amabot_widget""><tr><td><b class=""h1"">Save with the Amazon.com Visa Card</b><br clear=""left""><a href=""/gp/amabot/?pf_rd_url=%2Fgp%2Fcobrandcard%2Fmarketing.html%2Fref%3Dcobrand_ch_gw%2F002-3729295-8812824%3Fie%3DUTF8%26source%3Dh%26type%3DP&pf_rd_p=246513401&pf_rd_s=right-6-contract&pf_rd_t=101&pf_rd_i=507846&pf_rd_m=ATVPDKIKX0DER&pf_rd_r=14989TMFGQ7QJ53ESP6Z""><img src=""http://ec1.images-amazon.com/images/G/01/marketing/visa/rewards/rewards-card_77._V52790619_.gif"" width=""77"" align=""left"" height=""73"" border=""0"" /></a><b>Save $30 instantly</b> and earn up to <b>3% rewards</b> with the <a href=""/gp/amabot/?pf_rd_url=%2Fgp%2Fcobrandcard%2Fmarketing.html%2Fref%3Dcobrand_ch_gw%2F002-3729295-8812824%3Fie%3DUTF8%26source%3Dh%26type%3DP&pf_rd_p=246513401&pf_rd_s=right-6-contract&pf_rd_t=101&pf_rd_i=507846&pf_rd_m=ATVPDKIKX0DER&pf_rd_r=14989TMFGQ7QJ53ESP6Z"">Amazon.com Visa Card</a>.</td></tr></table><br clear=""all""><br>
        </td>
      </tr>
      <tr height=""1"">
        <td height=""1"" align=""left""><img src=""http://ec1.images-amazon.com/images/G/01/x-locale/common/transparent-pixel.gif"" width=""90"" height=""1"" border=""0"" /></td>
        <td height=""1"" align=""left""><img src=""http://ec1.images-amazon.com/images/G/01/x-locale/common/transparent-pixel.gif"" width=""400"" height=""1"" border=""0"" /></td>
        <td height=""1"" align=""left""><img src=""http://ec1.images-amazon.com/images/G/01/x-locale/common/transparent-pixel.gif"" width=""300"" height=""1"" border=""0"" /></td>
      </tr>
    </table>
    
    
    
  
<table border=""0"" cellspacing=""0"" cellpadding=""0"" width=""100%"" class=""tigerbox"" style=""margin-bottom: 20px""><tr><td><table border=""0"" class=""small"" cellpadding=""0"" cellspacing=""0"" width=""100%""><tr><td><table border=""0"" cellpadding=""8"" cellspacing=""0"" width=""100%""><tr><td><b class=""h1"">Shop These Stores at Amazon.com</b></td></tr></table><table border=""0"" cellpadding=""8"" cellspacing=""0"" width=""100%""><tr><td class=""small"" width=""20%"" valign=""top"" align=""left""><a href=""/gp/amabot/?pf_rd_url=%2FTarget%2Fb%2Fref%3Damb_link_3254762_1%2F002-3729295-8812824%3Fie%3DUTF8%26node%3D700060&pf_rd_p=236194701&pf_rd_s=bottom-logos-1&pf_rd_t=101&pf_rd_i=507846&pf_rd_m=ATVPDKIKX0DER&pf_rd_r=14989TMFGQ7QJ53ESP6Z""><img src=""http://ec1.images-amazon.com/images/G/01/gateway/partner-logos/target_logo._V58636053_.gif"" width=""120"" height=""28"" border=""0"" /></a><span class=""tiny""></span></td><td class=""small"" width=""20%"" valign=""top"" align=""left""><a href=""/gp/amabot/?pf_rd_url=%2FOffice-Depot%2Fb%2Fref%3Damb_link_3254762_2%2F002-3729295-8812824%3Fie%3DUTF8%26node%3D1064952&pf_rd_p=236194701&pf_rd_s=bottom-logos-1&pf_rd_t=101&pf_rd_i=507846&pf_rd_m=ATVPDKIKX0DER&pf_rd_r=14989TMFGQ7QJ53ESP6Z""><img src=""http://ec1.images-amazon.com/images/G/01/gateway/partner-logos/officedepot_logo._V58636063_.gif"" width=""140"" height=""30"" border=""0"" /></a><span class=""tiny""></span></td><td colspan=""3"">&nbsp;</td></tr></table></td></tr></table></td></tr><tr><td><hr class=""hr-leftbrowse"" noshade=""true"" size=""1"" /></td></tr><tr><td><table border=""0"" class=""small"" cellpadding=""0"" cellspacing=""0"" width=""100%""><tr><td><table border=""0"" cellpadding=""8"" cellspacing=""0"" width=""100%""><tr><td><b class=""h1"">Visit Our Featured Partners' Web Sites</b></td></tr></table><table border=""0"" cellpadding=""8"" cellspacing=""0"" width=""100%""><tr><td class=""small"" width=""20%"" valign=""top"" align=""left""><a href=""/gp/amabot/?pf_rd_url=https%3A%2F%2Fwww.amazon.com%3A443%2Fgp%2Fredirect.html%2Fref%3Damb_link_3254802_1%2F002-3729295-8812824%3Flocation%3Dhttps%3A%2F%2Fwww.fidelity.com%2Fframeless_amazon.shtml%26token%3DC7D288D73F6EE9E9BCD83D672FA04D9C6E681353&pf_rd_p=236194901&pf_rd_s=bottom-logos-2&pf_rd_t=101&pf_rd_i=507846&pf_rd_m=ATVPDKIKX0DER&pf_rd_r=14989TMFGQ7QJ53ESP6Z""><img src=""http://ec1.images-amazon.com/images/G/01/gateway/partner-logos/fidelity_logo._V58636034_.gif"" border=""0"" /></a><span class=""tiny""></span></td><td class=""small"" width=""20%"" valign=""top"" align=""left""><a href=""/gp/amabot/?pf_rd_url=%2Fgp%2Fredirect.html%2Fref%3Damb_link_3254802_2%2F002-3729295-8812824%3F%255Fencoding%3DUTF8%26location%3Dhttp%253A%252F%252Famazon.shutterfly.com%252F%253Fscic%253D0%2526ref%253Dce%25255Fstf%25255Fgw%26token%3DEC30763999BF7877D8867B47B481FBEB2C7A344A&pf_rd_p=236194901&pf_rd_s=bottom-logos-2&pf_rd_t=101&pf_rd_i=507846&pf_rd_m=ATVPDKIKX0DER&pf_rd_r=14989TMFGQ7QJ53ESP6Z""><img src=""http://ec1.images-amazon.com/images/G/01/gateway/partner-logos/shutterfly_logo._V58636062_.gif"" width=""100"" height=""32"" border=""0"" /></a><span class=""tiny""></span></td><td class=""small"" width=""20%"" valign=""top"" align=""left""><a href=""/gp/amabot/?pf_rd_url=%2Fgp%2Fredirect.html%2Fref%3Damb_link_3254802_3%2F002-3729295-8812824%3F%255Fencoding%3DUTF8%26location%3Dhttp%253A%252F%252Fwww.sidestep.com%252Fair%252F%253Fst3%253D10819098%2526st4%253D10010001%26token%3D79D531D9D160F0F0169E445C8A122D9527919616&pf_rd_p=236194901&pf_rd_s=bottom-logos-2&pf_rd_t=101&pf_rd_i=507846&pf_rd_m=ATVPDKIKX0DER&pf_rd_r=14989TMFGQ7QJ53ESP6Z""><img src=""http://ec1.images-amazon.com/images/G/01/gateway/partner-logos/sidestep_logo._V58636056_.gif"" border=""0"" /></a><span class=""tiny""></span></td><td class=""small"" width=""20%"" valign=""top"" align=""left""><a href=""/gp/amabot/?pf_rd_url=%2Fgp%2Fredirect.html%2Fref%3Damb_link_3254802_4%2F002-3729295-8812824%3F%255Fencoding%3DUTF8%26location%3Dhttp%253A%252F%252Fwww.tirerack.com%252Fa.jsp%253Fa%253DFB1%2526url%253D%25252Findex%25255Famazon.jsp%26token%3D4D16ECE409430205F24865C1FAD0EC3D05D97F83&pf_rd_p=236194901&pf_rd_s=bottom-logos-2&pf_rd_t=101&pf_rd_i=507846&pf_rd_m=ATVPDKIKX0DER&pf_rd_r=14989TMFGQ7QJ53ESP6Z""><img src=""http://ec1.images-amazon.com/images/G/01/gateway/partner-logos/tirerack_logo._V58636052_.gif"" border=""0"" /></a><span class=""tiny""></span></td><td class=""small"" width=""20%"" valign=""top"" align=""left""><a href=""/gp/amabot/?pf_rd_url=%2Fgp%2Fredirect.html%2Fref%3Damb_link_3254802_5%2F002-3729295-8812824%3Flocation%3Dhttp%3A%2F%2Fclk.atdmt.com%2FAVE%2Fgo%2Fcpblswwa0470000026ave%2Fdirect%3Bwi.135%3Bhi.15%2F01%26token%3DF2B383C0D8C522B93DD31762B537B25124ED9D35&pf_rd_p=236194901&pf_rd_s=bottom-logos-2&pf_rd_t=101&pf_rd_i=507846&pf_rd_m=ATVPDKIKX0DER&pf_rd_r=14989TMFGQ7QJ53ESP6Z""><img src=""http://ec1.images-amazon.com/images/G/01/gateway/partner-logos/weightwatchers_logo._V58636048_.gif"" border=""0"" /></a><span class=""tiny""></span></td></tr></table></td></tr></table></td></tr></table>
<style type=""text/css"">
.wonderbar-list {
    margin: 0px;
    padding: 0pt 0pt 0pt 11pt;
}
</style>
<tr><td colspan=""2"">
<form method=""GET"" action=""/s/ref=wbnavss/002-3729295-8812824"">
<table border=""0"" width=""100%"" cellpadding=""1"" cellspacing=""0"" bgcolor=""#999999"">
<tr><td>
<table border=""0"" width=""100%"" bgcolor=""#ffffff"" cellspacing=""0"" cellpadding=""5"">
<tr valign=""top"">
<td width=""33%"">
<b>Where's My Stuff?</b><br />
<ul class=""wonderbar-list"">
<li>Track your <a href=""https://www.amazon.com/gp/css/history/view.html/002-3729295-8812824?ie=UTF8&is-secure=true&orderFilter=wheres-my-stuff"">recent orders</a>.</li>
<li>View or change your orders in <a href=""/gp/css/homepage.html/002-3729295-8812824"">Your Account</a>.</li>
</ul>
</td>
<td width=""33%"">
<b>Shipping &amp; Returns</b><br />
<ul class=""wonderbar-list"">
<li>See our <a href=""/gp/help/customer/display.html/002-3729295-8812824?ie=UTF8&nodeId=468520"">shipping rates &amp; policies</a>.</li>
<li><a href=""/gp/css/returns/homepage.html/002-3729295-8812824?ie=UTF8"">Return</a> an item &#40;here&#39;s our <a href=""/gp/help/customer/display.html/002-3729295-8812824?ie=UTF8&nodeId=901888"">Returns Policy</a>&#41;.</li></ul>
</td>
<td width=""33%"">
<b>Need Help?</b><br />
<ul class=""wonderbar-list"">
<li>Forgot your password? <a href=""https://www.amazon.com/gp/css/account/forgot-password/email.html/002-3729295-8812824"">Click here</a>.</li> 
<li><a href=""/exec/obidos/subst/gifts/gift-certificates/gc-redeeming.html/ref=hy_f_7/002-3729295-8812824?%5Fencoding=UTF8"">Redeem</a> or <a href=""/gp/product/B00067L6TQ/ref=hy_f_8/002-3729295-8812824?ie=UTF8"">buy</a> a gift certificate.</li>
<li><a href=""/gp/help/customer/display.html/002-3729295-8812824?ie=UTF8&nodeId=508510"">Visit our Help department</a>.</li></ul>
</td></tr>
</table>
</td></tr>
<tr><td>
<!-- Search Box --->
<table border=""0"" width=""100%"" bgcolor=""#eeeecc"" cellspacing=""0"" cellpadding=""5""><tr><td align=""center"">
<b>Search</b>
<select name=""url""><option value=""index=blended"">Amazon.com</option><option value=""index=stripbooks:relevance-above"">Books</option><option value=""index=music"">Popular Music</option><option value=""index=music-dd"">Music Downloads</option><option value=""index=classical"">Classical Music</option><option value=""index=dvd"">DVD</option><option value=""index=amazontv&platform=gurupa"">Video Downloads</option><option value=""index=vhs"">VHS</option><option value=""index=apparel-index&platform=gurupa"">Apparel</option><option value=""index=grocery"">Grocery</option><option value=""index=local-index&platform=gurupa"">Yellow Pages</option><option value=""index=toys-and-games"">Toys</option><option value=""index=baby-products"">Baby</option><option value=""index=pc-hardware"">Computers</option><option value=""index=videogames"">Video Games</option><option value=""index=electronics"">Electronics</option><option value=""index=photo"">Camera & Photo</option><option value=""index=software"">Software</option><option value=""index=tools"">Tools & Hardware</option><option value=""index=office-products"">Office Products</option><option value=""index=magazines"">Magazines</option><option value=""index=sporting-index&platform=gurupa"">Sports & Outdoors</option><option value=""index=garden"">Outdoor Living</option><option value=""index=kitchen"">Kitchen</option><option value=""index=jewelry-index&platform=gurupa"">Jewelry & Watches</option><option value=""index=beauty-index&platform=gurupa"">Beauty</option><option value=""index=gourmet-index&platform=gurupa"">Gourmet Food</option><option value=""index=mi-index&platform=gurupa"">Musical Instruments</option><option value=""index=hpc-index&platform=gurupa"">Health/Personal Care</option><option value=""index=pet-supplies&store-name=kitchen&search-type=ss"">Pet Supplies</option><option value=""index=stripbooks:relevance-above&field-browse=27"">Travel</option><option value=""index=wireless-phones"">Cell Phones & Service</option><option value=""index=outlet"">Outlet</option><option value=""index=auction-redirect"">Auctions</option><option value=""index=fixed-price-redirect"">zShops</option><option value=""index=automotive-index&platform=gurupa"">Automotive</option><option value=""index=industrial-index&platform=gurupa"">Industrial & Scientific</option></select>
<input type=""text"" name=""field-keywords"" size=""15"">&nbsp;&nbsp;
<input type=""image"" src=""http://ec1.images-amazon.com/images/G/01/photo/go_button_photo.gif""  width=""21"" align=""absmiddle"" name=""Go"" height=""21"" border=""0"" /></td></tr></table>
</td></tr>
</table>
</form>
</td></tr>
 
<div align=""right"">
<a href=""#top"" class=""small"">Top of Page</a>
</div>
  
  
<style type=""text/css"">
<!--
.bottomNavLinks
{
  font-family: verdana,arial,helvetica,sans-serif;
  font-size: 13px;
  padding-top: 5px;
  padding-bottom: 5px;
}
-->
</style>
<table width=""100%"" align=""center"" border=""0""><tr><td><table class=""bottomNavLinks"" align=""center"" width=""100%"" cellpadding=""0"" cellspacing=""0"" border=""0"">
<tr align=""center""><td colspan=""2"">
<a href=""/exec/obidos/subst/home/all-stores.html/ref=gw_m_b_as/002-3729295-8812824?%5Fencoding=UTF8"">Directory of All Stores</a></td></tr>
</table></td></tr><tr><td><table class=""bottomNavLinks"" align=""center"" width=""100%"" cellpadding=""0"" cellspacing=""0"" border=""0""><tr align=""center""><td colspan=""2"">
International Sites:&nbsp;
<a href=""/gp/redirect.html/002-3729295-8812824?%5Fencoding=UTF8&location=http%3A%2F%2Fwww.amazon.ca%3Fsite%3Damazon%26tag%3Dintl-usfooter-cahome-21&token=AD297DDA0F9D6C887976CD08894B02DB57139A39"">Canada</a>&nbsp;&nbsp;|&nbsp;&nbsp;<a href=""/gp/redirect.html/002-3729295-8812824?%5Fencoding=UTF8&location=http%3A%2F%2Fwww.amazon.co.uk%3Fsite%3Damazon%26tag%3Dintl-usfooter-ukhome-21&token=EDA85A835C0C35E68FBAFD33CEB75576E7B44F1F"">United Kingdom</a>&nbsp;&nbsp;|&nbsp;&nbsp;<a href=""/gp/redirect.html/002-3729295-8812824?%5Fencoding=UTF8&location=http%3A%2F%2Fwww.amazon.de%3Fsite%3Damazon%26tag%3Dintl-usfooter-dehome-21&token=EBC7637E551C69E801C6B030EF8B5ED613A56E92"">Germany</a>&nbsp;&nbsp;|&nbsp;&nbsp;<a href=""/gp/redirect.html/002-3729295-8812824?%5Fencoding=UTF8&location=http%3A%2F%2Fwww.amazon.co.jp%3Fsite%3Damazon%26tag%3Dintl-usfooter-jphome-22&token=0AE1DFACC954F91986074504F57C1362C85FB6E8"">Japan</a>&nbsp;&nbsp;|&nbsp;&nbsp;<a href=""/gp/redirect.html/002-3729295-8812824?%5Fencoding=UTF8&location=http%3A%2F%2Fwww.amazon.fr%3Fsite%3Damazon%26tag%3Dintl-usfooter-frhome&token=EE1EA6AB57493F5C3DA46E2CE0BE07638B8F6F3F"">France</a>&nbsp;&nbsp;|&nbsp;&nbsp;<a href=""/gp/redirect.html/002-3729295-8812824?%5Fencoding=UTF8&location=http%3A%2F%2Fwww.joyo.com%3Fsource%3Damazon-usfooter&token=0C8F02DAE64B5895BDA317DE0E4110D9A77AA9A2"">China</a>
</td></tr>
</table></td></tr><tr><td><table class=""bottomNavLinks"" align=""center"" width=""100%"" cellpadding=""0"" cellspacing=""0"" border=""0"">
<tr align=""center""><td colspan=""2"">
<a href=""/gp/help/customer/display.html/ref=gw_m_b_he/002-3729295-8812824?ie=UTF8&nodeId=508510"">Help</a>&nbsp;&nbsp;|&nbsp;&nbsp;<a href=""/gp/cart/view.html/ref=gw_m_b_sc/002-3729295-8812824?ie=UTF8"">View Cart</a>&nbsp;&nbsp;|&nbsp;&nbsp;<a href=""/gp/css/homepage.html/ref=gw_m_b_ya/002-3729295-8812824?ie=UTF8"">Your Account</a>&nbsp;&nbsp;|&nbsp;&nbsp;<a href=""/Money-home-page/b/ref=gw_m_b_si/002-3729295-8812824?ie=UTF8&node=3309511"">Sell Items</a>&nbsp;&nbsp;|&nbsp;&nbsp;<a href=""/gp/sign-in.html/ref=gw_m_b_oc/002-3729295-8812824?ie=UTF8&path=%2Fexec%2Fobidos%2Fone-click-main&useRedirectOnSuccess=1&opt=a"">1-Click Settings</a></td></tr>
</table></td></tr><tr><td><table class=""bottomNavLinks"" align=""center"" width=""100%"" cellpadding=""0"" cellspacing=""0"" border=""0""><tr align=""center""><td colspan=""2"">
<a href=""/gp/redirect.html/002-3729295-8812824?%5Fencoding=UTF8&location=http%3A%2F%2Fphx.corporate-ir.net%2Fphoenix.zhtml%3Fp%3Dirol-irhome%26c%3D97664&token=C5CA27E570ABF6606B802CA4294B56184805E5A0"">Investor Relations</a>&nbsp;&nbsp;|&nbsp;&nbsp;<a href=""/gp/redirect.html/002-3729295-8812824?%5Fencoding=UTF8&location=http%3A%2F%2Fphx.corporate-ir.net%2Fphoenix.zhtml%3Fp%3Dirol-mediaHome%26c%3D176060&token=C5CA27E570ABF6606B802CA4294B56184805E5A0"">Press Release</a>&nbsp;&nbsp;|&nbsp;&nbsp;<a href=""/b/002-3729295-8812824?%5Fencoding=UTF8&node=14201851"">Careers at Amazon</a>&nbsp;&nbsp;|&nbsp;&nbsp;<a href=""/gp/redirect.html/002-3729295-8812824?%5Fencoding=UTF8&location=http%3A%2F%2Fassociates.amazon.com%2Fgp%2Fassociates%2Fjoin&token=CA33846610D84A7D03A14FC01A4C9307C6AE8023"">Join Associates</a>&nbsp;&nbsp;|&nbsp;&nbsp;<a href=""/exec/obidos/subst/partners/direct/direct-application.html/ref=gw_bt_ad/002-3729295-8812824?%5Fencoding=UTF8"">Join Advantage</a>&nbsp;&nbsp;|&nbsp;&nbsp;<a href=""http://zme.amazon.com/exec/varzea/subst/fx/home.html/ref=gw_bt_hs/002-3729295-8812824?%5Fencoding=UTF8"">Join Honor System</a>
</td></tr>
</table></td></tr></table>
<center class=""tiny"">
<a href=""/exec/obidos/subst/misc/policy/conditions-of-use.html/002-3729295-8812824"">Conditions of Use</a>
|
<a href=""/exec/obidos/tg/browse/-/468496/002-3729295-8812824"">Privacy Notice</a>
&copy; 1996-2008, Amazon.com, Inc. or its affiliates
</center>
<!-- whfh-dlMiMriNwU16xvv+KCOKa3XOlOwQm35Z rid-14989TMFGQ7QJ53ESP6Z -->
  </body>
</html>
<!-- MEOW -->
";
            XmlDocument doc;

            doc = HtmlParser.Parse(source);
            Assert.IsNotNull(doc["root"]["html"]["head"]);
            Assert.IsNotNull(doc["root"]["html"]["body"]);
        }

        [TestMethod]
        [TestProperty("Lib", "LillTek.Advanced")]
        public void HtmlParser_Parse_EBAY()
        {
            // Generate a DOM source scraped from www.ebay.com and 
            // make sure we don't see an exception.

            const string source =
@"
<html xmlns:IE=""#default""><head>
      <meta http-equiv=""Content-Type"" content=""text/html; charset=ISO-8859-1"">
   <base href=""http://pics.ebaystatic.com/aw/pics/""><meta name=""description"" content=""Buy and sell electronics, cars, clothing, apparel, collectibles, sporting goods, digital cameras, and everything else on eBay, the world's online marketplace. Sign up and begin to buy and sell - auction or buy it now - almost anything on eBay.com.""><meta name=""keywords"" content=""ebay, electronics, cars, clothing, apparel, collectibles, sporting goods, ebay, digital cameras, antiques, tickets, jewelry, online shopping, auction, online auction""><title>eBay - New &amp; used electronics, cars, apparel, collectibles, sporting goods &amp; more at low prices</title>

<!--pid:V3.hp.US.A.1-->

<script src=""http://include.ebaystatic.com/js/e479/us/homepage_e4795us.js"" type=""text/javascript""></script><link rel=""stylesheet"" type=""text/css"" href=""http://include.ebaystatic.com/aw/pics/us/css/homepage.css""><style type=""text/css"">
		    A.whitelinks:link{color:#ffffff;}
			.subtext{font-size: 11px;}
			.buttonsm {font-size: 11px; cursor: hand;}
			.btmbrdr {background: #FFFFE5 url(http://pics.ebaystatic.com/aw/pics/userSitePrefs/bottomDropShadow_20x20.gif) repeat-x bottom;} 
			.rtbrdr {background: #FFFFE5 url(http://pics.ebaystatic.com/aw/pics/userSitePrefs/sideDropShadow_20x20.gif) repeat-y left;} 
			.rt1brdr {background: #FFFFE5 url(http://pics.ebaystatic.com/aw/pics/userSitePrefs/dropshadow2_20x10.gif) repeat-y left;} 
			.lftbrdr {background-color: #FEEEA3;border-left: 2px solid #F9B709;} 
			.lft1brdr {background-color: #FFFFE5;border-left: 2px solid #F9B709;} 
			.topbrdr {background-color: #FEEEA3;border-top: 2px solid #F9B709;} 
			.favSelect {width: 100%;}
			.favNavHeader {	margin: 0;padding: 0 5px 0 0;background-color: #CECEFF;border: 1px solid #A9A9F7;}
			.favNavHeaderBuy {margin: 0;padding: 0 5px 0 0;background-color: #E2E3FF;border-top: 1px solid #A9A9F7;border-right: 1px solid #A9A9F7;
			border-left: 1px solid #A9A9F7;}
			.favNavContent {margin: 0;	padding: 5px;background-color: #FFF;border-width: 0 1px 1px 1px;border-style: solid;border-color: #A9A9F7;}
			.favCenter {padding: 0 16px 0 16px;}		
			@media all {
				IE\:HOMEPAGE {behavior:url(#default#homepage)}
			}		
		</style><style type=""text/css""><!--
						
								.favsearchbar {
				   margin-top:0px;
				   width:760px;
				   height:24px;
				   padding-top:2px;
				   padding-bottom:2px;
				   background-color:#FFCC00;
				   padding-left:10px;
				   margin-bottom:0px; }
						--></style></head><body bgcolor=""#FFFFFF"" link=""#0000FF"" onLoad=""init();toolboxOnLoad();"" onUnload=""cleanUp();""><script language=""javascript"" type=""text/javascript""><!--
			if(document.all)
				document.write(""<IE:HOMEPAGE ID = 'oHomePage' />"");
				
		//--></script><span id=""redirectUSP"">&nbsp;</span><table width=""746""><tr><td height=""5px""></td></tr><tr><td id=""sFlashTextId"" align=""right"" class=""navigation""></td></tr><tr><td height=""5px""></td></tr></table><table border=""0"" cellpadding=""0"" cellspacing=""0"" width=""760""><tr><td><div id=""ebx_layer"" style=""display:none;""> </div><!--Header code starts--><noscript><link rel=""stylesheet"" type=""text/css"" href=""http://include.ebaystatic.com/aw/pics/css/ebay.css""></noscript><script type=""text/javascript"" language=""JavaScript1.1"">includeHost = 'http://include.ebaystatic.com/';
		</script><script src=""http://include.ebaystatic.com/js/e479/us/ebaybase_e4795us.js""> </script><script src=""http://include.ebaystatic.com/js/e479/us/ebaysup_e4795us.js""> </script><script type=""text/javascript"" language=""JavaScript1.1"">
			ebay.oDocument._getControl(""headerCommon"")._exec(""writeStyleSheet"");
				</script><div id=""cobrandHeader""> </div><span class=""ebay""><form class=""nomargin"" method=""get"" name=""headerSearch"" onsubmit=""ebay.oDocument._getControl('searchHeader')._exec('submitHeaderSearch', this, 'http://search.ebay.com', '000', 'Start new search');"" action=""http://search.ebay.com/search/search.dll""><input type=""hidden"" name=""from"" value=""R40""><table border=""0"" cellpadding=""0"" cellspacing=""0"" width=""100%""><tr valign=""top""><td width=""0%""><script type=""text/javascript"" language=""JavaScript1.1"">ebay.oDocument._getControlEx(""cobrandCollection"")._exec(""writeBrow"");</script><a href=""http://www.ebay.com/""><img border=""0"" alt=""From collectibles to cars, buy and sell all kinds of items on eBay"" title=""From collectibles to cars, buy and sell all kinds of items on eBay"" src=""logos/logoEbay_150x70.gif"" align=""""></a></td><td width=""0%""><img src=""s.gif"" width=""10"" height=""1""></td><td height=""28"" width=""100%"" valign=""bottom""><table bgcolor=""#EFEFEF"" border=""0"" cellpadding=""0"" cellspacing=""0"" height=""28"" width=""100%""><tr><td height=""28"" valign=""top"" width=""0%""><img src=""navbar/topLeft_12x12.gif"" width=""12"" height=""12""></td><td class=""novisited"" nowrap width=""100%""><span class=""pipe""><a href=""http://www.ebay.com/"">home</a> | <a href=""http://my.ebay.com/ws/eBayISAPI.dll?MyeBay&amp;CurrentPage=MyeBayWon&amp;SubmitAction.ChangeFilter=x&amp;View=Won&amp;NewFilter=WaitPayment#Won"">pay</a><span><script type=""text/javascript"" language=""JavaScript1.1"">ebay.oDocument._getControl(""signIn"")._exec(""writeLink"",""register"",""http://cgi1.ebay.com/aw-cgi/eBayISAPI.dll?RegisterShow"","""","""",false,true,null);</script><noscript> | <a href=""http://cgi1.ebay.com/aw-cgi/eBayISAPI.dll?RegisterShow"">register</a></noscript></span><span><script type=""text/javascript"" language=""JavaScript1.1"">
			ebay.oDocument._getControl(""signIn"")._exec(""writeLink"",""sign in"",""http://signin.ebay.com/ws2/eBayISAPI.dll?SignIn&ssPageName=h:h:sin:US"",""sign out"",""http://signin.ebay.com/ws2/eBayISAPI.dll?SignIn&ssPageName=h:h:sout:US"",false,true,null);</script><noscript> | <a href=""http://signin.ebay.com/ws2/eBayISAPI.dll?SignIn"" class=""novisited"">sign in/out</a></noscript></span> | <a href=""http://pages.ebay.com/sitemap.html"">site map</a></span></td><td width=""0%""><img src=""s.gif"" width=""20"" height=""1""></td><td align=""right"" nowrap valign=""bottom"" width=""0%""><span class=""ebay""><script language=""JavaScript"">ebay.oDocument._getControl(""searchHeader"")._exec(""writeSearch"",""Start new search"",""20"", ""standard"");</script><noscript><input type=""text"" class=""prefill"" maxlength=""300"" name=""satitle"" size=""20""></noscript><img src=""s.gif"" width=""1"" height=""1""><input type=""submit"" class=""standard"" value=""Search""></span></td><td valign=""top""><img src=""navbar/topRight_12x12.gif"" width=""12"" height=""12""></td></tr></table><table bgcolor=""#EFEFEF"" border=""0"" cellpadding=""0"" cellspacing=""0"" height=""24"" width=""100%""><tr><td height=""24"" nowrap valign=""bottom"" width=""100%""><a href=""http://hub.ebay.com/buy""><img border=""0"" alt=""Shop for items"" title=""Shop for items"" src=""navbar/buy.gif"" align=""""></a><a href=""http://sell.ebay.com/sell""><img border=""0"" alt=""Sell your item"" title=""Sell your item"" src=""navbar/sell.gif"" align=""""></a><a href=""http://my.ebay.com/ws/eBayISAPI.dll?MyeBay""><img border=""0"" alt=""Track your eBay activities"" title=""Track your eBay activities"" src=""navbar/myebay.gif"" align=""""></a><a href=""http://hub.ebay.com/community""><img border=""0"" alt=""Learn, connect, and stay informed-for business and for fun"" title=""Learn, connect, and stay informed-for business and for fun"" src=""navbar/comm.gif"" align=""""></a><a href=""http://pages.ebay.com/help/index.html""><img border=""0"" alt=""Get help, find answers and contact Customer Support"" title=""Get help, find answers and contact Customer Support"" src=""navbar/help.gif"" align=""""></a></td><td align=""right"" class=""novisited"" nowrap valign=""top""><span class=""ebay""><a class=""standard"" style=""margin:0 12px 0 10px;display:block"" href=""http://search.ebay.com/ws/search/AdvSearch?sofindtype=1&amp;amp;ssPageName=h:h:advsearch:US"">Advanced Search</a></span></td></tr><tr><td bgcolor=""#000099"" colspan=""4"" height=""1""><img src=""s.gif"" height=""1""></td></tr></table><table border=""0"" cellpadding=""0"" cellspacing=""0"" width=""100%""><tr valign=""top""><td width=""100%""><span style=""margin:8px 0 8px 0;display:block""><script type=""text/javascript"" language=""Javascript1.1"">
		ebay.oDocument._getControl(""greetings"")._exec(""writePersonalHeader"",""Sign in"",""http://signin.ebay.com/ws2/eBayISAPI.dll?SignIn&ssPageName=h:h:sin:US"", ""register"",""http://cgi1.ebay.com/aw-cgi/eBayISAPI.dll?RegisterShow"", ""Sign out"",""http://signin.ebay.com/ws2/eBayISAPI.dll?SignIn"",'Hello! <span style=""white-space: nowrap;"" class=""help"">##1## or ##2##.</span>','Hello, ##1##! <span style=""white-space: nowrap;"" class=""help"">(##2##.)</span>','Hello, ##1##! <span style=""white-space: nowrap;"" class=""help"">(Not you? ##2##.)</span>','Hello! <a href=""http://signin.ebay.com/ws2/eBayISAPI.dll?SignIn"" class=""novisited"">Sign in/out</a>.','<img src=""icon/iconWarnRed_16x16.gif"" title=""Alert"" alt=""Alert"">',' | You have <a href=""http://my.ebay.com/ws/eBayISAPI.dll?MyeBay&amp;CurrentPage=MyeBayMyMessages&amp;ssPageName=STRK:MM:GHRMDR&amp;FolderId=0""> ##1## alert</a>.',' | You have <a href=""http://my.ebay.com/ws/eBayISAPI.dll?MyeBay&amp;CurrentPage=MyeBayMyMessages&amp;ssPageName=STRK:MM:GHRMDR&amp;FolderId=0""> ##1## alerts</a>.',1,null);</script><noscript>Hello! <a href=""http://signin.ebay.com/ws2/eBayISAPI.dll?SignIn"" class=""novisited"">Sign in/out</a>.</noscript><img src=""s.gif"" height=""16"" width=""1""><span style=""white-space: nowrap;"" class=""help"" id=""BTAMarker""> </span></span></td><td class=""navigation"" nowrap><script language=""JavaScript"">
														if(!document.layers){document.write('<img src=""s.gif"" width=""1"" height=""5"">');}
												</script><noscript><img src=""s.gif"" width=""1"" height=""5""></noscript></td><td width=""0%""><img src=""s.gif"" width=""10"" height=""1""></td><td align=""right"" width=""0%""><script>ebay.oDocument._getControl('poweredby')._exec('writeSource','<a onclick=""ebayShowPopupWindow(this.href, \'Sponsor\', 840, 660,false,false,false,\'yes\'); return false;"" href=""http://java.com/en/ebay7.jsp""><img src=""navbar/poweredByLogo_112x22.gif"" border=""0""></a>');</script><noscript><img src=""navbar/poweredByLogo_112x22.gif"" width=""112"" height=""22"" border=""0""></noscript></td></tr></table></td></tr></table></form></span><!--Header code ends--></td></tr></table><script>
			ebay.oDocument.oPage.createConfig = function()
			{
				 var c = ebay.oDocument.addConfig( new EbayConfig( ""Common.Flash.DoubleClick.UrlData"" ) );
				 c.sDomain = 'http://us.ebayobjects.com/';
				 c.sDartSite = 'ebay.us.homepage.visitor'; 
				 c.bUseRTM = true; 
			}
			ebay.oDocument.oPage.createConfig();
		</script><script language=""javascript"" type=""text/javascript""><!--
				slOut = 'homepage.visitor/visitor';
				if(typeof(dcSite) == ""undefined""){
					var dcSite = '/ebay.us.';
				}else{
					dcSite = '/ebay.us.';
				}
				if(typeof(isHomepage) == ""undefined""){
					var isHomepage = true;
				}
			//--></script><script language=""javascript"" type=""text/javascript""><!--
				function ebCreateConfigurations()
				{	
					 var hpRedesignConfig = ebay.oDocument.addConfig(new EbayConfig(""hpRedesign""));
					 hpRedesignConfig.sCustomAdParams = 'tp=1';
					 var c = ebay.oDocument.addConfig(new EbayConfig(""uspConfig""));  
					 c.redirectLayerId = 'redirectUSP';
					 c.currentSiteId = '0';
					 c.countryNames = ['Argentina', 'Australia', 'Austria', 'Belgium', 'Brazil', 'Canada', 'China', 'France', 'Germany', 'Hong Kong', 'India', 'Ireland', 'Italy', 'Korea', 'Malaysia', 'Mexico', 'Netherlands', 'New Zealand', 'Philippines', 'Poland', 'Singapore', 'Spain', 'Sweden', 'Switzerland', 'Taiwan', 'United Kingdom', 'US'];
				   
								    
				    c.countryIds = ['-1', '15', '16', '23', '-1', '2', '223', '71', '77', '201', '203', '205', '101', '-1', '207', '-1', '146', '208', '211', '212', '216', '186', '218', '193', '196', '3', '0'];								   
				   c.targetURL=['http://www.mercadolibre.com.ar/', 'http://www.ebay.com.au/', 'http://www.ebay.at/', 'http://www.ebay.be/', 'http://www.mercadolivre.com.br/', 'http://www.ebay.ca/', 'http://www.ebay.com.cn/', 'http://www.ebay.fr/', 'http://www.ebay.de/', 'http://www.ebay.com.hk/', 'http://www.ebay.in/', 'http://www.ebay.ie/', 'http://www.ebay.it/', 'http://www.auction.co.kr/', 'http://www.ebay.com.my/', 'http://www.mercadolibre.com.mx/', 'http://www.ebay.nl/', 'http://www.ebay.com/nz/', 'http://www.ebay.ph/', 'http://www.ebay.pl/', 'http://www.ebay.com.sg/', 'http://www.ebay.es/', 'http://www.ebay.se/', 'http://www.ebay.ch/', 'http://www.tw.ebay.com/', 'http://www.ebay.co.uk/', 'http://www.ebay.com/'];						   					
				   c.selCountry = 'selCountry';
				   c.chkAutodirect = 'chkAuto';
				   c.anchorCancel = 'cancel';
				   c.closeImg = 'close';					   
				   c.segment = 'A';
				   c.learnMoreText = '<b>Site Preference</b><br>eBay has a local site for you. You can set #1# as your site preference and be automatically directed to it instead of #2#. To cancel the redirect, simply click the \""Cancel site preference\"" link on #1#.';
				   c.learnMore = 'learnMore';			
				   c.msgRemind = 'Been to #1#?';
				   c.targetSiteName = '#1#';
				   c.currentSiteName = '#2#';			  	   				   
				   c.msgBackTo = '<a href=""#"" name=""msgBackToLink"" class=""navigation"">Back to #1#.</a>';
				   c.msgRedirected = '<span class=""navigation"">You have been redirected to #2#.</span><a href=""#"" name=""msgRedirectedLink"" class=""navigation"">Cancel this preference.</a>';
				   c.msgVisitSite = '<a href=""#"" name=""msgVisitSiteLink""  class=""navigation"">Visit #1#.</a>';
				   c.redirectHTML = '<table width=""750px"" cellpadding=""0"" cellspacing=""0"" border=""0"" bgcolor=""#FFFFE5""><tr><td style=""border-left: 1px solid #F9B709;""><img src=""http://pics.ebaystatic.com/aw/pics/s.gif"" alt="""" width=""8"" height=""8""/></td><td width=""60%""><img src=""http://pics.ebaystatic.com/aw/pics/s.gif"" alt="""" width=""1"" height=""1""/></td><td width=""40%""><img src=""http://pics.ebaystatic.com/aw/pics/s.gif"" alt="""" width=""1"" height=""1""/></td><td style=""border-right: 1px solid #F9B709;""><img src=""http://pics.ebaystatic.com/aw/pics/s.gif"" alt="""" width=""8"" height=""8""/></td></tr><tr><td style=""border-left: 1px solid #F9B709;""><img src=""http://pics.ebaystatic.com/aw/pics/s.gif"" alt="""" width=""8"" height=""1""/></td><td><span id=""msgBackTo"">&#160;</span><span id=""msgRedirected"">&#160;</span></td><td align=""right""><span id=""msgVisitSite"">&#160;</span></td><td style=""border-right: 1px solid #F9B709;""><img src=""http://pics.ebaystatic.com/aw/pics/s.gif"" alt="""" width=""8"" height=""1""/></td></tr><tr><td bgcolor=""#FFFFFF""><img src=""http://pics.ebaystatic.com/aw/pics/userSitePrefs/corner1BottomLeft_20x20.gif"" alt="""" border=""0""/></td><td style=""border-bottom: 1px solid #F9B709;""><img src=""http://pics.ebaystatic.com/aw/pics/s.gif"" alt="""" width=""1"" height=""8""/></td><td style=""border-bottom: 1px solid #F9B709;""><img src=""http://pics.ebaystatic.com/aw/pics/s.gif"" alt="""" width=""1"" height=""8""/></td><td bgcolor=""#FFFFFF""><img src=""http://pics.ebaystatic.com/aw/pics/userSitePrefs/corner1BottomRight_20x20.gif"" alt="""" border=""0""/></td></tr></table>';				   
				   c.flHTML = '<table cellpadding=""0"" cellspacing=""0"" width=""400px"" border=""0""><tr><td><img src=""http://pics.ebaystatic.com/aw/pics/userSitePrefs/cornerTopLeft_20x20.gif""></td><td colspan=""3"" class=""topbrdr""><img src=""http://pics.ebaystatic.com/aw/pics/s.gif""  height=""7""></td><td class=""topbrdr"" rowspan=""2""><a href=""#"" name=""close""><img src=""http://pics.ebaystatic.com/aw/pics/icon/iconClose_20x20.gif""  border=""0""></a></td><td><img src=""http://pics.ebaystatic.com/aw/pics/userSitePrefs/cornerTopRight_20x20.gif""></td></tr><tr bgcolor=""#FFFFE5""><td colspan=""4"" class=""lftbrdr""><img src=""http://pics.ebaystatic.com/aw/pics/s.gif""  height=""10""></td><td class=""rt1brdr""><img src=""http://pics.ebaystatic.com/aw/pics/s.gif""></td></tr><tr bgcolor=""#FFFFE5""><td colspan=""5"" class=""lft1brdr""><img src=""http://pics.ebaystatic.com/aw/pics/s.gif""  height=""7""></td><td class=""rtbrdr""><img src=""http://pics.ebaystatic.com/aw/pics/s.gif""></td></tr><tr bgcolor=""#FFFFE5""><td class=""lft1brdr""><img src=""http://pics.ebaystatic.com/aw/pics/s.gif""></td><td valign=""top""><img src=""http://pics.ebaystatic.com/aw/pics/userSitePrefs/globe_26x27.gif""></td><td><img src=""http://pics.ebaystatic.com/aw/pics/s.gif""  width=""7""></td><td valign=""middle""><div id=""headerInfo""><b><span id=""msgRemind"">&#160;</span></b><br>Discover the advantages of your local site!</div><br/></td><td><img src=""http://pics.ebaystatic.com/aw/pics/s.gif""></td><td class=""rtbrdr""><img src=""http://pics.ebaystatic.com/aw/pics/s.gif""></td></tr><tr bgcolor=""#FFFFE5""><td class=""lft1brdr""><img src=""http://pics.ebaystatic.com/aw/pics/s.gif""></td><td valign=""top""><img src=""http://pics.ebaystatic.com/aw/pics/s.gif""></td><td><img src=""http://pics.ebaystatic.com/aw/pics/s.gif"" width=""7""></td><td>Where do you want to go?<br><input type=""radio"" name=""selCountry"" value=""R""> <span id=""targetSiteName"">#1</span><br><input type=""radio"" name=""selCountry"" value=""N"" checked> <span id=""currentSiteName"">#2</span> <br><table width=""100%"" cellpadding=""0"" cellspacing=""0"" ><tr><td colspan=""3""><img src=""http://pics.ebaystatic.com/aw/pics/s.gif"" height=""10""></td></tr><tr><td valign=""top""><input type=""checkbox"" name=""chkAuto""></td><td>&#160;</td><td><img src=""http://pics.ebaystatic.com/aw/pics/s.gif""  height=""3""><br>Always automatically direct me to the selected eBay site. <span  id=""learnMoreLink""><a href=""#"" name=""learnMore"">Learn more</a></span></td></tr></table><br><div style=""background:#D4D0C8;padding-top:3px;width:75px;padding-bottom:2px;padding-left:12px;padding-right:12px;border-bottom-color:#00FF;border-bottom-style:solid;border-bottom-width:1px;border-right-color:#808080;border-right-style:solid;border-right-width:1px;""><a href=""#"" name=""continue"" style=""width:100%; text-align:center;text-decoration:none;color:#000000;cursor:default;"">Continue</a></div></td><td><img src=""http://pics.ebaystatic.com/aw/pics/s.gif""></td><td class=""rtbrdr""><img src=""http://pics.ebaystatic.com/aw/pics/s.gif""></td></tr><tr bgcolor=""#FFFFE5""><td colspan=""5"" class=""lft1brdr""><img src=""http://pics.ebaystatic.com/aw/pics/s.gif"" height=""20""></td><td class=""rtbrdr""><img src=""http://pics.ebaystatic.com/aw/pics/s.gif""></td></tr><tr  bgcolor=""#FFFFE5""><td class=""lft1brdr""><img src=""http://pics.ebaystatic.com/aw/pics/s.gif""></td><td colspan=""2""><img src=""http://pics.ebaystatic.com/aw/pics/s.gif""></td><td><a href=""#"" name=""cancel"" class=""navigation"">Cancel and don\'t ask me again on this site.</a></td><td><img src=""http://pics.ebaystatic.com/aw/pics/s.gif""></td><td class=""rtbrdr""><img src=""http://pics.ebaystatic.com/aw/pics/s.gif""></td></tr><tr><td><img src=""http://pics.ebaystatic.com/aw/pics/userSitePrefs/cornerBottomLeft_20x20.gif""></td><td colspan=""4"" class=""btmbrdr""<img src=""http://pics.ebaystatic.com/aw/pics/s.gif""></td><td><img src=""http://pics.ebaystatic.com/aw/pics/userSitePrefs/cornerBottomRight_20x20.gif""></td></tr></table>';		  
				}
				ebCreateConfigurations();
			//--></script><iframe name=""rtm"" id=""rtm"" height=""1"" width=""1"" frameborder=""0"" scrolling=""no""></iframe><script src=""http://include.ebaystatic.com/js/e479/us/homepagebody_e4795us.js"" type=""text/javascript""></script><script language=""JavaScript"" type=""text/javascript"">
			ebay.oDocument.oPage.createConfig = function()
			{
			var c = new EbayRichMediaHomepageConfig();
			c.bRMHomepage = false; 
			c.bRMAllowed = false;  
			c.sFlashLinkText = 'View Flash Version'; 
			c.sFlashTextId = 'sFlashTextId';
			c.bNotifyOnload = false; 
			return c;
			}
			ebay.oDocument._getControl('rmhp').init(ebay.oDocument.oPage.createConfig());
		</script><script language=""JavaScript"" type=""text/javascript"">
					   // RTM configuration.
					   var c = ebay.oDocument.addConfig(new EbayConfig(""RTMEngine""));  
					   c.srtmEngineHost = 'http://srx.main.ebayrtm.com/rtm?RtmCmd&a=inline';			   
					   c.aPids = [1,29];
					   //$URL.RTMSRV to be defined	       	 
					    c.sIfrName = ""rtm"";
						ebay.oDocument._getControl(""rtm"")._exec(""loadPlacements"",c);	
					 </script><table border=""0"" cellpadding=""0"" cellspacing=""0"" width=""760""><tr><td><img src=""hp/imgHPTag.jpg"" border=""0"" height=""43"" width=""760""></td></tr><tr><td><table border=""0"" cellpadding=""0"" cellspacing=""0"" width=""760""><tr><td><table border=""0"" cellpadding=""0"" cellspacing=""0""><form action=""http://search.ebay.com/search/search.dll"" method=""GET"" name=""searchform"" onsubmit=""setPopOutSwitch(false); return handleSubmit(this, 'http://search.ebay.com/');""><tr bgcolor=""#efefef""><td colspan=""7""><img src=""s.gif"" width=""1"" height=""5""></td></tr><tr bgcolor=""#efefef""><td align=""left"" rowspan=""2""><img src=""s.gif"" width=""8"" height=""1""></td><td nowrap height=""10"" valign=""middle""><input type=""hidden"" name=""cgiurl"" value=""http://cgi.ebay.com/ws/""><input type=""hidden"" name=""fkr"" value=""1""><input type=""hidden"" name=""from"" value=""R8""><input type=""text"" size=""40"" maxlength=""300"" value="""" hspace=""0"" name=""satitle"">&nbsp;<select name=""category0"" size=""1""><option value="""">All Categories</option><option value=""20081"">Antiques</option><option value=""550"">Art</option><option value=""2984"">Baby</option><option value=""267"">Books</option><option value=""12576"">Business &amp; Industrial</option><option value=""625"">Cameras &amp; Photo</option><option value=""6000"">Cars, Boats, Vehicles &amp; Parts</option><option value=""15032"">Cell Phones</option><option value=""11450"">Clothing, Shoes &amp; Accessories</option><option value=""11116"">Coins &amp; Paper Money</option><option value=""1"">Collectibles</option><option value=""58058"">Computers &amp; Networking</option><option value=""293"">Consumer Electronics</option><option value=""14339"">Crafts</option><option value=""237"">Dolls &amp; Bears</option><option value=""11232"">DVDs &amp; Movies</option><option value=""45100"">Entertainment Memorabilia</option><option value=""31411"">Gift Certificates</option><option value=""26395"">Health &amp; Beauty</option><option value=""11700"">Home &amp; Garden</option><option value=""281"">Jewelry &amp; Watches</option><option value=""11233"">Music</option><option value=""619"">Musical Instruments</option><option value=""870"">Pottery &amp; Glass</option><option value=""10542"">Real Estate</option><option value=""316"">Specialty Services</option><option value=""382"">Sporting Goods</option><option value=""64482"">Sports Mem, Cards &amp; Fan Shop</option><option value=""260"">Stamps</option><option value=""1305"">Tickets</option><option value=""220"">Toys &amp; Hobbies</option><option value=""3252"">Travel</option><option value=""1249"">Video Games</option><option value=""99"">Everything Else</option></select>&nbsp;<input type=""submit"" border=""0"" value=""Search""></td><td width=""10"">&nbsp;</td><td align=""left"" width=""50%"" valign=""middle""><font size=""-2""><a href=""http://search.ebay.com/ws/search/AdvSearch?sofindtype=13"">Advanced Search</a><br></font></td><td width=""10"">&nbsp;</td></tr><tr bgcolor=""#efefef""><td nowrap colspan=""8""></td></tr></form></table></td><td bgcolor=""#EFEFEF""><img src=""s.gif"" width=""15"" height=""8""><br><a href=""http://us.ebayobjects.com/2c;27244798;12515765;s"" target=""livehelpwin"" onclick=""return popupWindow(this.href,this.target,472,320,'no','no');""><img src=""home//imgLiveHelp_trans.gif"" alt=""Live Help"" border=""0"" height=""17"" width=""79""></a></td><td bgcolor=""#EFEFEF""><img src=""s.gif"" width=""15"" height=""1""></td></tr></table></td></tr><tr bgcolor=""#efefef""><td colspan=""7""><img src=""s.gif"" width=""1"" height=""5""></td></tr><tr><td colspan=""7""><img src=""s.gif"" width=""1"" height=""5""></td></tr><tr><td><table border=""0"" cellpadding=""0"" cellspacing=""0"" width=""760""><tr><td valign=""top"" align=""left"" width=""200""><table border=""0"" cellpadding=""0"" cellspacing=""0"" width=""100%""><tr><td width=""1"" rowspan=""6"" bgcolor=""#FFCC00""><img src=""s.gif"" width=""1"" height=""1""></td><td width=""1"" bgcolor=""#FFCC00""><img src=""s.gif"" width=""1"" height=""1""></td><td width=""1"" bgcolor=""#FFCC00""><img src=""s.gif"" width=""16"" height=""1""></td><td width=""1"" rowspan=""6"" bgcolor=""#FFCC00""><img src=""s.gif"" width=""1"" height=""1""></td></tr><tr><td bgcolor=""#FFCC00"" width=""16"" height=""25"" valign=""middle"" align=""center"" background=""""><img src=""tbx/squares.gif"" border=""0""></td><td bgcolor=""#FFCC00"" height=""25"" valign=""middle"" background=""""><font color=""#000000""><font size=""2""><b>Specialty Sites</b><br></font></font></td></tr><tr><td bgcolor=""#FFFFCC"" background=""""><img src=""s.gif"" width=""1"" height=""4""></td><td bgcolor=""#FFFFCC"" background=""""><img src=""s.gif"" width=""159"" border=""0"" height=""4""></td></tr><tr><td bgcolor=""#FFFFCC"" background="""">&nbsp;</td><td bgcolor=""#FFFFCC"" background="""" height="""">
    <b><a href=""http://www.express.ebay.com/"">eBay Express</a>&nbsp;<img src=""new.gif""></b><br>
	<b><a href=""http://www.motors.ebay.com/"">eBay Motors</a></b><br>
	<b><a href=""http://stores.ebay.com/"">eBay Stores</a></b><br>
	<b><a href=""http://www.ebaybusiness.com"">eBay Business</a></b><br>
	<b><a href=""http://www.half.ebay.com/"">Half.com</a></b><br>	
	<b><a href=""http://www.rent.com/"">Apartments on Rent.com</a></b><br>
</td></tr><tr><td bgcolor=""#FFFFCC"" background="""" colspan=""2""><img src=""s.gif"" width=""1"" border=""0"" height=""4""></td></tr><tr><td bgcolor=""#FFCC00"" background="""" colspan=""2""><img src=""s.gif"" width=""1"" height=""1""></td></tr><tr><td colspan=""4""><img src=""s.gif"" width=""10"" height=""5""></td></tr></table><table border=""0"" cellpadding=""0"" cellspacing=""0"" width=""100%""><tr><td width=""1"" rowspan=""7"" bgcolor=""#FFCC00""><img src=""s.gif"" width=""1"" height=""1""></td><td width=""1"" bgcolor=""#FFCC00""><img src=""s.gif"" width=""1"" height=""1""></td><td width=""1"" bgcolor=""#FFCC00""><img src=""s.gif"" width=""16"" height=""1""></td><td width=""1"" rowspan=""7"" bgcolor=""#FFCC00""><img src=""s.gif"" width=""1"" height=""1""></td></tr><tr><td bgcolor=""#FFCC00"" width=""16"" height=""25"" valign=""middle"" align=""center"" background=""""><img src=""tbx/squares.gif"" border=""0""></td><td bgcolor=""#FFCC00"" height=""25"" valign=""middle"" background=""""><font color=""#000000""><font size=""2""><b>Categories</b><br></font></font></td></tr><tr><td bgcolor=""#FFFFCC"" background=""""><img src=""s.gif"" width=""1"" height=""4""></td><td bgcolor=""#FFFFCC"" background=""""><img src=""s.gif"" width=""159"" border=""0"" height=""4""></td></tr><tr><td bgcolor=""#FFFFCC"" background="""">&nbsp;</td><td bgcolor=""#FFFFCC"" background="""" height="""">
	<b><a href=""http://antiques.ebay.com/"" title=""Furniture | Rugs, Carpets | Silver | More"">Antiques</a></b><br>
	<b><a href=""http://art.ebay.com/"" title=""Paintings | Posters | Prints | More"">Art</a></b><br>
	<b><a href=""http://baby.ebay.com/"" title=""Feeding | Gear | Nursery Furniture | Strollers | More"">Baby</a></b><br>
	<b><a href=""http://books.ebay.com/"" title=""Antiquarian &amp; Collectible | Children's Books | Fiction Books | Nonfiction Books | Textbooks, Education"">Books</a></b><br>
	<b><a href=""http://business.ebay.com/"" title=""Construction | Food Service &amp; Retail | Manufacturing &amp; Metalworking | Office, Printing &amp; Shipping | More"">Business &amp; Industrial</a></b><br>
	<b><a href=""http://photography.ebay.com/"" title=""Camcorders | Digital Camera Accessories | Digital Cameras | Film Cameras | Lenses &amp; Filters"">Cameras &amp; Photo</a></b><br>
	<b><a href=""http://www.motors.ebay.com/"" title=""Motorcycles | Parts &amp; Accessories | Passenger Vehicles | Powersports | Other Vehicles"">Cars, Boats, Vehicles &amp; Parts</a></b><br>
	<b><a href=""http://cell-phones.ebay.com/"" title=""Accessories, Parts | Cell Phones | Prepaid Phones &amp; Cards | More"">Cell Phones</a></b><br>
	<b><a href=""http://clothing.ebay.com/"" title=""Boys | Girls | Handbags | Men's | Women's"">Clothing, Shoes &amp; Accessories</a></b><br>
	<b><a href=""http://coins.ebay.com/"" title=""Bullion | Coins: US | Coins: World | Paper Money | More"">Coins &amp; Paper Money</a></b><br>
	<b><a href=""http://collectibles.ebay.com/"" title=""Advertising | Casino | Comics | Decorative Collectibles | Militaria | More"">Collectibles</a></b><br>
	<b><a href=""http://computers.ebay.com/"" title=""Desktop PCs | Laptops | Monitors &amp; Projectors | Networking | Software | More"">Computers &amp; Networking</a></b><br>
	<b><a href=""http://electronics.ebay.com/"" title=""Car Electronics | Home Audio | MP3, Portable Audio | Televisions | More"">Consumer Electronics</a></b><br>
	<b><a href=""http://crafts.ebay.com/"" title=""Drawing l Painting l Scrapbooking l Sewing l Woodworking l More"">Crafts</a></b><br>
	<b><a href=""http://dolls.ebay.com/"" title=""Bears | Dollhouse Miniatures | Dolls | More"">Dolls &amp; Bears</a></b><br>
	<b><a href=""http://dvd.ebay.com/"" title=""DVD | Film | Laserdisc | VHS | Other Formats"">DVDs &amp; Movies</a></b><br>
	<b><a href=""http://entertainment-memorabilia.ebay.com/"" title=""Autographs-Original | Movie | Music | Television | More"">Entertainment Memorabilia</a></b><br>
	<b><a href=""http://gift-certificates.ebay.com/"" title="""">Gift Certificates</a></b><br>
	<b><a href=""http://health-beauty.ebay.com/"" title=""Fragrances | Health Care | Makeup | Skin Care | More"">Health &amp; Beauty</a></b><br>
	<b><a href=""http://home.ebay.com/"" title=""Dining &amp; Bar | Furniture | Home Decor | Kitchen | Major Appliances | Pet Supplies | Tools | More"">Home &amp; Garden</a></b><br>
	<b><a href=""http://jewelry.ebay.com/"" title=""Bracelets | Earrings | Necklaces &amp; Pendants | Rings | Watches | More"">Jewelry &amp; Watches</a></b><br>
	<b><a href=""http://music.ebay.com/"" title=""Cassettes | CDs | Records | More"">Music</a></b><br>
	<b><a href=""http://instruments.ebay.com/"" title=""Guitar | Keyboard, Piano | Percussion | Pro Audio | More"">Musical Instruments</a></b><br>
	<b><a href=""http://pottery-glass.ebay.com/"" title=""Glass | Pottery &amp; China"">Pottery &amp; Glass</a></b><br>
	<b><a href=""http://realestate.ebay.com/?ssPageName=MOPS123:HRE01"" title=""Commercial | Land | Residential | Timeshares | Other Real Estate"">Real Estate</a></b><br>
	<b><a href=""http://services.ebay.com/"" title=""Custom Clothing &amp; Jewelry | Printing &amp; Personalization | Web &amp; Computer Services | More"">Specialty Services</a></b><br>
	<b><a href=""http://sports.ebay.com/"" title=""Athletic Apparel | Athletic Footwear | Golf | Cycling | Fishing | More"">Sporting Goods</a></b><br>
	<b><a href=""http://sports-cards.ebay.com/"" title=""Autographs | Fan Apparel &amp; Souvenirs | Game Used Memorabilia"">Sports Mem, Cards &amp; Fan Shop</a></b><br>
	<b><a href=""http://stamps.ebay.com/"" title=""Asia | Europe | United States | Worldwide | More"">Stamps</a></b><br>
	<b><a href=""http://tickets.ebay.com/"" title=""Event Tickets | Experiences"">Tickets</a></b><br>
	<b><a href=""http://toys.ebay.com/"" title=""Action Figures | Diecast, Toy Vehicles | Games | More"">Toys &amp; Hobbies</a></b><br>
	<b><a href=""http://pages.ebay.com/travel/index.html"" title=""Airline | Cruises | Lodging | Luggage | Vacation Packages"">Travel</a></b><br>
	<b><a href=""http://video-games.ebay.com/"" title=""Accessories | Games | Internet Games | Systems | More"">Video Games</a></b><br>
	<b><a href=""http://everythingelse.ebay.com/"" title=""Gifts | More"">Everything Else</a></b><br>
	<b><i><a href=""http://hub.ebay.com/buy"" title="""">All Categories</a>&nbsp;</i></b><br>
</td></tr><tr><td bgcolor=""#FFFFCC"" background="""">&nbsp;</td><td bgcolor=""#FFFFCC"" background="""" height=""""><font color=""#FFCC00"">&nbsp;&nbsp;------------------------</font><br>
				<b><a href=""http://pages.ebay.com/marketplace_research/"">Marketplace Research</a>&nbsp;<img src=""new.gif""></b><br>
				<b><a href=""http://pulse.ebay.com/"">eBay Pulse</a></b><br>
				<b><a href=""http://pages.ebay.com/givingworks/index.html"">Giving Works (Charity)</a></b><br>
				<b><a href=""http://www.ebayliveauctions.com/"">Live Auctions</a></b><br>
				<b><a href=""http://reviews.ebay.com"">Reviews &amp; Guides</a></b><br>
				<b><a href=""http://pages.ebay.com/wantitnow/"">Want It Now</a></b><br>
				<b><a href=""http://solutions.ebay.com/"">Solutions Directory</a></b><br>
				<b><a href=""http://pages.ebay.com/catindex/catwholesale.html"">Wholesale</a></b><br>
			</td></tr><tr><td bgcolor=""#FFFFCC"" background="""" colspan=""2""><img src=""s.gif"" width=""1"" border=""0"" height=""4""></td></tr><tr><td bgcolor=""#FFCC00"" background="""" colspan=""2""><img src=""s.gif"" width=""1"" height=""1""></td></tr><tr><td colspan=""4""><img src=""s.gif"" width=""10"" height=""5""></td></tr></table><table border=""0"" cellpadding=""0"" cellspacing=""0"" width=""100%""><tr><td width=""1"" rowspan=""6"" bgcolor=""#FFCC00""><img src=""s.gif"" width=""1"" height=""1""></td><td width=""1"" bgcolor=""#FFCC00""><img src=""s.gif"" width=""1"" height=""1""></td><td width=""1"" bgcolor=""#FFCC00""><img src=""s.gif"" width=""16"" height=""1""></td><td width=""1"" rowspan=""6"" bgcolor=""#FFCC00""><img src=""s.gif"" width=""1"" height=""1""></td></tr><tr><td bgcolor=""#FFCC00"" width=""16"" height=""25"" valign=""middle"" align=""center"" background=""""><img src=""tbx/squares.gif"" border=""0""></td><td bgcolor=""#FFCC00"" height=""25"" valign=""middle"" background=""""><font color=""#000000""><font size=""2""><b>More eBay Sites</b><br></font></font></td></tr><tr><td bgcolor=""#FFFFCC"" background=""""><img src=""s.gif"" width=""1"" height=""4""></td><td bgcolor=""#FFFFCC"" background=""""><img src=""s.gif"" width=""159"" border=""0"" height=""4""></td></tr><tr><td bgcolor=""#FFFFCC"" background="""">&nbsp;</td><td bgcolor=""#FFFFCC"" background="""" height="""">
	<b><a href=""http://us.ebayobjects.com/2c;9739597;9123118;z?https://www.paypal.com/ebay"">PayPal</a></b><br>
	<b><a href=""http://adfarm.mediaplex.com/ad/ck/5662-31553-2357-0"">ProStores</a></b><br>
	<b><a href=""http://www.shopping.com/"">Shopping.com</a>&nbsp;<img src=""new.gif""></b><br>
	<b><a href=""http://pages.ebay.com/skype/"">Skype</a>&nbsp;<img src=""new.gif""></b><br>
</td></tr><tr><td bgcolor=""#FFFFCC"" background="""" colspan=""2""><img src=""s.gif"" width=""1"" border=""0"" height=""4""></td></tr><tr><td bgcolor=""#FFCC00"" background="""" colspan=""2""><img src=""s.gif"" width=""1"" height=""1""></td></tr><tr><td colspan=""4""><img src=""s.gif"" width=""10"" height=""5""></td></tr></table><table border=""0"" cellpadding=""0"" cellspacing=""0"" width=""100%""><tr><td width=""1"" rowspan=""6"" bgcolor=""#FFCC00""><img src=""s.gif"" width=""1"" height=""1""></td><td width=""1"" bgcolor=""#FFCC00""><img src=""s.gif"" width=""1"" height=""1""></td><td width=""1"" bgcolor=""#FFCC00""><img src=""s.gif"" width=""16"" height=""1""></td><td width=""1"" rowspan=""6"" bgcolor=""#FFCC00""><img src=""s.gif"" width=""1"" height=""1""></td></tr><tr><td bgcolor=""#FFCC00"" width=""16"" height=""25"" valign=""middle"" align=""center"" background=""""><img src=""tbx/squares.gif"" border=""0""></td><td bgcolor=""#FFCC00"" height=""25"" valign=""middle"" background=""""><font color=""#000000""><font size=""2""><b>Global Sites</b><br></font></font></td></tr><tr><td bgcolor=""#FFFFCC"" background=""""><img src=""s.gif"" width=""1"" height=""4""></td><td bgcolor=""#FFFFCC"" background=""""><img src=""s.gif"" width=""159"" border=""0"" height=""4""></td></tr><tr><td bgcolor=""#FFFFCC"" background=""""><img src=""s.gif"" width=""1"" height=""1"" border=""0""></td><td bgcolor=""#FFFFCC"" background="""" height="""" valign=""top"">
				Shop around the world
				<form name=""gs"" style=""border: 0;margin-top: 3;margin-bottom: 3;""><font face=""arial narrow"" size=""1""><select name=""globalsites""><option value="""">Select One...</option><option value=""http://www.mercadolibre.com.ar/"">Argentina</option><option value=""http://www.ebay.com.au/"">Australia</option><option value=""http://www.ebay.at/"">Austria</option><option value=""http://www.ebay.be/"">Belgium</option><option value=""http://www.mercadolivre.com.br/"">Brazil</option><option value=""http://www.ebay.ca/"">Canada</option><option value=""http://www.ebay.com.cn/"">China</option><option value=""http://www.ebay.fr/"">France</option><option value=""http://www.ebay.de/"">Germany</option><option value=""http://www.ebay.com.hk/"">Hong Kong</option><option value=""http://www.ebay.in/"">India</option><option value=""http://www.ebay.ie/"">Ireland</option><option value=""http://www.ebay.it/"">Italy</option><option value=""http://www.auction.co.kr/default.html"">Korea</option><option value=""http://www.ebay.com.my/"">Malaysia</option><option value=""http://www.mercadolibre.com.mx/"">Mexico</option><option value=""http://www.ebay.nl/"">Netherlands</option><option value=""http://www.ebay.com/nz/"">New Zealand</option><option value=""http://www.ebay.ph/"">Philippines</option><option value=""http://www.ebay.pl/"">Poland</option><option value=""http://www.ebay.com.sg/"">Singapore</option><option value=""http://www.ebay.es/"">Spain</option><option value=""http://www.ebay.se/"">Sweden</option><option value=""http://www.ebay.ch/"">Switzerland</option><option value=""http://www.tw.ebay.com/"">Taiwan</option><option value=""http://www.ebay.co.uk/"">United Kingdom</option></select>  <input type=""button"" value=""Go"" onclick=""if(this.form.globalsites.selectedIndex!=0)location = this.form.globalsites.options[this.form.globalsites.selectedIndex].value + '?redir=0';""></font></form>
				Learn how to <a href=""http://offer.ebay.com/ws/eBayISAPI.dll?GlobalTradeHub&amp;hubType=0"">buy</a> or <br><a href=""http://offer.ebay.com/ws/eBayISAPI.dll?GlobalTradeHub&amp;hubType=1"">sell</a> Globally
				</td></tr><tr><td bgcolor=""#FFFFCC"" background="""" colspan=""2""><img src=""s.gif"" width=""1"" border=""0"" height=""4""></td></tr><tr><td bgcolor=""#FFCC00"" background="""" colspan=""2""><img src=""s.gif"" width=""1"" height=""1""></td></tr><tr><td colspan=""4""><img src=""s.gif"" width=""10"" height=""5""></td></tr></table></td><td width=""5""><img src=""s.gif"" width=""5"" height=""1""></td><td height=""100%"" valign=""top"" align=""center"" width=""275""><script language=""javascript"" type=""text/javascript""><!--
			
				if (typeof(writeHomepageAd) != ""undefined"")
					writeHomepageAd(""homepage.visitor"", ""visitor"", 1, 275, 300, 275, 300, ""!cat=staticHP"");
			//--></script><noscript><a href=""http://us.ebayobjects.com/3j/ebay.us.homepage.visitor/visitor;tile=1;sz=275x300;ord=123456789??""><img src=""http://us.ebayobjects.com/1a/ebay.us.homepage.visitor/visitor;tile=1;sz=275x300;ord=123456789??"" width=""275"" height=""300"" border=""0"" alt=""""></a></noscript><img src=""s.gif"" width=""275"" height=""10""><script>

					function makeFlash(alternate){
						if ( ebay.oGlobals.oClient.activeXLibLoaded(""ShockwaveFlash.ShockwaveFlash.4"") )
						 {
						             ebay.oDocument.oPage.createConfig = function()
                                    {
                                                var c = new EbayFlashModuleConfig();
                                                c.sExecuteOn = ""inline"";
                                                c.sName = ""SkypeBannerUk.swf"";
												c.iWidth = 275;
                                                c.iHeight = 35;
												c.sSWF = ""http://pics.ebaystatic.com/aw/pics/mops/swf/Skype_SG_ticker_URL_275x35.swf"";
											return c;
                                    }
                                    ebay.oDocument._getControl( ""flash"" ).write( ebay.oDocument.oPage.createConfig() );
							} 
						else {
								document.write(alternate);
						}
					}
					alternate = '<a href=""http://us.ebayobjects.com/2c;44445264;12593038;h?http://www.skype.com/skypeU""><img src=""http://pics.ebaystatic.com/aw/pics/mops/other/Skype_SG_ticker_275x35.gif"" alt=""Skype"" border=""0"" width="""" height=""35""></a>';
								makeFlash(alternate);
	
				</script><img src=""s.gif"" width=""1"" height=""5""><table border=""0"" cellpadding=""0"" cellspacing=""0"" width=""100%""><tr><td width=""1"" rowspan=""6"" bgcolor=""#FFCC00""><img src=""s.gif"" width=""1"" height=""1""></td><td width=""1"" bgcolor=""#FFCC00""><img src=""s.gif"" width=""1"" height=""1""></td><td width=""1"" bgcolor=""#FFCC00""><img src=""s.gif"" width=""16"" height=""1""></td><td width=""1"" rowspan=""6"" bgcolor=""#FFCC00""><img src=""s.gif"" width=""1"" height=""1""></td></tr><tr><td colspan=""2"" bgcolor=""#FFCC00"" height=""2""><img src=""s.gif"" width=""1"" height=""2""></td></tr><tr><td bgcolor=""ffffff"" background=""""><img src=""s.gif"" width=""1"" height=""4""></td><td bgcolor=""ffffff"" background=""""><img src=""s.gif"" width=""159"" border=""0"" height=""4""></td></tr><tr><td align=""center"" valign=""middle"" bgcolor=""ffffff"" colspan=""2""><script language=""javascript"" type=""text/javascript""><!--
				ebay.oDocument.oPage.createConfig = function()
				{
				var c = getStandardAdTableConfig('homePage','homepage.visitor',['visitor'],2,['100x100','100x100'],'h',2,0,240,220);
				c.addParam('!cat', 'staticHP');
				c.addParam('tw','240')
				c.addParam('ta','center')
				return c;
				}
				writeAdTable(ebay.oDocument.oPage.createConfig());
			//--></script><noscript><a href=""http://us.ebayobjects.com/3j/ebay.us.homepage.visitor/visitor;tile=1;sz=100x100;ord=123456789??""><img src=""http://us.ebayobjects.com/1a/ebay.us.homepage.visitor/visitor;tile=1;sz=100x100;ord=123456789??"" width=""100"" height=""100"" border=""0"" alt=""""></a><a href=""http://us.ebayobjects.com/3j/ebay.us.homepage.visitor/visitor;tile=2;sz=100x100;ord=123456789??""><img src=""http://us.ebayobjects.com/1a/ebay.us.homepage.visitor/visitor;tile=2;sz=100x100;ord=123456789??"" width=""100"" height=""100"" border=""0"" alt=""""></a><br><a href=""http://us.ebayobjects.com/3j/ebay.us.homepage.visitor/visitor;tile=3;sz=100x100;ord=123456789??""><img src=""http://us.ebayobjects.com/1a/ebay.us.homepage.visitor/visitor;tile=3;sz=100x100;ord=123456789??"" width=""100"" height=""100"" border=""0"" alt=""""></a><a href=""http://us.ebayobjects.com/3j/ebay.us.homepage.visitor/visitor;tile=4;sz=100x100;ord=123456789??""><img src=""http://us.ebayobjects.com/1a/ebay.us.homepage.visitor/visitor;tile=4;sz=100x100;ord=123456789??"" width=""100"" height=""100"" border=""0"" alt=""""></a><br></noscript></td></tr><tr><td bgcolor=""ffffff"" background="""" colspan=""2""><img src=""s.gif"" width=""1"" border=""0"" height=""4""></td></tr><tr><td bgcolor=""#FFCC00"" background="""" colspan=""2""><img src=""s.gif"" width=""1"" height=""1""></td></tr><tr><td colspan=""4""><img src=""s.gif"" width=""10"" height=""5""></td></tr></table><div id=""featuredItemsSection""><table border=""0"" cellpadding=""0"" cellspacing=""0"" width=""100%""><tr><td width=""1"" rowspan=""17"" bgcolor=""#FFCC00""><img src=""s.gif"" width=""1"" height=""1""></td><td width=""1"" bgcolor=""#FFCC00""><img src=""s.gif"" width=""1"" height=""1""></td><td width=""1"" bgcolor=""#FFCC00""><img src=""s.gif"" width=""16"" height=""1""></td><td width=""1"" rowspan=""17"" bgcolor=""#FFCC00""><img src=""s.gif"" width=""1"" height=""1""></td></tr><tr><td bgcolor=""#FFCC00"" width=""16"" height=""25"" valign=""middle"" align=""center"" background=""""><img src=""tbx/squares.gif"" border=""0""></td><td bgcolor=""#FFCC00"" height=""25"" valign=""middle"" background=""""><font color=""#000000""><table border=""0"" cellpadding=""0"" cellspacing=""0"" width=""100%""><tr><td><font color=""#000000"" size=""2""><b>Featured Items</b><br></font></td><td align=""right""><font size=""-2""><a href=""http://pages.ebay.com/help/sell/hpf.html"">Learn how</a></font>&nbsp;</td></tr></table></font></td></tr><tr><td bgcolor=""#FFFFFF"" background=""""><img src=""s.gif"" width=""1"" height=""4""></td><td bgcolor=""#FFFFFF"" background=""""><img src=""s.gif"" width=""159"" border=""0"" height=""4""></td></tr><tr><td bgcolor=""#FFFFFF"" background=""""><img src=""s.gif"" width=""1"" height=""1"" border=""0""></td><td bgcolor=""#FFFFFF"" background="""" height="""" valign=""top""><!--unmatched element:objversion--></td></tr><tr bgcolor=""#FFFFFF""><td><img src=""homepage/Star.gif"" width=""11"" height=""14"" hspace=""2"" vspace=""3"" align=""middle""></td><td><span style=""width:250px; overflow: hidden'""><a href=""http://cgi.ebay.com/ws/eBayISAPI.dll?ViewItem&amp;Item=220027557735&amp;Category=12605"">Custom Home for Sale Running Y  Res...</a><br></span></td></tr><tr bgcolor=""#FFFFFF""><td><img src=""homepage/Star.gif"" width=""11"" height=""14"" hspace=""2"" vspace=""3"" align=""middle""></td><td><span style=""width:250px; overflow: hidden'""><a href=""http://cgi.ebay.com/ws/eBayISAPI.dll?ViewItem&amp;Item=290028552313&amp;Category=12605""> Historic Home For Sale Utica, New ...</a><br></span></td></tr><tr bgcolor=""#FFFFFF""><td><img src=""homepage/Star.gif"" width=""11"" height=""14"" hspace=""2"" vspace=""3"" align=""middle""></td><td><span style=""width:250px; overflow: hidden'""><a href=""http://cgi.ebay.com/ws/eBayISAPI.dll?ViewItem&amp;Item=200031057210&amp;Category=2518"">VTECH  HUGE LOT  * V. SMILE +14 GAM...</a><br></span></td></tr><tr bgcolor=""#FFFFFF""><td><img src=""homepage/Star.gif"" width=""11"" height=""14"" hspace=""2"" vspace=""3"" align=""middle""></td><td><span style=""width:250px; overflow: hidden'""><a href=""http://cgi.ebay.com/ws/eBayISAPI.dll?ViewItem&amp;Item=190027323690&amp;Category=15841"">166.5 Acres of Land Near Pilot Mout...</a><br></span></td></tr><tr bgcolor=""#FFFFFF""><td><img src=""homepage/Star.gif"" width=""11"" height=""14"" hspace=""2"" vspace=""3"" align=""middle""></td><td><span style=""width:250px; overflow: hidden'""><a href=""http://cgi.ebay.com/ws/eBayISAPI.dll?ViewItem&amp;Item=170033557355&amp;Category=31387"">ROGER DUBUIS SYMPATHIE CHRONOGRAPH ...</a><br></span></td></tr><tr bgcolor=""#FFFFFF""><td><img src=""homepage/Star.gif"" width=""11"" height=""14"" hspace=""2"" vspace=""3"" align=""middle""></td><td><span style=""width:250px; overflow: hidden'""><a href=""http://cgi.ebay.com/ws/eBayISAPI.dll?ViewItem&amp;Item=270032255472&amp;Category=38196"">World #1 Dominic Gerard®  French Ki...</a><br></span></td></tr><tr bgcolor=""#FFFFFF""><td colspan=""2"" height=""1""></td></tr><tr bgcolor=""#FFFFFF""><td colspan=""2"" height=""1""></td></tr><tr bgcolor=""#FFFFFF""><td colspan=""2"" height=""1""></td></tr><tr bgcolor=""#FFFFFF""><td colspan=""2"" height=""1""></td></tr><tr><td bgcolor=""#FFFFFF"" background=""""><img src=""s.gif"" width=""1"" height=""1"" border=""0""></td><td bgcolor=""#FFFFFF"" background="""" height="""" valign=""top""><i><a href=""http://listings.ebay.com/aw/listings/list/featured/index.html?ssPageName=MOPS123:allfeatureditems"">See all featured items...</a>&nbsp;</i></td></tr><tr><td bgcolor=""#FFFFFF"" background="""" colspan=""2""><img src=""s.gif"" width=""1"" border=""0"" height=""4""></td></tr><tr><td bgcolor=""#FFCC00"" background="""" colspan=""2""><img src=""s.gif"" width=""1"" height=""1""></td></tr><tr><td colspan=""4""><img src=""s.gif"" width=""10"" height=""5""></td></tr></table></div></td><td width=""5""><img src=""s.gif"" width=""5"" height=""1""></td><td valign=""top"" align=""right""><table border=""0"" cellpadding=""0"" cellspacing=""0"" width=""100%""><tr><td><a href=""http://us.ebayobjects.com/2c;47586106;12593038;l?http://www.express.ebay.com""><img src=""http://pics.ebaystatic.com/aw/pics/mops/other/PM-RelationshipMktg-eBayExpress_WinWin_Q306-Win5000-275x73.gif"" alt="""" border=""0"" height=""73"" width=""275""></a></td></tr><tr><td><img src=""s.gif"" width=""1"" height=""5""></td></tr><tr><td><a href=""https://scgi.ebay.com/ws/eBayISAPI.dll?RegisterEnterInfo&amp;siteid=0&amp;co_partnerid=2&amp;usage=0&amp;ru=default&amp;rafId=0&amp;encRafId=default""><img src=""hp/imgHPRegTest_v2_275x73.gif"" alt=""Register Now"" border=""0"" height=""73"" width=""275""></a><br><img src=""s.gif"" height=""5""></td></tr><tr><td><script language=""javascript"" type=""text/javascript""><!--
				writeMerchandiseProducts('hpA');
			//--></script></td></tr><tr><td><table border=""0"" cellpadding=""0"" cellspacing=""0"" width=""100%""><tr><td width=""1"" rowspan=""6"" bgcolor=""#7D81D7""><img src=""s.gif"" width=""1"" height=""1""></td><td width=""1"" bgcolor=""#7D81D7""><img src=""s.gif"" width=""1"" height=""1""></td><td width=""1"" bgcolor=""#7D81D7""><img src=""s.gif"" width=""16"" height=""1""></td><td width=""1"" rowspan=""6"" bgcolor=""#7D81D7""><img src=""s.gif"" width=""1"" height=""1""></td></tr><tr><td bgcolor=""#CCCCFF"" width=""16"" height=""25"" valign=""middle"" align=""center"" background=""""><img src=""tbx/squares.gif"" border=""0""></td><td bgcolor=""#CCCCFF"" height=""25"" valign=""middle"" background=""""><font color=""#000000""><font size=""2""><b>Helpful Links</b><br></font></font></td></tr><tr><td bgcolor=""#FFFFFF"" background=""""><img src=""s.gif"" width=""1"" height=""4""></td><td bgcolor=""#FFFFFF"" background=""""><img src=""s.gif"" width=""159"" border=""0"" height=""4""></td></tr><tr><td bgcolor=""#FFFFFF"" background=""""><img src=""s.gif"" width=""1"" height=""1"" border=""0""></td><td bgcolor=""#FFFFFF"" background="""" height="""" valign=""top""><a href=""http://pages.ebay.com/education/"">Learning Center</a><br><a href=""http://pages.ebay.com/paypal/buyer/protection.html"">PayPal Buyer Protection</a><br><a href=""http://pages.ebay.com/ebay_toolbar/"">eBay Toolbar</a><br></td></tr><tr><td bgcolor=""#FFFFFF"" background="""" colspan=""2""><img src=""s.gif"" width=""1"" border=""0"" height=""4""></td></tr><tr><td bgcolor=""#7D81D7"" background="""" colspan=""2""><img src=""s.gif"" width=""1"" height=""1""></td></tr><tr><td colspan=""4""><img src=""s.gif"" width=""10"" height=""5""></td></tr></table></td></tr></table></td></tr></table></td></tr><tr><td><script language=""JavaScript""><!-- //
				ebay.oDocument.oPage.createConfig = function()
				{
					var c = this.oDocument.addConfig(new EbayConfig(""Common.MyFavorites""));
					c.sContentUrl = ""http://my.ebay.com/ws/eBayISAPI.dll?MfcISAPICommand=getFavoriteNav"";
					c.sOrientation = ""0"";
					this._getControl(""myFavorites"").writeContainer(c);
				}
				ebay.oDocument.oPage.createConfig();
			//--></script></td></tr><tr><td><img src=""s.gif"" width=""1"" height=""10""></td></tr><tr><td height=""1"" width=""100%"" bgcolor=""#FFCC00""><img src=""s.gif"" width=""1"" height=""2""></td></tr><tr><td><img src=""s.gif"" width=""1"" height=""10""></td></tr><script language=""javascript"" type=""text/javascript""><!--
					var prop25 = 'VisitorR1';
				var writeLL = 1;var ssIsHP = 1;var pageName = 'eBay Home Page';var server = location.hostname;
			server.toLowerCase();
			//--></script><tr><td><script language=""javascript"" type=""text/javascript""><!--
				ebay.oDocument.oPage.createConfig = function()
				{
				var c = getStandardAdTableConfig('homePage','homepage.visitor',['visitor'],3,['234x60','234x60','234x60'],'h',1,0,760,70);
				c.addParam('!cat', 'staticHP');
				c.addParam('tw','760')
				c.addParam('ta','center')
				return c;
				}
				writeAdTable(ebay.oDocument.oPage.createConfig());
			//--></script><noscript><a href=""http://us.ebayobjects.com/3j/ebay.us.homepage.visitor/visitor;tile=1;sz=234x60;ord=123456789??""><img src=""http://us.ebayobjects.com/1a/ebay.us.homepage.visitor/visitor;tile=1;sz=234x60;ord=123456789??"" width=""234"" height=""60"" border=""0"" alt=""""></a><a href=""http://us.ebayobjects.com/3j/ebay.us.homepage.visitor/visitor;tile=2;sz=234x60;ord=123456789??""><img src=""http://us.ebayobjects.com/1a/ebay.us.homepage.visitor/visitor;tile=2;sz=234x60;ord=123456789??"" width=""234"" height=""60"" border=""0"" alt=""""></a><a href=""http://us.ebayobjects.com/3j/ebay.us.homepage.visitor/visitor;tile=3;sz=234x60;ord=123456789??""><img src=""http://us.ebayobjects.com/1a/ebay.us.homepage.visitor/visitor;tile=3;sz=234x60;ord=123456789??"" width=""234"" height=""60"" border=""0"" alt=""""></a><br></noscript></td></tr><tr><td class=""pipe""><a href=""http://www.mercadolibre.com.ar/"">Argentina</a>&nbsp;| <a href=""http://www.ebay.com.au/"">Australia</a>&nbsp;| <a href=""http://www.ebay.at/"">Austria</a>&nbsp;| <a href=""http://www.ebay.be/"">Belgium</a>&nbsp;| <a href=""http://www.mercadolivre.com.br/"">Brazil</a>&nbsp;| <a href=""http://www.ebay.ca/"">Canada</a>&nbsp;| <a href=""http://www.ebay.com.cn/"">China</a>&nbsp;| <a href=""http://www.ebay.fr/"">France</a>&nbsp;| <a href=""http://www.ebay.de/"">Germany</a>&nbsp;| <a href=""http://www.ebay.com.hk/"">Hong Kong</a>&nbsp;| <a href=""http://www.ebay.in/"">India</a>&nbsp;| <a href=""http://www.ebay.ie/"">Ireland</a>&nbsp;| <a href=""http://www.ebay.it/"">Italy</a>&nbsp;| <a href=""http://www.auction.co.kr/default.html"">Korea</a>&nbsp;| <a href=""http://www.ebay.com.my/"">Malaysia</a>&nbsp;| <a href=""http://www.mercadolibre.com.mx/"">Mexico</a>&nbsp;| <a href=""http://www.ebay.nl/"">Netherlands</a>&nbsp;| <a href=""http://www.ebay.com/nz/"">New Zealand</a>&nbsp;| <a href=""http://www.ebay.ph/"">Philippines</a>&nbsp;| <a href=""http://www.ebay.pl/"">Poland</a>&nbsp;| <a href=""http://www.ebay.com.sg/"">Singapore</a>&nbsp;| <a href=""http://www.ebay.es/"">Spain</a>&nbsp;| <a href=""http://www.ebay.se/"">Sweden</a>&nbsp;| <a href=""http://www.ebay.ch/"">Switzerland</a>&nbsp;| <a href=""http://www.tw.ebay.com/"">Taiwan</a>&nbsp;| <a href=""http://www.ebay.co.uk/"">United Kingdom</a></td></tr><tr><td><img src=""s.gif"" width=""1"" height=""10""></td></tr><tr><td class=""pipe""><b><a href=""http://pages.ebay.com/services/forum/feedback.html?ssPageName=home:f:f:US"">Feedback Forum</a></b>&nbsp;| <b><a href=""http://pages.ebay.com/ebay_toolbar/index.html"">eBay Toolbar</a></b>&nbsp;| <b><a href=""http://pages.ebay.com/download/?ssPageName=home:f:f:US"">Downloads</a></b>&nbsp;| <b><a href=""https://certificates.ebay.com"">Gift Certificates</a></b>&nbsp;| <b><a href=""https://www.paypal.com/"">PayPal</a></b>&nbsp;| <b><a href=""http://www.ebaycareers.com/"">Jobs</a></b>&nbsp;| <b><a href=""http://affiliates.ebay.com/"">Affiliates</a></b>&nbsp;| <b><a href=""http://developer.ebay.com/DevProgram/?ssPageName=home:f:f:US"">Developers</a></b>&nbsp;| <b><a href=""http://www.theebayshop.com/"">The eBay Shop</a></b></td></tr><tr><td><img src=""s.gif"" width=""1"" height=""10""></td></tr></table><script type=""text/javascript"">
				ebay.oUtils.oPrefetch.load(""http://include.ebaystatic.com/js/e479/us/features/search/results2body_e4795us.js"");
			</script><script type=""text/javascript"" language=""JavaScript"">
				ebay.oDocument.oPage.createConfig = function() {var  c = ebay.oDocument.addConfig(new EbayConfig('EBX.CrossLinking'));c.sLayer='ebx_layer';c.sHTML='<table align=""center"" style=""border:2px solid #0098CF; margin-bottom: 15px;"" width=""85%"" cellpadding=""0"" cellspacing=""0"" bgcolor=""#EDF9FF""><tr valign=""middle""><td width=""36"" style=""padding: 0 10px 0 10px;""><img align=""middle"" src=""icon/iconInfo_16x16.gif"" /></td><td width=""80%"" nowrap=""nowrap"" style=""padding-right:10px"" >You\'ve left eBay Express.</td><td align=""left"" nowrap=""nowrap""><a href=""http://www.express.<#1#>/"">Shop again on eBay Express</a><span style=""margin:0 10px 0 10px;"">|</span><img align=""bottom"" src=""express/icons/iconCart_15x10.gif"" alt=""""/> <a href=""http://cart.express.<#1#>/ws/eBayISAPI.dll?ExpressCart&action.view=&from=Header"">Shopping Cart<#2#></a></td><td width=""135"" nowrap=""nowrap"" style=""padding: 0 20px 0 20px;""><img align=""top"" src=""express/logos/logoExpress_95x39.gif"" alt=""""></img></td><td nowrap=""nowrap"" width=""26"" align=""left""><a href=""#1"" id=""b_close"" name=""b_close""><img align=""middle"" border=""0"" src=""buttons/btnExpressClose.gif"" alt="""" ></img></a></td></tr></table>';c.sCartCountText=' (<#1#>)';c.sClose='b_close';c.aHost={'0':'ebay.com','77':'ebay.de','2':'ebay.co.uk'};} ;ebay.oDocument.oPage.createConfig();</script><table border=""0"" cellpadding=""0"" cellspacing=""0"" width=""760""><tr><td align=""left""><a href=""http://pages.ebay.com/community/aboutebay/"">About eBay</a>&nbsp;|&nbsp;<a href=""http://www2.ebay.com/aw/marketing.shtml?ssPageName=home:f:f:US"">Announcements</a>&nbsp;|&nbsp;<a href=""http://us.ebayobjects.com/2c;23768387;9122844;v?http://pages.ebay.com/education/?ssPageName=home:f:f:US"">Learning Center</a>&nbsp;|&nbsp;<a href=""http://pages.ebay.com/securitycenter/?ssPageName=home:f:f:US"">Security &amp; Resolution Center</a>&nbsp;|&nbsp;<a href=""http://pages.ebay.com/help/policies/hub.html?ssPageName=home:f:f:US"">Policies</a>&nbsp;|&nbsp;<a href=""http://www.ebay.com/governmentrelations"">Government Relations</a>&nbsp;|&nbsp;<a href=""http://pages.ebay.com/sitemap.html"">Site Map</a>&nbsp;|&nbsp;<a href=""http://pages.ebay.com/help/?ssPageName=home:f:f:US"">Help</a></td></tr><tr><td height=""4""><img src=""s.gif"" width=""1"" height=""5""></td></tr><tr><td bgcolor=""#CCCCCC"" height=""1""><img src=""s.gif"" width=""1"" height=""1""></td></tr><tr><td valign=""top"" align=""left""><table border=""0"" cellpadding=""0"" cellspacing=""0"" width=""100%""><tr class=""help""><td valign=""top"" width=""100%"" class=""navigation"">Copyright  © 1995-2008 eBay Inc. All Rights Reserved.Designated trademarks and brands are the property of their respective owners.Use of this Web site constitutes acceptance of the eBay <a href=""http://pages.ebay.com/help/community/png-user.html"">User Agreement</a> and <a href=""http://pages.ebay.com/help/community/png-priv.html"">Privacy Policy</a>.
		</td><td valign=""top""></td></tr></table></td></tr><tr><td><img src=""s.gif"" width=""1"" height=""10""></td></tr></table><table border=""0"" cellpadding=""0"" cellspacing=""0"" width=""760""><tr><td><div id=""cobrandFooter""> </div><script src=""http://include.ebaystatic.com/js/e479/us/ebayfooter_e4795us.js"" type=""text/javascript""></script></td></tr></table><table border=""0"" cellpadding=""0"" cellspacing=""0"" width=""760""><tr><td align=""left"" class=""navigation""><a href=""http://cgi1.ebay.com/aw-cgi/eBayISAPI.dll?TimeShow&amp;ssPageName=home:f:f:US"">eBay official time</a> 
		 - Page last updated: 
	&nbsp;Sep-28-06 17:59:01 PDT</td></tr></table><img src=""http://us.ebayobjects.com/1a/ebay.us.rmtestinga/default;sz=1x1;ord=123456789?"" width=""1"" height=""1"" border=""0"" alt=""""><script language=""javascript"" type=""text/javascript""><!--
	document.write('<SCRIPT LANGUAGE=""JavaScript"" SRC=""http://us.ebayobjects.com/1aj/ebay.us.homepage.layer/layer;dcopt=ist;sz=1x1;ord=' + (new Date()).getTime() + '?""><\/SCRIPT>');
				
		//--></script></body></html>";

            XmlDocument doc;

            doc = HtmlParser.Parse(source);
            Assert.IsNotNull(doc["root"]["html"]["head"]);
            Assert.IsNotNull(doc["root"]["html"]["body"]);
        }

        [TestMethod]
        [TestProperty("Lib", "LillTek.Advanced")]
        public void HtmlParser_Parse_MSN()
        {
            // Generate a DOM source scraped from www.msn.com and 
            // make sure we don't see an exception.

            const string source =
@"
<!DOCTYPE html PUBLIC ""-//W3C//DTD XHTML 1.0 Strict//EN"" ""http://www.w3.org/TR/xhtml1/DTD/xhtml1-strict.dtd""><html xml:lang=""en-us"" lang=""en-us"" xmlns=""http://www.w3.org/1999/xhtml""><head><meta http-equiv=""Content-Type"" content=""text/html; charset=utf-8"" /><meta http-equiv=""pics-label"" content=""(pics-1.1 &quot;http://www.icra.org/ratingsv02.html&quot; l gen true for &quot;http://www.msn.com&quot; r (cz 1 lz 1 nz 1 oz 1 vz 1) gen true for &quot;http://msn.com&quot; r (cz 1 lz 1 nz 1 oz 1 vz 1) gen true for &quot;http://stb.msn.com&quot; r (cz 1 lz 1 nz 1 oz 1 vz 1)  gen true for &quot;http://stc.msn.com&quot; r (cz 1 lz 1 nz 1 oz 1 vz 1) gen true for &quot;http://stj.msn.com&quot; r (cz 1 lz 1 nz 1 oz 1 vz 1) &quot;http://www.rsac.org/ratingsv01.html&quot; l gen true for &quot;http://www.msn.com&quot; r (n 0 s 0 v 0 l 0)gen true for &quot;http://msn.com&quot; r (n 0 s 0 v 0 l 0)gen true for &quot;http://stb.msn.com&quot; r (n 0 s 0 v 0 l 0) gen true for &quot;http://stc.msn.com&quot; r (n 0 s 0 v 0 l 0) gen true for &quot;http://stj.msn.com&quot; r (n 0 s 0 v 0 l 0))"" /><link rel=""SHORTCUT ICON"" href=""http://hp.msn.com/global/c/hpv10/favicon.ico"" type=""image/x-icon"" /><title>MSN.com</title><style type=""text/css"">@import url(""http://stc.msn.com/br/hp/en-us/css/2/ushp.css"");@import url(""http://stc.msn.com/br/hp/en-us/css/2/override.css"");</style><!--[if IE]><style type=""text/css"">@import url(""http://stc.msn.com/br/hp/en-us/css/2/ie.css"");</style><![endif]--><!--[if lt IE 6]><style type=""text/css"">@import url(""http://stc.msn.com/br/hp/en-us/css/2/ie5.css"");</style><![endif]--><script type=""text/javascript"" src=""http://st.msn.com/br/hp/en-us/js/2/hptg.js""></script><script type=""text/javascript"" src=""http://ads1.msn.com/library/dap.js""></script><link type=""text/css"" rel=""stylesheet"" id=""csslink"" href=""http://stc.msn.com/br/hp/en-us/css/2/blu.css""  /><script type=""text/javascript"" >Msn.HP.Theme.Set({id:""csslink"",base:""http://stc.msn.com/br/"",themes:[{I:""blu"",T:""Blue theme"",U:""hp/en-us/css/2/blu.css"",B:""#1a76b7""},{I:""whi"",T:""White theme"",U:""hp/en-us/css/2/whi.css"",B:""#edf4ed""},{I:""gre"",T:""Green theme"",U:""hp/en-us/css/2/gre.css"",B:""#66a72d""},{I:""ora"",T:""Orange theme"",U:""hp/en-us/css/2/ora.css"",B:""#d97125""},{I:""red"",T:""Red theme"",U:""hp/en-us/css/2/red.css"",B:""#b33232""}]})</script><script type=""text/javascript"">var ppstatus='False',hpurl='http://www.msn.com/',s_account='msnportalhome',s_hook=true;Msn.HP.Settings.SetCookieDomain("".msn.com"")</script></head><body><div id=""omni""><img id=""ctag"" width=""1"" height=""1"" alt="""" src=""http://c.msn.com/c.gif?di=340&amp;pi=7317&amp;ps=83527&amp;tp=http://www.msn.com/&amp;rf="" /><script type=""text/javascript"" src=""http://stj.msn.com/br/om/js/1/s_code.js""></script><script type=""text/javascript"">Msn.Tracking.Setup({P:{pageName:""US Homepage V10.5"",server:""msn.com"",prop1:""Portal"",prop2:""en-us"",prop3:""10.5"",prop22:(typeof ppstatus=='string' ? ppstatus : 'False'),prop19:Msn.HP.Settings.GetPageState(),channel:""www.msn.com""}})</script><script type=""text/javascript"">Msn.Tracking.Setup({wait:2000})</script></div><div id=""wrapper"" class=""page6 region9""><div id=""head""><div id=""peelback"" class=""parent chrome6 single1""><div class=""child c1 first""><div class=""advertisement""><script type=""text/javascript"">dap(""&amp;PG=MSN9TP&amp;AP=1376"",1,1);</script></div></div></div><div id=""promo"" class=""parent chrome1 triple1 cf""><div class=""child c1 first""><div class=""advertisement"" id=""HeaderTextAd""><script type=""text/javascript"">dap(""&amp;PG=MSN9UT&amp;AP=1339"",1,1)</script></div></div><div class=""child c2""><div class=""link"" ><a href=""http://encarta.msn.com/encnet/features/onthisday.aspx"">Thursday, September 28, 2006</a></div></div><div class=""child c3 last""><div class=""link""><a href=""http://clk.atdmt.com/MSN/go/msnnkwme0040000002msn/direct/01/?href=http://get.live.com/messenger/overview  "">Download the NEW Messenger!</a></div></div></div><div id=""header"" class=""parent chrome1 quad0""><div id=""msnlogo"" class=""child c1 first""><div class=""imgMSFT"" title=""MSN.com""></div></div><div class=""child c2""><ul class=""linklist21 cf""><li class=""first""><a href=""/?lang=es-us"">Español</a></li><li class=""last""><a href=""http://rss.msn.com"">RSS</a></li></ul></div><br class=""b3"" /><div class=""child c3""><ul class=""linklist9 cf""><li class=""first selected""><a href=""http://search.msn.com/results.aspx?FORM=MSNH&amp;mkt=en-US&amp;q="">Web</a></li><li><a href=""http://search.msn.com/images/results.aspx?FORM=MSNH&amp;mkt=en-US&amp;q="">Images</a></li><li><a href=""http://search.msn.com/news/results.aspx?FORM=MSNH&amp;mkt=en-US&amp;q="">News</a></li><li><a href=""http://search.msn.com/local/results.aspx?FORM=MSNH&amp;mkt=en-US&amp;q="">Local</a></li><li><a href=""http://shopping.msn.com/results/shp/?FORM=MSNH&amp;text="">Shopping</a></li><li class=""last""><a href=""http://qna.live.com/search.aspx?FORM=MSNH&amp;amp;scope=web&amp;mkt=en-US&amp;q="" id=""beta"">QnA</a></li></ul></div><div class=""child c4 last""><form action=""http://search.msn.com/results.aspx"" method=""get"" class=""simple6"" id=""srchfrm""><p>Search the Web</p><div><label for=""f1"">Search:</label><input id=""f1"" type=""text"" name=""q"" size=""75"" maxlength=""250"" accesskey=""S"" title=""MSN Search""></input><input type=""hidden"" name=""FORM"" value=""MSNH"" /><input type=""submit"" class=""button"" value=""Search Web"" /></div></form></div></div><div id=""toolbar"" class=""parent chrome6 quad2 cf""><div id=""welcome"" class=""child c1 first""><div class=""abs""><span>Welcome</span></div></div><div id=""msgcount"" class=""child c2""></div><br class=""b3"" /><div id=""signin"" class=""child c3""><a href=""http://login.live.com/login.srf?wa=wsignin1.0&amp;rpsnv=10&amp;ct=1159494784&amp;rver=4.0.1531.0&amp;wp=LBI&amp;wreply=http:%2F%2Fwww.msn.com%2F&amp;lc=1033&amp;id=1184"" class=""dMSNME_1"">SIGN IN</a></div><div id=""theme"" class=""child c4 last""></div></div></div><div id=""page""><div id=""nav""><div class=""parent chrome1 double1 cf""><div class=""child c1 first""><ul class=""linkedimglinklist1 cf""><li class=""first""><a href=""http://hotmail.msn.com""><img src=""http://stb.msn.com/i/F6/528E19AA57C59BD28F9241C1469F1.gif"" width=""25"" height=""20"" alt=""Envelope"" /><span><strong>Hotmail</strong></span></a></li><li><a href=""http://get.live.com/messenger/overview""><img src=""http://stb.msn.com/i/E3/6C1DD5E3386DEAE0954D8340F25A.gif"" width=""25"" height=""20"" alt=""Two people figures"" /><span><strong>Messenger</strong></span></a></li><li><a href=""http://my.msn.com""><img src=""http://stb.msn.com/i/48/6CDE404B4BFEC334D023E5422081E0.gif"" width=""25"" height=""20"" alt=""Figure of person in front of computer monitor"" /><span><strong>My MSN</strong></span></a></li><li class=""last""><a href=""http://specials.msn.com/alphabet.aspx""><img src=""http://stb.msn.com/i/78/7CE57843948D6DF13E79A2DE4E15C.gif"" width=""25"" height=""20"" alt=""Figure of arrow pointing to screen"" /><span><strong>MSN Directory</strong></span></a></li></ul></div><div class=""child c2""><div class=""cols cnt5""><ul class=""linklist1""><li class=""first""><a href=""http://autos.msn.com/default.aspx"">Autos</a></li><li><a href=""http://careers.msn.com"">Careers &amp; Jobs</a></li><li><a href=""http://cityguides.msn.com"">City Guides</a></li><li><a href=""http://expo.live.com"">Classifieds</a></li><li class=""last""><a href=""http://msn.match.com/index.aspx?TrackingID=516163&amp;BannerID=543351"">Dating &amp; Personals</a></li></ul><ul class=""linklist1""><li class=""first""><a href=""http://games.msn.com"">Games</a></li><li><a href=""http://health.msn.com"">Health &amp; Fitness</a></li><li><a href=""http://astrocenter.astrology.msn.com/msn/DeptHoroscope.aspx?When=0&amp;Af=-1000&amp;VS"">Horoscopes</a></li><li><a href=""http://lifestyle.msn.com/BridgePage.aspx"">Lifestyle</a></li><li class=""last""><a href=""http://mappoint.msn.com"">Maps &amp; Directions</a></li></ul><ul class=""linklist1""><li class=""first""><a href=""http://moneycentral.msn.com/home.asp"">Money</a></li><li><a href=""http://movies.msn.com"">Movies</a></li><li><a href=""http://music.msn.com"">Music</a></li><li><a href=""http://msnbc.com"">News</a></li><li class=""last""><a href=""http://realestate.msn.com"">Real Estate</a></li></ul><ul class=""linklist1""><li class=""first""><a href=""http://g.msn.com/0AD00036/931292.1??HCType=1&amp;CID=931292&amp;PG=SHPHDR"">Shopping</a></li><li><a href=""http://slate.msn.com"">Slate Magazine</a></li><li><a href=""http://spaces.msn.com"">Spaces</a></li><li><a href=""http://msn.foxsports.com/?FSO1&amp;ATT=HTN"">Sports</a></li><li class=""last""><a href=""http://tech.msn.com"">Tech &amp; Gadgets</a></li></ul><ul class=""linklist1""><li class=""first""><a href=""http://travel.msn.com/default.aspx"">Travel</a></li><li><a href=""http://tv.msn.com"">TV</a></li><li><a href=""http://weather.msn.com"">Weather</a></li><li><a href=""http://www.whitepages.com/5050"">White Pages</a></li><li class=""last""><a href=""http://yellowpages.msn.com"">Yellow Pages</a></li></ul></div></div></div></div><div id=""content""><div id=""subhead""></div><div id=""area1"" class=""region6""><div id=""infopane"" class=""parent chrome7 single0""><div class=""child c1 first deck""><div id=""slide1"" class=""slide first""><div class=""linkedimg""><a href=""http://msn.foxsports.com/mlb/story/6003556?FSO1&amp;ATT=HCP&amp;GT1=8595""><img src=""http://stb.msn.com/i/46/D05BFAA0EB582B713A6E554B3076.jpg"" width=""365"" height=""170"" alt=""MLB Winners &amp; Losers // Sean Casey &amp; Brandon Inge slapping hands, Josh Beckett talks to catcher Doug Mirabelli (©Jessica Rinaldi, Adam Hunger/Reuters) "" /></a></div></div><div id=""slide2"" class=""slide""><div class=""linkedimg""><a href=""http://shopping.msn.com/content/shp/?ctId=956,ptnrid=164,ptnrdata=301320&amp;GT1=8590""><img src=""http://stb.msn.com/i/A7/417ED0427138AD2AF92A79B4419D.jpg"" width=""365"" height=""170"" alt=""Fall Home Décor Trends // Graphic bedding  (© MSN Shopping)"" /></a></div></div><div id=""slide3"" class=""slide""><div class=""photolistset""><div class=""photo"" id=""photohalf3""><a href=""http://msn.careerbuilder.com/custom/msn/careeradvice/viewarticle.aspx?articleid=834&amp;SiteId=cbmsnhp4834&amp;sc_extcmp=JS_834_home1&amp;cbRecursionCnt=1&amp;cbsid=63e04bf3dc88497b9f6bc2098ccfebb5-212583341-W0-2&amp;GT1=8522""><img src=""http://stb.msn.com/i/8A/799AFE18498DFCD264427055E02.jpg"" width=""213"" height=""170"" alt=""Where the Jobs Are // Businessman reading the newspaper classifieds (© Stockbyte/Getty Images)"" /></a></div><div class=""list"" id=""listhalf3""><h4>MORE ON MSN</h4><ul class=""linklist16""><li class=""first""><a href=""http://tech.msn.com/products/article.aspx?cp-documentid=954060&amp;GT1=8596"">Starbucks in space?</a></li><li><a href=""http://men.msn.com/articlees.aspx?cp-documentid=808240&amp;GT1=8572"">How the world takes time off men's lives</a></li><li><a href=""http://encarta.msn.com/quiz_214/movie_quotes_quiz_ii.html?GT1=8506  "">Movie quotes quiz</a></li><li><a href=""http://msn.match.com/msn/article.aspx?articleid=6673&amp;TrackingID=516311&amp;BannerID=544657&amp;menuid=7&amp;GT1=8535"">10 tips for your first post-divorce date</a></li><li class=""last""><a href=""http://health.msn.com/centers/sleep/default.aspx?GT1=8565"">Insomnia? Better sleep through diet</a></li></ul></div></div></div></div></div><div id=""today"" class=""parent chrome1 double1 cf""><div class=""child c1 first""><h3>Today's Picks</h3><ul class=""linklist16""><li class=""first""><a href=""http://movies.msn.com/movies/article.aspx?news=235889&amp;GT1=7701 "">Mystery author behind gritty JT LeRoy books is revealed</a></li><li><a href=""http://www.msnbc.msn.com/id/6356101/?GT1=8506"">Slide show: Animal Tracks</a></li><li class=""last""><a href=""http://msn.foxsports.com/cfb/story/5746512?FSO1&amp;ATT=HCP&amp;GT1=8595 "">Heisman Trophy watch</a></li></ul></div><div class=""child c2 last""><div class=""imglinkabs1 cf""><a href=""http://articles.moneycentral.msn.com/Investing/SuperModels/HowMuchPatienceDoesHPDeserve.aspx?GT1=8506 ""><img src=""http://stb.msn.com/i/C6/2BD225EF8C6AF1A75C88E1CE7DC777.jpg"" width=""70"" height=""70"" alt=""Former Chairwoman Patricia Dunn during the Hewlett-Packard data privacy hearing (© Kevin Lamarque/Reuters) "" /></a><a href=""http://articles.moneycentral.msn.com/Investing/SuperModels/HowMuchPatienceDoesHPDeserve.aspx?GT1=8506 ""><strong>Enron-esque?</strong></a><p><a href=""http://articles.moneycentral.msn.com/Investing/SuperModels/HowMuchPatienceDoesHPDeserve.aspx?GT1=8506 "">Analysis: H-P scandal keeps getting worse </a></p></div></div></div><div id=""spotlight"" class=""parent chrome7 single1 cf""><div class=""child c1 first""><div class=""flashlinkedimg"" id=""spotlightflash""><a href=""http://discoverspaces.live.com/?source=spotlight&amp;loc=us""><img src=""http://stb.msn.com/i/CF/2A93409E95DF9A19996CED8A3CF662.jpg"" width=""363"" height=""155"" alt=""Spaces: Learn More."" /></a><script type=""text/javascript"">Msn.Flash.Build('http://stb.msn.com/i/E1/8E525A60513038F4C93E26EBEBD4D.swf', '6', 363, 155, 'spotlightflash');</script></div></div></div><div id=""alsoonmsn"" class=""parent chrome5 single1 cf""><h2><a href=""http://special.msn.com/directory.armx"">Also on MSN</a></h2><div class=""child c1 first""><div class=""imglistset1 cf""><div class=""linkedimg""><a href=""http://cheftotherescue.msn.com/?GT1=8604""><img src=""http://stb.msn.com/i/2A/71897FD62FFF7055CC9FAB85DB318E.jpg"" width=""70"" height=""70"" alt=""Cat Cora prepares baked 'fried' chicken (© Kraft Kitchens)"" /></a></div><ul class=""linklist16""><li class=""first""><a href=""http://cheftotherescue.msn.com/?GT1=8604""><strong>Video: Kraft's 'fried' chicken recipe</strong></a></li><li><a href=""http://whatsyourstory.msn.com/?src=spaceshome&amp;id=2&amp;GT1=8534"">Blog: An athlete copes with paralysis</a></li><li><a href=""http://dixiechicks.msn.com/article.aspx?cp-documentid=999298&amp;GT1=8524 "">Critic on Dixie Chicks documentary</a></li><li><a href=""http://obey.msn.com/default.aspx?id=1&amp;GT1=8531"">Send a secret shout-out in a Sprite ad</a></li><li class=""last""><a href=""http://exposure.msn.com/?id=4003&amp;GT1=8534"">Video: Is graffiti gaining acceptance?</a></li></ul></div></div></div><div id=""video"" class=""parent chrome5 single1 cf""><h2><a href=""http://video.msn.com/?f=msnhome"">Video Highlights</a></h2><div class=""child c1 first""><div class=""imglistset1 cf""><div class=""linkedimg""><a href=""http://video.msn.com/v/us/v.htm?g=B34059AF-C944-403C-A4C2-449C7030E264&amp;t=&amp;f=01/64&amp;p=&amp;GT1=8506""><img src=""http://stb.msn.com/i/C4/5D50840B8301FB9D4D4E807539A9.jpg"" width=""70"" height=""70"" alt=""Paris Hilton in 'Nothing In This World' music video (© Warner Bros. Records)"" /></a></div><ul class=""linklist16""><li class=""first""><a href=""http://video.msn.com/v/us/v.htm?g=B34059AF-C944-403C-A4C2-449C7030E264&amp;t=&amp;f=01/64&amp;p=&amp;GT1=8506"">Paris: 'Nothing In This World'</a></li><li><a href=""http://video.msn.com/v/us/v.htm?g=EFDCCF7B-8802-4B3B-BAFA-BD0D27D8DC6D&amp;t=m17&amp;f=06/64&amp;p=Source_Today%20show%20health&amp;GT1=8506"">Good eats for runners </a></li><li><a href=""http://video.msn.com/v/us/v.htm?g=3BE436CA-A950-4A0E-B9FD-C21F584B5B8C&amp;t=m17&amp;f=06/64&amp;p=Source_Today%20show%20fashion&amp;GT1=8506"">Best bargain pants for women</a></li><li><a href=""http://video.msn.com/v/us/v.htm?g=9E76AFB1-7166-49FA-8CC8-7EA562081336&amp;t=c150&amp;f=06/64&amp;p=Source_Scarborough_Country&amp;GT1=8506"">The Jon Stewart factor &amp; politics</a></li><li class=""last""><a href=""http://video.msn.com/v/us/v.htm?g=5EB8A135-A7FB-461D-86DC-AB14ED2B114B,409E38BA-C12C-474B-B674-7255455CB48F,A7091430-8654-494A-8A64-6A1FA4CDDD02,FB94B14B-F6DD-4D19-AC0F-F8D1034FB579&amp;t=m17&amp;f=06/64&amp;p=hotvideo_m_edpicks&amp;GT1=8506"">Is there a special diet for the brain?</a></li></ul></div></div></div><div id=""entertain"" class=""parent chrome5 double2""><h2><a href=""http://entertainment.msn.com/"">Entertainment</a></h2><div class=""child c1 first""><div class=""imglistset1 cf""><div class=""linkedimg""><a href=""http://movies.msn.com?GT1=7701 ""><img src=""http://stb.msn.com/i/17/65491CF948F0DEF744766525E38CC.jpg"" width=""75"" height=""75"" alt=""Ashton Kutcher &amp; Kevin Costner in 'The Guardian' (© Touchstone Pictures)  "" /></a></div><ul class=""linklist16""><li class=""first""><a href=""http://movies.msn.com?GT1=7701 "">Does 'The Guardian' sink or swim?</a></li><li><a href=""http://tv.msn.com/tv/article.aspx?news=235953&amp;GT1=7703 "">Smith believes cause of son's death</a></li><li><a href=""http://music.msn.com/music/celebsbuzz?GT1=7702 "">Buzz: Spears, Aguilera end feud</a></li><li><a href=""http://tv.msn.com/tv/article.aspx?news=235770&amp;GT1=7703 "">Chevy Chase does 'Law &amp; Order'</a></li><li class=""last""><a href=""http://movies.msn.com/news/article.aspx?news=235946&amp;GT1=7701"">De Niro sucked into trans fats flap</a></li></ul></div></div><div class=""child c2 last""><form action=""http://movies.msn.com/search/movie/"" method=""get"" class=""simple1""><p>Find movies, actors and actresses</p><div><label for=""s1"">Find movies, actors and actresses</label><input id=""s1"" type=""text"" name=""ss"" size=""25"" maxlength=""100"" class=""hint"" value=""Find movies, actors and actresses"" title=""MSN Entertainment""></input><input type=""submit"" class=""button"" value=""Go"" /></div></form></div></div><div id=""popsrch"" class=""parent chrome5 double1 cf""><h2><a href=""http://search.msn.com"">Popular Searches</a></h2><div class=""child c1 first""><h3>People Search</h3><ul class=""linklist1""><li class=""first""><a href=""http://search.msn.com/news/results.aspx?q=Emily+Keyes&amp;mkt=en-US&amp;form=MSNHM1 "">Emily Keyes</a></li><li><a href=""http://search.msn.com/news/results.aspx?q=Frank+Robinson&amp;mkt=en-US&amp;form=MSNHM1 "">Frank Robinson</a></li><li><a href=""http://search.msn.com/news/results.aspx?q=Czarina+Maria+Feodorovna&amp;mkt=en-US&amp;form=MSNHM1 "">Czarina Maria Feodorovna</a></li><li><a href=""http://search.msn.com/news/results.aspx?q=Harry+Hamlin&amp;mkt=en-US&amp;form=MSNHM1"">Harry Hamlin</a></li><li class=""last""><a href=""http://search.msn.com/news/results.aspx?q=Dustin+Diamond+&amp;mkt=en-US&amp;form=MSNHM1  "">Dustin Diamond</a></li></ul></div><div class=""child c2 last""><h3>Suggested Searches</h3><ul class=""linklist1""><li class=""first""><a href=""http://search.msn.com/news/results.aspx?q=Sargento+Foods+Inc.&amp;mkt=en-US&amp;form=MSNHM1 "">Sargento Foods Inc.</a></li><li><a href=""http://search.msn.com/news/results.aspx?q=Lockwood+Valley+fire&amp;mkt=en-US&amp;form=MSNHM1 "">Lockwood Valley fire</a></li><li><a href=""http://search.msn.com/news/results.aspx?q=Tropical+Storm+Isaac&amp;mkt=en-US&amp;form=MSNHM1 "">Tropical Storm Isaac</a></li><li><a href=""http://search.msn.com/news/results.aspx?q=Mars+rover+Opportunity&amp;mkt=en-US&amp;form=MSNHM1 "">Mars rover Opportunity</a></li><li class=""last""><a href=""http://search.msn.com/news/results.aspx?q=Hewlett-Packard+spying&amp;mkt=en-US&amp;form=MSNHM1 "">Hewlett-Packard spying</a></li></ul></div></div><div id=""acm""></div></div><div id=""area2"" class=""region4""><div id=""lgad"" class=""parent chrome6 triple2 cf""><div class=""child c1 first""><div class=""advertisement""><script type=""text/javascript"">dap(""&amp;PG=MSNREC&amp;AP=1440"",300,125)</script></div></div><div class=""child c2""><div class=""abs""><span>Advertisement</span></div></div><div id=""adfbk"" class=""child c3 last""><div class=""link""><a href=""http://www.pdcsurveys.com/cgi-bin/p.pl?P=microsoft/67b16be3.htm"">Ad feedback</a></div></div></div><div id=""news"" class=""parent chrome5 single1 cf""><h2><a href=""http://msnbc.msn.com/"">MSNBC News</a></h2><div class=""child c1 first""><div class=""imglistset1 cf""><div class=""linkedimg""><a href=""http://www.msnbc.msn.com/id/15044215/""><img src=""http://stb.msn.com/i/9A/8255BDD4DB30D22F69953A5C6A0B.jpg"" width=""70"" height=""70"" alt=""Senate OKs terror trials bill"" /></a></div><ul class=""linklist16""><li class=""first""><a href=""http://www.msnbc.msn.com/id/15044215/"">Senate OKs terror trials bill</a></li><li><a href=""http://www.msnbc.msn.com/id/15043741/"">Blaze nears California homes </a></li><li><a href=""http://www.msnbc.msn.com/id/15044435/"">Al-Qaida in Iraq admits losses</a></li><li><a href=""http://www.msnbc.msn.com/id/15041037/"">Police ID Colo. school killer</a></li><li class=""last""><a href=""http://www.msnbc.msn.com/id/15047117/"">Officer killed, manhunt continues</a></li></ul></div></div></div><div id=""sports"" class=""parent chrome5 single1 cf""><h2><a href=""http://msn.foxsports.com/?FSO1&amp;ATT=HMN"">FOX Sports</a></h2><div class=""child c1 first""><div class=""imglistset1 cf""><div class=""linkedimg""><a href=""http://msn.foxsports.com/nfl/story/6011178?FSO1&amp;ATT=HMA""><img src=""http://stb.msn.com/i/AB/82666633D8D3267452AFDC6254F9C2.jpg"" width=""70"" height=""70"" alt=""Cowboys' WR Terrell Owens (© Tim Sharp/Associated Press)"" /></a></div><ul class=""linklist16""><li class=""first""><a href=""http://msn.foxsports.com/nfl/story/6011178?FSO1&amp;ATT=HMA"">T.O. case an 'accidental overdose'</a></li><li><a href=""http://msn.foxsports.com/mlb/story/6011940?FSO1&amp;ATT=HMA"">Postseason without Pedro? Maybe</a></li><li><a href=""http://msn.foxsports.com/golf/story/6009842?FSO1&amp;ATT=HMA"">Back on his own, Tiger doing fine</a></li><li><a href=""http://msn.foxsports.com/nfl/story/6001322?FSO1&amp;ATT=HMA"">Green out for at least two more games </a></li><li class=""last""><a href=""http://msn.foxsports.com/nfl/story/6011048?FSO1&amp;ATT=HMA"">NFL considering games outside of U.S.</a></li></ul></div></div></div><div id=""stocks"" class=""parent chrome5 triple4""><h2><a href=""http://moneycentral.msn.com/investor/research/welcome.asp"">Quotes</a></h2><div class=""child c1 first""><table summary=""This table charts the key U.S. financial indices by their last reported value and change since the most recent trading day opened."" class=""stock1"" ><caption>This table charts the key U.S. financial indices by their last reported value and change since the most recent trading day opened.</caption><thead><tr><th abbr=""Index"" id=""th1"">Index</th><th class=""currency"" abbr=""Last"" id=""th2"">Last</th><th class=""currency"" abbr=""Change"" id=""th3"">Change</th></tr></thead><tbody><tr class=""first""><td headers=""th1""><a href=""http://moneycentral.msn.com/stock_quote?Symbol=$INDU"">Dow</a></td><td class=""currency"" headers=""th2"">11,718.45</td><td class=""currency"" headers=""th3""><span class=""up"">+ 29.21</span></td></tr><tr><td headers=""th1""><a href=""http://moneycentral.msn.com/stock_quote?Symbol=$COMPX"">NASDAQ</a></td><td class=""currency"" headers=""th2"">2,270.02</td><td class=""currency"" headers=""th3""><span class=""up"">+ 6.63</span></td></tr><tr class=""last""><td headers=""th1""><a href=""http://moneycentral.msn.com/stock_quote?Symbol=$INX"">S&amp;P</a></td><td class=""currency"" headers=""th2"">1,339.15</td><td class=""currency"" headers=""th3""><span class=""up"">+ 2.56</span></td></tr></tbody></table></div><div class=""child c2""><form action=""http://g.msn.com/0USHP/28"" method=""get"" class=""simple1""><p>Get quote by symbol or company name</p><div><label for=""getquote"">Get Quote</label><input id=""getquote"" type=""text"" name=""Symbol"" size=""25"" maxlength=""140"" class=""hint"" accesskey=""q"" value=""Get quote"" title=""Get Quote""></input><input type=""submit"" class=""button"" value=""Go"" /></div></form></div><div class=""child c3 last""><div class=""advertisement""><script type=""text/javascript"">dap(""&amp;PG=MSNMMT&amp;AP=1402"",1,1)</script></div></div></div><div id=""money"" class=""parent chrome5 single1 cf""><h2><a href=""http://moneycentral.msn.com/home.asp"">Money</a></h2><div class=""child c1 first""><div class=""imglistset1 cf""><div class=""linkedimg""><a href=""http://articles.moneycentral.msn.com/Investing/CNBC/Dispatch/060928markets.aspx""><img src=""http://stb.msn.com/i/40/3726722EB5747CFDA3F77DDB2F925.jpg"" width=""70"" height=""70"" alt=""Money headline &amp; graph (© Corbis) "" /></a></div><ul class=""linklist16""><li class=""first""><a href=""http://articles.moneycentral.msn.com/Investing/CNBC/Dispatch/060928markets.aspx"">Dow closes just below record</a></li><li><a href=""http://realestate.msn.com/Rentals/Articlekip.aspx?cp-documentid=534310 "">Top sins of first-time renters</a></li><li><a href=""http://moneycentral.msn.com/content/Savinganddebt/Savemoney/P33729.asp"">7 traits of millionaires</a></li><li><a href=""http://articles.moneycentral.msn.com/Investing/CNBC/TVReports/IntelLaunchesWiMaxNetworkInBrazil.aspx"">Intel takes wireless tech to Amazon</a></li><li class=""last""><a href=""http://news.moneycentral.msn.com/provider/providerarticle.asp?Feed=AP&amp;Date=20060928&amp;ID=6062540"">526,000 ThinkPad batteries recalled</a></li></ul></div></div></div><div id=""weather"" class=""parent chrome5 double2""><h2><a href=""http://weather.msn.com/"">Weather</a></h2><div class=""child c1 first""><ul class=""forecast1""><li class=""cf""><h4><a href=""http://weather.msn.com/local.aspx?wealocations=wc:USWA0245"">Lynnwood, WA</a></h4>Clear, 71&#176;<ul class=""cf""><li><h5>Thursday</h5><img src=""http://st.msn.com/as/wea3/i/en-US/saw/32.gif"" width=""35"" height=""21"" alt=""Clear"" />71&#176; / 49&#176;</li><li><h5>Friday</h5><img src=""http://st.msn.com/as/wea3/i/en-US/saw/32.gif"" width=""35"" height=""21"" alt=""Clear"" />68&#176; / 49&#176;</li><li><h5>Saturday</h5><img src=""http://st.msn.com/as/wea3/i/en-US/saw/32.gif"" width=""35"" height=""21"" alt=""Clear"" />65&#176; / 46&#176;</li><li><h5>Sunday</h5><img src=""http://st.msn.com/as/wea3/i/en-US/saw/30.gif"" width=""35"" height=""21"" alt=""Partly Cloudy"" />61&#176; / 45&#176;</li></ul></li></ul></div><div class=""child c2 last""><form action=""http://weather.msn.com/search.aspx"" method=""get"" class=""simple1""><p>Weather Info</p><div><label for=""wesdaser"">Find Weather For:</label><input id=""wesdaser"" type=""text"" name=""weasearchstr"" size=""30"" maxlength=""250"" class=""hint"" accesskey=""z"" value=""Get forecast by city or ZIP code"" title=""Get Forecast""></input><input type=""submit"" class=""button"" value=""Go"" /></div></form></div></div><div id=""shopping"" class=""parent chrome5 triple3 cf""><h2><a href=""http://g.msn.com/0AD0002G/833639.1??HCType=1&amp;CID=833639&amp;PG=SHPHDR&amp;GT1=8591 "">Shopping</a></h2><div class=""child c1 first""><span class=""linkedimglink3""><a href=""http://g.msn.com/0AD0003F/960702.1??HCType=1&amp;CID=960702&amp;PG=SHPIMG&amp;GT1=8591""><img src=""http://stb.msn.com/i/46/76F7D3EA6D8674FD0DCEF4D1980FC.JPG"" width=""50"" height=""50"" alt=""Hot gadgets on sale"" /><span><strong>Hot gadgets on sale</strong></span></a></span></div><div class=""child c2""><ul class=""linklist16""><li class=""first""><a href=""http://g.msn.com/0AD0003F/960704.1??HCType=1&amp;CID=960704&amp;PG=SHPTOP&amp;GT1=8591"">Get this season's hottest fashion styles</a></li><li><a href=""http://g.msn.com/0AD0003F/960706.1??HCType=1&amp;CID=960706&amp;PG=SHPTXT&amp;GT1=8591"">Sweet deal: iPod nano for only $188 </a></li><li><a href=""http://g.msn.com/0AD0003F/960707.1??HCType=1&amp;CID=960707&amp;PG=SHPTXT&amp;GT1=8591"">Popular jewelry under $50</a></li><li><a href=""http://g.msn.com/0AD0003F/960709.1??HCType=1&amp;CID=960709&amp;PG=SHPTXT&amp;GT1=8591"">Give your bathroom a makeover</a></li><li><a href=""http://g.msn.com/0AD0003F/960711.1??HCType=1&amp;CID=960711&amp;PG=SHPTXT&amp;GT1=8591"">Wal-Mart: Furnish your home in style</a></li><li class=""last""><a href=""http://g.msn.com/0AD0003F/960714.1??HCType=1&amp;CID=960714&amp;PG=SHPTXT&amp;GT1=8591"">Tips to get these 5 must-have beauty looks</a></li></ul></div><div class=""child c3 last""><ul class=""linklist16""><li class=""first""><strong>Special offers from our stores</strong></li><li><a href=""http://g.msn.com/0AD0003F/960717.1??HCType=1&amp;CID=960717&amp;PG=SHPSAD"">Jazz up your business with HP color printers</a></li><li class=""last""><a href=""http://g.msn.com/0AD0003F/960719.1??HCType=1&amp;CID=960719&amp;PG=SHPSAD"">AVON: Buy 1 get 1 free offer &amp; free shipping today only</a></li></ul></div></div><div id=""msnservices"" class=""parent chrome5 double1 cf""><h2><a href=""http://join.msn.com/"">MSN SERVICES</a></h2><div class=""child c1 first""><ul class=""linklist1""><li class=""first""><a href=""http://www.expedia.com/pubspec/scripts/eap.asp?GOTO=DAILY&amp;PAGE=/flights/default.asp&amp;eapid=7201-1&amp;msncid=7201-1.wd.daily.flights.inf""><strong>Air Tickets</strong></a></li><li><a href=""https://broadband.msn.com/""> High-Speed Access</a></li><li><a href=""http://clk.atdmt.com/MSN/go/msnnkwto0060000001msn/direct/01/?href=http://get.live.com/toolbar/overview"">Windows Live Toolbar</a></li><li class=""last""><a href=""http://ideas.live.com/"">Beta Services</a></li></ul></div><div class=""child c2 last""><ul class=""linklist1""><li class=""first""><a href=""http://clk.atdmt.com/MSN/go/msnnkfrh0050000001msn/direct/01/?href=http://join.msn.com/?page=hotmail/gbb&amp;pgmarket=en-us&amp;ST=1&amp;xAPID=1983&amp;DI=1402"">Email Solutions</a></li><li><a href=""http://clk.atdmt.com/MSN/go/msnnkwme0100000003msn/direct/01/?href=http://get.live.com/messenger/overview"">Windows Live Messenger</a></li><li><a href=""http://clk.atdmt.com/MSN/go/msnnknbd0040000001msn/direct/01/?href=http://join.msn.com/?page=dialup/home&amp;pgmarket=en-us&amp;ST=1&amp;xAPID=1983&amp;DI=1402"">MSN Dial-up FREE trial</a></li><li class=""last""><a href=""http://mobile.msn.com/"">MSN Mobile</a></li></ul></div></div><div class=""parent chrome6 single1""><div class=""child c1 first""><div class=""advertisement""><script type=""text/javascript"">dap(""&amp;PG=MSNSUR&amp;AP=1140"",1,1);</script></div></div></div></div><div id=""subfoot""></div></div></div><div id=""foot""><div id=""foot1"" class=""parent chrome1 single1""><div class=""child c1 first""><form action=""http://search.msn.com/results.aspx"" method=""get"" class=""simple6""><p>Search the Web</p><div><label for=""footersearch"">Search the Web</label><input id=""footersearch"" type=""text"" name=""q"" size=""40"" maxlength=""250""></input><input type=""hidden"" name=""FORM"" value=""MSNHBT"" /><input type=""submit"" class=""button"" value=""Search"" /></div></form></div></div><div class=""parent chrome6 single1""><div class=""child c1 first""><div class=""msnfoot1 cf""><ul class=""primary""><li class=""first""><a href=""http://g.msn.com/0PR_/enus"">MSN Privacy</a></li><li><a href=""http://privacy.msn.com/tou/"">Legal</a></li><li class=""last""><a href=""http://advertising.msn.com/home/home.asp"">Advertise</a></li></ul><ul class=""secondary""><li class=""first""><a href=""http://help.msn.com/en_us/frameset.asp?ini=MSN_Homepagev2.ini"">Help</a></li><li><a href=""http://ccc01.opinionlab.com/o.asp?id=PRceBdYI"">Feedback</a></li><li><a href=""http://www.msn.com/worldwide.aspx"">MSN Worldwide</a></li><li><a href=""http://members.microsoft.com/careers/search/results.aspx?FromCP=Y&amp;JobCategoryCodeID=&amp;JobLocationCodeID=&amp;JobProductCodeID=10106&amp;JobTitleCodeID=&amp;Divisions=&amp;TargetLevels=&amp;Keywords=%20&amp;JobCode=&amp;ManagerAlias=&amp;Interval=10"">Jobs</a></li><li class=""last""><a href=""http://moneycentral.msn.com/inc/Attributions.asp"">Financial Data Providers</a></li></ul><div class=""copyright""><span>© 2006 Microsoft</span></div></div></div></div></div></div><script type=""text/javascript"" src=""http://st.msn.com/br/hp/en-us/js/2/hpb.js""></script><!--[if lt IE 7]><script type=""text/javascript"" src=""http://st.msn.com/br/hp/en-us/js/2/ieminwidth.js""></script><![endif]--><script type=""text/javascript"">/*<![CDATA[*/Msn.HP.Search.bind(""#header>div.c3 ul"");Msn.HP.TextBoxHint.bind(""form input.hint"");Msn.Tracking.Form.bind(""form"");Msn.HP.Module.AddContent(""acm"",""Add Content from MSN"",""Add additional content from MSN to this page."",""parent chrome5 single1 cf"",""linklist12"",[{name:""Hotmail"",href:""http://www.hotmail.msn.com/cgi-bin/sbox?rru=hotmail"",code:""H"",id:""hmm"",bind:""Msn.HP.Hotmail"",auth:typeof ppstatus==""string""?ppstatus:""False"",delay:1,close:0},{name:""Horoscopes"",href:""http://astrocenter.astrology.msn.com/msn/DeptHoroscope.aspx?When=0&Af=-1000&VS="",code:""R"",id:""horos"",bind:""Msn.HP.Horoscope""},{name:""Games"",href:""http://zone.msn.com"",code:""G"",id:""gmod"",bind:""Msn.HP.Rss.Module"",url:""/rss/games.aspx"",opennew:1,pm:0,allhot:1},{name:""Local News"",href:""http://msnbc.msn.com/id/3098358/"",code:""L"",id:""lnmod"",bind:""Msn.HP.LocalNews"",durl:""ajax/ldsproxy.aspx?xml_url=http://www.msn.com/ajax/localnewsdata.aspx?providerid={0}"",disp:1,chan:""From {0}"",cutoff:0,pm:1,def:3,timeout:10000},{name:""City Guides"",href:""http://cityguides.msn.com"",code:""C"",id:""cgmod"",bind:""Msn.HP.CityGuide"",disp:1,chan:1,cutoff:0,pm:0},{name:""Encarta"",href:""http://encarta.msn.com"",code:""Z"",id:""encmod"",bind:""Msn.HP.Encarta"",tab:""word""}]);Msn.HP.Theme.Switch.bind(""#theme"",typeof themes==""object""?themes:{});Msn.HP.Weather.bind(""#weather"");Msn.HP.Rss.Data.bind(""#news>div.c1"",{code:""M"",url:""rss/news.aspx"",max:10,def:5});Msn.HP.Rss.Data.bind(""#sports>div.c1"",{code:""F"",url:""rss/sports.aspx"",max:10,def:5});Msn.HP.Rss.Data.bind(""#entertain>div.c1"",{code:""E"",url:""rss/msnentertainment.aspx"",max:10,def:5});Msn.HP.Rss.Data.bind(""#money>div.c1"",{code:""T"",url:""rss/msnmoney.aspx"",max:10,def:5});Msn.HP.Slideshow.bind(""#infopane>div.deck"");Msn.HP.MakeMSN.bind(""#promo .c3 .link"",{url:typeof hpurl==""string""?hpurl:""http://www.msn.com/""});Msn.HP.Restore.bind(""#welcome>div"");Msn.HP.OpenNew.bind(""#adfbk a"");Msn.HP.OpenNew.bind(""#foot ul.secondary li.first a"");Msn.HP.OpenNew.bind(""#foot ul.secondary li.first+li a"")//]]></script></body></html>
";

            XmlDocument doc;

            doc = HtmlParser.Parse(source);
            Assert.IsNotNull(doc["root"]["html"]["head"]);
            Assert.IsNotNull(doc["root"]["html"]["body"]);
        }

        [TestMethod]
        [TestProperty("Lib", "LillTek.Advanced")]
        public void HtmlParser_Parse_Adobe()
        {
            // Generate a DOM source scraped from www.adobe.com and 
            // make sure we don't see an exception.

            const string source =
@"
<?xml version=""1.0"" encoding=""UTF-8""?>
<!DOCTYPE html PUBLIC ""-//W3C//DTD XHTML 1.0 Transitional//EN"" 
	""http://www.w3.org/TR/xhtml1/DTD/xhtml1-transitional.dtd"">
<html xmlns=""http://www.w3.org/1999/xhtml"" xml:lang=""en"" lang=""en""><!-- InstanceBegin template=""/Templates/master.dwt"" codeOutsideHTMLIsLocked=""true"" -->
<head><meta http-equiv=""Content-Type"" content=""text/html; charset=UTF-8"" />
<meta http-equiv=""Content-Language"" content=""en-us"" />
<!-- Template $Revision: 2.21 $ applied. -->
<!-- InstanceParam name=""0=default,1=portal,2=home"" type=""number"" value=""2"" -->
<!-- InstanceParam name=""columns"" type=""number"" value=""3"" -->
<!-- InstanceParam name=""locale"" type=""text"" value=""en_us"" -->
<!-- InstanceParam name=""dropdown,pod,tab,tree,map"" type=""text"" value=""dropdown,pod"" -->
<!--DWT [object Object]-->
<link rel=""icon"" href=""/favicon.ico"" type=""image/x-icon"" />  
<link rel=""shortcut icon"" href=""/favicon.ico"" type=""image/x-icon"" />
<link id=""print-css"" href=""/css/master_import/print.css"" rel=""stylesheet"" rev=""stylesheet"" type=""text/css"" media=""print"" charset=""utf-8"" />
<link id=""screen-css"" href=""/css/master_import/screen.css"" rel=""stylesheet"" rev=""stylesheet"" type=""text/css"" media=""screen"" charset=""utf-8"" />
<link id=""test-css"" href=""/css/features_test.css"" rel=""stylesheet"" rev=""stylesheet"" type=""text/css"" media=""screen"" charset=""utf-8"" />
<script src=""/js/global.js"" type=""text/javascript"" charset=""utf-8""></script>
<script src=""/js/globalnav.js"" type=""text/javascript"" charset=""utf-8""></script>
<script src=""/js/dyn/ui.js"" type=""text/javascript"" charset=""utf-8""></script>
<script src=""/js/htmltemplate/util.js"" type=""text/javascript"" charset=""utf-8""></script>
<!-- InstanceBeginEditable name=""head"" -->
<title>Adobe</title>
<link id=""homepage-css"" href=""/css/layout/home.css"" rel=""stylesheet"" rev=""stylesheet"" type=""text/css"" media=""screen"" charset=""utf-8"" />
<script src=""/js/dyn/home_ui.js"" type=""text/javascript"" charset=""utf-8""></script>
<!-- InstanceEndEditable -->		
</head>
<body>
<script type=""text/javascript"">
	// <![CDATA[
	com.adobe.htmltemplate.loadCondAssets(""dropdown"",""pod"");
	// ]]>
</script>
<!--googleoff: index--><a href=""/help/accessibility.html"" tabindex=""1"" id=""accesslink"">Accessibility</a><!--googleon: index-->
<!--googleoff: index-->
<!--$Revision: 1.26.2.2 $-->
<form id=""globalnav-search"" class=""en"" name=""globalnav-search"" method=""get"" action=""/go/gnav_search"" accept-charset=""utf-8"">
	<dl id=""globalnav"" class=""compact"">
		<dt id=""adobe-logo""><a href=""/"">Adobe</a></dt>
		<dd id=""user-menu"">
			<ul class=""pipe menu compact"">
				<li id=""globalnav-cart""><span class=""cart-icon""><a href=""/go/gn_cart"" rel=""_WSS GN-Cart GNav"">Your Cart</a></span></li>
				<li><span id=""greeting"">Welcome, <span id=""screenName"">Guest</span></span></li>
				<li id=""account""><a href=""/go/gn_your_account"" rel=""_WSS GN-Account GNav"">Your Account</a></li>
				<li id=""signout""><a href=""/go/gn_signout"" rel=""_WSS GN-Signout GNav"">Sign Out</a></li>
				<li id=""help""><a href=""/go/gn_contact"" rel=""_WSS GN-Contact GNav"">Contact</a></li>
				<li id=""international""><a href=""/go/gn_intl"" rel=""_WSS GN-Geo-Selector GNav"">United States (Change)</a></li>
			</ul>
		</dd>
		<dd id=""site-menu"">
			<ul id=""site-menu-dropdown"" class=""d-dropdown pipe menu compact"">
				<li><span class=""menu-title""><a href=""/go/gn_sol"" rel=""_WSS GN-Solutions GNav"">Solutions</a></span>
					<dl class=""menu"">
						<dt>Industries</dt>
						<dd><a href=""/go/gntray_ind_education"" rel=""_WSS GN-Ind-Edu GNav"">Education</a></dd>
						<dd><a href=""/go/gntray_ind_finance"" rel=""_WSS GN-Ind-Fin GNav"">Financial Services</a></dd>
						<dd><a href=""/go/gntray_ind_govt"" rel=""_WSS GN-Ind-Gov GNav"">Government</a></dd>
						<dd><a href=""/go/gntray_ind_manufacturing"" rel=""_WSS GN-Ind-Manuf GNav"">Manufacturing</a></dd>
						<dd><a href=""/go/gntray_ind_telecommunications"" rel=""_WSS GN-Ind-Tele GNav"">Telecommunications</a></dd>
						<dt class=""divide"">Solutions</dt>
						<dd><a href=""/go/gntray_sol_digital_imaging"" rel=""_WSS GN-Sol-Imag GNav"">Digital Imaging</a></dd>
						<dd><a href=""/go/gntray_sol_mobile"" rel=""_WSS GN-Sol-Mobile GNav"">Mobile</a></dd>
						<dd><a href=""/go/gntray_sol_print_publishing"" rel=""_WSS GN-Sol-Print GNav"">Print Publishing</a></dd>
						<dd><a href=""/go/gntray_sol_process_mgmt_services"" rel=""_WSS GN-Sol-Process GNav"">Process Management Services</a></dd>
						<dd><a href=""/go/gntray_sol_ria"" rel=""_WSS GN-Sol-RIA GNav"">Rich Internet Applications</a></dd>
						<dd><a href=""/go/gntray_sol_elearning"" rel=""_WSS GN-Sol-eLearn GNav"">Training and eLearning</a></dd>
						<dd><a href=""/go/gntray_sol_video_audio"" rel=""_WSS GN-Sol-Motion GNav"">Video and Audio</a></dd>
						<dd><a href=""/go/gntray_sol_web_conferencing"" rel=""_WSS GN-Sol-Conferencing GNav"">Web Conferencing</a></dd>
						<dd><a href=""/go/gntray_sol_web_publishing"" rel=""_WSS GN-Sol-Web GNav"">Web Publishing</a></dd>
						<dt class=""divide""><a href=""/go/gntray_sol_more"" rel=""_WSS GN-Sol-All GNav"">All Industries &amp; Solutions &#8250;</a></dt>
					</dl>
				</li>
				<li><span class=""menu-title""><a href=""/go/gn_prod"" rel=""_WSS GN-Prod GNav"">Products</a></span>
					<ul class=""menu"">
						<li><a href=""/go/gntray_prod_acrobat_family_home"" rel=""_WSS GN-Prod-Acrobat GNav"">Acrobat Family</a></li>
						<li><a href=""/go/gntray_prod_after_effects_home"" rel=""_WSS GN-Prod-AfterEffects GNav"">After Effects</a></li>
						<li><a href=""/go/gntray_prod_breeze_home"" rel=""_WSS GN-Prod-Breeze GNav"">Breeze</a></li>
						<li><a href=""/go/gntray_prod_coldfusion_home"" rel=""_WSS GN-Prod-ColdFusion GNav"">ColdFusion</a></li>
						<li><a href=""/go/gntray_prod_connect_home"" rel=""_WSS GN-Prod-Connect GNav"">Connect</a></li>
						<li><a href=""/go/gntray_prod_creative_suite_family_home"" rel=""_WSS GN-Prod-CS GNav"">Creative Suite Family</a></li>
						<li><a href=""/go/gntray_prod_dreamweaver_home"" rel=""_WSS GN-Prod-Dreamweaver GNav"">Dreamweaver</a></li>
						<li><a href=""/go/gntray_prod_flash_home"" rel=""_WSS GN-Prod-Flash GNav"">Flash</a></li>
						<li><a href=""/go/gnavtray_prod_flex_home"" rel=""_WSS GN-Prod-Flex GNav"">Flex</a></li>
						<li><a href=""/go/gntray_prod_illustrator_home"" rel=""_WSS GN-Prod-Illustrator GNav"">Illustrator</a></li>
						<li><a href=""/go/gntray_prod_indesign_home"" rel=""_WSS GN-Prod-InDesign GNav"">InDesign</a></li>
						<li><a href=""/go/gntray_prod_livecycle_home"" rel=""_WSS GN-Prod-LiveCycle GNav"">LiveCycle</a></li>
						<li><a href=""/go/gntray_prod_macromedia_studio_home"" rel=""_WSS GN-Prod-Studio GNav"">Macromedia Studio</a></li>
						<li><a href=""/go/gntray_prod_mobile_devices_home"" rel=""_WSS GN-Prod-Mobile GNav"">Mobile Products</a></li>
						<li><a href=""/go/gntray_prod_photoshop_family_home"" rel=""_WSS GN-Prod-Photoshop GNav"">Photoshop Family</a></li>
						<li><a href=""/go/gntray_prod_premiere_home"" rel=""_WSS GN-Prod-Premiere GNav"">Premiere</a></li>
						<li class=""divide""><a href=""/go/gntray_prod_products"" rel=""_WSS GN-Prod-All GNav"">All Products &#8250;</a></li>
					</ul>
				</li>
				<li><span class=""menu-title""><a href=""/go/gn_supp"" rel=""_WSS GN-Supp GNav"">Support</a></span>
					<ul class=""menu"">
						<li><a href=""/go/gn_supp_support"" rel=""_WSS GN-Supp-Home GNav"">Support Home</a></li>
						<li><a href=""/go/gntray_supp_cs_home"" rel=""_WSS GN-Supp-Service GNav"">Customer Service</a></li>
						<li><a href=""/go/gntray_supp_train_home"" rel=""_WSS GN-Supp-Training GNav"">Training</a></li>
						<li><a href=""/go/gntray_supp_programs"" rel=""_WSS GN-Supp-Programs GNav"">Support Programs</a></li>
						<li><a href=""/go/gntray_supp_forums_home"" rel=""_WSS GN-Supp-Forums GNav"">Forums</a></li>
						<li><a href=""/go/gntray_supp_documentation"" rel=""_WSS GN-Supp-Docs GNav"">Documentation</a></li>
						<li><a href=""/go/gntray_supp_updates"" rel=""_WSS GN-Supp-Updates GNav"">Updates</a></li>
						<li class=""divide""><a href=""/go/gntray_supp_more"" rel=""_WSS GN-Supp-All GNav"">More &#8250;</a></li>
					</ul>
				</li>
				<li><span class=""menu-title""><a href=""/go/gn_comm"" rel=""_WSS GN-Communities GNav"">Communities</a></span>
					<dl class=""menu"">
						<dt>By User</dt>
						<dd><a href=""/go/gntray_comm_designers"" rel=""_WSS GN-Users-Design GNav"">Designers</a></dd>
						<dd><a href=""/go/gntray_comm_devnet"" rel=""_WSS GN-Users-Dev GNav"">Developers</a></dd>
						<dd><a href=""/go/gntray_comm_educators"" rel=""_WSS GN-Users-Educ GNav"">Educators</a></dd>
						<dd><a href=""/go/gntray_comm_partners"" rel=""_WSS GN-Users-Partners GNav"">Partners</a></dd>
						<dt class=""divide"">By Resource</dt>
						<dd><a href=""/go/gntray_comm_labs"" target=""mm_window"" rel=""_WSS GN-Resource-Labs GNav"">Adobe Labs</a></dd>
						<dd><a href=""/go/gntray_comm_forums"" rel=""_WSS GN-Resource-Forums GNav"">Forums</a></dd>
						<dd><a href=""/go/gntray_comm_exchange_home"" rel=""_WSS GN-Resource-Exchange GNav"">Exchange</a></dd>
						<dd><a href=""/go/gntray_comm_blogs"" target=""mm_window"" rel=""_WSS GN-Resource-Blogs GNav"">Blogs</a></dd>
					</dl>
				</li>
				<li><span class=""menu-title""><a href=""/go/gn_comp"" rel=""_WSS GN-About GNav"">Company</a></span>
					<ul class=""menu"">
						<li><a href=""/go/gntray_comp_aboutadobe"" rel=""_WSS GN-About-Home GNav"">About Adobe</a></li>
						<li><a href=""/go/gntray_comp_press"" rel=""_WSS GN-About-Press GNav"">Press</a></li>
						<li><a href=""/go/gntray_comp_investor_relations"" rel=""_WSS GN-About-Inv GNav"">Investor Relations</a></li>
						<li><a href=""/go/gntray_comp_community_affairs"" rel=""_WSS GN-About-Community GNav"">Corporate Affairs</a></li>
						<li><a href=""/go/gntray_comp_jobs"" rel=""_WSS GN-About-Jobs GNav"">Jobs</a></li>
						<li><a href=""/go/gntray_comp_showcase"" rel=""_WSS GN-About-Showcase GNav"">Showcase</a></li>
						<li><a href=""/go/gntray_comp_events"" rel=""_WSS GN-About-Events GNav"">Events</a></li>
						<li><a href=""/go/gntray_comp_contact_adobe"" rel=""_WSS GN-About-Contact GNav"">Contact Adobe</a></li>
						<li class=""divide""><a href=""/go/gntray_comp_company_more"" rel=""_WSS GN-About-All GNav"">More &#8250;</a></li>
					</ul>
				</li>
				<li><span class=""menu-title""><a href=""/go/gn_dl"" rel=""_WSS GN-DL-Home GNav"">Downloads</a></span>
					<ul class=""menu"">
						<li><a href=""/go/gntray_dl_downloads"" rel=""_WSS GN-DL-Home GNav"">Downloads Home</a></li>
						<li><a href=""/go/gntray_dl_downloads"" rel=""_WSS GN-DL-Trial GNav"">Trial Downloads</a></li>
						<li><a href=""/go/gntray_dl_updates"" rel=""_WSS GN-DL-Updates GNav"">Updates</a></li>
						<li><a href=""/go/gntray_dl_exchange"" rel=""_WSS GN-DL-Exchange GNav"">Exchange</a></li>
						<li><a href=""/go/gntray_dl_get_reader"" rel=""_WSS GN-DL-Reader GNav"">Get Adobe Reader</a></li>
						<li><a href=""/go/gntray_dl_getflashplayer"" rel=""_WSS GN-DL-Flash GNav"">Get Flash Player</a></li>
						<li class=""divide""><a href=""/go/gntray_dl_more"" rel=""_WSS GN-DL-All GNav"">More &#8250;</a></li>
					</ul>
				</li>
				<li><span class=""menu-title""><a href=""/go/gn_store"" rel=""_WSS GN-Store-Home GNav"">Store</a></span>
					<ul class=""menu"">
						<li><a href=""/go/gntray_store"" rel=""_WSS GN-Store-Home GNav"">Store Home</a></li>
						<li><a href=""/go/gntray_store_software"" rel=""_WSS GN-Store-Software GNav"">Software</a></li>
						<li><a href=""/go/gntray_store_fonts"" rel=""_WSS GN-Store-Fonts GNav"">Fonts</a></li>
						<li><a href=""/go/gntray_store_books"" rel=""_WSS GN-Store-Books GNav"">Books</a></li>
						<li><a href=""/go/gntray_store_programs"" rel=""_WSS GN-Store-Support GNav"">Support</a></li>
						<li><a href=""/go/gntray_store_your_hist"" rel=""_WSS GN-Store-Account GNav"">Your Account</a></li>
						<li><a href=""/go/gntray_store_mvlp"" rel=""_WSS GN-Store-Volume GNav"">Volume Licensing</a></li>
						<li class=""divide""><a href=""/go/gntray_store_purchase_options"" rel=""_WSS GN-Store-Reseller GNav"">Other Ways to Buy &#8250;</a></li>
					</ul>
				</li>
			</ul>
		</dd>
		<dd id=""site-search"">
			<p><input type=""hidden"" name=""loc"" value=""en_us"" />
			<input type=""text"" id=""search-input"" name=""term"" />
			<button type=""submit"" id=""search"">Search</button></p>
		</dd>
	</dl>
<noscript>
<div id=""globalnav-noscript"">You may not have everything you need to view certain sections of Adobe.com. Please see our <a href=""/go/gn_sitereqs"">site requirements</a>.</div>
</noscript>
</form>
<!--googleon: index-->
<div id=""layoutLogic"" class=""L2""><!-- InstanceBeginEditable name=""c0 content"" -->
	<div id=""fma-swf"" class=""swfcontent fma""><p><a href=""/aboutadobe/""><img src=""/images/homepage/en_us/fma/FMA_max_fma.jpg"" alt=""MAX 2006"" width=""756"" height=""200"" border=""0"" usemap=""#Map"" />
<map name=""Map"" id=""Map"">
  <area shape=""rect"" coords=""2,1,754,200"" href=""/events/max/"" />
</map></a></p>
</div>
<script type=""text/javascript"">
	// <![CDATA[

	var props = new Object();
        props.swf = ""http://wwwimages.adobe.com/www.adobe.com/swf/homepage/fma_shell/FMA.swf"";
        props.id = ""fma"";
        props.w = ""756"";
        props.h = ""203"";
		props.wmode= ""opaque"";
		props.ver = ""7"";
        /*props.wmode = getWM(""fma-swf"");*/

	var fo = new SWFObject( props );
	fo.addVariable( ""locale"", ""en_us"" );
	fo.addVariable( ""clickTag"", ""/software/mx2004/index.html"" );
	fo.addVariable( ""clickTarget"", ""_self"" );
	fo.addVariable( ""getURLOK"", ""true"" );
	registerSWFObject( fo,""fma-swf"");
	// ]]>
</script>




	<!-- InstanceEndEditable -->
	<div id=""C1"" class=""columns-3-ABcc-A""><!-- InstanceBeginEditable name=""c1 content"" -->
		<div id=""solutions-section"" class=""dyn-pod p1 p1-top""><!-- START Solutions and Products -->
<h1>Solutions and products</h1>
<!-- START Solutions and Products PU -->
<p>Download Adobe Reader and Flash Player.</p>
<div class=""columns-2-AB-A"">
	<p><a href=""/products/acrobat/readstep2.html"" class=""noHover""><img src=""/images/shared/download_buttons/get_adobe_reader.gif"" alt=""Get Adobe Reader"" /></a></p>
</div>
<div class=""columns-2-AB-B"">
	<p><a href=""/shockwave/download/download.cgi?P1_Prod_Version=ShockwaveFlash&promoid=BIOW"" class=""noHover""><img src=""/images/shared/download_buttons/get_flash_player.gif"" alt=""Get Adobe Flash Player"" /></a></p>
</div>
<br class=""clear-both"" />
<!-- END Solutions and Products PU -->
<div class=""hr"">&nbsp;</div>
<div class=""columns-2-AB-A"">
	<h4>Industries</h4>
	<ul class=""link-list"">
		<li><a href=""/education/"">Education</a></li>
		<li><a href=""/financial/"">Financial services</a></li>
		<li><a href=""/government/"">Government</a></li>
		<li><a href=""/lifesciences/"">Life sciences</a></li>
		<li><a href=""/manufacturing/"">Manufacturing</a></li>
		<li><a href=""/resources/telecom/"">Telecommunications</a></li>
	</ul>
	<h4>Solutions</h4>
	<ul class=""link-list"">
		<li><a href=""/digitalimag/"">Digital imaging</a></li>
		<li><a href=""/mobile/"">Mobile</a></li>		
		<li><a href=""/print/"">Print publishing</a></li>
		<li><a href=""/products/server/"">Process management services</a></li>		
		<li><a href=""/resources/business/rich_internet_apps/"">Rich Internet applications</a></li>
		<li><a href=""/resources/elearning/"">Training and eLearning</a></li>	
		<li><a href=""/motion/"">Video and audio</a></li>
		<li><a href=""/products/breeze/solutions/webconferencing/"">Web conferencing</a></li>
		<li><a href=""/web/"">Web publishing</a></li>
	</ul>
	<p class=""call-action""><a href=""/solutions/"" class=""link-more"">See more industries and solutions</a></p>
</div>
<div class=""columns-2-AB-B"">
	<h4>Products</h4>
	<ul class=""link-list"">
		<li><a href=""/products/acrobat/"">Acrobat family</a></li>
		<li><a href=""/products/aftereffects/"">After Effects</a></li>
		<li><a href=""/products/breeze/"">Breeze</a></li>
		<li><a href=""/products/coldfusion/"">ColdFusion</a></li>
		<li><a href=""/creativesuite/"">Creative Suite family</a></li>
		<li><a href=""/products/dreamweaver/"">Dreamweaver</a></li>
		<li><a href=""/products/flash/flashpro/"">Flash</a></li>
		<li><a href=""/products/flashmediaserver/"">Flash Media Server</a></li>
		<li><a href=""/products/flex/"">Flex</a></li>
		<li><a href=""/products/illustrator/"">Illustrator</a></li>
		<li><a href=""/products/indesign/"">InDesign</a></li>
		<li><a href=""/products/livecycle/"">LiveCycle</a></li>
		<li><a href=""/products/studio/"">Studio</a></li>
		<li><a href=""/products/photoshop/family.html"">Photoshop family</a></li>
		<li><a href=""/products/premiere/"">Adobe Premiere</a></li>
	</ul>
	<p class=""call-action""><a href=""/products/"" class=""link-more"">See all products</a></p>
</div>
<br class=""clear-both"" />
<div class=""hr"">&nbsp;</div>
	<!-- START Support Pod -->
<h4>More technologies</h4>
<br />
<div class=""columns-2-AB-A"">
	<p><a href=""/shockwave/download/?promoid=BIOY"" class=""noHover""><img src=""/images/shared/download_buttons/get_shock_player.gif"" alt=""Get Shockwave Player"" /></a></p>
</div>
<div class=""columns-2-AB-B"">
<p><a href=""http://createpdf.adobe.com/?v=AHP"" class=""noHover""><img src=""/images/pdf_online_btn.gif"" alt=""Create PDF online"" /></a></p>
</div>
<br class=""clear-both"">
<!-- END Support Pod -->
<div class=""hr"">&nbsp;</div>
	<!-- START Support Pod -->
<h4>Support</h4>
<div class=""columns-2-AB-A"">
	<ul class=""link-list"">
		<li><a href=""/support/?promoid=BIOQ"">Support by product</a></li>
		<li><a href=""/support/forums/?promoid=BIOU"">Forums</a></li>		
	</ul>
</div>
<div class=""columns-2-AB-B"">
<ul class=""link-list"">
		<li><a href=""/support/programs/?promoid=BIOT"">Support programs</a></li>
	</ul>
</div>
<br class=""clear-both"" />
<!-- END Support Pod -->
<!-- END Solutions and Products --></div>	<!-- InstanceEndEditable --></div>
	<div id=""C2"" class=""columns-3-ABcc-B""><!-- InstanceBeginEditable name=""c2 content"" -->
		<div id=""announcements-section"" class=""dyn-pod p1 p1-top""><!-- START Announcements Pod -->
<h1>Announcements</h1>
<!-- START Annoucements 1 PU -->
<div class=""pullout-left left-60"">
	<p class=""pullout-item""><a href=""/products/acrobat/"" ><img src=""/images/shared/product_logos/60x45/60x45_Acrobat8Pro.jpg"" alt=""Introducing the Adobe Acrobat family"" class=""image-border""/></a></p>
	<h4><a href=""/products/acrobat/"" >Introducing the new Adobe Acrobat family</a></h4>
	<p>Communicate and collaborate with more secure PDF documents and interactive, real-time web conferencing.</p>
</div>
<!-- END Annoucements 1 PU -->
<!-- START Annoucements 2 PU -->
<div class=""pullout-left left-60"">
	<p class=""pullout-item""><a href=""/products/photoshopelwin/""><img src=""/images/shared/product_logos/60x45/60x45_PSE5.jpg"" alt=""Announcing Adobe Photoshop Elements 5.0"" class=""image-border""/></a></p>
	<h4><a href=""/products/photoshopelwin/"">Now available: Adobe Photoshop Elements 5.0</a></h4>
	<p>Make your photos look their best and show them off in creative, entertaining ways.</p>
</div>
<!-- END Annoucements 2 PU -->


<!-- START Annoucements 3 PU -->
<div class=""pullout-left left-60"">
	<p class=""pullout-item""><a href=""/products/premiereel/"" ><img src=""/images/shared/product_logos/60x45/60x45_PRE3.jpg"" alt=""Announcing Adobe Premiere Elements 3.0"" class=""image-border"" /></a></p>
	<h4><a href=""/products/premiereel/"" >Now available: Adobe Premiere Elements 3.0</a></h4>
	<p>Bring your home videos to life quickly and easily with helpful moviemaking options and amazing effects.</p>
</div><!-- END Annoucements 3 PU -->
<!-- START Annoucements 4 PU -->
<div class=""pullout-left left-60"">
	<p class=""pullout-item""><a href=""/products/captivate/""><img src=""/images/shared/product_logos/60x45/60x45_Captivate.gif"" alt=""New: Adobe Captivate 2"" /></a></p>
	<h4><a href=""/products/captivate/"">New: Adobe Captivate 2</a></h4>
	<p>Rapidly create engaging learning experiences without programming knowledge or multimedia skills.</p>
</div>
<!-- END Annoucements 4 PU -->


<!-- START Annoucements 5 PU -->
<div class=""pullout-left left-60"">
	<p class=""pullout-item""><a href=""/devnet/breeze/articles/sync_swf_contest.html""><img src=""/images/shared/product_logos/60x45/60x45_Breeze.gif"" alt=""Breeze meeting contest"" /></a></p>
	<h4><a href=""/devnet/breeze/articles/sync_swf_contest.html"">Flash developer contest — US$20,000 in prize money</a></h4>
	<p>Create a collaborative Flash application for use with Acrobat Connect (Breeze). Winners will be announced at MAX.</p>
</div>
<!-- END Annoucements 5 PU -->
<div class=""hr"">&nbsp;</div>
<!-- START Events & Seminars Pod -->
<h4>Events and seminars</h4>
<ul class=""link-list"">
<li><a href=""/go/acrobat8events"">Acrobat in action</a></li>
<li><a href=""http://www.adobe.com/de/events/photokina2006/index.html"">Photokina</a></li>
	<li><a href=""http://www.adobe.com/events/max/"">Adobe MAX</a></li>	
	<li><a href=""http://www.adobe.com/cfusion/event/index.cfm?event=detail&amp;id=462539&amp;loc=en_us"">Adobe Flex 2 live eSeminar series</a></li>
	<li><a href=""http://www.adobe.com/cfusion/event/index.cfm?event=detail&amp;id=452655&amp;loc=en_us"">Studio 8 eSeminar Series</a> </li>
	

</ul>
<p class=""call-action""><a href=""/events/"" class=""link-more"">See all events</a></p>
<!-- END Events & Seminars Pod -->



<!-- END Announcements Pod --></div>
	<!-- InstanceEndEditable --></div>
	<div id=""C3"" class=""columns-3-ABcc-cc""><!-- InstanceBeginEditable name=""c3 content"" -->
		<div id=""purchase-section"" class=""dyn-pod p2 p2-top""><!-- START Purchase Pod -->
<h1>Purchase</h1>
<!-- START Purchase PU -->
<div class=""pullout-left left-50"">
	<p class=""pullout-item""><a href=""/cfusion/store/html/index.cfm?event=displayStoreSelector&keyword=cs_premium_upg&promoid=JINY""><img src=""/images/homepage/promos/50x50_boxshot_CS2.3.gif"" alt=""Adobe Creative Suite 2.3 boxshot"" /></a></p>
	<h4><a href=""/cfusion/store/html/index.cfm?event=displayStoreSelector&keyword=cs_premium_upg&promoid=JINY"">Adobe Creative Suite 2.3</a></h4>
	<p class=""compact"">Now with the all-new Acrobat 8 Professional.<br />
		<a href=""/cfusion/store/html/index.cfm?event=displayStoreSelector&keyword=cs_premium_upg&promoid=JINY"" class=""link-more"">Preorder now</a></p>
</div>
<!-- END Purchase PU -->


<div class=""hr"">&nbsp;</div>
<h4><a href=""/cfusion/store/html/index.cfm?event=displayStoreSelector&amp;promoid=BIOZ"">Adobe Store</a></h4>
<ul class=""link-list"">
	<li><a href=""/aboutadobe/openoptions/?promoid=BIPB"">Volume license purchase</a></li>
	<li><a href=""/buy/?promoid=BIPC"">Other ways to purchase</a></li>
	<li><a href=""/special/offers.html?promoid=JKJN"">Special Offers</a></li>
</ul>
<!-- END Purchase Pod --></div>
		<div id=""devcenter-section""class=""dyn-pod p2 p2-top""><!-- START Developer Center Pod -->
<h1>Designer and developer</h1>
<h4><a href=""/designcenter/"">Design Center</a></h4>
<p>For creative professionals in web, print, and digital video.</p>
<h4><a href=""/devnet/"">Developer Center</a></h4>
<p>Tips, techniques, and other developer resources.</p>
<!-- END Developer Center Pod -->
</div>
		<div id=""showcase-section"" class=""dyn-pod p2 p2-top""><!-- START Showcase pod -->
<h1>Customer success</h1>
<div class=""pullout-left left-60"">
  <p class=""pullout-item""><a href=""/cfusion/showcase/index.cfm?promoid=home_sod_092506"" class=""noHover""><img src=""/showcase/sotd/images/2006/sept/25_58x43.jpg"" alt=""Site of the Day"" width=""58"" height=""43"" border=""0"" class=""image-border"" /></a></p>
  <h4><a href=""/cfusion/showcase/index.cfm?promoid=home_sod_092506"">UNSCENE<br />Urban Navigator</a></h4>	
</div>
<div class=""hr"">&nbsp;</div>
<h4>Pickard Chilton and Acrobat</h4>
<ul class=""link-list"">
  <li><a href=""/cfusion/showcase/index.cfm?event=casestudydetail&casestudyid=112525&loc=en_us"" class=""link-more"">View the success story </a></li>
</ul>
<h4><a href=""/cfusion/showcase/index.cfm"">Check out additional customer content, including Site of the Day.</a></h4>
<!-- END Showcase pod -->
</div>
	<!-- InstanceEndEditable --></div><br class=""clear-both"" />
</div>
<!--googleoff: index--><!-- global footer $Revision: 1.28 $ -->
<div id=""globalfooter"">
	<ul class=""pipe menu compact"">
		<li><a href=""/go/gftray_foot_aboutadobe"">Company</a></li>
		<li><a href=""/go/gftray_foot_privacy_security"">Online Privacy Policy</a></li>
		<li><a href=""/go/gftray_foot_terms"">Terms of Use</a></li>
		<li><a href=""/go/gftray_foot_contact_adobe"">Contact Us</a></li>
		<li><a href=""/go/gftray_foot_accessibility"">Accessibility</a></li>
		<li><a href=""/go/gftray_foot_report_piracy"">Report Piracy</a></li>
		<li><a href=""/go/gftray_foot_permissions_trademarks"">Permissions &amp; Trademarks</a></li>
		<li><a href=""/go/gftray_foot_product_license_agreements"">Product License Agreements</a></li>
		<li><a href=""/go/gftray_foot_feedback"">Send Feedback</a></li>
	</ul>
	<div class=""pullout-right right-100""> 
		<p class=""pullout-item""><a href=""/go/gftray_foot_truste"" target=""_blank""><img src=""/images/globalnav/eufinalmark.gif"" alt=""Reviewed by TRUSTe: site privacy statement"" width=""116"" height=""41"" id=""trustelogo"" /></a></p>		
		<p id=""copyright"">Copyright &#169; 2006 Adobe Systems Incorporated. <a href=""/go/gftray_all_rights_reserved"">All rights reserved</a>.</p>
		<p id=""terms"">Use of this website signifies your agreement to the <a href=""/go/gftray_foot_terms"">Terms of Use</a> and <a href=""/go/gftray_foot_privacy_security"">Online Privacy Policy (updated 06-21-2008)</a>.</p>
		<p id=""searchengine"">Search powered by<a href=""http://www.google.com/"" target=""new""><img class=""googlelogo"" src=""/images/master/logo_google.gif"" width=""43"" height=""18"" alt=""Powered by Google"" /></a></p>
	</div>
</div>
<!--googleon: index-->
<script src=""/js/htmltemplate/beforeonload.js"" type=""text/javascript""></script>
<!-- InstanceBeginEditable name=""analytics"" -->
<div style=""display: none;""><!-- SiteCatalyst code version: F.3. $Revision: 1.1 $
Copyright 2002 Omniture, Inc. More info available at
http://www.omniture.com --><script language=""JavaScript"" type=""text/javascript""><!--
var s_code=' '//--></script>
<script language=""JavaScript"" src=""/js/wss/wss_variables.js"" type=""text/javascript""></script>
<script language=""JavaScript"" src=""/js/wss/wss_trackpdf.js"" type=""text/javascript""></script>
<script language=""JavaScript"" src=""/js/wss/hbx.js"" type=""text/javascript""></script>
<script language=""JavaScript"" src=""/js/wss/events.js"" type=""text/javascript""></script>
<script language=""JavaScript"" src=""/uber/js/omniture_s_code.js"" type=""text/javascript""></script>
<script language=""JavaScript"" type=""text/javascript""><!--
var s_accountName;
var s_docHost = window.location.hostname.toLowerCase();
var s_docURL = window.location.pathname.toLowerCase();
if ((s_docHost.indexOf(""stage."") != -1) || (s_docHost.indexOf(""staging."") != -1)) {
  s_accountName=""mxadobetest"";
}
else {
  s_accountName=""mxmacromedia"";
}
if (s_docURL.indexOf(""/devnet/"") != -1) {
  s_channel=""DevNet"";
}
var s_wd=window,s_tm=new Date;if(s_code!=' '){s_code=s_dc(
s_accountName);if(s_code)document.write(s_code)}else
document.write('<im'+'g src=""http://192.168.112.2O7.net/b/ss/'+s_accountName+'/1/F.3-fb/s'+s_tm.getTime()+'?[AQB]'
+'&pageName='+escape(s_wd.s_pageName?s_wd.s_pageName:(s_wd.pageName?s_wd.pageName:''))
+'&server='+escape(s_wd.s_server?s_wd.s_server:(s_wd.server?s_wd.server:''))
+'&ch='+escape(s_wd.s_channel?s_wd.s_channel:(s_wd.channel?s_wd.channel:''))
+'&[AQE]"" height=""1"" width=""1"" border=""0"" alt="""" />')
function sendAnalyticsEvent(str){var ns=s_accountName;if(str!=null)ns+="",""+str;void(s_gs(ns));}
//--></script><noscript><img
src=""http://192.168.112.2O7.net/b/ss/mxmacromedia/1/F.3-XELvs""
height=""1"" width=""1"" border=""0"" alt="""" /></noscript><!--/DO NOT REMOVE/-->
<!-- End SiteCatalyst code version: F.3. -->
</div>

<!--oobegin 
* OnlineOpinionF3c v3.0
* The following code is Copyright 1998-2008 Opinionlab, Inc.
* All rights reserved. Unauthorized use is prohibited.
* This product and other products of OpinionLab, Inc. are protected by U.S. Patent No. US 6606581, 6421724, 6785717 B1 and other patents pending.
* http://www.opinionlab.com
-->
<script language=""javascript"" type=""text/javascript"" charset=""windows-1252"" src=""/onlineopinionF3c/oo_engine.js""></script>
<script language=""javascript"" type=""text/javascript"" charset=""windows-1252"" src=""/onlineopinionF3c/oo_conf_en-US.js""></script>
<!--ooend-->
<noscript>
<!--[if lt IE 7]><link href=""/css/master_import/noscript_ie6.css"" type=""text/css"" rel=""stylesheet"" /><![endif]-->
<!--[if IE 7]><link href=""/css/master_import/noscript_ie7.css"" type=""text/css"" rel=""stylesheet"" /><![endif]-->
</noscript><!-- InstanceEndEditable -->
<img id=""flash_pixel"" name=""flash_pixel"" src=""/images/pixel.gif"" width=""1"" height=""1"" alt="""" />
</body>
<!-- InstanceEnd --></html>
";

            XmlDocument doc;

            doc = HtmlParser.Parse(source);
            Assert.IsNotNull(doc["root"]["html"]["head"]);
            Assert.IsNotNull(doc["root"]["html"]["body"]);
        }

        [TestMethod]
        [TestProperty("Lib", "LillTek.Advanced")]
        public void HtmlParser_Parse_Corbis()
        {
            // Generate a DOM source scraped from www.microsoft.com and 
            // make sure we don't see an exception.

            const string source =
@"

<!DOCTYPE HTML PUBLIC ""-//W3C//DTD HTML 4.0 Transitional//EN"" >
<HTML>
	<HEAD>
	
	
		<style>
			<!-- --> #oHomePage {behavior:url(#default#homepage);}
	
	        
		</style>

	<script>
	if (self!= top) 
		top.location = self.location;
		
	function newwindow(url)
	{
		var params;
		var agent = navigator.userAgent;
		var windowName = ""CorbisWindow"";
		
		params = """";
		params += ""height=556,""
		params += ""width=603,""
		params += ""left=50,""
		params += ""top=50,""
		params += ""alwaysRaised=0,""
		params += ""directories=0,""
		params += ""fullscreen=0,""
		params += ""location=0,""
		params += ""menubar=b,""
		params += ""resizable=1,""
		params += ""scrollbars=1,""
		params += ""status=0,""
		params += ""toolbar=0""
		
		var win = window.open(url,windowName,params);	
		 
		if (agent.indexOf(""Mozilla/2"") != -1 && agent.indexOf(""Win"") == -1) 
		{
			win = window.open(url, windowName , params);
		}
		
		if (!win.opener) 
		{
			win.opener = window;
		}
	}
	
	function setHomePage()
	{
		if(document.all)
		{
			oHomePage.setHomePage(""http://pro.corbis.com"");
		}

	}
	function DoNotCache()
	{}
		</script>
		
		<link rel=""stylesheet"" href=""/creative/promo.css"" type=""text/css"" /><link href=""http://cache.corbis.com/pro/6.3/style/iestyle.css"" rel=""stylesheet"" type=""text/css"" /><meta name=""TITLE"" content=""Corbis: stock photography and digital pictures"" /><meta name=""KEYWORDS"" content=""Millions of images online, featuring the finest in historical, fine art, business, technology, celebrity, travel, sports and nature photography for advertising, publishing and multimedia design."" /><meta name=""DESCRIPTION"" content=""pictures, stock photography, digital stock photography, stock photos, stock image, stock pictures, stock agency, digital images, advertising stock, web images, online images, photos, photo search, photo research, digital stock, image licensing, royalty free, rights managed, online photos, historical photos, fine art photography, celebrity photos, technology photos, contemporary photos, travel photos, business people, animal photos, nature photography, find photographs, photography, Corbis, Corbis Image"" /><title>
	Corbis:  photography, rights, assignment, motion.
</title></HEAD>
	<body onunload="""" onload=""DoNotCache()"">
		<form name=""ctl01"" method=""post"" action=""Default.aspx"" id=""ctl01"">
<div>
<input type=""hidden"" name=""__EVENTTARGET"" id=""__EVENTTARGET"" value="""" />
<input type=""hidden"" name=""__EVENTARGUMENT"" id=""__EVENTARGUMENT"" value="""" />
<input type=""hidden"" name=""__LASTFOCUS"" id=""__LASTFOCUS"" value="""" />
<input type=""hidden"" name=""__VIEWSTATE"" id=""__VIEWSTATE"" value=""/wEPDwUJMzg5MDY0MDcyD2QWAgIDD2QWAgIED2QWAmYPDxYCHgdWaXNpYmxlZ2QWAgIBDw8WAh8AZ2QWCAIBD2QWDmYPZBYCZg8VAQdQU1dXVzEyZAICDxYCHgRocmVmBRhqYXZhc2NyaXB0OnNldEhvbWVQYWdlKCkWAmYPFQEcaHR0cDovL2NhY2hlLmNvcmJpcy5jb20vcHJvL2QCAw8VARxodHRwOi8vY2FjaGUuY29yYmlzLmNvbS9wcm8vZAIED2QWAgIEDxUBHGh0dHA6Ly9jYWNoZS5jb3JiaXMuY29tL3Byby9kAgYPZBYCAgQPFQEcaHR0cDovL2NhY2hlLmNvcmJpcy5jb20vcHJvL2QCCg9kFgJmDxUFHGh0dHA6Ly9jYWNoZS5jb3JiaXMuY29tL3Byby8OcHJvLmNvcmJpcy5jb20caHR0cDovL2NhY2hlLmNvcmJpcy5jb20vcHJvLxxodHRwOi8vY2FjaGUuY29yYmlzLmNvbS9wcm8vDnByby5jb3JiaXMuY29tZAILDxUCHGh0dHA6Ly9jYWNoZS5jb3JiaXMuY29tL3Byby8caHR0cDovL2NhY2hlLmNvcmJpcy5jb20vcHJvL2QCAg9kFhRmDxYCHgRUZXh0BTB3aWR0aDoxMDAlO2hlaWdodDoxOHB4O2JhY2tncm91bmQtY29sb3I6IzhiOGU4NTtkAgEPFQEcaHR0cDovL2NhY2hlLmNvcmJpcy5jb20vcHJvL2QCAg9kFgxmDxYCHgVjbGFzcwURc2VhcmNoVGFiSW5hY3RpdmUWAmYPFgQfAwUVc2VhcmNoVGFiSW5hY3RpdmVUZXh0Hglpbm5lcmh0bWwFD1NlYXJjaCBDcmVhdGl2ZWQCAQ8WAh8DBRFzZWFyY2hUYWJJbmFjdGl2ZRYCZg8WAh4Dc3JjBTFodHRwOi8vY2FjaGUuY29yYmlzLmNvbS9wcm8vc2VhcmNoX3RhYl9jcm5yX2kuZ2lmZAICD2QWAmYPFgQfAwUVc2VhcmNoVGFiSW5hY3RpdmVUZXh0HwQFEFNlYXJjaCBFZGl0b3JpYWxkAgMPFgIfAwURc2VhcmNoVGFiSW5hY3RpdmUWAmYPFgIfBQUxaHR0cDovL2NhY2hlLmNvcmJpcy5jb20vcHJvL3NlYXJjaF90YWJfY3Jucl9pLmdpZmQCBA8WAh8DBQ9zZWFyY2hUYWJBY3RpdmUWAmYPFgQfAwUTc2VhcmNoVGFiQWN0aXZlVGV4dB8EBQpTZWFyY2ggQWxsZAIFDxYCHwMFD3NlYXJjaFRhYkFjdGl2ZRYCZg8WAh8FBTFodHRwOi8vY2FjaGUuY29yYmlzLmNvbS9wcm8vc2VhcmNoX3RhYl9jcm5yX2EuZ2lmZAIFDxUBHGh0dHA6Ly9jYWNoZS5jb3JiaXMuY29tL3Byby9kAgYPZBYCAgEPFQEcaHR0cDovL2NhY2hlLmNvcmJpcy5jb20vcHJvL2QCCA8VARxodHRwOi8vY2FjaGUuY29yYmlzLmNvbS9wcm8vZAIJDxYCHwEFK2h0dHBzOi8vcHJvLmNvcmJpcy5jb20vbG9naW4vbG9naW5tYWluLmFzcHhkAgsPFQEcaHR0cDovL2NhY2hlLmNvcmJpcy5jb20vcHJvL2QCDQ8VARxodHRwOi8vY2FjaGUuY29yYmlzLmNvbS9wcm8vZAIPDxUBHGh0dHA6Ly9jYWNoZS5jb3JiaXMuY29tL3Byby9kAgQPZBYCZg9kFiBmDxYCHwIFBWVuLVVTZAIBD2QWAgIDDw9kFgIeB29uY2xpY2sFF3JldHVybih2YWxpZGF0ZUZvcm0oKSk7ZAICD2QWCgIDDxYCHwIFPWN0bDA4X3NlYXJjaEluUmVzdWx0c0NoZWNrQ29udHJvbF9TZWFyY2hJblJlc3VsdHNEcm9wQ2hlY2tCb3hkAgUPFgIfAgU0Y3RsMDhfc2VhcmNoSW5SZXN1bHRzQ2hlY2tDb250cm9sX3NlYXJjaEluUmVzdWx0c0RpdmQCBw8WAh8CBQR0cnVlZAIJDxYCHwIFB1Jlc3VsdHNkAgsPZBYCAgEPEA8WAh4HQ2hlY2tlZGhkZGRkAgUPZBYCZg8WAh4LXyFJdGVtQ291bnQCAhYEZg9kFgICAQ8QDxYEHwIFC1Bob3RvZ3JhcGh5HwdnFgIeBVZhbHVlBQEyZGRkAgEPZBYCAgEPEA8WBB8CBQxJbGx1c3RyYXRpb24fB2cWAh8JBQExZGRkAgYPZBYCZg8WAh8IAgIWBGYPZBYCAgEPEA8WBB8CBQVDb2xvch8HZxYCHwkFATFkZGQCAQ9kFgICAQ8QDxYEHwIFEUJsYWNrICZhbXA7IFdoaXRlHwdnFgIfCQUBMmRkZAIKDxYCHwIFLjxkaXYgaWQ9IkNvbGxlY3Rpb25zRGl2IiBzdHlsZT0iZGlzcGxheTpub25lIj5kAgsPZBYMAgEPEA8WAh8HZxYCHwYFGFNlbGVjdEFsbENvbGxlY3Rpb25zKDIpO2RkZAIDDxAPFgIfB2cWAh8GBRhTZWxlY3RBbGxDb2xsZWN0aW9ucygxKTtkZGQCBQ8QDxYCHwdnFgIfBgUYU2VsZWN0QWxsQ29sbGVjdGlvbnMoMCk7ZGRkAgsPEA9kFgIeCG9uY2hhbmdlBRNDaGVja0NvbGxlY3Rpb24oMik7DxYaZgIBAgICAwIEAgUCBgIHAggCCQIKAgsCDAINAg4CDwIQAhECEgITAhQCFQIWAhcCGAIZFhoQBRZBbmR5IFdhcmhvbCBGb3VuZGF0aW9uBQI1NWcQBRVBdGxhbnRpZGUgUGhvdG90cmF2ZWwFAjc5ZxAFA0JCQwUCODBnEAUKQmVhdGV3b3JrcwUCODFnEAUMQmV0dG1hbm4vVVBJBQExZxAFD0Jsb29taW1hZ2UgKFJNKQUCODJnEAUvQ2Flc2NvUGljdHVyZXMgKEZvcm1lcmx5IEFyY2hpdm8gSWNvbm9ncmFwaGljbykFAjgzZxAFEUNocmlzdGllJ3MgSW1hZ2VzBQI4NGcQBQtDb25kw6kgTmFzdAUCNDFnEAUKQ3JlYXNvdXJjZQUCODVnEAUSRG9ybGluZyBLaW5kZXJzbGV5BQI4NmcQBQhFbnZpc2lvbgUCODdnEAUdRmluZSBBcnQgUGhvdG9ncmFwaGljIExpYnJhcnkFAjg4ZxAFEEZyYXRlbGxpIEFsaW5hcmkFAjg5ZxAFDkh1bHRvbi1EZXV0c2NoBQI5MGcQBQ5JbWFnZSBQb2ludCBGUgUCOTFnEAUPSW1hZ2VzLmNvbSAoUk0pBQI5MmcQBRFNYXJ2ZWwgQ2hhcmFjdGVycwUCOTNnEAUDTUdNBQI5NGcQBRVNaWNoYWVsIE9jaHMgQXJjaGl2ZXMFAjk1ZxAFDFBob3RvQ3Vpc2luZQUCOTdnEAUcUm9iZXJ0IEhhcmRpbmcgV29ybGQgSW1hZ2VyeQUCOTlnEAUZU2NobGVnZWxtaWxjaCBQaG90b2dyYXBoeQUDMTAwZxAFCFN3aW0gSW5rBQMxMDJnEAURVmlzdWFscyBVbmxpbWl0ZWQFAzEwM2cQBQR6ZWZhBQI1OGdkZAINDxAPZBYCHwoFE0NoZWNrQ29sbGVjdGlvbigxKTsPFhRmAgECAgIDAgQCBQIGAgcCCAIJAgoCCwIMAg0CDgIPAhACEQISAhMWFBAFDEJsZW5kIEltYWdlcwUCNjBnEAUPQmxvb21pbWFnZSAoUkYpBQI2MWcQBRBCcmFuZCBYIFBpY3R1cmVzBQI2MmcQBQhDb21zdG9jawUCNjNnEAUJQ29yYmlzIFJGBQI3NmcQBQtEZXNpZ24gUGljcwUCNjVnEAUJRGV4IEltYWdlBQI2NGcQBQlHb29kc2hvb3QFAjY2ZxAFDEltYWdlIFNvdXJjZQUCNDNnEAUIaW1hZ2UxMDAFAjY3ZxAFD0ltYWdlcy5jb20gKFJGKQUCNjhnEAUJSW1hZ2VzaG9wBQI2OWcQBQxJbnNpZGVPdXRQaXgFAjc4ZxAFC01lZGlvSW1hZ2VzBQI3MGcQBRJNaWtlIFdhdHNvbiBJbWFnZXMFAjc3ZxAFB1BpeGxhbmQFAjcxZxAFE1N0b2NrYnl0ZS9TdG9ja2Rpc2MFAjcyZxAFDFRldHJhIEltYWdlcwUCNzNnEAUKVGhpbmtzdG9jawUCNzRnEAUHemVmYSBSRgUCNzVnZGQCDw8QD2QWAh8KBRNDaGVja0NvbGxlY3Rpb24oMCk7DxYGZgIBAgICAwIEAgUWBhAFA2VwYQUCNTlnEAUITmV3U3BvcnQFAjU3ZxAFB1JldXRlcnMFAjEwZxAFBFNhYmEFAjMzZxAFBVN5Z21hBQIyNWcQBQRadW1hBQI1NmdkZAIMDxYCHwIFMTxkaXYgaWQ9IkFkdmFuY2VkU2VhcmNoRGl2IiBzdHlsZT0iZGlzcGxheTpub25lIj5kAg0PZBYCAgMPFgIfCAIDFgZmD2QWAgIBDxAPFgQfAgUKSG9yaXpvbnRhbB8HZxYCHwkFATJkZGQCAQ9kFgICAQ8QDxYEHwIFCFZlcnRpY2FsHwdnFgIfCQUBMWRkZAICD2QWAgIBDxAPFgQfAgUIUGFub3JhbWEfB2cWAh8JBQEzZGRkAg4PZBYCAgUPFgIfCAIHFg5mD2QWAgIBDxAPFgQfAgUKQ29tbWVyY2lhbB8HZxYCHwkFAjEwZGRkAgEPZBYCAgEPEA8WBB8CBQlFZGl0b3JpYWwfB2cWAh8JBQExZGRkAgIPZBYCAgEPEA8WBB8CBQpIaXN0b3JpY2FsHwdnFgIfCQUBMmRkZAIDD2QWAgIBDxAPFgQfAgUDQXJ0HwdnFgIfCQUBNWRkZAIED2QWAgIBDxAPFgQfAgUETmV3cx8HZxYCHwkFATZkZGQCBQ9kFgICAQ8QDxYEHwIFBlNwb3J0cx8HZxYCHwkFATlkZGQCBg9kFgICAQ8QDxYEHwIFDUVudGVydGFpbm1lbnQfB2cWAh8JBQE3ZGRkAg8PZBYEAgsPDxYCHwIFCm1tL2RkL3l5eXlkZAIPDw8WAh8CBQptbS9kZC95eXl5ZGQCEg9kFgICAw8QZA8WAmYCARYCEAUGSW1hZ2VzZWcQBQpJbWFnZSBTZXRzBQRzZXRzZxYBZmQCFg8PFgIfAGhkZAIXD2QWAgIDDxBkDxYGZgIBAgICAwIEAgUWBhAFA0FsbGVnEAUGQWVyaWFsBQExZxAFCENsb3NlLXVwBQEyZxAFBUFib3ZlBQE1ZxAFBUJlbG93BQE2ZxAFCkZyb20gU3BhY2UFATdnZGQCGA9kFgICAw8QZA8WB2YCAQICAgMCBAIFAgYWBxAFFldpdGggb3IgV2l0aG91dCBQZW9wbGVlZxAFC1dpdGggUGVvcGxlBQE1ZxAFDldpdGhvdXQgUGVvcGxlBQE2ZxAFDTEgUGVyc29uIE9ubHkFATFnEAUNMiBQZW9wbGUgT25seQUBMmcQBQozLTUgUGVvcGxlBQEzZxAFEEdyb3VwcyBvciBDcm93ZHMFATRnZGQCGQ9kFgICAw8QZA8WBWYCAQICAgMCBBYFEAUPQWxsIFJlc29sdXRpb25zZWcQBQNMb3cFAjY3ZxAFBk1lZGl1bQUCNjRnEAUESGlnaAUDNTYxZxAFBVVsdHJhBQM1NTlnZGQCBg9kFgQCAw9kFgJmDxAPFgIeC18hRGF0YUJvdW5kZ2QQFQsXRW5nbGlzaCAoVW5pdGVkIFN0YXRlcykUQ2hpbmVzZSAoU2ltcGxpZmllZCkFRHV0Y2gYRW5nbGlzaCAoVW5pdGVkIEtpbmdkb20pBkZyZW5jaAZHZXJtYW4HSXRhbGlhbghKYXBhbmVzZQZQb2xpc2gTUG9ydHVndWVzZSAoQnJhemlsKQdTcGFuaXNoFQsFZW4tVVMGemgtQ0hTBW5sLU5MBWVuLUdCBWZyLUZSBWRlLURFBWl0LUlUBWphLUpQBXBsLVBMBXB0LUJSBWVzLUVTFCsDC2dnZ2dnZ2dnZ2dnFgFmZAIEDxYCHwIF/AQ8YSBocmVmPWh0dHA6Ly93d3cuY29yYmlzbW90aW9uLmNvbT9saW5raWQ9MTUwMDAwIHRhcmdldD1fYmxhbms+Q29yYmlzIE1vdGlvbjwvYT4mbmJzcDsgPGEgaHJlZj1odHRwOi8vZGVjb3IuY29yYmlzLmNvbS9kZWZhdWx0LmFzcHg/bGlua2lkPTE1MDAwIHRhcmdldD1fYmxhbms+RMOpY29yPC9hPiZuYnNwOyA8YSBocmVmPWh0dHA6Ly9lZHVjYXRpb24uY29yYmlzLmNvbS9kZWZhdWx0LmFzcHg/bGlua2lkPTE1MDAwIHRhcmdldD1fYmxhbms+RWR1Y2F0aW9uYWwgVXNlPC9hPiZuYnNwOyA8YSBocmVmPWh0dHA6Ly9tb2JpbGUuY29yYmlzLmNvbS9kZWZhdWx0LmFzcHg/bGlua2lkPTE1MDAwMCB0YXJnZXQ9X2JsYW5rPk1vYmlsZTwvYT4mbmJzcDsgPGEgaHJlZj0vY3JlYXRpdmUvc2VydmljZXMvY2F0YWxvZ3M/bGlua2lkPTE1MDAwMD5DYXRhbG9nczwvYT4mbmJzcDsgPGEgaHJlZj1odHRwOi8vd3d3LmNvcmJpcy5jb20vY29ycG9yYXRlL3ByZXNzcm9vbS9kZWZhdWx0LmFzcD9saW5raWQ9MTUwMDAwPlByZXNzcm9vbTwvYT4mbmJzcDsgPGEgaHJlZj1odHRwOi8vd3d3LmNvcmJpcy5jb20vY29ycG9yYXRlL0VtcGxveW1lbnQvRW1wbG95bWVudC5hc3A/bGlua2lkPTE1MDAwMD5FbXBsb3ltZW50PC9hPiZuYnNwOyBkGAEFHl9fQ29udHJvbHNSZXF1aXJlUG9zdEJhY2tLZXlfXxYcBT1jdGwwOCRzZWFyY2hJblJlc3VsdHNDaGVja0NvbnRyb2wkU2VhcmNoSW5SZXN1bHRzRHJvcENoZWNrQm94BTtjdGwwOCRyaWdodHNNYW5hZ2VkUm95YWx0eUZyZWVDb250cm9sJFJpZ2h0c01hbmFnZWRDaGVja0JveAU5Y3RsMDgkcmlnaHRzTWFuYWdlZFJveWFsdHlGcmVlQ29udHJvbCRSb3lhbHR5RnJlZUNoZWNrQm94BThjdGwwOCRwaG90b3NJbGx1c3RyYXRpb25zQ29udHJvbCRwaG90b3NSZXBlYXRlciRjdGwwMCRjYgU4Y3RsMDgkcGhvdG9zSWxsdXN0cmF0aW9uc0NvbnRyb2wkcGhvdG9zUmVwZWF0ZXIkY3RsMDEkY2IFNmN0bDA4JGNvbG9yQmxhY2tBbmRXaGl0ZUNvbnRyb2wkY29sb3JSZXBlYXRlciRjdGwwMCRjYgU2Y3RsMDgkY29sb3JCbGFja0FuZFdoaXRlQ29udHJvbCRjb2xvclJlcGVhdGVyJGN0bDAxJGNiBThjdGwwOCRvbmx5TW9kZWxSZWxlYXNlZENvbnRyb2wkT25seU1vZGVsUmVsZWFzZWRDaGVja0JveAUhY3RsMDgkY29sbGVjdGlvbnNDb250cm9sJGNoa0FsbFJNBSFjdGwwOCRjb2xsZWN0aW9uc0NvbnRyb2wkY2hrQWxsUkYFI2N0bDA4JGNvbGxlY3Rpb25zQ29udHJvbCRjaGtBbGxOZXdzBR9jdGwwOCRjb2xsZWN0aW9uc0NvbnRyb2wkbGlzdFJNBR9jdGwwOCRjb2xsZWN0aW9uc0NvbnRyb2wkbGlzdFJGBSFjdGwwOCRjb2xsZWN0aW9uc0NvbnRyb2wkbGlzdE5ld3MFPGN0bDA4JG9yaWVudGF0aW9uQ29udHJvbCRvcmllbnRhdGlvblJlcGVhdGVyJGN0bDAwJENoZWNrYm94MgU8Y3RsMDgkb3JpZW50YXRpb25Db250cm9sJG9yaWVudGF0aW9uUmVwZWF0ZXIkY3RsMDEkQ2hlY2tib3gyBTxjdGwwOCRvcmllbnRhdGlvbkNvbnRyb2wkb3JpZW50YXRpb25SZXBlYXRlciRjdGwwMiRDaGVja2JveDIFI2N0bDA4JGNhdGVnb3JpZXNDb250cm9sJGNiU2VsZWN0QWxsBTRjdGwwOCRjYXRlZ29yaWVzQ29udHJvbCRjYXRlZ29yeVJlcGVhdGVyJGN0bDAwJGN0bDAwBTRjdGwwOCRjYXRlZ29yaWVzQ29udHJvbCRjYXRlZ29yeVJlcGVhdGVyJGN0bDAxJGN0bDAwBTRjdGwwOCRjYXRlZ29yaWVzQ29udHJvbCRjYXRlZ29yeVJlcGVhdGVyJGN0bDAyJGN0bDAwBTRjdGwwOCRjYXRlZ29yaWVzQ29udHJvbCRjYXRlZ29yeVJlcGVhdGVyJGN0bDAzJGN0bDAwBTRjdGwwOCRjYXRlZ29yaWVzQ29udHJvbCRjYXRlZ29yeVJlcGVhdGVyJGN0bDA0JGN0bDAwBTRjdGwwOCRjYXRlZ29yaWVzQ29udHJvbCRjYXRlZ29yeVJlcGVhdGVyJGN0bDA1JGN0bDAwBTRjdGwwOCRjYXRlZ29yaWVzQ29udHJvbCRjYXRlZ29yeVJlcGVhdGVyJGN0bDA2JGN0bDAwBS9jdGwwOCRpbWFnZXNNYWRlQXZhaWxhYmxlQ29udHJvbCRJblRoZUxhc3RSYWRpbwUtY3RsMDgkaW1hZ2VzTWFkZUF2YWlsYWJsZUNvbnRyb2wkQmV0d2VlblJhZGlvBS1jdGwwOCRpbWFnZXNNYWRlQXZhaWxhYmxlQ29udHJvbCRCZXR3ZWVuUmFkaW9tsyw5jn+bGWm6G1jRO6uLuD/4hQ=="" />
</div>

<script type=""text/javascript"">
<!--
var theForm = document.forms['ctl01'];
if (!theForm) {
    theForm = document.ctl01;
}
function __doPostBack(eventTarget, eventArgument) {
    if (!theForm.onsubmit || (theForm.onsubmit() != false)) {
        theForm.__EVENTTARGET.value = eventTarget;
        theForm.__EVENTARGUMENT.value = eventArgument;
        theForm.submit();
    }
}
// -->
</script>



<script src=""http://cache.corbis.com/pro/6.3/javascript/search.js"" type=""text/javascript""></script>
<script src=""http://cache.corbis.com/pro/6.3/javascript/popupHandler.js"" type=""text/javascript""></script>
<script src=""http://cache.corbis.com/pro/6.3/javascript/Corbispopup.js"" type=""text/javascript""></script><script>var vDateFormat = 'mm/dd/yyyy';</script>
<script language=""JavaScript"">
	var vRoyaltyFree = 'ctl08_rightsManagedRoyaltyFreeControl_RoyaltyFreeCheckBox';
	var vRightsManaged = 'ctl08_rightsManagedRoyaltyFreeControl_RightsManagedCheckBox';
	vRightsError = ""Please select Rights Managed, Royalty-Free or both to search."";
</script>

<script language=""JavaScript"">
	var vPeopleInImage = 'ctl08_peopleInImageControl_ddlPeople';
</script>
<script language=""javascript"" src=""/jslibrary/flashobject123.js""></script>
<script language=""javascript"">
<!--

function RampGroup_PopUp(strUrl, popupName, intHeight, intWidth)
{
	if (strUrl == null || strUrl.Length <= 0)
		return;

	var strFeatures = ""directories=no,location=no,menubar=no,center=yes,scrollbars=yes,resizable=yes,toolbar=no"";
	if (intHeight != null)
		strFeatures += "",height=""+intHeight;
	if (intWidth != null)
		strFeatures += "",width="" + intWidth;

	if (popupName == null || popupName.Length <= 0)
	{
		var theWindow = window.open( strUrl, ""PopUpWindow"", strFeatures, false );
	}
	else
	{
		var theWindow = window.open( strUrl, popupName, strFeatures, false );
	}
	theWindow.focus();
}

//-->
</script>
<table id=""TemplateTable"" cellspacing=""0"" cellpadding=""0"" border=""0"" style=""border-width:0px;width:100%;border-collapse:collapse;"">
	<tr>
		<td id=""TemplateLeftBody"">
		<span id=oHomePage></span>
		
<table width=""100%"" height=62 border=""0"" cellspacing=""0"" cellpadding=""0"" bgcolor=""#8b8e85"" style=""WIDTH: 100%"">
  <tr>
	<td width=""100%"" valign=""top"">
		<a href=""javascript:setHomePage()"" id=""header_linkLogo""><img style=""margin-top:13px;margin-left:30;"" src=""http://cache.corbis.com/pro/logo.gif"" border=0 width=98 height=27/></a><br />
    </td>
	<td valign=bottom>	
		<table cellpadding=0 border=0 cellspacing=0>
		<tr><td colspan=5><img src=""http://cache.corbis.com/pro/1clearpx.gif"" height=""18"" width=""30""></td></tr>
		
		
			<tr>
		<td nowrap>
		<span id=""header_welcomeMessage"" class=""process-navlink""></span>
				</td>
		
		<td width=""30""><img src=""http://cache.corbis.com/pro/1clearpx.gif"" height=""1"" width=""30""></td>
		<td nowrap><a href=""http://pro.corbis.com/shoppingcart/cart.aspx"" target=""_top""><img src=""http://cache.corbis.com/pro/cart_icon.gif"" border=0></a></td>
		<td width=""5""><img src=""http://cache.corbis.com/pro/1clearpx.gif"" height=""1"" width=""5""></td>
		<td nowrap ><a href=""http://pro.corbis.com/shoppingcart/cart.aspx"" Class=""process-navlink"" target=""_top""><span>Cart</span></a> 
		<a id=""header_hypCartItems"" class=""global-navlink"" href=""/shoppingcart/cart.aspx"" target=""_top""></a></td>	
					
			</tr>	
			</table>
		</td>
	<td width=""12""><img src=""http://cache.corbis.com/pro/1clearpx.gif"" height=""1"" width=""12""></td>	
	</tr>
  <tr><td colspan=3><img src=""http://cache.corbis.com/pro/1clearpx.gif"" height=""3"" width=""20""></td></tr>
</table>






<div style=""width:100%;height:18px;background-color:#8b8e85;"" >
<table border=0 width=""100%"" cellpadding=0 cellspacing=0>
<tr>
<td nowrap><img src=""http://cache.corbis.com/pro/1clearpx.gif"" border=""0"" width=""7"" height=""1""></td>
<td nowrap valign=bottom >

<table border=0 cellpadding=0 cellspacing=0>
<tr>
<td id=""sectionNav_SearchTabs_tdTab2"" class=""searchTabInactive"" nowrap=""nowrap"" onclick=""SwitchTabs('2');"" valign=""bottom""><a href=""javascript:SwitchTabs('2');"" id=""sectionNav_SearchTabs_creativeTab"" class=""searchTabInactiveText"">Search Creative</a></td>
		
<td id=""sectionNav_SearchTabs_tdTabRight2"" class=""searchTabInactive"" valign=""top"" onclick=""SwitchTabs('2');""><img src=""http://cache.corbis.com/pro/search_tab_crnr_i.gif"" id=""sectionNav_SearchTabs_imgTab2"" border=""0"" /></td>
		
<td bgcolor=""#8b8e85"" width=3 height=1><img src=""http://cache.corbis.com/pro/1clearpx.gif"" border=0 height=1 width=""3""></td>
<td id=""sectionNav_SearchTabs_tdTab3"" class=""searchTabInactive"" nowrap=""nowrap"" onclick=""SwitchTabs('3');"" valign=""bottom""><a href=""javascript:SwitchTabs('3');"" id=""sectionNav_SearchTabs_currentEventsTab"" class=""searchTabInactiveText"">Search Editorial</a></td>
		
<td id=""sectionNav_SearchTabs_tdTabRight3"" class=""searchTabInactive"" valign=""top"" onclick=""SwitchTabs('3');""><img src=""http://cache.corbis.com/pro/search_tab_crnr_i.gif"" id=""sectionNav_SearchTabs_imgTab3"" border=""0"" /></td>
		
<td bgcolor=""#8b8e85"" width=3 height=1><img src=""http://cache.corbis.com/pro/1clearpx.gif"" border=0 height=1 width=""3""></td>
<td id=""sectionNav_SearchTabs_tdTab1"" class=""searchTabActive"" nowrap=""nowrap"" onclick=""SwitchTabs('1');"" valign=""bottom""><a href=""javascript:SwitchTabs('1');"" id=""sectionNav_SearchTabs_searchAllTab"" class=""searchTabActiveText"">Search All</a></td>
		
<td id=""sectionNav_SearchTabs_tdTabRight1"" class=""searchTabActive"" valign=""top"" onclick=""SwitchTabs('1');""><img src=""http://cache.corbis.com/pro/search_tab_crnr_a.gif"" id=""sectionNav_SearchTabs_imgTab1"" border=""0"" /></td>
		
</tr>



</table>
</td>
<td align=""right"" nowrap valign=top>
	<table border=""0"" cellpadding=""0"" cellspacing=""0"">
				<tr>
					<td nowrap><a href=""http://pro.corbis.com/myfolders/myfolders.aspx"" id=""A1"" target=""_top"" class=""global-navlink"">
							<span id=""sectionNav_Label1"">My Lightboxes</span></a>
					<td width=""25""><img src=""http://cache.corbis.com/pro/1clearpx.gif"" border=""0"" height=""7"" width=""1""></td>
					<div id=""sectionNav_pnlMyOrders"">
			
					<td nowrap ><a href=""http://pro.corbis.com/myorders/orderhistory.asp"" id=""A2"" target=""_top"" class=""global-navlink"">
							<span id=""sectionNav_Label2"">My Orders</span></a></td>
					<td width=""25""><img src=""http://cache.corbis.com/pro/1clearpx.gif"" border=""0"" height=""7"" width=""1""></td>
					
		</div>
					<td nowrap><a href=""https://pro.corbis.com/myprofile/profile.aspx"" target=""_top"" class=""global-navlink""><span id=""sectionNav_lblMyProfile"">My Profile</span></a></td>
					<td width=""25""><img src=""http://cache.corbis.com/pro/1clearpx.gif"" border=""0"" height=""7"" width=""1""></td>
					<td nowrap><a href=""https://pro.corbis.com/login/loginmain.aspx"" id=""sectionNav_linkLogin"" target=""_top"" class=""global-navlink"">
							<span id=""sectionNav_lblLogin"">Login</span></a>
						<a href=""http://pro.corbis.com/default.aspx?logout=true"" target=""_top"" class=""global-navlink"">
							</a>
					</td>
					<td width=""25""><img src=""http://cache.corbis.com/pro/1clearpx.gif"" border=""0"" height=""7"" width=""1""></td>
					<td nowrap><a id=""sectionNav_linkMyRep"" class=""global-navlink"" href=""/myprofile/ContactUs.aspx"" target=""_top"">Contact Us</a></td>
					<td width=""25""><img src=""http://cache.corbis.com/pro/1clearpx.gif"" border=""0"" height=""7"" width=""1""></td>
					<td nowrap>	<a id=""sectionNav_linkHelp"" class=""global-navlink"" href=""javascript:openHelp();"">Help</a>										
					</td>
					<td width=""14""><img src=""http://cache.corbis.com/pro/1clearpx.gif"" border=""0"" height=""7"" width=""1""></td>
				</tr>
		</table>
</td>
</tr>
</table>
</div>


		
<script>
var vPanelName = 'core';
var vLanguageCode = 'en-US';
</script>
<table width=""100%"" border=""0"" cellspacing=""0"" cellpadding=""0"" bgcolor=""#E9E9E7"" style=""width: 100%;"">
<tr><td height=""5""><img src=""http://cache.corbis.com/pro/1clearpx.gif"" alt="""" height=""5"" border=""0"" width=""100%""></td></tr>
</table>
<div style=""width:100%;background-color:#E9E9E7;padding-bottom:6px"">
	<div style=""width:14px;float:left;height:45px;"">
	<img src=""http://cache.corbis.com/pro/1clearpx.gif"" width=""14"">
	</div>	
	<div class=""searchControl"" nowrap>
		
<table border=""0"" cellspacing=""0"" cellpadding=""0"">
	<tr>
		<td>
			&nbsp;
		</td>
		<td nowrap>
			<input name=""ctl08$keywordsSearchControl$KeyWordsTextBox"" type=""text"" maxlength=""100"" id=""ctl08_keywordsSearchControl_KeyWordsTextBox"" class=""textbox"" style=""width:190px;"" />
		</td>
		<td>
			&nbsp;
		</td>
		<td nowrap align=""right"">
		<input type=""submit"" name=""ctl08$keywordsSearchControl$SearchButton"" value=""Search"" onclick=""return(validateForm());"" id=""ctl08_keywordsSearchControl_SearchButton"" class=""searchButton"" />
		</td>
	</tr>
</table>

		
		<div id=""divSearchInResults"">
<script>
	var vCategoryDropDown = '';
	var vSearchInCheckBox = 'ctl08_searchInResultsCheckControl_SearchInResultsDropCheckBox';
	var vSearchInResultsControl = 'ctl08_searchInResultsCheckControl_searchInResultsDiv';
	var vIsKeywordNull = true;
	var vTextResults = 'Results';
	
</script>
<div id=""ctl08_searchInResultsCheckControl_searchInResultsDiv"" style=""display:none;"">
<input id=""ctl08_searchInResultsCheckControl_SearchInResultsDropCheckBox"" type=""checkbox"" name=""ctl08$searchInResultsCheckControl$SearchInResultsDropCheckBox"" /><label for=""ctl08_searchInResultsCheckControl_SearchInResultsDropCheckBox"">Search in Results</label>

</div>
	<input type=""hidden"" id=""hdnSearchInResults"" name=""hdnSearchInResults"" > 
	


			
		</div>
	</div>
	<div style=""width:5px;float:left;"">
	<img src=""http://cache.corbis.com/pro/1clearpx.gif"" width=""5"">
	</div>	
	
	
	<table cellspacing=0 cellpadding=0 border=0>
	<tr valign=""top"">
	<td nowrap style=""padding-right:8px;"">
<div style=""white-space: nowrap;"">
			<input id=""ctl08_rightsManagedRoyaltyFreeControl_RightsManagedCheckBox"" type=""checkbox"" name=""ctl08$rightsManagedRoyaltyFreeControl$RightsManagedCheckBox"" checked=""checked"" /><label for=""ctl08_rightsManagedRoyaltyFreeControl_RightsManagedCheckBox"">Rights Managed</label>
</div>
<div style=""white-space: nowrap;"">
			<input id=""ctl08_rightsManagedRoyaltyFreeControl_RoyaltyFreeCheckBox"" type=""checkbox"" name=""ctl08$rightsManagedRoyaltyFreeControl$RoyaltyFreeCheckBox"" checked=""checked"" /><label for=""ctl08_rightsManagedRoyaltyFreeControl_RoyaltyFreeCheckBox"">Royalty-Free</label>
</div>		
</td>
	<td width=1><div class=""searchDivider""><img src=""http://cache.corbis.com/pro/1clearpx.gif"" width=""1""></div></td>
	<td nowrap style=""padding-right:8px;padding-left:5px;"">
		<div>
				<input id=""ctl08_photosIllustrationsControl_photosRepeater_ctl00_cb"" type=""checkbox"" name=""ctl08$photosIllustrationsControl$photosRepeater$ctl00$cb"" checked=""checked"" /><label for=""ctl08_photosIllustrationsControl_photosRepeater_ctl00_cb"">Photography</label>
		</div>
	
		<div>
				<input id=""ctl08_photosIllustrationsControl_photosRepeater_ctl01_cb"" type=""checkbox"" name=""ctl08$photosIllustrationsControl$photosRepeater$ctl01$cb"" checked=""checked"" /><label for=""ctl08_photosIllustrationsControl_photosRepeater_ctl01_cb"">Illustration</label>
		</div>
	


		
</td>
	<td width=1><div class=""searchDivider""><img src=""http://cache.corbis.com/pro/1clearpx.gif"" width=""1""></div></td>
	<td nowrap style=""padding-right:8px;padding-left:5px;"">
			<div>
					<input id=""ctl08_colorBlackAndWhiteControl_colorRepeater_ctl00_cb"" type=""checkbox"" name=""ctl08$colorBlackAndWhiteControl$colorRepeater$ctl00$cb"" checked=""checked"" /><label for=""ctl08_colorBlackAndWhiteControl_colorRepeater_ctl00_cb"">Color</label>
			</div>
		
			<div>
					<input id=""ctl08_colorBlackAndWhiteControl_colorRepeater_ctl01_cb"" type=""checkbox"" name=""ctl08$colorBlackAndWhiteControl$colorRepeater$ctl01$cb"" checked=""checked"" /><label for=""ctl08_colorBlackAndWhiteControl_colorRepeater_ctl01_cb"">Black &amp; White</label>
			</div>
		
		
</td>
	<td width=1><div class=""searchDivider""><img src=""http://cache.corbis.com/pro/1clearpx.gif"" width=""1""></div></td>
	<td rowspan=2 nowrap valign=top style=""padding-right:8px;padding-left:5px;"">
		<input id=""ctl08_onlyModelReleasedControl_OnlyModelReleasedCheckBox"" type=""checkbox"" name=""ctl08$onlyModelReleasedControl$OnlyModelReleasedCheckBox"" /><label for=""ctl08_onlyModelReleasedControl_OnlyModelReleasedCheckBox"">Only Model-Released</label>	
	</td>
	<td rowspan=2 align=right valign=bottom width=""100%"">
<script>
	var vTurnOn = 'Advanced Search (Turn On)';
	var vTurnOff = 'Advanced Search (Turn Off)';

	var vCollTurnOn = 'Collections Search (Turn On)';
	var vCollTurnOff = 'Collections Search (Turn Off)';
	
	function doMSO()
	{
		layout();
		toggleAdvancedSearch();
	}
	
	function doMSOCollections()
	{
    	toggleCollectionSearch(2);
	}
</script>
<table bgcolor=""#e9eae7"" border=""0"" cellspacing=""0"" cellpadding=""0"">
<tr>
<td >
	<table bgcolor=""#e9eae7"" border=""0"" cellspacing=""0"" cellpadding=""0"">
	<tr>
	<td id=""ctl08_moreSearchOptions_tdCollections"" bgcolor=""#e9eae7"" align=""right"">		
	<b><a href=""javascript:doMSOCollections()"" id=""linkTurnOnOffCollections"" style=""cursor:hand;text-decoration:underline;"">Collections Search (Turn On)</a></b>
			 							
	</td>
		
	</tr>
	<tr>
	<td bgcolor=""#e9eae7"" align=""right"" >		
	<b><a href=""javascript:doMSO()"" id=""linkTurnOnOff"" style=""cursor:hand;text-decoration:underline;"">Advanced Search (Turn On)</a></b>
			 							
	</td>
	</tr>
	<tr>
	<td bgcolor=""#e9eae7"" nowrap align=""right"">
	<a id=""ctl08_moreSearchOptions_linkSearchTips"" href=""javascript:popupHelp();"" style=""text-decoration:underline;CURSOR:hand"">Search Tips</a>
	</td>
	</tr>
	</table>
	
</td>
</tr>
</table>


		</td>	
	<td width=""10"" rowspan=2 nowrap><img src=""http://cache.corbis.com/pro/1clearpx.gif"" alt="""" width=""10"" height=5 border=""0""></td>		
	</tr>
	
	</table>	
	
</div>
<table cellpadding=0 border=0 cellspacing=0 width=""100%"">
<tr>
<td height=7 width=""100%""><img src=""http://cache.corbis.com/pro/searchbar_dropshadow.gif"" width=""100%"" height=7 border=0></td>
</tr>
</table>
<div id=""CollectionsDiv"" style=""display:none"">
    <table width=""100%"" border=""0"" cellspacing=""0"" cellpadding=""0"" bgcolor=""#E9E9E7"">
    <tr>
    	<td width=""100%""><img src=""http://cache.corbis.com/pro/1clearpx.gif"" alt="""" width=""100%"" height=5 border=""0""></td>
    </tr>	
    </table>
    <table id=""Table1"" width=""100%"" border=""0"" cellspacing=""0"" cellpadding=""0"" bgcolor=""#E9E9E7"">
    <tr>
        <td>
	
<table border=""0"" cellspacing=""0"" cellpadding=""0"" width=""100%""> 
	<tr valign=""top"">		
		<td style=""padding-left:15px; width: 150; white-space: nowrap"" nowrap>
		    <span class=""checkbox"" style=""white-space: nowrap;""><input id=""ctl08_collectionsControl_chkAllRM"" type=""checkbox"" name=""ctl08$collectionsControl$chkAllRM"" checked=""checked"" onclick=""SelectAllCollections(2);"" /><label for=""ctl08_collectionsControl_chkAllRM"">All Rights Managed</label></span>
		</td>
		
		<td style=""padding-left:30px; width: 150; white-space: nowrap"" nowrap> 
		    <span class=""checkbox"" style=""white-space: nowrap;""><input id=""ctl08_collectionsControl_chkAllRF"" type=""checkbox"" name=""ctl08$collectionsControl$chkAllRF"" checked=""checked"" onclick=""SelectAllCollections(1);"" /><label for=""ctl08_collectionsControl_chkAllRF"">All Royalty-Free</label></span>
	    </td>

		<td style=""padding-left:30px; width: 150; white-space: nowrap"" nowrap> 
		    <span class=""checkbox"" style=""white-space: nowrap;""><input id=""ctl08_collectionsControl_chkAllNews"" type=""checkbox"" name=""ctl08$collectionsControl$chkAllNews"" checked=""checked"" onclick=""SelectAllCollections(0);"" /><label for=""ctl08_collectionsControl_chkAllNews"">All News</label></span>
	    </td>
	    
	    <td align=""right"" width=""100%"" style=""padding-right:12px"" rowspan=""2"">
	        <a href=""javascript:doMSOCollections()"" style=""cursor:hand;text-decoration:underline;"">Collections Search (Turn Off)</a>
	        <div style=""padding-top:5px;""><span id=""ctl08_collectionsControl_lblHelpText"">To select more than one collection, hold down the ""ctrl"" key.</span></div>
	    </td>
	</tr>
	<tr valign=""top"">
		<td style=""padding-left:15px; width: 150;""><select size=""10"" name=""ctl08$collectionsControl$listRM"" multiple=""multiple"" id=""ctl08_collectionsControl_listRM"" onchange=""CheckCollection(2);"" style=""min-width:150px;"">
			<option selected=""selected"" value=""55"">Andy Warhol Foundation</option>
			<option selected=""selected"" value=""79"">Atlantide Phototravel</option>
			<option selected=""selected"" value=""80"">BBC</option>
			<option selected=""selected"" value=""81"">Beateworks</option>
			<option selected=""selected"" value=""1"">Bettmann/UPI</option>
			<option selected=""selected"" value=""82"">Bloomimage (RM)</option>
			<option selected=""selected"" value=""83"">CaescoPictures (Formerly Archivo Iconographico)</option>
			<option selected=""selected"" value=""84"">Christie's Images</option>
			<option selected=""selected"" value=""41"">Cond&#233; Nast</option>
			<option selected=""selected"" value=""85"">Creasource</option>
			<option selected=""selected"" value=""86"">Dorling Kindersley</option>
			<option selected=""selected"" value=""87"">Envision</option>
			<option selected=""selected"" value=""88"">Fine Art Photographic Library</option>
			<option selected=""selected"" value=""89"">Fratelli Alinari</option>
			<option selected=""selected"" value=""90"">Hulton-Deutsch</option>
			<option selected=""selected"" value=""91"">Image Point FR</option>
			<option selected=""selected"" value=""92"">Images.com (RM)</option>
			<option selected=""selected"" value=""93"">Marvel Characters</option>
			<option selected=""selected"" value=""94"">MGM</option>
			<option selected=""selected"" value=""95"">Michael Ochs Archives</option>
			<option selected=""selected"" value=""97"">PhotoCuisine</option>
			<option selected=""selected"" value=""99"">Robert Harding World Imagery</option>
			<option selected=""selected"" value=""100"">Schlegelmilch Photography</option>
			<option selected=""selected"" value=""102"">Swim Ink</option>
			<option selected=""selected"" value=""103"">Visuals Unlimited</option>
			<option selected=""selected"" value=""58"">zefa</option>

		</select></td>
		<td style=""padding-left:30px; width: 150;""><select size=""10"" name=""ctl08$collectionsControl$listRF"" multiple=""multiple"" id=""ctl08_collectionsControl_listRF"" onchange=""CheckCollection(1);"" style=""min-width:150px;"">
			<option selected=""selected"" value=""60"">Blend Images</option>
			<option selected=""selected"" value=""61"">Bloomimage (RF)</option>
			<option selected=""selected"" value=""62"">Brand X Pictures</option>
			<option selected=""selected"" value=""63"">Comstock</option>
			<option selected=""selected"" value=""76"">Corbis RF</option>
			<option selected=""selected"" value=""65"">Design Pics</option>
			<option selected=""selected"" value=""64"">Dex Image</option>
			<option selected=""selected"" value=""66"">Goodshoot</option>
			<option selected=""selected"" value=""43"">Image Source</option>
			<option selected=""selected"" value=""67"">image100</option>
			<option selected=""selected"" value=""68"">Images.com (RF)</option>
			<option selected=""selected"" value=""69"">Imageshop</option>
			<option selected=""selected"" value=""78"">InsideOutPix</option>
			<option selected=""selected"" value=""70"">MedioImages</option>
			<option selected=""selected"" value=""77"">Mike Watson Images</option>
			<option selected=""selected"" value=""71"">Pixland</option>
			<option selected=""selected"" value=""72"">Stockbyte/Stockdisc</option>
			<option selected=""selected"" value=""73"">Tetra Images</option>
			<option selected=""selected"" value=""74"">Thinkstock</option>
			<option selected=""selected"" value=""75"">zefa RF</option>

		</select></td>
		<td style=""padding-left:30px; width: 150;""><select size=""10"" name=""ctl08$collectionsControl$listNews"" multiple=""multiple"" id=""ctl08_collectionsControl_listNews"" onchange=""CheckCollection(0);"" style=""min-width:150px;"">
			<option selected=""selected"" value=""59"">epa</option>
			<option selected=""selected"" value=""57"">NewSport</option>
			<option selected=""selected"" value=""10"">Reuters</option>
			<option selected=""selected"" value=""33"">Saba</option>
			<option selected=""selected"" value=""25"">Sygma</option>
			<option selected=""selected"" value=""56"">Zuma</option>

		</select></td>	
	</tr>
</table>


        </td>
       </tr>
    </table>
	<table width=""100%"" border=""0"" cellspacing=""0"" cellpadding=""0"" bgcolor=""#E9E9E7"">
        <tr>
        	<td width=""100%""><img src=""http://cache.corbis.com/pro/1clearpx.gif"" alt="""" width=""100%"" height=5 border=""0""></td>
        </tr>
    </table>
    <table cellpadding=0 border=0 cellspacing=0 width=""100%"">
        <tr>
            <td height=7 width=""100%""><img src=""http://cache.corbis.com/pro/searchbar_dropshadow.gif"" width=""100%"" height=7 border=0></td>
        </tr>
    </table>


</div>

<div id=""AdvancedSearchDiv"" style=""display:none"">
<table width=""100%"" border=""0"" cellspacing=""0"" cellpadding=""0"" bgcolor=""#E9E9E7"">
<tr>
	<td width=""100%""><img src=""http://cache.corbis.com/pro/1clearpx.gif"" alt="""" width=""100%"" height=5 border=""0""></td>
</tr>	
</table>
<table id=""AdvancedSearchTable"" width=""100%"" border=""0"" cellspacing=""0"" cellpadding=""0"" bgcolor=""#E9E9E7"">
<tr valign=""top"">
	<td width=""14""><img src=""http://cache.corbis.com/pro/1clearpx.gif"" alt="""" width=""14"" border=""0""></td>
	<td nowrap valign=""top"">
		
<table bgcolor=""#e9eae7"" border=""0"" cellspacing=""0"" cellpadding=""0"">
	<tr>
		<td valign=""top"">
			<table bgcolor=""#e9eae7"" border=""0"" cellspacing=""0"" cellpadding=""0"">
				<tr>
					<td bgcolor=""#e9eae7"" nowrap>
						<b>
							<span id=""ctl08_orientationControl_OrientationLabel"">Orientation</span>:</b>
					</td>
				</tr>
				
						<tr>
							<td bgcolor=""#e9eae7"" nowrap>
								<span NAME=""Checkbox1""><input id=""ctl08_orientationControl_orientationRepeater_ctl00_Checkbox2"" type=""checkbox"" name=""ctl08$orientationControl$orientationRepeater$ctl00$Checkbox2"" checked=""checked"" /><label for=""ctl08_orientationControl_orientationRepeater_ctl00_Checkbox2"">Horizontal</label></span>
							</td>
						</tr>
					
						<tr>
							<td bgcolor=""#e9eae7"" nowrap>
								<span NAME=""Checkbox1""><input id=""ctl08_orientationControl_orientationRepeater_ctl01_Checkbox2"" type=""checkbox"" name=""ctl08$orientationControl$orientationRepeater$ctl01$Checkbox2"" checked=""checked"" /><label for=""ctl08_orientationControl_orientationRepeater_ctl01_Checkbox2"">Vertical</label></span>
							</td>
						</tr>
					
						<tr>
							<td bgcolor=""#e9eae7"" nowrap>
								<span NAME=""Checkbox1""><input id=""ctl08_orientationControl_orientationRepeater_ctl02_Checkbox2"" type=""checkbox"" name=""ctl08$orientationControl$orientationRepeater$ctl02$Checkbox2"" checked=""checked"" /><label for=""ctl08_orientationControl_orientationRepeater_ctl02_Checkbox2"">Panorama</label></span>
							</td>
						</tr>
					
			</table>
		</td>
	</tr>
</table>

	</td>
	<td width=""8""><img src=""http://cache.corbis.com/pro/1clearpx.gif"" alt="""" width=""10"" border=""0""></td>
	<td width=""1"" style=""width: 1px;"" bgcolor=""#999999""><img src=""http://cache.corbis.com/pro/1clearpx.gif"" width=""1""></td>
	<td width=""8""><img src=""http://cache.corbis.com/pro/1clearpx.gif"" alt="""" width=""10"" border=""0""></td>	
	<td nowrap valign=""top"">
		

<b><span id=""ctl08_categoriesControl_CategoriesLabel"">Categories</span>:</b>
<br>
<input id=""ctl08_categoriesControl_cbSelectAll"" type=""checkbox"" name=""ctl08$categoriesControl$cbSelectAll"" checked=""checked"" onclick=""SelectAllCategories();"" /><label for=""ctl08_categoriesControl_cbSelectAll"">Search All</label>
<br>

							<input id=""ctl08_categoriesControl_categoryRepeater_ctl00_ctl00"" type=""checkbox"" name=""ctl08$categoriesControl$categoryRepeater$ctl00$ctl00"" checked=""checked"" onclick=""CheckCategories();"" /><label for=""ctl08_categoriesControl_categoryRepeater_ctl00_ctl00"">Commercial</label>
			<br>
		
							<input id=""ctl08_categoriesControl_categoryRepeater_ctl01_ctl00"" type=""checkbox"" name=""ctl08$categoriesControl$categoryRepeater$ctl01$ctl00"" checked=""checked"" onclick=""CheckCategories();"" /><label for=""ctl08_categoriesControl_categoryRepeater_ctl01_ctl00"">Editorial</label>
			<br>
		
							<input id=""ctl08_categoriesControl_categoryRepeater_ctl02_ctl00"" type=""checkbox"" name=""ctl08$categoriesControl$categoryRepeater$ctl02$ctl00"" checked=""checked"" onclick=""CheckCategories();"" /><label for=""ctl08_categoriesControl_categoryRepeater_ctl02_ctl00"">Historical</label>
			<br>
		
							<input id=""ctl08_categoriesControl_categoryRepeater_ctl03_ctl00"" type=""checkbox"" name=""ctl08$categoriesControl$categoryRepeater$ctl03$ctl00"" checked=""checked"" onclick=""CheckCategories();"" /><label for=""ctl08_categoriesControl_categoryRepeater_ctl03_ctl00"">Art</label>
			<br>
		
							<input id=""ctl08_categoriesControl_categoryRepeater_ctl04_ctl00"" type=""checkbox"" name=""ctl08$categoriesControl$categoryRepeater$ctl04$ctl00"" checked=""checked"" onclick=""CheckCategories();"" /><label for=""ctl08_categoriesControl_categoryRepeater_ctl04_ctl00"">News</label>
			<br>
		
							<input id=""ctl08_categoriesControl_categoryRepeater_ctl05_ctl00"" type=""checkbox"" name=""ctl08$categoriesControl$categoryRepeater$ctl05$ctl00"" checked=""checked"" onclick=""CheckCategories();"" /><label for=""ctl08_categoriesControl_categoryRepeater_ctl05_ctl00"">Sports</label>
			<br>
		
							<input id=""ctl08_categoriesControl_categoryRepeater_ctl06_ctl00"" type=""checkbox"" name=""ctl08$categoriesControl$categoryRepeater$ctl06$ctl00"" checked=""checked"" onclick=""CheckCategories();"" /><label for=""ctl08_categoriesControl_categoryRepeater_ctl06_ctl00"">Entertainment</label>
			<br>
		



	</td>
	<td width=""8""><img src=""http://cache.corbis.com/pro/1clearpx.gif"" alt="""" width=""10"" border=""0""></td>
	<td width=""1"" style=""width: 1px;"" bgcolor=""#999999"" ><img src=""http://cache.corbis.com/pro/1clearpx.gif"" width=""1""></td>
	<td width=""8""><img src=""http://cache.corbis.com/pro/1clearpx.gif"" alt="""" width=""10"" border=""0""></td>
	<td nowrap valign=""top"">
		
<table bgcolor=""#e9eae7"" border=""0"" cellspacing=""0"" cellpadding=""0"">
	<tr>
		<td>
			<table bgcolor=""#e9eae7"" border=""0"" cellspacing=""0"" cellpadding=""0"">
				<tr>
					<td bgcolor=""#e9eae7"" nowrap>
						<b>
						<span id=""ctl08_imagesMadeAvailableControl_ImagesMadeAvailableLabel"">Images Made Available</span>:</b>
					</td>
				</tr>
				<tr>
					<td bgcolor=""#e9eae7"" nowrap>
						<input id=""ctl08_imagesMadeAvailableControl_InTheLastRadio"" type=""radio"" name=""ctl08$imagesMadeAvailableControl$And"" value=""InTheLastRadio"" checked=""checked"" /><label for=""ctl08_imagesMadeAvailableControl_InTheLastRadio"">In The Last</label>
												
						<input name=""ctl08$imagesMadeAvailableControl$InTheLastTextBox"" type=""text"" maxlength=""3"" id=""ctl08_imagesMadeAvailableControl_InTheLastTextBox"" class=""textbox"" style=""width:50px;"" />
						<span id=""ctl08_imagesMadeAvailableControl_DaysLabel"">days</span>
					</td>
					<td>
						&nbsp;
					</td>
					<td bgcolor=""#e9eae7"" nowrap>
						&nbsp;
					</td>
				</tr>
				<tr valign=""top"">
					<td>
						<table border=""0"" cellspacing=""0"" cellpadding=""0"">
							<tr>
								<td bgcolor=""#e9eae7"" nowrap>
									<input id=""ctl08_imagesMadeAvailableControl_BetweenRadio"" type=""radio"" name=""ctl08$imagesMadeAvailableControl$And"" value=""BetweenRadio"" /><label for=""ctl08_imagesMadeAvailableControl_BetweenRadio"">Between</label>
								</td>
								<td>&nbsp;</td>
								<td align=""right"">
									<input name=""ctl08$imagesMadeAvailableControl$BetweenTextBox"" type=""text"" value=""mm/dd/yyyy"" id=""ctl08_imagesMadeAvailableControl_BetweenTextBox"" class=""textbox"" onkeypress=""selectBetween();"" style=""width:75px;"" />
								</td>
							</tr>
							<tr>
								<td align=""right"">
									<span id=""ctl08_imagesMadeAvailableControl_AndLabel"" style=""display:inline-block;width:50px;"">and</span>
								</td>							
								<td>&nbsp;</td>
								<td align=""right"">
								<input name=""ctl08$imagesMadeAvailableControl$AndTextbox"" type=""text"" value=""mm/dd/yyyy"" id=""ctl08_imagesMadeAvailableControl_AndTextbox"" class=""textbox"" onkeypress=""selectBetween();"" style=""width:75px;"" />
							</td>
						</tr>
					</table>
					</td>
					<td bgcolor=""#e9eae7"" nowrap>
						&nbsp;
					</td>
				</tr>
			</table>
		</td>
	</tr>
</table>

		<br>
		
<table>
	<tr>
		<td nowrap><b>
			<span id=""ctl08_datePhotographedOrCreatedControl_DatePhotographedOrCreatedLabel"">Date Photographed Or Created</span>:</b>
		</td>
	</tr>
	<tr>
		<td>
			<input name=""ctl08$datePhotographedOrCreatedControl$DatePhotographedOrCreatedTextBox"" type=""text"" maxlength=""255"" id=""ctl08_datePhotographedOrCreatedControl_DatePhotographedOrCreatedTextBox"" class=""textbox"" style=""width:170px;"" />
		</td>
	</tr>
	<tr>
		<td nowrap>
			<span id=""ctl08_datePhotographedOrCreatedControl_BottomTextLabel"">
            
            
            
            
            (e.g. 1935-1941, mid 1980s,
            
            
            
            
            <br />
            
            
            
            
             02/28/2002, 12th Century)
         
         
         
         
         </span>
		</td>
	</tr>
	
</table>

		<br>
		<span id=""ctl08_locationControl_LocationLabel"" style=""font-weight:bold;"">Location</span>:
<br>
<input name=""ctl08$locationControl$LocationTextBox"" type=""text"" maxlength=""50"" id=""ctl08_locationControl_LocationTextBox"" class=""textbox"" style=""width:170px;"" />
					

	</td>
	<td width=""8""><img src=""http://cache.corbis.com/pro/1clearpx.gif"" alt="""" width=""10"" border=""0""></td>
	<td width=""1"" style=""width: 1px;"" bgcolor=""#999999""><img src=""http://cache.corbis.com/pro/1clearpx.gif"" width=""1""></td>
	<td width=""8""><img src=""http://cache.corbis.com/pro/1clearpx.gif"" alt="""" width=""10"" border=""0""></td>
	<td nowrap valign=""top"">
		<table>
			<tr>
				<td height=""3""><img src=""http://cache.corbis.com/pro/1clearpx.gif"" alt="""" width=""1"" height=""3"" border=""0""></td>
			</tr>
			<tr>
				<td>
					
<b><span id=""ctl08_searchImagesImageSetsControl_SearchForLabel"">Search For</span>:</b>
<br>
<select name=""ctl08$searchImagesImageSetsControl$ddlSearchFor"" id=""ctl08_searchImagesImageSetsControl_ddlSearchFor"" style=""width:170px;"">
			<option selected=""selected"" value="""">Images</option>
			<option value=""sets"">Image Sets</option>

		</select>

		

				</td>
			</tr>
			<tr>
				<td height=""3""><img src=""http://cache.corbis.com/pro/1clearpx.gif"" alt="""" width=""1"" height=""3"" border=""0""></td>
			</tr>
			<tr>
				<td>
					
<table cellpadding=""0"" cellspacing=""0"">
	<tr>
		<td>
			<b><span id=""ctl08_photographerNameControl_PhotographersNameLabel"">Photographer Name</span>:</b>
		</td>
	</tr>
	<tr>
		<td>
			<input name=""ctl08$photographerNameControl$PhotographerNameTextBox"" type=""text"" maxlength=""50"" id=""ctl08_photographerNameControl_PhotographerNameTextBox"" class=""textbox"" style=""width:170px;"" />
		</td>
	</tr>
</table>
					


				</td>
			</tr>
			<tr>
				<td height=""3""><img src=""http://cache.corbis.com/pro/1clearpx.gif"" alt="""" width=""1"" height=""3"" border=""0""></td>
			</tr>
			<tr>
				<td>
					
<b><span id=""ctl08_imageNumbersControl_ImageNumbersLabel"">Image Numbers</span>:</b>
<br>
<textarea name=""ctl08$imageNumbersControl$ImageNumbersTextBox"" rows=""4"" cols=""20"" id=""ctl08_imageNumbersControl_ImageNumbersTextBox"" class=""textarea"" style=""width:170px;""></textarea>
					
				</td>
			</tr>
			<tr>
    			<td>
					<span id=""ctl08_specifySourceControl_SpecifySourceLabel"" style=""font-weight:bold;"">Source</span>:
		
	<br>
			<input name=""ctl08$specifySourceControl$SpecifySourceTextBox"" type=""text"" maxlength=""50"" id=""ctl08_specifySourceControl_SpecifySourceTextBox"" class=""textbox"" style=""width:170px;"" />
	
					

				</td>
			</tr>
			<tr>
				<td nowrap colspan=""2"">
					
				</td>
			</tr>
		</table>
	</td>
	
	<td width=""8""><img src=""http://cache.corbis.com/pro/1clearpx.gif"" alt="""" width=""10"" border=""0""></td>
	<td width=""1"" style=""width: 1px;"" bgcolor=""#999999""><img src=""http://cache.corbis.com/pro/1clearpx.gif"" width=""1""></td>
	<td width=""8""><img src=""http://cache.corbis.com/pro/1clearpx.gif"" alt="""" width=""10"" border=""0""></td>
	<td>
		<table>
			<tr>
				<td>
					
<table bgcolor=""#e9eae7"" border=""0"" cellspacing=""0"" cellpadding=""0"">
	<tr>
		<td bgcolor=""#e9eae7"" nowrap>
			<b>
			<span id=""ctl08_pointOfViewControl_PointOfViewLabel"">Point of View</span>:</b>
		</td>
	</tr>
	<tr>
		<td bgcolor=""#e9eae7"" nowrap>
			<select name=""ctl08$pointOfViewControl$ddlPointOfView"" id=""ctl08_pointOfViewControl_ddlPointOfView"" style=""width:180px;"">
			<option value="""">All</option>
			<option value=""1"">Aerial</option>
			<option value=""2"">Close-up</option>
			<option value=""5"">Above</option>
			<option value=""6"">Below</option>
			<option value=""7"">From Space</option>

		</select>
		</td>
	</tr>
</table>

				</td>
			</tr>
			<tr>
				<td height=""10""><img src=""http://cache.corbis.com/pro/1clearpx.gif"" alt="""" width=""1"" height=""10"" border=""0""></td>
			</tr>
			<tr>
				<td>
					
<b><span id=""ctl08_peopleInImageControl_PeopleInImageLabel"">People In Image</span>:</b>
<br>
<select name=""ctl08$peopleInImageControl$ddlPeople"" id=""ctl08_peopleInImageControl_ddlPeople"" style=""width:180px;"">
			<option value="""">With or Without People</option>
			<option value=""5"">With People</option>
			<option value=""6"">Without People</option>
			<option value=""1"">1 Person Only</option>
			<option value=""2"">2 People Only</option>
			<option value=""3"">3-5 People</option>
			<option value=""4"">Groups or Crowds</option>

		</select>

				</td>
			</tr>
			<tr>
				<td height=""10""><img src=""http://cache.corbis.com/pro/1clearpx.gif"" alt="""" width=""1"" height=""10"" border=""0""></td>
			</tr>
			<tr>
				<td>
					
<b><span id=""ctl08_immediateAvailabilityControl_ImmediateAvaliabilityLabel"">Immediate Availability</span>:</b>
<br><select name=""ctl08$immediateAvailabilityControl$ddlImmediateAvailability"" id=""ctl08_immediateAvailabilityControl_ddlImmediateAvailability"" style=""width:180px;"">
			<option value="""">All Resolutions</option>
			<option value=""67"">Low</option>
			<option value=""64"">Medium</option>
			<option value=""561"">High</option>
			<option value=""559"">Ultra</option>

		</select>
<br><span id=""ctl08_immediateAvailabilityControl_RMOnlyLabel"" style=""display:inline-block;width:180px;"">RM only. All RF Images are immediately available.</span>

				</td>
			</tr>
			<tr>
				<td>
					&nbsp;
				</td>
			</tr>
			<tr>
				<td>
					&nbsp;
				</td>
			</tr>
	    </table>
	</td>
    <td width=""100%"" align=""right"">
		
<table bgcolor=""#e9eae7"" border=""0"" cellspacing=""0"" cellpadding=""0"">
	<tr>
		<td valign=""top"" style=""padding-right:12px;"">
			<table bgcolor=""#e9eae7"" border=""0"" cellspacing=""0"" cellpadding=""0"">
			    <tr>
                    <td align=""right"">
			            <a href=""javascript:doMSO()"" style=""cursor:hand;text-decoration:underline;"">Advanced Search (Turn Off)</a>
        	        </td>
			    </tr>
				<tr>
					<td align=""right"" style=""padding-top:5px;"">
						<a href=""javascript:resetFilters();"">Reset Filters</a>
					</td>
				</tr>
				<tr>
					<td align=""right"" style=""padding-top:5px;"">
						<a id=""ctl08_resetFiltersControl_chlSearchAssistance"" href=""javascript:RampGroup_PopUp('/popup/SearchAssistance.aspx','',230,480)"">Search Assistance Settings</a>
					</td>
				</tr>
				
			</table>
		</td>
	</tr>
</table>


    </td>
</tr>
</table>
<table width=""100%"" border=""0"" cellspacing=""0"" cellpadding=""0"" bgcolor=""#E9E9E7"">
    <tr>
	<td width=""100%""><img src=""http://cache.corbis.com/pro/1clearpx.gif"" alt="""" width=""100%"" height=5 border=""0""></td>
	</tr>
</table>
<table cellpadding=0 border=0 cellspacing=0 width=""100%"">
<tr>
<td height=7 width=""100%""><img src=""http://cache.corbis.com/pro/searchbar_dropshadow.gif"" width=""100%"" height=7 border=0></td>
</tr>
</table>
</div>
		

			
<div class=""HomePageContent""><div id=""topbannerflash""></div><script type=""text/javascript"">
			var fo = new FlashObject(""creative/content/homepageflash/HomePageAll.swf?lang=en-US"", ""topbanner"", ""995"", ""440"", 6, ""ffffff"");
			
			var NoFlashImages = new Array();
			var NoFlashImageNumber = 0;
			
			var playerVersionArray = new Array();
			playerVersionArray[0] = 6;
			var goodPlayerVersion = new com.deconcept.PlayerVersion(playerVersionArray);
			if(com.deconcept.FlashObjectUtil.getPlayerVersion().versionIsValid(goodPlayerVersion)) 
			{
				fo.write(""topbannerflash"");
			} 
			else 
			{
				var noContent = '<table border=""0"" cellpadding=""0"" cellspacing=""0""><tr><td valign=""top"" bgcolor=""#333333"" style=""font: 12px Verdana; color: white; margin: 0 0 12px 0;"" height=""300"" width=""926"" colspan=""5""><br/><br/>&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;<img src=""http://cache.corbis.com/pro/en-US/hdr.gif"" border=""0""/><p/>&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;Please ensure you have the <a href=""http://www.adobe.com"" style=""color: #46cce3;"">latest version</a> installed and enabled on your computer.<br/>&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;<a href=""http://www.adobe.com"" style=""color: #46cce3;"">http://www.adobe.com</a></td></tr></table>';
				if(noContent.length > 1)
				{
					document.getElementById(""topbannerflash"").innerHTML = noContent;
				}
				else
				{
					document.getElementById(""topbannerflash"").innerHTML = NoFlashImages[0];
					if (NoFlashImages.length > 1) 
					{
						setTimeout(""FlopImages()"", 000);
					}
				}
			}
			
			function FlopImages() {
				NoFlashImageNumber++;
				if (NoFlashImageNumber > NoFlashImages.length-1) {
					NoFlashImageNumber = 0;
				}
				document.getElementById(""topbannerflash"").innerHTML = NoFlashImages[NoFlashImageNumber];
				setTimeout(""FlopImages()"", 000);
			}
		</script></div>
														
<table width=""970"" cellpadding=""0"" border=""0"" cellspacing=""0"" style=""margin-left: 30px; margin-bottom: 14px;"">
	<tr>
	    <td colspan=""2""><hr></td>
	</tr>
  
	<tr>
	    <td valign=""top"">
			<table>
				<tr>
					<td valign=middle nowrap>
						<script language=""javascript"" SRC=""http://cache.corbis.com/pro/onlineopinion/onlineopinion_unc.js""></script>
						<span id=""bod1_lblHelpUsImprove""><span class=""RegularText"" style=""PADDING-BOTTOM:10px""><b>Help us improve our site</b></span></span>
					</td>
					<td valign=""middle""><a href=""javascript:O_LC();""><img src=""http://cache.corbis.com/pro/onlineopinion/content/en-US/plus.gif"" border=""0""/></a></td>
				</tr>
			</table>
		</td>
	    <td valign=""top"" align=""right""><span id=""bod1_lblChooseLanguage"">Choose Language</span>: <select name=""bod1$language$ddlLanguages"" onchange=""javascript:setTimeout('__doPostBack(\'bod1$language$ddlLanguages\',\'\')', 0)"" id=""bod1_language_ddlLanguages"">
			<option selected=""selected"" value=""en-US"">English (United States)</option>
			<option value=""zh-CHS"">Chinese (Simplified)</option>
			<option value=""nl-NL"">Dutch</option>
			<option value=""en-GB"">English (United Kingdom)</option>
			<option value=""fr-FR"">French</option>
			<option value=""de-DE"">German</option>
			<option value=""it-IT"">Italian</option>
			<option value=""ja-JP"">Japanese</option>
			<option value=""pl-PL"">Polish</option>
			<option value=""pt-BR"">Portuguese (Brazil)</option>
			<option value=""es-ES"">Spanish</option>

		</select>
</td>
	</tr>
</table>
<table width=""500"" cellpadding=""0"" border=""0"" cellspacing=""0"" style=""width:500px; margin-left: 30px;"">
	<tr>
		<td valign=""top"">
			<a href=http://www.corbismotion.com?linkid=150000 target=_blank>Corbis Motion</a>&nbsp; <a href=http://decor.corbis.com/default.aspx?linkid=15000 target=_blank>Décor</a>&nbsp; <a href=http://education.corbis.com/default.aspx?linkid=15000 target=_blank>Educational Use</a>&nbsp; <a href=http://mobile.corbis.com/default.aspx?linkid=150000 target=_blank>Mobile</a>&nbsp; <a href=/creative/services/catalogs?linkid=150000>Catalogs</a>&nbsp; <a href=http://www.corbis.com/corporate/pressroom/default.asp?linkid=150000>Pressroom</a>&nbsp; <a href=http://www.corbis.com/corporate/Employment/Employment.asp?linkid=150000>Employment</a>&nbsp; 
			<a id=""bod1_linkUseSite"" class=""bottomnavlink"" href=""javascript:newwindow('/creative/common/terms.asp')"">Site Usage Agreement</a>&nbsp;  <a id=""bod1_linkPrivacy"" class=""bottomnavlink"" href=""javascript:newwindow('/creative/common/privacy.asp')"">New Privacy Policy - Please Read</a>&nbsp; <a id=""bod1_linkLicense"" class=""bottomnavlink"" href=""javascript:openHelp('/help/default.aspx?tab=content&amp;pg=/Legal_Information_and_Policies/Image_Licensing/About_Image_Licensing.htm')""></a> <a id=""bod1_linkLicenseTerms"" class=""bottomnavlink"" href=""/creative/terms"">Licensing Terms and Conditions</a>&nbsp; <a id=""bod1_linkContactUs"" class=""bottomnavlink"" href=""/myprofile/ContactUs.aspx"">Contact Us</a>&nbsp; <a id=""bod1_linkAbout"" class=""bottomnavlink"" href=""http://www.corbis.com/corporate/overview/overview.asp"">About Corbis</a>&nbsp; 
		</td>
	</tr>
</table>
<table width=""970"" style=""width:970px; margin-left: 30px;"">
	<tr>
		<td>
			<br><span id=""bod1_lblAllRightsReserved"">© 2001-2008 by Corbis Corporation. All visual media © by Corbis Corporation and/or its media providers. All Rights Reserved.</span>
		</td>
	</tr>
</table>

			<img src=""/images/1clearpx.gif"" height=""24"" width=""1"" border=0 >
		</td>
	</tr>
</table>

<script language=""JavaScript"">
	var vSearchButton = 'ctl08_keywordsSearchControl_SearchButton';
	var vKeywordsText = 'ctl08_keywordsSearchControl_KeyWordsTextBox';
	var vKeywordsEmptyError = ""Please enter a keyword or image id!"";
</script>

<script language=""JavaScript"">
	var vSearchInResultsCheck = 'ctl08_searchInResultsCheckControl_SearchInResultsDropCheckBox';
</script>

<script language=""JavaScript"">
	var photosIllustrationArray = new Array();
	photosIllustrationArray[0] = 'ctl08_photosIllustrationsControl_photosRepeater_ctl00_cb';
	photosIllustrationArray[1] = 'ctl08_photosIllustrationsControl_photosRepeater_ctl01_cb';
</script>

<script language=""JavaScript"">
	var colorBlackAndWhiteArray = new Array();
	colorBlackAndWhiteArray[0] = 'ctl08_colorBlackAndWhiteControl_colorRepeater_ctl00_cb';
	colorBlackAndWhiteArray[1] = 'ctl08_colorBlackAndWhiteControl_colorRepeater_ctl01_cb';
</script>

<script language=""JavaScript"">
	var vModelReleaseOnly = 'ctl08_onlyModelReleasedControl_OnlyModelReleasedCheckBox';
</script>

<script language=""JavaScript"">
	var collectionsArray = new Array();
	collectionsArray[0] = 'ctl08_collectionsControl_listNews';
	collectionsArray[1] = 'ctl08_collectionsControl_listRF';
	collectionsArray[2] = 'ctl08_collectionsControl_listRM';
	var collectionsCheckAll = new Array();

	collectionsCheckAll[0] = 'ctl08_collectionsControl_chkAllNews';

	collectionsCheckAll[1] = 'ctl08_collectionsControl_chkAllRF';

	collectionsCheckAll[2] = 'ctl08_collectionsControl_chkAllRM';
</script>

<script language=""JavaScript"">
	var orientationArray = new Array();
	orientationArray[0] = 'ctl08_orientationControl_orientationRepeater_ctl00_Checkbox2';
	orientationArray[1] = 'ctl08_orientationControl_orientationRepeater_ctl01_Checkbox2';
	orientationArray[2] = 'ctl08_orientationControl_orientationRepeater_ctl02_Checkbox2';
</script>

<script language=""JavaScript"">
	var categoryNameArray = new Array();
	categoryNameArray[0] = 'Commercial';
	categoryNameArray[1] = 'Editorial';
	categoryNameArray[2] = 'Historical';
	categoryNameArray[3] = 'Art';
	categoryNameArray[4] = 'News';
	categoryNameArray[5] = 'Sports';
	categoryNameArray[6] = 'Entertainment';
</script>

<script language=""JavaScript"">
	var categoryValueArray = new Array();
	categoryValueArray[0] = '10';
	categoryValueArray[1] = '1';
	categoryValueArray[2] = '2';
	categoryValueArray[3] = '5';
	categoryValueArray[4] = '6';
	categoryValueArray[5] = '9';
	categoryValueArray[6] = '7';
</script>

<script language=""JavaScript"">
	var categoryArray = new Array();
	var categorySelected;
	categoryArray[0] = 'ctl08_categoriesControl_categoryRepeater_ctl00_ctl00';
	categoryArray[1] = 'ctl08_categoriesControl_categoryRepeater_ctl01_ctl00';
	categoryArray[2] = 'ctl08_categoriesControl_categoryRepeater_ctl02_ctl00';
	categoryArray[3] = 'ctl08_categoriesControl_categoryRepeater_ctl03_ctl00';
	categoryArray[4] = 'ctl08_categoriesControl_categoryRepeater_ctl04_ctl00';
	categoryArray[5] = 'ctl08_categoriesControl_categoryRepeater_ctl05_ctl00';
	categoryArray[6] = 'ctl08_categoriesControl_categoryRepeater_ctl06_ctl00';
	var categoryCheckAll = 'ctl08_categoriesControl_cbSelectAll';
</script>

<script language=""JavaScript"">
	var vInTheLastRadio = 'ctl08_imagesMadeAvailableControl_InTheLastRadio';
	var vInTheLastTextBox = 'ctl08_imagesMadeAvailableControl_InTheLastTextBox';
	var vBetweenRadio = 'ctl08_imagesMadeAvailableControl_BetweenRadio';
	var vBetweenTextBox = 'ctl08_imagesMadeAvailableControl_BetweenTextBox';
	var vAndTextBox = 'ctl08_imagesMadeAvailableControl_AndTextbox';
	var vInvalidMonthError = ""is not a valid month - please use mm/dd/yyyy format"";
	var vInvalidYearError = ""Dates must be 4-digit years between 1900 and 2020"";
	var vIsNotADayInError = ""is not a day in"";
	var vPleaseEnterDateError = ""Please enter a valid date."";
	var vDateFormatError = ""Please check date format!"";
	var vNumberError = ""Please enter a valid number."";
	var vEndDateError = ""Please enter a start date earlier than the end date"";
	var vDateFormat = ""mm/dd/yyyy"";
SetImagesMadeAvailableDate();
</script>

<script language=""JavaScript"">
function isNumeric(sField){if(sField.value != ''){if(isNaN(sField.value)){alert(""Please enter a valid number."");sField.focus();}}}
</script>
<script language=""JavaScript"">
	var vDatePhotoOrCreated = 'ctl08_datePhotographedOrCreatedControl_DatePhotographedOrCreatedTextBox';
</script>

<script language=""JavaScript"">
	var vLocation = 'ctl08_locationControl_LocationTextBox';
</script>

<script language=""JavaScript"">
	var vSearchImageImageSets = 'ctl08_searchImagesImageSetsControl_ddlSearchFor';
</script>

<script language=""JavaScript"">
	var vPhotographerName = 'ctl08_photographerNameControl_PhotographerNameTextBox';
</script>

<script language=""JavaScript"">
	var vImageNumbers = 'ctl08_imageNumbersControl_ImageNumbersTextBox';
</script>

<script language=""JavaScript"">
	var vSpecifySource = 'ctl08_specifySourceControl_SpecifySourceTextBox';
</script>

<script language=""JavaScript"">
	var vEnglishOnly = 'ctl08_englishOnlyControl_EnglishOnlyCheckBox';
</script>

<script language=""JavaScript"">
	var vPointOfView = 'ctl08_pointOfViewControl_ddlPointOfView';
</script>

<script language=""JavaScript"">
	var vImmediateAvailability = 'ctl08_immediateAvailabilityControl_ddlImmediateAvailability';
</script>
<script language=""javascript"" src=""http://cache.corbis.com/pro/6.3/javascript/s_code.js""></script>
<script language=""JavaScript""><!--
s.pageName=""""
s.server=""""
s.channel=""Home""
s.pageType=""""
s.prop1=""""
s.prop2=""""
s.prop3=""""
s.prop4=""""
s.prop5=""""
s.prop6=""""
s.prop7=""""
s.prop8=""""
s.prop9=""""
s.prop10=""""
s.campaign=""""
s.state=""""
s.zip=""""
s.events=""""
s.products=""""
s.purchaseID=""""
/************* DO NOT ALTER ANYTHING BELOW THIS LINE ! **************/var s_code=s.t();if(s_code)document.write(s_code)//--></script>
<script language=""JavaScript""><!--if(navigator.appVersion.indexOf('MSIE')>=0)document.write(unescape('%3C')+'\!-'+'-')//--></script><!-- End SiteCatalyst code version: H.2. --></form>
		
	</body>
</HTML>
";

            XmlDocument doc;

            doc = HtmlParser.Parse(source);
            Assert.IsNotNull(doc["root"]["html"]["head"]);
            Assert.IsNotNull(doc["root"]["html"]["body"]);
        }

        [TestMethod]
        [TestProperty("Lib", "LillTek.Advanced")]
        public void HtmlParser_Parse_UW()
        {

            // Generate a DOM source scraped from www.washington.edu and 
            // make sure we don't see an exception.

            const string source =
@"
<!DOCTYPE html PUBLIC ""-//W3C//DTD XHTML 1.0 Strict//EN""
        ""http://www.w3.org/TR/xhtml1/DTD/xhtml1-strict.dtd"">
<!--
    If you're interested in using the flyout menus, you're better off
    with the examples rather than trying to figure out how to use them
    in this file.  The documentation is at:
    
	http://www.washington.edu/webinfo/case/flyout/

$Id: index.html,v 1.29 2006/09/28 17:01:04 fmf Exp $
-->
<html xmlns=""http://www.w3.org/1999/xhtml"" xml:lang=""en"" lang=""en"">
<head>
	<meta http-equiv=""content-type"" content=""text/html; charset=iso-8859-1"" />
	<title>University of Washington</title>
	<style type=""text/css"">
		@import 'home/home2006.css';
		@import 'home/flash2006.css';
@import 'home/features/slideshow.css';

body {
	min-width: 77em;
}
* html div#header div.ieminwidth {
	width: 77em;
}
div#linkbar {
	height: 18em;
}
	</style>
	<script type=""text/javascript"" src=""home/scripts/home2006pre.js""></script>
	<script type=""text/javascript"">
<!--
function mIn () { return true; }
function mOut () { return true; }
function makeLayer () { return true; }
function flyDefs () { return true; }
if (! document.flyout_disable && 'undefined' != typeof document.getElementById)
    document.write ('<' + 'script src=""/home/scripts/flyout.js"" ' +
			'type=""text/javascript""><' + ""/script>\n"");
// -->
</script>

	<script type=""text/javascript"" src=""home/scripts/flash-detect.js""></script>
	<script type=""text/javascript"" src=""home/scripts/home2006.js""></script>
	<script type=""text/javascript"" src=""home/scripts/flash2006.js""></script>
	<script type=""text/javascript"">
	// <![CDATA[
		// 'img alt text'
		document.uw_visitimgarr = new Array (
			'ctour5.jpg Rainier Vista from Red Square',
			'ctour6.jpg Red Square',
			'ctour7.jpg Students in the Quad',
			'ctour8.jpg Cherry trees in the Quad [ks]',
			'ctour9.jpg Students between classes',
			'ctour10.jpg Students between classes'
		);
		// { img: 'src', url: 'url', alt: 'alt txt' }
		document.uw_quickimgarr = new Array (
		);
	// ]]>
	</script>
</head>
<body id=""uwhome"" class=""noscript"">
<script type=""text/javascript"">bodystart ();</script>
<div id=""header"">
	<div id=""iesizer"" class=""ieminwidth""></div>
	<img id=""logo"" src=""home/graphics/home2006/logograd.gif"" alt=""University of Washington logo"" />
	<h1>University of Washington</h1>
	<ul id=""campuses"">
		<li id=""bothell""><a href=""http://www.uwb.edu/"">Bothell</a></li>
		<li id=""seattle"">Seattle</li>
		<li id=""tacoma""><a href=""http://www.tacoma.washington.edu/"">Tacoma</a></li>
	</ul>
	<div id=""searchbox"">
		<form method=""get"" action=""http://www.google.com/u/washington"" onsubmit=""return searchcheck ()"">
			<div>
				<input type=""hidden"" name=""site"" value=""search"" />
				<input type=""hidden"" name=""hl"" value=""en"" />
				<input type=""hidden"" name=""lr"" value="""" />
				<input type=""hidden"" name=""safe"" value=""off"" />
				<input id=""stext"" name=""q"" type=""text"" size=""33"" onfocus=""searchfocus ()"" onblur=""searchblur ()"" value="""" />
				<script type=""text/javascript""> webkitsearch (); </script>
				<input id=""submit"" type=""submit"" value=""Search"" />
				<script type=""text/javascript""> searchsetup (); </script>
			</div>
		</form>
		<ul>
			<li><a href=""/home/directories.html"">UW Directories</a></li>
			<li><a href=""http://depts.washington.edu/mediarel/temp/events.shtml"">Calendar</a></li>
		</ul>
	</div>
</div>
<div id=""linkbar"">
	<ul id=""mainlinks"">
		<li id=""m_ab""><h2><a href=""http://depts.washington.edu/mediarel/temp/about.shtml"" onmouseover=""mIn ('m_ab')"" onmouseout=""mOut ('m_ab')"">About the UW</a></h2>
			<ul class=""flyout"" id=""l_m_ab"">
				<li><a href=""http://depts.washington.edu/mediarel/temp/mission.shtml"">Mission, Stats, Facts</a></li>
				<li><a href=""http://www.uwnews.org/Uwnews/sites/OOP/index.asp?sm=38"">Office of the President</a></li>
				<li><a href=""http://depts.washington.edu/mediarel/temp/visit.shtml"">Visit the UW</a></li>
				<li><a href=""/diversity/"">Diversity</a></li>
				<li><a href=""http://depts.washington.edu/mediarel/temp/events.shtml"">UW Events</a></li>
				<li><a href=""http://depts.washington.edu/mediarel/temp/museums.shtml"">Museums and Exhibits</a></li>
				<li><a href=""http://depts.washington.edu/mediarel/temp/administration.shtml"">Administration and Governance</a></li>
				<li><a href=""/admin/business/oem/"">Emergency Information</a></li>
			</ul></li>
		<li id=""m_de""><h2><a href=""http://depts.washington.edu/mediarel/temp/academics.shtml"" onmouseover=""mIn ('m_de')"" onmouseout=""mOut ('m_de')"">Academics and Research</a></h2>
			<ul class=""flyout"" id=""l_m_de"">
				<li><a href=""/home/departments/departments.html"">Colleges, Schools, Departments</a></li>
				<li><a href=""/research/"">Office of Research</a></li>
				<li><a href=""http://depts.washington.edu/techtran/"">UW TechTransfer</a></li>
				<li><a href=""http://www.lib.washington.edu/"">Libraries</a></li>
			</ul></li>
		<li id=""m_ad""><h2><a href=""/admin/pubserv/admission/"" onmouseover=""mIn ('m_ad')"" onmouseout=""mOut ('m_ad')"">Admissions</a></h2>
			<ul class=""flyout"" id=""l_m_ad"">
				<li><a href=""/admin/pubserv/admission/"">Admissions</a></li>
				<li><a href=""http://depts.washington.edu/mediarel/temp/tours.shtml"">Campus Tours</a></li>
				<li><a href=""http://www.tacoma.washington.edu/options/"">New Freshmen Options</a></li>
				<li><a href=""http://www.tacoma.washington.edu/transfer/"">Transfer Enrollment</a></li>
				<li><a href=""http://www.uwb.edu/"">UW Bothell</a></li>
				<li><a href=""http://www.tacoma.washington.edu/"">UW Tacoma</a></li>
				<li><a href=""http://www.outreach.washington.edu/pc/uwhome/conted_fly/"">Continuing Education</a></li>
			</ul></li>
		<li id=""m_me""><h2><a href=""http://www.uwmedicine.org/"" onmouseover=""mIn ('m_me')"" onmouseout=""mOut ('m_me')"">UW Medicine</a></h2>
				<ul class=""flyout"" id=""l_m_me"">
					<li><a href=""http://www.uwmedicine.org/PatientCare/PatientCareOverview/"">Patient Care</a></li>
					<li><a href=""http://www.uwmedicine.org/Education/EducationOverview/"">Education</a></li>
					<li><a href=""http://www.uwmedicine.org/Research/ResearchOverview/"">Medical Research</a></li>
				</ul></li>
	</ul>
	<div class=""feature featslideshow"" id=""dawgdaze"">
    <div class=""featslideshowmain"">
	<h2>Dawg Daze Fall '06</h2>
	<p>All this week the UW will be welcoming a new class of students to campus. Dawg Daze features academic workshops, concerts, movies and more. So whether you're new to campus or just wondering what it's all about, check out these links.</p>

	<ul>
	    <li><a href=""http://depts.washington.edu/dawgdaze/index.php"">Overview, announcements, video &gt;</a></li>

	    <li><a href=""http://depts.washington.edu/dawgdaze/events_2006/index.html"">Complete list of events &gt;</a></li>



	</ul>

    </div>
    <div class=""featslideshowimgwrap"">

	<img src=""http://uwmedia.org/uwnews/pages/dawgdaze2006/daze1x.jpg"" alt=""Random images of young people on campus promoting Dawg Daze."" title=""Dawg Daze"" />


    </div>
</div>

</div>
<div id=""mainbody"">
	<div id=""newsbarwrap"">
		<ul id=""newsbar"">
			<li><div><a href=""http://www.henryart.org/ex/monsen75.html"" class=""img""><img src=""/home/graphics/news2006/monsen.jpg"" title=""Cindy Sherman. Untitled #228. 1990."" alt=""Cindy Sherman. Untitled #228. 1990. Chromogenic (Ektacolor) print. Henry Art Gallery, Joseph and Elaine Monsen Photography Collection, gift of Joseph and Elaine Monsen and The Boeing Company. Courtesy of the artist and Metro Pictures."" /></a><h2><a href=""http://www.henryart.org/ex/monsen75.html"">75 at 75</a></h2>Joseph Monsen's 75 favorite photographs. <a href=""http://www.henryart.org/ex/monsen75.html"" class=""more"">More &gt;</a></div><div class=""ieminwidth""></div></li>
<li><div><a href=""http://www.washington.edu/burkemuseum/events/cos/"" class=""img""><img src=""/home/graphics/news2006/deadart.jpg"" title=""Drawing by Isaac Hernandez Ruiz"" alt=""A turquoise skull drawing by artist Isaac Hernandez Ruiz to mark the Day of the Dead exhibit opening Saturday at the Burke Museum."" /></a><h2><a href=""http://www.washington.edu/burkemuseum/events/cos/"">Day of the Dead</a></h2>The exhibit opens Saturday @ the Burke Museum. <a href=""http://www.washington.edu/burkemuseum/events/cos/"" class=""more"">More &gt;</a></div><div class=""ieminwidth""></div></li>
<li><div><a href=""http://gohuskies.cstv.com/sports/m-footbl/wash-m-footbl-main.html"" class=""img""><img src=""/home/graphics/news2006/fb26.jpg"" title=""File photo"" alt=""File photo"" /></a><h2><a href=""http://gohuskies.cstv.com/sports/m-footbl/wash-m-footbl-main.html"">Husky Football</a></h2>Dawgs hit the road to face Arizona  <a href=""http://gohuskies.cstv.com/sports/m-footbl/wash-m-footbl-main.html"" class=""more"">More &gt;</a></div><div class=""ieminwidth""></div></li>

		</ul>
		<div class=""clear ieminwidth""></div>
	</div>
	<div id=""tablewrap"">
		<table cellspacing=""0"" summary=""main content"">
			<tr>
				<td id=""quicklinks"">
					<div class=""ieminwidth""></div>
					<h2>UW QuickLinks</h2> 
					<ul>
						<li><a href=""http://www.uwnews.org/"">News</a></li>
						<li><a href=""http://depts.washington.edu/mediarel/temp/events.shtml"">Calendar</a></li>
						<li><a href=""http://www.lib.washington.edu/"">Libraries</a></li>
						<li><a href=""http://www.outreach.washington.edu/pc/uwhome/conted_fly/"">Continuing Education &amp; Summer Quarter</a></li>
						<li><a href=""/home/international/"">Global Affairs</a></li>
						<li><a href=""/students/"">Student Guide</a></li>
						<li><a href=""/research/industry/"">Business and Industry</a></li>
						<li><a href=""http://myuw.washington.edu/"">MyUW</a></li>
						<li><a href=""http://gohuskies.ocsn.com/"">Husky Sports</a></li>
						<li><a href=""/alumni/"">UW Alumni</a></li>
					</ul>
				</td>
				<td id=""news"">
					<div class=""ieminwidth""></div>
					<h2>More news from the University of Washington</h2> 
					<ul>
						
<!-- MIDDLE -->

<li>
<h3>
<a  href=""http://uwnews.org/uweek/uweekarticle.asp?articleID=26773"">Creative writing to get $15 million</a></h3> 
    The Creative Writing Program at the UW has been promised an estimated $15 million, the largest bequest ever made to the College of Arts & Sciences.  
 <a href=""http://uwnews.org/uweek/uweekarticle.asp?articleID=26773"" class=""more"">UWeek &gt; </a> 
</li>

<li>
<h3><a  
href=""http://uwnews.org/uweek/uweekarticle.asp?articleID=26801"">Chemistry prof wins Pioneer Award</a></h3>
Younan Xia, who does research at some of the smallest scales imaginable, has been named to receive the Director's Pioneer Award from the National Institutes of Health. The honor includes $2.5 million in direct research funding over five years.    <a href=""http://uwnews.org/uweek/uweekarticle.asp?articleID=26801"" class=""more"">UWeek &gt;</a> 
</li>

<li>
<h3><a href=""http://seattlepi.nwsource.com/health/286768_1918flu28.html"">Unraveling a Spanish flu mystery</a></h3>
 UW scientists using sophisticated genetic analysis now believe that an incredibly violent immune response to the 1918 Spanish flu virus is what made it the deadliest outbreak in human history. <a href=""http://seattlepi.nwsource.com/health/286768_1918flu28.html"" class=""more"">Seattle P-I &gt;</a>
</li>

<li>
<h3><a  
href=""http://seattlepi.nwsource.com/local/286622_firstday27.html"">East meets West: from small town to big city</a></h3>
To mark the first day of classes, the Seattle P-I profiled students from small-town Washington -- Toppenish, to be exact, a town that's roughly a quarter the size of the UW's student body on the Seattle campus.
<a href="" http://seattlepi.nwsource.com/local/286622_firstday27.html"" class=""more""> Read the article </a>
</li>



					</ul>
				</td>
				<td id=""visit"">
					<div class=""ieminwidth""></div>
					<h2>Visit the UW</h2>
					<img id=""visitimg"" src=""home/graphics/home2006/ctour5.jpg"" style=""width:11.25em;height:6.5625em"" title=""tour title"" alt=""tour alt"" />
					<script type=""text/javascript"">visitimage (); </script>
					<p>Welcome to the University of Washington! Here are a few of the ways to visit our campus in person or virtually. If you have questions, give our Visitor Information Center a call - 206-543-9198.</p>
					<ul>
						<li><a href=""http://admit.washington.edu/Visit/"">Students/prospective students</a></li>
						<li><a href=""http://admit.washington.edu/Visit/Tourcast/"">Download MP3 tour</a></li>
						<li><a href=""http://depts.washington.edu/mediarel/temp/tours.shtml"">Special interest tours</a></li>
						<li><a href=""/home/maps/"">Campus Maps</a></li>
					</ul>
				</td>
			</tr>
		</table>
		<img id=""george"" src=""home/graphics/home2006/george.png"" title=""Statue of George Washington on the UW Campus"" alt=""Statue of George Washington on the UW Campus"" /> 
		<a href=""http://www.uwfoundation.org""><img id=""quickimg"" src=""home/graphics/home2006/campaignuw1.gif"" title="""" alt=""Campaign UW - Creating Futures"" /></a>
		<script type=""text/javascript"">quickselimg ()</script>
	</div>
	<ul id=""footlinks"">
		<li class=""first""><a href=""/home/maps/"">Maps</a></li>
		<li><a href=""/home/siteinfo/"">FAQs</a></li>
		<li><a href=""/home/directories.html"">Directories</a></li>
		<li><a href=""/jobs/"">Employment</a></li>
		<li><a href=""http://myuw.washington.edu/"">MyUW</a></li>
		<li><a href=""/uwin/"">UWIN</a></li>
		<li><a href=""/home/siteinfo/form/"">Contact Us</a></li>
	</ul>
</div>
<div id=""footer"">
	&copy; 2006 University of Washington
</div>
</body>
</html>
<!--Created by chtml  on Sep 28, 2006 3:43pm-->
";

            XmlDocument doc;

            doc = HtmlParser.Parse(source);
            Assert.IsNotNull(doc["root"]["html"]["head"]);
            Assert.IsNotNull(doc["root"]["html"]["body"]);
        }

        [TestMethod]
        [TestProperty("Lib", "LillTek.Advanced")]
        public void HtmlParser_Parse_Expedia()
        {

            // Generate a DOM source scraped from www.expedia.com and 
            // make sure we don't see an exception.

            const string source =
@"

<!--::434226::-->




<!DOCTYPE html PUBLIC ""-//W3C//DTD XHTML 1.0 Transitional//EN"" ""http://www.w3.org/TR/xhtml1/DTD/xhtml1-transitional.dtd"">
<html xmlns=""http://www.w3.org/1999/xhtml"">
<head>
<COMMENT TITLE=""MONITOR""></COMMENT>
<meta http-equiv=""Content-Language"" content=""en-us"" />
<meta http-equiv=""Content-type"" content=""text/html; charset=iso-8859-1""/>
<META NAME=""ROBOTS"" CONTENT=""NOODP"">
<META NAME=""keywords"" content=""cheap airfare, discount airfare, cheap flights, expedia, www.expedia.com, travelcheap, discount flights, expedia airfare, discount travel, expedia flights, cheap air fare, travel expedia, expedia travel, cheap plane tickets, cheap travel, planetickets, cheap air plane tickets, discounttravel, online reservations, vacation rentals, vacation packages, hotel reservations, lodging, resorts, car reservations, car rentals, maps, guidebooks, guide books, travel guides, destination information"">

<META NAME=""description"" content=""Expedia.com is the premier online travel planning and flight-booking site. Purchase airline tickets online, find vacation packages, and make hotel and car reservations, find maps, destination information, travel news and more."">


<link rel=""stylesheet"" type=""text/css"" href=""/daily/styles/main.css""></link>

<style type=""text/css"">
@import url(""/daily/styles/2ColFx.css"");
@import url(""/daily/styles/CCDeals.css"");
@import url(""/daily/styles/COffer.css"");
@import url(""/daily/styles/DynPkg.css"");
@import url(""/daily/styles/Flash.css"");
@import url(""/daily/styles/Flex.css"");
@import url(""/daily/styles/Wiz.css"");
@import url(""/daily/styles/ilt.css"");
@import url(""/daily/styles/miText.css"");

</style>

<title>Expedia Travel -- discount airfare, flights, hotels, cars, vacations, cruises, activities, maps</title>

<script language=""javascript"" type=""text/javascript"" src=""/daily/common/tth.js""></script>

</head>
<body>
<div id=""divMainBody"">

<link rel=""stylesheet"" type=""text/css"" href=""/daily/styles/main.css"" />


<div id=""divSiteAnalytics"" style=""display:none;"">
<script language=""JavaScript"" type=""text/javascript"" src=""/daily/common/SiteAnalytics.js""></script>

<script language=""JavaScript"">
<!--
	var oSiteAn = new SiteAn();
	oSiteAn.SetAccount(GetAccount(document.location.toString(),""1""));
	
	oSiteAn.SetPageName(""Home Page"");
	oSiteAn.SetCurrencyCode(""USD"");
	oSiteAn.SetHierarchy("""");
	oSiteAn.SetChannel(""home"");
	oSiteAn.SetEvents("""");
	oSiteAn.SetTraffic1(document.getElementsByTagName(""Title"")[0].text);
	oSiteAn.SetTraffic12(""4141DAF0272B4E09A1D7BAABBB6463CC"");
	oSiteAn.SetVar31("""");
	oSiteAn.SetVar32(""Home Page"");
	oSiteAn.SetVar33("""");
	
// -->
</script>
<script language=""JavaScript"" src=""/pubspec/scripts/include/siteanalytics_include.js""></script>

</div>
<script language=""javascript"" src=""http://media.expedia.com/media/content/expus/flash/tutorials/itin/launcher.js""></script>
<script language=""javascript"" src=""/daily/js/flash.js""></script>
<script language=""vbscript"" src=""/daily/js/flash.vbs""></script>

<!--start header code-->

<div id=""divHeader"">
	<a target=""_top"" id=""imgLogo"" href=""/""><img src=""http://media.expedia.com/media/content/expus/graphics/logos/exp/expedia_185x50.gif"" border=""0"" alt=""expedia.com"" /></a>
	<div id=""divTopSkip""><a href=""#startcontent"">Skip to main content</a></div>
	<div id=""divTopBanner"">
	
<!--::309784::-->


		<div id=""divTextBanner""><span></span></div>
	
	</div>
</div>




<div id=""divTopNav"">
	<ul>
	<li id=""liNavHome""><a class=""selected"" id=""A251_1"" target=""_top"" href=""/?rfrr=-483""><span>Home</span></a></li>
	<li id=""liNavFlt""><a id=""A251_2"" target=""_top"" href=""/daily/flights/default.asp?rfrr=-409""><span>Flights</span></a></li>
	<li id=""liNavHot""><a id=""A251_3"" target=""_top"" href=""/daily/hotels/default.asp?rfrr=-383""><span>Hotels</span></a></li>
	<li id=""liNavCar""><a id=""A251_4"" target=""_top"" href=""/daily/cars/default.asp?rfrr=-361""><span>Cars</span></a></li>
	<li id=""liNavVac""><a id=""A251_5"" target=""_top"" href=""/daily/packages/default.asp?rfrr=-360""><span>Vacation Packages</span></a></li>
	<li id=""liNavCru""><a id=""A251_6"" target=""_top"" href=""/daily/cruise/default.asp?rfrr=-359""><span>Cruises</span></a></li>
	<li id=""liNavAct""><a id=""A251_14"" target=""_top"" href=""/daily/activities/default.asp?rfrr=-37440""><span>Activities</span></a></li>
	<li id=""liNavDeal""><a id=""A251_12"" target=""_top"" href=""/daily/deals/default.asp?rfrr=-1488""><span>Deals and Destinations</span></a></li>
	<li id=""liNavMap""><a id=""A251_8"" target=""_top"" href=""/pubspec/scripts/eap.asp?goto=maps&rfrr=-357""><span>Maps</span></a></li>
	<li id=""liNavCorp""><a id=""A251_9"" target=""_top"" href=""/daily/business/default.asp?rfrr=-709""><span>Corporate Travel</span></a></li>
	</ul>
</div>

<div id=""divHeaderBar"">
	<div class=""left""><b>Welcome</b> - Already a member?&nbsp;&nbsp;<a id=""A253_2"" href=""/pub/agent.dll?qscr=logi&ussl=1&rfrr=-1264"">Sign&nbsp;in</a></div>
	<div class=""right"">
		<a id=""A252_2"" href=""/pub/agent.dll?qscr=litn&rfrr=-938""><img src=""http://media.expedia.com/media/content/expus/graphics/icons/myitins_15x12.gif"" width=""15"" height=""12"" alt="""" /><span>My Itineraries</span></a>
		<a id=""A252_3"" href=""/pub/agent.dll?qscr=info&rfrr=-939""><img src=""http://media.expedia.com/media/content/expus/graphics/icons/AcctInfo_9x12.gif"" width=""9"" height=""12"" alt="""" /><span>My Account</span></a>
		<a id=""A252_4"" href=""/daily/service/default.asp?rfrr=-940""><img src=""http://media.expedia.com/media/content/expus/graphics/icons/CustSup_14x12.gif"" width=""14"" height=""12"" alt="""" /><span>Customer Support</span>
        
	</div>
</div>
<span id=""cssanchor""><a name=""startcontent"" id=""startcontent""></a></span>

<script type=""text/javascript"" src=""/pubspec/scripts/include/xmlhttp.js""></script>
<script type=""text/javascript"" src=""/pubspec/scripts/include/autocomplete.js""></script>
<iframe src=""/eta/ac.htm"" style=""display:none;visibility:hidden;position:absolute;width:275;height:150;z-index:100000;background:white"" name=""airportac_out"" id=""airportac_out"" MARGINHEIGHT=0 MARGINWIDTH=0 NORESIZE FRAMEBORDER=0 SCROLLING=NO></iframe>


<div id=""div2ColFxOuterContainer"">
	
	<div id=""div2ColFxTopContent"">
		<!--::434228::-->

		<script language=""Javascript"" type=""text/javascript"">
		//<![CDATA[
			var aLOBinfo = new Array();
			var aLOBimages = new Array();
			var aLOBtracked = new Array();
			var strDefOffer = ""Flight"";

		aLOBinfo[""Flight""] = ""62%358111D3%dicicmF3%psa.tluafedF2%sgnivas-artxe-aidepxeF2%somorpF2%yliadF2%moc.aidepxe.wwwF2%F2%:ptth=tcerideR&=seulaVwaR&0806,8965,0355,9155,4625,2615,2415,7705,1605,713,742,632,051,001,29,48,27,06,15,34,13=seulaV&92931,42911,52911,4021,9989,71721,63211=stegraT&27011,23501,1169,0539,7988,3488,6388,7978,269,21=stnemgeS&92931=DItegraT&358111=DIdA&43406=DIthgilF&kcilc=epyT/gn.tneve/moc.aidepxe.sda//:ptth|http://media.expedia.com/media/content/expus/graphics/launch/deals/0906_10th100Coup_768x264.gif|"";
		aLOBimages[""Flight""] = new Image();
		
		aLOBimages[""Flight""].src = aLOBinfo[""Flight""].split(""|"")[1];
		
		aLOBtracked[""Flight""] = false;
		
		//]]>
		</script>

<!--

ShowAd(1, FLIGHTS, COREOFFER, OVERVIEW, , , , , , )
//-->


<div id=""divCOfferMain"">

	<a href=""javascript: void 0;"" onmousemove=""window.status='';"" onclick=""goThere();return false;"" id=""lnkCOfferCO""></a>	
	
</div>

<script language=""javascript"">
//<![CDATA[
var oCOfferDiv = document.getElementById(""divCOfferMain"");
var bLoadedOnce = false;
aCOfferImages = new Array();


	var trackImage = new Image();
	var bPageHasDCCoreOffer = true;
	var sCurrentLOB = """";
	var bFallback = false;
	var sCOfferURL = """";
	if (typeof aLOBinfo[""Flight""] != ""undefined"") setOffer(""Flight"");
	else setOffer(strDefOffer);
	function setOffer(sLOB)
	{ 
		sLOB = strDefOffer; 
		if (sCurrentLOB == sLOB) return;  
		if (typeof aLOBinfo[sLOB] == ""undefined"") return;  
		sCurrentLOB = sLOB;
		var aCOfferInfo = aLOBinfo[sLOB].split(""|"");
		if(aCOfferInfo[0] != """")
		{
			var img = aCOfferInfo[1];
			var clickurl = aCOfferInfo[0];
			var trackurl = aCOfferInfo[2];
			
			if (!aLOBtracked[sLOB])
			{
				trackImage.src = trackurl;
				aLOBtracked[sLOB] = true;
			}
			bFallback = false;
		}
		else if(bFallback) 
			return;
		else
		{
			var img = ""http://media.expedia.com/media/content/expus/graphics/launch/flights/200135379-001_LMflight_768x264.jpg"";
			var clickurl = ""psa.slaedthgilf/slaed_etunimtsal/slaed/yliad/"";
			bFallback = true;
		}
		if (bLoadedOnce) startFade();
		oCOfferDiv.style.backgroundImage = ""url("" + img + "")"";
		//document.getElementById(""lnkCOfferCO"").href = rev(clickurl);
		sCOfferURL = clickurl;
		if (bLoadedOnce) completeFade();
		bLoadedOnce = true;
	}
	
	function goThere()
	{
		window.location.href = rev(sCOfferURL);
	}
	function rev(str) 
	{
		if (!str) return '';
		var revstr='';
		for (i = str.length-1; i>=0; i--)
			revstr+=str.charAt(i)
		return revstr;
	}



	function startFade()
	{
		
		if (document.all && oCOfferDiv.filters.length > 0) oCOfferDiv.filters[0].Apply();                   
		else if(!document.all) {oCOfferDiv.style.MozOpacity = "".5"";oCOfferDiv.style.opacity = "".5"";}
		
	}
	function completeFade()
	{
		
		if (document.all && oCOfferDiv.filters.length > 0) oCOfferDiv.filters[0].Play();
		else if(!document.all) setMozOpac(50);
		
	}
	function setMozOpac(nOpac)
	{
		oCOfferDiv.style.MozOpacity = nOpac/101;
		oCOfferDiv.style.opacity = nOpac/101;
		if (nOpac < 100) 
			setTimeout(""setMozOpac("" + (nOpac+5) + "", 7);"");
	}
//]]>	
</script>
	</div>
	
	
	<div id=""div2ColFxColLeft"">
		<!--::434227::-->


    <script type=""text/javascript"" src=""/pubspec/scripts/include/srvy.js""></script>
    <script type=""text/javascript"" src=""/daily/js/e.js""></script>
<script type=""text/javascript"" src=""/daily/js/d.js""></script>
<script type=""text/javascript"" src=""/daily/js/homw.js""></script>

    <script type=""text/javascript"" src=""/daily/js/cal.js""></script>
    <IFRAME SRC=""/daily/common/calx.htm"" STYLE=""visibility:hidden;position:absolute;width:148px;height:194px;z-index:100;display:none;"" ID=""CalFrame"" NAME=""CalFrame"" MARGINHEIGHT=0 MARGINWIDTH=0 NORESIZE FRAMEBORDER=0 SCROLLING=NO></IFRAME>
    <script language=""javascript"" src=""/daily/js/calx.js""></script>
        
    
    <form id=""Wiz"" name=""Wiz"" method=""post"" action=""/daily/common/moreinfo.asp"">
    <input type=""hidden"" id=""BundleType"" name=""BundleType"" value=""1"" /><input type=""hidden"" id=""WT"" name=""WT"" value=""Home"" /><input type=""hidden"" id=""FCity"" name=""FCity"" value="""" /><input type=""hidden"" id=""FTLA"" name=""FTLA"" value="""" /><input type=""hidden"" id=""TCity"" name=""TCity"" value="""" /><input type=""hidden"" id=""TTLA"" name=""TTLA"" value="""" /><input type=""hidden"" id=""TCityId"" name=""TCityId"" value="""" /><input type=""hidden"" id=""FDate"" name=""FDate"" value=""mm/dd/yy"" /><input type=""hidden"" id=""TDate"" name=""TDate"" value=""mm/dd/yy"" /><input type=""hidden"" id=""MDate"" name=""MDate"" value="""" /><input type=""hidden"" id=""FTime"" name=""FTime"" value="""" /><input type=""hidden"" id=""TTime"" name=""TTime"" value="""" /><input type=""hidden"" id=""CarC"" name=""CarC"" value="""" /><input type=""hidden"" id=""nR"" name=""nR"" value="""" /><input type=""hidden"" id=""aRA"" name=""aRA"" value="""" /><input type=""hidden"" id=""aRS"" name=""aRS"" value="""" /><input type=""hidden"" id=""aRC"" name=""aRC"" value="""" /><input type=""hidden"" id=""aRCA"" name=""aRCA"" value="""" /><input type=""hidden"" id=""Inf"" name=""Inf"" value=""s"" /><input type=""hidden"" id=""CalS"" name=""CalS"" value=""9/28/2006"" /><input type=""hidden"" id=""CalE"" name=""CalE"" value=""8/24/2007"" />
    <div id=""divW"" onkeypress=""enter(event);"" style=""background:url(http://switch.atdmt.com/action/expedia_homepage) no-repeat"">
        <div id=""divWTitle""><span>Plan your trip</span></div>
        
            <div id=""divWBody"" >
        
            <div id=""divWrs"" class=""bl"">
                
                    <div id=""divWr1"" class=""divWr"" onclick=""hw('1');""><input id=""r1"" name=""srch"" type=""radio"" value=""flt""  class=""Wr""  checked=""checked""/><label for=""r1"">Flight</label></div>
                    <div id=""divWr2"" class=""divWr"" onclick=""hw('2');""><input id=""r2"" name=""srch"" type=""radio"" value=""hot"" class=""Wr"" /><label for=""r2"">Hotel</label></div>
                    <div id=""divWr3"" class=""divWr"" onclick=""hw('3');""><input id=""r3"" name=""srch"" type=""radio"" value=""car"" class=""Wr"" /><label for=""r3"">Car</label></div>
                    <div id=""divWr8"" class=""divWr"" onclick=""hw('8');""><input id=""r8"" name=""srch"" type=""radio"" value=""cru"" class=""Wr"" /><label for=""r8"">Cruise</label></div>
                
            </div>
            
            <div id=""divWFields"" class=""bl"">
                
                <div class=""bl1""><div class=""flmed"">Leaving from:<br/><input id=""fcy"" name=""fcy"" type=""text"" class=""med"" /></div><div class=""frmed"">Going to:<br/><input id=""tcy"" name=""tcy"" type=""text"" class=""med"" /></div></div><div class=""bl1""><div class=""flmed""><div class=""fl"">Departing:<br/><input id=""fdt"" name=""fdt"" type=""text"" class=""small"" /></div><div class=""fr"">Time:<br/><select id=""ftt"" name=""ftt"" class=""small""><option value=""362"">Any</option></select></div></div><div class=""frmed""><div class=""fl"">Returning:<br/><input id=""tdt"" name=""tdt"" type=""text"" class=""small"" /></div><div class=""fr"">Time:<br/><select id=""ttt"" name=""ttt"" class=""small""><option value=""362"">Any</option></select></div></div></div><div class=""clearer""></div>
                
            </div>
            
                <div id=""divRASC"" class=""bl"">
                
                <div class=""bl1""><div class=""flrasc"" style=""width:80px;"">Adults(19-64):<br/><select id=""rad"" name=""rad""><option value=""2"">2</option></select></div><div class=""flrasc"" style=""width:80px;"">Seniors(65+):<br/><select id=""rse"" name=""rse""><option value=""0"">0</option></select></div><div class=""flrasc"" style=""width:80px;"">Children(0-18):<br/><select id=""rch"" name=""rch""><option value=""0"">0</option></select></div></div>
                
                <div class=""clearer""></div>
                
                </div>
                <div id=""divCA"" class=""bl"" style=""display:none;""></div>
            
                <div id=""divCC"" class=""bl"" style=""display:none;""></div>
            <div id=""divWOptions"" class=""bl""><div id=""divO1"" class=""bl1""><a href=""javascript://"" onmouseover=""window.status='';return(true);"" onmouseout=""window.status='';"" onclick=""SetAO();""></a></div><div class=""clearer""></div></div>
            <div id=""divWSubmit"" class=""bl"">
                <span id=""divWst""><div style=""float:right;""><div style=""clear:both;""><div style=""float:left;""><div class=""wizBtnLB"" onclick=""SF();"" onmouseover=""stmo('Search for flights');""><div class=""wizBtnRB""><div class=""wizBtnMB"">Search for flights</div></div></div></div></div>
<div style=""clear:both;""><div style=""float:left;margin-top:8px;""><div class=""wizBtnLB"" onclick=""SFBundle(3);"" onmouseover=""stmo('Search for flights + hotels ');""><div class=""wizBtnRB""><div class=""wizBtnMB"">Search for flights + hotels</div></div></div></div></div>

</div><div class=""clearer""></div></span>
            </div>
            
            
                <div id=""divTele"" class=""bl"" style=""display:none;"">Book online or call a specialist at <b>1 (800) 551-2534</b></div>
            
                <div id=""divWSavings"">
	                <div style=""float:left;font-size:4px;height:5px;width:auto;"">
		                <div style=""background:#7694bf url(http://media.expedia.com/media/content/expus/graphics/common/corners/img_crnr_homePkg_TL.gif) no-repeat left;float:left;height:5px;width:5px;""></div>
		                <div style=""border-top:solid 1px #7694bf;float:left;font-size:3px;width:142px;""></div>
		                <div style=""background:#7694bf url(http://media.expedia.com/media/content/expus/graphics/common/corners/img_crnr_homePkg_TR.gif) no-repeat right;float:left;height:5px;width:5px;""></div>
	                </div>
	                <div style=""border-left:solid 1px #7694bf;border-right:solid 1px #7694bf;float:left;width:auto;margin:0;padding:3px 0px;"">
	                    <a href=""/daily/promos/vacations/Save-with-Expedia-Vacation-Packages/default.asp""><div style=""width:138px;margin-left:6px;height:30px;background:#ffffff url(http://media.expedia.com/media/content/expus/graphics/wiz/img_hmpg_PkgSav.gif) no-repeat;cursor:pointer;cursor:hand;""></div></a>
	                    <div style=""width:134px;text-align:center;""><!--::434237::-->
<script language=""javascript"">tttitle = 'Book flight + hotel together and SAVE';tttext = '<div style=""padding:3px;font-family:arial;font-size:13px;""><div style=""padding-top:5px;"">Once you enter your travel dates and departure and destination info, you can:</div><div style=""padding-top:8px;""><img src=""http://media.expedia.com/media/content/expus/graphics/common/bullet/s_FFCC66_6x6.gif"" hspace=""3"" align=""middle"" WIDTH=""6"" alt="""" HEIGHT=""6""><b>Choose any flight.</b></div><div style=""font-size:11px;text-indent:12px"">(The cheapest flight is pre-selected.)</div><div style=""padding-top:8px;""><img src=""http://media.expedia.com/media/content/expus/graphics/common/bullet/s_FFCC66_6x6.gif"" hspace=""3"" align=""middle"" WIDTH=""6"" alt="""" HEIGHT=""6""><b>Choose any hotel.</b></div><div style=""font-size:11px;text-indent:12px"">(You\'ll see the total price with each option.)</div><div style=""padding:8px 0px;""><b>Then, we pass along the savings to you for booking together!</b></div><div style=""font-size:10px;padding-bottom:5px;"">Average $220 savings are based on actual bookings for air + hotel trips for two adults to Expedia’s top 25 vacation package destinations.  Savings vary by destination and origin.</div></div>';ttbc = '336699';tticon = '/eta/tip_icon.gif';ttw = 275;</script><img src=""/eta/tip_icon.gif"" hspace=3 border=0 align=middle><a href=""javascript://"" onclick=""event.cancelBubble=true;TT(this);return false;"">Learn more...</a></div>
		                <div id=""divHBWSavingsRad"" style=""width:150px;"">
                            <div id=""divWr4"" class=""divWr1"" onclick=""hw('4');""><input id=""r4"" name=""srch"" type=""radio"" value=""flthot"" class=""Wr"" /><label for=""r4"">Flight + Hotel</label></div>
                            <div id=""divWr7"" class=""divWr1"" onclick=""hw('7');""><input id=""r7"" name=""srch"" type=""radio"" value=""fltcar"" class=""Wr"" /><label for=""r7"">Flight + Car</label></div>
                            <div id=""divWr5"" class=""divWr1"" onclick=""hw('5');""><input id=""r5"" name=""srch"" type=""radio"" value=""flthotcar"" class=""Wr"" /><label for=""r5"">Flight + Hotel + Car</label></div>
                            <div id=""divWr6"" class=""divWr1"" onclick=""hw('6');""><input id=""r6"" name=""srch"" type=""radio"" value=""hotcar"" class=""Wr"" /><label for=""r6"">Hotel + Car</label></div>
		                </div>
	                </div>
	                <div style=""float:left;font-size:4px;height:5px;width:auto;"">
		                <div style=""background:#f1f4f7 url(http://media.expedia.com/media/content/expus/graphics/common/corners/img_crnr_homePkg_BL.gif) no-repeat left;float:left;height:5px;width:5px;""></div>
		                <div style=""border-bottom:solid 1px #7694bf;float:left;font-size:3px;height:4px;width:142px;""></div>
		                <div style=""background:#f1f4f7 url(http://media.expedia.com/media/content/expus/graphics/common/corners/img_crnr_homePkg_BR.gif) no-repeat right;float:left;height:5px;width:5px;""></div>
	                </div>
	                <div class=""clearer""></div>
                </div>
            
        </div>
        
    </div>
    </form>
    
<script type=""text/javascript"">document.attachEvent('onreadystatechange',SI);function SI(){if(document.readyState=='interactive'||document.readyState=='complete'){I();}}</script>

<!--{{Traveler Alert (A column)#434231}}-->
<!--::434232::-->
<div class=""divSDark"" style=""margin-left:8px;""><img src=""/eta/spaceit.gif"" alt=""Suitcase"" style=""position:absolute;top:6px;left:199px;filter: progid:DXImageTransform.Microsoft.AlphaImageLoader(src='http://media.expedia.com/media/content/expus/graphics/icons/suitcase_59x49.png', sizingMethod=scale);width:59px;height:49px;""/>
<div class=""TBarDark"">Why travel with Expedia?</div><div><div class=""fl""><div class=""fl"" style=""""><p><a href=""/daily/highlights/best-rate-guarantee/default.asp?mcicid=HP_BPG""><b>Best Price Guarantee</b></a></p><p class=""padding"">Get the lowest price on flights,<br/>hotels, vacations, cruises and activities.</p></div><div class=""clearer""></div></div><div class=""fl""><div class=""fl"" style=""""><p style=""padding-top:4px;""><a href=""/daily/highlights/Expedia-Promise/default.asp?mcicid=HP_Promise""><b>The Expedia Promise</b></a></p><p class=""padding"">See the seven ways we're improving<br />
your trip&#151;from start to finish.
</p></div><div class=""clearer""></div></div><div class=""fl""><div class=""fl"" style=""""><p style=""padding-top:4px;""><a href=""/daily/promos/hurricane-promise/default.asp?mcicid=HP_HurricaneP""><b>Hassle-free Hurricane Promise</b></a></p><p class=""padding"">Weather threat? No worries. We’ll <br />
waive fees and help you re-book.
</p></div><div class=""clearer""></div></div><div class=""fl""><div class=""fl"" style=""""><p style=""padding-top:4px;""><a href=""/daily/promos/package-protection/default.asp?mcicid=HP_pkgprotection""><b>Package Protection Plan</b></a></p><p>Cancel for any reason! Comprehensive <br/>protection for your investment.</p></div><div class=""clearer""></div></div><div class=""clearer""></div></div><div style=""""></div></div>
<!--{{Email Module#422983}}-->
<!--{{RSS_E-mail Module#434236}}-->
<!--::438000::-->

<form id=""ec"" name=""ec"" method=""post"" action=""/daily/home/ec.asp"">
<input type=""hidden"" id=""hTLA1"" name=""hTLA1"" />
<div style=""width:272px;margin:0px 0px 16px 8px;"" onkeypress=""ece(event);"">

	<div style=""background:#7694bf url(http://media.expedia.com/media/content/expus/graphics/common/corners/corner_top_L.gif) no-repeat top left;height:25px;padding-left:8px;"">
	    <div style=""background:#7694bf url(http://media.expedia.com/media/content/expus/graphics/common/corners/corner_top_R.gif) no-repeat top right;height:25px;padding-right:5px;"">
	        <div style=""color:#ffffff;font-size:13px;font-weight:bold;line-height:25px;vertical-align:middle;"">Travel deals e-mail</div>
	    </div>
	</div>

    <div style=""border-left:solid 1px #7694bf;border-right:solid 1px #7694bf;"">
        <div style=""padding:8px 8px 0px 2px;"">
        
            <div style=""background:#ffffff url(http://media.expedia.com/media/content/expus/graphics/icons/mail_icon_54.gif) no-repeat top left;padding-left:54px;font-size:11px;line-height:14px;"">Be the first to hear about latest sales, promotions, money-saving deals and more!<div class=""clearer""></div></div>
        
        </div>
        <div style=""padding:0px 8px;margin-top:6px;background-color:#ffffff;"">
            <div style=""float:left;width:124px;vertical-align:middle;""><input id=""email"" name=""email"" type=""text"" value=""Enter e-mail address"" maxlength=""128"" style=""width:124px;max-width:124px;font-family:Arial;font-size:11px;"" onfocus=""this.select();"" /></div>
            <div style=""float:right;width:124px;vertical-align:middle;""><input id=""hair"" name=""hair"" type=""text"" value=""Enter home airport"" maxlength=""100"" style=""width:124px;max-width:124px;font-family:Arial;font-size:11px;"" onfocus=""this.select();"" autocomplete=""off"" /></div>
            <div class=""clearer""></div>
        </div>        
        <div style=""padding:0px 8px 2px 8px;margin-top:6px;background-color:#ffffff;"">
            <a href=""javascript:ecsf();"" style=""font-size:11px;line-height:13px;float:right;"">Sign up now!</a>
            <a href=""javascript:ecsf();""><img src=""/daily/common/images/btn_sec_small.gif"" alt=""Sign up now!"" height=""13"" width=""13"" style=""vertical-align:bottom;margin-right:4px;float:right;"" /></a>
            <div class=""clearer""></div>
        </div>
        <div class=""clearer""></div>
    </div>

	<div style=""background:#ffffff url(http://media.expedia.com/media/content/expus/graphics/common/corners/corner_bottom_L.gif) no-repeat top left;height:5px;padding-left:5px;"">
	    <div style=""background:#ffffff url(http://media.expedia.com/media/content/expus/graphics/common/corners/corner_bottom_R.gif) no-repeat top right;height:5px;padding-right:5px;"">
	        <div style=""border-bottom:solid 1px #7694bf;line-height:4px;height:4px;""></div>
	    </div>
	</div>

    <div class=""clearer""></div>
</div></form><script type=""text/javascript"">EnableAutoComplete('hair', 'airportac_out', 'ACOut', document.ec, 'airport', 'hTLA1');</script> 
<!--::434234::-->
<div class=""divS"" style=""margin-left:8px;""><div class=""TBarDark"">Tools and customer support</div><div style=""padding-top:6px;""><div class=""fl"" style=""width:132px;""><p><a href=""/exptrack/tools/status.asp?mcicid=HP_FltStatus"">Flight status</a></p><p><a href=""/daily/service/assistance.asp?mcicid=HP_Alerts"">Traveler alerts</a></p><p><a href=""/daily/weather/default.asp?mcicid=HP_Weather"">Weather</a></p><p><a href=""/daily/airports/default.asp?mcicid=HP_Airports"">Airport guides</a></p></div><div class=""fr"" style=""width:136px;""><p><a href=""""><!-- --></a></p><p><a href=""/daily/service/visa.asp?mcicid=HP_Passport""><span style=""color:#ff7300; font-weight:bold;"">New!</span> <strong>2007 Passport requirements</strong></a></p><p><a href=""http://www.expediaguides.com/farealert"">Fare Alert: Track airfare</a></p><p><a href=""/daily/outposts/rss/expedia_rss.asp?mcicid=HP_RSS"">Get deals via RSS</a></p></div><div class=""clearer""></div></div><div style=""border-bottom:solid 1px #cccccc;margin:0px 8px;""></div><div class=""divS1l"" style=""padding-top:5px;""><p>For quick answers to your questions or ways to contact us, visit our <a href=""/daily/service/default.asp?mcicid=HP_CustSupport"">Customer Support Center</a>.</p></div></div>
	</div>
	
	<div id=""div2ColFxColRight"">
		<!--::434235::-->

<style type=""text/css"">
div.TBarRnd  {height:24px;padding:0px 0px 0px 8px;font:bold 13px/24px Arial,Verdana,Helvetica;margin:0px 0px 4px 0px;background:#dee7ef url(http://media.expedia.com/media/content/expus/graphics/common/corners/img_crnr_modtitlebar.gif) no-repeat top right;color:#333;}
.divmiTextLaunch{padding:0px 8px 12px 8px;}

.FlexCol .divS .TBar {margin-bottom:5px;}
.FlexCol .divS1l A {text-decoration:none;}
.FlexCol .divS p {padding:0px 5px 0px 8px;}
.FlexCol .divS1l {margin:0px 0px 0px 0px;}

<!-- --> #divRTILM {background-color:#FFFAEE;font:11px/14px Arial;width:152px;border-bottom:solid 1px #FFEFBD;margin:0px 0px 8px 0px;}
<!-- --> #divRTILM .TBarRTI {color:#C60;font:bold 16px/24px Arial;padding: 0px 0px 0px 8px;height:24px;background:#FFEFBD url(http://media.expedia.com/media/content/expus/graphics/common/corners/img_crnr_modtitlebar_Y.gif) no-repeat top right;}
<!-- --> #divRTILM .divRTISection {padding:9px 8px 3px 8px;border-bottom:solid 1px #FFDF7B;}
<!-- --> #divRTILM .divRTIItem {padding:0px 0px 6px 0px;}
<!-- --> #divRTILM .divRTIItem a {text-decoration:none;}
<!-- --> #divRTILM .divRTIItem a:hover {text-decoration:underline;}
<!-- --> #divRTILM .divRTIItem a .redlink {font:bold 11px/14px Arial;color:#C60;}
<!-- --> #divRTILM .divRTIItem a:hover .redlink {color:#F60;text-decoration:underline;}
<!-- --> #divRTILM .divRTIBottomLink {background:#FFFAEE url(http://media.expedia.com/media/content/expus/graphics/common/arrows/btn_sec_small.gif) no-repeat  8px 7px;padding:5px 8px 7px 24px;border-top:solid 2px #fff;}


<!-- --> #divRTIDaily {font:11px/14px Arial;width:152px;}
<!-- --> #divRTIDaily .TBarRTI {color:#C60;font:bold 16px/24px Arial;padding: 0px 0px 0px 8px;background:#FFEFBD url(http://media.expedia.com/media/content/expus/graphics/common/corners/img_crnr_modtitlebar_Y.gif) no-repeat top right;}
<!-- --> #divRTIDaily .divRTISection {padding:9px 6px 4px 9px;border-bottom:dashed 1px #dadadc;}
<!-- --> #divRTIDaily .divRTICaption {padding:0px 0px 6px 0px;font:bold 11px Arial;color:#C60;}
<!-- --> #divRTIDaily .divRTIItem {padding:0px 0px 6px 0px;}
<!-- --> #divRTIDaily a {text-decoration:none;}
<!-- --> #divRTIDaily a:hover {text-decoration:underline;}
<!-- --> #divRTIDaily .divRTIItem a .redlink {font:bold 11px/14px Arial;color:#C60;}
<!-- --> #divRTIDaily .divRTIItem a:hover .redlink {color:#F60;text-decoration:underline;}
<!-- --> #divRTIDaily .divRTIBottomLink {background:#FFF url(http://media.expedia.com/media/content/expus/graphics/common/arrows/btn_sec_small.gif) no-repeat  8px 7px;padding:6px 8px 7px 24px;border-bottom:dashed 1px #dadadc;}
<!-- --> #divRTIDaily .divRTIBottomLink a {text-decoration:underline;}

</style>

<div class=""FlexChilddiv"">

	<div class=""FlexRow"" style=""margin:6px 0px 6px 0px;"">
	
	
		<div class=""FlexCol"" style=""width:472px;margin:0px 0px 0px 0px;"">
			<div class=""FlexInner"">
		<table cellpadding=""0"" cellspacing=""0"" border=""0"" style=""font-family:Arial;width:472px;"">
	<tr style=""vertical-align:top;"">
		<td><a href=""/daily/promos/expedia-10th-anniversary/default.asp?mcicid=10thAnnv_HP_bcoltop"" target=""_blank""><img src=""http://media.expedia.com/media/content/expus/graphics/promos/other/0906_10thSale_114x68.gif"" style=""margin-right:10px;"" width=""114"" alt="""" HEIGHT=""68"" border=""0""></a><td>
		<td><span style=""font-size:18px; color:#1F4E7C;"">Anniversary Sale savings!</span><br>
		<span style=""font-size:11px;"">It’s our 10th anniversary, and we’re celebrating with amazing deals and special offers on our top 10 favorite destinations.</span><br>
		<span style=""font-size:11px;""><img src=""http://media.expedia.com/media/content/expus/graphics/other/arrow_13x13.gif"" style=""margin-top:2px; margin-right:3px;"" width=""13"" alt="""" HEIGHT=""13""><a href=""/daily/promos/expedia-10th-anniversary/default.asp?mcicid=10thAnnv_HP_bcoltop"" target=""_blank"" style=""vertical-align:top;"">Book now, save big!</a></span>
		</td>
	</tr>
</table>
			</div>
		</div>
	
	</div>
	
	<div class=""clearer""></div>
	
	<div class=""FlexRow"" style=""margin:6px 0px 6px 0px;"">
	
	
		<div class=""FlexCol"" style=""width:312px;margin:0px 8px 0px 0px;"">
			<div class=""FlexInner"">
		<!--::434243::-->
<div class=""TBarRnd"">Featured offers</div>
<div class=""divmiTextLaunch"" style=""width:308px;"">


		<div class=""divmiTextItemLaunch"">
			<div class=""divmiTextLeftLaunch"" style=""margin:0px 0px 5px 0px;"">
			<a href=""http://www.expediagiveaway.com/?mcicid=HP2_featdeals1"" target=""_blank"">
			<img class=""imgmiText"" src=""http://media.expedia.com/media/content/expus/graphics/promos/other/sweepstakes_logo_88x48.gif"" alt=""Win a $50,000 dream vacation!"" border=""0""  style=""margin:0px 8px 0px 0px;"" WIDTH=""90"" HEIGHT=""48""/>
			</a>
			</div>
			<div class=""divmiTextRightLaunch"">
			<a href=""http://www.expediagiveaway.com/?mcicid=HP2_featdeals1"" target=""_blank""><span class=""spanmiTextCaptionLaunch"">Win a $50,000 dream vacation!</span></a>
				<div class=""divmiTextTextLaunch"">
					Don't miss out: You could win a $50,000 vacation from Expedia.
				</div>
			</div>
		</div>
		<div class=""clearer""></div>		

		<div class=""divmiTextItemLaunch"">
			<div class=""divmiTextLeftLaunch"" style=""margin:0px 0px 5px 0px;"">
			<a href=""/daily/promos/hotel/Holiday-Weekend-Getaways/default.asp?mcicid=HP2_featdeals2"">
			<img class=""imgmiText"" src=""http://media.expedia.com/media/content/shared/images\088x048\RF\AA032014_88x48.jpg"" alt=""Celebrate Columbus Day and save!"" border=""0""  style=""margin:0px 8px 0px 0px;"" WIDTH=""88"" HEIGHT=""48""/>
			</a>
			</div>
			<div class=""divmiTextRightLaunch"">
			<a href=""/daily/promos/hotel/Holiday-Weekend-Getaways/default.asp?mcicid=HP2_featdeals2""><span class=""spanmiTextCaptionLaunch"">Celebrate Columbus Day and save!</span></a>
				<div class=""divmiTextTextLaunch"">
					Enjoy the long weekend with our great low rates on hotels and vacations.
				</div>
			</div>
		</div>
		<div class=""clearer""></div>		

</div>

<!--::434303::-->

<div class=""FlexChilddiv"">

	<div class=""FlexRow"" style=""margin:0px 0px 0px 0px;"">
	
	
		<div class=""FlexCol"" style=""width:152px;margin:0px 0px 0px 0px;"">
			<div class=""FlexInner"">
		<div id=""flashshopdest""></div>
<script language=""JavaScript"">
	var hasRightVersion = DetectFlashVer(requiredMajorVersion, requiredMinorVersion, requiredRevision);
	if (hasRightVersion)
	{
		CreateFlashControl(""flashshopdest"", ""http://media.expedia.com/media/content/expus/flash/0506_shopdest_modules.swf"", ""FFFFFF"", ""false"", ""152"", ""305"", """");
	}
	else
	{
		var alternateContent = '<img src=""http://media.expedia.com/media/content/expus/flash/0506_shopdest_modules.jpg"" alt=""destinations"" width=""156"" height=""305"" border=""0"" usemap=""#hpdest"">';
		document.write(alternateContent);  // insert non-flash content
	}
</script>


<script language=""JavaScript"" type=""text/javascript"">
<!-- 
document.write('<div id=""crawlerContent"" style=""display:none"">'); 
// -->
</script>
<a href=""/daily/vacations/las-vegas/default.asp"">Las Vegas</a><br>
<a href=""/daily/vacations/New-York/default.asp"">New York</a><br>
<a href=""/daily/vacations/Hawaii/default.asp"">Hawaii</a><br>
<a href=""/daily/vacations/Caribbean/default.asp"">Caribbean</a><br>
<a href=""/daily/vacations/orlando/default.asp"">Orlando</a><br>
<a href=""/daily/vacations/mexico/default.asp"">Mexico</a><br>
<script language=""JavaScript"" type=""text/javascript"">
<!-- 
document.write('</div>'); 
// -->
</script>

<map name=""hpdest"">
<area alt=""Las Vegas"" coords=""3,26,155,156"" href=""/daily/vacations/las-vegas/default.asp"">
<area alt=""New York"" coords=""2,157,155,180"" href=""/daily/vacations/New-York/default.asp"">
<area alt=""Hawaii"" coords=""3,181,155,205"" href=""/daily/vacations/Hawaii/default.asp"">
<area alt=""Caribbean"" coords=""3,205,155,229"" href=""/daily/vacations/Caribbean/default.asp"">
<area alt=""Orlando"" coords=""3,229,155,252"" href=""/daily/vacations/orlando/default.asp"">
<area alt=""Mexico"" coords=""3,252,155,279"" href=""/daily/vacations/mexico/default.asp"">
<area alt=""See more..."" coords=""5,300,67,283"" href=""/daily/vacations/default.asp?mcicid=HP_moredest"">
</map>
			</div>
		</div>
	
		<div class=""FlexCol"" style=""width:8px;margin:0px 0px 0px 0px;"">
			<div class=""FlexInner"">
		<br>
			</div>
		</div>
	
		<div class=""FlexCol"" style=""width:152px;margin:0px 0px 0px 0px;"">
			<div class=""FlexInner"">
		<div id=""flashhptheme""></div>
<script language=""JavaScript"">
	var hasRightVersion = DetectFlashVer(requiredMajorVersion, requiredMinorVersion, requiredRevision);
	if (hasRightVersion)
	{
		CreateFlashControl(""flashhptheme"", ""http://media.expedia.com/media/content/expus/flash/theme/home_modules.swf"", ""FFFFFF"", ""false"", ""152"", ""305"", """");
	}
	else
	{
		var alternateContent = '<img src=""http://media.expedia.com/media/content/expus/flash/0506_home_modules.jpg"" alt=""Shope by theme"" width=""155"" height=""305"" border=""0"" usemap=""#hptheme"">';
		document.write(alternateContent);  // insert non-flash content
	}
</script>


<script language=""JavaScript"" type=""text/javascript"">
<!-- 
document.write('<div id=""crawlerContent"" style=""display:none"">'); 
// -->
</script>
<a href=""/daily/theme/beach.asp"">Beach Vacations</a><br>
<a href=""/daily/theme/adventure.asp"">Adventure Vacations</a><br>
<a href=""/daily/theme/family.asp"">Family Vacations</a><br>
<a href=""/daily/theme/romance.asp"">Romantic Vacations</a><br>
<a href=""/daily/vacations/ski/default.asp"">Ski Vacations</a><br>
<a href=""/daily/theme/luxury.asp"">Luxury Vacations</a><br>
<a href=""/daily/theme/all.asp"">See more...</a><br>
<script language=""JavaScript"" type=""text/javascript"">
<!-- 
document.write('</div>'); 
// -->
</script>

<map name=""hptheme"">
<area alt=""Beach"" coords=""2,30,153,156"" href=""/daily/theme/beach.asp"">
<area alt=""Adventure"" coords=""1,157,154,181"" href=""/daily/theme/adventure.asp"">
<area alt=""Family"" coords=""2,181,154,204"" href=""/daily/theme/family.asp"">
<area alt=""Romance"" coords=""2,204,154,229"" href=""/daily/theme/romance.asp"">
<area alt=""Ski"" coords=""2,229,155,252"" href=""/daily/vacations/ski/default.asp"">
<area alt=""Luxury"" coords=""3,252,153,280"" href=""/daily/theme/Luxury.asp"">
<area alt=""See more..."" coords=""5,300,67,283"" href=""/daily/theme/all.asp?mcicid=HP_morethemes"">
</map>
			</div>
		</div>
	
	</div>
	
</div>
<div class=""clearer""></div>
<div style=""height:6px;overflow:hidden;""></div>

<!--::434239::-->

<div class=""FlexChilddiv"">

	<div class=""FlexRow"" style=""margin:0px 0px 0px 0px;"">
	
	
		<div class=""FlexCol"" style=""width:152px;margin:4px 8px 0px 0px;"">
			<div class=""FlexInner"">
		<!--::434244::-->
<div class=""divS""><div class=""TBar"">World Heritage</div><div><div class=""divS1l"" style=""width:150px;""><p><a href=""/daily/vacations/world-heritage/default.asp?mcicid=HP_WH""><b>Explore world treasures</b></a></p><p>Trek to an ancient city high in Peru's cloud forest, or marvel at the world's most beautiful building.</p></div><div class=""clearer""></div></div></div>
			</div>
		</div>
	
		<div class=""FlexCol"" style=""width:152px;margin:4px 0px 0px 0px;"">
			<div class=""FlexInner"">
		<!--::434245::-->
<div class=""divS""><div class=""TBar"">TerraPass: Fly Green</div><div><div class=""divS1l"" style=""width:150px;""><p><a href=""/pubspec/scripts/eap.asp?GOTO=TSHOPSDETAIL&LocationID=178276&OfferingID=6779""><b>Fight global warming</b></a></p><p>Buy back the CO<sub>2</sub> emissions of your next flight! Pick up a TerraPass and help to fund clean energy. </p></div><div class=""clearer""></div></div></div>
			</div>
		</div>
	
	</div>
	
</div>
<div class=""clearer""></div>
			</div>
		</div>
	
		<div class=""FlexCol"" style=""width:150px;margin:0px 0px 0px 0px;"">
			<div class=""FlexInner"">
		<!--::434241::-->

<div id=""divRTILM"">
	<div class=""TBarRTI""><span>Last-minute deals</span></div>
    
    <div class=""divRTISection"" id=""divRTISection1"">
        
	    <div class=""divRTIItem"" id=""divRTIItem1_1""><a href=""/daily/deals/lastminute_deals/flightdeals.asp?mcicid=HP_LM1"">Last-minute flight values</a></div>
	    
	    <div class=""divRTIItem"" id=""divRTIItem1_2"">Vegas: <a href=""/daily/promos/vacations/las-vegas-last-minute/default.asp?mcicid=HP_LM2"">Hotels from <span class=""redlink"">$45</span></a></div>
	    
	    <div class=""divRTIItem"" id=""divRTIItem1_3"">NYC: <a href=""/promos/vacations/last_minute_NYC_deals/default.asp?mcicid=HP_LM3"">Air+hotel from <span class=""redlink"">$371</span></a></div>
	    
	    <div class=""divRTIItem"" id=""divRTIItem1_4"">Hot deals: <a href=""/daily/deals/expedia-hot-deals.asp?mcicid=HP_LM4"">Trips from <span class=""redlink"">$158</span></a></div>
	    
    </div>
    
	<div class=""divRTIBottomLink""><a href=""/daily/deals/lastminute_deals/default.asp?mcicid=HP_moreLM"">All last-minute deals</a></div>
	
</div>



<!--::434248::-->

<div id=""divRTIDaily"">
	<div class=""TBarRTI""><span>Special values</span></div>
    
    <div class=""divRTISection"" id=""divRTISection1"">
        
	    <div class=""divRTICaption"">Vacation Package Deals</div>
	    
	    <div class=""divRTIItem"" id=""divRTIItem1_1"">Hawaii: <a href=""/daily/promos/expedia-10th-anniversary/hawaii_vacation.asp?mcicid=10thAnnv_HP_PHWI"">Last night <span class=""redlink"">free</span>!</a></div>
	    
	    <div class=""divRTIItem"" id=""divRTIItem1_2"">Caribbean: <a href=""/daily/promos/expedia-10th-anniversary/caribbean_vacation.asp?mcicid=10thAnnv_HP_PCRBN"">Trips from <span class=""redlink"">$475</span></a></div>
	    
	    <div class=""divRTIItem"" id=""divRTIItem1_3"">Mexico: <a href=""/daily/promos/expedia-10th-anniversary/mexico_vacation.asp?mcicid=10thAnnv_HP_PMX"">Flight+hotel from <span class=""redlink"">$424</span></a></div>
	    
    </div>
    
    <div class=""divRTISection"" id=""divRTISection2"">
        
	    <div class=""divRTICaption"">Hotel Deals</div>
	    
	    <div class=""divRTIItem"" id=""divRTIItem2_1"">Orlando: <a href=""/daily/promos/expedia-10th-anniversary/orlando_vacation.asp?mcicid=10thAnnv_HP_HORL"">First night <span class=""redlink"">free</span></a></div>
	    
	    <div class=""divRTIItem"" id=""divRTIItem2_2"">NYC: <a href=""/daily/promos/expedia-10th-anniversary/new_york_city_vacation.asp?mcicid=10thAnnv_HP_HNY"">Save <span class=""redlink"">15&#37;</span></a></div>
	    
	    <div class=""divRTIItem"" id=""divRTIItem2_3"">Vegas: <a href=""/daily/promos/expedia-10th-anniversary/las_vegas_vacation.asp?mcicid=10thAnnv_HP_HLV"">3 stars from <span class=""redlink"">$45</span></a></div>
	    
    </div>
    
    <div class=""divRTISection"" id=""divRTISection3"">
        
	    <div class=""divRTICaption"">Cruise Deals</div>
	    
	    <div class=""divRTIItem"" id=""divRTIItem3_1"">Hawaii: <a href=""/daily/promos/expedia-10th-anniversary/hawaiian_cruises.asp"">7 nts. from <span class=""redlink"">$649</span></a></div>
	    
	    <div class=""divRTIItem"" id=""divRTIItem3_2""><a href=""/daily/promos/expedia-10th-anniversary/caribbean_cruises.asp?mcicid=10thAnnv_HP_CCRBN"">Caribbean cruises from <span class=""redlink"">$48</span> per day</a></div>
	    
	    <div class=""divRTIItem"" id=""divRTIItem3_3""><a href=""/daily/promos/expedia-10th-anniversary/mexico_cruises.asp?mcicid=10thAnnv_HP_CMX"">Cruise Mexico from <span class=""redlink"">$199</span>!</a></div>
	    
    </div>
    
    <div class=""divRTISection"" id=""divRTISection4"">
        
	    <div class=""divRTICaption"">Flight Deals</div>
	    
	    <div class=""divRTIItem"" id=""divRTIItem4_1""><a href=""/daily/deals/lastminute_deals/flightdeals.asp?mcicid=HP_FLT1"">Last-minute flight values</a></div>
	    
	    <div class=""divRTIItem"" id=""divRTIItem4_2""><a href=""/daily/promos/flights/ua_fall_winter_sale/default.asp?mcicid=HP_FLT2"">U.S. flights from <span class=""redlink"">$103</span>+</a></div>
	    
	    <div class=""divRTIItem"" id=""divRTIItem4_3""><a href=""/daily/deals/aircar/default.asp?mcicid=HP_FLT3"">See top flight deals</a></div>
	    
    </div>
    
	<div class=""divRTIBottomLink""><a href=""/daily/deals/promotions.asp?mcicid=HP_alldeals"">See more deals</a></div>
	
</div>


			</div>
		</div>
	
	</div>
	
</div>
<div class=""clearer""></div>

	</div>
</div>


<div id=""divFooter"">
<div id=""divBotNav"">

		<a href=""/daily/service/about.asp?rfrr=-950"" REL=""NOFOLLOW"">about Expedia.com</a>|
		<a href=""http://press.expedia.com/"" REL=""NOFOLLOW"">press room</a>|
		<a href=""http://investors.expediainc.com/phoenix.zhtml?c=190013&p=irol-irhome"" REL=""NOFOLLOW"">investor relations</a>|
		<a href=""/daily/service/legal.asp"" REL=""NOFOLLOW"">Expedia.com terms of use</a>|
		<a href=""/daily/service/privacy.asp"" REL=""NOFOLLOW"">updated privacy policy</a>|
		<a href=""/daily/associates/default.asp?rfrr=-952"" REL=""NOFOLLOW"">become an affiliate</a>|
		<a href=""/daily/exptrack/other/exppart_advertise.asp?rfrr=-954"" REL=""NOFOLLOW"">advertising</a>|
		<a href=""/daily/service/jobs/default.asp?rfrr=-1667"" REL=""NOFOLLOW"">jobs</a>
		
<br/>

		<a href=""/"">home</a>|
		<a href=""http://flights.expedia.com/"">flights</a>|
		<a href=""/hotels/Hotels_01.asp"">hotels</a>|
		<a href=""http://rental-cars.expedia.com/"">cars</a>|
		<a href=""/daily/cruises"">cruises</a>|
		<a href=""/daily/activities/activities.asp"">activities</a>|
		<a href=""/daily/sitetour/default.asp?rfrr=-951"">site map</a>
	
	<div id=""divLegal"">
	
		<a class=""explink"" href=""http://www.expedia.com"">Expedia</a>, Inc. is not responsible for content on external Web sites.&nbsp;&copy;2006 Expedia, Inc. All rights reserved.
		<br/>Photos: Courtesy of Getty Images, Corbis<br>
		<br><a href=""/daily/edit/fees.asp"">Plus sign (+) means taxes and fees are additional.</a>

	</div>
</div>

<div id=""divIntlSites"">
International sites:&nbsp;&nbsp;&nbsp;
<a class=""font10Arial"" href=""http://www.expedia.co.uk"">United Kingdom</a>|
<a class=""font10Arial"" href=""http://www.expedia.ca"">Canada</a>|
<a class=""font10Arial"" href=""http://www.expedia.de"">Germany</a>|
<a class=""font10Arial"" href=""http://www.expedia.fr"">France</a>|
<a class=""font10Arial"" href=""http://www.expedia.it"">Italy</a>|
<a class=""font10Arial"" href=""http://www.expedia.nl"">Netherlands</a>|
<a class=""font10Arial"" href=""http://www.expedia.com.au"">Australia</a>
</div>

<div id=""divPartner"">
Partner sites:&nbsp;&nbsp;&nbsp;
<a href=""http://www.vacationdeprivation.com"">Vacation Deprivation</a>|
<a href=""http://www.citysearch.com"">Citysearch</a>|
<a href=""http://www.evite.com"">Evite</a>|
<a href=""http://www.hotels.com"">Hotels.com</a>|
<a href=""http://www.hsn.com"">HSN</a>|
<a href=""http://www.ticketmaster.com"">Ticketmaster</a>|
<a href=""http://www.reserveamerica.com"">ReserveAmerica</a>|
<a href=""http://www.hotwire.com"">Hotwire</a>|
<a href=""http://www.lendingtree.com/stm/default.asp"">LendingTree</a>
<br/>
<a href=""http://www.realestate.com"">RealEstate.com</a>|
<a href=""http://www.gifts.com"">Gifts.com</a>|
<a href=""http://www.entertainment.com"">Entertainment.com</a>|
<a href=""http://www.match.com"">Match.com</a>|
<a href=""http://www.tripadvisor.com"">TripAdvisor</a>|
<a href=""http://www.condosaver.com"">CondoSaver.com</a>|
<a href=""http://www.classicvacations.com"">ClassicVacations.com</a>|
<a href=""http://www.improvementscatalog.com"">ImprovementsCatalog.com</a>
</div>

</div>

</div>

</body>
</html>
";

            XmlDocument doc;

            doc = HtmlParser.Parse(source);
            Assert.IsNotNull(doc["root"]["html"]["head"]);
            Assert.IsNotNull(doc["root"]["html"]["body"]);
        }

        //        [TestMethod]
        //        [TestProperty("Lib", "LillTek.Advanced")]
        //        public void Parse_New() {

        //            // Generate a DOM source scraped from www.msn.com and 
        //            // make sure we don't see an exception.

        //            const string    source =
        //@"
        //";

        //            XmlDocument doc;

        //            doc = HtmlParser.Parse(source);
        //            Assert.IsNotNull(doc["root"]["html"]["head"]);
        //            Assert.IsNotNull(doc["root"]["html"]["body"]);
        //        }
    }
}

