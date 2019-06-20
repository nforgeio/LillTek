//-----------------------------------------------------------------------------
// FILE:        _MsgEP.cs
// CONTRIBUTOR: Jeff Lill
// COPYRIGHT:   Copyright (c) 2005-2015 by Jeffrey Lill.  All rights reserved.
// DESCRIPTION: Unit tests for _MsgEP

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Net;
using System.Net.Sockets;
using System.Reflection;
using System.Threading;

using Microsoft.VisualStudio.TestTools.UnitTesting;

using LillTek.Common;
using LillTek.Cryptography;
using LillTek.Messaging;
using LillTek.Testing;

namespace LillTek.Messaging.Test
{
    [TestClass]
    public class _MsgEP
    {
        [TestMethod]
        [TestProperty("Lib", "LillTek.Messaging")]
        public void MsgEP_Casting()
        {
            MsgEP ep;
            string v;

            ep = (string)null;
            Assert.IsNull(ep);

            ep = (Uri)null;
            Assert.IsNull(ep);

            v = (MsgEP)null;
            Assert.IsNull(v);

            ep = "logical://foo/bar";
            Assert.AreEqual((MsgEP)"logical://foo/bar", ep);

            v = MsgEP.Parse("logical://foo/bar");
            Assert.AreEqual("logical://foo/bar", v);

            ep = new Uri("logical://foo/bar");
            Assert.AreEqual("logical://foo/bar", ep.ToString());
        }

