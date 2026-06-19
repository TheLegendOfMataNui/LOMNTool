using System;
using System.Collections.Generic;
using System.Linq;
using System.IO;
using SharpGLTF.Schema2;
using D3DX.Mesh;
using SharpDX;

namespace LOMNTool.GLTF
{
    public static class GLTFImporter
    {
        // PEEK FUNCTION: Checks how many valid meshes exist in the file before fully importing
        public static int GetMeshNodeCount(string filename)
        {
            var model = ModelRoot.Load(filename);
            return model.LogicalNodes.Count(n => n.Mesh != null);
        }

        // BATCH IMPORT: Extracts every single mesh as a distinct, individually named XFile
        public static Dictionary<string, XFile> ImportSplitMeshes(string filename, Matrix transform, out BHDFile bhdOut)
        {
            transform = Matrix.Identity;
            var model = ModelRoot.Load(filename);
            var results = new Dictionary<string, XFile>();
            bhdOut = null;

            if (model.LogicalMeshes.Count == 0) return results;

            if (model.LogicalSkins.Count > 0)
            {
                bhdOut = ExtractSkeleton(model);
            }

            // Grab every node that contains a distinct mesh object
            var meshNodes = model.LogicalNodes.Where(n => n.Mesh != null).ToList();

            foreach (var node in meshNodes)
            {
                XFile result = new XFile(new XHeader());
                result.Templates.Add(XReader.NativeTemplates["XSkinMeshHeader"]);
                result.Templates.Add(XReader.NativeTemplates["VertexDuplicationIndices"]);
                result.Templates.Add(XReader.NativeTemplates["SkinWeights"]);

                XObject frameObject = new XObject(new XToken(XToken.TokenID.NAME) { NameData = "Frame" }, "Root");
                XObject frameTransformObject = new XObject(new XToken(XToken.TokenID.NAME) { NameData = "FrameTransformMatrix" });

                frameTransformObject.Members.Add(new XObjectMember("frameMatrix", new XToken(XToken.TokenID.NAME) { NameData = "Matrix4x4" },
                    new XObjectStructure(XReader.NativeTemplates["Matrix4x4"],
                    new XObjectMember("matrix", new XToken(XToken.TokenID.FLOAT),
                    1.0f, 0.0f, 0.0f, 0.0f, 0.0f, 1.0f, 0.0f, 0.0f, 0.0f, 0.0f, 1.0f, 0.0f, 0.0f, 0.0f, 0.0f, 1.0f))));

                frameObject.Children.Add(new XChildObject(frameTransformObject, false));

                if (bhdOut != null)
                {
                    string[] namePool = bhdOut.NameSlots;
                    var rootNodes = model.LogicalNodes.Where(n => namePool.Contains(n.Name) && (n.VisualParent == null || !namePool.Contains(n.VisualParent.Name))).ToList();

                    foreach (var rootNode in rootNodes)
                    {
                        frameObject.Children.Add(new XChildObject(BuildFrameHierarchy(rootNode, namePool), false));
                    }
                }

                // Extract only this specific mesh node
                XObject meshObj = ExtractMesh(node.Mesh, transform, model.LogicalSkins.FirstOrDefault(), bhdOut, model, -1);
                frameObject.Children.Add(new XChildObject(meshObj, false));
                result.Objects.Add(frameObject);

                // Establish the file name using the node or mesh name
                string safeName = string.IsNullOrWhiteSpace(node.Name) ? node.Mesh.Name : node.Name;
                if (string.IsNullOrWhiteSpace(safeName)) safeName = $"Mesh_{meshNodes.IndexOf(node)}";

                // Ensure no illegal characters cause IO crash during file generation
                foreach (char c in Path.GetInvalidFileNameChars())
                {
                    safeName = safeName.Replace(c.ToString(), "");
                }

                // Ensure unique dictionary keys if there are multiple objects with the same name
                string finalName = safeName;
                int suffix = 1;
                while (results.ContainsKey(finalName))
                {
                    finalName = $"{safeName}_{suffix}";
                    suffix++;
                }

                results.Add(finalName, result);
            }

            return results;
        }

