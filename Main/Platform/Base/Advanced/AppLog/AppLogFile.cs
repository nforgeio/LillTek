//-----------------------------------------------------------------------------
// FILE:        AppLogFile.cs
// CONTRIBUTOR: Jeff Lill
// COPYRIGHT:   Copyright (c) 2005-2015 by Jeffrey Lill.  All rights reserved.
// DESCRIPTION: Provides methods to access an application log file.

using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Text;
using System.Threading;

using LillTek.Common;

namespace LillTek.Advanced
{
    /// <summary>
    /// Provides methods to access an application log file.
    /// </summary>
    /// <remarks>
    /// <para>
    /// A log file is basically an ordered series of log records with
    /// each record holding zero or more application defined name/value
    /// pairs.  The <see cref="Create" /> method creates a new log file
    /// that will be configured to write new log records via <see cref="Write" />.
    /// The <see cref="Open" /> method opens the named log file.
    /// </para>
    /// <para><b><u>Implementation Notes</u></b></para>
    /// <para>
    /// Each log file holds a series of application log records as noted in 
    /// <see cref="AppLog" />.  Log files come in two basic types, <b>committed</b>
    /// and <b>uncommitted</b> log files.  Uncommitted files are generally
    /// named with a timestamp and the <b>.new</b> file extension.  Uncommitted
    /// log files hold the records in the processed of being written by 
    /// log writers.  Committed files generally have the <b>.log</b> file
    /// extension and hold log records that have already been completely
    /// saved by a log writer and are ready to be processed by a log reader.
    /// </para>
    /// <para>
    /// Committed and uncomitted log files share the same basic file structure,
    /// although uncommitted files are incomplete.  The diagram below shows the
    /// basic file structure:
    /// </para>
    /// <code language="none">
    /// 
    ///      Log File Structure
    /// 
    ///     +------------------+
    ///     |      Header      |
    ///     +------------------+
    ///     |   Log Record 0   |
    ///     +------------------+
    ///     |   Log Record 1   |
    ///     +------------------+
    ///     |       ...        |
    ///     +------------------+
    ///     |   Log Record N   |
    ///     +------------------+
    ///     |    Record End    |    16-bit: Magic number (0xB764) indicating
    ///     +------------------+            the end of the records section.
    ///     |                  |
    ///     |   Field Names    |
    ///     |                  |
    ///     +------------------+
    /// 
    /// </code>
    /// <para>
    /// The header holds the magic number, the file format version, and
    /// the byte offset of the Field Names section of the file.  This
    /// is followed by a series of log records (whose format is described
    /// below) and finally by the Field Names section, which maps 16-bit
    /// field name IDs to string field names.
    /// </para>
    /// <code language="none">
    /// 
    ///        Header Structure
    /// 
    ///     +------------------+
    ///     |   Magic Number   |    32-bit: AppLog.Magic
    ///     +------------------+
    ///     |  Version Major   |    16-bit: Format major version number
    ///     +------------------+
    ///     |  Version Minor   |    16-bit: Format minor version number
    ///     +------------------+
    ///     |  Field Names Pos |    32-bit: Byte offset of the Field Names section
    ///     +------------------+            or 0 if the file is uncommitted
    ///     |                  |
    ///     |     Date (UTC)   |    64-bit: File creation data expressed as a
    ///     |                  |            .NET tick count (UTC)
    ///     +------------------+
    ///     |                  |
    ///     |   Schema Name    |    Application schema name (string with 16-bit length)
    ///     |                  |
    ///     +------------------+
    ///     |                  |
    ///     |  Schema Version  |    Application schema version (string with 16-bit length)
    ///     |                  |
    ///     +------------------+
    ///     
    /// </code>
    /// <para>
    /// Log records are logically a series of name/value pairs where the
    /// name is a string and the value is either a string or a byte 
    /// array.  To reduce the size of log files and to improve performance,
    /// names are stored as integer values in the log records written
    /// to disk.  These values index the name in the File Names section
    /// of the log file.
    /// </para>
    /// <para>
    /// Each log record is written to the file using the format shown
    /// below:
    /// </para>
    /// <code language="none">
    /// 
    ///        Record Structure
    ///     
    ///     +------------------+
    ///     |   Record Magic   |    16-bit: Record magic number (0xA764)
    ///     +------------------+
    ///     |   Record Length  |    32-bit: # of bytes of record data to follow
    ///     +------------------+
    ///     |   Record Flags   |    8-bit:  Flag bits (0x80 = record is deleted)
    ///     +------------------+
    ///     |    Field Count   |    16-bit: Number of field name/value pairs to follow
    ///     +------------------+
    ///     |     Field 0      |
    ///     +------------------+
    ///     |     Field 1      |
    ///     +------------------+            Field Records
    ///     |       ...        |
    ///     +------------------+
    ///     |     Field N      |
    ///     +------------------+
    /// 
    /// </code>
    /// <para>
    /// The flags field currently indicates whether the record is marked
    /// for deletion or not by setting bit 0x80.  The other bits are 
    /// reserved for future use and should be set to zero.
    /// </para>
    /// <code language="none">
    /// 
    ///        Field Structure
    /// 
    ///     +------------------+
    ///     |    Field Flags   |    8-bit:  Flag bits and partial field name offset
    ///     +------------------|
    ///     |    Name Index    |    8-bit:  Additional name offset (optional)
    ///     +------------------|
    ///     |    Value Size    |    var:    Variable length value size in bytes
    ///     +------------------|
    ///     |     Value[0]     |
    ///     +------------------|
    ///     |     Value[1]     |
    ///     +------------------|            The value bytes
    ///     |       ...        |
    ///     +------------------|
    ///     |     Value[N]     |
    ///     +------------------|
    /// 
    /// </code>
    /// <para>
    /// One bit of the flags are used to encode whether the value is a byte 
    /// array or a UTF-8 encoded string.  The remaining bits are are used to encode
    /// 6 bits of the field name index as well as to indicate whether
    /// an additional 8-bits are necessary to encode the full field
    /// name index.
    /// </para>
    /// <code language="none">
    /// 
    ///     Field Flag Bits
    ///     ---------------
    /// 
    ///     0x80    Set for a byte array value, zero for a UTF-8 string
    ///     0x40    Set if an additional 8-bits are necessary to encode
    ///             the field name index
    ///     0x3F    Mask for the field name index
    /// 
    /// </code>
    /// <para>
    /// If the 0x40 flag bit is not set, then the value gotten by masking the
    /// field flag with 0x3F is the complete field name index and the optional
    /// Name Index field will not be written to the record.  If the 0x40 bit
    /// is set, then the bits masked by 0x3F are the most signficant 6 bits of
    /// the field name index and the 8-bit Name Index byte that follows are
    /// the least signficant 8 bits on the index.  This scheme allows for
    /// encoding the first 64 field names in a log file in the single byte
    /// flag, and up to a maximum of 16K unique field names within a single
    /// log file.
    /// </para>
    /// <para>
    /// The Value Size indicating the number of bytes in a field's data is
    /// a variable length value.  This most significant bit of the first
    /// byte indicates whether the size is 7 or 31 bits.  If bit 0x80 of
    /// the first byte is 0 then the remaining 7 bits represents the length
    /// of the data (a range of 0..127).  If bit 0x80 is set then the remaining
    /// 7 bits of the byte represent the most signficant 7 bits of the 
    /// length with the next three bytes representing the least signficant 
    /// 24 bits of the data length (a range of 0..2^31-1).
    /// </para>
    /// <para>
    /// Uncommited log files are created by first writing the header with
    /// the Field Names offset set to 0 (which is the indicator that a log
    /// file is uncomitted or potentially damaged).  Log records are appended to
    /// the file while the class maintains a table tracking the field names
    /// added and mapping them to unique indices.  Uncommited log files
    /// are committed when <see cref="Close" /> is called.
    /// </para>
    /// <para>
    /// A log file is committed by writing the field names to the end of the
    /// log file, ordered by their field name index and then recording 
    /// the offset of the first field name in the file's header record.
    /// Field names are written using a 16-bit length followed by UTF-8
    /// encoded text.
    /// </para>
    /// <code language="none">
    /// 
    ///         Field Names
    /// 
    ///     +------------------+
    ///     |      Count       |    16-bit: Number of field names
    ///     +------------------+
    ///     |     Field 0      |    Field names stored as 16-bit length followed by
    ///     +------------------+    UDF-8 encoded text.
    ///     |     Field 1      |
    ///     +------------------+
    ///     |       ...        |
    ///     +------------------+
    ///     |     Field N      |
    ///     +------------------+
    /// 
    /// </code>
    /// <note>
    /// All multibyte integer values are stored in network (big-endian) order.
    /// </note>
    /// </remarks>
    /// <threadsafety static="false" instance="false" />
    public class AppLogFile
    {
        private const int MaxFieldNames = 32768;
        private const int MaxDataLen    = 32767;

