using System;
using System.Collections.Generic;
using System.Reflection;
using System.Text.RegularExpressions;
using System.Xml;
using UnityEditor;

namespace ME.BECS.Editor {

    /// <summary>
    /// Defines field tooltip state and operations.
    /// </summary>
    [InitializeOnLoad]
    public static class FieldTooltip {
        private static readonly Dictionary<MemberInfo, string> summaries = new();
        private static readonly Dictionary<string, string> sources = new();
        private static readonly Regex commentsAndStrings = new(@"//[^\r\n]*|/\*[\s\S]*?\*/|@""(?:""""|[^""])*""|""(?:\\.|[^""\\])*""|'(?:\\.|[^'\\])*'", RegexOptions.Compiled);
        private static readonly Regex scopes = new(@"\b(namespace|class|struct|interface|record)\s+(@?\w+(?:\.\w+)*)|[{};]", RegexOptions.Compiled);
        private static readonly Regex docs = new(@"(?m)^[ \t]*///[^\r\n]*(?:\r?\n[ \t]*///[^\r\n]*)*", RegexOptions.Compiled);

        static FieldTooltip() {
            EditorApplication.projectChanged += () => { summaries.Clear(); sources.Clear(); };
        }

        /// <summary>
        /// Returns the requested entry from field tooltip.
        /// </summary>
        public static string Get(FieldInfo field, string serializedTooltip = null) {
            if (field == null) return serializedTooltip;
            var becs = (ME.BECS.TooltipAttribute)Attribute.GetCustomAttribute(field, typeof(ME.BECS.TooltipAttribute));
            var unity = (UnityEngine.TooltipAttribute)Attribute.GetCustomAttribute(field, typeof(UnityEngine.TooltipAttribute));
            if (becs != null || unity != null) {
                return string.IsNullOrEmpty(becs?.text) == false ? becs.text : unity?.tooltip;
            }
            if (string.IsNullOrEmpty(serializedTooltip) == false) return serializedTooltip;
            if (summaries.TryGetValue(field, out var cached) == true) return cached;
            var result = ReadSummary(field.DeclaringType, field.Name);
            summaries[field] = result;
            return result;
        }

        /// <summary>
        /// Returns the requested entry from field tooltip.
        /// </summary>
        public static string Get(Type type) {
            if (type == null) return null;
            var becs = (ME.BECS.TooltipAttribute)Attribute.GetCustomAttribute(type, typeof(ME.BECS.TooltipAttribute));
            var unity = (UnityEngine.TooltipAttribute)Attribute.GetCustomAttribute(type, typeof(UnityEngine.TooltipAttribute));
            if (becs != null || unity != null) return string.IsNullOrEmpty(becs?.text) == false ? becs.text : unity?.tooltip;
            var comment = (ME.BECS.EditorCommentAttribute)Attribute.GetCustomAttribute(type, typeof(ME.BECS.EditorCommentAttribute));
            if (string.IsNullOrEmpty(comment?.comment) == false) return comment.comment;
            if (summaries.TryGetValue(type, out var cached) == true) return cached;
            var result = ReadSummary(type, null);
            summaries[type] = result;
            return result;
        }

        private static string ReadSummary(Type type, string fieldName) {
            if (type == null) return null;
            foreach (var assembly in UnityEditor.Compilation.CompilationPipeline.GetAssemblies()) {
                if (assembly.name != type.Assembly.GetName().Name) continue;
                foreach (var path in assembly.sourceFiles) {
                    // Generated sources are not an editable documentation source.
                    if (path.Replace('\\', '/').Contains("/ME.BECS.Gen/") == true) continue;
                    if (sources.TryGetValue(path, out var source) == false) {
                        try { source = System.IO.File.ReadAllText(path); }
                        catch (System.IO.IOException) { continue; }
                        sources[path] = source;
                    }
                    if (source.Contains(fieldName ?? type.Name) == false || source.Contains("<summary>") == false) continue;
                    var result = ParseSummary(source, type, fieldName);
                    if (string.IsNullOrEmpty(result) == false) return result;
                }
            }
            return null;
        }

        internal static string ParseSummary(string source, Type type, string fieldName) {
            // Keep offsets intact while excluding braces in comments and literals.
            var code = commentsAndStrings.Replace(source, match => new string(' ', match.Length));
            foreach (Match doc in docs.Matches(source)) {
                var start = doc.Index + doc.Length;
                if (fieldName == null) {
                    var body = code.IndexOf('{', start);
                    if (body < 0) continue;
                    var declaration = Regex.Replace(code.Substring(start, body - start), @"\[[^\]]*\]", " ");
                    if (Regex.IsMatch(declaration, @"^\s*(?:(?:public|internal|private|protected|readonly|partial|unsafe|sealed|abstract|static)\s+)*(?:struct|class|record)\s+", RegexOptions.Singleline) == false) continue;
                    if (MatchesType(code.Substring(0, start) + declaration + "{", type) == false) continue;
                } else {
                    var end = code.IndexOf(';', start);
                    if (end < 0) continue;
                    var declaration = code.Substring(start, end - start + 1);
                    declaration = Regex.Replace(declaration, @"\[[^\]]*\]", " ");
                    if (Regex.IsMatch(declaration, @"\b" + Regex.Escape(fieldName) + @"\s*(?:=|;|,)") == false) continue;
                    // Do not cross a method or type body to find a same-named field.
                    var equals = declaration.IndexOf('=');
                    var signature = equals < 0 ? declaration : declaration.Substring(0, equals);
                    if (signature.IndexOfAny(new[] { '{', '}', '(', ')' }) >= 0) continue;
                    if (Regex.IsMatch(signature, @"\b" + Regex.Escape(fieldName) + @"\s*(?:$|;|,)") == false) continue;
                    if (MatchesType(code.Substring(0, doc.Index), type) == false) continue;
                }
                var xml = Regex.Replace(doc.Value, @"(?m)^[ \t]*/// ?", "");
                try {
                    var document = new XmlDocument { XmlResolver = null };
                    document.LoadXml("<doc>" + xml + "</doc>");
                    var summary = document.SelectSingleNode("/doc/summary");
                    if (summary == null) continue;
                    foreach (XmlNode reference in summary.SelectNodes(".//see|.//seealso|.//paramref|.//typeparamref")) {
                        var text = reference.Attributes?["langword"]?.Value ?? reference.Attributes?["name"]?.Value ?? reference.Attributes?["cref"]?.Value;
                        if (string.IsNullOrEmpty(text) == false) reference.InnerText = Regex.Replace(text, @"^[A-Z]:", "");
                    }
                    return Regex.Replace(summary.InnerText, @"\s+", " ").Trim();
                } catch (XmlException) {
                    // Incomplete documentation should never break the inspector.
                }
            }
            return null;
        }

        private static bool MatchesType(string prefix, Type type) {
            var stack = new System.Collections.Generic.List<string>();
            string pending = null;
            var fileNamespace = string.Empty;
            foreach (Match token in scopes.Matches(prefix)) {
                if (token.Groups[1].Success == true) {
                    pending = token.Groups[2].Value.TrimStart('@');
                } else if (token.Value == "{") {
                    stack.Add(pending);
                    pending = null;
                } else if (token.Value == "}") {
                    if (stack.Count > 0) stack.RemoveAt(stack.Count - 1);
                    pending = null;
                } else if (pending != null) {
                    fileNamespace = pending;
                    pending = null;
                }
            }
            if (stack.Count == 0 || stack[stack.Count - 1] == null) return false;
            var names = new System.Collections.Generic.List<string>();
            if (string.IsNullOrEmpty(fileNamespace) == false) names.Add(fileNamespace);
            foreach (var name in stack) {
                if (name != null) names.Add(name);
            }
            var expected = Regex.Replace(type.FullName ?? type.Name, @"`\d+", "").Replace('+', '.');
            return string.Join(".", names) == expected;
        }
    }
}
