//-----------------------------------------------------------------------------
// FILE:        Linq2SqlCommand.cs
// CONTRIBUTOR: Jeff Lill
// COPYRIGHT:   Copyright (c) 2005-2015 by Jeffrey Lill. All rights reserved.
// DESCRIPTION: Implements the LINQ2SQL commands.

using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Threading;

using LillTek.Common;

namespace LillTek.Tools.Vegomatic
{
    /// <summary>
    /// Implements the LINQ2SQL commands.
    /// </summary>
    public static class Linq2SqlCommand
    {
        /// <summary>
        /// Executes the specified LINQ2SQL command.
        /// </summary>
        /// <param name="args">The command arguments.</param>
        /// <returns>0 on success, a non-zero error code otherwise.</returns>
        public static int Execute(string[] args)
        {

            const string usage =
@"
Usage: 

-------------------------------------------------------------------------------
vegomatic linq2sql readonly <class> <property> <source>

Scans the SQLMetal generated C# <source> file specified for
the specified <class> and <property> and adds ""IsDbGenerated=true""
to the [Column(...)] attribute for the named property.
The property's setter will also be removed.  The source file
will be rewritten with the changes.

This is useful for read-only database columns that are defined 
in the database schema as non-null with a default value.

-------------------------------------------------------------------------------
vegomatic linq2sql renameproperty <class> <old> <new> <source>

Scans the SQLMetal generated C# <source> file and renames the
specified property within the specified <class>.  Pass <old>
as the original property name and <new> as the new name.

-------------------------------------------------------------------------------
vegomatic linq2sql noupdatecheck <source>

Scans the SQLMetal generated C# <source> file for column definitons
and modifies their [Column(...)] attributes to disable optimistic
concurrency checking (if not already disabled).  The modified 
attribute will look like: [Column(...,UpdateCheck=UpdateCheck.Never)]

-------------------------------------------------------------------------------
vegomatic linq2sql replaceinclass <class> <oldtext> <newtext> <source>

Scans the SQLMetal generated C# <source> file looking for the source
code of the <class> specified and then replacing text within
the bounds of the class where <oldtext>  is the text to be
replaced and <newtext> is the replacement text.

-------------------------------------------------------------------------------
vegomatic linq2sql deletemethod  <class> <method> <source>

Scans the SQLMetal generated C# <source> file for the specified
<class> definition and removes the <method> source code if
present.  This command assumes that the method is private and
returns void.

Note that the command also deletes any lines of source within
the class that reference the method.

-------------------------------------------------------------------------------
vegomatic linq2sql stubproperty <class> <property> <source>

Scans the SQLMetal generated C# <source> file for the specified
<class> definition and looks for the specified public <property>.
The setter for this property will be replaced with code that
will throw a NotImplementedException.

";
            if (args.Length == 0)
            {
                Program.Error(usage);
                return 1;
            }

            switch (args[0].ToLowerInvariant())
            {
                case "readonly":

                    if (args.Length != 4)
                    {
                        Program.Error(usage);
                        return 1;
                    }

                    return ReadOnly(args[1], args[2], args[3]);

                case "renameproperty":

                    if (args.Length != 5)
                    {
                        Program.Error(usage);
                        return 1;
                    }

                    return RenameProperty(args[1], args[2], args[3], args[4]);

                case "replace":

                    if (args.Length != 5)
                    {
                        Program.Error(usage);
                        return 1;
                    }

                    return ReplaceInClass(args[1], args[2], args[3], args[4]);

                case "deletemethod":

                    if (args.Length != 4)
                    {
                        Program.Error(usage);
                        return 1;
                    }

                    return DeleteMethod(args[1], args[2], args[3]);

                case "stubproperty":

                    if (args.Length != 4)
                    {
                        Program.Error(usage);
                        return 1;
                    }

                    return StubProperty(args[1], args[2], args[3]);

                case "noupdatecheck":

                    if (args.Length != 2)
                    {
                        Program.Error(usage);
                        return 1;
                    }

                    return NoUpdateCheck(args[1]);

                default:

                    Program.Error(usage);
                    return 1;
            }
        }

