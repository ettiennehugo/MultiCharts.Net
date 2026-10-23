#!/usr/bin/env python3
"""G-V offline reference for the G-R regime engine (phase3-specs §3.3).

Usage:  dotnet run -c Release -- --export gv   (C# side: features, published filtered probs, init + fitted params per refit)
        python3 reference.py gv                (or tests/RegimeTests/run.sh --gv)
Needs:  numpy pandas scikit-learn hmmlearn  (pip install numpy pandas scikit-learn hmmlearn; a venv is fine).

Checks (pass criteria from the spec): |dlnL|/|lnL| <= 1e-6, ordered parameters within 1e-4, posteriors within 1e-5.
 * z-score stats: numpy mean / std (ddof 0) of the fit window == C# zMean / zSd.
 * GMM: sklearn GaussianMixture(full, reg_covar=1e-6) started from the C# k-means++ init (weights_init / means_init /
   precisions_init) and run for the SAME number of EM steps as C# (max_iter = C# iters, tol = 0) so both stop on the same
   parameters; then also a free run (tol = 1e-6 per sample, max_iter = 200) for information.
 * HMM: hmmlearn GaussianHMM(full, init_params='', params='stmc') from the C# HMM init, n_iter = C# iters.
   transmat_prior convention (hmmlearn 0.3.x, base.py _do_mstep): transmat_ = normalize(max(transmat_prior - 1 + xi_sum, 0)),
   i.e. transmat_prior is a Dirichlet alpha and alpha - 1 is the pseudo-count. Our pseudo-count of 1 per cell
   (A_ij = (sum xi + 1) / (sum gamma + K)) therefore needs transmat_prior = 2.0 (used below). startprob_prior = 1.0 (no
   pseudo-count: pi0 = gamma_1). covars_prior = 0, covars_weight = 0, means_weight = 0 (plain ML M step). C# adds
   regCovar = 1e-6 to each covariance diagonal in the M step (as sklearn does); hmmlearn has no such knob, so RegHMM below
   adds it after hmmlearn's own M step (without it the smoothed posteriors differ by ~4e-5, parameters by ~6e-6).
   Compared: score, transmat_, means_, covars_, smoothed predict_proba vs the C# in-window gamma.
 * Filtered: a numpy forward pass on the hmmlearn parameters (warm-up over the last 63 window observations, then one step
   per day until the next refit) vs the C# published p0..p2.
The ES walk-forward replay (>= 99.9% same labels, 2016-2026) stays a live check on the MC journal export (A-REG).
"""
import json, sys, os
import numpy as np, pandas as pd
from sklearn.mixture import GaussianMixture
from hmmlearn.hmm import GaussianHMM
from scipy.stats import multivariate_normal

class RegHMM(GaussianHMM):
    """GaussianHMM whose M step adds reg_covar to each covariance diagonal, exactly like the C# (and sklearn) M step."""
    reg_covar = 1e-6
    def _do_mstep(self, stats):
        super()._do_mstep(stats)
        if 'c' in self.params:
            self._covars_ = self._covars_ + self.reg_covar * np.eye(self.n_features)[None, :, :]

TOL_LL, TOL_PAR, TOL_POST = 1e-6, 1e-4, 1e-5
d = sys.argv[1] if len(sys.argv) > 1 else "gv"
feat = pd.read_csv(os.path.join(d, "gv_features.csv"))
fits = json.load(open(os.path.join(d, "gv_fits.json")))["fits"]
valid = feat.dropna(subset=["f1", "f2", "f3"]).reset_index(drop=True)
X = valid[["f1", "f2", "f3"]].to_numpy()
results = []

def rep(name, err, tol):
    ok = bool(err <= tol); results.append(ok)
    print(f"  {'PASS' if ok else 'FAIL'} {name}: {err:.3e} (tol {tol:g})")

def order(mu):   # labels ordered by the f2 mean (column 1), ties f3 (column 2)
    return sorted(range(len(mu)), key=lambda k: (mu[k][1], mu[k][2], k))

