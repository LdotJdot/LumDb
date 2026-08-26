namespace LumDbExplorer
{
    partial class Form1
    {
        private System.ComponentModel.IContainer components = null;

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                CloseDatabase();
                components?.Dispose();
            }
            base.Dispose(disposing);
        }

        #region Windows Form Designer generated code

        private void InitializeComponent()
        {
            components = new System.ComponentModel.Container();
            menuStrip = new MenuStrip();
            mnuFile = new ToolStripMenuItem();
            mnuNew = new ToolStripMenuItem();
            mnuOpen = new ToolStripMenuItem();
            mnuSaveCopy = new ToolStripMenuItem();
            mnuClose = new ToolStripMenuItem();
            mnuExit = new ToolStripMenuItem();
            mnuTable = new ToolStripMenuItem();
            mnuCreateTable = new ToolStripMenuItem();
            mnuDropTable = new ToolStripMenuItem();
            mnuTableProps = new ToolStripMenuItem();
            mnuRefresh = new ToolStripMenuItem();
            mnuData = new ToolStripMenuItem();
            mnuInsert = new ToolStripMenuItem();
            mnuEdit = new ToolStripMenuItem();
            mnuDelete = new ToolStripMenuItem();
            mnuHelp = new ToolStripMenuItem();
            mnuAbout = new ToolStripMenuItem();
            toolStrip = new ToolStrip();
            btnNew = new ToolStripButton();
            btnOpen = new ToolStripButton();
            btnSaveCopy = new ToolStripButton();
            btnCreateTable = new ToolStripButton();
            btnInsert = new ToolStripButton();
            btnEdit = new ToolStripButton();
            btnDelete = new ToolStripButton();
            btnRefresh = new ToolStripButton();
            statusStrip = new StatusStrip();
            slPath = new ToolStripStatusLabel();
            slVersion = new ToolStripStatusLabel();
            slRows = new ToolStripStatusLabel();
            slPage = new ToolStripStatusLabel();
            split = new SplitContainer();
            tree = new TreeView();
            treeMenu = new ContextMenuStrip(components);
            ctxCreateTable = new ToolStripMenuItem();
            ctxDropTable = new ToolStripMenuItem();
            ctxTableProps = new ToolStripMenuItem();
            ctxRefresh = new ToolStripMenuItem();
            grid = new DataGridView();
            gridMenu = new ContextMenuStrip(components);
            ctxInsert = new ToolStripMenuItem();
            ctxEdit = new ToolStripMenuItem();
            ctxDelete = new ToolStripMenuItem();
            pager = new Panel();
            btnPrev = new Button();
            btnNext = new Button();
            lblPage = new Label();
            cmbPageSize = new ComboBox();
            menuStrip.SuspendLayout();
            toolStrip.SuspendLayout();
            statusStrip.SuspendLayout();
            ((System.ComponentModel.ISupportInitialize)split).BeginInit();
            split.Panel1.SuspendLayout();
            split.Panel2.SuspendLayout();
            split.SuspendLayout();
            treeMenu.SuspendLayout();
            ((System.ComponentModel.ISupportInitialize)grid).BeginInit();
            gridMenu.SuspendLayout();
            pager.SuspendLayout();
            SuspendLayout();
            //
            // menuStrip
            //
            menuStrip.Items.AddRange(new ToolStripItem[] { mnuFile, mnuTable, mnuData, mnuHelp });
            menuStrip.Location = new Point(0, 0);
            menuStrip.Name = "menuStrip";
            menuStrip.Padding = new Padding(6, 2, 0, 2);
            menuStrip.Size = new Size(1280, 25);
            //
            // File
            //
            mnuFile.DropDownItems.AddRange(new ToolStripItem[] { mnuNew, mnuOpen, mnuSaveCopy, new ToolStripSeparator(), mnuClose, new ToolStripSeparator(), mnuExit });
            mnuFile.Text = "文件";
            mnuNew.ShortcutKeys = Keys.Control | Keys.N;
            mnuNew.Text = "新建数据库…";
            mnuNew.Click += (_, _) => NewDatabase();
            mnuOpen.ShortcutKeys = Keys.Control | Keys.O;
            mnuOpen.Text = "打开…";
            mnuOpen.Click += (_, _) => OpenDatabase();
            mnuSaveCopy.Text = "另存副本…";
            mnuSaveCopy.Click += (_, _) => SaveCopy();
            mnuClose.Text = "关闭";
            mnuClose.Click += (_, _) => CloseDatabase();
            mnuExit.Text = "退出";
            mnuExit.Click += (_, _) => Close();
            //
            // Table
            //
            mnuTable.DropDownItems.AddRange(new ToolStripItem[] { mnuCreateTable, mnuDropTable, mnuTableProps, new ToolStripSeparator(), mnuRefresh });
            mnuTable.Text = "表";
            mnuCreateTable.Text = "新建表…";
            mnuCreateTable.Click += (_, _) => CreateTable();
            mnuDropTable.Text = "删除表";
            mnuDropTable.Click += (_, _) => DropTable();
            mnuTableProps.Text = "属性与统计…";
            mnuTableProps.Click += (_, _) => ShowTableProperties();
            mnuRefresh.ShortcutKeys = Keys.F5;
            mnuRefresh.Text = "刷新";
            mnuRefresh.Click += (_, _) => RefreshAll();
            //
            // Data
            //
            mnuData.DropDownItems.AddRange(new ToolStripItem[] { mnuInsert, mnuEdit, mnuDelete });
            mnuData.Text = "数据";
            mnuInsert.ShortcutKeys = Keys.Insert;
            mnuInsert.Text = "插入行…";
            mnuInsert.Click += (_, _) => InsertRow();
            mnuEdit.Text = "编辑行…";
            mnuEdit.Click += (_, _) => EditRow();
            mnuDelete.ShortcutKeys = Keys.Delete;
            mnuDelete.Text = "删除行";
            mnuDelete.Click += (_, _) => DeleteRow();
            //
            // Help
            //
            mnuHelp.DropDownItems.Add(mnuAbout);
            mnuHelp.Text = "帮助";
            mnuAbout.Text = "关于";
            mnuAbout.Click += (_, _) => ShowAbout();
            //
            // toolStrip
            //
            toolStrip.Items.AddRange(new ToolStripItem[]
            {
                btnNew, btnOpen, btnSaveCopy, new ToolStripSeparator(),
                btnCreateTable, btnRefresh, new ToolStripSeparator(),
                btnInsert, btnEdit, btnDelete,
            });
            btnNew.Text = "新建";
            btnNew.DisplayStyle = ToolStripItemDisplayStyle.Text;
            btnNew.Click += (_, _) => NewDatabase();
            btnOpen.Text = "打开";
            btnOpen.DisplayStyle = ToolStripItemDisplayStyle.Text;
            btnOpen.Click += (_, _) => OpenDatabase();
            btnSaveCopy.Text = "副本";
            btnSaveCopy.DisplayStyle = ToolStripItemDisplayStyle.Text;
            btnSaveCopy.Click += (_, _) => SaveCopy();
            btnCreateTable.Text = "建表";
            btnCreateTable.DisplayStyle = ToolStripItemDisplayStyle.Text;
            btnCreateTable.Click += (_, _) => CreateTable();
            btnRefresh.Text = "刷新";
            btnRefresh.DisplayStyle = ToolStripItemDisplayStyle.Text;
            btnRefresh.Click += (_, _) => RefreshAll();
            btnInsert.Text = "插入";
            btnInsert.DisplayStyle = ToolStripItemDisplayStyle.Text;
            btnInsert.Click += (_, _) => InsertRow();
            btnEdit.Text = "编辑";
            btnEdit.DisplayStyle = ToolStripItemDisplayStyle.Text;
            btnEdit.Click += (_, _) => EditRow();
            btnDelete.Text = "删除";
            btnDelete.DisplayStyle = ToolStripItemDisplayStyle.Text;
            btnDelete.Click += (_, _) => DeleteRow();
            //
            // statusStrip
            //
            slPath.Spring = true;
            slPath.TextAlign = ContentAlignment.MiddleLeft;
            slPath.Text = "未打开数据库";
            slVersion.Text = "";
            slRows.Text = "";
            slPage.Text = "";
            statusStrip.Items.AddRange(new ToolStripItem[] { slPath, slVersion, slRows, slPage });
            //
            // split
            //
            split.Dock = DockStyle.Fill;
            split.SplitterDistance = 240;
            split.Panel1.Controls.Add(tree);
            split.Panel2.Controls.Add(grid);
            split.Panel2.Controls.Add(pager);
            //
            // tree
            //
            tree.Dock = DockStyle.Fill;
            tree.HideSelection = false;
            tree.ContextMenuStrip = treeMenu;
            tree.AfterSelect += Tree_AfterSelect;
            tree.NodeMouseClick += Tree_NodeMouseClick;
            ctxCreateTable.Text = "新建表…";
            ctxCreateTable.Click += (_, _) => CreateTable();
            ctxDropTable.Text = "删除表";
            ctxDropTable.Click += (_, _) => DropTable();
            ctxTableProps.Text = "属性与统计…";
            ctxTableProps.Click += (_, _) => ShowTableProperties();
            ctxRefresh.Text = "刷新";
            ctxRefresh.Click += (_, _) => RefreshAll();
            treeMenu.Items.AddRange(new ToolStripItem[] { ctxCreateTable, ctxDropTable, ctxTableProps, new ToolStripSeparator(), ctxRefresh });
            //
            // pager
            //
            pager.Dock = DockStyle.Bottom;
            pager.Height = 40;
            pager.Padding = new Padding(8, 6, 8, 6);
            btnPrev.Text = "上一页";
            btnPrev.AutoSize = true;
            btnPrev.Location = new Point(8, 6);
            btnPrev.Click += (_, _) => ChangePage(-1);
            btnNext.Text = "下一页";
            btnNext.AutoSize = true;
            btnNext.Location = new Point(90, 6);
            btnNext.Click += (_, _) => ChangePage(1);
            lblPage.AutoSize = true;
            lblPage.Location = new Point(180, 12);
            lblPage.Text = "第 1 页";
            cmbPageSize.DropDownStyle = ComboBoxStyle.DropDownList;
            cmbPageSize.Items.AddRange(["25", "50", "100", "200"]);
            cmbPageSize.SelectedItem = "50";
            cmbPageSize.Location = new Point(300, 8);
            cmbPageSize.Width = 72;
            cmbPageSize.SelectedIndexChanged += (_, _) => { currentPage = 0; LoadGrid(); };
            pager.Controls.Add(btnPrev);
            pager.Controls.Add(btnNext);
            pager.Controls.Add(lblPage);
            pager.Controls.Add(cmbPageSize);
            //
            // grid
            //
            grid.AllowUserToAddRows = false;
            grid.AllowUserToDeleteRows = false;
            grid.ReadOnly = true;
            grid.SelectionMode = DataGridViewSelectionMode.FullRowSelect;
            grid.MultiSelect = false;
            grid.Dock = DockStyle.Fill;
            grid.RowHeadersVisible = false;
            grid.AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.DisplayedCells;
            grid.AlternatingRowsDefaultCellStyle = new DataGridViewCellStyle { BackColor = Color.FromArgb(248, 250, 252) };
            grid.ContextMenuStrip = gridMenu;
            grid.CellDoubleClick += (_, _) => EditRow();
            ctxInsert.Text = "插入行…";
            ctxInsert.Click += (_, _) => InsertRow();
            ctxEdit.Text = "编辑行…";
            ctxEdit.Click += (_, _) => EditRow();
            ctxDelete.Text = "删除行";
            ctxDelete.Click += (_, _) => DeleteRow();
            gridMenu.Items.AddRange(new ToolStripItem[] { ctxInsert, ctxEdit, ctxDelete });
            //
            // Form1
            //
            AutoScaleDimensions = new SizeF(7F, 17F);
            AutoScaleMode = AutoScaleMode.Font;
            ClientSize = new Size(1280, 720);
            Controls.Add(split);
            Controls.Add(toolStrip);
            Controls.Add(statusStrip);
            Controls.Add(menuStrip);
            Font = new Font("Microsoft YaHei UI", 9F);
            MainMenuStrip = menuStrip;
            MinimumSize = new Size(800, 500);
            Name = "Form1";
            Text = "LumDb Explorer";
            FormClosing += (_, e) => CloseDatabase();
            menuStrip.ResumeLayout(false);
            menuStrip.PerformLayout();
            toolStrip.ResumeLayout(false);
            toolStrip.PerformLayout();
            statusStrip.ResumeLayout(false);
            statusStrip.PerformLayout();
            split.Panel1.ResumeLayout(false);
            split.Panel2.ResumeLayout(false);
            ((System.ComponentModel.ISupportInitialize)split).EndInit();
            split.ResumeLayout(false);
            treeMenu.ResumeLayout(false);
            ((System.ComponentModel.ISupportInitialize)grid).EndInit();
            gridMenu.ResumeLayout(false);
            pager.ResumeLayout(false);
            pager.PerformLayout();
            ResumeLayout(false);
            PerformLayout();
        }

        #endregion

        private MenuStrip menuStrip;
        private ToolStripMenuItem mnuFile, mnuNew, mnuOpen, mnuSaveCopy, mnuClose, mnuExit;
        private ToolStripMenuItem mnuTable, mnuCreateTable, mnuDropTable, mnuTableProps, mnuRefresh;
        private ToolStripMenuItem mnuData, mnuInsert, mnuEdit, mnuDelete;
        private ToolStripMenuItem mnuHelp, mnuAbout;
        private ToolStrip toolStrip;
        private ToolStripButton btnNew, btnOpen, btnSaveCopy, btnCreateTable, btnInsert, btnEdit, btnDelete, btnRefresh;
        private StatusStrip statusStrip;
        private ToolStripStatusLabel slPath, slVersion, slRows, slPage;
        private SplitContainer split;
        private TreeView tree;
        private ContextMenuStrip treeMenu;
        private ToolStripMenuItem ctxCreateTable, ctxDropTable, ctxTableProps, ctxRefresh;
        private DataGridView grid;
        private ContextMenuStrip gridMenu;
        private ToolStripMenuItem ctxInsert, ctxEdit, ctxDelete;
        private Panel pager;
        private Button btnPrev, btnNext;
        private Label lblPage;
        private ComboBox cmbPageSize;
    }
}
