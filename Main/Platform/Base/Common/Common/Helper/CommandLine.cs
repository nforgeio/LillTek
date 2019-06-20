//-----------------------------------------------------------------------------
// FILE:        CommandLine.cs
// CONTRIBUTOR: Jeff Lill
// COPYRIGHT:   Copyright (c) 2005-2015 by Jeffrey Lill.  All rights reserved.
// DESCRIPTION: Performs operations on application command line arguments.

using System;
using System.IO;
using System.Text;
using System.Collections;
using System.Collections.Generic;

namespace LillTek.Common
{
    /// <summary>
    /// Performs common operations on application command line arguments.
    /// </summary>
    /// <remarks>
    /// <para>
    /// The static <see cref="ExpandFiles" /> method can be used to process
    /// response files specified in command line arguments.  Response files
    /// are specified by prepending a '@' character to the name of a text
    /// file and then treating each line of the file as a command line argument.
    /// </para>
    /// <para>
    /// The static <see cref="ExpandWildcards" /> method can be used to 
    /// expand file name with the potential of wildcard characters into
    /// the set of actual files that match the pattern.
    /// </para>
    /// <para>
    /// An instance of the <see cref="CommandLine" /> class can also be
    /// created and used to ease the processing of arguments via an
    /// integrated argument parser and indexers.  Arguments formated
    /// as a command line option:
    /// </para>
    /// <code language="none">
    /// 
    ///     -&lt;option name&gt;[:&lt;value&gt;]
    /// 
    /// </code>
    /// <para>
    /// will be parsed into name/value pairs and will be available for
    /// lookup via the string keyed indexer.  Options that specify no
    /// value will be assigned an empty string value.
    /// </para>
    /// <para>
    /// The class will also make all arguments available via the
    /// integer keyed indexer which will return arguments based on
    /// their position on the command line and also via the <see cref="Arguments" />
    /// property.  Command line values (arguments that are not command
    /// line options) are available via the <see cref="Values" /> property.
    /// </para>
    /// </remarks>
    public sealed class CommandLine
    {
        //---------------------------------------------------------------------
        // Static members

        /// <summary>
        /// Parses the argument string passed into a <see cref="CommandLine" />
        /// instance, dealing with quoted parameters, etc.
        /// </summary>
        /// <param name="argString">The argument string.</param>
        public static CommandLine Parse(string argString)
        {
            List<string>    args    = new List<string>();
            char[]          wsChars = new char[] { ' ', '\t' };
            int             p, pEnd;

            if (argString == null)
                throw new ArgumentNullException("argString");

            p = 0;
            while (true)
            {
                // Advance past any whitespace

                while (p < argString.Length && Char.IsWhiteSpace(argString[p]))
                    p++;

                if (p == argString.Length)
                    break;

                if (argString[p] == '"')
                {
                    pEnd = argString.IndexOf('"', p + 1);
                    if (pEnd == -1)
                    {
                        // Unbalanced quote

                        args.Add(argString.Substring(p + 1).Trim());
                        break;
                    }

                    p++;
                    args.Add(argString.Substring(p, pEnd - p));
                    p = pEnd + 1;
                }
                else
                {
                    pEnd = argString.IndexOfAny(wsChars, p);
                    if (pEnd == -1)
                    {

                        args.Add(argString.Substring(p).Trim());
                        break;
                    }

                    args.Add(argString.Substring(p, pEnd - p).Trim());
                    p = pEnd + 1;
                }
            }

            return new CommandLine(args.ToArray());
        }

