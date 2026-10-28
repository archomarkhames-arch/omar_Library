# مكتبة م. عمر خميس لريفيت (Revit 2017 → 2027)

## التشغيل
- الكود في `revit/src` (C#). أي رفع لـ `revit/src` أو `revit/installer` على GitHub بيشغّل
  `.github/workflows/revit-build.yml` أوتوماتيك، وده بيعمل:
  - DLL لكل إصدار من 2017 لـ 2027:
    - 2017–2024 بـ net48
    - 2025–2026 بـ net8
    - 2027 بـ net10
  - ملفات التحديث في `revit/update/` (`manifest.json` + `R<سنة>-<إصدار>.zip`).
  - المثبّت `revit/setup/OmarKhamis_Revit_Setup.exe`.
  - تقرير البناء `revit/update/build_report.txt`.
- رقم الإصدار في `revit/src/version.txt`، والجديد في `revit/src/notes.txt`. **غيّر الرقم قبل كل رفع.**
- الإضافة بتدوّر على التحديثات في
  `https://raw.githubusercontent.com/archomarkhames-arch/omar_library/main/revit/update/manifest.json`.
  - بتنزّل الإصدار الجديد جنب القديم في `%AppData%\OmarKhamisRevit\versions\…`.
  - بتغيّر مسار الـ `.addin`، فالإصدار الجديد بيشتغل من أول ما ريفيت يتفتح تاني.

## الأدوات (المرحلة 1)
- ترقيم الغرف
- تشطيبات الغرف (أرضيات / أسقف / تجليد بفتحات الأبواب والشبابيك / وزرة مقطوعة عند الأبواب)
- شيتات بالجملة
- إعادة تسمية بالجملة
- تاجات بضغطة
- كميات Excel
- تحديث
- عن المكتبة
