using System;
using System.IO;
using System.Reflection;
using Autodesk.Revit.Attributes;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using OmarKhamis.Revit.Core;

namespace OmarKhamis.Revit
{
    /// <summary>Entry point: builds the «م. عمر خميس» ribbon tab.</summary>
    public class App : IExternalApplication
    {
        public const string Tab = "م. عمر خميس";

        public Result OnStartup(UIControlledApplication app)
        {
            try { app.CreateRibbonTab(Tab); } catch { }
            string asm = Assembly.GetExecutingAssembly().Location;

            var rooms = app.CreateRibbonPanel(Tab, "الغرف");
            Btn(rooms, "RoomNumber", "ترقيم\nالغرف", "Commands.RoomNumberCmd", "room_number",
                "ترقيم الغرف تلقائي بالترتيب أو بالضغط عليها واحدة واحدة، مع بادئة ورقم بداية وخطوة.");
            Btn(rooms, "RoomFinish", "تشطيبات\nالغرف", "Commands.RoomFinishCmd", "room_finish",
                "عمل أرضيات وأسقف وتجاليد حوائط ووزرات لكل غرفة من حدودها بضغطة واحدة.");

            var sheets = app.CreateRibbonPanel(Tab, "الشيتات والفيوز");
            Btn(sheets, "Sheets", "شيتات\nبالجملة", "Commands.SheetsCmd", "sheets",
                "إنشاء شيتات كتير مرة واحدة من قايمة أرقام وأسماء، ووضع الفيوز عليها.");
            Btn(sheets, "Rename", "إعادة\nتسمية", "Commands.RenameCmd", "rename",
                "إعادة تسمية الفيوز أو الشيتات أو الأنواع بالجملة: بحث واستبدال، بادئة، لاحقة، ترقيم.");

            var doc = app.CreateRibbonPanel(Tab, "التوثيق والكميات");
            Btn(doc, "Tags", "تاجات\nبضغطة", "Commands.TagAllCmd", "tags",
                "وضع تاجات للأبواب والشبابيك والغرف والأثاث وغيرها في الفيو الحالي، للعناصر اللي ملهاش تاج بس.");
            Btn(doc, "Qty", "كميات\nExcel", "Commands.QuantitiesCmd", "excel",
                "حصر كميات المشروع (حوائط، أرضيات، أسقف، أبواب، شبابيك، غرف، أثاث…) في ملف Excel مرتب بالعربي.");

            var lib = app.CreateRibbonPanel(Tab, "المكتبة");
            Btn(lib, "Update", "تحديث", "Commands.UpdateCmd", "update", "البحث عن إصدار جديد وتنزيله.");
            Btn(lib, "About", "عن\nالمكتبة", "Commands.AboutCmd", "about", "معلومات المكتبة والتواصل مع م. عمر خميس.");

            Updater.Cleanup();
            Updater.CheckInBackground();
            app.Idling += (s, e) =>
            {
                var m = Updater.Pending;
                if (m == null) return;
                Updater.Pending = null;
                try { Dlg.Info("تحديث جديد ✓", m); } catch { }
            };
            return Result.Succeeded;
        }

        public Result OnShutdown(UIControlledApplication app) { return Result.Succeeded; }

        static void Btn(RibbonPanel panel, string name, string text, string cls, string icon, string tip)
        {
            var d = new PushButtonData("OKR_" + name, text, Assembly.GetExecutingAssembly().Location, "OmarKhamis.Revit." + cls)
            {
                ToolTip = tip,
                LargeImage = Icons.Get(icon, 32),
                Image = Icons.Get(icon, 16)
            };
            panel.AddItem(d);
        }
    }
}
