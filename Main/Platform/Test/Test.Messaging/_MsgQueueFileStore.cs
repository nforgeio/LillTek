//-----------------------------------------------------------------------------
// FILE:        _MsgQueueFileStore.cs
// CONTRIBUTOR: Jeff Lill
// COPYRIGHT:   Copyright (c) 2005-2015 by Jeffrey Lill.  All rights reserved.
// DESCRIPTION: Unit tests

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Reflection;
using System.Threading;

using Microsoft.VisualStudio.TestTools.UnitTesting;

using LillTek.Common;
using LillTek.Cryptography;
using LillTek.Messaging;
using LillTek.Messaging.Queuing;
using LillTek.Testing;

namespace LillTek.Messaging.Queuing.Test
{
    [TestClass]
    public class _MsgQueueFileStore
    {

        private const int MessageCount = 1000;
        private string root;

        [TestInitialize]
        public void Initialize()
        {
            // root = "c:\\temp\\queue";

            root = Helper.AddTrailingSlash(Path.GetTempPath()) + Helper.NewGuid().ToString();
        }

        [TestCleanup]
        public void Cleanup()
        {
            ClearFolders();
        }

        private void ClearFolders()
        {
            Thread.Sleep(1000);     // Give the file system the chance to flush any changes.

            try
            {
                string[] files = Directory.GetFiles(root, "*.*");

                foreach (string file in files)
                    File.Delete(file);

                Helper.DeleteFile(root, true);
            }
            catch
            {
                // Ignore
            }
        }

        [TestMethod]
        [TestProperty("Lib", "LillTek.Messaging")]
        public void MsgQueueFileStore_Basic()
        {
            MsgQueueFileStore store = null;
            int count;
            QueuedMsgInfo msgInfo;
            QueuedMsg msg, msgTest;
            object persistID;

            ClearFolders();

            try
            {
                store = new MsgQueueFileStore(root);
                store.Open();

                // Should initialize with no persisted messages

                Assert.AreEqual(0, store.Count);
                count = 0;
                foreach (QueuedMsgInfo i in store)
                    count++;
                Assert.AreEqual(0, store.Count);

                // Try some basic operations while the store is open.

                msg = new QueuedMsg();
                msg.TargetEP = "logical://target";
                msg.ResponseEP = "logical://response";
                msg.SessionID = Helper.NewGuid();
                msg.SendTime = new DateTime(2000, 1, 1);
                msg.ExpireTime = new DateTime(2000, 1, 2);
                msg.Body = "Hello World!";

                msgInfo = new QueuedMsgInfo(null, msg);
                Assert.IsNull(store.GetPersistID(msg.ID));
                store.Add(msgInfo, msg);

                Assert.AreEqual(1, store.Count);
                count = 0;
                foreach (QueuedMsgInfo i in store)
                    count++;

                Assert.AreEqual(1, store.Count);

                persistID = store.GetPersistID(msg.ID);
                Assert.IsNotNull(persistID);

                msgTest = store.Get(persistID);
                msgTest.DeserializedBody();
                Assert.AreEqual(msg, msgTest);

                store.SetDeliveryAttempt(persistID, 10, new DateTime(2001, 1, 1));
                msgInfo = store.GetInfo(persistID);
                Assert.AreEqual(10, msgInfo.DeliveryAttempts);
                Assert.AreEqual(new DateTime(2001, 1, 1), msgInfo.DeliveryTime);

                store.SetPriority(persistID, DeliveryPriority.Low);
                msgInfo = store.GetInfo(persistID);
                Assert.AreEqual(DeliveryPriority.Low, msgInfo.Priority);
                msgTest = store.Get(persistID);
                msgTest.DeserializedBody();
                Assert.AreEqual(DeliveryPriority.Low, msgTest.Priority);

                store.Modify(persistID, "logical://target2", new DateTime(2002, 1, 1), new DateTime(2002, 1, 2), DeliveryStatus.Poison);
                msgInfo = store.GetInfo(persistID);
                Assert.AreEqual((MsgEP)"logical://target2", msgInfo.TargetEP);
                Assert.AreEqual(new DateTime(2002, 1, 1), msgInfo.DeliveryTime);
                Assert.AreEqual(new DateTime(2002, 1, 2), msgInfo.ExpireTime);
                Assert.AreEqual(DeliveryStatus.Poison, msgInfo.Status);
                msgTest = store.Get(persistID);
                msgTest.DeserializedBody();
                Assert.AreEqual(DeliveryPriority.Low, msgTest.Priority);
                Assert.AreEqual(new DateTime(2002, 1, 2), msgTest.ExpireTime);

                Assert.AreEqual((MsgEP)"logical://target2", msgTest.TargetEP);
                Assert.AreEqual(msg.ID, msgTest.ID);
                Assert.AreEqual(msg.SessionID, msgTest.SessionID);
                Assert.AreEqual(msg.SendTime, msgTest.SendTime);
                Assert.AreEqual(new DateTime(2002, 1, 2), msgTest.ExpireTime);
                Assert.AreEqual(msg.Body, msgTest.Body);

                store.Remove(persistID);
                Assert.AreEqual(0, store.Count);
                Assert.IsNull(store.GetPersistID(msg.ID));
                Assert.IsNull(store.Get(persistID));
                msgTest.DeserializedBody();
                Assert.IsNull(store.GetInfo(persistID));

                // Persist another message and then close and reopen
                // the store and confirm that the message is still
                // there.

                msg = new QueuedMsg();
                msg.TargetEP = "logical://target2";
                msg.ResponseEP = "logical://response2";
                msg.SessionID = Helper.NewGuid();
                msg.SendTime = new DateTime(2000, 1, 1);
                msg.ExpireTime = new DateTime(2000, 1, 2);
                msg.Body = "Hello World!";

                msgInfo = new QueuedMsgInfo(null, msg);
                store.Add(msgInfo, msg);

                store.Close();
                store = new MsgQueueFileStore(root);
                store.Open();

                persistID = store.GetPersistID(msg.ID);
                Assert.IsNotNull(persistID);

                msgTest = store.Get(persistID);
                msgTest.DeserializedBody();
                Assert.AreEqual(msg, msgTest);
            }
            finally
            {
                if (store != null)
                    store.Close();
            }
        }

