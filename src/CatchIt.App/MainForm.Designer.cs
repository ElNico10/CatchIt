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
        lblWelcome = new Label();
        btnSearch = new Button();
        btnWatchlist = new Button();
        btnDashboard = new Button();
        btnProfile = new Button();
        lblGuestNote = new Label();
        pnlTop.SuspendLayout();
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
        // lblWelcome
        //
        lblWelcome.AutoSize = true;
        lblWelcome.Font = new Font("Segoe UI", 16F, FontStyle.Bold);
        lblWelcome.Location = new Point(30, 70);
        lblWelcome.Name = "lblWelcome";
        lblWelcome.Size = new Size(246, 30);
        lblWelcome.TabIndex = 1;
        lblWelcome.Text = "What would you like to do?";
        //
        // btnSearch
        //
        btnSearch.Location = new Point(32, 120);
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
        btnWatchlist.Location = new Point(328, 120);
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
        btnDashboard.Location = new Point(32, 200);
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
        btnProfile.Location = new Point(328, 200);
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
        lblGuestNote.Location = new Point(32, 282);
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
        Controls.Add(pnlTop);
        Controls.Add(lblWelcome);
        Controls.Add(btnSearch);
        Controls.Add(btnWatchlist);
        Controls.Add(btnDashboard);
        Controls.Add(btnProfile);
        Controls.Add(lblGuestNote);
        FormBorderStyle = FormBorderStyle.FixedSingle;
        MaximizeBox = false;
        Name = "MainForm";
        StartPosition = FormStartPosition.CenterScreen;
        Text = "CatchIt";
        pnlTop.ResumeLayout(false);
        ResumeLayout(false);
        PerformLayout();
    }

    #endregion

    private Panel pnlTop;
    private Label lblUser;
    private Button btnLogout;
    private Label lblWelcome;
    private Button btnSearch;
    private Button btnWatchlist;
    private Button btnDashboard;
    private Button btnProfile;
    private Label lblGuestNote;
}
