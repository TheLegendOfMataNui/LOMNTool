using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.IO;
using D3DX.Mesh;

namespace LOMNTool
{
    public class Program
    {
        //public const string TestFile = @"C:\Program Files (x86)\LEGO Bionicle\Data\Levels\Lev1\Bech\Xs\Plnt_backup.x";
        //public const string TestFile = @"C:\Users\Admin\Desktop\Modding\Bionicle\Sample Files\Plnt_backup - Copy.x";
        public const string TestFile = @"C:\Users\Admin\Desktop\Modding\Bionicle\Sample Files\Plnt_backup.x";
        //public const string TestFile = @"C:\Users\Admin\Desktop\Modding\Bionicle\Sample Files\TremorColored.obj";
        //public const string TestFile = @"C:\Users\Admin\Desktop\Modding\Bionicle\Sample Files\TremorColored.x";
        //@"C:\Program Files (x86)\LEGO Bionicle\Data\characters\ssss\Xs\ssss.x";
        //@"C:\Program Files (x86)\LEGO Bionicle\Data\characters\kopa\Xs\swrd.x";
        //@"C:\Program Files (x86)\LEGO Bionicle\Data\Levels\Lev1\Vllg\Xs\Main.x";
        //public const string TestFile = @"C:\Users\Admin\Desktop\Modding\Bionicle\Sample Files\onua.x";

        static void Main(string[] args)
        {
            Console.WriteLine("LOMNTool v" + System.Reflection.Assembly.GetEntryAssembly().GetName().Version.ToString());
            Console.WriteLine("Inputs:");
            foreach (string arg in args)
                Console.WriteLine("'" + arg + "'");
            Console.WriteLine();

            if (args.Length == 0)
            {
#if DEBUG
                args = new string[] { TestFile };
#else
                Console.WriteLine("Drag files onto LOMNTool.exe to work with them.");
                Console.WriteLine("Press any key to close...");
                Console.ReadKey();
                return;
#endif
            }

            foreach (string arg in args)
            {
                string extension = Path.GetExtension(arg);
                if (extension == ".x")
                {
                    XFile(arg);
                }
                else if (extension == ".obj")
                {
                    OBJFile(arg);
                }
                else
                {
                    Console.WriteLine("Unknown file extension '" + extension + "'!");
                }

            }
            Console.WriteLine("Press any key to close...");
            Console.ReadKey();
        }

        public static void XFile(string arg)
        {
            Console.WriteLine("Processing X file '" + arg + "'...");
            using (FileStream stream = new FileStream(arg, FileMode.Open))
            using (BinaryReader reader = new BinaryReader(stream))
            {
#if !DEBUG
                try
                {
#endif
                    XFile file = new XFile(reader);

                    XUtils.ExportOBJ(file.Objects[0][1].Object, Path.ChangeExtension(arg, ".obj"), SharpDX.Matrix.RotationX(-SharpDX.MathUtil.PiOverTwo), true, ".dds");
#if !DEBUG
            }
                catch (Exception ex)
                {
                    Console.WriteLine("Exception: \n\n" + ex.ToString());
                }
#endif
            }
        }

        public static void OBJFile(string arg)
        {
            Console.WriteLine("Processing OBJ mesh '" + arg + "'...");

            XFile file = XUtils.ImportOBJ(arg, SharpDX.Matrix.RotationX(SharpDX.MathUtil.PiOverTwo), true, true);

            using (FileStream stream = new FileStream(Path.ChangeExtension(arg, ".x"), FileMode.Create))
            using (BinaryWriter writer = new BinaryWriter(stream))
            {
                file.Write(writer);
            }
        }
    }
}
