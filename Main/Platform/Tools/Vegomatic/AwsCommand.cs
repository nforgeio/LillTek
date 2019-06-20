//-----------------------------------------------------------------------------
// FILE:        AwsCommand.cs
// CONTRIBUTOR: Jeff Lill
// COPYRIGHT:   Copyright (c) 2005-2015 by Jeffrey Lill. All rights reserved.
// DESCRIPTION: Implements the Amazon Web Service (AWS) commands.

using System;
using System.IO;
using System.Text;
using System.Threading;
using System.Collections;
using System.Collections.Generic;

using LillTek.Common;

namespace LillTek.Tools.Vegomatic
{
    /// <summary>
    /// Implements the Amazon Web Service (AWS) commands.
    /// </summary>
    public static class AwsCommand
    {

        /// <summary>
        /// Executes the specified AWS command.
        /// </summary>
        /// <param name="args">The command arguments.</param>
        /// <returns>0 on success, a non-zero error code otherwise.</returns>
        public static int Execute(string[] args)
        {
            const string usage =
@"
Usage: 

-------------------------------------------------------------------------------
vegomatic aws instance-info

Retrieves and prints the AWS instance metadata for the current EC2 instance.
";

            if (args.Length < 1)
            {
                Program.Error(usage);
                return 1;
            }

            switch (args[0].ToLowerInvariant())
            {
                case "instance-info":

                    if (!Helper.IsAWS)
                    {
                        Program.Error("Not hosted on AWS.");
                        return 1;
                    }

                    AwsInstanceInfo info = Helper.AwsInstanceInfo;

                    Console.WriteLine("AWS Instance Metadata");
                    Console.WriteLine("---------------------");
                    Console.WriteLine("ServerRole:     {0}", info.ServerRole);
                    Console.WriteLine("Location:       {0}", info.Location);
                    Console.WriteLine("InstanceID:     {0}", info.InstanceID);
                    Console.WriteLine("InstanceType:   {0}", info.InstanceType);
                    Console.WriteLine("LocalHostName:  {0}", info.LocalHostName);
                    Console.WriteLine("LocalAddress:   {0}", info.LocalAddress);
                    Console.WriteLine("PublicHostName: {0}", info.PublicHostName);
                    Console.WriteLine("PublicAddress:  {0}", info.PublicAddress);
                    Console.WriteLine("SecurityGroups: {0}", info.SecurityGroups);

                    return 0;

                default:

                    Program.Error(usage);
                    return 1;
            }
        }
    }
}
