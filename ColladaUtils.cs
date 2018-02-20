using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using D3DX.Mesh;
using SharpDX;

namespace LOMNTool.Collada
{
    public static class Utils
    {
        public static void ExportCOLLADA(XFile file, string filename, Matrix transform, bool flipV = true, string textureExtension = null)
        {
            Document doc = new Document();
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

                    
                }
            }

            doc.BuildDocument().Save(filename);
        }
    }
}
