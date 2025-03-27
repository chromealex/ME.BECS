using System.Reflection;

namespace ME.BECS.Editor.JSON {

    public abstract class ObjectReferenceSerializer<T, TValue> : SerializerBase<TValue> where T : UnityEngine.Object where TValue : unmanaged {

        public override object FromString(System.Type fieldType, string value) {
            var str = value;
            var protocol = $"{this.ProtocolPrefix}://";
            string customData = null;
            if (str.Contains('#') == true) {
                var splitted = str.Split('#', System.StringSplitOptions.RemoveEmptyEntries);
                str = splitted[0];
                customData = splitted[1];
            }

            if (str.StartsWith(protocol) == true) {
                var configObj = EditorUtils.GetAssetByPathPart<T>(str.Substring(protocol.Length));
                ObjectReferenceRegistry.data.Add(configObj, out var isNew);
                if (isNew == true) ObjectReferenceValidate.Validate(ObjectReferenceRegistry.data.items.Length - 1, 1);
                return this.Deserialize(ObjectReferenceRegistry.GetId(configObj), configObj, customData);
            } else {
                return null;
            }
        }

        public override void Serialize(System.Text.StringBuilder builder, object obj, UnityEditor.SerializedProperty property) {
            var customData = string.Empty;
            var entityConfig = ObjectReferenceRegistry.GetObjectBySourceId<T>(this.GetId((TValue)obj, ref customData));
            if (entityConfig == null) {
                builder.Append('"');
                builder.Append(this.ProtocolPrefix);
                builder.Append("://");
                builder.Append("null");
                if (string.IsNullOrEmpty(customData) == false) {
                    builder.Append('#');
                    builder.Append(customData);
                }
                builder.Append('"');
            } else {
                builder.Append('"');
                builder.Append(this.ProtocolPrefix);
                builder.Append("://");
                builder.Append(UnityEditor.AssetDatabase.GetAssetPath(entityConfig).Substring("Assets/".Length));
                if (string.IsNullOrEmpty(customData) == false) {
                    builder.Append('#');
                    builder.Append(customData);
                }
                builder.Append('"');
            }
        }

        public override void Deserialize(object obj, UnityEditor.SerializedProperty property) {
            property.boxedValue = this.FromString(obj.GetType(), (string)obj);
        }

        public abstract string ProtocolPrefix { get; }
        public abstract uint GetId(TValue obj, ref string customData);
        public abstract TValue Deserialize(uint objectId, T obj, string customData);

    }

    public abstract class PrimitiveSerializer<T> : SerializerBase<T> where T : System.IConvertible {
        public override void Serialize(System.Text.StringBuilder builder, object obj, UnityEditor.SerializedProperty property) {
            var val = (T)obj;
            builder.Append(this.ToString(val));
        }
        public override void Deserialize(object obj, UnityEditor.SerializedProperty property) {
            property.boxedValue = obj;
        }
        public virtual string ToString(T val) {
            return val.ToString(System.Globalization.CultureInfo.InvariantCulture);
        }
    }

    public class EnumSerializer : SerializerBase<System.Enum> {
        
        public override bool IsValid(System.Type type) => type.IsEnum;

        public override object FromString(System.Type fieldType, string value) {
            if (fieldType.GetCustomAttribute(typeof(System.FlagsAttribute)) != null) {
                // bitmask
                var splitter = ' ';
                if (value.Contains("|") == true) splitter = '|';
                if (value.Contains(",") == true) splitter = ',';
                var values = value.Split(splitter, System.StringSplitOptions.RemoveEmptyEntries);
                var mask = 0;
                foreach (var item in values) {
                    var e = (int)System.Enum.Parse(fieldType, item.Trim());
                    mask |= e;
                }
                return mask;
            }
            return System.Enum.Parse(fieldType, value);
        }

        public override void Serialize(System.Text.StringBuilder builder, object obj, UnityEditor.SerializedProperty property) {
            builder.Append('"');
            builder.Append(obj.ToString());
            builder.Append('"');
        }

        public override void Deserialize(object obj, UnityEditor.SerializedProperty property) {
            property.boxedValue = this.FromString(obj.GetType(), (string)obj);
        }
    }

    public class FloatSerializer : PrimitiveSerializer<float> {
        public override object FromString(System.Type fieldType, string value) => float.Parse(value, System.Globalization.CultureInfo.InvariantCulture);
    }

    public class DoubleSerializer : PrimitiveSerializer<double> {
        public override object FromString(System.Type fieldType, string value) => double.Parse(value);
    }

