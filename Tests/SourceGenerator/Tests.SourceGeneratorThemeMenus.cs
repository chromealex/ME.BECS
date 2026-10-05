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
            var assembly = Assembly.Load("ME.BECS.Gen.Editor");
            var metadata = assembly.GetCustomAttributes(typeof(AssemblyMetadataAttribute), false).Cast<AssemblyMetadataAttribute>().ToArray();
            var plan = metadata.Single(item => item.Key == "ME.BECS.ThemeMenuInputs.v1").Value.Split('\n');
            var inputs = metadata.Where(item => item.Key == "ME.BECS.TypeInput.v1").Select(item => item.Value.Split('\t'))
                .Where(row => row[0] == "editor").ToArray();
            string Decode(string value) => Encoding.UTF8.GetString(Convert.FromBase64String(value));
            var schema = Decode(inputs.Single(row => row[1] == "theme-menu-schema")[3]).Split('\n');
            var themes = inputs.Where(row => row[1] == "theme-menu").Select(row => Decode(row[3]).Split('\n')).ToArray();
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
            var assembly = Assembly.Load("ME.BECS.Gen.Runtime");
            Assert.IsNull(assembly.GetType("ME.BECS.Editor.ThemesMenu"));
            var metadata = assembly.GetCustomAttributes(typeof(AssemblyMetadataAttribute), false).Cast<AssemblyMetadataAttribute>().ToArray();
            Assert.IsFalse(metadata.Any(item => item.Key == "ME.BECS.ThemeMenuInputs.v1"));
            Assert.IsFalse(metadata.Where(item => item.Key == "ME.BECS.TypeInput.v1").Select(item => item.Value.Split('\t'))
                .Any(row => row.Length > 1 && (row[1] == "theme-menu-schema" || row[1] == "theme-menu")));
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
