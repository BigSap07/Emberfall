using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;

namespace Emberfall.Core.Json
{
    public enum JsonType { Null, Bool, Number, String, Array, Object }

    /// <summary>
    /// Tiny dependency-free JSON reader/writer.
    /// Lives in Core so content loading and save data behave identically
    /// inside Unity and inside the offline dev harness.
    /// </summary>
    public sealed class JsonValue
    {
        private static readonly JsonValue NullValue = new JsonValue();

        private JsonType _type;
        private bool _bool;
        private double _number;
        private string _string;
        private List<JsonValue> _array;
        private Dictionary<string, JsonValue> _object;

        public JsonValue() { _type = JsonType.Null; }
        public JsonValue(bool v) { _type = JsonType.Bool; _bool = v; }
        public JsonValue(double v) { _type = JsonType.Number; _number = v; }

        public JsonValue(string v)
        {
            if (v == null) { _type = JsonType.Null; }
            else { _type = JsonType.String; _string = v; }
        }

        public JsonType Type { get { return _type; } }
        public bool IsNull { get { return _type == JsonType.Null; } }
        public bool IsObject { get { return _type == JsonType.Object; } }
        public bool IsArray { get { return _type == JsonType.Array; } }

        public static JsonValue NewObject()
        {
            JsonValue v = new JsonValue();
            v._type = JsonType.Object;
            v._object = new Dictionary<string, JsonValue>();
            return v;
        }

        public static JsonValue NewArray()
        {
            JsonValue v = new JsonValue();
            v._type = JsonType.Array;
            v._array = new List<JsonValue>();
            return v;
        }

        // ---------- container access ----------

        public int Count
        {
            get
            {
                if (_type == JsonType.Array) return _array.Count;
                if (_type == JsonType.Object) return _object.Count;
                return 0;
            }
        }

        public IEnumerable<string> Keys
        {
            get
            {
                if (_type != JsonType.Object) return new string[0];
                return _object.Keys;
            }
        }

        public JsonValue this[int index]
        {
            get
            {
                if (_type != JsonType.Array || index < 0 || index >= _array.Count) return NullValue;
                return _array[index];
            }
        }

        public JsonValue this[string key]
        {
            get
            {
                if (_type != JsonType.Object) return NullValue;
                JsonValue found;
                if (_object.TryGetValue(key, out found)) return found;
                return NullValue;
            }
        }

        public bool Has(string key)
        {
            return _type == JsonType.Object && _object.ContainsKey(key) && !_object[key].IsNull;
        }

        public JsonValue Set(string key, JsonValue value)
        {
            if (_type != JsonType.Object) throw new InvalidOperationException("Set() called on a non-object JSON value");
            _object[key] = value != null ? value : NullValue;
            return this;
        }

        public JsonValue Set(string key, string value) { return Set(key, new JsonValue(value)); }
        public JsonValue Set(string key, double value) { return Set(key, new JsonValue(value)); }
        public JsonValue Set(string key, int value) { return Set(key, new JsonValue((double)value)); }
        public JsonValue Set(string key, long value) { return Set(key, new JsonValue((double)value)); }
        public JsonValue Set(string key, bool value) { return Set(key, new JsonValue(value)); }

        public JsonValue Add(JsonValue value)
        {
            if (_type != JsonType.Array) throw new InvalidOperationException("Add() called on a non-array JSON value");
            _array.Add(value != null ? value : NullValue);
            return this;
        }

        public JsonValue Add(string value) { return Add(new JsonValue(value)); }
        public JsonValue Add(int value) { return Add(new JsonValue((double)value)); }

        // ---------- scalar access ----------

        public string AsString(string fallback)
        {
            if (_type == JsonType.String) return _string;
            if (_type == JsonType.Number) return _number.ToString(CultureInfo.InvariantCulture);
            if (_type == JsonType.Bool) return _bool ? "true" : "false";
            return fallback;
        }

        public double AsDouble(double fallback)
        {
            if (_type == JsonType.Number) return _number;
            if (_type == JsonType.Bool) return _bool ? 1.0 : 0.0;
            if (_type == JsonType.String)
            {
                double parsed;
                if (double.TryParse(_string, NumberStyles.Float, CultureInfo.InvariantCulture, out parsed)) return parsed;
            }
            return fallback;
        }

        public float AsFloat(float fallback) { return (float)AsDouble(fallback); }

        public int AsInt(int fallback)
        {
            double d = AsDouble(double.NaN);
            if (double.IsNaN(d)) return fallback;
            return (int)Math.Round(d, MidpointRounding.AwayFromZero);
        }

        public long AsLong(long fallback)
        {
            double d = AsDouble(double.NaN);
            if (double.IsNaN(d)) return fallback;
            return (long)Math.Round(d, MidpointRounding.AwayFromZero);
        }