for f in fits:
    end = valid.index[valid.date == f["fittedThrough"]][0]
    W = X[end - f["window"] + 1: end + 1]
    assert valid.date[end - f["window"] + 1] == f["windowStart"]
    print(f"refit {f['fittedThrough']} window {f['window']}")
    zm, zs = W.mean(0), W.std(0)
    rep("z mean/sd vs numpy (ddof 0)", max(np.abs(zm - f["zMean"]).max(), np.abs(zs - f["zSd"]).max()), 1e-12)
    Z = (W - np.array(f["zMean"])) / np.array(f["zSd"])
    N = len(Z)

    # ---- GMM ----
    gi, g = f["gmmInit"], f["gmm"]
    prec = np.array([np.linalg.inv(np.array(c)) for c in gi["cov"]])
    gm = GaussianMixture(n_components=3, covariance_type="full", reg_covar=1e-6, tol=0.0, max_iter=max(g["iters"], 1),
                         weights_init=np.array(gi["w"]), means_init=np.array(gi["mu"]), precisions_init=prec)
    import warnings; warnings.filterwarnings("ignore")
    gm.fit(Z)
    o = order(gm.means_)
    ll = gm.score(Z) * N
    rep("GMM lnL", abs(ll - g["logLik"]) / abs(g["logLik"]), TOL_LL)
    rep("GMM ordered params (w, mu, cov)", max(np.abs(gm.weights_[o] - g["w"]).max(), np.abs(gm.means_[o] - g["mu"]).max(),
        np.abs(gm.covariances_[o] - np.array(g["cov"])).max()), TOL_PAR)
    def post(w, mu, cov):
        lp = np.stack([np.log(w[k]) + multivariate_normal(mu[k], cov[k]).logpdf(Z) for k in range(3)], 1)
        lp -= lp.max(1, keepdims=True); p = np.exp(lp); return p / p.sum(1, keepdims=True)
    rep("GMM posteriors", np.abs(gm.predict_proba(Z)[:, o] - post(np.array(g["w"]), np.array(g["mu"]), np.array(g["cov"]))).max(), TOL_POST)
    free = GaussianMixture(n_components=3, covariance_type="full", reg_covar=1e-6, tol=1e-6, max_iter=200,
                           weights_init=np.array(gi["w"]), means_init=np.array(gi["mu"]), precisions_init=prec).fit(Z)
    print(f"  info GMM free run: sklearn n_iter {free.n_iter_} converged {free.converged_} vs C# iters {g['iters']}; "
          f"|dlnL|/|lnL| {abs(free.score(Z) * N - g['logLik']) / abs(g['logLik']):.2e}")

    # ---- HMM ----
    hi, h = f["hmmInit"], f["hmm"]
    m = RegHMM(n_components=3, covariance_type="full", n_iter=max(h["iters"], 1), tol=-np.inf, init_params="", params="stmc",
                    startprob_prior=1.0, transmat_prior=2.0, means_prior=0, means_weight=0, covars_prior=0, covars_weight=0)
    m.startprob_ = np.array(hi["pi"]); m.transmat_ = np.array(hi["A"]); m.means_ = np.array(hi["mu"]); m.covars_ = np.array(hi["cov"])
    m.fit(Z)
    o = order(m.means_)
    ll = m.score(Z)
    rep("HMM lnL (score)", abs(ll - h["logLik"]) / abs(h["logLik"]), TOL_LL)
    A = m.transmat_[np.ix_(o, o)]
    rep("HMM transmat_", np.abs(A - np.array(h["A"])).max(), TOL_PAR)
    rep("HMM means_", np.abs(m.means_[o] - np.array(h["mu"])).max(), TOL_PAR)
    rep("HMM covars_", np.abs(m.covars_[o] - np.array(h["cov"])).max(), TOL_PAR)
    rep("HMM smoothed predict_proba vs C# in-window gamma", np.abs(m.predict_proba(Z)[:, o] - np.array(f["gamma"])).max(), TOL_POST)
    print(f"  info HMM iters {h['iters']} (hmmlearn n_iter {m.monitor_.iter})")

    # ---- filtered: numpy forward pass on hmmlearn params ----
    pi, mu, cov = m.startprob_[o], m.means_[o], m.covars_[o]
    def step(a, z):
        lb = np.array([multivariate_normal(mu[k], cov[k]).logpdf(z) for k in range(3)]); lb -= lb.max()
        pred = pi if a is None else a @ A
        x = pred * np.exp(lb); return x / x.sum()
    a = None
    for z in Z[-63:]: a = step(a, z)
    days = valid[valid.fittedThrough == f["fittedThrough"]]
    err = 0.0
    for i, r in days.iterrows():
        a = step(a, (X[i] - np.array(f["zMean"])) / np.array(f["zSd"]))
        err = max(err, np.abs(a - r[["p0", "p1", "p2"]].to_numpy(float)).max())
    rep(f"filtered posteriors over {len(days)} published days", err, TOL_POST)

print(f"G-V: {sum(results)} passed, {len(results) - sum(results)} failed")
sys.exit(0 if all(results) else 1)
