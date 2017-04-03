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
            this.btnAddDevice = new System.Windows.Forms.Button();
            this.btnRemove = new System.Windows.Forms.Button();
            this.listDisks = new System.Windows.Forms.ListView();
            this.columnDescription = ((System.Windows.Forms.ColumnHeader)(new System.Windows.Forms.ColumnHeader()));
            this.columnPath = ((System.Windows.Forms.ColumnHeader)(new System.Windows.Forms.ColumnHeader()));
            this.btnCancel = new System.Windows.Forms.Button();
            this.btnOK = new System.Windows.Forms.Button();
            this.label2 = new System.Windows.Forms.Label();
            this.label1 = new System.Windows.Forms.Label();
            this.txtTargetIQN = new System.Windows.Forms.TextBox();
            this.SuspendLayout();
            // 
            // btnAddDevice
            // 
            this.btnAddDevice.Location = new System.Drawing.Point(417, 39);
            this.btnAddDevice.Name = "btnAddDevice";
            this.btnAddDevice.Size = new System.Drawing.Size(140, 23);
            this.btnAddDevice.TabIndex = 18;
            this.btnAddDevice.Text = "Add Device";
            this.btnAddDevice.UseVisualStyleBackColor = true;
            this.btnAddDevice.Click += new System.EventHandler(this.btnAddSPTI_Click);
            // 
            // btnRemove
            // 
            this.btnRemove.Location = new System.Drawing.Point(417, 68);
            this.btnRemove.Name = "btnRemove";
            this.btnRemove.Size = new System.Drawing.Size(140, 23);
            this.btnRemove.TabIndex = 15;
            this.btnRemove.Text = "Remove";
            this.btnRemove.UseVisualStyleBackColor = true;
            this.btnRemove.Click += new System.EventHandler(this.btnRemove_Click);
            // 
            // listDisks
            // 
            this.listDisks.AllowColumnReorder = true;
            this.listDisks.Columns.AddRange(new System.Windows.Forms.ColumnHeader[] {
            this.columnDescription,
            this.columnPath});
            this.listDisks.FullRowSelect = true;
            this.listDisks.Location = new System.Drawing.Point(61, 39);
            this.listDisks.MultiSelect = false;
            this.listDisks.Name = "listDisks";
            this.listDisks.Size = new System.Drawing.Size(350, 168);
            this.listDisks.TabIndex = 12;
            this.listDisks.UseCompatibleStateImageBehavior = false;
            this.listDisks.View = System.Windows.Forms.View.Details;
            // 
            // columnDescription
            // 
            this.columnDescription.Text = "Description";
            this.columnDescription.Width = 163;
            // 
            // columnPath
            // 
            this.columnPath.Text = "Path";
            this.columnPath.Width = 183;
            // 
            // btnCancel
            // 
            this.btnCancel.DialogResult = System.Windows.Forms.DialogResult.Cancel;
            this.btnCancel.Location = new System.Drawing.Point(482, 219);
            this.btnCancel.Name = "btnCancel";
            this.btnCancel.Size = new System.Drawing.Size(75, 23);
            this.btnCancel.TabIndex = 17;
            this.btnCancel.Text = "Cancel";
            this.btnCancel.UseVisualStyleBackColor = true;
            this.btnCancel.Click += new System.EventHandler(this.btnCancel_Click);
            // 
            // btnOK
            // 
            this.btnOK.Location = new System.Drawing.Point(401, 219);
            this.btnOK.Name = "btnOK";
            this.btnOK.Size = new System.Drawing.Size(75, 23);
            this.btnOK.TabIndex = 16;
            this.btnOK.Text = "OK";
            this.btnOK.UseVisualStyleBackColor = true;
            this.btnOK.Click += new System.EventHandler(this.btnOK_Click);
            // 
            // label2
            // 
            this.label2.AutoSize = true;
            this.label2.Location = new System.Drawing.Point(6, 44);
            this.label2.Name = "label2";
            this.label2.Size = new System.Drawing.Size(49, 13);
            this.label2.TabIndex = 14;
            this.label2.Text = "Devices:";
            // 
            // label1
            // 
            this.label1.AutoSize = true;
            this.label1.Location = new System.Drawing.Point(26, 10);
            this.label1.Name = "label1";
            this.label1.Size = new System.Drawing.Size(29, 13);
            this.label1.TabIndex = 13;
            this.label1.Text = "IQN:";
            // 
            // txtTargetIQN
            // 
            this.txtTargetIQN.Location = new System.Drawing.Point(61, 7);
            this.txtTargetIQN.Name = "txtTargetIQN";
            this.txtTargetIQN.Size = new System.Drawing.Size(275, 20);
            this.txtTargetIQN.TabIndex = 11;
            // 
            // AddSPTITargetForm
            // 
            this.AutoScaleDimensions = new System.Drawing.SizeF(6F, 13F);
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.ClientSize = new System.Drawing.Size(569, 254);
            this.Controls.Add(this.btnAddDevice);
            this.Controls.Add(this.btnRemove);
            this.Controls.Add(this.listDisks);
            this.Controls.Add(this.btnCancel);
            this.Controls.Add(this.btnOK);
            this.Controls.Add(this.label2);
            this.Controls.Add(this.label1);
            this.Controls.Add(this.txtTargetIQN);
            this.Icon = ((System.Drawing.Icon)(resources.GetObject("$this.Icon")));
            this.Name = "AddSPTITargetForm";
            this.Text = "Add SCSI Passthrough Target";
            this.Load += new System.EventHandler(this.AddSPTITargetForm_Load);
            this.ResumeLayout(false);
            this.PerformLayout();

        }

        #endregion

        private System.Windows.Forms.Button btnAddDevice;
        private System.Windows.Forms.Button btnRemove;
        private System.Windows.Forms.ListView listDisks;
        private System.Windows.Forms.ColumnHeader columnDescription;
        private System.Windows.Forms.ColumnHeader columnPath;
        private System.Windows.Forms.Button btnCancel;
        private System.Windows.Forms.Button btnOK;
        private System.Windows.Forms.Label label2;
        private System.Windows.Forms.Label label1;
        private System.Windows.Forms.TextBox txtTargetIQN;
    }
}