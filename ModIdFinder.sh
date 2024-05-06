#!/bin/bash

find . -type f -name "*.sbmi" >> ./allModDatas.txt

MODIDARR=()

while read path; do
  while read sbmiLine; do
      if [[ $sbmiLine = \<Id\>* ]] ; then
          tmp=${sbmiLine#*>}
          modId=${tmp%<*}
          MODIDARR+=($modId)
      fi
  done < "$path"
done < allModDatas.txt

data_string="${MODIDARR[*]}"
echo "value=[${data_string//${IFS:0:1}/,}]"
