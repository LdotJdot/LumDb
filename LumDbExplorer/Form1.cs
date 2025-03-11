using LumDbEngine.Element.Engine;
using LumDbExplorer.LumExplorer;

namespace LumDbExplorer
{
    public partial class Form1 : Form
    {
        public Form1()
        {
            InitializeComponent();
            treeView1.Nodes.Add(root);
        }

        DbExplorer de;
        DbEngine db;

        private void OpenToolStripMenuItem_Click(object sender, EventArgs e)
        {
            var op = new OpenFileDialog();
            op.Multiselect = false;

            if (op.ShowDialog() == DialogResult.OK && op.FileName != null)
            {
                db?.Dispose();
                db = null;
                db = new DbEngine(op.FileName);
                UpdateTable(op.FileName);
            }
            op.Dispose();
        }

        TreeNode root = new TreeNode("root");

        private void UpdateTable(string name)
        {
            if (db != null)
            {
                using var ts = db.StartTransaction();
                var tables = ts.GetTableNames();
                var tn = new TreeNode(name);
                foreach (var tb in tables.Values)
                {
                    tn.Nodes.Add(new TreeNode(tb.tableName) { Tag = tb });
                }
                root.Nodes.Add(tn);
            }
        }


        private void UpdateGrid((string tableName, (string columnName, string dataType, bool isKey)[] columns)? tableInfo, uint countPerPage, uint page)
        {
            if (tableInfo != null)
            {

                DataGridViewRow dgvr = new DataGridViewRow();

                dataGridView1.Columns.Clear();

                foreach (var val in tableInfo.Value.columns)
                {
                    var dgc = new DataGridViewColumn();
                    dgc.Name = val.columnName;
                    dgc.HeaderText = $"{val.columnName}({val.dataType},{val.isKey})";
                    dataGridView1.Columns.Add(dgc);
                    dgc.CellTemplate = new DataGridViewTextBoxCell();
                }

                using var values = db.StartTransaction();
                var res = values.Where(tableInfo.Value.tableName, false, (uint)countPerPage * (page), (uint)countPerPage);
                if (res.IsSuccess)
                {
                    foreach (var val in res.Values)
                    {
                        dataGridView1.Rows.Add(val);
                    }
                }

                dataGridView1.AutoResizeColumns(DataGridViewAutoSizeColumnsMode.ColumnHeader);
            }

        }

        private void CloseToolStripMenuItem_Click(object sender, EventArgs e)
        {
            de?.Dispose();
            de = null;
        }

        private void treeView1_NodeMouseClick(object sender, TreeNodeMouseClickEventArgs e)
        {
            if (e.Button == System.Windows.Forms.MouseButtons.Left)
            {
                currentPage = 0;

                if (e.Node.Level == 2)
                {
                    currentTableInfo = e.Node.Tag as (string tableName, (string columnName, string dataType, bool isKey)[] columns)?;
                    UpdateGrid(currentTableInfo, countPerPage, currentPage);
                }
            }
        }

        uint currentPage = 0;
        const uint countPerPage = 50;
        (string tableName, (string columnName, string dataType, bool isKey)[] columns)? currentTableInfo;   

        private void button2_Click_1(object sender, EventArgs e)
        {
            currentPage++;
            UpdateGrid(currentTableInfo, countPerPage, currentPage);
        }

        private void button1_Click_1(object sender, EventArgs e)
        {
            currentPage--;
            if (currentPage < 0) currentPage = 0;
            UpdateGrid(currentTableInfo, countPerPage, currentPage);
        }
    }
}
