//-----------------------------------------------------------------------------
// FILE:        AwsInstanceInfo.cs
// CONTRIBUTOR: Jeff Lill
// COPYRIGHT:   Copyright (c) 2005-2015 by Jeffrey Lill.  All rights reserved.
// DESCRIPTION: Describes an AWS instance's metadata.

using System;
using System.IO;
using System.Net;
using System.Text;
using System.Threading;

namespace LillTek.Common
{
    /// <summary>
    /// Describes an AWS instance's metadata.
    /// </summary>
    public class AwsInstanceInfo
    {
        /// <summary>
        /// Constructs an instance by querying the AWS infrastructure.
        /// </summary>
        /// <exception cref="InvalidOperationException">Thrown if the application is not hosted on the AWS cloud.</exception>
        /// <exception cref="Exception">Thrown if the infrastructure query failed.</exception>
        internal AwsInstanceInfo()
        {
            if (!Helper.IsAWS)
                throw new InvalidOperationException("Cannot load AWS metadata when the application is not hosted by the AWS cloud.");

            // Load the server role from the C:\Aws-Boot\Server-Role.txt file if present.

            try
            {
                const string path = @"C:\Aws-Boot\Server-Role.txt";

                if (File.Exists(path))
                    this.ServerRole = File.ReadAllText(path, Encoding.ASCII).Trim();
            }
            catch
            {
                this.ServerRole = "UNKNOWN";
            }

            // Perform nine parallel HTTP queries to the AWS infrastructure to load
            // the desired metadata.  If any of the operations fail, wait 5 seconds and
            // try one more time.

            const string uriPrefix = "http://169.254.169.254/2009-04-04/meta-data/";
            const string errFormat = "Error [{0}] getting AWS instance metadata [{1}].";

            Exception error = null;
            bool retry = false;

        retry:

            int cPending = 9;

            // #1: placement/availability-zone

            var request1 = HttpWebRequest.Create(uriPrefix + "placement/availability-zone");

            request1.BeginGetResponse(
                (ar) =>
                {
                    try
                    {
                        using (var stream = request1.EndGetResponse(ar).GetResponseStream())
                        {

                            this.Location = ReadText(stream);
                        }
                    }
                    catch (Exception e)
                    {
                        error = new Exception(string.Format(errFormat, e.Message, "placement/availability-zone"), e);
                    }
                    finally
                    {
                        Interlocked.Decrement(ref cPending);
                    }
                },
                null);

            // #2: instance-id

            var request2 = HttpWebRequest.Create(uriPrefix + "instance-id");

            request2.BeginGetResponse(
                (ar) =>
                {
                    try
                    {
                        using (var stream = request2.EndGetResponse(ar).GetResponseStream())
                        {
                            this.InstanceID = ReadText(stream);
                        }
                    }
                    catch (Exception e)
                    {
                        error = new Exception(string.Format(errFormat, e.Message, "instance-id"), e);
                    }
                    finally
                    {
                        Interlocked.Decrement(ref cPending);
                    }
                },
                null);

            // #3: ami-id

            var request3 = HttpWebRequest.Create(uriPrefix + "ami-id");

            request3.BeginGetResponse(
                (ar) =>
                {
                    try
                    {
                        using (var stream = request3.EndGetResponse(ar).GetResponseStream())
                        {
                            this.AmiID = ReadText(stream);
                        }
                    }
                    catch (Exception e)
                    {
                        error = new Exception(string.Format(errFormat, e.Message, "ami-id"), e);
                    }
                    finally
                    {
                        Interlocked.Decrement(ref cPending);
                    }
                },
                null);

            // #4: instance-type

            var request4 = HttpWebRequest.Create(uriPrefix + "instance-type");

            request4.BeginGetResponse(
                (ar) =>
                {
                    try
                    {
                        using (var stream = request4.EndGetResponse(ar).GetResponseStream())
                        {
                            this.InstanceType = ReadText(stream);
                        }
                    }
                    catch (Exception e)
                    {
                        error = new Exception(string.Format(errFormat, e.Message, "instance-type"), e);
                    }
                    finally
                    {
                        Interlocked.Decrement(ref cPending);
                    }
                },
                null);

            // #5: local-hostname

            var request5 = HttpWebRequest.Create(uriPrefix + "local-hostname");

            request5.BeginGetResponse(
                (ar) =>
                {
                    try
                    {
                        using (var stream = request5.EndGetResponse(ar).GetResponseStream())
                        {
                            this.LocalHostName = ReadText(stream);
                        }
                    }
                    catch (Exception e)
                    {
                        error = new Exception(string.Format(errFormat, e.Message, "local-hostname"), e);
                    }
                    finally
                    {
                        Interlocked.Decrement(ref cPending);
                    }
                },
                null);

            // #6: local-ipv4

            var request6 = HttpWebRequest.Create(uriPrefix + "local-ipv4");

            request6.BeginGetResponse(
                (ar) =>
                {
                    try
                    {
                        using (var stream = request6.EndGetResponse(ar).GetResponseStream())
                        {
                            this.LocalAddress = Helper.ParseIPAddress(ReadText(stream));
                        }
                    }
                    catch (Exception e)
                    {
                        error = new Exception(string.Format(errFormat, e.Message, "local-ipv4"), e);
                    }
                    finally
                    {
                        Interlocked.Decrement(ref cPending);
                    }
                },
                null);

            // #7: public-hostname

            var request7 = HttpWebRequest.Create(uriPrefix + "public-hostname");

            request7.BeginGetResponse(
                (ar) =>
                {
                    try
                    {
                        using (var stream = request7.EndGetResponse(ar).GetResponseStream())
                        {
                            this.PublicHostName = ReadText(stream);
                        }
                    }
                    catch (Exception e)
                    {
                        error = new Exception(string.Format(errFormat, e.Message, "public-hostname"), e);
                    }
                    finally
                    {
                        Interlocked.Decrement(ref cPending);
                    }
                },
                null);

            // #8: public-ipv4

            var request8 = HttpWebRequest.Create(uriPrefix + "public-ipv4");

            request8.BeginGetResponse(
                (ar) =>
                {
                    try
                    {
                        using (var stream = request8.EndGetResponse(ar).GetResponseStream())
                        {
                            this.PublicAddress = Helper.ParseIPAddress(ReadText(stream));
                        }
                    }
                    catch (Exception e)
                    {
                        error = new Exception(string.Format(errFormat, e.Message, "public-ipv4"), e);
                    }
                    finally
                    {
                        Interlocked.Decrement(ref cPending);
                    }
                },
                null);

            // #9: security-groups

            var request9 = HttpWebRequest.Create(uriPrefix + "security-groups");

            request9.BeginGetResponse(
                (ar) =>
                {
                    try
                    {
                        using (var stream = request9.EndGetResponse(ar).GetResponseStream())
                        {
                            this.SecurityGroups = ReadText(stream);
                        }
                    }
                    catch (Exception e)
                    {
                        error = new Exception(string.Format(errFormat, e.Message, "security-groups"), e);
                    }
                    finally
                    {
                        Interlocked.Decrement(ref cPending);
                    }
                },
                null);

            // Poll for completion of the queries.

            while (cPending > 0)
                Thread.Sleep(250);

            if (error != null)
            {

                if (!retry)
                {
                    Thread.Sleep(5000);
                    retry = true;

                    goto retry;
                }

                throw error;
            }
        }

