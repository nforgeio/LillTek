//-----------------------------------------------------------------------------
// FILE:        MsgQueueFileStore.cs
// CONTRIBUTOR: Jeff Lill
// COPYRIGHT:   Copyright (c) 2005-2015 by Jeffrey Lill.  All rights reserved.
// DESCRIPTION: A file system based IMsgQueueStore implementation.

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;

using LillTek.Common;
using LillTek.Cryptography;
using LillTek.Messaging;
using LillTek.Messaging.Internal;

namespace LillTek.Messaging.Queuing
{
    /// <summary>
    /// A file system based <see cref="IMsgQueueStore"/> implementation.
    /// </summary>
    /// <remarks>
    /// <para>
    /// This implementation persists message queue messages to a folder
    /// and subfolders in the file system.  The root folder holds the
    /// <c>message.index</c> file and up to 1000 subfolders numbered
    /// from 000 to 999.  The <b>message.index</b> file holds a cached
    /// copy of the message metadata and the subfolders hold the message
    /// files, each file named using the message's GUID, as in:
    /// <c>C7DB7A74-668A-4cf0-8B49-92453318FCE0.msg</c>.
    /// </para>
    /// <para>
    /// Each message file holds all of the information necessary to
    /// route the message to its ultimate destination.  This includes
    /// the message headers including the target queue endpoint as
    /// well as the message body.
    /// </para>
    /// <para>
    /// The message index file holds the important metadata about
    /// each message file.  This file is read when the store
    /// is opened to quickly load the metadata for each message
    /// on the backing store.  While the store is open, it
    /// maintains an in-memory copy of any changes made to to the
    /// set of messages and their metadata.  These changes are
    /// not written to the index file until the store is
    /// closed.  This means that the index file may be out of
    /// sync with the actual messages and metadata if the system
    /// crashes before index is rewritten.
    /// </para>
    /// <para>
    /// The <see cref="MsgQueueFileStore" /> handles this by
    /// rebuilding the index file when necessary.  Since all necessary
    /// metadata is stored in the message files, the index file can
    /// be rebuilt by scanning all of the message files.  The trick
    /// is to determine when this scan is necessary when the store
    /// is opened.  There are three scenarios handled by this class:
    /// </para>
    /// <list type="number">
    ///     <item>The index file does not exit.</item>
    ///     <item>The system crashed before the store could update the index.</item>
    ///     <item>The set of messages in the file system differ from what's in the index.</item>
    /// </list>
    /// <para>
    /// So, the main purpose of the index file is to speed up the process
    /// of opening a store when there's a large number of messages persisted
    /// by avoiding having to go through the lengthly process of opening
    /// and scanning each message.  The only time this scan will be
    /// necessary is when any of the scanarios above are detected.
    /// </para>
    /// <para><b><u>Message Index File Format</u></b></para>
    /// <para>
    /// The index file has a binary format consisting of a header record
    /// followed by records with metadata describing each persisted message.
    /// Here's the layout of the header record (note that all integers are
    /// stored in network (big-endian) byte order):
    /// </para>
    /// <code language="none">
    /// +------------------+
    /// |   Magic Number   |    32-bits: 0xCB1CDC85
    /// +------------------+
    /// |  Format Version  |    32-bits: 0
    /// +------------------+
    /// |     Reserved     |    32-bits: 0
    /// +------------------+
    /// |    Open Flag     |    32-bits: 1 if store is open or crashed
    /// +------------------+
    /// |   Message Count  |    32-bits: Number of message metadata records
    /// +------------------+
    /// </code>
    /// <para>
    /// The header record is followed by <b>Message Count</b> metadata
    /// and file path records.  These records are stored <see cref="ArgCollection" />
    /// formatted strings (using '=' for the assignment character and
    /// TAB as the separator).  Note that the file name stored is relative
    /// to the root folder.  This means that the entire message queue folder
    /// can be relocated, if necessary, when the queue service is not
    /// running.
    /// </para>
    /// <code language="none">
    /// +------------------+
    /// |  Metadata Size   |    16-bits: Number of metadata bytes to follow
    /// +------------------+
    /// |                  |
    /// |                  |
    /// |                  |
    /// |       UTF-8      |    byte[]:  The Metadata as a UTF-8 encoded <see cref="ArgCollection" />
    /// |                  |             
    /// |                  |
    /// |                  |
    /// +------------------+
    /// |  File Path Size  |    16-bits: Number of file name characters to follow
    /// +------------------+
    /// |                  |
    /// |                  |
    /// |                  |
    /// |       UTF-8      |    byte[]:  The relative file path
    /// |                  |             
    /// |                  |
    /// |                  |
    /// +------------------+
    /// </code>
    /// <para><b><u>Message File Format</u></b></para>
    /// <para>
    /// Messages are stored individually in binary formatted files consisting of
    /// a small header, the message payload, and the message metadata.  The metadata
    /// is explicitly placed at the end of the file so that it can be easily
    /// rewritten as the message moves between queues or other metadata values
    /// change.  Here's the file layout (note that all integers are formatted
    /// as big-endian):
    /// </para>
    /// <code language="none">
    /// +------------------+
    /// |   Magic Number   |    32-bits: 0x8E2C366D
    /// +------------------+
    /// |  Format Version  |    32-bits: 0
    /// +------------------+
    /// |     Reserved     |    32-bits: 0
    /// +------------------+
    /// |                  |
    /// |                  |
    /// |     MD5 Hash     |    16-bytes: MD5 Hash of the remaining data
    /// |                  |              in the file
    /// |                  |
    /// +------------------+
    ///
    /// +------------------+
    /// |    Body Size     |    32-bits: # of serialized message body bytes
    /// +------------------+
    /// |                  |
    /// |                  |
    /// |    Serialized    |    Count:   The UTF-8 encoded <see cref="ArgCollection" />
    /// |   Message Body   |
    /// |                  |
    /// |                  |
    /// +------------------+
    /// 
    /// +------------------+
    /// |  Metadata Size   |    16-bits: Number of metadata bytes to follow
    /// +------------------+
    /// |                  |
    /// |                  |
    /// |                  |
    /// |       UTF-8      |    byte[]:  The Metadata as a UTF-8 encoded <see cref="ArgCollection" />
    /// |                  |
    /// |                  |
    /// |                  |
    /// +------------------+
    /// </code>
    /// <para>
    /// The metadata stored as single string formatted by <see cref="ArgCollection" />
    /// using '=' as the assignment character and TAB as the separator.
    /// </para>
    /// </remarks>
    /// <threadsafety instance="true" />
    public sealed class MsgQueueFileStore : IMsgQueueStore, ILockable
    {
        private const int IndexMagic     = unchecked((int)0xCB1CDC85);
        private const int MsgMagic       = unchecked((int)0x8E2C366D);
        private const int OpenFlagOffset = 12;                          // Byte offset of the index file "open" flag
        private const int MsgMD5Offset   = 12;                          // Byte offset of the MD5 hash
        private const int MsgHeaderSize  = 12 + MD5Hasher.DigestSize;   // Byte size of a message file header
        private const int FolderCount    = 1000;

