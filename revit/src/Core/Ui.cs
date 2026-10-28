using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Windows.Forms;

namespace OmarKhamis.Revit.Core
{
    /// <summary>Small right-to-left Arabic dialog builder used by every tool (WinForms works the same in Revit 2017 → 2027).</summary>
    public class Dlg : Form
    {
        public static readonly Color Accent = Color.FromArgb(0xC8, 0x8A, 0x2E);
        public static readonly Color Ink = Color.FromArgb(0x22, 0x26, 0x2E);
        public static readonly Font Body = new Font("Segoe UI", 10f);
        public static readonly Font Bold = new Font("Segoe UI", 10f, FontStyle.Bold);

        readonly TableLayoutPanel _grid;
        readonly Dictionary<string, Control> _ctl = new Dictionary<string, Control>();
        public Button OkButton;

        public Dlg(string title, string intro = null, int width = 560)
        {
            Text = title + " — م. عمر خميس";
            RightToLeft = RightToLeft.Yes;
            RightToLeftLayout = true;
            Font = Body;
            FormBorderStyle = FormBorderStyle.FixedDialog;
            MaximizeBox = false; MinimizeBox = false;
            StartPosition = FormStartPosition.CenterScreen;
            AutoSize = true; AutoSizeMode = AutoSizeMode.GrowAndShrink;
            BackColor = Color.White;
            ShowInTaskbar = false;
            Padding = new Padding(14);

            var outer = new FlowLayoutPanel { FlowDirection = FlowDirection.TopDown, AutoSize = true, WrapContents = false, Dock = DockStyle.Fill };
            Controls.Add(outer);
            var head = new Label { Text = title, Font = new Font("Segoe UI", 13f, FontStyle.Bold), ForeColor = Ink, AutoSize = true, Margin = new Padding(0, 0, 0, 4) };
            outer.Controls.Add(head);
            if (!string.IsNullOrEmpty(intro))
                outer.Controls.Add(new Label { Text = intro, AutoSize = true, MaximumSize = new Size(width, 0), ForeColor = Color.DimGray, Margin = new Padding(0, 0, 0, 10) });

            _grid = new TableLayoutPanel { ColumnCount = 2, AutoSize = true, Width = width, Margin = new Padding(0) };
            _grid.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 170));
            _grid.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, width - 175));
            outer.Controls.Add(_grid);

            var bar = new FlowLayoutPanel { FlowDirection = FlowDirection.LeftToRight, AutoSize = true, Width = width, Margin = new Padding(0, 14, 0, 0) };
            OkButton = new Button { Text = "تنفيذ ✓", DialogResult = DialogResult.OK, AutoSize = true, BackColor = Accent, ForeColor = Color.White, FlatStyle = FlatStyle.Flat, Font = Bold, Padding = new Padding(14, 4, 14, 4) };
            OkButton.FlatAppearance.BorderSize = 0;
            var cancel = new Button { Text = "إلغاء", DialogResult = DialogResult.Cancel, AutoSize = true, FlatStyle = FlatStyle.Flat, Padding = new Padding(10, 4, 10, 4) };
            bar.Controls.Add(OkButton); bar.Controls.Add(cancel);
            outer.Controls.Add(bar);
            AcceptButton = OkButton; CancelButton = cancel;
        }

        void Row(string label, Control c, string key)
        {
            int r = _grid.RowCount++;
            _grid.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            if (label != null)
                _grid.Controls.Add(new Label { Text = label, AutoSize = true, Anchor = AnchorStyles.Right | AnchorStyles.Top, Margin = new Padding(3, 7, 3, 3) }, 0, r);
            c.Margin = new Padding(3, 3, 3, 3);
            if (c.Dock == DockStyle.None && !(c is CheckBox) && !(c is Label)) c.Width = _grid.ColumnStyles[1].Width > 0 ? (int)_grid.ColumnStyles[1].Width - 10 : 300;
            _grid.Controls.Add(c, label == null ? 0 : 1, r);
            if (label == null) _grid.SetColumnSpan(c, 2);
            if (key != null) _ctl[key] = c;
        }

        public Dlg Note(string text, Color? color = null)
        {
            Row(null, new Label { Text = text, AutoSize = true, MaximumSize = new Size((int)_grid.Width - 10, 0), ForeColor = color ?? Color.DimGray }, null);
            return this;
        }

        public Dlg Txt(string key, string label, string value = "")
        {
            Row(label, new TextBox { Text = value ?? "" }, key); return this;
        }

        public Dlg Number(string key, string label, decimal value, decimal min = 0, decimal max = 1000000, int decimals = 0)
        {
            var n = new NumericUpDown { Minimum = min, Maximum = max, DecimalPlaces = decimals, Value = Math.Max(min, Math.Min(max, value)), ThousandsSeparator = false };
            Row(label, n, key); return this;
        }

        public Dlg Choice(string key, string label, IEnumerable<string> items, int selected = 0)
        {
            var cb = new ComboBox { DropDownStyle = ComboBoxStyle.DropDownList };
            foreach (var s in items) cb.Items.Add(s);
            if (cb.Items.Count > 0) cb.SelectedIndex = Math.Max(0, Math.Min(selected, cb.Items.Count - 1));
            Row(label, cb, key); return this;
        }

        public Dlg Check(string key, string text, bool value)
        {
            Row(null, new CheckBox { Text = text, Checked = value, AutoSize = true }, key); return this;
        }

        public Dlg Checks(string key, string label, IEnumerable<string> items, bool all = true, int height = 180)
        {
            var cl = new CheckedListBox { CheckOnClick = true, Height = height, IntegralHeight = false };
            foreach (var s in items) cl.Items.Add(s, all);
            Row(label, cl, key);
            var tools = new FlowLayoutPanel { AutoSize = true, FlowDirection = FlowDirection.RightToLeft };
            var a = new LinkLabel { Text = "تحديد الكل", AutoSize = true };
            var b = new LinkLabel { Text = "إلغاء الكل", AutoSize = true };
            a.LinkClicked += (s, e) => { for (int i = 0; i < cl.Items.Count; i++) cl.SetItemChecked(i, true); };
            b.LinkClicked += (s, e) => { for (int i = 0; i < cl.Items.Count; i++) cl.SetItemChecked(i, false); };
            tools.Controls.Add(a); tools.Controls.Add(b);
            Row("", tools, null);
            return this;
        }

        public Dlg Multiline(string key, string label, string value, int height = 160)
        {
            var t = new TextBox { Multiline = true, ScrollBars = ScrollBars.Vertical, Height = height, Text = value ?? "", AcceptsReturn = true };
            Row(label, t, key); return this;
        }

        public string S(string key) { var c = _ctl[key]; return c is ComboBox ? Convert.ToString(((ComboBox)c).SelectedItem) : c.Text; }
        public int Idx(string key) { return ((ComboBox)_ctl[key]).SelectedIndex; }
        public double D(string key) { return (double)((NumericUpDown)_ctl[key]).Value; }
        public int I(string key) { return (int)((NumericUpDown)_ctl[key]).Value; }
        public bool B(string key) { return ((CheckBox)_ctl[key]).Checked; }
        public List<int> Picked(string key) { return ((CheckedListBox)_ctl[key]).CheckedIndices.Cast<int>().ToList(); }
        public Control Ctl(string key) { return _ctl[key]; }

        public bool Ask() { return ShowDialog() == DialogResult.OK; }

        // ---------- quick messages (Revit TaskDialog keeps the Revit look)
        public static void Info(string title, string text)
        {
            var td = new Autodesk.Revit.UI.TaskDialog("م. عمر خميس") { MainInstruction = title, MainContent = text };
            td.Show();
        }

        public static bool Confirm(string title, string text)
        {
            var td = new Autodesk.Revit.UI.TaskDialog("م. عمر خميس")
            {
                MainInstruction = title,
                MainContent = text,
                CommonButtons = Autodesk.Revit.UI.TaskDialogCommonButtons.Yes | Autodesk.Revit.UI.TaskDialogCommonButtons.No,
                DefaultButton = Autodesk.Revit.UI.TaskDialogResult.Yes
            };
            return td.Show() == Autodesk.Revit.UI.TaskDialogResult.Yes;
        }
    }
}
