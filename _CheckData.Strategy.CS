using System;
using System.IO;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using PowerLanguage.Function;
using ATCenterProxy.interop;

namespace PowerLanguage.Strategy
{
  /// <summary>
  /// Chart Type: Candle 
  /// 
  /// Ideal Instruments and Timeframes:
  /// Daily
  /// 
  /// Notes:
  /// Checks the consistency of data used for a backtest to make sure the sufficient data is available
  /// for exact results. Should not be used for live trading scenario's. 
  /// 
  /// TODO:
  ///   * Check whether you can access the holiday calendar for the country and align the check of the symbol data according to it's country of listing,
  ///     session specifications and time frame for the symbol.
  /// </summary>
	public class _CheckData : SignalObject
  {
    //Inputs
    [Input] public String TraceFilename { get; set; }
    [Input] public bool FixData { get; set; }

    public _CheckData(object _ctx) : base(_ctx) { }

    protected USHolidays _usHolidays;
    protected List<DateTime> _barsReceived;
    protected List<DateTime> _expectedBars;
    protected DateTime _firstBarTime;
    protected DateTime _previousReceivedBarTime;
    protected TimeSpan _barDuration;
    protected UInt64 _totalExpectedBars;
    protected UInt64 _totalReceivedBars;
    protected double _percentageDataReceived;
    protected List<Tuple<DateTime, DateTime>> _missingBarRanges;
    IROList<SessionObject> _sessions;

    protected override void Create()
    {
      TraceFilename = "";
      FixData = false;
      _usHolidays = new USHolidays(this);
    }

    protected override void StartCalc()
    {
      _sessions = Bars.Sessions; //get the set of sessions trading happens on the instrument
      _barsReceived = new List<DateTime>();
      _expectedBars = new List<DateTime>();
      _missingBarRanges = new List<Tuple<DateTime, DateTime>>();
      _barDuration = Bars.Info.Resolution.Duration();
      _firstBarTime = DateTime.MinValue;
      _previousReceivedBarTime = DateTime.MinValue;
      _totalExpectedBars = 0;
      _totalReceivedBars = 0;
      _percentageDataReceived = 0;

      //check that intrabar order generation is not used for data, user needs to run the check for the smallest
      //resolution of data to be used
      if (Environment.IOGEnabled)
        ExecControl.Abort("Data checker should not be run with intrabar order generation, run the check for the lowest resolution you're going to use.");
    }

