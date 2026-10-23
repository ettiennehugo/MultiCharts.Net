#!/usr/bin/env python3
"""M-EVENT (spec rev 2.1, EV-4.5) table builder: generates the `// <event-table>` region of _EventCalendar.Function.CS.

Usage:  python3 tools/m-event/build_table.py [--offline] [--from 2016] [--to 2027] [--check]
  --offline  parse only the cached pages under tools/m-event/raw/ (reproducible build; default if every page is cached)
  --refetch  fetch every page again (updates raw/ and raw/manifest.json)
  --check    do not write; exit 1 if the generated region differs from the file

Source chain per item: official page first; else a Wayback capture of the same URL (timestamp recorded); else
ALFRED/FRED (second source). Nothing is typed by hand: hand-sourced corrections live in corrections.json with a URL each
and are checked against the parsed pages. Items with no source are REFUSED (reported, not written).
Python 3 stdlib (+ openpyxl for the Census xlsx).
"""
import argparse, csv, datetime as dt, hashlib, html, io, json, os, re, subprocess, sys

VERSION = "1.1.0"
HERE = os.path.dirname(os.path.abspath(__file__))
ROOT = os.path.abspath(os.path.join(HERE, "..", ".."))
RAW = os.path.join(HERE, "raw")
OUT = os.path.join(HERE, "out")
CS = os.path.join(ROOT, "_EventCalendar.Function.CS")
UA = "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/124.0 Safari/537.36"

KIND = dict(FomcDecision=1, FomcPress=2, FomcMinutes=3, Cpi=4, Ppi=5, Nfp=6, Jolts=7, Claims=8, Pce=9, GdpAdvance=10,
            GdpSecond=11, GdpThird=12, RetailSales=13, IsmMfg=14, IsmSvc=15, JacksonHole=16, ChairTestimony=17)
FLAG = dict(Sep=1, Quad=2, Unscheduled=4, Scheduled=8, Delayed=16, Canceled=32, Csv=64, RuleOnly=128)
MONTHS = {m: i for i, m in enumerate(["January", "February", "March", "April", "May", "June", "July", "August", "September",
                                      "October", "November", "December"], 1)}
LAPSE_2025_START = "2025-10-01 00:01"   # EV-5.3 / EV-5.5d (BLS CPI shutdown FAQ)

def mnum_of(n): return next(v for k, v in MONTHS.items() if k.startswith(n[:3]))
def fed_hist(y): return "https://www.federalreserve.gov/monetarypolicy/fomchistorical%d.htm" % y
SOURCES = {   # cache name -> URL
    "fed_calendars.html": "https://www.federalreserve.gov/monetarypolicy/fomccalendars.htm",
    "bls_lapse.html": "https://www.bls.gov/bls/2025-lapse-revised-release-dates.htm",
    "bea_2025.html": "https://www.bea.gov/news/schedule/full-2025",
    "bea_2026.html": "https://www.bea.gov/news/schedule/full",
    "bea_2027.html": "https://www.bea.gov/news/schedule/next-year",
    "census_marts.xlsx": "https://www.census.gov/retail/marts/www/MARTSreleasedates.xlsx",
    "dol_claims.html": "https://oui.doleta.gov/unemploy/claims_arch.asp",
    "eia_wpsr_archive.html": "https://www.eia.gov/petroleum/supply/weekly/archive/",
    "cme_es_calendar.json": "https://www.cmegroup.com/CmeWS/mvc/ProductCalendar/Future/133",
    "cme_rolldates.html": "https://www.cmegroup.com/trading/equity-index/rolldates.html",
    "cboe_vx_specs.html": "https://www.cboe.com/tradable_products/vix/vix_futures/specifications/",
    "fed_pr_20200303.html": "https://www.federalreserve.gov/newsevents/pressreleases/monetary20200303a.htm",
    "fed_pr_20200315.html": "https://www.federalreserve.gov/newsevents/pressreleases/monetary20200315a.htm",
}
for _y in range(2016, 2021): SOURCES["fed_hist_%d.html" % _y] = fed_hist(_y)
for _y in range(2016, 2028): SOURCES["bls_%d.html" % _y] = "https://www.bls.gov/schedule/%d/home.htm" % _y
ALFRED = dict(Cpi=10, Ppi=46, Nfp=50, Jolts=192, Pce=54, Gdp=53, RetailSales=9, Claims=180)
for _k, _r in ALFRED.items(): SOURCES["alfred_%d.txt" % _r] = "https://alfred.stlouisfed.org/release/downloaddates?rid=%d&ff=txt" % _r
# Evidence read through a Wayback capture (box TLS to archive.org is blocked; captured URL + timestamp recorded, not cached)
WAYBACK = {
    # Earliest capture showing the Monday roll rule (PR27 review, analyst1 CDX reading). Captures 20220521, 20220608 and
    # 20220609055445 still show ESM22 roll Thu 6/9/22 (Sep 9/8), so the published ESM22 roll was 6/9: cutover after ESM22.
    "cme_rolldates_20220610": ("https://web.archive.org/web/20220610060819/https://www.cmegroup.com/trading/equity-index/rolldates.html",
                               "2022-06-10 06:08:19 UTC", "first capture with the Monday rule: 2022-09 roll Mon 9/12; 2022-12 Mon 12/12 (ESM22 had already rolled Thu 6/9)"),
    "cme_rolldates_20220609": ("https://web.archive.org/web/20220609055445/https://www.cmegroup.com/trading/equity-index/rolldates.html",
                               "2022-06-09 05:54:45 UTC", "last capture with the Thursday rule: 2022-06 roll Thu 6/9; 2022-09 Thu 9/8"),
}

# ------------------------------------------------------------------ fetch / cache
def manifest_path(): return os.path.join(RAW, "manifest.json")
def load_manifest():
    try: return json.load(open(manifest_path()))
    except Exception: return {}

def fetch(name, url, man, refetch):
    p = os.path.join(RAW, name)
    if os.path.exists(p) and not refetch: return open(p, "rb").read()
    if ARGS.offline: raise SystemExit("offline and not cached: " + name)
    r = subprocess.run(["curl", "-s", "--compressed", "-L", "-m", "90", "-H", "User-Agent: " + UA, "-H", "Accept-Language: en-US,en;q=0.9",
                        "-w", "%{http_code}", "-o", p, url], capture_output=True, text=True)
    if r.stdout.strip() != "200": raise SystemExit("fetch %s -> HTTP %s" % (url, r.stdout))
    b = open(p, "rb").read()
    man[name] = dict(url=url, fetched_utc=dt.datetime.utcnow().strftime("%Y-%m-%d %H:%M"), sha256=hashlib.sha256(b).hexdigest(), bytes=len(b))
    return b