        [TestMethod]
        [TestProperty("Lib", "LillTek.Messaging")]
        public void MsgEP_Physical_Basic()
        {
            MsgEP ep;

            ep = new MsgEP("physical://root:70/hub/leaf");
            Assert.AreEqual("root", ep.RootHost);
            Assert.AreEqual(70, ep.RootPort);
            CollectionAssert.AreEqual(new string[] { "hub", "leaf" }, ep.Segments);
            Assert.IsNull(ep.ChannelEP);
            Assert.IsNull(ep.ObjectID);
            Assert.AreEqual("physical://root:70/hub/leaf", ep.ToString());
            Assert.IsTrue(ep.IsPhysical);
            Assert.IsFalse(ep.IsLogical);
            Assert.IsFalse(ep.IsChannel);
            Assert.IsFalse(ep.IsNull);

            ep = "physical://root:70/hub/leaf";
            Assert.AreEqual("root", ep.RootHost);
            Assert.AreEqual(70, ep.RootPort);
            CollectionAssert.AreEqual(new string[] { "hub", "leaf" }, ep.Segments);
            Assert.IsNull(ep.ChannelEP);
            Assert.IsNull(ep.ObjectID);
            Assert.AreEqual("physical://root:70/hub/leaf", (string)ep);
            Assert.IsTrue(ep.IsPhysical);
            Assert.IsFalse(ep.IsLogical);
            Assert.IsFalse(ep.IsChannel);

            ep = new MsgEP("physical://ROOT:70/HUB/LEAF");
            Assert.AreEqual("root", ep.RootHost);
            Assert.AreEqual(70, ep.RootPort);
            CollectionAssert.AreEqual(new string[] { "hub", "leaf" }, ep.Segments);
            Assert.IsNull(ep.ChannelEP);
            Assert.IsNull(ep.ObjectID);
            Assert.AreEqual("physical://root:70/hub/leaf", ep.ToString());

            ep = new MsgEP("physical://ROOT:70/HUB/LEAF/ExtraCrap");
            Assert.AreEqual("root", ep.RootHost);
            Assert.AreEqual(70, ep.RootPort);
            CollectionAssert.AreEqual(new string[] { "hub", "leaf", "extracrap" }, ep.Segments);
            Assert.IsNull(ep.ChannelEP);
            Assert.IsNull(ep.ObjectID);
            Assert.AreEqual("physical://root:70/hub/leaf/extracrap", ep.ToString());

            ep = new MsgEP("physical://ROOT:70/HUB/LEAF/?ExtraCrap");
            Assert.AreEqual("root", ep.RootHost);
            Assert.AreEqual(70, ep.RootPort);
            CollectionAssert.AreEqual(new string[] { "hub", "leaf" }, ep.Segments);
            Assert.IsNull(ep.ChannelEP);
            Assert.IsNull(ep.ObjectID);
            Assert.AreEqual("physical://root:70/hub/leaf", ep.ToString());

            ep = new MsgEP("physical://root:70/hub/");
            Assert.AreEqual("root", ep.RootHost);
            Assert.AreEqual(70, ep.RootPort);
            CollectionAssert.AreEqual(new string[] { "hub" }, ep.Segments);
            Assert.IsNull(ep.ChannelEP);
            Assert.IsNull(ep.ObjectID);
            Assert.AreEqual("physical://root:70/hub", ep.ToString());

            ep = new MsgEP("physical://root:70/hub");
            Assert.AreEqual("root", ep.RootHost);
            Assert.AreEqual(70, ep.RootPort);
            CollectionAssert.AreEqual(new string[] { "hub" }, ep.Segments);
            Assert.IsNull(ep.ChannelEP);
            Assert.IsNull(ep.ObjectID);
            Assert.AreEqual("physical://root:70/hub", ep.ToString());

            ep = new MsgEP("physical://root:10005/");
            Assert.AreEqual("root", ep.RootHost);
            Assert.AreEqual(10005, ep.RootPort);
            CollectionAssert.AreEqual(new string[0], ep.Segments);
            Assert.IsNull(ep.ChannelEP);
            Assert.IsNull(ep.ObjectID);
            Assert.AreEqual("physical://root:10005", ep.ToString());

            ep = new MsgEP("physical://root:70");
            Assert.AreEqual("root", ep.RootHost);
            Assert.AreEqual(70, ep.RootPort);
            CollectionAssert.AreEqual(new string[0], ep.Segments);
            Assert.IsNull(ep.ChannelEP);
            Assert.IsNull(ep.ObjectID);
            Assert.AreEqual("physical://root:70", ep.ToString());

            ep = new MsgEP("physical://root");
            Assert.AreEqual("root", ep.RootHost);
            Assert.AreEqual(-1, ep.RootPort);
            CollectionAssert.AreEqual(new string[0], ep.Segments);
            Assert.IsNull(ep.ChannelEP);
            Assert.IsNull(ep.ObjectID);
            Assert.AreEqual("physical://root", ep.ToString());

            ep = new MsgEP("physical://?c=tcp://127.0.0.1:3333");
            Assert.IsTrue(ep.IsPhysical);
            Assert.IsTrue(ep.IsChannel);
            Assert.IsFalse(ep.IsLogical);
            Assert.AreEqual(ChannelEP.Parse("tcp://127.0.0.1:3333"), ep.ChannelEP);
            Assert.IsNull(ep.RootHost);
            Assert.AreEqual(-1, ep.RootPort);
            CollectionAssert.AreEqual(new string[0], ep.Segments);
            Assert.AreEqual("physical://?c=tcp://127.0.0.1:3333", ep.ToString());

            ep = new MsgEP(ChannelEP.Parse("tcp://1.2.3.4:1111"));
            Assert.IsTrue(ep.IsPhysical);
            Assert.IsTrue(ep.IsChannel);
            Assert.IsFalse(ep.IsLogical);
            Assert.AreEqual(ChannelEP.Parse("tcp://1.2.3.4:1111"), ep.ChannelEP);
            Assert.IsNull(ep.RootHost);
            Assert.AreEqual(-1, ep.RootPort);
            CollectionAssert.AreEqual(new string[0], ep.Segments);
            Assert.AreEqual("physical://?c=tcp://1.2.3.4:1111", ep.ToString());
        }

