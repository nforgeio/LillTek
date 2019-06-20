//-----------------------------------------------------------------------------
// FILE:        QueryServerForm.cs
// OWNER:       Jeff Lill
// COPYRIGHT:   Copyright (c) 2005-2015 by Jeffrey Lill.  All rights reserved.
// DESCRIPTION: The Query Server test form

using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Diagnostics;
using System.Drawing;
using System.Text;
using System.Threading;
using System.Windows.Forms;

using LillTek.Common;
using LillTek.Messaging;

namespace LillTek.Test.Messaging
{
    /// <summary>
    /// The Query Server test form
    /// </summary>
    public partial class QueryServerForm : Form, ITestForm
    {
        private bool        running = false;
        private DateTime    startTime;
        private long        startTimer;
        private long        cPerf;
        private long        cTotal;
        private int         cbPayload;
        private int         delay;
        private bool        synchronous;

        /// <summary>
        /// Constructor.
        /// </summary>
        public QueryServerForm()
        {
            InitializeComponent();

            TopLevel = false;
            Visible  = true;
            Dock     = DockStyle.Fill;
        }

        /// <summary>
        /// Stops the execution of a test if one is running.
        /// </summary>
        public void Stop()
        {
            stopButton_Click(null, null);
        }

        /// <summary>
        /// Sets the UI state.
        /// </summary>
        private void SetUIState()
        {
            startButton.Enabled = !running;
            stopButton.Enabled  = running;
        }

        /// <summary>
        /// Called periodically by the main form allowing statistics to
        /// be rendered.  This will be called on the UI thread.
        /// </summary>
        public void OnTimer()
        {
            if (!running)
            {
                statusBox.Text = string.Empty;
                return;
            }

            StringBuilder   sb = new StringBuilder();
            TimeSpan        time = HiResTimer.CalcTimeSpan(startTimer);
            long            cTotal;

            cTotal = Interlocked.Read(ref this.cTotal);

            sb.AppendFormat("Total:    {0}\r\n", cTotal);
            sb.AppendFormat("Rate:     {0:0.00}/sec\r\n", (cTotal - cPerf) / time.TotalSeconds);
            sb.AppendFormat("Ave Rate: {0:0.00}/sec\r\n", cTotal / (SysTime.Now - startTime).TotalSeconds);
            statusBox.Text = sb.ToString();

            cPerf = cTotal;
            startTimer = HiResTimer.Count;
        }

        //---------------------------------------------------------------------
        // Form event handlers

        private void QueryForm_Load(object sender, EventArgs args)
        {
            SetUIState();
        }

        private void startButton_Click(object sender, EventArgs args)
        {
            if (running || !MainForm.Running)
                return;

            startTime   = SysTime.Now;
            startTimer  = HiResTimer.Count;
            cTotal      = 0;
            cPerf       = 0;
            cbPayload   = int.Parse(payloadSizeBox.Text);
            delay       = int.Parse(delayBox.Text);
            synchronous = syncCheckBox.Checked;

            MainForm.Router.Dispatcher.AddTarget(this);
            running = true;
            SetUIState();
        }

        private void stopButton_Click(object sender, EventArgs args)
        {
            if (!running)
                return;

            MainForm.Router.Dispatcher.RemoveTarget(this);
            Thread.Sleep(1000);

            running = false;
            statusBox.Text = string.Empty;
            SetUIState();
        }

        //---------------------------------------------------------------------
        // Message handlers

        /// <summary></summary>
        /// <param name="query"></param>
        [MsgHandler(LogicalEP = "abstract://LillTek/Test/Message/Query")]
        [MsgSession(Type = SessionTypeID.Query, Idempotent = true)]
        public void OnMsg(QueryMsg query)
        {
            var reply = new ResponseMsg(cbPayload);

            Interlocked.Increment(ref cTotal);

            if (synchronous)
            {
                using (TimedLock.Lock(this, 10000))
                {
                    if (delay > 0)
                        Thread.Sleep(delay);

                    MainForm.Router.ReplyTo(query, reply);
                }
            }
            else
            {
                if (delay > 0)
                    Thread.Sleep(delay);

                MainForm.Router.ReplyTo(query, reply);
            }
        }

        private void payloadSizeBox_Validating(object sender, CancelEventArgs args)
        {
            string  error = string.Empty;
            int     v;

            try
            {
                v = int.Parse(payloadSizeBox.Text);
                if (v < 0)
                    throw new ArgumentException();

                cbPayload = v;
            }
            catch
            {
                error       = "Enter the payload size in bytes.";
                args.Cancel = true;
            }

            errorProvider.SetError(payloadSizeBox, error);
        }

        private void delayBox_Validating(object sender, CancelEventArgs args)
        {
            string  error = string.Empty;
            int     v;

            try
            {
                v = int.Parse(delayBox.Text);
                if (v < 0)
                    throw new ArgumentException();

                delay = v;
            }
            catch
            {
                error       = "Enter the processing delay in milliseconds.";
                args.Cancel = true;
            }

            errorProvider.SetError(delayBox, error);
        }

        private void syncCheckBox_CheckedChanged(object sender, EventArgs args)
        {
            synchronous = syncCheckBox.Checked;
        }
    }
}
