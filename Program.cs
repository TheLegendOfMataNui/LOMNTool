using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using System.IO;
using D3DX.Mesh;

namespace LOMNTool
{
    public class Program
    {
        public const string TestFile = @"E:\Projects\Modding\Bionicle\Sample Files\Main.x";

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
                // Get all the config args
                foreach (string arg in args)
                {
                    if (arg.StartsWith("-"))
                    {
                        Console.WriteLine("Parsing Command Line Argument " + arg);
                        string[] parts = arg.Substring(arg.IndexOf(']') + 1).Split('=');
                        Config.SetTemporary(arg.Substring(arg.IndexOf('[') + 1, arg.IndexOf(']') - 2), parts[0], parts[1]);
                    }
                }

                // Process the files
                foreach (string arg in args)
                {
                    if (!arg.StartsWith("-"))
                    {
                        Console.WriteLine("Processing file '" + arg + "'...");

                        string extension = Path.GetExtension(arg.ToLower());
                        if (arg.EndsWith(".bcl.obj"))
                        {
                            BCLOBJFile(arg);
                        }
                        else if (arg.EndsWith(".bhd.dae"))
                        {
                            BHDDAEFile(arg);
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
                        else if (extension == ".gltf" || extension == ".glb")
                        {
                            GLTFFile(arg);
                        }
                        else if (extension == ".bhd")
                        {
                            BHDFile(arg);
                        }
                        else
                        {
                            Console.WriteLine("Unknown file extension '" + extension + "'!");
                        }
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
                    BHDFile bhd = null;
                    if (File.Exists(Path.ChangeExtension(arg, ".bhd")))
                    {
                        Console.WriteLine("    Reading BHD...");
                        bhd = new BHDFile(Path.ChangeExtension(arg, ".bhd"));
                    }
                    Console.WriteLine("    Writing DAE file...");
                    bool stripUnusedMaterials = Config.GetValueOrDefault("DAE", "StripUnusedMaterials", "False").ToLower() == "true";
                    Collada.Utils.ExportCOLLADA(file, bhd, Path.ChangeExtension(arg, ".dae"), SharpDX.Matrix.RotationX(-SharpDX.MathUtil.PiOverTwo), true, ".dds", stripUnusedMaterials);
                }
                else if (modelFormat == "GLTF")
                {
                    Console.WriteLine("    Writing GLTF file...");
                    BHDFile bhd = null;
                    if (File.Exists(Path.ChangeExtension(arg, ".bhd")))
                        bhd = new BHDFile(Path.ChangeExtension(arg, ".bhd"));

                    LOMNTool.GLTF.GLTFExporter.Export(file, bhd, Path.ChangeExtension(arg, ".gltf"));
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

        public static void GLTFFile(string arg)
        {
            Console.WriteLine("    Importing GLTF file (checking for morph sequences)...");

            BHDFile bhd;
            Dictionary<string, XFile> frames = LOMNTool.GLTF.GLTFImporter.ImportMorphSequence(arg, SharpDX.Matrix.RotationX(SharpDX.MathUtil.PiOverTwo), out bhd);

            string dir = Path.GetDirectoryName(arg);
            string baseName = Path.GetFileNameWithoutExtension(arg);

            // Sequence Detection: Find trailing numbers in the filename (e.g. "rkm1")
            string prefix = baseName;
            string numStr = "";
            int startNum = 0;

            Match match = Regex.Match(baseName, @"(\d+)$");
            if (match.Success)
            {
                prefix = baseName.Substring(0, match.Index);
                numStr = match.Value;
                startNum = int.Parse(numStr);
            }

            int morphCounter = 0;

            // Force "base" frame to process first, then sort morphs numerically
            var orderedKeys = new List<string> { "base" };
            var morphKeys = frames.Keys.Where(k => k.StartsWith("morph_"))
                                       .OrderBy(k => int.Parse(k.Substring(6)));
            orderedKeys.AddRange(morphKeys);

            // 1. Save all generated geometry targets (.X files)
            foreach (var key in orderedKeys)
            {
                if (!frames.ContainsKey(key)) continue;

                string outputPath;
                if (match.Success)
                {
                    // Formats the updated digit back using the original padding size (e.g. "01" -> "02")
                    string formattedNum = (startNum + morphCounter).ToString(new string('0', numStr.Length));
                    outputPath = Path.Combine(dir, prefix + formattedNum + ".x");
                }
                else
                {
                    // Fallback if no numeric sequence is found
                    if (key == "base")
                    {
                        outputPath = Path.Combine(dir, baseName + ".x");
                    }
                    else
                    {
                        outputPath = Path.Combine(dir, $"{baseName}_{key}.x");
                    }
                }

                using (FileStream stream = new FileStream(outputPath, FileMode.Create, FileAccess.Write, FileShare.Read))
                using (BinaryWriter writer = new BinaryWriter(stream))
                {
                    frames[key].Write(writer);
                }
                Console.WriteLine("    Successfully wrote " + outputPath);

                morphCounter++;
            }

            // 2. Save the Skeleton file if one was extracted
            if (bhd != null && bhd.Bones.Count > 0)
            {
                string bhdPath = Path.Combine(dir, baseName + ".bhd");
                bhd.Write(bhdPath);
                Console.WriteLine("    Successfully wrote " + bhdPath);
            }
        }

        public static void BHDDAEFile(string arg)
        {
            BHDFile file = Collada.Utils.ImportCOLLADASkeleton(arg, SharpDX.Matrix.RotationY(-SharpDX.MathUtil.PiOverTwo));
            file.Write(arg.Replace(".dae", ""));
        }

        public static void BHDFile(string arg)
        {
            BHDFile file = new BHDFile(arg);

            using (System.IO.StreamWriter writer = new StreamWriter(arg + ".txt"))
            {
                writer.WriteLine("[WARNING]: Assuming Biped bone names.");
                foreach (BHDFile.Bone bone in file.Bones)
                {
                    writer.WriteLine("Bone " + bone.Index + " (" + LOMNTool.BHDFile.BipedBoneNames[bone.Index] + ") (parent: " + bone.ParentIndex + "):");
                    writer.WriteLine("  " + bone.Transform.Row1);
                    writer.WriteLine("  " + bone.Transform.Row2);
                    writer.WriteLine("  " + bone.Transform.Row3);
                    writer.WriteLine("  " + bone.Transform.Row4);
                }
            }
        }

        public static void ExportToGLTF(string arg)
        {
            D3DX.Mesh.XFile xFile = new D3DX.Mesh.XFile(new System.IO.BinaryReader(System.IO.File.OpenRead(arg)));

            BHDFile bhd = null;
            string bhdPath = Path.ChangeExtension(arg, ".bhd");
            if (System.IO.File.Exists(bhdPath))
                bhd = new BHDFile(bhdPath);

            LOMNTool.GLTF.GLTFExporter.Export(xFile, bhd, Path.ChangeExtension(arg, ".gltf"));

            Console.WriteLine("Exported GLTF successfully!");
        }
    }
}