        /// <summary>
        /// Reads the text from the HTTP response stream passed.
        /// </summary>
        /// <param name="stream">The response stream.</param>
        /// <returns>The text read.</returns>
        private static string ReadText(Stream stream)
        {
            var ms = new EnhancedMemoryStream();

            ms.CopyFrom(stream, -1);
            ms.Position = 0;
            return ms.ReadAllText(Encoding.ASCII).Trim();
        }

        /// <summary>
        /// The AWS instance ID.
        /// </summary>
        public string InstanceID { get; private set; }

        /// <summary>
        /// Identifies the class of virtual machine hosting the instance.
        /// </summary>
        public string InstanceType { get; private set; }

        /// <summary>
        /// The Amazon datacenter availability zone.
        /// </summary>
        public string Location { get; private set; }

        /// <summary>
        /// The ID for the Amazon Machine Image.
        /// </summary>
        public string AmiID { get; private set; }

        /// <summary>
        /// Identifies the server role
        /// </summary>
        /// <remarks>
        /// <note>
        /// This is actually loaded from the <b>C:\Aws-Boot\Server-Role.txt</b> file
        /// if present.  This not actually AWS metadata.
        /// </note>
        /// </remarks>
        public string ServerRole { get; private set; }

        /// <summary>
        /// The IP address of the instance reachable from the Internet.
        /// </summary>
        public IPAddress PublicAddress { get; private set; }

        /// <summary>
        /// The IP address of the instance reachable from within the AWS cloud.
        /// </summary>
        public IPAddress LocalAddress { get; private set; }

        /// <summary>
        /// The Internet accessable hostname for this instance.
        /// </summary>
        public string PublicHostName { get; private set; }

        /// <summary>
        /// The internal AWS cloud host name for this instance.
        /// </summary>
        public string LocalHostName { get; private set; }

        /// <summary>
        /// Identifies the AWS security group (aka firewall) protecting this instance.
        /// </summary>
        public string SecurityGroups { get; private set; }
    }
}
