namespace ISCSIConsole
{
    partial class MainForm
    {
        /// <summary>
        /// Required designer variable.
        /// </summary>
        private System.ComponentModel.IContainer components = null;

        /// <summary>
        /// Clean up any resources being used.
        /// </summary>
        /// <param name="disposing">true if managed resources should be disposed; otherwise, false.</param>
        protected override void Dispose(bool disposing)
        {
            if (disposing && (components != null))
            {
                components.Dispose();
            }
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
            this.btnStart = new System.Windows.Forms.Button();
            this.lblPort = new System.Windows.Forms.Label();
            this.txtPort = new System.Windows.Forms.TextBox();
            this.lblIPAddress = new System.Windows.Forms.Label();
            this.comboIPAddress = new System.Windows.Forms.ComboBox();
            this.btnAddTarget = new System.Windows.Forms.Button();
            this.lblTargets = new System.Windows.Forms.Label();
            this.btnRemoveTarget = new System.Windows.Forms.Button();
            this.listTargets = new System.Windows.Forms.ListBox();
            this.statusStrip1 = new System.Windows.Forms.StatusStrip();
            this.lblStatus = new System.Windows.Forms.ToolStripStatusLabel();
            this.btnAddSPTITarget = new System.Windows.Forms.Button();
            this.statusStrip1.SuspendLayout();
            this.SuspendLayout();
            // 
            // btnStart
            // 
            this.btnStart.Location = new System.Drawing.Point(354, 12);
            this.btnStart.Name = "btnStart";
            this.btnStart.Size = new System.Drawing.Size(106, 23);
            this.btnStart.TabIndex = 0;
            this.btnStart.Text = "Start";
            this.btnStart.UseVisualStyleBackColor = true;
            this.btnStart.Click += new System.EventHandler(this.btnStart_Click);
            // 
            // lblPort
            // 
            this.lblPort.AutoSize = true;
            this.lblPort.Location = new System.Drawing.Point(232, 17);
            this.lblPort.Name = "lblPort";
            this.lblPort.Size = new System.Drawing.Size(53, 13);
            this.lblPort.TabIndex = 1;
            this.lblPort.Text = "TCP Port:";
            // 
            // txtPort
            // 
            this.txtPort.Location = new System.Drawing.Point(291, 14);
            this.txtPort.Name = "txtPort";
            this.txtPort.Size = new System.Drawing.Size(53, 20);
            this.txtPort.TabIndex = 2;
            this.txtPort.Text = "3260";
            // 
            // lblIPAddress
            // 
            this.lblIPAddress.AutoSize = true;
            this.lblIPAddress.Location = new System.Drawing.Point(12, 16);
            this.lblIPAddress.Name = "lblIPAddress";
            this.lblIPAddress.Size = new System.Drawing.Size(61, 13);
            this.lblIPAddress.TabIndex = 3;
            this.lblIPAddress.Text = "IP Address:";
            // 
            // comboIPAddress
            // 
            this.comboIPAddress.DropDownStyle = System.Windows.Forms.ComboBoxStyle.DropDownList;
            this.comboIPAddress.FormattingEnabled = true;
            this.comboIPAddress.Location = new System.Drawing.Point(79, 13);
            this.comboIPAddress.Name = "comboIPAddress";
            this.comboIPAddress.Size = new System.Drawing.Size(136, 21);
            this.comboIPAddress.TabIndex = 4;
            // 
            // btnAddTarget
            // 
            this.btnAddTarget.Location = new System.Drawing.Point(354, 57);
            this.btnAddTarget.Name = "btnAddTarget";
            this.btnAddTarget.Size = new System.Drawing.Size(106, 23);
            this.btnAddTarget.TabIndex = 5;
            this.btnAddTarget.Text = "Add Target";
            this.btnAddTarget.UseVisualStyleBackColor = true;
            this.btnAddTarget.Click += new System.EventHandler(this.btnAddTarget_Click);
            // 
            // lblTargets
            // 
            this.lblTargets.AutoSize = true;
            this.lblTargets.Location = new System.Drawing.Point(12, 57);
            this.lblTargets.Name = "lblTargets";
            this.lblTargets.Size = new System.Drawing.Size(46, 13);
            this.lblTargets.TabIndex = 7;
            this.lblTargets.Text = "Targets:";
            // 
            // btnRemoveTarget
            // 
            this.btnRemoveTarget.Enabled = false;
            this.btnRemoveTarget.Location = new System.Drawing.Point(354, 115);
            this.btnRemoveTarget.Name = "btnRemoveTarget";
            this.btnRemoveTarget.Size = new System.Drawing.Size(106, 23);
            this.btnRemoveTarget.TabIndex = 8;
            this.btnRemoveTarget.Text = "Remove Target";
            this.btnRemoveTarget.UseVisualStyleBackColor = true;
            this.btnRemoveTarget.Click += new System.EventHandler(this.btnRemoveTarget_Click);
            // 
            // listTargets
            // 
            this.listTargets.FormattingEnabled = true;
            this.listTargets.Location = new System.Drawing.Point(79, 57);
            this.listTargets.Name = "listTargets";
            this.listTargets.Size = new System.Drawing.Size(265, 95);
            this.listTargets.TabIndex = 9;
            this.listTargets.SelectedIndexChanged += new System.EventHandler(this.listTargets_SelectedIndexChanged);
            // 
            // statusStrip1
            // 
            this.statusStrip1.Items.AddRange(new System.Windows.Forms.ToolStripItem[] {
            this.lblStatus});
            this.statusStrip1.Location = new System.Drawing.Point(0, 173);
            this.statusStrip1.Name = "statusStrip1";
            this.statusStrip1.Size = new System.Drawing.Size(474, 22);
            this.statusStrip1.SizingGrip = false;
            this.statusStrip1.TabIndex = 10;
            this.statusStrip1.Text = "statusStrip1";
            // 
            // lblActiveSessions
            // 
            this.lblStatus.Name = "lblActiveSessions";
            this.lblStatus.Size = new System.Drawing.Size(0, 17);
            // 
            // btnAddSPTITarget
            // 
            this.btnAddSPTITarget.Location = new System.Drawing.Point(354, 86);
            this.btnAddSPTITarget.Name = "btnAddSPTITarget";
            this.btnAddSPTITarget.Size = new System.Drawing.Size(106, 23);
            this.btnAddSPTITarget.TabIndex = 11;
            this.btnAddSPTITarget.Text = "Add SPTI Device";
            this.btnAddSPTITarget.UseVisualStyleBackColor = true;
            this.btnAddSPTITarget.Visible = false;
            this.btnAddSPTITarget.Click += new System.EventHandler(this.btnAddSPTIDevice_Click);
            // 
            // MainForm
            // 
            this.AcceptButton = this.btnStart;
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.None;
            this.ClientSize = new System.Drawing.Size(474, 195);
            this.Controls.Add(this.btnAddSPTITarget);
            this.Controls.Add(this.statusStrip1);
            this.Controls.Add(this.listTargets);
            this.Controls.Add(this.btnRemoveTarget);
            this.Controls.Add(this.lblTargets);
            this.Controls.Add(this.btnAddTarget);
            this.Controls.Add(this.comboIPAddress);
            this.Controls.Add(this.lblIPAddress);
            this.Controls.Add(this.txtPort);
            this.Controls.Add(this.lblPort);
            this.Controls.Add(this.btnStart);
            this.FormBorderStyle = System.Windows.Forms.FormBorderStyle.FixedSingle;
            this.Icon = ((System.Drawing.Icon)(resources.GetObject("$this.Icon")));
            this.MaximizeBox = false;
            this.MaximumSize = new System.Drawing.Size(480, 220);
            this.MinimumSize = new System.Drawing.Size(480, 220);
            this.Name = "MainForm";
            this.SizeGripStyle = System.Windows.Forms.SizeGripStyle.Hide;
            this.Text = "iSCSI Console";
            this.Load += new System.EventHandler(this.MainForm_Load);
            this.statusStrip1.ResumeLayout(false);
            this.statusStrip1.PerformLayout();
            this.ResumeLayout(false);
            this.PerformLayout();

        }

        #endregion

        private System.Windows.Forms.Button btnStart;
        private System.Windows.Forms.Label lblPort;
        private System.Windows.Forms.TextBox txtPort;
        private System.Windows.Forms.Label lblIPAddress;
        private System.Windows.Forms.ComboBox comboIPAddress;
        private System.Windows.Forms.Button btnAddTarget;
        private System.Windows.Forms.Label lblTargets;
        private System.Windows.Forms.Button btnRemoveTarget;
        private System.Windows.Forms.ListBox listTargets;
        private System.Windows.Forms.StatusStrip statusStrip1;
        private System.Windows.Forms.ToolStripStatusLabel lblStatus;
        private System.Windows.Forms.Button btnAddSPTITarget;
    }
}