        private static int ReadOnly(string className, string propertyName, string source)
        {
            // $hack(jeffl): This code depends on the formatting of the SQLMetal 
            //               generated source file.

            try
            {
                StringBuilder   output = new StringBuilder();
                string          match;
                string          line;
                string          trim;
                int             pos;

                using (var reader = new StreamReader(source))
                {
                    // Copy source lines to the output until we reach the class definition.
                    // This will be formatted as:
                    //
                    // public partial class <class> : ...

                    match = string.Format("public partial class {0} :", className);

                    line = reader.ReadLine();
                    while (line != null)
                    {
                        output.AppendLine(line);
                        if (line.Trim().StartsWith(match))
                            break;

                        line = reader.ReadLine();
                    }

                    if (line == null)
                        throw new FormatException(string.Format("Class [{0}] not found.", className));

                    // Scan down until we find the property's [Column(...)] attribute.  Note that
                    // I'm assuming that the Storage parameter remains set to the property name
                    // with a leading underscore.
                    //
                    // If we see a [DataContract] attribute then the property was not found.

                    match = string.Format("[Column(Storage=\"_{0}\"", propertyName);

                    line = reader.ReadLine();
                    while (line != null)
                    {
                        trim = line.Trim();

                        if (trim.StartsWith(match))
                            break;
                        else if (trim.StartsWith("[DataContract("))
                            throw new FormatException(string.Format("Property [{0}] not found in class [{1}].", propertyName, className));

                        output.AppendLine(line);
                        line = reader.ReadLine();
                    }

                    // The current line holds the [Column(...)] attribute.  Append IsDbGenerated=true

                    pos = line.IndexOf(")]");
                    if (pos == -1)
                        throw new FormatException("Unexpected [Column(...)] attribute formatting.");

                    line = line.Substring(0, pos) + ", IsDbGenerated=true)]";
                    output.AppendLine(line);

                    // Copy the rest of the source file to the output

                    line = reader.ReadLine();
                    while (line != null)
                    {
                        output.AppendLine(line);
                        line = reader.ReadLine();
                    }
                }

                // Update the original source file

                Helper.WriteToFile(source, output.ToString(), Helper.AnsiEncoding);
            }
            catch (Exception e)
            {
                Program.Error(e.Message);
                return 1;
            }

            return 0;
        }

        private static int RenameProperty(string className, string oldName, string newName, string source)
        {
            // $hack(jeffl): This code depends on the formatting of the SQLMetal 
            //               generated source file.

            try
            {
                StringBuilder   output = new StringBuilder();
                string          match;
                string          line;

                using (var reader = new StreamReader(source))
                {
                    // Copy source lines to the output until we reach the class definition.
                    // This will be formatted as:
                    //
                    // public partial class <class> : ...

                    match = string.Format("public partial class {0} :", className);

                    line = reader.ReadLine();
                    while (line != null)
                    {
                        output.AppendLine(line);
                        if (line.Trim().StartsWith(match))
                            break;

                        line = reader.ReadLine();
                    }

                    if (line == null)
                        throw new FormatException(string.Format("Class [{0}] not found.", className));

                    // Scan down until we reach another class definition or
                    // we find the property definition line of the form:
                    //
                    //      public <type> <old name>

                    line = reader.ReadLine();
                    while (line != null)
                    {
                        if (line.StartsWith("public partial class"))
                            break;

                        string[] split = line.Split(' ');

                        if (split.Length >= 3 && split[0].Trim() == "public" && split[2].Trim() == oldName)
                        {
                            // Reassemble the line with the new name

                            line = split[0] + " " + split[1] + " " + newName;

                            for (int i = 3; i < split.Length; i++)
                                line += " " + split[i];

                            break;
                        }

                        output.AppendLine(line);
                        line = reader.ReadLine();
                    }

                    // Process the remaining source lines without change.

                    while (line != null)
                    {
                        output.AppendLine(line);
                        line = reader.ReadLine();
                    }
                }

                // Update the original source file

                Helper.WriteToFile(source, output.ToString(), Helper.AnsiEncoding);
            }
            catch (Exception e)
            {
                Program.Error(e.Message);
                return 1;
            }

            return 0;
        }

