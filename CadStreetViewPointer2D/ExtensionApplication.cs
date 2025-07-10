#define DEBUG
#define NON_VOLATILE_MEMORY

using System; // Potřebné pro 4.6 Math
using System.Diagnostics;

#region O_PROGRAM_DETERMINE_CAD_PLATFORM
#if ZWCAD
using ZwSoft.ZwCAD.ApplicationServices;
using ZwSoft.ZwCAD.Geometry;
using ZwSoft.ZwCAD.Runtime;
using ZwSoft.ZwCAD.EditorInput;
#else
using Autodesk.AutoCAD.ApplicationServices;
using Autodesk.AutoCAD.Geometry;
using Autodesk.AutoCAD.Runtime;
using Autodesk.AutoCAD.EditorInput;
#endif
#endregion

[assembly: CommandClass(typeof(CadStreetViewPointer2D.ExtensionApplication))]
namespace CadStreetViewPointer2D
{
    public class ExtensionApplication : IExtensionApplication
    {
        public static Document Document
        {
            get
            {
#if ZWCAD
                return ZwSoft.ZwCAD.ApplicationServices.Application.DocumentManager.MdiActiveDocument; 
#else
                return Autodesk.AutoCAD.ApplicationServices.Core.Application.DocumentManager.MdiActiveDocument;
#endif
            }
        }

        public static Editor DocumentEditor
        {
            get { return Document.Editor; }
        }

        public static bool IsAcad
        {
            get
            {
                if (Process.GetCurrentProcess().ProcessName.Contains("acad"))
                {
                    foreach (var assembly in AppDomain.CurrentDomain.GetAssemblies())
                    {
                        var fullName = assembly.FullName;
                        if (fullName != null && fullName.StartsWith("acdbmgd", StringComparison.OrdinalIgnoreCase))
                        {
                            return true;
                        }
                    }
                }
                return false;
            }
        }

        public void Initialize() // Zaváděcí funkce při spuštění
        {
            DocumentEditor.WriteMessage(
                "\n==========================================" +
                "\n   Google StreetView pointer generator for ZwCAD " +
                "\n   (c) 2025 Martin Coplák  |  VUT Brno" +
                "\n   Contact: martin.coplak@viapont.cz" +
                "\n------------------------------------------" +
                "\n   CadStreetView Pointer (2D)  |   Version: 25.07.09" +
                "\n==========================================\n"
            );

        }

