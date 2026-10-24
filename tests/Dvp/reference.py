#!/usr/bin/env python3
"""M-DVP reference (spec DV-T1 / §15.1): weighted least squares by numpy QR on the u-Vandermonde basis, independent of the
Forsythe recurrence in _DvpEngine.Function.CS. Writes fixtures/fit_cases.txt (committed; the C# suite needs no numpy).
Usage: python3 tests/Dvp/reference.py   (numpy >= 1.20)
Weights follow App E buildTrend: volume normalised to mean 1 (sum w = n) when weighted, sigma = sqrt(SSE/(n-d-1))."""
import os
import numpy as np

NS = [12, 30, 240, 1000]
NCASES = 50

def fit(y, w, d):
    n = len(y)
    ctr = scl = (n - 1) / 2.0
    u = (np.arange(n) - ctr) / scl
    X = np.vander(u, d + 1, increasing=True)
    sw = np.sqrt(w)
    Q, R = np.linalg.qr(X * sw[:, None])
    beta = np.linalg.solve(R, Q.T @ (y * sw))
    yhat = X @ beta
    e = y - yhat
    sse = float(np.sum(w * e * e))
    ybar = np.sum(w * y) / np.sum(w)
    s2 = float(np.sum(w * (y - ybar) ** 2))
    sigma = np.sqrt(sse / (n - d - 1))
    r2 = 1 - sse / s2
    bic = n * np.log(sse / n) + (d + 2) * np.log(n)
    Rinv_T = np.linalg.inv(R).T
    def ev(uu):
        x = np.array([uu ** k for k in range(d + 1)])
        z = Rinv_T @ x
        return float(x @ beta), float(z @ z)
    ue = (e - e.mean())
    m2 = np.mean(ue ** 2); m3 = np.mean(ue ** 3)
    dw = float(np.sum(np.diff(e) ** 2) / np.sum(e ** 2))
    skew = float(m3 / m2 ** 1.5)
    return sse, sigma, r2, bic, ev, u, dw, skew

def main():
    rng = np.random.default_rng(20261024)
    out = []
    for c in range(NCASES):
        n = NS[c % 4]
        weighted = (c // 4) % 2 == 1
        t = np.linspace(-1, 1, n)
        coef = rng.normal(0, 1, 4) * np.array([1, 8, 5, 3])
        noise = np.zeros(n); a = 0.6
        for i in range(n):
            noise[i] = (a * noise[i - 1] if i else 0) + rng.normal(0, 1)
        y = 4000 + coef[0] + coef[1] * t + coef[2] * t ** 2 + coef[3] * t ** 3 + 2.0 * noise
        y = np.round(y * 4) / 4 + 0.125 * (rng.random(n) < 0.5)  # quarter-tick prices plus HL2 eighths
        v = np.round(rng.lognormal(6, 1, n))
        v[rng.random(n) < 0.05] = 0
        w = v * n / v.sum() if weighted else np.ones(n)
        out.append(f"C {c} {n} {1 if weighted else 0}")
        out.append("Y " + " ".join(repr(float(x)) for x in y))
        out.append("V " + " ".join(repr(float(x)) for x in v))
        samples = [0, n // 3, n // 2, n - 1]
        for d in range(4):
            sse, sigma, r2, bic, ev, u, dw, skew = fit(y, w, d)
            ut = 1 + 2.0 / (n - 1)
            vals = [sse, sigma, r2, bic, dw, skew]
            yt, ht = ev(ut); vals += [yt, ht]
            for i in samples:
                yi, hi = ev(u[i]); vals += [yi, hi]
            out.append(f"E {d} " + " ".join(repr(float(x)) for x in vals))
    here = os.path.dirname(os.path.abspath(__file__))
    with open(os.path.join(here, "fixtures", "fit_cases.txt"), "w") as f:
        f.write("\n".join(out) + "\n")

if __name__ == "__main__":
    main()
