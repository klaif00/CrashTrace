# CrashTrace

**أداة تشخيص أعطال لبرامج ويندوز.**

CrashTrace بتشغّل البرنامج تحت debugger، وتراقبه من أول لحظة، ولما يحصل أي عطل، بتطلعلك تقرير مفصل بيشرح اللي حصل. الأداة بتشتغل مع ملفات ويندوز 32-bit و 64-bit على حد سواء.

الـ README ده مقسوم لجزئين. لو عايز تستخدم الأداة بس، اقرا الجزء الأول. لو عايز تفهم الكود أو تعدّل عليه، روح للجزء التاني.

---

## الجزء الأول - دليل المستخدم

### الأداة دي بتعمل إيه بالظبط

لو عندك برنامج بيفتح ويقفل لوحده، أو بيقولك "مكتبة ناقصة"، أو بيقع قبل ما واجهته تظهر أصلاً - CrashTrace شبه كاميرا مراقبة موجّهة على البرنامج ده. بتشغّله، وتسجل كل حاجة بتحصل جواه، ولما يقع، بتكتب تقرير كامل.

مش زي أدوات تانية بتقف بس وتراقب، CrashTrace هو debugger حقيقي. بيستخدم نفس الـ debugging API الرسمية اللي ويندوز بيوفرها لأدوات التطوير زي Visual Studio. ده معناه إنه بيشوف حاجات أدوات تانية بتفوتها.

### يعني إيه "تشغيل تحت المراقبة"

انت مش اللي بتشغّل البرنامج. CrashTrace هي اللي بتشغّله، وبتفضل متصلة بيه من أول تعليمة. طول ما البرنامج شغال، الأداة بتراقب:

- كل ملف نظام بيحمّله
- كل thread بيفتحه
- كل exception بتحصل جواه (حتى اللي البرنامج بيعالجها ويكمل عادي)
- هل قفل بشكل طبيعي، ولا وقع، ولا علّق

لو البرنامج قفل، الأداة بالفعل عارفة السبب.

### التقرير فيه إيه

لما الجلسة تخلص، الأداة بتكتب ملفين جنب البرنامج الهدف، الاتنين بتاريخ ووقت:

- **تقرير نصي عادي (`.txt`)** - سهل القراءة، وسهل لصقه في منتدى دعم
- **تقرير Markdown (`.md`)** - منسّق بعناوين وجداول وأكواد، مفيد لو هتشاركه على GitHub

التقرير بيحتوي على:

- **النتيجة**: وقع؟ قفل لوحده؟ علّق؟
- **لو وقع**: كود الحالة (زي `0xC0000005` للـ access violation)، الموديول اللي وقع فيه، الـ offset جواه، وشرح مبسط بالكلام العادي
- **Registers** وقت الكراش
- **الذاكرة حوالي مكان الكراش** - hex dump، بالإضافة لحالة صفحة الذاكرة (كانت free، ولا committed، ولا read-only، ولا executable؟)
- **Mini-disassembly** للتعليمات اللي قبل وبعد الكراش
- **Screenshot** لنافذة الهدف وقت الكراش
- **Minidump (`.dmp`)** تقدر تفتحه بـ WinDbg أو Visual Studio لتحليل أعمق. **ملاحظة:** الملف ده ممكن يكون كبير - غالبًا عشرات أو مئات الميجابايت، حسب حجم العملية.
- **قائمة الـ dependencies الناقصة** لو كان فيه أي منها
- **الـ threads** اللي كانت شغالة وقت الكراش
- **كل الـ DLLs** اللي اتحمّلت خلال الجلسة
- **Related Windows Event Log entries**
- **تقارير Windows Error Reporting (WER)** اللي بتذكر الهدف

### الألعاب والـ anti-cheat