        public bool AsBool(bool fallback)
        {
            if (_type == JsonType.Bool) return _bool;
            if (_type == JsonType.Number) return Math.Abs(_number) > 0.0001;
            if (_type == JsonType.String) return string.Equals(_string, "true", StringComparison.OrdinalIgnoreCase);
            return fallback;
        }

        public List<JsonValue> AsList()
        {
            if (_type == JsonType.Array) return _array;
            return new List<JsonValue>();
        }

        public string GetString(string key, string fallback) { return this[key].AsString(fallback); }
        public float GetFloat(string key, float fallback) { return this[key].AsFloat(fallback); }
        public int GetInt(string key, int fallback) { return this[key].AsInt(fallback); }
        public long GetLong(string key, long fallback) { return this[key].AsLong(fallback); }
        public bool GetBool(string key, bool fallback) { return this[key].AsBool(fallback); }
        public List<JsonValue> GetList(string key) { return this[key].AsList(); }

        // ---------- parsing ----------

        public static JsonValue Parse(string text)
        {
            if (string.IsNullOrEmpty(text)) throw new FormatException("Empty JSON text");
            int index = 0;
            JsonValue result = ParseValue(text, ref index);
            SkipWhitespace(text, ref index);
            return result;
        }

        public static bool TryParse(string text, out JsonValue value, out string error)
        {
            value = null;
            error = null;
            try
            {
                value = Parse(text);
                return true;
            }
            catch (Exception ex)
            {
                error = ex.Message;
                return false;
            }
        }

        private static void SkipWhitespace(string s, ref int i)
        {
            while (i < s.Length)
            {
                char c = s[i];
                if (c == ' ' || c == '\t' || c == '\r' || c == '\n') { i++; continue; }
                // tolerate // line comments so designers can annotate content files
                if (c == '/' && i + 1 < s.Length && s[i + 1] == '/')
                {
                    while (i < s.Length && s[i] != '\n') i++;
                    continue;
                }
                break;
            }
        }

        private static JsonValue ParseValue(string s, ref int i)
        {
            SkipWhitespace(s, ref i);
            if (i >= s.Length) throw new FormatException("Unexpected end of JSON at " + i);
            char c = s[i];
            if (c == '{') return ParseObject(s, ref i);
            if (c == '[') return ParseArray(s, ref i);
            if (c == '"') return new JsonValue(ParseString(s, ref i));
            if (c == 't' || c == 'f') return new JsonValue(ParseBool(s, ref i));
            if (c == 'n') { Expect(s, ref i, "null"); return new JsonValue(); }
            return new JsonValue(ParseNumber(s, ref i));
        }

        private static void Expect(string s, ref int i, string literal)
        {
            if (i + literal.Length > s.Length || string.CompareOrdinal(s, i, literal, 0, literal.Length) != 0)
                throw new FormatException("Expected " + literal + " at " + i);
            i += literal.Length;
        }

        private static bool ParseBool(string s, ref int i)
        {
            if (s[i] == 't') { Expect(s, ref i, "true"); return true; }
            Expect(s, ref i, "false");
            return false;
        }

        private static double ParseNumber(string s, ref int i)
        {
            int start = i;
            if (i < s.Length && (s[i] == '-' || s[i] == '+')) i++;
            while (i < s.Length)
            {
                char c = s[i];
                bool isDigit = c >= '0' && c <= '9';
                if (isDigit || c == '.' || c == 'e' || c == 'E' || c == '-' || c == '+') { i++; continue; }
                break;
            }
            string slice = s.Substring(start, i - start);
            double value;
            if (!double.TryParse(slice, NumberStyles.Float, CultureInfo.InvariantCulture, out value))
                throw new FormatException("Malformed number " + slice + " at " + start);
            return value;
        }

        private static string ParseString(string s, ref int i)
        {
            i++; // opening quote
            StringBuilder sb = new StringBuilder();
            while (true)
            {
                if (i >= s.Length) throw new FormatException("Unterminated string");
                char c = s[i++];
                if (c == '"') break;
                if (c != '\\') { sb.Append(c); continue; }
                if (i >= s.Length) throw new FormatException("Unterminated escape sequence");
                char e = s[i++];
                switch (e)
                {
                    case '"': sb.Append('"'); break;
                    case '\\': sb.Append('\\'); break;
                    case '/': sb.Append('/'); break;
                    case 'b': sb.Append('\b'); break;
                    case 'f': sb.Append('\f'); break;
                    case 'n': sb.Append('\n'); break;
                    case 'r': sb.Append('\r'); break;
                    case 't': sb.Append('\t'); break;
                    case 'u':
                        if (i + 4 > s.Length) throw new FormatException("Malformed unicode escape");
                        sb.Append((char)Convert.ToInt32(s.Substring(i, 4), 16));
                        i += 4;
                        break;
                    default: throw new FormatException("Unknown escape character " + e);
                }
            }
            return sb.ToString();
        }

