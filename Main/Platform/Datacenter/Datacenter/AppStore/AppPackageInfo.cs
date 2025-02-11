//-----------------------------------------------------------------------------
// FILE:        AppPackageInfo.cs
// CONTRIBUTOR: Jeff Lill
// COPYRIGHT:   Copyright (c) 2005-2015 by Jeffrey Lill. All rights reserved.
// DESCRIPTION: Holds information about an application package.

using System;

using LillTek.Common;

namespace LillTek.Datacenter
{
    /// <summary>
    /// Holds information about an application package.
    /// </summary>
    public sealed class AppPackageInfo
    {
        //---------------------------------------------------------------------
        // Static methods

        /// <summary>
        /// Deserializes an instance from a string generated by <see cref="ToString" />.
        /// </summary>
        /// <param name="serialized">The serialized string.</param>
        /// <returns>The parsed <see cref="AppPackageInfo" /> instance.</returns>
        /// <remarks>
        /// <note>
        /// The <see cref="FullPath" /> property is not serialized
        /// by <see cref="ToString" /> and will return as <c>null</c>.
        /// </note>
        /// </remarks>
        public static AppPackageInfo Parse(string serialized)
        {
            return new AppPackageInfo(serialized);
        }

        //---------------------------------------------------------------------
        // Instance methods

        /// <summary>
        /// The application package reference URI.
        /// </summary>
        public readonly AppRef AppRef;

        /// <summary>
        /// The application package file name.
        /// </summary>
        public readonly string FileName;

        /// <summary>
        /// The fully qualified path to this package on the local machine.
        /// This field is not serialized and will be set to <c>null</c>
        /// in <see cref="AppPackageInfo" /> instances parsed from a string.
        /// </summary>
        public readonly string FullPath;

        /// <summary>
        /// The last write time (UTC) of the local package file.
        /// This field is not serialized and will be set to <see cref="DateTime.MinValue" />
        /// in <see cref="AppPackageInfo" /> instances parsed from a string.
        /// </summary>
        public readonly DateTime WriteTimeUtc;

        /// <summary>
        /// The package's MD5 hash.
        /// </summary>
        public readonly byte[] MD5;

        /// <summary>
        /// The package size in bytes.
        /// </summary>
        public readonly int Size;

        /// <summary>
        /// Deserializes an instance from a string generated by <see cref="ToString" />.
        /// </summary>
        /// <param name="serialized">The serialized string.</param>
        /// <remarks>
        /// <note>
        /// The <see cref="FullPath" /> property is not serialized
        /// by <see cref="ToString" /> and will return as <c>null</c>.
        /// </note>
        /// </remarks>
        public AppPackageInfo(string serialized)
        {
            ArgCollection args = ArgCollection.Parse(serialized, '=', '\t');

            this.AppRef       = new AppRef(args["ref"]);
            this.MD5          = Helper.FromHex(args["md5"]);
            this.Size         = int.Parse(args["size"]);
            this.FileName     = this.AppRef.FileName;
            this.FullPath     = null;
            this.WriteTimeUtc = DateTime.MinValue;
        }

        /// <summary>
        /// Constructor.
        /// </summary>
        /// <param name="appRef">The application package reference URI.</param>
        /// <param name="fileName">The application package file name.</param>
        /// <param name="fullPath">The fully qualified path to this package on the local machine.</param>
        /// <param name="md5">The package's MD5 hash.</param>
        /// <param name="size">The package size in bytes.</param>
        /// <param name="writeTimeUtc">The last write time (UTC) for the package file.</param>
        public AppPackageInfo(AppRef appRef, string fileName, string fullPath, byte[] md5, int size, DateTime writeTimeUtc)
        {
            this.AppRef       = appRef;
            this.FileName     = fileName;
            this.FullPath     = fullPath;
            this.MD5          = md5;
            this.Size         = size;
            this.WriteTimeUtc = writeTimeUtc;
        }

        /// <summary>
        /// Renders the package information into a string suitable for
        /// persisting across a network.
        /// </summary>
        /// <returns>The serialized instance.</returns>
        /// <remarks>
        /// Use <see cref="AppPackageInfo(string)" /> or <see cref="Parse" /> to 
        /// unserialize this information.  Note that the <see cref="FullPath" />
        /// and <see cref="WriteTimeUtc" /> properties will not be persisted
        /// in the result string.
        /// </remarks>
        public override string ToString()
        {
            var args = new ArgCollection('=', '\t');

            args["ref"]  = AppRef.ToString();
            args["md5"]  = Helper.ToHex(MD5);
            args["size"] = Size.ToString();

            return args.ToString();
        }
    }
}
