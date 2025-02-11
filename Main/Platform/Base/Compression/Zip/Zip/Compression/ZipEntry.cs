// ZipEntry.cs
//
// Copyright (C) 2001 Mike Krueger
// Copyright (C) 2004 John Reilly
//
// This file was translated from java, it was part of the GNU Classpath
// Copyright (C) 2001 Free Software Foundation, Inc.
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
using System.IO;

namespace LillTek.Compression.Zip
{

	/// <summary>
	/// Defines known values for the <see cref="HostSystemID"/> property.
	/// </summary>
	public enum HostSystemID
	{
		/// <summary>
		/// Host system = MSDOS
		/// </summary>
		Msdos = 0,
		/// <summary>
		/// Host system = Amiga
		/// </summary>
		Amiga = 1,
		/// <summary>
		/// Host system = Open VMS
		/// </summary>
		OpenVms = 2,
		/// <summary>
		/// Host system = Unix
		/// </summary>
		Unix = 3,
		/// <summary>
		/// Host system = VMCms
		/// </summary>
		VMCms = 4,
		/// <summary>
		/// Host system = Atari ST
		/// </summary>
		AtariST = 5,
		/// <summary>
		/// Host system = OS2
		/// </summary>
		OS2 = 6,
		/// <summary>
		/// Host system = Macintosh
		/// </summary>
		Macintosh = 7,
		/// <summary>
		/// Host system = ZSystem
		/// </summary>
		ZSystem = 8,
		/// <summary>
		/// Host system = Cpm
		/// </summary>
		Cpm = 9,
		/// <summary>
		/// Host system = Windows NT
		/// </summary>
		WindowsNT = 10,
		/// <summary>
		/// Host system = MVS
		/// </summary>
		MVS = 11,
		/// <summary>
		/// Host system = VSE
		/// </summary>
		Vse = 12,
		/// <summary>
		/// Host system = Acorn RISC
		/// </summary>
		AcornRisc = 13,
		/// <summary>
		/// Host system = VFAT
		/// </summary>
		Vfat = 14,
		/// <summary>
		/// Host system = Alternate MVS
		/// </summary>
		AlternateMvs = 15,
		/// <summary>
		/// Host system = BEOS
		/// </summary>
		BeOS = 16,
		/// <summary>
		/// Host system = Tandem
		/// </summary>
		Tandem = 17,
		/// <summary>
		/// Host system = OS400
		/// </summary>
		OS400 = 18,
		/// <summary>
		/// Host system = OSX
		/// </summary>
		OSX = 19,
		/// <summary>
		/// Host system = WinZIP AES
		/// </summary>
		WinZipAES = 99,
	}
	
	/// <summary>
	/// This class represents an entry in a zip archive.  This can be a file
	/// or a directory
	/// ZipFile and ZipInputStream will give you instances of this class as 
	/// information about the members in an archive.  ZipOutputStream
	/// uses an instance of this class when creating an entry in a Zip file.
	/// <br/>
	/// <br/>Author of the original java version : Jochen Hoenicke
	/// </summary>
	public class ZipEntry : ICloneable
	{
		[Flags]
		enum Known : byte
		{
			None = 0,
			Size = 0x01,
			CompressedSize = 0x02,
			Crc = 0x04,
			Time = 0x08,
			ExternalAttributes = 0x10,
		}

		#region Instance Fields
		Known known;
		int    externalFileAttributes = -1;     // contains external attributes (O/S dependant)
		
		ushort versionMadeBy;					// Contains host system and version information
												// only relevant for central header entries
		
		string name;
		ulong  size;
		ulong  compressedSize;
		ushort versionToExtract;                // Version required to extract (library handles <= 2.0)
		uint   crc;
		uint   dosTime;
		
		CompressionMethod  method = CompressionMethod.Deflated;
		byte[] extra;
		string comment;
		
		int flags;                             // general purpose bit flags

		long zipFileIndex = -1;                // used by ZipFile
		long offset;                           // used by ZipFile and ZipOutputStream
		
