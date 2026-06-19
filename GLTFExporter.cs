using System;
using System.Collections.Generic;
using System.Linq;
using SharpGLTF.Scenes;
using SharpGLTF.Geometry;
using SharpGLTF.Geometry.VertexTypes;
using SharpGLTF.Materials;
using D3DX.Mesh;
using SharpDX;

namespace LOMNTool.GLTF
{
    using StaticVertexBuilder = VertexBuilder<VertexPositionNormal, VertexColor1Texture1, VertexEmpty>;
    using SkinnedVertexBuilder = VertexBuilder<VertexPositionNormal, VertexColor1Texture1, VertexJoints4>;

    public static class GLTFExporter
    {
        public static void Export(XFile xFile, BHDFile bhd, string outputPath)
        {
            if (bhd != null)
            {
                ExportSkinned(xFile, bhd, outputPath);
            }
            else
            {
                ExportStatic(xFile, outputPath);
            }
        }

        // BATCH EXPORT: Groups multiple XFiles and their distinct objects into a single GLTF scene
        public static void ExportCombined(List<XFile> xFiles, List<string> names, BHDFile bhd, string outputPath)
        {
            var scene = new SceneBuilder();
            NodeBuilder[] gltfNodes = null;
            string combinedName = System.IO.Path.GetFileNameWithoutExtension(outputPath);

            if (bhd != null)
            {
                string[] nameSlots = bhd.NameSlots ?? DeduceNameSlots(xFiles[0]);

                gltfNodes = new NodeBuilder[bhd.Bones.Count];
                for (int i = 0; i < bhd.Bones.Count; i++)
                {
                    gltfNodes[i] = new NodeBuilder(nameSlots[i]);
                    gltfNodes[i].LocalTransform = ConvertBhdMatrix(bhd.Bones[i].Transform);
                }

                // Append _Root to prevent namespace collision with the mesh
                var masterSkeletonRoot = new NodeBuilder(combinedName + "_Root");
                scene.AddNode(masterSkeletonRoot);

                for (int i = 0; i < bhd.Bones.Count; i++)
                {
                    if (bhd.Bones[i].ParentIndex != 0xFFFFFFFF && bhd.Bones[i].ParentIndex != i)
                    {
                        gltfNodes[bhd.Bones[i].ParentIndex].AddNode(gltfNodes[i]);
                    }
                    else
                    {
                        masterSkeletonRoot.AddNode(gltfNodes[i]);
                    }
                }
            }

            // Loop through all collected meshes and add them individually to the scene
            for (int i = 0; i < xFiles.Count; i++)
            {
                var xFile = xFiles[i];
                var baseName = names[i];

                if (bhd != null)
                {
                    var meshBuilder = new MeshBuilder<VertexPositionNormal, VertexColor1Texture1, VertexJoints4>(baseName);
                    ParseMeshes(xFile, null, meshBuilder, gltfNodes);
                    scene.AddSkinnedMesh(meshBuilder, System.Numerics.Matrix4x4.Identity, gltfNodes);
                }
                else
                {
                    var meshBuilder = new MeshBuilder<VertexPositionNormal, VertexColor1Texture1, VertexEmpty>(baseName);
                    ParseMeshes(xFile, meshBuilder, null, null);

                    // Append _Root to static mesh parent nodes
                    var rootNode = new NodeBuilder(baseName + "_Root");
                    scene.AddNode(rootNode);
                    scene.AddRigidMesh(meshBuilder, rootNode);
                }
            }

            var model = scene.ToGltf2();
            model.SaveGLB(outputPath); // CHANGED: SaveGLTF to SaveGLB
        }

        private static void ExportStatic(XFile xFile, string outputPath)
        {
            string baseName = System.IO.Path.GetFileNameWithoutExtension(outputPath);

            var scene = new SceneBuilder();
            var meshBuilder = new MeshBuilder<VertexPositionNormal, VertexColor1Texture1, VertexEmpty>(baseName);

            ParseMeshes(xFile, meshBuilder, null, null);

            // Append _Root to prevent namespace collision
            var rootNode = new NodeBuilder(baseName + "_Root");
            scene.AddNode(rootNode);
            scene.AddRigidMesh(meshBuilder, rootNode);

            var model = scene.ToGltf2();
            model.SaveGLB(outputPath); // CHANGED: SaveGLTF to SaveGLB
        }

