"""Web cockpit server. Binds 127.0.0.1 only. Start it with cockpit-web.bat, or:

    python serve.py [--port 8765] [--mock]

GET /               web/index.html (and the other files in web/)
GET /api/state      state_builder.build_state(data.collect(env)), cached for 5 seconds
POST /api/set       header X-Cockpit: 1, JSON {"key": ..., "value": "..."}; keys block_reset, weekly, playtest_date.
                    Writes Tools/cockpit/state.json (gitignored) through the same functions the TUI keys b and w use.
                    Anything else is a 400. Everything else is read only (the transcript cache aside, as in the TUI).
"""

from __future__ import annotations

import argparse
import http.server
import json
import re
import sys
import threading
import time
from datetime import date, timedelta
from pathlib import Path

import data
import state_builder

HERE = Path(__file__).resolve().parent
WEB = HERE / "web"
HOST = "127.0.0.1"
DEFAULT_PORT = 8765
CACHE_S = 5.0
USAGE_CACHE_S = 60.0                     # ccusage takes seconds; the rest of the state is cheap
ALLOWED_KEYS = ("block_reset", "weekly", "playtest_date")
MAX_BODY = 4096
BARE_PERCENT = re.compile(r"\s*(\d{1,3}(?:[.,]\d+)?)\s*%?\s*")


# ---------------------------------------------------------------- writes

def apply_set(env: data.Env, key: str, value: str) -> dict:
    """Validate one allowed write and store it. Raises ValueError(message) for anything not accepted."""
    if key not in ALLOWED_KEYS:
        raise ValueError(f"key must be one of {', '.join(ALLOWED_KEYS)}")
    if not isinstance(value, str):
        raise ValueError("value must be a string")
    now = env.now()
    if key == "block_reset":
        left = data.parse_remaining(value)
        if not left or left <= timedelta(0):
            raise ValueError("could not read the time left: use 3h12, 3:12 or 01:12")
        if left > timedelta(hours=5):
            raise ValueError("a 5h block has at most 5h left")
        end = (now + left).replace(second=0, microsecond=0)
        changes = {"block_end_manual": end.isoformat(), "block_set_at": now.replace(microsecond=0).isoformat()}
    elif key == "weekly":
        w = data.parse_weekly(value, now)
        if w is None:
            m = BARE_PERCENT.fullmatch(value)
            old = data.load_state(env).get("weekly") or {}
            reset = data.parse_time(old.get("reset"))
            if m and float(m.group(1).replace(",", ".")) <= 100 and reset and reset > now:
                w = {"pct": float(m.group(1).replace(",", ".")), "reset": old["reset"],
                     "set_at": now.replace(microsecond=0).isoformat()}      # new percent, the stored reset still stands
            elif m:
                raise ValueError("no reset stored yet: type the percent and the time left, for example 62 2d13h")
            else:
                raise ValueError("could not read it: use 62 or 62 2d13h")
        changes = {"weekly": w}
    else:
        v = value.strip()
        if v:
            if not re.fullmatch(r"\d{4}-\d{2}-\d{2}", v):
                raise ValueError("use YYYY-MM-DD, or leave it empty to clear")
            try:
                date.fromisoformat(v)
            except ValueError:
                raise ValueError("not a real date") from None
        changes = {"playtest_date": v}
    data.save_state(env, **changes)
    back = data.load_state(env)          # save_state swallows write errors, so read it back
    if any(back.get(k) != v for k, v in changes.items()):
        raise OSError("state.json could not be written")
    return changes


# ---------------------------------------------------------------- state cache

class Store:
    def __init__(self, env: data.Env):
        self.env = env
        self.lock = threading.Lock()
        self.cached: tuple[float, bytes] | None = None

    def state_json(self) -> bytes:
        with self.lock:
            if self.cached and time.monotonic() - self.cached[0] < CACHE_S:
                return self.cached[1]
            body = json.dumps(state_builder.build_state(data.collect(self.env)), ensure_ascii=False).encode("utf-8")
            self.cached = (time.monotonic(), body)
            return body

    def set(self, key: str, value) -> dict:
        with self.lock:
            changes = apply_set(self.env, key, value)
            self.cached = None
            return changes


# ---------------------------------------------------------------- http