        private EnhancedStream              file;           // Stream referencing the actual log file (null if closed)
        private string                      path;           // The log file name
        private Version                     version;        // File format version
        private int                         fieldNamesPos;  // File position of the field names section for readers (0 for writers)
        private string                      schemaName;     // Application schema version for the log file
        private Version                     schemaVersion;  // Application schema name for this file
        private DateTime                    createDate;     // Date the file was created
        private string[]                    id2Field;       // Used in files opened for reading to map field IDs to names
        private Dictionary<string, int>     field2ID;       // Used in files opened for writing to map field names to IDs
        private int                         cRecWritten;    // Number of records written to the file
        private bool                        readMode;       // True if the file was opened for reading, false for writing
        private bool                        recDeleted;     // True if any records in the file were marked for deletion.
        private long                        hdrFNPos;       // File position of the Field Names Pos offset in the header
        private long                        firstRecPos;    // File position of the first record

        /// <summary>
        /// Conbstructor.
        /// </summary>
        public AppLogFile()
        {
            this.file = null;
        }

        /// <summary>
        /// Opens an existing application log file.
        /// </summary>
        /// <param name="path">The log file name.</param>
        /// <param name="cbBuffer">The file buffer size in bytes.</param>
        /// <exception cref="LogCorruptedException">Thrown when the log file is determined to be corrupted.</exception>
        /// <exception cref="LogVersionException">Thrown when the current implementation does not support the log version.</exception>
        public void Open(string path, int cbBuffer)
        {
            Version     ver;
            int         cFieldNames;

            this.file        = new EnhancedFileStream(path, FileMode.Open, FileAccess.ReadWrite, FileShare.None, cbBuffer);
            this.path        = Path.GetFullPath(path);
            this.cRecWritten = 0;
            this.readMode    = true;
            this.recDeleted  = false;

            // Read the file header.

            try
            {
                try
                {
                    if (file.ReadInt32() != AppLog.FileMagic)
                        throw new LogCorruptedException();

                    ver           = new Version(file.ReadInt16(), file.ReadInt16());
                    hdrFNPos      = file.Position;
                    fieldNamesPos = file.ReadInt32();
                    schemaName    = file.ReadString16();
                    schemaVersion = new Version(file.ReadString16());
                    createDate    = new DateTime(file.ReadInt64());
                }
                catch
                {
                    throw new LogCorruptedException();
                }

                if (ver > AppLog.Version)
                    throw new LogVersionException();

                if (fieldNamesPos <= file.Position)
                    throw new LogCorruptedException();  // This field wasn't rewritten or isn't valid

                firstRecPos = file.Position;            // Remember the position of the first record

                // The file must include at least one record.

                if (file.ReadInt16() != AppLog.RecordMagic)
                    throw new LogCorruptedException();

                // Load the field names.

                try
                {
                    file.Position = fieldNamesPos;
                    cFieldNames   = file.ReadInt16();
                    if (cFieldNames < 0 || cFieldNames > MaxFieldNames)
                        throw new LogCorruptedException();

                    id2Field = new string[cFieldNames];
                    for (int i = 0; i < cFieldNames; i++)
                    {
                        id2Field[i] = file.ReadString16();
                        if (id2Field[i] == null)
                            throw new LogCorruptedException();
                    }
                }
                catch
                {
                    throw new LogCorruptedException();
                }

                // Seek back to the first record.

                file.Position = firstRecPos;
            }
            catch
            {
                if (file != null)
                {
                    file.Close();
                    file = null;
                }
            }
        }

