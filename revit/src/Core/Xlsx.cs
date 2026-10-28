using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Security;
using System.Text;

namespace OmarKhamis.Revit.Core
{
    /// <summary>Tiny .xlsx writer (no Excel needed, Arabic friendly, right-to-left sheets, bold header, sums row).</summary>
    public class Xlsx
    {
        public class Sheet
        {
            public string Name;
            public List<object[]> Rows = new List<object[]>();
            public bool Rtl = true;
            public int[] Totals;   // column indexes to sum at the bottom
        }

        public List<Sheet> Sheets = new List<Sheet>();

        public Sheet Add(string name, params string[] header)
        {
            var s = new Sheet { Name = Clean(name) };
            s.Rows.Add(header.Cast<object>().ToArray());
            Sheets.Add(s);
            return s;
        }

        static string Clean(string n)
        {
            foreach (var c in new[] { '\\', '/', '?', '*', '[', ']', ':' }) n = n.Replace(c, '-');
            return n.Length > 31 ? n.Substring(0, 31) : n;
        }

        static string Col(int i) { var s = ""; i++; while (i > 0) { int m = (i - 1) % 26; s = (char)('A' + m) + s; i = (i - 1) / 26; } return s; }
        static string X(string s) { return SecurityElement.Escape(s ?? "") ?? ""; }

        public void Save(string path)
        {
            if (File.Exists(path)) File.Delete(path);
            using (var fs = new FileStream(path, FileMode.CreateNew))
            using (var z = new ZipArchive(fs, ZipArchiveMode.Create))
            {
                Put(z, "[Content_Types].xml", "<?xml version=\"1.0\" encoding=\"UTF-8\" standalone=\"yes\"?><Types xmlns=\"http://schemas.openxmlformats.org/package/2006/content-types\"><Default Extension=\"rels\" ContentType=\"application/vnd.openxmlformats-package.relationships+xml\"/><Default Extension=\"xml\" ContentType=\"application/xml\"/><Override PartName=\"/xl/workbook.xml\" ContentType=\"application/vnd.openxmlformats-officedocument.spreadsheetml.sheet.main+xml\"/><Override PartName=\"/xl/styles.xml\" ContentType=\"application/vnd.openxmlformats-officedocument.spreadsheetml.styles+xml\"/>"
                    + string.Concat(Sheets.Select((s, i) => "<Override PartName=\"/xl/worksheets/sheet" + (i + 1) + ".xml\" ContentType=\"application/vnd.openxmlformats-officedocument.spreadsheetml.worksheet+xml\"/>")) + "</Types>");
                Put(z, "_rels/.rels", "<?xml version=\"1.0\" encoding=\"UTF-8\" standalone=\"yes\"?><Relationships xmlns=\"http://schemas.openxmlformats.org/package/2006/relationships\"><Relationship Id=\"rId1\" Type=\"http://schemas.openxmlformats.org/officeDocument/2006/relationships/officeDocument\" Target=\"xl/workbook.xml\"/></Relationships>");
                Put(z, "xl/workbook.xml", "<?xml version=\"1.0\" encoding=\"UTF-8\" standalone=\"yes\"?><workbook xmlns=\"http://schemas.openxmlformats.org/spreadsheetml/2006/main\" xmlns:r=\"http://schemas.openxmlformats.org/officeDocument/2006/relationships\"><sheets>"
                    + string.Concat(Sheets.Select((s, i) => "<sheet name=\"" + X(s.Name) + "\" sheetId=\"" + (i + 1) + "\" r:id=\"rId" + (i + 1) + "\"/>")) + "</sheets></workbook>");
                Put(z, "xl/_rels/workbook.xml.rels", "<?xml version=\"1.0\" encoding=\"UTF-8\" standalone=\"yes\"?><Relationships xmlns=\"http://schemas.openxmlformats.org/package/2006/relationships\">"
                    + string.Concat(Sheets.Select((s, i) => "<Relationship Id=\"rId" + (i + 1) + "\" Type=\"http://schemas.openxmlformats.org/officeDocument/2006/relationships/worksheet\" Target=\"worksheets/sheet" + (i + 1) + ".xml\"/>"))
                    + "<Relationship Id=\"rIdS\" Type=\"http://schemas.openxmlformats.org/officeDocument/2006/relationships/styles\" Target=\"styles.xml\"/></Relationships>");
                // styles: 0 normal, 1 header (bold, gold fill), 2 number 0.00, 3 total row (bold 0.00)
                Put(z, "xl/styles.xml", "<?xml version=\"1.0\" encoding=\"UTF-8\" standalone=\"yes\"?><styleSheet xmlns=\"http://schemas.openxmlformats.org/spreadsheetml/2006/main\">"
                    + "<numFmts count=\"1\"><numFmt numFmtId=\"164\" formatCode=\"#,##0.00\"/></numFmts>"
                    + "<fonts count=\"2\"><font><sz val=\"11\"/><name val=\"Calibri\"/></font><font><b/><sz val=\"11\"/><color rgb=\"FFFFFFFF\"/><name val=\"Calibri\"/></font></fonts>"
                    + "<fills count=\"4\"><fill><patternFill patternType=\"none\"/></fill><fill><patternFill patternType=\"gray125\"/></fill><fill><patternFill patternType=\"solid\"><fgColor rgb=\"FFC88A2E\"/></patternFill></fill><fill><patternFill patternType=\"solid\"><fgColor rgb=\"FFF3E6D0\"/></patternFill></fill></fills>"
                    + "<borders count=\"2\"><border/><border><left style=\"thin\"><color rgb=\"FFD0D0D0\"/></left><right style=\"thin\"><color rgb=\"FFD0D0D0\"/></right><top style=\"thin\"><color rgb=\"FFD0D0D0\"/></top><bottom style=\"thin\"><color rgb=\"FFD0D0D0\"/></bottom></border></borders>"
                    + "<cellStyleXfs count=\"1\"><xf/></cellStyleXfs><cellXfs count=\"4\">"
                    + "<xf borderId=\"1\" applyBorder=\"1\"/>"
                    + "<xf fontId=\"1\" fillId=\"2\" borderId=\"1\" applyFont=\"1\" applyFill=\"1\" applyBorder=\"1\"><alignment horizontal=\"center\" vertical=\"center\" wrapText=\"1\"/></xf>"
                    + "<xf numFmtId=\"164\" borderId=\"1\" applyNumberFormat=\"1\" applyBorder=\"1\"/>"
                    + "<xf numFmtId=\"164\" fontId=\"0\" fillId=\"3\" borderId=\"1\" applyNumberFormat=\"1\" applyFill=\"1\" applyBorder=\"1\"/>"
                    + "</cellXfs></styleSheet>");
                for (int i = 0; i < Sheets.Count; i++) Put(z, "xl/worksheets/sheet" + (i + 1) + ".xml", SheetXml(Sheets[i]));
            }
        }

