using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Xml.Linq;
using System.IO;

namespace LOMNTool.Collada
{
    public struct Float2
    {
        public float X;
        public float Y;

        public Float2(float x, float y)
        {
            X = x;
            Y = x;
        }

        public static Float2 Read(BinaryReader reader)
        {
            return new Float2(reader.ReadSingle(), reader.ReadSingle());
        }

        public void Write(BinaryWriter writer)
        {
            writer.Write(X);
            writer.Write(Y);
        }

        /*public static Float2 Unpack(BinaryReader reader)
        {
            return new Float2(HalfUtils.Unpack(reader.ReadUShort()), HalfUtils.Unpack(reader.ReadUShort()));
        }

        public void Pack(BinaryWriter writer)
        {
            writer.Write(HalfUtils.Pack(X));
            writer.Write(HalfUtils.Pack(Y));
        }*/

        public override string ToString()
        {
            return "<" + X + "," + Y + ">";
        }
    }

    public struct Float3
    {
        public float X;
        public float Y;
        public float Z;

        public Float3(float x, float y, float z)
        {
            X = x;
            Y = y;
            Z = z;
        }

        public static Float3 Read(BinaryReader reader)
        {
            return new Float3(reader.ReadSingle(), reader.ReadSingle(), reader.ReadSingle());
        }

        public void Write(BinaryWriter writer)
        {
            writer.Write(X);
            writer.Write(Y);
            writer.Write(Z);
        }

        /*public static Float3 Unpack(BinaryReader reader)
        {
            return new Float3(HalfUtils.Unpack(reader.ReadUShort()), HalfUtils.Unpack(reader.ReadUShort()), HalfUtils.Unpack(reader.ReadUShort()));
        }

        public void Pack(BinaryWriter writer)
        {
            writer.Write(HalfUtils.Pack(X));
            writer.Write(HalfUtils.Pack(Y));
            writer.Write(HalfUtils.Pack(Z));
        }*/

        public static Float3 Cross(Float3 left, Float3 right)
        {
            return new Float3(left.Y * right.Z - right.Y * left.Z,
                           -1 * (left.X * right.Z - right.X * left.Z),
                           left.X * right.Y - right.X * left.Y);
        }
    }

    public struct Float4
    {
        public float X;
        public float Y;
        public float Z;
        public float W;

        public Float4(float x, float y, float z, float w)
        {
            X = x;
            Y = y;
            Z = z;
            W = w;
        }

        public static Float4 Read(BinaryReader reader)
        {
            return new Float4(reader.ReadSingle(), reader.ReadSingle(), reader.ReadSingle(), reader.ReadSingle());
        }

        public void Write(BinaryWriter writer)
        {
            writer.Write(X);
            writer.Write(Y);
            writer.Write(Z);
            writer.Write(W);
        }

        /*public static Float4 Unpack(BinaryReader reader)
        {
            return new Float4(HalfUtils.Unpack(reader.ReadUShort()), HalfUtils.Unpack(reader.ReadUShort()), HalfUtils.Unpack(reader.ReadUShort()), HalfUtils.Unpack(reader.ReadUShort()));
        }

        public void Pack(BinaryWriter writer)
        {
            writer.Write(HalfUtils.Pack(X));
            writer.Write(HalfUtils.Pack(Y));
            writer.Write(HalfUtils.Pack(Z));
            writer.Write(HalfUtils.Pack(W));
        }*/
    }

    public struct Float4x4
    {
        // This code is super easy to work with using Visual Studio's alt-drag multiline selection.
        public float M00;
        public float M01;
        public float M02;
        public float M03;
        public float M10;
        public float M11;
        public float M12;
        public float M13;
        public float M20;
        public float M21;
        public float M22;
        public float M23;
        public float M30;
        public float M31;
        public float M32;
        public float M33;

        public Float3 Right
        {
            get
            {
                return new Float3(M00, M10, M20);
            }
            set
            {
                M00 = value.X;
                M10 = value.Y;
                M20 = value.Z;
            }
        }
        public Float3 Up
        {
            get
            {
                return new Float3(M01, M11, M21);
            }
            set
            {
                M01 = value.X;
                M11 = value.Y;
                M21 = value.Z;
            }
        }
        public Float3 Forward
        {
            get
            {
                return new Float3(M02, M12, M22);
            }
            set
            {
                M02 = value.X;
                M12 = value.Y;
                M22 = value.Z;
            }
        }
        public Float3 Translation
        {
            get
            {
                return new Float3(M03, M13, M23);
            }
            set
            {
                M03 = value.X;
                M13 = value.Y;
                M23 = value.Z;
            }
        }

        public static readonly Float4x4 Identity = new Float4x4() { M00 = 1.0f, M11 = 1.0f, M22 = 1.0f, M33 = 1.0f };

        public Float4x4(String value)
        {
            String[] raw = value.Split(new char[] { ' ', '\n' }, StringSplitOptions.RemoveEmptyEntries);
            M00 = float.Parse(raw[0 * 4 + 0], System.Globalization.CultureInfo.InvariantCulture);
            M01 = float.Parse(raw[0 * 4 + 1], System.Globalization.CultureInfo.InvariantCulture);
            M02 = float.Parse(raw[0 * 4 + 2], System.Globalization.CultureInfo.InvariantCulture);
            M03 = float.Parse(raw[0 * 4 + 3], System.Globalization.CultureInfo.InvariantCulture);
            M10 = float.Parse(raw[1 * 4 + 0], System.Globalization.CultureInfo.InvariantCulture);
            M11 = float.Parse(raw[1 * 4 + 1], System.Globalization.CultureInfo.InvariantCulture);
            M12 = float.Parse(raw[1 * 4 + 2], System.Globalization.CultureInfo.InvariantCulture);
            M13 = float.Parse(raw[1 * 4 + 3], System.Globalization.CultureInfo.InvariantCulture);
            M20 = float.Parse(raw[2 * 4 + 0], System.Globalization.CultureInfo.InvariantCulture);
            M21 = float.Parse(raw[2 * 4 + 1], System.Globalization.CultureInfo.InvariantCulture);
            M22 = float.Parse(raw[2 * 4 + 2], System.Globalization.CultureInfo.InvariantCulture);
            M23 = float.Parse(raw[2 * 4 + 3], System.Globalization.CultureInfo.InvariantCulture);
            M30 = float.Parse(raw[3 * 4 + 0], System.Globalization.CultureInfo.InvariantCulture);
            M31 = float.Parse(raw[3 * 4 + 1], System.Globalization.CultureInfo.InvariantCulture);
            M32 = float.Parse(raw[3 * 4 + 2], System.Globalization.CultureInfo.InvariantCulture);
            M33 = float.Parse(raw[3 * 4 + 3], System.Globalization.CultureInfo.InvariantCulture);
        }

