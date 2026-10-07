using System;
using System.IO;
using System.Reflection;
using NUnit.Framework;

namespace ME.BECS.Tests {
    public class Tests_ILCacheTransport {
        private static Type Cache => Assembly.Load("ME.BECS.Editor").GetType("ME.BECS.Editor.ILPersistentAnalysis", true);
        private static object Call(string name, params object[] args) => Cache.GetMethod(name, BindingFlags.Static | BindingFlags.NonPublic).Invoke(null, args);
        [Serializable] private sealed class LegacyEnvelope { public string data; public string checksum; }

        [Test]
        public void RawEnvelopePreservesPayloadAndRejectsCorruption() {
            const string data = "{\"payload\":\"quotes \\\" and unicode Ж\"}\n";
            var writer = new StringWriter();
            Call("WriteCacheData", writer, data);
            var encoded = writer.ToString();
            Assert.AreEqual(data, Call("ReadCacheData", new StringReader(encoded)));
            StringAssert.EndsWith(data, encoded);
            var error = Assert.Throws<TargetInvocationException>(() => Call("ReadCacheData", new StringReader(encoded + "x")));
            Assert.IsInstanceOf<FormatException>(error.InnerException);
        }

        [Test]
        public void PreviousJsonEnvelopeRemainsReadable() {
            const string data = "{\"version\":2}";
            var names = Cache.Assembly.GetType("ME.BECS.CodeGeneration.SourceGeneratorNames", true);
            var hash = (string)names.GetMethod("Hash", BindingFlags.Static | BindingFlags.NonPublic).Invoke(null, new object[] { data });
            var encoded = UnityEngine.JsonUtility.ToJson(new LegacyEnvelope { data = data, checksum = hash });
            Assert.AreEqual(data, Call("ReadCacheData", new StringReader(encoded)));
        }
    }
}
