namespace CatchIt.App;

partial class ProductDetailForm
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
        components = new System.ComponentModel.Container();
        tips = new ToolTip(components);
        lblTitle = new Label();
        lblSubtitle = new Label();
        lblPrice = new Label();
        lblOnSale = new Label();
        lblFacts = new Label();
        flowRange = new FlowLayoutPanel();
        rbThreeMonths = new RadioButton();
        rbSixMonths = new RadioButton();
        rbTwelveMonths = new RadioButton();
        rbAll = new RadioButton();
        chart = new PriceChart();
        lblAlternatives = new Label();
        lblNoAlternatives = new Label();
        flowAlternatives = new FlowLayoutPanel();
        lblDisclaimer = new Label();
        btnClose = new Button();
        flowRange.SuspendLayout();
        SuspendLayout();
        //
        // lblTitle
        //
        lblTitle.AutoEllipsis = true;
        lblTitle.Font = new Font("Segoe UI", 13F, FontStyle.Bold);
        lblTitle.Location = new Point(32, 14);
        lblTitle.Name = "lblTitle";
        lblTitle.Size = new Size(896, 52);
        lblTitle.TabIndex = 0;
        //
        // lblSubtitle
        //
        lblSubtitle.AutoEllipsis = true;
        lblSubtitle.ForeColor = SystemColors.GrayText;
        lblSubtitle.Location = new Point(32, 68);
        lblSubtitle.Name = "lblSubtitle";
        lblSubtitle.Size = new Size(896, 20);
        lblSubtitle.TabIndex = 1;
        //
        // lblPrice
        //
        lblPrice.AutoSize = true;
        lblPrice.Font = new Font("Segoe UI", 22F, FontStyle.Bold);
        lblPrice.Location = new Point(28, 102);
        lblPrice.Name = "lblPrice";
        lblPrice.Size = new Size(0, 41);
        lblPrice.TabIndex = 2;
        //
        // lblOnSale
        //
        lblOnSale.AutoSize = true;
        lblOnSale.BackColor = Color.ForestGreen;
        lblOnSale.Font = new Font("Segoe UI", 10F, FontStyle.Bold);
        lblOnSale.ForeColor = Color.White;
        lblOnSale.Location = new Point(32, 152);
        lblOnSale.Name = "lblOnSale";
        lblOnSale.Padding = new Padding(8, 3, 8, 3);
        lblOnSale.Size = new Size(74, 25);
        lblOnSale.TabIndex = 3;
        lblOnSale.Text = "On Sale";
        lblOnSale.Visible = false;
        //
        // lblFacts
        //
        lblFacts.Location = new Point(32, 190);
        lblFacts.Name = "lblFacts";
        lblFacts.Size = new Size(310, 150);
        lblFacts.TabIndex = 4;
        //
        // flowRange
        //
        flowRange.Controls.Add(rbThreeMonths);
        flowRange.Controls.Add(rbSixMonths);
        flowRange.Controls.Add(rbTwelveMonths);
        flowRange.Controls.Add(rbAll);
        flowRange.Location = new Point(362, 100);
        flowRange.Name = "flowRange";
        flowRange.Size = new Size(566, 32);
        flowRange.TabIndex = 5;
        //
        // rbThreeMonths
        //
        rbThreeMonths.Appearance = Appearance.Button;
        rbThreeMonths.AutoSize = true;
        rbThreeMonths.Name = "rbThreeMonths";
        rbThreeMonths.Padding = new Padding(8, 2, 8, 2);
        rbThreeMonths.TabIndex = 0;
        rbThreeMonths.Tag = "ThreeMonths";
        rbThreeMonths.Text = "3 months";
        rbThreeMonths.UseVisualStyleBackColor = true;
        rbThreeMonths.CheckedChanged += range_CheckedChanged;
        //
        // rbSixMonths
        //
        rbSixMonths.Appearance = Appearance.Button;
        rbSixMonths.AutoSize = true;
        rbSixMonths.Name = "rbSixMonths";
        rbSixMonths.Padding = new Padding(8, 2, 8, 2);
        rbSixMonths.TabIndex = 1;
        rbSixMonths.Tag = "SixMonths";
        rbSixMonths.Text = "6 months";
        rbSixMonths.UseVisualStyleBackColor = true;
        rbSixMonths.CheckedChanged += range_CheckedChanged;
        //
        // rbTwelveMonths
        //
        rbTwelveMonths.Appearance = Appearance.Button;
        rbTwelveMonths.AutoSize = true;
        rbTwelveMonths.Name = "rbTwelveMonths";
        rbTwelveMonths.Padding = new Padding(8, 2, 8, 2);
        rbTwelveMonths.TabIndex = 2;
        rbTwelveMonths.Tag = "TwelveMonths";
        rbTwelveMonths.Text = "12 months";
        rbTwelveMonths.UseVisualStyleBackColor = true;
        rbTwelveMonths.CheckedChanged += range_CheckedChanged;
        //
        // rbAll
        //
        rbAll.Appearance = Appearance.Button;
        rbAll.AutoSize = true;
        rbAll.Name = "rbAll";
        rbAll.Padding = new Padding(8, 2, 8, 2);
        rbAll.TabIndex = 3;
        rbAll.Tag = "All";
        rbAll.Text = "All";
        rbAll.UseVisualStyleBackColor = true;
        rbAll.CheckedChanged += range_CheckedChanged;
        //
        // chart
        //
        chart.Location = new Point(362, 138);
        chart.Name = "chart";
        chart.Size = new Size(566, 240);
        chart.TabIndex = 6;
        //
        // lblAlternatives
        //
        lblAlternatives.AutoSize = true;
        lblAlternatives.Font = new Font("Segoe UI", 12F, FontStyle.Bold);
        lblAlternatives.Location = new Point(32, 396);
        lblAlternatives.Name = "lblAlternatives";
        lblAlternatives.Size = new Size(94, 21);
        lblAlternatives.TabIndex = 7;
        lblAlternatives.Text = "Alternatives";
        //
        // lblNoAlternatives
        //
        lblNoAlternatives.ForeColor = SystemColors.GrayText;
        lblNoAlternatives.Location = new Point(32, 428);
        lblNoAlternatives.Name = "lblNoAlternatives";
        lblNoAlternatives.Size = new Size(896, 40);
        lblNoAlternatives.TabIndex = 8;
        lblNoAlternatives.Text = "No cheaper, better-reviewed or likely-to-drop alternatives are stored for this product.";
        lblNoAlternatives.Visible = false;
        //
        // flowAlternatives
        //
        flowAlternatives.Location = new Point(32, 424);
        flowAlternatives.Name = "flowAlternatives";
        flowAlternatives.Size = new Size(896, 184);
        flowAlternatives.TabIndex = 9;
        flowAlternatives.WrapContents = false;
        //
        // lblDisclaimer
        //
        lblDisclaimer.ForeColor = SystemColors.GrayText;
        lblDisclaimer.Location = new Point(32, 624);
        lblDisclaimer.Name = "lblDisclaimer";
        lblDisclaimer.Size = new Size(896, 36);
        lblDisclaimer.TabIndex = 10;
        //
        // btnClose
        //
        btnClose.DialogResult = DialogResult.Cancel;
        btnClose.Location = new Point(32, 666);
        btnClose.Name = "btnClose";
        btnClose.Size = new Size(120, 32);
        btnClose.TabIndex = 11;
        btnClose.Text = "Close";
        btnClose.UseVisualStyleBackColor = true;
        //
        // ProductDetailForm
        //
        AutoScaleDimensions = new SizeF(7F, 15F);
        AutoScaleMode = AutoScaleMode.Font;
        CancelButton = btnClose;
        ClientSize = new Size(960, 718);
        Controls.Add(lblTitle);
        Controls.Add(lblSubtitle);
        Controls.Add(lblPrice);
        Controls.Add(lblOnSale);
        Controls.Add(lblFacts);
        Controls.Add(flowRange);
        Controls.Add(chart);
        Controls.Add(lblAlternatives);
        Controls.Add(lblNoAlternatives);
        Controls.Add(flowAlternatives);
        Controls.Add(lblDisclaimer);
        Controls.Add(btnClose);
        FormBorderStyle = FormBorderStyle.FixedDialog;
        MaximizeBox = false;
        MinimizeBox = false;
        Name = "ProductDetailForm";
        StartPosition = FormStartPosition.CenterParent;
        Text = "CatchIt - Product details";
        flowRange.ResumeLayout(false);
        flowRange.PerformLayout();
        ResumeLayout(false);
        PerformLayout();
    }

    #endregion

    private ToolTip tips;
    private Label lblTitle;
    private Label lblSubtitle;
    private Label lblPrice;
    private Label lblOnSale;
    private Label lblFacts;
    private FlowLayoutPanel flowRange;
    private RadioButton rbThreeMonths;
    private RadioButton rbSixMonths;
    private RadioButton rbTwelveMonths;
    private RadioButton rbAll;
    private PriceChart chart;
    private Label lblAlternatives;
    private Label lblNoAlternatives;
    private FlowLayoutPanel flowAlternatives;
    private Label lblDisclaimer;
    private Button btnClose;
}
