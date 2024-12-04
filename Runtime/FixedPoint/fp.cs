namespace ME.BECS {
    
    using Unity.Mathematics;

    public struct fp {

        internal const uint PRECISION = 1000;
        
        internal int value;

        public static readonly fp MaxValue = new fp(int.MaxValue);
        
        public fp(int rawValue) => this.value = rawValue;

        public fp FromFloat(float value) {
            this.value = (int)(value * PRECISION);
            return this;
        }

        public float ToFloat() {
            return this.value / (float)PRECISION;
        }
        
        public static fp operator +(fp value1, fp value2) {
            if (value1.value == int.MaxValue || value2.value == int.MaxValue) {
                return fp.MaxValue;
            }
            value1.value += value2.value;
            return value1;
        }
        
        public static fp operator -(fp value1, fp value2) {
            value1.value -= value2.value;
            return value1;
        }

        public static fp operator *(fp value1, fp value2) {
            value1.value *= value2.value;
            return value1;
        }

        public static fp operator /(fp value1, fp value2) {
            value1.value /= value2.value;
            return value1;
        }

        public override string ToString() {
            return this.value.ToString();
        }

    }

}