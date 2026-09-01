using System.Drawing;
using System.Runtime.InteropServices;
using System.Windows.Interop;
using System.Windows.Media.Imaging;

namespace AppKeeper.Services;

public static class IconService
{
    [DllImport("gdi32.dll")]
    private static extern bool DeleteObject(nint objectHandle);

    public static BitmapSource? GetIcon(string path)
    {
        try
        {
            using var icon = Icon.ExtractAssociatedIcon(path);
            if (icon is null)
                return null;

            using var bitmap = icon.ToBitmap();
            var handle = bitmap.GetHicon();
            try
            {
                var source = Imaging.CreateBitmapSourceFromHIcon(handle, System.Windows.Int32Rect.Empty,
                    BitmapSizeOptions.FromWidthAndHeight(32, 32));
                source.Freeze();
                return source;
            }
            finally
            {
                DeleteObject(handle);
            }
        }
        catch
        {
            return null;
        }
    }
}
