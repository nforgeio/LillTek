namespace LillTek.Test.Messaging {
    partial class QueryServerForm {
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
            this.statusBox = new System.Windows.Forms.TextBox();
            this.startButton = new System.Windows.Forms.Button();
            this.stopButton = new System.Windows.Forms.Button();
            this.payloadSizeBox = new System.Windows.Forms.TextBox();
            this.label1 = new System.Windows.Forms.Label();
            this.syncCheckBox = new System.Windows.Forms.CheckBox();
            this.label2 = new System.Windows.Forms.Label();
            this.delayBox = new System.Windows.Forms.TextBox();
            this.errorProvider = new System.Windows.Forms.ErrorProvider(this.components);
            ((System.ComponentModel.ISupportInitialize) (this.errorProvider)).BeginInit();
            this.SuspendLayout();
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
            this.statusBox.TabIndex = 7;
            // 
            // startButton
            // 
            this.startButton.Location = new System.Drawing.Point(370,4);
            this.startButton.Name = "startButton";
            this.startButton.Size = new System.Drawing.Size(75,23);
            this.startButton.TabIndex = 5;
            this.startButton.Text = "Start";
            this.startButton.UseVisualStyleBackColor = true;
            this.startButton.Click += new System.EventHandler(this.startButton_Click);
            // 
            // stopButton
            // 
            this.stopButton.Location = new System.Drawing.Point(451,4);
            this.stopButton.Name = "stopButton";
            this.stopButton.Size = new System.Drawing.Size(75,23);
            this.stopButton.TabIndex = 6;
            this.stopButton.Text = "Stop";
            this.stopButton.UseVisualStyleBackColor = true;
            this.stopButton.Click += new System.EventHandler(this.stopButton_Click);
            // 
            // payloadSizeBox
            // 
            this.payloadSizeBox.Location = new System.Drawing.Point(62,7);
            this.payloadSizeBox.Name = "payloadSizeBox";
            this.payloadSizeBox.Size = new System.Drawing.Size(47,20);
            this.payloadSizeBox.TabIndex = 1;
            this.payloadSizeBox.Text = "0";
            this.payloadSizeBox.Validating += new System.ComponentModel.CancelEventHandler(this.payloadSizeBox_Validating);
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
            // syncCheckBox
            // 
            this.syncCheckBox.AutoSize = true;
            this.syncCheckBox.Location = new System.Drawing.Point(229,8);
            this.syncCheckBox.Name = "syncCheckBox";
            this.syncCheckBox.Size = new System.Drawing.Size(88,17);
            this.syncCheckBox.TabIndex = 4;
            this.syncCheckBox.Text = "Synchronous";
            this.syncCheckBox.UseVisualStyleBackColor = true;
            this.syncCheckBox.CheckedChanged += new System.EventHandler(this.syncCheckBox_CheckedChanged);
            // 
            // label2
            // 
            this.label2.AutoSize = true;
            this.label2.Location = new System.Drawing.Point(115,10);
            this.label2.Name = "label2";
            this.label2.Size = new System.Drawing.Size(37,13);
            this.label2.TabIndex = 2;
            this.label2.Text = "Delay:";
            // 
            // delayBox
            // 
            this.delayBox.Location = new System.Drawing.Point(159,6);
            this.delayBox.Name = "delayBox";
            this.delayBox.Size = new System.Drawing.Size(56,20);
            this.delayBox.TabIndex = 3;
            this.delayBox.Text = "0";
            this.delayBox.Validating += new System.ComponentModel.CancelEventHandler(this.delayBox_Validating);
            // 
            // errorProvider
            // 
            this.errorProvider.ContainerControl = this;
            // 
            // QueryServerForm
            // 
            this.AutoScaleDimensions = new System.Drawing.SizeF(6F,13F);
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.ClientSize = new System.Drawing.Size(528,304);
            this.Controls.Add(this.delayBox);
            this.Controls.Add(this.label2);
            this.Controls.Add(this.syncCheckBox);
            this.Controls.Add(this.payloadSizeBox);
            this.Controls.Add(this.label1);
            this.Controls.Add(this.stopButton);
            this.Controls.Add(this.startButton);
            this.Controls.Add(this.statusBox);
            this.FormBorderStyle = System.Windows.Forms.FormBorderStyle.None;
            this.Name = "QueryServerForm";
            this.Text = "Query: Server";
            this.Load += new System.EventHandler(this.QueryForm_Load);
            ((System.ComponentModel.ISupportInitialize) (this.errorProvider)).EndInit();
            this.ResumeLayout(false);
            this.PerformLayout();

        }

        #endregion

        private System.Windows.Forms.TextBox statusBox;
        private System.Windows.Forms.Button startButton;
        private System.Windows.Forms.Button stopButton;
        private System.Windows.Forms.TextBox payloadSizeBox;
        private System.Windows.Forms.Label label1;
        private System.Windows.Forms.CheckBox syncCheckBox;
        private System.Windows.Forms.Label label2;
        private System.Windows.Forms.TextBox delayBox;
        private System.Windows.Forms.ErrorProvider errorProvider;


    }
}