		bool forceZip64_;
		byte cryptoCheckValue_;
		#endregion
		
		#region Constructors
		/// <summary>
		/// Creates a zip entry with the given name.
		/// </summary>
		/// <param name="name">
		/// The name for this entry. Can include directory components.
		/// The convention for names is 'unix' style paths with relative names only.
		/// There are with no device names and path elements are separated by '/' characters.
		/// </param>
		/// <exception cref="ArgumentNullException">
		/// The name passed is <c>null</c>
		/// </exception>
		public ZipEntry(string name)
			: this(name, 0, ZipConstants.VersionMadeBy, CompressionMethod.Deflated)
		{
		}

		/// <summary>
		/// Creates a zip entry with the given name and version required to extract
		/// </summary>
		/// <param name="name">
		/// The name for this entry. Can include directory components.
		/// The convention for names is 'unix'  style paths with no device names and 
		/// path elements separated by '/' characters.  This is not enforced see <see cref="CleanName(string)">CleanName</see>
		/// on how to ensure names are valid if this is desired.
		/// </param>
		/// <param name="versionRequiredToExtract">
		/// The minimum 'feature version' required this entry
		/// </param>
		/// <exception cref="ArgumentNullException">
		/// The name passed is <c>null</c>
		/// </exception>
		internal ZipEntry(string name, int versionRequiredToExtract)
			: this(name, versionRequiredToExtract, ZipConstants.VersionMadeBy,
			CompressionMethod.Deflated)
		{
		}
		
		/// <summary>
		/// Initializes an entry with the given name and made by information
		/// </summary>
		/// <param name="name">Name for this entry</param>
		/// <param name="madeByInfo">Version and HostSystem Information</param>
		/// <param name="versionRequiredToExtract">Minimum required zip feature version required to extract this entry</param>
		/// <param name="method">Compression method for this entry.</param>
		/// <exception cref="ArgumentNullException">
		/// The name passed is <c>null</c>
		/// </exception>
		/// <exception cref="ArgumentOutOfRangeException">
		/// versionRequiredToExtract should be 0 (auto-calculate) or > 10
		/// </exception>
		/// <remarks>
		/// This constructor is used by the ZipFile class when reading from the central header
		/// It is not generally useful, use the constructor specifying the name only.
		/// </remarks>
		internal ZipEntry(string name, int versionRequiredToExtract, int madeByInfo,
			CompressionMethod method)
		{
			if (name == null) {
				throw new System.ArgumentNullException("ZipEntry name");
			}

			if ( name.Length > 0xffff )	{
				throw new ArgumentException("Name is too long", "name");
			}

			if ( (versionRequiredToExtract != 0) && (versionRequiredToExtract < 10) ) {
				throw new ArgumentOutOfRangeException("versionRequiredToExtract");
			}
			
			this.DateTime = System.DateTime.Now;
			this.name = name;
			this.versionMadeBy = (ushort)madeByInfo;
			this.versionToExtract = (ushort)versionRequiredToExtract;
			this.method = method;
		}
		
		/// <summary>
		/// Creates a deep copy of the given zip entry.
		/// </summary>
		/// <param name="entry">
		/// The entry to copy.
		/// </param>
		[Obsolete("Use Clone instead")]
		public ZipEntry(ZipEntry entry)
		{
			if ( entry == null ) {
				throw new ArgumentNullException("entry");
			}

			known                  = entry.known;
			name                   = entry.name;
			size                   = entry.size;
			compressedSize         = entry.compressedSize;
			crc                    = entry.crc;
			dosTime                = entry.dosTime;
			method                 = entry.method;
			comment                = entry.comment;
			versionToExtract       = entry.versionToExtract;
			versionMadeBy          = entry.versionMadeBy;
			externalFileAttributes = entry.externalFileAttributes;
			flags                  = entry.flags;

			zipFileIndex           = entry.zipFileIndex;
			offset                 = entry.offset;

			forceZip64_			   = entry.forceZip64_;

			if ( entry.extra != null ) {
				extra = new byte[entry.extra.Length];
				Array.Copy(entry.extra, 0, extra, 0, entry.extra.Length);
			}
		}

