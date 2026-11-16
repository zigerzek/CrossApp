# Коміт лабораторної роботи 5 — Git Bash

Виконувати з кореня проєкту після перегляду змін. Автоматично коміт не створювався.

```bash
cd /c/dev/CrossApp
git status --short
git diff --cached --name-only

git add -- .gitignore CrossApp.slnx README.md \
  src/Core/Abstractions src/Core/Storage src/Core/Services src/Core/SampleData.cs \
  src/Cli/Program.cs src/Cli/Composition \
  tests/ServiceChecks \
  docs/lab05-defense.md docs/lab05-report.md docs/lab05-commit.md

git diff --cached --stat
git diff --cached --check
git commit -m "lab05: service layer, memory and file stores, manual DI and extras"
git log -1 --oneline
```

Якщо `git diff --cached --name-only` до додавання вже показує інші файли, спочатку перегляньте їх: коміт включає всі staged-зміни. Наведений git add додає тільки файли цієї роботи. Зміни у `tests/DomainChecks/Program.cs`, старих звітах `output/lab1/` і папці `reports/` існували до лабораторної 5 та не включені до цієї команди.

Створений програмою `data/catalog.json` містить локальні демонстраційні дані й ігнорується Git. Початкові 18 товарів задані в SampleData.cs. Файл біля виконуваної програми також ігнорується, оскільки розташований у bin/.

Надсилання вже створеного коміту на поточну віддалену гілку:

```bash
git push
```
