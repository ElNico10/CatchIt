namespace CatchIt.App;

partial class SearchForm
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
        lblDescription = new Label();
        lblDisclaimer = new Label();
        btnBack = new Button();
        SuspendLayout();
        //
        // lblTitle
        //
        lblTitle.AutoSize = true;
        lblTitle.Font = new Font("Segoe UI", 16F, FontStyle.Bold);
        lblTitle.Location = new Point(30, 20);
        lblTitle.Name = "lblTitle";
        lblTitle.Size = new Size(190, 30);
        lblTitle.TabIndex = 0;
        lblTitle.Text = "Search products";
        //
        // lblDescription
        //
        lblDescription.Location = new Point(32, 66);
        lblDescription.Name = "lblDescription";
        lblDescription.Size = new Size(456, 64);
        lblDescription.TabIndex = 1;
        lblDescription.Text = "Coming soon: search the catalog by product name or keyword. Each result will show its marketplace and current price, cheapest first, with filters for brand quality tier and seller rating.";
        //
        // lblDisclaimer
        //
        lblDisclaimer.ForeColor = SystemColors.GrayText;
        lblDisclaimer.Location = new Point(32, 144);
        lblDisclaimer.Name = "lblDisclaimer";
        lblDisclaimer.Size = new Size(456, 40);
        lblDisclaimer.TabIndex = 2;
        lblDisclaimer.Text = "Prices and seller ratings are simulated for demonstration. The data comes from the local seeded catalog, not from live marketplaces.";
        //
        // btnBack
        //
        btnBack.DialogResult = DialogResult.Cancel;
        btnBack.Location = new Point(32, 210);
        btnBack.Name = "btnBack";
        btnBack.Size = new Size(120, 32);
        btnBack.TabIndex = 0;
        btnBack.Text = "Back";
        btnBack.UseVisualStyleBackColor = true;
        //
        // SearchForm
        //
        AutoScaleDimensions = new SizeF(7F, 15F);
        AutoScaleMode = AutoScaleMode.Font;
        CancelButton = btnBack;
        ClientSize = new Size(520, 270);
        Controls.Add(lblTitle);
        Controls.Add(lblDescription);
        Controls.Add(lblDisclaimer);
        Controls.Add(btnBack);
        FormBorderStyle = FormBorderStyle.FixedDialog;
        MaximizeBox = false;
        MinimizeBox = false;
        Name = "SearchForm";
        StartPosition = FormStartPosition.CenterParent;
        Text = "CatchIt - Search";
        ResumeLayout(false);
        PerformLayout();
    }

    #endregion

    private Label lblTitle;
    private Label lblDescription;
    private Label lblDisclaimer;
    private Button btnBack;
}
