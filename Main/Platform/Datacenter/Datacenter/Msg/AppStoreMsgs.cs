//-----------------------------------------------------------------------------
// FILE:        AppStoreMsgs.cs
// CONTRIBUTOR: Jeff Lill
// COPYRIGHT:   Copyright (c) 2005-2015 by Jeffrey Lill.  All rights reserved.
// DESCRIPTION: Implements the messages and other classes used to for 
//              communication between AppStore and AppStoreHandler 
//              instances.

using System;
using System.Diagnostics;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Text;

using LillTek.Common;
using LillTek.Cryptography;
using LillTek.Datacenter;
using LillTek.Messaging;

namespace LillTek.Datacenter.Msgs.AppStore
{
    /// <summary>
    /// A general purpose one-way message used to communicate between application
    /// store and application cache instances.
    /// </summary>
    public class AppStoreMsg : PropertyMsg
    {
        //---------------------------------------------------------------------
        // Static members

        /// <summary>
        /// Command to return the logical endpoint for the primary 
        /// application store instance.
        /// </summary>
        public const string GetPrimaryCmd = "get-primary-ep";

        /// <summary>
        /// Command to list the packages on an application store.
        /// </summary>
        public const string ListCmd = "list";

        /// <summary>
        /// Command to remove a package.
        /// </summary>
        public const string RemoveCmd = "remove";

        /// <summary>
        /// Command to synchronize an application store against the primary store.
        /// </summary>
        public const string SyncCmd = "sync";

        /// <summary>
        /// Command to verify that the application store instance has a
        /// specific application package ready for downloading.  If the
        /// package is not available locally in the store is not the
        /// primary, then the store will attempt to download the package
        /// from the primary before returning to the client.  The result
        /// includes the store instance endpoint.
        /// </summary>
        public const string DownloadCmd = "download";

        /// <summary>
        /// Returns the type ID string to be used when serializing the message.
        /// </summary>
        public static new string GetTypeID()
        {
            return "LT.DC.AppStoreMsg";
        }

        //---------------------------------------------------------------------
        // Instance members

        /// <summary>
        /// Constructor.
        /// </summary>
        public AppStoreMsg()
        {
        }

        /// <summary>
        /// Constructor.
        /// </summary>
        /// <param name="command">The command.</param>
        public AppStoreMsg(string command)
        {
            this.Command = command;
        }

        /// <summary>
        /// Constructor.
        /// </summary>
        /// <param name="command">The command.</param>
        /// <param name="appRef">Identifies an application package.</param>
        public AppStoreMsg(string command, AppRef appRef)
        {
            this.Command = command;
            this.AppRef  = appRef;
        }

        /// <summary>
        /// Protected constructor that does not instantiate the property hash table.
        /// </summary>
        /// <param name="param">Pass <see cref="Stub.Param" />.</param>
        protected AppStoreMsg(Stub param)
            : base(param)
        {
        }

        /// <summary>
        /// The command.
        /// </summary>
        public string Command
        {
            get { return base._Get("cmd"); }
            set { base._Set("cmd", value); }
        }

        /// <summary>
        /// Identifies an application package.
        /// </summary>
        public AppRef AppRef
        {
            get { return new AppRef(base._Get("appref")); }
            set { base._Set("appref", value.ToString()); }
        }

        /// <summary>
        /// Generates a clone of this message instance, generating a new 
        /// <see cref="Msg._MsgID" /> property if the original ID is
        /// not empty.
        /// </summary>
        /// <returns>The cloned message.</returns>
        public override Msg Clone()
        {
            AppStoreMsg clone;

            clone = new AppStoreMsg(Stub.Param);
            clone.CopyBaseFields(this, true);

            return clone;
        }
    }

    /// <summary>
    /// A general purpose message used to query for services from application stores.
    /// </summary>
    public class AppStoreQuery : AppStoreMsg
    {
        //---------------------------------------------------------------------
        // Static members

        /// <summary>
        /// Returns the type ID string to be used when serializing the message.
        /// </summary>
        public static new string GetTypeID()
        {
            return "LT.DC.AppStoreQuery";
        }

        //---------------------------------------------------------------------
        // Instance members

        /// <summary>
        /// Constructor.
        /// </summary>
        public AppStoreQuery()
        {
        }

        /// <summary>
        /// Constructor.
        /// </summary>
        /// <param name="command">The command.</param>
        public AppStoreQuery(string command)
            : base(command)
        {
        }

