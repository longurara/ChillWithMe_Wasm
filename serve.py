"""Local preview: serve WebGL and separate asset packs without duplicating them."""
import argparse
from http.server import SimpleHTTPRequestHandler, ThreadingHTTPServer
from pathlib import Path
from urllib.parse import unquote, urlsplit

ROOT = Path(__file__).resolve().parent

class Handler(SimpleHTTPRequestHandler):
    extensions_map = {**SimpleHTTPRequestHandler.extensions_map, '.wasm': 'application/wasm', '.data': 'application/octet-stream', '.json': 'application/json', '.js': 'application/javascript'}

    def translate_path(self, path):
        route = unquote(urlsplit(path).path)
        prefix = '/assetpacks/'
        if route.startswith(prefix):
            base = (ROOT/'AssetLibrary/assetpacks').resolve()
            target = (base/route[len(prefix):]).resolve()
        else:
            base = (ROOT/'WebPlayer').resolve()
            target = (base/route.lstrip('/')).resolve()
        # Never expose original data or tooling through traversal paths.
        if not target.is_relative_to(base): return str(ROOT/'WebPlayer/__invalid_path__')
        return str(target)

    def end_headers(self):
        self.send_header('Cache-Control', 'no-cache')
        self.send_header('X-Content-Type-Options', 'nosniff')
        super().end_headers()

if __name__ == '__main__':
    parser = argparse.ArgumentParser()
    parser.add_argument('--port', type=int, default=8093)
    args = parser.parse_args()
    server = ThreadingHTTPServer(('127.0.0.1', args.port), Handler)
    print(f'Chill standalone: http://127.0.0.1:{args.port}/', flush=True)
    try: server.serve_forever()
    except KeyboardInterrupt: pass
    finally: server.server_close()
