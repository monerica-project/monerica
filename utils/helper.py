""" A script to interact with the yaml. Move to your /tmp dir and play with it
"""

import yaml
from yaml.resolver import BaseResolver

# Keeps our strings just how we like them using overrides
class folded_unicode(str): pass
class DoubleQuoted(str): pass

def folded_unicode_representer(dumper, data):
    return dumper.represent_scalar(u'tag:yaml.org,2002:str', data, style='>')

def represent_double_quoted(dumper, data):
  return dumper.represent_scalar(BaseResolver.DEFAULT_SCALAR_TAG, data, style='"')

yaml.add_representer(folded_unicode, folded_unicode_representer)
yaml.add_representer(DoubleQuoted, represent_double_quoted)


with open('DIRECTORY.yaml', 'r') as file:
    data = yaml.safe_load(file)


def walk(data):
    for business, subcategory in data.items():
        for _, info in subcategory.items():
            for entry in info:
                name = entry['name']
                link = entry['link']        
                trusted = entry['trusted']
                desc = entry['description']
                # make modifiactions here

                # preserve our string formatting
                entry['description'] = folded_unicode(desc)
                entry['name'] = DoubleQuoted(name)
                entry['link'] = DoubleQuoted(link)
                entry['trusted'] = DoubleQuoted(trusted)
    return data

updated_data = walk(data)
with open('updated_dir.yaml', 'w') as file:
    yaml.dump(updated_data, file, sort_keys=False, allow_unicode=True)