using UnityEngine;

#pragma warning disable 0660, 0661

namespace ME.BECS.FixedPoint
{
    public partial struct float2
    {
        public static explicit operator Vector2(float2 v)     { return new Vector2((float)v.x, (float)v.y); }
        public static explicit operator float2(Vector2 v)     { return new float2((sfloat)v.x, (sfloat)v.y); }
    }

    public partial struct float3
    {
        public static explicit operator Vector3(float3 v)     { return new Vector3((float)v.x, (float)v.y, (float)v.z); }
        public static explicit operator float3(Vector3 v)     { return new float3((sfloat)v.x, (sfloat)v.y, (sfloat)v.z); }
    }

    public partial struct float4
    {
        public static explicit operator float4(Vector4 v)     { return new float4((sfloat)v.x, (sfloat)v.y, (sfloat)v.z, (sfloat)v.w); }
        public static explicit operator Vector4(float4 v)     { return new Vector4((float)v.x, (float)v.y, (float)v.z, (float)v.w); }
    }
    
    public partial struct int2
    {
        public static implicit operator Vector2Int(int2 v)     { return new Vector2Int(v.x, v.y); }
        public static implicit operator int2(Vector2Int v)     { return new int2(v.x, v.y); }
    }

    public partial struct int3
    {
        public static implicit operator Vector3Int(int3 v)     { return new Vector3Int(v.x, v.y, v.z); }
        public static implicit operator int3(Vector3Int v)     { return new int3(v.x, v.y, v.z); }
    }

    public partial struct quaternion
    {
        public static explicit operator Quaternion(quaternion q)  { return new Quaternion((float)q.value.x, (float)q.value.y, (float)q.value.z, (float)q.value.w); }
        public static explicit operator quaternion(Quaternion q)  { return new quaternion((sfloat)q.x, (sfloat)q.y, (sfloat)q.z, (sfloat)q.w); }
    }

    public partial struct float4x4
    {
        public static explicit operator float4x4(Matrix4x4 m) { return new float4x4((float4)m.GetColumn(0), (float4)m.GetColumn(1), (float4)m.GetColumn(2), (float4)m.GetColumn(3)); }
        public static explicit operator Matrix4x4(float4x4 m) { return new Matrix4x4((Vector4)m.c0, (Vector4)m.c1, (Vector4)m.c2, (Vector4)m.c3); }
    }
    
    public partial struct float2
    {
        public static implicit operator Unity.Mathematics.float2(float2 v)     { return new Unity.Mathematics.float2((float)v.x, (float)v.y); }
        public static implicit operator float2(Unity.Mathematics.float2 v)     { return new float2((sfloat)v.x, (sfloat)v.y); }
    }

    public partial struct float3
    {
        public static implicit operator Unity.Mathematics.float3(float3 v)     { return new Unity.Mathematics.float3((float)v.x, (float)v.y, (float)v.z); }
        public static implicit operator float3(Unity.Mathematics.float3 v)     { return new float3((sfloat)v.x, (sfloat)v.y, (sfloat)v.z); }
    }

    public partial struct float4
    {
        public static implicit operator float4(Unity.Mathematics.float4 v)     { return new float4((sfloat)v.x, (sfloat)v.y, (sfloat)v.z, (sfloat)v.w); }
        public static implicit operator Unity.Mathematics.float4(float4 v)     { return new Unity.Mathematics.float4((float)v.x, (float)v.y, (float)v.z, (float)v.w); }
    }
    
    public partial struct int2
    {
        public static implicit operator Unity.Mathematics.int2(int2 v)     { return new Unity.Mathematics.int2(v.x, v.y); }
        public static implicit operator int2(Unity.Mathematics.int2 v)     { return new int2(v.x, v.y); }
    }

    public partial struct int3
    {
        public static implicit operator Unity.Mathematics.int3(int3 v)     { return new Unity.Mathematics.int3(v.x, v.y, v.z); }
        public static implicit operator int3(Unity.Mathematics.int3 v)     { return new int3(v.x, v.y, v.z); }
    }

    public partial struct int4
    {
        public static implicit operator int4(Unity.Mathematics.int4 v)     { return new int4(v.x, v.y, v.z, v.w); }
        public static implicit operator Unity.Mathematics.int4(int4 v)     { return new Unity.Mathematics.int4(v.x, v.y, v.z, v.w); }
    }

    public partial struct quaternion
    {
        public static implicit operator Unity.Mathematics.quaternion(quaternion q)  { return new Unity.Mathematics.quaternion((float)q.value.x, (float)q.value.y, (float)q.value.z, (float)q.value.w); }
        public static implicit operator quaternion(Unity.Mathematics.quaternion q)  { return new quaternion((sfloat)q.value.x, (sfloat)q.value.y, (sfloat)q.value.z, (sfloat)q.value.w); }
    }

    public partial struct float4x4
    {
        public static implicit operator float4x4(Unity.Mathematics.float4x4 m) { return new float4x4((float4)m.c0, (float4)m.c1, (float4)m.c2, (float4)m.c3); }
        public static implicit operator Unity.Mathematics.float4x4(float4x4 m) { return new Unity.Mathematics.float4x4((Vector4)m.c0, (Vector4)m.c1, (Vector4)m.c2, (Vector4)m.c3); }
    }
}
