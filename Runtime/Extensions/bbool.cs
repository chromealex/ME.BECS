using INLINE = System.Runtime.CompilerServices.MethodImplAttribute;
using System.Runtime.InteropServices;

/// <summary>
/// Implementation of blittable boolean
/// </summary>
[System.Serializable]
[StructLayout(LayoutKind.Sequential, Size = 1, Pack = 1)]
public struct bbool : System.IEquatable<bbool> {

    public byte value;

    [INLINE(256)]
    public bbool(int value) {
        this.value = (byte)value;
    }

    [INLINE(256)]
    public bbool(bool value) {
        this.value = value ? (byte)1 : (byte)0;
    }

    [INLINE(256)]
    public static implicit operator bbool(bool value) => new bbool() { value = (value == true ? (byte)1 : (byte)0) };

    [INLINE(256)]
    public static implicit operator bool(bbool value) => value.value == 1;
    
    [INLINE(256)]
    public static bool operator ==(bbool a, bool b) => (a.value == 1 ? true : false) == b;

    [INLINE(256)]
    public static bool operator !=(bbool a, bool b) => !(a == b);

    [INLINE(256)]
    public static bool operator ==(bbool a, byte b) => a.value == b;

    [INLINE(256)]
    public static bool operator !=(bbool a, byte b) => !(a == b);

    [INLINE(256)]
    public bool Equals(bbool other) {
        return this.value == other.value;
    }

    [INLINE(256)]
    public override bool Equals(object obj) {
        return obj is bbool other && this.Equals(other);
    }

    [INLINE(256)]
    public override int GetHashCode() {
        return this.value.GetHashCode();
    }

}