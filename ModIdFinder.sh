#!/bin/bash

find . -type f -name "*.sbmi" >> ./allModDatas.txt

MODIDARR=()

while read path; do
  while read sbmiLine; do
      if [[ $sbmiLine = \<Id\>* ]] ; then
          tmp=${sbmiLine#*>}
          modId=${tmp%<*}
          modPathTmp=${path%/*}
          modPath=${modPathTmp// /\`}
		  
		  for editedFile in "$1"
		  do
			echo "Checking $editedFile"
			if [[ $modPath == *$editedFile* ]] ; then
				MODIDARR+=(\{\"value\":$modId,\"path\":\"$modPath\"\})
				break
			fi
		  done
      fi
  done < "$path"
done < allModDatas.txt

delim=""
joined=""
for item in "${MODIDARR[@]}"; do
  joined="${joined}${delim}${item//\`/ }"
  delim=","
done
echo "matrix={\"include\":[$joined]}]"
