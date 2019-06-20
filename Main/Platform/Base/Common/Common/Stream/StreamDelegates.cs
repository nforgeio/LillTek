//-----------------------------------------------------------------------------
// FILE:        StreamDelegates.cs
// CONTRIBUTOR: Jeff Lill
// COPYRIGHT:   Copyright (c) 2005-2015 by Jeffrey Lill.  All rights reserved.
// DESCRIPTION: Defines the stream delegates and related structures used to
//              manipulate EnhancedStreams from unmanaged code.

using System;
using System.IO;
using System.Text;
using System.Runtime.InteropServices;

namespace LillTek.Common
{
    /// <summary>
    /// Sets the current position of a stream as a 32-bit value, returning
    /// true on success.
    /// </summary>
    public delegate bool SetStreamPos32Delegate(int pos);

    /// <summary>
    /// Returns the current position a the stream as a 32-bit value.
    /// </summary>
    public delegate int GetStreamPos32Delegate();

    /// <summary>
    /// Sets the current position of a stream as a 64-bit value, returning
    /// true on success.
    /// </summary>
    public delegate bool SetStreamPos64Delegate(long pos);

    /// <summary>
    /// Returns the current position of a stream as a 64-bit value.
    /// </summary>
    public delegate long GetStreamPos64Delegate();

    /// <summary>
    /// Attempts to write bytes from the buffer passed to a stream, 
    /// returning the number of bytes written or <b>-1</b> if there was an error.
    /// </summary>
    public delegate int WriteStreamDelegate(IntPtr pBuf, int count);

    /// <summary>
    /// Attempts to read bytes from a stream returning the number of
    /// bytes read, 0 if the end of the stream has been reached or <b>-1</b>
    /// if there was an error.
    /// </summary>
    public delegate int ReadStreamDelegate(IntPtr pBuf, int count);

    /// <summary>
    /// Returns the length of a stream as a 32-bit value or <b>-1</b> if
    /// there was an error.
    /// </summary>
    public delegate int GetStreamLength32Delegate();

    /// <summary>
    /// Sets the length of a stream to a 32-bit value, returning true
    /// on success.
    /// </summary>
    public delegate bool SetStreamLength32Delegate(int length);

    /// <summary>
    /// Returns the length of a stream as a 64-bit value or <b>-1</b>
    /// if there was an error.
    /// </summary>
    public delegate long GetStreamLength64Delegate();

    /// <summary>
    /// Sets the length of a stream as a 64-bit value, returning true
    /// on success.
    /// </summary>
    public delegate bool SetStreamLength64Delegate(long length);

    /// <summary>
    /// Holds the generated delegates used to make unmanaged calls on an
    /// enhanced stream.
    /// </summary>
    public sealed class StreamDelegates
    {
        //---------------------------------------------------------------------
        // Public read-only member variables.

        /// <summary>
        /// Sets the current position of a stream as a 32-bit value.
        /// </summary>
        public readonly SetStreamPos32Delegate SetPos32;

        /// <summary>
        /// Returns the current position a the stream as a 32-bit value.
        /// </summary>
        public readonly GetStreamPos32Delegate GetPos32;

        /// <summary>
        /// Sets the current position of a stream as a 64-bit value.
        /// </summary>
        public readonly SetStreamPos64Delegate SetPos64;

        /// <summary>
        /// Returns the current position of a stream as a 64-bit value.
        /// </summary>
        public readonly GetStreamPos64Delegate GetPos64;

        /// <summary>
        /// Attempts to write bytes from the buffer passed to a stream, 
        /// returning the number of bytes written or <b>-1</b> if there was an error.
        /// </summary>
        public readonly WriteStreamDelegate Write;

        /// <summary>
        /// Attempts to read bytes from a stream returning the number of
        /// bytes read, 0 if the end of the stream has been reached or <b>-1</b>
        /// if there was an error.
        /// </summary>
        public readonly ReadStreamDelegate Read;