        private Dictionary<Guid, QueuedMsgInfo> messages;   // The message metadata
        private EnhancedFileStream              fsIndex;    // The index file (held open to gaurantee exclusive access)
        private string                          indexPath;  // The fully qualified index file path
        private string                          root;       // The fully qualified path of the message folder
                                                            // (includes a termninating "\\".
        private byte[]                          md5Zeros;   // 16 bytes of zeros used for writing MD5 placeholder

        //---------------------------------------------------------------------
        // Implementation Note
        //
        // The QueuedMsgInfo.ProviderData property is set to the fully
        // path to the message's file and the PersistID is the message's ID.

        /// <summary>
        /// Constructor.
        /// </summary>
        /// <param name="messageFolder">The path to the folder where the messages files will be located.</param>
        public MsgQueueFileStore(string messageFolder)
        {
            this.messages = null;
            this.fsIndex  = null;
            this.root     = Helper.AddTrailingSlash(Path.GetFullPath(messageFolder));
            this.md5Zeros = new byte[MD5Hasher.DigestSize];
        }

        /// <summary>
        /// Opens the store, preparing it for reading and writing messages and
        /// metadata to the backing store.
        /// </summary>
        public void Open()
        {
            bool newIndexFile;

            using (TimedLock.Lock(this))
            {
                if (this.messages != null)
                    throw new InvalidOperationException("Message store is already open.");

                indexPath = root + "messages.index";
                Helper.CreateFileTree(indexPath);

                newIndexFile = !File.Exists(indexPath);
                fsIndex      = new EnhancedFileStream(indexPath, FileMode.OpenOrCreate, FileAccess.ReadWrite);

                LoadIndex(newIndexFile);
            }
        }

