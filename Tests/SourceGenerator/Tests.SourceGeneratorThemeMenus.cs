using System;
using System.Globalization;
using System.Linq;
using System.Reflection;
using System.Text;
using NUnit.Framework;

namespace ME.BECS.Tests {
    public class Tests_SourceGeneratorThemeMenus {
        [Test]
        public void EditorMenuMatchesCompilerPlanWithoutChangingPreferences() {
            var assembly = Assembly.Load("ME.BECS.Editor");
            var metadata = assembly.GetCustomAttributes(typeof(AssemblyMetadataAttribute), false).Cast<AssemblyMetadataAttribute>().ToArray();
            var plan = metadata.Single(item => item.Key == "ME.BECS.ThemeMenuInputs.v1").Value.Split('\n');
            var inputs = Tests_SourceGeneratorInputCatalog.Rows(true).Select(row => row.Split('\t')).ToArray();
            string Decode(string value) => Encoding.UTF8.GetString(Convert.FromBase64String(value));
            var schema = Decode(inputs.Single(row => row[0] == "theme-menu-schema")[2]).Split('\n');
            var themes = inputs.Where(row => row[0] == "theme-menu").OrderBy(row => int.Parse(row[1], CultureInfo.InvariantCulture))
                .Select(row => Decode(row[2]).Split('\n')).ToArray();
            Assert.AreEqual(assembly.GetName().Name, Decode(inputs.Single(row => row[0] == "thememenu-registration-owner")[3]));
            Assert.AreEqual(1, metadata.Count(item => item.Key == "ME.BECS.ThemeMenuFragment.v1"));
            Assert.AreEqual("v1", schema[0]);
            CollectionAssert.AreEqual(schema, plan.Take(3));
            Assert.AreEqual(themes.Length.ToString(CultureInfo.InvariantCulture), schema[1]);
            Assert.AreEqual(themes.Length + 3, plan.Length);
            var builtIns = int.Parse(schema[2], CultureInfo.InvariantCulture);
            Assert.That(builtIns, Is.InRange(1, themes.Length));
            var names = themes.Select(theme => theme[0]).ToArray();
            Assert.AreEqual(names.Length, names.Distinct(StringComparer.Ordinal).Count());
            CollectionAssert.AreEqual(names.Skip(builtIns).OrderBy(name => name, StringComparer.Ordinal), names.Skip(builtIns));

            var owner = assembly.GetType("ME.BECS.Editor.ThemesMenu", true);
            var methods = owner.GetMethods(BindingFlags.Static | BindingFlags.NonPublic | BindingFlags.DeclaredOnly);
            var menuItems = methods.SelectMany(method => method.GetCustomAttributesData()
                .Where(attribute => attribute.AttributeType.FullName == "UnityEditor.MenuItem")
                .Select(attribute => (method, values: attribute.ConstructorArguments.Select(argument => argument.Value).ToArray()))).ToArray();
            Assert.AreEqual(themes.Length + 1, menuItems.Length);
            Assert.IsTrue(menuItems.All(item => item.values.Length == 3 && item.method.GetParameters().Length == 0));
            var validation = menuItems.Single(item => (bool)item.values[1]);
            Assert.AreEqual("ME.BECS/Themes/" + themes[0][0], validation.values[0]);
            Assert.AreEqual(typeof(bool), validation.method.ReturnType);
            for (var index = 0; index < themes.Length; ++index) {
                var priority = 200 + index + (index < builtIns ? 0 : 10);
                Assert.AreEqual(index.ToString(CultureInfo.InvariantCulture) + "\t" + priority.ToString(CultureInfo.InvariantCulture) +
                    "\t" + themes[index][0] + "\t" + themes[index][1], plan[index + 3]);
                var menu = menuItems.Single(item => !(bool)item.values[1] && (string)item.values[0] == "ME.BECS/Themes/" + themes[index][0]);
                Assert.AreEqual(priority, menu.values[2]);
                Assert.AreEqual(typeof(void), menu.method.ReturnType);
                Assert.Less(menu.method.Name.Length, 32, "Stylesheet names must not become unbounded metadata identifiers.");
            }
        }

