using arookas;
using System;
using System.Collections.Generic;
using System.Linq;

namespace VtblGenerator
{
	internal class Manager
	{
		private readonly Structure _ParentStruct = new();
		private readonly List<Structure> _DerivativeStructs = new();

		public Manager(string vtblMangled)
		{
			_ParentStruct.SetFromVtbl(Demangler.Demangle(vtblMangled).Replace("::__vt", ""));
		}

		// First line of the vtable (excluding the first 2 "0"'s) should have index 0
		public void ParseLine(string line, int idx)
		{
			if (line == "0")
			{
				_ParentStruct._Symbols.Add(new(idx * 4, null, null));
				return;
			}

			string dirtyFunc = Demangler.Demangle(line);
			// Remove the parameters to not confuse the qualifier splitting system
			string cleanedFunc = dirtyFunc.Substring(0, dirtyFunc.IndexOf("("));
			List<string> qualifiers = cleanedFunc.Split("::", StringSplitOptions.RemoveEmptyEntries).ToList();
			qualifiers.RemoveAt(qualifiers.Count - 1);

			if (_ParentStruct._Name == qualifiers[^1])
			{
				_ParentStruct._Symbols.Add(new FunctionSymbol(idx * 4, qualifiers, dirtyFunc));
			}
			// Ignore thunk functions
			else if (cleanedFunc.Contains("@"))
			{
				return;
			}
			else
			{
				// If the function isn't from the parent structure, then we can create a new structure
				// or if the structure exists, add to it
				bool found = false;
				foreach (Structure structure in _DerivativeStructs)
				{
					if (structure._Name == qualifiers[^1])
					{
						structure._Symbols.Add(new FunctionSymbol(idx * 4, qualifiers, dirtyFunc));
						found = true;
						break;
					}
				}

				if (!found)
				{
					Structure structure = new()
					{
						_Name = qualifiers[^1]
					};

					for (int i = 0; i < qualifiers.Count - 1; i++)
					{
						structure._Qualifiers.Add(qualifiers[i]);
					}

					structure._Symbols.Add(new FunctionSymbol(idx * 4, qualifiers, dirtyFunc));
					_DerivativeStructs.Add(structure);
				}

				_ParentStruct._Symbols.Add(new(idx * 4, qualifiers, dirtyFunc));
			}
		}

		public void Output()
		{
			List<string> symbolString;
			int maxWidth;

			foreach (Structure structure in _DerivativeStructs)
			{
				foreach (string qual in structure._Qualifiers)
				{
					Console.WriteLine($"namespace {qual} {{");
				}

				Console.WriteLine($"struct {structure._Name} {{");

				// FIX: pad out virtual functions to get the offset generation correctly
				for (int i = 0; i < structure._Symbols.Count; i++)
				{
					if (structure._Symbols[i]._Offset == i * 4)
					{
						continue;
					}

					// FIX: thanks JoshuaMK for reminding me that single inheritance classes
					// share functions 100% from before the last member of the vtbl
					bool found = false;

					if (_DerivativeStructs.Count == 1)
					{
						foreach (var parentSymbol in _ParentStruct._Symbols)
						{
							if (parentSymbol._Offset == i)
							{
								structure._Symbols.Insert(i, parentSymbol);
								if (parentSymbol._Name.Contains("~"))
								{
									structure._Symbols[i]._Name = "~" + structure._Name + "()";
								}
								found = true;
								break;
							}
						}
					}

					if (!found)
					{
						structure._Symbols.Insert(i, new(i * 4, null, null));
					}
				}

				symbolString = new();
				maxWidth = 0;
				for (int i = 0; i < structure._Symbols.Count; i++)
				{
					FunctionSymbol symbol = structure._Symbols[i];

					string symbolStr = symbol.ToString();
					if (maxWidth < symbolStr.Length)
					{
						maxWidth = symbolStr.Length + 1;
					}
					symbolString.Add(symbolStr);
				}

				for (int i = 0; i < symbolString.Count; i++)
				{
					Console.Write($"\t{symbolString[i]}");

					for (int j = 0; j < maxWidth - symbolString[i].Length; j++)
					{
						Console.Write(" ");
					}

					Console.WriteLine($"// _{structure._Symbols[i]._Offset.ToString("X2").ToUpper()}");
				}

				Console.WriteLine("\n\t// _00 VTBL\n");
				Console.WriteLine("};");

				for (int i = structure._Qualifiers.Count - 1; i >= 0; i--)
				{
					string qual = structure._Qualifiers[i];
					Console.WriteLine($"}} // namespace {qual}");
				}
				Console.WriteLine();
			}

			// MAIN STRUCTURE
			foreach (string qual in _ParentStruct._Qualifiers)
			{
				Console.WriteLine($"namespace {qual} {{");
			}

			Console.Write($"struct {_ParentStruct._Name}");

			if (_DerivativeStructs.Count != 0)
			{
				Console.Write($" : public {_DerivativeStructs[0]._Name}");
				for (int i = 1; i < _DerivativeStructs.Count; i++)
				{
					Console.Write($", public {_DerivativeStructs[i]._Name}");
				}
			}

			Console.WriteLine(" {");

			// HACK: pad out virtual functions to get the offset generation correctly
			for (int i = 0; i < _ParentStruct._Symbols.Count; i++)
			{
				if (_ParentStruct._Symbols[i]._Offset != i * 4)
				{
					_ParentStruct._Symbols.Insert(i, new(i * 4, null, null));
				}
			}

			symbolString = new();
			maxWidth = 0;
			for (int i = 0; i < _ParentStruct._Symbols.Count; i++)
			{
				FunctionSymbol symbol = _ParentStruct._Symbols[i];

				string symbolStr = symbol.ToString();
				if (maxWidth < symbolStr.Length)
				{
					maxWidth = symbolStr.Length + 1;
				}
				symbolString.Add(symbolStr);
			}

			for (int i = 0; i < symbolString.Count; i++)
			{
				Console.Write($"\t{symbolString[i]}");

				for (int j = 0; j < maxWidth - symbolString[i].Length; j++)
				{
					Console.Write(" ");
				}

				Console.WriteLine($"// _{_ParentStruct._Symbols[i]._Offset.ToString("X2").ToUpper()}");
			}

			Console.WriteLine("\n\t// _00 VTBL\n");
			Console.WriteLine("};");

			for (int i = _ParentStruct._Qualifiers.Count - 1; i >= 0; i--)
			{
				string qual = _ParentStruct._Qualifiers[i];
				Console.WriteLine($"}} // namespace {qual}");
			}
			Console.WriteLine();
		}
	}

