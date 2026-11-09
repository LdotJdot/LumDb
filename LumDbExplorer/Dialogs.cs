using LumDbEngine.Element.Structure;
using System.Text;

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

    /// <summary>One column of a table, shown in the drop-down of the column pickers.</summary>
    internal sealed class ColumnChoice
    {
        public string Name { get; }
        public string Type { get; }
        public bool IsKey { get; }

        public ColumnChoice(string name, string type, bool isKey)
        {
            Name = name;
            Type = type;
            IsKey = isKey;
        }

        public override string ToString() => $"{Name}  ({Type}{(IsKey ? ", Key" : "")})";
    }

    internal sealed class AddColumnDialog : Form
    {
        readonly TextBox _name = new();
        readonly ComboBox _type = new();
        readonly CheckBox _key = new();
        readonly ComboBox _position = new();
        readonly TextBox _default = new();
        readonly Label _keyHint = new();
        readonly HashSet<string> _existing;
        readonly uint _rowCount;

        public string ColumnName => _name.Text.Trim();
        public DbValueType Type { get; private set; } = DbValueType.Int;
        public bool IsKey => _key.Checked;

        /// <summary>Zero-based position of the new column, or null to append it at the end.</summary>
        public int? Ordinal => _position.SelectedIndex <= 0 ? null : _position.SelectedIndex - 1;

        public string DefaultText => _default.Text;

        public AddColumnDialog(string table, string[] existingColumns, uint rowCount)
        {
            _existing = new HashSet<string>(existingColumns, StringComparer.Ordinal);
            _rowCount = rowCount;

            Text = $"添加列 — {table}";
            StartPosition = FormStartPosition.CenterParent;
            FormBorderStyle = FormBorderStyle.FixedDialog;
            MinimizeBox = false;
            MaximizeBox = false;
            Font = new Font("Microsoft YaHei UI", 9F);
            ClientSize = new Size(540, 390);

            var layout = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                ColumnCount = 2,
                RowCount = 6,
                Padding = new Padding(12),
            };
            layout.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 84));
            layout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
            for (int i = 0; i < 4; i++)
                layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 34));
            layout.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            layout.RowStyles.Add(new RowStyle(SizeType.Percent, 100));

            layout.Controls.Add(new Label { Text = "列名", AutoSize = true, Anchor = AnchorStyles.Left }, 0, 0);
            _name.Dock = DockStyle.Fill;
            layout.Controls.Add(_name, 1, 0);

            layout.Controls.Add(new Label { Text = "类型", AutoSize = true, Anchor = AnchorStyles.Left }, 0, 1);
            var typeRow = new FlowLayoutPanel
            {
                Dock = DockStyle.Fill,
                FlowDirection = FlowDirection.LeftToRight,
                WrapContents = false,
                Margin = new Padding(0),
            };
            _type.DropDownStyle = ComboBoxStyle.DropDownList;
            _type.Width = 190;
            _type.Items.AddRange(Enum.GetValues<DbValueType>()
                .Where(t => t != DbValueType.Unknow)
                .Select(t => (object)t.ToString())
                .ToArray());
            _type.SelectedItem = nameof(DbValueType.Int);
            _type.SelectedIndexChanged += (_, _) => OnTypeChanged();
            _key.AutoSize = true;
            _key.Margin = new Padding(12, 7, 0, 0);
            _key.Text = "索引(Key)";
            typeRow.Controls.Add(_type);
            typeRow.Controls.Add(_key);
            layout.Controls.Add(typeRow, 1, 1);

            layout.Controls.Add(new Label { Text = "位置", AutoSize = true, Anchor = AnchorStyles.Left }, 0, 2);
            _position.DropDownStyle = ComboBoxStyle.DropDownList;
            _position.Dock = DockStyle.Fill;
            _position.Items.Add("追加到末尾");
            for (int i = 0; i < existingColumns.Length; i++)
                _position.Items.Add($"插入到第 {i + 1} 列（{existingColumns[i]} 之前）");
            _position.SelectedIndex = 0;
            layout.Controls.Add(_position, 1, 2);

            layout.Controls.Add(new Label { Text = "默认值", AutoSize = true, Anchor = AnchorStyles.Left }, 0, 3);
            _default.Dock = DockStyle.Fill;
            layout.Controls.Add(_default, 1, 3);

            _keyHint.AutoSize = true;
            _keyHint.MaximumSize = new Size(490, 0);
            _keyHint.ForeColor = Color.FromArgb(150, 60, 30);
            _keyHint.Margin = new Padding(0, 6, 0, 6);
            layout.Controls.Add(_keyHint, 0, 4);
            layout.SetColumnSpan(_keyHint, 2);

            var note = new Label
            {
                AutoSize = true,
                MaximumSize = new Size(490, 0),
                ForeColor = SystemColors.GrayText,
                Text = "添加列会在当前事务内重写整张表：已有行写入默认值（留空表示该类型的零值）。\r\n"
                     + "插入到中间会改变后面各列的序号，行 Id 保持不变。一张表最多 100 列。",
            };
            layout.Controls.Add(note, 0, 5);
            layout.SetColumnSpan(note, 2);

            var buttons = new FlowLayoutPanel
            {
                Dock = DockStyle.Bottom,
                Height = 44,
                FlowDirection = FlowDirection.RightToLeft,
                Padding = new Padding(8),
            };
            var ok = new Button { Text = "添加", DialogResult = DialogResult.None, Width = 88 };
            var cancel = new Button { Text = "取消", DialogResult = DialogResult.Cancel, Width = 88 };
            ok.Click += (_, _) => Accept();
            buttons.Controls.Add(ok);
            buttons.Controls.Add(cancel);

            Controls.Add(layout);
            Controls.Add(buttons);
            AcceptButton = ok;
            CancelButton = cancel;

            OnTypeChanged();
        }

        void OnTypeChanged()
        {
            if (_type.SelectedItem is not string name || !CellCodec.TryParseType(name, out var type))
                return;

            Type = type;
            _default.PlaceholderText = CellCodec.DefaultHint(type);

            var canBeKey = CellCodec.CanBeKey(type);
            if (!canBeKey) _key.Checked = false;
            _key.Enabled = canBeKey && _rowCount <= 1;

            if (_rowCount > 1)
                _keyHint.Text = $"表内已有 {_rowCount:N0} 行，引擎不允许新增 Key 列（多行会共享同一个默认值）。";
            else if (!canBeKey)
                _keyHint.Text = $"{type} 是变长类型，不能作为 Key 列。";
            else
                _keyHint.Text = "勾选后为该列建立二级索引。";
        }

        void Accept()
        {
            var name = ColumnName;
            if (name.Length == 0)
            {
                MessageBox.Show(this, "列名必填。", Text, MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            if (Encoding.UTF8.GetByteCount(name) > 32)
            {
                MessageBox.Show(this, "列名最长 32 个 UTF-8 字节。", Text, MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            if (_existing.Contains(name))
            {
                MessageBox.Show(this, $"已存在同名列：{name}", Text, MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            if (_existing.Count + 1 > 100)
            {
                MessageBox.Show(this, "一张表最多 100 列。", Text, MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            if (_key.Checked && !CellCodec.CanBeKey(Type))
            {
                MessageBox.Show(this, $"{Type} 不能作为 Key 列。", Text, MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            DialogResult = DialogResult.OK;
            Close();
        }
    }

    internal sealed class DropColumnDialog : Form
    {
        readonly ComboBox _column = new();

        public string ColumnName => (_column.SelectedItem as ColumnChoice)?.Name ?? "";

        public DropColumnDialog(string table, (string name, string type, bool isKey)[] columns)
        {
            Text = $"删除列 — {table}";
            StartPosition = FormStartPosition.CenterParent;
            FormBorderStyle = FormBorderStyle.FixedDialog;
            MinimizeBox = false;
            MaximizeBox = false;
            Font = new Font("Microsoft YaHei UI", 9F);
            ClientSize = new Size(500, 230);

            var layout = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                ColumnCount = 2,
                RowCount = 3,
                Padding = new Padding(12),
            };
            layout.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 60));
            layout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
            layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 34));
            layout.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            layout.RowStyles.Add(new RowStyle(SizeType.Percent, 100));

            layout.Controls.Add(new Label { Text = "列", AutoSize = true, Anchor = AnchorStyles.Left }, 0, 0);
            _column.DropDownStyle = ComboBoxStyle.DropDownList;
            _column.Dock = DockStyle.Fill;
            foreach (var c in columns)
                _column.Items.Add(new ColumnChoice(c.name, c.type, c.isKey));
            _column.SelectedIndex = columns.Length > 0 ? 0 : -1;
            layout.Controls.Add(_column, 1, 0);

            var note = new Label
            {
                AutoSize = true,
                MaximumSize = new Size(440, 0),
                ForeColor = Color.FromArgb(150, 60, 30),
                Text = columns.Length <= 1
                    ? "表至少要保留一列，无法删除最后一列。"
                    : "删除列会在当前事务内重写整张表并丢弃该列的全部数据，行 Id 保持不变，操作不可撤销。",
            };
            layout.Controls.Add(note, 0, 1);
            layout.SetColumnSpan(note, 2);

            var buttons = new FlowLayoutPanel
            {
                Dock = DockStyle.Bottom,
                Height = 44,
                FlowDirection = FlowDirection.RightToLeft,
                Padding = new Padding(8),
            };
            var ok = new Button { Text = "删除", DialogResult = DialogResult.None, Width = 88, Enabled = columns.Length > 1 };
            var cancel = new Button { Text = "取消", DialogResult = DialogResult.Cancel, Width = 88 };
            ok.Click += (_, _) => Accept();
            buttons.Controls.Add(ok);
            buttons.Controls.Add(cancel);

            Controls.Add(layout);
            Controls.Add(buttons);
            AcceptButton = ok;
            CancelButton = cancel;
        }

        void Accept()
        {
            if (string.IsNullOrEmpty(ColumnName))
            {
                MessageBox.Show(this, "请选择要删除的列。", Text, MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            DialogResult = DialogResult.OK;
            Close();
        }
    }
}
