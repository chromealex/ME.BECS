namespace ME.BECS.Tests.GpuPrototype {
    // Test-only integer port of the checkout's sfloat +, -, * (not native GPU float).
    internal static class GpuSfloatTransformShader {
        public const string Source = @"
#pragma kernel Main
#pragma kernel Arithmetic
#pragma kernel LocalHierarchy
#pragma kernel WorldHierarchy
#pragma kernel PrepareResident
#pragma kernel ExportRenderMatrices
struct Input { uint4 p; uint4 r; uint4 s; };
struct Matrix { uint4 c0; uint4 c1; uint4 c2; uint4 c3; };
StructuredBuffer<Input> Inputs;
RWStructuredBuffer<Matrix> Outputs;
StructuredBuffer<uint2> Pairs;
RWStructuredBuffer<uint4> ArithmeticResults;
uint Count;
uint exponent(uint a) { return (a >> 23) & 255u; }
bool finite(uint a) { return exponent(a) != 255u; }
bool nanvalue(uint a) { return (a & 0x7fffffffu) > 0x7f800000u; }
bool zero(uint a) { return (a & 0x7fffffffu) == 0u; }
int mantissa(uint a) {
    uint m = a & 0x7fffffu;
    if (exponent(a) != 0u) { m |= 0x800000u; }
    return (a & 0x80000000u) != 0u ? -int(m) : int(m);
}
uint addordered(uint a, uint b) {
    int ea = int(exponent(a)); int eb = int(exponent(b)); int delta = ea - eb;
    if (ea == 255) {
        if (eb != 255) { return a; }
        return a == b ? a : 0xffc00000u;
    }
    if (delta > 25) { return a; }
    int ma = mantissa(a); int mb = mantissa(b);
    if (eb == 0) { eb = 1; if (ea == 0) { ea = 1; } delta = ea - eb; }
    // Unsigned shifts preserve the C# wraparound bits; signed right shift is arithmetic.
    int m = asint(asuint(ma) << 6) + (asint(asuint(mb) << 6) >> delta);
    uint am = m < 0 ? uint(-m) : uint(m);
    if (am == 0u) { return 0u; }
    uint msb = am >> 23; int e = ea - 6;
    while (msb == 0u) { e -= 8; am <<= 8; msb = am >> 23; }
    int bit = 0; while (msb > 1u) { msb >>= 1; ++bit; }
    e += bit; am >>= bit;
    uint sign = asuint(m) & 0x80000000u;
    if (uint(e - 1) < 254u) { return sign | (uint(e) << 23) | (am & 0x7fffffu); }
    if (e >= 255) { return sign | 0x7f800000u; }
    if (e >= -24) { return sign | (am >> (-e + 1)); }
    return 0u;
}
uint sfadd(uint a, uint b) { return exponent(a) >= exponent(b) ? addordered(a,b) : addordered(b,a); }
uint sfsub(uint a, uint b) { return sfadd(a,b ^ 0x80000000u); }
uint sfmul(uint a, uint b) {
    int ea = int(exponent(a)); int eb = int(exponent(b));
    uint sign = (a ^ b) & 0x80000000u;
    // Match operand-order-sensitive special handling in sfloat.operator*.
    if (ea == 0 && zero(a)) { return finite(b) ? sign : 0xffc00000u; }
    if (ea == 255) {
        if (nanvalue(a)) { return a; }
        if (zero(b) || nanvalue(b)) { return 0xffc00000u; }
        return sign | 0x7f800000u;
    }
    if (eb == 0 && zero(b)) { return sign; }
    if (eb == 255) {
        if (nanvalue(b)) { return b; }
        return sign | 0x7f800000u;
    }
    uint ma = a & 0x7fffffu; uint mb = b & 0x7fffffu;
    if (ea == 0) { ea = 1; while ((ma & 0x800000u) == 0u) { ma <<= 1; --ea; } }
    else { ma |= 0x800000u; }
    if (eb == 0) { eb = 1; while ((mb & 0x800000u) == 0u) { mb <<= 1; --eb; } }
    else { mb |= 0x800000u; }
    // Exact 24x24 product using 16-bit limbs: no shader int64 dependency.
    uint low = (ma & 65535u) * (mb & 65535u);
    uint cross = (ma >> 16) * (mb & 65535u) + (mb >> 16) * (ma & 65535u);
    uint lo = low + (cross << 16);
    uint hi = (ma >> 16) * (mb >> 16) + (cross >> 16) + (lo < low ? 1u : 0u);
    uint am = (hi << 9) | (lo >> 23);
    // C# signed long >> 23 floors negative products, rather than truncating toward zero.
    if (sign != 0u && (lo & 0x7fffffu) != 0u) { ++am; }
    int e = ea + eb - 127;
    if ((am & 0x1000000u) != 0u) { am >>= 1; ++e; }
    if (e >= 255) { return sign | 0x7f800000u; }
    if (e <= 0) { if (e <= -24) { return sign; } am >>= -e + 1; e = 0; }
    return sign | (uint(e) << 23) | (am & 0x7fffffu);
}
[numthreads(64,1,1)]
void Arithmetic(uint3 id:SV_DispatchThreadID) {
    if (id.x >= Count) { return; }
    uint2 p = Pairs[id.x];
    ArithmeticResults[id.x] = uint4(sfadd(p.x,p.y), sfsub(p.x,p.y), sfmul(p.x,p.y),0u);
}
Matrix localMatrix(Input i) {
    uint4 r = i.r; uint4 s = i.s;
    const uint one=0x3f800000u, two=0x40000000u;
    Matrix m;
    m.c0 = uint4(sfmul(sfsub(one,sfmul(two,sfadd(sfmul(r.y,r.y),sfmul(r.z,r.z)))),s.x),
        sfmul(sfmul(sfadd(sfmul(r.x,r.y),sfmul(r.z,r.w)),s.x),two),
        sfmul(sfmul(sfsub(sfmul(r.x,r.z),sfmul(r.y,r.w)),s.x),two),0u);
    m.c1 = uint4(sfmul(sfmul(sfsub(sfmul(r.x,r.y),sfmul(r.z,r.w)),s.y),two),
        sfmul(sfsub(one,sfmul(two,sfadd(sfmul(r.x,r.x),sfmul(r.z,r.z)))),s.y),
        sfmul(sfmul(sfadd(sfmul(r.y,r.z),sfmul(r.x,r.w)),s.y),two),0u);
    m.c2 = uint4(sfmul(sfmul(sfadd(sfmul(r.x,r.z),sfmul(r.y,r.w)),s.z),two),
        sfmul(sfmul(sfsub(sfmul(r.y,r.z),sfmul(r.x,r.w)),s.z),two),
        sfmul(sfsub(one,sfmul(two,sfadd(sfmul(r.x,r.x),sfmul(r.y,r.y)))),s.z),0u);
    m.c3 = uint4(i.p.xyz,one); return m;
}
[numthreads(64,1,1)]
void Main(uint3 id:SV_DispatchThreadID) {
    if (id.x < Count) { Outputs[id.x] = localMatrix(Inputs[id.x]); }
}
// info: input flags, parent index, local changed, world changed.
struct Node { Matrix local; Matrix world; uint4 info; };
RWStructuredBuffer<Node> Nodes;
StructuredBuffer<uint2> Roots;
uint RootCount;
bool sfequal(uint a, uint b) {
    return !nanvalue(a) && (a == b || (zero(a) && zero(b)));
}
bool vequal(uint4 a, uint4 b) {
    return sfequal(a.x,b.x) && sfequal(a.y,b.y) && sfequal(a.z,b.z) && sfequal(a.w,b.w);
}
bool mequal(Matrix a, Matrix b) {
    return vequal(a.c0,b.c0) && vequal(a.c1,b.c1) && vequal(a.c2,b.c2) && vequal(a.c3,b.c3);
}
uint4 vadd(uint4 a, uint4 b) {
    return uint4(sfadd(a.x,b.x),sfadd(a.y,b.y),sfadd(a.z,b.z),sfadd(a.w,b.w));
}
uint4 vmul(uint4 a, uint b) {
    return uint4(sfmul(a.x,b),sfmul(a.y,b),sfmul(a.z,b),sfmul(a.w,b));
}
uint4 matrixVector(Matrix a, uint4 b) {
    // Same left-associative expression as FixedPoint math.mul, no reassociation.
    return vadd(vadd(vadd(vmul(a.c0,b.x),vmul(a.c1,b.y)),vmul(a.c2,b.z)),vmul(a.c3,b.w));
}
Matrix matrixMultiply(Matrix a, Matrix b) {
    Matrix m;
    m.c0=matrixVector(a,b.c0); m.c1=matrixVector(a,b.c1);
    m.c2=matrixVector(a,b.c2); m.c3=matrixVector(a,b.c3);
    return m;
}
RWStructuredBuffer<Matrix> RenderMatrices;
[numthreads(64,1,1)]
void ExportRenderMatrices(uint3 id:SV_DispatchThreadID) {
    if (id.x < Count) { RenderMatrices[id.x]=Nodes[id.x].world; }
}
// Advance the previous completed update's static/calculated state, then clear per-step dirtiness.
// This benchmark has immutable topology/tags and no external world-matrix invalidations.
[numthreads(64,1,1)]
void PrepareResident(uint3 id:SV_DispatchThreadID) {
    if (id.x >= Count) { return; }
    Node n=Nodes[id.x];
    if (n.info.w!=0u) {
        n.info.x |= 32u;
        if ((n.info.x & 4u)!=0u) { n.info.x |= 8u; }
    }
    if ((n.info.x & 3u)==3u || (n.info.x & 8u)!=0u) { n.info.x &= ~1u; }
    n.info.x &= ~16u;
    n.info.z=0u; n.info.w=0u;
    Nodes[id.x]=n;
}
[numthreads(64,1,1)]
void LocalHierarchy(uint3 id:SV_DispatchThreadID) {
    if (id.x >= Count) { return; }
    Node n=Nodes[id.x];
    if ((n.info.x & 1u) != 0u) {
        Matrix m=localMatrix(Inputs[id.x]);
        if (!mequal(n.local,m)) { n.local=m; n.info.z=1u; }
    }
    Nodes[id.x]=n;
}
[numthreads(64,1,1)]
void WorldHierarchy(uint3 id:SV_DispatchThreadID) {
    if (id.x >= RootCount) { return; }
    uint2 range=Roots[id.x];
    // Trees have disjoint ranges. Parent indices precede children in the same thread.
    for (uint index=range.x; index<range.x+range.y; ++index) {
        Node n=Nodes[index]; uint flags=n.info.x;
        bool root=n.info.y==0xffffffffu;
        bool dirty=n.info.z!=0u || (flags & 16u)!=0u || (flags & 32u)==0u ||
            ((flags & 4u)!=0u && (flags & 8u)==0u);
        Matrix parent=n.world;
        if (!root) {
            Node p=Nodes[n.info.y]; parent=p.world; dirty=dirty || p.info.w!=0u;
        }
        if (dirty) {
            if (root) { n.world=n.local; } else { n.world=matrixMultiply(parent,n.local); }
            n.info.w=1u;
        }
        Nodes[index]=n;
    }
}

";
    }
}