        /// <summary>
        /// Closes the store, releasing any resources.
        /// </summary>
        public void Close()
        {
            if (fsIndex != null)
            {
                SaveIndex(false);

                fsIndex.Close();
                fsIndex = null;
            }

            messages = null;
        }

        /// <summary>
        /// Returns the number of messages currently persisted.
        /// </summary>
        public int Count
        {
            get
            {
                using (TimedLock.Lock(this))
                {
                    if (messages == null)
                        throw new ObjectDisposedException(this.GetType().Name);

                    return messages.Count;
                }
            }
        }

        /// <summary>
        /// Returns an <see cref="IEnumerator" /> over the set of <see cref="QueuedMsgInfo" /> records describing
        /// each message currently persisted in the backing store.
        /// </summary>
        /// <returns>An <see cref="IEnumerator" /> instances.</returns>
        IEnumerator<QueuedMsgInfo> IEnumerable<QueuedMsgInfo>.GetEnumerator()
        {
            using (TimedLock.Lock(this))
            {
                if (messages == null)
                    throw new ObjectDisposedException(this.GetType().Name);

                return messages.Values.GetEnumerator();
            }
        }

        /// <summary>
        /// Returns an <see cref="IEnumerator" /> over the set of <see cref="QueuedMsgInfo" /> records describing
        /// each message currently persisted in the backing store.
        /// </summary>
        /// <returns>An <see cref="IEnumerator" /> instances.</returns>
        System.Collections.IEnumerator System.Collections.IEnumerable.GetEnumerator()
        {
            using (TimedLock.Lock(this))
            {
                if (messages == null)
                    throw new ObjectDisposedException(this.GetType().Name);

                return messages.Values.GetEnumerator();
            }
        }

        /// <summary>
        /// Returns a non-<c>null</c> persist ID if the message exists in the backing
        /// store, <c>null</c> if it does not exist.
        /// </summary>
        /// <param name="ID">The message ID.</param>
        /// <returns>The persist ID of the object or <c>null</c>.</returns>
        public object GetPersistID(Guid ID)
        {
            using (TimedLock.Lock(this))
            {
                if (messages == null)
                    throw new ObjectDisposedException(this.GetType().Name);

                QueuedMsgInfo msgInfo;

                if (messages.TryGetValue(ID, out msgInfo))
                    return msgInfo.ID;

                return null;
            }
        }

        /// <summary>
        /// Adds a message to the backing store and updates the <see cref="QueuedMsgInfo.PersistID" />
        /// and <see cref="QueuedMsgInfo.ProviderData" /> fields in the <see cref="QueuedMsgInfo" />
        /// instance passed.
        /// </summary>
        /// <param name="msgInfo">The message metadata.</param>
        /// <param name="msg">The message.</param>
        public void Add(QueuedMsgInfo msgInfo, QueuedMsg msg)
        {
            using (TimedLock.Lock(this))
            {
                if (messages == null)
                    throw new ObjectDisposedException(this.GetType().Name);

                string msgPath;

                msgPath = GetMessagePath(msgInfo.ID, true);
                WriteMessage(msgPath, msg, msgInfo);

                msgInfo.PersistID    = msg.ID;
                msgInfo.ProviderData = msgPath;
                messages[msg.ID]     = msgInfo;
            }
        }

        /// <summary>
        /// Removes a message from the backing store if the message is present.
        /// </summary>
        /// <param name="persistID">The provider specific ID of the message being removed.</param>
        public void Remove(object persistID)
        {
            using (TimedLock.Lock(this))
            {
                if (messages == null)
                    throw new ObjectDisposedException(this.GetType().Name);

                Guid            msgID = (Guid)persistID;
                QueuedMsgInfo   msgInfo;
                string          msgPath;

                if (messages.TryGetValue(msgID, out msgInfo))
                {
                    msgPath = (string)msgInfo.ProviderData;
                    messages.Remove(msgID);
                }
                else
                    msgPath = GetMessagePath(msgID, false);

                if (File.Exists(msgPath))
                    File.Delete(msgPath);
            }
        }

