using FileHelpers;
using PowerLanguage.Function;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;

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
		public const double DEFAULT_EPSILON = 0.0000001;  //avoid dividing by really small numbers
		public const int WEEK_LENGTH = 5;
		public const int MONTH_LENGTH = 21;
		public const int QUARTER_LENGTH = 63;
		public const string DEFAULT_OUTPUT_DIRECTORY = "C:\\Share\\Trading\\MultiCharts\\Data";
		public static readonly string[] COLUMNS_TO_OUTPUT = {
			"Up4P",
			"Dn4P",
			"UpDn4PR5D",
			"UpDn4PR10D",
			"Up25PQrt",
			"Dn25PQrt",
			"Up25PMth",
			"Dn25PMth",
			"Up50PMth",
			"Dn50PMth",
			"Up13P34D",
			"Dn13P34D",
			"Up20P5D",
			"Dn20P5D",
			"Above40SMA",
			"Below40SMA",
			"NewHighQrt",
			"NewLowQrt",
			"NewHighMth",
			"NewLowMth",
		};

		// types
		[DelimitedRecord(",")]
		[IgnoreFirst()]
		public class Entry
		{
			[FieldConverter(ConverterKind.Date, "ddMMyyyy")]
			public DateTime TimeStamp;

			public int Count; // Number of instruments analysed to compute these numbers

			public int Up4P;  // Number of stocks up 4%+ today(UP4%+)

			public int Dn4P;  //Number of stocks down 4%+ today(DN4%+)

			public string UpDn4PR5D = "0.00000000"; // ratio of stocks up vs stocks down 4%+ on this date against the stocks up vs down 5-days ago

			public string UpDn4PR10D = "0.00000000"; // ratio of stocks up vs stocks down 4%+ on this date against the stocks up vs down 10-days ago

			public int Up25PQrt;  // Number of stocks up 25%+ in a quarter

			public int Dn25PQrt;  // Number of stocks down 25%+ in a quarter

			public int Up25PMth;  // Number of stocks up 25%+ in a month

			public int Dn25PMth;  // Number of stocks down 25%+ in a month

			public int Up50PMth;  // Number of stocks up 50%+ in a month

			public int Dn50PMth;  // Number of stocks down 50%+ in a month

			public int Up13P34D;  // Number of stocks up 13%+ in 34-days

			public int Dn13P34D;  // Number of stocks down 13%+ in 34-days

			public int Up20P5D;  // Number of stocks up 20%+ in 5-days

			public int Dn20P5D;  // Number of stocks down 20%+ in 5-days

			public int Above40SMA;  // Number of stocks above the 40-day SMA

			public int Below40SMA;  // Number of stocks Below the 40-day SMA

			public int NewHighQrt;  // Number of stocks new high for quarter

			public int NewLowQrt;  // Number of stocks new low for mont

			public int NewHighMth;  // Number of stocks new high for month

			public int NewLowMth;  // Number of stocks new low for month
		}

		[DelimitedRecord(",")]
		[IgnoreFirst()]
		public class OHLCV
		{
			[FieldConverter(ConverterKind.Date, "ddMMyyyy")]
			public DateTime TimeStamp;

			public double Open;
			public double High;
			public double Low;
			public double Close;
			public double Volume;

		}

		public enum EUpdateMode
		{
			Replace,  //replace the data entries in the file with the new data
			Update,   //keep file entries and when a timestamp if found update it's values
		}

		//inputs
		[Input] public EUpdateMode UpdateMode { get; set; }
		[Input] public string OutputDirectory { get; set; }

		//properties


		//attributes
		private static readonly object s_saveLock = new object();
		private Guid m_guid;
		private SortedDictionary<DateTime, Entry> m_entries;
		Function.AverageFC m_sma40;

		//interface methods
		public _MarketBreadth(object _ctx) : base(_ctx) { }

		protected override void Create()
		{
			UpdateMode = EUpdateMode.Replace;
			OutputDirectory = DEFAULT_OUTPUT_DIRECTORY;
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
				entry.Up4P = Bars.CurrentBar > 2 && Bars.Close[1] != 0.0 && Bars.Close[0] / Bars.Close[1] > 1.04 ? 1 : 0;
				entry.Dn4P = Bars.CurrentBar > 2 && Bars.Close[1] != 0.0 && Bars.Close[0] / Bars.Close[1] < 0.96 ? 1 : 0;
				entry.Up25PQrt = Bars.CurrentBar > QUARTER_LENGTH && Bars.Close[QUARTER_LENGTH] != 0.0 && Bars.Close[0] / Bars.Close[QUARTER_LENGTH] > 1.25 ? 1 : 0;
				entry.Dn25PQrt = Bars.CurrentBar > QUARTER_LENGTH && Bars.Close[QUARTER_LENGTH] != 0.0 && Bars.Close[0] / Bars.Close[QUARTER_LENGTH] < 0.75 ? 1 : 0;
				entry.Up25PMth = Bars.CurrentBar > MONTH_LENGTH && Bars.Close[MONTH_LENGTH] != 0.0 && Bars.Close[0] / Bars.Close[MONTH_LENGTH] > 1.25 ? 1 : 0;
				entry.Dn25PMth = Bars.CurrentBar > MONTH_LENGTH && Bars.Close[MONTH_LENGTH] != 0.0 && Bars.Close[0] / Bars.Close[MONTH_LENGTH] < 0.75 ? 1 : 0;
				entry.Up50PMth = Bars.CurrentBar > MONTH_LENGTH && Bars.Close[MONTH_LENGTH] != 0.0 && Bars.Close[0] / Bars.Close[MONTH_LENGTH] > 1.5 ? 1 : 0;
				entry.Dn50PMth = Bars.CurrentBar > MONTH_LENGTH && Bars.Close[MONTH_LENGTH] != 0.0 && Bars.Close[0] / Bars.Close[MONTH_LENGTH] < 0.5 ? 1 : 0;
				entry.Up13P34D = Bars.CurrentBar > 34 && Bars.Close[34] != 0.0 && Bars.Close[0] / Bars.Close[34] > 1.13 ? 1 : 0;
				entry.Dn13P34D = Bars.CurrentBar > 34 && Bars.Close[34] != 0.0 && Bars.Close[0] / Bars.Close[34] < 0.87 ? 1 : 0;
				entry.Up20P5D = Bars.CurrentBar > WEEK_LENGTH && Bars.Close[WEEK_LENGTH] != 0.0 && Bars.Close[0] / Bars.Close[WEEK_LENGTH] > 1.2 ? 1 : 0;
				entry.Dn20P5D = Bars.CurrentBar > WEEK_LENGTH && Bars.Close[WEEK_LENGTH] != 0.0 && Bars.Close[0] / Bars.Close[WEEK_LENGTH] < 0.8 ? 1 : 0;
				entry.Above40SMA = Bars.CurrentBar > 40 && m_sma40.Value < Bars.Close[0] ? 1 : 0;
				entry.Below40SMA = Bars.CurrentBar > 40 && m_sma40.Value > Bars.Close[0] ? 1 : 0;
				entry.NewHighMth = Bars.CurrentBar > MONTH_LENGTH && Bars.Close.Highest(MONTH_LENGTH, 1) < Bars.Close[0] ? 1 : 0;
				entry.NewLowMth = Bars.CurrentBar > MONTH_LENGTH && Bars.Close.Lowest(MONTH_LENGTH, 1) > Bars.Close[0] ? 1 : 0;
				entry.NewHighQrt = Bars.CurrentBar > QUARTER_LENGTH && Bars.Close.Highest(QUARTER_LENGTH, 1) < Bars.Close[0] ? 1 : 0;
				entry.NewLowQrt = Bars.CurrentBar > QUARTER_LENGTH && Bars.Close.Lowest(QUARTER_LENGTH, 1) > Bars.Close[0] ? 1 : 0;

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

				if (allFinished)
				{
					lock (s_saveLock)
					{
						Output.WriteLine($"Output data called from {this.Bars.Info.Name}");
						var entries = consolidateEntries();
						saveMetadataFile(entries);
						saveFieldFiles(entries);
					}
				}
			}
		}

		protected SortedDictionary<DateTime, Entry> consolidateEntries()
		{
			SortedDictionary<DateTime, Entry> entries = new SortedDictionary<DateTime, Entry>();

			foreach (var signal in this.PortfolioStrategies)
			{
				var signalEntries = signal.PortfolioData[KEY_ENTRIES] as SortedDictionary<DateTime, Entry>;
				if (signalEntries == null) continue;

				foreach (var signalEntry in signalEntries)
				{
					DateTime date = signalEntry.Key.Date;
					Entry source = signalEntry.Value;

					if (!entries.TryGetValue(date, out var target))
					{
						// create a fresh entry for this date (never reuse the original single-instrument object)
						target = new Entry
						{
							TimeStamp = source.TimeStamp.Date,
							UpDn4PR5D = "0.00000000",
							UpDn4PR10D = "0.00000000"
						};
						entries[date] = target;
					}

					// accumulate counts from this signal
					target.Count += source.Count;
					target.Up4P += source.Up4P;
					target.Dn4P += source.Dn4P;
					target.Up25PQrt += source.Up25PQrt;
					target.Dn25PQrt += source.Dn25PQrt;
					target.Up25PMth += source.Up25PMth;
					target.Dn25PMth += source.Dn25PMth;
					target.Up50PMth += source.Up50PMth;
					target.Dn50PMth += source.Dn50PMth;
					target.Up13P34D += source.Up13P34D;
					target.Dn13P34D += source.Dn13P34D;
					target.Up20P5D += source.Up20P5D;
					target.Dn20P5D += source.Dn20P5D;
					target.Above40SMA += source.Above40SMA;
					target.Below40SMA += source.Below40SMA;
					target.NewHighQrt += source.NewHighQrt;
					target.NewLowQrt += source.NewLowQrt;
					target.NewHighMth += source.NewHighMth;
					target.NewLowMth += source.NewLowMth;
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
					upDownRatioNow = entry.Dn4P != 0 ? (double)entry.Up4P / entry.Dn4P : 0;
					double upDownRatioMin5 = entryMin5.Dn4P != 0 ? (double)entryMin5.Up4P / entryMin5.Dn4P : 1; //we return 1 to avoid div by zero below
					double ratio5 = upDownRatioMin5 > DEFAULT_EPSILON ? upDownRatioNow / upDownRatioMin5 : 0.0;
					entry.UpDn4PR5D = ratio5.ToString("0.00000000");
				}

				if (i >= 10)
				{
					var entryMin10 = entries.Values.ElementAt(i - 10);
					double upDownRatioMin10 = entryMin10.Dn4P != 0 ? (double)entryMin10.Up4P / entryMin10.Dn4P : 1; //we return 1 to avoid div by zero below
					double ratio10 = upDownRatioMin10 > DEFAULT_EPSILON ? upDownRatioNow / upDownRatioMin10 : 0.0;
					entry.UpDn4PR10D = ratio10.ToString("0.00000000");
				}
			}

			return entries;
		}

		protected void saveMetadataFile(SortedDictionary<DateTime, Entry> entries)
		{
			var outputFile = new DelimitedFileEngine<Entry>();
			string metadataFile = Path.Combine(OutputDirectory, "_metadata.csv");
			Entry[] fileEntriesRaw = System.IO.File.Exists(metadataFile) ? outputFile.ReadFile(metadataFile) : Array.Empty<Entry>();

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
				string directory = Path.GetDirectoryName(OutputDirectory);
				if (!string.IsNullOrEmpty(directory) && !Directory.Exists(directory))
					Directory.CreateDirectory(directory);

				//output the fixed column data
				outputFile.ErrorManager.ErrorMode = ErrorMode.ThrowException;
				outputFile.HeaderText = outputFile.GetFileHeader();
				string fileContent = outputFile.WriteString(fileEntriesRaw);
				File.WriteAllText(metadataFile, fileContent);
			}
			catch (Exception e)
			{
				Output.WriteLine($"ERROR writing MarketBreadth file: {e.Message}");
				Output.WriteLine($"   Full path attempted: {metadataFile}");
				Output.WriteLine($"   Stack trace: {e.StackTrace}");
			}
		}

		protected void saveFieldFiles(SortedDictionary<DateTime, Entry> entries)
		{
			if (string.IsNullOrEmpty(OutputDirectory) || entries == null || entries.Count == 0)
				return;

			// ensure the output directory exists
			string dir = OutputDirectory.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
			if (!Directory.Exists(dir))
			{
				try
				{
					Directory.CreateDirectory(dir);
				}
				catch (Exception ex)
				{
					Output.WriteLine($"ERROR creating OutputDirectory {dir}: {ex.Message}");
					return;
				}
			}

			var engine = new DelimitedFileEngine<OHLCV>();
			engine.ErrorManager.ErrorMode = ErrorMode.ThrowException;

			// Custom header so the first column is named "Date" (exactly what you asked for and perfect for MultiCharts ASCII import)
			engine.HeaderText = "Date,Open,High,Low,Close,Volume";

			foreach (string fieldName in COLUMNS_TO_OUTPUT)
			{
				string filePath = Path.Combine(dir, fieldName + ".csv");

				var records = new List<OHLCV>(entries.Count);

				// reflection lets us pull any field by its name from the Entry object (clean and future-proof)
				var prop = typeof(Entry).GetField(fieldName, BindingFlags.Public | BindingFlags.Instance);
				if (prop == null)
				{
					Output.WriteLine($"Warning: Field '{fieldName}' not found in Entry class - skipping file");
					continue;
				}

				foreach (var kvp in entries)
				{
					Entry e = kvp.Value;
					object rawValue = prop.GetValue(e);

					double fieldValue = 0;

					if (rawValue is int intVal)
					{
						fieldValue = intVal;
					}
					else if (rawValue is string strVal && double.TryParse(strVal, System.Globalization.NumberStyles.Any, System.Globalization.CultureInfo.InvariantCulture, out double dblVal))
					{
						// Ratios are stored as strings (e.g. "1.23456789"). We scale by 1 000 000 so we keep 6 decimal places of precision
						// inside an int (perfect for MultiCharts "price" field). You can divide by 1_000_000 later in your indicator if you need the exact ratio.
						fieldValue = dblVal;
					}

					records.Add(new OHLCV
					{
						TimeStamp = e.TimeStamp.Date,   // Date field (formatted ddMMyyyy by the converter)
						Open = fieldValue,
						High = fieldValue,
						Low = fieldValue,
						Close = fieldValue,
						Volume = 0
					});
				}

				try
				{
					string content = engine.WriteString(records.ToArray());
					File.WriteAllText(filePath, content);
					Output.WriteLine($"✅ Saved {records.Count} records to {filePath}");
				}
				catch (Exception ex)
				{
					Output.WriteLine($"ERROR writing {fieldName}.csv: {ex.Message}");
				}
			}
		}

		//methods


	}
}