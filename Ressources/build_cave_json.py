# Wiki ARK Survival Evolved
# Script de construction d'un json de grottes
import os
import io
import json
from colorama import init as colorama_init, Fore, Back, Style

input = "J:/ARK/Ressources/Valguero/Grottes.txt"
json_output = "Cartes/Grottes_Valguero_1.json"

#Grotte de la jungle;Artéfact de l'Immunité:
#- Terrestre; 	54.2 	62.7
#"cave-entrance": [
#    {
#        "lat": 54.2,
#        "lon": 62.7,
#        "name": "Grotte de la jungle",
#        "description": "Entrée Terrestre<br/>Contient:\n* {{ItemLink|Artéfact de l'Immunité}}",
#        "image": ""
#    }
#]
# Attention si plusieurs atréfacts !
#description": "Contient:\n* {{ItemLink|Artéfact du Seigneur du Ciel}}\n* {{ItemLink|Artéfact du Sournois}}\n* {{ItemLink|Artéfact du Colosse}}\n* {{ItemLink|Artéfact de la Sagesse}}",

cave_entrance_tab = []
colorama_init()
wd = os.path.dirname(__file__)
state = 0
with io.open(os.path.join(wd,input), mode="r", encoding="utf-8") as file_in:
    lines = file_in.readlines()
    artifacts = None
    for line in lines:
        line = line.rstrip()
        if len(line) == 0:
           state = 0
           continue
        if state == 0:
            split = [s.strip() for s in line.split(';')]
            name = split[0]
            if len(split) == 2:
                artifacts = [s.strip() for s in split[1].split(',')]
            else:
                artifacts = None
            state = 1
        elif state == 1:
            split = [s.strip() for s in line.split(';')]
            cave_entrance = {}
            cave_entrance['name'] = name
            cave_entrance['lat'] = float(split[1])
            cave_entrance['lon'] = float(split[2])
            description = ""
            if len(split[0]) != 0:
                description = f"Entrée {split[0]}<br/>"
            if artifacts != None:
                description = description + "Contient:"
                # attention pb avec les ItemLink si plus de la dans la description
                for (n, artifact) in enumerate(artifacts):
                    if n < 2:
                        description = description + f"\n* {{{{ItemLink|{artifact}}}}}"
                    else:
                        description = description + f"\n* {artifact}"
            cave_entrance['description'] = description
            cave_entrance['image'] = ""
            cave_entrance_tab.append(cave_entrance)

markers = {}
markers["cave-entrance"] = cave_entrance_tab

# format output json
json_out = {}
json_out["$schema"] = "https://ark.wiki.gg/extensions/DataMaps/schemas/v0.16.json"
json_out["$mixin"] = True
json_out["markers"] = markers
with io.open(os.path.join(wd,json_output), mode="w", encoding="utf-8") as json_file:
    json.dump(json_out, json_file, ensure_ascii=False, indent="  ")
