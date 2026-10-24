"""Preview server: serves this folder and a canned /api/state. Real server: serve.py (to be written)."""
import http.server, json, pathlib, sys
ROOT = pathlib.Path(__file__).resolve().parent
class H(http.server.SimpleHTTPRequestHandler):
    def __init__(self, *a, **k): super().__init__(*a, directory=str(ROOT), **k)
    def do_GET(self):
        if self.path.split('?')[0] == '/api/state':
            b = (ROOT / 'state.sample.json').read_bytes()
            self.send_response(200); self.send_header('Content-Type', 'application/json'); self.send_header('Content-Length', str(len(b))); self.end_headers(); self.wfile.write(b)
        else: super().do_GET()
    def do_POST(self):
        self.rfile.read(int(self.headers.get('Content-Length', 0) or 0))
        b = b'{"ok":true}'; self.send_response(200); self.send_header('Content-Type', 'application/json'); self.send_header('Content-Length', str(len(b))); self.end_headers(); self.wfile.write(b)
    def log_message(self, *a): pass
if __name__ == '__main__':
    port = int(sys.argv[1]) if len(sys.argv) > 1 else 8765
    http.server.ThreadingHTTPServer(('127.0.0.1', port), H).serve_forever()
