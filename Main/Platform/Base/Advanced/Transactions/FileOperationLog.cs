//-----------------------------------------------------------------------------
// FILE:        FileOperationLog.cs
// CONTRIBUTOR: Jeff Lill
// COPYRIGHT:   Copyright (c) 2005-2015 by Jeffrey Lill.  All rights reserved.
// DESCRIPTION: The IOperationLog implementation for file-based transactions.

using System;
using System.IO;
using System.Collections.Generic;

using LillTek.Common;
using LillTek.Advanced;

namespace LillTek.Transactions
{
    /// <summary>
    /// The <see cref="IOperationLog" /> implementation for file-based transactions.
    /// </summary>
    /// <remarks>
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
    /// </remarks>
    internal sealed class FileOperationLog : IOperationLog, ILockable
    {
        //---------------------------------------------------------------------
        // Static members

        /// <summary>
        /// Verifies that a log file is not corrupt.
        /// </summary>
        /// <param name="path">The path to the log file.</param>
        /// <param name="transactionID">Returns as the file's transaction <see cref="Guid" />.</param>
        /// <returns><c>true</c> if the log file is valid, <c>false</c> if it is corrupt.</returns>
        public static bool Validate(string path, out Guid transactionID)
        {
            int cb;

            try
            {
                using (var file = new EnhancedFileStream(path, FileMode.Open, FileAccess.ReadWrite))
                {
                    if (file.ReadInt32() != Magic ||    // Magic number
                        file.ReadInt32() != 0)
                    {        // Format Versopn
                        throw new Exception();
                    }

                    switch (file.ReadInt32())           // Mode
                    {         
                        case (int)OperationLogMode.Undo:
                        case (int)OperationLogMode.Redo:

                            break;

                        default:

                            throw new Exception();
                    }

                    file.ReadInt32();                   // Reserved
                    transactionID = new Guid(file.ReadBytes(16));

                    while (!file.Eof)
                    {
                        if (file.ReadInt32() != Magic)
                            throw new Exception();

                        cb = file.ReadInt32();
                        if (cb < 0 || cb + file.Position > file.Length)
                            throw new Exception();

                        file.Position += cb;
                    }

                    return true;
                }
            }
            catch
            {
                transactionID = Guid.Empty;
                return false;
            }

        }

        //---------------------------------------------------------------------
        // Instance members

        private const string    ClosedMsg  = "Operation log is closed.";

        private const int       Magic      = 0x214A08A6;        // Magic number
        private const int       ModeOffset = 3 * 4;             // Byte offset of the MODE field in the file header
        private const int       HeaderSize = 4 * 4 + 16;        // Size of the file header

        private EnhancedFileStream  file;                       // The log file or null if closed
        private string              path;                       // Fully qualified path to the log file
        private Guid                transactionID;              // The transaction ID
        private OperationLogMode    mode;                       // The current mode

        /// <summary>
        /// Opens or creates a file operation log file.
        /// </summary>
        /// <param name="path">The path to the log file.</param>
        /// <param name="transactionID">The log's transaction <see cref="Guid" />.</param>
        /// <remarks>
        /// New logs will be created in <see cref="OperationLogMode.Undo" /> mode.
        /// </remarks>
        public FileOperationLog(string path, Guid transactionID)
        {
            this.path = Path.GetFullPath(path);
            this.file = new EnhancedFileStream(path, FileMode.OpenOrCreate, FileAccess.ReadWrite);

            if (file.Length == 0)
            {
                // Initialize a new file

                file.WriteInt32(Magic);
                file.WriteInt32(0);
                file.WriteInt32(0);
                file.WriteInt32((int)OperationLogMode.Undo);
                file.WriteBytesNoLen(transactionID.ToByteArray());
                file.Flush();
            }
            else
            {
                // Open an existing file.

                try
                {
                    if (file.ReadInt32() != Magic ||    // Magic number
                        file.ReadInt32() != 0)
                    {        // Format Versopn
                        throw new Exception();
                    }

                    file.ReadInt32();                   // Reserved

                    switch (file.ReadInt32())           // Mode
                    {
                        case (int)OperationLogMode.Undo:

                            mode = OperationLogMode.Undo;
                            break;

                        case (int)OperationLogMode.Redo:

                            mode = OperationLogMode.Redo;
                            break;

                        default:

                            throw new Exception();
                    }

                    this.transactionID = new Guid(file.ReadBytes(16));
                    if (transactionID != this.transactionID)
                        throw new Exception();
                }
                catch
                {
                    throw new TransactionException(CorruptMsg);
                }
            }
        }