        /// <summary>
        /// Loads a message from the backing store.
        /// </summary>
        /// <param name="persistID">The provider specific ID of the message being loaded.</param>
        /// <returns>The <see cref="QueuedMsg" /> or <c>null</c> if the message does not exist.</returns>
        public QueuedMsg Get(object persistID)
        {
            using (TimedLock.Lock(this))
            {
                if (messages == null)
                    throw new ObjectDisposedException(this.GetType().Name);

                Guid            msgID = (Guid)persistID;
                QueuedMsgInfo   msgInfo;

                if (messages.TryGetValue(msgID, out msgInfo))
                    return ReadMessage(msgID, (string)msgInfo.ProviderData);
                else
                    return null;
            }
        }

        /// <summary>
        /// Loads a message from the backing store.
        /// </summary>
        /// <param name="persistID">The provider specific ID of the message being loaded.</param>
        /// <returns>The <see cref="QueuedMsg" /> or <c>null</c> if the message does not exist.</returns>
        public QueuedMsgInfo GetInfo(object persistID)
        {
            using (TimedLock.Lock(this))
            {
                if (messages == null)
                    throw new ObjectDisposedException(this.GetType().Name);

                Guid            msgID = (Guid)persistID;
                QueuedMsgInfo   msgInfo;

                if (messages.TryGetValue(msgID, out msgInfo))
                    return msgInfo;
                else
                    return null;
            }
        }

        /// <summary>
        /// Updates delivery attempt related metadata for a message.
        /// </summary>
        /// <param name="persistID">The provider specific ID of the message.</param>
        /// <param name="deliveryAttempts">The number of delivery attempts.</param>
        /// <param name="deliveryTime">The delivery attempt time.</param>
        public void SetDeliveryAttempt(object persistID, int deliveryAttempts, DateTime deliveryTime)
        {
            using (TimedLock.Lock(this))
            {
                if (messages == null)
                    throw new ObjectDisposedException(this.GetType().Name);

                Guid            msgID = (Guid)persistID;
                QueuedMsgInfo   msgInfo;

                if (messages.TryGetValue(msgID, out msgInfo))
                {
                    msgInfo.DeliveryAttempts = deliveryAttempts;
                    msgInfo.DeliveryTime     = deliveryTime;

                    WriteMessageMetadata((string)msgInfo.ProviderData, msgInfo);
                }
            }
        }

        /// <summary>
        /// Updates a message's priority.
        /// </summary>
        /// <param name="persistID">The provider specific ID of the message.</param>
        /// <param name="priority">The new priority value.</param>
        public void SetPriority(object persistID, DeliveryPriority priority)
        {
            using (TimedLock.Lock(this))
            {
                if (messages == null)
                    throw new ObjectDisposedException(this.GetType().Name);

                Guid            msgID = (Guid)persistID;
                QueuedMsgInfo   msgInfo;

                if (messages.TryGetValue(msgID, out msgInfo))
                {
                    msgInfo.Priority = priority;
                    WriteMessageMetadata((string)msgInfo.ProviderData, msgInfo);
                }
            }
        }

        /// <summary>
        /// Updates a message's target endpoint and status.  This is typically used
        /// for moving an expired message to a dead letter queue.
        /// </summary>
        /// <param name="persistID">The provider specific ID of the message.</param>
        /// <param name="targetEP">The new message target endpoint.</param>
        /// <param name="deliveryTime">The new message delivery time.</param>
        /// <param name="expireTime">The new message expiration time.</param>
        /// <param name="status">The new <see cref="DeliveryStatus" />.</param>
        public void Modify(object persistID, MsgEP targetEP, DateTime deliveryTime, DateTime expireTime, DeliveryStatus status)
        {
            using (TimedLock.Lock(this))
            {
                if (messages == null)
                    throw new ObjectDisposedException(this.GetType().Name);

                Guid            msgID = (Guid)persistID;
                QueuedMsgInfo   msgInfo;

                if (messages.TryGetValue(msgID, out msgInfo))
                {
                    msgInfo.TargetEP     = targetEP;
                    msgInfo.DeliveryTime = deliveryTime;
                    msgInfo.ExpireTime   = expireTime;
                    msgInfo.Status       = status;

                    WriteMessageMetadata((string)msgInfo.ProviderData, msgInfo);
                }
            }
        }

