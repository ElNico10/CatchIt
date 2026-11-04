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
        DataGridViewCellStyle rightStyle = new DataGridViewCellStyle();
        DataGridViewCellStyle statusStyle = new DataGridViewCellStyle();
        lblTitle = new Label();
        lblHint = new Label();
        dgvWatch = new DataGridView();
        colProduct = new DataGridViewTextBoxColumn();
        colBrand = new DataGridViewTextBoxColumn();
        colPrice = new DataGridViewTextBoxColumn();
        colMarketplace = new DataGridViewTextBoxColumn();
        colTarget = new DataGridViewTextBoxColumn();
        colStatus = new DataGridViewTextBoxColumn();
        lblEmpty = new Label();
        lblTarget = new Label();
        txtTarget = new TextBox();
        btnSetTarget = new Button();
        btnClearTarget = new Button();
        btnRemove = new Button();
        lblStatus = new Label();
        lblDisclaimer = new Label();
        btnBack = new Button();
        ((System.ComponentModel.ISupportInitialize)dgvWatch).BeginInit();
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
        // lblHint
        //
        lblHint.ForeColor = SystemColors.GrayText;
        lblHint.Location = new Point(32, 56);
        lblHint.Name = "lblHint";
        lblHint.Size = new Size(796, 20);
        lblHint.TabIndex = 1;
        lblHint.Text = "Double-click a product to see its price history. Set a target price to be told when the price reaches it.";
        //
        // dgvWatch
        //
        dgvWatch.AllowUserToAddRows = false;
        dgvWatch.AllowUserToDeleteRows = false;
        dgvWatch.AllowUserToResizeRows = false;
        dgvWatch.AutoGenerateColumns = false;
        dgvWatch.AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill;
        dgvWatch.BackgroundColor = SystemColors.Window;
        dgvWatch.ColumnHeadersHeightSizeMode = DataGridViewColumnHeadersHeightSizeMode.AutoSize;
        dgvWatch.Columns.AddRange(new DataGridViewColumn[] { colProduct, colBrand, colPrice, colMarketplace, colTarget, colStatus });
        dgvWatch.Location = new Point(32, 84);
        dgvWatch.MultiSelect = false;
        dgvWatch.Name = "dgvWatch";
        dgvWatch.ReadOnly = true;
        dgvWatch.RowHeadersVisible = false;
        dgvWatch.SelectionMode = DataGridViewSelectionMode.FullRowSelect;
        dgvWatch.ShowCellToolTips = true;
        dgvWatch.Size = new Size(796, 290);
        dgvWatch.TabIndex = 2;
        dgvWatch.CellDoubleClick += dgvWatch_CellDoubleClick;
        dgvWatch.SelectionChanged += dgvWatch_SelectionChanged;
        //
        // colProduct
        //
        colProduct.DataPropertyName = "Product";
        colProduct.FillWeight = 34F;
        colProduct.HeaderText = "Product";
        colProduct.Name = "colProduct";
        colProduct.ReadOnly = true;
        colProduct.SortMode = DataGridViewColumnSortMode.NotSortable;
        //
        // colBrand
        //
        colBrand.DataPropertyName = "Brand";
        colBrand.FillWeight = 14F;
        colBrand.HeaderText = "Brand";
        colBrand.Name = "colBrand";
        colBrand.ReadOnly = true;
        colBrand.SortMode = DataGridViewColumnSortMode.NotSortable;
        //
        // colPrice
        //
        rightStyle.Alignment = DataGridViewContentAlignment.MiddleRight;
        colPrice.DataPropertyName = "Price";
        colPrice.DefaultCellStyle = rightStyle;
        colPrice.FillWeight = 12F;
        colPrice.HeaderText = "Lowest price";
        colPrice.Name = "colPrice";
        colPrice.ReadOnly = true;
        colPrice.SortMode = DataGridViewColumnSortMode.NotSortable;
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
        // colTarget
        //
        colTarget.DataPropertyName = "Target";
        colTarget.DefaultCellStyle = rightStyle;
        colTarget.FillWeight = 12F;
        colTarget.HeaderText = "Target price";
        colTarget.Name = "colTarget";
        colTarget.ReadOnly = true;
        colTarget.SortMode = DataGridViewColumnSortMode.NotSortable;
        //
        // colStatus
        //
        statusStyle.Font = new Font("Segoe UI", 9F, FontStyle.Bold);
        statusStyle.ForeColor = Color.ForestGreen;
        colStatus.DataPropertyName = "Status";
        colStatus.DefaultCellStyle = statusStyle;
        colStatus.FillWeight = 14F;
        colStatus.HeaderText = "";
        colStatus.Name = "colStatus";
        colStatus.ReadOnly = true;
        colStatus.SortMode = DataGridViewColumnSortMode.NotSortable;
        //
        // lblEmpty
        //
        lblEmpty.Location = new Point(32, 100);
        lblEmpty.Name = "lblEmpty";
        lblEmpty.Size = new Size(796, 60);
        lblEmpty.TabIndex = 3;
        lblEmpty.Text = "Your watchlist is empty. Find a product with Search, open it, and choose Add to watchlist.";
        lblEmpty.Visible = false;
        //
        // lblTarget
        //
        lblTarget.AutoSize = true;
        lblTarget.Location = new Point(32, 391);
        lblTarget.Name = "lblTarget";
        lblTarget.Size = new Size(125, 15);
        lblTarget.TabIndex = 4;
        lblTarget.Text = "Target price (dollars):";
        //
        // txtTarget
        //
        txtTarget.Location = new Point(166, 387);
        txtTarget.Name = "txtTarget";
        txtTarget.Size = new Size(110, 23);
        txtTarget.TabIndex = 5;
        //
        // btnSetTarget
        //
        btnSetTarget.Location = new Point(288, 384);
        btnSetTarget.Name = "btnSetTarget";
        btnSetTarget.Size = new Size(110, 30);
        btnSetTarget.TabIndex = 6;
        btnSetTarget.Text = "Set target";
        btnSetTarget.UseVisualStyleBackColor = true;
        btnSetTarget.Click += btnSetTarget_Click;
        //
        // btnClearTarget
        //
        btnClearTarget.Location = new Point(406, 384);
        btnClearTarget.Name = "btnClearTarget";
        btnClearTarget.Size = new Size(110, 30);
        btnClearTarget.TabIndex = 7;
        btnClearTarget.Text = "Clear target";
        btnClearTarget.UseVisualStyleBackColor = true;
        btnClearTarget.Click += btnClearTarget_Click;
        //
        // btnRemove
        //
        btnRemove.Location = new Point(688, 384);
        btnRemove.Name = "btnRemove";
        btnRemove.Size = new Size(140, 30);
        btnRemove.TabIndex = 8;
        btnRemove.Text = "Remove from list";
        btnRemove.UseVisualStyleBackColor = true;
        btnRemove.Click += btnRemove_Click;
        //
        // lblStatus
        //
        lblStatus.Location = new Point(32, 424);
        lblStatus.Name = "lblStatus";
        lblStatus.Size = new Size(796, 22);
        lblStatus.TabIndex = 9;
        //
        // lblDisclaimer
        //
        lblDisclaimer.ForeColor = SystemColors.GrayText;
        lblDisclaimer.Location = new Point(32, 456);
        lblDisclaimer.Name = "lblDisclaimer";
        lblDisclaimer.Size = new Size(796, 36);
        lblDisclaimer.TabIndex = 10;
        //
        // btnBack
        //
        btnBack.DialogResult = DialogResult.Cancel;
        btnBack.Location = new Point(32, 502);
        btnBack.Name = "btnBack";
        btnBack.Size = new Size(120, 32);
        btnBack.TabIndex = 11;
        btnBack.Text = "Back";
        btnBack.UseVisualStyleBackColor = true;
        //
        // WatchlistForm
        //
        AutoScaleDimensions = new SizeF(7F, 15F);
        AutoScaleMode = AutoScaleMode.Font;
        CancelButton = btnBack;
        ClientSize = new Size(860, 552);
        Controls.Add(lblTitle);
        Controls.Add(lblHint);
        Controls.Add(dgvWatch);
        Controls.Add(lblEmpty);
        Controls.Add(lblTarget);
        Controls.Add(txtTarget);
        Controls.Add(btnSetTarget);
        Controls.Add(btnClearTarget);
        Controls.Add(btnRemove);
        Controls.Add(lblStatus);
        Controls.Add(lblDisclaimer);
        Controls.Add(btnBack);
        FormBorderStyle = FormBorderStyle.FixedDialog;
        MaximizeBox = false;
        MinimizeBox = false;
        Name = "WatchlistForm";
        StartPosition = FormStartPosition.CenterParent;
        Text = "CatchIt - Watchlist";
        ((System.ComponentModel.ISupportInitialize)dgvWatch).EndInit();
        ResumeLayout(false);
        PerformLayout();
    }

    #endregion

    private Label lblTitle;
    private Label lblHint;
    private DataGridView dgvWatch;
    private DataGridViewTextBoxColumn colProduct;
    private DataGridViewTextBoxColumn colBrand;
    private DataGridViewTextBoxColumn colPrice;
    private DataGridViewTextBoxColumn colMarketplace;
    private DataGridViewTextBoxColumn colTarget;
    private DataGridViewTextBoxColumn colStatus;
    private Label lblEmpty;
    private Label lblTarget;
    private TextBox txtTarget;
    private Button btnSetTarget;
    private Button btnClearTarget;
    private Button btnRemove;
    private Label lblStatus;
    private Label lblDisclaimer;
    private Button btnBack;
}
