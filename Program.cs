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
        public const string TestFile = @"C:\Program Files (x86)\LEGO Bionicle\Data\characters\onua\Xs\onua.x";
        //public const string TestFile = @"C:\Users\Admin\Desktop\Modding\Bionicle\Sample Files\main.bcl.obj";
        //public const string TestFile = @"C:\Users\Admin\Desktop\Modding\Bionicle\Sample Files\Main Omega.bcl.obj";
        //public const string TestFile = @"C:\Users\Admin\Desktop\Modding\Bionicle\Sample Files\Watr2.bcl.obj";
        //public const string TestFile = @"C:\Program Files (x86)\LEGO Bionicle\Data\Levels\Lev1\Bech\Bcls\Main.ocl";
        //public const string TestFile = @"C:\Users\Admin\Desktop\Modding\Bionicle\Sample Files\COLLADA Test\conversion\Main.x";
        //public const string TestFile = @"C:\Users\Admin\Desktop\Modding\Bionicle\Sample Files\Main.obj";
        //public const string TestFile = @"C:\Users\Admin\Desktop\Modding\Bionicle\Sample Files\COLLADA Test\conversion\Main.x";
        //public const string TestFile = @"C:\Users\Admin\Desktop\Modding\Bionicle\Sample Files\Edited_files\Edited files\Mskc.obj";
        //public const string TestFile = @"C:\Program Files (x86)\LEGO Bionicle\Data\Levels\Lev1\Bech\Xs\Plnt_backup.x";
        //public const string TestFile = @"C:\Users\Admin\Desktop\Modding\Bionicle\Sample Files\Plnt_backup - Copy.x";
        //public const string TestFile = @"C:\Users\Admin\Desktop\Modding\Bionicle\Sample Files\Plnt_backup.x";
        //public const string TestFile = @"C:\Users\Admin\Desktop\Modding\Bionicle\Sample Files\TremorColored.obj";
        //public const string TestFile = @"C:\Users\Admin\Desktop\Modding\Bionicle\Sample Files\TremorColored.x";
        //@"C:\Program Files (x86)\LEGO Bionicle\Data\characters\ssss\Xs\ssss.x";
        //@"C:\Program Files (x86)\LEGO Bionicle\Data\characters\kopa\Xs\swrd.x";
        //@"C:\Program Files (x86)\LEGO Bionicle\Data\Levels\Lev1\Vllg\Xs\Main.x";
        //public const string TestFile = @"C:\Users\Admin\Desktop\Modding\Bionicle\Sample Files\onua.x";

        public static INIConfig Config;

        static void Main(string[] args)
        {
            Console.WriteLine("LOMNTool v" + System.Reflection.Assembly.GetEntryAssembly().GetName().Version.ToString());

            Config = new INIConfig(Path.Combine(Path.GetDirectoryName(System.Reflection.Assembly.GetEntryAssembly().Location), "LOMNTool.ini"));

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

#if !DEBUG
            try
            {
#endif
            foreach (string arg in args)
            {
                Console.WriteLine("Processing file '" + arg + "'...");

                string extension = Path.GetExtension(arg.ToLower());
                if (arg.EndsWith(".bcl.obj"))
                {
                    BCLOBJFile(arg);
                }
                else if (extension == ".x")
                {
                    XFile(arg);
                }
                else if (extension == ".obj")
                {
                    OBJFile(arg);
                }
                else if (extension == ".bcl")
                {
                    BCLFile(arg);
                }
                else if (extension == ".ocl")
                {
                    OCLFile(arg);
                }
                else if (extension == ".dae")
                {
                    DAEFile(arg);
                }
                else
                {
                    Console.WriteLine("Unknown file extension '" + extension + "'!");
                }
            }
            #if !DEBUG
            }
            catch (Exception ex)
            {
                Console.WriteLine("Exception: \n\n" + ex.ToString());
            }
            #endif
            Console.WriteLine("Press any key to close...");
            Console.ReadKey();
            Config.Write(Path.Combine(Path.GetDirectoryName(System.Reflection.Assembly.GetEntryAssembly().Location), "LOMNTool.ini"));
        }

        public static void XFile(string arg)
        {
            using (FileStream stream = new FileStream(arg, FileMode.Open))
            using (BinaryReader reader = new BinaryReader(stream))
            {
                XFile file = new XFile(reader);

                string modelFormat = Config.GetValueOrDefault("Models", "Format", "DAE");
                if (modelFormat == "OBJ")
                {
                    bool splitMaterials = Config.GetValueOrDefault("OBJ", "SplitByMaterial", "False").ToLower() == "true";
                    Console.WriteLine("    Writing OBJ file...");
                    XUtils.ExportOBJ(file.Objects[0][1].Object, Path.ChangeExtension(arg, ".obj"), SharpDX.Matrix.RotationX(-SharpDX.MathUtil.PiOverTwo), true, ".dds", splitMaterials);
                }
                else if (modelFormat == "DAE")
                {
                    Console.WriteLine("    Writing DAE file...");
                    bool stripUnusedMaterials = Config.GetValueOrDefault("DAE", "StripUnusedMaterials", "False").ToLower() == "true";
                    Collada.Utils.ExportCOLLADA(file, Path.ChangeExtension(arg, ".dae"), SharpDX.Matrix.RotationX(-SharpDX.MathUtil.PiOverTwo), true, ".dds", stripUnusedMaterials);
                }
                else if (modelFormat == "TXT")
                {
                    Console.WriteLine("    Dumping X tokens...");
                    stream.Position = 0;
                    XHeader header = new XHeader(reader);
                    List<XTemplate> templates = new List<XTemplate>();
                    List<XObject> objects = new List<XObject>();
                    XReader xreader = new XReader(reader, header, templates, objects);

                    using (StreamWriter writer = new StreamWriter(Path.ChangeExtension(arg, ".txt")))
                    {
                        foreach (XToken token in xreader.Tokens)
                        {
                            writer.WriteLine(token.ToString());
                        }
                    }
                }
                else
                {
                    Console.WriteLine("    [ERROR]: Invalid model format specified in LOMNTool.ini! (LOMNTool.ini:[Models].Format)");
                }
            }
        }

        public static void OBJFile(string arg)
        {
            Console.WriteLine("    Writing X file...");

            XFile file = XUtils.ImportOBJ(arg, SharpDX.Matrix.RotationX(SharpDX.MathUtil.PiOverTwo), true, true);

            using (FileStream stream = new FileStream(Path.ChangeExtension(arg, ".x"), FileMode.Create))
            using (BinaryWriter writer = new BinaryWriter(stream))
            {
                file.Write(writer);
            }
        }

        public static void OCLFile(string arg)
        {
            using (FileStream stream = new FileStream(arg, FileMode.Open, FileAccess.Read, FileShare.Read))
            using (BinaryReader reader = new BinaryReader(stream))
            {
                OCLFile file = new LOMNTool.OCLFile(reader);
                file.LogDebug();
                file.DumpOBJ(Path.ChangeExtension(arg, ".ocl.obj"));
            }
        }

        public static void BCLFile(string arg)
        {
            using (FileStream stream = new FileStream(arg, FileMode.Open, FileAccess.Read, FileShare.Read))
            using (BinaryReader reader = new BinaryReader(stream))
            {
                BCLFile file = new BCLFile(reader);
                file.ExportOBJ(Path.ChangeExtension(arg, ".bcl.obj"));
            }
        }

        public static void BCLOBJFile(string arg)
        {
            BCLFile file = LOMNTool.BCLFile.ImportOBJ(arg);

            using (FileStream stream = new FileStream(arg.Substring(0, arg.Length - 8) + ".bcl", FileMode.Create, FileAccess.Write, FileShare.Read))
            using (BinaryWriter writer = new BinaryWriter(stream))
            {
                file.Write(writer);
            }
        }

        public static void DAEFile(string arg)
        {
            XFile file = Collada.Utils.ImportCOLLADA(arg, SharpDX.Matrix.RotationX(SharpDX.MathUtil.PiOverTwo), true, true);

            using (FileStream stream = new FileStream(Path.ChangeExtension(arg, ".x"), FileMode.Create, FileAccess.Write, FileShare.Read))
            using (BinaryWriter writer = new BinaryWriter(stream))
            {
                file.Write(writer);
            }
        }
    }
}
