using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.IO;
using SharpDX;

namespace LOMNTool
{
    /// <summary>
    /// A file that stores a model's skeleton.
    /// </summary>
    public class BHDFile
    {
        public static string[] BipedBoneNames =
        {
            "Bip01",
            "Bip01_Pelvis",
            "Bip01_Spine",
            "Bip01_Spine1",
            "Bip01_Spine2",
            "Bip01_Spine3",
            "Bip01_R_Thigh",
            "Bip01_L_Thigh",
            "Bip01_R_Calf",
            "Bip01_L_Calf",
            "Bip01_R_Foot",
            "Bip01_L_Foot",
            "Bip01_R_Clavicle",
            "Bip01_L_Clavicle",
            "Bip01_R_UpperArm",
            "Bip01_L_UpperArm",
            "Bip01_R_Forearm",
            "Bip01_L_Forearm",
            "Bip01_R_Hand",
            "Bip01_L_Hand",
            "Bip01_Neck",
            "Bip01_Neck1",
            "Bip01_Head",
            "Bip01_R_Finger0",
            "Bip01_L_Finger0",
            "Bip01_R_Finger1",
            "Bip01_L_Finger1",
            "Bip01_R_Finger2",
            "Bip01_L_Finger2",
            "Bip01_R_Finger3",
            "Bip01_L_Finger3",
            "Bip01_R_Finger4",
            "Bip01_L_Finger4",
            "Bip01_R_Finger01",
            "Bip01_L_Finger01",
            "Bip01_R_Finger11",
            "Bip01_L_Finger11",
            "Bip01_R_Finger21",
            "Bip01_L_Finger21",
            "Bip01_R_Finger31",
            "Bip01_L_Finger31",
            "Bip01_R_Finger41",
            "Bip01_L_Finger41",
            "Bip01_R_Finger02",
            "Bip01_L_Finger02",
            "Bip01_R_Finger12",
            "Bip01_L_Finger12",
            "Bip01_R_Finger22",
            "Bip01_L_Finger22",
            "Bip01_R_Finger32",
            "Bip01_L_Finger32",
            "Bip01_R_Finger42",
            "Bip01_L_Finger42",
            "Bip01_R_Toe0",
            "Bip01_L_Toe0",
            "Bip01_R_Toe1",
            "Bip01_L_Toe1",
            "Bip01_R_Toe2",
            "Bip01_L_Toe2",
            "Bip01_R_Toe01",
            "Bip01_L_Toe01",
            "Bip01_R_Toe11",
            "Bip01_L_Toe11",
            "Bip01_R_Toe21",
            "Bip01_L_Toe21",
            "Bip01_R_Toe3",
            "Bip01_L_Toe3",
            "Bip01_R_Toe31",
            "Bip01_L_Toe31",
            "Bip01_R_Toe4",
            "Bip01_L_Toe4",
            "Bip01_R_Toe41",
            "Bip01_L_Toe41",
            "MiscBone0",
            "MiscBone1",
            "MiscBone2",
            "MiscBone3",
            "MiscBone4",
            "MiscBone5",
            "MiscBone6",
            "MiscBone7",
            "MiscBone8",
            "MiscBone9",
            "MiscBone10",
            "MiscBone11",
            "MiscBone12",
            "MiscBone13",
            "MiscBone14",
            "MiscBone15",
            "MiscBone16",
            "MiscBone17",
            "MiscBone18",
            "Bip01_Tail4",
            "Bip01_Tail3",
            "Bip01_Tail2",
            "Bip01_Neck4",
            "Bip01_Neck3",
            "Bip01_Neck2",
            "Bip01_Tail",
            "Bip01_Tail1"
        };

        public class Bone
        {
            public uint Index;
            public uint ParentIndex; // 0xFFFFFFFF means this bone name isn't used.
            public Matrix Transform;

            public List<Bone> Children = new List<Bone>(); // Not stored in the file, just computed to be helpful.
        }

        public List<Bone> Bones = new List<Bone>();

        public BHDFile()
        {

        }

        public BHDFile(string filename)
        {
            using (FileStream stream = new FileStream(filename, FileMode.Open, FileAccess.Read, FileShare.Read))
            using (BinaryReader reader = new BinaryReader(stream))
            {
                uint count = reader.ReadUInt32();
                for (uint i = 0; i < count; i++)
                {
                    Bone b = new Bone();
                    b.ParentIndex = reader.ReadUInt32();
                    b.Index = i;
                    b.Transform = Matrix.Identity;
                    Bones.Add(b);
                }
                for (int i = 0; i < count; i++)
                {
                    Bone b = Bones[i];
                    if (b.ParentIndex != 0xFFFFFFFF && b.ParentIndex != i)
                    {
                        Bones[(int)b.ParentIndex].Children.Add(b);
                    }

                    /*b.Transform = new Matrix(reader.ReadSingle(), reader.ReadSingle(), reader.ReadSingle(), 0.0f,
                                                reader.ReadSingle(), reader.ReadSingle(), reader.ReadSingle(), 0.0f,
                                                reader.ReadSingle(), reader.ReadSingle(), reader.ReadSingle(), 0.0f,
                                                reader.ReadSingle(), reader.ReadSingle(), reader.ReadSingle(), 1.0f);*/
                    /*b.Transform = new Matrix(reader.ReadSingle(), reader.ReadSingle(), reader.ReadSingle(), reader.ReadSingle(),
                                                reader.ReadSingle(), reader.ReadSingle(), reader.ReadSingle(), reader.ReadSingle(),
                                                reader.ReadSingle(), reader.ReadSingle(), reader.ReadSingle(), reader.ReadSingle(),
                                                0.0f, 0.0f, 0.0f, 1.0f);*/
                    b.Transform.Column1 = new Vector4(reader.ReadSingle(), reader.ReadSingle(), reader.ReadSingle(), 0.0f);
                    b.Transform.Column2 = new Vector4(reader.ReadSingle(), reader.ReadSingle(), reader.ReadSingle(), 0.0f);
                    b.Transform.Column3 = new Vector4(reader.ReadSingle(), reader.ReadSingle(), reader.ReadSingle(), 0.0f);
                    b.Transform.Column4 = new Vector4(reader.ReadSingle(), reader.ReadSingle(), reader.ReadSingle(), 1.0f);
                }
            }
        }

        public void Write(string filename)
        {
            using (FileStream stream = new FileStream(filename, FileMode.Create, FileAccess.Write, FileShare.Read))
            using (BinaryWriter writer = new BinaryWriter(stream))
            {
                writer.Write((uint)Bones.Count);
                foreach (Bone b in Bones)
                {
                    writer.Write(b.ParentIndex);
                }
                foreach (Bone b in Bones)
                {
                    writer.Write(b.Transform.M11);
                    writer.Write(b.Transform.M12);
                    writer.Write(b.Transform.M13);
                    writer.Write(b.Transform.M21);
                    writer.Write(b.Transform.M22);
                    writer.Write(b.Transform.M23);
                    writer.Write(b.Transform.M31);
                    writer.Write(b.Transform.M32);
                    writer.Write(b.Transform.M33);
                    writer.Write(b.Transform.M41);
                    writer.Write(b.Transform.M42);
                    writer.Write(b.Transform.M43);
                }
            }
        }
    }
}
