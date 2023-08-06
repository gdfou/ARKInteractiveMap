# Wiki ARK Survival Evolved
# Script de conversion d'un fichier de données d'exploration ou de ressources au format WikiText vers un fichier json
import os
import io
import json
import re
from colorama import init as colorama_init, Fore, Back, Style

mixins_files = [
    "Cartes/Définitions des groupes normés",
    "Cartes/Icônes des ravitaillements",
    "Cartes/Obélisques Lost Island"
    "Cartes/Grottes Lost Island"
]
wikitext_input = "J:/ARK/Ressources/Lost_Island/Exploration_Lost_Island.wt"
json_output = "Lost_Island/Lost_Island_Exploration_1.json"
backgrounds = [ { "name": "Croquis", "image": "Lost Island Map.jpg" } ]
explorer_notes_file = None #"Ragnarok/Explorer_Notes_GP2_FR.json"
group_with_label = [
    "dossier", 
    "poi", 
    "glitch", 
    "explorer-note", 
    "terminal", 
    "mission-terminal"
]
rename_group_dict = {
    "note" : "explorer-note",
    "cave-loot": "cave-crate",
    "crate": "surface-crate",
    "sea-loot": "sea-crate"
}
explorer_note_dict = {
    "Découverte d'" : "Découverte de ",
    "Meiyin": "Mei-Yin",
    "Dinosaures corrompus": "Créatures corrompues",
    "Gasbag": "Sacagaz",
    "MegaMek": "Mega Mek"
}
custom_groups = {
    "beaver-dam": {
        "name": "Barrage d'un Castoroides",
        "size": 5.5,
        "icon": "Giant Beaver Dam.png",
		"fillColor": "#76432f",
		"borderColor": "#fffbf3"
    }
}
artifacts_file = "artifacts.json"
crate_level_IS = { # The Island/The Center/Ragnarok 
    '03': 'W',
    '15': 'G',
    '25': 'B',
    '35': 'P',
    '45': 'Y',
    '60': 'R',
    '70': 'R'
}
crate_level_SE = { # Scorched Earth 
    '03': 'W',
    '15': 'G',
    '30': 'B',
    '45': 'P',
    '55': 'Y',
    '60': 'R',
    '70': 'R'
}
#c0315254560 or c0315254570
#"cc:wgbyr": { "overrideIcon": "PieWGBYR.svg" }
def decouper_en_blocs_de_2(caracteres):
    return [caracteres[i:i+2] for i in range(0, len(caracteres), 2)]
def _build_crate_group(crate_level, group_def: str):
    split = decouper_en_blocs_de_2(group_def[1:])
    cc = "cc:"
    overrideIcon = "Pie"
    for c in split:
        if c not in crate_level:
            print(f"{Fore.CYAN}Ne trouve pas le level {c} dans la liste 'crate_level'{Style.RESET_ALL}")
            return None
        else:
            c = crate_level[c]
            cc = cc + c.lower()
            overrideIcon = overrideIcon + c
    return f'"{cc}": {{ "overrideIcon": "{overrideIcon}.svg" }}'
def build_crate_group(group_def: str):
    result = _build_crate_group(crate_level_IS, group_def)
    if result == None:
        result = _build_crate_group(crate_level_SE, group_def)
    return result

#cave-crate cave cg:11 Ccc:br
#surface-crate cg:21 cc:bgw
#sea-crate cg:22
#artifact cg:1 cc:immune

# === wikitext process ===
def load_artifacts(path: str, filename: str):
    with io.open(os.path.join(path,filename), mode="r", encoding="utf-8") as json_file:
        json_data = json.load(json_file)
        return json_data

mixins_groups = {}
mixins_markers = {}
mixins_layers = {}
def load_mixins(path: str, filename: str):
    with io.open(os.path.join(path,filename), mode="r", encoding="utf-8") as json_file:
        json_data = json.load(json_file)
        if 'groups' in json_data:
            data = json_data['groups']
            for item in data:
                mixins_groups[item] = data[item]
        if 'markers' in json_data:
            data = json_data['markers']
            for item in data:
                mixins_markers[item] = data[item]
        if 'layers' in json_data:
            data = json_data['layers']
            for item in data:
                mixins_layers[item] = data[item]

