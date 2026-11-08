# NORA 4.0.0 — Windows x64

## English

### New
- **Sources / Results workspace:** conversation attachments and project context live under Sources; generated files and exports are listed under Results.
- **Shared multi-tab browser:** open the browser from the toolbar. User interaction no longer triggers a timed NORA takeover or pauses browser work.
- **Expanded model catalog:** 29 GGUF entries, with CapyRiver 2B and 4B kept at the top. Added Gemma 2 9B, Mistral Nemo 12B, Qwen2.5 14B Instruct and Coder, DeepSeek Coder V2 Lite 16B, Qwen2.5 32B and 72B, DeepSeek R1 Distill 70B, and Mistral Large 123B Q2_K.
- Model files use pinned Hugging Face revisions and SHA-256 validation. The 123B Q2_K file is about 45 GB; 70B–123B choices are intended for high-memory workstations and can be slow.
- New model descriptions are available in Russian, English, and Simplified Chinese.

### Improvements
- Catalog search includes model name, task description, and identifier.
- Updated chat workspace controls and split-browser layout.
- Preserves existing local chats and model data across app replacement.

### Not included
- Speech synthesis is not included in this release. Audio-specific model runtimes are not listed as installable chat models.

## Русский

### Добавлено
- **Рабочая область «Источники / Результаты»:** вложения и контекст проекта находятся в «Источниках», созданные файлы и экспорты — в «Результатах».
- **Общий браузер с несколькими вкладками:** браузер открывается с панели инструментов. Действия пользователя больше не запускают таймер передачи управления NORA и не блокируют работу с браузером.
- **Расширен каталог моделей:** 29 GGUF-вариантов; CapyRiver 2B и 4B остаются сверху. Добавлены Gemma 2 9B, Mistral Nemo 12B, Qwen2.5 14B Instruct и Coder, DeepSeek Coder V2 Lite 16B, Qwen2.5 32B и 72B, DeepSeek R1 Distill 70B и Mistral Large 123B Q2_K.
- Для файлов моделей закреплены ревизии Hugging Face и проверка SHA-256. Файл Mistral Large 123B Q2_K занимает около 45 ГБ; модели 70B–123B рассчитаны на мощные рабочие станции с большим объёмом памяти и могут работать медленно.
- Описания новых моделей доступны на русском, английском и упрощённом китайском.

### Улучшено
- Поиск в каталоге учитывает название, задачу и идентификатор модели.
- Обновлены элементы чата и компоновка браузера рядом с чатом.
- При замене приложения сохраняются локальные чаты и файлы моделей.

### Не входит в эту версию
- Синтез речи не добавлен. Аудиомодели без поддерживаемого движка не выдаются за устанавливаемые модели чата.

## 简体中文

### 新增功能
- **来源 / 结果工作区：**聊天附件和项目上下文显示在“来源”中，生成的文件与导出内容显示在“结果”中。
- **共享多标签浏览器：**从工具栏打开浏览器。用户操作不会再触发 NORA 定时接管，也不会暂停浏览器任务。
- **扩展模型目录：**共 29 个 GGUF 选项，CapyRiver 2B 和 4B 保持置顶。新增 Gemma 2 9B、Mistral Nemo 12B、Qwen2.5 14B Instruct 与 Coder、DeepSeek Coder V2 Lite 16B、Qwen2.5 32B 和 72B、DeepSeek R1 Distill 70B，以及 Mistral Large 123B Q2_K。
- 模型文件固定到 Hugging Face 修订版本，并验证 SHA-256。123B Q2_K 文件约 45 GB；70B–123B 模型面向大内存工作站，运行速度可能较慢。
- 新增模型说明支持俄语、英语和简体中文。

### 改进
- 目录搜索可匹配模型名称、用途和标识符。
- 更新聊天工作区控件和聊天旁的浏览器布局。
- 替换应用时保留本地聊天和模型文件。

### 本版本未包含
- 本版本不包含语音合成。没有兼容运行引擎的音频模型不会被列为可安装聊天模型。
