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
        DataGridViewCellStyle rightStyle = new DataGridViewCellStyle();
        lblTitle = new Label();
        lblHint = new Label();
        lblBelow = new Label();
        dgvBelow = new DataGridView();
        lblBelowEmpty = new Label();
        lblNotYet = new Label();
        dgvNotYet = new DataGridView();
        lblNotYetEmpty = new Label();
        lblNote = new Label();
        lblDisclaimer = new Label();
        btnBack = new Button();
        dgvBelowProduct = new DataGridViewTextBoxColumn();
        dgvBelowBrand = new DataGridViewTextBoxColumn();
        dgvBelowPrice = new DataGridViewTextBoxColumn();
        dgvBelowTarget = new DataGridViewTextBoxColumn();
        dgvBelowGap = new DataGridViewTextBoxColumn();
        dgvBelowAverage = new DataGridViewTextBoxColumn();
        dgvNotYetProduct = new DataGridViewTextBoxColumn();
        dgvNotYetBrand = new DataGridViewTextBoxColumn();
        dgvNotYetPrice = new DataGridViewTextBoxColumn();
        dgvNotYetTarget = new DataGridViewTextBoxColumn();
        dgvNotYetGap = new DataGridViewTextBoxColumn();
        dgvNotYetAverage = new DataGridViewTextBoxColumn();
        ((System.ComponentModel.ISupportInitialize)dgvBelow).BeginInit();
        ((System.ComponentModel.ISupportInitialize)dgvNotYet).BeginInit();
        SuspendLayout();
        rightStyle.Alignment = DataGridViewContentAlignment.MiddleRight;
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
        // lblHint
        //
        lblHint.ForeColor = SystemColors.GrayText;
        lblHint.Location = new Point(32, 56);
        lblHint.Name = "lblHint";
        lblHint.Size = new Size(836, 20);
        lblHint.TabIndex = 1;
        lblHint.Text = "Double-click a product to see its price history. The last column compares today's price with its usual price over the past 90 days.";
        //
        // lblBelow
        //
        lblBelow.AutoSize = true;
        lblBelow.Font = new Font("Segoe UI", 11F, FontStyle.Bold);
        lblBelow.ForeColor = Color.ForestGreen;
        lblBelow.Location = new Point(32, 88);
        lblBelow.Name = "lblBelow";
        lblBelow.Size = new Size(147, 20);
        lblBelow.TabIndex = 2;
        lblBelow.Text = "Below Target Price";
        //
        // dgvBelow
        //
        dgvBelow.AllowUserToAddRows = false;
        dgvBelow.AllowUserToDeleteRows = false;
        dgvBelow.AllowUserToResizeRows = false;
        dgvBelow.AutoGenerateColumns = false;
        dgvBelow.AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill;
        dgvBelow.BackgroundColor = SystemColors.Window;
        dgvBelow.ColumnHeadersHeightSizeMode = DataGridViewColumnHeadersHeightSizeMode.AutoSize;
        dgvBelow.Columns.AddRange(new DataGridViewColumn[] { dgvBelowProduct, dgvBelowBrand, dgvBelowPrice, dgvBelowTarget, dgvBelowGap, dgvBelowAverage });
        dgvBelow.Location = new Point(32, 116);
        dgvBelow.MultiSelect = false;
        dgvBelow.Name = "dgvBelow";
        dgvBelow.ReadOnly = true;
        dgvBelow.RowHeadersVisible = false;
        dgvBelow.SelectionMode = DataGridViewSelectionMode.FullRowSelect;
        dgvBelow.Size = new Size(836, 150);
        dgvBelow.TabIndex = 3;
        dgvBelow.CellDoubleClick += grid_CellDoubleClick;
        //
        // dgvBelowProduct
        //
        dgvBelowProduct.DataPropertyName = "Product";
        dgvBelowProduct.FillWeight = 38F;
        dgvBelowProduct.HeaderText = "Product";
        dgvBelowProduct.Name = "dgvBelowProduct";
        dgvBelowProduct.ReadOnly = true;
        dgvBelowProduct.SortMode = DataGridViewColumnSortMode.NotSortable;
        //
        // dgvBelowBrand
        //
        dgvBelowBrand.DataPropertyName = "Brand";
        dgvBelowBrand.FillWeight = 13F;
        dgvBelowBrand.HeaderText = "Brand";
        dgvBelowBrand.Name = "dgvBelowBrand";
        dgvBelowBrand.ReadOnly = true;
        dgvBelowBrand.SortMode = DataGridViewColumnSortMode.NotSortable;
        //
        // dgvBelowPrice
        //
        dgvBelowPrice.DataPropertyName = "Price";
        dgvBelowPrice.DefaultCellStyle = rightStyle;
        dgvBelowPrice.FillWeight = 11F;
        dgvBelowPrice.HeaderText = "Price";
        dgvBelowPrice.Name = "dgvBelowPrice";
        dgvBelowPrice.ReadOnly = true;
        dgvBelowPrice.SortMode = DataGridViewColumnSortMode.NotSortable;
        //
        // dgvBelowTarget
        //
        dgvBelowTarget.DataPropertyName = "Target";
        dgvBelowTarget.DefaultCellStyle = rightStyle;
        dgvBelowTarget.FillWeight = 11F;
        dgvBelowTarget.HeaderText = "Target";
        dgvBelowTarget.Name = "dgvBelowTarget";
        dgvBelowTarget.ReadOnly = true;
        dgvBelowTarget.SortMode = DataGridViewColumnSortMode.NotSortable;
        //
        // dgvBelowGap
        //
        dgvBelowGap.DataPropertyName = "Gap";
        dgvBelowGap.DefaultCellStyle = rightStyle;
        dgvBelowGap.FillWeight = 16F;
        dgvBelowGap.HeaderText = "Gap to target";
        dgvBelowGap.Name = "dgvBelowGap";
        dgvBelowGap.ReadOnly = true;
        dgvBelowGap.SortMode = DataGridViewColumnSortMode.NotSortable;
        //
        // dgvBelowAverage
        //
        dgvBelowAverage.DataPropertyName = "Average";
        dgvBelowAverage.DefaultCellStyle = rightStyle;
        dgvBelowAverage.FillWeight = 17F;
        dgvBelowAverage.HeaderText = "Versus 90-day average";
        dgvBelowAverage.Name = "dgvBelowAverage";
        dgvBelowAverage.ReadOnly = true;
        dgvBelowAverage.SortMode = DataGridViewColumnSortMode.NotSortable;
        //
        // lblBelowEmpty
        //
        lblBelowEmpty.ForeColor = SystemColors.GrayText;
        lblBelowEmpty.Location = new Point(32, 120);
        lblBelowEmpty.Name = "lblBelowEmpty";
        lblBelowEmpty.Size = new Size(836, 24);
        lblBelowEmpty.TabIndex = 4;
        //
        // lblNotYet
        //
        lblNotYet.AutoSize = true;
        lblNotYet.Font = new Font("Segoe UI", 11F, FontStyle.Bold);
        lblNotYet.Location = new Point(32, 280);
        lblNotYet.Name = "lblNotYet";
        lblNotYet.Size = new Size(260, 20);
        lblNotYet.TabIndex = 5;
        lblNotYet.Text = "Not at target yet (closest first)";
        //
        // dgvNotYet
        //
        dgvNotYet.AllowUserToAddRows = false;
        dgvNotYet.AllowUserToDeleteRows = false;
        dgvNotYet.AllowUserToResizeRows = false;
        dgvNotYet.AutoGenerateColumns = false;
        dgvNotYet.AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill;
        dgvNotYet.BackgroundColor = SystemColors.Window;
        dgvNotYet.ColumnHeadersHeightSizeMode = DataGridViewColumnHeadersHeightSizeMode.AutoSize;
        dgvNotYet.Columns.AddRange(new DataGridViewColumn[] { dgvNotYetProduct, dgvNotYetBrand, dgvNotYetPrice, dgvNotYetTarget, dgvNotYetGap, dgvNotYetAverage });
        dgvNotYet.Location = new Point(32, 308);
        dgvNotYet.MultiSelect = false;
        dgvNotYet.Name = "dgvNotYet";
        dgvNotYet.ReadOnly = true;
        dgvNotYet.RowHeadersVisible = false;
        dgvNotYet.SelectionMode = DataGridViewSelectionMode.FullRowSelect;
        dgvNotYet.Size = new Size(836, 170);
        dgvNotYet.TabIndex = 6;
        dgvNotYet.CellDoubleClick += grid_CellDoubleClick;
        //
        // dgvNotYetProduct
        //
        dgvNotYetProduct.DataPropertyName = "Product";
        dgvNotYetProduct.FillWeight = 38F;
        dgvNotYetProduct.HeaderText = "Product";
        dgvNotYetProduct.Name = "dgvNotYetProduct";
        dgvNotYetProduct.ReadOnly = true;
        dgvNotYetProduct.SortMode = DataGridViewColumnSortMode.NotSortable;
        //
        // dgvNotYetBrand
        //
        dgvNotYetBrand.DataPropertyName = "Brand";
        dgvNotYetBrand.FillWeight = 13F;
        dgvNotYetBrand.HeaderText = "Brand";
        dgvNotYetBrand.Name = "dgvNotYetBrand";
        dgvNotYetBrand.ReadOnly = true;
        dgvNotYetBrand.SortMode = DataGridViewColumnSortMode.NotSortable;
        //
        // dgvNotYetPrice
        //
        dgvNotYetPrice.DataPropertyName = "Price";
        dgvNotYetPrice.DefaultCellStyle = rightStyle;
        dgvNotYetPrice.FillWeight = 11F;
        dgvNotYetPrice.HeaderText = "Price";
        dgvNotYetPrice.Name = "dgvNotYetPrice";
        dgvNotYetPrice.ReadOnly = true;
        dgvNotYetPrice.SortMode = DataGridViewColumnSortMode.NotSortable;
        //
        // dgvNotYetTarget
        //
        dgvNotYetTarget.DataPropertyName = "Target";
        dgvNotYetTarget.DefaultCellStyle = rightStyle;
        dgvNotYetTarget.FillWeight = 11F;
        dgvNotYetTarget.HeaderText = "Target";
        dgvNotYetTarget.Name = "dgvNotYetTarget";
        dgvNotYetTarget.ReadOnly = true;
        dgvNotYetTarget.SortMode = DataGridViewColumnSortMode.NotSortable;
        //
        // dgvNotYetGap
        //
        dgvNotYetGap.DataPropertyName = "Gap";
        dgvNotYetGap.DefaultCellStyle = rightStyle;
        dgvNotYetGap.FillWeight = 16F;
        dgvNotYetGap.HeaderText = "Gap to target";
        dgvNotYetGap.Name = "dgvNotYetGap";
        dgvNotYetGap.ReadOnly = true;
        dgvNotYetGap.SortMode = DataGridViewColumnSortMode.NotSortable;
        //
        // dgvNotYetAverage
        //
        dgvNotYetAverage.DataPropertyName = "Average";
        dgvNotYetAverage.DefaultCellStyle = rightStyle;
        dgvNotYetAverage.FillWeight = 17F;
        dgvNotYetAverage.HeaderText = "Versus 90-day average";
        dgvNotYetAverage.Name = "dgvNotYetAverage";
        dgvNotYetAverage.ReadOnly = true;
        dgvNotYetAverage.SortMode = DataGridViewColumnSortMode.NotSortable;
        //
        // lblNotYetEmpty
        //
        lblNotYetEmpty.ForeColor = SystemColors.GrayText;
        lblNotYetEmpty.Location = new Point(32, 312);
        lblNotYetEmpty.Name = "lblNotYetEmpty";
        lblNotYetEmpty.Size = new Size(836, 24);
        lblNotYetEmpty.TabIndex = 7;
        //
        // lblNote
        //
        lblNote.Location = new Point(32, 488);
        lblNote.Name = "lblNote";
        lblNote.Size = new Size(836, 22);
        lblNote.TabIndex = 8;
        //
        // lblDisclaimer
        //
        lblDisclaimer.ForeColor = SystemColors.GrayText;
        lblDisclaimer.Location = new Point(32, 516);
        lblDisclaimer.Name = "lblDisclaimer";
        lblDisclaimer.Size = new Size(836, 36);
        lblDisclaimer.TabIndex = 9;
        //
        // btnBack
        //
        btnBack.DialogResult = DialogResult.Cancel;
        btnBack.Location = new Point(32, 562);
        btnBack.Name = "btnBack";
        btnBack.Size = new Size(120, 32);
        btnBack.TabIndex = 10;
        btnBack.Text = "Back";
        btnBack.UseVisualStyleBackColor = true;
        //
        // DashboardForm
        //
        AutoScaleDimensions = new SizeF(7F, 15F);
        AutoScaleMode = AutoScaleMode.Font;
        CancelButton = btnBack;
        ClientSize = new Size(900, 612);
        Controls.Add(lblTitle);
        Controls.Add(lblHint);
        Controls.Add(lblBelow);
        Controls.Add(dgvBelow);
        Controls.Add(lblBelowEmpty);
        Controls.Add(lblNotYet);
        Controls.Add(dgvNotYet);
        Controls.Add(lblNotYetEmpty);
        Controls.Add(lblNote);
        Controls.Add(lblDisclaimer);
        Controls.Add(btnBack);
        FormBorderStyle = FormBorderStyle.FixedDialog;
        MaximizeBox = false;
        MinimizeBox = false;
        Name = "DashboardForm";
        StartPosition = FormStartPosition.CenterParent;
        Text = "CatchIt - Dashboard";
        ((System.ComponentModel.ISupportInitialize)dgvBelow).EndInit();
        ((System.ComponentModel.ISupportInitialize)dgvNotYet).EndInit();
        ResumeLayout(false);
        PerformLayout();
    }

    #endregion

    private Label lblTitle;
    private Label lblHint;
    private Label lblBelow;
    private DataGridView dgvBelow;
    private Label lblBelowEmpty;
    private Label lblNotYet;
    private DataGridView dgvNotYet;
    private Label lblNotYetEmpty;
    private Label lblNote;
    private Label lblDisclaimer;
    private Button btnBack;
    private DataGridViewTextBoxColumn dgvBelowProduct;
    private DataGridViewTextBoxColumn dgvBelowBrand;
    private DataGridViewTextBoxColumn dgvBelowPrice;
    private DataGridViewTextBoxColumn dgvBelowTarget;
    private DataGridViewTextBoxColumn dgvBelowGap;
    private DataGridViewTextBoxColumn dgvBelowAverage;
    private DataGridViewTextBoxColumn dgvNotYetProduct;
    private DataGridViewTextBoxColumn dgvNotYetBrand;
    private DataGridViewTextBoxColumn dgvNotYetPrice;
    private DataGridViewTextBoxColumn dgvNotYetTarget;
    private DataGridViewTextBoxColumn dgvNotYetGap;
    private DataGridViewTextBoxColumn dgvNotYetAverage;
}
