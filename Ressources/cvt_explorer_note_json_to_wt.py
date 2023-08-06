# Wiki ARK Survival Evolved
# Script de conversion d'un fichier de données de notes d'explorateur au format json vers un fichier au format WikiText
import os
import io
import json

json_input = "Genesis_Part_2/Explorer_Notes_GP2_FR.json"
wikitext_output = "Genesis_Part_2/Explorer_Notes_GP2_FR_1.wt"

# 1) wiki_text.begin_lines
# 2) header => !<0> !! <1> !! .....
# 3) table  => 
#|-
#| <0> || <1> || ...
# 4) wiki_text.end_lines


# process input
wd = os.path.dirname(__file__)
out_lines = []
json_in = {}
with io.open(os.path.join(wd,json_input), mode="r", encoding="utf-8") as file_in:
    json_in = json.load(file_in)
# begin lines
for line in json_in['wiki_text']['begin_lines']:
    out_lines.append(line + "\n")
# header
line = "!"
for key in json_in['header']:
    line += f"{key} !! "
line = line[:-4]
out_lines.append(line + "\n")
# table
for item in json_in['table']:
    out_lines.append("|-\n")
    line = "| "
    for key in json_in['header']:
        line += f"{item[key]} || "
    line = line[:-4]
    out_lines.append(line + "\n")
# begin lines
for line in json_in['wiki_text']['end_lines']:
    out_lines.append(line + "\n")
# write to file
with io.open(os.path.join(wd,wikitext_output), mode="w", encoding="utf-8") as file_out:
    file_out.writelines(out_lines)