> **تنبيه مهم:** لو الهدف بيستخدم kernel-level anti-cheat (زي EasyAntiCheat، BattlEye، Vanguard، XignCode، GameGuard، وأنظمة مشابهة)، الأداة مش هتشتغل عليه. في بعض الحالات اللعبة هترفض تشتغل، وفي حالات أسوأ حساب اللعبة نفسه ممكن ياخد حظر بسبب استخدام debugger. CrashTrace بتحذرك من ده قبل ما تبدأ.

بالنسبة للألعاب اللي مفيهاش kernel anti-cheat، الأداة بتشتغل عادي. سواء اللعبة 32-bit أو 64-bit.

### الوضعين التانيين

بجانب الوضع الأساسي ("شغّل وراقب")، فيه زرارين بيعملوا تحليل static بحت:

**DLL Scan** - تختار ملف EXE أو DLL، والأداة بتقرا الـ import table بتاعه من غير ما تشغّله أبدًا. بتقولك أنهي مكتبات محتاجها البرنامج، وهل كل واحدة موجودة على القرص. كمان بتقرا .NET assembly references لو الهدف managed assembly.

**Can I Run It?** - نفس الفكرة، بس مركزة على متطلبات النظام. الأداة بتقرا الملف وبتقولك:

- نسخة ويندوز اللي البرنامج اتبنى ليها
- المعمارية (x86 / x64 / ARM)
- أنهي Visual C++ runtimes محتاجها
- أنهي DirectX components بيستخدمها
- هل محتاج صلاحيات admin (من الـ manifest)
- هل موقّع، ولا مضغوط (packed)، ولا فيه sections بـ entropy عالية
- أي strings متعلقة بـ DRM أو anti-debug موجودة في الـ binary

ولا وضع من الاتنين بيشغّل البرنامج ولا بيلمس نظامك. كل حاجة read-only.

### الحاجات اللي مش بتقدر تعملها

بصراحة عن القيود:

- مش بتشخّص stutter، ولا FPS واطي، ولا مشاكل performance. هي مش profiler.
- مش بتشخّص **سبب** انقطاع الصوت أو الشاشة السودا. هي بتصوّر نافذة الهدف وقت الكراش، فلو الشاشة كانت سودا في اللحظة دي بالتحديد، هتشوف ده - بس الأداة مش هتقوللك ليه.
- مش بتصلح أي حاجة. بتشخّص بس.
- مش بتلتقط الأخطاء اللي البرنامج بيعالجها بنفسه ويكمّل من بعدها، إلا لو الأخطاء دي في الآخر أدت لكراش. الـ first-chance exceptions بتتسجل، بس مش بتتحلل كسبب للكراش لوحدها.
- مش بتشخّص مشاكل الشبكة (disconnect، lag).
- مش بتعمل attach لعمليات شغالة بالفعل. بتشغّل عمليات جديدة بس.
- مش بتشتغل مع kernel-level anti-cheat أو DRM.

### إزاي تستخدمها

1. كليك يمين على الملف ونفّذه كـ **Run as Administrator**. الأداة محتاجة debug privileges واللي في العادة مش متاحة غير لحساب administrator.
2. اضغط **Select** واختار البرنامج اللي عايز تختبره.
3. اضغط **Start**.
4. استنى لما المشكلة تحصل. ده ممكن ياخد ثواني أو دقايق حسب البرنامج.
5. لو البرنامج وقع أو قفل، التقرير هيظهر جنب البرنامج الهدف. فيه كمان زرار **Copy Analysis** بتحط التقرير كله على الـ clipboard.

لو عايز توقف الجلسة بدري، اضغط **Stop**. الأداة هتحاول تقفل البرنامج بشكل طبيعي الأول، وبعدين هتجبره يقفل بعد كام ثانية.

### استخدام AI لقراءة التقرير

التقرير فيه تفاصيل تقنية كثيرة. مش لازم تفهم كلها. أسهل طريقة:

