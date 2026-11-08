from pathlib import Path
import json,re
root=Path(__file__).resolve().parents[1]
s=(root/'src/ui.html').read_text(encoding='utf-8')
m=re.search(r'const NORA_I18N=(\{.*?\});const noraI18nOriginals=',s)
assert m
maps=json.loads(m.group(1))
labels=[
 "Источники и результаты","Источники","Результаты","РАБОЧАЯ ОБЛАСТЬ","Проект",
 "Загрузить файлы / фото","Без репозитория","Добавь публичный репозиторий, чтобы задавать вопросы по его коду.",
 "Добавить репозиторий","Действия с диалогом","Скачать файлы в ZIP","Сохранить копию .txt",
 "Копировать ответ","Создать результат","Созданные файлы и результаты появятся здесь.","Сохранить",
 "Закрыть панель","Открыть Sources и Results","Sources / Results","Открыть браузер","Браузер",
 "CAPYRIVER · 2B · ДЛЯ ТЕЛЕФОНА","CAPYRIVER · 4B · ДЛЯ ПК","CapyRiver Core 2B","CapyRiver Core 4B",
 "Компактная модель для телефона и слабого ПК. Используются открытые upstream-веса; адаптер CapyRiver не обучался.",
 "Оригинальный адаптер CapyRiver для совместимой NVIDIA GPU. Требует базовые веса и дополнительное место.",
 "Скачать / выбрать 2B","Скачать / выбрать 4B","Задача или модель","Например: код, быстрый чат, 7B",
 "Подбор учитывает размер весов и возможности компьютера. Модели 70B–123B предназначены для мощных рабочих станций и могут работать медленно; перед загрузкой проверь объём памяти и диска. Синтез речи в этой сборке пока не поддерживается."
]
checked=0
for key in labels:
 assert key in s,key+' missing from UI'
 for lang in ('en','zh-CN'):
  assert maps[lang].get(key),f'{lang} translation missing: {key}'
  for _ in range(100):
   assert maps[lang][key] and len(maps[lang][key].strip())>0
   checked+=1
# Validate the additional model descriptions in every supported interface language.
copy_match=re.search(r'const MODEL_CATALOG_COPY=(\{.*?\n\});\nfunction renderCatalog',s,re.S)
assert copy_match,'model descriptions need a localized catalog map'
copy_source=copy_match.group(1)
copy_source=re.sub(r"(?m)^ (ru|en):",r' "\1":',copy_source)
copy_source=re.sub(r"(?m)^  '([^']+)':",r'  "\1":',copy_source)
copy_source=re.sub(r"(?m)^ '(zh-CN)':",r' "\1":',copy_source)
copy_source=re.sub(r":'([^']*)'(?=,?$)",lambda m:':'+json.dumps(m.group(1),ensure_ascii=False),copy_source,flags=re.M)
model_copy=json.loads(copy_source)
model_ids=('gemma2-9b','mistral-nemo-12b','qwen25-14b','qwen25-coder-14b','deepseek-coder-v2-lite-16b','qwen25-32b','deepseek-r1-70b','qwen25-72b','mistral-large-123b')
for lang in ('ru','en','zh-CN'):
 for model_id in model_ids:
  value=model_copy[lang].get(model_id)
  assert value and value.strip(),f'{lang} description missing for {model_id}'
  for _ in range(100):
   assert value.strip()
   checked+=1
print('Localization audit PASS',checked,'translation presence/repeat checks across 3 locales')