        public Float4x4(Float3 Right, Float3 Up, Float3 Forward, Float3 Translation)
        {
            M00 = Right.X;
            M01 = Up.X;
            M02 = Forward.X;
            M03 = Translation.X;
            M10 = Right.Y;
            M11 = Up.Y;
            M12 = Forward.Y;
            M13 = Translation.Y;
            M20 = Right.Z;
            M21 = Up.Z;
            M22 = Forward.Z;
            M23 = Translation.Z;
            M30 = 0.0f;
            M31 = 0.0f;
            M32 = 0.0f;
            M33 = 1.0f;
        }

        public override string ToString()
        {
            StringBuilder sb = new StringBuilder();
            sb.Append(M00.ToString(System.Globalization.CultureInfo.InvariantCulture) + " ");
            sb.Append(M01.ToString(System.Globalization.CultureInfo.InvariantCulture) + " ");
            sb.Append(M02.ToString(System.Globalization.CultureInfo.InvariantCulture) + " ");
            sb.Append(M03.ToString(System.Globalization.CultureInfo.InvariantCulture) + " ");
            sb.Append(M10.ToString(System.Globalization.CultureInfo.InvariantCulture) + " ");
            sb.Append(M11.ToString(System.Globalization.CultureInfo.InvariantCulture) + " ");
            sb.Append(M12.ToString(System.Globalization.CultureInfo.InvariantCulture) + " ");
            sb.Append(M13.ToString(System.Globalization.CultureInfo.InvariantCulture) + " ");
            sb.Append(M20.ToString(System.Globalization.CultureInfo.InvariantCulture) + " ");
            sb.Append(M21.ToString(System.Globalization.CultureInfo.InvariantCulture) + " ");
            sb.Append(M22.ToString(System.Globalization.CultureInfo.InvariantCulture) + " ");
            sb.Append(M23.ToString(System.Globalization.CultureInfo.InvariantCulture) + " ");
            sb.Append(M30.ToString(System.Globalization.CultureInfo.InvariantCulture) + " ");
            sb.Append(M31.ToString(System.Globalization.CultureInfo.InvariantCulture) + " ");
            sb.Append(M32.ToString(System.Globalization.CultureInfo.InvariantCulture) + " ");
            sb.Append(M33.ToString(System.Globalization.CultureInfo.InvariantCulture) + " ");
            return sb.ToString();
        }
    }

    public delegate void LogLineWrittenEventHandler(String text);

    public struct Color
    {
        public float R;
        public float G;
        public float B;
        public float A;

        public static readonly Color White = new Color(1.0f, 1.0f, 1.0f, 1.0f);
        public static readonly Color Black = new Color(0.0f, 0.0f, 0.0f, 1.0f);
        public static readonly Color Transparent = new Color(0.0f, 0.0f, 0.0f, 0.0f);

        public Color(float r, float g, float b, float a)
        {
            R = r;
            G = g;
            B = b;
            A = a;
        }

        public Color(XElement e)
        {
            if (e.Name.LocalName.ToString().ToLower() != "color")
                throw new Exception("Tried to load a color from an XElement of name " + e.Name);

            String[] parts = e.Value.Split(new char[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);
            R = float.Parse(parts[0], System.Globalization.CultureInfo.InvariantCulture);
            G = float.Parse(parts[1], System.Globalization.CultureInfo.InvariantCulture);
            B = float.Parse(parts[2], System.Globalization.CultureInfo.InvariantCulture);
            A = float.Parse(parts[3], System.Globalization.CultureInfo.InvariantCulture);
        }

        public XElement BuildElement()
        {
            return new XElement("color", R.ToString(System.Globalization.CultureInfo.InvariantCulture) + " " + G.ToString(System.Globalization.CultureInfo.InvariantCulture) + " " + B.ToString(System.Globalization.CultureInfo.InvariantCulture) + " " + A.ToString(System.Globalization.CultureInfo.InvariantCulture));
        }

        public override string ToString()
        {
            return "rgba(" + R + ", " + G + ", " + B + ", " + A + ")";
        }
    }

    public class DataAccessorParam
    {
        public String Name = "";
        public String TypeName = "";

        public DataAccessorParam(String name, String typename)
        {
            Name = name;
            TypeName = typename;
        }

        public DataAccessorParam(XElement e)
        {
            Name = e.Attribute("name")?.Value ?? "";
            TypeName = e.Attribute("type").Value;
        }

        public XElement BuildElement()
        {
            return new XElement("param", new XAttribute("name", Name), new XAttribute("type", TypeName));
        }
    }

    public class DataAccessor<T>
    {

        public DataSource<T> Source = null;
        public List<DataAccessorParam> Parameters = new List<DataAccessorParam>();
        public int Stride = 0;
        public int Count
        {
            get
            {
                return (Source?.ArrayCount ?? 0) / (Stride == 0 ? 1 : Stride); // If Source == null, treat array count as 0. If Stride == 0, treat stride as 1 to avound divide-by-zero.
            }
        }

        public DataAccessor(DataSource<T> source, params DataAccessorParam[] parameters)
        {
            Source = source;
            Stride = parameters.Length;
            Parameters = new List<DataAccessorParam>(parameters);
        }

        public DataAccessor(DataSource<T> source, XElement e)
        {
            Source = source;
            Stride = int.Parse(e.Attribute("stride")?.Value ?? "1");
            foreach (XElement p in e.Elements())
            {
                Parameters.Add(new DataAccessorParam(p));
            }
        }

        public XElement BuildElement()
        {
            XElement e = new XElement("accessor", new XAttribute("source", "#" + Source?.ArrayID ?? ""), new XAttribute("count", Count), new XAttribute("stride", Stride));
            foreach (DataAccessorParam p in Parameters)
            {
                e.Add(p.BuildElement());
            }
            return e;
        }
    }

    public abstract class DataSource<T>
    {
        public String ID = "";
        public String ArrayID = "";
        public int ArrayCount { get { return Items.Count; } }
        public List<T> Items = new List<T>();
        public DataAccessor<T> Accessor = null;

        public abstract String ArrayElementName { get; }
        public abstract T ReadEntry(String s);
        public abstract String WriteEntry(T e);

        public DataSource()
        {

        }

        public DataSource(String id, String arrayid, IEnumerable<T> items, params DataAccessorParam[] parameters)
        {
            ID = id;
            ArrayID = arrayid;
            Items.AddRange(items);
            Accessor = new DataAccessor<T>(this, parameters);
        }

