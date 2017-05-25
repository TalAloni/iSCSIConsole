namespace ISCSIConsole
{
    partial class AddSPTITargetForm
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
            System.ComponentModel.ComponentResourceManager resources = new System.ComponentModel.ComponentResourceManager(typeof(AddSPTITargetForm));
            this.DevicePanel = new System.Windows.Forms.Panel();
            this.listDisks = new System.Windows.Forms.ListView();
            this.columnDescription = ((System.Windows.Forms.ColumnHeader)(new System.Windows.Forms.ColumnHeader()));
            this.columnPath = ((System.Windows.Forms.ColumnHeader)(new System.Windows.Forms.ColumnHeader()));
            this.lblDevices = new System.Windows.Forms.Label();
            this.lblIQN = new System.Windows.Forms.Label();
            this.txtTargetIQN = new System.Windows.Forms.TextBox();
            this.ButtonPanel = new System.Windows.Forms.Panel();
            this.btnCancel = new System.Windows.Forms.Button();
            this.btnAddDevice = new System.Windows.Forms.Button();
            this.DevicePanel.SuspendLayout();
            this.ButtonPanel.SuspendLayout();
            this.SuspendLayout();
            // 
            // DevicePanel
            // 
            this.DevicePanel.Anchor = ((System.Windows.Forms.AnchorStyles)((((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Bottom) 
            | System.Windows.Forms.AnchorStyles.Left) 
            | System.Windows.Forms.AnchorStyles.Right)));
            this.DevicePanel.Controls.Add(this.listDisks);
            this.DevicePanel.Controls.Add(this.lblDevices);
            this.DevicePanel.Controls.Add(this.lblIQN);
            this.DevicePanel.Controls.Add(this.txtTargetIQN);
            this.DevicePanel.Location = new System.Drawing.Point(0, 0);
            this.DevicePanel.Name = "DevicePanel";
            this.DevicePanel.Size = new System.Drawing.Size(486, 213);
            this.DevicePanel.TabIndex = 18;
            // 
            // listDisks
            // 
            this.listDisks.AllowColumnReorder = true;
            this.listDisks.Anchor = ((System.Windows.Forms.AnchorStyles)((((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Bottom) 
            | System.Windows.Forms.AnchorStyles.Left) 
            | System.Windows.Forms.AnchorStyles.Right)));
            this.listDisks.Columns.AddRange(new System.Windows.Forms.ColumnHeader[] {
            this.columnDescription,
            this.columnPath});
            this.listDisks.FullRowSelect = true;
            this.listDisks.Location = new System.Drawing.Point(64, 38);
            this.listDisks.MultiSelect = false;
            this.listDisks.Name = "listDisks";
            this.listDisks.Size = new System.Drawing.Size(413, 168);
            this.listDisks.TabIndex = 16;
            this.listDisks.UseCompatibleStateImageBehavior = false;
            this.listDisks.View = System.Windows.Forms.View.Details;
            // 
            // columnDescription
            // 
            this.columnDescription.Text = "Description";
            this.columnDescription.Width = 205;
            // 
            // columnPath
            // 
            this.columnPath.Text = "Path";
            this.columnPath.Width = 204;
            // 
            // lblDevices
            // 
            this.lblDevices.AutoSize = true;
            this.lblDevices.Location = new System.Drawing.Point(9, 43);
            this.lblDevices.Name = "lblDevices";
            this.lblDevices.Size = new System.Drawing.Size(49, 13);
            this.lblDevices.TabIndex = 18;
            this.lblDevices.Text = "Devices:";
            // 
            // lblIQN
            // 
            this.lblIQN.AutoSize = true;
            this.lblIQN.Location = new System.Drawing.Point(29, 9);
            this.lblIQN.Name = "lblIQN";
            this.lblIQN.Size = new System.Drawing.Size(29, 13);
            this.lblIQN.TabIndex = 17;
            this.lblIQN.Text = "IQN:";
            // 
            // txtTargetIQN
            // 
            this.txtTargetIQN.Location = new System.Drawing.Point(64, 6);
            this.txtTargetIQN.Name = "txtTargetIQN";
            this.txtTargetIQN.Size = new System.Drawing.Size(275, 20);
            this.txtTargetIQN.TabIndex = 15;
            // 
            // ButtonPanel
            // 
            this.ButtonPanel.Controls.Add(this.btnCancel);
            this.ButtonPanel.Controls.Add(this.btnAddDevice);
            this.ButtonPanel.Dock = System.Windows.Forms.DockStyle.Bottom;
            this.ButtonPanel.Location = new System.Drawing.Point(0, 212);
            this.ButtonPanel.Name = "ButtonPanel";
            this.ButtonPanel.Size = new System.Drawing.Size(486, 42);
            this.ButtonPanel.TabIndex = 19;
            // 
            // btnCancel
            // 
            this.btnCancel.Anchor = ((System.Windows.Forms.AnchorStyles)(((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Bottom) 
            | System.Windows.Forms.AnchorStyles.Right)));
            this.btnCancel.DialogResult = System.Windows.Forms.DialogResult.Cancel;
            this.btnCancel.Location = new System.Drawing.Point(404, 7);
            this.btnCancel.Name = "btnCancel";
            this.btnCancel.Size = new System.Drawing.Size(75, 23);
            this.btnCancel.TabIndex = 19;
            this.btnCancel.Text = "Cancel";
            this.btnCancel.UseVisualStyleBackColor = true;
            this.btnCancel.Click += new System.EventHandler(this.btnCancel_Click);
            // 
            // btnAddDevice
            // 
            this.btnAddDevice.Anchor = ((System.Windows.Forms.AnchorStyles)(((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Bottom) 
            | System.Windows.Forms.AnchorStyles.Right)));
            this.btnAddDevice.Location = new System.Drawing.Point(323, 7);
            this.btnAddDevice.Name = "btnAddDevice";
            this.btnAddDevice.Size = new System.Drawing.Size(75, 23);
            this.btnAddDevice.TabIndex = 18;
            this.btnAddDevice.Text = "Add Device";
            this.btnAddDevice.UseVisualStyleBackColor = true;
            this.btnAddDevice.Click += new System.EventHandler(this.btnAddDevice_Click);
            // 
            // AddSPTITargetForm
            // 
            this.AutoScaleDimensions = new System.Drawing.SizeF(6F, 13F);
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.ClientSize = new System.Drawing.Size(486, 254);
            this.Controls.Add(this.ButtonPanel);
            this.Controls.Add(this.DevicePanel);
            this.Icon = ((System.Drawing.Icon)(resources.GetObject("$this.Icon")));
            this.Name = "AddSPTITargetForm";
            this.Text = "Add SCSI Passthrough Device";
            this.Load += new System.EventHandler(this.AddSPTITargetForm_Load);
            this.DevicePanel.ResumeLayout(false);
            this.DevicePanel.PerformLayout();
            this.ButtonPanel.ResumeLayout(false);
            this.ResumeLayout(false);

        }

        #endregion
        private System.Windows.Forms.Panel DevicePanel;
        private System.Windows.Forms.ListView listDisks;
        private System.Windows.Forms.ColumnHeader columnDescription;
        private System.Windows.Forms.ColumnHeader columnPath;
        private System.Windows.Forms.Label lblDevices;
        private System.Windows.Forms.Label lblIQN;
        private System.Windows.Forms.TextBox txtTargetIQN;
        private System.Windows.Forms.Panel ButtonPanel;
        private System.Windows.Forms.Button btnCancel;
        private System.Windows.Forms.Button btnAddDevice;
    }
}