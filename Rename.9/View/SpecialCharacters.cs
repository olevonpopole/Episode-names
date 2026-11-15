using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;
using Episode_Names.Helper;

namespace Episode_Names.View
{
    public partial class SpecialCharacters : Form
    {
        public SpecialCharacters()
        {
            InitializeComponent();
        }

        private void btnQuestionMark_Click(object sender, EventArgs e)
        {
            Clipboard.SetText("？");
        }

        private void btnColon_Click(object sender, EventArgs e)
        {
            Clipboard.SetText(FilenameCharacters.Colon);
        }

        private void btnSlash_Click(object sender, EventArgs e)
        {
            Clipboard.SetText(" ∕ ");
        }

        private void btnBackslash_Click(object sender, EventArgs e)
        {
            Clipboard.SetText("＼");
        }

        private void btnGreater_Click(object sender, EventArgs e)
        {
            Clipboard.SetText("＞");
        }

        private void btnSmaller_Click(object sender, EventArgs e)
        {
            Clipboard.SetText("＜");
        }

        private void btnAsterisk_Click(object sender, EventArgs e)
        {
            Clipboard.SetText("＊");
        }
    }
}
