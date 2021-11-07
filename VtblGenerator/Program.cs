using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using arookas;

namespace VtblGenerator
{
	public class Structure
	{
		// Name of the structure
		public string _Name = string.Empty;
		// Last element is always guaranteed to be the same as _Name
		public List<string> _QualifiedNames = new();
		// Virtual functions inside the structure
		public List<string> _Functions = new();

		public Structure()
		{
		}

		public Structure(string name, List<string> functionsDemangled)
		{
			string[] qualifiers = name.Split("::", StringSplitOptions.RemoveEmptyEntries);

			_Name = qualifiers[^1];
			_QualifiedNames.AddRange(qualifiers);
			_Functions.AddRange(functionsDemangled);
		}

		public void Output()
		{
			// Output the surroundings
			for (int i = 0; i < _QualifiedNames.Count - 1; i++)
			{
				Console.WriteLine($"namespace {_QualifiedNames[i].Trim()} {{ // Unsure if namespace or structure!");
			}

			// For padding calculations
			int maxStringLength = 0;

			List<string> toPrintFuncs = new();
			List<string> toPrintComments = new();
			List<Structure> otherStructures = new();

			for (int i = 0; i < _Functions.Count; i++)
			{
				string curFunc = _Functions[i].Trim();
				// Start comment with the offset of the function
				string commentString = $"// _{i * 4:X2}";

				if (curFunc.Contains(_Name))
				{
					curFunc = curFunc.Replace(_Name + "::", "");
				}

				// Remove structure qualifiers at the start of the string
				for (int j = 0; j < _QualifiedNames.Count - 1; j++)
				{
					if (curFunc.StartsWith(_QualifiedNames[j]))
					{
						commentString += ", from " + curFunc.Substring(0, _QualifiedNames[j].Length + 2).Trim();
						curFunc = curFunc.Remove(0, _QualifiedNames[j].Length + 2);
					}
				}

				// Function is a demangled symbol, and not a pure virtual
				if (!curFunc.StartsWith("virtual void"))
				{
					string noParams = curFunc.Substring(0, curFunc.IndexOf("("));
					string[] otherQualifiedNames = noParams.Split("::", StringSplitOptions.RemoveEmptyEntries);
					if (otherQualifiedNames.Length > 1)
					{
						bool found = false;

						foreach (Structure structure in otherStructures)
						{
							for (int j = 0; j < otherQualifiedNames.Length; j++)
							{
								if (structure._Name == otherQualifiedNames[j])
								{
									structure._Functions.Add(curFunc);
									found = true;
									break;
								}
							}
						}

						if (!found)
						{
							Structure structure = new(otherQualifiedNames[0], new() { curFunc });
							otherStructures.Add(structure);
						}

						commentString += ", from " + curFunc.Substring(0, otherQualifiedNames[0].Length + 2).Trim();
						curFunc = curFunc.Remove(0, otherQualifiedNames[0].Length + 2);
					}

					// Make sure the function is a virtual void and NOT a dtor/ctor
					bool isDtor = curFunc.StartsWith("~");

					if (!isDtor)
					{
						curFunc = $"virtual void {curFunc}";
					}
					else
					{
						curFunc = $"virtual {curFunc};";
					}
				}

				if (maxStringLength < curFunc.Length)
				{
					maxStringLength = curFunc.Length;
				}

				toPrintFuncs.Add("\t" + curFunc);
				toPrintComments.Add(commentString);
			}

			foreach (var structure in otherStructures)
			{
				structure.Output();
				Console.WriteLine();
			}

			Console.Write($"struct {_Name}");

			if (otherStructures.Count != 0)
			{
				Console.Write($" : public {otherStructures[0]._Name}");
				for (int i = 1; i < otherStructures.Count; i++)
				{
					Console.Write($", public {otherStructures[i]._Name}");
				}
			}

			Console.WriteLine("\n{");
			for (int i = 0; i < toPrintFuncs.Count; i++)
			{
				Console.Write(toPrintFuncs[i]);

				// Align the comments
				for (int j = 0; j < (maxStringLength - toPrintFuncs[i].Length) + 2; j++)
				{
					Console.Write(" ");
				}
				Console.WriteLine(toPrintComments[i]);
			}
			Console.WriteLine("\n\t// _00, VTBL");
			Console.WriteLine("};");


			for (int i = _QualifiedNames.Count - 2; i >= 0; i--)
			{
				Console.WriteLine($"}} // namespace {_QualifiedNames[i]}");
			}
		}
	}


	class Program
	{
		static void Main(string[] args)
		{
			List<string> lines = new();
			string currentInp = Console.ReadLine();
			while (!string.IsNullOrEmpty(currentInp))
			{
				lines.Add(currentInp);
				currentInp = Console.ReadLine();
			}

			if (lines.Count == 0)
			{
				Console.WriteLine("Copy and paste __vt__ definition from an .s file!");
				return;
			}

			Console.WriteLine("// NOTE: THE SCOPE AND FUNCTION OFFSETS OF ALL CLASSES BESIDES THE ONE YOU INPUTTED MAY BE WRONG!\n");

			// Remove empty lines
			lines = lines.Where(s => !string.IsNullOrEmpty(s)).ToList();

			List<string> functions = new();
			bool sameScope = false;
			string prevScope = "";
			for (int i = 0; i < lines.Count; i++)
			{
				string line = lines[i].Trim();

				if (!line.StartsWith(".4byte") || (line.StartsWith(".4byte 0") && i < 3))
				{
					continue;
				}

				string symbol = line.Split(" ")[1];

				int vtblOffset = (i - 3) * 4;
				string demangled;
				if (symbol != "0")
				{
					demangled = Demangler.Demangle(symbol.Replace("\"", ""));

					string scopeStr = demangled.Split("::")[0];
					sameScope = scopeStr == prevScope;
					prevScope = scopeStr;
				}
				else
				{
					demangled = $"virtual void _{vtblOffset:X2}() = 0;";
				}

				functions.Add(demangled);
			}

			// Create a main structure and output it
			string rawStr = Demangler.Demangle(lines[0].Replace(":", "")).Replace("__vt", "");
			new Structure(rawStr, functions)
				.Output();
		}
	}
}