        /// <summary>
        /// Loads the message index file.  If the index does not exist or is corrupt then
        /// the index will be rebuilt by performing a full scan of message files under
        /// the root folder.
        /// </summary>
        /// <param name="newIndexFile">Pass as <c>true</c> if a new index file is being created.</param>
        private void LoadIndex(bool newIndexFile)
        {
            string                      formatErr = string.Format("Invalid or missing index file [{0}].", indexPath);
            int                         cMessages;
            string[]                    files;
            Dictionary<Guid, string>    msgFiles;

            TimedLock.AssertLocked(this);
            messages = new Dictionary<Guid, QueuedMsgInfo>();

            // List the message files and build a hash table mapping each message GUID 
            // the fully qualified path to the file.

            files    = Helper.GetFilesByPattern(root + "*.msg", SearchOption.AllDirectories);
            msgFiles = new Dictionary<Guid, string>(files.Length);

            foreach (string path in files)
            {
                string  file = Path.GetFileName(path).ToLowerInvariant();
                Guid    msgID;

                if (!file.EndsWith(".msg"))
                    continue;

                try
                {
                    msgID = new Guid(file.Substring(0, file.Length - 4));
                }
                catch
                {
                    continue;
                }

                msgFiles[msgID] = path;
            }

            // Load the index file.

            try
            {
                // Parse the index file header

                fsIndex.Position = 0;
                if (fsIndex.ReadInt32() != IndexMagic)      // Magic Number
                    throw new FormatException(formatErr);

                if (fsIndex.ReadInt32() != 0)               // Format Version
                    throw new FormatException(formatErr);

                fsIndex.ReadInt32();                        // Reserved

                if (fsIndex.ReadInt32() != 0)
                    throw new FormatException(string.Format("Index file [{0}] was not closed properly. Full message folder scan will be performed.", indexPath));

                cMessages = fsIndex.ReadInt32();            // Message Count

                // Parse the message metadata

                for (int i = 0; i < cMessages; i++)
                {
                    QueuedMsgInfo msgInfo;

                    msgInfo              = new QueuedMsgInfo(fsIndex.ReadString16());
                    msgInfo.PersistID    = msgInfo.ID;
                    msgInfo.ProviderData = root + fsIndex.ReadString16();   // Make the paths absolute

                    messages[msgInfo.ID] = msgInfo;
                }

                // Perform an extra consistency check by listing all of the message files
                // under the root folder and comparing the message GUIDs encoded into the
                // file names with the GUIDs loaded from the index file and then bringing
                // the index into sync with the actual message files.

                bool updated = false;
                int cLoaded  = 0;

                // Delete any metadata for messages that exist in the index
                // but don't exist on the file system.

                var delList = new List<Guid>();

                foreach (Guid msgID in messages.Keys)
                    if (!msgFiles.ContainsKey(msgID))
                        delList.Add(msgID);

                foreach (Guid msgID in delList)
                    messages.Remove(msgID);

                if (delList.Count > 0)
                {
                    updated = true;
                    SysLog.LogWarning(string.Format("Message index [{0}] has message metadata for messages that do not exist. [{1}] messages will be removed from the index.", indexPath, delList.Count));
                }

                // Load metadata for messages that exist in the file system
                // but were not in the index.

                foreach (Guid msgID in msgFiles.Keys)
                    if (!messages.ContainsKey(msgID))
                    {
                        string path = msgFiles[msgID];

                        try
                        {
                            messages[msgID] = ReadMessageMetadata(msgID, path);
                            cLoaded++;
                        }
                        catch (Exception e)
                        {
                            SysLog.LogException(e);
                        }
                    }

                if (cLoaded > 0)
                {
                    updated = true;
                    SysLog.LogWarning(string.Format("Message index [{0}] is missing metadata for [{1}] messages. Missing entries will be added.", indexPath, cLoaded));
                }

                if (updated)
                    SaveIndex(true);

                // Mark the index as "open" for crash detection.

                fsIndex.Position = OpenFlagOffset;
                fsIndex.WriteInt32(1);
                fsIndex.Flush();
            }
            catch
            {
                if (newIndexFile)
                    SysLog.LogWarning("Rebuilding missing message index file [{0}].", indexPath);
                else
                    SysLog.LogWarning("Rebuilding corrupt message index file [{0}].", indexPath);

                // Clear the index file if there was a serious error and then
                // rebuild it from scratch by scanning the message metadata.

                fsIndex.SetLength(0);
                fsIndex.Flush();
                messages.Clear();

                foreach (Guid msgID in msgFiles.Keys)
                {
                    try
                    {
                        messages.Add(msgID, ReadMessageMetadata(msgID, msgFiles[msgID]));
                    }
                    catch (Exception e2)
                    {
                        SysLog.LogException(e2);
                    }
                }

                // Save the index, marking the file as "open" for crash detection

                SaveIndex(true);
            }
        }