def text_of(b):
    s = b.decode("utf-8", "replace")
    s = re.sub(r"(?is)<(script|style)[^>]*>.*?</\1>", "", s)
    s = re.sub(r"(?i)<br\s*/?>|</(p|div|tr|li|h\d|td|th)>", "\n", s)
    s = html.unescape(re.sub(r"<[^>]+>", " ", s))
    return [l for l in (re.sub(r"[ \t\xa0]+", " ", x).strip() for x in s.split("\n")) if l]

# ------------------------------------------------------------------ model
class Ev:
    def __init__(s, kind, et, ref, src, flags=0):
        s.kind, s.et, s.ref, s.src, s.flags = kind, et, ref, src, flags
    def key(s): return (s.kind, s.et)

def T(d, hm): return dt.datetime(d.year, d.month, d.day, int(hm[:2]), int(hm[3:5]))
def parse_ampm(t):
    m = re.match(r"(\d{1,2}):(\d{2})\s*([AP])M", t.strip(), re.I)
    h = int(m.group(1)) % 12 + (12 if m.group(3).upper() == "P" else 0)
    return "%02d:%s" % (h, m.group(2))
def ymdhm(d): return d.year * 100000000 + d.month * 1000000 + d.day * 10000 + d.hour * 100 + d.minute
def pdt(s): return dt.datetime.strptime(s, "%Y-%m-%d %H:%M") if s else None

REPORT = {"refused": [], "unresolved": [], "verify": [], "info": [], "disagree": [], "mismatch": []}
HOLES = []   # EV-4.6 coverage holes: (kind, first ET minute, last ET minute, why); emitted as s_coverageHoles
GDPK = ("GdpAdvance", "GdpSecond", "GdpThird")
def month_span(y, m):
    a = dt.datetime(y, m, 1); b = (a + dt.timedelta(days=32)).replace(day=1) - dt.timedelta(minutes=1); return a, b
def day_span(d): a = dt.datetime(d.year, d.month, d.day); return a, a + dt.timedelta(hours=23, minutes=59)
def refuse(what, why, kinds=(), span=None):
    """A refused item. With kinds + span (ET datetimes) it is also a coverage hole the engine fails closed on (EV-4.6)."""
    REPORT["refused"].append("%s: %s" % (what, why))
    for k in kinds: HOLES.append((k, span[0], span[1], "%s: %s" % (what, why)))

# ------------------------------------------------------------------ Fed
def fed(pages, y0, y1):
    ev = []
    src_cal = SOURCES["fed_calendars.html"]
    s = pages["fed_calendars.html"].decode("utf-8", "replace")
    parts = re.split(r"(\d{4}) FOMC Meetings</a>", s)
    today = TODAY
    for i in range(1, len(parts), 2):
        y = int(parts[i]); body = parts[i + 1]
        if y < 2021 or not (y0 <= y <= y1): continue
        blocks = re.split(r'<div class="[^"]*row fomc-meeting"', body)[1:]
        for b in blocks:
            mo = re.search(r"fomc-meeting__month[^>]*><strong>([^<]+)</strong>", b).group(1).strip()
            da = re.search(r"fomc-meeting__date[^>]*>([^<]+)<", b).group(1).strip()
            if "notation" in da or "unscheduled" in da.lower(): 
                if "unscheduled" in da.lower(): refuse("FOMC %d %s %s" % (y, mo, da), "unscheduled meeting on the current calendar page: parse not implemented")
                continue
            sep = da.endswith("*"); da = da.rstrip("*").strip()
            m2 = mo.split("/")[-1]; d2 = int(da.split("-")[-1])
            mnum = MONTHS[next(k for k in MONTHS if k.startswith(m2[:3]))]
            dec = dt.date(y, mnum, d2)
            st = re.search(r"monetary(\d{8})a\.htm", b)
            if st:
                sd = dt.datetime.strptime(st.group(1), "%Y%m%d").date()
                if sd != dec: REPORT["disagree"].append("FOMC %s: statement link %s != meeting day 2" % (dec, sd))
                dec = sd
            ref = dec.isoformat()
            ev.append(Ev("FomcDecision", T(dec, "14:00"), ref, src_cal, FLAG["Sep"] if sep else 0))
            if re.search(r"fomcpresconf\d{8}\.htm", b): ev.append(Ev("FomcPress", T(dec, "14:30"), ref, src_cal))
            elif dec > today: refuse("FomcPress %s" % ref, "future meeting: the calendar page lists no press conference yet (no source)", ("FomcPress",), day_span(dec))
            mr = re.search(r"\(Released ([A-Z][a-z]+) (\d{1,2}), (\d{4})\)", b)
            if mr: md = dt.date(int(mr.group(3)), MONTHS[mr.group(1)], int(mr.group(2)))
            elif dec > today - dt.timedelta(days=21):
                md = dec + dt.timedelta(days=21)   # Fed page wording: "released three weeks after the date of the policy decision"
                REPORT["info"].append("FomcMinutes %s: decision + 21 days (calendar page wording; not yet released)" % ref)
            else: refuse("FomcMinutes %s" % ref, "no release date on the page"); continue
            ev.append(Ev("FomcMinutes", T(md, "14:00"), ref, src_cal))
    for y in range(max(2016, y0), min(2020, y1) + 1):
        s = pages["fed_hist_%d.html" % y].decode("utf-8", "replace"); src = fed_hist(y)
        hs = list(re.finditer(r'<h5 class="panel-heading[^"]*">(.*?)</h5>', s, re.S))
        for j, h in enumerate(hs):
            head = re.sub(r"\s+", " ", h.group(1)).strip()
            b = s[h.end(): hs[j + 1].start() if j + 1 < len(hs) else len(s)]
            if "notation vote" in head: continue
            if head.startswith("October 4 (unscheduled)") and y == 2019:
                # unscheduled call; statement "Released October 11, 2019" with no time on the page and not a rate decision
                refuse("FomcDecision 2019-10-04 (unscheduled)", "statement released 2019-10-11, no time on the page; not a rate decision", ("FomcDecision",), day_span(dt.date(2019, 10, 11))); continue
            m = re.match(r"([A-Z][a-z]+)(?:/([A-Z][a-z]+))? (\d{1,2})(?:-(\d{1,2}))? ?(\((unscheduled|cancelled|conference call)\))? Meeting", head)
            if not m: REPORT["disagree"].append("Fed %d: unparsed heading %r" % (y, head)); continue
            mon = mnum_of(m.group(2) or m.group(1)); d2 = int(m.group(4) or m.group(3)); tag = m.group(6)
            day2 = dt.date(y, mon, d2)
            st = re.search(r"monetary(\d{8})a\.htm", b)
            ref = day2.isoformat()
            if tag == "cancelled":
                ev.append(Ev("FomcDecision", T(day2, "14:00"), ref, src, 0))
                ev.append(Ev("FomcPress", T(day2, "14:30"), ref, src, 0))   # canceled by corrections.json (EV-5.5c)
                continue
            if not st: refuse("FOMC %s" % head, "no statement link"); continue
            sd = dt.datetime.strptime(st.group(1), "%Y%m%d").date(); ref = sd.isoformat()
            if tag == "unscheduled":
                prk = "fed_pr_%s.html" % st.group(1)
                if prk not in pages: refuse("FomcDecision %s (unscheduled)" % ref, "press release not cached"); continue
                ft = re.search(r"For release at (\d{1,2}):(\d{2}) ([ap])\.m\.", pages[prk].decode("utf-8", "replace"))
                hm = "%02d:%s" % (int(ft.group(1)) % 12 + (12 if ft.group(3) == "p" else 0), ft.group(2))
                ev.append(Ev("FomcDecision", T(sd, hm), ref, SOURCES[prk], FLAG["Unscheduled"]))
                if "fomcpresconf" in b: refuse("FomcPress %s (unscheduled)" % ref, "fomcpresconf page carries no time", ("FomcPress",), day_span(sd))
            else:
                sep = "SEP:" in re.sub(r"<[^>]+>", " ", b) or "Projection Materials" in b
                ev.append(Ev("FomcDecision", T(sd, "14:00"), ref, src, FLAG["Sep"] if sep else 0))
                if re.search(r"fomcpresconf\d{8}\.htm", b): ev.append(Ev("FomcPress", T(sd, "14:30"), ref, src))
            # NOTE 2020-04-08: the minutes of the unscheduled 2020-03-15 meeting are flagged Unscheduled (no published date
            # in advance), so they are known only at their instant even though the meeting was known from 03-15 17:00.
            mr = re.search(r"Minutes[^(]{0,40}\(Released ([A-Z][a-z]+) (\d{1,2}), (\d{4})\)", re.sub(r"<[^>]+>", " ", b))
            if mr: ev.append(Ev("FomcMinutes", T(dt.date(int(mr.group(3)), MONTHS[mr.group(1)], int(mr.group(2))), "14:00"), ref, src,
                                FLAG["Unscheduled"] if tag else 0))
            elif tag: REPORT["info"].append("FOMC %s %s: minutes in another meeting's minutes" % (ref, tag))
            else: refuse("FomcMinutes %s" % ref, "no release date on the page")
    return ev

