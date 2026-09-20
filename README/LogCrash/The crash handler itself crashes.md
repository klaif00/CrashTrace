==================================================
 Automated Crash / Exit Diagnostic Report
==================================================
Target program : C:\Users\****\Desktop\New folder (2)\aatest.exe
Started at     : 2026-09-20 16:02:03
Finished at    : 2026-09-20 16:02:08
Architecture   : x86 (32-bit)
Subsystem      : Windows GUI 6.0
Entry point    : 0x0000766E
Sections       : 3
DLL chars      : HIGH_ENTROPY_VA, DYNAMIC_BASE (ASLR), NX_COMPAT (DEP), NO_SEH, TERMINAL_SERVER_AWARE
Managed (.NET) : Yes
SHA256         : 5CAF45D5D1686B7B8D1467E574F4FB75242A6E52B4416FE870F38B593C692C59
Signature      : Not digitally signed (or signature could not be verified)

==================================================
 PROBLEM
==================================================
Result        : CRASH (unhandled exception)
Status code   : 0xE0434352
Meaning       : .NET CLR Exception - unhandled exception in managed code
Managed exception : System.Exception
Managed message   : Primary exception that triggers the (broken) handler above.
Managed stack trace:
  Test40_CrashInsideCrashHandler
  <BuildButtons>b__27
  <AddButton>b__4f
  OnClick
  OnClick
  OnMouseUp
  WmMouseUp
  WndProc
  WndProc
  WndProc
  OnMessage
  WndProc
  DebuggableCallback
  DispatchMessageW
  FPushMessageLoop
  RunMessageLoopInner
  RunMessageLoop
  Run
  Main
Address       : 0x760AB5B2
Module        : C:\Windows\SysWOW64\KernelBase.dll
Offset        : 0x12B5B2
Time to crash : 4 second(s) after start

Faulting code : MOV ECX, [ESP+disp8]
Explanation   : Load a value from memory or register into a register. A crash here usually means the source address was invalid.

Hang          : The target window became unresponsive.
First hang at : 16:02:07
Hang count    : 1

==================================================
 MISSING DEPENDENCIES
==================================================
Not yet loaded, but the file DOES exist on disk (probably NOT the cause):
  - mscoree.dll


==================================================
 TECHNICAL DETAILS
==================================================
CPU registers at crash:
  EAX = 0x0093E978
  EBX = 0x00000005
  ECX = 0x00000005
  EDX = 0x00000000
  ESI = 0x0093EA38
  EDI = 0x00000001
  ESP = 0x0093E978
  EBP = 0x0093E9D0
  EIP = 0x760AB5B2
  EFLAGS = 0x00200216

Mini disassembly around crash site:
  (engine: SharpDisasm (udis86))
  0x760AB59A    push ecx
  0x760AB59B    lea eax, [esp+0x1c]
  0x760AB59F    push eax
  0x760AB5A0    call 0x760b4f04
  0x760AB5A5    add esp, 0xc
  0x760AB5A8    lea eax, [esp]
  0x760AB5AB    push eax
  0x760AB5AC    call dword [0x7615d3fc]
  0x760AB5B2    mov ecx, [esp+0x54]      <-- FAULTING INSTRUCTION
  0x760AB5B6    xor ecx, esp
  0x760AB5B8    call 0x760b03e0
  0x760AB5BD    mov esp, ebp
  0x760AB5BF    pop ebp
  0x760AB5C0    ret 0x10
  0x760AB5C3    and dword [esp+0x10], 0x0
  0x760AB5C8    jmp 0x760ab5a8
  0x760AB5CA    push 0xf


Memory region at crash site:
  Base        : 0x760AB000
  Size        : 0xAE000
  State       : MEM_COMMIT
  Protection  : PAGE_EXECUTE_READ
  Type        : MEM_IMAGE
  Note        : This is a memory-mapped IMAGE (code or data from a loaded executable/DLL).

