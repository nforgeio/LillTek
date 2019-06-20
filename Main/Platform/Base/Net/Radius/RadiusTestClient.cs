//-----------------------------------------------------------------------------
// FILE:        RadiusTestClient.cs
// CONTRIBUTOR: Jeff Lill
// COPYRIGHT:   Copyright (c) 2005-2015 by Jeffrey Lill.  All rights reserved.
// DESCRIPTION: Wraps the RadUtils Radius command line client in a form easy to use for
//              unit tests.

using System;
using System.Diagnostics;
using System.IO;
using System.Net;
using System.Threading;

using LillTek.Common;
using LillTek.Net.Sockets;

namespace LillTek.Net.Radius
{
    /// <summary>
    /// Wraps the RadUtils Radius command line client in a form easy to use for
    /// unit tests.
    /// </summary>
    /// <remarks>
    /// <para>
    /// This class requires that the RadUtils client files be located
    /// at:
    /// </para>
    /// <code language="none">
    ///     $(LT_TESTBIN)\RadiusClient
    /// </code>
    /// <para>
    /// The purpose of this class is to implement client side interop testing
    /// to verify that the LillTek RADIUS code can interoperate properly with
    /// other RADIUS code bases.
    /// </para>
    /// </remarks>
    /// <threadsafety instance="true" />
    internal static class RadiusTestClient
    {
        private static int nextID = 0;

        /// <summary>
        /// Sends an access request packet to the specified IP endpoint with the 
        /// </summary>
        /// <param name="targetEP">The RADIUS server endpoint.</param>
        /// <param name="secret">The NAS shared secret.</param>
        /// <param name="userName">The user account.</param>
        /// <param name="password">The password.</param>
        /// <returns><c>true</c> if authentition succeeded, <c>false</c> if it was explicitly rejected.</returns>
        /// <exception cref="TimeoutException">Thrown if the transaction timed out.</exception>
        /// <exception cref="NotAvailableException">Thrown if RADIUS command line client is not installed.</exception>
        /// <remarks>
        /// <note>
        /// The command line client tool is primitive and supports a maximum of 8 character
        /// user names.
        /// </note>
        /// </remarks>
        public static bool Authenticate(IPEndPoint targetEP, string secret, string userName, string password)
        {
            StreamWriter    writer     = null;
            StringReader    reader     = null;
            string          scriptPath = null;
            string          testBinPath;
            string          exePath;
            string          args;
            int             cAuths;
            int             cDenied;

            if (userName.Length > 8)
                throw new ArgumentException("[userName] exceeds 8 characters.", "userName");

            testBinPath = EnvironmentVars.Get("LT_TESTBIN");
            if (testBinPath == null)
                throw new ArgumentException("[LT_TESTBIN] environment variable does not exist.");

            if (secret.IndexOfAny(new char[] { ' ', '\t', '\r', '\n' }) != -1)
                throw new NotImplementedException("NAS secret cannot have whitespace.");

            testBinPath = Helper.AddTrailingSlash(testBinPath) + @"RadiusClient\";
            exePath     = testBinPath + "Radclient.exe";
            scriptPath  = Path.GetTempFileName();

            if (!File.Exists(exePath))
                throw new NotAvailableException("RADIUS Client not found at: " + exePath);

            try
            {
                // Write the script file specifying the packet attributes

                writer = new StreamWriter(scriptPath, false, Helper.AnsiEncoding);
                writer.WriteLine("User-Name={0}", userName);
                writer.WriteLine("Password={0}", password);
                writer.WriteLine("NAS-IP-Address={0}", NetHelper.GetActiveAdapter());
                writer.Close();
                writer = null;

                // Invoke the command line client

                args = string.Format("-c 1 -i {0} -n 0 -p 1 -r 2 -t 7 -f\"{1}\" -sx {2} auth {3}",
                                     nextID++, scriptPath, targetEP, secret);

                Environment.CurrentDirectory = testBinPath.Substring(0, testBinPath.Length - 1);
                var result = Helper.ExecuteCaptureStreams(exePath, args);

                if (result.ExitCode != 0 || result.StandardOutput.Length == 0)
                    throw new TimeoutException("RADIUS client timeout or other error.");

                // Parse the output.

                reader = new StringReader(result.StandardOutput);

                cAuths  = -1;
                cDenied = -1;
                for (string line = reader.ReadLine(); line != null; line = reader.ReadLine())
                {
                    line = line.Trim();
                    if (line.Length == 0)
                        continue;

                    if (line.StartsWith("Total approved auths:"))
                        cAuths = int.Parse(line.Substring(23));

                    else if (line.StartsWith("Total denied auths:"))
                        cDenied = int.Parse(line.Substring(21));
                }

                reader.Close();
                reader = null;

                if (cAuths == -1 || cDenied == -1)
                    return false;
                else if (cAuths > 0)
                    return true;
                else if (cDenied > 0)
                    return false;
                else
                    throw new TimeoutException("RADIUS client timeout.");
            }
            finally
            {
                if (writer != null)
                    writer.Close();

                if (reader != null)
                    reader.Close();

                Helper.DeleteFile(scriptPath);
            }
        }
    }
}