        private static JsonValue ParseArray(string s, ref int i)
        {
            JsonValue result = NewArray();
            i++;
            SkipWhitespace(s, ref i);
            if (i < s.Length && s[i] == ']') { i++; return result; }
            while (true)
            {
                result._array.Add(ParseValue(s, ref i));
                SkipWhitespace(s, ref i);
                if (i >= s.Length) throw new FormatException("Unterminated array");
                if (s[i] == ',') { i++; continue; }
                if (s[i] == ']') { i++; break; }
                throw new FormatException("Expected , or ] at " + i);
            }
            return result;
        }

        private static JsonValue ParseObject(string s, ref int i)
        {
            JsonValue result = NewObject();
            i++;
            SkipWhitespace(s, ref i);
            if (i < s.Length && s[i] == '}') { i++; return result; }
            while (true)
            {
                SkipWhitespace(s, ref i);
                if (i >= s.Length || s[i] != '"') throw new FormatException("Expected object key at " + i);
                string key = ParseString(s, ref i);
                SkipWhitespace(s, ref i);
                if (i >= s.Length || s[i] != ':') throw new FormatException("Expected : at " + i);
                i++;
                result._object[key] = ParseValue(s, ref i);
                SkipWhitespace(s, ref i);
                if (i >= s.Length) throw new FormatException("Unterminated object");
                if (s[i] == ',') { i++; continue; }
                if (s[i] == '}') { i++; break; }
                throw new FormatException("Expected , or } at " + i);
            }
            return result;
        }

        // ---------- writing ----------

        public string ToJson(bool pretty)
        {
            StringBuilder sb = new StringBuilder();
            Write(sb, pretty, 0);
            return sb.ToString();
        }

        public override string ToString() { return ToJson(false); }

        private void Write(StringBuilder sb, bool pretty, int indent)
        {
            switch (_type)
            {
                case JsonType.Null:
                    sb.Append("null");
                    break;
                case JsonType.Bool:
                    sb.Append(_bool ? "true" : "false");
                    break;
                case JsonType.Number:
                    WriteNumber(sb, _number);
                    break;
                case JsonType.String:
                    WriteString(sb, _string);
                    break;
                case JsonType.Array:
                {
                    if (_array.Count == 0) { sb.Append("[]"); break; }
                    sb.Append('[');
                    for (int k = 0; k < _array.Count; k++)
                    {
                        if (k > 0) sb.Append(',');
                        NewLine(sb, pretty, indent + 1);
                        _array[k].Write(sb, pretty, indent + 1);
                    }
                    NewLine(sb, pretty, indent);
                    sb.Append(']');
                    break;
                }
                case JsonType.Object:
                {
                    if (_object.Count == 0) { sb.Append("{}"); break; }
                    sb.Append('{');
                    bool first = true;
                    foreach (KeyValuePair<string, JsonValue> pair in _object)
                    {
                        if (!first) sb.Append(',');
                        first = false;
                        NewLine(sb, pretty, indent + 1);
                        WriteString(sb, pair.Key);
                        sb.Append(':');
                        if (pretty) sb.Append(' ');
                        pair.Value.Write(sb, pretty, indent + 1);
                    }
                    NewLine(sb, pretty, indent);
                    sb.Append('}');
                    break;
                }
            }
        }

        private static void NewLine(StringBuilder sb, bool pretty, int indent)
        {
            if (!pretty) return;
            sb.Append('\n');
            sb.Append(' ', indent * 2);
        }

        private static void WriteNumber(StringBuilder sb, double value)
        {
            if (value == Math.Floor(value) && Math.Abs(value) < 1e15)
                sb.Append(((long)value).ToString(CultureInfo.InvariantCulture));
            else
                sb.Append(value.ToString("R", CultureInfo.InvariantCulture));
        }

        private static void WriteString(StringBuilder sb, string value)
        {
            sb.Append('"');
            for (int k = 0; k < value.Length; k++)
            {
                char c = value[k];
                switch (c)
                {
                    case '"': sb.Append("\\\""); break;
                    case '\\': sb.Append("\\\\"); break;
                    case '\n': sb.Append("\\n"); break;
                    case '\r': sb.Append("\\r"); break;
                    case '\t': sb.Append("\\t"); break;
                    default:
                        if (c < ' ') sb.Append("\\u").Append(((int)c).ToString("x4", CultureInfo.InvariantCulture));
                        else sb.Append(c);
                        break;
                }
            }
            sb.Append('"');
        }
    }
}