        /// <summary>
        /// Returns a "log is corrupt" message.
        /// </summary>
        private string CorruptMsg
        {
            get { return string.Format("Transaction log [{0}] is corrupt.", path); }
        }

        /// <summary>
        /// Closes the log file if it's open.
        /// </summary>
        public void Close()
        {
            using (TimedLock.Lock(this))
            {
                if (file != null)
                {
                    file.Close();
                    file = null;
                }
            }
        }

        /// <summary>
        /// Returns the fully qualified path to the log file.
        /// </summary>
        public string FullPath
        {
            get { return path; }
        }

        /// <summary>
        /// The <see cref="Guid" /> for the base transaction associated with this log.
        /// </summary>
        public Guid TransactionID
        {
            get { return transactionID; }
        }

        /// <summary>
        /// Returns the <see cref="OperationLogMode" /> indicating whether the <see cref="IOperationLog" /> 
        /// is an <see cref="OperationLogMode.Undo" /> or <see cref="OperationLogMode.Redo" /> log.
        /// </summary>
        public OperationLogMode Mode
        {
            get { return mode; }

            internal set
            {
                using (TimedLock.Lock(this))
                {
                    if (file == null)
                        throw new TransactionException(ClosedMsg);

                    mode = value;
                    file.Position = ModeOffset;
                    file.WriteInt32((int)value);
                    file.Flush();
                }
            }
        }

        /// <summary>
        /// Returns the position of the next operation to be written to the log.
        /// </summary>
        /// <remarks>
        /// <note>
        /// This property can only be called if the operation log is in <see cref="OperationLogMode.Undo" />
        /// mode.
        /// </note>
        /// </remarks>
        /// <exception cref="TransactionException">Thrown if the log isn't open or if the mode isn't <see cref="OperationLogMode.Undo" />.</exception>
        public ILogPosition Position
        {
            get
            {
                using (TimedLock.Lock(this))
                {
                    if (file == null)
                        throw new TransactionException(ClosedMsg);

                    if (mode != OperationLogMode.Undo)
                        throw new TransactionException("Write is available only when the log is in UNDO mode.");

                    return new FileLogPosition(file.Length);
                }
            }
        }

        /// <summary>
        /// Truncates the log to the position passed.
        /// </summary>
        /// <param name="position">The <see cref="ILogPosition" /> defining where the truncation should occur.</param>
        /// <remarks>
        /// <note>
        /// This property can only be called if the operation log is in <see cref="OperationLogMode.Undo" />
        /// mode.
        /// </note>
        /// <para>
        /// This method is used in combination with the <see cref="Position" /> property to roll
        /// back operations within a base transaction.
        /// </para>
        /// </remarks>
        /// <exception cref="TransactionException">Thrown if the log isn't open or if the mode isn't <see cref="OperationLogMode.Undo" />.</exception>
        public void Truncate(ILogPosition position)
        {
            using (TimedLock.Lock(this))
            {
                if (file == null)
                    throw new TransactionException(ClosedMsg);

                if (mode != OperationLogMode.Undo)
                    throw new TransactionException("Write is available only when the log is in UNDO mode.");

                file.SetLength(((FileLogPosition)position).Position);
            }
        }

        /// <summary>
        /// Returns the list of the <see cref="ILogPosition" />s with each operation in the log.
        /// </summary>
        /// <param name="reverse">Pass <c>true</c> to return the positions in the reverse order that they were appended to the log.</param>
        /// <returns>The operation position list.</returns>
        /// <exception cref="TransactionException">Thrown if the log is not open or is corrupt.</exception>
        public List<ILogPosition> GetPositions(bool reverse)
        {
            var     list = new List<ILogPosition>();
            long    length;
            int     cb;

            using (TimedLock.Lock(this))
            {
                if (file == null)
                    throw new TransactionException(ClosedMsg);

                length = file.Length;
                file.Position = HeaderSize;

                while (!file.Eof)
                {
                    list.Add(new FileLogPosition(file.Position));

                    if (file.ReadInt32() != Magic)
                        throw new TransactionException(CorruptMsg);

                    cb = file.ReadInt32();
                    if (cb < 0 || cb + file.Position > length)
                        throw new TransactionException(CorruptMsg);

                    file.Position += cb;
                }
            }

            if (reverse)
                list.Reverse();

            return list;
        }

