#!/usr/bin/env bash
# Повний сценарій перевірки лабораторної роботи 9 (Git Bash / Linux / macOS).
# Запуск із будь-якого каталогу:  bash scripts/lab09-demo.sh
set -uo pipefail
cd "$(dirname "${BASH_SOURCE[0]}")/.."
ROOT="$PWD"
CLI="dotnet run --no-build --project $ROOT/src/Cli --"

step() { printf '\n\033[1;36m== %s\033[0m\n' "$*"; }

# Тимчасовий каталог у «змішаному» форматі (C:/…), щоб Git Bash не перетворював шлях по-своєму.
TMP="$(mktemp -d)"
command -v cygpath >/dev/null 2>&1 && TMP="$(cygpath -m "$TMP")"
trap 'rm -rf "$TMP"' EXIT

step "1. Збірка і тести"
dotnet build CrossApp.slnx -v q || exit 1
dotnet test tests/Core.Tests -v q || exit 1

step "2. Активна конфігурація (профіль Development)"
$CLI config

step "3. Запуск із кореня solution і з src/Cli — той самий файл даних"
$CLI config | grep "Файл даних"
(cd "$ROOT/src/Cli" && dotnet run --no-build -- config | grep "Файл даних")

step "4. Експорт колекції у файл даних (до перевизначення)"
$CLI export | head -n 8

step "5. CROSSAPP_DATA_PATH перекриває шлях із файлу"
CROSSAPP_DATA_PATH="$TMP/override" $CLI config | grep "Файл даних"
CROSSAPP_DATA_PATH="$TMP/override" $CLI export | head -n 6
ls -la "$TMP/override"

step "6. Вкладений ключ у змінній середовища: CROSSAPP_Storage__FileName"
CROSSAPP_Storage__FileName=custom.json $CLI config | grep "Файл даних"

step "7. Аргумент командного рядка: --Storage:DataPath=…"
$CLI config --Storage:DataPath="$TMP/from-cli" | grep "Файл даних"

step "8. Профіль Production (дані в LocalApplicationData, лог Warning)"
CROSSAPP_ENVIRONMENT=Production $CLI config | grep -E "Профіль|MinimumLevel|Location|Файл даних"

step "9. Secret: user-secrets і змінна середовища"
dotnet user-secrets set "Integration:ApiKey" "local-dev-key" --project src/Cli
dotnet user-secrets list --project src/Cli
$CLI config | grep ApiKey
CROSSAPP_Integration__ApiKey=from-env $CLI config | grep ApiKey

step "10. Імпорт (async), скасування, пошкоджений і відсутній файли"
CROSSAPP_DATA_PATH="$TMP/import" $CLI import
CROSSAPP_DATA_PATH="$TMP/import" $CLI import                 # другий раз: 0 нових (ідемпотентність)
CROSSAPP_DATA_PATH="$TMP/import" $CLI export | grep Експортовано   # експорт → імпорт власного файлу
CROSSAPP_DATA_PATH="$TMP/import" $CLI import | grep Імпортовано
CROSSAPP_DATA_PATH="$TMP/import" $CLI import --path data/catalog.json --cancel
printf '[ { "id": ' > "$TMP/broken.json"
CROSSAPP_DATA_PATH="$TMP/import" $CLI import --path "$TMP/broken.json"      # зіпсований JSON
$CLI --file --path data/bad_sample.json                                       # лаб. 8: DomainException «кількість не може бути 0»
CROSSAPP_DATA_PATH="$TMP/import" $CLI import --path missing.json

step "11. Додавання товару (одиниця за замовчуванням із Warehouse:DefaultUnit)"
CROSSAPP_DATA_PATH="$TMP/import" $CLI add SKU-500 "Гвинт" 25
CROSSAPP_DATA_PATH="$TMP/import" $CLI list | tail -n 3

step "12. Зіпсований JSON у файлі даних — повідомлення без stack trace"
mkdir -p "$TMP/bad" && printf '[ { "id": ' > "$TMP/bad/items.json"
CROSSAPP_DATA_PATH="$TMP/bad" $CLI list

step "13. Лаб. 8 (збережено): імпорт у пам'ять, потік, прогрес, таймаут"
$CLI --file
$CLI --file --stream
$CLI --file --timeout 1ms

step "14. Лаб. 5–7 (збережено): звіт"
$CLI report-top | head -n 8

step "15. Git: секрет не в репозиторії, немає абсолютних шляхів"
git status --short
echo "git grep по значенню секрету в src:"; git grep -n "local-dev-key" -- src || echo "  (порожньо)"
echo "git grep по абсолютних шляхах у src:"; git grep -nE 'C:\\|/home/' -- src || echo "  (порожньо)"

printf '\n\033[1;32mГотово.\033[0m\n'