        /// <summary>
        /// Expands command line arguments by processing arguments
        /// beginning with '@' as input files.
        /// </summary>
        /// <returns>The set of expanded arguments.</returns>
        /// <remarks>
        /// <para>
        /// Command line arguments will be assumed to specify a
        /// text file name after the '@'.  This file will be read
        /// and each non-empty line of text will be inserted as a
        /// command line parameter.
        /// </para>
        /// <para>
        /// Lines of text whose first non-whitespace character is a
        /// pound sign (#) will be ignored as comments.
        /// </para>
        /// <para>
        /// Command line parameters may also span multiple lines by
        /// beginning the parameter with a line of text begininning with
        /// "{{" and finishing it with a line of text containing "}}".
        /// In this case, the command line parameter will be set to the
        /// text between the {{...}} with any CRLF sequences replaced by
        /// a single space.
        /// </para>
        /// <para>
        /// Here's an example:
        /// </para>
        /// <code language="none">
        /// # This is a comment and will be ignored
        /// 
        /// -param1:aaa
        /// -param2:bbb
        /// {{
        /// -param3:hello
        /// world
        /// }}
        /// </code>
        /// <para>
        /// This will be parsed as three command line parameters:
        /// <b>-param1:aaa</b>, <b>-param2:bbb</b>, and <b>-param3:hello world</b>
        /// </para>
        /// </remarks>
        /// <exception cref="IOException">Thrown if there's a problem opening an "@" input file.</exception>
        /// <exception cref="FormatException">Thrown if there's an error parsing an "@" input file.</exception>
        public static string[] ExpandFiles(string[] args)
        {
            List<string> list;

            list = new List<string>();
            foreach (string arg in args)
            {
                if (!arg.StartsWith("@"))
                {
                    list.Add(arg);
                    continue;
                }

                string          path = arg.Substring(1);
                StreamReader    reader = new StreamReader(path);
                string          line;

                try
                {
                    for (line = reader.ReadLine(); line != null; line = reader.ReadLine())
                    {
                        line = line.Trim();
                        if (line == string.Empty || line[0] == '#')
                            continue;   // Ignore empty lines and comments

                        if (line.StartsWith("{{"))
                        {

                            var sb = new StringBuilder(256);

                            // The text up to the next line beginning with "}}" is the next parameter.

                            while (true)
                            {
                                line = reader.ReadLine();
                                if (line == null)
                                    throw new FormatException(string.Format("Command line file [{0}] has an unclosed \"{{{{\" section.", path));

                                line = line.Trim();
                                if (line.StartsWith("}}"))
                                    break;

                                sb.Append(line);
                                sb.Append(' ');
                            }

                            line = sb.ToString().Trim();

                            if (line == string.Empty)
                                continue;
                        }

                        list.Add(line);
                    }
                }
                finally
                {
                    reader.Close();
                }
            }

            args = new String[list.Count];
            list.CopyTo(0, args, 0, list.Count);

            return args;
        }

        /// <summary>
        /// Checks the argument passed for wildcards and expands them into the
        /// appopriate set of matching file names.
        /// </summary>
        /// <param name="path">The file path potentially including wildcards.</param>
        /// <returns>The set of matching file names.</returns>
        public static string[] ExpandWildcards(string path)
        {
            int         pos;
            string      dir;
            string      pattern;

            if (path.IndexOfAny(Helper.FileWildcards) == -1)
                return new string[] { path };

            pos = path.LastIndexOfAny(new char[] { '\\', '/', ':' });
            if (pos == -1)
                return Directory.GetFiles(".", path);

            dir = path.Substring(0, pos);
            pattern = path.Substring(pos + 1);

            return Directory.GetFiles(dir, pattern);
        }

        /// <summary>
        /// Formats an array of objects into a form suitable for passing to a 
        /// process on the command line by adding double quotes around any values
        /// with embedded spaces.
        /// </summary>
        /// <param name="args">The arguments to be formatted.</param>
        /// <returns>the formatted string.</returns>
        /// <exception cref="FormatException">Thrown if any of the arguments contain double quote or any other invalid characters.</exception>
        public static string Format(params object[] args)
        {
            var sb = new StringBuilder();

            for (int i = 0; i < args.Length; i++)
            {
                string v = args[i].ToString();
                bool space = false;

                foreach (char ch in v)
                {
                    if (ch == ' ')
                        space = true;
                    else if (ch < ' ' || ch == '"')
                        throw new FormatException(string.Format("Illegal character [code={0}] in command line argument.", (int)ch));
                }

                if (i > 0)
                    sb.Append(' ');

                if (space)
                {
                    sb.Append('"');
                    sb.Append(v);
                    sb.Append('"');
                }
                else
                    sb.Append(v);
            }

            return sb.ToString();
        }

