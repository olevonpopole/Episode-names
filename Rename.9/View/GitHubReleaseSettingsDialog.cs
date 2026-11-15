using Episode_Names.Helper;
using System;
using System.Drawing;
using System.Windows.Forms;

namespace Episode_Names.View
{
    internal sealed class GitHubReleaseSettingsDialog : Form
    {
        private readonly TextBox repository;
        private readonly TextBox token;
        private readonly CheckBox clearToken;

        internal GitHubReleaseSettingsDialog()
        {
            Text = "GitHub-Releases – Update-Einstellungen";
            Size = new Size(550, 280);
            MinimumSize = new Size(480, 260);
            StartPosition = FormStartPosition.CenterParent;
            FormBorderStyle = FormBorderStyle.FixedDialog;
            MaximizeBox = false;
            var panel = new TableLayoutPanel { Dock = DockStyle.Fill,
                RowCount = 6, ColumnCount = 1, Padding = new Padding(12) };
            panel.RowStyles.Add(new RowStyle(SizeType.Absolute, 24));
            panel.RowStyles.Add(new RowStyle(SizeType.Absolute, 32));
            panel.RowStyles.Add(new RowStyle(SizeType.Absolute, 43));
            panel.RowStyles.Add(new RowStyle(SizeType.Absolute, 32));
            panel.RowStyles.Add(new RowStyle(SizeType.Absolute, 35));
            panel.RowStyles.Add(new RowStyle(SizeType.Percent, 100));

            panel.Controls.Add(new Label {
                Text = "GitHub-Repository (Besitzer/Name):",
                Dock = DockStyle.Fill, AutoSize = true }, 0, 0);
            repository = new TextBox { Dock = DockStyle.Top,
                Text = Properties.Settings.Default.GitHubUpdateRepository };
            panel.Controls.Add(repository, 0, 1);
            panel.Controls.Add(new Label {
                Dock = DockStyle.Fill,
                Text = "Privates Repo: Fine-grained Personal Access Token mit Contents: Read. " +
                    "Leer lassen, um einen gespeicherten Token zu behalten.",
                AutoSize = true }, 0, 2);
            token = new TextBox { Dock = DockStyle.Top,
                UseSystemPasswordChar = true };
            panel.Controls.Add(token, 0, 3);
            clearToken = new CheckBox { Text = "Gespeicherten Token löschen",
                AutoSize = true, Dock = DockStyle.Fill };
            panel.Controls.Add(clearToken, 0, 4);
            var buttons = new FlowLayoutPanel {
                Dock = DockStyle.Fill, FlowDirection = FlowDirection.RightToLeft
            };
            var save = new Button { Text = "Speichern", Width = 100 };
            var cancel = new Button { Text = "Abbrechen", Width = 100,
                DialogResult = DialogResult.Cancel };
            save.Click += (s, e) => SaveSettings();
            buttons.Controls.Add(cancel);
            buttons.Controls.Add(save);
            panel.Controls.Add(buttons, 0, 5);
            Controls.Add(panel);
            CancelButton = cancel;
            AcceptButton = save;
        }

        private void SaveSettings()
        {
            string name = repository.Text.Trim();
            if (!ReleasePackageValidator.ValidRepository(name))
            {
                MessageBox.Show(this, "Repository im Format Besitzer/Name eingeben.",
                    "Ungültige GitHub-Einstellungen", MessageBoxButtons.OK,
                    MessageBoxIcon.Warning);
                return;
            }
            try
            {
                string saved = Properties.Settings.Default.GitHubUpdateToken;
                if (clearToken.Checked) saved = "";
                else if (!string.IsNullOrWhiteSpace(token.Text))
                    saved = GitHubUpdateCredentialStore.Protect(token.Text);
                Properties.Settings.Default.GitHubUpdateRepository = name;
                Properties.Settings.Default.GitHubUpdateToken = saved;
                Properties.Settings.Default.Save();
                token.Clear();
                DialogResult = DialogResult.OK;
                Close();
            }
            catch (Exception)
            {
                MessageBox.Show(this,
                    "GitHub-Token konnte unter diesem Windows-Konto nicht sicher gespeichert werden.",
                    "Schutz der Anmeldedaten", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            }
        }
    }
}
