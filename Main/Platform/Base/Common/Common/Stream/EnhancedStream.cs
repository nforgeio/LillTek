//-----------------------------------------------------------------------------
// FILE:        EnhancedStream.cs
// CONTRIBUTOR: Jeff Lill
// COPYRIGHT:   Copyright (c) 2005-2015 by Jeffrey Lill.  All rights reserved.
// DESCRIPTION: Extends basic stream classes to handle the serialization of additional types.

using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

// $todo(jeff.lill): 
//
// Many of the length operations are specified using 32-bit integers.
// This should be changed to 64-bit values.

// $todo(jeff.lill): Implement all [*Async()] method overrides.

namespace LillTek.Common
{

    /// <summary>
    /// Extends basic stream classes to handle the serialization of additional types.
    /// </summary>
    public class EnhancedStream : Stream
    {
        //---------------------------------------------------------------------
        // Static members

        private const int CopyBufSize = 8192;

        /// <summary>
        /// Returns an enhanced stream with no backing store.
        /// </summary>
        /// <remarks>
        /// Any data written to this stream will simply be discarded.  Any reads
        /// will simply return 0 bytes of data.
        /// </remarks>
        public static new EnhancedStream Null
        {
            get { return new EnhancedStream(Stream.Null); }
        }

        /// <summary>
        /// Copies data from the current position of the input stream
        /// to the current position of the output stream, advancing
        /// both stream's positions.
        /// </summary>
        /// <param name="input">The input stream.</param>
        /// <param name="output">The output stream.</param>
        /// <param name="length">The number of bytes to copy.</param>
        /// <remarks>
        /// <note>
        /// If <paramref name="length" /> is greater than the number of
        /// bytes remaining in the input stream then only the data up to
        /// the end of the input file will be copied and no exception
        /// will be thrown.
        /// </note>
        /// </remarks>
        public static void Copy(Stream input, Stream output, int length)
        {
            byte[]  buf = new byte[4096];
            int     cb;

            while (length > 0)
            {
                cb = input.Read(buf, 0, Math.Min(buf.Length, length));
                if (cb == 0)
                    return;

                output.Write(buf, 0, cb);
                length -= cb;
            }
        }

        //---------------------------------------------------------------------
        // Instance members

        private Stream  s;      // The underlying stream

        /// <summary>
        /// Constructs an enhanced stream from the stream passed.
        /// </summary>
        /// <param name="stream">The stream.</param>
        public EnhancedStream(Stream stream)
        {
            this.s = stream;
        }

        /// <summary>
        /// Verifies that the buffer length passed is reasonable given the
        /// current stream position.
        /// </summary>
        /// <param name="cb">The length to check.</param>
        /// <remarks>
        /// <para>
        /// The method will throw an exception if the length is negative or
        /// if the length plus the current file position exceeds the length
        /// of the stream.
        /// </para>
        /// <note>
        /// This method does not perform the check if the stream does not support 
        /// seeking because the stream length will not be valid in this case.
        /// </note>
        /// </remarks>
        public void VerifyBufLength(int cb)
        {
            if (!s.CanSeek)
                return;     // Stream length property is not valid.

            if (cb < 0 || cb + s.Position > s.Length)
                throw new InvalidOperationException("Invalid buffer length.");
        }

        /// <summary>
        /// Returns the underlying stream.
        /// </summary>
        public Stream InnerStream
        {
            get { return s; }
        }

        /// <summary>
        /// Returns the current stream position.
        /// </summary>
        public override long Position
        {
            get { return s.Position; }
            set { s.Position = value; }
        }

        /// <summary>
        /// Returns <c>true</c> if the stream supports read operations.
        /// </summary>
        public override bool CanRead
        {
            get { return s.CanRead; }
        }

        /// <summary>
        /// Returns <c>true</c> if the stream supports write operations.
        /// </summary>
        public override bool CanWrite
        {
            get { return s.CanWrite; }
        }

        /// <summary>
        /// Returns <c>true</c> if the stream supports seek operations.
        /// </summary>
        public override bool CanSeek
        {
            get { return s.CanSeek; }
        }

        /// <summary>
        /// Returns the current size of the stream in bytes.
        /// </summary>
        public override long Length
        {
            get { return s.Length; }
        }

        /// <summary>
        /// Sets the current stream position.
        /// </summary>
        /// <param name="position">The new position.</param>
        /// <param name="origin">The origin.</param>
        public override long Seek(long position, SeekOrigin origin)
        {
            return s.Seek(position, origin);
        }

