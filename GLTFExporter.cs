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
        public static void Export(XFile xFile, BHDFile bhd, bool preserveUnusedMaterials, string outputPath)
        {
            if (bhd != null)
            {
                ExportSkinned(xFile, bhd, preserveUnusedMaterials, outputPath);
            }
            else
            {
                ExportStatic(xFile, preserveUnusedMaterials, outputPath);
            }
        }

        // BATCH EXPORT: Groups multiple XFiles and their distinct objects into a single GLTF scene
        public static void ExportCombined(List<XFile> xFiles, List<string> names, BHDFile bhd, bool preserveUnusedMaterials, string outputPath)
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

                // Build unused bones group in memory (appended later to protect importer armature detection)
                var unusedRoot = new NodeBuilder(combinedName + "_UnusedBones");
                unusedRoot.LocalTransform = System.Numerics.Matrix4x4.CreateScale(0.0001f);

                // Build SRP data block in memory
                var srpRoot = new NodeBuilder(combinedName + "_SaffireRestPoseData");
                srpRoot.LocalTransform = System.Numerics.Matrix4x4.CreateScale(0.0001f);

                for (int i = 0; i < bhd.Bones.Count; i++)
                {
                    var m = bhd.Bones[i].Transform;
                    bool isUnused = float.IsNaN(m.M11);

                    if (bhd.Bones[i].ParentIndex != 0xFFFFFFFF && bhd.Bones[i].ParentIndex != i)
                    {
                        gltfNodes[bhd.Bones[i].ParentIndex].AddNode(gltfNodes[i]);
                    }
                    else
                    {
                        if (isUnused)
                        {
                            unusedRoot.AddNode(gltfNodes[i]);
                        }
                        else
                        {
                            masterSkeletonRoot.AddNode(gltfNodes[i]);
                        }
                    }

                    // Duplicate the pure rest matrix into hidden nodes using pure translation to survive Blender TRS decomposition
                    var srpNode = new NodeBuilder("SRP_" + nameSlots[i]);

                    if (isUnused) m = Matrix.Identity; // FIX: Prevent NaN values from crashing SharpGLTF

                    srpNode.LocalTransform = ConvertBhdMatrix(m);
                    srpRoot.AddNode(srpNode);

                    var r1 = new NodeBuilder("R1_" + nameSlots[i]);
                    r1.LocalTransform = System.Numerics.Matrix4x4.CreateTranslation(m.M11, m.M12, m.M13);
                    srpNode.AddNode(r1);

                    var r2 = new NodeBuilder("R2_" + nameSlots[i]);
                    r2.LocalTransform = System.Numerics.Matrix4x4.CreateTranslation(m.M21, m.M22, m.M23);
                    srpNode.AddNode(r2);

                    var r3 = new NodeBuilder("R3_" + nameSlots[i]);
                    r3.LocalTransform = System.Numerics.Matrix4x4.CreateTranslation(m.M31, m.M32, m.M33);
                    srpNode.AddNode(r3);

                    var r4 = new NodeBuilder("R4_" + nameSlots[i]);
                    r4.LocalTransform = System.Numerics.Matrix4x4.CreateTranslation(m.M41, m.M42, m.M43);
                    srpNode.AddNode(r4);

                    var r5 = new NodeBuilder("R5_" + nameSlots[i]);
                    r5.LocalTransform = System.Numerics.Matrix4x4.CreateTranslation(m.M14, m.M24, m.M34);
                    srpNode.AddNode(r5);
                }

                // Append hidden groups at the end so the importer parses active bones first
                masterSkeletonRoot.AddNode(unusedRoot);
                masterSkeletonRoot.AddNode(srpRoot);
            }

            // Loop through all collected meshes and add them individually to the scene
            for (int i = 0; i < xFiles.Count; i++)
            {
                var xFile = xFiles[i];
                var baseName = names[i];

                if (bhd != null)
                {
                    var meshBuilder = new MeshBuilder<VertexPositionNormal, VertexColor1Texture1, VertexJoints4>(baseName);
                    ParseMeshes(xFile, null, meshBuilder, gltfNodes, preserveUnusedMaterials);
                    scene.AddSkinnedMesh(meshBuilder, System.Numerics.Matrix4x4.Identity, gltfNodes);
                }
                else
                {
                    var meshBuilder = new MeshBuilder<VertexPositionNormal, VertexColor1Texture1, VertexEmpty>(baseName);
                    ParseMeshes(xFile, meshBuilder, null, null, preserveUnusedMaterials);

                    // Append _Root to static mesh parent nodes
                    var rootNode = new NodeBuilder(baseName + "_Root");
                    scene.AddNode(rootNode);
                    scene.AddRigidMesh(meshBuilder, rootNode);
                }
            }

            var model = scene.ToGltf2();
            model.SaveGLB(outputPath);
        }

        private static void ExportStatic(XFile xFile, bool preserveUnusedMaterials, string outputPath)
        {
            string baseName = System.IO.Path.GetFileNameWithoutExtension(outputPath);

            var scene = new SceneBuilder();
            var meshBuilder = new MeshBuilder<VertexPositionNormal, VertexColor1Texture1, VertexEmpty>(baseName);

            ParseMeshes(xFile, meshBuilder, null, null, preserveUnusedMaterials);

            // Append _Root to prevent namespace collision
            var rootNode = new NodeBuilder(baseName + "_Root");
            scene.AddNode(rootNode);
            scene.AddRigidMesh(meshBuilder, rootNode);

            var model = scene.ToGltf2();
            model.SaveGLB(outputPath);
        }

        private static void ExportSkinned(XFile xFile, BHDFile bhd, bool preserveUnusedMaterials, string outputPath)
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

            // Build unused bones group in memory (appended later to protect importer armature detection)
            var unusedRoot = new NodeBuilder(baseName + "_UnusedBones");
            unusedRoot.LocalTransform = System.Numerics.Matrix4x4.CreateScale(0.0001f);

            // Build SRP data block in memory
            var srpRoot = new NodeBuilder(baseName + "_SaffireRestPoseData");
            srpRoot.LocalTransform = System.Numerics.Matrix4x4.CreateScale(0.0001f);

            for (int i = 0; i < bhd.Bones.Count; i++)
            {
                var m = bhd.Bones[i].Transform;
                bool isUnused = float.IsNaN(m.M11);

                if (bhd.Bones[i].ParentIndex != 0xFFFFFFFF && bhd.Bones[i].ParentIndex != i)
                {
                    gltfNodes[bhd.Bones[i].ParentIndex].AddNode(gltfNodes[i]);
                }
                else
                {
                    if (isUnused)
                    {
                        unusedRoot.AddNode(gltfNodes[i]);
                    }
                    else
                    {
                        masterSkeletonRoot.AddNode(gltfNodes[i]);
                    }
                }

                // Duplicate the pure rest matrix into hidden nodes using pure translation to survive Blender TRS decomposition
                var srpNode = new NodeBuilder("SRP_" + nameSlots[i]);

                if (isUnused) m = Matrix.Identity; // FIX: Prevent NaN values from crashing SharpGLTF

                srpNode.LocalTransform = ConvertBhdMatrix(m);
                srpRoot.AddNode(srpNode);

                var r1 = new NodeBuilder("R1_" + nameSlots[i]);
                r1.LocalTransform = System.Numerics.Matrix4x4.CreateTranslation(m.M11, m.M12, m.M13);
                srpNode.AddNode(r1);

                var r2 = new NodeBuilder("R2_" + nameSlots[i]);
                r2.LocalTransform = System.Numerics.Matrix4x4.CreateTranslation(m.M21, m.M22, m.M23);
                srpNode.AddNode(r2);

                var r3 = new NodeBuilder("R3_" + nameSlots[i]);
                r3.LocalTransform = System.Numerics.Matrix4x4.CreateTranslation(m.M31, m.M32, m.M33);
                srpNode.AddNode(r3);

                var r4 = new NodeBuilder("R4_" + nameSlots[i]);
                r4.LocalTransform = System.Numerics.Matrix4x4.CreateTranslation(m.M41, m.M42, m.M43);
                srpNode.AddNode(r4);

                var r5 = new NodeBuilder("R5_" + nameSlots[i]);
                r5.LocalTransform = System.Numerics.Matrix4x4.CreateTranslation(m.M14, m.M24, m.M34);
                srpNode.AddNode(r5);
            }

            // Append hidden groups at the end so the importer parses active bones first
            masterSkeletonRoot.AddNode(unusedRoot);
            masterSkeletonRoot.AddNode(srpRoot);

            ParseMeshes(xFile, null, meshBuilder, gltfNodes, preserveUnusedMaterials);

            scene.AddSkinnedMesh(meshBuilder, System.Numerics.Matrix4x4.Identity, gltfNodes);

            var model = scene.ToGltf2();
            model.SaveGLB(outputPath);
        }

        public static void ExportBCL(BCLFile bcl, string outputPath)
        {
            string baseName = System.IO.Path.GetFileNameWithoutExtension(outputPath);

            var scene = new SceneBuilder();
            var meshBuilder = new MeshBuilder<VertexPositionNormal, VertexColor1Texture1, VertexEmpty>(baseName);

            // 1. Determine the absolute maximum material index used by any triangle in the BCL
            int maxMat = -1;
            if (bcl.Triangles.Count > 0)
            {
                maxMat = bcl.Triangles.Max(t => t.Unk01);
            }

            // 2. Generate a continuous array of materials from 0 to maxMat to ensure order and unused slots are locked in
            var materials = new List<MaterialBuilder>();
            for (int i = 0; i <= maxMat; i++)
            {
                string matName = $"MAT_{i:D3}_Collision";
                materials.Add(new MaterialBuilder(matName).WithMetallicRoughnessShader());
            }

            if (materials.Count == 0)
            {
                materials.Add(new MaterialBuilder("MAT_000_Collision").WithMetallicRoughnessShader());
            }

            // 3. Inject the UV(-9999, -9999) dummy triangle exploit for every material so Blender is forced to keep the empty ones
            for (int mIdx = 0; mIdx < materials.Count; mIdx++)
            {
                var mat = materials[mIdx];
                var prim = meshBuilder.UsePrimitive(mat);

                var v0 = new StaticVertexBuilder(); v0.Geometry.Position = new System.Numerics.Vector3(0, 0, 0); v0.Geometry.Normal = new System.Numerics.Vector3(0, 1, 0); v0.Material.TexCoord = new System.Numerics.Vector2(-9999f, -9999f); v0.Material.Color = new System.Numerics.Vector4(1, 1, 1, 1);
                var v1 = new StaticVertexBuilder(); v1.Geometry.Position = new System.Numerics.Vector3(0.01f, 0, 0); v1.Geometry.Normal = new System.Numerics.Vector3(0, 1, 0); v1.Material.TexCoord = new System.Numerics.Vector2(-9999f, -9999f); v1.Material.Color = new System.Numerics.Vector4(1, 1, 1, 1);
                var v2 = new StaticVertexBuilder(); v2.Geometry.Position = new System.Numerics.Vector3(0, 0.01f, 0); v2.Geometry.Normal = new System.Numerics.Vector3(0, 1, 0); v2.Material.TexCoord = new System.Numerics.Vector2(-9999f, -9999f); v2.Material.Color = new System.Numerics.Vector4(1, 1, 1, 1);

                prim.AddTriangle(v0, v1, v2);
            }

            // 4. Build the actual collision geometry, mapping each face to the correct unified material index
            foreach (var t in bcl.Triangles)
            {
                var mat = materials[t.Unk01];
                var prim = meshBuilder.UsePrimitive(mat);

                var v1 = bcl.Vertices[t.Index1];
                var v2 = bcl.Vertices[t.Index2];
                var v3 = bcl.Vertices[t.Index3];

                // Add basic surface normals to make SharpGLTF valid
                var d1 = v2 - v1;
                var d2 = v3 - v1;
                var norm = Vector3.Cross(d1, d2);
                norm.Normalize();
                if (float.IsNaN(norm.X)) norm = new Vector3(0, 1, 0);

                var n = new System.Numerics.Vector3(norm.X, norm.Y, norm.Z);

                var sv1 = new StaticVertexBuilder(); sv1.Geometry.Position = new System.Numerics.Vector3(v1.X, v1.Y, v1.Z); sv1.Geometry.Normal = n; sv1.Material.TexCoord = new System.Numerics.Vector2(0, 0); sv1.Material.Color = new System.Numerics.Vector4(1, 1, 1, 1);
                var sv2 = new StaticVertexBuilder(); sv2.Geometry.Position = new System.Numerics.Vector3(v2.X, v2.Y, v2.Z); sv2.Geometry.Normal = n; sv2.Material.TexCoord = new System.Numerics.Vector2(0, 0); sv2.Material.Color = new System.Numerics.Vector4(1, 1, 1, 1);
                var sv3 = new StaticVertexBuilder(); sv3.Geometry.Position = new System.Numerics.Vector3(v3.X, v3.Y, v3.Z); sv3.Geometry.Normal = n; sv3.Material.TexCoord = new System.Numerics.Vector2(0, 0); sv3.Material.Color = new System.Numerics.Vector4(1, 1, 1, 1);

                prim.AddTriangle(sv1, sv2, sv3);
            }

            var rootNode = new NodeBuilder(baseName + "_Root");
            scene.AddNode(rootNode);
            scene.AddRigidMesh(meshBuilder, rootNode);

            var model = scene.ToGltf2();
            model.SaveGLB(outputPath);
        }

        public static void ExportOCL(OCLFile ocl, string outputPath)
        {
            string baseName = System.IO.Path.GetFileNameWithoutExtension(outputPath);

            var scene = new SceneBuilder();
            var meshBuilder = new MeshBuilder<VertexPositionNormal, VertexColor1Texture1, VertexEmpty>(baseName);

            // 1. Traverse octree and gather all triangles
            var allTriangles = new List<OCLFile.OctreeNode.Triangle>();
            GatherOCLTriangles(ocl.RootNode, allTriangles);

            // 2. Determine the absolute maximum material index used by any triangle in the OCL
            int maxMat = -1;
            if (allTriangles.Count > 0)
            {
                maxMat = (int)allTriangles.Max(t => t.MaterialIndex);
            }

            // 3. Generate a continuous array of materials from 0 to maxMat to ensure order and unused slots are locked in
            var materials = new List<MaterialBuilder>();
            for (int i = 0; i <= maxMat; i++)
            {
                string matName = $"MAT_{i:D3}_Collision";
                materials.Add(new MaterialBuilder(matName).WithMetallicRoughnessShader());
            }

            if (materials.Count == 0)
            {
                materials.Add(new MaterialBuilder("MAT_000_Collision").WithMetallicRoughnessShader());
            }

            // 4. Inject the UV(-9999, -9999) dummy triangle exploit for every material so Blender is forced to keep the empty ones
            for (int mIdx = 0; mIdx < materials.Count; mIdx++)
            {
                var mat = materials[mIdx];
                var prim = meshBuilder.UsePrimitive(mat);

                var v0 = new StaticVertexBuilder(); v0.Geometry.Position = new System.Numerics.Vector3(0, 0, 0); v0.Geometry.Normal = new System.Numerics.Vector3(0, 1, 0); v0.Material.TexCoord = new System.Numerics.Vector2(-9999f, -9999f); v0.Material.Color = new System.Numerics.Vector4(1, 1, 1, 1);
                var v1 = new StaticVertexBuilder(); v1.Geometry.Position = new System.Numerics.Vector3(0.01f, 0, 0); v1.Geometry.Normal = new System.Numerics.Vector3(0, 1, 0); v1.Material.TexCoord = new System.Numerics.Vector2(-9999f, -9999f); v1.Material.Color = new System.Numerics.Vector4(1, 1, 1, 1);
                var v2 = new StaticVertexBuilder(); v2.Geometry.Position = new System.Numerics.Vector3(0, 0.01f, 0); v2.Geometry.Normal = new System.Numerics.Vector3(0, 1, 0); v2.Material.TexCoord = new System.Numerics.Vector2(-9999f, -9999f); v2.Material.Color = new System.Numerics.Vector4(1, 1, 1, 1);

                prim.AddTriangle(v0, v1, v2);
            }

            // 5. Build the actual collision geometry, mapping each face to the correct unified material index
            foreach (var t in allTriangles)
            {
                var mat = materials[(int)t.MaterialIndex];
                var prim = meshBuilder.UsePrimitive(mat);

                var v1 = t.Position1;
                var v2 = t.Position2;
                var v3 = t.Position3;

                var n = new System.Numerics.Vector3(t.Normal.X, t.Normal.Y, t.Normal.Z);

                var sv1 = new StaticVertexBuilder(); sv1.Geometry.Position = new System.Numerics.Vector3(v1.X, v1.Y, v1.Z); sv1.Geometry.Normal = n; sv1.Material.TexCoord = new System.Numerics.Vector2(0, 0); sv1.Material.Color = new System.Numerics.Vector4(1, 1, 1, 1);
                var sv2 = new StaticVertexBuilder(); sv2.Geometry.Position = new System.Numerics.Vector3(v2.X, v2.Y, v2.Z); sv2.Geometry.Normal = n; sv2.Material.TexCoord = new System.Numerics.Vector2(0, 0); sv2.Material.Color = new System.Numerics.Vector4(1, 1, 1, 1);
                var sv3 = new StaticVertexBuilder(); sv3.Geometry.Position = new System.Numerics.Vector3(v3.X, v3.Y, v3.Z); sv3.Geometry.Normal = n; sv3.Material.TexCoord = new System.Numerics.Vector2(0, 0); sv3.Material.Color = new System.Numerics.Vector4(1, 1, 1, 1);

                prim.AddTriangle(sv1, sv2, sv3);
            }

            var rootNode = new NodeBuilder(baseName + "_Root");
            scene.AddNode(rootNode);
            scene.AddRigidMesh(meshBuilder, rootNode);

            var model = scene.ToGltf2();
            model.SaveGLB(outputPath);
        }

        private static void GatherOCLTriangles(OCLFile.OctreeNode node, List<OCLFile.OctreeNode.Triangle> list)
        {
            if (node == null) return;
            list.AddRange(node.Triangles);
            if (node.Children != null)
            {
                foreach (var child in node.Children)
                {
                    GatherOCLTriangles(child, list);
                }
            }
        }

        private static void ParseMeshes(XFile xFile, IMeshBuilder<MaterialBuilder> staticBuilder, IMeshBuilder<MaterialBuilder> skinnedBuilder, NodeBuilder[] skeletonNodes, bool preserveUnusedMaterials)
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

                                    // MATERIAL ORDER PRESERVATION: Prepend index to safely extract it when importing back from Blender
                                    string matName = $"MAT_{materials.Count:D3}_{System.IO.Path.GetFileNameWithoutExtension(texName)}";
                                    var matBuilder = new MaterialBuilder(matName).WithMetallicRoughnessShader();
                                    materials.Add(matBuilder);
                                }
                            }
                        }

                        if (materials.Count == 0) materials.Add(new MaterialBuilder("dummy.dds").WithMetallicRoughnessShader());

                        if (preserveUnusedMaterials)
                        {
                            // UNUSED MATERIAL FIX: Inject a valid dummy triangle tagged with UV(-9999, -9999) to force Blender to keep the material
                            for (int mIdx = 0; mIdx < materials.Count; mIdx++)
                            {
                                var mat = materials[mIdx];
                                if (skinnedBuilder != null)
                                {
                                    var prim = skinnedBuilder.UsePrimitive(mat);
                                    var v0 = new SkinnedVertexBuilder(); v0.Geometry.Position = new System.Numerics.Vector3(0, 0, 0); v0.Geometry.Normal = new System.Numerics.Vector3(0, 1, 0); v0.Material.TexCoord = new System.Numerics.Vector2(-9999f, -9999f); v0.Material.Color = new System.Numerics.Vector4(1, 1, 1, 1); v0.Skinning.SetBindings((0, 1.0f));
                                    var v1 = new SkinnedVertexBuilder(); v1.Geometry.Position = new System.Numerics.Vector3(0.01f, 0, 0); v1.Geometry.Normal = new System.Numerics.Vector3(0, 1, 0); v1.Material.TexCoord = new System.Numerics.Vector2(-9999f, -9999f); v1.Material.Color = new System.Numerics.Vector4(1, 1, 1, 1); v1.Skinning.SetBindings((0, 1.0f));
                                    var v2 = new SkinnedVertexBuilder(); v2.Geometry.Position = new System.Numerics.Vector3(0, 0.01f, 0); v2.Geometry.Normal = new System.Numerics.Vector3(0, 1, 0); v2.Material.TexCoord = new System.Numerics.Vector2(-9999f, -9999f); v2.Material.Color = new System.Numerics.Vector4(1, 1, 1, 1); v2.Skinning.SetBindings((0, 1.0f));
                                    prim.AddTriangle(v0, v1, v2);
                                }
                                else if (staticBuilder != null)
                                {
                                    var prim = staticBuilder.UsePrimitive(mat);
                                    var v0 = new StaticVertexBuilder(); v0.Geometry.Position = new System.Numerics.Vector3(0, 0, 0); v0.Geometry.Normal = new System.Numerics.Vector3(0, 1, 0); v0.Material.TexCoord = new System.Numerics.Vector2(-9999f, -9999f); v0.Material.Color = new System.Numerics.Vector4(1, 1, 1, 1);
                                    var v1 = new StaticVertexBuilder(); v1.Geometry.Position = new System.Numerics.Vector3(0.01f, 0, 0); v1.Geometry.Normal = new System.Numerics.Vector3(0, 1, 0); v1.Material.TexCoord = new System.Numerics.Vector2(-9999f, -9999f); v1.Material.Color = new System.Numerics.Vector4(1, 1, 1, 1);
                                    var v2 = new StaticVertexBuilder(); v2.Geometry.Position = new System.Numerics.Vector3(0, 0.01f, 0); v2.Geometry.Normal = new System.Numerics.Vector3(0, 1, 0); v2.Material.TexCoord = new System.Numerics.Vector2(-9999f, -9999f); v2.Material.Color = new System.Numerics.Vector4(1, 1, 1, 1);
                                    prim.AddTriangle(v0, v1, v2);
                                }
                            }
                        }

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

            // Alpha Clamping: Hardcode to 1.0f (W) so Blender doesn't render meshes with 0.0 game alpha as invisible
            col = colorMap.TryGetValue(vIndex, out Vector4 color) ? new System.Numerics.Vector4(color.X, color.Y, color.Z, 1.0f) : new System.Numerics.Vector4(1, 1, 1, 1);
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