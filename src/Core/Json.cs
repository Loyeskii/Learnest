using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.Text;

namespace Learnest.Core
{
    /// <summary>
    /// A small, dependency free JSON DOM.  Learnest ships as a single executable with no
    /// third party assemblies, so serialisation is implemented here instead of pulling in
    /// Newtonsoft.Json / System.Text.Json.
    ///
    /// Design notes:
    ///  * Ordered dictionaries keep generated profiles diff friendly and stable.
    ///  * Numbers are kept as either long or double so ids, counts and durations round trip.
    ///  * A strict parser (no trailing commas, no comments) and a forgiving reader
    ///    (BOM, whitespace, unknown escapes) keep hand edited files working.
    /// </summary>
    public sealed class JsonValue
    {
        private readonly JsonKind _kind;
        private readonly object _value;

        private JsonValue(JsonKind kind, object value)
        {
            _kind = kind;
            _value = value;
        }

        public JsonKind Kind { get { return _kind; } }
        public bool IsNull { get { return _kind == JsonKind.Null; } }
        public bool IsObject { get { return _kind == JsonKind.Object; } }
        public bool IsArray { get { return _kind == JsonKind.Array; } }

        public static readonly JsonValue Null = new JsonValue(JsonKind.Null, null);

        public static JsonValue Object()
        {
            return new JsonValue(JsonKind.Object, new JsonObject());
        }

        public static JsonValue Object(JsonObject value)
        {
            return new JsonValue(JsonKind.Object, value ?? new JsonObject());
        }

        public static JsonValue Array()
        {
            return new JsonValue(JsonKind.Array, new JsonArray());
        }

        public static JsonValue Array(JsonArray value)
        {
            return new JsonValue(JsonKind.Array, value ?? new JsonArray());
        }

        public static JsonValue String(string value)
        {
            return value == null ? Null : new JsonValue(JsonKind.String, value);
        }

        public static JsonValue Number(long value) { return new JsonValue(JsonKind.Number, value); }
        public static JsonValue Number(double value) { return new JsonValue(JsonKind.Number, value); }
        public static JsonValue Bool(bool value) { return new JsonValue(JsonKind.Bool, value); }

        public JsonObject AsObject()
        {
            if (_kind != JsonKind.Object) { return new JsonObject(); }
            return (JsonObject)_value;
        }

        public JsonArray AsArray()
        {
            if (_kind != JsonKind.Array) { return new JsonArray(); }
            return (JsonArray)_value;
        }

        // ------------------------------------------------------------ accessors
        public JsonValue Get(string key)
        {
            if (_kind != JsonKind.Object) { return Null; }
            JsonValue found;
            if (((JsonObject)_value).TryGetValue(key, out found)) { return found ?? Null; }
            return Null;
        }

        public string AsString(string fallback)
        {
            if (_kind == JsonKind.String) { return (string)_value; }
            if (_kind == JsonKind.Number) { return Convert.ToString(_value, CultureInfo.InvariantCulture); }
            if (_kind == JsonKind.Bool) { return ((bool)_value) ? "true" : "false"; }
            return fallback;
        }

        public double AsDouble(double fallback)
        {
            if (_kind == JsonKind.Number)
            {
                if (_value is long) { return (long)_value; }
                return (double)_value;
            }
            if (_kind == JsonKind.String)
            {
                double parsed;
                if (double.TryParse((string)_value, NumberStyles.Float, CultureInfo.InvariantCulture, out parsed)) { return parsed; }
            }
            return fallback;
        }

        public long AsLong(long fallback)
        {
            if (_kind == JsonKind.Number)
            {
                if (_value is long) { return (long)_value; }
                return (long)Math.Round((double)_value);
            }
            if (_kind == JsonKind.String)
            {
                long parsed;
                if (long.TryParse((string)_value, NumberStyles.Integer, CultureInfo.InvariantCulture, out parsed)) { return parsed; }
            }
            return fallback;
        }

        public int AsInt(int fallback)
        {
            return (int)AsLong(fallback);
        }

        public bool AsBool(bool fallback)
        {
            if (_kind == JsonKind.Bool) { return (bool)_value; }
            if (_kind == JsonKind.Number) { return AsLong(0) != 0; }
            if (_kind == JsonKind.String)
            {
                string s = ((string)_value).Trim();
                if (s.Equals("true", StringComparison.OrdinalIgnoreCase)) { return true; }
                if (s.Equals("false", StringComparison.OrdinalIgnoreCase)) { return false; }
            }
            return fallback;
        }

