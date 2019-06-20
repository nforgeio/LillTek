//-----------------------------------------------------------------------------
// FILE:        TSQLPreprocessor.cs
// CONTRIBUTOR: Jeff Lill
// COPYRIGHT:   Copyright (c) 2005-2015 by Jeffrey Lill.  All rights reserved.
// DESCRIPTION: Handles the preprocessing of macros and transactions in T-SQL scripts.

using System;
using System.IO;
using System.Text;
using System.Reflection;
using System.Collections;
using System.Text.RegularExpressions;

using LillTek.Common;

namespace LillTek.Data.Install
{
    /// <summary>
    /// Handles the preprocessing of macros and transactions in T-SQL scripts.
    /// </summary>
    public sealed class TSQLPreprocessor
    {
        /// <summary>
        /// Set to <c>true</c> to enable transaction processing.
        /// </summary>
        public bool Trans = false;

        private Hashtable   symbols = null;     // Table of enum types
        private Hashtable   macros = null;     // Table of macro definitions

        /// <summary>
        /// Constructor
        /// </summary>
        public TSQLPreprocessor()
        {
        }

        /// <summary>
        /// Processes the macros and transactions in the input file and writes
        /// the results to the output.
        /// </summary>
        /// <param name="srcFile">Path to the source file.</param>
        /// <param name="destFile">Path to the output file.</param>
        public void Process(string srcFile, string destFile)
        {
            StreamReader    reader = new StreamReader(srcFile, Encoding.ASCII);
            StreamWriter    writer = new StreamWriter(destFile, false, Encoding.ASCII);
            string          input;

            try
            {
                input = reader.ReadToEnd();
                input = Process(input);

                writer.Write(input);
            }
            finally
            {
                reader.Close();
                writer.Close();
            }
        }

        /// <summary>
        /// Processes any macros and transactions in the input string and returns
        /// the processed results.
        /// </summary>
        /// <param name="input">The unprocessed input string.</param>
        /// <returns>The processed output.</returns>
        public string Process(string input)
        {
            if (Trans)
                input = ProcessTransactions(input);

            if (macros != null)
                input = ProcessMacros(input);

            if (symbols != null)
                input = ProcessSymbols(input);

            return input;
        }

        /// <summary>
        /// Appends the transaction template to the string builder after replacing
        /// the @transID variable with one generated from varID.
        /// </summary>
        private void AppendTransaction(StringBuilder sb, string template, int varID)
        {
            sb.Append(template.Replace("@transID", "@__trans" + varID.ToString()));
        }

        /// <summary>
        /// This method handles the processing of TSQL transaction related statements.
        /// </summary>
        /// <param name="input">The input TSQL</param>
        /// <returns>The processed output.</returns>
        private string ProcessTransactions(string input)
        {
            // $todo: My implementation is super brain-dead.  I should implement this
            //        via Regex but I'm too lazy to really learn it right now.

            // The idea here is to replace instances of $(transaction), $(commit), and $(rollback)
            // with code that uses transaction save points to implement nested transactions.  The
            // code generated will look something like:

            // $(transaction):
            //
            // begin transaction
            // set @transID = '_' + cast(@@trancount-1 as varchar(10))
            // exec sp_executesql N'save transaction @trans', N'@trans varchar(10)', @trans=@transID

            // $(commit)
            //
            // commit transaction

            // $(rollback)
            //
            // declare @transID	varchar(10)
            // set @transID = '_' + cast(@@trancount-1 as varchar(10))
            // exec sp_executesql N'rollback transaction @trans', N'@trans varchar(10)', @trans=@transID
            // commit transaction

            // The only trick here is that I have to generate unique local variable names for each
            // generated section since TSQL is too stupid to implement variable scope.

            const string beginTemplate =
@"
-- $(transaction)
begin
declare @transID varchar(10)
begin transaction
set @transID = '_' + cast(@@trancount-1 as varchar(10))
exec sp_executesql N'save transaction @trans', N'@trans varchar(10)', @trans=@transID
end
-- $(transaction)
";

            const string commitTemplate =
@"
-- $(commit)
begin
commit transaction
end
-- $(commit)
";

            const string rollbackTemplate =
@"
-- $(rollback)
begin
declare @transID varchar(10)
set @transID = '_' + cast(@@trancount-1 as varchar(10))
exec sp_executesql N'rollback transaction @trans', N'@trans varchar(10)', @trans=@transID
commit transaction
end
-- $(rollback)
";
            int             varID = 0;
            StringBuilder   sb;
            string          pat;
            int             pos, next;

            // Handle $(transaction)

            sb  = new StringBuilder(input.Length);
            pat  = "$(transaction)";
            next = 0;
            pos  = input.IndexOf(pat, next);
            while (pos != -1)
            {
                sb.Append(input.Substring(next, pos - next));
                AppendTransaction(sb, beginTemplate, varID++);

                next = pos + pat.Length;
                pos  = input.IndexOf(pat, next);
            }

            sb.Append(input.Substring(next));
            input = sb.ToString();

            // Handle $(commit)

            sb   = new StringBuilder(input.Length);
            pat   = "$(commit)";
            next = 0;
            pos = input.IndexOf(pat, next);
            while (pos != -1)
            {
                sb.Append(input.Substring(next, pos - next));
                AppendTransaction(sb, commitTemplate, varID++);

                next = pos + pat.Length;
                pos  = input.IndexOf(pat, next);
            }

            sb.Append(input.Substring(next));
            input = sb.ToString();

            // Handle $(rollback)

            sb   = new StringBuilder(input.Length);
            pat  = "$(rollback)";
            next = 0;
            pos = input.IndexOf(pat, next);
            while (pos != -1)
            {
                sb.Append(input.Substring(next, pos - next));
                AppendTransaction(sb, rollbackTemplate, varID++);

                next = pos + pat.Length;
                pos  = input.IndexOf(pat, next);
            }

            sb.Append(input.Substring(next));
            input = sb.ToString();

            return input;
        }