        /// <summary>
        /// Creates a new uncommitted application log file, overwriting
        /// any existing file.
        /// </summary>
        /// <param name="path">The log file name.</param>
        /// <param name="cbBuffer">The file buffer size in bytes.</param>
        /// <param name="schemaName">Specifies the application schema name for all records to be written to this log file.</param>
        /// <param name="schemaVersion">Specifies the application schema version for all records to be written to this log file.</param>
        public void Create(string path, int cbBuffer, string schemaName, Version schemaVersion)
        {
            this.file          = new EnhancedFileStream(path, FileMode.Create, FileAccess.ReadWrite, FileShare.None, cbBuffer);
            this.path          = Path.GetFullPath(path);
            this.version       = AppLog.Version;
            this.fieldNamesPos = 0;
            this.schemaName    = schemaName;
            this.schemaVersion = schemaVersion;
            this.createDate    = DateTime.UtcNow;
            this.readMode      = false;
            this.field2ID      = new Dictionary<string, int>();
            this.cRecWritten   = 0;

            // Write the header record with FieldNamesPos=0

            file.WriteInt32(AppLog.FileMagic);
            file.WriteInt16(AppLog.Version.Major);
            file.WriteInt16(AppLog.Version.Minor);
            hdrFNPos = file.Position;
            file.WriteInt32(0);                     // FieldNamesPos=0
            file.WriteString16(schemaName);
            file.WriteString16(schemaVersion.ToString());
            file.WriteInt64(createDate.Ticks);

            firstRecPos = file.Position;
        }

