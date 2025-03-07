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

        private void 打开ToolStripMenuItem_Click(object sender, EventArgs e)
        {
            var op = new OpenFileDialog();
            op.Multiselect = false;

            if (op.ShowDialog() == DialogResult.OK && op.FileName != null)
            {
                de = new DbExplorer(op.FileName);
                Update();
            }
            op.Dispose();
        }


        private void Update()
        {

        }

        private void textBox1_TextChanged(object sender, EventArgs e)
        {

        }

        private void textBox1_KeyPress(object sender, KeyPressEventArgs e)
        {
            if (e.KeyChar == 13)
            {
                if (uint.TryParse(textBox1.Text, out var pageId))
                {
                    textBox2.Text = de?.GetPage(pageId)??"未打开";
                }
                else
                {
                    MessageBox.Show("无效");
                }
            }
        }

        private void 关闭ToolStripMenuItem_Click(object sender, EventArgs e)
        {
            de?.Dispose();
            de = null;
        }
    }
}
