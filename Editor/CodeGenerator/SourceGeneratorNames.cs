namespace ME.BECS.CodeGeneration {

    // Compiled into both the analyzer and Editor assembly. No Unity/Roslyn dependencies.
    internal static class SourceGeneratorNames {
        private const int MaxReversibleLength = 128;

        internal static string Encode(string value) {
            // Keep existing short names stable; H cannot collide with a hexadecimal prefix.
            if (value.Length > MaxReversibleLength) return "H" + Hash(value);
            var result = new System.Text.StringBuilder(value.Length * 4);
            foreach (var character in value) result.Append(((int)character).ToString("X4", System.Globalization.CultureInfo.InvariantCulture));
            return result.ToString();
        }

        internal static string Hash(string value) {
            using (var sha = System.Security.Cryptography.SHA256.Create()) {
                var bytes = sha.ComputeHash(System.Text.Encoding.UTF8.GetBytes(value));
                var result = new System.Text.StringBuilder(64);
                foreach (var b in bytes) result.Append(b.ToString("X2", System.Globalization.CultureInfo.InvariantCulture));
                return result.ToString();
            }
        }
    }
}
