using System;
using System.Collections.Generic;
using System.Linq;
using Autodesk.Revit.Attributes;
using Autodesk.Revit.DB;
using Autodesk.Revit.DB.Architecture;
using Autodesk.Revit.UI;
using OmarKhamis.Revit.Core;

namespace OmarKhamis.Revit.Commands
{
    [Transaction(TransactionMode.Manual)]
    public class RenameCmd : IExternalCommand
    {
        static int _target = 0;
        static string _find = "", _repl = "", _pre = "", _suf = "";
        static bool _number = false;
        static int _start = 1, _digits = 2;

        class Item { public Element E; public string Label; public Func<string> Get; public Func<string, bool> Set; }

        public Result Execute(ExternalCommandData data, ref string message, ElementSet elements)
        {
            var uidoc = data.Application.ActiveUIDocument;
            if (uidoc == null) return Result.Cancelled;
            var doc = uidoc.Document;

            var targets = new[] { "أسماء الفيوز", "أسماء الشيتات", "أرقام الشيتات", "أسماء الغرف", "أسماء المستويات", "أسماء الأنواع (Types)" };
            var first = new Dlg("إعادة تسمية بالجملة", "اختار إيه اللي هتغيّر اسمه.")
                .Choice("target", "الهدف", targets, _target);
            if (!first.Ask()) return Result.Cancelled;
            _target = first.Idx("target");

            var items = Collect(doc, _target);
            if (items.Count == 0) { Dlg.Info("مفيش عناصر", "مفيش حاجة من النوع ده في المشروع."); return Result.Cancelled; }

            var dlg = new Dlg("إعادة تسمية — " + targets[_target], "حدد العناصر، وبعدين اختار التغيير. ممكن تجمع أكتر من تغيير مع بعض.")
                .Checks("items", "العناصر", items.Select(i => i.Label), true, 220)
                .Txt("find", "ابحث عن", _find)
                .Txt("repl", "واستبدله بـ", _repl)
                .Txt("pre", "أضف في الأول", _pre)
                .Txt("suf", "أضف في الآخر", _suf)
                .Check("num", "رقّم بالترتيب (الرقم بيتحط بعد الإضافة في الأول)", _number)
                .Number("start", "أول رقم", _start, 0, 99999)
                .Number("digits", "عدد الخانات", _digits, 1, 5);
            if (!dlg.Ask()) return Result.Cancelled;
            _find = dlg.S("find"); _repl = dlg.S("repl"); _pre = dlg.S("pre"); _suf = dlg.S("suf"); _number = dlg.B("num"); _start = dlg.I("start"); _digits = dlg.I("digits");

            int ok = 0, fail = 0, n = _start;
            var fails = new List<string>();
            using (var t = new Transaction(doc, "إعادة تسمية — م. عمر خميس"))
            {
                t.Start();
                foreach (int i in dlg.Picked("items"))
                {
                    var it = items[i];
                    var old = it.Get() ?? "";
                    var name = _find.Length > 0 ? old.Replace(_find, _repl) : old;
                    name = _pre + (_number ? n.ToString().PadLeft(_digits, '0') + " " : "") + name + _suf;
                    n++;
                    if (name == old) continue;
                    try { if (it.Set(name)) ok++; else { fail++; } }
                    catch (Exception ex) { fail++; if (fails.Count < 6) fails.Add(old + " ← " + ex.Message); }
                }
                t.Commit();
            }
            Dlg.Info("تم ✓", "اتغيّر " + ok + (fail > 0 ? "\nمتغيرش " + fail + " (غالباً الاسم الجديد مستخدم قبل كدا)" + (fails.Count > 0 ? ":\n" + string.Join("\n", fails) : "") : ""));
            return Result.Succeeded;
        }

        static List<Item> Collect(Document doc, int target)
        {
            var res = new List<Item>();
            switch (target)
            {
                case 0:
                    foreach (var v in new FilteredElementCollector(doc).OfClass(typeof(View)).Cast<View>().Where(v => !v.IsTemplate && !(v is ViewSheet) && v.ViewType != ViewType.ProjectBrowser && v.ViewType != ViewType.SystemBrowser && v.ViewType != ViewType.Internal && v.ViewType != ViewType.Undefined).OrderBy(v => v.ViewType.ToString()).ThenBy(v => v.Name))
                    { var x = v; res.Add(new Item { E = x, Label = SheetsCmd.Kind(x) + " — " + x.Name, Get = () => x.Name, Set = s => { x.Name = s; return true; } }); }
                    break;
                case 1:
                case 2:
                    foreach (var s0 in new FilteredElementCollector(doc).OfClass(typeof(ViewSheet)).Cast<ViewSheet>().OrderBy(s => s.SheetNumber))
                    {
                        var x = s0;
                        if (target == 1) res.Add(new Item { E = x, Label = x.SheetNumber + " — " + x.Name, Get = () => x.Name, Set = s => { x.Name = s; return true; } });
                        else res.Add(new Item { E = x, Label = x.SheetNumber + " — " + x.Name, Get = () => x.SheetNumber, Set = s => { x.SheetNumber = s; return true; } });
                    }
                    break;
                case 3:
                    foreach (var r0 in new FilteredElementCollector(doc).OfCategory(BuiltInCategory.OST_Rooms).WhereElementIsNotElementType().Cast<Room>().OrderBy(r => r.Number))
                    { var x = r0; res.Add(new Item { E = x, Label = x.Number + " — " + Compat.Str(x, BuiltInParameter.ROOM_NAME), Get = () => Compat.Str(x, BuiltInParameter.ROOM_NAME), Set = s => Compat.SetStr(x, BuiltInParameter.ROOM_NAME, s) }); }
                    break;
                case 4:
                    foreach (var l0 in new FilteredElementCollector(doc).OfClass(typeof(Level)).Cast<Level>().OrderBy(l => l.Elevation))
                    { var x = l0; res.Add(new Item { E = x, Label = x.Name, Get = () => x.Name, Set = s => { x.Name = s; return true; } }); }
                    break;
                case 5:
                    foreach (var e0 in new FilteredElementCollector(doc).WhereElementIsElementType().Cast<ElementType>()
                        .Where(e => e.Category != null && e.Category.CategoryType == CategoryType.Model && e.CanBeRenamed).OrderBy(e => e.Category.Name).ThenBy(e => e.Name))
                    { var x = e0; res.Add(new Item { E = x, Label = x.Category.Name + " — " + (x.FamilyName ?? "") + " : " + x.Name, Get = () => x.Name, Set = s => { x.Name = s; return true; } }); }
                    break;
            }
            return res;
        }
    }
}
