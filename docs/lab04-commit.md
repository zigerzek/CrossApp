# Коміт лабораторної 4

Відкрий PowerShell у C:\dev\CrossApp. Спочатку перевір:

```powershell
dotnet build CrossApp.slnx
dotnet run --project src/Cli -f net10.0 --no-build
dotnet run --project tests/DomainChecks -f net10.0 --no-build
git status --short
git diff --check
git diff --cached --name-only
```

Остання команда показує вже підготовлені зміни. Якщо там є сторонні файли, прибери саме їх з індексу через `git restore --staged -- шлях/до/файлу`: вміст робочого файлу ця команда не видаляє.

Додай лише файли лабораторної 4:

```powershell
git add -- CrossApp.slnx README.md src/Cli/Program.cs src/Core/Domain src/Core/Dto/BatchDto.cs src/Core/Dto/ImportResult.cs src/Core/Import/GoodsCsvImporter.cs src/Core/Import/GoodsDomainMapper.cs data/lab04.csv tests/DomainChecks docs/lab04-commit.md reports/Lab04_CrossApp.docx
git diff --cached --stat
git diff --cached --check
git diff --cached -- src/Core/Domain src/Core/Import src/Core/Dto src/Cli/Program.cs
git commit -m "lab04: warehouse domain invariants, batch lifecycle and import mapping"
git log -1 --oneline
```

До початку цієї роботи вже були змінені data/sample.csv та data/sample.json і створений data/clean.csv. Наведений git add не додає їх. Не використовуй `git add .`, якщо хочеш коміт лише четвертої лабораторної. Новий коміт містить різницю, а попередні лабораторні залишаються в історії Git.

Для завантаження вже створеного коміту на налаштований віддалений репозиторій:

```powershell
git push
```

Якщо Git повідомить, що гілка не має upstream, перевір її назву та remote командами `git branch --show-current` і `git remote -v`, а тоді виконай `git push -u origin НАЗВА_ТВОЄЇ_ГІЛКИ` з фактичною назвою. Сам коміт і push під час підготовки лабораторної не виконувалися.
