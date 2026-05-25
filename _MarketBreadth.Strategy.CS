using FileHelpers;
using PowerLanguage.Function;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace PowerLanguage.Strategy
{
	/// <summary>
	/// Used in a portfolio of stocks in the market to compute market breadth entries based on the daily stock moves.
	/// Calculates the following primary and secondary indicators:
	/// ○ Primary breadth indicators
	/// 	§ Number of stocks up 4%+ today(UP4%+)
	/// 	§ Number of stocks down 4%+ today(DN4%+)
	/// 	§ 5-day ratio of stocks up vs stocks down 4%+
	/// 	§ 10-day ratio of stock up vs stocks down 4%+
	/// 	§ Number of stocks up 25%+ in a quarter
	/// 	§ Number of stocks down 25%+ in a quarter
	/// ○ Secondary breadth indicators
	/// 	§ Number of stocks up 25%+ in a month
	/// 	§ Number of stocks down 25%+ in a month
	/// 	§ Number of stocks up 50%+ in a month
	/// 	§ Number of stocks down 50%+ in a month
	/// 	§ Number of stocks up 13%+ in 34-days
	///   § Number of stocks down 13%+ in 34-days
	/// ○ Additional breadth indicators
	///   § Number of stocks up 20%+ in 5-days
	///   § Number of stocks down 20%+ in 5-days
	///   § Number of stocks above 40-day SMA
	///   § Number of stocks below 40-day SMA
	///   § Number of stocks new high for year
	///   § Number of stocks new low for year
	///   § Number of stocks new high for quarter
	///   § Number of stocks new low for quarter
	///   § Number of stocks new high for month
	///   § Number of stocks new low for month
	/// </summary>
	public class _MarketBreadth : PortfolioSignalObject
	{
		//constants
		public const string KEY_ENTRIES = "entries";
		public const string KEY_FINISHED = "finished";
		public const double DEFAULT_EPSILON = 0.0000001;	//avoid dividing by really small numbers
		public const int WEEK_LENGTH = 5;
		public const int MONTH_LENGTH = 21;
		public const int QUARTER_LENGTH = 63;
		public const int YEAR_LENGTH = 252;

		// types
		[FixedLengthRecord()]
		[IgnoreFirst()]
		public class Entry
		{
			[FieldFixedLength(15)]
			[FieldConverter(ConverterKind.Date, "ddMMyyyy")]
			public DateTime TimeStamp;

			[FieldFixedLength(15)]
			public int Count; // Number of instruments analysed to compute these numbers

			[FieldFixedLength(15)]
			public int Up4Pct;  // Number of stocks up 4%+ today(UP4%+)

			[FieldFixedLength(15)]
			public int Down4Pct;  //Number of stocks down 4%+ today(DN4%+)

			[FieldFixedLength(20)]
			public string UpDown4PctRat5D = "0.00000000".PadLeft(20); // ratio of stocks up vs stocks down 4%+ on this date against the stocks up vs down 5-days ago

			[FieldFixedLength(20)]
			public string UpDown4PctRat10D = "0.00000000".PadLeft(20); // ratio of stocks up vs stocks down 4%+ on this date against the stocks up vs down 10-days ago

			[FieldFixedLength(15)]
			public int Up25PctQrt;  // Number of stocks up 25%+ in a quarter

			[FieldFixedLength(15)]
			public int Down25PctQrt;  // Number of stocks down 25%+ in a quarter

			[FieldFixedLength(15)]
			public int Up25PctMth;  // Number of stocks up 25%+ in a month

			[FieldFixedLength(15)]
			public int Down25PctMth;  // Number of stocks down 25%+ in a month

			[FieldFixedLength(15)]
			public int Up50PctMth;  // Number of stocks up 50%+ in a month

			[FieldFixedLength(15)]
			public int Down50PctMth;  // Number of stocks down 50%+ in a month

			[FieldFixedLength(15)]
			public int Up13Pct34D;  // Number of stocks up 13%+ in 34-days

			[FieldFixedLength(15)]
			public int Down13Pct34D;  // Number of stocks down 13%+ in 34-days

			[FieldFixedLength(15)]
			public int Up20Pct5D;  // Number of stocks up 20%+ in 5-days

			[FieldFixedLength(15)]
			public int Down20Pct5D;  // Number of stocks down 20%+ in 5-days

			[FieldFixedLength(15)]
			public int Above40SMA;  // Number of stocks above the 40-day SMA

			[FieldFixedLength(15)]
			public int Below40SMA;  // Number of stocks Below the 40-day SMA

			[FieldFixedLength(15)]
			public int NewHighYear;  // Number of stocks new high for year

			[FieldFixedLength(15)]
			public int NewLowYear;  // Number of stocks new low for year

			[FieldFixedLength(15)]
			public int NewHighQrt;  // Number of stocks new high for quarter

			[FieldFixedLength(15)]
			public int NewLowQrt;  // Number of stocks new low for mont

			[FieldFixedLength(15)]
			public int NewHighMth;  // Number of stocks new high for month

			[FieldFixedLength(15)]
			public int NewLowMth;  // Number of stocks new low for month
		}

		public enum EUpdateMode
		{
			Replace,  //replace the data entries in the file with the new data
			Update,   //keep file entries and when a timestamp if found update it's values
		}

		//inputs
		[Input] public EUpdateMode UpdateMode { get; set; }
		[Input] public string OutputFile { get; set; }

		//properties


		//attributes
		private Guid m_guid;
		//private int m_index;
		private SortedDictionary<DateTime, Entry> m_entries;
		Function.AverageFC m_sma40;

		//interface methods
		public _MarketBreadth(object _ctx) : base(_ctx) { }

		protected override void Create()
		{			
			UpdateMode = EUpdateMode.Replace;
			OutputFile = Path.Combine(System.Environment.GetFolderPath(System.Environment.SpecialFolder.MyDocuments), "MarketBreadth.asc");
			m_sma40 = new AverageFC(this);
		}

		protected override void StartCalc()
		{
			m_guid = Guid.NewGuid();
			m_sma40.price = Bars.Close;
			m_sma40.length = 40;

			m_entries = new SortedDictionary<DateTime, Entry>();
			this.MyPortfolioData[KEY_ENTRIES] = m_entries;
			this.MyPortfolioData[KEY_FINISHED] = false;
		}

		protected override void CalcBar()
		{
			if (Environment.CalcReason == CalculationReason.Default)
			{
				var entry = new Entry();

				entry.TimeStamp = Bars.Time[0].Date;
				entry.Count = 1;
				entry.Up4Pct = Bars.CurrentBar > 2 && Bars.Close[1] != 0.0 && Bars.Close[0] / Bars.Close[1] > 1.04 ? 1 : 0;
				entry.Down4Pct = Bars.CurrentBar > 2 && Bars.Close[1] != 0.0 && Bars.Close[0] / Bars.Close[1] < 0.96 ? 1 : 0;
				entry.Up25PctQrt = Bars.CurrentBar > QUARTER_LENGTH && Bars.Close[QUARTER_LENGTH] != 0.0 && Bars.Close[0] / Bars.Close[QUARTER_LENGTH] > 1.25 ? 1 : 0;
				entry.Down25PctQrt = Bars.CurrentBar > QUARTER_LENGTH && Bars.Close[QUARTER_LENGTH] != 0.0 && Bars.Close[0] / Bars.Close[QUARTER_LENGTH] < 0.75 ? 1 : 0;
				entry.Up25PctMth = Bars.CurrentBar > MONTH_LENGTH && Bars.Close[MONTH_LENGTH] != 0.0 && Bars.Close[0] / Bars.Close[MONTH_LENGTH] > 1.25 ? 1 : 0;
				entry.Down25PctMth = Bars.CurrentBar > MONTH_LENGTH && Bars.Close[MONTH_LENGTH] != 0.0 && Bars.Close[0] / Bars.Close[MONTH_LENGTH] < 0.75 ? 1 : 0;
				entry.Up50PctMth = Bars.CurrentBar > MONTH_LENGTH && Bars.Close[MONTH_LENGTH] != 0.0 && Bars.Close[0] / Bars.Close[MONTH_LENGTH] > 1.5 ? 1 : 0;
				entry.Down50PctMth = Bars.CurrentBar > MONTH_LENGTH && Bars.Close[MONTH_LENGTH] != 0.0 && Bars.Close[0] / Bars.Close[MONTH_LENGTH] < 0.5 ? 1 : 0;
				entry.Up13Pct34D = Bars.CurrentBar > 34 && Bars.Close[34] != 0.0 && Bars.Close[0] / Bars.Close[34] > 1.13 ? 1 : 0;
				entry.Down13Pct34D = Bars.CurrentBar > 34 && Bars.Close[34] != 0.0 && Bars.Close[0] / Bars.Close[34] < 0.87 ? 1 : 0;
				entry.Up20Pct5D = Bars.CurrentBar > WEEK_LENGTH && Bars.Close[WEEK_LENGTH] != 0.0 && Bars.Close[0] / Bars.Close[WEEK_LENGTH] > 1.2 ? 1 : 0;
				entry.Down20Pct5D = Bars.CurrentBar > WEEK_LENGTH && Bars.Close[WEEK_LENGTH] != 0.0 && Bars.Close[0] / Bars.Close[WEEK_LENGTH] < 0.8 ? 1 : 0;
				entry.Above40SMA = Bars.CurrentBar > 40 && m_sma40.Value < Bars.Close[0] ? 1 : 0;
				entry.Below40SMA = Bars.CurrentBar > 40 && m_sma40.Value > Bars.Close[0] ? 1 : 0;
				entry.NewHighMth = Bars.CurrentBar > MONTH_LENGTH && Bars.Close.Highest(MONTH_LENGTH, 1) < Bars.Close[0] ? 1 : 0;
				entry.NewLowMth = Bars.CurrentBar > MONTH_LENGTH && Bars.Close.Lowest(MONTH_LENGTH, 1) > Bars.Close[0] ? 1 : 0;
				entry.NewHighQrt = Bars.CurrentBar > QUARTER_LENGTH && Bars.Close.Highest(QUARTER_LENGTH, 1) < Bars.Close[0] ? 1 : 0;
				entry.NewLowQrt = Bars.CurrentBar > QUARTER_LENGTH && Bars.Close.Lowest(QUARTER_LENGTH, 1) > Bars.Close[0] ? 1 : 0;
				entry.NewHighYear = Bars.CurrentBar > YEAR_LENGTH && Bars.Close.Highest(YEAR_LENGTH, 1) < Bars.Close[0] ? 1 : 0;
				entry.NewLowYear = Bars.CurrentBar > YEAR_LENGTH && Bars.Close.Lowest(YEAR_LENGTH, 1) > Bars.Close[0] ? 1 : 0;

				m_entries[Bars.Time[0]] = entry;
			}

			if (Bars.LastBarOnChart)
			{
				this.MyPortfolioData[KEY_FINISHED] = true;
				bool allFinished = true;
				foreach (var signal in this.PortfolioStrategies)
				{
					allFinished &= (bool)signal.PortfolioData[KEY_FINISHED];
					if (!allFinished) break;
				}

				if (allFinished) saveOutputFile();
			}
		}

		protected void saveOutputFile()
		{
			Output.WriteLine($"saveOutputFile called from {this.Bars.Info.Name}");

			SortedDictionary<DateTime, Entry> entries = new SortedDictionary<DateTime, Entry>();
			foreach (var signal in this.PortfolioStrategies)
			{
				var signalEntries = (SortedDictionary<DateTime, Entry>)signal.PortfolioData[KEY_ENTRIES];
				if (signalEntries != null)
				{
					//create entries
					foreach (var signalEntry in signalEntries)
					{
						if (entries.TryGetValue(signalEntry.Key, out var entry))
						{
							entry.Count += signalEntry.Value.Count;
							entry.Up4Pct += signalEntry.Value.Up4Pct;
							entry.Down4Pct += signalEntry.Value.Down4Pct;
							entry.Up25PctQrt += signalEntry.Value.Up25PctQrt;
							entry.Down25PctQrt += signalEntry.Value.Down25PctQrt;
							entry.Up25PctMth += signalEntry.Value.Up25PctMth;
							entry.Down25PctMth += signalEntry.Value.Down25PctMth;
							entry.Up50PctMth += signalEntry.Value.Up50PctMth;
							entry.Down50PctMth += signalEntry.Value.Down50PctMth;
							entry.Up13Pct34D += signalEntry.Value.Up13Pct34D;
							entry.Down13Pct34D += signalEntry.Value.Down13Pct34D;
							entry.Up20Pct5D += signalEntry.Value.Up20Pct5D;
							entry.Down20Pct5D += signalEntry.Value.Down20Pct5D;
							entry.Above40SMA += signalEntry.Value.Above40SMA;
							entry.Below40SMA += signalEntry.Value.Below40SMA;
							entry.NewHighMth += signalEntry.Value.NewHighMth;
							entry.NewLowMth += signalEntry.Value.NewLowMth;
							entry.NewHighQrt += signalEntry.Value.NewHighQrt;
							entry.NewLowQrt += signalEntry.Value.NewLowQrt;
							entry.NewHighYear += signalEntry.Value.NewHighYear;
							entry.NewLowYear += signalEntry.Value.NewLowYear;
							entries[signalEntry.Key] = entry;
						}
						else
							entries[signalEntry.Key] = signalEntry.Value;
					}
				}
			}

			//create derived entries
			for (int i = 0; i < entries.Values.Count; ++i)
			{
				var entry = entries.Values.ElementAt(i);
				double upDownRatioNow = 0.0;  //compute value only once below
				if (i >= 5)
				{
					var entryMin5 = entries.Values.ElementAt(i - 5);
					upDownRatioNow = entry.Down4Pct != 0 ? (double)entry.Up4Pct / entry.Down4Pct : 0;
					double upDownRatioMin5 = entryMin5.Down4Pct != 0 ? (double)entryMin5.Up4Pct / entryMin5.Down4Pct : 1; //we return 1 to avoid div by zero below
					double ratio5 = upDownRatioMin5 > DEFAULT_EPSILON ? upDownRatioNow / upDownRatioMin5 : 0.0;
					entry.UpDown4PctRat5D = ratio5.ToString("0.00000000").PadLeft(20);
				}

				if (i >= 10)
				{
					var entryMin10 = entries.Values.ElementAt(i - 10);
					double upDownRatioMin10 = entryMin10.Down4Pct != 0 ? (double)entryMin10.Up4Pct / entryMin10.Down4Pct : 1; //we return 1 to avoid div by zero below
					double ratio10 = upDownRatioMin10 > DEFAULT_EPSILON ? upDownRatioNow / upDownRatioMin10 : 0.0;
					entry.UpDown4PctRat10D = ratio10.ToString("0.00000000").PadLeft(20);
				}
			}

			var outputFile = new FixedFileEngine<Entry>();
			Entry[] fileEntriesRaw = System.IO.File.Exists(OutputFile) ? outputFile.ReadFile(OutputFile) : Array.Empty<Entry>();

			SortedDictionary<DateTime, Entry> fileEntries = new SortedDictionary<DateTime, Entry>();

			if (UpdateMode == EUpdateMode.Replace)
			{
				fileEntries = entries;
			}
			else
			{
				//update entries in the file
				foreach (var entry in fileEntriesRaw)
				{
					if (fileEntries.ContainsKey(entry.TimeStamp)) Output.WriteLine($"Error: Duplicate entry found on timestamp {entry.TimeStamp:ddMMyyyy}");
					fileEntries[entry.TimeStamp] = entry;
				}

				foreach (var entry in entries.Values)
				{
					fileEntries[entry.TimeStamp] = entry;
				}
			}

			fileEntriesRaw = fileEntries.Values.ToArray();

			try 
			{
				//create directory of output file if it does not exist
				string directory = Path.GetDirectoryName(OutputFile);
				if (!string.IsNullOrEmpty(directory) && !Directory.Exists(directory))
					Directory.CreateDirectory(directory);

				//output the fixed column data
				outputFile.ErrorManager.ErrorMode = ErrorMode.ThrowException;
				outputFile.HeaderText = outputFile.GetFileHeader();
				string fileContent = outputFile.WriteString(fileEntriesRaw);
				File.WriteAllText(OutputFile, fileContent);
			}
			catch (Exception e)
			{
				Output.WriteLine($"ERROR writing MarketBreadth file: {e.Message}");
				Output.WriteLine($"   Full path attempted: {OutputFile}");
				Output.WriteLine($"   Stack trace: {e.StackTrace}");
			}
		}

		//methods


	}
}