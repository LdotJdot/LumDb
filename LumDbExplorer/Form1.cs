using LumDbEngine.Element.Engine;
using LumDbExplorer.LumExplorer;

namespace LumDbExplorer
{
    public partial class Form1 : Form
    {
        public Form1()
        {
            InitializeComponent();
        }

        DbExplorer de;
        DbEngine db;

        private void ��ToolStripMenuItem_Click(object sender, EventArgs e)
        {
            var op = new OpenFileDialog();
            op.Multiselect = false;

            if (op.ShowDialog() == DialogResult.OK && op.FileName != null)
            {
                db = new DbEngine(op.FileName);
                UpdateTable();
            }
            op.Dispose();
        }


        private void UpdateTable()
        {
           
        }


      

        private void �ر�ToolStripMenuItem_Click(object sender, EventArgs e)
        {
            de?.Dispose();
            de = null;
        }
    }
}
