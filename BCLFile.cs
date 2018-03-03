using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.IO;
using SharpDX;

namespace LOMNTool
{
    public class BCLFile
    {
        public struct Triangle
        {
            public ushort Index1;
            public ushort Index2;
            public ushort Index3;
            public ushort Unk01; // Material index? Water body index?

            public Triangle(ushort index1, ushort index2, ushort index3, ushort unk01)
            {
                Index1 = index1;
                Index2 = index2;
                Index3 = index3;
                Unk01 = unk01;
            }

            public Triangle(BinaryReader reader)
            {
                Index1 = reader.ReadUInt16();
                Index2 = reader.ReadUInt16();
                Index3 = reader.ReadUInt16();
                Unk01 = reader.ReadUInt16();
            }

            public void Write(BinaryWriter writer)
            {
                writer.Write(Index1);
                writer.Write(Index2);
                writer.Write(Index3);
                writer.Write(Unk01);
            }
        }

        public List<Vector3> Vertices;
        public List<Triangle> Triangles;

        public BCLFile(List<Vector3> vertices, List<Triangle> triangles)
        {
            Vertices = vertices;
            Triangles = triangles;
        }

        public BCLFile(BinaryReader reader)
        {
            Vertices = new List<Vector3>();
            uint vertexCount = reader.ReadUInt32();
            for (uint i = 0; i < vertexCount; i++)
            {
                Vertices.Add(new Vector3(reader.ReadSingle(), reader.ReadSingle(), reader.ReadSingle()));
            }

            Triangles = new List<Triangle>();
            ushort triangleCount = reader.ReadUInt16();
            for (ushort i = 0; i < triangleCount; i++)
            {
                Triangles.Add(new Triangle(reader));
                if (Triangles[Triangles.Count - 1].Unk01 != 1)
                    Console.WriteLine("    MATERIAL ANOMALY: " + Triangles[Triangles.Count - 1].Unk01);
            }
        }

        public void Write(BinaryWriter writer)
        {
            writer.Write((uint)Vertices.Count);
            foreach (Vector3 v in Vertices)
            {
                writer.Write(v.X);
                writer.Write(v.Y);
                writer.Write(v.Z);
            }

            writer.Write((uint)Triangles.Count);
            foreach (Triangle t in Triangles)
            {
                t.Write(writer);
            }
        }

        public void ExportOBJ(string filename)
        {
            using (StreamWriter writer = new StreamWriter(filename, false))
            using (StreamWriter matWriter = new StreamWriter(Path.ChangeExtension(filename, ".mtl"), false))
            {
                writer.WriteLine("mtllib " + Path.GetFileName(Path.ChangeExtension(filename, ".mtl")));

                foreach (Vector3 v in Vertices)
                    writer.WriteLine("v " + v.X + " " + v.Y + " " + v.Z);

                int lastMaterial = -1;
                List<int> writtenMaterials = new List<int>(); // Keep track of which materials have already been written
                foreach (Triangle t in Triangles)
                {
                    if (lastMaterial != t.Unk01)
                    {
                        writer.WriteLine("usemtl Material_" + t.Unk01);
                        lastMaterial = t.Unk01;

                        if (!writtenMaterials.Contains(t.Unk01))
                        {
                            matWriter.WriteLine("newmtl Material_" + t.Unk01);
                            writtenMaterials.Add(t.Unk01);
                        }
                    }
                    writer.WriteLine("f " + (t.Index1 + 1) + " " + (t.Index2 + 1) + " " + (t.Index3 + 1));
                }
            }
        }
    }
}