        public DataSource(XElement e)
        {
            ID = e.Attribute("id").Value;
            // Read array
            XElement ArrayElement = e.Element(Document.Namespace + ArrayElementName + "_array");
            if (ArrayElement == null)
            {
                throw new Exception("Data array of type '" + ArrayElementName + "_array' was not found.");
            }
            ArrayID = ArrayElement.Attribute("id").Value;
            foreach (String s in ArrayElement.Value.Split(new char[] { ' ', '\n' }, StringSplitOptions.RemoveEmptyEntries))
            {
                Items.Add(ReadEntry(s));
            }
            // Read accessor
            Accessor = new DataAccessor<T>(this, e.Element(Document.Namespace + "technique_common").Element(Document.Namespace + "accessor"));
        }

        public XElement BuildElement()
        {
            XElement ArrayElement = new XElement(ArrayElementName + "_array", new XAttribute("id", ArrayID), new XAttribute("count", Items.Count));
            StringBuilder sb = new StringBuilder();
            foreach (T item in Items)
            {
                sb.Append(WriteEntry(item) + " ");
            }
            ArrayElement.Value = sb.ToString();
            return new XElement("source", new XAttribute("id", ID),
                        ArrayElement,
                        new XElement("technique_common",
                            Accessor.BuildElement()));
        }
    }

    public class FloatDataSource : DataSource<float>
    {
        public FloatDataSource() : base()
        {

        }

        public FloatDataSource(String id, String arrayid, IEnumerable<float> items, params DataAccessorParam[] parameters) : base(id, arrayid, items, parameters)
        {

        }

        public FloatDataSource(XElement e) : base(e)
        {

        }

        public override String ArrayElementName { get { return "float"; } }

        public override float ReadEntry(string s)
        {
            return float.Parse(s, System.Globalization.CultureInfo.InvariantCulture);
        }

        public override string WriteEntry(float e)
        {
            return e.ToString(System.Globalization.CultureInfo.InvariantCulture);
        }
    }

    public class NameDataSource : DataSource<String>
    {
        public NameDataSource() : base()
        {

        }

        public NameDataSource(String id, String arrayid, IEnumerable<String> items, params DataAccessorParam[] parameters) : base(id, arrayid, items, parameters)
        {

        }

        public NameDataSource(XElement e) : base(e)
        {

        }

        public override String ArrayElementName { get { return "Name"; } }

        public override string ReadEntry(string s)
        {
            return s;
        }

        public override string WriteEntry(string e)
        {
            return e;
        }
    }

    public class AssetInfo
    {
        public String AuthorName = "";
        public String AuthorTool = "MiniCollada";
        public String Comments = "";
        public String SourceData = "";
        public DateTime Created;
        public DateTime Modified;
        public float UnitSizeInMeters = 1.0f;
        public String UnitName = "meter";
        public enum CoordinateSystem
        {
            X_UP,
            Y_UP,
            Z_UP
        };
        public CoordinateSystem UpAxis = CoordinateSystem.Y_UP;

        public AssetInfo()
        {

        }

        public AssetInfo(XElement e)
        {
            foreach (XElement el in e.Elements())
            {
                if (el.Name.LocalName.ToString() == "contributor")
                {
                    foreach (XElement ele in el.Elements())
                    {
                        if (ele.Name.LocalName.ToString() == "author")
                        {
                            AuthorName = ele.Value;
                        }
                        else if (ele.Name.LocalName.ToString() == "authoring_tool")
                        {
                            AuthorTool = ele.Value;
                        }
                        else if (ele.Name.LocalName.ToString() == "comments")
                        {
                            Comments = ele.Value;
                        }
                        else if (ele.Name.LocalName.ToString() == "source_data")
                        {
                            SourceData = ele.Value;
                        }
                    }
                }
                else if (el.Name.LocalName.ToString() == "created")
                {
                    Created = (DateTime)el;
                }
                else if (el.Name.LocalName.ToString() == "modified")
                {
                    Modified = (DateTime)el;
                }
                else if (el.Name.LocalName.ToString() == "unit")
                {
                    UnitSizeInMeters = float.Parse(el.Attribute("meter").Value, System.Globalization.CultureInfo.InvariantCulture);
                    UnitName = el.Attribute("name").Value;
                }
                else if (el.Name.LocalName.ToString() == "up_axis")
                {
                    UpAxis = (CoordinateSystem)CoordinateSystem.Parse(typeof(CoordinateSystem), el.Value, true);
                }
            }
        }

        public XElement BuildElement()
        {
            return new XElement("asset",
                        new XElement("contributor",
                            new XElement("author", AuthorName),
                            new XElement("authoring_tool", AuthorTool),
                            new XElement("comments", Comments),
                            new XElement("source_data", SourceData)),
                        new XElement("created", Created.ToString()),
                        new XElement("modified", Modified.ToString()),
                        new XElement("unit", new XAttribute("meter", UnitSizeInMeters.ToString(System.Globalization.CultureInfo.InvariantCulture)), new XAttribute("name", UnitName)),
                        new XElement("up_axis", UpAxis.ToString()));
        }
    }

    public class Effect
    {
        public enum ShaderType
        {
            Constant,
            Lambert,
            Phong,
            Blinn
        };

        public enum TransparencyType
        {
            A_ONE,
            A_ZERO,
            RGB_ONE,
            RGB_ZERO,
        };

        public String ID = "neweffect";
        public String Name = "New Effect";
        public ShaderType Shader = ShaderType.Lambert;
        // Constant, Lambert, Phong, and Blinn
        public Color Emission = Color.Black;
        public Color Reflective = Color.Black;
        public float Reflectivity = 0;
        public Color Transparent = Color.Black;
        public float Transparency = 0;
        public TransparencyType TransparencyMode = TransparencyType.A_ONE;
        public float IndexOfRefraction = 1;
        // Lambert, Phong, and Blinn:
        public Color Ambient = Color.Black;
        public Color Diffuse = Color.White;
        // Phong and Blinn:
        public Color Specular = Color.White;
        public float Shininess = 3.0f;

        public Effect()
        {

        }

        public Effect(String id, String name, ShaderType shader)
        {
            ID = id;
            Name = name;
            Shader = shader;
        }

