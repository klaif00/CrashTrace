/*
 * CrashTrace - Crash Diagnostic Tool
 * Copyright (c) 2026 Limen
 * Licensed under the MIT License.
 */

/*
 * Developed by Limen
 * 
 * 
 * All Win32 / dbghelp P/Invoke declarations, structs and constants used
 * by the debugger engine, kept in one place separate from the UI and
 * orchestration logic in MainForm.cs.
 */
using System;
using System.Runtime.InteropServices;
using System.Text;

namespace TestApp
{
	internal static class NativeMethods
	{
		#region Structs

		// STARTUPINFO for CreateProcess. Field layout matches the
		// Windows SDK definition on both x86 and x64.
		[StructLayout(LayoutKind.Sequential, CharSet = CharSet.Auto)]
		public struct STARTUPINFO
		{
			public int cb;
			public string lpReserved;
			public string lpDesktop;
			public string lpTitle;
			public int dwX;
			public int dwY;
			public int dwXSize;
			public int dwYSize;
			public int dwXCountChars;
			public int dwYCountChars;
			public int dwFillAttribute;
			public int dwFlags;
			public short wShowWindow;
			public short cbReserved2;
			public IntPtr lpReserved2;
			public IntPtr hStdInput;
			public IntPtr hStdOutput;
			public IntPtr hStdError;
		}

		// PROCESS_INFORMATION out parameter of CreateProcess.
		[StructLayout(LayoutKind.Sequential)]
		public struct PROCESS_INFORMATION
		{
			public IntPtr hProcess;
			public IntPtr hThread;
			public uint dwProcessId;
			public uint dwThreadId;
		}

		// MEMORY_BASIC_INFORMATION returned by VirtualQueryEx.
		[StructLayout(LayoutKind.Sequential)]
		public struct MEMORY_BASIC_INFORMATION
		{
			public IntPtr BaseAddress;
			public IntPtr AllocationBase;
			public uint AllocationProtect;
			public IntPtr RegionSize;
			public uint State;
			public uint Protect;
			public uint Type;
		}

		// Floating-point area of the WOW64 context.
		[StructLayout(LayoutKind.Sequential)]
		public struct WOW64_FLOATING_SAVE_AREA
		{
			public uint ControlWord;
			public uint StatusWord;
			public uint TagWord;
			public uint ErrorOffset;
			public uint ErrorSelector;
			public uint DataOffset;
			public uint DataSelector;
			[MarshalAs(UnmanagedType.ByValArray, SizeConst = 80)]
			public byte[] RegisterArea;
			public uint Cr0NpxState;
		}

		// 32-bit thread context as returned by Wow64GetThreadContext.
		// The array fields must be sized by the caller before the call.
		[StructLayout(LayoutKind.Sequential)]
		public struct WOW64_CONTEXT
		{
			public uint ContextFlags;
			public uint Dr0;
			public uint Dr1;
			public uint Dr2;
			public uint Dr3;
			public uint Dr6;
			public uint Dr7;
			public WOW64_FLOATING_SAVE_AREA FloatSave;
			public uint SegGs;
			public uint SegFs;
			public uint SegEs;
			public uint SegDs;
			public uint Edi;
			public uint Esi;
			public uint Ebx;
			public uint Edx;
			public uint Ecx;
			public uint Eax;
			public uint Ebp;
			public uint Eip;
			public uint SegCs;
			public uint EFlags;
			public uint Esp;
			public uint SegSs;
			[MarshalAs(UnmanagedType.ByValArray, SizeConst = 512)]
			public byte[] ExtendedRegisters;
		}

		// 64-bit thread context. Explicit layout with the field offsets
		// from the Windows SDK so we only declare what we need.
		[StructLayout(LayoutKind.Explicit, Size = 1232)]
		public struct CONTEXT_AMD64
		{
			[FieldOffset(0x30)] public uint ContextFlags;
			[FieldOffset(0x38)] public ushort SegCs;
			[FieldOffset(0x3A)] public ushort SegDs;
			[FieldOffset(0x3C)] public ushort SegEs;
			[FieldOffset(0x3E)] public ushort SegFs;
			[FieldOffset(0x40)] public ushort SegGs;
			[FieldOffset(0x42)] public ushort SegSs;
			[FieldOffset(0x44)] public uint EFlags;
			[FieldOffset(0x78)] public ulong Rax;
			[FieldOffset(0x80)] public ulong Rcx;
			[FieldOffset(0x88)] public ulong Rdx;
			[FieldOffset(0x90)] public ulong Rbx;
			[FieldOffset(0x98)] public ulong Rsp;
			[FieldOffset(0xA0)] public ulong Rbp;
			[FieldOffset(0xA8)] public ulong Rsi;
			[FieldOffset(0xB0)] public ulong Rdi;
			[FieldOffset(0xB8)] public ulong R8;
			[FieldOffset(0xC0)] public ulong R9;
			[FieldOffset(0xC8)] public ulong R10;
			[FieldOffset(0xD0)] public ulong R11;
			[FieldOffset(0xD8)] public ulong R12;
			[FieldOffset(0xE0)] public ulong R13;
			[FieldOffset(0xE8)] public ulong R14;
			[FieldOffset(0xF0)] public ulong R15;
			[FieldOffset(0xF8)] public ulong Rip;
		}