explorer_notes = []
def load_explorer_notes(path, filename):
    with io.open(os.path.join(path,filename), mode="r", encoding="utf-8") as json_file:
        json_data = json.load(json_file)
        if 'table' in json_data:
            data = json_data['table']
            for item in data:
                if 'Lat' in item and 'Lon' in item and len(item['Lat']) > 0 and len(item['Lon']) > 0:
                    explorer_notes.append(item)

def rename_group(group: str):
    if group in rename_group_dict:
        return rename_group_dict[group]
    else:
        return group

def remove_first_comma(label: str):
    split = [s.strip() for s in list(filter(None, label.split(',')))]
    if len(split) == 2:
        new_name = split[1].replace(': ', ' ')
        label = new_name.replace(':', ' ')
    return label

# Chroniques #16
# 
#[[Notes des explorateurs/HLN-A#Chroniques de Genesis 2 #16 (Extinction)|Chroniques #16]]
#                              ^                            ^
#[[Notes des explorateurs#Note de Mei-Yin #3 (Extinction)|Note #3]]
#                        ^                   ^
#[[Notes des explorateurs/HLN-A#Découverte de HLN-A #12 (Extinction)|Découverte #12]]
#                              ^                        ^
#{{ItemLink|Créatures corrompues}}
#          ^                    ^
#{{ItemLink|Exécuteur|text=Manuel : Exécuteur|image=Exécuteur.png}} => Exécuteur
#          ^         ^
def extract_note_name(label: str):
    result = None
    pos = label.find('#')
    if pos > 0:
        pos2 = label.find('(', pos)
        if pos2 == -1:
            pos2 = label.find('|', pos)
        result = label[pos+1:pos2].strip()
    else:
        pos = label.find('|')
        if pos > 0:
            pos2 = label.find('|', pos+1)
            if pos2 == -1:
                result = label[pos+1:len(label)-2].strip()
            else:
                result = label[pos+1:pos2].strip()
    return result.lower()

def find_coord_in_explorer_note(lat: str, lon: str):
    lat_in = float(lat)
    lon_in = float(lon)
    notes = []
    for item in explorer_notes:
        ilat = float(item['Lat'])
        ilon = float(item['Lon'])
        elat = abs(lat_in - ilat)
        if elat < 1:
            elon = abs(lon_in - ilon)
            if elon < 1:
                item['elat'] = elat
                item['elon'] = elon
                notes.append(item)
    if len(notes) == 1:
        return notes[0]
    elif len(notes) == 0:
        print(f"{Fore.CYAN}Ne trouve pas la note ({lat};{lon}) dans le fichier '{explorer_notes_file}'{Style.RESET_ALL}")
        os._exit(0)
    else:
        result = sorted(notes, key=lambda x: x['elat']*x['elon']) 
        return result[0]

# label = 'Chroniques de Genesis 2 #16'
# label = 'Dossier Dinosaures corrompus' => 'Créatures corrompues'
# Attention si le label est 'Découverte d'HLN-A #12' !!!!
# Attention: 'Note de 'Meiyin' => 'Mei-Yin'
# Attention: 'Celle qui attend' => 'Celle Qui Attend'
def find_key_and_replace(label: str):
    for key, value in explorer_note_dict.items():
        if key in label:
            return label.replace(key, value)
    return label

def find_explorer_note(label: str, lat: str, lon: str):
    if "Dossier " in label:
        label = label[8:]
    elif "Manuel " in label:
        label = label[7:]
    label = find_key_and_replace(label)
    result = next((x for x in explorer_notes if extract_note_name(x['Sujet']) == label.lower()), None)
    if result == None:
        result = find_coord_in_explorer_note(lat, lon)
        if result == None:
            print(f"{Fore.YELLOW}Ne trouve pas : '{label}'{Style.RESET_ALL}")
    return result

def find_in_mixin_layers(icon: str):
    for (key, value) in mixins_layers.items():
        if 'overrideIcon' in value and value['overrideIcon'] == icon:
            return key
    return None

