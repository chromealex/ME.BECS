#if FIXED_POINT
using tfloat = sfloat;
using ME.BECS.FixedPoint;
using Bounds = ME.BECS.FixedPoint.AABB;
using Rect = ME.BECS.FixedPoint.Rect;
#else
using tfloat = System.Single;
using Unity.Mathematics;
using Bounds = UnityEngine.Bounds;
using Rect = UnityEngine.Rect;
#endif

namespace ME.BECS.Editor {
    
    using System.Globalization;

    public static class FpCodeGenerator {

        public struct Item {

            public string name;
            public string type;
            public string type_long;
            public string comment;
            public string min;
            public string max;
            public string precision;
            public string precision_sqrt;
            public string one_name;

            public string drawer_variants;
            public string editor_field;

            public string CUSTOM_METHODS;
            public string CUSTOM_MATH_METHODS;

        }
        
        public static void Generate() {

            var items = new Item[] {
                new Item() {
                    name = "uangle",
                    type = "uint",
                    type_long = "ulong",
                    comment = "100 parts in 1 degree",
                    min = "0u",
                    max = "360u * 100u",
                    precision = "100u",
                    precision_sqrt = $"{(uint)(math.sqrt(100u) * 1000u)}u",
                    one_name = "Degree",
                    drawer_variants = @"""deg"", ""original""",
                    editor_field = "UnsignedIntegerField",
                    CUSTOM_METHODS = "",
                    CUSTOM_MATH_METHODS = @"

public const double TORADIANS_DBL = 0.017453292519943296;

        /// <summary>Returns the result of converting a float value from degrees to radians.</summary>
        /// <param name=""x"">Angle in degrees.</param>
        /// <returns>Angle converted to radians.</returns>
        [INLINE(256)]
        public static uangle radians(uangle x) { return x * (float)TORADIANS_DBL; }
",
                },
                new Item() {
                    name = "usec",
                    type = "uint",
                    type_long = "ulong",
                    comment = "1 second = 1000 ms, we do not care about values < 1 ms",
                    min = "0u",
                    max = "uint.MaxValue",
                    precision = "1000u",
                    precision_sqrt = $"{(uint)(math.sqrt(1000u) * 1000u)}u",
                    one_name = "Second",
                    drawer_variants = @"""sec"", ""ms""",
                    editor_field = "UnsignedIntegerField",
                },
                new Item() {
                    name = "umeter",
                    type = "uint",
                    type_long = "ulong",
                    comment = "1000 values in 1 meter",
                    min = "0u",
                    max = "uint.MaxValue",
                    precision = "1000u",
                    precision_sqrt = $"{(uint)(math.sqrt(1000u) * 1000u)}u",
                    one_name = "Meter",
                    drawer_variants = @"""meters"", ""mmeters""",
                    editor_field = "UnsignedIntegerField",
                },
                new Item() {
                    name = "meter",
                    type = "int",
                    type_long = "long",
                    comment = "1000 values in 1 meter",
                    min = "0",
                    max = "int.MaxValue",
                    precision = "1000",
                    precision_sqrt = $"{(uint)(math.sqrt(1000u) * 1000u)}",
                    one_name = "Meter",
                    drawer_variants = @"""meters"", ""mmeters""",
                    editor_field = "IntegerField",
                },
                new Item() {
                    name = "ucvalue",
                    type = "uint",
                    type_long = "ulong",
                    comment = "1000 values in 0..1",
                    min = "0u",
                    max = "1u * 1000u",
                    precision = "1000u",
                    precision_sqrt = $"{(uint)(math.sqrt(1000u) * 1000u)}u",
                    one_name = "",
                    drawer_variants = @"""0..1"", ""0..1000""",
                    editor_field = "UnsignedIntegerField",
                },
                new Item() {
                    name = "uvalue",
                    type = "uint",
                    type_long = "ulong",
                    comment = "1000 values in 0..1",
                    min = "0u",
                    max = "uint.MaxValue",
                    precision = "1000u",
                    precision_sqrt = $"{(uint)(math.sqrt(1000u) * 1000u)}u",
                    one_name = "",
                    drawer_variants = @"""value"", ""original""",
                    editor_field = "UnsignedIntegerField",
                },
                new Item() {
                    name = "svalue",
                    type = "int",
                    type_long = "long",
                    comment = "1000 values in 0..1",
                    min = "0",
                    max = "int.MaxValue",
                    precision = "1000",
                    precision_sqrt = $"{(uint)(math.sqrt(1000u) * 1000u)}",
                    one_name = "",
                    drawer_variants = @"""value"", ""original""",
                    editor_field = "IntegerField",
                },
                new Item() {
                    name = "uspeed",
                    type = "uint",
                    type_long = "ulong",
                    comment = "100 values",
                    min = "0u",
                    max = "1u * 100u",
                    precision = "100u",
                    precision_sqrt = $"{(uint)(math.sqrt(100u) * 100u)}u",
                    one_name = "",
                    drawer_variants = @"""m\u200A\u2215\u200Bs"", ""original""",
                    editor_field = "UnsignedIntegerField",
                },
            };
            
            var templates = UnityEditor.AssetDatabase.FindAssets("t:TextAsset .FpTpl");
            foreach (var guid in templates) {
                var path = UnityEditor.AssetDatabase.GUIDToAssetPath(guid);
                var fileName = System.IO.Path.GetFileName(path);
                var srcDir = System.IO.Path.GetDirectoryName(path);
                var text = System.IO.File.ReadAllText(path);
                foreach (var item in items) {
                    var dir = $"{srcDir}/{item.name}";
                    if (System.IO.Directory.Exists(dir) == false) {
                        System.IO.Directory.CreateDirectory(dir);
                    }
                    var tpl = new Tpl(text);
                    var filePath = $"{dir}/{fileName.Replace(".FpTpl.txt", string.Empty)}.{item.name}.gen.cs";
                    var keys = new System.Collections.Generic.Dictionary<string, int>();
                    var variables = new System.Collections.Generic.Dictionary<string, string>();
                    variables.Add(nameof(item.name), item.name);
                    variables.Add(nameof(item.type), item.type);
                    variables.Add(nameof(item.type_long), item.type_long);
                    variables.Add(nameof(item.comment), item.comment);
                    variables.Add(nameof(item.min), item.min);
                    variables.Add(nameof(item.max), item.max);
                    variables.Add(nameof(item.precision), item.precision);
                    variables.Add(nameof(item.precision_sqrt), item.precision_sqrt);
                    variables.Add(nameof(item.one_name), item.one_name);
                    variables.Add(nameof(item.drawer_variants), item.drawer_variants);
                    variables.Add(nameof(item.editor_field), item.editor_field);
                    variables.Add(nameof(item.CUSTOM_METHODS), item.CUSTOM_METHODS);
                    variables.Add(nameof(item.CUSTOM_MATH_METHODS), item.CUSTOM_MATH_METHODS);
                    System.IO.File.WriteAllText(filePath, tpl.GetString(keys, variables));
                    UnityEditor.AssetDatabase.ImportAsset(filePath);
                }
            }

        }

    }

}