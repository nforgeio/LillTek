//-----------------------------------------------------------------------------
// FILE:        QueryClientForm.cs
// OWNER:       Jeff Lill
// COPYRIGHT:   Copyright (c) 2005-2015 by Jeffrey Lill.  All rights reserved.
// DESCRIPTION: The Query Client test form

using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Text;
using System.Threading;
using System.Windows.Forms;

using LillTek.Common;
using LillTek.Messaging;

namespace LillTek.Test.Messaging
{
    /// <summary>
    /// The Query Client test form
    /// </summary>
    public partial class QueryClientForm : Form, ITestForm
    {
        private bool        running = false;
        private DateTime    startTime;
        private long        startTimer;
        private long        cPerf;
        private long        cTotal;
        private long        cTimeout;
        private Thread      bkThread;
        private int         cParallelQueries;
        private int         cbPayload;
        private long        totalLatency;

        /// <summary>
        /// Constructor.
        /// </summary>
        public QueryClientForm()
        {
            InitializeComponent();

            TopLevel = false;
            Visible = true;
            Dock = DockStyle.Fill;
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
            startButton.Enabled          = !running;
            stopButton.Enabled           = running;
            parallelQueriesLabel.Enabled = !running;
            parallelQueriesBox.Enabled   = !running;
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

            StringBuilder   sb   = new StringBuilder();
            TimeSpan        time = HiResTimer.CalcTimeSpan(startTimer);
            long            cTotal;
            long            cTimeout;

            cTotal   = Interlocked.Read(ref this.cTotal);
            cTimeout = Interlocked.Read(ref this.cTimeout);

            sb.AppendFormat("Total:    {0}\r\n", cTotal);
            sb.AppendFormat("Rate:     {0:0.00}/sec\r\n", (cTotal - cPerf) / time.TotalSeconds);
            sb.AppendFormat("Ave Rate: {0:0.00}/sec\r\n", cTotal / (SysTime.Now - startTime).TotalSeconds);
            sb.AppendFormat("Retry:    {0}\r\n", MainForm.Router.Metrics.SessionRetries.Count);
            sb.AppendFormat("Timeout:  {0}\r\n", MainForm.Router.Metrics.SessionTimeouts.Count);

            if (cParallelQueries == 1)
                sb.AppendFormat("Latency:  {0}ms\r\n", TimeSpan.FromTicks(Interlocked.Read(ref totalLatency) / cTotal).TotalMilliseconds);

            statusBox.Text = sb.ToString();

            cPerf      = cTotal;
            startTimer = HiResTimer.Count;
        }

        //---------------------------------------------------------------------
        // Background thread

        /// <summary>
        /// The background thread.
        /// </summary>
        private void BkThread()
        {
            MsgEP           ep = "abstract://LillTek/Test/Message/Query";
            IAsyncResult[]  ar = new IAsyncResult[cParallelQueries];
            ResponseMsg     ack;
            long            startCount;

            while (true)
            {
                startCount = HiResTimer.Count;

                for (int i = 0; i < cParallelQueries; i++)
                    ar[i] = MainForm.Router.BeginQuery(ep, new QueryMsg(cbPayload), null, null);

                for (int i = 0; i < cParallelQueries; i++)
                {
                    try
                    {
                        ack = (ResponseMsg)MainForm.Router.EndQuery(ar[i]);
                        Interlocked.Increment(ref cTotal);
                    }
                    catch (TimeoutException)
                    {
                        Interlocked.Increment(ref cTimeout);
                    }
                    catch
                    {
                    }
                }

                Interlocked.Add(ref totalLatency, HiResTimer.CalcTimeSpan(startCount).Ticks);
            }
        }

        //---------------------------------------------------------------------
        // Form event handlers

        private void QueryClientForm_Load(object sender, EventArgs args)
        {
            SetUIState();
        }

        private void startButton_Click(object sender, EventArgs args)
        {
            if (running || !MainForm.Running)
                return;

            startTime    = SysTime.Now;
            startTimer   = HiResTimer.Count;
            cTotal       = 0;
            cPerf        = 0;
            cTimeout     = 0;
            cbPayload    = int.Parse(payloadSizeBox.Text);
            totalLatency = 0;

            running = true;
            SetUIState();

            bkThread = new Thread(new ThreadStart(BkThread));
            bkThread.Start();
        }

        private void stopButton_Click(object sender, EventArgs args)
        {
            if (!running)
                return;

            bkThread.Abort();
            bkThread.Join();
            bkThread = null;
            Thread.Sleep(1000);

            MainForm.Router.Dispatcher.RemoveTarget(this);
            Thread.Sleep(1000);

            running = false;
            statusBox.Text = string.Empty;
            SetUIState();
        }

        private void parallelQueriesBox_Validating(object sender, CancelEventArgs args)
        {
            string  error = string.Empty;
            int     v;

            try
            {
                v = int.Parse(parallelQueriesBox.Text);
                if (v <= 0)
                    throw new ArgumentException();

                cParallelQueries = v;
            }
            catch
            {
                error = "Enter a positive number";
                args.Cancel = true;
            }

            errorProvider.SetError(parallelQueriesBox, error);
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
                error = "Enter the payload size in bytes.";
                args.Cancel = true;
            }

            errorProvider.SetError(payloadSizeBox, error);
        }
    }
}