        // STANDARD IMPORT: Maintains full compatibility with non-morphed objects
        public static XFile Import(string filename, Matrix transform, out BHDFile bhdOut)
        {
            transform = Matrix.Identity;

            var model = ModelRoot.Load(filename);
            XFile result = new XFile(new XHeader());
            bhdOut = null;

            result.Templates.Add(XReader.NativeTemplates["XSkinMeshHeader"]);
            result.Templates.Add(XReader.NativeTemplates["VertexDuplicationIndices"]);
            result.Templates.Add(XReader.NativeTemplates["SkinWeights"]);

            XObject frameObject = new XObject(new XToken(XToken.TokenID.NAME) { NameData = "Frame" }, "Root");
            XObject frameTransformObject = new XObject(new XToken(XToken.TokenID.NAME) { NameData = "FrameTransformMatrix" });

            frameTransformObject.Members.Add(new XObjectMember("frameMatrix", new XToken(XToken.TokenID.NAME) { NameData = "Matrix4x4" },
                new XObjectStructure(XReader.NativeTemplates["Matrix4x4"],
                new XObjectMember("matrix", new XToken(XToken.TokenID.FLOAT),
                1.0f, 0.0f, 0.0f, 0.0f, 0.0f, 1.0f, 0.0f, 0.0f, 0.0f, 0.0f, 1.0f, 0.0f, 0.0f, 0.0f, 0.0f, 1.0f))));

            frameObject.Children.Add(new XChildObject(frameTransformObject, false));

            if (model.LogicalSkins.Count > 0)
            {
                bhdOut = ExtractSkeleton(model);

                string[] namePool = bhdOut.NameSlots;
                var rootNodes = model.LogicalNodes.Where(n => namePool.Contains(n.Name) && (n.VisualParent == null || !namePool.Contains(n.VisualParent.Name))).ToList();

                foreach (var rootNode in rootNodes)
                {
                    frameObject.Children.Add(new XChildObject(BuildFrameHierarchy(rootNode, namePool), false));
                }
            }

            if (model.LogicalMeshes.Count > 0)
            {
                XObject meshObj = ExtractMesh(model.LogicalMeshes[0], transform, model.LogicalSkins.FirstOrDefault(), bhdOut, model, -1);
                frameObject.Children.Add(new XChildObject(meshObj, false));
            }

            result.Objects.Add(frameObject);
            return result;
        }

        // SEQUENCE IMPORT: Extracts the base model and generates separate states for each shape key
        public static Dictionary<string, XFile> ImportMorphSequence(string filename, Matrix transform, out BHDFile bhdOut)
        {
            transform = Matrix.Identity;
            var model = ModelRoot.Load(filename);
            var results = new Dictionary<string, XFile>();
            bhdOut = null;

            if (model.LogicalMeshes.Count == 0) return results;

            // Generate skeleton structure once
            if (model.LogicalSkins.Count > 0)
            {
                bhdOut = ExtractSkeleton(model);
            }

            // Determine max morph targets available in the primary mesh using MorphTargetsCount
            var gltfMesh = model.LogicalMeshes[0];
            int maxMorphs = 0;
            foreach (var prim in gltfMesh.Primitives)
            {
                maxMorphs = Math.Max(maxMorphs, prim.MorphTargetsCount);
            }

            // 1. Generate Base Frame ("base")
            results.Add("base", CreateXFileFrame(model, transform, bhdOut, -1));

            // 2. Generate an absolute frame for each available shape key delta
            for (int i = 0; i < maxMorphs; i++)
            {
                results.Add($"morph_{i + 1}", CreateXFileFrame(model, transform, bhdOut, i));
            }

            return results;
        }

        private static XFile CreateXFileFrame(ModelRoot model, Matrix transform, BHDFile bhdOut, int morphIndex)
        {
            XFile result = new XFile(new XHeader());
            result.Templates.Add(XReader.NativeTemplates["XSkinMeshHeader"]);
            result.Templates.Add(XReader.NativeTemplates["VertexDuplicationIndices"]);
            result.Templates.Add(XReader.NativeTemplates["SkinWeights"]);

            XObject frameObject = new XObject(new XToken(XToken.TokenID.NAME) { NameData = "Frame" }, "Root");
            XObject frameTransformObject = new XObject(new XToken(XToken.TokenID.NAME) { NameData = "FrameTransformMatrix" });

            frameTransformObject.Members.Add(new XObjectMember("frameMatrix", new XToken(XToken.TokenID.NAME) { NameData = "Matrix4x4" },
                new XObjectStructure(XReader.NativeTemplates["Matrix4x4"],
                new XObjectMember("matrix", new XToken(XToken.TokenID.FLOAT),
                1.0f, 0.0f, 0.0f, 0.0f, 0.0f, 1.0f, 0.0f, 0.0f, 0.0f, 0.0f, 1.0f, 0.0f, 0.0f, 0.0f, 0.0f, 1.0f))));

            frameObject.Children.Add(new XChildObject(frameTransformObject, false));

            if (bhdOut != null)
            {
                string[] namePool = bhdOut.NameSlots;
                var rootNodes = model.LogicalNodes.Where(n => namePool.Contains(n.Name) && (n.VisualParent == null || !namePool.Contains(n.VisualParent.Name))).ToList();

                foreach (var rootNode in rootNodes)
                {
                    frameObject.Children.Add(new XChildObject(BuildFrameHierarchy(rootNode, namePool), false));
                }
            }

            if (model.LogicalMeshes.Count > 0)
            {
                XObject meshObj = ExtractMesh(model.LogicalMeshes[0], transform, model.LogicalSkins.FirstOrDefault(), bhdOut, model, morphIndex);
                frameObject.Children.Add(new XChildObject(meshObj, false));
            }

            result.Objects.Add(frameObject);
            return result;
        }