        /// <summary>
        /// Closes the application log file, committing the file if it is open
        /// for writing and is uncommitted.
        /// </summary>
        public void Close()
        {
            if (file == null)
                return;

            if (readMode)
            {
                bool    delFile = false;
                int     magic;
                int     cbRecord;
                int     flags;

                // If any records were marked for deletion in this file, then
                // walk the file records to see if all of the records are marked
                // for deletion.  If so, then we're going to delete the file
                // after closing it.

                if (recDeleted)
                {
                    delFile = true;
                    file.Position = firstRecPos;

                    magic = file.ReadInt16();
                    while (magic == AppLog.RecordMagic)
                    {
                        cbRecord = file.ReadInt32();
                        flags    = file.ReadByte();

                        if ((flags & 0x80) == 0)
                        {

                            delFile = false;
                            break;
                        }

                        file.Position = file.Position + cbRecord - 1;
                        magic         = file.ReadInt16();
                    }
                }

                // Finish up

                file.Close();
                file = null;

                if (delFile)
                    File.Delete(path);
            }
            else
            {
                // If the file was opened for writing and there were no records
                // written then close the file and delete it.

                if (cRecWritten == 0)
                {
                    file.Close();
                    file = null;
                    File.Delete(path);

                    return;
                }

                // Write the RecordEnd marker followed by the field names section
                // and then go back and write the offset to the field names section
                // into the file header.

                string[]    fieldNames;
                int         fieldNamesPos;

                file.WriteInt16(AppLog.RecordEnd);
                fieldNamesPos = (int)file.Position;

                fieldNames = new string[field2ID.Count];
                foreach (string key in field2ID.Keys)
                    fieldNames[field2ID[key]] = key;
#if DEBUG
                for (int i = 0; i < fieldNames.Length; i++)
                    Assertion.Test(fieldNames[i] != null);
#endif
                file.WriteInt16(fieldNames.Length);
                for (int i = 0; i < fieldNames.Length; i++)
                    file.WriteString16(fieldNames[i]);

                file.Position = hdrFNPos;
                file.WriteInt32(fieldNamesPos);

                // Close the file and rename it from *.new to *.log

                file.Close();
                file = null;

                // It's possible under rare circumstances to get a file name collision
                // if the system is running so fast that we're on the same millisecond
                // as when the last log file was created.  This will probably never happen
                // in real life, but just in case, I'm going to retry the file rename
                // once after sleeping for 20ms.

                try
                {
                    Directory.Move(path, Helper.GetFullNameWithoutExtension(path) + ".log");
                }
                catch (IOException)
                {
                    Thread.Sleep(20);
                    Directory.Move(path, Path.GetDirectoryName(path) + Helper.PathSepString + DateTime.UtcNow.ToString(AppLog.FileDateFormat) + ".log");
                }
            }
        }

        /// <summary>
        /// Returns the fully qualified path to this log file in the file system.
        /// </summary>
        public string FullPath
        {
            get { return path; }
        }

        /// <summary>
        /// Returns the log's creation date (UTC).
        /// </summary>
        public DateTime CreateDate
        {
            get { return createDate; }
        }