        [TestMethod]
        [TestProperty("Lib", "LillTek.Messaging")]
        public void MsgEP_RemoveLastSlash()
        {
            // WCF likes to add terminating slashes to its URIs.  We need
            // to remove these for LillTek messaging.

            Assert.AreEqual("physical://root/hub", (string)MsgEP.Parse("physical://root/hub/"));
            Assert.AreEqual("logical://root/hub", (string)MsgEP.Parse("logical://root/hub/"));
            Assert.AreEqual("logical://root/hub", (string)MsgEP.Parse("abstract://root/hub/"));
        }

        [TestMethod]
        [TestProperty("Lib", "LillTek.Messaging")]
        public void MsgEP_Physical_Query()
        {
            MsgEP ep;

            ep = MsgEP.Parse("physical://root?o=foo");
            Assert.AreEqual("foo", ep.ObjectID);

            ep = MsgEP.Parse("physical://root?o=");
            Assert.AreEqual("", ep.ObjectID);

            ep = MsgEP.Parse("physical://root?c=tcp://127.0.0.1:55");
            Assert.AreEqual("tcp://127.0.0.1:55", ep.ChannelEP.ToString());

            ep = MsgEP.Parse("physical://root?c=tcp://127.0.0.1:55&o=foobar");
            Assert.AreEqual("tcp://127.0.0.1:55", ep.ChannelEP.ToString());
            Assert.AreEqual("foobar", ep.ObjectID);

            ep = MsgEP.Parse("physical://root?c=tcp://127.0.0.1:55&o=foobar&");
            Assert.AreEqual("tcp://127.0.0.1:55", ep.ChannelEP.ToString());
            Assert.AreEqual("foobar", ep.ObjectID);
        }

        [TestMethod]
        [TestProperty("Lib", "LillTek.Messaging")]
        public void MsgEP_Physical_EscapeInput()
        {
            MsgEP ep;

            ep = MsgEP.Parse("physical://root%20node/hub%20node/leaf%20node?o=hello%20world&c=mcast://*:77");
            Assert.AreEqual("root node", ep.RootHost);
            CollectionAssert.AreEqual(new string[] { "hub node", "leaf node" }, ep.Segments);
            Assert.AreEqual("hello world", ep.ObjectID);
            Assert.AreEqual("mcast://*:77", ep.ChannelEP.ToString());
        }

        [TestMethod]
        [TestProperty("Lib", "LillTek.Messaging")]
        public void MsgEP_Physical_EscapeOutput()
        {
            MsgEP ep;

            ep = new MsgEP();
            ep.IsPhysical = true;
            ep.RootHost = "root node";
            ep.Segments = new string[] { "hub node", "leaf node" };
            ep.ObjectID = "object=id& test";
            ep.ChannelEP = new ChannelEP("mcast://*:10");

            Assert.AreEqual("physical://root%20node/hub%20node/leaf%20node?o=object%3did%26%20test&c=mcast://*:10", ep.ToString());
        }

