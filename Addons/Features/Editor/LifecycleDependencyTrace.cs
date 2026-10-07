namespace ME.BECS.Editor.Systems {
    using System;
    using System.Globalization;
    using System.Linq;
    using System.Security.Cryptography;
    using System.Text;
    using scg = System.Collections.Generic;

    // Symbolic handles only. No jobs, allocations in State, or lifecycle methods are executed.
    public sealed class LifecycleDependencyTrace {
        private readonly scg.Dictionary<string, string> values = new(StringComparer.Ordinal);
        private readonly scg.Dictionary<string, string[]> joins = new(StringComparer.Ordinal);
        internal readonly scg.List<string> Events = new();
        internal string Result => this.Read("dependsOn");
        public LifecycleDependencyTrace() => this.Reset();
        internal void Reset() { this.values.Clear(); this.joins.Clear(); this.Events.Clear(); this.values.Add("dependsOn", "input"); }
        private string Read(string name) => this.values.TryGetValue(name, out var value) ? value :
            throw new InvalidOperationException("Uninitialized symbolic dependency: " + name);
        internal void Copy(string output, string input) => this.values[output] = this.Read(input);
        internal void Merge(string output, scg.IEnumerable<string> inputs) {
            var values = inputs.Select(this.Read).SelectMany(value => this.joins.TryGetValue(value, out var leaves) ? leaves : new[] { value })
                .Where(value => value != "default").Distinct().OrderBy(value => value, StringComparer.Ordinal).ToArray();
            var joined = values.Length == 0 ? "default" : values.Length == 1 ? values[0] : Hash("join\n" + string.Join("\n", values));
            if (values.Length > 1) this.joins[joined] = values;
            this.values[output] = joined;
        }
        internal void Apply(string output, string input) => this.Operation(output, input, "apply");
        internal void Invoke(string output, string input, int slot) => this.Operation(output, input, "invoke:" + slot.ToString(CultureInfo.InvariantCulture));
        private void Operation(string output, string input, string operation) {
            var value = this.Read(input);
            var descriptor = this.Events.Count.ToString(CultureInfo.InvariantCulture) + "\t" + operation + "\t" + value;
            var result = Hash(descriptor);
            this.Events.Add(descriptor);
            this.values[output] = result;
        }
        internal void Generic(string output, string input, int first, int count, bool parallel, bool apply) {
            this.Copy("$genericInput", input);
            this.Copy("$genericPrevious", input);
            var results = new scg.List<string>();
            for (var index = 0; index < count; ++index) {
                var name = "$generic" + index.ToString(CultureInfo.InvariantCulture);
                this.Invoke(name, parallel ? "$genericInput" : "$genericPrevious", checked(first + index));
                if (!parallel && apply) this.Apply(name, name);
                if (!parallel) this.Copy("$genericPrevious", name);
                results.Add(name);
            }
            if (count == 0) this.Copy(output, "$genericInput");
            else if (parallel) this.Merge(output, results);
            else this.Copy(output, "$genericPrevious");
        }
        internal static LifecycleDependencyTrace FromPlan(string[] rows) {
            var trace = new LifecycleDependencyTrace();
            trace.Copy("$input", "dependsOn");
            var next = 0;
            var ended = false;
            for (var line = 3; line < rows.Length; ++line) {
                if (rows[line].Length == 0 && line == rows.Length - 1) continue;
                if (ended) throw new FormatException("Trailing plan data");
                var fields = rows[line].Split('\t');
                if (fields.Length == 2 && fields[0] == "result") {
                    var result = Integer(fields[1], -1);
                    if (result >= next) throw new FormatException("Invalid result handle");
                    trace.Copy("dependsOn", result == -1 ? "$input" : "$step" + result.ToString(CultureInfo.InvariantCulture));
                    ended = true;
                    continue;
                }
                if (fields.Length != 11 || Integer(fields[0], 0) != next) throw new FormatException("Invalid step ordinal");
                var dependencies = new scg.List<string>();
                foreach (var item in fields[3].Split(new[] { ',' }, StringSplitOptions.RemoveEmptyEntries)) {
                    var dependency = Integer(item, -1);
                    if (dependency >= next) throw new FormatException("Forward dependency");
                    dependencies.Add(dependency == -1 ? "$input" : "$step" + dependency.ToString(CultureInfo.InvariantCulture));
                }
                var output = "$step" + (next++).ToString(CultureInfo.InvariantCulture);
                trace.Merge(output, dependencies);
                if (Flag(fields[8])) trace.Apply(output, output);
                if (fields[4] == "invoke") {
                    var first = Integer(fields[5], 0);
                    var count = Integer(fields[6], 0);
                    if (fields[7] == "ordinary") {
                        if (count != 1) throw new FormatException("Invalid ordinary slot count");
                        trace.Invoke(output, output, first);
                    } else if (fields[7] == "parallel" || fields[7] == "sequential")
                        trace.Generic(output, output, first, count, fields[7] == "parallel", Flag(fields[9]));
                    else throw new FormatException("Invalid generic mode");
                } else if (fields[4] != "pass") throw new FormatException("Invalid step operation");
                if (Flag(fields[9])) trace.Apply(output, output);
            }
            if (!ended) throw new FormatException("Missing plan result");
            return trace;
        }
        private static bool Flag(string value) => value == "1" ? true : value == "0" ? false : throw new FormatException("Invalid flag");
        private static int Integer(string value, int minimum) {
            if (!int.TryParse(value, NumberStyles.AllowLeadingSign, CultureInfo.InvariantCulture, out var result) || result < minimum ||
                result.ToString(CultureInfo.InvariantCulture) != value) throw new FormatException("Invalid integer");
            return result;
        }
        private static string Hash(string value) {
            using var hash = SHA256.Create();
            return BitConverter.ToString(hash.ComputeHash(Encoding.UTF8.GetBytes(value))).Replace("-", "");
        }
    }
}
