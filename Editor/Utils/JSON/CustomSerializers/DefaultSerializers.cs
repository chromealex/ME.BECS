using System.Reflection;

namespace ME.BECS.Editor.JSON {

    /// <summary>
    /// Serializes and deserializes object reference values in editor data.
    /// </summary>
    public abstract class ObjectReferenceSerializer<T, TValue> : SerializerBase<TValue> where T : UnityEngine.Object where TValue : unmanaged {

        /// <summary>
        /// Parses the supplied text into the represented value.
        /// </summary>
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
                var configObj = this.GetObject(fieldType, str.Substring(protocol.Length));
                ObjectReferenceRegistry.data.Add(configObj, out var isNew);
                if (isNew == true) ObjectReferenceValidate.Validate(ObjectReferenceRegistry.data.objects.Length - 1, 1);
                return this.Convert(fieldType, this.Deserialize(ObjectReferenceRegistry.GetId(configObj), (T)configObj, customData));
            } else {
                return null;
            }
        }

        /// <summary>
        /// Returns object.
        /// </summary>
        protected virtual UnityEngine.Object GetObject(System.Type fieldType, string path) {
            return EditorUtils.GetAssetByPathPart<T>(path);
        }

        /// <summary>
        /// Converts the supplied value into the requested representation.
        /// </summary>
        protected virtual object Convert(System.Type fieldType, TValue obj) {
            return obj;
        }

        /// <summary>
        /// Writes object reference serializer to the supplied serialized representation.
        /// </summary>
        public override void Serialize(System.Text.StringBuilder builder, object obj, UnityEditor.SerializedProperty property) {
            var customData = string.Empty;
            UnityEngine.Object objItem = null;
            if (obj is IObjectReferenceId objRefId) {
                objItem = ObjectReferenceRegistry.GetObjectBySourceId<T>(objRefId.Id);
            } else {
                objItem = ObjectReferenceRegistry.GetObjectBySourceId<T>(this.GetId((TValue)obj, ref customData));
            }
            if (objItem == null) {
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
                builder.Append(UnityEditor.AssetDatabase.GetAssetPath(objItem).Substring("Assets/".Length));
                if (string.IsNullOrEmpty(customData) == false) {
                    builder.Append('#');
                    builder.Append(customData);
                }
                builder.Append('"');
            }
        }

        /// <summary>
        /// Restores object reference serializer from the supplied serialized representation.
        /// </summary>
        public override void Deserialize(object obj, UnityEditor.SerializedProperty property) {
            property.boxedValue = this.FromString(obj.GetType(), (string)obj);
        }

        /// <summary>
        /// Protocol prefix used by <c>ObjectReferenceSerializer</c>.
        /// </summary>
        public abstract string ProtocolPrefix { get; }
        /// <summary>
        /// Returns ID.
        /// </summary>
        public abstract uint GetId(TValue obj, ref string customData);
        /// <summary>
        /// Restores object reference serializer from the supplied serialized representation.
        /// </summary>
        public abstract TValue Deserialize(uint objectId, T obj, string customData);

    }

    /// <summary>
    /// Serializes and deserializes primitive values in editor data.
    /// </summary>
    public abstract class PrimitiveSerializer<T> : SerializerBase<T> where T : System.IConvertible {
        /// <summary>
        /// Writes primitive serializer to the supplied serialized representation.
        /// </summary>
        public override void Serialize(System.Text.StringBuilder builder, object obj, UnityEditor.SerializedProperty property) {
            var val = (T)obj;
            builder.Append(this.ToString(val));
        }
        /// <summary>
        /// Restores primitive serializer from the supplied serialized representation.
        /// </summary>
        public override void Deserialize(object obj, UnityEditor.SerializedProperty property) {
            property.boxedValue = obj;
        }
        /// <summary>
        /// Formats this value for display or diagnostics.
        /// </summary>
        public virtual string ToString(T val) {
            return val.ToString(System.Globalization.CultureInfo.InvariantCulture);
        }
    }

    /// <summary>
    /// Serializes and deserializes enum values in editor data.
    /// </summary>
    public class EnumSerializer : SerializerBase<System.Enum> {
        
        /// <summary>
        /// Tests whether the context is valid.
        /// </summary>
        public override bool IsValid(System.Type type) => type.IsEnum;

        /// <summary>
        /// Parses the supplied text into the represented value.
        /// </summary>
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

        /// <summary>
        /// Writes enum serializer to the supplied serialized representation.
        /// </summary>
        public override void Serialize(System.Text.StringBuilder builder, object obj, UnityEditor.SerializedProperty property) {
            builder.Append('"');
            builder.Append(obj.ToString());
            builder.Append('"');
        }

        /// <summary>
        /// Restores enum serializer from the supplied serialized representation.
        /// </summary>
        public override void Deserialize(object obj, UnityEditor.SerializedProperty property) {
            property.boxedValue = this.FromString(obj.GetType(), (string)obj);
        }
    }

    /// <summary>
    /// Serializes and deserializes float values in editor data.
    /// </summary>
    public class FloatSerializer : PrimitiveSerializer<float> {
        /// <summary>
        /// Parses the supplied text into the represented value.
        /// </summary>
        public override object FromString(System.Type fieldType, string value) => float.Parse(value, System.Globalization.CultureInfo.InvariantCulture);
    }

    /// <summary>
    /// Serializes and deserializes double values in editor data.
    /// </summary>
    public class DoubleSerializer : PrimitiveSerializer<double> {
        /// <summary>
        /// Parses the supplied text into the represented value.
        /// </summary>
        public override object FromString(System.Type fieldType, string value) => double.Parse(value);
    }

    /// <summary>
    /// Serializes and deserializes decimal values in editor data.
    /// </summary>
    public class DecimalSerializer : PrimitiveSerializer<decimal> {
        /// <summary>
        /// Parses the supplied text into the represented value.
        /// </summary>
        public override object FromString(System.Type fieldType, string value) => decimal.Parse(value);
    }

    /// <summary>
    /// Serializes and deserializes int values in editor data.
    /// </summary>
    public class IntSerializer : PrimitiveSerializer<int> {
        /// <summary>
        /// Parses the supplied text into the represented value.
        /// </summary>
        public override object FromString(System.Type fieldType, string value) => int.Parse(value);
    }

    /// <summary>
    /// Serializes and deserializes u int values in editor data.
    /// </summary>
    public class UIntSerializer : PrimitiveSerializer<uint> {
        /// <summary>
        /// Parses the supplied text into the represented value.
        /// </summary>
        public override object FromString(System.Type fieldType, string value) => uint.Parse(value);
    }

    /// <summary>
    /// Serializes and deserializes short values in editor data.
    /// </summary>
    public class ShortSerializer : PrimitiveSerializer<short> {
        /// <summary>
        /// Parses the supplied text into the represented value.
        /// </summary>
        public override object FromString(System.Type fieldType, string value) => short.Parse(value);
    }

    /// <summary>
    /// Serializes and deserializes u short values in editor data.
    /// </summary>
    public class UShortSerializer : PrimitiveSerializer<ushort> {
        /// <summary>
        /// Parses the supplied text into the represented value.
        /// </summary>
        public override object FromString(System.Type fieldType, string value) => ushort.Parse(value);
    }

    /// <summary>
    /// Serializes and deserializes s byte values in editor data.
    /// </summary>
    public class SByteSerializer : PrimitiveSerializer<sbyte> {
        /// <summary>
        /// Parses the supplied text into the represented value.
        /// </summary>
        public override object FromString(System.Type fieldType, string value) => sbyte.Parse(value);
    }

    /// <summary>
    /// Serializes and deserializes byte values in editor data.
    /// </summary>
    public class ByteSerializer : PrimitiveSerializer<byte> {
        /// <summary>
        /// Parses the supplied text into the represented value.
        /// </summary>
        public override object FromString(System.Type fieldType, string value) => byte.Parse(value);
    }

    /// <summary>
    /// Serializes and deserializes bool values in editor data.
    /// </summary>
    public class BoolSerializer : PrimitiveSerializer<bool> {
        /// <summary>
        /// Parses the supplied text into the represented value.
        /// </summary>
        public override object FromString(System.Type fieldType, string value) => bool.Parse(value.ToLower());
        /// <summary>
        /// Formats this value for display or diagnostics.
        /// </summary>
        public override string ToString(bool obj) {
            return obj.ToString().ToLower();
        }

    }

    /// <summary>
    /// Serializes and deserializes float2 values in editor data.
    /// </summary>
    public class Float2Serializer : SerializerBase<Unity.Mathematics.float2> {

        /// <summary>
        /// Parses the supplied text into the represented value.
        /// </summary>
        public override object FromString(System.Type fieldType, string value) {
            var splitted = (value).Split(',', System.StringSplitOptions.RemoveEmptyEntries);
            float.TryParse(splitted[0], System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out float x);
            float.TryParse(splitted[1], System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out float y);
            return new Unity.Mathematics.float2(x, y);
        }
        
        /// <summary>
        /// Writes float2 serializer to the supplied serialized representation.
        /// </summary>
        public override void Serialize(System.Text.StringBuilder builder, object obj, UnityEditor.SerializedProperty property) {
            var val = (Unity.Mathematics.float2)obj;
            builder.Append('"');
            builder.Append(val.x.ToString(System.Globalization.CultureInfo.InvariantCulture));
            builder.Append(", ");
            builder.Append(val.y.ToString(System.Globalization.CultureInfo.InvariantCulture));
            builder.Append('"');
        }

        /// <summary>
        /// Restores float2 serializer from the supplied serialized representation.
        /// </summary>
        public override void Deserialize(object obj, UnityEditor.SerializedProperty property) {
            property.boxedValue = this.FromString(obj.GetType(), (string)obj);
        }

    }

    /// <summary>
    /// Serializes and deserializes float3 values in editor data.
    /// </summary>
    public class Float3Serializer : SerializerBase<Unity.Mathematics.float3> {
        
        /// <summary>
        /// Parses the supplied text into the represented value.
        /// </summary>
        public override object FromString(System.Type fieldType, string value) {
            var splitted = ((string)value).Split(',', System.StringSplitOptions.RemoveEmptyEntries);
            float.TryParse(splitted[0], System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out float x);
            float.TryParse(splitted[1], System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out float y);
            float.TryParse(splitted[2], System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out float z);
            return new Unity.Mathematics.float3(x, y, z);
        }

        /// <summary>
        /// Writes float3 serializer to the supplied serialized representation.
        /// </summary>
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

        /// <summary>
        /// Restores float3 serializer from the supplied serialized representation.
        /// </summary>
        public override void Deserialize(object obj, UnityEditor.SerializedProperty property) {
            property.boxedValue = this.FromString(obj.GetType(), (string)obj);
        }

    }

    /// <summary>
    /// Serializes and deserializes float4 values in editor data.
    /// </summary>
    public class Float4Serializer : SerializerBase<Unity.Mathematics.float4> {
        
        /// <summary>
        /// Parses the supplied text into the represented value.
        /// </summary>
        public override object FromString(System.Type fieldType, string value) {
            var splitted = (value).Split(',', System.StringSplitOptions.RemoveEmptyEntries);
            float.TryParse(splitted[0], System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out float x);
            float.TryParse(splitted[1], System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out float y);
            float.TryParse(splitted[2], System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out float z);
            float.TryParse(splitted[3], System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out float w);
            return new Unity.Mathematics.float4(x, y, z, w);
        }

        /// <summary>
        /// Writes float4 serializer to the supplied serialized representation.
        /// </summary>
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

        /// <summary>
        /// Restores float4 serializer from the supplied serialized representation.
        /// </summary>
        public override void Deserialize(object obj, UnityEditor.SerializedProperty property) {
            property.boxedValue = this.FromString(obj.GetType(), (string)obj);
        }

    }

    /// <summary>
    /// Serializes and deserializes quaternion values in editor data.
    /// </summary>
    public class QuaternionSerializer : SerializerBase<Unity.Mathematics.quaternion> {
        
        /// <summary>
        /// Parses the supplied text into the represented value.
        /// </summary>
        public override object FromString(System.Type fieldType, string value) {
            var splitted = ((string)value).Split(',', System.StringSplitOptions.RemoveEmptyEntries);
            float.TryParse(splitted[0], System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out float x);
            float.TryParse(splitted[1], System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out float y);
            float.TryParse(splitted[2], System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out float z);
            return Unity.Mathematics.quaternion.Euler(x, y, z);
        }

        /// <summary>
        /// Writes quaternion serializer to the supplied serialized representation.
        /// </summary>
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

        /// <summary>
        /// Restores quaternion serializer from the supplied serialized representation.
        /// </summary>
        public override void Deserialize(object obj, UnityEditor.SerializedProperty property) {
            property.boxedValue = this.FromString(obj.GetType(), (string)obj);
        }

    }

    /// <summary>
    /// Serializes and deserializes s float values in editor data.
    /// </summary>
    public class SFloatSerializer : SerializerBase<sfloat> {

        /// <summary>
        /// Parses the supplied text into the represented value.
        /// </summary>
        public override object FromString(System.Type fieldType, string value) {
            float.TryParse(value, System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out var val);
            return (sfloat)(val);
        }
        
        /// <summary>
        /// Writes s float serializer to the supplied serialized representation.
        /// </summary>
        public override void Serialize(System.Text.StringBuilder builder, object obj, UnityEditor.SerializedProperty property) {
            var val = (sfloat)obj;
            builder.Append(val.ToString(System.Globalization.CultureInfo.InvariantCulture));
        }

        /// <summary>
        /// Restores s float serializer from the supplied serialized representation.
        /// </summary>
        public override void Deserialize(object obj, UnityEditor.SerializedProperty property) {
            property.boxedValue = this.FromString(obj.GetType(), (string)obj);
        }

    }

    /// <summary>
    /// Serializes and deserializes fp float2 values in editor data.
    /// </summary>
    public class FpFloat2Serializer : SerializerBase<ME.BECS.FixedPoint.float2> {

        /// <summary>
        /// Parses the supplied text into the represented value.
        /// </summary>
        public override object FromString(System.Type fieldType, string value) {
            var splitted = (value).Split(',', System.StringSplitOptions.RemoveEmptyEntries);
            float.TryParse(splitted[0], System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out float x);
            float.TryParse(splitted[1], System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out float y);
            return new ME.BECS.FixedPoint.float2(x, y);
        }
        
        /// <summary>
        /// Writes fp float2 serializer to the supplied serialized representation.
        /// </summary>
        public override void Serialize(System.Text.StringBuilder builder, object obj, UnityEditor.SerializedProperty property) {
            var val = (ME.BECS.FixedPoint.float2)obj;
            builder.Append('"');
            builder.Append(val.x.ToString(System.Globalization.CultureInfo.InvariantCulture));
            builder.Append(", ");
            builder.Append(val.y.ToString(System.Globalization.CultureInfo.InvariantCulture));
            builder.Append('"');
        }

        /// <summary>
        /// Restores fp float2 serializer from the supplied serialized representation.
        /// </summary>
        public override void Deserialize(object obj, UnityEditor.SerializedProperty property) {
            property.boxedValue = this.FromString(obj.GetType(), (string)obj);
        }

    }

    /// <summary>
    /// Serializes and deserializes fp float3 values in editor data.
    /// </summary>
    public class FpFloat3Serializer : SerializerBase<ME.BECS.FixedPoint.float3> {
        
        /// <summary>
        /// Parses the supplied text into the represented value.
        /// </summary>
        public override object FromString(System.Type fieldType, string value) {
            var splitted = ((string)value).Split(',', System.StringSplitOptions.RemoveEmptyEntries);
            float.TryParse(splitted[0], System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out float x);
            float.TryParse(splitted[1], System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out float y);
            float.TryParse(splitted[2], System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out float z);
            return new ME.BECS.FixedPoint.float3(x, y, z);
        }

        /// <summary>
        /// Writes fp float3 serializer to the supplied serialized representation.
        /// </summary>
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

        /// <summary>
        /// Restores fp float3 serializer from the supplied serialized representation.
        /// </summary>
        public override void Deserialize(object obj, UnityEditor.SerializedProperty property) {
            property.boxedValue = this.FromString(obj.GetType(), (string)obj);
        }

    }

    /// <summary>
    /// Serializes and deserializes fp float4 values in editor data.
    /// </summary>
    public class FpFloat4Serializer : SerializerBase<ME.BECS.FixedPoint.float4> {
        
        /// <summary>
        /// Parses the supplied text into the represented value.
        /// </summary>
        public override object FromString(System.Type fieldType, string value) {
            var splitted = (value).Split(',', System.StringSplitOptions.RemoveEmptyEntries);
            float.TryParse(splitted[0], System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out float x);
            float.TryParse(splitted[1], System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out float y);
            float.TryParse(splitted[2], System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out float z);
            float.TryParse(splitted[3], System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out float w);
            return new ME.BECS.FixedPoint.float4(x, y, z, w);
        }

        /// <summary>
        /// Writes fp float4 serializer to the supplied serialized representation.
        /// </summary>
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

        /// <summary>
        /// Restores fp float4 serializer from the supplied serialized representation.
        /// </summary>
        public override void Deserialize(object obj, UnityEditor.SerializedProperty property) {
            property.boxedValue = this.FromString(obj.GetType(), (string)obj);
        }

    }

    /// <summary>
    /// Serializes and deserializes fp quaternion values in editor data.
    /// </summary>
    public class FpQuaternionSerializer : SerializerBase<ME.BECS.FixedPoint.quaternion> {
        
        /// <summary>
        /// Parses the supplied text into the represented value.
        /// </summary>
        public override object FromString(System.Type fieldType, string value) {
            var splitted = ((string)value).Split(',', System.StringSplitOptions.RemoveEmptyEntries);
            float.TryParse(splitted[0], System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out float x);
            float.TryParse(splitted[1], System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out float y);
            float.TryParse(splitted[2], System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out float z);
            return ME.BECS.FixedPoint.quaternion.Euler(x, y, z);
        }

        /// <summary>
        /// Writes fp quaternion serializer to the supplied serialized representation.
        /// </summary>
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

        /// <summary>
        /// Restores fp quaternion serializer from the supplied serialized representation.
        /// </summary>
        public override void Deserialize(object obj, UnityEditor.SerializedProperty property) {
            property.boxedValue = this.FromString(obj.GetType(), (string)obj);
        }

    }

    /// <summary>
    /// Serializes and deserializes layer values in editor data.
    /// </summary>
    public class LayerSerializer : SerializerBase<ME.BECS.Units.Layer> {
        
        /// <summary>
        /// Writes layer serializer to the supplied serialized representation.
        /// </summary>
        public override void Serialize(System.Text.StringBuilder builder, object obj, UnityEditor.SerializedProperty property) {
            var layer = (ME.BECS.Units.Layer)obj;
            builder.Append('"');
            builder.Append(ME.BECS.Units.Editor.LayerAliasUtils.GetAliasOf(layer));
            builder.Append('"');
        }

        /// <summary>
        /// Parses the supplied text into the represented value.
        /// </summary>
        public override object FromString(System.Type fieldType, string value) {
            var fromString = new ME.BECS.Units.Layer { value = ME.BECS.Units.Editor.LayerAliasUtils.GetLayerByAlias(value).value };
            return fromString;
        }

        /// <summary>
        /// Restores layer serializer from the supplied serialized representation.
        /// </summary>
        public override void Deserialize(object obj, UnityEditor.SerializedProperty property) {
            property.boxedValue = this.FromString(obj.GetType(), (string)obj);
        }

    }
    
    /// <summary>
    /// Serializes and deserializes layer mask values in editor data.
    /// </summary>
    public class LayerMaskSerializer : SerializerBase<ME.BECS.Units.LayerMask> {
        
        /// <summary>
        /// Writes layer mask serializer to the supplied serialized representation.
        /// </summary>
        public override void Serialize(System.Text.StringBuilder builder, object obj, UnityEditor.SerializedProperty property) {
            var layer = (ME.BECS.Units.LayerMask)obj;
            builder.Append('"');
            builder.Append(ME.BECS.Units.Editor.LayerAliasUtils.LayerMaskToString(layer));
            builder.Append('"'); 
        }

        /// <summary>
        /// Parses the supplied text into the represented value.
        /// </summary>
        public override object FromString(System.Type fieldType, string value) {
            return ME.BECS.Units.Editor.LayerAliasUtils.StringToLayerMask(value);
        }

        /// <summary>
        /// Restores layer mask serializer from the supplied serialized representation.
        /// </summary>
        public override void Deserialize(object obj, UnityEditor.SerializedProperty property) {
            property.boxedValue = this.FromString(obj.GetType(), (string)obj);
        }

    }

}