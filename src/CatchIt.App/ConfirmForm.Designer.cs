namespace CatchIt.App;

partial class ConfirmForm
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
        lblInfo = new Label();
        txtCode = new TextBox();
        lblError = new Label();
        btnConfirm = new Button();
        lnkResend = new LinkLabel();
        btnCancel = new Button();
        SuspendLayout();
        //
        // lblTitle
        //
        lblTitle.AutoSize = true;
        lblTitle.Font = new Font("Segoe UI", 16F, FontStyle.Bold);
        lblTitle.Location = new Point(30, 18);
        lblTitle.Name = "lblTitle";
        lblTitle.Size = new Size(240, 30);
        lblTitle.TabIndex = 0;
        lblTitle.Text = "Confirm your account";
        //
        // lblInfo
        //
        lblInfo.Location = new Point(32, 60);
        lblInfo.Name = "lblInfo";
        lblInfo.Size = new Size(316, 36);
        lblInfo.TabIndex = 1;
        //
        // txtCode
        //
        txtCode.Font = new Font("Segoe UI", 12F);
        txtCode.Location = new Point(32, 102);
        txtCode.MaxLength = 6;
        txtCode.Name = "txtCode";
        txtCode.Size = new Size(316, 29);
        txtCode.TabIndex = 0;
        txtCode.TextAlign = HorizontalAlignment.Center;
        //
        // lblError
        //
        lblError.ForeColor = Color.Firebrick;
        lblError.Location = new Point(32, 136);
        lblError.Name = "lblError";
        lblError.Size = new Size(316, 34);
        lblError.TabIndex = 3;
        //
        // btnConfirm
        //
        btnConfirm.Location = new Point(32, 176);
        btnConfirm.Name = "btnConfirm";
        btnConfirm.Size = new Size(316, 32);
        btnConfirm.TabIndex = 1;
        btnConfirm.Text = "Confirm";
        btnConfirm.UseVisualStyleBackColor = true;
        btnConfirm.Click += btnConfirm_Click;
        //
        // lnkResend
        //
        lnkResend.Location = new Point(32, 216);
        lnkResend.Name = "lnkResend";
        lnkResend.Size = new Size(316, 20);
        lnkResend.TabIndex = 2;
        lnkResend.TabStop = true;
        lnkResend.Text = "Send me a new code";
        lnkResend.TextAlign = ContentAlignment.MiddleCenter;
        lnkResend.LinkClicked += lnkResend_LinkClicked;
        //
        // btnCancel
        //
        btnCancel.DialogResult = DialogResult.Cancel;
        btnCancel.Location = new Point(32, 244);
        btnCancel.Name = "btnCancel";
        btnCancel.Size = new Size(316, 32);
        btnCancel.TabIndex = 3;
        btnCancel.Text = "Not now";
        btnCancel.UseVisualStyleBackColor = true;
        //
        // ConfirmForm
        //
        AcceptButton = btnConfirm;
        AutoScaleDimensions = new SizeF(7F, 15F);
        AutoScaleMode = AutoScaleMode.Font;
        CancelButton = btnCancel;
        ClientSize = new Size(380, 294);
        Controls.Add(lblTitle);
        Controls.Add(lblInfo);
        Controls.Add(txtCode);
        Controls.Add(lblError);
        Controls.Add(btnConfirm);
        Controls.Add(lnkResend);
        Controls.Add(btnCancel);
        FormBorderStyle = FormBorderStyle.FixedDialog;
        MaximizeBox = false;
        MinimizeBox = false;
        Name = "ConfirmForm";
        StartPosition = FormStartPosition.CenterParent;
        Text = "CatchIt - Confirm account";
        ResumeLayout(false);
        PerformLayout();
    }

    #endregion

    private Label lblTitle;
    private Label lblInfo;
    private TextBox txtCode;
    private Label lblError;
    private Button btnConfirm;
    private LinkLabel lnkResend;
    private Button btnCancel;
}
