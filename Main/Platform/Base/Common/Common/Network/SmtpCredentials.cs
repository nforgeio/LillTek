//-----------------------------------------------------------------------------
// FILE:        SmtpCredentials.cs
// CONTRIBUTOR: Jeff Lill
// COPYRIGHT:   Copyright (c) 2005-2015 by Jeffrey Lill.  All rights reserved.
// DESCRIPTION: Holds the credentials for a SmtpClient instance to use for
//              authenticating to a SMTP server.

using System;
using System.Net;
using System.Net.Mail;

namespace LillTek.Common
{
    /// <summary>
    /// Holds the credentials for a <see cref="SmtpClient" /> instance to 
    /// use for authenticating to a SMTP server. 
    /// </summary>
    public class SmtpCredentials : ICredentialsByHost
    {
        private NetworkCredential credentials;

        /// <summary>
        /// Constructs an instance from a <see cref="NetworkCredential" />.
        /// </summary>
        /// <param name="credentials">The credentials.</param>
        public SmtpCredentials(NetworkCredential credentials)
        {
            this.credentials = credentials;
        }

        /// <summary>
        /// Constructs an instance from and account ID and password.
        /// </summary>
        /// <param name="account">The account ID.</param>
        /// <param name="password">The password.</param>
        public SmtpCredentials(string account, string password)
        {
            this.credentials = new NetworkCredential(account, password);
        }

        /// <summary>
        /// Constructs an instance from and account ID, password, and domain.
        /// </summary>
        /// <param name="account">The account ID.</param>
        /// <param name="password">The password.</param>
        /// <param name="domain">The domain.</param>
        public SmtpCredentials(string account, string password, string domain)
        {
            this.credentials = new NetworkCredential(account, password, domain);
        }

        /// <summary>
        /// Used by <see cref="SmtpClient" /> to obtain the network credentials.
        /// </summary>
        /// <param name="host">The SMTP server host name.</param>
        /// <param name="port">The SMTP server port number.</param>
        /// <param name="authenticationType">The required autnetication type.</param>
        /// <returns>The credentials.</returns>
        /// <remarks>
        /// This method simply returns the credentials held by the instance.  The
        /// method parameters are ignored.
        /// </remarks>
        public NetworkCredential GetCredential(string host, int port, string authenticationType)
        {
            return credentials;
        }
    }
}
