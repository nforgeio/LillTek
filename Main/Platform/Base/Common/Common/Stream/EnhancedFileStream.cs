//-----------------------------------------------------------------------------
// FILE:        EnhancedFileStream.cs
// CONTRIBUTOR: Jeff Lill
// COPYRIGHT:   Copyright (c) 2005-2015 by Jeffrey Lill.  All rights reserved.
// DESCRIPTION: Implements an enhanced file stream.

using System;
using System.IO;
using System.Text;

using Microsoft.Win32.SafeHandles;

// $todo(jeff.lill): Implement all [*Async()] method overrides.

namespace LillTek.Common
{
    /// <summary>
    /// Implements a buffered enhanced file stream.
    /// </summary>
    public sealed class EnhancedFileStream : EnhancedStream
    {
        private const int DefBufferSize = 8192;

#if !MOBILE_DEVICE

        /// <summary>
        /// Initializes a new instance of the FileStream class for the specified file handle, with the 
        /// specified read/write permission. 
        /// </summary>
        /// <param name="handle">
        /// A file handle for the file that the current <see cref="FileStream" /> object will encapsulate.
        /// </param>
        /// <param name="access">A <see cref="FileAccess" /> constant.</param>
        /// <remarks>
        /// See <see cref="FileStream(SafeFileHandle,FileAccess)" /> for more information.
        /// </remarks>
        public EnhancedFileStream(SafeFileHandle handle, FileAccess access)
            : base(new FileStream(handle, access, DefBufferSize))
        {
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="FileStream" /> class for the specified file handle, 
        /// with the specified read/write permission, and buffer size. 
        /// </summary>
        /// <param name="handle">
        /// A file handle for the file that the current <see cref="FileStream" /> object will encapsulate.
        /// </param>
        /// <param name="access">A <see cref="FileAccess" /> constant.</param>
        /// <param name="bufferSize">The desired buffer size.</param>
        /// <remarks>
        /// See <see cref="FileStream(SafeFileHandle,FileAccess,int)" /> for more information.
        /// </remarks>
        public EnhancedFileStream(SafeFileHandle handle, FileAccess access, int bufferSize)
            : base(new FileStream(handle, access, bufferSize))
        {
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="FileStream" /> class with the specified path and creation mode. 
        /// </summary>
        /// <param name="path">A relative or absolute path for the file that the current <see cref="FileStream" /> object will encapsulate. </param>
        /// <param name="mode">A <see cref="FileMode" /> constant that determines how to open or create the file.</param>
        /// <remarks>
        /// See <see cref="FileStream(string,FileMode)" /> for more information.
        /// </remarks>
        public EnhancedFileStream(string path, FileMode mode)
            : base(new BufferedStream(new FileStream(path, mode), DefBufferSize))
        {
        }

#endif // !MOBILE_DEVICE

#if !SILVERLIGHT

        /// <summary>
        /// Initializes a new instance of the <see cref="FileStream" /> class with the specified path, creation mode, 
        /// and read/write permission. 
        /// </summary>
        /// <param name="path">A relative or absolute path for the file that the current <see cref="FileStream" /> object will encapsulate.</param>
        /// <param name="mode">A <see cref="FileMode" /> constant that determines how to open or create the file.</param>
        /// <param name="access">A <see cref="FileAccess" /> constant.</param>
        /// <remarks>
        /// See <see cref="FileStream(string,FileMode,FileAccess)" /> for more information.
        /// </remarks>
        public EnhancedFileStream(string path, FileMode mode, FileAccess access)
            : base(new BufferedStream(new FileStream(path, mode, access), DefBufferSize))
        {
        }

#endif

        /// <summary>
        /// Initializes a new instance of the <see cref="FileStream" /> class with the specified path, 
        /// creation mode, read/write and sharing permission, and buffer size. 
        /// </summary>
        /// <param name="path">A relative or absolute path for the file that the current <see cref="FileStream" /> object will encapsulate.</param>
        /// <param name="mode">A <see cref="FileMode" /> constant that determines how to open or create the file.</param>
        /// <param name="access">A <see cref="FileAccess" /> constant.</param>
        /// <param name="share">A <see cref="FileShare" /> constant.</param>
        /// <param name="bufferSize">The desired buffer size.</param>
        /// <remarks>
        /// See <see cref="FileStream(string,FileMode,FileAccess,FileShare,int)" /> for more information.
        /// </remarks>
        public EnhancedFileStream(string path, FileMode mode, FileAccess access, FileShare share, int bufferSize)
            : base(new FileStream(path, mode, access, share, bufferSize))
        {
        }
    }
}
