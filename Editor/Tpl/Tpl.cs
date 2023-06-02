namespace ME.BECS.Editor {
    
    using System.Text.RegularExpressions;

    public class Tpl {

        public string data;

        public Tpl(string data) {
            this.data = data;
        }

        public string GetString(System.Collections.Generic.Dictionary<string, int> counters, System.Collections.Generic.Dictionary<string, string> variables) {

            foreach (var kv in counters) {

                variables.TryAdd(kv.Key, kv.Value.ToString());

            }
            
            var txt = this.data;
            txt = Regex.Replace(txt, @"{{(\w+)}}", (match) => {
                
                var variable = match.Groups[1].Value;
                var val = string.Empty;
                if (variables.TryGetValue(variable, out var strVal) == true) {
                    val = strVal;
                }
                return val;

            });
            txt = Regex.Replace(txt, @"{(\w+)\[(.+?)\]}", (match) => {
                
                var variable = match.Groups[1].Value;
                var count = 0;
                if (counters.TryGetValue(variable, out var counterValue) == true) {
                    count = counterValue;
                }
                var content = match.Groups[2].Value;
                var str = string.Empty;
                for (int i = 0; i < count; ++i) {
                    str += content.Replace("#i#", i.ToString());
                }
                return str;

            });
            txt = Regex.Replace(txt, @"{(\w+)\((.+?)\)\[(.+?)\]}", (match) => {
                
                var variable = match.Groups[1].Value;
                var count = 0;
                if (counters.TryGetValue(variable, out var counterValue) == true) {
                    count = counterValue;
                }
                var sep = match.Groups[2].Value;
                var content = match.Groups[3].Value;
                var str = string.Empty;
                for (int i = 0; i < count; ++i) {
                    if (i > 0) str += sep;
                    str += content.Replace("#i#", i.ToString());
                }
                return str;

            });

            return txt;

        }

    }

}