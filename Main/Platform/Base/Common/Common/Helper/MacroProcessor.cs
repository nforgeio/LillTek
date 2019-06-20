//-----------------------------------------------------------------------------
// FILE:        MacroProcessor.cs
// CONTRIBUTOR: Jeff Lill
// COPYRIGHT:   Copyright (c) 2005-2015 by Jeffrey Lill. All rights reserved.
// DESCRIPTION: Implements a mechanism for replacing macros in a string
//              with the corresponding values.

using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Text;

namespace LillTek.Common
{
    /// <summary>
    /// Implements a mechanism for replacing macros in a string with the corresponding values.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Use the class by first initializing it with a set of macro name/value pairs by
    /// calling the <see cref="Add" /> method.  Then call the <see cref="Expand(string)" /> 
    /// method to expand any macros of the form <b>$(macro)</b> into the corresponding value.
    /// </para>
    /// <note>
    /// Macro names are case insensitive and that macros can be nested up
    /// to 16 levels deep.
    /// </note>
    /// </remarks>
    public sealed class MacroProcessor
    {
        private Dictionary<string, string> vars;

        /// <summary>
        /// Static constructor.
        /// </summary>
        public MacroProcessor()
        {
            vars = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        }

        /// <summary>
        /// Adds a name/value pair to the set of macro variables.
        /// </summary>
        /// <param name="name">The name.</param>
        /// <param name="value">The value</param>
        public void Add(string name, object value)
        {
            vars.Add(name, value.ToString());
        }

        /// <summary>
        /// Removes all macro definitions.
        /// </summary>
        public void Clear()
        {
            vars.Clear();
        }

        /// <summary>
        /// Accesses the named macro value.
        /// </summary>
        public string this[string name]
        {
            get
            {
                string value = null;

                vars.TryGetValue(name, out value);
                return value;
            }

            set { vars[name] = value; }
        }

        /// <summary>
        /// Returns the value of a specified variable.
        /// </summary>
        /// <param name="name">The variable name.</param>
        /// <returns>The value of the variable (or <c>null</c>).</returns>
        private string Get(string name)
        {
            string value = null;

            vars.TryGetValue(name, out value);
            return value;
        }

        /// <summary>
        /// Handles the actual recursive expansion of a string.
        /// </summary>
        /// <param name="input">The string containing the variables to expand.</param>
        /// <param name="nesting">The nesting level.</param>
        /// <returns>The input string with exapanded variables.</returns>
        private string Expand(string input, int nesting)
        {
            if (nesting >= 16)
                throw new StackOverflowException("Too many nested macro variable expansions.");

            StringBuilder   sb;
            int             p, pStart, pEnd;
            string          name;
            string          value;

            // Return right away if there are no macro characters in the string.

            if (input.IndexOf('$') == -1)
                return input;

            // Process variables of the form $(name)

            sb = new StringBuilder(input.Length + 64);
            p = 0;
            while (true)
            {
                // Scan for the next environment variable name quoted with percent $(name) characters.

                pStart = input.IndexOf("$(", p);
                if (pStart == -1)
                {
                    // No starting quote so append the rest of the string and exit.

                    sb.Append(input.Substring(p));
                    break;
                }

                pEnd = input.IndexOf(')', pStart + 1);
                if (pEnd == -1)
                {
                    // No terminating quote so append the rest of the string and exit.

                    sb.Append(input.Substring(p));
                    break;
                }

                name = input.Substring(pStart + 2, pEnd - pStart - 2);

                // Append any text from the beginning of this scan up
                // to the start of the variable.

                sb.Append(input.Substring(p, pStart - p));

                // Look up the variable's value.  If found, then recursively 
                // expand and then insert the value.  If not found then 
                // append the variable name without change.

                value = Get(name);
                if (value == null)
                    sb.Append(input.Substring(pStart, pEnd - pStart + 1));
                else
                    sb.Append(Expand(value, nesting + 1));

                // Advance past this definition

                p = pEnd + 1;
            }

            return sb.ToString();
        }

        /// <summary>
        /// Replaces macro variables in the string passed with the variable
        /// values.
        /// </summary>
        /// <param name="input">The string containing the variables to expand.</param>
        /// <returns>The input string with expanded variables.</returns>
        /// <remarks>
        /// <para>
        /// Looks for macro variables in the string of the form <b>$(name)</b>
        /// and replaces them with the corresponding value if there is one.  This works
        /// recursively for up to 16 levels of nesting.  Strings that match these forms
        /// that don't map to an environment variable will remain untouched.
        /// </para>
        /// </remarks>
        public string Expand(string input)
        {
            return Expand(input, 0);
        }
    }
}
