//-----------------------------------------------------------------------------
// FILE:        ADTestSettings.cs
// CONTRIBUTOR: Jeff Lill
// COPYRIGHT:   Copyright (c) 2005-2015 by Jeffrey Lill.  All rights reserved.
// DESCRIPTION: Unit test Active Directory domain settings.
using System;
using System.Collections.Generic;
using System.Text;

using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace LillTek.Common
{
    /// <summary>
    /// Unit test Active Directory domain settings.
    /// </summary>
    /// <remarks>
    /// <para>
    /// This class returns information about the current Active Directory
    /// domain along with the account credentials to be used for unit 
    /// tests that require them.
    /// </para> 
    /// <para>
    /// The constructor initializes the instance fields by parsing
    /// at the <b>LT_TEST_AD</b> environment variable.  This variable
    /// must be present and be formatted as:
    /// </para>
    /// <code language="none">
    ///     servers=adserver1,adserver2;nasSecret=xxxx;domain=mydomain;account=myaccount;password=mypassword
    /// </code>
    /// <para>
    /// where <b>servers</b> specifies the IP addresses or host names of the Active Directory
    /// servers (separated by commas), <b>nasSecret</b> is the shared RADIUS NAS secret (omit this
    /// or set this to blank to disable IAS testing), <b>domain</b> is the name of the domain, <b>account</b>  
    /// is the test account name, and <b>password</b> is the test account password.
    /// </para>
    /// </remarks>
    public sealed class ADTestSettings
    {
        /// <summary>
        /// The list Active Directory servers or IP addresses.
        /// </summary>
        public readonly string[] Servers;

        /// <summary>
        /// The shared secret to be used when using RADIUS authentication against
        /// the Active Directory via IAS (note that IAS must also be configured 
        /// with for RADIUS clients using this secret).  This is set to the
        /// empty string to disable IAS tests.
        /// </summary>
        public readonly string NasSecret;

        /// <summary>
        /// The Active Directory domain to be used for unit testing.
        /// </summary>
        public readonly string Domain;

        /// <summary>
        /// The Active Directory account to be used for unit testing.
        /// </summary>
        public readonly string Account;

        /// <summary>
        /// The account password.
        /// </summary>
        public readonly string Password;

        /// <summary>
        /// Constructor.
        /// </summary>
        /// <remarks>
        /// <para>
        /// The constructor initializes the instance fields by parsing
        /// at the <b>LT_TEST_AD</b> environment variable.  This variable
        /// must be present and be formatted as:
        /// </para>
        /// <code language="none">
        ///     servers=adserver1,adserver2;nasSecret=my-secret;domain=mydomain;account=myaccount;password=mypassword
        /// </code>
        /// <para>
        /// where <b>servers</b> specifies the IP addresses or host names of the Active Directory
        /// servers, <b>domain</b> is the name of the domain, <b>account</b> is the test account
        /// name, and <b>password</b> is the test account password.
        /// </para>
        /// </remarks>
        /// <exception cref="ArgumentException">Thrown if the <b>LT_TEST_AD</b> environment variable was not found.</exception>
        public ADTestSettings()
        {
            var         args = new Dictionary<string, string>(StringComparer.InvariantCultureIgnoreCase);
            string      envVar;

            envVar = Environment.GetEnvironmentVariable("LT_TEST_AD");
            if (envVar == null)
                Assert.Inconclusive("[LT_TEST_AD] environment variable was not found.");

            var equalSplit = new char[] { '=' };

            foreach (var arg in envVar.Split(';'))
            {
                var fields = envVar.Split(equalSplit, 2);

                args[fields[0]] = fields[1];
            }

            string value;

            this.Servers   = new string[0];
            this.NasSecret = string.Empty;
            this.Domain    = null;
            this.Account   = null;
            this.Password  = null;

            if (args.TryGetValue("servers", out value))
                this.Servers = value.Split(new char[] { ',' }, StringSplitOptions.RemoveEmptyEntries);

            if (args.TryGetValue("nasSecret", out value))
                this.NasSecret = value;

            if (args.TryGetValue("domain", out value))
                this.Domain= value;

            if (args.TryGetValue("account", out value))
                this.Account = value;

            if (args.TryGetValue("password", out value))
                this.Password = value;
        }

        /// <summary>
        /// Returns the active directory servers as a comma separated list of 
        /// host names or IP addresses.
        /// </summary>
        /// <returns>The list of active directory servers.</returns>
        public string GetServerList()
        {
            var sb = new StringBuilder();

            for (int i = 0; i < Servers.Length; i++)
            {
                if (i > 0)
                    sb.Append(',');

                sb.Append(Servers[i]);
            }

            return sb.ToString();
        }
    }
}