        public DateTime AsDate(DateTime fallback)
        {
            DateTime parsed;
            if (IsoTime.TryParse(AsString(null), out parsed)) { return parsed; }
            return fallback;
        }

        public DateTime? AsDateOrNull()
        {
            DateTime parsed;
            if (IsoTime.TryParse(AsString(null), out parsed)) { return parsed; }
            return null;
        }

        public List<string> AsStringList()
        {
            List<string> list = new List<string>();
            if (_kind != JsonKind.Array) { return list; }
            JsonArray array = (JsonArray)_value;
            for (int i = 0; i < array.Count; i++)
            {
                string s = array[i].AsString(null);
                if (s != null) { list.Add(s); }
            }
            return list;
        }

        public List<double> AsDoubleList()
        {
            List<double> list = new List<double>();
            if (_kind != JsonKind.Array) { return list; }
            JsonArray array = (JsonArray)_value;
            for (int i = 0; i < array.Count; i++) { list.Add(array[i].AsDouble(0)); }
            return list;
        }

        // ------------------------------------------------------------ writing
        public override string ToString() { return Write(false); }

        public string ToPrettyString() { return Write(true); }

        private string Write(bool pretty)
        {
            StringBuilder sb = new StringBuilder(256);
            WriteTo(sb, pretty, 0);
            return sb.ToString();
        }

        private void WriteTo(StringBuilder sb, bool pretty, int depth)
        {
            switch (_kind)
            {
                case JsonKind.Null:
                    sb.Append("null");
                    break;
                case JsonKind.Bool:
                    sb.Append(((bool)_value) ? "true" : "false");
                    break;
                case JsonKind.Number:
                    if (_value is long) { sb.Append(((long)_value).ToString(CultureInfo.InvariantCulture)); }
                    else
                    {
                        double d = (double)_value;
                        if (double.IsNaN(d) || double.IsInfinity(d)) { sb.Append('0'); }
                        else { sb.Append(d.ToString("R", CultureInfo.InvariantCulture)); }
                    }
                    break;
                case JsonKind.String:
                    Escape(sb, (string)_value);
                    break;
                case JsonKind.Array:
                    WriteArray(sb, pretty, depth);
                    break;
                default:
                    WriteObject(sb, pretty, depth);
                    break;
            }
        }

        private void WriteArray(StringBuilder sb, bool pretty, int depth)
        {
            JsonArray array = (JsonArray)_value;
            if (array.Count == 0) { sb.Append("[]"); return; }
            sb.Append('[');
            for (int i = 0; i < array.Count; i++)
            {
                if (i > 0) { sb.Append(','); }
                if (pretty) { NewLine(sb, depth + 1); }
                array[i].WriteTo(sb, pretty, depth + 1);
            }
            if (pretty) { NewLine(sb, depth); }
            sb.Append(']');
        }

        private void WriteObject(StringBuilder sb, bool pretty, int depth)
        {
            JsonObject obj = (JsonObject)_value;
            if (obj.Count == 0) { sb.Append("{}"); return; }
            sb.Append('{');
            bool first = true;
            foreach (KeyValuePair<string, JsonValue> pair in obj)
            {
                if (!first) { sb.Append(','); }
                first = false;
                if (pretty) { NewLine(sb, depth + 1); }
                Escape(sb, pair.Key);
                sb.Append(':');
                if (pretty) { sb.Append(' '); }
                (pair.Value ?? Null).WriteTo(sb, pretty, depth + 1);
            }
            if (pretty) { NewLine(sb, depth); }
            sb.Append('}');
        }

        private static void NewLine(StringBuilder sb, int depth)
        {
            sb.Append('\n');
            for (int i = 0; i < depth; i++) { sb.Append("  "); }
        }

        private static void Escape(StringBuilder sb, string value)
        {
            if (value == null) { sb.Append("null"); return; }
            sb.Append('"');
            for (int i = 0; i < value.Length; i++)
            {
                char c = value[i];
                switch (c)
                {
                    case '"': sb.Append("\\\""); break;
                    case '\\': sb.Append("\\\\"); break;
                    case '\b': sb.Append("\\b"); break;
                    case '\f': sb.Append("\\f"); break;
                    case '\n': sb.Append("\\n"); break;
                    case '\r': sb.Append("\\r"); break;
                    case '\t': sb.Append("\\t"); break;
                    default:
                        if (c < ' ')
                        {
                            sb.Append("\\u").Append(((int)c).ToString("x4", CultureInfo.InvariantCulture));
                        }
                        else
                        {
                            sb.Append(c);
                        }
                        break;
                }
            }
            sb.Append('"');
        }

