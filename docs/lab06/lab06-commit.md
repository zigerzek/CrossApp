# Коміт лабораторної 6 — Git Bash

Переглянь зміни й виконай команди з кореня проєкту. Коміт і push автоматично не виконувалися.

```bash
cd /c/dev/CrossApp
git status --short
git diff --cached --name-only

git add -- CrossApp.slnx README.md \
  src/Core/Events src/Core/Abstractions/INotificationSink.cs \
  src/Core/Services/CatalogService.cs src/Core/Import/GoodsCsvImporter.cs \
  src/Cli/Program.cs src/Cli/Cli.csproj src/Cli/Notifications \
  data/lab06.csv tests/EventChecks \
  docs/lab06-defense.md docs/lab06-report.md docs/lab06-commit.md

git diff --cached --stat
git diff --cached --check
git commit -m "lab06: domain events, subscribers, notification sinks and import callbacks"
git log -1 --oneline
```

Якщо перша перевірка staged-файлів уже показує інші файли, переглянь їх перед комітом: git commit включає всі staged-зміни. Наведений git add вибирає тільки файли лабораторної 6. Зміни попередніх звітів і `tests/DomainChecks/Program.cs` не включені. Журнали app.log/sink.log та bin/obj ігноруються Git.
