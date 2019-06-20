namespace LillTek.Test.Messaging {
    partial class QueryClientForm {
        /// <summary>
        /// Required designer variable.
        /// </summary>
        private System.ComponentModel.IContainer components = null;

        /// <summary>
        /// Clean up any resources being used.
        /// </summary>
        /// <param name="disposing"><c>true</c> if managed resources should be disposed; otherwise, false.</param>
        protected override void Dispose(bool disposing) {
            if (disposing && (components != null)) {
                components.Dispose();
            }
            base.Dispose(disposing);
        }

        #region Windows Form Designer generated code

        /// <summary>
        /// Required method for Designer support - do not modify
        /// the contents of this method with the code editor.
        /// </summary>
        private void InitializeComponent() {
            this.components = new System.ComponentModel.Container();
            this.stopButton = new System.Windows.Forms.Button();
            this.startButton = new System.Windows.Forms.Button();
            this.statusBox = new System.Windows.Forms.TextBox();
            this.parallelQueriesLabel = new System.Windows.Forms.Label();
            this.parallelQueriesBox = new System.Windows.Forms.TextBox();
            this.errorProvider = new System.Windows.Forms.ErrorProvider(this.components);
            this.label1 = new System.Windows.Forms.Label();
            this.payloadSizeBox = new System.Windows.Forms.TextBox();
            ((System.ComponentModel.ISupportInitialize) (this.errorProvider)).BeginInit();
            this.SuspendLayout();
            // 
            // stopButton
            // 
            this.stopButton.Location = new System.Drawing.Point(451,4);
            this.stopButton.Name = "stopButton";
            this.stopButton.Size = new System.Drawing.Size(75,23);
            this.stopButton.TabIndex = 5;
            this.stopButton.Text = "Stop";
            this.stopButton.UseVisualStyleBackColor = true;
            this.stopButton.Click += new System.EventHandler(this.stopButton_Click);
            // 
            // startButton
            // 
            this.startButton.Location = new System.Drawing.Point(370,4);
            this.startButton.Name = "startButton";
            this.startButton.Size = new System.Drawing.Size(75,23);
            this.startButton.TabIndex = 4;
            this.startButton.Text = "Start";
            this.startButton.UseVisualStyleBackColor = true;
            this.startButton.Click += new System.EventHandler(this.startButton_Click);
            // 
            // statusBox
            // 
            this.statusBox.BackColor = System.Drawing.Color.White;
            this.statusBox.Enabled = false;
            this.statusBox.Font = new System.Drawing.Font("Courier New",8.25F,System.Drawing.FontStyle.Regular,System.Drawing.GraphicsUnit.Point,((byte) (0)));
            this.statusBox.ForeColor = System.Drawing.Color.Black;
            this.statusBox.Location = new System.Drawing.Point(3,33);
            this.statusBox.Multiline = true;
            this.statusBox.Name = "statusBox";
            this.statusBox.ReadOnly = true;
            this.statusBox.Size = new System.Drawing.Size(523,267);
            this.statusBox.TabIndex = 6;
            // 
            // parallelQueriesLabel
            // 
            this.parallelQueriesLabel.AutoSize = true;
            this.parallelQueriesLabel.Location = new System.Drawing.Point(118,9);
            this.parallelQueriesLabel.Name = "parallelQueriesLabel";
            this.parallelQueriesLabel.Size = new System.Drawing.Size(83,13);
            this.parallelQueriesLabel.TabIndex = 2;
            this.parallelQueriesLabel.Text = "Parallel Queries:";
            // 
            // parallelQueriesBox
            // 
            this.parallelQueriesBox.Location = new System.Drawing.Point(207,6);
            this.parallelQueriesBox.Name = "parallelQueriesBox";
            this.parallelQueriesBox.Size = new System.Drawing.Size(42,20);
            this.parallelQueriesBox.TabIndex = 3;
            this.parallelQueriesBox.Text = "1";
            this.parallelQueriesBox.Validating += new System.ComponentModel.CancelEventHandler(this.parallelQueriesBox_Validating);
            // 
            // errorProvider
            // 
            this.errorProvider.ContainerControl = this;
            // 
            // label1
            // 
            this.label1.AutoSize = true;
            this.label1.Location = new System.Drawing.Point(8,9);
            this.label1.Name = "label1";
            this.label1.Size = new System.Drawing.Size(48,13);
            this.label1.TabIndex = 0;
            this.label1.Text = "Payload:";
            // 
            // payloadSizeBox
            // 
            this.payloadSizeBox.Location = new System.Drawing.Point(62,6);
            this.payloadSizeBox.Name = "payloadSizeBox";
            this.payloadSizeBox.Size = new System.Drawing.Size(47,20);
            this.payloadSizeBox.TabIndex = 1;
            this.payloadSizeBox.Text = "0";
            this.payloadSizeBox.Validating += new System.ComponentModel.CancelEventHandler(this.payloadSizeBox_Validating);
            // 
            // QueryClientForm
            // 
            this.AutoScaleDimensions = new System.Drawing.SizeF(6F,13F);
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.ClientSize = new System.Drawing.Size(528,304);
            this.Controls.Add(this.payloadSizeBox);
            this.Controls.Add(this.label1);
            this.Controls.Add(this.parallelQueriesBox);
            this.Controls.Add(this.parallelQueriesLabel);
            this.Controls.Add(this.stopButton);
            this.Controls.Add(this.startButton);
            this.Controls.Add(this.statusBox);
            this.FormBorderStyle = System.Windows.Forms.FormBorderStyle.None;
            this.Name = "QueryClientForm";
            this.Text = "Query: Client";
            this.Load += new System.EventHandler(this.QueryClientForm_Load);
            ((System.ComponentModel.ISupportInitialize) (this.errorProvider)).EndInit();
            this.ResumeLayout(false);
            this.PerformLayout();

        }

        #endregion

        private System.Windows.Forms.Button stopButton;
        private System.Windows.Forms.Button startButton;
        private System.Windows.Forms.TextBox statusBox;
        private System.Windows.Forms.Label parallelQueriesLabel;
        private System.Windows.Forms.TextBox parallelQueriesBox;
        private System.Windows.Forms.ErrorProvider errorProvider;
        private System.Windows.Forms.TextBox payloadSizeBox;
        private System.Windows.Forms.Label label1;
    }
}