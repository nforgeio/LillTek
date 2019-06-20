//-----------------------------------------------------------------------------
// FILE:        MainForm.cs
// OWNER:       JEFFL
// COPYRIGHT:   Copyright (c) 2005-2015 by Jeffrey Lill.  All rights reserved.
// DESCRIPTION: Main form and application entry point

#undef SIMPACKETS       // Define this to enable the periodic generation
                        // of test trace packets

using System;
using System.IO;
using System.Text;
using System.Threading;
using System.Collections;
using System.Diagnostics;
using System.ComponentModel;
using System.Windows.Forms;
using System.Data;

using LillTek.Client;
using LillTek.Common;

namespace LillTek.Tools.NetTrace
{
    /// <summary>
    /// The main application form.
    /// See the application entry point method <see cref="MainForm.Main">Main</see>
    /// for a description of the command line parameters.
    /// </summary>
    public class MainForm : System.Windows.Forms.Form
    {

        //---------------------------------------------------------------------
        // Static members

        /// <summary>
        /// The main entry point for the application.  There are no command
        /// line parameters at this time.
        /// </summary>
        [STAThread]
        static void Main()
        {
            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);
            Application.Run(new MainForm());
        }

        //---------------------------------------------------------------------
        // Instance members

        private IContainer components;
        private NetTraceSinkDelegate onTraceUI;
        private DataTable dtCapture;
        private bool isRunning;
        private bool captureFile;
        private StreamWriter captureWriter;

#if SIMPACKETS
        private GatedTimer              timer;
#endif

        private System.Windows.Forms.DataGrid TraceList;
        private System.Windows.Forms.TextBox TraceDetail;
        private System.Windows.Forms.DataGridTableStyle Time;
        private System.Windows.Forms.DataGridTableStyle SourceEP;
        private System.Windows.Forms.DataGridTableStyle Summary;
        private System.Windows.Forms.DataGridTextBoxColumn TimeCol;
        private System.Windows.Forms.Panel MainForm_Fill_Panel;
        private ToolStripContainer toolStripContainer1;
        private ToolStripContainer toolStripContainer2;
        private ToolStripContainer toolStripContainer3;
        private ToolStrip toolStrip1;
        private ToolStripButton runButton;
        private ToolStripButton pauseButton;
        private ToolStripSeparator toolStripSeparator1;
        private ToolStripButton clearButton;
        private ToolStripButton bottomButton;
        private ToolStripButton captureFileButton;
        private System.Windows.Forms.Splitter Splitter;

        /// <summary>
        /// Constructor.
        /// </summary>
        public MainForm()
        {
            InitializeComponent();
        }

        /// <summary>
        /// Clean up any resources being used.
        /// </summary>
        protected override void Dispose(bool disposing)
        {
            if (disposing && components != null)
                components.Dispose();

            base.Dispose(disposing);
        }