Memory around crash site:
00000000760AB592  89 44 24 10 C1 E0 02 50  51 8D 44 24 1C 50 E8 5F  |.D$....PQ.D$.P._|
00000000760AB5A2  99 00 00 83 C4 0C 8D 04  24 50 FF 15 FC D3 15 76  |........$P.....v|
00000000760AB5B2  8B 4C 24 54 33 CC E8 23  4E 00 00 8B E5 5D C2 10  |.L$T3..#N....]..|
00000000760AB5C2  00 83 64 24 10 00 EB DE  6A 0F 58 EB C3 CC CC CC  |..d$....j.X.....|
00000000760AB5D2  CC CC CC CC CC CC CC CC  CC CC CC CC CC CC 8B FF  |................|
00000000760AB5E2  55 8B EC 8B 55 0C 8B 4D  08 6A 00 E8 57 8E FE FF  |U...U..M.j..W...|


Stack memory (top of stack):
  Region base : 0x93E000
  Region size : 0x2000
000000000093E978  52 43 43 E0 01 00 00 00  00 00 00 00 B2 B5 0A 76  |RCC............v|
000000000093E988  05 00 00 00 00 15 13 80  00 00 00 00 00 00 00 00  |................|
000000000093E998  00 00 00 00 00 00 26 75  00 03 9E 00 8C EA 93 00  |......&u........|
000000000093E9A8  01 00 00 00 72 79 BF 84  70 8E 9F 00 70 EA 93 00  |....ry..p...p...|
000000000093E9B8  1C 00 00 00 78 8E 9F 00  00 03 9E 00 98 E9 93 00  |....x...........|
000000000093E9C8  00 03 9E 00 1F 6D 6E 36  6C EA 93 00 A1 C6 3E 75  |.....mn6l.....>u|
000000000093E9D8  52 43 43 E0 01 00 00 00  05 00 00 00 38 EA 93 00  |RCC.........8...|
000000000093E9E8  74 C3 86 6C 00 00 00 00  4C 50 A0 02 01 00 00 00  |t..l....LP......|
000000000093E9F8  00 03 9E 00 01 00 00 00  52 43 43 E0 3C B0 A3 00  |........RCC.<...|
000000000093EA08  00 00 00 00 78 8E 9F 00  77 00 00 00 78 8E 9F 00  |....x...w...x...|
000000000093EA18  18 50 9F 00 88 E4 93 00  2C EA 93 00 52 43 43 E0  |.P......,...RCC.|
000000000093EA28  52 43 43 E0 00 00 00 00  4C 50 A0 02 00 00 00 00  |RCC.....LP......|
000000000093EA38  00 15 13 80 00 00 00 00  00 00 00 00 00 00 00 00  |................|
000000000093EA48  00 00 26 75 00 03 9E 00  8C 04 9E 00 E8 E9 93 00  |..&u............|
000000000093EA58  88 E4 93 00 28 EB 93 00  60 A7 3E 75 D0 EF 2B 19  |....(...`.>u..+.|
000000000093EA68  00 00 00 00 34 EB 93 00  46 D5 3E 75 00 00 00 00  |....4...F.>u....|
000000000093EA78  2C C2 86 6C FC B5 98 02  60 B4 98 02 38 37 A0 02  |,..l....`...87..|
000000000093EA88  46 03 BE 6D 84 0B 27 75  00 EC 93 00 00 00 00 00  |F..m..'u........|
000000000093EA98  01 00 00 00 00 03 9E 00  00 00 00 00 7C EA 93 00  |............|...|
000000000093EAA8  FC B5 98 02 80 EA 93 00  60 B4 98 02 84 EA 93 00  |........`.......|
000000000093EAB8  38 37 A0 02 34 EB 93 00  44 EB 93 00 3C EB 93 00  |87..4...D...<...|
000000000093EAC8  38 EB 93 00 34 EB 93 00  74 EA 93 00 6C D4 3E 75  |8...4...t...l.>u|
000000000093EAD8  00 00 00 00 9D 35 33 75  DB 10 00 00 30 EB 93 00  |.....53u....0...|
000000000093EAE8  60 B4 98 02 00 03 9E 00  C0 40 5D 02 2C 50 A0 02  |`........@].,P..|
000000000093EAF8  80 EA 93 00 EE FC 26 75  74 50 A0 02 6C 50 A0 02  |......&utP..lP..|
000000000093EB08  4C 50 A0 02 33 98 42 75  00 13 91 02 00 03 9E 00  |LP..3.Bu........|
000000000093EB18  00 00 00 00 C4 25 1B 73  4C 50 A0 02 78 EA 93 00  |.....%.sLP..x...|
000000000093EB28  FC EC 93 00 57 D5 3E 75  01 00 00 00 44 EB 93 00  |....W.>u....D...|
000000000093EB38  4E 3E 74 02 4C 50 A0 02  34 13 91 02 64 EB 93 00  |N>t.LP..4...d...|
000000000093EB48  46 3D 74 02 2B 3D 74 02  0B B7 6A 67 38 37 A0 02  |F=t.+=t...jg87..|
000000000093EB58  5C 01 4A 00 60 B4 98 02  38 37 A0 02 74 EB 93 00  |\.J.`...87..t...|
000000000093EB68  A7 DF 6A 67 60 B4 98 02  38 37 A0 02 90 EB 93 00  |..jg`...87......|