        /// <summary>
        /// This method loads any enumeration types tagged with 
        /// <see cref="TSQLPPAttribute" /> from the assembly 
        /// passed into the symbol table.
        /// </summary>
        /// <param name="assembly">The assembly to be processed.</param>
        public void LoadSymbols(Assembly assembly)
        {
            var types = assembly.GetTypes();

            foreach (var type in types)
            {
                CustomAttribute attr;

                if (!type.IsEnum && !type.IsClass)
                    continue;   // Ignore everything except enum and class types

                attr = CustomAttribute.Get(type.GetCustomAttributes(false), "LillTek.Common" + ".TSQLPPAttribute");
                if (attr == null)
                    continue;   // Ignore types not marked with [TSQLPP]

                if (symbols == null)
                    symbols = new Hashtable();

                if (symbols[type.Name] != null)
                    throw new Exception(string.Format("Type name conflict: [{0}] and [{1}].", ((Type)symbols[type.Name]).FullName, type.FullName));

                symbols.Add(type.Name, type);
            }
        }

        /// <summary>
        /// This method loads any enumeration types tagged with 
        /// <see cref="TSQLPPAttribute" /> from the assembly whose file name is
        /// passed into the symbol table.
        /// </summary>
        /// <param name="fileName">Name of the assembly file.</param>
        public void LoadSymbols(string fileName)
        {
            LoadSymbols(Assembly.LoadFrom(fileName));
        }

        /// <summary>
        /// This method processes the symbol strings of the form $(type.value) by replacing
        /// them with the corresponding integer constant.
        /// </summary>
        /// <param name="input">The input string.</param>
        /// <returns>The processed output.</returns>
        private string ProcessSymbols(string input)
        {
            Regex               regEx = new Regex(@"\$\((?<type>\w+)\.(?<value>\w+)\)");
            MatchCollection     matches;
            StringBuilder       sb;
            int                 pos;

            matches = regEx.Matches(input);
            if (matches.Count == 0)
                return input;

            sb  = new StringBuilder(input.Length);
            pos = 0;

            for (int i = 0; i < matches.Count; i++)
            {
                Match           match     = matches[i];
                string          typeName  = match.Groups["type"].Value;
                string          typeValue = match.Groups["value"].Value;
                System.Type     type;
                string          value;

                type = (System.Type)symbols[typeName];
                if (type == null)
                    throw new Exception(string.Format("Type not found for expression $({0}.{1}).  Make sure the type has a [TSQLPP] attribute.", typeName, typeValue));

                if (type.IsEnum)
                {
                    try
                    {
                        value = ((int)Enum.Parse(type, typeValue, true)).ToString();
                    }
                    catch
                    {
                        throw new Exception(string.Format("Value not found for expression $({0}.{1}).", typeName, typeValue));
                    }
                }
                else
                {
                    Assertion.Test(type.IsClass);

                    try
                    {
                        FieldInfo   fi;
                        object      raw;

                        fi = type.GetField(typeValue);
                        if (fi == null)
                            throw new Exception();

                        if (!fi.IsLiteral || fi.IsInitOnly)
                            throw new ArgumentException(string.Format("Field $({0}.{1}) is not a constant.", typeName, typeValue));

                        raw = fi.GetRawConstantValue();
                        if (raw == null)
                            throw new ArgumentException(string.Format("Constant $({0}.{1}) has no value.", typeName, typeValue));

                        value = raw.ToString();
                    }
                    catch (ArgumentException)
                    {
                        throw;
                    }
                    catch
                    {
                        throw new Exception(string.Format("Value not found for expression $({0}.{1}).", typeName, typeValue));
                    }
                }

                sb.Append(input.Substring(pos, match.Index - pos));
                sb.Append(value);

                pos = match.Index + match.Length;
            }

            sb.Append(input.Substring(matches[matches.Count - 1].Index + matches[matches.Count - 1].Length));

            return sb.ToString();
        }

