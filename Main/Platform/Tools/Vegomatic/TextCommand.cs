//-----------------------------------------------------------------------------
// FILE:        TextCommand.cs
// CONTRIBUTOR: Jeff Lill
// COPYRIGHT:   Copyright (c) 2005-2015 by Jeffrey Lill. All rights reserved.
// DESCRIPTION: Implements the TEXT commands.

using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;

using LillTek.Common;

namespace LillTek.Tools.Vegomatic
{
    /// <summary>
    /// Implements the TEXT commands.
    /// </summary>
    public static class TextCommand
    {
        /// <summary>
        /// Executes the specified TEXT command.
        /// </summary>
        /// <param name="args">The command arguments.</param>
        /// <returns>0 on success, a non-zero error code otherwise.</returns>
        public static int Execute(string[] args)
        {
            const string usage =
@"
Usage: 

-------------------------------------------------------------------------------
vegomatic text crlf <file>

Normalizes the specified text file by converting any LF line
endings to CRLF and writing the result to stdout.

-------------------------------------------------------------------------------
vegomatic text top <count> <file>

Reads the top <count> lines of text from the specified file
and writes them to stdout in normalized form.

-------------------------------------------------------------------------------
vegomatic text replace <patterns> <file>

Searches a file for a set of strings and replaces these with
new values.  <patterns> specifies the search and replacement
strings, one per line of text, with each line formatted as:

    <pattern1>""-->""<replace1>
    <pattern2>""-->""<replace2>
    <pattern3>""-->""<replace3>

with lines without a ""-->"" included are ignored.

<file> specifies the file to be munged (encoded as UTF-8)

-------------------------------------------------------------------------------
vegomatic text explore <file>

Loads a potentially very large text file into memory and then
provides a simple command based UI for searching the text.

";
            if (args.Length < 2)
            {
                Program.Error(usage);
                return 1;
            }

            switch (args[0].ToLowerInvariant())
            {
                case "crlf":

                    return CRLF(args[1]);

                case "top":

                    try
                    {
                        if (args.Length != 3)
                            throw new Exception();

                        return Top(int.Parse(args[1]), args[2]);
                    }
                    catch
                    {
                        Program.Error("Usage: vegomatic text top <count> <file>");
                        return 1;
                    }

                case "replace":

                    try
                    {
                        if (args.Length != 3)
                            throw new Exception();

                        return Replace(args[1], args[2]);
                    }
                    catch
                    {
                        Program.Error("Usage: vegomatic text replace <patterns> <file>");
                        return 1;
                    }

                case "explore":

                    try
                    {
                        if (args.Length != 2)
                            throw new Exception();

                        return Explore(args[1]);
                    }
                    catch
                    {
                        Program.Error("Usage: vegomatic text explore <file>");
                        return 1;
                    }

                default:

                    Program.Error(usage);
                    return 1;
            }
        }

        private static void UpdateStatus(StreamReader input, string label)
        {
            if (!Program.UpdateStatusNow)
                return;

            Program.WriteStatus("{0}: {1}%", label, (int)(100.0 * (double)input.BaseStream.Position / (double)input.BaseStream.Length));
        }

        private static int CRLF(string fileName)
        {
            StreamReader    reader = null;
            string          line;
            int             c = 0;

            try
            {
                reader = new StreamReader(new FileStream(fileName, FileMode.Open, FileAccess.Read, FileShare.None, 128 * 1024));
                line   = reader.ReadLine();
                while (line != null)
                {
                    UpdateStatus(reader, fileName);

                    c++;
                    line = line.Replace("\r", "");
                    line = line.Replace("\n", "");
                    Program.Output(line);

                    line = reader.ReadLine();
                }
            }
            catch (Exception e)
            {
                Program.Error("Error ({0}): {1}", e.GetType().Name, e.Message);
                return 1;
            }
            finally
            {
                if (reader != null)
                    reader.Close();
            }

            Program.Log("Lines Processed: {0}", c);

            return 0;
        }

        private static int Top(int count, string fileName)
        {
            StreamReader    reader = null;
            string          line;
            int             c = 0;

            try
            {
                reader = new StreamReader(new FileStream(fileName, FileMode.Open, FileAccess.Read, FileShare.None, 128 * 1024));
                line  = reader.ReadLine();
                while (line != null)
                {
                    UpdateStatus(reader, fileName);

                    c++;
                    line = line.Replace("\r", "");
                    line = line.Replace("\n", "");
                    Program.Output(line);

                    if (c >= count)
                        break;

                    line = reader.ReadLine();
                    c++;
                }
            }
            catch (Exception e)
            {
                Program.Error("Error ({0}): {1}", e.GetType().Name, e.Message);
                return 1;
            }
            finally
            {
                if (reader != null)
                    reader.Close();
            }

            Program.Log("Lines Processed: {0}", c);

            return 0;
        }

