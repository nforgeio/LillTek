//-----------------------------------------------------------------------------
// FILE:        IParseable.cs
// CONTRIBUTOR: Jeff Lill
// COPYRIGHT:   Copyright (c) 2005-2015 by Jeffrey Lill. All rights reserved.
// DESCRIPTION: Defines an interface the Config and Serialize classes can use to 
//              render structured values from a configuration setting.

using System;
using System.Reflection;

namespace LillTek.Common
{
    /// <summary>
    /// Defines an interface the <see cref="Config" /> and <see cref="Serialize" /> classes
    /// can use to render structured values from a configuration setting.
    /// </summary>
    /// <remarks>
    /// <note>
    /// The implementing class must also have a parameterless default constructor.
    /// </note>
    /// </remarks>
    public interface IParseable
    {
        /// <summary>
        /// Attempts to parse the configuration value.
        /// </summary>
        /// <param name="value">The configuration value.</param>
        /// <returns><c>true</c> if the value could be parsed, <b>false</b> if the value is not valid for the type.</returns>
        bool TryParse(string value);
    }
}
