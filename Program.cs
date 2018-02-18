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
        public const string TestFile = @"C:\Program Files (x86)\LEGO Bionicle\Data\Levels\Lev1\Vllg\Xs\Main.x";//@"C:\Users\Admin\Desktop\Modding\Bionicle\Sample Files\onua.x";

        static void Main(string[] args)
        {
            foreach (string arg in args)
                Console.WriteLine("'" + arg + "'");
            Console.WriteLine();


            if (args.Length == 0)
            {
                args = new string[] { TestFile };
                /*Console.WriteLine("Drag files onto LOMNTool.exe to work with them.");
                Console.WriteLine("Press any key to close...");
                Console.ReadKey();
                return;*/
            }

            string extension = Path.GetExtension(args[0]);
            if (extension == ".x")
            {
                XFile(args);
            }
            else
            {
                Console.WriteLine("Unknown file extension '" + extension + "'!");
                Console.WriteLine("Press any key to close...");
                Console.ReadKey();
            }
        }

        public static void XFile(string[] args)
        {
            using (FileStream stream = new FileStream(args[0], FileMode.Open))
            using (BinaryReader reader = new BinaryReader(stream))
            {
                XFile file = new XFile(reader);

                XUtils.ExportOBJ(file.Objects[0][1].Object, Path.ChangeExtension(args[0], ".obj"), SharpDX.Matrix.RotationX(-SharpDX.MathUtil.PiOverTwo), true, ".dds");

                Console.WriteLine("Press any key to close...");
                Console.ReadKey();
            }

        }
    }
}
