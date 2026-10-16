using System.Collections.Generic;
using Emberfall.Core.Json;

namespace Emberfall.Core.Telemetry
{
    /// <summary>
    /// The unsent events, oldest first. A plain list with two bounds the recorder relies on: it
    /// never holds more than StoredCap events (the oldest are dropped to make room, so a phone that
    /// is offline for a week keeps the recent past and not an unbounded file), and it only ever
    /// holds events that passed TelemetrySchema, because FromJson runs every stored row back
    /// through the whitelist.
    /// </summary>
    public sealed class TelemetryQueue
    {
        public const int StoredCap = 500;

        private readonly List<TelemetryEvent> _items = new List<TelemetryEvent>();

        public int Count { get { return _items.Count; } }

        public TelemetryEvent this[int index] { get { return _items[index]; } }

        public void Add(TelemetryEvent ev)
        {
            if (ev == null) return;
            _items.Add(ev);
            if (_items.Count > StoredCap) _items.RemoveRange(0, _items.Count - StoredCap);
        }

        /// <summary>Up to <paramref name="max"/> events from the front, left in the queue.</summary>
        public List<TelemetryEvent> Peek(int max)
        {
            int n = _items.Count < max ? _items.Count : max;
            return _items.GetRange(0, n);
        }

        /// <summary>Removes these events by reference. One that is no longer here (the stored cap
        /// dropped it while it was in flight) is skipped, so a late acknowledgement cannot take
        /// newer events with it.</summary>
        public void Remove(List<TelemetryEvent> sent)
        {
            for (int i = 0; i < sent.Count; i++) _items.Remove(sent[i]);
        }

        public void Clear() { _items.Clear(); }

        public JsonValue ToJson()
        {
            JsonValue array = JsonValue.NewArray();
            for (int i = 0; i < _items.Count; i++)
            {
                JsonValue row = JsonValue.NewObject();
                row.Set("e", _items[i].Name);
                row.Set("p", _items[i].Payload);
                row.Set("b", _items[i].Build);
                array.Add(row);
            }
            return array;
        }

        /// <summary>Reads stored rows, keeping only those that still pass the whitelist.</summary>
        public static TelemetryQueue FromJson(JsonValue array)
        {
            TelemetryQueue queue = new TelemetryQueue();
            if (array == null || !array.IsArray) return queue;
            List<JsonValue> rows = array.AsList();
            for (int i = 0; i < rows.Count; i++)
            {
                if (!rows[i].IsObject) continue;
                TelemetryEvent ev = TelemetrySchema.TryBuild(rows[i].GetString("e", ""), rows[i]["p"], rows[i].GetString("b", ""));
                if (ev != null) queue.Add(ev);
            }
            return queue;
        }
    }
}
