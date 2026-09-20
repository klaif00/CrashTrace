/*
 * CrashTrace - Crash Diagnostic Tool
 * Copyright (c) 2026 Limen
 * Licensed under the MIT License.
 */

/*
 * Developed by Limen
 * 
 * Reads .NET assembly references from a managed EXE/DLL.
 *
 * Native programs declare their dependencies in the PE import table,
 * but .NET programs declare them in the CLR metadata as "assembly
 * references". That is a completely different structure, and this
 * class reads it.
 *
 * We use Assembly.ReflectionOnlyLoadFrom, which loads only the
 * metadata (no code execution, no side effects). Then we read the
 * GetReferencedAssemblies() list, which is exactly what the CLR
 * would need to resolve at runtime.
 */
using System;
using System.Collections.Generic;
using System.Reflection;

namespace TestApp
{
	internal static class ClrAssemblyReader
	{
		// Returns the list of referenced assembly names (with .dll appended
		// if missing) for the given managed file. Returns an empty list on
		// any failure, so callers can treat the file as "no references".
		public static List<string> GetReferences(string filePath)
		{
			List<string> result = new List<string>();
			try
			{
				// Reflection-only load: reads metadata without executing code.
				Assembly asm = Assembly.ReflectionOnlyLoadFrom(filePath);
				foreach (AssemblyName refName in asm.GetReferencedAssemblies())
				{
					string n = refName.Name;
					if (string.IsNullOrEmpty(n)) continue;

					// Normalize to a .dll name so the rest of the tool can
					// compare references against files on disk.
					if (!n.EndsWith(".dll", StringComparison.OrdinalIgnoreCase))
						n = n + ".dll";

					// Skip duplicates.
					if (!result.Contains(n)) result.Add(n);
				}
			}
			catch
			{
			}
			return result;
		}
	}
}