		#endregion
		
		/// <summary>
		/// Get a value indicating wether the entry has a CRC value available.
		/// </summary>
		public bool HasCrc 
		{
			get {
				return (known & Known.Crc) != 0;
			}
		}

		/// <summary>
		/// Get/Set flag indicating if entry is encrypted.
		/// A simple helper routine to aid interpretation of <see cref="Flags">flags</see>
		/// </summary>
		public bool IsCrypted 
		{
			get {
				return (flags & 1) != 0; 
			}
			set {
				if (value) {
					flags |= 1;
				} 
				else {
					flags &= ~1;
				}
			}
		}

		/// <summary>
		/// Get / set a flag indicating wether entry name and comment text are
		/// encoded in Unicode UTF8
		/// </summary>
		public bool IsUnicodeText
		{
			get {
				return ( flags & (int)GeneralBitFlags.UnicodeText ) != 0;
			}
			set {
				if ( value ) {
					flags |= (int)GeneralBitFlags.UnicodeText;
				}
				else {
					flags &= ~(int)GeneralBitFlags.UnicodeText;
				}
			}
		}
		
		/// <summary>
		/// Value used during password checking for PKZIP 2.0 / 'classic' encryption.
		/// </summary>
		internal byte CryptoCheckValue
		{
			get {
				return cryptoCheckValue_;
			}

			set	{
				cryptoCheckValue_ = value;
			}
		}

		/// <summary>
		/// Get/Set general purpose bit flag for entry
		/// </summary>
		/// <remarks>
		/// General purpose bit flag<br/>
		/// Bit 0: If set, indicates the file is encrypted<br/>
		/// Bit 1-2 Only used for compression type 6 Imploding, and 8, 9 deflating<br/>
		/// Imploding:<br/>
		/// Bit 1 if set indicates an 8K sliding dictionary was used.  If clear a 4k dictionary was used<br/>
		/// Bit 2 if set indicates 3 Shannon-Fanno trees were used to encode the sliding dictionary, 2 otherwise<br/>
		/// <br/>
		/// Deflating:<br/>
		///   Bit 2    Bit 1<br/>
		///     0        0       Normal compression was used<br/>
		///     0        1       Maximum compression was used<br/>
		///     1        0       Fast compression was used<br/>
		///     1        1       Super fast compression was used<br/>
		/// <br/>
		/// Bit 3: If set, the fields crc-32, compressed size
		/// and uncompressed size are were not able to be written during zip file creation
		/// The correct values are held in a data descriptor immediately following the compressed data. <br/>
		/// Bit 4: Reserved for use by PKZIP for enhanced deflating<br/>
		/// Bit 5: If set indicates the file contains compressed patch data<br/>
		/// Bit 6: If set indicates strong encryption was used.<br/>
		/// Bit 7-15: Unused or reserved<br/>
		/// </remarks>
		public int Flags 
		{
			get { 
				return flags; 
			}
			set {
				flags = value; 
			}
		}

		/// <summary>
		/// Get/Set index of this entry in Zip file
		/// </summary>
		public long ZipFileIndex 
		{
			get {
				return zipFileIndex;
			}
			set {
				zipFileIndex = value;
			}
		}
		
		/// <summary>
		/// Get/set offset for use in central header
		/// </summary>
		public long Offset 
		{
			get {
				return offset;
			}
			set {
				offset = value;
			}
		}

		/// <summary>
		/// Get/Set external file attributes as an integer.
		/// The values of this are operating system dependant see
		/// <see cref="HostSystem">HostSystem</see> for details
		/// </summary>
		public int ExternalFileAttributes 
		{
			get {
				if ((known & Known.ExternalAttributes) == 0) {
					return -1;
				} 
				else {
					return externalFileAttributes;
				}
			}
			
			set {
				externalFileAttributes = value;
				known |= Known.ExternalAttributes;
			}
		}

