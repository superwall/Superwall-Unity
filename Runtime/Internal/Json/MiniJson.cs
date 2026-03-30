using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;

namespace Superwall.Internal
{
    /// <summary>
    /// Lightweight JSON parser and serializer for Unity.
    /// Parses JSON into Dictionary&lt;string, object&gt;, List&lt;object&gt;, and primitives.
    /// </summary>
    public static class Json
    {
        public static object Deserialize(string json)
        {
            if (json == null)
                return null;

            return Parser.Parse(json);
        }

        public static string Serialize(object obj)
        {
            return Serializer.Serialize(obj);
        }

        sealed class Parser : IDisposable
        {
            StringReader _reader;

            Parser(string jsonString)
            {
                _reader = new StringReader(jsonString);
            }

            public static object Parse(string jsonString)
            {
                using (var parser = new Parser(jsonString))
                {
                    return parser.ParseValue();
                }
            }

            public void Dispose()
            {
                _reader.Dispose();
            }

            object ParseValue()
            {
                SkipWhitespace();
                int peek = _reader.Peek();
                if (peek == -1)
                    return null;

                char c = (char)peek;
                switch (c)
                {
                    case '{':
                        return ParseObject();
                    case '[':
                        return ParseArray();
                    case '"':
                        return ParseString();
                    case '-':
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
                        return ParseNumber();
                    default:
                        return ParseLiteral();
                }
            }

            Dictionary<string, object> ParseObject()
            {
                var dict = new Dictionary<string, object>();

                // consume '{'
                _reader.Read();

                while (true)
                {
                    SkipWhitespace();
                    int peek = _reader.Peek();
                    if (peek == -1)
                        break;

                    if ((char)peek == '}')
                    {
                        _reader.Read();
                        break;
                    }

                    if ((char)peek == ',')
                    {
                        _reader.Read();
                        continue;
                    }

                    // key
                    string key = ParseString();

                    // colon
                    SkipWhitespace();
                    if (_reader.Peek() == ':')
                        _reader.Read();

                    // value
                    object value = ParseValue();
                    dict[key] = value;
                }

                return dict;
            }

            List<object> ParseArray()
            {
                var list = new List<object>();

                // consume '['
                _reader.Read();

                while (true)
                {
                    SkipWhitespace();
                    int peek = _reader.Peek();
                    if (peek == -1)
                        break;

                    if ((char)peek == ']')
                    {
                        _reader.Read();
                        break;
                    }

                    if ((char)peek == ',')
                    {
                        _reader.Read();
                        continue;
                    }

                    list.Add(ParseValue());
                }

                return list;
            }

            string ParseString()
            {
                var sb = new StringBuilder();

                // consume opening '"'
                _reader.Read();

                while (true)
                {
                    int c = _reader.Read();
                    if (c == -1 || c == '"')
                        break;

                    if (c == '\\')
                    {
                        int next = _reader.Read();
                        if (next == -1)
                            break;

                        switch ((char)next)
                        {
                            case '"':  sb.Append('"');  break;
                            case '\\': sb.Append('\\'); break;
                            case '/':  sb.Append('/');  break;
                            case 'b':  sb.Append('\b'); break;
                            case 'f':  sb.Append('\f'); break;
                            case 'n':  sb.Append('\n'); break;
                            case 'r':  sb.Append('\r'); break;
                            case 't':  sb.Append('\t'); break;
                            case 'u':
                                var hex = new char[4];
                                for (int i = 0; i < 4; i++)
                                {
                                    int h = _reader.Read();
                                    if (h == -1) break;
                                    hex[i] = (char)h;
                                }
                                ushort codePoint = Convert.ToUInt16(new string(hex), 16);
                                sb.Append((char)codePoint);
                                break;
                            default:
                                sb.Append('\\');
                                sb.Append((char)next);
                                break;
                        }
                    }
                    else
                    {
                        sb.Append((char)c);
                    }
                }

                return sb.ToString();
            }

            object ParseNumber()
            {
                var sb = new StringBuilder();
                bool isFloat = false;

                while (true)
                {
                    int peek = _reader.Peek();
                    if (peek == -1)
                        break;

                    char c = (char)peek;
                    if (c == '.' || c == 'e' || c == 'E')
                        isFloat = true;

                    if ((c >= '0' && c <= '9') || c == '-' || c == '+' || c == '.' || c == 'e' || c == 'E')
                    {
                        sb.Append(c);
                        _reader.Read();
                    }
                    else
                    {
                        break;
                    }
                }

                string numStr = sb.ToString();

