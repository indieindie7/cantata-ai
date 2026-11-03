#!/bin/bash
# usage: batch.sh "true:mod1 false:stock2 ..."
for spec in $1; do
  m=${spec%%:*}; t=${spec##*:}
  taskkill //IM cantata.exe //F >/dev/null 2>&1; sleep 3
  bash /c/re/cantata/mod/run.sh $m $t ${t##*_} >/dev/null 2>&1
  for i in $(seq 1 300); do sleep 5; tasklist | grep -q cantata.exe || break; grep -q "|$t|" "/c/Program Files (x86)/Steam/steamapps/common/Cantata/BepInEx/harness_results.txt" && break; done
  sleep 3; taskkill //IM cantata.exe //F >/dev/null 2>&1
done
echo done > /c/re/cantata/mod/batch.done
