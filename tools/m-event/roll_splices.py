#!/usr/bin/env python3
"""M-EVENT roll check helper (PR A): the rule roll dates for ES/NQ quarterlies 2016-2024, to compare with the EVROLL lines of
_EventCalendarTest Mode=2 (RollDetection=Series). Mirrors EventRules.RollDate: Monday before the third Friday from
2022-07-01 (EventRules.RollRuleMondayFrom, M-EVENT PR B: ESU22 is the first Monday roll), else the Thursday eight days before. Holiday adjustment of the expiry is not applied here
(no quarterly third Friday 2016-2024 is an NYSE holiday). Output: tools/m-event/out/roll_splices_2016_2024.csv
Columns: contract,expiry,rule_roll,observed_splice (empty: fill from the EVROLL trace), offset_sessions (empty)."""
import csv, datetime as dt, os

def third_friday(y, m):
    d = dt.date(y, m, 1)
    return d + dt.timedelta(days=(4 - d.weekday()) % 7 + 14)

def roll(y, m):
    f = third_friday(y, m)
    return f - dt.timedelta(days=4 if f >= dt.date(2022, 7, 1) else 8)

out = os.path.join(os.path.dirname(os.path.abspath(__file__)), "out", "roll_splices_2016_2024.csv")
os.makedirs(os.path.dirname(out), exist_ok=True)
with open(out, "w", newline="") as fh:
    w = csv.writer(fh, lineterminator="\n")
    w.writerow(["contract", "expiry", "rule_roll", "observed_splice", "offset_sessions"])
    for y in range(2016, 2025):
        for m, c in ((3, "H"), (6, "M"), (9, "U"), (12, "Z")):
            w.writerow([f"ES{c}{y % 100:02d}", third_friday(y, m), roll(y, m), "", ""])
print(out)