        [TestMethod]
        [TestProperty("Lib", "LillTek.Messaging")]
        public void MsgEP_Physical_Compare()
        {
            Assert.IsTrue(MsgEP.Parse("physical://foo.com:70").IsPhysicalRoot);
            Assert.IsTrue(MsgEP.Parse("physical://foo.com:70?c=tcp://127.0.0.1:77").IsPhysicalRoot);
            Assert.IsFalse(MsgEP.Parse("physical://foo.com:70/bar?c=tcp://127.0.0.1:77").IsPhysicalRoot);
            Assert.IsFalse(MsgEP.Parse("physical://?c=tcp://127.0.0.1:77").IsPhysicalRoot);

            Assert.IsTrue(MsgEP.Parse("physical://foo.com:70").IsPhysicalMatch("physical://foo.com:70"));
            Assert.IsTrue(MsgEP.Parse("physical://foo.com:70?o=77").IsPhysicalMatch("physical://foo.com:70"));
            Assert.IsTrue(MsgEP.Parse("physical://foo.com:70").IsPhysicalMatch("physical://foo.com:70?o=77"));
            Assert.IsTrue(MsgEP.Parse("physical://foo.com:70/bar").IsPhysicalMatch("physical://foo.com:70/bar"));
            Assert.IsTrue(MsgEP.Parse("physical://foo.com:70/bar").IsPhysicalMatch("physical://foo.com:70/bar?/c=tcp:127.0.0.1:80"));
            Assert.IsFalse(MsgEP.Parse("physical://foo.com:60").IsPhysicalMatch("physical://foo.com:70"));
            Assert.IsFalse(MsgEP.Parse("physical://foox.com:70").IsPhysicalMatch("physical://foo.com:70"));
            Assert.IsFalse(MsgEP.Parse("physical://foo.com:70/bar").IsPhysicalMatch("physical://foo.com:70"));
            Assert.IsFalse(MsgEP.Parse("physical://foo.com:70").IsPhysicalMatch("physical://foo.com:70/bar"));

            Assert.IsTrue(MsgEP.Parse("physical://foo.com:70").IsPhysicalDescendant(MsgEP.Parse("physical://foo.com:70/bar")));
            Assert.IsTrue(MsgEP.Parse("physical://foo.com:70").IsPhysicalDescendant(MsgEP.Parse("physical://foo.com:70/bar/foobar")));
            Assert.IsFalse(MsgEP.Parse("physical://foo.com:70").IsPhysicalDescendant(MsgEP.Parse("physical://foo.com:70")));
            Assert.IsFalse(MsgEP.Parse("physical://foo.com:70").IsPhysicalDescendant(MsgEP.Parse("physical://xfoo.com:70")));
            Assert.IsFalse(MsgEP.Parse("physical://foo.com:70").IsPhysicalDescendant(MsgEP.Parse("physical://foo.com:60")));
        }

        [TestMethod]
        [TestProperty("Lib", "LillTek.Messaging")]
        public void MsgEP_Physical_GetParent()
        {
            Assert.AreEqual("physical://foo.com:70", MsgEP.Parse("physical://foo.com:70/bar").GetPhysicalParent().ToString());
            Assert.AreEqual("physical://foo.com:70", MsgEP.Parse("physical://foo.com:70/bar").GetPhysicalParent().ToString());
            Assert.AreEqual("physical://foo.com:70/bar", MsgEP.Parse("physical://foo.com:70/bar/foobar").GetPhysicalParent().ToString());
            Assert.IsNull(MsgEP.Parse("physical://foo.com:70").GetPhysicalParent());
        }

        [TestMethod]
        [TestProperty("Lib", "LillTek.Messaging")]
        public void MsgEP_Physical_IsPeer()
        {
            Assert.IsTrue(MsgEP.Parse("physical://foo.com:70").IsPhysicalPeer(MsgEP.Parse("physical://foo.com:70")));
            Assert.IsTrue(MsgEP.Parse("physical://foo.com:70/bar").IsPhysicalPeer(MsgEP.Parse("physical://foo.com:70/bar")));
            Assert.IsTrue(MsgEP.Parse("physical://foo.com:70/bar/foo").IsPhysicalPeer(MsgEP.Parse("physical://foo.com:70/bar/bar")));

            Assert.IsFalse(MsgEP.Parse("physical://foo.com:70").IsPhysicalPeer(MsgEP.Parse("physical://foobar.com:70")));
            Assert.IsFalse(MsgEP.Parse("physical://foo.com:70").IsPhysicalPeer(MsgEP.Parse("physical://foo.com:80")));
            Assert.IsFalse(MsgEP.Parse("physical://foo.com:70/foo").IsPhysicalPeer(MsgEP.Parse("physical://foo.com:70")));
            Assert.IsFalse(MsgEP.Parse("physical://foo.com:70").IsPhysicalPeer(MsgEP.Parse("physical://foo.com:70/foo")));
            Assert.IsFalse(MsgEP.Parse("physical://foo.com:70/foo").IsPhysicalPeer(MsgEP.Parse("physical://foo.com:70")));
            Assert.IsFalse(MsgEP.Parse("physical://foo.com:70/bar/foo").IsPhysicalPeer(MsgEP.Parse("physical://foo.com:70/foo/foo")));
        }

