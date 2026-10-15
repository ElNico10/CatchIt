namespace CatchIt.App;

partial class MainForm
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
        pnlTop = new Panel();
        lblUser = new Label();
        btnLogout = new Button();
        pnlBanner = new Panel();
        lblBanner = new Label();
        btnConfirm = new Button();
        pnlContent = new Panel();
        lblWelcome = new Label();
        btnSearch = new Button();
        btnWatchlist = new Button();
        btnDashboard = new Button();
        btnProfile = new Button();
        lblGuestNote = new Label();
        pnlTop.SuspendLayout();
        pnlBanner.SuspendLayout();
        pnlContent.SuspendLayout();
        SuspendLayout();
        //
        // pnlTop
        //
        pnlTop.BackColor = SystemColors.ControlLight;
        pnlTop.Controls.Add(lblUser);
        pnlTop.Controls.Add(btnLogout);
        pnlTop.Dock = DockStyle.Top;
        pnlTop.Location = new Point(0, 0);
        pnlTop.Name = "pnlTop";
        pnlTop.Size = new Size(640, 48);
        pnlTop.TabIndex = 0;
        //
        // lblUser
        //
        lblUser.Anchor = AnchorStyles.Top | AnchorStyles.Bottom | AnchorStyles.Left | AnchorStyles.Right;
        lblUser.Location = new Point(16, 0);
        lblUser.Name = "lblUser";
        lblUser.Size = new Size(500, 48);
        lblUser.TabIndex = 0;
        lblUser.TextAlign = ContentAlignment.MiddleLeft;
        //
        // btnLogout
        //
        btnLogout.Anchor = AnchorStyles.Top | AnchorStyles.Right;
        btnLogout.Location = new Point(528, 8);
        btnLogout.Name = "btnLogout";
        btnLogout.Size = new Size(100, 32);
        btnLogout.TabIndex = 1;
        btnLogout.Text = "Logout";
        btnLogout.UseVisualStyleBackColor = true;
        btnLogout.Click += btnLogout_Click;
        //
        // pnlBanner
        //
        pnlBanner.BackColor = Color.FromArgb(255, 244, 200);
        pnlBanner.Controls.Add(lblBanner);
        pnlBanner.Controls.Add(btnConfirm);
        pnlBanner.Dock = DockStyle.Top;
        pnlBanner.Location = new Point(0, 48);
        pnlBanner.Name = "pnlBanner";
        pnlBanner.Size = new Size(640, 44);
        pnlBanner.TabIndex = 1;
        pnlBanner.Visible = false;
        //
        // lblBanner
        //
        lblBanner.Anchor = AnchorStyles.Top | AnchorStyles.Bottom | AnchorStyles.Left | AnchorStyles.Right;
        lblBanner.Location = new Point(16, 0);
        lblBanner.Name = "lblBanner";
        lblBanner.Size = new Size(486, 44);
        lblBanner.TabIndex = 0;
        lblBanner.Text = "Your account isn't confirmed yet. You can keep using CatchIt while you confirm it.";
        lblBanner.TextAlign = ContentAlignment.MiddleLeft;
        //
        // btnConfirm
        //
        btnConfirm.Anchor = AnchorStyles.Top | AnchorStyles.Right;
        btnConfirm.Location = new Point(508, 6);
        btnConfirm.Name = "btnConfirm";
        btnConfirm.Size = new Size(120, 32);
        btnConfirm.TabIndex = 1;
        btnConfirm.Text = "Confirm account";
        btnConfirm.UseVisualStyleBackColor = true;
        btnConfirm.Click += btnConfirm_Click;
        //
        // pnlContent
        //
        pnlContent.Controls.Add(lblWelcome);
        pnlContent.Controls.Add(btnSearch);
        pnlContent.Controls.Add(btnWatchlist);
        pnlContent.Controls.Add(btnDashboard);
        pnlContent.Controls.Add(btnProfile);
        pnlContent.Controls.Add(lblGuestNote);
        pnlContent.Dock = DockStyle.Fill;
        pnlContent.Location = new Point(0, 48);
        pnlContent.Name = "pnlContent";
        pnlContent.Size = new Size(640, 292);
        pnlContent.TabIndex = 2;
        //
        // lblWelcome
        //
        lblWelcome.AutoSize = true;
        lblWelcome.Font = new Font("Segoe UI", 16F, FontStyle.Bold);
        lblWelcome.Location = new Point(30, 22);
        lblWelcome.Name = "lblWelcome";
        lblWelcome.Size = new Size(246, 30);
        lblWelcome.TabIndex = 1;
        lblWelcome.Text = "What would you like to do?";
        //
        // btnSearch
        //
        btnSearch.Location = new Point(32, 72);
        btnSearch.Name = "btnSearch";
        btnSearch.Font = new Font("Segoe UI", 12F);
        btnSearch.Size = new Size(280, 64);
        btnSearch.TabIndex = 0;
        btnSearch.Text = "Search products";
        btnSearch.UseVisualStyleBackColor = true;
        btnSearch.Click += btnSearch_Click;
        //
        // btnWatchlist
        //
        btnWatchlist.Location = new Point(328, 72);
        btnWatchlist.Name = "btnWatchlist";
        btnWatchlist.Font = new Font("Segoe UI", 12F);
        btnWatchlist.Size = new Size(280, 64);
        btnWatchlist.TabIndex = 1;
        btnWatchlist.Text = "My watchlist";
        btnWatchlist.UseVisualStyleBackColor = true;
        btnWatchlist.Click += btnWatchlist_Click;
        //
        // btnDashboard
        //
        btnDashboard.Location = new Point(32, 152);
        btnDashboard.Name = "btnDashboard";
        btnDashboard.Font = new Font("Segoe UI", 12F);
        btnDashboard.Size = new Size(280, 64);
        btnDashboard.TabIndex = 2;
        btnDashboard.Text = "Dashboard (below target price)";
        btnDashboard.UseVisualStyleBackColor = true;
        btnDashboard.Click += btnDashboard_Click;
        //
        // btnProfile
        //
        btnProfile.Location = new Point(328, 152);
        btnProfile.Name = "btnProfile";
        btnProfile.Font = new Font("Segoe UI", 12F);
        btnProfile.Size = new Size(280, 64);
        btnProfile.TabIndex = 3;
        btnProfile.Text = "My profile";
        btnProfile.UseVisualStyleBackColor = true;
        btnProfile.Click += btnProfile_Click;
        //
        // lblGuestNote
        //
        lblGuestNote.ForeColor = SystemColors.GrayText;
        lblGuestNote.Location = new Point(32, 234);
        lblGuestNote.Name = "lblGuestNote";
        lblGuestNote.Size = new Size(576, 36);
        lblGuestNote.TabIndex = 6;
        lblGuestNote.Text = "You are browsing as a guest. The watchlist, dashboard and profile need an account because they save your data.";
        //
        // MainForm
        //
        AutoScaleDimensions = new SizeF(7F, 15F);
        AutoScaleMode = AutoScaleMode.Font;
        ClientSize = new Size(640, 340);
        Controls.Add(pnlContent);
        Controls.Add(pnlBanner);
        Controls.Add(pnlTop);
        FormBorderStyle = FormBorderStyle.FixedSingle;
        MaximizeBox = false;
        Name = "MainForm";
        StartPosition = FormStartPosition.CenterScreen;
        Text = "CatchIt";
        pnlTop.ResumeLayout(false);
        pnlBanner.ResumeLayout(false);
        pnlContent.ResumeLayout(false);
        pnlContent.PerformLayout();
        ResumeLayout(false);
    }

    #endregion

    private Panel pnlTop;
    private Label lblUser;
    private Button btnLogout;
    private Panel pnlBanner;
    private Label lblBanner;
    private Button btnConfirm;
    private Panel pnlContent;
    private Label lblWelcome;
    private Button btnSearch;
    private Button btnWatchlist;
    private Button btnDashboard;
    private Button btnProfile;
    private Label lblGuestNote;
}