		// One entry in the STACKFRAME64 address list.
		[StructLayout(LayoutKind.Sequential)]
		public struct ADDRESS64
		{
			public ulong Offset;
			public ushort Segment;
			public uint Mode;
		}

		// Kernel debugger help block carried inside STACKFRAME64.
		[StructLayout(LayoutKind.Sequential)]
		public struct KDHELP64
		{
			public ulong Thread;
			public uint ThCallbackStack;
			public uint ThCallbackBStore;
			public uint NextCallback;
			public uint FramePointer;
			public ulong KiCallUserMode;
			public ulong KeUserCallbackDispatcher;
			public ulong SystemRangeStart;
			public ulong KiUserExceptionDispatcher;
			public ulong StackBase;
			public ulong StackLimit;
			[MarshalAs(UnmanagedType.ByValArray, SizeConst = 5)]
			public ulong[] Reserved;
		}

		// Stack frame description used by StackWalk64.
		[StructLayout(LayoutKind.Sequential)]
		public struct STACKFRAME64
		{
			public ADDRESS64 AddrPC;
			public ADDRESS64 AddrReturn;
			public ADDRESS64 AddrFrame;
			public ADDRESS64 AddrStack;
			public ADDRESS64 AddrBStore;
			public IntPtr FuncTableEntry;
			[MarshalAs(UnmanagedType.ByValArray, SizeConst = 4)]
			public ulong[] Params;
			public int Far;
			public int Virtual;
			[MarshalAs(UnmanagedType.ByValArray, SizeConst = 3)]
			public ulong[] Reserved;
			public KDHELP64 KdHelp;
		}

		// Screen rectangle returned by GetWindowRect.
		[StructLayout(LayoutKind.Sequential)]
		public struct RECT
		{
			public int Left;
			public int Top;
			public int Right;
			public int Bottom;
		}

		#endregion

		#region Delegates

		// Callback signature for EnumWindows.
		public delegate bool EnumWindowsProc(IntPtr hWnd, IntPtr lParam);

		#endregion

		#region Constants

		// Thread access rights used with OpenThread.
		public const uint THREAD_GET_CONTEXT = 0x0008;
		public const uint THREAD_QUERY_INFORMATION = 0x0040;

		// Context flags.
		public const uint CONTEXT_FULL_X86 = 0x10007;
		public const ulong CONTEXT_FULL_AMD64 = 0x0010000B;

		// StackWalk64 address mode and machine types.
		public const uint ADDR_MODE_FLAT = 3;
		public const uint IMAGE_FILE_MACHINE_I386 = 0x014c;
		public const uint IMAGE_FILE_MACHINE_AMD64 = 0x8664;

		// Stack walk limits and SYMBOL_INFO layout constants.
		public const int MAX_STACK_FRAMES = 40;
		public const int SYMBOL_INFO_FIXED_SIZE = 88;
		public const int SYMBOL_INFO_NAME_OFFSET = 84;
		public const int MAX_SYM_NAME_LEN = 1024;

		// DbgHelp symbol options.
		public const uint SYMOPT_UNDNAME = 0x00000002;
		public const uint SYMOPT_DEFERRED_LOADS = 0x00000004;
		public const uint SYMOPT_LOAD_LINES = 0x00000010;
		public const uint SYMOPT_DEBUG = 0x80000000;

		// Debug event codes from the Windows debug API.
		public const uint EXCEPTION_DEBUG_EVENT = 1;
		public const uint CREATE_THREAD_DEBUG_EVENT = 2;
		public const uint CREATE_PROCESS_DEBUG_EVENT = 3;
		public const uint EXIT_THREAD_DEBUG_EVENT = 4;
		public const uint EXIT_PROCESS_DEBUG_EVENT = 5;
		public const uint LOAD_DLL_DEBUG_EVENT = 6;
		public const uint UNLOAD_DLL_DEBUG_EVENT = 7;
		public const uint OUTPUT_DEBUG_STRING_EVENT = 8;