# ------------------------------------------------------------------ BLS
BLS_KIND = [("Consumer Price Index", "Cpi"), ("Producer Price Index", "Ppi"), ("Employment Situation", "Nfp"),
            ("Job Openings and Labor Turnover Survey", "Jolts")]
def bls(pages, y0, y1):
    ev = []
    for y in range(max(2016, y0), min(2027, y1) + 1):
        L = text_of(pages["bls_%d.html" % y]); src = SOURCES["bls_%d.html" % y]
        for i in range(2, len(L)):
            m = re.match(r"(Consumer Price Index|Producer Price Index|Employment Situation|Job Openings and Labor Turnover Survey) for ([A-Z][a-z]+) (\d{4})$", L[i])
            if not m: continue
            dm = re.match(r"[A-Z][a-z]+day, ([A-Z][a-z]+) (\d{1,2}), (\d{4})$", L[i - 2])
            if not dm or not re.match(r"\d{1,2}:\d{2} [AP]M$", L[i - 1]): REPORT["disagree"].append("BLS %d: bad row %r" % (y, L[i - 2:i + 1])); continue
            d = dt.date(int(dm.group(3)), MONTHS[dm.group(1)], int(dm.group(2)))
            k = dict(BLS_KIND)[m.group(1)]
            ev.append(Ev(k, T(d, parse_ampm(L[i - 1])), "%s-%02d" % (m.group(3), MONTHS[m.group(2)]), src))
    return ev

def bls_lapse(pages):
    """(kind, ref, original, actual|None) rows of the BLS revised-dates page."""
    s = pages["bls_lapse.html"].decode("utf-8", "replace"); out = []
    for t in re.findall(r"<table.*?</table>", s, re.S):
        for tr in re.findall(r"<tr.*?</tr>", t, re.S):
            c = [html.unescape(re.sub(r"<[^>]+>|\s+", " ", x)).strip() for x in re.findall(r"<t[hd][^>]*>(.*?)</t[hd]>", tr, re.S)]
            if len(c) < 4 or c[0] == "Release": continue
            name = re.sub(r"\s*\(.*?\)", "", c[0]).strip()
            k = {"Employment Situation": "Nfp", "Consumer Price Index": "Cpi", "Producer Price Index": "Ppi", "Job Openings and Labor Turnover": "Jolts"}.get(name)
            if not k or c[0].startswith("State"): continue
            rm = re.match(r"([A-Z][a-z]+) (\d{4})", c[1]); ref = "%s-%02d" % (rm.group(2), MONTHS[rm.group(1)])
            def pd(x): m = re.search(r"([A-Z][a-z]+) (\d{1,2}), (\d{4})", x); return dt.date(int(m.group(3)), MONTHS[m.group(1)], int(m.group(2))) if m else None
            if c[3].startswith("NO CHANGE"): continue
            tm = parse_ampm(c[4].replace(" ET", "")) if len(c) > 4 and c[4] else None
            orig = pd(c[2]); act = None if c[3].startswith("Canceled") else pd(c[3])
            out.append((k, ref, orig, act, tm))
    return out

