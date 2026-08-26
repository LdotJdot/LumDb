using LumDbEngine.Element.Engine;
using LumDbEngine.Element.Engine.Transaction;
using LumDbEngine.Element.Structure;

namespace LumDbExplorer
{
    public partial class Form1 : Form
    {
        DbEngine? db;
        string? dbPath;
        uint currentPage;
        uint totalRows;
        TableInfo? currentTable;

        record TableInfo(string Name, (string columnName, string dataType, bool isKey)[] Columns);

        public Form1()
        {
            InitializeComponent();
            UpdateCommands();
        }

        void NewDatabase()
        {
            using var dlg = new SaveFileDialog
            {
                Filter = "LumDb 数据库 (*.db)|*.db|所有文件 (*.*)|*.*",
                Title = "新建数据库",
                FileName = "new.db",
                OverwritePrompt = true,
            };
            if (dlg.ShowDialog(this) != DialogResult.OK) return;
            try
            {
                if (File.Exists(dlg.FileName))
                    File.Delete(dlg.FileName);
                OpenPath(dlg.FileName, create: true);
            }
            catch (Exception ex)
            {
                ShowError("新建失败", ex);
            }
        }

        void OpenDatabase()
        {
            using var dlg = new OpenFileDialog
            {
                Filter = "LumDb 数据库 (*.db)|*.db|所有文件 (*.*)|*.*",
                Title = "打开数据库",
                CheckFileExists = true,
            };
            if (dlg.ShowDialog(this) != DialogResult.OK) return;
            try { OpenPath(dlg.FileName, create: false); }
            catch (Exception ex) { ShowError("打开失败", ex); }
        }

        void OpenPath(string path, bool create)
        {
            CloseDatabase();
            db = new DbEngine(path, createIfNotExists: create);
            dbPath = path;
            currentPage = 0;
            currentTable = null;
            Text = $"LumDb Explorer — {Path.GetFileName(path)}";
            RefreshTree();
            UpdateCommands();
        }

        void SaveCopy()
        {
            if (db == null) return;
            using var dlg = new SaveFileDialog
            {
                Filter = "LumDb 数据库 (*.db)|*.db|所有文件 (*.*)|*.*",
                Title = "另存已提交镜像",
                FileName = dbPath == null ? "copy.db" : Path.GetFileNameWithoutExtension(dbPath) + "-copy.db",
            };
            if (dlg.ShowDialog(this) != DialogResult.OK) return;
            try
            {
                db.SaveTo(dlg.FileName);
                MessageBox.Show(this, "已写入已提交镜像（不含未提交事务）。\n" + dlg.FileName, "另存副本", MessageBoxButtons.OK, MessageBoxIcon.Information);
            }
            catch (Exception ex) { ShowError("另存失败", ex); }
        }

        void CloseDatabase()
        {
            try { db?.Dispose(); }
            catch { /* ignore */ }
            db = null;
            dbPath = null;
            currentTable = null;
            currentPage = 0;
            totalRows = 0;
            tree.Nodes.Clear();
            grid.Rows.Clear();
            grid.Columns.Clear();
            Text = "LumDb Explorer";
            UpdateCommands();
        }

        void RefreshAll()
        {
            if (db == null) return;
            var keep = currentTable?.Name;
            RefreshTree();
            if (keep != null && tree.Nodes.Count > 0)
            {
                foreach (TreeNode n in tree.Nodes[0].Nodes)
                {
                    if (n.Tag is TableInfo t && t.Name == keep)
                    {
                        tree.SelectedNode = n;
                        break;
                    }
                }
            }
            LoadGrid();
        }

        void RefreshTree()
        {
            tree.Nodes.Clear();
            if (db == null) return;

            var root = new TreeNode(dbPath != null ? Path.GetFileName(dbPath) : "(memory)") { Tag = "db" };
            using var ts = db.StartTransaction();
            var names = ts.GetTableNames();
            if (names.IsSuccess)
            {
                foreach (var tb in names.Values.OrderBy(t => t.tableName, StringComparer.OrdinalIgnoreCase))
                {
                    var info = new TableInfo(tb.tableName, tb.columns);
                    var node = new TreeNode($"{tb.tableName}  ({tb.columns.Length} 列)") { Tag = info };
                    root.Nodes.Add(node);
                }
            }
            tree.Nodes.Add(root);
            root.Expand();
            UpdateStatus();
        }

        void Tree_NodeMouseClick(object? sender, TreeNodeMouseClickEventArgs e)
        {
            if (e.Button == MouseButtons.Right)
                tree.SelectedNode = e.Node;
        }

        void Tree_AfterSelect(object? sender, TreeViewEventArgs e)
        {
            if (e.Node?.Tag is TableInfo info)
            {
                currentTable = info;
                currentPage = 0;
                LoadGrid();
            }
            UpdateCommands();
        }

