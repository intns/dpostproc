using arookas;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace VtblGenerator
{
	internal class Manager
	{
		private readonly Structure _ParentStruct = new();
		private readonly List<Structure> _DerivativeStructs = new();
		public List<string> _OriginalVtbl = new();

		public Manager(string vtblMangled)
		{
			_ParentStruct.SetFromVtbl(Demangler.Demangle(vtblMangled).Replace("::__vt", ""));
		}

		private void AddToParentStruct(int idx, List<string>? qualifiers, string? dirtyFunc, bool? isWeak)
		{
			_ParentStruct._Symbols.Add(new FunctionSymbol(idx, qualifiers, dirtyFunc, isWeak));
		}

		private void AddToDerivativeStruct(int idx, List<string>? qualifiers, string? dirtyFunc, bool? isWeak)
		{
			// If the function isn't from the parent structure, then we can create a new structure
			// or if the structure exists, add to it
			bool found = false;
			foreach (Structure s in _DerivativeStructs)
			{
				if (s._Name == qualifiers[^1])
				{
					s._Symbols.Add(new FunctionSymbol(idx, qualifiers, dirtyFunc, isWeak));
					found = true;
					break;
				}
			}

			if (found)
			{
				return;
			}

			Structure structure = new()
			{
				_Name = qualifiers[^1]
			};

			for (int i = 0; i < qualifiers.Count - 1; i++)
			{
				structure._Qualifiers.Add(qualifiers[i]);
			}

			structure._Symbols.Add(new FunctionSymbol(idx, qualifiers, dirtyFunc, isWeak));
			_DerivativeStructs.Add(structure);
		}

		// First line of the vtable (excluding the first 2 "0"'s) should have index 0
		public void ParseLine(string line, int idx, ref List<string> linkMapSrc)
		{
			// Add pure virtual function
			if (line == "0")
			{
				AddToParentStruct(8 + (idx * 4), null, null, null);
				return;
			}
			// Ignore thunk functions
			else if (line.Contains("@"))
			{
				return;
			}

			// Check the linker map to see if the function is weak
			bool isWeak = false;
			for (int i = 0; i < linkMapSrc.Count; i++)
			{
				string lm = linkMapSrc[i];
				if (lm.Contains(line) && lm.Contains("func,weak"))
				{
					isWeak = true;
					break;
				}
			}

			string dirtyFunc = Demangler.Demangle(line);

			// Remove the parameters to not confuse the qualifier splitting system
			// and then remove the name from the qualifiers
			string cleanedFunc = dirtyFunc[..dirtyFunc.IndexOf("(")];
			List<string> qualifiers = cleanedFunc.Split("::", StringSplitOptions.RemoveEmptyEntries).ToList();
			qualifiers.RemoveAt(qualifiers.Count - 1);

			bool inParentStruct = true;

			// If the amount of qualifiers matches, then we can test it
			if (qualifiers.Count == (_ParentStruct._Qualifiers.Count+1))
			{
				// Check every qualifier 
				for (int i = 0; i < _ParentStruct._Qualifiers.Count; i++)
				{
					if (_ParentStruct._Qualifiers[i] != qualifiers[i])
					{
						inParentStruct = false;
						break;
					}
				}

				if (inParentStruct)
				{
					// Check if the name matches, if so, then we've found it!
					inParentStruct = _ParentStruct._Name == qualifiers[^1];
				}
			}
			else
			{
				// If not, we're not a virtual function of the parent class
				inParentStruct = false;
			}

			// Virtual tables are always offset by 8, and (idx * 4) because
			// the offsets are 32 bit integers (4 bytes)
			int trueOffset = 8 + (idx * 4);
			if (inParentStruct)
			{
				AddToParentStruct(trueOffset, qualifiers, dirtyFunc, isWeak);
			}
			else
			{
				AddToDerivativeStruct(trueOffset, qualifiers, dirtyFunc, isWeak);
			}
		}

		public void Output(ref List<string> symbolMapSrc)
		{
			List<string> symbolString;
			int maxWidth;

			string filePath = string.Join("/", _ParentStruct._Qualifiers) + "/" + _ParentStruct._Name + ".h";
			Directory.CreateDirectory(Directory.GetCurrentDirectory() + "/out/");
			string outFolder = Directory.GetCurrentDirectory() + "/out/";
			for (int i = 0; i < _ParentStruct._Qualifiers.Count; i++)
			{
				outFolder += _ParentStruct._Qualifiers[i] + "/";
				Directory.CreateDirectory(outFolder);
			}
			using StreamWriter newFile = File.CreateText(Directory.GetCurrentDirectory() + "/out/" + filePath);

			string ifdef = filePath.Replace("/", "_").Replace(".h", "").ToUpper() + "_H";
			ifdef = ifdef.TrimStart('_');
			newFile.WriteLine($"#ifndef _{ifdef}");
			newFile.WriteLine($"#define _{ifdef}\n");

			newFile.WriteLine("/*");
			foreach (var vtblLine in _OriginalVtbl)
			{
				newFile.WriteLine("\t" + vtblLine.Trim());
			}
			newFile.WriteLine("*/\n");

			foreach (Structure structure in _DerivativeStructs)
			{
				foreach (string qual in structure._Qualifiers)
				{
					newFile.WriteLine($"namespace {qual} {{");
				}

				newFile.WriteLine($"struct {structure._Name} {{");

				// FIX: pad out virtual functions to get the offset generation correctly
				for (int i = 0; i < structure._Symbols.Count; i++)
				{
					if (structure._Symbols[i]._Offset == 8 + (i * 4))
					{
						continue;
					}

					bool found = false;
					foreach (var parentSymbol in _ParentStruct._Symbols)
					{
						if (parentSymbol._Offset == 8 + (i * 4))
						{
							structure._Symbols.Insert(i, new(parentSymbol._Offset, parentSymbol._Qualifiers, parentSymbol._Name, !string.IsNullOrEmpty(parentSymbol._Comments)));
							if (parentSymbol._Name != null && parentSymbol._Name.Contains("~"))
							{
								structure._Symbols[i]._Name = "~" + structure._Name + "()";
							}
							found = true;
							break;
						}
					}

					if (!found)
					{
						structure._Symbols.Insert(i, new(8 + (i * 4), null, null, null));
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
					newFile.Write($"\t{symbolString[i]}");

					for (int j = 0; j < maxWidth - symbolString[i].Length; j++)
					{
						newFile.Write(" ");
					}

					newFile.WriteLine($"{structure._Symbols[i].GetComments()}");
				}

				newFile.WriteLine("};");

				for (int i = structure._Qualifiers.Count - 1; i >= 0; i--)
				{
					string qual = structure._Qualifiers[i];
					newFile.WriteLine($"}} // namespace {qual}");
				}
				newFile.WriteLine();
			}

			// MAIN STRUCTURE
			foreach (string qual in _ParentStruct._Qualifiers)
			{
				newFile.WriteLine($"namespace {qual} {{");
			}

			newFile.Write($"struct {_ParentStruct._Name}");

			if (_DerivativeStructs.Count != 0)
			{
				newFile.Write($" : public {_DerivativeStructs[0]._Name}");
				for (int i = 1; i < _DerivativeStructs.Count; i++)
				{
					newFile.Write($", public {_DerivativeStructs[i]._Name}");
				}
			}

			newFile.WriteLine(" {");

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
				newFile.Write($"\t{symbolString[i]}");

				for (int j = 0; j < maxWidth - symbolString[i].Length; j++)
				{
					newFile.Write(" ");
				}

				newFile.WriteLine($"{_ParentStruct._Symbols[i].GetComments()}");
			}

			newFile.WriteLine();

			string qualifiedName = $"Q{1 + (_ParentStruct._Qualifiers.Count)}";
			for (int i = 0; i < _ParentStruct._Qualifiers.Count; i++)
			{
				qualifiedName += _ParentStruct._Qualifiers[i].Length;
				qualifiedName += _ParentStruct._Qualifiers[i];
			}
			qualifiedName += _ParentStruct._Name.Length;
			qualifiedName += _ParentStruct._Name;

			for (int i = 0; i < symbolMapSrc.Count; i++)
			{
				if (symbolMapSrc[i].Contains($"__{qualifiedName}"))
				{
					string[] line = symbolMapSrc[i].Split(new char[] { '\t', ' ' }, StringSplitOptions.RemoveEmptyEntries);

					//  0        1      2         3 4                               5            6
					//  004149a0 0000b4 8041a060  4 read__12MapCollisionFR6Stream 	sysCommonU.a mapCollision.cpp
					string function = Demangler.Demangle(line[4]);

					bool exit = false;
					foreach (var symbol in _ParentStruct._Symbols)
					{
						if (symbol._Name == function)
						{
							exit = true;
							break;
						}
					}

					if (exit)
					{
						continue;
					}

					int paramIdx = function.IndexOf("(");
					if (paramIdx == -1)
					{
						continue;
					}

					string cleanedFunc = function[..paramIdx];
					string[] qualifiers = cleanedFunc.Split("::", StringSplitOptions.RemoveEmptyEntries);

					if (qualifiers[^1] == _ParentStruct._Name || qualifiers[^1] == "~" + _ParentStruct._Name)
					{
						string args = function[paramIdx..];
						if (args == "()")
						{
							newFile.WriteLine("\t" + qualifiers[^1] + $"();");
						}
						else
						{
							newFile.WriteLine("\t" + qualifiers[^1] + $"{args};");
						}
					}
					else
					{
						newFile.WriteLine("\tvoid " + qualifiers[^1] + function[paramIdx..] + ";");
					}
				}
			}

			newFile.WriteLine("};");

			for (int i = _ParentStruct._Qualifiers.Count - 1; i >= 0; i--)
			{
				string qual = _ParentStruct._Qualifiers[i];
				newFile.WriteLine($"}} // namespace {qual}");
			}
			newFile.WriteLine();

			newFile.WriteLine("#endif");
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
		public string _Name = string.Empty;
		public int _Offset = 0;
		public bool _IsPureVirtual = false;
		public string _Comments = string.Empty;

#nullable enable
		public FunctionSymbol(int offset, List<string>? qualifiers, string? name, bool? weakFunc)
		{
			if (name != null)
			{
				_Name = name;
			}
			if (qualifiers != null)
			{
				_Qualifiers = qualifiers;
			}
			if (weakFunc != null)
			{
				_Comments = weakFunc.Value ? "(weak)" : "";
			}

			_Offset = offset;
		}
#nullable disable

		public override string ToString()
		{
			string output = "virtual ";

			if (!string.IsNullOrEmpty(_Name))
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

			return output + "; ";
		}

		public string GetComments()
		{
			string comments = "// _" + _Offset.ToString("X2").ToUpper();
			if (_Comments != string.Empty)
			{
				comments += $" {_Comments}";
			}

			return comments;
		}
	}

	internal class Program
	{
		private static void OutputVtbl(List<string> vtblLines, ref List<string> linkMapLines, ref List<string> symbolMapLines)
		{
			Manager manager = new(vtblLines[0]);
			manager._OriginalVtbl = vtblLines.ToArray().ToList();

			// Remove empty lines
			vtblLines = vtblLines.Where(s => !string.IsNullOrEmpty(s)).ToList();

			// Get the class the virtual table is a part of
			string mainClassName = Demangler.Demangle(vtblLines[0].Replace(":", "")).Replace("::__vt", "");

			// Clean the vtblLines to remove the .4byte
			for (int i = 0; i < vtblLines.Count; i++)
			{
				vtblLines[i] = vtblLines[i].Replace(".4byte", "").Trim();
			}

			for (int i = 3; i < vtblLines.Count; i++)
			{
				manager.ParseLine(vtblLines[i].Replace("\"", ""), i - 3, ref linkMapLines);
			}
			manager.Output(ref symbolMapLines);
		}

		private static void Main()
		{
			Console.Write("Path to the linker map: ");
			string linkMapSrc = @"C:\Users\Arun\Downloads\pikmin2_linker.map"; //Console.ReadLine();
			List<string> linkMapContents = new List<string>();
			if (!string.IsNullOrEmpty(linkMapSrc))
			{
				linkMapContents = File.ReadAllLines(linkMapSrc).ToList();

				for (int i = 0; i < linkMapContents.Count; i++)
				{
					if (!linkMapContents[i].Contains("func,weak"))
					{
						linkMapContents.RemoveAt(i);
						i--;
					}
				}
			}

			Console.Write("Path to the symbol map: ");
			string symbolMapPath = @"C:\Users\Arun\Downloads\pik2.map"; //Console.ReadLine();
			List<string> symbolMapContents = new List<string>();
			if (!string.IsNullOrEmpty(symbolMapPath))
			{
				symbolMapContents = File.ReadAllLines(symbolMapPath).ToList();

				for (int i = 0; i < symbolMapContents.Count; i++)
				{
					if (symbolMapContents[i].Contains("UNUSED")
						|| symbolMapContents[i].Contains("__vt__")
						|| symbolMapContents[i].Contains("@")
						|| !symbolMapContents[i].Contains(" 4 ")
						|| string.IsNullOrEmpty(symbolMapContents[i]))
					{
						symbolMapContents.RemoveAt(i);
						i--;
					}
				}
			}

			string[] files = Directory.GetFiles(@"D:\Backups\DESKTOP_08_02_2022\pikmin2\asm" /*Console.ReadLine()*/, "*.s", SearchOption.AllDirectories);
			foreach (var file in files)
			{
				string[] fileContents = File.ReadAllLines(file);
				for (int i = 0; i < fileContents.Length; i++)
				{
					if (fileContents[i].StartsWith("__vt__"))
					{
						List<string> lines = new()
						{
							fileContents[i]
						};
						while (fileContents[++i].Trim().StartsWith(".4byte"))
						{
							lines.Add(fileContents[i]);
						}
						OutputVtbl(lines, ref linkMapContents, ref symbolMapContents);
					}
				}
			}
		}
	}
}