        /// <summary>
        /// Returns the length of a stream as a 32-bit value or <b>-1</b> if
        /// there was an error.
        /// </summary>
        public readonly GetStreamLength32Delegate GetLength32;

        /// <summary>
        /// Sets the length of a stream to a 32-bit value, returning true
        /// on success.
        /// </summary>
        public readonly SetStreamLength32Delegate SetLength32;

        /// <summary>
        /// Returns the length of a stream as a 64-bit value or <b>-1</b>
        /// if there was an error.
        /// </summary>
        public readonly GetStreamLength64Delegate GetLength64;

        /// <summary>
        /// Sets the length of a stream as a 64-bit value, returning true
        /// on success.
        /// </summary>
        public readonly SetStreamLength64Delegate SetLength64;

        private readonly Stream stream;     // The underlying stream

        /// <summary>
        /// Initializes the structure with the appropriate delegates for
        /// a stream.
        /// </summary>
        /// <param name="stream">The stream.</param>
        public StreamDelegates(Stream stream)
        {
            this.stream      = stream;
            this.SetPos32    = new SetStreamPos32Delegate(SetPos32Handler);
            this.GetPos32    = new GetStreamPos32Delegate(GetPos32Handler);
            this.SetPos64    = new SetStreamPos64Delegate(SetPos64Handler);
            this.GetPos64    = new GetStreamPos64Delegate(GetPos64Handler);
            this.Write       = new WriteStreamDelegate(WriteHandler);
            this.Read        = new ReadStreamDelegate(ReadHandler);
            this.GetLength32 = new GetStreamLength32Delegate(GetLength32Handler);
            this.SetLength32 = new SetStreamLength32Delegate(SetLength32Handler);
            this.GetLength64 = new GetStreamLength64Delegate(GetLength64Handler);
            this.SetLength64 = new SetStreamLength64Delegate(SetLength64Handler);
        }

        /// <summary>
        /// Returns the underlying stream.
        /// </summary>
        public Stream Stream
        {
            get { return stream; }
        }

        //---------------------------------------------------------------------
        // Callback handlers

        private bool SetPos32Handler(int pos)
        {
            try
            {
                stream.Position = pos;
                return true;
            }
            catch
            {
                return false;
            }
        }

        private int GetPos32Handler()
        {
            long pos;

            try
            {
                pos = stream.Position;
                if (pos > int.MaxValue)
                    return -1;

                return (int)pos;
            }
            catch
            {
                return -1;
            }
        }

        private bool SetPos64Handler(long pos)
        {
            try
            {
                stream.Position = pos;
                return true;
            }
            catch
            {
                return false;
            }
        }

        private long GetPos64Handler()
        {
            try
            {
                return stream.Position;
            }
            catch
            {
                return -1;
            }
        }

        private int WriteHandler(IntPtr pBuf, int count)
        {
            byte[] buf;

            try
            {
                buf = new byte[count];
                Marshal.Copy(pBuf, buf, 0, count);
                stream.Write(buf, 0, count);

                return count;
            }
            catch
            {
                return -1;
            }
        }

        private int ReadHandler(IntPtr pBuf, int count)
        {
            byte[] buf;

            try
            {
                buf = new byte[count];
                count = stream.Read(buf, 0, count);
                Marshal.Copy(buf, 0, pBuf, count);

                return count;
            }
            catch
            {
                return -1;
            }
        }

        private int GetLength32Handler()
        {
            long length;

            try
            {
                length = stream.Length;
                if (length > int.MaxValue)
                    return -1;

                return (int)length;
            }
            catch
            {
                return -1;
            }
        }

        private bool SetLength32Handler(int length)
        {
            try
            {
                stream.SetLength(length);
                return true;
            }
            catch
            {
                return false;
            }
        }

        private long GetLength64Handler()
        {
            try
            {
                return stream.Length;
            }
            catch
            {
                return -1;
            }
        }

        private bool SetLength64Handler(long length)
        {
            try
            {
                stream.SetLength(length);
                return true;
            }
            catch
            {
                return false;
            }
        }
    }
}