        [TestMethod]
        [TestProperty("Lib", "LillTek.Messaging")]
        public void MsgEP_Logical_Basic()
        {
            MsgEP ep;

            ep = MsgEP.Parse("logical://foo");
            Assert.IsTrue(ep.IsLogical);
            Assert.IsFalse(ep.IsPhysical);
            Assert.AreEqual(1, ep.Segments.Length);
            Assert.AreEqual("foo", ep.Segments[0]);
            Assert.IsFalse(ep.HasWildCard);
            Assert.IsFalse(ep.IsNull);

            ep = MsgEP.Parse("logical://foo/bar");
            Assert.AreEqual(2, ep.Segments.Length);
            Assert.AreEqual("foo", ep.Segments[0]);
            Assert.AreEqual("bar", ep.Segments[1]);
            Assert.IsFalse(ep.HasWildCard);

            ep = MsgEP.Parse("logical://foo/bar/*");
            Assert.AreEqual(3, ep.Segments.Length);
            Assert.AreEqual("foo", ep.Segments[0]);
            Assert.AreEqual("bar", ep.Segments[1]);
            Assert.AreEqual("*", ep.Segments[2]);
            Assert.IsTrue(ep.HasWildCard);

            Assert.IsTrue(MsgEP.Parse("logical://foo").Equals(MsgEP.Parse("logical://foo")));
            Assert.IsTrue(MsgEP.Parse("logical://foo/bar").Equals(MsgEP.Parse("logical://foo/bar")));
            Assert.IsTrue(MsgEP.Parse("logical://foo/bar/*").Equals(MsgEP.Parse("logical://foo/bar/*")));
            Assert.IsFalse(MsgEP.Parse("logical://foo/bar").Equals(MsgEP.Parse("physical://foo/bar")));
            Assert.IsFalse(MsgEP.Parse("physical://foo/bar").Equals(MsgEP.Parse("logical://foo/bar")));

            ep = MsgEP.Parse("logical://null");
            Assert.IsTrue(ep.IsNull);

            ep = MsgEP.Parse("logical://null/test");
            Assert.IsTrue(ep.IsNull);

            // We strip terminating "/" characters now

            Assert.AreEqual("logical://foo", (string)MsgEP.Parse("logical://foo/"));

            try
            {
                MsgEP.Parse("xxx://foo/bar");
                Assert.Fail("Must verify invalid scheme.");
            }
            catch (Exception e)
            {
                Assert.IsInstanceOfType(e, typeof(ArgumentException));
            }

            try
            {
                MsgEP.Parse("logical://");
                Assert.Fail("Must verify that segments exist.");
            }
            catch (Exception e)
            {
                Assert.IsInstanceOfType(e, typeof(ArgumentException));
            }

            try
            {

                MsgEP.Parse("logical:////");
                Assert.Fail("Must verify that segments are not empty.");
            }
            catch (Exception e)
            {
                Assert.IsInstanceOfType(e, typeof(ArgumentException));
            }

            try
            {
                MsgEP.Parse("logical://foo?bar=test");
                Assert.Fail("Must verify that logical endpoints don't allow queries.");
            }
            catch (Exception e)
            {
                Assert.IsInstanceOfType(e, typeof(ArgumentException));
            }
        }

        [TestMethod]
        [TestProperty("Lib", "LillTek.Messaging")]
        public void MsgEP_Logical_EscapeInput()
        {
            MsgEP ep;

            ep = MsgEP.Parse("logical://root%20node/hub%20node/leaf%20node");
            Assert.IsTrue(ep.IsLogical);
            CollectionAssert.AreEqual(new string[] { "root node", "hub node", "leaf node" }, ep.Segments);
        }

        [TestMethod]
        [TestProperty("Lib", "LillTek.Messaging")]
        public void MsgEP_Logical_EscapeOutput()
        {
            MsgEP ep;

            ep = new MsgEP();
            ep.IsPhysical = false;
            ep.Segments = new string[] { "root node", "hub node", "leaf node" };

            Assert.AreEqual("logical://root%20node/hub%20node/leaf%20node", ep.ToString());
        }

