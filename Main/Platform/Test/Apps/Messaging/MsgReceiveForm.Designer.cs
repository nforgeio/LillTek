namespace LillTek.Test.Messaging {
    partial class MsgReceiveForm {
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
            this.errorProvider = new System.Windows.Forms.ErrorProvider(this.components);
            this.statusBox = new System.Windows.Forms.RichTextBox();
            this.endPointLabel = new System.Windows.Forms.Label();
            this.endPointBox = new System.Windows.Forms.TextBox();
            ((System.ComponentModel.ISupportInitialize) (this.errorProvider)).BeginInit();
            this.SuspendLayout();
            // 
            // stopButton
            // 
            this.stopButton.Location = new System.Drawing.Point(451,4);
            this.stopButton.Name = "stopButton";
            this.stopButton.Size = new System.Drawing.Size(75,23);
            this.stopButton.TabIndex = 3;
            this.stopButton.Text = "Stop";
            this.stopButton.UseVisualStyleBackColor = true;
            this.stopButton.Click += new System.EventHandler(this.stopButton_Click);
            // 
            // startButton
            // 
            this.startButton.Location = new System.Drawing.Point(370,4);
            this.startButton.Name = "startButton";
            this.startButton.Size = new System.Drawing.Size(75,23);
            this.startButton.TabIndex = 2;
            this.startButton.Text = "Start";
            this.startButton.UseVisualStyleBackColor = true;
            this.startButton.Click += new System.EventHandler(this.startButton_Click);
            // 
            // errorProvider
            // 
            this.errorProvider.ContainerControl = this;
            // 
            // statusBox
            // 
            this.statusBox.Font = new System.Drawing.Font("Courier New",8.25F,System.Drawing.FontStyle.Regular,System.Drawing.GraphicsUnit.Point,((byte) (0)));
            this.statusBox.Location = new System.Drawing.Point(3,33);
            this.statusBox.Name = "statusBox";
            this.statusBox.Size = new System.Drawing.Size(523,267);
            this.statusBox.TabIndex = 4;
            this.statusBox.Text = "";
            // 
            // endPointLabel
            // 
            this.endPointLabel.AutoSize = true;
            this.endPointLabel.Location = new System.Drawing.Point(12,9);
            this.endPointLabel.Name = "endPointLabel";
            this.endPointLabel.Size = new System.Drawing.Size(52,13);
            this.endPointLabel.TabIndex = 0;
            this.endPointLabel.Text = "Endpoint:";
            // 
            // endPointBox
            // 
            this.endPointBox.Location = new System.Drawing.Point(70,6);
            this.endPointBox.Name = "endPointBox";
            this.endPointBox.Size = new System.Drawing.Size(294,20);
            this.endPointBox.TabIndex = 1;
            this.endPointBox.Text = "logical://Test";
            // 
            // MsgReceiveForm
            // 
            this.AutoScaleDimensions = new System.Drawing.SizeF(6F,13F);
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.ClientSize = new System.Drawing.Size(528,304);
            this.Controls.Add(this.endPointBox);
            this.Controls.Add(this.endPointLabel);
            this.Controls.Add(this.statusBox);
            this.Controls.Add(this.stopButton);
            this.Controls.Add(this.startButton);
            this.FormBorderStyle = System.Windows.Forms.FormBorderStyle.None;
            this.Name = "MsgReceiveForm";
            this.Text = "Receive: Message";
            this.Load += new System.EventHandler(this.QueryClientForm_Load);
            ((System.ComponentModel.ISupportInitialize) (this.errorProvider)).EndInit();
            this.ResumeLayout(false);
            this.PerformLayout();

        }

        #endregion

        private System.Windows.Forms.Button stopButton;
        private System.Windows.Forms.Button startButton;
        private System.Windows.Forms.ErrorProvider errorProvider;
        private System.Windows.Forms.RichTextBox statusBox;
        private System.Windows.Forms.TextBox endPointBox;
        private System.Windows.Forms.Label endPointLabel;
    }
}