        public Effect(XElement e)
        {
            ID = e?.Attribute("id")?.Value ?? "[minicollada_invalid_id]";
            Name = e?.Attribute("name")?.Value ?? ID;
            XElement mat = e.Element(Document.Namespace + "profile_COMMON").Element(Document.Namespace + "technique").Elements().ElementAt(0);
            Shader = (ShaderType)ShaderType.Parse(typeof(ShaderType), mat.Name.LocalName, true);
            foreach (XElement el in mat.Elements())
            {
                switch (el.Name.LocalName.ToLower())
                {
                    case "emission":
                        Emission = new Color(el.Element(Document.Namespace + "color"));
                        break;
                    case "reflective":
                        Reflective = new Color(el.Element(Document.Namespace + "color"));
                        break;
                    case "reflectivity":
                        Reflectivity = float.Parse(el.Element(Document.Namespace + "float").Value, System.Globalization.CultureInfo.InvariantCulture);
                        break;
                    case "transparent":
                        Transparent = new Color(el.Element(Document.Namespace + "color"));
                        TransparencyMode = (TransparencyType)Enum.Parse(typeof(TransparencyType), el.Attribute("opaque").Value);
                        break;
                    case "transparency":
                        Transparency = float.Parse(el.Element(Document.Namespace + "float").Value, System.Globalization.CultureInfo.InvariantCulture);
                        break;
                    case "index_of_refraction":
                        IndexOfRefraction = float.Parse(el.Element(Document.Namespace + "float").Value, System.Globalization.CultureInfo.InvariantCulture);
                        break;
                    case "ambient":
                        Ambient = new Color(el.Element(Document.Namespace + "color"));
                        break;
                    case "diffuse":
                        Diffuse = new Color(el.Element(Document.Namespace + "color"));
                        break;
                    case "specular":
                        Specular = new Color(el.Element(Document.Namespace + "color"));
                        break;
                    case "shininess":
                        Shininess = float.Parse(el.Element(Document.Namespace + "float").Value, System.Globalization.CultureInfo.InvariantCulture);
                        break;
                }
            }
        }

        public XElement BuildElement()
        {
            XElement ShaderElement = new XElement(Shader.ToString().ToLower(),
                                            new XElement("emission", Emission.BuildElement()),
                                            new XElement("reflective", Reflective.BuildElement()),
                                            new XElement("reflectivity", new XElement("float", Reflectivity.ToString(System.Globalization.CultureInfo.InvariantCulture))),
                                            new XElement("transparent", new XAttribute("opaque", TransparencyMode.ToString()), Transparent.BuildElement()),
                                            new XElement("transparency", new XElement("float", Transparency.ToString(System.Globalization.CultureInfo.InvariantCulture))),
                                            new XElement("index_of_refraction", new XElement("float", IndexOfRefraction.ToString(System.Globalization.CultureInfo.InvariantCulture))));

            if (Shader == ShaderType.Lambert || Shader == ShaderType.Phong || Shader == ShaderType.Blinn)
            {
                ShaderElement.Add(new XElement("ambient", Ambient.BuildElement()));
                ShaderElement.Add(new XElement("diffuse", Diffuse.BuildElement()));
                if (Shader == ShaderType.Phong || Shader == ShaderType.Blinn)
                {
                    ShaderElement.Add(new XElement("specular", Specular.BuildElement()));
                    ShaderElement.Add(new XElement("shininess", new XElement("float", Shininess.ToString(System.Globalization.CultureInfo.InvariantCulture))));
                }
            }

            return new XElement("effect", new XAttribute("id", ID), new XAttribute("name", Name),
                        new XElement("profile_COMMON",
                            new XElement("technique", new XAttribute("sid", "common"),
                                ShaderElement)));
        }
    }

    public class Material
    {
        public String ID = "newmaterial";
        public String Name = "New Material";
        public Effect Effect = null;

        public Material(String ID, String Name, Effect Effect)
        {
            this.ID = ID;
            this.Name = Name;
            this.Effect = Effect;
        }

        public Material(Document.EffectLibrary Effects, XElement e)
        {
            this.ID = e.Attribute("id").Value;
            this.Name = e.Attribute("name").Value;
            this.Effect = Effects.Effects[e.Element(Document.Namespace + "instance_effect").Attribute("url").Value.Substring(1)];
        }

        public XElement BuildElement()
        {
            return new XElement("material", new XAttribute("id", ID), new XAttribute("name", Name),
                        new XElement("instance_effect", new XAttribute("url", "#" + Effect.ID)));
        }
    }

    public class Geometry
    {
        public String ID = "";
        public String Name = "";
        public String MaterialID = "";

        public FloatDataSource Positions = null;
        public FloatDataSource Normals = null;
        public List<FloatDataSource> UVs = new List<FloatDataSource>();
        public List<FloatDataSource> Colors = new List<FloatDataSource>();
        public List<FloatDataSource> Tangents = new List<FloatDataSource>();
        public List<FloatDataSource> Bitangents = new List<FloatDataSource>();

        /// <summary>
        /// The index data for the triangles. 
        /// <para/>
        /// It is stored in the format p1 n1 t1 p2 n2 t2 p3 n3 t3, etc. 
        /// If one of the data sources is <see cref="null"/>, that component is not in this data.
        /// </summary>
        public List<int> IndexData = new List<int>();

        public Geometry()
        {

        }

        public Geometry(String id, String name, String materialid, FloatDataSource positions, FloatDataSource normals = null, IEnumerable<FloatDataSource> uvs = null, IEnumerable<FloatDataSource> colors = null, IEnumerable<FloatDataSource> tangents = null, IEnumerable<FloatDataSource> bitangents = null)
        {
            ID = id;
            Name = name;
            MaterialID = materialid;
            Positions = positions;
            Normals = normals;
            if (uvs != null)
                UVs = new List<FloatDataSource>(uvs);
            if (colors != null)
                Colors = new List<FloatDataSource>(colors);
            if (tangents != null)
                Tangents = new List<FloatDataSource>(tangents);
            if (bitangents != null)
                Bitangents = new List<FloatDataSource>(bitangents);
        }

