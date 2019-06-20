namespace LillTek.Test.Messaging {
    partial class MessageQueueForm {
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
            this.errorProvider = new System.Windows.Forms.ErrorProvider(this.components);
            this.queueEPLabel = new System.Windows.Forms.Label();
            this.queueEPBox = new System.Windows.Forms.TextBox();
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
            // errorProvider
            // 
            this.errorProvider.ContainerControl = this;
            // 
            // queueEPLabel
            // 
            this.queueEPLabel.AutoSize = true;
            this.queueEPLabel.Location = new System.Drawing.Point(12,9);
            this.queueEPLabel.Name = "queueEPLabel";
            this.queueEPLabel.Size = new System.Drawing.Size(42,13);
            this.queueEPLabel.TabIndex = 7;
            this.queueEPLabel.Text = "Queue:";
            // 
            // queueEPBox
            // 
            this.queueEPBox.Location = new System.Drawing.Point(62,6);
            this.queueEPBox.Name = "queueEPBox";
            this.queueEPBox.Size = new System.Drawing.Size(302,20);
            this.queueEPBox.TabIndex = 8;
            this.queueEPBox.Text = "Queue1";
            // 
            // MessageQueueForm
            // 
            this.AutoScaleDimensions = new System.Drawing.SizeF(6F,13F);
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.ClientSize = new System.Drawing.Size(528,304);
            this.Controls.Add(this.queueEPBox);
            this.Controls.Add(this.queueEPLabel);
            this.Controls.Add(this.stopButton);
            this.Controls.Add(this.startButton);
            this.Controls.Add(this.statusBox);
            this.FormBorderStyle = System.Windows.Forms.FormBorderStyle.None;
            this.Name = "MessageQueueForm";
            this.Text = "Message Queue";
            this.Load += new System.EventHandler(this.MessageQueueForm_Load);
            ((System.ComponentModel.ISupportInitialize) (this.errorProvider)).EndInit();
            this.ResumeLayout(false);
            this.PerformLayout();

        }

        #endregion

        private System.Windows.Forms.Button stopButton;
        private System.Windows.Forms.Button startButton;
        private System.Windows.Forms.TextBox statusBox;
        private System.Windows.Forms.ErrorProvider errorProvider;
        private System.Windows.Forms.TextBox queueEPBox;
        private System.Windows.Forms.Label queueEPLabel;
    }
}