# ------------------------------------------------------------------ BEA
def bea(pages, y0, y1):
    ev, canceled = [], []
    for y, name in ((2025, "bea_2025.html"), (2026, "bea_2026.html"), (2027, "bea_2027.html")):
        if not (y0 <= y <= y1): continue
        L = text_of(pages[name]); src = SOURCES[name]
        for i in range(3, len(L)):
            t = L[i]
            if not re.match(r"(Gross Domestic Product|GDP \(|Personal Income and Outlays,)", t) or "by State" in t or "by Industry," in t[:45]: continue
            dm = re.match(r"([A-Z][a-z]+) (\d{1,2})$", L[i - 3])
            if not dm or L[i - 1] != "N ews": REPORT["disagree"].append("BEA %d: bad row %r" % (y, L[i - 3:i + 1])); continue
            d = dt.date(y, MONTHS[dm.group(1)], int(dm.group(2))); et = T(d, parse_ampm(L[i - 2]))
            released = i + 1 < len(L) and L[i + 1] == "View"
            if t.startswith("Personal Income"):
                rm = re.findall(r"([A-Z][a-z]+) (\d{4})", t.split(",", 1)[1]); rm2 = re.search(r"([A-Z][a-z]+) and ([A-Z][a-z]+) (\d{4})", t)
                ref = "%s-%02d" % (rm[-1][1], MONTHS[rm[-1][0]]) if not rm2 else "%s-%02d/%02d" % (rm2.group(3), MONTHS[rm2.group(1)], MONTHS[rm2.group(2)])
                k = "Pce"
            else:
                q = re.search(r"(\d)(?:st|nd|rd|th) Quarter(?: and Year)? (\d{4})", t)
                if not q or not re.search(r"\((Advance|Second|Third|Initial|Updated) Estimate\)", t):
                    REPORT["info"].append("BEA skipped title %r" % t); continue
                ref = "%sQ%s" % (q.group(2), q.group(1))
                est = re.search(r"\((Advance|Second|Third|Initial|Updated) Estimate\)", t).group(1)
                k = {"Advance": "GdpAdvance", "Second": "GdpSecond", "Third": "GdpThird", "Initial": "GdpAdvance"}.get(est)
                if est == "Updated": refuse("GDP %s Updated Estimate %s" % (ref, et), "lapse-era 'Updated' estimate has no Advance/Second/Third kind", GDPK, day_span(et.date())); continue
                if est == "Initial": REPORT["info"].append("GDP %s 'Initial' estimate %s mapped to GdpAdvance (replaces the never-released advance)" % (ref, et))
            e = Ev(k, et, ref, src)
            if d < TODAY and not released: canceled.append(e)
            else: ev.append(e)
    return ev, canceled

def alfred_dates(pages, rid):
    out = []
    for l in pages["alfred_%d.txt" % rid].decode("utf-8", "replace").split("\n"):
        if re.match(r"^\d{4}-\d{2}-\d{2}\s*$", l): out.append(dt.date.fromisoformat(l.strip()))
    return out

