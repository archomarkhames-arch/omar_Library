using System;
using System.Collections.Generic;
using System.Linq;
using Autodesk.Revit.Attributes;
using Autodesk.Revit.DB;
using Autodesk.Revit.DB.Architecture;
using Autodesk.Revit.UI;
using Autodesk.Revit.UI.Selection;
using OmarKhamis.Revit.Core;

namespace OmarKhamis.Revit.Commands
{
    public class RoomFilter : ISelectionFilter
    {
        public bool AllowElement(Element e) { return e is Room; }
        public bool AllowReference(Reference r, XYZ p) { return false; }
    }

    [Transaction(TransactionMode.Manual)]
    public class RoomNumberCmd : IExternalCommand
    {
        static string _prefix = "", _suffix = "";
        static int _start = 1, _step = 1, _digits = 2, _mode = 0, _order = 0;
        static bool _levelPrefix = false;

        public Result Execute(ExternalCommandData data, ref string message, ElementSet elements)
        {
            var uidoc = data.Application.ActiveUIDocument;
            if (uidoc == null) return Result.Cancelled;
            var doc = uidoc.Document;
            var view = uidoc.ActiveView;

            var dlg = new Dlg("ترقيم الغرف", "اختار طريقة الترقيم. الأرقام بتتكتب في «رقم الغرفة» (Number).")
                .Choice("mode", "الطريقة", new[] { "اضغط على الغرف واحدة واحدة بالترتيب", "تلقائي لكل غرف الفيو الحالي" }, _mode)
                .Choice("order", "الترتيب التلقائي", new[] { "من فوق لتحت — من اليمين للشمال", "من فوق لتحت — من الشمال لليمين", "من الشمال لليمين — عمود عمود", "حسب المساحة (الأكبر الأول)" }, _order)
                .Txt("prefix", "بادئة (قبل الرقم)", _prefix)
                .Check("lvl", "ابدأ بكود الدور (مثال: 1-01 للدور الأول)", _levelPrefix)
                .Number("start", "أول رقم", _start, 0, 99999)
                .Number("step", "الزيادة", _step, 1, 1000)
                .Number("digits", "عدد الخانات", _digits, 1, 6)
                .Txt("suffix", "لاحقة (بعد الرقم)", _suffix)
                .Note("في طريقة الضغط: اضغط على الغرف بالترتيب، ولما تخلص دوس Esc. لو الغرفة مش بتتحدد، فعّل Interior Fill أو Reference من Visibility/Graphics.");
            if (!dlg.Ask()) return Result.Cancelled;
            _mode = dlg.Idx("mode"); _order = dlg.Idx("order"); _prefix = dlg.S("prefix"); _suffix = dlg.S("suffix");
            _start = dlg.I("start"); _step = dlg.I("step"); _digits = dlg.I("digits"); _levelPrefix = dlg.B("lvl");

            int n = _start, done = 0;
            Func<Room, string> make = r =>
            {
                string lv = "";
                if (_levelPrefix && r.Level != null)
                {
                    var levels = new FilteredElementCollector(doc).OfClass(typeof(Level)).Cast<Level>().OrderBy(l => l.Elevation).ToList();
                    int idx = levels.FindIndex(l => Compat.SameId(l.Id, r.Level.Id));
                    lv = (idx < 0 ? 0 : idx) + "-";
                }
                var s = _prefix + lv + n.ToString().PadLeft(_digits, '0') + _suffix;
                n += _step;
                return s;
            };

            using (var tg = new TransactionGroup(doc, "ترقيم الغرف — م. عمر خميس"))
            {
                tg.Start();
                if (_mode == 0)
                {
                    while (true)
                    {
                        Reference r;
                        try { r = uidoc.Selection.PickObject(ObjectType.Element, new RoomFilter(), "اضغط على الغرفة رقم " + (_prefix + n.ToString().PadLeft(_digits, '0') + _suffix) + " — Esc للإنهاء"); }
                        catch (Autodesk.Revit.Exceptions.OperationCanceledException) { break; }
                        var room = doc.GetElement(r) as Room;
                        if (room == null) continue;
                        using (var t = new Transaction(doc, "رقم غرفة"))
                        {
                            t.Start();
                            Swap(doc, room, make(room));
                            t.Commit();
                        }
                        done++;
                    }
                }
                else
                {
                    var rooms = new FilteredElementCollector(doc, view.Id).OfCategory(BuiltInCategory.OST_Rooms).WhereElementIsNotElementType()
                        .Cast<Room>().Where(r => r.Area > 0 && r.Location is LocationPoint).ToList();
                    if (rooms.Count == 0) { tg.RollBack(); Dlg.Info("مفيش غرف", "مفيش غرف متحطوطة في الفيو الحالي. افتح فيو بلان فيه غرف."); return Result.Cancelled; }
                    rooms = Sort(rooms, _order);
                    using (var t = new Transaction(doc, "ترقيم الغرف"))
                    {
                        t.Start();
                        // temporary numbers first so «رقم مكرر» warnings never appear while renumbering
                        foreach (var r in rooms) Compat.SetStr(r, BuiltInParameter.ROOM_NUMBER, "~" + Guid.NewGuid().ToString("N").Substring(0, 8));
                        foreach (var r in rooms) { Compat.SetStr(r, BuiltInParameter.ROOM_NUMBER, make(r)); done++; }
                        t.Commit();
                    }
                }
                if (done > 0) tg.Assimilate(); else tg.RollBack();
            }
            if (done > 0) Dlg.Info("تم ✓", "اترقّم " + done + " غرفة.");
            return Result.Succeeded;
        }

        /// <summary>Gives the room its new number; if another room already has it, that room takes this room's old number.</summary>
        static void Swap(Document doc, Room room, string num)
        {
            var other = new FilteredElementCollector(doc).OfCategory(BuiltInCategory.OST_Rooms).WhereElementIsNotElementType()
                .Cast<Room>().FirstOrDefault(r => !Compat.SameId(r.Id, room.Id) && r.Number == num);
            var old = room.Number;
            if (other != null) Compat.SetStr(other, BuiltInParameter.ROOM_NUMBER, "~tmp");
            Compat.SetStr(room, BuiltInParameter.ROOM_NUMBER, num);
            if (other != null) Compat.SetStr(other, BuiltInParameter.ROOM_NUMBER, old);
        }

        static List<Room> Sort(List<Room> rooms, int order)
        {
            Func<Room, XYZ> P = r => ((LocationPoint)r.Location).Point;
            if (order == 3) return rooms.OrderByDescending(r => r.Area).ToList();
            double band = Compat.FromMm(2000);
            if (order == 2) return rooms.OrderBy(r => Math.Round(P(r).X / band)).ThenByDescending(r => P(r).Y).ToList();
            var rows = rooms.OrderBy(r => r.Level == null ? 0 : r.Level.Elevation).ThenByDescending(r => Math.Round(P(r).Y / band));
            return order == 0 ? rows.ThenByDescending(r => P(r).X).ToList() : rows.ThenBy(r => P(r).X).ToList();
        }
    }
}
