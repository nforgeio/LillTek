//-----------------------------------------------------------------------------
// FILE:        IOwinEnvironment.cs
// CONTRIBUTOR: Jeff Lill
// COPYRIGHT:   Copyright (c) 2005-2015 by Jeffrey Lill.  All rights reserved.
// DESCRIPTION: Defines the OWIN environment dictionary.

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

using Microsoft.Owin;
using Microsoft.Owin.Hosting;

using Owin;

using LillTek.Common;

namespace LillTek.Owin
{
    /// <summary>
    /// Defines the OWIN environment dictionary.
    /// </summary>
    public interface IOwinEnvironment : IDictionary<string, string>
    {
    }
}
