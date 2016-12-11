namespace ISCSIConsole
{
    partial class AddTargetForm
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
            System.ComponentModel.ComponentResourceManager resources = new System.ComponentModel.ComponentResourceManager(typeof(AddTargetForm));
            this.btnAddDiskImage = new System.Windows.Forms.Button();
            this.btnAddPhysicalDisk = new System.Windows.Forms.Button();
            this.txtTargetIQN = new System.Windows.Forms.TextBox();
            this.label1 = new System.Windows.Forms.Label();
            this.label2 = new System.Windows.Forms.Label();
            this.btnOK = new System.Windows.Forms.Button();
            this.btnCancel = new System.Windows.Forms.Button();
            this.listDisks = new System.Windows.Forms.ListView();
            this.columnDescription = new System.Windows.Forms.ColumnHeader();
            this.columnSize = new System.Windows.Forms.ColumnHeader();
            this.btnAddVolume = new System.Windows.Forms.Button();
            this.btnCreateDiskImage = new System.Windows.Forms.Button();
            this.btnRemove = new System.Windows.Forms.Button();
            this.SuspendLayout();
            // 
            // btnAddDiskImage
            // 
            this.btnAddDiskImage.Location = new System.Drawing.Point(340, 73);
            this.btnAddDiskImage.Name = "btnAddDiskImage";
            this.btnAddDiskImage.Size = new System.Drawing.Size(140, 23);
            this.btnAddDiskImage.TabIndex = 4;
            this.btnAddDiskImage.Text = "Add Existing Virtual Disk";
            this.btnAddDiskImage.UseVisualStyleBackColor = true;
            this.btnAddDiskImage.Click += new System.EventHandler(this.btnAddDiskImage_Click);
            // 
            // btnAddPhysicalDisk
            // 
            this.btnAddPhysicalDisk.Location = new System.Drawing.Point(340, 102);
            this.btnAddPhysicalDisk.Name = "btnAddPhysicalDisk";
            this.btnAddPhysicalDisk.Size = new System.Drawing.Size(140, 23);
            this.btnAddPhysicalDisk.TabIndex = 5;
            this.btnAddPhysicalDisk.Text = "Add Physical Disk";
            this.btnAddPhysicalDisk.UseVisualStyleBackColor = true;
            this.btnAddPhysicalDisk.Visible = false;
            this.btnAddPhysicalDisk.Click += new System.EventHandler(this.btnAddPhysicalDisk_Click);
            // 
            // txtTargetIQN
            // 
            this.txtTargetIQN.Location = new System.Drawing.Point(57, 12);
            this.txtTargetIQN.Name = "txtTargetIQN";
            this.txtTargetIQN.Size = new System.Drawing.Size(275, 20);
            this.txtTargetIQN.TabIndex = 1;
            // 
            // label1
            // 
            this.label1.AutoSize = true;
            this.label1.Location = new System.Drawing.Point(12, 15);
            this.label1.Name = "label1";
            this.label1.Size = new System.Drawing.Size(29, 13);
            this.label1.TabIndex = 4;
            this.label1.Text = "IQN:";
            // 
            // label2
            // 
            this.label2.AutoSize = true;
            this.label2.Location = new System.Drawing.Point(12, 51);
            this.label2.Name = "label2";
            this.label2.Size = new System.Drawing.Size(36, 13);
            this.label2.TabIndex = 6;
            this.label2.Text = "Disks:";
            // 
            // btnOK
            // 
            this.btnOK.Location = new System.Drawing.Point(324, 200);
            this.btnOK.Name = "btnOK";
            this.btnOK.Size = new System.Drawing.Size(75, 23);
            this.btnOK.TabIndex = 8;
            this.btnOK.Text = "OK";
            this.btnOK.UseVisualStyleBackColor = true;
            this.btnOK.Click += new System.EventHandler(this.btnOK_Click);
            // 
            // btnCancel
            // 
            this.btnCancel.DialogResult = System.Windows.Forms.DialogResult.Cancel;
            this.btnCancel.Location = new System.Drawing.Point(405, 200);
            this.btnCancel.Name = "btnCancel";
            this.btnCancel.Size = new System.Drawing.Size(75, 23);
            this.btnCancel.TabIndex = 9;
            this.btnCancel.Text = "Cancel";
            this.btnCancel.UseVisualStyleBackColor = true;
            this.btnCancel.Click += new System.EventHandler(this.btnCancel_Click);
            // 
            // listDisks
            // 
            this.listDisks.Columns.AddRange(new System.Windows.Forms.ColumnHeader[] {
            this.columnDescription,
            this.columnSize});
            this.listDisks.FullRowSelect = true;
            this.listDisks.Location = new System.Drawing.Point(57, 44);
            this.listDisks.MultiSelect = false;
            this.listDisks.Name = "listDisks";
            this.listDisks.Size = new System.Drawing.Size(275, 139);
            this.listDisks.TabIndex = 2;
            this.listDisks.UseCompatibleStateImageBehavior = false;
            this.listDisks.View = System.Windows.Forms.View.Details;
            this.listDisks.SelectedIndexChanged += new System.EventHandler(this.listDisks_SelectedIndexChanged);
            this.listDisks.ColumnWidthChanging += new System.Windows.Forms.ColumnWidthChangingEventHandler(this.listDisks_ColumnWidthChanging);
            // 
            // columnDescription
            // 
            this.columnDescription.Text = "Description";
            this.columnDescription.Width = 211;
            // 
            // columnSize
            // 
            this.columnSize.Text = "Size";
            // 
            // btnAddVolume
            // 
            this.btnAddVolume.Location = new System.Drawing.Point(340, 131);
            this.btnAddVolume.Name = "btnAddVolume";
            this.btnAddVolume.Size = new System.Drawing.Size(140, 23);
            this.btnAddVolume.TabIndex = 6;
            this.btnAddVolume.Text = "Add Volume";
            this.btnAddVolume.UseVisualStyleBackColor = true;
            this.btnAddVolume.Visible = false;
            this.btnAddVolume.Click += new System.EventHandler(this.btnAddVolume_Click);
            // 
            // btnCreateDiskImage
            // 
            this.btnCreateDiskImage.Location = new System.Drawing.Point(340, 44);
            this.btnCreateDiskImage.Name = "btnCreateDiskImage";
            this.btnCreateDiskImage.Size = new System.Drawing.Size(140, 23);
            this.btnCreateDiskImage.TabIndex = 3;
            this.btnCreateDiskImage.Text = "Create Virtual Disk";
            this.btnCreateDiskImage.UseVisualStyleBackColor = true;
            this.btnCreateDiskImage.Click += new System.EventHandler(this.btnCreateDiskImage_Click);
            // 
            // btnRemove
            // 
            this.btnRemove.Enabled = false;
            this.btnRemove.Location = new System.Drawing.Point(340, 160);
            this.btnRemove.Name = "btnRemove";
            this.btnRemove.Size = new System.Drawing.Size(140, 23);
            this.btnRemove.TabIndex = 7;
            this.btnRemove.Text = "Remove";
            this.btnRemove.UseVisualStyleBackColor = true;
            this.btnRemove.Click += new System.EventHandler(this.btnRemove_Click);
            // 
            // AddTargetForm
            // 
            this.AcceptButton = this.btnOK;
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.None;
            this.CancelButton = this.btnCancel;
            this.ClientSize = new System.Drawing.Size(494, 235);
            this.Controls.Add(this.btnRemove);
            this.Controls.Add(this.btnCreateDiskImage);
            this.Controls.Add(this.btnAddVolume);
            this.Controls.Add(this.listDisks);
            this.Controls.Add(this.btnCancel);
            this.Controls.Add(this.btnOK);
            this.Controls.Add(this.label2);
            this.Controls.Add(this.label1);
            this.Controls.Add(this.txtTargetIQN);
            this.Controls.Add(this.btnAddPhysicalDisk);
            this.Controls.Add(this.btnAddDiskImage);
            this.FormBorderStyle = System.Windows.Forms.FormBorderStyle.FixedSingle;
            this.Icon = ((System.Drawing.Icon)(resources.GetObject("$this.Icon")));
            this.MaximizeBox = false;
            this.MaximumSize = new System.Drawing.Size(500, 260);
            this.MinimumSize = new System.Drawing.Size(500, 260);
            this.Name = "AddTargetForm";
            this.SizeGripStyle = System.Windows.Forms.SizeGripStyle.Hide;
            this.Text = "Add iSCSI Target";
            this.Load += new System.EventHandler(this.AddTargetForm_Load);
            this.FormClosing += new System.Windows.Forms.FormClosingEventHandler(this.AddTargetForm_FormClosing);
            this.ResumeLayout(false);
            this.PerformLayout();

        }

        #endregion

        private System.Windows.Forms.Button btnAddDiskImage;
        private System.Windows.Forms.Button btnAddPhysicalDisk;
        private System.Windows.Forms.TextBox txtTargetIQN;
        private System.Windows.Forms.Label label1;
        private System.Windows.Forms.Label label2;
        private System.Windows.Forms.Button btnOK;
        private System.Windows.Forms.Button btnCancel;
        private System.Windows.Forms.ListView listDisks;
        private System.Windows.Forms.ColumnHeader columnDescription;
        private System.Windows.Forms.ColumnHeader columnSize;
        private System.Windows.Forms.Button btnAddVolume;
        private System.Windows.Forms.Button btnCreateDiskImage;
        private System.Windows.Forms.Button btnRemove;
    }
}