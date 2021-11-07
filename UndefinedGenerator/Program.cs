using System;
using System.Collections.Generic;
using System.IO;

namespace UndefinedGenerator
{
	class Program
	{
		static void Main(string[] args)
		{
			string pathToLog = Console.ReadLine();
			if (string.IsNullOrWhiteSpace(pathToLog))
			{
				Console.WriteLine("Enter path to the error log file of mwldeppc.exe!");
				return;
			}

			string[] lines = File.ReadAllLines(pathToLog);
			List<string> newLines = new();
			newLines.Add(".include \"macros.inc\"");
			newLines.Add(".section CHANGEME");
			for (int i = 0; i < lines.Length; i++)
			{
				if (lines[i].Contains("undefined: 'lbl_"))
				{
					int lblOffs = lines[i].IndexOf("lbl_");
					string cleanedLabel = lines[i].Substring(lblOffs, 4 + 8);
					Console.WriteLine(cleanedLabel);
					newLines.Add($".global {cleanedLabel}:");
					newLines.Add($"{cleanedLabel}");
				}
			}
			File.WriteAllLines("fix.s", newLines);
		}
	}
}