        #region JTSK2_STREETVIEW
        [CommandMethod("JTSK2_STREETVIEW")]
        public static void JTSK2_STREETVIEW()
        {
            PromptPointOptions place = new PromptPointOptions("\nVyber místo"); // Místo new(...): Důvod podpora v4.6
            PromptPointResult evPoint = DocumentEditor.GetPoint(place);
            if (evPoint.Status != PromptStatus.OK) return;
            Point3d point = evPoint.Value;
#if DEBUG
            DocumentEditor.WriteMessage("\nX_JTSK " + point.X + ", Y_JTSK: " + point.Y + " \n");
#endif   
            double[] pointOfInterest = JTSK2WGS84(point.X, point.Y, point.Z);
            string url = string.Format("http://maps.google.com/maps?q=&layer=c&cbll={0},{1}", pointOfInterest[1], pointOfInterest[0]); // Místo $"{var}" použit string.Format(..., var1, var2, ...): Důvod podpora v4.6
            try
            {
                Process.Start(new ProcessStartInfo
                {
                    FileName = url,
                    UseShellExecute = true // Nutné pro zobrazení URL v browseru
                });
            }
            catch (System.Exception)
            { }
            finally
            {
                DocumentEditor.WriteMessage("\nLAT_WGS84 " + pointOfInterest[1] + ", LON_WGS84: " + pointOfInterest[0] + " \n");
            }
        }
        #endregion
        #region SUPPORTIVE_FUNCTIONS
        private static double[] JTSK2WGS84(double source_X, double source_Y, double H = 267) // 267m referenční výška pro VUT Brno
        {
            var X_JTSK = Math.Abs(source_Y); // Vždy, otočeno z důvodů otočení XY
            var Y_JTSK = Math.Abs(source_X); // Vždy, otočeno z důvodů otočení XY
            // Výpočet zemepisných souŘadnic z rovinných souřadnic
            var ro = Math.Sqrt(X_JTSK * X_JTSK + Y_JTSK * Y_JTSK);
            var epislon = 2 * Math.Atan2(Y_JTSK, ro + X_JTSK); // Opraven kvadrant
            var D = epislon / 0.97992470462083;
            var S = 2 * Math.Atan(Math.Exp(1 / 0.97992470462083 * Math.Log(12310230.12797036 / ro))) - Math.PI / 2;
            var sin_S = Math.Sin(S);
            var cos_S = Math.Cos(S);
            var sin_U = 0.863499969506341 * sin_S - 0.504348889819882 * cos_S * Math.Cos(D);

            var cos_U = Math.Sqrt(1 - sin_U * sin_U);
            var sin_DV = Math.Sin(D) * cos_S / cos_U;
            var cos_DV = Math.Sqrt(1 - sin_DV * sin_DV);
            var sin_V = 0.420215144586493 * cos_DV - 0.907424504992097 * sin_DV;
            var cos_V = 0.907424504992097 * cos_DV + 0.420215144586493 * sin_DV;
            var t = Math.Exp(2 / 1.000597498371542 * Math.Log((1 + sin_U) / cos_U / 1.003419163966575));
            var pom = (t - 1) / (t + 1);

            double sin_B;
            do
            {
                sin_B = pom;
                pom = t * Math.Exp(0.081696831215303 * Math.Log((1 + 0.081696831215303 * sin_B) / (1 - 0.081696831215303 * sin_B)));
                pom = (pom - 1) / (pom + 1);
            }
            while (Math.Abs(pom - sin_B) > 1e-15);
            var L_JTSK = 2 * Math.Atan(sin_V / (1 + cos_V)) / 1.000597498371542;
            var B_JTSK = Math.Atan(pom / Math.Sqrt(1 - pom * pom));

            // Pravoúhlé souřadnice S-JTSJ
            var e1 = 1 - (1 - 1 / 299.152812853) * (1 - 1 / 299.152812853);
            ro = 6377397.15508 / Math.Sqrt(1 - e1 * Math.Sin(B_JTSK) * Math.Sin(B_JTSK));
            var x1 = (ro + H) * Math.Cos(B_JTSK) * Math.Cos(L_JTSK); // Chyba
            var x_w = -4.99821 / 3600 * Math.PI / 180;
            var y1 = (ro + H) * Math.Cos(B_JTSK) * Math.Sin(L_JTSK); // Chyba
            var y_w = -1.58676 / 3600 * Math.PI / 180;
            var z1 = ((1 - e1) * ro + H) * Math.Sin(B_JTSK);
            var z_w = -5.2611 / 3600 * Math.PI / 180;

            // Pravoúhlé souřadnice WGS-84
            var x2 = 570.69 + (1 + 3.543e-6) * (x1 + z_w * y1 - y_w * z1);
            var y2 = 85.69 + (1 + 3.543e-6) * (-z_w * x1 + y1 + x_w * z1);
            var z2 = 462.84 + (1 + 3.543e-6) * (y_w * x1 - x_w * y1 + z1);

            // Geodetické souřadnice pro systém WGS84
            var a = 298.257223563 / (298.257223563 - 1);
            var p = Math.Sqrt(x2 * x2 + y2 * y2);
            var e2 = 1 - (1 - 1 / 298.257223563) * (1 - 1 / 298.257223563);
            var theta = Math.Atan(z2 * a / p);
            var st = Math.Sin(theta);
            var ct = Math.Cos(theta);
            t = (z2 + e2 * a * 6378137.0 * st * st * st) / (p - e2 * 6378137.0 * ct * ct * ct);

            var lat = Math.Atan(t) / Math.PI * 180;
            var lon = 2 * Math.Atan(y2 / (p + x2)) / Math.PI * 180;
            return new double[] { lon, lat }; // Ve formátu 16.5,52.5 tedy lon,lat
        }
        #endregion
        #region TERMINATE
        public void Terminate()
        {

        }
        #endregion
    }
}