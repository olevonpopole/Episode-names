using Episode_Names.Helper;
using System;
using System.Collections.Generic;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Windows.Forms;

namespace Episode_Names
{
    public partial class Form_Settings : Form
    {
        #region Variablen
        private bool valueChanged = false;
        private readonly Form1 mainForm; // wird gebraucht um Topmost zu setzen
        #endregion

        public Form_Settings(Form1 form)
        {
            InitializeComponent();
            mainForm = form;
            setData();
            // Display the new token beside the existing format reference
            // without changing the legacy Windows Forms designer layout.
            var seriesHelp = new System.Windows.Forms.Label {
                AutoSize = true, Location = new System.Drawing.Point(11, 198),
                Text = "Serientitel:       %series"
            };
            txtFormat.Parent.Controls.Add(seriesHelp);
            var seasonHelp = new System.Windows.Forms.Label {
                AutoSize = true, Location = new System.Drawing.Point(11, 218),
                Text = "Staffelnummer:  %season  (z. B. 02)"
            };
            txtFormat.Parent.Controls.Add(seasonHelp);
        }

        #region Vordergrund (TopMost) in MainForm setzen, wenn geändert
        private void setTopMost()
        {
            mainForm.TopMost = Properties.Settings.Default.Foreground;
        }
        #endregion