# process input
alias = {}
groups = {}
markers = {}
group_to_remove = []
artifact_idx = 1
colorama_init()
wd = os.path.dirname(__file__)
if explorer_notes_file != None:
    load_explorer_notes(wd, explorer_notes_file)
for filename in mixins_files:
    load_mixins(wd, filename.replace(' ','_') + ".json")
artifacts = load_artifacts(wd, artifacts_file)
state = 0 # recherche de 'ResourceMap'
with io.open(os.path.join(wd,wikitext_input), mode="r", encoding="utf-8") as file_in:
    lines = file_in.readlines()
    for line in lines:
        if state == 0:
            if 'ResourceMap' in line:
                state = 1
        else:
            if len(line) > 3 and line[0] == '|':
                line = line.rstrip()
                # marker or group or command ?
                if '=' in line:
                    split = [s.strip() for s in list(filter(None, line[1:].split(' ')))]
                    if split[0] == "marker": # => group
                        if " name = " in line:
                            #| marker <label> name = <alias>
                            pos = line.index(" name = ")
                            label = split[1]
                            # "alias":{"<label>":"<alias>"}
                            alias[label] = line[pos+8:].strip()
                            print(f"'{line}' ===> alias '{label}': '{alias[label]}'")
                        elif " icon = " in line:
                            #| marker <group> icon = <icon>
                            pos = line.index(" icon = ")
                            group = split[1]
                            # {{filepath:PieBPYR.svg}} => PieBPYR.svg
                            icon = split[4].split(':')[1][:-2]
                            # "groups": {"<group>": {"name":"<group>", "icon":"<icon>"}}
                            groups[group] = {"name":group,"icon":icon}
                            print(f"'{line}' ===> groups '{group}' : '{groups[group]}'")
                        else:
                            #| marker <group>   = <size>
                            pos = line.index(" = ")
                            group = line[9:pos]
                            size = line[pos+3:]
                            # find in mixins_groups
                            group = rename_group(group)
                            if group not in mixins_groups:
                                # "groups": {"<group>": {"name":"<group>", "icon":"Blank.png", "size":<size>}}
                                groups[group] = {"name":group,"icon":"Blank.png", "size":int(size)}
                                print(f"'{line}' ===> groups '{groups[group]}'")
                else: # poi => marker
                    #| <lat>, <lon>, <group>,<label>
                    # "marker": {"<group>":{"lat":<lat>, "lon":<lon>, "name":"<label>", "image":"Blank.png"}}
                    split = [s.strip() for s in list(filter(None, line[1:].split(',')))]
                    lat = split[0]
                    lon = split[1]
                    group = split[2]
                    # find in mixins_markers
                    pos = line.find(f"{group},")
                    if pos > 0:
                        label = line[pos:]
                    else:
                        label = group
                    label = remove_first_comma(label)
                    group_ext = None
                    if 'crate' in group: # composed group (crate c25354560)
                        split = group.split()
                        group = split[0]
                        group_ext = split[1]
                    group = rename_group(group)
                    if group not in mixins_markers:
                        if group == "cave-crate":
                            group = "cave-crate cave"
                        if group == "artifact":
                            artifact_ref = artifacts[label]
                            artifact_item = {"lat":float(lat), "lon":float(lon), "name": label, "description": ""}
                            for (key,value) in artifact_ref.items():
                                artifact_item[key] = value
                            group = artifact_ref['tag']
                            del artifact_item['tag']
                            markers[group] = [artifact_item]
                            print(f"'{line}' ===> markers '{group}': '{lat}', '{lon}', '{label}'")
                            artifact_idx = artifact_idx + 1
                        elif group == "cave-entrance":
                            if group not in markers:
                                markers[group] = []
                            markers[group].append({"lat":float(lat), "lon":float(lon), "name":label, "image": "Blank.png"})
                            print(f"'{line}' ===> markers '{group}': '{lat}', '{lon}', '{label}'")
                        elif group in group_with_label :
                            if group not in markers:
                                markers[group] = []
                            if group == "explorer-note" or group == "glitch" or group == "dossier":
                                note_desc = find_explorer_note(label, lat, lon)
                                if note_desc != None:
                                    ID = note_desc['ID']
                                    label = f"{label} <span class=\"datamap-explorer-note-id\">(ID: {ID})</span>"
                            markers[group].append({"lat":float(lat), "lon":float(lon), "name":label})
                            print(f"'{line}' ===> markers '{group}': '{lat}', '{lon}', '{label}'")
                        else: # marker simple
                            if group_ext != None:
                                # crate c25354560 => find group
                                if group_ext in groups:
                                    group_def = groups[group_ext]
                                    result = find_in_mixin_layers(group_def['icon'])
                                    if result != None:
                                        group = f"{group} {group_ext} {result}"
                                        if group_ext not in group_to_remove:
                                            group_to_remove.append(group_ext)
                                    else:
                                        print(f"{Fore.YELLOW}Ne trouve pas la définition du groupe pour '{group}': '{group_ext}' => '{group_def['icon']}'{Style.RESET_ALL}")
                                        result = build_crate_group(group_ext)
                                        print(f"{Fore.GREEN}Créer le groupe : {result} {Style.RESET_ALL}")
                                else:
                                    print(f"{Fore.YELLOW}Ne trouve pas le groupe : '{group_ext}'{Style.RESET_ALL}")
                                    result = build_crate_group(group_ext)
                                    print(f"{Fore.GREEN}Créer le groupe : {result} {Style.RESET_ALL}")
                            if group not in markers:
                                markers[group] = []
                            markers[group].append({"lat":float(lat), "lon":float(lon)})
                            print(f"'{line}' ===> markers '{group}': '{lat}', '{lon}'")
                    else: # group in mixins_markers -> check if correct
                        ms = mixins_markers[group]
                        found = False
                        for m in ms:
                            if m['lat'] == float(lat) and m['lon'] == float(lon):
                                found = True
                                break
                        if found == False:
                            print(f"{Fore.YELLOW}CHECK => {group}: {label} ({lat},{lon}){Style.RESET_ALL}")
                            if group not in markers:
                                markers[group] = []
                            markers[group].append({"name":label, "lat":float(lat), "lon":float(lon)})

