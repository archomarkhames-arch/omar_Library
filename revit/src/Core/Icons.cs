using System;
using System.IO;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace OmarKhamis.Revit.Core
{
    public static class Icons
    {
        public static ImageSource Get(string name, int size)
        {
            try
            {
                var s = typeof(Icons).Assembly.GetManifestResourceStream("OKR." + name + "_" + size + ".png");
                if (s == null) return null;
                var b = new BitmapImage();
                b.BeginInit();
                b.StreamSource = s;
                b.CacheOption = BitmapCacheOption.OnLoad;
                b.EndInit();
                b.Freeze();
                return b;
            }
            catch { return null; }
        }
    }
}