        public Geometry(XElement e)
        {
            ID = e.Attribute("id").Value;
            Name = e.Attribute("name")?.Value ?? "";
            XElement MeshElement = e.Element(Document.Namespace + "mesh");
            if (MeshElement == null)
                throw new Exception("Geometry is not a mesh.");

            // Read data sources
            Dictionary<String, FloatDataSource> DataSources = new Dictionary<String, FloatDataSource>();
            foreach (XElement s in MeshElement.Elements())
            {
                if (s.Name.LocalName == "source")
                {
                    FloatDataSource fds = new FloatDataSource(s);
                    DataSources.Add(fds.ID, fds);
                }
            }

            // Read <vertices> entry and find the id of the position datasource
            //String vertexid = MeshElement.Element(Document.Namespace + "vertices").Attribute("id").Value;
            foreach (XElement vcomponent in MeshElement.Element(Document.Namespace + "vertices").Elements())
            {
                if (vcomponent.Attribute("semantic").Value.ToLower() == "position")
                {
                    Positions = DataSources[vcomponent.Attribute("source").Value.Substring(1)];
                    break;
                }
            }

            // Read <triangles>
            XElement tr = MeshElement.Element(Document.Namespace + "triangles");
            if (tr == null)
                throw new Exception("Mesh doesn't have any triangles.");
            int voffset = 0;
            String vsource = "";
            int noffset = 0;
            String nsource = "";
            int toffset = 0;
            String tsource = "";
            // TODO: Read multiple TEXCOORD channels
            // TODO: Read multiple COLOR channels
            // TODO: Read multiple TEXTANGENT channels
            // TODO: Read multiple TEXBINORMAL channels
            List<int> offsets = new List<int>(); // For counting the number of unique offsets
            foreach (XElement inputelement in tr.Elements())
            {
                if (inputelement.Name.LocalName == "input")
                {
                    int offset = int.Parse(inputelement.Attribute("offset").Value);
                    if (!offsets.Contains(offset))
                        offsets.Add(offset);
                    String source = inputelement.Attribute("source").Value.Substring(1); // Remove '#'
                    switch (inputelement.Attribute("semantic").Value)
                    {
                        case "VERTEX":
                            voffset = offset;
                            vsource = source;
                            break;
                        case "NORMAL":
                            noffset = offset;
                            nsource = source;
                            Normals = DataSources[source];
                            break;
                        case "TEXCOORD":
                            toffset = offset;
                            tsource = source;
                            UVs.Add(DataSources[source]);
                            break;
                    }
                }
            }
            string[] rawdata = tr.Element(Document.Namespace + "p")?.Value.Split(new char[] { ' ', '\n' }, StringSplitOptions.RemoveEmptyEntries) ?? new string[] { };
            for (int vindex = 0; vindex < rawdata.Length / offsets.Count; vindex++)
            {
                int offset = vindex * offsets.Count;
                if (vsource != "")
                {
                    IndexData.Add(int.Parse(rawdata[offset + voffset]));
                }
                if (nsource != "")
                {
                    IndexData.Add(int.Parse(rawdata[offset + noffset]));
                }
                if (tsource != "")
                {
                    IndexData.Add(int.Parse(rawdata[offset + toffset]));
                }
            }
        }

        public XElement BuildElement()
        {
            XElement MeshElement = new XElement("mesh");
            if (Positions != null)
                MeshElement.Add(Positions.BuildElement());
            if (Normals != null)
                MeshElement.Add(Normals.BuildElement());
            foreach (FloatDataSource fds in UVs)
            {
                MeshElement.Add(fds.BuildElement());
            }
            foreach (FloatDataSource fds in Colors)
            {
                MeshElement.Add(fds.BuildElement());
            }
            foreach (FloatDataSource fds in Tangents)
            {
                MeshElement.Add(fds.BuildElement());
            }
            foreach (FloatDataSource fds in Bitangents)
            {
                MeshElement.Add(fds.BuildElement());
            }
            MeshElement.Add(new XElement("vertices", new XAttribute("id", ID + "-vtx"),
                            new XElement("input", new XAttribute("semantic", "POSITION"), new XAttribute("source", "#" + Positions.ID))));
            int vertexcomponents = 0;
            if (Positions != null) vertexcomponents++;
            if (Normals != null) vertexcomponents++;
            if (UVs != null) vertexcomponents++;
            XElement TrianglesElement = new XElement("triangles", new XAttribute("count", IndexData.Count / vertexcomponents / 3), new XAttribute("material", MaterialID));
            if (Positions != null)
                TrianglesElement.Add(new XElement("input", new XAttribute("semantic", "VERTEX"), new XAttribute("offset", "0"), new XAttribute("source", "#" + ID + "-vtx")));
            if (Normals != null)
                TrianglesElement.Add(new XElement("input", new XAttribute("semantic", "NORMAL"), new XAttribute("offset", "1"), new XAttribute("source", "#" + Normals.ID)));
            for (int i = 0; i < UVs.Count; i++)
            {
                TrianglesElement.Add(new XElement("input", new XAttribute("semantic", "TEXCOORD"), new XAttribute("offset", 2 + i), new XAttribute("set", i), new XAttribute("source", "#" + UVs[i].ID)));
            }
            for (int i = 0; i < Colors.Count; i++)
            {
                TrianglesElement.Add(new XElement("input", new XAttribute("semantic", "COLOR"), new XAttribute("offset", 2 + UVs.Count + i), new XAttribute("set", i), new XAttribute("source", "#" + Colors[i].ID)));
            }
            for (int i = 0; i < Tangents.Count; i++)
            {
                TrianglesElement.Add(new XElement("input", new XAttribute("semantic", "TEXTANGENT"), new XAttribute("offset", 2 + UVs.Count + Colors.Count + i), new XAttribute("set", i), new XAttribute("source", "#" + Tangents[i].ID)));
            }
            for (int i = 0; i < Bitangents.Count; i++)
            {
                TrianglesElement.Add(new XElement("input", new XAttribute("semantic", "TEXBINORMAL"), new XAttribute("offset", 2 + UVs.Count + Colors.Count + Tangents.Count + i), new XAttribute("set", i), new XAttribute("source", "#" + Bitangents[i].ID)));
            }
            XElement p = new XElement("p");
            StringBuilder sb = new StringBuilder();
            foreach (int i in IndexData)
            {
                sb.Append(i + " ");
            }
            p.Value = sb.ToString();
            TrianglesElement.Add(p);
            MeshElement.Add(TrianglesElement);
            return new XElement("geometry", new XAttribute("id", ID), new XAttribute("name", Name),
                        MeshElement);
        }
    }

    public class Controller
    {
        public String ID = "";
        public String Name = "";
        public Geometry SkinSource = null;
        public Float4x4 BindShapeMatrix = Float4x4.Identity;

        public NameDataSource JointNameSource = null;
        public FloatDataSource JointInverseBindMatrixSource = null;
        public FloatDataSource JointWeightSource = null;

        public List<int> InfluenceCounts = new List<int>();
        public List<int> InfluenceJoints = new List<int>();
        public List<int> InfluenceWeights = new List<int>();

        public Controller()
        {

        }

        public Controller(String id, String name, Geometry skinsource, Float4x4 bindshapematrix,
                            NameDataSource jointnamesource, FloatDataSource jointinversebindmatrixsource, FloatDataSource jointweightsource)
        {
            ID = id;
            Name = name;
            SkinSource = skinsource;
            BindShapeMatrix = bindshapematrix;
            JointNameSource = jointnamesource;
            JointInverseBindMatrixSource = jointinversebindmatrixsource;
            JointWeightSource = jointweightsource;
        }