        private static XObject BuildFrameHierarchy(Node node, string[] namePool)
        {
            XObject frame = new XObject(new XToken(XToken.TokenID.NAME) { NameData = "Frame" }, node.Name);
            XObject frameTransform = new XObject(new XToken(XToken.TokenID.NAME) { NameData = "FrameTransformMatrix" });

            System.Numerics.Matrix4x4 numMat = node.LocalMatrix;

            frameTransform.Members.Add(new XObjectMember("frameMatrix", new XToken(XToken.TokenID.NAME) { NameData = "Matrix4x4" },
                new XObjectStructure(XReader.NativeTemplates["Matrix4x4"],
                new XObjectMember("matrix", new XToken(XToken.TokenID.FLOAT),
                numMat.M11, numMat.M12, numMat.M13, numMat.M14,
                numMat.M21, numMat.M22, numMat.M23, numMat.M24,
                numMat.M31, numMat.M32, numMat.M33, numMat.M34,
                numMat.M41, numMat.M42, numMat.M43, numMat.M44))));

            frame.Children.Add(new XChildObject(frameTransform, false));

            foreach (var child in node.VisualChildren)
            {
                if (namePool.Contains(child.Name))
                {
                    frame.Children.Add(new XChildObject(BuildFrameHierarchy(child, namePool), false));
                }
            }

            return frame;
        }

        private static BHDFile ExtractSkeleton(ModelRoot model)
        {
            BHDFile bhd = new BHDFile();

            bool isBiped = false;
            foreach (var node in model.LogicalNodes)
            {
                if (BHDFile.BipedBoneNames.Contains(node.Name)) { isBiped = true; break; }
            }

            string[] namePool = isBiped ? BHDFile.BipedBoneNames : BHDFile.NonBipedBoneNames;
            bhd.NameSlots = namePool;

            BHDFile.Bone[] bones = new BHDFile.Bone[namePool.Length];
            Dictionary<Node, uint> nodeToIndex = new Dictionary<Node, uint>();

            foreach (var node in model.LogicalNodes)
            {
                int index = Array.IndexOf(namePool, node.Name);
                if (index == -1) continue;

                nodeToIndex[node] = (uint)index;

                BHDFile.Bone b = new BHDFile.Bone();
                b.Index = (uint)index;

                System.Numerics.Matrix4x4 numMat = node.LocalMatrix;

                Matrix dxTransform = new Matrix(
                    numMat.M11, numMat.M12, numMat.M13, numMat.M14,
                    numMat.M21, numMat.M22, numMat.M23, numMat.M24,
                    numMat.M31, numMat.M32, numMat.M33, numMat.M34,
                    numMat.M41, numMat.M42, numMat.M43, numMat.M44);

                b.Transform = ConvertToBhdMatrix(dxTransform);
                bones[index] = b;
            }

            foreach (var node in model.LogicalNodes)
            {
                if (!nodeToIndex.ContainsKey(node)) continue;
                uint myIndex = nodeToIndex[node];

                if (node.VisualParent != null && nodeToIndex.ContainsKey(node.VisualParent))
                {
                    bones[myIndex].ParentIndex = nodeToIndex[node.VisualParent];
                }
                else
                {
                    if (myIndex == 0)
                        bones[myIndex].ParentIndex = myIndex;
                    else
                        bones[myIndex].ParentIndex = 0xFFFFFFFF;
                }
            }

            for (int i = 0; i < bones.Length; i++)
            {
                if (bones[i] != null)
                {
                    bhd.Bones.Add(bones[i]);
                }
                else
                {
                    bhd.Bones.Add(new BHDFile.Bone() { Index = (uint)i, ParentIndex = 0xFFFFFFFF, Transform = new Matrix(float.NaN, float.NaN, float.NaN, float.NaN, float.NaN, float.NaN, float.NaN, float.NaN, float.NaN, float.NaN, float.NaN, float.NaN, 0.0f, 0.0f, 0.0f, 1.0f) });
                }
            }

            return bhd;
        }