		// Debug loop flags and continue-status values.
		public const uint DEBUG_ONLY_THIS_PROCESS = 0x00000002;
		public const uint DBG_CONTINUE = 0x00010002;
		public const uint DBG_EXCEPTION_NOT_HANDLED = 0x80010001;

		// Common NTSTATUS exception codes we care about.
		public const uint STATUS_ACCESS_VIOLATION = 0xC0000005;
		public const uint STATUS_GUARD_PAGE_VIOLATION = 0x80000001;
		public const uint STATUS_DATATYPE_MISALIGNMENT = 0x80000002;
		public const uint STATUS_BREAKPOINT = 0x80000003;
		public const uint STATUS_SINGLE_STEP = 0x80000004;

		// Size of the raw buffer we pass to WaitForDebugEvent.
		public const int DEBUG_EVENT_BUFFER_SIZE = 512;

		// Window messages and SendMessageTimeout flags.
		public const uint WM_CLOSE = 0x0010;
		public const uint SMTO_ABORTIFHUNG = 0x0002;
		public const uint SMTO_NORMAL = 0x0000;
		public const uint STILL_ACTIVE = 259;

		// MiniDumpWriteDump flags. Only a subset is used by MiniDumpWriter.
		public const uint MiniDumpNormal = 0x00000000;
		public const uint MiniDumpWithDataSegs = 0x00000001;
		public const uint MiniDumpWithFullMemory = 0x00000002;
		public const uint MiniDumpWithHandleData = 0x00000004;
		public const uint MiniDumpFilterMemory = 0x00000008;
		public const uint MiniDumpScanMemory = 0x00000010;
		public const uint MiniDumpWithUnloadedModules = 0x00000020;
		public const uint MiniDumpWithIndirectlyReferencedMemory = 0x00000040;
		public const uint MiniDumpFilterModulePaths = 0x00000080;
		public const uint MiniDumpWithProcessThreadData = 0x00000100;
		public const uint MiniDumpWithPrivateReadWriteMemory = 0x00000200;
		public const uint MiniDumpWithThreadInfo = 0x00001000;

		// Process information class for NtQueryInformationProcess.
		public const int ProcessWow64Information = 26;

		#endregion

		#region kernel32.dll

		[DllImport("kernel32.dll", SetLastError = true, CharSet = CharSet.Auto)]
		public static extern bool CreateProcess(
			string lpApplicationName, string lpCommandLine,
			IntPtr lpProcessAttributes, IntPtr lpThreadAttributes,
			bool bInheritHandles, uint dwCreationFlags, IntPtr lpEnvironment,
			string lpCurrentDirectory, ref STARTUPINFO lpStartupInfo,
			out PROCESS_INFORMATION lpProcessInformation);

		[DllImport("kernel32.dll", SetLastError = true)]
		public static extern bool WaitForDebugEvent(IntPtr lpDebugEvent, uint dwMilliseconds);
		
		[DllImport("kernel32.dll", SetLastError = true)]
        public static extern bool GetExitCodeProcess(IntPtr hProcess, out uint lpExitCode);

		[DllImport("kernel32.dll", SetLastError = true)]
		public static extern bool ContinueDebugEvent(uint dwProcessId, uint dwThreadId, uint dwContinueStatus);

		[DllImport("kernel32.dll", SetLastError = true)]
		public static extern bool DebugSetProcessKillOnExit(bool KillOnExit);

		[DllImport("kernel32.dll", SetLastError = true)]
		public static extern bool TerminateProcess(IntPtr hProcess, uint uExitCode);

		[DllImport("kernel32.dll", SetLastError = true)]
		public static extern bool CloseHandle(IntPtr hObject);

		[DllImport("kernel32.dll", CharSet = CharSet.Auto, SetLastError = true)]
		public static extern uint GetFinalPathNameByHandle(IntPtr hFile, StringBuilder lpszFilePath, uint cchFilePath, uint dwFlags);

		[DllImport("kernel32.dll", SetLastError = true)]
		public static extern IntPtr VirtualQueryEx(IntPtr hProcess, IntPtr lpAddress, out MEMORY_BASIC_INFORMATION lpBuffer, uint dwLength);

		[DllImport("kernel32.dll", SetLastError = true)]
		public static extern IntPtr OpenThread(uint dwDesiredAccess, bool bInheritHandle, uint dwThreadId);

		[DllImport("kernel32.dll", SetLastError = true)]
		public static extern bool Wow64GetThreadContext(IntPtr hThread, ref WOW64_CONTEXT lpContext);

		[DllImport("kernel32.dll", SetLastError = true)]
		public static extern bool GetThreadContext(IntPtr hThread, ref CONTEXT_AMD64 lpContext);

