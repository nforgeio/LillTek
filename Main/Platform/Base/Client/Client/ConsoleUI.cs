//-----------------------------------------------------------------------------
// FILE:        ConsoleUI.cs
// CONTRIBUTOR: Jeff Lill
// COPYRIGHT:   Copyright (c) 2005-2015 by Jeffrey Lill.  All rights reserved.
// DESCRIPTION: Wraps a RichTextControl to implement a console output window.

using System;
using System.IO;
using System.Drawing;
using System.Windows.Forms;

using LillTek.Common;

namespace LillTek.Client
{
    /// <summary>
    /// Wraps a <see cref="RichTextBox "/> to implement a console output window.
    /// </summary>
    /// <remarks>
    /// The <see cref="RichTextBox "/>  has some serious limitations when used as a console
    /// output window.  For one, thing, it is not thread safe.  A more insidious
    /// problem is that it crashes when the text length nears 128K.  The class
    /// wraps a <see cref="RichTextBox "/>  instance to address these limitations.
    /// </remarks>
    public class ConsoleUI
    {
        //---------------------------------------------------------------------
        // Local types

        private enum EntryType
        {
            Clear,
            Normal,
            Error
        }

        private sealed class LogInfo
        {
            public EntryType Type;
            public string Text;

            public LogInfo(EntryType type, string text)
            {
                this.Type = type;
                this.Text = text;
            }
        }

        //---------------------------------------------------------------------
        // Instance members

        private object              syncLock = new object();
        private RichTextBox         textBox;    // The wrapped control
        private TextWriter          logWriter;  // Non-null if console output is to be 
                                                // replicated to a stream
        private MethodArg1Invoker   onUILog;    // Used to marshal to the UI thread
        private bool                frozen;     // True if new output to the console
                                                // should be ignored
        private bool                error;      // Indicates whether an error has been logged

        /// <summary>
        /// Wraps the rich text box passed with a ConsoleUI instance.
        /// </summary>
        /// <param name="textBox"></param>
        public ConsoleUI(RichTextBox textBox)
        {
            this.textBox   = textBox;
            this.logWriter = null;
            this.onUILog   = new MethodArg1Invoker(OnUILog);
            this.frozen    = false;
            this.error     = false;
        }

        /// <summary>
        /// This controls whether any text written to the console will
        /// also be replicated to a <see cref="TextWriter" />.  This
        /// defaults to null.
        /// </summary>
        public TextWriter LogWriter
        {
            get { return logWriter; ; }
            set { logWriter = value; }
        }

        /// <summary>
        /// Controls whether or not new output text to the console window
        /// should be ignored.
        /// </summary>
        /// <remarks>
        /// <note>
        /// This property impacts only the output to the control
        /// window.  Text will continue to be written to STDOUT and STDERR
        /// if <see cref="LogWriter" /> is set.
        /// </note>
        /// </remarks>
        public bool Frozen
        {
            get { return frozen; }
            set { frozen = value; }
        }

        /// <summary>
        /// Indicates whether an error has been logged to the console.
        /// </summary>
        /// <remarks>
        /// This is set to true when <see cref="WriteError" /> is called
        /// and can also be set directly by applications.
        /// </remarks>
        public bool Error
        {
            get { return error; }
            set { error = value; }
        }

        /// <summary>
        /// Handles the actual rendering of a log entry into the underlying
        /// rich text box.
        /// </summary>
        /// <param name="arg">A LogInfo instance.</param>
        private void OnUILog(object arg)
        {
            LogInfo info;

            if (frozen)
                return;

            info = (LogInfo)arg;
            switch (info.Type)
            {
                case EntryType.Clear:

                    error = false;
                    textBox.Clear();
                    return;

                case EntryType.Normal:

                    textBox.SelectionColor = Color.Black;
                    break;

                case EntryType.Error:

                    textBox.SelectionColor = Color.Red;
                    break;
            }

            if (textBox.TextLength > 64000)
            {
                textBox.Select(0, 10000);
                textBox.Text = textBox.Text.Substring(10000);
            }

            textBox.AppendText(info.Text + "\r\n");
            textBox.Select(textBox.TextLength, 0);
            textBox.Select();
            textBox.ScrollToCaret();
        }

        /// <summary>
        /// Clears the console window and resets the <see cref="Error" /> flag to <c>false</c>.
        /// </summary>
        public void Clear()
        {
            ApplicationHost.Invoke(textBox, onUILog, new object[] { new LogInfo(EntryType.Clear, null) });
        }

        /// <summary>
        /// Writes a blank line of text to the console window.
        /// </summary>
        public void Write()
        {
            Write(string.Empty);
        }

        /// <summary>
        /// Formats the string and arguments passed and then writes it
        /// to a line of the console window using normal formatting.
        /// </summary>
        /// <param name="format">The format string.</param>
        /// <param name="args">The formate arguments.</param>
        public void Write(string format, params object[] args)
        {
            string line;

            if (args.Length == 0)
                line = format;
            else
                line = string.Format(format, args);

            if (logWriter != null)
            {
                lock (syncLock)
                    logWriter.WriteLine(line);
            }

            if (frozen)
                return;

            ApplicationHost.Invoke(textBox, onUILog, new object[] { new LogInfo(EntryType.Normal, line) });
        }

        /// <summary>
        /// Formats the string and arguments passed and then writes it
        /// to a line of the console window using error formatting.
        /// </summary>
        /// <param name="format">The format string.</param>
        /// <param name="args">The formate arguments.</param>
        /// <remarks>
        /// This method also sets the <see cref="Error" /> property
        /// to <c>true</c>.
        /// </remarks>
        public void WriteError(string format, params object[] args)
        {
            string line;

            error = true;
            if (args.Length == 0)
                line = format;
            else
                line = string.Format(format, args);

            if (logWriter != null)
            {

                lock (syncLock)
                    logWriter.WriteLine(line);
            }

            if (frozen)
                return;

            ApplicationHost.Invoke(textBox, onUILog, new object[] { new LogInfo(EntryType.Error, line) });
        }
    }
}