        /// <summary>
        /// Returns the set of <see cref="ILogPosition" /> for each operation from the end of the
        /// log to the <paramref name="position" /> passed, in the reverse order that the operations
        /// were added to the log.
        /// </summary>
        /// <param name="position">The limit position.</param>
        /// <returns>The operation position list.</returns>
        /// <exception cref="ArgumentException">Thrown if the position passed is not valid.</exception>
        /// <exception cref="TransactionException">Thrown if the log is not open or is corrupt.</exception>
        public List<ILogPosition> GetPositionsTo(ILogPosition position)
        {
            var     list = new List<ILogPosition>();
            var     pos = (FileLogPosition)position;
            long    length;
            int     cb;

            using (TimedLock.Lock(this))
            {
                if (file == null)
                    throw new TransactionException(ClosedMsg);

                if (pos.Position < HeaderSize || pos.Position > file.Length)
                    throw new ArgumentException("Invalid log position.", "position");

                length        = file.Length;
                file.Position = pos.Position;

                while (!file.Eof)
                {
                    list.Add(new FileLogPosition(file.Position));

                    if (file.ReadInt32() != Magic)
                        throw new TransactionException(CorruptMsg);

                    cb = file.ReadInt32();
                    if (cb < 0 || cb + file.Position > length)
                        throw new TransactionException(CorruptMsg);

                    file.Position += cb;
                }
            }

            list.Reverse();
            return list;
        }

        /// <summary>
        /// Reads the operation from the specified position in the log.
        /// </summary>
        /// <param name="resource">The parent <see cref="ITransactedResource" /> responsible for deserializing the operation.</param>
        /// <param name="position">See the <see cref="ILogPosition" />.</param>
        /// <returns>The <see cref="IOperation" /> read from the log.</returns>
        public IOperation Read(ITransactedResource resource, ILogPosition position)
        {

            int         cb;
            long        recEndPos;
            string      description;
            IOperation  operation;

            using (TimedLock.Lock(this))
            {
                if (file == null)
                    throw new TransactionException(ClosedMsg);

                file.Position = ((FileLogPosition)position).Position;

                if (file.ReadInt32() != Magic)
                    throw new TransactionException(CorruptMsg);

                cb = file.ReadInt32();
                if (cb <= 0 || cb + file.Position > file.Length)
                    throw new TransactionException(CorruptMsg);

                recEndPos   = file.Position + cb;
                description = file.ReadString32();
                operation   = resource.ReadOperation(file);

                if (file.Position != recEndPos)
                {
                    SysLog.LogWarning("ITransactedResource.ReadOperation() returned with an unexpected stream position.");
                    file.Position = recEndPos;
                }

                return operation;
            }
        }

        /// <summary>
        /// Writes an <see cref="IOperation" /> to the log.
        /// </summary>
        /// <param name="resource">The parent <see cref="ITransactedResource" /> responsible for serializing the operation.</param>
        /// <param name="operation">The operation to be written.</param>
        /// <remarks>
        /// <note>
        /// This property can only be called if the operation log is in <see cref="OperationLogMode.Undo" />
        /// mode.
        /// </note>
        /// </remarks>
        /// <exception cref="TransactionException">Thrown if the log isn't open or if the mode isn't <see cref="OperationLogMode.Undo" />.</exception>
        public void Write(ITransactedResource resource, IOperation operation)
        {

            long cbPos;
            long cb;

            using (TimedLock.Lock(this))
            {
                if (file == null)
                    throw new TransactionException(ClosedMsg);

                if (mode != OperationLogMode.Undo)
                    throw new TransactionException("Write is available only when the log is in UNDO mode.");

                file.WriteInt32(Magic);                     // Magic number
                cbPos = file.Position;                      // Length place holder
                file.WriteInt32(0);
                file.WriteString32(operation.Description);  // Description
                resource.WriteOperation(file, operation);    // Serialized operation

                cb = file.Position - cbPos - 4;
                if (cb < 0 || cb > int.MaxValue)
                    throw new TransactionException("ITransactedResource.WriteOperation() returned with an unexpected stream position.");

                file.Position = cbPos;
                file.WriteInt32((int)cb);
                file.Position = file.Length;
                file.Flush();
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
