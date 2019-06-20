//-----------------------------------------------------------------------------
// FILE:        _MailHelper.cs
// CONTRIBUTOR: Jeff Lill
// COPYRIGHT:   Copyright (c) 2005-2015 by Jeffrey Lill.  All rights reserved.
// DESCRIPTION: Unit tests


using System;
using System.Text;
using System.Diagnostics;
using System.Net;
using System.Net.Mail;
using System.Net.Mime;

using Microsoft.VisualStudio.TestTools.UnitTesting;

using LillTek.Common;

namespace LillTek.Net.Mail.Test
{
    [TestClass]
    public class _MailHelper
    {
        [TestMethod]
        [TestProperty("Lib", "LillTek.Net.Mail")]
        public void MailHelper_MessageSerializeBasic()
        {
            MailMessage message;
            EnhancedMemoryStream stream;

            // Write

            message = new MailMessage("jeff@lilltek.com", "joe@bloe.com");
            message.From = new MailAddress("jeff@lilltek.com");
            message.Subject = "Test Message";
            message.IsBodyHtml = true;
            message.Body = "<html><body>Test</body></html>";

            stream = new EnhancedMemoryStream();
            MailHelper.WriteMessage(stream, message);

            // Read

            stream.Position = 0;
            message = MailHelper.ReadMessage(stream);

            Assert.AreEqual("jeff@lilltek.com", message.From.ToString());
            Assert.AreEqual(1, message.To.Count);
            Assert.AreEqual("joe@bloe.com", message.To[0].ToString());

            Assert.AreEqual("Test Message", message.Subject);
            Assert.IsTrue(message.IsBodyHtml);
            Assert.AreEqual("<html><body>Test</body></html>", message.Body);
        }

        [TestMethod]
        [TestProperty("Lib", "LillTek.Net.Mail")]
        public void MailHelper_MessageSerializeAdvanced()
        {
            MailMessage message;
            EnhancedMemoryStream stream;
            Attachment attachment;
            EnhancedMemoryStream dataStream;

            // Write

            message = new MailMessage("jeff@lilltek.com", "joe@bloe.com");
            message.Priority = MailPriority.High;
            message.From = new MailAddress("jeff@lilltek.com");
            message.Sender = new MailAddress("sender@lilltek.com");
            message.Subject = "Test Message";
            message.SubjectEncoding = Encoding.UTF8;
            message.IsBodyHtml = true;
            message.Body = "<html><body>Test</body></html>";
            message.BodyEncoding = Encoding.Unicode;

            message.ReplyToList.Add(new MailAddress("replyto@lilltek.com"));

            message.To.Add(new MailAddress("jane@doe.com"));

            message.CC.Add(new MailAddress("cc1@lilltek.com"));
            message.CC.Add(new MailAddress("cc2@lilltek.com"));

            message.Bcc.Add(new MailAddress("bcc1@lilltek.com"));
            message.Bcc.Add(new MailAddress("bcc2@lilltek.com"));

            dataStream = new EnhancedMemoryStream();
            dataStream.WriteBytesNoLen(new byte[] { 0, 1, 2, 3, 4, 5, 6, 7, 8, 9 });
            dataStream.Position = 0;

            attachment = new Attachment(dataStream, "application/octet-stream");
            attachment.ContentDisposition.FileName = "test.dat";
            message.Attachments.Add(attachment);

            stream = new EnhancedMemoryStream();
            MailHelper.WriteMessage(stream, message);

            // Read

            stream.Position = 0;
            message = MailHelper.ReadMessage(stream);

            Assert.AreEqual(MailPriority.High, message.Priority);
            Assert.AreEqual("jeff@lilltek.com", message.From.ToString());
            Assert.AreEqual("sender@lilltek.com", message.Sender.ToString());

            Assert.AreEqual(1, message.ReplyToList.Count);
            Assert.AreEqual("replyto@lilltek.com", message.ReplyToList[0].ToString());

            Assert.AreEqual(2, message.To.Count);
            Assert.AreEqual("joe@bloe.com", message.To[0].ToString());
            Assert.AreEqual("jane@doe.com", message.To[1].ToString());

            Assert.AreEqual(2, message.CC.Count);
            Assert.AreEqual("cc1@lilltek.com", message.CC[0].ToString());
            Assert.AreEqual("cc2@lilltek.com", message.CC[1].ToString());

            Assert.AreEqual(2, message.Bcc.Count);
            Assert.AreEqual("bcc1@lilltek.com", message.Bcc[0].ToString());
            Assert.AreEqual("bcc2@lilltek.com", message.Bcc[1].ToString());

            Assert.AreEqual("Test Message", message.Subject);
            Assert.AreEqual(Encoding.UTF8.WebName, message.SubjectEncoding.WebName);

            Assert.IsTrue(message.IsBodyHtml);
            Assert.AreEqual("<html><body>Test</body></html>", message.Body);
            Assert.AreEqual(Encoding.Unicode.WebName, message.BodyEncoding.WebName);

            Assert.AreEqual(1, message.Attachments.Count);

            attachment = message.Attachments[0];
            Assert.AreEqual("application/octet-stream", attachment.ContentType.MediaType);
            Assert.AreEqual("test.dat", attachment.ContentDisposition.FileName);
            CollectionAssert.AreEqual(new byte[] { 0, 1, 2, 3, 4, 5, 6, 7, 8, 9 }, new EnhancedStream(attachment.ContentStream).ReadBytes(10));
        }
    }
}