        private static XObject ExtractMesh(Mesh gltfMesh, Matrix transform, Skin skin, BHDFile bhd, ModelRoot model, int morphIndex)
        {
            XObject mesh = XReader.NativeTemplates["Mesh"].Instantiate();
            XObject meshNormals = XReader.NativeTemplates["MeshNormals"].Instantiate();
            XObject meshTextureCoords = XReader.NativeTemplates["MeshTextureCoords"].Instantiate();
            XObject meshMaterialList = XReader.NativeTemplates["MeshMaterialList"].Instantiate();

            Dictionary<string, List<Tuple<int, float>>> boneWeights = new Dictionary<string, List<Tuple<int, float>>>();
            if (skin != null)
            {
                foreach (var joint in skin.Joints)
                    boneWeights[joint.Name] = new List<Tuple<int, float>>();
            }

            int vertexOffset = 0;
            int faceOffset = 0;
            int materialIndex = 0;

            foreach (var prim in gltfMesh.Primitives)
            {
                var positions = prim.GetVertexAccessor("POSITION")?.AsVector3Array();
                var normals = prim.GetVertexAccessor("NORMAL")?.AsVector3Array();
                var uvs = prim.GetVertexAccessor("TEXCOORD_0")?.AsVector2Array();
                var joints0 = prim.GetVertexAccessor("JOINTS_0")?.AsVector4Array();
                var weights0 = prim.GetVertexAccessor("WEIGHTS_0")?.AsVector4Array();
                var indices = prim.GetIndices();

                if (positions == null || indices == null) continue;

                // Look up position and normal modifications for the targeted shape key frame
                IList<System.Numerics.Vector3> morphDeltas = null;
                IList<System.Numerics.Vector3> morphNormalDeltas = null;

                if (morphIndex >= 0 && morphIndex < prim.MorphTargetsCount)
                {
                    var morphDict = prim.GetMorphTargetAccessors(morphIndex);
                    if (morphDict.TryGetValue("POSITION", out var targetAccessor))
                    {
                        morphDeltas = targetAccessor.AsVector3Array();
                    }
                    if (morphDict.TryGetValue("NORMAL", out var normalAccessor))
                    {
                        morphNormalDeltas = normalAccessor.AsVector3Array();
                    }
                }

                for (int i = 0; i < positions.Count; i++)
                {
                    Vector3 p = new Vector3(positions[i].X, positions[i].Y, positions[i].Z);

                    // Add absolute vertex offset from morph structure if active
                    if (morphDeltas != null && i < morphDeltas.Count)
                    {
                        p.X += morphDeltas[i].X;
                        p.Y += morphDeltas[i].Y;
                        p.Z += morphDeltas[i].Z;
                    }

                    Vector4 pTrans = Vector3.Transform(p, transform);
                    mesh["vertices"].Values.Add(XUtils.Vector(new Vector3(pTrans.X, pTrans.Y, pTrans.Z)));

                    if (normals != null && i < normals.Count)
                    {
                        Vector3 n = new Vector3(normals[i].X, normals[i].Y, normals[i].Z);

                        // SHADING FIX: Apply Normal Deltas to fix smooth shading in DX Viewer
                        if (morphNormalDeltas != null && i < morphNormalDeltas.Count)
                        {
                            n.X += morphNormalDeltas[i].X;
                            n.Y += morphNormalDeltas[i].Y;
                            n.Z += morphNormalDeltas[i].Z;
                        }

                        // Re-normalize vector after morph shift
                        float lenSq = n.X * n.X + n.Y * n.Y + n.Z * n.Z;
                        if (lenSq > 0.000001f)
                        {
                            float len = (float)Math.Sqrt(lenSq);
                            n.X /= len;
                            n.Y /= len;
                            n.Z /= len;
                        }

                        Vector4 nTrans = Vector4.Transform(new Vector4(n, 0.0f), transform);
                        meshNormals["normals"].Values.Add(XUtils.Vector(new Vector3(nTrans.X, nTrans.Y, nTrans.Z)));
                    }
                    else
                    {
                        meshNormals["normals"].Values.Add(XUtils.Vector(new Vector3(0, 1, 0)));
                    }

                    if (uvs != null && i < uvs.Count)
                    {
                        meshTextureCoords["textureCoords"].Values.Add(XUtils.TexCoord(new Vector2(uvs[i].X, uvs[i].Y)));
                    }
                    else
                    {
                        meshTextureCoords["textureCoords"].Values.Add(XUtils.TexCoord(new Vector2(0, 0)));
                    }

                    if (skin != null && joints0 != null && weights0 != null)
                    {
                        int globalVIndex = vertexOffset + i;
                        var j = joints0[i];
                        var w = weights0[i];

                        if (w.X > 0) boneWeights[skin.Joints[(int)j.X].Name].Add(new Tuple<int, float>(globalVIndex, w.X));
                        if (w.Y > 0) boneWeights[skin.Joints[(int)j.Y].Name].Add(new Tuple<int, float>(globalVIndex, w.Y));
                        if (w.Z > 0) boneWeights[skin.Joints[(int)j.Z].Name].Add(new Tuple<int, float>(globalVIndex, w.Z));
                        if (w.W > 0) boneWeights[skin.Joints[(int)j.W].Name].Add(new Tuple<int, float>(globalVIndex, w.W));
                    }
                }

                for (int i = 0; i < indices.Count; i += 3)
                {
                    List<int> faceIndices = new List<int> {
                        (int)(indices[i] + vertexOffset),
                        (int)(indices[i + 1] + vertexOffset),
                        (int)(indices[i + 2] + vertexOffset)
                    };

                    mesh["faces"].Values.Add(XUtils.Face(faceIndices));
                    meshNormals["faceNormals"].Values.Add(XUtils.Face(faceIndices));

                    meshMaterialList["faceIndexes"].Values.Add(materialIndex);
                    faceOffset++;
                }

                XObject matObj = XReader.NativeTemplates["Material"].Instantiate();
                var baseColorChannel = prim.Material?.FindChannel("BaseColor");
                var baseColor = baseColorChannel?.Color ?? System.Numerics.Vector4.One;

                matObj["faceColor"].Values.Add(XUtils.ColorRGBA(baseColor.X, baseColor.Y, baseColor.Z, 1.0f));
                matObj["power"].Values.Add(0.0f);
                matObj["specularColor"].Values.Add(XUtils.ColorRGB(0, 0, 0));
                matObj["emissiveColor"].Values.Add(XUtils.ColorRGB(0, 0, 0));

                string texName = prim.Material?.Name;
                if (!string.IsNullOrWhiteSpace(texName) && !texName.Equals("dummy", StringComparison.OrdinalIgnoreCase) && !texName.Equals("dummy.dds", StringComparison.OrdinalIgnoreCase) && !texName.Equals("MeshMaterial", StringComparison.OrdinalIgnoreCase))
                {
                    if (!texName.EndsWith(".dds", StringComparison.OrdinalIgnoreCase))
                        texName += ".dds";

                    XObject texFilename = XReader.NativeTemplates["TextureFilename"].Instantiate();
                    texFilename["filename"].Values.Add(texName);
                    matObj.Children.Add(new XChildObject(texFilename, false));
                }

                meshMaterialList.Children.Add(new XChildObject(matObj, false));

                vertexOffset += positions.Count;
                materialIndex++;
            }

            mesh["nVertices"].Values.Add(mesh["vertices"].Values.Count);
            mesh["nFaces"].Values.Add(mesh["faces"].Values.Count);

            meshNormals["nNormals"].Values.Add(meshNormals["normals"].Values.Count);
            meshNormals["nFaceNormals"].Values.Add(meshNormals["faceNormals"].Values.Count);
            mesh.Children.Add(new XChildObject(meshNormals, false));

            meshTextureCoords["nTextureCoords"].Values.Add(meshTextureCoords["textureCoords"].Values.Count);
            mesh.Children.Add(new XChildObject(meshTextureCoords, false));

            meshMaterialList["nMaterials"].Values.Add(meshMaterialList.Children.Count);
            meshMaterialList["nFaceIndexes"].Values.Add(meshMaterialList["faceIndexes"].Values.Count);
            mesh.Children.Add(new XChildObject(meshMaterialList, false));

            if (skin != null && bhd != null)
            {
                XObject skinMeshHeader = XReader.NativeTemplates["XSkinMeshHeader"].Instantiate();
                skinMeshHeader["nMaxSkinWeightsPerVertex"].Values.Add(1);
                skinMeshHeader["nMaxSkinWeightsPerFace"].Values.Add(3);
                skinMeshHeader["nBones"].Values.Add(bhd.NameSlots.Length);
                mesh.Children.Add(new XChildObject(skinMeshHeader, false));

                Matrix[] worldMatrixCache = new Matrix[bhd.NameSlots.Length];

                for (int i = 0; i < bhd.NameSlots.Length; i++)
                {
                    string boneName = bhd.NameSlots[i];
                    bool hasWeights = boneWeights.ContainsKey(boneName) && boneWeights[boneName].Count > 0;

                    XObject skinWeights = XReader.NativeTemplates["SkinWeights"].Instantiate();
                    skinWeights["transformNodeName"].Values.Add(boneName);

                    if (hasWeights)
                    {
                        skinWeights["nWeights"].Values.Add(boneWeights[boneName].Count);
                        foreach (var influence in boneWeights[boneName])
                        {
                            skinWeights["vertexIndices"].Values.Add(influence.Item1);
                            skinWeights["weights"].Values.Add(influence.Item2);
                        }
                    }
                    else
                    {
                        skinWeights["nWeights"].Values.Add(1);
                        skinWeights["vertexIndices"].Values.Add(0);
                        skinWeights["weights"].Values.Add(0.0f);
                    }

                    Matrix worldTransform = GetBhdWorldMatrix(i, bhd, worldMatrixCache);
                    Matrix dxIbp = Matrix.Invert(worldTransform);

                    XObjectStructure matStruct = new XObjectStructure(XReader.NativeTemplates["Matrix4x4"], new XObjectMember("matrix", new XToken(XToken.TokenID.FLOAT)));
                    foreach (float f in dxIbp.ToArray())
                        matStruct["matrix"].Values.Add(f);

                    skinWeights["matrixOffset"].Values.Add(matStruct);
                    mesh.Children.Add(new XChildObject(skinWeights, false));
                }
            }

            // EXPLICIT IDENTITY MAP: Added at the absolute end of the mesh block to match DX8 formatting.
            XObject vdi = XReader.NativeTemplates["VertexDuplicationIndices"].Instantiate();
            int totalVerts = mesh["vertices"].Values.Count;
            vdi["nIndices"].Values.Add(totalVerts);
            vdi["nOriginalVertices"].Values.Add(totalVerts);
            for (int i = 0; i < totalVerts; i++)
            {
                vdi["indices"].Values.Add(i);
            }
            mesh.Children.Add(new XChildObject(vdi, false));

            return mesh;
        }