        // ------------------------------------------------------------ parsing
        public static JsonValue Parse(string text)
        {
            if (string.IsNullOrEmpty(text)) { return Null; }
            int index = 0;
            JsonValue value = ParseValue(text, ref index);
            return value ?? Null;
        }

        public static bool TryParse(string text, out JsonValue value)
        {
            value = null;
            try
            {
                value = Parse(text);
                return value != null;
            }
            catch
            {
                return false;
            }
        }

        private static JsonValue ParseValue(string text, ref int index)
        {
            SkipWhitespace(text, ref index);
            if (index >= text.Length) { throw new JsonException("Unexpected end of JSON input."); }
            char c = text[index];
            switch (c)
            {
                case '{': return ParseObject(text, ref index);
                case '[': return ParseArray(text, ref index);
                case '"': return String(ParseString(text, ref index));
                case 't':
                    Expect(text, ref index, "true");
                    return Bool(true);
                case 'f':
                    Expect(text, ref index, "false");
                    return Bool(false);
                case 'n':
                    Expect(text, ref index, "null");
                    return Null;
                default:
                    return ParseNumber(text, ref index);
            }
        }

        private static JsonValue ParseObject(string text, ref int index)
        {
            JsonObject obj = new JsonObject();
            index++; // '{'
            SkipWhitespace(text, ref index);
            if (index < text.Length && text[index] == '}') { index++; return Object(obj); }
            while (true)
            {
                SkipWhitespace(text, ref index);
                if (index >= text.Length || text[index] != '"')
                {
                    throw new JsonException("Expected a property name at position " + index + ".");
                }
                string key = ParseString(text, ref index);
                SkipWhitespace(text, ref index);
                if (index >= text.Length || text[index] != ':')
                {
                    throw new JsonException("Expected ':' after property '" + key + "'.");
                }
                index++;
                JsonValue value = ParseValue(text, ref index);
                obj[key] = value;
                SkipWhitespace(text, ref index);
                if (index >= text.Length) { throw new JsonException("Unterminated object."); }
                if (text[index] == ',') { index++; continue; }
                if (text[index] == '}') { index++; break; }
                throw new JsonException("Expected ',' or '}' at position " + index + ".");
            }
            return Object(obj);
        }

        private static JsonValue ParseArray(string text, ref int index)
        {
            JsonArray array = new JsonArray();
            index++; // '['
            SkipWhitespace(text, ref index);
            if (index < text.Length && text[index] == ']') { index++; return Array(array); }
            while (true)
            {
                JsonValue value = ParseValue(text, ref index);
                array.Add(value);
                SkipWhitespace(text, ref index);
                if (index >= text.Length) { throw new JsonException("Unterminated array."); }
                if (text[index] == ',') { index++; continue; }
                if (text[index] == ']') { index++; break; }
                throw new JsonException("Expected ',' or ']' at position " + index + ".");
            }
            return Array(array);
        }

        private static string ParseString(string text, ref int index)
        {
            StringBuilder sb = new StringBuilder();
            index++; // opening quote
            while (true)
            {
                if (index >= text.Length) { throw new JsonException("Unterminated string."); }
                char c = text[index++];
                if (c == '"') { break; }
                if (c != '\\')
                {
                    sb.Append(c);
                    continue;
                }
                if (index >= text.Length) { throw new JsonException("Unterminated escape sequence."); }
                char escape = text[index++];
                switch (escape)
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
                        if (index + 4 > text.Length) { throw new JsonException("Truncated \\u escape."); }
                        string hex = text.Substring(index, 4);
                        index += 4;
                        ushort code;
                        if (!ushort.TryParse(hex, NumberStyles.HexNumber, CultureInfo.InvariantCulture, out code))
                        {
                            throw new JsonException("Invalid \\u escape '" + hex + "'.");
                        }
                        sb.Append((char)code);
                        break;
                    default:
                        throw new JsonException("Unknown escape '\\" + escape + "'.");
                }
            }
            return sb.ToString();
        }