		/// <summary>
		/// Get the version made by for this entry or zero if unknown.
		/// The value / 10 indicates the major version number, and 
		/// the value mod 10 is the minor version number
		/// </summary>
		public int VersionMadeBy 
		{
			get { 
				return (versionMadeBy & 0xff);
			}
		}

		/// <summary>
		/// Test the external attributes for this <see cref="ZipEntry"/> to
		/// see if the external attributes are Dos based (including WINNT and variants)
		/// and match the values
		/// </summary>
		/// <param name="attributes">The attributes to test.</param>
		/// <returns>Returns <c>true</c> if the external attributes are known to be DOS/Windows 
		/// based and have the same attributes set as the value passed.</returns>
		bool HasDosAttributes(int attributes)
		{
			bool result = false;
			if ( (known & Known.ExternalAttributes) != 0 ) {
				if ( ((HostSystem == (int)HostSystemID.Msdos) || 
					(HostSystem == (int)HostSystemID.WindowsNT)) && 
					(ExternalFileAttributes & attributes) == attributes) {
					result = true;
				}
			}
			return result;
		}

		/// <summary>
		/// Gets the compatability information for the <see cref="ExternalFileAttributes">external file attribute</see>
		/// If the external file attributes are compatible with MS-DOS and can be read
		/// by PKZIP for DOS version 2.04g then this value will be zero.  Otherwise the value
		/// will be non-zero and identify the host system on which the attributes are compatible.
		/// </summary>
		/// 		
		/// <remarks>
		/// The values for this as defined in the Zip File format and by others are shown below.  The values are somewhat
		/// misleading in some cases as they are not all used as shown.  You should consult the relevant documentation
		/// to obtain up to date and correct information.  The modified appnote by the infozip group is
		/// particularly helpful as it documents a lot of peculiarities.  The document is however a little dated.
		/// <list type="table">
		/// <item>0 - MS-DOS and OS/2 (FAT / VFAT / FAT32 file systems)</item>
		/// <item>1 - Amiga</item>
		/// <item>2 - OpenVMS</item>
		/// <item>3 - Unix</item>
		/// <item>4 - VM/CMS</item>
		/// <item>5 - Atari ST</item>
		/// <item>6 - OS/2 HPFS</item>
		/// <item>7 - Macintosh</item>
		/// <item>8 - Z-System</item>
		/// <item>9 - CP/M</item>
		/// <item>10 - Windows NTFS</item>
		/// <item>11 - MVS (OS/390 - Z/OS)</item>
		/// <item>12 - VSE</item>
		/// <item>13 - Acorn Risc</item>
		/// <item>14 - VFAT</item>
		/// <item>15 - Alternate MVS</item>
		/// <item>16 - BeOS</item>
		/// <item>17 - Tandem</item>
		/// <item>18 - OS/400</item>
		/// <item>19 - OS/X (Darwin)</item>
		/// <item>99 - WinZip AES</item>
		/// <item>remainder - unused</item>
		/// </list>
		/// </remarks>
		public int HostSystem 
		{
			get {
				return (versionMadeBy >> 8) & 0xff; 
			}

			set {
				versionMadeBy &= 0xff;
				versionMadeBy |= (ushort)((value & 0xff) << 8);
			}
		}
		
