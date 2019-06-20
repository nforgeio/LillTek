//-----------------------------------------------------------------------------
// FILE:        MailHelper.cs
// CONTRIBUTOR: Jeff Lill
// COPYRIGHT:   Copyright (c) 2005-2015 by Jeffrey Lill.  All rights reserved.
// DESCRIPTION: Implements email related utility methods.

using System;
using System.Collections;
using System.IO;
using System.Net;
using System.Net.Mail;
using System.Net.Mime;
using System.Text;
using System.Threading;

using LillTek.Common;

namespace LillTek.Net.Mail
{
    /// <summary>
    /// Implements email related utility methods.
    /// </summary>
    /// <remarks>
    /// <para>
    /// The current serialized email message format version number is <b>2</b>.  The
    /// format change was made to support a change to the <see cref="MailMessage" />
    /// class in .NET 4.0.  Mail messages now support the <see cref="MailMessage.ReplyToList" />
    /// which can accept multiple email addresses.  Format version 2 of the serialized email
    /// messages now serialize multiple reply to addresses.
    /// </para>
    /// </remarks>
    public static class MailHelper
    {
        private const int MessageMagic = 0x7ACC1F13;    // Magic number for an email message
        private const int FormatVer    = 2;             // Format version number

        /// <summary>
        /// Converts an email address into a string.
        /// </summary>
        /// <param name="address">The address (or <c>null</c>).</param>
        private static string GetAddressString(MailAddress address)
        {
            if (address == null)
                return null;
            else
                return address.ToString();
        }

        /// <summary>
        /// Reads an email address from the stream.
        /// </summary>
        /// <param name="input">The input stream.</param>
        /// <returns>The email address or <c>null</c>.</returns>
        private static MailAddress ReadMailAddress(EnhancedStream input)
        {
            string value;

            value = input.ReadString32();
            if (value == null)
                return null;

            return new MailAddress(value);
        }

        /// <summary>
        /// Writes an email message to a stream.
        /// </summary>
        /// <param name="output">The output stream.</param>
        /// <param name="message">The message to be written.</param>
        public static void WriteMessage(EnhancedStream output, MailMessage message)
        {
            output.WriteInt32(MessageMagic);
            output.WriteInt32(FormatVer);

            output.WriteInt32((int)message.Priority);
            output.WriteString32(GetAddressString(message.From));
            output.WriteString32(GetAddressString(message.Sender));

            output.WriteInt32(message.ReplyToList.Count);
            foreach (var address in message.ReplyToList)
                output.WriteString32(GetAddressString(address));

            output.WriteInt32(message.To.Count);
            foreach (var address in message.To)
                output.WriteString32(GetAddressString(address));

            output.WriteInt32(message.CC.Count);
            foreach (var address in message.CC)
                output.WriteString32(GetAddressString(address));

            output.WriteInt32(message.Bcc.Count);
            foreach (var address in message.Bcc)
                output.WriteString32(GetAddressString(address));

            output.WriteString32(message.Subject);
            output.WriteString32(message.SubjectEncoding != null ? message.SubjectEncoding.WebName : null);

            output.WriteString32(message.Body);
            output.WriteString32(message.BodyEncoding.WebName);
            output.WriteBool(message.IsBodyHtml);

            output.WriteInt32(message.AlternateViews.Count);
            foreach (var view in message.AlternateViews)
                output.WriteString32(view.ToString());

            output.WriteInt32(message.Attachments.Count);
            foreach (var attachment in message.Attachments)
            {
                output.WriteString32(attachment.ContentType != null ? attachment.ContentType.ToString() : null);

                // Write the attachment data

                output.WriteInt32((int)attachment.ContentStream.Length);

                attachment.ContentStream.Position = 0;
                output.CopyFrom(attachment.ContentStream, -1);

                // Write the ContentDisposition

                output.WriteInt32(attachment.ContentDisposition.Parameters.Count);
                foreach (string key in attachment.ContentDisposition.Parameters.Keys)
                {
                    output.WriteString32(key);
                    output.WriteString32(attachment.ContentDisposition.Parameters[key]);
                }
            }

            // $todo(jeff.lill): 
            //
            // I'm not serializing the message headers at this point.
        }

        /// <summary>
        /// Reads an email message from a stream.
        /// </summary>
        /// <param name="input">The input stream.</param>
        /// <returns>The <see cref="MailMessage" /> read.</returns>
        /// <exception cref="FormatException">Thrown if the input stream does not contain a valid message.</exception>
        public static MailMessage ReadMessage(EnhancedStream input)
        {
            MailMessage     message = new MailMessage();
            string          value;
            int             count;
            int             formatVersion;

            if (input.ReadInt32() != MessageMagic)
                throw new FormatException("Invalid mail message.");

            formatVersion = input.ReadInt32();
            switch (formatVersion)
            {
                case 1:
                case 2:

                    break;

                default:

                    throw new FormatException(string.Format("Unsupported mail format version [{0}].", formatVersion));
            }

            message.Priority = (MailPriority)input.ReadInt32();

            message.From = ReadMailAddress(input);
            message.Sender = ReadMailAddress(input);

            if (formatVersion == 1)
                message.ReplyToList.Add(ReadMailAddress(input));
            else
            {
                count = input.ReadInt32();
                for (int i = 0; i < count; i++)
                    message.ReplyToList.Add(ReadMailAddress(input));
            }

            count = input.ReadInt32();
            for (int i = 0; i < count; i++)
                message.To.Add(ReadMailAddress(input));

            count = input.ReadInt32();
            for (int i = 0; i < count; i++)
                message.CC.Add(ReadMailAddress(input));

            count = input.ReadInt32();
            for (int i = 0; i < count; i++)
                message.Bcc.Add(ReadMailAddress(input));

            value = input.ReadString32();
            if (value != null)
                message.Subject = value;

            value = input.ReadString32();
            if (value != null)
                message.SubjectEncoding = Encoding.GetEncoding(value);

            value = input.ReadString32();
            if (value != null)
                message.Body = value;

            value = input.ReadString32();
            if (value != null)
                message.BodyEncoding = Encoding.GetEncoding(value);

            message.IsBodyHtml = input.ReadBool();

            count = input.ReadInt32();
            for (int i = 0; i < count; i++)
                message.AlternateViews.Add(AlternateView.CreateAlternateViewFromString(input.ReadString32()));

            count = input.ReadInt32();
            for (int i = 0; i < count; i++)
            {
                Attachment      attachment;
                MemoryStream    contentStream;
                string          contentType;
                int             cParameters;
                int             cb;

                contentType = input.ReadString32();

                // Read the attachment data

                cb = input.ReadInt32();
                if (input.Position + cb > input.Length)
                    throw new FormatException("Corrupt email message.");

                contentStream = new MemoryStream(cb);
                input.CopyTo(contentStream, cb);
                contentStream.Position = 0;

                attachment = new Attachment(contentStream, contentType);

                // Read the ContentDisposition

                cParameters = input.ReadInt32();
                for (int j = 0; j < cParameters; j++)
                {
                    string paramName = input.ReadString32();
                    string paramValue = input.ReadString32();

                    attachment.ContentDisposition.Parameters[paramName] = paramValue;
                }

                // Add the attachment to the message.

                message.Attachments.Add(attachment);
            }

            // $todo(jeff.lill): 
            //
            // I'm not serializing the message headers.

            return message;
        }
    }
}