        /// <summary>
        /// Sets the stream length.
        /// </summary>
        /// <param name="length">The new stream length.</param>
        public override void SetLength(long length)
        {
            s.SetLength(length);
        }

        /// <summary>
        /// Returns <c>true</c> if the current position is at the end
        /// of the file.
        /// </summary>
        public virtual bool Eof
        {
            get { return s.Length == s.Position; }
        }

        /// <summary>
        /// Writes data from the offset the specified buffer to the
        /// stream.
        /// </summary>
        /// <param name="buffer">The data buffer.</param>
        /// <param name="offset">Offset of the first byte to be written.</param>
        /// <param name="count">The number of bytes to write.</param>
        public override void Write(byte[] buffer, int offset, int count)
        {
            s.Write(buffer, offset, count);
        }

        /// <summary>
        /// Writes the boolean value to the stream at the current position,
        /// advancing the position past the written value.
        /// </summary>
        /// <param name="b">The value.</param>
        public void WriteBool(bool b)
        {
            s.WriteByte(b ? (byte)1 : (byte)0);
        }

        /// <summary>
        /// Writes the byte value to the stream at the current position,
        /// advancing the position past the written value.
        /// </summary>
        /// <param name="value">The value.</param>
        public override void WriteByte(byte value)
        {
            s.WriteByte(value);
        }

        /// <summary>
        /// Writes the byte array to the stream at the current position,
        /// advancing the position past the written value.  This version
        /// writes a 16-bit length.
        /// </summary>
        /// <param name="bytes">The byte array.</param>
        /// <remarks>
        /// <note>
        /// <c>null</c> arrays may be written and also that array
        /// length cannot exceed ushort.MaxValue-1.
        /// </note>
        /// </remarks>
        public void WriteBytes16(byte[] bytes)
        {
            if (bytes == null)
            {
                WriteInt16(ushort.MaxValue);
                return;
            }

            if (bytes.Length >= ushort.MaxValue)
                throw new ArgumentException("Byte array is too large.");

            WriteInt16(bytes.Length);
            s.Write(bytes, 0, bytes.Length);
        }

        /// <summary>
        /// Writes the byte array to the stream at the current position,
        /// advancing the position past the written value.  
        /// </summary>
        /// <param name="bytes">The byte array.</param>
        /// <remarks>
        /// This version does not write a length.
        /// </remarks>
        public void WriteBytesNoLen(byte[] bytes)
        {
            s.Write(bytes, 0, bytes.Length);
        }

        /// <summary>
        /// Writes the byte array to the stream at the current position,
        /// advancing the position past the written value.  This version
        /// writes a 32-bit length.
        /// </summary>
        /// <param name="bytes">The byte array.</param>
        /// <remarks>
        /// <note>
        /// <c>null</c> arrays may be written and also that array
        /// length cannot exceed uint.MaxValue-1.
        /// </note>
        /// </remarks>
        public void WriteBytes32(byte[] bytes)
        {
            if (bytes == null)
            {
                WriteInt32(-1);
                return;
            }

            WriteInt32(bytes.Length);
            s.Write(bytes, 0, bytes.Length);
        }

        /// <summary>
        /// Writes the 16-bit integer value to the stream at the current position,
        /// advancing the position past the written value.  The integer is written
        /// in network (<b>big-endian</b>) order.
        /// </summary>
        /// <param name="i">The value.</param>
        public void WriteInt16(int i)
        {
            s.WriteByte((byte)(i >> 8));
            s.WriteByte((byte)i);
        }

        /// <summary>
        /// Writes the 16-bit integer value to the stream at the current position,
        /// advancing the position past the written value.  The integer is written
        /// in <b>little-endian</b> order.
        /// </summary>
        /// <param name="i">The value.</param>
        public void WriteInt16Le(int i)
        {
            s.WriteByte((byte)i);
            s.WriteByte((byte)(i >> 8));
        }

        /// <summary>
        /// Writes the 32-bit integer value to the stream at the current position,
        /// advancing the position past the written value.  The integer is written
        /// in network (<b>big-endian</b>) order.
        /// </summary>
        /// <param name="i">The value.</param>
        public void WriteInt32(int i)
        {
            s.WriteByte((byte)(i >> 24));
            s.WriteByte((byte)(i >> 16));
            s.WriteByte((byte)(i >> 8));
            s.WriteByte((byte)i);
        }

