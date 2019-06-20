//-----------------------------------------------------------------------------
// FILE:        DependencyResolver.cs
// CONTRIBUTOR: Jeff Lill
// COPYRIGHT:   Copyright (c) 2005-2015 by Jeffrey Lill.  All rights reserved.
// DESCRIPTION: Implements a mechanism for ensuring exclusive access to a named
//              resource across processes.

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Web.Http.Dependencies;

using Microsoft.Owin;
using Microsoft.Owin.Hosting;
using Microsoft.Practices.Unity;

using Owin;

using LillTek.Common;

namespace LillTek.Owin
{
    /// <summary>
    /// Implements a dependency resolver suitable for use in a ASP.NET
    /// Web API application.
    /// </summary>
    /// <threadsafety instance="true"/>
    public class DependencyResolver : IDependencyResolver
    {
        private object          syncLock   = new object();
        private bool            isDisposed = false;
        private IUnityContainer container;

        /// <summary>
        /// Constructs an instance with a new dependency container.
        /// </summary>
        public DependencyResolver()
        {
            this.container = new UnityContainer();
        }

        /// <summary>
        /// Constructs an instance using the specified dependency container.
        /// </summary>
        /// <param name="container">The <see cref="IUnityContainer"/>.</param>
        public DependencyResolver(IUnityContainer container)
        {
            this.container = container;
        }

        /// <summary>
        /// Finalizer.
        /// </summary>
        ~DependencyResolver()
        {
            Dispose(false);
        }

        /// <summary>
        /// Releases important resources.
        /// </summary>
        public void Dispose()
        {
            Dispose(true);
        }

        /// <summary>
        /// Releases important resources.
        /// </summary>
        /// <param name="disposing">Pass <c>true</c> if we're disposing, <c>false</c> if we're finalizing.</param>
        protected virtual void Dispose(bool disposing)
        {
            lock (syncLock)
            {
                if (isDisposed)
                {
                    return;
                }

                if (container != null)
                {
                    container.Dispose();
                    container = null;
                }

                if (disposing)
                {
                    GC.SuppressFinalize(this);
                }

                isDisposed = true;
            }
        }

        /// <summary>
        /// Returns the underlying <see cref="IUnityContainer"/>.
        /// </summary>
        /// <remarks>
        /// <para>
        /// Use this property to register the application's <b>ApiController</b> and other types that need
        /// to be dependency injected before calling <b>IAppBuilder.UseWebApi()</b> to start the web application.
        /// </para>
        /// <note>
        /// Once the web application is started, the container should be considered to
        /// be read-only: to not attempt any further registrations.
        /// </note>
        /// </remarks>
        public IUnityContainer Container
        {
            get { return container; }
        }

        /// <summary>
        /// Attempts to resolve a type into a single object instance.
        /// </summary>
        /// <param name="serviceType">The requested service type.</param>
        /// <returns>The object instance if the mapping was successful or <c>null</c>.</returns>
        public object GetService(Type serviceType)
        {
            try
            {
                return container.Resolve(serviceType);
            }
            catch (ResolutionFailedException)
            {
                return null;
            }
        }

        /// <summary>
        /// Attempts to resolve all of the object instances registered for a given type.
        /// </summary>
        /// <param name="serviceType">The requested service type.></param>
        /// <returns>The set of object instances found or an empty set.</returns>
        public IEnumerable<object> GetServices(Type serviceType)
        {
            try
            {
                return container.ResolveAll(serviceType);
            }
            catch (ResolutionFailedException)
            {
                return new object[0];
            }
        }

        /// <summary>
        /// Creates a new child container, typically to be associated with a Web API request.
        /// </summary>
        /// <returns>The new <see cref="IDependencyScope"/>.</returns>
        public IDependencyScope BeginScope()
        {
            lock (syncLock)
            {
                return new DependencyResolver(container.CreateChildContainer());
            }
        }
    }
}