        private static JsonValue ParseNumber(string text, ref int index)
        {
            int start = index;
            if (index < text.Length && (text[index] == '-' || text[index] == '+')) { index++; }
            bool isFloat = false;
            while (index < text.Length)
            {
                char c = text[index];
                if (c >= '0' && c <= '9') { index++; continue; }
                if (c == '.' || c == 'e' || c == 'E' || c == '+' || c == '-') { isFloat = isFloat || c == '.' || c == 'e' || c == 'E'; index++; continue; }
                break;
            }
            string token = text.Substring(start, index - start);
            if (token.Length == 0) { throw new JsonException("Expected a value at position " + start + "."); }
            if (!isFloat)
            {
                long asLong;
                if (long.TryParse(token, NumberStyles.Integer, CultureInfo.InvariantCulture, out asLong))
                {
                    return Number(asLong);
                }
            }
            double asDouble;
            if (double.TryParse(token, NumberStyles.Float, CultureInfo.InvariantCulture, out asDouble))
            {
                return Number(asDouble);
            }
            throw new JsonException("Invalid number '" + token + "'.");
        }

        private static void Expect(string text, ref int index, string literal)
        {
            if (index + literal.Length > text.Length ||
                string.CompareOrdinal(text, index, literal, 0, literal.Length) != 0)
            {
                throw new JsonException("Expected '" + literal + "' at position " + index + ".");
            }
            index += literal.Length;
        }

        private static void SkipWhitespace(string text, ref int index)
        {
            while (index < text.Length)
            {
                char c = text[index];
                if (c == ' ' || c == '\t' || c == '\r' || c == '\n' || c == '\uFEFF') { index++; continue; }
                break;
            }
        }
    }

    public enum JsonKind { Null, Bool, Number, String, Array, Object }

    /// <summary>Insertion ordered string to JsonValue map.</summary>
    public sealed class JsonObject : IEnumerable<KeyValuePair<string, JsonValue>>
    {
        private readonly List<string> _order = new List<string>();
        private readonly Dictionary<string, JsonValue> _map = new Dictionary<string, JsonValue>(StringComparer.Ordinal);

        public int Count { get { return _order.Count; } }

        public IEnumerable<string> Keys { get { return _order; } }

        public JsonValue this[string key]
        {
            get
            {
                JsonValue value;
                if (_map.TryGetValue(key, out value)) { return value ?? JsonValue.Null; }
                return JsonValue.Null;
            }
            set
            {
                if (!_map.ContainsKey(key)) { _order.Add(key); }
                _map[key] = value ?? JsonValue.Null;
            }
        }

        public bool ContainsKey(string key) { return _map.ContainsKey(key); }

        public bool TryGetValue(string key, out JsonValue value) { return _map.TryGetValue(key, out value); }

        public bool Remove(string key)
        {
            if (!_map.Remove(key)) { return false; }
            _order.Remove(key);
            return true;
        }

        /// <summary>Set only when the value is meaningful, keeping output tidy.</summary>
        public void SetIfNotEmpty(string key, string value)
        {
            if (!string.IsNullOrEmpty(value)) { this[key] = JsonValue.String(value); }
        }

        public IEnumerator<KeyValuePair<string, JsonValue>> GetEnumerator()
        {
            for (int i = 0; i < _order.Count; i++)
            {
                yield return new KeyValuePair<string, JsonValue>(_order[i], _map[_order[i]]);
            }
        }

        IEnumerator IEnumerable.GetEnumerator() { return GetEnumerator(); }
    }

    public sealed class JsonArray : IEnumerable<JsonValue>
    {
        private readonly List<JsonValue> _items = new List<JsonValue>();

        public int Count { get { return _items.Count; } }

        public JsonValue this[int index]
        {
            get
            {
                if (index < 0 || index >= _items.Count) { return JsonValue.Null; }
                return _items[index] ?? JsonValue.Null;
            }
            set { _items[index] = value ?? JsonValue.Null; }
        }

        public void Add(JsonValue value) { _items.Add(value ?? JsonValue.Null); }
        public void Add(string value) { _items.Add(JsonValue.String(value)); }

        public IEnumerator<JsonValue> GetEnumerator() { return _items.GetEnumerator(); }
        IEnumerator IEnumerable.GetEnumerator() { return GetEnumerator(); }
    }

    public sealed class JsonException : Exception
    {
        public JsonException(string message) : base(message) { }
    }
}
