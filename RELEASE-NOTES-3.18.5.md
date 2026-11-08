# NORA 3.18.5

## English

- Removed the separate native startup/loading screen. NORA opens directly into its bundled interface.
- Fixed the WebView2 local-page navigation gate and moved the UI to a fresh browser profile to avoid stale cached content.
- Completed the English translation table, aligned English and Simplified Chinese coverage, corrected mistranslated model terms, and added a 100-pass localization integrity audit.
- Startup errors are logged and shown in the selected interface language. Chats and model files are preserved.

## Русский

- Удалён отдельный системный экран запуска и загрузки. NORA сразу открывает встроенный интерфейс.
- Исправлена проверка адреса локальной страницы WebView2; для интерфейса создан отдельный чистый профиль браузера без старого кэша.
- Заполнены английские переводы, выровнено покрытие английского и упрощённого китайского, исправлены технические термины; добавлена 100-кратная проверка целостности переводов.
- Ошибки запуска записываются в журнал и показываются на выбранном языке интерфейса. Чаты и файлы моделей сохраняются.

## 简体中文

- 移除了单独的原生启动/加载页面。NORA 现在会直接打开内置界面。
- 修复了 WebView2 本地页面的导航校验，并为界面启用独立的新浏览器配置，避免旧缓存导致加载失败。
- 补齐英文翻译、统一英文与简体中文的词条覆盖，并修正模型相关术语；新增 100 轮本地化完整性检查。
- 启动错误会写入日志，并按所选界面语言显示。聊天记录和模型文件均会保留。