        [Test]
        public void RuntimeHasNoThemeMenuOrEditorThemeInputs() {
            var assembly = Tests_SourceGeneratorInputCatalog.Owner(false);
            Assert.IsNull(assembly.GetType("ME.BECS.Editor.ThemesMenu"));
            var metadata = assembly.GetCustomAttributes(typeof(AssemblyMetadataAttribute), false).Cast<AssemblyMetadataAttribute>().ToArray();
            Assert.IsFalse(metadata.Any(item => item.Key == "ME.BECS.ThemeMenuInputs.v1"));
            Assert.IsFalse(Tests_SourceGeneratorInputCatalog.Rows(false).Select(row => row.Split('\t'))
                .Any(row => row[0] == "theme-menu-schema" || row[0] == "theme-menu" || row[0] == "thememenu-registration-owner"));
        }

        [Test]
        public void ThemeFragmentRoundTripsAndRetiresWithoutRuntimePublication() {
            const BindingFlags flags = BindingFlags.Static | BindingFlags.NonPublic;
            var format = Assembly.Load("ME.BECS.Editor").GetType("ME.BECS.CodeGeneration.SourceGeneratorThemeMenuFragmentFormat", true);
            object Call(string name, params object[] args) => format.GetMethod(name, flags).Invoke(null, args);
            string Encode(string value) => Convert.ToBase64String(Encoding.UTF8.GetBytes(value));
            var rows = new[] { "theme-menu-schema\t0\t" + Encode("v1\n1\n1"), "theme-menu\t0\t" + Encode("Default\nDefaultStyle") };
            var entry = (string)Call("EntryValue", (object)rows);
            var selection = rows.Concat(new[] { "thememenu-publication-schema\t0\tdjE=",
                "thememenu-registration-owner\t0\t" + Encode(entry) + "\t" + Encode("Example.Editor") }).ToArray();
            var documents = (Array)Call("Documents", selection, true);
            Assert.AreEqual(1, documents.Length);
            var document = documents.GetValue(0);
            var content = (string)Call("Serialize", document);
            Assert.IsTrue((bool)Call("TryParse", content, null));
            Assert.IsFalse((bool)Call("TryParse", content + "corrupt", null));
            CollectionAssert.AreEqual(rows, (string[])Call("Rows", entry));
            var entries = document.GetType().GetField("Entries", BindingFlags.Instance | BindingFlags.NonPublic);
            entries.SetValue(document, Array.CreateInstance(entries.FieldType.GetElementType(), 0));
            Assert.IsTrue((bool)Call("TryParse", Call("Serialize", document), null), "Retired publications must remain readable.");
            Assert.IsEmpty((Array)Call("Documents", Array.Empty<string>(), false));
            var error = Assert.Throws<TargetInvocationException>(() => Call("Documents", selection, false));
            Assert.IsInstanceOf<InvalidOperationException>(error.InnerException);
        }

        [TestCase(false)]
        [TestCase(true)]
        public void ThemeFeederRetiresLegacyBodyWithoutWritingFiles(bool editor) {
            var type = Assembly.Load("ME.BECS.Editor").GetType("ME.BECS.Editor.ThemesCodeGenerator", true);
            var feeder = Activator.CreateInstance(type);
            type.GetField("editorAssembly").SetValue(feeder, editor);
            var references = new System.Collections.Generic.List<Type>();
            var files = ((System.Collections.Generic.IEnumerable<string>)type.GetMethod("GetRetiredSourceFiles").Invoke(feeder, null)).ToArray();
            type.GetMethod("AddSourceGeneratorReferences").Invoke(feeder, new object[] { references });
            Assert.AreEqual(editor ? 1 : 0, files.Length);
            if (editor) {
                Assert.AreEqual("MenuThemes", files[0]);
                Assert.AreEqual("ME.BECS.Editor.Themes", references.Single().FullName);
            } else {
                Assert.IsEmpty(references);
                var input = new StringBuilder();
                type.GetMethod("AppendSourceGeneratorInputs").Invoke(feeder, new object[] { input });
                Assert.AreEqual("", input.ToString());
            }
            Assert.AreEqual("none", type.GetProperty("SourceInitializationKind").GetValue(feeder));
            Assert.AreEqual("none", type.GetProperty("SourceRegistrationKind").GetValue(feeder));
        }
    }
}
