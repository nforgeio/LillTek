//-----------------------------------------------------------------------------
// FILE:        FileTransactionLog.cs
// CONTRIBUTOR: Jeff Lill
// COPYRIGHT:   Copyright (c) 2005-2015 by Jeffrey Lill.  All rights reserved.
// DESCRIPTION: A file-based implementation of ITransactionLog.

using System;
using System.IO;
using System.Collections.Generic;

using LillTek.Common;
using LillTek.Advanced;

namespace LillTek.Transactions
{
    /// <summary>
    /// A file-based implementation of <see cref="ITransactionLog" />.
    /// </summary>
    /// <threadsafety instance="true" />
    /// <remarks>
    /// <para>
    /// The <see cref="FileTransactionLog" /> is pretty simple.  The log is simply a
    /// set of files within a folder, one file per base transaction, corresponding
    /// directly to the <see cref="IOperationLog" />s maintained by the transaction log.  
    /// Each transaction log file is named using the transaction ID and the ".log"
    /// extension.  In addition to the log files, a file named "transactions.lock" file will
    /// be present.  This file doesn't hold any real data at this point and is 
    /// opened and locked by the transaction lock to prevent another transaction log
    /// instance from acquiring control over the log folder.
    /// </para>
    /// <para>
    /// The log file format consists of a small header followed by zero or more
    /// serialized operation records.  All integers are encoded using big-endian 
    /// byte ordering.
    /// </para>
    /// <code language="none">
    ///        Header
    /// +------------------+
    /// |   Magic Number   |    32-bits:  0x214A08A6
    /// +------------------+
    /// |  Format Version  |    32-bits:  0
    /// +------------------+
    /// |     Reserved     |    32-bits:  0
    /// +------------------+
    /// |       Type       |    32-bits:  0=UNDO 1=REDO
    /// +------------------+
    /// |                  |
    /// |                  |
    /// |                  |
    /// |   Transaction    |    16-bytes: The transaction GUID
    /// |       GUID       |
    /// |                  |
    /// |                  |
    /// |                  |
    /// +------------------+
    /// 
    ///     Operation 0
    /// +------------------+
    /// |   Magic Number   |    32-bits:  0x214A08A6
    /// +------------------+
    /// |      Length      |    32-bits:  Byte count of operation data to follow
    /// +------------------+
    /// | Description Len  |    32-bits:  Description byte length or -1 if null
    /// +------------------+
    /// |                  |
    /// |                  |
    /// |                  |
    /// |      UTF-8       |
    /// |     Encoded      |
    /// |   Description    |
    /// |                  |
    /// |                  |
    /// |                  |
    /// |                  |
    /// +------------------+
    /// |                  |
    /// |                  |
    /// |                  |
    /// |    Serialized    |
    /// |    Operation     |
    /// |                  |
    /// |                  |
    /// |                  |
    /// +------------------+
    /// </code>
    /// <para>
    /// The transaction operations are serialized to the stream in the order
    /// they were originally performed on the resource.  Each operation
    /// written includes a magic number, the human readable description of
    /// the operation returned by <see cref="IOperation.Description" /> 
    /// (or <c>null</c>) followed by the serialized operation data as written
    /// by <see cref="ITransactedResource.WriteOperation" />.
    /// </para>
    /// <para>
    /// The <see cref="FileTransactionLog" /> constructor accepts the path to the
    /// file system folder to be used by the log.  When <see cref="Open" />
    /// is called this folder is created if it doesn't already exist and
    /// the folder is scanned for transaction log files.  If there are no
    /// log files present then the transacted resource is up-to-date and
    /// is ready to accept new transactions.  The presence of log files
    /// indicates that the process or system was stopped before all changes
    /// to the resource were committed.
    /// </para>
    /// <para>
    /// <see cref="Open" /> returns immediately with <see cref="LogStatus.Ready" /> if 
    /// there are no files transaction files found.  If log files are present,
    /// <see cref="Open" /> scans each of them to determine that all of the
    /// files are intact or whether one or more of them are corrupt.  <see cref="Open" />
    /// will return either a <see cref="LogStatus.Recover" /> or <see cref="LogStatus.Corrupt" />
    /// status code.
    /// </para>
    /// <para>
    /// If the <see cref="TransactionManager" /> decides to continue with a
    /// transaction recovery effort after <see cref="Open" /> returns a 
    /// non-<see cref="LogStatus.Ready" /> status, it will call <see cref="GetOrphanTransactions" />
    /// which will scan the log files again, adding the IDs of the good files
    /// to the list returned and deleting any corrupt files.  The <see cref="TransactionManager" />
    /// will then open each transaction log and redo or undo the operations
    /// found within against the resource.
    /// </para>
    /// </remarks>
    public sealed class FileTransactionLog : ITransactionLog
    {
        private const string NotOpenMsg = "Transaction log is not open.";

