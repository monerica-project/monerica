#!/usr/bin/env python3
"""Open one order page in a headless browser and print what a visitor would see, as JSON.

Used by the Monerica review order checker. It only reads: it loads the page, optionally types
ONE value (the receiving address or order id the reviewer supplied) into the page's lookup box,
and returns the visible text plus an optional screenshot. It never logs in, never pays, never
submits anything else.

  render.py --url URL [--proxy socks5://127.0.0.1:9050] [--fill VALUE] [--shot FILE.png | --shot-inline]

Safety (the link comes from an anonymous reviewer, so the page is hostile until proven otherwise):
  * On the server this script is started through /opt/monerica-order-render/run.sh as the
    locked-down user "order-render". A firewall rule for that user refuses every connection to
    this machine and to private networks (only Tor's SOCKS port is allowed). That rule is the
    real protection against a page that redirects, re-resolves its DNS name or opens sockets
    towards internal addresses; the checks below are a second layer and give clearer errors.
  * Chromium's own sandbox is on, and the process gets no secrets in its environment.
"""
import argparse
import base64
import ipaddress
import json
import os
import re
import socket
import sys
from urllib.parse import urlparse

os.environ.setdefault("PLAYWRIGHT_BROWSERS_PATH", "/opt/monerica-order-render/browsers")

from playwright.sync_api import TimeoutError as PwTimeout  # noqa: E402
from playwright.sync_api import sync_playwright  # noqa: E402

MAX_TEXT = 14000
UA = ("Mozilla/5.0 (X11; Linux x86_64) AppleWebKit/537.36 (KHTML, like Gecko) "
      "Chrome/131.0.0.0 Safari/537.36")
FIELD_HINT = re.compile(r"address|wallet|recipient|destination|payout|receiv|withdraw|order|track|code|"
                        r"token|id\b|hash|txid", re.I)
BUTTON_HINT = re.compile(r"check|view|show|unlock|verify|submit|track|search|confirm|continue|find|"
                         r"open|access|look ?up|go\b|details", re.I)
MAX_URL = 500


def host_is_public(host: str, via_proxy: bool) -> bool:
    """False for loopback / private / link-local targets, so a review can never point us inward."""
    host = (host or "").strip("[]").lower().rstrip(".")  # "localhost." is localhost
    if not host or host == "localhost" or host.endswith(".localhost") or host.endswith(".local") \
            or host.endswith(".internal"):
        return False
    if host.endswith(".onion"):
        return via_proxy
    try:
        return ipaddress.ip_address(host).is_global
    except ValueError:
        pass
    if via_proxy:
        return True  # resolved by the proxy (Tor), which cannot reach our private network
    # Looked up on every request (no cache): a name that later starts answering with a private
    # address is refused here too. The firewall rule covers the gap between this check and connect.
    try:
        infos = socket.getaddrinfo(host, None)
        return bool(infos) and all(ipaddress.ip_address(i[4][0]).is_global for i in infos)
    except (socket.gaierror, ValueError):
        return False


def visible_text(page) -> str:
    try:
        text = page.evaluate("() => document.body ? document.body.innerText : ''") or ""
    except Exception:
        text = ""
    for frame in page.frames[1:4]:
        try:
            extra = frame.evaluate("() => document.body ? document.body.innerText : ''") or ""
            if extra.strip():
                text += "\n[frame]\n" + extra
        except Exception:
            pass
    text = re.sub(r"[ \t ]+", " ", text)
    text = re.sub(r"\n\s*\n+", "\n", text).strip()
    return text[:MAX_TEXT]


def text_inputs(page):
    found = []
    loc = page.locator("input:visible, textarea:visible")
    for i in range(min(loc.count(), 12)):
        el = loc.nth(i)
        try:
            kind = (el.get_attribute("type") or "text").lower()
            if kind in ("hidden", "password", "email", "checkbox", "radio", "file", "submit", "button",
                        "number", "range", "color", "date"):
                continue
            if el.is_disabled() or not el.is_editable():
                continue
            label = " ".join(filter(None, [el.get_attribute("placeholder"), el.get_attribute("name"),
                                           el.get_attribute("aria-label"), el.get_attribute("id")]))
            found.append((el, label))
        except Exception:
            continue
    return found


def try_fill(page, value: str) -> str:
    fields = text_inputs(page)
    if not fields:
        return "no text box on the page"
    hinted = [f for f in fields if FIELD_HINT.search(f[1])]
    el, label = (hinted or fields)[0]
    before = visible_text(page)
    el.fill(value)
    el.press("Enter")
    page.wait_for_timeout(3500)
    if visible_text(page) == before:
        buttons = page.locator("button:visible, input[type=submit]:visible, [role=button]:visible")
        for i in range(min(buttons.count(), 25)):
            b = buttons.nth(i)
            try:
                name = (b.inner_text() or b.get_attribute("value") or b.get_attribute("aria-label") or "").strip()
                if name and len(name) < 40 and BUTTON_HINT.search(name) and b.is_enabled():
                    b.click(timeout=3000)
                    page.wait_for_timeout(3500)
                    return f"typed into '{label[:60]}' and pressed '{name}'"
            except Exception:
                continue
        return f"typed into '{label[:60]}' and pressed Enter (page did not change)"
    return f"typed into '{label[:60]}' and pressed Enter"