        [TestMethod]
        [TestProperty("Lib", "LillTek.Messaging")]
        public void MsgQueueFileStore_Multiple()
        {
            MsgQueueFileStore store = null;
            int count;
            QueuedMsgInfo msgInfo;
            QueuedMsg msg;
            object persistID;
            Guid[] ids;

            ClearFolders();

            try
            {
                store = new MsgQueueFileStore(root);
                store.Open();

                ids = new Guid[MessageCount];

                for (int i = 0; i < MessageCount; i++)
                {
                    msg = new QueuedMsg();
                    msg.TargetEP = "logical://test/" + i.ToString();
                    msg.Body = i;
                    msgInfo = new QueuedMsgInfo(null, msg);

                    store.Add(msgInfo, msg);
                    ids[i] = msg.ID;
                }

                Assert.AreEqual(MessageCount, store.Count);
                count = 0;
                foreach (QueuedMsgInfo i in store)
                    count++;

                Assert.AreEqual(MessageCount, count);

                for (int i = 0; i < ids.Length; i++)
                {
                    Guid id = ids[i];

                    persistID = store.GetPersistID(id);
                    Assert.IsNotNull(persistID);

                    msgInfo = store.GetInfo(persistID);
                    Assert.IsNotNull(msgInfo);
                    Assert.AreEqual((MsgEP)("logical://test/" + i.ToString()), msgInfo.TargetEP);

                    msg = store.Get(persistID);
                    msg.DeserializedBody();
                    Assert.AreEqual(i, (int)msg.Body);
                }

                // Close and reopen the store, making sure that all of the 
                // messages are still there.

                store.Close();
                store.Open();
                Assert.AreEqual(MessageCount, store.Count);

                for (int i = 0; i < ids.Length; i++)
                {
                    Guid id = ids[i];

                    persistID = store.GetPersistID(id);
                    Assert.IsNotNull(persistID);

                    msgInfo = store.GetInfo(persistID);
                    Assert.IsNotNull(msgInfo);
                    Assert.AreEqual((MsgEP)("logical://test/" + i.ToString()), msgInfo.TargetEP);

                    msg = store.Get(persistID);
                    msg.DeserializedBody();
                    Assert.AreEqual(i, (int)msg.Body);
                }

                // Remove all of the messges and verify that they're gone

                for (int i = 0; i < MessageCount; i++)
                {
                    persistID = store.GetPersistID(ids[i]);
                    Assert.IsNotNull(persistID);

                    store.Remove(persistID);
                    Assert.IsNull(store.GetPersistID(ids[i]));
                    Assert.IsNull(store.Get(persistID));
                    Assert.IsNull(store.GetInfo(persistID));
                }

                // Close and reopen the store and verify that there are no messages.

                store.Close();
                store.Open();

                Assert.AreEqual(0, store.Count);
            }
            finally
            {
                if (store != null)
                    store.Close();
            }
        }