        /// <summary>
        /// Constructor.
        /// </summary>
        /// <param name="command">The command.</param>
        /// <param name="appRef">Identifies an application package.</param>
        public AppStoreQuery(string command, AppRef appRef)
            : base(command, appRef)
        {
        }

        /// <summary>
        /// Protected constructor that does not instantiate the property hash table.
        /// </summary>
        /// <param name="param">Pass <see cref="Stub.Param" />.</param>
        private AppStoreQuery(Stub param)
            : base(param)
        {
        }

        /// <summary>
        /// Generates a clone of this message instance, generating a new 
        /// <see cref="Msg._MsgID" /> property if the original ID is
        /// not empty.
        /// </summary>
        /// <returns>The cloned message.</returns>
        public override Msg Clone()
        {
            AppStoreQuery clone;

            clone = new AppStoreQuery(Stub.Param);
            clone.CopyBaseFields(this, true);

            return clone;
        }
    }

    /// <summary>
    /// Holds the response to an <see cref="AppStoreMsg" /> message.
    /// </summary>
    public sealed class AppStoreAck : BlobPropertyMsg, IAck
    {
        //---------------------------------------------------------------------
        // Static members

        /// <summary>
        /// Returns the type ID string to be used when serializing the message.
        /// </summary>
        public static new string GetTypeID()
        {
            return "LT.DC.AppStoreAck";
        }

        //---------------------------------------------------------------------
        // Instance members

        private AppPackageInfo[] packages = null;

        /// <summary>
        /// Constructor.
        /// </summary>
        public AppStoreAck()
        {
        }

        /// <summary>
        /// Constructs an instance from an exception to be transmitted back
        /// to the client.
        /// </summary>
        /// <param name="e">The exception.</param>
        public AppStoreAck(Exception e)
        {
            this.Exception         = e.Message;
            this.ExceptionTypeName = e.GetType().FullName;
        }

        /// <summary>
        /// Protected constructor that does not instantiate the property hash table.
        /// </summary>
        /// <param name="param">Pass <see cref="Stub.Param" />.</param>
        private AppStoreAck(Stub param)
            : base(param)
        {
        }

        /// <summary>
        /// The logical endpoint requested (or <c>null</c>).
        /// </summary>
        public string StoreEP
        {
            get { return base._Get("store-ep"); }

            set
            {
                if (value != null)
                    base._Set("store-ep", value);
            }
        }

        /// <summary>
        /// Lists the packages available on an application store.
        /// </summary>
        public AppPackageInfo[] Packages
        {
            get
            {
                if (packages != null)
                    return packages;

                if (base._Data == null || base._Data.Length == 0)
                    return packages = new AppPackageInfo[0];

                using (var reader = new StreamReader(new MemoryStream(base._Data), Encoding.UTF8))
                {
                    var list = new List<AppPackageInfo>();

                    while (true)
                    {

                        string line;

                        line = reader.ReadLine();
                        if (line == null)
                            break;

                        list.Add(AppPackageInfo.Parse(line));
                    }

                    return packages = list.ToArray();
                }
            }

            set
            {
                if (value == null)
                {
                    packages = null;
                    return;
                }

                packages = value;

                var ms     = new MemoryStream();
                var writer = new StreamWriter(ms, Encoding.UTF8);

                try
                {
                    for (int i = 0; i < packages.Length; i++)
                        writer.WriteLine(packages[i].ToString());

                    writer.Flush();
                    base._Data = ms.ToArray();
                }
                finally
                {
                    writer.Close();
                }
            }
        }

        /// <summary>
        /// Generates a clone of this message instance, generating a new 
        /// <see cref="Msg._MsgID" /> property if the original ID is
        /// not empty.
        /// </summary>
        /// <returns>The cloned message.</returns>
        public override Msg Clone()
        {
            AppStoreAck clone;

            clone = new AppStoreAck(Stub.Param);
            clone.CopyBaseFields(this, true);

            return clone;
        }

        //---------------------------------------------------------------------
        // IAck Implementation

        /// <summary>
        /// The exception's message string if the was an exception detected
        /// on by the server (null or the empty string if there was no error).
        /// </summary>
        public string Exception
        {
            get { return (string)base["_exception"]; }
            set { base["_exception"] = value; }
        }

        /// <summary>
        /// The fully qualified name of the exception type.
        /// </summary>
        public string ExceptionTypeName
        {
            get { return (string)base["_exception-type"]; }
            set { base["_exception-type"] = value; }
        }
    }
}