        private string      folder;         // The folder path (without a trailing slash)
        private FileStream  lockFile;       // The transaction folder lock file
        private bool        isOpen;         // True if the log is open
        private object      syncLock;       // Thread synchronization object

        /// <summary>
        /// Constructor.
        /// </summary>
        /// <param name="folder">The path to the transaction log folder.</param>
        public FileTransactionLog(string folder)
        {
            this.folder   = Path.GetFullPath(Helper.StripTrailingSlash(folder));
            this.lockFile = null;
        }

        /// <summary>
        /// Opens the transaction log, returning a <see cref="LogStatus" />
        /// value indicating the state of the transaction log.
        /// </summary>
        /// <param name="manager">The <see cref="TransactionManager" />.</param>
        /// <returns>The log's <see cref="LogStatus" /> code.</returns>
        /// <remarks>
        /// <para>
        /// This method will return <see cref="LogStatus.Ready" /> if the log is ready to
        /// begin handling transactions.
        /// </para>
        /// <para>
        /// <see cref="LogStatus.Recover" /> will be returned if the log was not 
        /// closed properly (probably due to a system or application failure) and 
        /// there are transactions that need to be recovered.  The <see cref="TransactionManager" />
        /// will call <see cref="GetOrphanTransactions" /> to get the <see cref="Guid" />s of
        /// the orphaned transactions and will then call <see cref="OpenOperationLog" />
        /// to open each transaction operation log and then undo or redo the transaction
        /// operations depending on the state of the operation log.
        /// </para>
        /// <para>
        /// The method returns <see cref="LogStatus.Corrupt" /> if the transaction
        /// log is corrupt and that there's the potential for the resource to 
        /// be in an inconsistent state after recovering the remaining transactions.
        /// </para>
        /// </remarks>
        /// <exception cref="TransactionException">Thrown if the log is already open.</exception>
        public LogStatus Open(TransactionManager manager)
        {
            bool orphans;
            Guid id;

            try
            {
                if (isOpen)
                    throw new TransactionException("Transaction log is already open.");

                Helper.CreateFolderTree(folder);
                lockFile = new FileStream(folder + Helper.PathSepString + "transactions.lock", FileMode.Create, FileAccess.ReadWrite);

                orphans = false;
                foreach (var file in Helper.GetFilesByPattern(folder + Helper.PathSepString + "*.log", SearchOption.AllDirectories))
                {

                    orphans = true;
                    if (!FileOperationLog.Validate(file, out id))
                        return LogStatus.Corrupt;
                }

                isOpen = true;
                syncLock = manager.SyncRoot;

                return orphans ? LogStatus.Recover : LogStatus.Ready;
            }
            catch
            {
                if (lockFile != null)
                {
                    lockFile.Close();
                    lockFile = null;
                }

                throw;
            }
        }

        /// <summary>
        /// Closes the transaction log if it is open.
        /// </summary>
        public void Close()
        {
            Close(false);
        }

        /// <summary>
        /// Closes the transaction log if it is open, optionally simulating a system crash.
        /// </summary>
        /// <param name="simulateCrash">
        /// Pass as <c>true</c> to simulate a system crash by leaving the transaction
        /// log state as is.
        /// </param>
        /// <remarks>
        /// <note>
        /// Implementations may choose to ignore the <paramref name="simulateCrash" />
        /// parameter is this is used only for UNIT testing.
        /// </note>
        /// </remarks>
        public void Close(bool simulateCrash)
        {
            isOpen = false;

            if (lockFile != null)
            {
                lockFile.Close();
                lockFile = null;
            }
        }