        [TestMethod]
        [TestProperty("Lib", "LillTek.Messaging")]
        public void MsgQueueFileStore_Corrupt_MissingIndex()
        {
            MsgQueueFileStore store = null;
            int count;
            QueuedMsgInfo msgInfo;
            QueuedMsg msg;
            object persistID;
            Guid[] ids;

            // Create a store with a bunch of messages, close it, and
            // then delete the index file.  Then open the store and 
            // verify that it rebuilds the index.

            ClearFolders();

            try
            {
                store = new MsgQueueFileStore(root);
                store.Open();

                ids = new Guid[MessageCount];

                for (int i = 0; i < MessageCount; i++)
                {
                    msg = new QueuedMsg();
                    msg.TargetEP = "logical://test/" + i.ToString();
                    msg.Body = i;
                    msgInfo = new QueuedMsgInfo(null, msg);

                    store.Add(msgInfo, msg);
                    ids[i] = msg.ID;
                }

                Assert.AreEqual(MessageCount, store.Count);
                count = 0;
                foreach (QueuedMsgInfo i in store)
                    count++;

                Assert.AreEqual(MessageCount, count);

                for (int i = 0; i < ids.Length; i++)
                {
                    Guid id = ids[i];

                    persistID = store.GetPersistID(id);
                    Assert.IsNotNull(persistID);

                    msgInfo = store.GetInfo(persistID);
                    Assert.IsNotNull(msgInfo);
                    Assert.AreEqual((MsgEP)("logical://test/" + i.ToString()), msgInfo.TargetEP);

                    msg = store.Get(persistID);
                    msg.DeserializedBody();
                    Assert.AreEqual(i, (int)msg.Body);
                }

                // Close the store, delete the index, then reopen the store and
                // verify that it rebuilt the index.

                store.Close();
                File.Delete(root + "\\messages.index");
                store.Open();
                Assert.AreEqual(MessageCount, store.Count);

                for (int i = 0; i < ids.Length; i++)
                {
                    Guid id = ids[i];

                    persistID = store.GetPersistID(id);
                    Assert.IsNotNull(persistID);

                    msgInfo = store.GetInfo(persistID);
                    Assert.IsNotNull(msgInfo);
                    Assert.AreEqual((MsgEP)("logical://test/" + i.ToString()), msgInfo.TargetEP);

                    msg = store.Get(persistID);
                    msg.DeserializedBody();
                    Assert.AreEqual(i, (int)msg.Body);
                }
            }
            finally
            {
                if (store != null)
                    store.Close();
            }
        }

        [TestMethod]
        [TestProperty("Lib", "LillTek.Messaging")]
        public void MsgQueueFileStore_Corrupt_MissingMessage()
        {
            MsgQueueFileStore store = null;
            int count;
            QueuedMsgInfo msgInfo;
            QueuedMsg msg;
            object persistID;
            Guid[] ids;
            Dictionary<Guid, bool> deletedIDs;
            List<string> deletedFiles;

            // Create a store with a bunch of messages then close
            // it and delete some of the message files.  Then reopen
            // the store and verify that it deleted metadata entries
            // for the removed messages.

            ClearFolders();

            try
            {
                store = new MsgQueueFileStore(root);
                store.Open();

                ids = new Guid[MessageCount];

                for (int i = 0; i < MessageCount; i++)
                {
                    msg = new QueuedMsg();
                    msg.TargetEP = "logical://test/" + i.ToString();
                    msg.Body = i;
                    msgInfo = new QueuedMsgInfo(null, msg);

                    store.Add(msgInfo, msg);
                    ids[i] = msg.ID;
                }

                Assert.AreEqual(MessageCount, store.Count);
                count = 0;
                foreach (QueuedMsgInfo i in store)
                    count++;

                Assert.AreEqual(MessageCount, count);

                for (int i = 0; i < ids.Length; i++)
                {
                    Guid id = ids[i];

                    persistID = store.GetPersistID(id);
                    Assert.IsNotNull(persistID);

                    msgInfo = store.GetInfo(persistID);
                    Assert.IsNotNull(msgInfo);
                    Assert.AreEqual((MsgEP)("logical://test/" + i.ToString()), msgInfo.TargetEP);

                    msg = store.Get(persistID);
                    msg.DeserializedBody();
                    Assert.AreEqual(i, (int)msg.Body);
                }

                // Close the store and then delete 1/2 of the message files.

                deletedIDs = new Dictionary<Guid, bool>();
                deletedFiles = new List<string>();

                for (int i = 0; i < MessageCount / 2; i++)
                {
                    deletedIDs.Add(ids[i], true);
                    deletedFiles.Add(store.GetMessagePath(ids[i], false));
                }

                store.Close();

                foreach (string file in deletedFiles)
                    File.Delete(file);

                // Reopen the store and verify that it noticed the deleted files 
                // and that their metadata was deleted from the index and remaining message
                // metadata is still intact.

                store.Open();
                Assert.AreEqual(MessageCount - MessageCount / 2, store.Count);

                for (int i = 0; i < ids.Length; i++)
                {
                    Guid id = ids[i];

                    if (deletedIDs.ContainsKey(id))
                        Assert.IsNull(store.GetPersistID(id));
                    else
                    {
                        persistID = store.GetPersistID(id);
                        Assert.IsNotNull(persistID);

                        msgInfo = store.GetInfo(persistID);
                        Assert.IsNotNull(msgInfo);
                        Assert.AreEqual((MsgEP)("logical://test/" + i.ToString()), msgInfo.TargetEP);

                        msg = store.Get(persistID);
                        msg.DeserializedBody();
                        Assert.AreEqual(i, (int)msg.Body);
                    }
                }
            }
            finally
            {
                if (store != null)
                    store.Close();
            }
        }

