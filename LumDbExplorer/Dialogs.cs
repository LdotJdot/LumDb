using LumDbEngine.Element.Structure;

namespace LumDbExplorer
{
    internal sealed class CreateTableDialog : Form
    {
        readonly TextBox _name = new();
        readonly DataGridView _grid = new();

        public string TableName => _name.Text.Trim();
        public (string columnName, DbValueType type, bool isKey)[] Columns { get; private set; } = [];

        public CreateTableDialog()
        {
            Text = "新建表";
            StartPosition = FormStartPosition.CenterParent;
            FormBorderStyle = FormBorderStyle.FixedDialog;
            MinimizeBox = false;
            MaximizeBox = false;
            ClientSize = new Size(560, 420);
            Font = new Font("Microsoft YaHei UI", 9F);

            var types = Enum.GetValues<DbValueType>().Where(t => t != DbValueType.Unknow).Select(t => t.ToString()).ToArray();

            _grid.Dock = DockStyle.Fill;
            _grid.AllowUserToAddRows = true;
            _grid.RowHeadersVisible = false;
            _grid.AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill;
            _grid.Columns.Add(new DataGridViewTextBoxColumn { Name = "col", HeaderText = "列名", FillWeight = 40 });
            _grid.Columns.Add(new DataGridViewComboBoxColumn { Name = "typ", HeaderText = "类型", DataSource = types, FillWeight = 40 });
            _grid.Columns.Add(new DataGridViewCheckBoxColumn { Name = "key", HeaderText = "索引(Key)", FillWeight = 20 });

            var top = new FlowLayoutPanel { Dock = DockStyle.Top, Height = 40, Padding = new Padding(8, 8, 8, 0) };
            top.Controls.Add(new Label { Text = "表名", AutoSize = true, Padding = new Padding(0, 6, 0, 0) });
            _name.Width = 240;
            _name.Text = "table1";
            top.Controls.Add(_name);

            var bottom = new FlowLayoutPanel { Dock = DockStyle.Bottom, Height = 44, FlowDirection = FlowDirection.RightToLeft, Padding = new Padding(8) };
            var ok = new Button { Text = "创建", DialogResult = DialogResult.None, Width = 88 };
            var cancel = new Button { Text = "取消", DialogResult = DialogResult.Cancel, Width = 88 };
            ok.Click += (_, _) => Accept();
            bottom.Controls.Add(ok);
            bottom.Controls.Add(cancel);

            var hint = new Label
            {
                Dock = DockStyle.Bottom,
                Height = 36,
                Text = "列名最长 32 字符。Key 会建二级索引。引擎行 Id 自动生成，不必作为列。",
                Padding = new Padding(10, 4, 10, 0),
            };

            Controls.Add(_grid);
            Controls.Add(hint);
            Controls.Add(bottom);
            Controls.Add(top);
            AcceptButton = ok;
            CancelButton = cancel;
        }

        void Accept()
        {
            if (string.IsNullOrWhiteSpace(TableName) || TableName.Length > 32)
            {
                MessageBox.Show(this, "表名必填且不超过 32 字符。", Text, MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            var list = new List<(string, DbValueType, bool)>();
            var names = new HashSet<string>(StringComparer.Ordinal);
            foreach (DataGridViewRow row in _grid.Rows)
            {
                if (row.IsNewRow) continue;
                var n = Convert.ToString(row.Cells["col"].Value)?.Trim() ?? "";
                if (n.Length == 0) continue;
                if (n.Length > 32)
                {
                    MessageBox.Show(this, $"列名过长：{n}", Text, MessageBoxButtons.OK, MessageBoxIcon.Warning);
                    return;
                }
                if (!names.Add(n))
                {
                    MessageBox.Show(this, $"重复列名：{n}", Text, MessageBoxButtons.OK, MessageBoxIcon.Warning);
                    return;
                }
                var typeName = Convert.ToString(row.Cells["typ"].Value) ?? "Int";
                if (!CellCodec.TryParseType(typeName, out var typ))
                {
                    MessageBox.Show(this, $"未知类型：{typeName}", Text, MessageBoxButtons.OK, MessageBoxIcon.Warning);
                    return;
                }
                var key = row.Cells["key"].Value is true;
                list.Add((n, typ, key));
            }

            if (list.Count == 0)
            {
                MessageBox.Show(this, "至少需要一列。", Text, MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            Columns = list.ToArray();
            DialogResult = DialogResult.OK;
            Close();
        }
    }

    internal sealed class RowEditDialog : Form
    {
        readonly TextBox[] _boxes;
        readonly (string name, DbValueType type, bool isKey)[] _cols;

        public string[] Values { get; private set; } = [];

        public RowEditDialog(string table, (string name, DbValueType type, bool isKey)[] cols, string[]? existing, uint? rowId)
        {
            _cols = cols;
            Text = existing == null ? $"插入 — {table}" : $"编辑 #{rowId} — {table}";
            StartPosition = FormStartPosition.CenterParent;
            FormBorderStyle = FormBorderStyle.FixedDialog;
            MinimizeBox = false;
            MaximizeBox = false;
            Font = new Font("Microsoft YaHei UI", 9F);
            Width = 520;
            Height = Math.Min(640, 120 + cols.Length * 36);

            var layout = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                ColumnCount = 2,
                RowCount = cols.Length + 1,
                Padding = new Padding(12),
            };
            layout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 32));
            layout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 68));