        [TestMethod]
        [TestProperty("Lib", "LillTek.Messaging")]
        public void MsgEP_Logical_Match()
        {
            Assert.IsTrue(MsgEP.Parse("logical://*").LogicalMatch("logical://*"));
            Assert.IsTrue(MsgEP.Parse("logical://*").LogicalMatch("logical://foo"));
            Assert.IsTrue(MsgEP.Parse("logical://foo").LogicalMatch("logical://*"));
            Assert.IsTrue(MsgEP.Parse("logical://*").LogicalMatch("logical://foo/*"));
            Assert.IsTrue(MsgEP.Parse("logical://foo/*").LogicalMatch("logical://*"));
            Assert.IsTrue(MsgEP.Parse("logical://*").LogicalMatch("logical://foo/bar"));
            Assert.IsTrue(MsgEP.Parse("logical://foo/bar").LogicalMatch("logical://*"));
            Assert.IsTrue(MsgEP.Parse("logical://foo").LogicalMatch("logical://foo"));
            Assert.IsTrue(MsgEP.Parse("logical://foo/bar").LogicalMatch("logical://foo/*"));
            Assert.IsTrue(MsgEP.Parse("logical://foo/*").LogicalMatch("logical://foo"));
            Assert.IsTrue(MsgEP.Parse("logical://foo/*").LogicalMatch("logical://foo/*"));
            Assert.IsTrue(MsgEP.Parse("logical://foo/*").LogicalMatch("logical://foo/bar/*"));

            Assert.IsFalse(MsgEP.Parse("logical://foo").LogicalMatch("logical://bar"));
            Assert.IsFalse(MsgEP.Parse("logical://foo/bar").LogicalMatch("logical://foo"));
            Assert.IsFalse(MsgEP.Parse("logical://foo").LogicalMatch("logical://bar"));
            Assert.IsFalse(MsgEP.Parse("logical://foo").LogicalMatch("logical://foo/bar"));
            Assert.IsFalse(MsgEP.Parse("logical://foo/*").LogicalMatch("logical://bar/*"));
            Assert.IsFalse(MsgEP.Parse("logical://foo/bar").LogicalMatch("logical://foo/bar/foobar/*"));
        }

        [TestMethod]
        [TestProperty("Lib", "LillTek.Messaging")]
        public void MsgEP_SetChannelEP()
        {
            MsgEP ep;

            ep = "physical://foo/bar";
            Assert.AreEqual("physical://foo/bar", ep.ToString(-1, true));
            Assert.IsNull(ep.ChannelEP);

            ep.ChannelEP = "tcp://1.2.3.4:5";
            Assert.AreEqual("physical://foo/bar?c=tcp://1.2.3.4:5", ep.ToString(-1, true));
            Assert.IsNotNull(ep.ChannelEP);

            ep.ChannelEP = null;
            Assert.AreEqual("physical://foo/bar", ep.ToString(-1, true));
            Assert.IsNull(ep.ChannelEP);
        }

        [TestMethod]
        [TestProperty("Lib", "LillTek.Messaging")]
        public void MsgEP_Instance()
        {
            MsgEP ep;

            ep = MsgEP.Parse("logical://foo", "bar");
            Assert.AreEqual("logical://foo/bar", ep.ToString());

            ep = MsgEP.Parse("logical://foo", "/bar");
            Assert.AreEqual("logical://foo/bar", ep.ToString());
        }

