//-----------------------------------------------------------------------------
// FILE:        MailAgent.cs
// CONTRIBUTOR: Jeff Lill
// COPYRIGHT:   Copyright (c) 2005-2015 by Jeffrey Lill.  All rights reserved.
// DESCRIPTION: Implements an agent that queues email messages for delivery
//              via a SMTP relay server.

using System;
using System.Collections;
using System.IO;
using System.Net;
using System.Net.Mail;
using System.Net.Mime;
using System.Threading;

using LillTek.Common;

// $todo(jeff.lill):
//
// Do I need to worry about poison messages or messages that cannot be delivered for
// a long period of time?

namespace LillTek.Net.Mail
{
    /// <summary>
    /// Implements an agent that queues email messages for delivery
    /// via a SMTP relay server.
    /// </summary>
    /// <remarks>
    /// <para>
    /// This class implements functionality equivalent to the IIS and SQL/Server Database Mail services.
    /// The rational for this custom implementation is to provide an easy-to-configure way to deliver
    /// email for pure service applications.
    /// </para>
    /// <para>
    /// This class is designed to deliver email to an SMTP relay server which will be responsible for
    /// routing messages to their ultimate destination.  Messages are persisted to a file system folder
    /// and delivery to the relay server is perfomed on a background thread.
    /// </para>
    /// <para>
    /// The class is easy to use.  Simply instantiate an instance, passing the SMTP relay server's 
    /// network binding and credentials as well as the path to the file system folder where messages
    /// are to be queued.  Then call <see cref="Enqueue" /> to queue email messages for delivery.
    /// Call <see cref="Stop" /> or <see cref="Dispose" /> to stop the agent.
    /// </para>
    /// </remarks>
    public class MailAgent : IDisposable
    {
        private NetworkBinding  smtpServer;
        private string          account;
        private string          password;
        private string          queueFolder;
        private TimeSpan        pollInterval;
        private GatedTimer       queueTimer;
        private SmtpClient      smtp;

        /// <summary>
        /// Constructs and starts the agent.
        /// </summary>
        /// <param name="smtpServer">The <see cref="NetworkBinding" /> for the relay SMTP server.</param>
        /// <param name="account">The relay server account.</param>
        /// <param name="password">The relay server password.</param>
        /// <param name="queueFolder">Path to the folder where email messages should be queued.</param>
        /// <param name="pollInterval">Interval at which the agent will poll the queue folder for messages waiting to be delivered.</param>
        /// <remarks>
        /// <para>
        /// This method initializes and starts the mail agent, creating the mail queue folder if it doesn't already exist.
        /// </para>
        /// <note>
        /// Pass a network binding with the host or port set to <b>Any</b> to disable email transmission.
        /// </note>
        /// </remarks>
        public MailAgent(NetworkBinding smtpServer, string account, string password, string queueFolder, TimeSpan pollInterval)
        {
            queueFolder = Path.GetFullPath(queueFolder);
            Helper.CreateFolderTree(queueFolder);

            this.smtpServer   = smtpServer;
            this.account      = account;
            this.password     = password;
            this.queueFolder  = queueFolder;
            this.pollInterval = pollInterval;

            smtp = new SmtpClient(smtpServer.HostOrAddress, smtpServer.Port);
            smtp.Credentials = new SmtpCredentials(account, password);
            smtp.DeliveryMethod = SmtpDeliveryMethod.Network;

            this.queueTimer = new GatedTimer(new TimerCallback(OnQueueTimer), null, pollInterval);
        }

        /// <summary>
        /// Destructor.
        /// </summary>
        ~MailAgent()
        {
            Stop();
        }

        /// <summary>
        /// Stops the agent if it is running.
        /// </summary>
        public void Stop()
        {
            if (queueTimer != null)
            {
                queueTimer.Dispose();
                queueTimer = null;
            }
        }

        /// <summary>
        /// Stops the agent.
        /// </summary>
        public void Dispose()
        {
            Stop();
        }

        /// <summary>
        /// Queues an email message for deliver,
        /// </summary>
        /// <param name="message">The outbound message.</param>
        /// <remarks>
        /// <note>
        /// This message will do nothing but log a warning if the agent was started
        /// with a <b>Any</b> network binding.
        /// </note>
        /// </remarks>
        public void Enqueue(MailMessage message)
        {
            // Generate a unique file name that will sort roughly in the order
            // that the messages were added to the queue so that messages were
            // are delivered to the relay server in rougly the order that the
            // were queued.

            string fileName = string.Format("{0}-{1}.msg", Helper.ToIsoDate(DateTime.UtcNow).Replace(':', '-'), Guid.NewGuid());

            if (smtpServer.IsAny)
            {
                // Email transmission is disabled

                SysLog.LogWarning("Email transmission is disabled.");
                return;
            }

            using (var output = new EnhancedFileStream(Path.Combine(queueFolder, fileName), FileMode.Create, FileAccess.ReadWrite))
                MailHelper.WriteMessage(output, message);
        }

        /// <summary>
        /// Handles the actual email transmission from the queue.
        /// </summary>
        /// <param name="state">Not used.</param>
        private void OnQueueTimer(object state)
        {
            try
            {
                string[] messageFiles = Helper.GetFilesByPattern(Path.Combine(queueFolder, "*.*"), SearchOption.TopDirectoryOnly);

                if (messageFiles.Length == 0)
                    return;

                foreach (string messagePath in messageFiles)
                {
                    MailMessage message;

                    if (queueTimer == null)
                        break;  // The agent is no longer running.

                    try
                    {
                        using (var input = new EnhancedFileStream(messagePath, FileMode.Open))
                        {
                            try
                            {
                                message = MailHelper.ReadMessage(input);
                            }
                            catch (Exception e)
                            {
                                // Delete messages that could not be deserialized.

                                SysLog.LogException(e);
                                Helper.DeleteFile(messagePath);
                                continue;
                            }
                        }
                    }
                    catch (IOException)
                    {
                        continue;   // Ignore files that may be locked for writing
                    }

                    try
                    {
                        smtp.Send(message);
                        Helper.DeleteFile(messagePath);
                    }
                    catch (SmtpException e)
                    {
                        // Delete messages that couldn't be delivered due to non-transient
                        // problems with the destination mailbox.  These errors won't typically
                        // occur when we're fowarding mail to a pure relay server but may 
                        // occur if the relay server may also host actual mailboxes.

                        switch (e.StatusCode)
                        {
                            case SmtpStatusCode.MailboxUnavailable:

                                SysLog.LogWarning("MailAgent: {0}", e.Message);
                                Helper.DeleteFile(messagePath);
                                break;
                        }
                    }
                    catch (Exception e)
                    {
                        // Abort all transmissions for the remaining exceptions types.

                        SysLog.LogException(e);
                        break;
                    }
                }
            }
            catch (Exception e)
            {
                SysLog.LogException(e);
            }
        }
    }
}