        #region Standardwerte und gespeicherte Daten setzen
        private void setData()
        {
            nbNumber.Value = Properties.Settings.Default.startNumber;
            chbForeground.Checked = Properties.Settings.Default.Foreground;
            TopMost = Properties.Settings.Default.Foreground;
            txtFormat.Text = Properties.Settings.Default.formatString;
            btnSave.Enabled = valueChanged = false;
            string text =
                "Version: 1.0.0.0 - 1.0.0.2" +
                "\n\t•    Sprachen beim Punkt \"Anisearch\" wurden ergänzt (Türkisch, Spanisch, etc.)." +
                "\n\t•    Unter \"Anisearch\" ist jetzt ein Menüpunkt \"Search\". Hier kann man direkt nach Animes suchen. Diese werden dann darunter angezeigt. Es kann auch eine bevorzugte Sprache angegeben werden - Wenn man z.B. \"Deutsch\" auswählt, werden die deutschen Namen angezeigt (falls vorhanden) -." +
                "\n\t•    Die Auswahl erfolgt durch Doppelklick." +
                "\n\t•    Per Rechtsklick öffnet sich ein Context-Menü mit dem man ein Ergebnis mit dem Browser öffnen kann (Je nach gewählter Sprache, wird man zu \"de.anisearch\", \"en.anisearch\", etc. weitergeleitet)." +
                "\n\t•    Darstellung der geholten Episodenliste von Anisearch geändert. Jetzt wird nur noch die Folgennummer und der Titel angezeigt" +
                "\n\t•    Stellt nun die Position auf \"2\", wenn ihr was von Anisearch holt" +
                "\n\t•    oder geht auf \"Factory Settings\" um die Werkeinstellungen wieder herzustellen" +
                "\n" +
                "\n" +
                "\nVersion 1.1" +
                "\n\t•    \"Search & Replace\"-Funktion: Sucht im angegebenen Pfad nach einem Text und ersetzt ihn durch einen anderen (z.B. Alle Punkte der Dateinamen im angegebenen Pfad werden durch Leerzeichen ersetzt \"Das.ist.mein.Dateiname.mkv\" zu \"Das ist mein Dateiname.mkv\")" +
                "\n\t•    Funktion \"Immer im Vordergrund\"" +
                "\n\t•    Einstellungen wurden zusammengefasst" +
                "\n\t•    Bug-Fixes" +
                "\n" +
                "\n" +
                "\nVersion 1.2" +
                "\n\t•    Wurde an die neue Anisearch-Website angepasst" +
                "\n" +
                "\n" +
                "\nVersion 1.3" +
                "\n\t•    Wurde an die neue Anisearch-Website angepasst" +
                "\n\t•    Neue Funktion: \"Insert Position\" und \"Delete Positions\"" +
                "\n\t•    \"Insert Position\" fügt bei der angegebenen Position einen Text hinein" +
                "\n\t•    \"Delete Positions\" löscht die Zeichen auf den angegebenen Positionen (die Positionen tragt man einfach daneben in das Textfeld). Positionen werden mit \";\" getrennt und es kann auch von-bis angegeben werden. Also z.B. \"1;5;7-10\", dann werden die Zeichen auf den Positionen 1,5,7,8,9,10 gelöscht." +
                "\n" +
                "\n" +
                "\nVersion 1.3.1" +
                "\n\t•    Es wurde ein Fehler behoben, bei dem keine englischen Episodennamen gefunden werden konnten" +
                "\n" +
                "\n" +
                "\nVersion 1.3.2" +
                "\n\t•    Es wurde ein Fehler bei der Anisearch-Suche behoben, bei der die Suche mit anderen Sprachen nicht funktionierte" +
                "\n" +
                "\n" +
                "\nVersion 1.4.0" +
                "\n\t•    Programm wird jetzt mit einem Installer installiert" +
                "\n\t•    Diverse Bug-fixes" +
                "\n\t•    Optimierung des Codes (heißt soviel wie weniger Redundanz, heißt soviel wie die Datei ist kleiner (.exe von 1,98MB auf 1,6MB)" +
                "\n\t•    Search & Replace ist jetzt in der gleichen Form" +
                "\n\t•    Anisearch-Suche mit anderen Sprachen sollte funktionieren (Warum haben die verschiedene Seitenlayouts bei Sprachen die nicht englisch oder deutsch sind???)" +
                "\n\t•    Tooltips werden schneller angezeigt" +
                "\n" +
                "\n" +
                "\nVersion 1.4.1" +
                "\n\t•    Changelog hinzugefügt" +
                "\n\t•    An Anisearch-Website angepasst" +
                "\n" +
                "\n" +
                "\nVersion 1.4.2" +
                "\n\t•    Bugfix: Links von Anisearch mit \"https\" haben nicht funktioniert" +
                "\n" +
                "\n" +
                "\nVersion 1.4.3" +
                "\n\t•    Die Anisearch-Suche hat nicht funktioniert, wenn sie genau ein Ergebnis ergab (Anisearch leitet dann nämlich gleich zur Seite des Animes weiter)" +
                "\nVersion 1.4.4" +
                "\n\t•    Die Anisearch-Suche hat nicht funktioniert, wenn sie genau ein Ergebnis ergab (Anisearch leitet dann nämlich gleich zur Seite des Animes weiter)" +
                "\n" +
                "\n" +
                "\nVersion 1.4.5" +
                "\n\t•    Die Anisearch-Suche hat nicht mehr funktioniert" +
                "\n" +
                "\n" +
                "\nVersion 1.4.6" +
                "\n\t•    Anpassung an neue Anisearch-Website" +
                "\n\t•    \"Get - Filenames\" hatte eine falsche Sortierung (Bsp: \"1\", \"10\", \"2\")" +
                "\n" +
                "\n" +
                "\nVersion 1.4.7" +
                "\n\t•    Anisearch-Suche zusammengefasst (vorher waren zwei verschiedene Fenster; Suche und URL)" +
                "\n\t•    TVDB als neue Seite hinzugefügt" +
                "\n\t•    Menüpunkte sind nun deutsch";

#if DEBUG
            rtbChangelog.ReadOnly = false;
            text = string.IsNullOrWhiteSpace(Properties.Settings.Default.Changelog) ? text : Properties.Settings.Default.Changelog;
#endif
            rtbChangelog.Text = text;


            string[] textBoxLines = rtbChangelog.Lines;
            for (int i = 0; i < textBoxLines.Length; i++)
            {
                string line = textBoxLines[i];
                if (line.Contains("Version"))
                {
                    rtbChangelog.SelectionStart = rtbChangelog.GetFirstCharIndexFromLine(i);
                    rtbChangelog.SelectionLength = line.Length;
                    rtbChangelog.SelectionFont = new Font("Arial", (float)12.0, FontStyle.Bold, GraphicsUnit.Pixel);
                }
                else if (line.StartsWith("\t•"))
                {
                    rtbChangelog.SelectionStart = rtbChangelog.GetFirstCharIndexFromLine(i);
                    rtbChangelog.SelectionLength = 3;
                    rtbChangelog.SelectionFont = new Font("Arial", (float)12.0, FontStyle.Bold, GraphicsUnit.Pixel);
                }
            }
            rtbChangelog.SelectionLength = 0;

        }
        #endregion