        private static int Replace(string patternsFile, string fileName)
        {
            try
            {
                var patterns = new Dictionary<string, string>();

                // Parse the patterns file

                using (var reader = new StreamReader(patternsFile, Encoding.UTF8))
                {
                    string      line;
                    int         pos;
                    string      pattern;
                    string      replace;

                    line = reader.ReadLine();
                    while (line != null)
                    {
                        pos = line.IndexOf("-->");
                        if (pos != -1)
                        {
                            pattern = line.Substring(0, pos).Trim();
                            replace = line.Substring(pos + 3).Trim();

                            if (pattern.Length > 0)
                                patterns[pattern] = replace;
                        }

                        line = reader.ReadLine();
                    }
                }

                // Process the file.  $hack(jeff.lill): This isn't very efficient

                string text;

                using (var reader = new StreamReader(fileName))
                    text = reader.ReadToEnd();

                foreach (string pattern in patterns.Keys)
                    text = text.Replace(pattern, patterns[pattern]);

                using (var writer = new StreamWriter(fileName, false, Encoding.UTF8))
                    writer.Write(text);
            }
            catch (Exception e)
            {
                Program.Error("Error ({0}): {1}", e.GetType().Name, e.Message);
                return 1;
            }

            return 0;
        }

        private static void Log(string logPath, string format, params object[] args)
        {
            if (logPath == null)
                return;

            Helper.CreateFileTree(logPath);

            if (args.Length > 0)
                Helper.AppendToFile(logPath, string.Format(format, args) + "\r\n");
            else
                Helper.AppendToFile(logPath, format + "\r\n");
        }

