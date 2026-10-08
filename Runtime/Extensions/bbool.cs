#if INLINE_DISABLED
using INLINE = ME.BECS.NoInline;
#else
using INLINE = System.Runtime.CompilerServices.MethodImplAttribute;
#endif
using System.Runtime.InteropServices;

/// <summary>
/// Implementation of blittable boolean
/// </summary>
[System.Serializable]
[StructLayout(LayoutKind.Sequential, Size = 1, Pack = 1)]
public struct bbool : System.IEquatable<bbool> {

    /// <summary>
    /// Stored value used by this instance.
    /// </summary>
    public byte value;

    /// <summary>
    /// Initializes <c>bbool</c> from the supplied value.
    /// </summary>
    [INLINE(256)]
    public bbool(int value) {
        this.value = (byte)value;
    }

    /// <summary>
    /// Initializes <c>bbool</c> from the supplied value.
    /// </summary>
    [INLINE(256)]
    public bbool(bool value) {
        this.value = value ? (byte)1 : (byte)0;
    }

    /// <summary>
    /// Converts the supplied value to <c>bbool</c>.
    /// </summary>
    [INLINE(256)]
    public static implicit operator bbool(bool value) => new bbool() { value = (value == true ? (byte)1 : (byte)0) };

    /// <summary>
    /// Converts the supplied value to <c>bool</c>.
    /// </summary>
    [INLINE(256)]
    public static implicit operator bool(bbool value) => value.value == 1;
    
    /// <summary>
    /// Tests equality of the operands.
    /// </summary>
    [INLINE(256)]
    public static bool operator ==(bbool a, bool b) => (a.value == 1 ? true : false) == b;

    /// <summary>
    /// Tests whether the operands differ.
    /// </summary>
    [INLINE(256)]
    public static bool operator !=(bbool a, bool b) => !(a == b);

    /// <summary>
    /// Tests equality of the operands.
    /// </summary>
    [INLINE(256)]
    public static bool operator ==(bbool a, byte b) => a.value == b;

    /// <summary>
    /// Tests whether the operands differ.
    /// </summary>
    [INLINE(256)]
    public static bool operator !=(bbool a, byte b) => !(a == b);

    /// <summary>
    /// Tests equality using the identity or value comparison defined by this type.
    /// </summary>
    [INLINE(256)]
    public bool Equals(bbool other) {
        return this.value == other.value;
    }

    /// <summary>
    /// Tests equality using the identity or value comparison defined by this type.
    /// </summary>
    [INLINE(256)]
    public override bool Equals(object obj) {
        return obj is bbool other && this.Equals(other);
    }

    /// <summary>
    /// Returns a hash code consistent with this type's equality comparison.
    /// </summary>
    [INLINE(256)]
    public override int GetHashCode() {
        return this.value.GetHashCode();
    }

}