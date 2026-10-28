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
    /// <summary>Floor finish, ceiling, wall cladding and skirting for every selected room, from its finish boundary.</summary>
    [Transaction(TransactionMode.Manual)]
    public class RoomFinishCmd : IExternalCommand
    {
        static bool _floor = true, _ceil = true, _clad = false, _skirt = true, _roomH = true, _comment = true;
        static double _floorOff = 0, _ceilH = 2800, _cladH = 2800, _skirtH = 100;
        static string _floorT, _ceilT, _cladT, _skirtT;

        public Result Execute(ExternalCommandData data, ref string message, ElementSet elements)
        {
            var uidoc = data.Application.ActiveUIDocument;
            if (uidoc == null) return Result.Cancelled;
            var doc = uidoc.Document;

            // rooms: the selection, or all rooms of the active view
            ICollection<ElementId> selIds = uidoc.Selection.GetElementIds();
            var rooms = selIds.Select(id => doc.GetElement(id)).OfType<Room>().Where(r => r.Area > 0).ToList();
            bool fromSel = rooms.Count > 0;
            if (!fromSel)
                rooms = new FilteredElementCollector(doc, uidoc.ActiveView.Id).OfCategory(BuiltInCategory.OST_Rooms).WhereElementIsNotElementType()
                    .Cast<Room>().Where(r => r.Area > 0).ToList();
            if (rooms.Count == 0)
            {
                Dlg.Info("مفيش غرف", "حدد غرف الأول، أو افتح فيو بلان فيه غرف متحطوطة.");
                return Result.Cancelled;
            }

            var floorTypes = new FilteredElementCollector(doc).OfClass(typeof(FloorType)).Cast<FloorType>().Where(t => !t.IsFoundationSlab).OrderBy(t => t.Name).ToList();
            var ceilTypes = new FilteredElementCollector(doc).OfClass(typeof(CeilingType)).Cast<CeilingType>().OrderBy(t => t.Name).ToList();
            var wallTypes = new FilteredElementCollector(doc).OfClass(typeof(WallType)).Cast<WallType>().Where(t => t.Kind == WallKind.Basic).OrderBy(t => t.Width).ThenBy(t => t.Name).ToList();
            if (floorTypes.Count == 0 && wallTypes.Count == 0) { Dlg.Info("مفيش أنواع", "المشروع مفيهوش أنواع أرضيات أو حوائط."); return Result.Cancelled; }

            Func<IEnumerable<Element>, string, int> pick = (list, remembered) =>
            {
                var names = list.Select(e => e.Name).ToList();
                int i = remembered == null ? -1 : names.IndexOf(remembered);
                return i;
            };
            var wNames = wallTypes.Select(t => t.Name + "  (" + Math.Round(Compat.ToMm(t.Width)) + " مم)").ToList();
            int wThin = wallTypes.FindIndex(t => Compat.ToMm(t.Width) >= 5);

            var dlg = new Dlg("تشطيبات الغرف", (fromSel ? "هيتعمل للغرف المحددة: " : "هيتعمل لكل غرف الفيو الحالي: ") + rooms.Count + " غرفة. اختار الأنواع اللي هتتعمل بيها.")
                .Check("floor", "أرضية تشطيب (بلاط / باركيه…)", _floor)
                .Choice("floorT", "نوع الأرضية", floorTypes.Select(t => t.Name), Math.Max(0, pick(floorTypes.Cast<Element>(), _floorT)))
                .Number("floorOff", "ارتفاع عن المستوى (مم)", (decimal)_floorOff, -2000, 2000);
            if (Compat.CanCreateCeilings)
                dlg.Check("ceil", "سقف معلق / جبس", _ceil)
                   .Choice("ceilT", "نوع السقف", ceilTypes.Select(t => t.Name), Math.Max(0, pick(ceilTypes.Cast<Element>(), _ceilT)))
                   .Number("ceilH", "ارتفاع السقف من المستوى (مم)", (decimal)_ceilH, 0, 20000);
            else
                dlg.Note("الأسقف بتتعمل من ريفيت 2022 وأحدث (ريفيت " + Compat.Version + " مفيهوش أمر لإنشاء الأسقف).", System.Drawing.Color.DarkOrange);
            dlg.Check("clad", "تجليد حوائط (سيراميك / رخام / خشب…)", _clad)
               .Choice("cladT", "نوع التجليد (حائط رفيع)", wNames, Math.Max(0, pick(wallTypes.Cast<Element>(), _cladT) >= 0 ? pick(wallTypes.Cast<Element>(), _cladT) : Math.Max(0, wThin)))
               .Check("roomH", "ارتفاع التجليد = ارتفاع الغرفة", _roomH)
               .Number("cladH", "أو ارتفاع ثابت (مم)", (decimal)_cladH, 50, 20000)
               .Check("skirt", "وزرة (بتتقطع عند الأبواب)", _skirt)
               .Choice("skirtT", "نوع الوزرة (حائط رفيع)", wNames, Math.Max(0, pick(wallTypes.Cast<Element>(), _skirtT) >= 0 ? pick(wallTypes.Cast<Element>(), _skirtT) : Math.Max(0, wThin)))
               .Number("skirtH", "ارتفاع الوزرة (مم)", (decimal)_skirtH, 10, 1000)
               .Check("comment", "اكتب اسم ورقم الغرفة في «Comments» (عشان الجداول)", _comment)
               .Note("التجليد والوزرة بيتعملوا على وش الحائط من جوه الغرفة، ومش بيأثروا على حدود الغرفة. فتحات الأبواب والشبابيك بتتفتح في التجليد أوتوماتيك.");
            if (!dlg.Ask()) return Result.Cancelled;

            _floor = dlg.B("floor") && floorTypes.Count > 0; _floorOff = dlg.D("floorOff");
            if (_floor) _floorT = floorTypes[dlg.Idx("floorT")].Name;
            _ceil = Compat.CanCreateCeilings && dlg.B("ceil") && ceilTypes.Count > 0;
            if (Compat.CanCreateCeilings) { _ceilH = dlg.D("ceilH"); if (ceilTypes.Count > 0) _ceilT = ceilTypes[dlg.Idx("ceilT")].Name; }
            _clad = dlg.B("clad") && wallTypes.Count > 0; _roomH = dlg.B("roomH"); _cladH = dlg.D("cladH");
            _skirt = dlg.B("skirt") && wallTypes.Count > 0; _skirtH = dlg.D("skirtH");
            _comment = dlg.B("comment");
            WallType cladType = wallTypes.Count > 0 ? wallTypes[dlg.Idx("cladT")] : null, skirtType = wallTypes.Count > 0 ? wallTypes[dlg.Idx("skirtT")] : null;
            if (cladType != null) _cladT = cladType.Name;
            if (skirtType != null) _skirtT = skirtType.Name;
            if (!_floor && !_ceil && !_clad && !_skirt) return Result.Cancelled;

            int nf = 0, nc = 0, nw = 0, ns = 0, bad = 0;
            var errors = new List<string>();
            using (var t = new Transaction(doc, "تشطيبات الغرف — م. عمر خميس"))
            {
                t.Start();
                var fo = t.GetFailureHandlingOptions();
                fo.SetFailuresPreprocessor(new QuietWarnings());
                t.SetFailureHandlingOptions(fo);
                foreach (var room in rooms)
                {
                    string tagText = (room.Number + " - " + Compat.Str(room, BuiltInParameter.ROOM_NAME)).Trim(' ', '-');
                    try
                    {
                        var b = Geo.Boundary(room);
                        if (b.Count == 0) { bad++; continue; }
                        var level = room.Level;
                        double z = level.Elevation;
                        if (_floor)
                        {
                            var ft = floorTypes[floorTypes.FindIndex(x => x.Name == _floorT)];
                            var f = Compat.CreateFloor(doc, Geo.Loops(b, z), ft.Id, level.Id);
                            if (f != null)
                            {
                                var p = f.get_Parameter(BuiltInParameter.FLOOR_HEIGHTABOVELEVEL_PARAM);
                                if (p != null && !p.IsReadOnly) p.Set(Compat.FromMm(_floorOff));
                                var rb = f.get_Parameter(BuiltInParameter.WALL_ATTR_ROOM_BOUNDING);
                                if (rb != null && !rb.IsReadOnly) rb.Set(0);
                                if (_comment) Compat.SetStr(f, BuiltInParameter.ALL_MODEL_INSTANCE_COMMENTS, tagText);
                                nf++;
                            }
                        }
                        if (_ceil)
                        {
                            var ct = ceilTypes[ceilTypes.FindIndex(x => x.Name == _ceilT)];
                            var c = Compat.CreateCeiling(doc, Geo.Loops(b, z), ct.Id, level.Id);
                            if (c != null)
                            {
                                var p = c.get_Parameter(BuiltInParameter.CEILING_HEIGHTABOVELEVEL_PARAM);
                                if (p != null && !p.IsReadOnly) p.Set(Compat.FromMm(_ceilH));
                                if (_comment) Compat.SetStr(c, BuiltInParameter.ALL_MODEL_INSTANCE_COMMENTS, tagText);
                                nc++;
                            }
                        }
                        double baseOff = _floor ? Compat.FromMm(_floorOff) : 0;
                        if (_clad)
                        {
                            double h = _roomH && room.UnboundedHeight > 0 ? room.UnboundedHeight - baseOff : Compat.FromMm(_cladH);
                            if (_ceil && _roomH) h = Math.Min(h, Compat.FromMm(_ceilH) - baseOff + Compat.FromMm(50)); // just above the ceiling
                            nw += Lining(doc, room, b, cladType, baseOff, h, false, tagText);
                        }
                        if (_skirt)
                            ns += Lining(doc, room, b, skirtType, baseOff, Compat.FromMm(_skirtH), true, tagText);
                    }
                    catch (Exception ex) { bad++; if (errors.Count < 5) errors.Add(tagText + ": " + ex.Message); }
                }
                t.Commit();
            }
            var msg = "الأرضيات: " + nf + "\nالأسقف: " + nc + "\nحوائط التجليد: " + nw + "\nقطع الوزرة: " + ns;
            if (bad > 0) msg += "\n\nغرف معرفتش أشتغل عليها: " + bad + (errors.Count > 0 ? "\n" + string.Join("\n", errors) : "") + "\n(غالباً الغرفة مش مقفولة أو حدودها متداخلة)";
            Dlg.Info("تم ✓", msg);
            return Result.Succeeded;
        }

        /// <summary>Thin walls along the inner face of the room; corners are trimmed, doors (and windows for cladding) are opened.</summary>
        static int Lining(Document doc, Room room, List<List<BoundarySegment>> loops, WallType wt, double baseOff, double height, bool skirting, string comment)
        {
            if (wt == null || height <= 0) return 0;
            int made = 0;
            double half = wt.Width / 2;
            double z = room.Level.Elevation;
            double testZ = ((LocationPoint)room.Location).Point.Z + baseOff + Compat.FromMm(300);

            foreach (var loop in loops)
            {
                // 1) offset every physical segment to the room side
                var segs = new List<Tuple<Curve, Element>>();
                foreach (var s in loop)
                {
                    Element host = doc.GetElement(s.ElementId);
                    Curve c = Geo.Flat(s.GetCurve(), z);
                    if (host == null || host is ModelCurve || c.Length < Compat.FromMm(20)) { segs.Add(Tuple.Create<Curve, Element>(null, null)); continue; }
                    Curve off = null;
                    foreach (var sign in new[] { 1.0, -1.0 })
                    {
                        Curve cand;
                        try { cand = c.CreateOffset(sign * half, XYZ.BasisZ); } catch { continue; }
                        var m = Geo.Mid(cand);
                        if (room.IsPointInRoom(new XYZ(m.X, m.Y, testZ))) { off = cand; break; }
                    }
                    segs.Add(Tuple.Create(off, host));
                }
                // 2) trim / extend neighbouring straight pieces so corners close
                int n = segs.Count;
                var lines = segs.Select(x => x.Item1).ToList();
                for (int i = 0; i < n; i++)
                {
                    var a = lines[i] as Line; var b = lines[(i + 1) % n] as Line;
                    if (a == null || b == null || n < 2) continue;
                    var x = Geo.Cross(a, b);
                    if (x == null) continue;
                    if (x.DistanceTo(a.GetEndPoint(1)) > wt.Width * 4 + Compat.FromMm(50)) continue; // not a real corner
                    try
                    {
                        lines[i] = Line.CreateBound(a.GetEndPoint(0), x);
                        lines[(i + 1) % n] = Line.CreateBound(x, b.GetEndPoint(1));
                    }
                    catch { }
                }
                // 3) walls (skirting is split at doors, cladding gets openings)
                for (int i = 0; i < n; i++)
                {
                    var c = lines[i]; var host = segs[i].Item2;
                    if (c == null) continue;
                    var inserts = Inserts(doc, host, skirting);
                    var pieces = new List<Curve> { c };
                    var line = c as Line;
                    if (skirting && line != null && inserts.Count > 0)
                    {
                        var gaps = new List<double[]>();
                        foreach (var ins in inserts)
                        {
                            double a0, b0;
                            if (Geo.Span(line, ins.get_BoundingBox(null), out a0, out b0)) gaps.Add(new[] { a0, b0 });
                        }
                        pieces = Cut(line, gaps);
                    }
                    foreach (var piece in pieces)
                    {
                        if (piece.Length < Compat.FromMm(30)) continue;
                        Wall w;
                        try { w = Wall.Create(doc, piece, wt.Id, room.Level.Id, height, baseOff, false, false); }
                        catch { continue; }
                        var rb = w.get_Parameter(BuiltInParameter.WALL_ATTR_ROOM_BOUNDING); if (rb != null && !rb.IsReadOnly) rb.Set(0);
                        var lk = w.get_Parameter(BuiltInParameter.WALL_KEY_REF_PARAM); if (lk != null && !lk.IsReadOnly) lk.Set(0); // centerline
                        if (!string.IsNullOrEmpty(comment)) Compat.SetStr(w, BuiltInParameter.ALL_MODEL_INSTANCE_COMMENTS, (skirting ? "وزرة " : "تجليد ") + comment);
                        try { WallUtils.DisallowWallJoinAtEnd(w, 0); WallUtils.DisallowWallJoinAtEnd(w, 1); } catch { }
                        made++;
                        if (!skirting && line != null) OpenAt(doc, w, (Line)piece, inserts, z + baseOff, z + baseOff + height);
                    }
                }
            }
            return made;
        }

        /// <summary>Doors (and windows for cladding) hosted in the boundary wall.</summary>
        static List<Element> Inserts(Document doc, Element host, bool doorsOnly)
        {
            var res = new List<Element>();
            var wall = host as Wall;
            if (wall == null) return res;
            foreach (ElementId id in wall.FindInserts(true, false, true, false))
            {
                var e = doc.GetElement(id) as FamilyInstance;
                if (e == null) continue;
                if (Compat.IsCategory(e, BuiltInCategory.OST_Doors) || (!doorsOnly && Compat.IsCategory(e, BuiltInCategory.OST_Windows))) res.Add(e);
            }
            return res;
        }

        static List<Curve> Cut(Line line, List<double[]> gaps)
        {
            var res = new List<Curve>();
            var g = gaps.OrderBy(x => x[0]).ToList();
            double cur = 0;
            XYZ p0 = line.GetEndPoint(0), p1 = line.GetEndPoint(1);
            Func<double, XYZ> at = t => p0 + (p1 - p0) * t;
            foreach (var x in g)
            {
                if (x[0] > cur) { try { res.Add(Line.CreateBound(at(cur), at(x[0]))); } catch { } }
                cur = Math.Max(cur, x[1]);
            }
            if (cur < 1) { try { res.Add(Line.CreateBound(at(cur), at(1))); } catch { } }
            return res;
        }

        static void OpenAt(Document doc, Wall w, Line line, List<Element> inserts, double bottom, double top)
        {
            foreach (var ins in inserts)
            {
                var bb = ins.get_BoundingBox(null);
                double a, b;
                if (bb == null || !Geo.Span(line, bb, out a, out b)) continue;
                double z0 = Math.Max(bottom, bb.Min.Z), z1 = Math.Min(top, bb.Max.Z);
                if (z1 - z0 < Compat.FromMm(20)) continue;
                XYZ p0 = line.GetEndPoint(0), p1 = line.GetEndPoint(1);
                var s = p0 + (p1 - p0) * a; var e = p0 + (p1 - p0) * b;
                try { doc.Create.NewOpening(w, new XYZ(s.X, s.Y, z0), new XYZ(e.X, e.Y, z1)); } catch { }
            }
        }
    }

    /// <summary>Swallows overlap/join warnings so a batch run isn't interrupted by dozens of dialogs.</summary>
    public class QuietWarnings : IFailuresPreprocessor
    {
        public FailureProcessingResult PreprocessFailures(FailuresAccessor fa)
        {
            foreach (var f in fa.GetFailureMessages())
                if (f.GetSeverity() == FailureSeverity.Warning) fa.DeleteWarning(f);
            return FailureProcessingResult.Continue;
        }
    }
}
