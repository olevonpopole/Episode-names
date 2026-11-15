using System;
using System.Drawing;
using System.IO;
using System.Windows.Forms;
using Episode_Names.Renaming;

namespace Episode_Names.View
{
    /// <summary>Read-only, explicit confirmation of all source-to-destination mappings.</summary>
    internal sealed class RenamePreviewDialog : Form
    {
        public RenamePreviewDialog(RenamePlan plan)
        {
            if (plan == null)
                throw new ArgumentNullException("plan");

            Text = "Umbenennungsvorschau – vor Dateiänderungen prüfen";
            StartPosition = FormStartPosition.CenterParent;
            MinimumSize = new Size(700, 360);
            Size = new Size(1000, 570);
            ShowInTaskbar = false;

            var summary = new Label
            {
                Dock = DockStyle.Top,
                Height = 63,
                Padding = new Padding(12, 10, 12, 0),
                AutoEllipsis = true,
                Text = string.Format("{0} Dateien, {1} Änderungen, {2} Konflikte.\r\n{3}",
                    plan.Entries.Count, plan.ChangeCount, plan.ErrorCount,
                    plan.ErrorCount > 0
                        ? "BLOCKIERT: Erst alle Konflikte beheben. Es wurden keine Dateien verändert."
                        : "Die Dateinamen prüfen. Nur nach ausdrücklicher Bestätigung wird umbenannt.")
            };

            var grid = new DataGridView
            {
                Dock = DockStyle.Fill,
                ReadOnly = true,
                AllowUserToAddRows = false,
                AllowUserToDeleteRows = false,
                AllowUserToResizeRows = false,
                AutoSizeRowsMode = DataGridViewAutoSizeRowsMode.AllCells,
                MultiSelect = false,
                SelectionMode = DataGridViewSelectionMode.FullRowSelect,
                RowHeadersVisible = false,
                AutoGenerateColumns = false
            };
            grid.Columns.Add(new DataGridViewTextBoxColumn
            {
                HeaderText = "Quelle",
                Width = 250,
                DataPropertyName = "Source"
            });
            grid.Columns.Add(new DataGridViewTextBoxColumn
            {
                HeaderText = "Ziel",
                Width = 250,
                DataPropertyName = "Destination"
            });
            grid.Columns.Add(new DataGridViewTextBoxColumn
            {
                HeaderText = "Prüfung",
                AutoSizeMode = DataGridViewAutoSizeColumnMode.Fill,
                MinimumWidth = 230,
                DefaultCellStyle = { WrapMode = DataGridViewTriState.True }
            });
            foreach (RenameEntry entry in plan.Entries)
            {
                string issue = !string.IsNullOrEmpty(entry.Problem) ? "FEHLER: " + entry.Problem
                    : entry.IsUnchanged ? "Unverändert" : "Bereit";
                int index = grid.Rows.Add(
                    entry.SourcePath == null ? "" : Path.GetFileName(entry.SourcePath),
                    entry.DestinationPath == null ? "" : Path.GetFileName(entry.DestinationPath),
                    issue);
                DataGridViewRow row = grid.Rows[index];
                row.Cells[0].ToolTipText = entry.SourcePath ?? "";
                row.Cells[1].ToolTipText = entry.DestinationPath ?? "";
                if (!string.IsNullOrEmpty(entry.Problem))
                    row.DefaultCellStyle.BackColor = Color.MistyRose;
            }

            var buttons = new FlowLayoutPanel
            {
                Dock = DockStyle.Bottom,
                Height = 51,
                FlowDirection = FlowDirection.RightToLeft,
                Padding = new Padding(8),
                WrapContents = false
            };
            var execute = new Button
            {
                Text = "Änderungen ausführen",
                Width = 165,
                Height = 28,
                Enabled = plan.CanExecute,
                DialogResult = DialogResult.OK
            };
            var cancel = new Button
            {
                Text = "Abbrechen",
                Width = 105,
                Height = 28,
                DialogResult = DialogResult.Cancel
            };
            buttons.Controls.Add(execute);
            buttons.Controls.Add(cancel);
            Controls.Add(grid);
            Controls.Add(buttons);
            Controls.Add(summary);
            AcceptButton = plan.CanExecute ? execute : cancel;
            CancelButton = cancel;
        }
    }
}