        private static int ReplaceInClass(string className, string oldText, string newText, string source)
        {
            // $hack(jeffl): This code depends on the formatting of the SQLMetal 
            //               generated source file.

            try
            {
                StringBuilder   output = new StringBuilder();
                string          match;
                string          line;

                using (var reader = new StreamReader(source))
                {
                    // Copy source lines to the output until we reach the class definition.
                    // This will be formatted as:
                    //
                    // public partial class <class> : ...

                    match = string.Format("public partial class {0} :", className);

                    line = reader.ReadLine();
                    while (line != null)
                    {
                        output.AppendLine(line);
                        if (line.Trim().StartsWith(match))
                            break;

                        line = reader.ReadLine();
                    }

                    if (line == null)
                        throw new FormatException(string.Format("Class [{0}] not found.", className));

                    // Scan down until we reach the end of the class (indicated
                    // by a line starting with TAB + "}", replacing any instances
                    // of oldText with newText that we find.

                    line = reader.ReadLine();
                    while (line != null)
                    {
                        if (line.StartsWith("\t}"))
                            break;      // End of the class definition

                        line = line.Replace(oldText, newText);

                        output.AppendLine(line);
                        line = reader.ReadLine();
                    }

                    // Process the remaining source lines without change.

                    while (line != null)
                    {
                        output.AppendLine(line);
                        line = reader.ReadLine();
                    }
                }

                // Update the original source file

                Helper.WriteToFile(source, output.ToString(), Helper.AnsiEncoding);
            }
            catch (Exception e)
            {
                Program.Error(e.Message);
                return 1;
            }

            return 0;
        }

        private static int DeleteMethod(string className, string method, string source)
        {
            // $hack(jeffl): This code depends on the formatting of the SQLMetal 
            //               generated source file.

            try
            {
                StringBuilder   output = new StringBuilder();
                string          match;
                string          line;

                using (var reader = new StreamReader(source))
                {
                    // Copy source lines to the output until we reach the class definition.
                    // This will be formatted as:
                    //
                    // public partial class <class> : ...

                    match = string.Format("public partial class {0} :", className);

                    line = reader.ReadLine();
                    while (line != null)
                    {
                        output.AppendLine(line);
                        if (line.Trim().StartsWith(match))
                            break;

                        line = reader.ReadLine();
                    }

                    if (line == null)
                        throw new FormatException(string.Format("Class [{0}] not found.", className));

                    // Scan down until we reach the end of the class (indicated
                    // by a line starting with TAB + "}" or we find the specified 
                    // private void method().  If we find the method then delete it.

                    match = "private void " + method + "(";

                    line = reader.ReadLine();
                    while (line != null)
                    {
                        if (line.StartsWith("\t}"))
                            break;  // End of the class definition

                        if (line.Trim().StartsWith(match))
                        {
                            // This is the method so skip over lines until we
                            // reach the end of the method (indicated by
                            // TAB + TAB + "}"

                            line = reader.ReadLine();
                            while (line != null)
                            {
                                output.AppendLine();

                                if (line.StartsWith("\t\t}"))
                                {
                                    line = string.Empty;
                                    break;
                                }

                                line = reader.ReadLine();
                            }
                        }

                        if (line.Contains("this." + method))
                            line = string.Empty;    // Strip out references to the method

                        output.AppendLine(line);
                        line = reader.ReadLine();
                    }

                    // Process the remaining source lines without change.

                    while (line != null)
                    {
                        output.AppendLine(line);
                        line = reader.ReadLine();
                    }
                }

                // Update the original source file

                Helper.WriteToFile(source, output.ToString(), Helper.AnsiEncoding);
            }
            catch (Exception e)
            {
                Program.Error(e.Message);
                return 1;
            }

            return 0;
        }