        [TestMethod]
        [TestProperty("Lib", "LillTek.Messaging")]
        public void MsgEP_Abstract()
        {
            string settings =
@"
&set root myroot

MsgRouter.AbstractMap[abstract://test one] = logical://test/one
MsgRouter.AbstractMap[abstract://foo]      = logical://bar
MsgRouter.AbstractMap[abstract://foobar]   = logical://$(root)/foobar
";
            Config.SetConfig(settings.Replace('&', '#'));
            MsgEP.LoadAbstractMap();

            Assert.AreEqual("logical://test/one", MsgEP.Parse("abstract://test one").ToString());
            Assert.AreEqual("logical://bar", MsgEP.Parse("abstract://foo").ToString());
            Assert.AreEqual("logical://bar", MsgEP.Parse("ABSTRACT://FOO").ToString());
            Assert.AreEqual("logical://myroot/foobar", MsgEP.Parse("abstract://foobar").ToString());
            Assert.AreEqual("logical://notfound", MsgEP.Parse("abstract://notfound").ToString());
            Assert.AreEqual("logical://bar/bar", new MsgEP("abstract://foo", "bar").ToString());
        }

        [TestMethod]
        [TestProperty("Lib", "LillTek.Messaging")]
        public void MsgEP_Compare()
        {
            Assert.AreEqual(0, MsgEP.Compare("logical://test/*", "logical://test/*"));
            Assert.AreEqual(0, MsgEP.Compare("logical://aa", "logical://AA"));
            Assert.AreEqual(-1, MsgEP.Compare("logical://aa", "logical://bb"));
            Assert.AreEqual(+1, MsgEP.Compare("logical://BB", "logical://aa"));
        }

        [TestMethod]
        [TestProperty("Lib", "LillTek.Messaging")]
        public void MsgEP_Broadcast_Logical()
        {
            MsgEP ep;

            ep = new MsgEP("logical://seg0/seg1");
            Assert.IsTrue(ep.IsLogical);
            Assert.IsFalse(ep.Broadcast);
            Assert.AreEqual("logical://seg0/seg1", ep.ToString());
            ep = ep.Clone();
            Assert.IsFalse(ep.Broadcast);
            Assert.AreEqual("logical://seg0/seg1", ep.ToString());

            ep = new MsgEP("logical://seg0/seg1?broadcast");
            Assert.IsTrue(ep.Broadcast);
            Assert.AreEqual("logical://seg0/seg1?broadcast", ep.ToString());
            ep = ep.Clone();
            Assert.IsTrue(ep.Broadcast);
            Assert.AreEqual("logical://seg0/seg1?broadcast", ep.ToString());

            ep = new MsgEP("logical://seg0/seg1?BROADCAST");
            Assert.IsTrue(ep.Broadcast);
            Assert.AreEqual("logical://seg0/seg1?broadcast", ep.ToString());
            ep = ep.Clone();
            Assert.IsTrue(ep.Broadcast);
            Assert.AreEqual("logical://seg0/seg1?broadcast", ep.ToString());

            try
            {
                ep = new MsgEP("logical://seg0/seg1?badparam=5");
                Assert.Fail("Expected an ArgumentException");
            }
            catch (Exception e)
            {
                Assert.IsInstanceOfType(e, typeof(ArgumentException));
            }

            ep = new MsgEP("logical://seg0/seg1");
            ep.Broadcast = true;
            Assert.IsTrue(ep.Broadcast);
            Assert.AreEqual("logical://seg0/seg1?broadcast", ep.ToString());

            ep = new MsgEP("logical://seg0/seg1?broadcast");
            ep.Broadcast = false;
            Assert.IsFalse(ep.Broadcast);
            Assert.AreEqual("logical://seg0/seg1", ep.ToString());

            // Compare() does not ignore the broadcast parameter.

            Assert.IsTrue(MsgEP.Compare("logical://seg0/seg1", "logical://seg0/seg1") == 0);
            Assert.IsTrue(MsgEP.Compare("logical://seg0/seg1", "logical://seg0/seg1?broadcast") != 0);

            // Equals() and LogicalMatch() ignore the broadcast parameter.

            Assert.AreEqual((MsgEP)"logical://seg0/seg1", (MsgEP)"logical://seg0/seg1");
            Assert.AreEqual((MsgEP)"logical://seg0/seg1", (MsgEP)"logical://seg0/seg1?broadcast");

            Assert.IsTrue(new MsgEP("logical://seg0/seg1").LogicalMatch("logical://seg0/seg1"));
            Assert.IsTrue(new MsgEP("logical://seg0/seg1").LogicalMatch("logical://seg0/seg1?broadcast"));
            Assert.IsTrue(new MsgEP("logical://seg0/seg1?broadcast").LogicalMatch("logical://seg0/seg1"));
            Assert.IsTrue(new MsgEP("logical://seg0/seg1?broadcast").LogicalMatch("logical://seg0/seg1?broadcast"));
        }

        [TestMethod]
        [TestProperty("Lib", "LillTek.Messaging")]
        public void MsgEP_Broadcast_Physical()
        {
            MsgEP ep;

            ep = new MsgEP("physical://seg0/seg1");
            Assert.IsTrue(ep.IsPhysical);
            Assert.IsFalse(ep.Broadcast);
            Assert.AreEqual("physical://seg0/seg1", ep.ToString());
            ep = ep.Clone();
            Assert.IsFalse(ep.Broadcast);
            Assert.AreEqual("physical://seg0/seg1", ep.ToString());

            ep = new MsgEP("physical://seg0/seg1?broadcast");
            Assert.IsTrue(ep.Broadcast);
            Assert.AreEqual("physical://seg0/seg1?broadcast", ep.ToString());
            ep = ep.Clone();
            Assert.IsTrue(ep.Broadcast);
            Assert.AreEqual("physical://seg0/seg1?broadcast", ep.ToString());

            ep = new MsgEP("physical://seg0/seg1?BROADCAST");
            Assert.IsTrue(ep.Broadcast);
            Assert.AreEqual("physical://seg0/seg1?broadcast", ep.ToString());
            ep = ep.Clone();
            Assert.IsTrue(ep.Broadcast);
            Assert.AreEqual("physical://seg0/seg1?broadcast", ep.ToString());

            ep = new MsgEP("physical://seg0/seg1");
            ep.Broadcast = true;
            Assert.IsTrue(ep.Broadcast);
            Assert.AreEqual("physical://seg0/seg1?broadcast", ep.ToString());

            ep = new MsgEP("physical://seg0/seg1?broadcast");
            ep.Broadcast = false;
            Assert.IsFalse(ep.Broadcast);
            Assert.AreEqual("physical://seg0/seg1", ep.ToString());

            // Compare() does not ignore the broadcast parameter.

            Assert.IsTrue(MsgEP.Compare("physical://seg0/seg1", "physical://seg0/seg1") == 0);
            Assert.IsTrue(MsgEP.Compare("physical://seg0/seg1", "physical://seg0/seg1?broadcast") != 0);

            // Equals() ignores the broadcast parameter.

            Assert.AreEqual((MsgEP)"physical://seg0/seg1", (MsgEP)"physical://seg0/seg1");
            Assert.AreEqual((MsgEP)"physical://seg0/seg1", (MsgEP)"physical://seg0/seg1?broadcast");
        }

        [TestMethod]
        [TestProperty("Lib", "LillTek.Messaging")]
        public void MsgEP_Clone_ResetBroadcast()
        {
            MsgEP ep;

            ep = "logical://test";
            Assert.IsFalse(ep.Broadcast);
            ep = ep.Clone();
            Assert.IsFalse(ep.Broadcast);
            Assert.AreEqual("logical://test", ep.ToString());

            ep = "logical://test?broadcast";
            Assert.IsTrue(ep.Broadcast);
            ep = ep.Clone();
            Assert.IsTrue(ep.Broadcast);
            Assert.AreEqual("logical://test?broadcast", ep.ToString());

            ep = "logical://test?broadcast";
            Assert.IsTrue(ep.Broadcast);
            ep = ep.Clone(false);
            Assert.IsTrue(ep.Broadcast);
            Assert.AreEqual("logical://test?broadcast", ep.ToString());

            ep = "logical://test?broadcast";
            Assert.IsTrue(ep.Broadcast);
            ep = ep.Clone(true);
            Assert.IsFalse(ep.Broadcast);
            Assert.AreEqual("logical://test", ep.ToString());
        }
    }
}

