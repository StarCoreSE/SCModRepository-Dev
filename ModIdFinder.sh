#!/bin/bash

find . -type f -name "*.sbmi" >> ./allModDatas.txt

MODIDARR=()

while read path; do
  while read sbmiLine; do
      if [[ $sbmiLine = \<Id\>* ]] ; then
          tmp=${sbmiLine#*>}
          modId=${tmp%<*}
          modPath=${path%/*}
          MODIDARR+="{\"value\":$modId,\"path\":\"$modPath\"}"
      fi
  done < "$path"
done < allModDatas.txt

delim=""
joined=""
for item in "${MODIDARR[@]}"; do
  joined="$joined$delim$item"
  delim=","
done
echo "$joined"