        /// <summary>
        /// Writes the 32-bit integer value to the stream at the current position,
        /// advancing the position past the written value.  The integer is written
        /// in <b>little-endian</b> order.
        /// </summary>
        /// <param name="i">The value.</param>
        public void WriteInt32Le(int i)
        {
            s.WriteByte((byte)i);
            s.WriteByte((byte)(i >> 8));
            s.WriteByte((byte)(i >> 16));
            s.WriteByte((byte)(i >> 24));
        }

        /// <summary>
        /// Writes the 64-bit integer value to the stream at the current position,
        /// advancing the position past the written value.  The integer is written
        /// in network (<b>big-endian</b>) order.
        /// </summary>
        /// <param name="value">The value.</param>
        public void WriteInt64(long value)
        {
            s.WriteByte((byte)(value >> 56));
            s.WriteByte((byte)(value >> 48));
            s.WriteByte((byte)(value >> 40));
            s.WriteByte((byte)(value >> 32));
            s.WriteByte((byte)(value >> 24));
            s.WriteByte((byte)(value >> 16));
            s.WriteByte((byte)(value >> 8));
            s.WriteByte((byte)value);
        }

        /// <summary>
        /// Writes the 64-bit integer value to the stream at the current position,
        /// advancing the position past the written value.  The integer is written
        /// in <b>little-endian</b> order.
        /// </summary>
        /// <param name="value">The value.</param>
        public void WriteInt64Le(long value)
        {
            s.WriteByte((byte)value);
            s.WriteByte((byte)(value >> 8));
            s.WriteByte((byte)(value >> 16));
            s.WriteByte((byte)(value >> 24));
            s.WriteByte((byte)(value >> 32));
            s.WriteByte((byte)(value >> 40));
            s.WriteByte((byte)(value >> 48));
            s.WriteByte((byte)(value >> 56));
        }

        /// <summary>
        /// Writes the float value to the stream at the current position,
        /// advancing the position past the written value.
        /// </summary>
        /// <param name="value">The value.</param>
        public void WriteFloat(float value)
        {
            WriteString16(value.ToString());
        }

        /// <summary>
        /// Writes the string value to the stream at the current position,
        /// advancing the position past the written value.  This version
        /// does not write a length to the stream.
        /// </summary>
        /// <param name="value">The string value.</param>
        /// <remarks>
        /// <note>
        /// <c>null</c> strings may not be written using this method.
        /// </note>
        /// </remarks>
        public void WriteStringNoLen(string value)
        {
            if (value == null)
                throw new ArgumentNullException();

            byte[]  buf;
            int     cb;

            buf = Encoding.UTF8.GetBytes(value);
            cb  = buf.Length;

            s.Write(buf, 0, cb);
        }

        /// <summary>
        /// Writes the string value to the stream at the current position,
        /// advancing the position past the written value.  This version
        /// writes a 16-bit length.
        /// </summary>
        /// <param name="value">The string value.</param>
        /// <remarks>
        /// <note>
        /// <c>null</c> strings may be written using this method and that
        /// up to ushort.MaxValue-1 UTF-8 bytes may be written.
        /// </note>
        /// </remarks>
        public void WriteString16(string value)
        {
            if (value == null)
            {
                WriteInt16(ushort.MaxValue);
                return;
            }

            byte[]  buf;
            int     cb;

            buf = Encoding.UTF8.GetBytes(value);
            cb  = buf.Length;

            if (cb >= ushort.MaxValue)
                throw new ArgumentException("String size exceeds ushort.MaxValue-1 after converting to UTF-8.");

            WriteInt16(cb);
            s.Write(buf, 0, cb);
        }

        /// <summary>
        /// Writes the string value to the stream at the current position,
        /// advancing the position past the written value.  This version
        /// writes a 32-bit length.
        /// </summary>
        /// <param name="value">The string value.</param>
        /// <remarks>
        /// <note>
        /// <c>null</c> strings may be written using this method and that
        /// up to uint.MaxValue-1 UTF-8 bytes may be written.
        /// </note>
        /// </remarks>
        public void WriteString32(string value)
        {
            if (value == null)
            {
                WriteInt32(-1);
                return;
            }

            byte[]  buf;
            int     cb;

            buf = Encoding.UTF8.GetBytes(value);
            cb  = buf.Length;

            WriteInt32(cb);
            s.Write(buf, 0, cb);
        }

