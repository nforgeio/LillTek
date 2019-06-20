//-----------------------------------------------------------------------------
// FILE:        ConfigException.cs
// CONTRIBUTOR: Jeff Lill
// COPYRIGHT:   Copyright (c) 2005-2015 by Jeffrey Lill.  All rights reserved.
// DESCRIPTION: Available for use by applications when they detect a problem
//              with their configuration settings.

using System;
using System.Text;

namespace LillTek.Common
{
    /// <summary>
    /// Available for use by applications when they detect a problem
    /// with their configuration settings.
    /// </summary>
    public sealed class ConfigException : ApplicationException
    {
        /// <summary>
        /// Constructor.
        /// </summary>
        /// <param name="config">The <see cref="Config" /> instance.</param>
        /// <param name="key">The key name.</param>
        public ConfigException(Config config, string key)
            : base(string.Format("Configuration Error [key={0}{1}]", config.KeyPrefix, key))
        {
        }

        /// <summary>
        /// Constructor.
        /// </summary>
        /// <param name="config">The <see cref="Config" /> instance.</param>
        /// <param name="key">The key name.</param>
        /// <param name="message">The exception message.</param>
        public ConfigException(Config config, string key, string message)
            : base(string.Format("Configuration Error [key={0}{1}]: {2}", config.KeyPrefix, key, message))
        {
        }

        /// <summary>
        /// Constructor.
        /// </summary>
        /// <param name="config">The <see cref="Config" /> instance.</param>
        /// <param name="key">The key name.</param>
        /// <param name="format">The exception message format string.</param>
        /// <param name="args">The message arguments.</param>
        public ConfigException(Config config, string key, string format, params object[] args)
            : base(string.Format("Configuration Error [key={0}{1}]: {2}", config.KeyPrefix, key, string.Format(format, args)))
        {
        }
    }
}
