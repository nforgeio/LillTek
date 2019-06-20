//-----------------------------------------------------------------------------
// FILE:        Build.cs
// CONTRIBUTOR: Jeff Lill
// COPYRIGHT:   Copyright (c) 2005-2015 by Jeffrey Lill.  All rights reserved.
// DESCRIPTION: Defines the build related constants.

using System;

namespace LillTek 
{
    /// <summary>
    /// LillTek build constants.
    /// </summary>
    public static partial class Build 
    {	
        /// <summary>
        /// The build version for all LillTek assemblies.
        /// </summary>
        /// <remarks>
        /// <para>
        /// .NET version numbers are formatted in four parts:
        /// </para>
        /// <example>
        /// <![CDATA[<major>.<minor>.<build>.<revision>
        /// ]]>
        /// </example>
        /// <para>
        /// Note that the <b>major</b> version number matches the version of the
        /// targeted .NET Framework, the <b>minor</b> version number is incremented
        /// infrequently when there are large and potentially disruptive changes
        /// to the platform.  The <b>build</b> number idenfies the <b>Subversion
        /// revision</b> for the working copy at the time of the build, and 
        /// <b>revision</b> is used for rare situations to identify temporary
        /// build that could not make it into the source repository for some
        /// reason.  This will typically be set to zero and is manually incremented
        /// for each build that was not checked in.
        /// </para>
        /// </remarks>
        public const string Version = "5." + Build.Revision + ".0";

        /// <summary>
        /// The company name to use for all LillTek assemblies.
        /// </summary>
        public const string Company = "Jeffrey Lill";

        /// <summary>
        /// The copyright statement to be included in all assemblies.
        /// </summary>
        public const string Copyright = "Copyright (c) 2005-2015 by Jeffrey Lill.  All rights reserved.";

        /// <summary>
        /// The product statement to be included in all assemblies.
        /// </summary>
        public const string Product = "LillTek Platform";

        /// <summary>
        /// The build configuration.
        /// </summary>
        public const string Configuration =
#if WINFULL
#if DEBUG
            "WINFULL(Debug)";
#else
            "WINFULL(Release)";
#endif
#endif

#if ANDROID
#if DEBUG
            "Android(Debug)";
#else
            "Android(Release)";
#endif
#endif

#if APPLE_IOS
#if DEBUG
            "iOS(Debug)";
#else
            "iOS(Release)";
#endif
#endif
    }
}