        /// <summary>
        /// Returns the next non-deleted record from the file, optionally marking it for deletion.
        /// </summary>
        /// <param name="deleteRecord">Pass <c>true</c> to mark the record for deletion.</param>
        /// <returns>The next record or <c>null</c> if there are no more records to be read.</returns>
        /// <exception cref="LogCorruptedException">Thrown when the log file is determined to be corrupted.</exception>
        private AppLogRecord Read(bool deleteRecord)
        {
            int             magic;
            int             cbRecord;
            int             flags;
            int             cFields;
            bool            deleted;
            int             nameIndex;
            bool            isByteArray;
            byte[]          data;
            int             cbData;
            AppLogRecord    record;
            long            flagPos;
            long            nextPos;

            if (!readMode)
                throw new LogAccessException(readMode);

        tryAgain: try
            {
                magic = file.ReadInt16();
                if (magic == AppLog.RecordEnd)
                {
                    // Seek back 2 bytes to keep the file position
                    // at the RecordEnd marker.

                    file.Seek(-2, SeekOrigin.Current);
                    return null;
                }

                if (magic != AppLog.RecordMagic)
                    throw new LogCorruptedException();

                cbRecord = file.ReadInt32();
                flagPos  = file.Position;
                deleted  = (file.ReadByte() & 0x80) != 0;
                cFields  = file.ReadInt16();

                if (deleted)
                    record = null;
                else
                    record = new AppLogRecord(schemaName, schemaVersion);

                for (int i = 0; i < cFields; i++)
                {
                    flags       = file.ReadByte();
                    isByteArray = (flags & 0x80) != 0;

                    if ((flags & 0x40) != 0)
                        nameIndex = ((flags & 0x3F) << 8) | file.ReadByte();
                    else
                        nameIndex = flags & 0x3F;

                    if (nameIndex >= id2Field.Length)
                        throw new LogCorruptedException();

                    cbData = file.ReadByte();
                    if ((cbData & 0x80) != 0)
                    {
                        // cbData holds the most significant 7-bits of the field
                        // length and the next three bytes hold the least
                        // significant 24-bits.

                        cbData = ((cbData & 0x7F) << 24) | (file.ReadByte() << 16) | (file.ReadByte() << 8) | file.ReadByte();
                    }

                    data = file.ReadBytes(cbData);

                    if (record != null)
                    {
                        if (isByteArray)
                            record.Add(id2Field[nameIndex], data);
                        else
                            record.Add(id2Field[nameIndex], Helper.FromUTF8(data));
                    }
                }

                if (record != null)
                {
                    if (deleteRecord)
                    {
                        recDeleted = true;
                        nextPos = file.Position;
                        file.Position = flagPos;
                        file.WriteByte(0x80);
                        file.Position = nextPos;
                    }

                    if (file.Position != flagPos + cbRecord)
                        throw new LogCorruptedException();

                    return record;
                }
                else
                    goto tryAgain;
            }
            catch
            {
                throw new LogCorruptedException();
            }
        }

        /// <summary>
        /// Returns the next non-deleted record from the file but does not
        /// advance the record pointer.
        /// </summary>
        /// <returns>The next record or <c>null</c> if there are no more records to be read.</returns>
        public AppLogRecord Peek()
        {
            long            savePos;
            AppLogRecord    record;

            savePos = file.Position;
            record = Read(false);
            file.Position = savePos;

            return record;
        }

        /// <summary>
        /// Returns the next non-deleted record from the file.
        /// </summary>
        /// <returns>The next record or <c>null</c> if there are no more records to be read.</returns>
        /// <exception cref="LogCorruptedException">Thrown when the log file is determined to be corrupted.</exception>
        public AppLogRecord Read()
        {
            return Read(false);
        }

        /// <summary>
        /// Returns the next non-deleted record from the file and then marks it for deletion.
        /// </summary>
        /// <returns>The next record or <c>null</c> if there are no more records to be read.</returns>
        /// <exception cref="LogCorruptedException">Thrown when the log file is determined to be corrupted.</exception>
        public AppLogRecord ReadDelete()
        {
            return Read(true);
        }