		/// <summary>
		/// Get minimum Zip feature version required to extract this entry
		/// </summary>		
		/// <remarks>
		/// Minimum features are defined as:<br/>
		/// 1.0 - Default value<br/>
		/// 1.1 - File is a volume label<br/>
		/// 2.0 - File is a folder/directory<br/>
		/// 2.0 - File is compressed using Deflate compression<br/>
		/// 2.0 - File is encrypted using traditional encryption<br/>
		/// 2.1 - File is compressed using Deflate64<br/>
		/// 2.5 - File is compressed using PKWARE DCL Implode<br/>
		/// 2.7 - File is a patch data set<br/>
		/// 4.5 - File uses Zip64 format extensions<br/>
		/// 4.6 - File is compressed using BZIP2 compression<br/>
		/// 5.0 - File is encrypted using DES<br/>
		/// 5.0 - File is encrypted using 3DES<br/>
		/// 5.0 - File is encrypted using original RC2 encryption<br/>
		/// 5.0 - File is encrypted using RC4 encryption<br/>
		/// 5.1 - File is encrypted using AES encryption<br/>
		/// 5.1 - File is encrypted using corrected RC2 encryption<br/>
		/// 5.1 - File is encrypted using corrected RC2-64 encryption<br/>
		/// 6.1 - File is encrypted using non-OAEP key wrapping<br/>
		/// 6.2 - Central directory encryption (not confirmed yet)<br/>
		/// 6.3 - File is compressed using LZMA<br/>
		/// 6.3 - File is compressed using PPMD+<br/>
		/// 6.3 - File is encrypted using Blowfish<br/>
		/// 6.3 - File is encrypted using Twofish<br/>
		/// </remarks>
		public int Version 
		{
			get {
				// Return recorded version if known.
				if (versionToExtract != 0) {
					return versionToExtract;
				} 
				else {
					int result = 10;
					if ( LocalHeaderRequiresZip64 ) {
						result = ZipConstants.VersionZip64;	
					}
					else if (CompressionMethod.Deflated == method) {
						result = 20;
					} 
					else if (IsDirectory == true) {
						result = 20;
					} 
					else if (IsCrypted == true) {
						result = 20;
					} 
					else if (HasDosAttributes(0x08) ) {
						result = 11;
					}
					return result;
				}
			}
		}

		/// <summary>
		/// Get a value indicating wether this entry can be decompressed by the library.
		/// </summary>
		public bool CanDecompress
		{
			get {
				return (Version <= ZipConstants.VersionMadeBy) &&
					((Version == 10) ||
					(Version == 11) ||
					(Version == 20) ||
					(Version == 45)) &&
					IsCompressionMethodSupported();
			}
		}

		/// <summary>
		/// Force this entry to be recorded using Zip64 extensions.
		/// </summary>
		public void ForceZip64()
		{
			forceZip64_ = true;
		}
		
		/// <summary>
		/// Get a value indicating wether Zip64 extensions were forced.
		/// </summary>
		/// <returns></returns>
		public bool IsZip64Forced()
		{
			return forceZip64_;
		}

		/// <summary>
		/// Gets a value indicating if the entry requires Zip64 extensions 
		/// to store the full entry values.
		/// </summary>
		public bool LocalHeaderRequiresZip64 
		{
			get {
				bool result = forceZip64_;

				if ( !result ) {
					ulong trueCompressedSize = compressedSize;

					if ( (versionToExtract == 0) && IsCrypted ) {
						trueCompressedSize += ZipConstants.CryptoHeaderSize;
					}

					// TODO: A better estimation of the true limit based on compression overhead should be used
					// to determine when an entry should use Zip64.
					result = ((this.size >= uint.MaxValue) || (trueCompressedSize >= uint.MaxValue)) &&
						((versionToExtract == 0) || (versionToExtract >= ZipConstants.VersionZip64));
				}

				return result;
			}
		}
		
		/// <summary>
		/// Get a value indicating wether the central directory entry requires Zip64 extensions to be stored.
		/// </summary>
		public bool CentralHeaderRequiresZip64
		{
			get {
				return LocalHeaderRequiresZip64 || (offset >= 0xffffffff);
			}
		}
		
		/// <summary>
		/// Get/Set DosTime
		/// </summary>		
		public long DosTime 
		{
			get {
				if ((known & Known.Time) == 0) {
					return 0;
				} 
				else {
					return dosTime;
				}
			}
			set {
				this.dosTime = (uint)value;
				known |= Known.Time;
			}
		}
			