            _boxes = new TextBox[cols.Length];
            for (int i = 0; i < cols.Length; i++)
            {
                var label = $"{cols[i].name}  ({cols[i].type}{(cols[i].isKey ? ", Key" : "")})";
                layout.Controls.Add(new Label { Text = label, AutoSize = true, Anchor = AnchorStyles.Left }, 0, i);
                _boxes[i] = new TextBox { Dock = DockStyle.Fill };
                if (existing != null && i < existing.Length)
                    _boxes[i].Text = existing[i];
                if (cols[i].type is DbValueType.BytesVar or DbValueType.StrVar)
                    _boxes[i].PlaceholderText = cols[i].type == DbValueType.BytesVar ? "十六进制，可空" : "";
                layout.Controls.Add(_boxes[i], 1, i);
            }

            var buttons = new FlowLayoutPanel { FlowDirection = FlowDirection.RightToLeft, Dock = DockStyle.Fill };
            var ok = new Button { Text = "保存", Width = 88 };
            var cancel = new Button { Text = "取消", DialogResult = DialogResult.Cancel, Width = 88 };
            ok.Click += (_, _) =>
            {
                try
                {
                    Values = _boxes.Select(b => b.Text).ToArray();
                    foreach (var (col, i) in cols.Select((c, i) => (c, i)))
                        _ = CellCodec.Parse(Values[i], col.type);
                    DialogResult = DialogResult.OK;
                    Close();
                }
                catch (Exception ex)
                {
                    MessageBox.Show(this, "无法解析单元格：\n" + ex.Message, Text, MessageBoxButtons.OK, MessageBoxIcon.Warning);
                }
            };
            buttons.Controls.Add(ok);
            buttons.Controls.Add(cancel);
            layout.Controls.Add(buttons, 1, cols.Length);

            Controls.Add(layout);
            AcceptButton = ok;
            CancelButton = cancel;
        }
    }

    internal sealed class TablePropertiesDialog : Form
    {
        public TablePropertiesDialog(
            string dbPath,
            string version,
            long fileBytes,
            string table,
            (string name, string type, bool isKey)[] columns,
            uint rowCount)
        {
            Text = $"表属性 — {table}";
            StartPosition = FormStartPosition.CenterParent;
            FormBorderStyle = FormBorderStyle.FixedDialog;
            MinimizeBox = false;
            MaximizeBox = false;
            ClientSize = new Size(520, 440);
            Font = new Font("Microsoft YaHei UI", 9F);

            var info = new TextBox
            {
                Dock = DockStyle.Top,
                Height = 110,
                Multiline = true,
                ReadOnly = true,
                BorderStyle = BorderStyle.None,
                Text =
                    $"数据库  {dbPath}\r\n" +
                    $"格式    {version}\r\n" +
                    $"文件    {(fileBytes < 0 ? "（内存，未落盘）" : $"{fileBytes:N0} 字节")}\r\n" +
                    $"表      {table}\r\n" +
                    $"行数    {rowCount:N0}\r\n" +
                    $"列数    {columns.Length}",
            };

            var grid = new DataGridView
            {
                Dock = DockStyle.Fill,
                ReadOnly = true,
                AllowUserToAddRows = false,
                AllowUserToDeleteRows = false,
                RowHeadersVisible = false,
                AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill,
            };
            grid.Columns.Add("n", "列名");
            grid.Columns.Add("t", "类型");
            grid.Columns.Add("k", "Key");
            foreach (var c in columns)
                grid.Rows.Add(c.name, c.type, c.isKey ? "是" : "");

            var close = new Button { Text = "关闭", DialogResult = DialogResult.OK, Dock = DockStyle.Bottom, Height = 36 };
            Controls.Add(grid);
            Controls.Add(close);
            Controls.Add(info);
            AcceptButton = close;
            CancelButton = close;
        }
    }
}