        /// <summary>
        /// Parses the macros definitions found in the reader passed.
        /// </summary>
        /// <param name="reader">The macro definition stream.</param>
        /// <param name="fileName">Name identifying the reader stream (used for reporting errors).</param>
        public void LoadMacros(TextReader reader, string fileName)
        {
            string      line;
            string      name;
            string      value;
            int         pos;
            int         lineNum;

            macros = new Hashtable();

            lineNum = 0;
            line    = reader.ReadLine();

            while (line != null)
            {
                lineNum++;

                line = line.Trim();
                if (line == string.Empty || line.StartsWith("//") || line.StartsWith("--"))
                {
                    line = reader.ReadLine();
                    continue;
                }

                pos = line.IndexOf('=');
                if (pos == -1)
                    throw new Exception(string.Format("{0}({1}): Missing '=' in macro definition.", fileName, lineNum));

                name  = line.Substring(0, pos).Trim();
                value = line.Substring(pos + 1).Trim();

                if (name == string.Empty || name.IndexOfAny(new char[] { '.', '(', ')' }) != -1)
                    throw new Exception(string.Format("{0}({1}): Invalid macro name.", fileName, lineNum));

                if (macros[name] != null)
                    throw new Exception(string.Format("{0}({1}): Invalid macro [{2}] has already been defined.", fileName, lineNum, name));

                macros[name] = value;

                line = reader.ReadLine();
            }
        }

        /// <summary>
        /// This method parses the macro definition file specified.
        /// </summary>
        /// <param name="fileName">Name of the definition file.</param>
        public void LoadMacros(string fileName)
        {
            var reader = new StreamReader(fileName, Helper.AnsiEncoding, false);

            try
            {
                LoadMacros(reader, fileName);
            }
            finally
            {
                reader.Close();
            }
        }

        /// <summary>
        /// This method processes the macros strings of the form $(value) by replacing
        /// them with the corresponding value.
        /// </summary>
        /// <param name="input">The input string.</param>
        /// <returns>The processed output.</returns>
        private string ProcessMacros(string input)
        {
            Regex               regEx = new Regex(@"\$\((?<name>\w+)\)");
            MatchCollection     matches;
            StringBuilder       sb;
            int                 pos;

            matches = regEx.Matches(input);
            if (matches.Count == 0)
                return input;

            sb  = new StringBuilder(input.Length);
            pos = 0;

            for (int i = 0; i < matches.Count; i++)
            {
                Match       match = matches[i];
                string      macro = match.Groups["name"].Value;
                string      value;

                switch (macro)
                {
                    case "transaction":
                    case "commit":
                    case "rollback":

                        // Leave the transaction macros intact

                        value = "$(" + macro + ")";
                        break;

                    default:

                        value = (string)macros[macro];
                        if (value == null)
                            throw new Exception(string.Format("Macro $({0}) not defined.", macro));

                        break;
                }

                sb.Append(input.Substring(pos, match.Index - pos));
                sb.Append(value);

                pos = match.Index + match.Length;
            }

            sb.Append(input.Substring(matches[matches.Count - 1].Index + matches[matches.Count - 1].Length));

            return sb.ToString();
        }
    }
}