		/// <summary>
		/// Gets/Sets the time of last modification of the entry.
		/// </summary>
		public DateTime DateTime 
		{
			get {
				// Although technically not valid some archives have dates set to zero.
				// This mimics some archivers handling and is a good a cludge as any probably.
				if ( dosTime == 0 ) {
					return DateTime.Now;
				}
				else {
					uint sec  = 2 * (dosTime & 0x1f);
					uint min  = (dosTime >> 5) & 0x3f;
					uint hrs  = (dosTime >> 11) & 0x1f;
					uint day  = (dosTime >> 16) & 0x1f;
					uint mon  = ((dosTime >> 21) & 0xf);
					uint year = ((dosTime >> 25) & 0x7f) + 1980;
					return new System.DateTime((int)year, (int)mon, (int)day, (int)hrs, (int)min, (int)sec);
				}
			}
			set {
				DosTime = ((uint)value.Year - 1980 & 0x7f) << 25 | 
					((uint)value.Month) << 21 |
					((uint)value.Day) << 16 |
					((uint)value.Hour) << 11 |
					((uint)value.Minute) << 5 |
					((uint)value.Second) >> 1;
			}
		}
		
		/// <summary>
		/// Returns the entry name.  The path components in the entry should
		/// always separated by slashes ('/').  Dos device names like C: should also
		/// be removed.  See the <see cref="ZipNameTransform"/> class, or <see cref="CleanName(string)"/>
		/// </summary>
		public string Name 
		{
			get {
				return name;
			}
		}
		
		/// <summary>
		/// Gets/Sets the size of the uncompressed data.
		/// </summary>
		/// <returns>
		/// The size or <b>-1</b> if unknown.
		/// </returns>
		public long Size 
		{
			get {
				return (known & Known.Size) != 0 ? (long)size : -1L;
			}
			set {
				this.size  = (ulong)value;
				this.known |= Known.Size;
			}
		}
		
		/// <summary>
		/// Gets/Sets the size of the compressed data.
		/// </summary>
		/// <returns>
		/// The compressed entry size or <b>-1</b> if unknown.
		/// </returns>
		public long CompressedSize 
		{
			get {
				return (known & Known.CompressedSize) != 0 ? (long)compressedSize : -1L;
			}
			set {
				this.compressedSize = (ulong)value;
				this.known |= Known.CompressedSize;
			}
		}

		/// <summary>
		/// Gets/Sets the crc of the uncompressed data.
		/// </summary>
		/// <exception cref="System.ArgumentOutOfRangeException">
		/// Crc is not in the range 0..0xffffffffL
		/// </exception>
		/// <returns>
		/// The crc value or <b>-1</b> if unknown.
		/// </returns>
		public long Crc 
		{
			get {
				return (known & Known.Crc) != 0 ? crc & 0xffffffffL : -1L;
			}
			set {
				if (((ulong)crc & 0xffffffff00000000L) != 0) {
					throw new ArgumentOutOfRangeException("value");
				}
				this.crc = (uint)value;
				this.known |= Known.Crc;
			}
		}
		
		/// <summary>
		/// Gets/Sets the compression method. Only Deflated and Stored are supported.
		/// </summary>
		/// <returns>
		/// The compression method for this entry
		/// </returns>
		/// <see cref="LillTek.Compression.Zip.CompressionMethod.Deflated"/>
		/// <see cref="LillTek.Compression.Zip.CompressionMethod.Stored"/>
		public CompressionMethod CompressionMethod {
			get {
				return method;
			}

			set {
				if ( !IsCompressionMethodSupported(value) ) {
					throw new NotSupportedException("Compression method not supported");
				}
				this.method = value;
			}
		}
		
