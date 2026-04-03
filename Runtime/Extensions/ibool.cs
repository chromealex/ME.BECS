using INLINE = System.Runtime.CompilerServices.MethodImplAttribute;
using System.Runtime.InteropServices;

/// <summary>
/// Implementation of blittable boolean
/// </summary>
[System.Serializable]
[StructLayout(LayoutKind.Sequential, Size = 4, Pack = 4)]
public struct ibool : System.IEquatable<ibool> {

    public int value;

    [INLINE(256)]
    public ibool(int value) {
        this.value = value;
    }

    [INLINE(256)]
    public ibool(bool value) {
        this.value = value ? 1 : 0;
    }

    [INLINE(256)]
    public static implicit operator ibool(bool value) => new ibool() { value = (value == true ? 1 : 0) };

    [INLINE(256)]
    public static implicit operator bool(ibool value) => value.value == 1;
    
    [INLINE(256)]
    public static bool operator ==(ibool a, bool b) => (a.value == 1 ? true : false) == b;

    [INLINE(256)]
    public static bool operator !=(ibool a, bool b) => !(a == b);

    [INLINE(256)]
    public static bool operator ==(ibool a, int b) => a.value == b;

    [INLINE(256)]
    public static bool operator !=(ibool a, int b) => !(a == b);

    [INLINE(256)]
    public bool Equals(ibool other) {
        return this.value == other.value;
    }

    [INLINE(256)]
    public override bool Equals(object obj) {
        return obj is ibool other && this.Equals(other);
    }

    [INLINE(256)]
    public override int GetHashCode() {
        return this.value.GetHashCode();
    }

    public override string ToString() {
        return this.value == 0 ? "false" : $"true ({this.value})";
    }

}