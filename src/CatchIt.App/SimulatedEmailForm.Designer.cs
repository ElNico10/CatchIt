namespace CatchIt.App;

partial class SimulatedEmailForm
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
        lblMeta = new Label();
        lblIntro = new Label();
        txtCode = new TextBox();
        btnCopy = new Button();
        lblNote = new Label();
        btnOk = new Button();
        SuspendLayout();
        //
        // lblMeta
        //
        lblMeta.Location = new Point(32, 18);
        lblMeta.Name = "lblMeta";
        lblMeta.Size = new Size(316, 40);
        lblMeta.TabIndex = 0;
        //
        // lblIntro
        //
        lblIntro.Location = new Point(32, 68);
        lblIntro.Name = "lblIntro";
        lblIntro.Size = new Size(316, 20);
        lblIntro.TabIndex = 1;
        lblIntro.Text = "Your confirmation code is:";
        //
        // txtCode
        //
        txtCode.BackColor = SystemColors.Window;
        txtCode.Font = new Font("Segoe UI", 20F, FontStyle.Bold);
        txtCode.Location = new Point(32, 92);
        txtCode.Name = "txtCode";
        txtCode.ReadOnly = true;
        txtCode.Size = new Size(316, 43);
        txtCode.TabIndex = 0;
        txtCode.TextAlign = HorizontalAlignment.Center;
        //
        // btnCopy
        //
        btnCopy.Location = new Point(32, 146);
        btnCopy.Name = "btnCopy";
        btnCopy.Size = new Size(316, 32);
        btnCopy.TabIndex = 1;
        btnCopy.Text = "Copy code";
        btnCopy.UseVisualStyleBackColor = true;
        btnCopy.Click += btnCopy_Click;
        //
        // lblNote
        //
        lblNote.ForeColor = SystemColors.GrayText;
        lblNote.Location = new Point(32, 190);
        lblNote.Name = "lblNote";
        lblNote.Size = new Size(316, 36);
        lblNote.TabIndex = 4;
        lblNote.Text = "Demo mode: this is a simulated email. Nothing was actually sent.";
        //
        // btnOk
        //
        btnOk.DialogResult = DialogResult.OK;
        btnOk.Location = new Point(32, 232);
        btnOk.Name = "btnOk";
        btnOk.Size = new Size(316, 32);
        btnOk.TabIndex = 2;
        btnOk.Text = "OK";
        btnOk.UseVisualStyleBackColor = true;
        //
        // SimulatedEmailForm
        //
        AcceptButton = btnOk;
        AutoScaleDimensions = new SizeF(7F, 15F);
        AutoScaleMode = AutoScaleMode.Font;
        CancelButton = btnOk;
        ClientSize = new Size(380, 282);
        Controls.Add(lblMeta);
        Controls.Add(lblIntro);
        Controls.Add(txtCode);
        Controls.Add(btnCopy);
        Controls.Add(lblNote);
        Controls.Add(btnOk);
        FormBorderStyle = FormBorderStyle.FixedDialog;
        MaximizeBox = false;
        MinimizeBox = false;
        Name = "SimulatedEmailForm";
        StartPosition = FormStartPosition.CenterParent;
        Text = "Simulated email";
        ResumeLayout(false);
        PerformLayout();
    }

    #endregion

    private Label lblMeta;
    private Label lblIntro;
    private TextBox txtCode;
    private Button btnCopy;
    private Label lblNote;
    private Button btnOk;
}
