namespace ME.BECS.CsvImporter {
    
    using scg = System.Collections.Generic;

    public static class CSVParser {

        #region CSV
        public static scg::List<string[]> ReadCSV(string text) {

            var iStart = 0;
            var csv = new scg::List<string[]>(1000);

            if (text.Contains("\r\n") == true) {
                text = text.Replace("\r\n", "\n");
            }

            var tmpList = new scg::List<string>(1000);
            while (iStart < text.Length) {

                var list = ParseCSVLine(tmpList, text, ref iStart);
                if (list == null) {
                    break;
                }

                csv.Add(list);

            }

            return csv;

        }

        private static string[] ParseCSVLine(scg::List<string> tmpList, string line, ref int iStart) {

            tmpList.Clear();

            var textLength = line.Length;
            var iWordStart = iStart;
            var insideQuote = false;

            while (iStart < textLength) {

                var c = line[iStart];

                if (insideQuote) {

                    if (c == '\"') { //--[ Look for Quote End ]------------

                        if (iStart + 1 >= textLength || line[iStart + 1] != '\"') { //-- Single Quote:  Quotation Ends

                            insideQuote = false;

                        } else if (iStart + 2 < textLength && line[iStart + 2] == '\"') { //-- Tripple Quotes: Quotation ends

                            insideQuote = false;
                            iStart += 2;

                        } else {

                            iStart++; // Skip Double Quotes

                        }

                    }

                } else { //-----[ Separators ]----------------------

                    if (c == ',') {

                        AddCSVtoken(tmpList, ref line, iStart, ref iWordStart);

                    } else if (c == '\n' || c == '\r') {

                        /*if (c == '\r' && iStart < line.Length - 1 && line[iStart + 1] == '\n') {
                            ++iStart;
                        }*/
                        break;

                    } else { //--------[ Start Quote ]--------------------

                        if (c == '\"') {

                            insideQuote = true;

                        }

                    }

                }

                iStart++;

            }

            AddCSVtoken(tmpList, ref line, iStart, ref iWordStart);

            if (iStart < textLength) {

                iStart++;

            }

            var arr = tmpList.ToArray();
            return arr;

        }

        private static char[] charsTemp = new char[] { ' ', '"' };

        private static void AddCSVtoken(scg::List<string> list, ref string line, int iEnd, ref int iWordStart) {

            var text = line.Substring(iWordStart, iEnd - iWordStart);
            iWordStart = iEnd + 1;

            text = text.Replace(@"""""", @"""").TrimFast();
            if (text.Length > 1 && text[0] == '"' && text[text.Length - 1] == '"') {

                text = text.Substring(1, text.Length - 2);

            }

            if (text.Contains(",") == true) {

                text = text.TrimFast().Trim(charsTemp);

            }

            list.Add(text);

        }
        #endregion

    }

    public static class StringExt {

        public static string[] SplitLines(this string str) {
            return str.Split(new string[] { "\r\n", "\n", "\r" }, System.StringSplitOptions.RemoveEmptyEntries);
        }

        public static string TrimFast(this string str) {

            if (str == null || str.Length == 0) {
                return str;
            }

            if (str[0] == ' ' || str[str.Length - 1] == ' ') {
                return str.Trim();
            }

            return str;

        }

    }

}