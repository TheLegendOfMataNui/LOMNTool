using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.IO;
using SharpDX;

namespace LOMNTool
{
    public class OCLFile
    {
        public class OctreeNode
        {
            public enum NodeType : byte
            {
                Branch = 0,
                Leaf = 1
            }

            public class Triangle
            {
                public Vector3 Normal;
                public float OddThing; // Normal.X * Position1.X - Normal.Y * Position1.Y + Normal.Z * Position1.Z
                public float Unk05; // Some obscure formula I can't discern
                public Vector3 Position1;
                public Vector3 Position2;
                public Vector3 Position3;
                public Vector3 One; // Always 1.0f, 1.0f, 1.0f
                public uint Unk09; // 0x48
                public uint Unk10; // 0x4C

                public Triangle(BinaryReader reader)
                {
                    Normal = new Vector3(reader.ReadSingle(), reader.ReadSingle(), reader.ReadSingle());
                    OddThing = reader.ReadSingle();
                    Unk05 = reader.ReadSingle();
                    Position1 = new Vector3(reader.ReadSingle(), reader.ReadSingle(), reader.ReadSingle());
                    Position2 = new Vector3(reader.ReadSingle(), reader.ReadSingle(), reader.ReadSingle());
                    Position3 = new Vector3(reader.ReadSingle(), reader.ReadSingle(), reader.ReadSingle());
                    One = new Vector3(reader.ReadSingle(), reader.ReadSingle(), reader.ReadSingle());
                    Unk09 = reader.ReadUInt32();
                    Unk10 = reader.ReadUInt32();
                }

                public override string ToString()
                {
                    return "Unk04: " + OddThing + ", Unk05: " + Unk05 + ", Unk09: " + Unk09.ToString("X8") + ", Unk10: " + Unk10.ToString("X8");
                }
            }

            public class AdjacencyData
            {

            }

            public ushort Index;
            public List<Triangle> Triangles = new List<Triangle>();
            public uint TriangleDataOffset;
            public uint pPolyList = 0; // Always 0x00000000
            public OctreeNode[] Children = new OctreeNode[8];
            public uint pUnk03; // dword_40 // When Type == Leaf, has a value, otherwise, null. Offset of 6 pointers.
            public NodeType Type;
            public Vector3 Min;
            public Vector3 Max;

            public OctreeNode(BinaryReader reader)
            {
                Index = reader.ReadUInt16();
                int triangleCount = reader.ReadUInt16(); // 2bytes padding
                TriangleDataOffset = reader.ReadUInt32();
                pPolyList = reader.ReadUInt32();

                // Read triangles
                long pos = reader.BaseStream.Position;
                reader.BaseStream.Position = TriangleDataOffset;
                for (uint i = 0; i < triangleCount; i++)
                {
                    Triangles.Add(new Triangle(reader));
                }
                reader.BaseStream.Position = pos;

                // Read children
                for (int i = 0; i < 8; i++)
                {
                    uint offset = reader.ReadUInt32();
                    if (offset != 0)
                    { 
                        long p = reader.BaseStream.Position;
                        reader.BaseStream.Position = offset;
                        Children[i] = new OctreeNode(reader);
                        reader.BaseStream.Position = p;
                    }
                }
                pUnk03 = reader.ReadUInt32();
                Type = (NodeType)reader.ReadByte();
                reader.ReadBytes(3); // 3bytes padding
                Min = new Vector3(reader.ReadSingle(), reader.ReadSingle(), reader.ReadSingle());
                Max = new Vector3(reader.ReadSingle(), reader.ReadSingle(), reader.ReadSingle());
            }
        }

        public OctreeNode RootNode = null;

        public OCLFile(BinaryReader reader)
        {
            reader.BaseStream.Position = 0x1C;
            RootNode = new OctreeNode(reader);
        }

        /// <summary>
        /// Dumps a representation of this file to the debug stream.
        /// </summary>
        /// <param name="node"></param>
        /// <param name="prefix"></param>
        public void LogDebug(OctreeNode node = null, string prefix = "  ")
        {
            if (node == null)
                node = RootNode;

            System.Diagnostics.Debug.WriteLine(prefix + "Min: " + node.Min.ToString() + ", Max: " + node.Max.ToString());
            System.Diagnostics.Debug.WriteLine(prefix + "Index: " + node.Index.ToString("X4") + ", TriangleDataOffset: " + node.TriangleDataOffset.ToString("X8") + ", pPolyList: " + node.pPolyList.ToString("X8") + ", pUnk03: " + node.pUnk03.ToString("X8") + ", Node Type: " + node.Type.ToString());
            System.Diagnostics.Debug.WriteLine(prefix + node.Triangles.Count.ToString("X8") + " Triangles:");
            foreach (OctreeNode.Triangle t in node.Triangles)
                System.Diagnostics.Debug.WriteLine(prefix + " - " + t.ToString());

            for (int i = 0; i < node.Children.Length; i++)
            {
                if (node.Children[i] != null)
                    LogDebug(node.Children[i], prefix + "  ");
            }
        }
    }
}