        public Controller(Document.GeometryLibrary geometries, XElement e)
        {
            ID = e.Attribute("id")?.Value ?? "";
            Name = e.Attribute("name")?.Value ?? ID;
            SkinSource = geometries.Geometries[e.Element(Document.Namespace + "skin").Attribute("source").Value.Substring(1)];
            XElement bsmatrix = e.Element(Document.Namespace + "skin").Element(Document.Namespace + "bind_shape_matrix");
            if (bsmatrix != null)
                BindShapeMatrix = new Float4x4(bsmatrix.Value);

            // Read the sources and store them for when we read the input semantics
            Dictionary<String, FloatDataSource> floatsources = new Dictionary<string, FloatDataSource>();
            Dictionary<String, NameDataSource> namesources = new Dictionary<string, NameDataSource>();
            foreach (XElement el in e.Element(Document.Namespace + "skin").Elements())
            {
                if (el.Name.LocalName == "source")
                {
                    if (el.Elements().ElementAt(0).Name.LocalName == "float_array")
                    {
                        FloatDataSource fds = new FloatDataSource(el);
                        floatsources.Add(fds.ID, fds);
                    }
                    else if (el.Elements().ElementAt(0).Name.LocalName == "Name_array")
                    {
                        NameDataSource nds = new NameDataSource(el);
                        namesources.Add(nds.ID, nds);
                    }
                }
            }
            foreach (XElement el in e.Element(Document.Namespace + "skin").Elements())
            {
                if (el.Name.LocalName == "joints")
                {
                    foreach (XElement input in el.Elements())
                    {
                        if (input.Attribute("semantic").Value == "JOINT")
                        {
                            JointNameSource = namesources[input.Attribute("source").Value.Substring(1)];
                        }
                        else if (input.Attribute("semantic").Value == "INV_BIND_MATRIX")
                        {
                            JointInverseBindMatrixSource = floatsources[input.Attribute("source").Value.Substring(1)];
                        }
                    }
                }
                else if (el.Name.LocalName == "vertex_weights")
                {
                    List<int> UniqueOffsets = new List<int>();
                    int JointOffset = -1;
                    int WeightOffset = -1;
                    foreach (XElement ele in el.Elements())
                    {
                        if (ele.Name.LocalName == "input")
                        {
                            int offset = int.Parse(ele.Attribute("offset").Value);
                            if (!UniqueOffsets.Contains(offset))
                                UniqueOffsets.Add(offset);
                            if (ele.Attribute("semantic").Value == "JOINT")
                            {
                                JointNameSource = namesources[ele.Attribute("source").Value.Substring(1)];
                                JointOffset = offset;
                            }
                            else if (ele.Attribute("semantic").Value == "WEIGHT")
                            {
                                JointWeightSource = floatsources[ele.Attribute("source").Value.Substring(1)];
                                WeightOffset = offset;
                            }
                        }
                        else if (ele.Name.LocalName == "vcount")
                        {
                            String[] data = (ele.Value.Split(new char[] { ' ', '\n' }, StringSplitOptions.RemoveEmptyEntries));
                            foreach (String s in data)
                            {
                                InfluenceCounts.Add(int.Parse(s));
                            }
                        }
                        else if (ele.Name.LocalName == "v")
                        {
                            if (JointOffset == -1 || WeightOffset == -1)
                                throw new Exception("<vertex_weights> input elements were not defined before the <v>.");
                            int offset = 0;
                            String[] raw = ele.Value.Split(new char[] { ' ', '\n' }, StringSplitOptions.RemoveEmptyEntries);
                            for (int i = 0; i < int.Parse(el.Attribute("count").Value); i++)
                            {
                                for (int j = 0; j < InfluenceCounts[i]; j++)
                                {
                                    InfluenceJoints.Add(int.Parse(raw[offset + JointOffset]));
                                    InfluenceWeights.Add(int.Parse(raw[offset + WeightOffset]));
                                    offset += UniqueOffsets.Count;
                                }
                            }
                        }
                    }
                }
            }
        }

        public XElement BuildElement()
        {
            XElement skin = new XElement("skin", new XAttribute("source", "#" + SkinSource?.ID ?? ""));
            skin.Add(new XElement("bind_shape_matrix", BindShapeMatrix.ToString()));
            skin.Add(JointNameSource.BuildElement());
            skin.Add(JointInverseBindMatrixSource.BuildElement());
            skin.Add(JointWeightSource.BuildElement());
            skin.Add(new XElement("joints", new XElement("input", new XAttribute("semantic", "JOINT"), new XAttribute("source", "#" + JointNameSource.ID)),
                                            new XElement("input", new XAttribute("semantic", "INV_BIND_MATRIX"), new XAttribute("source", "#" + JointInverseBindMatrixSource.ID))));
            XElement v = new XElement("v");
            int influence = 0;
            foreach (int numinfluences in InfluenceCounts)
            {
                for (int i = 0; i < numinfluences; i++)
                {
                    v.Value += InfluenceJoints[influence] + " " + InfluenceWeights[influence] + " ";
                    influence++;
                }
            }
            skin.Add(new XElement("vertex_weights", new XAttribute("count", InfluenceCounts.Count),
                            new XElement("input", new XAttribute("semantic", "JOINT"), new XAttribute("offset", "0"), new XAttribute("source", "#" + JointNameSource.ID)),
                            new XElement("input", new XAttribute("semantic", "WEIGHT"), new XAttribute("offset", "1"), new XAttribute("source", "#" + JointWeightSource.ID)),
                            new XElement("vcount", String.Join(" ", InfluenceCounts)),
                            v));
            return new XElement("controller", new XAttribute("id", ID), new XAttribute("name", Name), skin);
        }
    }

    public class Node
    {
        public class Instance
        {
            public enum InstanceType
            {
                Geometry,
                Controller
            };
            public InstanceType Type = InstanceType.Geometry;
            public String URL = "";
            public Material Material = null;

            public Instance()
            {

            }

            public Instance(InstanceType type, String url, Material material)
            {
                Type = type;
                URL = url;
                Material = material;
            }

            public Instance(Geometry geometry, Material material) : this(InstanceType.Geometry, geometry.ID, material)
            {

            }

            public Instance(Controller controller, Material material) : this(InstanceType.Controller, controller.ID, material)
            {

            }

            public Instance(Document.MaterialLibrary materials, XElement e)
            {
                URL = e.Attribute("url").Value.Substring(1);
                String MaterialTarget = e.Element(Document.Namespace + "bind_material")?.Element(Document.Namespace + "technique_common")?.Element(Document.Namespace + "instance_material")?.Attribute("target")?.Value.Substring(1) ?? "";
                if (MaterialTarget != "")
                    Material = materials.Materials[MaterialTarget];
                switch (e.Name.LocalName)
                {
                    case "instance_geometry":
                        Type = InstanceType.Geometry;
                        break;
                    case "instance_controller":
                        Type = InstanceType.Controller;
                        break;
                    default:
                        throw new Exception("Instance type '" + e.Name.LocalName + "' not supported in nodes.");
                }
            }

            public XElement BuildElement()
            {
                XElement e = new XElement("instance_" + Type.ToString().ToLower(), new XAttribute("url", "#" + URL));
                if (Material != null)
                    e.Add(new XElement("bind_material",
                                new XElement("technique_common",
                                    new XElement("instance_material", new XAttribute("symbol", Material.ID), new XAttribute("target", "#" + Material.ID)))));
                return e;
            }
        }

