// TarException.cs
//
// Copyright 2004 John Reilly
//
// This program is free software; you can redistribute it and/or
// modify it under the terms of the GNU General Public License
// as published by the Free Software Foundation; either version 2
// of the License, or (at your option) any later version.
//
// This program is distributed in the hope that it will be useful,
// but WITHOUT ANY WARRANTY; without even the implied warranty of
// MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE.  See the
// GNU General Public License for more details.
//
// You should have received a copy of the GNU General Public License
// along with this program; if not, write to the Free Software
// Foundation, Inc., 59 Temple Place - Suite 330, Boston, MA  02111-1307, USA.
//
// Linking this library statically or dynamically with other modules is
// making a combined work based on this library.  Thus, the terms and
// conditions of the GNU General Public License cover the whole
// combination.
// 
// As a special exception, the copyright holders of this library give you
// permission to link this library with independent modules to produce an
// executable, regardless of the license terms of these independent
// modules, and to copy and distribute the resulting executable under
// terms of your choice, provided that you also meet, for each linked
// independent module, the terms and conditions of the license of that
// module.  An independent module is a module which is not derived from
// or based on this library.  If you modify this library, you may extend
// this exception to your version of the library, but you are not
// obligated to do so.  If you do not wish to do so, delete this
// exception statement from your version.

// LillTek.com: Minor changes to the source files to change the namespace
//              to LillTek.Compression and otherwise make them suitable
//              for Windows/CE builds.
//
//              Copyright (c) 2005-2015 by Jeffrey Lill. All rights reserved.

using System;

#if !COMPACT_FRAMEWORK_V10 && !COMPACT_FRAMEWORK_V20
using System.Runtime.Serialization;
#endif

using LillTek.Compression;

namespace LillTek.Compression.Tar {
	
	/// <summary>
	/// TarExceptions are used for exceptions specific to tar classes and code.	
    /// </summary>
#if !COMPACT_FRAMEWORK_V10 && !COMPACT_FRAMEWORK_V20 && !MOBILE_DEVICE
	[Serializable]
#endif
    public class TarException : SharpZipBaseException
	{
#if !COMPACT_FRAMEWORK_V10 && !COMPACT_FRAMEWORK_V20 && !MOBILE_DEVICE
		/// <summary>
		/// Deserialization constructor 
		/// </summary>
		/// <param name="info"><see cref="SerializationInfo"/> for this constructor</param>
		/// <param name="context"><see cref="StreamingContext"/> for this constructor</param>
		protected TarException(SerializationInfo info, StreamingContext context)
			: base(info, context)

		{
		}
#endif

		/// <summary>
		/// Initialises a new instance of the TarException class.
		/// </summary>
		public TarException()
		{
		}
		
		/// <summary>
		/// Initialises a new instance of the TarException class with a specified message.
		/// </summary>
		/// <param name="message">The message that describes the error.</param>
		public TarException(string message)
			: base(message)
		{
		}

		/// <summary>
		/// 
		/// </summary>
		/// <param name="message">A message describing the error.</param>
		/// <param name="exception">The exception that is the cause of the current exception.</param>
		public TarException(string message, Exception exception)
			: base(message, exception)
		{
		}
	}
}
