#!/usr/bin/env bash
# Одноразове прибирання структури репозиторію (Git Bash). Нічого не комітить.
#   bash scripts/tidy-structure.sh
# Що робить:
#   1. видаляє порожню службову папку B/ (перевірка орфографії Windows);
#   2. розкладає документи лаб. 4–9 по docs/labNN/ і звіти .docx/.pdf — у docs/reports/;
#   3. забирає дублікати (output/ і docs/Zvit_*.docx — однакові файли);
#   4. додає у git data/catalog.json і docs/ (раніше були в .gitignore, а Cli.csproj копіює catalog.json).
set -euo pipefail
cd "$(dirname "${BASH_SOURCE[0]}")/.."

# git mv для відстежуваних файлів, звичайний mv — для невідстежуваних
move() { mkdir -p "$(dirname "$2")"; git mv "$1" "$2" 2>/dev/null || mv "$1" "$2"; }

echo "→ службова папка B/"
rm -rf B

echo "→ docs/labNN/"
for n in 04 05 06 07 08 09; do
  for f in docs/lab"$n"-*.md; do
    [ -e "$f" ] && move "$f" "docs/lab$n/$(basename "$f")"
  done
done
[ -e LAB08_DEFENSE_GUIDE.md ] && move LAB08_DEFENSE_GUIDE.md docs/lab08/lab08-defense-full.md

echo "→ звіти (.docx/.pdf/.drawio) → docs/reports/"
for n in 1 7 8; do
  src_dir="output/lab$n"
  [ -d "$src_dir" ] || continue
  for f in "$src_dir"/*; do
    move "$f" "docs/reports/lab0$n/$(basename "$f")"
  done
done
# однакові копії у docs/ більше не потрібні
for n in 7 8; do
  dup="docs/Zvit_Laboratorna_$n.docx"; keep="docs/reports/lab0$n/Zvit_Laboratorna_$n.docx"
  if [ -e "$dup" ] && [ -e "$keep" ] && cmp -s "$dup" "$keep"; then
    git rm -q -f --ignore-unmatch "$dup" 2>/dev/null || true
    rm -f "$dup"
  fi
done
rmdir output/lab1 output/lab7 output/lab8 output 2>/dev/null || true

echo "→ git add: data/catalog.json і docs/"
git add data/catalog.json docs 2>/dev/null || true

echo
echo "Готово. Перевірте результат:  git status --short"