        /// <summary>
        /// Attempts to read count bytes from the stream, writing them to the
        /// buffer begining at the offset.
        /// </summary>
        /// <param name="buffer">The input buffer.</param>
        /// <param name="offset">Offset where data is to be read.</param>
        /// <param name="count">Number of bytes to read.</param>
        /// <returns>The number of bytes actually read.</returns>
        public override int Read(byte[] buffer, int offset, int count)
        {
            return s.Read(buffer, offset, count);
        }

        /// <summary>
        /// Reads the boolean value from the current position, advancing
        /// the position past the value read.
        /// </summary>
        /// <returns>
        /// The boolean value.
        /// </returns>
        public bool ReadBool()
        {
            return ReadByte() == 1 ? true : false;
        }

        /// <summary>
        /// Reads the byte value from the current position, advancing
        /// the position past the value read.
        /// </summary>
        /// <returns>
        /// The byte read converted to an integer or <b>-1</b> if the end of the
        /// stream has been reached.
        /// </returns>
        public override int ReadByte()
        {
            return s.ReadByte();
        }

        /// <summary>
        /// Reads the byte array from the current position, advancing
        /// the position past the value read.  This version reads a
        /// 16-bit length.
        /// </summary>
        /// <returns>
        /// The byte array.
        /// </returns>
        public byte[] ReadBytes16()
        {
            byte[]   bytes;
            int     cb;

            cb = ReadInt16();
            if (cb == -1)
                return null;

            cb = (ushort)cb;

            VerifyBufLength(cb);
            bytes = new byte[cb];
            s.Read(bytes, 0, cb);

            return bytes;
        }

        /// <summary>
        /// Reads the byte array from the current position, advancing
        /// the position past the value read.
        /// </summary>
        /// <param name="cb">The number of bytes to read.</param>
        /// <returns>
        /// The byte array.  Note that the array returned may have a length
        /// less than the size requested if the end of the file has been
        /// reached.
        /// </returns>
        public byte[] ReadBytes(int cb)
        {
            byte[]  buf;
            byte[]  temp;
            int     cbRead;

            buf    = new byte[cb];
            cbRead = s.Read(buf, 0, cb);

            if (cbRead == cb)
                return buf;

            temp = new byte[cbRead];
            Array.Copy(buf, temp, cbRead);
            return temp;
        }

        /// <summary>
        /// Reads the byte array from the current position to the
        /// end of the stream.
        /// </summary>
        public byte[] ReadBytesToEnd()
        {
            return ReadBytes((int)(this.Length - this.Position));
        }

        /// <summary>
        /// Reads the byte array from the current position, advancing
        /// the position past the value read.  This version reads a
        /// 32-bit length.
        /// </summary>
        /// <returns>
        /// The byte array.
        /// </returns>
        public byte[] ReadBytes32()
        {
            byte[]  bytes;
            int     cb;

            cb = ReadInt32();
            if (cb == -1)
                return null;

            VerifyBufLength(cb);
            bytes = new byte[cb];
            s.Read(bytes, 0, cb);

            return bytes;
        }

        /// <summary>
        /// Reads the 16-bit integer value in network <b>big-endian</b> order 
        /// from the current position, advancing the position past the value read.
        /// </summary>
        /// <returns>
        /// The integer value.
        /// </returns>
        public int ReadInt16()
        {
            int v0, v1;

            v0 = ReadByte();
            v1 = ReadByte();

            return (short)((v0 << 8) | v1);
        }

        /// <summary>
        /// Reads the 16-bit integer value in <b>little-endian</b> order 
        /// from the current position, advancing the position past the value read.
        /// </summary>
        /// <returns>
        /// The integer value.
        /// </returns>
        public int ReadInt16Le()
        {
            int v0, v1;

            v1 = ReadByte();
            v0 = ReadByte();

            return (short)((v0 << 8) | v1);
        }

        /// <summary>
        /// Reads the 32-bit integer value in <b>big-endian</b> order from 
        /// the current position, advancing the position past the value read.
        /// </summary>
        /// <returns>
        /// The integer value.
        /// </returns>
        public int ReadInt32()
        {
            int v0, v1, v2, v3;

            v0 = ReadByte();
            v1 = ReadByte();
            v2 = ReadByte();
            v3 = ReadByte();

            return (v0 << 24) | (v1 << 16) | (v2 << 8) | v3;
        }

