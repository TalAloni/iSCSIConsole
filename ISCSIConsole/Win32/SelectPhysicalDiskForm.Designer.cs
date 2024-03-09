namespace ISCSIConsole
{
    partial class SelectPhysicalDiskForm
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
            System.ComponentModel.ComponentResourceManager resources = new System.ComponentModel.ComponentResourceManager(typeof(SelectPhysicalDiskForm));
            this.btnCancel = new System.Windows.Forms.Button();
            this.btnOK = new System.Windows.Forms.Button();
            this.chkReadOnly = new System.Windows.Forms.CheckBox();
            this.listPhysicalDisks = new System.Windows.Forms.ListView();
            this.columnDisk = new System.Windows.Forms.ColumnHeader();
            this.columnDescription = new System.Windows.Forms.ColumnHeader();
            this.columnSerialNumber = new System.Windows.Forms.ColumnHeader();
            this.columnSize = new System.Windows.Forms.ColumnHeader();
            this.SuspendLayout();
            // 
            // btnCancel
            // 
            this.btnCancel.DialogResult = System.Windows.Forms.DialogResult.Cancel;
            this.btnCancel.Location = new System.Drawing.Point(405, 191);
            this.btnCancel.Name = "btnCancel";
            this.btnCancel.Size = new System.Drawing.Size(75, 23);
            this.btnCancel.TabIndex = 3;
            this.btnCancel.Text = "Cancel";
            this.btnCancel.UseVisualStyleBackColor = true;
            this.btnCancel.Click += new System.EventHandler(this.btnCancel_Click);
            // 
            // btnOK
            // 
            this.btnOK.Location = new System.Drawing.Point(324, 191);
            this.btnOK.Name = "btnOK";
            this.btnOK.Size = new System.Drawing.Size(75, 23);
            this.btnOK.TabIndex = 2;
            this.btnOK.Text = "OK";
            this.btnOK.UseVisualStyleBackColor = true;
            this.btnOK.Click += new System.EventHandler(this.btnOK_Click);
            // 
            // chkReadOnly
            // 
            this.chkReadOnly.AutoSize = true;
            this.chkReadOnly.Location = new System.Drawing.Point(12, 195);
            this.chkReadOnly.Name = "chkReadOnly";
            this.chkReadOnly.Size = new System.Drawing.Size(76, 17);
            this.chkReadOnly.TabIndex = 1;
            this.chkReadOnly.Text = "Read Only";
            this.chkReadOnly.UseVisualStyleBackColor = true;
            // 
            // listPhysicalDisks
            // 
            this.listPhysicalDisks.Columns.AddRange(new System.Windows.Forms.ColumnHeader[] {
            this.columnDisk,
            this.columnDescription,
            this.columnSerialNumber,
            this.columnSize});
            this.listPhysicalDisks.FullRowSelect = true;
            this.listPhysicalDisks.Location = new System.Drawing.Point(12, 12);
            this.listPhysicalDisks.Name = "listPhysicalDisks";
            this.listPhysicalDisks.Size = new System.Drawing.Size(468, 173);
            this.listPhysicalDisks.TabIndex = 0;
            this.listPhysicalDisks.UseCompatibleStateImageBehavior = false;
            this.listPhysicalDisks.View = System.Windows.Forms.View.Details;
            this.listPhysicalDisks.ColumnWidthChanging += new System.Windows.Forms.ColumnWidthChangingEventHandler(this.listPhysicalDisks_ColumnWidthChanging);
            // 
            // columnDisk
            // 
            this.columnDisk.Text = "Disk";
            // 
            // columnDescription
            // 
            this.columnDescription.Text = "Description";
            this.columnDescription.Width = 210;
            // 
            // columnSerialNumber
            // 
            this.columnSerialNumber.Text = "Serial Number";
            this.columnSerialNumber.Width = 134;
            // 
            // columnSize
            // 
            this.columnSize.Text = "Size";
            this.columnSize.TextAlign = System.Windows.Forms.HorizontalAlignment.Right;
            // 
            // SelectPhysicalDiskForm
            // 
            this.AcceptButton = this.btnOK;
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.None;
            this.CancelButton = this.btnCancel;
            this.ClientSize = new System.Drawing.Size(494, 225);
            this.Controls.Add(this.listPhysicalDisks);
            this.Controls.Add(this.chkReadOnly);
            this.Controls.Add(this.btnOK);
            this.Controls.Add(this.btnCancel);
            this.FormBorderStyle = System.Windows.Forms.FormBorderStyle.FixedSingle;
            this.Icon = ((System.Drawing.Icon)(resources.GetObject("$this.Icon")));
            this.MaximizeBox = false;
            this.MaximumSize = new System.Drawing.Size(500, 250);
            this.MinimumSize = new System.Drawing.Size(500, 250);
            this.Name = "SelectPhysicalDiskForm";
            this.SizeGripStyle = System.Windows.Forms.SizeGripStyle.Hide;
            this.Text = "Select Physical Disk";
            this.Load += new System.EventHandler(this.SelectPhysicalDiskForm_Load);
            this.ResumeLayout(false);
            this.PerformLayout();

        }

        #endregion

        private System.Windows.Forms.Button btnCancel;
        private System.Windows.Forms.Button btnOK;
        private System.Windows.Forms.CheckBox chkReadOnly;
        private System.Windows.Forms.ListView listPhysicalDisks;
        private System.Windows.Forms.ColumnHeader columnDisk;
        private System.Windows.Forms.ColumnHeader columnDescription;
        private System.Windows.Forms.ColumnHeader columnSerialNumber;
        private System.Windows.Forms.ColumnHeader columnSize;
    }
}