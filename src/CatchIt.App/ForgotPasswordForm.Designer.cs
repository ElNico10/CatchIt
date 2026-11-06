namespace CatchIt.App;

partial class ForgotPasswordForm
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
        lblEmail = new Label();
        txtEmail = new TextBox();
        btnRequest = new Button();
        lblCode = new Label();
        txtCode = new TextBox();
        lblNewPassword = new Label();
        txtNewPassword = new TextBox();
        lblConfirm = new Label();
        txtConfirm = new TextBox();
        lblError = new Label();
        btnSetPassword = new Button();
        btnCancel = new Button();
        SuspendLayout();
        //
        // lblTitle
        //
        lblTitle.AutoSize = true;
        lblTitle.Font = new Font("Segoe UI", 14F, FontStyle.Bold);
        lblTitle.Location = new Point(30, 18);
        lblTitle.Name = "lblTitle";
        lblTitle.Size = new Size(170, 25);
        lblTitle.TabIndex = 0;
        lblTitle.Text = "Reset your password";
        //
        // lblEmail
        //
        lblEmail.AutoSize = true;
        lblEmail.Location = new Point(32, 58);
        lblEmail.Name = "lblEmail";
        lblEmail.Size = new Size(39, 15);
        lblEmail.TabIndex = 1;
        lblEmail.Text = "Email";
        //
        // txtEmail
        //
        txtEmail.Location = new Point(32, 76);
        txtEmail.Name = "txtEmail";
        txtEmail.Size = new Size(316, 23);
        txtEmail.TabIndex = 0;
        //
        // btnRequest
        //
        btnRequest.Location = new Point(32, 108);
        btnRequest.Name = "btnRequest";
        btnRequest.Size = new Size(316, 32);
        btnRequest.TabIndex = 1;
        btnRequest.Text = "Reset Password";
        btnRequest.UseVisualStyleBackColor = true;
        btnRequest.Click += btnRequest_Click;
        //
        // lblCode
        //
        lblCode.AutoSize = true;
        lblCode.Location = new Point(32, 152);
        lblCode.Name = "lblCode";
        lblCode.Size = new Size(118, 15);
        lblCode.TabIndex = 3;
        lblCode.Text = "Code from the email";
        //
        // txtCode
        //
        txtCode.Location = new Point(32, 170);
        txtCode.MaxLength = 6;
        txtCode.Name = "txtCode";
        txtCode.Size = new Size(316, 23);
        txtCode.TabIndex = 2;
        //
        // lblNewPassword
        //
        lblNewPassword.AutoSize = true;
        lblNewPassword.Location = new Point(32, 202);
        lblNewPassword.Name = "lblNewPassword";
        lblNewPassword.Size = new Size(84, 15);
        lblNewPassword.TabIndex = 5;
        lblNewPassword.Text = "New password";
        //
        // txtNewPassword
        //
        txtNewPassword.Location = new Point(32, 220);
        txtNewPassword.Name = "txtNewPassword";
        txtNewPassword.Size = new Size(316, 23);
        txtNewPassword.TabIndex = 3;
        txtNewPassword.UseSystemPasswordChar = true;
        //
        // lblConfirm
        //
        lblConfirm.AutoSize = true;
        lblConfirm.Location = new Point(32, 252);
        lblConfirm.Name = "lblConfirm";
        lblConfirm.Size = new Size(133, 15);
        lblConfirm.TabIndex = 7;
        lblConfirm.Text = "Confirm new password";
        //
        // txtConfirm
        //
        txtConfirm.Location = new Point(32, 270);
        txtConfirm.Name = "txtConfirm";
        txtConfirm.Size = new Size(316, 23);
        txtConfirm.TabIndex = 4;
        txtConfirm.UseSystemPasswordChar = true;
        //
        // lblError
        //
        lblError.ForeColor = Color.Firebrick;
        lblError.Location = new Point(32, 300);
        lblError.Name = "lblError";
        lblError.Size = new Size(316, 36);
        lblError.TabIndex = 9;
        //
        // btnSetPassword
        //
        btnSetPassword.Location = new Point(32, 340);
        btnSetPassword.Name = "btnSetPassword";
        btnSetPassword.Size = new Size(316, 32);
        btnSetPassword.TabIndex = 5;
        btnSetPassword.Text = "Set New Password";
        btnSetPassword.UseVisualStyleBackColor = true;
        btnSetPassword.Click += btnSetPassword_Click;
        //
        // btnCancel
        //
        btnCancel.DialogResult = DialogResult.Cancel;
        btnCancel.Location = new Point(32, 380);
        btnCancel.Name = "btnCancel";
        btnCancel.Size = new Size(316, 32);
        btnCancel.TabIndex = 6;
        btnCancel.Text = "Back to login";
        btnCancel.UseVisualStyleBackColor = true;
        //
        // ForgotPasswordForm
        //
        AcceptButton = btnRequest;
        AutoScaleDimensions = new SizeF(7F, 15F);
        AutoScaleMode = AutoScaleMode.Font;
        CancelButton = btnCancel;
        ClientSize = new Size(380, 432);
        Controls.Add(lblTitle);
        Controls.Add(lblEmail);
        Controls.Add(txtEmail);
        Controls.Add(btnRequest);
        Controls.Add(lblCode);
        Controls.Add(txtCode);
        Controls.Add(lblNewPassword);
        Controls.Add(txtNewPassword);
        Controls.Add(lblConfirm);
        Controls.Add(txtConfirm);
        Controls.Add(lblError);
        Controls.Add(btnSetPassword);
        Controls.Add(btnCancel);
        FormBorderStyle = FormBorderStyle.FixedDialog;
        MaximizeBox = false;
        MinimizeBox = false;
        Name = "ForgotPasswordForm";
        StartPosition = FormStartPosition.CenterParent;
        Text = "CatchIt - Forgot password";
        ResumeLayout(false);
        PerformLayout();
    }

    #endregion

    private Label lblTitle;
    private Label lblEmail;
    private TextBox txtEmail;
    private Button btnRequest;
    private Label lblCode;
    private TextBox txtCode;
    private Label lblNewPassword;
    private TextBox txtNewPassword;
    private Label lblConfirm;
    private TextBox txtConfirm;
    private Label lblError;
    private Button btnSetPassword;
    private Button btnCancel;
}