Call stack (top = where it happened):
  #0  KernelBase.dll+0x12B5B2
  #1  clr.dll+0x18C6A1
  #2  clr.dll+0x18D546
  #3  0x2743E4E
  #4  0x2743D46
  #5  System.Windows.Forms.ni.dll+0x1BDFA7
  #6  System.Windows.Forms.ni.dll+0x8E8EAB
  #7  System.Windows.Forms.ni.dll+0x8A5FCA
  #8  System.Windows.Forms.ni.dll+0xC4D135
  #9  System.Windows.Forms.ni.dll+0xC69B07
  #10  System.Windows.Forms.ni.dll+0x222B12
  #11  System.Windows.Forms.ni.dll+0x1C5143
  #12  System.Windows.Forms.ni.dll+0x1C50D5
  #13  System.Windows.Forms.ni.dll+0x8B43AB
  #14  0x25DD0B9
  #15  user32.dll+0x4348B
  #16  user32.dll+0x3A42A
  #17  user32.dll+0x3819A
  #18  user32.dll+0x37F60
  #19  System.Windows.Forms.ni.dll+0x22882D
  #20  System.Windows.Forms.ni.dll+0x1D563F
  #21  System.Windows.Forms.ni.dll+0x1D522D
  #22  System.Windows.Forms.ni.dll+0x1D5083
  #23  System.Windows.Forms.ni.dll+0x1AD841
  #24  0x274087E
  #25  clr.dll+0x10576
  #26  clr.dll+0x1379A
  #27  clr.dll+0x19B3B
  #28  clr.dll+0x16ECEB
  #29  clr.dll+0x16F3CA
  #30  clr.dll+0x16F2F7
  #31  clr.dll+0x16F478
  #32  clr.dll+0x16F59E
  #33  clr.dll+0x16AFA5
  #34  mscoreei.dll+0xFA84
  #35  mscoree.dll+0xE81E
  #36  mscoree.dll+0x14338
  #37  ntdll.dll+0x67A9E
  #38  ntdll.dll+0x67A6E

