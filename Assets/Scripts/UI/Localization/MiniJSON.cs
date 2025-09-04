// Assets/Scripts/UI/Localization/MiniJson.cs
// MIT: https://gist.github.com/darktable/1411710  (versión recortada para objetos planos)
using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.Text;

namespace MiniJSON
{
    public static class Json
    {
        public static object Deserialize(string json)
        {
            if (json == null) return null;
            return Parser.Parse(json);
        }

        sealed class Parser : IDisposable
        {
            const string WORD_BREAK = "{}[],:\"";
            StringReader json;

            Parser(string jsonString) { json = new StringReader(jsonString); }
            public static object Parse(string jsonString)
            {
                using (var instance = new Parser(jsonString)) { return instance.ParseValue(); }
            }

            public void Dispose() { json.Dispose(); }

            Dictionary<string, object> ParseObject()
            {
                var table = new Dictionary<string, object>(StringComparer.OrdinalIgnoreCase);
                json.Read(); // {
                while (true)
                {
                    switch (NextToken)
                    {
                        case TOKEN.NONE: return null;
                        case TOKEN.CURLY_CLOSE: return table;
                        default:
                            var name = ParseString();
                            if (name == null) return null;
                            if (NextToken != TOKEN.COLON) return null;
                            json.Read();
                            table[name] = ParseValue();
                            switch (NextToken)
                            {
                                case TOKEN.COMMA: json.Read(); continue;
                                case TOKEN.CURLY_CLOSE: return table;
                                default: return null;
                            }
                    }
                }
            }

            List<object> ParseArray()
            {
                var array = new List<object>();
                json.Read(); // [
                var parsing = true;
                while (parsing)
                {
                    var token = NextToken;
                    switch (token)
                    {
                        case TOKEN.NONE: return null;
                        case TOKEN.SQUARE_CLOSE: parsing = false; break;
                        case TOKEN.COMMA: continue;
                        default: array.Add(ParseValue()); break;
                    }
                }
                return array;
            }

            object ParseValue()
            {
                switch (NextToken)
                {
                    case TOKEN.STRING: return ParseString();
                    case TOKEN.NUMBER: return ParseNumber();
                    case TOKEN.CURLY_OPEN: return ParseObject();
                    case TOKEN.SQUARE_OPEN: return ParseArray();
                    case TOKEN.TRUE: return true;
                    case TOKEN.FALSE: return false;
                    case TOKEN.NULL: return null;
                }
                return null;
            }

            string ParseString()
            {
                var s = new StringBuilder();
                json.Read(); // "
                bool parsing = true;
                while (parsing)
                {
                    if (json.Peek() == -1) break;
                    var c = (char)json.Read();
                    switch (c)
                    {
                        case '"': parsing = false; break;
                        case '\\':
                            if (json.Peek() == -1) { parsing = false; break; }
                            c = (char)json.Read();
                            switch (c)
                            {
                                case '"': case '\\': case '/': s.Append(c); break;
                                case 'b': s.Append('\b'); break;
                                case 'f': s.Append('\f'); break;
                                case 'n': s.Append('\n'); break;
                                case 'r': s.Append('\r'); break;
                                case 't': s.Append('\t'); break;
                                case 'u':
                                    var hex = new char[4];
                                    for (int i = 0; i < 4; i++) hex[i] = (char)json.Read();
                                    s.Append((char)Convert.ToInt32(new string(hex), 16));
                                    break;
                            }
                            break;
                        default: s.Append(c); break;
                    }
                }
                return s.ToString();
            }

            object ParseNumber()
            {
                var number = NextWord;
                if (number.IndexOf('.') == -1)
                {
                    long parsedInt;
                    long.TryParse(number, NumberStyles.Any, CultureInfo.InvariantCulture, out parsedInt);
                    return parsedInt;
                }
                double parsedDouble;
                double.TryParse(number, NumberStyles.Any, CultureInfo.InvariantCulture, out parsedDouble);
                return parsedDouble;
            }

            void EatWhitespace()
            {
                while (char.IsWhiteSpace(PeekChar)) json.Read();
            }

            char PeekChar { get { int peek = json.Peek(); return peek == -1 ? '\0' : (char)peek; } }

            string NextWord
            {
                get
                {
                    var sb = new StringBuilder();
                    while (!IsWordBreak(PeekChar)) { sb.Append((char)json.Read()); if (json.Peek() == -1) break; }
                    return sb.ToString();
                }
            }

            TOKEN NextToken
            {
                get
                {
                    EatWhitespace();
                    if (json.Peek() == -1) return TOKEN.NONE;
                    switch (PeekChar)
                    {
                        case '{': return TOKEN.CURLY_OPEN;
                        case '}': return TOKEN.CURLY_CLOSE;
                        case '[': return TOKEN.SQUARE_OPEN;
                        case ']': return TOKEN.SQUARE_CLOSE;
                        case ',': return TOKEN.COMMA;
                        case '"': return TOKEN.STRING;
                        case ':': return TOKEN.COLON;
                        case '0':
                        case '1':
                        case '2':
                        case '3':
                        case '4':
                        case '5':
                        case '6':
                        case '7':
                        case '8':
                        case '9':
                        case '-': return TOKEN.NUMBER;
                    }
                    var word = NextWord;
                    switch (word)
                    {
                        case "false": return TOKEN.FALSE;
                        case "true": return TOKEN.TRUE;
                        case "null": return TOKEN.NULL;
                    }
                    return TOKEN.NONE;
                }
            }

            static bool IsWordBreak(char c) { return char.IsWhiteSpace(c) || WORD_BREAK.IndexOf(c) != -1 || c == '\0'; }

            enum TOKEN { NONE, CURLY_OPEN, CURLY_CLOSE, SQUARE_OPEN, SQUARE_CLOSE, COLON, COMMA, STRING, NUMBER, TRUE, FALSE, NULL }
            sealed class StringReader : System.IO.StringReader { public StringReader(string s) : base(s) { } }
        }
    }
}