        /// <summary>
        /// Specifies the position within the application log of the next record to be
        /// read.  This method is available only for files opened for reading.
        /// </summary>
        /// <remarks>
        /// This should be treated as opaque by the calling code.  In this implementation
        /// this is simply the byte offset of the current position in the file rendered
        /// as a string or as "END" indicating that the file position should be set
        /// to the record end marker.
        /// </remarks>
        /// <exception cref="LogAccessException">Thrown for files opened for writing.</exception>
        public string Position
        {
            get
            {
                if (!readMode)
                    throw new LogAccessException(readMode);

                return file.Position.ToString();
            }

            set
            {
                long    orgPos;
                long    pos;
                int     v;

                if (!readMode)
                    throw new LogAccessException(readMode);

                if (String.Compare(value, "END", true) == 0)
                {
                    // $hack(jeff.lill): 
                    //
                    // This is a bit of a hack but it will work fine
                    // unless the file format changes.  The assumption
                    // here is that the record end marker is the
                    // 16-bit int just before the field names section
                    // of the file.

                    pos = fieldNamesPos - 2;
                }
                else
                {
                    pos = long.Parse(value);
                }

                orgPos = file.Position;
                file.Position = pos;

                // Verify that this offset points to the beginning of a record
                // or the end of records magic number.

                v = file.ReadInt16();
                if (v != AppLog.RecordMagic && v != AppLog.RecordEnd)
                {
                    file.Position = orgPos;
                    throw new ArgumentException("Invalid position string.");
                }

                file.Position = pos;
            }
        }

        /// <summary>
        /// Appends the record to the log file.
        /// </summary>
        /// <param name="record">The record to be appended.</param>
        public void Write(AppLogRecord record)
        {

            long recLenPos;
            long nextPos;

            if (readMode)
                throw new LogAccessException(readMode);

            file.WriteInt16(AppLog.RecordMagic);
            recLenPos = file.Position;
            file.WriteInt32(0);                 // Leave space for the record length
            file.WriteByte(0);                  // Flags = 0
            file.WriteInt16(record.Count);      // Field Count

            foreach (DictionaryEntry entry in record)
            {
                string  fieldName = ((string)entry.Key).ToLowerInvariant();
                int     fieldID;
                int     flags;
                byte[]  data;
                string  s;

                // Map the field name to an ID, adding the field to
                // the map table if necessary

                if (!field2ID.TryGetValue(fieldName, out fieldID))
                {
                    fieldID = field2ID.Count;
                    field2ID.Add(fieldName, fieldID);
                    if (field2ID.Count > MaxFieldNames)
                        throw new LogException("Too many unique field names.");
                }

                // Convert strings to UTF-8 byte arrays and set the
                // flag 0x80 bit if the data is a byte array.

                s = entry.Value as string;
                if (s == null)
                {
                    flags = 0x80;
                    data = (byte[])entry.Value;
                }
                else
                {
                    flags = 0;
                    data = Helper.ToUTF8(s);
                }

                // Combine the field ID into the flag bits and write the
                // flags out as well as the extended portion of the field
                // name ID (if necessary).

                if (fieldID > 0x3F)
                {
                    flags |= 0x40;
                    flags |= (fieldID >> 8) & 0x3F;
                    file.WriteByte((byte)flags);
                    file.WriteByte((byte)(fieldID & 0xFF));
                }
                else
                    file.WriteByte((byte)(flags | fieldID));

                // Write out the variable length data length field.

                if (data.Length <= 127)
                    file.WriteByte((byte)data.Length);
                else
                {
                    file.WriteByte((byte)(0x80 | (data.Length >> 24)));
                    file.WriteByte((byte)(data.Length >> 16));
                    file.WriteByte((byte)(data.Length >> 8));
                    file.WriteByte((byte)(data.Length));
                }

                // Write out the value bytes

                file.WriteBytesNoLen(data);

                // Compute the overall length of the record and
                // go back and write it after the magic number.

                nextPos       = file.Position;
                file.Position = recLenPos;
                file.WriteInt32((int)(nextPos - recLenPos - 4));
                file.Position = nextPos;
            }

            cRecWritten++;
        }

        /// <summary>
        /// Returns the current size of the log file in bytes.
        /// </summary>
        public long Size
        {
            get { return file.Length; }
        }

        /// <summary>
        /// Returns the number of records written to the log since
        /// it was created.
        /// </summary>
        public int WriteCount
        {
            get { return cRecWritten; }
        }
    }
}