class Handler(http.server.SimpleHTTPRequestHandler):
    store: Store = None  # set by make_server

    def __init__(self, *a, **k):
        super().__init__(*a, directory=str(WEB), **k)

    def log_request(self, code="-", size="-"):
        if str(code) not in ("200", "304"):
            super().log_request(code, size)

    def end_headers(self):
        # files in web/ are revalidated every time, so an edited app.js or style.css is never served stale
        if not any(h.lower().startswith(b"cache-control") for h in getattr(self, "_headers_buffer", [])):
            self.send_header("Cache-Control", "no-cache")
        super().end_headers()

    def send_json(self, code: int, obj) -> None:
        body = obj if isinstance(obj, bytes) else json.dumps(obj).encode("utf-8")
        self.send_response(code)
        self.send_header("Content-Type", "application/json; charset=utf-8")
        self.send_header("Content-Length", str(len(body)))
        self.send_header("Cache-Control", "no-store")
        self.end_headers()
        self.wfile.write(body)

    def host_ok(self) -> bool:
        """Refuse a Host header that is not this server (DNS rebinding)."""
        port = self.server.server_address[1]
        return self.headers.get("Host", "") in (f"127.0.0.1:{port}", f"localhost:{port}", "127.0.0.1", "localhost")

    def do_GET(self):
        path = self.path.split("?", 1)[0]
        if not self.host_ok():
            return self.send_json(400, {"error": "bad Host header"})
        if path == "/api/state":
            try:
                return self.send_json(200, self.store.state_json())
            except Exception as e:  # the page shows its own message; the console gets the detail
                import traceback
                traceback.print_exc()
                return self.send_json(500, {"error": e.__class__.__name__})
        if path.endswith(".py") or path.startswith("/api/"):
            return self.send_json(404, {"error": "not found"})
        return super().do_GET()

    def do_HEAD(self):
        return self.send_json(405, {"error": "GET only"}) if self.path.startswith("/api/") else super().do_HEAD()

    def do_POST(self):
        if self.path.split("?", 1)[0] != "/api/set":
            return self.send_json(404, {"error": "not found"})
        if not self.host_ok():
            return self.send_json(400, {"error": "bad Host header"})
        if self.headers.get("X-Cockpit") != "1":
            return self.send_json(400, {"error": "missing X-Cockpit: 1 header"})
        try:
            n = int(self.headers.get("Content-Length", "0"))
        except ValueError:
            n = -1
        if not 0 < n <= MAX_BODY:
            return self.send_json(400, {"error": "body missing or too large"})
        try:
            body = json.loads(self.rfile.read(n).decode("utf-8"))
        except (UnicodeDecodeError, json.JSONDecodeError):
            return self.send_json(400, {"error": "body is not JSON"})
        if not isinstance(body, dict) or set(body) != {"key", "value"}:
            return self.send_json(400, {"error": 'body must be {"key": ..., "value": ...}'})
        try:
            changes = self.store.set(body["key"], body["value"])
        except ValueError as e:
            return self.send_json(400, {"error": str(e)})
        except OSError as e:
            return self.send_json(500, {"error": str(e)})
        return self.send_json(200, {"ok": True, "key": body["key"], "stored": changes})

    def do_PUT(self):
        return self.send_json(405, {"error": "not allowed"})

    do_DELETE = do_PATCH = do_PUT


def make_server(env: data.Env, port: int = DEFAULT_PORT) -> tuple[http.server.ThreadingHTTPServer, Store]:
    store = Store(env)
    handler = type("BoundHandler", (Handler,), {"store": store})
    httpd = http.server.ThreadingHTTPServer((HOST, port), handler)
    httpd.daemon_threads = True
    return httpd, store


def main() -> int:
    ap = argparse.ArgumentParser(description="Emberfall web cockpit (127.0.0.1 only)")
    ap.add_argument("--port", type=int, default=DEFAULT_PORT)
    ap.add_argument("--mock", action="store_true", help="serve Tools/cockpit/mock instead of D:\\Emberfall")
    args = ap.parse_args()
    env = data.mock_env() if args.mock else data.live_env()
    data.USAGE_TTL = USAGE_CACHE_S
    try:
        httpd, _ = make_server(env, args.port)
    except OSError as e:
        print(f"Cannot bind {HOST}:{args.port}: {e}", file=sys.stderr)
        return 1
    print(f"Emberfall cockpit on http://{HOST}:{args.port}/" + ("  (MOCK DATA)" if args.mock else ""), flush=True)
    try:
        httpd.serve_forever()
    except KeyboardInterrupt:
        pass
    return 0


if __name__ == "__main__":
    sys.exit(main())