        /// <summary>
        /// Reads the 32-bit integer value in <b>little-endian</b> order from 
        /// the current position, advancing the position past the value read.
        /// </summary>
        /// <returns>
        /// The integer value.
        /// </returns>
        public int ReadInt32Le()
        {
            int v0, v1, v2, v3;

            v3 = ReadByte();
            v2 = ReadByte();
            v1 = ReadByte();
            v0 = ReadByte();

            return (v0 << 24) | (v1 << 16) | (v2 << 8) | v3;
        }

        /// <summary>
        /// Reads the 64-bit integer value in <b>big-endian</b> order from 
        /// the current position, advancing the position past the value read.
        /// </summary>
        /// <returns>
        /// The integer value.
        /// </returns>
        public long ReadInt64()
        {
            uint v0, v1, v2, v3, v4, v5, v6, v7;

            v0 = (uint)ReadByte();
            v1 = (uint)ReadByte();
            v2 = (uint)ReadByte();
            v3 = (uint)ReadByte();
            v4 = (uint)ReadByte();
            v5 = (uint)ReadByte();
            v6 = (uint)ReadByte();
            v7 = (uint)ReadByte();

            return ((long)v0 << 56) |
                ((long)v1 << 48) |
                ((long)v2 << 40) |
                ((long)v3 << 32) |
                ((long)v4 << 24) |
                ((long)v5 << 16) |
                ((long)v6 << 8) |
                ((long)v7);
        }

        /// <summary>
        /// Reads the 64-bit integer value in <b>little-endian</b> order from 
        /// the current position, advancing the position past the value read.
        /// </summary>
        /// <returns>
        /// The integer value.
        /// </returns>
        public long ReadInt64Le()
        {
            uint v0, v1, v2, v3, v4, v5, v6, v7;

            v7 = (uint)ReadByte();
            v6 = (uint)ReadByte();
            v5 = (uint)ReadByte();
            v4 = (uint)ReadByte();
            v3 = (uint)ReadByte();
            v2 = (uint)ReadByte();
            v1 = (uint)ReadByte();
            v0 = (uint)ReadByte();

            return ((long)v0 << 56) |
                ((long)v1 << 48) |
                ((long)v2 << 40) |
                ((long)v3 << 32) |
                ((long)v4 << 24) |
                ((long)v5 << 16) |
                ((long)v6 << 8) |
                ((long)v7);
        }

        /// <summary>
        /// Reads the float value from the current position, advancing
        /// the position past the value read.
        /// </summary>
        /// <returns>
        /// The float value.
        /// </returns>
        public float ReadFloat()
        {
            return float.Parse(ReadString16());
        }

        /// <summary>
        /// Reads the string value from the current position, advancing
        /// the position past the value read.  This version reads a 
        /// 16-bit length.
        /// </summary>
        /// <returns>
        /// The string value.
        /// </returns>
        public string ReadString16()
        {
            int     cb;
            byte[]  buf;

            cb = ReadInt16();
            if (cb == -1)
                return null;

            cb = (ushort)cb;

            VerifyBufLength(cb);
            buf = new byte[cb];
            s.Read(buf, 0, cb);

            return Encoding.UTF8.GetString(buf, 0, cb);
        }

        /// <summary>
        /// Reads the UTF-8 encoded string whose byte length is passed
        /// from the stream.
        /// </summary>
        /// <param name="cb">The byte length of the encoded string.</param>
        /// <returns>The string read.</returns>
        public string ReadString(int cb)
        {
            byte[] buf;

            VerifyBufLength(cb);
            buf = new byte[cb];
            s.Read(buf, 0, cb);

            return Encoding.UTF8.GetString(buf, 0, cb);
        }

        /// <summary>
        /// Reads the string value from the current position, advancing
        /// the position past the value read.  This version reads a 
        /// 32-bit length.
        /// </summary>
        /// <returns>
        /// The string value.
        /// </returns>
        public string ReadString32()
        {
            int     cb;
            byte[]  buf;

            cb = ReadInt32();
            if (cb == -1)
                return null;

            VerifyBufLength(cb);
            buf = new byte[cb];
            s.Read(buf, 0, cb);

            return Encoding.UTF8.GetString(buf, 0, cb);
        }

        /// <summary>
        /// Writes the properties to the stream.
        /// </summary>
        /// <param name="properties">The property set.</param>
        public void WriteProperties(Dictionary<string, string> properties)
        {
            WriteInt16(properties.Count);
            foreach (string name in properties.Keys)
            {
                WriteString16(name);
                WriteString32(properties[name]);
            }
        }

