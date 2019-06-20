//-----------------------------------------------------------------------------
// FILE:        RunWithComponent.cs
// CONTRIBUTOR: Jeff Lill
// COPYRIGHT:   Copyright (c) 2005-2015 by Jeffrey Lill.  All rights reserved.
// DESCRIPTION: Implements an OWIN middleware component used for handling HTTP
//              requests asynchronouly in association with an object instance.

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
    /// Implements an OWIN middleware component used for handling HTTP
    /// requests asynchronouly in association with an object instance.
    /// </summary>
    public class RunWithComponent<T>
    {
        private ComponentFunc               nextComponent;
        private T                           instance;
        private Func<IOwinContext, T, Task> action;

        /// <summary>
        /// Constructor.
        /// </summary>
        /// <param name="nextComponent">The next component in the pipeline (or <c>null</c>).</param>
        /// <param name="action">The action to be performed.</param>
        /// <param name="instance">The object to be associated with this component.</param>
        public RunWithComponent(ComponentFunc nextComponent, Func<IOwinContext, T, Task> action, T instance)
        {
            this.nextComponent = nextComponent;
            this.instance      = instance;
            this.action        = action;
        }

        /// <summary>
        /// Implements the component.
        /// </summary>
        /// <param name="environment">The OWIN environment.</param>
        /// <returns>The task used to complete the operation.</returns>
        public async Task Invoke(IDictionary<string, object> environment)
        {
            if (action != null)
            {
                await action(new OwinContext(environment), instance);
            }

            if (nextComponent != null)
            {
                await nextComponent(environment);
            }
        }
    }
}
