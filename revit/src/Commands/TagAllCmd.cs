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
    public class TagAllCmd : IExternalCommand
    {
        class Cat { public string Name; public BuiltInCategory Bic, TagBic; }
        static readonly Cat[] Cats =
        {
            new Cat { Name = "الغرف", Bic = BuiltInCategory.OST_Rooms, TagBic = BuiltInCategory.OST_RoomTags },
            new Cat { Name = "الأبواب", Bic = BuiltInCategory.OST_Doors, TagBic = BuiltInCategory.OST_DoorTags },
            new Cat { Name = "الشبابيك", Bic = BuiltInCategory.OST_Windows, TagBic = BuiltInCategory.OST_WindowTags },
            new Cat { Name = "الأثاث", Bic = BuiltInCategory.OST_Furniture, TagBic = BuiltInCategory.OST_FurnitureTags },
            new Cat { Name = "الدواليب والمطابخ (Casework)", Bic = BuiltInCategory.OST_Casework, TagBic = BuiltInCategory.OST_CaseworkTags },
            new Cat { Name = "الأجهزة الصحية", Bic = BuiltInCategory.OST_PlumbingFixtures, TagBic = BuiltInCategory.OST_PlumbingFixtureTags },
            new Cat { Name = "الإضاءة", Bic = BuiltInCategory.OST_LightingFixtures, TagBic = BuiltInCategory.OST_LightingFixtureTags },
            new Cat { Name = "الحوائط", Bic = BuiltInCategory.OST_Walls, TagBic = BuiltInCategory.OST_WallTags },
            new Cat { Name = "الأعمدة الإنشائية", Bic = BuiltInCategory.OST_StructuralColumns, TagBic = BuiltInCategory.OST_StructuralColumnTags },
            new Cat { Name = "النماذج العامة (Generic)", Bic = BuiltInCategory.OST_GenericModel, TagBic = BuiltInCategory.OST_GenericModelTags },
        };
        static List<int> _sel = new List<int> { 0, 1, 2 };
        static bool _leader = false;

        public Result Execute(ExternalCommandData data, ref string message, ElementSet elements)
        {
            var uidoc = data.Application.ActiveUIDocument;
            if (uidoc == null) return Result.Cancelled;
            var doc = uidoc.Document;
            var view = uidoc.ActiveView;
            if (!(view is ViewPlan || view is ViewSection || (view is View3D && ((View3D)view).IsLocked)))
            {
                Dlg.Info("الفيو ده مينفعش", "افتح مسقط أو قطاع أو واجهة (أو 3D مقفول) وجرب تاني.");
                return Result.Cancelled;
            }

            var dlg = new Dlg("تاجات بضغطة", "هيتحط تاج لكل عنصر ظاهر في الفيو الحالي ملهوش تاج.")
                .Checks("cats", "الفئات", Cats.Select(c => c.Name), false, 200)
                .Check("leader", "بخط إشارة (Leader)", _leader);
            var cl = (System.Windows.Forms.CheckedListBox)dlg.Ctl("cats");
            foreach (var i in _sel) if (i < cl.Items.Count) cl.SetItemChecked(i, true);
            if (!dlg.Ask()) return Result.Cancelled;
            _sel = dlg.Picked("cats"); _leader = dlg.B("leader");

            var tagged = Compat.TaggedIn(doc, view);
            var report = new List<string>();
            int total = 0;
            using (var t = new Transaction(doc, "تاجات — م. عمر خميس"))
            {
                t.Start();
                var fo = t.GetFailureHandlingOptions(); fo.SetFailuresPreprocessor(new QuietWarnings()); t.SetFailureHandlingOptions(fo);
                foreach (var i in _sel)
                {
                    var c = Cats[i];
                    var tagType = new FilteredElementCollector(doc).OfCategory(c.TagBic).WhereElementIsElementType().FirstElementId();
                    if (tagType == null || tagType == ElementId.InvalidElementId) { report.Add(c.Name + ": مفيش تاج متحمّل للفئة دي — حمّل Tag family الأول"); continue; }
                    int n = 0;
                    var els = new FilteredElementCollector(doc, view.Id).OfCategory(c.Bic).WhereElementIsNotElementType().ToElements();
                    foreach (var e in els)
                    {
                        if (tagged.Contains(Compat.IdValue(e.Id))) continue;
                        try
                        {
                            if (c.Bic == BuiltInCategory.OST_Rooms)
                            {
                                var r = e as Room;
                                if (r == null || r.Area <= 0 || !(r.Location is LocationPoint) || !(view is ViewPlan)) continue;
                                var p = ((LocationPoint)r.Location).Point;
                                var rt = doc.Create.NewRoomTag(new LinkElementId(r.Id), new UV(p.X, p.Y), view.Id);
                                if (rt != null) n++;
                            }
                            else
                            {
                                var bb = e.get_BoundingBox(view);
                                if (bb == null) continue;
                                var at = (bb.Min + bb.Max) / 2;
                                if (Compat.Tag(doc, view, e, at, _leader, null) != null) n++;
                            }
                        }
                        catch { }
                    }
                    total += n;
                    report.Add(c.Name + ": " + n);
                }
                t.Commit();
            }
            Dlg.Info("تم ✓ — " + total + " تاج", string.Join("\n", report));
            return Result.Succeeded;
        }
    }
}