        #region Windows Form Designer generated code
        /// <summary>
        /// Required method for Designer support - do not modify
        /// the contents of this method with the code editor.
        /// </summary>
        private void InitializeComponent()
        {
            System.ComponentModel.ComponentResourceManager resources = new System.ComponentModel.ComponentResourceManager(typeof(MainForm));
            this.components = new System.ComponentModel.Container();
            this.TraceList = new System.Windows.Forms.DataGrid();
            this.Time = new System.Windows.Forms.DataGridTableStyle();
            this.TimeCol = new System.Windows.Forms.DataGridTextBoxColumn();
            this.SourceEP = new System.Windows.Forms.DataGridTableStyle();
            this.Summary = new System.Windows.Forms.DataGridTableStyle();
            this.Splitter = new System.Windows.Forms.Splitter();
            this.TraceDetail = new System.Windows.Forms.TextBox();
            this.MainForm_Fill_Panel = new System.Windows.Forms.Panel();
            this.toolStripContainer1 = new System.Windows.Forms.ToolStripContainer();
            this.toolStripContainer2 = new System.Windows.Forms.ToolStripContainer();
            this.toolStripContainer3 = new System.Windows.Forms.ToolStripContainer();
            this.toolStrip1 = new System.Windows.Forms.ToolStrip();
            this.runButton = new System.Windows.Forms.ToolStripButton();
            this.pauseButton = new System.Windows.Forms.ToolStripButton();
            this.toolStripSeparator1 = new System.Windows.Forms.ToolStripSeparator();
            this.clearButton = new System.Windows.Forms.ToolStripButton();
            this.bottomButton = new System.Windows.Forms.ToolStripButton();
            this.captureFileButton = new System.Windows.Forms.ToolStripButton();
            ((System.ComponentModel.ISupportInitialize)(this.TraceList)).BeginInit();
            this.MainForm_Fill_Panel.SuspendLayout();
            this.toolStripContainer1.SuspendLayout();
            this.toolStripContainer2.SuspendLayout();
            this.toolStripContainer3.ContentPanel.SuspendLayout();
            this.toolStripContainer3.TopToolStripPanel.SuspendLayout();
            this.toolStripContainer3.SuspendLayout();
            this.toolStrip1.SuspendLayout();
            this.SuspendLayout();
            // 
            // TraceList
            // 
            this.TraceList.CaptionVisible = false;
            this.TraceList.DataMember = "";
            this.TraceList.Dock = System.Windows.Forms.DockStyle.Top;
            this.TraceList.HeaderForeColor = System.Drawing.SystemColors.ControlText;
            this.TraceList.Location = new System.Drawing.Point(0, 0);
            this.TraceList.Name = "TraceList";
            this.TraceList.ReadOnly = true;
            this.TraceList.Size = new System.Drawing.Size(656, 216);
            this.TraceList.TabIndex = 0;
            this.TraceList.TableStyles.AddRange(new System.Windows.Forms.DataGridTableStyle[] {
            this.Time,
            this.SourceEP,
            this.Summary});
            this.TraceList.CurrentCellChanged += new System.EventHandler(this.TraceList_CurrentCellChanged);
            // 
            // Time
            // 
            this.Time.DataGrid = this.TraceList;
            this.Time.GridColumnStyles.AddRange(new System.Windows.Forms.DataGridColumnStyle[] {
            this.TimeCol});
            this.Time.HeaderForeColor = System.Drawing.SystemColors.ControlText;
            this.Time.MappingName = "Time";
            this.Time.ReadOnly = true;
            // 
            // TimeCol
            // 
            this.TimeCol.Alignment = System.Windows.Forms.HorizontalAlignment.Center;
            this.TimeCol.Format = "{0:T}";
            this.TimeCol.FormatInfo = null;
            this.TimeCol.Width = 75;
            // 
            // SourceEP
            // 
            this.SourceEP.DataGrid = this.TraceList;
            this.SourceEP.HeaderForeColor = System.Drawing.SystemColors.ControlText;
            this.SourceEP.MappingName = "SourceEP";
            this.SourceEP.ReadOnly = true;
            // 
            // Summary
            // 
            this.Summary.DataGrid = this.TraceList;
            this.Summary.HeaderForeColor = System.Drawing.SystemColors.ControlText;
            this.Summary.MappingName = "Summary";
            this.Summary.ReadOnly = true;
            // 
            // Splitter
            // 
            this.Splitter.Dock = System.Windows.Forms.DockStyle.Top;
            this.Splitter.Location = new System.Drawing.Point(0, 216);
            this.Splitter.Name = "Splitter";
            this.Splitter.Size = new System.Drawing.Size(656, 3);
            this.Splitter.TabIndex = 1;
            this.Splitter.TabStop = false;
            // 
            // TraceDetail
            // 
            this.TraceDetail.Dock = System.Windows.Forms.DockStyle.Fill;
            this.TraceDetail.Font = new System.Drawing.Font("Courier New", 9.75F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            this.TraceDetail.Location = new System.Drawing.Point(0, 219);
            this.TraceDetail.Multiline = true;
            this.TraceDetail.Name = "TraceDetail";
            this.TraceDetail.ScrollBars = System.Windows.Forms.ScrollBars.Both;
            this.TraceDetail.Size = new System.Drawing.Size(656, 233);
            this.TraceDetail.TabIndex = 2;
            this.TraceDetail.WordWrap = false;
            // 
            // MainForm_Fill_Panel
            // 
            this.MainForm_Fill_Panel.Controls.Add(this.TraceDetail);
            this.MainForm_Fill_Panel.Controls.Add(this.Splitter);
            this.MainForm_Fill_Panel.Controls.Add(this.TraceList);
            this.MainForm_Fill_Panel.Cursor = System.Windows.Forms.Cursors.Default;
            this.MainForm_Fill_Panel.Dock = System.Windows.Forms.DockStyle.Fill;
            this.MainForm_Fill_Panel.Location = new System.Drawing.Point(0, 0);
            this.MainForm_Fill_Panel.Name = "MainForm_Fill_Panel";
            this.MainForm_Fill_Panel.Size = new System.Drawing.Size(656, 452);
            this.MainForm_Fill_Panel.TabIndex = 0;
            // 
            // toolStripContainer1
            // 
            // 
            // toolStripContainer1.ContentPanel
            // 
            this.toolStripContainer1.ContentPanel.Size = new System.Drawing.Size(656, 427);
            this.toolStripContainer1.Dock = System.Windows.Forms.DockStyle.Fill;
            this.toolStripContainer1.Location = new System.Drawing.Point(0, 0);
            this.toolStripContainer1.Name = "toolStripContainer1";
            this.toolStripContainer1.Size = new System.Drawing.Size(656, 452);
            this.toolStripContainer1.TabIndex = 4;
            this.toolStripContainer1.Text = "toolStripContainer1";
            // 
            // toolStripContainer2
            // 
            // 
            // toolStripContainer2.ContentPanel
            // 
            this.toolStripContainer2.ContentPanel.Size = new System.Drawing.Size(656, 427);
            this.toolStripContainer2.Dock = System.Windows.Forms.DockStyle.Fill;
            this.toolStripContainer2.Location = new System.Drawing.Point(0, 0);
            this.toolStripContainer2.Name = "toolStripContainer2";
            this.toolStripContainer2.Size = new System.Drawing.Size(656, 452);
            this.toolStripContainer2.TabIndex = 3;
            this.toolStripContainer2.Text = "toolStripContainer2";
            // 
            // toolStripContainer3
            // 
            // 
            // toolStripContainer3.ContentPanel
            // 
            this.toolStripContainer3.ContentPanel.AutoScroll = true;
            this.toolStripContainer3.ContentPanel.Controls.Add(this.MainForm_Fill_Panel);
            this.toolStripContainer3.ContentPanel.Controls.Add(this.toolStripContainer1);
            this.toolStripContainer3.ContentPanel.Controls.Add(this.toolStripContainer2);
            this.toolStripContainer3.ContentPanel.Size = new System.Drawing.Size(656, 452);
            this.toolStripContainer3.Dock = System.Windows.Forms.DockStyle.Fill;
            this.toolStripContainer3.Location = new System.Drawing.Point(0, 0);
            this.toolStripContainer3.Name = "toolStripContainer3";
            this.toolStripContainer3.Size = new System.Drawing.Size(656, 477);
            this.toolStripContainer3.TabIndex = 3;
            this.toolStripContainer3.Text = "toolStripContainer3";
            // 
            // toolStripContainer3.TopToolStripPanel
            // 
            this.toolStripContainer3.TopToolStripPanel.Controls.Add(this.toolStrip1);
            // 
            // toolStrip1
            // 
            this.toolStrip1.Dock = System.Windows.Forms.DockStyle.None;
            this.toolStrip1.Items.AddRange(new System.Windows.Forms.ToolStripItem[] {
            this.runButton,
            this.pauseButton,
            this.toolStripSeparator1,
            this.clearButton,
            this.bottomButton,
            this.captureFileButton});
            this.toolStrip1.Location = new System.Drawing.Point(3, 0);
            this.toolStrip1.Name = "toolStrip1";
            this.toolStrip1.Size = new System.Drawing.Size(295, 25);
            this.toolStrip1.TabIndex = 0;
            // 
            // runButton
            // 
            this.runButton.DisplayStyle = System.Windows.Forms.ToolStripItemDisplayStyle.Text;
            this.runButton.Image = ((System.Drawing.Image)(resources.GetObject("runButton.Image")));
            this.runButton.ImageTransparentColor = System.Drawing.Color.Magenta;
            this.runButton.Name = "runButton";
            this.runButton.Size = new System.Drawing.Size(32, 22);
            this.runButton.Tag = "Run";
            this.runButton.Text = "Run";
            this.runButton.ToolTipText = "Start capture";
            this.runButton.Click += new System.EventHandler(this.runButton_Click);
            // 
            // pauseButton
            // 
            this.pauseButton.DisplayStyle = System.Windows.Forms.ToolStripItemDisplayStyle.Text;
            this.pauseButton.Image = ((System.Drawing.Image)(resources.GetObject("pauseButton.Image")));
            this.pauseButton.ImageTransparentColor = System.Drawing.Color.Magenta;
            this.pauseButton.Name = "pauseButton";
            this.pauseButton.Size = new System.Drawing.Size(42, 22);
            this.pauseButton.Tag = "Pause";
            this.pauseButton.Text = "Pause";
            this.pauseButton.ToolTipText = "Pause capture";
            this.pauseButton.Click += new System.EventHandler(this.pauseButton_Click);
            // 
            // toolStripSeparator1
            // 
            this.toolStripSeparator1.Name = "toolStripSeparator1";
            this.toolStripSeparator1.Size = new System.Drawing.Size(6, 25);
            // 
            // clearButton
            // 
            this.clearButton.DisplayStyle = System.Windows.Forms.ToolStripItemDisplayStyle.Text;
            this.clearButton.Image = ((System.Drawing.Image)(resources.GetObject("clearButton.Image")));
            this.clearButton.ImageTransparentColor = System.Drawing.Color.Magenta;
            this.clearButton.Name = "clearButton";
            this.clearButton.Size = new System.Drawing.Size(38, 22);
            this.clearButton.Tag = "Clear";
            this.clearButton.Text = "Clear";
            this.clearButton.ToolTipText = "Clear capture";
            this.clearButton.Click += new System.EventHandler(this.clearButton_Click);
            // 
            // bottomButton
            // 
            this.bottomButton.DisplayStyle = System.Windows.Forms.ToolStripItemDisplayStyle.Text;
            this.bottomButton.Image = ((System.Drawing.Image)(resources.GetObject("bottomButton.Image")));
            this.bottomButton.ImageTransparentColor = System.Drawing.Color.Magenta;
            this.bottomButton.Name = "bottomButton";
            this.bottomButton.Size = new System.Drawing.Size(51, 22);
            this.bottomButton.Tag = "Bottom";
            this.bottomButton.Text = "Bottom";
            this.bottomButton.ToolTipText = "Jump to bottom of capture";
            this.bottomButton.Click += new System.EventHandler(this.bottomButton_Click);
            // 
            // captureFileButton
            // 
            this.captureFileButton.DisplayStyle = System.Windows.Forms.ToolStripItemDisplayStyle.Text;
            this.captureFileButton.Image = ((System.Drawing.Image)(resources.GetObject("captureFileButton.Image")));
            this.captureFileButton.ImageTransparentColor = System.Drawing.Color.Magenta;
            this.captureFileButton.Name = "captureFileButton";
            this.captureFileButton.Size = new System.Drawing.Size(83, 22);
            this.captureFileButton.Tag = "CaptureFile";
            this.captureFileButton.Text = "Capture File...";
            this.captureFileButton.ToolTipText = "Capture to file";
            this.captureFileButton.Click += new System.EventHandler(this.captureFileButton_Click);
            // 
            // MainForm
            // 
            this.AutoScaleBaseSize = new System.Drawing.Size(5, 13);
            this.ClientSize = new System.Drawing.Size(656, 477);
            this.Controls.Add(this.toolStripContainer3);
            this.Icon = ((System.Drawing.Icon)(resources.GetObject("$this.Icon")));
            this.Name = "MainForm";
            this.Text = "LillTek NetTrace Console";
            this.FormClosing += new System.Windows.Forms.FormClosingEventHandler(this.MainForm_FormClosing);
            this.Load += new System.EventHandler(this.MainForm_Load);
            ((System.ComponentModel.ISupportInitialize)(this.TraceList)).EndInit();
            this.MainForm_Fill_Panel.ResumeLayout(false);
            this.MainForm_Fill_Panel.PerformLayout();
            this.toolStripContainer1.ResumeLayout(false);
            this.toolStripContainer1.PerformLayout();
            this.toolStripContainer2.ResumeLayout(false);
            this.toolStripContainer2.PerformLayout();
            this.toolStripContainer3.ContentPanel.ResumeLayout(false);
            this.toolStripContainer3.TopToolStripPanel.ResumeLayout(false);
            this.toolStripContainer3.TopToolStripPanel.PerformLayout();
            this.toolStripContainer3.ResumeLayout(false);
            this.toolStripContainer3.PerformLayout();
            this.toolStrip1.ResumeLayout(false);
            this.toolStrip1.PerformLayout();
            this.ResumeLayout(false);

        }
        #endregion

        /// <summary>
        /// Called on form load.
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="args"></param>
        private void MainForm_Load(object sender, System.EventArgs args)
        {
            DataColumnCollection    dtColumns;
            DataGridTableStyle      dgStyle;
            DataGridTextBoxColumn   col;

            // Initialize the data table and set

            dtCapture = new DataTable("capture");
            dtColumns = dtCapture.Columns;
            dtColumns.Add("Time", typeof(DateTime));
            dtColumns.Add("SourceEP", typeof(string));
            dtColumns.Add("Event", typeof(string));
            dtColumns.Add("Summary", typeof(string));
            dtColumns.Add("Details", typeof(string));

            // Bind these to the trace grid

            TraceList.DataSource = dtCapture;

            // Configure the grid columns

            dgStyle = new DataGridTableStyle();
            dgStyle.MappingName = dtCapture.TableName;
            dgStyle.ReadOnly = true;
            dgStyle.AllowSorting = false;

            // Time

            col = new DataGridTextBoxColumn();
            col.Alignment = HorizontalAlignment.Center;
            col.HeaderText = "Time (UTC)";
            col.MappingName = "Time";
            col.NullText = string.Empty;
            col.ReadOnly = true;
            col.Width = 100;
            col.Format = "HH:mm:ss.fff";
            dgStyle.GridColumnStyles.Add(col);

            // SourceEP

            col = new DataGridTextBoxColumn();
            col.Alignment = HorizontalAlignment.Center;
            col.HeaderText = "SourceEP";
            col.MappingName = "SourceEP";
            col.NullText = string.Empty;
            col.ReadOnly = true;
            col.Width = 75;
            dgStyle.GridColumnStyles.Add(col);

            // Event

            col = new DataGridTextBoxColumn();
            col.Alignment = HorizontalAlignment.Left;
            col.HeaderText = "Event";
            col.MappingName = "Event";
            col.NullText = string.Empty;
            col.ReadOnly = true;
            col.Width = 225;
            dgStyle.GridColumnStyles.Add(col);

            // Summary

            col = new DataGridTextBoxColumn();
            col.Alignment = HorizontalAlignment.Left;
            col.HeaderText = "Summary";
            col.MappingName = "Summary";
            col.NullText = string.Empty;
            col.ReadOnly = true;
            col.Width = 1600;
            dgStyle.GridColumnStyles.Add(col);

            // Details (hidden)

            col = new DataGridTextBoxColumn();
            col.Alignment = HorizontalAlignment.Left;
            col.HeaderText = "Details";
            col.MappingName = "Details";
            col.NullText = string.Empty;
            col.ReadOnly = true;
            col.Width = 0;
            dgStyle.GridColumnStyles.Add(col);

            TraceList.TableStyles.Clear();
            TraceList.TableStyles.Add(dgStyle);

            // Complete the initialization

            isRunning = true;
            captureFile = false;
            captureWriter = null;
            SetUIState();

            onTraceUI = new NetTraceSinkDelegate(OnTraceUI);
            NetTraceSink.Start(new NetTraceSinkDelegate(OnTrace));

#if SIMPACKETS
            NetTrace.Start();
            timer = new GatedTimer(new TimerCallback(OnTimer),null,TimeSpan.FromTicks(0),TimeSpan.FromSeconds(5.0));
#endif
        }

#if SIMPACKETS
        private void OnTimer(object o)
        {
            FormsHelper.Invoke(this,new TimerCallback(OnTimerUI),new object[] {null});
        }

        private int count = 0;

        private void OnTimerUI(object o)
        {
            NetTrace.Write("event","summary text","count=" + (count++).ToString() + "\r\n\r\nThis is a test\r\nof the emergency broadcasting system.\r\nThis is only a test.");
        }
#endif // SIMPACKETS

        /// <summary>
        /// Called by the NetTraceSink for the set of received packets.
        /// </summary>
        /// <param name="packets">The received trace packets.</param>
        private void OnTrace(NetTracePacket[] packets)
        {

            ApplicationHost.Invoke(this, onTraceUI, new object[] { packets });
        }

        /// <summary>
        /// Handles the reception of a trace packet on the UI thread.
        /// </summary>
        /// <param name="packets">The received trace packet.</param>
        private void OnTraceUI(NetTracePacket[] packets)
        {
            NetTracePacket  packet;
            int             curCount;
            int             curRow;
            bool            move;

            if (captureFile && captureWriter != null)
            {
                for (int i = 0; i < packets.Length; i++)
                {
                    packet = packets[i];
                    captureWriter.WriteLine(string.Format("{0} {1} {2} {3}", packet.ReceiveTime, packet.SourceEP.ToString(), packet.Event, packet.Summary));
                    captureWriter.WriteLine(packet.Details);
                    captureWriter.WriteLine("--------");
                }
            }

            if (!isRunning)
                return;


            // If we're selected row in the table is the last row, then 
            // we're going to make sure we move to the new row after adding 
            // it below.

            curCount = dtCapture.Rows.Count;
            curRow = TraceList.CurrentCell.RowNumber;
            move = curCount == 0 || curRow == curCount - 1;

            // Add the new trace packets to the capture table (and the TraceList).

            dtCapture.BeginLoadData();
            for (int i = 0; i < packets.Length; i++)
            {
                packet = packets[i];
                dtCapture.LoadDataRow(new object[] { packet.ReceiveTime, packet.SourceEP.ToString(), packet.Event, packet.Summary, packet.Details }, true);
            }
            dtCapture.EndLoadData();

            // Move to the new bottom row if we were at the bottom

            if (move)
                TraceList.CurrentRowIndex = dtCapture.Rows.Count - 1;

            // Make sure that the details box is filled for the current trace packet

            TraceList_CurrentCellChanged(null, null);
        }

        private void TraceList_CurrentCellChanged(object sender, System.EventArgs args)
        {
            CurrencyManager cm;

            if (dtCapture.Rows.Count == 0)
                TraceDetail.Text = string.Empty;
            else
            {
                cm = (CurrencyManager)this.BindingContext[TraceList.DataSource, TraceList.DataMember];
                TraceDetail.Text = dtCapture.Rows[cm.Position]["Details"].ToString();
            }
        }

        private void SetUIState()
        {
            clearButton.Enabled = true;
            captureFileButton.Enabled = true;
            pauseButton.Enabled = isRunning;
            runButton.Enabled = !isRunning;
            bottomButton.Enabled = true;
        }

        private void MainForm_FormClosing(object sender, FormClosingEventArgs args)
        {
            if (captureWriter != null)
            {
                captureWriter.Close();
                captureWriter = null;
            }
        }

        private void runButton_Click(object sender, EventArgs args)
        {
            isRunning = true;
            if (dtCapture.Rows.Count > 0)
                TraceList.CurrentRowIndex = dtCapture.Rows.Count - 1;

            if (captureFile)
            {
                captureWriter.Close();
                captureWriter = null;
                captureFile = false;
            }

            SetUIState();
        }

        private void pauseButton_Click(object sender, EventArgs args)
        {
            isRunning = false;

            if (captureFile)
            {
                captureWriter.Close();
                captureWriter = null;
                captureFile = false;
            }

            SetUIState();
        }

        private void clearButton_Click(object sender, EventArgs args)
        {
            dtCapture.Rows.Clear();
            TraceList_CurrentCellChanged(null, null);

            SetUIState();
        }

        private void bottomButton_Click(object sender, EventArgs args)
        {
            if (dtCapture.Rows.Count > 0)
                TraceList.CurrentRowIndex = dtCapture.Rows.Count - 1;

            SetUIState();
        }

        private void captureFileButton_Click(object sender, EventArgs args)
        {
            SaveFileDialog dialog;

            dialog = new SaveFileDialog();
            dialog.CheckPathExists = true;
            dialog.DefaultExt = "log";
            dialog.CreatePrompt = false;
            dialog.OverwritePrompt = true;
            dialog.RestoreDirectory = false;
            dialog.Filter = "Log Files (*.log)|*.log";
            dialog.FilterIndex = 1;

            if (dialog.ShowDialog() != DialogResult.OK)
                return;

            if (captureWriter != null)
            {
                captureWriter.Close();
                captureWriter = null;
                captureFile = false;
            }

            try
            {
                captureWriter = new StreamWriter(dialog.FileName);
            }
            catch (Exception e)
            {
                MessageBox.Show(e.Message, this.Text, MessageBoxButtons.OK, MessageBoxIcon.Error);
                return;
            }

            isRunning = false;
            captureFile = true;

            SetUIState();
        }
    }
}