        /// <summary>
        /// Reads a set of string name/value properties from the stream.
        /// </summary>
        /// <param name="comparer">The specific <see cref="StringComparer" /> to be used or <c>null</c> for the default comparer.</param>
        /// <returns>The set of properties.</returns>
        public Dictionary<string, string> ReadProperties(StringComparer comparer)
        {
            Dictionary<string, string>  props;
            int                         count;

            count = (ushort)ReadInt16();
            props = new Dictionary<string, string>(comparer);

            for (int i = 0; i < count; i++)
            {
                string name;
                string value;

                name = ReadString16();
                value = ReadString32();

                props[name] = value;
            }

            return props;
        }

        /// <summary>
        /// Copies bytes from this stream to another stream.
        /// </summary>
        /// <param name="output">The output stream.</param>
        /// <param name="cb">The number of bytes to copy or <b>-1</b> to copy to the EOF.</param>
        /// <returns>The number of bytes actually copied.</returns>
        /// <remarks>
        /// <para>
        /// Copying starts at the current position of this stream
        /// and the positions of both streams will be advanced 
        /// as copy proceeds.
        /// </para>
        /// <para>
        /// Pass int.MaxValue to copy the remainder of the stream
        /// to the output (up to 2GB).
        /// </para>
        /// </remarks>
        public new int CopyTo(Stream output, int cb)
        {
            byte[]  buf = new byte[CopyBufSize];
            int     cbRemain;
            int     cbRead;
            int     cbTotal;

            if (cb < 0)
                cb = int.MaxValue;

            cbTotal  = 0;
            cbRemain = cb;

            while (cbRemain > 0)
            {
                cb = cbRemain;
                if (cb >= CopyBufSize)
                    cb = CopyBufSize;

                cbRead = this.Read(buf, 0, cb);
                if (cbRead == 0)
                {
                    // We've reached the end of the input

                    output.Write(buf, 0, cbRead);
                    return cbTotal + cbRead;
                }

                output.Write(buf, 0, cbRead);

                cbTotal += cbRead;
                cbRemain -= cbRead;
            }

            return cbTotal;
        }

        /// <summary>
        /// Copies bytes from another stream to this stream.
        /// </summary>
        /// <param name="input">The input stream.</param>
        /// <param name="cb">The number of bytes to copy or <b>-1</b> from the EOF.</param>
        /// <returns>The number of bytes actually copied.</returns>
        /// <remarks>
        /// <para>
        /// Copying starts at the current position of this stream
        /// and the positions of both streams will be advanced 
        /// as copy proceeds.
        /// </para>
        /// <para>
        /// Pass int.MaxValue to copy the remainder of the stream
        /// to the this stream (up to 2GB).
        /// </para>
        /// </remarks>
        public int CopyFrom(Stream input, int cb)
        {

            byte[]  buf = new byte[CopyBufSize];
            int     cbRemain;
            int     cbRead;
            int     cbTotal;

            if (cb < 0)
                cb = int.MaxValue;

            cbTotal  = 0;
            cbRemain = cb;

            while (cbRemain > 0)
            {

                cb = cbRemain;
                if (cb >= CopyBufSize)
                    cb = CopyBufSize;

                cbRead = input.Read(buf, 0, cb);
                if (cbRead == 0)
                {
                    // We've reached the end of the input

                    this.Write(buf, 0, cbRead);
                    return cbTotal + cbRead;
                }

                this.Write(buf, 0, cbRead);

                cbTotal += cbRead;
                cbRemain -= cbRead;
            }

            return cbTotal;
        }

#if !MOBILE_DEVICE

        /// <summary>
        /// Reads all bytes from the current position to the end of the stream and
        /// converts them to text using the specified encoding.
        /// </summary>
        /// <param name="encoding">The text encoding.</param>
        /// <returns>The text read.</returns>
        public string ReadAllText(Encoding encoding)
        {
            byte[]  buffer;
            long    cb;

            cb = this.Length - this.Position;
            if (cb > int.MaxValue)
                throw new InvalidDataException("Stream is too large.");

            buffer = new byte[(int)cb];
            Read(buffer, 0, buffer.Length);

            return encoding.GetString(buffer);
        }

#endif

        /// <summary>
        /// Flushes any buffers.
        /// </summary>
        /// <remarks>
        /// This is a NOP for this implementation.
        /// </remarks>
        public override void Flush()
        {
            s.Flush();
        }

        /// <summary>
        /// Closes the stream.
        /// </summary>
        public override void Close()
        {
            s.Close();
        }
    }
}