		[DllImport("kernel32.dll", SetLastError = true)]
		public static extern bool ReadProcessMemory(IntPtr hProcess, IntPtr lpBaseAddress, byte[] lpBuffer, int dwSize, out int lpNumberOfBytesRead);

		[DllImport("kernel32.dll", SetLastError = true)]
		public static extern int GetGuiResources(IntPtr hProcess, uint uiFlags);

		[DllImport("kernel32.dll", SetLastError = true)]
		public static extern IntPtr GetModuleHandle(string lpModuleName);

		[DllImport("kernel32.dll", SetLastError = true, CharSet = CharSet.Auto)]
		public static extern uint GetModuleFileName(IntPtr hModule, StringBuilder lpFilename, uint nSize);
		
		[DllImport("kernel32.dll", SetLastError = true)]
		public static extern IntPtr OpenProcess(uint dwDesiredAccess, bool bInheritHandle, uint dwProcessId);
		
		[DllImport("kernel32.dll", SetLastError = true)]
		public static extern bool IsWow64Process(IntPtr hProcess, out bool Wow64Process);

		
		#endregion

		#region ntdll.dll

		[DllImport("ntdll.dll", SetLastError = true)]
		public static extern int NtQueryInformationProcess(
			IntPtr ProcessHandle,
			int ProcessInformationClass,
			ref IntPtr ProcessInformation,
			uint ProcessInformationLength,
			out uint ReturnLength);

		#endregion

		#region user32.dll

		[DllImport("user32.dll")]
		public static extern bool EnumWindows(EnumWindowsProc lpEnumFunc, IntPtr lParam);

		[DllImport("user32.dll")]
		public static extern uint GetWindowThreadProcessId(IntPtr hWnd, out uint lpdwProcessId);

		[DllImport("user32.dll")]
		public static extern bool PostMessage(IntPtr hWnd, uint Msg, IntPtr wParam, IntPtr lParam);

		[DllImport("user32.dll")]
		public static extern bool IsWindowVisible(IntPtr hWnd);

		[DllImport("user32.dll")]
		public static extern bool IsHungAppWindow(IntPtr hWnd);

		[DllImport("user32.dll")]
		public static extern bool GetWindowRect(IntPtr hWnd, out RECT lpRect);

		[DllImport("user32.dll")]
		public static extern bool PrintWindow(IntPtr hWnd, IntPtr hdcBlt, uint nFlags);

		[DllImport("user32.dll", SetLastError = true, CharSet = CharSet.Auto)]
		public static extern IntPtr SendMessageTimeout(
			IntPtr hWnd, uint Msg, IntPtr wParam, IntPtr lParam,
			uint fuFlags, uint uTimeout, out IntPtr lpdwResult);

		#endregion

		#region dbghelp.dll

		[DllImport("dbghelp.dll", SetLastError = true, CharSet = CharSet.Auto)]
		public static extern bool SymInitialize(IntPtr hProcess, string UserSearchPath, bool fInvadeProcess);

		[DllImport("dbghelp.dll", SetLastError = true)]
		public static extern bool SymCleanup(IntPtr hProcess);

		[DllImport("dbghelp.dll", SetLastError = true)]
		public static extern bool SymRefreshModuleList(IntPtr hProcess);

		[DllImport("dbghelp.dll", SetLastError = true, CharSet = CharSet.Ansi)]
		public static extern bool SymFromAddr(IntPtr hProcess, ulong Address, out ulong Displacement, IntPtr Symbol);

		[DllImport("dbghelp.dll", SetLastError = true, CharSet = CharSet.Ansi)]
		public static extern bool SymGetLineFromAddr64(IntPtr hProcess, ulong dwAddr, out uint pdwDisplacement, IntPtr Line);

		[DllImport("dbghelp.dll", SetLastError = true)]
		public static extern uint SymSetOptions(uint SymOptions);

		[DllImport("dbghelp.dll", SetLastError = true)]
		public static extern bool StackWalk64(
			uint MachineType, IntPtr hProcess, IntPtr hThread,
			ref STACKFRAME64 StackFrame, IntPtr ContextRecord,
			IntPtr ReadMemoryRoutine, IntPtr FunctionTableAccessRoutine,
			IntPtr GetModuleBaseRoutine, IntPtr TranslateAddress);

		[DllImport("dbghelp.dll", SetLastError = true)]
		public static extern bool MiniDumpWriteDump(
			IntPtr hProcess,
			uint ProcessId,
			IntPtr hFile,
			uint DumpType,
			IntPtr ExceptionParam,
			IntPtr UserStreamParam,
			IntPtr CallbackParam);

		#endregion
	}
}