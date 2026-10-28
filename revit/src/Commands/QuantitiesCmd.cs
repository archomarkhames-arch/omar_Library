using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using Autodesk.Revit.Attributes;
using Autodesk.Revit.DB;
using Autodesk.Revit.DB.Architecture;
using Autodesk.Revit.UI;
using OmarKhamis.Revit.Core;

namespace OmarKhamis.Revit.Commands
{
    [Transaction(TransactionMode.ReadOnly)]
    public class QuantitiesCmd : IExternalCommand
    {
        class Cat { public string Name; public BuiltInCategory Bic; public bool Len, Area, Vol; }
        static readonly Cat[] Cats =
        {
            new Cat { Name = "الحوائط", Bic = BuiltInCategory.OST_Walls, Len = true, Area = true, Vol = true },
            new Cat { Name = "الأرضيات", Bic = BuiltInCategory.OST_Floors, Area = true, Vol = true },
            new Cat { Name = "الأسقف", Bic = BuiltInCategory.OST_Ceilings, Area = true },
            new Cat { Name = "الأسقف الخرسانية والبلاطات", Bic = BuiltInCategory.OST_Roofs, Area = true, Vol = true },
            new Cat { Name = "الأعمدة", Bic = BuiltInCategory.OST_StructuralColumns, Vol = true },
            new Cat { Name = "الكمرات", Bic = BuiltInCategory.OST_StructuralFraming, Len = true, Vol = true },
            new Cat { Name = "الأساسات", Bic = BuiltInCategory.OST_StructuralFoundation, Vol = true },
            new Cat { Name = "الأبواب", Bic = BuiltInCategory.OST_Doors },
            new Cat { Name = "الشبابيك", Bic = BuiltInCategory.OST_Windows },
            new Cat { Name = "الأثاث", Bic = BuiltInCategory.OST_Furniture },
            new Cat { Name = "الدواليب والمطابخ", Bic = BuiltInCategory.OST_Casework },
            new Cat { Name = "الأجهزة الصحية", Bic = BuiltInCategory.OST_PlumbingFixtures },
            new Cat { Name = "الإضاءة", Bic = BuiltInCategory.OST_LightingFixtures },
            new Cat { Name = "الدرابزين", Bic = BuiltInCategory.OST_StairsRailing, Len = true },
            new Cat { Name = "السلالم", Bic = BuiltInCategory.OST_Stairs },
            new Cat { Name = "النماذج العامة", Bic = BuiltInCategory.OST_GenericModel },
        };
        static bool _byLevel = true, _rooms = true, _open = true;