def main() -> int:
    ap = argparse.ArgumentParser()
    ap.add_argument("--url", required=True)
    ap.add_argument("--proxy")
    ap.add_argument("--fill")
    ap.add_argument("--shot")
    ap.add_argument("--shot-inline", action="store_true",
                    help="return the screenshot inside the JSON (shot_b64) instead of writing a file")
    ap.add_argument("--wait", type=int, default=7000)
    a = ap.parse_args()

    out = {"ok": False, "url": a.url, "final_url": None, "status": None, "title": None, "text": "",
           "inputs": [], "fill": None, "error": None, "blocked": 0}
    u = urlparse(a.url)
    via_proxy = bool(a.proxy)
    if u.scheme not in ("http", "https") or not host_is_public(u.hostname or "", via_proxy):
        out["error"] = "URL not allowed (must be a public http/https address)"
        print(json.dumps(out))
        return 0

    with sync_playwright() as p:
        browser = p.chromium.launch(
            headless=True,
            chromium_sandbox=True,
            proxy={"server": a.proxy} if a.proxy else None,
            args=["--disable-dev-shm-usage", "--no-first-run", "--disable-background-networking",
                  "--disable-extensions", "--js-flags=--max-old-space-size=384"])
        try:
            ctx = browser.new_context(user_agent=UA, viewport={"width": 1280, "height": 1600},
                                      accept_downloads=False, service_workers="block", locale="en-US")

            def guard(route):
                r = urlparse(route.request.url)
                if r.scheme in ("data", "blob", "about"):
                    return route.continue_()
                if r.scheme not in ("http", "https") or not host_is_public(r.hostname or "", via_proxy):
                    out["blocked"] += 1
                    return route.abort()
                if route.request.resource_type == "media":
                    return route.abort()
                return route.continue_()

            ctx.route("**/*", guard)
            # WebSockets do not pass through the route above; an order page has no need for them.
            ctx.route_web_socket("**/*", lambda ws: ws.close())
            page = ctx.new_page()
            page.set_default_timeout(20000)
            try:
                resp = page.goto(a.url, wait_until="domcontentloaded", timeout=90000 if via_proxy else 40000)
                out["status"] = resp.status if resp else None
            except PwTimeout:
                out["error"] = "timed out loading the page"
            except Exception as e:  # noqa: BLE001
                out["error"] = "could not load the page: " + str(e).splitlines()[0][:200]
            if out["error"] is None or page.url != "about:blank":
                try:
                    page.wait_for_load_state("networkidle", timeout=a.wait)
                except PwTimeout:
                    pass
                page.wait_for_timeout(1500)
                # Some order pages only work for a browser that has already been on the site
                # (they read settings the home page stores). When the first load shows an error
                # page or bounces to the home page, visit the home page and open the link again.
                first = visible_text(page)
                bounced = urlparse(page.url).path in ("", "/") and u.path not in ("", "/")
                if bounced or len(first) < 400 or re.search(r"couldn.t load|something went wrong|application error", first, re.I):
                    try:
                        page.goto(f"{u.scheme}://{u.netloc}/", wait_until="domcontentloaded", timeout=40000)
                        page.wait_for_timeout(2500)
                        resp = page.goto(a.url, wait_until="domcontentloaded", timeout=40000)
                        out["status"] = resp.status if resp else out["status"]
                        out["retried_via_home"] = True
                        try:
                            page.wait_for_load_state("networkidle", timeout=a.wait)
                        except PwTimeout:
                            pass
                        page.wait_for_timeout(1500)
                    except Exception:  # noqa: BLE001
                        pass
                if a.fill:
                    try:
                        out["fill"] = try_fill(page, a.fill)
                    except Exception as e:  # noqa: BLE001
                        out["fill"] = "could not type the value: " + str(e).splitlines()[0][:160]
                    try:
                        page.wait_for_load_state("networkidle", timeout=5000)
                    except PwTimeout:
                        pass
                out["final_url"] = (page.url or "")[:MAX_URL]
                try:
                    out["title"] = (page.title() or "")[:200]
                except Exception:
                    pass
                out["text"] = visible_text(page)
                out["inputs"] = [label[:80] for _, label in text_inputs(page)][:8]
                if a.shot:
                    try:
                        page.screenshot(path=a.shot, full_page=False, type="jpeg", quality=60)
                    except Exception:
                        pass
                elif a.shot_inline:
                    try:
                        shot = page.screenshot(full_page=False, type="jpeg", quality=60)
                        if len(shot) < 4_000_000:
                            out["shot_b64"] = base64.b64encode(shot).decode("ascii")
                    except Exception:
                        pass
                out["ok"] = bool(out["text"])
        finally:
            browser.close()
    print(json.dumps(out))
    return 0


if __name__ == "__main__":
    sys.exit(main())
