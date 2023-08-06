# Wiki ARK Survival Evolved
# Script de conversion d'un fichier de données de notes d'explorateur (FR ou EN) au format WikiText vers un fichier json
import os
import io
import json
from colorama import init as colorama_init, Fore, Back, Style

wikitext_input = "Genesis_Part_2/Explorer_Notes_GP2_FR.wt"
json_output    = "Genesis_Part_2/Explorer_Notes_GP2_FR.json"
json_exp_input = None #"Genesis_Part_1/Genesis_Part_1_Exploration.json"
actors_file    = "Genesis_Part_2/actors.json"
to_add = None #[
#    {'Lat':46.07002, 'Lon':86.43435, 'ID':876}
#]

actors = []
def load_actors(path: str,filename: str):
    with io.open(os.path.join(wd,filename), mode="r", encoding="utf-8") as json_file:
        json_data = json.load(json_file)
        if 'notes' in json_data:
            data = json_data['notes']
            for item in data:
                actors.append(item)
        if 'glitches' in json_data:
            data = json_data['glitches']
            for item in data:
                if 'noteId' in item:
                    item['noteIndex'] = item['noteId']
                actors.append(item)
        print(f"{Fore.CYAN}actors = {len(actors)}{Style.RESET_ALL}")

def find_actor(lat: str, lon: str):
    lat_in = float(lat)
    lon_in = float(lon)
    notes = []
    for item in actors:
        ilat = item['lat']
        ilon = item['long']
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
        print(f"{Fore.CYAN}Ne trouve pas la note ({lat};{lon}) dans le fichier '{actors_file}'{Style.RESET_ALL}")
        os._exit(0)
    else:
        result = sorted(notes, key=lambda x: x['elat']*x['elon']) 
        return result[0]

json_exploration = []
def load_json_exploration(path: str, json_input: str):
    if json_input != None:
        with io.open(os.path.join(path,json_input), mode="r", encoding="utf-8") as file_in:
            json_data = json.load(file_in)
            for item in json_data['markers']['explorer-note']:
                json_exploration.append(item)
            for item in json_data['markers']['dossier']:
                json_exploration.append(item)
            for item in json_data['markers']['glitch']:
                json_exploration.append(item)
            print(f"{Fore.CYAN}nb_exp_notes = {len(json_exploration)}{Style.RESET_ALL}")

def find_note_in_table(table: dict, lat_in: float, lon_in: float, precision: int = 0.1):
    notes = []
    for item in table:
        if item['Lat'] != 'N/A' and len(item['Lat']) != 0:
            lat = float(item['Lat'])
            lon = float(item['Lon'])
            elat = abs(lat_in - lat)
            if elat < precision:
                elon = abs(lon_in - lon)
                if elon < precision:
                    item['elat'] = elat
                    item['elon'] = elon
                    notes.append(item)
    if len(notes) == 1:
        return notes[0]
    elif len(notes) == 0:
        return None
    else:
        result = sorted(notes, key=lambda x: x['elat']*x['elon']) 
        return result[0]

def find_note_id_in_table(tabel: dict, id: int):
    for item in table:
        if 'ID' in item and item['ID'] == id:
            return item
    return None

# process input
colorama_init()
print(f"{Fore.CYAN}Analyse de {wikitext_input}{Style.RESET_ALL}")
wd = os.path.dirname(__file__)
load_actors(wd, actors_file)
load_json_exploration(wd, json_exp_input)
#=== Extinction ===
#{| class="wikitable sortable mw-collapsible mw-collapsed"
#!Type !! Sujet !! Auteur !! Lat !! Lon !! Lieux
#|-
#| Dossier || {{ItemLink|Créatures corrompues}} || [[Notes des explorateurs#Helena Walker|Helena]] || 56 || 89.3 || [[Terres désolées (Extinction)|Terres désolées]]
# ....
#|-
#| Chroniques de Genesis 2 || [[Notes des explorateurs/Santiago#Chroniques de Genesis 2 #20 (Extinction)|Chroniques #20]] || [[Notes des explorateurs#Santiago|Santiago]] || 13.9 || 83.6 || [[Dôme de neige (Extinction)|Dôme de neige]]
#|}</div>
#
#<div style="display:inline-block;vertical-align:top;margin:10px">