        uint PageSize
        {
            get
            {
                if (cmbPageSize.SelectedItem is string s && uint.TryParse(s, out var n) && n > 0)
                    return n;
                return 50;
            }
        }

        void ChangePage(int delta)
        {
            if (delta < 0 && currentPage == 0) return;
            var next = (int)currentPage + delta;
            if (next < 0) next = 0;
            var maxPage = totalRows == 0 ? 0 : (totalRows - 1) / PageSize;
            if ((uint)next > maxPage) next = (int)maxPage;
            currentPage = (uint)next;
            LoadGrid();
        }

        void LoadGrid()
        {
            grid.Rows.Clear();
            grid.Columns.Clear();
            totalRows = 0;
            if (db == null || currentTable == null)
            {
                UpdateStatus();
                return;
            }

            var idCol = new DataGridViewTextBoxColumn { Name = "_id", HeaderText = "Id", ReadOnly = true, FillWeight = 12 };
            grid.Columns.Add(idCol);
            foreach (var c in currentTable.Columns)
            {
                var header = c.isKey ? $"{c.columnName}  ({c.dataType}, Key)" : $"{c.columnName}  ({c.dataType})";
                grid.Columns.Add(new DataGridViewTextBoxColumn { Name = c.columnName, HeaderText = header });
            }

            try
            {
                using var ts = db.StartTransaction();
                var cnt = ts.Count(currentTable.Name, static (ref RowView _) => true);
                totalRows = cnt.IsSuccess ? cnt.Value : 0;

                uint skip = currentPage * PageSize;
                uint taken = 0;
                var table = currentTable;
                ts.GoThrough(table.Name, (uint id, ref RowView row) =>
                {
                    if (skip > 0) { skip--; return true; }
                    if (taken >= PageSize) return false;
                    var cells = new object[table.Columns.Length + 1];
                    cells[0] = id;
                    for (int i = 0; i < table.Columns.Length; i++)
                        cells[i + 1] = CellCodec.Format(row.GetCell(i));
                    var r = grid.Rows[grid.Rows.Add(cells)];
                    r.Tag = id;
                    taken++;
                    return true;
                });
            }
            catch (Exception ex)
            {
                ShowError("读取失败", ex);
            }

            UpdateStatus();
        }

        void CreateTable()
        {
            if (db == null) return;
            using var dlg = new CreateTableDialog();
            if (dlg.ShowDialog(this) != DialogResult.OK) return;
            RunWrite(ts =>
            {
                var r = ts.Create(dlg.TableName, dlg.Columns);
                if (!r.IsSuccess) throw r.Exception ?? new Exception("建表失败");
            }, "建表失败");
            currentTable = new TableInfo(dlg.TableName, dlg.Columns.Select(c => (c.columnName, c.type.ToString(), c.isKey)).ToArray());
            RefreshTree();
            SelectTable(dlg.TableName);
        }

        void DropTable()
        {
            if (db == null || currentTable == null) return;
            if (MessageBox.Show(this, $"删除表 “{currentTable.Name}” 及其全部数据？", "删除表", MessageBoxButtons.YesNo, MessageBoxIcon.Warning) != DialogResult.Yes)
                return;
            var name = currentTable.Name;
            RunWrite(ts =>
            {
                var r = ts.Drop(name);
                if (!r.IsSuccess) throw r.Exception ?? new Exception("删表失败");
            }, "删表失败");
            currentTable = null;
            RefreshTree();
            LoadGrid();
        }

        void ShowTableProperties()
        {
            if (db == null || currentTable == null) return;
            uint rows = 0;
            using (var ts = db.StartTransaction())
            {
                var c = ts.Count(currentTable.Name, static (ref RowView _) => true);
                if (c.IsSuccess) rows = c.Value;
            }
            long bytes = dbPath != null && File.Exists(dbPath) ? new FileInfo(dbPath).Length : -1;
            using var dlg = new TablePropertiesDialog(
                dbPath ?? "(memory)",
                db.VersionString,
                bytes,
                currentTable.Name,
                currentTable.Columns.Select(c => (c.columnName, c.dataType, c.isKey)).ToArray(),
                rows);
            dlg.ShowDialog(this);
        }

        void InsertRow()
        {
            if (db == null || currentTable == null) return;
            var cols = ResolveTypes(currentTable);
            using var dlg = new RowEditDialog(currentTable.Name, cols, null, null);
            if (dlg.ShowDialog(this) != DialogResult.OK) return;
            RunWrite(ts =>
            {
                var values = cols.Select((c, i) => (c.name, CellCodec.Parse(dlg.Values[i], c.type))).ToArray();
                var r = ts.Insert(currentTable.Name, values);
                if (!r.IsSuccess) throw r.Exception ?? new Exception("插入失败");
            }, "插入失败");
            LoadGrid();
        }