        [TestMethod]
        [TestProperty("Lib", "LillTek.Messaging")]
        public void MsgQueueFileStore_Corrupt_ExtraMessage()
        {
            MsgQueueFileStore store = null;
            int count;
            QueuedMsgInfo msgInfo;
            QueuedMsg msg;
            object persistID;
            Guid[] ids;

            // Create a store with a bunch of messages then close
            // it and explicitly add a message file.  Then reopen
            // the store and verify that it loaded the metadata
            // for the new message.

            ClearFolders();

            try
            {
                store = new MsgQueueFileStore(root);
                store.Open();

                ids = new Guid[MessageCount];

                for (int i = 0; i < MessageCount; i++)
                {
                    msg = new QueuedMsg();
                    msg.TargetEP = "logical://test/" + i.ToString();
                    msg.Body = i;
                    msgInfo = new QueuedMsgInfo(null, msg);

                    store.Add(msgInfo, msg);
                    ids[i] = msg.ID;
                }

                Assert.AreEqual(MessageCount, store.Count);
                count = 0;
                foreach (QueuedMsgInfo i in store)
                    count++;

                Assert.AreEqual(MessageCount, count);

                for (int i = 0; i < ids.Length; i++)
                {
                    Guid id = ids[i];

                    persistID = store.GetPersistID(id);
                    Assert.IsNotNull(persistID);

                    msgInfo = store.GetInfo(persistID);
                    Assert.IsNotNull(msgInfo);
                    Assert.AreEqual((MsgEP)("logical://test/" + i.ToString()), msgInfo.TargetEP);

                    msg = store.Get(persistID);
                    msg.DeserializedBody();
                    Assert.AreEqual(i, (int)msg.Body);
                }

                // Close the store and explicitly create a new message file.

                Guid newID = Helper.NewGuid();
                string newPath = store.GetMessagePath(newID, true);

                msg = new QueuedMsg();
                msg.ID = newID;
                msg.TargetEP = "logical://foo/bar";
                msg.Body = "Hello World!";
                msgInfo = new QueuedMsgInfo(null, msg);

                store.WriteMessage(newPath, msg, msgInfo);      // I'm calling this internal write method
                                                                // so that the metadata won't be saved to the index.
                store.Close();

                // Reopen the store and verify that it loaded the metadata
                // for the new message and that the other message metadata is 
                // still intact.

                store.Open();
                Assert.AreEqual(MessageCount + 1, store.Count);

                persistID = store.GetPersistID(newID);
                Assert.IsNotNull(persistID);

                msgInfo = store.GetInfo(persistID);
                Assert.IsNotNull(msgInfo);
                Assert.AreEqual((MsgEP)"logical://foo/bar", msgInfo.TargetEP);

                msg = store.Get(persistID);
                msg.DeserializedBody();
                Assert.AreEqual("Hello World!", (string)msg.Body);

                for (int i = 0; i < ids.Length; i++)
                {
                    Guid id = ids[i];

                    persistID = store.GetPersistID(id);
                    Assert.IsNotNull(persistID);

                    msgInfo = store.GetInfo(persistID);
                    Assert.IsNotNull(msgInfo);
                    Assert.AreEqual((MsgEP)("logical://test/" + i.ToString()), msgInfo.TargetEP);

                    msg = store.Get(persistID);
                    msg.DeserializedBody();
                    Assert.AreEqual(i, (int)msg.Body);
                }
            }
            finally
            {
                if (store != null)
                    store.Close();
            }
        }