        private static int StubProperty(string className, string property, string source)
        {
            // $hack(jeffl): This code depends on the formatting of the SQLMetal 
            //               generated source file.

            try
            {
                StringBuilder   output = new StringBuilder();
                string          match;
                string          line;

                using (var reader = new StreamReader(source))
                {
                    // Copy source lines to the output until we reach the class definition.
                    // This will be formatted as:
                    //
                    // public partial class <class> : ...

                    match = string.Format("public partial class {0} :", className);

                    line = reader.ReadLine();
                    while (line != null)
                    {
                        output.AppendLine(line);
                        if (line.Trim().StartsWith(match))
                            break;

                        line = reader.ReadLine();
                    }

                    if (line == null)
                        throw new FormatException(string.Format("Class [{0}] not found.", className));

                    // Scan down until we reach the end of the class (indicated
                    // by a line starting with TAB + "}" or we find the specified 
                    // public property.

                    line = reader.ReadLine();
                    while (line != null)
                    {
                        if (line.StartsWith("\t}"))
                            break;  // End of the class definition

                        string[] split = line.Trim().Split(' ');

                        if (split.Length == 3 && split[0] == "public" && split[2] == property)
                        {
                            // We're at the top of the property definition.  Continue
                            // processing lines until we reach the setter.

                            output.AppendLine(line);

                            line = reader.ReadLine();
                            while (line != null)
                            {
                                if (line.Trim() == "set")
                                    break;

                                output.AppendLine(line);
                                line = reader.ReadLine();
                            }

                            if (line == null)
                                break;

                            // We found the setter.  Output the new setter.

                            output.AppendLine("\t\t\tset");
                            output.AppendLine("\t\t\t{");
                            output.AppendLine("\t\t\t\tthrow new NotImplementedException(\"Cannot associate entities using this property. Use the entity ID instead.\");");
                            output.AppendLine("\t\t\t}");

                            // Read the rest of the original setter (but don't output the lines).

                            line = reader.ReadLine();
                            while (line != null)
                            {
                                if (line.StartsWith("\t\t\t}"))
                                    break;

                                line = reader.ReadLine();
                            }

                            line = string.Empty;
                            break;
                        }

                        output.AppendLine(line);
                        line = reader.ReadLine();
                    }

                    // Process the remaining source lines without change.

                    while (line != null)
                    {
                        output.AppendLine(line);
                        line = reader.ReadLine();
                    }
                }

                // Update the original source file

                Helper.WriteToFile(source, output.ToString(), Helper.AnsiEncoding);
            }
            catch (Exception e)
            {
                Program.Error(e.Message);
                return 1;
            }

            return 0;
        }

        private static int NoUpdateCheck(string source)
        {
            // $hack(jeffl): 
            // 
            // This code depends on the formatting of the SQLMetal 
            // generated source file.

            // $todo(jeff.lill):
            //
            // Might want to add options to be able to explicitly set update
            // checking for some specific columns.

            try
            {
                StringBuilder   output = new StringBuilder();
                string          line;

                using (var reader = new StreamReader(source))
                {
                    line = reader.ReadLine();
                    while (line != null)
                    {
                        if (line.IndexOf("[Column(") != -1)
                        {
                            // Line is a [Column(...)] attribute.

                            if (line.IndexOf("UpdateCheck") != -1)
                                output.AppendLine(line);        // Attribute already has an UpdateCheck parameter so don't mess with it
                            else
                            {
                                // Add UpdateCheck=UpdateCheck.Never to the attribute

                                int p = line.LastIndexOf(")]");

                                if (p == -1)
                                    output.AppendLine(line);    // Should never happen
                                else
                                    output.AppendLine(line.Substring(0, p) + ", UpdateCheck=UpdateCheck.Never" + line.Substring(p));
                            }
                        }
                        else
                            output.AppendLine(line);

                        line = reader.ReadLine();
                    }
                }

                // Update the original source file

                Helper.WriteToFile(source, output.ToString(), Helper.AnsiEncoding);
            }
            catch (Exception e)
            {
                Program.Error(e.Message);
                return 1;
            }

            return 0;
        }
    }
}