        public Result Execute(ExternalCommandData data, ref string message, ElementSet elements)
        {
            var uidoc = data.Application.ActiveUIDocument;
            if (uidoc == null) return Result.Cancelled;
            var doc = uidoc.Document;

            var dlg = new Dlg("حصر الكميات على Excel", "كل فئة في شيت لوحدها، مجمّعة حسب النوع، بالمتر والمتر المربع والمكعب، وفي الآخر صف إجمالي.")
                .Checks("cats", "الفئات", Cats.Select(c => c.Name), true, 220)
                .Check("rooms", "شيت الغرف (المساحة والمحيط والارتفاع والتشطيبات)", _rooms)
                .Check("lvl", "افصل الكميات حسب الدور", _byLevel)
                .Check("open", "افتح الملف بعد الحفظ", _open);
            if (!dlg.Ask()) return Result.Cancelled;
            _rooms = dlg.B("rooms"); _byLevel = dlg.B("lvl"); _open = dlg.B("open");

            var sfd = new System.Windows.Forms.SaveFileDialog
            {
                Filter = "Excel (*.xlsx)|*.xlsx",
                FileName = San(string.IsNullOrEmpty(doc.Title) ? "Project" : doc.Title) + " - كميات.xlsx",
                InitialDirectory = Environment.GetFolderPath(Environment.SpecialFolder.Desktop)
            };
            if (sfd.ShowDialog() != System.Windows.Forms.DialogResult.OK) return Result.Cancelled;

            var x = new Xlsx();
            var summary = x.Add("ملخص", "الفئة", "العدد", "الطول م", "المساحة م²", "الحجم م³");
            summary.Totals = new[] { 1 };

            foreach (int i in dlg.Picked("cats"))
            {
                var c = Cats[i];
                var els = new FilteredElementCollector(doc).OfCategory(c.Bic).WhereElementIsNotElementType().ToElements()
                    .Where(e => e.Category != null && !(e is ElementType) && (e.GetTypeId() != ElementId.InvalidElementId || e is Wall || e is Floor)).ToList();
                if (els.Count == 0) continue;
                var head = new List<string>();
                if (_byLevel) head.Add("الدور");
                head.Add("النوع"); head.Add("العدد");
                if (c.Len) head.Add("الطول م");
                if (c.Area) head.Add("المساحة م²");
                if (c.Vol) head.Add("الحجم م³");
                var sh = x.Add(c.Name, head.ToArray());
                var groups = els.GroupBy(e => (_byLevel ? LevelName(doc, e) + "|" : "") + Compat.TypeName(doc, e)).OrderBy(g => g.Key);
                double tl = 0, ta = 0, tv = 0;
                foreach (var g in groups)
                {
                    var row = new List<object>();
                    var k = g.Key.Split('|');
                    if (_byLevel) { row.Add(k[0]); row.Add(k[1]); } else row.Add(k[0]);
                    row.Add(g.Count());
                    double l = g.Sum(e => Length(e)) * 0.3048, a = g.Sum(e => Compat.Dbl(e, BuiltInParameter.HOST_AREA_COMPUTED)) * Compat.M2, v = g.Sum(e => Compat.Dbl(e, BuiltInParameter.HOST_VOLUME_COMPUTED)) * Compat.M3;
                    if (c.Len) row.Add(Math.Round(l, 2));
                    if (c.Area) row.Add(Math.Round(a, 2));
                    if (c.Vol) row.Add(Math.Round(v, 3));
                    tl += l; ta += a; tv += v;
                    sh.Rows.Add(row.ToArray());
                }
                int first = _byLevel ? 2 : 1;
                var tot = new List<int> { first };
                for (int j = 1; j <= (c.Len ? 1 : 0) + (c.Area ? 1 : 0) + (c.Vol ? 1 : 0); j++) tot.Add(first + j);
                sh.Totals = tot.ToArray();
                summary.Rows.Add(new object[] { c.Name, els.Count, c.Len ? (object)Math.Round(tl, 2) : null, c.Area ? (object)Math.Round(ta, 2) : null, c.Vol ? (object)Math.Round(tv, 3) : null });
            }

            if (_rooms)
            {
                var rooms = new FilteredElementCollector(doc).OfCategory(BuiltInCategory.OST_Rooms).WhereElementIsNotElementType().Cast<Room>().Where(r => r.Area > 0)
                    .OrderBy(r => r.Level == null ? 0 : r.Level.Elevation).ThenBy(r => r.Number).ToList();
                if (rooms.Count > 0)
                {
                    var sh = x.Add("الغرف", "الدور", "الرقم", "الاسم", "المساحة م²", "المحيط م", "الارتفاع م", "الحجم م³", "تشطيب الأرضية", "تشطيب الحوائط", "تشطيب السقف", "القسم");
                    foreach (var r in rooms)
                        sh.Rows.Add(new object[]
                        {
                            r.Level == null ? "" : r.Level.Name, r.Number, Compat.Str(r, BuiltInParameter.ROOM_NAME),
                            Math.Round(r.Area * Compat.M2, 2), Math.Round(r.Perimeter * 0.3048, 2), Math.Round(r.UnboundedHeight * 0.3048, 2), Math.Round(r.Volume * Compat.M3, 2),
                            Compat.Str(r, BuiltInParameter.ROOM_FINISH_FLOOR), Compat.Str(r, BuiltInParameter.ROOM_FINISH_WALL), Compat.Str(r, BuiltInParameter.ROOM_FINISH_CEILING), Compat.Str(r, BuiltInParameter.ROOM_DEPARTMENT)
                        });
                    sh.Totals = new[] { 3, 4, 6 };
                    summary.Rows.Add(new object[] { "الغرف", rooms.Count, null, Math.Round(rooms.Sum(r => r.Area) * Compat.M2, 2), Math.Round(rooms.Sum(r => r.Volume) * Compat.M3, 2) });
                }
            }

            try { x.Save(sfd.FileName); }
            catch (IOException) { Dlg.Info("الملف مفتوح", "اقفل ملف الإكسل القديم وجرب تاني."); return Result.Cancelled; }
            if (_open) { try { Process.Start(new ProcessStartInfo(sfd.FileName) { UseShellExecute = true }); } catch { } }
            else Dlg.Info("تم ✓", "اتحفظ في:\n" + sfd.FileName);
            return Result.Succeeded;
        }

        static double Length(Element e)
        {
            var p = e.get_Parameter(BuiltInParameter.CURVE_ELEM_LENGTH);
            if (p != null && p.HasValue) return p.AsDouble();
            var lc = e.Location as LocationCurve;
            return lc != null ? lc.Curve.Length : 0;
        }

        static string LevelName(Document doc, Element e)
        {
            var id = e.LevelId;
            if (id == null || id == ElementId.InvalidElementId)
            {
                var p = e.get_Parameter(BuiltInParameter.FAMILY_LEVEL_PARAM) ?? e.get_Parameter(BuiltInParameter.SCHEDULE_LEVEL_PARAM) ?? e.get_Parameter(BuiltInParameter.WALL_BASE_CONSTRAINT);
                if (p != null && p.StorageType == StorageType.ElementId) id = p.AsElementId();
            }
            var l = id == null ? null : doc.GetElement(id) as Level;
            return l == null ? "-" : l.Name;
        }

        static string San(string s) { foreach (var c in Path.GetInvalidFileNameChars()) s = s.Replace(c, '_'); return s; }
    }
}