        [TestMethod]
        [TestProperty("Lib", "LillTek.Messaging")]
        public void MsgQueueFileStore_Corrupt_Messages()
        {
            MsgQueueFileStore store = null;
            int count;
            QueuedMsgInfo msgInfo;
            QueuedMsg msg;
            object persistID;
            Guid[] ids;
            Dictionary<Guid, bool> corruptIDs;
            List<string> corruptFiles;

            // Create a store with a bunch of messages then close
            // it and corrupt some of the message files.  Then attempt
            // to open the files and verify that the corrupted files
            // are detected.

            ClearFolders();

            try
            {
                store = new MsgQueueFileStore(root);
                store.Open();

                ids = new Guid[MessageCount];

                for (int i = 0; i < MessageCount; i++)
                {
                    msg = new QueuedMsg();
                    msg.TargetEP = "logical://test/" + i.ToString();
                    msg.Body = i;
                    msgInfo = new QueuedMsgInfo(null, msg);

                    store.Add(msgInfo, msg);
                    ids[i] = msg.ID;
                }

                Assert.AreEqual(MessageCount, store.Count);
                count = 0;
                foreach (QueuedMsgInfo i in store)
                    count++;
                Assert.AreEqual(MessageCount, count);

                for (int i = 0; i < ids.Length; i++)
                {
                    Guid id = ids[i];

                    persistID = store.GetPersistID(id);
                    Assert.IsNotNull(persistID);

                    msgInfo = store.GetInfo(persistID);
                    Assert.IsNotNull(msgInfo);
                    Assert.AreEqual((MsgEP)("logical://test/" + i.ToString()), msgInfo.TargetEP);

                    msg = store.Get(persistID);
                    msg.DeserializedBody();
                    Assert.AreEqual(i, (int)msg.Body);
                }

                // Corrupt 1/2 of the message files in various ways.

                corruptIDs = new Dictionary<Guid, bool>();
                corruptFiles = new List<string>();

                for (int i = 0; i < MessageCount / 2; i++)
                {
                    corruptIDs.Add(ids[i], true);
                    corruptFiles.Add(store.GetMessagePath(ids[i], false));
                }

                int cbTruncate = 0;

                for (int i = 0; i < corruptFiles.Count; i++)
                {
                    string file = corruptFiles[i];

                    using (EnhancedFileStream fs = new EnhancedFileStream(file, FileMode.Open, FileAccess.ReadWrite))
                    {
                        if ((i & 1) == 0)
                        {
                            // Truncate the file by at least one byte

                            int cb;

                            cb = Math.Min((int)fs.Length - 1, cbTruncate++);
                            fs.SetLength(cb);
                        }
                        else
                        {
                            // Mess with a byte at random position in the file.

                            int pos = Helper.Rand((int)fs.Length);
                            byte b;

                            fs.Position = pos;
                            b = (byte)fs.ReadByte();
                            fs.Position = pos;
                            fs.WriteByte((byte)(~b));
                        }
                    }
                }

                // Load all of the message files and verify that the corrupt files
                // are detected.

                for (int i = 0; i < ids.Length; i++)
                {
                    Guid id = ids[i];

                    if (corruptIDs.ContainsKey(id))
                    {
                        try
                        {
                            persistID = store.GetPersistID(id);
                            Assert.IsNotNull(persistID);

                            msgInfo = store.GetInfo(persistID);
                            Assert.IsNotNull(msgInfo);
                            Assert.AreEqual((MsgEP)("logical://test/" + i.ToString()), msgInfo.TargetEP);

                            msg = store.Get(persistID);
                            Assert.Fail("Exception expected");
                        }
                        catch
                        {
                            // Expecting an exception
                        }
                    }
                    else
                    {
                        persistID = store.GetPersistID(id);
                        Assert.IsNotNull(persistID);

                        msgInfo = store.GetInfo(persistID);
                        Assert.IsNotNull(msgInfo);
                        Assert.AreEqual((MsgEP)("logical://test/" + i.ToString()), msgInfo.TargetEP);

                        msg = store.Get(persistID);
                        msg.DeserializedBody();
                        Assert.AreEqual(i, (int)msg.Body);
                    }
                }
            }
            finally
            {
                if (store != null)
                    store.Close();
            }
        }
    }
}

