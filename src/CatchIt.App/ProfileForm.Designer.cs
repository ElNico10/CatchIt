namespace CatchIt.App;

partial class ProfileForm
{
    private System.ComponentModel.IContainer components = null;

    protected override void Dispose(bool disposing)
    {
        if (disposing && (components != null))
        {
            components.Dispose();
        }
        base.Dispose(disposing);
    }

    #region Windows Form Designer generated code

    private void InitializeComponent()
    {
        lblTitle = new Label();
        lblName = new Label();
        txtName = new TextBox();
        lblEmail = new Label();
        txtEmail = new TextBox();
        lblStatus = new Label();
        btnSave = new Button();
        grpPassword = new GroupBox();
        lblCurrent = new Label();
        txtCurrent = new TextBox();
        lblNew = new Label();
        txtNew = new TextBox();
        lblConfirmPassword = new Label();
        txtConfirmPassword = new TextBox();
        btnChangePassword = new Button();
        btnClose = new Button();
        grpPassword.SuspendLayout();
        SuspendLayout();
        //
        // lblTitle
        //
        lblTitle.AutoSize = true;
        lblTitle.Font = new Font("Segoe UI", 16F, FontStyle.Bold);
        lblTitle.Location = new Point(30, 18);
        lblTitle.Name = "lblTitle";
        lblTitle.Size = new Size(116, 30);
        lblTitle.TabIndex = 0;
        lblTitle.Text = "My profile";
        //
        // lblName
        //
        lblName.AutoSize = true;
        lblName.Location = new Point(32, 62);
        lblName.Name = "lblName";
        lblName.Size = new Size(39, 15);
        lblName.TabIndex = 1;
        lblName.Text = "Name";
        //
        // txtName
        //
        txtName.Location = new Point(32, 80);
        txtName.MaxLength = 100;
        txtName.Name = "txtName";
        txtName.Size = new Size(356, 23);
        txtName.TabIndex = 0;
        //
        // lblEmail
        //
        lblEmail.AutoSize = true;
        lblEmail.Location = new Point(32, 112);
        lblEmail.Name = "lblEmail";
        lblEmail.Size = new Size(39, 15);
        lblEmail.TabIndex = 3;
        lblEmail.Text = "Email";
        //
        // txtEmail
        //
        txtEmail.Location = new Point(32, 130);
        txtEmail.Name = "txtEmail";
        txtEmail.Size = new Size(356, 23);
        txtEmail.TabIndex = 1;
        //
        // lblStatus
        //
        lblStatus.Location = new Point(32, 158);
        lblStatus.Name = "lblStatus";
        lblStatus.Size = new Size(356, 34);
        lblStatus.TabIndex = 5;
        //
        // btnSave
        //
        btnSave.Location = new Point(32, 196);
        btnSave.Name = "btnSave";
        btnSave.Size = new Size(356, 32);
        btnSave.TabIndex = 2;
        btnSave.Text = "Save";
        btnSave.UseVisualStyleBackColor = true;
        btnSave.Click += btnSave_Click;
        //
        // grpPassword
        //
        grpPassword.Controls.Add(lblCurrent);
        grpPassword.Controls.Add(txtCurrent);
        grpPassword.Controls.Add(lblNew);
        grpPassword.Controls.Add(txtNew);
        grpPassword.Controls.Add(lblConfirmPassword);
        grpPassword.Controls.Add(txtConfirmPassword);
        grpPassword.Controls.Add(btnChangePassword);
        grpPassword.Enabled = false;
        grpPassword.Location = new Point(32, 246);
        grpPassword.Name = "grpPassword";
        grpPassword.Size = new Size(356, 214);
        grpPassword.TabIndex = 3;
        grpPassword.TabStop = false;
        grpPassword.Text = "Change password (coming soon)";
        //
        // lblCurrent
        //
        lblCurrent.AutoSize = true;
        lblCurrent.Location = new Point(12, 26);
        lblCurrent.Name = "lblCurrent";
        lblCurrent.Size = new Size(104, 15);
        lblCurrent.TabIndex = 0;
        lblCurrent.Text = "Current password";
        //
        // txtCurrent
        //
        txtCurrent.Location = new Point(12, 44);
        txtCurrent.Name = "txtCurrent";
        txtCurrent.Size = new Size(332, 23);
        txtCurrent.TabIndex = 1;
        txtCurrent.UseSystemPasswordChar = true;
        //
        // lblNew
        //
        lblNew.AutoSize = true;
        lblNew.Location = new Point(12, 74);
        lblNew.Name = "lblNew";
        lblNew.Size = new Size(87, 15);
        lblNew.TabIndex = 2;
        lblNew.Text = "New password";
        //
        // txtNew
        //
        txtNew.Location = new Point(12, 92);
        txtNew.Name = "txtNew";
        txtNew.Size = new Size(332, 23);
        txtNew.TabIndex = 3;
        txtNew.UseSystemPasswordChar = true;
        //
        // lblConfirmPassword
        //
        lblConfirmPassword.AutoSize = true;
        lblConfirmPassword.Location = new Point(12, 122);
        lblConfirmPassword.Name = "lblConfirmPassword";
        lblConfirmPassword.Size = new Size(122, 15);
        lblConfirmPassword.TabIndex = 4;
        lblConfirmPassword.Text = "Confirm new password";
        //
        // txtConfirmPassword
        //
        txtConfirmPassword.Location = new Point(12, 140);
        txtConfirmPassword.Name = "txtConfirmPassword";
        txtConfirmPassword.Size = new Size(332, 23);
        txtConfirmPassword.TabIndex = 5;
        txtConfirmPassword.UseSystemPasswordChar = true;
        //
        // btnChangePassword
        //
        btnChangePassword.Location = new Point(12, 172);
        btnChangePassword.Name = "btnChangePassword";
        btnChangePassword.Size = new Size(332, 30);
        btnChangePassword.TabIndex = 6;
        btnChangePassword.Text = "Change password";
        btnChangePassword.UseVisualStyleBackColor = true;
        //
        // btnClose
        //
        btnClose.DialogResult = DialogResult.Cancel;
        btnClose.Location = new Point(32, 476);
        btnClose.Name = "btnClose";
        btnClose.Size = new Size(356, 32);
        btnClose.TabIndex = 4;
        btnClose.Text = "Close";
        btnClose.UseVisualStyleBackColor = true;
        //
        // ProfileForm
        //
        AcceptButton = btnSave;
        AutoScaleDimensions = new SizeF(7F, 15F);
        AutoScaleMode = AutoScaleMode.Font;
        CancelButton = btnClose;
        ClientSize = new Size(420, 528);
        Controls.Add(lblTitle);
        Controls.Add(lblName);
        Controls.Add(txtName);
        Controls.Add(lblEmail);
        Controls.Add(txtEmail);
        Controls.Add(lblStatus);
        Controls.Add(btnSave);
        Controls.Add(grpPassword);
        Controls.Add(btnClose);
        FormBorderStyle = FormBorderStyle.FixedDialog;
        MaximizeBox = false;
        MinimizeBox = false;
        Name = "ProfileForm";
        StartPosition = FormStartPosition.CenterParent;
        Text = "CatchIt - Profile";
        grpPassword.ResumeLayout(false);
        grpPassword.PerformLayout();
        ResumeLayout(false);
        PerformLayout();
    }

    #endregion

    private Label lblTitle;
    private Label lblName;
    private TextBox txtName;
    private Label lblEmail;
    private TextBox txtEmail;
    private Label lblStatus;
    private Button btnSave;
    private GroupBox grpPassword;
    private Label lblCurrent;
    private TextBox txtCurrent;
    private Label lblNew;
    private TextBox txtNew;
    private Label lblConfirmPassword;
    private TextBox txtConfirmPassword;
    private Button btnChangePassword;
    private Button btnClose;
}
