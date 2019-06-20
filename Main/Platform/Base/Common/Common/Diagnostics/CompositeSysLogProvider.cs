//-----------------------------------------------------------------------------
// FILE:        CompositeSysLogProvider.cs
// CONTRIBUTOR: Jeff Lill
// COPYRIGHT:   Copyright (c) 2005-2015 by Jeffrey Lill.  All rights reserved.
// DESCRIPTION: Implements an ISysLogProvider that is composed of other providers.

using System;
using System.Collections.Generic;

namespace LillTek.Common
{
    /// <summary>
    /// Implements an <see cref="ISysLogProvider" /> that is composed of other providers.
    /// </summary>
    /// <remarks>
    /// This class is useful for situations when events need to be logged to multiple
    /// places.  Pass the child log providers to the constructor or call the <see cref="Add" />
    /// method.
    /// </remarks>
    /// <threadsafety instance="true" />
    public class CompositeSysLogProvider : ISysLogProvider
    {
        private object                  syncLock = new object();
        private List<ISysLogProvider>   providers;

        /// <summary>
        /// Constructs a composite provider from zero or more child providers.
        /// </summary>
        /// <param name="providers">The providers.</param>
        /// <exception cref="ArgumentNullException">Thrown if the provider array or any of the providers withing are <c>null</c>.</exception>
        public CompositeSysLogProvider(params ISysLogProvider[] providers)
        {
            if (providers == null)
                throw new ArgumentNullException("providers");

            this.providers = new List<ISysLogProvider>(providers.Length);

            foreach (var provider in providers)
            {
                if (provider == null)
                    throw new ArgumentNullException("One or more providers passed are NULL.");

                this.providers.Add(provider);
            }
        }

        /// <summary>
        /// Adds a new log provider to the collection.
        /// </summary>
        /// <param name="provider">The new provider.</param>
        /// <exception cref="ArgumentNullException">Thrown if the provider passed is <c>null</c>.</exception>
        public void Add(ISysLogProvider provider)
        {
            if (provider == null)
                throw new ArgumentNullException("provider");

            lock (syncLock)
            {

                this.providers.Add(provider);
            }
        }

        /// <summary>
        /// Flushes any cached log information to persistent storage.
        /// </summary>
        public void Flush()
        {
            lock (syncLock)
            {
                foreach (var provider in providers)
                {
                    try
                    {
                        provider.Flush();
                    }
                    catch
                    {
                        // Ignore errors.
                    }
                }
            }
        }

        /// <summary>
        /// Logs a <see cref="SysLogEntry" />
        /// </summary>
        /// <param name="entry">The log entry.</param>
        public void Log(SysLogEntry entry)
        {
            lock (syncLock)
            {
                foreach (var provider in providers)
                {
                    try
                    {
                        provider.Log(entry);
                    }
                    catch
                    {
                        // Ignore errors.
                    }
                }
            }
        }

        /// <summary>
        /// Logs an informational entry.
        /// </summary>
        /// <param name="extension">Extended log entry information (or <c>null</c>).</param>
        /// <param name="message">The message.</param>
        public void LogInformation(ISysLogEntryExtension extension, string message)
        {
            lock (syncLock)
            {
                foreach (var provider in providers)
                {
                    try
                    {
                        provider.LogInformation(extension, message);
                    }
                    catch
                    {
                        // Ignore errors.
                    }
                }
            }
        }

        /// <summary>
        /// Logs a warning.
        /// </summary>
        /// <param name="extension">Extended log entry information (or <c>null</c>).</param>
        /// <param name="message">The message.</param>
        public void LogWarning(ISysLogEntryExtension extension, string message)
        {
            lock (syncLock)
            {
                foreach (var provider in providers)
                {
                    try
                    {
                        provider.LogWarning(extension, message);
                    }
                    catch
                    {
                        // Ignore errors.
                    }
                }
            }
        }

        /// <summary>
        /// Logs an error.
        /// </summary>
        /// <param name="extension">Extended log entry information (or <c>null</c>).</param>
        /// <param name="message">The message.</param>
        public void LogError(ISysLogEntryExtension extension, string message)
        {
            lock (syncLock)
            {
                foreach (var provider in providers)
                {
                    try
                    {
                        provider.LogError(extension, message);
                    }
                    catch
                    {
                        // Ignore errors.
                    }
                }
            }
        }

        /// <summary>
        /// Logs an exception.
        /// </summary>
        /// <param name="extension">Extended log entry information (or <c>null</c>).</param>
        /// <param name="e">The exception being logged.</param>
        public void LogException(ISysLogEntryExtension extension, Exception e)
        {
            lock (syncLock)
            {
                foreach (var provider in providers)
                {
                    try
                    {
                        provider.LogException(extension, e);
                    }
                    catch
                    {
                        // Ignore errors.
                    }
                }
            }
        }

        /// <summary>
        /// Logs an exception with additional information.
        /// </summary>
        /// <param name="extension">Extended log entry information (or <c>null</c>).</param>
        /// <param name="e">The exception being logged.</param>
        /// <param name="message">The message.</param>
        public void LogException(ISysLogEntryExtension extension, Exception e, string message)
        {
            lock (syncLock)
            {
                foreach (var provider in providers)
                {
                    try
                    {
                        provider.LogException(extension, e, message);
                    }
                    catch
                    {
                        // Ignore errors.
                    }
                }
            }
        }

        /// <summary>
        /// Logs a successful security related change or access attempt.
        /// </summary>
        /// <param name="extension">Extended log entry information (or <c>null</c>).</param>
        /// <param name="message">The message.</param>
        public void LogSecuritySuccess(ISysLogEntryExtension extension, string message)
        {
            lock (syncLock)
            {
                foreach (var provider in providers)
                {
                    try
                    {
                        provider.LogSecuritySuccess(extension, message);
                    }
                    catch
                    {
                        // Ignore errors.
                    }
                }
            }
        }

        /// <summary>
        /// Logs a failed security related change or access attempt.
        /// </summary>
        /// <param name="extension">Extended log entry information (or <c>null</c>).</param>
        /// <param name="message">The message.</param>
        public void LogSecurityFailure(ISysLogEntryExtension extension, string message)
        {
            lock (syncLock)
            {
                foreach (var provider in providers)
                {
                    try
                    {
                        provider.LogSecurityFailure(extension, message);
                    }
                    catch
                    {
                        // Ignore errors.
                    }
                }
            }
        }

        /// <summary>
        /// Logs debugging related information.
        /// </summary>
        /// <param name="extension">Extended log entry information (or <c>null</c>).</param>
        /// <param name="category">Used to group debugging related log entries.</param>
        /// <param name="message">The message.</param>
        public void Trace(ISysLogEntryExtension extension, string category, string message)
        {
            lock (syncLock)
            {
                foreach (var provider in providers)
                {
                    try
                    {
                        provider.Trace(extension, category, message);
                    }
                    catch
                    {
                        // Ignore errors.
                    }
                }
            }
        }
    }
}
