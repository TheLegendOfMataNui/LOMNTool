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

            if (model.LogicalSkins.Count > 0)
            {
                bool hasSrp = model.LogicalNodes.Any(n => n.Name != null && n.Name.StartsWith("SRP_"));
                if (hasSrp)
                {
                    bhdOut = ExtractHybridSkeleton(model);
                }
                else
                {
                    bhdOut = ExtractSkeleton(model);
                }
            }
            else
            {
                bhdOut = null;
            }

            if (model.LogicalMeshes.Count == 0) return results;

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
                    var rootBones = bhdOut.Bones.Where(b => b.ParentIndex == 0xFFFFFFFF || b.ParentIndex == b.Index).ToList();
                    foreach (var rootBone in rootBones)
                    {
                        frameObject.Children.Add(new XChildObject(BuildFrameHierarchyFromBHD(rootBone.Index, bhdOut), false));
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

            if (model.LogicalSkins.Count > 0)
            {
                bool hasSrp = model.LogicalNodes.Any(n => n.Name != null && n.Name.StartsWith("SRP_"));
                if (hasSrp)
                {
                    bhdOut = ExtractHybridSkeleton(model);
                }
                else
                {
                    bhdOut = ExtractSkeleton(model);
                }
            }
            else
            {
                bhdOut = null;
            }

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
                var rootBones = bhdOut.Bones.Where(b => b.ParentIndex == 0xFFFFFFFF || b.ParentIndex == b.Index).ToList();
                foreach (var rootBone in rootBones)
                {
                    frameObject.Children.Add(new XChildObject(BuildFrameHierarchyFromBHD(rootBone.Index, bhdOut), false));
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

            if (model.LogicalSkins.Count > 0)
            {
                bool hasSrp = model.LogicalNodes.Any(n => n.Name != null && n.Name.StartsWith("SRP_"));
                if (hasSrp)
                {
                    bhdOut = ExtractHybridSkeleton(model);
                }
                else
                {
                    bhdOut = ExtractSkeleton(model);
                }
            }
            else
            {
                bhdOut = null;
            }

            if (model.LogicalMeshes.Count == 0) return results;

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
                var rootBones = bhdOut.Bones.Where(b => b.ParentIndex == 0xFFFFFFFF || b.ParentIndex == b.Index).ToList();
                foreach (var rootBone in rootBones)
                {
                    frameObject.Children.Add(new XChildObject(BuildFrameHierarchyFromBHD(rootBone.Index, bhdOut), false));
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

        // HYBRID SKELETON MERGE: Takes custom translations/lengths from Blender, but enforces Reference Rotations
        private static BHDFile ExtractHybridSkeleton(ModelRoot model)
        {
            BHDFile bhd = new BHDFile();

            bool isBiped = false;
            foreach (var node in model.LogicalNodes)
            {
                if (BHDFile.BipedBoneNames.Contains(node.Name)) { isBiped = true; break; }
            }

            List<string> dynamicNamePool = new List<string>(isBiped ? BHDFile.BipedBoneNames : BHDFile.NonBipedBoneNames);

            if (model.LogicalSkins.Count > 0)
            {
                foreach (var joint in model.LogicalSkins[0].Joints)
                {
                    if (!dynamicNamePool.Contains(joint.Name) && !string.IsNullOrWhiteSpace(joint.Name))
                    {
                        dynamicNamePool.Add(joint.Name);
                    }
                }
            }

            string[] namePool = dynamicNamePool.ToArray();
            bhd.NameSlots = namePool;

            BHDFile.Bone[] bones = new BHDFile.Bone[namePool.Length];
            Dictionary<Node, uint> nodeToIndex = new Dictionary<Node, uint>();

            // Find the master armature node to bypass any object-level transforms applied in Blender
            Node armatureNode = null;
            foreach (var node in model.LogicalNodes)
            {
                if (Array.IndexOf(namePool, node.Name) != -1)
                {
                    if (node.VisualParent != null && Array.IndexOf(namePool, node.VisualParent.Name) == -1)
                    {
                        armatureNode = node.VisualParent;
                        break;
                    }
                }
            }

            System.Numerics.Matrix4x4 armatureInverse = System.Numerics.Matrix4x4.Identity;
            if (armatureNode != null)
            {
                System.Numerics.Matrix4x4.Invert(armatureNode.WorldMatrix, out armatureInverse);
            }

            // Extract the embedded Saffire Rest Pose data from the GLB
            Dictionary<string, Matrix> embeddedSrp = new Dictionary<string, Matrix>();
            foreach (var node in model.LogicalNodes)
            {
                if (node.Name != null && node.Name.StartsWith("SRP_"))
                {
                    string boneName = node.Name.Substring(4);
                    System.Numerics.Matrix4x4 numMat = node.LocalMatrix;
                    Matrix dxTransform = new Matrix(
                        numMat.M11, numMat.M12, numMat.M13, numMat.M14,
                        numMat.M21, numMat.M22, numMat.M23, numMat.M24,
                        numMat.M31, numMat.M32, numMat.M33, numMat.M34,
                        numMat.M41, numMat.M42, numMat.M43, numMat.M44);
                    embeddedSrp[boneName] = dxTransform;
                }
            }

            Dictionary<string, Matrix> newWorldMatrices = new Dictionary<string, Matrix>();

            // Sort nodes hierarchically (parents must be processed before children)
            var sortedNodes = model.LogicalNodes.Where(n => Array.IndexOf(namePool, n.Name) != -1).ToList();
            sortedNodes.Sort((a, b) => {
                int depthA = 0; var currA = a; while (currA != null) { depthA++; currA = currA.VisualParent; }
                int depthB = 0; var currB = b; while (currB != null) { depthB++; currB = currB.VisualParent; }
                return depthA.CompareTo(depthB);
            });

            foreach (var node in sortedNodes)
            {
                int index = Array.IndexOf(namePool, node.Name);
                nodeToIndex[node] = (uint)index;

                BHDFile.Bone b = new BHDFile.Bone();
                b.Index = (uint)index;

                // 1. Get the absolute real-world position of this joint from Blender
                System.Numerics.Matrix4x4 armSpaceMat = node.WorldMatrix * armatureInverse;
                Vector3 targetPos = new Vector3(armSpaceMat.Translation.X, armSpaceMat.Translation.Y, armSpaceMat.Translation.Z);

                // 2. Fetch the preserved Saffire joint rotation from the embedded SRP
                Matrix refLocalDx = Matrix.Identity;
                bool hasRef = false;

                if (embeddedSrp.ContainsKey(node.Name))
                {
                    refLocalDx = embeddedSrp[node.Name];
                    hasRef = true;
                }

                if (!hasRef)
                {
                    // Fallback for custom user bones added in Blender
                    System.Numerics.Matrix4x4 numMat = node.LocalMatrix;
                    refLocalDx = new Matrix(
                        numMat.M11, numMat.M12, numMat.M13, numMat.M14,
                        numMat.M21, numMat.M22, numMat.M23, numMat.M24,
                        numMat.M31, numMat.M32, numMat.M33, numMat.M34,
                        numMat.M41, numMat.M42, numMat.M43, numMat.M44);
                }

                // Isolate pure rotation
                Matrix localRot = refLocalDx;
                localRot.M41 = 0; localRot.M42 = 0; localRot.M43 = 0;

                Vector3 localTrans;
                Matrix parentNewWorld = Matrix.Identity;

                if (node.VisualParent != null && nodeToIndex.ContainsKey(node.VisualParent))
                {
                    string parentName = node.VisualParent.Name;
                    if (newWorldMatrices.ContainsKey(parentName))
                    {
                        parentNewWorld = newWorldMatrices[parentName];
                    }
                }

                // 3. Transform the absolute blender position into the parent's pristine original coordinate space
                Matrix invParentWorld = Matrix.Invert(parentNewWorld);
                Vector4 localPos4 = Vector3.Transform(targetPos, invParentWorld);
                localTrans = new Vector3(localPos4.X, localPos4.Y, localPos4.Z);

                // 4. Construct the final Hybrid Local Matrix
                Matrix newLocalDx = localRot;
                newLocalDx.M41 = localTrans.X;
                newLocalDx.M42 = localTrans.Y;
                newLocalDx.M43 = localTrans.Z;

                newWorldMatrices[node.Name] = newLocalDx * parentNewWorld;

                b.Transform = ConvertToBhdMatrix(newLocalDx);
                bones[index] = b;
            }

            foreach (var node in sortedNodes)
            {
                uint myIndex = nodeToIndex[node];
                if (node.VisualParent != null && nodeToIndex.ContainsKey(node.VisualParent))
                {
                    bones[myIndex].ParentIndex = nodeToIndex[node.VisualParent];
                }
                else
                {
                    bones[myIndex].ParentIndex = (myIndex == 0) ? myIndex : 0xFFFFFFFF;
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

        // PRISTINE SKELETON BUILDER: Bypasses GLTF nodes entirely and builds frames purely from Saffire BHD math
        private static XObject BuildFrameHierarchyFromBHD(uint boneIndex, BHDFile bhd)
        {
            string name = bhd.NameSlots[boneIndex];
            XObject frame = new XObject(new XToken(XToken.TokenID.NAME) { NameData = "Frame" }, name);
            XObject frameTransform = new XObject(new XToken(XToken.TokenID.NAME) { NameData = "FrameTransformMatrix" });

            Matrix m = bhd.Bones[(int)boneIndex].Transform;
            Matrix dxMat = ConvertToRowMajor(m);

            frameTransform.Members.Add(new XObjectMember("frameMatrix", new XToken(XToken.TokenID.NAME) { NameData = "Matrix4x4" },
                new XObjectStructure(XReader.NativeTemplates["Matrix4x4"],
                new XObjectMember("matrix", new XToken(XToken.TokenID.FLOAT),
                dxMat.M11, dxMat.M12, dxMat.M13, dxMat.M14,
                dxMat.M21, dxMat.M22, dxMat.M23, dxMat.M24,
                dxMat.M31, dxMat.M32, dxMat.M33, dxMat.M34,
                dxMat.M41, dxMat.M42, dxMat.M43, dxMat.M44))));

            frame.Children.Add(new XChildObject(frameTransform, false));

            var children = bhd.Bones.Where(b => b.ParentIndex == boneIndex && b.Index != boneIndex).ToList();
            foreach (var child in children)
            {
                frame.Children.Add(new XChildObject(BuildFrameHierarchyFromBHD(child.Index, bhd), false));
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

            // Start with the default names to preserve base engine indices
            List<string> dynamicNamePool = new List<string>(isBiped ? BHDFile.BipedBoneNames : BHDFile.NonBipedBoneNames);

            // UPGRADE: Detect any brand new custom bones added in Blender and append them
            if (model.LogicalSkins.Count > 0)
            {
                foreach (var joint in model.LogicalSkins[0].Joints)
                {
                    if (!dynamicNamePool.Contains(joint.Name) && !string.IsNullOrWhiteSpace(joint.Name))
                    {
                        dynamicNamePool.Add(joint.Name);
                    }
                }
            }

            string[] namePool = dynamicNamePool.ToArray();
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

        // Helper to extract the original material index from the MAT_XXX tag
        private static int ExtractMatIndex(string name)
        {
            if (string.IsNullOrEmpty(name)) return -1;
            var match = System.Text.RegularExpressions.Regex.Match(name, @"^MAT_(\d{3})_");
            if (match.Success)
            {
                return int.Parse(match.Groups[1].Value);
            }
            return -1;
        }

        private static XObject ExtractMesh(Mesh gltfMesh, Matrix transform, Skin skin, BHDFile bhd, ModelRoot model, int morphIndex)
        {
            XObject mesh = XReader.NativeTemplates["Mesh"].Instantiate();
            XObject meshNormals = XReader.NativeTemplates["MeshNormals"].Instantiate();
            XObject meshTextureCoords = XReader.NativeTemplates["MeshTextureCoords"].Instantiate();
            XObject meshVertexColors = XReader.NativeTemplates["MeshVertexColors"].Instantiate();
            XObject meshMaterialList = XReader.NativeTemplates["MeshMaterialList"].Instantiate();

            // MATERIAL RESTORATION: Read LogicalMaterials, sort them by MAT_XXX tag, and build the list once
            var originalMaterials = model.LogicalMaterials.ToList();
            var sortedMaterials = new List<Material>(originalMaterials);
            sortedMaterials.Sort((a, b) => {
                int idxA = ExtractMatIndex(a.Name);
                int idxB = ExtractMatIndex(b.Name);
                if (idxA == -1 && idxB == -1) return originalMaterials.IndexOf(a).CompareTo(originalMaterials.IndexOf(b));
                if (idxA == -1) return 1;
                if (idxB == -1) return -1;
                return idxA.CompareTo(idxB);
            });

            Dictionary<Material, int> materialToIndex = new Dictionary<Material, int>();
            int matIdx = 0;
            foreach (var gltfMat in sortedMaterials)
            {
                XObject matObj = XReader.NativeTemplates["Material"].Instantiate();
                var baseColorChannel = gltfMat.FindChannel("BaseColor");
                var baseColor = baseColorChannel?.Color ?? System.Numerics.Vector4.One;

                matObj["faceColor"].Values.Add(XUtils.ColorRGBA(baseColor.X, baseColor.Y, baseColor.Z, baseColor.W));
                matObj["power"].Values.Add(0.0f);
                matObj["specularColor"].Values.Add(XUtils.ColorRGB(0, 0, 0));
                matObj["emissiveColor"].Values.Add(XUtils.ColorRGB(0, 0, 0));

                string texName = gltfMat.Name;

                // Strip the MAT_XXX tag for the final file
                if (texName != null && texName.StartsWith("MAT_") && texName.Length >= 8 && texName[7] == '_')
                {
                    texName = texName.Substring(8);
                }

                // Override with actual texture filename if available
                if (baseColorChannel != null && baseColorChannel.Value.Texture != null && baseColorChannel.Value.Texture.PrimaryImage != null)
                {
                    if (!string.IsNullOrWhiteSpace(baseColorChannel.Value.Texture.PrimaryImage.Name))
                    {
                        texName = baseColorChannel.Value.Texture.PrimaryImage.Name;
                    }
                }

                if (!string.IsNullOrWhiteSpace(texName) && !texName.Equals("dummy", StringComparison.OrdinalIgnoreCase) && !texName.Equals("dummy.dds", StringComparison.OrdinalIgnoreCase) && !texName.Equals("MeshMaterial", StringComparison.OrdinalIgnoreCase))
                {
                    if (!texName.EndsWith(".dds", StringComparison.OrdinalIgnoreCase))
                        texName += ".dds";

                    XObject texFilename = XReader.NativeTemplates["TextureFilename"].Instantiate();
                    texFilename["filename"].Values.Add(texName);
                    matObj.Children.Add(new XChildObject(texFilename, false));
                }

                meshMaterialList.Children.Add(new XChildObject(matObj, false));
                materialToIndex[gltfMat] = matIdx;
                matIdx++;
            }

            if (meshMaterialList.Children.Count == 0)
            {
                XObject matObj = XReader.NativeTemplates["Material"].Instantiate();
                matObj["faceColor"].Values.Add(XUtils.ColorRGBA(1.0f, 1.0f, 1.0f, 1.0f));
                matObj["power"].Values.Add(0.0f);
                matObj["specularColor"].Values.Add(XUtils.ColorRGB(0, 0, 0));
                matObj["emissiveColor"].Values.Add(XUtils.ColorRGB(0, 0, 0));
                meshMaterialList.Children.Add(new XChildObject(matObj, false));
            }

            Dictionary<string, List<Tuple<int, float>>> boneWeights = new Dictionary<string, List<Tuple<int, float>>>();
            if (skin != null)
            {
                foreach (var joint in skin.Joints)
                    boneWeights[joint.Name] = new List<Tuple<int, float>>();
            }

            int vertexOffset = 0;
            int faceOffset = 0;
            bool hasVertexColors = false;

            foreach (var prim in gltfMesh.Primitives)
            {
                var positions = prim.GetVertexAccessor("POSITION")?.AsVector3Array();
                var normals = prim.GetVertexAccessor("NORMAL")?.AsVector3Array();
                var uvs = prim.GetVertexAccessor("TEXCOORD_0")?.AsVector2Array();
                var joints0 = prim.GetVertexAccessor("JOINTS_0")?.AsVector4Array();
                var weights0 = prim.GetVertexAccessor("WEIGHTS_0")?.AsVector4Array();
                var colors = prim.GetVertexAccessor("COLOR_0")?.AsVector4Array();
                var indices = prim.GetIndices();

                if (positions == null || indices == null) continue;
                if (colors != null) hasVertexColors = true;

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

                        if (morphNormalDeltas != null && i < morphNormalDeltas.Count)
                        {
                            n.X += morphNormalDeltas[i].X;
                            n.Y += morphNormalDeltas[i].Y;
                            n.Z += morphNormalDeltas[i].Z;
                        }

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

                    if (colors != null && i < colors.Count)
                    {
                        meshVertexColors["vertexColors"].Values.Add(XUtils.IndexedColor(vertexOffset + i, new Vector4(colors[i].X, colors[i].Y, colors[i].Z, colors[i].W)));
                    }
                    else if (hasVertexColors)
                    {
                        meshVertexColors["vertexColors"].Values.Add(XUtils.IndexedColor(vertexOffset + i, new Vector4(1, 1, 1, 1)));
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

                int currentFaceMaterialIndex = 0;
                if (prim.Material != null && materialToIndex.ContainsKey(prim.Material))
                {
                    currentFaceMaterialIndex = materialToIndex[prim.Material];
                }

                for (int i = 0; i < indices.Count; i += 3)
                {
                    int v0 = (int)indices[i];
                    int v1 = (int)indices[i + 1];
                    int v2 = (int)indices[i + 2];

                    // Filter out the dummy triangles created by the exporter for material preservation
                    if (uvs != null && v0 < uvs.Count && uvs[v0].X < -9000f)
                    {
                        continue;
                    }

                    List<int> faceIndices = new List<int> {
                        (int)(v0 + vertexOffset),
                        (int)(v1 + vertexOffset),
                        (int)(v2 + vertexOffset)
                    };

                    mesh["faces"].Values.Add(XUtils.Face(faceIndices));
                    meshNormals["faceNormals"].Values.Add(XUtils.Face(faceIndices));

                    meshMaterialList["faceIndexes"].Values.Add(currentFaceMaterialIndex);
                    faceOffset++;
                }

                vertexOffset += positions.Count;
            }

            mesh["nVertices"].Values.Add(mesh["vertices"].Values.Count);
            mesh["nFaces"].Values.Add(mesh["faces"].Values.Count);

            meshNormals["nNormals"].Values.Add(meshNormals["normals"].Values.Count);
            meshNormals["nFaceNormals"].Values.Add(meshNormals["faceNormals"].Values.Count);
            mesh.Children.Add(new XChildObject(meshNormals, false));

            meshTextureCoords["nTextureCoords"].Values.Add(meshTextureCoords["textureCoords"].Values.Count);
            mesh.Children.Add(new XChildObject(meshTextureCoords, false));

            if (hasVertexColors)
            {
                meshVertexColors["nVertexColors"].Values.Add(meshVertexColors["vertexColors"].Values.Count);
                mesh.Children.Add(new XChildObject(meshVertexColors, false));
            }

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

        public static BCLFile ImportBCL(string filename)
        {
            var model = ModelRoot.Load(filename);
            var vertices = new List<Vector3>();
            var triangles = new List<BCLFile.Triangle>();

            if (model.LogicalMeshes.Count == 0)
                return new BCLFile(vertices, triangles);

            var originalMaterials = model.LogicalMaterials.ToList();
            var sortedMaterials = new List<Material>(originalMaterials);
            sortedMaterials.Sort((a, b) => {
                int idxA = ExtractMatIndex(a.Name);
                int idxB = ExtractMatIndex(b.Name);
                if (idxA == -1 && idxB == -1) return originalMaterials.IndexOf(a).CompareTo(originalMaterials.IndexOf(b));
                if (idxA == -1) return 1;
                if (idxB == -1) return -1;
                return idxA.CompareTo(idxB);
            });

            Dictionary<Material, int> materialToIndex = new Dictionary<Material, int>();
            int matIdx = 0;
            foreach (var gltfMat in sortedMaterials)
            {
                int originalIdx = ExtractMatIndex(gltfMat.Name);
                if (originalIdx != -1)
                {
                    materialToIndex[gltfMat] = originalIdx;
                }
                else
                {
                    materialToIndex[gltfMat] = matIdx;
                }
                matIdx++;
            }

            int vertexOffset = 0;

            // Iterate over all nodes to capture exact world positions of disjoint meshes
            foreach (var node in model.LogicalNodes.Where(n => n.Mesh != null))
            {
                System.Numerics.Matrix4x4 transform = node.WorldMatrix;

                foreach (var prim in node.Mesh.Primitives)
                {
                    var positions = prim.GetVertexAccessor("POSITION")?.AsVector3Array();
                    var uvs = prim.GetVertexAccessor("TEXCOORD_0")?.AsVector2Array();
                    var indices = prim.GetIndices();

                    if (positions == null || indices == null) continue;

                    for (int i = 0; i < positions.Count; i++)
                    {
                        var pTrans = System.Numerics.Vector3.Transform(positions[i], transform);
                        vertices.Add(new Vector3(pTrans.X, pTrans.Y, pTrans.Z));
                    }

                    ushort currentMaterial = 0;
                    if (prim.Material != null && materialToIndex.ContainsKey(prim.Material))
                    {
                        currentMaterial = (ushort)materialToIndex[prim.Material];
                    }

                    for (int i = 0; i < indices.Count; i += 3)
                    {
                        int v0 = (int)indices[i];
                        int v1 = (int)indices[i + 1];
                        int v2 = (int)indices[i + 2];

                        // Filter out the dummy triangles created by the exporter for material preservation
                        if (uvs != null && v0 < uvs.Count && uvs[v0].X < -9000f)
                        {
                            continue;
                        }

                        triangles.Add(new BCLFile.Triangle((ushort)(v0 + vertexOffset), (ushort)(v1 + vertexOffset), (ushort)(v2 + vertexOffset), currentMaterial));
                    }

                    vertexOffset += positions.Count;
                }
            }

            return new BCLFile(vertices, triangles);
        }
    }
}