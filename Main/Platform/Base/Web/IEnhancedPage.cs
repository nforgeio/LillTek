//-----------------------------------------------------------------------------
// FILE:        IEnhancedPage.cs
// CONTRIBUTOR: Jeff Lill
// COPYRIGHT:   Copyright (c) 2005-2015 by Jeffrey Lill.  All rights reserved.
// DESCRIPTION: The interface defining extensions to the standard ASP.NET 
//              Page class.

using System;
using System.IO;
using System.Collections.Generic;
using System.Web.UI;

using LillTek.Common;
using LillTek.Cryptography;

namespace LillTek.Web
{
    /// <summary>
    /// The interface defining extensions to the standard ASP.NET <see cref="Page" /> class.
    /// </summary>
    public interface IEnhancedPage
    {
        /// <summary>
        /// Returns the page's absolute virtual path (optionally adjusting for URI re-writing).
        /// </summary>
        /// <remarks>
        /// <para>
        /// This method differs from the <see cref="Page"/>.<see cref="TemplateControl.AppRelativeVirtualPath" />
        /// property in two ways: First this property always returns a path that begins with a forward
        /// slash (<b>/</b>) whereas <see cref="TemplateControl.AppRelativeVirtualPath" /> will begin with a tilda
        /// (<b>~</b>) and second, pages that implement this interface may customize the path returned
        /// to harmonize with the site's URL re-writing scheme (if any).
        /// </para>
        /// <note>
        /// Pages that need to implement this interface for other reasons, can simply implement this
        /// interface as shown below:
        /// </note>
        /// <code language="cs">
        /// public string VirtualPagePath {
        /// 
        ///     get { return base.AppRelativeVirtualPath.Substring(1); }
        /// }
        /// </code>
        /// </remarks>
        string VirtualPagePath { get; }
    }
}
