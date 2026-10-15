namespace CatchIt.App;

partial class WatchlistForm
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
        btnBack = new Button();
        SuspendLayout();
        //
        // lblTitle
        //
        lblTitle.AutoSize = true;
        lblTitle.Font = new Font("Segoe UI", 16F, FontStyle.Bold);
        lblTitle.Location = new Point(30, 20);
        lblTitle.Name = "lblTitle";
        lblTitle.Size = new Size(138, 30);
        lblTitle.TabIndex = 0;
        lblTitle.Text = "My watchlist";
        //
        // lblDescription
        //
        lblDescription.Location = new Point(32, 66);
        lblDescription.Name = "lblDescription";
        lblDescription.Size = new Size(456, 64);
        lblDescription.TabIndex = 1;
        lblDescription.Text = "Coming soon: the products you save will be listed here. You will be able to add or remove products and set a target price for each one.";
        //
        // btnBack
        //
        btnBack.DialogResult = DialogResult.Cancel;
        btnBack.Location = new Point(32, 150);
        btnBack.Name = "btnBack";
        btnBack.Size = new Size(120, 32);
        btnBack.TabIndex = 0;
        btnBack.Text = "Back";
        btnBack.UseVisualStyleBackColor = true;
        //
        // WatchlistForm
        //
        AutoScaleDimensions = new SizeF(7F, 15F);
        AutoScaleMode = AutoScaleMode.Font;
        CancelButton = btnBack;
        ClientSize = new Size(520, 210);
        Controls.Add(lblTitle);
        Controls.Add(lblDescription);
        Controls.Add(btnBack);
        FormBorderStyle = FormBorderStyle.FixedDialog;
        MaximizeBox = false;
        MinimizeBox = false;
        Name = "WatchlistForm";
        StartPosition = FormStartPosition.CenterParent;
        Text = "CatchIt - Watchlist";
        ResumeLayout(false);
        PerformLayout();
    }

    #endregion

    private Label lblTitle;
    private Label lblDescription;
    private Button btnBack;
}