        void EditRow()
        {
            if (db == null || currentTable == null) return;
            if (grid.CurrentRow?.Tag is not uint id)
            {
                MessageBox.Show(this, "请先选中一行。", "编辑", MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }
            var cols = ResolveTypes(currentTable);
            var existing = new string[cols.Length];
            for (int i = 0; i < cols.Length; i++)
                existing[i] = Convert.ToString(grid.CurrentRow.Cells[i + 1].Value) ?? "";

            using var dlg = new RowEditDialog(currentTable.Name, cols, existing, id);
            if (dlg.ShowDialog(this) != DialogResult.OK) return;
            RunWrite(ts =>
            {
                for (int i = 0; i < cols.Length; i++)
                {
                    var r = ts.Update(currentTable.Name, id, cols[i].name, CellCodec.Parse(dlg.Values[i], cols[i].type));
                    if (!r.IsSuccess) throw r.Exception ?? new Exception("更新失败");
                }
            }, "更新失败");
            LoadGrid();
        }

        void DeleteRow()
        {
            if (db == null || currentTable == null) return;
            if (grid.CurrentRow?.Tag is not uint id)
            {
                MessageBox.Show(this, "请先选中一行。", "删除", MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }
            if (MessageBox.Show(this, $"删除 Id = {id} 的行？", "删除行", MessageBoxButtons.YesNo, MessageBoxIcon.Question) != DialogResult.Yes)
                return;
            var table = currentTable.Name;
            RunWrite(ts =>
            {
                var r = ts.Delete(table, id);
                if (!r.IsSuccess) throw r.Exception ?? new Exception("删除失败");
            }, "删除失败");
            LoadGrid();
        }

        void RunWrite(Action<ITransaction> act, string title)
        {
            if (db == null) return;
            try
            {
                using var ts = db.StartTransaction();
                act(ts);
            }
            catch (Exception ex)
            {
                ShowError(title, ex);
            }
        }

        static (string name, DbValueType type, bool isKey)[] ResolveTypes(TableInfo table)
        {
            var list = new (string, DbValueType, bool)[table.Columns.Length];
            for (int i = 0; i < table.Columns.Length; i++)
            {
                var c = table.Columns[i];
                if (!CellCodec.TryParseType(c.dataType, out var t))
                    t = DbValueType.StrVar;
                list[i] = (c.columnName, t, c.isKey);
            }
            return list;
        }

        void SelectTable(string name)
        {
            if (tree.Nodes.Count == 0) return;
            foreach (TreeNode n in tree.Nodes[0].Nodes)
            {
                if (n.Tag is TableInfo t && t.Name == name)
                {
                    tree.SelectedNode = n;
                    return;
                }
            }
        }

        void UpdateCommands()
        {
            var hasDb = db != null;
            var hasTable = hasDb && currentTable != null;
            mnuSaveCopy.Enabled = btnSaveCopy.Enabled = hasDb;
            mnuClose.Enabled = hasDb;
            mnuCreateTable.Enabled = btnCreateTable.Enabled = ctxCreateTable.Enabled = hasDb;
            mnuDropTable.Enabled = ctxDropTable.Enabled = hasTable;
            mnuTableProps.Enabled = ctxTableProps.Enabled = hasTable;
            mnuRefresh.Enabled = btnRefresh.Enabled = ctxRefresh.Enabled = hasDb;
            mnuInsert.Enabled = btnInsert.Enabled = ctxInsert.Enabled = hasTable;
            mnuEdit.Enabled = btnEdit.Enabled = ctxEdit.Enabled = hasTable;
            mnuDelete.Enabled = btnDelete.Enabled = ctxDelete.Enabled = hasTable;
            btnPrev.Enabled = btnNext.Enabled = hasTable;
            UpdateStatus();
        }

        void UpdateStatus()
        {
            slPath.Text = db == null ? "未打开数据库" : (dbPath ?? "(memory)");
            slVersion.Text = db == null ? "" : $"格式 {db.VersionString}";
            slRows.Text = currentTable == null ? "" : $"行 {totalRows:N0}";
            var pages = totalRows == 0 ? 1 : (int)((totalRows + PageSize - 1) / PageSize);
            slPage.Text = currentTable == null ? "" : $"页 {currentPage + 1}/{pages}";
            lblPage.Text = currentTable == null ? "" : $"第 {currentPage + 1} / {pages} 页（每页 {PageSize}）";
        }

        void ShowAbout()
        {
            MessageBox.Show(this,
                "LumDb Explorer\n\n" +
                "新建或打开 .db 文件，创建表，插入/编辑/删除行。\n" +
                "另存副本写入已提交镜像（SaveTo），可用另一引擎打开。\n\n" +
                $"引擎格式版本：{DbEngine.CurrentVersionString}",
                "关于", MessageBoxButtons.OK, MessageBoxIcon.Information);
        }

        void ShowError(string title, Exception ex)
            => MessageBox.Show(this, ex.Message, title, MessageBoxButtons.OK, MessageBoxIcon.Error);
    }
}
