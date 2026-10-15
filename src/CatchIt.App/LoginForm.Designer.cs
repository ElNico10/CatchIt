namespace CatchIt.App;

partial class LoginForm
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
        lblSubtitle = new Label();
        lblEmail = new Label();
        txtEmail = new TextBox();
        lblPassword = new Label();
        txtPassword = new TextBox();
        lblError = new Label();
        btnLogin = new Button();
        lnkRegister = new LinkLabel();
        btnGuest = new Button();
        SuspendLayout();
        //
        // lblTitle
        //
        lblTitle.AutoSize = true;
        lblTitle.Font = new Font("Segoe UI", 20F, FontStyle.Bold);
        lblTitle.Location = new Point(30, 20);
        lblTitle.Name = "lblTitle";
        lblTitle.Size = new Size(104, 37);
        lblTitle.TabIndex = 0;
        lblTitle.Text = "CatchIt";
        //
        // lblSubtitle
        //
        lblSubtitle.Location = new Point(32, 60);
        lblSubtitle.Name = "lblSubtitle";
        lblSubtitle.Size = new Size(316, 20);
        lblSubtitle.TabIndex = 1;
        lblSubtitle.Text = "Deal finder and price history";
        //
        // lblEmail
        //
        lblEmail.AutoSize = true;
        lblEmail.Location = new Point(32, 95);
        lblEmail.Name = "lblEmail";
        lblEmail.Size = new Size(39, 15);
        lblEmail.TabIndex = 2;
        lblEmail.Text = "Email";
        //
        // txtEmail
        //
        txtEmail.Location = new Point(32, 113);
        txtEmail.Name = "txtEmail";
        txtEmail.Size = new Size(316, 23);
        txtEmail.TabIndex = 0;
        //
        // lblPassword
        //
        lblPassword.AutoSize = true;
        lblPassword.Location = new Point(32, 145);
        lblPassword.Name = "lblPassword";
        lblPassword.Size = new Size(57, 15);
        lblPassword.TabIndex = 4;
        lblPassword.Text = "Password";
        //
        // txtPassword
        //
        txtPassword.Location = new Point(32, 163);
        txtPassword.Name = "txtPassword";
        txtPassword.Size = new Size(316, 23);
        txtPassword.TabIndex = 1;
        txtPassword.UseSystemPasswordChar = true;
        //
        // lblError
        //
        lblError.ForeColor = Color.Firebrick;
        lblError.Location = new Point(32, 192);
        lblError.Name = "lblError";
        lblError.Size = new Size(316, 20);
        lblError.TabIndex = 6;
        //
        // btnLogin
        //
        btnLogin.Location = new Point(32, 218);
        btnLogin.Name = "btnLogin";
        btnLogin.Size = new Size(316, 32);
        btnLogin.TabIndex = 2;
        btnLogin.Text = "Login";
        btnLogin.UseVisualStyleBackColor = true;
        btnLogin.Click += btnLogin_Click;
        //
        // lnkRegister
        //
        lnkRegister.Location = new Point(32, 258);
        lnkRegister.Name = "lnkRegister";
        lnkRegister.Size = new Size(316, 20);
        lnkRegister.TabIndex = 3;
        lnkRegister.TabStop = true;
        lnkRegister.Text = "New here? Create an account";
        lnkRegister.TextAlign = ContentAlignment.MiddleCenter;
        lnkRegister.LinkClicked += lnkRegister_LinkClicked;
        //
        // btnGuest
        //
        btnGuest.Location = new Point(32, 288);
        btnGuest.Name = "btnGuest";
        btnGuest.Size = new Size(316, 32);
        btnGuest.TabIndex = 4;
        btnGuest.Text = "Continue as guest";
        btnGuest.UseVisualStyleBackColor = true;
        btnGuest.Click += btnGuest_Click;
        //
        // LoginForm
        //
        AcceptButton = btnLogin;
        AutoScaleDimensions = new SizeF(7F, 15F);
        AutoScaleMode = AutoScaleMode.Font;
        ClientSize = new Size(380, 345);
        Controls.Add(lblTitle);
        Controls.Add(lblSubtitle);
        Controls.Add(lblEmail);
        Controls.Add(txtEmail);
        Controls.Add(lblPassword);
        Controls.Add(txtPassword);
        Controls.Add(lblError);
        Controls.Add(btnLogin);
        Controls.Add(lnkRegister);
        Controls.Add(btnGuest);
        FormBorderStyle = FormBorderStyle.FixedDialog;
        MaximizeBox = false;
        MinimizeBox = false;
        Name = "LoginForm";
        StartPosition = FormStartPosition.CenterScreen;
        Text = "CatchIt - Login";
        ResumeLayout(false);
        PerformLayout();
    }

    #endregion

    private Label lblTitle;
    private Label lblSubtitle;
    private Label lblEmail;
    private TextBox txtEmail;
    private Label lblPassword;
    private TextBox txtPassword;
    private Label lblError;
    private Button btnLogin;
    private LinkLabel lnkRegister;
    private Button btnGuest;
}
