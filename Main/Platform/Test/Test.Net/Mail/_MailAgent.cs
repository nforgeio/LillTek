//-----------------------------------------------------------------------------
// FILE:        _MailAgent.cs
// CONTRIBUTOR: Jeff Lill
// COPYRIGHT:   Copyright (c) 2005-2015 by Jeffrey Lill.  All rights reserved.
// DESCRIPTION: Unit tests

using System;
using System.IO;
using System.Text;
using System.Diagnostics;
using System.Net;
using System.Net.Mail;
using System.Net.Mime;
using System.Threading;

using Microsoft.VisualStudio.TestTools.UnitTesting;

using LillTek.Common;

namespace LillTek.Net.Mail.Test
{
    [TestClass]
    public class _MailAgent
    {
        private const string Server = "mail.blackmoon.com:SMTP";
        private const string Account = "schedule@paraworks";
        private const string Password = "Math.Mail";

        [TestMethod]
        [TestProperty("Lib", "LillTek.Net.Mail")]
        public void MailAgent_BasicDelivery()
        {
            // Make sure that we can queue and deliver several messages.

            string folder = Path.Combine(Path.GetTempPath(), "MailAgent");

            using (var agent = new MailAgent(new NetworkBinding(Server), Account, Password, folder, TimeSpan.FromSeconds(1)))
            {
                MailMessage message;

                for (int i = 0; i < 10; i++)
                {
                    message = new MailMessage("jeff@lill-home.com", "jeff@lilltek.com");
                    message.Subject = string.Format("Test Message #{0}", i);
                    message.Body = "This is a test of the emergency broadcasting system.\r\nThis is only a test.\r\n";

                    agent.Enqueue(message);
                }

                Thread.Sleep(20000);    // Wait 20 seconds to give the agent a chance to deliver the messages.
            }
        }

        [TestMethod]
        [TestProperty("Lib", "LillTek.Net.Mail")]
        public void MailAgent_ExtendedDelivery()
        {
            // Make sure that we can queue and deliver several messages over an
            // extended period of time where the background thread will perform
            // multiple polls.

            string folder = Path.Combine(Path.GetTempPath(), "MailAgent");

            using (var agent = new MailAgent(new NetworkBinding(Server), Account, Password, folder, TimeSpan.FromSeconds(1)))
            {
                MailMessage message;

                for (int i = 0; i < 10; i++)
                {
                    message = new MailMessage("jeff@lill-home.com", "jeff@lilltek.com");
                    message.Subject = string.Format("Test Message #{0}", i);
                    message.Body = "This is a test of the emergency broadcasting system.\r\nThis is only a test.\r\n";

                    agent.Enqueue(message);
                    Thread.Sleep(2000);
                }

                Thread.Sleep(5000); // Wait 5 seconds to give the agent a chance to deliver the messages.
            }
        }
    }
}

