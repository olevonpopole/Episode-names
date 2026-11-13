using Episode_Names.Anisearch;
using Episode_Names.Exceptions;
using Episode_Names.Helper;
using Episode_Names.Renaming;
using Episode_Names.View;
using System;
using System.Collections.Generic;
using System.Data;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace Episode_Names
{
    public partial class Form1 : Form
    {
        #region Variablen
        List<string> oldData;
        List<string> newData; //old- und newData wird für "Restore" benötigt; um die alten Filenamen wiederherzustelen muss man auch den neuen wissen
        List<string> data;
        bool abort;
        #endregion


        public Form1(string path = null)
        {
            InitializeComponent();
            setData();
            if(path != null)
            {
                txtPath.Text = path;
            }
        }

        #region Standard und gespeicherte Daten setzen
        private void setData()
        {
            txtPath.Text = Properties.Settings.Default.BrowseLocation;
            nbPosition.Value = Properties.Settings.Default.position;
            txtSplit.Text = Properties.Settings.Default.splitString;
            TopMost = Properties.Settings.Default.Foreground;
            txtSearch.Text = Properties.Settings.Default.SR_Search;
            pgBar.Value = cmbOption.SelectedIndex = 0;
            txtMessage.Text = "";
            abort = false;
            Updater.CheckUpdate(true, pgBar);
        }
        #endregion


        #region Auswahl zwischen vier Methoden (Rename, Insert/Delete Position, Replace) (Kommt drauf an was in der cmbBox angegeben und ob diese visible ist)
        private void Rename_Click(object sender, EventArgs e)
        {
            initNew();
            if (cmbOption.Visible && cmbOption.SelectedIndex == 0)
            {
                btnRename_Click();
            }
            else
            {
                renameWithoutData_Click(false);
            }
            abort = false;
        }
        #endregion


        #region Standardwerte für Message, Progressbar und alte/neue Daten setzen
        private void initNew()
        {
            txtMessage.Text = "";
            pgBar.Value = 0;
            // Preserve the last successful undo mapping if the user cancels a preview.
        }
        #endregion


        #region Metadata-basierte Umbenennung und sichere Vorschau
        private void btnRename_Click()
        {
            if (Properties.Settings.Default.OnlyNumber)
            {
                renameWithoutData_Click(true);
                return;
            }
            if (data == null)
            {
                MessageHandler.MessagesOK(MessageBoxIcon.Exclamation, "Es wurden keine Daten gesetzt.");
                return;
            }

            try
            {
                List<FileInfo> files = GetVisibleFiles();
                if (files.Count != data.Count)
                {
                    MessageHandler.MessagesOK(MessageBoxIcon.Warning,
                        "Abbruch vor der Vorschau: " + files.Count + " Dateien und " +
                        data.Count + " Datenzeilen passen nicht zusammen.\n" +
                        "Bitte fehlende/überzählige Zeilen zuerst im Dateneditor korrigieren.");
                    return;
                }

                pgBar.Maximum = Math.Max(1, files.Count);
                var requests = new List<RenameRequest>();
                for (int i = 0; i < files.Count; i++)
                {
                    pgBar.Value = i;
                    FileInfo file = files[i];
                    string name = FormatString(data[i], file.Directory.Name) + file.Extension;
                    requests.Add(new RenameRequest(file.FullName, Path.Combine(file.DirectoryName, name)));
                }

                PreviewAndExecute(requests, false);
            }
            catch (Exception ex)
            {
                MessageHandler.MessagesOK(MessageBoxIcon.Error,
                    "Umbenennung nicht geplant; keine Datei wurde geändert.\n" + ex.Message);
            }
            finally
            {
                pgBar.Value = 0;
            }
        }

        private List<FileInfo> GetVisibleFiles()
        {
            var directory = new DirectoryInfo(txtPath.Text);
            List<FileInfo> files = directory.GetFiles()
                .Where(file => (file.Attributes & FileAttributes.Hidden) == 0).ToList();
            files.Sort(new StringComparator());
            return files;
        }

        /// <summary>
        /// Every rename mode (including restore) passes through this single
        /// validation, visual confirmation and revalidated execution path.
        /// </summary>
        private bool PreviewAndExecute(IEnumerable<RenameRequest> requests, bool restoring)
        {
            RenamePlan plan = RenamePlan.Create(requests);
            using (var preview = new RenamePreviewDialog(plan))
            {
                if (preview.ShowDialog(this) != DialogResult.OK)
                {
                    txtMessage.Text = plan.ErrorCount > 0
                        ? "Umbenennung blockiert: " + plan.ErrorCount + " Konflikte."
                        : "Vorschau abgebrochen – keine Dateien geändert.";
                    return false;
                }
            }

            try
            {
                IReadOnlyList<RenameRequest> moves = plan.Execute();
                if (restoring)
                {
                    oldData = null;
                    newData = null;
                    txtMessage.Text = "Wiederhergestellt: " + moves.Count + " Dateien.";
                }
                else
                {
                    oldData = moves.Select(move => move.SourcePath).ToList();
                    newData = moves.Select(move => move.DestinationPath).ToList();
                    txtMessage.Text = "Erfolgreich umbenannt: " + moves.Count + " Dateien.";
                }
                return true;
            }
            catch (IOException ex)
            {
                txtMessage.Text = "Abgebrochen – Details beachten.";
                MessageHandler.MessagesOK(MessageBoxIcon.Error, ex.Message);
                return false;
            }
        }
        #endregion


        #region Format-String wird angepasst
        private string FormatString(string row, string dirName)
        {
            IEnumerable<string> text = Properties.Settings.Default.formatString.Split(new string[] { SettingHelper.Separator.ToString() }, StringSplitOptions.RemoveEmptyEntries);

            List<string> formates = SettingHelper.Formates;
            List<string> formatesWithoutNumeration = SettingHelper.FormatesWithoutNumeration;

            StringBuilder fullname = new StringBuilder();
            if (!Properties.Settings.Default.formatString.StartsWith(SettingHelper.Separator.ToString()))
            {
                fullname.Append(text.FirstOrDefault() ?? string.Empty);
                text = text.Skip(1);
            }

            foreach (string line in text)
            {
                string format = formates.Where(f => line.Length >= f.Length && line.Substring(0, f.Length) == f).OrderBy(f=>f,new StringComparator()).LastOrDefault();
                if(format == null)
                {
                    for(int i = 1; i <= SettingHelper.MaxNumber.ToString().Length; i++)
                    {
                        if(line.Length >= i && int.TryParse(line.Substring(0,i), out int result) && result <= SettingHelper.MaxNumber)
                        {
                            format = result.ToString();
                        }
                    }
                }

                if (format != null)
                {
                    try
                    {
                        #region Überprüfen um was für einen Format-String Befehl es sich handelt
                        if (format == SettingHelper.Number)
                        {
                            int temp = pgBar.Value + Properties.Settings.Default.startNumber;
                            string number = nullvalues(pgBar.Maximum - 1 + Properties.Settings.Default.startNumber, temp) + temp;
                            fullname.Append(number);
                        }
                        else if (format == SettingHelper.Position)
                        {
                            char a = txtSplit.Text == "\\t" ? '\t' : '\n';
                            fullname.Append(SplitLine(row, a, (int)nbPosition.Value));
                        }
                        else if (format == SettingHelper.Directory)
                        {
                            fullname.Append(dirName);
                        }
                        else //Nummer 1-100
                        {
                            char a = txtSplit.Text == "\\t" ? '\t' : '\n';
                            fullname.Append(SplitLine(row, a, Convert.ToInt32(format)));
                        }

                        fullname.Append(line.Substring(format.Length, line.Length - format.Length)); //Rest der nach dem %-Befehl steht. Bsp: %nabc also "abc"
                        #endregion
                    }
                    catch (IndexOutOfRangeException ex)
                    {
                        throw new InvalidOperationException(
                            "Eine Datenzeile enthält nicht die angeforderte Spalte " +
                            nbPosition.Value + " (Trennzeichen: " + txtSplit.Text + ").", ex);
                    }
                }
                else
                {
                    fullname.Append(SettingHelper.Separator).Append(line);
                }
                
                    
            }
            return ReplaceSpecialCharacters(fullname.ToString());
        }
        #endregion

        

        #region Format-String anpassen (mit nur %n)
        private string FormatString()
        {
            string[] text = Properties.Settings.Default.formatString.Split(new string[] { "%" }, StringSplitOptions.RemoveEmptyEntries);

            string fullname = "";

            foreach (string line in text)
            {
                if (line.Substring(0, 1).Equals("n"))
                {
                    int temp = pgBar.Value + Properties.Settings.Default.startNumber;
                    string number = nullvalues(pgBar.Maximum - 1 + Properties.Settings.Default.startNumber, temp) + temp;
                    fullname += number;
                    fullname += line.Substring(1, line.Length - 1);
                }
                else
                    fullname += ((!text[0].Equals(line)) ? ("%" + line) : line);
               

            }
            return fullname;
        }
        #endregion


        #region Textline wird nach Split-String gesplittet, Position wird rausgeholt und Sonderzeichen werden ersetzt
        private string SplitLine(string line, char a, int position)
        {
            string erg = (a.Equals('\n')) ?
                            line.Split(txtSplit.Text.Split(
                                    new string[] { "|" }, StringSplitOptions.None), StringSplitOptions.RemoveEmptyEntries
                                    )[position - 1] :
                            line.Split(new char[] { a }, StringSplitOptions.RemoveEmptyEntries)[position - 1];
            
            return ReplaceSpecialCharacters(erg);
        }
        #endregion


        #region Sonderzeichen werden ersetzt
        private static string ReplaceSpecialCharacters(string erg)
        {
            return erg.Replace("?",   "？")
                      .Replace(" : ", "：")
                      .Replace(":",   "：")
                      .Replace("/",   " ∕ ")
                      .Replace("\\",  "＼")
                      .Replace("\"",  "''")
                      .Replace(">",   "＞")
                      .Replace("<",   "＜")
                      .Replace("*",   "＊");
        }
        #endregion


        #region Button-Restore Klick
        private void btnRestore_Click(object sender, EventArgs e)
        {
            txtMessage.Text = "";
            restore();
        }
        #endregion


        #region Wiederherstellen mit Prüfung aller Zielnamen
        private void restore()
        {
            if (oldData == null || newData == null || oldData.Count == 0 ||
                oldData.Count != newData.Count)
            {
                MessageHandler.MessagesOK(MessageBoxIcon.Exclamation,
                    "Es sind keine umbenannten Dateien zum Wiederherstellen vorhanden.");
                return;
            }

            var requests = new List<RenameRequest>();
            for (int i = oldData.Count - 1; i >= 0; i--)
                requests.Add(new RenameRequest(newData[i], oldData[i]));

            PreviewAndExecute(requests, true);
        }
        #endregion


        #region button-Number Klick
        private void bntNumber_Click(object sender, EventArgs e)
        {
            initNew();
            renameWithoutData_Click(null);
        }
        #endregion


        #region Zurückgeben der 0er, welche vor einer Zahl aufgrund der Anzahl der Dateien fehlen
        private string nullvalues(int max, int current)
        {
            int erg = max.ToString().Length - current.ToString().Length;

            return new String('0', erg);
        }
        #endregion


        #region Button-Browse Klick
        private void btnBrowseFolder_Click(object sender, EventArgs e)
        {
            FileFolderDialog dialog = new FileFolderDialog();
            dialog.ShowDialog(txtPath.Text);
            if (!dialog.SelectedPath().Equals(""))
                txtPath.Text = dialog.SelectedPath();
        }
        #endregion


        #region Button-New_Data Klick. Aufrufen der Form und anschließendes Holen der Daten
        private void insertDataToolStripMenuItem_Click(object sender, EventArgs e)
        {
            Data_Insert form = new Data_Insert();
            DialogResult result = form.ShowDialog();
            if (result == DialogResult.OK)
                data = string.IsNullOrWhiteSpace(form.getData()) ? null : form.getData().Split('\n').ToList();

        }
        #endregion


        #region ButtonEdit_Data Klick. Aufrufen der Form mit den gespeicherten Daten
        private void editDataToolStripMenuItem_Click(object sender, EventArgs e)
        {
            if (data != null)
            {
                startData_Insert(data);
            }
            else
                MessageHandler.MessagesOK(MessageBoxIcon.Error, "Es sind keine Daten vorhanden, welche bearbeitet werden können");
        }
        #endregion


        #region Starten von Data_Insert mit geholten Daten
        private void startData_Insert(IEnumerable<string> data)
        {
            Data_Insert form = new Data_Insert(data);
            DialogResult result = form.ShowDialog();
            if (result == DialogResult.OK)
            {
                this.data = form.getData().Split('\n').ToList();
            }

        }
        #endregion


        #region FileNamen des angegebenen Ordners holen und falls erfolgreich "Data_Insert" aufrufen
        private void getFileNamesToolStripMenuItem_Click(object sender, EventArgs e)
        {
            try
            {
                startData_Insert(FileHelper.FileNamesOfFolder(txtPath.Text));
            }

            catch (Exception e1)
            {
                ErrorHelper.HandleException(e1);
            }
        }
        #endregion


        #region Speichern der Daten beim Beenden
        private void Form1_FormClosing(object sender, FormClosingEventArgs e)
        {
            Properties.Settings.Default.position = nbPosition.Value;
            Properties.Settings.Default.BrowseLocation = txtPath.Text;
            Properties.Settings.Default.SR_Search = txtSearch.Text;
            if (!cmbOption.Visible)
            {
                Properties.Settings.Default.SR_Replace = txtSplit.Text;
            }
            else
            {
                if(cmbOption.SelectedIndex == 0)
                    Properties.Settings.Default.splitString = txtSplit.Text;
            }

            Properties.Settings.Default.Save();
        }
        #endregion



        #region Anisearch Menüpunkt
        private void anisearchToolStripMenuItem_Click(object sender, EventArgs e)
        {
            Anisearch_Table form = new Anisearch_Table();
            DialogResult result = form.ShowDialog();
            if (result == DialogResult.OK){
                startData_Insert(form.getData());
            }
        }
        #endregion


        #region Einstellungen
        private void settingsToolStripMenuItem_Click(object sender, EventArgs e)
        {
            Form_Settings form = new Form_Settings(this);
            DialogResult result = form.ShowDialog();
            switch (result)
            {
                #region Factory-Settings

                case DialogResult.Retry:
                    setData();
                    break;

                #endregion

                #region Foreground-Setting

                case DialogResult.Yes:
                    TopMost = Properties.Settings.Default.Foreground;
                    break;

                #endregion

            }
        }

        #endregion


        #region Search&Replace Klick
        private void searchReplaceToolStripMenuItem_Click(object sender, EventArgs e)
        {
            switch (searchReplaceToolStripMenuItem.Text)
            {
                case "Umbenennen":
                    setVisibility(false);
                    break;

                default:
                    setVisibility(true);
                    break;
            }
        }
        #endregion


        #region Visibilität von Controls setzen (Trigger ist Klick auf Search&Replace
        private void setVisibility(bool v){
            int x = 477;
            string text = "Suchen && Ersetzen";

            txtSearch.Visible = v;
            lblReplace.Visible = v;
            lblSearch.Visible = v;
            cmbOption.Visible = !v;
            
            
            //falls auf true gesetzt wird
            if (cmbOption.Visible)
            {
                cmbOption_SelectedIndexChanged();
            }
            else
            {
                nbPosition.Visible = !v;
                lblPosition.Visible = !v;
                x = 529;
                text = "Umbenennen";
                if(cmbOption.SelectedIndex == 0)
                    Properties.Settings.Default.splitString = txtSplit.Text;
                string splitText = Properties.Settings.Default.SR_Replace;
                txtSplit.Text = splitText;
                txtSplit.Width = 160;

            }
            txtSplit.Location = new System.Drawing.Point(x, txtSplit.Location.Y);
            searchReplaceToolStripMenuItem.Text = text;
        }
        #endregion


        #region Nummerierung, Positionen und Suchen/Ersetzen mit verpflichtender Vorschau
        private void renameWithoutData_Click(bool? numberClicked)
        {
            try
            {
                List<FileInfo> files = GetVisibleFiles();
                pgBar.Maximum = Math.Max(1, files.Count);
                var requests = new List<RenameRequest>();

                for (int index = 0; index < files.Count; index++)
                {
                    FileInfo file = files[index];
                    pgBar.Value = index;
                    string filename = Path.GetFileNameWithoutExtension(file.Name);
                    string newName;

                    if (numberClicked == null)
                    {
                        int number = index + Properties.Settings.Default.startNumber;
                        newName = nullvalues(files.Count - 1 + Properties.Settings.Default.startNumber, number) + number;
                    }
                    else if (numberClicked == true)
                    {
                        newName = ReplaceSpecialCharacters(FormatString());
                    }
                    else if (cmbOption.Visible)
                    {
                        if (cmbOption.SelectedIndex == 1)
                            newName = DeletePositions(filename);
                        else if (cmbOption.SelectedIndex == 2)
                            newName = InsertPosition(filename);
                        else
                            throw new InvalidOperationException("Ungültige Umbenennungsoption.");
                    }
                    else
                    {
                        if (string.IsNullOrEmpty(txtSearch.Text))
                            throw new InvalidOperationException("Bitte einen Suchtext eingeben.");
                        newName = filename.Replace(ReplaceSpecialCharacters(txtSearch.Text),
                            ReplaceSpecialCharacters(txtSplit.Text));
                    }

                    requests.Add(new RenameRequest(file.FullName,
                        Path.Combine(file.DirectoryName, newName + file.Extension)));
                }

                PreviewAndExecute(requests, false);
            }
            catch (Exception ex)
            {
                MessageHandler.MessagesOK(MessageBoxIcon.Error,
                    "Vorschau konnte nicht erstellt werden – keine Datei wurde geändert.\n" + ex.Message);
            }
            finally
            {
                pgBar.Value = 0;
            }
        }
        #endregion


        #region Position an angegebener Stelle einfügen
        private string InsertPosition(string filename)
        {
            filename = filename.Insert((int)(nbPosition.Value - 1), ReplaceSpecialCharacters(txtSplit.Text));

            return filename;
        }
        #endregion


        #region Angegebene Positionen löschen
        private string DeletePositions(string filename)
        {
            HashSet<int> positions = getPositions();
            var builder = new System.Text.StringBuilder(filename);
            
            foreach (int position in positions.OrderByDescending(o => o))
            {
                try
                {
                    builder.Remove((position - 1), 1); //Position != Index
                }
                catch (ArgumentOutOfRangeException ex)
                {
                    throw new InvalidOperationException(
                        "Position " + position + " ist für die Datei " + filename +
                        " nicht vorhanden. Bitte die Positionen korrigieren.", ex);
                }
            }

            return builder.ToString();
        }
        #endregion


        #region Zu löschende Positionen holen
        private HashSet<int> getPositions()
        {
            HashSet<int> positions = new HashSet<int>(); //HashSet wegen Sortierung und keine Duplikate
            List<string> stpositions = txtSplit.Text.Split(new string[] { ";" }, StringSplitOptions.RemoveEmptyEntries).ToList();


            foreach (string position in stpositions)
            {
                List<string> arrpos = position.Split(new string[] { "-" }, StringSplitOptions.RemoveEmptyEntries).ToList();

                int start = Convert.ToInt32(arrpos[0].Trim());
                if (arrpos.Count == 1)
                {
                    positions.Add(start);
                }
                else
                {
                    int secondNumber = Convert.ToInt32(arrpos[1].Trim());
                    int count = secondNumber - start;

                    if (arrpos.Count == 2 && secondNumber >= start)
                    {
                        foreach(int pos in Enumerable.Range(start, count + 1))
                            positions.Add(pos);
                    }

                    else
                    {
                        throw new InvalidOperationException();
                    }
                }
            }
            
            return positions;
        }
        #endregion


        #region Ändern des Tooltips durch Ändernung der Option auf "Delete Position", "Insert Position" und "Rename" oder "Replace"
        private void cmbOption_SelectedIndexChanged(object sender, EventArgs e)
        {
            cmbOption_SelectedIndexChanged();
        }
        private void cmbOption_SelectedIndexChanged()
        {
            bool visibility = true;
            int width = 113;
            string tooltipText = "Gibt an wonach jede Zeile in \"Data\" aufgeteilt werden soll\nBefehle werden mit | getrennt";
            string splitText = "";

            switch (cmbOption.SelectedIndex)
            {
                case 0:
                    splitText = Properties.Settings.Default.splitString;
                    break;

                case 1:
                    visibility = false;
                    width = 212;
                    tooltipText = cmbOption.Visible ? "Positionen werden mit \";\" getrennt\nEs kann von-bis angegeben werden\nBeispiel: \"1;5;7 - 10\" " : "";
                    break;

            }

            nbPosition.Visible = visibility;
            lblPosition.Visible = visibility;
            txtSplit.Text = splitText;
            txtSplit.Width = width;
            toolTip1.SetToolTip(txtSplit, tooltipText);
        }
        #endregion


        #region Split-String speichern, wenn Fokus verlässt
        private void txtSplit_Leave(object sender, EventArgs e)
        {
            if (cmbOption.Visible && cmbOption.SelectedIndex == 0)
                Properties.Settings.Default.splitString = txtSplit.Text;
        }
        #endregion

        private void updateToolStripMenuItem_Click(object sender, EventArgs e)
        {
            Updater.CheckUpdate(false, pgBar);
        }

        private void infoToolStripMenuItem_Click(object sender, EventArgs e)
        {
            Process.Start("https://github.com/Kirdock/Episode-names/releases");
        }

        private void ordnernamenHolenToolStripMenuItem_Click(object sender, EventArgs e)
        {
            try
            {
                startData_Insert(FileHelper.FolderNamesOfFolder(txtPath.Text));
            }

            catch (Exception e1)
            {
                ErrorHelper.HandleException(e1);
            }
        }

        private void specialCharactersToolStripMenuItem_Click(object sender, EventArgs e)
        {
            using(var form = new SpecialCharacters())
            {
                form.ShowDialog();
            }
        }
    }
}
