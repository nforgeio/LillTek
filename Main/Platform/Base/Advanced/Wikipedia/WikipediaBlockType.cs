//-----------------------------------------------------------------------------
// FILE:        WikipediaBlockType.cs
// CONTRIBUTOR: Jeff Lill
// COPYRIGHT:   Copyright (c) 2005-2015 by Jeffrey Lill.  All rights reserved.
// DESCRIPTION: Identfies the possible wiki text block types.

using System;
using System.Diagnostics;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Xml.Linq;

using LillTek.Common;

namespace LillTek.Advanced
{
    /// <summary>
    /// Identfies the possible wiki text block types.
    /// </summary>
    public enum WikipediaBlockType
    {
        /// <summary>
        /// Normal paragraph.
        /// </summary> block
        Normal,

        /// <summary>
        /// Block is already formatted.
        /// </summary>
        Preformatted,

        /// <summary>
        /// Block is a bulleted list item.
        /// </summary>
        Bullet,

        /// <summary>
        /// Block is a numbered list item.
        /// </summary>
        Numbered,

        /// <summary>
        /// Term within a definition list.
        /// </summary>
        Term,

        /// <summary>
        /// Description witin a definition list.
        /// </summary>
        Definition,

        /// <summary>
        /// Virtual list root.
        /// </summary>
        RootList,

        /// <summary>
        /// Definition list.
        /// </summary>
        DefinitionList,

        /// <summary>
        /// Bullet list.
        /// </summary>
        BulletList,

        /// <summary>
        /// Numbered list.
        /// </summary>
        NumberedList
    }
}
