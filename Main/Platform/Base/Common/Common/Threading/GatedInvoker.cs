//-----------------------------------------------------------------------------
// FILE:        GatedInvoker.cs
// CONTRIBUTOR: Jeff Lill
// COPYRIGHT:   Copyright (c) 2005-2015 by Jeffrey Lill.  All rights reserved.
// DESCRIPTION: Implements thread safety on an unsafe object implementation by
//              ensuring that only one call to the object's methods made 
//              via this class will be active at any moment in time.

using System;
using System.Threading;
using System.Reflection;
using System.Collections;
using System.Collections.Generic;

namespace LillTek.Common
{
    /// <summary>
    /// Implements thread safety on an unsafe object implementation by ensuring 
    /// that only one call to the object's methods made via this class will be 
    /// active at any moment in time.
    /// </summary>
    /// <remarks>
    /// <para>
    /// To use this class, simply instantiate an instance via <see cref="GatedInvoker" />
    /// passing the target object and then use <see cref="Invoke" /> to invoke the
    /// named method, passing the specified arguments.
    /// </para>
    /// <note>
    /// The method called must be declared as an instance method
    /// and that the number and types of parameters passed to <see cref="Invoke" />
    /// must be compatible with those of the target method.
    /// </note>
    /// <note>
    /// The current implementation is pretty simplistic.  It works only
    /// each there is exactly one instance of each named method.  The class does
    /// not attempt to match up parameter counts or types to try to pick between
    /// method overloads.  The work-around is to ensure that all method names
    /// called by this class are unique.
    /// </note>
    /// </remarks>
    public sealed class GatedInvoker
    {
        private object                          syncLock = new object();
        private object                          target;         // The target object instance
        private System.Type                     targetType;     // The target object type
        private Dictionary<string, MethodInfo>  methodCache;    // Cache of method name to MethodInfo to
        // improve performance

        /// <summary>
        /// Constructs a GatedInvoke instance.
        /// </summary>
        /// <param name="target">The target object who's methods will be called.</param>
        public GatedInvoker(object target)
        {
            if (target == null)
                throw new ArgumentNullException("target");

            this.target      = target;
            this.targetType  = target.GetType();
            this.methodCache = new Dictionary<string, MethodInfo>();
        }

        /// <summary>
        /// Invokes the named method of the target object, passing the arguments
        /// passed as the method parameters and ensuring that only one thread at
        /// a time is able to invoke a method through this instance.
        /// </summary>
        /// <param name="methodName">The method name.</param>
        /// <param name="args">The parameters to be passed to the method.</param>
        /// <returns>Returns the value returned by the method or <c>null</c> if the method is void.</returns>
        /// <remarks>
        /// <note>
        /// The method called must be declared as an instance method
        /// and that the number and types of parameters passed to <see cref="Invoke" />
        /// must be compatible with those of the target method.
        /// </note>
        /// </remarks>
        public object Invoke(string methodName, params object[] args)
        {
            MethodInfo method;

            lock (syncLock)
            {
                // Map the method name to a MethodInfo instance, using the 
                // methodCache to improve performance

                if (!methodCache.TryGetValue(methodName, out method))
                {
                    MethodInfo[] methods;

                    methods = targetType.GetMethods(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
                    method  = null;

                    for (int i = 0; i < methods.Length; i++)
                        if (methods[i].Name == methodName)
                        {
                            if (method != null)
                                throw new InvalidOperationException(string.Format("Target object type [{0}] implements multiple overloads for [{1}].", targetType.FullName, methodName));

                            method = methods[i];
                        }

                    methodCache.Add(methodName, method);
                }

                if (method == null)
                    throw new ArgumentNullException(string.Format("methodName", "Method [{0}] not found.", methodName));

                // Call the method.

                return method.Invoke(target, args);
            }
        }
    }
}