		/// <summary>
		/// Gets/Sets the extra data.
		/// </summary>
		/// <exception cref="System.ArgumentOutOfRangeException">
		/// Extra data is longer than 64KB (0xffff) bytes.
		/// </exception>
		/// <returns>
        /// Extra data or <c>null</c> if not set.
		/// </returns>
		public byte[] ExtraData {
			
			get {
// TODO: This is safer but less efficient.  Think about wether it should change.
//				return (byte[]) extra.Clone();
				return extra;
			}

			set {
				if (value == null) {
					this.extra = null;
				}
				else {
					if (value.Length > 0xffff) {
						throw new System.ArgumentOutOfRangeException("value");
					}
				
					this.extra = new byte[value.Length];
					Array.Copy(value, 0, this.extra, 0, value.Length);
				}
			}
		}
		
		/// <summary>
		/// Process extra data fields updating the entry based on the contents.
		/// </summary>
		/// <param name="localHeader"><c>true</c> if the extra data fields should be handled
		/// for a local header, rather than for a central header.
		/// </param>
		internal void ProcessExtraData(bool localHeader)
		{
			ZipExtraData extraData = new ZipExtraData(this.extra);

			if ( extraData.Find(0x0001) ) {
				if ( (versionToExtract & 0xff) < ZipConstants.VersionZip64 ) {
					throw new ZipException("Zip64 Extended information found but version is not valid");
				}

				// The recorded size will change but remember that this is zip64.
				forceZip64_ = true;

				if ( extraData.ValueLength < 4 ) {
					throw new ZipException("Extra data extended Zip64 information length is invalid");
				}

				if ( localHeader || (size == uint.MaxValue) ) {
					size = (ulong)extraData.ReadLong();
				}

				if ( localHeader || (compressedSize == uint.MaxValue) ) {
					compressedSize = (ulong)extraData.ReadLong();
				}
			}
			else {
				if ( 
					((versionToExtract & 0xff) >= ZipConstants.VersionZip64) &&
					( (size == uint.MaxValue) ||
					(compressedSize == uint.MaxValue) )) {
					throw new ZipException("Zip64 Extended information required but is missing.");
				}
			}

/* TODO: Testing for handling of windows extra data
			if ( extraData.Find(10) ) {
				// No room for any tags.
				if ( extraData.ValueLength < 8 ) {
					throw new ZipException("NTFS Extra data invalid");
				}

				extraData.ReadInt(); // Reserved

				while ( extraData.UnreadCount >= 4 ) {
					int ntfsTag = extraData.ReadShort();
					int ntfsLength = extraData.ReadShort();
					if ( ntfsTag == 1 ) {
						if ( ntfsLength >= 24 ) {
							long lastModification = extraData.ReadLong();
							long lastAccess = extraData.ReadLong();
							long createTime = extraData.ReadLong();

							DateTime = System.DateTime.FromFileTime(lastModification);
						}
						break;
					}
					else {
						// An unknown NTFS tag so simply skip it.
						extraData.Skip(ntfsLength);
					}
				}
			}
			else 
*/			
			if ( extraData.Find(0x5455) ) {
				int length = extraData.ValueLength;	
				int flags = extraData.ReadByte();
					
				// Can include other times but these are ignored.  Length of data should
				// actually be 1 + 4 * no of bits in flags.
				if ( ((flags & 1) != 0) && (length >= 5) ) {
					int iTime = extraData.ReadInt();

					DateTime = (new System.DateTime ( 1970, 1, 1, 0, 0, 0 ).ToUniversalTime() +
						new TimeSpan ( 0, 0, 0, iTime, 0 )).ToLocalTime();
				}
			}
		}

		/// <summary>
		/// Gets/Sets the entry comment.
		/// </summary>
		/// <exception cref="System.ArgumentOutOfRangeException">
		/// If comment is longer than 0xffff.
		/// </exception>
		/// <returns>
        /// The comment or <c>null</c> if not set.
		/// </returns>
		/// <remarks>
		/// A comment is only available for entries when read via the <see cref="ZipFile"/> class.
		/// The <see cref="ZipInputStream"/> class doesnt have the comment data available.
		/// </remarks>
		public string Comment {
			get {
				return comment;
			}
			set {
				// This test is strictly incorrect as the length is in characters
				// while the storage limit is in bytes.
				// While the test is partially correct in that a comment of this length or greater 
				// is definitely invalid, shorter comments may also have an invalid length
				// where there are multi-byte characters
				// The full test is not possible here however as the code page to apply conversions with
				// isnt available.
				if ( (value != null) && (value.Length > 0xffff) ) {
#if COMPACT_FRAMEWORK_V10
					throw new ArgumentOutOfRangeException("value");
#else
					throw new ArgumentOutOfRangeException("value", "cannot exceed 65535");
#endif
				}
				
				comment = value;
			}
		}
		
