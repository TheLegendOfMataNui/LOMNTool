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

                XObject meshNormals = null;
                XObject meshTextureCoords = null;
                XObject meshMaterialList = null;

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
                        Vector3 pos = Vector((XObjectStructure)mesh["vertices"].Values[i]);
                        Vector4 pos2 = Vector3.Transform(pos, transform);
                        writer.WriteLine("v " + pos2.X + " " + pos2.Y + " " + pos2.Z);
                    }

                    // Gather normals
                    int normalCount = (int)meshNormals["nNormals"].Values[0];
                    for (int i = 0; i < normalCount; i++)
                    {
                        Vector3 norm = Vector((XObjectStructure)meshNormals["normals"].Values[i]);
                        Vector4 norm2 = Vector3.Transform(norm, transform);
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

            public static XFile ImportOBJ(string filename, Matrix transform, bool flipV = true, bool removeTextureExtension = true)
            {
                XFile result = new XFile(new XHeader(3, 3, XHeader.HeaderFormat.Binary, 32));

                result.Templates.Add(XReader.NativeTemplates["XSkinMeshHeader"]);
                result.Templates.Add(XReader.NativeTemplates["VertexDuplicationIndices"]);
                result.Templates.Add(XReader.NativeTemplates["SkinWeights"]);

                XTemplate vectorTemplate = XReader.NativeTemplates["Vector"];

                XObject FrameObject = new XObject(new XToken(XToken.TokenID.NAME) { NameData = "Frame" }, "Root");
                XObject FrameTransformMatrix = new XObject(new XToken(XToken.TokenID.NAME) { NameData = "FrameTransformMatrix" });
                FrameTransformMatrix.Members.Add(new XObjectMember("frameMatrix", new XToken(XToken.TokenID.NAME) { NameData = "Matrix4x4" }, new XObjectStructure(XReader.NativeTemplates["Matrix4x4"], new XObjectMember("matrix", new XToken(XToken.TokenID.FLOAT), 1.0, 0.0, 0.0, 0.0, 0.0, 1.0, 0.0, 0.0, 0.0, 0.0, 1.0, 0.0, 0.0, 0.0, 0.0, 1.0))));
                FrameObject.Children.Add(new XChildObject(FrameTransformMatrix, false));

                // Read the obj file as one whole mesh
                XObject MeshObject = XReader.NativeTemplates["Mesh"].Instantiate();
                FrameObject.Children.Add(new XChildObject(MeshObject, false));
                XObject MeshNormalsObject = XReader.NativeTemplates["MeshNormals"].Instantiate();
                MeshObject.Children.Add(new XChildObject(MeshNormalsObject, false));
                XObject MeshTextureCoordsObject = XReader.NativeTemplates["MeshTextureCoords"].Instantiate();
                MeshObject.Children.Add(new XChildObject(MeshTextureCoordsObject, false));
                XObject MeshMaterialListObject = XReader.NativeTemplates["MeshMaterialList"].Instantiate();
                MeshObject.Children.Add(new XChildObject(MeshMaterialListObject, false));

                // Store the positions and UVs so we can weld the pairs together later
                List<Vector3> positions = new List<Vector3>();
                List<Vector2> uvs = new List<Vector2>();
                List<List<Tuple<int, int>>> faces = new List<List<Tuple<int, int>>>(); // list of faces (list of <Pos index, UV index>). Not pretty, but it works.

                List<string> materialNames = new List<string>(); // Store the names of materials so we can look up indexes to them.
                int currentMaterial = 0;
                using (System.IO.StreamReader reader = new System.IO.StreamReader(filename))
                {
                    while (!reader.EndOfStream)
                    {
                        string line = reader.ReadLine().Trim();
                        if (String.IsNullOrEmpty(line))
                            continue;

                        string[] parts = line.Split(new char[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);
                        
                        if (parts[0] == "v")
                        {
                            Vector4 v = Vector3.Transform(new Vector3(Single.Parse(parts[1]), Single.Parse(parts[2]), Single.Parse(parts[3])), transform);
                            //MeshObject["vertices"].Values.Add(Vector(new Vector3(v.X, v.Y, v.Z)));
                            positions.Add(new Vector3(v.X, v.Y, v.Z));
                        }
                        else if (parts[0] == "vn")
                        {
                            Vector4 v = Vector4.Transform(new Vector4(Single.Parse(parts[1]), Single.Parse(parts[2]), Single.Parse(parts[3]), 0.0f), transform);
                            MeshNormalsObject["normals"].Values.Add(Vector(new Vector3(v.X, v.Y, v.Z)));
                        }
                        else if (parts[0] == "vt")
                        {
                            Vector2 coords = new Vector2(Single.Parse(parts[1]), Single.Parse(parts[2]));
                            if (flipV)
                                coords.Y = 1.0f - coords.Y;
                            //MeshTextureCoordsObject["textureCoords"].Values.Add(TexCoord(coords));
                            uvs.Add(coords);
                        }
                        else if (parts[0] == "f")
                        {
                            // Handle each vertex in the polygon
                            //List<int> posIndices = new List<int>();
                            List<int> normIndices = new List<int>();
                            //List<int> uvIndices = new List<int>();
                            List<Tuple<int, int>> face = new List<Tuple<int, int>>();

                            for (int i = 1; i < parts.Length; i++)
                            {
                                string[] components = parts[i].Split('/');
                                int pindex = Int32.Parse(components[0]) - 1;
                                int uvindex = -1;
                                if (components.Length > 1 && components[1].Length > 0)
                                    uvindex = Int32.Parse(components[1]) - 1;
                                if (components.Length > 2 && components[2].Length > 0)
                                    normIndices.Add(Int32.Parse(components[2]) - 1);

                                face.Add(new Tuple<int, int>(pindex, uvindex));
                            }

                            //MeshObject["faces"].Values.Add(Face(posIndices));
                            if (normIndices.Count > 0)
                                MeshNormalsObject["faceNormals"].Values.Add(Face(normIndices));
                            // Texture coordinates are directly linked to vertices

                            faces.Add(face);

                            MeshMaterialListObject["faceIndexes"].Values.Add(currentMaterial);
                        }
                        else if (parts[0] == "usemtl")
                        {
                            string matName = parts[1];
                            if (materialNames.Contains(matName))
                                currentMaterial = materialNames.IndexOf(matName);
                            else
                                Console.WriteLine("    [WARNING]: OBJ Material '" + matName + "' not found.");
                        }
                        else if (parts[0] == "mtllib")
                        {
                            // Parse material library!
                            using (System.IO.StreamReader mtlReader = new System.IO.StreamReader(System.IO.Path.Combine(System.IO.Path.GetDirectoryName(filename), parts[1])))
                            {
                                XObject MaterialObject = null;

                                while (!mtlReader.EndOfStream)
                                {
                                    string mtlLine = mtlReader.ReadLine().Trim();

                                    if (String.IsNullOrEmpty(mtlLine))
                                        continue;

                                    string[] mtlParts = mtlLine.Split(' ');

                                    if (mtlParts[0] == "newmtl")
                                    {
                                        if (MaterialObject != null)
                                            MeshMaterialListObject.Children.Add(new XChildObject(MaterialObject, false));
                                        MaterialObject = XReader.NativeTemplates["Material"].Instantiate();
                                        MaterialObject["faceColor"].Values.Add(ColorRGBA(1.0, 1.0, 1.0, 1.0));
                                        MaterialObject["specularColor"].Values.Add(ColorRGB(1.0, 1.0, 1.0));
                                        MaterialObject["power"].Values.Add(0.0);
                                        MaterialObject["emissiveColor"].Values.Add(ColorRGB(0.0, 0.0, 0.0));
                                        materialNames.Add(mtlLine.Substring(7));
                                    }
                                    else if (mtlParts[0] == "Kd")
                                    {
                                        MaterialObject["faceColor"].Values[0] = (ColorRGBA(Double.Parse(mtlParts[1]), Double.Parse(mtlParts[2]), Double.Parse(mtlParts[3]), 1.0));
                                    }
                                    else if (mtlParts[0] == "Ks")
                                    {
                                        MaterialObject["specularColor"].Values[0] = (ColorRGB(Double.Parse(mtlParts[1]), Double.Parse(mtlParts[2]), Double.Parse(mtlParts[3])));
                                    }
                                    else if (mtlParts[0] == "Ke")
                                    {
                                        MaterialObject["emissiveColor"].Values[0] = (ColorRGB(Double.Parse(mtlParts[1]), Double.Parse(mtlParts[2]), Double.Parse(mtlParts[3])));
                                    }
                                    else if (mtlParts[0] == "Ns")
                                    {
                                        MaterialObject["power"].Values[0] = (Double.Parse(mtlParts[1]));
                                    }
                                    else if (mtlParts[0] == "Tr")
                                    {
                                        (MaterialObject["faceColor"].Values[0] as XObjectStructure)["alpha"].Values[0] = 1.0 - Double.Parse(mtlParts[1]);
                                    }
                                    else if (mtlParts[0] == "d")
                                    {
                                        (MaterialObject["faceColor"].Values[0] as XObjectStructure)["alpha"].Values[0] = Double.Parse(mtlParts[1]);
                                    }
                                    else if (mtlParts[0] == "map_Kd")
                                    {
                                        XObject texFilename = XReader.NativeTemplates["TextureFilename"].Instantiate();
                                        string texname = mtlLine.Substring(7);
                                        if (removeTextureExtension)
                                            texname = System.IO.Path.GetFileNameWithoutExtension(texname);
                                        texFilename["filename"].Values.Add(texname);
                                        MaterialObject.Children.Add(new XChildObject(texFilename, false));
                                    }
                                }

                                if (MaterialObject != null)
                                    MeshMaterialListObject.Children.Add(new XChildObject(MaterialObject, false));
                            }
                        }
                        else
                        {
                            System.Diagnostics.Debug.WriteLine("    [INFO]: Ignored OBJ line '" + line + "'.");
                        }
                    }
                }

                // Weld all the used combinations of <position, uv>.
                List<List<int>> newFaces = null;
                List<Tuple<Vector3, Vector2>> newPositionUVs = WeldTextureCoordinates(positions, uvs, faces, out newFaces);
                foreach (Tuple<Vector3, Vector2> vdata in newPositionUVs)
                {
                    MeshObject["vertices"].Values.Add(Vector(vdata.Item1));
                    MeshTextureCoordsObject["textureCoords"].Values.Add(TexCoord(vdata.Item2));
                }
                foreach (List<int> face in newFaces)
                {
                    MeshObject["faces"].Values.Add(Face(face));
                }

                // Fix all the counts.
                MeshObject["nVertices"].Values.Add(MeshObject["vertices"].Values.Count);
                MeshObject["nFaces"].Values.Add(MeshObject["faces"].Values.Count);
                MeshNormalsObject["nNormals"].Values.Add(MeshNormalsObject["normals"].Values.Count);
                MeshNormalsObject["nFaceNormals"].Values.Add(MeshNormalsObject["faceNormals"].Values.Count);
                MeshTextureCoordsObject["nTextureCoords"].Values.Add(MeshTextureCoordsObject["textureCoords"].Values.Count);
                MeshMaterialListObject["nMaterials"].Values.Add(MeshMaterialListObject.Children.Count); // Because MeshMaterialList is a restricted template, all children are guaranteed to be Material objects.
                MeshMaterialListObject["nFaceIndexes"].Values.Add(MeshMaterialListObject["faceIndexes"].Values.Count);

                result.Objects.Add(FrameObject);

                return result;
            }

            public static Vector3 Vector(XObjectStructure vector)
            {
                return new Vector3((float)(double)vector["x"].Values[0], (float)(double)vector["y"].Values[0], (float)(double)vector["z"].Values[0]);
            }

            public static XObjectStructure Vector(Vector3 vector)
            {
                return new XObjectStructure(XReader.NativeTemplates["Vector"], 
                    new XObjectMember("x", new XToken(XToken.TokenID.FLOAT), vector.X),
                    new XObjectMember("y", new XToken(XToken.TokenID.FLOAT), vector.Y),
                    new XObjectMember("z", new XToken(XToken.TokenID.FLOAT), vector.Z));
            }

            public static Vector2 TexCoord(XObjectStructure coords2d)
            {
                return new Vector2((float)(double)coords2d["u"].Values[0], (float)(double)coords2d["v"].Values[0]);
            }

            public static XObjectStructure TexCoord(Vector2 uv)
            {
                return new XObjectStructure(XReader.NativeTemplates["Coords2d"],
                    new XObjectMember("u", new XToken(XToken.TokenID.FLOAT), uv.X),
                    new XObjectMember("v", new XToken(XToken.TokenID.FLOAT), uv.Y));
            }

            public static XObjectStructure Face(List<int> indices)
            {
                return new XObjectStructure(XReader.NativeTemplates["MeshFace"], 
                    new XObjectMember("nFaceVertexIndices", new XToken(XToken.TokenID.DWORD), indices.Count),
                    new XObjectMember("faceVertexIndices", new XToken(XToken.TokenID.DWORD), indices.Cast<object>().ToArray()));
            }

            public static XObjectStructure ColorRGB(double r, double g, double b)
            {
                return new XObjectStructure(XReader.NativeTemplates["ColorRGB"],
                    new XObjectMember("red", new XToken(XToken.TokenID.FLOAT), r),
                    new XObjectMember("green", new XToken(XToken.TokenID.FLOAT), g),
                    new XObjectMember("blue", new XToken(XToken.TokenID.FLOAT), b));
            }

            public static XObjectStructure ColorRGBA(double r, double g, double b, double a)
            {
                return new XObjectStructure(XReader.NativeTemplates["ColorRGB"],
                    new XObjectMember("red", new XToken(XToken.TokenID.FLOAT), r),
                    new XObjectMember("green", new XToken(XToken.TokenID.FLOAT), g),
                    new XObjectMember("blue", new XToken(XToken.TokenID.FLOAT), b),
                    new XObjectMember("alpha", new XToken(XToken.TokenID.FLOAT), a));
            }

            // This might be the ugliest C# code I've ever written. Just look at that signature! Ugh!
            public static List<Tuple<Vector3, Vector2>> WeldTextureCoordinates(List<Vector3> positions, List<Vector2> uvs, List<List<Tuple<int, int>>> indices, out List<List<int>> newIndices)
            {
                List<Tuple<Vector3, Vector2>> results = new List<Tuple<Vector3, Vector2>>();
                newIndices = new List<List<int>>();

                foreach (List<Tuple<int, int>> face in indices)
                {
                    List<int> newFace = new List<int>();
                    foreach (Tuple<int, int> indexPair in face)
                    {
                        if (!results.Contains(new Tuple<Vector3, Vector2>(positions[indexPair.Item1], uvs[indexPair.Item2])))
                        {
                            results.Add(new Tuple<Vector3, Vector2>(positions[indexPair.Item1], uvs[indexPair.Item2]));
                            newFace.Add(results.Count - 1);
                        }
                        else
                        {
                            newFace.Add(results.IndexOf(new Tuple<Vector3, Vector2>(positions[indexPair.Item1], uvs[indexPair.Item2])));
                        }
                    }
                    newIndices.Add(newFace);
                }

                return results;
            }
        }
    }
}
