using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Xml.Linq;
using D3DX.Mesh;
using SharpDX;

namespace LOMNTool.Collada
{
    public static class Utils
    {
        private const string SCHEMA_URL = "http://www.collada.org/2005/11/COLLADASchema";

        public static void ExportCOLLADA(XFile file, string filename, Matrix transform, bool flipV = true, string textureExtension = null)
        {
            XNamespace ns = SCHEMA_URL;
            XDocument doc = new XDocument();
            XElement COLLADA = new XElement(ns + "COLLADA", new XAttribute("xmlns", SCHEMA_URL), new XAttribute("version", "1.4.1"));

            COLLADA.Add(new XElement(ns + "asset",
                new XElement(ns + "contributor",
                    new XElement(ns + "author"),
                    new XElement(ns + "authoring_tool", "LOMNTool " + System.Reflection.Assembly.GetAssembly(typeof(Utils)).GetName().Version.ToString()),
                    new XElement(ns + "comments")),
                new XElement(ns + "created", DateTime.Now.ToString("o")),
                new XElement(ns + "keywords"),
                new XElement(ns + "modified", DateTime.Now.ToString("o")),
                new XElement(ns + "unit", new XAttribute("meter", "0.01"), new XAttribute("name", "centimeter")),
                new XElement(ns + "up_axis", "Y_UP")));

            XElement library_images = new XElement(ns + "library_images");
            XElement library_materials = new XElement(ns + "library_materials");
            XElement library_effects = new XElement(ns + "library_effects");
            XElement library_geometries = new XElement(ns + "library_geometries");
            XElement visual_scene = new XElement(ns + "visual_scene", new XAttribute("id", "Scene"), new XAttribute("name", "Scene"));

            int meshID = 1;
            foreach (D3DX.Mesh.XObject frame in file.Objects)
            {
                foreach (XChildObject frameChild in frame.Children)
                {
                    D3DX.Mesh.XObject obj = frameChild.Object;
                    if (obj.DataType.ID == XToken.TokenID.NAME && obj.DataType.NameData == "Mesh")
                    {
                        int vertexCount = (int)obj["nVertices"].Values[0];
                        int faceCount = (int)obj["nFaces"].Values[0];

                        bool hasNormals = false;
                        bool hasTexCoords = false;
                        bool hasColors = false;

                        D3DX.Mesh.XObject normalObject = null;

                        XElement geometry = new XElement(ns + "geometry", new XAttribute("id", "Mesh" + meshID + "-GEOMETRY"), new XAttribute("name", "Mesh" + meshID));
                        XElement mesh = new XElement(ns + "mesh");
                        XElement bindMaterialCommon = new XElement(ns + "technique_common");
                        geometry.Add(mesh);

                        int materialCount = -1;
                        // Maps material indices to face indices
                        Dictionary<int, List<int>> materialGroups = null;

                        List<string> posList = new List<string>();
                        for (int i = 0; i < vertexCount; i++)
                        {
                            //XObjectStructure vec = ;
                            Vector3 vec = XUtils.Vector((XObjectStructure)obj["vertices"].Values[i]);
                            Vector4 newVec = Vector4.Transform(new Vector4(vec.X, vec.Y, vec.Z, 1.0f), transform);
                            posList.Add(newVec.X.ToString(System.Globalization.CultureInfo.InvariantCulture.NumberFormat));
                            posList.Add(newVec.Y.ToString(System.Globalization.CultureInfo.InvariantCulture.NumberFormat));
                            posList.Add(newVec.Z.ToString(System.Globalization.CultureInfo.InvariantCulture.NumberFormat));
                        }
                        /*FloatDataSource positions = new FloatDataSource("mesh_positions", "mesh_positions_array", posList,
                            new DataAccessorParam("X", "float"),
                            new DataAccessorParam("Y", "float"),
                            new DataAccessorParam("Z", "float"));*/

                        XElement posSource = new XElement(ns + "source", new XAttribute("id", "Mesh" + meshID + "-POSITION"));
                        XElement posArray = new XElement(ns + "float_array", new XAttribute("id", "Mesh" + meshID + "-POSITION-array"), new XAttribute("count", vertexCount * 3 + ""));
                        posArray.Add(String.Join(" ", posList));
                        posSource.Add(posArray);
                        posSource.Add(new XElement(ns + "technique_common",
                            new XElement(ns + "accessor", new XAttribute("source", "#Mesh" + meshID + "-POSITION-array"), new XAttribute("count", vertexCount.ToString()), new XAttribute("stride", "3"),
                                new XElement(ns + "param", new XAttribute("name", "X"), new XAttribute("type", "float")),
                                new XElement(ns + "param", new XAttribute("name", "Y"), new XAttribute("type", "float")),
                                new XElement(ns + "param", new XAttribute("name", "Z"), new XAttribute("type", "float")))));
                        mesh.Add(posSource);

                        foreach (XChildObject child in obj.Children)
                        {
                            if (child.Object.DataType.NameData == "MeshNormals")
                            {
                                hasNormals = true;
                                normalObject = child.Object;

                                List<string> normList = new List<string>();
                                foreach (XObjectStructure value in child.Object["normals"].Values)
                                {
                                    Vector3 vec = XUtils.Vector(value);
                                    Vector4 newVec = Vector4.Transform(new Vector4(vec.X, vec.Y, vec.Z, 0.0f), transform);
                                    normList.Add(newVec.X.ToString(System.Globalization.CultureInfo.InvariantCulture.NumberFormat));
                                    normList.Add(newVec.Y.ToString(System.Globalization.CultureInfo.InvariantCulture.NumberFormat));
                                    normList.Add(newVec.Z.ToString(System.Globalization.CultureInfo.InvariantCulture.NumberFormat));
                                }

                                XElement source = new XElement(ns + "source", new XAttribute("id", "Mesh" + meshID + "-NORMAL"));
                                XElement array = new XElement(ns + "float_array", new XAttribute("id", "Mesh" + meshID + "-NORMAL-array"), new XAttribute("count", (int)child.Object["nNormals"].Values[0] * 3 + ""));
                                array.Add(String.Join(" ", normList));
                                source.Add(array);
                                source.Add(new XElement(ns + "technique_common",
                                    new XElement(ns + "accessor", new XAttribute("source", "#Mesh" + meshID + "-NORMAL-array"), new XAttribute("count", (int)child.Object["nNormals"].Values[0] + ""), new XAttribute("stride", "3"),
                                        new XElement(ns + "param", new XAttribute("name", "X"), new XAttribute("type", "float")),
                                        new XElement(ns + "param", new XAttribute("name", "Y"), new XAttribute("type", "float")),
                                        new XElement(ns + "param", new XAttribute("name", "Z"), new XAttribute("type", "float")))));
                                mesh.Add(source);
                            }
                            else if (child.Object.DataType.NameData == "MeshTextureCoords")
                            {
                                hasTexCoords = true;

                                List<string> uvList = new List<string>();
                                foreach (XObjectStructure value in child.Object["textureCoords"].Values)
                                {
                                    double u = Convert.ToDouble(value["u"].Values[0]);
                                    double v = Convert.ToDouble(value["v"].Values[0]);
                                    if (flipV)
                                        v = 1.0 - v;
                                    uvList.Add(u.ToString(System.Globalization.CultureInfo.InvariantCulture.NumberFormat));
                                    uvList.Add(v.ToString(System.Globalization.CultureInfo.InvariantCulture.NumberFormat));
                                }

                                XElement source = new XElement(ns + "source", new XAttribute("id", "Mesh" + meshID + "-UV"));
                                XElement array = new XElement(ns + "float_array", new XAttribute("id", "Mesh" + meshID + "-UV-array"), new XAttribute("count", (int)child.Object["nTextureCoords"].Values[0] * 2 + ""));
                                array.Add(String.Join(" ", uvList));
                                source.Add(array);
                                source.Add(new XElement(ns + "technique_common",
                                    new XElement(ns + "accessor", new XAttribute("source", "#Mesh" + meshID + "-UV-array"), new XAttribute("count", (int)child.Object["nTextureCoords"].Values[0] + ""), new XAttribute("stride", "2"),
                                        new XElement(ns + "param", new XAttribute("name", "S"), new XAttribute("type", "float")),
                                        new XElement(ns + "param", new XAttribute("name", "T"), new XAttribute("type", "float")))));
                                mesh.Add(source);
                            }
                            else if (child.Object.DataType.NameData == "MeshVertexColors")
                            {
                                hasColors = true;

                                List<string> colorList = new List<string>();
                                Vector4[] colors = new Vector4[(int)child.Object["nVertexColors"].Values[0]];
                                foreach (XObjectStructure value in child.Object["vertexColors"].Values)
                                {
                                    int index = (int)value["index"].Values[0];
                                    Vector4 color = XUtils.ColorRGBA((XObjectStructure)value.Members[1].Values[0]); // ["indexColor"]
                                    if (colors[index] == Vector4.Zero)
                                    {
                                        colors[index] = color;
                                    }
                                    else
                                    {
                                        Console.WriteLine("ExportCOLLADA: Multiple colors defined for vertex " + index + "!");
                                    }
                                }

                                foreach (Vector4 v in colors)
                                {
                                    colorList.Add(v.X.ToString(System.Globalization.CultureInfo.InvariantCulture.NumberFormat));
                                    colorList.Add(v.Y.ToString(System.Globalization.CultureInfo.InvariantCulture.NumberFormat));
                                    colorList.Add(v.Z.ToString(System.Globalization.CultureInfo.InvariantCulture.NumberFormat));
                                    colorList.Add(v.W.ToString(System.Globalization.CultureInfo.InvariantCulture.NumberFormat));
                                }

                                XElement source = new XElement(ns + "source", new XAttribute("id", "Mesh" + meshID + "-COLOR"));
                                XElement array = new XElement(ns + "float_array", new XAttribute("id", "Mesh" + meshID + "-COLOR-array"), new XAttribute("count", (int)child.Object["nVertexColors"].Values[0] * 4 + ""));
                                array.Add(String.Join(" ", colorList));
                                source.Add(array);
                                source.Add(new XElement(ns + "technique_common",
                                    new XElement(ns + "accessor", new XAttribute("source", "#Mesh" + meshID + "-COLOR-array"), new XAttribute("count", (int)child.Object["nVertexColors"].Values[0] + ""), new XAttribute("stride", "4"),
                                        new XElement(ns + "param", new XAttribute("name", "R"), new XAttribute("type", "float")),
                                        new XElement(ns + "param", new XAttribute("name", "G"), new XAttribute("type", "float")),
                                        new XElement(ns + "param", new XAttribute("name", "B"), new XAttribute("type", "float")),
                                        new XElement(ns + "param", new XAttribute("name", "A"), new XAttribute("type", "float")))));
                                mesh.Add(source);
                            }
                            else if (child.Object.DataType.NameData == "MeshMaterialList")
                            {
                                materialCount = (int)child.Object["nMaterials"].Values[0];
                                int nFaceIndexes = (int)child.Object["nFaceIndexes"].Values[0];
                                materialGroups = new Dictionary<int, List<int>>();

                                for (int i = 0; i < nFaceIndexes; i++)
                                {
                                    int matIndex = (int)child.Object["faceIndexes"].Values[i];
                                    if (materialGroups.ContainsKey(matIndex))
                                        materialGroups[matIndex].Add(i);
                                    else
                                        materialGroups.Add(matIndex, new List<int>(new int[] { i }));
                                }

                                foreach (XChildObject material in child.Object.Children)
                                {
                                    Vector4 faceColor = XUtils.ColorRGBA((XObjectStructure)material.Object["faceColor"].Values[0]);
                                    double power = Convert.ToDouble(material.Object["power"].Values[0]);
                                    Vector3 specularColor = XUtils.ColorRGB((XObjectStructure)material.Object["specularColor"].Values[0]);
                                    Vector3 emissiveColor = XUtils.ColorRGB((XObjectStructure)material.Object["emissiveColor"].Values[0]);

                                    int materialIndex = library_materials.Elements().Count();

                                    // Material
                                    library_materials.Add(new XElement(ns + "material", new XAttribute("id", "Material" + materialIndex), new XAttribute("name", "Material" + materialIndex),
                                        new XElement(ns + "instance_effect", new XAttribute("url", "#Material" + materialIndex + "-EFFECT"))));

                                    // Bindings in Geometry Instance
                                    bindMaterialCommon.Add(new XElement(ns + "instance_material", new XAttribute("symbol", "Material" + materialIndex), new XAttribute("target", "#Material" + materialIndex)));

                                    // Texture
                                    bool hasTexture = false;
                                    string textureID = "";
                                    XElement textureReference = null;
                                    foreach (XChildObject texChild in material.Object.Children)
                                    {
                                        if (texChild.Object.DataType.NameData == "TextureFilename")
                                        {
                                            hasTexture = true;
                                            textureID = "Texture" + library_images.Elements().Count();
                                            string texFilename = (string)texChild.Object["filename"].Values[0];
                                            if (textureExtension != null)
                                                texFilename = System.IO.Path.ChangeExtension(texFilename, textureExtension);

                                            library_images.Add(new XElement(ns + "image", new XAttribute("id", textureID), new XAttribute("name", textureID),
                                                new XElement(ns + "init_from", "file://" + texFilename)));

                                            textureReference = new XElement(ns + "texture", new XAttribute("texture", textureID), new XAttribute("texcoord", "CHANNEL0")); // TODO: Add Maya-specific wrap info

                                            break; // There should only be one TextureFilename per Material.
                                        }
                                    }

                                    // Effect
                                    library_effects.Add(new XElement(ns + "effect", new XAttribute("id", "Material" + materialIndex + "-EFFECT"), new XAttribute("name", "Material" + materialIndex),
                                        new XElement(ns + "profile_COMMON",
                                            new XElement(ns + "technique", new XAttribute("sid", "standard"),
                                                new XElement(ns + "phong",
                                                    new XElement(ns + "emission",
                                                        new XElement(ns + "color", new XAttribute("sid", "emission"), "0.0 0.0 0.0 1.0")),
                                                    new XElement(ns + "ambient",
                                                        new XElement(ns + "color", new XAttribute("sid", "ambient"), emissiveColor.X + " " + emissiveColor.Y + " " + emissiveColor.Z + " 1.000000")),
                                                    new XElement(ns + "diffuse",
                                                        hasTexture ? textureReference : new XElement(ns + "color", new XAttribute("sid", "diffuse"), faceColor.X + " " + faceColor.Y + " " + faceColor.Z + " 1.0")),
                                                    new XElement(ns + "specular",
                                                        new XElement(ns + "color", new XAttribute("sid", "specular"), power > 0.0 ? specularColor.X + " " + specularColor.Y + " " + specularColor.Z + " 1.0" : "0.0 0.0 0.0 1.0")),
                                                    new XElement(ns + "shininess",
                                                        new XElement(ns + "float", new XAttribute("sid", "shininess"), power.ToString())),
                                                    new XElement(ns + "reflective",
                                                        new XElement(ns + "color", new XAttribute("sid", "reflective"), "0.0 0.0 0.0 1.0")),
                                                    new XElement(ns + "reflectivity",
                                                        new XElement(ns + "float", new XAttribute("sid", "reflectivity"), "0.5")),
                                                    new XElement(ns + "transparent", new XAttribute("opaque", "RGB_ZERO"),
                                                        new XElement(ns + "color", new XAttribute("sid", "transparent"), "0.0 0.0 0.0 1.0")),
                                                    new XElement(ns + "transparency",
                                                        new XElement(ns + "float", new XAttribute("sid", "transparency"), faceColor.W.ToString())))))));
                                }
                            }
                        }

                        mesh.Add(new XElement(ns + "vertices", new XAttribute("id", "Mesh" + meshID + "-VERTEX"),
                            new XElement(ns + "input", new XAttribute("semantic", "POSITION"), new XAttribute("source", "#Mesh" + meshID + "-POSITION"))));

                        if (materialGroups == null)
                        {
                            throw new FormatException("ExportCOLLADA: Mesh didn't have a MeshMaterialList!");
                        }
                        else
                        {
                            foreach (KeyValuePair<int, List<int>> pair in materialGroups)
                            {
                                int offset = 0;
                                XElement triangles = new XElement(ns + "triangles", new XAttribute("count", pair.Value.Count.ToString()), new XAttribute("material", "Material" + pair.Key),
                                    new XElement(ns + "input", new XAttribute("semantic", "VERTEX"), new XAttribute("offset", offset.ToString()), new XAttribute("source", "#Mesh" + meshID + "-VERTEX")));
                                offset++;

                                List<List<string>> pInputs = new List<List<string>>(); // Will be interleaved.

                                // VERTEX input
                                List<string> vertexIndices = new List<string>();
                                foreach (int faceIndex in pair.Value)
                                {
                                    XObjectStructure face = (XObjectStructure)obj["faces"].Values[faceIndex];

                                    foreach (int vertexIndex in face["faceVertexIndices"].Values)
                                    {
                                        vertexIndices.Add(vertexIndex.ToString());
                                    }
                                }
                                pInputs.Add(vertexIndices);

                                // NORMAL input
                                if (hasNormals)
                                {
                                    triangles.Add(new XElement(ns + "input", new XAttribute("semantic", "NORMAL"), new XAttribute("offset", offset.ToString()), new XAttribute("source", "#Mesh" + meshID + "-NORMAL")));
                                    offset++;

                                    List<string> normalIndices = new List<string>();
                                    foreach (int faceIndex in pair.Value)
                                    {
                                        XObjectStructure face = (XObjectStructure)normalObject["faceNormals"].Values[faceIndex];

                                        foreach (int normalIndex in face["faceVertexIndices"].Values)
                                        {
                                            normalIndices.Add(normalIndex.ToString());
                                        }
                                    }
                                    pInputs.Add(normalIndices);
                                }

                                // TEXCOORD input
                                if (hasTexCoords)
                                {
                                    triangles.Add(new XElement(ns + "input", new XAttribute("semantic", "TEXCOORD"), new XAttribute("offset", offset.ToString()), new XAttribute("set", "0"), new XAttribute("source", "#Mesh" + meshID + "-UV")));
                                    offset++;

                                    // .X texture coordinate indices match position indices.
                                    pInputs.Add(vertexIndices);
                                }

                                // COLOR input
                                if (hasColors)
                                {
                                    triangles.Add(new XElement(ns + "input", new XAttribute("semantic", "COLOR"), new XAttribute("offset", offset.ToString()), new XAttribute("source", "#Mesh" + meshID + "-COLOR")));
                                    offset++;

                                    // We have already arranged the colors to match vertex order, so we can use position indices.
                                    pInputs.Add(vertexIndices);
                                }

                                // Interleave pInputs into the <p> element!
                                XElement p = new XElement(ns + "p");

                                int index = 0;
                                foreach (int faceIndex in pair.Value)
                                {
                                    XObjectStructure face = (XObjectStructure)obj["faces"].Values[faceIndex];

                                    for (int vertexIndex = 0; vertexIndex < face["faceVertexIndices"].Values.Count; vertexIndex++)
                                    {
                                        foreach (List<string> input in pInputs)
                                            p.Add(input[index] + " ");
                                        index++;
                                    }
                                }

                                triangles.Add(p);

                                mesh.Add(triangles);
                            }
                        }

                        //Geometry geometry = new Geometry("mesh_geometry", "mesh_geometry", )
                        library_geometries.Add(geometry);

                        visual_scene.Add(new XElement(ns + "node", new XAttribute("name", "Mesh" + meshID + "Instance"), new XAttribute("id", "Mesh" + meshID + "Instance"), new XAttribute("sid", "Mesh" + meshID + "Instance"),
                            new XElement(ns + "matrix", new XAttribute("sid", "matrix"), "1.0 0.0 0.0 0.0 0.0 1.0 0.0 0.0 0.0 0.0 1.0 0.0 0.0 0.0 0.0 1.0"),
                            new XElement(ns + "instance_geometry", new XAttribute("url", "#Mesh" + meshID + "-GEOMETRY"),
                                new XElement(ns + "bind_material",
                                    bindMaterialCommon))));

                        meshID++;
                    }
                }
            }

            COLLADA.Add(library_images);
            COLLADA.Add(library_materials);
            COLLADA.Add(library_effects);
            COLLADA.Add(library_geometries);
            COLLADA.Add(new XElement(ns + "library_visual_scenes", visual_scene));
            COLLADA.Add(new XElement(ns + "scene",
                new XElement(ns + "instance_visual_scene", new XAttribute("url", "#Scene"))));

            doc.Add(COLLADA);
            doc.Save(filename);

            /*Document doc = new Document();
            // Metadata
            doc.Asset.AuthorTool = "LOMNTool v" + System.Reflection.Assembly.GetEntryAssembly().GetName().Version.ToString();
            doc.Asset.Created = DateTime.Now;
            doc.Asset.Modified = DateTime.Now;
            doc.Asset.UnitName = "meter";
            doc.Asset.UnitSizeInMeters = 1.0f;
            doc.Asset.UpAxis = AssetInfo.CoordinateSystem.Y_UP;

            // VisualScenes, Scenes
            VisualScene scene = new VisualScene("lomntool-export", "LOMNTool Export");
            doc.VisualScenes.VisualScenes.Add(scene.ID, scene);
            doc.Scene = new Document.SceneContainer();
            doc.Scene.Scenes.Add(scene);

            // Meshes
            foreach (XObject obj in file.Objects)
            {
                if (obj.DataType.ID == XToken.TokenID.NAME && obj.DataType.NameData == "Mesh")
                {
                    int vertexCount = (int)obj["nVertices"].Values[0];
                    int faceCount = (int)obj["nFaces"].Values[0];

                    List<float> posList = new List<float>();
                    for (int i = 0; i < vertexCount; i++)
                        posList.Add((float)Convert.ToDouble(obj["vertices"].Values[i]));
                    FloatDataSource positions = new FloatDataSource("mesh_positions", "mesh_positions_array", posList,
                        new DataAccessorParam("X", "float"),
                        new DataAccessorParam("Y", "float"),
                        new DataAccessorParam("Z", "float"));

                    foreach (XChildObject child in obj.Children)
                    {
                        if (child.Object.DataType.NameData == "MeshNormals")
                        {

                        }
                        else if (child.Object.DataType.NameData == "MeshTextureCoords")
                        {

                        }
                        else if (child.Object.DataType.NameData == "MeshVertexColors")
                        {

                        }
                    }

                    Geometry geometry = new Geometry("mesh_geometry", "mesh_geometry", )
                }
            }

            doc.BuildDocument().Save(filename);*/
        }
    }
}