1. افتح ملف `.md` (هو أنسب نسخة للقراءة).
2. انسخ محتواه كله.
3. الصقه في AI assistant زي ChatGPT أو Claude أو Gemini.
4. اسأله حاجة زي: *"اقرأ تقرير الكراش ده واشرحلي بالبساطة إيه اللي حصل وإزاي أصلحه."*

التقرير مصمّم إنه يبقى مكتفي بذاته. الـ AI عنده كل اللي محتاجه يديك إجابة مفيدة من غير ما يسألك أسئلة تانية.
تحذير!!! الاداة ممكن تتشاف كا فيرس او تروجن لانها بتحقن نفسها في البرنامج او اللعبه عشن تشتغل كا debugger فا متقلقش  وكمان لو لقيت الاداة وقفت او مش شغاله لزمن تقفل الانتي فايرس وتشتخرج الاداة من اول وجديد وتشغله
---

## الجزء التاني - للمبرمجين

### البنية المعمارية

هي debugger حقيقي، مش monitor سلبي. بتستقبل كل حدث ويندوز بيبعته للـ debugger: DLL load و unload، thread creation و exit، exceptions (سواء first-chance أو second-chance)، و debug string output.

الملف التنفيذي الأساسي مبني كـ **x64**. الـ `RootNamespace` اسمه `TestApp` (اسم قديم)، لكن الـ assembly الناتج اسمه `CrashTrace.exe`.

### بتقرا إيه من العملية الهدف

- **CPU registers** - `EAX..EDI` + `ESP/EBP/EIP` للـ 32-bit، و `RAX..R15` + `RSP/RBP/RIP` للـ 64-bit
- **Stack dump** - 4 كيلوبايت بداية من `ESP`/`RSP`
- **Memory region info** - عبر `VirtualQueryEx`، بتلتقط حالة الصفحة وخصائص الحماية والنوع
- **Instruction bytes** عند عنوان الكراش، عشان الـ mini-disassembler
- **PEB** - للـ 32-bit targets، عشان تمشي على الـ environment block
- **IAT** - مش حاليًا، بس `ApiHookDetector` بيفحص أول بايتات من مجموعة ثابتة من الدوال المصدّرة

### فك التعليمة (Disassembly)

فيه decoderين:

