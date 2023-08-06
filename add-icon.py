import os, io

# list icon `Ressources/Icons`
def list_file(path):
    result = []
    for item in os.listdir(path):
        if os.path.isfile(os.path.join(path, item)):
            result.append(item)
    return result

def copy_file(name, new_name):
    filename = name
    if os.path.isfile(filename):
        new_filename = new_name
        if os.path.isfile(new_filename):
            os.remove(new_filename)
        os.rename(filename, new_filename)
        print(f"Copie de {filename} vers {new_filename}")

icon_list = list_file("Ressources/Icons")

# vs2022 project file
copy_file("ARKInteractiveMap.csproj", "ARKInteractiveMap.csproj.bak")
lines_out = []
with io.open("ARKInteractiveMap.csproj.bak", mode="r", encoding="utf-8") as file_in:
    lines = file_in.readlines()
    find_icons = 0
    for line in lines:
        # find <EmbeddedResource Include="Ressources\Icons\
        if find_icons == 0 and '<EmbeddedResource Include="Ressources\\Icons\\' in line:
            find_icons = 1
        elif find_icons == 1:
            if "</ItemGroup>" in line:
                find_icons = 2
                for icon in icon_list:
                    lines_out.append(f'    <EmbeddedResource Include="Ressources\\Icons\\{icon}" />\n')
                lines_out.append(line)
        else:
            lines_out.append(line)

with io.open("ARKInteractiveMap.csproj", mode="w", encoding="utf-8") as file_out:
    file_out.writelines(lines_out)
    file_out.flush()
print("ok")