        private static void ExportSkinned(XFile xFile, BHDFile bhd, string outputPath)
        {
            string baseName = System.IO.Path.GetFileNameWithoutExtension(outputPath);

            var scene = new SceneBuilder();
            var meshBuilder = new MeshBuilder<VertexPositionNormal, VertexColor1Texture1, VertexJoints4>(baseName);

            string[] nameSlots = bhd.NameSlots ?? DeduceNameSlots(xFile);

            var gltfNodes = new NodeBuilder[bhd.Bones.Count];
            for (int i = 0; i < bhd.Bones.Count; i++)
            {
                gltfNodes[i] = new NodeBuilder(nameSlots[i]);
                gltfNodes[i].LocalTransform = ConvertBhdMatrix(bhd.Bones[i].Transform);
            }

            // Append _Root to prevent namespace collision
            var masterSkeletonRoot = new NodeBuilder(baseName + "_Root");
            scene.AddNode(masterSkeletonRoot);

            for (int i = 0; i < bhd.Bones.Count; i++)
            {
                if (bhd.Bones[i].ParentIndex != 0xFFFFFFFF && bhd.Bones[i].ParentIndex != i)
                {
                    gltfNodes[bhd.Bones[i].ParentIndex].AddNode(gltfNodes[i]);
                }
                else
                {
                    masterSkeletonRoot.AddNode(gltfNodes[i]);
                }
            }

            ParseMeshes(xFile, null, meshBuilder, gltfNodes);

            scene.AddSkinnedMesh(meshBuilder, System.Numerics.Matrix4x4.Identity, gltfNodes);

            var model = scene.ToGltf2();
            model.SaveGLB(outputPath); // CHANGED: SaveGLTF to SaveGLB
        }

