//-----------------------------------------------------------------------------
// FILE:        Extensions.cs
// CONTRIBUTOR: Jeff Lill
// COPYRIGHT:   Copyright (c) 2005-2015 by Jeffrey Lill.  All rights reserved.
// DESCRIPTION: Implements web related class extension methods.

using System;
using System.IO;
using System.Net;
using System.Web;
using System.Text;
using System.Text.RegularExpressions;
using System.Collections.Generic;

using LillTek.Common;
using LillTek.Advanced;

namespace LillTek.Web
{
    /// <summary>
    /// Implements web related class extension methods.
    /// </summary>
    public static class Extensions
    {
        /// <summary>
        /// Writes the contents of a file directly to the output of an HTTP response.
        /// </summary>
        /// <param name="response">The HTTP response.</param>
        /// <param name="path">Path to the file.</param>
        public static void WriteFile(this HttpResponse response, string path)
        {
            // $todo(jeff.lill): Investigate whether calling the WriteFile(handle) method would be faster

            response.BinaryWrite(File.ReadAllBytes(path));
        }
    }
}
