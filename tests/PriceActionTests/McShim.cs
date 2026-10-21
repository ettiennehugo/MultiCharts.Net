// Minimal stand-ins for the two MultiCharts types the engine adapter Step(IInstrument) touches. Tests use Add() only.
using System;
namespace PowerLanguage
{
	public enum EBarState { Open, Inside, Close }
	public interface ISeries<T> { T this[int i] { get; } }
	public interface IInstrument
	{
		ISeries<double> Open { get; } ISeries<double> High { get; } ISeries<double> Low { get; } ISeries<double> Close { get; } ISeries<double> Volume { get; }
		ISeries<DateTime> Time { get; } EBarState Status { get; } int CurrentBar { get; }
	}
}