        private static int Explore(string path)
        {
            // $hack(jeff.lill):
            //
            // For now, I'm just going to load the file into a huge StringBuilder
            // and then convert it into a string.  This means that the data will
            // be in memory twice for a moment.  I'm doing it this way so that 
            // I can use the stock Regex class to do scanning.

            const string commandUsage =
@"
TEXT Commands:

    find <regex>    -- Searches for a regular expression
    log [ <path> ]  -- Writes find results to a file or disables logging
    quiet [on|off]  -- Controls on screen find results  
    help            -- Displays this message
    exit            -- Exits the tool
";
            try
            {
                string          logPath   = null;
                bool            quietMode = false;
                StringBuilder   sb;
                long            size;
                string          text;
                int             cLines;

                Program.Output("");
                Program.Output("Loading: {0}", path);

                using (var stream = new FileStream(path, FileMode.Open))
                {
                    size = stream.Length;
                }

                sb = new StringBuilder((int)Math.Min(int.MaxValue, size));
                cLines = 0;

                using (var reader = new StreamReader(path, Encoding.UTF8))
                {
                    for (var line = reader.ReadLine(); line != null; line = reader.ReadLine())
                    {
                        sb.AppendLine(line);
                        cLines++;
                    }
                }

                Program.Output("Load Complete: {0:#,###} Line Count: {1:#,###}", size, cLines);
                Program.Output("Initializing...");

                text = sb.ToString();
                sb.Clear();
                sb = null;
                GC.Collect();

                Console.WriteLine();

                while (true)
                {
                    const int cLinesBeforeAndAfter = 5;

                    CommandLine     cmdLine;
                    string          command;
                    string          pattern;
                    Regex           regex;
                    int             cMatches;
                    int             p, pos, posEnd;
                    int             posMatchLine;
                    List<string>    lines;
                    string          matchLine;

                    Console.Write("EXPLORE: ");
                    command = Console.ReadLine();
                    cmdLine = CommandLine.Parse(command);

                    if (cmdLine.Count == 0)
                        continue;

                    switch (cmdLine[0].ToLower())
                    {
                        case "log":

                            if (cmdLine.Count < 2)
                                logPath = null;
                            else
                                logPath = cmdLine[1];

                            Helper.CreateFileTree(logPath);
                            File.WriteAllText(logPath, string.Empty);
                            break;

                        case "quiet":

                            if (cmdLine.Count == 1)
                                quietMode = !quietMode;

                            switch (cmdLine[1].ToLower())
                            {
                                case "on":

                                    quietMode = true;
                                    break;

                                case "off":

                                    quietMode = false;
                                    break;
                            }

                            Console.WriteLine();
                            Console.WriteLine("Quiet Mode: {0}", quietMode ? "ON" : "OFF");
                            Console.WriteLine();
                            break;

                        case "find":

                            p = command.IndexOf(' ');
                            if (p == -1)
                            {
                                Console.WriteLine(commandUsage);
                                continue;
                            }

                            pattern = command.Substring(p).TrimStart();
                            regex = new Regex(pattern, RegexOptions.Compiled | RegexOptions.Multiline);

                            Console.WriteLine("Scanning...");

                            cMatches = 0;
                            foreach (Match match in regex.Matches(text))
                            {
                                cMatches++;

                                if (!quietMode)
                                {
                                    Console.WriteLine();
                                    Console.WriteLine("-----------------------------------------------------------");
                                    Console.WriteLine("*** Match: index: {0:#,###} length: {1:#,###}", match.Index, match.Length);
                                    Console.WriteLine();
                                }

                                // Scan backwards to print out the lines before.

                                lines = new List<string>();
                                pos = match.Index;

                                if (pos == 0)
                                    posMatchLine = 0;
                                else
                                {
                                    if (text[pos - 1] == '\n')
                                        posMatchLine = pos;
                                    else
                                        posMatchLine = -1;

                                    for (int lineCount = 0; lineCount < cLinesBeforeAndAfter; lineCount++)
                                    {
                                        posEnd = pos;
                                        pos--;

                                        while (pos > 0 && text[pos] != '\n')
                                            pos--;

                                        if (posMatchLine == -1)
                                        {
                                            if (pos == 0)
                                                posMatchLine = 0;
                                            else
                                                posMatchLine = pos + 1;
                                        }

                                        if (pos > 0 && text[pos - 1] == '\r')
                                            pos--;

                                        p = pos;
                                        while (p < posEnd && (text[p] == '\r' || text[p] == '\n'))
                                            p++;

                                        lines.Add(text.Substring(p, posEnd - p).TrimEnd());

                                        if (pos == 0)
                                            break;
                                    }
                                }

                                if (!quietMode)
                                {
                                    for (int i = lines.Count - 1; i >= 0; i--)
                                        Console.WriteLine("    {0}", lines[i]);
                                }

                                // Write the matching line, highlighting the match.

                                posEnd = text.IndexOfAny(new char[] { '\r', '\n' }, match.Index);
                                if (posEnd != -1)
                                    matchLine = text.Substring(match.Index, posEnd - match.Index);
                                else
                                    matchLine = text.Substring(match.Index);

                                Log(logPath, matchLine);

                                if (!quietMode)
                                {
                                    Console.WriteLine(">>> {0}", matchLine.TrimEnd());

                                    Console.Write("    ");
                                    for (int i = posMatchLine; i < match.Index; i++)
                                        Console.Write(" ");

                                    switch (match.Length)
                                    {
                                        case 1:

                                            Console.Write("^");
                                            break;

                                        case 2:

                                            Console.Write("^");
                                            Console.Write("^");
                                            break;

                                        default:

                                            Console.Write("^");

                                            for (int i = 0; i < match.Length - 2; i++)
                                                Console.Write("-");

                                            Console.Write("^");
                                            break;
                                    }

                                    Console.WriteLine();
                                    Console.WriteLine();
                                }

                                // Write the lines after.

                                lines = new List<string>();

                                pos = posMatchLine + matchLine.Length;
                                if (pos < text.Length && text[pos] == '\n')
                                    pos++;
                                else if (pos < text.Length && text[pos] == '\r')
                                {
                                    pos++;
                                    if (pos < text.Length && text[pos] == '\n')
                                        pos++;
                                }

                                for (int lineCount = 0; lineCount < cLinesBeforeAndAfter; lineCount++)
                                {
                                    posEnd = text.IndexOfAny(new char[] { '\r', '\n' }, pos);

                                    if (posEnd == -1)
                                    {
                                        lines.Add(text.Substring(pos).TrimEnd());
                                        break;
                                    }
                                    else
                                        lines.Add(text.Substring(pos, posEnd - pos).TrimEnd());

                                    pos = posEnd;
                                    if (pos < text.Length && text[pos] == '\n')
                                        pos++;
                                    else if (pos < text.Length && text[pos] == '\r')
                                    {
                                        pos++;
                                        if (pos < text.Length && text[pos] == '\n')
                                            pos++;
                                    }
                                }

                                if (!quietMode)
                                {
                                    foreach (var line in lines)
                                        Console.WriteLine("    {0}", line);
                                }

                                if (Console.KeyAvailable)
                                {
                                    Console.ReadKey();
                                    Console.WriteLine();
                                    Console.WriteLine("*** Operation Cancelled ***");
                                    Console.WriteLine();
                                    break;
                                }
                            }

                            if (cMatches == 0)
                            {
                                Console.WriteLine();
                                Console.WriteLine("-----------------------------------------------------------");
                                Console.WriteLine("*** Not Found ***");
                                Console.WriteLine();
                            }

                            Console.WriteLine();
                            break;

                        case "exit":

                            Console.WriteLine();
                            Console.WriteLine();
                            return 0;

                        default:
                        case "help":

                            Console.WriteLine(commandUsage);
                            break;
                    }
                }
            }
            catch (Exception e)
            {
                Program.Error(e.Message);
                return 1;
            }
        }
    }
}