NOTES = [
    "time assumed 08:30 (not in the source): BEA GdpAdvance/GdpSecond/GdpThird/Pce rows 2016-2024 (ALFRED dates only) and every RetailSales row (Census sheet has dates only).",
    "GdpSecond 2019-02-28 (2018Q4): (m-1)%3 tags it Second; it was the shutdown 'Initial' estimate. s_corrections moves GdpAdvance 2018Q4 to 02-28 and cancels GdpSecond 2018Q4.",
    "FomcMinutes 2020-04-08 (ref 2020-03-15) is flagged Unscheduled: minutes of an unscheduled meeting, known only at the instant.",
    "FomcDecision 2020-03-18 (canceled in s_corrections) carries no Sep flag: the Fed's 2020 page lists it only as 'March 17-18 (cancelled)' with no SEP; it was a scheduled SEP meeting (EV-8.2 counts 2020 published SEP = 3). Canceled, so the flag has no runtime effect.",
]
ASSUMED = " (time assumed 08:30: ALFRED gives no time)"
ASSUMED_CENSUS = " (time assumed 08:30: the Census release-date sheet gives no time)"
def bea_alfred(pages, y0, y1):
    """BEA 2016-2024: BEA's archive pages are JS-rendered / 404, so ALFRED (second source) gives the dates. Time 08:30
    (BEA's GDP/PIO release time, ALFRED gives dates only: reported as unresolved)."""
    ev = []
    gdp = [d for d in alfred_dates(pages, 53) if 2016 <= d.year <= 2024 and y0 <= d.year <= y1]
    pio = [d for d in alfred_dates(pages, 54) if 2016 <= d.year <= 2024 and y0 <= d.year <= y1]
    src_g, src_p = SOURCES["alfred_53.txt"], SOURCES["alfred_54.txt"]
    bym = {}
    for d in gdp: bym.setdefault((d.year, d.month), []).append(d)
    for (y, m), ds in sorted(bym.items()):
        if len(ds) != 1: refuse("GDP %d-%02d" % (y, m), "ALFRED lists %d dates in the month (%s): estimate ambiguous" % (len(ds), ",".join(map(str, ds))), GDPK, month_span(y, m)); continue
        d = ds[0]; phase = (m - 1) % 3   # Jan/Apr/Jul/Oct advance of the prior quarter
        qy, qq = (y - 1, 4) if m <= 3 else (y, (m - 1) // 3)
        k = ("GdpAdvance", "GdpSecond", "GdpThird")[phase]
        # NOTE 2019-02-28: (m-1)%3 tags it GdpSecond 2018Q4, but it was the shutdown "Initial" estimate; corrections.json
        # (GdpAdvance 2018Q4 -> 02-28, GdpSecond 2018Q4 canceled) carries the real history.
        ev.append(Ev(k, T(d, "08:30"), "%dQ%d" % (qy, qq), src_g + ASSUMED))
    for y in range(2016, 2025):
        if not (y0 <= y <= y1): continue
        for m in range(1, 13):
            if not any((x.year, x.month) == (y, m) for x in gdp): refuse("GDP %d-%02d" % (y, m), "no ALFRED date in the month", GDPK, month_span(y, m))
    pm = {}
    for d in pio: pm.setdefault((d.year, d.month), []).append(d)
    gset = set(gdp)
    for (y, m), ds in sorted(pm.items()):
        if len(ds) != 1: refuse("PCE %d-%02d" % (y, m), "ALFRED lists %d dates (%s)" % (len(ds), ",".join(map(str, ds))), ("Pce",), month_span(y, m)); continue
        d = ds[0]
        if d in gset: refuse("PCE %s" % d, "same day as a GDP release: BEA time (08:30 or 10:00) not verifiable from ALFRED", ("Pce",), day_span(d)); continue
        r = dt.date(y, m, 1) - dt.timedelta(days=1)
        ev.append(Ev("Pce", T(d, "08:30"), "%d-%02d" % (r.year, r.month), src_p + ASSUMED))
    return ev

# ------------------------------------------------------------------ Census
def census(pages, y0, y1):
    import openpyxl
    wb = openpyxl.load_workbook(io.BytesIO(pages["census_marts.xlsx"]), data_only=True); ws = wb.active
    rows = []   # (date, starred)
    for r in ws.iter_rows(values_only=True):
        if not isinstance(r[0], int) or r[0] < 2015: continue
        for m in range(1, 13):
            v = r[m]
            if v is None or v == "N/A": continue
            for part in str(v).split(","):
                part = part.strip(); rows.append((dt.date(r[0], m, int(part.rstrip("*"))), part.endswith("**")))
    rows.sort(); ev = []; delayed = []
    ref = None
    for d, star in rows:
        ref = (d.year, d.month - 1) if ref is None else (ref[0] + (ref[1] == 12), ref[1] % 12 + 1)
        if ref[1] == 0: ref = (ref[0] - 1, 12)
        if not star and (d.year * 12 + d.month) - (ref[0] * 12 + ref[1]) != 1:
            REPORT["disagree"].append("Census %s: sequential ref %d-%02d is not one month lag" % (d, ref[0], ref[1]))
        if not (y0 <= d.year <= y1) or d.year < 2016: continue
        e = Ev("RetailSales", T(d, "08:30"), "%d-%02d" % ref, SOURCES["census_marts.xlsx"] + ASSUMED_CENSUS)
        (delayed if star else ev).append(e)
    return ev, delayed

# ------------------------------------------------------------------ Claims (rule check vs ALFRED)
def nyse_fed_holiday(d):
    def obs(x): return x - dt.timedelta(1) if x.weekday() == 5 else x + dt.timedelta(1) if x.weekday() == 6 else x
    def nth(y, m, wd, n): f = dt.date(y, m, 1); return f + dt.timedelta(((wd - f.weekday()) % 7) + 7 * (n - 1))
    def last(y, m, wd): l = dt.date(y, m + 1, 1) - dt.timedelta(1) if m < 12 else dt.date(y, 12, 31); return l - dt.timedelta((l.weekday() - wd) % 7)
    y = d.year
    s = {obs(dt.date(y, 1, 1)), obs(dt.date(y + 1, 1, 1)), nth(y, 1, 0, 3), nth(y, 2, 0, 3), last(y, 5, 0), obs(dt.date(y, 7, 4)),
         nth(y, 9, 0, 1), nth(y, 10, 0, 2), obs(dt.date(y, 11, 11)), nth(y, 11, 3, 4), obs(dt.date(y, 12, 25))}
    if y >= 2021: s.add(obs(dt.date(y, 6, 19)))
    return d in s
def claims(pages, y0, y1):
    al = set(alfred_dates(pages, 180)); corr = []
    last = max(al)
    for y in range(max(2016, y0), y1 + 1):
        d = dt.date(y, 1, 1); d += dt.timedelta((3 - d.weekday()) % 7)
        while d.year == y and d <= last:
            rd = d - dt.timedelta(1) if nyse_fed_holiday(d) else d
            if rd not in al:
                alt = [x for x in al if 0 < (rd - x).days <= 3 or 0 < (x - rd).days <= 3]
                alt = [x for x in alt if x.weekday() != 3 or x == rd]
                if alt:
                    corr.append(dict(kind="Claims", ref="week of %s" % d, original=T(rd, "08:30"), actual=T(alt[0], "08:30"), announced=None,
                                     canceled_since=None, flags=FLAG["Unscheduled"], source=SOURCES["alfred_180.txt"] + " (DOL ETA weekly claims; announcement time unknown: known at the instant)"))
                else:
                    lapse = dt.date(2025, 10, 1) <= rd <= dt.date(2025, 11, 13)
                    corr.append(dict(kind="Claims", ref="week of %s" % d, original=T(rd, "08:30"), actual=None, announced=None,
                                     canceled_since=pdt(LAPSE_2025_START) if lapse else None, flags=0,
                                     source=(SOURCES["dol_claims.html"] + " (no releases 2025-10-02..11-13, lapse); " if lapse else "") + SOURCES["alfred_180.txt"]))
            d += dt.timedelta(7)
    return corr

# ------------------------------------------------------------------ expiry check (rule vs CME)
def third_friday(y, m): f = dt.date(y, m, 1); return f + dt.timedelta(((4 - f.weekday()) % 7) + 14)
def easter(y):
    a = y % 19; b = y // 100; c = y % 100; d = b // 4; e = b % 4; f = (b + 8) // 25; g = (b - f + 1) // 3; h = (19 * a + b - d - g + 15) % 30
    i = c // 4; k = c % 4; l = (32 + 2 * e + 2 * i - h - k) % 7; m = (a + 11 * h + 22 * l) // 451; mo = (h + l - 7 * m + 114) // 31; da = ((h + l - 7 * m + 114) % 31) + 1
    return dt.date(y, mo, da)
# NYSE holidays: ONE source. nyse_holidays.txt is exported from _NyseHolidays.Rules (C#) and EventTests asserts the file equals
# the C# rules for 2000-2035, so Python and C# cannot drift. --check also recomputes the rule set below and fails on any diff.
HOLIDAYS_TXT = os.path.join(HERE, "nyse_holidays.txt")
def load_nyse():
    return {dt.date.fromisoformat(l.strip()) for l in open(HOLIDAYS_TXT) if l.strip() and not l.startswith("#")}
NYSE_SPECIAL = (dt.date(2001, 9, 11), dt.date(2001, 9, 12), dt.date(2001, 9, 13), dt.date(2001, 9, 14),   # 9/11
                dt.date(2004, 6, 11), dt.date(2007, 1, 2),                                                # Reagan, Ford mourning
                dt.date(2012, 10, 29), dt.date(2012, 10, 30),                                             # Hurricane Sandy
                dt.date(2018, 12, 5), dt.date(2025, 1, 9))                                                # G.H.W. Bush, Carter mourning
def nyse_rule(y):
    """Independent mirror of _NyseHolidays.Rules.Holidays (used only by --check to cross-check nyse_holidays.txt)."""
    def obs(x): return x - dt.timedelta(1) if x.weekday() == 5 else x + dt.timedelta(1) if x.weekday() == 6 else x
    def nth(m, wd, n): f = dt.date(y, m, 1); return f + dt.timedelta((wd - f.weekday()) % 7 + 7 * (n - 1))
    def last(m, wd):
        l = dt.date(y, m, 28) + dt.timedelta(4); l = l - dt.timedelta(l.day); return l - dt.timedelta((l.weekday() - wd) % 7)
    h = {nth(1, 0, 3), nth(2, 0, 3), easter(y) - dt.timedelta(2), last(5, 0), obs(dt.date(y, 7, 4)), nth(9, 0, 1), nth(11, 3, 4), obs(dt.date(y, 12, 25))}
    ny = dt.date(y, 1, 1)
    if ny.weekday() != 5: h.add(obs(ny))          # NYSE: a Saturday New Year is not observed on the Friday before
    if y >= 2022: h.add(obs(dt.date(y, 6, 19)))
    # special closures exactly as _NyseHolidays.s_special (pre-2017 one-offs are out of its documented range)
    h |= {x for x in NYSE_SPECIAL if x.year == y}
    return h
NYSE = None
def nyse_closed(d): return d.weekday() >= 5 or d in NYSE
def rule_expiry(y, m):
    """R2.1-d as amended (PR27): third Friday, moved back to the preceding NYSE business day (SOQ requirement, EV-1.20)."""
    d = third_friday(y, m)
    while nyse_closed(d): d -= dt.timedelta(1)
    return d
def expiry_check(pages):
    MC = {"H": 3, "M": 6, "U": 9, "Z": 12}
    js = json.loads(pages["cme_es_calendar.json"].decode("utf-8", "replace")); cme = {}
    def walk(o):
        if isinstance(o, dict):
            c = o.get("contractCode") or o.get("productCode") or o.get("code"); ltd = o.get("lastTrade") or o.get("lastTradeDate") or o.get("lastTradingDate")
            if isinstance(c, str) and re.match(r"ES[HMUZ]\d{2}$", c) and ltd:
                v = ltd.get("date") if isinstance(ltd, dict) else ltd
                for f in ("%d %b %Y", "%m/%d/%Y", "%Y-%m-%d", "%d-%b-%Y"):
                    try: cme[c] = dt.datetime.strptime(str(v)[:11].strip(), f).date(); break
                    except ValueError: pass
            for v in o.values(): walk(v)
        elif isinstance(o, list):
            for v in o: walk(v)
    walk(js)
    rows = []
    for c, d in sorted(cme.items(), key=lambda kv: kv[1]):
        y = 2000 + int(c[3:]); m = MC[c[2]]; r = rule_expiry(y, m)
        rows.append((c, y, m, d, r))
        if d != r: REPORT["disagree"].append("%s: CME ProductCalendar last trade %s vs rule %s: fix the rule or the NYSE calendar (%s)" % (c, d, r, SOURCES["cme_es_calendar.json"]))
    return cme, rows

# ------------------------------------------------------------------ assemble
def main():
    global ARGS, TODAY
    ap = argparse.ArgumentParser(); ap.add_argument("--offline", action="store_true"); ap.add_argument("--refetch", action="store_true")
    ap.add_argument("--check", action="store_true"); ap.add_argument("--from", dest="y0", type=int, default=2016); ap.add_argument("--to", dest="y1", type=int, default=2027)
    ARGS = ap.parse_args()
    os.makedirs(RAW, exist_ok=True); os.makedirs(OUT, exist_ok=True)
    global NYSE
    NYSE = load_nyse()
    if ARGS.check:
        exp = set().union(*(nyse_rule(y) for y in range(2000, 2036)))
        if exp != NYSE: raise SystemExit("nyse_holidays.txt != NYSE rule mirror: only-file %s only-rule %s" % (sorted(NYSE - exp)[:5], sorted(exp - NYSE)[:5]))
    man = load_manifest(); pages = {}
    for n, u in SOURCES.items(): pages[n] = fetch(n, u, man, ARGS.refetch)
    if not ARGS.offline: json.dump(man, open(manifest_path(), "w"), indent=1, sort_keys=True); open(manifest_path(), "a").write("\n")
    TODAY = dt.datetime.strptime(min(v["fetched_utc"] for v in man.values())[:10], "%Y-%m-%d").date()
    y0, y1 = ARGS.y0, ARGS.y1

    table = fed(pages, y0, y1) + bls(pages, y0, y1)
    bea_ev, bea_cx = bea(pages, y0, y1); table += bea_ev + bea_alfred(pages, y0, y1)
    cen, cen_delayed = census(pages, y0, y1); table += cen
    corr = []
    # BLS lapse page
    for k, ref, orig, act, tm in bls_lapse(pages):
        if orig is None or orig.year < y0 or orig.year > y1: continue
        tm = tm or {"Jolts": "10:00"}.get(k, "08:30")
        o = T(orig, tm)
        lapse25 = orig < dt.date(2026, 1, 20)
        c = dict(kind=k, ref=ref, original=o, actual=T(act, tm) if act else None, announced=None,
                 canceled_since=pdt(LAPSE_2025_START) if lapse25 else None, flags=0 if act is None else FLAG["Unscheduled"],
                 source=SOURCES["bls_lapse.html"] + ("" if lapse25 else " (2026 lapse start unknown: canceled at the original instant)") +
                        ("" if act is None else "; revised date first-publication capture unavailable: known at the instant"))
        corr.append(c)
    # hand-sourced
    for r in json.load(open(os.path.join(HERE, "corrections.json")))["rows"]:
        f = 0
        for x in r["flags"]: f |= FLAG[x]
        corr.append(dict(kind=r["kind"], ref=r["ref"], original=pdt(r["original"]), actual=pdt(r["actual"]), announced=pdt(r["announced"]),
                         canceled_since=pdt(r["canceled_since"]), flags=f, source=r["source"], check=r["check"]))
    # BEA 2025 lapse: never-released rows + their replacements (EV-5.5f)
    for e in bea_cx:
        rep = [x for x in table if x.ref == e.ref and x.kind == e.kind and x.et > e.et]
        if len(rep) == 1:
            table.remove(rep[0])
            corr.append(dict(kind=e.kind, ref=e.ref, original=e.et, actual=rep[0].et, announced=None, canceled_since=pdt(LAPSE_2025_START),
                             flags=FLAG["Unscheduled"], source=e.src + " (original never released, lapse; announcement of the new date not capturable: known at the instant)"))
        else:
            corr.append(dict(kind=e.kind, ref=e.ref, original=e.et, actual=None, announced=None, canceled_since=pdt(LAPSE_2025_START), flags=0,
                             source=e.src + " (never released, lapse)"))
        table.append(e)
    # BEA rows released after the 2025 lapse whose original is not on the page (PIO Sep 2025, PIO Oct/Nov 2025, GDP Q3 Initial if unmatched)
    for e in list(table):
        if e.src.startswith("https://www.bea.gov") and dt.date(2025, 11, 13) <= e.et.date() <= dt.date(2026, 1, 31) and \
                (e.kind == "Pce" and e.ref in ("2025-09", "2025-10/11")):
            table.remove(e)
            corr.append(dict(kind=e.kind, ref=e.ref, original=None, actual=e.et, announced=None, canceled_since=None, flags=FLAG["Unscheduled"],
                             source=e.src + " (lapse-delayed; original date not on the page: unscheduled, known at the instant)"))
            REPORT["unresolved"].append("Pce %s %s: original date and announcement time unknown (BEA page lists only the revised date)" % (e.ref, e.et))
    # Census lapse-delayed (**) rows
    for e in cen_delayed:
        if e.ref == "2018-12": continue   # corrections.json (EV-5.5g)
        corr.append(dict(kind="RetailSales", ref=e.ref, original=None, actual=e.et, announced=None, canceled_since=None, flags=FLAG["Unscheduled"],
                         source=e.src + " ('**' = lapse-delayed; original date unknown: unscheduled, known at the instant)"))
        REPORT["unresolved"].append("RetailSales %s %s: lapse-delayed ('**'); original date / announcement unknown" % (e.ref, e.et))
    corr += claims(pages, y0, y1)

    # apply: table holds ORIGINAL instants. Remove actuals present on the pages; insert originals that are gone.
    tk = {}
    for e in table: tk.setdefault((e.kind, e.et), e)
    for c in corr:
        a = c.get("actual"); o = c.get("original")
        if c["kind"] == "Claims": continue   # rule kind
        if a is not None:
            hit = tk.pop((c["kind"], a), None)
            chk = c.get("check")
            if hit is None and chk in ("bls_actual", "census") and not any(x.kind == c["kind"] and x.et == a for x in table):
                if chk == "census" and a in [e.et for e in cen_delayed]: pass
                else: REPORT["disagree"].append("correction %s %s: actual %s not found on the source page" % (c["kind"], c["ref"], a))
            if chk == "alfred_53" and a.date() not in alfred_dates(pages, 53): REPORT["disagree"].append("correction %s %s: actual not in ALFRED" % (c["kind"], c["ref"]))
            if hit is not None: table.remove(hit)
        if o is not None and (c["kind"], o) not in tk:
            if c.get("check") == "fed_canceled": REPORT["disagree"].append("canceled FOMC original %s not parsed" % o)
            e = Ev(c["kind"], o, c["ref"], c["source"].split(" ")[0]); table.append(e); tk[(e.kind, e.et)] = e
    # ref-period duplicates without a correction (EV-4.5 step 5)
    corr_keys = {(c["kind"], c["original"]) for c in corr if c.get("original")}
    seen = {}
    for e in sorted(table, key=lambda e: e.et):
        if e.kind.startswith("Fomc"): continue
        if (e.kind, e.ref) in seen and (e.kind, e.et) not in corr_keys and (e.kind, seen[(e.kind, e.ref)].et) not in corr_keys:
            REPORT["disagree"].append("%s %s has two dates %s / %s without a correction" % (e.kind, e.ref, seen[(e.kind, e.ref)].et, e.et))
        seen[(e.kind, e.ref)] = e
    # ALFRED cross-check (membership; ALFRED also lists extra vintages)
    for k, rid in (("Cpi", 10), ("Ppi", 46), ("Nfp", 50), ("Jolts", 192), ("RetailSales", 9)):
        al = set(alfred_dates(pages, rid)); lastal = max(al)
        acts = {c["actual"].date() for c in corr if c["kind"] == k and c.get("actual")}
        for e in table:
            if e.kind != k or e.et.date() > lastal: continue
            canceled = any(c["kind"] == k and c.get("original") == e.et for c in corr)
            if not canceled and e.et.date() not in al: REPORT["disagree"].append("ALFRED rid %d lacks %s %s %s" % (rid, k, e.ref, e.et))
        for d in acts:
            if d <= lastal and d not in al: REPORT["disagree"].append("ALFRED rid %d lacks correction actual %s %s" % (rid, k, d))

    cme, exp_rows = expiry_check(pages)
    table.sort(key=lambda e: (e.et, KIND[e.kind]))
    corr.sort(key=lambda c: (c.get("original") or c.get("actual"), KIND[c["kind"]]))
    # EV-8.2-style report: times, federal holidays
    for e in table:
        if e.et.strftime("%H:%M") not in ("08:30", "10:00", "10:30", "14:00", "14:30") and not e.flags & FLAG["Unscheduled"]:
            REPORT["info"].append("odd time %s %s %s" % (e.kind, e.ref, e.et))
        if nyse_fed_holiday(e.et.date()): REPORT["info"].append("federal holiday release %s %s %s" % (e.kind, e.ref, e.et))

    # a refused span that a table or correction row of the same kind fills is not a hole
    have = [(e.kind, e.et) for e in table] + [(c["kind"], c["actual"]) for c in corr if c.get("actual")]
    seen = set(); keep = []
    for h in HOLES:
        if (h[0], h[1], h[2]) in seen or any(k == h[0] and h[1] <= t <= h[2] for k, t in have): continue
        seen.add((h[0], h[1], h[2])); keep.append(h)
    HOLES[:] = sorted(keep, key=lambda h: (h[1], KIND[h[0]]))
    region = emit(table, corr, man)
    write_csv(table, corr, y0, y1)
    src = open(CS, encoding="utf-8").read()
    a = src.index("\t// <event-table>\n"); b = src.index("\t// </event-table>\n") + len("\t// </event-table>\n")
    new = src[:a] + region + src[b:]
    print_report(table, corr, exp_rows)
    if REPORT["disagree"]:
        print("\nREFUSING TO WRITE: %d disagreement(s)" % len(REPORT["disagree"])); sys.exit(2)
    if ARGS.check:
        if new != src: print("table region out of date"); sys.exit(1)
        print("table region up to date"); return
    if new != src: open(CS, "w", encoding="utf-8", newline="\n").write(new)
    print("\nwrote %s (%d rows, %d corrections)" % (CS, len(table), len(corr)))

def cs_str(s): return "null" if s is None else '"' + s.replace("\\", "\\\\").replace('"', '\\"') + '"'
def flagexpr(f):
    if not f: return "EEventFlags.None"
    return " | ".join("EEventFlags." + n for n, v in FLAG.items() if f & v)
def emit(table, corr, man):
    shas = sorted((n, v["sha256"][:16], v["fetched_utc"]) for n, v in man.items())
    L = ["\t// <event-table>",
         "\t// GENERATED by tools/m-event/build_table.py v%s (EV-4.5) -- do not edit by hand; re-run the script." % VERSION,
         "\t// Pages fetched %s .. %s UTC (raw copies + manifest: tools/m-event/raw/). Source sha256[:16]:" % (min(s[2] for s in shas), max(s[2] for s in shas))]
    for n, h, _ in shas: L.append("\t//   %-26s %s  %s" % (n, h, man[n]["url"]))
    for k, (u, ts, what) in WAYBACK.items(): L.append("\t//   wayback %s (%s): %s" % (ts, u, what))
    for n_ in NOTES: L.append("\t// NOTE " + n_)
    L.append("\t// Coverage holes (EV-4.6, s_coverageHoles): %d refused spans; a day inside one is Unknown for that kind (EV-4.6b)." % len(HOLES))
    L += ["\tpublic static partial class EventRules", "\t{"]
    def arr(name, typ, vals, per):
        L.append("\t\tstatic readonly %s[] %s = {" % (typ, name))
        for i in range(0, len(vals), per): L.append("\t\t\t" + ", ".join(vals[i:i + per]) + ",")
        L.append("\t\t};")
    arr("s_yyyymmddhhmm", "long", [str(ymdhm(e.et)) for e in table], 8)
    arr("s_kind", "byte", [str(KIND[e.kind]) for e in table], 24)
    arr("s_flags", "ushort", [str(e.flags) for e in table], 24)
    arr("s_ref", "string", [cs_str(e.ref) for e in table], 8)
    L.append("\t\t/// <summary>EV-4.6 coverage holes from refused items: kind, first ET minute, last ET minute (yyyyMMddHHmm).</summary>")
    arr("s_coverageHoles", "long", ["%d, %d, %d" % (KIND[k], ymdhm(a), ymdhm(b)) for k, a, b, _ in HOLES], 4)
    L.append("\t\tstatic readonly string[] s_coverageHoleWhy = {")
    for k, a, b, why in HOLES: L.append("\t\t\t%s," % cs_str(why))
    L.append("\t\t};")
    # EV-4.6b: expected hash (mirror of EventRules.ComputeTableSha); a mismatch at runtime disables the engine
    y_ = lambda d: ymdhm(d) if d else 0
    hs = "".join("%d,%d,%d,%s;" % (ymdhm(e.et), KIND[e.kind], e.flags, e.ref or "") for e in table)
    hs += "".join("%s,%d,%d,%d,%d,%d;" % (c["kind"], y_(c.get("original")), y_(c.get("actual")), y_(c.get("announced")), y_(c.get("canceled_since")), c["flags"]) for c in corr)
    if HOLES: hs += "holes" + "".join(",%d,%d,%d" % (KIND[k], ymdhm(a), ymdhm(b)) for k, a, b, _ in HOLES)
    L.append("\t\tstatic readonly string s_expectedTableSha = \"%s\";" % hashlib.sha256(hs.encode("utf-8")).hexdigest()[:16])
    L.append("\t\tstatic readonly EventCorrection[] s_corrections = {")
    for c in corr:
        L.append("\t\t\tnew EventCorrection { kind = EEventKind.%s, refPeriod = %s, originalEt = %d, actualEt = %d, announcedEt = %d, canceledSinceEt = %d, flags = %s,"
                 % (c["kind"], cs_str(c["ref"]), ymdhm(c["original"]) if c.get("original") else 0, ymdhm(c["actual"]) if c.get("actual") else 0,
                    ymdhm(c["announced"]) if c.get("announced") else 0, ymdhm(c["canceled_since"]) if c.get("canceled_since") else 0, flagexpr(c["flags"])))
        L.append("\t\t\t\tsource = %s }," % cs_str(c["source"]))
    L += ["\t\t};", "\t}", "\t// </event-table>", ""]
    return "\n".join(L)

def write_csv(table, corr, y0, y1):
    p = os.path.join(OUT, "events_%d_%d.csv" % (y0, y1))
    with open(p, "w", newline="") as f:
        w = csv.writer(f, lineterminator="\n")
        w.writerow(["row", "kind", "date_et", "time_et", "flags", "ref_period", "actual_et", "announced_et", "canceled_since_et", "source"])
        for e in table: w.writerow(["table", e.kind, e.et.strftime("%Y-%m-%d"), e.et.strftime("%H:%M"), flagexpr(e.flags).replace("EEventFlags.", ""), e.ref, "", "", "", e.src])
        for c in corr:
            o = c.get("original"); a = c.get("actual"); f_ = lambda d: d.strftime("%Y-%m-%d %H:%M") if d else ""
            w.writerow(["correction", c["kind"], o.strftime("%Y-%m-%d") if o else "", o.strftime("%H:%M") if o else "", flagexpr(c["flags"]).replace("EEventFlags.", ""),
                        c["ref"], f_(a) if a else "Canceled", f_(c.get("announced")), f_(c.get("canceled_since")), c["source"]])

def print_report(table, corr, exp_rows):
    print("M-EVENT build_table.py v%s  rows %d  corrections %d" % (VERSION, len(table), len(corr)))
    print("\n== coverage (table rows: first .. last)")
    for k in KIND:
        r = [e.et for e in table if e.kind == k]
        if r: print("  %-13s %s .. %s  (%d)" % (k, min(r).date(), max(r).date(), len(r)))
        elif KIND[k] not in (8, 14, 15): print("  %-13s NONE" % k)
    print("\n== expiry: rule (R2.1-d) vs CME contract calendar (%d contracts)" % len(exp_rows))
    for c, y, m, d, r in exp_rows: print("  %s %d-%02d CME %s rule %s %s" % (c, y, m, d, r, "MISMATCH" if d != r else "ok"))
    for sec in ("mismatch", "disagree", "refused", "unresolved", "info"):
        print("\n== %s (%d)" % (sec, len(REPORT[sec])))
        for x in REPORT[sec]: print("  - " + x)

if __name__ == "__main__":
    main()
