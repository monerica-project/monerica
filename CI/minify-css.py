#!/usr/bin/env python3
"""Build a fully minified stylesheet from the readable one.

    python3 CI/minify-css.py src/DirectoryManager/DirectoryManager.Web/wwwroot/css/2026-11-10-3.css

writes 2026-11-10-3.min.css next to it. The readable .css is the source of truth; never hand-edit a
.min.css. Safe by construction: quoted strings and url(...) values are set aside first and put back
untouched, spaces are only removed where CSS never needs them (around { } ; , and after a colon that
is inside a declaration), and spacing inside calc()/min()/max()/clamp() is left alone.
"""
import re
import sys


def minify(css: str) -> str:
    kept = []

    def keep(m):
        kept.append(m.group(0))
        return f"\x00{len(kept) - 1}\x00"

    # 1) set aside strings and url(...) so nothing inside them is touched (comments first: they may hold quotes)
    css = re.sub(r"/\*.*?\*/", "", css, flags=re.S)
    css = re.sub(r'"(?:\\.|[^"\\])*"|\'(?:\\.|[^\'\\])*\'|url\(\s*[^)"\']*\)', keep, css)

    css = re.sub(r"\s+", " ", css)
    out = []
    depth = 0          # brace depth
    in_decl = False    # inside "{ ... }" of a style rule (declarations), not an at-rule prelude
    i = 0
    # Walk rule by rule so "a :hover" (selector) is never confused with "color: red" (declaration).
    parts = re.split(r"([{};])", css)
    stack = []         # True for a block that holds declarations, False for one that holds rules
    prelude = True
    for tok in parts:
        if tok == "{":
            sel = out.pop().strip() if out else ""
            holds_rules = bool(re.match(r"@(media|supports|container|layer|document)\b", sel)) or sel.startswith("@keyframes") or sel.startswith("@-webkit-keyframes")
            if sel.startswith("@"):
                sel = re.sub(r"\s*([,:])\s*", r"\1", sel) if not holds_rules else re.sub(r"\(\s*([a-z-]+)\s*:\s*", r"(\1:", sel)
            else:
                sel = re.sub(r"\s*([,>])\s*", r"\1", sel)
            out.append(sel + "{")
            stack.append(not holds_rules)
        elif tok == "}":
            while out and out[-1] in (";", ""):
                out.pop()
            out.append("}")
            if stack:
                stack.pop()
        elif tok == ";":
            if out and out[-1] not in (";", ) and not out[-1].endswith("{"):
                out.append(";")
        else:
            t = tok.strip()
            if not t:
                continue
            if stack and stack[-1]:      # a declaration: "prop : value"
                t = re.sub(r"^([-\w]+)\s*:\s*", r"\1:", t)
                t = re.sub(r"\s*,\s*", ",", t)
                t = re.sub(r"\s*!\s*important", "!important", t)
            out.append(t)
    css = "".join(out)
    css = re.sub(r"\x00(\d+)\x00", lambda m: kept[int(m.group(1))], css)
    return css


if __name__ == "__main__":
    src = sys.argv[1]
    assert src.endswith(".css") and not src.endswith(".min.css"), "pass the readable .css file"
    text = open(src, encoding="utf-8").read()
    small = minify(text)
    assert small.count("{") == small.count("}"), "brace mismatch after minifying"
    assert "\n" not in small
    dest = src[:-4] + ".min.css"
    open(dest, "w", encoding="utf-8").write(small)
    print(f"{src}: {len(text)} bytes -> {dest}: {len(small)} bytes")
