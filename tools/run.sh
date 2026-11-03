#!/bin/bash
# usage: run.sh <modEnabled true|false> <tag> [R5|R10|R13|CHn]
G="/c/Program Files (x86)/Steam/steamapps/common/Cantata"
C="$G/BepInEx/config/local.cantata.aimod.cfg"
SCEN=""; SAVE=""
if [[ "${3:-}" == CH* ]]; then SCEN="$3"; else
SAVE=$(ls /c/Users/john/AppData/LocalLow/afterschool/cantata/saves/campaign/*${3:-R13}_*.csave | head -1 | sed "s#/c/#C:/#"); fi
python - "$C" "$1" "$2" "$SAVE" "$SCEN" <<'PY'
import sys,re
c,mod,tag,save,scen=sys.argv[1:]
t=open(c).read()
def setv(sec,key,val):
    global t
    if not re.search(r'^'+key+' = ',t,flags=re.M):
        t=t.replace('['+sec+']','['+sec+']\n'+key+' = ',1)
    t=re.sub(r'(\['+sec+r'\][^\[]*?\n'+key+r' = )[^\n]*',lambda m:m.group(1)+val,t,flags=re.S)
setv('General','Enabled',mod); setv('Harness','Enabled','true'); setv('Harness','SavePath',save); setv('Harness','Tag',tag); setv('Harness','Scenario',scen); setv('Harness','MaxRounds','60')
open(c,'w').write(t)
PY
powershell -NoProfile -Command "Start-Process -WindowStyle Minimized -WorkingDirectory 'C:\Program Files (x86)\Steam\steamapps\common\Cantata' 'C:\Program Files (x86)\Steam\steamapps\common\Cantata\cantata.exe'"
