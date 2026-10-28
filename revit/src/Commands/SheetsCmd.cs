using System;
using System.Collections.Generic;
using System.Linq;
using Autodesk.Revit.Attributes;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using OmarKhamis.Revit.Core;

namespace OmarKhamis.Revit.Commands
{
    [Transaction(TransactionMode.Manual)]
    public class SheetsCmd : IExternalCommand
    {
        static int _mode = 0, _tb = -1, _start = 1, _digits = 2;
        static string _prefix = "A-", _list = "A-101 | المسقط الأفقي - الدور الأرضي\nA-102 | المسقط الأفقي - الدور الأول\nA-201 | الواجهات\nA-301 | القطاعات";

        public Result Execute(ExternalCommandData data, ref string message, ElementSet elements)
        {
            var uidoc = data.Application.ActiveUIDocument;
            if (uidoc == null) return Result.Cancelled;
            var doc = uidoc.Document;

            var tbs = new FilteredElementCollector(doc).OfCategory(BuiltInCategory.OST_TitleBlocks).WhereElementIsElementType().Cast<FamilySymbol>()
                .OrderBy(s => s.Family.Name).ThenBy(s => s.Name).ToList();
            var tbNames = new List<string> { "(بدون إطار)" };
            tbNames.AddRange(tbs.Select(s => s.Family.Name + " : " + s.Name));

            var onSheets = new HashSet<long>(new FilteredElementCollector(doc).OfClass(typeof(Viewport)).Cast<Viewport>().Select(v => Compat.IdValue(v.ViewId)));
            var free = new FilteredElementCollector(doc).OfClass(typeof(View)).Cast<View>()
                .Where(v => !v.IsTemplate && Placeable(v) && !onSheets.Contains(Compat.IdValue(v.Id)))
                .OrderBy(v => Kind(v)).ThenBy(v => v.Name).ToList();

            var dlg = new Dlg("شيتات بالجملة", "اعمل شيتات كتير مرة واحدة.")
                .Choice("mode", "الطريقة", new[] { "من قايمة أرقام وأسماء", "شيت لكل فيو مش متحط على شيت" }, _mode)
                .Choice("tb", "الإطار (Title block)", tbNames, _tb < 0 ? (tbNames.Count > 1 ? 1 : 0) : Math.Min(_tb, tbNames.Count - 1))
                .Multiline("list", "القايمة (رقم | اسم)\nكل شيت في سطر", _list, 140)
                .Txt("prefix", "بادئة الرقم (لطريقة الفيوز)", _prefix)
                .Number("start", "أول رقم", _start, 0, 99999)
                .Number("digits", "عدد الخانات", _digits, 1, 5)
                .Checks("views", "الفيوز (لطريقة الفيوز)", free.Select(v => Kind(v) + " — " + v.Name), false, 170)
                .Note("في طريقة الفيوز كل فيو بيتحط في نص الشيت، واسم الشيت = اسم الفيو.");
            if (!dlg.Ask()) return Result.Cancelled;
            _mode = dlg.Idx("mode"); _tb = dlg.Idx("tb"); _list = dlg.S("list"); _prefix = dlg.S("prefix"); _start = dlg.I("start"); _digits = dlg.I("digits");
            ElementId tbId = _tb <= 0 ? ElementId.InvalidElementId : tbs[_tb - 1].Id;

            var existing = new HashSet<string>(new FilteredElementCollector(doc).OfClass(typeof(ViewSheet)).Cast<ViewSheet>().Select(s => s.SheetNumber), StringComparer.OrdinalIgnoreCase);
            int made = 0, placed = 0, skipped = 0;
            using (var t = new Transaction(doc, "شيتات بالجملة — م. عمر خميس"))
            {
                t.Start();
                if (tbId != ElementId.InvalidElementId) { var sym = (FamilySymbol)doc.GetElement(tbId); if (!sym.IsActive) sym.Activate(); }
                if (_mode == 0)
                {
                    foreach (var raw in _list.Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries))
                    {
                        var parts = raw.Split(new[] { '|', '\t' }, 2);
                        var num = parts[0].Trim(); var name = parts.Length > 1 ? parts[1].Trim() : "";
                        if (num.Length == 0) continue;
                        if (existing.Contains(num)) { skipped++; continue; }
                        var s = ViewSheet.Create(doc, tbId);
                        s.SheetNumber = num; if (name.Length > 0) s.Name = name;
                        existing.Add(num); made++;
                    }
                }
                else
                {
                    int n = _start;
                    foreach (int i in dlg.Picked("views"))
                    {
                        var v = free[i];
                        string num;
                        do { num = _prefix + n.ToString().PadLeft(_digits, '0'); n++; } while (existing.Contains(num));
                        var s = ViewSheet.Create(doc, tbId);
                        s.SheetNumber = num; s.Name = v.Name; existing.Add(num); made++;
                        doc.Regenerate();
                        var c = Center(doc, s);
                        if (Viewport.CanAddViewToSheet(doc, s.Id, v.Id)) { Viewport.Create(doc, s.Id, v.Id, c); placed++; }
                    }
                }
                t.Commit();
            }
            Dlg.Info("تم ✓", "اتعمل " + made + " شيت" + (placed > 0 ? "، واتحط عليهم " + placed + " فيو" : "") + (skipped > 0 ? "\nاتساب " + skipped + " رقم لأنه موجود قبل كدا" : "") + ".");
            return Result.Succeeded;
        }

        static XYZ Center(Document doc, ViewSheet s)
        {
            var tb = new FilteredElementCollector(doc, s.Id).OfCategory(BuiltInCategory.OST_TitleBlocks).FirstElement();
            var bb = tb == null ? null : tb.get_BoundingBox(s);
            if (bb != null) return new XYZ((bb.Min.X + bb.Max.X) / 2 - Compat.FromMm(40), (bb.Min.Y + bb.Max.Y) / 2, 0);
            var o = s.Outline;
            return new XYZ((o.Min.U + o.Max.U) / 2, (o.Min.V + o.Max.V) / 2, 0);
        }

        public static bool Placeable(View v)
        {
            switch (v.ViewType)
            {
                case ViewType.FloorPlan: case ViewType.CeilingPlan: case ViewType.Elevation: case ViewType.Section:
                case ViewType.ThreeD: case ViewType.DraftingView: case ViewType.Detail: case ViewType.AreaPlan: case ViewType.EngineeringPlan:
                case ViewType.Rendering: case ViewType.Walkthrough:
                    return true;
            }
            return false;
        }

        public static string Kind(View v)
        {
            switch (v.ViewType)
            {
                case ViewType.FloorPlan: return "مسقط";
                case ViewType.CeilingPlan: return "سقف";
                case ViewType.Elevation: return "واجهة";
                case ViewType.Section: return "قطاع";
                case ViewType.Detail: return "تفصيلة";
                case ViewType.ThreeD: return "3D";
                case ViewType.DraftingView: return "رسم";
                case ViewType.AreaPlan: return "مساحات";
                case ViewType.EngineeringPlan: return "إنشائي";
                case ViewType.Schedule: return "جدول";
                case ViewType.Legend: return "مفتاح";
                case ViewType.DrawingSheet: return "شيت";
            }
            return v.ViewType.ToString();
        }
    }
}