        /// <summary>
        /// Write current message metadata state to the index file.
        /// </summary>
        /// <param name="open">Indicates whether the file should be marked as OPEN or CLOSED.</param>
        private void SaveIndex(bool open)
        {
            fsIndex.Position = 0;
            fsIndex.SetLength(0);

            // Write the header

            fsIndex.WriteInt32(IndexMagic);     // Magic Number
            fsIndex.WriteInt32(0);              // Format Version
            fsIndex.WriteInt32(0);              // Reserved
            fsIndex.WriteInt32(0);              // Open Flag
            fsIndex.WriteInt32(messages.Count); // Message Count

            // Write the metadata.

            foreach (QueuedMsgInfo msgInfo in messages.Values)
            {
                fsIndex.WriteString16(msgInfo.ToString());
                fsIndex.WriteString16(((string)msgInfo.ProviderData).Substring(root.Length));  // Make the saved path relative
            }

            fsIndex.Position = OpenFlagOffset;
            fsIndex.WriteInt32(open ? 1 : 0);
            fsIndex.Flush();
        }

        /// <summary>
        /// Returns the fully qualified message file path based on the GUID.
        /// </summary>
        /// <param name="msgID">The message ID.</param>
        /// <param name="createFolders">Pass as <c>true</c> to ensure that the folder tree above the path returned exists.</param>
        /// <returns>The message file path.</returns>
        internal string GetMessagePath(Guid msgID, bool createFolders)
        {
            string path = string.Format("{0}{1:0##}\\{2}.msg", root, Helper.HashToIndex(FolderCount, msgID.GetHashCode()), msgID.ToString("D"));

            if (createFolders)
                Helper.CreateFileTree(path);

            return path;
        }

        /// <summary>
        /// Updates a message file's metadata.
        /// </summary>
        /// <param name="path">The file path.</param>
        /// <param name="msgInfo">The message metadata.</param>
        internal void WriteMessageMetadata(string path, QueuedMsgInfo msgInfo)
        {
            using (var fsMsg = new EnhancedFileStream(path, FileMode.Open, FileAccess.ReadWrite))
            {
                // Seek past the message's header and body.

                int     cbBody;
                byte[]  md5Hash;

                fsMsg.Position = MsgHeaderSize;
                cbBody         = fsMsg.ReadInt32();
                fsMsg.Position = fsMsg.Position + cbBody;

                // Write the metadata and truncate the file.

                fsMsg.WriteString16(msgInfo.ToString());
                fsMsg.SetLength(fsMsg.Position);

                // Regenerate the MD5 Hash

                fsMsg.Position = MsgMD5Offset + MD5Hasher.DigestSize;
                md5Hash        = MD5Hasher.Compute(fsMsg, fsMsg.Length - fsMsg.Position);
                fsMsg.Position = MsgMD5Offset;
                fsMsg.WriteBytesNoLen(md5Hash);
            }
        }

        /// <summary>
        /// Writes a message file.
        /// </summary>
        /// <param name="path">The file path.</param>
        /// <param name="msg">The message.</param>
        /// <param name="msgInfo">The message metadata.</param>
        internal void WriteMessage(string path, QueuedMsg msg, QueuedMsgInfo msgInfo)
        {
            byte[] md5Hash;

            using (var fsMsg = new EnhancedFileStream(path, FileMode.Create, FileAccess.ReadWrite))
            {
                // Write the file header

                fsMsg.WriteInt32(MsgMagic);         // Magic Number
                fsMsg.WriteInt32(0);                // Format Version
                fsMsg.WriteInt32(0);                // Reserved
                fsMsg.WriteBytesNoLen(md5Zeros);    // MD5 hash placeholder

                // Write the message body.

                fsMsg.WriteBytes32(msg.BodyRaw);

                // Write the metadata.

                fsMsg.WriteString16(msgInfo.ToString());

                // Compute and save the MD5 hash

                fsMsg.Position = MsgHeaderSize;
                md5Hash        = MD5Hasher.Compute(fsMsg, fsMsg.Length - fsMsg.Position);
                fsMsg.Position = MsgMD5Offset;
                fsMsg.WriteBytesNoLen(md5Hash);
            }
        }

