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
[StructLayout(LayoutKind.Sequential, Size = 4, Pack = 4)]
public struct ibool : System.IEquatable<ibool> {

    /// <summary>
    /// Stored value used by this instance.
    /// </summary>
    public int value;

    /// <summary>
    /// Initializes <c>ibool</c> from the supplied value.
    /// </summary>
    [INLINE(256)]
    public ibool(int value) {
        this.value = value;
    }

    /// <summary>
    /// Initializes <c>ibool</c> from the supplied value.
    /// </summary>
    [INLINE(256)]
    public ibool(bool value) {
        this.value = value ? 1 : 0;
    }

    /// <summary>
    /// Converts the supplied value to <c>ibool</c>.
    /// </summary>
    [INLINE(256)]
    public static implicit operator ibool(bool value) => new ibool() { value = (value == true ? 1 : 0) };

    /// <summary>
    /// Converts the supplied value to <c>bool</c>.
    /// </summary>
    [INLINE(256)]
    public static implicit operator bool(ibool value) => value.value == 1;
    
    /// <summary>
    /// Tests equality of the operands.
    /// </summary>
    [INLINE(256)]
    public static bool operator ==(ibool a, bool b) => (a.value == 1 ? true : false) == b;

    /// <summary>
    /// Tests whether the operands differ.
    /// </summary>
    [INLINE(256)]
    public static bool operator !=(ibool a, bool b) => !(a == b);

    /// <summary>
    /// Tests equality of the operands.
    /// </summary>
    [INLINE(256)]
    public static bool operator ==(ibool a, int b) => a.value == b;

    /// <summary>
    /// Tests whether the operands differ.
    /// </summary>
    [INLINE(256)]
    public static bool operator !=(ibool a, int b) => !(a == b);

    /// <summary>
    /// Tests equality using the identity or value comparison defined by this type.
    /// </summary>
    [INLINE(256)]
    public bool Equals(ibool other) {
        return this.value == other.value;
    }

    /// <summary>
    /// Tests equality using the identity or value comparison defined by this type.
    /// </summary>
    [INLINE(256)]
    public override bool Equals(object obj) {
        return obj is ibool other && this.Equals(other);
    }

    /// <summary>
    /// Returns a hash code consistent with this type's equality comparison.
    /// </summary>
    [INLINE(256)]
    public override int GetHashCode() {
        return this.value.GetHashCode();
    }

    /// <summary>
    /// Formats this value for display or diagnostics.
    /// </summary>
    public override string ToString() {
        return this.value == 0 ? "false" : $"true ({this.value})";
    }

}