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
        DataGridViewCellStyle priceStyle = new DataGridViewCellStyle();
        lblTitle = new Label();
        txtSearch = new TextBox();
        btnSearch = new Button();
        lblTier = new Label();
        cmbTier = new ComboBox();
        lblRating = new Label();
        cmbRating = new ComboBox();
        btnClearFilters = new Button();
        lblStatus = new Label();
        dgvResults = new DataGridView();
        colProduct = new DataGridViewTextBoxColumn();
        colBrand = new DataGridViewTextBoxColumn();
        colQuality = new DataGridViewTextBoxColumn();
        colMarketplace = new DataGridViewTextBoxColumn();
        colSellerRating = new DataGridViewTextBoxColumn();
        colPrice = new DataGridViewTextBoxColumn();
        lblDisclaimer = new Label();
        btnBack = new Button();
        ((System.ComponentModel.ISupportInitialize)dgvResults).BeginInit();
        SuspendLayout();
        //
        // lblTitle
        //
        lblTitle.AutoSize = true;
        lblTitle.Font = new Font("Segoe UI", 16F, FontStyle.Bold);
        lblTitle.Location = new Point(30, 14);
        lblTitle.Name = "lblTitle";
        lblTitle.Size = new Size(190, 30);
        lblTitle.TabIndex = 0;
        lblTitle.Text = "Search products";
        //
        // txtSearch
        //
        txtSearch.Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right;
        txtSearch.Font = new Font("Segoe UI", 11F);
        txtSearch.Location = new Point(32, 58);
        txtSearch.Name = "txtSearch";
        txtSearch.PlaceholderText = "Search by product name or keyword, e.g. wireless headphones";
        txtSearch.Size = new Size(722, 27);
        txtSearch.TabIndex = 0;
        txtSearch.TextChanged += txtSearch_TextChanged;
        //
        // btnSearch
        //
        btnSearch.Anchor = AnchorStyles.Top | AnchorStyles.Right;
        btnSearch.Location = new Point(768, 57);
        btnSearch.Name = "btnSearch";
        btnSearch.Size = new Size(100, 29);
        btnSearch.TabIndex = 1;
        btnSearch.Text = "Search";
        btnSearch.UseVisualStyleBackColor = true;
        btnSearch.Click += btnSearch_Click;
        //
        // lblTier
        //
        lblTier.AutoSize = true;
        lblTier.Location = new Point(32, 106);
        lblTier.Name = "lblTier";
        lblTier.Size = new Size(79, 15);
        lblTier.TabIndex = 2;
        lblTier.Text = "Brand quality";
        //
        // cmbTier
        //
        cmbTier.DropDownStyle = ComboBoxStyle.DropDownList;
        cmbTier.FormattingEnabled = true;
        cmbTier.Items.AddRange(new object[] { "Any", "Budget", "Standard", "Premium" });
        cmbTier.Location = new Point(117, 102);
        cmbTier.Name = "cmbTier";
        cmbTier.Size = new Size(120, 23);
        cmbTier.TabIndex = 2;
        cmbTier.SelectedIndexChanged += filter_Changed;
        //
        // lblRating
        //
        lblRating.AutoSize = true;
        lblRating.Location = new Point(262, 106);
        lblRating.Name = "lblRating";
        lblRating.Size = new Size(110, 15);
        lblRating.TabIndex = 4;
        lblRating.Text = "Seller rating (sim.)";
        //
        // cmbRating
        //
        cmbRating.DropDownStyle = ComboBoxStyle.DropDownList;
        cmbRating.FormattingEnabled = true;
        cmbRating.Items.AddRange(new object[] { "Any", "3.0 or higher", "3.5 or higher", "4.0 or higher", "4.5 or higher" });
        cmbRating.Location = new Point(378, 102);
        cmbRating.Name = "cmbRating";
        cmbRating.Size = new Size(140, 23);
        cmbRating.TabIndex = 3;
        cmbRating.SelectedIndexChanged += filter_Changed;
        //
        // btnClearFilters
        //
        btnClearFilters.Location = new Point(540, 100);
        btnClearFilters.Name = "btnClearFilters";
        btnClearFilters.Size = new Size(110, 27);
        btnClearFilters.TabIndex = 4;
        btnClearFilters.Text = "Clear filters";
        btnClearFilters.UseVisualStyleBackColor = true;
        btnClearFilters.Click += btnClearFilters_Click;
        //
        // lblStatus
        //
        lblStatus.Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right;
        lblStatus.Font = new Font("Segoe UI", 10F);
        lblStatus.Location = new Point(32, 140);
        lblStatus.Name = "lblStatus";
        lblStatus.Size = new Size(836, 22);
        lblStatus.TabIndex = 8;
        //
        // dgvResults
        //
        dgvResults.AllowUserToAddRows = false;
        dgvResults.AllowUserToDeleteRows = false;
        dgvResults.AllowUserToResizeRows = false;
        dgvResults.Anchor = AnchorStyles.Top | AnchorStyles.Bottom | AnchorStyles.Left | AnchorStyles.Right;
        dgvResults.AutoGenerateColumns = false;
        dgvResults.AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill;
        dgvResults.BackgroundColor = SystemColors.Window;
        dgvResults.ColumnHeadersHeightSizeMode = DataGridViewColumnHeadersHeightSizeMode.AutoSize;
        dgvResults.Columns.AddRange(new DataGridViewColumn[] { colProduct, colBrand, colQuality, colMarketplace, colSellerRating, colPrice });
        dgvResults.Location = new Point(32, 168);
        dgvResults.MultiSelect = false;
        dgvResults.Name = "dgvResults";
        dgvResults.ReadOnly = true;
        dgvResults.RowHeadersVisible = false;
        dgvResults.SelectionMode = DataGridViewSelectionMode.FullRowSelect;
        dgvResults.ShowCellToolTips = true;
        dgvResults.Size = new Size(836, 320);
        dgvResults.TabIndex = 5;
        //
        // colProduct
        //
        colProduct.DataPropertyName = "Product";
        colProduct.FillWeight = 36F;
        colProduct.HeaderText = "Product";
        colProduct.Name = "colProduct";
        colProduct.ReadOnly = true;
        colProduct.SortMode = DataGridViewColumnSortMode.NotSortable;
        //
        // colBrand
        //
        colBrand.DataPropertyName = "Brand";
        colBrand.FillWeight = 13F;
        colBrand.HeaderText = "Brand";
        colBrand.Name = "colBrand";
        colBrand.ReadOnly = true;
        colBrand.SortMode = DataGridViewColumnSortMode.NotSortable;
        //
        // colQuality
        //
        colQuality.DataPropertyName = "Quality";
        colQuality.FillWeight = 10F;
        colQuality.HeaderText = "Quality";
        colQuality.Name = "colQuality";
        colQuality.ReadOnly = true;
        colQuality.SortMode = DataGridViewColumnSortMode.NotSortable;
        //
        // colMarketplace
        //
        colMarketplace.DataPropertyName = "Marketplace";
        colMarketplace.FillWeight = 14F;
        colMarketplace.HeaderText = "Marketplace";
        colMarketplace.Name = "colMarketplace";
        colMarketplace.ReadOnly = true;
        colMarketplace.SortMode = DataGridViewColumnSortMode.NotSortable;
        //
        // colSellerRating
        //
        colSellerRating.DataPropertyName = "SellerRating";
        colSellerRating.FillWeight = 14F;
        colSellerRating.HeaderText = "Seller rating (simulated)";
        colSellerRating.Name = "colSellerRating";
        colSellerRating.ReadOnly = true;
        colSellerRating.SortMode = DataGridViewColumnSortMode.NotSortable;
        //
        // colPrice
        //
        priceStyle.Alignment = DataGridViewContentAlignment.MiddleRight;
        colPrice.DataPropertyName = "Price";
        colPrice.DefaultCellStyle = priceStyle;
        colPrice.FillWeight = 13F;
        colPrice.HeaderText = "Price (simulated)";
        colPrice.Name = "colPrice";
        colPrice.ReadOnly = true;
        colPrice.SortMode = DataGridViewColumnSortMode.NotSortable;
        //
        // lblDisclaimer
        //
        lblDisclaimer.Anchor = AnchorStyles.Bottom | AnchorStyles.Left | AnchorStyles.Right;
        lblDisclaimer.ForeColor = SystemColors.GrayText;
        lblDisclaimer.Location = new Point(32, 498);
        lblDisclaimer.Name = "lblDisclaimer";
        lblDisclaimer.Size = new Size(836, 36);
        lblDisclaimer.TabIndex = 9;
        lblDisclaimer.Text = "Prices and seller ratings are simulated for demonstration. The data comes from the local seeded catalog, not from live marketplaces.";
        //
        // btnBack
        //
        btnBack.Anchor = AnchorStyles.Bottom | AnchorStyles.Left;
        btnBack.DialogResult = DialogResult.Cancel;
        btnBack.Location = new Point(32, 544);
        btnBack.Name = "btnBack";
        btnBack.Size = new Size(120, 32);
        btnBack.TabIndex = 6;
        btnBack.Text = "Back";
        btnBack.UseVisualStyleBackColor = true;
        //
        // SearchForm
        //
        AcceptButton = btnSearch;
        AutoScaleDimensions = new SizeF(7F, 15F);
        AutoScaleMode = AutoScaleMode.Font;
        CancelButton = btnBack;
        ClientSize = new Size(900, 600);
        Controls.Add(lblTitle);
        Controls.Add(txtSearch);
        Controls.Add(btnSearch);
        Controls.Add(lblTier);
        Controls.Add(cmbTier);
        Controls.Add(lblRating);
        Controls.Add(cmbRating);
        Controls.Add(btnClearFilters);
        Controls.Add(lblStatus);
        Controls.Add(dgvResults);
        Controls.Add(lblDisclaimer);
        Controls.Add(btnBack);
        MinimumSize = new Size(780, 520);
        Name = "SearchForm";
        StartPosition = FormStartPosition.CenterParent;
        Text = "CatchIt - Search";
        ((System.ComponentModel.ISupportInitialize)dgvResults).EndInit();
        ResumeLayout(false);
        PerformLayout();
    }

    #endregion

    private Label lblTitle;
    private TextBox txtSearch;
    private Button btnSearch;
    private Label lblTier;
    private ComboBox cmbTier;
    private Label lblRating;
    private ComboBox cmbRating;
    private Button btnClearFilters;
    private Label lblStatus;
    private DataGridView dgvResults;
    private DataGridViewTextBoxColumn colProduct;
    private DataGridViewTextBoxColumn colBrand;
    private DataGridViewTextBoxColumn colQuality;
    private DataGridViewTextBoxColumn colMarketplace;
    private DataGridViewTextBoxColumn colSellerRating;
    private DataGridViewTextBoxColumn colPrice;
    private Label lblDisclaimer;
    private Button btnBack;
}