        /// <summary>
        /// Determines if the help <b>-?</b> command line option is present in any of the arguments.
        /// </summary>
        /// <param name="args">The command line arguments.</param>
        /// <returns><c>true</c> if the <b>-?</b> help option is present.</returns>
        public static bool HasHelpOption(string[] args)
        {
            foreach (string arg in args)
                if (arg == "-?")
                    return true;

            return false;
        }

        //---------------------------------------------------------------------
        // Instance members

        private Dictionary<string, string>  options;
        private string[]                    args;
        private string[]                    values;
        private int                         valuePos;

        /// <summary>
        /// Constructs an instance, converting the object array into string arguments
        /// and then expanding any response file arguments.
        /// </summary>
        /// <param name="args">The command line arguments.</param>
        public CommandLine(object[] args)
            : this(args, true)
        {
        }

        /// <summary>
        /// Converts an array of objects to an array of strings.
        /// </summary>
        private static string[] ToStrings(object[] args)
        {
            string[] output = new string[args.Length];

            for (int i = 0; i < args.Length; i++)
                output[i] = args[i].ToString();

            return output;
        }

        /// <summary>
        /// Constructs an instance, converting the object array into string arguments,
        /// and then optionally expanding any response file specified in the arguments passed.
        /// </summary>
        /// <param name="args">The command line arguments.</param>
        /// <param name="expandFiles">Pass <c>true</c> to expand any specified response files.</param>
        public CommandLine(object[] args, bool expandFiles)
            : this(ToStrings(args), expandFiles)
        {
        }

        /// <summary>
        /// Constructs an instance, expanding any response file specified
        /// in the arguments passed.
        /// </summary>
        /// <param name="args">The command line arguments.</param>
        public CommandLine(string[] args)
            : this(args, true)
        {
        }

        /// <summary>
        /// Constructs an instance optionally expanding any response file specified
        /// in the arguments passed.
        /// </summary>
        /// <param name="args">The command line arguments.</param>
        /// <param name="expandFiles">Pass <c>true</c> to expand any specified response files.</param>
        public CommandLine(string[] args, bool expandFiles)
        {
            List<string>    valueList = new List<string>();
            string          name;
            string          value;
            int             p;

            this.args = expandFiles ? ExpandFiles(args) : args;

            options = new Dictionary<string, string>();
            for (int i = 0; i < this.args.Length; i++)
            {
                var arg = this.args[i];

                if (!arg.StartsWith("-"))
                {
                    valueList.Add(arg);
                    continue;
                }

                p = arg.IndexOf(':');
                if (p == -1)
                {
                    name  = arg.Substring(1);
                    value = string.Empty;
                }
                else
                {
                    name  = arg.Substring(1, p - 1);
                    value = arg.Substring(p + 1);
                }

                name = name.Trim();
                if (name == string.Empty)
                    continue;

                options[name] = value;
            }

            values = valueList.ToArray();
            valuePos = 0;
        }

        /// <summary>
        /// Returns the array of command line arguments (including both
        /// command line options and values).
        /// </summary>
        public string[] Arguments
        {
            get { return args; }
        }

        /// <summary>
        /// Returns the array of command line values (arguments that are not
        /// command line options).
        /// </summary>
        public string[] Values
        {
            get { return values; }
        }

        /// <summary>
        /// The current position in the <see cref="Values" /> array used by
        /// <see cref="GetNextValue" />.
        /// </summary>
        public int ValuePos
        {
            get { return valuePos; }

            set
            {
                if (value < 0)
                    throw new IndexOutOfRangeException();

                valuePos = value;
            }
        }

        /// <summary>
        /// Returns the next value (arguments that are not command line options)
        /// from the command line or <c>null</c> if the end of the command line
        /// has been reached.  <see cref="ValuePos" /> will be advanced whenever
        /// a non-<c>null</c> value is returned.
        /// </summary>
        /// <returns>The next value or <c>null</c>.</returns>
        public string GetNextValue()
        {
            if (valuePos < values.Length)
                return values[valuePos++];
            else
                return null;
        }

