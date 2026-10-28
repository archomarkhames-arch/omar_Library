using System;
using System.Diagnostics;
using Autodesk.Revit.Attributes;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using OmarKhamis.Revit.Core;

namespace OmarKhamis.Revit.Commands
{
    [Transaction(TransactionMode.ReadOnly)]
    public class UpdateCmd : IExternalCommand
    {
        public Result Execute(ExternalCommandData data, ref string message, ElementSet elements)
        {
            try
            {
                string msg;
                System.Windows.Forms.Cursor.Current = System.Windows.Forms.Cursors.WaitCursor;
                bool got = Updater.Run(out msg);
                System.Windows.Forms.Cursor.Current = System.Windows.Forms.Cursors.Default;
                if (got) Dlg.Info("تحديث جديد ✓", msg);
                else Dlg.Info("إنت على آخر إصدار ✓", "الإصدار الحالي: " + Updater.Current + (Updater.LatestSeen != null ? "\nآخر إصدار على السيرفر: " + Updater.LatestSeen : ""));
            }
            catch (Exception ex)
            {
                Dlg.Info("معرفتش أوصل لسيرفر التحديثات", "اتأكد من النت وجرب تاني.\n\n" + ex.Message);
            }
            return Result.Succeeded;
        }
    }

    [Transaction(TransactionMode.ReadOnly)]
    public class AboutCmd : IExternalCommand
    {
        public Result Execute(ExternalCommandData data, ref string message, ElementSet elements)
        {
            var td = new TaskDialog("م. عمر خميس")
            {
                MainInstruction = "مكتبة م. عمر خميس لريفيت",
                MainContent = "الإصدار " + Updater.Current + " — ريفيت " + Compat.Version + "\n\n"
                    + "أدوات BIM بالعربي: ترقيم وتشطيبات الغرف، شيتات وإعادة تسمية بالجملة، تاجات بضغطة، وحصر كميات على Excel.\n"
                    + "وجاي: Families بارامترية للإنتريور، الخامات، واللوحات التنفيذية.\n\n"
                    + "للتواصل: 01005433170 (واتساب)",
                FooterText = "© 2026 Eng. Omar Khamis"
            };
            td.AddCommandLink(TaskDialogCommandLinkId.CommandLink1, "صفحة المكتبة", "الخدمات والفيديوهات والاشتراك");
            td.AddCommandLink(TaskDialogCommandLinkId.CommandLink2, "كلّم م. عمر على واتساب");
            var r = td.Show();
            try
            {
                if (r == TaskDialogResult.CommandLink1) Process.Start(new ProcessStartInfo("https://okl-admin.arch-omar-khames.workers.dev/buy") { UseShellExecute = true });
                if (r == TaskDialogResult.CommandLink2) Process.Start(new ProcessStartInfo("https://wa.me/201005433170") { UseShellExecute = true });
            }
            catch { }
            return Result.Succeeded;
        }
    }
}
