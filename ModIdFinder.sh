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
for item in "${MODIDARR[@]}"; do
  printf "%s" "$delim$item"
  delim=","
done