#!Type !! Topic !! Author !! Lat !! Lon !! Location !! ID
#|-
#| Dossier || {{ItemLink|Basilisk}} || [[Explorer Notes#Helena Walker|Helena]] || 36.4 || 56.1 || [[River Valley (Aberration)|River Valley]] || 493
# ....
#|-
#|Genesis 2 Chronicles || [[Explorer Notes#Santiago Genesis 2 Chronicles #15 (Aberration)|Chronicles #15]] || [[Explorer Notes#Santiago|Santiago]] || 43.8 || 12.5 || [[The Ancient Device (Aberration)|The Ancient Device]] || 
#|}</div>
#
#<div style="display:inline-block;vertical-align:top;margin:10px">
json_out = {}
wt_begin_lines = []
wt_end_lines = []
header = None
table = []
lat_idx = 0
lon_idx = 0
ctr = 0
with io.open(os.path.join(wd,wikitext_input), mode="r", encoding="utf-8") as file_in:
    lines = file_in.readlines()
    state = 0
    for line in lines:
        line = line.rstrip()
        if state == 0:
            if line[0] == '!': # table header
                state = 1
                header = [s.strip() for s in list(line[1:].split('!!'))]
                lat_idx = header.index('Lat')
                lon_idx = header.index('Lon')
            else:
                wt_begin_lines.append(line)
        elif state == 1:
            if line.find("|-") == 0:
                pass
            elif line.find("|}") == 0:
                wt_end_lines.append(line)
                state = 2
            else:
                item = [s.strip() for s in list(line[2:].split('||'))]
                if len(item) != len(header):
                    print(f"{Fore.YELLOW}Item '{item}' n'a pas le même nombre d'élément que le header '{header}'{Style.RESET_ALL}")
                    os._exit(0)
                json_item = {}
                for i in range(len(header)):
                    json_item[header[i]] = item[i]
                if 'ID' not in json_item or json_item['ID'] == None or len(json_item['ID']) == 0:
                    if len(item[lat_idx]) != 0 and len(item[lon_idx]) != 0:
                        print(f"{Fore.GREEN}Recherche dans 'actors' de l'ID de ({item[lat_idx]};{item[lon_idx]}){Style.RESET_ALL}")
                        note = find_actor(item[lat_idx], item[lon_idx])
                        print(f"{item[1]} => {Fore.CYAN}{note['noteIndex']}{Style.RESET_ALL}")
                        json_item['ID'] = int(note['noteIndex'])
                    else:
                        print(f"{Fore.YELLOW}L'élément {item[1]} n'a pas de coordonnées !{Style.RESET_ALL}")
                        json_item['ID'] = ''
                else:
                    print(f"{item[1]} => {item['ID']}")
                    json_item['ID'] = int(json_item['ID'])
                json_item['index'] = ctr
                table.append(json_item)
                ctr = ctr + 1
        elif state == 2:
            wt_end_lines.append(line)

if to_add != None:
    for note in to_add:
        result = find_note_id_in_table(table, note['ID'])
        if result == None:
            print(f"{Fore.YELLOW}Ne trouve pas de référence existante dans table pour l'ID {id}{Style.RESET_ALL}")
        else:
            for key in header:
                if key not in note:
                    note[key] = result[key]
            index = table.index(result)
            table.insert(index, note)

print(f"{Fore.CYAN}table = {len(table)}{Style.RESET_ALL}")
if len(actors) != len(table) or (len(json_exploration) != len(table) and len(json_exploration) > 0) or (len(actors) != len(json_exploration) and len(json_exploration) > 0):
    print(f"{Fore.YELLOW}Pas le même nombre d'éléments dans les 3 tables: actors = {len(actors)}, exp_notes = {len(json_exploration)}, table = {len(table)}{Style.RESET_ALL}")

# verif coherance de table
ctr = 0
for item in json_exploration:
    result = find_note_in_table(table, float(item['lat']), float(item['lon']))
    if result == None:
        print(f"{Fore.YELLOW}[{ctr}]: Ne trouve pas la note ({item['lat']};{item['lon']}): {item['name']}{Style.RESET_ALL}")
    #else:
    #    print(f"[{ctr}]: ({item['lat']};{item['lon']}) == ({result['Lat']};{result['Lon']}) : ID {result['ID']}, '{result['Sujet']}' => '{item['name']}'")
    ctr = ctr + 1

if len(json_exploration) == 0:
    for item in actors:
        result = find_note_in_table(table, item['lat'], item['long'])
        if result == None:
            print(f"{Fore.YELLOW}[{ctr}]: Ne trouve pas la note ({item['lat']};{item['long']}){Style.RESET_ALL}")
        else:
            if 'found' in result:
                print(f"{Fore.YELLOW}[{ctr}] élément déjà) trouvé : {result['found']} => {result['Sujet']}{Style.RESET_ALL}")
            result['found'] = item['noteIndex']

# cleaning table
for item in table:
    if 'elat' in item:
        del item['elat']
    if 'elon' in item:
        del item['elon']
    if 'found' in item:
        del item['found']
    if 'index' in item:
        del item['index']

if 'ID' not in header:
    header.append('ID')

wt = {}
wt['begin_lines'] = wt_begin_lines
wt['end_lines'] = wt_end_lines
json_out['header'] = header
json_out['table'] = table
json_out['wiki_text'] = wt

with io.open(os.path.join(wd,json_output), mode="w", encoding="utf-8") as json_file:
    json.dump(json_out, json_file, ensure_ascii=False, indent="  ")