        /// <summary>
        /// Returns the orphaned transaction <see cref="Guid" />s discovered after
        /// <see cref="Open" /> returns <see cref="LogStatus.Recover" />.
        /// </summary>
        /// <returns>The list of transaction <see cref="Guid" />s.</returns>
        /// <exception cref="TransactionException">Thrown if the log is not open.</exception>
        /// <remarks>
        /// This method returns only the IDs for valid, non-corrupted transaction
        /// operation logs.  It will delete any corrupted logs it finds.
        /// </remarks>
        public List<Guid> GetOrphanTransactions()
        {
            var     orphans = new List<Guid>();
            Guid    id;

            using (TimedLock.Lock(syncLock))
            {
                if (!isOpen)
                    throw new TransactionException(NotOpenMsg);

                foreach (string file in Helper.GetFilesByPattern(folder + Helper.PathSepString + "*.log", SearchOption.AllDirectories))
                {
                    if (FileOperationLog.Validate(file, out id))
                        orphans.Add(id);
                    else
                    {
                        try
                        {
                            File.Delete(file);
                        }
                        catch
                        {
                            // Ignore errors
                        }
                    }
                }

                return orphans;
            }
        }

        /// <summary>
        /// Opens an existing <see cref="IOperationLog" />.
        /// </summary>
        /// <param name="transactionID">The operation's base transaction <see cref="Guid" />.</param>
        /// <returns>The <see cref="IOperationLog" /> instance.</returns>
        /// <exception cref="TransactionException">Thrown if the log is not open.</exception>
        public IOperationLog OpenOperationLog(Guid transactionID)
        {
            using (TimedLock.Lock(syncLock))
            {
                if (!isOpen)
                    throw new TransactionException(NotOpenMsg);

                return new FileOperationLog(folder + Helper.PathSepString + transactionID.ToString() + ".log", transactionID);
            }
        }

        /// <summary>
        /// Closes an <see cref="IOperationLog" />.
        /// </summary>
        /// <param name="operationLog">The <see cref="IOperationLog" />.</param>
        /// <exception cref="TransactionException">Thrown if the log is not open.</exception>
        public void CloseOperationLog(IOperationLog operationLog)
        {
            using (TimedLock.Lock(syncLock))
            {
                if (!isOpen)
                    throw new TransactionException(NotOpenMsg);

                ((FileOperationLog)operationLog).Close();
            }
        }

        /// <summary>
        /// Creates an <see cref="IOperationLog" /> in <see cref="OperationLogMode.Undo" /> mode.
        /// </summary>
        /// <param name="transactionID">The base transaction <see cref="Guid" />.</param>
        /// <returns>The <see cref="IOperationLog" /> instance.</returns>
        /// <remarks>
        /// This is called when a transaction is first created and operations need
        /// to be persisted to an undo log so that they can be undone if the process
        /// crashes before all of the operations have been persisted.
        /// </remarks>
        /// <exception cref="TransactionException">Thrown if the log is not open.</exception>
        public IOperationLog CreateOperationLog(Guid transactionID)
        {
            using (TimedLock.Lock(syncLock))
            {
                if (!isOpen)
                    throw new TransactionException(NotOpenMsg);

                return new FileOperationLog(folder + Helper.PathSepString + transactionID.ToString() + ".log", transactionID);
            }
        }

        /// <summary>
        /// Sets an <see cref="IOperationLog" />'s mode to <see cref="OperationLogMode.Redo" />
        /// and then closes the log.
        /// </summary>
        /// <param name="operationLog">The <see cref="IOperationLog" />.</param>
        /// <remarks>
        /// This is called when the base transaction is committed and all of the
        /// operations that compose the transaction have been persisted to the
        /// log.  The method sets the operation log mode to <see cref="OperationLogMode.Redo" />
        /// so that the operations will be reapplied after a process crash.
        /// </remarks>
        /// <exception cref="TransactionException">Thrown if the log is not open.</exception>
        public void CommitOperationLog(IOperationLog operationLog)
        {
            using (TimedLock.Lock(syncLock))
            {
                if (!isOpen)
                    throw new TransactionException(NotOpenMsg);

                var fileLog = (FileOperationLog)operationLog;

                fileLog.Mode = OperationLogMode.Redo;
                fileLog.Close();
            }
        }

        /// <summary>
        /// Closes and deletes an <see cref="IOperationLog" />.
        /// </summary>
        /// <param name="operationLog">The <see cref="IOperationLog" />.</param>
        /// <remarks>
        /// This is called after the all of the transactions have been applied to
        /// the underlying resource and the log is no longer necessary.
        /// </remarks>
        /// <exception cref="TransactionException">Thrown if the log is not open.</exception>
        public void RemoveOperationLog(IOperationLog operationLog)
        {
            using (TimedLock.Lock(syncLock))
            {
                if (!isOpen)
                    throw new TransactionException(NotOpenMsg);

                var fileLog = (FileOperationLog)operationLog;
                var path = fileLog.FullPath;

                fileLog.Close();
                File.Delete(path);
            }
        }
    }
}