Note on symbols:
  Function names could not be resolved, so only module names and offsets
  are shown. This does not affect the accuracy of the analysis - the
  offset (for example "game.exe+0x1A4F2") is stable across runs and
  machines, unlike a raw memory address.

  Why symbols might be unavailable:
    - the symbol engine failed to start.
    - Symbol resolution in this tool is fully offline by design: it never
      connects to the internet, and it does not use Microsoft's online
      symbol server. It only looks for symbol files (PDB) that already
      exist locally on this computer.
    - Bundling all Windows symbol files with the tool is not practical:
      the full set for a single Windows build alone can exceed several
      gigabytes, would noticeably slow the tool down at startup, and would
      still not match a different Windows build running on another machine.
    - Symbols for the target program itself (its own EXE) are also not
      bundled. Only the program's developer can provide them, and they
      must be placed next to the target EXE to be used.

  The rest of this report is still fully valid - it just shows addresses
  and offsets instead of human-readable function names.

Performance at crash:
  Working set  : 27 MB
  Handles      : 344
  Free RAM     : 11008 MB
  Free disk    : 10 GB

==================================================
 ENVIRONMENT
==================================================
Section entropy:
  .text      size=0x00005800 entropy=5.418 - Normal - typical code or data
  (unnamed)  size=0x00000010 entropy=0.000 - Low - typically padding, zero-filled, or highly repetitive data

Threads at crash:
  Total threads: 6
  TID 2772     state=Wait         wait=Executive          prio=Normal       cpu=687ms
  TID 7756     state=Wait         wait=Executive          prio=Normal       cpu=0ms
  TID 2240     state=Wait         wait=Executive          prio=Normal       cpu=0ms
  TID 3792     state=Wait         wait=Executive          prio=Normal       cpu=0ms
  TID 12376    state=Wait         wait=Executive          prio=Highest      cpu=15ms
  TID 5716     state=Wait         wait=Executive          prio=Normal       cpu=15ms

Compatibility : Windows applied compatibility shims to this program.
Application manifest:
Source: embedded resource
<?xml version="1.0" encoding="UTF-8" standalone="yes"?>
<assembly xmlns="urn:schemas-microsoft-com:asm.v1" manifestVersion="1.0">
  <assemblyIdentity version="1.0.0.0" name="MyApplication.app"/>
  <trustInfo xmlns="urn:schemas-microsoft-com:asm.v2">
    <security>
      <requestedPrivileges xmlns="urn:schemas-microsoft-com:asm.v3">
        <requestedExecutionLevel level="asInvoker" uiAccess="false"/>
      </requestedPrivileges>
    </security>
  </trustInfo>
</assembly>

Notable strings in the executable:
  !This program cannot be run in DOS mode.
  lSystem.Resources.ResourceReader, mscorlib, Version=4.0.0.0, Culture=neutral, PublicKeyToken=b77a5c561934e089#System.Resources.RuntimeResourceSet
  aatest.exe
  System.Windows.Forms
  System.ComponentModel
  Test01_NullReference
  Test02_IndexOutOfRange
  Test03_ArgumentOutOfRange
  Test04_DivideByZero
  Test05_InvalidCast
  Test06_FormatException
  Test07_KeyNotFound
  Test08_ManagedStackOverflow
  Test09_OutOfMemory
  Test10_StaticConstructorThrows
  Test11_BackgroundThreadCrash
  Test12_AsyncEventHandlerCrash
  Test13_AccessViolationRead
  Test14_AccessViolationWrite
  Test15_CallThroughGarbagePointer
  Test16_NativeStackOverflow
  Test17_HeapBufferOverflow
  Test18_DoubleFree
  Test19_UseAfterFree
  Test20_InvalidHandleReuse
  Test21_GuardPageViolation
  Test22_IllegalInstruction
  Test23_DepViolation
  Test24_NativeSehUnhandled
  Test25_RaiseFailFast
  Test26_CallingConventionStackCorruption
  Test27_StackBufferSmash
  Test28_ClassicDeadlock
  Test29_HangOnUnsignaledEvent
  Test30_UiThreadBusyLoop
  Test31_UiThreadLongSleep
  Test32_Livelock
  Test33_TimerCallbackCrash
  Test34_ManagedFailFast
  Test35_ForceTerminateProcess
  Test36_HandleExhaustion
  Test37_BadHResult
  Test38_CleanExitControl
  Test39_ExceptionDuringUnwind
  Test40_CrashInsideCrashHandler
  MEM_COMMIT
  MEM_RESERVE
  PAGE_READWRITE
  PAGE_EXECUTE_READWRITE
  PAGE_GUARD
  GENERIC_READ
  OPEN_EXISTING
  FILE_ATTRIBUTE_NORMAL
  RaiseFailFastException
  MessageBeep_WrongConvention
  System.Runtime.InteropServices
  System.Runtime.Versioning
  System.Reflection
  System.Security.Permissions
  System.Runtime.CompilerServices