    public class DecimalSerializer : PrimitiveSerializer<decimal> {
        public override object FromString(System.Type fieldType, string value) => decimal.Parse(value);
    }

    public class IntSerializer : PrimitiveSerializer<int> {
        public override object FromString(System.Type fieldType, string value) => int.Parse(value);
    }

    public class UIntSerializer : PrimitiveSerializer<uint> {
        public override object FromString(System.Type fieldType, string value) => uint.Parse(value);
    }

    public class ShortSerializer : PrimitiveSerializer<short> {
        public override object FromString(System.Type fieldType, string value) => short.Parse(value);
    }

    public class UShortSerializer : PrimitiveSerializer<ushort> {
        public override object FromString(System.Type fieldType, string value) => ushort.Parse(value);
    }

    public class SByteSerializer : PrimitiveSerializer<sbyte> {
        public override object FromString(System.Type fieldType, string value) => sbyte.Parse(value);
    }

    public class ByteSerializer : PrimitiveSerializer<byte> {
        public override object FromString(System.Type fieldType, string value) => byte.Parse(value);
    }

    public class BoolSerializer : PrimitiveSerializer<bool> {
        public override object FromString(System.Type fieldType, string value) => bool.Parse(value.ToLower());
        public override string ToString(bool obj) {
            return obj.ToString().ToLower();
        }

    }

    public class Float2Serializer : SerializerBase<Unity.Mathematics.float2> {

        public override object FromString(System.Type fieldType, string value) {
            var splitted = (value).Split(',', System.StringSplitOptions.RemoveEmptyEntries);
            float.TryParse(splitted[0], System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out float x);
            float.TryParse(splitted[1], System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out float y);
            return new Unity.Mathematics.float2(x, y);
        }
        
        public override void Serialize(System.Text.StringBuilder builder, object obj, UnityEditor.SerializedProperty property) {
            var val = (Unity.Mathematics.float2)obj;
            builder.Append('"');
            builder.Append(val.x.ToString(System.Globalization.CultureInfo.InvariantCulture));
            builder.Append(", ");
            builder.Append(val.y.ToString(System.Globalization.CultureInfo.InvariantCulture));
            builder.Append('"');
        }

        public override void Deserialize(object obj, UnityEditor.SerializedProperty property) {
            property.boxedValue = this.FromString(obj.GetType(), (string)obj);
        }

    }

    public class Float3Serializer : SerializerBase<Unity.Mathematics.float3> {
        
        public override object FromString(System.Type fieldType, string value) {
            var splitted = ((string)value).Split(',', System.StringSplitOptions.RemoveEmptyEntries);
            float.TryParse(splitted[0], System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out float x);
            float.TryParse(splitted[1], System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out float y);
            float.TryParse(splitted[2], System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out float z);
            return new Unity.Mathematics.float3(x, y, z);
        }

        public override void Serialize(System.Text.StringBuilder builder, object obj, UnityEditor.SerializedProperty property) {
            var val = (Unity.Mathematics.float3)obj;
            builder.Append('"');
            builder.Append(val.x.ToString(System.Globalization.CultureInfo.InvariantCulture));
            builder.Append(", ");
            builder.Append(val.y.ToString(System.Globalization.CultureInfo.InvariantCulture));
            builder.Append(", ");
            builder.Append(val.z.ToString(System.Globalization.CultureInfo.InvariantCulture));
            builder.Append('"');
        }

        public override void Deserialize(object obj, UnityEditor.SerializedProperty property) {
            property.boxedValue = this.FromString(obj.GetType(), (string)obj);
        }

    }

    public class Float4Serializer : SerializerBase<Unity.Mathematics.float4> {
        
        public override object FromString(System.Type fieldType, string value) {
            var splitted = (value).Split(',', System.StringSplitOptions.RemoveEmptyEntries);
            float.TryParse(splitted[0], System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out float x);
            float.TryParse(splitted[1], System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out float y);
            float.TryParse(splitted[2], System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out float z);
            float.TryParse(splitted[3], System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out float w);
            return new Unity.Mathematics.float4(x, y, z, w);
        }

        public override void Serialize(System.Text.StringBuilder builder, object obj, UnityEditor.SerializedProperty property) {
            var val = (Unity.Mathematics.float4)obj;
            builder.Append('"');
            builder.Append(val.x.ToString(System.Globalization.CultureInfo.InvariantCulture));
            builder.Append(", ");
            builder.Append(val.y.ToString(System.Globalization.CultureInfo.InvariantCulture));
            builder.Append(", ");
            builder.Append(val.z.ToString(System.Globalization.CultureInfo.InvariantCulture));
            builder.Append(", ");
            builder.Append(val.w.ToString(System.Globalization.CultureInfo.InvariantCulture));
            builder.Append('"');
        }