        private static void ParseMeshes(XFile xFile, IMeshBuilder<MaterialBuilder> staticBuilder, IMeshBuilder<MaterialBuilder> skinnedBuilder, NodeBuilder[] skeletonNodes)
        {
            foreach (var frame in xFile.Objects)
            {
                foreach (XChildObject frameChild in frame.Children)
                {
                    var obj = frameChild.Object;
                    if (obj.DataType.ID == XToken.TokenID.NAME && obj.DataType.NameData == "Mesh")
                    {
                        XObject meshNormals = null;
                        foreach (XChildObject child in obj.Children)
                            if (child.Object.DataType.NameData == "MeshNormals") meshNormals = child.Object;

                        var colorMap = new Dictionary<int, Vector4>();
                        foreach (XChildObject child in obj.Children)
                        {
                            if (child.Object.DataType.NameData == "MeshVertexColors")
                            {
                                foreach (XObjectStructure value in child.Object["vertexColors"].Values)
                                {
                                    int vIdx = (int)value["index"].Values[0];
                                    Vector4 color = D3DX.Mesh.XUtils.ColorRGBA((XObjectStructure)value.Members[1].Values[0]);
                                    colorMap[vIdx] = color;
                                }
                            }
                        }

                        var faceMaterials = new List<int>();
                        var materials = new List<MaterialBuilder>();

                        XObject meshMaterialList = null;
                        foreach (XChildObject child in obj.Children)
                            if (child.Object.DataType.NameData == "MeshMaterialList") meshMaterialList = child.Object;

                        if (meshMaterialList != null)
                        {
                            foreach (object matIdx in meshMaterialList["faceIndexes"].Values)
                                faceMaterials.Add((int)matIdx);

                            foreach (XChildObject matChild in meshMaterialList.Children)
                            {
                                if (matChild.Object.DataType.NameData == "Material")
                                {
                                    string texName = matChild.Object.Name ?? "dummy.dds";
                                    foreach (XChildObject texChild in matChild.Object.Children)
                                    {
                                        if (texChild.Object.DataType.NameData == "TextureFilename")
                                            texName = (string)texChild.Object["filename"].Values[0];
                                    }

                                    materials.Add(new MaterialBuilder(texName).WithMetallicRoughnessShader());
                                }
                            }
                        }

                        if (materials.Count == 0) materials.Add(new MaterialBuilder("dummy.dds").WithMetallicRoughnessShader());

                        var vertexWeights = new Dictionary<int, List<(int jointIndex, float weight)>>();
                        if (skinnedBuilder != null && skeletonNodes != null)
                        {
                            foreach (XChildObject child in obj.Children)
                            {
                                if (child.Object.DataType.NameData == "SkinWeights")
                                {
                                    string boneName = (string)child.Object["transformNodeName"].Values[0];
                                    int boneIndex = Array.FindIndex(skeletonNodes, n => n.Name == boneName);
                                    if (boneIndex == -1) continue;

                                    var indices = child.Object["vertexIndices"].Values;
                                    var weights = child.Object["weights"].Values;
                                    int nWeights = (int)child.Object["nWeights"].Values[0];

                                    for (int w = 0; w < nWeights; w++)
                                    {
                                        int vIdx = (int)indices[w];
                                        float weight = (float)(double)weights[w];
                                        if (!vertexWeights.ContainsKey(vIdx)) vertexWeights[vIdx] = new List<(int, float)>();
                                        vertexWeights[vIdx].Add((boneIndex, weight));
                                    }
                                }
                            }
                        }

                        int vertexCount = obj["vertices"].Values.Count;
                        int faceCount = (int)obj["nFaces"].Values[0];
                        var faces = obj["faces"].Values;

                        int[] lockedNormals = new int[vertexCount];
                        for (int i = 0; i < vertexCount; i++) lockedNormals[i] = -1;

                        for (int i = 0; i < faceCount; i++)
                        {
                            var face = (XObjectStructure)faces[i];
                            var vIndices = face["faceVertexIndices"].Values;
                            var nIndices = vIndices;

                            if (meshNormals != null && i < meshNormals["faceNormals"].Values.Count)
                            {
                                var fNormals = (XObjectStructure)meshNormals["faceNormals"].Values[i];
                                nIndices = fNormals["faceVertexIndices"].Values;
                            }

                            for (int v = 0; v < vIndices.Count; v++)
                            {
                                int vIdx = (int)vIndices[v];
                                int nIdx = v < nIndices.Count ? (int)nIndices[v] : vIdx;

                                if (vIdx >= 0 && vIdx < vertexCount && lockedNormals[vIdx] == -1)
                                {
                                    lockedNormals[vIdx] = nIdx;
                                }
                            }
                        }

                        for (int i = 0; i < faceCount; i++)
                        {
                            var face = (XObjectStructure)faces[i];
                            var vIndices = face["faceVertexIndices"].Values;

                            int matIndex = (i < faceMaterials.Count) ? faceMaterials[i] : 0;
                            if (matIndex >= materials.Count) matIndex = 0;
                            var material = materials[matIndex];

                            int vCount = vIndices.Count;
                            for (int v = 2; v < vCount; v++)
                            {
                                int vIdx0 = (int)vIndices[0];
                                int vIdx1 = (int)vIndices[v - 1];
                                int vIdx2 = (int)vIndices[v];

                                int nIdx0 = lockedNormals[vIdx0] != -1 ? lockedNormals[vIdx0] : vIdx0;
                                int nIdx1 = lockedNormals[vIdx1] != -1 ? lockedNormals[vIdx1] : vIdx1;
                                int nIdx2 = lockedNormals[vIdx2] != -1 ? lockedNormals[vIdx2] : vIdx2;

                                if (skinnedBuilder != null)
                                {
                                    var prim = skinnedBuilder.UsePrimitive(material);
                                    var v1 = BuildSkinnedVertex(obj, vIdx0, nIdx0, colorMap, vertexWeights, 0);
                                    var v2 = BuildSkinnedVertex(obj, vIdx1, nIdx1, colorMap, vertexWeights, 0);
                                    var v3 = BuildSkinnedVertex(obj, vIdx2, nIdx2, colorMap, vertexWeights, 0);
                                    prim.AddTriangle(v1, v2, v3);
                                }
                                else if (staticBuilder != null)
                                {
                                    var prim = staticBuilder.UsePrimitive(material);
                                    var v1 = BuildStaticVertex(obj, vIdx0, nIdx0, colorMap);
                                    var v2 = BuildStaticVertex(obj, vIdx1, nIdx1, colorMap);
                                    var v3 = BuildStaticVertex(obj, vIdx2, nIdx2, colorMap);
                                    prim.AddTriangle(v1, v2, v3);
                                }
                            }
                        }
                    }
                }
            }
        }

        private static StaticVertexBuilder BuildStaticVertex(XObject meshObj, int vIndex, int nIndex, Dictionary<int, Vector4> colorMap)
        {
            var v = new StaticVertexBuilder();
            ExtractVertexGeometry(meshObj, vIndex, nIndex, colorMap, ref v.Geometry.Position, ref v.Geometry.Normal, ref v.Material.TexCoord, ref v.Material.Color);
            return v;
        }

