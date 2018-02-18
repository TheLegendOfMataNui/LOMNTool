using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using SharpDX;

namespace D3DX
{
    namespace Mesh
    {
        public static class XUtils
        {
            public static void ExportOBJ(XObject mesh, string filename, Matrix transform, bool flipV = true, string textureExtension = null)
            {
                if (mesh.DataType.NameData != "Mesh")
                    throw new ArgumentException("'mesh' must be a Mesh object!");

                int vertexCount = (int)mesh["nVertices"].Values[0];
                int faceCount = (int)mesh["nFaces"].Values[0];

                XObject meshNormals = null;// = mesh[0].Object;
                XObject meshTextureCoords = null;// = mesh[2].Object;
                XObject meshMaterialList = null;// = mesh[3].Object;

                foreach (XChildObject child in mesh.Children)
                {
                    if (child.Object.DataType.NameData == "MeshNormals")
                        meshNormals = child.Object;
                    else if (child.Object.DataType.NameData == "MeshTextureCoords")
                        meshTextureCoords = child.Object;
                    else if (child.Object.DataType.NameData == "MeshMaterialList")
                        meshMaterialList = child.Object;
                }

                using (System.IO.StreamWriter writer = new System.IO.StreamWriter(filename, false))
                using (System.IO.StreamWriter matWriter = new System.IO.StreamWriter(System.IO.Path.ChangeExtension(filename, ".mtl")))
                {
                    writer.WriteLine("mtllib " + System.IO.Path.GetFileNameWithoutExtension(filename) + ".mtl");
                    // Write materials
                    for (int i = 0; i < (int)meshMaterialList["nMaterials"].Values[0]; i++)
                    {
                        matWriter.WriteLine("newmtl Material" + i.ToString().PadLeft(3, '0'));
                        XObject material = meshMaterialList[i].Object;
                        XObjectStructure faceColor = (XObjectStructure)material["faceColor"].Values[0];
                        float specExponent = (float)(double)material["power"].Values[0];
                        XObjectStructure specularColor = (XObjectStructure)material["specularColor"].Values[0];
                        XObjectStructure emissiveColor = (XObjectStructure)material["emissiveColor"].Values[0];

                        matWriter.WriteLine("Kd " + (double)faceColor["red"].Values[0] + " " + (double)faceColor["green"].Values[0] + " " + (double)faceColor["blue"].Values[0]);
                        matWriter.WriteLine("d " + (double)faceColor["alpha"].Values[0]);
                        matWriter.WriteLine("Tr " + (1.0 - (double)faceColor["alpha"].Values[0]));

                        matWriter.WriteLine("Ns " + specExponent);
                        // Hack for games with white spec color but zero exponent that means no spec
                        if (specExponent > 0.0)
                            matWriter.WriteLine("Ks " + (double)specularColor["red"].Values[0] + " " + (double)specularColor["green"].Values[0] + " " + (double)specularColor["blue"].Values[0]);
                        else
                            matWriter.WriteLine("Ks 0 0 0");

                        matWriter.WriteLine("Ke " + (double)emissiveColor["red"].Values[0] + " " + (double)emissiveColor["green"].Values[0] + " " + (double)emissiveColor["blue"].Values[0]);

                        // Look for the possible TextureFilename
                        foreach (XChildObject child in material.Children)
                        {
                            if (child.Object.DataType.NameData == "TextureFilename")
                            {
                                string texFilename = (string)child.Object["filename"].Values[0];
                                if (textureExtension != null)
                                    texFilename = System.IO.Path.ChangeExtension(texFilename, textureExtension);
                                matWriter.WriteLine("map_Kd " + texFilename);
                                break;
                            }
                        }
                    }

                    // Gather positions
                    for (int i = 0; i < vertexCount; i++)
                    {
                        //Positions.Add(Vector((XObjectStructure)mesh["vertices"].Values[i]));
                        Vector3 pos = Vector((XObjectStructure)mesh["vertices"].Values[i]);
                        Vector4 pos2 = Vector3.Transform(pos, transform);
                        writer.WriteLine("v " + pos2.X + " " + pos2.Y + " " + pos2.Z);
                    }

                    // Gather normals
                    int normalCount = (int)meshNormals["nNormals"].Values[0];
                    for (int i = 0; i < normalCount; i++)
                    {
                        //Normals.Add(Vector((XObjectStructure)meshNormals["normals"].Values[i]));
                        Vector3 norm = Vector((XObjectStructure)meshNormals["normals"].Values[i]);
                        Vector4 norm2 = Vector3.Transform(norm, transform); // This is correct because ToYUp has no translation
                        writer.WriteLine("vn " + norm2.X + " " + norm2.Y + " " + norm2.Z);
                    }

                    // Gather texture coordinates
                    int uvCount = (int)meshTextureCoords["nTextureCoords"].Values[0];
                    if (uvCount != vertexCount)
                        throw new FormatException("Different number of vertices and texture coordinates!");
                    for (int i = 0; i < uvCount; i++)
                    {
                        Vector2 uv = TexCoord((XObjectStructure)meshTextureCoords["textureCoords"].Values[i]);
                        if (flipV)
                            uv.Y = 1.0f - uv.Y;
                        writer.WriteLine("vt " + uv.X + " " + uv.Y);
                    }

                    // Write each face
                    int mtl = -1;
                    for (int i = 0; i < faceCount; i++)
                    {
                        XObjectStructure face = (XObjectStructure)mesh["faces"].Values[i];
                        XObjectStructure faceNormals = (XObjectStructure)meshNormals["faceNormals"].Values[i];

                        int newMaterialIndex = (int)meshMaterialList["faceIndexes"].Values[i];
                        if (newMaterialIndex != mtl)
                        {
                            writer.WriteLine("usemtl Material" + newMaterialIndex.ToString().PadLeft(3, '0'));
                            mtl = newMaterialIndex;
                        }

                        writer.Write("f ");
                        for (int v = 0; v < (int)face["nFaceVertexIndices"].Values[0]; v++)
                        {
                            int vIndex = (int)face["faceVertexIndices"].Values[v] + 1;
                            int nIndex = (int)faceNormals["faceVertexIndices"].Values[v] + 1;

                            writer.Write(vIndex + "/" + vIndex + "/" + nIndex + " ");
                        }
                        writer.WriteLine();
                    }
                }
            }

            public static Vector3 Vector(XObjectStructure vector)
            {
                return new Vector3((float)(double)vector["x"].Values[0], (float)(double)vector["y"].Values[0], (float)(double)vector["z"].Values[0]);
            }

            public static Vector2 TexCoord(XObjectStructure coords2d)
            {
                return new Vector2((float)(double)coords2d["u"].Values[0], (float)(double)coords2d["v"].Values[0]);
            }
        }
    }
}
