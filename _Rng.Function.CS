using System;

namespace PowerLanguage.Function
{
	// ============================================================================================
	// Phase 3 §0.4 — deterministic PRNG (SplitMix64). Results depend only on the seed, never on the .NET runtime
	// (System.Random's algorithm is an implementation detail and must not be used for research draws).
	// Seed convention: _Rng.Seed(componentId, tradingDay.Ticks, configHash...). Pure C# (tests/VpHelperTests).
	// ============================================================================================

	// <rng>
	public sealed class _Rng
	{
		private ulong m_state;

		public _Rng(ulong seed) { m_state = seed; }

		public ulong State { get { return m_state; } }

		/// <summary>SplitMix64 step: state += golden gamma, then the Stafford 13 mix.</summary>
		public ulong Next()
		{
			unchecked
			{
				m_state += 0x9E3779B97F4A7C15UL;
				return Mix(m_state);
			}
		}

		public static ulong Mix(ulong z)
		{
			unchecked
			{
				z = (z ^ (z >> 30)) * 0xBF58476D1CE4E5B9UL;
				z = (z ^ (z >> 27)) * 0x94D049BB133111EBUL;
				return z ^ (z >> 31);
			}
		}

		/// <summary>Uniform double in [0, 1): (next >> 11) x 2^-53.</summary>
		public double NextDouble() { return (Next() >> 11) * (1.0 / 9007199254740992.0); }

		/// <summary>Uniform int in [0, n) by Lemire's multiply-shift with rejection (32-bit, no 128-bit multiply needed). n must be in [1, int.MaxValue].</summary>
		public int NextInt(int n)
		{
			if (n <= 0) throw new ArgumentOutOfRangeException("n");
			unchecked
			{
				uint un = (uint)n;
				uint x = (uint)(Next() >> 32);
				ulong m = (ulong)x * un;
				uint l = (uint)m;
				if (l < un)
				{
					uint t = (0u - un) % un;
					while (l < t)
					{
						x = (uint)(Next() >> 32);
						m = (ulong)x * un;
						l = (uint)m;
					}
				}
				return (int)(m >> 32);
			}
		}

		/// <summary>Hash the parts through SplitMix64 into one seed (order-sensitive).</summary>
		public static ulong Seed(params long[] parts)
		{
			unchecked
			{
				ulong h = 0x6A09E667F3BCC909UL;
				if (parts != null)
					foreach (long p in parts) h = Mix(h + 0x9E3779B97F4A7C15UL ^ (ulong)p);
				return h;
			}
		}

		/// <summary>Stable 64-bit FNV-1a hash of a string (e.g. a config Canon()), for seeds and the per-run config hash.</summary>
		public static long Hash(string s)
		{
			unchecked
			{
				ulong h = 14695981039346656037UL;
				if (s != null)
					foreach (char c in s) { h ^= c; h *= 1099511628211UL; }
				return (long)h;
			}
		}
	}
	// </rng>
}