        private static SkinnedVertexBuilder BuildSkinnedVertex(XObject meshObj, int vIndex, int nIndex, Dictionary<int, Vector4> colorMap, Dictionary<int, List<(int jointIndex, float weight)>> vertexWeights, int fallbackJointIndex)
        {
            var v = new SkinnedVertexBuilder();
            ExtractVertexGeometry(meshObj, vIndex, nIndex, colorMap, ref v.Geometry.Position, ref v.Geometry.Normal, ref v.Material.TexCoord, ref v.Material.Color);

            if (vertexWeights.TryGetValue(vIndex, out var influences))
            {
                var top4 = influences.OrderByDescending(x => x.weight).Take(4).ToList();
                float sum = top4.Sum(x => x.weight);
                if (sum > 0.0001f)
                {
                    if (top4.Count == 1) v.Skinning.SetBindings((top4[0].jointIndex, 1.0f));
                    else if (top4.Count == 2) v.Skinning.SetBindings((top4[0].jointIndex, top4[0].weight / sum), (top4[1].jointIndex, top4[1].weight / sum));
                    else if (top4.Count == 3) v.Skinning.SetBindings((top4[0].jointIndex, top4[0].weight / sum), (top4[1].jointIndex, top4[1].weight / sum), (top4[2].jointIndex, top4[2].weight / sum));
                    else v.Skinning.SetBindings((top4[0].jointIndex, top4[0].weight / sum), (top4[1].jointIndex, top4[1].weight / sum), (top4[2].jointIndex, top4[2].weight / sum), (top4[3].jointIndex, top4[3].weight / sum));
                }
                else v.Skinning.SetBindings((fallbackJointIndex, 1.0f));
            }
            else v.Skinning.SetBindings((fallbackJointIndex, 1.0f));

            return v;
        }

        private static void ExtractVertexGeometry(XObject meshObj, int vIndex, int nIndex, Dictionary<int, Vector4> colorMap, ref System.Numerics.Vector3 pos, ref System.Numerics.Vector3 norm, ref System.Numerics.Vector2 tex, ref System.Numerics.Vector4 col)
        {
            var vertices = meshObj["vertices"].Values;
            if (vIndex >= 0 && vIndex < vertices.Count)
            {
                var p = D3DX.Mesh.XUtils.Vector((XObjectStructure)vertices[vIndex]);
                pos = new System.Numerics.Vector3(p.X, p.Y, p.Z);
            }

            foreach (XChildObject child in meshObj.Children)
            {
                if (child.Object.DataType.NameData == "MeshNormals")
                {
                    var normals = child.Object["normals"].Values;
                    if (nIndex >= 0 && nIndex < normals.Count)
                    {
                        var n = D3DX.Mesh.XUtils.Vector((XObjectStructure)normals[nIndex]);
                        norm = new System.Numerics.Vector3(n.X, n.Y, n.Z);
                    }
                }
                else if (child.Object.DataType.NameData == "MeshTextureCoords")
                {
                    var texs = child.Object["textureCoords"].Values;
                    if (vIndex >= 0 && vIndex < texs.Count)
                    {
                        var t = (XObjectStructure)texs[vIndex];
                        tex = new System.Numerics.Vector2((float)(double)t["u"].Values[0], (float)(double)t["v"].Values[0]);
                    }
                }
            }

            col = colorMap.TryGetValue(vIndex, out Vector4 color) ? new System.Numerics.Vector4(color.X, color.Y, color.Z, color.W) : new System.Numerics.Vector4(1, 1, 1, 1);
        }

        private static string[] DeduceNameSlots(XFile xFile)
        {
            foreach (var frame in xFile.Objects)
                foreach (var frameChild in frame.Children)
                    if (frameChild.Object.DataType.NameData == "Mesh")
                        foreach (var child in frameChild.Object.Children)
                            if (child.Object.DataType.NameData == "SkinWeights")
                            {
                                string name = (string)child.Object["transformNodeName"].Values[0];
                                if (BHDFile.BipedBoneNames.Contains(name)) return BHDFile.BipedBoneNames;
                                if (BHDFile.NonBipedBoneNames.Contains(name)) return BHDFile.NonBipedBoneNames;
                            }
            return BHDFile.BipedBoneNames;
        }

        private static System.Numerics.Matrix4x4 ConvertBhdMatrix(Matrix m)
        {
            if (float.IsNaN(m.M11)) return System.Numerics.Matrix4x4.Identity;

            return new System.Numerics.Matrix4x4(
                m.M11, m.M21, m.M31, 0.0f,
                m.M12, m.M22, m.M32, 0.0f,
                m.M13, m.M23, m.M33, 0.0f,
                m.M14, m.M24, m.M34, 1.0f);
        }
    }
}