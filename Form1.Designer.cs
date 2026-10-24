namespace StateRepositoryMgr
{
  partial class Form1
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
      this.btnMsConfig = new System.Windows.Forms.Button();
      this.lblBootMode = new System.Windows.Forms.Label();
      this.lblServiceStatus = new System.Windows.Forms.Label();
      this.grpSystem = new System.Windows.Forms.GroupBox();
      this.btnStopSrvc = new System.Windows.Forms.Button();
      this.btnRefresh = new System.Windows.Forms.Button();
      this.grpRepository = new System.Windows.Forms.GroupBox();
      this.lvFiles = new System.Windows.Forms.ListView();
      this.btnBrowse = new System.Windows.Forms.Button();
      this.label2 = new System.Windows.Forms.Label();
      this.label1 = new System.Windows.Forms.Label();
      this.txtWorkPath = new System.Windows.Forms.TextBox();
      this.txtLivePath = new System.Windows.Forms.TextBox();
      this.grpOperations = new System.Windows.Forms.GroupBox();
      this.lvPackages = new System.Windows.Forms.ListView();
      this.btnSearchPackage = new System.Windows.Forms.Button();
      this.lblSearchPackage = new System.Windows.Forms.Label();
      this.txtSearchPackage = new System.Windows.Forms.TextBox();
      this.btnRestore = new System.Windows.Forms.Button();
      this.btnReplace = new System.Windows.Forms.Button();
      this.btnRemovePackage = new System.Windows.Forms.Button();
      this.btnValidate = new System.Windows.Forms.Button();
      this.btnBackup = new System.Windows.Forms.Button();
      this.statusStrip1 = new System.Windows.Forms.StatusStrip();
      this.txtLog = new System.Windows.Forms.RichTextBox();
      this.txtEditedPath = new System.Windows.Forms.TextBox();
      this.label3 = new System.Windows.Forms.Label();
      this.grpSystem.SuspendLayout();
      this.grpRepository.SuspendLayout();
      this.grpOperations.SuspendLayout();
      this.SuspendLayout();
      // 
      // btnMsConfig
      // 
      this.btnMsConfig.Location = new System.Drawing.Point(487, 32);
      this.btnMsConfig.Name = "btnMsConfig";
      this.btnMsConfig.Size = new System.Drawing.Size(86, 23);
      this.btnMsConfig.TabIndex = 0;
      this.btnMsConfig.Text = "msconfig.exe";
      this.btnMsConfig.UseVisualStyleBackColor = true;
      this.btnMsConfig.Click += new System.EventHandler(this.btnMsConfig_Click);
      // 
      // lblBootMode
      // 
      this.lblBootMode.AutoSize = true;
      this.lblBootMode.Location = new System.Drawing.Point(6, 32);
      this.lblBootMode.Name = "lblBootMode";
      this.lblBootMode.Size = new System.Drawing.Size(103, 13);
      this.lblBootMode.TabIndex = 1;
      this.lblBootMode.Text = "Normal / Safe Mode";
      // 
      // lblServiceStatus
      // 
      this.lblServiceStatus.AutoSize = true;
      this.lblServiceStatus.Location = new System.Drawing.Point(124, 32);
      this.lblServiceStatus.Name = "lblServiceStatus";
      this.lblServiceStatus.Size = new System.Drawing.Size(98, 13);
      this.lblServiceStatus.TabIndex = 1;
      this.lblServiceStatus.Text = "Running / Stopped";
      // 
      // grpSystem
      // 
      this.grpSystem.Controls.Add(this.btnStopSrvc);
      this.grpSystem.Controls.Add(this.btnRefresh);
      this.grpSystem.Controls.Add(this.lblBootMode);
      this.grpSystem.Controls.Add(this.lblServiceStatus);
      this.grpSystem.Controls.Add(this.btnMsConfig);
      this.grpSystem.Location = new System.Drawing.Point(12, 12);
      this.grpSystem.Name = "grpSystem";
      this.grpSystem.Size = new System.Drawing.Size(673, 69);
      this.grpSystem.TabIndex = 2;
      this.grpSystem.TabStop = false;
      this.grpSystem.Text = "System Status";
      // 
      // btnStopSrvc
      // 
      this.btnStopSrvc.Location = new System.Drawing.Point(303, 32);
      this.btnStopSrvc.Name = "btnStopSrvc";
      this.btnStopSrvc.Size = new System.Drawing.Size(86, 23);
      this.btnStopSrvc.TabIndex = 3;
      this.btnStopSrvc.Text = "Stop Srvc";
      this.btnStopSrvc.UseVisualStyleBackColor = true;
      this.btnStopSrvc.Click += new System.EventHandler(this.btnStopSrvc_Click);
      // 
      // btnRefresh
      // 
      this.btnRefresh.Location = new System.Drawing.Point(395, 32);
      this.btnRefresh.Name = "btnRefresh";
      this.btnRefresh.Size = new System.Drawing.Size(86, 23);
      this.btnRefresh.TabIndex = 3;
      this.btnRefresh.Text = "Refresh";
      this.btnRefresh.UseVisualStyleBackColor = true;
      this.btnRefresh.Click += new System.EventHandler(this.btnRefresh_Click);
      // 
      // grpRepository
      // 
      this.grpRepository.Controls.Add(this.txtEditedPath);
      this.grpRepository.Controls.Add(this.lvFiles);
      this.grpRepository.Controls.Add(this.btnBrowse);
      this.grpRepository.Controls.Add(this.label3);
      this.grpRepository.Controls.Add(this.label2);
      this.grpRepository.Controls.Add(this.label1);
      this.grpRepository.Controls.Add(this.txtWorkPath);
      this.grpRepository.Controls.Add(this.txtLivePath);
      this.grpRepository.Location = new System.Drawing.Point(12, 87);
      this.grpRepository.Name = "grpRepository";
      this.grpRepository.Size = new System.Drawing.Size(673, 230);
      this.grpRepository.TabIndex = 3;
      this.grpRepository.TabStop = false;
      this.grpRepository.Text = "Repository Files";
      // 
      // lvFiles
      // 
      this.lvFiles.HideSelection = false;
      this.lvFiles.Location = new System.Drawing.Point(6, 109);
      this.lvFiles.Name = "lvFiles";
      this.lvFiles.Size = new System.Drawing.Size(661, 114);
      this.lvFiles.TabIndex = 4;
      this.lvFiles.UseCompatibleStateImageBehavior = false;
      // 
      // btnBrowse
      // 
      this.btnBrowse.Location = new System.Drawing.Point(498, 54);
      this.btnBrowse.Name = "btnBrowse";
      this.btnBrowse.Size = new System.Drawing.Size(75, 23);
      this.btnBrowse.TabIndex = 3;
      this.btnBrowse.Text = "Browse ...";
      this.btnBrowse.UseVisualStyleBackColor = true;
      // 
      // label2
      // 
      this.label2.AutoSize = true;
      this.label2.Location = new System.Drawing.Point(15, 59);
      this.label2.Name = "label2";
      this.label2.Size = new System.Drawing.Size(92, 13);
      this.label2.TabIndex = 2;
      this.label2.Text = "Arbeitsverzeichnis";
      // 
      // label1
      // 
      this.label1.AutoSize = true;
      this.label1.Location = new System.Drawing.Point(15, 33);
      this.label1.Name = "label1";
      this.label1.Size = new System.Drawing.Size(107, 13);
      this.label1.TabIndex = 2;
      this.label1.Text = "StateRepository-Pfad";
      // 
      // txtWorkPath
      // 
      this.txtWorkPath.Location = new System.Drawing.Point(127, 56);
      this.txtWorkPath.Name = "txtWorkPath";
      this.txtWorkPath.Size = new System.Drawing.Size(365, 20);
      this.txtWorkPath.TabIndex = 1;
      this.txtWorkPath.Text = "C:\\Temp\\StateRepoWork";
      // 
      // txtLivePath
      // 
      this.txtLivePath.Location = new System.Drawing.Point(127, 30);
      this.txtLivePath.Name = "txtLivePath";
      this.txtLivePath.ReadOnly = true;
      this.txtLivePath.Size = new System.Drawing.Size(365, 20);
      this.txtLivePath.TabIndex = 0;
      this.txtLivePath.Text = "C:\\ProgramData\\Microsoft\\Windows\\AppRepository";
      // 
      // grpOperations
      // 
      this.grpOperations.Controls.Add(this.lvPackages);
      this.grpOperations.Controls.Add(this.btnSearchPackage);
      this.grpOperations.Controls.Add(this.lblSearchPackage);
      this.grpOperations.Controls.Add(this.txtSearchPackage);
      this.grpOperations.Controls.Add(this.btnRestore);
      this.grpOperations.Controls.Add(this.btnReplace);
      this.grpOperations.Controls.Add(this.btnRemovePackage);
      this.grpOperations.Controls.Add(this.btnValidate);
      this.grpOperations.Controls.Add(this.btnBackup);
      this.grpOperations.Location = new System.Drawing.Point(12, 323);
      this.grpOperations.Name = "grpOperations";
      this.grpOperations.Size = new System.Drawing.Size(673, 182);
      this.grpOperations.TabIndex = 4;
      this.grpOperations.TabStop = false;
      this.grpOperations.Text = "Operations";
      // 
      // lvPackages
      // 
      this.lvPackages.HideSelection = false;
      this.lvPackages.Location = new System.Drawing.Point(12, 89);
      this.lvPackages.Name = "lvPackages";
      this.lvPackages.Size = new System.Drawing.Size(655, 84);
      this.lvPackages.TabIndex = 7;
      this.lvPackages.UseCompatibleStateImageBehavior = false;
      // 
      // btnSearchPackage
      // 
      this.btnSearchPackage.Location = new System.Drawing.Point(267, 60);
      this.btnSearchPackage.Name = "btnSearchPackage";
      this.btnSearchPackage.Size = new System.Drawing.Size(122, 23);
      this.btnSearchPackage.TabIndex = 6;
      this.btnSearchPackage.Text = "Search ...";
      this.btnSearchPackage.UseVisualStyleBackColor = true;
      this.btnSearchPackage.Click += new System.EventHandler(this.btnSearchPackage_Click);
      // 
      // lblSearchPackage
      // 
      this.lblSearchPackage.AutoSize = true;
      this.lblSearchPackage.Location = new System.Drawing.Point(14, 65);
      this.lblSearchPackage.Name = "lblSearchPackage";
      this.lblSearchPackage.Size = new System.Drawing.Size(90, 13);
      this.lblSearchPackage.TabIndex = 5;
      this.lblSearchPackage.Text = "Search Package:";
      // 
      // txtSearchPackage
      // 
      this.txtSearchPackage.Location = new System.Drawing.Point(138, 62);
      this.txtSearchPackage.Name = "txtSearchPackage";
      this.txtSearchPackage.Size = new System.Drawing.Size(123, 20);
      this.txtSearchPackage.TabIndex = 4;
      // 
      // btnRestore
      // 
      this.btnRestore.Location = new System.Drawing.Point(138, 29);
      this.btnRestore.Name = "btnRestore";
      this.btnRestore.Size = new System.Drawing.Size(123, 23);
      this.btnRestore.TabIndex = 3;
      this.btnRestore.Text = "Restore Backup";
      this.btnRestore.UseVisualStyleBackColor = true;
      this.btnRestore.Click += new System.EventHandler(this.btnRestore_Click);
      // 
      // btnReplace
      // 
      this.btnReplace.Location = new System.Drawing.Point(266, 29);
      this.btnReplace.Name = "btnReplace";
      this.btnReplace.Size = new System.Drawing.Size(123, 23);
      this.btnReplace.TabIndex = 3;
      this.btnReplace.Text = "Replace Repository";
      this.btnReplace.UseVisualStyleBackColor = true;
      this.btnReplace.Click += new System.EventHandler(this.btnReplace_Click);
      // 
      // btnRemovePackage
      // 
      this.btnRemovePackage.Location = new System.Drawing.Point(395, 60);
      this.btnRemovePackage.Name = "btnRemovePackage";
      this.btnRemovePackage.Size = new System.Drawing.Size(123, 23);
      this.btnRemovePackage.TabIndex = 3;
      this.btnRemovePackage.Text = "Remove Package";
      this.btnRemovePackage.UseVisualStyleBackColor = true;
      this.btnRemovePackage.Click += new System.EventHandler(this.btnRemovePackage_Click);
      // 
      // btnValidate
      // 
      this.btnValidate.Location = new System.Drawing.Point(395, 29);
      this.btnValidate.Name = "btnValidate";
      this.btnValidate.Size = new System.Drawing.Size(123, 23);
      this.btnValidate.TabIndex = 3;
      this.btnValidate.Text = "Validate Database";
      this.btnValidate.UseVisualStyleBackColor = true;
      // 
      // btnBackup
      // 
      this.btnBackup.Location = new System.Drawing.Point(9, 29);
      this.btnBackup.Name = "btnBackup";
      this.btnBackup.Size = new System.Drawing.Size(123, 23);
      this.btnBackup.TabIndex = 3;
      this.btnBackup.Text = "Create Backup";
      this.btnBackup.UseVisualStyleBackColor = true;
      this.btnBackup.Click += new System.EventHandler(this.btnBackup_Click);
      // 
      // statusStrip1
      // 
      this.statusStrip1.Location = new System.Drawing.Point(0, 654);
      this.statusStrip1.Name = "statusStrip1";
      this.statusStrip1.Size = new System.Drawing.Size(697, 22);
      this.statusStrip1.TabIndex = 5;
      this.statusStrip1.Text = "statusStrip1";
      // 
      // txtLog
      // 
      this.txtLog.Location = new System.Drawing.Point(12, 511);
      this.txtLog.Name = "txtLog";
      this.txtLog.Size = new System.Drawing.Size(673, 140);
      this.txtLog.TabIndex = 6;
      this.txtLog.Text = "";
      // 
      // txtEditedPath
      // 
      this.txtEditedPath.Location = new System.Drawing.Point(127, 83);
      this.txtEditedPath.Name = "txtEditedPath";
      this.txtEditedPath.ReadOnly = true;
      this.txtEditedPath.Size = new System.Drawing.Size(365, 20);
      this.txtEditedPath.TabIndex = 5;
      // 
      // label3
      // 
      this.label3.AutoSize = true;
      this.label3.Location = new System.Drawing.Point(17, 86);
      this.label3.Name = "label3";
      this.label3.Size = new System.Drawing.Size(83, 13);
      this.label3.TabIndex = 2;
      this.label3.Text = "Editierte Version";
      // 
      // Form1
      // 
      this.AutoScaleDimensions = new System.Drawing.SizeF(6F, 13F);
      this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
      this.ClientSize = new System.Drawing.Size(697, 676);
      this.Controls.Add(this.txtLog);
      this.Controls.Add(this.statusStrip1);
      this.Controls.Add(this.grpOperations);
      this.Controls.Add(this.grpRepository);
      this.Controls.Add(this.grpSystem);
      this.Name = "Form1";
      this.Text = "StateRepository - Manager";
      this.grpSystem.ResumeLayout(false);
      this.grpSystem.PerformLayout();
      this.grpRepository.ResumeLayout(false);
      this.grpRepository.PerformLayout();
      this.grpOperations.ResumeLayout(false);
      this.grpOperations.PerformLayout();
      this.ResumeLayout(false);
      this.PerformLayout();

    }

    #endregion

    private System.Windows.Forms.Button btnMsConfig;
    private System.Windows.Forms.Label lblBootMode;
    private System.Windows.Forms.Label lblServiceStatus;
    private System.Windows.Forms.GroupBox grpSystem;
    private System.Windows.Forms.Button btnRefresh;
    private System.Windows.Forms.GroupBox grpRepository;
    private System.Windows.Forms.ListView lvFiles;
    private System.Windows.Forms.Button btnBrowse;
    private System.Windows.Forms.Label label2;
    private System.Windows.Forms.Label label1;
    private System.Windows.Forms.TextBox txtWorkPath;
    private System.Windows.Forms.TextBox txtLivePath;
    private System.Windows.Forms.GroupBox grpOperations;
    private System.Windows.Forms.Button btnValidate;
    private System.Windows.Forms.Button btnBackup;
    private System.Windows.Forms.Button btnRestore;
    private System.Windows.Forms.Button btnReplace;
    private System.Windows.Forms.StatusStrip statusStrip1;
    private System.Windows.Forms.RichTextBox txtLog;
    private System.Windows.Forms.Button btnStopSrvc;
    private System.Windows.Forms.ListView lvPackages;
    private System.Windows.Forms.Button btnSearchPackage;
    private System.Windows.Forms.Label lblSearchPackage;
    private System.Windows.Forms.TextBox txtSearchPackage;
    private System.Windows.Forms.Button btnRemovePackage;
    private System.Windows.Forms.TextBox txtEditedPath;
    private System.Windows.Forms.Label label3;
  }
}

