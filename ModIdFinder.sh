#!/bin/bash

find . -type f -name "*.sbmi" >> ./allModDatas.txt

MODIDARR=()

while read path; do
  echo Reading $path
  while read sbmiLine; do
      if [[ $sbmiLine = \<Id\>* ]] ; then
          tmp=${sbmiLine#*>}
          modId=${tmp%<*}
          MODIDARR+=($modId)
      fi
  done < "$path"
done < allModDatas.txt

data_string="${MODIDARR[*]}"
echo "[${data_string//${IFS:0:1}/,}]"
