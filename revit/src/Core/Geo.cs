using System;
using System.Collections.Generic;
using System.Linq;
using Autodesk.Revit.DB;
using Autodesk.Revit.DB.Architecture;

namespace OmarKhamis.Revit.Core
{
    public static class Geo
    {
        /// <summary>Signed area of a closed loop in plan (positive = counter-clockwise).</summary>
        public static double SignedArea(IEnumerable<Curve> loop)
        {
            var pts = new List<XYZ>();
            foreach (var c in loop)
            {
                var t = c.Tessellate();
                for (int i = 0; i < t.Count - 1; i++) pts.Add(t[i]);
            }
            double a = 0;
            for (int i = 0; i < pts.Count; i++)
            {
                var p = pts[i]; var q = pts[(i + 1) % pts.Count];
                a += p.X * q.Y - q.X * p.Y;
            }
            return a / 2;
        }

        /// <summary>Room boundary loops at the finish face; the outer loop is first.</summary>
        public static List<List<BoundarySegment>> Boundary(Room room)
        {
            var opt = new SpatialElementBoundaryOptions { SpatialElementBoundaryLocation = SpatialElementBoundaryLocation.Finish };
            IList<IList<BoundarySegment>> raw = room.GetBoundarySegments(opt);
            var res = new List<List<BoundarySegment>>();
            if (raw == null) return res;
            foreach (IList<BoundarySegment> l in raw) res.Add(new List<BoundarySegment>(l));
            res = res.OrderByDescending(l => Math.Abs(SignedArea(l.Select(s => s.GetCurve())))).ToList();
            return res;
        }

        public static List<CurveLoop> Loops(List<List<BoundarySegment>> b, double z)
        {
            var res = new List<CurveLoop>();
            foreach (var l in b)
            {
                var cl = new CurveLoop();
                foreach (var s in l)
                {
                    var c = Flat(s.GetCurve(), z);
                    if (c != null) cl.Append(c);
                }
                res.Add(cl);
            }
            return res;
        }

        /// <summary>Same curve moved to elevation z.</summary>
        public static Curve Flat(Curve c, double z)
        {
            var p = c.GetEndPoint(0);
            if (Math.Abs(p.Z - z) < 1e-9) return c;
            return c.CreateTransformed(Transform.CreateTranslation(new XYZ(0, 0, z - p.Z)));
        }

        public static XYZ Mid(Curve c) { return c.Evaluate(0.5, true); }

        /// <summary>Unbounded intersection of two lines in plan (null when parallel).</summary>
        public static XYZ Cross(Line a, Line b)
        {
            XYZ p = a.GetEndPoint(0), r = a.Direction, q = b.GetEndPoint(0), s = b.Direction;
            double den = r.X * s.Y - r.Y * s.X;
            if (Math.Abs(den) < 1e-9) return null;
            double t = ((q.X - p.X) * s.Y - (q.Y - p.Y) * s.X) / den;
            return new XYZ(p.X + t * r.X, p.Y + t * r.Y, p.Z);
        }

        /// <summary>Parameter range [a,b] (0..1 along a line) covered by an element's bounding box.</summary>
        public static bool Span(Line line, BoundingBoxXYZ bb, out double a, out double b)
        {
            a = b = 0;
            if (bb == null) return false;
            var p0 = line.GetEndPoint(0); var dir = line.Direction; var len = line.Length;
            double lo = double.MaxValue, hi = double.MinValue;
            foreach (var x in new[] { bb.Min.X, bb.Max.X })
                foreach (var y in new[] { bb.Min.Y, bb.Max.Y })
                {
                    double t = ((x - p0.X) * dir.X + (y - p0.Y) * dir.Y) / len;
                    lo = Math.Min(lo, t); hi = Math.Max(hi, t);
                }
            a = Math.Max(0, lo); b = Math.Min(1, hi);
            return b - a > 1e-6;
        }
    }
}