==================================================
 WHAT YOU SHOULD TRY
==================================================
  1. The crash was reported inside a core Windows module. This is usually misleading: the actual problem is almost always an invalid argument or a bad pointer passed in from the program itself. Windows did not cause the crash, it just reported it.
  2. Look at the call stack above. The first frame inside the program's own code (not a Windows DLL) is where the real fault occurred.
  3. Windows has applied compatibility shims to this program, meaning Microsoft already knows it has issues on this Windows version. You may want to try changing the compatibility mode of the EXE manually (Properties -> Compatibility).
  4. The program became unresponsive at least once during monitoring. If it eventually recovered, the cause is likely a slow disk, a blocked network call, or a background operation. If it never recovered, this may indicate an infinite loop or a deadlock inside the program.

==================================================
 LAST EVENTS LEADING UP TO THIS RESULT
==================================================
  [16:02:04] [DLL] Loaded: C:\Windows\SysWOW64\CoreUIComponents.dll
  [16:02:04] [DLL] Loaded: C:\Windows\SysWOW64\CoreMessaging.dll
  [16:02:04] [DLL] Loaded: C:\Windows\SysWOW64\ws2_32.dll
  [16:02:04] [DLL] Loaded: C:\Windows\SysWOW64\WinTypes.dll
  [16:02:04] [DLL] Loaded: C:\Windows\SysWOW64\WinTypes.dll
  [16:02:04] [DLL] Module unloaded: C:\Windows\SysWOW64\WinTypes.dll
  [16:02:04] [DLL] Loaded: C:\Windows\SysWOW64\ntmarta.dll
  [16:02:06] [Exception - Non-fatal] Code: 0xE0434352 (.NET CLR Exception - unhandled exception in managed code) at address 0x760AB5B2 - the program will likely handle this itself.
  [16:02:06] [Exception - Non-fatal] Code: 0xE0434352 (.NET CLR Exception - unhandled exception in managed code) at address 0x760AB5B2 - the program will likely handle this itself.
  [16:02:06] [DLL] Loaded: C:\Windows\Microsoft.NET\Framework\v4.0.30319\diasymreader.dll
  [16:02:07] [Managed] Exception: System.Exception - Primary exception that triggers the (broken) handler above.
  [16:02:07] [!!! FATAL CRASH !!!] Code: 0xE0434352 (.NET CLR Exception - unhandled exception in managed code) at address 0x760AB5B2 in module: C:\Windows\SysWOW64\KernelBase.dll - Thread 2772
  [16:02:07] [Call Stack] 39 frame(s) captured - see the final report for details.
  [16:02:07] [Hang] Target window became unresponsive at 16:02:07
  [16:02:08] [Info] Monitoring stopped.

==================================================
 LIVE EVENT LOG