        public override void Deserialize(object obj, UnityEditor.SerializedProperty property) {
            property.boxedValue = this.FromString(obj.GetType(), (string)obj);
        }

    }

    public class QuaternionSerializer : SerializerBase<Unity.Mathematics.quaternion> {
        
        public override object FromString(System.Type fieldType, string value) {
            var splitted = ((string)value).Split(',', System.StringSplitOptions.RemoveEmptyEntries);
            float.TryParse(splitted[0], System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out float x);
            float.TryParse(splitted[1], System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out float y);
            float.TryParse(splitted[2], System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out float z);
            return Unity.Mathematics.quaternion.Euler(x, y, z);
        }

        public override void Serialize(System.Text.StringBuilder builder, object obj, UnityEditor.SerializedProperty property) {
            var val = (Unity.Mathematics.quaternion)obj;
            var euler = Unity.Mathematics.math.Euler(val);
            builder.Append('"');
            builder.Append(euler.x.ToString(System.Globalization.CultureInfo.InvariantCulture));
            builder.Append(", ");
            builder.Append(euler.y.ToString(System.Globalization.CultureInfo.InvariantCulture));
            builder.Append(", ");
            builder.Append(euler.z.ToString(System.Globalization.CultureInfo.InvariantCulture));
            builder.Append('"');
        }

        public override void Deserialize(object obj, UnityEditor.SerializedProperty property) {
            property.boxedValue = this.FromString(obj.GetType(), (string)obj);
        }

    }

    public class SFloatSerializer : SerializerBase<sfloat> {

        public override object FromString(System.Type fieldType, string value) {
            float.TryParse(value, System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out var val);
            return (sfloat)(val);
        }
        
        public override void Serialize(System.Text.StringBuilder builder, object obj, UnityEditor.SerializedProperty property) {
            var val = (sfloat)obj;
            builder.Append(val.ToString(System.Globalization.CultureInfo.InvariantCulture));
        }

        public override void Deserialize(object obj, UnityEditor.SerializedProperty property) {
            property.boxedValue = this.FromString(obj.GetType(), (string)obj);
        }

    }

    public class FpFloat2Serializer : SerializerBase<ME.BECS.FixedPoint.float2> {

        public override object FromString(System.Type fieldType, string value) {
            var splitted = (value).Split(',', System.StringSplitOptions.RemoveEmptyEntries);
            float.TryParse(splitted[0], System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out float x);
            float.TryParse(splitted[1], System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out float y);
            return new ME.BECS.FixedPoint.float2(x, y);
        }
        
        public override void Serialize(System.Text.StringBuilder builder, object obj, UnityEditor.SerializedProperty property) {
            var val = (ME.BECS.FixedPoint.float2)obj;
            builder.Append('"');
            builder.Append(val.x.ToString(System.Globalization.CultureInfo.InvariantCulture));
            builder.Append(", ");
            builder.Append(val.y.ToString(System.Globalization.CultureInfo.InvariantCulture));
            builder.Append('"');
        }

        public override void Deserialize(object obj, UnityEditor.SerializedProperty property) {
            property.boxedValue = this.FromString(obj.GetType(), (string)obj);
        }

    }

    public class FpFloat3Serializer : SerializerBase<ME.BECS.FixedPoint.float3> {
        
        public override object FromString(System.Type fieldType, string value) {
            var splitted = ((string)value).Split(',', System.StringSplitOptions.RemoveEmptyEntries);
            float.TryParse(splitted[0], System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out float x);
            float.TryParse(splitted[1], System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out float y);
            float.TryParse(splitted[2], System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out float z);
            return new ME.BECS.FixedPoint.float3(x, y, z);
        }

        public override void Serialize(System.Text.StringBuilder builder, object obj, UnityEditor.SerializedProperty property) {
            var val = (ME.BECS.FixedPoint.float3)obj;
            builder.Append('"');
            builder.Append(val.x.ToString(System.Globalization.CultureInfo.InvariantCulture));
            builder.Append(", ");
            builder.Append(val.y.ToString(System.Globalization.CultureInfo.InvariantCulture));
            builder.Append(", ");
            builder.Append(val.z.ToString(System.Globalization.CultureInfo.InvariantCulture));
            builder.Append('"');
        }

