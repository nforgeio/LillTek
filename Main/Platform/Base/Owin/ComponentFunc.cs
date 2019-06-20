//-----------------------------------------------------------------------------
// FILE:        ComponentFunc.cs
// CONTRIBUTOR: Jeff Lill
// COPYRIGHT:   Copyright (c) 2005-2015 by Jeffrey Lill.  All rights reserved.
// DESCRIPTION: Defines the OWIN pipeline application function signature.

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

namespace LillTek.Owin
{
    /// <summary>
    /// Defines the OWIN pipeline application function signature.
    /// </summary>
    /// <param name="environment">The OWIN environment dictionary.</param>
    /// <returns>The <see cref="Task"/> used to track the function's asynchronous completion.</returns>
    public delegate Task ComponentFunc(IDictionary<string, object> environment);
}
