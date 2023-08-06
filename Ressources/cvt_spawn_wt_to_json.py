# Wiki ARK Survival Evolved
# Script de conversion d'un fichier de données d'apparition du joueur au format WikiText vers un fichier json
import os
import io
import json
from colorama import init as colorama_init, Fore, Back, Style
from unidecode import unidecode

wikitext_input = "J:\ARK\Ressources\Crystal_Isles\Apparition.wt"
json_output = "Cartes/ApparitionJoueur_Crystal_Isles.json"

#{{#vardefine:color-sanctuary|#0fe}}
#{{#vardefine:color-Hauts plateauxn|#00de4a}}
#-->{{#vardefine:color-smallislands|#f00}}<!--
#|29.4, 9.1,, {{#var:color-sunkenforest}},Forêt engloutie
#|51.4, 64.7,, {{#var:color-sanctuary}},Sanctuaire

# sans #var
#|72.8, 19.0,, White, Arctique

#"groups": {
#    "playerSpawns-sanctuary": {
#        "name": "Lieux d'apparition: Sanctuaire",
#        "fillColor": "#0fe", "borderColor": "#fff", "size": 5.5, "icon": "Simple Bed.png"
#    },
#
#"markers": {
#    "playerSpawns-sanctuary": [
#        { "lat": 51.4, "lon": 64.7 },

# process input
colors = {"Red": "#ff0000", "White":"#ffffff", "Green": "#00ff00", "Yellow": "#ffff00", "Blue": "#0000ff"}
groups = {}
markers = {}
wd = os.path.dirname(__file__)
state = 0 # recherche de 'ResourceMap'
with io.open(wikitext_input, mode="r", encoding="utf-8") as file_in:
    lines = file_in.readlines()
    for line in lines:
        line = line.rstrip()
        if state == 0:
            if 'MapLocations' in line:
                state = 1
            elif "#vardefine:color-" in line:
                # extraction variable 
                # {{#vardefine:color-sanctuary|#0fe}} => color-sanctuary = #0fe
                #                    ^        ^    ^
                #-->{{#vardefine:color-smallislands|#f00}}<!--
                #                      ^           ^    ^
                pos1 = line.index('color-') + 5
                pos2 = line.index('|', pos1)
                pos3 = line.index('}', pos2)
                group_name = f"playerSpawns-{line[pos1+1:pos2]}".replace(' ', '-')
                color = line[pos2+1:pos3]
                groups[group_name] = {"fillColor": color, "borderColor": "#000", "size": 5.5, "icon": "Simple Bed.png"}
        elif state == 1 and len(line) > 3 and line[0] == '|':
            # marker or command ?
            if '=' not in line:
                #|29.4, 9.1,, {{#var:color-sunkenforest}},Forêt engloutie
                #| 44.2, 39.3, , {{#var:color-foulcreek}}, Ruisseau nauséabond
                #             ^!!
                if ', ,' in line:
                    line = line.replace(', ,', ',,')
                split = [s.strip() for s in list(filter(None, line[1:].split(',')))]
                marker = { "lat": float(split[0]), "lon": float(split[1]) }
                # split[2] = {{#var:color-sunkenforest}}
                #                        ^            ^
                pos1 = split[2].find('-')
                if pos1 > 0:
                    pos2 = split[2].index('}', pos1)
                    group_name = f"playerSpawns-{split[2][pos1+1:pos2]}".replace(' ', '-')
                else:
                    str = split[3].replace(' ','-').lower()
                    group_name = f"playerSpawns-{unidecode(str)}"
                if group_name not in groups:
                    color = split[2]
                    if '#' not in color:
                        color = colors[color]
                    groups[group_name] = {"fillColor": color, "borderColor": "#000", "size": 5.5, "icon": "Simple Bed.png"}
                elif "name" not in groups[group_name]:
                    groups[group_name]["name"] = f"Lieux d'apparition: {split[3]}"
                if group_name not in markers:
                    markers[group_name] = []
                markers[group_name].append(marker)
        elif line == "}}":
            state = 2

# format output json
json_out = {}
json_out["$schema"] = "https://ark.wiki.gg/extensions/DataMaps/schemas/v0.16.json"
json_out["$mixin"] = True
json_out["groups"] = groups
json_out["markers"] = markers
with io.open(os.path.join(wd,json_output), mode="w", encoding="utf-8") as json_file:
    json.dump(json_out, json_file, ensure_ascii=False, indent="  ")