==================================================
[16:02:03] [Info] Target architecture: x86 (32-bit), Subsystem: Windows GUI 6.0
[16:02:03] [Info] Sections: 3, Entry point: 0x0000766E
[16:02:03] [Info] DLL characteristics: HIGH_ENTROPY_VA, DYNAMIC_BASE (ASLR), NX_COMPAT (DEP), NO_SEH, TERMINAL_SERVER_AWARE
[16:02:03] [Info] Managed (.NET): Yes, TLS: No, Relocations: Yes
[16:02:03] [Info] This is a managed .NET assembly - many native-crash diagnostics do not apply.
[16:02:03] [Warning] Symbol engine (dbghelp.dll) failed to start (error -2147483635). Function names will be limited; Module+Offset will still work.
[16:02:03] [Info] Monitoring started for: C:\Users\****\Desktop\New folder (2)\aatest.exe (PID 12032)
[16:02:03] [Process] Target process created successfully.
[16:02:03] [DLL] Loaded: C:\Windows\System32\ntdll.dll
[16:02:03] [DLL] Loaded: C:\Windows\SysWOW64\ntdll.dll
[16:02:03] [DLL] Loaded: C:\Windows\System32\wow64.dll
[16:02:03] [DLL] Loaded: C:\Windows\System32\wow64win.dll
[16:02:03] [WOW64] Subsystem event at 0x7FFF351406B0 (64-bit layer, not part of the 32-bit program)
[16:02:03] [DLL] Loaded: C:\Windows\System32\wow64cpu.dll
[16:02:03] [DLL] Loaded: C:\Windows\SysWOW64\mscoree.dll
[16:02:03] [DLL] Loaded: C:\Windows\SysWOW64\kernel32.dll
[16:02:03] [DLL] Loaded: C:\Windows\SysWOW64\KernelBase.dll
[16:02:03] [DLL] Loaded: C:\Windows\SysWOW64\apphelp.dll
[16:02:03] [DLL] Loaded: C:\Windows\SysWOW64\AcGenral.dll
[16:02:03] [DLL] Loaded: C:\Windows\SysWOW64\msvcrt.dll
[16:02:03] [DLL] Loaded: C:\Windows\SysWOW64\sechost.dll
[16:02:03] [DLL] Loaded: C:\Windows\SysWOW64\rpcrt4.dll
[16:02:03] [DLL] Loaded: C:\Windows\SysWOW64\shlwapi.dll
[16:02:03] [DLL] Loaded: C:\Windows\SysWOW64\user32.dll
[16:02:03] [DLL] Loaded: C:\Windows\SysWOW64\win32u.dll
[16:02:03] [DLL] Loaded: C:\Windows\SysWOW64\gdi32.dll
[16:02:03] [DLL] Loaded: C:\Windows\SysWOW64\gdi32full.dll
[16:02:03] [DLL] Loaded: C:\Windows\SysWOW64\msvcp_win.dll
[16:02:03] [DLL] Loaded: C:\Windows\SysWOW64\ucrtbase.dll
[16:02:03] [DLL] Loaded: C:\Windows\SysWOW64\ole32.dll
[16:02:03] [DLL] Loaded: C:\Windows\SysWOW64\combase.dll
[16:02:03] [DLL] Loaded: C:\Windows\SysWOW64\oleaut32.dll
[16:02:03] [DLL] Loaded: C:\Windows\SysWOW64\shell32.dll
[16:02:03] [DLL] Loaded: C:\Windows\SysWOW64\advapi32.dll
[16:02:03] [DLL] Loaded: C:\Windows\SysWOW64\uxtheme.dll
[16:02:03] [DLL] Loaded: C:\Windows\SysWOW64\winmm.dll
[16:02:03] [DLL] Loaded: C:\Windows\SysWOW64\samcli.dll
[16:02:03] [DLL] Loaded: C:\Windows\SysWOW64\msacm32.dll
[16:02:03] [DLL] Loaded: C:\Windows\SysWOW64\version.dll
[16:02:03] [DLL] Loaded: C:\Windows\SysWOW64\userenv.dll
[16:02:03] [DLL] Loaded: C:\Windows\SysWOW64\dwmapi.dll
[16:02:03] [DLL] Loaded: C:\Windows\SysWOW64\urlmon.dll
[16:02:03] [DLL] Loaded: C:\Windows\SysWOW64\winspool.drv
[16:02:03] [DLL] Loaded: C:\Windows\SysWOW64\mpr.dll
[16:02:03] [DLL] Loaded: C:\Windows\SysWOW64\sspicli.dll
[16:02:03] [DLL] Loaded: C:\Windows\SysWOW64\winmmbase.dll
[16:02:03] [DLL] Loaded: C:\Windows\SysWOW64\iertutil.dll
[16:02:03] [DLL] Loaded: C:\Windows\SysWOW64\SHCore.dll
[16:02:03] [DLL] Loaded: C:\Windows\SysWOW64\srvcli.dll
[16:02:03] [DLL] Loaded: C:\Windows\SysWOW64\netutils.dll
[16:02:03] [DLL] Loaded: C:\Windows\SysWOW64\imm32.dll
[16:02:04] [Exception - Non-fatal] Code: 0x4000001F (First-chance notification on WOW64 startup (routine, not an error)) at address 0x77DC1BA2 - the program will likely handle this itself.
[16:02:04] [DLL] Loaded: C:\Windows\Microsoft.NET\Framework\v4.0.30319\mscoreei.dll
[16:02:04] [DLL] Loaded: C:\Windows\SysWOW64\kernel.appcore.dll
[16:02:04] [DLL] Loaded: C:\Windows\Microsoft.NET\Framework\v4.0.30319\clr.dll
[16:02:04] [DLL] Loaded: C:\Windows\SysWOW64\vcruntime140_clr0400.dll
[16:02:04] [DLL] Loaded: C:\Windows\SysWOW64\ucrtbase_clr0400.dll
[16:02:04] [Thread] New thread created (TID 7756)
[16:02:04] [Exception - Non-fatal] Code: 0x04242420 (.NET debugger notification (routine)) at address 0x760AB5B2 - the program will likely handle this itself.
[16:02:04] [DLL] Loaded: C:\Windows\SysWOW64\psapi.dll
[16:02:04] [DLL] Module unloaded: C:\Windows\SysWOW64\psapi.dll
[16:02:04] [DLL] Loaded: C:\Windows\assembly\NativeImages_v4.0.30319_32\mscorlib\ef4992f14ba62dc115c01e7a6018e263\mscorlib.ni.dll
[16:02:04] [DLL] Loaded: C:\Windows\SysWOW64\bcryptprimitives.dll
[16:02:04] [Thread] New thread created (TID 12376)
[16:02:04] [Thread] New thread created (TID 3792)
[16:02:04] [Thread] New thread created (TID 2240)
[16:02:04] [DLL] Loaded: C:\Windows\Microsoft.NET\Framework\v4.0.30319\clrjit.dll
[16:02:04] [DLL] Loaded: C:\Windows\assembly\NativeImages_v4.0.30319_32\System\dfbf0633415b3a3aa4269dadfbf59dd7\System.ni.dll
[16:02:04] [DLL] Loaded: C:\Windows\assembly\NativeImages_v4.0.30319_32\System.Drawing\c849c8b772883a7d22a281e720854cf7\System.Drawing.ni.dll
[16:02:04] [DLL] Loaded: C:\Windows\assembly\NativeImages_v4.0.30319_32\System.Windows.Forms\dfc9997f7c0c1e092ab93d14314ddfd3\System.Windows.Forms.ni.dll
[16:02:04] [DLL] Loaded: C:\Windows\assembly\NativeImages_v4.0.30319_32\System.Core\125e7f523f68e1f804b59ce7662cee20\System.Core.ni.dll
[16:02:04] [DLL] Loaded: C:\Windows\assembly\NativeImages_v4.0.30319_32\System.Configuration\e0fbeb6785aa6fa336bfcd8e9ce437c2\System.Configuration.ni.dll
[16:02:04] [DLL] Loaded: C:\Windows\assembly\NativeImages_v4.0.30319_32\System.Xml\09693b06a195c27853dbfbd12b550387\System.Xml.ni.dll
[16:02:04] [DLL] Loaded: C:\Windows\SysWOW64\windows.storage.dll
[16:02:04] [DLL] Loaded: C:\Windows\SysWOW64\wldp.dll
[16:02:04] [DLL] Loaded: C:\Windows\SysWOW64\profapi.dll
[16:02:04] [DLL] Loaded: C:\Windows\SysWOW64\bcrypt.dll
[16:02:04] [DLL] Loaded: C:\Windows\SysWOW64\cryptsp.dll
[16:02:04] [DLL] Loaded: C:\Windows\SysWOW64\rsaenh.dll
[16:02:04] [DLL] Loaded: C:\Windows\SysWOW64\cryptbase.dll
[16:02:04] [DLL] Loaded: C:\Windows\WinSxS\x86_microsoft.windows.common-controls_6595b64144ccf1df_5.82.19041.1110_none_c0da534e38c01f4d\comctl32.dll
[16:02:04] [DLL] Loaded: C:\Windows\WinSxS\x86_microsoft.windows.gdiplus_6595b64144ccf1df_1.1.19041.1288_none_d9539a9fe102720c\GdiPlus.dll
[16:02:04] [DLL] Loaded: C:\Windows\SysWOW64\DWrite.dll
[16:02:04] [Thread] New thread created (TID 5716)
[16:02:04] [DLL] Loaded: C:\Windows\SysWOW64\msctf.dll
[16:02:04] [DLL] Loaded: C:\Windows\WinSxS\x86_microsoft.windows.common-controls_6595b64144ccf1df_6.0.19041.1110_none_a8625c1886757984\comctl32.dll
[16:02:04] [DLL] Loaded: C:\Windows\SysWOW64\TextShaping.dll
[16:02:04] [DLL] Loaded: C:\Windows\SysWOW64\TextInputFramework.dll
[16:02:04] [DLL] Loaded: C:\Windows\SysWOW64\CoreUIComponents.dll
[16:02:04] [DLL] Loaded: C:\Windows\SysWOW64\CoreMessaging.dll
[16:02:04] [DLL] Loaded: C:\Windows\SysWOW64\ws2_32.dll
[16:02:04] [DLL] Loaded: C:\Windows\SysWOW64\WinTypes.dll
[16:02:04] [DLL] Loaded: C:\Windows\SysWOW64\WinTypes.dll
[16:02:04] [DLL] Module unloaded: C:\Windows\SysWOW64\WinTypes.dll
[16:02:04] [DLL] Loaded: C:\Windows\SysWOW64\ntmarta.dll
[16:02:06] [Exception - Non-fatal] Code: 0xE0434352 (.NET CLR Exception - unhandled exception in managed code) at address 0x760AB5B2 - the program will likely handle this itself.
[16:02:06] [Exception - Non-fatal] Code: 0xE0434352 (.NET CLR Exception - unhandled exception in managed code) at address 0x760AB5B2 - the program will likely handle this itself.
[16:02:06] [DLL] Loaded: C:\Windows\Microsoft.NET\Framework\v4.0.30319\diasymreader.dll
[16:02:07] [Managed] Exception: System.Exception - Primary exception that triggers the (broken) handler above.
[16:02:07] [!!! FATAL CRASH !!!] Code: 0xE0434352 (.NET CLR Exception - unhandled exception in managed code) at address 0x760AB5B2 in module: C:\Windows\SysWOW64\KernelBase.dll - Thread 2772
[16:02:07] [Call Stack] 39 frame(s) captured - see the final report for details.
[16:02:07] [Hang] Target window became unresponsive at 16:02:07
[16:02:08] [Info] Monitoring stopped.

==================================================
 End of report
==================================================