        public override void Deserialize(object obj, UnityEditor.SerializedProperty property) {
            property.boxedValue = this.FromString(obj.GetType(), (string)obj);
        }

    }

    public class FpFloat4Serializer : SerializerBase<ME.BECS.FixedPoint.float4> {
        
        public override object FromString(System.Type fieldType, string value) {
            var splitted = (value).Split(',', System.StringSplitOptions.RemoveEmptyEntries);
            float.TryParse(splitted[0], System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out float x);
            float.TryParse(splitted[1], System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out float y);
            float.TryParse(splitted[2], System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out float z);
            float.TryParse(splitted[3], System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out float w);
            return new ME.BECS.FixedPoint.float4(x, y, z, w);
        }

        public override void Serialize(System.Text.StringBuilder builder, object obj, UnityEditor.SerializedProperty property) {
            var val = (ME.BECS.FixedPoint.float4)obj;
            builder.Append('"');
            builder.Append(val.x.ToString(System.Globalization.CultureInfo.InvariantCulture));
            builder.Append(", ");
            builder.Append(val.y.ToString(System.Globalization.CultureInfo.InvariantCulture));
            builder.Append(", ");
            builder.Append(val.z.ToString(System.Globalization.CultureInfo.InvariantCulture));
            builder.Append(", ");
            builder.Append(val.w.ToString(System.Globalization.CultureInfo.InvariantCulture));
            builder.Append('"');
        }

        public override void Deserialize(object obj, UnityEditor.SerializedProperty property) {
            property.boxedValue = this.FromString(obj.GetType(), (string)obj);
        }

    }

    public class FpQuaternionSerializer : SerializerBase<ME.BECS.FixedPoint.quaternion> {
        
        public override object FromString(System.Type fieldType, string value) {
            var splitted = ((string)value).Split(',', System.StringSplitOptions.RemoveEmptyEntries);
            float.TryParse(splitted[0], System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out float x);
            float.TryParse(splitted[1], System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out float y);
            float.TryParse(splitted[2], System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out float z);
            return ME.BECS.FixedPoint.quaternion.Euler(x, y, z);
        }

        public override void Serialize(System.Text.StringBuilder builder, object obj, UnityEditor.SerializedProperty property) {
            var val = (ME.BECS.FixedPoint.quaternion)obj;
            var euler = val.ToEuler();
            builder.Append('"');
            builder.Append(euler.x.ToString(System.Globalization.CultureInfo.InvariantCulture));
            builder.Append(", ");
            builder.Append(euler.y.ToString(System.Globalization.CultureInfo.InvariantCulture));
            builder.Append(", ");
            builder.Append(euler.z.ToString(System.Globalization.CultureInfo.InvariantCulture));
            builder.Append('"');
        }

        public override void Deserialize(object obj, UnityEditor.SerializedProperty property) {
            property.boxedValue = this.FromString(obj.GetType(), (string)obj);
        }

    }

    public class LayerSerializer : SerializerBase<ME.BECS.Units.Layer> {
        
        public override void Serialize(System.Text.StringBuilder builder, object obj, UnityEditor.SerializedProperty property) {
            var layer = (ME.BECS.Units.Layer)obj;
            builder.Append('"');
            builder.Append(ME.BECS.Units.Editor.LayerAliasUtils.GetAliasOf(layer));
            builder.Append('"');
        }

        public override object FromString(System.Type fieldType, string value) {
            var fromString = new ME.BECS.Units.Layer { value = ME.BECS.Units.Editor.LayerAliasUtils.GetLayerByAlias(value).value };
            return fromString;
        }

        public override void Deserialize(object obj, UnityEditor.SerializedProperty property) {
            property.boxedValue = this.FromString(obj.GetType(), (string)obj);
        }

    }
    
    public class LayerMaskSerializer : SerializerBase<ME.BECS.Units.LayerMask> {
        
        public override void Serialize(System.Text.StringBuilder builder, object obj, UnityEditor.SerializedProperty property) {
            var layer = (ME.BECS.Units.LayerMask)obj;
            builder.Append('"');
            builder.Append(ME.BECS.Units.Editor.LayerAliasUtils.LayerMaskToString(layer));
            builder.Append('"'); 
        }

        public override object FromString(System.Type fieldType, string value) {
            return ME.BECS.Units.Editor.LayerAliasUtils.StringToLayerMask(value);
        }

        public override void Deserialize(object obj, UnityEditor.SerializedProperty property) {
            property.boxedValue = this.FromString(obj.GetType(), (string)obj);
        }

    }

}