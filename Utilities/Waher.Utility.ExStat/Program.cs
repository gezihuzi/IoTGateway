﻿using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Xml;

namespace Waher.Utility.ExStat
{
	public class Program
	{
		private const int BufSize = 16 * 65536;

		/// <summary>
		/// Searches through exception log files in a folder, counting exceptions,
		/// genering an XML file with some basic statistics.
		/// 
		/// Command line switches:
		/// 
		/// -p PATH               Path to start the search. If not provided, the
		///                       current path will be used, with *.* as search
		///                       pattern. Can be used multiple times to search
		///                       through multiple paths, or use multiple search
		///                       patterns.
		/// -x FILENAME           Export findings to an XML file.
		/// -s                    Include subfolders in search.
		/// -?                    Help.
		/// </summary>
		static int Main(string[] args)
		{
			FileStream FileOutput = null;
			XmlWriter Output = null;
			List<string> Paths = new List<string>();
			string XmlFileName = null;
			bool Subfolders = false;
			bool Help = false;
			int i = 0;
			int c = args.Length;
			string s;

			try
			{
				while (i < c)
				{
					s = args[i++].ToLower();

					switch (s)
					{
						case "-p":
							if (i >= c)
								throw new Exception("Missing path.");

							Paths.Add(args[i++]);
							break;

						case "-x":
							if (i >= c)
								throw new Exception("Missing export filename.");

							if (string.IsNullOrEmpty(XmlFileName))
								XmlFileName = args[i++];
							else
								throw new Exception("Only one export file allowed.");
							break;

						case "-s":
							Subfolders = true;
							break;

						case "-?":
							Help = true;
							break;

						default:
							throw new Exception("Unrecognized switch: " + s);
					}
				}

				if (Help || c == 0)
				{
					Console.Out.WriteLine("Searches through exception log files in a folder, counting exceptions,");
					Console.Out.WriteLine("genering an XML file with some basic statistics.");
					Console.Out.WriteLine();
					Console.Out.WriteLine("Command line switches:");
					Console.Out.WriteLine();
					Console.Out.WriteLine("-p PATH               Path to start the search. If not provided, the");
					Console.Out.WriteLine("                      current path will be used, with *.* as search");
					Console.Out.WriteLine("                      pattern. Can be used multiple times to search");
					Console.Out.WriteLine("                      through multiple paths, or use multiple search");
					Console.Out.WriteLine("                      patterns.");
					Console.Out.WriteLine("-x FILENAME           Export findings to an XML file.");
					Console.Out.WriteLine("-enc ENCODING         Text encoding if Byte-order-marks not available.");
					Console.Out.WriteLine("                      Default=UTF-8");
					Console.Out.WriteLine("-s                    Include subfolders in search.");
					Console.Out.WriteLine("-?                    Help.");
					return 0;
				}

				string SearchPattern;

				if (Paths.Count == 0)
					Paths.Add(Directory.GetCurrentDirectory());

				if (string.IsNullOrEmpty(XmlFileName))
					throw new Exception("Missing output file name.");

				XmlWriterSettings Settings = new XmlWriterSettings()
				{
					CloseOutput = true,
					ConformanceLevel = ConformanceLevel.Document,
					Encoding = Encoding.UTF8,
					Indent = true,
					IndentChars = "\t",
					NewLineChars = "\n",
					NewLineHandling = NewLineHandling.Entitize,
					NewLineOnAttributes = false,
					OmitXmlDeclaration = false,
					WriteEndDocumentOnClose = true, 
					CheckCharacters = false
				};

				FileOutput = File.Create(XmlFileName);
				Output = XmlWriter.Create(FileOutput, Settings);

				Output.WriteStartDocument();
				Output.WriteStartElement("Statistics", "http://waher.se/schema/ExStat.xsd");

				Dictionary<string, bool> FileProcessed = new Dictionary<string, bool>();

				foreach (string Path0 in Paths)
				{
					string Path = Path0;

					if (Directory.Exists(Path))
						SearchPattern = "*.*";
					else
					{
						SearchPattern = System.IO.Path.GetFileName(Path);
						Path = System.IO.Path.GetDirectoryName(Path);

						if (!Directory.Exists(Path))
							throw new Exception("Path does not exist.");
					}

					string[] FileNames = Directory.GetFiles(Path, SearchPattern, Subfolders ? SearchOption.AllDirectories : SearchOption.TopDirectoryOnly);
					Statistics Statistics = new Statistics();
					byte[] Buffer = new byte[BufSize];
					int NrRead = 0;
					int Last = 0;
					int j;
					bool SkipHyphens;

					foreach (string FileName in FileNames)
					{
						if (FileProcessed.ContainsKey(FileName))
							continue;

						FileProcessed[FileName] = true;

						Console.Out.WriteLine(FileName + "...");

						try
						{
							using (FileStream fs = File.OpenRead(FileName))
							{
								do
								{
									SkipHyphens = false;
									i = 0;

									if (Last > 0)
									{
										if (Last < BufSize)
											Array.Copy(Buffer, Last, Buffer, 0, (i = BufSize - Last));
										else
											SkipHyphens = true;

										Last = 0;
									}

									NrRead = fs.Read(Buffer, i, BufSize - i);
									if (NrRead <= 0)
										break;

									if (SkipHyphens)
									{
										while (i < BufSize && NrRead > 0 && Buffer[i] == '-')
											i++;

										Last = i;
									}
									else
									{
										NrRead += i;
										i = 0;
									}

									j = NrRead - 4;
									while (i < j)
									{
										if (Buffer[i] == '-' && Buffer[i + 1] == '-' && Buffer[i + 2] == '-' &&
											Buffer[i + 3] == '-' && Buffer[i + 4] == '-')
										{
											s = Encoding.Default.GetString(Buffer, Last, i - Last);
											Process(s, Statistics);

											i += 5;
											while (i < NrRead && Buffer[i] == '-')
												i++;

											Last = i;
										}
										else
											i++;
									}
								}
								while (NrRead == BufSize);

								if (Last < NrRead)
								{
									s = Encoding.Default.GetString(Buffer, Last, NrRead - Last);
									Process(s, Statistics);
								}
							}
						}
						catch (Exception ex)
						{
							Console.Out.WriteLine(ex.Message);
						}
					}


					Export(Output, "PerType", "type", Statistics.PerType, "PerMessage", "PerSource");
					Export(Output, "PerMessage", "message", Statistics.PerMessage, "PerType", "PerSource");
					Export(Output, "PerSource", string.Empty, Statistics.PerSource, "PerType", "PerMessage");
					Export(Output, "PerHour", "hour", "yyyy-MM-ddTHH", Statistics.PerHour);
					Export(Output, "PerDay", "day", "yyyy-MM-dd", Statistics.PerDay);
					Export(Output, "PerMonth", "month", "yyyy-MM", Statistics.PerMonth);
				}

				Output.WriteEndElement();
				Output.WriteEndDocument();

				return 0;
			}
			catch (Exception ex)
			{
				Console.Out.WriteLine(ex.Message);
				return -1;
			}
			finally
			{
				Output?.Flush();
				Output?.Close();
				Output?.Dispose();
				FileOutput?.Dispose();
			}
		}

