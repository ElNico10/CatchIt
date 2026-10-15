namespace CatchIt.App;

partial class RegisterForm
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
        lblPassword = new Label();
        txtPassword = new TextBox();
        lblHint = new Label();
        lblError = new Label();
        btnRegister = new Button();
        btnCancel = new Button();
        SuspendLayout();
        //
        // lblTitle
        //
        lblTitle.AutoSize = true;
        lblTitle.Font = new Font("Segoe UI", 16F, FontStyle.Bold);
        lblTitle.Location = new Point(30, 18);
        lblTitle.Name = "lblTitle";
        lblTitle.Size = new Size(190, 30);
        lblTitle.TabIndex = 0;
        lblTitle.Text = "Create an account";
        //
        // lblName
        //
        lblName.AutoSize = true;
        lblName.Location = new Point(32, 62);
        lblName.Name = "lblName";
        lblName.Size = new Size(94, 15);
        lblName.TabIndex = 1;
        lblName.Text = "Name (optional)";
        //
        // txtName
        //
        txtName.Location = new Point(32, 80);
        txtName.Name = "txtName";
        txtName.Size = new Size(316, 23);
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
        txtEmail.Size = new Size(316, 23);
        txtEmail.TabIndex = 1;
        //
        // lblPassword
        //
        lblPassword.AutoSize = true;
        lblPassword.Location = new Point(32, 162);
        lblPassword.Name = "lblPassword";
        lblPassword.Size = new Size(57, 15);
        lblPassword.TabIndex = 5;
        lblPassword.Text = "Password";
        //
        // txtPassword
        //
        txtPassword.Location = new Point(32, 180);
        txtPassword.Name = "txtPassword";
        txtPassword.Size = new Size(316, 23);
        txtPassword.TabIndex = 2;
        txtPassword.UseSystemPasswordChar = true;
        //
        // lblHint
        //
        lblHint.ForeColor = SystemColors.GrayText;
        lblHint.Location = new Point(32, 206);
        lblHint.Name = "lblHint";
        lblHint.Size = new Size(316, 32);
        lblHint.TabIndex = 7;
        lblHint.Text = "At least 8 characters, with at least one letter and one number.";
        //
        // lblError
        //
        lblError.ForeColor = Color.Firebrick;
        lblError.Location = new Point(32, 240);
        lblError.Name = "lblError";
        lblError.Size = new Size(316, 34);
        lblError.TabIndex = 8;
        //
        // btnRegister
        //
        btnRegister.Location = new Point(32, 280);
        btnRegister.Name = "btnRegister";
        btnRegister.Size = new Size(316, 32);
        btnRegister.TabIndex = 3;
        btnRegister.Text = "Register";
        btnRegister.UseVisualStyleBackColor = true;
        btnRegister.Click += btnRegister_Click;
        //
        // btnCancel
        //
        btnCancel.DialogResult = DialogResult.Cancel;
        btnCancel.Location = new Point(32, 318);
        btnCancel.Name = "btnCancel";
        btnCancel.Size = new Size(316, 32);
        btnCancel.TabIndex = 4;
        btnCancel.Text = "Back to login";
        btnCancel.UseVisualStyleBackColor = true;
        //
        // RegisterForm
        //
        AcceptButton = btnRegister;
        AutoScaleDimensions = new SizeF(7F, 15F);
        AutoScaleMode = AutoScaleMode.Font;
        CancelButton = btnCancel;
        ClientSize = new Size(380, 368);
        Controls.Add(lblTitle);
        Controls.Add(lblName);
        Controls.Add(txtName);
        Controls.Add(lblEmail);
        Controls.Add(txtEmail);
        Controls.Add(lblPassword);
        Controls.Add(txtPassword);
        Controls.Add(lblHint);
        Controls.Add(lblError);
        Controls.Add(btnRegister);
        Controls.Add(btnCancel);
        FormBorderStyle = FormBorderStyle.FixedDialog;
        MaximizeBox = false;
        MinimizeBox = false;
        Name = "RegisterForm";
        StartPosition = FormStartPosition.CenterParent;
        Text = "CatchIt - Register";
        ResumeLayout(false);
        PerformLayout();
    }

    #endregion

    private Label lblTitle;
    private Label lblName;
    private TextBox txtName;
    private Label lblEmail;
    private TextBox txtEmail;
    private Label lblPassword;
    private TextBox txtPassword;
    private Label lblHint;
    private Label lblError;
    private Button btnRegister;
    private Button btnCancel;
}