		/// <summary>
		/// Gets a value indicating if the entry is a directory.
		/// however.
		/// </summary>
		/// <remarks>
		/// A directory is determined by an entry name with a trailing slash '/'.
		/// The external file attributes can also indicate an entry is for a directory.
		/// Currently only dos/windows attributes are tested in this manner.
		/// The trailing slash convention should always be followed.
		/// </remarks>
		public bool IsDirectory 
		{
			get {
				int nameLength = name.Length;
				bool result = 
					((nameLength > 0) && 
					((name[nameLength - 1] == '/') || (name[nameLength - 1] == '\\'))) ||
					HasDosAttributes(16)
					;
				return result;
			}
		}
		
		/// <summary>
		/// Get a value of <c>true</c> if the entry appears to be a file; <c>false</c> otherwise
		/// </summary>
		/// <remarks>
		/// This only takes account of DOS/Windows attributes.  Other operating systems are ignored.
		/// For linux and others the result may be incorrect.
		/// </remarks>
		public bool IsFile
		{
			get {
				return !IsDirectory && !HasDosAttributes(8);
			}
		}
		
		/// <summary>
		/// Test entry to see if data can be extracted.
		/// </summary>
		/// <returns>Returns <c>true</c> if data can be extracted for this entry; <c>false</c> otherwise.</returns>
		public bool IsCompressionMethodSupported()
		{
			return IsCompressionMethodSupported(CompressionMethod);
		}
		
		#region ICloneable Members
		/// <summary>
		/// Creates a copy of this zip entry.
		/// </summary>
		public object Clone()
		{
			ZipEntry result = (ZipEntry)this.MemberwiseClone();

			if ( extra != null ) {
				result.extra = new byte[extra.Length];
				Array.Copy(result.extra, 0, extra, 0, extra.Length);
			}

			return result;
		}
		
		#endregion

		/// <summary>
		/// Gets the string representation of this ZipEntry.
		/// </summary>
		public override string ToString()
		{
			return name;
		}

		/// <summary>
		/// Test a <see cref="CompressionMethod">compression method</see> to see if this library
		/// supports extracting data compressed with that method
		/// </summary>
		/// <param name="method">The compression method to test.</param>
		/// <returns>Returns <c>true</c> if the compression method is supported; <c>false</c> otherwise</returns>
		public static bool IsCompressionMethodSupported(CompressionMethod method)
		{
			return
				( method == CompressionMethod.Deflated ) ||
				( method == CompressionMethod.Stored );
		}
		
		/// <summary>
		/// Cleans a name making it conform to Zip file conventions.
		/// Devices names ('c:\') and UNC share names ('\\server\share') are removed
		/// and forward slashes ('\') are converted to back slashes ('/').
		/// Names are made relative by trimming leading slashes which is compatible
		/// with the ZIP naming convention.
		/// </summary>
		/// <param name="name">Name to clean</param>
		public static string CleanName(string name)
		{
			if (name == null) {
				return string.Empty;
			}
			
			if (Path.IsPathRooted(name) == true) {
				// NOTE:
				// for UNC names...  \\machine\share\zoom\beet.txt gives \zoom\beet.txt
				name = name.Substring(Path.GetPathRoot(name).Length);
			}

			name = name.Replace(@"\", "/");
			
			while ( (name.Length > 0) && (name[0] == '/')) {
				name = name.Remove(0, 1);
			}
			return name;
		}
	}
}
