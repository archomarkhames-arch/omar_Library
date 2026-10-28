using System;
using System.Collections.Generic;
using System.Linq;
using Autodesk.Revit.DB;

namespace OmarKhamis.Revit.Core
{
    /// <summary>Everything that differs between Revit 2017 and 2027 lives here, so the tools stay identical.</summary>
    public static class Compat
    {
        public const double MM = 304.8;          // millimetres in one internal foot
        public const double M2 = 0.09290304;      // square metres in one square foot
        public const double M3 = 0.028316846592;  // cubic metres in one cubic foot

        public static double FromMm(double mm) { return mm / MM; }
        public static double ToMm(double ft) { return ft * MM; }

        public static long IdValue(ElementId id)
        {
            if (id == null) return -1;
#if RV2024_UP
            return id.Value;
#else
            return id.IntegerValue;
#endif
        }

        public static ElementId NewId(long v)
        {
#if RV2024_UP
            return new ElementId(v);
#else
            return new ElementId((int)v);
#endif
        }

        public static bool SameId(ElementId a, ElementId b) { return IdValue(a) == IdValue(b); }

        public static bool IsCategory(Element e, BuiltInCategory bic)
        {
            return e != null && e.Category != null && IdValue(e.Category.Id) == (long)bic;
        }

        public static int Version
        {
            get
            {
#if R2017
                return 2017;
#elif R2018
                return 2018;
#elif R2019
                return 2019;
#elif R2020
                return 2020;
#elif R2021
                return 2021;
#elif R2022
                return 2022;
#elif R2023
                return 2023;
#elif R2024
                return 2024;
#elif R2025
                return 2025;
#elif R2026
                return 2026;
#else
                return 2027;
#endif
            }
        }

        // ---------- floors / ceilings
        public static Element CreateFloor(Document doc, IList<CurveLoop> loops, ElementId floorTypeId, ElementId levelId)
        {
#if RV2022_UP
            return Floor.Create(doc, loops, floorTypeId, levelId);
#else
            var arr = new CurveArray();
            foreach (Curve c in loops[0]) arr.Append(c);
            var ft = doc.GetElement(floorTypeId) as FloorType;
            var lv = doc.GetElement(levelId) as Level;
            Floor f = doc.Create.NewFloor(arr, ft, lv, false);
            // openings (columns, shafts…) are cut with extra loops
            for (int i = 1; i < loops.Count; i++)
            {
                var hole = new CurveArray();
                foreach (Curve c in loops[i]) hole.Append(c);
                try { doc.Create.NewOpening(f, hole, true); } catch { }
            }
            return f;
#endif
        }

        public static bool CanCreateCeilings
        {
            get
            {
#if RV2022_UP
                return true;
#else
                return false;
#endif
            }
        }

        public static Element CreateCeiling(Document doc, IList<CurveLoop> loops, ElementId ceilingTypeId, ElementId levelId)
        {
#if RV2022_UP
            return Ceiling.Create(doc, loops, ceilingTypeId, levelId);
#else
            return null;
#endif
        }

        // ---------- tags
        public static Element Tag(Document doc, View view, Element e, XYZ at, bool leader, ElementId tagTypeId)
        {
            var r = new Reference(e);
#if RV2018_UP
            IndependentTag t = IndependentTag.Create(doc, view.Id, r, leader, TagMode.TM_ADDBY_CATEGORY, TagOrientation.Horizontal, at);
#else
            IndependentTag t = doc.Create.NewTag(view, e, leader, TagMode.TM_ADDBY_CATEGORY, TagOrientation.Horizontal, at);
#endif
            if (t != null && tagTypeId != null && tagTypeId != ElementId.InvalidElementId)
            {
                try { t.ChangeTypeId(tagTypeId); } catch { }
            }
            return t;
        }

        /// <summary>Elements already tagged in a view (ids of the tagged elements).</summary>
        public static HashSet<long> TaggedIn(Document doc, View view)
        {
            var set = new HashSet<long>();
            foreach (IndependentTag t in new FilteredElementCollector(doc, view.Id).OfClass(typeof(IndependentTag)).Cast<IndependentTag>())
            {
                try
                {
#if RV2022_UP
                    foreach (ElementId id in t.GetTaggedLocalElementIds()) set.Add(IdValue(id));
#else
                    set.Add(IdValue(t.TaggedLocalElementId));
#endif
                }
                catch { }
            }
            foreach (Element rt in new FilteredElementCollector(doc, view.Id).OfCategory(BuiltInCategory.OST_RoomTags).WhereElementIsNotElementType())
            {
                var tag = rt as Autodesk.Revit.DB.Architecture.RoomTag;
                if (tag != null && tag.Room != null) set.Add(IdValue(tag.Room.Id));
            }
            return set;
        }

        // ---------- parameters
        public static string Str(Element e, BuiltInParameter bip)
        {
            var p = e == null ? null : e.get_Parameter(bip);
            if (p == null) return "";
            return p.StorageType == StorageType.String ? (p.AsString() ?? "") : (p.AsValueString() ?? "");
        }

        public static double Dbl(Element e, BuiltInParameter bip)
        {
            var p = e == null ? null : e.get_Parameter(bip);
            return p != null && p.HasValue && p.StorageType == StorageType.Double ? p.AsDouble() : 0;
        }

        public static bool SetStr(Element e, BuiltInParameter bip, string v)
        {
            var p = e == null ? null : e.get_Parameter(bip);
            if (p == null || p.IsReadOnly) return false;
            return p.Set(v ?? "");
        }

        public static string TypeName(Document doc, Element e)
        {
            var t = doc.GetElement(e.GetTypeId());
            if (t == null) return e.Name ?? "";
            var fam = t as FamilySymbol;
            if (fam != null && fam.Family != null) return fam.Family.Name + " : " + t.Name;
            return t.Name ?? "";
        }
    }
}