- **BasicDisassembler** - decoder مكتوب بالإيد لمجموعة فرعية من x86/x64 opcodes. مفيش اعتماد خارجي. مش بيغطي كل الـ instruction set، بس بيغطي اللي بيظهر في مواقع الكراش غالبًا.
- **DisassemblyHelper** - بيغلّف SharpDisasm (نسخة C# من udis86). بيقرا 96 بايت قبل عنوان الكراش و 128 بايت بعده، بيجرب إزاحات بداية مختلفة لحد ما الـ disassembly يوصل بالظبط لعنوان الكراش، وبعدين بيفك 8 تعليمات قبل العطل + التعليمة اللي وقعت نفسها + 8 تعليمات بعدها.

لو SharpDisasm فشل لأي سبب، الأداة بترجع لـ BasicDisassembler. التقرير بيوضح أنهي engine هو اللي أنتج الناتج.

### قراية .NET exceptions

للأهداف الـ managed (.NET)، CrashTrace بتقرا الـ exception object من الـ thread اللي وقع باستخدام ClrMD (`Microsoft.Diagnostics.Runtime`). ده بيدي نوع الـ exception، الرسالة، و managed stack trace.

فيه قيد هنا يستاهل الفهم: ClrMD بيحمّل DLL native اسمه **DAC** (Data Access Component). الـ DAC لازم يطابق معمارية العملية الهدف بالظبط. لو الـ host 64-bit والهدف 32-bit، التحميل داخل العملية بيفشل.

عشان نتحايل على ده، الأداة بتشحن helper executables اتنين:

- `TestAppHelper32.exe` - مبني كـ x86، للأهداف 32-bit
- `TestAppHelper64.exe` - مبني كـ x64، للأهداف 64-bit

العملية الرئيسية (دايمًا x64) بتحاول تقرا الـ exception داخل العملية الأول. لو فشلت - مثلًا لأن الهدف 32-bit - بتشغّل الـ helper المطابق، وتمررله الـ PID والـ crashing thread ID ومسار ملف مؤقت. الـ helper بيقرا الـ exception ويكتب النتيجة كـ key-value text file. العملية الرئيسية بتقرا الملف ده وتملأ حقول التقرير.

### تصنيف الموديولات

فيه قائمتين ثابتتين في `ModuleClassifier`:

- **SuspiciousModules** - overlays و hooks معروفة إنها بتتعارض مع الألعاب (Discord overlay، MSI Afterburner، RivaTuner، Xbox Game Bar، OBS hooks، NVIDIA capture، إلخ)
- **AntiCheatModules** - EasyAntiCheat، BattlEye، Vanguard، XignCode، GameGuard، PunkBuster، وغيرهم. دول معلوماتيين بس - وجودهم مش مشكلة في حد ذاته، بس بيفسر بعض الـ crashes.

فيه كمان فحص منفصل لـ Windows compatibility shims (`aclayers.dll`، `acgenral.dll`، `acspecfc.dll`) اللي بتظهر في قائمة الموديولات المحمّلة لما AppCompat بيكون نشط.

### فحص الـ PE

خمس كلاسات بتعمل تحليل PE ثابت من غير تشغيل الملف:

- **`PeImportReader`** - بتقرا الـ Import Directory. بتتعامل مع PE32 و PE32+ في الاتنين.
- **`PeHeaderReader`** - بتقرا `Machine` و `Subsystem` و `DllCharacteristics` و `EntryPointRva`، الـ section table، وبتكشف الـ managed assemblies عن طريق الـ CLR runtime header directory.
- **`EntropyAnalyzer`** - بتحسب Shannon entropy لكل section. الـ entropy فوق 7.5 عادة معناه إن الـ section مضغوطة (packed) أو مشفّرة.
- **`StringExtractor`** - بتستخرج ASCII strings من الـ binary. هي صارمة عن قصد: بترفض السلاسل المتكررة، وبتطلب نسبة حروف عالية مقارنة بالطول الكلي، وبتفرض على الأقل كلمة من 3 حروف متتالية. ده بيخلي الناتج مركّز.
- **`DllScanner`** - بتربط كل ما سبق مع بعض. لما تدّيها ملف، بترجع المعمارية، وهل هو .NET، والـ direct imports، والـ .NET assembly references، وهل كل dependency موجود على القرص.

### تحليل الـ dependencies

مسارين وقت تشغيل الهدف:

1. **Direct imports**: كل DLL في الـ import table بتاع الهدف المفروض تظهر في خريطة الـ loaded modules. أي حاجة مش بتحمّل بتتقارن بالقرص. لو موجودة على القرص بس مش بتحمّل، فده عادةً delayed load أو optional dependency. لو مش موجودة على القرص خالص، بتتسجل كـ real missing dependency.
2. **Indirect dependencies**: كل DLL حمّل من نفس فولدر الهدف بيتقرا هو كمان الـ import table بتاعه، وأي entries ناقصة فيه بتتسجل. ده بيلتقط حالات إن الهدف بيحمّل DLL خاص باللعبة، والـ DLL ده نفسه محتاج runtime ناقص.

التقرير بيفرّق بين confirmed missing DLLs، و indirect ones، واللي موجودة على القرص بس مش بتحمّل.

### Crash minidump

لما يمسك fatal crash، قبل ما يتقفل الـ process handle، الأداة بتكتب minidump بـ `MiniDumpWriteDump` بالـ flags دي:

CrashTrace هي Windows Forms application مكتوبة بـ C# ومستهدفة .NET Framework 4.5. هي user-mode debugger مبنية على الـ debugging API الرسمية من ويندوز:

ملف الـ `.dmp` ينفع يتفتح بـ WinDbg أو Visual Studio. حجمه بيوصل غالبًا لعشرات أو مئات الميجابايت. ده طبيعي.

### كشف الـ Hang

Thread مخصّص بيتفحص النافذة الرئيسية للهدف مرة كل ثانية. بيستخدم فحصين:

- `IsHungAppWindow` (المسار السريع)
- `SendMessageTimeout` بـ `WM_NULL` و `SMTO_ABORTIFHUNG`، بـ timeout 700 ميلي ثانية (الـ fallback)

لو النافذة بطلت تستجيب، بيتسجل hang. لو الـ hang استمر أكتر من 90 ثانية، الـ watchdog بيجبر الهدف يقفل عشان التقرير يطلع برضه.

### الـ Watchdog

Thread منفصل بيراقب الـ debug loop نفسه. بيتفحص:

- هل الهدف لسه حي (`GetExitCodeProcess`)
- هل الـ debug loop استقبل أي أحداث مؤخرًا (`lastActivityTick`)

لو مفيش نشاط لمدة 60 ثانية، أو الهدف قفل من غير ما الـ loop تلاحظ، الـ watchdog بيجبر الـ loop تخرج. ده بيمنع الأداة نفسها إنها تعلّق للأبد لو حصل deadlock.

### التقارير

صيغتين بيتكتبوا مرة واحدة في آخر الجلسة بـ `File.WriteAllText`:

- **`.txt`** - نص عادي. منظّم بفواصل `====`. سهل إنك تعمله grep، سهل لصقه في منتدى.
- **`.md`** - Markdown. نفس المحتوى، بس بعناوين وجداول و code fences. أفضل للمشاركة على GitHub أو Discord.

وقت الجلسة، سطور الـ log بتتجمّع في الذاكرة (`allLogLines`). التقرير بيتكتب مرة واحدة في الآخر، مش بشكل تدريجي.

بخصوص الـ symbols: CrashTrace بتدعم قراية الـ symbols عبر `dbghelp.dll`، بس ده **offline فقط بحكم التصميم**. مش بتتصل بـ Microsoft symbol server أبدًا. بتدور بس على ملفات PDB الموجودة محليًا (في `C:\Symbols`، `%LOCALAPPDATA%\TestAppSymbols`، `%PROGRAMDATA%\Microsoft\Windows\Debug\Symbols`، أو أي مسار محلي في `_NT_SYMBOL_PATH`). لو مفيش PDB محلي، التقرير بيعرض module+offset بدل أسماء الدوال. ده لسه مفيد - الـ module+offset ثابت بين التشغيلات.

### القيود

- هي مش profiler. مش بتساعد في stutter أو FPS واطي أو frame-time spikes.
- هي مش RenderDoc. مش بتلتقط frames أو graphics API calls.
- مش بتلتقط الأخطاء اللي البرنامج بيعالجها بنفسه ويكمّل منها، إلا لو الأخطاء دي في الآخر بتسبب كراش. الـ first-chance exceptions بتتسجل، بس مش بتتحلل كسبب للكراش لوحدها.
- قراية الـ symbols offline فقط. مفيش اتصال بـ Microsoft symbol server، ولا اتصال بالإنترنت بأي شكل.
- مش بتشتغل مع kernel-level anti-cheat أو DRM.
- بتشغّل عمليات جديدة بس. مش بتعمل attach لعمليات شغالة بالفعل.

---

## خلاصة

CrashTrace هي user-mode debugger صغيرة مركّزة على مهمة واحدة: تعرف ليه برنامج ويندوز توقف عن الشغل. بتلتقط لحظة الكراش بتفصيل، بتحلل الـ dependencies والـ PE structure، بتقرا .NET exceptions، وبتطلع تقرير يقدر يفهمه إنسان و AI على حد سواء.

الأداة offline بحكم التصميم. مش بترسل أي بيانات لأي مكان.

---

## الترخيص

MIT License. شوف ملف `LICENSE` للتفاصيل.