# check if all group in markers are referenced
for marker in markers:
    if "artifact" in marker:
        marker = "artifact"
    elif marker == "cave-crate cave":
        marker = "cave-crate"
    elif "crate " in marker:
        marker = marker.split(' ')[1]
    if marker not in mixins_groups and marker not in custom_groups and marker not in groups:
        print(f"{Fore.YELLOW}Ne trouve pas le groupe : '{marker}'{Style.RESET_ALL}")
# remove group to remove
for g in group_to_remove:
    del groups[g]
# add custom groups
for (key, value) in custom_groups.items():
    groups[key] = value
# check for unused group
group_to_keep = {}
for marker in markers:
    for g in groups:
        if re.search(r'\b' + g + r'\b', marker):
            if g not in group_to_keep:
                group_to_keep[g] = 0
            group_to_keep[g] = group_to_keep[g] + 1
group_to_remove = []
for g in groups:
    if g not in group_to_keep:
        group_to_remove.append(g)
for g in group_to_remove:
    del groups[g]
# sort markers
sorted_markers = {}
markers_sorted_keys = sorted(markers.keys())
for key in markers_sorted_keys:
  sorted_markers[key] = markers[key]
# format output json
json_out = {}
json_out["$schema"] = "https://ark.wiki.gg/extensions/DataMaps/schemas/v0.16.json"
json_out["mixins"] = mixins_files
json_out["backgrounds"] = backgrounds
json_out["settings"] = {"enableSearch": True}
if len(alias) > 0:
    json_out["alias"] = alias
if len(groups) > 0:
    json_out["groups"] = groups
json_out["markers"] = sorted_markers
with io.open(os.path.join(wd,json_output), mode="w", encoding="utf-8") as json_file:
    json.dump(json_out, json_file, ensure_ascii=False, indent="  ")