		private static void Export(XmlWriter Output, string ElementName, string AttributeName, Histogram<string> Histogram, params string[] SubElementNames)
		{
			KeyValuePair<string, Bucket>[] A = new KeyValuePair<string, Bucket>[Histogram.Buckets.Count];
			Histogram.Buckets.CopyTo(A, 0);
			Array.Sort<KeyValuePair<string, Bucket>>(A, (r1, r2) =>
			{
				long i = r2.Value.Count - r1.Value.Count;
				if (i != 0)
				{
					if (i > int.MaxValue)
						i = int.MaxValue;
					else if (i < int.MinValue)
						i = int.MinValue;

					return (int)i;
				}

				return string.Compare(r1.Key, r2.Key);
			});

			Output.WriteStartElement(ElementName);

			string s;
			bool AsValue = string.IsNullOrEmpty(AttributeName);

			foreach (KeyValuePair<string, Bucket> Rec in A)
			{
				Output.WriteStartElement("Stat");
				Output.WriteAttributeString("count", Rec.Value.Count.ToString());

				s = Rec.Key.Replace("\r\n", "\n").Replace('\r', '\n');
				if (AsValue)
					Output.WriteElementString("Value", s);
				else
					Output.WriteAttributeString(AttributeName, s);

				if (!(Rec.Value.SubHistograms is null))
					Export(Output, Rec.Value.SubHistograms, SubElementNames);

				Output.WriteEndElement();
			}

			Output.WriteEndElement();
		}

		private static void Export(XmlWriter Output, Histogram<string>[] SubHistograms, params string[] ElementNames)
		{
			int i, c = SubHistograms.Length, d = ElementNames.Length;

			for (i = 0; i < c; i++)
				Export(Output, i < d ? ElementNames[i] : "Details", string.Empty, SubHistograms[i]);
		}

		private static void Export(XmlWriter Output, string ElementName, string AttributeName, string DateTimeFormat,
			Histogram<DateTime> Histogram, params string[] SubElementNames)
		{
			Output.WriteStartElement(ElementName);

			foreach (KeyValuePair<DateTime, Bucket> Rec in Histogram.Buckets)
			{
				Output.WriteStartElement("Stat");
				Output.WriteAttributeString(AttributeName, Rec.Key.ToString(DateTimeFormat));
				Output.WriteAttributeString("count", Rec.Value.Count.ToString());

				if (!(Rec.Value.SubHistograms is null))
					Export(Output, Rec.Value.SubHistograms, SubElementNames);

				Output.WriteEndElement();
			}

			Output.WriteEndElement();
		}

		private static void Process(string s, Statistics Statistics)
		{
			int i = s.IndexOf("Type:");
			if (i < 0)
				return;

			int j = s.IndexOf('\n', i);
			if (j < 0)
				return;

			string Type = s.Substring(i + 5, j - i - 5).Trim();

			i = s.IndexOf("Time:", j);
			if (i < 0)
				return;

			j = s.IndexOf('\n', i);
			if (j < 0)
				return;

			string TimeStr = s.Substring(i + 5, j - i - 5).Trim();
			if (!DateTime.TryParse(TimeStr, out DateTime Time))
				return;

			DateTime Day = Time.Date;
			DateTime Month = new DateTime(Day.Year, Day.Month, 1);
			DateTime Hour = new DateTime(Time.Year, Time.Month, Time.Day, Time.Hour, 0, 0);

			i = j;
			j = s.IndexOf("   at", i);
			if (j < 0)
				return;

			string Message = s.Substring(i, j - i).Trim();
			string StackTrace = s.Substring(j).TrimEnd();

			Statistics.PerType.Inc(Type, Message, StackTrace);
			Statistics.PerMessage.Inc(Message, Type, StackTrace);
			Statistics.PerSource.Inc(StackTrace, Type, Message);
			Statistics.PerHour.Inc(Hour);
			Statistics.PerDay.Inc(Day);
			Statistics.PerMonth.Inc(Month);
		}

	}
}