        private static Matrix GetBhdWorldMatrix(int boneIdx, BHDFile bhd, Matrix[] cache)
        {
            if (boneIdx < 0 || boneIdx >= bhd.Bones.Count) return Matrix.Identity;
            if (cache[boneIdx].M44 != 0.0f) return cache[boneIdx];

            var bone = bhd.Bones[boneIdx];
            if (bone == null || float.IsNaN(bone.Transform.M11))
            {
                cache[boneIdx] = Matrix.Identity;
                return Matrix.Identity;
            }

            Matrix rowMajorLocal = ConvertToRowMajor(bone.Transform);

            if (bone.ParentIndex == 0xFFFFFFFF || bone.ParentIndex == bone.Index || bone.ParentIndex >= bhd.Bones.Count)
            {
                cache[boneIdx] = rowMajorLocal;
            }
            else
            {
                cache[boneIdx] = rowMajorLocal * GetBhdWorldMatrix((int)bone.ParentIndex, bhd, cache);
            }

            return cache[boneIdx];
        }

        private static SharpDX.Matrix ConvertToBhdMatrix(SharpDX.Matrix m)
        {
            return new SharpDX.Matrix(
                m.M11, m.M21, m.M31, m.M41,
                m.M12, m.M22, m.M32, m.M42,
                m.M13, m.M23, m.M33, m.M43,
                0.0f, 0.0f, 0.0f, 1.0f);
        }

        private static SharpDX.Matrix ConvertToRowMajor(SharpDX.Matrix m)
        {
            return new SharpDX.Matrix(
                m.M11, m.M21, m.M31, 0.0f,
                m.M12, m.M22, m.M32, 0.0f,
                m.M13, m.M23, m.M33, 0.0f,
                m.M14, m.M24, m.M34, 1.0f);
        }
    }
}