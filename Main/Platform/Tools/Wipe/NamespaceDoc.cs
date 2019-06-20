//-----------------------------------------------------------------------------
// FILE:        Program.cs
// CONTRIBUTOR: Jeff Lill
// COPYRIGHT:   Copyright (c) 2005-2015 by Jeffrey Lill. All rights reserved.
// DESCRIPTION: Implements the File Wipe tool.

using System;
using System.IO;
using System.Text;
using System.Threading;
using System.Collections;
using System.Collections.Generic;

namespace LillTek.Tools.Wipe
{
    /// <summary>
    /// Implements the File Wipe tool.
    /// </summary>
    /// <remarks>
    /// <code language="none">
    /// Writes across the extent of the file(s) specified and then
    /// deletes the file(s).
    /// 
    /// usage: wipe [-r] &lt;file&gt;
    /// 
    ///     &lt;file&gt;  the folder and name of the file to be wiped.  The folder
    ///             is optional.  The current folder will be assumed if this
    ///             is not present.  The file name may include the (*) and (?)
    ///             wildcard characters.
    ///             
    ///                 -r      indicates that the &lt;file&gt; pattern should be applied
    ///                         recursively to subfolders
    /// </code>
    /// </remarks>
    public static class OverviewDoc
    {
    }

}