	internal class Structure
	{
		public string _Name;
		public List<string> _Qualifiers = new();
		public List<FunctionSymbol> _Symbols = new();

		// Param is demangled vtbl string
		public void SetFromVtbl(string vtblStr)
		{
			string[] qualifiers = vtblStr.Split("::", StringSplitOptions.RemoveEmptyEntries);
			_Name = qualifiers[^1];
			for (int i = 0; i < qualifiers.Length - 1; i++)
			{
				_Qualifiers.Add(qualifiers[i]);
			}
		}
	}

	internal class FunctionSymbol
	{
		public List<string> _Qualifiers = new();
		public string _Name = null;
		public int _Offset = 0;
		public bool _IsPureVirtual = false;

#nullable enable
		public FunctionSymbol(int offset, List<string>? qualifiers, string? name)
		{
			if (name != null)
			{
				_Name = name;
			}
			if (qualifiers != null)
			{
				_Qualifiers = qualifiers;
			}
			_Offset = offset;
		}
#nullable disable

		public override string ToString()
		{
			string output = "virtual ";

			if (_Name != null)
			{
				string name = _Name;
				for (int i = 0; i < _Qualifiers.Count; i++)
				{
					name = name.Replace(_Qualifiers[i] + "::", "");
				}
				// Normal functions
				if (!name.Contains("~"))
				{
					output += "void " + name;
				}
				else
				{
					output += name;
				}
			}
			else
			{
				// Pure virtual functions
				output += $"void _{_Offset.ToString("X2").ToUpper()}() = 0";
			}

			return output + ";";
		}
	}

	internal class Program
	{
		private static void Main()
		{
			Console.WriteLine("[INPUT START]");
			List<string> lines = new();
			string currentInp = Console.ReadLine();
			while (!string.IsNullOrEmpty(currentInp))
			{
				lines.Add(currentInp);
				currentInp = Console.ReadLine();
			}
			Console.WriteLine("[INPUT END]");
			if (lines.Count == 0)
			{
				Console.WriteLine("Copy and paste __vt__ definition from an .s file!");
				return;
			}

			Console.WriteLine("[OUTPUT START]");

			// Remove empty lines
			lines = lines.Where(s => !string.IsNullOrEmpty(s)).ToList();

			// Get the class the virtual table is a part of
			string mainClassName = Demangler.Demangle(lines[0].Replace(":", "")).Replace("::__vt", "");

			// Clean the lines to remove the .4byte
			for (int i = 0; i < lines.Count; i++)
			{
				lines[i] = lines[i].Replace(".4byte", "").Trim();
			}

			Manager manager = new(lines[0]);
			for (int i = 3; i < lines.Count; i++)
			{
				manager.ParseLine(lines[i].Replace("\"", ""), i - 3);
			}
			manager.Output();
			Console.WriteLine("[OUTPUT END]");
		}
	}
}