    protected override void CalcBar()
    {
      //force update of US holidays if required
      _usHolidays.Call();

      //make sure strategy is never used for live trading
      if (Environment.IsAutoTradingMode || Environment.IsRealTimeCalc)
        return;

      //save the time of the first bar processed
      if (_firstBarTime == DateTime.MinValue) _firstBarTime = Bars.Time[0];

      //break on duplicate bars being received
      if (Environment.CalcReason == CalculationReason.Default && _barsReceived.Contains(Bars.Time[0]) && System.Diagnostics.Debugger.IsAttached)
        System.Diagnostics.Debugger.Break();

      //check whether data is available for this bar
      if (Environment.CalcReason == CalculationReason.Default && !_barsReceived.Contains(Bars.Time[0]))
      {
        //add newly received bar to set of bars received
        _barsReceived.Add(Bars.Time[0]);

        //determine the next expected bar and log missing bar ranges if required 
        if (_previousReceivedBarTime == DateTime.MinValue)
          _previousReceivedBarTime = Bars.Time[0];
        else
        {
          DateTime expectedBarTime = getExpectedBarTime(_previousReceivedBarTime);
          if (expectedBarTime != Bars.Time[0])
          {
            if (expectedBarTime < Bars.Time[0])
              _missingBarRanges.Add(Tuple.Create(expectedBarTime, Bars.Time[0].AddDays(-1)));
            else
              _missingBarRanges.Add(Tuple.Create(Bars.Time[0].AddDays(-1), expectedBarTime));
          }
          _previousReceivedBarTime = Bars.Time[0];
        }

        //increment the number of bars received
        _totalReceivedBars++;
      }

      //for the last bar on the chart compute the total number of expected bars and compute the
      //percentages of data that were found for the bars
      if (Bars.LastBarOnChart)
      {
        calculateExpectedNumberOfBars();
        _percentageDataReceived = _totalReceivedBars / (double)_totalExpectedBars;

        //trace missing data to the trace file is required
        if (TraceFilename != "")
        {
          try
          {
            StreamWriter trace = new StreamWriter(TraceFilename, true);
            trace.WriteLine("------------------------------------------------------------------------");
            trace.WriteLine("{0} - Quality {1:P}, Received {2}, Expected {3}", Bars.Info.Name, _percentageDataReceived, _totalReceivedBars, _totalExpectedBars);
            trace.WriteLine("------------------------------------------------------------------------");
            trace.WriteLine("Symbol,MissingBars,StartDate,EndDate");

            foreach (Tuple<DateTime, DateTime> missingBarRange in _missingBarRanges)
            {
              ulong missingBarCount = 0;
              DateTime currentBarDate = missingBarRange.Item1 < missingBarRange.Item2 ? missingBarRange.Item1 : missingBarRange.Item2;
              DateTime endDate = missingBarRange.Item1 > missingBarRange.Item2 ? missingBarRange.Item1 : missingBarRange.Item2;
              while (currentBarDate <= endDate)
              {
                if (_expectedBars.Contains(currentBarDate)) missingBarCount++;
                currentBarDate += _barDuration;
              }

              trace.WriteLine("{0},{1},{2},{3}", Bars.Info.Name, missingBarCount, missingBarRange.Item1, missingBarRange.Item2);
            }

            trace.Close();
          }
          catch (Exception exception)
          {
            Output.WriteLine("Error writing file '{0}' - {1}", TraceFilename, exception.Message);
          }
        }
        else
        {
          //trace results to output
          Output.WriteLine("------------------------------------------------------------------------");
          Output.WriteLine("{0} - Quality {1:P}, Received {2}, Expected {3}", Bars.Info.Name, _percentageDataReceived, _totalReceivedBars, _totalExpectedBars);
          Output.WriteLine("------------------------------------------------------------------------");
          Output.WriteLine("Symbol,MissingBars,StartDate,EndDate");

          foreach (Tuple<DateTime, DateTime> missingBarRange in _missingBarRanges)
          {
            ulong missingBarCount = 1;
            DateTime currentBarDate = missingBarRange.Item1 < missingBarRange.Item2 ? missingBarRange.Item1 : missingBarRange.Item2;
            DateTime endDate = missingBarRange.Item1 > missingBarRange.Item2 ? missingBarRange.Item1 : missingBarRange.Item2;
            while (currentBarDate < endDate)
            {
              if (_expectedBars.Contains(currentBarDate)) missingBarCount++;
              currentBarDate += _barDuration;
            }

            Output.WriteLine("{0},{1},{2},{3}", Bars.Info.Name, missingBarCount, missingBarRange.Item1, missingBarRange.Item2);
          }
        }

        //send data requests for all the missing bars of data

        //TBD: This does not work in Multicharts since it does not actually request the data from the vendor.

        if (FixData)
        {
          foreach (Tuple<DateTime, DateTime> missingBarRange in _missingBarRanges) {
            InstrumentDataRequest request = Bars.Request;
            request.Subscribe2RT = true;
            request.Range = DataRequest.CreateFromTo(missingBarRange.Item1, missingBarRange.Item2);
            request.Verify();
            DataLoader.BeginLoadData(request, OnData, null);
          }
        }
      }
    }

    //Data loader callback.
    protected void OnData(IDataLoaderResult result) { }

    //Determines whether a given bar is within the trading session for the instrument.
    protected bool isInTradingSession(ref DateTime dateTime)
    {
      for (int i = 0; i < _sessions.Count; i++)
      {
        SessionObject session = _sessions[i];
        if (dateTime.DayOfWeek >= session.StartDay && dateTime.DayOfWeek <= session.EndDay &&
            dateTime.TimeOfDay >= session.StartTime && dateTime.TimeOfDay <= session.EndTime) return true;
      }

      return false;
    }

    //Calculates the next expected bar based off the date/time of the previously received bar, bar duration, session
    //information and set of public holidays.
    protected DateTime getExpectedBarTime(DateTime previousBarTime)
    {
      DateTime nextBarTime = previousBarTime;
      DateTime expectedBarTime = DateTime.MinValue;

      while (expectedBarTime == DateTime.MinValue)
      {
        nextBarTime += _barDuration;
        if (isInTradingSession(ref nextBarTime) && !_usHolidays.Holidays.Contains(nextBarTime.Date))
          expectedBarTime = nextBarTime;
      }

      return expectedBarTime;
    }

    //Calculates the expected number of bars given the time frame being backtested and the begin/end times.
    protected void calculateExpectedNumberOfBars()
    {
      List<DateTime> currentYearHolidays = new List<DateTime>();
      _totalExpectedBars = 0;
      _expectedBars.Clear();
      DateTime currentBarTime = _firstBarTime;

      //Bars.Info.Resolution.Attributes <- check what is stored in this thing!!!

      while (currentBarTime < Bars.Time[0])
      {
        //skip current bar is it falls outside the regular trading sessions for the instrument
        bool inTradingSession = isInTradingSession(ref currentBarTime);
        if (!inTradingSession)
        {
          currentBarTime += _barDuration;
          continue;
        }

        //skip current bar if it falls on holiday
        if (currentYearHolidays.Contains(currentBarTime.Date))
        {
          currentBarTime += _barDuration;
          continue;
        }

        _expectedBars.Add(currentBarTime);
        _totalExpectedBars++;
        currentBarTime += _barDuration;
      }
    }

  }
}