        public String ID;
        public String Name;
        public String SID;
        public enum NodeType
        {
            Node,
            Joint
        };
        public NodeType Type = NodeType.Node;
        public List<Instance> Instances = new List<Instance>();
        public List<Node> Nodes = new List<Node>();

        public Float4x4 Matrix = Float4x4.Identity;

        public Node()
        {

        }

        public Node(String id, String name, String sid, NodeType type)
        {
            ID = id;
            Name = name;
            SID = sid;
            Type = type;
        }

        public Node(Document.MaterialLibrary materials, XElement e)
        {
            ID = e.Attribute("id")?.Value ?? "";
            Name = e.Attribute("name")?.Value ?? "";
            SID = e.Attribute("sid")?.Value ?? "";
            if (ID == "") ID = SID;
            if (Name == "") Name = ID;
            Type = (NodeType)Enum.Parse(typeof(NodeType), e.Attribute("type")?.Value ?? "NODE", true);
            Matrix = e.Element(Document.Namespace + "matrix") != null ? new Float4x4(e.Element(Document.Namespace + "matrix").Value) : Float4x4.Identity;
            // TODO: Also read <rotate> and <translate> and <scale> elements and apply them to Matrix
            foreach (XElement el in e.Elements())
            {
                if (el.Name.LocalName.StartsWith("instance_"))
                {
                    try
                    {
                        Instances.Add(new Instance(materials, el));
                    }
                    catch (Exception ex)
                    {
                        System.Diagnostics.Debug.WriteLine("Exception reading instance. Details: \n" + ex.ToString());
                    }
                }
                else if (el.Name.LocalName == "node")
                {
                    Nodes.Add(new Node(materials, el));
                }
            }
        }

        public XElement BuildElement()
        {
            XElement e = new XElement("node", new XAttribute("id", ID), new XAttribute("name", Name), new XAttribute("sid", SID), new XAttribute("type", Type.ToString().ToUpper()));
            e.Add(new XElement("matrix", new XAttribute("sid", "matrix"), Matrix.ToString()));
            foreach (Instance i in Instances)
            {
                e.Add(i.BuildElement());
            }
            foreach (Node n in Nodes)
            {
                e.Add(n.BuildElement());
            }
            return e;
        }
    }

    public class VisualScene
    {
        public String ID = "";
        public String Name = "";
        public List<Node> Nodes = new List<Node>();

        public VisualScene()
        {

        }

        public VisualScene(String id, String name)
        {
            ID = id;
            Name = name;
        }

        public VisualScene(Document.MaterialLibrary materials, XElement e)
        {
            ID = e.Attribute("id").Value;
            Name = e.Attribute("name")?.Value ?? ID;
            foreach (XElement el in e.Elements())
            {
                if (el.Name.LocalName == "node")
                {
                    Nodes.Add(new Node(materials, el));
                }
            }
        }

        public XElement BuildElement()
        {
            XElement e = new XElement("visual_scene", new XAttribute("id", ID), new XAttribute("name", Name));
            foreach (Node n in Nodes)
            {
                e.Add(n.BuildElement());
            }
            return e;
        }
    }

    public class Document
    {
        public static XNamespace Namespace = XNamespace.Get("http://www.collada.org/2005/11/COLLADASchema");

        public static event LogLineWrittenEventHandler LogLineWritten;

        public class EffectLibrary
        {
            public Dictionary<String, Effect> Effects = new Dictionary<string, Effect>();

            public EffectLibrary()
            {

            }

            public EffectLibrary(IEnumerable<Effect> Effects)
            {
                foreach (Effect e in Effects)
                {
                    this.Effects.Add(e.ID, e);
                }
            }

            public EffectLibrary(XElement e)
            {
                foreach (XElement child in e.Elements())
                {
                    Effect ef = new Effect(child);
                    Log("      Read effect '" + ef.Name + "'.");
                    Effects.Add(ef.ID, ef);
                }
            }

            public XElement BuildElement()
            {
                XElement e = new XElement("library_effects");
                foreach (Effect ef in Effects.Values)
                {
                    Log("      Writing effect '" + ef.Name + "'.");
                    e.Add(ef.BuildElement());
                }
                return e;
            }
        }

        public class MaterialLibrary
        {
            public Dictionary<String, Material> Materials = new Dictionary<String, Material>();

            public MaterialLibrary()
            {

            }

            public MaterialLibrary(IEnumerable<Material> Materials)
            {
                foreach (Material m in Materials)
                {
                    this.Materials.Add(m.ID, m);
                }
            }

            public MaterialLibrary(EffectLibrary effects, XElement e)
            {
                foreach (XElement child in e.Elements())
                {
                    Material m = new Material(effects, child);
                    Materials.Add(m.ID, m);
                }
            }

            public XElement BuildElement()
            {
                XElement e = new XElement("library_materials");
                foreach (Material m in Materials.Values)
                {
                    e.Add(m.BuildElement());
                }
                return e;
            }
        }

        public class GeometryLibrary
        {
            public Dictionary<String, Geometry> Geometries = new Dictionary<string, Geometry>();

            public GeometryLibrary()
            {

            }

            public GeometryLibrary(IEnumerable<Geometry> geometries)
            {
                foreach (Geometry g in geometries)
                {
                    Geometries.Add(g.ID, g);
                }
            }

            public GeometryLibrary(XElement e)
            {
                foreach (XElement ge in e.Elements())
                {
                    if (ge.Name.LocalName == "geometry")
                    {
                        try
                        {
                            Geometry g = new Geometry(ge);
                            Log("      Read geometry '" + g.Name + "'.");
                            Geometries.Add(g.ID, g);
                        }
                        catch (Exception ex)
                        {
                            System.Diagnostics.Debug.WriteLine("Exception reading geometry entry. Details: \n" + ex.ToString());
                        }
                    }
                }
            }

            public XElement BuildElement()
            {
                XElement e = new XElement("library_geometries");
                foreach (Geometry g in Geometries.Values)
                {
                    Log("      Writing geometry '" + g.Name + "'.");
                    e.Add(g.BuildElement());
                }
                return e;
            }
        }

        public class ControllerLibrary
        {
            public Dictionary<String, Controller> Controllers = new Dictionary<string, Controller>();

            public ControllerLibrary()
            {

            }

            public ControllerLibrary(IEnumerable<Controller> controllers)
            {
                foreach (Controller c in controllers)
                {
                    Controllers.Add(c.ID, c);
                }
            }

            public ControllerLibrary(GeometryLibrary geometries, XElement e)
            {
                foreach (XElement el in e.Elements())
                {
                    if (el.Name.LocalName == "controller")
                    {
                        Controller c = new Controller(geometries, el);
                        Log("      Read controller '" + c.Name + "'.");
                        Controllers.Add(c.ID, c);
                    }
                }
            }

