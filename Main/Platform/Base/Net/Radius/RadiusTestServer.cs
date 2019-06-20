//-----------------------------------------------------------------------------
// FILE:        RadiusTestServer.cs
// CONTRIBUTOR: Jeff Lill
// COPYRIGHT:   Copyright (c) 2005-2015 by Jeffrey Lill.  All rights reserved.
// DESCRIPTION: Wraps the RADL Radius Server in an easy to use form for unit testing.

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Net;
using System.Text;
using System.Threading;

using LillTek.Common;
using LillTek.Net.Sockets;

namespace LillTek.Net.Radius
{
    /// <summary>
    /// Wraps the RADL Radius Server in an easy to use form for unit testing.
    /// </summary>
    /// <remarks>
    /// <para>
    /// This class requires that the RADL Radius server files be located
    /// at:
    /// </para>
    /// <code language="none">
    ///     $(LT_TESTBIN)\RadiusServer
    /// </code>
    /// <para>
    /// Use the <see cref="Start(string,string)" /> method to start the server, passing the
    /// user account names and passwords as well as the RADIUS client names and
    /// passwords.  Note that the free version of RADL limits user account
    /// names to 8 characters.
    /// </para>
    /// <para>
    /// The server will be started on the local machine and can be reached 
    /// via the Ethernet loopback driver on UDP port 1812.
    /// </para>
    /// </remarks>
    internal sealed class RadiusTestServer
    {
        private Process     serverProcess = null;
        private string      usersPath     = null;
        private string      clientsPath   = null;
        private string      orgDir        = null;

        /// <summary>
        /// Constructor.
        /// </summary>
        public RadiusTestServer()
        {
        }

        /// <summary>
        /// The RADIUS server utility is essentially hardcoded to use the AAA (1645) port
        /// rather than port 1812 as specified in the RFC.  This property returns the
        /// proper IP endpoint to use when addressing this server.
        /// </summary>
        public IPEndPoint EndPoint
        {
            get { return new IPEndPoint(NetHelper.GetActiveAdapter(), 1645); }
        }

        /// <summary>
        /// Starts the RADIUS server, intializing the user and client
        /// databases.
        /// </summary>
        /// <param name="users">The user database file contents.</param>
        /// <param name="clients">The client database file contents.</param>
        /// <remarks>
        /// See the <b>users.example</b> and <b>clients.example</b> files
        /// in the <b>raddb</b> folder for a description of how these
        /// files must be formatted.
        /// </remarks>
        /// <exception cref="NotAvailableException">Thrown if the RADIUS server application is not installed.</exception>
        public void Start(string users, string clients)
        {
            StreamWriter    writer = null;
            string          testBinPath;
            string          exePath;

            testBinPath = EnvironmentVars.Get("LT_TESTBIN");
            if (testBinPath == null)
                throw new ArgumentException("[LT_TESTBIN] environment variable does not exist.");

            testBinPath = Helper.AddTrailingSlash(testBinPath) + @"RadiusServer\";
            exePath     = testBinPath + "Radl.exe";
            usersPath   = testBinPath + @"raddb\users";
            clientsPath = testBinPath + @"raddb\clients";

            if (!File.Exists(exePath))
                throw new NotAvailableException("RADIUS Server not found at: " + exePath);

            orgDir                       = Environment.CurrentDirectory;
            Environment.CurrentDirectory = testBinPath.Substring(0, testBinPath.Length - 1);

            try
            {
                writer = new StreamWriter(usersPath, false, Helper.AnsiEncoding);
                writer.Write(users);
                writer.Close();
                writer = null;

                writer = new StreamWriter(clientsPath, false, Helper.AnsiEncoding);
                writer.Write(clients);
                writer.Close();
                writer = null;

                serverProcess                     = Process.Start(exePath, string.Empty);
                serverProcess.EnableRaisingEvents = true;

                Thread.Sleep(5000);    // Give the server a chance to initialize
            }
            finally
            {
                if (writer != null)
                    writer.Close();
            }
        }

        /// <summary>
        /// Starts the server intializing it with the specified set of users and devices.
        /// </summary>
        /// <param name="users">The set of user passwords keyed by user account.</param>
        /// <param name="devices">The set of device shared secrets keyed by device IP address.</param>
        public void Start(Dictionary<string, string> users, Dictionary<IPAddress, string> devices)
        {
            StringBuilder   sb;
            string          usersDB;
            string          devicesDB;

            sb = new StringBuilder();
            foreach (string user in users.Keys)
            {
                sb.AppendFormat("{0}\tPassword = {1}\r\n", user, users[user]);
                sb.AppendLine("\tUser-Service-Type = Login-User");
                sb.AppendLine();
            }

            usersDB = sb.ToString();

            sb = new StringBuilder();
            foreach (IPAddress ip in devices.Keys)
                sb.AppendFormat("{0}\t{1}\r\n", ip, devices[ip]);

            devicesDB = sb.ToString();

            Start(usersDB, devicesDB);
        }

        /// <summary>
        /// Stops the RADIUS server.
        /// </summary>
        public void Stop()
        {
            if (serverProcess != null)
            {
                serverProcess.Kill();
                serverProcess.WaitForExit();
                serverProcess.Close();
                serverProcess = null;

                Thread.Sleep(1000);
            }

            if (usersPath != null)
            {
                Helper.DeleteFile(usersPath);
                usersPath = null;
            }

            if (clientsPath != null)
            {
                Helper.DeleteFile(clientsPath);
                clientsPath = null;
            }

            if (orgDir != null)
                Environment.CurrentDirectory = orgDir;
        }
    }
}

