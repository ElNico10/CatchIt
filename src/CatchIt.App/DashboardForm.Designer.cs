namespace CatchIt.App;

partial class DashboardForm
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
        lblSection = new Label();
        lblDescription = new Label();
        btnBack = new Button();
        SuspendLayout();
        //
        // lblTitle
        //
        lblTitle.AutoSize = true;
        lblTitle.Font = new Font("Segoe UI", 16F, FontStyle.Bold);
        lblTitle.Location = new Point(30, 20);
        lblTitle.Name = "lblTitle";
        lblTitle.Size = new Size(114, 30);
        lblTitle.TabIndex = 0;
        lblTitle.Text = "Dashboard";
        //
        // lblSection
        //
        lblSection.AutoSize = true;
        lblSection.Font = new Font("Segoe UI", 12F, FontStyle.Bold);
        lblSection.Location = new Point(32, 70);
        lblSection.Name = "lblSection";
        lblSection.Size = new Size(147, 21);
        lblSection.TabIndex = 1;
        lblSection.Text = "Below Target Price";
        //
        // lblDescription
        //
        lblDescription.ForeColor = SystemColors.GrayText;
        lblDescription.Location = new Point(32, 98);
        lblDescription.Name = "lblDescription";
        lblDescription.Size = new Size(456, 48);
        lblDescription.TabIndex = 2;
        lblDescription.Text = "Coming soon: watchlisted products whose current price has dropped below your target price will be listed here whenever you open the dashboard.";
        //
        // btnBack
        //
        btnBack.DialogResult = DialogResult.Cancel;
        btnBack.Location = new Point(32, 166);
        btnBack.Name = "btnBack";
        btnBack.Size = new Size(120, 32);
        btnBack.TabIndex = 0;
        btnBack.Text = "Back";
        btnBack.UseVisualStyleBackColor = true;
        //
        // DashboardForm
        //
        AutoScaleDimensions = new SizeF(7F, 15F);
        AutoScaleMode = AutoScaleMode.Font;
        CancelButton = btnBack;
        ClientSize = new Size(520, 226);
        Controls.Add(lblTitle);
        Controls.Add(lblSection);
        Controls.Add(lblDescription);
        Controls.Add(btnBack);
        FormBorderStyle = FormBorderStyle.FixedDialog;
        MaximizeBox = false;
        MinimizeBox = false;
        Name = "DashboardForm";
        StartPosition = FormStartPosition.CenterParent;
        Text = "CatchIt - Dashboard";
        ResumeLayout(false);
        PerformLayout();
    }

    #endregion

    private Label lblTitle;
    private Label lblSection;
    private Label lblDescription;
    private Button btnBack;
}