            public XElement BuildElement()
            {
                XElement e = new XElement("library_controllers");
                foreach (Controller c in Controllers.Values)
                {
                    Log("      Writing controller '" + c.Name + "'.");
                    e.Add(c.BuildElement());
                }
                return e;
            }
        }

        public class VisualSceneLibrary
        {
            public Dictionary<String, VisualScene> VisualScenes = new Dictionary<string, VisualScene>();

            public VisualSceneLibrary()
            {

            }

            public VisualSceneLibrary(IEnumerable<VisualScene> visualscenes)
            {
                foreach (VisualScene vs in visualscenes)
                {
                    VisualScenes.Add(vs.ID, vs);
                }
            }

            public VisualSceneLibrary(MaterialLibrary materials, XElement e)
            {
                foreach (XElement el in e.Elements())
                {
                    if (el.Name.LocalName == "visual_scene")
                    {
                        VisualScene vs = new VisualScene(materials, el);
                        VisualScenes.Add(vs.ID, vs);
                    }
                }
            }

            public XElement BuildElement()
            {
                XElement e = new XElement("library_visual_scenes");
                foreach (VisualScene vs in VisualScenes.Values)
                {
                    e.Add(vs.BuildElement());
                }
                return e;
            }
        }

        public class SceneContainer
        {
            public List<VisualScene> Scenes = new List<VisualScene>();

            public SceneContainer()
            {

            }

            public SceneContainer(IEnumerable<VisualScene> Scenes)
            {
                this.Scenes = new List<VisualScene>(Scenes);
            }

            public SceneContainer(VisualSceneLibrary VisualScenes, XElement e)
            {
                foreach (XElement el in e.Elements())
                {
                    if (el.Name.LocalName.ToString().ToLower() == "instance_visual_scene")
                    {
                        Scenes.Add(VisualScenes.VisualScenes[el.Attribute("url").Value.Substring(1)]);
                    }
                }
            }

            public XElement BuildElement()
            {
                XElement e = new XElement("scene");
                foreach (VisualScene scene in Scenes)
                {
                    e.Add(new XElement("instance_visual_scene", new XAttribute("url", "#" + scene.ID)));
                }
                return e;
            }
        }

        public AssetInfo Asset = new AssetInfo();
        public EffectLibrary Effects = new EffectLibrary();
        public MaterialLibrary Materials = new MaterialLibrary();
        public GeometryLibrary Geometries = new GeometryLibrary();
        public ControllerLibrary Controllers = new ControllerLibrary();
        public VisualSceneLibrary VisualScenes = new VisualSceneLibrary();
        public SceneContainer Scene = new SceneContainer();

        public Document()
        {

        }

        // Parses all of the elements of the given maintype (e.g. "library_effects" or "library_materials").
        // This facilitates loading certain maintypes before others, which is needed for how we deserialize references.
        private void ParseAll(XElement container, String type)
        {
            foreach (XElement child in container.Elements())
            {
                if (child.Name.LocalName.ToString().ToLower() == type.ToLower())
                {
                    switch (type.ToLower())
                    {
                        case "library_effects":
                            EffectLibrary e = new EffectLibrary(child);
                            foreach (Effect effect in e.Effects.Values)
                            {
                                Effects.Effects.Add(effect.ID, effect);
                            }
                            break;
                        case "library_materials":
                            MaterialLibrary m = new MaterialLibrary(Effects, child);
                            foreach (Material material in m.Materials.Values)
                            {
                                Materials.Materials.Add(material.ID, material);
                            }
                            break;
                        case "library_geometries":
                            GeometryLibrary g = new GeometryLibrary(child);
                            foreach (Geometry geo in g.Geometries.Values)
                            {
                                Geometries.Geometries.Add(geo.ID, geo);
                            }
                            break;
                        case "library_controllers":
                            ControllerLibrary c = new ControllerLibrary(Geometries, child);
                            foreach (Controller con in c.Controllers.Values)
                            {
                                Controllers.Controllers.Add(con.ID, con);
                            }
                            break;
                        case "library_visual_scenes":
                            VisualSceneLibrary vs = new VisualSceneLibrary(Materials, child);
                            foreach (VisualScene vslib in vs.VisualScenes.Values)
                            {
                                VisualScenes.VisualScenes.Add(vslib.ID, vslib);
                            }
                            break;
                        case "scene":
                            Scene = new SceneContainer(VisualScenes, child); // There is only ever one according to spec.
                            break;
                    }
                }
            }
        }

        public Document(XDocument doc)
        {
            // Force the namespace to be accurated
            Log("Loading COLLADA document...");
            foreach (XElement e in doc.Descendants())
            {
                e.Name = XName.Get(e.Name.LocalName, Namespace.NamespaceName);
            }
            XElement RootElement = doc.Root;
            Log("   Loading asset information...");
            Asset = new AssetInfo(RootElement.Element(Namespace + "asset"));
            Log("   Loading effect library...");
            ParseAll(RootElement, "library_effects");
            Log("   Loading material library...");
            ParseAll(RootElement, "library_materials");
            Log("   Loading geometry library...");
            ParseAll(RootElement, "library_geometries");
            Log("   Loading controller library...");
            ParseAll(RootElement, "library_controllers");
            Log("   Loading visual scene library...");
            ParseAll(RootElement, "library_visual_scenes");
            ParseAll(RootElement, "scene");
            Log("Load complete.");
        }

        public Document(String text) : this(XDocument.Parse(text))
        {

        }

        public XDocument BuildDocument()
        {
            Log("Building COLLADA document...");
            XDocument result = new XDocument(new XElement(Namespace + "COLLADA", new XAttribute("xmlns", "http://www.collada.org/2005/11/COLLADASchema"), new XAttribute("version", "1.4.1")));
            Log("   Building asset information...");
            result.Root.Add(Asset.BuildElement());
            Log("   Building effect library...");
            result.Root.Add(Effects.BuildElement());
            Log("   Building material library...");
            result.Root.Add(Materials.BuildElement());
            Log("   Building geometry library...");
            result.Root.Add(Geometries.BuildElement());
            Log("   Building controller library...");
            result.Root.Add(Controllers.BuildElement());
            Log("   Building visual scene library...");
            result.Root.Add(VisualScenes.BuildElement());
            result.Root.Add(Scene.BuildElement());
            foreach (XElement e in result.Descendants())
            {
                e.Name = XName.Get(e.Name.LocalName, Namespace.NamespaceName);
            }
            Log("Build complete.");
            return result;
        }

        private static void Log(String line)
        {
            LogLineWritten?.Invoke(line);
        }
    }
}
