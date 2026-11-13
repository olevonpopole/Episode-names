using System.Windows.Forms;
namespace Episode_Names.Anisearch
{
    partial class Anisearch_Table
    {
        /// <summary>
        /// Required designer variable.
        /// </summary>
        private System.ComponentModel.IContainer components = null;

        /// <summary>
        /// Clean up any resources being used.
        /// </summary>
        /// <param name="disposing">true if managed resources should be disposed; otherwise, false.</param>
        protected override void Dispose(bool disposing)
        {
            if (disposing && (components != null))
            {
                components.Dispose();
            }
            base.Dispose(disposing);
        }

        #region Windows Form Designer generated code

        /// <summary>
        /// Required method for Designer support - do not modify
        /// the contents of this method with the code editor.
        /// </summary>
        private void InitializeComponent()
        {
            this.components = new System.ComponentModel.Container();
            System.ComponentModel.ComponentResourceManager resources = new System.ComponentModel.ComponentResourceManager(typeof(Anisearch_Table));
            this.txtUrl = new System.Windows.Forms.TextBox();
            this.btnOk = new System.Windows.Forms.Button();
            this.cmbLanguageEpisodes = new System.Windows.Forms.ComboBox();
            this.listResult = new System.Windows.Forms.ListBox();
            this.contextMenuStrip1 = new System.Windows.Forms.ContextMenuStrip(this.components);
            this.linkImBrowserÖffnenToolStripMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            this.txtSearch = new System.Windows.Forms.TextBox();
            this.cmbLanguageSearch = new System.Windows.Forms.ComboBox();
            this.label1 = new System.Windows.Forms.Label();
            this.label2 = new System.Windows.Forms.Label();
            this.statusStrip1 = new System.Windows.Forms.StatusStrip();
            this.pgLoading = new System.Windows.Forms.ToolStripProgressBar();
            this.label3 = new System.Windows.Forms.Label();
            this.cmbWebsite = new System.Windows.Forms.ComboBox();
            this.label4 = new System.Windows.Forms.Label();
            this.lblSeason = new System.Windows.Forms.Label();
            this.cmbSeasons = new System.Windows.Forms.ComboBox();
            this.btnCheckURL = new System.Windows.Forms.Button();
            this.contextMenuStrip1.SuspendLayout();
            this.statusStrip1.SuspendLayout();
            this.SuspendLayout();
            // 
            // txtUrl
            // 
            this.txtUrl.Anchor = ((System.Windows.Forms.AnchorStyles)(((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Left) 
            | System.Windows.Forms.AnchorStyles.Right)));
            this.txtUrl.Location = new System.Drawing.Point(0, 323);
            this.txtUrl.Name = "txtUrl";
            this.txtUrl.Size = new System.Drawing.Size(191, 20);
            this.txtUrl.TabIndex = 0;
            this.txtUrl.KeyDown += new System.Windows.Forms.KeyEventHandler(this.txtUrl_KeyDown);
            // 
            // btnOk
            // 
            this.btnOk.Location = new System.Drawing.Point(0, 385);
            this.btnOk.Name = "btnOk";
            this.btnOk.Size = new System.Drawing.Size(352, 25);
            this.btnOk.TabIndex = 1;
            this.btnOk.Text = "&OK";
            this.btnOk.UseVisualStyleBackColor = true;
            this.btnOk.Click += new System.EventHandler(this.btnOk_Click);
            // 
            // cmbLanguageEpisodes
            // 
            this.cmbLanguageEpisodes.DropDownStyle = System.Windows.Forms.ComboBoxStyle.DropDownList;
            this.cmbLanguageEpisodes.FormattingEnabled = true;
            this.cmbLanguageEpisodes.Items.AddRange(new object[] {
            "Deutsch",
            "Englisch",
            "Japanisch"});
            this.cmbLanguageEpisodes.Location = new System.Drawing.Point(192, 322);
            this.cmbLanguageEpisodes.Name = "cmbLanguageEpisodes";
            this.cmbLanguageEpisodes.Size = new System.Drawing.Size(83, 21);
            this.cmbLanguageEpisodes.TabIndex = 2;
            // 
            // listResult
            // 
            this.listResult.Anchor = ((System.Windows.Forms.AnchorStyles)(((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Left) 
            | System.Windows.Forms.AnchorStyles.Right)));
            this.listResult.ContextMenuStrip = this.contextMenuStrip1;
            this.listResult.FormattingEnabled = true;
            this.listResult.Location = new System.Drawing.Point(0, 84);
            this.listResult.Name = "listResult";
            this.listResult.Size = new System.Drawing.Size(353, 212);
            this.listResult.TabIndex = 5;
            this.listResult.SelectedIndexChanged += new System.EventHandler(this.listResult_SelectedIndexChanged);
            this.listResult.MouseDown += new System.Windows.Forms.MouseEventHandler(this.listResult_MouseDown);
            // 
            // contextMenuStrip1
            // 
            this.contextMenuStrip1.Items.AddRange(new System.Windows.Forms.ToolStripItem[] {
            this.linkImBrowserÖffnenToolStripMenuItem});
            this.contextMenuStrip1.Name = "contextMenuStrip1";
            this.contextMenuStrip1.Size = new System.Drawing.Size(197, 26);
            // 
            // linkImBrowserÖffnenToolStripMenuItem
            // 
            this.linkImBrowserÖffnenToolStripMenuItem.Name = "linkImBrowserÖffnenToolStripMenuItem";
            this.linkImBrowserÖffnenToolStripMenuItem.Size = new System.Drawing.Size(196, 22);
            this.linkImBrowserÖffnenToolStripMenuItem.Text = "Link im Browser öffnen";
            this.linkImBrowserÖffnenToolStripMenuItem.Click += new System.EventHandler(this.linkImBrowserÖffnenToolStripMenuItem_Click);
            // 
            // txtSearch
            // 
            this.txtSearch.Anchor = ((System.Windows.Forms.AnchorStyles)(((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Left) 
            | System.Windows.Forms.AnchorStyles.Right)));
            this.txtSearch.Location = new System.Drawing.Point(1, 45);
            this.txtSearch.Name = "txtSearch";
            this.txtSearch.Size = new System.Drawing.Size(274, 20);
            this.txtSearch.TabIndex = 6;
            this.txtSearch.KeyDown += new System.Windows.Forms.KeyEventHandler(this.txtSearch_KeyDown);
            // 
            // cmbLanguageSearch
            // 
            this.cmbLanguageSearch.DropDownStyle = System.Windows.Forms.ComboBoxStyle.DropDownList;
            this.cmbLanguageSearch.FormattingEnabled = true;
            this.cmbLanguageSearch.Location = new System.Drawing.Point(272, 44);
            this.cmbLanguageSearch.Name = "cmbLanguageSearch";
            this.cmbLanguageSearch.Size = new System.Drawing.Size(83, 21);
            this.cmbLanguageSearch.TabIndex = 7;
            // 
            // label1
            // 
            this.label1.AutoSize = true;
            this.label1.Location = new System.Drawing.Point(-2, 30);
            this.label1.Name = "label1";
            this.label1.Size = new System.Drawing.Size(52, 13);
            this.label1.TabIndex = 8;
            this.label1.Text = "Suchtext:";
            // 
            // label2
            // 
            this.label2.AutoSize = true;
            this.label2.Location = new System.Drawing.Point(-2, 69);
            this.label2.Name = "label2";
            this.label2.Size = new System.Drawing.Size(62, 13);
            this.label2.TabIndex = 9;
            this.label2.Text = "Ergebnisse:";
            // 
            // statusStrip1
            // 
            this.statusStrip1.Items.AddRange(new System.Windows.Forms.ToolStripItem[] {
            this.pgLoading});
            this.statusStrip1.Location = new System.Drawing.Point(0, 413);
            this.statusStrip1.Name = "statusStrip1";
            this.statusStrip1.Size = new System.Drawing.Size(355, 22);
            this.statusStrip1.TabIndex = 10;
            this.statusStrip1.Text = "statusStrip1";
            // 
            // pgLoading
            // 
            this.pgLoading.Name = "pgLoading";
            this.pgLoading.Size = new System.Drawing.Size(100, 16);
            // 
            // label3
            // 
            this.label3.AutoSize = true;
            this.label3.Location = new System.Drawing.Point(-2, 9);
            this.label3.Name = "label3";
            this.label3.Size = new System.Drawing.Size(49, 13);
            this.label3.TabIndex = 11;
            this.label3.Text = "Website:";
            // 
            // cmbWebsite
            // 
            this.cmbWebsite.DropDownStyle = System.Windows.Forms.ComboBoxStyle.DropDownList;
            this.cmbWebsite.FormattingEnabled = true;
            this.cmbWebsite.Items.AddRange(new object[] {
            "aniDB",
            "Anisearch",
            "Fernsehserien.de",
            "TVDB",
            "TVmaze",
            "TMDB (API-Key)"});
            this.cmbWebsite.Location = new System.Drawing.Point(53, 6);
            this.cmbWebsite.Name = "cmbWebsite";
            this.cmbWebsite.Size = new System.Drawing.Size(121, 21);
            this.cmbWebsite.TabIndex = 12;
            this.cmbWebsite.SelectedIndexChanged += new System.EventHandler(this.cmbWebsite_SelectedIndexChanged);
            // 
            // label4
            // 
            this.label4.AutoSize = true;
            this.label4.Location = new System.Drawing.Point(-2, 307);
            this.label4.Name = "label4";
            this.label4.Size = new System.Drawing.Size(32, 13);
            this.label4.TabIndex = 13;
            this.label4.Text = "URL:";
            // 
            // lblSeason
            // 
            this.lblSeason.AutoSize = true;
            this.lblSeason.Location = new System.Drawing.Point(-2, 358);
            this.lblSeason.Name = "lblSeason";
            this.lblSeason.Size = new System.Drawing.Size(40, 13);
            this.lblSeason.TabIndex = 14;
            this.lblSeason.Text = "Staffel:";
            // 
            // cmbSeasons
            // 
            this.cmbSeasons.DropDownStyle = System.Windows.Forms.ComboBoxStyle.DropDownList;
            this.cmbSeasons.FormattingEnabled = true;
            this.cmbSeasons.Location = new System.Drawing.Point(44, 355);
            this.cmbSeasons.Name = "cmbSeasons";
            this.cmbSeasons.Size = new System.Drawing.Size(231, 21);
            this.cmbSeasons.TabIndex = 15;
            // 
            // btnCheckURL
            // 
            this.btnCheckURL.Location = new System.Drawing.Point(272, 321);
            this.btnCheckURL.Name = "btnCheckURL";
            this.btnCheckURL.Size = new System.Drawing.Size(83, 23);
            this.btnCheckURL.TabIndex = 16;
            this.btnCheckURL.Text = "Check";
            this.btnCheckURL.UseVisualStyleBackColor = true;
            this.btnCheckURL.Click += new System.EventHandler(this.btnCheckURL_Click);
            // 
            // Anisearch_Table
            // 
            this.AutoScaleDimensions = new System.Drawing.SizeF(6F, 13F);
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.ClientSize = new System.Drawing.Size(355, 435);
            this.Controls.Add(this.btnCheckURL);
            this.Controls.Add(this.cmbSeasons);
            this.Controls.Add(this.lblSeason);
            this.Controls.Add(this.label4);
            this.Controls.Add(this.cmbWebsite);
            this.Controls.Add(this.label3);
            this.Controls.Add(this.statusStrip1);
            this.Controls.Add(this.label2);
            this.Controls.Add(this.label1);
            this.Controls.Add(this.cmbLanguageSearch);
            this.Controls.Add(this.txtSearch);
            this.Controls.Add(this.listResult);
            this.Controls.Add(this.cmbLanguageEpisodes);
            this.Controls.Add(this.btnOk);
            this.Controls.Add(this.txtUrl);
            this.FormBorderStyle = System.Windows.Forms.FormBorderStyle.FixedSingle;
            this.Icon = ((System.Drawing.Icon)(resources.GetObject("$this.Icon")));
            this.MaximizeBox = false;
            this.MinimumSize = new System.Drawing.Size(325, 131);
            this.Name = "Anisearch_Table";
            this.StartPosition = System.Windows.Forms.FormStartPosition.CenterScreen;
            this.Text = "Insert Anisearch URL here";
            this.FormClosing += new System.Windows.Forms.FormClosingEventHandler(this.Anisearch_Table_FormClosing);
            this.contextMenuStrip1.ResumeLayout(false);
            this.statusStrip1.ResumeLayout(false);
            this.statusStrip1.PerformLayout();
            this.ResumeLayout(false);
            this.PerformLayout();

        }

        #endregion

        private System.Windows.Forms.TextBox txtUrl;
        private System.Windows.Forms.Button btnOk;
        private ComboBox cmbLanguageEpisodes;
        private ListBox listResult;
        private TextBox txtSearch;
        private ComboBox cmbLanguageSearch;
        private Label label1;
        private Label label2;
        private StatusStrip statusStrip1;
        private ToolStripProgressBar pgLoading;
        private Label label3;
        private ComboBox cmbWebsite;
        private Label label4;
        private ContextMenuStrip contextMenuStrip1;
        private ToolStripMenuItem linkImBrowserÖffnenToolStripMenuItem;
        private Label lblSeason;
        private ComboBox cmbSeasons;
        private Button btnCheckURL;
    }
}