        /// <summary>
        /// Reads the metadata from a message file.
        /// </summary>
        /// <param name="msgID">The message ID.</param>
        /// <param name="path">Fully qualified path to the message file.</param>
        /// <returns>The <see cref="QueuedMsgInfo" />.</returns>
        internal QueuedMsgInfo ReadMessageMetadata(Guid msgID, string path)
        {
            QueuedMsgInfo   msgInfo;
            int             cbBody;
            byte[]          md5Hash;
            long            savePos;

            using (var fsMsg = new EnhancedFileStream(path, FileMode.Open, FileAccess.ReadWrite))
            {
                try
                {
                    // Read the message file header

                    if (fsMsg.ReadInt32() != MsgMagic)      // Magic Number
                        throw new Exception();

                    if (fsMsg.ReadInt32() != 0)             // Format Version
                        throw new Exception();

                    fsMsg.ReadInt32();                      // Reserved

                    // Verify that the MD5 hash saved in then file matches the
                    // hash computed for the remainder of the file as it exists
                    // right now.

                    md5Hash = fsMsg.ReadBytes(MD5Hasher.DigestSize);
                    savePos = fsMsg.Position;

                    if (!Helper.ArrayEquals(md5Hash, MD5Hasher.Compute(fsMsg, fsMsg.Length - fsMsg.Position)))
                        throw new FormatException(string.Format("Message file [{0}] is corrupt. MD5 digests do not match.", path));

                    fsMsg.Position = savePos;

                    // Skip over the message body data

                    cbBody = fsMsg.ReadInt32();
                    fsMsg.Position = fsMsg.Position + cbBody;

                    // Read the metadata and add the provider specific information

                    msgInfo              = new QueuedMsgInfo(fsMsg.ReadString16());
                    msgInfo.PersistID    = msgInfo.ID;
                    msgInfo.ProviderData = path;

                    return msgInfo;
                }
                catch
                {
                    throw new FormatException(string.Format("Bad message file [{0}].", path));
                }
            }
        }

        /// <summary>
        /// Reads a message from a message file.
        /// </summary>
        /// <param name="msgID">The message ID.</param>
        /// <param name="path">The file path.</param>
        /// <returns>A <see cref="QueuedMsg" />.</returns>
        internal QueuedMsg ReadMessage(Guid msgID, string path)
        {
            QueuedMsgInfo   msgInfo;
            byte[]          body;
            byte[]          md5Hash;
            long            savePos;

            using (var fsMsg = new EnhancedFileStream(path, FileMode.Open, FileAccess.ReadWrite))
            {
                try
                {
                    // Read the message file header

                    if (fsMsg.ReadInt32() != MsgMagic)          // Magic Number
                        throw new Exception();

                    if (fsMsg.ReadInt32() != 0)                 // Format Version
                        throw new Exception();

                    fsMsg.ReadInt32();                          // Reserved

                    // Verify that the MD5 hash saved in then file matches the
                    // hash computed for the remainder of the file as it exists
                    // right now.

                    md5Hash = fsMsg.ReadBytes(MD5Hasher.DigestSize);
                    savePos = fsMsg.Position;

                    if (!Helper.ArrayEquals(md5Hash, MD5Hasher.Compute(fsMsg, fsMsg.Length - fsMsg.Position)))
                        throw new FormatException(string.Format("Message file [{0}] is corrupt. MD5 digests do not match.", path));

                    fsMsg.Position = savePos;

                    // Read the message body

                    body = fsMsg.ReadBytes32();

                    // Read the metadata and add the provider specific information

                    msgInfo              = new QueuedMsgInfo(fsMsg.ReadString16());
                    msgInfo.PersistID    = msgInfo.ID;
                    msgInfo.ProviderData = path;

                    if (msgID != msgInfo.ID)
                        throw new FormatException("Message ID does not match the metadata.");

                    return new QueuedMsg(msgInfo, body, false);
                }
                catch
                {
                    throw new FormatException(string.Format("Bad message file [{0}].", path));
                }
            }
        }

        //---------------------------------------------------------------------
        // ILockable implementation

        private object lockKey = TimedLock.AllocLockKey();

        /// <summary>
        /// Used by <see cref="TimedLock" /> to provide better deadlock
        /// diagnostic information.
        /// </summary>
        /// <returns>The process unique lock key for this instance.</returns>
        public object GetLockKey()
        {
            return lockKey;
        }
    }
}