        #region TabControll anpassen
        private void tabControl1_DrawItem(object sender, DrawItemEventArgs e)
        {
            Graphics g = e.Graphics;
            Brush _textBrush;

            // Get the item from the collection.
            TabPage _tabPage = tabControl1.TabPages[e.Index];

            // Get the real bounds for the tab rectangle.
            Rectangle _tabBounds = tabControl1.GetTabRect(e.Index);

            if (e.State == DrawItemState.Selected)
            {

                // Draw a different background color, and don't paint a focus rectangle.
                _textBrush = new SolidBrush(Color.Black);
                g.FillRectangle(Brushes.Gray, e.Bounds);
            }
            else
            {
                _textBrush = new SolidBrush(e.ForeColor);
                e.DrawBackground();
            }

            // Use our own font.
            Font _tabFont = new Font("Arial", (float)10.0, FontStyle.Bold, GraphicsUnit.Pixel);

            // Draw string. Center the text.
            StringFormat _stringFlags = new StringFormat();
            _stringFlags.Alignment = StringAlignment.Center;
            _stringFlags.LineAlignment = StringAlignment.Center;
            g.DrawString(_tabPage.Text, _tabFont, _textBrush, _tabBounds, new StringFormat(_stringFlags));
        }
#endregion


#region Button Save Click
        private void btnSave_Click(object sender, EventArgs e)
        {
            if (saveData())
            {
                MessageHandler.MessagesOK(MessageBoxIcon.Information, "Gespeichert");
                setTopMost();
            }
        }
#endregion


#region Speichern der EInstellungen
        private bool saveData()
        {
            bool success = false;
            if (FormatStringValid())
            {

                btnSave.Enabled = valueChanged = false;

                Properties.Settings.Default.startNumber = (int) nbNumber.Value;
                Properties.Settings.Default.Foreground = TopMost = chbForeground.Checked;
                Properties.Settings.Default.formatString = txtFormat.Text;
#if DEBUG
                Properties.Settings.Default.Changelog = rtbChangelog.Text;
#endif
                Properties.Settings.Default.Save();
                success = true;
            }
            else
                MessageHandler.MessagesOK(MessageBoxIcon.Error, "Ungültiger Format-String!");

            return success;

        }
#endregion


#region Überprüfen ob der FormatString gültig ist
        private bool FormatStringValid()
        {
            IEnumerable<string> text = txtFormat.Text.Split(new string[] { SettingHelper.Separator.ToString() }, StringSplitOptions.RemoveEmptyEntries);
            text = txtFormat.Text.StartsWith(SettingHelper.Separator.ToString()) ? text : text.Skip(1);

            List<string> formates = SettingHelper.Formates;

            bool withoutNumber = text.Any(item => formates.Where(format => format != SettingHelper.Number).Any(format => item.Length >= format.Length && item.Substring(0, format.Length) == format));
            bool onlyNumber = text.Any(item => item.Length >= SettingHelper.Number.Length && item.Substring(0, SettingHelper.Number.Length) == SettingHelper.Number);
            

            Properties.Settings.Default.OnlyNumber = onlyNumber && !withoutNumber;
            Properties.Settings.Default.Save();
            return withoutNumber || onlyNumber;
        }
#endregion


#region Falls was geändert Save-Button auf enabled stellen
        private void ValueChanged(object sender, EventArgs e)
        {
            btnSave.Enabled = valueChanged = true;
        }
#endregion


#region Wenn das Fenster geschlossen wird, Einstellungen speichern?
        private void Form_Settings_FormClosing(object sender, FormClosingEventArgs e)
        {
            if (valueChanged)
            {
                DialogResult? result = MessageHandler.MessagesYesNo(MessageBoxIcon.Warning, "Die Einstellungen wurden nicht gespeichert!\n Jetzt speichern?");
                if (result == DialogResult.Yes)
                {
                    if (!saveData())
                        e.Cancel = true;
                    else
                    {
                        valueChanged = false;
                        DialogResult = DialogResult.Yes;
                    }
                }
            }
        }
#endregion


#region Einstellungen speichern und Form schließen
        private void btnOK_Click(object sender, EventArgs e)
        {
            if (saveData())
            {
                DialogResult = DialogResult.Yes;
                Close();
            }
        }
#endregion


#region Standardwerte wiederherstellen
        private void btnFacSet_Click(object sender, EventArgs e)
        {
            DialogResult? result = MessageHandler.MessagesYesNo(MessageBoxIcon.Exclamation, "Sind Sie sicher, dass Sie die Einstellungen zurücksetzen wollen?");
            if (result == DialogResult.Yes)
            {
                Properties.Settings.Default.Reset();
                setData();
                DialogResult = DialogResult.Retry;
            }
        }
#endregion
    }
}