        string SheetXml(Sheet s)
        {
            int cols = s.Rows.Count == 0 ? 1 : s.Rows.Max(r => r.Length);
            var w = new double[cols];
            foreach (var r in s.Rows)
                for (int c = 0; c < r.Length; c++)
                {
                    var len = r[c] == null ? 0 : Convert.ToString(r[c], CultureInfo.InvariantCulture).Length;
                    w[c] = Math.Max(w[c], Math.Min(60, len + 3));
                }
            var sb = new StringBuilder("<?xml version=\"1.0\" encoding=\"UTF-8\" standalone=\"yes\"?><worksheet xmlns=\"http://schemas.openxmlformats.org/spreadsheetml/2006/main\">");
            sb.Append("<sheetViews><sheetView workbookViewId=\"0\"" + (s.Rtl ? " rightToLeft=\"1\"" : "") + "><pane ySplit=\"1\" topLeftCell=\"A2\" activePane=\"bottomLeft\" state=\"frozen\"/></sheetView></sheetViews>");
            sb.Append("<cols>");
            for (int c = 0; c < cols; c++) sb.Append("<col min=\"" + (c + 1) + "\" max=\"" + (c + 1) + "\" width=\"" + Math.Max(10, w[c]).ToString(CultureInfo.InvariantCulture) + "\" customWidth=\"1\"/>");
            sb.Append("</cols><sheetData>");
            int rn = 0;
            foreach (var r in s.Rows)
            {
                rn++;
                sb.Append("<row r=\"" + rn + "\">");
                for (int c = 0; c < r.Length; c++) Cell(sb, Col(c) + rn, r[c], rn == 1 ? 1 : 0);
                sb.Append("</row>");
            }
            if (s.Totals != null && s.Rows.Count > 1)
            {
                rn++;
                sb.Append("<row r=\"" + rn + "\">");
                Cell(sb, "A" + rn, "الإجمالي", 3);
                foreach (var c in s.Totals)
                    sb.Append("<c r=\"" + Col(c) + rn + "\" s=\"3\"><f>SUM(" + Col(c) + "2:" + Col(c) + (rn - 1) + ")</f></c>");
                sb.Append("</row>");
            }
            sb.Append("</sheetData><autoFilter ref=\"A1:" + Col(cols - 1) + Math.Max(1, s.Rows.Count) + "\"/></worksheet>");
            return sb.ToString();
        }

        static void Cell(StringBuilder sb, string r, object v, int style)
        {
            if (v == null) { sb.Append("<c r=\"" + r + "\" s=\"" + style + "\"/>"); return; }
            if (v is double || v is int || v is long || v is float || v is decimal)
            {
                double d = Convert.ToDouble(v, CultureInfo.InvariantCulture);
                sb.Append("<c r=\"" + r + "\" s=\"" + (style == 0 ? (v is int || v is long ? 0 : 2) : style) + "\"><v>" + d.ToString("R", CultureInfo.InvariantCulture) + "</v></c>");
                return;
            }
            sb.Append("<c r=\"" + r + "\" s=\"" + style + "\" t=\"inlineStr\"><is><t xml:space=\"preserve\">" + X(Convert.ToString(v, CultureInfo.InvariantCulture)) + "</t></is></c>");
        }

        static void Put(ZipArchive z, string name, string text)
        {
            var e = z.CreateEntry(name, CompressionLevel.Optimal);
            using (var s = e.Open())
            {
                var b = new UTF8Encoding(false).GetBytes(text);
                s.Write(b, 0, b.Length);
            }
        }
    }
}