        /// <summary>
        /// Returns the number of arguments on the command line.
        /// </summary>
        public int Count
        {
            get { return args.Length; }
        }

        /// <summary>
        /// Returns an argument from the command line based on its position.
        /// </summary>
        /// <param name="index">The zero-based position of the desired argument.</param>
        /// <returns>The argument string.</returns>
        public string this[int index]
        {
            get { return this.args[index]; }
        }

        /// <summary>
        /// Returns the value associated with a command line option.
        /// </summary>
        /// <param name="optionName">The option name.</param>
        /// <returns>The option value or <c>null</c> if the option was not found.</returns>
        /// <remarks>
        /// <note>
        /// Command line option names are case sensitive.
        /// </note>
        /// </remarks>
        public string this[string optionName]
        {
            get
            {
                string value;

                if (!options.TryGetValue(optionName, out value))
                    return null;

                return value;
            }
        }

        /// <summary>
        /// Returns the value associated with a command line option if
        /// the option was present on the command line otherwise, a specified
        /// default value will be returned.
        /// </summary>
        /// <param name="optionName">The option name.</param>
        /// <param name="def">The default value.</param>
        /// <returns>The option value if present, the specified default value otherwise.</returns>
        /// <remarks>
        /// <note>
        /// Command line option names are case sensitive.
        /// </note>
        /// </remarks>
        public string GetOption(string optionName, string def)
        {
            string value;

            if (!options.TryGetValue(optionName, out value))
                return def;

            return value;
        }

        /// <summary>
        /// Returns all of the values a command line option that appears multiple
        /// times in the command.
        /// </summary>
        /// <param name="optionName">The option name.</param>
        /// <returns>The array of values found.</returns>
        /// <remarks>
        /// <note>
        /// Command line option names are case sensitive.
        /// </note>
        /// <note>
        /// Only command line options that actually specify a value using the
        /// colon (:) syntax are returned by this method.
        /// </note>
        /// </remarks>
        public string[] GetOptionValues(string optionName)
        {
            List<string>    values = new List<string>();
            string          prefix = "-" + optionName.ToLowerInvariant() + ":";

            foreach (var arg in args)
                if (arg.ToLowerInvariant().StartsWith(prefix))
                    values.Add(arg.Substring(prefix.Length));

            return values.ToArray();
        }

        /// <summary>
        /// Determines if the help <b>-?</b> command line option is present in any of the arguments.
        /// </summary>
        /// <returns><c>true</c> if the <b>-?</b> help option is present.</returns>
        public bool HasHelpOption()
        {
            return options.ContainsKey("?");
        }

        /// <summary>
        /// Returns a new <see cref="CommandLine" /> which includes the arguments
        /// starting at the position passed to the end of the command line.
        /// </summary>
        /// <param name="position">The index of the first argument to be included in the result.</param>
        /// <returns>The new <see cref="CommandLine" />.</returns>
        public CommandLine Subset(int position)
        {
            if (position < 0 || position > Arguments.Length)
                throw new ArgumentOutOfRangeException("position");

            var args = new string[Arguments.Length - position];

            for (int i = 0; i < args.Length; i++)
                args[i] = Arguments[position + i];

            return new CommandLine(args);
        }

        /// <summary>
        /// Renders the command line as a string suitable for presenting to a process or
        /// a command line shell.  Arguments that include spaces will be enclosed in 
        /// double quotes.
        /// </summary>
        /// <returns>The command line string.</returns>
        public override string ToString()
        {
            var sb = new StringBuilder();

            for (int i = 0; i < Arguments.Length; i++)
            {
                var arg = Arguments[i];

                if (i > 0)
                    sb.Append(' ');

                if (arg.IndexOf(' ') != -1)
                    sb.AppendFormat("\"{0}\"", arg);
                else
                    sb.Append(arg);
            }

            return sb.ToString();
        }
    }
}
