using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace Shifter
{
	internal class Program
	{
		public class FILE
		{
			public string name = "";
			public List<string> lines = new();
			public bool edited = false;
		}


		public class RamFs
		{
			public List<FILE> files = new();

			public void Populate(string folder)
			{
				string[] rFiles = Directory.GetFiles(folder);
				files.AddRange(rFiles.Select(f => new FILE() { name = f, lines = File.ReadAllLines(f).ToList() }));
			}

			public void Populate(string[] fileList)
			{
				files.AddRange(fileList.Select(f => new FILE() { name = f, lines = File.ReadAllLines(f).ToList() }));
			}

			public void WriteEdits()
			{
				foreach (FILE file in files.Where(file => file.edited))
				{
					File.WriteAllLines(file.name, file.lines);
					file.edited = false;
				}
			}
		}

		private static void Main(string[] args)
		{
			Console.Write("Input the ASM directory of your project: ");
			List<string> asmFiles = new List<string>();
			asmFiles.AddRange(Directory.GetFiles(Console.ReadLine(), "*.s", SearchOption.AllDirectories));

			RamFs fs = new();
			fs.Populate(asmFiles.ToArray());

			RamFs secondaryFs = new();
			secondaryFs.Populate(asmFiles.ToArray());
			foreach (FILE file in fs.files)
			{
				for (int i = 0; i < file.lines.Count; i++)
				{
					string rawLine = file.lines[i];
					string line = rawLine.Trim();

					if (line.StartsWith("#") || !line.Contains(".4byte"))
					{
						continue;
					}

					string[] tokens = line.Split(' ');
					if (tokens.Length != 2 || !tokens[1].StartsWith("0x80"))
					{
						continue;
					}

					long offset = Convert.ToInt64(tokens[1], 16);
					if (offset <= 0x80003100 && offset % 4 == 0)
					{
						continue;
					}

					string offset_str = offset.ToString("X");
					foreach (FILE f in secondaryFs.files)
					{
						for (int j = 0; j < f.lines.Count; j++)
						{
							string l = f.lines[j];
							if (!l.StartsWith("/* " + offset_str))
							{
								continue;
							}

							// If the label already exists, we can just reference it
							if (f.lines[j - 1].StartsWith("lbl_"))
							{
								// Make sure it's global, and if it's not, make it
								if (!f.lines[j - 2].Contains(".global"))
								{
									f.lines.Insert(j - 1, $".global lbl_{offset_str}");
									j += 1;

									f.edited = true;
								}

								tokens[1] = $"lbl_{offset_str}";
								file.lines[i] = "\t" + string.Join(" ", tokens);
								file.edited = true;
							}
							else
							{
								string label = $"lbl_{offset_str}";
								if (f.lines[j - 1].Contains(":"))
								{
									label = f.lines[j - 1].Replace(":", "");
								}
								else
								{
									f.lines.Insert(j, label + ":");
								}

								tokens[1] = label;
								file.lines[i] = "\t" + string.Join(" ", tokens);
								file.edited = true;
							}
						}
					}
					secondaryFs.WriteEdits();
				}

				Console.WriteLine(file.name);
				fs.WriteEdits();
			}
		}
	}
}