                if (isFloat)
                {
                    double d;
                    if (double.TryParse(numStr, NumberStyles.Float, CultureInfo.InvariantCulture, out d))
                        return d;
                }
                else
                {
                    int i;
                    if (int.TryParse(numStr, NumberStyles.Integer, CultureInfo.InvariantCulture, out i))
                        return i;

                    long l;
                    if (long.TryParse(numStr, NumberStyles.Integer, CultureInfo.InvariantCulture, out l))
                        return l;

                    double d;
                    if (double.TryParse(numStr, NumberStyles.Float, CultureInfo.InvariantCulture, out d))
                        return d;
                }

                return 0;
            }

            object ParseLiteral()
            {
                var sb = new StringBuilder();

                while (true)
                {
                    int peek = _reader.Peek();
                    if (peek == -1)
                        break;

                    char c = (char)peek;
                    if ((c >= 'a' && c <= 'z') || (c >= 'A' && c <= 'Z'))
                    {
                        sb.Append(c);
                        _reader.Read();
                    }
                    else
                    {
                        break;
                    }
                }

                string word = sb.ToString();
                switch (word)
                {
                    case "true":  return true;
                    case "false": return false;
                    case "null":  return null;
                    default:      return null;
                }
            }

            void SkipWhitespace()
            {
                while (true)
                {
                    int peek = _reader.Peek();
                    if (peek == -1)
                        return;

                    char c = (char)peek;
                    if (c == ' ' || c == '\t' || c == '\n' || c == '\r')
                        _reader.Read();
                    else
                        return;
                }
            }
        }

        sealed class Serializer
        {
            StringBuilder _sb;

            Serializer()
            {
                _sb = new StringBuilder();
            }

            public static string Serialize(object obj)
            {
                var s = new Serializer();
                s.SerializeValue(obj);
                return s._sb.ToString();
            }

            void SerializeValue(object value)
            {
                if (value == null)
                {
                    _sb.Append("null");
                }
                else if (value is string str)
                {
                    SerializeString(str);
                }
                else if (value is bool b)
                {
                    _sb.Append(b ? "true" : "false");
                }
                else if (value is IDictionary dict)
                {
                    SerializeDictionary(dict);
                }
                else if (value is IList list)
                {
                    SerializeArray(list);
                }
                else if (value is char c)
                {
                    SerializeString(c.ToString());
                }
                else if (IsNumeric(value))
                {
                    SerializeNumber(value);
                }
                else
                {
                    SerializeString(value.ToString());
                }
            }

            void SerializeDictionary(IDictionary dict)
            {
                _sb.Append('{');
                bool first = true;

                foreach (DictionaryEntry entry in dict)
                {
                    if (!first)
                        _sb.Append(',');

                    SerializeString(entry.Key.ToString());
                    _sb.Append(':');
                    SerializeValue(entry.Value);

                    first = false;
                }

                _sb.Append('}');
            }

            void SerializeArray(IList list)
            {
                _sb.Append('[');
                bool first = true;

                foreach (object item in list)
                {
                    if (!first)
                        _sb.Append(',');

                    SerializeValue(item);
                    first = false;
                }

                _sb.Append(']');
            }

            void SerializeString(string str)
            {
                _sb.Append('"');

                foreach (char c in str)
                {
                    switch (c)
                    {
                        case '"':  _sb.Append("\\\""); break;
                        case '\\': _sb.Append("\\\\"); break;
                        case '\b': _sb.Append("\\b");  break;
                        case '\f': _sb.Append("\\f");  break;
                        case '\n': _sb.Append("\\n");  break;
                        case '\r': _sb.Append("\\r");  break;
                        case '\t': _sb.Append("\\t");  break;
                        default:
                            if (c < ' ')
                            {
                                _sb.Append("\\u");
                                _sb.Append(((int)c).ToString("x4"));
                            }
                            else
                            {
                                _sb.Append(c);
                            }
                            break;
                    }
                }

                _sb.Append('"');
            }

            void SerializeNumber(object value)
            {
                if (value is float f)
                    _sb.Append(f.ToString("R", CultureInfo.InvariantCulture));
                else if (value is double d)
                    _sb.Append(d.ToString("R", CultureInfo.InvariantCulture));
                else if (value is decimal dec)
                    _sb.Append(dec.ToString(CultureInfo.InvariantCulture));
                else
                    _sb.Append(Convert.ToInt64(value).ToString(CultureInfo.InvariantCulture));
            }

            static bool IsNumeric(object value)
            {
                return value is int
                    || value is long
                    || value is float
                    || value is double
                    || value is decimal
                    || value is byte
                    || value is sbyte
                    || value is short
                    || value is ushort
                    || value is uint
                    || value is ulong;
            }
        }
    }
}
